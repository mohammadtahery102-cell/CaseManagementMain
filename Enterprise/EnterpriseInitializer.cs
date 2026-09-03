using System;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Enterprise
{
    // ─────────────────────────────────────────────────────────────────────────
    // فاز یک «هسته سازمانی» (Enterprise Core) — ساخت جداول پایگاه‌داده.
    // آموزش: این کلاس دقیقاً مثل AccountingInitializer کار می‌کند؛ کاملاً
    // افزایشی است و هیچ جدول یا ستون موجود نرم‌افزار را تغییر نمی‌دهد یا حذف
    // نمی‌کند. فقط جداول جدید با پیشوند Ent را در صورت نبود می‌سازد.
    // همه جداول ستون CenterID دارند تا با معماری چندمرکزی موجود
    // (SecurityContext/CenterGuard) سازگار باشند.
    //
    // موتورها عمداً «عمومی» طراحی شده‌اند: به‌جای وابستگی به TblCase، هر رکورد
    // با جفت (EntityName, EntityID) شناخته می‌شود. در فاز یک فقط موجودیت
    // "TblCase" به آن‌ها وصل می‌شود، اما ماژول‌های دیگر بعداً بدون تغییر
    // ساختار دیتابیس قابل اتصال‌اند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class EnterpriseInitializer
    {
        // نام موجودیتِ پیش‌فرض فاز یک (پرونده). هرجا EntityName لازم است از
        // همین ثابت استفاده می‌شود تا رشته تکراری در کد پخش نشود.
        public const string EntityCase = "TblCase";

        public static void EnsureEnterpriseObjects()
        {
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                EnsureWorkflowObjects(con);
                EnsureApprovalObjects(con);
                EnsureTaskObjects(con);
                EnsureRuleObjects(con);
                EnsureLockObjects(con);
                EnsureVersionObjects(con);
                EnsureSecurityObjects(con);
                EnsureErrorLogObjects(con);
                EnsurePermissionObjects(con);
                EnsureModuleObjects(con);
            }

            // ثبت نقاط اتصال بین موتورها (بعد از ساخت جداول).
            ApprovalService.Install();
            PermissionService.Install();
        }

        // ═══════════════════════════════════════════════════════════════════
        // ویژگی ۱ — موتور گردش‌کار (Workflow Engine)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureWorkflowObjects(SQLiteConnection con)
        {
            // ─── تعریف گردش‌کار ───────────────────────────────────────────
            // یک گردش‌کار برای هر موجودیت (EntityName) تعریف می‌شود. چند
            // گردش‌کار برای یک موجودیت مجاز است اما فقط یکی می‌تواند IsActive
            // باشد (این قاعده در WorkflowService رعایت می‌شود).
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntWorkflow (
    WorkflowID  INTEGER PRIMARY KEY AUTOINCREMENT,
    Code        TEXT    NOT NULL UNIQUE,
    Name        TEXT    NOT NULL,
    EntityName  TEXT    NOT NULL,
    Description TEXT    NULL,
    IsActive    INTEGER NOT NULL DEFAULT 1,
    CenterID    INTEGER NULL,
    CreatedBy   TEXT    NULL,
    CreatedAt   TEXT    NOT NULL DEFAULT (datetime('now'))
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntWorkflow_Entity ON EntWorkflow(EntityName, IsActive);");

            // ─── مراحل (State) ───────────────────────────────────────────
            // IsInitial: مرحله شروع (فقط یکی در هر گردش‌کار).
            // IsFinal:   مرحله پایانی؛ رسیدن به آن، نمونه را Completed می‌کند.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntWorkflowState (
    StateID    INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkflowID INTEGER NOT NULL,
    Code       TEXT    NOT NULL,
    Name       TEXT    NOT NULL,
    IsInitial  INTEGER NOT NULL DEFAULT 0,
    IsFinal    INTEGER NOT NULL DEFAULT 0,
    SortOrder  INTEGER NOT NULL DEFAULT 0,
    Color      TEXT    NULL,
    CONSTRAINT FK_EntState_Workflow FOREIGN KEY (WorkflowID)
        REFERENCES EntWorkflow (WorkflowID) ON DELETE CASCADE
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntWorkflowState_WF ON EntWorkflowState(WorkflowID, SortOrder);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_EntWorkflowState_Code ON EntWorkflowState(WorkflowID, Code);");

            // ─── گذارها (Transition) ─────────────────────────────────────
            // RequiredPermission: کلید مجوز لازم برای اجرای این گذار (ویژگی ۹).
            //   خالی یعنی هر کسی که به فرم دسترسی دارد می‌تواند اجرا کند.
            // RequiresApproval:  اگر ۱ باشد، اجرای گذار مستقیم انجام نمی‌شود و
            //   یک درخواست تأیید چندسطحی ساخته می‌شود (ویژگی ۲).
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntWorkflowTransition (
    TransitionID       INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkflowID         INTEGER NOT NULL,
    FromStateID        INTEGER NOT NULL,
    ToStateID          INTEGER NOT NULL,
    Name               TEXT    NOT NULL,
    RequiredPermission TEXT    NULL,
    RequiresApproval   INTEGER NOT NULL DEFAULT 0,
    ApprovalChainID    INTEGER NULL,
    SortOrder          INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT FK_EntTransition_Workflow FOREIGN KEY (WorkflowID)
        REFERENCES EntWorkflow (WorkflowID) ON DELETE CASCADE
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntWorkflowTransition_From ON EntWorkflowTransition(WorkflowID, FromStateID);");

            // ─── نمونه در حال اجرا (Instance) ────────────────────────────
            // برای هر رکورد (EntityName, EntityID) حداکثر یک نمونه فعال.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntWorkflowInstance (
    InstanceID     INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkflowID     INTEGER NOT NULL,
    EntityName     TEXT    NOT NULL,
    EntityID       INTEGER NOT NULL,
    CurrentStateID INTEGER NOT NULL,
    Status         TEXT    NOT NULL DEFAULT 'جاری',
    CenterID       INTEGER NULL,
    StartedBy      TEXT    NULL,
    StartedAt      TEXT    NOT NULL DEFAULT (datetime('now')),
    CompletedAt    TEXT    NULL,
    CONSTRAINT FK_EntInstance_Workflow FOREIGN KEY (WorkflowID)
        REFERENCES EntWorkflow (WorkflowID) ON DELETE CASCADE
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntWorkflowInstance_Entity ON EntWorkflowInstance(EntityName, EntityID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntWorkflowInstance_State ON EntWorkflowInstance(CurrentStateID, Status);");

            // ─── تاریخچه گذارها ─────────────────────────────────────────
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntWorkflowHistory (
    HistoryID    INTEGER PRIMARY KEY AUTOINCREMENT,
    InstanceID   INTEGER NOT NULL,
    FromStateID  INTEGER NULL,
    ToStateID    INTEGER NOT NULL,
    TransitionID INTEGER NULL,
    ActionBy     TEXT    NULL,
    ActionAt     TEXT    NOT NULL DEFAULT (datetime('now')),
    Note         TEXT    NULL,
    CONSTRAINT FK_EntHistory_Instance FOREIGN KEY (InstanceID)
        REFERENCES EntWorkflowInstance (InstanceID) ON DELETE CASCADE
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntWorkflowHistory_Instance ON EntWorkflowHistory(InstanceID, ActionAt);");

            EnsureDefaultCaseWorkflow(con);
        }

        // گردش‌کار پیش‌فرض پرونده — مطابق همان وضعیت‌های سرویسی که هم‌اکنون در
        // TblLookup/ServiceStatus و FrmCase استفاده می‌شوند. هدف این است که
        // گردش‌کار از روز اول با داده واقعی سیستم هم‌خوان باشد و کاربر مجبور
        // به تعریف دستی نباشد. idempotent: اگر از قبل ساخته شده، کاری نمی‌کند.
        private static void EnsureDefaultCaseWorkflow(SQLiteConnection con)
        {
            const string code = "CASE_DEFAULT";

            long workflowId = ToInt64(Scalar(con,
                "SELECT WorkflowID FROM EntWorkflow WHERE Code = @Code", "@Code", code));

            if (workflowId > 0) return;

            Exec(con, @"
INSERT INTO EntWorkflow (Code, Name, EntityName, Description, IsActive, CreatedBy)
VALUES (@Code, 'گردش‌کار پرونده', @Entity, 'گردش‌کار پیش‌فرض بررسی و تأیید پرونده', 1, 'system');",
                "@Code", code, "@Entity", EntityCase);

            workflowId = ToInt64(Scalar(con,
                "SELECT WorkflowID FROM EntWorkflow WHERE Code = @Code", "@Code", code));

            if (workflowId <= 0) return;

            // مراحل پیش‌فرض — عمداً همان عبارات فارسی موجود در ServiceStatus
            // استفاده می‌شود تا نمایش برای کاربر آشنا باشد.
            AddState(con, workflowId, "DRAFT",    "پیش‌نویس",        1, 0, 10, "#94A3B8");
            AddState(con, workflowId, "PENDING",  "در انتظار تأیید", 0, 0, 20, "#F59E0B");
            AddState(con, workflowId, "REVIEW",   "در حال بررسی",    0, 0, 30, "#3B82F6");
            AddState(con, workflowId, "APPROVED", "تأیید شده",       0, 1, 40, "#22C55E");
            AddState(con, workflowId, "REJECTED", "رد شده",          0, 1, 50, "#EF4444");

            AddTransition(con, workflowId, "DRAFT",   "PENDING",  "ارسال برای تأیید", null,               0, 10);
            AddTransition(con, workflowId, "PENDING", "REVIEW",   "شروع بررسی",       "Workflow.Review",  0, 20);
            AddTransition(con, workflowId, "REVIEW",  "APPROVED", "تأیید نهایی",      "Workflow.Approve", 1, 30);
            AddTransition(con, workflowId, "REVIEW",  "REJECTED", "رد پرونده",        "Workflow.Approve", 0, 40);
            AddTransition(con, workflowId, "PENDING", "DRAFT",    "بازگشت به پیش‌نویس", "Workflow.Review", 0, 50);
        }

        private static void AddState(SQLiteConnection con, long workflowId, string code,
                                     string name, int isInitial, int isFinal, int sortOrder, string color)
        {
            Exec(con, @"
INSERT OR IGNORE INTO EntWorkflowState
    (WorkflowID, Code, Name, IsInitial, IsFinal, SortOrder, Color)
VALUES
    (@WF, @Code, @Name, @Init, @Final, @Sort, @Color);",
                "@WF",    workflowId,
                "@Code",  code,
                "@Name",  name,
                "@Init",  isInitial,
                "@Final", isFinal,
                "@Sort",  sortOrder,
                "@Color", color);
        }

        private static void AddTransition(SQLiteConnection con, long workflowId, string fromCode,
                                          string toCode, string name, string requiredPermission,
                                          int requiresApproval, int sortOrder)
        {
            long fromId = StateId(con, workflowId, fromCode);
            long toId   = StateId(con, workflowId, toCode);
            if (fromId <= 0 || toId <= 0) return;

            Exec(con, @"
INSERT INTO EntWorkflowTransition
    (WorkflowID, FromStateID, ToStateID, Name, RequiredPermission, RequiresApproval, SortOrder)
VALUES
    (@WF, @From, @To, @Name, @Perm, @Appr, @Sort);",
                "@WF",   workflowId,
                "@From", fromId,
                "@To",   toId,
                "@Name", name,
                "@Perm", (object)requiredPermission ?? DBNull.Value,
                "@Appr", requiresApproval,
                "@Sort", sortOrder);
        }

        private static long StateId(SQLiteConnection con, long workflowId, string code)
        {
            return ToInt64(Scalar(con,
                "SELECT StateID FROM EntWorkflowState WHERE WorkflowID = @WF AND Code = @Code",
                "@WF", workflowId, "@Code", code));
        }

        // ═══════════════════════════════════════════════════════════════════
        // ویژگی ۲ — سامانه تأیید چندسطحی (Multi-Level Approval)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureApprovalObjects(SQLiteConnection con)
        {
            // ─── زنجیره تأیید ────────────────────────────────────────────
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntApprovalChain (
    ChainID    INTEGER PRIMARY KEY AUTOINCREMENT,
    Code       TEXT    NOT NULL UNIQUE,
    Name       TEXT    NOT NULL,
    EntityName TEXT    NOT NULL,
    IsActive   INTEGER NOT NULL DEFAULT 1,
    CenterID   INTEGER NULL,
    CreatedBy  TEXT    NULL,
    CreatedAt  TEXT    NOT NULL DEFAULT (datetime('now'))
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntApprovalChain_Entity ON EntApprovalChain(EntityName, IsActive);");

            // ─── سطوح زنجیره ────────────────────────────────────────────
            // تأییدکننده هر سطح با یکی از این‌ها مشخص می‌شود (هر کدام پر شد):
            //   ApproverUserID     → یک کاربر مشخص
            //   ApproverRole       → هر کاربری با این نقش
            //   RequiredPermission → هر کاربری که این مجوز را دارد (ویژگی ۹)
            // اگر هر سه خالی باشند، فقط مدیر سیستم می‌تواند تأیید کند.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntApprovalLevel (
    LevelID            INTEGER PRIMARY KEY AUTOINCREMENT,
    ChainID            INTEGER NOT NULL,
    LevelNo            INTEGER NOT NULL,
    Name               TEXT    NOT NULL,
    ApproverRole       TEXT    NULL,
    ApproverUserID     INTEGER NULL,
    RequiredPermission TEXT    NULL,
    CONSTRAINT FK_EntLevel_Chain FOREIGN KEY (ChainID)
        REFERENCES EntApprovalChain (ChainID) ON DELETE CASCADE
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_EntApprovalLevel_No ON EntApprovalLevel(ChainID, LevelNo);");

            // ─── درخواست تأیید ──────────────────────────────────────────
            // WorkflowInstanceID/TargetStateID فقط وقتی پر می‌شوند که درخواست
            // از یک گذار گردش‌کار ساخته شده باشد؛ درخواست مستقل هم مجاز است.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntApprovalRequest (
    RequestID          INTEGER PRIMARY KEY AUTOINCREMENT,
    ChainID            INTEGER NOT NULL,
    EntityName         TEXT    NOT NULL,
    EntityID           INTEGER NOT NULL,
    Title              TEXT    NOT NULL,
    Reason             TEXT    NULL,
    Status             TEXT    NOT NULL DEFAULT 'در انتظار',
    CurrentLevelNo     INTEGER NOT NULL DEFAULT 1,
    WorkflowInstanceID INTEGER NULL,
    TransitionID       INTEGER NULL,
    TargetStateID      INTEGER NULL,
    CenterID           INTEGER NULL,
    RequestedBy        TEXT    NULL,
    RequestedByUserID  INTEGER NULL,
    RequestedAt        TEXT    NOT NULL DEFAULT (datetime('now')),
    CompletedAt        TEXT    NULL
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntApprovalRequest_Status ON EntApprovalRequest(Status, CurrentLevelNo);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntApprovalRequest_Entity ON EntApprovalRequest(EntityName, EntityID);");

            // ─── تصمیم‌های ثبت‌شده ──────────────────────────────────────
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntApprovalAction (
    ActionID   INTEGER PRIMARY KEY AUTOINCREMENT,
    RequestID  INTEGER NOT NULL,
    LevelNo    INTEGER NOT NULL,
    Decision   TEXT    NOT NULL,
    Comment    TEXT    NULL,
    ActionBy   TEXT    NULL,
    ActionByUserID INTEGER NULL,
    ActionAt   TEXT    NOT NULL DEFAULT (datetime('now')),
    CONSTRAINT FK_EntAction_Request FOREIGN KEY (RequestID)
        REFERENCES EntApprovalRequest (RequestID) ON DELETE CASCADE
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntApprovalAction_Request ON EntApprovalAction(RequestID, LevelNo);");

            EnsureDefaultCaseChain(con);
        }

        // زنجیره تأیید پیش‌فرض پرونده: دو سطح (مدیر مرکز، سپس مدیر کل).
        // گذار «تأیید نهایی» گردش‌کار پیش‌فرض به همین زنجیره وصل می‌شود.
        private static void EnsureDefaultCaseChain(SQLiteConnection con)
        {
            const string code = "CASE_APPROVAL";

            long chainId = ToInt64(Scalar(con,
                "SELECT ChainID FROM EntApprovalChain WHERE Code = @Code", "@Code", code));

            if (chainId <= 0)
            {
                Exec(con, @"
INSERT INTO EntApprovalChain (Code, Name, EntityName, IsActive, CreatedBy)
VALUES (@Code, 'تأیید پرونده', @Entity, 1, 'system');",
                    "@Code", code, "@Entity", EntityCase);

                chainId = ToInt64(Scalar(con,
                    "SELECT ChainID FROM EntApprovalChain WHERE Code = @Code", "@Code", code));

                if (chainId <= 0) return;

                Exec(con, @"
INSERT OR IGNORE INTO EntApprovalLevel (ChainID, LevelNo, Name, ApproverRole)
VALUES (@Chain, 1, 'تأیید مدیر مرکز', 'Admin');", "@Chain", chainId);

                Exec(con, @"
INSERT OR IGNORE INTO EntApprovalLevel (ChainID, LevelNo, Name, ApproverRole)
VALUES (@Chain, 2, 'تأیید مدیر کل', 'SuperAdmin');", "@Chain", chainId);
            }

            // اتصال گذار «نیازمند تأیید» گردش‌کار پیش‌فرض به این زنجیره — فقط
            // اگر هنوز به هیچ زنجیره‌ای وصل نشده باشد (تنظیم دستی کاربر حفظ شود).
            Exec(con, @"
UPDATE EntWorkflowTransition
SET    ApprovalChainID = @Chain
WHERE  RequiresApproval = 1
  AND  ApprovalChainID IS NULL
  AND  WorkflowID IN (SELECT WorkflowID FROM EntWorkflow WHERE Code = 'CASE_DEFAULT');",
                "@Chain", chainId);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ویژگی ۳ — مدیریت وظایف (Task Management)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureTaskObjects(SQLiteConnection con)
        {
            // وظیفه می‌تواند به یک رکورد وصل باشد (EntityName/EntityID) یا مستقل.
            // SourceType/SourceID نشان می‌دهد وظیفه دستی ساخته شده یا خودکار
            // (از سامانه تأیید یا موتور قواعد) تا وظایف خودکار تکراری نشوند.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntTask (
    TaskID           INTEGER PRIMARY KEY AUTOINCREMENT,
    Title            TEXT    NOT NULL,
    Description      TEXT    NULL,
    EntityName       TEXT    NULL,
    EntityID         INTEGER NULL,
    AssignedToUserID INTEGER NULL,
    AssignedToRole   TEXT    NULL,
    Priority         TEXT    NOT NULL DEFAULT 'متوسط',
    Status           TEXT    NOT NULL DEFAULT 'باز',
    DueDate          TEXT    NULL,
    SourceType       TEXT    NOT NULL DEFAULT 'دستی',
    SourceID         INTEGER NULL,
    CenterID         INTEGER NULL,
    CreatedBy        TEXT    NULL,
    CreatedByUserID  INTEGER NULL,
    CreatedAt        TEXT    NOT NULL DEFAULT (datetime('now')),
    CompletedBy      TEXT    NULL,
    CompletedAt      TEXT    NULL
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntTask_Assignee ON EntTask(AssignedToUserID, Status);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntTask_Role ON EntTask(AssignedToRole, Status);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntTask_Entity ON EntTask(EntityName, EntityID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntTask_Source ON EntTask(SourceType, SourceID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntTask_Due ON EntTask(Status, DueDate);");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ویژگی ۴ — موتور قواعد (Rule Engine)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureRuleObjects(SQLiteConnection con)
        {
            // قاعده = «اگر شرط برقرار بود، این کار را بکن».
            // شرط: ConditionField (نام ستون همان جدول) + Operator + ConditionValue
            // کار:  ActionType (هشدار / جلوگیری / وظیفه / ثبت رویداد) + ActionParam
            //
            // آموزش — چرا رشته‌ای و داده‌محور: تا مدیر سیستم بتواند بدون تغییر
            // کد و بدون کامپایل دوباره، قواعد سازمانی را تعریف/غیرفعال کند.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntRule (
    RuleID         INTEGER PRIMARY KEY AUTOINCREMENT,
    Code           TEXT    NOT NULL UNIQUE,
    Name           TEXT    NOT NULL,
    EntityName     TEXT    NOT NULL,
    EventName      TEXT    NOT NULL,
    ConditionField TEXT    NULL,
    Operator       TEXT    NULL,
    ConditionValue TEXT    NULL,
    ActionType     TEXT    NOT NULL,
    ActionParam    TEXT    NULL,
    Message        TEXT    NULL,
    Priority       INTEGER NOT NULL DEFAULT 0,
    StopOnMatch    INTEGER NOT NULL DEFAULT 0,
    IsActive       INTEGER NOT NULL DEFAULT 1,
    CenterID       INTEGER NULL,
    CreatedBy      TEXT    NULL,
    CreatedAt      TEXT    NOT NULL DEFAULT (datetime('now'))
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntRule_Event ON EntRule(EntityName, EventName, IsActive, Priority);");

            // تاریخچه اجرای قواعد — برای عیب‌یابی و پاسخ‌گویی.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntRuleLog (
    LogID      INTEGER PRIMARY KEY AUTOINCREMENT,
    RuleID     INTEGER NOT NULL,
    EntityName TEXT    NOT NULL,
    EntityID   INTEGER NULL,
    EventName  TEXT    NULL,
    Matched    INTEGER NOT NULL DEFAULT 0,
    Outcome    TEXT    NULL,
    CenterID   INTEGER NULL,
    RunBy      TEXT    NULL,
    RunAt      TEXT    NOT NULL DEFAULT (datetime('now'))
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntRuleLog_Rule ON EntRuleLog(RuleID, RunAt);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntRuleLog_Entity ON EntRuleLog(EntityName, EntityID);");

            EnsureDefaultRules(con);
        }

        // قواعد نمونه — عمداً «هشدار» هستند و نه «جلوگیری»، تا فعال‌شدنشان هیچ
        // عملیات موجودی را متوقف نکند. مدیر سیستم می‌تواند آن‌ها را تغییر دهد.
        private static void EnsureDefaultRules(SQLiteConnection con)
        {
            AddRule(con, "CASE_NO_PHONE", "پرونده بدون شماره تماس", EntityCase,
                    RuleEngine.EventAfterSave, "Phone", RuleEngine.OpEmpty, null,
                    RuleEngine.ActionWarn, null,
                    "برای این پرونده شماره تماس ثبت نشده است.", 10);

            AddRule(con, "CASE_HIGH_PRIORITY", "پیگیری پرونده اولویت اول", EntityCase,
                    RuleEngine.EventAfterSave, "PriorityLevel", RuleEngine.OpEquals, "اول",
                    RuleEngine.ActionTask, "Admin",
                    "پیگیری فوری پرونده با اولویت اول", 20);
        }

        private static void AddRule(SQLiteConnection con, string code, string name, string entityName,
                                    string eventName, string field, string op, string value,
                                    string actionType, string actionParam, string message, int priority)
        {
            Exec(con, @"
INSERT OR IGNORE INTO EntRule
    (Code, Name, EntityName, EventName, ConditionField, Operator, ConditionValue,
     ActionType, ActionParam, Message, Priority, IsActive, CreatedBy)
VALUES
    (@Code, @Name, @Entity, @Event, @Field, @Op, @Value,
     @Action, @Param, @Message, @Priority, 1, 'system');",
                "@Code",     code,
                "@Name",     name,
                "@Entity",   entityName,
                "@Event",    eventName,
                "@Field",    (object)field ?? DBNull.Value,
                "@Op",       (object)op ?? DBNull.Value,
                "@Value",    (object)value ?? DBNull.Value,
                "@Action",   actionType,
                "@Param",    (object)actionParam ?? DBNull.Value,
                "@Message",  (object)message ?? DBNull.Value,
                "@Priority", priority);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ویژگی ۵ — قفل رکورد (Record Locking)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureLockObjects(SQLiteConnection con)
        {
            // قفل «بدبینانه»: وقتی کاربری رکوردی را برای ویرایش باز می‌کند،
            // کاربر دیگر تا آزادشدن قفل نمی‌تواند همان رکورد را ویرایش کند.
            //
            // ExpiresAt تضمین می‌کند قفلِ جامانده (بسته‌شدن ناگهانی برنامه)
            // سیستم را برای همیشه قفل نکند؛ قفل منقضی خودکار نادیده گرفته
            // و بازنویسی می‌شود.
            //
            // UNIQUE روی (EntityName, EntityID) کلید درستیِ کار است: حتی اگر دو
            // کاربر هم‌زمان تلاش کنند، فقط یکی موفق به INSERT می‌شود.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntRecordLock (
    LockID      INTEGER PRIMARY KEY AUTOINCREMENT,
    EntityName  TEXT    NOT NULL,
    EntityID    INTEGER NOT NULL,
    UserID      INTEGER NULL,
    Username    TEXT    NULL,
    MachineName TEXT    NULL,
    CenterID    INTEGER NULL,
    AcquiredAt  TEXT    NOT NULL DEFAULT (datetime('now')),
    HeartbeatAt TEXT    NOT NULL DEFAULT (datetime('now')),
    ExpiresAt   TEXT    NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_EntRecordLock_Entity ON EntRecordLock(EntityName, EntityID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntRecordLock_User ON EntRecordLock(UserID);");

            // قفل‌های منقضی‌شده از اجرای قبلی در همان ابتدای کار پاک می‌شوند.
            Exec(con, "DELETE FROM EntRecordLock WHERE ExpiresAt <= datetime('now');");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ویژگی ۶ — تاریخچه نسخه‌ها (Version History)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureVersionObjects(SQLiteConnection con)
        {
            // از هر رکورد در هر بار ثبت/ویرایش/حذف، یک «عکس فوری» گرفته می‌شود.
            //
            // آموزش — چرا متن ساده و نه JSON: پروژه به هیچ کتابخانه JSON وابسته
            // نیست و افزودن وابستگی جدید ممنوع است. قالب «کلید=مقدار» در هر خط
            // هم برای مقایسه ساده است و هم مستقیماً برای انسان خواناست.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntRecordVersion (
    VersionID       INTEGER PRIMARY KEY AUTOINCREMENT,
    EntityName      TEXT    NOT NULL,
    EntityID        INTEGER NOT NULL,
    VersionNo       INTEGER NOT NULL,
    Operation       TEXT    NOT NULL,
    Snapshot        TEXT    NULL,
    ChangedFields   TEXT    NULL,
    CenterID        INTEGER NULL,
    ChangedBy       TEXT    NULL,
    ChangedByUserID INTEGER NULL,
    ChangedAt       TEXT    NOT NULL DEFAULT (datetime('now'))
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntRecordVersion_Entity ON EntRecordVersion(EntityName, EntityID, VersionNo);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntRecordVersion_Changed ON EntRecordVersion(ChangedAt);");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ویژگی ۷ — ممیزی امنیتی (Security Audit)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureSecurityObjects(SQLiteConnection con)
        {
            // آموزش — چرا جدول جدا از TblAuditLog: آن جدول «رویدادهای کسب‌وکاری»
            // (ثبت/ویرایش/حذف پرونده) را نگه می‌دارد و کاربران آن را به‌عنوان
            // گزارش کاری می‌بینند. رویدادهای امنیتی ماهیت متفاوتی دارند
            // (ورود ناموفق، رد دسترسی، تغییر نقش) و باید جداگانه، با سطح
            // اهمیت و بدون هم‌آمیختگی با گزارش‌های کاری نگهداری شوند.
            // TblAuditLog دست‌نخورده باقی می‌ماند (سازگاری کامل عقب‌رو).
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntSecurityEvent (
    EventID    INTEGER PRIMARY KEY AUTOINCREMENT,
    EventType  TEXT    NOT NULL,
    Severity   TEXT    NOT NULL DEFAULT 'عادی',
    Success    INTEGER NOT NULL DEFAULT 1,
    UserID     INTEGER NULL,
    Username   TEXT    NULL,
    MachineName TEXT   NULL,
    EntityName TEXT    NULL,
    EntityID   INTEGER NULL,
    Detail     TEXT    NULL,
    CenterID   INTEGER NULL,
    CreatedAt  TEXT    NOT NULL DEFAULT (datetime('now'))
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntSecurityEvent_Type ON EntSecurityEvent(EventType, CreatedAt);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntSecurityEvent_User ON EntSecurityEvent(Username, CreatedAt);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntSecurityEvent_Sev ON EntSecurityEvent(Severity, CreatedAt);");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ویژگی ۸ — ثبت متمرکز خطاها (Central Error Logging)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureErrorLogObjects(SQLiteConnection con)
        {
            // تا امروز خطاها فقط با Msg.Show به کاربر نشان داده می‌شدند و اثری
            // از آن‌ها باقی نمی‌ماند (جز audit_errors.log که فقط برای خطاهای
            // خودِ AuditLogger است). این جدول همه خطاها را در یک جای واحد
            // نگه می‌دارد تا پشتیبانی بتواند مشکل کاربر را بازسازی کند.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntErrorLog (
    ErrorID       INTEGER PRIMARY KEY AUTOINCREMENT,
    Source        TEXT    NULL,
    FormName      TEXT    NULL,
    Severity      TEXT    NOT NULL DEFAULT 'خطا',
    ExceptionType TEXT    NULL,
    Message       TEXT    NULL,
    StackTrace    TEXT    NULL,
    UserID        INTEGER NULL,
    Username      TEXT    NULL,
    MachineName   TEXT    NULL,
    CenterID      INTEGER NULL,
    IsResolved    INTEGER NOT NULL DEFAULT 0,
    ResolvedBy    TEXT    NULL,
    ResolvedAt    TEXT    NULL,
    Note          TEXT    NULL,
    CreatedAt     TEXT    NOT NULL DEFAULT (datetime('now'))
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntErrorLog_Created ON EntErrorLog(CreatedAt);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntErrorLog_Resolved ON EntErrorLog(IsResolved, CreatedAt);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntErrorLog_Source ON EntErrorLog(Source);");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ویژگی ۹ — ماتریس نقش و مجوز (Role & Permission Matrix)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsurePermissionObjects(SQLiteConnection con)
        {
            // فهرست مجوزهای شناخته‌شده سیستم.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntPermission (
    PermissionID INTEGER PRIMARY KEY AUTOINCREMENT,
    PermKey      TEXT    NOT NULL UNIQUE,
    Name         TEXT    NOT NULL,
    Category     TEXT    NOT NULL DEFAULT 'عمومی',
    Description  TEXT    NULL,
    SortOrder    INTEGER NOT NULL DEFAULT 0
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_EntPermission_Cat ON EntPermission(Category, SortOrder);");

            // مجوز در سطح نقش.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntRolePermission (
    RoleName  TEXT    NOT NULL,
    PermKey   TEXT    NOT NULL,
    IsGranted INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (RoleName, PermKey)
);");

            // استثنا در سطح کاربر — بر نقش اولویت دارد و می‌تواند «اجازه» یا
            // «منع صریح» باشد (IsGranted = 0 یعنی حتی اگر نقش اجازه داده، نه).
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntUserPermission (
    UserID    INTEGER NOT NULL,
    PermKey   TEXT    NOT NULL,
    IsGranted INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (UserID, PermKey)
);");

            EnsureDefaultPermissions(con);
        }

        // فهرست مجوزها و تخصیص پیش‌فرض آن‌ها به نقش‌ها.
        //
        // آموزش — مهم‌ترین قاعده این بخش «سازگاری عقب‌رو» است: مقادیر پیش‌فرض
        // عمداً دقیقاً همان اختیاراتی را می‌دهند که نقش‌ها همین حالا در
        // SecurityContext دارند (Admin همه‌کاره، Operator ویرایش بدون حذف،
        // Viewer فقط مشاهده). بنابراین روز اول پس از به‌روزرسانی، هیچ کاربری
        // چیزی کم یا زیاد نمی‌شود؛ از آن پس مدیر می‌تواند ماتریس را تغییر دهد.
        private static void EnsureDefaultPermissions(SQLiteConnection con)
        {
            // (کلید، نام، دسته، ترتیب، Admin, Operator, Viewer)
            AddPermission(con, "Case.View",     "مشاهده پرونده",   "پرونده", 10, true,  true,  true);
            AddPermission(con, "Case.Create",   "ثبت پرونده",      "پرونده", 20, true,  true,  false);
            AddPermission(con, "Case.Edit",     "ویرایش پرونده",   "پرونده", 30, true,  true,  false);
            AddPermission(con, "Case.Delete",   "حذف پرونده",      "پرونده", 40, true,  false, false);
            AddPermission(con, "Case.Export",   "خروجی پرونده",    "پرونده", 50, true,  true,  true);

            AddPermission(con, "Workflow.View",    "مشاهده گردش‌کار", "گردش‌کار", 60, true,  true,  true);
            AddPermission(con, "Workflow.Review",  "بررسی پرونده",    "گردش‌کار", 70, true,  true,  false);
            AddPermission(con, "Workflow.Approve", "تأیید نهایی",     "گردش‌کار", 80, true,  false, false);
            AddPermission(con, "Workflow.Manage",  "تعریف گردش‌کار",  "گردش‌کار", 90, true,  false, false);

            AddPermission(con, "Approval.Decide", "تصمیم‌گیری تأیید", "تأیید", 100, true, false, false);
            AddPermission(con, "Approval.Manage", "تعریف زنجیره تأیید", "تأیید", 110, true, false, false);

            AddPermission(con, "Task.View",   "مشاهده وظایف", "وظایف", 120, true, true,  true);
            AddPermission(con, "Task.Manage", "مدیریت وظایف", "وظایف", 130, true, false, false);

            AddPermission(con, "Rule.Manage", "مدیریت قواعد", "قواعد", 140, true, false, false);

            AddPermission(con, "Lock.Override", "آزادسازی اجباری قفل", "امنیت", 150, true, false, false);
            AddPermission(con, "Security.View", "مشاهده ممیزی امنیتی", "امنیت", 160, true, false, false);
            AddPermission(con, "Error.View",    "مشاهده گزارش خطاها",  "امنیت", 170, true, false, false);
            AddPermission(con, "Version.View",  "مشاهده تاریخچه نسخه‌ها", "امنیت", 180, true, true, true);

            AddPermission(con, "User.Manage",     "مدیریت کاربران", "سیستم", 190, true, false, false);
            AddPermission(con, "Permission.Manage", "مدیریت مجوزها", "سیستم", 200, true, false, false);
            AddPermission(con, "Module.Manage",   "مدیریت ماژول‌ها", "سیستم", 210, true, false, false);
            AddPermission(con, "Settings.Manage", "تنظیمات نرم‌افزار", "سیستم", 220, true, false, false);

            AddPermission(con, "Finance.View",    "بخش مالی",       "مالی", 230, true, true,  true);
            AddPermission(con, "Accounting.View", "حسابداری ایتام", "مالی", 240, true, true,  true);

            // ─── Sprint 0A — بستن شکاف‌های بدون بررسی مجوز (خانواده/اسناد/
            // متقاضی/روابط/بایگانی) و عملیات چاپ و خروجی و تاریخچه. مقادیر
            // پیش‌فرض دقیقاً همان دسترسی فعلی بر پایه SecurityContext را
            // بازتولید می‌کنند تا هیچ کاربری روز اول چیزی کم یا زیاد نشود.
            AddPermission(con, "Family.Edit",       "ویرایش عضو خانواده",   "خانواده", 250, true, true,  false);
            AddPermission(con, "Family.Delete",     "حذف عضو خانواده",      "خانواده", 260, true, false, false);
            AddPermission(con, "Family.Print",      "چاپ عضو خانواده",      "خانواده", 270, true, true,  true);

            AddPermission(con, "Docs.Edit",         "ویرایش سند",           "اسناد", 280, true, true,  false);
            AddPermission(con, "Docs.Delete",       "حذف سند",              "اسناد", 290, true, false, false);
            AddPermission(con, "Docs.Print",        "چاپ سند",              "اسناد", 300, true, true,  true);

            AddPermission(con, "Applicant.Edit",    "ویرایش متقاضی",        "متقاضیان", 310, true, true,  false);
            AddPermission(con, "Applicant.Delete",  "حذف متقاضی",           "متقاضیان", 320, true, false, false);

            AddPermission(con, "CaseRelation.Edit",   "ثبت رابطه پرونده",   "پرونده", 330, true, true,  false);
            AddPermission(con, "CaseRelation.Delete", "حذف رابطه پرونده",   "پرونده", 340, true, false, false);

            AddPermission(con, "Case.Print",        "چاپ پرونده",           "پرونده", 350, true, true,  true);

            AddPermission(con, "Archive.Restore",         "بازیابی از بایگانی", "بایگانی", 360, true, false, false);
            AddPermission(con, "Archive.PermanentDelete", "حذف دائمی از بایگانی", "بایگانی", 370, false, false, false);

            AddPermission(con, "Report.Run",    "اجرای گزارش‌ساز پویا", "گزارش", 380, true, true, true);
            AddPermission(con, "Report.Export", "خروجی گزارش‌ساز پویا", "گزارش", 390, true, true, true);

            AddPermission(con, "Barcode.Print", "چاپ بارکد", "بارکد", 400, true, true, true);

            AddPermission(con, "GuardianCard.Print",           "چاپ کارت شناسایی",   "کارت شناسایی", 410, true, true, true);
            AddPermission(con, "GuardianCard.ManageTemplates", "مدیریت قالب کارت",   "کارت شناسایی", 420, true, false, false);

            // ─── Phase 2 — کنترلِ دقیق‌ترِ عملیاتِ قالبِ کارت. مقادیرِ پیش‌فرض
            // عمداً هم‌سطحِ GuardianCard.ManageTemplates بالا هستند (فقط
            // Admin) — این پنج کلید فقط برایِ کنترلِ ریزتر در کنارِ دروازهٔ
            // کلیِ فرم اضافه شده‌اند، نه جایگزینِ آن؛ خودِ ManageTemplates
            // دست‌نخورده می‌ماند.
            AddPermission(con, "GuardianCard.Template.View",     "مشاهدهٔ قالب‌های کارت", "کارت شناسایی", 421, true, true,  true);
            AddPermission(con, "GuardianCard.Template.Create",   "ایجاد قالب کارت",       "کارت شناسایی", 422, true, false, false);
            AddPermission(con, "GuardianCard.Template.Edit",     "ویرایش قالب کارت",      "کارت شناسایی", 423, true, false, false);
            AddPermission(con, "GuardianCard.Template.Delete",   "حذف قالب کارت",         "کارت شناسایی", 424, true, false, false);
            AddPermission(con, "GuardianCard.Template.Activate", "فعال/غیرفعال‌سازی قالب کارت", "کارت شناسایی", 425, true, false, false);

            // توجه: «AssistanceReceipt.Print» عمداً در Sprint 0A افزوده نشده.
            // چاپ رسید مساعدت از FrmFinance.cs فراخوانی می‌شود و طبق محدودیت
            // صریح این اسپرینت («Do not touch Finance») دست‌نخورده باقی مانده؛
            // نامزد Sprint 0B/بعدی است (نگاه کنید گزارش تکمیل Sprint 0A).

            // ─── نسخهٔ ۱٫۰ / فاز ۳ (موج ۴) — مالی و حسابداری. مقادیر پیش‌فرض
            // دقیقاً همان دسترسی فعلیِ بر پایهٔ SecurityContext را بازتولید
            // می‌کنند تا هیچ کاربری روز اول چیزی کم یا زیاد نشود.
            AddPermission(con, "Finance.Edit",    "ثبت/ویرایش کمک مالی",     "مالی", 440, true, true,  false);

            AddPermission(con, "Accounting.Edit",        "ثبت/ویرایش سند حسابداری", "حسابداری", 450, true, true,  false);
            AddPermission(con, "Accounting.Reverse",     "ابطال سند حسابداری",      "حسابداری", 460, true, false, false);
            AddPermission(con, "Accounting.ClosePeriod", "بستن دوره مالی",          "حسابداری", 470, true, false, false);
            AddPermission(con, "Accounting.Repair",      "اصلاح داده‌های تاریخی حسابداری", "حسابداری", 480, false, false, false);
            AddPermission(con, "Accounting.Backup",      "پشتیبان‌گیری/بازیابی حسابداری",  "حسابداری", 490, false, false, false);

            // ─── نسخهٔ ۱٫۰ / فاز ۴ (موج ۵) — کاربران/تنظیمات/پشتیبان‌گیری/
            // همگام‌سازی/مراکز/ماژول‌ها. «User.Manage» و «Module.Manage» از
            // قبل (پایهٔ اولیهٔ PermissionService) تعریف شده بودند ولی به هیچ
            // مسیری وصل نبودند — همین‌جا فقط سیمِ‌کشی می‌شوند، کلید تازه‌ای
            // لازم نیست. مراکز/پشتیبان‌گیری عمداً فقط SuperAdmin می‌مانند
            // (دقیقاً همان رفتار فعلی؛ گسترش به Admin یک تصمیمِ محصولیِ جداست).
            AddPermission(con, "Center.Manage",  "مدیریت مراکز",                 "سیستم", 500, false, false, false);
            AddPermission(con, "Backup.Create",  "پشتیبان‌گیری کامل نرم‌افزار",   "سیستم", 510, false, false, false);
            AddPermission(con, "Backup.Restore", "بازیابی کامل نرم‌افزار",       "سیستم", 520, false, false, false);
            AddPermission(con, "Sync.Execute",   "اجرای همگام‌سازی",             "همگام‌سازی", 530, true, true, false);

            // دستیار هوشمند — فاز ۱. طبق AI_ASSISTANT_PHASE1_FIXES.md §5:
            // جست‌وجو برای همه (مثل Case.View)؛ ایجاد یادآوری فقط برای کسانی که
            // خودشان می‌توانند یادآوری بسازند (Operator به بالا).
            AddPermission(con, "AI.Search",           "جستجوی هوشمند (زبان طبیعی)",   "دستیار هوشمند", 610, true, true, true);
            AddPermission(con, "AI.Reminders.Create", "ایجاد یادآوری با دستور طبیعی", "دستیار هوشمند", 611, true, true, false);
        }

        private static void AddPermission(SQLiteConnection con, string key, string name,
                                          string category, int sortOrder,
                                          bool admin, bool operatorRole, bool viewer)
        {
            Exec(con, @"
INSERT OR IGNORE INTO EntPermission (PermKey, Name, Category, SortOrder)
VALUES (@Key, @Name, @Cat, @Sort);",
                "@Key", key, "@Name", name, "@Cat", category, "@Sort", sortOrder);

            // مدیر کل همیشه همه‌چیز را دارد (در کد هم تضمین شده تا هرگز قفل نشود).
            GrantRole(con, "SuperAdmin", key, true);
            GrantRole(con, "Admin",      key, admin);
            GrantRole(con, "Operator",   key, operatorRole);
            GrantRole(con, "Viewer",     key, viewer);
        }

        // INSERT OR IGNORE یعنی اگر مدیر سیستم قبلاً این مقدار را تغییر داده،
        // به‌روزرسانی بعدی نرم‌افزار تنظیم او را بازنویسی نمی‌کند.
        private static void GrantRole(SQLiteConnection con, string role, string key, bool granted)
        {
            Exec(con, @"
INSERT OR IGNORE INTO EntRolePermission (RoleName, PermKey, IsGranted)
VALUES (@Role, @Key, @Granted);",
                "@Role", role, "@Key", key, "@Granted", granted ? 1 : 0);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ویژگی ۱۰ — مدیریت ماژول‌ها (فعال/غیرفعال به تفکیک نقش و کاربر)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureModuleObjects(SQLiteConnection con)
        {
            // IsCore یعنی «ماژول پایه»: هرگز نمی‌توان آن را خاموش کرد، وگرنه
            // ممکن است راه بازگشت به تنظیمات بسته شود و برنامه عملاً قفل گردد.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntModule (
    ModuleID    INTEGER PRIMARY KEY AUTOINCREMENT,
    ModuleKey   TEXT    NOT NULL UNIQUE,
    Name        TEXT    NOT NULL,
    Description TEXT    NULL,
    IsCore      INTEGER NOT NULL DEFAULT 0,
    IsEnabled   INTEGER NOT NULL DEFAULT 1,
    SortOrder   INTEGER NOT NULL DEFAULT 0
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntRoleModule (
    RoleName  TEXT    NOT NULL,
    ModuleKey TEXT    NOT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (RoleName, ModuleKey)
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS EntUserModule (
    UserID    INTEGER NOT NULL,
    ModuleKey TEXT    NOT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (UserID, ModuleKey)
);");

            EnsureDefaultModules(con);
        }

        // ماژول‌های شناخته‌شده — دقیقاً همان بخش‌هایی که در منوی کناری هستند.
        //
        // آموزش — سازگاری عقب‌رو: همه ماژول‌ها به‌صورت پیش‌فرض «فعال» ثبت
        // می‌شوند و هیچ سطری در EntRoleModule/EntUserModule ساخته نمی‌شود؛
        // یعنی روز اول، منوی همه کاربران دقیقاً مثل قبل است.
        private static void EnsureDefaultModules(SQLiteConnection con)
        {
            AddModule(con, ModuleService.ModuleCases,      "پرونده‌ها",         0,  10);
            AddModule(con, ModuleService.ModuleApplicants, "متقاضیان",          0,  20);
            AddModule(con, ModuleService.ModuleSearch,     "جستجوی پیشرفته",    0,  30);
            AddModule(con, ModuleService.ModuleFinance,    "مالی",              0,  40);
            AddModule(con, ModuleService.ModuleAccounting, "حسابداری ایتام",    0,  50);
            AddModule(con, ModuleService.ModuleSync,       "همگام‌سازی",        0,  60);
            AddModule(con, ModuleService.ModuleDuplicates, "پرونده‌های تکراری", 0,  70);
            AddModule(con, ModuleService.ModuleDataQuality,"کیفیت داده",        0,  75);
            AddModule(con, ModuleService.ModuleBarcode,    "بارکد و جستجو",     0,  77);
            AddModule(con, ModuleService.ModuleArchive,    "بایگانی",           0,  78);
            AddModule(con, ModuleService.ModuleAuditReport,"گزارش رویدادها",    0,  80);
            AddModule(con, ModuleService.ModuleReportBuilder, "گزارش‌ساز پویا", 0,  85);

            AddModule(con, ModuleService.ModuleWorkflow,   "گردش‌کار",          0,  90);
            AddModule(con, ModuleService.ModuleApprovals,  "تأییدها",           0, 100);
            AddModule(con, ModuleService.ModuleTasks,      "وظایف",             0, 110);
            AddModule(con, ModuleService.ModuleRules,      "قواعد سازمانی",     0, 120);
            AddModule(con, ModuleService.ModuleLocks,      "قفل رکوردها",       0, 130);
            AddModule(con, ModuleService.ModuleVersions,   "تاریخچه نسخه‌ها",   0, 140);
            AddModule(con, ModuleService.ModuleSecurity,   "ممیزی امنیتی",      0, 150);
            AddModule(con, ModuleService.ModuleErrors,     "گزارش خطاها",       0, 160);

            // ماژول‌های پایه — غیرقابل خاموش‌کردن.
            AddModule(con, ModuleService.ModuleUsers,       "کاربران و دسترسی", 1, 170);
            AddModule(con, ModuleService.ModulePermissions, "ماتریس مجوزها",    1, 180);
            AddModule(con, ModuleService.ModuleModules,     "مدیریت ماژول‌ها",  1, 190);
            AddModule(con, ModuleService.ModuleSettings,    "تنظیمات",          1, 200);
        }

        private static void AddModule(SQLiteConnection con, string key, string name,
                                      int isCore, int sortOrder)
        {
            Exec(con, @"
INSERT OR IGNORE INTO EntModule (ModuleKey, Name, IsCore, IsEnabled, SortOrder)
VALUES (@Key, @Name, @Core, 1, @Sort);",
                "@Key", key, "@Name", name, "@Core", isCore, "@Sort", sortOrder);
        }

        // ─── کمکی‌های داخلی ─────────────────────────────────────────────────
        // آموزش: همان الگوی Exec در AccountingInitializer، فقط با پشتیبانی از
        // پارامتر تا هیچ مقداری به‌صورت رشته‌ای داخل SQL چسبانده نشود.
        internal static void Exec(SQLiteConnection con, string sql, params object[] nameValuePairs)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                AddParams(cmd, nameValuePairs);
                cmd.ExecuteNonQuery();
            }
        }

        internal static object Scalar(SQLiteConnection con, string sql, params object[] nameValuePairs)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                AddParams(cmd, nameValuePairs);
                return cmd.ExecuteScalar();
            }
        }

        private static void AddParams(SQLiteCommand cmd, object[] nameValuePairs)
        {
            if (nameValuePairs == null) return;

            for (int i = 0; i + 1 < nameValuePairs.Length; i += 2)
                cmd.Parameters.AddWithValue(
                    Convert.ToString(nameValuePairs[i]),
                    nameValuePairs[i + 1] ?? DBNull.Value);
        }

        internal static long ToInt64(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            try { return Convert.ToInt64(value); }
            catch { return 0; }
        }
    }
}

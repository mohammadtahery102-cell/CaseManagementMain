using System;
using System.Data;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ─────────────────────────────────────────────────────────────────────────
    // ویژگی ۳ — مدیریت وظایف.
    //
    // وظیفه یا دستی ساخته می‌شود یا خودکار (مثلاً وقتی درخواست تأیید به سطح
    // شما می‌رسد). تخصیص می‌تواند به یک کاربر مشخص یا به یک نقش باشد؛ در حالت
    // نقش، وظیفه در فهرست همه کاربرانِ آن نقش دیده می‌شود.
    //
    // نام کلاس عمداً TaskService است و نه Task، تا با System.Threading.Tasks.Task
    // اشتباه/تداخل نکند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class TaskService
    {
        public const string StatusOpen       = "باز";
        public const string StatusInProgress = "در حال انجام";
        public const string StatusDone       = "انجام شده";
        public const string StatusCanceled   = "لغو شده";

        public const string SourceManual   = "دستی";
        public const string SourceApproval = "تأیید";
        public const string SourceWorkflow = "گردش‌کار";
        public const string SourceRule     = "قاعده";

        // ─── ساخت ─────────────────────────────────────────────────────────
        public static long Create(
            string title, string description,
            string entityName, int entityId,
            int assignedToUserId, string assignedToRole,
            string priority, string dueDate,
            string sourceType, int sourceId)
        {
            if (string.IsNullOrWhiteSpace(title)) return 0;

            return EntDb.Insert(@"
INSERT INTO EntTask
    (Title, Description, EntityName, EntityID, AssignedToUserID, AssignedToRole,
     Priority, Status, DueDate, SourceType, SourceID, CenterID, CreatedBy, CreatedByUserID)
VALUES
    (@Title, @Desc, @Entity, @EntityId, @UserId, @Role,
     @Priority, @Status, @Due, @SourceType, @SourceId, @Center, @By, @ByID);",
                "@Title",      title,
                "@Desc",       description,
                "@Entity",     entityName,
                "@EntityId",   entityId > 0 ? (object)entityId : null,
                "@UserId",     assignedToUserId > 0 ? (object)assignedToUserId : null,
                "@Role",       assignedToRole,
                "@Priority",   string.IsNullOrWhiteSpace(priority) ? "متوسط" : priority,
                "@Status",     StatusOpen,
                "@Due",        dueDate,
                "@SourceType", string.IsNullOrWhiteSpace(sourceType) ? SourceManual : sourceType,
                "@SourceId",   sourceId > 0 ? (object)sourceId : null,
                "@Center",     SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : null,
                "@By",         SecurityContext.Username,
                "@ByID",       SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : null);
        }

        // ساخت وظیفه خودکار — اگر وظیفه بازی با همان منبع و همان رکورد از قبل
        // وجود داشته باشد چیزی ساخته نمی‌شود (جلوگیری از انباشت وظایف تکراری).
        //
        // آموزش — چرا رکورد هم بخشی از کلید تکراری است: برای وظایف ساخته‌شده
        // توسط موتور قواعد، SourceID شناسه خودِ قاعده است نه شناسه رکورد. اگر
        // فقط (منبع، شناسه منبع) مقایسه می‌شد، یک قاعده پس از اولین اجرا برای
        // هیچ پرونده دیگری وظیفه نمی‌ساخت.
        public static long CreateAuto(
            string title, string description,
            string entityName, int entityId,
            int assignedToUserId, string assignedToRole,
            string sourceType, int sourceId)
        {
            long existing = EntDb.ToInt64(EntDb.Scalar(@"
SELECT COUNT(*) FROM EntTask
WHERE  SourceType = @Type AND SourceID = @Id
  AND  IFNULL(EntityName, '') = IFNULL(@Entity, '')
  AND  IFNULL(EntityID, 0)    = @EntityId
  AND  Status IN (@Open, @Progress)
  AND  IFNULL(AssignedToUserID, 0) = @UserId
  AND  IFNULL(AssignedToRole, '')  = IFNULL(@Role, '');",
                "@Type",     sourceType,
                "@Id",       sourceId,
                "@Entity",   entityName ?? "",
                "@EntityId", entityId,
                "@Open",     StatusOpen,
                "@Progress", StatusInProgress,
                "@UserId",   assignedToUserId,
                "@Role",     assignedToRole ?? ""));

            if (existing > 0) return 0;

            return Create(title, description, entityName, entityId,
                          assignedToUserId, assignedToRole,
                          "متوسط", null, sourceType, sourceId);
        }

        // بستن خودکار وظایف مربوط به یک منبع (مثلاً وقتی درخواست تأیید بسته شد).
        public static void CloseBySource(string sourceType, int sourceId, string note)
        {
            EntDb.Exec(@"
UPDATE EntTask
SET    Status      = @Done,
       CompletedAt = datetime('now'),
       CompletedBy = @By,
       Description = CASE
                       WHEN @Note IS NULL THEN Description
                       ELSE IFNULL(Description || ' | ', '') || @Note
                     END
WHERE  SourceType = @Type AND SourceID = @Id
  AND  Status IN (@Open, @Progress);",
                "@Done",     StatusDone,
                "@By",       SecurityContext.Username,
                "@Note",     note,
                "@Type",     sourceType,
                "@Id",       sourceId,
                "@Open",     StatusOpen,
                "@Progress", StatusInProgress);
        }

        // ─── تغییر وضعیت / تخصیص ──────────────────────────────────────────
        public static WorkflowActionResult ChangeStatus(int taskId, string newStatus)
        {
            DataTable table = EntDb.Query(
                "SELECT TaskID, Status, CenterID FROM EntTask WHERE TaskID = @Id;", "@Id", taskId);

            if (table.Rows.Count == 0)
                return WorkflowActionResult.Fail("وظیفه پیدا نشد.");

            if (!SecurityContext.CanAccessCenter(EntDb.ToInt(table.Rows[0]["CenterID"])))
                return WorkflowActionResult.Fail("این وظیفه متعلق به مرکز دیگری است.");

            bool closing = newStatus == StatusDone || newStatus == StatusCanceled;

            EntDb.Exec(@"
UPDATE EntTask
SET    Status      = @Status,
       CompletedAt = CASE WHEN @Closing = 1 THEN datetime('now') ELSE NULL END,
       CompletedBy = CASE WHEN @Closing = 1 THEN @By ELSE NULL END
WHERE  TaskID = @Id;",
                "@Status",  newStatus,
                "@Closing", closing ? 1 : 0,
                "@By",      SecurityContext.Username,
                "@Id",      taskId);

            AuditLogger.Log("TaskStatus", "EntTask", taskId,
                            EntDb.ToText(table.Rows[0]["Status"]), newStatus);

            return new WorkflowActionResult { Applied = true, Message = "وضعیت وظیفه تغییر کرد." };
        }

        public static WorkflowActionResult Assign(int taskId, int userId, string role)
        {
            if (!SecurityContext.IsAdmin())
                return WorkflowActionResult.Fail("تخصیص وظیفه فقط برای مدیر سیستم مجاز است.");

            EntDb.Exec(@"
UPDATE EntTask SET AssignedToUserID = @UserId, AssignedToRole = @Role WHERE TaskID = @Id;",
                "@UserId", userId > 0 ? (object)userId : null,
                "@Role",   role,
                "@Id",     taskId);

            return new WorkflowActionResult { Applied = true, Message = "وظیفه تخصیص داده شد." };
        }

        public static WorkflowActionResult Delete(int taskId)
        {
            if (!SecurityContext.IsAdmin())
                return WorkflowActionResult.Fail("حذف وظیفه فقط برای مدیر سیستم مجاز است.");

            EntDb.Exec("DELETE FROM EntTask WHERE TaskID = @Id;", "@Id", taskId);
            return new WorkflowActionResult { Applied = true, Message = "وظیفه حذف شد." };
        }

        // ─── خواندن ───────────────────────────────────────────────────────
        // وظایف کاربر جاری: آن‌هایی که مستقیم به او تخصیص یافته یا به نقش او.
        public static DataTable GetMyTasks(bool openOnly)
        {
            return EntDb.Query(@"
SELECT t.TaskID      AS 'شناسه',
       t.Title       AS 'عنوان',
       t.Priority    AS 'اولویت',
       t.Status      AS 'وضعیت',
       t.DueDate     AS 'مهلت',
       t.EntityName  AS 'موجودیت',
       t.EntityID    AS 'شناسه رکورد',
       t.SourceType  AS 'منبع',
       t.CreatedBy   AS 'ایجادکننده',
       t.CreatedAt   AS 'تاریخ ایجاد',
       t.Description AS 'توضیح'
FROM   EntTask t
WHERE  (IFNULL(t.AssignedToUserID, 0) = @UserId
        OR (IFNULL(t.AssignedToRole, '') <> '' AND t.AssignedToRole = @Role))
  AND  (@OpenOnly = 0 OR t.Status IN (@Open, @Progress))
  AND  (@Center = 0 OR IFNULL(t.CenterID, 0) = @Center)
ORDER  BY t.TaskID DESC;",
                "@UserId",   SecurityContext.UserId,
                "@Role",     SecurityContext.Role ?? "",
                "@OpenOnly", openOnly ? 1 : 0,
                "@Open",     StatusOpen,
                "@Progress", StatusInProgress,
                "@Center",   SecurityContext.CenterFilterId);
        }

        public static DataTable GetAll(string status)
        {
            return EntDb.Query(@"
SELECT t.TaskID      AS 'شناسه',
       t.Title       AS 'عنوان',
       t.Priority    AS 'اولویت',
       t.Status      AS 'وضعیت',
       t.DueDate     AS 'مهلت',
       COALESCE(u.Username, t.AssignedToRole, '—') AS 'مسئول',
       t.EntityName  AS 'موجودیت',
       t.EntityID    AS 'شناسه رکورد',
       t.SourceType  AS 'منبع',
       t.CreatedBy   AS 'ایجادکننده',
       t.CreatedAt   AS 'تاریخ ایجاد',
       t.Description AS 'توضیح'
FROM   EntTask t
LEFT   JOIN TblUsers u ON u.UserID = t.AssignedToUserID
WHERE  (IFNULL(@Status, '') = '' OR t.Status = @Status)
  AND  (@Center = 0 OR IFNULL(t.CenterID, 0) = @Center)
ORDER  BY t.TaskID DESC;",
                "@Status", status ?? "",
                "@Center", SecurityContext.CenterFilterId);
        }

        // وظایف مربوط به یک رکورد مشخص (برای نمایش در فرم آن رکورد).
        public static DataTable GetForEntity(string entityName, int entityId)
        {
            return EntDb.Query(@"
SELECT TaskID AS 'شناسه', Title AS 'عنوان', Status AS 'وضعیت',
       Priority AS 'اولویت', DueDate AS 'مهلت'
FROM   EntTask
WHERE  EntityName = @Entity AND EntityID = @Id
ORDER  BY TaskID DESC;", "@Entity", entityName, "@Id", entityId);
        }

        // تعداد وظایف باز کاربر جاری — برای نشان دادن روی داشبورد.
        public static int MyOpenCount()
        {
            return EntDb.ToInt(EntDb.Scalar(@"
SELECT COUNT(*) FROM EntTask
WHERE  (IFNULL(AssignedToUserID, 0) = @UserId
        OR (IFNULL(AssignedToRole, '') <> '' AND AssignedToRole = @Role))
  AND  Status IN (@Open, @Progress)
  AND  (@Center = 0 OR IFNULL(CenterID, 0) = @Center);",
                "@UserId",   SecurityContext.UserId,
                "@Role",     SecurityContext.Role ?? "",
                "@Open",     StatusOpen,
                "@Progress", StatusInProgress,
                "@Center",   SecurityContext.CenterFilterId));
        }
    }
}

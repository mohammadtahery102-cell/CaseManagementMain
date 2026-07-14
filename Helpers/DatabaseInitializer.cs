using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    public static class DatabaseInitializer
    {
        public static void EnsureDatabaseObjects()
        {
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                // فعال‌سازی Foreign Key در SQLite (پیش‌فرض خاموش است)
                ExecuteNonQuery(con, "PRAGMA foreign_keys = ON;");

                // ─── TblCase (مرجع: SQL Server Schema) ───────────────────────
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblCase (
    CasID                 INTEGER PRIMARY KEY AUTOINCREMENT,
    FormNo                INTEGER NULL,
    Code                  TEXT NULL,
    Phone                 TEXT NULL,
    PhotoPath             TEXT NULL,
    FamilyPhotoPath       TEXT NULL,
    CaseNo                TEXT NULL,
    CaseDate              TEXT NULL,
    District              TEXT NULL,
    RequestType           TEXT NULL,
    PriorityLevel         TEXT NULL,
    HeadFullName          TEXT NULL,
    HeadFatherName        TEXT NULL,
    HeadSadat             TEXT NULL,
    Religion              TEXT NULL,
    HeadTazkiraNo         TEXT NULL,
    HeadOriginalResidence TEXT NULL,
    HeadCurrentResidence  TEXT NULL,
    RelationshipToFamily  TEXT NULL,
    RelativePhone         TEXT NULL,
    CoveredByOrg          TEXT NULL,
    Job                   TEXT NULL,
    Skill                 TEXT NULL,
    DisabilityDegree      TEXT NULL,
    DisabilityType        TEXT NULL,
    MigrationCardType     TEXT NULL,
    MaritalStatus         TEXT NULL,
    Surveyors             TEXT NULL,
    SurveyDate            TEXT NULL,
    LocationAddress       TEXT NULL,
    EducationLevel        TEXT NULL,
    ServiceStatus         TEXT NOT NULL DEFAULT 'فعال',
    StopReason            TEXT NULL,
    UrgentSituation       TEXT NULL,
    Zone                  TEXT NULL,
    Province              TEXT NULL,
    CreatedAt             TEXT NOT NULL DEFAULT (datetime('now')),
    UpdatedAt             TEXT NULL,
    CONSTRAINT UQ_TblCase_Code   UNIQUE (Code),
    CONSTRAINT UQ_TblCase_FormNo UNIQUE (FormNo),
    CONSTRAINT CK_TblCase_ServiceStatus
        CHECK (ServiceStatus IN ('قطع', 'قطع موقت', 'در انتظار تأیید', 'فعال'))
);");

                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_CaseDate ON TblCase(CaseDate);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_Code ON TblCase(Code);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_FormNo ON TblCase(FormNo);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_Province ON TblCase(Province);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_ProvinceDistrict ON TblCase(Province, District);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_ServiceStatus ON TblCase(ServiceStatus);");

                // ─── TblFamily (مرجع: SQL Server Schema) ─────────────────────
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblFamily (
    FamID                   INTEGER PRIMARY KEY AUTOINCREMENT,
    CasID                   INTEGER NOT NULL,
    MemberName              TEXT NULL,
    MemberFatherName        TEXT NULL,
    MemberTazkiraNo         TEXT NULL,
    BirthDate               TEXT NULL,
    MemberSadat             TEXT NULL,
    Gender                  TEXT NULL,
    PhysicalStatus          TEXT NULL,
    HasDisability           TEXT NULL,
    MemberDisabilityDegree  TEXT NULL,
    MemberEducation         TEXT NULL,
    SchoolName              TEXT NULL,
    GradeLevel              TEXT NULL,
    UniversityName          TEXT NULL,
    StudyYear               TEXT NULL,
    Major                   TEXT NULL,
    StudyField              TEXT NULL,
    OfficialStatus          TEXT NULL,
    Details                 TEXT NULL,
    MemberPhotoPath         TEXT NULL,
    Skill                   TEXT NULL,
    LeaveReason             TEXT NULL,
    CONSTRAINT FK_Family_Case FOREIGN KEY (CasID)
        REFERENCES TblCase (CasID) ON DELETE CASCADE
);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblFamily_CasID ON TblFamily(CasID);");

                // ─── TblDocs (مرجع: SQL Server Schema) ───────────────────────
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblDocs (
    DocID            INTEGER PRIMARY KEY AUTOINCREMENT,
    CasID            INTEGER NOT NULL,
    DocType          TEXT NULL,
    OriginalFileName TEXT NULL,
    DocFilePath      TEXT NULL,
    RelatedCaseRef   TEXT NULL,
    DocDescription   TEXT NULL,
    CONSTRAINT FK_Docs_Case FOREIGN KEY (CasID)
        REFERENCES TblCase (CasID) ON DELETE CASCADE
);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblDocs_CasID ON TblDocs(CasID);");

                // ─── TblUsers (مرجع: SQL Server + MustChangePassword فاز صفر) ─
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblUsers (
    UserID             INTEGER PRIMARY KEY AUTOINCREMENT,
    Username           TEXT NOT NULL UNIQUE,
    PasswordHash       BLOB NOT NULL,
    PasswordSalt       BLOB NOT NULL,
    Role               TEXT NOT NULL,
    IsActive           INTEGER NOT NULL DEFAULT 1,
    MustChangePassword INTEGER NOT NULL DEFAULT 1,
    CreatedAt          TEXT NOT NULL DEFAULT (datetime('now'))
);");

                // ─── TblAssistance (مرجع: SQL Server Schema) ─────────────────
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblAssistance (
    AssistanceID   INTEGER PRIMARY KEY AUTOINCREMENT,
    CasID          INTEGER NOT NULL,
    AssistanceDate TEXT NOT NULL,
    Amount         REAL NOT NULL,
    AssistanceType TEXT NOT NULL,
    Description    TEXT NULL,
    CreatedAt      TEXT NOT NULL DEFAULT (datetime('now')),
    CreatedBy      TEXT NULL,
    CONSTRAINT FK_Assistance_Case FOREIGN KEY (CasID)
        REFERENCES TblCase (CasID) ON DELETE CASCADE
);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblAssistance_CasID ON TblAssistance(CasID);");
                EnsureAssistanceForeignKey(con);

                // ─── TblAuditLog (مرجع: SQL Server Schema) ───────────────────
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblAuditLog (
    LogID      INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID     INTEGER NULL,
    Username   TEXT NULL,
    Operation  TEXT NOT NULL,
    EntityName TEXT NOT NULL,
    EntityID   INTEGER NULL,
    OldValue   TEXT NULL,
    NewValue   TEXT NULL,
    CreatedAt  TEXT NOT NULL DEFAULT (datetime('now'))
);");
                // آموزش — رفع باگ چندمرکزی: بدون این ستون، رویدادهای همه
                // مراکز در گزارش رویدادها به همه کاربران نشان داده می‌شد.
                EnsureColumn(con, "TblAuditLog", "CenterID", "INTEGER NULL");

                // ─── TblAuditLogs (جدول دوم لاگ — مرجع: SQL Server Schema) ───
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblAuditLogs (
    LogID      INTEGER PRIMARY KEY AUTOINCREMENT,
    TableName  TEXT NULL,
    ActionType TEXT NULL,
    RecordID   INTEGER NULL,
    OldData    TEXT NULL,
    NewData    TEXT NULL,
    UserID     INTEGER NULL,
    ActionDate TEXT NULL DEFAULT (datetime('now'))
);");

                // ─── TblCaseStatusHistory (مرجع: SQL Server Schema) ──────────
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblCaseStatusHistory (
    StatusID  INTEGER PRIMARY KEY AUTOINCREMENT,
    CasID     INTEGER NOT NULL,
    OldStatus TEXT NULL,
    NewStatus TEXT NOT NULL,
    ChangedAt TEXT NOT NULL DEFAULT (datetime('now')),
    ChangedBy TEXT NULL
);");

                // ─── TblAppSettings (مرجع: SQL Server Schema) ────────────────
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblAppSettings (
    SettingKey   TEXT PRIMARY KEY,
    SettingValue TEXT NULL,
    UpdatedAt    TEXT NOT NULL DEFAULT (datetime('now'))
);");

                // ─── ایندکس‌ها (P4): سرعت کوئری‌های مالی/تاریخچه/داشبورد ─────
                // این جداول قبلاً ایندکس نداشتند و کوئری‌های روند زمانی و مالی
                // مجبور به اسکن کامل جدول بودند.
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCaseStatusHistory_CasID ON TblCaseStatusHistory(CasID);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCaseStatusHistory_Changed ON TblCaseStatusHistory(NewStatus, ChangedAt);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblAssistance_Date ON TblAssistance(AssistanceDate);");

                // ─── ایندکس‌های داشبورد «اعضای خانواده» (بخش عملکرد) ─────────
                // این کوئری هر بار MemberEducation/Gender را روی کل TblFamily
                // فیلتر و گروه‌بندی می‌کند؛ بدون ایندکس با رشد داده کند می‌شود.
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblFamily_MemberEducation ON TblFamily(MemberEducation);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblFamily_Gender ON TblFamily(Gender);");

                // ─── TblLookup (مقادیر پایه) ─────────────────────────────────────
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblLookup (
    LookupID  INTEGER PRIMARY KEY AUTOINCREMENT,
    Category  TEXT NOT NULL,
    Value     TEXT NOT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    IsActive  INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT UQ_Lookup UNIQUE (Category, Value)
);");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblLookup_Category ON TblLookup(Category);");
                EnsureDefaultLookups(con);

                // ─── TblCenter (مدیریت مراکز) ────────────────────────────────────
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblCenter (
    CenterID   INTEGER PRIMARY KEY AUTOINCREMENT,
    CenterCode TEXT NOT NULL UNIQUE,
    CenterName TEXT NOT NULL,
    IsActive   INTEGER NOT NULL DEFAULT 1,
    CreatedAt  TEXT NOT NULL DEFAULT (datetime('now'))
);");
                EnsureDefaultCenters(con);

                // ─── TblReminder (یادآوری با زمان الارم — به درخواست کاربر) ───
                // RemindAt به‌صورت میلادی ISO "yyyy-MM-dd HH:mm" ذخیره می‌شود تا
                // مقایسه با datetime('now') درست کار کند؛ IsDone/IsNotified برای
                // جلوگیری از نمایش تکراری زنگ.
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblReminder (
    ReminderID INTEGER PRIMARY KEY AUTOINCREMENT,
    Title      TEXT NOT NULL,
    Note       TEXT NULL,
    RemindAt   TEXT NOT NULL,
    IsDone     INTEGER NOT NULL DEFAULT 0,
    IsNotified INTEGER NOT NULL DEFAULT 0,
    CenterID   INTEGER NULL,
    CreatedBy  TEXT NULL,
    CreatedAt  TEXT NOT NULL DEFAULT (datetime('now'))
);");

                // ─── TblApplicant (فرم متقاضی/درخواستی — به درخواست کاربر) ────
                // یادداشت اولیه متقاضی که بعداً می‌تواند به پرونده کامل تبدیل شود.
                ExecuteNonQuery(con, @"
CREATE TABLE IF NOT EXISTS TblApplicant (
    ApplicantID  INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName     TEXT NOT NULL,
    FatherName   TEXT NULL,
    Phone        TEXT NULL,
    Province     TEXT NULL,
    District     TEXT NULL,
    RequestType  TEXT NULL,
    Note         TEXT NULL,
    Status       TEXT NOT NULL DEFAULT 'در انتظار',
    ConvertedCasID INTEGER NULL,
    CenterID     INTEGER NULL,
    CreatedBy    TEXT NULL,
    CreatedAt    TEXT NOT NULL DEFAULT (datetime('now'))
);");

                // ─── مهاجرت افزایشی فاز صفر: ستون MustChangePassword ─────────
                EnsureColumn(con, "TblUsers", "MustChangePassword", "INTEGER NOT NULL DEFAULT 1");
                EnsureColumn(con, "TblUsers", "LastCenterID",       "INTEGER NULL");
                EnsureColumn(con, "TblUsers", "Phone",              "TEXT NULL");
                EnsureColumn(con, "TblUsers", "WhatsApp",           "TEXT NULL");

                // ─── مرکز اختصاصی کاربر (رفع باگ امنیتی): هر کاربر غیر از
                // SuperAdmin باید به یک مرکز مشخص محدود باشد تا نتواند در
                // صفحه ورود هر مرکزی را انتخاب کند. رکوردهای قدیمی از
                // LastCenterID مقداردهی می‌شوند؛ در نبود آن، مرکز ۱ (مرکز
                // اصلی) پیش‌فرض است.
                EnsureColumn(con, "TblUsers", "CenterID", "INTEGER NULL");
                ExecuteNonQuery(con, @"
UPDATE TblUsers
SET CenterID = COALESCE(LastCenterID, 1)
WHERE CenterID IS NULL AND Role <> 'SuperAdmin';");

                // ─── ارتقای امنیتی سازگار با نسخه قبل (بخش امنیت) ────────────
                // PasswordIterations: تعداد Iteration هش هر کاربر (رمزهای قدیمی
                // با مقدار قدیمی‌شان قابل تأیید می‌مانند؛ رمزهای جدید امن‌تر می‌شوند).
                EnsureColumn(con, "TblUsers", "PasswordIterations", "INTEGER NULL");
                ExecuteNonQuery(con, string.Format(
                    "UPDATE TblUsers SET PasswordIterations = {0} WHERE PasswordIterations IS NULL;",
                    PasswordHelper.LegacyIterations));

                // FailedLoginCount/LockoutUntil: محدودیت تلاش ورود ناموفق (Lockout)
                EnsureColumn(con, "TblUsers", "FailedLoginCount", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(con, "TblUsers", "LockoutUntil",     "TEXT NULL");

                // ─── ستون‌های جدید TblFamily (بخش ۶): مذهب، وضعیت تأهل، وضعیت خدمات ──
                EnsureColumn(con, "TblFamily", "Religion",      "TEXT NULL");
                EnsureColumn(con, "TblFamily", "MaritalStatus", "TEXT NULL");
                EnsureColumn(con, "TblFamily", "ServiceStatus", "TEXT NOT NULL DEFAULT 'فعال'");
                EnsureColumn(con, "TblFamily", "StopReason",    "TEXT NULL");

                // ─── شرح تفصیلی معلولیت (فیلد «توضیحات بیشتر» در تب مشخصات جسمی) ──
                EnsureColumn(con, "TblFamily", "DisabilityDetails", "TEXT NULL");

                // ─── فیلدهای تحصیلی جدید (مطابق قالب Word اعضای خانواده) ─────────
                EnsureColumn(con, "TblFamily", "SchoolType",          "TEXT NULL"); // خصوصی/دولتی (مکتب)
                EnsureColumn(con, "TblFamily", "SchoolPrevGrade",     "TEXT NULL"); // معدل سال قبل (مکتب)
                EnsureColumn(con, "TblFamily", "UniversityType",      "TEXT NULL"); // خصوصی/دولتی (دانشگاه)
                EnsureColumn(con, "TblFamily", "UniversityPrevGrade", "TEXT NULL"); // معدل سال قبل (دانشگاه)
                EnsureColumn(con, "TblFamily", "SeminaryLevel",       "TEXT NULL"); // دروس حوزوی: سطح ۱/۲/۳
                EnsureColumn(con, "TblFamily", "EducationCoverage",   "TEXT NULL"); // تحت پوشش آموزشی: بله/خیر

                // ─── GlobalID برای merge هوشمند Backup ──────────────────────────
                EnsureColumn(con, "TblCase", "GlobalID", "TEXT NULL");
                ExecuteNonQuery(con, "CREATE UNIQUE INDEX IF NOT EXISTS IX_TblCase_GlobalID ON TblCase(GlobalID) WHERE GlobalID IS NOT NULL;");
                // رکوردهای قدیمی که GlobalID ندارند: یک UUID ساده تولید می‌شود
                ExecuteNonQuery(con, @"
UPDATE TblCase
SET GlobalID = lower(hex(randomblob(4))) || '-'
             || lower(hex(randomblob(2))) || '-'
             || lower(hex(randomblob(2))) || '-'
             || lower(hex(randomblob(2))) || '-'
             || lower(hex(randomblob(6)))
WHERE GlobalID IS NULL;");

                // ─── افزودن CenterID به جداول اصلی (مهاجرت چندمرکزی) ─────────
                // DEFAULT 1 یعنی رکوردهای قدیمی به مرکز اصلی (001) منتقل می‌شوند.
                EnsureColumn(con, "TblCase", "CenterID", "INTEGER NOT NULL DEFAULT 1");
                ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_CenterID ON TblCase(CenterID);");

                // ─── GlobalID روی جداول فرزند (رفع باگ: Merge بکاپ رکوردهای
                //     خانواده/سند/کمک را برای پرونده‌های از قبل موجود دوباره
                //     درج می‌کرد چون هیچ کلید یکتای سراسری برای تشخیص تکراری
                //     نداشتند). الگوی مشابه TblCase.GlobalID اینجا هم اجرا می‌شود.
                EnsureChildGlobalId(con, "TblFamily",    "FamID");
                EnsureChildGlobalId(con, "TblDocs",      "DocID");
                EnsureChildGlobalId(con, "TblAssistance","AssistanceID");

                // ─── مهاجرت: "در انتظار" → "در انتظار تأیید" + ستون StopReason ──
                MigrateServiceStatusRebuild(con);

                // ─── رفع باگ حیاتی: MigrateServiceStatusRebuild با RENAME موقت
                //     TblCase → TblCase_old باعث شد SQLite به‌طور خودکار متن
                //     FOREIGN KEY جداول فرزند (TblFamily/TblDocs/TblAssistance)
                //     را به "TblCase_old" بازنویسی کند؛ چون آن جدول در پایان
                //     migration حذف شده بود، از آن پس هر INSERT/DELETE روی این
                //     جداول (با فعال بودن Foreign Key Enforcement) با خطای
                //     «no such table: main.TblCase_old» شکست می‌خورد. این تابع
                //     آن را برای دیتابیس‌هایی که از این مشکل آسیب دیده‌اند ترمیم
                //     می‌کند؛ روی دیتابیس سالم کاری انجام نمی‌دهد (idempotent).
                RepairBrokenChildForeignKey(con, "TblFamily",     "FK_Family_Case",     "ON DELETE CASCADE");
                RepairBrokenChildForeignKey(con, "TblDocs",       "FK_Docs_Case",       "ON DELETE CASCADE");
                RepairBrokenChildForeignKey(con, "TblAssistance", "FK_Assistance_Case", "ON DELETE CASCADE");
                // آموزش — باگ قبلی: چون EnsureDefaultLookups (بالاتر) از قبل مقدار
                // "در انتظار تأیید" را با INSERT OR IGNORE درج کرده، UPDATE برای تغییر
                // نام رکورد قدیمی "در انتظار" به همان مقدار با خطای UNIQUE(Category,Value)
                // شکست می‌خورد. چون مقدار جدید تضمین‌شده از قبل وجود دارد، به‌جای rename
                // فقط رکورد قدیمی تکراری حذف می‌شود.
                ExecuteNonQuery(con, @"
DELETE FROM TblLookup WHERE Category = 'ServiceStatus' AND Value = 'در انتظار';");

                // ─── اصلاح فهرست ولایات: حذف مقادیر تکراری/اشتباه، افزودن پنجشیر ──
                ExecuteNonQuery(con, "DELETE FROM TblLookup WHERE Category = 'Province' AND Value IN ('گور', 'میدان وردک');");
                ExecuteNonQuery(con, "INSERT OR IGNORE INTO TblLookup (Category, Value, SortOrder) VALUES ('Province', 'پنجشیر', 35);");

                // ─── مهاجرت کامل Schema (B2): افزودن ستون‌های گمشده و بازسازی
                //     جداولی که در نسخه‌های خیلی قدیمی ساختار متفاوت داشتند.
                MigrateSchema(con);

                // ─── یکسان‌سازی فرمت تاریخ (B1): همه تاریخ‌های موجود به yyyy-MM-dd ─
                NormalizeDateColumns(con);

                // ═══════════════════════════════════════════════════════════════
                // بخش تنظیمات پیشرفته (Control Center) — زیرساخت پایه (بدون تغییر
                // در فرم‌ها؛ فقط ستون‌ها/دسته‌های Lookup جدید، افزایشی و بی‌خطر)
                // ═══════════════════════════════════════════════════════════════

                // ─── فیلدهای جدید مرکز: هر مرکز حالا می‌تواند ولایت/آدرس/تلفن/
                //     مسئول/ایمیل/لوگو/رنگ اختصاصی داشته باشد ──────────────────
                EnsureColumn(con, "TblCenter", "Province",    "TEXT NULL");
                EnsureColumn(con, "TblCenter", "Address",     "TEXT NULL");
                EnsureColumn(con, "TblCenter", "Phone",       "TEXT NULL");
                EnsureColumn(con, "TblCenter", "ManagerName", "TEXT NULL");
                EnsureColumn(con, "TblCenter", "Email",       "TEXT NULL");
                EnsureColumn(con, "TblCenter", "LogoPath",    "TEXT NULL");
                EnsureColumn(con, "TblCenter", "Color",       "TEXT NULL");

                // ─── تاریخ آخرین تغییر رمز (برای تنظیم «اجبار تغییر دوره‌ای رمز») ──
                EnsureColumn(con, "TblUsers", "LastPasswordChangeAt", "TEXT NULL");

                // ─── رفع ناسازگاری موجود بین مقادیر هاردکد فرم‌ها و دیتابیس:
                //     چند دسته Lookup از قبل در دیتابیس با مقادیر قدیمی/متفاوت از
                //     چیزی که الان واقعاً در FrmCase/FrmFamily نمایش داده می‌شود
                //     وجود داشتند (مثلاً RequestType قدیمی: ولایتی/آواره داخلی...).
                //     چون این پروژه هنوز داده واقعی کاربر ندارد که به این مقادیر
                //     دقیق وابسته باشد (فیلد آزاد متنی در TblCase، نه FK)، دیتابیس
                //     با مقدار واقعیِ در حال استفاده هم‌سو می‌شود تا وقتی این
                //     دسته‌ها از دیتابیس بارگذاری شوند (گام بعدی)، هیچ تغییری در
                //     رفتار دیده‌شده کاربر رخ ندهد.
                ReconcileLookupCategory(con, "RequestType",
                    new[] { "یتیم", "معلول", "مهاجر", "بدسرپرست", "کهولت سن", "بی‌سرپرست" });
                ReconcileLookupCategory(con, "PriorityLevel",
                    new[] { "اول", "دوم", "سوم" });
                ReconcileLookupCategory(con, "MaritalStatus",
                    new[] { "مجرد", "متأهل", "مطلقه" });

                // ─── دسته‌های جدید Lookup — دقیقاً همان مقادیری که الان در
                //     Designer.cs هاردکد هستند تا هیچ تغییر رفتاری رخ ندهد؛ بعد
                //     از این، مدیر سیستم می‌تواند این‌ها را از تنظیمات ویرایش کند.
                //     نکته: «مذهب» فرم‌ها در واقع «تشیع/تسنن» است، نه دین جهانی؛
                //     برای جلوگیری از تداخل با دسته موجود Religion (اسلام/مسیحیت/
                //     هندو/سیک که فعلاً در هیچ فرمی استفاده نمی‌شود)، دسته جدید و
                //     مجزای Madhab برای همان کمبوی «مذهب» واقعی در فرم‌ها ساخته شد.
                EnsureDefaultLookupSet(con, "CoveredByOrg",       new[] { "بله", "خیر" });
                EnsureDefaultLookupSet(con, "Madhab",             new[] { "اهل تشیع", "اهل تسنن" });
                EnsureDefaultLookupSet(con, "HeadSadat",          new[] { "عام", "سادات" });
                EnsureDefaultLookupSet(con, "MemberSadat",        new[] { "عام", "سادات", "همه" });
                EnsureDefaultLookupSet(con, "DisabilityDegree",   new[] { "اول", "دوم", "سوم" });
                EnsureDefaultLookupSet(con, "HeadEducationLevel", new[] { "ابتدایی", "متوسط", "عالی", "لیسانس", "دکترا", "طلبه", "بی‌سواد" });
                EnsureDefaultLookupSet(con, "MemberEducation",    new[] { "مکتب", "دانشگاه", "طلبه", "بی‌سواد", "ترک تحصیل" });
                EnsureDefaultLookupSet(con, "MemberGender",       new[] { "دختر", "پسر" });
                EnsureDefaultLookupSet(con, "PhysicalStatus",     new[] { "سالم", "معلول", "مریض" });
                EnsureDefaultLookupSet(con, "StudyYear",          new[]
                {
                    "سمستر اول", "سمستر دوم", "سمستر سوم", "سمستر چهارم",
                    "سمستر پنجم", "سمستر ششم", "سمستر هفتم", "سمستر هشتم",
                    "لیسانس", "ماستری", "دکترا"
                });
                EnsureDefaultLookupSet(con, "GradeLevel", new[]
                {
                    "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12"
                });
                EnsureDefaultLookupSet(con, "AssistanceType", new[] { "نقدی", "غیرنقدی" });
                EnsureDefaultLookupSet(con, "DocType", new string[0]); // فعلاً مقدار پیش‌فرض ندارد؛ فقط دسته ساخته می‌شود

                // ─── ساخت admin پیش‌فرض ──────────────────────────────────────
                EnsureDefaultAdmin(con);

                // ─── شماره‌فرمِ گمشده را پر کن (مثلاً پرونده‌های همگام‌شده‌ی قدیمی
                // که بدون FormNo ثبت شده‌اند) تا خروجی جمعی روی آن‌ها هم کار کند.
                BackfillMissingFormNumbers(con);
            }
        }

        // به پرونده‌هایی که «شماره فرم» ندارند (NULL/خالی/غیرعددی) یک شماره‌ی یکتا
        // و افزایشی می‌دهد که از بزرگ‌ترین شماره‌ی فعلی شروع می‌شود. idempotent است:
        // روی پرونده‌هایی که از قبل شماره دارند دست نمی‌زند.
        private static void BackfillMissingFormNumbers(SQLiteConnection con)
        {
            int next;
            using (var cmd = new SQLiteCommand(
                "SELECT COALESCE(MAX(CAST(CASE WHEN FormNo GLOB '*[0-9]*' AND FormNo NOT GLOB '*[^0-9]*' THEN FormNo ELSE '0' END AS INTEGER)), 0) + 1 FROM TblCase", con))
            {
                object r = cmd.ExecuteScalar();
                next = (r == null || r == DBNull.Value) ? 1 : Convert.ToInt32(r);
            }

            var missing = new System.Collections.Generic.List<long>();
            using (var cmd = new SQLiteCommand(
                "SELECT CasID FROM TblCase WHERE FormNo IS NULL OR TRIM(COALESCE(FormNo,'')) = '' OR FormNo GLOB '*[^0-9]*' ORDER BY CasID", con))
            using (var dr = cmd.ExecuteReader())
                while (dr.Read())
                    missing.Add(Convert.ToInt64(dr["CasID"]));

            if (missing.Count == 0) return;

            using (var tr = con.BeginTransaction())
            {
                foreach (long casId in missing)
                {
                    using (var cmd = new SQLiteCommand(
                        "UPDATE TblCase SET FormNo = @F WHERE CasID = @Id", con, tr))
                    {
                        cmd.Parameters.AddWithValue("@F", next.ToString());
                        cmd.Parameters.AddWithValue("@Id", casId);
                        cmd.ExecuteNonQuery();
                    }
                    next++;
                }
                tr.Commit();
            }
        }

        // یک دسته Lookup را دقیقاً برابر مجموعه مقادیر داده‌شده می‌کند: مقادیر
        // اضافی/قدیمی حذف، مقادیر گمشده درج می‌شوند. چون فیلدهای TblCase/TblFamily
        // مرتبط متن آزاد هستند (نه FK)، این تغییر امن است و داده رکوردهای موجود
        // را دست‌نخورده نگه می‌دارد (فقط فهرست انتخاب کشویی به‌روز می‌شود).
        private static void ReconcileLookupCategory(SQLiteConnection con, string category, string[] correctValues)
        {
            string placeholders = correctValues.Length > 0
                ? string.Join(",", BuildPlaceholders(correctValues.Length))
                : "''"; // فهرست خالی: چیزی نباید نگه داشته شود (NOT IN ('') یعنی همه حذف می‌شوند)

            using (var cmd = new SQLiteCommand(
                "DELETE FROM TblLookup WHERE Category = @Cat AND Value NOT IN (" + placeholders + ")", con))
            {
                cmd.Parameters.AddWithValue("@Cat", category);
                for (int i = 0; i < correctValues.Length; i++)
                    cmd.Parameters.AddWithValue("@v" + i, correctValues[i]);
                cmd.ExecuteNonQuery();
            }

            for (int i = 0; i < correctValues.Length; i++)
            {
                // آموزش — از INSERT...ON CONFLICT DO UPDATE به‌جای INSERT OR IGNORE
                // استفاده شد چون IGNORE برای رکوردهای از قبل موجود (مثلاً «مهاجر» که
                // در فهرست قدیمی هم بود) SortOrder را دست‌نخورده می‌گذاشت و ترتیب
                // نمایش کشویی را به‌هم می‌ریخت.
                using (var cmd = new SQLiteCommand(@"
INSERT INTO TblLookup (Category, Value, SortOrder) VALUES (@Cat, @Val, @Ord)
ON CONFLICT(Category, Value) DO UPDATE SET SortOrder = @Ord", con))
                {
                    cmd.Parameters.AddWithValue("@Cat", category);
                    cmd.Parameters.AddWithValue("@Val", correctValues[i]);
                    cmd.Parameters.AddWithValue("@Ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static string[] BuildPlaceholders(int count)
        {
            var result = new string[count];
            for (int i = 0; i < count; i++)
                result[i] = "@v" + i;
            return result;
        }

        // یک دسته Lookup تازه را فقط اگر خالی است با مقادیر پیش‌فرض پر می‌کند
        // (اگر مدیر سیستم قبلاً مقداری اضافه/حذف کرده باشد دست نمی‌زند).
        private static void EnsureDefaultLookupSet(SQLiteConnection con, string category, string[] defaultValues)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(1) FROM TblLookup WHERE Category = @Cat", con))
            {
                cmd.Parameters.AddWithValue("@Cat", category);
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    return; // قبلاً مقداردهی شده (یا مدیر سیستم دستی مدیریت کرده)
            }

            for (int i = 0; i < defaultValues.Length; i++)
            {
                using (var cmd = new SQLiteCommand(
                    "INSERT OR IGNORE INTO TblLookup (Category, Value, SortOrder) VALUES (@Cat, @Val, @Ord)", con))
                {
                    cmd.Parameters.AddWithValue("@Cat", category);
                    cmd.Parameters.AddWithValue("@Val", defaultValues[i]);
                    cmd.Parameters.AddWithValue("@Ord", i);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ─── تشخیص FK شکسته: آیا محدودیت FOREIGN KEY این جدول به جدولی غیر از
        //     TblCase اشاره می‌کند (مثلاً به TblCase_old که دیگر وجود ندارد)؟
        private static bool IsChildForeignKeyBroken(SQLiteConnection con, string tableName)
        {
            using (var cmd = new SQLiteCommand("PRAGMA foreign_key_list(" + tableName + ")", con))
            using (var reader = cmd.ExecuteReader())
            {
                bool found = false;
                while (reader.Read())
                {
                    found = true;
                    string referencedTable = reader["table"].ToString();
                    if (!string.Equals(referencedTable, "TblCase", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                // اگر اصلاً FK پیدا نشد هم یعنی چیزی خراب است (باید همیشه یکی باشد)
                return !found;
            }
        }

        // ─── بازسازی جدول فرزند با FOREIGN KEY درست، با حفظ کامل تمام
        //     ستون‌ها (هرچه در طول migration های مختلف اضافه شده) و داده‌ها.
        private static void RepairBrokenChildForeignKey(SQLiteConnection con, string tableName,
                                                          string fkConstraintName, string onDeleteClause)
        {
            if (!TableExists(con, tableName))
                return;

            if (!IsChildForeignKeyBroken(con, tableName))
                return; // سالم است؛ کاری لازم نیست (idempotent)

            List<ColumnDef> columns = GetColumnDefs(con, tableName);
            string tempName = tableName + "_fkfix_old";

            // PRAGMA foreign_keys نباید داخل تراکنش فعال تغییر کند؛ اینجا خارج
            // از هر تراکنشی هستیم (این متد در ابتدای EnsureDatabaseObjects اجرا می‌شود).
            ExecuteNonQuery(con, "PRAGMA foreign_keys = OFF;");

            try
            {
                using (var tr = con.BeginTransaction())
                {
                    try
                    {
                        ExecuteNonQuery(con, tr, "ALTER TABLE " + tableName + " RENAME TO " + tempName + ";");

                        string colNames = "";
                        string colDefs = "";
                        for (int i = 0; i < columns.Count; i++)
                        {
                            if (i > 0) { colNames += ", "; colDefs += ",\n    "; }
                            colNames += "[" + columns[i].Name + "]";
                            colDefs += BuildColumnDefText(columns[i]);
                        }

                        ExecuteNonQuery(con, tr, string.Format(@"
CREATE TABLE {0} (
    {1},
    CONSTRAINT {2} FOREIGN KEY (CasID) REFERENCES TblCase (CasID) {3}
);", tableName, colDefs, fkConstraintName, onDeleteClause));

                        ExecuteNonQuery(con, tr, string.Format(
                            "INSERT INTO {0} ({1}) SELECT {1} FROM {2};",
                            tableName, colNames, tempName));

                        ExecuteNonQuery(con, tr, "DROP TABLE " + tempName + ";");

                        tr.Commit();
                    }
                    catch
                    {
                        try { tr.Rollback(); } catch { }
                        throw;
                    }
                }
            }
            finally
            {
                ExecuteNonQuery(con, "PRAGMA foreign_keys = ON;");
            }

            // ایندکس‌های این جدول با DROP TABLE از بین رفتند؛ دوباره ساخته می‌شوند.
            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_" + tableName + "_CasID ON " + tableName + "(CasID);");
            ExecuteNonQuery(con, "CREATE UNIQUE INDEX IF NOT EXISTS IX_" + tableName + "_GlobalID ON " + tableName + "(GlobalID) WHERE GlobalID IS NOT NULL;");
        }

        private class ColumnDef
        {
            public string Name;
            public string Type;
            public bool NotNull;
            public string DefaultValue;
            public bool IsPrimaryKey;
        }

        private static List<ColumnDef> GetColumnDefs(SQLiteConnection con, string tableName)
        {
            var result = new List<ColumnDef>();
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + tableName + ")", con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(new ColumnDef
                    {
                        Name = reader["name"].ToString(),
                        Type = reader["type"].ToString(),
                        NotNull = Convert.ToInt32(reader["notnull"]) == 1,
                        DefaultValue = reader["dflt_value"] == DBNull.Value ? null : reader["dflt_value"].ToString(),
                        IsPrimaryKey = Convert.ToInt32(reader["pk"]) == 1
                    });
                }
            }
            return result;
        }

        private static string BuildColumnDefText(ColumnDef col)
        {
            if (col.IsPrimaryKey)
                return "[" + col.Name + "] INTEGER PRIMARY KEY AUTOINCREMENT";

            string text = "[" + col.Name + "] " + (string.IsNullOrWhiteSpace(col.Type) ? "TEXT" : col.Type);
            text += col.NotNull ? " NOT NULL" : " NULL";

            if (!string.IsNullOrWhiteSpace(col.DefaultValue))
            {
                // آموزش — باگ ظریف: PRAGMA table_info مقدار DEFAULT تابعی مثل
                // (datetime('now')) را به‌صورت "datetime('now')" (بدون پرانتز
                // بیرونی) برمی‌گرداند. اگر همان را مستقیم بعد از DEFAULT بگذاریم،
                // CREATE TABLE با خطای Syntax شکست می‌خورد (SQLite برای DEFAULT
                // مبتنی بر Expression الزاماً پرانتز می‌خواهد؛ فقط مقادیر ثابت
                // مثل رشته/عدد/NULL بدون پرانتز مجازند).
                string dv = col.DefaultValue.Trim();
                bool isSafeLiteral =
                    dv.StartsWith("'") || dv.StartsWith("\"") ||
                    string.Equals(dv, "NULL", StringComparison.OrdinalIgnoreCase) ||
                    (dv.StartsWith("(") && dv.EndsWith(")")) ||
                    IsNumericLiteral(dv);

                text += isSafeLiteral ? " DEFAULT " + dv : " DEFAULT (" + dv + ")";
            }

            return text;
        }

        private static bool IsNumericLiteral(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int start = (s[0] == '-' || s[0] == '+') ? 1 : 0;
            if (start >= s.Length) return false;
            bool hasDigit = false;
            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '.') continue;
                if (!char.IsDigit(c)) return false;
                hasDigit = true;
            }
            return hasDigit;
        }

        // ─── افزودن GlobalID به یک جدول فرزند (خانواده/سند/کمک) + پرکردن مقدار
        //     برای رکوردهای قدیمی + ایندکس یکتا. idempotent (اجرای مکرر بی‌خطر).
        private static void EnsureChildGlobalId(SQLiteConnection con, string tableName, string pkColumn)
        {
            EnsureColumn(con, tableName, "GlobalID", "TEXT NULL");
            ExecuteNonQuery(con, string.Format(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_{0}_GlobalID ON {0}(GlobalID) WHERE GlobalID IS NOT NULL;",
                tableName));
            ExecuteNonQuery(con, string.Format(@"
UPDATE {0}
SET GlobalID = lower(hex(randomblob(4))) || '-'
             || lower(hex(randomblob(2))) || '-'
             || lower(hex(randomblob(2))) || '-'
             || lower(hex(randomblob(2))) || '-'
             || lower(hex(randomblob(6)))
WHERE GlobalID IS NULL;", tableName));
        }

        // ─── مقادیر پایه پیش‌فرض — فقط آنچه در دیتابیس وجود ندارد ─────────
        private static void EnsureDefaultLookups(SQLiteConnection con)
        {
            var defaults = new[]
            {
                // ServiceStatus — باید با CHECK constraint هماهنگ باشد
                new[] { "ServiceStatus",   "فعال",            "0" },
                new[] { "ServiceStatus",   "در انتظار تأیید", "1" },
                new[] { "ServiceStatus",   "قطع موقت",        "2" },
                new[] { "ServiceStatus",   "قطع",             "3" },
                // RequestType
                new[] { "RequestType",     "ولایتی",          "0" },
                new[] { "RequestType",     "مهاجر",           "1" },
                new[] { "RequestType",     "آواره داخلی",     "2" },
                new[] { "RequestType",     "عودت‌کننده",      "3" },
                // PriorityLevel
                new[] { "PriorityLevel",   "بحرانی",          "0" },
                new[] { "PriorityLevel",   "اورژانسی",        "1" },
                new[] { "PriorityLevel",   "عادی",            "2" },
                // MaritalStatus
                new[] { "MaritalStatus",   "متأهل",           "0" },
                new[] { "MaritalStatus",   "مجرد",            "1" },
                new[] { "MaritalStatus",   "بیوه",            "2" },
                new[] { "MaritalStatus",   "طلاق‌گرفته",      "3" },
                // Religion
                new[] { "Religion",        "اسلام",           "0" },
                new[] { "Religion",        "مسیحیت",          "1" },
                new[] { "Religion",        "هندو",            "2" },
                new[] { "Religion",        "سیک",             "3" },
                // DisabilityType
                new[] { "DisabilityType",  "جسمی",            "0" },
                new[] { "DisabilityType",  "ذهنی",            "1" },
                new[] { "DisabilityType",  "بینایی",          "2" },
                new[] { "DisabilityType",  "شنوایی",          "3" },
                new[] { "DisabilityType",  "گفتاری",          "4" },
                new[] { "DisabilityType",  "حسی",             "5" },
                // MigrationCardType
                new[] { "MigrationCardType","هویت مهاجر",     "0" },
                new[] { "MigrationCardType","برگه عودت",      "1" },
                new[] { "MigrationCardType","پناهجو",         "2" },
                new[] { "MigrationCardType","سند کمیساریا",   "3" },
                // Gender
                new[] { "Gender",          "مذکر",            "0" },
                new[] { "Gender",          "مؤنث",            "1" },
                // Province (اصلی‌ترین ولایات)
                new[] { "Province",        "کابل",            "0" },
                new[] { "Province",        "هرات",            "1" },
                new[] { "Province",        "بلخ",             "2" },
                new[] { "Province",        "قندهار",          "3" },
                new[] { "Province",        "ننگرهار",         "4" },
                new[] { "Province",        "بدخشان",          "5" },
                new[] { "Province",        "بغلان",           "6" },
                new[] { "Province",        "تخار",            "7" },
                new[] { "Province",        "غزنی",            "8" },
                new[] { "Province",        "هلمند",           "9" },
                new[] { "Province",        "لغمان",           "10"},
                new[] { "Province",        "کندز",            "11"},
                new[] { "Province",        "فاریاب",          "12"},
                new[] { "Province",        "جوزجان",          "13"},
                new[] { "Province",        "سمنگان",          "14"},
                new[] { "Province",        "بامیان",          "15"},
                new[] { "Province",        "پکتیا",           "16"},
                new[] { "Province",        "لوگر",            "17"},
                new[] { "Province",        "وردک",            "18"},
                new[] { "Province",        "غور",             "19"},
                new[] { "Province",        "فراه",            "20"},
                new[] { "Province",        "خوست",            "21"},
                new[] { "Province",        "کاپیسا",          "22"},
                new[] { "Province",        "پروان",           "23"},
                new[] { "Province",        "زابل",            "24"},
                new[] { "Province",        "ارزگان",          "25"},
                new[] { "Province",        "نیمروز",          "26"},
                new[] { "Province",        "نورستان",         "27"},
                new[] { "Province",        "کنر",             "28"},
                new[] { "Province",        "سرپل",            "29"},
                new[] { "Province",        "دایکندی",         "30"},
                new[] { "Province",        "پکتیکا",          "31"},
                new[] { "Province",        "بادغیس",          "32"},
                new[] { "Province",        "پنجشیر",          "35"},
            };

            using (var tr = con.BeginTransaction())
            {
                foreach (var row in defaults)
                {
                    using (var cmd = new SQLiteCommand(
                        "INSERT OR IGNORE INTO TblLookup (Category, Value, SortOrder) VALUES (@Cat, @Val, @Ord)", con, tr))
                    {
                        cmd.Parameters.AddWithValue("@Cat", row[0]);
                        cmd.Parameters.AddWithValue("@Val", row[1]);
                        cmd.Parameters.AddWithValue("@Ord", int.Parse(row[2]));
                        cmd.ExecuteNonQuery();
                    }
                }
                tr.Commit();
            }
        }

        // ─── مراکز پیش‌فرض — فقط اگر جدول خالی باشد ──────────────────────────
        private static void EnsureDefaultCenters(SQLiteConnection con)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(1) FROM TblCenter", con))
            {
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    return;
            }

            string[][] centers = new string[][]
            {
                new[] { "001", "کابل"        },
                new[] { "002", "بلخ"          },
                new[] { "003", "هرات"         },
                new[] { "004", "قندهار"       },
                new[] { "005", "مزارشریف"     },
                new[] { "006", "جلال‌آباد"    },
                new[] { "007", "کندز"         },
                new[] { "008", "پکتیا"        },
                new[] { "009", "بغلان"        },
                new[] { "010", "تخار"         },
                new[] { "011", "مرکز اصلی"   },
            };

            using (var tr = con.BeginTransaction())
            {
                foreach (var c in centers)
                {
                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO TblCenter (CenterCode, CenterName) VALUES (@Code, @Name)", con, tr))
                    {
                        cmd.Parameters.AddWithValue("@Code", c[0]);
                        cmd.Parameters.AddWithValue("@Name", c[1]);
                        cmd.ExecuteNonQuery();
                    }
                }
                tr.Commit();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // مهاجرت: تغییر نام وضعیت "در انتظار" به "در انتظار تأیید" + افزودن
        // ستون StopReason ("دلیل قطع موقت"). SQLite اجازه تغییر CHECK constraint
        // موجود روی جدول را نمی‌دهد، پس جدول با قید جدید بازسازی می‌شود.
        // این تابع فقط یک‌بار اجرا می‌شود (idempotent — تشخیص از وجود ستون StopReason).
        // نکته: باید بعد از EnsureColumn(GlobalID) و EnsureColumn(CenterID) فراخوانی
        // شود چون داده‌های آن دو ستون هنگام کپی داده حفظ می‌شوند.
        // ─────────────────────────────────────────────────────────────────────
        private static void MigrateServiceStatusRebuild(SQLiteConnection con)
        {
            if (!TableExists(con, "TblCase") || ColumnExists(con, "TblCase", "StopReason"))
                return;

            using (var tr = con.BeginTransaction())
            {
                try
                {
                    ExecuteNonQuery(con, tr, "ALTER TABLE TblCase RENAME TO TblCase_old;");

                    ExecuteNonQuery(con, tr, @"
CREATE TABLE TblCase (
    CasID                 INTEGER PRIMARY KEY AUTOINCREMENT,
    FormNo                INTEGER NULL,
    Code                  TEXT NULL,
    Phone                 TEXT NULL,
    PhotoPath             TEXT NULL,
    FamilyPhotoPath       TEXT NULL,
    CaseNo                TEXT NULL,
    CaseDate              TEXT NULL,
    District              TEXT NULL,
    RequestType           TEXT NULL,
    PriorityLevel         TEXT NULL,
    HeadFullName          TEXT NULL,
    HeadFatherName        TEXT NULL,
    HeadSadat             TEXT NULL,
    Religion              TEXT NULL,
    HeadTazkiraNo         TEXT NULL,
    HeadOriginalResidence TEXT NULL,
    HeadCurrentResidence  TEXT NULL,
    RelationshipToFamily  TEXT NULL,
    RelativePhone         TEXT NULL,
    CoveredByOrg          TEXT NULL,
    Job                   TEXT NULL,
    Skill                 TEXT NULL,
    DisabilityDegree      TEXT NULL,
    DisabilityType        TEXT NULL,
    MigrationCardType     TEXT NULL,
    MaritalStatus         TEXT NULL,
    Surveyors             TEXT NULL,
    SurveyDate            TEXT NULL,
    LocationAddress       TEXT NULL,
    EducationLevel        TEXT NULL,
    ServiceStatus         TEXT NOT NULL DEFAULT 'فعال',
    StopReason            TEXT NULL,
    UrgentSituation       TEXT NULL,
    Zone                  TEXT NULL,
    Province              TEXT NULL,
    GlobalID              TEXT NULL,
    CenterID              INTEGER NOT NULL DEFAULT 1,
    CreatedAt             TEXT NOT NULL DEFAULT (datetime('now')),
    UpdatedAt             TEXT NULL,
    CONSTRAINT UQ_TblCase_Code   UNIQUE (Code),
    CONSTRAINT UQ_TblCase_FormNo UNIQUE (FormNo),
    CONSTRAINT CK_TblCase_ServiceStatus
        CHECK (ServiceStatus IN ('قطع', 'قطع موقت', 'در انتظار تأیید', 'فعال'))
);");

                    ExecuteNonQuery(con, tr, @"
INSERT INTO TblCase
(
    CasID, FormNo, Code, Phone, PhotoPath, FamilyPhotoPath, CaseNo, CaseDate,
    District, RequestType, PriorityLevel, HeadFullName, HeadFatherName, HeadSadat,
    Religion, HeadTazkiraNo, HeadOriginalResidence, HeadCurrentResidence,
    RelationshipToFamily, RelativePhone, CoveredByOrg, Job, Skill,
    DisabilityDegree, DisabilityType, MigrationCardType, MaritalStatus,
    Surveyors, SurveyDate, LocationAddress, EducationLevel, ServiceStatus, StopReason,
    UrgentSituation, Zone, Province, GlobalID, CenterID, CreatedAt, UpdatedAt
)
SELECT
    CasID, FormNo, Code, Phone, PhotoPath, FamilyPhotoPath, CaseNo, CaseDate,
    District, RequestType, PriorityLevel, HeadFullName, HeadFatherName, HeadSadat,
    Religion, HeadTazkiraNo, HeadOriginalResidence, HeadCurrentResidence,
    RelationshipToFamily, RelativePhone, CoveredByOrg, Job, Skill,
    DisabilityDegree, DisabilityType, MigrationCardType, MaritalStatus,
    Surveyors, SurveyDate, LocationAddress, EducationLevel,
    CASE WHEN ServiceStatus = 'در انتظار' THEN 'در انتظار تأیید' ELSE ServiceStatus END,
    NULL,
    UrgentSituation, Zone, Province, GlobalID, CenterID, CreatedAt, UpdatedAt
FROM TblCase_old;");

                    ExecuteNonQuery(con, tr, "DROP TABLE TblCase_old;");
                    tr.Commit();
                }
                catch
                {
                    try { tr.Rollback(); } catch { }
                    throw;
                }
            }

            // بازسازی ایندکس‌ها (جدول جدید تازه ساخته شده، ایندکس ندارد)
            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_CaseDate ON TblCase(CaseDate);");
            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_Code ON TblCase(Code);");
            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_FormNo ON TblCase(FormNo);");
            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_Province ON TblCase(Province);");
            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_ProvinceDistrict ON TblCase(Province, District);");
            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_ServiceStatus ON TblCase(ServiceStatus);");
            ExecuteNonQuery(con, "CREATE UNIQUE INDEX IF NOT EXISTS IX_TblCase_GlobalID ON TblCase(GlobalID) WHERE GlobalID IS NOT NULL;");
            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblCase_CenterID ON TblCase(CenterID);");
        }

        // مهاجرت: نسخه‌های قبلی TblAssistance بدون FOREIGN KEY ساخته شده بودند
        // (حذف پرونده رکوردهای TblAssistance را یتیم می‌گذاشت). چون SQLite اجازه
        // افزودن CONSTRAINT به جدول موجود را نمی‌دهد، جدول با ستون‌های همان بازسازی
        // می‌شود. رکوردهایی که به پرونده حذف‌شده اشاره دارند (یتیم قدیمی) کپی نمی‌شوند.
        private static void EnsureAssistanceForeignKey(SQLiteConnection con)
        {
            using (var cmd = new SQLiteCommand("PRAGMA foreign_key_list(TblAssistance)", con))
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                    return;
            }

            ExecuteNonQuery(con, "ALTER TABLE TblAssistance RENAME TO TblAssistance_old;");

            ExecuteNonQuery(con, @"
CREATE TABLE TblAssistance (
    AssistanceID   INTEGER PRIMARY KEY AUTOINCREMENT,
    CasID          INTEGER NOT NULL,
    AssistanceDate TEXT NOT NULL,
    Amount         REAL NOT NULL,
    AssistanceType TEXT NOT NULL,
    Description    TEXT NULL,
    CreatedAt      TEXT NOT NULL DEFAULT (datetime('now')),
    CreatedBy      TEXT NULL,
    CONSTRAINT FK_Assistance_Case FOREIGN KEY (CasID)
        REFERENCES TblCase (CasID) ON DELETE CASCADE
);");

            // COALESCE روی ستون‌های NOT NULL: اگر داده قدیمی مقدار نال داشته باشد
            // (مثلاً CreatedAt خالی)، کپی به جدول جدید نباید با خطای constraint
            // شکست بخورد.
            ExecuteNonQuery(con, @"
INSERT INTO TblAssistance
    (AssistanceID, CasID, AssistanceDate, Amount, AssistanceType, Description, CreatedAt, CreatedBy)
SELECT
    AssistanceID,
    CasID,
    COALESCE(AssistanceDate, date('now')),
    COALESCE(Amount, 0),
    COALESCE(AssistanceType, ''),
    Description,
    COALESCE(CreatedAt, datetime('now')),
    CreatedBy
FROM TblAssistance_old
WHERE CasID IN (SELECT CasID FROM TblCase);");

            ExecuteNonQuery(con, "DROP TABLE TblAssistance_old;");
            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblAssistance_CasID ON TblAssistance(CasID);");
        }

        // ─────────────────────────────────────────────────────────────────────
        // مهاجرت کامل Schema (B2)
        // آموزش: `CREATE TABLE IF NOT EXISTS` روی دیتابیس قدیمی که جدول را از
        // قبل دارد هیچ کاری نمی‌کند، پس ستون‌های جدید اضافه نمی‌شوند و برنامه
        // با «no such column» کرش می‌کند. این متد دو کار می‌کند:
        //   ۱. جداولی که در نسخه خیلی قدیمی ساختار کلیدی متفاوت داشتند
        //      (TblFamily/TblDocs با ستون Id به‌جای FamID/DocID) را بازسازی می‌کند.
        //   ۲. برای هر جدول، ستون‌های گمشده را با ALTER TABLE ADD COLUMN اضافه
        //      می‌کند (idempotent — اگر ستون باشد رد می‌شود، داده حذف نمی‌شود).
        // TblCase در همه نسخه‌ها کلید اصلی یکسان (CasID) داشته، پس فقط ستون‌های
        // گمشده به آن اضافه می‌شوند (بازسازی لازم نیست).
        // ─────────────────────────────────────────────────────────────────────
        private static void MigrateSchema(SQLiteConnection con)
        {
            RebuildTblFamilyIfLegacy(con);
            RebuildTblDocsIfLegacy(con);

            EnsureColumns(con, "TblCase", new[]
            {
                "FormNo INTEGER", "Code TEXT", "Phone TEXT", "PhotoPath TEXT",
                "FamilyPhotoPath TEXT", "CaseNo TEXT", "CaseDate TEXT", "District TEXT",
                "RequestType TEXT", "PriorityLevel TEXT", "HeadFullName TEXT",
                "HeadFatherName TEXT", "HeadSadat TEXT", "Religion TEXT", "HeadTazkiraNo TEXT",
                "HeadOriginalResidence TEXT", "HeadCurrentResidence TEXT", "RelationshipToFamily TEXT",
                "RelativePhone TEXT", "CoveredByOrg TEXT", "Job TEXT", "Skill TEXT",
                "DisabilityDegree TEXT", "DisabilityType TEXT", "MigrationCardType TEXT",
                "MaritalStatus TEXT", "Surveyors TEXT", "SurveyDate TEXT", "LocationAddress TEXT",
                "EducationLevel TEXT", "ServiceStatus TEXT", "UrgentSituation TEXT",
                "Zone TEXT", "Province TEXT", "CreatedAt TEXT", "UpdatedAt TEXT"
            });

            EnsureColumns(con, "TblFamily", new[]
            {
                "MemberName TEXT", "MemberFatherName TEXT", "MemberTazkiraNo TEXT", "BirthDate TEXT",
                "MemberSadat TEXT", "Gender TEXT", "PhysicalStatus TEXT", "HasDisability TEXT",
                "MemberDisabilityDegree TEXT", "MemberEducation TEXT", "SchoolName TEXT",
                "GradeLevel TEXT", "UniversityName TEXT", "StudyYear TEXT", "Major TEXT",
                "StudyField TEXT", "OfficialStatus TEXT", "Details TEXT", "MemberPhotoPath TEXT",
                "Skill TEXT", "LeaveReason TEXT"
            });

            EnsureColumns(con, "TblDocs", new[]
            {
                "DocType TEXT", "OriginalFileName TEXT", "DocFilePath TEXT",
                "RelatedCaseRef TEXT", "DocDescription TEXT"
            });
        }

        // بازسازی TblFamily اگر ساختار قدیمی (ستون Id به‌جای FamID) دارد.
        private static void RebuildTblFamilyIfLegacy(SQLiteConnection con)
        {
            if (!TableExists(con, "TblFamily") || ColumnExists(con, "TblFamily", "FamID"))
                return;
            if (!ColumnExists(con, "TblFamily", "Id"))
                return;

            bool hasName = ColumnExists(con, "TblFamily", "MemberName");
            bool hasFather = ColumnExists(con, "TblFamily", "MemberFatherName");

            using (var tr = con.BeginTransaction())
            {
                try
                {
                    ExecuteNonQuery(con, tr, "ALTER TABLE TblFamily RENAME TO TblFamily_legacy;");
                    ExecuteNonQuery(con, tr, @"
CREATE TABLE TblFamily (
    FamID                   INTEGER PRIMARY KEY AUTOINCREMENT,
    CasID                   INTEGER NOT NULL,
    MemberName              TEXT NULL,
    MemberFatherName        TEXT NULL,
    MemberTazkiraNo         TEXT NULL,
    BirthDate               TEXT NULL,
    MemberSadat             TEXT NULL,
    Gender                  TEXT NULL,
    PhysicalStatus          TEXT NULL,
    HasDisability           TEXT NULL,
    MemberDisabilityDegree  TEXT NULL,
    MemberEducation         TEXT NULL,
    SchoolName              TEXT NULL,
    GradeLevel              TEXT NULL,
    UniversityName          TEXT NULL,
    StudyYear               TEXT NULL,
    Major                   TEXT NULL,
    StudyField              TEXT NULL,
    OfficialStatus          TEXT NULL,
    Details                 TEXT NULL,
    MemberPhotoPath         TEXT NULL,
    Skill                   TEXT NULL,
    LeaveReason             TEXT NULL,
    CONSTRAINT FK_Family_Case FOREIGN KEY (CasID)
        REFERENCES TblCase (CasID) ON DELETE CASCADE
);");

                    string nameExpr = hasName ? "MemberName" : "NULL";
                    string fatherExpr = hasFather ? "MemberFatherName" : "NULL";
                    ExecuteNonQuery(con, tr, $@"
INSERT INTO TblFamily (FamID, CasID, MemberName, MemberFatherName)
SELECT Id, CasID, {nameExpr}, {fatherExpr}
FROM TblFamily_legacy
WHERE CasID IN (SELECT CasID FROM TblCase);");

                    ExecuteNonQuery(con, tr, "DROP TABLE TblFamily_legacy;");
                    tr.Commit();
                }
                catch
                {
                    try { tr.Rollback(); } catch { }
                    throw;
                }
            }

            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblFamily_CasID ON TblFamily(CasID);");
        }

        // بازسازی TblDocs اگر ساختار قدیمی (Id به‌جای DocID و FilePath به‌جای DocFilePath) دارد.
        private static void RebuildTblDocsIfLegacy(SQLiteConnection con)
        {
            if (!TableExists(con, "TblDocs") || ColumnExists(con, "TblDocs", "DocID"))
                return;
            if (!ColumnExists(con, "TblDocs", "Id"))
                return;

            bool hasFilePath = ColumnExists(con, "TblDocs", "FilePath");

            using (var tr = con.BeginTransaction())
            {
                try
                {
                    ExecuteNonQuery(con, tr, "ALTER TABLE TblDocs RENAME TO TblDocs_legacy;");
                    ExecuteNonQuery(con, tr, @"
CREATE TABLE TblDocs (
    DocID            INTEGER PRIMARY KEY AUTOINCREMENT,
    CasID            INTEGER NOT NULL,
    DocType          TEXT NULL,
    OriginalFileName TEXT NULL,
    DocFilePath      TEXT NULL,
    RelatedCaseRef   TEXT NULL,
    DocDescription   TEXT NULL,
    CONSTRAINT FK_Docs_Case FOREIGN KEY (CasID)
        REFERENCES TblCase (CasID) ON DELETE CASCADE
);");

                    string pathExpr = hasFilePath ? "FilePath" : "NULL";
                    ExecuteNonQuery(con, tr, $@"
INSERT INTO TblDocs (DocID, CasID, DocFilePath)
SELECT Id, CasID, {pathExpr}
FROM TblDocs_legacy
WHERE CasID IN (SELECT CasID FROM TblCase);");

                    ExecuteNonQuery(con, tr, "DROP TABLE TblDocs_legacy;");
                    tr.Commit();
                }
                catch
                {
                    try { tr.Rollback(); } catch { }
                    throw;
                }
            }

            ExecuteNonQuery(con, "CREATE INDEX IF NOT EXISTS IX_TblDocs_CasID ON TblDocs(CasID);");
        }

        // افزودن چند ستون در صورت نبود؛ تعریف هر ستون به شکل "Name TYPE".
        private static void EnsureColumns(SQLiteConnection con, string tableName, string[] columnDefs)
        {
            foreach (string def in columnDefs)
            {
                int spaceIndex = def.IndexOf(' ');
                string columnName = def.Substring(0, spaceIndex);
                string columnType = def.Substring(spaceIndex + 1);
                EnsureColumn(con, tableName, columnName, columnType);
            }
        }

        // ─── یکسان‌سازی فرمت تاریخ (B1) ──────────────────────────────────────
        // آموزش: نسخه‌های قبلی بعضی تاریخ‌ها را با ساعت ("2026-07-08 00:00:00")
        // و بعضی را فقط تاریخ ("2026-07-08") ذخیره می‌کردند. این ناسازگاری
        // مقایسه متنی در جستجو/گزارش را خراب می‌کرد. اینجا همه مقادیر موجود
        // که ساعت دارند به فرمت خالص yyyy-MM-dd نرمال می‌شوند. تابع date()
        // روی مقدار نامعتبر NULL برمی‌گرداند، پس با شرط IS NOT NULL از خراب
        // شدن داده جلوگیری می‌شود.
        private static void NormalizeDateColumns(SQLiteConnection con)
        {
            NormalizeDateColumn(con, "TblCase", "CaseDate");
            NormalizeDateColumn(con, "TblCase", "SurveyDate");
            NormalizeDateColumn(con, "TblFamily", "BirthDate");
            NormalizeDateColumn(con, "TblAssistance", "AssistanceDate");
        }

        private static void NormalizeDateColumn(SQLiteConnection con, string tableName, string columnName)
        {
            if (!TableExists(con, tableName) || !ColumnExists(con, tableName, columnName))
                return;

            ExecuteNonQuery(con, string.Format(@"
UPDATE {0}
SET {1} = date({1})
WHERE {1} IS NOT NULL
  AND length({1}) > 10
  AND date({1}) IS NOT NULL;", tableName, columnName));
        }

        private static bool TableExists(SQLiteConnection con, string tableName)
        {
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@n", con))
            {
                cmd.Parameters.AddWithValue("@n", tableName);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static bool ColumnExists(SQLiteConnection con, string tableName, string columnName)
        {
            using (var cmd = new SQLiteCommand($"PRAGMA table_info({tableName})", con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        // نسخه‌ای از ExecuteNonQuery که داخل یک تراکنش اجرا می‌شود.
        private static void ExecuteNonQuery(SQLiteConnection con, SQLiteTransaction tr, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, con, tr))
            {
                cmd.CommandTimeout = 120;
                cmd.ExecuteNonQuery();
            }
        }

        // بررسی وجود ستون و افزودن آن در صورت نبود (بدون حذف داده)
        private static void EnsureColumn(SQLiteConnection con, string tableName, string columnName, string columnDef)
        {
            bool exists = false;
            using (var cmd = new SQLiteCommand($"PRAGMA table_info({tableName})", con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
                ExecuteNonQuery(con, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDef}");
        }

        // ساخت admin فقط اگر هیچ کاربری وجود ندارد — تنها مسیر ساخت admin.
        // آموزش — نقش SuperAdmin: چون فرم مدیریت کاربران (FrmUsers) فقط
        // Admin/Operator/Viewer را برای ساخت کاربر جدید ارائه می‌دهد (هرکدام
        // به یک مرکز مشخص محدودند)، اولین کاربر سیستم باید SuperAdmin باشد
        // تا بتواند همه مراکز را ببیند/مدیریت کند و کاربران مراکز دیگر را
        // بسازد. بدون این، هیچ‌کس هرگز به نمای «همه مراکز» دسترسی نمی‌داشت.
        private static void EnsureDefaultAdmin(SQLiteConnection con)
        {
            using (var countCmd = new SQLiteCommand("SELECT COUNT(1) FROM TblUsers", con))
            {
                if (Convert.ToInt32(countCmd.ExecuteScalar()) > 0)
                    return;
            }

            byte[] hash;
            byte[] salt;
            int iterations;
            PasswordHelper.CreateHash("admin123", out hash, out salt, out iterations);

            using (var cmd = new SQLiteCommand(@"
INSERT INTO TblUsers (Username, PasswordHash, PasswordSalt, PasswordIterations, Role, IsActive, MustChangePassword)
VALUES (@Username, @PasswordHash, @PasswordSalt, @PasswordIterations, @Role, 1, 1)", con))
            {
                cmd.Parameters.AddWithValue("@Username", "admin");
                cmd.Parameters.AddWithValue("@PasswordHash", hash);
                cmd.Parameters.AddWithValue("@PasswordSalt", salt);
                cmd.Parameters.AddWithValue("@PasswordIterations", iterations);
                cmd.Parameters.AddWithValue("@Role", "SuperAdmin");
                cmd.ExecuteNonQuery();
            }
        }

        private static void ExecuteNonQuery(SQLiteConnection con, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, con))
            {
                cmd.CommandTimeout = 120;
                cmd.ExecuteNonQuery();
            }
        }
    }
}

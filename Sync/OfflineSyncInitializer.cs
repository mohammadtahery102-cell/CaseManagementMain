using System;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // زیرساخت «کار آفلاین و همگام‌سازی» — فاز ۲ و ۵.
    //
    // آموزش — این کلاس دقیقاً مثل EnterpriseInitializer/AccountingInitializer
    // کاملاً افزایشی است: دو جدول جدید می‌سازد و چند ستون به جدول‌های اصلی
    // اضافه می‌کند. هیچ ستون موجودی تغییر نمی‌کند، هیچ داده‌ای بازنویسی
    // نمی‌شود و هر کوئریِ فعلی دقیقاً مثل قبل کار می‌کند.
    //
    // چرا این ستون‌ها لازم‌اند (نتیجهٔ تحلیل فاز ۱):
    //   • GlobalID از قبل روی TblCase/TblFamily/TblDocs/TblAssistance هست
    //     (برای merge بکاپ ساخته شده بود) — یعنی «هویت مستقل از شعبه» که
    //     سخت‌ترین بخش همگام‌سازی است، از قبل حل شده. اینجا ساخته نمی‌شود.
    //   • ولی «کِی و توسط چه کسی تغییر کرد» فقط روی TblCase وجود داشت
    //     (CreatedAt/UpdatedAt) و روی TblFamily/TblDocs اصلاً نبود. بدون آن
    //     تشخیص تعارض ممکن نیست، چون معلوم نیست کدام نسخه تازه‌تر است.
    //   • RowVersion شمارندهٔ خوش‌بینانه (optimistic) است: پایهٔ تشخیص
    //     «این رکورد از زمانی که من گرفتمش عوض شده».
    //
    // ⚠ این فایل هیچ اتصالِ اینترنتی ندارد و هیچ حالت آنلاینِ ساختگی نمی‌سازد.
    // فقط پایهٔ داده‌ای را می‌گذارد تا لایهٔ انتقال (فاز ۳ و ۶) روی آن بنشیند.
    // ═════════════════════════════════════════════════════════════════════════
    public static class OfflineSyncInitializer
    {
        // ─── وضعیت‌های صفِ عملیات ────────────────────────────────────────────
        public const string StatePending  = "در انتظار";
        public const string StateSent     = "ارسال شد";
        public const string StateFailed   = "ناموفق";
        public const string StateConflict = "تعارض";

        // عملیاتی که پس از تصمیمِ مدیر دیگر معنا ندارد (مثلاً ویرایشِ رکوردی
        // که مدیر پذیرفته حذف شود). عمداً «ارسال شد» علامت نمی‌خورد، چون
        // ارسال نشده — و ردّش برای بازرسی باقی می‌ماند.
        public const string StateDiscarded = "کنار گذاشته شد";

        // ─── انواع عملیات ────────────────────────────────────────────────────
        public const string OperationCreate     = "ثبت";
        public const string OperationUpdate     = "ویرایش";
        public const string OperationDelete     = "حذف";
        public const string OperationAttachment = "پیوست";
        public const string OperationStatus     = "تغییر وضعیت";

        // جدول‌هایی که تغییراتشان همگام‌سازی می‌شود، و کلید اصلی هرکدام.
        // همان مجموعه‌ای که GlobalID دارند (پیش‌نیازِ همگام‌سازی).
        public static readonly string[][] SyncedTables =
        {
            new[] { "TblCase",       "CasID"        },
            new[] { "TblFamily",     "FamID"        },
            new[] { "TblDocs",       "DocID"        },
            new[] { "TblAssistance", "AssistanceID" }
        };

        public static void EnsureOfflineSyncObjects()
        {
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                EnsureOutbox(con);
                EnsureSyncState(con);
                EnsureConflicts(con);
                EnsureBaseline(con);
                EnsureFiles(con);
                EnsureFileDownloads(con);
                EnsureTrackingColumns(con);
                EnsureGlobalIdentity(con);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // فاز ۲ — صفِ محلیِ عملیات (Local Operation Queue)
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureOutbox(SQLiteConnection con)
        {
            // آموزش — چرا صفِ جدا و نه استفاده از TblAuditLog:
            // لاگ ممیزی «روایت برای انسان» است؛ ترتیب تحویل، وضعیت ارسال،
            // تعداد تلاش و خطای آخر ندارد و کاربران آن را به‌عنوان گزارش کاری
            // می‌بینند. یک صفِ همگام‌سازی به همهٔ این‌ها نیاز دارد و نباید با
            // گزارش کاری قاطی شود. TblAuditLog دست‌نخورده می‌ماند.
            //
            // چرا EntityGlobalID و نه فقط شناسهٔ محلی: شناسهٔ عددیِ محلی در
            // شعبهٔ دیگر بی‌معناست (هر دو شعبه آفلاین CasID=501 می‌سازند).
            // کلید واقعیِ تبادل، همان GlobalID موجود است.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SyncOutbox (
    OutboxID       INTEGER PRIMARY KEY AUTOINCREMENT,
    OperationType  TEXT    NOT NULL,
    EntityName     TEXT    NOT NULL,
    EntityGlobalID TEXT    NULL,
    EntityLocalID  INTEGER NULL,
    ParentGlobalID TEXT    NULL,
    RowVersion     INTEGER NOT NULL DEFAULT 1,
    Payload        TEXT    NULL,
    CenterID       INTEGER NULL,
    UserID         INTEGER NULL,
    Username       TEXT    NULL,
    MachineName    TEXT    NULL,
    CreatedAt      TEXT    NOT NULL DEFAULT (datetime('now')),
    State          TEXT    NOT NULL DEFAULT '" + StatePending + @"',
    Attempts       INTEGER NOT NULL DEFAULT 0,
    LastAttemptAt  TEXT    NULL,
    LastError      TEXT    NULL,
    SentAt         TEXT    NULL
);");

            // ترتیبِ ارسال همیشه بر اساس OutboxID است (ترتیب وقوع)، پس
            // ایندکس روی State+OutboxID دقیقاً همان چیزی است که «صفِ بعدی»
            // را بدون اسکن کامل جدول برمی‌گرداند.
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_SyncOutbox_State ON SyncOutbox(State, OutboxID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_SyncOutbox_Entity ON SyncOutbox(EntityName, EntityGlobalID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_SyncOutbox_Created ON SyncOutbox(CreatedAt);");
        }

        // ═══════════════════════════════════════════════════════════════════
        // وضعیت همگام‌سازی (نشانگرها و تنظیمات) — کلید/مقدار
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureSyncState(SQLiteConnection con)
        {
            // آموزش — چرا کلید/مقدار و نه ستون‌های ثابت: لایه‌های بعدی
            // (فاز ۳ و ۶) نشانگرهای تازه لازم خواهند داشت (زمان آخرین دریافت،
            // نشانگر صفحه‌بندی سرور، شناسهٔ دستگاه). با این قالب، افزودن آن‌ها
            // نیاز به مهاجرت جدول ندارد. دقیقاً الگوی TblAppSettings.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SyncState (
    StateKey   TEXT PRIMARY KEY,
    StateValue TEXT NULL,
    UpdatedAt  TEXT NOT NULL DEFAULT (datetime('now'))
);");
        }

        // ═══════════════════════════════════════════════════════════════════
        // تعارض‌ها — فقط *ثبت*؛ حل کردن کار فاز ۴ است
        // ═══════════════════════════════════════════════════════════════════
        public const string ConflictOpen     = "باز";
        public const string ConflictResolved = "حل شد";

        // انواع تعارضِ قابل تشخیص در این فاز.
        public const string ConflictConcurrentEdit = "ویرایش هم‌زمان";
        public const string ConflictDeletedRemote  = "حذف‌شده در برابر ویرایش";
        public const string ConflictVersion        = "نسخهٔ ناسازگار";
        public const string ConflictDuplicateCode  = "کد اختصاصی تکراری";
        public const string ConflictAttachment     = "تعارض پیوست";

        // ─── وضعیت‌های فایل (فاز ۷) ─────────────────────────────────────────
        public const string FilePending    = "در انتظار";
        public const string FileUploaded   = "ارسال شد";
        public const string FileDownloaded = "دریافت شد";
        public const string FileFailed     = "ناموفق";
        public const string FileMissing    = "فایل روی دیسک نیست";
        public const string FileConflict   = "تعارض";
        public const string FileRejected   = "رد شد";   // نوع/اندازه/مسیر نامعتبر

        // ─── وضعیت‌های صفِ دریافت (فاز B) ───────────────────────────────────
        //
        // آموزش — چرا مجموعهٔ جدا و نه همان وضعیت‌های ارسال: یک فایل می‌تواند
        // هم‌زمان «ارسال‌شده» باشد و «در انتظار دریافتِ نسخهٔ تازه‌تر». اگر یک
        // ستون هر دو را نگه می‌داشت، یکی دیگری را پاک می‌کرد و آمار سلامت
        // دروغ می‌شد.
        public const string DownloadPending   = "در انتظار دریافت";
        public const string DownloadDone      = "دریافت شد";
        public const string DownloadFailed    = "دریافت ناموفق";
        public const string DownloadCorrupt   = "خراب — هش نمی‌خواند";
        public const string DownloadDenied    = "دسترسی رد شد";
        public const string DownloadConflict  = "تعارض";
        public const string DownloadNotNeeded = "لازم نیست";
        public const string DownloadRemoved   = "در سرور حذف شده";

        private static void EnsureConflicts(SQLiteConnection con)
        {
            // آموزش — چرا هر دو نسخه (محلی و سرور) کامل ذخیره می‌شوند:
            // تصمیم‌گیریِ فاز ۴ (ادغام فیلدی یا بازبینی مدیر) بدون داشتنِ هر دو
            // طرف ممکن نیست، و اگر فقط اشاره‌گر نگه می‌داشتیم، تا زمان بازبینی
            // یکی از دو طرف می‌توانست عوض شود و تعارض غیرقابل‌داوری می‌شد.
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SyncConflict (
    ConflictID     INTEGER PRIMARY KEY AUTOINCREMENT,
    EntityName     TEXT    NOT NULL,
    EntityGlobalID TEXT    NOT NULL,
    ConflictType   TEXT    NOT NULL,
    OutboxID       INTEGER NULL,
    LocalVersion   INTEGER NULL,
    RemoteVersion  INTEGER NULL,
    LocalPayload   TEXT    NULL,
    RemotePayload  TEXT    NULL,
    CenterID       INTEGER NULL,
    DetectedBy     TEXT    NULL,
    MachineName    TEXT    NULL,
    DetectedAt     TEXT    NOT NULL DEFAULT (datetime('now')),
    Status         TEXT    NOT NULL DEFAULT '" + ConflictOpen + @"',
    Resolution     TEXT    NULL,
    ResolvedBy     TEXT    NULL,
    ResolvedAt     TEXT    NULL
);");

            Exec(con, "CREATE INDEX IF NOT EXISTS IX_SyncConflict_Status ON SyncConflict(Status, ConflictID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_SyncConflict_Entity ON SyncConflict(EntityName, EntityGlobalID);");
        }

        // ═══════════════════════════════════════════════════════════════════
        // «آخرین نسخهٔ پذیرفته‌شده» — مبنای واقعیِ ادغام سه‌طرفه
        //
        // آموزش — شکافی که این جدول پر می‌کند:
        // تا امروز، مبنای ادغام از تاریخچهٔ نسخه‌ها (EntRecordVersion) خوانده
        // می‌شد: «نسخهٔ پیش از آخرین تغییرِ محلی». خودِ SyncConflictAnalyzer در
        // توضیحاتش صادقانه می‌گفت این «آخرین نسخهٔ مشترک با سرور» نیست.
        //
        // چرا فرقشان مهم است: مبنا باید *آخرین حالتی* باشد که دو طرف روی آن
        // توافق داشتند. اگر کاربر از آخرین همگام‌سازی سه بار ویرایش کرده باشد،
        // «نسخهٔ قبلی» فقط سومین ویرایش را پس می‌زند و دو ویرایشِ اولش را
        // «تغییرِ محلی» نمی‌بیند — پس آن فیلدها به‌اشتباه «دست‌نخورده» به‌نظر
        // می‌رسند و مقدارِ سرور رویشان می‌نشیند، یا (در حالتِ محافظه‌کارانه)
        // بی‌دلیل به میز مدیر می‌روند.
        //
        // اینجا دقیقاً همان چیزی ذخیره می‌شود که *واقعاً با سرور مبادله شد*:
        //   • باری که سرور صراحتاً پذیرفت (پاسخ Accepted در جریان ارسال)
        //   • باری که از سرور گرفتیم و با موفقیت اعمال کردیم
        //
        // ⚠ یک ردیف برای هر رکورد (کلیدِ یکتا روی موجودیت+هویت سراسری): این
        // جدول تاریخچه نیست، «آخرین نقطهٔ توافق» است. رشدش کراندار است و با
        // تعداد رکوردها می‌ماند، نه با تعداد ویرایش‌ها.
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureBaseline(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SyncBaseline (
    BaselineID     INTEGER PRIMARY KEY AUTOINCREMENT,
    EntityName     TEXT    NOT NULL,
    EntityGlobalID TEXT    NOT NULL,
    RowVersion     INTEGER NOT NULL DEFAULT 0,
    Payload        TEXT    NULL,
    Source         TEXT    NULL,
    UpdatedAt      TEXT    NOT NULL DEFAULT (datetime('now'))
);");

            Exec(con, @"CREATE UNIQUE INDEX IF NOT EXISTS UX_SyncBaseline_Entity
                        ON SyncBaseline(EntityName, EntityGlobalID);");
        }

        // منبعِ ثبتِ مبنا — فقط برای بازرسی و اشکال‌زدایی.
        public const string BaselineFromPush = "پذیرشِ سرور";
        public const string BaselineFromPull = "دریافت از سرور";

        // ═══════════════════════════════════════════════════════════════════
        // فاز ۷ — فراداده‌ی همگام‌سازیِ فایل‌ها
        // ═══════════════════════════════════════════════════════════════════
        //
        // آموزش — چرا جدول جدا و نه ستون روی جدول‌های موجود:
        // مسیرِ فایل از قبل در خودِ رکورد هست (TblCase.PhotoPath،
        // TblCase.FamilyPhotoPath، TblFamily.MemberPhotoPath، TblDocs.DocFilePath)
        // و آن‌ها *دادهٔ کسب‌وکار* هستند و نباید دست بخورند. چیزی که وجود
        // نداشت، دادهٔ *همگام‌سازی* است: هش محتوا، نسخه، وضعیت ارسال/دریافت و
        // شمارندهٔ تلاش. این‌ها ماهیت متفاوتی دارند (پرتغییر، فنی، بی‌ربط به
        // گزارش‌های کاربر) پس در جدول خودشان می‌نشینند. هیچ فرادادهٔ موجودی
        // تکرار نمی‌شود — نامِ ستونِ صاحبِ مسیر ذخیره می‌شود، نه خودِ مسیر
        // به‌عنوان منبعِ حقیقت.
        //
        // ⚠ کلیدِ تطبیق «مسیر» نیست: مسیرِ محلی روی ماشین دیگر بی‌معناست.
        // هر فایل با (رکوردِ صاحب + نامِ ستون) شناخته می‌شود و محتوایش با هش.
        private static void EnsureFiles(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SyncFile (
    FileID         INTEGER PRIMARY KEY AUTOINCREMENT,
    FileGlobalID   TEXT    NOT NULL,
    EntityName     TEXT    NOT NULL,
    EntityGlobalID TEXT    NOT NULL,
    ColumnName     TEXT    NOT NULL,
    FileName       TEXT    NULL,
    LocalPath      TEXT    NULL,
    ContentHash    TEXT    NULL,
    SizeBytes      INTEGER NOT NULL DEFAULT 0,
    FileVersion    INTEGER NOT NULL DEFAULT 1,
    CreatedAt      TEXT    NOT NULL DEFAULT (datetime('now')),
    ModifiedAt     TEXT    NULL,
    SyncStatus     TEXT    NOT NULL DEFAULT '" + FilePending + @"',
    UploadState    TEXT    NOT NULL DEFAULT '" + FilePending + @"',
    DownloadState  TEXT    NULL,
    Attempts       INTEGER NOT NULL DEFAULT 0,
    LastAttemptAt  TEXT    NULL,
    LastError      TEXT    NULL,
    UploadedAt     TEXT    NULL,
    CenterID       INTEGER NULL,
    RegisteredBy   TEXT    NULL,
    MachineName    TEXT    NULL
);");

            // یک ردیف برای هر (رکورد، ستون): جلوگیری از ثبت تکراریِ یک فایل.
            Exec(con, @"CREATE UNIQUE INDEX IF NOT EXISTS UX_SyncFile_Owner
                        ON SyncFile(EntityName, EntityGlobalID, ColumnName);");

            Exec(con, "CREATE INDEX IF NOT EXISTS IX_SyncFile_Upload ON SyncFile(UploadState, FileID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_SyncFile_Hash   ON SyncFile(ContentHash);");
        }

        // ═══════════════════════════════════════════════════════════════════
        // فاز B — صفِ دریافتِ فایل
        //
        // آموزش — چرا جدولِ جدا و نه چند ستون روی SyncFile:
        // SyncFile «فایل‌هایی که این ماشین دارد» را توصیف می‌کند و کلیدش
        // (رکورد، ستون) است. صفِ دریافت اما فهرستی است که *سرور* اعلام کرده و
        // کلیدش هویت سراسریِ فایل روی سرور است. مهم‌تر: ردیفی می‌تواند در صف
        // دریافت باشد که هیچ همتای محلی ندارد (فایلِ شعبهٔ دیگر که هنوز نیامده)
        // — چنین ردیفی در SyncFile جا نمی‌شود چون هنوز مسیرِ محلی ندارد.
        //
        // ⚠ این جدول *وضعیتِ پایدار* است، نه حافظه: قطعِ برق وسطِ دریافت، ردیف
        // را در حالت «در انتظار» رها می‌کند و اجرای بعدی از همان‌جا (و از همان
        // بایت، به کمک فایل .part) ادامه می‌دهد. هیچ چیزی گم نمی‌شود.
        private static void EnsureFileDownloads(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SyncFileDownload (
    DownloadID     INTEGER PRIMARY KEY AUTOINCREMENT,
    FileGlobalID   TEXT    NOT NULL,
    EntityName     TEXT    NOT NULL,
    EntityGlobalID TEXT    NOT NULL,
    ColumnName     TEXT    NOT NULL,
    FileName       TEXT    NULL,
    FileType       TEXT    NULL,
    ContentHash    TEXT    NULL,
    SizeBytes      INTEGER NOT NULL DEFAULT 0,
    FileVersion    INTEGER NOT NULL DEFAULT 1,
    RemoteDeleted  INTEGER NOT NULL DEFAULT 0,
    RemoteCursor   INTEGER NOT NULL DEFAULT 0,
    State          TEXT    NOT NULL DEFAULT '" + DownloadPending + @"',
    Attempts       INTEGER NOT NULL DEFAULT 0,
    LastAttemptAt  TEXT    NULL,
    LastError      TEXT    NULL,
    LocalPath      TEXT    NULL,
    CompletedAt    TEXT    NULL,
    CenterID       INTEGER NULL,
    DiscoveredAt   TEXT    NOT NULL DEFAULT (datetime('now'))
);");

            // یک ردیف برای هر فایلِ سرور: فهرست‌گیریِ دوباره ردیف تکراری
            // نمی‌سازد، فقط همان را به‌روز می‌کند.
            Exec(con, @"CREATE UNIQUE INDEX IF NOT EXISTS UX_SyncFileDownload_File
                        ON SyncFileDownload(FileGlobalID);");

            Exec(con, "CREATE INDEX IF NOT EXISTS IX_SyncFileDownload_State ON SyncFileDownload(State, DownloadID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_SyncFileDownload_Owner ON SyncFileDownload(EntityName, EntityGlobalID, ColumnName);");
        }

        // ═══════════════════════════════════════════════════════════════════
        // فاز ۵ — ستون‌های حداقلیِ ردیابی روی جدول‌های اصلی
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureTrackingColumns(SQLiteConnection con)
        {
            foreach (string[] table in SyncedTables)
            {
                string name = table[0];
                if (!TableExists(con, name)) continue;

                // ⚠ فقط افزودن. هیچ ستون موجودی تغییر نمی‌کند.
                // RowVersion از ۱ شروع می‌شود: هر رکوردِ فعلی «نسخهٔ ۱» است.
                EnsureColumn(con, name, "RowVersion",     "INTEGER NOT NULL DEFAULT 1");
                EnsureColumn(con, name, "LastModifiedAt", "TEXT NULL");
                EnsureColumn(con, name, "LastModifiedBy", "TEXT NULL");
                EnsureColumn(con, name, "SyncStatus",     "TEXT NULL");
            }

            // رکوردهای موجود «هنوز همگام نشده» علامت نمی‌خورند: تا وقتی هیچ
            // سروری وجود ندارد، ادعای «همگام‌نشده» بی‌معنا و گمراه‌کننده است.
            // مقدار NULL یعنی «نامشخص/خارج از چرخهٔ همگام‌سازی».
        }

        // ═══════════════════════════════════════════════════════════════════
        // تضمینِ هویت سراسری روی *هر* مسیرِ درج
        //
        // آموزش — باگ واقعی که این بازبینی پیدا کرد: ستون GlobalID فقط یک بار
        // هنگام مهاجرت برای ردیف‌های موجود پر می‌شد و هیچ DEFAULTی نداشت. از
        // میان مسیرهای درج، TblFamily/TblDocs/TblAssistance و ورودی اکسل و
        // موتور همگام‌سازی همگی خودشان GlobalID می‌سازند — ولی «فرم پرونده»
        // (FrmCase)، یعنی اصلی‌ترین مسیر ساخت پرونده در برنامه، آن را ست
        // نمی‌کرد. نتیجه: هر پرونده‌ای که کاربر می‌ساخت تا زمان اجرای بعدیِ
        // برنامه GlobalID نداشت. این برای merge بکاپ یک باگ خاموش بود و برای
        // همگام‌سازی کشنده است (ردیف صف بدون هویت، در شعبهٔ دیگر بی‌معناست).
        //
        // چرا Trigger و نه تغییر کوئریِ فرم‌ها: هویت باید *مستقل از مسیر درج*
        // تضمین شود. با Trigger، هر مسیر موجود و هر کد آینده خودکار پوشش
        // داده می‌شود، هیچ فرمی تغییر نمی‌کند، و امکان «فراموش کردن» از بین
        // می‌رود. Trigger فقط وقتی مقدار NULL باشد کار می‌کند، پس روی
        // درج‌هایی که خودشان GlobalID می‌دهند هیچ اثری ندارد.
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureGlobalIdentity(SQLiteConnection con)
        {
            const string newUuid =
                "lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' || " +
                "lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || " +
                "lower(hex(randomblob(6)))";

            foreach (string[] table in SyncedTables)
            {
                string name = table[0];
                string key  = table[1];

                if (!TableExists(con, name) || !ColumnExists(con, name, "GlobalID")) continue;

                // ردیف‌های موجودِ بدون هویت (از جمله پرونده‌هایی که در همین
                // اجرا با فرم ساخته شده‌اند) پر می‌شوند.
                Exec(con, "UPDATE " + name + " SET GlobalID = " + newUuid + " WHERE GlobalID IS NULL;");

                Exec(con, "DROP TRIGGER IF EXISTS TR_" + name + "_GlobalID;");
                Exec(con, @"
CREATE TRIGGER TR_" + name + @"_GlobalID
AFTER INSERT ON " + name + @"
FOR EACH ROW WHEN NEW.GlobalID IS NULL
BEGIN
    UPDATE " + name + " SET GlobalID = " + newUuid + " WHERE " + key + " = NEW." + key + @";
END;");
            }
        }

        // ─── کمکی‌ها (هم‌الگو با DatabaseInitializer) ────────────────────────
        private static bool ColumnExists(SQLiteConnection con, string tableName, string columnName)
        {
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + tableName + ")", con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    if (string.Equals(Convert.ToString(reader["name"]), columnName,
                                      StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            return false;
        }

        private static bool TableExists(SQLiteConnection con, string tableName)
        {
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@n;", con))
            {
                cmd.Parameters.AddWithValue("@n", tableName);
                object result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
            }
        }

        private static void EnsureColumn(SQLiteConnection con, string tableName,
                                         string columnName, string columnDef)
        {
            bool exists = false;
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + tableName + ")", con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(Convert.ToString(reader["name"]), columnName,
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
                Exec(con, "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + columnDef + ";");
        }

        private static void Exec(SQLiteConnection con, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, con))
                cmd.ExecuteNonQuery();
        }
    }
}

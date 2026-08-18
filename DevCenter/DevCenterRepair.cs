using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.DevCenter
{
    // ═════════════════════════════════════════════════════════════════════════
    // ترمیم خودکار — تبدیلِ «مرکز کنترل» از ابزارِ *تشخیص* به ابزارِ *درمان*.
    //
    // آموزش — شکافی که این فایل پر می‌کند:
    // «دکتر دیتابیس» یازده ایراد را پیدا می‌کرد و می‌گفت «۱۲ رکورد با ارجاع
    // نامعتبر» — و همین. مدیر هیچ راهی نداشت جز تماس با برنامه‌نویس، حتی برای
    // ایرادهایی که درمانشان یک UPDATE ساده و کاملاً بی‌خطر است. نتیجه‌اش این
    // بود که ایرادهای کوچک انباشته می‌شدند تا وقتی که بزرگ شوند.
    //
    // ⚠ پنج قاعده‌ی غیرقابل‌مذاکره — هر ترمیمِ آینده هم باید رعایتشان کند:
    //
    // ۱. «هیچ ترمیمی داده‌ی کسب‌وکار را حذف نمی‌کند». هیچ DELETE ای در این
    //    فایل نیست و نباید باشد. ایرادهایی که درمانشان حذف است (مثل رکوردِ
    //    یتیم یا کدِ تکراری) عمداً ترمیمِ خودکار ندارند: تصمیمش انسانی است و
    //    باید در فرمِ مربوطه گرفته شود. اینجا فقط شمرده و گزارش می‌شوند.
    //
    // ۲. «همیشه بکاپ، پیش از اولین نوشتن». اگر بکاپ‌گیری شکست بخورد، هیچ
    //    ترمیمی اجرا نمی‌شود. مسیرِ بکاپ در گزارش می‌آید تا مدیر بداند اگر
    //    نتیجه را نپسندید از کجا برگردد.
    //
    // ۳. «همه یا هیچ». تمام ترمیم‌های انتخاب‌شده در *یک* تراکنش اجرا می‌شوند.
    //    شکستِ یکی یعنی برگشتِ همه — هرگز حالتِ نیمه‌ترمیم‌شده باقی نمی‌ماند.
    //
    // ۴. «شمارشِ پیش از اجرا». هر ترمیم اول می‌گوید روی چند ردیف اثر می‌گذارد.
    //    مدیر عدد را می‌بیند و بعد تصمیم می‌گیرد. ترمیمی که صفر ردیف دارد
    //    اصلاً اجرا نمی‌شود.
    //
    // ۵. «مدارا با اسکیما». هر ترمیم پیش‌نیازهای خودش (جدول/ستون) را اعلام
    //    می‌کند؛ در نبودشان «در دسترس نیست» گزارش می‌شود، نه استثنا. این
    //    ماژول باید روی پایگاه‌داده‌ی قدیمی یا خراب هم باز شود — همان‌جایی که
    //    بیشترین نیاز به آن هست.
    // ═════════════════════════════════════════════════════════════════════════
    internal static class DevCenterRepair
    {
        private static readonly DatabaseHelper Db = new DatabaseHelper();

        // همان عبارتِ تولید شناسه‌ای که OfflineSyncInitializer استفاده می‌کند.
        // ⚠ عمداً کپی‌برداریِ متنی نیست بلکه همان قالب است: اگر دو جا دو قالبِ
        // متفاوت تولید کنند، شناسه‌های یک پایگاه‌داده دو شکل می‌شوند و تطبیقِ
        // رشته‌ای در همگام‌سازی می‌شکند.
        private const string NewUuidSql =
            "lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' || " +
            "lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || " +
            "lower(hex(randomblob(6)))";

        // ═════════════════════════════════════════════════════════════════════
        // تعریف یک ترمیم
        // ═════════════════════════════════════════════════════════════════════
        internal sealed class RepairAction
        {
            public string Key = "";              // شناسه‌ی پایدار (در گزارش و انتخاب)
            public string Title = "";            // آنچه مدیر می‌بیند
            public string Explanation = "";      // چه چیزی را و چرا درست می‌کند
            public string[] Requires;            // «Table» یا «Table.Column»

            // شمارشِ ردیف‌های نیازمندِ ترمیم. هرگز چیزی نمی‌نویسد.
            public Func<int> Count;

            // اجرای ترمیم داخلِ تراکنشِ داده‌شده. تعداد ردیف‌های تغییرکرده را
            // برمی‌گرداند. هیچ ترمیمی نباید خودش تراکنش باز کند.
            public Func<SQLiteConnection, SQLiteTransaction, int> Apply;
        }

        // وضعیت‌های ستون «وضعیت» در جدولِ نتیجه.
        public const string StateClean       = "سالم — کاری لازم نیست";
        public const string StateNeedsRepair = "قابل ترمیم";
        public const string StateUnavailable = DevCenterService.NotAvailable;
        public const string StateFailed      = "خطا";

        // ═════════════════════════════════════════════════════════════════════
        // فهرست ترمیم‌ها
        // ═════════════════════════════════════════════════════════════════════
        internal static List<RepairAction> All()
        {
            var list = new List<RepairAction>();

            // ── ۱) هویت سراسریِ گمشده ────────────────────────────────────────
            // بدون GlobalID، رکورد در همگام‌سازی و در ادغامِ بکاپ نامرئی است.
            // تریگرِ OfflineSyncInitializer از این پس جلویش را می‌گیرد، ولی
            // رکوردهایی که پیش از آن ساخته شده‌اند همچنان بی‌هویت‌اند.
            foreach (string[] table in Sync.OfflineSyncInitializer.SyncedTables)
            {
                string name = table[0];
                list.Add(new RepairAction
                {
                    Key = "globalid_" + name,
                    Title = "هویت سراسریِ گمشده — " + name,
                    Explanation =
                        "رکوردهایی که ستون GlobalID آن‌ها خالی است شناسه‌ی یکتا می‌گیرند. " +
                        "بدون این شناسه، رکورد در همگام‌سازی با سرور و در ادغامِ بکاپ " +
                        "دیده نمی‌شود. هیچ داده‌ای بازنویسی نمی‌شود؛ فقط خالی‌ها پر می‌شوند.",
                    Requires = new[] { name + ".GlobalID" },
                    Count = () => Scalar(
                        "SELECT COUNT(1) FROM [" + name + "] WHERE NULLIF(GlobalID,'') IS NULL;"),
                    Apply = (con, tr) => Exec(con, tr,
                        "UPDATE [" + name + "] SET GlobalID = " + NewUuidSql +
                        " WHERE NULLIF(GlobalID,'') IS NULL;")
                });
            }

            // ── ۲) شماره‌ی نسخه‌ی نامعتبر ────────────────────────────────────
            // RowVersion پایه‌ی تشخیصِ تعارض است. مقدارِ خالی یا صفر باعث
            // می‌شود مقایسه‌ی نسخه‌ها بی‌معنا شود و تغییرِ سرور بی‌دلیل
            // «قدیمی» یا «جدید» به‌نظر برسد.
            foreach (string[] table in Sync.OfflineSyncInitializer.SyncedTables)
            {
                string name = table[0];
                list.Add(new RepairAction
                {
                    Key = "rowversion_" + name,
                    Title = "شماره‌ی نسخه‌ی نامعتبر — " + name,
                    Explanation =
                        "RowVersion خالی یا کوچک‌تر از ۱ به ۱ برگردانده می‌شود. " +
                        "این شماره مبنای تشخیصِ «این رکورد از آخرین همگام‌سازی عوض شده» " +
                        "است؛ مقدارِ نامعتبر یعنی تشخیصِ تعارضِ نادرست.",
                    Requires = new[] { name + ".RowVersion" },
                    Count = () => Scalar(
                        "SELECT COUNT(1) FROM [" + name + "] WHERE IFNULL(RowVersion, 0) < 1;"),
                    Apply = (con, tr) => Exec(con, tr,
                        "UPDATE [" + name + "] SET RowVersion = 1 WHERE IFNULL(RowVersion, 0) < 1;")
                });
            }

            // ── ۳) فاصله‌ی اضافی در کدِ اختصاصی و شماره‌ی فرم ────────────────
            //
            // آموزش — چرا این مهم‌ترین ترمیمِ این فهرست است: تطبیقِ عکس و سند،
            // تطبیقِ همگام‌سازیِ HTML و تولیدِ بارکد، همگی مقایسه‌ی *دقیقِ
            // رشته‌ای* روی Code انجام می‌دهند. یک فاصله‌ی نامرئی در ابتدا یا
            // انتهای کد باعث می‌شود پرونده «پیدا نشود» بدون هیچ پیام خطایی —
            // و کاربر ساعت‌ها دنبال فایلِ گمشده بگردد در حالی که مشکل یک
            // کاراکترِ فاصله است.
            list.Add(new RepairAction
            {
                Key = "trim_code",
                Title = "فاصله‌ی اضافی در «کد اختصاصی»",
                Explanation =
                    "فاصله‌های ابتدا و انتهای کد حذف می‌شوند. تطبیقِ عکس، سند، بارکد و " +
                    "همگام‌سازی همگی مقایسه‌ی دقیقِ رشته‌ای انجام می‌دهند، پس یک فاصله‌ی " +
                    "نامرئی پرونده را «پیدا نشده» می‌کند بدون هیچ پیام خطایی. " +
                    "خودِ کد تغییر نمی‌کند — فقط فاصله‌های دور آن.",
                Requires = new[] { "TblCase.Code" },
                Count = () => Scalar(
                    "SELECT COUNT(1) FROM TblCase WHERE Code IS NOT NULL AND Code <> TRIM(Code);"),
                Apply = (con, tr) => Exec(con, tr,
                    "UPDATE TblCase SET Code = TRIM(Code) WHERE Code IS NOT NULL AND Code <> TRIM(Code);")
            });

            list.Add(new RepairAction
            {
                Key = "trim_formno",
                Title = "فاصله‌ی اضافی در «شماره فرم»",
                Explanation =
                    "فاصله‌های ابتدا و انتهای شماره فرم حذف می‌شوند. شماره فرم در خروجیِ " +
                    "جمعی و در نام‌گذاری فایل‌ها استفاده می‌شود و فاصله‌ی اضافی همان‌جا " +
                    "دردسر می‌سازد.",
                Requires = new[] { "TblCase.FormNo" },
                Count = () => Scalar(
                    "SELECT COUNT(1) FROM TblCase WHERE FormNo IS NOT NULL AND FormNo <> TRIM(FormNo);"),
                Apply = (con, tr) => Exec(con, tr,
                    "UPDATE TblCase SET FormNo = TRIM(FormNo) WHERE FormNo IS NOT NULL AND FormNo <> TRIM(FormNo);")
            });

            // ── ۴) ناسازگاریِ بایگانی ───────────────────────────────────────
            // دو حالتِ ناممکن که «دکتر» از قبل تشخیص می‌داد ولی درمانی نداشت.
            list.Add(new RepairAction
            {
                Key = "archive_missing_meta",
                Title = "پرونده‌ی بایگانی‌شده بدون تاریخ/کاربرِ بایگانی",
                Explanation =
                    "پرونده‌هایی که «بایگانی» علامت خورده‌اند ولی تاریخ یا کاربرِ بایگانی " +
                    "ندارند تکمیل می‌شوند (تاریخ از آخرین به‌روزرسانی، و کاربرِ «نامشخص»). " +
                    "وضعیتِ بایگانیِ خودِ پرونده عوض نمی‌شود.",
                Requires = new[] { "TblCase.IsArchived", "TblCase.ArchivedAt", "TblCase.ArchivedBy" },
                Count = () => Scalar(
                    "SELECT COUNT(1) FROM TblCase WHERE IsArchived = 1 " +
                    "AND (NULLIF(ArchivedAt,'') IS NULL OR NULLIF(ArchivedBy,'') IS NULL);"),
                Apply = (con, tr) => Exec(con, tr,
                    "UPDATE TblCase SET " +
                    "  ArchivedAt = IFNULL(NULLIF(ArchivedAt,''), IFNULL(NULLIF(UpdatedAt,''), datetime('now'))), " +
                    "  ArchivedBy = IFNULL(NULLIF(ArchivedBy,''), 'نامشخص') " +
                    "WHERE IsArchived = 1 " +
                    "AND (NULLIF(ArchivedAt,'') IS NULL OR NULLIF(ArchivedBy,'') IS NULL);")
            });

            list.Add(new RepairAction
            {
                Key = "archive_stale_meta",
                Title = "پرونده‌ی غیربایگانی با تاریخِ بایگانیِ باقی‌مانده",
                Explanation =
                    "پرونده‌ای که از بایگانی درآمده ولی تاریخ و کاربرِ بایگانیِ قدیمی‌اش " +
                    "پاک نشده، این دو فیلد خالی می‌شوند. گزارش‌های بایگانی به همین دو " +
                    "فیلد نگاه می‌کنند و ماندنشان آمار را غلط می‌کند.",
                Requires = new[] { "TblCase.IsArchived", "TblCase.ArchivedAt" },
                Count = () => Scalar(
                    "SELECT COUNT(1) FROM TblCase WHERE IFNULL(IsArchived,0) = 0 " +
                    "AND NULLIF(ArchivedAt,'') IS NOT NULL;"),
                Apply = (con, tr) => Exec(con, tr,
                    "UPDATE TblCase SET ArchivedAt = NULL, ArchivedBy = NULL " +
                    "WHERE IFNULL(IsArchived,0) = 0 AND NULLIF(ArchivedAt,'') IS NOT NULL;")
            });

            // ── ۵) صفِ ارسالِ همگام‌سازی: تلاشِ دوباره ────────────────────────
            //
            // آموزش — چرا این ترمیم برای مدیر حیاتی است: وقتی سرور مدتی خطا
            // می‌دهد (یا آدرسش اشتباه بوده)، ردیف‌ها «ناموفق» می‌شوند. طبق
            // طراحیِ SyncService این ردیف‌ها در صف می‌مانند و دوباره تلاش
            // می‌شوند — ولی شمارنده‌ی تلاش و متنِ خطای قدیمی رویشان می‌ماند و
            // گزارشِ سلامت را برای همیشه قرمز نگه می‌دارد، حتی بعد از رفعِ
            // مشکل. این ترمیم آن‌ها را به «در انتظار» برمی‌گرداند و شمارنده را
            // صفر می‌کند: یعنی «از نو، با صفحه‌ی پاک».
            //
            // ⚠ هیچ ردیفی حذف نمی‌شود و هیچ باری تغییر نمی‌کند — فقط وضعیت.
            list.Add(new RepairAction
            {
                Key = "outbox_retry_failed",
                Title = "صفِ ارسال — تلاشِ دوباره برای ردیف‌های ناموفق",
                Explanation =
                    "ردیف‌های «ناموفق» صفِ ارسال به «در انتظار» برمی‌گردند و شمارنده‌ی " +
                    "تلاش و خطای قبلی پاک می‌شود. پس از رفعِ مشکلِ شبکه یا آدرسِ سرور، " +
                    "این کار باعث می‌شود در همگام‌سازیِ بعدی از نو تلاش شوند و امتیازِ " +
                    "سلامت هم واقعی شود. هیچ ردیفی حذف و هیچ داده‌ای عوض نمی‌شود.",
                Requires = new[] { "SyncOutbox" },
                Count = () => Scalar(
                    "SELECT COUNT(1) FROM SyncOutbox WHERE State = @S;",
                    new SQLiteParameter("@S", Sync.OfflineSyncInitializer.StateFailed)),
                Apply = (con, tr) => Exec(con, tr,
                    "UPDATE SyncOutbox SET State = @P, Attempts = 0, LastError = NULL " +
                    "WHERE State = @S;",
                    new SQLiteParameter("@P", Sync.OfflineSyncInitializer.StatePending),
                    new SQLiteParameter("@S", Sync.OfflineSyncInitializer.StateFailed))
            });

            // ── ۶) صفِ ارسال: ردیف‌های ابدی ─────────────────────────────────
            //
            // ردیفی که GlobalID ندارد در سمت مقابل بی‌معناست و *هرگز* قابل
            // پذیرش نیست؛ تا ابد در صف می‌ماند و هر بار یک تلاشِ ناموفق ثبت
            // می‌کند. کنار گذاشته می‌شود — نه حذف: ردّش برای بازرسی می‌ماند،
            // دقیقاً مثل کاری که SyncOutboxService.Discard می‌کند.
            list.Add(new RepairAction
            {
                Key = "outbox_unsendable",
                Title = "صفِ ارسال — ردیف‌های بدون هویت (هرگز ارسال‌شدنی نیستند)",
                Explanation =
                    "ردیف‌هایی که EntityGlobalID ندارند در شعبه‌ی مقابل قابل شناسایی " +
                    "نیستند و هیچ‌وقت پذیرفته نمی‌شوند؛ فقط صف را شلوغ و آمار را خراب " +
                    "می‌کنند. این‌ها «کنار گذاشته شد» علامت می‌خورند — حذف نمی‌شوند تا " +
                    "ردّشان برای بازرسی بماند. ابتدا ترمیمِ «هویت سراسریِ گمشده» را اجرا " +
                    "کنید تا فقط ردیف‌هایی که واقعاً چاره‌ای ندارند بمانند.",
                Requires = new[] { "SyncOutbox" },
                Count = () => Scalar(
                    "SELECT COUNT(1) FROM SyncOutbox WHERE NULLIF(EntityGlobalID,'') IS NULL " +
                    "AND State IN (@P, @F);",
                    new SQLiteParameter("@P", Sync.OfflineSyncInitializer.StatePending),
                    new SQLiteParameter("@F", Sync.OfflineSyncInitializer.StateFailed)),
                Apply = (con, tr) => Exec(con, tr,
                    "UPDATE SyncOutbox SET State = @D, LastError = @E, LastAttemptAt = datetime('now') " +
                    "WHERE NULLIF(EntityGlobalID,'') IS NULL AND State IN (@P, @F);",
                    new SQLiteParameter("@D", Sync.OfflineSyncInitializer.StateDiscarded),
                    new SQLiteParameter("@E", "کنار گذاشته شد توسط ترمیم خودکار: بدون هویت سراسری"),
                    new SQLiteParameter("@P", Sync.OfflineSyncInitializer.StatePending),
                    new SQLiteParameter("@F", Sync.OfflineSyncInitializer.StateFailed))
            });

            // ── ۷) صفِ دریافتِ فایل: تلاشِ دوباره ────────────────────────────
            // فایلی که وسطِ دریافت قطع شده یا هشش نخوانده، در حالتِ ناموفق
            // می‌ماند. برگرداندنش به «در انتظار دریافت» یعنی اجرای بعدی دوباره
            // تلاش می‌کند — و چون دریافت با هش بررسی می‌شود، فایلِ خراب
            // جایگزین می‌گردد.
            list.Add(new RepairAction
            {
                Key = "download_retry",
                Title = "صفِ دریافتِ فایل — تلاشِ دوباره برای ناموفق‌ها و خراب‌ها",
                Explanation =
                    "فایل‌هایی که دریافتشان ناموفق بوده یا هشِ محتوایشان نخوانده، به " +
                    "«در انتظار دریافت» برمی‌گردند تا در همگام‌سازیِ بعدی از نو گرفته " +
                    "شوند. چون درستیِ محتوا با هش بررسی می‌شود، فایلِ خراب با نسخه‌ی " +
                    "سالم جایگزین می‌گردد.",
                Requires = new[] { "SyncFileDownload" },
                Count = () => Scalar(
                    "SELECT COUNT(1) FROM SyncFileDownload WHERE State IN (@F, @C);",
                    new SQLiteParameter("@F", Sync.OfflineSyncInitializer.DownloadFailed),
                    new SQLiteParameter("@C", Sync.OfflineSyncInitializer.DownloadCorrupt)),
                Apply = (con, tr) => Exec(con, tr,
                    "UPDATE SyncFileDownload SET State = @P, Attempts = 0, LastError = NULL " +
                    "WHERE State IN (@F, @C);",
                    new SQLiteParameter("@P", Sync.OfflineSyncInitializer.DownloadPending),
                    new SQLiteParameter("@F", Sync.OfflineSyncInitializer.DownloadFailed),
                    new SQLiteParameter("@C", Sync.OfflineSyncInitializer.DownloadCorrupt))
            });

            // ── ۸) صداقتِ وضعیتِ فایل‌های محلی ──────────────────────────────
            //
            // ⚠ این ترمیم *دیسک را نگاه می‌کند*، پس شمارشش کند است. ولی
            // ارزشش را دارد: SyncFile ادعا می‌کند فایلی آماده‌ی ارسال است در
            // حالی که روی دیسک نیست. هر بار همگام‌سازی یک شکستِ بی‌فایده
            // ثبت می‌شود. علامتِ صریحِ «فایل روی دیسک نیست» هم آمار را صادق
            // می‌کند و هم به مدیر می‌گوید کدام فایل‌ها را باید دوباره پیوست کند.
            list.Add(new RepairAction
            {
                Key = "syncfile_missing",
                Title = "فایل‌های ثبت‌شده‌ای که روی دیسک نیستند",
                Explanation =
                    "ردیف‌هایی از فهرستِ فایل‌های همگام‌سازی که فایلشان دیگر روی دیسک " +
                    "وجود ندارد، صریحاً «فایل روی دیسک نیست» علامت می‌خورند. بدون این، " +
                    "هر اجرای همگام‌سازی یک شکستِ بی‌فایده ثبت می‌کند. " +
                    "⚠ این بررسی به دیسک مراجعه می‌کند و ممکن است چند ثانیه طول بکشد.",
                Requires = new[] { "SyncFile.LocalPath" },
                Count = () => MissingLocalFileIds().Count,
                Apply = MarkMissingLocalFiles
            });

            // ── ۹) هویت سراسریِ *تکراری* ────────────────────────────────────
            //
            // آموزش — چرا این جدی‌ترین ایرادِ ممکن برای همگام‌سازی است:
            // کلِ موتورِ همگام‌سازی روی این فرض بنا شده که «GlobalID یکتاست».
            // SyncApplier رکوردِ محلی را با
            // «SELECT * FROM T WHERE GlobalID = @G LIMIT 1» پیدا می‌کند — یعنی
            // اگر دو رکورد یک هویت داشته باشند، همیشه فقط *یکی* از آن‌ها دیده
            // می‌شود. نتیجه: تغییراتِ سرور تا ابد روی رکوردِ اول می‌نشیند و
            // رکوردِ دوم هرگز به‌روز نمی‌شود، و از آن بدتر، تغییراتِ رکوردِ دوم
            // با هویتِ رکوردِ اول ارسال می‌شود و در شعبهٔ مقابل داده‌ی درست را
            // بازنویسی می‌کند. این ایراد بی‌صداست و هیچ خطایی تولید نمی‌کند.
            //
            // چطور به‌وجود می‌آید: ادغامِ دو بکاپ، کپیِ دستیِ فایلِ دیتابیس بین
            // دو شعبه، یا بازیابیِ یک بکاپ روی پایگاه‌دادهٔ پرشده.
            //
            // ⚠ درمان محافظه‌کارانه است: *قدیمی‌ترین* رکورد (کوچک‌ترین کلید)
            // هویتِ اصلی را نگه می‌دارد — چون همان است که احتمالاً قبلاً با
            // سرور تبادل شده — و فقط تکراری‌های بعدی هویتِ تازه می‌گیرند.
            // هیچ رکوردی حذف نمی‌شود و هیچ دادهٔ کسب‌وکاری تغییر نمی‌کند.
            foreach (string[] table in Sync.OfflineSyncInitializer.SyncedTables)
            {
                string name = table[0];
                string key  = table[1];

                // ردیف‌هایی که هویتشان با ردیفِ قدیمی‌تری مشترک است.
                string duplicateFilter =
                    " FROM [" + name + "] WHERE NULLIF(GlobalID,'') IS NOT NULL AND [" + key + "] NOT IN " +
                    "(SELECT MIN([" + key + "]) FROM [" + name + "] " +
                    " WHERE NULLIF(GlobalID,'') IS NOT NULL GROUP BY GlobalID)";

                list.Add(new RepairAction
                {
                    Key = "dupglobalid_" + name,
                    Title = "هویت سراسریِ تکراری — " + name,
                    Explanation =
                        "دو یا چند رکورد شناسه‌ی سراسریِ یکسان دارند. همگام‌سازی همیشه فقط " +
                        "اولی را می‌بیند، پس بقیه هرگز به‌روز نمی‌شوند و تغییراتشان با هویتِ " +
                        "رکوردِ اول ارسال می‌شود و در شعبه‌ی مقابل داده‌ی درست را خراب می‌کند. " +
                        "این ایراد هیچ پیام خطایی تولید نمی‌کند. " +
                        "درمان: قدیمی‌ترین رکورد هویتش را نگه می‌دارد و تکراری‌های بعدی شناسه‌ی " +
                        "تازه می‌گیرند. هیچ رکوردی حذف و هیچ داده‌ای بازنویسی نمی‌شود.",
                    Requires = new[] { name + ".GlobalID" },
                    Count = () => Scalar("SELECT COUNT(1)" + duplicateFilter + ";"),
                    Apply = (con, tr) => Exec(con, tr,
                        "UPDATE [" + name + "] SET GlobalID = " + NewUuidSql +
                        " WHERE [" + key + "] IN (SELECT [" + key + "]" + duplicateFilter + ");")
                });
            }

            // ── ۱۰) مرکزِ نامشخص روی پرونده ─────────────────────────────────
            //
            // پرونده‌ای که CenterID ندارد از فیلترِ مرکز رد نمی‌شود، پس در
            // فهرست‌ها و گزارش‌های هر مرکز نامرئی است — ولی در پایگاه‌داده هست
            // و در آمارِ کلی شمرده می‌شود. اختلافِ همیشگیِ «تعداد پرونده‌ها با
            // جمعِ مراکز نمی‌خواند» معمولاً از همین‌جاست.
            list.Add(new RepairAction
            {
                Key = "case_missing_center",
                Title = "پرونده‌ی بدون مرکز",
                Explanation =
                    "پرونده‌هایی که CenterID خالی یا صفر دارند به کوچک‌ترین مرکزِ تعریف‌شده " +
                    "نسبت داده می‌شوند. چنین پرونده‌ای از فیلترِ مرکز رد نمی‌شود و عملاً در " +
                    "هیچ فهرست یا گزارشی دیده نمی‌شود، در حالی که در آمارِ کلی شمرده می‌شود. " +
                    "⚠ اگر بیش از یک مرکز دارید، پس از ترمیم بررسی کنید که این پرونده‌ها " +
                    "واقعاً به همان مرکز تعلق دارند.",
                Requires = new[] { "TblCase.CenterID", "TblCenter" },
                Count = () => Scalar("SELECT COUNT(1) FROM TblCase WHERE IFNULL(CenterID,0) < 1;"),
                Apply = (con, tr) => Exec(con, tr,
                    "UPDATE TblCase SET CenterID = IFNULL((SELECT MIN(CenterID) FROM TblCenter), 1) " +
                    "WHERE IFNULL(CenterID,0) < 1;")
            });

            // ── ۱۱) تاریخِ ثبتِ گمشده ───────────────────────────────────────
            // گزارش‌های دوره‌ای و مرتب‌سازیِ «تازه‌ترین» به CreatedAt تکیه
            // می‌کنند؛ رکوردِ بدون تاریخ از بازه‌ها بیرون می‌افتد.
            list.Add(new RepairAction
            {
                Key = "case_missing_createdat",
                Title = "پرونده‌ی بدون تاریخِ ثبت",
                Explanation =
                    "پرونده‌هایی که CreatedAt خالی دارند، تاریخشان از آخرین به‌روزرسانی " +
                    "(و در نبودِ آن، زمانِ اکنون) پر می‌شود. گزارش‌های دوره‌ای و مرتب‌سازیِ " +
                    "«تازه‌ترین پرونده‌ها» به این ستون نگاه می‌کنند، پس رکوردِ بدون تاریخ از " +
                    "همه‌ی بازه‌های زمانی بیرون می‌افتد.",
                Requires = new[] { "TblCase.CreatedAt" },
                Count = () => Scalar("SELECT COUNT(1) FROM TblCase WHERE NULLIF(CreatedAt,'') IS NULL;"),
                Apply = (con, tr) => Exec(con, tr,
                    "UPDATE TblCase SET CreatedAt = " +
                    "IFNULL(NULLIF(UpdatedAt,''), datetime('now')) " +
                    "WHERE NULLIF(CreatedAt,'') IS NULL;")
            });

            // ── ۱۲) تعارضِ بازِ بی‌صاحب ─────────────────────────────────────
            //
            // تعارضی که رکوردش دیگر وجود ندارد هرگز قابلِ حل نیست: هر چهار
            // مسیرِ SyncConflictResolver اول رکوردِ محلی را پیدا می‌کنند و
            // با «رکورد محلی پیدا نشد» شکست می‌خورند. چنین ردیفی برای همیشه
            // در فهرستِ «تعارضِ باز» می‌ماند و امتیازِ سلامت را قرمز نگه
            // می‌دارد. بسته می‌شود — نه حذف: هر دو نسخه‌ی داده برای بازرسی
            // سرِ جایشان می‌مانند.
            foreach (string[] table in Sync.OfflineSyncInitializer.SyncedTables)
            {
                string name = table[0];

                string orphanFilter =
                    " FROM SyncConflict WHERE Status = @O AND EntityName = @N " +
                    "AND NULLIF(EntityGlobalID,'') IS NOT NULL " +
                    "AND NOT EXISTS (SELECT 1 FROM [" + name + "] t " +
                    "                WHERE t.GlobalID = SyncConflict.EntityGlobalID)";

                list.Add(new RepairAction
                {
                    Key = "conflict_orphan_" + name,
                    Title = "تعارضِ بازِ بی‌صاحب — " + name,
                    Explanation =
                        "تعارض‌هایی که رکوردِ مربوطشان دیگر در پایگاه‌داده نیست، «حل شد» " +
                        "علامت می‌خورند. چنین تعارضی با هیچ‌کدام از دکمه‌های «پذیرش محلی/سرور/" +
                        "ادغام» قابل حل نیست (همه با خطای «رکورد محلی پیدا نشد» شکست می‌خورند) " +
                        "و تا ابد در فهرستِ تعارض‌های باز می‌ماند و امتیازِ سلامت را قرمز نگه " +
                        "می‌دارد. هیچ ردیفی حذف نمی‌شود — هر دو نسخه‌ی داده برای بازرسی می‌ماند.",
                    Requires = new[] { "SyncConflict", name + ".GlobalID" },
                    Count = () => Scalar(
                        "SELECT COUNT(1)" + orphanFilter + ";",
                        new SQLiteParameter("@O", Sync.OfflineSyncInitializer.ConflictOpen),
                        new SQLiteParameter("@N", name)),
                    Apply = (con, tr) => Exec(con, tr,
                        "UPDATE SyncConflict SET Status = @R, Resolution = @Res, " +
                        "ResolvedBy = @By, ResolvedAt = datetime('now') " +
                        "WHERE ConflictID IN (SELECT ConflictID" + orphanFilter + ");",
                        new SQLiteParameter("@R", Sync.OfflineSyncInitializer.ConflictResolved),
                        new SQLiteParameter("@Res", "بسته شد توسط ترمیم خودکار: رکورد دیگر وجود ندارد"),
                        new SQLiteParameter("@By", "ترمیم خودکار"),
                        new SQLiteParameter("@O", Sync.OfflineSyncInitializer.ConflictOpen),
                        new SQLiteParameter("@N", name))
                });
            }

            return list;
        }

        // ═════════════════════════════════════════════════════════════════════
        // بررسی (بدون هیچ تغییری)
        // ═════════════════════════════════════════════════════════════════════
        internal sealed class ScanResult
        {
            public DataTable Rows;
            public int Repairable;      // تعداد ترمیم‌هایی که کاری برای انجام دارند
            public int AffectedRows;    // مجموع ردیف‌های قابل ترمیم
            public bool Cancelled;
        }

        internal static ScanResult Scan(IProgress<DevCenterService.DevProgress> progress,
                                        System.Threading.CancellationToken cancel)
        {
            // اسکیما ممکن است از آخرین اجرا عوض شده باشد (مهاجرت/بازیابیِ بکاپ).
            DevCenterService.ResetSchemaCache();

            var table = new DataTable();
            table.Columns.Add("انتخاب", typeof(bool));
            table.Columns.Add("ترمیم", typeof(string));
            table.Columns.Add("تعداد", typeof(int));
            table.Columns.Add("وضعیت", typeof(string));
            table.Columns.Add("توضیح", typeof(string));
            table.Columns.Add("کلید", typeof(string));

            var result = new ScanResult { Rows = table };
            List<RepairAction> actions = All();
            int step = 0;

            foreach (RepairAction action in actions)
            {
                if (cancel.IsCancellationRequested) { result.Cancelled = true; break; }

                step++;
                if (progress != null)
                    progress.Report(new DevCenterService.DevProgress(
                        step, actions.Count, action.Title));

                string missing = FirstMissing(action.Requires);
                if (missing != null)
                {
                    table.Rows.Add(false, action.Title, 0,
                        StateUnavailable + " — " + missing, action.Explanation, action.Key);
                    continue;
                }

                int count;
                try { count = action.Count(); }
                catch (Exception ex)
                {
                    table.Rows.Add(false, action.Title, 0,
                        StateFailed + ": " + ex.Message, action.Explanation, action.Key);
                    continue;
                }

                if (count <= 0)
                {
                    table.Rows.Add(false, action.Title, 0, StateClean, action.Explanation, action.Key);
                    continue;
                }

                // ⚠ ردیف‌های قابل ترمیم از پیش تیک نمی‌خورند. انتخاب باید
                // تصمیمِ آگاهانه‌ی مدیر باشد، نه پیش‌فرضی که با یک کلیک
                // ناخواسته اجرا شود.
                table.Rows.Add(false, action.Title, count, StateNeedsRepair,
                    action.Explanation, action.Key);

                result.Repairable++;
                result.AffectedRows += count;
            }

            return result;
        }

        // ═════════════════════════════════════════════════════════════════════
        // اجرا
        //
        // ⚠ ترتیبِ عملیات عمدی است و نباید عوض شود:
        //   ۱. بکاپ (اگر شکست خورد، هیچ چیز اجرا نمی‌شود)
        //   ۲. یک تراکنش برای همه‌ی ترمیم‌ها
        //   ۳. ثبت در لاگ امنیتی و ممیزی — *پس از* موفقیت
        // ═════════════════════════════════════════════════════════════════════
        internal static string ApplySelected(IList<string> keys,
                                             IProgress<DevCenterService.DevProgress> progress,
                                             System.Threading.CancellationToken cancel)
        {
            if (keys == null || keys.Count == 0)
                return "هیچ ترمیمی انتخاب نشده است. ابتدا «بررسی» را اجرا کنید و ترمیم‌های " +
                       "موردنظر را تیک بزنید.";

            var selected = new List<RepairAction>();
            foreach (RepairAction action in All())
                if (keys.Contains(action.Key)) selected.Add(action);

            if (selected.Count == 0)
                return "ترمیم‌های انتخاب‌شده پیدا نشدند.";

            var log = new StringBuilder();

            // ── گام ۱: بکاپ ──
            if (progress != null)
                progress.Report(new DevCenterService.DevProgress(0, selected.Count + 1,
                    "بکاپ کامل پیش از ترمیم"));

            string backupPath;
            try
            {
                backupPath = new BackupHelper().ExportBackup(ResolveBackupFolder());
            }
            catch (Exception ex)
            {
                // ⚠ بدون بکاپ، هیچ ترمیمی اجرا نمی‌شود. ایمنیِ داده بر
                // راحتیِ کاربر مقدم است.
                return "بکاپ‌گیری پیش از ترمیم ناموفق بود؛ هیچ تغییری اعمال نشد." +
                       Environment.NewLine + "علت: " + ex.Message;
            }

            log.AppendLine("بکاپ کامل گرفته شد: " + backupPath);
            log.AppendLine();

            // ── گام ۲: همه‌ی ترمیم‌ها در یک تراکنش ──
            var applied = new List<string>();
            int totalRows = 0;

            try
            {
                Db.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
                {
                    int step = 0;
                    foreach (RepairAction action in selected)
                    {
                        cancel.ThrowIfCancellationRequested();

                        step++;
                        if (progress != null)
                            progress.Report(new DevCenterService.DevProgress(
                                step, selected.Count + 1, action.Title));

                        int changed = action.Apply(con, tr);
                        totalRows += changed;
                        applied.Add(action.Title + " — " + changed.ToString("N0") + " ردیف");
                    }
                });
            }
            catch (OperationCanceledException)
            {
                return "ترمیم لغو شد؛ هیچ تغییری اعمال نشد (تراکنش برگردانده شد)." +
                       Environment.NewLine + "بکاپ در این مسیر باقی است: " + backupPath;
            }
            catch (Exception ex)
            {
                try { Enterprise.ErrorLogger.Log(ex, "DevCenterRepair.ApplySelected"); } catch { }
                return "ترمیم با خطا مواجه شد؛ *هیچ* تغییری اعمال نشد (تراکنش برگردانده شد)." +
                       Environment.NewLine + "علت: " + ex.Message +
                       Environment.NewLine + "بکاپ در این مسیر باقی است: " + backupPath;
            }

            foreach (string line in applied) log.AppendLine("✔ " + line);

            log.AppendLine();
            log.Append("مجموع: ").Append(totalRows.ToString("N0"))
               .Append(" ردیف در ").Append(applied.Count).Append(" ترمیم اصلاح شد.");

            // ── گام ۳: ردّ ممیزی ──
            // کشِ اسکیما پس از تغییر باید تازه شود، وگرنه بررسیِ بعدی روی
            // تصویرِ کهنه اجرا می‌شود.
            DevCenterService.ResetSchemaCache();

            string summary = string.Join(" | ", applied.ToArray());
            DevCenterService.LogAction("ترمیم خودکار — " + summary);

            try
            {
                AuditLogger.Log("ترمیم خودکار (مرکز کنترل)", "TblCase", 0, "",
                    "بکاپ: " + backupPath + " — " + summary);
            }
            catch { }

            return log.ToString();
        }

        // ═════════════════════════════════════════════════════════════════════
        // ترمیمِ «فایلِ روی دیسک نیست» — تنها ترمیمی که به بیرونِ دیتابیس نگاه
        // می‌کند، پس شمارش و اجرایش جدا نوشته شده‌اند.
        // ═════════════════════════════════════════════════════════════════════
        private static List<long> MissingLocalFileIds()
        {
            var ids = new List<long>();

            DataTable rows = Db.Query(
                "SELECT FileID, LocalPath FROM SyncFile " +
                "WHERE NULLIF(LocalPath,'') IS NOT NULL AND IFNULL(UploadState,'') <> @M;",
                new SQLiteParameter("@M", Sync.OfflineSyncInitializer.FileMissing));

            foreach (DataRow row in rows.Rows)
            {
                string path = Convert.ToString(row["LocalPath"]);
                bool exists;
                try { exists = File.Exists(path); }
                catch { exists = false; }   // مسیرِ نامعتبر = غیرقابل دسترسی

                if (!exists) ids.Add(Convert.ToInt64(row["FileID"]));
            }

            return ids;
        }

        private static int MarkMissingLocalFiles(SQLiteConnection con, SQLiteTransaction tr)
        {
            List<long> ids = MissingLocalFileIds();
            int changed = 0;

            foreach (long id in ids)
            {
                changed += Exec(con, tr,
                    "UPDATE SyncFile SET UploadState = @M, LastError = @E, " +
                    "LastAttemptAt = datetime('now') WHERE FileID = @Id;",
                    new SQLiteParameter("@M", Sync.OfflineSyncInitializer.FileMissing),
                    new SQLiteParameter("@E", "علامت‌گذاری توسط ترمیم خودکار: فایل روی دیسک پیدا نشد"),
                    new SQLiteParameter("@Id", id));
            }

            return changed;
        }

        // ═════════════════════════════════════════════════════════════════════
        // کمکی‌ها
        // ═════════════════════════════════════════════════════════════════════
        private static int Scalar(string sql, params SQLiteParameter[] parameters)
        {
            object value = Db.ExecuteScalar(sql, parameters);
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static int Exec(SQLiteConnection con, SQLiteTransaction tr,
                                string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = new SQLiteCommand(sql, con, tr))
            {
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteNonQuery();
            }
        }

        // همان قاعده‌ی DevCenterService: «Table» یا «Table.Column».
        private static string FirstMissing(string[] requirements)
        {
            if (requirements == null) return null;

            foreach (string requirement in requirements)
            {
                if (string.IsNullOrWhiteSpace(requirement)) continue;

                int dot = requirement.IndexOf('.');
                if (dot < 0)
                {
                    if (!DevCenterService.TableExists(requirement)) return "جدول " + requirement;
                }
                else
                {
                    string table = requirement.Substring(0, dot);
                    string column = requirement.Substring(dot + 1);
                    if (!DevCenterService.TableExists(table)) return "جدول " + table;
                    if (!DevCenterService.ColumnExists(table, column)) return "ستون " + requirement;
                }
            }
            return null;
        }

        // همان منطقِ SyncEngine: مسیرِ تنظیم‌شده، وگرنه پوشه‌ی کنارِ برنامه.
        private static string ResolveBackupFolder()
        {
            string configured = SettingsHelper.Get(SettingsHelper.BackupPath);
            if (!string.IsNullOrWhiteSpace(configured)) return configured;

            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RepairBackups");
            Directory.CreateDirectory(baseDir);
            return baseDir;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.DevCenter
{
    // ═════════════════════════════════════════════════════════════════════════
    // موتور دادهٔ «مرکز کنترل توسعه‌دهنده».
    //
    // آموزش — قاعدهٔ اصلی این فایل: «بازاستفاده، نه بازنویسی».
    // هرجا سرویسی از قبل وجود دارد (DataQualityChecker، DuplicateDetector،
    // FileCleanupHelper، ErrorLogger، SecurityAudit، LockService،
    // ModuleService، PermissionService، LookupHelper، SettingsHelper) همان
    // صدا زده می‌شود. اینجا فقط چیزهایی نوشته شده که در پروژه معادل عمومیِ
    // قابل‌فراخوانی نداشتند (مثل شمارش رکوردهای یتیم یا اندازهٔ دیتابیس).
    //
    // این کلاس هیچ داده‌ای را تغییر نمی‌دهد مگر در بخش «نگهداری» که هر عملیات
    // آن از قبل توسط UI تأیید گرفته و در لاگ امنیتی ثبت می‌شود.
    // ═════════════════════════════════════════════════════════════════════════
    internal static class DevCenterService
    {
        private static readonly DatabaseHelper Db = new DatabaseHelper();

        // ═════════════════════════════════════════════════════════════════════
        // بازتابِ اسکیمای واقعیِ دیتابیس
        //
        // آموزش — درسی که از یک باگ واقعی گرفته شد: نامِ ستون «حدس» زده شده بود
        // (LockedByUserID) در حالی که ستونِ واقعیِ EntRecordLock نامش UserID
        // است. نتیجه: SQLiteException «no such column».
        //
        // درمانِ ریشه‌ای فقط اصلاحِ آن یک نام نیست؛ این است که هیچ بخشی از این
        // ماژول به «فرضِ» وجود جدول یا ستون تکیه نکند. این ماژول ابزارِ عیب‌یابی
        // است و باید دقیقاً روی پایگاه‌دادهٔ *خراب یا قدیمی* هم باز شود — یعنی
        // همان‌جایی که بیشترین احتمالِ نبودِ جدول/ستون هست. پس پیش از هر کوئری،
        // وجودِ جدول و ستون از خودِ sqlite_master و PRAGMA table_info پرسیده
        // می‌شود و در نبودشان به‌جای استثنا، «در دسترس نیست» گزارش می‌گردد.
        //
        // کش، کوئری‌های تکراری را حذف می‌کند و با ResetSchemaCache (پس از
        // عملیات نگهداری) تازه می‌شود.
        // ═════════════════════════════════════════════════════════════════════
        private static readonly object SchemaLock = new object();
        private static HashSet<string> _tableCache;
        private static readonly Dictionary<string, HashSet<string>> ColumnCache =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public static void ResetSchemaCache()
        {
            lock (SchemaLock)
            {
                _tableCache = null;
                ColumnCache.Clear();
            }
        }

        public static bool TableExists(string table)
        {
            if (string.IsNullOrWhiteSpace(table)) return false;

            lock (SchemaLock)
            {
                if (_tableCache == null)
                {
                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        DataTable t = Db.Query(
                            "SELECT name FROM sqlite_master WHERE type IN ('table','view');");
                        foreach (DataRow row in t.Rows) set.Add(Convert.ToString(row["name"]));
                    }
                    catch { /* دیتابیس در دسترس نیست — همه چیز «موجود نیست» */ }
                    _tableCache = set;
                }
                return _tableCache.Contains(table);
            }
        }

        public static bool ColumnExists(string table, string column)
        {
            if (!TableExists(table)) return false;

            lock (SchemaLock)
            {
                HashSet<string> columns;
                if (!ColumnCache.TryGetValue(table, out columns))
                {
                    columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        DataTable t = Db.Query("PRAGMA table_info([" + table + "]);");
                        foreach (DataRow row in t.Rows) columns.Add(Convert.ToString(row["name"]));
                    }
                    catch { }
                    ColumnCache[table] = columns;
                }
                return columns.Contains(column);
            }
        }

        // اولین موردِ غایب از فهرست «Table» یا «Table.Column» را برمی‌گرداند؛
        // null یعنی همه چیز موجود است.
        private static string FirstMissing(params string[] requirements)
        {
            if (requirements == null) return null;

            foreach (string requirement in requirements)
            {
                if (string.IsNullOrWhiteSpace(requirement)) continue;

                int dot = requirement.IndexOf('.');
                if (dot < 0)
                {
                    if (!TableExists(requirement)) return "جدول " + requirement;
                }
                else
                {
                    string table  = requirement.Substring(0, dot);
                    string column = requirement.Substring(dot + 1);
                    if (!TableExists(table))          return "جدول " + table;
                    if (!ColumnExists(table, column)) return "ستون " + requirement;
                }
            }
            return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // پیشرفت و لغو
        //
        // آموزش — چرا یک نوعِ کوچکِ اختصاصی و نه فقط IProgress<int>: نوار
        // پیشرفتِ درصدی به‌تنهایی به کاربر نمی‌گوید *چه چیزی* در حال اجراست.
        // در ابزار عیب‌یابی، «۳ از ۱۱ — بررسی رکوردهای تکراری» بسیار
        // ارزشمندتر از «۲۷٪» است، چون اگر عملیات طول بکشد کاربر می‌فهمد کدام
        // مرحله کند است.
        //
        // ⚠ قاعده: درصد فقط وقتی گزارش می‌شود که تعداد کلِ مراحل *واقعاً*
        // معلوم باشد. جایی که معلوم نیست (VACUUM/REINDEX/ANALYZE که یک دستورِ
        // اتمیکِ SQLite هستند) عمداً حالت نامعین باقی می‌ماند — درصدِ ساختگی
        // بدتر از نبودِ درصد است.
        // ═════════════════════════════════════════════════════════════════════
        internal sealed class DevProgress
        {
            public readonly int    Current;
            public readonly int    Total;
            public readonly string Text;

            public DevProgress(int current, int total, string text)
            {
                Current = current; Total = total; Text = text ?? "";
            }

            public int Percent
            {
                get { return Total <= 0 ? 0 : (int)Math.Min(100L, Current * 100L / Total); }
            }
        }

        private static void Report(IProgress<DevProgress> progress, int current, int total, string text)
        {
            if (progress != null) progress.Report(new DevProgress(current, total, text));
        }

        // متن استانداردِ «در دسترس نیست» — یک عبارت واحد در کل ماژول.
        public const string NotAvailable = "در دسترس نیست";

        // متن استانداردِ پنهان‌سازیِ مقادیر حساس — یک سیاست واحد برای بستهٔ
        // پشتیبانی و کاوشگر دیتابیس.
        public const string RedactedValue = "[پنهان شد]";

        // ─── شمارشِ سه‌حالته: موفق / ناموجود / خطا ───────────────────────────
        // آموزش — چرا SafeScalarInt به‌تنهایی کافی نبود: آن نسخه در هر خطایی
        // «۰» برمی‌گرداند، و ۰ در این ماژول یعنی «سالم». پس یک کوئریِ شکسته
        // به‌صورت «همه‌چیز سالم است» گزارش می‌شد — بدترین حالتِ ممکن برای یک
        // ابزارِ عیب‌یابی. حالا شکست از صفرِ واقعی تفکیک می‌شود.
        private static bool TryScalarInt(string sql, out int value, out string error)
        {
            value = 0; error = null;
            try
            {
                object result = Db.ExecuteScalar(sql);
                value = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ─── ثبت امنیتی هر عملیات ────────────────────────────────────────────
        // آموزش — سرویس لاگ امنیتیِ موجود عیناً بازاستفاده می‌شود: خودش کاربر،
        // نام رایانه و تاریخ/ساعت را ثبت می‌کند، پس نیازی به جدول یا کد جدید
        // نیست (خواستهٔ بخش SECURITY کاملاً پوشش داده می‌شود).
        public static void LogAction(string action)
        {
            try
            {
                Enterprise.SecurityAudit.Log(
                    "مرکز کنترل توسعه‌دهنده", "بالا", true, action);
            }
            catch { /* ثبت لاگ نباید مانع کار شود */ }
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۱) نمای کلی سیستم
        // ═════════════════════════════════════════════════════════════════════
        internal sealed class SystemOverview
        {
            public int    HealthScore;
            public string AppVersion    = "";
            public string DbVersion     = "";
            public string DbStatus      = "";
            public string DbSize        = "";
            public long   TotalRecords;
            public int    TotalUsers;
            public int    OnlineUsers;
            public string MemoryUsage   = "";
            public string StorageUsage  = "";
            public string Uptime        = "";
            public string Performance   = "";
            public List<string> HealthNotes = new List<string>();
        }

        // هر بخش مستقل محافظت می‌شود: شکستِ یکی نباید بقیهٔ نما را از بین ببرد
        // (خواستهٔ «اگر یک ویجت خطا داد، بقیه کار کنند»).
        public static SystemOverview GetOverview()
        {
            ResetSchemaCache();

            var o = new SystemOverview();

            o.AppVersion   = Guard(GetAppVersion,   "نامشخص");
            o.DbVersion    = Guard(GetDbVersion,    NotAvailable);
            o.DbSize       = Guard(delegate { return FormatBytes(GetDbFileSize()); }, NotAvailable);
            o.Uptime       = Guard(FormatUptime,    NotAvailable);
            o.MemoryUsage  = Guard(delegate { return FormatBytes(GetWorkingSet()); }, NotAvailable);
            o.StorageUsage = Guard(GetStorageUsage, NotAvailable);

            // ─ وضعیت دیتابیس (سریع؛ integrity_check کامل در «دکتر دیتابیس») ─
            string quick = SafeScalarText("PRAGMA quick_check;");
            bool dbReachable = !string.IsNullOrEmpty(quick);
            o.DbStatus = !dbReachable ? NotAvailable : (quick == "ok" ? "سالم" : quick);

            // ⚠ مقدار منفی ⇒ «در دسترس نیست». هرگز صفر برگردانده نمی‌شود، چون
            // صفر یک عددِ *واقعی* است و کاربر آن را باور می‌کند؛ همان اشتباهی
            // که در «دکتر دیتابیس» عمداً از آن پرهیز شده است.
            o.TotalRecords = Guard(CountAllRecords, -1L);

            // TblUsers ممکن است در پایگاه‌دادهٔ ناقص/قدیمی نباشد.
            int userCount; string userError;
            o.TotalUsers = TableExists("TblUsers") &&
                           TryScalarInt("SELECT COUNT(1) FROM TblUsers;", out userCount, out userError)
                ? userCount
                : -1;                                   // -1 ⇒ «در دسترس نیست»

            // «کاربران آنلاین» در این معماری تک‌کاربره/تک‌ایستگاه ثبت نمی‌شود.
            // نزدیک‌ترین معیارِ واقعیِ موجود: کاربرانی که قفل رکورد فعال دارند.
            //
            // ⚠ نامِ ستون از اسکیمای واقعیِ EntRecordLock گرفته شده است
            // (UserID). نسخهٔ قبلی نامِ حدسیِ LockedByUserID داشت و خطای
            // «no such column» می‌داد — ریشهٔ باگی که این بازبینی رفعش کرد.
            int lockedUsers; string lockError;
            o.OnlineUsers = ColumnExists("EntRecordLock", "UserID") &&
                            ColumnExists("EntRecordLock", "ExpiresAt") &&
                            TryScalarInt("SELECT COUNT(DISTINCT UserID) FROM EntRecordLock " +
                                         "WHERE UserID IS NOT NULL AND ExpiresAt > datetime('now');",
                                         out lockedUsers, out lockError)
                ? lockedUsers
                : -1;

            // ─── امتیاز سلامت (۰..۱۰۰) ───────────────────────────────────────
            // آموزش — امتیاز از «معیارهای واقعیِ قابل اندازه‌گیری» ساخته می‌شود،
            // نه عددِ دلخواه. هر ایراد وزن مشخصی کم می‌کند و دلیلش در
            // HealthNotes برای کاربر نوشته می‌شود تا عدد قابل‌توضیح باشد.
            //
            // ⚠ قاعدهٔ مهم: معیاری که *قابل اندازه‌گیری نبوده* هرگز جریمه نمی‌شود
            // و به‌جایش در یادداشت‌ها «در دسترس نیست» ثبت می‌گردد — وگرنه یک
            // پایگاه‌دادهٔ قدیمی به‌غلط «بحرانی» گزارش می‌شد.
            int score = 100;

            if (!dbReachable)
            {
                score -= 40; o.HealthNotes.Add("وضعیت دیتابیس قابل بررسی نیست");
            }
            else if (o.DbStatus != "سالم")
            {
                score -= 40; o.HealthNotes.Add("وضعیت دیتابیس سالم نیست");
            }

            int unresolvedErrors;
            if (TableExists("EntErrorLog") &&
                TryGuarded(delegate { return Enterprise.ErrorLogger.UnresolvedCount(); }, out unresolvedErrors))
            {
                if (unresolvedErrors > 0)
                {
                    score -= Math.Min(20, unresolvedErrors);
                    o.HealthNotes.Add(unresolvedErrors + " خطای بررسی‌نشده");
                }
            }

            if (ColumnExists("TblCase", "CasID") && ColumnExists("TblFamily", "CasID"))
            {
                int orphans = SafeScalarInt(
                    "SELECT COUNT(1) FROM TblCase c " +
                    "WHERE NOT EXISTS (SELECT 1 FROM TblFamily f WHERE f.CasID = c.CasID);");
                if (orphans > 0) { score -= Math.Min(15, orphans); o.HealthNotes.Add(orphans + " رکورد یتیم"); }
            }

            int missingFiles = Guard(CountMissingFiles, 0);
            if (missingFiles > 0) { score -= Math.Min(10, missingFiles); o.HealthNotes.Add(missingFiles + " فایل گمشده"); }

            string lastBackup = Guard(delegate { return SettingsHelper.Get(SettingsHelper.LastBackupDate); }, "");
            if (string.IsNullOrWhiteSpace(lastBackup))
            {
                score -= 10; o.HealthNotes.Add("هیچ بکاپی ثبت نشده");
            }
            else if (lastBackup != DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            {
                score -= 5; o.HealthNotes.Add("بکاپ امروز گرفته نشده");
            }

            o.HealthScore = Math.Max(0, Math.Min(100, score));

            o.Performance =
                o.HealthScore >= 85 ? "عالی" :
                o.HealthScore >= 70 ? "قابل قبول" :
                o.HealthScore >= 50 ? "نیازمند رسیدگی" : "بحرانی";

            return o;
        }

        // «هر مقدار مستقل» — شکستِ یک محاسبه فقط همان مقدار را جایگزین می‌کند.
        private static T Guard<T>(Func<T> get, T fallback)
        {
            try { return get(); } catch { return fallback; }
        }

        private static bool TryGuarded<T>(Func<T> get, out T value)
        {
            try { value = get(); return true; }
            catch { value = default(T); return false; }
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۲) دکتر دیتابیس
        // ═════════════════════════════════════════════════════════════════════
        // چهار وضعیتِ ممکن برای هر بررسی — «در دسترس نیست» و «خطا» عمداً از
        // «سالم» جدا هستند تا یک بررسیِ اجرانشده هرگز سبز گزارش نشود.
        public const string StateHealthy     = "سالم";
        public const string StateAttention   = "نیازمند بررسی";
        public const string StateUnavailable = NotAvailable;
        public const string StateFailed      = "خطا";

        // نتیجهٔ «دکتر دیتابیس» — شاملِ حالتِ لغو تا نتیجهٔ *ناقص* هرگز به‌جای
        // نتیجهٔ کامل جا نزند.
        internal sealed class DoctorReport
        {
            public DataTable Rows;
            public bool      Cancelled;
            public int       Completed;
            public int       Total;
        }

        // بافتِ اجرای بررسی‌ها: شمارندهٔ مرحله، گزارش پیشرفت و توکن لغو.
        private sealed class DoctorContext
        {
            public DataTable Table;
            public IProgress<DevProgress> Progress;
            public System.Threading.CancellationToken Cancel;
            public int Step;
            public int Total;
        }

        public const int DoctorCheckCount = 11;

        // امضای بدون پارامتر برای سازگاری با فراخوانی‌های موجود حفظ شده است.
        public static DataTable RunDatabaseDoctor()
        {
            return RunDatabaseDoctor(null, System.Threading.CancellationToken.None).Rows;
        }

        internal static DoctorReport RunDatabaseDoctor(
            IProgress<DevProgress> progress, System.Threading.CancellationToken cancel)
        {
            // اسکیما ممکن است از آخرین اجرا عوض شده باشد (مهاجرت/بازیابی بکاپ).
            ResetSchemaCache();

            DataTable t = NewTable("بررسی", "نتیجه", "تعداد", "وضعیت");

            var ctx = new DoctorContext
            {
                Table = t, Progress = progress, Cancel = cancel, Step = 0, Total = DoctorCheckCount
            };

            // ۱) یکپارچگی ساختاری (PRAGMA خودِ SQLite — به هیچ جدولی وابسته نیست)
            GuardedCheck(ctx, "یکپارچگی دیتابیس", null, delegate
            {
                string integrity = SafeScalarText("PRAGMA integrity_check;");
                if (string.IsNullOrEmpty(integrity))
                    return CheckResult.Failed("پاسخی از دیتابیس دریافت نشد");
                return integrity == "ok"
                    ? CheckResult.Ok(StateHealthy)
                    : CheckResult.Bad(integrity, 0);
            });

            // ۲) کیفیت داده — سرویس موجود بازاستفاده می‌شود
            GuardedCheck(ctx, "کیفیت داده", new[] { "TblCase" }, delegate
            {
                int n = new DataQualityChecker(Db).Check().Count;
                return n == 0 ? CheckResult.Ok("بدون ایراد")
                              : CheckResult.Bad("پرونده دارای نقص اطلاعات", n);
            });

            // ۳) رکوردهای تکراری — سرویس موجود بازاستفاده می‌شود
            GuardedCheck(ctx, "رکوردهای تکراری", new[] { "TblCase" }, delegate
            {
                // آموزش — DuplicateDetector از قبل توکن لغو می‌پذیرد؛ پیش‌تر
                // اینجا None پاس داده می‌شد و همین سنگین‌ترین بررسیِ ماژول
                // غیرقابل‌لغو بود. حالا توکنِ واقعی پاس می‌شود.
                int n = new DuplicateDetector(Db).Detect(null, ctx.Cancel).Count;
                return n == 0 ? CheckResult.Ok("موردی یافت نشد")
                              : CheckResult.Bad("جفت پروندهٔ مشکوک", n);
            });

            // ۴) ارجاع‌های شکسته — هر جدولِ فرزند مستقل شمرده می‌شود تا نبودِ
            //    یکی، کلِ بررسی را از کار نیندازد.
            GuardedCheck(ctx, "ارجاع‌های شکسته", new[] { "TblCase.CasID" }, delegate
            {
                int n = 0;
                n += CountBrokenChildren("TblFamily");
                n += CountBrokenChildren("TblDocs");
                n += CountBrokenChildren("TblAssistance");
                return n == 0 ? CheckResult.Ok(StateHealthy)
                              : CheckResult.Bad("رکورد با ارجاع نامعتبر", n);
            });

            // ۵) رکوردهای یتیم (پرونده‌ای که هیچ عضو خانواده ندارد)
            GuardedCheck(ctx, "رکوردهای یتیم", new[] { "TblCase.CasID", "TblFamily.CasID" }, delegate
            {
                int n = ScalarOrThrow(
                    "SELECT COUNT(1) FROM TblCase c " +
                    "WHERE NOT EXISTS (SELECT 1 FROM TblFamily f WHERE f.CasID = c.CasID);");
                return n == 0 ? CheckResult.Ok("موردی یافت نشد")
                              : CheckResult.Bad("پرونده بدون عضو خانواده", n);
            });

            // ۶) اسناد گمشده
            GuardedCheck(ctx, "اسناد گمشده", new[] { "TblDocs.DocFilePath" }, delegate
            {
                int n = CountMissingPathsOrThrow(
                    "SELECT DocFilePath AS P FROM TblDocs WHERE NULLIF(DocFilePath,'') IS NOT NULL");
                return n == 0 ? CheckResult.Ok(StateHealthy)
                              : CheckResult.Bad("فایل سند روی دیسک نیست", n);
            });

            // ۷) عکس‌های گمشده — سه منبع، هرکدام فقط اگر ستونش موجود باشد
            GuardedCheck(ctx, "عکس‌های گمشده", null, delegate
            {
                int n = 0;
                bool any = false;

                if (ColumnExists("TblCase", "PhotoPath"))
                {
                    any = true;
                    n += CountMissingPathsOrThrow("SELECT PhotoPath AS P FROM TblCase WHERE NULLIF(PhotoPath,'') IS NOT NULL");
                }
                if (ColumnExists("TblCase", "FamilyPhotoPath"))
                {
                    any = true;
                    n += CountMissingPathsOrThrow("SELECT FamilyPhotoPath AS P FROM TblCase WHERE NULLIF(FamilyPhotoPath,'') IS NOT NULL");
                }
                if (ColumnExists("TblFamily", "MemberPhotoPath"))
                {
                    any = true;
                    n += CountMissingPathsOrThrow("SELECT MemberPhotoPath AS P FROM TblFamily WHERE NULLIF(MemberPhotoPath,'') IS NOT NULL");
                }

                if (!any) return CheckResult.Unavailable("هیچ ستون مسیر عکسی وجود ندارد");
                return n == 0 ? CheckResult.Ok(StateHealthy)
                              : CheckResult.Bad("فایل عکس روی دیسک نیست", n);
            });

            // ۸) پیوست‌های بدون استفاده
            GuardedCheck(ctx, "پیوست‌های بدون استفاده", new[] { "TblDocs" }, delegate
            {
                int n = new FileCleanupHelper().FindUnusedFiles().Count;
                return n == 0 ? CheckResult.Ok(StateHealthy)
                              : CheckResult.Bad("فایل روی دیسک بدون رکورد", n);
            });

            // ۹) شماره‌گذاری نامعتبر (کد/شمارهٔ فرم خالی یا تکراری)
            GuardedCheck(ctx, "شماره‌گذاری نامعتبر", new[] { "TblCase.Code", "TblCase.FormNo" }, delegate
            {
                int n = ScalarOrThrow(@"
SELECT
 (SELECT COUNT(1) FROM TblCase WHERE NULLIF(Code,'') IS NULL OR NULLIF(FormNo,'') IS NULL) +
 (SELECT COUNT(1) FROM (SELECT Code   FROM TblCase WHERE NULLIF(Code,'')   IS NOT NULL GROUP BY Code   HAVING COUNT(1) > 1)) +
 (SELECT COUNT(1) FROM (SELECT FormNo FROM TblCase WHERE NULLIF(FormNo,'') IS NOT NULL GROUP BY FormNo HAVING COUNT(1) > 1));");
                return n == 0 ? CheckResult.Ok(StateHealthy)
                              : CheckResult.Bad("کد/شماره خالی یا تکراری", n);
            });

            // ۱۰) سازگاری بایگانی — ستون‌های بایگانی در نسخه‌های قدیمی نبودند
            GuardedCheck(ctx, "سازگاری بایگانی",
                new[] { "TblCase.IsArchived", "TblCase.ArchivedAt", "TblCase.ArchivedBy" }, delegate
            {
                int n = ScalarOrThrow(@"
SELECT
 (SELECT COUNT(1) FROM TblCase WHERE IsArchived = 1 AND (NULLIF(ArchivedAt,'') IS NULL OR NULLIF(ArchivedBy,'') IS NULL)) +
 (SELECT COUNT(1) FROM TblCase WHERE IsArchived = 0 AND NULLIF(ArchivedAt,'') IS NOT NULL);");

                // بخشِ اسناد فقط اگر ستون‌هایش موجود باشد به شمارش اضافه می‌شود.
                if (ColumnExists("TblDocs", "IsArchived") &&
                    ColumnExists("TblDocs", "ArchivedAt") &&
                    ColumnExists("TblDocs", "ArchivedBy"))
                {
                    n += ScalarOrThrow(
                        "SELECT COUNT(1) FROM TblDocs WHERE IsArchived = 1 " +
                        "AND (NULLIF(ArchivedAt,'') IS NULL OR NULLIF(ArchivedBy,'') IS NULL);");
                }

                return n == 0 ? CheckResult.Ok(StateHealthy)
                              : CheckResult.Bad("رکورد بایگانیِ ناقص", n);
            });

            // ۱۱) سازگاری بارکد — بارکد از روی «کد اختصاصی» ساخته می‌شود
            // (Code128)، پس کدِ خالی یا دارای کاراکتر خارج از محدودهٔ چاپیِ
            // ASCII اصلاً قابل تولید/اسکن نیست.
            GuardedCheck(ctx, "سازگاری بارکد", new[] { "TblCase.Code" }, delegate
            {
                int n = ScalarOrThrow(
                    "SELECT COUNT(1) FROM TblCase WHERE NULLIF(Code,'') IS NULL OR Code GLOB '*[^ -~]*';");
                return n == 0 ? CheckResult.Ok(StateHealthy)
                              : CheckResult.Bad("کد غیرقابل تبدیل به بارکد", n);
            });

            return new DoctorReport
            {
                Rows      = t,
                Cancelled = cancel.IsCancellationRequested,
                Completed = t.Rows.Count,
                Total     = ctx.Total
            };
        }

        // نتیجهٔ یک بررسی: متن، تعداد و وضعیت.
        private sealed class CheckResult
        {
            public string Text = "";
            public int    Count;
            public string State = StateHealthy;

            public static CheckResult Ok(string text)                 { return new CheckResult { Text = text, State = StateHealthy }; }
            public static CheckResult Bad(string text, int count)     { return new CheckResult { Text = text, Count = count, State = StateAttention }; }
            public static CheckResult Unavailable(string reason)      { return new CheckResult { Text = NotAvailable + " — " + reason, State = StateUnavailable }; }
            public static CheckResult Failed(string message)          { return new CheckResult { Text = "خطا: " + message, State = StateFailed }; }
        }

        // ─── هستهٔ «هر ویجت مستقل» ───────────────────────────────────────────
        // پیش‌نیازها بررسی می‌شوند؛ اگر جدول/ستونی نباشد «در دسترس نیست» ثبت
        // می‌گردد، و اگر خودِ بررسی استثنا بدهد فقط همان سطر «خطا» می‌شود.
        // در هر دو حالت بقیهٔ بررسی‌ها بدون تأثیر ادامه می‌یابند.
        // آموزش — لغو و پیشرفت هر دو *در همین یک نقطه* اعمال می‌شوند، نه در
        // بدنهٔ یازده بررسی. یعنی هیچ بررسیِ آینده‌ای نمی‌تواند «یادش برود»
        // لغو را رعایت کند، و بدنهٔ بررسی‌ها دست‌نخورده باقی می‌ماند.
        private static void GuardedCheck(DoctorContext ctx, string name, string[] requires, Func<CheckResult> run)
        {
            // لغو ⇒ این بررسی و بقیه اصلاً شروع نمی‌شوند؛ ردیف‌های تاکنون
            // ثبت‌شده به‌عنوان «نتیجهٔ ناقص» برگردانده می‌شوند.
            if (ctx.Cancel.IsCancellationRequested) return;

            ctx.Step++;
            Report(ctx.Progress, ctx.Step, ctx.Total, name);

            CheckResult result;

            string missing = FirstMissing(requires);
            if (missing != null)
            {
                result = CheckResult.Unavailable(missing + " وجود ندارد");
            }
            else
            {
                try { result = run(); }
                catch (OperationCanceledException)
                {
                    // لغو یک «خطا» نیست و نباید در لاگ خطا یا در گرید به‌عنوان
                    // ایراد ثبت شود.
                    return;
                }
                catch (Exception ex)
                {
                    result = CheckResult.Failed(ex.Message);
                    TryLog(ex, "DevCenter.Doctor/" + name);
                }
            }

            ctx.Table.Rows.Add(name, result.Text,
                               result.Count == 0 ? "" : result.Count.ToString("N0"),
                               result.State);
        }

        // شمارشِ فرزندانِ بی‌والد — فقط اگر جدول و ستونش واقعاً وجود داشته باشد.
        private static int CountBrokenChildren(string childTable)
        {
            if (!ColumnExists(childTable, "CasID")) return 0;

            return ScalarOrThrow(
                "SELECT COUNT(1) FROM " + childTable + " x " +
                "WHERE NOT EXISTS (SELECT 1 FROM TblCase c WHERE c.CasID = x.CasID);");
        }

        // برخلاف SafeScalarInt، این نسخه خطا را پنهان نمی‌کند — GuardedCheck
        // آن را می‌گیرد و به «خطا» تبدیل می‌کند (نه به «سالم»).
        private static int ScalarOrThrow(string sql)
        {
            object value = Db.ExecuteScalar(sql);
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static int CountMissingPathsOrThrow(string sql)
        {
            int missing = 0;
            DataTable t = Db.Query(sql);
            foreach (DataRow row in t.Rows)
            {
                string path = Convert.ToString(row["P"]);
                if (string.IsNullOrWhiteSpace(path)) continue;
                try { if (!File.Exists(path)) missing++; }
                catch { /* مسیرِ نامعتبر (کاراکتر غیرمجاز) = فایلِ غیرقابل دسترسی */ missing++; }
            }
            return missing;
        }

        private static void TryLog(Exception ex, string source)
        {
            try { Enterprise.ErrorLogger.Log(ex, source); } catch { }
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۳) نگهداری
        // ═════════════════════════════════════════════════════════════════════
        // امضای مشترکِ همهٔ عملیاتِ «نگهداری»: پیشرفت + لغو.
        internal delegate string DevOperation(
            IProgress<DevProgress> progress, System.Threading.CancellationToken cancel);

        internal static string OptimizeDatabase(IProgress<DevProgress> progress,
                                                System.Threading.CancellationToken cancel)
        {
            long before = GetDbFileSize();
            RunSql("VACUUM;", cancel);
            long after = GetDbFileSize();
            return "فشرده‌سازی انجام شد. حجم: " + FormatBytes(before) + " ← " + FormatBytes(after);
        }

        internal static string RebuildIndexes(IProgress<DevProgress> progress,
                                              System.Threading.CancellationToken cancel)
        {
            RunSql("REINDEX;", cancel);
            return "بازسازی همهٔ Indexها انجام شد.";
        }

        internal static string RefreshStatistics(IProgress<DevProgress> progress,
                                                  System.Threading.CancellationToken cancel)
        {
            RunSql("ANALYZE;", cancel);
            return "آمار بهینه‌ساز کوئری (ANALYZE) به‌روزرسانی شد.";
        }

        internal static string VerifyAttachments(IProgress<DevProgress> progress,
                                                  System.Threading.CancellationToken cancel)
        {
            if (!ColumnExists("TblDocs", "DocFilePath"))
                return NotAvailable + " — ستون TblDocs.DocFilePath در این پایگاه‌داده وجود ندارد.";

            // اینجا تعداد کل *واقعاً* معلوم است (تعداد ردیف‌های سند)، پس درصدِ
            // واقعی گزارش می‌شود.
            int missing = CountMissingPaths(
                "SELECT DocFilePath AS P FROM TblDocs WHERE NULLIF(DocFilePath,'') IS NOT NULL",
                progress, cancel, "بررسی فایل اسناد");

            cancel.ThrowIfCancellationRequested();

            int unused = Guard(delegate { return new FileCleanupHelper().FindUnusedFiles().Count; }, -1);
            return "پیوست‌ها بررسی شد — گمشده: " + missing +
                   " | بدون استفاده: " + (unused < 0 ? NotAvailable : unused.ToString());
        }

        internal static string VerifyStorage(IProgress<DevProgress> progress,
                                              System.Threading.CancellationToken cancel)
        {
            var sb = new StringBuilder();
            foreach (var pair in GetStoragePaths())
            {
                bool exists = Directory.Exists(pair.Value);
                sb.AppendLine(pair.Key + ": " + (exists ? "موجود" : "پیدا نشد") + " — " + pair.Value);
            }

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory));
                sb.AppendLine("فضای آزاد درایو " + drive.Name + ": " + FormatBytes(drive.AvailableFreeSpace)
                              + " از " + FormatBytes(drive.TotalSize));
            }
            catch { }

            return sb.ToString().TrimEnd();
        }

        // ═════════════════════════════════════════════════════════════════════
        // همگام‌سازی دستی با سرور (فاز ۶.۲)
        //
        // آموزش — این متد هیچ منطق همگام‌سازیِ تازه‌ای ندارد: فقط SyncService
        // موجود را با انتقالِ HTTP صدا می‌زند و نتیجه را به متنِ خروجیِ تب
        // «نگهداری» تبدیل می‌کند. اگر سروری تنظیم نشده باشد، همان پیامِ
        // صادقانهٔ «آفلاین» برمی‌گردد و صف دست‌نخورده می‌ماند.
        // ═════════════════════════════════════════════════════════════════════
        internal static string RunSynchronization(IProgress<DevProgress> progress,
                                                   System.Threading.CancellationToken cancel)
        {
            var transport = new Sync.HttpSyncTransport();

            if (!transport.IsConfigured)
                return "آدرس سرور تنظیم نشده است — برنامه آفلاین کار می‌کند." + Environment.NewLine +
                       "در انتظار ارسال: " + Sync.SyncOutboxService.PendingCount().ToString("N0");

            // پیشرفتِ SyncProgress به قالب پیشرفتِ مرکز کنترل نگاشت می‌شود تا
            // همان نوار و درصد بدون تغییر کار کند.
            IProgress<Sync.SyncProgress> bridge = progress == null
                ? null
                : new Progress<Sync.SyncProgress>(p =>
                    Report(progress, p.Current, p.Total, p.Phase));

            // ⚠ فاز B — از همان قفلِ سراسریِ مدیرِ پس‌زمینه عبور می‌کند تا
            // همگام‌سازیِ دستی و خودکار هرگز هم‌زمان اجرا نشوند.
            Sync.SyncRunResult result = Sync.BackgroundSyncManager.RunManual(bridge, cancel);

            if (result == null)
                return "همگام‌سازیِ دیگری هم‌اکنون در حال اجراست — این درخواست انجام نشد." +
                       Environment.NewLine +
                       "وضعیت: " + Sync.BackgroundSyncManager.LastPhase;

            var sb = new StringBuilder();
            sb.AppendLine(result.Skipped ? result.Message : result.Summary);
            sb.Append("در انتظار ارسال: ").Append(Sync.SyncOutboxService.PendingCount().ToString("N0"));
            sb.Append(" | ناموفق: ").Append(Sync.SyncOutboxService.FailedCount().ToString("N0"));
            sb.Append(" | تعارض باز: ").Append(Sync.SyncConflictStore.OpenCount().ToString("N0"));

            return sb.ToString();
        }

        // ═════════════════════════════════════════════════════════════════════
        // گزارش تشخیصی همگام‌سازی (فاز C)
        //
        // یک تصویرِ کاملِ متنی که کاربر می‌تواند کپی کند و برای پشتیبانی
        // بفرستد — بدون نیاز به دسترسی به دیتابیس یا لاگ‌ها.
        // ═════════════════════════════════════════════════════════════════════
        internal static string RunSyncDiagnostics(IProgress<DevProgress> progress,
                                                   System.Threading.CancellationToken cancel)
        {
            Report(progress, 0, 1, "جمع‌آوری وضعیت همگام‌سازی");

            string report = Sync.BackgroundSyncManager.BuildDiagnosticReport();

            Report(progress, 1, 1, "گزارش آماده شد");
            return report;
        }

        internal static string VerifyBackup(string backupFolder,
            IProgress<DevProgress> progress, System.Threading.CancellationToken cancel)
        {
            return VerifyBackup(backupFolder);
        }

        public static string VerifyBackup(string backupFolder)
        {
            // آموزش — قالبِ بکاپ همان چیزی است که BackupHelper.ExportBackup
            // می‌سازد (یک CaseManagementBackup.xml)؛ اینجا فقط خوانده و
            // اعتبارسنجی می‌شود، هیچ چیزی بازنویسی نمی‌گردد.
            string xmlPath = Path.Combine(backupFolder, "CaseManagementBackup.xml");
            if (!File.Exists(xmlPath))
                return "نامعتبر: فایل CaseManagementBackup.xml در این پوشه پیدا نشد.";

            // ⚠ امنیت — XXE: فایل بکاپ «ورودیِ نامعتبرِ بالقوه» است (از پوشه‌ای
            // که کاربر انتخاب می‌کند، شاید از یک فلش یا اشتراک شبکه). حالت
            // پیش‌فرضِ DataSet.ReadXml(path) در .NET Framework از XmlTextReader
            // استفاده می‌کند که DTD را پردازش می‌کند؛ یک فایل ساختگی می‌توانست
            // با Entityهای خارجی محتوای فایل‌های محلی را بخواند یا با
            // «billion laughs» حافظه را تمام کند. با این تنظیمات، DTD به‌کلی رد
            // و هیچ منبع خارجی‌ای resolve نمی‌شود.
            var xmlSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver   = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };

            // DataSet یک IDisposable است و بکاپ‌های بزرگ حافظهٔ زیادی می‌گیرند؛
            // با using بلافاصله پس از خواندن آزاد می‌شود.
            using (var ds = new DataSet())
            {
                ds.Locale = CultureInfo.InvariantCulture;
                using (XmlReader reader = XmlReader.Create(xmlPath, xmlSettings))
                    ds.ReadXml(reader);

                var sb = new StringBuilder("بکاپ معتبر است. جدول‌های موجود:");
                foreach (DataTable table in ds.Tables)
                    sb.AppendLine().Append("  ").Append(table.TableName).Append(": ")
                      .Append(table.Rows.Count.ToString("N0")).Append(" رکورد");
                return sb.ToString();
            }
        }

        internal static string ClearTemporaryFiles(IProgress<DevProgress> progress,
                                                    System.Threading.CancellationToken cancel)
        {
            return ClearTemporaryFilesCore(progress, cancel);
        }

        public static string ClearTemporaryFiles()
        {
            return ClearTemporaryFilesCore(null, System.Threading.CancellationToken.None);
        }

        private static string ClearTemporaryFilesCore(IProgress<DevProgress> progress,
                                                       System.Threading.CancellationToken cancel)
        {
            int deleted = 0;
            long freed = 0;
            var skipped = new List<string>();

            string tempPath = SettingsHelper.Get(SettingsHelper.TempPath);
            var folders = new List<string>();
            if (!string.IsNullOrWhiteSpace(tempPath)) folders.Add(tempPath);
            folders.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp"));

            foreach (string folder in folders.Distinct())
            {
                // ⛔ محافظ حیاتی: «مسیر موقت» یک تنظیمِ آزادِ متنی است که مدیر
                // سیستم تایپ می‌کند. اگر اشتباهاً روی C:\ یا پوشهٔ خودِ برنامه
                // یا پوشهٔ دیتابیس تنظیم شده باشد، این عملیات — که کاربر آن را
                // «پاکسازی فایل موقت» می‌بیند — کلِ نصب یا داده‌های واقعی را
                // بازگشت‌ناپذیر حذف می‌کرد. پیش از هر حذفی مسیر اعتبارسنجی
                // می‌شود و مسیرِ خطرناک فقط گزارش می‌گردد، نه پاک.
                string reason;
                if (IsProtectedFolder(folder, out reason))
                {
                    skipped.Add(folder + " (" + reason + ")");
                    continue;
                }

                if (!Directory.Exists(folder)) continue;

                string[] files;
                try { files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories); }
                catch (Exception ex) { skipped.Add(folder + " (" + ex.Message + ")"); continue; }

                int index = 0;
                foreach (string file in files)
                {
                    // لغو در میانهٔ کار امن است: فایل‌های حذف‌شده حذف‌شده‌اند و
                    // نتیجهٔ جزئی گزارش می‌شود؛ هیچ فایلی نیمه‌حذف نمی‌ماند.
                    cancel.ThrowIfCancellationRequested();

                    index++;
                    if (index % 25 == 0 || index == files.Length)
                        Report(progress, index, files.Length, "پاکسازی فایل‌های موقت");

                    try
                    {
                        var info = new FileInfo(file);
                        long size = info.Length;
                        info.Delete();
                        deleted++; freed += size;
                    }
                    catch { /* فایل در حال استفاده — رد می‌شود */ }
                }
            }

            var sb = new StringBuilder(deleted == 0
                ? "فایل موقتی برای حذف پیدا نشد."
                : "تعداد فایل حذف‌شده: " + deleted + " | فضای آزادشده: " + FormatBytes(freed));

            foreach (string item in skipped)
                sb.AppendLine().Append("  ⛔ پاک نشد — مسیر ناایمن: ").Append(item);

            return sb.ToString();
        }

        // ─── مسیرهایی که هرگز نباید پاکسازی شوند ─────────────────────────────
        // ریشهٔ درایو، پوشهٔ نصب برنامه، پوشهٔ دیتابیس، و پوشه‌های سیستمی/کاربری
        // ویندوز. همچنین هر مسیری که *والدِ* یکی از این‌ها باشد (چون پاکسازی
        // بازگشتی است و آن‌ها را هم در بر می‌گیرد).
        private static bool IsProtectedFolder(string folder, out string reason)
        {
            reason = null;

            string full;
            try
            {
                if (string.IsNullOrWhiteSpace(folder)) { reason = "مسیر خالی"; return true; }
                full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception ex) { reason = ex.Message; return true; }

            if (full.Length == 0) { reason = "مسیر خالی"; return true; }

            string root = (Path.GetPathRoot(full) ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                reason = "ریشهٔ درایو";
                return true;
            }

            foreach (string critical in ProtectedFolders())
            {
                if (string.IsNullOrWhiteSpace(critical)) continue;

                string other;
                try { other = Path.GetFullPath(critical).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
                catch { continue; }
                if (other.Length == 0) continue;

                if (string.Equals(full, other, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "پوشهٔ محافظت‌شده";
                    return true;
                }

                // مسیرِ داده‌شده والدِ یک پوشهٔ محافظت‌شده است ⇒ حذفِ بازگشتی
                // آن پوشه را هم می‌بلعد.
                if (other.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "شاملِ پوشهٔ محافظت‌شده";
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> ProtectedFolders()
        {
            yield return AppDomain.CurrentDomain.BaseDirectory;

            string dbPath = GetDbFilePath();
            if (!string.IsNullOrWhiteSpace(dbPath))
            {
                string dbFolder = null;
                try { dbFolder = Path.GetDirectoryName(dbPath); } catch { }
                if (!string.IsNullOrWhiteSpace(dbFolder)) yield return dbFolder;
            }

            foreach (var special in new[]
            {
                Environment.SpecialFolder.Windows,
                Environment.SpecialFolder.System,
                Environment.SpecialFolder.SystemX86,
                Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86,
                Environment.SpecialFolder.CommonProgramFiles,
                Environment.SpecialFolder.UserProfile,
                Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolder.Desktop,
                Environment.SpecialFolder.DesktopDirectory,
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolder.CommonApplicationData
            })
            {
                string path = null;
                try { path = Environment.GetFolderPath(special); } catch { }
                if (!string.IsNullOrWhiteSpace(path)) yield return path;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۴) مرکز لاگ — همگی از سرویس‌های موجود
        // ═════════════════════════════════════════════════════════════════════
        // آموزش — هر چهار منبعِ لاگ ممکن است روی پایگاه‌دادهٔ قدیمی/ناقص وجود
        // نداشته باشند. به‌جای استثنا، یک جدولِ تک‌سطریِ «در دسترس نیست»
        // برگردانده می‌شود تا تب باز بماند و بقیهٔ منابع قابل انتخاب باشند.
        private static DataTable UnavailableTable(string reason)
        {
            DataTable t = NewTable("وضعیت");
            t.Rows.Add(NotAvailable + " — " + reason);
            return t;
        }

        private static DataTable GuardedLog(string table, Func<DataTable> query)
        {
            if (!TableExists(table))
                return UnavailableTable("جدول " + table + " در این پایگاه‌داده وجود ندارد");

            try { return query(); }
            catch (Exception ex)
            {
                TryLog(ex, "DevCenter.Log/" + table);
                return UnavailableTable(ex.Message);
            }
        }

        public static DataTable GetErrorLog(int days)
        {
            return GuardedLog("EntErrorLog",
                delegate { return Enterprise.ErrorLogger.GetErrors("", false, days); });
        }

        public static DataTable GetSecurityLog(int days)
        {
            return GuardedLog("EntSecurityEvent",
                delegate { return Enterprise.SecurityAudit.GetEvents("", "", "", days); });
        }

        public static DataTable GetAuditLog(int days)
        {
            return GuardedLog("TblAuditLog", delegate { return Db.Query(@"
SELECT LogID      AS 'شناسه',
       CreatedAt  AS 'تاریخ',
       Username   AS 'کاربر',
       Operation  AS 'عملیات',
       EntityName AS 'جدول',
       EntityID   AS 'شناسه رکورد',
       OldValue   AS 'مقدار قبلی',
       NewValue   AS 'مقدار جدید'
FROM   TblAuditLog
WHERE  (@Days <= 0 OR CreatedAt >= datetime('now', '-' || @Days || ' days'))
ORDER  BY LogID DESC
LIMIT  2000;", new SQLiteParameter("@Days", days)); });
        }

        // «لاگ سیستم» = ردیابی تغییرات سطح رکورد که خودِ برنامه از قبل
        // در TblAuditLogs می‌نویسد (جدای از TblAuditLog کاربری).
        public static DataTable GetSystemLog(int days)
        {
            return GuardedLog("TblAuditLogs", delegate { return Db.Query(@"
SELECT LogID      AS 'شناسه',
       ActionDate AS 'تاریخ',
       TableName  AS 'جدول',
       ActionType AS 'نوع عملیات',
       RecordID   AS 'شناسه رکورد',
       UserID     AS 'کاربر'
FROM   TblAuditLogs
WHERE  (@Days <= 0 OR ActionDate >= datetime('now', '-' || @Days || ' days'))
ORDER  BY LogID DESC
LIMIT  2000;", new SQLiteParameter("@Days", days)); });
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۵) بستهٔ پشتیبانی (ZIP)
        // ═════════════════════════════════════════════════════════════════════
        public const int SupportSectionCount = 10;   // ۹ فایل + محاسبهٔ نمای کلی

        internal static string BuildSupportPackage(string targetZipPath,
            IProgress<DevProgress> progress, System.Threading.CancellationToken cancel)
        {
            string staging = Path.Combine(Path.GetTempPath(), "SupportPkg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);

            int step = 0;

            try
            {
                // هر بخش مستقل نوشته می‌شود: اگر یکی شکست بخورد، فایلش حاوی
                // پیام خطا می‌شود ولی بستهٔ پشتیبانی همچنان ساخته می‌گردد —
                // دقیقاً همان‌جایی که بیشترین نیاز به بسته وجود دارد.
                // ⚡ نمای کلی *یک بار* محاسبه می‌شود. پیش‌تر هر دو بخشِ زیر
                // GetOverview را جداگانه صدا می‌زدند، و هر فراخوانی یعنی
                // quick_check + COUNT روی همهٔ جدول‌ها + File.Exists روی همهٔ
                // مسیرهای ذخیره‌شده. روی پایگاه‌دادهٔ بزرگ این کارِ سنگین دو
                // برابر انجام می‌شد.
                // null ⇒ محاسبه ممکن نشد؛ WriteSection همان را به یک فایلِ
                // «این بخش ساخته نشد» تبدیل می‌کند (نه یک گزارشِ صفرِ گمراه‌کننده).
                Report(progress, ++step, SupportSectionCount, "جمع‌آوری نمای کلی سیستم");
                SystemOverview overview = Guard<SystemOverview>(GetOverview, null);
                cancel.ThrowIfCancellationRequested();

                WriteSection(staging, "01_ApplicationInfo.txt", delegate { return BuildApplicationInfo(overview); },
                             progress, ref step, cancel);
                WriteSection(staging, "02_DatabaseInfo.txt",    delegate { return BuildDatabaseInfo(overview); },
                             progress, ref step, cancel);
                WriteSection(staging, "03_Configuration.txt",   BuildConfiguration,
                             progress, ref step, cancel);
                WriteSection(staging, "04_SystemInfo.txt",      BuildSystemInfoText,
                             progress, ref step, cancel);
                WriteSection(staging, "05_Modules.csv",  delegate { return ToCsv(GetInstalledModules()); },
                             progress, ref step, cancel);
                WriteSection(staging, "06_Plugins.csv",  delegate { return ToCsv(GetLoadedPlugins()); },
                             progress, ref step, cancel);
                WriteSection(staging, "07_ErrorLog.csv", delegate { return ToCsv(GetErrorLog(90)); },
                             progress, ref step, cancel);
                WriteSection(staging, "08_AuditLog.csv", delegate { return ToCsv(GetAuditLog(90)); },
                             progress, ref step, cancel);
                WriteSection(staging, "09_HealthReport.csv",
                             delegate { return ToCsv(RunDatabaseDoctor(null, cancel).Rows); },
                             progress, ref step, cancel);

                // ⚠ آخرین بررسیِ لغو *پیش از* دست‌زدن به فایل مقصد: اگر کاربر
                // لغو کرده باشد، فایل قبلی نباید حذف شود و هیچ ZIPِ ناقصی هم
                // ساخته نشود. بستهٔ نصفه‌کاره‌ای که به‌دستِ توسعه‌دهنده برسد
                // بدتر از نبودِ بسته است.
                cancel.ThrowIfCancellationRequested();

                if (File.Exists(targetZipPath)) File.Delete(targetZipPath);
                ZipFile.CreateFromDirectory(staging, targetZipPath, CompressionLevel.Optimal, false);
                return targetZipPath;
            }
            finally
            {
                try { Directory.Delete(staging, true); } catch { }
            }
        }

        // سازگاری با فراخوانیِ ساده (بدون پیشرفت/لغو).
        private static void WriteSection(string folder, string fileName, Func<string> build)
        {
            int ignored = 0;
            WriteSection(folder, fileName, build, null, ref ignored,
                         System.Threading.CancellationToken.None);
        }

        private static void WriteSection(string folder, string fileName, Func<string> build,
            IProgress<DevProgress> progress, ref int step, System.Threading.CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            Report(progress, ++step, SupportSectionCount, fileName);

            string content;
            try { content = build(); }
            catch (OperationCanceledException) { throw; }   // لغو باید بالا برود، نه اینکه فایلِ خطا بسازد
            catch (Exception ex)
            {
                content = NotAvailable + " — این بخش ساخته نشد." + Environment.NewLine + ex.Message;
                TryLog(ex, "DevCenter.Support/" + fileName);
            }

            try { File.WriteAllText(Path.Combine(folder, fileName), content, Encoding.UTF8); }
            catch (Exception ex) { TryLog(ex, "DevCenter.Support.Write/" + fileName); }
        }

        private static string BuildApplicationInfo(SystemOverview o)
        {
            if (o == null) throw new InvalidOperationException("نمای کلی سیستم محاسبه نشد.");

            var sb = new StringBuilder();
            sb.AppendLine("نسخهٔ نرم‌افزار : " + o.AppVersion);
            sb.AppendLine("مسیر نصب       : " + AppDomain.CurrentDomain.BaseDirectory);
            sb.AppendLine("مدت اجرا       : " + o.Uptime);
            sb.AppendLine("مصرف حافظه     : " + o.MemoryUsage);
            sb.AppendLine("امتیاز سلامت   : " + o.HealthScore + " / 100 (" + o.Performance + ")");
            if (o.HealthNotes.Count > 0)
                sb.AppendLine("نکات سلامت     : " + string.Join(" — ", o.HealthNotes));
            return sb.ToString();
        }

        private static string BuildDatabaseInfo(SystemOverview o)
        {
            if (o == null) throw new InvalidOperationException("نمای کلی سیستم محاسبه نشد.");

            var sb = new StringBuilder();
            sb.AppendLine("نسخهٔ دیتابیس  : " + o.DbVersion);
            sb.AppendLine("وضعیت          : " + o.DbStatus);
            sb.AppendLine("حجم فایل       : " + o.DbSize);
            sb.AppendLine("مجموع رکوردها  : " + (o.TotalRecords < 0 ? NotAvailable : o.TotalRecords.ToString("N0")));
            sb.AppendLine("تعداد کاربران  : " + (o.TotalUsers < 0 ? NotAvailable : o.TotalUsers.ToString("N0")));
            sb.AppendLine();
            sb.AppendLine("تعداد رکورد هر جدول:");
            foreach (DataRow row in GetTableRowCounts().Rows)
                sb.AppendLine("  " + row[0] + " : " + row[1]);
            return sb.ToString();
        }

        private static string BuildConfiguration()
        {
            if (!ColumnExists("TblAppSettings", "SettingKey") ||
                !ColumnExists("TblAppSettings", "SettingValue"))
                return NotAvailable + " — جدول TblAppSettings در این پایگاه‌داده وجود ندارد.";

            var sb = new StringBuilder();
            try
            {
                // ⚠ فقط کلیدهای غیرحساس. رمز/هش/کلید هرگز در بستهٔ پشتیبانی نمی‌رود.
                DataTable t = Db.Query(
                    "SELECT SettingKey, SettingValue FROM TblAppSettings ORDER BY SettingKey;");
                foreach (DataRow row in t.Rows)
                {
                    string key = Convert.ToString(row["SettingKey"]);
                    if (IsSensitiveKey(key)) { sb.AppendLine(key + " = " + RedactedValue); continue; }
                    sb.AppendLine(key + " = " + Convert.ToString(row["SettingValue"]));
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine(NotAvailable + " — " + ex.Message);
            }
            return sb.ToString();
        }

        private static bool IsSensitiveKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            string k = key.ToLowerInvariant();
            return k.Contains("password") || k.Contains("secret") ||
                   k.Contains("token")    || k.Contains("apikey") ||
                   k.Contains("license")  || k.Contains("hash");
        }

        private static string BuildSystemInfoText()
        {
            var sb = new StringBuilder();
            foreach (DataRow row in GetDiagnostics().Rows)
                sb.AppendLine(row[0] + " : " + row[1]);
            return sb.ToString();
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۶) عیب‌یابی
        // ═════════════════════════════════════════════════════════════════════
        // هر سطر مستقل و محافظت‌شده اضافه می‌شود: شکستِ یکی، بقیهٔ گزارش
        // عیب‌یابی را از بین نمی‌برد.
        private static void AddDiag(DataTable t, string name, Func<string> value)
        {
            string text;
            try { text = value() ?? ""; }
            catch (Exception ex) { text = NotAvailable + " — " + ex.Message; }
            t.Rows.Add(name, string.IsNullOrWhiteSpace(text) ? NotAvailable : text);
        }

        public static DataTable GetDiagnostics()
        {
            return GetDiagnostics(null, System.Threading.CancellationToken.None);
        }

        internal static DataTable GetDiagnostics(IProgress<DevProgress> progress,
                                                  System.Threading.CancellationToken cancel)
        {
            DataTable t = NewTable("مورد", "مقدار");

            // ردیف‌های سنگین (ماژول‌ها، اسمبلی‌ها، قفل‌ها) در انتها هستند؛ توکن
            // بین ردیف‌ها بررسی می‌شود تا لغو در گزارشِ عیب‌یابی هم مؤثر باشد.
            // بستهٔ محلی به‌جای فیلدِ ایستا استفاده می‌شود — GetDiagnostics هم
            // از نخِ رابط کاربری و هم از نخِ «بستهٔ پشتیبانی» صدا زده می‌شود و
            // یک شمارندهٔ ایستا بینشان مشترک و در نتیجه ناایمن می‌شد.
            const int totalDiagnostics = 29;
            int step = 0;

            Action<string, Func<string>> add = delegate (string name, Func<string> value)
            {
                cancel.ThrowIfCancellationRequested();
                Report(progress, ++step, totalDiagnostics, name);
                AddDiag(t, name, value);
            };

            add("نسخهٔ ویندوز",   delegate { return Environment.OSVersion.VersionString; });
            add("سیستم ۶۴ بیتی",  delegate { return Environment.Is64BitOperatingSystem ? "بله" : "خیر"; });
            add("نسخهٔ .NET",     delegate { return Environment.Version.ToString(); });
            add("نسخهٔ SQLite",   delegate { return SafeScalarText("SELECT sqlite_version();"); });
            add("معماری پردازنده", delegate { return Environment.Is64BitProcess ? "x64 (فرایند)" : "x86 (فرایند)"; });
            add("تعداد هسته",      delegate { return Environment.ProcessorCount.ToString(); });
            add("نام رایانه",      delegate { return Environment.MachineName; });
            add("کاربر ویندوز",    delegate { return Environment.UserName; });
            add("مصرف حافظه",      delegate { return FormatBytes(GetWorkingSet()); });
            add("مدت اجرای برنامه", FormatUptime);
            add("مسیر برنامه",     delegate { return AppDomain.CurrentDomain.BaseDirectory; });

            add("ماژول‌های فعال", delegate
            {
                if (!TableExists("EntModule")) return NotAvailable;
                return GetInstalledModules().Rows.Count.ToString();
            });
            add("اسمبلی‌های بارگذاری‌شده",
                delegate { return AppDomain.CurrentDomain.GetAssemblies().Length.ToString(); });

            // فرم‌های باز
            // آموزش — Application.OpenForms فقط روی نخِ رابط کاربری تضمین‌شده
            // است. GetDiagnostics از مسیر «بستهٔ پشتیبانی» روی نخ پس‌زمینه هم
            // اجرا می‌شود، پس این بخش محافظت می‌شود تا یک شمارشِ ناهم‌زمان کل
            // گزارش را از کار نیندازد.
            var openForms = new List<string>();
            cancel.ThrowIfCancellationRequested();
            Report(progress, ++step, totalDiagnostics, "فرم‌های باز");
            try
            {
                foreach (Form f in Application.OpenForms) openForms.Add(f.Name);
                t.Rows.Add("فرم‌های باز (" + openForms.Count + ")",
                           openForms.Count == 0 ? "—" : string.Join("، ", openForms));
            }
            catch
            {
                t.Rows.Add("فرم‌های باز", "در دسترس نیست (خارج از نخ رابط کاربری)");
            }

            // قفل‌های فعال — سرویس موجود
            add("قفل‌های فعال رکورد", delegate
            {
                if (!TableExists("EntRecordLock")) return NotAvailable;
                return Enterprise.LockService.GetActiveLocks().Rows.Count.ToString();
            });

            // ─── وضعیت همگام‌سازی (فاز ۳) ────────────────────────────────────
            // آموزش — این ردیف‌ها از همان مسیرِ محافظت‌شدهٔ بقیه عبور می‌کنند،
            // پس روی پایگاه‌دادهٔ قدیمی که جدول‌های Sync* را ندارد «در دسترس
            // نیست» نشان می‌دهند و بقیهٔ گزارش سالم می‌ماند.
            Sync.SyncService.SyncHealth syncHealth = null;
            add("همگام‌سازی — وضعیت", delegate
            {
                syncHealth = Sync.SyncService.GetHealth(null);
                return syncHealth.Configured
                    ? "فعال — " + syncHealth.ConnectionText
                    : "غیرفعال (بدون سرور) — " + syncHealth.ConnectionText;
            });

            add("همگام‌سازی — آخرین اجرا", delegate
            {
                if (syncHealth == null) return NotAvailable;
                return string.IsNullOrWhiteSpace(syncHealth.LastSyncAt)
                    ? "تاکنون انجام نشده" : syncHealth.LastSyncAt;
            });

            add("همگام‌سازی — در انتظار ارسال", delegate
            {
                return syncHealth == null ? NotAvailable : syncHealth.Pending.ToString("N0");
            });

            add("همگام‌سازی — ناموفق", delegate
            {
                return syncHealth == null ? NotAvailable : syncHealth.Failed.ToString("N0");
            });

            add("همگام‌سازی — تعارض بازبینی‌نشده", delegate
            {
                return syncHealth == null ? NotAvailable : syncHealth.Conflicts.ToString("N0");
            });

            add("همگام‌سازی — سلامت", delegate
            {
                if (syncHealth == null) return NotAvailable;

                // امتیاز منفی ⇒ سنجش بی‌معناست (سروری وجود ندارد).
                string text = syncHealth.Score < 0
                    ? NotAvailable
                    : syncHealth.Score + " / 100";

                return syncHealth.Notes.Count == 0
                    ? text
                    : text + " — " + string.Join(" • ", syncHealth.Notes.ToArray());
            });

            // ─── وضعیت همگام‌سازی فایل‌ها (فاز ۷) ─────────────────────────────
            Sync.SyncFileService.FileHealth fileHealth = null;
            add("پیوست‌ها — وضعیت", delegate
            {
                // ⚠ فاز B — با انتقالِ *واقعی* سنجیده می‌شود، وگرنه امتیاز
                // همیشه طوری حساب می‌شد که انگار هیچ سروری وجود ندارد و
                // انتقال‌های ناموفق هرگز در سلامت دیده نمی‌شدند.
                fileHealth = Sync.SyncFileService.GetHealth(new Sync.HttpFileSyncTransport());
                return fileHealth.Total.ToString("N0") + " فایل ردیابی‌شده";
            });

            add("پیوست‌ها — در انتظار ارسال", delegate
            {
                return fileHealth == null ? NotAvailable : fileHealth.Pending.ToString("N0");
            });

            add("پیوست‌ها — ارسال ناموفق", delegate
            {
                return fileHealth == null ? NotAvailable : fileHealth.Failed.ToString("N0");
            });

            add("پیوست‌ها — فایل گمشده", delegate
            {
                return fileHealth == null ? NotAvailable : fileHealth.Missing.ToString("N0");
            });

            add("پیوست‌ها — آخرین ارسال", delegate
            {
                if (fileHealth == null) return NotAvailable;
                return string.IsNullOrWhiteSpace(fileHealth.LastSyncAt)
                    ? "تاکنون انجام نشده" : fileHealth.LastSyncAt;
            });

            add("پیوست‌ها — فضای ذخیره‌سازی", delegate
            {
                string root = Helpers.FileHelper.GetBaseRootFolder();
                if (string.IsNullOrWhiteSpace(root)) return NotAvailable;
                return Directory.Exists(root) ? "موجود — " + root : "پیدا نشد — " + root;
            });

            add("پیوست‌ها — سلامت", delegate
            {
                if (fileHealth == null) return NotAvailable;

                string text = fileHealth.Score < 0 ? NotAvailable : fileHealth.Score + " / 100";
                return fileHealth.Notes.Count == 0
                    ? text
                    : text + " — " + string.Join(" • ", fileHealth.Notes.ToArray());
            });

            // ─── وضعیت همگام‌سازی فایل (فاز B) ───────────────────────────────
            // آموزش — این بخش عمداً *کنارِ* ردیف‌های موجود می‌نشیند و هیچ‌کدام
            // را عوض نمی‌کند: همان الگوی محافظت‌شده، پس روی پایگاه‌دادهٔ قدیمی
            // که جدول SyncFileDownload را ندارد «در دسترس نیست» نشان می‌دهد.
            Sync.BackgroundSyncManager.SyncStatus bgStatus = null;

            add("همگام‌سازی فایل — اتصال سرور", delegate
            {
                bgStatus = Sync.BackgroundSyncManager.GetStatus();

                var transport = new Sync.HttpSyncTransport();
                if (!transport.IsConfigured) return "پیکربندی نشده — کار آفلاین";

                return transport.GetStatus(System.Threading.CancellationToken.None).DisplayText;
            });

            add("همگام‌سازی فایل — سرویس پس‌زمینه", delegate
            {
                if (bgStatus == null) return NotAvailable;

                if (!bgStatus.Started) return "اجرا نشده";

                string text = bgStatus.Running
                    ? "در حال اجرا" + (string.IsNullOrWhiteSpace(bgStatus.CurrentPhase)
                                        ? "" : " — " + bgStatus.CurrentPhase)
                    : "بیکار";

                return text + " | خودکار: " + (bgStatus.AutoEnabled ? "روشن" : "خاموش")
                            + " | بازه: هر " + bgStatus.IntervalMinutes + " دقیقه";
            });

            add("همگام‌سازی فایل — آخرین موفق", delegate
            {
                if (bgStatus == null) return NotAvailable;
                return string.IsNullOrWhiteSpace(bgStatus.LastSuccessAt)
                    ? "تاکنون انجام نشده" : bgStatus.LastSuccessAt;
            });

            add("همگام‌سازی فایل — آخرین تلاش", delegate
            {
                if (bgStatus == null) return NotAvailable;

                if (string.IsNullOrWhiteSpace(bgStatus.LastAttemptAt)) return "تاکنون انجام نشده";

                return string.IsNullOrWhiteSpace(bgStatus.LastResult)
                    ? bgStatus.LastAttemptAt
                    : bgStatus.LastAttemptAt + " — " + bgStatus.LastResult;
            });

            // ⚠ جدا از «آخرین تلاش» است: یک اجرای موفق نباید ردِ شکستِ قبلی
            // را پاک کند، وگرنه الگوی شکست‌های متناوب هرگز دیده نمی‌شود.
            add("همگام‌سازی فایل — آخرین ناموفق", delegate
            {
                if (bgStatus == null) return NotAvailable;

                if (string.IsNullOrWhiteSpace(bgStatus.LastFailureAt)) return "تاکنون ناموفقی ثبت نشده";

                return string.IsNullOrWhiteSpace(bgStatus.LastFailure)
                    ? bgStatus.LastFailureAt
                    : bgStatus.LastFailureAt + " — " + bgStatus.LastFailure;
            });

            add("همگام‌سازی فایل — در انتظار ارسال", delegate
            {
                return fileHealth == null ? NotAvailable : fileHealth.Pending.ToString("N0");
            });

            add("همگام‌سازی فایل — در انتظار دریافت", delegate
            {
                return fileHealth == null ? NotAvailable : fileHealth.PendingDownloads.ToString("N0");
            });

            add("همگام‌سازی فایل — انتقال‌های ناموفق", delegate
            {
                if (fileHealth == null) return NotAvailable;

                return "ارسال: " + fileHealth.Failed.ToString("N0")
                     + " | دریافت: " + fileHealth.FailedDownloads.ToString("N0");
            });

            add("همگام‌سازی فایل — مجموع همگام‌شده", delegate
            {
                if (fileHealth == null) return NotAvailable;

                int uploaded = fileHealth.Total - fileHealth.Pending - fileHealth.Failed
                             - fileHealth.Missing - fileHealth.Rejected;
                if (uploaded < 0) uploaded = 0;

                return "ارسال‌شده: " + uploaded.ToString("N0")
                     + " | دریافت‌شده: " + fileHealth.Downloaded.ToString("N0")
                     + " | مجموع ردیابی‌شده: " + fileHealth.Total.ToString("N0");
            });

            add("همگام‌سازی فایل — سلامت ذخیره‌سازی", delegate
            {
                string root = Helpers.FileHelper.GetBaseRootFolder();
                if (string.IsNullOrWhiteSpace(root)) return "ریشهٔ ذخیره‌سازی تعیین نشده";

                if (!Directory.Exists(root)) return "پیدا نشد — " + root;

                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root)));
                    if (!drive.IsReady) return "موجود — " + root;

                    return "موجود | فضای آزاد: " + FormatBytes(drive.AvailableFreeSpace)
                         + " از " + FormatBytes(drive.TotalSize);
                }
                catch { return "موجود — " + root; }
            });

            add("همگام‌سازی فایل — امتیاز سلامت", delegate
            {
                if (fileHealth == null) return NotAvailable;

                // ⚠ امتیاز منفی یعنی سنجش بی‌معناست، نه «صفر».
                string text = fileHealth.Score < 0 ? NotAvailable : fileHealth.Score + " / 100";

                return fileHealth.Notes.Count == 0
                    ? text
                    : text + " — " + string.Join(" • ", fileHealth.Notes.ToArray());
            });

            // کارهای پس‌زمینه: در این معماری فقط بکاپ خودکار روزانه است.
            add("کار پس‌زمینه — بکاپ خودکار", delegate
            {
                string lastBackup = SettingsHelper.Get(SettingsHelper.LastBackupDate);
                return string.IsNullOrWhiteSpace(lastBackup)
                    ? "تاکنون اجرا نشده" : "آخرین اجرا: " + lastBackup;
            });

            return t;
        }

        public static DataTable GetLoadedPlugins()
        {
            DataTable t = NewTable("نام", "نسخه", "مسیر");
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string location;
                try { location = asm.IsDynamic ? "(پویا)" : asm.Location; }
                catch { location = "(نامشخص)"; }

                AssemblyName name = asm.GetName();
                t.Rows.Add(name.Name, name.Version == null ? "" : name.Version.ToString(), location);
            }
            return t;
        }

        public static DataTable GetInstalledModules()
        {
            return GuardedLog("EntModule", delegate { return Enterprise.ModuleService.GetModules(); });
        }

        public static DataTable GetActiveLocks()
        {
            return GuardedLog("EntRecordLock", delegate { return Enterprise.LockService.GetActiveLocks(); });
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۷) کاوشگر دیتابیس (فقط خواندنی)
        // ═════════════════════════════════════════════════════════════════════
        public static List<string> GetTableNames()
        {
            var names = new List<string>();
            DataTable t = Db.Query(
                "SELECT name FROM sqlite_master WHERE type = 'table' " +
                "AND name NOT LIKE 'sqlite_%' ORDER BY name;");
            foreach (DataRow row in t.Rows) names.Add(Convert.ToString(row["name"]));
            return names;
        }

        // ⚠ فقط خواندنی: نامِ جدول از فهرست واقعیِ sqlite_master اعتبارسنجی
        // می‌شود (جلوگیری از تزریق)، و هیچ مسیرِ نوشتنی در این متد وجود ندارد.
        public static DataTable BrowseTable(string tableName, string searchText, int limit)
        {
            if (!GetTableNames().Contains(tableName))
                throw new ArgumentException("جدول نامعتبر است.");

            var columns = GetColumnNames(tableName);

            // ⚠ امنیت — افشای اعتبارنامه: کاوشگر «هر جدولی» را نشان می‌دهد،
            // از جمله TblUsers با ستون‌های PasswordHash/PasswordSalt. تا پیش از
            // این، هشِ رمزِ همهٔ کاربران روی صفحه دیده می‌شد و با «خروجی CSV»
            // از سیستم خارج می‌شد — در حالی که همین ماژول در «بستهٔ پشتیبانی»
            // دقیقاً همین کلیدها را پنهان می‌کند. حالا هر دو مسیر یک سیاست
            // دارند و مقدارِ حساس *اصلاً از SQLite بیرون نمی‌آید*.
            var safeColumns = columns.Where(c => !IsSensitiveKey(c)).ToList();

            string projection = columns.Count == 0 ? "*" : string.Join(", ", columns.Select(c =>
                IsSensitiveKey(c) ? "'" + RedactedValue + "' AS [" + c + "]" : "[" + c + "]"));

            string sql = "SELECT " + projection + " FROM [" + tableName + "]";
            var parameters = new List<SQLiteParameter>();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                // جست‌وجوی متنی روی همهٔ ستون‌ها، بدون نیاز به دانستن نوعشان.
                // ستون‌های حساس از جست‌وجو هم کنار گذاشته می‌شوند، وگرنه
                // می‌شد با آزمون‌وخطا وجود یک مقدارِ پنهان‌شده را تأیید کرد.
                if (safeColumns.Count > 0)
                {
                    var conditions = safeColumns.Select(c =>
                        "IFNULL(CAST([" + c + "] AS TEXT), '') LIKE @Search");
                    sql += " WHERE " + string.Join(" OR ", conditions);
                    parameters.Add(new SQLiteParameter("@Search", "%" + searchText.Trim() + "%"));
                }
            }

            sql += " LIMIT " + Math.Max(1, limit) + ";";
            return Db.Query(sql, parameters.ToArray());
        }

        private static List<string> GetColumnNames(string tableName)
        {
            var columns = new List<string>();
            DataTable t = Db.Query("PRAGMA table_info([" + tableName + "]);");
            foreach (DataRow row in t.Rows) columns.Add(Convert.ToString(row["name"]));
            return columns;
        }

        public static DataTable GetTableRowCounts()
        {
            DataTable t = NewTable("جدول", "تعداد رکورد");
            foreach (string name in GetTableNames())
                t.Rows.Add(name, SafeScalarInt("SELECT COUNT(1) FROM [" + name + "];").ToString("N0"));
            return t;
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۸) ابزار توسعه‌دهنده
        // ═════════════════════════════════════════════════════════════════════
        public const string DebugModeKey = "DevDebugMode";

        public static bool IsDebugMode()
        {
            return SettingsHelper.GetInt(DebugModeKey, 0) == 1;
        }

        public static string SetDebugMode(bool enabled)
        {
            SettingsHelper.Set(DebugModeKey, enabled ? "1" : "0");
            return enabled ? "حالت اشکال‌زدایی فعال شد." : "حالت اشکال‌زدایی غیرفعال شد.";
        }

        public static string ReloadConfiguration()
        {
            SettingsHelper.ClearCache();
            return "پیکربندی از دیتابیس دوباره خوانده شد.";
        }

        public static string ReloadPermissions()
        {
            Enterprise.PermissionService.InvalidateCache();
            Enterprise.ModuleService.InvalidateCache();
            return "کش مجوزها و ماژول‌ها پاک شد؛ در اولین استفاده دوباره خوانده می‌شود.";
        }

        public static string ReloadLookups()
        {
            LookupHelper.ClearCache();
            return "کش جدول‌های مرجع (Lookup) پاک شد.";
        }

        public static string TestNotifications()
        {
            // آموزش — «تست اعلان» یعنی همان مسیر واقعیِ اعلان اجرا شود، نه یک
            // پیام ساختگی؛ پس یک رویداد در لاگ امنیتی ثبت و نتیجه گزارش می‌شود.
            LogAction("تست اعلان‌ها");
            return "اعلان آزمایشی ثبت شد. برای دیدن آن به تب «اعلان‌ها» در داشبورد مراجعه کنید.";
        }

        public static string TestEmail()
        {
            string email = SettingsHelper.Get(SettingsHelper.Email);
            return string.IsNullOrWhiteSpace(email)
                ? "ایمیل در تنظیمات پیکربندی نشده است — تست انجام نشد."
                : "پیکربندی ایمیل موجود است (" + email + ")، اما سرویس ارسال ایمیل در این نسخه فعال نیست.";
        }

        public static string TestSms()
        {
            string mobile = SettingsHelper.Get(SettingsHelper.Mobile);
            return string.IsNullOrWhiteSpace(mobile)
                ? "شمارهٔ پیامک در تنظیمات پیکربندی نشده است — تست انجام نشد."
                : "پیکربندی پیامک موجود است (" + mobile + ")، اما سرویس ارسال پیامک در این نسخه فعال نیست.";
        }

        // ═════════════════════════════════════════════════════════════════════
        // خروجی CSV مشترک همهٔ تب‌ها
        // ═════════════════════════════════════════════════════════════════════
        public static void ExportToCsv(DataTable table, string path)
        {
            // UTF-8 با BOM تا اکسل متن فارسی را درست باز کند.
            File.WriteAllText(path, ToCsv(table), new UTF8Encoding(true));
        }

        private static string ToCsv(DataTable table)
        {
            if (table == null) return "";

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => CsvCell(c.ColumnName))));
            foreach (DataRow row in table.Rows)
                sb.AppendLine(string.Join(",", row.ItemArray.Select(v => CsvCell(Convert.ToString(v)))));
            return sb.ToString();
        }

        private static string CsvCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            // ⚠ امنیت — تزریق فرمول (CSV Injection): اکسل هر سلولی را که با
            // = + - @ یا Tab شروع شود «فرمول» می‌بیند و اجرا می‌کند. متنِ این
            // خروجی‌ها از دادهٔ واقعیِ کاربران و پیام‌های خطا می‌آید، یعنی
            // مهاجم می‌تواند با یک نامِ آلوده روی رایانهٔ کسی که فایل را باز
            // می‌کند فرمان اجرا کند. یک آپاستروفِ ابتدایی، اکسل را وادار
            // می‌کند همان را «متن» بخواند.
            if ("=+-@\t\r".IndexOf(value[0]) >= 0)
                value = "'" + value;

            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // ═════════════════════════════════════════════════════════════════════
        // کمکی‌های داخلی
        // ═════════════════════════════════════════════════════════════════════
        private static DataTable NewTable(params string[] columns)
        {
            var t = new DataTable();
            foreach (string c in columns) t.Columns.Add(c);
            return t;
        }

        private static void RunSql(string sql)
        {
            RunSql(sql, System.Threading.CancellationToken.None);
        }

        // آموزش — لغوِ یک دستورِ اتمیکِ SQLite: VACUUM/REINDEX/ANALYZE یک
        // دستورِ واحدند و نمی‌توان بینشان «مرحله» گذاشت. تنها راهِ درستِ لغو،
        // SQLiteCommand.Cancel() است که به موتور SQLite وقفه (interrupt)
        // می‌دهد؛ خودِ SQLite تراکنش را امن برمی‌گرداند و فایل سالم می‌ماند.
        // با using، اتصال در هر مسیری — موفق، خطا یا لغو — بسته می‌شود.
        private static void RunSql(string sql, System.Threading.CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            using (SQLiteConnection con = Db.GetConnection())
            {
                con.Open();

                // VACUUM/REINDEX/ANALYZE به قفلِ انحصاریِ فایل نیاز دارند. بدون
                // busy_timeout، اگر در همان لحظه کاربر دیگری در حال نوشتن باشد
                // بلافاصله «database is locked» می‌گرفتیم؛ با آن، تا این مدت
                // صبر و دوباره تلاش می‌شود. همان ثابتِ موجودِ DAL بازاستفاده
                // می‌شود تا رفتار با بقیهٔ مسیرهای نوشتن یکسان بماند.
                using (var pragma = new SQLiteCommand(
                    "PRAGMA busy_timeout=" + DatabaseHelper.WriteBusyTimeoutMs + ";", con))
                    pragma.ExecuteNonQuery();

                using (var cmd = new SQLiteCommand(sql, con))
                using (cancel.Register(delegate { try { cmd.Cancel(); } catch { } }))
                {
                    try { cmd.ExecuteNonQuery(); }
                    catch (SQLiteException) when (cancel.IsCancellationRequested)
                    {
                        // وقفهٔ عمدی، نه خرابی: به‌صورت لغو گزارش می‌شود.
                        throw new OperationCanceledException(cancel);
                    }
                }
            }

            // عملیات نگهداری می‌تواند اسکیما را تغییر دهد؛ کش باطل می‌شود تا
            // بررسی‌های بعدی از وضعیت واقعی بخوانند.
            ResetSchemaCache();
        }

        private static string SafeScalarText(string sql)
        {
            try
            {
                using (SQLiteConnection con = Db.GetConnection())
                using (var cmd = new SQLiteCommand(sql, con))
                {
                    con.Open();
                    return Convert.ToString(cmd.ExecuteScalar());
                }
            }
            catch { return ""; }
        }

        private static int SafeScalarInt(string sql)
        {
            try
            {
                object value = Db.ExecuteScalar(sql);
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
            catch { return 0; }
        }

        // ⚠ اگر شمارشِ حتی یک جدول شکست بخورد، «جمعِ ناقص» برگردانده نمی‌شود.
        // یک عددِ کمتر از واقعیت بدتر از «در دسترس نیست» است، چون کاربر روی آن
        // تصمیم می‌گیرد (مثلاً فکر می‌کند داده‌ای از دست رفته).
        private static long CountAllRecords()
        {
            long total = 0;
            foreach (string name in GetTableNames())
            {
                int count; string error;
                if (!TryScalarInt("SELECT COUNT(1) FROM [" + name + "];", out count, out error))
                    return -1;
                total += count;
            }
            return total;
        }

        // فقط منابعی شمرده می‌شوند که ستونشان واقعاً وجود دارد.
        private static int CountMissingFiles()
        {
            int missing = 0;
            if (ColumnExists("TblCase", "PhotoPath"))
                missing += CountMissingPaths("SELECT PhotoPath AS P FROM TblCase WHERE NULLIF(PhotoPath,'') IS NOT NULL");
            if (ColumnExists("TblDocs", "DocFilePath"))
                missing += CountMissingPaths("SELECT DocFilePath AS P FROM TblDocs WHERE NULLIF(DocFilePath,'') IS NOT NULL");
            return missing;
        }

        private static int CountMissingPaths(string sql)
        {
            return CountMissingPaths(sql, null, System.Threading.CancellationToken.None, null);
        }

        private static int CountMissingPaths(string sql, IProgress<DevProgress> progress,
            System.Threading.CancellationToken cancel, string stepText)
        {
            int missing = 0;
            try
            {
                DataTable t = Db.Query(sql);
                int index = 0;
                foreach (DataRow row in t.Rows)
                {
                    cancel.ThrowIfCancellationRequested();

                    index++;
                    if (progress != null && (index % 50 == 0 || index == t.Rows.Count))
                        Report(progress, index, t.Rows.Count, stepText);

                    string path = Convert.ToString(row["P"]);
                    if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path)) missing++;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
            return missing;
        }

        private static string GetAppVersion()
        {
            try { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
            catch { return "نامشخص"; }
        }

        private static string GetDbVersion()
        {
            string userVersion = SafeScalarText("PRAGMA user_version;");
            string schema = SafeScalarText("PRAGMA schema_version;");
            return "user_version=" + (string.IsNullOrEmpty(userVersion) ? "0" : userVersion)
                 + " | schema_version=" + (string.IsNullOrEmpty(schema) ? "0" : schema);
        }

        // مسیر دیتابیس از *همان رشتهٔ اتصالی* خوانده می‌شود که برنامه با آن کار
        // می‌کند، نه از نامِ حدس‌زده‌شدهٔ «CaseDB.sqlite». اگر دیتابیس جابه‌جا
        // شده باشد، نسخهٔ قبلی حجمِ «۰ بایت» گزارش می‌کرد و VACUUM هم اندازهٔ
        // قبل/بعدِ بی‌معنی نشان می‌داد — یعنی یک عددِ غلط، نه «در دسترس نیست».
        private static string GetDbFilePath()
        {
            try
            {
                ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["CaseDb"];
                if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                    return "";

                string source = new SQLiteConnectionStringBuilder(settings.ConnectionString).DataSource;
                if (string.IsNullOrWhiteSpace(source)) return "";

                if (source.IndexOf("|DataDirectory|", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string dataDir = AppDomain.CurrentDomain.GetData("DataDirectory") as string;
                    if (string.IsNullOrEmpty(dataDir)) dataDir = AppDomain.CurrentDomain.BaseDirectory;
                    source = source.Replace("|DataDirectory|",
                        dataDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                }

                return Path.GetFullPath(source);
            }
            catch { return ""; }
        }

        private static long GetDbFileSize()
        {
            try
            {
                string path = GetDbFilePath();
                return File.Exists(path) ? new FileInfo(path).Length : 0;
            }
            catch { return 0; }
        }

        private static long GetWorkingSet()
        {
            try { return Process.GetCurrentProcess().WorkingSet64; }
            catch { return 0; }
        }

        private static string FormatUptime()
        {
            try
            {
                TimeSpan up = DateTime.Now - Process.GetCurrentProcess().StartTime;
                return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                                     (int)up.TotalHours, up.Minutes, up.Seconds);
            }
            catch { return "نامشخص"; }
        }

        private static List<KeyValuePair<string, string>> GetStoragePaths()
        {
            var list = new List<KeyValuePair<string, string>>();
            Action<string, string> add = delegate (string label, string key)
            {
                string value = SettingsHelper.Get(key);
                if (!string.IsNullOrWhiteSpace(value))
                    list.Add(new KeyValuePair<string, string>(label, value));
            };

            add("مسیر عکس‌ها", SettingsHelper.PhotoStoragePath);
            add("مسیر بکاپ",   SettingsHelper.BackupPath);
            add("مسیر گزارش‌ها", SettingsHelper.ReportsPath);
            add("مسیر لاگ‌ها",  SettingsHelper.LogsPath);
            add("مسیر موقت",   SettingsHelper.TempPath);
            return list;
        }

        private static string GetStorageUsage()
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory));
                long used = drive.TotalSize - drive.AvailableFreeSpace;
                int percent = drive.TotalSize > 0 ? (int)(used * 100 / drive.TotalSize) : 0;
                return FormatBytes(used) + " از " + FormatBytes(drive.TotalSize) + " (" + percent + "٪)";
            }
            catch { return "نامشخص"; }
        }

        // ═════════════════════════════════════════════════════════════════════
        // سنجه‌های «گزارش سلامت» — همگی روی همان محافظ‌های موجود سوارند تا روی
        // پایگاه‌دادهٔ ناقص هم استثنا ندهند و «اندازه‌گیری‌نشده» را با «صفر»
        // اشتباه نگیرند (مقدار منفی ⇒ در دسترس نیست).
        // ═════════════════════════════════════════════════════════════════════
        internal static int CountFailedLogins(int days)
        {
            if (!ColumnExists("EntSecurityEvent", "Success") ||
                !ColumnExists("EntSecurityEvent", "CreatedAt"))
                return 0;

            int value; string error;
            return TryScalarInt(
                "SELECT COUNT(1) FROM EntSecurityEvent WHERE Success = 0 " +
                "AND CreatedAt >= datetime('now', '-" + Math.Max(1, days) + " days');",
                out value, out error) ? value : 0;
        }

        internal static int UnresolvedErrorCount()
        {
            if (!TableExists("EntErrorLog")) return 0;

            int count;
            return TryGuarded(delegate { return Enterprise.ErrorLogger.UnresolvedCount(); }, out count)
                ? count : 0;
        }

        internal static int CountMissingStorageFolders()
        {
            try
            {
                int missing = 0;
                foreach (var pair in GetStoragePaths())
                    if (!Directory.Exists(pair.Value)) missing++;
                return missing;
            }
            catch { return -1; }
        }

        // درصد فضای *آزاد* درایو برنامه؛ منفی ⇒ قابل اندازه‌گیری نبود.
        internal static int GetFreeDiskPercent()
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory));
                if (drive.TotalSize <= 0) return -1;
                return (int)(drive.AvailableFreeSpace * 100 / drive.TotalSize);
            }
            catch { return -1; }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 بایت";
            string[] units = { "بایت", "کیلوبایت", "مگابایت", "گیگابایت", "ترابایت" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return value.ToString(unit == 0 ? "N0" : "N1", CultureInfo.InvariantCulture) + " " + units[unit];
        }
    }
}

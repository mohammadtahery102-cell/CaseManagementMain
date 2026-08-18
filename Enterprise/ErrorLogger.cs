using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ─────────────────────────────────────────────────────────────────────────
    // ویژگی ۸ — ثبت متمرکز خطاها.
    //
    // همه خطاها در یک جدول واحد ثبت می‌شوند تا پشتیبانی بتواند مشکل گزارش‌شده
    // کاربر را بدون حدس‌زدن بازسازی کند.
    //
    // دو لایه اطمینان:
    //  ۱. ثبت در دیتابیس (EntErrorLog).
    //  ۲. اگر خودِ دیتابیس مشکل داشته باشد، نوشتن در فایل متنی — دقیقاً همان
    //     الگوی محافظه‌کارانه‌ای که AuditLogger.TryWriteErrorLog دارد.
    //
    // این کلاس هیچ‌وقت استثنا پرتاب نمی‌کند؛ ثبت خطا نباید خودش خطای تازه بسازد.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ErrorLogger
    {
        public const string SeverityError    = "خطا";
        public const string SeverityWarning  = "هشدار";
        public const string SeverityCritical = "بحرانی";

        // فایل پشتیبان وقتی ثبت در دیتابیس ممکن نیست.
        private static readonly string FallbackLogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");

        private static bool _installed;

        // ─── نصب گیرنده‌های سراسری خطا ────────────────────────────────────
        // آموزش: بدون این‌ها، هر استثنای گرفته‌نشده در یک رویداد UI باعث بسته
        // شدن ناگهانی برنامه می‌شود و هیچ ردی باقی نمی‌ماند. با نصب این دو
        // گیرنده، خطا ثبت و به کاربر پیام مناسب فارسی داده می‌شود.
        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            Application.ThreadException += delegate (object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                Log(e.Exception, "Application.ThreadException", null, SeverityCritical);
                ShowFriendlyMessage(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
            {
                Log(e.ExceptionObject as Exception, "AppDomain.UnhandledException", null, SeverityCritical);
            };

            // خطاهای گرفته‌نشده در رویدادهای UI به‌جای بستن برنامه، به
            // ThreadException بالا هدایت می‌شوند.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        }

        // ─── ثبت ──────────────────────────────────────────────────────────
        public static long Log(Exception ex, string source, string formName = null,
                               string severity = SeverityError)
        {
            if (ex == null) return 0;

            return Write(source, formName, severity,
                         ex.GetType().FullName, ex.Message, ex.ToString());
        }

        public static long LogMessage(string message, string source,
                                      string formName = null, string severity = SeverityWarning)
        {
            return Write(source, formName, severity, null, message, null);
        }

        // ثبت خطا + نمایش پیام به کاربر، در یک فراخوانی.
        // برای استفاده در بلوک‌های catch فرم‌ها: رفتار قبلی (نمایش پیام) حفظ
        // می‌شود و فقط ثبت به آن اضافه می‌گردد.
        public static void Handle(Exception ex, string source, string userMessage = null)
        {
            Log(ex, source);

            string message = string.IsNullOrWhiteSpace(userMessage)
                ? "خطا: " + (ex == null ? "" : ex.Message)
                : userMessage + " " + (ex == null ? "" : ex.Message);

            Msg.Show(message);
        }

        private static long Write(string source, string formName, string severity,
                                  string exceptionType, string message, string stackTrace)
        {
            try
            {
                return EntDb.Insert(@"
INSERT INTO EntErrorLog
    (Source, FormName, Severity, ExceptionType, Message, StackTrace,
     UserID, Username, MachineName, CenterID)
VALUES
    (@Source, @Form, @Severity, @Type, @Message, @Stack,
     @UserId, @User, @Machine, @Center);",
                    "@Source",   source,
                    "@Form",     formName,
                    "@Severity", string.IsNullOrWhiteSpace(severity) ? SeverityError : severity,
                    "@Type",     exceptionType,
                    "@Message",  Trim(message, 4000),
                    "@Stack",    Trim(stackTrace, 8000),
                    "@UserId",   SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : null,
                    "@User",     SecurityContext.Username,
                    "@Machine",  SafeMachineName(),
                    "@Center",   SecurityContext.CurrentCenterId > 0
                                     ? (object)SecurityContext.CurrentCenterId : null);
            }
            catch (Exception loggingError)
            {
                // دیتابیس در دسترس نیست → فایل متنی، تا خطا کاملاً گم نشود.
                string text = string.Format(
                    "[{0:yyyy-MM-dd HH:mm:ss}] {1} | {2} | {3} | (ثبت در دیتابیس ناموفق: {4})",
                    DateTime.Now, source, exceptionType, message, loggingError.Message);

                Debug.WriteLine(text);
                TryWriteFallback(text + Environment.NewLine + stackTrace);
                return 0;
            }
        }

        private static void ShowFriendlyMessage(Exception ex)
        {
            try
            {
                Msg.Show(
                    "خطای پیش‌بینی‌نشده‌ای رخ داد. جزئیات آن برای پشتیبانی ثبت شد." +
                    Environment.NewLine + Environment.NewLine +
                    (ex == null ? "" : ex.Message),
                    "خطا");
            }
            catch
            {
                // اگر حتی نمایش پیام هم ممکن نبود، کاری از دست ما برنمی‌آید.
            }
        }

        private static void TryWriteFallback(string text)
        {
            try { File.AppendAllText(FallbackLogPath, text + Environment.NewLine); }
            catch { }
        }

        private static string Trim(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "…";
        }

        private static string SafeMachineName()
        {
            try { return Environment.MachineName; }
            catch { return ""; }
        }

        // ─── خواندن / مدیریت ──────────────────────────────────────────────
        public static DataTable GetErrors(string severity, bool unresolvedOnly, int lastDays)
        {
            return EntDb.Query(@"
SELECT ErrorID       AS 'شناسه',
       CreatedAt     AS 'تاریخ',
       Severity      AS 'اهمیت',
       Source        AS 'منبع',
       FormName      AS 'فرم',
       ExceptionType AS 'نوع خطا',
       Message       AS 'پیام',
       Username      AS 'کاربر',
       MachineName   AS 'رایانه',
       CASE IsResolved WHEN 1 THEN 'بررسی شد' ELSE 'باز' END AS 'وضعیت',
       ResolvedBy    AS 'بررسی‌کننده',
       Note          AS 'یادداشت'
FROM   EntErrorLog
WHERE  (IFNULL(@Severity, '') = '' OR Severity = @Severity)
  AND  (@UnresolvedOnly = 0 OR IsResolved = 0)
  AND  (@Days <= 0 OR CreatedAt >= datetime('now', '-' || @Days || ' days'))
  -- خطاها ممکن است پیش از انتخاب مرکز رخ دهند؛ چنین خطاهایی برای همه
  -- مدیران قابل مشاهده می‌مانند (همان منطق ممیزی امنیتی).
  AND  (@Center = 0 OR IFNULL(CenterID, 0) = 0 OR CenterID = @Center)
ORDER  BY ErrorID DESC
LIMIT  2000;",
                "@Severity",       severity,
                "@UnresolvedOnly", unresolvedOnly ? 1 : 0,
                "@Days",           lastDays,
                "@Center",         SecurityContext.CenterFilterId);
        }

        public static string GetStackTrace(int errorId)
        {
            return EntDb.ToText(EntDb.Scalar(
                "SELECT StackTrace FROM EntErrorLog WHERE ErrorID = @Id;", "@Id", errorId));
        }

        public static WorkflowActionResult MarkResolved(int errorId, string note)
        {
            if (!SecurityContext.IsAdmin())
                return WorkflowActionResult.Fail("علامت‌گذاری خطا فقط برای مدیر سیستم مجاز است.");

            EntDb.Exec(@"
UPDATE EntErrorLog
SET    IsResolved = 1, ResolvedBy = @By, ResolvedAt = datetime('now'), Note = @Note
WHERE  ErrorID = @Id;",
                "@By", SecurityContext.Username, "@Note", note, "@Id", errorId);

            return new WorkflowActionResult { Applied = true, Message = "خطا «بررسی شد» علامت خورد." };
        }

        public static int UnresolvedCount()
        {
            return EntDb.ToInt(EntDb.Scalar(
                "SELECT COUNT(*) FROM EntErrorLog WHERE IsResolved = 0;"));
        }

        // پاک‌سازی خطاهای قدیمیِ بررسی‌شده — جلوگیری از رشد بی‌پایان جدول.
        public static int PurgeOlderThan(int days)
        {
            if (days <= 0) return 0;

            if (!SecurityContext.IsAdmin()) return 0;

            return EntDb.Exec(@"
DELETE FROM EntErrorLog
WHERE  IsResolved = 1 AND CreatedAt < datetime('now', '-' || @Days || ' days');",
                "@Days", days);
        }
    }
}

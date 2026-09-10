using System;
using System.IO;
using DrawingImage = System.Drawing.Image;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // قاعدهٔ واحدِ «چه عکسی پذیرفته می‌شود».
    //
    // آموزش — چرا یک‌جا: تا امروز هر فرم عددِ خودش را داشت — عکس سرپرست تا
    // ۱۵ مگابایت بدونِ حداقل، عکس جمعی ۵۰ کیلوبایت تا ۱ مگابایت، عکس عضو
    // خانواده تا ۵ مگابایت. یعنی سه قاعدهٔ متفاوت برای یک نوع داده، و تغییرِ
    // سیاست یعنی سه جای پراکنده. حالا محدودیت‌ها اینجا تعریف می‌شوند و
    // فرم‌ها فقط صدایشان می‌زنند.
    //
    // تصمیمِ کاربر (۱۴۰۵/۰۶/۱۶): عکسِ *پرسنلیِ* سرپرست خانوار باید بین
    // ۵۰ کیلوبایت و ۵۰۰ کیلوبایت باشد. همان قاعده برای بقیهٔ عکس‌های پرسنلی
    // (سرپرست کودک، نمایندهٔ قانونی، عضو خانواده) هم به‌کار می‌رود چون از
    // یک جنس‌اند. عکسِ *جمعی* و عکسِ *بازدید میدانی* صحنه‌اند نه پرسنلی، پس
    // سقفشان یک مگابایت می‌ماند (همان مقدارِ قبلیِ عکس جمعی).
    // ═════════════════════════════════════════════════════════════════════════
    public static class PhotoRules
    {
        private const long KB = 1024L;

        // ── عکسِ پرسنلی ──────────────────────────────────────────────────────
        public const long MinPortraitBytes = 50 * KB;
        public const long MaxPortraitBytes = 500 * KB;

        // ── عکسِ جمعیِ خانواده ───────────────────────────────────────────────
        public const long MinGroupBytes = 50 * KB;
        public const long MaxGroupBytes = 1024 * KB;

        // ── عکسِ بازدید میدانی (صحنه، مثل عکس جمعی) ──────────────────────────
        public const long MinVisitPhotoBytes = 50 * KB;
        public const long MaxVisitPhotoBytes = 1024 * KB;

        public static string PortraitSizeMessage
        {
            get { return SizeMessage(MinPortraitBytes, MaxPortraitBytes); }
        }

        public static string GroupSizeMessage
        {
            get { return SizeMessage(MinGroupBytes, MaxGroupBytes); }
        }

        // پیامِ خطا از خودِ اعداد ساخته می‌شود تا اگر محدودیت عوض شد، متنِ
        // نمایش‌داده‌شده هرگز با قاعدهٔ واقعی نخواند.
        public static string SizeMessage(long minBytes, long maxBytes)
        {
            return "حجم عکس باید بین " + Describe(minBytes) + " و " + Describe(maxBytes) + " باشد";
        }

        private static string Describe(long bytes)
        {
            if (bytes >= 1024 * KB)
            {
                double mb = bytes / (double)(1024 * KB);
                return ReportDoc.Fa(mb.ToString(mb % 1 == 0 ? "0" : "0.#",
                    System.Globalization.CultureInfo.InvariantCulture)) + " مگابایت";
            }

            return ReportDoc.Fa((bytes / KB).ToString(
                System.Globalization.CultureInfo.InvariantCulture)) + " کیلوبایت";
        }

        // ── وارسی ────────────────────────────────────────────────────────────
        // پیامِ خطا برگردانده می‌شود و *نمایش* داده نمی‌شود: فراخوانندهٔ تک‌عکسی
        // آن را در یک دیالوگ نشان می‌دهد و فراخوانندهٔ دسته‌ای (عکس‌های بازدید)
        // همه را در یک فهرست جمع می‌کند — بدونِ این، کاربر ده پنجرهٔ خطای
        // پشت‌سرِ‌هم می‌گرفت.
        public static bool IsValidPhoto(string filePath, long minBytes, long maxBytes, out string reason)
        {
            reason = "";

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                reason = "فایل عکس پیدا نشد";
                return false;
            }

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                reason = "فقط فایل JPG، JPEG یا PNG مجاز است";
                return false;
            }

            long length;
            try { length = new FileInfo(filePath).Length; }
            catch { reason = "حجم فایل خوانده نشد"; return false; }

            if (length < minBytes || length > maxBytes)
            {
                reason = SizeMessage(minBytes, maxBytes);
                return false;
            }

            // خواندنِ واقعیِ تصویر — پسوندِ درست تضمین نمی‌کند محتوا سالم است.
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (DrawingImage.FromStream(fs, false, true))
                    return true;
            }
            catch
            {
                reason = "فایل انتخاب‌شده عکس معتبر نیست";
                return false;
            }
        }
    }
}

using System;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // وضعیت لایسنس نرم‌افزار.
    // آموزش — این enum فقط «وضعیت» را گزارش می‌کند؛ در این نسخه (زیرساخت)
    // هیچ‌کدام از حالت‌ها باعث غیرفعال شدن قابلیتی نمی‌شوند. نرم‌افزار در همه
    // حالت‌ها کامل کار می‌کند؛ صرفاً زیرساخت فعال‌سازی/انقضا آماده می‌شود تا
    // در آینده در صورت تصمیم مدیر، سیاست اعمال (enforcement) روی آن سوار شود.
    // ─────────────────────────────────────────────────────────────────────────
    public enum LicenseStatus
    {
        Trial,      // بدون لایسنس فعال — دوره آزمایشی (بدون محدودیت واقعی فعلاً)
        Active,     // لایسنس معتبر و در تاریخ
        Expired,    // لایسنس معتبر بود ولی تاریخش گذشته
        Invalid,    // توکن نامعتبر / امضای مخدوش / مربوط به دستگاه دیگر
        MachineMismatch // امضا درست است ولی برای این دستگاه صادر نشده
    }

    // مدل اطلاعات یک لایسنس فعال‌شده — نتیجه‌ی رمزگشایی و اعتبارسنجی توکن.
    public sealed class LicenseInfo
    {
        public string LicensedTo { get; set; }     // نام مؤسسه/مشتری
        public string Edition { get; set; }         // نسخه: Standard / Pro / Enterprise
        public DateTime IssuedAt { get; set; }      // تاریخ صدور
        public DateTime? ExpiresAt { get; set; }    // تاریخ انقضا (null = دائمی)
        public string HardwareId { get; set; }      // شناسه سخت‌افزار مجاز ("ANY" = هر دستگاه)
        public string Features { get; set; }        // فهرست ویژگی‌های فعال (با کاما جدا)

        public LicenseStatus Status { get; set; }   // وضعیت محاسبه‌شده هنگام اعتبارسنجی
        public string RawToken { get; set; }        // توکن اصلی (برای ذخیره)

        public bool IsPerpetual { get { return ExpiresAt == null; } }

        public int? DaysRemaining
        {
            get
            {
                if (ExpiresAt == null) return null;
                return (int)Math.Ceiling((ExpiresAt.Value.Date - DateTime.Today).TotalDays);
            }
        }

        // متن فارسی خوانا برای نمایش در «درباره برنامه» / تنظیمات.
        public string StatusDisplay
        {
            get
            {
                switch (Status)
                {
                    case LicenseStatus.Active:
                        return IsPerpetual
                            ? "فعال (دائمی)"
                            : "فعال — " + DaysRemaining + " روز باقی‌مانده";
                    case LicenseStatus.Expired:
                        return "منقضی‌شده";
                    case LicenseStatus.MachineMismatch:
                        return "نامعتبر برای این دستگاه";
                    case LicenseStatus.Invalid:
                        return "توکن نامعتبر";
                    default:
                        return "نسخه آزمایشی (بدون لایسنس)";
                }
            }
        }
    }
}

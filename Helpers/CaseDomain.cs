using System;
using System.Collections.Generic;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // منبعِ واحدِ حقیقت برای دسته‌های پایهٔ پرونده (Phase 1 — استانداردسازی).
    //
    // چرا این فایل ساخته شد: مقادیرِ «وضعیت خدمات»، «نوع پرونده»، «دلیل تعلیق»
    // و «نقش عضو» قبلاً در ۱۲ فایل به‌صورت رشتهٔ هاردکد تکرار شده بودند
    // (FrmCase.Designer، FrmDashboard، FrmFinance، FrmFamily.Designer، ...).
    // هر بار که یک مقدار عوض می‌شد، بعضی نقاط جا می‌ماندند و فیلترها/گزارش‌ها
    // بی‌صدا خالی برمی‌گشتند.
    //
    // این کلاس فقط «فهرستِ مرجع» است؛ در زمانِ اجرا همچنان LookupHelper از
    // TblLookup می‌خواند (تا مدیر بتواند از تنظیمات ویرایش کند). از این مقادیر
    // برای seed کردنِ TblLookup، ساختِ CHECK constraint، و مهاجرتِ مقادیرِ
    // قدیمی استفاده می‌شود.
    //
    // مهم: چهار دسته هرگز نباید با هم قاطی شوند. مثلاً «یتیم» هم در MemberRole
    // (نقش عضو) و هم قبلاً در RequestType (نوع پرونده) بود؛ به‌همین دلیل نوع
    // پرونده به «ایتام» تغییر کرد تا این دو هیچ‌وقت با هم اشتباه نشوند.
    // ═══════════════════════════════════════════════════════════════════════
    public static class CaseDomain
    {
        // ─── نام دسته‌ها در TblLookup.Category ──────────────────────────────
        public const string CatServiceStatus    = "ServiceStatus";
        public const string CatCaseType         = "RequestType";   // نامِ تاریخیِ ستون/دسته حفظ شد
        public const string CatSuspensionReason = "SuspensionReason";
        public const string CatMemberRole       = "MemberRole";

        // ─── وضعیت خدمات (۶ مقدار) ─────────────────────────────────────────
        // ترتیبِ آرایه = ترتیبِ SortOrder در کشویی‌ها = مسیرِ طبیعیِ چرخهٔ عمر.
        public static readonly string[] ServiceStatuses =
        {
            "متقاضی",
            "در حال بررسی",
            "در انتظار تایید",
            "فعال",
            "قطع",
            "قطع موقت"
        };

        // ─── نوع پرونده (۷ مقدار) ──────────────────────────────────────────
        public static readonly string[] CaseTypes =
        {
            "ایتام",
            "بدسرپرست",
            "معلول",
            "مهاجر",
            "کهن‌سال",
            "بی‌سرپرست",
            "سایر"
        };

        // ─── دلیل تعلیق (۷ مقدار) ──────────────────────────────────────────
        public static readonly string[] SuspensionReasons =
        {
            "تقلب",
            "ترک سکونت",
            "بالای سن قانونی",
            "اقتصاد بهتر",
            "فوت",
            "یتیم نیست",
            "سایر"
        };

        // ─── نقش عضو (۵ مقدار) ─────────────────────────────────────────────
        // این فهرست از قبل دقیقاً همین بود و تغییری نکرد.
        public static readonly string[] MemberRoles =
        {
            "یتیم",
            "پدر",
            "مادر",
            "فرزند",
            "سایر"
        };

        // ─── وضعیت‌هایی که یعنی «پرونده دیگر فعال نیست» ────────────────────
        // گزارش‌های «مستفیدینِ فعال» باید این‌ها را کنار بگذارند (فاز ۹).
        public static readonly string[] TerminatedStatuses = { "قطع", "قطع موقت" };

        // وضعیت‌هایی که هنوز به خدمت‌گیری نرسیده‌اند (پیش از فعال شدن).
        public static readonly string[] PreServiceStatuses = { "متقاضی", "در حال بررسی", "در انتظار تایید" };

        public const string StatusActive = "فعال";

        // ─── نگاشتِ مقادیرِ قدیمی → مقادیرِ استاندارد ───────────────────────
        // هم در مهاجرتِ دیتابیس (یک‌بار) و هم در نرمال‌سازیِ زمانِ اجرا
        // (ورودیِ اکسل/سینک از نسخه‌های قدیمی‌تر) استفاده می‌شود.
        public static readonly Dictionary<string, string> LegacyServiceStatusMap =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "در انتظار تأیید", "در انتظار تایید" },  // همزه → ی
                { "در انتظار",       "در انتظار تایید" },
                { "درانتظار",        "در انتظار تایید" },
                // گونه‌های «ی» عربی که از فایل‌های سینک/اکسل می‌آیند
                // (قبلاً فقط HtmlSyncProvider این‌ها را می‌شناخت).
                { "در انتظار تاييد", "در انتظار تایید" },
                { "انتظار تاييد",    "در انتظار تایید" },
                { "انتظار تایید",    "در انتظار تایید" },
                { "در حالت قطع",     "قطع" }
            };

        public static readonly Dictionary<string, string> LegacyCaseTypeMap =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "یتیم",      "ایتام" },
                { "کهولت سن",  "کهن‌سال" }
            };

        public static readonly Dictionary<string, string> LegacySuspensionReasonMap =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "انتقال محل سکونت",    "ترک سکونت"       },
                { "رسیدن به سن قانونی",  "بالای سن قانونی" },
                { "بهبود وضعیت اقتصادی", "اقتصاد بهتر"     },
                { "فوت‌شده",             "فوت"             },
                { "یتیم نبودن",          "یتیم نیست"       }
            };

        // ─── قطعهٔ CHECK constraint جدول TblCase ───────────────────────────
        // از همان آرایهٔ بالا ساخته می‌شود تا قید دیتابیس و کشویی‌های فرم
        // هرگز از هم جدا نیفتند.
        public static string ServiceStatusCheckSql
        {
            get
            {
                var quoted = new List<string>();
                foreach (string s in ServiceStatuses)
                    quoted.Add("'" + s + "'");
                return "CONSTRAINT CK_TblCase_ServiceStatus\r\n" +
                       "        CHECK (ServiceStatus IN (" + string.Join(", ", quoted.ToArray()) + "))";
            }
        }

        // نرمال‌سازیِ یک مقدارِ وضعیت خدمات (برای ایمپورت/سینک/ورودیِ آزاد).
        public static string NormalizeServiceStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string v = value.Trim();
            string mapped;
            return LegacyServiceStatusMap.TryGetValue(v, out mapped) ? mapped : v;
        }

        public static string NormalizeCaseType(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string v = value.Trim();
            string mapped;
            return LegacyCaseTypeMap.TryGetValue(v, out mapped) ? mapped : v;
        }

        public static bool IsTerminated(string serviceStatus)
        {
            string v = NormalizeServiceStatus(serviceStatus);
            return v == "قطع" || v == "قطع موقت";
        }

        // SQL امنِ «فقط مستفیدینِ فعال» — در گزارش‌ها استفاده می‌شود.
        public static string ActiveOnlySql(string column)
        {
            return column + " = '" + StatusActive + "'";
        }
    }
}

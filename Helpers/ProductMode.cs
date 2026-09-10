using System;
using System.Collections.Generic;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // فاز ۱ ERP — حالت محصول (Charity / Erp).
    //
    // آموزش: این کلاس هیچ جدول یا فرم خیریه‌ای را حذف نمی‌کند. فقط یک کلید
    // در TblAppSettings است که ناوبری، گزارش‌ساز و چند سطح UI را فیلتر می‌کند.
    // منطق کسب‌وکار (ذخیره پرونده، مساعدت، کارت سرپرست، …) دست‌نخورده می‌ماند
    // و در حالت Charity دقیقاً مثل قبل کار می‌کند.
    //
    // مقدار ذخیره‌نشده = Charity (سازگاری عقب‌رو برای آزمون‌ها و نصب‌های
    // موجود). Program.cs با EnsureDefault مقدار را برای پایگاه تازه‌خالی Erp
    // و برای پایگاه دارای پرونده Charity می‌نویسد.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ProductMode
    {
        public const string SettingKey = "ProductMode";
        public const string Charity    = "Charity";
        public const string Erp        = "Erp";

        public const int SchemaVersionNumber = 1;

        private static readonly HashSet<string> CharityModules =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Enterprise.ModuleService.ModuleCases,
                Enterprise.ModuleService.ModuleApplicants,
                Enterprise.ModuleService.ModuleSearch,
                Enterprise.ModuleService.ModuleFinance,
                Enterprise.ModuleService.ModuleDuplicates,
                Enterprise.ModuleService.ModuleDataQuality,
                Enterprise.ModuleService.ModuleBarcode,
                Enterprise.ModuleService.ModuleArchive
            };

        private static readonly HashSet<string> CharityNavTitles =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "منابع مالی و خیّرین",
                "قواعد مساعدت"
            };

        private static readonly HashSet<string> CharityReportSources =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Cases", "Family", "Documents", "Assistance",
                "CaseHistory", "FamilyHistory", "MissingDocuments",
                "Funding", "FieldVisits", "Timeline",
                "VulnerabilityScore", "CompletionStatus"
            };

        public static bool IsErp
        {
            get
            {
                return string.Equals(Current, Erp, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsCharity
        {
            get { return !IsErp; }
        }

        public static string Current
        {
            get
            {
                string value = SettingsHelper.Get(SettingKey, "");
                if (string.Equals(value, Erp, StringComparison.OrdinalIgnoreCase))
                    return Erp;
                return Charity;
            }
        }

        public static void Set(string mode)
        {
            string normalized = Normalize(mode);
            SettingsHelper.Set(SettingKey, normalized);
        }

        public static string Normalize(string mode)
        {
            if (string.Equals(mode, Erp, StringComparison.OrdinalIgnoreCase))
                return Erp;
            return Charity;
        }

        // اگر کلید هنوز نوشته نشده: پایگاه دارای پرونده → Charity، خالی → Erp.
        // اگر کلید موجود باشد دست نمی‌زند (انتخاب مدیر حفظ می‌شود).
        public static string EnsureDefault()
        {
            string existing = SettingsHelper.Get(SettingKey, "");
            if (!string.IsNullOrWhiteSpace(existing))
                return Normalize(existing);

            string chosen = HasAnyCase() ? Charity : Erp;
            SettingsHelper.Set(SettingKey, chosen);
            return chosen;
        }

        public static bool IsCharityModule(string moduleKey)
        {
            if (string.IsNullOrWhiteSpace(moduleKey)) return false;
            return CharityModules.Contains(moduleKey);
        }

        public static bool IsCharityPermission(string permKey)
        {
            if (string.IsNullOrWhiteSpace(permKey)) return false;
            if (IsCharityModule(permKey)) return true;

            string key = permKey.Trim();
            return Starts(key, "Case.")
                || Starts(key, "Family.")
                || Starts(key, "Docs.")
                || Starts(key, "Applicant.")
                || Starts(key, "CaseRelation.")
                || Starts(key, "Archive.")
                || Starts(key, "Representative.")
                || Starts(key, "GuardianCard.")
                || Starts(key, "AssistanceReceipt.")
                || string.Equals(key, "AI.Search", StringComparison.OrdinalIgnoreCase)
                || Starts(key, "AI.Reminders.")
                || Starts(key, "Barcode.")
                || Starts(key, "Finance.");
        }

        private static bool Starts(string key, string prefix)
        {
            return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool HidesNavTitle(string title)
        {
            if (!IsErp || string.IsNullOrWhiteSpace(title)) return false;
            return CharityNavTitles.Contains(title);
        }

        public static bool IsCharityReportSource(string sourceKey)
        {
            if (string.IsNullOrWhiteSpace(sourceKey)) return false;
            return CharityReportSources.Contains(sourceKey);
        }

        private static bool HasAnyCase()
        {
            try
            {
                object count = new DatabaseHelper().ExecuteScalar(
                    "SELECT COUNT(*) FROM TblCase;");
                if (count == null || count == DBNull.Value) return false;
                return Convert.ToInt64(count) > 0;
            }
            catch
            {
                // جدول پرونده هنوز نیست (مثلاً آزمون فقط حسابداری) → Charity فرض.
                return false;
            }
        }
    }
}

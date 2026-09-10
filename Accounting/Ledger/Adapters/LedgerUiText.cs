using CaseManagement.Accounting.Ledger.Domain;

namespace CaseManagement.Accounting.Ledger.Adapters
{
    public static class LedgerUiText
    {
        public static string Error(string code, string fallback)
        {
            if (string.IsNullOrEmpty(code)) return fallback ?? "";
            switch (code)
            {
                case LedgerErrorCodes.Unbalanced: return "سند تراز نیست (جمع بدهکار باید با بستانکار برابر باشد).";
                case LedgerErrorCodes.PeriodNotOpen: return "دوره مالی برای این تاریخ باز نیست.";
                case LedgerErrorCodes.PeriodLocked: return "دوره یا سال مالی قفل است.";
                case LedgerErrorCodes.YearNotOpen: return "سال مالی باز نیست.";
                case LedgerErrorCodes.AccountNotLeaf: return "ثبت فقط روی حساب برگ (سطح آخر) مجاز است.";
                case LedgerErrorCodes.AccountInactive: return "حساب غیرفعال است.";
                case LedgerErrorCodes.AccountDeleted: return "حساب حذف شده است.";
                case LedgerErrorCodes.AccountMissing: return "حساب پیدا نشد.";
                case LedgerErrorCodes.ConcurrencyConflict: return "این ردیف توسط کاربر دیگری تغییر کرده است. دوباره بارگذاری کنید.";
                case LedgerErrorCodes.AlreadyDeleted: return "این ردیف قبلاً حذف شده است.";
                case LedgerErrorCodes.AlreadyPosted: return "سند قبلاً ثبت قطعی شده است.";
                case LedgerErrorCodes.AlreadyReversed: return "سند قبلاً برگشت شده است.";
                case LedgerErrorCodes.NotDraft: return "فقط پیش‌نویس قابل این عملیات است.";
                case LedgerErrorCodes.ApprovalRequired: return "ابتدا سند را تأیید کنید.";
                case LedgerErrorCodes.PermissionDenied: return "مجوز این عملیات را ندارید.";
                case LedgerErrorCodes.CenterRequired: return "مرکز شعبه برای سند الزامی است.";
                case LedgerErrorCodes.CenterDenied: return "دسترسی به این مرکز ندارید.";
                case LedgerErrorCodes.FxRateMissing: return "نرخ ارز برای این تاریخ تعریف نشده است.";
                case LedgerErrorCodes.InvalidLine: return "سطر سند نامعتبر است.";
                case LedgerErrorCodes.TooFewLines: return "سند باید حداقل دو سطر داشته باشد.";
                case LedgerErrorCodes.JournalMissing: return "سند پیدا نشد.";
                case LedgerErrorCodes.InvalidStatus: return "وضعیت سند برای این عملیات مجاز نیست.";
                case LedgerErrorCodes.Overlap: return "بازه تاریخ با رکورد موجود تداخل دارد.";
                case LedgerErrorCodes.InvalidDate: return "تاریخ نامعتبر است.";
                case LedgerErrorCodes.HasChildren: return "حساب زیرمجموعه دارد.";
                case LedgerErrorCodes.HasPostings: return "حساب گردش دفتر کل دارد.";
                case LedgerErrorCodes.PostedImmutable: return "سند ثبت‌شده قابل ویرایش یا حذف نیست.";
                case LedgerErrorCodes.MappingMissing: return "نگاشت صندوق/دسته به حساب دفتر کل تعریف نشده است.";
                case LedgerErrorCodes.DimensionMissing: return "مرکز هزینه یا پروژه پیدا نشد.";
                case LedgerErrorCodes.DimensionInactive: return "مرکز هزینه یا پروژه غیرفعال است.";
                case LedgerErrorCodes.DimensionNotLeaf: return "ثبت فقط روی برگ مرکز هزینه/پروژه مجاز است.";
                default: return string.IsNullOrEmpty(fallback) ? code : fallback;
            }
        }

        public static string TypeName(string code)
        {
            if (code == LedgerCodes.TypeAsset) return "دارایی";
            if (code == LedgerCodes.TypeLiability) return "بدهی";
            if (code == LedgerCodes.TypeEquity) return "حقوق مالکانه";
            if (code == LedgerCodes.TypeRevenue) return "درآمد";
            if (code == LedgerCodes.TypeExpense) return "هزینه";
            return code ?? "";
        }

        public static string Money(long minor, int minorUnits)
        {
            if (minorUnits < 0) minorUnits = 2;
            decimal scale = 1m;
            for (int i = 0; i < minorUnits; i++) scale *= 10m;
            return (minor / scale).ToString("N" + minorUnits);
        }
    }
}

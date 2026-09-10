using System;

namespace CaseManagement.Accounting.Ledger.Domain
{
    /// <summary>
    /// Canonical string values persisted in Gl* tables. Not a substitute for GlAccountType rows.
    /// </summary>
    public static class LedgerCodes
    {
        public const int DefaultCompanyId = 1;
        public const int SharedCenterId = 0;
        public const long RateOne = 1000000L;
        public const int MaxAccountDepth = 16;

        public const string BaseCurrency = "AFN";

        public const string StatusOpen = "Open";
        public const string StatusClosed = "Closed";
        public const string StatusLocked = "Locked";

        public const string JournalDraft = "Draft";
        public const string JournalApproved = "Approved";
        public const string JournalPosted = "Posted";
        public const string JournalReversed = "Reversed";

        public const string SourceManual = "Manual";
        public const string SourceOpening = "Opening";
        public const string SourceClose = "Close";
        public const string SourceReversal = "Reversal";
        public const string SourceCashBook = "CashBook";
        public const string SourceInventory = "Inventory";
        public const string SourcePurchase = "Purchase";
        public const string SourceSales = "Sales";
        public const string SourcePayroll = "Payroll";
        public const string SourceFixedAsset = "FixedAsset";

        public const string TypeAsset = "ASSET";
        public const string TypeLiability = "LIABILITY";
        public const string TypeEquity = "EQUITY";
        public const string TypeRevenue = "REVENUE";
        public const string TypeExpense = "EXPENSE";

        public const string BalanceDebit = "DEBIT";
        public const string BalanceCredit = "CREDIT";
        public const string StatementBs = "BS";
        public const string StatementPl = "PL";

        public const string CalendarSolarHijri = "SOLAR_HIJRI";
        public const string SystemUser = "SYSTEM";

        public const string MapFund = "FUND";
        public const string MapIncome = "INCOME_CAT";
        public const string MapExpense = "EXPENSE_CAT";
        public const string DocAccTransaction = "AccTransaction";
        public const string AccReceipt = "دریافت";
        public const string AccPayment = "پرداخت";
    }

    public static class LedgerPermissions
    {
        public const string View = "Ledger.View";
        public const string Create = "Ledger.Create";
        public const string Approve = "Ledger.Approve";
        public const string Post = "Ledger.Post";
        public const string Reverse = "Ledger.Reverse";
        public const string ManageCoA = "Ledger.ManageCoA";
        public const string ClosePeriod = "Ledger.ClosePeriod";
        public const string CloseYear = "Ledger.CloseYear";
        public const string ReopenYear = "Ledger.ReopenYear";
        public const string UnlockYear = "Ledger.UnlockYear";
        public const string ManageCostCenter = "Ledger.ManageCostCenter";
        public const string ManageProject = "Ledger.ManageProject";
        public const string MapCashBook = "Ledger.MapCashBook";
    }

    public static class LedgerErrorCodes
    {
        public const string Unbalanced = "UNBALANCED";
        public const string PeriodNotOpen = "PERIOD_NOT_OPEN";
        public const string PeriodLocked = "PERIOD_LOCKED";
        public const string YearNotOpen = "YEAR_NOT_OPEN";
        public const string AccountNotLeaf = "ACCOUNT_NOT_LEAF";
        public const string AccountInactive = "ACCOUNT_INACTIVE";
        public const string AccountDeleted = "ACCOUNT_DELETED";
        public const string AccountMissing = "ACCOUNT_MISSING";
        public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
        public const string AlreadyDeleted = "ALREADY_DELETED";
        public const string AlreadyPosted = "ALREADY_POSTED";
        public const string AlreadyReversed = "ALREADY_REVERSED";
        public const string NotDraft = "NOT_DRAFT";
        public const string ApprovalRequired = "APPROVAL_REQUIRED";
        public const string PermissionDenied = "PERMISSION_DENIED";
        public const string CenterRequired = "CENTER_REQUIRED";
        public const string CenterDenied = "CENTER_DENIED";
        public const string FxRateMissing = "FX_RATE_MISSING";
        public const string InvalidLine = "INVALID_LINE";
        public const string TooFewLines = "TOO_FEW_LINES";
        public const string JournalMissing = "JOURNAL_MISSING";
        public const string InvalidStatus = "INVALID_STATUS";
        public const string Overlap = "DATE_OVERLAP";
        public const string InvalidDate = "INVALID_DATE";
        public const string HasChildren = "HAS_CHILDREN";
        public const string HasPostings = "HAS_POSTINGS";
        public const string Cycle = "COA_CYCLE";
        public const string TypeMismatch = "ACCOUNT_TYPE_MISMATCH";
        public const string Validation = "VALIDATION";
        public const string PostedImmutable = "POSTED_IMMUTABLE";
        public const string MappingMissing = "MAPPING_MISSING";
        public const string DimensionMissing = "DIMENSION_MISSING";
        public const string DimensionInactive = "DIMENSION_INACTIVE";
        public const string DimensionNotLeaf = "DIMENSION_NOT_LEAF";
    }

    public static class LedgerTime
    {
        public static string UtcNow(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string DateOnly(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            DateTime parsed;
            if (DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out parsed))
            {
                return parsed.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            }
            if (value.Length >= 10) return value.Substring(0, 10);
            return value;
        }

        public static long ToMinorUnits(decimal major, int minorUnits)
        {
            if (minorUnits < 0) minorUnits = 2;
            decimal scale = 1m;
            for (int i = 0; i < minorUnits; i++) scale *= 10m;
            return (long)Math.Round(major * scale, MidpointRounding.AwayFromZero);
        }
    }
}

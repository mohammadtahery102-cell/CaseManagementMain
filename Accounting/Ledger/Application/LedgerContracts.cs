using System;
using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Domain;

namespace CaseManagement.Accounting.Ledger.Application
{
    public interface ILedgerIdentity
    {
        string UserName { get; }
        int UserId { get; }
        int CompanyId { get; }
        int CenterId { get; }
        bool IsSuperAdmin { get; }
        DateTime UtcNow { get; }
        bool HasPermission(string permissionKey);
    }

    public sealed class LedgerIdentity : ILedgerIdentity
    {
        public string UserName { get; set; }
        public int UserId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public bool IsSuperAdmin { get; set; }
        public DateTime UtcNow { get; set; }
        public Func<string, bool> PermissionChecker { get; set; }

        public bool HasPermission(string permissionKey)
        {
            if (IsSuperAdmin) return true;
            if (PermissionChecker != null) return PermissionChecker(permissionKey);
            return false;
        }
    }

    public class LedgerResult
    {
        public bool Ok { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public long JournalId { get; set; }
        public long EntityId { get; set; }
        public long RowVersion { get; set; }
        public GlJournal Journal { get; set; }

        public static LedgerResult Success(long journalId, long rowVersion)
        {
            return new LedgerResult
            {
                Ok = true,
                JournalId = journalId,
                EntityId = journalId,
                RowVersion = rowVersion
            };
        }

        public static LedgerResult Entity(long entityId, long rowVersion)
        {
            return new LedgerResult
            {
                Ok = true,
                EntityId = entityId,
                JournalId = entityId,
                RowVersion = rowVersion
            };
        }

        public static LedgerResult Fail(string code, string message)
        {
            return new LedgerResult
            {
                Ok = false,
                ErrorCode = code,
                Message = message
            };
        }
    }

    public class JournalLineDraft
    {
        public int LineNo { get; set; }
        public long AccountId { get; set; }
        public long DebitMinor { get; set; }
        public long CreditMinor { get; set; }
        public string CurrencyCode { get; set; }
        public long ExchangeRateMicros { get; set; }
        public string Description { get; set; }
        public long? CostCenterId { get; set; }
        public long? ProjectId { get; set; }
        public int? PartyId { get; set; }
        public int? FundId { get; set; }
    }

    public class SaveDraftJournalCommand
    {
        public long JournalId { get; set; }
        public long ExpectedRowVersion { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string PostingDate { get; set; }
        public string DocumentDate { get; set; }
        public string ReferenceNumber { get; set; }
        public string Description { get; set; }
        public string JournalSource { get; set; }
        public string SourceModule { get; set; }
        public string SourceDocumentType { get; set; }
        public long? SourceDocumentId { get; set; }
        public string ClientJournalGuid { get; set; }
        public List<JournalLineDraft> Lines { get; set; }
    }

    public class PostJournalCommand
    {
        public long JournalId { get; set; }
        public long ExpectedRowVersion { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string PostingDate { get; set; }
        public string DocumentDate { get; set; }
        public string ReferenceNumber { get; set; }
        public string Description { get; set; }
        public string JournalSource { get; set; }
        public string SourceModule { get; set; }
        public string SourceDocumentType { get; set; }
        public long? SourceDocumentId { get; set; }
        public string ClientJournalGuid { get; set; }
        public List<JournalLineDraft> Lines { get; set; }
    }

    public class ReverseJournalCommand
    {
        public long JournalId { get; set; }
        public long ExpectedRowVersion { get; set; }
        public string PostingDate { get; set; }
        public string Description { get; set; }
    }

    public class JournalStatusCommand
    {
        public long JournalId { get; set; }
        public long ExpectedRowVersion { get; set; }
    }

    public class CreateAccountCommand
    {
        public int CompanyId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string AccountTypeCode { get; set; }
        public long? ParentAccountId { get; set; }
        public bool IsLeaf { get; set; }
        public bool AllowPosting { get; set; }
        public string ControlCurrencyCode { get; set; }
        public bool IsContra { get; set; }
        public long ExpectedRowVersion { get; set; }
    }

    public class CreateFiscalYearCommand
    {
        public int CompanyId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string CalendarType { get; set; }
        public long ExpectedRowVersion { get; set; }
    }

    public class CreateFiscalPeriodCommand
    {
        public long FiscalYearId { get; set; }
        public int PeriodNo { get; set; }
        public string Name { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public long ExpectedRowVersion { get; set; }
    }

    public class CalendarStatusCommand
    {
        public long EntityId { get; set; }
        public long ExpectedRowVersion { get; set; }
    }

    public class SoftDeleteCommand
    {
        public long EntityId { get; set; }
        public long ExpectedRowVersion { get; set; }
    }

    /// <summary>
    /// Application port for journal lifecycle. WinForms, REST, Web, and mobile call this.
    /// </summary>
    public interface IGeneralLedger
    {
        LedgerResult SaveDraft(SaveDraftJournalCommand command, ILedgerIdentity identity);
        LedgerResult SubmitForApproval(JournalStatusCommand command, ILedgerIdentity identity);
        LedgerResult Approve(JournalStatusCommand command, ILedgerIdentity identity);
        LedgerResult Reject(JournalStatusCommand command, ILedgerIdentity identity);
        LedgerResult Post(PostJournalCommand command, ILedgerIdentity identity);
        LedgerResult Reverse(ReverseJournalCommand command, ILedgerIdentity identity);
        LedgerResult SoftDeleteDraft(JournalStatusCommand command, ILedgerIdentity identity);
        LedgerResult GetJournal(long journalId, ILedgerIdentity identity);
        IList<GlJournal> ListJournals(string fromDate, string toDate, ILedgerIdentity identity);
    }

    public interface IChartOfAccountsService
    {
        LedgerResult Create(CreateAccountCommand command, ILedgerIdentity identity);
        LedgerResult SoftDelete(SoftDeleteCommand command, ILedgerIdentity identity);
        LedgerResult Deactivate(SoftDeleteCommand command, ILedgerIdentity identity);
        LedgerResult Activate(SoftDeleteCommand command, ILedgerIdentity identity);
        IList<GlAccount> List(int companyId, bool includeDeleted);
        IList<GlAccountType> ListTypes(int companyId);
        GlAccount Get(long accountId);
        GlCompany GetCompany(int companyId);
    }

    public interface IFiscalCalendarService
    {
        LedgerResult CreateYear(CreateFiscalYearCommand command, ILedgerIdentity identity);
        LedgerResult AddPeriod(CreateFiscalPeriodCommand command, ILedgerIdentity identity);
        LedgerResult ClosePeriod(CalendarStatusCommand command, ILedgerIdentity identity);
        LedgerResult LockPeriod(CalendarStatusCommand command, ILedgerIdentity identity);
        LedgerResult CloseYear(CalendarStatusCommand command, ILedgerIdentity identity);
        LedgerResult LockYear(CalendarStatusCommand command, ILedgerIdentity identity);
        LedgerResult UnlockYear(CalendarStatusCommand command, ILedgerIdentity identity);
        LedgerResult ReopenYear(CalendarStatusCommand command, ILedgerIdentity identity);
        LedgerResult ReopenPeriod(CalendarStatusCommand command, ILedgerIdentity identity);
        GlFiscalPeriod Resolve(int companyId, string postingDate);
        IList<GlFiscalYear> ListYears(int companyId);
        IList<GlFiscalPeriod> ListPeriods(long fiscalYearId);
    }

    public class UpsertCashBookMapCommand
    {
        public int CompanyId { get; set; }
        public string MapKind { get; set; }
        public long SourceId { get; set; }
        public long AccountId { get; set; }
        public long ExpectedRowVersion { get; set; }
    }

    public class CreateDimensionCommand
    {
        public int CompanyId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public long? ParentId { get; set; }
        public bool IsLeaf { get; set; }
    }

    public interface ICashBookGlService
    {
        IList<CashBookTxn> ListTransactions(ILedgerIdentity identity);
        IList<GlCashBookMap> ListMaps(int companyId, string mapKind);
        IList<KeyValuePair<long, string>> ListMappableSources(string mapKind, ILedgerIdentity identity);
        LedgerResult UpsertMap(UpsertCashBookMapCommand command, ILedgerIdentity identity);
        LedgerResult PostTransaction(long txnId, ILedgerIdentity identity);
        LedgerResult ReverseIfVoided(long txnId, ILedgerIdentity identity);
    }

    public interface ICostCenterService
    {
        LedgerResult Create(CreateDimensionCommand command, ILedgerIdentity identity);
        LedgerResult Activate(SoftDeleteCommand command, ILedgerIdentity identity);
        LedgerResult Deactivate(SoftDeleteCommand command, ILedgerIdentity identity);
        IList<GlCostCenter> List(int companyId, bool includeInactive);
        GlCostCenter Get(long costCenterId);
    }

    public interface IProjectService
    {
        LedgerResult Create(CreateDimensionCommand command, ILedgerIdentity identity);
        LedgerResult Activate(SoftDeleteCommand command, ILedgerIdentity identity);
        LedgerResult Deactivate(SoftDeleteCommand command, ILedgerIdentity identity);
        IList<GlProject> List(int companyId, bool includeInactive);
        GlProject Get(long projectId);
    }

    public class LedgerReportQuery
    {
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long CostCenterId { get; set; }
        public long ProjectId { get; set; }
        public long FiscalYearId { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public long AccountId { get; set; }
    }

    public class TrialBalanceRow
    {
        public long AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string AccountTypeCode { get; set; }
        public bool IsContra { get; set; }
        public long OpeningDebit { get; set; }
        public long OpeningCredit { get; set; }
        public long PeriodDebit { get; set; }
        public long PeriodCredit { get; set; }
        public long ClosingDebit { get; set; }
        public long ClosingCredit { get; set; }
    }

    public class GeneralLedgerLineRow
    {
        public string PostingDate { get; set; }
        public string JournalNumber { get; set; }
        public string Description { get; set; }
        public long DebitBaseMinor { get; set; }
        public long CreditBaseMinor { get; set; }
        public long RunningNet { get; set; }
    }

    public class StatementLine
    {
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string AccountTypeCode { get; set; }
        public long AmountMinor { get; set; }
    }

    public class BalanceSheetResult
    {
        public IList<StatementLine> Assets { get; set; }
        public IList<StatementLine> Liabilities { get; set; }
        public IList<StatementLine> Equity { get; set; }
        public long AssetTotal { get; set; }
        public long LiabilityTotal { get; set; }
        public long EquityTotal { get; set; }
        public long CurrentPeriodNetIncome { get; set; }
        public bool EquationHolds { get; set; }
    }

    public class ProfitAndLossResult
    {
        public IList<StatementLine> Revenue { get; set; }
        public IList<StatementLine> Expenses { get; set; }
        public long RevenueTotal { get; set; }
        public long ExpenseTotal { get; set; }
        public long NetIncome { get; set; }
    }

    public interface ILedgerReporting
    {
        IList<TrialBalanceRow> GetTrialBalance(LedgerReportQuery query, ILedgerIdentity identity);
        IList<GeneralLedgerLineRow> GetGeneralLedger(LedgerReportQuery query, ILedgerIdentity identity);
        BalanceSheetResult GetBalanceSheet(LedgerReportQuery query, ILedgerIdentity identity);
        ProfitAndLossResult GetProfitAndLoss(LedgerReportQuery query, ILedgerIdentity identity);
        IList<CurrencyPositionRow> GetCurrencyPositions(LedgerReportQuery query, ILedgerIdentity identity);
    }

    public class CurrencyPositionRow
    {
        public long AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string AccountTypeCode { get; set; }
        public string CurrencyCode { get; set; }
        public long TransactionNetMinor { get; set; }
        public long BookedBaseMinor { get; set; }
        public long RateToBaseMicros { get; set; }
        public long RevaluedBaseMinor { get; set; }
        public long UnrealizedBaseMinor { get; set; }
    }

    public class YearEndCloseCommand
    {
        public long FiscalYearId { get; set; }
        public long ExpectedRowVersion { get; set; }
        public int CenterId { get; set; }
    }

    public class OpeningBalanceCommand
    {
        public long FiscalYearId { get; set; }
        public int CenterId { get; set; }
        public string PostingDate { get; set; }
        public string Description { get; set; }
        public List<JournalLineDraft> Lines { get; set; }
    }

    public class InitializeNextYearCommand
    {
        public long PriorFiscalYearId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool PostOpeningFromPrior { get; set; }
        public int CenterId { get; set; }
    }

    public class UpsertExchangeRateCommand
    {
        public int CompanyId { get; set; }
        public string CurrencyCode { get; set; }
        public string RateDate { get; set; }
        public long RateToBaseMicros { get; set; }
    }

    public class RevalueCommand
    {
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string AsOfDate { get; set; }
    }

    public interface IYearEndService
    {
        LedgerResult Close(YearEndCloseCommand command, ILedgerIdentity identity);
        LedgerResult Reopen(YearEndCloseCommand command, ILedgerIdentity identity);
        LedgerResult PostOpening(OpeningBalanceCommand command, ILedgerIdentity identity);
        LedgerResult PostOpeningFromPriorYear(long priorYearId, long nextYearId, ILedgerIdentity identity);
        LedgerResult InitializeNextYear(InitializeNextYearCommand command, ILedgerIdentity identity);
    }

    public interface ICurrencyAccountingService
    {
        IList<GlCurrency> ListCurrencies(int companyId);
        IList<GlExchangeRate> ListRates(int companyId, string currencyCode);
        LedgerResult UpsertRate(UpsertExchangeRateCommand command, ILedgerIdentity identity);
        LedgerResult Revalue(RevalueCommand command, ILedgerIdentity identity);
        string FunctionalCurrency(int companyId);
    }
}

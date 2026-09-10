using System.Collections.Generic;

namespace CaseManagement.Accounting.Ledger.Domain
{
    public class GlCompany
    {
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string BaseCurrencyCode { get; set; }
        public int MinorUnits { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public string DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class GlCurrency
    {
        public long CurrencyId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string CurrencyCode { get; set; }
        public string Name { get; set; }
        public int MinorUnits { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public string DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class GlExchangeRate
    {
        public long ExchangeRateId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string CurrencyCode { get; set; }
        public string RateDate { get; set; }
        public long RateToBaseMicros { get; set; }
        public bool IsDeleted { get; set; }
        public string DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class GlAccountType
    {
        public long AccountTypeId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string AccountTypeCode { get; set; }
        public string Name { get; set; }
        public string NormalBalance { get; set; }
        public string Statement { get; set; }
        public bool IsDeleted { get; set; }
        public string DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class GlFiscalYear
    {
        public long FiscalYearId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string CalendarType { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string Status { get; set; }
        public string ClosedAt { get; set; }
        public string ClosedBy { get; set; }
        public string LockedAt { get; set; }
        public string LockedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class GlFiscalPeriod
    {
        public long FiscalPeriodId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long FiscalYearId { get; set; }
        public int PeriodNo { get; set; }
        public string Name { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string Status { get; set; }
        public bool IsDeleted { get; set; }
        public string DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class GlAccount
    {
        public long AccountId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string AccountTypeCode { get; set; }
        public long? ParentAccountId { get; set; }
        public int Level { get; set; }
        public bool IsLeaf { get; set; }
        public bool AllowPosting { get; set; }
        public bool IsActive { get; set; }
        public bool IsContra { get; set; }
        public string ControlCurrencyCode { get; set; }
        public bool IsDeleted { get; set; }
        public string DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class GlLedgerSetting
    {
        public long LedgerSettingId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public bool RequiresJournalApproval { get; set; }
        public long NextJournalNumber { get; set; }
        public string NumberPrefix { get; set; }
        public bool IsDeleted { get; set; }
        public string DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class GlJournal
    {
        public long JournalId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long FiscalYearId { get; set; }
        public long FiscalPeriodId { get; set; }
        public string JournalNumber { get; set; }
        public string JournalSource { get; set; }
        public string SourceModule { get; set; }
        public string SourceDocumentType { get; set; }
        public long? SourceDocumentId { get; set; }
        public string PostingDate { get; set; }
        public string DocumentDate { get; set; }
        public string ReferenceNumber { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public long? ReversesJournalId { get; set; }
        public string ClientJournalGuid { get; set; }
        public bool IsDeleted { get; set; }
        public string DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
        public string ApprovedAt { get; set; }
        public string ApprovedBy { get; set; }
        public string PostedAt { get; set; }
        public string PostedBy { get; set; }
        public IList<GlJournalLine> Lines { get; set; }
    }

    public class GlJournalLine
    {
        public long JournalLineId { get; set; }
        public long JournalId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public int LineNo { get; set; }
        public long AccountId { get; set; }
        public long DebitMinor { get; set; }
        public long CreditMinor { get; set; }
        public string CurrencyCode { get; set; }
        public long ExchangeRateMicros { get; set; }
        public long DebitBaseMinor { get; set; }
        public long CreditBaseMinor { get; set; }
        public string Description { get; set; }
        public long? CostCenterId { get; set; }
        public long? ProjectId { get; set; }
        public int? PartyId { get; set; }
        public int? FundId { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }
}

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
    }

    public interface IChartOfAccountsService
    {
        LedgerResult Create(CreateAccountCommand command, ILedgerIdentity identity);
        LedgerResult SoftDelete(SoftDeleteCommand command, ILedgerIdentity identity);
        LedgerResult Deactivate(SoftDeleteCommand command, ILedgerIdentity identity);
        IList<GlAccount> List(int companyId, bool includeDeleted);
        GlAccount Get(long accountId);
    }

    public interface IFiscalCalendarService
    {
        LedgerResult CreateYear(CreateFiscalYearCommand command, ILedgerIdentity identity);
        LedgerResult AddPeriod(CreateFiscalPeriodCommand command, ILedgerIdentity identity);
        LedgerResult ClosePeriod(CalendarStatusCommand command, ILedgerIdentity identity);
        LedgerResult LockPeriod(CalendarStatusCommand command, ILedgerIdentity identity);
        LedgerResult CloseYear(CalendarStatusCommand command, ILedgerIdentity identity);
        LedgerResult LockYear(CalendarStatusCommand command, ILedgerIdentity identity);
        LedgerResult ReopenYear(CalendarStatusCommand command, ILedgerIdentity identity);
        GlFiscalPeriod Resolve(int companyId, string postingDate);
    }
}

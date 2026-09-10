using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;
using CaseManagement.Payroll.Domain;
using CaseManagement.Payroll.Infrastructure;

namespace CaseManagement.Payroll.Application
{
    public class PayrollOutboxAdapter : IOutboxHandler
    {
        private readonly PayrollStore _store;
        private readonly LedgerRepository _repo;
        private readonly IGeneralLedger _gl;

        public PayrollOutboxAdapter()
            : this(new PayrollStore(), new LedgerRepository(), new PostingEngine()) { }

        public PayrollOutboxAdapter(PayrollStore store, LedgerRepository repo, IGeneralLedger gl)
        {
            _store = store;
            _repo = repo;
            _gl = gl;
        }

        public bool CanHandle(string sourceModule)
        {
            return sourceModule == LedgerCodes.SourcePayroll;
        }

        public LedgerResult Handle(AccOutboxRow row, ILedgerIdentity identity)
        {
            if (row.DocumentType != PayrollCodes.DocRun)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Unsupported payroll document type.");
            PrRun run = _store.GetRun(row.DocumentId);
            if (row.Operation == LedgerCodes.OutboxReverse)
            {
                int companyId = run != null ? run.CompanyId : LedgerCodes.DefaultCompanyId;
                GlJournal existingR = _repo.GetJournalBySource(companyId, LedgerCodes.SourcePayroll, PayrollCodes.DocRun, row.DocumentId);
                if (existingR == null) return LedgerResult.Success(0, 0);
                if (existingR.Status == LedgerCodes.JournalReversed)
                    return LedgerResult.Success(existingR.JournalId, existingR.RowVersion);
                return _gl.Reverse(new ReverseJournalCommand
                {
                    JournalId = existingR.JournalId,
                    ExpectedRowVersion = existingR.RowVersion,
                    PostingDate = existingR.PostingDate
                }, identity);
            }
            if (run == null) return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Payroll run not found.");
            if (run.TotalMinor <= 0) return LedgerResult.Success(0, 0);
            GlJournal existing = _repo.GetJournalBySource(run.CompanyId, LedgerCodes.SourcePayroll, PayrollCodes.DocRun, run.RunId);
            if (existing != null && existing.Status == LedgerCodes.JournalPosted)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);
            long exp = _store.Account(run.CompanyId, "ExpenseAccountID");
            long pay = _store.Account(run.CompanyId, "PayableAccountID");
            if (exp <= 0 || pay <= 0)
                return LedgerResult.Fail(LedgerErrorCodes.MappingMissing, "Payroll accounts are missing.");
            PostJournalCommand cmd = new PostJournalCommand
            {
                CompanyId = run.CompanyId,
                CenterId = run.CenterId,
                PostingDate = run.RunDate,
                DocumentDate = run.RunDate,
                ReferenceNumber = run.DocNo,
                Description = "Payroll " + run.DocNo,
                JournalSource = LedgerCodes.SourcePayroll,
                SourceModule = LedgerCodes.SourcePayroll,
                SourceDocumentType = PayrollCodes.DocRun,
                SourceDocumentId = run.RunId,
                Lines = new List<JournalLineDraft>
                {
                    new JournalLineDraft { LineNo = 1, AccountId = exp, DebitMinor = run.TotalMinor, CreditMinor = 0 },
                    new JournalLineDraft { LineNo = 2, AccountId = pay, DebitMinor = 0, CreditMinor = run.TotalMinor }
                }
            };
            return CaseManagement.Trade.GlOutboxPostHelper.PostOrApprove(_gl, cmd, identity);
        }
    }
}

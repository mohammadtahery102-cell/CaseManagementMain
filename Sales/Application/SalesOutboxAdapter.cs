using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;
using CaseManagement.Sales.Domain;
using CaseManagement.Sales.Infrastructure;

namespace CaseManagement.Sales.Application
{
    public class SalesOutboxAdapter : IOutboxHandler
    {
        private readonly SalesStore _store;
        private readonly LedgerRepository _repo;
        private readonly IGeneralLedger _gl;

        public SalesOutboxAdapter()
            : this(new SalesStore(), new LedgerRepository(), new PostingEngine()) { }

        public SalesOutboxAdapter(SalesStore store, LedgerRepository repo, IGeneralLedger gl)
        {
            _store = store;
            _repo = repo;
            _gl = gl;
        }

        public bool CanHandle(string sourceModule)
        {
            return sourceModule == LedgerCodes.SourceSales;
        }

        public LedgerResult Handle(AccOutboxRow row, ILedgerIdentity identity)
        {
            if (row.DocumentType != SalesCodes.DocInvoice)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Unsupported sales document type.");
            if (row.Operation == LedgerCodes.OutboxReverse)
            {
                SalInvoice invR = _store.GetInvoice(row.DocumentId);
                int companyId = invR != null ? invR.CompanyId : LedgerCodes.DefaultCompanyId;
                GlJournal existingR = _repo.GetJournalBySource(companyId, LedgerCodes.SourceSales, SalesCodes.DocInvoice, row.DocumentId);
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

            SalInvoice inv = _store.GetInvoice(row.DocumentId);
            if (inv == null)
                return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Sales invoice not found.");
            if (inv.AmountMinor <= 0) return LedgerResult.Success(0, 0);

            GlJournal existing = _repo.GetJournalBySource(inv.CompanyId, LedgerCodes.SourceSales, SalesCodes.DocInvoice, inv.InvoiceId);
            if (existing != null && existing.Status == LedgerCodes.JournalPosted)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);

            long ar = _store.ArAccountId(inv.CompanyId);
            long rev = _store.RevenueAccountId(inv.CompanyId);
            if (ar <= 0 || rev <= 0)
                return LedgerResult.Fail(LedgerErrorCodes.MappingMissing, "AR or Revenue account is missing.");

            PostJournalCommand cmd = new PostJournalCommand
            {
                CompanyId = inv.CompanyId,
                CenterId = inv.CenterId,
                PostingDate = inv.InvoiceDate,
                DocumentDate = inv.InvoiceDate,
                ReferenceNumber = inv.DocNo,
                Description = "Sales invoice " + inv.DocNo,
                JournalSource = LedgerCodes.SourceSales,
                SourceModule = LedgerCodes.SourceSales,
                SourceDocumentType = SalesCodes.DocInvoice,
                SourceDocumentId = inv.InvoiceId,
                Lines = new List<JournalLineDraft>
                {
                    new JournalLineDraft { LineNo = 1, AccountId = ar, DebitMinor = inv.AmountMinor, CreditMinor = 0 },
                    new JournalLineDraft { LineNo = 2, AccountId = rev, DebitMinor = 0, CreditMinor = inv.AmountMinor }
                }
            };
            return CaseManagement.Trade.GlOutboxPostHelper.PostOrApprove(_gl, cmd, identity);
        }
    }
}

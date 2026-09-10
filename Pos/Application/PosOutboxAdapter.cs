using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;
using CaseManagement.Pos.Domain;
using CaseManagement.Pos.Infrastructure;
using CaseManagement.Sales.Infrastructure;

namespace CaseManagement.Pos.Application
{
    public class PosOutboxAdapter : IOutboxHandler
    {
        private readonly PosStore _store;
        private readonly SalesStore _sales;
        private readonly LedgerRepository _repo;
        private readonly IGeneralLedger _gl;

        public PosOutboxAdapter()
            : this(new PosStore(), new SalesStore(), new LedgerRepository(), new PostingEngine()) { }

        public PosOutboxAdapter(PosStore store, SalesStore sales, LedgerRepository repo, IGeneralLedger gl)
        {
            _store = store;
            _sales = sales;
            _repo = repo;
            _gl = gl;
        }

        public bool CanHandle(string sourceModule)
        {
            return sourceModule == LedgerCodes.SourcePos;
        }

        public LedgerResult Handle(AccOutboxRow row, ILedgerIdentity identity)
        {
            if (row.DocumentType != PosCodes.DocReturn)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Unsupported POS document type.");
            PosReturnDoc d = _store.GetReturn(row.DocumentId);
            if (row.Operation == LedgerCodes.OutboxReverse)
            {
                int companyId = d != null ? d.CompanyId : LedgerCodes.DefaultCompanyId;
                GlJournal existingR = _repo.GetJournalBySource(companyId, LedgerCodes.SourcePos, PosCodes.DocReturn, row.DocumentId);
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
            if (d == null) return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "POS return not found.");
            if (d.AmountMinor <= 0) return LedgerResult.Success(0, 0);
            GlJournal existing = _repo.GetJournalBySource(d.CompanyId, LedgerCodes.SourcePos, PosCodes.DocReturn, d.ReturnId);
            if (existing != null && existing.Status == LedgerCodes.JournalPosted)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);
            long ar = _sales.ArAccountId(d.CompanyId);
            long rev = _sales.RevenueAccountId(d.CompanyId);
            if (ar <= 0 || rev <= 0)
                return LedgerResult.Fail(LedgerErrorCodes.MappingMissing, "AR or Revenue account is missing.");
            PostJournalCommand cmd = new PostJournalCommand
            {
                CompanyId = d.CompanyId,
                CenterId = d.CenterId,
                PostingDate = d.ReturnDate,
                DocumentDate = d.ReturnDate,
                ReferenceNumber = d.DocNo,
                Description = "POS return " + d.DocNo,
                JournalSource = LedgerCodes.SourcePos,
                SourceModule = LedgerCodes.SourcePos,
                SourceDocumentType = PosCodes.DocReturn,
                SourceDocumentId = d.ReturnId,
                Lines = new List<JournalLineDraft>
                {
                    new JournalLineDraft { LineNo = 1, AccountId = rev, DebitMinor = d.AmountMinor, CreditMinor = 0 },
                    new JournalLineDraft { LineNo = 2, AccountId = ar, DebitMinor = 0, CreditMinor = d.AmountMinor }
                }
            };
            return CaseManagement.Trade.GlOutboxPostHelper.PostOrApprove(_gl, cmd, identity);
        }
    }
}

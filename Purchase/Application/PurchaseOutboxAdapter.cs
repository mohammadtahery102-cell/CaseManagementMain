using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;
using CaseManagement.Purchase.Domain;
using CaseManagement.Purchase.Infrastructure;

namespace CaseManagement.Purchase.Application
{
    public class PurchaseOutboxAdapter : IOutboxHandler
    {
        private readonly PurchaseStore _store;
        private readonly Inventory.Infrastructure.InventoryStore _inv;
        private readonly LedgerRepository _repo;
        private readonly IGeneralLedger _gl;

        public PurchaseOutboxAdapter()
            : this(new PurchaseStore(), new Inventory.Infrastructure.InventoryStore(), new LedgerRepository(), new PostingEngine()) { }

        public PurchaseOutboxAdapter(PurchaseStore store, Inventory.Infrastructure.InventoryStore inv, LedgerRepository repo, IGeneralLedger gl)
        {
            _store = store;
            _inv = inv;
            _repo = repo;
            _gl = gl;
        }

        public bool CanHandle(string sourceModule)
        {
            return sourceModule == LedgerCodes.SourcePurchase;
        }

        public LedgerResult Handle(AccOutboxRow row, ILedgerIdentity identity)
        {
            if (row.DocumentType != PurchaseCodes.DocInvoice)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Unsupported purchase document type.");
            if (row.Operation == LedgerCodes.OutboxReverse)
                return Reverse(row, identity);
            return Post(row, identity);
        }

        private LedgerResult Post(AccOutboxRow row, ILedgerIdentity identity)
        {
            PurInvoice inv = _store.GetInvoice(row.DocumentId);
            if (inv == null)
                return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Purchase invoice not found.");
            if (inv.AmountMinor <= 0)
                return LedgerResult.Success(0, 0);

            int companyId = inv.CompanyId;
            GlJournal existing = _repo.GetJournalBySource(companyId, LedgerCodes.SourcePurchase, PurchaseCodes.DocInvoice, inv.InvoiceId);
            if (existing != null && existing.Status == LedgerCodes.JournalPosted)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);

            long ap = _store.ApAccountId(companyId);
            PurGoodsReceipt gr = _store.GetReceipt(inv.ReceiptId);
            long vendorItem = 0;
            long cat = 0;
            if (gr != null)
            {
                IList<PurLine> lines = _store.ListLines("PurGoodsReceiptLine", "ReceiptID", gr.ReceiptId);
                if (lines.Count > 0)
                {
                    Inventory.Domain.InvItem item = _inv.GetItem(lines[0].ItemId);
                    if (item != null) { vendorItem = item.ItemId; cat = item.CategoryId; }
                }
            }
            long grir = _inv.ResolveAccount(companyId, vendorItem, cat, Inventory.Domain.InventoryCodes.RoleGrir);
            if (grir <= 0 || ap <= 0)
                return LedgerResult.Fail(LedgerErrorCodes.MappingMissing, "GR/IR or AP account is missing.");

            PostJournalCommand cmd = new PostJournalCommand
            {
                CompanyId = companyId,
                CenterId = inv.CenterId,
                PostingDate = inv.InvoiceDate,
                DocumentDate = inv.InvoiceDate,
                ReferenceNumber = inv.DocNo,
                Description = "Purchase invoice " + inv.DocNo,
                JournalSource = LedgerCodes.SourcePurchase,
                SourceModule = LedgerCodes.SourcePurchase,
                SourceDocumentType = PurchaseCodes.DocInvoice,
                SourceDocumentId = inv.InvoiceId,
                Lines = new List<JournalLineDraft>
                {
                    Line(1, grir, inv.AmountMinor, 0),
                    Line(2, ap, 0, inv.AmountMinor)
                }
            };
            return CaseManagement.Trade.GlOutboxPostHelper.PostOrApprove(_gl, cmd, identity);
        }

        private LedgerResult Reverse(AccOutboxRow row, ILedgerIdentity identity)
        {
            PurInvoice inv = _store.GetInvoice(row.DocumentId);
            int companyId = inv != null ? inv.CompanyId : LedgerCodes.DefaultCompanyId;
            GlJournal existing = _repo.GetJournalBySource(companyId, LedgerCodes.SourcePurchase, PurchaseCodes.DocInvoice, row.DocumentId);
            if (existing == null) return LedgerResult.Success(0, 0);
            if (existing.Status == LedgerCodes.JournalReversed)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);
            return _gl.Reverse(new ReverseJournalCommand
            {
                JournalId = existing.JournalId,
                ExpectedRowVersion = existing.RowVersion,
                PostingDate = existing.PostingDate
            }, identity);
        }

        private static JournalLineDraft Line(int no, long accountId, long debit, long credit)
        {
            return new JournalLineDraft { LineNo = no, AccountId = accountId, DebitMinor = debit, CreditMinor = credit };
        }
    }
}

using System;
using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;
using CaseManagement.Inventory.Domain;
using CaseManagement.Inventory.Infrastructure;

namespace CaseManagement.Inventory.Application
{
    /// <summary>
    /// Outbox handler. Copies stored ValueMinor onto GL drafts. Never calculates average cost.
    /// </summary>
    public class InventoryOutboxAdapter : IOutboxHandler
    {
        private readonly InventoryStore _store;
        private readonly LedgerRepository _repo;
        private readonly IGeneralLedger _gl;

        public InventoryOutboxAdapter()
            : this(new InventoryStore(), new LedgerRepository(), new PostingEngine()) { }

        public InventoryOutboxAdapter(InventoryStore store, LedgerRepository repo, IGeneralLedger gl)
        {
            _store = store;
            _repo = repo;
            _gl = gl;
        }

        public bool CanHandle(string sourceModule)
        {
            return sourceModule == LedgerCodes.SourceInventory;
        }

        public LedgerResult Handle(AccOutboxRow row, ILedgerIdentity identity)
        {
            if (row.DocumentType != InventoryCodes.DocInvDocument)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Unsupported inventory document type.");
            if (row.Operation == LedgerCodes.OutboxReverse)
                return Reverse(row, identity);
            return Post(row, identity);
        }

        private LedgerResult Post(AccOutboxRow row, ILedgerIdentity identity)
        {
            InvDocument doc = _store.GetDocument(row.DocumentId);
            if (doc == null)
                return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Inventory document not found.");

            int companyId = doc.CompanyId > 0 ? doc.CompanyId : LedgerCodes.DefaultCompanyId;
            GlJournal existing = _repo.GetJournalBySource(companyId, LedgerCodes.SourceInventory,
                InventoryCodes.DocInvDocument, doc.DocumentId);
            if (existing != null && existing.Status == LedgerCodes.JournalPosted)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);
            if (existing != null && existing.Status == LedgerCodes.JournalReversed)
                return LedgerResult.Fail(LedgerErrorCodes.AlreadyReversed, "Inventory source journal was reversed.");

            IList<InvItemLedger> ledgers = _store.ListLedgerForDocument(doc.DocumentId);
            long absValue = 0;
            for (int i = 0; i < ledgers.Count; i++)
            {
                long v = ledgers[i].ValueMinor;
                absValue += v < 0 ? -v : v;
            }
            if (absValue == 0)
                return LedgerResult.Success(0, 0);

            long debitInv = 0;
            long creditInv = 0;
            long cogs = 0;
            long gain = 0;
            long loss = 0;
            long opening = 0;
            long invAcc = 0;
            long cogsAcc = 0;
            long gainAcc = 0;
            long lossAcc = 0;
            long openAcc = 0;

            for (int i = 0; i < ledgers.Count; i++)
            {
                InvItemLedger led = ledgers[i];
                InvItem item = _store.GetItem(led.ItemId);
                if (item == null)
                    return LedgerResult.Fail(LedgerErrorCodes.MappingMissing, "Item missing for ledger row.");
                long v = led.ValueMinor;
                long abs = v < 0 ? -v : v;
                if (abs == 0) continue;
                long inventoryId = _store.ResolveAccount(companyId, item.ItemId, item.CategoryId, InventoryCodes.RoleInventory);
                if (inventoryId <= 0)
                    return LedgerResult.Fail(LedgerErrorCodes.MappingMissing, "Inventory map missing.");
                invAcc = inventoryId;
                if (v > 0) debitInv += v;
                else creditInv += abs;

                string role = InventoryValidationService.OffsetRole(doc, v > 0);
                long offsetId = _store.ResolveAccount(companyId, item.ItemId, item.CategoryId, role);
                if (offsetId <= 0)
                    return LedgerResult.Fail(LedgerErrorCodes.MappingMissing, role + " map missing.");
                if (role == InventoryCodes.RoleCogs) { cogs += abs; cogsAcc = offsetId; }
                else if (role == InventoryCodes.RoleAdjGain) { gain += abs; gainAcc = offsetId; }
                else if (role == InventoryCodes.RoleAdjLoss) { loss += abs; lossAcc = offsetId; }
                else if (role == InventoryCodes.RoleOpeningOffset || role == InventoryCodes.RoleGrir)
                { opening += abs; openAcc = offsetId; }
            }

            List<JournalLineDraft> lines = new List<JournalLineDraft>();
            int n = 1;
            if (debitInv > 0) lines.Add(Line(n++, invAcc, debitInv, 0));
            if (creditInv > 0) lines.Add(Line(n++, invAcc, 0, creditInv));
            if (cogs > 0) lines.Add(Line(n++, cogsAcc, cogs, 0));
            if (gain > 0) lines.Add(Line(n++, gainAcc, 0, gain));
            if (loss > 0) lines.Add(Line(n++, lossAcc, loss, 0));
            if (opening > 0) lines.Add(Line(n++, openAcc, 0, opening));

            PostJournalCommand cmd = new PostJournalCommand
            {
                CompanyId = companyId,
                CenterId = doc.CenterId,
                PostingDate = doc.PostingDate,
                DocumentDate = doc.PostingDate,
                ReferenceNumber = doc.DocNo,
                Description = string.IsNullOrWhiteSpace(doc.Description) ? (doc.DocumentType + " " + doc.DocNo) : doc.Description,
                JournalSource = LedgerCodes.SourceInventory,
                SourceModule = LedgerCodes.SourceInventory,
                SourceDocumentType = InventoryCodes.DocInvDocument,
                SourceDocumentId = doc.DocumentId,
                Lines = lines
            };

            LedgerResult posted = _gl.Post(cmd, identity);
            if (posted.Ok) return posted;
            if (posted.ErrorCode != LedgerErrorCodes.ApprovalRequired) return posted;
            if (!identity.HasPermission(LedgerPermissions.Create) || !identity.HasPermission(LedgerPermissions.Approve))
                return posted;

            SaveDraftJournalCommand draft = new SaveDraftJournalCommand
            {
                CompanyId = cmd.CompanyId,
                CenterId = cmd.CenterId,
                PostingDate = cmd.PostingDate,
                DocumentDate = cmd.DocumentDate,
                ReferenceNumber = cmd.ReferenceNumber,
                Description = cmd.Description,
                JournalSource = cmd.JournalSource,
                SourceModule = cmd.SourceModule,
                SourceDocumentType = cmd.SourceDocumentType,
                SourceDocumentId = cmd.SourceDocumentId,
                Lines = cmd.Lines
            };
            LedgerResult saved = _gl.SaveDraft(draft, identity);
            if (!saved.Ok) return saved;
            LedgerResult approved = _gl.Approve(new JournalStatusCommand { JournalId = saved.JournalId, ExpectedRowVersion = saved.RowVersion }, identity);
            if (!approved.Ok) return approved;
            return _gl.Post(new PostJournalCommand
            {
                JournalId = approved.JournalId > 0 ? approved.JournalId : saved.JournalId,
                ExpectedRowVersion = approved.RowVersion,
                CompanyId = companyId,
                CenterId = doc.CenterId
            }, identity);
        }

        private LedgerResult Reverse(AccOutboxRow row, ILedgerIdentity identity)
        {
            InvDocument doc = _store.GetDocument(row.DocumentId);
            int companyId = doc != null ? doc.CompanyId : (identity != null ? identity.CompanyId : LedgerCodes.DefaultCompanyId);
            GlJournal existing = _repo.GetJournalBySource(companyId, LedgerCodes.SourceInventory,
                InventoryCodes.DocInvDocument, row.DocumentId);
            if (existing == null)
                return LedgerResult.Success(0, 0);
            if (existing.Status == LedgerCodes.JournalReversed)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);
            if (existing.Status != LedgerCodes.JournalPosted)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Only a posted journal can be reversed.");
            return _gl.Reverse(new ReverseJournalCommand
            {
                JournalId = existing.JournalId,
                ExpectedRowVersion = existing.RowVersion,
                PostingDate = existing.PostingDate
            }, identity);
        }

        private static JournalLineDraft Line(int no, long accountId, long debit, long credit)
        {
            return new JournalLineDraft
            {
                LineNo = no,
                AccountId = accountId,
                DebitMinor = debit,
                CreditMinor = credit
            };
        }
    }
}

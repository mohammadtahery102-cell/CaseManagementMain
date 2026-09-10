using System;
using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;

namespace CaseManagement.Accounting.Ledger.Application
{
    public class CashBookGlService : ICashBookGlService
    {
        private readonly LedgerRepository _repo;
        private readonly CashBookReadRepository _acc;
        private readonly IGeneralLedger _gl;

        public CashBookGlService() : this(new LedgerRepository(), new CashBookReadRepository(), new PostingEngine()) { }

        public CashBookGlService(LedgerRepository repo, CashBookReadRepository acc, IGeneralLedger gl)
        {
            _repo = repo;
            _acc = acc;
            _gl = gl;
        }

        public IList<CashBookTxn> ListTransactions(ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return new List<CashBookTxn>();
            int center = identity.IsSuperAdmin && identity.CenterId == 0 ? 0 : identity.CenterId;
            return _acc.List(center);
        }

        public IList<GlCashBookMap> ListMaps(int companyId, string mapKind)
        {
            return _repo.ListMaps(companyId, mapKind ?? "");
        }

        public IList<KeyValuePair<long, string>> ListMappableSources(string mapKind, ILedgerIdentity identity)
        {
            int center = identity != null && identity.IsSuperAdmin && identity.CenterId == 0 ? 0 : (identity == null ? 0 : identity.CenterId);
            if (mapKind == LedgerCodes.MapFund) return _acc.ListFunds(center);
            if (mapKind == LedgerCodes.MapIncome) return _acc.ListIncomeCategories();
            if (mapKind == LedgerCodes.MapExpense) return _acc.ListExpenseCategories();
            return new List<KeyValuePair<long, string>>();
        }

        public LedgerResult UpsertMap(UpsertCashBookMapCommand command, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.MapCashBook))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.MapCashBook);
            if (command == null || string.IsNullOrWhiteSpace(command.MapKind) || command.SourceId <= 0 || command.AccountId <= 0)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Map kind, source, and account are required.");

            GlAccount account = _repo.GetAccount(command.AccountId);
            if (account == null || account.IsDeleted || !account.IsActive || !account.IsLeaf)
                return LedgerResult.Fail(LedgerErrorCodes.AccountNotLeaf, "Map target must be an active leaf account.");

            if (command.MapKind == LedgerCodes.MapFund && account.AccountTypeCode != LedgerCodes.TypeAsset)
                return LedgerResult.Fail(LedgerErrorCodes.TypeMismatch, "Fund map must be an ASSET account.");
            if (command.MapKind == LedgerCodes.MapIncome && account.AccountTypeCode != LedgerCodes.TypeRevenue)
                return LedgerResult.Fail(LedgerErrorCodes.TypeMismatch, "Income map must be a REVENUE account.");
            if (command.MapKind == LedgerCodes.MapExpense && account.AccountTypeCode != LedgerCodes.TypeExpense)
                return LedgerResult.Fail(LedgerErrorCodes.TypeMismatch, "Expense map must be an EXPENSE account.");

            int companyId = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            GlCashBookMap existing = _repo.GetMap(companyId, command.MapKind, command.SourceId);
            if (existing == null)
            {
                long id = _repo.InsertMap(new GlCashBookMap
                {
                    CompanyId = companyId,
                    MapKind = command.MapKind,
                    SourceId = command.SourceId,
                    AccountId = command.AccountId
                }, now, identity.UserName);
                _repo.InsertMasterAudit("MapCashBook", "GlCashBookMap", id, null, command.MapKind, identity);
                return LedgerResult.Entity(id, 1);
            }

            existing.AccountId = command.AccountId;
            if (!_repo.UpdateMapConcurrency(existing, command.ExpectedRowVersion > 0 ? command.ExpectedRowVersion : existing.RowVersion, now, identity.UserName))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            return LedgerResult.Entity(existing.MapId, existing.RowVersion + 1);
        }

        public LedgerResult PostTransaction(long txnId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.Post))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Post);

            CashBookTxn txn = _acc.GetTransaction(txnId);
            if (txn == null)
                return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Cash-book transaction not found.");
            if (txn.IsReversed)
                return ReverseIfVoided(txnId, identity);

            int companyId = identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId;
            GlJournal existing = _repo.GetJournalBySource(companyId, LedgerCodes.SourceCashBook, LedgerCodes.DocAccTransaction, txn.TxnId);
            if (existing != null && existing.Status == LedgerCodes.JournalPosted)
                return Ok(existing);
            if (existing != null && existing.Status == LedgerCodes.JournalReversed)
                return LedgerResult.Fail(LedgerErrorCodes.AlreadyReversed, "Source journal was reversed; Acc row must be a new TxnID.");

            if (string.IsNullOrEmpty(txn.PostingDateIso))
                return LedgerResult.Fail(LedgerErrorCodes.InvalidDate, "Cannot convert cash-book date to ISO.");
            if (!txn.FundId.HasValue || !txn.CategoryId.HasValue)
                return LedgerResult.Fail(LedgerErrorCodes.MappingMissing, "Fund and category are required.");

            string catKind = txn.Direction == LedgerCodes.AccReceipt ? LedgerCodes.MapIncome : LedgerCodes.MapExpense;
            if (txn.Direction != LedgerCodes.AccReceipt && txn.Direction != LedgerCodes.AccPayment)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Unknown cash-book direction.");

            GlCashBookMap fundMap = _repo.GetMap(companyId, LedgerCodes.MapFund, txn.FundId.Value);
            GlCashBookMap catMap = _repo.GetMap(companyId, catKind, txn.CategoryId.Value);
            if (fundMap == null || catMap == null)
                return LedgerResult.Fail(LedgerErrorCodes.MappingMissing, "Fund or category is not mapped.");

            GlCompany company = _repo.GetCompany(companyId);
            int units = company != null ? company.MinorUnits : 2;
            long minor = LedgerTime.ToMinorUnits(txn.AmountMajor, units);
            if (minor <= 0)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Amount must be greater than zero.");

            int center = txn.CenterId > 0 ? txn.CenterId : identity.CenterId;
            if (center <= 0)
                return LedgerResult.Fail(LedgerErrorCodes.CenterRequired, "Journal CenterID must be a real branch.");

            long fundAcc = fundMap.AccountId;
            long catAcc = catMap.AccountId;
            bool receipt = txn.Direction == LedgerCodes.AccReceipt;

            PostJournalCommand cmd = new PostJournalCommand
            {
                CompanyId = companyId,
                CenterId = center,
                PostingDate = txn.PostingDateIso,
                DocumentDate = txn.PostingDateIso,
                ReferenceNumber = txn.DocNo,
                Description = string.IsNullOrWhiteSpace(txn.Description) ? (txn.Direction + " " + txn.DocNo) : txn.Description,
                JournalSource = LedgerCodes.SourceCashBook,
                SourceModule = LedgerCodes.SourceCashBook,
                SourceDocumentType = LedgerCodes.DocAccTransaction,
                SourceDocumentId = txn.TxnId,
                Lines = new List<JournalLineDraft>
                {
                    Line(1, receipt ? fundAcc : catAcc, minor, 0, txn),
                    Line(2, receipt ? catAcc : fundAcc, 0, minor, txn)
                }
            };

            LedgerResult posted = _gl.Post(cmd, identity);
            if (posted.Ok) return posted;
            if (posted.ErrorCode != LedgerErrorCodes.ApprovalRequired)
                return posted;

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
                CenterId = center
            }, identity);
        }

        public LedgerResult ReverseIfVoided(long txnId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.Reverse))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Reverse);

            CashBookTxn txn = _acc.GetTransaction(txnId);
            if (txn == null)
                return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Cash-book transaction not found.");
            if (!txn.IsReversed)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Acc row is not voided.");

            int companyId = identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId;
            GlJournal existing = _repo.GetJournalBySource(companyId, LedgerCodes.SourceCashBook, LedgerCodes.DocAccTransaction, txn.TxnId);
            if (existing == null)
                return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "No GL journal for this cash-book row.");
            if (existing.Status == LedgerCodes.JournalReversed)
                return Ok(existing);
            if (existing.Status != LedgerCodes.JournalPosted)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Only a posted journal can be reversed.");

            return _gl.Reverse(new ReverseJournalCommand
            {
                JournalId = existing.JournalId,
                ExpectedRowVersion = existing.RowVersion,
                PostingDate = existing.PostingDate
            }, identity);
        }

        private static JournalLineDraft Line(int no, long accountId, long debit, long credit, CashBookTxn txn)
        {
            return new JournalLineDraft
            {
                LineNo = no,
                AccountId = accountId,
                DebitMinor = debit,
                CreditMinor = credit,
                FundId = txn.FundId,
                PartyId = txn.PartyId,
                Description = txn.Description
            };
        }

        private static LedgerResult Ok(GlJournal j)
        {
            LedgerResult r = LedgerResult.Success(j.JournalId, j.RowVersion);
            r.Journal = j;
            return r;
        }
    }
}

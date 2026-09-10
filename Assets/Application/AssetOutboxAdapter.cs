using System;
using System.Collections.Generic;
using System.Data;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;
using CaseManagement.Assets.Domain;
using CaseManagement.Assets.Infrastructure;

namespace CaseManagement.Assets.Application
{
    public class AssetOutboxAdapter : IOutboxHandler
    {
        private readonly AssetStore _store;
        private readonly LedgerRepository _repo;
        private readonly IGeneralLedger _gl;

        public AssetOutboxAdapter()
            : this(new AssetStore(), new LedgerRepository(), new PostingEngine()) { }

        public AssetOutboxAdapter(AssetStore store, LedgerRepository repo, IGeneralLedger gl)
        {
            _store = store;
            _repo = repo;
            _gl = gl;
        }

        public bool CanHandle(string sourceModule)
        {
            return sourceModule == LedgerCodes.SourceFixedAsset;
        }

        public LedgerResult Handle(AccOutboxRow row, ILedgerIdentity identity)
        {
            if (row.Operation == LedgerCodes.OutboxReverse)
                return Reverse(row, identity);
            if (row.DocumentType == AssetCodes.DocAcquisition) return PostAcquisition(row, identity);
            if (row.DocumentType == AssetCodes.DocDepreciation) return PostDepreciation(row, identity);
            if (row.DocumentType == AssetCodes.DocDisposal) return PostDisposal(row, identity);
            return LedgerResult.Fail(LedgerErrorCodes.Validation, "Unsupported asset document type.");
        }

        private LedgerResult PostAcquisition(AccOutboxRow row, ILedgerIdentity identity)
        {
            DataRow d = _store.GetHeader("FaAcquisition", "AcquisitionID", row.DocumentId);
            if (d == null) return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Acquisition not found.");
            int companyId = Convert.ToInt32(d["CompanyID"]);
            long amount = Convert.ToInt64(d["AmountMinor"]);
            if (amount <= 0) return LedgerResult.Success(0, 0);
            return Post(companyId, Convert.ToInt32(d["CenterID"]), Convert.ToString(d["DocDate"]), Convert.ToString(d["DocNo"]),
                AssetCodes.DocAcquisition, row.DocumentId, identity,
                Line(1, _store.Account(companyId, "AssetAccountID"), amount, 0),
                Line(2, _store.Account(companyId, "CashAccountID"), 0, amount));
        }

        private LedgerResult PostDepreciation(AccOutboxRow row, ILedgerIdentity identity)
        {
            DataRow d = _store.GetHeader("FaDepreciation", "DepreciationID", row.DocumentId);
            if (d == null) return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Depreciation not found.");
            int companyId = Convert.ToInt32(d["CompanyID"]);
            long amount = Convert.ToInt64(d["TotalMinor"]);
            if (amount <= 0) return LedgerResult.Success(0, 0);
            return Post(companyId, Convert.ToInt32(d["CenterID"]), Convert.ToString(d["DocDate"]), Convert.ToString(d["DocNo"]),
                AssetCodes.DocDepreciation, row.DocumentId, identity,
                Line(1, _store.Account(companyId, "DepExpAccountID"), amount, 0),
                Line(2, _store.Account(companyId, "AccumAccountID"), 0, amount));
        }

        private LedgerResult PostDisposal(AccOutboxRow row, ILedgerIdentity identity)
        {
            DataRow d = _store.GetHeader("FaDisposal", "DisposalID", row.DocumentId);
            if (d == null) return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Disposal not found.");
            int companyId = Convert.ToInt32(d["CompanyID"]);
            long cost = Convert.ToInt64(d["CostMinor"]);
            long accum = Convert.ToInt64(d["AccumMinor"]);
            long nbv = cost - accum;
            List<JournalLineDraft> lines = new List<JournalLineDraft>();
            int n = 1;
            if (accum > 0) lines.Add(Line(n++, _store.Account(companyId, "AccumAccountID"), accum, 0));
            if (nbv > 0) lines.Add(Line(n++, _store.Account(companyId, "DisposalLossAccountID"), nbv, 0));
            lines.Add(Line(n, _store.Account(companyId, "AssetAccountID"), 0, cost));
            return Post(companyId, Convert.ToInt32(d["CenterID"]), Convert.ToString(d["DocDate"]), Convert.ToString(d["DocNo"]),
                AssetCodes.DocDisposal, row.DocumentId, identity, lines.ToArray());
        }

        private LedgerResult Post(int companyId, int centerId, string date, string docNo, string type, long id, ILedgerIdentity identity, params JournalLineDraft[] lines)
        {
            GlJournal existing = _repo.GetJournalBySource(companyId, LedgerCodes.SourceFixedAsset, type, id);
            if (existing != null && existing.Status == LedgerCodes.JournalPosted)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].AccountId <= 0)
                    return LedgerResult.Fail(LedgerErrorCodes.MappingMissing, "Fixed asset account map is missing.");
            }
            PostJournalCommand cmd = new PostJournalCommand
            {
                CompanyId = companyId,
                CenterId = centerId,
                PostingDate = date,
                DocumentDate = date,
                ReferenceNumber = docNo,
                Description = type + " " + docNo,
                JournalSource = LedgerCodes.SourceFixedAsset,
                SourceModule = LedgerCodes.SourceFixedAsset,
                SourceDocumentType = type,
                SourceDocumentId = id,
                Lines = new List<JournalLineDraft>(lines)
            };
            return CaseManagement.Trade.GlOutboxPostHelper.PostOrApprove(_gl, cmd, identity);
        }

        private LedgerResult Reverse(AccOutboxRow row, ILedgerIdentity identity)
        {
            GlJournal existing = _repo.GetJournalBySource(LedgerCodes.DefaultCompanyId, LedgerCodes.SourceFixedAsset, row.DocumentType, row.DocumentId);
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

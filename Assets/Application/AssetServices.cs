using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Assets.Domain;
using CaseManagement.Assets.Infrastructure;
using CaseManagement.Trade;

namespace CaseManagement.Assets.Application
{
    public class AssetService
    {
        private readonly AssetStore _store;
        public AssetService() : this(new AssetStore()) { }
        public AssetService(AssetStore store) { _store = store; }

        public TradeResult CreateCategory(string code, string name, ILedgerIdentity identity)
        {
            return Master("FaCategory", code, name, identity);
        }

        public TradeResult CreateLocation(string code, string name, ILedgerIdentity identity)
        {
            return Master("FaLocation", code, name, identity);
        }

        public TradeResult CreateCustodian(string code, string name, ILedgerIdentity identity)
        {
            return Master("FaCustodian", code, name, identity);
        }

        private TradeResult Master(string table, string code, string name, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(AssetPermissions.Create))
                return TradeResult.Fail("PERMISSION", AssetPermissions.Create);
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                return TradeResult.Fail("VALIDATION", "Code and name are required.");
            int c = identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId;
            long id = _store.InsertMaster(table, c, identity.CenterId, code, name, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            return id > 0 ? TradeResult.Success(id, 1) : TradeResult.Fail("VALIDATION", "Could not create master.");
        }

        public TradeResult CreateAcquisition(FaAsset asset, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(AssetPermissions.Create))
                return TradeResult.Fail("PERMISSION", AssetPermissions.Create);
            if (asset == null || string.IsNullOrWhiteSpace(asset.Name) || asset.CostMinor <= 0 || asset.UsefulLifeMonths <= 0)
                return TradeResult.Fail("VALIDATION", "Asset name, cost and useful life are required.");
            if (!identity.IsSuperAdmin && identity.CenterId > 0 && asset.CenterId > 0 && identity.CenterId != asset.CenterId)
                return TradeResult.Fail("PERMISSION", "Cross-branch assets are not allowed.");
            asset.CompanyId = asset.CompanyId > 0 ? asset.CompanyId : (identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId);
            asset.CenterId = asset.CenterId > 0 ? asset.CenterId : (identity.CenterId > 0 ? identity.CenterId : 1);
            if (asset.CategoryId <= 0) asset.CategoryId = _store.DefaultId("FaCategory", "CategoryID", asset.CompanyId);
            if (asset.LocationId <= 0) asset.LocationId = _store.DefaultId("FaLocation", "LocationID", asset.CompanyId);
            if (asset.CustodianId <= 0) asset.CustodianId = _store.DefaultId("FaCustodian", "CustodianID", asset.CompanyId);
            if (string.IsNullOrWhiteSpace(asset.Code)) asset.Code = "A" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string now = LedgerTime.UtcNow(identity.UtcNow);
            string date = identity.UtcNow.ToString("yyyy-MM-dd");
            long acqId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                long assetId = _store.InsertAsset(con, tr, asset, date, now, identity.UserName);
                string no = _store.AllocateNo(con, tr, asset.CompanyId, "AQ");
                acqId = _store.InsertDoc(con, tr, "FaAcquisition", ", AmountMinor", ", @amt", asset.CompanyId, asset.CenterId, no, TradeCodes.Draft, date, assetId, now, identity.UserName,
                    new SQLiteParameter("@amt", asset.CostMinor));
            });
            DocumentWorkflowService.Ensure(AssetCodes.EntityAcquisition, acqId, asset.CenterId, identity);
            return TradeResult.Success(acqId, 1);
        }

        public TradeResult SubmitAcquisition(long id, ILedgerIdentity identity)
        { return Move("FaAcquisition", "AcquisitionID", id, AssetCodes.EntityAcquisition, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, AssetPermissions.Create); }

        public TradeResult ApproveAcquisition(long id, ILedgerIdentity identity)
        { return Move("FaAcquisition", "AcquisitionID", id, AssetCodes.EntityAcquisition, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, AssetPermissions.Approve); }

        public TradeResult PostAcquisition(long id, ILedgerIdentity identity)
        {
            return PostDoc("FaAcquisition", "AcquisitionID", id, AssetCodes.EntityAcquisition, AssetCodes.DocAcquisition, identity);
        }

        public TradeResult Transfer(long assetId, long toLocationId, long toCustodianId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(AssetPermissions.Create))
                return TradeResult.Fail("PERMISSION", AssetPermissions.Create);
            FaAsset a = _store.GetAsset(assetId);
            if (a == null || a.Status != AssetCodes.StatusActive)
                return TradeResult.Fail("VALIDATION", "Active asset is required.");
            if (!identity.IsSuperAdmin && identity.CenterId > 0 && identity.CenterId != a.CenterId)
                return TradeResult.Fail("PERMISSION", "Cross-branch assets are not allowed.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                string no = _store.AllocateNo(con, tr, a.CompanyId, "TR");
                newId = _store.InsertDoc(con, tr, "FaTransfer", ", FromLocationID, ToLocationID, FromCustodianID, ToCustodianID", ", @fl, @tl, @fc, @tc",
                    a.CompanyId, a.CenterId, no, TradeCodes.Posted, identity.UtcNow.ToString("yyyy-MM-dd"), assetId, now, identity.UserName,
                    new SQLiteParameter("@fl", a.LocationId), new SQLiteParameter("@tl", toLocationId),
                    new SQLiteParameter("@fc", a.CustodianId), new SQLiteParameter("@tc", toCustodianId));
            });
            _store.MoveAsset(assetId, toLocationId, toCustodianId, now, identity.UserName);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult CreateDisposal(long assetId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(AssetPermissions.Create))
                return TradeResult.Fail("PERMISSION", AssetPermissions.Create);
            FaAsset a = _store.GetAsset(assetId);
            if (a == null || a.Status != AssetCodes.StatusActive)
                return TradeResult.Fail("VALIDATION", "Active asset is required.");
            TradeResult isoDisp = TradeIsolation.DenyIfCrossTenant(identity, a.CompanyId, a.CenterId);
            if (!isoDisp.Ok) return isoDisp;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                string no = _store.AllocateNo(con, tr, a.CompanyId, "DS");
                newId = _store.InsertDoc(con, tr, "FaDisposal", ", CostMinor, AccumMinor", ", @cost, @acc",
                    a.CompanyId, a.CenterId, no, TradeCodes.Draft, identity.UtcNow.ToString("yyyy-MM-dd"), assetId, now, identity.UserName,
                    new SQLiteParameter("@cost", a.CostMinor), new SQLiteParameter("@acc", a.AccumDepMinor));
            });
            DocumentWorkflowService.Ensure(AssetCodes.EntityDisposal, newId, a.CenterId, identity);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult SubmitDisposal(long id, ILedgerIdentity identity)
        { return Move("FaDisposal", "DisposalID", id, AssetCodes.EntityDisposal, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, AssetPermissions.Create); }

        public TradeResult ApproveDisposal(long id, ILedgerIdentity identity)
        { return Move("FaDisposal", "DisposalID", id, AssetCodes.EntityDisposal, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, AssetPermissions.Approve); }

        public TradeResult PostDisposal(long id, ILedgerIdentity identity)
        {
            DataRow row = _store.GetHeader("FaDisposal", "DisposalID", id);
            if (row == null) return TradeResult.Fail("NOT_FOUND", "Disposal not found.");
            TradeResult posted = PostDoc("FaDisposal", "DisposalID", id, AssetCodes.EntityDisposal, AssetCodes.DocDisposal, identity);
            if (!posted.Ok) return posted;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                _store.DisposeAsset(con, tr, Convert.ToInt64(row["AssetID"]));
            });
            return posted;
        }

        public FaAsset GetAsset(long id) { return _store.GetAsset(id); }
        public FaAcquisition GetAcquisition(long id)
        {
            DataRow r = _store.GetHeader("FaAcquisition", "AcquisitionID", id);
            if (r == null) return null;
            FaAcquisition d = new FaAcquisition();
            d.AcquisitionId = Convert.ToInt64(r["AcquisitionID"]);
            d.CompanyId = Convert.ToInt32(r["CompanyID"]);
            d.CenterId = Convert.ToInt32(r["CenterID"]);
            d.DocNo = Convert.ToString(r["DocNo"]);
            d.Status = Convert.ToString(r["Status"]);
            d.DocDate = Convert.ToString(r["DocDate"]);
            d.AssetId = Convert.ToInt64(r["AssetID"]);
            d.AmountMinor = Convert.ToInt64(r["AmountMinor"]);
            d.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return d;
        }

        public IList<AssetRegisterRow> Register(ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(AssetPermissions.View)) return new List<AssetRegisterRow>();
            return _store.Register(Co(identity), Ctr(identity));
        }

        public IList<AssetMovementRow> Movements(ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(AssetPermissions.View)) return new List<AssetMovementRow>();
            return _store.Movements(Co(identity), Ctr(identity));
        }

        private TradeResult Move(string table, string idCol, long id, string entity, string from, string to, string wf, ILedgerIdentity identity, string perm)
        {
            if (identity == null || !identity.HasPermission(perm))
                return TradeResult.Fail("PERMISSION", perm);
            DataRow row = _store.GetHeader(table, idCol, id);
            if (row == null) return TradeResult.Fail("NOT_FOUND", table);
            if (Convert.ToString(row["Status"]) != from) return TradeResult.Fail("INVALID_STATUS", "Expected " + from);
            int center = Convert.ToInt32(row["CenterID"]);
            int company = Convert.ToInt32(row["CompanyID"]);
            TradeResult iso = TradeIsolation.DenyIfCrossTenant(identity, company, center);
            if (!iso.Ok) return iso;
            if (!_store.UpdateStatus(table, idCol, id, to, Convert.ToInt64(row["RowVersion"]), LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(entity, id, center, wf, identity);
            return TradeResult.Success(id, 0);
        }

        private TradeResult PostDoc(string table, string idCol, long id, string entity, string docType, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(AssetPermissions.Post))
                return TradeResult.Fail("PERMISSION", AssetPermissions.Post);
            DataRow row = _store.GetHeader(table, idCol, id);
            if (row == null) return TradeResult.Fail("NOT_FOUND", table);
            string status = Convert.ToString(row["Status"]);
            int companyId = Convert.ToInt32(row["CompanyID"]);
            int center = Convert.ToInt32(row["CenterID"]);
            TradeResult iso = TradeIsolation.DenyIfCrossTenant(identity, companyId, center);
            if (!iso.Ok) return iso;
            if (status != TradeCodes.Approved && !(!_store.RequiresApproval(companyId) && status == TradeCodes.Draft))
                return TradeResult.Fail("INVALID_STATUS", "Document must be Approved.");
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                AccOutboxWriter.Enqueue(con, tr, LedgerCodes.SourceFixedAsset, docType, id, LedgerCodes.OutboxPost, companyId, center, identity.UserName);
            });
            if (!_store.UpdateStatus(table, idCol, id, TradeCodes.Posted, Convert.ToInt64(row["RowVersion"]), LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(entity, id, center, "POSTED", identity);
            return TradeResult.Success(id, 0);
        }

        private static int Co(ILedgerIdentity identity)
        {
            return identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId;
        }

        private static int Ctr(ILedgerIdentity identity)
        {
            if (identity.IsSuperAdmin && identity.CenterId == 0) return 0;
            return identity.CenterId;
        }
    }

    public class DepreciationService
    {
        private readonly AssetStore _store;
        public DepreciationService() : this(new AssetStore()) { }
        public DepreciationService(AssetStore store) { _store = store; }

        public TradeResult Run(int companyId, int centerId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(AssetPermissions.Create))
                return TradeResult.Fail("PERMISSION", AssetPermissions.Create);
            companyId = companyId > 0 ? companyId : LedgerCodes.DefaultCompanyId;
            centerId = centerId > 0 ? centerId : (identity.CenterId > 0 ? identity.CenterId : 1);
            IList<FaAsset> assets = _store.ListActive(companyId);
            long total = 0;
            List<FaAsset> lines = new List<FaAsset>();
            for (int i = 0; i < assets.Count; i++)
            {
                FaAsset a = assets[i];
                if (a.UsefulLifeMonths <= 0 || a.NbvMinor <= 0) continue;
                long amt = a.CostMinor / a.UsefulLifeMonths;
                if (amt > a.NbvMinor) amt = a.NbvMinor;
                if (amt <= 0) continue;
                a.AccumDepMinor = amt;
                total += amt;
                lines.Add(a);
            }
            if (lines.Count == 0) return TradeResult.Fail("VALIDATION", "No depreciable assets.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                string no = _store.AllocateNo(con, tr, companyId, "DP");
                newId = _store.InsertDoc(con, tr, "FaDepreciation", ", TotalMinor", ", @tot", companyId, centerId, no, TradeCodes.Draft, identity.UtcNow.ToString("yyyy-MM-dd"), 0, now, identity.UserName,
                    new SQLiteParameter("@tot", total));
                for (int i = 0; i < lines.Count; i++)
                    _store.InsertDepLine(con, tr, newId, companyId, lines[i].AssetId, lines[i].AccumDepMinor);
            });
            DocumentWorkflowService.Ensure(AssetCodes.EntityDepreciation, newId, centerId, identity);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult Submit(long id, ILedgerIdentity identity)
        { return Move(id, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, AssetPermissions.Create); }

        public TradeResult Approve(long id, ILedgerIdentity identity)
        { return Move(id, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, AssetPermissions.Approve); }

        public TradeResult Post(long id, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(AssetPermissions.Post))
                return TradeResult.Fail("PERMISSION", AssetPermissions.Post);
            DataRow row = _store.GetHeader("FaDepreciation", "DepreciationID", id);
            if (row == null) return TradeResult.Fail("NOT_FOUND", "Depreciation not found.");
            string status = Convert.ToString(row["Status"]);
            int companyId = Convert.ToInt32(row["CompanyID"]);
            if (status != TradeCodes.Approved && !(!_store.RequiresApproval(companyId) && status == TradeCodes.Draft))
                return TradeResult.Fail("INVALID_STATUS", "Document must be Approved.");
            int center = Convert.ToInt32(row["CenterID"]);
            IList<DataRow> lines = _store.DepLines(id);
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                for (int i = 0; i < lines.Count; i++)
                    _store.AddAccum(con, tr, Convert.ToInt64(lines[i]["AssetID"]), Convert.ToInt64(lines[i]["AmountMinor"]));
                AccOutboxWriter.Enqueue(con, tr, LedgerCodes.SourceFixedAsset, AssetCodes.DocDepreciation, id, LedgerCodes.OutboxPost, companyId, center, identity.UserName);
            });
            if (!_store.UpdateStatus("FaDepreciation", "DepreciationID", id, TradeCodes.Posted, Convert.ToInt64(row["RowVersion"]), LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(AssetCodes.EntityDepreciation, id, center, "POSTED", identity);
            return TradeResult.Success(id, 0);
        }

        public IList<DepreciationReportRow> Report(ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(AssetPermissions.View)) return new List<DepreciationReportRow>();
            int c = identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId;
            int ctr = identity.IsSuperAdmin && identity.CenterId == 0 ? 0 : identity.CenterId;
            return _store.DepReport(c, ctr);
        }

        private TradeResult Move(long id, string from, string to, string wf, ILedgerIdentity identity, string perm)
        {
            if (identity == null || !identity.HasPermission(perm))
                return TradeResult.Fail("PERMISSION", perm);
            DataRow row = _store.GetHeader("FaDepreciation", "DepreciationID", id);
            if (row == null) return TradeResult.Fail("NOT_FOUND", "Depreciation not found.");
            if (Convert.ToString(row["Status"]) != from) return TradeResult.Fail("INVALID_STATUS", "Expected " + from);
            if (!_store.UpdateStatus("FaDepreciation", "DepreciationID", id, to, Convert.ToInt64(row["RowVersion"]), LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(AssetCodes.EntityDepreciation, id, Convert.ToInt32(row["CenterID"]), wf, identity);
            return TradeResult.Success(id, 0);
        }
    }
}

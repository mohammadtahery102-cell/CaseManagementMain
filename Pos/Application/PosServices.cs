using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Inventory.Application;
using CaseManagement.Inventory.Domain;
using CaseManagement.Pos.Domain;
using CaseManagement.Pos.Infrastructure;
using CaseManagement.Sales.Application;
using CaseManagement.Sales.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Pos.Application
{
    public class PosService
    {
        private readonly PosStore _store;
        private readonly SalesService _sales;
        private readonly CustomerService _customers;
        private readonly InventoryPostingService _inv;
        private readonly InventoryQueryService _invQ;

        public PosService()
            : this(new PosStore(), new SalesService(), new CustomerService(), new InventoryPostingService(), new InventoryQueryService()) { }

        public PosService(PosStore store, SalesService sales, CustomerService customers, InventoryPostingService inv, InventoryQueryService invQ)
        {
            _store = store;
            _sales = sales;
            _customers = customers;
            _inv = inv;
            _invQ = invQ;
        }

        public TradeResult CreateTerminal(string code, string name, long warehouseId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PosPermissions.Create))
                return TradeResult.Fail("PERMISSION", PosPermissions.Create);
            int c = Co(identity);
            int ctr = identity.CenterId > 0 ? identity.CenterId : 1;
            if (warehouseId <= 0) warehouseId = _invQ.DefaultWarehouseId(c);
            long id = _store.InsertTerminal(c, ctr, code, name, warehouseId, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            return id > 0 ? TradeResult.Success(id, 1) : TradeResult.Fail("VALIDATION", "Could not create terminal.");
        }

        public TradeResult CreateDrawer(long terminalId, string code, string name, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PosPermissions.Create))
                return TradeResult.Fail("PERMISSION", PosPermissions.Create);
            DataRow term = _store.GetTerminal(terminalId);
            if (term == null) return TradeResult.Fail("NOT_FOUND", "Terminal not found.");
            long id = _store.InsertDrawer(Convert.ToInt32(term["CompanyID"]), Convert.ToInt32(term["CenterID"]), terminalId, code, name, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            return id > 0 ? TradeResult.Success(id, 1) : TradeResult.Fail("VALIDATION", "Could not create drawer.");
        }

        public TradeResult CreateSale(long terminalId, long drawerId, IList<PosLine> lines, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PosPermissions.Create))
                return TradeResult.Fail("PERMISSION", PosPermissions.Create);
            if (lines == null || lines.Count == 0) return TradeResult.Fail("VALIDATION", "Lines are required.");
            DataRow term = _store.GetTerminal(terminalId);
            if (term == null) return TradeResult.Fail("NOT_FOUND", "Terminal not found.");
            int companyId = Convert.ToInt32(term["CompanyID"]);
            int centerId = Convert.ToInt32(term["CenterID"]);
            if (!identity.IsSuperAdmin && identity.CenterId > 0 && identity.CenterId != centerId)
                return TradeResult.Fail("PERMISSION", "Cross-branch POS is not allowed.");
            long amount = 0;
            for (int i = 0; i < lines.Count; i++) amount += lines[i].Qty * lines[i].UnitPriceMinor;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                PosSale s = new PosSale();
                s.CompanyId = companyId;
                s.CenterId = centerId;
                s.TerminalId = terminalId;
                s.DrawerId = drawerId;
                s.WarehouseId = Convert.ToInt64(term["WarehouseID"]);
                if (s.WarehouseId <= 0) s.WarehouseId = _invQ.DefaultWarehouseId(companyId);
                s.DocNo = _store.AllocateNo(con, tr, companyId, "S");
                s.SaleDate = identity.UtcNow.ToString("yyyy-MM-dd");
                s.AmountMinor = amount;
                newId = _store.InsertSale(con, tr, s, now, identity.UserName);
                for (int i = 0; i < lines.Count; i++)
                    _store.InsertSaleLine(con, tr, newId, companyId, i + 1, lines[i]);
            });
            DocumentWorkflowService.Ensure(PosCodes.EntitySale, newId, centerId, identity);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult SubmitSale(long id, ILedgerIdentity identity)
        { return MoveSale(id, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, PosPermissions.Create); }

        public TradeResult ApproveSale(long id, ILedgerIdentity identity)
        { return MoveSale(id, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, PosPermissions.Approve); }

        public TradeResult PostSale(long id, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PosPermissions.Post))
                return TradeResult.Fail("PERMISSION", PosPermissions.Post);
            PosSale s = _store.GetSale(id);
            if (s == null) return TradeResult.Fail("NOT_FOUND", "Sale not found.");
            TradeResult isoSale = TradeIsolation.DenyIfCrossTenant(identity, s.CompanyId, s.CenterId);
            if (!isoSale.Ok) return isoSale;
            if (s.Status != TradeCodes.Approved && !(!_store.RequiresApproval(s.CompanyId) && s.Status == TradeCodes.Draft))
                return TradeResult.Fail("INVALID_STATUS", "Sale must be Approved.");
            long customerId = EnsureWalkIn(s, identity);
            if (customerId <= 0) return TradeResult.Fail("VALIDATION", "Walk-in customer could not be created.");
            IList<PosLine> posLines = _store.SaleLines(id);
            SalesCommand cmd = new SalesCommand();
            cmd.CompanyId = s.CompanyId;
            cmd.CenterId = s.CenterId;
            cmd.CustomerId = customerId;
            cmd.WarehouseId = s.WarehouseId;
            cmd.Date = s.SaleDate;
            cmd.Lines = new List<SalLine>();
            for (int i = 0; i < posLines.Count; i++)
            {
                SalLine l = new SalLine();
                l.ItemId = posLines[i].ItemId;
                l.Qty = posLines[i].Qty;
                l.UnitPriceMinor = posLines[i].UnitPriceMinor;
                cmd.Lines.Add(l);
            }
            TradeResult order = _sales.CreateOrder(cmd, identity);
            if (!order.Ok) return order;
            TradeResult step = _sales.SubmitOrder(order.EntityId, identity);
            if (!step.Ok) return step;
            step = _sales.ApproveOrder(order.EntityId, identity);
            if (!step.Ok) return step;
            step = _sales.PostOrder(order.EntityId, identity);
            if (!step.Ok) return step;
            TradeResult dn = _sales.CreateDelivery(order.EntityId, identity);
            if (!dn.Ok) return dn;
            step = _sales.SubmitDelivery(dn.EntityId, identity);
            if (!step.Ok) return step;
            step = _sales.ApproveDelivery(dn.EntityId, identity);
            if (!step.Ok) return step;
            step = _sales.PostDelivery(dn.EntityId, identity);
            if (!step.Ok) return step;
            TradeResult inv = _sales.CreateInvoice(dn.EntityId, identity);
            if (!inv.Ok) return inv;
            step = _sales.SubmitInvoice(inv.EntityId, identity);
            if (!step.Ok) return step;
            step = _sales.ApproveInvoice(inv.EntityId, identity);
            if (!step.Ok) return step;
            step = _sales.PostInvoice(inv.EntityId, identity);
            if (!step.Ok) return step;
            _store.SetSaleLinks(id, order.EntityId, dn.EntityId, inv.EntityId, customerId);
            if (!_store.UpdateSaleStatus(id, TradeCodes.Posted, s.RowVersion, LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(PosCodes.EntitySale, id, s.CenterId, "POSTED", identity);
            return TradeResult.Success(id, 0);
        }

        public TradeResult CreateReturn(long saleId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PosPermissions.Create))
                return TradeResult.Fail("PERMISSION", PosPermissions.Create);
            PosSale s = _store.GetSale(saleId);
            if (s == null || s.Status != TradeCodes.Posted)
                return TradeResult.Fail("INVALID_STATUS", "Posted sale is required.");
            IList<PosLine> lines = _store.SaleLines(saleId);
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                PosReturnDoc d = new PosReturnDoc();
                d.CompanyId = s.CompanyId;
                d.CenterId = s.CenterId;
                d.SaleId = saleId;
                d.DocNo = _store.AllocateNo(con, tr, s.CompanyId, "R");
                d.ReturnDate = identity.UtcNow.ToString("yyyy-MM-dd");
                d.AmountMinor = s.AmountMinor;
                newId = _store.InsertReturn(con, tr, d, now, identity.UserName);
                for (int i = 0; i < lines.Count; i++)
                    _store.InsertReturnLine(con, tr, newId, s.CompanyId, i + 1, lines[i]);
            });
            DocumentWorkflowService.Ensure(PosCodes.EntityReturn, newId, s.CenterId, identity);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult SubmitReturn(long id, ILedgerIdentity identity)
        { return MoveReturn(id, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, PosPermissions.Create); }

        public TradeResult ApproveReturn(long id, ILedgerIdentity identity)
        { return MoveReturn(id, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, PosPermissions.Approve); }

        public TradeResult PostReturn(long id, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PosPermissions.Post))
                return TradeResult.Fail("PERMISSION", PosPermissions.Post);
            PosReturnDoc d = _store.GetReturn(id);
            if (d == null) return TradeResult.Fail("NOT_FOUND", "Return not found.");
            TradeResult isoRet = TradeIsolation.DenyIfCrossTenant(identity, d.CompanyId, d.CenterId);
            if (!isoRet.Ok) return isoRet;
            if (d.Status != TradeCodes.Approved && !(!_store.RequiresApproval(d.CompanyId) && d.Status == TradeCodes.Draft))
                return TradeResult.Fail("INVALID_STATUS", "Return must be Approved.");
            PosSale s = _store.GetSale(d.SaleId);
            IList<PosLine> lines = _store.ReturnLines(id);
            List<InvDocumentLine> invLines = new List<InvDocumentLine>();
            for (int i = 0; i < lines.Count; i++)
            {
                InvDocumentLine l = new InvDocumentLine();
                l.ItemId = lines[i].ItemId;
                l.QtyBase = lines[i].Qty;
                l.QtyDoc = lines[i].Qty;
                l.UnitCostMinor = 0;
                invLines.Add(l);
            }
            InventoryPostCommand cmd = new InventoryPostCommand();
            cmd.CompanyId = d.CompanyId;
            cmd.CenterId = d.CenterId;
            cmd.DocumentType = InventoryCodes.TypeAdjustment;
            cmd.PostingDate = d.ReturnDate;
            cmd.WarehouseId = s.WarehouseId;
            cmd.Description = "POS return " + d.DocNo;
            cmd.SourceModule = LedgerCodes.SourcePos;
            cmd.SourceDocumentType = PosCodes.DocReturn;
            cmd.SourceDocumentId = d.ReturnId;
            cmd.Lines = invLines;
            InventoryResult posted = _inv.CreateAndPost(cmd, identity);
            if (!posted.Ok) return TradeResult.Fail(posted.ErrorCode, posted.Message);
            _store.SetReturnInv(id, posted.EntityId);
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                AccOutboxWriter.Enqueue(con, tr, LedgerCodes.SourcePos, PosCodes.DocReturn, id, LedgerCodes.OutboxPost, d.CompanyId, d.CenterId, identity.UserName);
            });
            if (!_store.UpdateReturnStatus(id, TradeCodes.Posted, d.RowVersion, LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(PosCodes.EntityReturn, id, d.CenterId, "POSTED", identity);
            return TradeResult.Success(id, 0);
        }

        public PosSale GetSale(long id) { return _store.GetSale(id); }
        public PosReturnDoc GetReturn(long id) { return _store.GetReturn(id); }

        public IList<DailySalesRow> DailySales(ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PosPermissions.View)) return new List<DailySalesRow>();
            return _store.DailySales(Co(identity), Ctr(identity));
        }

        public IList<CashSummaryRow> CashSummary(ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PosPermissions.View)) return new List<CashSummaryRow>();
            return _store.CashSummary(Co(identity), Ctr(identity));
        }

        public IList<PosTxnRow> Transactions(ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PosPermissions.View)) return new List<PosTxnRow>();
            return _store.Transactions(Co(identity), Ctr(identity));
        }

        private long EnsureWalkIn(PosSale s, ILedgerIdentity identity)
        {
            object existing = new CaseManagement.DAL.DatabaseHelper().ExecuteScalar(
                "SELECT CustomerID FROM SalCustomer WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                new SQLiteParameter("@c", s.CompanyId), new SQLiteParameter("@code", PosCodes.WalkInCode));
            if (existing != null && existing != DBNull.Value) return Convert.ToInt64(existing);
            SalCustomer c = new SalCustomer();
            c.CompanyId = s.CompanyId;
            c.CenterId = s.CenterId;
            c.Code = PosCodes.WalkInCode;
            c.Name = "Walk-in";
            TradeResult created = _customers.CreateCustomer(c, identity);
            return created.Ok ? created.EntityId : 0;
        }

        private TradeResult MoveSale(long id, string from, string to, string wf, ILedgerIdentity identity, string perm)
        {
            if (identity == null || !identity.HasPermission(perm)) return TradeResult.Fail("PERMISSION", perm);
            PosSale s = _store.GetSale(id);
            if (s == null) return TradeResult.Fail("NOT_FOUND", "Sale not found.");
            TradeResult iso = TradeIsolation.DenyIfCrossTenant(identity, s.CompanyId, s.CenterId);
            if (!iso.Ok) return iso;
            if (s.Status != from) return TradeResult.Fail("INVALID_STATUS", "Expected " + from);
            if (!_store.UpdateSaleStatus(id, to, s.RowVersion, LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(PosCodes.EntitySale, id, s.CenterId, wf, identity);
            return TradeResult.Success(id, 0);
        }

        private TradeResult MoveReturn(long id, string from, string to, string wf, ILedgerIdentity identity, string perm)
        {
            if (identity == null || !identity.HasPermission(perm)) return TradeResult.Fail("PERMISSION", perm);
            PosReturnDoc d = _store.GetReturn(id);
            if (d == null) return TradeResult.Fail("NOT_FOUND", "Return not found.");
            TradeResult iso = TradeIsolation.DenyIfCrossTenant(identity, d.CompanyId, d.CenterId);
            if (!iso.Ok) return iso;
            if (d.Status != from) return TradeResult.Fail("INVALID_STATUS", "Expected " + from);
            if (!_store.UpdateReturnStatus(id, to, d.RowVersion, LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(PosCodes.EntityReturn, id, d.CenterId, wf, identity);
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
}

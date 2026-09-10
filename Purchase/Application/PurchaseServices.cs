using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Inventory.Application;
using CaseManagement.Inventory.Domain;
using CaseManagement.Purchase.Domain;
using CaseManagement.Purchase.Infrastructure;
using CaseManagement.Trade;

namespace CaseManagement.Purchase.Application
{
    public class VendorService
    {
        private readonly PurchaseStore _store;
        public VendorService() : this(new PurchaseStore()) { }
        public VendorService(PurchaseStore store) { _store = store; }

        public TradeResult CreateVendor(PurVendor vendor, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PurchasePermissions.Create))
                return TradeResult.Fail("PERMISSION", PurchasePermissions.Create);
            if (vendor == null || string.IsNullOrWhiteSpace(vendor.Code) || string.IsNullOrWhiteSpace(vendor.Name))
                return TradeResult.Fail("VALIDATION", "Vendor code and name are required.");
            vendor.CompanyId = TradeIsolation.ResolveCompany(identity, vendor.CompanyId);
            if (vendor.CompanyId <= 0) vendor.CompanyId = LedgerCodes.DefaultCompanyId;
            if (vendor.CategoryId <= 0) vendor.CategoryId = _store.DefaultCategoryId(vendor.CompanyId);
            long id = _store.InsertVendor(vendor, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            if (id <= 0) return TradeResult.Fail("VALIDATION", "Could not create vendor.");
            return TradeResult.Success(id, 1);
        }

        public TradeResult CreateContact(long vendorId, string name, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PurchasePermissions.Create))
                return TradeResult.Fail("PERMISSION", PurchasePermissions.Create);
            PurVendor v = _store.GetVendor(vendorId);
            if (v == null || string.IsNullOrWhiteSpace(name))
                return TradeResult.Fail("VALIDATION", "Vendor and contact name are required.");
            new CaseManagement.DAL.DatabaseHelper().ExecuteNonQuery(@"
INSERT INTO PurVendorContact (VendorID, CompanyID, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@v, @c, @n, 0, 1, @now, @now, @u, @u);",
                new System.Data.SQLite.SQLiteParameter("@v", vendorId),
                new System.Data.SQLite.SQLiteParameter("@c", v.CompanyId),
                new System.Data.SQLite.SQLiteParameter("@n", name),
                new System.Data.SQLite.SQLiteParameter("@now", CaseManagement.Accounting.Ledger.Domain.LedgerTime.UtcNow(identity.UtcNow)),
                new System.Data.SQLite.SQLiteParameter("@u", identity.UserName ?? ""));
            return TradeResult.Success(vendorId, 1);
        }

        public PurVendor Get(long id) { return _store.GetVendor(id); }
    }

    public class PurchaseWorkflowService
    {
        private readonly PurchaseStore _store;
        public PurchaseWorkflowService() : this(new PurchaseStore()) { }
        public PurchaseWorkflowService(PurchaseStore store) { _store = store; }

        public TradeResult SubmitOrder(long orderId, ILedgerIdentity identity)
        {
            return Move("PurOrder", "OrderID", orderId, PurchaseCodes.EntityOrder, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, PurchasePermissions.Create);
        }

        public TradeResult ApproveOrder(long orderId, ILedgerIdentity identity)
        {
            return Move("PurOrder", "OrderID", orderId, PurchaseCodes.EntityOrder, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, PurchasePermissions.Approve);
        }

        public TradeResult SubmitReceipt(long id, ILedgerIdentity identity)
        {
            return Move("PurGoodsReceipt", "ReceiptID", id, PurchaseCodes.EntityReceipt, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, PurchasePermissions.Create);
        }

        public TradeResult ApproveReceipt(long id, ILedgerIdentity identity)
        {
            return Move("PurGoodsReceipt", "ReceiptID", id, PurchaseCodes.EntityReceipt, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, PurchasePermissions.Approve);
        }

        public TradeResult SubmitInvoice(long id, ILedgerIdentity identity)
        {
            return Move("PurInvoice", "InvoiceID", id, PurchaseCodes.EntityInvoice, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, PurchasePermissions.Create);
        }

        public TradeResult ApproveInvoice(long id, ILedgerIdentity identity)
        {
            return Move("PurInvoice", "InvoiceID", id, PurchaseCodes.EntityInvoice, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, PurchasePermissions.Approve);
        }

        private TradeResult Move(string table, string idCol, long id, string entity, string from, string to, string wfCode, ILedgerIdentity identity, string perm)
        {
            if (identity == null || !identity.HasPermission(perm))
                return TradeResult.Fail("PERMISSION", perm);
            System.Data.DataRow row = _store.GetHeader(table, idCol, id);
            if (row == null) return TradeResult.Fail("NOT_FOUND", table);
            if (Convert.ToString(row["Status"]) != from)
                return TradeResult.Fail("INVALID_STATUS", "Expected " + from);
            int center = Convert.ToInt32(row["CenterID"]);
            int company = Convert.ToInt32(row["CompanyID"]);
            TradeResult iso = TradeIsolation.DenyIfCrossTenant(identity, company, center);
            if (!iso.Ok) return iso;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.UpdateStatusSimple(table, idCol, id, to, Convert.ToInt64(row["RowVersion"]), now, identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(entity, id, center, wfCode, identity);
            return TradeResult.Success(id, 0);
        }
    }

    public class PurchaseService
    {
        private readonly PurchaseStore _store;
        private readonly PurchaseWorkflowService _wf;
        private readonly InventoryPostingService _inv;
        private readonly IFiscalCalendarService _calendar;

        public PurchaseService()
            : this(new PurchaseStore(), new PurchaseWorkflowService(), new InventoryPostingService(), new FiscalCalendarService()) { }

        public PurchaseService(PurchaseStore store, PurchaseWorkflowService wf, InventoryPostingService inv, IFiscalCalendarService calendar)
        {
            _store = store;
            _wf = wf;
            _inv = inv;
            _calendar = calendar;
        }

        public TradeResult CreateRequest(PurchaseCommand cmd, ILedgerIdentity identity)
        {
            return CreateHeader("PurRequest", "RequestID", "PR", PurchaseCodes.EntityOrder, cmd, identity, false);
        }

        public TradeResult CreateOrder(PurchaseCommand cmd, ILedgerIdentity identity)
        {
            return CreateHeader("PurOrder", "OrderID", "PO", PurchaseCodes.EntityOrder, cmd, identity, true);
        }

        public TradeResult ConvertRequestToOrder(long requestId, ILedgerIdentity identity)
        {
            System.Data.DataRow req = _store.GetHeader("PurRequest", "RequestID", requestId);
            if (req == null) return TradeResult.Fail("NOT_FOUND", "Request not found.");
            PurchaseCommand cmd = new PurchaseCommand();
            cmd.CompanyId = Convert.ToInt32(req["CompanyID"]);
            cmd.CenterId = Convert.ToInt32(req["CenterID"]);
            cmd.VendorId = Convert.ToInt64(req["VendorID"]);
            cmd.WarehouseId = Convert.ToInt64(req["WarehouseID"]);
            cmd.Date = req["DocDate"].ToString();
            cmd.RequestId = requestId;
            cmd.Lines = _store.ListLines("PurRequestLine", "RequestID", requestId);
            TradeResult created = CreateOrder(cmd, identity);
            if (!created.Ok) return created;
            _store.UpdateStatusSimple("PurRequest", "RequestID", requestId, TradeCodes.Converted,
                Convert.ToInt64(req["RowVersion"]), LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            return created;
        }

        public TradeResult SubmitOrder(long id, ILedgerIdentity identity) { return _wf.SubmitOrder(id, identity); }
        public TradeResult ApproveOrder(long id, ILedgerIdentity identity) { return _wf.ApproveOrder(id, identity); }

        public TradeResult PostOrder(long orderId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PurchasePermissions.Post))
                return TradeResult.Fail("PERMISSION", PurchasePermissions.Post);
            PurOrder o = _store.GetOrder(orderId);
            if (o == null) return TradeResult.Fail("NOT_FOUND", "Order not found.");
            TradeResult iso = TradeIsolation.DenyIfCrossTenant(identity, o.CompanyId, o.CenterId);
            if (!iso.Ok) return iso;
            if (!CanPost(o.CompanyId, o.Status))
                return TradeResult.Fail("INVALID_STATUS", "Order must be Approved (or Draft when approval is off).");
            if (!_store.UpdateStatusSimple("PurOrder", "OrderID", orderId, TradeCodes.Posted, o.RowVersion,
                LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(PurchaseCodes.EntityOrder, orderId, o.CenterId, "POSTED", identity);
            return TradeResult.Success(orderId, 0);
        }

        public TradeResult CreateGoodsReceipt(long orderId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PurchasePermissions.Create))
                return TradeResult.Fail("PERMISSION", PurchasePermissions.Create);
            PurOrder o = _store.GetOrder(orderId);
            if (o == null || o.Status != TradeCodes.Posted)
                return TradeResult.Fail("INVALID_STATUS", "Order must be Posted before goods receipt.");
            TradeResult isoGr = TradeIsolation.DenyIfCrossTenant(identity, o.CompanyId, o.CenterId);
            if (!isoGr.Ok) return isoGr;
            IList<PurLine> lines = _store.ListLines("PurOrderLine", "OrderID", orderId);
            long newId = 0;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                PurGoodsReceipt d = new PurGoodsReceipt();
                d.CompanyId = o.CompanyId;
                d.CenterId = o.CenterId;
                d.DocNo = _store.AllocateNo(con, tr, o.CompanyId, "GR");
                d.Status = TradeCodes.Draft;
                d.PostingDate = o.OrderDate;
                d.OrderId = o.OrderId;
                d.VendorId = o.VendorId;
                d.WarehouseId = o.WarehouseId;
                newId = _store.InsertReceipt(con, tr, d, now, identity.UserName);
                for (int i = 0; i < lines.Count; i++)
                    _store.InsertLine(con, tr, "PurGoodsReceiptLine", "ReceiptID", newId, o.CompanyId, i + 1, lines[i]);
            });
            DocumentWorkflowService.Ensure(PurchaseCodes.EntityReceipt, newId, o.CenterId, identity);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult SubmitReceipt(long id, ILedgerIdentity identity) { return _wf.SubmitReceipt(id, identity); }
        public TradeResult ApproveReceipt(long id, ILedgerIdentity identity) { return _wf.ApproveReceipt(id, identity); }

        public TradeResult PostGoodsReceipt(long receiptId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PurchasePermissions.Post))
                return TradeResult.Fail("PERMISSION", PurchasePermissions.Post);
            PurGoodsReceipt gr = _store.GetReceipt(receiptId);
            if (gr == null) return TradeResult.Fail("NOT_FOUND", "Goods receipt not found.");
            TradeResult isoGrPost = TradeIsolation.DenyIfCrossTenant(identity, gr.CompanyId, gr.CenterId);
            if (!isoGrPost.Ok) return isoGrPost;
            if (!CanPost(gr.CompanyId, gr.Status))
                return TradeResult.Fail("INVALID_STATUS", "Goods receipt must be Approved.");
            GlFiscalPeriod period = _calendar.Resolve(gr.CompanyId, gr.PostingDate);
            if (period == null || period.Status != LedgerCodes.StatusOpen)
                return TradeResult.Fail("PERIOD_CLOSED", "Fiscal period is not Open.");

            IList<PurLine> lines = _store.ListLines("PurGoodsReceiptLine", "ReceiptID", receiptId);
            List<InvDocumentLine> invLines = new List<InvDocumentLine>();
            for (int i = 0; i < lines.Count; i++)
            {
                InvDocumentLine l = new InvDocumentLine();
                l.ItemId = lines[i].ItemId;
                l.QtyBase = lines[i].Qty;
                l.QtyDoc = lines[i].Qty;
                l.UnitCostMinor = lines[i].UnitPriceMinor;
                invLines.Add(l);
            }

            InventoryPostCommand cmd = new InventoryPostCommand();
            cmd.CompanyId = gr.CompanyId;
            cmd.CenterId = gr.CenterId;
            cmd.DocumentType = InventoryCodes.TypeReceipt;
            cmd.PostingDate = gr.PostingDate;
            cmd.WarehouseId = gr.WarehouseId;
            cmd.Description = "GR " + gr.DocNo;
            cmd.SourceModule = LedgerCodes.SourcePurchase;
            cmd.SourceDocumentType = PurchaseCodes.DocGoodsReceipt;
            cmd.SourceDocumentId = gr.ReceiptId;
            cmd.Lines = invLines;
            InventoryResult posted = _inv.CreateAndPost(cmd, identity);
            if (!posted.Ok) return TradeResult.Fail(posted.ErrorCode, posted.Message);

            if (!_store.UpdateStatusSimple("PurGoodsReceipt", "ReceiptID", receiptId, TradeCodes.Posted, gr.RowVersion,
                LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                _store.SetReceiptInv(con, tr, receiptId, posted.EntityId);
            });
            DocumentWorkflowService.MoveTo(PurchaseCodes.EntityReceipt, receiptId, gr.CenterId, "POSTED", identity);
            return TradeResult.Success(receiptId, posted.EntityId);
        }

        public TradeResult CreateInvoice(long receiptId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PurchasePermissions.Create))
                return TradeResult.Fail("PERMISSION", PurchasePermissions.Create);
            PurGoodsReceipt gr = _store.GetReceipt(receiptId);
            if (gr == null || gr.Status != TradeCodes.Posted)
                return TradeResult.Fail("INVALID_STATUS", "Goods receipt must be Posted.");
            IList<PurLine> lines = _store.ListLines("PurGoodsReceiptLine", "ReceiptID", receiptId);
            long amount = 0;
            for (int i = 0; i < lines.Count; i++) amount += lines[i].Qty * lines[i].UnitPriceMinor;
            long newId = 0;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                PurInvoice d = new PurInvoice();
                d.CompanyId = gr.CompanyId;
                d.CenterId = gr.CenterId;
                d.DocNo = _store.AllocateNo(con, tr, gr.CompanyId, "IV");
                d.Status = TradeCodes.Draft;
                d.InvoiceDate = gr.PostingDate;
                d.ReceiptId = gr.ReceiptId;
                d.OrderId = gr.OrderId;
                d.VendorId = gr.VendorId;
                d.AmountMinor = amount;
                newId = _store.InsertInvoice(con, tr, d, now, identity.UserName);
                for (int i = 0; i < lines.Count; i++)
                    _store.InsertLine(con, tr, "PurInvoiceLine", "InvoiceID", newId, gr.CompanyId, i + 1, lines[i]);
            });
            DocumentWorkflowService.Ensure(PurchaseCodes.EntityInvoice, newId, gr.CenterId, identity);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult SubmitInvoice(long id, ILedgerIdentity identity) { return _wf.SubmitInvoice(id, identity); }
        public TradeResult ApproveInvoice(long id, ILedgerIdentity identity) { return _wf.ApproveInvoice(id, identity); }

        public TradeResult PostInvoice(long invoiceId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PurchasePermissions.Post))
                return TradeResult.Fail("PERMISSION", PurchasePermissions.Post);
            PurInvoice inv = _store.GetInvoice(invoiceId);
            if (inv == null) return TradeResult.Fail("NOT_FOUND", "Invoice not found.");
            TradeResult isoInv = TradeIsolation.DenyIfCrossTenant(identity, inv.CompanyId, inv.CenterId);
            if (!isoInv.Ok) return isoInv;
            if (!CanPost(inv.CompanyId, inv.Status))
                return TradeResult.Fail("INVALID_STATUS", "Invoice must be Approved.");
            GlFiscalPeriod period = _calendar.Resolve(inv.CompanyId, inv.InvoiceDate);
            if (period == null || period.Status != LedgerCodes.StatusOpen)
                return TradeResult.Fail("PERIOD_CLOSED", "Fiscal period is not Open.");
            if (inv.AmountMinor <= 0)
                return TradeResult.Fail("VALIDATION", "Invoice amount must be greater than zero.");

            string now = LedgerTime.UtcNow(identity.UtcNow);
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                AccOutboxWriter.Enqueue(con, tr, LedgerCodes.SourcePurchase, PurchaseCodes.DocInvoice,
                    invoiceId, LedgerCodes.OutboxPost, inv.CompanyId, inv.CenterId, identity.UserName);
            });
            if (!_store.UpdateStatusSimple("PurInvoice", "InvoiceID", invoiceId, TradeCodes.Posted, inv.RowVersion, now, identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(PurchaseCodes.EntityInvoice, invoiceId, inv.CenterId, "POSTED", identity);
            return TradeResult.Success(invoiceId, inv.RowVersion + 1);
        }

        public IList<OpenPurchaseOrderRow> OpenOrders(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<OpenPurchaseOrderRow>();
            return _store.OpenOrders(Co(identity), Ctr(identity));
        }

        public IList<VendorPurchaseRow> VendorPurchases(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<VendorPurchaseRow>();
            return _store.VendorPurchases(Co(identity));
        }

        public IList<PurInvoice> PurchaseHistory(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<PurInvoice>();
            return _store.PurchaseHistory(Co(identity), Ctr(identity));
        }

        public IList<GrIrRow> GrIrReport(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<GrIrRow>();
            return _store.GrIr(Co(identity));
        }

        public PurOrder GetOrder(long id) { return _store.GetOrder(id); }
        public PurGoodsReceipt GetReceipt(long id) { return _store.GetReceipt(id); }
        public PurInvoice GetInvoice(long id) { return _store.GetInvoice(id); }

        private TradeResult CreateHeader(string table, string idCol, string kind, string entity, PurchaseCommand cmd, ILedgerIdentity identity, bool ensureWf)
        {
            if (identity == null || !identity.HasPermission(PurchasePermissions.Create))
                return TradeResult.Fail("PERMISSION", PurchasePermissions.Create);
            if (cmd == null || cmd.VendorId <= 0 || cmd.Lines == null || cmd.Lines.Count == 0)
                return TradeResult.Fail("VALIDATION", "Vendor and lines are required.");
            if (!identity.IsSuperAdmin && identity.CenterId > 0 && cmd.CenterId > 0 && identity.CenterId != cmd.CenterId)
                return TradeResult.Fail("PERMISSION", "Cross-branch purchase is not allowed.");
            PurVendor vendor = _store.GetVendor(cmd.VendorId);
            if (vendor == null) return TradeResult.Fail("VALIDATION", "Vendor not found.");
            int companyId = TradeIsolation.ResolveCompany(identity, cmd.CompanyId);
            if (companyId <= 0) companyId = LedgerCodes.DefaultCompanyId;
            int center = cmd.CenterId > 0 ? cmd.CenterId : (identity.CenterId > 0 ? identity.CenterId : 1);
            string date = string.IsNullOrEmpty(cmd.Date) ? identity.UtcNow.ToString("yyyy-MM-dd") : cmd.Date;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                string no = _store.AllocateNo(con, tr, companyId, kind);
                newId = _store.InsertHeader(con, tr, table, idCol, companyId, center, no, TradeCodes.Draft, date,
                    cmd.VendorId, cmd.WarehouseId, cmd.RequestId, now, identity.UserName);
                for (int i = 0; i < cmd.Lines.Count; i++)
                    _store.InsertLine(con, tr, table + "Line", idCol, newId, companyId, i + 1, cmd.Lines[i]);
            });
            if (ensureWf) DocumentWorkflowService.Ensure(entity, newId, center, identity);
            return TradeResult.Success(newId, 1);
        }

        private bool CanPost(int companyId, string status)
        {
            if (status == TradeCodes.Approved) return true;
            if (!_store.RequiresApproval(companyId) && status == TradeCodes.Draft) return true;
            return false;
        }

        private static bool CanView(ILedgerIdentity identity)
        {
            return identity != null && identity.HasPermission(PurchasePermissions.View);
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

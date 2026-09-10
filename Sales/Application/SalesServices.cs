using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Inventory.Application;
using CaseManagement.Inventory.Domain;
using CaseManagement.Sales.Domain;
using CaseManagement.Sales.Infrastructure;
using CaseManagement.Trade;

namespace CaseManagement.Sales.Application
{
    public class CustomerService
    {
        private readonly SalesStore _store;
        public CustomerService() : this(new SalesStore()) { }
        public CustomerService(SalesStore store) { _store = store; }

        public TradeResult CreateCustomer(SalCustomer customer, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(SalesPermissions.Create))
                return TradeResult.Fail("PERMISSION", SalesPermissions.Create);
            if (customer == null || string.IsNullOrWhiteSpace(customer.Code) || string.IsNullOrWhiteSpace(customer.Name))
                return TradeResult.Fail("VALIDATION", "Customer code and name are required.");
            customer.CompanyId = customer.CompanyId > 0 ? customer.CompanyId : identity.CompanyId;
            if (customer.CompanyId <= 0) customer.CompanyId = LedgerCodes.DefaultCompanyId;
            if (customer.CategoryId <= 0) customer.CategoryId = _store.DefaultCategoryId(customer.CompanyId);
            long id = _store.InsertCustomer(customer, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            if (id <= 0) return TradeResult.Fail("VALIDATION", "Could not create customer.");
            return TradeResult.Success(id, 1);
        }

        public TradeResult CreateContact(long customerId, string name, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(SalesPermissions.Create))
                return TradeResult.Fail("PERMISSION", SalesPermissions.Create);
            SalCustomer c = _store.GetCustomer(customerId);
            if (c == null || string.IsNullOrWhiteSpace(name))
                return TradeResult.Fail("VALIDATION", "Customer and contact name are required.");
            new CaseManagement.DAL.DatabaseHelper().ExecuteNonQuery(@"
INSERT INTO SalCustomerContact (CustomerID, CompanyID, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@v, @c, @n, 0, 1, @now, @now, @u, @u);",
                new System.Data.SQLite.SQLiteParameter("@v", customerId),
                new System.Data.SQLite.SQLiteParameter("@c", c.CompanyId),
                new System.Data.SQLite.SQLiteParameter("@n", name),
                new System.Data.SQLite.SQLiteParameter("@now", LedgerTime.UtcNow(identity.UtcNow)),
                new System.Data.SQLite.SQLiteParameter("@u", identity.UserName ?? ""));
            return TradeResult.Success(customerId, 1);
        }
    }

    public class SalesWorkflowService
    {
        private readonly SalesStore _store;
        public SalesWorkflowService() : this(new SalesStore()) { }
        public SalesWorkflowService(SalesStore store) { _store = store; }

        public TradeResult SubmitOrder(long id, ILedgerIdentity identity)
        { return Move("SalOrder", "OrderID", id, SalesCodes.EntityOrder, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, SalesPermissions.Create); }
        public TradeResult ApproveOrder(long id, ILedgerIdentity identity)
        { return Move("SalOrder", "OrderID", id, SalesCodes.EntityOrder, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, SalesPermissions.Approve); }
        public TradeResult SubmitDelivery(long id, ILedgerIdentity identity)
        { return Move("SalDelivery", "DeliveryID", id, SalesCodes.EntityDelivery, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, SalesPermissions.Create); }
        public TradeResult ApproveDelivery(long id, ILedgerIdentity identity)
        { return Move("SalDelivery", "DeliveryID", id, SalesCodes.EntityDelivery, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, SalesPermissions.Approve); }
        public TradeResult SubmitInvoice(long id, ILedgerIdentity identity)
        { return Move("SalInvoice", "InvoiceID", id, SalesCodes.EntityInvoice, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, SalesPermissions.Create); }
        public TradeResult ApproveInvoice(long id, ILedgerIdentity identity)
        { return Move("SalInvoice", "InvoiceID", id, SalesCodes.EntityInvoice, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, SalesPermissions.Approve); }

        private TradeResult Move(string table, string idCol, long id, string entity, string from, string to, string wfCode, ILedgerIdentity identity, string perm)
        {
            if (identity == null || !identity.HasPermission(perm))
                return TradeResult.Fail("PERMISSION", perm);
            System.Data.DataRow row = _store.GetHeader(table, idCol, id);
            if (row == null) return TradeResult.Fail("NOT_FOUND", table);
            if (Convert.ToString(row["Status"]) != from)
                return TradeResult.Fail("INVALID_STATUS", "Expected " + from);
            int center = Convert.ToInt32(row["CenterID"]);
            if (!_store.UpdateStatusSimple(table, idCol, id, to, Convert.ToInt64(row["RowVersion"]), LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(entity, id, center, wfCode, identity);
            return TradeResult.Success(id, 0);
        }
    }

    public class SalesService
    {
        private readonly SalesStore _store;
        private readonly SalesWorkflowService _wf;
        private readonly InventoryPostingService _inv;
        private readonly IFiscalCalendarService _calendar;

        public SalesService()
            : this(new SalesStore(), new SalesWorkflowService(), new InventoryPostingService(), new FiscalCalendarService()) { }

        public SalesService(SalesStore store, SalesWorkflowService wf, InventoryPostingService inv, IFiscalCalendarService calendar)
        {
            _store = store;
            _wf = wf;
            _inv = inv;
            _calendar = calendar;
        }

        public TradeResult CreateQuotation(SalesCommand cmd, ILedgerIdentity identity)
        {
            return CreateHeader("SalQuotation", "QuoteID", "QT", SalesCodes.EntityOrder, cmd, identity, false);
        }

        public TradeResult CreateOrder(SalesCommand cmd, ILedgerIdentity identity)
        {
            return CreateHeader("SalOrder", "OrderID", "SO", SalesCodes.EntityOrder, cmd, identity, true);
        }

        public TradeResult ConvertQuoteToOrder(long quoteId, ILedgerIdentity identity)
        {
            System.Data.DataRow q = _store.GetHeader("SalQuotation", "QuoteID", quoteId);
            if (q == null) return TradeResult.Fail("NOT_FOUND", "Quotation not found.");
            SalesCommand cmd = new SalesCommand();
            cmd.CompanyId = Convert.ToInt32(q["CompanyID"]);
            cmd.CenterId = Convert.ToInt32(q["CenterID"]);
            cmd.CustomerId = Convert.ToInt64(q["CustomerID"]);
            cmd.WarehouseId = Convert.ToInt64(q["WarehouseID"]);
            cmd.Date = q["DocDate"].ToString();
            cmd.QuoteId = quoteId;
            cmd.Lines = _store.ListLines("SalQuotationLine", "QuoteID", quoteId);
            TradeResult created = CreateOrder(cmd, identity);
            if (!created.Ok) return created;
            _store.UpdateStatusSimple("SalQuotation", "QuoteID", quoteId, TradeCodes.Converted,
                Convert.ToInt64(q["RowVersion"]), LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            return created;
        }

        public TradeResult SubmitOrder(long id, ILedgerIdentity identity) { return _wf.SubmitOrder(id, identity); }
        public TradeResult ApproveOrder(long id, ILedgerIdentity identity) { return _wf.ApproveOrder(id, identity); }

        public TradeResult PostOrder(long orderId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(SalesPermissions.Post))
                return TradeResult.Fail("PERMISSION", SalesPermissions.Post);
            SalOrder o = _store.GetOrder(orderId);
            if (o == null) return TradeResult.Fail("NOT_FOUND", "Order not found.");
            if (!CanPost(o.CompanyId, o.Status))
                return TradeResult.Fail("INVALID_STATUS", "Order must be Approved.");
            if (!_store.UpdateStatusSimple("SalOrder", "OrderID", orderId, TradeCodes.Posted, o.RowVersion,
                LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(SalesCodes.EntityOrder, orderId, o.CenterId, "POSTED", identity);
            return TradeResult.Success(orderId, 0);
        }

        public TradeResult CreateDelivery(long orderId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(SalesPermissions.Create))
                return TradeResult.Fail("PERMISSION", SalesPermissions.Create);
            SalOrder o = _store.GetOrder(orderId);
            if (o == null || o.Status != TradeCodes.Posted)
                return TradeResult.Fail("INVALID_STATUS", "Order must be Posted before delivery.");
            IList<SalLine> lines = _store.ListLines("SalOrderLine", "OrderID", orderId);
            long newId = 0;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                SalDelivery d = new SalDelivery();
                d.CompanyId = o.CompanyId;
                d.CenterId = o.CenterId;
                d.DocNo = _store.AllocateNo(con, tr, o.CompanyId, "DN");
                d.Status = TradeCodes.Draft;
                d.PostingDate = o.OrderDate;
                d.OrderId = o.OrderId;
                d.CustomerId = o.CustomerId;
                d.WarehouseId = o.WarehouseId;
                newId = _store.InsertDelivery(con, tr, d, now, identity.UserName);
                for (int i = 0; i < lines.Count; i++)
                    _store.InsertLine(con, tr, "SalDeliveryLine", "DeliveryID", newId, o.CompanyId, i + 1, lines[i]);
            });
            DocumentWorkflowService.Ensure(SalesCodes.EntityDelivery, newId, o.CenterId, identity);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult SubmitDelivery(long id, ILedgerIdentity identity) { return _wf.SubmitDelivery(id, identity); }
        public TradeResult ApproveDelivery(long id, ILedgerIdentity identity) { return _wf.ApproveDelivery(id, identity); }

        public TradeResult PostDelivery(long deliveryId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(SalesPermissions.Post))
                return TradeResult.Fail("PERMISSION", SalesPermissions.Post);
            SalDelivery dn = _store.GetDelivery(deliveryId);
            if (dn == null) return TradeResult.Fail("NOT_FOUND", "Delivery not found.");
            if (!CanPost(dn.CompanyId, dn.Status))
                return TradeResult.Fail("INVALID_STATUS", "Delivery must be Approved.");
            GlFiscalPeriod period = _calendar.Resolve(dn.CompanyId, dn.PostingDate);
            if (period == null || period.Status != LedgerCodes.StatusOpen)
                return TradeResult.Fail("PERIOD_CLOSED", "Fiscal period is not Open.");

            IList<SalLine> lines = _store.ListLines("SalDeliveryLine", "DeliveryID", deliveryId);
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
            cmd.CompanyId = dn.CompanyId;
            cmd.CenterId = dn.CenterId;
            cmd.DocumentType = InventoryCodes.TypeIssue;
            cmd.PostingDate = dn.PostingDate;
            cmd.WarehouseId = dn.WarehouseId;
            cmd.Description = "DN " + dn.DocNo;
            cmd.SourceModule = LedgerCodes.SourceSales;
            cmd.SourceDocumentType = SalesCodes.DocDelivery;
            cmd.SourceDocumentId = dn.DeliveryId;
            cmd.Lines = invLines;
            InventoryResult posted = _inv.CreateAndPost(cmd, identity);
            if (!posted.Ok) return TradeResult.Fail(posted.ErrorCode, posted.Message);
            if (!_store.UpdateStatusSimple("SalDelivery", "DeliveryID", deliveryId, TradeCodes.Posted, dn.RowVersion,
                LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                _store.SetDeliveryInv(con, tr, deliveryId, posted.EntityId);
            });
            DocumentWorkflowService.MoveTo(SalesCodes.EntityDelivery, deliveryId, dn.CenterId, "POSTED", identity);
            return TradeResult.Success(deliveryId, posted.EntityId);
        }

        public TradeResult CreateInvoice(long deliveryId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(SalesPermissions.Create))
                return TradeResult.Fail("PERMISSION", SalesPermissions.Create);
            SalDelivery dn = _store.GetDelivery(deliveryId);
            if (dn == null || dn.Status != TradeCodes.Posted)
                return TradeResult.Fail("INVALID_STATUS", "Delivery must be Posted.");
            IList<SalLine> lines = _store.ListLines("SalDeliveryLine", "DeliveryID", deliveryId);
            long amount = 0;
            for (int i = 0; i < lines.Count; i++) amount += lines[i].Qty * lines[i].UnitPriceMinor;
            long newId = 0;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                SalInvoice d = new SalInvoice();
                d.CompanyId = dn.CompanyId;
                d.CenterId = dn.CenterId;
                d.DocNo = _store.AllocateNo(con, tr, dn.CompanyId, "IV");
                d.Status = TradeCodes.Draft;
                d.InvoiceDate = dn.PostingDate;
                d.DeliveryId = dn.DeliveryId;
                d.OrderId = dn.OrderId;
                d.CustomerId = dn.CustomerId;
                d.AmountMinor = amount;
                newId = _store.InsertInvoice(con, tr, d, now, identity.UserName);
                for (int i = 0; i < lines.Count; i++)
                    _store.InsertLine(con, tr, "SalInvoiceLine", "InvoiceID", newId, dn.CompanyId, i + 1, lines[i]);
            });
            DocumentWorkflowService.Ensure(SalesCodes.EntityInvoice, newId, dn.CenterId, identity);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult SubmitInvoice(long id, ILedgerIdentity identity) { return _wf.SubmitInvoice(id, identity); }
        public TradeResult ApproveInvoice(long id, ILedgerIdentity identity) { return _wf.ApproveInvoice(id, identity); }

        public TradeResult PostInvoice(long invoiceId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(SalesPermissions.Post))
                return TradeResult.Fail("PERMISSION", SalesPermissions.Post);
            SalInvoice inv = _store.GetInvoice(invoiceId);
            if (inv == null) return TradeResult.Fail("NOT_FOUND", "Invoice not found.");
            if (!CanPost(inv.CompanyId, inv.Status))
                return TradeResult.Fail("INVALID_STATUS", "Invoice must be Approved.");
            GlFiscalPeriod period = _calendar.Resolve(inv.CompanyId, inv.InvoiceDate);
            if (period == null || period.Status != LedgerCodes.StatusOpen)
                return TradeResult.Fail("PERIOD_CLOSED", "Fiscal period is not Open.");
            if (inv.AmountMinor <= 0)
                return TradeResult.Fail("VALIDATION", "Invoice amount must be greater than zero.");
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                AccOutboxWriter.Enqueue(con, tr, LedgerCodes.SourceSales, SalesCodes.DocInvoice,
                    invoiceId, LedgerCodes.OutboxPost, inv.CompanyId, inv.CenterId, identity.UserName);
            });
            if (!_store.UpdateStatusSimple("SalInvoice", "InvoiceID", invoiceId, TradeCodes.Posted, inv.RowVersion,
                LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(SalesCodes.EntityInvoice, invoiceId, inv.CenterId, "POSTED", identity);
            return TradeResult.Success(invoiceId, inv.RowVersion + 1);
        }

        public IList<OpenSalesOrderRow> OpenOrders(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<OpenSalesOrderRow>();
            return _store.OpenOrders(Co(identity), Ctr(identity));
        }

        public IList<CustomerSalesRow> CustomerSales(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<CustomerSalesRow>();
            return _store.CustomerSales(Co(identity));
        }

        public IList<SalInvoice> SalesHistory(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<SalInvoice>();
            return _store.SalesHistory(Co(identity), Ctr(identity));
        }

        public IList<RevenueRow> RevenueAnalysis(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<RevenueRow>();
            return _store.Revenue(Co(identity), Ctr(identity));
        }

        public SalOrder GetOrder(long id) { return _store.GetOrder(id); }
        public SalDelivery GetDelivery(long id) { return _store.GetDelivery(id); }
        public SalInvoice GetInvoice(long id) { return _store.GetInvoice(id); }

        private TradeResult CreateHeader(string table, string idCol, string kind, string entity, SalesCommand cmd, ILedgerIdentity identity, bool ensureWf)
        {
            if (identity == null || !identity.HasPermission(SalesPermissions.Create))
                return TradeResult.Fail("PERMISSION", SalesPermissions.Create);
            if (cmd == null || cmd.CustomerId <= 0 || cmd.Lines == null || cmd.Lines.Count == 0)
                return TradeResult.Fail("VALIDATION", "Customer and lines are required.");
            if (!identity.IsSuperAdmin && identity.CenterId > 0 && cmd.CenterId > 0 && identity.CenterId != cmd.CenterId)
                return TradeResult.Fail("PERMISSION", "Cross-branch sales is not allowed.");
            if (_store.GetCustomer(cmd.CustomerId) == null)
                return TradeResult.Fail("VALIDATION", "Customer not found.");
            int companyId = cmd.CompanyId > 0 ? cmd.CompanyId : identity.CompanyId;
            if (companyId <= 0) companyId = LedgerCodes.DefaultCompanyId;
            int center = cmd.CenterId > 0 ? cmd.CenterId : (identity.CenterId > 0 ? identity.CenterId : 1);
            string date = string.IsNullOrEmpty(cmd.Date) ? identity.UtcNow.ToString("yyyy-MM-dd") : cmd.Date;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                string no = _store.AllocateNo(con, tr, companyId, kind);
                newId = _store.InsertHeader(con, tr, table, companyId, center, no, TradeCodes.Draft, date,
                    cmd.CustomerId, cmd.WarehouseId, cmd.QuoteId, now, identity.UserName);
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
            return identity != null && identity.HasPermission(SalesPermissions.View);
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

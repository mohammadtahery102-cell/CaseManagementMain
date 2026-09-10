using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Sales.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Sales.Infrastructure
{
    public class SalesStore
    {
        private readonly DatabaseHelper _db;
        public SalesStore() : this(new DatabaseHelper()) { }
        public SalesStore(DatabaseHelper db) { _db = db; }

        public void ExecuteInTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            _db.ExecuteInTransaction(action);
        }

        public bool RequiresApproval(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT RequiresApproval FROM SalSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v == null || v == DBNull.Value || Convert.ToInt32(v) != 0;
        }

        public long ArAccountId(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT ArAccountID FROM SalSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long RevenueAccountId(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT RevenueAccountID FROM SalSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long DefaultCategoryId(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT CategoryID FROM SalCustomerCategory WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY CategoryID LIMIT 1;", P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long InsertCustomer(SalCustomer c, string now, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO SalCustomer (CompanyID, CenterID, CategoryID, Code, Name, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @cat, @code, @name, 1, 0, 1, @n, @n, @u, @u);",
                P("@c", c.CompanyId), P("@ctr", c.CenterId), P("@cat", c.CategoryId), P("@code", c.Code), P("@name", c.Name),
                P("@n", now), P("@u", user ?? ""));
            object id = _db.ExecuteScalar("SELECT CustomerID FROM SalCustomer WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", c.CompanyId), P("@code", c.Code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        public SalCustomer GetCustomer(long id)
        {
            DataTable t = _db.Query("SELECT * FROM SalCustomer WHERE CustomerID = @id AND IsDeleted = 0;", P("@id", id));
            if (t.Rows.Count == 0) return null;
            DataRow r = t.Rows[0];
            SalCustomer c = new SalCustomer();
            c.CustomerId = Convert.ToInt64(r["CustomerID"]);
            c.CompanyId = Convert.ToInt32(r["CompanyID"]);
            c.CenterId = Convert.ToInt32(r["CenterID"]);
            c.CategoryId = Convert.ToInt64(r["CategoryID"]);
            c.Code = r["Code"].ToString();
            c.Name = r["Name"].ToString();
            c.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return c;
        }

        public string AllocateNo(SQLiteConnection con, SQLiteTransaction tr, int companyId, string kind)
        {
            long next = 1;
            string prefix = "SAL";
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT NextDocNumber, NumberPrefix FROM SalSetting WHERE CompanyID = @c AND IsDeleted = 0;", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        next = Convert.ToInt64(r["NextDocNumber"]);
                        prefix = Convert.ToString(r["NumberPrefix"]);
                    }
                }
            }
            using (SQLiteCommand upd = new SQLiteCommand("UPDATE SalSetting SET NextDocNumber = NextDocNumber + 1 WHERE CompanyID = @c;", con, tr))
            {
                upd.Parameters.AddWithValue("@c", companyId);
                upd.ExecuteNonQuery();
            }
            return prefix + "-" + kind + next.ToString();
        }

        public long InsertHeader(SQLiteConnection con, SQLiteTransaction tr, string table,
            int companyId, int centerId, string docNo, string status, string date, long customerId, long warehouseId, long sourceId, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO " + table + @" (CompanyID, CenterID, DocNo, Status, DocDate, CustomerID, WarehouseID, SourceID, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @no, @st, @dt, @cust, @w, @src, 0, 1, @n, @n, @u, @u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@ctr", centerId);
                cmd.Parameters.AddWithValue("@no", docNo);
                cmd.Parameters.AddWithValue("@st", status);
                cmd.Parameters.AddWithValue("@dt", date);
                cmd.Parameters.AddWithValue("@cust", customerId);
                cmd.Parameters.AddWithValue("@w", warehouseId);
                cmd.Parameters.AddWithValue("@src", sourceId > 0 ? (object)sourceId : DBNull.Value);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public void InsertLine(SQLiteConnection con, SQLiteTransaction tr, string table, string parentCol, long parentId, int companyId, int lineNo, SalLine line)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO " + table + @" (" + parentCol + @", CompanyID, LineNo, ItemID, Qty, UnitPriceMinor, AmountMinor)
VALUES (@p, @c, @n, @i, @q, @u, @a);", con, tr))
            {
                cmd.Parameters.AddWithValue("@p", parentId);
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@n", lineNo);
                cmd.Parameters.AddWithValue("@i", line.ItemId);
                cmd.Parameters.AddWithValue("@q", line.Qty);
                cmd.Parameters.AddWithValue("@u", line.UnitPriceMinor);
                cmd.Parameters.AddWithValue("@a", line.Qty * line.UnitPriceMinor);
                cmd.ExecuteNonQuery();
            }
        }

        public IList<SalLine> ListLines(string table, string parentCol, long parentId)
        {
            DataTable t = _db.Query("SELECT * FROM " + table + " WHERE " + parentCol + " = @p ORDER BY LineNo;", P("@p", parentId));
            List<SalLine> list = new List<SalLine>();
            foreach (DataRow r in t.Rows)
            {
                SalLine l = new SalLine();
                l.ItemId = Convert.ToInt64(r["ItemID"]);
                l.Qty = Convert.ToInt64(r["Qty"]);
                l.UnitPriceMinor = Convert.ToInt64(r["UnitPriceMinor"]);
                list.Add(l);
            }
            return list;
        }

        public DataRow GetHeader(string table, string idCol, long id)
        {
            DataTable t = _db.Query("SELECT * FROM " + table + " WHERE " + idCol + " = @id AND IsDeleted = 0;", P("@id", id));
            return t.Rows.Count == 0 ? null : t.Rows[0];
        }

        public bool UpdateStatusSimple(string table, string idCol, long id, string status, long rv, string now, string user)
        {
            bool ok = false;
            ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                using (SQLiteCommand cmd = new SQLiteCommand(@"
UPDATE " + table + @" SET Status = @st, UpdatedAt = @n, UpdatedBy = @u, RowVersion = RowVersion + 1
WHERE " + idCol + @" = @id AND RowVersion = @rv;", con, tr))
                {
                    cmd.Parameters.AddWithValue("@st", status);
                    cmd.Parameters.AddWithValue("@n", now);
                    cmd.Parameters.AddWithValue("@u", user ?? "");
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@rv", rv);
                    ok = cmd.ExecuteNonQuery() == 1;
                }
            });
            return ok;
        }

        public long InsertDelivery(SQLiteConnection con, SQLiteTransaction tr, SalDelivery d, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO SalDelivery (CompanyID, CenterID, DocNo, Status, PostingDate, OrderID, CustomerID, WarehouseID, InvDocumentID, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @no, @st, @dt, @o, @cust, @w, @inv, 0, 1, @n, @n, @u, @u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", d.CompanyId);
                cmd.Parameters.AddWithValue("@ctr", d.CenterId);
                cmd.Parameters.AddWithValue("@no", d.DocNo);
                cmd.Parameters.AddWithValue("@st", d.Status);
                cmd.Parameters.AddWithValue("@dt", d.PostingDate);
                cmd.Parameters.AddWithValue("@o", d.OrderId);
                cmd.Parameters.AddWithValue("@cust", d.CustomerId);
                cmd.Parameters.AddWithValue("@w", d.WarehouseId);
                cmd.Parameters.AddWithValue("@inv", (object)d.InvDocumentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public void SetDeliveryInv(SQLiteConnection con, SQLiteTransaction tr, long deliveryId, long invId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE SalDelivery SET InvDocumentID = @i WHERE DeliveryID = @id;", con, tr))
            {
                cmd.Parameters.AddWithValue("@i", invId);
                cmd.Parameters.AddWithValue("@id", deliveryId);
                cmd.ExecuteNonQuery();
            }
        }

        public SalOrder GetOrder(long id)
        {
            DataRow r = GetHeader("SalOrder", "OrderID", id);
            if (r == null) return null;
            SalOrder o = new SalOrder();
            o.OrderId = Convert.ToInt64(r["OrderID"]);
            o.CompanyId = Convert.ToInt32(r["CompanyID"]);
            o.CenterId = Convert.ToInt32(r["CenterID"]);
            o.DocNo = r["DocNo"].ToString();
            o.Status = r["Status"].ToString();
            o.CustomerId = Convert.ToInt64(r["CustomerID"]);
            o.WarehouseId = Convert.ToInt64(r["WarehouseID"]);
            o.OrderDate = r["DocDate"].ToString();
            o.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return o;
        }

        public SalDelivery GetDelivery(long id)
        {
            DataRow r = GetHeader("SalDelivery", "DeliveryID", id);
            if (r == null) return null;
            SalDelivery d = new SalDelivery();
            d.DeliveryId = Convert.ToInt64(r["DeliveryID"]);
            d.CompanyId = Convert.ToInt32(r["CompanyID"]);
            d.CenterId = Convert.ToInt32(r["CenterID"]);
            d.DocNo = r["DocNo"].ToString();
            d.Status = r["Status"].ToString();
            d.PostingDate = r["PostingDate"].ToString();
            d.OrderId = Convert.ToInt64(r["OrderID"]);
            d.CustomerId = Convert.ToInt64(r["CustomerID"]);
            d.WarehouseId = Convert.ToInt64(r["WarehouseID"]);
            d.InvDocumentId = r["InvDocumentID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["InvDocumentID"]);
            d.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return d;
        }

        public long InsertInvoice(SQLiteConnection con, SQLiteTransaction tr, SalInvoice d, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO SalInvoice (CompanyID, CenterID, DocNo, Status, InvoiceDate, DeliveryID, OrderID, CustomerID, AmountMinor, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @no, @st, @dt, @d, @o, @cust, @a, 0, 1, @n, @n, @u, @u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", d.CompanyId);
                cmd.Parameters.AddWithValue("@ctr", d.CenterId);
                cmd.Parameters.AddWithValue("@no", d.DocNo);
                cmd.Parameters.AddWithValue("@st", d.Status);
                cmd.Parameters.AddWithValue("@dt", d.InvoiceDate);
                cmd.Parameters.AddWithValue("@d", d.DeliveryId);
                cmd.Parameters.AddWithValue("@o", d.OrderId);
                cmd.Parameters.AddWithValue("@cust", d.CustomerId);
                cmd.Parameters.AddWithValue("@a", d.AmountMinor);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public SalInvoice GetInvoice(long id)
        {
            DataRow r = GetHeader("SalInvoice", "InvoiceID", id);
            if (r == null) return null;
            SalInvoice d = new SalInvoice();
            d.InvoiceId = Convert.ToInt64(r["InvoiceID"]);
            d.CompanyId = Convert.ToInt32(r["CompanyID"]);
            d.CenterId = Convert.ToInt32(r["CenterID"]);
            d.DocNo = r["DocNo"].ToString();
            d.Status = r["Status"].ToString();
            d.InvoiceDate = r["InvoiceDate"].ToString();
            d.DeliveryId = Convert.ToInt64(r["DeliveryID"]);
            d.OrderId = Convert.ToInt64(r["OrderID"]);
            d.CustomerId = Convert.ToInt64(r["CustomerID"]);
            d.AmountMinor = Convert.ToInt64(r["AmountMinor"]);
            d.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return d;
        }

        public IList<OpenSalesOrderRow> OpenOrders(int companyId, int center)
        {
            DataTable t = _db.Query(@"
SELECT o.DocNo, c.Name AS CustomerName, o.Status, o.DocDate
FROM SalOrder o JOIN SalCustomer c ON c.CustomerID = o.CustomerID
WHERE o.CompanyID = @c AND o.IsDeleted = 0 AND o.Status <> @p AND (@ctr = 0 OR o.CenterID = @ctr)
ORDER BY o.OrderID DESC;", P("@c", companyId), P("@p", TradeCodes.Posted), P("@ctr", center));
            List<OpenSalesOrderRow> list = new List<OpenSalesOrderRow>();
            foreach (DataRow r in t.Rows)
            {
                OpenSalesOrderRow row = new OpenSalesOrderRow();
                row.DocNo = r["DocNo"].ToString();
                row.CustomerName = r["CustomerName"].ToString();
                row.Status = r["Status"].ToString();
                row.OrderDate = r["DocDate"].ToString();
                list.Add(row);
            }
            return list;
        }

        public IList<CustomerSalesRow> CustomerSales(int companyId)
        {
            DataTable t = _db.Query(@"
SELECT c.Code, c.Name, COALESCE(SUM(i.AmountMinor),0) AS Amt
FROM SalCustomer c
LEFT JOIN SalInvoice i ON i.CustomerID = c.CustomerID AND i.Status = @p AND i.IsDeleted = 0
WHERE c.CompanyID = @co AND c.IsDeleted = 0
GROUP BY c.CustomerID, c.Code, c.Name ORDER BY c.Code;",
                P("@co", companyId), P("@p", TradeCodes.Posted));
            List<CustomerSalesRow> list = new List<CustomerSalesRow>();
            foreach (DataRow r in t.Rows)
            {
                CustomerSalesRow row = new CustomerSalesRow();
                row.CustomerCode = r["Code"].ToString();
                row.CustomerName = r["Name"].ToString();
                row.AmountMinor = Convert.ToInt64(r["Amt"]);
                list.Add(row);
            }
            return list;
        }

        public IList<SalInvoice> SalesHistory(int companyId, int center)
        {
            DataTable t = _db.Query(@"
SELECT * FROM SalInvoice WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr)
ORDER BY InvoiceID DESC LIMIT 200;", P("@c", companyId), P("@ctr", center));
            List<SalInvoice> list = new List<SalInvoice>();
            foreach (DataRow r in t.Rows)
            {
                SalInvoice d = new SalInvoice();
                d.InvoiceId = Convert.ToInt64(r["InvoiceID"]);
                d.DocNo = r["DocNo"].ToString();
                d.Status = r["Status"].ToString();
                d.InvoiceDate = r["InvoiceDate"].ToString();
                d.AmountMinor = Convert.ToInt64(r["AmountMinor"]);
                list.Add(d);
            }
            return list;
        }

        public IList<RevenueRow> Revenue(int companyId, int center)
        {
            IList<SalInvoice> hist = SalesHistory(companyId, center);
            List<RevenueRow> list = new List<RevenueRow>();
            for (int i = 0; i < hist.Count; i++)
            {
                if (hist[i].Status != TradeCodes.Posted) continue;
                RevenueRow row = new RevenueRow();
                row.InvoiceNo = hist[i].DocNo;
                row.InvoiceDate = hist[i].InvoiceDate;
                row.AmountMinor = hist[i].AmountMinor;
                list.Add(row);
            }
            return list;
        }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }
    }
}

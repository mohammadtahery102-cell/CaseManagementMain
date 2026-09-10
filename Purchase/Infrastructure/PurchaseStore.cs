using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Purchase.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Purchase.Infrastructure
{
    public class PurchaseStore
    {
        private readonly DatabaseHelper _db;
        public PurchaseStore() : this(new DatabaseHelper()) { }
        public PurchaseStore(DatabaseHelper db) { _db = db; }

        public void ExecuteInTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            _db.ExecuteInTransaction(action);
        }

        public bool RequiresApproval(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT RequiresApproval FROM PurSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v == null || v == DBNull.Value || Convert.ToInt32(v) != 0;
        }

        public long ApAccountId(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT ApAccountID FROM PurSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long DefaultCategoryId(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT CategoryID FROM PurVendorCategory WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY CategoryID LIMIT 1;", P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long InsertVendor(PurVendor v, string now, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO PurVendor (CompanyID, CenterID, CategoryID, Code, Name, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @cat, @code, @name, 1, 0, 1, @n, @n, @u, @u);",
                P("@c", v.CompanyId), P("@ctr", v.CenterId), P("@cat", v.CategoryId), P("@code", v.Code), P("@name", v.Name),
                P("@n", now), P("@u", user ?? ""));
            object id = _db.ExecuteScalar("SELECT VendorID FROM PurVendor WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", v.CompanyId), P("@code", v.Code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        public PurVendor GetVendor(long id)
        {
            DataTable t = _db.Query("SELECT * FROM PurVendor WHERE VendorID = @id AND IsDeleted = 0;", P("@id", id));
            if (t.Rows.Count == 0) return null;
            DataRow r = t.Rows[0];
            PurVendor v = new PurVendor();
            v.VendorId = Convert.ToInt64(r["VendorID"]);
            v.CompanyId = Convert.ToInt32(r["CompanyID"]);
            v.CenterId = Convert.ToInt32(r["CenterID"]);
            v.CategoryId = Convert.ToInt64(r["CategoryID"]);
            v.Code = r["Code"].ToString();
            v.Name = r["Name"].ToString();
            v.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return v;
        }

        public string AllocateNo(SQLiteConnection con, SQLiteTransaction tr, int companyId, string kind)
        {
            long next = 1;
            string prefix = "PUR";
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT NextDocNumber, NumberPrefix FROM PurSetting WHERE CompanyID = @c AND IsDeleted = 0;", con, tr))
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
            using (SQLiteCommand upd = new SQLiteCommand("UPDATE PurSetting SET NextDocNumber = NextDocNumber + 1 WHERE CompanyID = @c;", con, tr))
            {
                upd.Parameters.AddWithValue("@c", companyId);
                upd.ExecuteNonQuery();
            }
            return prefix + "-" + kind + next.ToString();
        }

        public long InsertHeader(SQLiteConnection con, SQLiteTransaction tr, string table, string idCol,
            int companyId, int centerId, string docNo, string status, string date, long vendorId, long warehouseId, long sourceId, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO " + table + @" (CompanyID, CenterID, DocNo, Status, DocDate, VendorID, WarehouseID, SourceID, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @no, @st, @dt, @v, @w, @src, 0, 1, @n, @n, @u, @u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@ctr", centerId);
                cmd.Parameters.AddWithValue("@no", docNo);
                cmd.Parameters.AddWithValue("@st", status);
                cmd.Parameters.AddWithValue("@dt", date);
                cmd.Parameters.AddWithValue("@v", vendorId);
                cmd.Parameters.AddWithValue("@w", warehouseId);
                cmd.Parameters.AddWithValue("@src", sourceId > 0 ? (object)sourceId : DBNull.Value);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public void InsertLine(SQLiteConnection con, SQLiteTransaction tr, string table, string parentCol, long parentId, int companyId, int lineNo, PurLine line)
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

        public IList<PurLine> ListLines(string table, string parentCol, long parentId)
        {
            DataTable t = _db.Query("SELECT * FROM " + table + " WHERE " + parentCol + " = @p ORDER BY LineNo;", P("@p", parentId));
            List<PurLine> list = new List<PurLine>();
            foreach (DataRow r in t.Rows)
            {
                PurLine l = new PurLine();
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

        public bool UpdateStatus(SQLiteConnection con, SQLiteTransaction tr, string table, string idCol, long id, string status, long rv, string now, string user)
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
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public bool UpdateStatusSimple(string table, string idCol, long id, string status, long rv, string now, string user)
        {
            bool ok = false;
            ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                ok = UpdateStatus(con, tr, table, idCol, id, status, rv, now, user);
            });
            return ok;
        }

        public long InsertReceipt(SQLiteConnection con, SQLiteTransaction tr, PurGoodsReceipt d, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO PurGoodsReceipt (CompanyID, CenterID, DocNo, Status, PostingDate, OrderID, VendorID, WarehouseID, InvDocumentID, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @no, @st, @dt, @o, @v, @w, @inv, 0, 1, @n, @n, @u, @u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", d.CompanyId);
                cmd.Parameters.AddWithValue("@ctr", d.CenterId);
                cmd.Parameters.AddWithValue("@no", d.DocNo);
                cmd.Parameters.AddWithValue("@st", d.Status);
                cmd.Parameters.AddWithValue("@dt", d.PostingDate);
                cmd.Parameters.AddWithValue("@o", d.OrderId);
                cmd.Parameters.AddWithValue("@v", d.VendorId);
                cmd.Parameters.AddWithValue("@w", d.WarehouseId);
                cmd.Parameters.AddWithValue("@inv", (object)d.InvDocumentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public void SetReceiptInv(SQLiteConnection con, SQLiteTransaction tr, long receiptId, long invId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE PurGoodsReceipt SET InvDocumentID = @i WHERE ReceiptID = @id;", con, tr))
            {
                cmd.Parameters.AddWithValue("@i", invId);
                cmd.Parameters.AddWithValue("@id", receiptId);
                cmd.ExecuteNonQuery();
            }
        }

        public PurGoodsReceipt GetReceipt(long id)
        {
            DataRow r = GetHeader("PurGoodsReceipt", "ReceiptID", id);
            if (r == null) return null;
            PurGoodsReceipt d = new PurGoodsReceipt();
            d.ReceiptId = Convert.ToInt64(r["ReceiptID"]);
            d.CompanyId = Convert.ToInt32(r["CompanyID"]);
            d.CenterId = Convert.ToInt32(r["CenterID"]);
            d.DocNo = r["DocNo"].ToString();
            d.Status = r["Status"].ToString();
            d.PostingDate = r["PostingDate"].ToString();
            d.OrderId = Convert.ToInt64(r["OrderID"]);
            d.VendorId = Convert.ToInt64(r["VendorID"]);
            d.WarehouseId = Convert.ToInt64(r["WarehouseID"]);
            d.InvDocumentId = r["InvDocumentID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["InvDocumentID"]);
            d.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return d;
        }

        public PurOrder GetOrder(long id)
        {
            DataRow r = GetHeader("PurOrder", "OrderID", id);
            if (r == null) return null;
            PurOrder o = new PurOrder();
            o.OrderId = Convert.ToInt64(r["OrderID"]);
            o.CompanyId = Convert.ToInt32(r["CompanyID"]);
            o.CenterId = Convert.ToInt32(r["CenterID"]);
            o.DocNo = r["DocNo"].ToString();
            o.Status = r["Status"].ToString();
            o.VendorId = Convert.ToInt64(r["VendorID"]);
            o.WarehouseId = Convert.ToInt64(r["WarehouseID"]);
            o.OrderDate = r["DocDate"].ToString();
            o.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return o;
        }

        public long InsertInvoice(SQLiteConnection con, SQLiteTransaction tr, PurInvoice d, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO PurInvoice (CompanyID, CenterID, DocNo, Status, InvoiceDate, ReceiptID, OrderID, VendorID, AmountMinor, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @no, @st, @dt, @r, @o, @v, @a, 0, 1, @n, @n, @u, @u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", d.CompanyId);
                cmd.Parameters.AddWithValue("@ctr", d.CenterId);
                cmd.Parameters.AddWithValue("@no", d.DocNo);
                cmd.Parameters.AddWithValue("@st", d.Status);
                cmd.Parameters.AddWithValue("@dt", d.InvoiceDate);
                cmd.Parameters.AddWithValue("@r", d.ReceiptId);
                cmd.Parameters.AddWithValue("@o", d.OrderId);
                cmd.Parameters.AddWithValue("@v", d.VendorId);
                cmd.Parameters.AddWithValue("@a", d.AmountMinor);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public PurInvoice GetInvoice(long id)
        {
            DataRow r = GetHeader("PurInvoice", "InvoiceID", id);
            if (r == null) return null;
            PurInvoice d = new PurInvoice();
            d.InvoiceId = Convert.ToInt64(r["InvoiceID"]);
            d.CompanyId = Convert.ToInt32(r["CompanyID"]);
            d.CenterId = Convert.ToInt32(r["CenterID"]);
            d.DocNo = r["DocNo"].ToString();
            d.Status = r["Status"].ToString();
            d.InvoiceDate = r["InvoiceDate"].ToString();
            d.ReceiptId = Convert.ToInt64(r["ReceiptID"]);
            d.OrderId = Convert.ToInt64(r["OrderID"]);
            d.VendorId = Convert.ToInt64(r["VendorID"]);
            d.AmountMinor = Convert.ToInt64(r["AmountMinor"]);
            d.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return d;
        }

        public IList<OpenPurchaseOrderRow> OpenOrders(int companyId, int center)
        {
            DataTable t = _db.Query(@"
SELECT o.DocNo, v.Name AS VendorName, o.Status, o.DocDate
FROM PurOrder o JOIN PurVendor v ON v.VendorID = o.VendorID
WHERE o.CompanyID = @c AND o.IsDeleted = 0 AND o.Status <> @p AND (@ctr = 0 OR o.CenterID = @ctr)
ORDER BY o.OrderID DESC;",
                P("@c", companyId), P("@p", TradeCodes.Posted), P("@ctr", center));
            List<OpenPurchaseOrderRow> list = new List<OpenPurchaseOrderRow>();
            foreach (DataRow r in t.Rows)
            {
                OpenPurchaseOrderRow row = new OpenPurchaseOrderRow();
                row.DocNo = r["DocNo"].ToString();
                row.VendorName = r["VendorName"].ToString();
                row.Status = r["Status"].ToString();
                row.OrderDate = r["DocDate"].ToString();
                list.Add(row);
            }
            return list;
        }

        public IList<VendorPurchaseRow> VendorPurchases(int companyId)
        {
            DataTable t = _db.Query(@"
SELECT v.Code, v.Name, COALESCE(SUM(i.AmountMinor),0) AS Amt
FROM PurVendor v
LEFT JOIN PurInvoice i ON i.VendorID = v.VendorID AND i.Status = @p AND i.IsDeleted = 0
WHERE v.CompanyID = @c AND v.IsDeleted = 0
GROUP BY v.VendorID, v.Code, v.Name ORDER BY v.Code;",
                P("@c", companyId), P("@p", TradeCodes.Posted));
            List<VendorPurchaseRow> list = new List<VendorPurchaseRow>();
            foreach (DataRow r in t.Rows)
            {
                VendorPurchaseRow row = new VendorPurchaseRow();
                row.VendorCode = r["Code"].ToString();
                row.VendorName = r["Name"].ToString();
                row.AmountMinor = Convert.ToInt64(r["Amt"]);
                list.Add(row);
            }
            return list;
        }

        public IList<PurInvoice> PurchaseHistory(int companyId, int center)
        {
            DataTable t = _db.Query(@"
SELECT * FROM PurInvoice WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr)
ORDER BY InvoiceID DESC LIMIT 200;", P("@c", companyId), P("@ctr", center));
            List<PurInvoice> list = new List<PurInvoice>();
            foreach (DataRow r in t.Rows)
            {
                PurInvoice d = new PurInvoice();
                d.InvoiceId = Convert.ToInt64(r["InvoiceID"]);
                d.DocNo = r["DocNo"].ToString();
                d.Status = r["Status"].ToString();
                d.InvoiceDate = r["InvoiceDate"].ToString();
                d.AmountMinor = Convert.ToInt64(r["AmountMinor"]);
                d.VendorId = Convert.ToInt64(r["VendorID"]);
                list.Add(d);
            }
            return list;
        }

        public IList<GrIrRow> GrIr(int companyId)
        {
            DataTable t2 = _db.Query(@"
SELECT g.DocNo AS ReceiptNo, i.DocNo AS InvoiceNo, COALESCE(i.AmountMinor,0) AS InvoiceAmt
FROM PurGoodsReceipt g
LEFT JOIN PurInvoice i ON i.ReceiptID = g.ReceiptID AND i.IsDeleted = 0
WHERE g.CompanyID = @c AND g.IsDeleted = 0;", P("@c", companyId));
            List<GrIrRow> list = new List<GrIrRow>();
            foreach (DataRow r in t2.Rows)
            {
                GrIrRow row = new GrIrRow();
                row.ReceiptNo = r["ReceiptNo"].ToString();
                row.InvoiceNo = r["InvoiceNo"] == DBNull.Value ? "" : r["InvoiceNo"].ToString();
                row.InvoiceAmountMinor = Convert.ToInt64(r["InvoiceAmt"]);
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

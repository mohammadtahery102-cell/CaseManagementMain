using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Pos.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Pos.Infrastructure
{
    public class PosStore
    {
        private readonly DatabaseHelper _db;
        public PosStore() : this(new DatabaseHelper()) { }
        public PosStore(DatabaseHelper db) { _db = db; }

        public void ExecuteInTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            _db.ExecuteInTransaction(action);
        }

        public bool RequiresApproval(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT RequiresApproval FROM PosSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v == null || v == DBNull.Value || Convert.ToInt32(v) != 0;
        }

        public long InsertTerminal(int companyId, int centerId, string code, string name, long warehouseId, string now, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO PosTerminal (CompanyID, CenterID, Code, Name, WarehouseID, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c,@ctr,@code,@name,@w,0,1,@n,@n,@u,@u);",
                P("@c", companyId), P("@ctr", centerId), P("@code", code), P("@name", name), P("@w", warehouseId), P("@n", now), P("@u", user ?? ""));
            object id = _db.ExecuteScalar("SELECT TerminalID FROM PosTerminal WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;", P("@c", companyId), P("@code", code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        public long InsertDrawer(int companyId, int centerId, long terminalId, string code, string name, string now, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO PosDrawer (CompanyID, CenterID, TerminalID, Code, Name, FloatMinor, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c,@ctr,@t,@code,@name,0,0,1,@n,@n,@u,@u);",
                P("@c", companyId), P("@ctr", centerId), P("@t", terminalId), P("@code", code), P("@name", name), P("@n", now), P("@u", user ?? ""));
            object id = _db.ExecuteScalar("SELECT DrawerID FROM PosDrawer WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;", P("@c", companyId), P("@code", code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        public DataRow GetTerminal(long id)
        {
            DataTable t = _db.Query("SELECT * FROM PosTerminal WHERE TerminalID = @id AND IsDeleted = 0;", P("@id", id));
            return t.Rows.Count == 0 ? null : t.Rows[0];
        }

        public string AllocateNo(SQLiteConnection con, SQLiteTransaction tr, int companyId, string kind)
        {
            long next = 1;
            string prefix = "POS";
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT NextDocNumber, NumberPrefix FROM PosSetting WHERE CompanyID = @c AND IsDeleted = 0;", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read()) { next = Convert.ToInt64(r["NextDocNumber"]); prefix = Convert.ToString(r["NumberPrefix"]); }
                }
            }
            using (SQLiteCommand upd = new SQLiteCommand("UPDATE PosSetting SET NextDocNumber = NextDocNumber + 1 WHERE CompanyID = @c;", con, tr))
            {
                upd.Parameters.AddWithValue("@c", companyId);
                upd.ExecuteNonQuery();
            }
            return prefix + "-" + kind + next.ToString();
        }

        public long InsertSale(SQLiteConnection con, SQLiteTransaction tr, PosSale s, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO PosSale (CompanyID, CenterID, TerminalID, DrawerID, WarehouseID, CustomerID, DocNo, Status, SaleDate, AmountMinor, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c,@ctr,@t,@d,@w,@cust,@no,@st,@dt,@amt,0,1,@n,@n,@u,@u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", s.CompanyId);
                cmd.Parameters.AddWithValue("@ctr", s.CenterId);
                cmd.Parameters.AddWithValue("@t", s.TerminalId);
                cmd.Parameters.AddWithValue("@d", s.DrawerId);
                cmd.Parameters.AddWithValue("@w", s.WarehouseId);
                cmd.Parameters.AddWithValue("@cust", s.CustomerId);
                cmd.Parameters.AddWithValue("@no", s.DocNo);
                cmd.Parameters.AddWithValue("@st", TradeCodes.Draft);
                cmd.Parameters.AddWithValue("@dt", s.SaleDate);
                cmd.Parameters.AddWithValue("@amt", s.AmountMinor);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public void InsertSaleLine(SQLiteConnection con, SQLiteTransaction tr, long saleId, int companyId, int lineNo, PosLine line)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "INSERT INTO PosSaleLine (SaleID, CompanyID, LineNo, ItemID, Qty, UnitPriceMinor, AmountMinor) VALUES (@s,@c,@n,@i,@q,@p,@a);", con, tr))
            {
                cmd.Parameters.AddWithValue("@s", saleId);
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@n", lineNo);
                cmd.Parameters.AddWithValue("@i", line.ItemId);
                cmd.Parameters.AddWithValue("@q", line.Qty);
                cmd.Parameters.AddWithValue("@p", line.UnitPriceMinor);
                cmd.Parameters.AddWithValue("@a", line.Qty * line.UnitPriceMinor);
                cmd.ExecuteNonQuery();
            }
        }

        public PosSale GetSale(long id)
        {
            DataTable t = _db.Query("SELECT * FROM PosSale WHERE SaleID = @id AND IsDeleted = 0;", P("@id", id));
            if (t.Rows.Count == 0) return null;
            DataRow r = t.Rows[0];
            PosSale s = new PosSale();
            s.SaleId = Convert.ToInt64(r["SaleID"]);
            s.CompanyId = Convert.ToInt32(r["CompanyID"]);
            s.CenterId = Convert.ToInt32(r["CenterID"]);
            s.TerminalId = Convert.ToInt64(r["TerminalID"]);
            s.DrawerId = Convert.ToInt64(r["DrawerID"]);
            s.WarehouseId = Convert.ToInt64(r["WarehouseID"]);
            s.CustomerId = Convert.ToInt64(r["CustomerID"]);
            s.DocNo = Convert.ToString(r["DocNo"]);
            s.Status = Convert.ToString(r["Status"]);
            s.SaleDate = Convert.ToString(r["SaleDate"]);
            s.AmountMinor = Convert.ToInt64(r["AmountMinor"]);
            s.OrderId = r["OrderID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["OrderID"]);
            s.DeliveryId = r["DeliveryID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["DeliveryID"]);
            s.InvoiceId = r["InvoiceID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["InvoiceID"]);
            s.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return s;
        }

        public IList<PosLine> SaleLines(long saleId)
        {
            List<PosLine> list = new List<PosLine>();
            DataTable t = _db.Query("SELECT ItemID, Qty, UnitPriceMinor FROM PosSaleLine WHERE SaleID = @id ORDER BY LineNo;", P("@id", saleId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                PosLine l = new PosLine();
                l.ItemId = Convert.ToInt64(t.Rows[i]["ItemID"]);
                l.Qty = Convert.ToInt64(t.Rows[i]["Qty"]);
                l.UnitPriceMinor = Convert.ToInt64(t.Rows[i]["UnitPriceMinor"]);
                list.Add(l);
            }
            return list;
        }

        public bool UpdateSaleStatus(long id, string status, long rv, string now, string user)
        {
            return _db.ExecuteNonQuery("UPDATE PosSale SET Status = @st, RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u WHERE SaleID = @id AND RowVersion = @rv AND IsDeleted = 0;",
                P("@st", status), P("@n", now), P("@u", user ?? ""), P("@id", id), P("@rv", rv)) == 1;
        }

        public void SetSaleLinks(long id, long orderId, long deliveryId, long invoiceId, long customerId)
        {
            _db.ExecuteNonQuery("UPDATE PosSale SET OrderID = @o, DeliveryID = @d, InvoiceID = @i, CustomerID = @c WHERE SaleID = @id;",
                P("@o", orderId), P("@d", deliveryId), P("@i", invoiceId), P("@c", customerId), P("@id", id));
        }

        public long InsertReturn(SQLiteConnection con, SQLiteTransaction tr, PosReturnDoc d, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO PosReturn (CompanyID, CenterID, SaleID, DocNo, Status, ReturnDate, AmountMinor, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c,@ctr,@s,@no,@st,@dt,@amt,0,1,@n,@n,@u,@u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", d.CompanyId);
                cmd.Parameters.AddWithValue("@ctr", d.CenterId);
                cmd.Parameters.AddWithValue("@s", d.SaleId);
                cmd.Parameters.AddWithValue("@no", d.DocNo);
                cmd.Parameters.AddWithValue("@st", TradeCodes.Draft);
                cmd.Parameters.AddWithValue("@dt", d.ReturnDate);
                cmd.Parameters.AddWithValue("@amt", d.AmountMinor);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public void InsertReturnLine(SQLiteConnection con, SQLiteTransaction tr, long returnId, int companyId, int lineNo, PosLine line)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "INSERT INTO PosReturnLine (ReturnID, CompanyID, LineNo, ItemID, Qty, UnitPriceMinor, AmountMinor) VALUES (@r,@c,@n,@i,@q,@p,@a);", con, tr))
            {
                cmd.Parameters.AddWithValue("@r", returnId);
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@n", lineNo);
                cmd.Parameters.AddWithValue("@i", line.ItemId);
                cmd.Parameters.AddWithValue("@q", line.Qty);
                cmd.Parameters.AddWithValue("@p", line.UnitPriceMinor);
                cmd.Parameters.AddWithValue("@a", line.Qty * line.UnitPriceMinor);
                cmd.ExecuteNonQuery();
            }
        }

        public PosReturnDoc GetReturn(long id)
        {
            DataTable t = _db.Query("SELECT * FROM PosReturn WHERE ReturnID = @id AND IsDeleted = 0;", P("@id", id));
            if (t.Rows.Count == 0) return null;
            DataRow r = t.Rows[0];
            PosReturnDoc d = new PosReturnDoc();
            d.ReturnId = Convert.ToInt64(r["ReturnID"]);
            d.CompanyId = Convert.ToInt32(r["CompanyID"]);
            d.CenterId = Convert.ToInt32(r["CenterID"]);
            d.SaleId = Convert.ToInt64(r["SaleID"]);
            d.DocNo = Convert.ToString(r["DocNo"]);
            d.Status = Convert.ToString(r["Status"]);
            d.ReturnDate = Convert.ToString(r["ReturnDate"]);
            d.AmountMinor = Convert.ToInt64(r["AmountMinor"]);
            d.InvDocumentId = r["InvDocumentID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["InvDocumentID"]);
            d.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return d;
        }

        public IList<PosLine> ReturnLines(long returnId)
        {
            List<PosLine> list = new List<PosLine>();
            DataTable t = _db.Query("SELECT ItemID, Qty, UnitPriceMinor FROM PosReturnLine WHERE ReturnID = @id ORDER BY LineNo;", P("@id", returnId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                PosLine l = new PosLine();
                l.ItemId = Convert.ToInt64(t.Rows[i]["ItemID"]);
                l.Qty = Convert.ToInt64(t.Rows[i]["Qty"]);
                l.UnitPriceMinor = Convert.ToInt64(t.Rows[i]["UnitPriceMinor"]);
                list.Add(l);
            }
            return list;
        }

        public bool UpdateReturnStatus(long id, string status, long rv, string now, string user)
        {
            return _db.ExecuteNonQuery("UPDATE PosReturn SET Status = @st, RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u WHERE ReturnID = @id AND RowVersion = @rv AND IsDeleted = 0;",
                P("@st", status), P("@n", now), P("@u", user ?? ""), P("@id", id), P("@rv", rv)) == 1;
        }

        public void SetReturnInv(long id, long invId)
        {
            _db.ExecuteNonQuery("UPDATE PosReturn SET InvDocumentID = @i WHERE ReturnID = @id;", P("@i", invId), P("@id", id));
        }

        public IList<DailySalesRow> DailySales(int companyId, int centerId)
        {
            List<DailySalesRow> list = new List<DailySalesRow>();
            DataTable t = _db.Query("SELECT DocNo, SaleDate, AmountMinor, Status FROM PosSale WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr);",
                P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                DailySalesRow r = new DailySalesRow();
                r.DocNo = Convert.ToString(t.Rows[i]["DocNo"]);
                r.SaleDate = Convert.ToString(t.Rows[i]["SaleDate"]);
                r.AmountMinor = Convert.ToInt64(t.Rows[i]["AmountMinor"]);
                r.Status = Convert.ToString(t.Rows[i]["Status"]);
                list.Add(r);
            }
            return list;
        }

        public IList<CashSummaryRow> CashSummary(int companyId, int centerId)
        {
            List<CashSummaryRow> list = new List<CashSummaryRow>();
            DataTable t = _db.Query(@"
SELECT t.Code, SUM(s.AmountMinor) AS Amt FROM PosSale s JOIN PosTerminal t ON t.TerminalID = s.TerminalID
WHERE s.CompanyID = @c AND s.IsDeleted = 0 AND s.Status = @st AND (@ctr = 0 OR s.CenterID = @ctr)
GROUP BY t.Code;",
                P("@c", companyId), P("@ctr", centerId), P("@st", TradeCodes.Posted));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                CashSummaryRow r = new CashSummaryRow();
                r.TerminalCode = Convert.ToString(t.Rows[i]["Code"]);
                r.AmountMinor = Convert.ToInt64(t.Rows[i]["Amt"]);
                list.Add(r);
            }
            return list;
        }

        public IList<PosTxnRow> Transactions(int companyId, int centerId)
        {
            List<PosTxnRow> list = new List<PosTxnRow>();
            DataTable sales = _db.Query("SELECT DocNo, AmountMinor, Status FROM PosSale WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr);",
                P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < sales.Rows.Count; i++)
            {
                PosTxnRow r = new PosTxnRow();
                r.Kind = "Sale";
                r.DocNo = Convert.ToString(sales.Rows[i]["DocNo"]);
                r.AmountMinor = Convert.ToInt64(sales.Rows[i]["AmountMinor"]);
                r.Status = Convert.ToString(sales.Rows[i]["Status"]);
                list.Add(r);
            }
            DataTable rets = _db.Query("SELECT DocNo, AmountMinor, Status FROM PosReturn WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr);",
                P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < rets.Rows.Count; i++)
            {
                PosTxnRow r = new PosTxnRow();
                r.Kind = "Return";
                r.DocNo = Convert.ToString(rets.Rows[i]["DocNo"]);
                r.AmountMinor = Convert.ToInt64(rets.Rows[i]["AmountMinor"]);
                r.Status = Convert.ToString(rets.Rows[i]["Status"]);
                list.Add(r);
            }
            return list;
        }

        private static SQLiteParameter P(string n, object v) { return new SQLiteParameter(n, v ?? DBNull.Value); }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;
using CaseManagement.Inventory.Domain;

namespace CaseManagement.Inventory.Infrastructure
{
    public class InventoryStore
    {
        private readonly DatabaseHelper _db;

        public InventoryStore() : this(new DatabaseHelper()) { }

        public InventoryStore(DatabaseHelper db) { _db = db; }

        public void ExecuteInTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            _db.ExecuteInTransaction(action);
        }

        public InvWarehouse GetWarehouse(long id)
        {
            return MapWarehouse(QueryRow("SELECT * FROM InvWarehouse WHERE WarehouseID = @id AND IsDeleted = 0;", P("@id", id)));
        }

        public InvLocation GetLocation(long id)
        {
            return MapLocation(QueryRow("SELECT * FROM InvLocation WHERE LocationID = @id AND IsDeleted = 0;", P("@id", id)));
        }

        public InvItem GetItem(long id)
        {
            return MapItem(QueryRow("SELECT * FROM InvItem WHERE ItemID = @id AND IsDeleted = 0;", P("@id", id)));
        }

        public InvItem GetItemByCode(int companyId, string code)
        {
            return MapItem(QueryRow("SELECT * FROM InvItem WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", companyId), P("@code", code)));
        }

        public InvDocument GetDocument(long id)
        {
            InvDocument d = MapDocument(QueryRow("SELECT * FROM InvDocument WHERE DocumentID = @id AND IsDeleted = 0;", P("@id", id)));
            return d;
        }

        public InvDocument GetDocument(SQLiteConnection con, SQLiteTransaction tr, long id)
        {
            return MapDocument(QueryRow(con, tr, "SELECT * FROM InvDocument WHERE DocumentID = @id AND IsDeleted = 0;", P("@id", id)));
        }

        public IList<InvDocumentLine> ListLines(long documentId)
        {
            return MapLines(Query("SELECT * FROM InvDocumentLine WHERE DocumentID = @id ORDER BY LineNo;", P("@id", documentId)));
        }

        public IList<InvDocumentLine> ListLines(SQLiteConnection con, SQLiteTransaction tr, long documentId)
        {
            return MapLines(Query(con, tr, "SELECT * FROM InvDocumentLine WHERE DocumentID = @id ORDER BY LineNo;", P("@id", documentId)));
        }

        public IList<InvItemLedger> ListLedgerForDocument(long documentId)
        {
            return MapLedgers(Query("SELECT * FROM InvItemLedger WHERE DocumentID = @id ORDER BY ItemLedgerID;", P("@id", documentId)));
        }

        public long ResolveAccount(int companyId, long itemId, long categoryId, string role)
        {
            object v = _db.ExecuteScalar(@"
SELECT AccountID FROM InvItemMap
WHERE CompanyID = @c AND IsDeleted = 0 AND MapRole = @r
  AND ((OwnerType = 'Item' AND OwnerID = @item)
    OR (OwnerType = 'Category' AND OwnerID = @cat)
    OR (OwnerType = 'Company' AND OwnerID = @c))
ORDER BY CASE OwnerType WHEN 'Item' THEN 0 WHEN 'Category' THEN 1 ELSE 2 END
LIMIT 1;",
                P("@c", companyId), P("@r", role), P("@item", itemId), P("@cat", categoryId));
            if (v == null || v == DBNull.Value) return 0;
            return Convert.ToInt64(v);
        }

        public InvItemBalance GetBalance(int companyId, long itemId, long warehouseId, long locationId)
        {
            return MapBalance(QueryRow(@"
SELECT * FROM InvItemBalance
WHERE CompanyID = @c AND ItemID = @i AND WarehouseID = @w AND LocationID = @l;",
                P("@c", companyId), P("@i", itemId), P("@w", warehouseId), P("@l", locationId)));
        }

        public WarehouseTotals GetWarehouseTotals(int companyId, long itemId, long warehouseId)
        {
            WarehouseTotals t = new WarehouseTotals();
            DataTable table = Query(@"
SELECT COALESCE(SUM(QuantityOnHand),0) AS Q, COALESCE(SUM(InventoryValueMinor),0) AS V
FROM InvItemBalance WHERE CompanyID = @c AND ItemID = @i AND WarehouseID = @w;",
                P("@c", companyId), P("@i", itemId), P("@w", warehouseId));
            if (table.Rows.Count > 0)
            {
                t.Qty = Convert.ToInt64(table.Rows[0]["Q"]);
                t.Value = Convert.ToInt64(table.Rows[0]["V"]);
            }
            t.Avg = t.Qty == 0 ? 0 : t.Value / t.Qty;
            return t;
        }

        public long InsertItem(InvItem item)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO InvItem (CompanyID, CenterID, Code, Name, CategoryID, BaseUomID, CostingMethod, IsStockable,
  MinQtyBase, MaxQtyBase, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 0, @code, @name, @cat, @uom, @cm, @stk, @min, @max, 1, 0, 1, @now, @now, @by, @by);",
                P("@c", item.CompanyId), P("@code", item.Code), P("@name", item.Name), P("@cat", item.CategoryId),
                P("@uom", item.BaseUomId), P("@cm", item.CostingMethod), P("@stk", item.IsStockable ? 1 : 0),
                P("@min", item.MinQtyBase), P("@max", item.MaxQtyBase), P("@now", item.CreatedAt), P("@by", item.CreatedBy));
            object id = _db.ExecuteScalar("SELECT ItemID FROM InvItem WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", item.CompanyId), P("@code", item.Code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        public long DefaultCategoryId(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT CategoryID FROM InvItemCategory WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY CategoryID LIMIT 1;",
                P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long DefaultUomId(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT UomID FROM InvUnitOfMeasure WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY UomID LIMIT 1;",
                P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long DefaultWarehouseId(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT WarehouseID FROM InvWarehouse WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY WarehouseID LIMIT 1;",
                P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long DefaultLocationId(long warehouseId)
        {
            object v = _db.ExecuteScalar("SELECT LocationID FROM InvLocation WHERE WarehouseID = @w AND IsDeleted = 0 AND IsLeaf = 1 ORDER BY LocationID LIMIT 1;",
                P("@w", warehouseId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public string AllocateDocNo(SQLiteConnection con, SQLiteTransaction tr, int companyId, string type)
        {
            long next;
            string prefix;
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT NextDocNumber, NumberPrefix FROM InvSetting WHERE CompanyID = @c AND IsDeleted = 0;", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return type + "-1";
                    next = Convert.ToInt64(r["NextDocNumber"]);
                    prefix = Convert.ToString(r["NumberPrefix"]);
                }
            }
            using (SQLiteCommand upd = new SQLiteCommand(
                "UPDATE InvSetting SET NextDocNumber = NextDocNumber + 1, RowVersion = RowVersion + 1 WHERE CompanyID = @c;", con, tr))
            {
                upd.Parameters.AddWithValue("@c", companyId);
                upd.ExecuteNonQuery();
            }
            return (prefix ?? "INV") + "-" + type.Substring(0, 1) + next.ToString();
        }

        public long InsertDocument(SQLiteConnection con, SQLiteTransaction tr, InvDocument d)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO InvDocument (CompanyID, CenterID, DocNo, DocumentType, Status, PostingDate, WarehouseID, ToLocationID,
  CostCenterID, ProjectID, PartyID, SourceModule, SourceDocumentType, SourceDocumentID, GroupId, Description,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @no, @ty, @st, @dt, @wh, @to, @cc, @pr, @pty, @sm, @sdt, @sid, @g, @desc,
  0, 1, @now, @now, @by, @by);", con, tr))
            {
                BindDoc(cmd, d);
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public void InsertLine(SQLiteConnection con, SQLiteTransaction tr, InvDocumentLine line, int companyId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO InvDocumentLine (DocumentID, CompanyID, LineNo, ItemID, LocationID, QtyDoc, QtyBase, UnitCostMinor, ValueMinor, CostCenterID, ProjectID)
VALUES (@d, @c, @n, @i, @l, @qd, @qb, @uc, @vm, @cc, @pr);", con, tr))
            {
                cmd.Parameters.AddWithValue("@d", line.DocumentId);
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@n", line.LineNo);
                cmd.Parameters.AddWithValue("@i", line.ItemId);
                cmd.Parameters.AddWithValue("@l", line.LocationId);
                cmd.Parameters.AddWithValue("@qd", line.QtyDoc);
                cmd.Parameters.AddWithValue("@qb", line.QtyBase);
                cmd.Parameters.AddWithValue("@uc", line.UnitCostMinor);
                cmd.Parameters.AddWithValue("@vm", line.ValueMinor);
                cmd.Parameters.AddWithValue("@cc", (object)line.CostCenterId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pr", (object)line.ProjectId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateLineValue(SQLiteConnection con, SQLiteTransaction tr, long documentId, int lineNo, long unit, long value)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "UPDATE InvDocumentLine SET UnitCostMinor = @u, ValueMinor = @v WHERE DocumentID = @d AND LineNo = @n;", con, tr))
            {
                cmd.Parameters.AddWithValue("@u", unit);
                cmd.Parameters.AddWithValue("@v", value);
                cmd.Parameters.AddWithValue("@d", documentId);
                cmd.Parameters.AddWithValue("@n", lineNo);
                cmd.ExecuteNonQuery();
            }
        }

        public bool UpdateDocumentStatus(SQLiteConnection con, SQLiteTransaction tr, InvDocument d, long expectedRv)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
UPDATE InvDocument SET Status = @st, UpdatedAt = @ua, UpdatedBy = @ub, RowVersion = RowVersion + 1
WHERE DocumentID = @id AND RowVersion = @rv;", con, tr))
            {
                cmd.Parameters.AddWithValue("@st", d.Status);
                cmd.Parameters.AddWithValue("@ua", d.UpdatedAt);
                cmd.Parameters.AddWithValue("@ub", d.UpdatedBy);
                cmd.Parameters.AddWithValue("@id", d.DocumentId);
                cmd.Parameters.AddWithValue("@rv", expectedRv);
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public void InsertLedger(SQLiteConnection con, SQLiteTransaction tr, InvItemLedger row)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO InvItemLedger (CompanyID, CenterID, ItemID, WarehouseID, LocationID, DocumentType, DocumentID, DocumentLineNo,
  MovementType, QtyBase, UnitCostMinor, ValueMinor, PostingDate, CostCenterID, ProjectID, ReversesLedgerID, CreatedAt, CreatedBy)
VALUES (@c, @ctr, @i, @w, @l, @dt, @did, @ln, @mv, @q, @u, @v, @pd, @cc, @pr, @rev, @now, @by);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", row.CompanyId);
                cmd.Parameters.AddWithValue("@ctr", row.CenterId);
                cmd.Parameters.AddWithValue("@i", row.ItemId);
                cmd.Parameters.AddWithValue("@w", row.WarehouseId);
                cmd.Parameters.AddWithValue("@l", row.LocationId);
                cmd.Parameters.AddWithValue("@dt", row.DocumentType);
                cmd.Parameters.AddWithValue("@did", row.DocumentId);
                cmd.Parameters.AddWithValue("@ln", row.DocumentLineNo);
                cmd.Parameters.AddWithValue("@mv", row.MovementType);
                cmd.Parameters.AddWithValue("@q", row.QtyBase);
                cmd.Parameters.AddWithValue("@u", row.UnitCostMinor);
                cmd.Parameters.AddWithValue("@v", row.ValueMinor);
                cmd.Parameters.AddWithValue("@pd", row.PostingDate);
                cmd.Parameters.AddWithValue("@cc", (object)row.CostCenterId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pr", (object)row.ProjectId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rev", DBNull.Value);
                cmd.Parameters.AddWithValue("@now", row.PostingDate);
                cmd.Parameters.AddWithValue("@by", row.CreatedBy ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        public void ApplyBalanceDelta(SQLiteConnection con, SQLiteTransaction tr,
            int companyId, long itemId, long warehouseId, long locationId,
            long qtyDelta, long valueDelta, string now, string user, long warehouseAvg)
        {
            using (SQLiteCommand sel = new SQLiteCommand(@"
SELECT BalanceID, QuantityOnHand, InventoryValueMinor, RowVersion
FROM InvItemBalance WHERE CompanyID = @c AND ItemID = @i AND WarehouseID = @w AND LocationID = @l;", con, tr))
            {
                sel.Parameters.AddWithValue("@c", companyId);
                sel.Parameters.AddWithValue("@i", itemId);
                sel.Parameters.AddWithValue("@w", warehouseId);
                sel.Parameters.AddWithValue("@l", locationId);
                object id = null;
                long qty = 0, val = 0, rv = 1;
                using (SQLiteDataReader r = sel.ExecuteReader())
                {
                    if (r.Read())
                    {
                        id = r["BalanceID"];
                        qty = Convert.ToInt64(r["QuantityOnHand"]);
                        val = Convert.ToInt64(r["InventoryValueMinor"]);
                        rv = Convert.ToInt64(r["RowVersion"]);
                    }
                }
                long nq = qty + qtyDelta;
                long nv = val + valueDelta;
                if (id == null)
                {
                    using (SQLiteCommand ins = new SQLiteCommand(@"
INSERT INTO InvItemBalance (CompanyID, ItemID, WarehouseID, LocationID, QuantityOnHand, InventoryValueMinor, AverageCostMinor, RowVersion, UpdatedAt, UpdatedBy)
VALUES (@c, @i, @w, @l, @q, @v, @a, 1, @now, @by);", con, tr))
                    {
                        ins.Parameters.AddWithValue("@c", companyId);
                        ins.Parameters.AddWithValue("@i", itemId);
                        ins.Parameters.AddWithValue("@w", warehouseId);
                        ins.Parameters.AddWithValue("@l", locationId);
                        ins.Parameters.AddWithValue("@q", nq);
                        ins.Parameters.AddWithValue("@v", nv);
                        ins.Parameters.AddWithValue("@a", warehouseAvg);
                        ins.Parameters.AddWithValue("@now", now);
                        ins.Parameters.AddWithValue("@by", user ?? "");
                        ins.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (SQLiteCommand upd = new SQLiteCommand(@"
UPDATE InvItemBalance SET QuantityOnHand = @q, InventoryValueMinor = @v, AverageCostMinor = @a,
  RowVersion = RowVersion + 1, UpdatedAt = @now, UpdatedBy = @by
WHERE BalanceID = @id;", con, tr))
                    {
                        upd.Parameters.AddWithValue("@q", nq);
                        upd.Parameters.AddWithValue("@v", nv);
                        upd.Parameters.AddWithValue("@a", warehouseAvg);
                        upd.Parameters.AddWithValue("@now", now);
                        upd.Parameters.AddWithValue("@by", user ?? "");
                        upd.Parameters.AddWithValue("@id", id);
                        upd.ExecuteNonQuery();
                    }
                }
            }

            using (SQLiteCommand avg = new SQLiteCommand(@"
UPDATE InvItemBalance SET AverageCostMinor = @a
WHERE CompanyID = @c AND ItemID = @i AND WarehouseID = @w;", con, tr))
            {
                avg.Parameters.AddWithValue("@a", warehouseAvg);
                avg.Parameters.AddWithValue("@c", companyId);
                avg.Parameters.AddWithValue("@i", itemId);
                avg.Parameters.AddWithValue("@w", warehouseId);
                avg.ExecuteNonQuery();
            }
        }

        public bool AllowNegativeStock(int companyId)
        {
            object v = _db.ExecuteScalar(
                "SELECT AllowNegativeStock FROM InvSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v != null && v != DBNull.Value && Convert.ToInt32(v) != 0;
        }

        public bool RequiresApproval(int companyId)
        {
            object v = _db.ExecuteScalar(
                "SELECT RequiresApproval FROM InvSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v != null && v != DBNull.Value && Convert.ToInt32(v) != 0;
        }

        public WarehouseTotals GetWarehouseTotals(SQLiteConnection con, SQLiteTransaction tr, int companyId, long itemId, long warehouseId)
        {
            DataTable table = Query(con, tr, @"
SELECT COALESCE(SUM(QuantityOnHand),0) AS Q, COALESCE(SUM(InventoryValueMinor),0) AS V
FROM InvItemBalance WHERE CompanyID = @c AND ItemID = @i AND WarehouseID = @w;",
                P("@c", companyId), P("@i", itemId), P("@w", warehouseId));
            WarehouseTotals t = new WarehouseTotals();
            if (table.Rows.Count > 0)
            {
                t.Qty = Convert.ToInt64(table.Rows[0]["Q"]);
                t.Value = Convert.ToInt64(table.Rows[0]["V"]);
            }
            t.Avg = t.Qty == 0 ? 0 : t.Value / t.Qty;
            return t;
        }

        public void ApplyQtyDelta(SQLiteConnection con, SQLiteTransaction tr,
            int companyId, long itemId, long warehouseId, long locationId, long qtyDelta, string now, string user)
        {
            using (SQLiteCommand sel = new SQLiteCommand(@"
SELECT BalanceID, QuantityOnHand FROM InvItemBalance
WHERE CompanyID = @c AND ItemID = @i AND WarehouseID = @w AND LocationID = @l;", con, tr))
            {
                sel.Parameters.AddWithValue("@c", companyId);
                sel.Parameters.AddWithValue("@i", itemId);
                sel.Parameters.AddWithValue("@w", warehouseId);
                sel.Parameters.AddWithValue("@l", locationId);
                object id = null;
                long qty = 0;
                using (SQLiteDataReader r = sel.ExecuteReader())
                {
                    if (r.Read())
                    {
                        id = r["BalanceID"];
                        qty = Convert.ToInt64(r["QuantityOnHand"]);
                    }
                }
                long nq = qty + qtyDelta;
                if (id == null)
                {
                    using (SQLiteCommand ins = new SQLiteCommand(@"
INSERT INTO InvItemBalance (CompanyID, ItemID, WarehouseID, LocationID, QuantityOnHand, InventoryValueMinor, AverageCostMinor, RowVersion, UpdatedAt, UpdatedBy)
VALUES (@c, @i, @w, @l, @q, 0, 0, 1, @now, @by);", con, tr))
                    {
                        ins.Parameters.AddWithValue("@c", companyId);
                        ins.Parameters.AddWithValue("@i", itemId);
                        ins.Parameters.AddWithValue("@w", warehouseId);
                        ins.Parameters.AddWithValue("@l", locationId);
                        ins.Parameters.AddWithValue("@q", nq);
                        ins.Parameters.AddWithValue("@now", now);
                        ins.Parameters.AddWithValue("@by", user ?? "");
                        ins.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (SQLiteCommand upd = new SQLiteCommand(@"
UPDATE InvItemBalance SET QuantityOnHand = @q, RowVersion = RowVersion + 1, UpdatedAt = @now, UpdatedBy = @by
WHERE BalanceID = @id;", con, tr))
                    {
                        upd.Parameters.AddWithValue("@q", nq);
                        upd.Parameters.AddWithValue("@now", now);
                        upd.Parameters.AddWithValue("@by", user ?? "");
                        upd.Parameters.AddWithValue("@id", id);
                        upd.ExecuteNonQuery();
                    }
                }
            }
        }

        public void AllocateWarehouseValue(SQLiteConnection con, SQLiteTransaction tr,
            int companyId, long itemId, long warehouseId, long warehouseValue, long warehouseAvg, string now, string user)
        {
            DataTable table = Query(con, tr, @"
SELECT BalanceID, QuantityOnHand FROM InvItemBalance
WHERE CompanyID = @c AND ItemID = @i AND WarehouseID = @w
ORDER BY LocationID;",
                P("@c", companyId), P("@i", itemId), P("@w", warehouseId));
            long allocated = 0;
            for (int i = 0; i < table.Rows.Count; i++)
            {
                long qty = Convert.ToInt64(table.Rows[i]["QuantityOnHand"]);
                long locVal = (i == table.Rows.Count - 1) ? (warehouseValue - allocated) : (qty * warehouseAvg);
                allocated += locVal;
                using (SQLiteCommand upd = new SQLiteCommand(@"
UPDATE InvItemBalance SET InventoryValueMinor = @v, AverageCostMinor = @a, UpdatedAt = @now, UpdatedBy = @by
WHERE BalanceID = @id;", con, tr))
                {
                    upd.Parameters.AddWithValue("@v", locVal);
                    upd.Parameters.AddWithValue("@a", warehouseAvg);
                    upd.Parameters.AddWithValue("@now", now);
                    upd.Parameters.AddWithValue("@by", user ?? "");
                    upd.Parameters.AddWithValue("@id", table.Rows[i]["BalanceID"]);
                    upd.ExecuteNonQuery();
                }
            }
        }

        public IList<long> ListInventoryMapAccountIds(int companyId)
        {
            DataTable table = Query(@"
SELECT DISTINCT AccountID FROM InvItemMap
WHERE CompanyID = @c AND IsDeleted = 0 AND MapRole = @r;",
                P("@c", companyId), P("@r", InventoryCodes.RoleInventory));
            List<long> ids = new List<long>();
            foreach (DataRow r in table.Rows) ids.Add(Convert.ToInt64(r["AccountID"]));
            return ids;
        }

        public long SumGlNet(int companyId, IList<long> accountIds)
        {
            if (accountIds == null || accountIds.Count == 0) return 0;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("SELECT COALESCE(SUM(l.DebitMinor - l.CreditMinor),0) FROM GlJournalLine l ");
            sb.Append("JOIN GlJournal j ON j.JournalID = l.JournalID ");
            sb.Append("WHERE j.CompanyID = @c AND j.IsDeleted = 0 AND j.Status = @st AND l.AccountID IN (");
            for (int i = 0; i < accountIds.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(accountIds[i].ToString());
            }
            sb.Append(");");
            object v = _db.ExecuteScalar(sb.ToString(), P("@c", companyId), P("@st", LedgerCodes.JournalPosted));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public IList<StockOnHandRow> ListOnHand(int companyId, int centerFilter)
        {
            DataTable table = Query(@"
SELECT i.Code AS ItemCode, i.Name AS ItemName, w.Code AS WarehouseCode, loc.Code AS LocationCode, w.CenterID,
  b.QuantityOnHand, b.InventoryValueMinor, b.AverageCostMinor
FROM InvItemBalance b
JOIN InvItem i ON i.ItemID = b.ItemID
JOIN InvWarehouse w ON w.WarehouseID = b.WarehouseID
JOIN InvLocation loc ON loc.LocationID = b.LocationID
WHERE b.CompanyID = @c AND (@ctr = 0 OR w.CenterID = @ctr)
ORDER BY i.Code, w.Code, loc.Code;",
                P("@c", companyId), P("@ctr", centerFilter));
            List<StockOnHandRow> list = new List<StockOnHandRow>();
            foreach (DataRow r in table.Rows)
            {
                StockOnHandRow row = new StockOnHandRow();
                row.ItemCode = r["ItemCode"].ToString();
                row.ItemName = r["ItemName"].ToString();
                row.WarehouseCode = r["WarehouseCode"].ToString();
                row.LocationCode = r["LocationCode"].ToString();
                row.CenterId = Convert.ToInt32(r["CenterID"]);
                row.QuantityOnHand = Convert.ToInt64(r["QuantityOnHand"]);
                row.InventoryValueMinor = Convert.ToInt64(r["InventoryValueMinor"]);
                row.AverageCostMinor = Convert.ToInt64(r["AverageCostMinor"]);
                list.Add(row);
            }
            return list;
        }

        public IList<InvItemLedger> ListLedger(int companyId, long itemId)
        {
            return MapLedgers(Query(@"
SELECT * FROM InvItemLedger WHERE CompanyID = @c AND (@i = 0 OR ItemID = @i)
ORDER BY PostingDate, ItemLedgerID;",
                P("@c", companyId), P("@i", itemId)));
        }

        public IList<ReorderRow> ListReorder(int companyId, int centerFilter)
        {
            DataTable table = Query(@"
SELECT i.Code, i.Name, i.MinQtyBase, COALESCE(SUM(b.QuantityOnHand),0) AS Qty
FROM InvItem i
LEFT JOIN InvItemBalance b ON b.ItemID = i.ItemID AND b.CompanyID = i.CompanyID
LEFT JOIN InvWarehouse w ON w.WarehouseID = b.WarehouseID
WHERE i.CompanyID = @c AND i.IsDeleted = 0 AND i.MinQtyBase > 0
  AND (@ctr = 0 OR w.CenterID IS NULL OR w.CenterID = @ctr)
GROUP BY i.ItemID, i.Code, i.Name, i.MinQtyBase
HAVING COALESCE(SUM(b.QuantityOnHand),0) <= i.MinQtyBase
ORDER BY i.Code;",
                P("@c", companyId), P("@ctr", centerFilter));
            List<ReorderRow> list = new List<ReorderRow>();
            foreach (DataRow r in table.Rows)
            {
                ReorderRow row = new ReorderRow();
                row.ItemCode = r["Code"].ToString();
                row.ItemName = r["Name"].ToString();
                row.MinQtyBase = Convert.ToInt64(r["MinQtyBase"]);
                row.QuantityOnHand = Convert.ToInt64(r["Qty"]);
                list.Add(row);
            }
            return list;
        }

        public long SumBalanceValue(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT COALESCE(SUM(InventoryValueMinor),0) FROM InvItemBalance WHERE CompanyID = @c;",
                P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public IList<InvItem> ListItems(int companyId)
        {
            DataTable table = Query("SELECT * FROM InvItem WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY Code;", P("@c", companyId));
            List<InvItem> list = new List<InvItem>();
            foreach (DataRow r in table.Rows) list.Add(MapItem(r));
            return list;
        }

        public IList<InvWarehouse> ListWarehouses(int companyId, int centerFilter)
        {
            DataTable table = Query(@"
SELECT * FROM InvWarehouse WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr) ORDER BY Code;",
                P("@c", companyId), P("@ctr", centerFilter));
            List<InvWarehouse> list = new List<InvWarehouse>();
            foreach (DataRow r in table.Rows) list.Add(MapWarehouse(r));
            return list;
        }

        public IList<InvDocument> ListDocuments(int companyId, int centerFilter)
        {
            DataTable table = Query(@"
SELECT * FROM InvDocument WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr)
ORDER BY DocumentID DESC LIMIT 200;",
                P("@c", companyId), P("@ctr", centerFilter));
            List<InvDocument> list = new List<InvDocument>();
            foreach (DataRow r in table.Rows) list.Add(MapDocument(r));
            return list;
        }

        public class WarehouseTotals
        {
            public long Qty;
            public long Value;
            public long Avg;
        }

        private static void BindDoc(SQLiteCommand cmd, InvDocument d)
        {
            cmd.Parameters.AddWithValue("@c", d.CompanyId);
            cmd.Parameters.AddWithValue("@ctr", d.CenterId);
            cmd.Parameters.AddWithValue("@no", d.DocNo);
            cmd.Parameters.AddWithValue("@ty", d.DocumentType);
            cmd.Parameters.AddWithValue("@st", d.Status);
            cmd.Parameters.AddWithValue("@dt", d.PostingDate);
            cmd.Parameters.AddWithValue("@wh", d.WarehouseId);
            cmd.Parameters.AddWithValue("@to", (object)d.ToLocationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cc", (object)d.CostCenterId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pr", (object)d.ProjectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pty", (object)d.PartyId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sm", (object)d.SourceModule ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sdt", (object)d.SourceDocumentType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sid", (object)d.SourceDocumentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@g", (object)d.GroupId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@desc", (object)d.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@now", d.CreatedAt);
            cmd.Parameters.AddWithValue("@by", d.CreatedBy ?? "");
        }

        private DataTable Query(string sql, params SQLiteParameter[] p)
        {
            return _db.Query(sql, p);
        }

        private DataTable Query(SQLiteConnection con, SQLiteTransaction tr, string sql, params SQLiteParameter[] p)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con, tr))
            {
                if (p != null) cmd.Parameters.AddRange(p);
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable t = new DataTable();
                    da.Fill(t);
                    return t;
                }
            }
        }

        private DataRow QueryRow(string sql, params SQLiteParameter[] p)
        {
            DataTable t = Query(sql, p);
            return t.Rows.Count == 0 ? null : t.Rows[0];
        }

        private DataRow QueryRow(SQLiteConnection con, SQLiteTransaction tr, string sql, params SQLiteParameter[] p)
        {
            DataTable t = Query(con, tr, sql, p);
            return t.Rows.Count == 0 ? null : t.Rows[0];
        }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        private static InvWarehouse MapWarehouse(DataRow r)
        {
            if (r == null) return null;
            InvWarehouse w = new InvWarehouse();
            w.WarehouseId = Convert.ToInt64(r["WarehouseID"]);
            w.CompanyId = Convert.ToInt32(r["CompanyID"]);
            w.CenterId = Convert.ToInt32(r["CenterID"]);
            w.Code = r["Code"].ToString();
            w.Name = r["Name"].ToString();
            w.IsActive = Convert.ToInt32(r["IsActive"]) != 0;
            w.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return w;
        }

        private static InvLocation MapLocation(DataRow r)
        {
            if (r == null) return null;
            InvLocation l = new InvLocation();
            l.LocationId = Convert.ToInt64(r["LocationID"]);
            l.WarehouseId = Convert.ToInt64(r["WarehouseID"]);
            l.CompanyId = Convert.ToInt32(r["CompanyID"]);
            l.Code = r["Code"].ToString();
            l.Name = r["Name"].ToString();
            l.IsLeaf = Convert.ToInt32(r["IsLeaf"]) != 0;
            l.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return l;
        }

        private static InvItem MapItem(DataRow r)
        {
            if (r == null) return null;
            InvItem i = new InvItem();
            i.ItemId = Convert.ToInt64(r["ItemID"]);
            i.CompanyId = Convert.ToInt32(r["CompanyID"]);
            i.Code = r["Code"].ToString();
            i.Name = r["Name"].ToString();
            i.CategoryId = Convert.ToInt64(r["CategoryID"]);
            i.BaseUomId = Convert.ToInt64(r["BaseUomID"]);
            i.CostingMethod = r["CostingMethod"].ToString();
            i.IsStockable = Convert.ToInt32(r["IsStockable"]) != 0;
            i.MinQtyBase = Convert.ToInt64(r["MinQtyBase"]);
            i.MaxQtyBase = Convert.ToInt64(r["MaxQtyBase"]);
            i.IsActive = Convert.ToInt32(r["IsActive"]) != 0;
            i.RowVersion = Convert.ToInt64(r["RowVersion"]);
            i.CreatedAt = r["CreatedAt"].ToString();
            return i;
        }

        private static InvDocument MapDocument(DataRow r)
        {
            if (r == null) return null;
            InvDocument d = new InvDocument();
            d.DocumentId = Convert.ToInt64(r["DocumentID"]);
            d.CompanyId = Convert.ToInt32(r["CompanyID"]);
            d.CenterId = Convert.ToInt32(r["CenterID"]);
            d.DocNo = r["DocNo"].ToString();
            d.DocumentType = r["DocumentType"].ToString();
            d.Status = r["Status"].ToString();
            d.PostingDate = r["PostingDate"].ToString();
            d.WarehouseId = Convert.ToInt64(r["WarehouseID"]);
            d.ToLocationId = r["ToLocationID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["ToLocationID"]);
            d.Description = r["Description"] == DBNull.Value ? null : r["Description"].ToString();
            d.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return d;
        }

        private static IList<InvDocumentLine> MapLines(DataTable table)
        {
            List<InvDocumentLine> list = new List<InvDocumentLine>();
            foreach (DataRow r in table.Rows)
            {
                InvDocumentLine l = new InvDocumentLine();
                l.LineId = Convert.ToInt64(r["LineID"]);
                l.DocumentId = Convert.ToInt64(r["DocumentID"]);
                l.LineNo = Convert.ToInt32(r["LineNo"]);
                l.ItemId = Convert.ToInt64(r["ItemID"]);
                l.LocationId = Convert.ToInt64(r["LocationID"]);
                l.QtyDoc = Convert.ToInt64(r["QtyDoc"]);
                l.QtyBase = Convert.ToInt64(r["QtyBase"]);
                l.UnitCostMinor = Convert.ToInt64(r["UnitCostMinor"]);
                l.ValueMinor = Convert.ToInt64(r["ValueMinor"]);
                list.Add(l);
            }
            return list;
        }

        private static IList<InvItemLedger> MapLedgers(DataTable table)
        {
            List<InvItemLedger> list = new List<InvItemLedger>();
            foreach (DataRow r in table.Rows)
            {
                InvItemLedger l = new InvItemLedger();
                l.ItemLedgerId = Convert.ToInt64(r["ItemLedgerID"]);
                l.CompanyId = Convert.ToInt32(r["CompanyID"]);
                l.CenterId = Convert.ToInt32(r["CenterID"]);
                l.ItemId = Convert.ToInt64(r["ItemID"]);
                l.WarehouseId = Convert.ToInt64(r["WarehouseID"]);
                l.LocationId = Convert.ToInt64(r["LocationID"]);
                l.DocumentType = r["DocumentType"].ToString();
                l.DocumentId = Convert.ToInt64(r["DocumentID"]);
                l.DocumentLineNo = Convert.ToInt32(r["DocumentLineNo"]);
                l.MovementType = r["MovementType"].ToString();
                l.QtyBase = Convert.ToInt64(r["QtyBase"]);
                l.UnitCostMinor = Convert.ToInt64(r["UnitCostMinor"]);
                l.ValueMinor = Convert.ToInt64(r["ValueMinor"]);
                l.PostingDate = r["PostingDate"].ToString();
                list.Add(l);
            }
            return list;
        }

        private static InvItemBalance MapBalance(DataRow r)
        {
            if (r == null) return null;
            InvItemBalance b = new InvItemBalance();
            b.BalanceId = Convert.ToInt64(r["BalanceID"]);
            b.CompanyId = Convert.ToInt32(r["CompanyID"]);
            b.ItemId = Convert.ToInt64(r["ItemID"]);
            b.WarehouseId = Convert.ToInt64(r["WarehouseID"]);
            b.LocationId = Convert.ToInt64(r["LocationID"]);
            b.QuantityOnHand = Convert.ToInt64(r["QuantityOnHand"]);
            b.InventoryValueMinor = Convert.ToInt64(r["InventoryValueMinor"]);
            b.AverageCostMinor = Convert.ToInt64(r["AverageCostMinor"]);
            b.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return b;
        }
    }
}

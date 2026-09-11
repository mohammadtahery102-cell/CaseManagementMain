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

        public IList<InvItemLedger> ListOriginalLedgerForDocument(SQLiteConnection con, SQLiteTransaction tr, long documentId)
        {
            return MapLedgers(Query(con, tr,
                "SELECT * FROM InvItemLedger WHERE DocumentID = @id AND ReversesLedgerID IS NULL ORDER BY ItemLedgerID;",
                P("@id", documentId)));
        }

        public IList<InvItemLedger> ListOriginalLedgerForDocument(long documentId)
        {
            return MapLedgers(Query(
                "SELECT * FROM InvItemLedger WHERE DocumentID = @id AND ReversesLedgerID IS NULL ORDER BY ItemLedgerID;",
                P("@id", documentId)));
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
            try
            {
                _db.ExecuteNonQuery(@"
INSERT INTO InvItem (CompanyID, CenterID, Code, Name, Barcode, CategoryID, BaseUomID, CostingMethod, IsStockable,
  MinQtyBase, MaxQtyBase, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 0, @code, @name, @bc, @cat, @uom, @cm, @stk, @min, @max, @act, 0, 1, @now, @now, @by, @by);",
                    P("@c", item.CompanyId), P("@code", item.Code), P("@name", item.Name), P("@bc", BlankToNull(item.Barcode)),
                    P("@cat", item.CategoryId), P("@uom", item.BaseUomId), P("@cm", item.CostingMethod),
                    P("@stk", item.IsStockable ? 1 : 0), P("@min", item.MinQtyBase), P("@max", item.MaxQtyBase),
                    P("@act", item.IsActive ? 1 : 0), P("@now", item.CreatedAt), P("@by", item.CreatedBy));
            }
            catch (SQLiteException)
            {
                return 0;
            }
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

        public IList<InvWarehouse> ListWarehouses(int companyId, int centerFilter)
        {
            DataTable table = Query(@"
SELECT * FROM InvWarehouse WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr) ORDER BY Code;",
                P("@c", companyId), P("@ctr", centerFilter));
            List<InvWarehouse> list = new List<InvWarehouse>();
            foreach (DataRow r in table.Rows) list.Add(MapWarehouse(r));
            return list;
        }

        public IList<InvLocation> ListLocations(long warehouseId)
        {
            DataTable table = Query(@"
SELECT * FROM InvLocation WHERE WarehouseID = @w AND IsDeleted = 0 AND IsActive = 1 AND IsLeaf = 1 ORDER BY Code;",
                P("@w", warehouseId));
            List<InvLocation> list = new List<InvLocation>();
            foreach (DataRow r in table.Rows) list.Add(MapLocation(r));
            return list;
        }

        public long InsertLocation(long warehouseId, int companyId, string code, string name, string now, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO InvLocation (WarehouseID, CompanyID, Code, Name, Level, IsLeaf, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@w, @c, @code, @name, 1, 1, 1, 0, 1, @n, @n, @u, @u);",
                P("@w", warehouseId), P("@c", companyId), P("@code", code), P("@name", name),
                P("@n", now), P("@u", user ?? ""));
            object id = _db.ExecuteScalar(
                "SELECT LocationID FROM InvLocation WHERE WarehouseID = @w AND Code = @code AND IsDeleted = 0;",
                P("@w", warehouseId), P("@code", code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
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
INSERT INTO InvDocument (CompanyID, CenterID, DocNo, DocumentType, Status, PostingDate, WarehouseID, ToWarehouseID, ToLocationID,
  CostCenterID, ProjectID, PartyID, SourceModule, SourceDocumentType, SourceDocumentID, GroupId, Description,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @no, @ty, @st, @dt, @wh, @towh, @to, @cc, @pr, @pty, @sm, @sdt, @sid, @g, @desc,
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

        public bool UpdateDraftHeader(SQLiteConnection con, SQLiteTransaction tr, InvDocument d, long expectedRv)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
UPDATE InvDocument SET PostingDate = @dt, Description = @desc, WarehouseID = @wh,
  ToLocationID = @to, ToWarehouseID = @towh, UpdatedAt = @ua, UpdatedBy = @ub, RowVersion = RowVersion + 1
WHERE DocumentID = @id AND Status = @st AND RowVersion = @rv;", con, tr))
            {
                cmd.Parameters.AddWithValue("@dt", d.PostingDate);
                cmd.Parameters.AddWithValue("@desc", (object)d.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@wh", d.WarehouseId);
                cmd.Parameters.AddWithValue("@to", (object)d.ToLocationId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@towh", (object)d.ToWarehouseId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ua", d.UpdatedAt);
                cmd.Parameters.AddWithValue("@ub", d.UpdatedBy);
                cmd.Parameters.AddWithValue("@id", d.DocumentId);
                cmd.Parameters.AddWithValue("@st", InventoryCodes.StatusDraft);
                cmd.Parameters.AddWithValue("@rv", expectedRv);
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public void DeleteLines(SQLiteConnection con, SQLiteTransaction tr, long documentId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "DELETE FROM InvDocumentLine WHERE DocumentID = @d;", con, tr))
            {
                cmd.Parameters.AddWithValue("@d", documentId);
                cmd.ExecuteNonQuery();
            }
        }

        public long GetLocationOnHand(SQLiteConnection con, SQLiteTransaction tr,
            int companyId, long itemId, long warehouseId, long locationId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT COALESCE(SUM(QuantityOnHand),0) FROM InvItemBalance
WHERE CompanyID = @c AND ItemID = @i AND WarehouseID = @w AND LocationID = @l;", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@i", itemId);
                cmd.Parameters.AddWithValue("@w", warehouseId);
                cmd.Parameters.AddWithValue("@l", locationId);
                object v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
            }
        }

        public long InsertLedger(SQLiteConnection con, SQLiteTransaction tr, InvItemLedger row)
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
                cmd.Parameters.AddWithValue("@rev", row.ReversesLedgerId.HasValue && row.ReversesLedgerId.Value > 0
                    ? (object)row.ReversesLedgerId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@now", row.PostingDate);
                cmd.Parameters.AddWithValue("@by", row.CreatedBy ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
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
ORDER BY PostingDate, ItemLedgerID
LIMIT 2000;",
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

        public IList<InvDocument> ListDocuments(int companyId, int centerFilter)
        {
            return ListDocuments(companyId, centerFilter, null, null, 400);
        }

        public IList<InvDocument> ListDocuments(int companyId, int centerFilter, string typeFilter, string search, int limit)
        {
            if (limit <= 0 || limit > 2000) limit = 400;
            DataTable table = Query(@"
SELECT * FROM InvDocument
WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr)
  AND (@ty = '' OR DocumentType = @ty)
  AND (@q = '' OR DocNo LIKE @like OR Description LIKE @like OR DocumentType LIKE @like OR Status LIKE @like)
ORDER BY DocumentID DESC LIMIT " + limit + ";",
                P("@c", companyId), P("@ctr", centerFilter),
                P("@ty", typeFilter ?? ""), P("@q", search ?? ""), P("@like", "%" + (search ?? "") + "%"));
            List<InvDocument> list = new List<InvDocument>();
            foreach (DataRow r in table.Rows) list.Add(MapDocument(r));
            return list;
        }

        public IList<InvItem> ListItems(int companyId)
        {
            return ListItems(companyId, null, 0, 0);
        }

        public IList<InvItem> ListItems(int companyId, string search, bool includeInactive)
        {
            return ListItems(companyId, search, 0, includeInactive ? 0 : 1);
        }

        public IList<InvItem> ListItems(int companyId, string search, long categoryId, int activeMode)
        {
            string like = "%" + SanitizeLike(search) + "%";
            string catClause = "";
            if (categoryId > 0)
            {
                IList<long> tree = ListCategoryTreeIds(companyId, categoryId);
                if (tree.Count == 0) return new List<InvItem>();
                catClause = " AND i.CategoryID IN (" + string.Join(",", tree) + ")";
            }
            DataTable table = Query(@"
SELECT i.*, c.Name AS CategoryName, u.Name AS UomName
FROM InvItem i
LEFT JOIN InvItemCategory c ON c.CategoryID = i.CategoryID
LEFT JOIN InvUnitOfMeasure u ON u.UomID = i.BaseUomID
WHERE i.CompanyID = @c AND i.IsDeleted = 0
  AND (@act = 0 OR (@act = 1 AND i.IsActive = 1) OR (@act = -1 AND i.IsActive = 0))
  AND (@q = '' OR i.Code LIKE @like OR i.Name LIKE @like OR IFNULL(i.Barcode,'') LIKE @like)" + catClause + @"
ORDER BY i.Code
LIMIT 1000;",
                P("@c", companyId), P("@act", activeMode),
                P("@q", search ?? ""), P("@like", like));
            List<InvItem> list = new List<InvItem>();
            foreach (DataRow r in table.Rows) list.Add(MapItem(r));
            return list;
        }

        public InvItem GetItemByBarcode(int companyId, string barcode)
        {
            return GetItemByBarcode(companyId, barcode, 0);
        }

        public InvItem GetItemByBarcode(int companyId, string barcode, long exceptItemId)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            return MapItem(QueryRow(
                "SELECT * FROM InvItem WHERE CompanyID = @c AND Barcode = @b AND IsDeleted = 0 AND ItemID <> @id;",
                P("@c", companyId), P("@b", barcode.Trim()), P("@id", exceptItemId)));
        }

        public InvItem GetItemByCode(int companyId, string code, long exceptItemId)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            return MapItem(QueryRow(
                "SELECT * FROM InvItem WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0 AND ItemID <> @id;",
                P("@c", companyId), P("@code", code.Trim()), P("@id", exceptItemId)));
        }

        public InvItem GetItemByName(int companyId, string name, long exceptItemId)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return MapItem(QueryRow(
                "SELECT * FROM InvItem WHERE CompanyID = @c AND Name = @n AND IsDeleted = 0 AND ItemID <> @id;",
                P("@c", companyId), P("@n", name.Trim()), P("@id", exceptItemId)));
        }

        public bool UpdateItem(InvItem item, string now, string user)
        {
            try
            {
                int n = _db.ExecuteNonQuery(@"
UPDATE InvItem SET Code = @code, Name = @name, Barcode = @bc, CategoryID = @cat, BaseUomID = @uom,
  IsStockable = @stk, MinQtyBase = @min, MaxQtyBase = @max, IsActive = @act,
  UpdatedAt = @now, UpdatedBy = @by, RowVersion = RowVersion + 1
WHERE ItemID = @id AND CompanyID = @c AND IsDeleted = 0 AND RowVersion = @rv;",
                    P("@code", item.Code), P("@name", item.Name), P("@bc", BlankToNull(item.Barcode)),
                    P("@cat", item.CategoryId), P("@uom", item.BaseUomId), P("@stk", item.IsStockable ? 1 : 0),
                    P("@min", item.MinQtyBase), P("@max", item.MaxQtyBase), P("@act", item.IsActive ? 1 : 0),
                    P("@now", now), P("@by", user ?? ""), P("@id", item.ItemId),
                    P("@c", item.CompanyId), P("@rv", item.RowVersion));
                return n == 1;
            }
            catch (SQLiteException)
            {
                return false;
            }
        }

        public bool SetItemActive(long itemId, int companyId, bool active, string now, string user)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE InvItem SET IsActive = @a, UpdatedAt = @now, UpdatedBy = @by, RowVersion = RowVersion + 1
WHERE ItemID = @id AND CompanyID = @c AND IsDeleted = 0;",
                P("@a", active ? 1 : 0), P("@now", now), P("@by", user ?? ""), P("@id", itemId), P("@c", companyId));
            return n == 1;
        }

        public long ItemOnHand(int companyId, long itemId)
        {
            object v = _db.ExecuteScalar(
                "SELECT COALESCE(SUM(QuantityOnHand),0) FROM InvItemBalance WHERE CompanyID = @c AND ItemID = @i;",
                P("@c", companyId), P("@i", itemId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public bool SoftDeleteItem(long itemId, int companyId, string now, string user)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE InvItem SET IsDeleted = 1, IsActive = 0, DeletedAt = @now, DeletedBy = @by,
  UpdatedAt = @now, UpdatedBy = @by, RowVersion = RowVersion + 1
WHERE ItemID = @id AND CompanyID = @c AND IsDeleted = 0;",
                P("@now", now), P("@by", user ?? ""), P("@id", itemId), P("@c", companyId));
            return n == 1;
        }

        public bool ItemHasUsage(long itemId)
        {
            object ledger = _db.ExecuteScalar("SELECT 1 FROM InvItemLedger WHERE ItemID = @id LIMIT 1;", P("@id", itemId));
            if (ledger != null && ledger != DBNull.Value) return true;
            object line = _db.ExecuteScalar("SELECT 1 FROM InvDocumentLine WHERE ItemID = @id LIMIT 1;", P("@id", itemId));
            return line != null && line != DBNull.Value;
        }

        public InvItemCategory GetCategory(long id)
        {
            return MapCategory(QueryRow("SELECT * FROM InvItemCategory WHERE CategoryID = @id AND IsDeleted = 0;", P("@id", id)));
        }

        public InvItemCategory GetCategoryByCode(int companyId, string code, long exceptId)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            return MapCategory(QueryRow(
                "SELECT * FROM InvItemCategory WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0 AND CategoryID <> @id;",
                P("@c", companyId), P("@code", code.Trim()), P("@id", exceptId)));
        }

        public InvItemCategory GetCategoryByName(int companyId, string name, long exceptId)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return MapCategory(QueryRow(
                "SELECT * FROM InvItemCategory WHERE CompanyID = @c AND Name = @n AND IsDeleted = 0 AND CategoryID <> @id;",
                P("@c", companyId), P("@n", name.Trim()), P("@id", exceptId)));
        }

        public IList<InvItemCategory> ListCategories(int companyId)
        {
            DataTable table = Query(
                "SELECT * FROM InvItemCategory WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY Level, Code;",
                P("@c", companyId));
            List<InvItemCategory> list = new List<InvItemCategory>();
            foreach (DataRow r in table.Rows) list.Add(MapCategory(r));
            return list;
        }

        public IList<long> ListCategoryTreeIds(int companyId, long rootId)
        {
            List<long> ids = new List<long>();
            if (rootId <= 0) return ids;
            ids.Add(rootId);
            IList<InvItemCategory> all = ListCategories(companyId);
            bool added = true;
            while (added)
            {
                added = false;
                for (int i = 0; i < all.Count; i++)
                {
                    InvItemCategory c = all[i];
                    if (c.ParentCategoryId > 0 && ids.Contains(c.ParentCategoryId) && !ids.Contains(c.CategoryId))
                    {
                        ids.Add(c.CategoryId);
                        added = true;
                    }
                }
            }
            return ids;
        }

        public long InsertCategory(InvItemCategory cat, string now, string user)
        {
            try
            {
                _db.ExecuteNonQuery(@"
INSERT INTO InvItemCategory (CompanyID, CenterID, Code, Name, ParentCategoryID, Level, IsLeaf, IsActive, IsDeleted,
  RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 0, @code, @name, @p, @lv, 1, @act, 0, 1, @n, @n, @u, @u);",
                    P("@c", cat.CompanyId), P("@code", cat.Code), P("@name", cat.Name),
                    P("@p", cat.ParentCategoryId > 0 ? (object)cat.ParentCategoryId : DBNull.Value),
                    P("@lv", cat.Level), P("@act", cat.IsActive ? 1 : 0), P("@n", now), P("@u", user ?? ""));
            }
            catch (SQLiteException)
            {
                return 0;
            }
            object id = _db.ExecuteScalar(
                "SELECT CategoryID FROM InvItemCategory WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", cat.CompanyId), P("@code", cat.Code));
            long catId = id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
            if (catId > 0 && cat.ParentCategoryId > 0)
                SetCategoryLeaf(cat.ParentCategoryId);
            return catId;
        }

        public bool UpdateCategory(InvItemCategory cat, string now, string user)
        {
            InvItemCategory previous = GetCategory(cat.CategoryId);
            try
            {
                int n = _db.ExecuteNonQuery(@"
UPDATE InvItemCategory SET Code = @code, Name = @name, ParentCategoryID = @p, Level = @lv, IsActive = @act,
  UpdatedAt = @now, UpdatedBy = @by, RowVersion = RowVersion + 1
WHERE CategoryID = @id AND CompanyID = @c AND IsDeleted = 0 AND RowVersion = @rv;",
                    P("@code", cat.Code), P("@name", cat.Name),
                    P("@p", cat.ParentCategoryId > 0 ? (object)cat.ParentCategoryId : DBNull.Value),
                    P("@lv", cat.Level), P("@act", cat.IsActive ? 1 : 0),
                    P("@now", now), P("@by", user ?? ""), P("@id", cat.CategoryId),
                    P("@c", cat.CompanyId), P("@rv", cat.RowVersion));
                if (n != 1) return false;
            }
            catch (SQLiteException)
            {
                return false;
            }
            if (previous != null && previous.ParentCategoryId != cat.ParentCategoryId)
            {
                if (previous.ParentCategoryId > 0) SetCategoryLeaf(previous.ParentCategoryId);
                if (cat.ParentCategoryId > 0) SetCategoryLeaf(cat.ParentCategoryId);
            }
            RecalcCategoryLevels(cat.CategoryId, cat.Level);
            return true;
        }

        public bool SoftDeleteCategory(long categoryId, int companyId, string now, string user)
        {
            InvItemCategory previous = GetCategory(categoryId);
            int n = _db.ExecuteNonQuery(@"
UPDATE InvItemCategory SET IsDeleted = 1, IsActive = 0, DeletedAt = @now, DeletedBy = @by,
  UpdatedAt = @now, UpdatedBy = @by, RowVersion = RowVersion + 1
WHERE CategoryID = @id AND CompanyID = @c AND IsDeleted = 0;",
                P("@now", now), P("@by", user ?? ""), P("@id", categoryId), P("@c", companyId));
            if (n == 1 && previous != null && previous.ParentCategoryId > 0)
                SetCategoryLeaf(previous.ParentCategoryId);
            return n == 1;
        }

        public bool CategoryHasChildren(long categoryId)
        {
            object v = _db.ExecuteScalar(
                "SELECT 1 FROM InvItemCategory WHERE ParentCategoryID = @id AND IsDeleted = 0 LIMIT 1;",
                P("@id", categoryId));
            return v != null && v != DBNull.Value;
        }

        public bool CategoryHasItems(long categoryId)
        {
            object v = _db.ExecuteScalar(
                "SELECT 1 FROM InvItem WHERE CategoryID = @id AND IsDeleted = 0 LIMIT 1;",
                P("@id", categoryId));
            return v != null && v != DBNull.Value;
        }

        public InvUnitOfMeasure GetUom(long id)
        {
            return MapUom(QueryRow("SELECT * FROM InvUnitOfMeasure WHERE UomID = @id AND IsDeleted = 0;", P("@id", id)));
        }

        public InvUnitOfMeasure GetUomByCode(int companyId, string code, long exceptId)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            return MapUom(QueryRow(
                "SELECT * FROM InvUnitOfMeasure WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0 AND UomID <> @id;",
                P("@c", companyId), P("@code", code.Trim()), P("@id", exceptId)));
        }

        public InvUnitOfMeasure GetUomByName(int companyId, string name, long exceptId)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return MapUom(QueryRow(
                "SELECT * FROM InvUnitOfMeasure WHERE CompanyID = @c AND Name = @n AND IsDeleted = 0 AND UomID <> @id;",
                P("@c", companyId), P("@n", name.Trim()), P("@id", exceptId)));
        }

        public IList<InvUnitOfMeasure> ListUoms(int companyId)
        {
            DataTable table = Query(
                "SELECT * FROM InvUnitOfMeasure WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY Code;",
                P("@c", companyId));
            List<InvUnitOfMeasure> list = new List<InvUnitOfMeasure>();
            foreach (DataRow r in table.Rows) list.Add(MapUom(r));
            return list;
        }

        public long InsertUom(InvUnitOfMeasure uom, string now, string user)
        {
            try
            {
                _db.ExecuteNonQuery(@"
INSERT INTO InvUnitOfMeasure (CompanyID, CenterID, Code, Name, DecimalPlaces, IsActive, IsDeleted, RowVersion,
  CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 0, @code, @name, @dp, @act, 0, 1, @n, @n, @u, @u);",
                    P("@c", uom.CompanyId), P("@code", uom.Code), P("@name", uom.Name),
                    P("@dp", uom.DecimalPlaces), P("@act", uom.IsActive ? 1 : 0), P("@n", now), P("@u", user ?? ""));
            }
            catch (SQLiteException)
            {
                return 0;
            }
            object id = _db.ExecuteScalar(
                "SELECT UomID FROM InvUnitOfMeasure WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", uom.CompanyId), P("@code", uom.Code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        public bool UpdateUom(InvUnitOfMeasure uom, string now, string user)
        {
            try
            {
                int n = _db.ExecuteNonQuery(@"
UPDATE InvUnitOfMeasure SET Code = @code, Name = @name, DecimalPlaces = @dp, IsActive = @act,
  UpdatedAt = @now, UpdatedBy = @by, RowVersion = RowVersion + 1
WHERE UomID = @id AND CompanyID = @c AND IsDeleted = 0 AND RowVersion = @rv;",
                    P("@code", uom.Code), P("@name", uom.Name), P("@dp", uom.DecimalPlaces),
                    P("@act", uom.IsActive ? 1 : 0), P("@now", now), P("@by", user ?? ""),
                    P("@id", uom.UomId), P("@c", uom.CompanyId), P("@rv", uom.RowVersion));
                return n == 1;
            }
            catch (SQLiteException)
            {
                return false;
            }
        }

        public bool SoftDeleteUom(long uomId, int companyId, string now, string user)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE InvUnitOfMeasure SET IsDeleted = 1, IsActive = 0, DeletedAt = @now, DeletedBy = @by,
  UpdatedAt = @now, UpdatedBy = @by, RowVersion = RowVersion + 1
WHERE UomID = @id AND CompanyID = @c AND IsDeleted = 0;",
                P("@now", now), P("@by", user ?? ""), P("@id", uomId), P("@c", companyId));
            return n == 1;
        }

        public bool UomHasItems(long uomId)
        {
            object v = _db.ExecuteScalar(
                "SELECT 1 FROM InvItem WHERE BaseUomID = @id AND IsDeleted = 0 LIMIT 1;",
                P("@id", uomId));
            return v != null && v != DBNull.Value;
        }

        private void SetCategoryLeaf(long categoryId)
        {
            _db.ExecuteNonQuery(@"
UPDATE InvItemCategory SET IsLeaf = CASE
  WHEN EXISTS (SELECT 1 FROM InvItemCategory c WHERE c.ParentCategoryID = @id AND c.IsDeleted = 0) THEN 0 ELSE 1 END
WHERE CategoryID = @id;",
                P("@id", categoryId));
        }

        private void RecalcCategoryLevels(long categoryId, int level)
        {
            _db.ExecuteNonQuery("UPDATE InvItemCategory SET Level = @lv WHERE CategoryID = @id;",
                P("@lv", level), P("@id", categoryId));
            DataTable children = Query(
                "SELECT CategoryID FROM InvItemCategory WHERE ParentCategoryID = @id AND IsDeleted = 0;",
                P("@id", categoryId));
            foreach (DataRow r in children.Rows)
                RecalcCategoryLevels(Convert.ToInt64(r["CategoryID"]), level + 1);
        }

        public long InsertWarehouse(InvWarehouse w, string now, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO InvWarehouse (CompanyID, CenterID, Code, Name, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @code, @name, 1, 0, 1, @n, @n, @u, @u);",
                P("@c", w.CompanyId), P("@ctr", w.CenterId), P("@code", w.Code), P("@name", w.Name),
                P("@n", now), P("@u", user ?? ""));
            object id = _db.ExecuteScalar(
                "SELECT WarehouseID FROM InvWarehouse WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", w.CompanyId), P("@code", w.Code));
            long wh = id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
            if (wh > 0)
            {
                _db.ExecuteNonQuery(@"
INSERT INTO InvLocation (WarehouseID, CompanyID, Code, Name, Level, IsLeaf, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@w, @c, 'DEFAULT', 'پیش‌فرض', 1, 1, 1, 0, 1, @n, @n, @u, @u);",
                    P("@w", wh), P("@c", w.CompanyId), P("@n", now), P("@u", user ?? ""));
            }
            return wh;
        }

        public bool SetWarehouseActive(long warehouseId, int companyId, bool active, string now, string user)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE InvWarehouse SET IsActive = @a, UpdatedAt = @now, UpdatedBy = @by, RowVersion = RowVersion + 1
WHERE WarehouseID = @id AND CompanyID = @c AND IsDeleted = 0;",
                P("@a", active ? 1 : 0), P("@now", now), P("@by", user ?? ""), P("@id", warehouseId), P("@c", companyId));
            return n == 1;
        }

        public long WarehouseOnHand(int companyId, long warehouseId)
        {
            object v = _db.ExecuteScalar(
                "SELECT COALESCE(SUM(QuantityOnHand),0) FROM InvItemBalance WHERE CompanyID = @c AND WarehouseID = @w;",
                P("@c", companyId), P("@w", warehouseId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public bool HasLedgerForDocument(SQLiteConnection con, SQLiteTransaction tr, long documentId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT 1 FROM InvItemLedger WHERE DocumentID = @id LIMIT 1;", con, tr))
            {
                cmd.Parameters.AddWithValue("@id", documentId);
                object v = cmd.ExecuteScalar();
                return v != null && v != DBNull.Value;
            }
        }

        public IList<InventoryIntegrityRow> ListIntegrity(int companyId, int centerFilter)
        {
            DataTable table = Query(@"
SELECT i.ItemID, i.Code, i.Name,
  COALESCE(SUM(CASE WHEN l.DocumentType IN ('Opening','Receipt') THEN l.QtyBase ELSE 0 END),0) AS OpeningReceipt,
  COALESCE(SUM(CASE WHEN l.DocumentType = 'Opening' THEN l.QtyBase ELSE 0 END),0) AS OpeningQty,
  COALESCE(SUM(CASE WHEN l.DocumentType = 'Receipt' THEN l.QtyBase ELSE 0 END),0) AS ReceiptQty,
  COALESCE(SUM(CASE WHEN l.DocumentType = 'Issue' THEN -l.QtyBase ELSE 0 END),0) AS IssueQty,
  COALESCE(SUM(CASE WHEN l.DocumentType IN ('Adjustment','Revalue') THEN l.QtyBase ELSE 0 END),0) AS AdjQty,
  COALESCE(SUM(CASE WHEN l.DocumentType = 'Count' THEN l.QtyBase ELSE 0 END),0) AS CountQty,
  COALESCE(SUM(CASE WHEN l.DocumentType = 'Transfer' AND l.MovementType = 'In' THEN l.QtyBase ELSE 0 END),0) AS Tin,
  COALESCE(SUM(CASE WHEN l.DocumentType = 'Transfer' AND l.MovementType = 'Out' THEN -l.QtyBase ELSE 0 END),0) AS Tout,
  COALESCE(SUM(l.QtyBase),0) AS LedgerQty,
  COALESCE((SELECT SUM(b.QuantityOnHand) FROM InvItemBalance b
            JOIN InvWarehouse w2 ON w2.WarehouseID = b.WarehouseID
            WHERE b.ItemID = i.ItemID AND b.CompanyID = i.CompanyID AND (@ctr = 0 OR w2.CenterID = @ctr)),0) AS BalQty
FROM InvItem i
LEFT JOIN InvItemLedger l ON l.ItemID = i.ItemID AND l.CompanyID = i.CompanyID
LEFT JOIN InvWarehouse w ON w.WarehouseID = l.WarehouseID
WHERE i.CompanyID = @c AND i.IsDeleted = 0 AND (@ctr = 0 OR w.CenterID IS NULL OR w.CenterID = @ctr)
GROUP BY i.ItemID, i.Code, i.Name
HAVING COALESCE(SUM(l.QtyBase),0) <> 0 OR COALESCE((SELECT SUM(b.QuantityOnHand) FROM InvItemBalance b
            JOIN InvWarehouse w2 ON w2.WarehouseID = b.WarehouseID
            WHERE b.ItemID = i.ItemID AND b.CompanyID = i.CompanyID AND (@ctr = 0 OR w2.CenterID = @ctr)),0) <> 0
ORDER BY i.Code;",
                P("@c", companyId), P("@ctr", centerFilter));
            List<InventoryIntegrityRow> list = new List<InventoryIntegrityRow>();
            foreach (DataRow r in table.Rows)
            {
                InventoryIntegrityRow row = new InventoryIntegrityRow();
                row.ItemId = Convert.ToInt64(r["ItemID"]);
                row.ItemCode = r["Code"].ToString();
                row.ItemName = r["Name"].ToString();
                row.OpeningQty = Convert.ToInt64(r["OpeningQty"]);
                row.ReceiptQty = Convert.ToInt64(r["ReceiptQty"]);
                row.IssueQty = Convert.ToInt64(r["IssueQty"]);
                row.AdjustmentQty = Convert.ToInt64(r["AdjQty"]);
                row.CountQty = Convert.ToInt64(r["CountQty"]);
                row.TransferInQty = Convert.ToInt64(r["Tin"]);
                row.TransferOutQty = Convert.ToInt64(r["Tout"]);
                row.LedgerQty = Convert.ToInt64(r["LedgerQty"]);
                row.BalanceQty = Convert.ToInt64(r["BalQty"]);
                row.ComputedQty = row.OpeningQty + row.ReceiptQty - row.IssueQty + row.AdjustmentQty + row.CountQty
                    + row.TransferInQty - row.TransferOutQty;
                row.Balanced = row.ComputedQty == row.BalanceQty && row.LedgerQty == row.BalanceQty;
                list.Add(row);
            }
            return list;
        }

        public IList<InventoryVelocityRow> ListVelocity(int companyId, int centerFilter, bool fast)
        {
            string dir = fast ? "DESC" : "ASC";
            string sql = @"
SELECT i.Code, i.Name,
  COALESCE((SELECT SUM(b.QuantityOnHand) FROM InvItemBalance b
            JOIN InvWarehouse w ON w.WarehouseID = b.WarehouseID
            WHERE b.ItemID = i.ItemID AND b.CompanyID = i.CompanyID AND (@ctr = 0 OR w.CenterID = @ctr)),0) AS OnHand,
  COALESCE(SUM(CASE WHEN l.DocumentType = 'Issue' THEN -l.QtyBase ELSE 0 END),0) AS Issued,
  COALESCE(SUM(CASE WHEN l.DocumentType IN ('Receipt','Opening') THEN l.QtyBase ELSE 0 END),0) AS Received
FROM InvItem i
LEFT JOIN InvItemLedger l ON l.ItemID = i.ItemID AND l.CompanyID = i.CompanyID
WHERE i.CompanyID = @c AND i.IsDeleted = 0
GROUP BY i.ItemID, i.Code, i.Name
ORDER BY Issued " + dir + " , OnHand DESC LIMIT 200";
            DataTable table = Query(sql, P("@c", companyId), P("@ctr", centerFilter));
            List<InventoryVelocityRow> list = new List<InventoryVelocityRow>();
            foreach (DataRow r in table.Rows)
            {
                InventoryVelocityRow row = new InventoryVelocityRow();
                row.ItemCode = r["Code"].ToString();
                row.ItemName = r["Name"].ToString();
                row.QuantityOnHand = Convert.ToInt64(r["OnHand"]);
                row.IssuedQty = Convert.ToInt64(r["Issued"]);
                row.ReceiptQty = Convert.ToInt64(r["Received"]);
                if (!fast && row.QuantityOnHand <= 0 && row.IssuedQty > 0) continue;
                list.Add(row);
            }
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
            cmd.Parameters.AddWithValue("@towh", (object)d.ToWarehouseId ?? DBNull.Value);
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

        private static object BlankToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private static string SanitizeLike(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return value.Trim().Replace("%", "").Replace("_", "");
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
            i.Barcode = r.Table.Columns.Contains("Barcode") && r["Barcode"] != DBNull.Value ? r["Barcode"].ToString() : null;
            i.CategoryId = Convert.ToInt64(r["CategoryID"]);
            i.BaseUomId = Convert.ToInt64(r["BaseUomID"]);
            i.CostingMethod = r["CostingMethod"].ToString();
            i.IsStockable = Convert.ToInt32(r["IsStockable"]) != 0;
            i.MinQtyBase = Convert.ToInt64(r["MinQtyBase"]);
            i.MaxQtyBase = Convert.ToInt64(r["MaxQtyBase"]);
            i.IsActive = Convert.ToInt32(r["IsActive"]) != 0;
            i.RowVersion = Convert.ToInt64(r["RowVersion"]);
            i.CreatedAt = r["CreatedAt"].ToString();
            if (r.Table.Columns.Contains("CategoryName") && r["CategoryName"] != DBNull.Value)
                i.CategoryName = r["CategoryName"].ToString();
            if (r.Table.Columns.Contains("UomName") && r["UomName"] != DBNull.Value)
                i.UomName = r["UomName"].ToString();
            return i;
        }

        private static InvItemCategory MapCategory(DataRow r)
        {
            if (r == null) return null;
            InvItemCategory c = new InvItemCategory();
            c.CategoryId = Convert.ToInt64(r["CategoryID"]);
            c.CompanyId = Convert.ToInt32(r["CompanyID"]);
            c.Code = r["Code"].ToString();
            c.Name = r["Name"].ToString();
            c.ParentCategoryId = r["ParentCategoryID"] == DBNull.Value ? 0 : Convert.ToInt64(r["ParentCategoryID"]);
            c.Level = Convert.ToInt32(r["Level"]);
            c.IsLeaf = Convert.ToInt32(r["IsLeaf"]) != 0;
            c.IsActive = Convert.ToInt32(r["IsActive"]) != 0;
            c.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return c;
        }

        private static InvUnitOfMeasure MapUom(DataRow r)
        {
            if (r == null) return null;
            InvUnitOfMeasure u = new InvUnitOfMeasure();
            u.UomId = Convert.ToInt64(r["UomID"]);
            u.CompanyId = Convert.ToInt32(r["CompanyID"]);
            u.Code = r["Code"].ToString();
            u.Name = r["Name"].ToString();
            u.DecimalPlaces = Convert.ToInt32(r["DecimalPlaces"]);
            u.IsActive = Convert.ToInt32(r["IsActive"]) != 0;
            u.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return u;
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
            d.ToWarehouseId = r.Table.Columns.Contains("ToWarehouseID") && r["ToWarehouseID"] != DBNull.Value
                ? Convert.ToInt64(r["ToWarehouseID"]) : (long?)null;
            d.ToLocationId = r["ToLocationID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["ToLocationID"]);
            d.Description = r["Description"] == DBNull.Value ? null : r["Description"].ToString();
            d.SourceModule = r["SourceModule"] == DBNull.Value ? null : r["SourceModule"].ToString();
            d.SourceDocumentType = r["SourceDocumentType"] == DBNull.Value ? null : r["SourceDocumentType"].ToString();
            d.SourceDocumentId = r["SourceDocumentID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["SourceDocumentID"]);
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
                l.ReversesLedgerId = r.Table.Columns.Contains("ReversesLedgerID") && r["ReversesLedgerID"] != DBNull.Value
                    ? Convert.ToInt64(r["ReversesLedgerID"]) : (long?)null;
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

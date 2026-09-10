using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.Assets.Domain;
using CaseManagement.DAL;

namespace CaseManagement.Assets.Infrastructure
{
    public class AssetStore
    {
        private readonly DatabaseHelper _db;
        public AssetStore() : this(new DatabaseHelper()) { }
        public AssetStore(DatabaseHelper db) { _db = db; }

        public void ExecuteInTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            _db.ExecuteInTransaction(action);
        }

        public bool RequiresApproval(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT RequiresApproval FROM FaSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v == null || v == DBNull.Value || Convert.ToInt32(v) != 0;
        }

        public long Account(int companyId, string col)
        {
            object v = _db.ExecuteScalar("SELECT " + col + " FROM FaSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long DefaultId(string table, string idCol, int companyId)
        {
            object v = _db.ExecuteScalar("SELECT " + idCol + " FROM " + table + " WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY " + idCol + " LIMIT 1;", P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public string AllocateNo(SQLiteConnection con, SQLiteTransaction tr, int companyId, string kind)
        {
            long next = 1;
            string prefix = "FA";
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT NextDocNumber, NumberPrefix FROM FaSetting WHERE CompanyID = @c AND IsDeleted = 0;", con, tr))
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
            using (SQLiteCommand upd = new SQLiteCommand("UPDATE FaSetting SET NextDocNumber = NextDocNumber + 1 WHERE CompanyID = @c;", con, tr))
            {
                upd.Parameters.AddWithValue("@c", companyId);
                upd.ExecuteNonQuery();
            }
            return prefix + "-" + kind + next.ToString();
        }

        public long InsertMaster(string table, int companyId, int centerId, string code, string name, string now, string user)
        {
            _db.ExecuteNonQuery("INSERT INTO " + table + " (CompanyID, CenterID, Code, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) VALUES (@c,@ctr,@code,@name,0,1,@n,@n,@u,@u);",
                P("@c", companyId), P("@ctr", centerId), P("@code", code), P("@name", name), P("@n", now), P("@u", user ?? ""));
            object id = _db.ExecuteScalar("SELECT " + IdCol(table) + " FROM " + table + " WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", companyId), P("@code", code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        private static string IdCol(string table)
        {
            if (table == "FaCategory") return "CategoryID";
            if (table == "FaLocation") return "LocationID";
            return "CustodianID";
        }

        public long InsertAsset(SQLiteConnection con, SQLiteTransaction tr, FaAsset a, string date, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO FaAsset (CompanyID, CenterID, CategoryID, LocationID, CustodianID, Code, Name, CostMinor, AccumDepMinor, UsefulLifeMonths, Status, AcquisitionDate, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c,@ctr,@cat,@loc,@cu,@code,@name,@cost,0,@life,@st,@dt,0,1,@n,@n,@u,@u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", a.CompanyId);
                cmd.Parameters.AddWithValue("@ctr", a.CenterId);
                cmd.Parameters.AddWithValue("@cat", a.CategoryId);
                cmd.Parameters.AddWithValue("@loc", a.LocationId);
                cmd.Parameters.AddWithValue("@cu", a.CustodianId);
                cmd.Parameters.AddWithValue("@code", a.Code);
                cmd.Parameters.AddWithValue("@name", a.Name);
                cmd.Parameters.AddWithValue("@cost", a.CostMinor);
                cmd.Parameters.AddWithValue("@life", a.UsefulLifeMonths);
                cmd.Parameters.AddWithValue("@st", AssetCodes.StatusActive);
                cmd.Parameters.AddWithValue("@dt", date);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public FaAsset GetAsset(long id)
        {
            DataTable t = _db.Query("SELECT * FROM FaAsset WHERE AssetID = @id AND IsDeleted = 0;", P("@id", id));
            if (t.Rows.Count == 0) return null;
            DataRow r = t.Rows[0];
            FaAsset a = new FaAsset();
            a.AssetId = Convert.ToInt64(r["AssetID"]);
            a.CompanyId = Convert.ToInt32(r["CompanyID"]);
            a.CenterId = Convert.ToInt32(r["CenterID"]);
            a.CategoryId = Convert.ToInt64(r["CategoryID"]);
            a.LocationId = Convert.ToInt64(r["LocationID"]);
            a.CustodianId = Convert.ToInt64(r["CustodianID"]);
            a.Code = Convert.ToString(r["Code"]);
            a.Name = Convert.ToString(r["Name"]);
            a.CostMinor = Convert.ToInt64(r["CostMinor"]);
            a.AccumDepMinor = Convert.ToInt64(r["AccumDepMinor"]);
            a.UsefulLifeMonths = Convert.ToInt32(r["UsefulLifeMonths"]);
            a.Status = Convert.ToString(r["Status"]);
            a.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return a;
        }

        public IList<FaAsset> ListActive(int companyId)
        {
            List<FaAsset> list = new List<FaAsset>();
            DataTable t = _db.Query("SELECT AssetID FROM FaAsset WHERE CompanyID = @c AND Status = @st AND IsDeleted = 0;",
                P("@c", companyId), P("@st", AssetCodes.StatusActive));
            for (int i = 0; i < t.Rows.Count; i++) list.Add(GetAsset(Convert.ToInt64(t.Rows[i]["AssetID"])));
            return list;
        }

        public void AddAccum(SQLiteConnection con, SQLiteTransaction tr, long assetId, long amount)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "UPDATE FaAsset SET AccumDepMinor = AccumDepMinor + @a, RowVersion = RowVersion + 1 WHERE AssetID = @id AND IsDeleted = 0;", con, tr))
            {
                cmd.Parameters.AddWithValue("@a", amount);
                cmd.Parameters.AddWithValue("@id", assetId);
                cmd.ExecuteNonQuery();
            }
        }

        public void DisposeAsset(SQLiteConnection con, SQLiteTransaction tr, long assetId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "UPDATE FaAsset SET Status = @st, RowVersion = RowVersion + 1 WHERE AssetID = @id;", con, tr))
            {
                cmd.Parameters.AddWithValue("@st", AssetCodes.StatusDisposed);
                cmd.Parameters.AddWithValue("@id", assetId);
                cmd.ExecuteNonQuery();
            }
        }

        public void MoveAsset(long assetId, long loc, long cust, string now, string user)
        {
            _db.ExecuteNonQuery("UPDATE FaAsset SET LocationID = @l, CustodianID = @c, RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u WHERE AssetID = @id;",
                P("@l", loc), P("@c", cust), P("@n", now), P("@u", user ?? ""), P("@id", assetId));
        }

        public long InsertDoc(SQLiteConnection con, SQLiteTransaction tr, string table, string extraCols, string extraVals, int companyId, int centerId, string no, string status, string date, long assetId, string now, string user, params SQLiteParameter[] extra)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO " + table + @" (CompanyID, CenterID, DocNo, Status, DocDate, AssetID" + extraCols + @", IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c,@ctr,@no,@st,@dt,@a" + extraVals + @",0,1,@n,@n,@u,@u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@ctr", centerId);
                cmd.Parameters.AddWithValue("@no", no);
                cmd.Parameters.AddWithValue("@st", status);
                cmd.Parameters.AddWithValue("@dt", date);
                cmd.Parameters.AddWithValue("@a", assetId);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                if (extra != null) cmd.Parameters.AddRange(extra);
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public DataRow GetHeader(string table, string idCol, long id)
        {
            DataTable t = _db.Query("SELECT * FROM " + table + " WHERE " + idCol + " = @id AND IsDeleted = 0;", P("@id", id));
            return t.Rows.Count == 0 ? null : t.Rows[0];
        }

        public bool UpdateStatus(string table, string idCol, long id, string status, long rv, string now, string user)
        {
            return _db.ExecuteNonQuery("UPDATE " + table + " SET Status = @st, RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u WHERE " + idCol + " = @id AND RowVersion = @rv AND IsDeleted = 0;",
                P("@st", status), P("@n", now), P("@u", user ?? ""), P("@id", id), P("@rv", rv)) == 1;
        }

        public void InsertDepLine(SQLiteConnection con, SQLiteTransaction tr, long depId, int companyId, long assetId, long amount)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "INSERT INTO FaDepreciationLine (DepreciationID, CompanyID, AssetID, AmountMinor) VALUES (@d,@c,@a,@m);", con, tr))
            {
                cmd.Parameters.AddWithValue("@d", depId);
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@a", assetId);
                cmd.Parameters.AddWithValue("@m", amount);
                cmd.ExecuteNonQuery();
            }
        }

        public IList<DataRow> DepLines(long depId)
        {
            DataTable t = _db.Query("SELECT * FROM FaDepreciationLine WHERE DepreciationID = @id;", P("@id", depId));
            List<DataRow> list = new List<DataRow>();
            for (int i = 0; i < t.Rows.Count; i++) list.Add(t.Rows[i]);
            return list;
        }

        public IList<AssetRegisterRow> Register(int companyId, int centerId)
        {
            List<AssetRegisterRow> list = new List<AssetRegisterRow>();
            DataTable t = _db.Query("SELECT Code, Name, Status, CostMinor, AccumDepMinor FROM FaAsset WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr);",
                P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                AssetRegisterRow r = new AssetRegisterRow();
                r.Code = Convert.ToString(t.Rows[i]["Code"]);
                r.Name = Convert.ToString(t.Rows[i]["Name"]);
                r.Status = Convert.ToString(t.Rows[i]["Status"]);
                r.CostMinor = Convert.ToInt64(t.Rows[i]["CostMinor"]);
                r.AccumDepMinor = Convert.ToInt64(t.Rows[i]["AccumDepMinor"]);
                r.NbvMinor = r.CostMinor - r.AccumDepMinor;
                list.Add(r);
            }
            return list;
        }

        public IList<DepreciationReportRow> DepReport(int companyId, int centerId)
        {
            List<DepreciationReportRow> list = new List<DepreciationReportRow>();
            DataTable t = _db.Query("SELECT DocNo, DocDate, TotalMinor, Status FROM FaDepreciation WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr);",
                P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                DepreciationReportRow r = new DepreciationReportRow();
                r.DocNo = Convert.ToString(t.Rows[i]["DocNo"]);
                r.DocDate = Convert.ToString(t.Rows[i]["DocDate"]);
                r.TotalMinor = Convert.ToInt64(t.Rows[i]["TotalMinor"]);
                r.Status = Convert.ToString(t.Rows[i]["Status"]);
                list.Add(r);
            }
            return list;
        }

        public IList<AssetMovementRow> Movements(int companyId, int centerId)
        {
            List<AssetMovementRow> list = new List<AssetMovementRow>();
            AddMoves(list, "SELECT a.DocNo, a.DocDate, s.Code FROM FaAcquisition a JOIN FaAsset s ON s.AssetID = a.AssetID WHERE a.CompanyID = @c AND a.IsDeleted = 0 AND (@ctr = 0 OR a.CenterID = @ctr);", "Acquisition", companyId, centerId);
            AddMoves(list, "SELECT a.DocNo, a.DocDate, s.Code FROM FaTransfer a JOIN FaAsset s ON s.AssetID = a.AssetID WHERE a.CompanyID = @c AND a.IsDeleted = 0 AND (@ctr = 0 OR a.CenterID = @ctr);", "Transfer", companyId, centerId);
            AddMoves(list, "SELECT a.DocNo, a.DocDate, s.Code FROM FaDisposal a JOIN FaAsset s ON s.AssetID = a.AssetID WHERE a.CompanyID = @c AND a.IsDeleted = 0 AND (@ctr = 0 OR a.CenterID = @ctr);", "Disposal", companyId, centerId);
            return list;
        }

        private void AddMoves(List<AssetMovementRow> list, string sql, string kind, int companyId, int centerId)
        {
            DataTable t = _db.Query(sql, P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                AssetMovementRow r = new AssetMovementRow();
                r.Kind = kind;
                r.DocNo = Convert.ToString(t.Rows[i]["DocNo"]);
                r.DocDate = Convert.ToString(t.Rows[i]["DocDate"]);
                r.AssetCode = Convert.ToString(t.Rows[i]["Code"]);
                list.Add(r);
            }
        }

        private static SQLiteParameter P(string n, object v) { return new SQLiteParameter(n, v ?? DBNull.Value); }
    }
}

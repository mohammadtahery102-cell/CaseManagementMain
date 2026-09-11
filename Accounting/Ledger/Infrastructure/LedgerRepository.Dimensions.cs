using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;

namespace CaseManagement.Accounting.Ledger.Infrastructure
{
    public partial class LedgerRepository
    {
        public GlCostCenter GetCostCenterByCode(int companyId, string code)
        {
            return MapCostCenter(QueryRow(
                "SELECT * FROM GlCostCenter WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", companyId), P("@code", code)));
        }

        public GlProject GetProjectByCode(int companyId, string code)
        {
            return MapProject(QueryRow(
                "SELECT * FROM GlProject WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", companyId), P("@code", code)));
        }

        public int DimensionLineCount(bool costCenter, long id)
        {
            string col = costCenter ? "CostCenterID" : "ProjectID";
            object v = _db.ExecuteScalar(
                "SELECT COUNT(1) FROM GlJournalLine WHERE " + col + " = @id;", P("@id", id));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
        }

        public GlCostCenter GetCostCenter(long id)
        {
            return MapCostCenter(QueryRow("SELECT * FROM GlCostCenter WHERE CostCenterID = @id;", P("@id", id)));
        }

        public GlCostCenter GetCostCenter(SQLiteConnection con, SQLiteTransaction tr, long id)
        {
            return MapCostCenter(QueryRow(con, tr, "SELECT * FROM GlCostCenter WHERE CostCenterID = @id;", P("@id", id)));
        }

        public GlProject GetProject(long id)
        {
            return MapProject(QueryRow("SELECT * FROM GlProject WHERE ProjectID = @id;", P("@id", id)));
        }

        public GlProject GetProject(SQLiteConnection con, SQLiteTransaction tr, long id)
        {
            return MapProject(QueryRow(con, tr, "SELECT * FROM GlProject WHERE ProjectID = @id;", P("@id", id)));
        }

        public IList<GlCostCenter> ListCostCenters(int companyId, bool includeInactive)
        {
            DataTable table = Query(@"
SELECT * FROM GlCostCenter WHERE CompanyID = @c AND IsDeleted = 0
  AND (@all = 1 OR IsActive = 1)
ORDER BY Code;",
                P("@c", companyId), P("@all", includeInactive ? 1 : 0));
            List<GlCostCenter> list = new List<GlCostCenter>();
            foreach (DataRow r in table.Rows) list.Add(MapCostCenter(r));
            return list;
        }

        public IList<GlProject> ListProjects(int companyId, bool includeInactive)
        {
            DataTable table = Query(@"
SELECT * FROM GlProject WHERE CompanyID = @c AND IsDeleted = 0
  AND (@all = 1 OR IsActive = 1)
ORDER BY Code;",
                P("@c", companyId), P("@all", includeInactive ? 1 : 0));
            List<GlProject> list = new List<GlProject>();
            foreach (DataRow r in table.Rows) list.Add(MapProject(r));
            return list;
        }

        public bool CostCenterHasChildren(long id)
        {
            object v = _db.ExecuteScalar(
                "SELECT COUNT(1) FROM GlCostCenter WHERE ParentCostCenterID = @id AND IsDeleted = 0;", P("@id", id));
            return Convert.ToInt64(v) > 0;
        }

        public bool ProjectHasChildren(long id)
        {
            object v = _db.ExecuteScalar(
                "SELECT COUNT(1) FROM GlProject WHERE ParentProjectID = @id AND IsDeleted = 0;", P("@id", id));
            return Convert.ToInt64(v) > 0;
        }

        public long InsertCostCenter(GlCostCenter n)
        {
            return _db.ExecuteInsertReturningId(@"
INSERT INTO GlCostCenter (CompanyID, CenterID, Code, Name, ParentCostCenterID, Level, IsLeaf, IsActive,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@cid, @ctr, @code, @name, @parent, @lvl, @leaf, @act, 0, 1, @ca, @ua, @cb, @ub);",
                P("@cid", n.CompanyId), P("@ctr", n.CenterId), P("@code", n.Code), P("@name", n.Name),
                P("@parent", (object)n.ParentCostCenterId ?? DBNull.Value), P("@lvl", n.Level),
                P("@leaf", n.IsLeaf ? 1 : 0), P("@act", n.IsActive ? 1 : 0),
                P("@ca", n.CreatedAt), P("@ua", n.UpdatedAt), P("@cb", n.CreatedBy), P("@ub", n.UpdatedBy));
        }

        public long InsertProject(GlProject n)
        {
            return _db.ExecuteInsertReturningId(@"
INSERT INTO GlProject (CompanyID, CenterID, Code, Name, ParentProjectID, Level, IsLeaf, IsActive,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@cid, @ctr, @code, @name, @parent, @lvl, @leaf, @act, 0, 1, @ca, @ua, @cb, @ub);",
                P("@cid", n.CompanyId), P("@ctr", n.CenterId), P("@code", n.Code), P("@name", n.Name),
                P("@parent", (object)n.ParentProjectId ?? DBNull.Value), P("@lvl", n.Level),
                P("@leaf", n.IsLeaf ? 1 : 0), P("@act", n.IsActive ? 1 : 0),
                P("@ca", n.CreatedAt), P("@ua", n.UpdatedAt), P("@cb", n.CreatedBy), P("@ub", n.UpdatedBy));
        }

        public bool UpdateCostCenterConcurrency(GlCostCenter n, long expected)
        {
            int c = _db.ExecuteNonQuery(@"
UPDATE GlCostCenter SET Name = @name, IsLeaf = @leaf, IsActive = @act,
  RowVersion = RowVersion + 1, UpdatedAt = @ua, UpdatedBy = @ub
WHERE CostCenterID = @id AND RowVersion = @rv;",
                P("@name", n.Name), P("@leaf", n.IsLeaf ? 1 : 0), P("@act", n.IsActive ? 1 : 0),
                P("@ua", n.UpdatedAt), P("@ub", n.UpdatedBy), P("@id", n.CostCenterId), P("@rv", expected));
            return c == 1;
        }

        public bool UpdateProjectConcurrency(GlProject n, long expected)
        {
            int c = _db.ExecuteNonQuery(@"
UPDATE GlProject SET Name = @name, IsLeaf = @leaf, IsActive = @act,
  RowVersion = RowVersion + 1, UpdatedAt = @ua, UpdatedBy = @ub
WHERE ProjectID = @id AND RowVersion = @rv;",
                P("@name", n.Name), P("@leaf", n.IsLeaf ? 1 : 0), P("@act", n.IsActive ? 1 : 0),
                P("@ua", n.UpdatedAt), P("@ub", n.UpdatedBy), P("@id", n.ProjectId), P("@rv", expected));
            return c == 1;
        }

        public GlCashBookMap GetMap(int companyId, string kind, long sourceId)
        {
            return MapCashMap(QueryRow(@"
SELECT * FROM GlCashBookMap WHERE CompanyID = @c AND MapKind = @k AND SourceID = @s AND IsDeleted = 0;",
                P("@c", companyId), P("@k", kind), P("@s", sourceId)));
        }

        public IList<GlCashBookMap> ListMaps(int companyId, string kind)
        {
            DataTable table = Query(@"
SELECT m.*, a.AccountCode || ' ' || a.AccountName AS SourceName
FROM GlCashBookMap m
LEFT JOIN GlAccount a ON a.AccountID = m.AccountID
WHERE m.CompanyID = @c AND m.IsDeleted = 0 AND (@k = '' OR m.MapKind = @k)
ORDER BY m.MapKind, m.SourceID;",
                P("@c", companyId), P("@k", kind ?? ""));
            List<GlCashBookMap> list = new List<GlCashBookMap>();
            foreach (DataRow r in table.Rows)
            {
                GlCashBookMap m = MapCashMap(r);
                if (m != null) m.SourceName = Str(r["SourceName"]);
                list.Add(m);
            }
            return list;
        }

        public long InsertMap(GlCashBookMap m, string now, string user)
        {
            return _db.ExecuteInsertReturningId(@"
INSERT INTO GlCashBookMap (CompanyID, CenterID, MapKind, SourceID, AccountID, IsDeleted, RowVersion,
  CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 0, @k, @s, @a, 0, 1, @n, @n, @u, @u);",
                P("@c", m.CompanyId), P("@k", m.MapKind), P("@s", m.SourceId), P("@a", m.AccountId),
                P("@n", now), P("@u", user));
        }

        public bool UpdateMapConcurrency(GlCashBookMap m, long expected, string now, string user)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE GlCashBookMap SET AccountID = @a, RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u
WHERE MapID = @id AND RowVersion = @rv AND IsDeleted = 0;",
                P("@a", m.AccountId), P("@n", now), P("@u", user), P("@id", m.MapId), P("@rv", expected));
            return n == 1;
        }

        private static GlCostCenter MapCostCenter(DataRow r)
        {
            if (r == null) return null;
            return new GlCostCenter
            {
                CostCenterId = Long(r["CostCenterID"]),
                CompanyId = Int(r["CompanyID"]),
                CenterId = Int(r["CenterID"]),
                Code = Str(r["Code"]),
                Name = Str(r["Name"]),
                ParentCostCenterId = LongN(r["ParentCostCenterID"]),
                Level = Int(r["Level"]),
                IsLeaf = Flag(r["IsLeaf"]),
                IsActive = Flag(r["IsActive"]),
                IsDeleted = Flag(r["IsDeleted"]),
                RowVersion = Long(r["RowVersion"]),
                CreatedAt = Str(r["CreatedAt"]),
                UpdatedAt = Str(r["UpdatedAt"]),
                CreatedBy = Str(r["CreatedBy"]),
                UpdatedBy = Str(r["UpdatedBy"])
            };
        }

        private static GlProject MapProject(DataRow r)
        {
            if (r == null) return null;
            return new GlProject
            {
                ProjectId = Long(r["ProjectID"]),
                CompanyId = Int(r["CompanyID"]),
                CenterId = Int(r["CenterID"]),
                Code = Str(r["Code"]),
                Name = Str(r["Name"]),
                ParentProjectId = LongN(r["ParentProjectID"]),
                Level = Int(r["Level"]),
                IsLeaf = Flag(r["IsLeaf"]),
                IsActive = Flag(r["IsActive"]),
                IsDeleted = Flag(r["IsDeleted"]),
                RowVersion = Long(r["RowVersion"]),
                CreatedAt = Str(r["CreatedAt"]),
                UpdatedAt = Str(r["UpdatedAt"]),
                CreatedBy = Str(r["CreatedBy"]),
                UpdatedBy = Str(r["UpdatedBy"])
            };
        }

        private static GlCashBookMap MapCashMap(DataRow r)
        {
            if (r == null) return null;
            return new GlCashBookMap
            {
                MapId = Long(r["MapID"]),
                CompanyId = Int(r["CompanyID"]),
                MapKind = Str(r["MapKind"]),
                SourceId = Long(r["SourceID"]),
                AccountId = Long(r["AccountID"]),
                RowVersion = Long(r["RowVersion"])
            };
        }
    }
}

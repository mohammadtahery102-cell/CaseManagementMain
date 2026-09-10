using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Payroll.Domain;

namespace CaseManagement.Payroll.Infrastructure
{
    public class PayrollStore
    {
        private readonly DatabaseHelper _db;
        public PayrollStore() : this(new DatabaseHelper()) { }
        public PayrollStore(DatabaseHelper db) { _db = db; }

        public void ExecuteInTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            _db.ExecuteInTransaction(action);
        }

        public bool RequiresApproval(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT RequiresApproval FROM PrSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v == null || v == DBNull.Value || Convert.ToInt32(v) != 0;
        }

        public long Account(int companyId, string col)
        {
            object v = _db.ExecuteScalar("SELECT " + col + " FROM PrSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long DefaultDept(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT DepartmentID FROM PrDepartment WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY DepartmentID LIMIT 1;", P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long DefaultPosition(int companyId)
        {
            object v = _db.ExecuteScalar("SELECT PositionID FROM PrPosition WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY PositionID LIMIT 1;", P("@c", companyId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public long InsertMaster(string table, string idCol, int companyId, int centerId, string code, string name, string now, string user)
        {
            _db.ExecuteNonQuery("INSERT INTO " + table + " (CompanyID, CenterID, Code, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) VALUES (@c,@ctr,@code,@name,0,1,@n,@n,@u,@u);",
                P("@c", companyId), P("@ctr", centerId), P("@code", code), P("@name", name), P("@n", now), P("@u", user ?? ""));
            object id = _db.ExecuteScalar("SELECT " + idCol + " FROM " + table + " WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;", P("@c", companyId), P("@code", code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        public long InsertEmployee(PrEmployee e, string now, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO PrEmployee (CompanyID, CenterID, DepartmentID, PositionID, Code, Name, GrossMinor, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c,@ctr,@d,@p,@code,@name,@g,1,0,1,@n,@n,@u,@u);",
                P("@c", e.CompanyId), P("@ctr", e.CenterId), P("@d", e.DepartmentId), P("@p", e.PositionId),
                P("@code", e.Code), P("@name", e.Name), P("@g", e.GrossMinor), P("@n", now), P("@u", user ?? ""));
            object id = _db.ExecuteScalar("SELECT EmployeeID FROM PrEmployee WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;", P("@c", e.CompanyId), P("@code", e.Code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        public long InsertPeriod(int companyId, int centerId, string code, string start, string end, string now, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO PrPeriod (CompanyID, CenterID, Code, StartDate, EndDate, Status, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c,@ctr,@code,@s,@e,'Open',0,1,@n,@n,@u,@u);",
                P("@c", companyId), P("@ctr", centerId), P("@code", code), P("@s", start), P("@e", end), P("@n", now), P("@u", user ?? ""));
            object id = _db.ExecuteScalar("SELECT PeriodID FROM PrPeriod WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;", P("@c", companyId), P("@code", code));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        public string AllocateNo(SQLiteConnection con, SQLiteTransaction tr, int companyId)
        {
            long next = 1;
            string prefix = "PR";
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT NextDocNumber, NumberPrefix FROM PrSetting WHERE CompanyID = @c AND IsDeleted = 0;", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read()) { next = Convert.ToInt64(r["NextDocNumber"]); prefix = Convert.ToString(r["NumberPrefix"]); }
                }
            }
            using (SQLiteCommand upd = new SQLiteCommand("UPDATE PrSetting SET NextDocNumber = NextDocNumber + 1 WHERE CompanyID = @c;", con, tr))
            {
                upd.Parameters.AddWithValue("@c", companyId);
                upd.ExecuteNonQuery();
            }
            return prefix + "-R" + next.ToString();
        }

        public IList<PrEmployee> ActiveEmployees(int companyId)
        {
            List<PrEmployee> list = new List<PrEmployee>();
            DataTable t = _db.Query("SELECT * FROM PrEmployee WHERE CompanyID = @c AND IsActive = 1 AND IsDeleted = 0;", P("@c", companyId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                DataRow r = t.Rows[i];
                PrEmployee e = new PrEmployee();
                e.EmployeeId = Convert.ToInt64(r["EmployeeID"]);
                e.CompanyId = Convert.ToInt32(r["CompanyID"]);
                e.CenterId = Convert.ToInt32(r["CenterID"]);
                e.Code = Convert.ToString(r["Code"]);
                e.Name = Convert.ToString(r["Name"]);
                e.GrossMinor = Convert.ToInt64(r["GrossMinor"]);
                list.Add(e);
            }
            return list;
        }

        public long InsertRun(SQLiteConnection con, SQLiteTransaction tr, int companyId, int centerId, long periodId, string no, string date, long total, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO PrRun (CompanyID, CenterID, PeriodID, DocNo, Status, RunDate, TotalMinor, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c,@ctr,@p,@no,@st,@dt,@t,0,1,@n,@n,@u,@u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@ctr", centerId);
                cmd.Parameters.AddWithValue("@p", periodId);
                cmd.Parameters.AddWithValue("@no", no);
                cmd.Parameters.AddWithValue("@st", "Draft");
                cmd.Parameters.AddWithValue("@dt", date);
                cmd.Parameters.AddWithValue("@t", total);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public void InsertLine(SQLiteConnection con, SQLiteTransaction tr, long runId, int companyId, long employeeId, long amount)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO PrRunLine (RunID, CompanyID, EmployeeID, AmountMinor) VALUES (@r,@c,@e,@a);", con, tr))
            {
                cmd.Parameters.AddWithValue("@r", runId);
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@e", employeeId);
                cmd.Parameters.AddWithValue("@a", amount);
                cmd.ExecuteNonQuery();
            }
        }

        public PrRun GetRun(long id)
        {
            DataTable t = _db.Query("SELECT * FROM PrRun WHERE RunID = @id AND IsDeleted = 0;", P("@id", id));
            if (t.Rows.Count == 0) return null;
            DataRow r = t.Rows[0];
            PrRun run = new PrRun();
            run.RunId = Convert.ToInt64(r["RunID"]);
            run.CompanyId = Convert.ToInt32(r["CompanyID"]);
            run.CenterId = Convert.ToInt32(r["CenterID"]);
            run.PeriodId = Convert.ToInt64(r["PeriodID"]);
            run.DocNo = Convert.ToString(r["DocNo"]);
            run.Status = Convert.ToString(r["Status"]);
            run.RunDate = Convert.ToString(r["RunDate"]);
            run.TotalMinor = Convert.ToInt64(r["TotalMinor"]);
            run.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return run;
        }

        public bool UpdateStatus(long id, string status, long rv, string now, string user)
        {
            return _db.ExecuteNonQuery("UPDATE PrRun SET Status = @st, RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u WHERE RunID = @id AND RowVersion = @rv AND IsDeleted = 0;",
                P("@st", status), P("@n", now), P("@u", user ?? ""), P("@id", id), P("@rv", rv)) == 1;
        }

        public IList<EmployeeListRow> Employees(int companyId, int centerId)
        {
            List<EmployeeListRow> list = new List<EmployeeListRow>();
            DataTable t = _db.Query("SELECT Code, Name, GrossMinor FROM PrEmployee WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr);",
                P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                EmployeeListRow r = new EmployeeListRow();
                r.Code = Convert.ToString(t.Rows[i]["Code"]);
                r.Name = Convert.ToString(t.Rows[i]["Name"]);
                r.GrossMinor = Convert.ToInt64(t.Rows[i]["GrossMinor"]);
                list.Add(r);
            }
            return list;
        }

        public IList<PayrollSummaryRow> Summary(int companyId, int centerId)
        {
            List<PayrollSummaryRow> list = new List<PayrollSummaryRow>();
            DataTable t = _db.Query("SELECT DocNo, Status, TotalMinor FROM PrRun WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr);",
                P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                PayrollSummaryRow r = new PayrollSummaryRow();
                r.DocNo = Convert.ToString(t.Rows[i]["DocNo"]);
                r.Status = Convert.ToString(t.Rows[i]["Status"]);
                r.TotalMinor = Convert.ToInt64(t.Rows[i]["TotalMinor"]);
                list.Add(r);
            }
            return list;
        }

        public IList<PayrollRegisterRow> Register(long runId)
        {
            List<PayrollRegisterRow> list = new List<PayrollRegisterRow>();
            DataTable t = _db.Query(@"
SELECT e.Code, e.Name, l.AmountMinor FROM PrRunLine l JOIN PrEmployee e ON e.EmployeeID = l.EmployeeID WHERE l.RunID = @id;",
                P("@id", runId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                PayrollRegisterRow r = new PayrollRegisterRow();
                r.EmployeeCode = Convert.ToString(t.Rows[i]["Code"]);
                r.EmployeeName = Convert.ToString(t.Rows[i]["Name"]);
                r.AmountMinor = Convert.ToInt64(t.Rows[i]["AmountMinor"]);
                list.Add(r);
            }
            return list;
        }

        private static SQLiteParameter P(string n, object v) { return new SQLiteParameter(n, v ?? DBNull.Value); }
    }
}

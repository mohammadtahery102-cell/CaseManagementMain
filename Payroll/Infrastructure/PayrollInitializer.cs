using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using CaseManagement.Payroll.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Payroll.Infrastructure
{
    public static class PayrollInitializer
    {
        public static void Ensure()
        {
            SchemaVersion.Ensure();
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                CreateTables(con);
                Seed(con);
                TradeWorkflowSeed.Ensure(con, "PR_RUN", "اجرای حقوق", PayrollCodes.EntityRun,
                    PayrollPermissions.Create, PayrollPermissions.Approve, PayrollPermissions.Post);
            }
            SchemaVersion.SetIfNewer(SchemaVersion.ComponentPayroll, 1, "Payroll Foundation V1");
        }

        private static void CreateTables(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PrSetting (
  SettingID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  RequiresApproval INTEGER NOT NULL DEFAULT 1, NextDocNumber INTEGER NOT NULL DEFAULT 1,
  ExpenseAccountID INTEGER NOT NULL DEFAULT 0, PayableAccountID INTEGER NOT NULL DEFAULT 0,
  NumberPrefix TEXT NOT NULL DEFAULT 'PR',
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PrSetting ON PrSetting(CompanyID) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PrDepartment (
  DepartmentID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, Name TEXT NOT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PrDept ON PrDepartment(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PrPosition (
  PositionID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, Name TEXT NOT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PrPos ON PrPosition(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PrEmployee (
  EmployeeID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  DepartmentID INTEGER NOT NULL, PositionID INTEGER NOT NULL, Code TEXT NOT NULL, Name TEXT NOT NULL,
  GrossMinor INTEGER NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PrEmp ON PrEmployee(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PrPeriod (
  PeriodID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, StartDate TEXT NOT NULL, EndDate TEXT NOT NULL, Status TEXT NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PrPeriod ON PrPeriod(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PrRun (
  RunID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  PeriodID INTEGER NOT NULL, DocNo TEXT NOT NULL, Status TEXT NOT NULL, RunDate TEXT NOT NULL,
  TotalMinor INTEGER NOT NULL DEFAULT 0, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PrRun ON PrRun(CompanyID, DocNo) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PrRunLine (
  LineID INTEGER PRIMARY KEY, RunID INTEGER NOT NULL, CompanyID INTEGER NOT NULL,
  EmployeeID INTEGER NOT NULL, AmountMinor INTEGER NOT NULL
);");
        }

        private static void Seed(SQLiteConnection con)
        {
            int c = LedgerCodes.DefaultCompanyId;
            string now = LedgerTime.UtcNow(DateTime.UtcNow);
            string u = LedgerCodes.SystemUser;
            TradeCoaSeed.EnsureLeaf(con, c, PayrollCodes.AccountPayable, "حقوق پرداختنی", LedgerCodes.TypeLiability, "2000");
            long exp = TradeCoaSeed.AccountId(con, c, PayrollCodes.AccountExpense);
            long pay = TradeCoaSeed.AccountId(con, c, PayrollCodes.AccountPayable);
            if (Scalar(con, "SELECT COUNT(1) FROM PrSetting WHERE CompanyID = " + c) == 0)
            {
                Exec(con, @"
INSERT INTO PrSetting (CompanyID, RequiresApproval, NextDocNumber, ExpenseAccountID, PayableAccountID, NumberPrefix, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 1, 1, @e, @p, 'PR', 0, 1, @n, @n, @u, @u);",
                    P("@c", c), P("@e", exp), P("@p", pay), P("@n", now), P("@u", u));
            }
            if (Scalar(con, "SELECT COUNT(1) FROM PrDepartment WHERE CompanyID = " + c) == 0)
                Exec(con, "INSERT INTO PrDepartment (CompanyID, CenterID, Code, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) VALUES (@c, 0, 'GEN', 'عمومی', 0, 1, @n, @n, @u, @u);", P("@c", c), P("@n", now), P("@u", u));
            if (Scalar(con, "SELECT COUNT(1) FROM PrPosition WHERE CompanyID = " + c) == 0)
                Exec(con, "INSERT INTO PrPosition (CompanyID, CenterID, Code, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) VALUES (@c, 0, 'STAFF', 'کارمند', 0, 1, @n, @n, @u, @u);", P("@c", c), P("@n", now), P("@u", u));
        }

        private static long Scalar(SQLiteConnection con, string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                object v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
            }
        }

        private static SQLiteParameter P(string n, object v) { return new SQLiteParameter(n, v ?? DBNull.Value); }

        private static void Exec(SQLiteConnection con, string sql, params SQLiteParameter[] p)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                if (p != null && p.Length > 0) cmd.Parameters.AddRange(p);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

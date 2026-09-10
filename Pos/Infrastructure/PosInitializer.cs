using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using CaseManagement.Pos.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Pos.Infrastructure
{
    public static class PosInitializer
    {
        public static void Ensure()
        {
            SchemaVersion.Ensure();
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                CreateTables(con);
                Seed(con);
                TradeWorkflowSeed.Ensure(con, "POS_SALE", "فروش صندوق", PosCodes.EntitySale,
                    PosPermissions.Create, PosPermissions.Approve, PosPermissions.Post);
                TradeWorkflowSeed.Ensure(con, "POS_RET", "برگشت صندوق", PosCodes.EntityReturn,
                    PosPermissions.Create, PosPermissions.Approve, PosPermissions.Post);
            }
            SchemaVersion.SetIfNewer(SchemaVersion.ComponentPos, 1, "POS Foundation V1");
        }

        private static void CreateTables(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PosSetting (
  SettingID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  RequiresApproval INTEGER NOT NULL DEFAULT 1, NextDocNumber INTEGER NOT NULL DEFAULT 1,
  NumberPrefix TEXT NOT NULL DEFAULT 'POS',
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PosSetting ON PosSetting(CompanyID) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PosTerminal (
  TerminalID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  Code TEXT NOT NULL, Name TEXT NOT NULL, WarehouseID INTEGER NOT NULL DEFAULT 0,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PosTerm ON PosTerminal(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PosDrawer (
  DrawerID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  TerminalID INTEGER NOT NULL, Code TEXT NOT NULL, Name TEXT NOT NULL, FloatMinor INTEGER NOT NULL DEFAULT 0,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PosDrawer ON PosDrawer(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PosSale (
  SaleID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  TerminalID INTEGER NOT NULL, DrawerID INTEGER NOT NULL, WarehouseID INTEGER NOT NULL, CustomerID INTEGER NOT NULL DEFAULT 0,
  DocNo TEXT NOT NULL, Status TEXT NOT NULL, SaleDate TEXT NOT NULL, AmountMinor INTEGER NOT NULL DEFAULT 0,
  OrderID INTEGER NULL, DeliveryID INTEGER NULL, InvoiceID INTEGER NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PosSale ON PosSale(CompanyID, DocNo) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PosSaleLine (
  LineID INTEGER PRIMARY KEY, SaleID INTEGER NOT NULL, CompanyID INTEGER NOT NULL,
  LineNo INTEGER NOT NULL, ItemID INTEGER NOT NULL, Qty INTEGER NOT NULL, UnitPriceMinor INTEGER NOT NULL, AmountMinor INTEGER NOT NULL
);");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PosReturn (
  ReturnID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  SaleID INTEGER NOT NULL, DocNo TEXT NOT NULL, Status TEXT NOT NULL, ReturnDate TEXT NOT NULL,
  AmountMinor INTEGER NOT NULL DEFAULT 0, InvDocumentID INTEGER NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PosRet ON PosReturn(CompanyID, DocNo) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PosReturnLine (
  LineID INTEGER PRIMARY KEY, ReturnID INTEGER NOT NULL, CompanyID INTEGER NOT NULL,
  LineNo INTEGER NOT NULL, ItemID INTEGER NOT NULL, Qty INTEGER NOT NULL, UnitPriceMinor INTEGER NOT NULL, AmountMinor INTEGER NOT NULL
);");
        }

        private static void Seed(SQLiteConnection con)
        {
            int c = LedgerCodes.DefaultCompanyId;
            string now = LedgerTime.UtcNow(DateTime.UtcNow);
            string u = LedgerCodes.SystemUser;
            if (Scalar(con, "SELECT COUNT(1) FROM PosSetting WHERE CompanyID = " + c) == 0)
            {
                Exec(con, @"
INSERT INTO PosSetting (CompanyID, RequiresApproval, NextDocNumber, NumberPrefix, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 1, 1, 'POS', 0, 1, @n, @n, @u, @u);", P("@c", c), P("@n", now), P("@u", u));
            }
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

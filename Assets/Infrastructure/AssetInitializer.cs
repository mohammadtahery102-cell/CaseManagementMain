using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Assets.Domain;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using CaseManagement.Trade;

namespace CaseManagement.Assets.Infrastructure
{
    public static class AssetInitializer
    {
        public static void Ensure()
        {
            SchemaVersion.Ensure();
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                CreateTables(con);
                Seed(con);
                TradeWorkflowSeed.Ensure(con, "FA_ACQ", "تحصیل دارایی", AssetCodes.EntityAcquisition,
                    AssetPermissions.Create, AssetPermissions.Approve, AssetPermissions.Post);
                TradeWorkflowSeed.Ensure(con, "FA_DISP", "واگذاری دارایی", AssetCodes.EntityDisposal,
                    AssetPermissions.Create, AssetPermissions.Approve, AssetPermissions.Post);
                TradeWorkflowSeed.Ensure(con, "FA_DEP", "استهلاک دارایی", AssetCodes.EntityDepreciation,
                    AssetPermissions.Create, AssetPermissions.Approve, AssetPermissions.Post);
            }
            SchemaVersion.SetIfNewer(SchemaVersion.ComponentFixedAssets, 1, "Fixed Assets Foundation V1");
        }

        private static void CreateTables(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS FaSetting (
  SettingID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  RequiresApproval INTEGER NOT NULL DEFAULT 1, NextDocNumber INTEGER NOT NULL DEFAULT 1,
  AssetAccountID INTEGER NOT NULL DEFAULT 0, AccumAccountID INTEGER NOT NULL DEFAULT 0,
  CashAccountID INTEGER NOT NULL DEFAULT 0, DepExpAccountID INTEGER NOT NULL DEFAULT 0,
  DisposalLossAccountID INTEGER NOT NULL DEFAULT 0, NumberPrefix TEXT NOT NULL DEFAULT 'FA',
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_FaSetting ON FaSetting(CompanyID) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS FaCategory (
  CategoryID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, Name TEXT NOT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_FaCat ON FaCategory(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS FaLocation (
  LocationID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, Name TEXT NOT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_FaLoc ON FaLocation(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS FaCustodian (
  CustodianID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, Name TEXT NOT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_FaCust ON FaCustodian(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS FaAsset (
  AssetID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  CategoryID INTEGER NOT NULL, LocationID INTEGER NOT NULL, CustodianID INTEGER NOT NULL,
  Code TEXT NOT NULL, Name TEXT NOT NULL, CostMinor INTEGER NOT NULL, AccumDepMinor INTEGER NOT NULL DEFAULT 0,
  UsefulLifeMonths INTEGER NOT NULL, Status TEXT NOT NULL, AcquisitionDate TEXT NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_FaAsset ON FaAsset(CompanyID, Code) WHERE IsDeleted = 0;");
            Doc(con, "FaAcquisition", "AcquisitionID", "AmountMinor INTEGER NOT NULL");
            Doc(con, "FaDisposal", "DisposalID", "CostMinor INTEGER NOT NULL, AccumMinor INTEGER NOT NULL");
            Doc(con, "FaTransfer", "TransferID", "FromLocationID INTEGER NOT NULL, ToLocationID INTEGER NOT NULL, FromCustodianID INTEGER NOT NULL, ToCustodianID INTEGER NOT NULL");
            Doc(con, "FaDepreciation", "DepreciationID", "TotalMinor INTEGER NOT NULL");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS FaDepreciationLine (
  LineID INTEGER PRIMARY KEY, DepreciationID INTEGER NOT NULL, CompanyID INTEGER NOT NULL,
  AssetID INTEGER NOT NULL, AmountMinor INTEGER NOT NULL
);");
        }

        private static void Doc(SQLiteConnection con, string table, string idCol, string extra)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS " + table + @" (
  " + idCol + @" INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  DocNo TEXT NOT NULL, Status TEXT NOT NULL, DocDate TEXT NOT NULL, AssetID INTEGER NOT NULL DEFAULT 0,
  " + extra + @", IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_" + table + "_No ON " + table + "(CompanyID, DocNo) WHERE IsDeleted = 0;");
        }

        private static void Seed(SQLiteConnection con)
        {
            int c = LedgerCodes.DefaultCompanyId;
            string now = LedgerTime.UtcNow(DateTime.UtcNow);
            string u = LedgerCodes.SystemUser;
            TradeCoaSeed.EnsureLeaf(con, c, AssetCodes.AccountAsset, "دارایی ثابت", LedgerCodes.TypeAsset, "1000");
            TradeCoaSeed.EnsureLeaf(con, c, AssetCodes.AccountAccum, "استهلاک انباشته", LedgerCodes.TypeAsset, "1000");
            TradeCoaSeed.EnsureLeaf(con, c, AssetCodes.AccountDepExp, "هزینه استهلاک", LedgerCodes.TypeExpense, "5000");
            TradeCoaSeed.EnsureLeaf(con, c, AssetCodes.AccountDisposalLoss, "زیان واگذاری دارایی", LedgerCodes.TypeExpense, "5000");
            long asset = TradeCoaSeed.AccountId(con, c, AssetCodes.AccountAsset);
            long accum = TradeCoaSeed.AccountId(con, c, AssetCodes.AccountAccum);
            long cash = TradeCoaSeed.AccountId(con, c, AssetCodes.AccountCash);
            long dep = TradeCoaSeed.AccountId(con, c, AssetCodes.AccountDepExp);
            long loss = TradeCoaSeed.AccountId(con, c, AssetCodes.AccountDisposalLoss);
            if (Scalar(con, "SELECT COUNT(1) FROM FaSetting WHERE CompanyID = " + c) == 0)
            {
                Exec(con, @"
INSERT INTO FaSetting (CompanyID, RequiresApproval, NextDocNumber, AssetAccountID, AccumAccountID, CashAccountID, DepExpAccountID, DisposalLossAccountID, NumberPrefix, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 1, 1, @a, @ac, @cash, @dep, @loss, 'FA', 0, 1, @n, @n, @u, @u);",
                    P("@c", c), P("@a", asset), P("@ac", accum), P("@cash", cash), P("@dep", dep), P("@loss", loss), P("@n", now), P("@u", u));
            }
            if (Scalar(con, "SELECT COUNT(1) FROM FaCategory WHERE CompanyID = " + c) == 0)
                Exec(con, "INSERT INTO FaCategory (CompanyID, CenterID, Code, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) VALUES (@c, 0, 'GEN', 'عمومی', 0, 1, @n, @n, @u, @u);", P("@c", c), P("@n", now), P("@u", u));
            if (Scalar(con, "SELECT COUNT(1) FROM FaLocation WHERE CompanyID = " + c) == 0)
                Exec(con, "INSERT INTO FaLocation (CompanyID, CenterID, Code, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) VALUES (@c, 0, 'MAIN', 'محل اصلی', 0, 1, @n, @n, @u, @u);", P("@c", c), P("@n", now), P("@u", u));
            if (Scalar(con, "SELECT COUNT(1) FROM FaCustodian WHERE CompanyID = " + c) == 0)
                Exec(con, "INSERT INTO FaCustodian (CompanyID, CenterID, Code, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) VALUES (@c, 0, 'OWN', 'سازمان', 0, 1, @n, @n, @u, @u);", P("@c", c), P("@n", now), P("@u", u));
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

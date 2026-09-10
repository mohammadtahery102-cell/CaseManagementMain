using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using CaseManagement.Inventory.Domain;

namespace CaseManagement.Inventory.Infrastructure
{
    public static class InventoryInitializer
    {
        public static void Ensure()
        {
            SchemaVersion.Ensure();
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                CreateTables(con);
                Seed(con);
            }
            SchemaVersion.SetIfNewer(SchemaVersion.ComponentInventory, 1, "Inventory Foundation V1");
        }

        private static void CreateTables(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvItemCategory (
  CategoryID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, Name TEXT NOT NULL, ParentCategoryID INTEGER NULL, Level INTEGER NOT NULL DEFAULT 1,
  IsLeaf INTEGER NOT NULL DEFAULT 1, IsActive INTEGER NOT NULL DEFAULT 1, IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL, DeletedBy TEXT NULL, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_InvCat_Code ON InvItemCategory(CompanyID, Code) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvUnitOfMeasure (
  UomID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, Name TEXT NOT NULL, DecimalPlaces INTEGER NOT NULL DEFAULT 0,
  IsActive INTEGER NOT NULL DEFAULT 1, IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL, DeletedBy TEXT NULL, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_InvUom_Code ON InvUnitOfMeasure(CompanyID, Code) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvItem (
  ItemID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, Name TEXT NOT NULL, CategoryID INTEGER NOT NULL, BaseUomID INTEGER NOT NULL,
  CostingMethod TEXT NOT NULL, IsStockable INTEGER NOT NULL DEFAULT 1,
  MinQtyBase INTEGER NOT NULL DEFAULT 0, MaxQtyBase INTEGER NOT NULL DEFAULT 0,
  IsActive INTEGER NOT NULL DEFAULT 1, IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL, DeletedBy TEXT NULL, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_InvItem_Code ON InvItem(CompanyID, Code) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvWarehouse (
  WarehouseID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  Code TEXT NOT NULL, Name TEXT NOT NULL,
  IsActive INTEGER NOT NULL DEFAULT 1, IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL, DeletedBy TEXT NULL, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_InvWh_Code ON InvWarehouse(CompanyID, Code) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvLocation (
  LocationID INTEGER PRIMARY KEY, WarehouseID INTEGER NOT NULL, CompanyID INTEGER NOT NULL,
  Code TEXT NOT NULL, Name TEXT NOT NULL, ParentLocationID INTEGER NULL, Level INTEGER NOT NULL DEFAULT 1,
  IsLeaf INTEGER NOT NULL DEFAULT 1, IsActive INTEGER NOT NULL DEFAULT 1, IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL, DeletedBy TEXT NULL, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_InvLoc_Code ON InvLocation(WarehouseID, Code) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvItemMap (
  MapID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  OwnerType TEXT NOT NULL, OwnerID INTEGER NOT NULL, MapRole TEXT NOT NULL, AccountID INTEGER NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, DeletedAt TEXT NULL, DeletedBy TEXT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, @"
CREATE UNIQUE INDEX IF NOT EXISTS UX_InvItemMap_Role
ON InvItemMap(CompanyID, OwnerType, OwnerID, MapRole) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvSetting (
  SettingID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  CostingMethod TEXT NOT NULL, AllowNegativeStock INTEGER NOT NULL DEFAULT 0,
  RequireLocation INTEGER NOT NULL DEFAULT 0, RequiresApproval INTEGER NOT NULL DEFAULT 0,
  NextDocNumber INTEGER NOT NULL DEFAULT 1, NumberPrefix TEXT NOT NULL DEFAULT 'INV',
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_InvSetting_Co ON InvSetting(CompanyID) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvDocument (
  DocumentID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  DocNo TEXT NOT NULL, DocumentType TEXT NOT NULL, Status TEXT NOT NULL, PostingDate TEXT NOT NULL,
  WarehouseID INTEGER NOT NULL, ToLocationID INTEGER NULL,
  CostCenterID INTEGER NULL, ProjectID INTEGER NULL, PartyID INTEGER NULL,
  SourceModule TEXT NULL, SourceDocumentType TEXT NULL, SourceDocumentID INTEGER NULL, GroupId TEXT NULL,
  Description TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL, DeletedBy TEXT NULL, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_InvDoc_Source ON InvDocument(CompanyID, SourceModule, SourceDocumentType, SourceDocumentID);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_InvDoc_No ON InvDocument(CompanyID, DocumentType, DocNo) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvDocumentLine (
  LineID INTEGER PRIMARY KEY, DocumentID INTEGER NOT NULL, CompanyID INTEGER NOT NULL, LineNo INTEGER NOT NULL,
  ItemID INTEGER NOT NULL, LocationID INTEGER NOT NULL, QtyDoc INTEGER NOT NULL, QtyBase INTEGER NOT NULL,
  UnitCostMinor INTEGER NOT NULL DEFAULT 0, ValueMinor INTEGER NOT NULL DEFAULT 0,
  CostCenterID INTEGER NULL, ProjectID INTEGER NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_InvDocLine ON InvDocumentLine(DocumentID, LineNo);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvItemLedger (
  ItemLedgerID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  ItemID INTEGER NOT NULL, WarehouseID INTEGER NOT NULL, LocationID INTEGER NOT NULL,
  DocumentType TEXT NOT NULL, DocumentID INTEGER NOT NULL, DocumentLineNo INTEGER NOT NULL,
  MovementType TEXT NOT NULL, QtyBase INTEGER NOT NULL, UnitCostMinor INTEGER NOT NULL, ValueMinor INTEGER NOT NULL,
  PostingDate TEXT NOT NULL, CostCenterID INTEGER NULL, ProjectID INTEGER NULL, ReversesLedgerID INTEGER NULL,
  CreatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_InvLed_ItemWh ON InvItemLedger(CompanyID, ItemID, WarehouseID, LocationID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_InvLed_Doc ON InvItemLedger(DocumentID);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS InvItemBalance (
  BalanceID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  ItemID INTEGER NOT NULL, WarehouseID INTEGER NOT NULL, LocationID INTEGER NOT NULL,
  QuantityOnHand INTEGER NOT NULL DEFAULT 0, InventoryValueMinor INTEGER NOT NULL DEFAULT 0,
  AverageCostMinor INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  UpdatedAt TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, @"
CREATE UNIQUE INDEX IF NOT EXISTS UX_InvBal_Grain
ON InvItemBalance(CompanyID, ItemID, WarehouseID, LocationID);");
        }

        private static void Seed(SQLiteConnection con)
        {
            int companyId = LedgerCodes.DefaultCompanyId;
            string now = LedgerTime.UtcNow(DateTime.UtcNow);
            string user = LedgerCodes.SystemUser;
            if (Scalar(con, "SELECT COUNT(1) FROM InvUnitOfMeasure WHERE CompanyID = " + companyId) == 0)
            {
                Exec(con, @"
INSERT INTO InvUnitOfMeasure (CompanyID, CenterID, Code, Name, DecimalPlaces, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 0, 'PCS', 'عدد', 0, 1, 0, 1, @n, @n, @u, @u);",
                    P("@c", companyId), P("@n", now), P("@u", user));
            }
            if (Scalar(con, "SELECT COUNT(1) FROM InvItemCategory WHERE CompanyID = " + companyId) == 0)
            {
                Exec(con, @"
INSERT INTO InvItemCategory (CompanyID, CenterID, Code, Name, Level, IsLeaf, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 0, 'GEN', 'عمومی', 1, 1, 1, 0, 1, @n, @n, @u, @u);",
                    P("@c", companyId), P("@n", now), P("@u", user));
            }
            if (Scalar(con, "SELECT COUNT(1) FROM InvWarehouse WHERE CompanyID = " + companyId) == 0)
            {
                Exec(con, @"
INSERT INTO InvWarehouse (CompanyID, CenterID, Code, Name, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 1, 'MAIN', 'انبار اصلی', 1, 0, 1, @n, @n, @u, @u);",
                    P("@c", companyId), P("@n", now), P("@u", user));
                long wh = Scalar(con, "SELECT WarehouseID FROM InvWarehouse WHERE CompanyID = " + companyId + " AND Code = 'MAIN'");
                Exec(con, @"
INSERT INTO InvLocation (WarehouseID, CompanyID, Code, Name, Level, IsLeaf, IsActive, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@w, @c, 'DEFAULT', 'پیش‌فرض', 1, 1, 1, 0, 1, @n, @n, @u, @u);",
                    P("@w", wh), P("@c", companyId), P("@n", now), P("@u", user));
            }
            if (Scalar(con, "SELECT COUNT(1) FROM InvSetting WHERE CompanyID = " + companyId) == 0)
            {
                Exec(con, @"
INSERT INTO InvSetting (CompanyID, CostingMethod, AllowNegativeStock, RequireLocation, RequiresApproval, NextDocNumber, NumberPrefix,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @m, 0, 0, 0, 1, 'INV', 0, 1, @n, @n, @u, @u);",
                    P("@c", companyId), P("@m", InventoryCodes.CostingMovingAverage), P("@n", now), P("@u", user));
            }
            SeedCompanyMap(con, companyId, InventoryCodes.RoleInventory, "1300", now, user);
            SeedCompanyMap(con, companyId, InventoryCodes.RoleCogs, "5400", now, user);
            SeedCompanyMap(con, companyId, InventoryCodes.RoleAdjGain, "4410", now, user);
            SeedCompanyMap(con, companyId, InventoryCodes.RoleAdjLoss, "5410", now, user);
            SeedCompanyMap(con, companyId, InventoryCodes.RoleRevaluation, "4420", now, user);
            SeedCompanyMap(con, companyId, InventoryCodes.RoleGrir, "2400", now, user);
            SeedCompanyMap(con, companyId, InventoryCodes.RoleOpeningOffset, "3100", now, user);
        }

        private static void SeedCompanyMap(SQLiteConnection con, int companyId, string role, string accountCode, string now, string user)
        {
            long acc = Scalar(con, "SELECT AccountID FROM GlAccount WHERE CompanyID = " + companyId
                + " AND AccountCode = '" + accountCode.Replace("'", "''") + "' AND IsDeleted = 0");
            if (acc <= 0) return;
            if (Scalar(con, "SELECT COUNT(1) FROM InvItemMap WHERE CompanyID = " + companyId
                + " AND OwnerType = 'Company' AND OwnerID = " + companyId + " AND MapRole = '" + role.Replace("'", "''") + "' AND IsDeleted = 0") > 0)
                return;
            Exec(con, @"
INSERT INTO InvItemMap (CompanyID, OwnerType, OwnerID, MapRole, AccountID, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 'Company', @c, @r, @a, 0, 1, @n, @n, @u, @u);",
                P("@c", companyId), P("@r", role), P("@a", acc), P("@n", now), P("@u", user));
        }

        private static long Scalar(SQLiteConnection con, string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                object v = cmd.ExecuteScalar();
                if (v == null || v == DBNull.Value) return 0;
                return Convert.ToInt64(v);
            }
        }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        private static void Exec(SQLiteConnection con, string sql, params SQLiteParameter[] parameters)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                if (parameters != null && parameters.Length > 0) cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using CaseManagement.Purchase.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Purchase.Infrastructure
{
    public static class PurchaseInitializer
    {
        public static void Ensure()
        {
            SchemaVersion.Ensure();
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                CreateTables(con);
                Seed(con);
                TradeWorkflowSeed.Ensure(con, "PUR_PO", "سفارش خرید", PurchaseCodes.EntityOrder,
                    PurchasePermissions.Create, PurchasePermissions.Approve, PurchasePermissions.Post);
                TradeWorkflowSeed.Ensure(con, "PUR_GR", "رسید کالا", PurchaseCodes.EntityReceipt,
                    PurchasePermissions.Create, PurchasePermissions.Approve, PurchasePermissions.Post);
                TradeWorkflowSeed.Ensure(con, "PUR_INV", "فاکتور خرید", PurchaseCodes.EntityInvoice,
                    PurchasePermissions.Create, PurchasePermissions.Approve, PurchasePermissions.Post);
            }
            SchemaVersion.SetIfNewer(SchemaVersion.ComponentPurchase, 1, "Purchase Foundation V1");
        }

        private static void CreateTables(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PurVendorCategory (
  CategoryID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, Name TEXT NOT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PurVenCat ON PurVendorCategory(CompanyID, Code) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS PurVendor (
  VendorID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  CategoryID INTEGER NOT NULL, Code TEXT NOT NULL, Name TEXT NOT NULL,
  IsActive INTEGER NOT NULL DEFAULT 1, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PurVendor ON PurVendor(CompanyID, Code) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS PurVendorContact (
  ContactID INTEGER PRIMARY KEY, VendorID INTEGER NOT NULL, CompanyID INTEGER NOT NULL,
  Name TEXT NOT NULL, Phone TEXT NULL, Email TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS PurSetting (
  SettingID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  RequiresApproval INTEGER NOT NULL DEFAULT 1, NextDocNumber INTEGER NOT NULL DEFAULT 1,
  ApAccountID INTEGER NOT NULL DEFAULT 0, NumberPrefix TEXT NOT NULL DEFAULT 'PUR',
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PurSetting ON PurSetting(CompanyID) WHERE IsDeleted = 0;");

            CreateDoc(con, "PurRequest", "RequestID");
            CreateLines(con, "PurRequestLine", "RequestID");
            CreateDoc(con, "PurOrder", "OrderID");
            CreateLines(con, "PurOrderLine", "OrderID");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PurGoodsReceipt (
  ReceiptID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  DocNo TEXT NOT NULL, Status TEXT NOT NULL, PostingDate TEXT NOT NULL,
  OrderID INTEGER NOT NULL, VendorID INTEGER NOT NULL, WarehouseID INTEGER NOT NULL,
  InvDocumentID INTEGER NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PurGR_No ON PurGoodsReceipt(CompanyID, DocNo) WHERE IsDeleted = 0;");
            CreateLines(con, "PurGoodsReceiptLine", "ReceiptID");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS PurInvoice (
  InvoiceID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  DocNo TEXT NOT NULL, Status TEXT NOT NULL, InvoiceDate TEXT NOT NULL,
  ReceiptID INTEGER NOT NULL, OrderID INTEGER NOT NULL, VendorID INTEGER NOT NULL,
  AmountMinor INTEGER NOT NULL DEFAULT 0, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_PurInv_No ON PurInvoice(CompanyID, DocNo) WHERE IsDeleted = 0;");
            CreateLines(con, "PurInvoiceLine", "InvoiceID");
        }

        private static void CreateDoc(SQLiteConnection con, string table, string idCol)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS " + table + @" (
  " + idCol + @" INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  DocNo TEXT NOT NULL, Status TEXT NOT NULL, DocDate TEXT NOT NULL,
  VendorID INTEGER NOT NULL, WarehouseID INTEGER NOT NULL, SourceID INTEGER NULL,
  Description TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_" + table + "_No ON " + table + "(CompanyID, DocNo) WHERE IsDeleted = 0;");
        }

        private static void CreateLines(SQLiteConnection con, string table, string parentCol)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS " + table + @" (
  LineID INTEGER PRIMARY KEY, " + parentCol + @" INTEGER NOT NULL, CompanyID INTEGER NOT NULL,
  LineNo INTEGER NOT NULL, ItemID INTEGER NOT NULL, Qty INTEGER NOT NULL,
  UnitPriceMinor INTEGER NOT NULL DEFAULT 0, AmountMinor INTEGER NOT NULL DEFAULT 0
);");
        }

        private static void Seed(SQLiteConnection con)
        {
            int c = LedgerCodes.DefaultCompanyId;
            string now = LedgerTime.UtcNow(DateTime.UtcNow);
            string u = LedgerCodes.SystemUser;
            if (Scalar(con, "SELECT COUNT(1) FROM PurVendorCategory WHERE CompanyID = " + c) == 0)
            {
                Exec(con, @"
INSERT INTO PurVendorCategory (CompanyID, CenterID, Code, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 0, 'GEN', 'عمومی', 0, 1, @n, @n, @u, @u);",
                    P("@c", c), P("@n", now), P("@u", u));
            }
            long ap = Scalar(con, "SELECT AccountID FROM GlAccount WHERE CompanyID = " + c + " AND AccountCode = '2100' AND IsDeleted = 0");
            if (Scalar(con, "SELECT COUNT(1) FROM PurSetting WHERE CompanyID = " + c) == 0)
            {
                Exec(con, @"
INSERT INTO PurSetting (CompanyID, RequiresApproval, NextDocNumber, ApAccountID, NumberPrefix, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 1, 1, @ap, 'PUR', 0, 1, @n, @n, @u, @u);",
                    P("@c", c), P("@ap", ap), P("@n", now), P("@u", u));
            }
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

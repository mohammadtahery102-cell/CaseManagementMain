using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using CaseManagement.Sales.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Sales.Infrastructure
{
    public static class SalesInitializer
    {
        public static void Ensure()
        {
            SchemaVersion.Ensure();
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                CreateTables(con);
                Seed(con);
                TradeWorkflowSeed.Ensure(con, "SAL_SO", "سفارش فروش", SalesCodes.EntityOrder,
                    SalesPermissions.Create, SalesPermissions.Approve, SalesPermissions.Post);
                TradeWorkflowSeed.Ensure(con, "SAL_DN", "حواله فروش", SalesCodes.EntityDelivery,
                    SalesPermissions.Create, SalesPermissions.Approve, SalesPermissions.Post);
                TradeWorkflowSeed.Ensure(con, "SAL_INV", "فاکتور فروش", SalesCodes.EntityInvoice,
                    SalesPermissions.Create, SalesPermissions.Approve, SalesPermissions.Post);
            }
            SchemaVersion.SetIfNewer(SchemaVersion.ComponentSales, 1, "Sales Foundation V1");
        }

        private static void CreateTables(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SalCustomerCategory (
  CategoryID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  Code TEXT NOT NULL, Name TEXT NOT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_SalCustCat ON SalCustomerCategory(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SalCustomer (
  CustomerID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL DEFAULT 0,
  CategoryID INTEGER NOT NULL, Code TEXT NOT NULL, Name TEXT NOT NULL,
  IsActive INTEGER NOT NULL DEFAULT 1, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_SalCustomer ON SalCustomer(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SalCustomerContact (
  ContactID INTEGER PRIMARY KEY, CustomerID INTEGER NOT NULL, CompanyID INTEGER NOT NULL,
  Name TEXT NOT NULL, Phone TEXT NULL, Email TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SalSetting (
  SettingID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  RequiresApproval INTEGER NOT NULL DEFAULT 1, NextDocNumber INTEGER NOT NULL DEFAULT 1,
  ArAccountID INTEGER NOT NULL DEFAULT 0, RevenueAccountID INTEGER NOT NULL DEFAULT 0,
  NumberPrefix TEXT NOT NULL DEFAULT 'SAL',
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_SalSetting ON SalSetting(CompanyID) WHERE IsDeleted = 0;");
            CreateDoc(con, "SalQuotation", "QuoteID");
            CreateLines(con, "SalQuotationLine", "QuoteID");
            CreateDoc(con, "SalOrder", "OrderID");
            CreateLines(con, "SalOrderLine", "OrderID");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SalDelivery (
  DeliveryID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  DocNo TEXT NOT NULL, Status TEXT NOT NULL, PostingDate TEXT NOT NULL,
  OrderID INTEGER NOT NULL, CustomerID INTEGER NOT NULL, WarehouseID INTEGER NOT NULL,
  InvDocumentID INTEGER NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_SalDN_No ON SalDelivery(CompanyID, DocNo) WHERE IsDeleted = 0;");
            CreateLines(con, "SalDeliveryLine", "DeliveryID");
            Exec(con, @"
CREATE TABLE IF NOT EXISTS SalInvoice (
  InvoiceID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  DocNo TEXT NOT NULL, Status TEXT NOT NULL, InvoiceDate TEXT NOT NULL,
  DeliveryID INTEGER NOT NULL, OrderID INTEGER NOT NULL, CustomerID INTEGER NOT NULL,
  AmountMinor INTEGER NOT NULL DEFAULT 0, IsDeleted INTEGER NOT NULL DEFAULT 0,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_SalInv_No ON SalInvoice(CompanyID, DocNo) WHERE IsDeleted = 0;");
            CreateLines(con, "SalInvoiceLine", "InvoiceID");
        }

        private static void CreateDoc(SQLiteConnection con, string table, string idCol)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS " + table + @" (
  " + idCol + @" INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  DocNo TEXT NOT NULL, Status TEXT NOT NULL, DocDate TEXT NOT NULL,
  CustomerID INTEGER NOT NULL, WarehouseID INTEGER NOT NULL, SourceID INTEGER NULL,
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
            if (Scalar(con, "SELECT COUNT(1) FROM SalCustomerCategory WHERE CompanyID = " + c) == 0)
            {
                Exec(con, @"
INSERT INTO SalCustomerCategory (CompanyID, CenterID, Code, Name, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 0, 'GEN', 'عمومی', 0, 1, @n, @n, @u, @u);", P("@c", c), P("@n", now), P("@u", u));
            }
            long ar = Scalar(con, "SELECT AccountID FROM GlAccount WHERE CompanyID = " + c + " AND AccountCode = '1200' AND IsDeleted = 0");
            long rev = Scalar(con, "SELECT AccountID FROM GlAccount WHERE CompanyID = " + c + " AND AccountCode = '4100' AND IsDeleted = 0");
            if (Scalar(con, "SELECT COUNT(1) FROM SalSetting WHERE CompanyID = " + c) == 0)
            {
                Exec(con, @"
INSERT INTO SalSetting (CompanyID, RequiresApproval, NextDocNumber, ArAccountID, RevenueAccountID, NumberPrefix, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 1, 1, @ar, @rev, 'SAL', 0, 1, @n, @n, @u, @u);",
                    P("@c", c), P("@ar", ar), P("@rev", rev), P("@n", now), P("@u", u));
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

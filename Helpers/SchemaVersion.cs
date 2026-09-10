using System;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // فاز ۱ ERP — نسخهٔ اسکیما به‌ازای هر مؤلفه.
    //
    // آموزش: تا امروز مهاجرت یعنی اجرای همه‌ی Ensure* در هر شروع برنامه.
    // این جدول عدد نسخه را ثبت می‌کند تا فازهای بعد بتوانند «از نسخه N به
    // N+1» تصمیم بگیرند، بدون اینکه EnsureColumnهای موجود را حذف کنند.
    // کاملاً افزایشی است و هیچ جدول دیگری را تغییر نمی‌دهد.
    // ─────────────────────────────────────────────────────────────────────────
    public static class SchemaVersion
    {
        public const string ComponentCore       = "Core";
        public const string ComponentCharity    = "Charity";
        public const string ComponentAccounting = "Accounting";
        public const string ComponentEnterprise = "Enterprise";
        public const string ComponentSync       = "Sync";
        public const string ComponentAdmin      = "Admin";
        public const string ComponentAi         = "Ai";
        public const string ComponentInventory  = "Inventory";
        public const string ComponentPurchase   = "Purchase";
        public const string ComponentSales      = "Sales";
        public const string ComponentCrm        = "Crm";

        public static void Ensure()
        {
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(@"
CREATE TABLE IF NOT EXISTS CoreSchemaVersion (
    Component TEXT    NOT NULL PRIMARY KEY,
    Version   INTEGER NOT NULL,
    AppliedAt TEXT    NOT NULL DEFAULT (datetime('now')),
    Note      TEXT    NULL
);", con))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            SetIfNewer(ComponentCore, ProductMode.SchemaVersionNumber,
                "Phase 1: ProductMode + charity quarantine");
        }

        public static int Get(string component)
        {
            if (string.IsNullOrWhiteSpace(component)) return 0;

            try
            {
                object value = new DatabaseHelper().ExecuteScalar(
                    "SELECT Version FROM CoreSchemaVersion WHERE Component = @C;",
                    new SQLiteParameter("@C", component));
                if (value == null || value == DBNull.Value) return 0;
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        public static void SetIfNewer(string component, int version, string note)
        {
            if (string.IsNullOrWhiteSpace(component) || version < 0) return;

            int current = Get(component);
            if (current >= version) return;

            new DatabaseHelper().ExecuteNonQuery(@"
INSERT INTO CoreSchemaVersion (Component, Version, AppliedAt, Note)
VALUES (@C, @V, datetime('now'), @N)
ON CONFLICT(Component) DO UPDATE SET
    Version   = @V,
    AppliedAt = datetime('now'),
    Note      = @N
WHERE excluded.Version > CoreSchemaVersion.Version;",
                new SQLiteParameter("@C", component),
                new SQLiteParameter("@V", version),
                new SQLiteParameter("@N", note ?? ""));
        }
    }
}

using System;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // ماژول «حسابداری داخلی ایتام» — ساخت جداول پایگاه‌داده (کاملاً افزایشی).
    // آموزش: این کلاس هیچ‌کدام از جداول نرم‌افزار مدیریت پرونده را دست نمی‌زند؛
    // فقط جداول جدید با پیشوند Acc را در صورت نبود می‌سازد و مقادیر پیش‌فرض
    // (دسته‌های درآمد/هزینه، صندوق‌ها، تنظیمات گزارش) را مقداردهی می‌کند.
    // از همان DatabaseHelper (SQLite) پروژه استفاده می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public static class AccountingInitializer
    {
        public static void EnsureAccountingObjects()
        {
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                // ─── دوره مالی (برج/سال) ─────────────────────────────────────
                // آموزش: پرداخت شهریه گاهی چندماهه یک‌جا انجام می‌شود (مثل
                // «برج اسد و سنبله»)؛ Month = برج شروع، MonthTo = برج پایان
                // (اگر تک‌ماهه باشد برابر Month است).
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccPeriod (
    PeriodID       INTEGER PRIMARY KEY AUTOINCREMENT,
    Year           INTEGER NOT NULL,
    Month          INTEGER NOT NULL,          -- برج شروع ۱..۱۲
    MonthTo        INTEGER NOT NULL DEFAULT 0, -- برج پایان (چندماهه)؛ ۰ یعنی = Month
    Title          TEXT    NULL,              -- مثل: برج اسد ۱۴۰۴
    StartDate      TEXT    NULL,
    EndDate        TEXT    NULL,
    OpeningBalance REAL    NOT NULL DEFAULT 0, -- مانده ابتدای دوره (افغانی)
    Status         TEXT    NOT NULL DEFAULT 'باز', -- باز / بسته
    CenterID       INTEGER NULL,
    CreatedBy      TEXT    NULL,
    CreatedAt      TEXT    NOT NULL DEFAULT (datetime('now'))
);");
                EnsureColumn(con, "AccPeriod", "MonthTo", "INTEGER NOT NULL DEFAULT 0");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AccPeriod_YM ON AccPeriod(Year, Month);");

                // ─── صندوق (نقدی/بانک/کردیت/مرکزی) ───────────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccFund (
    FundID         INTEGER PRIMARY KEY AUTOINCREMENT,
    Name           TEXT    NOT NULL,
    FundType       TEXT    NULL,              -- نقدی / بانک / کردیت / مرکزی
    OpeningBalance REAL    NOT NULL DEFAULT 0,
    IsActive       INTEGER NOT NULL DEFAULT 1,
    CenterID       INTEGER NULL,
    CreatedAt      TEXT    NOT NULL DEFAULT (datetime('now'))
);");

                // ─── طرف حساب ────────────────────────────────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccParty (
    PartyID    INTEGER PRIMARY KEY AUTOINCREMENT,
    Name       TEXT    NOT NULL,
    PartyType  TEXT    NULL,   -- خیر/دفتر مرکزی/ولایت/ولسوالی/مرکز/کارمند/فروشنده/شخص
    Phone      TEXT    NULL,
    Note       TEXT    NULL,
    IsActive   INTEGER NOT NULL DEFAULT 1,
    CenterID   INTEGER NULL,
    CreatedAt  TEXT    NOT NULL DEFAULT (datetime('now'))
);");

                // ─── دسته‌بندی درآمد ─────────────────────────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccIncomeCategory (
    CatID     INTEGER PRIMARY KEY AUTOINCREMENT,
    Name      TEXT    NOT NULL UNIQUE,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    IsActive  INTEGER NOT NULL DEFAULT 1
);");

                // ─── دسته‌بندی هزینه ─────────────────────────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccExpenseCategory (
    CatID     INTEGER PRIMARY KEY AUTOINCREMENT,
    Name      TEXT    NOT NULL UNIQUE,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    IsActive  INTEGER NOT NULL DEFAULT 1
);");

                // ─── تراکنش (دریافت/پرداخت) — دفتر صندوق ──────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccTransaction (
    TxnID         INTEGER PRIMARY KEY AUTOINCREMENT,
    DocNo         TEXT    NULL,               -- شماره سند
    TxnDate       TEXT    NOT NULL,           -- تاریخ شمسی متن yyyy/MM/dd
    Direction     TEXT    NOT NULL,           -- دریافت / پرداخت
    PeriodID      INTEGER NULL,
    PartyID       INTEGER NULL,
    FundID        INTEGER NULL,
    CategoryType  TEXT    NULL,               -- Income / Expense
    CategoryID    INTEGER NULL,
    Amount        REAL    NOT NULL DEFAULT 0, -- مبلغ افغانی
    Qty           TEXT    NULL,               -- تعداد/مقدار (متن آزاد مثل «۹ وعده»)
    DollarAmount  REAL    NULL,               -- برای دریافت بودجه دلاری
    DollarRate    REAL    NULL,               -- نرخ دلار به افغانی
    Description   TEXT    NULL,
    AttachmentPath TEXT   NULL,
    CenterID      INTEGER NULL,
    CreatedBy     TEXT    NULL,
    CreatedAt     TEXT    NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AccTxn_Period ON AccTransaction(PeriodID);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AccTxn_Fund ON AccTransaction(FundID);");

                // ─── شهریه ایتام (جزئیات مطابق شیت «فرمت جزیی») ────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccStipend (
    StipendID     INTEGER PRIMARY KEY AUTOINCREMENT,
    PeriodID      INTEGER NULL,
    Province      TEXT    NULL,   -- ولایت
    District      TEXT    NULL,   -- ولسوالی
    Center        TEXT    NULL,   -- مرکز
    SadatType     TEXT    NOT NULL, -- عام / سادات / اهل سنت / غیرحاضران
    FamilySize    INTEGER NOT NULL, -- چند نفره (۱..۸)
    FamilyCount   INTEGER NOT NULL DEFAULT 0, -- تعداد خانوار
    OrphanCount   INTEGER NOT NULL DEFAULT 0, -- تعداد یتیم
    AmountPerFamily REAL  NOT NULL DEFAULT 0, -- مبلغ شهریه هر خانواده
    TotalPaid     REAL    NOT NULL DEFAULT 0, -- جمع پرداختی (خودکار)
    CenterID      INTEGER NULL,
    CreatedAt     TEXT    NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AccStipend_Period ON AccStipend(PeriodID);");

                // ─── حقوق کارکنان ────────────────────────────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccSalary (
    SalaryID     INTEGER PRIMARY KEY AUTOINCREMENT,
    PeriodID     INTEGER NULL,
    EmployeeName TEXT    NOT NULL,  -- نام
    Position     TEXT    NULL,      -- سمت
    Amount       REAL    NOT NULL DEFAULT 0,
    Note         TEXT    NULL,
    CenterID     INTEGER NULL,
    CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AccSalary_Period ON AccSalary(PeriodID);");

                // ─── هزینه‌های جاری / اقلام هزینه (شیت «حساب جاری») ───────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccExpenseItem (
    ItemID       INTEGER PRIMARY KEY AUTOINCREMENT,
    PeriodID     INTEGER NULL,
    CategoryID   INTEGER NULL,      -- دسته‌بندی هزینه
    CategoryName TEXT    NULL,      -- برای گزارش (کش‌شده)
    Description  TEXT    NULL,      -- شرح
    Qty          TEXT    NULL,      -- تعداد/مقدار (متن)
    Price        REAL    NOT NULL DEFAULT 0, -- قیمت/مبلغ
    DocNo        TEXT    NULL,      -- شماره سند
    ItemDate     TEXT    NULL,      -- تاریخ
    CenterID     INTEGER NULL,
    CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AccExpItem_Period ON AccExpenseItem(PeriodID);");

                // ─── تنظیمات گزارش (کلید/مقدار) ──────────────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccSettings (
    SettingKey   TEXT PRIMARY KEY,
    SettingValue TEXT NULL,
    UpdatedAt    TEXT NOT NULL DEFAULT (datetime('now'))
);");

                // ─── Audit مخصوص حسابداری ────────────────────────────────────
                Exec(con, @"
CREATE TABLE IF NOT EXISTS AccAudit (
    AuditID    INTEGER PRIMARY KEY AUTOINCREMENT,
    Operation  TEXT NULL,   -- ثبت/ویرایش/حذف
    EntityName TEXT NULL,
    EntityID   INTEGER NULL,
    Detail     TEXT NULL,
    Username   TEXT NULL,
    MachineName TEXT NULL,
    IPAddress  TEXT NULL,
    CenterID   INTEGER NULL,
    CreatedAt  TEXT NOT NULL DEFAULT (datetime('now'))
);");

                SeedDefaults(con);
            }
        }

        private static void SeedDefaults(SQLiteConnection con)
        {
            // دسته‌های درآمد پیش‌فرض
            SeedCategory(con, "AccIncomeCategory", new[] { "بودجه", "کمک خیرین", "انتقال", "برگشت وجه", "سایر" });

            // دسته‌های هزینه پیش‌فرض
            SeedCategory(con, "AccExpenseCategory", new[]
            {
                "شهریه ایتام عام", "شهریه ایتام سادات", "شهریه ایتام اهل سنت",
                "ترانسپورت", "غذا", "حقوق", "دستمزد", "خرید تجهیزات",
                "هزینه های جاری", "کرایه ساختمان", "لوازم و تجهیزات", "هزینه های متفرقه"
            });

            // صندوق‌های پیش‌فرض (فقط اگر هیچ صندوقی نیست)
            if (Count(con, "AccFund") == 0)
            {
                foreach (var f in new[]
                {
                    new[] { "صندوق نقدی", "نقدی" },
                    new[] { "بانک", "بانک" },
                    new[] { "کردیت", "کردیت" },
                    new[] { "صندوق مرکزی", "مرکزی" }
                })
                {
                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO AccFund (Name, FundType) VALUES (@n, @t)", con))
                    {
                        cmd.Parameters.AddWithValue("@n", f[0]);
                        cmd.Parameters.AddWithValue("@t", f[1]);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            // تنظیمات گزارش پیش‌فرض (از تنظیمات موجود مؤسسه اگر باشد)
            SeedSetting(con, "OrgName", SettingsHelper.Get(SettingsHelper.OrgName));
            SeedSetting(con, "HeaderText", "");
            SeedSetting(con, "FooterText", "");
            SeedSetting(con, "AccountantSignature", "");
            SeedSetting(con, "ManagerSignature", "");
            SeedSetting(con, "PreparerSignature", "");
            SeedSetting(con, "LogoPath", SettingsHelper.Get(SettingsHelper.LogoPath));
            SeedSetting(con, "StampPath", "");
        }

        private static void SeedCategory(SQLiteConnection con, string table, string[] names)
        {
            int order = 0;
            foreach (string name in names)
            {
                using (var cmd = new SQLiteCommand(
                    "INSERT OR IGNORE INTO " + table + " (Name, SortOrder) VALUES (@n, @o)", con))
                {
                    cmd.Parameters.AddWithValue("@n", name);
                    cmd.Parameters.AddWithValue("@o", order++);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void SeedSetting(SQLiteConnection con, string key, string value)
        {
            using (var cmd = new SQLiteCommand(
                "INSERT OR IGNORE INTO AccSettings (SettingKey, SettingValue) VALUES (@k, @v)", con))
            {
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", (object)(value ?? "") );
                cmd.ExecuteNonQuery();
            }
        }

        private static int Count(SQLiteConnection con, string table)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(1) FROM " + table, con))
                return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static void Exec(SQLiteConnection con, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, con))
                cmd.ExecuteNonQuery();
        }

        // مهاجرت ایمن ستون جدید روی دیتابیس‌های قبلاً ساخته‌شده (مطابق الگوی DatabaseInitializer.EnsureColumn)
        private static void EnsureColumn(SQLiteConnection con, string tableName, string columnName, string columnDef)
        {
            bool exists = false;
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + tableName + ")", con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }
            if (!exists)
                Exec(con, "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + columnDef);
        }
    }
}

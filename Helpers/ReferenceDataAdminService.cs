using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // یک ردیفِ مرجع، آماده برای نمایش در جدولِ تنظیمات.
    public sealed class ReferenceRow
    {
        public int    ID;
        public string Code;
        public string Name;
        public string DefaultName;   // مقدارِ اولیهٔ سیستم ("" برای ردیف‌های افزوده)
        public bool   IsBuiltIn;
        public bool   IsActive;
        public int    SortOrder;
        public int    UsageCount;    // چند پرونده از این مقدار استفاده می‌کنند
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Feature 1 — مدیریتِ «نوع پرونده» و «وضعیت خدمات».
    //
    // تنها نویسندهٔ TblRequestType/TblServiceStatus از سمتِ کاربر. فرم فقط
    // این کلاس را صدا می‌زند تا قواعدِ ایمنی در یک جا بماند.
    //
    // ⚠ قاعدهٔ بنیادی: ستونِ `Code` هویتِ برنامه‌ای است — منطقِ کد با آن
    // مقایسه می‌کند (`"DISABLED"`, `"ACTIVE"`, …) و `Name` فقط نمایشی است.
    // پس:
    //   • Codeِ ردیف‌های داخلی هرگز تغییر نمی‌کند و ردیفشان حذف نمی‌شود؛
    //   • تنها چیزی که برای آن‌ها قابلِ تغییر است `Name` و `IsActive` است؛
    //   • «حذف» اصلاً وجود ندارد — دادهٔ مرجع فقط غیرفعال می‌شود
    //     (همان قاعده‌ای که PROJECT_CONTEXT برای دادهٔ مرجع تعریف کرده).
    // ═══════════════════════════════════════════════════════════════════════
    public static class ReferenceDataAdminService
    {
        public const string TableRequestType   = "TblRequestType";
        public const string TableServiceStatus = "TblServiceStatus";

        public const string PermissionKey = "Settings.Manage";

        // مقادیرِ اولیهٔ سیستم — همان چیزی که DatabaseInitializer seed می‌کند.
        // اینجا تکرار شده‌اند چون «بازگردانی به پیش‌فرض» و متنِ راهنما باید
        // بدانند مقدارِ اصلی چه بوده، حتی وقتی کاربر آن را عوض کرده است.
        private static readonly Dictionary<string, string> DefaultRequestTypes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "ORPHAN",                "ایتام"     },
                { "UNSUPPORTED_CHILD",     "بی‌سرپرست"  },
                { "BADLY_SUPPORTED_CHILD", "بدسرپرست"  },
                { "DISABLED",              "معلول"     },
                { "MIGRANT",               "مهاجر"     },
                { "ELDERLY",               "کهن‌سال"    }
            };

        private static readonly Dictionary<string, string> DefaultServiceStatuses =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "APPLICANT",             "متقاضی"          },
                { "UNDER_REVIEW",          "در حال بررسی"    },
                { "PENDING_APPROVAL",      "در انتظار تایید" },
                { "ACTIVE",                "فعال"            },
                { "TEMPORARILY_SUSPENDED", "قطع موقت"        },
                { "SUSPENDED",             "قطع"             }
            };

        private static Dictionary<string, string> DefaultsFor(string table)
        {
            if (string.Equals(table, TableRequestType, StringComparison.OrdinalIgnoreCase))
                return DefaultRequestTypes;
            if (string.Equals(table, TableServiceStatus, StringComparison.OrdinalIgnoreCase))
                return DefaultServiceStatuses;
            throw new ArgumentException("جدولِ مرجعِ ناشناخته: " + table);
        }

        public static string PrimaryKeyOf(string table)
        {
            if (string.Equals(table, TableRequestType, StringComparison.OrdinalIgnoreCase))
                return "RequestTypeID";
            if (string.Equals(table, TableServiceStatus, StringComparison.OrdinalIgnoreCase))
                return "ServiceStatusID";
            throw new ArgumentException("جدولِ مرجعِ ناشناخته: " + table);
        }

        // ستونِ متنیِ TblCase که این جدولِ مرجع را آینه می‌کند — برای شمردنِ
        // «چند پرونده از این مقدار استفاده می‌کند».
        private static string UsageColumnOf(string table)
        {
            return string.Equals(table, TableRequestType, StringComparison.OrdinalIgnoreCase)
                ? "RequestType" : "ServiceStatus";
        }

        public static bool IsBuiltIn(string table, string code)
        {
            return DefaultsFor(table).ContainsKey((code ?? "").Trim());
        }

        public static string DefaultNameFor(string table, string code)
        {
            string name;
            return DefaultsFor(table).TryGetValue((code ?? "").Trim(), out name) ? name : "";
        }

        public static bool CanModify()
        {
            return Enterprise.PermissionService.HasPermission(PermissionKey);
        }

        // ─── خواندن ──────────────────────────────────────────────────────────
        public static List<ReferenceRow> Load(string table)
        {
            string pk = PrimaryKeyOf(table);
            string usageColumn = UsageColumnOf(table);
            var result = new List<ReferenceRow>();

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT r." + pk + " AS Id, r.Code, r.Name, r.IsActive, r.SortOrder, " +
                "       (SELECT COUNT(*) FROM TblCase c WHERE c." + usageColumn + " = r.Name) AS UsageCount " +
                "FROM " + table + " r ORDER BY r.SortOrder, r." + pk + ";", con))
            {
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        string code = dr["Code"].ToString();
                        result.Add(new ReferenceRow
                        {
                            ID          = Convert.ToInt32(dr["Id"]),
                            Code        = code,
                            Name        = dr["Name"].ToString(),
                            DefaultName = DefaultNameFor(table, code),
                            IsBuiltIn   = IsBuiltIn(table, code),
                            IsActive    = Convert.ToInt32(dr["IsActive"]) != 0,
                            SortOrder   = Convert.ToInt32(dr["SortOrder"]),
                            UsageCount  = Convert.ToInt32(dr["UsageCount"])
                        });
                    }
                }
            }

            return result;
        }

        // ─── تغییرِ نامِ نمایشی ───────────────────────────────────────────────
        // Code دست‌نخورده می‌ماند؛ فقط متنی که کاربر می‌بیند عوض می‌شود.
        //
        // ⚠ ستون‌های متنیِ TblCase (dual-write) مقدارِ *نام* را نگه می‌دارند،
        // پس تغییرِ نام بدونِ به‌روزرسانیِ آن‌ها یعنی پرونده‌های موجود به
        // مقداری اشاره می‌کنند که دیگر در فهرست نیست. هر دو با هم و در یک
        // تراکنش انجام می‌شوند.
        public static void Rename(string table, int id, string newName)
        {
            Require();

            string name = (newName ?? "").Trim();
            if (name.Length == 0) throw new ArgumentException("نام نمی‌تواند خالی باشد.");

            string pk = PrimaryKeyOf(table);
            string usageColumn = UsageColumnOf(table);
            string oldName = null, code = null;

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                using (var cmd = new SQLiteCommand(
                    "SELECT Code, Name FROM " + table + " WHERE " + pk + " = @Id;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) throw new InvalidOperationException("ردیف پیدا نشد.");
                        code    = dr["Code"].ToString();
                        oldName = dr["Name"].ToString();
                    }
                }

                if (string.Equals(oldName, name, StringComparison.Ordinal)) return;

                if (NameExists(con, table, name, id))
                    throw new InvalidOperationException("نامِ «" + name + "» از قبل استفاده شده است.");

                using (var tr = con.BeginTransaction())
                {
                    using (var cmd = new SQLiteCommand(
                        "UPDATE " + table + " SET Name = @Name WHERE " + pk + " = @Id;", con))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }

                    // پرونده‌هایی که نامِ قدیمی را نگه داشته‌اند با هم منتقل
                    // می‌شوند، وگرنه گزارش/جستجو/داشبورد مقداری را نشان
                    // می‌دادند که دیگر در هیچ فهرستی نیست.
                    using (var cmd = new SQLiteCommand(
                        "UPDATE TblCase SET " + usageColumn + " = @New WHERE " + usageColumn + " = @Old;", con))
                    {
                        cmd.Parameters.AddWithValue("@New", name);
                        cmd.Parameters.AddWithValue("@Old", oldName);
                        cmd.ExecuteNonQuery();
                    }

                    tr.Commit();
                }
            }

            ReferenceDataService.ClearCache();
            LookupHelper.ClearCache();
            AuditLogger.Log("تغییر نام دادهٔ مرجع", table, id, oldName, name + " (Code=" + code + ")");
        }

        // ─── فعال/غیرفعال ────────────────────────────────────────────────────
        // «حذف» عمداً وجود ندارد. غیرفعال‌کردن مقدار را از فهرست‌های ورودی
        // برمی‌دارد ولی پرونده‌های موجود دست‌نخورده می‌مانند.
        public static void SetActive(string table, int id, bool active)
        {
            Require();

            string pk = PrimaryKeyOf(table);
            string code = null, name = null;
            bool wasActive = false;

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                using (var cmd = new SQLiteCommand(
                    "SELECT Code, Name, IsActive FROM " + table + " WHERE " + pk + " = @Id;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) throw new InvalidOperationException("ردیف پیدا نشد.");
                        code      = dr["Code"].ToString();
                        name      = dr["Name"].ToString();
                        wasActive = Convert.ToInt32(dr["IsActive"]) != 0;
                    }
                }

                if (wasActive == active) return;

                using (var cmd = new SQLiteCommand(
                    "UPDATE " + table + " SET IsActive = @A WHERE " + pk + " = @Id;", con))
                {
                    cmd.Parameters.AddWithValue("@A", active ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            ReferenceDataService.ClearCache();
            LookupHelper.ClearCache();
            AuditLogger.Log(active ? "فعال‌سازی دادهٔ مرجع" : "غیرفعال‌سازی دادهٔ مرجع",
                table, id, wasActive ? "فعال" : "غیرفعال", (active ? "فعال" : "غیرفعال") +
                " — " + name + " (Code=" + code + ")");
        }

        // ─── افزودنِ مقدارِ تازه ──────────────────────────────────────────────
        // Code توسطِ کاربر داده نمی‌شود: از روی نام یک شناسهٔ ASCII ساخته
        // می‌شود با پیشوندِ CUSTOM_ تا هرگز با کدهای داخلی (که منطقِ برنامه
        // با آن‌ها مقایسه می‌کند) اشتباه گرفته نشود.
        public static int Add(string table, string name)
        {
            Require();

            string display = (name ?? "").Trim();
            if (display.Length == 0) throw new ArgumentException("نام نمی‌تواند خالی باشد.");

            string code = "CUSTOM_" + Math.Abs(display.GetHashCode()).ToString();

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                if (NameExists(con, table, display, 0))
                    throw new InvalidOperationException("نامِ «" + display + "» از قبل وجود دارد.");

                int nextSort;
                using (var cmd = new SQLiteCommand(
                    "SELECT IFNULL(MAX(SortOrder), 0) + 1 FROM " + table + ";", con))
                    nextSort = Convert.ToInt32(cmd.ExecuteScalar());

                using (var cmd = new SQLiteCommand(
                    "INSERT INTO " + table + " (Code, Name, SortOrder, IsActive) " +
                    "VALUES (@Code, @Name, @Sort, 1); SELECT last_insert_rowid();", con))
                {
                    cmd.Parameters.AddWithValue("@Code", code);
                    cmd.Parameters.AddWithValue("@Name", display);
                    cmd.Parameters.AddWithValue("@Sort", nextSort);

                    int newId = Convert.ToInt32((long)cmd.ExecuteScalar());

                    ReferenceDataService.ClearCache();
                    LookupHelper.ClearCache();
                    AuditLogger.Log("افزودن دادهٔ مرجع", table, newId, "",
                        display + " (Code=" + code + ")");
                    return newId;
                }
            }
        }

        // ─── بازگردانی به مقادیرِ پیش‌فرض ────────────────────────────────────
        // نامِ همهٔ ردیف‌های داخلی به مقدارِ اولیه برمی‌گردد و دوباره فعال
        // می‌شوند. ردیف‌هایی که کاربر خودش افزوده **دست‌نخورده** می‌مانند —
        // بازگردانیِ پیش‌فرض یعنی «سیستم را به حالتِ اولش برگردان»، نه
        // «کارِ کاربر را پاک کن».
        public static int RestoreDefaults(string table)
        {
            Require();

            int changed = 0;
            foreach (ReferenceRow row in Load(table))
            {
                if (!row.IsBuiltIn) continue;

                if (!string.Equals(row.Name, row.DefaultName, StringComparison.Ordinal))
                {
                    Rename(table, row.ID, row.DefaultName);
                    changed++;
                }

                if (!row.IsActive)
                {
                    SetActive(table, row.ID, true);
                    changed++;
                }
            }

            AuditLogger.Log("بازگردانی دادهٔ مرجع به پیش‌فرض", table, 0, "",
                "تعداد تغییر: " + changed);
            return changed;
        }

        private static bool NameExists(SQLiteConnection con, string table, string name, int exceptId)
        {
            string pk = PrimaryKeyOf(table);
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM " + table + " WHERE Name = @Name AND " + pk + " <> @Id;", con))
            {
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Id", exceptId);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static void Require()
        {
            if (!CanModify())
                throw new UnauthorizedAccessException(
                    "شما اجازهٔ تغییرِ «نوع پرونده» و «وضعیت خدمات» را ندارید.");
        }
    }
}

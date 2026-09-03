using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // «آخرین نسخهٔ پذیرفته‌شده» — نگهدارندهٔ مبنای واقعیِ ادغام سه‌طرفه.
    //
    // نقش: برای هر رکورد، آخرین باری را نگه می‌دارد که این شعبه و سرور روی آن
    // توافق داشتند. تحلیل‌گر تعارض با داشتنِ آن می‌تواند «چه کسی چه چیزی را
    // عوض کرده» را درست تشخیص دهد، به‌جای حدس زدن از روی تاریخچهٔ محلی.
    //
    // ⚠ سه قاعده، هم‌سو با بقیهٔ لایهٔ همگام‌سازی:
    //
    // ۱. هرگز استثنا به فراخوان نمی‌دهد. شکستِ ثبتِ مبنا نباید یک همگام‌سازیِ
    //    موفق را خراب کند؛ بدترین اثرش این است که تحلیلِ بعدی محافظه‌کارتر
    //    می‌شود (تعارض به مدیر می‌رود) — یعنی خطا همیشه به سمتِ ایمنی است.
    //
    // ۲. «آخرین حرف برنده است». یک ردیف برای هر رکورد؛ ثبتِ تازه جای قبلی را
    //    می‌گیرد. این جدول تاریخچه نیست و نباید بشود.
    //
    // ۳. قالبِ بار دقیقاً همان «کلید=مقدار» است که SyncOutbox و SyncApplier
    //    استفاده می‌کنند، پس بدون هیچ تبدیلی با هر دو طرف مقایسه‌شدنی است.
    // ═════════════════════════════════════════════════════════════════════════
    public static class SyncBaselineStore
    {
        private static readonly DatabaseHelper Db = new DatabaseHelper();

        // ─────────────────────────────────────────────────────────────────────
        // ثبت/به‌روزرسانیِ مبنا پس از یک تبادلِ *موفق* با سرور.
        // ─────────────────────────────────────────────────────────────────────
        public static void Set(string entityName, string globalId, string payload,
                               int rowVersion, string source)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityName) ||
                    string.IsNullOrWhiteSpace(globalId) ||
                    string.IsNullOrEmpty(payload)) return;

                if (!TableExists()) return;

                Db.ExecuteNonQuery(@"
INSERT INTO SyncBaseline (EntityName, EntityGlobalID, RowVersion, Payload, Source, UpdatedAt)
VALUES (@E, @G, @V, @P, @S, datetime('now'))
ON CONFLICT(EntityName, EntityGlobalID) DO UPDATE SET
    RowVersion = @V,
    Payload    = @P,
    Source     = @S,
    UpdatedAt  = datetime('now');",
                    new SQLiteParameter("@E", entityName),
                    new SQLiteParameter("@G", globalId),
                    new SQLiteParameter("@V", rowVersion < 0 ? 0 : rowVersion),
                    new SQLiteParameter("@P", payload),
                    new SQLiteParameter("@S", (object)source ?? DBNull.Value));
            }
            catch (Exception ex) { Swallow(ex, "Set/" + entityName); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // خواندنِ مبنا. null یعنی «برای این رکورد هنوز نقطهٔ توافقی ثبت نشده».
        //
        // ⚠ null با «مبنای خالی» فرق دارد: تحلیل‌گر در نبودِ مبنا محافظه‌کار
        // می‌شود و هیچ ادغامِ خودکاری انجام نمی‌دهد. پس هرگز به‌جای null یک
        // فرهنگِ خالی برنمی‌گردانیم.
        // ─────────────────────────────────────────────────────────────────────
        public static Dictionary<string, string> Get(string entityName, string globalId)
        {
            string payload = GetPayload(entityName, globalId);
            if (string.IsNullOrEmpty(payload)) return null;

            Dictionary<string, string> values = SyncApplier.Deserialize(payload);
            return values.Count == 0 ? null : values;
        }

        public static string GetPayload(string entityName, string globalId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityName) ||
                    string.IsNullOrWhiteSpace(globalId) || !TableExists()) return null;

                object value = Db.ExecuteScalar(
                    "SELECT Payload FROM SyncBaseline WHERE EntityName = @E AND EntityGlobalID = @G LIMIT 1;",
                    new SQLiteParameter("@E", entityName),
                    new SQLiteParameter("@G", globalId));

                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
            catch (Exception ex) { Swallow(ex, "GetPayload/" + entityName); return null; }
        }

        // شمارِ ردیف‌ها — برای گزارشِ تشخیصیِ مرکز کنترل.
        public static int Count()
        {
            try
            {
                if (!TableExists()) return 0;

                object value = Db.ExecuteScalar("SELECT COUNT(1) FROM SyncBaseline;");
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
            catch { return 0; }
        }

        // ─────────────────────────────────────────────────────────────────────
        private static bool TableExists()
        {
            try
            {
                object value = Db.ExecuteScalar(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='SyncBaseline';");

                return value != null && value != DBNull.Value && Convert.ToInt32(value) > 0;
            }
            catch { return false; }
        }

        private static void Swallow(Exception ex, string source)
        {
            try { Enterprise.ErrorLogger.Log(ex, "SyncBaselineStore." + source); } catch { }
        }
    }
}

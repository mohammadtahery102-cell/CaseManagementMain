using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // بازپرِ تاریخیِ صفِ همگام‌سازی — فاز «رفعِ همگام‌سازی».
    //
    // چرا لازم است: BackupHelper.ImportBackup پیش از این تغییر هرگز
    // SyncOutboxService.Capture را صدا نمی‌زد (رفعِ آن در همین فاز، جدا). یعنی
    // هر پرونده/عضو/کاربری که تا امروز از راهِ «بازیابیِ بکاپِ دفترِ دیگر»
    // وارد این نصب شده، در دیتابیسِ محلی هست ولی هیچ‌وقت واردِ SyncOutbox نشده
    // و هرگز به سرور نخواهد رسید — even پس از پیکربندیِ کاملِ سرور.
    //
    // این سرویس یک‌بار، دستی و از طریقِ UI اجرا می‌شود (نه خودکار، نه هنگامِ
    // راه‌اندازی) و برای هر موجودیتِ همگام‌سازی‌شونده، رکوردهایی را پیدا می‌کند
    // که هیچ ردیفی در SyncOutbox ندارند (نه Pending، نه Sent، نه هیچ حالتِ
    // دیگر) و برایشان Capture(...، OperationCreate) صدا می‌زند.
    //
    // ⚠ Idempotent بودن با ساختار، نه با یک جدولِ Checkpoint جدا: شرطِ انتخاب
    // («هیچ ردیفی در SyncOutbox ندارد») بلافاصله بعدِ اولین اجرا برای همان
    // رکورد دیگر برقرار نیست، پس اجرای دوباره (یا توقف/ازسرگیری) کاملاً بی‌خطر
    // است — نه تکراری می‌سازد، نه رکوردی را جا می‌اندازد.
    // ═════════════════════════════════════════════════════════════════════════
    public static class SyncBackfillService
    {
        public class EntityBacklog
        {
            public string EntityName;
            public int MissingCount;
        }

        public class EntityResult
        {
            public string EntityName;
            public int Captured;
        }

        // پیش‌نمایش بدونِ نوشتن — برای نمایش «چند رکورد صف می‌شود» پیش از تأیید.
        public static List<EntityBacklog> Preview()
        {
            var list = new List<EntityBacklog>();

            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                foreach (string[] entity in OfflineSyncInitializer.SyncedTables)
                {
                    string table = entity[0];
                    string pk = entity[1];

                    if (!TableExists(con, table)) continue;

                    int missing = Convert.ToInt32(ExecuteScalar(con,
                        "SELECT COUNT(1) FROM " + table + " t " +
                        "WHERE NOT EXISTS (SELECT 1 FROM SyncOutbox o " +
                        "WHERE o.EntityName = @E AND o.EntityLocalID = t.[" + pk + "]);",
                        new SQLiteParameter("@E", table)));

                    if (missing > 0)
                        list.Add(new EntityBacklog { EntityName = table, MissingCount = missing });
                }
            }

            return list;
        }

        // اجرای واقعی. progress (اختیاری) پس از هر موجودیت صدا زده می‌شود، نه
        // پس از هر رکورد — برای هزاران رکورد گزارشِ ردیف‌به‌ردیف فقط UI را کند
        // می‌کند بدونِ فایدهٔ واقعی.
        public static List<EntityResult> Run(Action<string> progress = null)
        {
            var results = new List<EntityResult>();

            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                foreach (string[] entity in OfflineSyncInitializer.SyncedTables)
                {
                    string table = entity[0];
                    string pk = entity[1];

                    if (!TableExists(con, table)) continue;

                    var missingIds = new List<int>();
                    using (var cmd = new SQLiteCommand(
                        "SELECT t.[" + pk + "] FROM " + table + " t " +
                        "WHERE NOT EXISTS (SELECT 1 FROM SyncOutbox o " +
                        "WHERE o.EntityName = @E AND o.EntityLocalID = t.[" + pk + "]);", con))
                    {
                        cmd.Parameters.AddWithValue("@E", table);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                missingIds.Add(Convert.ToInt32(reader[0]));
                        }
                    }

                    int captured = 0;
                    foreach (int id in missingIds)
                    {
                        // Capture خودش هر خطا را می‌بلعد و SuperAdmin را رد
                        // می‌کند (IsUnsyncableUser) — همان رفتاری که همه‌جای
                        // برنامه از این متد انتظار دارند.
                        SyncOutboxService.Capture(table, id, OfflineSyncInitializer.OperationCreate);
                        captured++;
                    }

                    if (progress != null)
                        progress(table + ": " + captured + " رکورد به صفِ ارسال افزوده شد.");

                    results.Add(new EntityResult { EntityName = table, Captured = captured });
                }
            }

            return results;
        }

        private static bool TableExists(SQLiteConnection con, string tableName)
        {
            object val = ExecuteScalar(con,
                "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@N;",
                new SQLiteParameter("@N", tableName));
            return Convert.ToInt32(val) > 0;
        }

        private static object ExecuteScalar(SQLiteConnection con, string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = new SQLiteCommand(sql, con))
            {
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteScalar();
            }
        }
    }
}

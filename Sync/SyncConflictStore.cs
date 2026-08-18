using System;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // انبارِ تعارض‌ها — نقطهٔ اتصالِ آمادهٔ فاز ۴.
    //
    // آموزش — در این فاز عمداً *هیچ تعارضی حل نمی‌شود*. دلیلش این نیست که
    // سخت است؛ دلیلش این است که حل خودکارِ تعارض بدون سیاستِ تأییدشده،
    // دادهٔ درستِ یک کاربر را بی‌صدا با دادهٔ کاربر دیگر جایگزین می‌کند و
    // برگشت‌ناپذیر است. پس اینجا فقط «ثبت و کنار گذاشتن» انجام می‌شود:
    // رکورد محلی دست‌نخورده می‌ماند، تغییر سرور اعمال نمی‌شود، و هر دو نسخه
    // برای داوریِ فاز ۴ نگه داشته می‌شوند.
    // ═════════════════════════════════════════════════════════════════════════
    public static class SyncConflictStore
    {
        private static readonly DatabaseHelper Db = new DatabaseHelper();

        public static long Record(string entityName, string globalId, string conflictType,
                                  long outboxId, int localVersion, int remoteVersion,
                                  string localPayload, string remotePayload)
        {
            try
            {
                if (!TableExists()) return 0;

                // یک تعارضِ بازِ تکراری برای همان رکورد ساخته نمی‌شود؛ در عوض
                // آخرین وضعیتِ طرفین به‌روزرسانی می‌گردد. بدون این، هر تلاشِ
                // ناموفقِ همگام‌سازی یک ردیف تعارضِ تازه می‌ساخت و فهرست
                // بازبینیِ مدیر بی‌استفاده می‌شد.
                object existing = Db.ExecuteScalar(
                    "SELECT ConflictID FROM SyncConflict " +
                    "WHERE EntityGlobalID = @G AND EntityName = @E AND Status = @S LIMIT 1;",
                    new SQLiteParameter("@G", globalId ?? ""),
                    new SQLiteParameter("@E", entityName ?? ""),
                    new SQLiteParameter("@S", OfflineSyncInitializer.ConflictOpen));

                if (existing != null && existing != DBNull.Value)
                {
                    long id = Convert.ToInt64(existing);

                    Db.ExecuteNonQuery(@"
UPDATE SyncConflict
SET ConflictType = @T, OutboxID = @O, LocalVersion = @LV, RemoteVersion = @RV,
    LocalPayload = @LP, RemotePayload = @RP, DetectedAt = datetime('now')
WHERE ConflictID = @Id;",
                        new SQLiteParameter("@T",  conflictType ?? ""),
                        new SQLiteParameter("@O",  outboxId > 0 ? (object)outboxId : DBNull.Value),
                        new SQLiteParameter("@LV", localVersion),
                        new SQLiteParameter("@RV", remoteVersion),
                        new SQLiteParameter("@LP", (object)localPayload ?? DBNull.Value),
                        new SQLiteParameter("@RP", (object)remotePayload ?? DBNull.Value),
                        new SQLiteParameter("@Id", id));

                    return id;
                }

                return Db.ExecuteInsertReturningId(@"
INSERT INTO SyncConflict
    (EntityName, EntityGlobalID, ConflictType, OutboxID, LocalVersion, RemoteVersion,
     LocalPayload, RemotePayload, CenterID, DetectedBy, MachineName, Status)
VALUES
    (@E, @G, @T, @O, @LV, @RV, @LP, @RP, @C, @U, @M, @S);",
                    new SQLiteParameter("@E",  entityName ?? ""),
                    new SQLiteParameter("@G",  globalId ?? ""),
                    new SQLiteParameter("@T",  conflictType ?? ""),
                    new SQLiteParameter("@O",  outboxId > 0 ? (object)outboxId : DBNull.Value),
                    new SQLiteParameter("@LV", localVersion),
                    new SQLiteParameter("@RV", remoteVersion),
                    new SQLiteParameter("@LP", (object)localPayload ?? DBNull.Value),
                    new SQLiteParameter("@RP", (object)remotePayload ?? DBNull.Value),
                    new SQLiteParameter("@C",  SecurityContext.CurrentCenterId > 0
                                                   ? (object)SecurityContext.CurrentCenterId : DBNull.Value),
                    new SQLiteParameter("@U",  (object)SecurityContext.Username ?? DBNull.Value),
                    new SQLiteParameter("@M",  SafeMachineName()),
                    new SQLiteParameter("@S",  OfflineSyncInitializer.ConflictOpen));
            }
            catch (Exception ex)
            {
                Swallow(ex, "Record");
                return 0;
            }
        }

        public static int OpenCount()
        {
            try
            {
                if (!TableExists()) return 0;

                object value = Db.ExecuteScalar(
                    "SELECT COUNT(1) FROM SyncConflict WHERE Status = @S;",
                    new SQLiteParameter("@S", OfflineSyncInitializer.ConflictOpen));

                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
            catch { return 0; }
        }

        public static DataTable GetOpen(int limit)
        {
            try
            {
                if (!TableExists()) return new DataTable();

                return Db.Query(
                    "SELECT * FROM SyncConflict WHERE Status = @S ORDER BY ConflictID LIMIT @L;",
                    new SQLiteParameter("@S", OfflineSyncInitializer.ConflictOpen),
                    new SQLiteParameter("@L", Math.Max(1, limit)));
            }
            catch { return new DataTable(); }
        }

        // برای فاز ۴ — امروز هیچ‌کس صدایش نمی‌زند.
        public static void MarkResolved(long conflictId, string resolution)
        {
            try
            {
                Db.ExecuteNonQuery(@"
UPDATE SyncConflict
SET Status = @S, Resolution = @R, ResolvedBy = @U, ResolvedAt = datetime('now')
WHERE ConflictID = @Id;",
                    new SQLiteParameter("@S",  OfflineSyncInitializer.ConflictResolved),
                    new SQLiteParameter("@R",  (object)resolution ?? DBNull.Value),
                    new SQLiteParameter("@U",  (object)SecurityContext.Username ?? DBNull.Value),
                    new SQLiteParameter("@Id", conflictId));
            }
            catch (Exception ex) { Swallow(ex, "MarkResolved"); }
        }

        private static bool TableExists()
        {
            try
            {
                object value = Db.ExecuteScalar(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='SyncConflict';");
                return value != null && value != DBNull.Value && Convert.ToInt32(value) > 0;
            }
            catch { return false; }
        }

        private static string SafeMachineName()
        {
            try { return Environment.MachineName; } catch { return ""; }
        }

        private static void Swallow(Exception ex, string source)
        {
            try { Enterprise.ErrorLogger.Log(ex, "SyncConflictStore." + source); } catch { }
        }
    }
}

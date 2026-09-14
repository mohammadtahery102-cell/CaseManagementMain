using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Sync
{
    // صف پایدار برای تغییراتی که والد محلی‌شان هنوز نرسیده.
    // نشانگر pull جلو می‌رود؛ این ردیف‌ها مستقل از نشانگر دوباره اعمال می‌شوند.
    // TTL و سقف تلاش جلوی رشد بی‌پایان را می‌گیرند؛ Purge برای پاکسازی مدیریتی است.
    public static class SyncDeferredApplyStore
    {
        public const int MaxAttemptCount = 20;
        public const int TtlDays = 14;

        private static readonly DatabaseHelper Db = new DatabaseHelper();

        public sealed class Counts
        {
            public int Pending;
            public int Exhausted;
            public int Expired;
            public int Total { get { return Pending + Exhausted + Expired; } }
        }

        public static void Enqueue(SyncChange change)
        {
            if (change == null || string.IsNullOrWhiteSpace(change.GlobalId))
                return;
            if (string.IsNullOrWhiteSpace(change.EntityName))
                return;

            try
            {
                Db.ExecuteNonQuery(@"
INSERT INTO SyncDeferredApply
    (EntityName, EntityGlobalID, ParentGlobalID, OperationType, RowVersion,
     Payload, CenterID, Username, MachineName, OccurredAt, SourceCursor,
     AttemptCount, LastError, LastAttemptAt)
VALUES
    (@E, @G, @P, @Op, @Ver, @Pay, @C, @U, @M, @At, @Cur, 0, NULL, NULL)
ON CONFLICT(EntityName, EntityGlobalID) DO UPDATE SET
    ParentGlobalID = excluded.ParentGlobalID,
    OperationType  = excluded.OperationType,
    RowVersion     = excluded.RowVersion,
    Payload        = excluded.Payload,
    CenterID       = excluded.CenterID,
    Username       = excluded.Username,
    MachineName    = excluded.MachineName,
    OccurredAt     = excluded.OccurredAt,
    SourceCursor   = excluded.SourceCursor;",
                    new SQLiteParameter("@E", change.EntityName),
                    new SQLiteParameter("@G", change.GlobalId),
                    new SQLiteParameter("@P", (object)change.ParentGlobalId ?? DBNull.Value),
                    new SQLiteParameter("@Op", (object)change.OperationType ?? DBNull.Value),
                    new SQLiteParameter("@Ver", Math.Max(1, change.RowVersion)),
                    new SQLiteParameter("@Pay", (object)change.Payload ?? DBNull.Value),
                    new SQLiteParameter("@C", change.CenterId),
                    new SQLiteParameter("@U", (object)change.Username ?? DBNull.Value),
                    new SQLiteParameter("@M", (object)change.MachineName ?? DBNull.Value),
                    new SQLiteParameter("@At", (object)change.OccurredAt ?? DBNull.Value),
                    new SQLiteParameter("@Cur", (object)change.Cursor ?? DBNull.Value));
            }
            catch
            {
                // جدول ممکن است هنوز ساخته نشده باشد؛ دریافت نباید بشکند.
            }
        }

        public static List<SyncChange> Retry()
        {
            int purged = PurgeStale();
            var applied = new List<SyncChange>();
            DataTable rows;
            try
            {
                rows = Db.Query(@"
SELECT * FROM SyncDeferredApply
WHERE AttemptCount < @max
  AND CreatedAt >= datetime('now', @ttl)
ORDER BY DeferredID;",
                    new SQLiteParameter("@max", MaxAttemptCount),
                    new SQLiteParameter("@ttl", "-" + TtlDays + " days"));
            }
            catch
            {
                Report(purged, 0, GetCounts());
                return applied;
            }

            if (rows.Rows.Count == 0)
            {
                Report(purged, 0, GetCounts());
                return applied;
            }

            bool progress;
            int safety = rows.Rows.Count + 2;
            do
            {
                progress = false;
                safety--;
                DataTable current;
                try
                {
                    current = Db.Query(@"
SELECT * FROM SyncDeferredApply
WHERE AttemptCount < @max
  AND CreatedAt >= datetime('now', @ttl)
ORDER BY DeferredID;",
                        new SQLiteParameter("@max", MaxAttemptCount),
                        new SQLiteParameter("@ttl", "-" + TtlDays + " days"));
                }
                catch { break; }

                foreach (DataRow row in current.Rows)
                {
                    SyncChange change = FromRow(row);
                    long deferredId = Convert.ToInt64(row["DeferredID"]);
                    try
                    {
                        SyncApplier.ApplyOutcome outcome = SyncApplier.Apply(change);
                        if (outcome == SyncApplier.ApplyOutcome.Deferred)
                        {
                            int attempts = Convert.ToInt32(row["AttemptCount"]) + 1;
                            Touch(deferredId, attempts, "والد هنوز نرسیده");
                            if (attempts >= MaxAttemptCount)
                                PurgeById(deferredId);
                            continue;
                        }

                        Remove(deferredId);
                        if (outcome == SyncApplier.ApplyOutcome.Inserted ||
                            outcome == SyncApplier.ApplyOutcome.Updated ||
                            outcome == SyncApplier.ApplyOutcome.Deleted)
                        {
                            applied.Add(change);
                            progress = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        int attempts = Convert.ToInt32(row["AttemptCount"]) + 1;
                        Touch(deferredId, attempts, ex.Message);
                        if (attempts >= MaxAttemptCount)
                            PurgeById(deferredId);
                    }
                }
            } while (progress && safety > 0);

            Report(purged, applied.Count, GetCounts());
            return applied;
        }

        public static int PurgeStale()
        {
            try
            {
                return Db.ExecuteNonQuery(@"
DELETE FROM SyncDeferredApply
WHERE AttemptCount >= @max
   OR CreatedAt < datetime('now', @ttl);",
                    new SQLiteParameter("@max", MaxAttemptCount),
                    new SQLiteParameter("@ttl", "-" + TtlDays + " days"));
            }
            catch { return 0; }
        }

        public static int PurgeAll()
        {
            try { return Db.ExecuteNonQuery("DELETE FROM SyncDeferredApply;"); }
            catch { return 0; }
        }

        public static int PurgeDeadlocked()
        {
            return PurgeStale();
        }

        public static int PendingCount()
        {
            return GetCounts().Pending;
        }

        public static Counts GetCounts()
        {
            var counts = new Counts();
            try
            {
                DataTable table = Db.Query(@"
SELECT
  SUM(CASE WHEN AttemptCount < @max AND CreatedAt >= datetime('now', @ttl) THEN 1 ELSE 0 END) AS Pending,
  SUM(CASE WHEN AttemptCount >= @max THEN 1 ELSE 0 END) AS Exhausted,
  SUM(CASE WHEN CreatedAt < datetime('now', @ttl) THEN 1 ELSE 0 END) AS Expired
FROM SyncDeferredApply;",
                    new SQLiteParameter("@max", MaxAttemptCount),
                    new SQLiteParameter("@ttl", "-" + TtlDays + " days"));
                if (table.Rows.Count == 0) return counts;
                counts.Pending = ToInt(table.Rows[0]["Pending"]);
                counts.Exhausted = ToInt(table.Rows[0]["Exhausted"]);
                counts.Expired = ToInt(table.Rows[0]["Expired"]);
            }
            catch { }
            return counts;
        }

        public static string FormatReport()
        {
            Counts counts = GetCounts();
            return "اعمال معوق: " + counts.Pending +
                   " در انتظار، " + counts.Exhausted +
                   " سقف تلاش، " + counts.Expired +
                   " منقضی (TTL " + TtlDays + " روز / حداکثر " + MaxAttemptCount + " تلاش)";
        }

        private static void Report(int purged, int applied, Counts remaining)
        {
            if (purged == 0 && applied == 0 && remaining.Total == 0)
                return;
            try
            {
                string message = "SyncDeferredApply purged=" + purged +
                    " applied=" + applied +
                    " pending=" + remaining.Pending +
                    " exhausted=" + remaining.Exhausted +
                    " expired=" + remaining.Expired;
                CaseManagement.Enterprise.ErrorLogger.LogMessage(
                    message, "SyncDeferredApply", null,
                    CaseManagement.Enterprise.ErrorLogger.SeverityWarning);
            }
            catch { }
        }

        private static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            return Convert.ToInt32(value);
        }

        private static SyncChange FromRow(DataRow row)
        {
            return new SyncChange
            {
                EntityName = Convert.ToString(row["EntityName"]),
                GlobalId = Convert.ToString(row["EntityGlobalID"]),
                ParentGlobalId = Convert.ToString(row["ParentGlobalID"]),
                OperationType = Convert.ToString(row["OperationType"]),
                RowVersion = row["RowVersion"] == DBNull.Value ? 1 : Convert.ToInt32(row["RowVersion"]),
                Payload = Convert.ToString(row["Payload"]),
                CenterId = row["CenterID"] == DBNull.Value ? 0 : Convert.ToInt32(row["CenterID"]),
                Username = Convert.ToString(row["Username"]),
                MachineName = Convert.ToString(row["MachineName"]),
                OccurredAt = Convert.ToString(row["OccurredAt"]),
                Cursor = Convert.ToString(row["SourceCursor"])
            };
        }

        private static void Remove(long deferredId)
        {
            PurgeById(deferredId);
        }

        private static void PurgeById(long deferredId)
        {
            Db.ExecuteNonQuery("DELETE FROM SyncDeferredApply WHERE DeferredID=@id;",
                new SQLiteParameter("@id", deferredId));
        }

        private static void Touch(long deferredId, int attempts, string error)
        {
            Db.ExecuteNonQuery(@"
UPDATE SyncDeferredApply
SET AttemptCount=@a, LastAttemptAt=datetime('now'), LastError=@e
WHERE DeferredID=@id;",
                new SQLiteParameter("@a", attempts),
                new SQLiteParameter("@e", (object)error ?? DBNull.Value),
                new SQLiteParameter("@id", deferredId));
        }
    }
}

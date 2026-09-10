using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;

namespace CaseManagement.Accounting.Ledger.Infrastructure
{
    public class AccOutboxStore
    {
        private readonly DatabaseHelper _db;

        public AccOutboxStore() : this(new DatabaseHelper()) { }

        public AccOutboxStore(DatabaseHelper db) { _db = db; }

        public IList<AccOutboxRow> ListDue(DateTime utcNow, int take)
        {
            string now = LedgerTime.UtcNow(utcNow);
            string stale = LedgerTime.UtcNow(utcNow.AddMinutes(-15));
            DataTable table = _db.Query(@"
SELECT * FROM AccOutbox
WHERE (Status IN (@p, @f) AND (NextAttemptAt IS NULL OR NextAttemptAt <= @now))
   OR (Status = @proc AND (LockedAt IS NULL OR LockedAt <= @stale))
ORDER BY OutboxID
LIMIT @n;",
                P("@p", LedgerCodes.OutboxPending),
                P("@f", LedgerCodes.OutboxFailed),
                P("@proc", LedgerCodes.OutboxProcessing),
                P("@now", now),
                P("@stale", stale),
                P("@n", take));
            return MapAll(table);
        }

        public IList<AccOutboxRow> ListRecent(int take)
        {
            DataTable table = _db.Query(
                "SELECT * FROM AccOutbox ORDER BY OutboxID DESC LIMIT @n;", P("@n", take));
            return MapAll(table);
        }

        public OutboxSnapshot Snapshot()
        {
            OutboxSnapshot s = new OutboxSnapshot();
            s.Pending = Count(LedgerCodes.OutboxPending);
            s.Processing = Count(LedgerCodes.OutboxProcessing);
            s.Failed = Count(LedgerCodes.OutboxFailed);
            s.DeadLetter = Count(LedgerCodes.OutboxDeadLetter);
            s.Completed = Count(LedgerCodes.OutboxCompleted);
            s.Recent = ListRecent(80);
            return s;
        }

        public bool Claim(long outboxId, string user, DateTime utcNow)
        {
            string now = LedgerTime.UtcNow(utcNow);
            int n = _db.ExecuteNonQuery(@"
UPDATE AccOutbox SET
  Status = @proc, LockedAt = @now, LockedBy = @by, RowVersion = RowVersion + 1
WHERE OutboxID = @id AND Status IN (@p, @f, @proc);",
                P("@proc", LedgerCodes.OutboxProcessing),
                P("@now", now),
                P("@by", user ?? ""),
                P("@id", outboxId),
                P("@p", LedgerCodes.OutboxPending),
                P("@f", LedgerCodes.OutboxFailed));
            return n == 1;
        }

        public void Complete(long outboxId, DateTime utcNow)
        {
            _db.ExecuteNonQuery(@"
UPDATE AccOutbox SET Status = @st, ProcessedAt = @now, LastError = NULL, LastErrorCode = NULL,
  LockedAt = NULL, LockedBy = NULL, RowVersion = RowVersion + 1
WHERE OutboxID = @id;",
                P("@st", LedgerCodes.OutboxCompleted),
                P("@now", LedgerTime.UtcNow(utcNow)),
                P("@id", outboxId));
        }

        public void Fail(AccOutboxRow row, string code, string message, DateTime utcNow)
        {
            int attempts = row.AttemptCount + 1;
            int max = row.MaxAttempts > 0 ? row.MaxAttempts : 8;
            string status = attempts >= max ? LedgerCodes.OutboxDeadLetter : LedgerCodes.OutboxFailed;
            DateTime next = utcNow.Add(OutboxRetryPolicy.DelayAfterFailure(attempts));
            _db.ExecuteNonQuery(@"
UPDATE AccOutbox SET Status = @st, AttemptCount = @att, NextAttemptAt = @next,
  LastError = @err, LastErrorCode = @code, LockedAt = NULL, LockedBy = NULL,
  RowVersion = RowVersion + 1
WHERE OutboxID = @id;",
                P("@st", status),
                P("@att", attempts),
                P("@next", status == LedgerCodes.OutboxDeadLetter ? (object)DBNull.Value : LedgerTime.UtcNow(next)),
                P("@err", Trunc(message, 1000)),
                P("@code", code ?? ""),
                P("@id", row.OutboxId));
        }

        public bool Requeue(long outboxId)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE AccOutbox SET Status = @p, AttemptCount = 0, NextAttemptAt = NULL, LastError = NULL,
  LastErrorCode = NULL, LockedAt = NULL, LockedBy = NULL, RowVersion = RowVersion + 1
WHERE OutboxID = @id AND Status IN (@f, @d);",
                P("@p", LedgerCodes.OutboxPending),
                P("@f", LedgerCodes.OutboxFailed),
                P("@d", LedgerCodes.OutboxDeadLetter),
                P("@id", outboxId));
            return n == 1;
        }

        private int Count(string status)
        {
            object v = _db.ExecuteScalar("SELECT COUNT(1) FROM AccOutbox WHERE Status = @s;", P("@s", status));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
        }

        private static IList<AccOutboxRow> MapAll(DataTable table)
        {
            List<AccOutboxRow> list = new List<AccOutboxRow>();
            foreach (DataRow r in table.Rows) list.Add(Map(r));
            return list;
        }

        private static AccOutboxRow Map(DataRow r)
        {
            AccOutboxRow row = new AccOutboxRow();
            row.OutboxId = Convert.ToInt64(r["OutboxID"]);
            row.CompanyId = Convert.ToInt32(r["CompanyID"]);
            row.CenterId = r["CenterID"] == DBNull.Value ? 0 : Convert.ToInt32(r["CenterID"]);
            row.SourceModule = r["SourceModule"].ToString();
            row.DocumentType = r["DocumentType"].ToString();
            row.DocumentId = Convert.ToInt64(r["DocumentID"]);
            row.Operation = r["Operation"].ToString();
            row.Payload = r["Payload"] == DBNull.Value ? null : r["Payload"].ToString();
            row.Status = r["Status"].ToString();
            row.AttemptCount = Convert.ToInt32(r["AttemptCount"]);
            row.MaxAttempts = Convert.ToInt32(r["MaxAttempts"]);
            row.NextAttemptAt = r["NextAttemptAt"] == DBNull.Value ? null : r["NextAttemptAt"].ToString();
            row.LastError = r["LastError"] == DBNull.Value ? null : r["LastError"].ToString();
            row.LastErrorCode = r["LastErrorCode"] == DBNull.Value ? null : r["LastErrorCode"].ToString();
            row.CreatedAt = r["CreatedAt"].ToString();
            row.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return row;
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max);
        }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }
    }
}

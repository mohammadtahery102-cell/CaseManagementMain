using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;

namespace CaseManagement.Accounting
{
    /// <summary>
    /// Enqueues AccOutbox rows on the caller connection. Does not post GL.
    /// </summary>
    public static class AccOutboxWriter
    {
        public static void Enqueue(SQLiteConnection con, SQLiteTransaction tr,
            string sourceModule, string documentType, long documentId, string operation,
            int companyId, int centerId, string userName)
        {
            if (con == null) throw new ArgumentNullException("con");
            if (string.IsNullOrWhiteSpace(sourceModule) || string.IsNullOrWhiteSpace(documentType)
                || string.IsNullOrWhiteSpace(operation) || documentId <= 0)
                return;

            string now = LedgerTime.UtcNow(DateTime.UtcNow);
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT OR IGNORE INTO AccOutbox
  (CompanyID, CenterID, SourceModule, DocumentType, DocumentID, Operation, Status,
   AttemptCount, MaxAttempts, CreatedAt, CreatedBy, RowVersion)
VALUES
  (@c, @ctr, @mod, @typ, @id, @op, @st, 0, 8, @now, @by, 1);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId > 0 ? companyId : LedgerCodes.DefaultCompanyId);
                cmd.Parameters.AddWithValue("@ctr", centerId);
                cmd.Parameters.AddWithValue("@mod", sourceModule);
                cmd.Parameters.AddWithValue("@typ", documentType);
                cmd.Parameters.AddWithValue("@id", documentId);
                cmd.Parameters.AddWithValue("@op", operation);
                cmd.Parameters.AddWithValue("@st", LedgerCodes.OutboxPending);
                cmd.Parameters.AddWithValue("@now", now);
                cmd.Parameters.AddWithValue("@by", userName ?? "");
                cmd.ExecuteNonQuery();
            }
        }
    }
}

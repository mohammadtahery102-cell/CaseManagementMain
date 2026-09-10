using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.AiPlatform.Domain;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.AiPlatform.Infrastructure
{
    public static class AiPlatformInitializer
    {
        public static void Ensure()
        {
            SchemaVersion.Ensure();
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                CreateTables(con);
                Seed(con);
            }
            SchemaVersion.SetIfNewer(SchemaVersion.ComponentAiPlatform, 1, "AI Platform Foundation V1");
            SchemaVersion.SetIfNewer(SchemaVersion.ComponentAiPlatform, 2, "AI Professional V2");
        }

        private static void CreateTables(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS AipSetting (
  SettingID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  Provider TEXT NOT NULL, Endpoint TEXT NULL, Model TEXT NULL, ApiKey TEXT NULL,
  TimeoutMs INTEGER NOT NULL DEFAULT 30000, MaxTokens INTEGER NOT NULL DEFAULT 1024,
  Temperature REAL NOT NULL DEFAULT 0.2, LicenseLevel TEXT NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_AipSetting ON AipSetting(CompanyID) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS AipConversation (
  ConversationID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  UserID INTEGER NOT NULL, Title TEXT NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_AipConv_User ON AipConversation(CompanyID, UserID, CreatedAt);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS AipMessage (
  MessageID INTEGER PRIMARY KEY, ConversationID INTEGER NOT NULL, CompanyID INTEGER NOT NULL,
  Role TEXT NOT NULL, Body TEXT NOT NULL, Provider TEXT NULL,
  CreatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_AipMsg_Conv ON AipMessage(ConversationID, MessageID);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS AipAuditLog (
  AuditID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, UserID INTEGER NOT NULL,
  UserName TEXT NOT NULL, Prompt TEXT NOT NULL, Provider TEXT NOT NULL,
  TimestampUtc TEXT NOT NULL, InputTokens INTEGER NOT NULL DEFAULT 0,
  OutputTokens INTEGER NOT NULL DEFAULT 0, Status TEXT NOT NULL
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_AipAudit_Time ON AipAuditLog(CompanyID, TimestampUtc);");
        }

        private static void Seed(SQLiteConnection con)
        {
            int c = LedgerCodes.DefaultCompanyId;
            string now = LedgerTime.UtcNow(DateTime.UtcNow);
            string u = LedgerCodes.SystemUser;
            if (Scalar(con, "SELECT COUNT(1) FROM AipSetting WHERE CompanyID = " + c) == 0)
            {
                Exec(con, @"
INSERT INTO AipSetting (CompanyID, Provider, Endpoint, Model, ApiKey, TimeoutMs, MaxTokens, Temperature, LicenseLevel,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @p, '', '', '', 30000, 1024, 0.2, @lic, 0, 1, @n, @n, @u, @u);",
                    P("@c", c), P("@p", AiProviderNames.None), P("@lic", AiLicenseLevels.Free),
                    P("@n", now), P("@u", u));
            }
        }

        private static long Scalar(SQLiteConnection con, string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                object v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
            }
        }

        private static SQLiteParameter P(string n, object v) { return new SQLiteParameter(n, v ?? DBNull.Value); }

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

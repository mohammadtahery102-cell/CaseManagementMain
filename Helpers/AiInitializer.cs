using System;
using System.Data.SQLite;
using System.Diagnostics;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // دستیار هوشمند — فاز ۱ (زیرساخت پایگاه‌داده).
    //
    // کاملاً افزایشی: فقط جدول‌های تازه (Ai*) و دو ستون تازه روی TblReminder
    // اضافه می‌کند؛ هیچ جدول/ستون موجودی تغییر یا حذف نمی‌شود. الگوی
    // «CREATE TABLE IF NOT EXISTS» دقیقاً مثل بقیه‌ی Initializer های موجود
    // (DatabaseInitializer، EnterpriseInitializer، AccountingInitializer).
    //
    // آموزش — چرا این جدول‌ها هرگز به Sync/ اضافه نمی‌شوند: ماژول Sync
    // (SyncEngine.cs/SyncComparer.cs) فقط TblCase و TblFamily را می‌فرستد.
    // AiConversation/AiMessage/AiIntentLog می‌توانند نام/تذکره/تلفن را به شکل
    // متن آزاد نگه دارند؛ همگام‌سازی آن‌ها یعنی این داده‌ها بین همه‌ی مراکز و
    // سرور مرکزی پخش شوند. طبق AI_ASSISTANT_PHASE1_FIXES.md §7 عمداً محلی
    // نگه داشته می‌شوند — این جدول‌ها را به فهرست Sync اضافه نکنید.
    public static class AiInitializer
    {
        // اگر FTS5 در نسخه‌ی SQLite.Interop.dll در حال اجرا موجود نباشد
        // (بررسی شد که هست، اما این پرچم محافظِ آینده است)، جست‌وجوی هوشمند
        // بدون فهرست FTS5 و فقط با LIKE ساختاریافته کار می‌کند.
        public static bool FtsAvailable { get; private set; }

        // نگه‌داری مقدار قبل از هر بار EnsureAiObjects برای آزمون‌های جداگانه.
        public static void ResetForTests()
        {
            FtsAvailable = false;
        }

        public static void EnsureAiObjects()
        {
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                Exec(con, @"
CREATE TABLE IF NOT EXISTS AiConversation (
    ConversationID INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID         INTEGER NULL,
    CenterID       INTEGER NOT NULL,
    Title          TEXT NULL,
    StartedAt      TEXT NOT NULL DEFAULT (datetime('now')),
    LastMessageAt  TEXT NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AiConversation_Center ON AiConversation(CenterID, LastMessageAt);");

                Exec(con, @"
CREATE TABLE IF NOT EXISTS AiMessage (
    MessageID      INTEGER PRIMARY KEY AUTOINCREMENT,
    ConversationID INTEGER NOT NULL REFERENCES AiConversation(ConversationID) ON DELETE CASCADE,
    Sender         TEXT NOT NULL,
    MessageText    TEXT NOT NULL,
    IntentDetected TEXT NULL,
    EntitiesJson   TEXT NULL,
    Confidence     REAL NULL,
    ExecutionMs    INTEGER NULL,
    CreatedAt      TEXT NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AiMessage_Conversation ON AiMessage(ConversationID, CreatedAt);");

                Exec(con, @"
CREATE TABLE IF NOT EXISTS AiIntentLog (
    IntentLogID  INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID       INTEGER NULL,
    CenterID     INTEGER NULL,
    RawQuery     TEXT NOT NULL,
    ParsedIntent TEXT NULL,
    Confidence   REAL NULL,
    Success      INTEGER NOT NULL,
    ErrorNote    TEXT NULL,
    CreatedAt    TEXT NOT NULL DEFAULT (datetime('now'))
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_AiIntentLog_Created ON AiIntentLog(CreatedAt);");

                // ─── AI_ASSISTANT_PHASE1_FIXES.md §1/§2: نمایه FTS5 با CenterID ──
                // اگر FTS5 در دسترس نباشد (بعید، اما محافظه‌کارانه)، کل جست‌وجوی
                // هوشمند نباید از کار بیفتد — فقط بدون مرحله‌ی اول (پیش‌فیلتر
                // FTS5) و صرفاً با LIKE ساختاریافته کار می‌کند.
                try
                {
                    Exec(con, @"
CREATE VIRTUAL TABLE IF NOT EXISTS AiCaseSearchIndex USING fts5(
    CasID UNINDEXED,
    CenterID UNINDEXED,
    HeadFullName, HeadFatherName,
    HeadTazkiraNo, Province, District, Phone,
    tokenize = 'unicode61 remove_diacritics 2'
);");

                    Exec(con, @"
CREATE TRIGGER IF NOT EXISTS Trg_TblCase_AI_Insert AFTER INSERT ON TblCase BEGIN
    INSERT INTO AiCaseSearchIndex (CasID, CenterID, HeadFullName, HeadFatherName, HeadTazkiraNo, Province, District, Phone)
    SELECT NEW.CasID, NEW.CenterID, NEW.HeadFullName, NEW.HeadFatherName, NEW.HeadTazkiraNo, NEW.Province, NEW.District, NEW.Phone;
END;");
                    Exec(con, @"
CREATE TRIGGER IF NOT EXISTS Trg_TblCase_AI_Update AFTER UPDATE ON TblCase BEGIN
    DELETE FROM AiCaseSearchIndex WHERE CasID = OLD.CasID;
    INSERT INTO AiCaseSearchIndex (CasID, CenterID, HeadFullName, HeadFatherName, HeadTazkiraNo, Province, District, Phone)
    SELECT NEW.CasID, NEW.CenterID, NEW.HeadFullName, NEW.HeadFatherName, NEW.HeadTazkiraNo, NEW.Province, NEW.District, NEW.Phone;
END;");
                    Exec(con, @"
CREATE TRIGGER IF NOT EXISTS Trg_TblCase_AI_Delete AFTER DELETE ON TblCase BEGIN
    DELETE FROM AiCaseSearchIndex WHERE CasID = OLD.CasID;
END;");

                    // بازپرِ یک‌باره: فقط اگر نمایه خالی است (شامل بازیابیِ حالتِ
                    // نیمه‌کاره‌ی یک اجرای قطع‌شده هم می‌شود چون INSERT OR IGNORE
                    // بی‌ضرر تکرار می‌شود).
                    object countObj = ExecScalar(con, "SELECT COUNT(*) FROM AiCaseSearchIndex;");
                    long ftsCount = countObj == null || countObj == DBNull.Value ? 0 : Convert.ToInt64(countObj);
                    if (ftsCount == 0)
                    {
                        Exec(con, @"
INSERT INTO AiCaseSearchIndex (CasID, CenterID, HeadFullName, HeadFatherName, HeadTazkiraNo, Province, District, Phone)
SELECT CasID, CenterID, HeadFullName, HeadFatherName, HeadTazkiraNo, Province, District, Phone FROM TblCase;");
                    }

                    FtsAvailable = true;
                }
                catch (Exception ex)
                {
                    FtsAvailable = false;
                    Debug.WriteLine("[AiInitializer] FTS5 unavailable, falling back to LIKE-only search: " + ex.Message);
                }

                // ─── AI_ASSISTANT_PHASE1_FIXES.md §8: نگه‌داشت محدود (۱۸۰ روز) ──
                int retentionDays = SettingsHelper.GetInt("Ai.RetentionDays", 180);
                Exec(con,
                    "DELETE FROM AiMessage WHERE ConversationID IN (SELECT ConversationID FROM AiConversation WHERE LastMessageAt < datetime('now', @Cutoff));",
                    "@Cutoff", "-" + retentionDays + " days");
                Exec(con,
                    "DELETE FROM AiConversation WHERE LastMessageAt < datetime('now', @Cutoff);",
                    "@Cutoff", "-" + retentionDays + " days");
                Exec(con,
                    "DELETE FROM AiIntentLog WHERE CreatedAt < datetime('now', @Cutoff);",
                    "@Cutoff", "-" + retentionDays + " days");

                // ─── AI_ASSISTANT_PHASE1_FIXES.md §9: آشتیِ سبک یادآوری/حسابرسی ──
                // AuditLogger.Log کانکشنِ خودش را باز می‌کند، پس یک تراکنشِ واحد
                // با درجِ TblReminder ممکن نیست. این‌جا فقط ردیف‌های یتیم را
                // برای بازبینی گزارش می‌کند؛ هیچ‌چیزی را خودکار اصلاح نمی‌کند.
                try
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT COUNT(*) FROM TblReminder
WHERE CreatedByAI = 1
  AND ReminderID NOT IN (
      SELECT EntityID FROM TblAuditLog
      WHERE EntityName = 'TblReminder' AND Operation = 'AI:CreateReminder'
  );", con))
                    {
                        object orphanCountObj = cmd.ExecuteScalar();
                        long orphanCount = orphanCountObj == null || orphanCountObj == DBNull.Value ? 0 : Convert.ToInt64(orphanCountObj);
                        if (orphanCount > 0)
                            Debug.WriteLine("[AiInitializer] " + orphanCount + " AI-created reminder(s) missing an audit log entry — see AI_ASSISTANT_PHASE1_FIXES.md §9.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[AiInitializer] reminder/audit reconciliation check failed: " + ex.Message);
                }
            }
        }

        private static void Exec(SQLiteConnection con, string sql, params object[] paramPairs)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                cmd.CommandTimeout = 120;
                if (paramPairs != null)
                {
                    for (int i = 0; i + 1 < paramPairs.Length; i += 2)
                        cmd.Parameters.AddWithValue((string)paramPairs[i], paramPairs[i + 1] ?? DBNull.Value);
                }
                cmd.ExecuteNonQuery();
            }
        }

        private static object ExecScalar(SQLiteConnection con, string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
                return cmd.ExecuteScalar();
        }
    }
}

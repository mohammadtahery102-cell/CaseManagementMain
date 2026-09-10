using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;

namespace CaseManagement.Trade
{
    public static class TradeWorkflowSeed
    {
        public static void Ensure(SQLiteConnection con, string code, string name, string entityName,
            string createPerm, string approvePerm, string postPerm)
        {
            object existing;
            try
            {
                using (SQLiteCommand cmd = new SQLiteCommand("SELECT WorkflowID FROM EntWorkflow WHERE Code = @c;", con))
                {
                    cmd.Parameters.AddWithValue("@c", code);
                    existing = cmd.ExecuteScalar();
                }
            }
            catch (SQLiteException)
            {
                return;
            }
            if (existing != null && existing != DBNull.Value) return;

            using (SQLiteCommand ins = new SQLiteCommand(@"
INSERT INTO EntWorkflow (Code, Name, EntityName, Description, IsActive, CreatedBy)
VALUES (@c, @n, @e, @d, 1, @u);", con))
            {
                ins.Parameters.AddWithValue("@c", code);
                ins.Parameters.AddWithValue("@n", name);
                ins.Parameters.AddWithValue("@e", entityName);
                ins.Parameters.AddWithValue("@d", name);
                ins.Parameters.AddWithValue("@u", LedgerCodes.SystemUser);
                ins.ExecuteNonQuery();
            }
            long wf;
            using (SQLiteCommand id = new SQLiteCommand("SELECT WorkflowID FROM EntWorkflow WHERE Code = @c;", con))
            {
                id.Parameters.AddWithValue("@c", code);
                wf = Convert.ToInt64(id.ExecuteScalar());
            }
            AddState(con, wf, "DRAFT", "پیش‌نویس", 1, 0, 10);
            AddState(con, wf, "SUBMITTED", "ارسال‌شده", 0, 0, 20);
            AddState(con, wf, "APPROVED", "تأیید شده", 0, 0, 30);
            AddState(con, wf, "POSTED", "ثبت قطعی", 0, 1, 40);
            AddState(con, wf, "REJECTED", "رد شده", 0, 1, 50);
            AddTransition(con, wf, "DRAFT", "SUBMITTED", "ارسال", createPerm);
            AddTransition(con, wf, "SUBMITTED", "APPROVED", "تأیید", approvePerm);
            AddTransition(con, wf, "APPROVED", "POSTED", "ثبت قطعی", postPerm);
            AddTransition(con, wf, "SUBMITTED", "DRAFT", "بازگشت", approvePerm);
            AddTransition(con, wf, "SUBMITTED", "REJECTED", "رد", approvePerm);
        }

        private static void AddState(SQLiteConnection con, long wf, string code, string name, int initial, int final, int sort)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT OR IGNORE INTO EntWorkflowState (WorkflowID, Code, Name, IsInitial, IsFinal, SortOrder)
VALUES (@w, @c, @n, @i, @f, @s);", con))
            {
                cmd.Parameters.AddWithValue("@w", wf);
                cmd.Parameters.AddWithValue("@c", code);
                cmd.Parameters.AddWithValue("@n", name);
                cmd.Parameters.AddWithValue("@i", initial);
                cmd.Parameters.AddWithValue("@f", final);
                cmd.Parameters.AddWithValue("@s", sort);
                cmd.ExecuteNonQuery();
            }
        }

        private static long State(SQLiteConnection con, long wf, string code)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT StateID FROM EntWorkflowState WHERE WorkflowID = @w AND Code = @c;", con))
            {
                cmd.Parameters.AddWithValue("@w", wf);
                cmd.Parameters.AddWithValue("@c", code);
                object v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
            }
        }

        private static void AddTransition(SQLiteConnection con, long wf, string from, string to, string name, string perm)
        {
            long f = State(con, wf, from);
            long t = State(con, wf, to);
            if (f <= 0 || t <= 0) return;
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO EntWorkflowTransition (WorkflowID, FromStateID, ToStateID, Name, RequiredPermission, RequiresApproval, SortOrder)
VALUES (@w, @f, @t, @n, @p, 0, 10);", con))
            {
                cmd.Parameters.AddWithValue("@w", wf);
                cmd.Parameters.AddWithValue("@f", f);
                cmd.Parameters.AddWithValue("@t", t);
                cmd.Parameters.AddWithValue("@n", name);
                cmd.Parameters.AddWithValue("@p", (object)perm ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

using System;
using System.Configuration;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.DAL
{
    public class DatabaseHelper
    {
        private readonly string connectionString;

        public DatabaseHelper()
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["CaseDb"];

            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new ConfigurationErrorsException("رشته اتصال CaseDb در App.config پیدا نشد یا خالی است.");

            connectionString = settings.ConnectionString;
        }

        public SQLiteConnection GetConnection()
        {
            // Ensure DataDirectory token is resolved by .NET when using |DataDirectory|
            string cs = connectionString;
            try
            {
                if (cs != null && cs.Contains("|DataDirectory|"))
                {
                    string dataDir = AppDomain.CurrentDomain.GetData("DataDirectory") as string;
                    if (string.IsNullOrEmpty(dataDir))
                    {
                        dataDir = AppDomain.CurrentDomain.BaseDirectory;
                        AppDomain.CurrentDomain.SetData("DataDirectory", dataDir);
                    }
                    cs = cs.Replace("|DataDirectory|", dataDir);
                }
            }
            catch
            {
                // If anything goes wrong, fall back to the original connection string.
            }

            // SQLite keeps foreign-key enforcement OFF by default per connection.
            // Without this, ON DELETE CASCADE (TblFamily/TblDocs/TblAssistance) never fires.
            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder(cs);
            builder.ForeignKeys = true;

            return new SQLiteConnection(builder.ConnectionString);
        }

        // ─── متدهای عمومی DAL ───────────────────────────────────────────────
        // آموزش: این متدها اضافه شده‌اند تا کد جدید بتواند بدون تکرار
        // using(GetConnection())/using(SQLiteCommand) عملیات ساده انجام دهد.
        // کد فعلی فرم‌ها همچنان از GetConnection() مستقیم استفاده می‌کند و
        // تغییری نکرده است؛ این‌ها فقط زیرساخت آماده برای توسعه آینده‌اند.

        public int ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            using (SQLiteConnection con = GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);

                con.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        public object ExecuteScalar(string sql, params SQLiteParameter[] parameters)
        {
            using (SQLiteConnection con = GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);

                con.Open();
                return cmd.ExecuteScalar();
            }
        }

        public DataTable Query(string sql, params SQLiteParameter[] parameters)
        {
            using (SQLiteConnection con = GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);

                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable table = new DataTable();
                    da.Fill(table);
                    return table;
                }
            }
        }

        // اجرای چند دستور به‌صورت اتمیک؛ اگر action خطا بدهد، Rollback خودکار
        // انجام می‌شود و خطا دوباره پرتاب می‌شود (مثل الگوی موجود در
        // BackupHelper.ImportBackup و FrmFamily/FrmDocs).
        public void ExecuteInTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            using (SQLiteConnection con = GetConnection())
            {
                con.Open();

                using (SQLiteTransaction tr = con.BeginTransaction())
                {
                    try
                    {
                        action(con, tr);
                        tr.Commit();
                    }
                    catch
                    {
                        try { tr.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }
    }
}

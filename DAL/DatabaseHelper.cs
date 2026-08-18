using System;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.IO;
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
            // اگر AppDomain هنوز DataDirectory نداشته باشد، System.Data.SQLite
            // در Open() نمی‌تواند توکنِ «|DataDirectory|» را برطرف کند؛ این
            // تضمین می‌کند همیشه یک مقدار (لااقل پوشه‌ی خودِ برنامه) موجود باشد.
            try
            {
                if (connectionString != null && connectionString.Contains("|DataDirectory|") &&
                    AppDomain.CurrentDomain.GetData("DataDirectory") == null)
                {
                    AppDomain.CurrentDomain.SetData("DataDirectory", AppDomain.CurrentDomain.BaseDirectory);
                }
            }
            catch { /* اگر ناموفق بود، همچنان تلاشِ اتصال ادامه پیدا می‌کند */ }

            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder(connectionString);
            builder.ForeignKeys = true;

            return new SQLiteConnection(builder.ConnectionString);
        }

        // آموزش — مسیرِ واقعیِ فایلِ دیتابیس روی دیسک (نه توکنِ خامِ
        // «|DataDirectory|»، بلکه مسیرِ کاملِ برطرف‌شده). بارها مشاهده شد که
        // چند نسخه‌ی مختلف از پروژه/برنامه روی یک سیستم نصب است و هرکدام
        // دیتابیسِ خودش را دارد؛ این متد برای نمایشِ صریحِ «این اجرا دقیقاً به
        // کدام فایل وصل است» در گزارش‌های تشخیصی استفاده می‌شود.
        //
        // ⚠ رفعِ باگ: نسخه‌ی قبلی توکنِ «|DataDirectory|» را جایگزین نمی‌کرد و
        // فقط AppDomain.SetData را صدا می‌زد؛ چون System.Data.SQLite خودش این
        // جایگزینی را *فقط هنگامِ Open شدنِ اتصال* انجام می‌دهد، خواندنِ مسیر
        // از یک اتصالِ بازنشده (مثلِ اینجا) همیشه همان توکنِ خام را برمی‌گرداند.
        public string GetDatabaseFilePath()
        {
            try
            {
                var builder = new SQLiteConnectionStringBuilder(connectionString);
                string dataSource = builder.DataSource ?? "";

                if (dataSource.IndexOf("|DataDirectory|", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string dataDir = AppDomain.CurrentDomain.GetData("DataDirectory") as string;
                    if (string.IsNullOrEmpty(dataDir))
                        dataDir = AppDomain.CurrentDomain.BaseDirectory;

                    dataSource = dataSource.Replace("|DataDirectory|", dataDir.TrimEnd('\\', '/'));
                }

                return Path.GetFullPath(dataSource);
            }
            catch
            {
                return "";
            }
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
        //
        // آموزش — busy_timeout فقط روی همین «مسیر نوشتن» اعمال می‌شود و نه در
        // GetConnection، تا رفتار بقیه‌ی فرم‌های برنامه (که مستقیم از
        // GetConnection استفاده می‌کنند) ذره‌ای تغییر نکند. بدون این تنظیم،
        // SQLite در لحظه‌ی قفل بودن فایل بلافاصله خطای «database is locked»
        // می‌دهد؛ با آن، تا این مدت صبر و دوباره تلاش می‌کند — یعنی دو کاربر
        // هم‌زمان می‌توانند سند ثبت کنند بدون خطای تصادفی.
        public const int WriteBusyTimeoutMs = 8000;

        public void ExecuteInTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            using (SQLiteConnection con = GetConnection())
            {
                con.Open();

                using (SQLiteCommand pragma = new SQLiteCommand("PRAGMA busy_timeout=" + WriteBusyTimeoutMs + ";", con))
                    pragma.ExecuteNonQuery();

                // BeginImmediate: قفل نوشتن از همان ابتدای تراکنش گرفته می‌شود،
                // نه در اولین نوشتن. این همان چیزی است که الگوی «بخوان، بررسی
                // کن، بنویس» (مثل بررسی یکتا بودن شماره سند و سپس درج آن) را
                // در برابر ثبت هم‌زمانِ دو کاربر ایمن می‌کند.
                using (SQLiteTransaction tr = con.BeginTransaction(System.Data.IsolationLevel.Serializable))
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

        // اجرای یک INSERT داخل تراکنش و برگرداندن شناسه‌ی ردیف تازه‌ساخته‌شده.
        //
        // آموزش — رفع یک باگ بحرانی حسابداری: الگوی قبلی در AccountingRepo این
        // بود که اول ExecuteNonQuery برای INSERT صدا زده شود و بعد
        // ExecuteScalar("SELECT last_insert_rowid()") برای گرفتن شناسه. اما هر
        // کدام از این دو متد کانکشن *جداگانه‌ی خودش* را باز و بسته می‌کند، و
        // چون در App.config گزینه‌ی Pooling فعال نیست، کانکشن دوم همیشه تازه‌ساز
        // است. مقدار last_insert_rowid() روی کانکشنی که هیچ INSERT انجام نداده
        // همیشه صفر است.
        //
        // اثر واقعی روی دیتابیس فعلی: از ۴۳ ردیف AccAudit، ۳۹ ردیف با
        // EntityID = 0 ثبت شده‌اند (تمام ردیف‌های «ثبت»)؛ فقط ردیف‌های «حذف»
        // شناسه‌ی درست دارند، چون آنجا شناسه مستقیماً پاس داده می‌شود. یعنی
        // ردّ حسابرسیِ مالی عملاً به هیچ رکوردی قابل اتصال نبود.
        //
        // اینجا INSERT و خواندن شناسه روی *یک کانکشن* و داخل *یک تراکنش*
        // انجام می‌شوند؛ هم شناسه درست است و هم عملیات اتمیک.
        public long ExecuteInsertReturningId(string sql, params SQLiteParameter[] parameters)
        {
            long newId = 0;

            ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                using (SQLiteCommand cmd = new SQLiteCommand(sql, con, tr))
                {
                    if (parameters != null)
                        cmd.Parameters.AddRange(parameters);
                    cmd.ExecuteNonQuery();
                }

                using (SQLiteCommand idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                {
                    object v = idCmd.ExecuteScalar();
                    newId = v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
                }
            });

            return newId;
        }
    }
}

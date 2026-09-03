using System;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Enterprise
{
    // ─────────────────────────────────────────────────────────────────────────
    // پوششِ نازک روی DatabaseHelper برای سرویس‌های هسته سازمانی.
    // آموزش — چرا این کلاس اضافه شد: DatabaseHelper پارامترها را به‌صورت
    // SQLiteParameter[] می‌گیرد و ساختنِ آن‌ها در ده سرویس، ده‌ها خط تکراری
    // می‌شود. این‌جا همان DatabaseHelper موجود استفاده می‌شود (نه اتصال جدید و
    // نه رشته اتصال جدید)؛ فقط پارامترها به شکل جفت‌های نام/مقدار پذیرفته
    // می‌شوند. هیچ کد موجودی به این کلاس وابسته نیست.
    // ─────────────────────────────────────────────────────────────────────────
    internal static class EntDb
    {
        internal static DatabaseHelper Helper()
        {
            return new DatabaseHelper();
        }

        internal static DataTable Query(string sql, params object[] nameValuePairs)
        {
            return Helper().Query(sql, BuildParams(nameValuePairs));
        }

        internal static int Exec(string sql, params object[] nameValuePairs)
        {
            return Helper().ExecuteNonQuery(sql, BuildParams(nameValuePairs));
        }

        internal static object Scalar(string sql, params object[] nameValuePairs)
        {
            return Helper().ExecuteScalar(sql, BuildParams(nameValuePairs));
        }

        // درج و برگرداندنِ کلید تولیدشده. آموزش: last_insert_rowid() فقط در
        // همان اتصال معتبر است، بنابراین هر دو دستور در یک اتصال اجرا می‌شوند.
        internal static long Insert(string sql, params object[] nameValuePairs)
        {
            using (SQLiteConnection con = Helper().GetConnection())
            {
                con.Open();

                using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
                {
                    cmd.Parameters.AddRange(BuildParams(nameValuePairs));
                    cmd.ExecuteNonQuery();
                }

                using (SQLiteCommand idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                    return ToInt64(idCmd.ExecuteScalar());
            }
        }

        internal static SQLiteParameter[] BuildParams(object[] nameValuePairs)
        {
            if (nameValuePairs == null || nameValuePairs.Length == 0)
                return new SQLiteParameter[0];

            int count = nameValuePairs.Length / 2;
            SQLiteParameter[] result = new SQLiteParameter[count];

            for (int i = 0; i < count; i++)
            {
                object value = nameValuePairs[(i * 2) + 1];

                // رشته خالی به NULL تبدیل می‌شود تا ستون‌های اختیاری تمیز بمانند
                // (همان رفتار AuditLogger.AddText).
                string text = value as string;
                if (text != null && text.Trim().Length == 0)
                    value = null;
                else if (text != null)
                    value = text.Trim();

                result[i] = new SQLiteParameter(
                    Convert.ToString(nameValuePairs[i * 2]),
                    value ?? DBNull.Value);
            }

            return result;
        }

        // ─── تبدیل‌های امن مقدار ستون ──────────────────────────────────────
        internal static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            try { return Convert.ToInt32(value); }
            catch { return 0; }
        }

        internal static long ToInt64(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            try { return Convert.ToInt64(value); }
            catch { return 0; }
        }

        internal static string ToText(object value)
        {
            if (value == null || value == DBNull.Value) return "";
            return Convert.ToString(value);
        }

        internal static bool ToBool(object value)
        {
            return ToInt(value) != 0;
        }

        internal static DateTime? ToDate(object value)
        {
            string text = ToText(value);
            if (text.Length == 0) return null;

            DateTime parsed;
            if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.None, out parsed))
                return parsed;

            return null;
        }

        // مقدار ستون در صورت وجودِ ستون در DataRow؛ در غیر این صورت null.
        internal static object Col(DataRow row, string columnName)
        {
            if (row == null || !row.Table.Columns.Contains(columnName)) return null;
            return row[columnName];
        }
    }
}

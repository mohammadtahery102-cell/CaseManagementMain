using System;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    // ثبت رویدادهای مالی در AccAudit — نام کاربر، نام کامپیوتر، IP، تاریخ/زمان.
    // آموزش: مستقل از AuditLogger عمومی است تا رویدادهای حسابداری جدا نگه‌داری
    // شوند؛ خطا در ثبت لاگ هرگز عملیات اصلی را متوقف نمی‌کند (catch خاموش).
    public static class AccAudit
    {
        public static void Log(string operation, string entity, int entityId, string detail)
        {
            LogChange(operation, entity, entityId, null, null, null, detail);
        }

        // آموزش — تکمیل ردّ حسابرسی طبق الزام «هر عملیات مالی باید مقدار قبلی،
        // مقدار جدید و دلیل تغییر را ثبت کند». نسخه‌ی قبلی فقط یک رشته‌ی
        // Detail داشت و هیچ‌جا مقدار قبلی نگه نمی‌داشت؛ یعنی بعد از یک ویرایش،
        // دیگر معلوم نبود عدد قبلی چه بوده. حالا OldValue/NewValue/Reason
        // به‌صورت ستون‌های مستقل ثبت می‌شوند تا قابل جست‌وجو و گزارش‌گیری باشند.
        public static void LogChange(string operation, string entity, int entityId,
            string oldValue, string newValue, string reason)
        {
            LogChange(operation, entity, entityId, oldValue, newValue, reason, null);
        }

        private static void LogChange(string operation, string entity, int entityId,
            string oldValue, string newValue, string reason, string detail)
        {
            try
            {
                // Detail برای سازگاری با ردیف‌های قبلی حفظ می‌شود؛ اگر پاس داده
                // نشود، از ترکیب مقدار قبلی/جدید ساخته می‌شود تا گزارش‌های
                // موجود که Detail را نشان می‌دهند خالی نمانند.
                if (string.IsNullOrEmpty(detail))
                {
                    if (!string.IsNullOrEmpty(oldValue) && !string.IsNullOrEmpty(newValue))
                        detail = oldValue + "  ←  " + newValue;
                    else
                        detail = newValue ?? oldValue ?? "";
                }

                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
INSERT INTO AccAudit (Operation, EntityName, EntityID, Detail, OldValue, NewValue, Reason, Username, MachineName, IPAddress, CenterID)
VALUES (@op, @en, @id, @d, @ov, @nv, @rs, @u, @m, @ip, @cid)", con))
                {
                    cmd.Parameters.AddWithValue("@op", (object)operation ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@en", (object)entity ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", entityId);
                    cmd.Parameters.AddWithValue("@d", (object)detail ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ov", (object)oldValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@nv", (object)newValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@rs", (object)reason ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@u", (object)SecurityContext.Username ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@m", (object)Environment.MachineName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ip", (object)GetLocalIP() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cid", SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // آموزش — قبلاً این catch کاملاً خاموش بود. برای یک ماژول مالی،
                // «شکست خاموشِ ثبت حسابرسی» یعنی ممکن است عملیاتی انجام شده
                // باشد و هیچ ردی نداشته باشد بدون آن‌که کسی بفهمد. عملیات اصلی
                // همچنان متوقف نمی‌شود (ثبت سند مهم‌تر از ثبت لاگ است)، اما
                // شکست در یک فایل کنار برنامه ثبت می‌شود تا قابل کشف باشد.
                WriteFallback(operation, entity, entityId, oldValue, newValue, reason, ex);
            }
        }

        // ثبت اضطراری روی دیسک وقتی نوشتن در جدول AccAudit ممکن نیست.
        private static void WriteFallback(string operation, string entity, int entityId,
            string oldValue, string newValue, string reason, Exception ex)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "AccAudit_Fallback.log");

                string line = string.Join(" | ", new[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    SecurityContext.Username ?? "",
                    Environment.MachineName ?? "",
                    operation ?? "", entity ?? "", entityId.ToString(),
                    "قبلی=" + (oldValue ?? ""), "جدید=" + (newValue ?? ""),
                    "دلیل=" + (reason ?? ""),
                    "خطای ثبت لاگ: " + ex.Message
                });

                System.IO.File.AppendAllText(path, line + Environment.NewLine,
                    System.Text.Encoding.UTF8);
            }
            catch { /* اگر دیسک هم در دسترس نیست، کاری از دست ما برنمی‌آید */ }
        }

        private static string GetLocalIP()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ip.ToString();
            }
            catch { }
            return "";
        }
    }
}

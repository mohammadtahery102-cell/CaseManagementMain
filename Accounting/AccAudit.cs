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
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
INSERT INTO AccAudit (Operation, EntityName, EntityID, Detail, Username, MachineName, IPAddress, CenterID)
VALUES (@op, @en, @id, @d, @u, @m, @ip, @cid)", con))
                {
                    cmd.Parameters.AddWithValue("@op", (object)operation ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@en", (object)entity ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", entityId);
                    cmd.Parameters.AddWithValue("@d", (object)detail ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@u", (object)SecurityContext.Username ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@m", (object)Environment.MachineName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ip", (object)GetLocalIP() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cid", SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* ثبت لاگ غیرحیاتی است */ }
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

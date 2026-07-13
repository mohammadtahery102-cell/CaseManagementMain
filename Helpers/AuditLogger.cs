using CaseManagement.DAL;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace CaseManagement.Helpers
{
    public static class AuditLogger
    {
        // ─── مسیر فایل لاگ خطا (برای خطاهای خود AuditLogger) ───────────────
        // آموزش: این فایل فقط برای خطاهای داخلی Logger است، نه رویدادهای عادی.
        // رویدادهای عادی در TblAuditLog دیتابیس ذخیره می‌شوند.
        private static readonly string ErrorLogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "audit_errors.log");

        // ─── ثبت رویداد در دیتابیس ──────────────────────────────────────────
        // آموزش — قابل خاموش‌کردن از تب امنیت (پیش‌فرض روشن = رفتار قبلی بدون
        // تغییر). فقط ثبت رویدادهای عمومی (TblAuditLog) را کنترل می‌کند؛
        // RecordStatusChange (تاریخچه وضعیت پرونده، داده کسب‌وکاری اصلی نه
        // صرفاً audit trail امنیتی) همیشه فعال می‌ماند.
        public static void Log(
            string operation,
            string entityName,
            int    entityId,
            string oldValue,
            string newValue)
        {
            if (SettingsHelper.GetInt(SettingsHelper.AuditEnabled, 1) == 0)
                return;

            try
            {
                using (SQLiteConnection con = new DatabaseHelper().GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO TblAuditLog
    (UserID, Username, Operation, EntityName, EntityID, OldValue, NewValue, CenterID)
VALUES
    (@UserID, @Username, @Operation, @EntityName, @EntityID, @OldValue, @NewValue, @CenterID)", con))
                {
                    AddNullableInt(cmd, "@UserID",     SecurityContext.UserId);
                    AddText       (cmd, "@Username",   SecurityContext.Username);
                    AddText       (cmd, "@Operation",  operation);
                    AddText       (cmd, "@EntityName", entityName);
                    AddNullableInt(cmd, "@EntityID",   entityId);
                    AddText       (cmd, "@OldValue",   oldValue);
                    AddText       (cmd, "@NewValue",   newValue);
                    AddNullableInt(cmd, "@CenterID",   SecurityContext.CurrentCenterId);

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // آموزش — چرا catch پر بهتر از catch خالی است:
                // ۱. Debug.WriteLine فقط در حین توسعه دیده می‌شود (Output window).
                // ۲. فایل لاگ در Production مشکل را ثبت می‌کند.
                // ۳. Exception بالا نمی‌رود — رفتار عملکردی برنامه تغییر نمی‌کند.
                // ۴. برنامه crash نمی‌کند فقط به خاطر مشکل در لاگ.
                string msg = $"[AuditLogger.Log] {DateTime.Now:yyyy-MM-dd HH:mm:ss} " +
                             $"Op={operation} Entity={entityName} ID={entityId} | {ex.Message}";
                Debug.WriteLine(msg);
                TryWriteErrorLog(msg);
            }
        }

        // ─── ثبت تغییر وضعیت پرونده ─────────────────────────────────────────
        public static void RecordStatusChange(int caseId, string oldStatus, string newStatus)
        {
            if (string.Equals(
                (oldStatus ?? "").Trim(),
                (newStatus ?? "").Trim(),
                StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                using (SQLiteConnection con = new DatabaseHelper().GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO TblCaseStatusHistory
    (CasID, OldStatus, NewStatus, ChangedBy)
VALUES
    (@CasID, @OldStatus, @NewStatus, @ChangedBy)", con))
                {
                    cmd.Parameters.AddWithValue("@CasID",     caseId);
                    AddText(cmd, "@OldStatus",  oldStatus);
                    AddText(cmd, "@NewStatus",  newStatus);
                    AddText(cmd, "@ChangedBy",  SecurityContext.Username);

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                string msg = $"[AuditLogger.RecordStatusChange] {DateTime.Now:yyyy-MM-dd HH:mm:ss} " +
                             $"CaseID={caseId} {oldStatus}→{newStatus} | {ex.Message}";
                Debug.WriteLine(msg);
                TryWriteErrorLog(msg);
            }
        }

        // ─── نوشتن خطا در فایل متنی ─────────────────────────────────────────
        // آموزش: از try/catch داخلی استفاده می‌کنیم چون حتی نوشتن فایل هم ممکن است
        // شکست بخورد (مثلاً مجوز نوشتن نداشته باشیم). در این حالت فقط Debug می‌بینیم.
        private static void TryWriteErrorLog(string message)
        {
            try
            {
                File.AppendAllText(ErrorLogPath, message + Environment.NewLine);
            }
            catch
            {
                // اگر نوشتن فایل هم شکست خورد، چیزی نمی‌توانیم بکنیم
            }
        }

        private static void AddNullableInt(SQLiteCommand cmd, string name, int value)
        {
            cmd.Parameters.AddWithValue(name, value > 0 ? (object)value : DBNull.Value);
        }

        private static void AddText(SQLiteCommand cmd, string name, string value)
        {
            cmd.Parameters.AddWithValue(name,
                string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim());
        }
    }
}

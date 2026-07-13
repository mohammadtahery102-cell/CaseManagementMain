using CaseManagement.DAL;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CaseManagement.Helpers
{
    public static class AutoBackupService
    {
        private const string LastBackupDateKey = "AutoBackupLastDate";

        // ─── Backup خودکار (روزانه/هفتگی/ماهانه — قابل تنظیم از Settings) ─────
        // آموزش — طراحی سیستم Backup قابل اعتماد:
        //   ۱. Backup در طول شروع برنامه اجرا می‌شود (Program.cs).
        //   ۲. تاریخ آخرین backup در دیتابیس ذخیره می‌شود.
        //   ۳. اگر فاصله لازم (طبق زمان‌بندی انتخابی) نگذشته باشد، دوباره نمی‌گیرد.
        //   ۴. تعداد نسخه‌های نگه‌داشته‌شده از تنظیمات خوانده می‌شود (پیش‌فرض ۱۴).
        //   ۵. شکست backup برنامه را crash نمی‌کند ولی لاگ می‌شود.
        public static void RunDailyBackupIfDue()
        {
            try
            {
                // مسیر بکاپ: اگر مدیر سیستم مسیر اختصاصی تنظیم کرده باشد همان
                // استفاده می‌شود؛ در غیر این صورت رفتار قبلی (زیرپوشه در محل
                // اصلی ذخیره فایل‌ها) حفظ می‌شود.
                string configuredBackupPath = SettingsHelper.Get(SettingsHelper.BackupPath);
                string root = FileHelper.GetBaseRootFolder();

                if (string.IsNullOrWhiteSpace(configuredBackupPath) && string.IsNullOrWhiteSpace(root))
                    return;

                DateTime today          = DateTime.Today;
                string   lastBackupDate = GetSetting(LastBackupDateKey);
                DateTime lastDate;

                int intervalDays = GetScheduleIntervalDays();
                if (DateTime.TryParse(lastBackupDate, out lastDate) &&
                    (today - lastDate.Date).TotalDays < intervalDays)
                    return;

                string autoBackupFolder = !string.IsNullOrWhiteSpace(configuredBackupPath) && Directory.Exists(configuredBackupPath)
                    ? Path.Combine(configuredBackupPath, "AutoBackups")
                    : Path.Combine(root, "AutoBackups");
                Directory.CreateDirectory(autoBackupFolder);

                BackupHelper backupHelper = new BackupHelper();
                string backupPath = backupHelper.ExportBackup(autoBackupFolder);

                SetSetting(LastBackupDateKey, today.ToString("yyyy-MM-dd"));
                SetSetting(SettingsHelper.LastBackupDate, today.ToString("yyyy-MM-dd"));
                PruneOldBackups(autoBackupFolder);
                AuditLogger.Log("بکاپ خودکار", "Backup", 0, "", backupPath);
            }
            catch (Exception ex)
            {
                // آموزش — catch پر به جای catch خالی:
                // شکست backup نباید برنامه را crash کند ولی باید لاگ شود.
                string msg = $"[AutoBackupService] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | {ex.Message}";
                Debug.WriteLine(msg);
                TryWriteErrorLog(msg);
            }
        }

        // زمان‌بندی: Daily/Weekly/Monthly (پیش‌فرض Daily = رفتار قبلی بدون تغییر)
        private static int GetScheduleIntervalDays()
        {
            string schedule = SettingsHelper.Get(SettingsHelper.BackupSchedule, "Daily");
            switch (schedule)
            {
                case "Weekly":  return 7;
                case "Monthly": return 30;
                default:        return 1;
            }
        }

        // ─── حذف backup های قدیمی ───────────────────────────────────────────
        private static void PruneOldBackups(string autoBackupFolder)
        {
            try
            {
                int keepCount = SettingsHelper.GetInt(SettingsHelper.BackupRetentionCount, 14);
                DirectoryInfo root = new DirectoryInfo(autoBackupFolder);
                DirectoryInfo[] backups = root
                    .GetDirectories("CaseManagementBackup_*")
                    .OrderByDescending(d => d.CreationTimeUtc)
                    .ToArray();

                for (int i = keepCount; i < backups.Length; i++)
                {
                    try
                    {
                        backups[i].Delete(true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PruneOldBackups] Could not delete {backups[i].Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PruneOldBackups] {ex.Message}");
            }
        }

        private static string GetSetting(string key)
        {
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT SettingValue FROM TblAppSettings WHERE SettingKey = @Key", con))
            {
                cmd.Parameters.AddWithValue("@Key", key);
                con.Open();
                object result = cmd.ExecuteScalar();
                return (result == null || result == DBNull.Value) ? "" : result.ToString();
            }
        }

        private static void SetSetting(string key, string value)
        {
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT OR REPLACE INTO TblAppSettings (SettingKey, SettingValue, UpdatedAt)
VALUES (@Key, @Value, datetime('now'))", con))
            {
                cmd.Parameters.AddWithValue("@Key",   key);
                cmd.Parameters.AddWithValue("@Value", value ?? "");
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static void TryWriteErrorLog(string message)
        {
            try
            {
                string configured = SettingsHelper.Get(SettingsHelper.LogsPath);
                string folder = !string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)
                    ? configured
                    : AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(folder, "backup_errors.log");
                File.AppendAllText(path, message + Environment.NewLine);
            }
            catch { }
        }
    }
}

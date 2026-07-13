using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // خواندن/نوشتن تنظیمات عمومی نرم‌افزار از TblAppSettings (Key-Value)
    public static class SettingsHelper
    {
        public const string OrgName            = "OrgName";
        public const string LogoPath           = "LogoPath";
        public const string Address            = "Address";
        public const string Phone               = "Phone";
        public const string Email              = "Email";
        public const string ThemeColor         = "ThemeColor";
        public const string BackupPath         = "BackupPath";
        public const string PhotoStoragePath   = "PhotoStoragePath";
        public const string StartCaseNo        = "StartCaseNo";
        public const string StartReceiptNo     = "StartReceiptNo";
        public const string LastBackupDate     = "LastBackupDate";

        // ─── تب اطلاعات مؤسسه (بخش کنترل سنتر) ───────────────────────────────
        public const string OrgNameEn   = "OrgNameEn";
        public const string Slogan      = "Slogan";
        public const string OrgCode     = "OrgCode";
        public const string RegNumber   = "RegNumber";
        public const string ManagerName = "ManagerName";
        public const string Mobile      = "Mobile";
        public const string WhatsApp    = "WhatsApp";
        public const string Website     = "Website";
        public const string Description = "Description";

        // ─── تب شماره‌گذاری ────────────────────────────────────────────────────
        public const string StartFamilyNo = "StartFamilyNo";
        public const string StartDocNo    = "StartDocNo";
        public const string StartReportNo = "StartReportNo";

        // ─── تب مسیرها و فایل‌ها ───────────────────────────────────────────────
        public const string ReportsPath = "ReportsPath";
        public const string LogsPath    = "LogsPath";
        public const string TempPath    = "TempPath";

        // ─── تب ظاهر نرم‌افزار ─────────────────────────────────────────────────
        public const string SecondaryColor = "SecondaryColor";
        public const string HeaderColor    = "HeaderColor";
        public const string ButtonColor    = "ButtonColor";
        public const string WarningColor   = "WarningColor";
        public const string SuccessColor   = "SuccessColor";
        public const string DangerColor    = "DangerColor";
        public const string ThemeMode      = "ThemeMode"; // "Light" یا "Dark" — فقط بعد از ری‌استارت اعمال می‌شود
        public const string FontFamily     = "FontFamily";
        public const string FontSize       = "FontSize";
        public const string FontColor      = "FontColor";

        // ─── تب چاپ و گزارش ────────────────────────────────────────────────────
        public const string DefaultPrinter = "DefaultPrinter";
        public const string PaperSize      = "PaperSize";
        public const string MarginTop      = "MarginTop";
        public const string MarginBottom   = "MarginBottom";
        public const string MarginLeft     = "MarginLeft";
        public const string MarginRight    = "MarginRight";
        public const string ShowLogoOnPrint = "ShowLogoOnPrint";
        public const string ShowStamp      = "ShowStamp";
        public const string ShowSignature  = "ShowSignature";
        public const string StampPath      = "StampPath";
        public const string SignaturePath  = "SignaturePath";

        // ─── تب امنیت ──────────────────────────────────────────────────────────
        public const string MinPasswordLength      = "MinPasswordLength";
        public const string MaxFailedAttempts      = "MaxFailedAttempts";
        public const string LockoutMinutes         = "LockoutMinutes";
        public const string SessionTimeoutMinutes  = "SessionTimeoutMinutes";
        public const string ForcePasswordChangeDays = "ForcePasswordChangeDays";
        public const string AuditEnabled           = "AuditEnabled";

        // ─── تب Backup ─────────────────────────────────────────────────────────
        public const string BackupSchedule       = "BackupSchedule"; // Daily/Weekly/Monthly
        public const string BackupRetentionCount = "BackupRetentionCount";
        public const string LastRestoreDate      = "LastRestoreDate";

        // ─── تب اعلان‌ها (هرکدام پیش‌فرض روشن = رفتار فعلی بدون تغییر) ─────────
        public const string Notify_BackupMissing     = "Notify_BackupMissing";
        public const string Notify_LowDisk           = "Notify_LowDisk";
        public const string Notify_IncompleteCase    = "Notify_IncompleteCase";
        public const string Notify_NoPhoto           = "Notify_NoPhoto";
        public const string Notify_NoDocs            = "Notify_NoDocs";
        public const string Notify_IncompleteFamily  = "Notify_IncompleteFamily";
        public const string Notify_IncompleteFinance = "Notify_IncompleteFinance";

        // ─── تب درباره نرم‌افزار ───────────────────────────────────────────────
        public const string DeveloperName = "DeveloperName";
        public const string LicenseInfo   = "LicenseInfo";
        public const string MachineId     = "MachineId";

        private static readonly Dictionary<string, string> _cache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded = false;
        private static readonly object _lock = new object();

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_lock)
            {
                if (_loaded) return;
                try
                {
                    using (var con = new DatabaseHelper().GetConnection())
                    using (var cmd = new SQLiteCommand("SELECT SettingKey, SettingValue FROM TblAppSettings", con))
                    {
                        con.Open();
                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                                _cache[dr["SettingKey"].ToString()] = dr["SettingValue"] == DBNull.Value ? "" : dr["SettingValue"].ToString();
                        }
                    }
                }
                catch { /* دیتابیس هنوز آماده نیست */ }
                _loaded = true;
            }
        }

        public static string Get(string key, string defaultValue = "")
        {
            EnsureLoaded();
            string value;
            return _cache.TryGetValue(key, out value) ? value : defaultValue;
        }

        public static int GetInt(string key, int defaultValue)
        {
            int result;
            return int.TryParse(Get(key, ""), out result) ? result : defaultValue;
        }

        public static void Set(string key, string value)
        {
            EnsureLoaded();
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
INSERT INTO TblAppSettings (SettingKey, SettingValue, UpdatedAt)
VALUES (@Key, @Value, datetime('now'))
ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = @Value, UpdatedAt = datetime('now')", con))
            {
                cmd.Parameters.AddWithValue("@Key", key);
                cmd.Parameters.AddWithValue("@Value", value ?? "");
                con.Open();
                cmd.ExecuteNonQuery();
            }
            _cache[key] = value ?? "";
        }

        public static void ClearCache()
        {
            lock (_lock)
            {
                _cache.Clear();
                _loaded = false;
            }
        }
    }
}

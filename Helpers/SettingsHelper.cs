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

        // فایل جزوه آموزشی. خالی = همان جزوه‌ی همراهِ نصب
        // (Manual\TrainingManual.pdf). با مقداردهی، نسخه‌ی ویرایش‌شده‌ی خودِ
        // مؤسسه جایگزین می‌شود (PDF یا Word).
        public const string ManualPath  = "ManualPath";

        // ستون‌های انتخاب‌شده‌ی گریدِ لیست پرونده‌ها (CSV از کلیدهای
        // CaseGridColumns). خالی = پیش‌فرض. «کد اختصاصی» همیشه ثابت است و
        // اینجا ذخیره نمی‌شود.
        public const string CaseGridColumns = "CaseGridColumns";

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

        // تعداد ردیفِ کارت‌های آماریِ تب «داشبورد کل پرونده‌ها» (۲ تا ۴).
        // خالی/نامعتبر = ۲ (همان رفتار قبلی، بدون تغییر برای نصب‌های موجود).
        public const string DashboardSummaryRows = "DashboardSummaryRows";

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

        // ─── تب کارت شناسایی سرپرست ──────────────────────────────────────────
        // آموزش — این متن‌ها پیش‌تر داخل CardService.BuildCardData ثابت (hard-code)
        // بودند و مؤسسه نمی‌توانست تغییرشان دهد. حالا هرکدام یک کلید تنظیمات
        // دارند و CardService با «مقدار پیش‌فرض = همان متن قبلی» می‌خواندشان؛
        // پس تا وقتی کاربر چیزی وارد نکرده، کارت دقیقاً مثل قبل چاپ می‌شود.
        // مشخصاتِ خودِ سرپرست (نام، پدر، تذکره، کد، تعداد ایتام، بارکد) عمداً
        // اینجا نیست — آن‌ها داده‌ی پرونده‌اند و باید از دیتابیس بیایند.
        public const string Card_OrgName        = "Card_OrgName";        // خالی = نام مؤسسه از تب «مؤسسه»
        public const string Card_MicrotextLabel = "Card_MicrotextLabel"; // پیشوند نوار تزئینی دور کارت
        public const string Card_Notice1        = "Card_Notice1";
        public const string Card_Notice2        = "Card_Notice2";
        public const string Card_Notice3        = "Card_Notice3";
        public const string Card_Notice4        = "Card_Notice4";
        public const string Card_Notice5        = "Card_Notice5";
        public const string Card_SignatureLabel = "Card_SignatureLabel";
        public const string Card_LegalLine      = "Card_LegalLine";
        public const string Card_IssuedBy       = "Card_IssuedBy";       // خالی = نام کاربر جاری
        public const string Card_Position       = "Card_Position";       // خالی = نقش کاربر جاری
        public const string Card_Address        = "Card_Address";        // خالی = آدرس مؤسسه
        public const string Card_Phone          = "Card_Phone";          // خالی = تلفن مؤسسه
        public const string Card_Website        = "Card_Website";        // خالی = وب‌سایت مؤسسه
        public const string Card_Email          = "Card_Email";          // خالی = ایمیل مؤسسه

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

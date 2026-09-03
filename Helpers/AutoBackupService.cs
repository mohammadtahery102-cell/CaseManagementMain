using CaseManagement.DAL;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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

                // آموزش — نسخهٔ ۱٫۰ (Option D): بدون رمزِ تنظیم‌شده، بکاپِ خودکار
                // اجرا نمی‌شود — هرگز به نوشتنِ فایلِ متنِ‌سادهٔ بدون رمزنگاری
                // برنمی‌گردد. این یک تغییرِ رفتاریِ آگاهانه است: نبودِ بکاپ،
                // امن‌تر از بکاپِ رمزنگاری‌نشدهٔ خاموش است.
                string autoBackupPassword = GetAutoBackupPassword();
                if (string.IsNullOrEmpty(autoBackupPassword))
                {
                    string warn = $"[AutoBackupService] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | رمزِ بکاپِ خودکار تنظیم نشده — بکاپ رد شد (تنظیمات → پشتیبان‌گیری).";
                    Debug.WriteLine(warn);
                    TryWriteErrorLog(warn);
                    return;
                }

                string autoBackupFolder = !string.IsNullOrWhiteSpace(configuredBackupPath) && Directory.Exists(configuredBackupPath)
                    ? Path.Combine(configuredBackupPath, "AutoBackups")
                    : Path.Combine(root, "AutoBackups");
                Directory.CreateDirectory(autoBackupFolder);

                BackupHelper backupHelper = new BackupHelper();
                string backupPath = backupHelper.ExportEncryptedBackup(autoBackupFolder, autoBackupPassword);

                // آموزش — رفعِ یک شکافِ جدیِ کشف‌شده در ممیزیِ بکاپ:
                // BackupHelper اصلاً جداولِ Acc* را نمی‌گیرد (خودِ
                // AccountingBackupHelper در بالای فایلش این را توضیح داده) و
                // بکاپِ حسابداری تا امروز فقط دستی، از داخلِ فرمِ حسابداری،
                // قابل گرفتن بود. یعنی سایتی که فقط به بکاپِ خودکار تکیه کند،
                // هیچ محافظتی روی دادهٔ مالی نداشت — نه دوره، نه صندوق، نه
                // تراکنش، نه شهریه، نه حقوق.
                // این فراخوانی جدا و بعد از بکاپِ اصلی است تا اگر حسابداری به
                // هر دلیلی شکست بخورد، بکاپِ اصلی که همین الان با موفقیت گرفته
                // شده از دست نرود؛ خطا لاگ می‌شود و روال ادامه پیدا می‌کند.
                string accBackupPath = null;
                try
                {
                    accBackupPath = AccountingBackupHelper.ExportEncryptedBackup(autoBackupFolder, autoBackupPassword);
                }
                catch (Exception accEx)
                {
                    string accMsg = $"[AutoBackupService] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | بکاپِ خودکارِ حسابداری شکست خورد: {accEx.Message}";
                    Debug.WriteLine(accMsg);
                    TryWriteErrorLog(accMsg);
                }

                SetSetting(LastBackupDateKey, today.ToString("yyyy-MM-dd"));
                SetSetting(SettingsHelper.LastBackupDate, today.ToString("yyyy-MM-dd"));
                PruneOldBackups(autoBackupFolder);
                AuditLogger.Log("بکاپ خودکار (رمزنگاری‌شده)", "Backup", 0, "",
                    accBackupPath == null ? backupPath : backupPath + " + " + accBackupPath);
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
        // آموزش — نسخهٔ ۱٫۰: بکاپِ خودکار حالا یک فایلِ تکِ رمزنگاری‌شده
        // می‌سازد (پسوندِ .cmbak)، نه یک پوشه؛ الگوی جست‌وجو مطابق آن عوض شد.
        private static void PruneOldBackups(string autoBackupFolder)
        {
            // آموزش — حالا بکاپِ خودکار دو فایل می‌سازد (اصلی + حسابداری). اگر
            // نگه‌داری فقط روی پیشوندِ اصلی اجرا می‌شد، فایل‌های حسابداری تا
            // ابد انباشته می‌شدند و دیسک را پر می‌کردند. هر پیشوند جداگانه
            // هَرَس می‌شود تا از هر کدام دقیقاً keepCount نسخه بماند — یعنی
            // بکاپِ حسابداریِ متناظرِ هر بکاپِ اصلی هم باقی می‌ماند.
            PruneByPrefix(autoBackupFolder, "CaseManagementBackup_");
            PruneByPrefix(autoBackupFolder, "AccountingBackup_");
        }

        private static void PruneByPrefix(string autoBackupFolder, string prefix)
        {
            try
            {
                int keepCount = SettingsHelper.GetInt(SettingsHelper.BackupRetentionCount, 14);
                DirectoryInfo root = new DirectoryInfo(autoBackupFolder);
                FileInfo[] backups = root
                    .GetFiles(prefix + "*" + BackupEncryption.EncryptedExtension)
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .ToArray();

                for (int i = keepCount; i < backups.Length; i++)
                {
                    try
                    {
                        backups[i].Delete();
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

        // ─── رمزِ بکاپِ خودکار (نگهداری با DPAPI) ───────────────────────────
        private static string GetAutoBackupPassword()
        {
            string protectedValue = SettingsHelper.Get(SettingsHelper.AutoBackupPasswordProtected, "");
            return UnprotectPassword(protectedValue);
        }

        // آموزش — چرا LocalMachine نه CurrentUser: بکاپِ خودکار در پس‌زمینه و
        // گاهی زیرِ حسابِ ویندوزِ متفاوتی اجرا می‌شود؛ CurrentUser باعث می‌شد
        // با تعویض کاربرِ ویندوز یا اجرای برنامه به‌عنوانِ سرویس، رمزِ ذخیره‌شده
        // دیگر قابلِ بازیابی نباشد. LocalMachine محدودتر از رمزِ متنِ‌ساده است
        // (نیازمندِ دسترسیِ محلی به همان دستگاه) ولی همان دستگاه/نصب را
        // پوشش می‌دهد — دقیقاً همان‌جا که بکاپِ خودکار اجرا می‌شود.
        public static string ProtectPassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword)) return "";

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainPassword);
            byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(protectedBytes);
        }

        public static string UnprotectPassword(string protectedBase64)
        {
            if (string.IsNullOrEmpty(protectedBase64)) return "";

            try
            {
                byte[] protectedBytes = Convert.FromBase64String(protectedBase64);
                byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                // رمزِ ذخیره‌شده خراب/غیرقابل‌بازیابی است (مثلاً بعد از انتقالِ
                // نصب به دستگاهی دیگر) — رفتارِ ایمن، خالی برگرداندن است، نه پرتاب خطا.
                return "";
            }
        }

        public static bool IsAutoBackupPasswordConfigured()
        {
            return !string.IsNullOrEmpty(GetAutoBackupPassword());
        }

        // فراخوانی از صفحهٔ تنظیمات، برای ذخیرهٔ رمزِ تازه (یا پاک‌کردن با رشتهٔ خالی).
        //
        // آموزش — چرا SettingsHelper.Set و نه متدِ خصوصیِ SetSetting در همین
        // کلاس: SettingsHelper یک کَشِ درون‌حافظه‌ای دارد (_cache) که فقط با
        // فراخوانیِ خودِ SettingsHelper.Set به‌روز می‌شود. نوشتنِ مستقیم با SQL
        // خام (همان‌طور که ابتدا اینجا انجام شده بود) دیتابیس را درست به‌روز
        // می‌کرد ولی کَش را نه — یعنی GetAutoBackupPassword (که از
        // SettingsHelper.Get می‌خواند) بلافاصله بعد از ذخیره، مقدارِ خالیِ
        // بایگانی‌شده را برمی‌گرداند. این باگ با آزمون پیدا و همین‌جا رفع شد.
        public static void SetAutoBackupPassword(string plainPassword)
        {
            SettingsHelper.Set(SettingsHelper.AutoBackupPasswordProtected,
                string.IsNullOrEmpty(plainPassword) ? "" : ProtectPassword(plainPassword));
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

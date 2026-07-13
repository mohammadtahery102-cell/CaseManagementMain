using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Set application culture to Persian (Jalali) with PersianCalendar
            try
            {
                var ci = CaseManagement.Helpers.PersianDateHelper.GetPersianCulture();
                System.Threading.Thread.CurrentThread.CurrentCulture = ci;
                System.Threading.Thread.CurrentThread.CurrentUICulture = ci;
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                DatabaseInitializer.EnsureDatabaseObjects();
                // ماژول حسابداری داخلی ایتام — ساخت جداول Acc* (کاملاً افزایشی؛
                // هیچ جدول موجود نرم‌افزار پرونده را تغییر نمی‌دهد).
                AccountingInitializer.EnsureAccountingObjects();
                AutoBackupService.RunDailyBackupIfDue();
                ApplyOrganizationTheme();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در آماده‌سازی سیستم: " + ex.Message);
                return;
            }

            using (FrmLogin login = new FrmLogin())
            {
                if (login.ShowDialog() != DialogResult.OK)
                    return;
            }

            CaseManagement.Helpers.SessionTimeoutMonitor.Start();
            Application.Run(new FrmDashboard());
        }

        // اعمال «رنگ سازمانی» + پالت کامل + فونت/اندازه ذخیره‌شده در تنظیمات
        // روی کل تم برنامه (بند ۴ بازطراحی ظاهری + تب «ظاهر نرم‌افزار»).
        // آموزش — چرا فقط یک‌بار موقع شروع: چون UiTheme از static field ساده
        // استفاده می‌کند (نه یک سیستم Theming زنده)، تغییر رنگ/فونت از تنظیمات
        // فقط بعد از باز کردن دوباره برنامه روی همه پنجره‌ها اعمال می‌شود؛
        // این نکته در خود تب «ظاهر نرم‌افزار» به کاربر گفته می‌شود.
        private static void ApplyOrganizationTheme()
        {
            string colorHex = SettingsHelper.Get(SettingsHelper.ThemeColor);
            if (!string.IsNullOrWhiteSpace(colorHex))
            {
                try { UiTheme.ApplyOrgColor(System.Drawing.ColorTranslator.FromHtml(colorHex)); }
                catch { /* رنگ نامعتبر است؛ رنگ پیش‌فرض حفظ می‌شود */ }
            }

            UiTheme.ApplyFullPalette(
                TryParseColor(SettingsHelper.Get(SettingsHelper.SuccessColor)),
                TryParseColor(SettingsHelper.Get(SettingsHelper.DangerColor)),
                TryParseColor(SettingsHelper.Get(SettingsHelper.WarningColor)),
                TryParseColor(SettingsHelper.Get(SettingsHelper.FontColor)),
                null, null);

            string fontFamily = SettingsHelper.Get(SettingsHelper.FontFamily);
            if (!string.IsNullOrWhiteSpace(fontFamily))
                UiTheme.ApplyFontPreference(fontFamily);

            int fontSizePercent = SettingsHelper.GetInt(SettingsHelper.FontSize, 100);
            if (fontSizePercent > 0)
                UiTheme.SizeScale = fontSizePercent / 100f;
        }

        private static System.Drawing.Color? TryParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            try { return System.Drawing.ColorTranslator.FromHtml(hex); }
            catch { return null; }
        }
    }
}

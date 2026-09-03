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

            // آموزش — رفعِ باگِ واقعیِ کشف‌شده: بدونِ این فراخوانی، هر گزارشِ RDLC
            // (الگوی قدیمیِ پرونده — چه پیش‌نمایشِ تعاملی، چه خروجیِ مستقیمِ
            // PDF/Word) با FileNotFoundException روی Microsoft.SqlServer.Types
            // شکست می‌خورد. باید پیش از هر استفاده‌ای از LocalReport اجرا شود.
            try { SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory); }
            catch { /* اگر خودِ RDLC استفاده نشود، نبودنش نباید کل برنامه را متوقف کند */ }

            // راست‌چینیِ عنوان‌ها در همه‌ی فرم‌ها و پنجره‌ها. عمداً همین‌جا (پیش از
            // فرمِ ورود) نصب می‌شود تا هر پنجره‌ای — از جمله دیالوگ‌های modal —
            // پوشش داده شود. توضیح کامل در Helpers/RtlCaptions.cs.
            RtlCaptions.Install();

            // چندزبانگی: زبانِ ذخیره‌شده بارگذاری و روی هر پنجره‌ای که باز شود
            // خودکار اعمال می‌شود. اگر زبان «فارسی» باشد هیچ ترجمه‌ای انجام
            // نمی‌شود و رفتار برنامه دقیقاً مثل قبل است.
            Lang.Initialize();
            LanguageSweep.Install();

            try
            {
                DatabaseInitializer.EnsureDatabaseObjects();
                // ماژول حسابداری داخلی ایتام — ساخت جداول Acc* (کاملاً افزایشی؛
                // هیچ جدول موجود نرم‌افزار پرونده را تغییر نمی‌دهد).
                AccountingInitializer.EnsureAccountingObjects();
                // ماژول اداری و کارمندان — ساخت جداول Adm* (رخصتی، ماموریت،
                // درخواست استخدام، قرارداد ترانسپورت). مثل حسابداری کاملاً
                // افزایشی است و هیچ جدول Acc* یا جدول پرونده را لمس نمی‌کند.
                AdminInitializer.EnsureAdminObjects();
                // فاز یک «هسته سازمانی» — ساخت جداول Ent* (گردش‌کار، تأیید
                // چندسطحی، وظایف، قواعد، قفل رکورد، نسخه‌ها، لاگ‌ها، مجوزها،
                // ماژول‌ها). مثل حسابداری کاملاً افزایشی است و هیچ جدول موجودی
                // را تغییر نمی‌دهد.
                CaseManagement.Enterprise.EnterpriseInitializer.EnsureEnterpriseObjects();

                // زیرساخت «کار آفلاین و همگام‌سازی» — صفِ محلیِ عملیات و
                // ستون‌های ردیابیِ نسخه/آخرین تغییر. مثل دو مورد بالا کاملاً
                // افزایشی است (دو جدول Sync* و چند ستون جدید) و هیچ اتصال
                // شبکه‌ای برقرار نمی‌کند.
                CaseManagement.Sync.OfflineSyncInitializer.EnsureOfflineSyncObjects();

                // دستیار هوشمند — فاز ۱: جدول‌های Ai* (گفتگو/پیام/گزارش قصد) و
                // نمایه‌ی جست‌وجوی FTS5. کاملاً افزایشی؛ بعد از Enterprise چون به
                // TblAuditLog برای آشتیِ یادآوری‌ها نیاز دارد.
                CaseManagement.Helpers.AiInitializer.EnsureAiObjects();

                // ویژگی ۸ — ثبت متمرکز خطاها: از این لحظه به بعد هر استثنای
                // گرفته‌نشده ثبت می‌شود و برنامه به‌جای بسته شدن ناگهانی، پیام
                // فارسی مناسب نشان می‌دهد. عمداً بعد از ساخت جداول نصب می‌شود
                // چون خودش برای ثبت به جدول EntErrorLog نیاز دارد.
                CaseManagement.Enterprise.ErrorLogger.Install();

                // مرکز کنترل توسعه‌دهنده (مخفی) — فقط یک فیلترِ پیامِ صفحه‌کلید
                // نصب می‌شود. هیچ گزینه‌ای به منو/نوار کناری/داشبورد/تنظیمات
                // اضافه نمی‌کند و برای هر کاربری غیر از «مدیر کل» کاملاً بی‌اثر
                // است (توضیح کامل در DevCenter/DevCenterAccess.cs).
                CaseManagement.DevCenter.DevCenterAccess.Install();

                AutoBackupService.RunDailyBackupIfDue();
                ApplyOrganizationTheme();
            }
            catch (Exception ex)
            {
                // خطای آماده‌سازی هم ثبت می‌شود (اگر دیتابیس در دسترس نباشد،
                // ErrorLogger خودش روی فایل متنی می‌نویسد).
                CaseManagement.Enterprise.ErrorLogger.Log(
                    ex, "Program.Startup", null,
                    CaseManagement.Enterprise.ErrorLogger.SeverityCritical);

                Msg.Show("خطا در آماده‌سازی سیستم: " + ex.Message);
                return;
            }

            using (FrmLogin login = new FrmLogin())
            {
                if (login.ShowDialog() != DialogResult.OK)
                    return;
            }

            CaseManagement.Helpers.SessionTimeoutMonitor.Start();

            // فاز B — همگام‌سازیِ پس‌زمینه. عمداً *پس از* ورود شروع می‌شود:
            // پیش از آن نه مرکزی مشخص است و نه مجوزی، و همگام‌سازی بدون هویت
            // یعنی نادیده گرفتنِ مرزِ دسترسی. اگر آدرس سروری تنظیم نشده باشد،
            // مدیر خودش می‌فهمد و هیچ اتصالی برقرار نمی‌کند — رفتار آفلاینِ
            // برنامه دقیقاً مثل قبل می‌ماند.
            try { CaseManagement.Sync.BackgroundSyncManager.Start(); }
            catch (Exception ex)
            {
                CaseManagement.Enterprise.ErrorLogger.Log(ex, "Program.StartBackgroundSync");
            }

            try
            {
                Application.Run(new FrmDashboard());
            }
            finally
            {
                // ⚠ توقفِ مرتب هنگام خروج: اجرای در جریان لغو می‌شود تا بستنِ
                // برنامه معلق نماند. لغو داده‌ای را از بین نمی‌برد — همهٔ صف‌ها
                // در پایگاه‌داده‌اند و اجرای بعدی از همان‌جا ادامه می‌دهد.
                try { CaseManagement.Sync.BackgroundSyncManager.Stop(); } catch { }
            }
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

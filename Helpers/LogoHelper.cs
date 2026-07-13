using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

namespace CaseManagement.Helpers
{
    // لوگوی نرم‌افزار: اگر مدیر از تنظیمات یک فایل لوگو انتخاب کرده باشد همان
    // استفاده می‌شود؛ در غیر این صورت یک نماد ساده (حرف اول نام مؤسسه، داخل
    // دایره‌ای به رنگ سازمانی) به‌صورت خودکار ساخته می‌شود تا همه فرم‌ها
    // (Login/Dashboard/About) همیشه یک آیکون/لوگوی یکسان و مناسب داشته باشند.
    public static class LogoHelper
    {
        private static Image _cachedLogo;
        private static Icon _cachedIcon;
        private static IntPtr _cachedIconHandle = IntPtr.Zero;
        private static string _cachedLogoPath;

        // آموزش — رفع نشت GDI: Icon.FromHandle مالک HICON نمی‌شود (طبق مستندات
        // مایکروسافت)، پس Icon.Dispose() آن را آزاد نمی‌کند. باید صراحتاً با
        // DestroyIcon آزاد شود؛ وگرنه هر بار که ادمین لوگو را در یک نشست عوض
        // می‌کند (ClearCache) یک HICON برای همیشه نشت می‌کند.
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        // ─── تصویر لوگو برای نمایش در بنر فرم‌ها (Login/Dashboard/About) ────
        public static Image GetLogoImage()
        {
            string logoPath = SettingsHelper.Get(SettingsHelper.LogoPath);

            if (_cachedLogo != null && _cachedLogoPath == logoPath)
                return _cachedLogo;

            DisposeCache();
            _cachedLogoPath = logoPath;

            if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    using (var fs = new FileStream(logoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var img = Image.FromStream(fs))
                        _cachedLogo = new Bitmap(img);

                    return _cachedLogo;
                }
                catch { /* فایل لوگو خراب/نامعتبر است؛ به نماد پیش‌فرض برمی‌گردیم */ }
            }

            _cachedLogo = GenerateMonogram();
            return _cachedLogo;
        }

        // ─── آیکون فرم (Form.Icon) — همان لوگو، تبدیل‌شده به Icon ───────────
        public static Icon GetAppIcon()
        {
            string logoPath = SettingsHelper.Get(SettingsHelper.LogoPath);

            if (_cachedIcon != null && _cachedLogoPath == logoPath)
                return _cachedIcon;

            using (Bitmap bmp = new Bitmap(GetLogoImage(), new Size(32, 32)))
            {
                IntPtr hIcon = bmp.GetHicon();
                _cachedIcon = Icon.FromHandle(hIcon);
                _cachedIconHandle = hIcon;
            }

            return _cachedIcon;
        }

        public static void ClearCache()
        {
            DisposeCache();
            _cachedLogoPath = null;
        }

        private static void DisposeCache()
        {
            if (_cachedLogo != null) { _cachedLogo.Dispose(); _cachedLogo = null; }
            if (_cachedIcon != null) { _cachedIcon.Dispose(); _cachedIcon = null; }
            if (_cachedIconHandle != IntPtr.Zero) { DestroyIcon(_cachedIconHandle); _cachedIconHandle = IntPtr.Zero; }
        }

        private static Bitmap GenerateMonogram()
        {
            string orgName = SettingsHelper.Get(SettingsHelper.OrgName);
            char letter = string.IsNullOrWhiteSpace(orgName) ? 'س' : orgName.Trim()[0];

            Bitmap bmp = new Bitmap(128, 128);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.Clear(Color.Transparent);

                using (Brush circleBrush = new SolidBrush(UiTheme.Primary))
                    g.FillEllipse(circleBrush, 4, 4, 120, 120);

                using (Font font = new Font("Segoe UI", 56F, FontStyle.Bold))
                using (Brush textBrush = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(letter.ToString(), font, textBrush, new RectangleF(0, 0, 128, 128), sf);
                }
            }

            return bmp;
        }
    }
}

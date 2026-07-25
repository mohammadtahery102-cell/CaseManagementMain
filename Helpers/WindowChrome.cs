using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // پشتیبانی از «پنجره‌ی بدون قابِ ویندوز» برای فرم‌هایی که نوار عنوان
    // سفارشی دارند: گوشه‌های گردِ بومیِ ویندوز ۱۱، جابه‌جایی پنجره با کشیدنِ
    // نوار عنوان، و تغییر اندازه از لبه‌ها.
    //
    // آموزش — چرا از API خودِ ویندوز استفاده می‌کنیم و نه Region: با Region
    // گوشه‌ها پله‌پله (بدون ضدلبه‌دندانه) بریده می‌شوند و سایه‌ی پنجره هم از
    // بین می‌رود. DwmSetWindowAttribute گوشه‌ی گردِ واقعیِ سیستم را می‌دهد
    // (با سایه و ضدلبه‌دندانه‌ی درست). روی ویندوز ۱۰ این ویژگی وجود ندارد و
    // فراخوانی بی‌اثر شکست می‌خورد — که کاملاً بی‌ضرر است و پنجره فقط
    // گوشه‌تیز می‌ماند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class WindowChrome
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        // گوشه‌ی گردِ بومی (ویندوز ۱۱). روی نسخه‌های قدیمی‌تر بی‌صدا نادیده گرفته می‌شود.
        public static void ApplyRoundedCorners(Form form)
        {
            try
            {
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch { /* ویندوز قدیمی‌تر — گوشه‌ها تیز می‌ماند، بدون هیچ مشکلی */ }
        }

        // آموزش — جابه‌جایی پنجره از روی یک کنترل (مثل نوار عنوان سفارشی):
        // به‌جای دنبال‌کردنِ دستیِ مختصات ماوس (که پرش و لختی دارد)، به ویندوز
        // می‌گوییم «انگار کاربر نوار عنوانِ واقعی را گرفته» — نتیجه دقیقاً همان
        // رفتار نرمِ بومی، شاملِ Snap به لبه‌های صفحه.
        public static void EnableDragMove(Control dragHandle, Form form)
        {
            dragHandle.MouseDown += delegate (object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            };
        }

        // دوبار کلیک روی نوار عنوان = بیشینه/بازگردانی (رفتار استاندارد ویندوز).
        public static void EnableDoubleClickMaximize(Control dragHandle, Form form)
        {
            dragHandle.DoubleClick += delegate
            {
                form.WindowState = form.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };
        }
    }
}

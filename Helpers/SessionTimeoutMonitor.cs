using System;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // پایان خودکار نشست بعد از مدتی بی‌فعالیتی (Timeout نشست — تب امنیت).
    // آموزش — قبلاً چنین قابلیتی در پروژه اصلاً وجود نداشت. برای پوشش کامل
    // فعالیت کاربر (حتی داخل دیالوگ‌های Modal مثل FrmCase/FrmFamily، نه فقط
    // داشبورد)، از IMessageFilter سراسری استفاده شده که هر پیام ماوس/کیبورد
    // را در کل برنامه می‌بیند، نه فقط یک فرم خاص.
    // رفتار وقتی Timeout برسد: به‌جای تلاش پرریسک برای بستن دیالوگ‌های باز و
    // بازگشت به صفحه ورود (که می‌تواند کار ذخیره‌نشده را در وضعیت نامشخص
    // رها کند)، ساده‌ترین و امن‌ترین رفتار انتخاب شد: هشدار و بستن کامل
    // برنامه؛ کاربر باید دوباره وارد شود.
    // ─────────────────────────────────────────────────────────────────────────
    public static class SessionTimeoutMonitor
    {
        private class ActivityFilter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m)
            {
                const int WM_MOUSEMOVE = 0x0200;
                const int WM_LBUTTONDOWN = 0x0201;
                const int WM_RBUTTONDOWN = 0x0204;
                const int WM_KEYDOWN = 0x0100;
                const int WM_MOUSEWHEEL = 0x020A;

                if (m.Msg == WM_MOUSEMOVE || m.Msg == WM_LBUTTONDOWN ||
                    m.Msg == WM_RBUTTONDOWN || m.Msg == WM_KEYDOWN || m.Msg == WM_MOUSEWHEEL)
                {
                    _lastActivity = DateTime.Now;
                }

                return false; // پیام همچنان به مقصد اصلی‌اش می‌رسد؛ فقط مشاهده می‌شود
            }
        }

        private static DateTime _lastActivity = DateTime.Now;
        private static Timer _timer;
        private static ActivityFilter _filter;
        private static bool _started;

        public static void Start()
        {
            if (_started) return;
            _started = true;

            _filter = new ActivityFilter();
            Application.AddMessageFilter(_filter);
            _lastActivity = DateTime.Now;

            _timer = new Timer();
            _timer.Interval = 30000; // هر ۳۰ ثانیه بررسی می‌شود
            _timer.Tick += delegate { CheckTimeout(); };
            _timer.Start();
        }

        public static void Stop()
        {
            if (!_started) return;
            _started = false;

            if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
            if (_filter != null) { Application.RemoveMessageFilter(_filter); _filter = null; }
        }

        private static void CheckTimeout()
        {
            int minutes = SettingsHelper.GetInt(SettingsHelper.SessionTimeoutMinutes, 0);
            if (minutes <= 0) return; // ۰ = غیرفعال (پیش‌فرض؛ رفتار قبلی بدون تغییر)

            if ((DateTime.Now - _lastActivity).TotalMinutes < minutes) return;

            Stop();
            Msg.Show(
                "به دلیل عدم فعالیت، نشست شما به پایان رسید. برای ادامه دوباره وارد شوید.",
                "پایان نشست",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            Application.Exit();
        }
    }
}

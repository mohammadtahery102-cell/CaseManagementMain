using System;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.DevCenter
{
    // ═════════════════════════════════════════════════════════════════════════
    // دروازهٔ مخفیِ «مرکز کنترل توسعه‌دهنده»
    //
    // آموزش — چرا IMessageFilter و نه هوک روی فرم‌ها:
    // خواستهٔ صریح این است که این قابلیت در هیچ منو، نوار کناری، داشبورد،
    // تنظیمات یا جست‌وجویی دیده نشود و فقط با یک میان‌بُر مخفی باز شود. اگر
    // میان‌بُر را روی فرم‌ها می‌بستیم باید KeyPreview و رویداد KeyDown چند فرم
    // را دست می‌زدیم — یعنی تغییر در فرم‌های موجود و ریسکِ تداخل با کلیدهای
    // فعلی. یک IMessageFilter در سطح Application پیام‌های صفحه‌کلید را پیش از
    // رسیدن به هر فرم می‌بیند، پس:
    //   • هیچ فرم موجودی تغییر نمی‌کند (صفر تغییر در UI).
    //   • میان‌بُر در هر پنجره‌ای کار می‌کند.
    //   • چون فقط همان یک ترکیب را مصرف می‌کند، رفتار بقیهٔ کلیدها دست‌نخورده
    //     می‌ماند (برای هر پیام دیگری false برمی‌گرداند).
    //
    // میان‌بُر: Ctrl + Shift + Alt + D
    // این ترکیب عمداً چهار-کلیدی است تا تصادفی فشرده نشود و با هیچ میان‌بُر
    // استاندارد ویندوز/برنامه تداخل نداشته باشد.
    //
    // ⚠ امنیت: خودِ میان‌بُر «راز» نیست — کنترل واقعی روی نقش کاربر است.
    // برای هر کاربری غیر از مدیر کل (SuperAdmin) فشردن میان‌بُر هیچ اثری
    // ندارد: نه پنجره‌ای باز می‌شود، نه پیامی داده می‌شود، نه خطایی. یعنی
    // کاربر عادی حتی نمی‌فهمد چنین چیزی وجود دارد.
    // ═════════════════════════════════════════════════════════════════════════
    public static class DevCenterAccess
    {
        private const int WM_KEYDOWN    = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;   // وقتی Alt نگه داشته شده

        private static bool _installed;
        private static FrmDevCenter _open;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            Application.AddMessageFilter(new HotKeyFilter());
        }

        // تنها راه باز شدن. اگر کاربر مدیر کل نباشد بی‌صدا رد می‌شود.
        public static void TryOpen(IWin32Window owner)
        {
            if (!SecurityContext.IsSuperAdmin())
                return;   // کاملاً بی‌صدا — هیچ نشانه‌ای از وجود این قابلیت

            try
            {
                // اگر از قبل باز است، همان پنجره جلو آورده می‌شود (نه نمونهٔ دوم).
                if (_open != null && !_open.IsDisposed)
                {
                    // فقط اگر کوچک شده باشد بازگردانده می‌شود؛ نسخهٔ قبلی
                    // بی‌قیدوشرط Normal می‌کرد و پنجرهٔ تمام‌صفحه را با هر بار
                    // فشردن میان‌بُر از حالت بیشینه بیرون می‌آورد.
                    if (_open.WindowState == FormWindowState.Minimized)
                        _open.WindowState = FormWindowState.Normal;

                    _open.BringToFront();
                    _open.Activate();
                    return;
                }

                // ثبت لاگ *پس از* ساخت موفق فرم: اگر ساخت شکست بخورد، ردّ
                // ممیزی نباید ادعا کند پنجره باز شده است.
                FrmDevCenter form = new FrmDevCenter();
                DevCenterService.LogAction("باز کردن مرکز کنترل توسعه‌دهنده");

                _open = form;
                _open.FormClosed += delegate { _open = null; };
                _open.Show(owner);
            }
            catch (Exception ex)
            {
                _open = null;
                Enterprise.ErrorLogger.Log(ex, "DevCenterAccess.TryOpen");
            }
        }

        private sealed class HotKeyFilter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_KEYDOWN && m.Msg != WM_SYSKEYDOWN)
                    return false;

                Keys key = (Keys)m.WParam.ToInt32() & Keys.KeyCode;
                if (key != Keys.D)
                    return false;

                if ((Control.ModifierKeys & (Keys.Control | Keys.Shift | Keys.Alt))
                    != (Keys.Control | Keys.Shift | Keys.Alt))
                    return false;

                // فقط برای مدیر کل مصرف می‌شود؛ برای بقیه پیام دست‌نخورده
                // به مقصد اصلی‌اش می‌رود تا هیچ تفاوت رفتاری حس نشود.
                if (!SecurityContext.IsSuperAdmin())
                    return false;

                TryOpen(Form.ActiveForm);
                return true;   // پیام مصرف شد
            }
        }
    }
}

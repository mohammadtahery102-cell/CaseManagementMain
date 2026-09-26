using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // TabControl بدون نوارِ تبِ بومیِ ویندوز. نوارِ دیده شده را PillTabStrip
    // می‌سازد (از راست شروع می‌شود). این کنترل فقط صفحات را نگه می‌دارد.
    //
    // TCM_ADJUSTRECT (0x1328) فضایی را که TabControl برای هدر تب‌ها رزرو
    // می‌کند برمی‌گرداند؛ بلعیدن پیام باعث می‌شود صفحه تمامِ کنترل را پر کند.
    public class HeadlessTabControl : TabControl
    {
        private const int TcmAdjustRect = 0x1328;

        public HeadlessTabControl()
        {
            Appearance = TabAppearance.FlatButtons;
            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(0, 1);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == TcmAdjustRect && !DesignMode)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
    }
}

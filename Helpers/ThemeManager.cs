using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // ThemeManager — لایه‌ی نازک روی UiTheme، نقطه‌ی واحد برای دو چیز:
    //   ۱) رنگ‌های «سطحِ پوسته» (Shell) — همان hex هایی که قبلاً مستقل در
    //      SidebarNav هاردکد شده بودند (BackDark/BackDarker/ItemText/GroupText)
    //      و از UiTheme.ApplyOrgColor بی‌خبر می‌ماندند. مقدارها این‌جا دقیقاً
    //      همان hex قبلی‌اند — صفر تغییرِ ظاهری، فقط منبعِ واحد.
    //   ۲) تنظیمِ استانداردِ فرم (راست‌چین + فونت) برای BaseForm.
    // فاز ۱ (زیرساخت) — هیچ فرمِ موجودی هنوز از این کلاس استفاده نمی‌کند؛
    // مهاجرت فرم‌ها فازهای بعدی است.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ThemeManager
    {
        public static Color ShellSurface     = ColorTranslator.FromHtml("#16213E");
        public static Color ShellSurfaceDark = ColorTranslator.FromHtml("#101A31");
        public static Color ShellItemText    = ColorTranslator.FromHtml("#B9C2D8");
        public static Color ShellGroupText   = ColorTranslator.FromHtml("#6C7A99");

        // اندازه‌ی ثابت (UiTheme.MakeFixedSize) عمداً این‌جا نیست: هر فرم
        // اندازه‌ی طراحیِ خودش را دارد و همان‌طور که امروز هست خودش صدا می‌زند.
        public static void ApplyStandardFormSetup(Form form)
        {
            form.RightToLeft = RightToLeft.Yes;
            form.RightToLeftLayout = true;
            form.Font = UiTheme.Font(UiTheme.SizeBody);
            form.BackColor = UiTheme.Background;
        }
    }
}

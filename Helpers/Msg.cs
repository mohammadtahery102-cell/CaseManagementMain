using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // آموزش — رفع باگ چپ‌به‌راست بودن MessageBox: بر خلاف فرم‌های خودِ برنامه،
    // MessageBox.Show یک پنجرهٔ کاملاً بومی و جداگانه است که RightToLeft فرم
    // مادر را ارث نمی‌برد؛ برای همین همیشه متن و دکمه‌ها را چپ‌به‌راست نشان
    // می‌داد. راه‌حل استاندارد ویندوز، دو پرچم MessageBoxOptions.RtlReading و
    // RightAlign است که با هم هم جهتِ خواندنِ متن و هم ترتیب دکمه‌های
    // OK/Cancel/Yes/No را کامل آینه می‌کنند (مطابق استاندارد فارسی).
    //
    // ═════════════════════════════════════════════════════════════════════════
    // بخش ۶ — فارسی‌سازی: چرا دیگر از MessageBox بومی استفاده نمی‌شود
    //
    // آینه‌کردن جهت، متنِ دکمه‌ها را فارسی نمی‌کرد. برچسب دکمه‌های
    // OK / Cancel / Yes / No را خودِ ویندوز می‌سازد و از زبانِ نصبِ ویندوز
    // می‌آید — نه از تنظیمات نرم‌افزار. روی هر ویندوز انگلیسی (که رایج‌ترین
    // حالت است) کاربر در وسطِ یک برنامهٔ کاملاً فارسی، دکمه‌های انگلیسی
    // می‌دید. همین تنها منبعِ باقی‌ماندهٔ متنِ انگلیسی در رابط کاربری بود.
    //
    // راه‌حل: همین کلاس — که از قبل تنها دروازهٔ پیام‌های برنامه است — به
    // دیالوگ‌های فارسیِ خودِ پروژه (UiTheme) هدایت شد. مزیت این نقطه‌ی واحد
    // این است که همهٔ صدها فراخوانیِ Msg.Show در سراسر پروژه بدون هیچ تغییری
    // فارسی می‌شوند و DialogResult برگشتی هم دقیقاً مثل قبل است، پس هیچ
    // منطق شرطی‌ای نمی‌شکند.
    //
    // ⚠ زبان‌های دیگر: متنِ دکمه‌ها از Lang.T می‌گذرد، پس اگر کاربر از
    // «تنظیمات» زبان را به انگلیسی (یا پشتو/عربی/اردو) تغییر دهد، دکمه‌ها هم
    // با بقیهٔ برنامه هماهنگ ترجمه می‌شوند — دقیقاً همان خواستهٔ بخش ۶.
    // ═════════════════════════════════════════════════════════════════════════
    //
    // این کلاس همان امضاهای متداول MessageBox.Show را دارد؛ برای رفع مشکل در
    // کل پروژه فقط کافی است هرجا MessageBox.Show( صدا زده می‌شد، به Msg.Show(
    // تغییر کند — بدون هیچ تغییری در آرگومان‌ها یا رفتار دکمه‌ها.
    public static class Msg
    {
        private const MessageBoxOptions Rtl = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

        public static DialogResult Show(string text)
        {
            return Show(text, "", MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        public static DialogResult Show(string text, string caption)
        {
            return Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons)
        {
            return Show(text, caption, buttons, MessageBoxIcon.None);
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(text, caption, buttons, icon, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            return UiTheme.ShowLocalizedDialog(null, text, caption, buttons, icon);
        }

        // اگر جایی صراحتاً MessageBoxOptions هم داده باشد، رفتار یکسان می‌ماند؛
        // پرچم‌های جهت در دیالوگ فارسی از قبل رعایت شده‌اند و این پارامتر فقط
        // برای حفظ سازگاری امضاها نگه داشته شده است.
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
        {
            return UiTheme.ShowLocalizedDialog(null, text, caption, buttons, icon);
        }

        // نسخه‌های دارای owner — دیالوگ روی همان پنجره وسط‌چین می‌شود.
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return UiTheme.ShowLocalizedDialog(owner, text, caption, buttons, icon);
        }

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            return UiTheme.ShowLocalizedDialog(owner, text, caption, buttons, icon);
        }

        // ─── پیام بومیِ ویندوز (فقط برای موارد نادرِ خارج از UI Thread) ──────
        // نگه داشته شد تا اگر جایی واقعاً به MessageBox بومی نیاز بود، همچنان
        // با جهتِ درست در دسترس باشد. در مسیرهای عادی استفاده نمی‌شود.
        public static DialogResult ShowNative(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return MessageBox.Show(text, caption, buttons, icon, MessageBoxDefaultButton.Button1, Rtl);
        }
    }
}

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
    // این کلاس همان امضاهای متداول MessageBox.Show را دارد؛ برای رفع مشکل در
    // کل پروژه فقط کافی است هرجا MessageBox.Show( صدا زده می‌شد، به Msg.Show(
    // تغییر کند — بدون هیچ تغییری در آرگومان‌ها یا رفتار دکمه‌ها.
    public static class Msg
    {
        private const MessageBoxOptions Rtl = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

        public static DialogResult Show(string text)
        {
            return MessageBox.Show(text, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, Rtl);
        }

        public static DialogResult Show(string text, string caption)
        {
            return MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, Rtl);
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons)
        {
            return MessageBox.Show(text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, Rtl);
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return MessageBox.Show(text, caption, buttons, icon, MessageBoxDefaultButton.Button1, Rtl);
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            return MessageBox.Show(text, caption, buttons, icon, defaultButton, Rtl);
        }

        // اگر جایی صراحتاً MessageBoxOptions هم داده باشد (مثلاً از قبل خودش
        // Rtl را داشته)، اینجا با OR ترکیب می‌شود تا هرگز پرچم گم یا تکراری‌کاری
        // اشتباه پیش نیاید.
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
        {
            return MessageBox.Show(text, caption, buttons, icon, defaultButton, options | Rtl);
        }
    }
}

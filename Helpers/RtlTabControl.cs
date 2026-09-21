using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // RtlTabControl — رفعِ باگِ شناخته‌شده‌ی WinForms: TabControl.RightToLeftLayout
    // مقدار را می‌پذیرد ولی exstyle بومیِ WS_EX_LAYOUTRTL را واقعاً به هندلِ
    // پنجره اعمال نمی‌کند، پس نوارِ سربرگ‌ها از چپ شروع می‌شود، نه راست، و
    // ResponsiveLayout.IsMirrored هم اشتباه محاسبه می‌کند.
    //
    // آموزش — قبلاً به‌صورتِ مستقل و یکسان در FrmCase.Designer.cs و
    // FrmFamily.Designer.cs (هر دو private nested class) تعریف شده بود؛ این‌جا
    // یک‌بار و مشترک است. رفتار عیناً همان است — فقط محلِ تعریف عوض شده.
    // ─────────────────────────────────────────────────────────────────────────
    public class RtlTabControl : TabControl
    {
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_LAYOUTRTL = 0x00400000;
                CreateParams cp = base.CreateParams;
                if (RightToLeftLayout)
                    cp.ExStyle |= WS_EX_LAYOUTRTL;
                return cp;
            }
        }
    }
}

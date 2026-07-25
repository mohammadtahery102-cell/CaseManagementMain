using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // کارتِ سفیدِ گردگوشه با حاشیه‌ی نرم — قابِ استاندارد بخش‌های فرم.
    //
    // آموزش — جایگزینِ GroupBox بومیِ ویندوز: آن کنترل قابِ خاکستریِ مربعی و
    // عنوانِ فرورفته دارد که ظاهرِ نرم‌افزارهای دهه‌ی گذشته را می‌سازد و
    // گوشه‌ی گرد هم نمی‌پذیرد. اینجا قاب را خودمان رسم می‌کنیم.
    //
    // خودِ GroupBoxهای موجود حذف نمی‌شوند؛ به‌عنوان میزبانِ محتوا داخل همین
    // کارت باقی می‌مانند تا هیچ ارجاعی در کد نشکند (فقط عنوان/قابشان خاموش
    // می‌شود).
    // ─────────────────────────────────────────────────────────────────────────
    public class SectionCard : Panel
    {
        public SectionCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            int radius = ResponsiveLayout.Scale(12);
            using (GraphicsPath path = StatCard.RoundedRect(rect, radius))
            {
                using (Brush fill = new SolidBrush(UiTheme.CardBack))
                    g.FillPath(fill, path);
                using (Pen border = new Pen(UiTheme.Border, 1f))
                    g.DrawPath(border, path);
            }

            base.OnPaint(e);
        }
    }
}

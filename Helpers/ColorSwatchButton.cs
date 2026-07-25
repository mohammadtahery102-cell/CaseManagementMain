using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // دکمه‌ی گرد کوچکِ انتخاب رنگ (با علامت ✓ روی گزینه‌ی انتخاب‌شده) — طبق
    // ردیف رنگ‌های طرح تصویریِ درخواستی کاربر برای تنظیمات. جایگزینِ ظاهریِ
    // دکمه‌ی «انتخاب رنگ» + مربع رنگِ قبلی است؛ منطقِ ذخیره/اعمال رنگ در
    // FrmSettings دست‌نخورده می‌ماند، فقط شکلِ انتخاب عوض می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public class ColorSwatchButton : Control
    {
        public Color SwatchColor { get; private set; }
        private bool _selected;

        public bool Selected
        {
            get { return _selected; }
            set { if (_selected == value) return; _selected = value; Invalidate(); }
        }

        public ColorSwatchButton(Color color)
        {
            SwatchColor = color;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(30, 30);
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle circle = new Rectangle(1, 1, Width - 3, Height - 3);

            if (_selected)
            {
                // حلقه‌ی نازکِ دورِ گزینه‌ی انتخاب‌شده (مثل طرح تصویری).
                using (Pen ring = new Pen(UiTheme.Primary, 2f))
                    g.DrawEllipse(ring, 0, 0, Width - 1, Height - 1);
            }

            using (Brush fill = new SolidBrush(SwatchColor))
                g.FillEllipse(fill, circle);

            if (_selected)
            {
                using (Pen check = new Pen(ContrastColor(SwatchColor), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    int cx = circle.X, cy = circle.Y, cw = circle.Width, ch = circle.Height;
                    g.DrawLines(check, new[]
                    {
                        new Point(cx + (int)(cw * 0.22), cy + (int)(ch * 0.52)),
                        new Point(cx + (int)(cw * 0.42), cy + (int)(ch * 0.72)),
                        new Point(cx + (int)(cw * 0.80), cy + (int)(ch * 0.28))
                    });
                }
            }
        }

        // چک‌مارک سفید روی رنگ‌های تیره، تیره روی رنگ‌های خیلی روشن (خوانا بماند).
        private static Color ContrastColor(Color c)
        {
            double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            return luminance > 0.7 ? Color.Black : Color.White;
        }
    }
}

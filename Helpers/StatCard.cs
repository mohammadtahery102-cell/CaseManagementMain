using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // کارت آماری داشبورد — طبق طرح تصویریِ درخواستی کاربر: زمینه‌ی کم‌رنگِ
    // هم‌خانواده با رنگ کارت، نشانِ گرد رنگی، عنوان، عددِ درشت، واحد، و یک
    // Sparkline در پایین.
    //
    // آموزش — آیکون در یک Label جداگانه با فونتِ صریحِ آیکونی رسم می‌شود، نه
    // داخل متنِ فارسی. علتش را در PillTabStrip هم دیدیم: فونت فارسیِ برنامه
    // گلیفِ آیکون/ایموجی ندارد و کاراکترها به‌صورت «▯» رندر می‌شوند؛ یک کنترل
    // هم فقط یک فونت می‌پذیرد. با جداکردن آیکون در Label خودش، هرکدام فونت
    // مناسب خودش را می‌گیرد.
    // ─────────────────────────────────────────────────────────────────────────
    public class StatCard : Panel
    {
        private readonly Label _lblValue;
        private readonly Sparkline _spark;

        private const int Radius = 14;
        private readonly Color _accent;

        public StatCard(string title, string unit, string iconGlyph, Color accent, Color tint)
        {
            _accent = accent;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Background;
            Padding = new Padding(14, 12, 14, 10);

            // ── ردیف بالا: نشان گرد (چپ) + عنوان (راست) ──
            Panel top = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent };

            IconBadge badge = new IconBadge(iconGlyph, accent) { Dock = DockStyle.Left, Width = 40 };

            Label lblTitle = new Label
            {
                Text = title, Dock = DockStyle.Fill, BackColor = Color.Transparent,
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 2, 0)
            };

            top.Controls.Add(lblTitle);
            top.Controls.Add(badge);

            _lblValue = new Label
            {
                Text = "0", Dock = DockStyle.Top, Height = 38, BackColor = Color.Transparent,
                Font = UiTheme.FontBold(21F), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            };

            Label lblUnit = new Label
            {
                Text = unit, Dock = DockStyle.Top, Height = 18, BackColor = Color.Transparent,
                Font = UiTheme.Font(UiTheme.SizeSmall - 1F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };

            _spark = new Sparkline { Dock = DockStyle.Fill, LineColor = accent };

            Controls.Add(_spark);
            Controls.Add(lblUnit);
            Controls.Add(_lblValue);
            Controls.Add(top);

            _tint = tint;
        }

        private readonly Color _tint;

        public void SetValue(int value)
        {
            _lblValue.Text = value.ToString("N0");
        }

        public void SetTrend(double[] values)
        {
            _spark.SetValues(values);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(rect, Radius))
            {
                using (Brush fill = new SolidBrush(_tint))
                    e.Graphics.FillPath(fill, path);
                using (Pen border = new Pen(Color.FromArgb(60, _accent), 1f))
                    e.Graphics.DrawPath(border, path);
            }
            base.OnPaint(e);
        }

        internal static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // نشانِ گردِ رنگی با یک گلیفِ آیکونی در مرکز.
    public class IconBadge : Control
    {
        private readonly string _glyph;
        private readonly Color _accent;

        public IconBadge(string glyph, Color accent)
        {
            _glyph = glyph;
            _accent = accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                      ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int size = Math.Min(Width, Height) - 2;
            int x = (Width - size) / 2;
            int y = (Height - size) / 2;

            using (Brush fill = new SolidBrush(_accent))
                g.FillEllipse(fill, x, y, size, size);

            using (Font f = IconFont.Get(13F))
            using (Brush b = new SolidBrush(Color.White))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(_glyph, f, b, new RectangleF(x, y, size, size), sf);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // فونت آیکونیِ ویندوز. «Segoe MDL2 Assets» روی ویندوز ۱۰/۱۱ همیشه نصب است
    // و آیکون‌های تک‌رنگِ خطی می‌دهد (دقیقاً سبکِ طرح تصویری). اگر نبود، به
    // «Segoe UI Symbol» و بعد فونت عمومی برمی‌گردیم تا هرگز کرش/مربعِ خالی
    // نداشته باشیم.
    // ─────────────────────────────────────────────────────────────────────────
    public static class IconFont
    {
        private static string _family;
        private static readonly object _lock = new object();

        // کدهای گلیف در Segoe MDL2 Assets
        public const string Home      = "";
        public const string Folder    = "";
        public const string People    = "";
        public const string Contact   = "";
        public const string Heart     = "";
        public const string Money     = "";
        public const string Calculator= "";
        public const string Card      = "";
        public const string Chart     = "";
        public const string Shield    = "";
        public const string Settings  = "";
        public const string Search    = "";
        public const string Add       = "";
        public const string Save      = "";
        public const string Bell      = "";
        public const string Mail      = "";
        public const string Menu      = "";
        public const string Sync      = "";
        public const string Book      = "";
        public const string Phone     = "";
        public const string Exit      = "";
        public const string Check     = "";
        public const string Cancel    = "";
        public const string Clock     = "";
        public const string Document  = "";
        public const string Edit      = "";

        private static string Family
        {
            get
            {
                if (_family == null)
                {
                    lock (_lock)
                    {
                        if (_family == null)
                            _family = Resolve();
                    }
                }
                return _family;
            }
        }

        private static string Resolve()
        {
            string[] candidates = { "Segoe MDL2 Assets", "Segoe UI Symbol", "Segoe UI" };
            foreach (string name in candidates)
            {
                try
                {
                    using (FontFamily ff = new FontFamily(name))
                        return ff.Name;
                }
                catch (ArgumentException) { }
            }
            return FontFamily.GenericSansSerif.Name;
        }

        public static Font Get(float size)
        {
            return new Font(Family, size, FontStyle.Regular, GraphicsUnit.Point);
        }
    }
}

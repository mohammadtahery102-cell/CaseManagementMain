using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // کارت آماری داشبورد — نسخهٔ پایدار قبلی:
    // زمینهٔ رنگیِ ملایم، نشان گرد، عنوان، عدد درشت، واحد، Sparkline.
    // ─────────────────────────────────────────────────────────────────────────
    public class StatCard : Panel
    {
        private readonly Label _lblValue;
        private readonly Sparkline _spark;

        private const int Radius = 14;
        private readonly Color _accent;
        private readonly Color _tint;

        public StatCard(string title, string unit, string iconGlyph, Color accent, Color tint)
            : this(title, unit, iconGlyph, accent, tint, false)
        {
        }

        public StatCard(string title, string unit, string iconGlyph, Color accent, Color tint, bool compact)
        {
            _accent = accent;
            _tint = tint;
            _ = compact;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Background;
            Padding = new Padding(14, 12, 14, 10);

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

            bool showUnit = !string.IsNullOrEmpty(unit);
            Label lblUnit = new Label
            {
                Text = unit, Dock = DockStyle.Top, Height = showUnit ? 18 : 0, BackColor = Color.Transparent,
                Font = UiTheme.Font(UiTheme.SizeSmall - 1F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight, Visible = showUnit
            };

            _spark = new Sparkline { Dock = DockStyle.Fill, LineColor = accent };

            Controls.Add(_spark);
            Controls.Add(lblUnit);
            Controls.Add(_lblValue);
            Controls.Add(top);
        }

        public void SetValue(int value)
        {
            _lblValue.Text = value.ToString("N0");
        }

        public void SetTrend(double[] values)
        {
            if (_spark == null) return;
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
        private string _glyph;
        private Color _accent;

        // به‌روزرسانیِ درجا — تا فهرست‌هایی مثل «آخرین فعالیت‌ها» بتوانند به‌جای
        // ساختِ دوباره‌ی کنترل‌ها فقط محتوایشان را عوض کنند (بسیار ارزان‌تر).
        public void SetIcon(string glyph, Color accent)
        {
            if (_glyph == glyph && _accent == accent) return;
            _glyph = glyph;
            _accent = accent;
            Invalidate();
        }

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
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int size = Math.Min(Width, Height) - 2;
            int x = (Width - size) / 2;
            int y = (Height - size) / 2;
            int radius = Math.Max(5, size * 28 / 100);

            using (Brush fill = new SolidBrush(_accent))
            using (GraphicsPath badge = StatCard.RoundedRect(new Rectangle(x, y, size, size), radius))
                g.FillPath(fill, badge);

            float pad = size * 0.24f;
            RectangleF iconBox = new RectangleF(x + pad, y + pad, size - pad * 2, size - pad * 2);
            using (Pen pen = new Pen(Color.White, Math.Max(1.2f, size * 0.075f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                if (!ModernGlyph.Draw(g, pen, iconBox, _glyph))
                {
                    using (Font f = IconFont.Get(Math.Max(9F, size * 0.45f)))
                    using (Brush b = new SolidBrush(Color.White))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(_glyph, f, b, new RectangleF(x, y, size, size), sf);
                }
            }
        }
    }

    // آیکون خطیِ مدرن (سبک Lucide / Heroicons) برای نشانِ کارت‌های آماری.
    // مختصات روی صفحهٔ ۲۴×۲۴ است و به جعبهٔ واقعی مقیاس می‌شود.
    internal static class ModernGlyph
    {
        public static bool Draw(Graphics g, Pen pen, RectangleF box, string glyph)
        {
            float s = Math.Min(box.Width, box.Height);
            if (s < 4) return false;
            float ox = box.X + (box.Width - s) / 2f;
            float oy = box.Y + (box.Height - s) / 2f;

            if (glyph == IconFont.Check) { DrawCheck(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Clock) { DrawClock(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Cancel) { DrawCancel(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Folder) { DrawFolder(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.People) { DrawPeople(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Document) { DrawDocument(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Card) { DrawCard(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Money) { DrawMoney(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Shield) { DrawShield(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Settings) { DrawSettings(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Chart) { DrawChart(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Add) { DrawAdd(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Edit) { DrawEdit(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Home) { DrawHome(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Search) { DrawSearch(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Sync) { DrawSync(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Bell) { DrawBell(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Contact) { DrawContact(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Calculator) { DrawCalculator(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Book) { DrawBook(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Phone) { DrawPhone(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Exit) { DrawExit(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Save) { DrawSave(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Mail) { DrawMail(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Heart) { DrawHeart(g, pen, ox, oy, s); return true; }
            if (glyph == IconFont.Menu) { DrawMenu(g, pen, ox, oy, s); return true; }
            return false;
        }

        private static PointF P(float ox, float oy, float s, float x, float y)
        {
            return new PointF(ox + x / 24f * s, oy + y / 24f * s);
        }

        private static void DrawCheck(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawLines(pen, new[] { P(ox, oy, s, 5, 12.5f), P(ox, oy, s, 10, 17.5f), P(ox, oy, s, 19, 6.5f) });
        }

        private static void DrawClock(Graphics g, Pen pen, float ox, float oy, float s)
        {
            float r = 9f / 24f * s;
            g.DrawEllipse(pen, ox + s / 2f - r, oy + s / 2f - r, r * 2, r * 2);
            g.DrawLine(pen, P(ox, oy, s, 12, 8), P(ox, oy, s, 12, 12.5f));
            g.DrawLine(pen, P(ox, oy, s, 12, 12.5f), P(ox, oy, s, 16, 15));
        }

        private static void DrawCancel(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawLine(pen, P(ox, oy, s, 7, 7), P(ox, oy, s, 17, 17));
            g.DrawLine(pen, P(ox, oy, s, 17, 7), P(ox, oy, s, 7, 17));
        }

        private static void DrawFolder(Graphics g, Pen pen, float ox, float oy, float s)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLines(new[]
                {
                    P(ox, oy, s, 3.5f, 8), P(ox, oy, s, 3.5f, 19), P(ox, oy, s, 20.5f, 19),
                    P(ox, oy, s, 20.5f, 10), P(ox, oy, s, 12, 10), P(ox, oy, s, 10, 7),
                    P(ox, oy, s, 3.5f, 7), P(ox, oy, s, 3.5f, 8)
                });
                g.DrawPath(pen, path);
            }
        }

        private static void DrawPeople(Graphics g, Pen pen, float ox, float oy, float s)
        {
            float r1 = 2.4f / 24f * s;
            g.DrawEllipse(pen, P(ox, oy, s, 8.5f, 6.2f).X - r1, P(ox, oy, s, 8.5f, 6.2f).Y, r1 * 2, r1 * 2);
            g.DrawArc(pen, ox + 3.2f / 24f * s, oy + 12.2f / 24f * s, 10.6f / 24f * s, 9f / 24f * s, 200, 140);
            float r2 = 2f / 24f * s;
            g.DrawEllipse(pen, P(ox, oy, s, 16.2f, 7.4f).X - r2, P(ox, oy, s, 16.2f, 7.4f).Y, r2 * 2, r2 * 2);
            g.DrawArc(pen, ox + 11.5f / 24f * s, oy + 13.2f / 24f * s, 9.2f / 24f * s, 8f / 24f * s, 220, 95);
        }

        private static void DrawDocument(Graphics g, Pen pen, float ox, float oy, float s)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLines(new[]
                {
                    P(ox, oy, s, 7, 3.5f), P(ox, oy, s, 14.5f, 3.5f), P(ox, oy, s, 20.5f, 9.5f),
                    P(ox, oy, s, 20.5f, 20.5f), P(ox, oy, s, 7, 20.5f), P(ox, oy, s, 7, 3.5f)
                });
                g.DrawPath(pen, path);
            }
            g.DrawLine(pen, P(ox, oy, s, 14.5f, 3.5f), P(ox, oy, s, 14.5f, 9.5f));
            g.DrawLine(pen, P(ox, oy, s, 14.5f, 9.5f), P(ox, oy, s, 20.5f, 9.5f));
            g.DrawLine(pen, P(ox, oy, s, 10, 13.5f), P(ox, oy, s, 17.5f, 13.5f));
            g.DrawLine(pen, P(ox, oy, s, 10, 17), P(ox, oy, s, 15.5f, 17));
        }

        private static void DrawCard(Graphics g, Pen pen, float ox, float oy, float s)
        {
            RectangleF rect = new RectangleF(ox + 3f / 24f * s, oy + 6f / 24f * s, 18f / 24f * s, 12.5f / 24f * s);
            float rr = 2.2f / 24f * s;
            using (GraphicsPath path = RoundBox(rect, rr))
                g.DrawPath(pen, path);
            g.DrawLine(pen, P(ox, oy, s, 3, 11), P(ox, oy, s, 21, 11));
            g.DrawLine(pen, P(ox, oy, s, 7, 15.2f), P(ox, oy, s, 12.5f, 15.2f));
        }

        private static void DrawMoney(Graphics g, Pen pen, float ox, float oy, float s)
        {
            RectangleF rect = new RectangleF(ox + 3f / 24f * s, oy + 6.5f / 24f * s, 18f / 24f * s, 11.5f / 24f * s);
            using (GraphicsPath path = RoundBox(rect, 2f / 24f * s))
                g.DrawPath(pen, path);
            float r = 2.6f / 24f * s;
            g.DrawEllipse(pen, ox + s / 2f - r, oy + s / 2f - r, r * 2, r * 2);
        }

        private static void DrawShield(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawLines(pen, new[]
            {
                P(ox, oy, s, 12, 3.5f), P(ox, oy, s, 19.5f, 6.5f), P(ox, oy, s, 19.5f, 13),
                P(ox, oy, s, 12, 21), P(ox, oy, s, 4.5f, 13), P(ox, oy, s, 4.5f, 6.5f), P(ox, oy, s, 12, 3.5f)
            });
        }

        private static void DrawSettings(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawLine(pen, P(ox, oy, s, 4, 8), P(ox, oy, s, 20, 8));
            g.DrawLine(pen, P(ox, oy, s, 4, 16), P(ox, oy, s, 20, 16));
            float r = 2.1f / 24f * s;
            using (Brush b = new SolidBrush(pen.Color))
            {
                g.FillEllipse(b, P(ox, oy, s, 9, 8).X - r, P(ox, oy, s, 9, 8).Y - r, r * 2, r * 2);
                g.FillEllipse(b, P(ox, oy, s, 15.5f, 16).X - r, P(ox, oy, s, 15.5f, 16).Y - r, r * 2, r * 2);
            }
        }

        private static void DrawChart(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawLine(pen, P(ox, oy, s, 5, 20), P(ox, oy, s, 5, 11));
            g.DrawLine(pen, P(ox, oy, s, 11, 20), P(ox, oy, s, 11, 6));
            g.DrawLine(pen, P(ox, oy, s, 17, 20), P(ox, oy, s, 17, 14));
            g.DrawLine(pen, P(ox, oy, s, 4, 20), P(ox, oy, s, 20, 20));
        }

        private static void DrawAdd(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawLine(pen, P(ox, oy, s, 12, 5.5f), P(ox, oy, s, 12, 18.5f));
            g.DrawLine(pen, P(ox, oy, s, 5.5f, 12), P(ox, oy, s, 18.5f, 12));
        }

        private static void DrawEdit(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawLine(pen, P(ox, oy, s, 4.5f, 19.5f), P(ox, oy, s, 9, 18.2f));
            g.DrawLine(pen, P(ox, oy, s, 9, 18.2f), P(ox, oy, s, 19.2f, 8));
            g.DrawLine(pen, P(ox, oy, s, 19.2f, 8), P(ox, oy, s, 16, 4.8f));
            g.DrawLine(pen, P(ox, oy, s, 16, 4.8f), P(ox, oy, s, 5.8f, 15));
            g.DrawLine(pen, P(ox, oy, s, 5.8f, 15), P(ox, oy, s, 4.5f, 19.5f));
        }

        private static void DrawHome(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawLines(pen, new[] { P(ox, oy, s, 4, 11), P(ox, oy, s, 12, 4.5f), P(ox, oy, s, 20, 11) });
            g.DrawLines(pen, new[]
            {
                P(ox, oy, s, 6.5f, 10.5f), P(ox, oy, s, 6.5f, 20), P(ox, oy, s, 17.5f, 20), P(ox, oy, s, 17.5f, 10.5f)
            });
        }

        private static void DrawSearch(Graphics g, Pen pen, float ox, float oy, float s)
        {
            float r = 6.2f / 24f * s;
            g.DrawEllipse(pen, ox + 5.2f / 24f * s, oy + 5.2f / 24f * s, r * 2, r * 2);
            g.DrawLine(pen, P(ox, oy, s, 14.8f, 14.8f), P(ox, oy, s, 20, 20));
        }

        private static void DrawSync(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawArc(pen, ox + 4.5f / 24f * s, oy + 4.5f / 24f * s, 15f / 24f * s, 15f / 24f * s, 40, 200);
            g.DrawLines(pen, new[] { P(ox, oy, s, 18.5f, 6.5f), P(ox, oy, s, 19.5f, 11), P(ox, oy, s, 15, 10.2f) });
        }

        private static void DrawBell(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawArc(pen, ox + 6.5f / 24f * s, oy + 4.5f / 24f * s, 11f / 24f * s, 11f / 24f * s, 200, 140);
            g.DrawLines(pen, new[] { P(ox, oy, s, 6.5f, 12), P(ox, oy, s, 5.5f, 18.5f), P(ox, oy, s, 18.5f, 18.5f), P(ox, oy, s, 17.5f, 12) });
            g.DrawArc(pen, ox + 9.5f / 24f * s, oy + 18f / 24f * s, 5f / 24f * s, 3.5f / 24f * s, 10, 160);
        }

        private static void DrawContact(Graphics g, Pen pen, float ox, float oy, float s)
        {
            float r = 3f / 24f * s;
            g.DrawEllipse(pen, ox + s / 2f - r, oy + 5.2f / 24f * s, r * 2, r * 2);
            g.DrawArc(pen, ox + 5.5f / 24f * s, oy + 13.5f / 24f * s, 13f / 24f * s, 9f / 24f * s, 200, 140);
        }

        private static void DrawCalculator(Graphics g, Pen pen, float ox, float oy, float s)
        {
            using (GraphicsPath path = RoundBox(new RectangleF(ox + 5f / 24f * s, oy + 3.5f / 24f * s, 14f / 24f * s, 17.5f / 24f * s), 2f / 24f * s))
                g.DrawPath(pen, path);
            g.DrawLine(pen, P(ox, oy, s, 8, 8), P(ox, oy, s, 16, 8));
            g.DrawLine(pen, P(ox, oy, s, 8.5f, 13), P(ox, oy, s, 8.5f, 13.2f));
            g.DrawLine(pen, P(ox, oy, s, 12, 13), P(ox, oy, s, 12, 13.2f));
            g.DrawLine(pen, P(ox, oy, s, 15.5f, 13), P(ox, oy, s, 15.5f, 13.2f));
        }

        private static void DrawBook(Graphics g, Pen pen, float ox, float oy, float s)
        {
            using (GraphicsPath path = RoundBox(new RectangleF(ox + 5f / 24f * s, oy + 4f / 24f * s, 14.5f / 24f * s, 16.5f / 24f * s), 1.6f / 24f * s))
                g.DrawPath(pen, path);
            g.DrawLine(pen, P(ox, oy, s, 12, 4), P(ox, oy, s, 12, 20.5f));
        }

        private static void DrawPhone(Graphics g, Pen pen, float ox, float oy, float s)
        {
            using (GraphicsPath path = RoundBox(new RectangleF(ox + 7.5f / 24f * s, oy + 3.5f / 24f * s, 9f / 24f * s, 17.5f / 24f * s), 2f / 24f * s))
                g.DrawPath(pen, path);
            g.DrawLine(pen, P(ox, oy, s, 10.5f, 18.8f), P(ox, oy, s, 13.5f, 18.8f));
        }

        private static void DrawExit(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawLines(pen, new[] { P(ox, oy, s, 10, 5), P(ox, oy, s, 5, 5), P(ox, oy, s, 5, 19), P(ox, oy, s, 10, 19) });
            g.DrawLine(pen, P(ox, oy, s, 10, 12), P(ox, oy, s, 19.5f, 12));
            g.DrawLines(pen, new[] { P(ox, oy, s, 16, 8.5f), P(ox, oy, s, 19.5f, 12), P(ox, oy, s, 16, 15.5f) });
        }

        private static void DrawSave(Graphics g, Pen pen, float ox, float oy, float s)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLines(new[]
                {
                    P(ox, oy, s, 5, 5), P(ox, oy, s, 16.5f, 5), P(ox, oy, s, 19.5f, 8),
                    P(ox, oy, s, 19.5f, 19.5f), P(ox, oy, s, 5, 19.5f), P(ox, oy, s, 5, 5)
                });
                g.DrawPath(pen, path);
            }
            g.DrawLine(pen, P(ox, oy, s, 8.5f, 5), P(ox, oy, s, 8.5f, 10));
            g.DrawLine(pen, P(ox, oy, s, 15.5f, 5), P(ox, oy, s, 15.5f, 10));
            g.DrawLine(pen, P(ox, oy, s, 8.5f, 10), P(ox, oy, s, 15.5f, 10));
        }

        private static void DrawMail(Graphics g, Pen pen, float ox, float oy, float s)
        {
            using (GraphicsPath path = RoundBox(new RectangleF(ox + 3.5f / 24f * s, oy + 6.5f / 24f * s, 17f / 24f * s, 12f / 24f * s), 1.8f / 24f * s))
                g.DrawPath(pen, path);
            g.DrawLines(pen, new[] { P(ox, oy, s, 4.2f, 8.2f), P(ox, oy, s, 12, 14), P(ox, oy, s, 19.8f, 8.2f) });
        }

        private static void DrawHeart(Graphics g, Pen pen, float ox, float oy, float s)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddBeziers(new[]
                {
                    P(ox, oy, s, 12, 20), P(ox, oy, s, 5, 14), P(ox, oy, s, 3.5f, 9), P(ox, oy, s, 7.5f, 6),
                    P(ox, oy, s, 10.5f, 6.5f), P(ox, oy, s, 12, 9), P(ox, oy, s, 12, 9),
                    P(ox, oy, s, 13.5f, 6.5f), P(ox, oy, s, 16.5f, 6), P(ox, oy, s, 20.5f, 9),
                    P(ox, oy, s, 19, 14), P(ox, oy, s, 12, 20), P(ox, oy, s, 12, 20)
                });
                g.DrawPath(pen, path);
            }
        }

        private static void DrawMenu(Graphics g, Pen pen, float ox, float oy, float s)
        {
            g.DrawLine(pen, P(ox, oy, s, 5, 8), P(ox, oy, s, 19, 8));
            g.DrawLine(pen, P(ox, oy, s, 5, 12), P(ox, oy, s, 19, 12));
            g.DrawLine(pen, P(ox, oy, s, 5, 16), P(ox, oy, s, 19, 16));
        }

        private static GraphicsPath RoundBox(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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

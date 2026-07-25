using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // پنلِ معرفیِ سیستم در صفحه‌ی ورود (سمت چپ).
    //
    // آموزش — چرا همه‌چیز با کد رسم می‌شود و نه با فایل تصویر: یک تصویرِ آماده
    // در مقیاس‌های ۱۲۵٪/۱۵۰٪/۲۰۰٪ کشیده و تار می‌شود، در حالی که رسمِ برداری
    // در هر مقیاسی تیز می‌ماند. همه‌ی اندازه‌ها هم از ResponsiveLayout.Scale
    // می‌آیند تا با DPI بزرگ شوند.
    //
    // صداقت فنی: کاربر «تصویر سه‌بعدی» خواسته بود. GDI+ موتور سه‌بعدی ندارد؛
    // آنچه اینجا ساخته شده یک تصویرسازیِ برداریِ لایه‌دار با عمقِ بصری است
    // (سایه، درخشش، پرسپکتیوِ ساده) — نه رندرِ سه‌بعدیِ واقعی.
    // ═════════════════════════════════════════════════════════════════════════
    public class LoginHeroPanel : Panel
    {
        // گرادیان سرمه‌ای → فیروزه‌ای (خواسته‌ی کاربر)
        private static readonly Color GradTop    = ColorTranslator.FromHtml("#0B1B34");
        private static readonly Color GradMid    = ColorTranslator.FromHtml("#0E2A4A");
        private static readonly Color GradBottom = ColorTranslator.FromHtml("#0D4F5C");
        private static readonly Color Teal       = ColorTranslator.FromHtml("#22D3EE");
        private static readonly Color TextDim    = ColorTranslator.FromHtml("#93A7C4");

        private readonly Image _logo;

        private sealed class Feature
        {
            public readonly string Glyph, Title;
            public Feature(string glyph, string title) { Glyph = glyph; Title = title; }
        }

        // پنج قابلیت اصلی (خواسته‌ی کاربر)
        private static readonly Feature[] Features =
        {
            new Feature(IconFont.Folder,   "مدیریت پرونده‌ها"),
            new Feature(IconFont.People,   "اعضای خانواده"),
            new Feature(IconFont.Document, "اسناد و مدارک"),
            new Feature(IconFont.Search,   "جستجوی پیشرفته"),
            new Feature(IconFont.Chart,    "گزارش‌گیری"),
        };

        public LoginHeroPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Fill;
            try { _logo = LogoHelper.GetLogoImage(); } catch { _logo = null; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int w = Width, h = Height;
            if (w <= 0 || h <= 0) return;

            PaintBackground(g, w, h);
            PaintDecor(g, w, h);

            // ── چیدمان عمودی: همه‌ی فاصله‌ها با DPI مقیاس می‌شوند ──
            int pad = ResponsiveLayout.Scale(48);
            int y = ResponsiveLayout.Scale(56);

            y = PaintEmblem(g, w, y);
            y += ResponsiveLayout.Scale(26);
            y = PaintTitles(g, w, pad, y);
            y += ResponsiveLayout.Scale(30);
            y = PaintFeatures(g, w, pad, y, h);

            PaintSecurityBox(g, w, pad, h);
        }

        // ─── پس‌زمینه: گرادیان سه‌مرحله‌ای سرمه‌ای → فیروزه‌ای ────────────────
        private static void PaintBackground(Graphics g, int w, int h)
        {
            using (var brush = new LinearGradientBrush(new Rectangle(0, 0, w, h), GradTop, GradBottom, 78f))
            {
                var blend = new ColorBlend(3);
                blend.Colors    = new[] { GradTop, GradMid, GradBottom };
                blend.Positions = new[] { 0f, 0.55f, 1f };
                brush.InterpolationColors = blend;
                g.FillRectangle(brush, 0, 0, w, h);
            }
        }

        // ─── المان‌های گرافیکیِ بسیار ظریف (خواسته‌ی کاربر: «ظریف، بدون شلوغی») ──
        private static void PaintDecor(Graphics g, int w, int h)
        {
            // درخششِ ملایمِ فیروزه‌ای در گوشه‌ی پایین
            using (var path = new GraphicsPath())
            {
                int r = Math.Max(w, h);
                path.AddEllipse(-r / 3, h - r / 2, r, r);
                using (var glow = new PathGradientBrush(path))
                {
                    glow.CenterColor = Color.FromArgb(46, Teal);
                    glow.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(glow, path);
                }
            }

            // خطوطِ موجِ بسیار کم‌رنگ
            using (var pen = new Pen(Color.FromArgb(16, 255, 255, 255), ResponsiveLayout.Scale(1)))
            {
                for (int i = 0; i < 4; i++)
                {
                    int baseY = h - ResponsiveLayout.Scale(150) + i * ResponsiveLayout.Scale(34);
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(
                            -20, baseY,
                            w * 0.30f, baseY - ResponsiveLayout.Scale(46),
                            w * 0.68f, baseY + ResponsiveLayout.Scale(38),
                            w + 20, baseY - ResponsiveLayout.Scale(14));
                        g.DrawPath(pen, path);
                    }
                }
            }

            // نقطه‌چینِ محوِ بالا-چپ (بافتِ ظریف)
            using (var dot = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
            {
                int step = ResponsiveLayout.Scale(22);
                int size = Math.Max(1, ResponsiveLayout.Scale(2));
                for (int x = step; x < w * 0.42f; x += step)
                    for (int yy = step; yy < h * 0.26f; yy += step)
                        g.FillEllipse(dot, x, yy, size, size);
            }
        }

        // ─── نشانِ مرکزی: لوگوی گنجینه با حلقه‌ی درخشان ──────────────────────
        private int PaintEmblem(Graphics g, int w, int y)
        {
            int size = ResponsiveLayout.Scale(104);
            int cx = w / 2;
            var rect = new Rectangle(cx - size / 2, y, size, size);

            // هاله‌ی بیرونی
            using (var path = new GraphicsPath())
            {
                int halo = (int)(size * 1.9f);
                path.AddEllipse(cx - halo / 2, y + size / 2 - halo / 2, halo, halo);
                using (var glow = new PathGradientBrush(path))
                {
                    glow.CenterColor = Color.FromArgb(64, Teal);
                    glow.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(glow, path);
                }
            }

            // حلقه‌ی نازک فیروزه‌ای
            using (var pen = new Pen(Color.FromArgb(120, Teal), Math.Max(1, ResponsiveLayout.Scale(2))))
                g.DrawEllipse(pen, Rectangle.Inflate(rect, ResponsiveLayout.Scale(10), ResponsiveLayout.Scale(10)));

            // دیسکِ لوگو
            using (var b = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                g.FillEllipse(b, rect);

            if (_logo != null)
            {
                var inner = Rectangle.Inflate(rect, -ResponsiveLayout.Scale(10), -ResponsiveLayout.Scale(10));
                using (var clip = new GraphicsPath())
                {
                    clip.AddEllipse(inner);
                    Region old = g.Clip;
                    g.Clip = new Region(clip);
                    g.DrawImage(_logo, inner);
                    g.Clip = old;
                }
            }

            return rect.Bottom;
        }

        // ─── عنوان بزرگ + شعار ───────────────────────────────────────────────
        private static int PaintTitles(Graphics g, int w, int pad, int y)
        {
            var area = new RectangleF(pad, y, w - pad * 2, ResponsiveLayout.Scale(44));
            using (var f = UiTheme.FontBold(ScaleFont(19f)))
            using (var b = new SolidBrush(Color.White))
            using (var sf = Center())
                g.DrawString("سیستم مدیریت پرونده گنجینه", f, b, area, sf);

            y = (int)area.Bottom + ResponsiveLayout.Scale(6);

            var subArea = new RectangleF(pad, y, w - pad * 2, ResponsiveLayout.Scale(26));
            using (var f = UiTheme.Font(ScaleFont(10.5f)))
            using (var b = new SolidBrush(TextDim))
            using (var sf = Center())
                g.DrawString("راهکار یکپارچه‌ی ثبت، پیگیری و گزارش‌گیریِ پرونده‌های ایتام", f, b, subArea, sf);

            return (int)subArea.Bottom;
        }

        // ─── پنج قابلیت اصلی ─────────────────────────────────────────────────
        private static int PaintFeatures(Graphics g, int w, int pad, int y, int h)
        {
            int rowH = ResponsiveLayout.Scale(46);
            int iconBox = ResponsiveLayout.Scale(34);
            int gap = ResponsiveLayout.Scale(14);

            // اگر ارتفاع کم بود، ردیف‌ها فشرده می‌شوند تا هرگز روی جعبه‌ی امنیت
            // نیفتند (روی نمایشگرهای کوتاه یا مقیاس ۲۰۰٪).
            int available = h - y - ResponsiveLayout.Scale(150);
            if (available < rowH * Features.Length)
                rowH = Math.Max(ResponsiveLayout.Scale(30), available / Features.Length);

            foreach (Feature f in Features)
            {
                int iconX = w - pad - iconBox;          // آیکون سمت راست (RTL)
                var iconRect = new Rectangle(iconX, y + (rowH - iconBox) / 2, iconBox, iconBox);

                using (var path = StatCard.RoundedRect(iconRect, ResponsiveLayout.Scale(9)))
                using (var b = new SolidBrush(Color.FromArgb(30, Teal)))
                    g.FillPath(b, path);

                using (var fnt = IconFont.Get(ScaleFont(11f)))
                using (var b = new SolidBrush(Teal))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(f.Glyph, fnt, b, iconRect, sf);

                var textRect = new RectangleF(pad, y, iconX - pad - gap, rowH);
                using (var fnt = UiTheme.Font(ScaleFont(10f)))
                using (var b = new SolidBrush(Color.FromArgb(226, 232, 240)))
                using (var sf = new StringFormat
                {
                    Alignment = StringAlignment.Far,          // راست‌چین
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap,
                    Trimming = StringTrimming.EllipsisCharacter
                })
                    g.DrawString(f.Title, fnt, b, textRect, sf);

                y += rowH;
            }

            return y;
        }

        // ─── جعبه‌ی امنیت اطلاعات (پایین پنل) ────────────────────────────────
        private static void PaintSecurityBox(Graphics g, int w, int pad, int h)
        {
            int boxH = ResponsiveLayout.Scale(74);
            int bottomPad = ResponsiveLayout.Scale(34);
            var rect = new Rectangle(pad, h - bottomPad - boxH, w - pad * 2, boxH);
            if (rect.Top < ResponsiveLayout.Scale(10)) return;   // جا نیست؛ رسم نکن

            using (var path = StatCard.RoundedRect(rect, ResponsiveLayout.Scale(12)))
            {
                using (var b = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
                    g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(46, Teal), 1f))
                    g.DrawPath(p, path);
            }

            int iconBox = ResponsiveLayout.Scale(34);
            var iconRect = new Rectangle(
                rect.Right - ResponsiveLayout.Scale(14) - iconBox,
                rect.Y + (boxH - iconBox) / 2, iconBox, iconBox);

            using (var b = new SolidBrush(Color.FromArgb(40, Teal)))
            using (var path = StatCard.RoundedRect(iconRect, ResponsiveLayout.Scale(9)))
                g.FillPath(b, path);

            using (var fnt = IconFont.Get(ScaleFont(11f)))
            using (var b = new SolidBrush(Teal))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(IconFont.Shield, fnt, b, iconRect, sf);

            int textRight = iconRect.Left - ResponsiveLayout.Scale(12);
            var titleRect = new RectangleF(rect.X + ResponsiveLayout.Scale(12), rect.Y + ResponsiveLayout.Scale(14),
                                           textRight - rect.X - ResponsiveLayout.Scale(12), ResponsiveLayout.Scale(20));
            using (var fnt = UiTheme.FontBold(ScaleFont(10f)))
            using (var b = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                g.DrawString("امنیت اطلاعات", fnt, b, titleRect, sf);

            var descRect = new RectangleF(titleRect.X, titleRect.Bottom, titleRect.Width, ResponsiveLayout.Scale(30));
            using (var fnt = UiTheme.Font(ScaleFont(8.5f)))
            using (var b = new SolidBrush(TextDim))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near })
                g.DrawString("داده‌ها به‌صورت محلی و رمزنگاری‌شده نگهداری می‌شوند.", fnt, b, descRect, sf);
        }

        // آموزش — اندازه‌ی فونت را دستی مقیاس نمی‌کنیم وقتی فرم AutoScaleMode.Dpi
        // دارد، چون خودِ WinForms فونت کنترل‌ها را بزرگ می‌کند. اما این پنل با
        // GDI+ رسم می‌شود و بیرون از آن سازوکار است، پس اینجا لازم است.
        private static float ScaleFont(float pt)
        {
            return pt * ResponsiveLayout.DpiScale;
        }

        private static StringFormat Center()
        {
            return new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };
        }
    }
}

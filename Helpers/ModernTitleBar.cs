using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // نوار عنوانِ سفارشیِ مدرن — طبق طرح تصویریِ صفحه‌ی ورود: نشانِ گردِ برنامه
    // در گوشه، عنوان، و سه دکمه‌ی پنجره (کوچک‌کردن/بیشینه/بستن) با حالت
    // Hover. کاملاً دستی رسم می‌شود تا در ویندوز ۱۰ و ۱۱ یکسان دیده شود.
    //
    // آموزش — «بستن» به‌جای Form.Close مقدار DialogResult را هم درست تنظیم
    // می‌کند، چون این نوار روی فرم‌های دیالوگی (مثل ورود) هم استفاده می‌شود و
    // آن‌ها با ShowDialog باز می‌شوند؛ بستنِ خام باعث می‌شد فراخوان نتواند
    // «انصراف» را از «موفق» تشخیص دهد.
    // ─────────────────────────────────────────────────────────────────────────
    public class ModernTitleBar : Panel
    {
        private readonly Form _owner;
        private readonly WindowButton _btnClose;
        private readonly WindowButton _btnMax;
        private readonly WindowButton _btnMin;

        public ModernTitleBar(Form owner, string title, Color backColor, bool showMaximize = true)
        {
            _owner = owner;

            Dock = DockStyle.Top;
            Height = 46;
            BackColor = backColor;
            RightToLeft = RightToLeft.No; // دکمه‌های پنجره همیشه در سمت راست (استاندارد ویندوز)

            // ── دکمه‌های پنجره (راست) ──
            _btnClose = new WindowButton(WindowButtonKind.Close, backColor) { Dock = DockStyle.Right, Width = 46 };
            _btnClose.Click += delegate
            {
                _owner.DialogResult = DialogResult.Cancel;
                _owner.Close();
            };

            _btnMax = new WindowButton(WindowButtonKind.Maximize, backColor) { Dock = DockStyle.Right, Width = 46, Visible = showMaximize };
            _btnMax.Click += delegate
            {
                _owner.WindowState = _owner.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };

            _btnMin = new WindowButton(WindowButtonKind.Minimize, backColor) { Dock = DockStyle.Right, Width = 46 };
            _btnMin.Click += delegate { _owner.WindowState = FormWindowState.Minimized; };

            // ── نشان برنامه (چپ) ──
            AppBadge badge = new AppBadge { Dock = DockStyle.Left, Width = 56 };

            // ── عنوان: کنارِ دکمه‌های پنجره، راست‌چین ──
            Label lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 12, 0)
            };

            // آموزش — ترتیب افزودن دکمه‌های پنجره مهم است: در چیدمانِ Dock=Right،
            // کنترلی که «دیرتر» اضافه شود بیرونی‌تر (راست‌تر) می‌نشیند. برای
            // رسیدن به ترتیب استاندارد ویندوز (کوچک‌کردن، بیشینه، بستن — با
            // بستن در راست‌ترین نقطه) باید دقیقاً به همین ترتیب اضافه شوند.
            // (در تست تصویری، ترتیبِ قبلی برعکس ظاهر شد و «بستن» چپ‌ترین بود.)
            Controls.Add(lblTitle);
            Controls.Add(_btnMin);
            Controls.Add(_btnMax);
            Controls.Add(_btnClose);
            Controls.Add(badge);

            // کشیدن پنجره از نوار عنوان و از خودِ متن عنوان/نشان.
            WindowChrome.EnableDragMove(this, owner);
            WindowChrome.EnableDragMove(lblTitle, owner);
            WindowChrome.EnableDragMove(badge, owner);
            if (showMaximize)
            {
                WindowChrome.EnableDoubleClickMaximize(this, owner);
                WindowChrome.EnableDoubleClickMaximize(lblTitle, owner);
            }
        }

        private enum WindowButtonKind { Minimize, Maximize, Close }

        // دکمه‌ی پنجره با آیکونِ رسم‌شده (نه فونت) تا در هر سیستمی یکسان باشد.
        private class WindowButton : Control
        {
            private readonly WindowButtonKind _kind;
            private readonly Color _baseBack;
            private bool _hover;

            public WindowButton(WindowButtonKind kind, Color baseBack)
            {
                _kind = kind;
                _baseBack = baseBack;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = baseBack;
                Cursor = Cursors.Hand;
                TabStop = false;
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // بستن، هنگام Hover قرمز می‌شود (قرارداد آشنای ویندوز).
                Color back = _hover
                    ? (_kind == WindowButtonKind.Close
                        ? ColorTranslator.FromHtml("#C42B1C")
                        : Color.FromArgb(38, 255, 255, 255))
                    : _baseBack;
                g.Clear(back);

                int cx = Width / 2, cy = Height / 2;
                using (Pen pen = new Pen(Color.White, 1.3f))
                {
                    switch (_kind)
                    {
                        case WindowButtonKind.Minimize:
                            g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
                            break;

                        case WindowButtonKind.Maximize:
                            g.DrawRectangle(pen, cx - 5, cy - 5, 10, 10);
                            break;

                        case WindowButtonKind.Close:
                            g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                            g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                            break;
                    }
                }
            }
        }

        // نشانِ گردِ برنامه در گوشه‌ی نوار عنوان (لوگوی مؤسسه اگر موجود باشد).
        private class AppBadge : Control
        {
            private readonly Image _logo;

            public AppBadge()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                try { _logo = LogoHelper.GetLogoImage(); } catch { _logo = null; }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Parent == null ? BackColor : Parent.BackColor);

                int size = 30;
                Rectangle rect = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);

                using (Brush b = new SolidBrush(UiTheme.Primary))
                    g.FillEllipse(b, rect);

                if (_logo != null)
                {
                    // لوگو داخل دایره بریده می‌شود تا لبه‌ها تمیز بماند.
                    using (GraphicsPath clip = new GraphicsPath())
                    {
                        clip.AddEllipse(rect);
                        Region old = g.Clip;
                        g.Clip = new Region(clip);
                        g.DrawImage(_logo, rect);
                        g.Clip = old;
                    }
                }
            }
        }
    }
}

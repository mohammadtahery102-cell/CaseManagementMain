using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // نوار ناوبری کناریِ تیره — طبق طرح‌های تصویریِ درخواستی کاربر (هم داشبورد
    // و هم فرم پرونده‌ها همین نوار را دارند، پس یک‌بار ساخته و در هر دو
    // استفاده می‌شود).
    //
    // آموزش — سمت راست، نه چپ: کل برنامه RTL است و در رابط‌های راست‌به‌چپ
    // نوار کناری استاندارد سمت راست می‌نشیند. (در یکی از دو عکسِ نمونه سمت
    // چپ کشیده شده بود، ولی همان عکس هم متن‌هایش راست‌چین است؛ سمت راست
    // انتخاب شد تا کل برنامه یکدست بماند.)
    //
    // آموزش — آیکون هر آیتم در یک Label جدا با فونتِ آیکونیِ ویندوز
    // (Segoe MDL2 Assets، نگاه کنید IconFont) رسم می‌شود، نه داخل متن فارسی؛
    // چون فونت فارسی گلیف آیکون ندارد و «▯» نشان می‌داد.
    // ─────────────────────────────────────────────────────────────────────────
    public class SidebarNav : Panel
    {
        public static readonly Color BackDark   = ColorTranslator.FromHtml("#16213E");
        public static readonly Color BackDarker = ColorTranslator.FromHtml("#101A31");
        public static readonly Color ItemText   = ColorTranslator.FromHtml("#B9C2D8");
        public static readonly Color GroupText  = ColorTranslator.FromHtml("#6C7A99");

        private readonly Panel _itemsHost;
        private readonly List<NavItem> _items = new List<NavItem>();
        private int _activeIndex = -1;

        public SidebarNav(string brandTitle, string brandSubtitle)
        {
            Dock = DockStyle.Right;
            Width = 232;
            BackColor = BackDark;
            RightToLeft = RightToLeft.Yes;

            // ── سربرگ برند ──
            Panel brand = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = BackDark, Padding = new Padding(14, 16, 14, 12) };

            BrandIcon icon = new BrandIcon { Dock = DockStyle.Right, Width = 52 };

            Panel titles = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 8, 0) };
            Label lblSub = new Label
            {
                Text = brandSubtitle, Dock = DockStyle.Top, Height = 18, BackColor = Color.Transparent,
                Font = UiTheme.Font(UiTheme.SizeSmall - 2F), ForeColor = GroupText,
                TextAlign = ContentAlignment.MiddleRight
            };
            Label lblTitle = new Label
            {
                Text = brandTitle, Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent,
                Font = UiTheme.FontBold(UiTheme.SizeLarge), ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleRight
            };
            titles.Controls.Add(lblTitle);
            titles.Controls.Add(lblSub);

            brand.Controls.Add(titles);
            brand.Controls.Add(icon);

            // ── میزبان آیتم‌ها (اسکرول‌پذیر برای صفحه‌های کوچک) ──
            _itemsHost = new Panel { Dock = DockStyle.Fill, BackColor = BackDark, AutoScroll = true, Padding = new Padding(10, 6, 10, 10) };

            Controls.Add(_itemsHost);
            Controls.Add(brand);
        }

        // عنوان گروه (مثل «اصلی»، «مالی و حسابداری» در طرح تصویری)
        public void AddGroup(string title)
        {
            Label lbl = new Label
            {
                Text = title, Dock = DockStyle.Top, Height = 26, BackColor = Color.Transparent,
                Font = UiTheme.FontBold(UiTheme.SizeSmall - 2F), ForeColor = GroupText,
                TextAlign = ContentAlignment.BottomRight, Padding = new Padding(0, 0, 8, 4)
            };
            AddToHost(lbl);
        }

        public int AddItem(string glyph, string text, EventHandler onClick)
        {
            int index = _items.Count;
            NavItem item = new NavItem(glyph, text) { Dock = DockStyle.Top };
            item.Click += delegate
            {
                SetActive(index);
                if (onClick != null) onClick(item, EventArgs.Empty);
            };
            _items.Add(item);
            AddToHost(item);
            return index;
        }

        // آموزش — Dock=Top آیتم‌ها را به ترتیبِ «معکوسِ» افزودن می‌چیند، چون هر
        // کنترلِ جدید بالای قبلی‌ها می‌نشیند. برای همین هر آیتم بعد از افزودن
        // به انتهای ترتیبِ z فرستاده می‌شود تا ترتیب بصری = ترتیب افزودن باشد.
        private void AddToHost(Control c)
        {
            _itemsHost.Controls.Add(c);
            c.BringToFront();
        }

        public void SetActive(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            _activeIndex = index;
            for (int i = 0; i < _items.Count; i++)
                _items[i].Active = (i == _activeIndex);
        }

        // ── یک آیتم ناوبری ──
        private class NavItem : Control
        {
            private readonly string _glyph;
            private bool _active;
            private bool _hover;

            public NavItem(string glyph, string text)
            {
                _glyph = glyph;
                Text = text;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Height = 42;
                Cursor = Cursors.Hand;
                BackColor = BackDark;
            }

            public bool Active
            {
                get { return _active; }
                set { if (_active == value) return; _active = value; Invalidate(); }
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                g.Clear(BackDark);

                Rectangle pill = new Rectangle(2, 3, Width - 5, Height - 7);
                if (_active)
                {
                    using (GraphicsPath path = StatCard.RoundedRect(pill, 9))
                    using (Brush b = new SolidBrush(UiTheme.Primary))
                        g.FillPath(b, path);
                }
                else if (_hover)
                {
                    using (GraphicsPath path = StatCard.RoundedRect(pill, 9))
                    using (Brush b = new SolidBrush(BackDarker))
                        g.FillPath(b, path);
                }

                Color fg = _active ? Color.White : ItemText;

                // آیکون سمت راست (شروعِ خواندن در RTL)
                using (Font f = IconFont.Get(12.5F))
                using (Brush b = new SolidBrush(fg))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(_glyph, f, b, new RectangleF(Width - 40, 0, 30, Height), sf);

                // متن، راست‌چین در فضای باقی‌مانده
                using (Font f = _active ? UiTheme.FontBold(UiTheme.SizeBody) : UiTheme.Font(UiTheme.SizeBody))
                using (Brush b = new SolidBrush(fg))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(Text, f, b, new RectangleF(10, 0, Width - 52, Height), sf);
            }
        }

        // نشانِ مربعِ گردِ برند (بالای نوار)
        private class BrandIcon : Control
        {
            public BrandIcon()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = BackDark;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(BackDark);

                int size = 46;
                Rectangle rect = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
                using (GraphicsPath path = StatCard.RoundedRect(rect, 12))
                using (Brush b = new SolidBrush(UiTheme.Primary))
                    g.FillPath(b, path);

                using (Font f = IconFont.Get(17F))
                using (Brush b = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(IconFont.People, f, b, rect, sf);
            }
        }
    }
}

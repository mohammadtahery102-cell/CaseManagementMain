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
        public static readonly Color BackDark   = ColorTranslator.FromHtml("#162231");
        public static readonly Color BackDarker = ColorTranslator.FromHtml("#1D2A3A");
        public static readonly Color ItemText   = ColorTranslator.FromHtml("#B9C2D8");
        public static readonly Color GroupText  = ColorTranslator.FromHtml("#6C7A99");

        private readonly Panel _itemsHost;
        private readonly List<NavItem> _items = new List<NavItem>();
        private readonly List<Control> _groupHeaders = new List<Control>();
        private Panel _brandTitles;
        private Label _footerLabel;
        private bool _collapsed;
        private bool _enterprise;
        private Timer _widthAnim;
        private int _activeIndex = -1;

        // گروهِ جاری که AddItem بعدی باید داخلش قرار بگیرد (برای آکاردئون).
        private Panel _currentGroupPanel;

        public SidebarNav(string brandTitle, string brandSubtitle)
        {
            Dock = DockStyle.Right;
            Width = 232;
            BackColor = BackDark;
            RightToLeft = RightToLeft.Yes;

            // ── سربرگ برند ──
            Panel brand = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = BackDark, Padding = new Padding(16, 14, 16, 10) };

            BrandIcon icon = new BrandIcon { Dock = DockStyle.Right, Width = 52 };

            Panel titles = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 8, 0) };
            Label lblSub = new Label
            {
                Text = brandSubtitle, Dock = DockStyle.Top, Height = 18, BackColor = Color.Transparent,
                Font = UiTheme.Font(UiTheme.SizeSmall - 2F), ForeColor = ColorTranslator.FromHtml("#94A3B8"),
                TextAlign = ContentAlignment.MiddleRight
            };
            Label lblTitle = new Label
            {
                Text = brandTitle, Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent,
                Font = UiTheme.FontBold(UiTheme.SizeLarge), ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleRight
            };
            _brandTitles = titles;
            titles.Controls.Add(lblTitle);
            titles.Controls.Add(lblSub);

            brand.Controls.Add(titles);
            brand.Controls.Add(icon);

            // ── میزبان آیتم‌ها (اسکرول‌پذیر برای صفحه‌های کوچک) ──
            _itemsHost = new Panel { Dock = DockStyle.Fill, BackColor = BackDark, AutoScroll = true, Padding = new Padding(10, 6, 10, 10) };

            Controls.Add(_itemsHost);
            Controls.Add(brand);
        }

        public void ApplyEnterpriseChrome()
        {
            _enterprise = true;
            Width = ErpUiPrefs.ExpandedWidth;
            _itemsHost.Padding = new Padding(12, 8, 12, 12);

            Button collapse = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = BackDarker,
                ForeColor = Color.White,
                Text = "جمع کردن منو  »",
                Font = UiTheme.Font(UiTheme.SizeSmall),
                Cursor = Cursors.Hand,
                TabStop = true
            };
            collapse.FlatAppearance.BorderSize = 0;
            collapse.Click += delegate { SetCollapsed(!_collapsed, animate: true, persist: true); };
            new ToolTip().SetToolTip(collapse, "جمع/باز کردن نوار کناری");
            collapse.Tag = "collapse";
            Controls.Add(collapse);

            Panel foot = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = BackDark,
                Padding = new Padding(12, 6, 12, 6)
            };
            _footerLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "● آنلاین   ·   v1.0.0",
                ForeColor = ColorTranslator.FromHtml("#94A3B8"),
                Font = UiTheme.Font(UiTheme.SizeSmall - 1F),
                TextAlign = ContentAlignment.MiddleRight
            };
            foot.Controls.Add(_footerLabel);
            Controls.Add(foot);
            _itemsHost.BringToFront();
            SetCollapsed(ErpUiPrefs.SidebarCollapsed, animate: false, persist: false);
        }

        public void SetCollapsed(bool collapsed, bool animate, bool persist)
        {
            if (!_enterprise) return;
            _collapsed = collapsed;
            if (persist) ErpUiPrefs.SidebarCollapsed = collapsed;
            ApplyCollapsedVisuals();
            int target = collapsed ? ErpUiPrefs.CollapsedWidth : ErpUiPrefs.ExpandedWidth;
            if (!animate || Width == target)
            {
                Width = target;
                return;
            }
            if (_widthAnim != null)
            {
                _widthAnim.Stop();
                _widthAnim.Dispose();
            }
            int from = Width;
            int steps = 10;
            int n = 0;
            _widthAnim = new Timer { Interval = 18 };
            _widthAnim.Tick += delegate
            {
                n++;
                double t = n / (double)steps;
                if (t >= 1)
                {
                    Width = target;
                    _widthAnim.Stop();
                    return;
                }
                double e = 1 - Math.Pow(1 - t, 3);
                Width = from + (int)Math.Round((target - from) * e);
            };
            _widthAnim.Start();
        }

        private void ApplyCollapsedVisuals()
        {
            if (_brandTitles != null) _brandTitles.Visible = !_collapsed;
            if (_footerLabel != null)
                _footerLabel.Text = _collapsed ? "●" : "● آنلاین   ·   v1.0.0";
            _itemsHost.Padding = _collapsed ? new Padding(6, 8, 6, 8) : new Padding(12, 8, 12, 12);
            foreach (Control h in _groupHeaders)
                h.Visible = !_collapsed;
            if (_collapsed)
            {
                foreach (Control c in _itemsHost.Controls)
                {
                    if (c is Panel && !_groupHeaders.Contains(c))
                        c.Visible = true;
                }
            }
            foreach (Control c in Controls)
            {
                Button b = c as Button;
                if (b != null && (c.Tag as string) == "collapse")
                    b.Text = _collapsed ? "«" : "جمع کردن منو  »";
            }
            Invalidate(true);
        }

        // عنوان گروه (مثل «اصلی»، «مالی و حسابداری» در طرح تصویری) — حالا
        // یک سربرگِ آکاردئون است: کلیک روی آن، آیتم‌های زیرش را جمع/باز می‌کند.
        // startExpanded=false برای گروه‌های پراستفاده‌کمتر (مثلاً «سازمانی» با
        // ده آیتم) استفاده می‌شود تا در بازِ اول کمتر اسکرول لازم باشد؛ هیچ
        // آیتمی حذف نمی‌شود، فقط پیش‌فرض جمع‌شده است و با یک کلیک باز می‌شود.
        public void AddGroup(string title, bool startExpanded = true)
        {
            Panel groupItems = new Panel
            {
                Dock = DockStyle.Top, Height = 0, BackColor = Color.Transparent,
                Visible = startExpanded
            };

            GroupHeader header = new GroupHeader(title) { Dock = DockStyle.Top, Expanded = startExpanded };
            header.ExpandedChanged += delegate { groupItems.Visible = header.Expanded; };

            AddToHost(header);
            AddToHost(groupItems);
            _groupHeaders.Add(header);

            _currentGroupPanel = groupItems;
        }

        public void EndGroup()
        {
            _currentGroupPanel = null;
        }

        public void AddCaption(string text)
        {
            Label cap = new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 22,
                BackColor = Color.Transparent,
                ForeColor = GroupText,
                Font = UiTheme.FontBold(UiTheme.SizeSmall - 1F),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(10, 0, 8, 0)
            };
            _groupHeaders.Add(cap);
            if (_currentGroupPanel != null)
            {
                _currentGroupPanel.Controls.Add(cap);
                cap.SendToBack();
                _currentGroupPanel.Height += cap.Height;
            }
            else
                AddToHost(cap);
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

            if (_currentGroupPanel != null)
            {
                _currentGroupPanel.Controls.Add(item);
                item.SendToBack();
                _currentGroupPanel.Height += item.Height;
            }
            else
            {
                AddToHost(item);
            }
            return index;
        }

        // آموزش — Dock=Top آیتم‌ها را به ترتیبِ «معکوسِ» افزودن می‌چیند، چون هر
        // کنترلِ جدید بالای قبلی‌ها می‌نشیند. برای همین هر آیتم بعد از افزودن
        // به انتهای ترتیبِ z فرستاده می‌شود تا ترتیب بصری = ترتیب افزودن باشد.
        private void AddToHost(Control c)
        {
            _itemsHost.Controls.Add(c);
            c.SendToBack();
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
                Height = 40;
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
                    using (GraphicsPath path = StatCard.RoundedRect(pill, 10))
                    using (Brush b = new SolidBrush(Color.FromArgb(41, 37, 99, 235)))
                        g.FillPath(b, path);
                    using (Brush bar = new SolidBrush(ColorTranslator.FromHtml("#2563EB")))
                        g.FillRectangle(bar, Width - 6, 10, 4, Height - 20);
                }
                else if (_hover)
                {
                    using (GraphicsPath path = StatCard.RoundedRect(pill, 9))
                    using (Brush b = new SolidBrush(BackDarker))
                        g.FillPath(b, path);
                }

                Color fg = _active ? Color.White : ItemText;

                RectangleF iconBox = Width < 100
                    ? new RectangleF(0, 0, Width, Height)
                    : new RectangleF(Width - 40, 0, 30, Height);
                using (Font f = IconFont.Get(12.5F))
                using (Brush b = new SolidBrush(fg))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(_glyph, f, b, iconBox, sf);

                if (Width >= 100)
                {
                    using (Font f = _active ? UiTheme.FontBold(UiTheme.SizeBody + 1F) : UiTheme.Font(UiTheme.SizeBody + 1F))
                    using (Brush b = new SolidBrush(fg))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                        g.DrawString(Text, f, b, new RectangleF(10, 0, Width - 52, Height), sf);
                }
            }
        }

        // ── سربرگِ آکاردئونِ یک گروه (عنوان + فلش باز/بسته) ──
        private class GroupHeader : Control
        {
            private readonly string _title;
            private bool _expanded;
            private bool _hover;

            public event EventHandler ExpandedChanged;

            public GroupHeader(string title)
            {
                _title = title;
                Text = title;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Height = 36;
                Cursor = Cursors.Hand;
                BackColor = BackDark;
            }

            public bool Expanded
            {
                get { return _expanded; }
                set
                {
                    if (_expanded == value) return;
                    _expanded = value;
                    Invalidate();
                    if (ExpandedChanged != null) ExpandedChanged(this, EventArgs.Empty);
                }
            }

            protected override void OnClick(EventArgs e)
            {
                Expanded = !Expanded;
                base.OnClick(e);
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(BackDark);

                Color fg = _hover ? Color.White : GroupText;

                // متنِ عنوانِ گروه، راست‌چین (شروع خواندن در RTL)
                using (Font f = UiTheme.FontBold(UiTheme.SizeSmall))
                using (Brush b = new SolidBrush(fg))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(_title, f, b, new RectangleF(20, 0, Width - 28, Height), sf);

                // فلشِ باز/بسته سمت چپ عنوان
                using (Font f = UiTheme.Font(UiTheme.SizeSmall - 1F))
                using (Brush b = new SolidBrush(fg))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(_expanded ? "▾" : "◂", f, b, new RectangleF(0, 0, 18, Height), sf);

                // خط جداکنندهٔ ظریف زیر سربرگ برای سلسله‌مراتب بصری بهتر
                using (Pen p = new Pen(BackDarker))
                    g.DrawLine(p, 4, Height - 1, Width - 4, Height - 1);
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

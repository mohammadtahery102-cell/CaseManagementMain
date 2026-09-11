using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // نوار ناوبری کناری RTL. ظاهر و آکاردئون فقط؛ مقصد کلیک‌ها در فراخواننده است.
    public class SidebarNav : Panel
    {
        public static readonly Color BackDark   = ColorTranslator.FromHtml("#152033");
        public static readonly Color BackDarker = ColorTranslator.FromHtml("#1C2B3E");
        public static readonly Color ItemText   = ColorTranslator.FromHtml("#A8B6CC");
        public static readonly Color GroupText  = ColorTranslator.FromHtml("#F1F5F9");
        public static readonly Color ChildText  = ColorTranslator.FromHtml("#94A3B8");
        public static readonly Color Accent     = ColorTranslator.FromHtml("#3B82F6");

        private readonly FlowLayoutPanel _itemsHost;
        private readonly List<NavItem> _items = new List<NavItem>();
        private readonly List<Control> _groupHeaders = new List<Control>();
        private readonly List<GroupEntry> _groups = new List<GroupEntry>();
        private Panel _brandTitles;
        private Label _footerLabel;
        private bool _collapsed;
        private bool _enterprise;
        private bool _suspendGroupEvents;
        private Timer _widthAnim;
        private int _activeIndex = -1;
        private GroupEntry _currentGroup;

        public SidebarNav(string brandTitle, string brandSubtitle)
        {
            Dock = DockStyle.Right;
            Width = 232;
            BackColor = BackDark;
            RightToLeft = RightToLeft.Yes;
            TabStop = true;

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

            // TopDown: داشبورد بالا، تنظیمات پایین؛ زیرمنو زیر دکمه باز می‌شود.
            _itemsHost = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = BackDark,
                AutoScroll = true,
                Padding = new Padding(8, 8, 8, 12),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            _itemsHost.Layout += delegate { StretchHostChildren(); };
            Controls.Add(_itemsHost);
            Controls.Add(brand);
        }

        public void ApplyEnterpriseChrome()
        {
            _enterprise = true;
            Width = ErpUiPrefs.ExpandedWidth;
            _itemsHost.Padding = new Padding(10, 8, 10, 12);

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
            new ToolTip().SetToolTip(collapse, "جمع یا باز کردن نوار کناری");
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopAnim(ref _widthAnim);
                for (int i = 0; i < _groups.Count; i++)
                {
                    GroupEntry ge = _groups[i];
                    StopAnim(ref ge.Anim);
                }
            }
            base.Dispose(disposing);
        }

        private static void StopAnim(ref Timer tmr)
        {
            if (tmr == null) return;
            tmr.Stop();
            tmr.Dispose();
            tmr = null;
        }

        private void ApplyCollapsedVisuals()
        {
            if (_brandTitles != null) _brandTitles.Visible = !_collapsed;
            if (_footerLabel != null)
                _footerLabel.Text = _collapsed ? "●" : "● آنلاین   ·   v1.0.0";
            _itemsHost.Padding = _collapsed ? new Padding(6, 8, 6, 8) : new Padding(10, 8, 10, 12);
            foreach (Control h in _groupHeaders)
                h.Visible = !_collapsed;
            foreach (Control c in Controls)
            {
                Button b = c as Button;
                if (b != null && (c.Tag as string) == "collapse")
                    b.Text = _collapsed ? "«" : "جمع کردن منو  »";
            }
            Invalidate(true);
        }

        public void AddGroup(string title, bool startExpanded = true)
        {
            AddGroup(title, "", startExpanded);
        }

        public void AddGroup(string title, string glyph, bool startExpanded)
        {
            FlowLayoutPanel groupItems = new FlowLayoutPanel
            {
                Height = 0,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 0, 6),
                Margin = new Padding(0),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                RightToLeft = RightToLeft.No
            };
            groupItems.Layout += delegate { StretchGroupChildren(groupItems); };

            GroupHeader header = new GroupHeader(title, glyph)
            {
                Expanded = startExpanded
            };

            var entry = new GroupEntry { Header = header, Items = groupItems, FullHeight = 6 };
            header.ExpandedChanged += delegate { OnGroupToggled(entry); };

            AddToHost(header);
            AddToHost(groupItems);
            _groupHeaders.Add(header);
            _groups.Add(entry);
            _currentGroup = entry;

            if (startExpanded)
                groupItems.Height = entry.FullHeight;
        }

        public void EndGroup()
        {
            _currentGroup = null;
        }

        public void AddDivider()
        {
            Panel line = new Panel
            {
                Dock = DockStyle.Top,
                Height = 14,
                BackColor = Color.Transparent
            };
            line.Paint += delegate (object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Color.FromArgb(55, 255, 255, 255)))
                    e.Graphics.DrawLine(p, 14, 7, line.Width - 14, 7);
            };
            AddToHost(line);
        }

        public void AddCaption(string text)
        {
            Label cap = new Label
            {
                Text = text,
                Height = 26,
                BackColor = Color.Transparent,
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                Font = UiTheme.FontBold(UiTheme.SizeSmall - 1F),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(10, 4, 36, 0),
                Margin = new Padding(0),
                RightToLeft = RightToLeft.Yes
            };
            _groupHeaders.Add(cap);
            AttachToCurrent(cap, cap.Height);
        }

        public int AddItem(string glyph, string text, EventHandler onClick)
        {
            int index = _items.Count;
            bool child = _currentGroup != null;
            NavItem item = new NavItem(glyph, text, child);
            item.Click += delegate
            {
                SetActive(index);
                if (onClick != null) onClick(item, EventArgs.Empty);
            };
            _items.Add(item);
            AttachToCurrent(item, item.Height);
            return index;
        }

        private void AttachToCurrent(Control c, int height)
        {
            if (_currentGroup != null)
            {
                c.Dock = DockStyle.None;
                c.Margin = new Padding(0);
                int inner = _currentGroup.Items.ClientSize.Width;
                if (inner > 20) c.Width = inner;
                _currentGroup.Items.Controls.Add(c);
                _currentGroup.FullHeight += height;
                if (_currentGroup.Header.Expanded && (_currentGroup.Anim == null))
                    _currentGroup.Items.Height = _currentGroup.FullHeight;
            }
            else
            {
                AddToHost(c);
            }
        }

        private void AddToHost(Control c)
        {
            c.Dock = DockStyle.None;
            c.Margin = new Padding(0);
            int w = HostContentWidth();
            if (w > 20) c.Width = w;
            _itemsHost.Controls.Add(c);
        }

        private int HostContentWidth()
        {
            return Math.Max(20, _itemsHost.ClientSize.Width - _itemsHost.Padding.Horizontal);
        }

        private void StretchHostChildren()
        {
            int w = HostContentWidth();
            foreach (Control c in _itemsHost.Controls)
            {
                if (c.Width != w) c.Width = w;
            }
        }

        private static void StretchGroupChildren(Control host)
        {
            int w = Math.Max(20, host.ClientSize.Width - host.Padding.Horizontal);
            foreach (Control c in host.Controls)
            {
                if (c.Width != w) c.Width = w;
            }
        }

        private void OnGroupToggled(GroupEntry entry)
        {
            if (_suspendGroupEvents) return;

            if (_enterprise && entry.Header.Expanded)
            {
                _suspendGroupEvents = true;
                try
                {
                    for (int i = 0; i < _groups.Count; i++)
                    {
                        GroupEntry other = _groups[i];
                        if (other == entry) continue;
                        if (other.Header.Expanded)
                            other.Header.Expanded = false;
                    }
                }
                finally { _suspendGroupEvents = false; }

                for (int i = 0; i < _groups.Count; i++)
                {
                    GroupEntry other = _groups[i];
                    if (other == entry) continue;
                    AnimateHeight(other, 0);
                }
            }

            AnimateHeight(entry, entry.Header.Expanded ? entry.FullHeight : 0);
            if (entry.Header.Expanded)
                ScrollGroupIntoView(entry);
        }

        private void AnimateHeight(GroupEntry g, int target)
        {
            StopAnim(ref g.Anim);

            if (IsDisposed || g.Items == null || g.Items.IsDisposed)
                return;

            int from = g.Items.Height;
            if (from == target)
            {
                if (target > 0) ScrollGroupIntoView(g);
                return;
            }

            int duration = 180;
            int interval = 16;
            int steps = Math.Max(1, duration / interval);
            int n = 0;
            Timer tmr = new Timer { Interval = interval };
            g.Anim = tmr;
            tmr.Tick += delegate
            {
                if (IsDisposed || g.Items == null || g.Items.IsDisposed)
                {
                    StopAnim(ref g.Anim);
                    return;
                }
                n++;
                double t = Math.Min(1.0, n / (double)steps);
                double e = t * t * (3 - 2 * t);
                try
                {
                    g.Items.Height = from + (int)Math.Round((target - from) * e);
                    _itemsHost.PerformLayout();
                }
                catch { StopAnim(ref g.Anim); return; }
                if (t >= 1)
                {
                    try { g.Items.Height = target; } catch { }
                    StopAnim(ref g.Anim);
                    _itemsHost.PerformLayout();
                    if (target > 0) ScrollGroupIntoView(g);
                }
            };
            tmr.Start();
        }

        private void ScrollGroupIntoView(GroupEntry g)
        {
            if (IsDisposed || _itemsHost == null || _itemsHost.IsDisposed) return;
            if (g.Header == null || !g.Header.Visible || !g.Header.IsHandleCreated) return;
            try
            {
                _itemsHost.PerformLayout();
                int y = g.Header.Top - 8;
                if (y < 0) y = 0;
                _itemsHost.AutoScrollPosition = new Point(0, y);
                _itemsHost.ScrollControlIntoView(g.Header);
            }
            catch { }
        }

        public void SetActive(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            _activeIndex = index;
            for (int i = 0; i < _items.Count; i++)
                _items[i].Active = (i == _activeIndex);
        }

        private sealed class GroupEntry
        {
            public GroupHeader Header;
            public FlowLayoutPanel Items;
            public int FullHeight;
            public Timer Anim;
        }

        private class NavItem : Control
        {
            private readonly string _glyph;
            private readonly bool _child;
            private bool _active;
            private bool _hover;

            public NavItem(string glyph, string text, bool child)
            {
                _glyph = glyph;
                _child = child;
                Text = text;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                          ControlStyles.Selectable, true);
                Height = child ? 34 : 42;
                Cursor = Cursors.Hand;
                BackColor = BackDark;
                TabStop = true;
                AccessibleRole = AccessibleRole.PushButton;
                AccessibleName = text;
            }

            public bool Active
            {
                get { return _active; }
                set { if (_active == value) return; _active = value; Invalidate(); }
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    OnClick(EventArgs.Empty);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                base.OnKeyDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(BackDark);

                bool rail = Width < 100;
                int indent = (_child && !rail) ? 28 : 0;
                Rectangle pill = new Rectangle(2, 2, Width - 5, Height - 5);

                if (_active)
                {
                    using (GraphicsPath path = StatCard.RoundedRect(pill, 8))
                    using (Brush b = new SolidBrush(_child
                        ? Color.FromArgb(36, 59, 130, 246)
                        : Color.FromArgb(56, 59, 130, 246)))
                        g.FillPath(b, path);
                    using (Brush bar = new SolidBrush(Accent))
                        g.FillRectangle(bar, Width - 5, 8, 3, Height - 16);
                }
                else if (_hover || Focused)
                {
                    using (GraphicsPath path = StatCard.RoundedRect(pill, 8))
                    using (Brush b = new SolidBrush(_child ? Color.FromArgb(28, 255, 255, 255) : BackDarker))
                        g.FillPath(b, path);
                }

                Color fg = _active
                    ? Color.White
                    : (_child ? ChildText : Color.White);

                RectangleF iconBox = rail
                    ? new RectangleF(0, 0, Width, Height)
                    : new RectangleF(Width - 38 - indent, 0, 28, Height);
                using (Font f = IconFont.Get(_child ? 11F : 13F))
                using (Brush b = new SolidBrush(fg))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(_glyph, f, b, iconBox, sf);

                if (!rail)
                {
                    float size = _child ? UiTheme.SizeSmall : UiTheme.SizeMedium;
                    Font itemFont = (_active || !_child) ? UiTheme.FontBold(size) : UiTheme.Font(size);
                    using (itemFont)
                    using (Brush b = new SolidBrush(fg))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                        g.DrawString(Text, itemFont, b, new RectangleF(10, 0, Width - 48 - indent, Height), sf);
                }

                if (Focused)
                {
                    using (Pen p = new Pen(Color.FromArgb(180, Accent)) { DashStyle = DashStyle.Dot })
                        g.DrawRectangle(p, 3, 3, Width - 8, Height - 8);
                }
            }
        }

        private class GroupHeader : Control
        {
            private readonly string _title;
            private readonly string _glyph;
            private bool _expanded;
            private bool _hover;

            public event EventHandler ExpandedChanged;

            public GroupHeader(string title, string glyph)
            {
                _title = title;
                _glyph = glyph ?? "";
                Text = title;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                          ControlStyles.Selectable, true);
                Height = 46;
                Cursor = Cursors.Hand;
                BackColor = BackDark;
                TabStop = true;
                AccessibleRole = AccessibleRole.PushButton;
                AccessibleName = title;
                AccessibleDescription = "گروه منو. Enter یا Space برای باز و بسته کردن.";
            }

            public bool Expanded
            {
                get { return _expanded; }
                set
                {
                    if (_expanded == value) return;
                    _expanded = value;
                    AccessibleName = _title + (_expanded ? "، باز" : "، جمع‌شده");
                    Invalidate();
                    if (ExpandedChanged != null) ExpandedChanged(this, EventArgs.Empty);
                }
            }

            protected override void OnClick(EventArgs e)
            {
                Expanded = !Expanded;
                base.OnClick(e);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    Expanded = !Expanded;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                base.OnKeyDown(e);
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(BackDark);

                Rectangle pill = new Rectangle(2, 3, Width - 5, Height - 6);
                if (_hover || Focused || _expanded)
                {
                    using (GraphicsPath path = StatCard.RoundedRect(pill, 10))
                    using (Brush b = new SolidBrush(_expanded
                        ? Color.FromArgb(40, 59, 130, 246)
                        : Color.FromArgb(22, 255, 255, 255)))
                        g.FillPath(b, path);
                }

                Color fg = GroupText;
                int chevronW = 22;
                RectangleF chevronBox = new RectangleF(6, 0, chevronW, Height);
                using (Font f = UiTheme.FontBold(13F))
                using (Brush b = new SolidBrush(_expanded ? Accent : ColorTranslator.FromHtml("#CBD5E1")))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(_expanded ? "▼" : "▶", f, b, chevronBox, sf);

                if (!string.IsNullOrEmpty(_glyph))
                {
                    Rectangle badge = new Rectangle(Width - 40, (Height - 26) / 2, 26, 26);
                    using (GraphicsPath path = StatCard.RoundedRect(badge, 7))
                    using (Brush b = new SolidBrush(Color.FromArgb(48, 59, 130, 246)))
                        g.FillPath(b, path);
                    using (Font f = IconFont.Get(11F))
                    using (Brush b = new SolidBrush(Color.White))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(_glyph, f, b, badge, sf);
                }

                float textRight = string.IsNullOrEmpty(_glyph) ? 12 : 46;
                using (Font f = UiTheme.FontBold(UiTheme.SizeMedium + 0.5F))
                using (Brush b = new SolidBrush(fg))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(_title, f, b, new RectangleF(chevronW + 8, 0, Width - chevronW - 8 - textRight, Height), sf);

                if (Focused)
                {
                    using (Pen p = new Pen(Color.FromArgb(180, Accent)) { DashStyle = DashStyle.Dot })
                        g.DrawRectangle(p, 3, 4, Width - 8, Height - 9);
                }
            }
        }

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

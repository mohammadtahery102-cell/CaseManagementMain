using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // نوار صفحه‌بندیِ فهرست‌ها — زیرساختِ مقیاس‌پذیری تا صدهزار پرونده.
    //
    // آموزش — مسئله‌ی واقعی: فهرست‌های این برنامه (پرونده‌ها، جستجوی پیشرفته،
    // اعضا، اسناد) کوئری‌شان هیچ LIMIT ندارد؛ یعنی همه‌ی ردیف‌ها از دیتابیس
    // خوانده، در یک DataTable ریخته، و به DataGridView بسته می‌شود. با ۱٬۶۰۰
    // پرونده این «کمی کند» است، ولی با ۱۰۰٬۰۰۰ پرونده هم زمانِ باز شدن فرم
    // به چند ثانیه می‌رسد و هم چند صد مگابایت حافظه مصرف می‌شود.
    //
    // راه‌حل استاندارد: صفحه‌بندیِ سمتِ دیتابیس. در هر لحظه فقط یک صفحه
    // (مثلاً ۱۰۰ ردیف) خوانده می‌شود — مستقل از این‌که کل جدول چند ردیف دارد.
    //
    // این کنترل «هیچ قابلیتی را حذف نمی‌کند»: همه‌ی ردیف‌ها همچنان در دسترس‌اند،
    // فقط به‌جای یک‌جا، صفحه‌به‌صفحه. خروجی‌های Excel/Word/PDF هم دست‌نخورده
    // می‌مانند چون آن‌ها کوئری خودشان را دارند.
    // ═════════════════════════════════════════════════════════════════════════
    public class GridPager : Panel
    {
        public event EventHandler PageChanged;

        private readonly Button _first, _prev, _next, _last;
        private readonly Label _info;
        private readonly ComboBox _pageSize;

        private int _pageIndex;      // صفر-مبنا
        private long _totalRows;

        public GridPager()
        {
            Dock = DockStyle.Bottom;
            Height = ResponsiveLayout.Scale(44);
            BackColor = UiTheme.CardBack;
            RightToLeft = RightToLeft.Yes;
            Padding = new Padding(ResponsiveLayout.Scale(10), ResponsiveLayout.Scale(6),
                                  ResponsiveLayout.Scale(10), ResponsiveLayout.Scale(6));

            // آموزش — چرا جدول، و نه یک FlowLayoutPanelِ ساده با عرض‌های ثابت:
            // نسخه‌ی اول همان‌طور نوشته شده بود و در پنل‌های عریض درست کار می‌کرد،
            // ولی در فهرستِ پرونده‌های FrmCase که فقط ~۵۶۰ پیکسل عرض دارد مجموعِ
            // عرض‌ها از جا بیشتر می‌شد و کمبوی «تعداد در صفحه» به بیرونِ کادر
            // رانده می‌شد (در آزمون با مختصات X=-73 دیده شد، یعنی کاملاً نامرئی).
            // حالا سه ستون داریم: دکمه‌ها و بخشِ «تعداد در صفحه» دقیقاً به اندازه‌ی
            // محتوایشان جا می‌گیرند و متنِ اطلاعات هرچه ماند را پر می‌کند؛ پس در
            // هیچ عرضی کنترلی بیرون نمی‌افتد.
            TableLayoutPanel flow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            flow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            flow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            flow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _first = MakeNavButton("«",  "صفحه‌ی اول");
            _prev  = MakeNavButton("‹",  "صفحه‌ی قبل");
            _next  = MakeNavButton("›",  "صفحه‌ی بعد");
            _last  = MakeNavButton("»",  "صفحه‌ی آخر");

            _first.Click += delegate { GoTo(0); };
            _prev.Click  += delegate { GoTo(_pageIndex - 1); };
            _next.Click  += delegate { GoTo(_pageIndex + 1); };
            _last.Click  += delegate { GoTo(PageCount - 1); };

            FlowLayoutPanel navGroup = MakeGroup();
            navGroup.Controls.Add(_first);
            navGroup.Controls.Add(_prev);
            navGroup.Controls.Add(_next);
            navGroup.Controls.Add(_last);

            _info = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(ResponsiveLayout.Scale(8), 0, ResponsiveLayout.Scale(8), 0)
            };

            _pageSize = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ResponsiveLayout.Scale(72),
                Font = UiTheme.Font(UiTheme.SizeSmall),
                Margin = new Padding(ResponsiveLayout.Scale(6), ResponsiveLayout.Scale(3), 0, 0)
            };
            _pageSize.Items.AddRange(new object[] { "50", "100", "200", "500" });
            _pageSize.SelectedIndex = 1;   // پیش‌فرض ۱۰۰
            _pageSize.SelectedIndexChanged += delegate { GoTo(0); };

            Label lblSize = new Label
            {
                Text = "تعداد در صفحه:", AutoSize = true,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, ResponsiveLayout.Scale(8), 0, 0)
            };

            FlowLayoutPanel sizeGroup = MakeGroup();
            sizeGroup.Controls.Add(lblSize);
            sizeGroup.Controls.Add(_pageSize);

            flow.Controls.Add(navGroup,  0, 0);
            flow.Controls.Add(_info,     1, 0);
            flow.Controls.Add(sizeGroup, 2, 0);

            Controls.Add(flow);

            Panel divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiTheme.Border };
            Controls.Add(divider);

            UpdateInfo();
        }

        // گروهی که فقط به اندازه‌ی محتوایش جا می‌گیرد (برای ستون‌های AutoSize).
        private static FlowLayoutPanel MakeGroup()
        {
            return new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
        }

        private Button MakeNavButton(string glyph, string tip)
        {
            Button b = new Button
            {
                Text = glyph,
                Width = ResponsiveLayout.Scale(38),
                Height = ResponsiveLayout.Scale(30),
                FlatStyle = FlatStyle.Flat,
                Font = UiTheme.FontBold(UiTheme.SizeBody),
                BackColor = Color.White,
                ForeColor = UiTheme.TextDark,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop = false,
                Margin = new Padding(ResponsiveLayout.Scale(2), 0, ResponsiveLayout.Scale(2), 0)
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = UiTheme.Border;
            b.FlatAppearance.MouseOverBackColor = UiTheme.HoverTint;
            new ToolTip().SetToolTip(b, tip);
            return b;
        }

        // ─── وضعیت صفحه ──────────────────────────────────────────────────────
        public int PageSize
        {
            get
            {
                int n;
                return int.TryParse(Convert.ToString(_pageSize.SelectedItem), out n) && n > 0 ? n : 100;
            }
        }

        public int PageIndex { get { return _pageIndex; } }
        public int Offset { get { return _pageIndex * PageSize; } }
        public long TotalRows { get { return _totalRows; } }

        public int PageCount
        {
            get
            {
                if (_totalRows <= 0) return 1;
                return (int)Math.Ceiling(_totalRows / (double)PageSize);
            }
        }

        // فراخوانِ فرم بعد از هر بار بارگذاری، تعداد کلِ ردیف‌ها را اعلام می‌کند
        // تا نوار بداند چند صفحه وجود دارد.
        public void SetTotal(long totalRows)
        {
            _totalRows = Math.Max(0, totalRows);
            if (_pageIndex >= PageCount) _pageIndex = Math.Max(0, PageCount - 1);
            UpdateInfo();
        }

        public void Reset()
        {
            _pageIndex = 0;
            UpdateInfo();
        }

        private void GoTo(int index)
        {
            int clamped = Math.Max(0, Math.Min(index, PageCount - 1));
            if (clamped == _pageIndex && index == clamped) { UpdateInfo(); return; }
            _pageIndex = clamped;
            UpdateInfo();
            if (PageChanged != null) PageChanged(this, EventArgs.Empty);
        }

        private readonly ToolTip _infoTip = new ToolTip();

        private void UpdateInfo()
        {
            // آموزش — این محافظ لازم است: انتساب Height در سازنده رویداد
            // SizeChanged را شلیک می‌کند، و آن موقع هنوز هیچ‌کدام از کنترل‌های
            // داخلی ساخته نشده‌اند. بدون این بررسی، همان‌جا NullReferenceException
            // می‌گرفتیم و فرم اصلاً باز نمی‌شد.
            if (_pageSize == null || _info == null || _first == null) return;

            long from = _totalRows == 0 ? 0 : Offset + 1;
            long to = Math.Min(_totalRows, (long)Offset + PageSize);

            // آموزش — چرا قالب‌بندی به‌جای چسباندنِ رشته‌ها: این متن عدد دارد،
            // پس هیچ‌وقت با فرهنگِ لغت تطبیق نمی‌خورد. با نگه داشتنِ کلِ جمله
            // به‌عنوان یک «قالب» با جای‌نگه‌دار، خودِ جمله ترجمه‌پذیر می‌شود و
            // ترتیبِ کلمات هم در هر زبان می‌تواند فرق کند.
            string full = string.Format(
                Lang.T("نمایش {0} تا {1} از {2} مورد   ·   صفحه {3} از {4}"),
                from.ToString("N0"), to.ToString("N0"), _totalRows.ToString("N0"),
                (_pageIndex + 1).ToString("N0"), PageCount.ToString("N0"));

            string shortText = string.Format(
                Lang.T("صفحه {0} از {1}  ·  {2} مورد"),
                (_pageIndex + 1).ToString("N0"), PageCount.ToString("N0"), _totalRows.ToString("N0"));

            // آموزش — در پنل‌های باریک متنِ کامل جا نمی‌شود و با «…» بریده می‌شد.
            // به‌جای بریدن، نسخه‌ی کوتاه نشان داده می‌شود و متنِ کامل همیشه در
            // Tooltip در دسترس است؛ پس هیچ اطلاعاتی از دست نمی‌رود.
            _info.Text = FitsInInfo(full) ? full : shortText;
            _infoTip.SetToolTip(_info, full);

            _first.Enabled = _prev.Enabled = _pageIndex > 0;
            _next.Enabled = _last.Enabled = _pageIndex < PageCount - 1;
        }

        private bool FitsInInfo(string text)
        {
            if (_info.Width <= 0) return true;   // هنوز چیده نشده؛ بعداً دوباره حساب می‌شود
            using (Graphics g = _info.CreateGraphics())
                return g.MeasureString(text, _info.Font).Width <= _info.Width - 4;
        }

        // با تغییر عرض، ممکن است متنِ کامل جا بشود یا نشود.
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateInfo();
        }
    }
}

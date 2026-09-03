using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    // ─────────────────────────────────────────────────────────────────────────
    // فرم هاب «حسابداری داخلی ایتام» — تب‌های اطلاعات پایه و عملیات مالی.
    // آموزش: مطابق الگوی فرم‌های موجود (FrmDashboard/FrmSettings) با TabControl
    // راست‌به‌چپ و کنترل‌های UiTheme ساخته شده تا ظاهر یکسان و حرفه‌ای باشد.
    // منطق داده در AccountingRepo است (جدایی لایه‌ها).
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmAccounting : Form
    {
        private readonly AccountingRepo _repo = new AccountingRepo();

        public FrmAccounting()
        {
            BuildUi();

            // مثل فرم تنظیمات، این فرم هم چند تب با دکمه‌های هم‌نام دارد؛ پس
            // هدفِ هر میان‌بُر در لحظه‌ی فشردن و از روی تبِ نمایان پیدا می‌شود.
            Helpers.FormShortcuts.For(this)
                .SaveVisible()
                .BindVisible(Keys.Control | Keys.P, "چاپ (تبِ جاری)", "چاپ")
                .BindVisible(Keys.F5, "اجرا / تازه‌سازی (تبِ جاری)", "اجرا");
        }

        private void BuildUi()
        {
            Text = "حسابداری داخلی ایتام  —  " + SecurityContext.CenterDisplay;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1280, 740);

            // سربرگ
            Panel banner = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = UiTheme.PrimaryDark };
            Label lblTitle = new Label
            {
                Text = "💰  حسابداری داخلی ایتام",
                Dock = DockStyle.Fill, ForeColor = Color.White, Font = UiTheme.FontBold(15F),
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 20, 0)
            };
            banner.Controls.Add(lblTitle);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = UiTheme.FontBold(10F);
            tabs.RightToLeft = RightToLeft.Yes;
            tabs.RightToLeftLayout = true;

            tabs.TabPages.Add(BuildTransactionsTab());   // دریافت/پرداخت + دفتر صندوق
            tabs.TabPages.Add(BuildStipendTab());        // شهریه ایتام
            tabs.TabPages.Add(BuildSalaryTab());         // حقوق کارکنان
            tabs.TabPages.Add(BuildExpenseItemsTab());   // هزینه‌های جاری
            tabs.TabPages.Add(BuildReportsTab());        // گزارش‌ها
            tabs.TabPages.Add(BuildPeriodsTab());        // دوره مالی
            tabs.TabPages.Add(BuildFundsTab());          // صندوق
            tabs.TabPages.Add(BuildPartiesTab());        // طرف حساب
            tabs.TabPages.Add(BuildCategoriesTab(true)); // دسته‌بندی درآمد
            tabs.TabPages.Add(BuildCategoriesTab(false));// دسته‌بندی هزینه
            tabs.TabPages.Add(BuildIntegrityTab());       // بررسی صحت حسابداری
            tabs.TabPages.Add(BuildSettingsTab());       // تنظیمات گزارش
            tabs.TabPages.Add(BuildAccBackupTab());      // بکاپ/بازیابی مستقل حسابداری

            Controls.Add(tabs);
            Controls.Add(banner);

            // آموزش — رفع باگ «عنوان‌ها و فیلدهای همه‌ی تب‌های حسابداری چپ‌چین
            // بودند»: علت «دو بار آینه‌شدن» بود. این فرم RightToLeft=Yes دارد و
            // ForceRtl پایین آن را به همه‌ی فرزندان هم می‌دهد؛ پنل‌های این فرم
            // *علاوه بر آن* FlowDirection=RightToLeft هم داشتند. این دو آینه
            // یکدیگر را خنثی می‌کردند و نتیجه دوباره چپ‌به‌راست می‌شد.
            // همه‌ی FlowLayoutPanelهای این فرم حالا FlowDirection=LeftToRight
            // دارند — همان قراردادی که بقیه‌ی فرم‌های پروژه (داشبورد، جستجوی
            // پیشرفته، فرم‌های Enterprise) از قبل استفاده می‌کنند و در آن‌ها
            // چیدمان درست از سمت راست شروع می‌شود.
            //
            // ForceRtl همچنان لازم است: با ساخت پویا (نه Designer)، وراثتِ
            // RightToLeft همیشه به‌موقع resolve نمی‌شود.
            ForceRtl(this);
        }

        private static void ForceRtl(Control root)
        {
            root.RightToLeft = RightToLeft.Yes;
            var rtlLayoutProp = root.GetType().GetProperty("RightToLeftLayout");
            if (rtlLayoutProp != null && rtlLayoutProp.CanWrite)
                rtlLayoutProp.SetValue(root, true, null);

            foreach (Control child in root.Controls)
                ForceRtl(child);
        }

        // ═══════════════════════════════════════════════════════════════════
        // کمکی‌های ساخت کنترل
        // ═══════════════════════════════════════════════════════════════════
        private DataGridView NewGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false
            };
            UiTheme.StyleGrid(g);

            // آموزش — چرا پنهان‌کردنِ ستون‌های شناسه اینجا تکرار می‌شود با
            // اینکه در هر Load* هم نوشته شده: آن خط‌ها بلافاصله بعد از
            // انتساب DataSource اجرا می‌شوند، ولی در آن لحظه گرید هنوز روی
            // فرمِ نمایش‌داده‌شده نیست و BindingContext ندارد، پس ساختِ ستون‌ها
            // به تعویق می‌افتد و Columns خالی است. در نتیجه شرطِ
            // Columns.Contains("TxnID") نادرست می‌شود و پنهان‌سازی هیچ اثری
            // ندارد؛ بعداً که فرم نمایش داده می‌شود ستون‌ها ساخته و *نمایان*
            // می‌مانند. به همین دلیل TxnID/StipendID/PeriodID/FundID روی
            // جدول‌ها دیده می‌شدند.
            //
            // DataBindingComplete دقیقاً بعد از ساختِ واقعیِ ستون‌ها اجرا
            // می‌شود، پس اینجا پنهان‌سازی همیشه اثر می‌کند. خط‌های قبلی حذف
            // نشدند تا اگر جایی زودتر اثر کرد، رفتار عوض نشود.
            g.DataBindingComplete += delegate { HideTechnicalColumns(g); };

            return g;
        }

        // ═══════════════════════════════════════════════════════════════════
        // چارچوبِ یکدستِ تب‌ها
        // ═══════════════════════════════════════════════════════════════════
        // آموزش — چرا این سه متد اضافه شدند: تبِ «دریافت/پرداخت» کارت‌بندی
        // داشت (عنوانِ بخش + کارتِ سفیدِ گردگوشه)، ولی دوازده تبِ دیگر
        // کنترل‌های لخت روی پس‌زمینه‌ی صاف بودند، با دکمه‌هایی به ارتفاع ۳۴ و
        // مختصاتِ مطلق در برابر ۳۸ و FlowLayout. نتیجه این بود که هر تب مثل
        // بخشی از یک برنامه‌ی دیگر به نظر می‌رسید.
        //
        // این سه متد همان چیدمانِ تبِ اول را یک‌جا تعریف می‌کنند تا هر تب فقط
        // صدایشان بزند. هیچ کنترلی حذف نمی‌شود و هیچ رویدادی جابه‌جا نمی‌شود —
        // فقط قابِ دورشان یکی می‌شود.
        //
        // ترتیبِ افزودن مهم است: در WinForms کنترلی که *دیرتر* اضافه شود در
        // چیدمانِ Dock جلوتر می‌نشیند. پس همیشه اول جدول (Fill)، بعد نوار
        // دکمه‌ها، و آخر کارتِ فرم افزوده می‌شود.

        private const int TabButtonHeight = 38;

        // کارتِ بالای تب: فیلدهای ورودی.
        private static Panel MakeFormCard(string title, Control content)
        {
            // محتوا دیگر خودش پس‌زمینه نمی‌کشد؛ کارت آن را می‌کشد.
            content.BackColor = Color.Transparent;
            content.Dock = DockStyle.Top;

            var card = new Helpers.SectionCard
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(2, 2, 2, 10)
            };

            var header = new Label
            {
                Dock = DockStyle.Top, Height = 38, Text = title,
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 16, 0),
                BackColor = Color.Transparent
            };

            card.Controls.Add(content);
            card.Controls.Add(header);

            var wrap = new Panel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 10, 10, 0), BackColor = Color.Transparent
            };
            wrap.Controls.Add(card);
            return wrap;
        }

        // کارتِ پایین تب: جدول.
        private static Panel MakeGridCard(string title, Control grid)
        {
            var card = new Helpers.SectionCard
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 8, 10, 10)
            };

            var header = new Label
            {
                Dock = DockStyle.Top, Height = 36, Text = title,
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 8, 0),
                BackColor = Color.Transparent
            };

            card.Controls.Add(grid);
            card.Controls.Add(header);

            var wrap = new Panel
            {
                Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 10),
                BackColor = Color.Transparent
            };
            wrap.Controls.Add(card);
            return wrap;
        }

        // نوارِ دکمه‌ها. عرضِ هر دکمه همان چیزی می‌ماند که تب تعیین کرده؛ فقط
        // ارتفاع، فاصله و گردیِ گوشه یکسان می‌شود. extras (مثل برچسبِ جمعِ کل)
        // بعد از دکمه‌ها در همان ردیف می‌نشیند.
        private static Panel MakeButtonBar(Control[] buttons, Control extra = null)
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true, BackColor = Color.Transparent,
                Padding = new Padding(12, 8, 12, 8)
            };

            foreach (Control c in buttons)
            {
                if (c == null) continue;

                var b = c as Button;
                if (b != null)
                {
                    // عرضِ تعیین‌شده‌ی تب حفظ می‌شود؛ اگر تعیین نشده، حداقلِ معقول.
                    int w = b.Width > 0 ? b.Width : 120;
                    b.Size = new Size(w, TabButtonHeight);
                    b.Margin = new Padding(4, 0, 4, 0);

                    Button bb = b;
                    bb.SizeChanged += delegate { UiTheme.RoundCorners(bb, 10); };
                    UiTheme.RoundCorners(bb, 10);
                }

                flow.Controls.Add(c);
            }

            if (extra != null)
            {
                extra.Margin = new Padding(16, 8, 4, 0);
                flow.Controls.Add(extra);
            }

            var bar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.Transparent };
            bar.Controls.Add(flow);
            return bar;
        }

        // ستون‌های کلیدِ فنی که هرگز نباید به کاربر نشان داده شوند. همان
        // نام‌هایی است که در سراسر این فرم یکی‌یکی پنهان می‌شدند.
        private static readonly string[] TechnicalColumns =
        {
            "TxnID", "StipendID", "SalaryID", "ItemID",
            "PeriodID", "FundID", "PartyID", "CatID"
        };

        private static void HideTechnicalColumns(DataGridView g)
        {
            foreach (string name in TechnicalColumns)
                if (g.Columns.Contains(name))
                    g.Columns[name].Visible = false;
        }

        // آموزش — ارتقای یکجای همه‌ی تب‌ها: این متد در ۶۰ جای این فرم صدا زده
        // می‌شود، پس با تغییرِ خودش، هر ۱۲ تب حسابداری بدون دست‌زدن به ساختار
        // هیچ‌کدام، ظاهرِ یکدستِ بقیه‌ی برنامه را می‌گیرند (ورودیِ گردگوشه با
        // حالت Focus/Hover و عنوانِ خودکار راست‌چین).
        //
        // امضای متد و پارامتر width عمداً حفظ شده تا هیچ‌یک از ۶۰ فراخوانی
        // نیاز به تغییر نداشته باشد و هیچ فیلدی از قلم نیفتد. width حالا
        // «عرضِ کمینه»ی فیلد است.
        private Panel Field(string label, Control input, int width)
        {
            var box = new Helpers.FieldBox(new Label(), label, input);
            box.Width = width;
            box.Margin = new Padding(6, 4, 6, 4);

            // NumericUpDown حاشیه‌ی بومی دارد که داخل قاب گردگوشه ناجور است.
            NumericUpDown nud = input as NumericUpDown;
            if (nud != null)
            {
                nud.BorderStyle = BorderStyle.None;
                nud.TextAlign = HorizontalAlignment.Right;
            }

            return box;
        }

        // مبلغ با جداکننده هزارگان (مثل الگوی موجود در FrmFinance) — مثلاً
        // 12000 هنگام تایپ به‌صورت 12,000 نمایش داده می‌شود تا خواناتر باشد.
        // آموزش — allowNegative فقط برای «مانده ابتدای دوره/صندوق» true است،
        // چون یک دوره ممکن است با کسری (مانده منفی) بسته شده باشد و این کسری
        // باید واقعی منتقل شود، نه با صفر جایگزین شود.
        private NumericUpDown NewAmountBox(int decimals = 0, bool allowNegative = false)
        {
            return new NumericUpDown
            {
                Maximum = 1000000000,
                Minimum = allowNegative ? -1000000000 : 0,
                DecimalPlaces = decimals,
                ThousandsSeparator = true,
                TextAlign = HorizontalAlignment.Right
            };
        }

        // ورودی دستی تاریخ شمسی با ماسک اسلش‌دار (سال/ماه/روز) + راهنمای فرمت
        // زیر فیلد — به‌جای پاپ‌آپ تقویم، برای ثبت سریع چند سند پشت‌سرهم.
        private MaskedTextBox NewDateBox()
        {
            var mtb = new MaskedTextBox("0000/00/00");
            mtb.PromptChar = '_';
            mtb.Text = PersianDateHelper.ToPersianDateString(DateTime.Today);
            mtb.TextAlign = HorizontalAlignment.Center;
            mtb.RightToLeft = RightToLeft.Yes;
            mtb.Font = UiTheme.Font(UiTheme.SizeBody);
            mtb.BorderStyle = BorderStyle.FixedSingle;
            return mtb;
        }

        // مثل Field، ولی راهنمای فرمت تاریخ زیر فیلد حفظ شده (حذف نشده).
        private Panel DateField(string label, MaskedTextBox box, int width = 190)
        {
            box.BorderStyle = BorderStyle.None;

            var field = new Helpers.FieldBox(new Label(), label, box);
            field.Dock = DockStyle.Top;

            var hint = new Label
            {
                Text = "فرمت: سال/ماه/روز — مثال 1404/05/12",
                AutoSize = false, Dock = DockStyle.Top, Height = 16,
                Font = UiTheme.Font(7.5F), ForeColor = Color.Gray,
                BackColor = Color.Transparent
            };
            // تراز راهنما هم مثل عنوان، خودکار «راستِ بصری» می‌شود.
            hint.HandleCreated += delegate
            {
                hint.TextAlign = Helpers.ResponsiveLayout.VisualRight(hint, ContentAlignment.MiddleRight);
            };

            var p = new Panel
            {
                Width = width,
                Height = Helpers.FieldBox.TotalHeight + 16,
                Margin = new Padding(6, 4, 6, 4),
                BackColor = Color.Transparent
            };
            p.Controls.Add(hint);
            p.Controls.Add(field);
            return p;
        }

        // ─── شبکه‌ی فیلدهای حسابداری (بازطراحی طبق طرح مرجع) ─────────────────
        // ستون‌های هم‌عرض با FieldBox — همان کنترلِ فیلدی که بقیه‌ی فرم‌های
        // برنامه استفاده می‌کنند، تا ظاهر یکدست شود و تراز عنوان‌ها هم خودکار
        // درست بماند (ResponsiveLayout.VisualRight).
        private static TableLayoutPanel MkAccFieldGrid(int columns)
        {
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 6, 14, 10),
                ColumnCount = columns,
                BackColor = Color.Transparent
            };
            for (int i = 0; i < columns; i++)
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
            return tlp;
        }

        private static Helpers.FieldBox AddAccField(TableLayoutPanel grid, string caption, Control input)
        {
            var box = new Helpers.FieldBox(new Label(), caption, input) { Dock = DockStyle.Top };
            grid.Controls.Add(box);
            return box;
        }

        // برچسبِ عددِ داخلِ کارت‌های خلاصه
        private static Label MakeSummaryValue()
        {
            return new Label
            {
                Text = "0", Dock = DockStyle.Fill, AutoSize = false,
                Font = UiTheme.FontBold(13F), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true
            };
        }

        // کارتِ خلاصه: عنوانِ کوچک بالا + مقدارِ درشت پایین، با ته‌رنگِ معنایی.
        private static Panel MakeSummaryTile(string caption, Label valueLabel, Color accent, Color tint)
        {
            var tile = new Helpers.SectionCard
            {
                Width = 172, Height = 76,
                Margin = new Padding(6, 0, 6, 0),
                Padding = new Padding(8, 6, 8, 6),
                BackColor = tint
            };

            var cap = new Label
            {
                Text = caption, Dock = DockStyle.Top, Height = 20,
                Font = UiTheme.FontBold(UiTheme.SizeSmall - 1F), ForeColor = accent,
                TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
            };

            tile.Controls.Add(valueLabel);
            tile.Controls.Add(cap);
            return tile;
        }

        private ComboBox NewCombo(bool dropDownList = true)
        {
            return new ComboBox { DropDownStyle = dropDownList ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown, Font = UiTheme.Font(UiTheme.SizeBody) };
        }

        private void BindCombo(ComboBox cmb, DataTable dt, string valueCol, string displayCol, bool addEmpty = false)
        {
            if (addEmpty)
            {
                DataRow r = dt.NewRow();
                r[valueCol] = DBNull.Value; r[displayCol] = "";
                dt.Rows.InsertAt(r, 0);
            }
            cmb.DataSource = dt;
            cmb.ValueMember = valueCol;
            cmb.DisplayMember = displayCol;
            cmb.SelectedIndex = -1;
        }

        private static double ParseNum(string s)
        {
            double d;
            s = (s ?? "").Trim().Replace(",", "");
            return double.TryParse(s, out d) ? d : 0;
        }

        // آموزش — رفع باگ بحرانی: System.Data.SQLite ستون‌های INTEGER را به‌صورت
        // Int64 (long) برمی‌گرداند، نه Int32 (int). به همین دلیل الگوی قبلی
        // «SelectedValue is int» همیشه false می‌شد و مقدار انتخاب‌شده‌ی کمبو
        // (دوره/طرف‌حساب/صندوق/دسته) به‌عنوان null تفسیر می‌شد؛ در نتیجه همه
        // رکوردها با PeriodID=NULL ذخیره می‌شدند و فیلتر گزارش‌ها بی‌اثر بود.
        // این helper هر نوع عددی (int/long) و DBNull/null را درست مدیریت می‌کند.
        private static int? ComboIntValue(ComboBox cmb)
        {
            object v = cmb.SelectedValue;
            if (v == null || v == DBNull.Value) return null;
            try { return Convert.ToInt32(v); }
            catch { return null; }
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: دریافت / پرداخت + دفتر صندوق
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _txnPeriod, _txnParty, _txnFund, _txnCategory;
        private ComboBox _txnDirection;
        private TextBox _txnDocNo, _txnQty, _txnDesc;
        private NumericUpDown _txnAmount, _txnDollar, _txnRate;
        private MaskedTextBox _txnDate;
        private DataGridView _gridTxn;
        private Label _lblFundBalance;
        // کارت‌های خلاصه‌ی بالای فهرست تراکنش‌ها (طرح مرجع). فقط نمایشی‌اند و
        // از همان داده‌ی گریدِ بارگذاری‌شده جمع می‌زنند — هیچ کوئری اضافه‌ای.
        private Label _lblTotalPaid;
        private Label _lblTotalReceived;

        // ─── حالتِ اصلاح سند ──────────────────────────────────────────────────
        // ۰ یعنی «ثبت سند تازه»؛ هر مقدار دیگری یعنی فرم دارد سندِ اصلاحیِ
        // جایگزینِ همان شناسه را می‌سازد. این تنها چیزی است که مسیرِ ذخیره را
        // تعیین می‌کند، پس هر جا فرم پاک می‌شود باید صفر شود.
        private int _revisingTxnId;
        private Button _btnSaveTxn;
        private Panel _pnlReviseBanner;
        private Label _lblReviseBanner;

        private TabPage BuildTransactionsTab()
        {
            var page = new TabPage("دریافت / پرداخت") { BackColor = UiTheme.Background };

            // فرم ورودی (بالا)
            var form = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent };

            _txnDirection = NewCombo(); _txnDirection.Items.AddRange(new object[] { "دریافت", "پرداخت" }); _txnDirection.SelectedIndex = 0;
            _txnDirection.SelectedIndexChanged += delegate { ReloadTxnCategories(); };
            // آموزش — شماره سند مسلسل، خودکار و غیرقابل ویرایش (به‌درخواست کاربر
            // و طبق اصول حسابداری). ReadOnly + رنگ متمایز تا کاربر متوجه شود دستی نیست.
            // با تغییر «دوره مالی» (رویداد پایین‌تر پس از ساخت _txnPeriod)، این
            // شماره دوباره محاسبه می‌شود تا در هر دوره از ۱ ریستارت شود.
            _txnDocNo = new TextBox { Text = _repo.NextDocNo(null), ReadOnly = true, BackColor = UiTheme.Background, TabStop = false };
            _txnDate = NewDateBox();
            _txnPeriod = NewCombo(); _txnParty = NewCombo(); _txnFund = NewCombo(); _txnCategory = NewCombo();
            _txnAmount = NewAmountBox(); _txnQty = new TextBox(); _txnDollar = NewAmountBox(2); _txnRate = NewAmountBox(2); _txnDesc = new TextBox();

            // آموزش — بازطراحی طبق طرح مرجع: به‌جای FlowLayoutPanel با عرض‌های
            // دستیِ متفاوت (که در عرض‌های مختلف نامرتب می‌شکست)، یک شبکه‌ی
            // منظم با همان FieldBox بقیه‌ی فرم‌ها. هیچ فیلدی حذف یا جابه‌جا
            // نشده؛ فقط چیدمان و ظاهرشان یکدست شد.
            var infoGrid = MkAccFieldGrid(4);
            AddAccField(infoGrid, "نوع",           _txnDirection);
            AddAccField(infoGrid, "شماره سند",     _txnDocNo);
            AddAccField(infoGrid, "تاریخ",         _txnDate);
            AddAccField(infoGrid, "دوره مالی",     _txnPeriod);
            AddAccField(infoGrid, "طرف حساب",      _txnParty);
            AddAccField(infoGrid, "صندوق",         _txnFund);
            AddAccField(infoGrid, "دسته‌بندی",     _txnCategory);
            AddAccField(infoGrid, "مبلغ (افغانی)", _txnAmount);
            AddAccField(infoGrid, "تعداد/مقدار",   _txnQty);
            AddAccField(infoGrid, "مبلغ دلاری",    _txnDollar);
            AddAccField(infoGrid, "نرخ دلار",      _txnRate);
            AddAccField(infoGrid, "توضیح",         _txnDesc);

            form.Controls.Add(infoGrid);

            // محاسبه خودکار: مبلغ افغانی = دلاری × نرخ (اگر هر دو وارد شوند)
            //
            // آموزش — رفع «بریدنِ خاموشِ مبلغ»: کد قبلی نتیجه را با
            // Math.Min(_txnAmount.Maximum, ...) به سقف می‌بُرید. یعنی اگر
            // دلار × نرخ از یک میلیارد بیشتر می‌شد، مبلغ بی‌سروصدا به سقف
            // تبدیل و همان ذخیره می‌شد — یک تفاوت مالی بزرگ بدون هیچ پیامی.
            // حالا به‌جای بریدن، به کاربر هشدار داده می‌شود و مبلغ دست‌نخورده
            // می‌ماند تا خودش تصمیم بگیرد.
            EventHandler calc = delegate
            {
                if (_txnDollar.Value <= 0 || _txnRate.Value <= 0) return;

                decimal computed = Math.Round(_txnDollar.Value * _txnRate.Value);
                if (computed > _txnAmount.Maximum)
                {
                    UiTheme.ShowWarning(this,
                        "حاصل «مبلغ دلاری × نرخ» برابر " + computed.ToString("N0") +
                        " افغانی است که از سقف مجاز (" + _txnAmount.Maximum.ToString("N0") +
                        ") بیشتر است.\nمبلغ افغانی به‌صورت خودکار پر نشد؛ لطفاً مقادیر را بررسی کنید.");
                    return;
                }

                _txnAmount.Value = computed;
            };
            _txnDollar.ValueChanged += calc;
            _txnRate.ValueChanged += calc;

            // ─── نوار دکمه‌ها (راست) + کارت‌های خلاصه (چپ) ────────────────────
            // آموزش — دکمه‌ها از مختصات مطلق (SetBounds) به FlowLayoutPanel
            // منتقل شدند تا در هر عرضی مرتب بمانند و در مقیاس‌های بالا روی هم
            // نیفتند. همان چهار دکمه با همان رویدادها.
            var btnFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true, BackColor = Color.Transparent,
                Padding = new Padding(12, 8, 12, 8)
            };

            var btnSave = UiTheme.CreateButton("ثبت تراکنش", "✔", UiTheme.Success);
            btnSave.Size = new Size(150, 38); btnSave.Margin = new Padding(4, 0, 4, 0);
            // آموزش — قفل‌کردن دکمه هنگام ثبت: بدون این، دو کلیک سریع پشت‌سرهم
            // دو بار SaveTransaction را اجرا می‌کرد و دو سند یکسان ثبت می‌شد.
            // (لایه‌ی داده هم مستقل از این، تکرار را می‌گیرد؛ این فقط جلوی
            // ایجاد شدنِ حالت را از همان ابتدا می‌گیرد.)
            btnSave.Click += delegate
            {
                btnSave.Enabled = false;
                try { SaveTransaction(); }
                finally { btnSave.Enabled = true; }
            };

            var btnNew = UiTheme.CreateSecondaryButton("فرم جدید", "＋");
            btnNew.Size = new Size(130, 38); btnNew.Margin = new Padding(4, 0, 4, 0);
            btnNew.Click += delegate { ResetTxnForm(); };

            var btnVoucher = UiTheme.CreateButton("چاپ فاکتور", "🧾", UiTheme.Primary);
            btnVoucher.Size = new Size(140, 38); btnVoucher.Margin = new Padding(4, 0, 4, 0);
            btnVoucher.Click += delegate { PrintSelectedVoucher(); };

            // «ویرایش» = ابطالِ سندِ انتخاب‌شده + صدور سندِ اصلاحی. اینجا فقط
            // فرم پر و حالتِ اصلاح روشن می‌شود؛ هیچ نوشتنی تا فشردنِ «ثبت»
            // انجام نمی‌گیرد.
            var btnEdit = UiTheme.CreateButton("ویرایش", "✎", UiTheme.Primary);
            btnEdit.Size = new Size(120, 38); btnEdit.Margin = new Padding(4, 0, 4, 0);
            btnEdit.Click += delegate { BeginReviseSelectedTxn(); };

            var btnDelete = UiTheme.CreateButton("حذف انتخاب‌شده", "✕", UiTheme.Danger);
            btnDelete.Size = new Size(160, 38); btnDelete.Margin = new Padding(4, 0, 4, 0);
            btnDelete.Click += delegate { DeleteSelectedTxn(); };

            // «قرارداد ترانسپورت» — سندِ اداریِ خدماتِ راننده. اگر تراکنشی در
            // جدول انتخاب شده باشد، قرارداد به آن پیوند می‌خورد (AdmDriverContract.TxnID)
            // و نسخهٔ امضاشده هم می‌تواند روی همان تراکنش آپلود شود. هیچ ستون
            // یا جدولِ مالی‌ای تغییر نمی‌کند.
            // «سند روی قالب رسمی» — همان تراکنش، روی شیت «سند پرداخت وجه».
            // دکمهٔ «چاپ فاکتور» بالا دست‌نخورده می‌ماند؛ این یک مسیر موازی است.
            var btnVoucherTpl = UiTheme.CreateSecondaryButton("سند روی قالب رسمی", "📄");
            btnVoucherTpl.Size = new Size(175, 38); btnVoucherTpl.Margin = new Padding(4, 0, 4, 0);
            btnVoucherTpl.Click += delegate { ExportSelectedVoucherTemplate(); };

            var btnContract = UiTheme.CreateSecondaryButton("قرارداد ترانسپورت", "🚚");
            btnContract.Size = new Size(175, 38); btnContract.Margin = new Padding(4, 0, 4, 0);
            btnContract.Click += delegate { ShowDriverContractForm(); };

            _btnSaveTxn = btnSave;

            foreach (Button b in new[] { btnSave, btnNew, btnVoucher, btnVoucherTpl, btnEdit, btnDelete, btnContract })
            {
                Button bb = b;
                bb.SizeChanged += delegate { UiTheme.RoundCorners(bb, 10); };
                UiTheme.RoundCorners(bb, 10);
                btnFlow.Controls.Add(bb);
            }

            // سه کارتِ خلاصه — مانده کل / جمع پرداخت / جمع دریافت (طبق طرح مرجع).
            // _lblFundBalance حذف نشده: همان کنترل قبلی است و همان متن را
            // می‌گیرد، فقط داخل کارتِ «مانده کل» نشسته.
            _lblFundBalance = new Label
            {
                Text = "", Dock = DockStyle.Fill, AutoSize = false,
                Font = UiTheme.FontBold(13F), ForeColor = UiTheme.PrimaryDark,
                TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true
            };

            var summary = new FlowLayoutPanel
            {
                Dock = DockStyle.Left, Width = 560, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, BackColor = Color.Transparent,
                Padding = new Padding(8, 6, 8, 6)
            };
            summary.Controls.Add(MakeSummaryTile("مانده کل", _lblFundBalance,
                ColorTranslator.FromHtml("#2563EB"), ColorTranslator.FromHtml("#EFF6FF")));
            _lblTotalPaid = MakeSummaryValue();
            summary.Controls.Add(MakeSummaryTile("جمع پرداخت", _lblTotalPaid,
                ColorTranslator.FromHtml("#EF4444"), ColorTranslator.FromHtml("#FEF2F2")));
            _lblTotalReceived = MakeSummaryValue();
            summary.Controls.Add(MakeSummaryTile("جمع دریافت", _lblTotalReceived,
                ColorTranslator.FromHtml("#22C55E"), ColorTranslator.FromHtml("#F0FDF4")));

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Color.Transparent };
            btnBar.Controls.Add(btnFlow);
            btnBar.Controls.Add(summary);

            _gridTxn = NewGrid();
            _gridTxn.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) PrintSelectedVoucher(); };

            var gridCard = new Helpers.SectionCard { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 10) };
            var gridHeader = new Label
            {
                Dock = DockStyle.Top, Height = 36, Text = "لیست تراکنش‌ها",
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 8, 0),
                BackColor = Color.Transparent
            };
            gridCard.Controls.Add(_gridTxn);
            gridCard.Controls.Add(gridHeader);

            var gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 10), BackColor = Color.Transparent };
            gridWrap.Controls.Add(gridCard);

            // کارتِ «اطلاعات اصلی» — دربرگیرنده‌ی شبکه‌ی فیلدها
            var infoCard = new Helpers.SectionCard { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(2, 2, 2, 10) };
            var infoHeader = new Label
            {
                Dock = DockStyle.Top, Height = 38, Text = "اطلاعات اصلی",
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 16, 0),
                BackColor = Color.Transparent
            };
            infoCard.Controls.Add(form);
            infoCard.Controls.Add(infoHeader);

            var infoWrap = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 10, 10, 0), BackColor = Color.Transparent };
            infoWrap.Controls.Add(infoCard);

            // نوارِ هشدارِ حالتِ اصلاح — به‌طور پیش‌فرض پنهان است. وقتی روشن
            // می‌شود کاربر باید بدون هیچ ابهامی بداند که «ثبت» دیگر یک سندِ
            // تازه‌ی مستقل نمی‌سازد، بلکه سندِ قبلی را باطل می‌کند.
            _lblReviseBanner = new Label
            {
                Dock = DockStyle.Fill, AutoSize = false, Text = "",
                Font = UiTheme.FontBold(UiTheme.SizeBody), ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 12, 0),
                BackColor = Color.Transparent
            };
            var btnCancelRevise = UiTheme.CreateSecondaryButton("انصراف از اصلاح", "✕");
            btnCancelRevise.Dock = DockStyle.Left; btnCancelRevise.Width = 160;
            btnCancelRevise.Click += delegate { ResetTxnForm(); };

            _pnlReviseBanner = new Panel
            {
                Dock = DockStyle.Top, Height = 44, Visible = false,
                BackColor = UiTheme.Danger, Padding = new Padding(8, 5, 8, 5)
            };
            _pnlReviseBanner.Controls.Add(_lblReviseBanner);
            _pnlReviseBanner.Controls.Add(btnCancelRevise);

            page.Controls.Add(gridWrap);
            page.Controls.Add(btnBar);
            page.Controls.Add(infoWrap);
            page.Controls.Add(_pnlReviseBanner);

            LoadTxnCombos();
            ReloadTxnCategories();
            LoadTransactions();
            return page;
        }

        // شماره سند را برای دوره‌ی فعلاً انتخاب‌شده دوباره محاسبه می‌کند
        // (بدون دوره انتخاب‌شده = شماره‌ی سراسری فقط برای پیش‌نمایش اولیه).
        private void RefreshDocNo()
        {
            _txnDocNo.Text = _repo.NextDocNo(ComboIntValue(_txnPeriod));
        }

        private void LoadTxnCombos()
        {
            BindCombo(_txnPeriod, _repo.GetPeriodsForCombo(), "PeriodID", "Display", true);
            _txnPeriod.SelectedIndexChanged += delegate { RefreshDocNo(); };
            BindCombo(_txnParty, _repo.GetPartiesForCombo(), "PartyID", "Display", true);
            BindCombo(_txnFund, _repo.GetFundsForCombo(), "FundID", "Display", true);
            _txnFund.SelectedIndexChanged += delegate { UpdateFundBalanceLabel(); };
        }

        private void ReloadTxnCategories()
        {
            bool income = _txnDirection.Text == "دریافت";
            BindCombo(_txnCategory, _repo.GetCategoriesForCombo(income), "CatID", "Display", true);

            // فیلدهای دلاری فقط برای دریافت معنی دارند
            bool showDollar = income;
            _txnDollar.Enabled = showDollar;
            _txnRate.Enabled = showDollar;
        }

        private void UpdateFundBalanceLabel()
        {
            if (_txnFund.SelectedValue == null || _txnFund.SelectedValue == DBNull.Value)
            {
                _lblFundBalance.Text = "";
                return;
            }
            int fundId = Convert.ToInt32(_txnFund.SelectedValue);
            double bal = _repo.GetFundBalance(fundId);
            _lblFundBalance.Text = "مانده فعلی صندوق «" + _txnFund.Text + "»:  " + bal.ToString("N0") + " افغانی";
        }

        // پاک‌سازی کامل فرم (دکمه «فرم جدید») — همه‌چیز خالی می‌شود، از جمله دوره
        // (پس شماره سند به حالت «بدون دوره» = پیش‌نمایش سراسری برمی‌گردد تا
        // کاربر دوباره دوره انتخاب کند و شماره‌ی واقعیِ همان دوره محاسبه شود).
        // ─── حالتِ اصلاح: روشن/خاموش ─────────────────────────────────────────
        // تنها جایی که _revisingTxnId عوض می‌شود، تا حالتِ فرم و آنچه دکمه‌ی
        // «ثبت» انجام می‌دهد هرگز از هم جدا نیفتند.
        private void SetReviseMode(int txnId, string docNo)
        {
            _revisingTxnId = txnId;

            bool on = txnId > 0;
            if (_pnlReviseBanner != null) _pnlReviseBanner.Visible = on;
            if (_lblReviseBanner != null)
                _lblReviseBanner.Text = on
                    ? "حالت اصلاح — با ثبت، سند شماره " + docNo + " باطل می‌شود و یک سند اصلاحی تازه صادر می‌گردد."
                    : "";
            if (_btnSaveTxn != null)
                _btnSaveTxn.Text = on ? "ثبت سند اصلاحی" : "ثبت";
        }

        private void BeginReviseSelectedTxn()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Edit")) { UiTheme.ShowWarning(this, "کاربر فقط مشاهده اجازه اصلاح ندارد."); return; }
            if (_gridTxn.CurrentRow == null || !_gridTxn.Columns.Contains("TxnID"))
            { UiTheme.ShowWarning(this, "ابتدا یک تراکنش را از جدول انتخاب کنید."); return; }

            object idv = _gridTxn.CurrentRow.Cells["TxnID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            int id = Convert.ToInt32(idv);

            DataRow r;
            try { r = _repo.GetTransactionForEdit(id); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در خواندن سند: " + ex.Message); return; }

            if (r == null) { UiTheme.ShowWarning(this, "سند پیدا نشد. فهرست را تازه کنید."); return; }
            if (Convert.ToInt32(r["IsReversed"]) != 0)
            { UiTheme.ShowWarning(this, "این سند قبلاً باطل شده و دیگر قابل اصلاح نیست."); return; }

            // دوره‌ی خودِ سند باید باز باشد؛ در دوره‌ی بسته هیچ سندی نه باطل
            // می‌شود و نه صادر. زودتر از ذخیره به کاربر می‌گوییم تا وقتش را
            // صرفِ پر کردنِ فرمی نکند که ثبت نخواهد شد.
            int? recPeriod = r["PeriodID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PeriodID"]);
            if (recPeriod != null && !_repo.IsPeriodOpen(recPeriod.Value))
            { UiTheme.ShowWarning(this, "دوره‌ی مالیِ این سند «بسته» است؛ سند بسته‌شده اصلاح نمی‌شود."); return; }

            _txnDirection.Text = Convert.ToString(r["Direction"]);
            SelectComboValue(_txnPeriod, r["PeriodID"]);
            SelectComboValue(_txnParty, r["PartyID"]);
            SelectComboValue(_txnFund, r["FundID"]);
            ReloadTxnCategories();
            SelectComboValue(_txnCategory, r["CategoryID"]);

            _txnDate.Text = Convert.ToString(r["TxnDate"]);
            _txnAmount.Value = ClampToNumeric(_txnAmount, r["Amount"]);
            _txnDollar.Value = ClampToNumeric(_txnDollar, r["DollarAmount"]);
            _txnRate.Value = ClampToNumeric(_txnRate, r["DollarRate"]);
            _txnQty.Text = Convert.ToString(r["Qty"]);
            _txnDesc.Text = Convert.ToString(r["Description"]);

            // شماره‌ی سندِ اصلاحی تازه گرفته می‌شود؛ شماره‌ی سندِ باطل‌شده
            // دوباره استفاده نمی‌شود (توضیح کامل در ReviseTransactionAtomic).
            RefreshDocNo();

            SetReviseMode(id, Convert.ToString(r["DocNo"]));
            UpdateFundBalanceLabel();
            _txnAmount.Focus();
        }

        // مقدارِ دیتابیس را داخل بازه‌ی مجازِ کنترل نگه می‌دارد.
        // آموزش: اگر مقدار از Maximum کنترل بزرگ‌تر باشد، NumericUpDown استثنا
        // پرتاب می‌کند و فرم وسطِ بارگذاری می‌شکند. بستنِ مقدار به بازه، بدترین
        // حالت را به یک عددِ قابلِ اصلاح تبدیل می‌کند نه یک کرش.
        private static decimal ClampToNumeric(NumericUpDown ctl, object dbValue)
        {
            if (dbValue == null || dbValue == DBNull.Value) return 0m;

            decimal v;
            try { v = Convert.ToDecimal(dbValue); }
            catch { return 0m; }

            if (v < ctl.Minimum) return ctl.Minimum;
            if (v > ctl.Maximum) return ctl.Maximum;
            return v;
        }

        private void ResetTxnForm()
        {
            SetReviseMode(0, null);
            _txnPeriod.SelectedIndex = -1; _txnParty.SelectedIndex = -1; _txnFund.SelectedIndex = -1; _txnCategory.SelectedIndex = -1;
            RefreshDocNo();
            _txnDate.Text = PersianDateHelper.ToPersianDateString(DateTime.Today);
            _txnAmount.Value = 0; _txnQty.Text = ""; _txnDollar.Value = 0; _txnRate.Value = 0; _txnDesc.Text = "";
            _txnDirection.Focus();
        }

        // آموزش — «پاک‌سازی نرم» بعد از ثبت موفق: به‌درخواست کاربر، فیلدهای پایه
        // (دوره/طرف‌حساب/صندوق/دسته/نوع/تاریخ) که معمولاً برای چند سند پشت‌سرهم
        // یکسان‌اند حفظ می‌شوند و فقط شماره سند (به بعدیِ همین دوره) و فیلدهای
        // جزئیات (مبلغ/تعداد/دلاری/نرخ/توضیح) پاک می‌شوند تا دوباره‌کاری نشود.
        private void SoftResetTxnForm()
        {
            // بعد از ثبتِ موفق، حالتِ اصلاح باید خاموش شود؛ وگرنه ثبتِ بعدی
            // دوباره همان سندِ (حالا باطل‌شده‌ی) قبلی را هدف می‌گیرد.
            SetReviseMode(0, null);
            RefreshDocNo();
            _txnAmount.Value = 0; _txnQty.Text = ""; _txnDollar.Value = 0; _txnRate.Value = 0; _txnDesc.Text = "";
            _txnAmount.Focus();
        }

        private void SaveTransaction()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Edit")) { UiTheme.ShowWarning(this, "کاربر فقط مشاهده اجازه ثبت ندارد."); return; }
            double amount = (double)_txnAmount.Value;
            if (amount <= 0) { UiTheme.ShowWarning(this, "مبلغ معتبر وارد کنید."); _txnAmount.Focus(); return; }

            // اصول حسابداری: هر رویداد مالی باید به یک دوره مالی متصل باشد.
            int? period = ComboIntValue(_txnPeriod);
            if (period == null) { UiTheme.ShowWarning(this, "دوره مالی را انتخاب کنید. هر تراکنش باید به یک دوره متصل باشد."); _txnPeriod.Focus(); return; }
            if (!_repo.IsPeriodOpen(period.Value)) { UiTheme.ShowWarning(this, "این دوره مالی «بسته» است و امکان ثبت تراکنش جدید در آن وجود ندارد."); return; }

            if (_txnFund.SelectedValue == null || _txnFund.SelectedValue == DBNull.Value)
            { UiTheme.ShowWarning(this, "صندوق را انتخاب کنید."); return; }
            if (!_txnDate.MaskCompleted) { UiTheme.ShowWarning(this, "تاریخ را کامل وارد کنید (سال/ماه/روز)."); _txnDate.Focus(); return; }

            try
            {
                bool income = _txnDirection.Text == "دریافت";
                int? party = ComboIntValue(_txnParty);
                int fund = Convert.ToInt32(_txnFund.SelectedValue);
                int? cat = ComboIntValue(_txnCategory);
                double? dollar = (double)_txnDollar.Value; if (dollar <= 0) dollar = null;
                double? rate = (double)_txnRate.Value; if (rate <= 0) rate = null;

                // آموزش — شماره سند دیگر اینجا نهایی نمی‌شود. قبلاً بررسیِ
                // یکتایی و گرفتن شماره‌ی بعدی در فرم و روی کانکشن‌های جدا انجام
                // می‌شد، پس بین «بررسی» و «درج» فاصله‌ای بود که دو کاربر
                // هم‌زمان می‌توانستند در آن یک شماره بگیرند. حالا کل این کار
                // داخل تراکنش پایگاه‌داده در AddTransactionAtomic انجام می‌شود.
                string docNo = _txnDocNo.Text.Trim();

                // هشدار (نه ممانعت) در صورت منفی شدن مانده صندوق بعد از پرداخت
                if (!income)
                {
                    double balAfter = _repo.GetFundBalance(fund) - amount;
                    if (balAfter < 0 &&
                        !UiTheme.ShowConfirm(this, "بعد از این پرداخت، مانده صندوق منفی می‌شود (" + balAfter.ToString("N0") + " افغانی).\nآیا ادامه می‌دهید؟", "هشدار کسری صندوق"))
                        return;
                }

                // ─── حالتِ اصلاح ───
                // مسیرِ جداگانه‌ای است چون یک کارِ دیگر انجام می‌دهد: ابطالِ
                // سندِ قبلی و صدور سندِ جایگزین، هر دو در یک تراکنش اتمیک.
                int revising = _revisingTxnId;
                if (revising > 0)
                {
                    string editReason = AskReviseReason();
                    if (editReason == null) return;

                    AccountingRepo.TransactionSaveResult revised;
                    try
                    {
                        revised = _repo.ReviseTransactionAtomic(revising, docNo, _txnDate.Text, _txnDirection.Text,
                            period, party, fund, income ? "Income" : "Expense", cat, amount, _txnQty.Text.Trim(),
                            dollar, rate, _txnDesc.Text.Trim(), "", editReason, false);
                    }
                    catch (AccountingDuplicateException dup)
                    {
                        if (!UiTheme.ShowConfirm(this, dup.Message, "سند تکراری"))
                            return;

                        revised = _repo.ReviseTransactionAtomic(revising, docNo, _txnDate.Text, _txnDirection.Text,
                            period, party, fund, income ? "Income" : "Expense", cat, amount, _txnQty.Text.Trim(),
                            dollar, rate, _txnDesc.Text.Trim(), "", editReason, true);
                    }

                    UiTheme.ShowSuccess(this,
                        "سند قبلی باطل شد و سند اصلاحی با شماره " + revised.DocNo + " صادر گردید.\n" +
                        "هر دو سند در دفتر و ردّ حسابرسی باقی می‌مانند.");
                    SoftResetTxnForm();
                    LoadTransactions();
                    UpdateFundBalanceLabel();
                    return;
                }

                AccountingRepo.TransactionSaveResult saved;
                try
                {
                    saved = _repo.AddTransactionAtomic(docNo, _txnDate.Text, _txnDirection.Text,
                        period, party, fund, income ? "Income" : "Expense", cat, amount, _txnQty.Text.Trim(),
                        dollar, rate, _txnDesc.Text.Trim(), "", false);
                }
                catch (AccountingDuplicateException dup)
                {
                    // سند مشابه پیدا شد — تصمیم با کاربر است، نه با سیستم.
                    if (!UiTheme.ShowConfirm(this, dup.Message, "سند تکراری"))
                        return;

                    saved = _repo.AddTransactionAtomic(docNo, _txnDate.Text, _txnDirection.Text,
                        period, party, fund, income ? "Income" : "Expense", cat, amount, _txnQty.Text.Trim(),
                        dollar, rate, _txnDesc.Text.Trim(), "", true);
                }

                UiTheme.ShowSuccess(this, "تراکنش با شماره سند " + saved.DocNo + " ثبت شد." +
                    (saved.DocNoReassigned ? "\n(شماره سند به‌دلیل استفاده‌ی هم‌زمان تغییر کرد.)" : ""));
                SoftResetTxnForm();
                LoadTransactions();
                UpdateFundBalanceLabel();
            }
            catch (AccountingRuleException ex)
            {
                // نقض قاعده‌ی حسابداری — پیام آماده و قابل فهم است.
                UiTheme.ShowWarning(this, ex.Message);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ثبت: " + ex.Message);
            }
        }

        // نمایش و چاپ فاکتور/سند تراکنش انتخاب‌شده در گرید
        private void PrintSelectedVoucher()
        {
            if (_gridTxn.CurrentRow == null || !_gridTxn.Columns.Contains("TxnID"))
            { UiTheme.ShowWarning(this, "ابتدا یک تراکنش را از جدول انتخاب کنید."); return; }
            object idv = _gridTxn.CurrentRow.Cells["TxnID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            try
            {
                new AccReports(_repo).PrintVoucher(this, Convert.ToInt32(idv));
            }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت فاکتور: " + ex.Message); }
        }

        // آموزش — «حذف» به «ابطال» تبدیل شد (اصل عدم حذف اسناد مالی):
        // رکورد در پایگاه داده باقی می‌ماند، از تمام مانده‌ها و گزارش‌ها کنار
        // گذاشته می‌شود، و دلیل ابطال به همراه نام کاربر و زمان در ردّ حسابرسی
        // ثبت می‌شود. ثبتِ «دلیل» اجباری است چون بدون آن، ردّ حسابرسی به این
        // پرسشِ حسابرس که «چرا این سند باطل شد؟» پاسخی ندارد.
        private void DeleteSelectedTxn()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Reverse")) { UiTheme.ShowWarning(this, "ابطال سند فقط برای مدیر مجاز است."); return; }
            if (_gridTxn.CurrentRow == null || !_gridTxn.Columns.Contains("TxnID")) { UiTheme.ShowWarning(this, "ابتدا یک تراکنش را انتخاب کنید."); return; }
            object idv = _gridTxn.CurrentRow.Cells["TxnID"].Value;
            if (idv == null || idv == DBNull.Value) return;

            string reason = AskVoidReason("ابطال تراکنش");
            if (reason == null) return;

            try
            {
                _repo.VoidTransaction(Convert.ToInt32(idv), reason);
                UiTheme.ShowSuccess(this, "تراکنش باطل شد و از مانده‌ها کنار گذاشته شد.");
                LoadTransactions();
                UpdateFundBalanceLabel();
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ابطال: " + ex.Message); }
        }

        // ─── گرفتن دلیل ابطال ────────────────────────────────────────────────
        // یک دیالوگ کوچک و ساده، هم‌سبک با بقیه‌ی فرم‌های برنامه.
        // خروجی null یعنی کاربر انصراف داد.
        private string AskVoidReason(string title)
        {
            return AskReasonDialog(title,
                "این سند حذف نمی‌شود؛ «باطل» می‌شود و در ردّ حسابرسی باقی می‌ماند.\nلطفاً دلیل ابطال را بنویسید:",
                "تأیید ابطال");
        }

        // دلیلِ اصلاح — همان دیالوگ، با متنی که دقیقاً می‌گوید چه اتفاقی می‌افتد.
        private string AskReviseReason()
        {
            return AskReasonDialog("اصلاح سند",
                "سند قبلی «باطل» می‌شود و یک سند اصلاحی تازه صادر می‌گردد؛ هر دو در دفتر می‌مانند.\nلطفاً دلیل اصلاح را بنویسید:",
                "تأیید اصلاح");
        }

        private string AskReasonDialog(string title, string infoText, string okText)
        {
            using (var dlg = new Form())
            {
                dlg.Text = title;
                dlg.RightToLeft = RightToLeft.Yes;
                dlg.RightToLeftLayout = true;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false; dlg.MaximizeBox = false;
                dlg.ClientSize = new Size(460, 190);
                dlg.BackColor = UiTheme.Background;
                dlg.Font = UiTheme.Font(UiTheme.SizeBody);

                var info = new Label
                {
                    Text = infoText,
                    Dock = DockStyle.Top, Height = 56, AutoSize = false,
                    TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(14, 8, 14, 4),
                    ForeColor = UiTheme.TextMuted, BackColor = Color.Transparent
                };

                var txt = new TextBox { Multiline = true, RightToLeft = RightToLeft.Yes };
                txt.SetBounds(14, 66, 430, 60);

                var btnOk = UiTheme.CreateButton(okText, "✔", UiTheme.Danger);
                btnOk.SetBounds(14, 138, 140, 34);
                var btnCancel = UiTheme.CreateSecondaryButton("انصراف", "✕");
                btnCancel.SetBounds(164, 138, 110, 34);

                btnOk.Click += delegate
                {
                    if (string.IsNullOrWhiteSpace(txt.Text))
                    {
                        UiTheme.ShowWarning(dlg, "نوشتن دلیل الزامی است.");
                        return;
                    }
                    dlg.DialogResult = DialogResult.OK;
                };
                btnCancel.Click += delegate { dlg.DialogResult = DialogResult.Cancel; };

                dlg.Controls.Add(txt);
                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.Controls.Add(info);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }

        private void LoadTransactions()
        {
            _gridTxn.DataSource = _repo.GetTransactions(null, null);
            if (_gridTxn.Columns.Contains("TxnID")) _gridTxn.Columns["TxnID"].Visible = false;
            FormatAmountColumn(_gridTxn, "مبلغ");
            UpdateTxnSummary();
        }

        // ─── کارت‌های خلاصه (طرح مرجع) ───────────────────────────────────────
        // آموزش — عمداً هیچ کوئری تازه‌ای زده نمی‌شود: همان جدولی که همین الان
        // در گرید نشسته جمع زده می‌شود. پس نه بار اضافه‌ای روی دیتابیس می‌آید
        // و نه امکان دارد عددِ کارت با فهرستِ زیرش ناهم‌خوان شود.
        private void UpdateTxnSummary()
        {
            if (_lblTotalPaid == null || _lblTotalReceived == null) return;

            decimal received = 0m, paid = 0m;
            DataTable t = _gridTxn.DataSource as DataTable;

            if (t != null && t.Columns.Contains("مبلغ") && t.Columns.Contains("نوع"))
            {
                foreach (DataRow r in t.Rows)
                {
                    if (r["مبلغ"] == DBNull.Value) continue;

                    // آموزش — رفع باگ وابستگی به زبان سیستم: کد قبلی مقدار را
                    // اول با Convert.ToString به رشته تبدیل می‌کرد (که از
                    // «فرهنگ جاری» ویندوز پیروی می‌کند) و بعد با
                    // InvariantCulture پارس می‌کرد. روی ویندوزی با تنظیمات
                    // منطقه‌ای فارسی/عربی، جداکننده‌ی اعشار و ارقام فرق دارند و
                    // این تبدیلِ رفت‌وبرگشت مقدار را خراب یا صفر می‌کرد — یعنی
                    // کارت‌های «جمع دریافت/پرداخت» عدد اشتباه نشان می‌دادند.
                    // مقدار در پایگاه داده از قبل عددی است، پس تبدیل به رشته
                    // اصلاً لازم نیست.
                    decimal amount;
                    try { amount = Convert.ToDecimal(r["مبلغ"]); }
                    catch { continue; }

                    string kind = Convert.ToString(r["نوع"]);
                    if (kind == "دریافت") received += amount;
                    else if (kind == "پرداخت") paid += amount;
                }
            }

            _lblTotalReceived.Text = received.ToString("N0");
            _lblTotalPaid.Text = paid.ToString("N0");
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: دوره مالی
        // ═══════════════════════════════════════════════════════════════════
        private DataGridView _gridPeriod;
        private TextBox _pYear, _pTitle;
        private NumericUpDown _pMonthFrom, _pMonthTo, _pOpening;
        private MaskedTextBox _pStart, _pEnd;
        private int _editingPeriodId;

        private TabPage BuildPeriodsTab()
        {
            var page = new TabPage("دوره مالی") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 200, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };

            _pYear = new TextBox { Text = "1404" };
            _pMonthFrom = new NumericUpDown { Minimum = 1, Maximum = 12, Value = 1 };
            _pMonthTo = new NumericUpDown { Minimum = 1, Maximum = 12, Value = 1 };
            _pTitle = new TextBox();
            _pOpening = NewAmountBox(0, true);
            _pStart = NewDateBox(); _pEnd = NewDateBox();

            form.Controls.Add(Field("سال", _pYear, 90));
            form.Controls.Add(Field("از برج", _pMonthFrom, 90));
            form.Controls.Add(Field("تا برج (چندماهه)", _pMonthTo, 130));
            form.Controls.Add(Field("عنوان دوره (اختیاری)", _pTitle, 220));
            form.Controls.Add(DateField("تاریخ شروع", _pStart));
            form.Controls.Add(DateField("تاریخ پایان", _pEnd));
            form.Controls.Add(Field("مانده ابتدای دوره", _pOpening, 160));

            // اگر «تا برج» کمتر از «از برج» شود، خودکار برابرش می‌کنیم (بازه معتبر)
            _pMonthFrom.ValueChanged += delegate { if (_pMonthTo.Value < _pMonthFrom.Value) _pMonthTo.Value = _pMonthFrom.Value; };

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره دوره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 130, 34); btnSave.Click += delegate { SavePeriod(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(152, 6, 90, 34); btnNew.Click += delegate { ClearPeriodForm(); };
            var btnCarry = UiTheme.CreateSecondaryButton("انتقال مانده از دوره قبل", "↺"); btnCarry.SetBounds(250, 6, 200, 34); btnCarry.Click += delegate { CarryForwardOpening(); };
            var btnClose = UiTheme.CreateButton("بستن دوره انتخاب‌شده", "🔒", UiTheme.Warning); btnClose.SetBounds(458, 6, 190, 34); btnClose.Click += delegate { CloseSelectedPeriod(); };
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnCarry); btnBar.Controls.Add(btnClose);

            _gridPeriod = NewGrid();
            _gridPeriod.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridPeriod.Columns.Contains("PeriodID")) return;
                var row = _gridPeriod.Rows[e.RowIndex];
                _editingPeriodId = Convert.ToInt32(row.Cells["PeriodID"].Value);
                _pYear.Text = row.Cells["سال"].Value?.ToString() ?? "";
                _pMonthFrom.Value = ParseMonth(row.Cells["از برج"].Value, 1);
                int monthTo = ParseMonth(row.Cells["تا برج"].Value, 0);
                _pMonthTo.Value = monthTo > 0 ? monthTo : _pMonthFrom.Value;
                _pTitle.Text = row.Cells["عنوان"].Value?.ToString() ?? "";
                _pOpening.Value = (decimal)ParseNum(row.Cells["مانده ابتدای دوره"].Value?.ToString());
            };
            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            gw.Controls.Add(_gridPeriod);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);

            LoadPeriods();
            return page;
        }

        private static int ParseMonth(object value, int fallback)
        {
            int m;
            return value != null && int.TryParse(value.ToString(), out m) ? m : fallback;
        }

        private void ClearPeriodForm()
        {
            _editingPeriodId = 0; _pMonthFrom.Value = 1; _pMonthTo.Value = 1; _pTitle.Text = ""; _pOpening.Value = 0;
        }

        private void SavePeriod()
        {
            int year = (int)ParseNum(_pYear.Text);
            int monthFrom = (int)_pMonthFrom.Value, monthTo = (int)_pMonthTo.Value;
            if (year < 1300) { UiTheme.ShowWarning(this, "سال معتبر وارد کنید."); return; }
            if (!_pStart.MaskCompleted || !_pEnd.MaskCompleted) { UiTheme.ShowWarning(this, "تاریخ شروع و پایان دوره را کامل وارد کنید (سال/ماه/روز)."); return; }

            string title;
            if (!string.IsNullOrWhiteSpace(_pTitle.Text)) title = _pTitle.Text.Trim();
            else if (monthTo != monthFrom) title = "برج " + monthFrom + " تا " + monthTo + " سال " + year;
            else title = "برج " + monthFrom + " سال " + year;

            double opening = (double)_pOpening.Value;
            try
            {
                if (_editingPeriodId == 0)
                    _repo.AddPeriod(year, monthFrom, monthTo, title, _pStart.Text, _pEnd.Text, opening);
                else
                {
                    if (!_repo.IsPeriodOpen(_editingPeriodId)) { UiTheme.ShowWarning(this, "دوره بسته‌شده قابل ویرایش نیست."); return; }
                    _repo.UpdatePeriod(_editingPeriodId, year, monthFrom, monthTo, title, _pStart.Text, _pEnd.Text, opening);
                }
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); return; }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ذخیره دوره: " + ex.Message); return; }

            UiTheme.ShowSuccess(this, "دوره مالی ذخیره شد.");
            ClearPeriodForm(); LoadPeriods();
        }

        private void CarryForwardOpening()
        {
            // مانده پایان آخرین دوره را به‌عنوان مانده ابتدای دوره جدید بگذارد
            DataTable dt = _repo.GetPeriodsForCombo();
            if (dt.Rows.Count == 0) { UiTheme.ShowWarning(this, "دوره‌ای برای انتقال مانده وجود ندارد."); return; }
            int lastId = Convert.ToInt32(dt.Rows[0]["PeriodID"]);
            double closing = _repo.GetPeriodClosing(lastId);
            double clamped = Math.Max((double)_pOpening.Minimum, Math.Min((double)_pOpening.Maximum, closing));
            _pOpening.Value = (decimal)Math.Round(clamped);
            UiTheme.ShowSuccess(this, "مانده پایان دوره «" + dt.Rows[0]["Display"] + "» ( " + closing.ToString("N0") + " ) به‌عنوان مانده ابتدای دوره جدید قرار گرفت.");
        }

        private void CloseSelectedPeriod()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.ClosePeriod"))
            { UiTheme.ShowWarning(this, "بستن دوره مالی فقط برای مدیر سیستم مجاز است."); return; }

            if (_editingPeriodId == 0) { UiTheme.ShowWarning(this, "ابتدا یک دوره را از جدول انتخاب کنید."); return; }
            double closing = _repo.GetPeriodClosing(_editingPeriodId);
            if (!UiTheme.ShowConfirm(this, "با بستن دوره دیگر قابل ویرایش نیست.\nمانده پایان دوره: " + closing.ToString("N0") + " افغانی\nادامه می‌دهید؟", "بستن دوره")) return;
            try { _repo.SetPeriodStatus(_editingPeriodId, "بسته"); }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); return; }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در بستن دوره: " + ex.Message); return; }
            LoadPeriods();
        }

        private void LoadPeriods()
        {
            _gridPeriod.DataSource = _repo.GetPeriods();
            if (_gridPeriod.Columns.Contains("PeriodID")) _gridPeriod.Columns["PeriodID"].Visible = false;
            FormatAmountColumn(_gridPeriod, "مانده ابتدای دوره");
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: صندوق
        // ═══════════════════════════════════════════════════════════════════
        private DataGridView _gridFund;
        private TextBox _fName;
        private NumericUpDown _fOpening;
        private ComboBox _fType;
        private int _editingFundId;

        private TabPage BuildFundsTab()
        {
            var page = new TabPage("صندوق") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };
            _fName = new TextBox(); _fOpening = NewAmountBox(0, true);
            _fType = NewCombo(); _fType.Items.AddRange(new object[] { "نقدی", "بانک", "کردیت", "مرکزی" });
            form.Controls.Add(Field("نام صندوق", _fName, 220));
            form.Controls.Add(Field("نوع", _fType, 140));
            form.Controls.Add(Field("مانده اولیه", _fOpening, 150));

            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.Width = 110; btnSave.Click += delegate { SaveFund(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.Width = 90; btnNew.Click += delegate { _editingFundId = 0; _fName.Text = ""; _fOpening.Value = 0; _fType.SelectedIndex = -1; };
            var btnToggle = UiTheme.CreateSecondaryButton("فعال/غیرفعال", "⊙"); btnToggle.Width = 140; btnToggle.Click += delegate { ToggleFund(); };
            var btnBar = MakeButtonBar(new Control[] { btnSave, btnNew, btnToggle });

            _gridFund = NewGrid();
            _gridFund.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridFund.Columns.Contains("FundID")) return;
                var row = _gridFund.Rows[e.RowIndex];
                _editingFundId = Convert.ToInt32(row.Cells["FundID"].Value);
                _fName.Text = row.Cells["نام صندوق"].Value?.ToString() ?? "";
                _fType.Text = row.Cells["نوع"].Value?.ToString() ?? "";
                _fOpening.Value = (decimal)ParseNum(row.Cells["مانده اولیه"].Value?.ToString());
            };
            page.Controls.Add(MakeGridCard("لیست صندوق‌ها", _gridFund));
            page.Controls.Add(btnBar);
            page.Controls.Add(MakeFormCard("اطلاعات صندوق", form));
            LoadFunds();
            return page;
        }

        private void SaveFund()
        {
            if (string.IsNullOrWhiteSpace(_fName.Text)) { UiTheme.ShowWarning(this, "نام صندوق را وارد کنید."); return; }
            double opening = (double)_fOpening.Value;
            try
            {
                if (_editingFundId == 0) _repo.AddFund(_fName.Text.Trim(), _fType.Text.Trim(), opening);
                else _repo.UpdateFund(_editingFundId, _fName.Text.Trim(), _fType.Text.Trim(), opening);
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); return; }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ذخیره صندوق: " + ex.Message); return; }

            UiTheme.ShowSuccess(this, "صندوق ذخیره شد."); _editingFundId = 0; _fName.Text = ""; _fOpening.Value = 0; _fType.SelectedIndex = -1;
            LoadFunds();
        }

        private void ToggleFund()
        {
            if (_editingFundId == 0) { UiTheme.ShowWarning(this, "ابتدا یک صندوق را انتخاب کنید."); return; }
            _repo.ToggleFund(_editingFundId); LoadFunds();
        }

        private void LoadFunds()
        {
            _gridFund.DataSource = _repo.GetFunds();
            if (_gridFund.Columns.Contains("FundID")) _gridFund.Columns["FundID"].Visible = false;
            FormatAmountColumn(_gridFund, "مانده اولیه");
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: طرف حساب
        // ═══════════════════════════════════════════════════════════════════
        private DataGridView _gridParty;
        private TextBox _paName, _paPhone, _paNote;
        private ComboBox _paType;
        private int _editingPartyId;

        private TabPage BuildPartiesTab()
        {
            var page = new TabPage("طرف حساب") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };
            _paName = new TextBox(); _paPhone = new TextBox(); _paNote = new TextBox();
            _paType = NewCombo(); _paType.Items.AddRange(new object[] { "خیر", "دفتر مرکزی", "ولایت", "ولسوالی", "مرکز", "کارمند", "فروشنده", "شخص" });
            form.Controls.Add(Field("نام طرف حساب", _paName, 220));
            form.Controls.Add(Field("نوع", _paType, 150));
            form.Controls.Add(Field("شماره تماس", _paPhone, 150));
            form.Controls.Add(Field("توضیح", _paNote, 240));

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34); btnSave.Click += delegate { SaveParty(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34); btnNew.Click += delegate { _editingPartyId = 0; _paName.Text = ""; _paPhone.Text = ""; _paNote.Text = ""; _paType.SelectedIndex = -1; };
            var btnToggle = UiTheme.CreateSecondaryButton("فعال/غیرفعال", "⊙"); btnToggle.SetBounds(230, 6, 140, 34); btnToggle.Click += delegate { if (_editingPartyId > 0) { _repo.ToggleParty(_editingPartyId); LoadParties(); } };
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnToggle);

            _gridParty = NewGrid();
            _gridParty.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridParty.Columns.Contains("PartyID")) return;
                var row = _gridParty.Rows[e.RowIndex];
                _editingPartyId = Convert.ToInt32(row.Cells["PartyID"].Value);
                _paName.Text = row.Cells["نام طرف حساب"].Value?.ToString() ?? "";
                _paType.Text = row.Cells["نوع"].Value?.ToString() ?? "";
                _paPhone.Text = row.Cells["تماس"].Value?.ToString() ?? "";
                _paNote.Text = row.Cells["توضیح"].Value?.ToString() ?? "";
            };
            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(_gridParty);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);
            LoadParties();
            return page;
        }

        private void SaveParty()
        {
            if (string.IsNullOrWhiteSpace(_paName.Text)) { UiTheme.ShowWarning(this, "نام طرف حساب را وارد کنید."); return; }
            try
            {
                if (_editingPartyId == 0) _repo.AddParty(_paName.Text.Trim(), _paType.Text.Trim(), _paPhone.Text.Trim(), _paNote.Text.Trim());
                else _repo.UpdateParty(_editingPartyId, _paName.Text.Trim(), _paType.Text.Trim(), _paPhone.Text.Trim(), _paNote.Text.Trim());
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); return; }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ذخیره طرف حساب: " + ex.Message); return; }

            UiTheme.ShowSuccess(this, "طرف حساب ذخیره شد."); _editingPartyId = 0; _paName.Text = ""; _paPhone.Text = ""; _paNote.Text = ""; _paType.SelectedIndex = -1;
            LoadParties();
        }

        private void LoadParties()
        {
            _gridParty.DataSource = _repo.GetParties();
            if (_gridParty.Columns.Contains("PartyID")) _gridParty.Columns["PartyID"].Visible = false;
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: دسته‌بندی درآمد / هزینه (مشترک)
        // ═══════════════════════════════════════════════════════════════════
        private TabPage BuildCategoriesTab(bool income)
        {
            var page = new TabPage(income ? "دسته‌بندی درآمد" : "دسته‌بندی هزینه") { BackColor = UiTheme.Background };
            var grid = NewGrid();
            var txtName = new TextBox();
            int editingId = 0;

            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };
            form.Controls.Add(Field(income ? "عنوان دسته درآمد" : "عنوان دسته هزینه", txtName, 260));

            Action reload = delegate
            {
                grid.DataSource = _repo.GetCategories(income);
                if (grid.Columns.Contains("CatID")) grid.Columns["CatID"].Visible = false;
            };

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34);
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34);
            var btnToggle = UiTheme.CreateSecondaryButton("فعال/غیرفعال", "⊙"); btnToggle.SetBounds(230, 6, 140, 34);
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnToggle);

            btnSave.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(txtName.Text)) { UiTheme.ShowWarning(this, "عنوان را وارد کنید."); return; }
                if (editingId == 0) _repo.AddCategory(income, txtName.Text.Trim());
                else _repo.UpdateCategory(income, editingId, txtName.Text.Trim());
                editingId = 0; txtName.Text = ""; reload();
            };
            btnNew.Click += delegate { editingId = 0; txtName.Text = ""; };
            btnToggle.Click += delegate { if (editingId > 0) { _repo.ToggleCategory(income, editingId); reload(); } };
            grid.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !grid.Columns.Contains("CatID")) return;
                editingId = Convert.ToInt32(grid.Rows[e.RowIndex].Cells["CatID"].Value);
                txtName.Text = grid.Rows[e.RowIndex].Cells["عنوان"].Value?.ToString() ?? "";
            };

            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(grid);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);
            reload();
            return page;
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: شهریه ایتام (مطابق شیت «فرمت جزیی»)
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _stPeriod, _stSadat, _stSize, _stFund;
        private TextBox _stProvince, _stDistrict, _stCenter;
        private NumericUpDown _stFamilyCount, _stOrphanCount, _stAmount;
        private DataGridView _gridStipend;
        private Label _lblStipendTotal;
        private int _editingStipendId;

        private TabPage BuildStipendTab()
        {
            var page = new TabPage("شهریه ایتام") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 190, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };

            _stPeriod = NewCombo(); _stProvince = new TextBox(); _stDistrict = new TextBox(); _stCenter = new TextBox();
            _stSadat = NewCombo(); _stSadat.Items.AddRange(new object[] { "عام", "سادات", "اهل سنت", "غیرحاضران" });
            _stSize = NewCombo(); for (int i = 1; i <= 8; i++) _stSize.Items.Add(i);
            _stFamilyCount = new NumericUpDown { Maximum = 100000 }; _stOrphanCount = new NumericUpDown { Maximum = 100000 }; _stAmount = NewAmountBox();
            // آموزش — رفع باگ حسابداری «مانده صندوق هم‌خوان نبود»: هر پرداخت
            // شهریه باید مشخص کند از کدام صندوق پرداخت شده، وگرنه آن صندوق
            // این خروج پول را در مانده‌اش نمی‌دید.
            _stFund = NewCombo();

            form.Controls.Add(Field("دوره مالی", _stPeriod, 180));
            form.Controls.Add(Field("ولایت", _stProvince, 130));
            form.Controls.Add(Field("ولسوالی", _stDistrict, 130));
            form.Controls.Add(Field("مرکز", _stCenter, 150));
            form.Controls.Add(Field("نوع", _stSadat, 130));
            form.Controls.Add(Field("چند نفره", _stSize, 100));
            form.Controls.Add(Field("تعداد خانوار", _stFamilyCount, 110));
            form.Controls.Add(Field("تعداد یتیم", _stOrphanCount, 110));
            form.Controls.Add(Field("مبلغ شهریه هر خانواده", _stAmount, 160));
            form.Controls.Add(Field("پرداخت از صندوق", _stFund, 170));

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34); btnSave.Click += delegate { SaveStipend(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34); btnNew.Click += delegate { ClearStipendForm(); };
            var btnDelete = UiTheme.CreateButton("حذف", "✕", UiTheme.Danger); btnDelete.SetBounds(230, 6, 100, 34); btnDelete.Click += delegate { DeleteSelectedStipend(); };
            // آموزش — به‌درخواست کاربر: هر رویداد مالی (از جمله ردیف شهریه) باید
            // یک فاکتور/رسید رسمی قابل چاپ داشته باشد.
            var btnVoucherSt = UiTheme.CreateButton("چاپ رسید شهریه", "🧾", UiTheme.Primary); btnVoucherSt.SetBounds(340, 6, 150, 34);
            btnVoucherSt.Click += delegate { PrintSelectedStipendVoucher(); };
            _lblStipendTotal = new Label { AutoSize = false, Font = UiTheme.FontBold(11F), ForeColor = UiTheme.PrimaryDark, TextAlign = ContentAlignment.MiddleRight };
            _lblStipendTotal.SetBounds(500, 6, 500, 34);
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnDelete); btnBar.Controls.Add(btnVoucherSt); btnBar.Controls.Add(_lblStipendTotal);

            _gridStipend = NewGrid();
            _gridStipend.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) PrintSelectedStipendVoucher(); };
            _gridStipend.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridStipend.Columns.Contains("StipendID")) return;
                var row = _gridStipend.Rows[e.RowIndex];
                _editingStipendId = Convert.ToInt32(row.Cells["StipendID"].Value);
                _stProvince.Text = row.Cells["ولایت"].Value?.ToString() ?? "";
                _stDistrict.Text = row.Cells["ولسوالی"].Value?.ToString() ?? "";
                _stCenter.Text = row.Cells["مرکز"].Value?.ToString() ?? "";
                _stSadat.Text = row.Cells["نوع"].Value?.ToString() ?? "";
                _stSize.Text = row.Cells["چند نفره"].Value?.ToString() ?? "";
                _stFamilyCount.Value = (decimal)ParseNum(row.Cells["تعداد خانوار"].Value?.ToString());
                _stOrphanCount.Value = (decimal)ParseNum(row.Cells["تعداد یتیم"].Value?.ToString());
                _stAmount.Value = (decimal)ParseNum(row.Cells["مبلغ شهریه"].Value?.ToString());
                SelectComboValue(_stFund, _gridStipend.Columns.Contains("FundID") ? row.Cells["FundID"].Value : null);
            };

            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(_gridStipend);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);

            BindCombo(_stPeriod, _repo.GetPeriodsForCombo(), "PeriodID", "Display", true);
            BindCombo(_stFund, _repo.GetFundsForCombo(), "FundID", "Display", true);
            _stPeriod.SelectedIndexChanged += delegate { LoadStipends(); };
            LoadStipends();
            return page;
        }

        // پاک‌سازی کامل (دکمه «جدید»)
        private void ClearStipendForm()
        {
            _editingStipendId = 0; _stProvince.Text = ""; _stDistrict.Text = ""; _stCenter.Text = "";
            _stSadat.SelectedIndex = -1; _stSize.SelectedIndex = -1; _stFamilyCount.Value = 0; _stOrphanCount.Value = 0; _stAmount.Value = 0;
            _stFund.SelectedIndex = -1;
        }

        // انتخاب مقدار یک ComboBox متصل به دیتاسورس بر اساس ValueMember (برای
        // بازگرداندن انتخاب صندوق هنگام کلیک روی ردیف گرید جهت ویرایش).
        private static void SelectComboValue(ComboBox cmb, object value)
        {
            if (value == null || value == DBNull.Value) { cmb.SelectedIndex = -1; return; }
            try { cmb.SelectedValue = Convert.ToInt32(value); }
            catch { cmb.SelectedIndex = -1; }
        }

        // پاک‌سازی نرم بعد از ثبت: ولایت/ولسوالی/مرکز/نوع حفظ می‌شوند (چون
        // معمولاً برای ردیف‌های متوالی یک منطقه یکسان‌اند)، فقط چندنفره/تعداد/مبلغ
        // پاک می‌شوند تا کاربر سریع ردیف بعدی همان منطقه را ثبت کند.
        private void SoftResetStipendForm()
        {
            _editingStipendId = 0;
            _stSize.SelectedIndex = -1; _stFamilyCount.Value = 0; _stOrphanCount.Value = 0; _stAmount.Value = 0;
        }

        private void SaveStipend()
        {
            int? period = ComboIntValue(_stPeriod);
            if (period == null) { UiTheme.ShowWarning(this, "دوره مالی را انتخاب کنید. هر ردیف شهریه باید به یک دوره متصل باشد."); _stPeriod.Focus(); return; }
            if (!_repo.IsPeriodOpen(period.Value)) { UiTheme.ShowWarning(this, "این دوره مالی «بسته» است و امکان ثبت شهریه در آن وجود ندارد."); return; }
            if (string.IsNullOrWhiteSpace(_stSadat.Text) || _stSize.SelectedIndex < 0)
            { UiTheme.ShowWarning(this, "نوع و چندنفره را انتخاب کنید."); return; }

            int size = (int)ParseNum(_stSize.Text);
            int familyCount = (int)_stFamilyCount.Value;
            int orphanCount = (int)_stOrphanCount.Value;
            double amount = (double)_stAmount.Value;
            int? fund = ComboIntValue(_stFund);

            try
            {
                if (_editingStipendId == 0)
                    _repo.AddStipend(period, _stProvince.Text.Trim(), _stDistrict.Text.Trim(), _stCenter.Text.Trim(), _stSadat.Text, size, familyCount, orphanCount, amount, fund);
                else
                    _repo.UpdateStipend(_editingStipendId, _stProvince.Text.Trim(), _stDistrict.Text.Trim(), _stCenter.Text.Trim(), _stSadat.Text, size, familyCount, orphanCount, amount, fund);
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); return; }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ذخیره شهریه: " + ex.Message); return; }

            UiTheme.ShowSuccess(this, "شهریه ذخیره شد.");
            SoftResetStipendForm();
            LoadStipends();
        }

        private void DeleteSelectedStipend()
        {
            if (_editingStipendId == 0) { UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید."); return; }

            string reason = AskVoidReason("ابطال ردیف شهریه");
            if (reason == null) return;

            try
            {
                _repo.VoidStipend(_editingStipendId, reason);
                UiTheme.ShowSuccess(this, "ردیف شهریه باطل شد.");
                ClearStipendForm();
                LoadStipends();
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ابطال: " + ex.Message); }
        }

        private void PrintSelectedStipendVoucher()
        {
            if (_gridStipend.CurrentRow == null || !_gridStipend.Columns.Contains("StipendID"))
            { UiTheme.ShowWarning(this, "ابتدا یک ردیف شهریه را از جدول انتخاب کنید."); return; }
            object idv = _gridStipend.CurrentRow.Cells["StipendID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            try { new AccReports(_repo).PrintStipendVoucher(this, Convert.ToInt32(idv)); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت رسید: " + ex.Message); }
        }

        private void LoadStipends()
        {
            int? period = ComboIntValue(_stPeriod);
            _gridStipend.DataSource = _repo.GetStipends(period);
            if (_gridStipend.Columns.Contains("StipendID")) _gridStipend.Columns["StipendID"].Visible = false;
            if (_gridStipend.Columns.Contains("PeriodID")) _gridStipend.Columns["PeriodID"].Visible = false;
            if (_gridStipend.Columns.Contains("FundID")) _gridStipend.Columns["FundID"].Visible = false;
            FormatAmountColumn(_gridStipend, "مبلغ شهریه");
            FormatAmountColumn(_gridStipend, "جمع پرداختی");

            double total = 0;
            var dt = (DataTable)_gridStipend.DataSource;
            foreach (DataRow r in dt.Rows) total += Convert.ToDouble(r["جمع پرداختی"]);
            _lblStipendTotal.Text = string.Format(Lang.T("جمع کل پرداختی شهریه:  {0}  افغانی"), total.ToString("N0"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: حقوق کارکنان
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _saPeriod, _saFund;
        private TextBox _saName, _saPosition, _saNote;
        private NumericUpDown _saAmount;
        private DataGridView _gridSalary;
        private Label _lblSalaryTotal;
        private int _editingSalaryId;

        private TabPage BuildSalaryTab()
        {
            var page = new TabPage("حقوق کارکنان") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 96, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };

            _saPeriod = NewCombo(); _saName = new TextBox(); _saPosition = new TextBox(); _saAmount = NewAmountBox(); _saNote = new TextBox();
            _saFund = NewCombo();
            form.Controls.Add(Field("دوره مالی", _saPeriod, 180));
            form.Controls.Add(Field("نام", _saName, 180));
            form.Controls.Add(Field("سمت", _saPosition, 150));
            form.Controls.Add(Field("مبلغ", _saAmount, 130));
            form.Controls.Add(Field("پرداخت از صندوق", _saFund, 170));
            form.Controls.Add(Field("توضیح", _saNote, 220));

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34); btnSave.Click += delegate { SaveSalary(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34); btnNew.Click += delegate { ClearSalaryForm(); };
            var btnDelete = UiTheme.CreateButton("حذف", "✕", UiTheme.Danger); btnDelete.SetBounds(230, 6, 100, 34); btnDelete.Click += delegate { DeleteSelectedSalary(); };
            var btnVoucherSa = UiTheme.CreateButton("چاپ فیش حقوق", "🧾", UiTheme.Primary); btnVoucherSa.SetBounds(340, 6, 150, 34);
            btnVoucherSa.Click += delegate { PrintSelectedSalaryVoucher(); };
            // «فورم دریافت حقوق ماهانه» — فورمِ رسمیِ Word که کارمند هنگام
            // گرفتن معاش امضاء می‌کند. جدا از «فیش حقوق» چاپیِ موجود است و آن
            // را دست نمی‌زند.
            var btnSalaryReceipt = UiTheme.CreateSecondaryButton("فورم دریافت حقوق", "📄"); btnSalaryReceipt.SetBounds(498, 6, 170, 34);
            btnSalaryReceipt.Click += delegate { ExportSalaryReceiptForm(); };
            _lblSalaryTotal = new Label { AutoSize = false, Font = UiTheme.FontBold(11F), ForeColor = UiTheme.PrimaryDark, TextAlign = ContentAlignment.MiddleRight };
            _lblSalaryTotal.SetBounds(678, 6, 400, 34);
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnDelete); btnBar.Controls.Add(btnVoucherSa); btnBar.Controls.Add(btnSalaryReceipt); btnBar.Controls.Add(_lblSalaryTotal);

            _gridSalary = NewGrid();
            _gridSalary.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) PrintSelectedSalaryVoucher(); };
            _gridSalary.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridSalary.Columns.Contains("SalaryID")) return;
                var row = _gridSalary.Rows[e.RowIndex];
                _editingSalaryId = Convert.ToInt32(row.Cells["SalaryID"].Value);
                _saName.Text = row.Cells["نام"].Value?.ToString() ?? "";
                _saPosition.Text = row.Cells["سمت"].Value?.ToString() ?? "";
                _saAmount.Value = (decimal)ParseNum(row.Cells["مبلغ"].Value?.ToString());
                _saNote.Text = row.Cells["توضیح"].Value?.ToString() ?? "";
                SelectComboValue(_saFund, _gridSalary.Columns.Contains("FundID") ? row.Cells["FundID"].Value : null);
            };

            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(_gridSalary);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);

            BindCombo(_saPeriod, _repo.GetPeriodsForCombo(), "PeriodID", "Display", true);
            BindCombo(_saFund, _repo.GetFundsForCombo(), "FundID", "Display", true);
            _saPeriod.SelectedIndexChanged += delegate { LoadSalaries(); };
            LoadSalaries();
            return page;
        }

        private void ClearSalaryForm() { _editingSalaryId = 0; _saName.Text = ""; _saPosition.Text = ""; _saAmount.Value = 0; _saNote.Text = ""; _saFund.SelectedIndex = -1; }

        private void SaveSalary()
        {
            int? period = ComboIntValue(_saPeriod);
            if (period == null) { UiTheme.ShowWarning(this, "دوره مالی را انتخاب کنید. هر ردیف حقوق باید به یک دوره متصل باشد."); _saPeriod.Focus(); return; }
            if (!_repo.IsPeriodOpen(period.Value)) { UiTheme.ShowWarning(this, "این دوره مالی «بسته» است و امکان ثبت حقوق در آن وجود ندارد."); return; }
            if (string.IsNullOrWhiteSpace(_saName.Text)) { UiTheme.ShowWarning(this, "نام کارمند را وارد کنید."); return; }
            double amount = (double)_saAmount.Value;
            int? fund = ComboIntValue(_saFund);
            try
            {
                if (_editingSalaryId == 0) _repo.AddSalary(period, _saName.Text.Trim(), _saPosition.Text.Trim(), amount, _saNote.Text.Trim(), fund);
                else _repo.UpdateSalary(_editingSalaryId, _saName.Text.Trim(), _saPosition.Text.Trim(), amount, _saNote.Text.Trim(), fund);
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); return; }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ذخیره حقوق: " + ex.Message); return; }

            // دوره مالی حفظ می‌شود؛ فقط فیلدهای هر کارمند پاک می‌شوند.
            UiTheme.ShowSuccess(this, "حقوق ذخیره شد."); ClearSalaryForm(); LoadSalaries();
        }

        private void DeleteSelectedSalary()
        {
            if (_editingSalaryId == 0) { UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید."); return; }

            string reason = AskVoidReason("ابطال ردیف حقوق");
            if (reason == null) return;

            try
            {
                _repo.VoidSalary(_editingSalaryId, reason);
                UiTheme.ShowSuccess(this, "ردیف حقوق باطل شد.");
                ClearSalaryForm(); LoadSalaries();
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ابطال: " + ex.Message); }
        }

        private void PrintSelectedSalaryVoucher()
        {
            if (_gridSalary.CurrentRow == null || !_gridSalary.Columns.Contains("SalaryID"))
            { UiTheme.ShowWarning(this, "ابتدا یک ردیف حقوق را از جدول انتخاب کنید."); return; }
            object idv = _gridSalary.CurrentRow.Cells["SalaryID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            try { new AccReports(_repo).PrintSalaryVoucher(this, Convert.ToInt32(idv)); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت فیش: " + ex.Message); }
        }

        // ─── فورم رسمیِ «دریافت حقوق ماهانه» (Word / PDF) ────────────────────
        // مقادیر از ردیفِ انتخاب‌شدهٔ جدولِ حقوق خوانده می‌شوند. «نام پدر» و
        // «ولایت» در AccSalary وجود ندارند، پس خالی می‌آیند و کاربر خودش
        // پرشان می‌کند — به‌جای حدس زدن.
        private void ExportSalaryReceiptForm()
        {
            if (_gridSalary.CurrentRow == null || !_gridSalary.Columns.Contains("SalaryID"))
            { UiTheme.ShowWarning(this, "ابتدا یک ردیف حقوق را از جدول انتخاب کنید."); return; }

            var row = _gridSalary.CurrentRow;
            string name = CellText(row, "نام");
            string position = CellText(row, "سمت");
            string amount = CellText(row, "مبلغ");

            int? period = ComboIntValue(_saPeriod);
            string periodTitle = period.HasValue ? _repo.GetPeriodTitle(period.Value) : "";

            var fields = new System.Collections.Generic.List<Helpers.FrmDocxForm.FieldDef>
            {
                Helpers.FrmDocxForm.FieldDef.Text("شماره مسلسل", "SerialNo", CellText(row, "SalaryID")),
                Helpers.FrmDocxForm.FieldDef.Text("نام / نام خانوادگی", "EmployeeName", name, true),
                Helpers.FrmDocxForm.FieldDef.Text("نام پدر", "FatherName"),
                Helpers.FrmDocxForm.FieldDef.Text("وظیفه", "Position", position),
                Helpers.FrmDocxForm.FieldDef.Text("ولایت / ولسوالی", "Province"),
                Helpers.FrmDocxForm.FieldDef.Text("بابت برج", "Month", periodTitle),
                Helpers.FrmDocxForm.FieldDef.Text("مبلغ دریافتی", "Amount", amount),
                Helpers.FrmDocxForm.FieldDef.Text("تاریخ", "Date",
                    Helpers.PersianDateHelper.ToPersianDateString(DateTime.Now))
            };

            using (var frm = new Helpers.FrmDocxForm("فورم دریافت حقوق کارمند",
                       Helpers.DocxFormExport.TplSalaryReceipt, fields,
                       "دریافت حقوق - " + name))
            {
                frm.Require("EmployeeName", "Amount");
                frm.ShowDialog(this);
            }
        }

        // ─── سند پرداخت وجه روی قالب رسمی اکسل ───────────────────────────────
        private void ExportSelectedVoucherTemplate()
        {
            if (_gridTxn.CurrentRow == null || !_gridTxn.Columns.Contains("TxnID"))
            { UiTheme.ShowWarning(this, "ابتدا یک تراکنش را از جدول انتخاب کنید."); return; }

            object idv = _gridTxn.CurrentRow.Cells["TxnID"].Value;
            if (idv == null || idv == DBNull.Value) return;

            if (!AccTemplateExport.TemplateExists)
            {
                UiTheme.ShowError(this, "فایل قالب رسمی پیدا نشد:\n" + AccTemplateExport.TemplatePath);
                return;
            }

            using (var sfd = new SaveFileDialog
            {
                Filter = "فایل اکسل|*.xlsx",
                FileName = "سند پرداخت وجه - " + CellText(_gridTxn.CurrentRow, "شماره سند") + ".xlsx"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    new AccReports(_repo).ExportVoucherTemplate(sfd.FileName, Convert.ToInt32(idv));
                    UiTheme.ShowSuccess(this, "سند روی قالب رسمی ذخیره شد:\n" + sfd.FileName);
                }
                catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت سند روی قالب: " + ex.Message); }
            }
        }

        // ─── قرارداد خدمات ترانسپورت (Word / PDF) ────────────────────────────
        // تراکنشِ انتخاب‌شده اختیاری است: اگر ردیفی انتخاب شده باشد، شمارهٔ
        // سند و طرف حساب از آن پیش‌پر می‌شوند و قرارداد به همان تراکنش پیوند
        // می‌خورد؛ اگر نه، کاربر خودش وارد می‌کند.
        private void ShowDriverContractForm()
        {
            int? txnId = null;
            string partyName = "", docNo = "";

            if (_gridTxn.CurrentRow != null && _gridTxn.Columns.Contains("TxnID"))
            {
                object idv = _gridTxn.CurrentRow.Cells["TxnID"].Value;
                if (idv != null && idv != DBNull.Value) txnId = Convert.ToInt32(idv);
                partyName = CellText(_gridTxn.CurrentRow, "طرف حساب");
                docNo = CellText(_gridTxn.CurrentRow, "شماره سند");
            }

            var fields = new System.Collections.Generic.List<Helpers.FrmDocxForm.FieldDef>
            {
                Helpers.FrmDocxForm.FieldDef.Section("قرارداد"),
                Helpers.FrmDocxForm.FieldDef.Text("شماره قرارداد", "ContractNo", docNo),
                Helpers.FrmDocxForm.FieldDef.Text("طرف حساب", "PartyName", partyName),

                Helpers.FrmDocxForm.FieldDef.Section("راننده و موتر"),
                Helpers.FrmDocxForm.FieldDef.Text("نام راننده", "DriverName"),
                Helpers.FrmDocxForm.FieldDef.Text("شماره تماس", "DriverPhone"),
                Helpers.FrmDocxForm.FieldDef.Text("مودل موتر", "CarModel"),
                Helpers.FrmDocxForm.FieldDef.Text("شماره پلیت", "PlateNo"),

                Helpers.FrmDocxForm.FieldDef.Section("شرایط"),
                Helpers.FrmDocxForm.FieldDef.Text("مناطق", "Areas"),
                Helpers.FrmDocxForm.FieldDef.Choice("نوع قرارداد", "FuelType",
                    new[] { "با سوخت", "بدون سوخت" }),
                Helpers.FrmDocxForm.FieldDef.Text("از تاریخ", "FromDate",
                    Helpers.PersianDateHelper.ToPersianDateString(DateTime.Now)),
                Helpers.FrmDocxForm.FieldDef.Text("تا تاریخ", "ToDate"),
                Helpers.FrmDocxForm.FieldDef.Text("دستمزد روزانه (افغانی)", "DailyWage"),
                Helpers.FrmDocxForm.FieldDef.Text("محل خدمات اضافی", "ExtraPlace"),
                Helpers.FrmDocxForm.FieldDef.Text("مبلغ توافق‌شدهٔ اضافی", "ExtraAmount")
            };

            using (var frm = new Helpers.FrmDocxForm("قرارداد خدمات ترانسپورت",
                       Helpers.DocxFormExport.TplDriverContract, fields, "قرارداد ترانسپورت"))
            {
                frm.Require("DriverName", "ContractNo");

                // قرارداد فقط یک‌بار در هر نشست ثبت می‌شود، حتی اگر کاربر هم
                // Word و هم PDF بگیرد.
                bool saved = false;
                frm.OnExported = delegate (string path)
                {
                    if (saved) return;
                    saved = SaveDriverContract(frm, txnId, path);
                };

                frm.ShowDialog(this);
            }
        }

        // ثبتِ قرارداد در AdmDriverContract. اگر ثبت نشد، فایلِ ساخته‌شده در
        // دستِ کاربر است و نباید کل عملیات شکست‌خورده به‌نظر برسد — ولی
        // بی‌صدا هم نمی‌ماند.
        private bool SaveDriverContract(IWin32Window owner, int? txnId, string filePath)
        {
            try
            {
                var db = new CaseManagement.DAL.DatabaseHelper();
                double wage;
                if (!double.TryParse(TokenOf(owner, "DailyWage"), out wage)) wage = 0;

                db.ExecuteNonQuery(@"
INSERT INTO AdmDriverContract
    (ContractNo, DriverName, DriverPhone, CarModel, PlateNo, PartyName, Areas, FuelType,
     FromDate, ToDate, DailyWage, ExtraPlace, ExtraAmount, TxnID, FilePath, CenterID, CreatedBy)
VALUES
    (@no, @dn, @dp, @cm, @pl, @pa, @ar, @ft, @fd, @td, @dw, @ep, @ea, @txn, @fp, @cid, @by)",
                    Prm("@no", TokenOf(owner, "ContractNo")), Prm("@dn", TokenOf(owner, "DriverName")),
                    Prm("@dp", TokenOf(owner, "DriverPhone")), Prm("@cm", TokenOf(owner, "CarModel")),
                    Prm("@pl", TokenOf(owner, "PlateNo")), Prm("@pa", TokenOf(owner, "PartyName")),
                    Prm("@ar", TokenOf(owner, "Areas")), Prm("@ft", TokenOf(owner, "FuelType")),
                    Prm("@fd", TokenOf(owner, "FromDate")), Prm("@td", TokenOf(owner, "ToDate")),
                    Prm("@dw", wage), Prm("@ep", TokenOf(owner, "ExtraPlace")),
                    Prm("@ea", TokenOf(owner, "ExtraAmount")),
                    Prm("@txn", (object)txnId ?? DBNull.Value), Prm("@fp", filePath),
                    Prm("@cid", SecurityContext.CurrentCenterId), Prm("@by", SecurityContext.Username));
                return true;
            }
            catch (Exception ex)
            {
                UiTheme.ShowWarning(this, "قرارداد ساخته شد، اما ثبت آن در دیتابیس انجام نشد:" +
                                          Environment.NewLine + ex.Message);
                return false;
            }
        }

        private static string TokenOf(IWin32Window owner, string token)
        {
            var frm = owner as Helpers.FrmDocxForm;
            return frm == null ? "" : frm.ValueOf(token);
        }

        private static System.Data.SQLite.SQLiteParameter Prm(string name, object value)
        {
            return new System.Data.SQLite.SQLiteParameter(name, value ?? DBNull.Value);
        }

        private static string CellText(DataGridViewRow row, string column)
        {
            if (!row.DataGridView.Columns.Contains(column)) return "";
            object v = row.Cells[column].Value;
            return v == null || v == DBNull.Value ? "" : Convert.ToString(v);
        }

        private void LoadSalaries()
        {
            int? period = ComboIntValue(_saPeriod);
            _gridSalary.DataSource = _repo.GetSalaries(period);
            if (_gridSalary.Columns.Contains("SalaryID")) _gridSalary.Columns["SalaryID"].Visible = false;
            if (_gridSalary.Columns.Contains("PeriodID")) _gridSalary.Columns["PeriodID"].Visible = false;
            if (_gridSalary.Columns.Contains("FundID")) _gridSalary.Columns["FundID"].Visible = false;
            FormatAmountColumn(_gridSalary, "مبلغ");

            double total = 0;
            var dt = (DataTable)_gridSalary.DataSource;
            foreach (DataRow r in dt.Rows) total += Convert.ToDouble(r["مبلغ"]);
            _lblSalaryTotal.Text = string.Format(Lang.T("جمع کل حقوق:  {0}  افغانی"), total.ToString("N0"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: هزینه‌های جاری (مطابق شیت «حساب جاری»)
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _exPeriod, _exCategory, _exFund;
        private TextBox _exDesc, _exQty, _exDocNo;
        private NumericUpDown _exPrice;
        private MaskedTextBox _exDate;
        private DataGridView _gridExpense;
        private Label _lblExpenseTotal;
        private int _editingExpenseId;

        private TabPage BuildExpenseItemsTab()
        {
            var page = new TabPage("هزینه‌های جاری") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 180, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };

            _exPeriod = NewCombo(); _exCategory = NewCombo(); _exDesc = new TextBox(); _exQty = new TextBox(); _exPrice = NewAmountBox(); _exDocNo = new TextBox();
            _exDate = NewDateBox(); _exFund = NewCombo();

            form.Controls.Add(Field("دوره مالی", _exPeriod, 170));
            form.Controls.Add(Field("دسته‌بندی", _exCategory, 180));
            form.Controls.Add(Field("شرح", _exDesc, 320));
            form.Controls.Add(Field("تعداد/مقدار", _exQty, 120));
            form.Controls.Add(Field("قیمت", _exPrice, 120));
            form.Controls.Add(Field("پرداخت از صندوق", _exFund, 170));
            form.Controls.Add(Field("شماره سند", _exDocNo, 110));
            form.Controls.Add(DateField("تاریخ", _exDate));

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34); btnSave.Click += delegate { SaveExpenseItem(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34); btnNew.Click += delegate { ClearExpenseForm(); };
            var btnDelete = UiTheme.CreateButton("حذف", "✕", UiTheme.Danger); btnDelete.SetBounds(230, 6, 100, 34); btnDelete.Click += delegate { DeleteSelectedExpenseItem(); };
            var btnVoucherEx = UiTheme.CreateButton("چاپ سند هزینه", "🧾", UiTheme.Primary); btnVoucherEx.SetBounds(340, 6, 150, 34);
            btnVoucherEx.Click += delegate { PrintSelectedExpenseVoucher(); };
            _lblExpenseTotal = new Label { AutoSize = false, Font = UiTheme.FontBold(11F), ForeColor = UiTheme.PrimaryDark, TextAlign = ContentAlignment.MiddleRight };
            _lblExpenseTotal.SetBounds(500, 6, 500, 34);
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnDelete); btnBar.Controls.Add(btnVoucherEx); btnBar.Controls.Add(_lblExpenseTotal);

            _gridExpense = NewGrid();
            _gridExpense.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) PrintSelectedExpenseVoucher(); };
            _gridExpense.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridExpense.Columns.Contains("ItemID")) return;
                var row = _gridExpense.Rows[e.RowIndex];
                _editingExpenseId = Convert.ToInt32(row.Cells["ItemID"].Value);
                _exCategory.Text = row.Cells["دسته‌بندی"].Value?.ToString() ?? "";
                _exDesc.Text = row.Cells["شرح"].Value?.ToString() ?? "";
                _exQty.Text = row.Cells["تعداد/مقدار"].Value?.ToString() ?? "";
                _exPrice.Value = (decimal)ParseNum(row.Cells["قیمت"].Value?.ToString());
                _exDocNo.Text = row.Cells["شماره سند"].Value?.ToString() ?? "";
                SelectComboValue(_exFund, _gridExpense.Columns.Contains("FundID") ? row.Cells["FundID"].Value : null);
            };

            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(_gridExpense);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);

            BindCombo(_exPeriod, _repo.GetPeriodsForCombo(), "PeriodID", "Display", true);
            BindCombo(_exCategory, _repo.GetCategoriesForCombo(false), "CatID", "Display", true);
            BindCombo(_exFund, _repo.GetFundsForCombo(), "FundID", "Display", true);
            _exPeriod.SelectedIndexChanged += delegate { LoadExpenseItems(); };
            LoadExpenseItems();
            return page;
        }

        // پاک‌سازی کامل (دکمه «جدید»)
        private void ClearExpenseForm()
        {
            _editingExpenseId = 0; _exCategory.SelectedIndex = -1; _exDesc.Text = ""; _exQty.Text = ""; _exPrice.Value = 0; _exDocNo.Text = "";
            _exDate.Text = PersianDateHelper.ToPersianDateString(DateTime.Today);
            _exFund.SelectedIndex = -1;
        }

        // پاک‌سازی نرم بعد از ثبت: دوره/دسته‌بندی/تاریخ/صندوق حفظ می‌شوند، فقط
        // شرح/تعداد/قیمت/شماره سند پاک می‌شوند تا ثبت اقلام متوالی سریع باشد.
        private void SoftResetExpenseForm()
        {
            _editingExpenseId = 0; _exDesc.Text = ""; _exQty.Text = ""; _exPrice.Value = 0; _exDocNo.Text = "";
        }

        private void SaveExpenseItem()
        {
            int? period = ComboIntValue(_exPeriod);
            if (period == null) { UiTheme.ShowWarning(this, "دوره مالی را انتخاب کنید. هر قلم هزینه باید به یک دوره متصل باشد."); _exPeriod.Focus(); return; }
            if (!_repo.IsPeriodOpen(period.Value)) { UiTheme.ShowWarning(this, "این دوره مالی «بسته» است و امکان ثبت هزینه در آن وجود ندارد."); return; }
            if (string.IsNullOrWhiteSpace(_exDesc.Text)) { UiTheme.ShowWarning(this, "شرح هزینه را وارد کنید."); return; }
            if (!_exDate.MaskCompleted) { UiTheme.ShowWarning(this, "تاریخ را کامل وارد کنید (سال/ماه/روز)."); _exDate.Focus(); return; }
            int? cat = ComboIntValue(_exCategory);
            double price = (double)_exPrice.Value;
            int? fund = ComboIntValue(_exFund);

            try
            {
                if (_editingExpenseId == 0)
                    _repo.AddExpenseItem(period, cat, _exCategory.Text, _exDesc.Text.Trim(), _exQty.Text.Trim(), price, _exDocNo.Text.Trim(), _exDate.Text, fund);
                else
                    _repo.UpdateExpenseItem(_editingExpenseId, cat, _exCategory.Text, _exDesc.Text.Trim(), _exQty.Text.Trim(), price, _exDocNo.Text.Trim(), _exDate.Text, fund);
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); return; }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ذخیره هزینه: " + ex.Message); return; }

            UiTheme.ShowSuccess(this, "هزینه ذخیره شد."); SoftResetExpenseForm(); LoadExpenseItems();
        }

        private void DeleteSelectedExpenseItem()
        {
            if (_editingExpenseId == 0) { UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید."); return; }

            string reason = AskVoidReason("ابطال قلم هزینه");
            if (reason == null) return;

            try
            {
                _repo.VoidExpenseItem(_editingExpenseId, reason);
                UiTheme.ShowSuccess(this, "قلم هزینه باطل شد.");
                ClearExpenseForm(); LoadExpenseItems();
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ابطال: " + ex.Message); }
        }

        private void PrintSelectedExpenseVoucher()
        {
            if (_gridExpense.CurrentRow == null || !_gridExpense.Columns.Contains("ItemID"))
            { UiTheme.ShowWarning(this, "ابتدا یک قلم هزینه را از جدول انتخاب کنید."); return; }
            object idv = _gridExpense.CurrentRow.Cells["ItemID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            try { new AccReports(_repo).PrintExpenseVoucher(this, Convert.ToInt32(idv)); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت سند: " + ex.Message); }
        }

        private void LoadExpenseItems()
        {
            int? period = ComboIntValue(_exPeriod);
            _gridExpense.DataSource = _repo.GetExpenseItems(period);
            if (_gridExpense.Columns.Contains("ItemID")) _gridExpense.Columns["ItemID"].Visible = false;
            if (_gridExpense.Columns.Contains("PeriodID")) _gridExpense.Columns["PeriodID"].Visible = false;
            if (_gridExpense.Columns.Contains("FundID")) _gridExpense.Columns["FundID"].Visible = false;
            FormatAmountColumn(_gridExpense, "قیمت");

            double total = 0;
            var dt = (DataTable)_gridExpense.DataSource;
            foreach (DataRow r in dt.Rows) total += Convert.ToDouble(r["قیمت"]);
            _lblExpenseTotal.Text = string.Format(Lang.T("جمع کل هزینه‌های جاری:  {0}  افغانی"), total.ToString("N0"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: گزارش‌ها (۸ گزارش با چاپ/PDF/اکسل)
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _repPeriod, _repFund, _repParty;

        private TabPage BuildReportsTab()
        {
            var page = new TabPage("گزارش‌ها") { BackColor = UiTheme.Background };
            var filterPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };
            _repPeriod = NewCombo(); _repFund = NewCombo(); _repParty = NewCombo();
            filterPanel.Controls.Add(Field("دوره مالی گزارش", _repPeriod, 200));
            filterPanel.Controls.Add(Field("صندوق (برای دفتر صندوق)", _repFund, 200));
            filterPanel.Controls.Add(Field("طرف حساب (برای دفتر طرف حساب)", _repParty, 220));

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(16), AutoScroll = true };

            AddReportButton(flow, "۱) صورت حساب کلی", delegate { RunReport(1); });
            AddReportButton(flow, "۲) صورت حساب جزیی شهریه", delegate { RunReport(2); });
            AddReportButton(flow, "۳) صورت حساب هزینه‌ها", delegate { RunReport(3); });
            AddReportButton(flow, "۴) صورت حساب حقوق", delegate { RunReport(4); });
            AddReportButton(flow, "۵) صورت حساب دریافت بودجه", delegate { RunReport(5); });
            AddReportButton(flow, "۶) دفتر صندوق", delegate { RunReport(6); });
            AddReportButton(flow, "۷) دفتر طرف حساب", delegate { RunReport(7); });
            AddReportButton(flow, "۸) ریز تراکنش‌های دوره", delegate { RunReport(8); });

            page.Controls.Add(flow);
            page.Controls.Add(filterPanel);

            BindCombo(_repPeriod, _repo.GetPeriodsForCombo(), "PeriodID", "Display", true);
            BindCombo(_repFund, _repo.GetFundsForCombo(), "FundID", "Display", true);
            BindCombo(_repParty, _repo.GetPartiesForCombo(), "PartyID", "Display", true);
            return page;
        }

        private void AddReportButton(FlowLayoutPanel flow, string title, EventHandler onClick)
        {
            var card = new Panel { Width = 260, Height = 140, Margin = new Padding(10), BackColor = UiTheme.CardBack, BorderStyle = BorderStyle.FixedSingle };
            var lbl = new Label { Text = title, AutoSize = false, Dock = DockStyle.Top, Height = 44, TextAlign = ContentAlignment.MiddleCenter, Font = UiTheme.FontBold(10.5F), ForeColor = UiTheme.TextDark };
            var btnPrint = UiTheme.CreateButton("پیش‌نمایش/چاپ/PDF", "🖨", UiTheme.Primary); btnPrint.SetBounds(10, 50, 240, 26); btnPrint.Click += onClick;
            var btnExcel = UiTheme.CreateSecondaryButton("خروجی اکسل", "📊"); btnExcel.SetBounds(10, 80, 240, 26);
            btnExcel.Tag = title;
            btnExcel.Click += delegate { RunReportExcel(title); };
            var btnTpl = UiTheme.CreateSecondaryButton("خروجی روی قالب رسمی", "📄"); btnTpl.SetBounds(10, 110, 240, 26);
            btnTpl.Tag = title;
            btnTpl.Click += delegate { RunReportTemplate(title); };
            card.Controls.Add(lbl); card.Controls.Add(btnPrint); card.Controls.Add(btnExcel); card.Controls.Add(btnTpl);
            flow.Controls.Add(card);
        }

        private int? SelectedReportPeriod { get { return ComboIntValue(_repPeriod); } }
        private int? SelectedReportFund { get { return ComboIntValue(_repFund); } }
        private int? SelectedReportParty { get { return ComboIntValue(_repParty); } }

        private void RunReport(int reportNo)
        {
            try
            {
                var reports = new AccReports(_repo);
                switch (reportNo)
                {
                    case 1: reports.PrintGeneralStatement(this, SelectedReportPeriod); break;
                    case 2: reports.PrintDetailedStatement(this, SelectedReportPeriod); break;
                    case 3: reports.PrintExpenseStatement(this, SelectedReportPeriod); break;
                    case 4: reports.PrintSalaryStatement(this, SelectedReportPeriod); break;
                    case 5: reports.PrintBudgetReceiptStatement(this, SelectedReportPeriod); break;
                    case 6:
                        if (SelectedReportFund == null) { UiTheme.ShowWarning(this, "ابتدا صندوق را انتخاب کنید."); return; }
                        reports.PrintFundLedger(this, SelectedReportFund.Value, SelectedReportPeriod); break;
                    case 7:
                        if (SelectedReportParty == null) { UiTheme.ShowWarning(this, "ابتدا طرف حساب را انتخاب کنید."); return; }
                        reports.PrintPartyLedger(this, SelectedReportParty.Value); break;
                    case 8:
                        if (SelectedReportPeriod == null) { UiTheme.ShowWarning(this, "ابتدا دوره مالی را انتخاب کنید."); return; }
                        reports.PrintPeriodDetail(this, SelectedReportPeriod.Value); break;
                }
            }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در تولید گزارش: " + ex.Message); }
        }

        // خروجی روی «قالب رسمی» (Templates\FinancialForms.xlsx).
        // فعلاً فقط گزارش ۱ قالب دارد؛ بقیه به‌تدریج اضافه می‌شوند.
        private void RunReportTemplate(string title)
        {
            // قالب رسمی فقط برای گزارش‌هایی وجود دارد که شیت متناظر دارند:
            // ۱ صورت حساب کلی · ۲ تفکیک شهریه و هزینه ها · ۴ حقوق.
            // گزارش‌های ۳ و ۵ تا ۸ در فایل قالب شیتی ندارند.
            bool hasTemplate = title.StartsWith("۱") || title.StartsWith("۲") || title.StartsWith("۴");
            if (!hasTemplate)
            {
                UiTheme.ShowWarning(this, "برای این گزارش هنوز قالب رسمی تعریف نشده است.\nفعلاً «خروجی اکسل» را استفاده کنید.");
                return;
            }
            if (!AccTemplateExport.TemplateExists)
            {
                UiTheme.ShowError(this, "فایل قالب رسمی پیدا نشد:\n" + AccTemplateExport.TemplatePath);
                return;
            }
            using (var sfd = new SaveFileDialog { Filter = "فایل اکسل|*.xlsx", FileName = title.Replace("/", "-") + " - قالب رسمی.xlsx" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var reports = new AccReports(_repo);
                    if (title.StartsWith("۲")) reports.ExportSplitStatementTemplate(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۴")) reports.ExportSalaryStatementTemplate(sfd.FileName, SelectedReportPeriod);
                    else reports.ExportGeneralStatementTemplate(sfd.FileName, SelectedReportPeriod);
                    UiTheme.ShowSuccess(this, "فایل اکسل روی قالب رسمی ذخیره شد:\n" + sfd.FileName);
                }
                catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت اکسل روی قالب: " + ex.Message); }
            }
        }

        private void RunReportExcel(string title)
        {
            using (var sfd = new SaveFileDialog { Filter = "فایل اکسل|*.xlsx", FileName = title.Replace("/", "-") + ".xlsx" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var reports = new AccReports(_repo);
                    if (title.StartsWith("۱")) reports.ExportGeneralStatementExcel(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۲")) reports.ExportDetailedStatementExcel(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۳")) reports.ExportExpenseStatementExcel(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۴")) reports.ExportSalaryStatementExcel(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۵")) reports.ExportBudgetReceiptExcel(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۶"))
                    {
                        if (SelectedReportFund == null) { UiTheme.ShowWarning(this, "ابتدا صندوق را انتخاب کنید."); return; }
                        reports.ExportFundLedgerExcel(sfd.FileName, SelectedReportFund.Value, SelectedReportPeriod);
                    }
                    else if (title.StartsWith("۷"))
                    {
                        if (SelectedReportParty == null) { UiTheme.ShowWarning(this, "ابتدا طرف حساب را انتخاب کنید."); return; }
                        reports.ExportPartyLedgerExcel(sfd.FileName, SelectedReportParty.Value);
                    }
                    else if (title.StartsWith("۸"))
                    {
                        if (SelectedReportPeriod == null) { UiTheme.ShowWarning(this, "ابتدا دوره مالی را انتخاب کنید."); return; }
                        reports.ExportPeriodDetailExcel(sfd.FileName, SelectedReportPeriod.Value);
                    }
                    UiTheme.ShowSuccess(this, "فایل اکسل ذخیره شد:\n" + sfd.FileName);
                }
                catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت اکسل: " + ex.Message); }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: بررسی صحت حسابداری (تأیید خودکار یکپارچگی)
        // ═══════════════════════════════════════════════════════════════════
        // آموزش — این تب کاملاً «فقط‌خواندنی» است: هیچ رکوردی را اصلاح نمی‌کند.
        // کارش این است که معادله‌ی حسابداری، تطبیق گزارش با دفتر، و سناریوهای
        // شناخته‌شده‌ی خطا را یک‌جا بررسی و فهرست کند تا حسابدار پیش از ارائه‌ی
        // گزارش رسمی بداند داده‌هایش سالم است یا نه.
        private DataGridView _gridIntegrity;
        private Label _lblIntegritySummary;

        private TabPage BuildIntegrityTab()
        {
            var page = new TabPage("بررسی صحت") { BackColor = UiTheme.Background };

            var info = new Label
            {
                Text = "این بخش داده‌های حسابداری را بررسی می‌کند و مغایرت‌ها را فهرست می‌کند. " +
                       "هیچ رکوردی به‌صورت خودکار تغییر یا اصلاح نمی‌شود — تصمیم اصلاح با شماست.",
                Dock = DockStyle.Top, Height = 44, AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMuted, Padding = new Padding(14, 8, 14, 4)
            };

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.CardBack };

            var btnRun = UiTheme.CreateButton("اجرای بررسی صحت", "🛡", UiTheme.Primary);
            btnRun.SetBounds(14, 10, 190, 36);
            btnRun.Click += delegate { RunIntegrityCheck(); };

            var btnExport = UiTheme.CreateSecondaryButton("خروجی اکسل", "📊");
            btnExport.SetBounds(214, 10, 150, 36);
            btnExport.Click += delegate { ExportIntegrityReport(); };

            // ابزار اصلاح داده‌های تاریخی — جدا از این بررسی. بررسی فقط گزارش
            // می‌دهد؛ اصلاح در آن ابزار و فقط با تأیید تک‌به‌تک انجام می‌شود.
            var btnRepair = UiTheme.CreateSecondaryButton("ابزار اصلاح داده‌های تاریخی", "🛠");
            btnRepair.SetBounds(374, 10, 230, 36);
            btnRepair.Click += delegate { OpenRepairTool(); };

            _lblIntegritySummary = new Label
            {
                AutoSize = false, Font = UiTheme.FontBold(11F), ForeColor = UiTheme.PrimaryDark,
                TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
            };
            _lblIntegritySummary.SetBounds(614, 10, 520, 36);

            btnBar.Controls.Add(btnRun);
            btnBar.Controls.Add(btnExport);
            btnBar.Controls.Add(btnRepair);
            btnBar.Controls.Add(_lblIntegritySummary);

            _gridIntegrity = NewGrid();
            // رنگ‌آمیزی بر اساس شدت، تا موارد بحرانی در یک نگاه دیده شوند.
            _gridIntegrity.RowPrePaint += delegate (object s, DataGridViewRowPrePaintEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridIntegrity.Columns.Contains("شدت")) return;
                object sev = _gridIntegrity.Rows[e.RowIndex].Cells["شدت"].Value;
                if (sev == null) return;

                var row = _gridIntegrity.Rows[e.RowIndex];
                if (sev.ToString() == AccIntegrity.SeverityCritical)
                {
                    row.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FEF2F2");
                    row.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#B91C1C");
                }
                else if (sev.ToString() == AccIntegrity.SeverityWarning)
                {
                    row.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FFFBEB");
                    row.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#B45309");
                }
            };

            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            gw.Controls.Add(_gridIntegrity);

            page.Controls.Add(gw);
            page.Controls.Add(btnBar);
            page.Controls.Add(info);
            return page;
        }

        // جدول نتیجه‌ی بررسی — از همان DataTable برای نمایش و خروجی اکسل
        // استفاده می‌شود تا عددِ نمایش‌داده‌شده و عددِ خروجی هرگز فرق نکنند.
        private DataTable BuildIntegrityTable(System.Collections.Generic.List<AccIntegrity.Issue> issues)
        {
            var dt = new DataTable("IntegrityIssues");
            dt.Columns.Add("شدت", typeof(string));
            dt.Columns.Add("دسته", typeof(string));
            dt.Columns.Add("شرح مغایرت", typeof(string));
            dt.Columns.Add("جدول", typeof(string));
            dt.Columns.Add("شناسه", typeof(int));
            dt.Columns.Add("مبلغ مرتبط", typeof(double));

            foreach (var i in issues)
                dt.Rows.Add(i.Severity, i.Category, i.Description, i.Entity, i.EntityId, i.Amount);

            return dt;
        }

        // ابزار اصلاح داده‌های تاریخی — فقط برای مدیر کل، چون رکوردهای معیوب
        // ممکن است به هر مرکزی تعلق داشته باشند (یا اصلاً مرکز نداشته باشند).
        private void OpenRepairTool()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Repair"))
            { UiTheme.ShowWarning(this, "اصلاح داده‌های تاریخی حسابداری فقط برای مدیر کل مجاز است."); return; }

            using (var frm = new FrmAccountingRepair(_repo))
                frm.ShowDialog(this);

            // ممکن است داده اصلاح شده باشد؛ بررسی صحت را تازه می‌کنیم.
            RunIntegrityCheck();
        }

        private void RunIntegrityCheck()
        {
            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                var issues = new AccIntegrity(_repo).RunAllChecks();
                _gridIntegrity.DataSource = BuildIntegrityTable(issues);
                FormatAmountColumn(_gridIntegrity, "مبلغ مرتبط");

                if (_gridIntegrity.Columns.Contains("شرح مغایرت"))
                    _gridIntegrity.Columns["شرح مغایرت"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                int critical = 0, warning = 0;
                foreach (var i in issues)
                {
                    if (i.Severity == AccIntegrity.SeverityCritical) critical++;
                    else if (i.Severity == AccIntegrity.SeverityWarning) warning++;
                }

                if (issues.Count == 0)
                {
                    _lblIntegritySummary.ForeColor = UiTheme.Success;
                    _lblIntegritySummary.Text = "✔ هیچ مغایرتی یافت نشد — داده‌های حسابداری سالم است.";
                }
                else
                {
                    _lblIntegritySummary.ForeColor = critical > 0 ? UiTheme.Danger : UiTheme.Warning;
                    _lblIntegritySummary.Text = "یافت شد:  " + critical + " مورد بحرانی،  " + warning + " مورد هشدار.";
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در بررسی صحت: " + ex.Message);
            }
            finally { Cursor = previous; }
        }

        private void ExportIntegrityReport()
        {
            var dt = _gridIntegrity.DataSource as DataTable;
            if (dt == null || dt.Rows.Count == 0)
            { UiTheme.ShowWarning(this, "ابتدا بررسی صحت را اجرا کنید."); return; }

            using (var sfd = new SaveFileDialog
            {
                Filter = "فایل اکسل|*.xlsx",
                FileName = "گزارش بررسی صحت حسابداری.xlsx"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (var wb = new ClosedXML.Excel.XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("بررسی صحت");
                        ws.RightToLeft = true;

                        for (int c = 0; c < dt.Columns.Count; c++)
                        {
                            ws.Cell(1, c + 1).Value = dt.Columns[c].ColumnName;
                            ws.Cell(1, c + 1).Style.Font.Bold = true;
                            ws.Cell(1, c + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2C5A85");
                            ws.Cell(1, c + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                        }

                        for (int r = 0; r < dt.Rows.Count; r++)
                            for (int c = 0; c < dt.Columns.Count; c++)
                                ws.Cell(r + 2, c + 1).Value = Convert.ToString(dt.Rows[r][c]);

                        ws.Columns().AdjustToContents();
                        wb.SaveAs(sfd.FileName);
                    }
                    UiTheme.ShowSuccess(this, "گزارش بررسی صحت ذخیره شد:\n" + sfd.FileName);
                }
                catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت اکسل: " + ex.Message); }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: تنظیمات گزارش (سربرگ/پاورقی/امضاها/مهر)
        // ═══════════════════════════════════════════════════════════════════
        private TextBox _setOrgName, _setHeader, _setFooter;
        private TextBox _setLogoPath, _setStampPath, _setAccSign, _setMgrSign, _setPrepSign;

        private TabPage BuildSettingsTab()
        {
            var page = new TabPage("تنظیمات گزارش") { BackColor = UiTheme.Background };
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, Padding = new Padding(14) };

            _setOrgName = new TextBox(); _setHeader = new TextBox(); _setFooter = new TextBox();
            _setLogoPath = new TextBox { ReadOnly = true }; _setStampPath = new TextBox { ReadOnly = true };
            _setAccSign = new TextBox { ReadOnly = true }; _setMgrSign = new TextBox { ReadOnly = true }; _setPrepSign = new TextBox { ReadOnly = true };

            panel.Controls.Add(Field("نام مؤسسه", _setOrgName, 400));
            panel.Controls.Add(Field("متن سربرگ", _setHeader, 400));
            panel.Controls.Add(Field("متن پاورقی", _setFooter, 400));
            panel.Controls.Add(BuildImagePicker("مسیر لوگو", _setLogoPath));
            panel.Controls.Add(BuildImagePicker("مسیر تصویر مهر", _setStampPath));
            panel.Controls.Add(BuildImagePicker("امضای حسابدار", _setAccSign));
            panel.Controls.Add(BuildImagePicker("امضای مسئول", _setMgrSign));
            panel.Controls.Add(BuildImagePicker("امضای تهیه‌کننده", _setPrepSign));

            var btnSave = UiTheme.CreateButton("ذخیره تنظیمات", "✔", UiTheme.Success);
            btnSave.SetBounds(0, 0, 180, 36);
            btnSave.Margin = new Padding(6, 20, 6, 4);
            btnSave.Click += delegate { SaveAccSettings(); };
            panel.Controls.Add(btnSave);

            page.Controls.Add(panel);
            LoadAccSettings();
            return page;
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: بکاپ/بازیابی مستقل حسابداری
        // آموزش — به‌درخواست جدی کاربر: بکاپ اصلی برنامه اصلاً جداول حسابداری
        // را نمی‌گرفت. این تب کاملاً مستقل است تا بتوان فقط داده‌های مالی را
        // (دوره/صندوق/طرف‌حساب/دسته‌بندی/تراکنش/شهریه/حقوق/هزینه/تنظیمات) بدون
        // لمس پرونده‌ها/کاربران پشتیبان گرفت یا بازیابی کرد.
        // ═══════════════════════════════════════════════════════════════════
        private TextBox _accBackupOutput;

        private TabPage BuildAccBackupTab()
        {
            var page = new TabPage("بکاپ حسابداری") { BackColor = UiTheme.Background };

            var info = new Label
            {
                Text = "این بخش فقط داده‌های حسابداری (دوره مالی، صندوق، طرف حساب، دسته‌بندی‌ها، تراکنش‌ها، شهریه، حقوق، هزینه‌های جاری و تنظیمات گزارش) را بکاپ/بازیابی می‌کند — کاملاً مستقل از بکاپ کلی نرم‌افزار.",
                Dock = DockStyle.Top, Height = 50, AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMuted, Padding = new Padding(14, 8, 14, 4)
            };

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.CardBack };
            var btnExport = UiTheme.CreateButton("گرفتن بکاپ حسابداری", "⇩", UiTheme.Success);
            btnExport.SetBounds(14, 10, 200, 36);
            btnExport.Click += delegate { ExportAccountingBackup(); };

            var btnImport = UiTheme.CreateButton("بازیابی بکاپ حسابداری", "⇧", UiTheme.Warning);
            btnImport.SetBounds(224, 10, 210, 36);
            btnImport.Click += delegate { ImportAccountingBackup(); };

            // آموزش — نسخهٔ ۱٫۰: مسیرِ جدا برای بازیابیِ بکاپ‌های حسابداریِ
            // ساخته‌شده پیش از این ارتقا (پوشهٔ رمزنگاری‌نشده).
            var btnImportLegacy = UiTheme.CreateSecondaryButton("بازیابی بکاپ قدیمی", "⇧");
            btnImportLegacy.SetBounds(444, 10, 180, 36);
            btnImportLegacy.Click += delegate { ImportAccountingBackupLegacy(); };

            btnBar.Controls.Add(btnExport);
            btnBar.Controls.Add(btnImport);
            btnBar.Controls.Add(btnImportLegacy);

            _accBackupOutput = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9.5F), RightToLeft = RightToLeft.No
            };

            page.Controls.Add(_accBackupOutput);
            page.Controls.Add(btnBar);
            page.Controls.Add(info);
            return page;
        }

        private void AppendAccBackupOutput(string line)
        {
            _accBackupOutput.AppendText((_accBackupOutput.TextLength > 0 ? Environment.NewLine : "") +
                "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line);
        }

        private void ExportAccountingBackup()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Backup"))
            { UiTheme.ShowWarning(this, "بکاپ‌گیری حسابداری فقط برای مدیر کل مجاز است."); return; }

            using (var fbd = new FolderBrowserDialog { Description = "پوشه‌ای برای ذخیره‌ی بکاپ حسابداری انتخاب کنید" })
            {
                if (fbd.ShowDialog(this) != DialogResult.OK) return;

                string password;
                if (!FrmPasswordPrompt.TryPrompt(this, "رمزِ بکاپ حسابداری",
                        "این رمز برای رمزنگاریِ بکاپ استفاده می‌شود. بدونِ آن، بازیابی ممکن نیست.",
                        requireConfirmation: true, password: out password))
                    return;

                try
                {
                    string path = AccountingBackupHelper.ExportEncryptedBackup(fbd.SelectedPath, password);
                    AppendAccBackupOutput("بکاپ رمزنگاری‌شدهٔ حسابداری با موفقیت ساخته شد: " + path);
                    UiTheme.ShowSuccess(this, "بکاپ حسابداری ذخیره شد:" + Environment.NewLine + path);
                }
                catch (Exception ex)
                {
                    AppendAccBackupOutput("خطا در بکاپ‌گیری: " + ex.Message);
                    UiTheme.ShowError(this, "خطا در بکاپ‌گیری حسابداری: " + ex.Message);
                }
            }
        }

        private void ImportAccountingBackup()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Backup"))
            { UiTheme.ShowWarning(this, "بازیابی حسابداری فقط برای مدیر کل مجاز است."); return; }

            using (var ofd = new OpenFileDialog
            {
                Title = "فایل بکاپ رمزنگاری‌شدهٔ حسابداری را انتخاب کنید",
                Filter = "بکاپ رمزنگاری‌شده (*" + BackupEncryption.EncryptedExtension + ")|*" + BackupEncryption.EncryptedExtension
            })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;

                if (!UiTheme.ShowConfirm(this,
                    "با بازیابی، تمام داده‌های فعلیِ حسابداری (همه‌ی مراکز) با محتوای این بکاپ جایگزین می‌شود.\n" +
                    "قبل از شروع، یک بکاپ ایمنیِ خودکار از وضعیت فعلی گرفته می‌شود.\n" +
                    "آیا مطمئن هستید؟", "تأیید بازیابی حسابداری"))
                    return;

                string password;
                if (!FrmPasswordPrompt.TryPrompt(this, "رمزِ بکاپ حسابداری", "رمزِ عبورِ همین فایلِ بکاپ را وارد کنید.",
                        requireConfirmation: false, password: out password))
                    return;

                try
                {
                    AccountingBackupHelper.ImportEncryptedBackup(ofd.FileName, password);
                    AppendAccBackupOutput("بازیابی حسابداری با موفقیت انجام شد از: " + ofd.FileName);
                    UiTheme.ShowSuccess(this, "بازیابی حسابداری با موفقیت انجام شد.\nبرای بارگذاری اطلاعات تازه، پنجره حسابداری را ببندید و دوباره باز کنید.");
                }
                catch (BackupEncryption.IntegrityException ex)
                {
                    AppendAccBackupOutput("رمز اشتباه یا فایل خراب/دستکاری‌شده: " + ex.Message);
                    UiTheme.ShowWarning(this, "رمز عبور اشتباه است یا فایلِ بکاپ خراب/دستکاری‌شده — هیچ داده‌ای تغییر نکرد.");
                }
                catch (Exception ex)
                {
                    AppendAccBackupOutput("خطا در بازیابی: " + ex.Message);
                    UiTheme.ShowError(this, "خطا در بازیابی حسابداری: " + ex.Message);
                }
            }
        }

        // آموزش — نسخهٔ ۱٫۰: بازیابیِ بکاپ‌های حسابداریِ ساخته‌شده پیش از این
        // ارتقا (پوشهٔ رمزنگاری‌نشده) — عیناً همان کدِ قدیمی، بدونِ تغییر.
        private void ImportAccountingBackupLegacy()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Backup"))
            { UiTheme.ShowWarning(this, "بازیابی حسابداری فقط برای مدیر کل مجاز است."); return; }

            using (var fbd = new FolderBrowserDialog { Description = "پوشه‌ی بکاپ قدیمیِ (رمزنگاری‌نشدهٔ) حسابداری را انتخاب کنید" })
            {
                if (fbd.ShowDialog(this) != DialogResult.OK) return;

                if (!UiTheme.ShowConfirm(this,
                    "با بازیابی، تمام داده‌های فعلیِ حسابداری (همه‌ی مراکز) با محتوای این بکاپ جایگزین می‌شود.\n" +
                    "قبل از شروع، یک بکاپ ایمنیِ خودکار از وضعیت فعلی گرفته می‌شود.\n" +
                    "آیا مطمئن هستید؟", "تأیید بازیابی حسابداری"))
                    return;

                try
                {
                    AccountingBackupHelper.ImportBackup(fbd.SelectedPath);
                    AppendAccBackupOutput("بازیابی حسابداری (قدیمی) با موفقیت انجام شد از: " + fbd.SelectedPath);
                    UiTheme.ShowSuccess(this, "بازیابی حسابداری با موفقیت انجام شد.\nبرای بارگذاری اطلاعات تازه، پنجره حسابداری را ببندید و دوباره باز کنید.");
                }
                catch (Exception ex)
                {
                    AppendAccBackupOutput("خطا در بازیابی: " + ex.Message);
                    UiTheme.ShowError(this, "خطا در بازیابی حسابداری: " + ex.Message);
                }
            }
        }

        private Panel BuildImagePicker(string label, TextBox pathBox)
        {
            var p = new Panel { Width = 460, Height = 58, Margin = new Padding(6, 4, 6, 4) };
            var l = new Label { Text = label, AutoSize = false, Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark };
            pathBox.Width = 330; pathBox.Height = 28; pathBox.Location = new Point(90, 24);
            var btn = UiTheme.CreateSecondaryButton("انتخاب...", "🖼"); btn.SetBounds(0, 22, 80, 30);
            btn.Click += delegate
            {
                using (var ofd = new OpenFileDialog { Filter = "تصویر|*.jpg;*.jpeg;*.png" })
                    if (ofd.ShowDialog(this) == DialogResult.OK) pathBox.Text = ofd.FileName;
            };
            p.Controls.Add(l); p.Controls.Add(pathBox); p.Controls.Add(btn);
            return p;
        }

        private void LoadAccSettings()
        {
            _setOrgName.Text = _repo.GetSetting("OrgName");
            _setHeader.Text = _repo.GetSetting("HeaderText");
            _setFooter.Text = _repo.GetSetting("FooterText");
            _setLogoPath.Text = _repo.GetSetting("LogoPath");
            _setStampPath.Text = _repo.GetSetting("StampPath");
            _setAccSign.Text = _repo.GetSetting("AccountantSignature");
            _setMgrSign.Text = _repo.GetSetting("ManagerSignature");
            _setPrepSign.Text = _repo.GetSetting("PreparerSignature");
        }

        private void SaveAccSettings()
        {
            _repo.SetSetting("OrgName", _setOrgName.Text.Trim());
            _repo.SetSetting("HeaderText", _setHeader.Text.Trim());
            _repo.SetSetting("FooterText", _setFooter.Text.Trim());
            _repo.SetSetting("LogoPath", _setLogoPath.Text.Trim());
            _repo.SetSetting("StampPath", _setStampPath.Text.Trim());
            _repo.SetSetting("AccountantSignature", _setAccSign.Text.Trim());
            _repo.SetSetting("ManagerSignature", _setMgrSign.Text.Trim());
            _repo.SetSetting("PreparerSignature", _setPrepSign.Text.Trim());
            UiTheme.ShowSuccess(this, "تنظیمات گزارش ذخیره شد.");
        }

        // قالب‌بندی ستون مبلغ به‌صورت سه‌رقمی
        private void FormatAmountColumn(DataGridView grid, string col)
        {
            if (grid.Columns.Contains(col))
            {
                grid.Columns[col].DefaultCellStyle.Format = "N0";
                grid.Columns[col].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }
        }
    }
}

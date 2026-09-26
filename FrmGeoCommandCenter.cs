using CaseManagement.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;

namespace CaseManagement
{
    // ═══════════════════════════════════════════════════════════════════════
    // FrmGeoCommandCenter — «مرکز فرماندهی آماری و تحلیلی».
    //
    // این فرم عمداً هیچ SQL ندارد: همهٔ اعداد از GeoAnalyticsService می‌آیند،
    // همهٔ جمله‌های تحلیلی از GeoInsightEngine و همهٔ هندسه از
    // AfghanMapGeometry. وظیفهٔ فرم فقط چیدمان، رویداد و خروجی است.
    //
    // چرخهٔ کار:
    //   فیلترها → RefreshAsync (روی نخِ پس‌زمینه) → نشاندنِ نتیجه روی UI
    //   کلیک روی نقشه → انتخاب منطقه و به‌روزرسانیِ پنلِ راست
    //   راست‌کلیک روی ولایت → ورود به سطحِ ولسوالی
    //   دوکلیک (نقشه یا هر ردیفِ جدول) → بازکردنِ FrmCase با همان فیلتر
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class FrmGeoCommandCenter : Form
    {
        // ─── فیلترِ جاری ───────────────────────────────────────────────────
        private GeoFilter _filter = GeoFilter.ForCurrentUser();
        private string _metric = GeoAnalyticsService.MetricCases;

        // ─── دادهٔ بارگذاری‌شده ─────────────────────────────────────────────
        private List<GeoAnalyticsService.RegionStats> _regions =
            new List<GeoAnalyticsService.RegionStats>();
        private GeoAnalyticsService.RegionStats _totals = new GeoAnalyticsService.RegionStats();
        private GeoAnalyticsService.RegionStats _scopeStats = new GeoAnalyticsService.RegionStats();
        private List<GeoAnalyticsService.BreakdownRow> _requestTypes, _serviceStatuses,
                                                        _economic, _vulnerability, _sponsorship, _completion;
        private GeoAnalyticsService.AssistanceStats _assistance = new GeoAnalyticsService.AssistanceStats();
        private DataTable _ageMatrix = new DataTable();
        private DataTable _unknownRegions = new DataTable();
        private List<GeoInsightEngine.Insight> _insights = new List<GeoInsightEngine.Insight>();

        private CancellationTokenSource _loadCts;
        private bool _building = true;

        // ─── کنترل‌ها ───────────────────────────────────────────────────────
        private AfghanMapControl _map;
        private ComboBox _cmbMetric, _cmbProvince, _cmbDistrict, _cmbCenter,
                         _cmbRequestType, _cmbServiceStatus, _cmbBand, _cmbEconomic,
                         _cmbSponsorship, _cmbGender, _cmbAgeGroup, _cmbAssistanceType,
                         _cmbCompletion, _cmbArchive;
        private TextBox _txtFrom, _txtTo;
        private Label _lblScope, _lblStatus, _lblSubScope;
        private Button _btnBack;
        private DataGridView _gridRegions;
        private FlowLayoutPanel _insightFlow;
        private TabControl _tabs;
        private readonly Dictionary<string, DataGridView> _tabGrids =
            new Dictionary<string, DataGridView>(StringComparer.Ordinal);
        private readonly List<StatTile> _kpiTiles = new List<StatTile>();
        private readonly List<StatTile> _ageTiles = new List<StatTile>();
        private readonly List<StatTile> _riskTiles = new List<StatTile>();

        // ═══════════════════════════════════════════════════════════════════
        public FrmGeoCommandCenter()
        {
            Text = "مرکز فرماندهی آماری و تحلیلی";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1180, 720);
            KeyPreview = true;

            BuildUi();
            _building = false;

            Load += delegate { RefreshAsync(); };
            KeyDown += FrmGeoCommandCenter_KeyDown;
        }

        private void FrmGeoCommandCenter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5) { RefreshAsync(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape && _map.IsDistrictLevel) { BackToCountry(); e.Handled = true; }
        }

        // ═══════════════════════════════════════════════════════════════════
        // چیدمان
        // ═══════════════════════════════════════════════════════════════════
        private void BuildUi()
        {
            SuspendLayout();

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = UiTheme.Background,
                Padding = new Padding(10, 8, 10, 8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));   // سربرگ
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));   // فیلترها
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // محتوا
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));   // خروجی‌ها
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));  // نقشه
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));  // داده

            Control header = BuildHeader();
            root.Controls.Add(header, 0, 0);
            root.SetColumnSpan(header, 2);

            Control filters = BuildFilterStrip();
            root.Controls.Add(filters, 0, 1);
            root.SetColumnSpan(filters, 2);

            root.Controls.Add(BuildMapColumn(), 0, 2);
            root.Controls.Add(BuildDataColumn(), 1, 2);

            Control footer = BuildFooter();
            root.Controls.Add(footer, 0, 3);
            root.SetColumnSpan(footer, 2);

            Controls.Add(root);
            ResumeLayout(true);
        }

        // ─── سربرگ ──────────────────────────────────────────────────────────
        private Control BuildHeader()
        {
            Panel bar = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.PrimaryDark };
            bar.Padding = new Padding(14, 0, 14, 0);

            Label title = new Label
            {
                Text = "مرکز فرماندهی آماری و تحلیلی",
                Font = UiTheme.FontBold(UiTheme.SizeTitle),
                ForeColor = Color.White,
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 330,
                TextAlign = ContentAlignment.MiddleRight
            };

            _lblScope = new Label
            {
                Text = "کل کشور",
                Font = UiTheme.FontBold(UiTheme.SizeMedium),
                ForeColor = Color.White,
                Dock = DockStyle.Right,
                Width = 250,
                TextAlign = ContentAlignment.MiddleRight
            };

            _lblSubScope = new Label
            {
                Text = "",
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = Color.FromArgb(200, 225, 245),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 10, 0)
            };

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0, 12, 0, 0)
            };

            _btnBack = HeaderButton("بازگشت به کشور", "◀");
            _btnBack.Visible = false;
            _btnBack.Click += delegate { BackToCountry(); };

            Button refresh = HeaderButton("بروزرسانی (F5)", "⟳");
            refresh.Click += delegate { RefreshAsync(); };

            Button clear = HeaderButton("پاک‌سازی فیلترها", "✕");
            clear.Click += delegate { ClearFilters(); };

            actions.Controls.Add(_btnBack);
            actions.Controls.Add(refresh);
            actions.Controls.Add(clear);

            bar.Controls.Add(_lblSubScope);
            bar.Controls.Add(_lblScope);
            bar.Controls.Add(title);
            bar.Controls.Add(actions);
            return bar;
        }

        private Button HeaderButton(string text, string icon)
        {
            Button b = UiTheme.CreateButton(text, icon, UiTheme.Primary);
            b.Font = UiTheme.FontBold(8.75f);
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(0, 34);
            b.Padding = new Padding(9, 4, 9, 4);
            b.Margin = new Padding(4, 0, 0, 0);
            b.FlatAppearance.MouseOverBackColor = UiTheme.PrimaryLight;
            return b;
        }

        // ─── نوارِ فیلتر ────────────────────────────────────────────────────
        private Control BuildFilterStrip()
        {
            Panel host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.CardBack,
                Padding = new Padding(8, 6, 8, 6),
                Margin = new Padding(0, 6, 0, 6)
            };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                AutoScroll = true
            };

            // ولایت تنها فیلتری است که رویدادِ خودش را جدا می‌گیرد: اول باید
            // فهرستِ ولسوالی‌ها را عوض کند، بعد یک‌بار کوئری بزند — وگرنه هر
            // تعویضِ ولایت دو بارگذاریِ پشت‌سرهم می‌ساخت.
            _cmbProvince = AddFilter(flow, "ولایت", 130, false);
            _cmbDistrict = AddFilter(flow, "ولسوالی", 130);
            _cmbCenter = AddFilter(flow, "مرکز", 120);
            _cmbRequestType = AddFilter(flow, "نوع پرونده", 125);
            _cmbServiceStatus = AddFilter(flow, "وضعیت خدمات", 130);
            _cmbEconomic = AddFilter(flow, "اولویت اقتصادی", 125);
            _cmbBand = AddFilter(flow, "سطح آسیب‌پذیری", 125);
            _cmbSponsorship = AddFilter(flow, "وضعیت حامی", 120);
            _cmbAssistanceType = AddFilter(flow, "نوع کمک", 100);
            _cmbGender = AddFilter(flow, "جنسیت عضو", 105);
            _cmbAgeGroup = AddFilter(flow, "گروه سنی عضو", 110);
            _cmbCompletion = AddFilter(flow, "تکمیل پرونده", 110);
            _cmbArchive = AddFilter(flow, "وضعیت رکورد", 110);

            _txtFrom = AddDateFilter(flow, "از تاریخ");
            _txtTo = AddDateFilter(flow, "تا تاریخ");

            PopulateFilters();

            host.Controls.Add(flow);
            return host;
        }

        private ComboBox AddFilter(FlowLayoutPanel parent, string caption, int width,
                                   bool autoRefresh = true)
        {
            Panel cell = new Panel { Width = width, Height = 46, Margin = new Padding(3, 2, 3, 2) };

            Label lbl = new Label
            {
                Text = caption,
                Dock = DockStyle.Top,
                Height = 18,
                Font = UiTheme.Font(8f),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };

            ComboBox cmb = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                RightToLeft = RightToLeft.Yes,
                Font = UiTheme.Font(9f),
                FlatStyle = FlatStyle.Flat
            };
            if (autoRefresh)
                cmb.SelectedIndexChanged += delegate { if (!_building) OnFilterChanged(); };

            cell.Controls.Add(cmb);
            cell.Controls.Add(lbl);
            parent.Controls.Add(cell);
            return cmb;
        }

        private TextBox AddDateFilter(FlowLayoutPanel parent, string caption)
        {
            Panel cell = new Panel { Width = 110, Height = 46, Margin = new Padding(3, 2, 3, 2) };

            Label lbl = new Label
            {
                Text = caption,
                Dock = DockStyle.Top,
                Height = 18,
                Font = UiTheme.Font(8f),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };

            TextBox tb = new TextBox
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                Font = UiTheme.Font(9f),
                TextAlign = HorizontalAlignment.Center
            };
            UiTheme.StyleTextBox(tb);
            UiTheme.SetTip(tb, "تاریخ شمسی مثل ۱۴۰۵/۰۶/۰۱ — خالی یعنی بدون محدودیت");
            tb.Leave += delegate { if (!_building) OnFilterChanged(); };

            cell.Controls.Add(tb);
            cell.Controls.Add(lbl);
            parent.Controls.Add(cell);
            return tb;
        }

        private void PopulateFilters()
        {
            // ولایت — از همان فهرستِ مرجعِ TblLookup، نه از داده‌های موجود،
            // تا ولایتِ بدونِ پرونده هم قابلِ انتخاب بماند.
            _cmbProvince.Items.Add("همه ولایات");
            foreach (string p in LookupHelper.GetValues("Province")) _cmbProvince.Items.Add(p);
            if (_cmbProvince.Items.Count <= 1)
                foreach (string p in AfghanMapGeometry.ProvinceNames()) _cmbProvince.Items.Add(p);
            _cmbProvince.SelectedIndex = 0;
            _cmbProvince.SelectedIndexChanged += delegate { if (!_building) RefillDistricts(); };

            _cmbDistrict.Items.Add("همه ولسوالی‌ها");
            _cmbDistrict.SelectedIndex = 0;

            _cmbCenter.Items.Add("همه مراکز");
            try
            {
                foreach (KeyValuePair<int, string> c in LoadCenters())
                    _cmbCenter.Items.Add(new ComboItem(c.Key, c.Value));
            }
            catch { }
            _cmbCenter.SelectedIndex = 0;

            _cmbRequestType.Items.Add("همه انواع");
            foreach (ReferenceOption o in ReferenceDataService.GetRequestTypes())
                _cmbRequestType.Items.Add(new ComboItem(o.ID, o.Name));
            _cmbRequestType.SelectedIndex = 0;

            _cmbServiceStatus.Items.Add("همه وضعیت‌ها");
            foreach (ReferenceOption o in ReferenceDataService.GetServiceStatuses())
                _cmbServiceStatus.Items.Add(new ComboItem(o.ID, o.Name));
            _cmbServiceStatus.SelectedIndex = 0;

            _cmbEconomic.Items.Add("همه سطوح");
            foreach (string e in GeoFilter.EconomicPriorities) _cmbEconomic.Items.Add(e);
            _cmbEconomic.Items.Add("ثبت‌نشده");
            _cmbEconomic.SelectedIndex = 0;

            _cmbBand.Items.Add("همه سطوح");
            _cmbBand.Items.Add(new ComboItem("HIGH", "پرخطر"));
            _cmbBand.Items.Add(new ComboItem("MEDIUM", "متوسط"));
            _cmbBand.Items.Add(new ComboItem("LOW", "کم‌خطر"));
            _cmbBand.SelectedIndex = 0;

            _cmbSponsorship.Items.Add("همه");
            foreach (string[] s in GeoFilter.SponsorshipStates)
                _cmbSponsorship.Items.Add(new ComboItem(s[0], s[1]));
            _cmbSponsorship.SelectedIndex = 0;

            _cmbAssistanceType.Items.Add("همه");
            foreach (string v in LookupHelper.GetValues("AssistanceType")) _cmbAssistanceType.Items.Add(v);
            _cmbAssistanceType.SelectedIndex = 0;

            _cmbGender.Items.Add("همه");
            _cmbGender.Items.Add(new ComboItem(GeoFilter.GenderMale, "مرد"));
            _cmbGender.Items.Add(new ComboItem(GeoFilter.GenderFemale, "زن"));
            _cmbGender.SelectedIndex = 0;

            _cmbAgeGroup.Items.Add("همه");
            foreach (string[] a in GeoFilter.AgeGroups)
                _cmbAgeGroup.Items.Add(new ComboItem(a[0], a[1]));
            _cmbAgeGroup.SelectedIndex = 0;

            _cmbCompletion.Items.Add("همه");
            _cmbCompletion.Items.Add(new ComboItem(50, "۵۰٪ به بالا"));
            _cmbCompletion.Items.Add(new ComboItem(75, "۷۵٪ به بالا"));
            _cmbCompletion.Items.Add(new ComboItem(100, "۱۰۰٪ (کامل)"));
            _cmbCompletion.SelectedIndex = 0;

            _cmbArchive.Items.Add("فقط فعال");
            _cmbArchive.Items.Add("شامل بایگانی");
            _cmbArchive.Items.Add("فقط بایگانی");
            _cmbArchive.SelectedIndex = 0;
        }

        private static List<KeyValuePair<int, string>> LoadCenters()
        {
            List<KeyValuePair<int, string>> list = new List<KeyValuePair<int, string>>();
            DAL.DatabaseHelper db = new DAL.DatabaseHelper();
            using (System.Data.SQLite.SQLiteConnection con = db.GetConnection())
            {
                con.Open();
                using (System.Data.SQLite.SQLiteCommand cmd = new System.Data.SQLite.SQLiteCommand(
                    "SELECT CenterID, Name FROM TblCenter ORDER BY Name;", con))
                using (System.Data.SQLite.SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(new KeyValuePair<int, string>(
                            Convert.ToInt32(r["CenterID"], CultureInfo.InvariantCulture),
                            Convert.ToString(r["Name"])));
                }
            }
            return list;
        }

        private void RefillDistricts()
        {
            string province = _cmbProvince.SelectedIndex <= 0 ? "" : Convert.ToString(_cmbProvince.SelectedItem);

            bool wasBuilding = _building;
            _building = true;
            _cmbDistrict.Items.Clear();
            _cmbDistrict.Items.Add("همه ولسوالی‌ها");
            foreach (string d in AfghanGeoData.GetDistricts(province)) _cmbDistrict.Items.Add(d);
            _cmbDistrict.SelectedIndex = 0;
            _building = wasBuilding;

            if (!_building) OnFilterChanged();
        }

        // ─── ستونِ نقشه ─────────────────────────────────────────────────────
        private Control BuildMapColumn()
        {
            TableLayoutPanel col = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0, 0, 6, 0)
            };
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 18));

            // ── کارتِ نقشه ─────────────────────────────────────────────────
            DashboardCard mapCard = new DashboardCard("نقشهٔ توزیع جغرافیایی");
            mapCard.Dock = DockStyle.Fill;
            mapCard.Margin = new Padding(0, 0, 0, 6);

            _cmbMetric = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                RightToLeft = RightToLeft.Yes,
                Width = 190,
                Font = UiTheme.Font(8.75f),
                Dock = DockStyle.Right
            };
            foreach (string[] m in GeoAnalyticsService.HeatMetrics)
                _cmbMetric.Items.Add(new ComboItem(m[0], m[1]));
            _cmbMetric.SelectedIndex = 0;
            _cmbMetric.SelectedIndexChanged += delegate
            {
                if (_building) return;
                ComboItem item = _cmbMetric.SelectedItem as ComboItem;
                _metric = item == null ? GeoAnalyticsService.MetricCases : Convert.ToString(item.Value);
                // تعویضِ معیار هیچ کوئریِ تازه‌ای نمی‌خواهد — همان ردیف‌ها با
                // عددِ دیگری رنگ می‌شوند.
                PaintMap();
                BindRegionsGrid();
            };
            mapCard.HeaderRight.Controls.Add(_cmbMetric);

            _map = new AfghanMapControl { Dock = DockStyle.Fill };
            _map.RegionSelected += Map_RegionSelected;
            _map.RegionDrillRequested += Map_RegionDrillRequested;
            _map.RegionActivated += Map_RegionActivated;
            mapCard.Content.Controls.Add(_map);

            // ── جدولِ مناطق ────────────────────────────────────────────────
            DashboardCard rankCard = new DashboardCard("رتبه‌بندی مناطق");
            rankCard.Dock = DockStyle.Fill;
            rankCard.Margin = new Padding(0, 0, 0, 6);

            _gridRegions = NewGrid();
            _gridRegions.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= _regions.Count) return;
                OpenCasesFor(RegionFilter(_regions[e.RowIndex]));
            };
            _gridRegions.SelectionChanged += delegate
            {
                if (_building || _gridRegions.CurrentRow == null) return;
                int i = _gridRegions.CurrentRow.Index;
                if (i >= 0 && i < _regions.Count)
                {
                    _map.SelectRegion(_regions[i].Region);
                    ShowScope(_regions[i]);
                }
            };
            UiTheme.SetTip(_gridRegions, "دوکلیک روی هر ردیف: همان پرونده‌ها در فرم پرونده‌ها باز می‌شود");
            rankCard.Content.Controls.Add(_gridRegions);

            // ── هوشِ مدیریتی ───────────────────────────────────────────────
            DashboardCard insightCard = new DashboardCard("هوش مدیریتی");
            insightCard.Dock = DockStyle.Fill;

            _insightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = UiTheme.CardBack,
                Padding = new Padding(4)
            };
            _insightFlow.SizeChanged += InsightFlow_SizeChanged;
            insightCard.Content.Controls.Add(_insightFlow);

            col.Controls.Add(mapCard, 0, 0);
            col.Controls.Add(rankCard, 0, 1);
            col.Controls.Add(insightCard, 0, 2);
            return col;
        }

        // ─── ستونِ داده ─────────────────────────────────────────────────────
        private Control BuildDataColumn()
        {
            TableLayoutPanel col = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(6, 0, 0, 0)
            };
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            col.Controls.Add(BuildTileRow(_kpiTiles, UiTheme.Primary, new[]
            {
                "پرونده‌ها", "خانواده‌ها", "اعضای خانواده", "مرد", "زن"
            }, 86), 0, 0);

            col.Controls.Add(BuildTileRow(_ageTiles, UiTheme.PrimaryLight, new[]
            {
                "کودک", "نوجوان", "جوان", "بزرگسال", "سالمند"
            }, 80), 0, 1);

            col.Controls.Add(BuildTileRow(_riskTiles, ColorTranslator.FromHtml("#7A5EA8"), new[]
            {
                "پرخطر", "میانگین آسیب‌پذیری", "دارای حامی", "فاقد حامی", "مجموع کمک‌ها"
            }, 80), 0, 2);

            col.Controls.Add(BuildTabs(), 0, 3);
            return col;
        }

        private Control BuildTileRow(List<StatTile> sink, Color accent, string[] captions, int height)
        {
            TableLayoutPanel row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = captions.Length,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 6)
            };
            for (int i = 0; i < captions.Length; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / captions.Length));

            for (int i = 0; i < captions.Length; i++)
            {
                StatTile tile = new StatTile(captions[i], accent) { Dock = DockStyle.Fill };
                tile.Margin = new Padding(i == 0 ? 0 : 3, 0, 3, 0);
                sink.Add(tile);
                row.Controls.Add(tile, i, 0);
            }
            return row;
        }

        private Control BuildTabs()
        {
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Font = UiTheme.Font(9f)
            };

            AddGridTab("نوع پرونده");
            AddGridTab("وضعیت خدمات");
            AddGridTab("اولویت اقتصادی");
            AddGridTab("آسیب‌پذیری");
            AddGridTab("حمایت مالی");
            AddGridTab("تکمیل پرونده");
            AddGridTab("جمعیت و سن");
            AddGridTab("کمک‌های مالی");
            AddGridTab("مناطق ناشناخته");

            return _tabs;
        }

        private void AddGridTab(string title)
        {
            TabPage page = new TabPage(title) { BackColor = UiTheme.CardBack, Padding = new Padding(4) };
            DataGridView grid = NewGrid();

            // دوکلیک روی هر ردیفِ تفکیک ⇒ همان زیرمجموعه در FrmCase.
            grid.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0) return;
                DrillFromBreakdown(title, e.RowIndex);
            };
            UiTheme.SetTip(grid, "دوکلیک روی هر ردیف: همان پرونده‌ها در فرم پرونده‌ها باز می‌شود");

            page.Controls.Add(grid);
            _tabs.TabPages.Add(page);
            _tabGrids[title] = grid;
        }

        private DataGridView NewGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RightToLeft = RightToLeft.Yes,
                RowHeadersVisible = false
            };
            UiTheme.StyleGrid(grid);
            return grid;
        }

        // ─── نوارِ خروجی ────────────────────────────────────────────────────
        private Control BuildFooter()
        {
            Panel bar = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(6, 7, 6, 6)
            };

            flow.Controls.Add(FooterButton("گزارش PDF (نقشه + داده)", "🗎", UiTheme.Danger, delegate { ExportPdf(); }));
            flow.Controls.Add(FooterButton("گزارش ولایتی (هر ولایت یک صفحه)", "🗂", ColorTranslator.FromHtml("#7A5EA8"),
                delegate { ExportProvincePagesPdf(); }));
            flow.Controls.Add(FooterButton("پیش‌نمایش و چاپ", "🖶", UiTheme.Primary, delegate { PrintReport(); }));
            flow.Controls.Add(FooterButton("خروجی اکسل", "▤", UiTheme.Success, delegate { ExportExcel(); }));
            flow.Controls.Add(FooterButton("تصویر نقشه", "🖼", UiTheme.Warning, delegate { ExportMapImage(); }));
            flow.Controls.Add(FooterButton("باز کردن پرونده‌ها", "▦", UiTheme.PrimaryDark,
                delegate { OpenCasesFor(_filter); }));

            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(8.75f),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(12, 0, 12, 0),
                Text = "آماده"
            };

            bar.Controls.Add(_lblStatus);
            bar.Controls.Add(flow);
            return bar;
        }

        private Button FooterButton(string text, string icon, Color color, EventHandler handler)
        {
            Button b = UiTheme.CreateButton(text, icon, color);
            b.Font = UiTheme.FontBold(8.75f);
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(0, 34);
            b.Padding = new Padding(10, 4, 10, 4);
            b.Margin = new Padding(4, 0, 0, 0);
            b.Click += handler;
            return b;
        }

        // ═══════════════════════════════════════════════════════════════════
        // خواندنِ فیلترها از UI
        // ═══════════════════════════════════════════════════════════════════
        private void OnFilterChanged()
        {
            CollectFilter();
            RefreshAsync();
        }

        private void CollectFilter()
        {
            GeoFilter f = new GeoFilter();

            f.CenterId = SecurityContext.CenterFilterId;
            ComboItem center = _cmbCenter.SelectedItem as ComboItem;
            if (center != null) f.CenterId = Convert.ToInt32(center.Value, CultureInfo.InvariantCulture);

            f.Province = _cmbProvince.SelectedIndex <= 0 ? "" : Convert.ToString(_cmbProvince.SelectedItem);
            f.District = _cmbDistrict.SelectedIndex <= 0 ? "" : Convert.ToString(_cmbDistrict.SelectedItem);

            f.RequestTypeId = IntOf(_cmbRequestType);
            f.ServiceStatusId = IntOf(_cmbServiceStatus);
            f.VulnerabilityBand = StrOf(_cmbBand);
            f.Sponsorship = StrOf(_cmbSponsorship);
            f.Gender = StrOf(_cmbGender);
            f.AgeGroup = StrOf(_cmbAgeGroup);
            f.MinCompletionPercent = IntOf(_cmbCompletion);

            if (_cmbEconomic.SelectedIndex > 0)
            {
                string v = Convert.ToString(_cmbEconomic.SelectedItem);
                // «ثبت‌نشده» یعنی مقدارِ خالی؛ با رشتهٔ خالی نمی‌شود فیلتر کرد
                // (خالی یعنی «بدون فیلتر»)، پس یک نشانهٔ اختصاصی به کار می‌رود.
                f.EconomicPriority = v == "ثبت‌نشده" ? GeoFilter.EconomicUnset : v;
            }

            if (_cmbAssistanceType.SelectedIndex > 0)
                f.AssistanceType = Convert.ToString(_cmbAssistanceType.SelectedItem);

            f.IncludeArchived = _cmbArchive.SelectedIndex == 1;
            f.OnlyArchived = _cmbArchive.SelectedIndex == 2;

            f.DateFrom = NormalizeDate(_txtFrom.Text);
            f.DateTo = NormalizeDate(_txtTo.Text);

            _filter = f;
        }

        private static string NormalizeDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            // ورودیِ کاربر ممکن است شمسی باشد؛ همان مسیرِ استانداردِ پروژه.
            string normalized = PersianDateHelper.NormalizeUserDateToStored(text.Trim());
            return string.IsNullOrWhiteSpace(normalized) ? "" : normalized;
        }

        private static int IntOf(ComboBox cmb)
        {
            ComboItem item = cmb.SelectedItem as ComboItem;
            if (item == null) return 0;
            try { return Convert.ToInt32(item.Value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static string StrOf(ComboBox cmb)
        {
            ComboItem item = cmb.SelectedItem as ComboItem;
            return item == null ? "" : Convert.ToString(item.Value);
        }

        private void ClearFilters()
        {
            _building = true;
            _cmbProvince.SelectedIndex = 0;
            RefillDistricts();
            _cmbCenter.SelectedIndex = 0;
            _cmbRequestType.SelectedIndex = 0;
            _cmbServiceStatus.SelectedIndex = 0;
            _cmbEconomic.SelectedIndex = 0;
            _cmbBand.SelectedIndex = 0;
            _cmbSponsorship.SelectedIndex = 0;
            _cmbAssistanceType.SelectedIndex = 0;
            _cmbGender.SelectedIndex = 0;
            _cmbAgeGroup.SelectedIndex = 0;
            _cmbCompletion.SelectedIndex = 0;
            _cmbArchive.SelectedIndex = 0;
            _txtFrom.Text = "";
            _txtTo.Text = "";
            _building = false;

            _map.DrillTo("");
            _btnBack.Visible = false;
            OnFilterChanged();
        }

        // ═══════════════════════════════════════════════════════════════════
        // بارگذاری
        //
        // آموزش — چرا async: با ۲۰٬۰۰۰ پرونده، تجمیعِ منطقه‌ای چند صد
        // میلی‌ثانیه طول می‌کشد. اگر روی نخِ UI اجرا شود، هر تغییرِ فیلتر
        // پنجره را قفل می‌کند. توکنِ لغو هم لازم است چون کاربر معمولاً چند
        // فیلتر را پشتِ‌سرِ هم عوض می‌کند و نتیجهٔ قدیمی نباید روی تازه بنشیند.
        // ═══════════════════════════════════════════════════════════════════
        private async void RefreshAsync()
        {
            if (_loadCts != null) { _loadCts.Cancel(); _loadCts.Dispose(); }
            _loadCts = new CancellationTokenSource();
            CancellationToken token = _loadCts.Token;

            SetStatus("در حال محاسبه…");
            UseWaitCursor = true;

            GeoFilter filter = _filter.Clone();
            bool districtLevel = _map.IsDistrictLevel;
            string drilled = _map.DrilledProvince;

            // در سطحِ ولسوالی، دامنه به همان ولایت محدود می‌شود.
            GeoFilter scopeFilter = filter.Clone();
            if (districtLevel) scopeFilter.Province = drilled;

            try
            {
                LoadResult result = await Task.Run(delegate
                {
                    LoadResult r = new LoadResult();
                    r.Regions = GeoAnalyticsService.GetRegionRollup(
                        scopeFilter, districtLevel ? GeoAnalyticsService.GeoLevel.District
                                                   : GeoAnalyticsService.GeoLevel.Province);
                    r.RequestTypes = GeoAnalyticsService.GetRequestTypeBreakdown(scopeFilter);
                    r.ServiceStatuses = GeoAnalyticsService.GetServiceStatusBreakdown(scopeFilter);
                    r.Economic = GeoAnalyticsService.GetEconomicPriorityBreakdown(scopeFilter);
                    r.Vulnerability = GeoAnalyticsService.GetVulnerabilityBreakdown(scopeFilter);
                    r.Sponsorship = GeoAnalyticsService.GetSponsorshipBreakdown(scopeFilter);
                    r.Completion = GeoAnalyticsService.GetCompletionBreakdown(scopeFilter);
                    r.Assistance = GeoAnalyticsService.GetAssistanceStats(scopeFilter);
                    r.AgeMatrix = GeoAnalyticsService.GetAgeGenderMatrix(scopeFilter);
                    r.UnknownRegions = GeoAnalyticsService.GetUnknownRegions(scopeFilter);
                    return r;
                }, token);

                if (token.IsCancellationRequested || IsDisposed) return;

                _regions = result.Regions;
                _requestTypes = result.RequestTypes;
                _serviceStatuses = result.ServiceStatuses;
                _economic = result.Economic;
                _vulnerability = result.Vulnerability;
                _sponsorship = result.Sponsorship;
                _completion = result.Completion;
                _assistance = result.Assistance;
                _ageMatrix = result.AgeMatrix;
                _unknownRegions = result.UnknownRegions;

                _totals = GeoAnalyticsService.Aggregate(_regions,
                    districtLevel ? drilled : "کل کشور");
                _scopeStats = _totals;

                _insights = GeoInsightEngine.Build(_regions, scopeFilter, districtLevel, drilled);

                BindAll();
                SetStatus(string.Format(CultureInfo.InvariantCulture,
                    "{0} منطقه • {1} پرونده • آخرین بروزرسانی {2}",
                    ReportDoc.Fa(_regions.Count), ReportDoc.Fa(_totals.Cases),
                    PersianDateHelper.ToPersianDateTimeStringSafe(DateTime.Now)));
            }
            catch (OperationCanceledException) { /* فیلترِ تازه‌تری آمد */ }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    SetStatus("خطا در محاسبهٔ آمار");
                    UiTheme.ShowError(this, "خطا در محاسبهٔ آمار: " + ex.Message);
                }
            }
            finally
            {
                if (!IsDisposed) UseWaitCursor = false;
            }
        }

        private sealed class LoadResult
        {
            public List<GeoAnalyticsService.RegionStats> Regions;
            public List<GeoAnalyticsService.BreakdownRow> RequestTypes, ServiceStatuses,
                                                           Economic, Vulnerability, Sponsorship, Completion;
            public GeoAnalyticsService.AssistanceStats Assistance;
            public DataTable AgeMatrix, UnknownRegions;
        }

        // ═══════════════════════════════════════════════════════════════════
        // نشاندنِ داده روی UI
        // ═══════════════════════════════════════════════════════════════════
        private void BindAll()
        {
            PaintMap();
            BindRegionsGrid();
            BindTiles(_scopeStats);
            BindTabs();
            BindInsights();
            UpdateScopeLabels();
        }

        private void PaintMap()
        {
            Dictionary<string, double> values =
                new Dictionary<string, double>(StringComparer.Ordinal);
            Dictionary<string, string> tips =
                new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (GeoAnalyticsService.RegionStats s in _regions)
            {
                values[s.Region] = s.MetricValue(_metric);
                tips[s.Region] = BuildTooltip(s);
            }

            string suffix = _metric == GeoAnalyticsService.MetricVulnerability ? "" : "";
            _map.SetData(values, tips, GeoAnalyticsService.HeatMetricDisplay(_metric), suffix);
        }

        private static string BuildTooltip(GeoAnalyticsService.RegionStats s)
        {
            return s.Region + Environment.NewLine +
                   "پرونده‌ها: " + Fa(s.Cases) + "   خانواده‌ها: " + Fa(s.Families) + Environment.NewLine +
                   "اعضا: " + Fa(s.Members) + "   (مرد " + Fa(s.Male) + " / زن " + Fa(s.Female) + ")" + Environment.NewLine +
                   "یتیم: " + Fa(s.Orphans) + "   معلول: " + Fa(s.Disabled) + "   مهاجر: " + Fa(s.Migrants) + Environment.NewLine +
                   "فعال: " + Fa(s.ActiveCases) + "   فاقد حامی: " + Fa(s.WithoutSponsor) + Environment.NewLine +
                   "میانگین آسیب‌پذیری: " + ReportDoc.Fa(s.VulnAvg.ToString("0.0", CultureInfo.InvariantCulture)) +
                   Environment.NewLine + Environment.NewLine +
                   "کلیک: انتخاب   •   راست‌کلیک: ولسوالی‌ها   •   دوکلیک: پرونده‌ها";
        }

        private void BindRegionsGrid()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add(_map.IsDistrictLevel ? "ولسوالی" : "ولایت", typeof(string));
            dt.Columns.Add(GeoAnalyticsService.HeatMetricDisplay(_metric), typeof(string));
            dt.Columns.Add("پرونده", typeof(int));
            dt.Columns.Add("خانواده", typeof(int));
            dt.Columns.Add("اعضا", typeof(int));
            dt.Columns.Add("فاقد حامی", typeof(int));

            foreach (GeoAnalyticsService.RegionStats s in _regions)
            {
                double m = s.MetricValue(_metric);
                dt.Rows.Add(s.Region,
                            ReportDoc.Fa(Math.Abs(m - Math.Round(m)) < 0.05
                                ? ((long)Math.Round(m)).ToString("#,0", CultureInfo.InvariantCulture)
                                : m.ToString("#,0.0", CultureInfo.InvariantCulture)),
                            s.Cases, s.Families, s.Members, s.WithoutSponsor);
            }

            bool wasBuilding = _building;
            _building = true;
            _gridRegions.DataSource = dt;
            _building = wasBuilding;
        }

        private void BindTiles(GeoAnalyticsService.RegionStats t)
        {
            SetTile(_kpiTiles, 0, Fa(t.Cases));
            SetTile(_kpiTiles, 1, Fa(t.Families));
            SetTile(_kpiTiles, 2, Fa(t.Members));
            SetTile(_kpiTiles, 3, Fa(t.Male));
            SetTile(_kpiTiles, 4, Fa(t.Female));

            SetTile(_ageTiles, 0, Fa(t.Child));
            SetTile(_ageTiles, 1, Fa(t.Teen));
            SetTile(_ageTiles, 2, Fa(t.Youth));
            SetTile(_ageTiles, 3, Fa(t.Adult));
            SetTile(_ageTiles, 4, Fa(t.Elder));

            SetTile(_riskTiles, 0, Fa(t.HighRisk));
            SetTile(_riskTiles, 1, ReportDoc.Fa(t.VulnAvg.ToString("0.0", CultureInfo.InvariantCulture)));
            SetTile(_riskTiles, 2, Fa(t.WithSponsor));
            SetTile(_riskTiles, 3, Fa(t.WithoutSponsor));
            SetTile(_riskTiles, 4, ReportDoc.Fa(t.AssistanceTotal.ToString("#,0", CultureInfo.InvariantCulture)));
        }

        private static void SetTile(List<StatTile> tiles, int index, string value)
        {
            if (index >= 0 && index < tiles.Count) tiles[index].Value = value;
        }

        private void BindTabs()
        {
            SetTab("نوع پرونده", GeoAnalyticsService.ToDataTable(_requestTypes, "نوع پرونده"));
            SetTab("وضعیت خدمات", GeoAnalyticsService.ToDataTable(_serviceStatuses, "وضعیت خدمات"));
            SetTab("اولویت اقتصادی", GeoAnalyticsService.ToDataTable(_economic, "اولویت اقتصادی"));
            SetTab("آسیب‌پذیری", GeoAnalyticsService.ToDataTable(_vulnerability, "سطح آسیب‌پذیری"));
            SetTab("حمایت مالی", GeoAnalyticsService.ToDataTable(_sponsorship, "وضعیت حمایت"));
            SetTab("تکمیل پرونده", GeoAnalyticsService.ToDataTable(_completion, "وضعیت تکمیل"));
            SetTab("جمعیت و سن", _ageMatrix);
            SetTab("مناطق ناشناخته", _unknownRegions);

            DataTable money = new DataTable();
            money.Columns.Add("شاخص", typeof(string));
            money.Columns.Add("مقدار", typeof(string));
            money.Rows.Add("تعداد پرداخت", Fa(_assistance.Count));
            money.Rows.Add("پرونده‌های دریافت‌کننده", Fa(_assistance.CasesReceiving));
            money.Rows.Add("مجموع کمک‌ها", Money(_assistance.Total));
            money.Rows.Add("میانگین کمک", Money(_assistance.Average));
            money.Rows.Add("بیشترین کمک", Money(_assistance.Max));
            money.Rows.Add("کمترین کمک", Money(_assistance.Min));
            SetTab("کمک‌های مالی", money);
        }

        private void SetTab(string title, DataTable data)
        {
            DataGridView grid;
            if (!_tabGrids.TryGetValue(title, out grid)) return;
            grid.DataSource = data;
        }

        private void BindInsights()
        {
            _insightFlow.SuspendLayout();
            foreach (Control c in _insightFlow.Controls) c.Dispose();
            _insightFlow.Controls.Clear();

            foreach (GeoInsightEngine.Insight ins in _insights)
                _insightFlow.Controls.Add(BuildInsightRow(ins));

            InsightFlow_SizeChanged(_insightFlow, EventArgs.Empty);
            _insightFlow.ResumeLayout(true);
        }

        private Control BuildInsightRow(GeoInsightEngine.Insight insight)
        {
            Panel row = new Panel
            {
                Width = Math.Max(240, _insightFlow.ClientSize.Width - 26),
                Height = 44,
                BackColor = UiTheme.CardBack,
                Margin = new Padding(2, 1, 2, 3),
                Cursor = insight.DrillFilter != null ? Cursors.Hand : Cursors.Default
            };

            Panel stripe = new Panel { Dock = DockStyle.Right, Width = 4, BackColor = insight.Accent };

            Label text = new Label
            {
                Text = insight.Glyph + "  " + insight.Text,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(8.75f),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(6, 0, 8, 0),
                Cursor = row.Cursor
            };

            if (insight.DrillFilter != null)
            {
                GeoFilter target = insight.DrillFilter;
                EventHandler open = delegate { OpenCasesFor(target); };
                row.Click += open;
                text.Click += open;
                UiTheme.SetTip(text, "کلیک: نمایش همین پرونده‌ها در فرم پرونده‌ها");
            }

            row.Controls.Add(text);
            row.Controls.Add(stripe);
            return row;
        }

        // FlowLayoutPanel عرضِ فرزندان را خودش نمی‌کشد. به‌جای آنکه هر ردیف
        // یک اشتراکِ SizeChanged بگیرد (که با هر بارگذاری انباشته می‌شد)،
        // یک هندلرِ واحد روی خودِ پنل همهٔ ردیف‌ها را هم‌عرض می‌کند.
        private void InsightFlow_SizeChanged(object sender, EventArgs e)
        {
            int width = Math.Max(240, _insightFlow.ClientSize.Width - 26);
            foreach (Control c in _insightFlow.Controls) c.Width = width;
        }

        private void UpdateScopeLabels()
        {
            _lblScope.Text = _map.IsDistrictLevel
                ? "ولایت " + _map.DrilledProvince + " — سطح ولسوالی"
                : "کل کشور — سطح ولایت";
            _lblSubScope.Text = _filter.Describe();
            _btnBack.Visible = _map.IsDistrictLevel;
        }

        private void ShowScope(GeoAnalyticsService.RegionStats stats)
        {
            _scopeStats = stats;
            BindTiles(stats);
            _lblSubScope.Text = stats.Region + " — " + _filter.Describe();
        }

        private void SetStatus(string text)
        {
            if (_lblStatus != null) _lblStatus.Text = text;
        }

        // ═══════════════════════════════════════════════════════════════════
        // تعاملِ نقشه
        // ═══════════════════════════════════════════════════════════════════
        private void Map_RegionSelected(object sender, AfghanMapControl.GeoRegionEventArgs e)
        {
            GeoAnalyticsService.RegionStats stats = FindRegion(e.Region);
            if (stats != null) ShowScope(stats);
            else
            {
                _lblSubScope.Text = e.Region + " — بدون پرونده در این محدوده";
                BindTiles(new GeoAnalyticsService.RegionStats { Region = e.Region });
            }

            // همگام‌سازیِ انتخاب با جدولِ رتبه‌بندی.
            for (int i = 0; i < _regions.Count; i++)
                if (string.Equals(_regions[i].Region, e.Region, StringComparison.Ordinal))
                {
                    if (i < _gridRegions.Rows.Count)
                    {
                        bool wasBuilding = _building;
                        _building = true;
                        _gridRegions.ClearSelection();
                        _gridRegions.Rows[i].Selected = true;
                        _gridRegions.FirstDisplayedScrollingRowIndex = i;
                        _building = wasBuilding;
                    }
                    break;
                }
        }

        private void Map_RegionDrillRequested(object sender, AfghanMapControl.GeoRegionEventArgs e)
        {
            if (e.IsDistrictLevel) { BackToCountry(); return; }

            if (AfghanGeoData.GetDistricts(e.Region).Length == 0)
            {
                UiTheme.ShowWarning(this, "فهرست ولسوالی‌های «" + e.Region + "» در سیستم موجود نیست.");
                return;
            }

            _map.DrillTo(e.Region);
            RefreshAsync();
        }

        private void Map_RegionActivated(object sender, AfghanMapControl.GeoRegionEventArgs e)
        {
            GeoFilter f = _filter.Clone();
            if (e.IsDistrictLevel) { f.Province = e.Province; f.District = e.Region; }
            else { f.Province = e.Region; f.District = ""; }
            OpenCasesFor(f);
        }

        private void BackToCountry()
        {
            _map.DrillTo("");
            _btnBack.Visible = false;
            RefreshAsync();
        }

        private GeoAnalyticsService.RegionStats FindRegion(string name)
        {
            foreach (GeoAnalyticsService.RegionStats s in _regions)
                if (string.Equals(s.Region, name, StringComparison.Ordinal)) return s;
            return null;
        }

        private GeoFilter RegionFilter(GeoAnalyticsService.RegionStats stats)
        {
            GeoFilter f = _filter.Clone();
            if (_map.IsDistrictLevel)
            {
                f.Province = string.IsNullOrWhiteSpace(stats.Parent) ? _map.DrilledProvince : stats.Parent;
                f.District = stats.Region;
            }
            else { f.Province = stats.Region; f.District = ""; }
            return f;
        }

        // ─── Drill-Down از تب‌های تفکیک ─────────────────────────────────────
        private void DrillFromBreakdown(string tabTitle, int rowIndex)
        {
            GeoFilter f = _filter.Clone();
            if (_map.IsDistrictLevel) f.Province = _map.DrilledProvince;

            switch (tabTitle)
            {
                case "نوع پرونده":
                    if (!Valid(_requestTypes, rowIndex)) return;
                    f.RequestTypeId = _requestTypes[rowIndex].DrillId;
                    break;

                case "وضعیت خدمات":
                    if (!Valid(_serviceStatuses, rowIndex)) return;
                    f.ServiceStatusId = _serviceStatuses[rowIndex].DrillId;
                    break;

                case "اولویت اقتصادی":
                    if (!Valid(_economic, rowIndex)) return;
                    string econ = _economic[rowIndex].DrillCode;
                    f.EconomicPriority = string.IsNullOrEmpty(econ) ? GeoFilter.EconomicUnset : econ;
                    break;

                case "آسیب‌پذیری":
                    if (!Valid(_vulnerability, rowIndex)) return;
                    f.VulnerabilityBand = _vulnerability[rowIndex].DrillCode;
                    break;

                case "حمایت مالی":
                    if (!Valid(_sponsorship, rowIndex)) return;
                    f.Sponsorship = _sponsorship[rowIndex].DrillCode;
                    break;

                case "تکمیل پرونده":
                    if (!Valid(_completion, rowIndex)) return;
                    // ستونِ کشِ وضعیت تکمیل فیلترِ مستقیمی در FrmCase ندارد؛
                    // نزدیک‌ترین معادلِ موجود، حداقلِ درصد است.
                    string code = _completion[rowIndex].DrillCode;
                    f.MinCompletionPercent = code == "COMPLETE" ? 100 : 0;
                    break;

                default:
                    // تب‌های جمعیت/کمک/مناطق ناشناخته ردیفِ قابلِ Drill ندارند.
                    return;
            }
            OpenCasesFor(f);
        }

        private static bool Valid(List<GeoAnalyticsService.BreakdownRow> rows, int index)
        {
            return rows != null && index >= 0 && index < rows.Count;
        }

        // ═══════════════════════════════════════════════════════════════════
        // اتصال به پرونده‌ها
        // ═══════════════════════════════════════════════════════════════════
        private void OpenCasesFor(GeoFilter filter)
        {
            try
            {
                using (FrmCase frm = new FrmCase(filter))
                    frm.ShowDialog(this);
                RefreshAsync();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در باز کردن فرم پرونده‌ها: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // خروجی‌ها
        // ═══════════════════════════════════════════════════════════════════
        private GeoReportComposer.ReportInput BuildReportInput(Bitmap mapImage)
        {
            return new GeoReportComposer.ReportInput
            {
                Title = "گزارش تحلیلی جغرافیایی پرونده‌ها",
                ScopeLabel = _map.IsDistrictLevel
                           ? "ولایت " + _map.DrilledProvince + " (سطح ولسوالی)"
                           : "کل کشور (سطح ولایت)",
                FilterDescription = _filter.Describe(),
                MetricTitle = GeoAnalyticsService.HeatMetricDisplay(_metric),
                MapImage = mapImage,
                Totals = _totals,
                Regions = _regions,
                RequestTypes = _requestTypes,
                ServiceStatuses = _serviceStatuses,
                EconomicPriorities = _economic,
                Sponsorship = _sponsorship,
                Insights = _insights,
                Assistance = _assistance,
                DistrictLevel = _map.IsDistrictLevel
            };
        }

        private void ExportPdf()
        {
            if (_regions.Count == 0)
            {
                UiTheme.ShowWarning(this, "داده‌ای برای گزارش وجود ندارد.");
                return;
            }

            string posterPath = null;
            try
            {
                UseWaitCursor = true;

                using (Bitmap map = _map.RenderToBitmap(1000, 760))
                {
                    GeoReportComposer.ReportInput input = BuildReportInput(map);
                    ReportDoc doc = GeoReportComposer.BuildDocument(input, out posterPath);

                    if (!ReportDoc.IsPdfPrinterAvailable())
                    {
                        UiTheme.ShowWarning(this,
                            "چاپگر «Microsoft Print to PDF» روی این سیستم پیدا نشد." + Environment.NewLine +
                            "به‌جای آن پیش‌نمایش چاپ باز می‌شود و می‌توانید از همان‌جا خروجی بگیرید.");
                        doc.Preview(this);
                        return;
                    }

                    string outputPath = BuildOutputPath("GeoReports",
                        "گزارش_جغرافیایی_" + ScopeFileTag(), ".pdf");
                    if (outputPath == null) return;

                    if (doc.SaveAsPdf(outputPath))
                    {
                        UiTheme.ShowSuccess(this, "گزارش PDF ساخته شد:" + Environment.NewLine + outputPath);
                        TryOpen(outputPath);
                    }
                    else
                    {
                        UiTheme.ShowError(this, "ساخت PDF ناموفق بود. پیش‌نمایش چاپ را امتحان کنید.");
                    }
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ساخت گزارش PDF: " + ex.Message);
            }
            finally
            {
                UseWaitCursor = false;
                SafeDelete(posterPath);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // گزارشِ چندولایتی — هر ولایت یک صفحه، به‌علاوهٔ صفحهٔ کلی.
        //
        // آموزش — چرا هر ولایت کوئریِ خودش را دارد: آمارِ یک ولایت در سطحِ
        // ولسوالی است (نه یک ردیفِ تجمیع‌شده)، و تفکیک‌هایش هم باید محدود به
        // همان ولایت باشند. پس به‌ازای هر ولایت یک بار سرویس با فیلترِ آن
        // ولایت صدا زده می‌شود. چون این یک عملیاتِ گزارش‌گیری است (نه هر
        // تازه‌سازیِ صفحه)، هزینه‌اش پذیرفتنی است و روی نخِ پس‌زمینه می‌رود.
        // ═══════════════════════════════════════════════════════════════════
        private async void ExportProvincePagesPdf()
        {
            if (_regions.Count == 0)
            {
                UiTheme.ShowWarning(this, "داده‌ای برای گزارش وجود ندارد.");
                return;
            }

            // فهرستِ انتخاب فقط ولایاتی را نشان می‌دهد که واقعاً پرونده دارند.
            List<string> candidates = new List<string>();
            if (_map.IsDistrictLevel) candidates.Add(_map.DrilledProvince);
            else foreach (GeoAnalyticsService.RegionStats s in _regions) candidates.Add(s.Region);

            List<string> chosen = GeoProvincePicker.Choose(this, candidates);
            if (chosen == null || chosen.Count == 0) return;

            string outputPath = BuildOutputPath("GeoReports", "گزارش_ولایتی", ".pdf");
            if (outputPath == null) return;

            List<string> posters = null;
            try
            {
                UseWaitCursor = true;
                SetStatus("در حال ساخت گزارش برای " + ReportDoc.Fa(chosen.Count) + " ولایت…");

                GeoFilter baseFilter = _filter.Clone();
                string metric = _metric;

                // ── نقشهٔ هر ولایت روی نخِ UI ساخته می‌شود ──────────────────
                // AfghanMapControl یک کنترلِ WinForms است و ساخت/رسمش باید
                // روی نخِ UI بماند؛ فقط کوئری‌ها به پس‌زمینه می‌روند.
                List<GeoReportComposer.ReportInput> pages =
                    new List<GeoReportComposer.ReportInput>();
                List<Bitmap> bitmaps = new List<Bitmap>();

                try
                {
                    for (int i = 0; i < chosen.Count; i++)
                    {
                        string province = chosen[i];
                        SetStatus("ولایت " + province + " (" + ReportDoc.Fa(i + 1) +
                                  " از " + ReportDoc.Fa(chosen.Count) + ")…");
                        Application.DoEvents();

                        GeoFilter pf = baseFilter.Clone();
                        pf.Province = province;
                        pf.District = "";

                        ProvinceReportData data = await Task.Run(delegate
                        {
                            return LoadProvinceData(pf);
                        });

                        Bitmap mapImage = RenderProvinceMap(province, data.Regions, metric);
                        bitmaps.Add(mapImage);

                        pages.Add(new GeoReportComposer.ReportInput
                        {
                            Title = "گزارش تحلیلی ولایت " + province,
                            ScopeLabel = "ولایت " + province + " (سطح ولسوالی)",
                            FilterDescription = pf.Describe(),
                            MetricTitle = GeoAnalyticsService.HeatMetricDisplay(metric),
                            PageTag = "ولایت " + ReportDoc.Fa(i + 1) + " از " + ReportDoc.Fa(chosen.Count),
                            MapImage = mapImage,
                            Regions = data.Regions,
                            Totals = GeoAnalyticsService.Aggregate(data.Regions, province),
                            RequestTypes = data.RequestTypes,
                            ServiceStatuses = data.ServiceStatuses,
                            EconomicPriorities = data.Economic,
                            Sponsorship = data.Sponsorship,
                            Assistance = data.Assistance,
                            Insights = GeoInsightEngine.Build(data.Regions, pf, true, province),
                            DistrictLevel = true
                        });
                    }

                    SetStatus("در حال ساخت فایل PDF…");

                    using (Bitmap overview = _map.RenderToBitmap(1400, 1060))
                    {
                        GeoReportComposer.ReportInput summary = BuildReportInput(overview);
                        summary.Title = "گزارش تحلیلی جغرافیایی — خلاصهٔ کل";
                        summary.PageTag = "خلاصهٔ کل (" + ReportDoc.Fa(chosen.Count) + " ولایت در ادامه)";

                        ReportDoc doc = GeoReportComposer.BuildDocument(summary, pages, out posters);

                        if (!ReportDoc.IsPdfPrinterAvailable())
                        {
                            UiTheme.ShowWarning(this,
                                "چاپگر «Microsoft Print to PDF» پیدا نشد؛ پیش‌نمایش چاپ باز می‌شود.");
                            doc.Preview(this);
                            return;
                        }

                        if (doc.SaveAsPdf(outputPath))
                        {
                            UiTheme.ShowSuccess(this,
                                "گزارش ولایتی ساخته شد (" + ReportDoc.Fa(chosen.Count) + " ولایت):" +
                                Environment.NewLine + outputPath);
                            TryOpen(outputPath);
                        }
                        else UiTheme.ShowError(this, "ساخت PDF ناموفق بود.");
                    }
                }
                finally
                {
                    foreach (Bitmap b in bitmaps) b.Dispose();
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ساخت گزارش ولایتی: " + ex.Message);
            }
            finally
            {
                UseWaitCursor = false;
                SetStatus("آماده");
                if (posters != null)
                    foreach (string p in posters) SafeDelete(p);
            }
        }

        private sealed class ProvinceReportData
        {
            public List<GeoAnalyticsService.RegionStats> Regions;
            public List<GeoAnalyticsService.BreakdownRow> RequestTypes, ServiceStatuses,
                                                           Economic, Sponsorship;
            public GeoAnalyticsService.AssistanceStats Assistance;
        }

        private static ProvinceReportData LoadProvinceData(GeoFilter filter)
        {
            return new ProvinceReportData
            {
                Regions = GeoAnalyticsService.GetRegionRollup(
                              filter, GeoAnalyticsService.GeoLevel.District),
                RequestTypes = GeoAnalyticsService.GetRequestTypeBreakdown(filter),
                ServiceStatuses = GeoAnalyticsService.GetServiceStatusBreakdown(filter),
                Economic = GeoAnalyticsService.GetEconomicPriorityBreakdown(filter),
                Sponsorship = GeoAnalyticsService.GetSponsorshipBreakdown(filter),
                Assistance = GeoAnalyticsService.GetAssistanceStats(filter)
            };
        }

        // نقشهٔ ولسوالی‌هایِ یک ولایت، بدونِ دست‌زدن به نقشهٔ روی صفحه.
        private static Bitmap RenderProvinceMap(string province,
                                                List<GeoAnalyticsService.RegionStats> districts,
                                                string metric)
        {
            using (AfghanMapControl temp = new AfghanMapControl())
            {
                Dictionary<string, double> values =
                    new Dictionary<string, double>(StringComparer.Ordinal);
                Dictionary<string, string> tips =
                    new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (GeoAnalyticsService.RegionStats s in districts)
                {
                    values[s.Region] = s.MetricValue(metric);
                    tips[s.Region] = s.Region;
                }

                temp.DrillTo(province);
                temp.SetData(values, tips, GeoAnalyticsService.HeatMetricDisplay(metric), "");
                return temp.RenderToBitmap(1400, 1060);
            }
        }

        private void PrintReport()
        {
            if (_regions.Count == 0)
            {
                UiTheme.ShowWarning(this, "داده‌ای برای چاپ وجود ندارد.");
                return;
            }

            string posterPath = null;
            try
            {
                UseWaitCursor = true;
                using (Bitmap map = _map.RenderToBitmap(1000, 760))
                {
                    ReportDoc doc = GeoReportComposer.BuildDocument(BuildReportInput(map), out posterPath);
                    doc.Preview(this);
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در پیش‌نمایش چاپ: " + ex.Message);
            }
            finally
            {
                UseWaitCursor = false;
                SafeDelete(posterPath);
            }
        }

        private void ExportMapImage()
        {
            try
            {
                string outputPath = BuildOutputPath("GeoReports", "نقشه_" + ScopeFileTag(), ".png");
                if (outputPath == null) return;

                // همان پوسترِ صفحهٔ اولِ گزارش — نقشه و داده کنارِ هم — تا
                // تصویرِ ذخیره‌شده به‌تنهایی هم گویا باشد.
                using (Bitmap map = _map.RenderToBitmap(1000, 760))
                using (Bitmap poster = GeoReportComposer.BuildPoster(BuildReportInput(map)))
                    poster.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);

                UiTheme.ShowSuccess(this, "تصویر گزارش ذخیره شد:" + Environment.NewLine + outputPath);
                TryOpen(outputPath);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ساخت تصویر: " + ex.Message);
            }
        }

        private void ExportExcel()
        {
            try
            {
                string outputPath = BuildOutputPath("ExcelReports",
                    "آمار_جغرافیایی_" + ScopeFileTag(), ".xlsx");
                if (outputPath == null) return;

                using (XLWorkbook wb = new XLWorkbook())
                {
                    AddSheet(wb, "مناطق", GeoReportComposer.RegionsToTable(
                        _regions, _map.IsDistrictLevel ? "ولسوالی" : "ولایت"));
                    AddSheet(wb, "نوع پرونده", GeoAnalyticsService.ToDataTable(_requestTypes, "نوع پرونده"));
                    AddSheet(wb, "وضعیت خدمات", GeoAnalyticsService.ToDataTable(_serviceStatuses, "وضعیت خدمات"));
                    AddSheet(wb, "اولویت اقتصادی", GeoAnalyticsService.ToDataTable(_economic, "اولویت اقتصادی"));
                    AddSheet(wb, "آسیب‌پذیری", GeoAnalyticsService.ToDataTable(_vulnerability, "سطح آسیب‌پذیری"));
                    AddSheet(wb, "حمایت مالی", GeoAnalyticsService.ToDataTable(_sponsorship, "وضعیت حمایت"));
                    AddSheet(wb, "جمعیت و سن", _ageMatrix);
                    AddSheet(wb, "مناطق ناشناخته", _unknownRegions);
                    AddInsightSheet(wb);

                    wb.SaveAs(outputPath);
                }

                UiTheme.ShowSuccess(this, "خروجی اکسل ذخیره شد:" + Environment.NewLine + outputPath);
                TryOpen(outputPath);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ساخت خروجی اکسل: " + ex.Message);
            }
        }

        private static void AddSheet(XLWorkbook wb, string name, DataTable data)
        {
            // نامِ شیت در اکسل حداکثر ۳۱ نویسه و بدونِ چند نویسهٔ خاص است.
            IXLWorksheet ws = wb.Worksheets.Add(SafeSheetName(name));
            ws.RightToLeft = true;

            if (data == null || data.Rows.Count == 0)
            {
                ws.Cell(1, 1).Value = "داده‌ای برای نمایش وجود ندارد.";
                return;
            }

            IXLTable table = ws.Cell(1, 1).InsertTable(data, "T_" + Math.Abs(name.GetHashCode()), true);
            table.Theme = XLTableTheme.TableStyleMedium2;
            ws.Columns().AdjustToContents();
        }

        private void AddInsightSheet(XLWorkbook wb)
        {
            IXLWorksheet ws = wb.Worksheets.Add("هوش مدیریتی");
            ws.RightToLeft = true;
            ws.Cell(1, 1).Value = "یافته";
            ws.Cell(1, 2).Value = "اهمیت";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 2).Style.Font.Bold = true;

            int row = 2;
            foreach (GeoInsightEngine.Insight ins in _insights)
            {
                ws.Cell(row, 1).Value = ins.Text;
                ws.Cell(row, 2).Value = ToneName(ins.Tone);
                row++;
            }
            ws.Columns().AdjustToContents();
            ws.Column(1).Width = 90;
        }

        private static string ToneName(GeoInsightEngine.InsightTone tone)
        {
            switch (tone)
            {
                case GeoInsightEngine.InsightTone.Critical: return "بحرانی";
                case GeoInsightEngine.InsightTone.Warning: return "هشدار";
                case GeoInsightEngine.InsightTone.Positive: return "مثبت";
                default: return "اطلاعی";
            }
        }

        private static string SafeSheetName(string name)
        {
            string clean = name;
            foreach (char c in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                clean = clean.Replace(c, ' ');
            return clean.Length > 31 ? clean.Substring(0, 31) : clean;
        }

        private string ScopeFileTag()
        {
            return _map.IsDistrictLevel ? _map.DrilledProvince : "کل_کشور";
        }

        private string BuildOutputPath(string subFolder, string baseName, string extension)
        {
            string root = FileHelper.GetOrChooseBaseRootFolder();
            if (string.IsNullOrWhiteSpace(root))
            {
                UiTheme.ShowWarning(this, "محل ذخیرهٔ فایل‌ها مشخص نیست.");
                return null;
            }

            string folder = Path.Combine(root, subFolder);
            Directory.CreateDirectory(folder);

            string safe = FileHelper.CleanName(baseName);
            return Path.Combine(folder, safe + "_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + extension);
        }

        private void TryOpen(string path)
        {
            try
            {
                if (UiTheme.SuppressDialogs) return;   // در حالتِ آزمون چیزی باز نشود
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch { /* بازنشدنِ فایل نباید خروجیِ موفق را خطا نشان دهد */ }
        }

        private static void SafeDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
            catch { }
        }

        // ─── کمکی‌های قالب‌بندی ─────────────────────────────────────────────
        private static string Fa(int value)
        {
            return ReportDoc.Fa(value.ToString("#,0", CultureInfo.InvariantCulture));
        }

        private static string Money(decimal value)
        {
            return ReportDoc.Fa(value.ToString("#,0", CultureInfo.InvariantCulture));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _loadCts != null)
            {
                _loadCts.Cancel();
                _loadCts.Dispose();
                _loadCts = null;
            }
            base.Dispose(disposing);
        }

        // ═══════════════════════════════════════════════════════════════════
        // آیتمِ کمبو با مقدارِ پنهان (شناسه یا کد) و متنِ نمایشی.
        // ═══════════════════════════════════════════════════════════════════
        private sealed class ComboItem
        {
            public readonly object Value;
            private readonly string _text;

            public ComboItem(object value, string text)
            {
                Value = value;
                _text = text ?? "";
            }

            public override string ToString() { return _text; }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GeoProvincePicker — انتخابِ چند ولایت برای گزارشِ چندصفحه‌ای.
    //
    // آموزش — چرا CheckedListBox و نه کمبویِ چندانتخابی: مدیر باید هم‌زمان
    // ببیند کدام‌ها تیک خورده‌اند و بتواند سریع «همه» یا «هیچ» کند. با ۳۴
    // ولایت، کمبو کارِ مقایسه را سخت می‌کند.
    // ═══════════════════════════════════════════════════════════════════════
    internal sealed class GeoProvincePicker : Form
    {
        private readonly CheckedListBox _list;

        private GeoProvincePicker(IList<string> provinces)
        {
            Text = "انتخاب ولایات گزارش";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(380, 460);

            Label hint = new Label
            {
                Text = "برای هر ولایتِ تیک‌خورده یک صفحهٔ جداگانه با نقشه و آمارش ساخته می‌شود.",
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(10, 8, 10, 4),
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.Font(8.75f),
                TextAlign = ContentAlignment.MiddleRight
            };

            _list = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                CheckOnClick = true,
                Font = UiTheme.Font(9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            foreach (string p in provinces) _list.Items.Add(p, true);

            Panel buttons = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = UiTheme.CardBack };

            Button ok = UiTheme.CreateButton("ساخت گزارش", "✔", UiTheme.Primary);
            ok.Size = new Size(130, 34);
            ok.Location = new Point(14, 7);
            ok.DialogResult = DialogResult.OK;

            Button cancel = UiTheme.CreateSecondaryButton("انصراف", "✕");
            cancel.Size = new Size(100, 34);
            cancel.Location = new Point(152, 7);
            cancel.DialogResult = DialogResult.Cancel;

            // «همه / هیچ» یک دکمهٔ رفت‌وبرگشتی است تا نوار شلوغ نشود.
            Button all = UiTheme.CreateSecondaryButton("همه / هیچ", "☑");
            all.Size = new Size(96, 34);
            all.Location = new Point(262, 7);
            all.Click += delegate { ToggleAll(); };

            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(all);

            Controls.Add(_list);
            Controls.Add(hint);
            Controls.Add(buttons);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        // اگر همه تیک‌خورده باشند همه را برمی‌دارد، وگرنه همه را تیک می‌زند.
        private void ToggleAll()
        {
            bool allChecked = _list.CheckedItems.Count == _list.Items.Count;
            for (int i = 0; i < _list.Items.Count; i++)
                _list.SetItemChecked(i, !allChecked);
        }

        // null یعنی کاربر انصراف داد.
        public static List<string> Choose(IWin32Window owner, IList<string> provinces)
        {
            if (provinces == null || provinces.Count == 0) return null;

            // در حالتِ آزمون هیچ دیالوگی نباید باز شود (قاعدهٔ UiTheme.SuppressDialogs).
            if (UiTheme.SuppressDialogs) return new List<string>(provinces);

            using (GeoProvincePicker dlg = new GeoProvincePicker(provinces))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;

                List<string> chosen = new List<string>();
                foreach (object item in dlg._list.CheckedItems)
                    chosen.Add(Convert.ToString(item));
                return chosen;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // StatTile — کارتِ عددیِ کوچکِ بالای پنلِ داده.
    //
    // آموزش — چرا کنترلِ اختصاصی و نه Label: کارت باید نوارِ رنگیِ بالا،
    // عددِ درشت و برچسبِ کوچک را با هم داشته باشد و در تغییرِ اندازه به‌هم
    // نریزد. یک OnPaint ساده از چیدنِ سه کنترلِ تودرتو هم سبک‌تر است هم
    // تمیزتر رسم می‌شود.
    // ═══════════════════════════════════════════════════════════════════════
    internal sealed class StatTile : Control
    {
        private readonly Color _accent;
        private string _value = "—";

        public StatTile(string caption, Color accent)
        {
            Text = caption;
            _accent = accent;
            BackColor = UiTheme.CardBack;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public string Value
        {
            get { return _value; }
            set { _value = value ?? "—"; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (Brush stripe = new SolidBrush(_accent))
                g.FillRectangle(stripe, 0, 0, Width, 3);
            using (Pen p = new Pen(UiTheme.Border))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

            using (StringFormat sf = new StringFormat(StringFormatFlags.DirectionRightToLeft))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;

                // اندازهٔ عدد با عرضِ واقعیِ کارت تطبیق داده می‌شود (نه با
                // حدسِ طولِ رشته): «مجموع کمک‌ها» می‌تواند ده رقم باشد و
                // بریده‌شدنش عددِ گمراه‌کننده نشان می‌دهد.
                using (Font valueFont = FitFont(g, _value, Width - 10))
                using (Brush ink = new SolidBrush(UiTheme.TextDark))
                    g.DrawString(_value, valueFont, ink,
                        new RectangleF(4, 8, Width - 8, Height - 30), sf);

                using (Font capFont = UiTheme.Font(8.25f))
                using (Brush muted = new SolidBrush(UiTheme.TextMuted))
                    g.DrawString(Text, capFont, muted,
                        new RectangleF(4, Height - 22, Width - 8, 18), sf);
            }
        }

        // بزرگ‌ترین اندازه‌ای که عدد در عرضِ کارت جا می‌شود.
        private static Font FitFont(Graphics g, string text, float maxWidth)
        {
            for (float size = 17f; size > 9f; size -= 0.5f)
            {
                Font candidate = UiTheme.FontBold(size);
                if (g.MeasureString(text, candidate).Width <= maxWidth) return candidate;
                candidate.Dispose();
            }
            return UiTheme.FontBold(9f);
        }
    }
}

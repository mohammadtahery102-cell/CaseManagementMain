using CaseManagement.DAL;
using CaseManagement.Helpers;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace CaseManagement
{
    public class FrmDashboard : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        private FlowLayoutPanel summaryPanel;
        private Chart statusChart;
        private Chart trendChart;
        private Chart geoChart;
        private DataGridView dgvCritical;
        private DataGridView dgvFamily;
        private DataGridView dgvGeo;
        private DataGridView dgvReminders;
        private DataGridView dgvCustomReminders;
        private System.Windows.Forms.Timer _reminderTimer;
        private DataGridView dgvQuality;
        private DataGridView dgvAudit;
        private FlowLayoutPanel familyCategoryTilesPanel;
        private FlowLayoutPanel familyMembersSummaryPanel;
        private FlowLayoutPanel notificationsPanel;
        private ComboBox cmbCriticalFilter;
        private ComboBox cmbTrendMode;
        private ComboBox cmbFamilyServiceStatus;
        private TextBox txtDistrictFilter;

        // «» = بدون فیلتر (همه وضعیت‌ها) — به درخواست کاربر تمام آمار/کارت‌های
        // تب «اعضای خانواده» بر اساس همین مقدار (ServiceStatus سطح پرونده) فیلتر می‌شوند.
        private string _familyServiceStatusFilter = "";
        private Label lblFamilySummary;
        private Label lblReminderSummary;
        private Label lblQualitySummary;

        // ComboBox برای تغییر مرکز توسط SuperAdmin (بدون نیاز به logout)
        private ComboBox _cmbCenterSwitch;

        // ─── فیلتر ولایت/ولسوالی روی کل داشبورد ─────────────────────────────
        // آموزش: این دو مقدار به همه‌ی کوئری‌های پرونده‌محورِ داشبورد تزریق می‌شوند
        // (CaseFilterSql/AddCaseFilterParams). وقتی خالی‌اند، شرط «(@Prov=''...)»
        // همیشه درست است، پس رفتار دقیقاً مثل قبل می‌ماند (بدون هیچ تغییر/رگرسیون).
        private ComboBox _cmbFilterProvince;
        private ComboBox _cmbFilterDistrict;
        private string _filterProvince = "";
        private string _filterDistrict = "";

        public FrmDashboard()
        {
            BuildUi();
            StartReminderTimer();
        }

        private void BuildUi()
        {
            Text = "داشبورد مدیریتی";
            RightToLeft = RightToLeft.Yes;
            // آموزش — به درخواست کاربر: آینه‌ی «فرم» خاموش شد تا دکمه‌های بستن/
            // کوچک نوار عنوان به سمت راست (استاندارد) بروند. اما آینه‌ی TabControl
            // روشن می‌ماند (پایین‌تر) تا محتوای همه‌ی تب‌ها بدون تغییر و درست RTL
            // بماند؛ فقط نوار ابزار و بنر بالای فرم دستی راست‌چین می‌شوند.
            RightToLeftLayout = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(9.5F);
            // عرض کمی افزایش یافت (۱۲۰۰→۱۲۶۰) تا با دکمه‌های فشرده‌شده،
            // نوار ابزار در یک ردیف کامل جا شود.
            UiTheme.MakeFixedSize(this, 1260, 730);

            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 56;
            toolbar.BackColor = UiTheme.PrimaryDark;

            FlowLayoutPanel toolButtons = new FlowLayoutPanel();
            toolButtons.Dock = DockStyle.Fill;
            // آموزش — رفع باگ «آینه دوبل»: فرم از قبل RightToLeft=Yes دارد که
            // به این پنل ارث می‌رسد. اگر همزمان FlowDirection هم RightToLeft
            // گذاشته شود، دو آینه با هم خنثی می‌شوند و نتیجه دوباره چپ‌به‌راست
            // می‌شود (دقیقاً همان چیزی که در اسکرین‌شات دیده شد — دکمه‌ها به
            // لبه راست نمی‌رسیدند). LeftToRight همراه با RightToLeft ارثی
            // فرم، دقیقاً یک‌بار آینه می‌شود و ترتیب/چیدمان درست از راست
            // شروع می‌شود.
            // آموزش — جریانِ دکمه‌ها با «RightToLeft=Yes» ارثیِ فرم (که هنوز روشن
            // است) آینه می‌شود؛ پس LeftToRight درست است تا دکمه‌ها از راست شروع
            // شوند. (این ربطی به RightToLeftLayout هندسی ندارد که خاموش شد.)
            toolButtons.FlowDirection = FlowDirection.LeftToRight;
            toolButtons.WrapContents = false;
            toolButtons.Padding = new Padding(8, 6, 8, 6);
            toolButtons.AutoSize = false;

            // آموزش — بازآرایی نوار ابزار به درخواست کاربر:
            // «دریافت اکسل» حذف شد (دیگر استفاده نمی‌شود)، «تازه‌سازی» کنار
            // لوگو منتقل شد، «درباره برنامه» حذف شد (با کلیک روی لوگو باز
            // می‌شود)، و «کاربران» به انتهای نوار منتقل شد.
            toolButtons.Controls.Add(CreateToolButton("پرونده‌ها", "▤", delegate { using (var frm = new FrmCase()) frm.ShowDialog(this); RefreshAll(); }));
            toolButtons.Controls.Add(CreateToolButton("متقاضیان", "✎", delegate { using (var frm = new FrmApplicant()) frm.ShowDialog(this); RefreshAll(); }));
            toolButtons.Controls.Add(CreateToolButton("جستجوی پیشرفته", "⌕", delegate { using (var frm = new FrmAdvancedSearch()) frm.ShowDialog(this); }));
            toolButtons.Controls.Add(CreateToolButton("مالی", "$", delegate { using (var frm = new FrmFinance()) frm.ShowDialog(this); RefreshAll(); }));
            toolButtons.Controls.Add(CreateToolButton("حسابداری ایتام", "💰", delegate { using (var frm = new CaseManagement.Accounting.FrmAccounting()) frm.ShowDialog(this); }));
            toolButtons.Controls.Add(CreateToolButton("همگام‌سازی", "🔄", delegate { using (var frm = new CaseManagement.Sync.FrmSyncWizard()) frm.ShowDialog(this); RefreshAll(); }));
            toolButtons.Controls.Add(CreateToolButton("تنظیمات", "⚙", OpenSettings));
            toolButtons.Controls.Add(CreateToolButton("جزوه آموزشی", "📘", OpenTrainingManual));
            toolButtons.Controls.Add(CreateToolButton("ارتباط با ما", "☎", OpenContactUs));
            toolButtons.Controls.Add(CreateToolButton("کاربران", "☺", OpenUsers));
            toolButtons.Controls.Add(CreateToolButton("خروج از حساب", "⎋", delegate { LogoutCurrentUser(); }));

            // ─── پانل اطلاعات کاربر + مرکز (سمت چپِ نوار) ─────────────────
            Panel userPanel = new Panel();
            userPanel.Dock      = DockStyle.Left;
            userPanel.Width     = 165; // کوچک‌تر شد تا combo/کاربر روی دکمه‌های toolbar نیفتد
            userPanel.BackColor = Color.Transparent;

            Label lblUser = new Label();
            lblUser.Text      = SecurityContext.Username + "  /  " + UiTheme.RoleDisplay(SecurityContext.Role);
            lblUser.ForeColor = Color.White;
            lblUser.Font      = UiTheme.FontBold(9.5F);
            lblUser.AutoSize  = false;
            lblUser.Dock      = DockStyle.Top;
            lblUser.Height    = 28;
            lblUser.TextAlign = ContentAlignment.BottomCenter;

            if (SecurityContext.IsSuperAdmin())
            {
                // SuperAdmin: ComboBox برای تغییر مرکز
                _cmbCenterSwitch = new ComboBox();
                _cmbCenterSwitch.DropDownStyle = ComboBoxStyle.DropDownList;
                _cmbCenterSwitch.Font = UiTheme.Font(9.5F);
                _cmbCenterSwitch.Dock = DockStyle.Top;
                _cmbCenterSwitch.Height = 26;
                LoadCenterSwitcher();
                _cmbCenterSwitch.SelectedIndexChanged += CmbCenterSwitch_Changed;

                userPanel.Controls.Add(_cmbCenterSwitch);
                userPanel.Controls.Add(lblUser);
            }
            else
            {
                // کاربر عادی: Label نام مرکز
                Label lblCenter = new Label();
                lblCenter.Text      = "◉  " + SecurityContext.CenterDisplay;
                lblCenter.ForeColor = Color.FromArgb(200, 240, 255);
                lblCenter.Font      = UiTheme.Font(9.5F);
                lblCenter.AutoSize  = false;
                lblCenter.Dock      = DockStyle.Top;
                lblCenter.Height    = 26;
                lblCenter.TextAlign = ContentAlignment.TopCenter;

                userPanel.Controls.Add(lblCenter);
                userPanel.Controls.Add(lblUser);
            }

            // آموزش — ترتیب مهم است: پنل کاربر (Dock=Left) باید «اول» اضافه شود تا
            // فضایش را از چپ بگیرد و سپس toolButtons (Dock=Fill) بقیه را پر کند؛
            // در غیر این صورت Fill کل نوار را می‌گیرد و پنل کاربر روی دکمه‌ها می‌افتد
            // (باگ «منو زیر combo رفت»).
            toolbar.Controls.Add(userPanel);
            toolbar.Controls.Add(toolButtons);

            // ─── بنر عنوان بزرگ + لوگو (بندهای ۳ و ۹ بازطراحی ظاهری) ──────
            // آموزش — ارتفاع از ۸۴ به ۱۱۲ افزایش یافت تا خط کوچک «حدیث روز»
            // هم در همین نوار سربرگ، نزدیک لوگو، جا شود (به درخواست کاربر).
            Panel titleBanner = new Panel();
            titleBanner.Dock = DockStyle.Top;
            titleBanner.Height = 112;
            titleBanner.BackColor = UiTheme.CardBack;

            // آموزش — اثر RightToLeftLayout=true: دستگاه مختصات و ترازها آینه
            // می‌شوند. برای همین ContentAlignment.MiddleRight به‌صورت بصری «چپ»
            // رندر می‌شد و عنوان به گوشه چپ می‌افتاد. برای نمایش عنوان و لوگو
            // در سمت راست بصری، از تراز/لنگر «چپِ منطقی» استفاده می‌کنیم که پس
            // از آینه‌شدن به راست بصری تبدیل می‌شود.
            // آموزش — به درخواست کاربر: دکمه «درباره برنامه» از نوار ابزار حذف
            // شد؛ حالا با کلیک روی همین لوگو باز می‌شود (Cursor=Hand برای نشان
            // دادن قابل‌کلیک‌بودن).
            // آموزش — چون فرم دیگر آینه‌ی هندسی ندارد، لوگو و دکمه‌ی تازه‌سازی در
            // یک پنلِ Dock=Right قرار می‌گیرند تا قطعاً و پایدار در سمت راست بنر
            // بمانند (بدون وابستگی به مختصات مطلق/آینه).
            Panel logoArea = new Panel();
            logoArea.Dock = DockStyle.Right;
            logoArea.Width = 100;
            logoArea.BackColor = UiTheme.CardBack;

            PictureBox picBannerLogo = new PictureBox();
            picBannerLogo.Image = LogoHelper.GetLogoImage();
            picBannerLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picBannerLogo.Size = new Size(72, 72);
            picBannerLogo.Location = new Point(14, 6);
            picBannerLogo.Cursor = Cursors.Hand;
            picBannerLogo.Click += delegate { using (var frm = new FrmAbout()) frm.ShowDialog(this); };
            logoArea.Controls.Add(picBannerLogo);

            // دکمه «تازه‌سازی» گرد، زیر لوگو.
            Button btnRefreshNearLogo = new Button();
            btnRefreshNearLogo.Text = "↻";
            btnRefreshNearLogo.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            btnRefreshNearLogo.Size = new Size(34, 34);
            btnRefreshNearLogo.Location = new Point(33, 78);
            btnRefreshNearLogo.FlatStyle = FlatStyle.Flat;
            btnRefreshNearLogo.FlatAppearance.BorderSize = 0;
            btnRefreshNearLogo.BackColor = UiTheme.Primary;
            btnRefreshNearLogo.ForeColor = Color.White;
            btnRefreshNearLogo.Cursor = Cursors.Hand;
            UiTheme.RoundCorners(btnRefreshNearLogo, 34);
            var refreshTip = new ToolTip();
            refreshTip.SetToolTip(btnRefreshNearLogo, "تازه‌سازی");
            btnRefreshNearLogo.Click += delegate { RefreshAll(); };
            logoArea.Controls.Add(btnRefreshNearLogo);

            titleBanner.Controls.Add(logoArea);

            // آموزش — رفع «چند کلمه‌ی حدیث گم می‌شود»: علت واقعی این بود که حدیث
            // در یک ردیفِ باریک (فقط ۲۴px، تک‌خط، فشرده بین عنوان و لوگو) جا داده
            // شده بود؛ برای جمله‌های بلندتر جا کم می‌آمد و بخشی از متن قطع
            // می‌شد. به‌درخواست کاربر، حدیث کاملاً از بنر جدا و در یک نوارِ
            // پهن و وسط‌چین (تمام عرض داشبورد) زیر بنر عنوان قرار گرفت؛ اینجا
            // فضای کافی برای دوخطی‌شدن و نمایش کامل جمله وجود دارد.
            Panel bannerTextArea = new Panel();
            bannerTextArea.Dock = DockStyle.Fill;
            bannerTextArea.Padding = new Padding(14, 8, 14, 8);

            Label lblBannerTitle = new Label();
            lblBannerTitle.Text = "داشبورد مدیریتی";
            lblBannerTitle.Font = UiTheme.FontBold(UiTheme.SizeTitle);
            lblBannerTitle.ForeColor = UiTheme.PrimaryDark;
            lblBannerTitle.Dock = DockStyle.Fill;
            // تراز متن با RightToLeft=Yes ارثی آینه می‌شود؛ MiddleLeft یعنی راستِ بصری.
            lblBannerTitle.TextAlign = ContentAlignment.MiddleLeft;
            bannerTextArea.Controls.Add(lblBannerTitle);
            titleBanner.Controls.Add(bannerTextArea); // Fill — سمت چپِ logoArea قرار می‌گیرد

            // ─── نوار حدیث روز — پهن، تمام‌عرض، وسط‌چین (زیر بنر عنوان) ──────
            Panel hadithBar = BuildHadithBar();

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = UiTheme.FontBold(10F);
            // آموزش — RightToLeftLayout ارث‌بری نمی‌شود: روی فرم true است اما
            // TabControl خودش پیش‌فرض false دارد، پس نوار تب‌ها از چپ شروع
            // می‌شد. با تنظیم مستقیم این دو خاصیت، تب index=0 (داشبورد) به سمت
            // راست منتقل می‌شود و ترتیب تب‌ها راست‌به‌چپ می‌شود.
            tabs.RightToLeft = RightToLeft.Yes;
            tabs.RightToLeftLayout = true;

            // ترتیب تب‌ها (RTL): تب index=0 در سمت راست نمایش داده می‌شود.
            // آموزش — به درخواست کاربر: «داشبورد کل پرونده‌ها» دوباره تب اول/
            // پیش‌فرض شد، و تب «تحلیل خانواده» حذف گردید.
            tabs.TabPages.Add(BuildSummaryTab());            // داشبورد کل پرونده‌ها (پیش‌فرض)
            tabs.TabPages.Add(BuildFamilyMembersStatsTab()); // اعضای خانواده
            tabs.TabPages.Add(BuildNotificationsTab());      // اعلان‌ها
            tabs.TabPages.Add(BuildTrendTab());              // روند زمانی
            tabs.TabPages.Add(BuildCriticalTab());           // وضعیت‌های بحرانی
            tabs.TabPages.Add(BuildGeographyTab());          // جغرافیا
            tabs.TabPages.Add(BuildReminderTab());           // یادآوری سروی
            tabs.TabPages.Add(BuildQualityTab());            // کیفیت داده
            tabs.TabPages.Add(BuildAuditTab());              // گزارش رویدادها

            tabs.SelectedIndex = 0; // پیش‌فرض روی «اعضای خانواده»

            Controls.Add(tabs);
            Controls.Add(BuildFilterBar());
            Controls.Add(hadithBar);
            Controls.Add(titleBanner);
            Controls.Add(toolbar);

            RefreshAll();
        }

        // نوار حدیث روز: تمام‌عرض داشبورد، وسط‌چین، با ارتفاع کافی برای دوخطی
        // شدن (متن اصلی + منبع) تا هرگز جمله بریده/گم نشود.
        private Panel BuildHadithBar()
        {
            HadithProvider.DailyItem daily = HadithProvider.GetRandom();
            string hadithText = (daily.Text ?? "").Trim().TrimEnd('.', '۔', '؛', ' ');

            Panel bar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.HoverTint, Padding = new Padding(20, 4, 20, 4) };

            TableLayoutPanel stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 16F));

            Label lblHadithText = new Label
            {
                Text = hadithText, Dock = DockStyle.Fill,
                Font = UiTheme.FontBold(9.5F), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = false
            };
            Label lblHadithSource = new Label
            {
                Text = daily.Source, Dock = DockStyle.Fill,
                Font = new Font(UiTheme.Font(8F), FontStyle.Italic), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.TopCenter, AutoEllipsis = true
            };

            stack.Controls.Add(lblHadithText, 0, 0);
            stack.Controls.Add(lblHadithSource, 0, 1);
            bar.Controls.Add(stack);
            return bar;
        }

        // ─── نوار فیلتر ولایت/ولسوالی (زیر بنر، بالای تب‌ها) ─────────────────
        private Panel BuildFilterBar()
        {
            Panel bar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 7, 12, 6)
            };

            Label lbl = new Label
            {
                Text = "فیلتر:", AutoSize = false, Width = 46, Height = 30,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeBody),
                ForeColor = UiTheme.TextDark, Margin = new Padding(2, 4, 2, 2)
            };
            flow.Controls.Add(lbl);

            _cmbFilterProvince = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, Width = 170, Height = 30,
                Font = UiTheme.Font(UiTheme.SizeBody), Margin = new Padding(4, 3, 4, 2)
            };
            _cmbFilterProvince.Items.Add("همه ولایت‌ها");
            try
            {
                foreach (string prov in Helpers.LookupHelper.GetValues("Province"))
                    _cmbFilterProvince.Items.Add(prov);
            }
            catch { }
            _cmbFilterProvince.SelectedIndex = 0;
            // با تغییر ولایت، فهرست ولسوالی‌های همان ولایت در کمبوی بعدی پر می‌شود.
            _cmbFilterProvince.SelectedIndexChanged += delegate { LoadFilterDistricts(); };
            flow.Controls.Add(_cmbFilterProvince);

            _cmbFilterDistrict = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Height = 30,
                Font = UiTheme.Font(UiTheme.SizeBody), Margin = new Padding(4, 3, 4, 2)
            };
            var districtTip = new ToolTip();
            districtTip.SetToolTip(_cmbFilterDistrict, "ولسوالی (وابسته به ولایت انتخاب‌شده)");
            flow.Controls.Add(_cmbFilterDistrict);
            LoadFilterDistricts();

            Button btnApply = UiTheme.CreateButton("اعمال فیلتر", "⌕", UiTheme.Primary);
            btnApply.Size = new Size(130, 30); btnApply.Margin = new Padding(4, 3, 4, 2);
            btnApply.Click += delegate { ApplyDashboardFilter(); };
            flow.Controls.Add(btnApply);

            Button btnClear = UiTheme.CreateSecondaryButton("حذف فیلتر", "✕");
            btnClear.Size = new Size(120, 30); btnClear.Margin = new Padding(4, 3, 4, 2);
            btnClear.Click += delegate
            {
                _cmbFilterProvince.SelectedIndex = 0;
                LoadFilterDistricts();
                ApplyDashboardFilter();
            };
            flow.Controls.Add(btnClear);

            bar.Controls.Add(flow);
            return bar;
        }

        // پر کردن کمبوی ولسوالی بر اساس ولایت انتخاب‌شده (وابسته/آبشاری).
        private void LoadFilterDistricts()
        {
            if (_cmbFilterDistrict == null) return;
            _cmbFilterDistrict.Items.Clear();
            _cmbFilterDistrict.Items.Add("همه ولسوالی‌ها");

            if (_cmbFilterProvince.SelectedIndex > 0)
            {
                try
                {
                    foreach (string d in Helpers.AfghanGeoData.GetDistricts(_cmbFilterProvince.Text.Trim()))
                        _cmbFilterDistrict.Items.Add(d);
                }
                catch { }
            }
            _cmbFilterDistrict.SelectedIndex = 0;
        }

        // خروج از حساب فعلی: برنامه ری‌استارت می‌شود و صفحه‌ی ورود دوباره می‌آید،
        // پس کاربر دیگری می‌تواند وارد شود (بدون بستن دستیِ کل برنامه).
        private void LogoutCurrentUser()
        {
            if (!UiTheme.ShowConfirm(this, "از حساب فعلی خارج می‌شوید؟ برنامه به صفحه‌ی ورود بازمی‌گردد.", "خروج از حساب"))
                return;
            Application.Restart();
        }

        private void ApplyDashboardFilter()
        {
            _filterProvince = (_cmbFilterProvince.SelectedIndex <= 0) ? "" : _cmbFilterProvince.Text.Trim();
            _filterDistrict = (_cmbFilterDistrict.SelectedIndex <= 0) ? "" : _cmbFilterDistrict.Text.Trim();
            RefreshAll();
        }

        // شرط SQL فیلتر پرونده برای یک alias مشخص از TblCase (خالی = بی‌اثر).
        // alias خالی یعنی ستون‌ها بدون پیشوند (مثل «FROM TblCase» بدون نام مستعار).
        private string CaseFilterSql(string alias)
        {
            string a = string.IsNullOrEmpty(alias) ? "" : alias + ".";
            return " AND (@Prov = '' OR " + a + "Province = @Prov)" +
                   " AND (@Dist = '' OR " + a + "District LIKE '%' || @Dist || '%')";
        }

        // افزودن پارامترهای فیلتر به یک Command (یک‌بار در هر Command کافی است،
        // حتی اگر شرط چند بار در کوئری تکرار شده باشد).
        private void AddCaseFilterParams(SQLiteCommand cmd)
        {
            cmd.Parameters.AddWithValue("@Prov", _filterProvince ?? "");
            cmd.Parameters.AddWithValue("@Dist", _filterDistrict ?? "");
        }

        private TabPage BuildSummaryTab()
        {
            TabPage page = new TabPage("داشبورد کل پرونده‌ها");
            page.BackColor = UiTheme.Background;

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 250;

            summaryPanel = new FlowLayoutPanel();
            summaryPanel.Dock = DockStyle.Fill;
            summaryPanel.BackColor = UiTheme.Background;
            summaryPanel.Padding = new Padding(12);
            summaryPanel.AutoScroll = false;
            summaryPanel.WrapContents = true;

            statusChart = CreateChart("وضعیت پرونده‌ها", SeriesChartType.Pie);

            split.Panel1.Controls.Add(summaryPanel);
            split.Panel2.Controls.Add(statusChart);
            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildTrendTab()
        {
            TabPage page = new TabPage("روند زمانی");
            page.BackColor = UiTheme.Background;

            Panel panel = new Panel();
            panel.Dock = DockStyle.Top;
            panel.Height = 50;
            panel.BackColor = UiTheme.CardBack;

            cmbTrendMode = new ComboBox();
            cmbTrendMode.Font = UiTheme.Font(9.5F);
            cmbTrendMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTrendMode.Items.AddRange(new object[] { "روزانه", "ماهانه" });
            cmbTrendMode.SelectedIndex = 0;
            cmbTrendMode.SetBounds(20, 12, 120, 28);
            cmbTrendMode.SelectedIndexChanged += delegate { LoadTrend(); };

            panel.Controls.Add(CreateLabel("نمایش", 150, 13, 60));
            panel.Controls.Add(cmbTrendMode);

            trendChart = CreateChart("روند ثبت و تغییر وضعیت", SeriesChartType.Line);
            trendChart.Dock = DockStyle.Fill;

            page.Controls.Add(trendChart);
            page.Controls.Add(panel);
            return page;
        }

        private TabPage BuildCriticalTab()
        {
            TabPage page = new TabPage("وضعیت‌های بحرانی");
            page.BackColor = UiTheme.Background;

            Panel panel = new Panel();
            panel.Dock = DockStyle.Top;
            panel.Height = 50;
            panel.BackColor = UiTheme.CardBack;

            cmbCriticalFilter = new ComboBox();
            cmbCriticalFilter.Font = UiTheme.Font(9.5F);
            cmbCriticalFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCriticalFilter.Items.AddRange(new object[] { "وضعیت فوری", "بدون عکس", "بدون سند", "بدون شماره تماس" });
            cmbCriticalFilter.SelectedIndex = 0;
            cmbCriticalFilter.SetBounds(20, 12, 160, 28);
            cmbCriticalFilter.SelectedIndexChanged += delegate { LoadCritical(); };

            panel.Controls.Add(CreateLabel("فیلتر", 190, 13, 60));
            panel.Controls.Add(cmbCriticalFilter);

            // آموزش — به درخواست کاربر: خروجی اکسل دقیقاً بر اساس همان
            // اطلاعاتی است که در حال حاضر (پس از اعمال فیلتر) در dgvCritical
            // نمایش داده می‌شود، نه یک کوئری جدا.
            Button btnExportCritical = UiTheme.CreateButton("خروجی اکسل", "⇑", UiTheme.Primary);
            btnExportCritical.SetBounds(270, 9, 120, 32);
            btnExportCritical.Click += delegate { ExportCriticalExcel(); };
            panel.Controls.Add(btnExportCritical);

            dgvCritical = CreateGrid();
            page.Controls.Add(dgvCritical);
            page.Controls.Add(panel);
            return page;
        }

        private void ExportCriticalExcel()
        {
            DataTable table = dgvCritical.DataSource as DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                UiTheme.ShowWarning(this, "داده‌ای برای خروجی اکسل وجود ندارد.");
                return;
            }
            ExportDataTableToExcel(table, "وضعیت‌های_بحرانی_" + (cmbCriticalFilter.Text ?? ""));
        }

        private TabPage BuildFamilyTab()
        {
            TabPage page = new TabPage("تحلیل خانواده");
            page.BackColor = UiTheme.Background;

            lblFamilySummary = CreateHeaderLabel();
            dgvFamily = CreateGrid();

            page.Controls.Add(dgvFamily);
            page.Controls.Add(lblFamilySummary);
            return page;
        }

        // ─── سیستم اعلان‌ها (بخش ۲) ──────────────────────────────────────────
        private TabPage BuildNotificationsTab()
        {
            TabPage page = new TabPage("اعلان‌ها");
            page.BackColor = UiTheme.Background;

            notificationsPanel = new FlowLayoutPanel();
            notificationsPanel.Dock = DockStyle.Fill;
            notificationsPanel.BackColor = UiTheme.Background;
            notificationsPanel.Padding = new Padding(12);
            notificationsPanel.AutoScroll = true;
            notificationsPanel.FlowDirection = FlowDirection.TopDown;
            notificationsPanel.WrapContents = false;

            page.Controls.Add(notificationsPanel);
            return page;
        }

        private class NotificationItem
        {
            public string Icon;
            public string Title;
            public string Detail;
            public Color Color;
        }

        private void LoadNotifications()
        {
            var items = new List<NotificationItem>();
            int cid = SecurityContext.CenterFilterId;

            // ۱) Backup امروز گرفته نشده (قابل خاموش‌کردن از تب اعلان‌ها)
            if (SettingsHelper.GetInt(SettingsHelper.Notify_BackupMissing, 1) == 1)
            {
                string lastBackup = SettingsHelper.Get(SettingsHelper.LastBackupDate);
                bool backupDoneToday = lastBackup == DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                if (!backupDoneToday)
                {
                    items.Add(new NotificationItem
                    {
                        Icon = "⚠",
                        Title = "بکاپ امروز گرفته نشده",
                        Detail = string.IsNullOrWhiteSpace(lastBackup) ? "تاکنون هیچ بکاپی ثبت نشده است" : "آخرین بکاپ: " + PersianDateHelper.ToPersianDateString(DateTime.Parse(lastBackup, System.Globalization.CultureInfo.InvariantCulture)),
                        Color = UiTheme.Danger
                    });
                }
            }

            // ۲) فضای دیسک کم است (قابل خاموش‌کردن)
            if (SettingsHelper.GetInt(SettingsHelper.Notify_LowDisk, 1) == 1)
            {
                try
                {
                    string root = FileHelper.GetBaseRootFolder();
                    string driveRoot = Path.GetPathRoot(string.IsNullOrWhiteSpace(root) ? Application.StartupPath : root);
                    DriveInfo di = new DriveInfo(driveRoot);
                    double freeGB = di.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

                    if (freeGB < 1.0)
                    {
                        items.Add(new NotificationItem
                        {
                            Icon = "⚠",
                            Title = "فضای دیسک کم است",
                            Detail = "فضای آزاد باقی‌مانده: " + freeGB.ToString("N2") + " گیگابایت",
                            Color = UiTheme.Danger
                        });
                    }
                }
                catch { /* بررسی فضای دیسک غیرحیاتی است */ }
            }

            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();

                if (SettingsHelper.GetInt(SettingsHelper.Notify_IncompleteCase, 1) == 1)
                {
                    int incompleteCases = Convert.ToInt32(NotificationScalar(con, @"
SELECT COUNT(1) FROM TblCase
WHERE (NULLIF(Code,'') IS NULL OR NULLIF(HeadFullName,'') IS NULL OR NULLIF(Phone,'') IS NULL)
  AND (@CID = 0 OR CenterID = @CID)", cid));
                    if (incompleteCases > 0)
                        items.Add(new NotificationItem { Icon = "✕", Title = "پرونده ناقص است", Detail = incompleteCases + " پرونده دارای اطلاعات ناقص", Color = UiTheme.Warning });
                }

                if (SettingsHelper.GetInt(SettingsHelper.Notify_NoPhoto, 1) == 1)
                {
                    int noPhoto = Convert.ToInt32(NotificationScalar(con, @"
SELECT COUNT(1) FROM TblCase
WHERE NULLIF(PhotoPath,'') IS NULL AND NULLIF(FamilyPhotoPath,'') IS NULL
  AND (@CID = 0 OR CenterID = @CID)", cid));
                    if (noPhoto > 0)
                        items.Add(new NotificationItem { Icon = "📷", Title = "عکس ندارد", Detail = noPhoto + " پرونده بدون عکس", Color = UiTheme.Warning });
                }

                if (SettingsHelper.GetInt(SettingsHelper.Notify_NoDocs, 1) == 1)
                {
                    int noDocs = Convert.ToInt32(NotificationScalar(con, @"
SELECT COUNT(1) FROM TblCase c
WHERE NOT EXISTS (SELECT 1 FROM TblDocs d WHERE d.CasID = c.CasID)
  AND (@CID = 0 OR c.CenterID = @CID)", cid));
                    if (noDocs > 0)
                        items.Add(new NotificationItem { Icon = "📄", Title = "سند ندارد", Detail = noDocs + " پرونده بدون سند", Color = UiTheme.Warning });

                    int incompleteDocs = Convert.ToInt32(NotificationScalar(con, @"
SELECT COUNT(1) FROM TblDocs d
JOIN TblCase c ON c.CasID = d.CasID
WHERE (NULLIF(d.DocType,'') IS NULL OR NULLIF(d.DocFilePath,'') IS NULL)
  AND (@CID = 0 OR c.CenterID = @CID)", cid));
                    if (incompleteDocs > 0)
                        items.Add(new NotificationItem { Icon = "📄", Title = "اسناد ناقص هستند", Detail = incompleteDocs + " سند دارای اطلاعات ناقص", Color = UiTheme.Warning });
                }

                if (SettingsHelper.GetInt(SettingsHelper.Notify_IncompleteFamily, 1) == 1)
                {
                    int noFamily = Convert.ToInt32(NotificationScalar(con, @"
SELECT COUNT(1) FROM TblCase c
WHERE NOT EXISTS (SELECT 1 FROM TblFamily f WHERE f.CasID = c.CasID)
  AND (@CID = 0 OR c.CenterID = @CID)", cid));
                    if (noFamily > 0)
                        items.Add(new NotificationItem { Icon = "👪", Title = "اعضای خانواده ناقص هستند", Detail = noFamily + " پرونده بدون عضو خانواده ثبت‌شده", Color = UiTheme.Warning });
                }

                // ۸) اطلاعات مالی ناقص — پرونده‌های «فعال» که هیچ کمکی برایشان
                // ثبت نشده (نشانه احتمالی از قلم‌افتادگی ثبت کمک، نه لزوماً خطا).
                if (SettingsHelper.GetInt(SettingsHelper.Notify_IncompleteFinance, 1) == 1)
                {
                    int noAssistance = Convert.ToInt32(NotificationScalar(con, @"
SELECT COUNT(1) FROM TblCase c
WHERE c.ServiceStatus = 'فعال'
  AND NOT EXISTS (SELECT 1 FROM TblAssistance a WHERE a.CasID = c.CasID)
  AND (@CID = 0 OR c.CenterID = @CID)", cid));
                    if (noAssistance > 0)
                        items.Add(new NotificationItem { Icon = "$", Title = "اطلاعات مالی ناقص است", Detail = noAssistance + " پرونده فعال بدون هیچ کمک ثبت‌شده", Color = UiTheme.Warning });
                }
            }

            if (items.Count == 0)
                items.Add(new NotificationItem { Icon = "✔", Title = "هیچ اعلانی وجود ندارد", Detail = "همه‌چیز به‌روز و کامل است", Color = UiTheme.Success });

            RenderNotifications(items);
        }

        private object NotificationScalar(SQLiteConnection con, string sql, int cid)
        {
            using (var cmd = new SQLiteCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                return cmd.ExecuteScalar();
            }
        }

        private void RenderNotifications(List<NotificationItem> items)
        {
            notificationsPanel.Controls.Clear();

            foreach (var item in items)
            {
                Panel card = new Panel();
                card.Width = 720;
                card.Height = 64;
                card.Margin = new Padding(0, 0, 0, 10);
                card.BackColor = UiTheme.CardBack;
                UiTheme.RoundCorners(card, 10);

                Panel stripe = new Panel();
                stripe.Dock = DockStyle.Right;
                stripe.Width = 6;
                stripe.BackColor = item.Color;
                card.Controls.Add(stripe);

                Label lblIcon = new Label();
                lblIcon.Text = item.Icon;
                lblIcon.Font = new Font("Segoe UI", 16F);
                lblIcon.ForeColor = item.Color;
                lblIcon.AutoSize = false;
                lblIcon.SetBounds(card.Width - 70, 12, 44, 40);
                lblIcon.TextAlign = ContentAlignment.MiddleCenter;
                card.Controls.Add(lblIcon);

                Label lblTitle = new Label();
                lblTitle.Text = item.Title;
                lblTitle.Font = UiTheme.FontBold(11F);
                lblTitle.ForeColor = UiTheme.TextDark;
                lblTitle.AutoSize = false;
                lblTitle.SetBounds(20, 8, card.Width - 100, 24);
                lblTitle.TextAlign = ContentAlignment.MiddleRight;
                card.Controls.Add(lblTitle);

                Label lblDetail = new Label();
                lblDetail.Text = item.Detail;
                lblDetail.Font = UiTheme.Font(9.5F);
                lblDetail.ForeColor = UiTheme.TextMuted;
                lblDetail.AutoSize = false;
                lblDetail.SetBounds(20, 32, card.Width - 100, 24);
                lblDetail.TextAlign = ContentAlignment.MiddleRight;
                card.Controls.Add(lblDetail);

                notificationsPanel.Controls.Add(card);
            }
        }

        // آموزش — دسته‌های ثابت تحصیلی (به درخواست کاربر): این پنج دسته همیشه
        // به‌عنوان کارت نمایش داده می‌شوند، حتی اگر هیچ عضوی در آن دسته نباشد
        // (عدد صفر). قبلاً چون کوئری GROUP BY فقط دسته‌های دارای داده را
        // برمی‌گرداند، هیچ کارتی نمایش داده نمی‌شد. هر ردیف: [متن نمایشی،
        // مقدار ذخیره‌شده در دیتابیس].
        private static readonly string[][] FamilyEducationCategories =
        {
            new[] { "دانشگاهی",   "دانشگاه" },
            new[] { "متعلمین",    "مکتب" },
            new[] { "طلبه",       "طلبه" },
            new[] { "بی‌سواد",    "بی‌سواد" },
            new[] { "ترک تحصیل",  "ترک تحصیل" },
        };

        // ─── داشبورد اعضای خانواده (بخش ۴) — بازطراحی حرفه‌ای ────────────────
        private TabPage BuildFamilyMembersStatsTab()
        {
            TabPage page = new TabPage("اعضای خانواده");
            page.BackColor = UiTheme.Background;

            Panel panel = new Panel();
            panel.Dock = DockStyle.Top;
            panel.Height = 52;
            panel.BackColor = UiTheme.CardBack;

            Button btnExportAll = UiTheme.CreateButton("خروجی اکسل کامل (همه اعضا)", "⇑", UiTheme.Primary);
            btnExportAll.SetBounds(16, 10, 240, 32);
            btnExportAll.Click += delegate { ExportFamilyMembersStatsExcel(); };
            panel.Controls.Add(btnExportAll);

            Button btnExportEveryMember = UiTheme.CreateSecondaryButton("فهرست کامل اعضا (اکسل)", "☰");
            btnExportEveryMember.SetBounds(266, 10, 210, 32);
            btnExportEveryMember.Click += delegate { ExportAllFamilyMembersExcel(); };
            panel.Controls.Add(btnExportEveryMember);

            // آموزش — به درخواست کاربر: دکمه اختصاصی برای دیدن و اکسل‌گرفتن
            // اعضای دارای معلولیت (که در مربع خلاصه فقط عددش بود).
            Button btnDisabled = UiTheme.CreateButton("نمایش اعضای معلول", "♿", UiTheme.Danger);
            btnDisabled.SetBounds(486, 10, 190, 32);
            btnDisabled.Click += delegate { ShowDisabledMembers(); };
            panel.Controls.Add(btnDisabled);

            // آموزش — فیلتر «وضعیت خدمات» به درخواست کاربر: با تغییر این
            // فیلتر، LoadFamilyMembersStats دوباره صدا زده می‌شود و همه
            // کارت‌ها/آمار/نمودارهای این تب بر همان اساس به‌روزرسانی می‌شوند.
            panel.Controls.Add(CreateLabel("وضعیت خدمات", 780, 16, 90));
            cmbFamilyServiceStatus = new ComboBox();
            cmbFamilyServiceStatus.Font = UiTheme.Font(9.5F);
            cmbFamilyServiceStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFamilyServiceStatus.Items.AddRange(new object[] { "همه", "فعال", "در انتظار تأیید", "قطع موقت", "قطع" });
            cmbFamilyServiceStatus.SelectedIndex = 0;
            cmbFamilyServiceStatus.SetBounds(660, 12, 115, 28);
            cmbFamilyServiceStatus.SelectedIndexChanged += delegate
            {
                _familyServiceStatusFilter = cmbFamilyServiceStatus.Text == "همه" ? "" : cmbFamilyServiceStatus.Text;
                LoadFamilyMembersStats();
            };
            panel.Controls.Add(cmbFamilyServiceStatus);

            // ─── نوار خلاصه کلی (KPI): مجموع کل / زیر ۱۸ / دختر / پسر / معلول ─
            familyMembersSummaryPanel = new FlowLayoutPanel();
            familyMembersSummaryPanel.Dock = DockStyle.Top;
            familyMembersSummaryPanel.Height = 98;
            familyMembersSummaryPanel.BackColor = UiTheme.Background;
            familyMembersSummaryPanel.Padding = new Padding(12, 8, 12, 0);
            familyMembersSummaryPanel.WrapContents = false;
            familyMembersSummaryPanel.AutoScroll = true;

            // ─── کارت‌های دسته تحصیلی (همیشه هر پنج دسته) ────────────────────
            familyCategoryTilesPanel = new FlowLayoutPanel();
            familyCategoryTilesPanel.Dock = DockStyle.Fill;
            familyCategoryTilesPanel.AutoScroll = true;
            familyCategoryTilesPanel.BackColor = UiTheme.Background;
            familyCategoryTilesPanel.Padding = new Padding(12, 8, 12, 8);
            familyCategoryTilesPanel.WrapContents = true;

            page.Controls.Add(familyCategoryTilesPanel);
            page.Controls.Add(familyMembersSummaryPanel);
            page.Controls.Add(panel);
            return page;
        }

        // شمارش تفکیکی هر دسته تحصیلی: کلید = مقدار ذخیره‌شده در دیتابیس،
        // مقدار = [دختر زیر۱۸، دختر بالای۱۸، پسر زیر۱۸، پسر بالای۱۸].
        private Dictionary<string, int[]> GetFamilyCategoryCounts()
        {
            var result = new Dictionary<string, int[]>();
            int cid = SecurityContext.CenterFilterId;

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT MemberEducation AS Edu,
    SUM(CASE WHEN Gender = 'دختر' AND Age < 18  THEN 1 ELSE 0 END) AS GU,
    SUM(CASE WHEN Gender = 'دختر' AND Age >= 18 THEN 1 ELSE 0 END) AS GO,
    SUM(CASE WHEN Gender = 'پسر'  AND Age < 18  THEN 1 ELSE 0 END) AS BU,
    SUM(CASE WHEN Gender = 'پسر'  AND Age >= 18 THEN 1 ELSE 0 END) AS BO
FROM (
    SELECT f.Gender, f.MemberEducation,
        CASE WHEN f.BirthDate IS NULL THEN 999
             ELSE CAST((julianday('now') - julianday(f.BirthDate)) / 365.25 AS INTEGER) END AS Age
    FROM TblFamily f
    JOIN TblCase c ON c.CasID = f.CasID
    WHERE (@CID = 0 OR c.CenterID = @CID)
      AND (@Status = '' OR c.ServiceStatus = @Status)" + CaseFilterSql("c") + @"
) x
GROUP BY MemberEducation", con))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Status", _familyServiceStatusFilter);
                AddCaseFilterParams(cmd);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        string edu = dr["Edu"] == DBNull.Value ? "" : dr["Edu"].ToString();
                        result[edu] = new int[]
                        {
                            dr["GU"] == DBNull.Value ? 0 : Convert.ToInt32(dr["GU"]),
                            dr["GO"] == DBNull.Value ? 0 : Convert.ToInt32(dr["GO"]),
                            dr["BU"] == DBNull.Value ? 0 : Convert.ToInt32(dr["BU"]),
                            dr["BO"] == DBNull.Value ? 0 : Convert.ToInt32(dr["BO"]),
                        };
                    }
                }
            }
            return result;
        }

        private DataTable GetFamilyMembersStatsTable()
        {
            int cid = SecurityContext.CenterFilterId;
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT
    CASE MemberEducation WHEN 'دانشگاه' THEN 'دانشگاهی' WHEN 'مکتب' THEN 'متعلم' ELSE MemberEducation END AS [دسته],
    SUM(CASE WHEN Gender = 'دختر' AND Age < 18 THEN 1 ELSE 0 END) AS [دختر زیر ۱۸],
    SUM(CASE WHEN Gender = 'دختر' AND Age >= 18 THEN 1 ELSE 0 END) AS [دختر بالای ۱۸],
    SUM(CASE WHEN Gender = 'پسر' AND Age < 18 THEN 1 ELSE 0 END) AS [پسر زیر ۱۸],
    SUM(CASE WHEN Gender = 'پسر' AND Age >= 18 THEN 1 ELSE 0 END) AS [پسر بالای ۱۸],
    COUNT(1) AS [مجموع]
FROM (
    SELECT f.FamID, f.Gender, f.MemberEducation,
        CASE WHEN f.BirthDate IS NULL THEN 999
             ELSE CAST((julianday('now') - julianday(f.BirthDate)) / 365.25 AS INTEGER) END AS Age
    FROM TblFamily f
    JOIN TblCase c ON c.CasID = f.CasID
    WHERE f.MemberEducation IN ('دانشگاه', 'مکتب', 'بی‌سواد', 'ترک تحصیل', 'طلبه')
      AND (@CID = 0 OR c.CenterID = @CID)
      AND (@Status = '' OR c.ServiceStatus = @Status)" + CaseFilterSql("c") + @"
) x
GROUP BY [دسته]
ORDER BY [دسته]", con))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Status", _familyServiceStatusFilter);
                AddCaseFilterParams(cmd);
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable t = new DataTable();
                    da.Fill(t);
                    return t;
                }
            }
        }

        // خلاصه کلی روی همه اعضای خانواده — بدون فیلتر MemberEducation که
        // GetFamilyMembersStatsTable دارد؛ پاسخ به این‌که «کلاً چند نفر زیر
        // ۱۸ سال داریم» جدا از تفکیک بر اساس دسته تحصیلی.
        private DataTable GetFamilyMembersOverallSummaryTable()
        {
            int cid = SecurityContext.CenterFilterId;
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT
    COUNT(1) AS [مجموع کل اعضا],
    SUM(CASE WHEN Age < 18 THEN 1 ELSE 0 END)  AS [مجموع کل اعضا زیر ۱۸ سال],
    SUM(CASE WHEN Age >= 18 THEN 1 ELSE 0 END) AS [مجموع کل اعضا بالای ۱۸ سال],
    SUM(CASE WHEN Disabled = 1 THEN 1 ELSE 0 END) AS [دارای معلولیت]
FROM (
    SELECT f.FamID,
        CASE WHEN f.BirthDate IS NULL THEN 999
             ELSE CAST((julianday('now') - julianday(f.BirthDate)) / 365.25 AS INTEGER) END AS Age,
        CASE WHEN NULLIF(f.HasDisability, '') IS NOT NULL
              AND f.HasDisability NOT IN ('0', 'false', 'False', 'نخیر', 'خیر', 'No', 'سالم')
             THEN 1 ELSE 0 END AS Disabled
    FROM TblFamily f
    JOIN TblCase c ON c.CasID = f.CasID
    WHERE (@CID = 0 OR c.CenterID = @CID)
      AND (@Status = '' OR c.ServiceStatus = @Status)" + CaseFilterSql("c") + @"
) x", con))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Status", _familyServiceStatusFilter);
                AddCaseFilterParams(cmd);
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable t = new DataTable();
                    da.Fill(t);
                    return t;
                }
            }
        }

        private void LoadFamilyMembersStats()
        {
            Dictionary<string, int[]> counts = GetFamilyCategoryCounts();

            familyCategoryTilesPanel.Controls.Clear();

            // همیشه هر پنج دسته ثابت را نمایش بده (حتی صفر) به ترتیب تعریف‌شده.
            for (int i = 0; i < FamilyEducationCategories.Length; i++)
            {
                string display = FamilyEducationCategories[i][0];
                string eduValue = FamilyEducationCategories[i][1];

                int[] c;
                if (!counts.TryGetValue(eduValue, out c))
                    c = new int[] { 0, 0, 0, 0 };

                Color accent = ChartPalette[i % ChartPalette.Length];
                familyCategoryTilesPanel.Controls.Add(
                    BuildFamilyCategoryTile(display, accent, c[0], c[1], c[2], c[3]));
            }

            LoadFamilyMembersOverallSummary();
        }

        // یک کارت دسته تحصیلی: سربرگ رنگی + جمع کل قابل‌کلیک + دو بخش
        // (دختر/پسر) که هرکدام دو مقدار قابل‌کلیک (بالای ۱۸ / زیر ۱۸) دارند +
        // دکمه خروجی اکسل کل همان دسته.
        private Panel BuildFamilyCategoryTile(string categoryDisplay, Color accent,
            int girlUnder18, int girlOver18, int boyUnder18, int boyOver18)
        {
            int cardW = 224;
            Panel card = new Panel();
            card.Width = cardW;
            card.Height = 300;
            card.Margin = new Padding(8);
            card.BackColor = UiTheme.CardBack;
            UiTheme.RoundCorners(card, 12);

            Panel header = new Panel();
            header.SetBounds(0, 0, cardW, 38);
            header.BackColor = accent;
            card.Controls.Add(header);

            Label lblTitle = new Label();
            lblTitle.Text = categoryDisplay;
            lblTitle.Font = UiTheme.FontBold(12F);
            lblTitle.ForeColor = Color.White;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.Dock = DockStyle.Fill;
            header.Controls.Add(lblTitle);

            int total = girlUnder18 + girlOver18 + boyUnder18 + boyOver18;

            int y = 48;
            y = AddFamilyGenderSection(card, "دختر", categoryDisplay, "دختر", girlUnder18, girlOver18, y, cardW);
            y = AddFamilyGenderSection(card, "پسر", categoryDisplay, "پسر", boyUnder18, boyOver18, y, cardW);

            // آموزش — به درخواست کاربر: «جمع» هر دسته در انتهای کارت (اخرش)
            // به‌صورت یک نوار قابل‌کلیک وسط‌چین می‌آید که همه اعضای دسته را
            // نمایش/خروجی می‌دهد؛ درست بالای دکمه خروجی اکسل.
            Panel totalRow = new Panel();
            totalRow.SetBounds(12, y, cardW - 24, 28);
            totalRow.BackColor = UiTheme.Background;
            totalRow.Cursor = Cursors.Hand;
            Label lblTotal = new Label
            {
                Text = "جمع کل دسته: " + total.ToString("N0"),
                Font = UiTheme.FontBold(10.5F),
                ForeColor = accent,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand
            };
            totalRow.Controls.Add(lblTotal);
            EventHandler totalClick = delegate { ShowFamilyMembersDetail(categoryDisplay, null, null); };
            totalRow.Click += totalClick; lblTotal.Click += totalClick;
            card.Controls.Add(totalRow);

            // دکمه خروجی اکسل کل دسته
            Button btnExport = UiTheme.CreateSecondaryButton("خروجی اکسل این دسته", "⇑");
            btnExport.SetBounds(12, card.Height - 42, cardW - 24, 32);
            btnExport.Font = UiTheme.FontBold(9F);
            btnExport.Click += delegate { ExportFamilyCategoryExcel(categoryDisplay); };
            card.Controls.Add(btnExport);

            return card;
        }

        // یک بخش جنسیت داخل کارت: برچسب وسط‌چین + دو جعبه کنار هم (بالای ۱۸ / زیر ۱۸).
        private int AddFamilyGenderSection(Panel card, string genderLabel, string category, string gender,
            int under18Count, int over18Count, int y, int cardW)
        {
            Label lbl = new Label();
            lbl.Text = genderLabel;
            lbl.Font = UiTheme.FontBold(9.5F);
            lbl.ForeColor = UiTheme.TextDark;
            lbl.TextAlign = ContentAlignment.MiddleCenter;
            lbl.SetBounds(12, y, cardW - 24, 18);
            card.Controls.Add(lbl);
            y += 20;

            int boxW = (cardW - 24 - 8) / 2; // دو جعبه با فاصله ۸
            int boxH = 50;
            // در RTL «بالای ۱۸» سمت راست و «زیر ۱۸» سمت چپ.
            card.Controls.Add(BuildFamilyStatBox(cardW - 12 - boxW, y, boxW, boxH, "بالای ۱۸ سال", over18Count, UiTheme.Success,
                delegate { ShowFamilyMembersDetail(category, gender, false); }));
            card.Controls.Add(BuildFamilyStatBox(12, y, boxW, boxH, "زیر ۱۸ سال", under18Count, UiTheme.Warning,
                delegate { ShowFamilyMembersDetail(category, gender, true); }));
            y += boxH + 8;

            return y;
        }

        // یک جعبه عدد قابل‌کلیک: عدد بزرگ بالا، عنوان کوچک پایین، پس‌زمینه ملایم.
        private Panel BuildFamilyStatBox(int x, int y, int w, int h, string caption, int count, Color accent, EventHandler onClick)
        {
            Panel box = new Panel();
            box.SetBounds(x, y, w, h);
            box.BackColor = UiTheme.Background;
            box.Cursor = Cursors.Hand;

            Label lblCount = new Label();
            lblCount.Text = count.ToString("N0");
            lblCount.Font = UiTheme.FontBold(15F);
            lblCount.ForeColor = accent;
            lblCount.TextAlign = ContentAlignment.MiddleCenter;
            lblCount.Cursor = Cursors.Hand;
            lblCount.SetBounds(0, 4, w, 26);

            Label lblCaption = new Label();
            lblCaption.Text = caption;
            lblCaption.Font = UiTheme.Font(8.5F);
            lblCaption.ForeColor = UiTheme.TextMuted;
            lblCaption.TextAlign = ContentAlignment.MiddleCenter;
            lblCaption.Cursor = Cursors.Hand;
            lblCaption.SetBounds(0, 30, w, 18);

            box.Controls.Add(lblCount);
            box.Controls.Add(lblCaption);

            box.Click += onClick;
            lblCount.Click += onClick;
            lblCaption.Click += onClick;

            return box;
        }

        private void LoadFamilyMembersOverallSummary()
        {
            familyMembersSummaryPanel.Controls.Clear();

            DataTable summary = GetFamilyMembersOverallSummaryTable();
            if (summary.Rows.Count == 0)
                return;

            DataRow row = summary.Rows[0];
            int total    = row["مجموع کل اعضا"]              == DBNull.Value ? 0 : Convert.ToInt32(row["مجموع کل اعضا"]);
            int under18  = row["مجموع کل اعضا زیر ۱۸ سال"]   == DBNull.Value ? 0 : Convert.ToInt32(row["مجموع کل اعضا زیر ۱۸ سال"]);
            int over18   = row["مجموع کل اعضا بالای ۱۸ سال"] == DBNull.Value ? 0 : Convert.ToInt32(row["مجموع کل اعضا بالای ۱۸ سال"]);
            // آموزش — به درخواست کاربر: کارت «دارای معلولیت» از این نوار حذف
            // شد چون دکمه اختصاصی «نمایش اعضای معلول» بالای صفحه همان کارکرد
            // را می‌دهد (مشاهده + خروجی اکسل)، پس نگه‌داشتن هر دو تکراری بود.
            AddFamilySummaryCard("مجموع کل اعضا", total, "♥", UiTheme.Primary, null);
            AddFamilySummaryCard("مجموع کل اعضا زیر ۱۸ سال", under18, "🧒", UiTheme.Warning, null);
            AddFamilySummaryCard("مجموع کل اعضا بالای ۱۸ سال", over18, "🧑", UiTheme.Success, null);
        }

        // کارت خلاصه اختصاصی داشبورد اعضای خانواده — عریض‌تر از AddSummaryCard
        // عمومی تا عنوان‌های بلند فارسی («مجموع کل اعضا زیر ۱۸ سال») کامل و
        // وسط‌چین جا شوند. اگر onClick داده شود، کل کارت قابل‌کلیک می‌شود.
        private void AddFamilySummaryCard(string title, int value, string icon, Color accent, EventHandler onClick)
        {
            Panel card = new Panel();
            card.Width = 250;
            card.Height = 78;
            card.Margin = new Padding(8, 4, 8, 4);
            card.BackColor = UiTheme.CardBack;
            UiTheme.RoundCorners(card, 12);

            Panel stripe = new Panel { Dock = DockStyle.Right, Width = 6, BackColor = accent };
            card.Controls.Add(stripe);

            Label lblValue = new Label();
            lblValue.Text = value.ToString("N0");
            lblValue.Font = UiTheme.FontBold(20F);
            lblValue.ForeColor = accent;
            lblValue.TextAlign = ContentAlignment.MiddleCenter;
            lblValue.SetBounds(10, 8, card.Width - 22, 34);
            card.Controls.Add(lblValue);

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.Font = UiTheme.FontBold(9.5F);
            lblTitle.ForeColor = UiTheme.TextDark;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.SetBounds(10, 44, card.Width - 22, 24);
            card.Controls.Add(lblTitle);

            if (onClick != null)
            {
                card.Cursor = Cursors.Hand;
                lblValue.Cursor = Cursors.Hand;
                lblTitle.Cursor = Cursors.Hand;
                card.Click += onClick;
                lblValue.Click += onClick;
                lblTitle.Click += onClick;
            }

            familyMembersSummaryPanel.Controls.Add(card);
        }

        private static string CategoryToEducationValue(string category)
        {
            foreach (string[] pair in FamilyEducationCategories)
                if (pair[0] == category)
                    return pair[1];
            return category;
        }

        // آموزش — به درخواست کاربر «تمام مشخصات عضو خانواده» نمایش داده می‌شود
        // (نه فقط چند ستون). ستون‌های داخلی GenderF/AgeF فقط برای فیلتر
        // جنسیت/سن هستند و بعد از پر شدن حذف می‌شوند تا در گزارش دیده نشوند.
        private DataTable GetFamilyMembersDetailTable(string category, string gender, bool? under18)
        {
            int cid = SecurityContext.CenterFilterId;
            string educationValue = CategoryToEducationValue(category);

            string genderSql = gender == null ? "" : "AND GenderF = @Gender";
            string ageSql = !under18.HasValue ? "" : (under18.Value ? "AND AgeF < 18" : "AND AgeF >= 18");

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT * FROM (
    SELECT
        c.Code                    AS [کد پرونده],
        c.HeadFullName            AS [نام سرپرست],
        f.MemberName              AS [نام عضو],
        f.MemberFatherName        AS [نام پدر عضو],
        f.Gender                  AS [جنسیت],
        CASE WHEN f.BirthDate IS NULL THEN NULL
             ELSE CAST((julianday('now') - julianday(f.BirthDate)) / 365.25 AS INTEGER) END AS [سن],
        f.BirthDate               AS [تاریخ تولد],
        f.MemberTazkiraNo         AS [شماره تذکره],
        f.MemberSadat             AS [سیادت],
        f.Religion                AS [مذهب],
        f.MaritalStatus           AS [وضعیت تأهل],
        f.MemberEducation         AS [تحصیلات],
        f.SchoolName              AS [نام مکتب],
        f.GradeLevel              AS [صنف],
        f.UniversityName          AS [نام دانشگاه],
        f.Major                   AS [رشته],
        f.StudyYear               AS [سمستر/درجه],
        f.PhysicalStatus          AS [وضعیت جسمی],
        f.HasDisability           AS [نوع معلولیت],
        f.MemberDisabilityDegree  AS [درجه معلولیت],
        f.Skill                   AS [مهارت],
        f.ServiceStatus           AS [وضعیت خدمات],
        f.Gender                  AS GenderF,
        CASE WHEN f.BirthDate IS NULL THEN 999
             ELSE CAST((julianday('now') - julianday(f.BirthDate)) / 365.25 AS INTEGER) END AS AgeF
    FROM TblFamily f
    JOIN TblCase c ON c.CasID = f.CasID
    WHERE f.MemberEducation = @Edu
      AND (@CID = 0 OR c.CenterID = @CID)
      AND (@Status = '' OR c.ServiceStatus = @Status)" + CaseFilterSql("c") + @"
) x
WHERE 1 = 1 " + genderSql + " " + ageSql + @"
ORDER BY [نام سرپرست]", con))
            {
                cmd.Parameters.AddWithValue("@Edu", educationValue);
                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Status", _familyServiceStatusFilter);
                AddCaseFilterParams(cmd);
                if (gender != null)
                    cmd.Parameters.AddWithValue("@Gender", gender);

                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable t = new DataTable();
                    da.Fill(t);
                    if (t.Columns.Contains("GenderF")) t.Columns.Remove("GenderF");
                    if (t.Columns.Contains("AgeF")) t.Columns.Remove("AgeF");
                    return t;
                }
            }
        }

        private void ShowFamilyMembersDetail(string category, string gender, bool? under18)
        {
            DataTable table = GetFamilyMembersDetailTable(category, gender, under18);

            string subTitle = gender == null ? "مجموع"
                : gender + " " + (under18 == true ? "زیر ۱۸ سال" : "بالای ۱۸ سال");

            ShowMembersPopup("جزئیات: " + category + " — " + subTitle, table, category + "_" + subTitle);
        }

        // پنجره مشترک نمایش فهرست اعضا با دکمه خروجی اکسل — هم برای جزئیات
        // دسته‌ها و هم برای «اعضای معلول» استفاده می‌شود (بدون تکرار کد).
        private void ShowMembersPopup(string windowTitle, DataTable table, string exportBaseName)
        {
            using (Form frm = new Form())
            {
                frm.Text = windowTitle;
                frm.RightToLeft = RightToLeft.Yes;
                frm.RightToLeftLayout = true;
                frm.StartPosition = FormStartPosition.CenterParent;
                UiTheme.MakeFixedSize(frm, 900, 500);
                frm.Font = UiTheme.Font(UiTheme.SizeBody);
                frm.BackColor = UiTheme.Background;

                Panel top = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
                Button btnExport = UiTheme.CreateButton("خروجی اکسل این بخش", "⇑", UiTheme.Primary);
                btnExport.SetBounds(20, 7, 180, 32);
                btnExport.Click += delegate { ExportDataTableToExcel(table, exportBaseName); };
                top.Controls.Add(btnExport);

                Label lblCount = CreateLabel("تعداد: " + table.Rows.Count, 210, 13, 200);
                top.Controls.Add(lblCount);

                DataGridView grid = CreateGrid();
                grid.DataSource = table;

                frm.Controls.Add(grid);
                frm.Controls.Add(top);

                frm.ShowDialog(this);
            }
        }

        // فهرست کامل اعضای دارای معلولیت (همه مشخصات) — تعریف «معلول» با همان
        // شرط شمارش مربع خلاصه هماهنگ است تا عدد و فهرست یکی باشند.
        private DataTable GetDisabledMembersTable()
        {
            int cid = SecurityContext.CenterFilterId;
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT
    c.Code                    AS [کد پرونده],
    c.HeadFullName            AS [نام سرپرست],
    f.MemberName              AS [نام عضو],
    f.MemberFatherName        AS [نام پدر عضو],
    f.Gender                  AS [جنسیت],
    CASE WHEN f.BirthDate IS NULL THEN NULL
         ELSE CAST((julianday('now') - julianday(f.BirthDate)) / 365.25 AS INTEGER) END AS [سن],
    f.MemberTazkiraNo         AS [شماره تذکره],
    f.PhysicalStatus          AS [وضعیت جسمی],
    f.HasDisability           AS [نوع معلولیت],
    f.MemberDisabilityDegree  AS [درجه معلولیت],
    f.MemberEducation         AS [تحصیلات],
    f.Skill                   AS [مهارت],
    f.ServiceStatus           AS [وضعیت خدمات]
FROM TblFamily f
JOIN TblCase c ON c.CasID = f.CasID
WHERE (@CID = 0 OR c.CenterID = @CID)
  AND (@Status = '' OR c.ServiceStatus = @Status)" + CaseFilterSql("c") + @"
  AND NULLIF(f.HasDisability, '') IS NOT NULL
  AND f.HasDisability NOT IN ('0', 'false', 'False', 'نخیر', 'خیر', 'No', 'سالم')
ORDER BY c.HeadFullName, f.MemberName", con))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Status", _familyServiceStatusFilter);
                AddCaseFilterParams(cmd);
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable t = new DataTable();
                    da.Fill(t);
                    return t;
                }
            }
        }

        private void ShowDisabledMembers()
        {
            DataTable table = GetDisabledMembersTable();
            if (table.Rows.Count == 0)
            {
                UiTheme.ShowWarning(this, "هیچ عضو دارای معلولیتی ثبت نشده است.");
                return;
            }
            ShowMembersPopup("اعضای دارای معلولیت", table, "اعضای_معلول");
        }

        private void ExportFamilyMembersStatsExcel()
        {
            try
            {
                string rootFolder = FileHelper.GetOrChooseBaseRootFolder();
                if (string.IsNullOrWhiteSpace(rootFolder))
                {
                    UiTheme.ShowWarning(this, "محل ذخیره فایل‌ها مشخص نیست");
                    return;
                }

                string reportsFolder = Path.Combine(rootFolder, "ExcelReports");
                Directory.CreateDirectory(reportsFolder);

                string outputPath = Path.Combine(reportsFolder,
                    "آمار_اعضای_خانواده_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".xlsx");

                DataTable overall = GetFamilyMembersOverallSummaryTable();
                DataTable byEducation = GetFamilyMembersStatsTable();

                using (XLWorkbook workbook = new XLWorkbook())
                {
                    IXLWorksheet wsOverall = workbook.Worksheets.Add("خلاصه کلی");
                    wsOverall.RightToLeft = true;
                    if (overall.Rows.Count == 0)
                        wsOverall.Cell(1, 1).Value = "داده‌ای برای نمایش وجود ندارد.";
                    else
                    {
                        IXLTable t = wsOverall.Cell(1, 1).InsertTable(overall, "Overall", true);
                        t.Theme = XLTableTheme.TableStyleMedium2;
                    }
                    wsOverall.Columns().AdjustToContents();

                    IXLWorksheet wsByEdu = workbook.Worksheets.Add("بر اساس تحصیلات");
                    wsByEdu.RightToLeft = true;
                    if (byEducation.Rows.Count == 0)
                        wsByEdu.Cell(1, 1).Value = "داده‌ای برای نمایش وجود ندارد.";
                    else
                    {
                        IXLTable t = wsByEdu.Cell(1, 1).InsertTable(byEducation, "ByEducation", true);
                        t.Theme = XLTableTheme.TableStyleMedium2;
                    }
                    wsByEdu.Columns().AdjustToContents();

                    workbook.SaveAs(outputPath);
                }

                UiTheme.ShowSuccess(this, "خروجی اکسل ذخیره شد:" + Environment.NewLine + outputPath);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ساخت خروجی اکسل: " + ex.Message);
            }
        }

        // خروجی اکسل کامل یک دسته تحصیلی (همه اعضای آن دسته، همه ستون‌ها).
        private void ExportFamilyCategoryExcel(string categoryDisplay)
        {
            DataTable table = GetFamilyMembersDetailTable(categoryDisplay, null, null);
            if (table.Rows.Count == 0)
            {
                UiTheme.ShowWarning(this, "در دسته «" + categoryDisplay + "» هیچ عضوی ثبت نشده است.");
                return;
            }
            ExportDataTableToExcel(table, "اعضای_" + categoryDisplay);
        }

        // خروجی اکسل فهرست کامل همه اعضای خانواده (بدون فیلتر تحصیلات) با تمام
        // مشخصات — برای گزارش‌گیری جامع.
        private void ExportAllFamilyMembersExcel()
        {
            int cid = SecurityContext.CenterFilterId;
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT
    c.Code                    AS [کد پرونده],
    c.HeadFullName            AS [نام سرپرست],
    f.MemberName              AS [نام عضو],
    f.MemberFatherName        AS [نام پدر عضو],
    f.Gender                  AS [جنسیت],
    CASE WHEN f.BirthDate IS NULL THEN NULL
         ELSE CAST((julianday('now') - julianday(f.BirthDate)) / 365.25 AS INTEGER) END AS [سن],
    f.BirthDate               AS [تاریخ تولد],
    f.MemberTazkiraNo         AS [شماره تذکره],
    f.MemberSadat             AS [سیادت],
    f.Religion                AS [مذهب],
    f.MaritalStatus           AS [وضعیت تأهل],
    f.MemberEducation         AS [تحصیلات],
    f.SchoolName              AS [نام مکتب],
    f.GradeLevel              AS [صنف],
    f.UniversityName          AS [نام دانشگاه],
    f.Major                   AS [رشته],
    f.StudyYear               AS [سمستر/درجه],
    f.PhysicalStatus          AS [وضعیت جسمی],
    f.HasDisability           AS [نوع معلولیت],
    f.MemberDisabilityDegree  AS [درجه معلولیت],
    f.Skill                   AS [مهارت],
    f.ServiceStatus           AS [وضعیت خدمات]
FROM TblFamily f
JOIN TblCase c ON c.CasID = f.CasID
WHERE (@CID = 0 OR c.CenterID = @CID)
  AND (@Status = '' OR c.ServiceStatus = @Status)
ORDER BY c.HeadFullName, f.MemberName", con))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Status", _familyServiceStatusFilter);
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable t = new DataTable();
                    da.Fill(t);
                    if (t.Rows.Count == 0)
                    {
                        UiTheme.ShowWarning(this, "هیچ عضو خانواده‌ای ثبت نشده است.");
                        return;
                    }
                    ExportDataTableToExcel(t, "فهرست_کامل_اعضای_خانواده");
                }
            }
        }

        // خروجی اکسل ساده برای یک DataTable (استفاده در جزئیات و مجموع کل)
        private void ExportDataTableToExcel(DataTable table, string suggestedName)
        {
            try
            {
                string rootFolder = FileHelper.GetOrChooseBaseRootFolder();
                if (string.IsNullOrWhiteSpace(rootFolder))
                {
                    UiTheme.ShowWarning(this, "محل ذخیره فایل‌ها مشخص نیست");
                    return;
                }

                string reportsFolder = Path.Combine(rootFolder, "ExcelReports");
                Directory.CreateDirectory(reportsFolder);

                string safeName = FileHelper.CleanName(suggestedName);
                string outputPath = Path.Combine(reportsFolder, safeName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".xlsx");

                using (XLWorkbook workbook = new XLWorkbook())
                {
                    IXLWorksheet ws = workbook.Worksheets.Add("داده‌ها");
                    ws.RightToLeft = true;

                    if (table.Rows.Count == 0)
                    {
                        ws.Cell(1, 1).Value = "داده‌ای برای نمایش وجود ندارد.";
                    }
                    else
                    {
                        IXLTable excelTable = ws.Cell(1, 1).InsertTable(table, "Data", true);
                        excelTable.Theme = XLTableTheme.TableStyleMedium2;
                        ws.Columns().AdjustToContents();
                    }

                    workbook.SaveAs(outputPath);
                }

                UiTheme.ShowSuccess(this, "خروجی اکسل ذخیره شد:" + Environment.NewLine + outputPath);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ساخت خروجی اکسل: " + ex.Message);
            }
        }

        private TabPage BuildGeographyTab()
        {
            TabPage page = new TabPage("جغرافیا");
            page.BackColor = UiTheme.Background;

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 320;

            Panel panel = new Panel();
            panel.Dock = DockStyle.Top;
            panel.Height = 50;
            panel.BackColor = UiTheme.CardBack;

            txtDistrictFilter = new TextBox();
            txtDistrictFilter.SetBounds(20, 12, 160, 26);
            UiTheme.StyleTextBox(txtDistrictFilter);

            Button btnApply = UiTheme.CreateButton("اعمال", "⌕", UiTheme.Primary);
            btnApply.SetBounds(190, 9, 90, 30);
            btnApply.Click += delegate { LoadGeography(); };

            panel.Controls.Add(CreateLabel("فیلتر ولسوالی", 280, 12, 100));
            panel.Controls.Add(txtDistrictFilter);
            panel.Controls.Add(btnApply);

            geoChart = CreateChart("پرونده‌ها بر اساس ولایت", SeriesChartType.Column);
            dgvGeo = CreateGrid();

            split.Panel1.Controls.Add(geoChart);
            split.Panel1.Controls.Add(panel);
            split.Panel2.Controls.Add(dgvGeo);

            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildReminderTab()
        {
            TabPage page = new TabPage("یادآوری سروی");
            page.BackColor = UiTheme.Background;

            // ─── نوار ابزار یادآوری‌های دستی (به درخواست کاربر) ──────────────
            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = UiTheme.CardBack };
            Button btnAddReminder = UiTheme.CreateButton("افزودن یادآوری", "＋", UiTheme.Primary);
            btnAddReminder.SetBounds(16, 9, 160, 32);
            btnAddReminder.Click += delegate { ShowAddReminderDialog(); };
            toolbar.Controls.Add(btnAddReminder);

            Button btnDoneReminder = UiTheme.CreateSecondaryButton("علامت انجام‌شده", "✔");
            btnDoneReminder.SetBounds(184, 9, 150, 32);
            btnDoneReminder.Click += delegate { MarkSelectedReminderDone(); };
            toolbar.Controls.Add(btnDoneReminder);

            Label lblHint = CreateLabel("یادآوری‌های زمان‌دار — سر موعد، زنگ/پیام نمایش داده می‌شود", 344, 15, 420);
            toolbar.Controls.Add(lblHint);

            // ─── دو بخش: بالا یادآوری‌های دستی، پایین پیگیری سروی پرونده‌ها ────
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 240;

            dgvCustomReminders = CreateGrid();
            UiTheme.ApplyPersianDateColumns(dgvCustomReminders, "موعد");
            Label lblCustomTitle = new Label
            {
                Text = "یادآوری‌های من", AutoSize = false, Dock = DockStyle.Top, Height = 26,
                Font = UiTheme.FontBold(10F), ForeColor = UiTheme.PrimaryDark,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 12, 0)
            };
            split.Panel1.Controls.Add(dgvCustomReminders);
            split.Panel1.Controls.Add(lblCustomTitle);

            lblReminderSummary = CreateHeaderLabel();
            dgvReminders = CreateGrid();
            UiTheme.ApplyPersianDateColumns(dgvReminders, "SurveyDate");
            split.Panel2.Controls.Add(dgvReminders);
            split.Panel2.Controls.Add(lblReminderSummary);

            page.Controls.Add(split);
            page.Controls.Add(toolbar);
            return page;
        }

        // ─── افزودن یادآوری زمان‌دار (عنوان/یادداشت/تاریخ+ساعت) ───────────────
        private void ShowAddReminderDialog()
        {
            using (Form frm = new Form())
            {
                frm.Text = "افزودن یادآوری";
                frm.RightToLeft = RightToLeft.Yes;
                frm.RightToLeftLayout = true;
                frm.FormBorderStyle = FormBorderStyle.FixedDialog;
                frm.StartPosition = FormStartPosition.CenterParent;
                frm.MaximizeBox = false;
                frm.MinimizeBox = false;
                frm.BackColor = UiTheme.Background;
                frm.Font = UiTheme.Font(UiTheme.SizeBody);
                frm.ClientSize = new Size(420, 320);

                Label lblT = new Label { Text = "عنوان", TextAlign = ContentAlignment.MiddleRight };
                lblT.SetBounds(300, 20, 100, 24);
                TextBox txtTitle = new TextBox(); txtTitle.SetBounds(20, 20, 270, 26); UiTheme.StyleTextBox(txtTitle);

                Label lblN = new Label { Text = "یادداشت", TextAlign = ContentAlignment.MiddleRight };
                lblN.SetBounds(300, 58, 100, 24);
                TextBox txtNote = new TextBox { Multiline = true, RightToLeft = RightToLeft.Yes };
                txtNote.SetBounds(20, 58, 270, 80); UiTheme.StyleTextBox(txtNote);

                Label lblD = new Label { Text = "تاریخ", TextAlign = ContentAlignment.MiddleRight };
                lblD.SetBounds(300, 152, 100, 24);
                Helpers.PersianDatePicker dtp = new Helpers.PersianDatePicker();
                dtp.SetBounds(20, 150, 200, 28);

                Label lblTime = new Label { Text = "ساعت : دقیقه", TextAlign = ContentAlignment.MiddleRight };
                lblTime.SetBounds(240, 192, 160, 24);
                NumericUpDown numHour = new NumericUpDown { Minimum = 0, Maximum = 23, Value = Math.Min(23, DateTime.Now.Hour) };
                numHour.SetBounds(150, 190, 60, 26);
                NumericUpDown numMin = new NumericUpDown { Minimum = 0, Maximum = 59, Value = DateTime.Now.Minute };
                numMin.SetBounds(20, 190, 60, 26);

                Button btnOk = UiTheme.CreateButton("ذخیره یادآوری", "✔", UiTheme.Success);
                btnOk.SetBounds(220, 250, 180, 38);
                Button btnCancel = UiTheme.CreateSecondaryButton("انصراف", "");
                btnCancel.SetBounds(20, 250, 120, 38);
                btnCancel.Click += delegate { frm.Close(); };

                btnOk.Click += delegate
                {
                    if (string.IsNullOrWhiteSpace(txtTitle.Text))
                    {
                        UiTheme.ShowWarning(frm, "عنوان یادآوری را وارد کنید.");
                        return;
                    }
                    DateTime remindAt = dtp.Value.Date.AddHours((double)numHour.Value).AddMinutes((double)numMin.Value);
                    SaveReminder(txtTitle.Text.Trim(), txtNote.Text.Trim(), remindAt);
                    frm.DialogResult = DialogResult.OK;
                    frm.Close();
                };

                frm.Controls.Add(lblT); frm.Controls.Add(txtTitle);
                frm.Controls.Add(lblN); frm.Controls.Add(txtNote);
                frm.Controls.Add(lblD); frm.Controls.Add(dtp);
                frm.Controls.Add(lblTime); frm.Controls.Add(numHour); frm.Controls.Add(numMin);
                frm.Controls.Add(btnOk); frm.Controls.Add(btnCancel);
                frm.AcceptButton = btnOk;

                if (frm.ShowDialog(this) == DialogResult.OK)
                    LoadCustomReminders();
            }
        }

        private void SaveReminder(string title, string note, DateTime remindAt)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand(@"
INSERT INTO TblReminder (Title, Note, RemindAt, CenterID, CreatedBy)
VALUES (@Title, @Note, @RemindAt, @CID, @By)", con))
            {
                cmd.Parameters.AddWithValue("@Title", title);
                cmd.Parameters.AddWithValue("@Note", (object)note ?? DBNull.Value);
                // میلادی ISO ذخیره می‌شود تا مقایسه با datetime('now') درست باشد.
                cmd.Parameters.AddWithValue("@RemindAt", remindAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@CID", SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value);
                cmd.Parameters.AddWithValue("@By", (object)SecurityContext.Username ?? DBNull.Value);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            UiTheme.ShowSuccess(this, "یادآوری ثبت شد.");
        }

        private void MarkSelectedReminderDone()
        {
            if (dgvCustomReminders == null || dgvCustomReminders.CurrentRow == null ||
                !dgvCustomReminders.Columns.Contains("ReminderID"))
            {
                UiTheme.ShowWarning(this, "ابتدا یک یادآوری را از جدول انتخاب کنید.");
                return;
            }
            object idv = dgvCustomReminders.CurrentRow.Cells["ReminderID"].Value;
            if (idv == null || idv == DBNull.Value) return;

            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("UPDATE TblReminder SET IsDone = 1 WHERE ReminderID = @ID", con))
            {
                cmd.Parameters.AddWithValue("@ID", Convert.ToInt32(idv));
                con.Open();
                cmd.ExecuteNonQuery();
            }
            LoadCustomReminders();
        }

        private void LoadCustomReminders()
        {
            if (dgvCustomReminders == null) return;
            int cid = SecurityContext.CenterFilterId;
            DataTable table = GetTableCid(@"
SELECT ReminderID,
       Title    AS [عنوان],
       Note     AS [یادداشت],
       RemindAt AS [موعد],
       CASE IsDone WHEN 1 THEN 'انجام‌شده' ELSE 'در انتظار' END AS [وضعیت]
FROM TblReminder
WHERE (@CID = 0 OR CenterID = @CID OR CenterID IS NULL)
ORDER BY IsDone, RemindAt", cid);
            dgvCustomReminders.DataSource = table;
            if (dgvCustomReminders.Columns.Contains("ReminderID"))
                dgvCustomReminders.Columns["ReminderID"].Visible = false;
        }

        // زنگ یادآوری: هر ۳۰ ثانیه بررسی می‌کند و موارد سررسیده را (که هنوز
        // اعلان نشده و انجام‌نشده) به‌صورت پیام نشان می‌دهد.
        private void StartReminderTimer()
        {
            _reminderTimer = new System.Windows.Forms.Timer();
            _reminderTimer.Interval = 30000;
            _reminderTimer.Tick += delegate { CheckDueReminders(); };
            _reminderTimer.Start();
            CheckDueReminders();
        }

        private void CheckDueReminders()
        {
            try
            {
                DataTable due = new DataTable();
                int cid = SecurityContext.CenterFilterId;
                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(@"
SELECT ReminderID, Title, Note, RemindAt
FROM TblReminder
WHERE IsDone = 0 AND IsNotified = 0
  AND RemindAt <= strftime('%Y-%m-%d %H:%M', 'now', 'localtime')
  AND (@CID = 0 OR CenterID = @CID OR CenterID IS NULL)
ORDER BY RemindAt", con))
                {
                    cmd.Parameters.AddWithValue("@CID", cid);
                    using (var da = new SQLiteDataAdapter(cmd))
                        da.Fill(due);
                }

                foreach (DataRow row in due.Rows)
                {
                    string title = row["Title"] == DBNull.Value ? "" : row["Title"].ToString();
                    string note = row["Note"] == DBNull.Value ? "" : row["Note"].ToString();
                    UiTheme.ShowWarning(this, "⏰ یادآوری: " + title + (string.IsNullOrWhiteSpace(note) ? "" : Environment.NewLine + note));

                    using (var con = db.GetConnection())
                    using (var upd = new SQLiteCommand("UPDATE TblReminder SET IsNotified = 1 WHERE ReminderID = @ID", con))
                    {
                        upd.Parameters.AddWithValue("@ID", Convert.ToInt32(row["ReminderID"]));
                        con.Open();
                        upd.ExecuteNonQuery();
                    }
                }
            }
            catch { /* بررسی یادآوری غیرحیاتی است؛ خطا نادیده گرفته می‌شود */ }
        }

        private TabPage BuildQualityTab()
        {
            TabPage page = new TabPage("کیفیت داده");
            page.BackColor = UiTheme.Background;

            lblQualitySummary = CreateHeaderLabel();
            dgvQuality = CreateGrid();

            // ─── منوی راست‌کلیک: باز کردن پرونده‌ی مشکل‌دار برای اصلاح ─────────
            ContextMenuStrip menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
            ToolStripMenuItem miOpen = new ToolStripMenuItem("✎  باز کردن پرونده برای اصلاح");
            miOpen.Click += delegate { OpenQualityCaseForEdit(); };
            ToolStripMenuItem miCopy = new ToolStripMenuItem("⧉  کپی کد اختصاصی");
            miCopy.Click += delegate
            {
                if (dgvQuality.CurrentRow != null && dgvQuality.Columns.Contains("Code"))
                {
                    try { Clipboard.SetText(Convert.ToString(dgvQuality.CurrentRow.Cells["Code"].Value)); } catch { }
                }
            };
            menu.Items.Add(miOpen);
            menu.Items.Add(miCopy);
            dgvQuality.ContextMenuStrip = menu;
            // با راست‌کلیک، همان ردیفِ زیر ماوس انتخاب می‌شود (تا منو روی رکورد درست عمل کند).
            dgvQuality.CellMouseDown += delegate (object s, DataGridViewCellMouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
                    dgvQuality.CurrentCell = dgvQuality.Rows[e.RowIndex].Cells[e.ColumnIndex];
            };
            // دابل‌کلیک هم پرونده را برای اصلاح باز می‌کند (میان‌بر سریع).
            dgvQuality.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0) OpenQualityCaseForEdit();
            };

            page.Controls.Add(dgvQuality);
            page.Controls.Add(lblQualitySummary);
            return page;
        }

        // باز کردن پرونده‌ی انتخاب‌شده در تب «کیفیت داده» داخل فرم پرونده برای اصلاح.
        private void OpenQualityCaseForEdit()
        {
            if (dgvQuality.CurrentRow == null || !dgvQuality.Columns.Contains("CasID"))
            {
                UiTheme.ShowWarning(this, "ابتدا یک پرونده را انتخاب کنید.");
                return;
            }
            object v = dgvQuality.CurrentRow.Cells["CasID"].Value;
            if (v == null || v == DBNull.Value) return;

            int casId = Convert.ToInt32(v);
            using (var frm = new FrmCase(casId))
                frm.ShowDialog(this);
            RefreshAll(); // پس از اصلاح، آمار به‌روز شود
        }

        private TabPage BuildAuditTab()
        {
            TabPage page = new TabPage("گزارش رویدادها");
            page.BackColor = UiTheme.Background;

            Panel panel = new Panel();
            panel.Dock = DockStyle.Top;
            panel.Height = 50;
            panel.BackColor = UiTheme.CardBack;

            Button btnRefresh = UiTheme.CreateButton("تازه‌سازی", "🔄", UiTheme.Primary);
            btnRefresh.SetBounds(20, 9, 110, 30);
            btnRefresh.Click += delegate { LoadAudit(); };
            panel.Controls.Add(btnRefresh);

            // آموزش — به درخواست کاربر: خروجی اکسل گزارش رویدادها (همان چیزی
            // که در حال حاضر در گرید نمایش داده می‌شود).
            Button btnExportAudit = UiTheme.CreateSecondaryButton("خروجی اکسل", "⇑");
            btnExportAudit.SetBounds(140, 9, 130, 30);
            btnExportAudit.Click += delegate
            {
                DataTable t = dgvAudit.DataSource as DataTable;
                if (t == null || t.Rows.Count == 0)
                {
                    UiTheme.ShowWarning(this, "داده‌ای برای خروجی اکسل وجود ندارد.");
                    return;
                }
                ExportDataTableToExcel(t, "گزارش_رویدادها");
            };
            panel.Controls.Add(btnExportAudit);

            dgvAudit = CreateGrid();
            UiTheme.ApplyPersianDateColumns(dgvAudit, "CreatedAt");
            page.Controls.Add(dgvAudit);
            page.Controls.Add(panel);
            return page;
        }

        private void RefreshAll()
        {
            LoadSummary();
            LoadTrend();
            LoadCritical();
            // آموزش — رفع باگ NullReferenceException: تب «تحلیل خانواده» حذف
            // شد، پس lblFamilySummary/dgvFamily دیگر ساخته نمی‌شوند و فراخوانی
            // LoadFamily() روی آن‌ها خطای Null می‌داد؛ این فراخوانی حذف شد.
            LoadFamilyMembersStats();
            LoadNotifications();
            LoadGeography();
            LoadReminders();
            LoadQuality();
            LoadAudit();
        }

        private void LoadSummary()
        {
            summaryPanel.Controls.Clear();
            int cid = SecurityContext.CenterFilterId;

            int total = 0, active = 0, waiting = 0, stopped = 0, stoppedTemp = 0, family = 0;

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT
    (SELECT COUNT(1) FROM TblCase        WHERE (@CID=0 OR CenterID=@CID)" + CaseFilterSql("") + @") AS Total,
    (SELECT COUNT(1) FROM TblFamily f
      JOIN TblCase c ON c.CasID = f.CasID
      WHERE (@CID=0 OR c.CenterID=@CID)" + CaseFilterSql("c") + @")                                  AS FamilyCount,
    SUM(CASE WHEN ServiceStatus = 'فعال' THEN 1 ELSE 0 END)       AS Active,
    SUM(CASE WHEN ServiceStatus = 'در انتظار تأیید' THEN 1 ELSE 0 END)  AS Waiting,
    SUM(CASE WHEN ServiceStatus = 'قطع' THEN 1 ELSE 0 END)        AS Stopped,
    SUM(CASE WHEN ServiceStatus = 'قطع موقت' THEN 1 ELSE 0 END)   AS StoppedTemp
FROM TblCase
WHERE (@CID = 0 OR CenterID = @CID)" + CaseFilterSql("") + @"", con))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                AddCaseFilterParams(cmd);
                con.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        total = Convert.ToInt32(dr["Total"]);
                        family = Convert.ToInt32(dr["FamilyCount"]);
                        active = dr["Active"] == DBNull.Value ? 0 : Convert.ToInt32(dr["Active"]);
                        waiting = dr["Waiting"] == DBNull.Value ? 0 : Convert.ToInt32(dr["Waiting"]);
                        stopped = dr["Stopped"] == DBNull.Value ? 0 : Convert.ToInt32(dr["Stopped"]);
                        stoppedTemp = dr["StoppedTemp"] == DBNull.Value ? 0 : Convert.ToInt32(dr["StoppedTemp"]);
                    }
                }
            }

            AddSummaryCard("کل پرونده‌ها", total, "▤", UiTheme.Primary);
            AddSummaryCard("فعال", active, "✔", UiTheme.Success);
            AddSummaryCard("در انتظار تأیید", waiting, "⏳", UiTheme.Warning);
            AddSummaryCard("قطع", stopped, "✕", UiTheme.Danger);
            AddSummaryCard("اعضای خانواده", family, "♥", UiTheme.Primary);

            DataTable chartData = new DataTable();
            chartData.Columns.Add("Title");
            chartData.Columns.Add("Count", typeof(int));
            chartData.Rows.Add("فعال", active);
            chartData.Rows.Add("در انتظار تأیید", waiting);
            chartData.Rows.Add("قطع", stopped);
            chartData.Rows.Add("قطع موقت", stoppedTemp);
            FillChart(statusChart, chartData, "Title", "Count", SeriesChartType.Pie);
        }

        private void LoadTrend()
        {
            bool monthly = cmbTrendMode != null && cmbTrendMode.Text == "ماهانه";
            string groupExpression = monthly
                ? "strftime('%Y-%m', CaseDate)"
                : "strftime('%Y-%m-%d', CaseDate)";
            string statusGroupExpression = monthly
                ? "strftime('%Y-%m', ChangedAt)"
                : "strftime('%Y-%m-%d', ChangedAt)";

            int cid = SecurityContext.CenterFilterId;
            DataTable registered, activated, stopped;

            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();

                registered = GetTableCidF(con, @"
SELECT " + groupExpression + @" AS Period, COUNT(1) AS CountValue
FROM TblCase
WHERE CaseDate >= date('now', '-12 months')
  AND (@CID = 0 OR CenterID = @CID)" + CaseFilterSql("") + @"
GROUP BY " + groupExpression + @"
ORDER BY Period", cid);

                // TblCaseStatusHistory مرتبط است با TblCase از طریق CasID.
                // فیلتر ولایت/ولسوالی داخل همان EXISTS روی TblCase c اعمال می‌شود.
                activated = GetTableCidF(con, @"
SELECT " + statusGroupExpression + @" AS Period, COUNT(1) AS CountValue
FROM TblCaseStatusHistory sh
WHERE sh.NewStatus = 'فعال' AND sh.ChangedAt >= date('now', '-12 months')
  AND EXISTS (SELECT 1 FROM TblCase c WHERE c.CasID = sh.CasID AND (@CID = 0 OR c.CenterID = @CID)" + CaseFilterSql("c") + @")
GROUP BY " + statusGroupExpression + @"
ORDER BY Period", cid);

                stopped = GetTableCidF(con, @"
SELECT " + statusGroupExpression + @" AS Period, COUNT(1) AS CountValue
FROM TblCaseStatusHistory sh
WHERE sh.NewStatus = 'قطع' AND sh.ChangedAt >= date('now', '-12 months')
  AND EXISTS (SELECT 1 FROM TblCase c WHERE c.CasID = sh.CasID AND (@CID = 0 OR c.CenterID = @CID)" + CaseFilterSql("c") + @")
GROUP BY " + statusGroupExpression + @"
ORDER BY Period", cid);
            }

            trendChart.Series.Clear();
            AddLineSeries(trendChart, "ثبت شده", registered);
            AddLineSeries(trendChart, "فعال شده", activated);
            AddLineSeries(trendChart, "قطع شده", stopped);
        }

        private void LoadCritical()
        {
            string filter = cmbCriticalFilter == null ? "وضعیت فوری" : cmbCriticalFilter.Text;
            string where;

            if (filter == "بدون عکس")
                where = "(NULLIF(PhotoPath, '') IS NULL AND NULLIF(FamilyPhotoPath, '') IS NULL)";
            else if (filter == "بدون سند")
                where = "NOT EXISTS (SELECT 1 FROM TblDocs d WHERE d.CasID = c.CasID)";
            else if (filter == "بدون شماره تماس")
                where = "NULLIF(Phone, '') IS NULL";
            else
                where = "NULLIF(UrgentSituation, '') IS NOT NULL";

            int cid = SecurityContext.CenterFilterId;
            dgvCritical.DataSource = GetTableCidF(@"
SELECT c.CasID, c.FormNo, c.Code, c.HeadFullName, c.Phone, c.Province, c.District, c.ServiceStatus, c.UrgentSituation
FROM TblCase c
WHERE " + where + @"
  AND (@CID = 0 OR c.CenterID = @CID)" + CaseFilterSql("c") + @"
ORDER BY c.CasID DESC", cid);
        }

        private void LoadFamily()
        {
            int cid = SecurityContext.CenterFilterId;
            int totalMembers = 0, children = 0, disabled = 0;
            decimal avgMembers = 0;
            DataTable familyGrid;

            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();

                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT
    (SELECT COUNT(1) FROM TblFamily f2
      JOIN TblCase c2 ON c2.CasID = f2.CasID
      WHERE (@CID=0 OR c2.CenterID=@CID)) AS TotalMembers,
    (SELECT COALESCE(AVG(CAST(MemberCount AS decimal(18,2))), 0)
     FROM (
         SELECT c.CasID, COUNT(f.FamID) AS MemberCount
         FROM TblCase c
         LEFT JOIN TblFamily f ON f.CasID = c.CasID
         WHERE (@CID=0 OR c.CenterID=@CID)
         GROUP BY c.CasID
     ) x) AS AvgMembers,
    (SELECT COUNT(1) FROM TblFamily f3
      JOIN TblCase c3 ON c3.CasID = f3.CasID
      WHERE f3.BirthDate IS NOT NULL AND f3.BirthDate > date('now', '-10 years')
        AND (@CID=0 OR c3.CenterID=@CID)) AS Children,
    (SELECT COUNT(1) FROM TblFamily f4
      JOIN TblCase c4 ON c4.CasID = f4.CasID
      WHERE NULLIF(f4.HasDisability, '') IS NOT NULL
        AND f4.HasDisability NOT IN ('0', 'false', 'False', 'نخیر', 'خیر', 'No')
        AND (@CID=0 OR c4.CenterID=@CID)) AS Disabled", con))
                {
                    cmd.Parameters.AddWithValue("@CID", cid);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            totalMembers = Convert.ToInt32(dr["TotalMembers"]);
                            avgMembers = Convert.ToDecimal(dr["AvgMembers"]);
                            children = Convert.ToInt32(dr["Children"]);
                            disabled = Convert.ToInt32(dr["Disabled"]);
                        }
                    }
                }

                familyGrid = GetTableCid(con, @"
SELECT c.CasID, c.FormNo, c.Code, c.HeadFullName, COUNT(f.FamID) AS [تعداد اعضا]
FROM TblCase c
LEFT JOIN TblFamily f ON f.CasID = c.CasID
WHERE (@CID = 0 OR c.CenterID = @CID)
GROUP BY c.CasID, c.FormNo, c.Code, c.HeadFullName
ORDER BY [تعداد اعضا] DESC, c.CasID DESC", cid);
            }

            lblFamilySummary.Text =
                "تعداد کل اعضا: " + totalMembers +
                "    میانگین اعضا برای هر پرونده: " + avgMembers.ToString("N2") +
                "    کودکان زیر 10 سال: " + children +
                "    افراد دارای معلولیت: " + disabled;

            dgvFamily.DataSource = familyGrid;
        }

        private void LoadGeography()
        {
            string district = txtDistrictFilter == null ? "" : txtDistrictFilter.Text.Trim();
            int cid = SecurityContext.CenterFilterId;
            string query = @"
SELECT Province AS [ولایت], COUNT(1) AS [تعداد پرونده]
FROM TblCase
WHERE NULLIF(Province, '') IS NOT NULL
  AND (@District = '' OR District LIKE @DistrictLike)
  AND (@CID = 0 OR CenterID = @CID)" + CaseFilterSql("") + @"
GROUP BY Province
ORDER BY [تعداد پرونده] DESC";

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@District", district);
                cmd.Parameters.AddWithValue("@DistrictLike", "%" + district + "%");
                cmd.Parameters.AddWithValue("@CID", cid);
                AddCaseFilterParams(cmd);

                DataTable table = new DataTable();
                da.Fill(table);
                dgvGeo.DataSource = table;
                FillChart(geoChart, table, "ولایت", "تعداد پرونده", SeriesChartType.Column);
            }
        }

        private void LoadReminders()
        {
            int cid = SecurityContext.CenterFilterId;
            DataTable table = GetTableCidF(@"
SELECT CasID, FormNo, Code, HeadFullName, Phone, SurveyDate, ServiceStatus
FROM TblCase
WHERE (SurveyDate IS NULL OR SurveyDate < date('now', '-6 months'))
  AND (@CID = 0 OR CenterID = @CID)" + CaseFilterSql("") + @"
ORDER BY SurveyDate, CasID DESC", cid);

            lblReminderSummary.Text = "پرونده‌های نیازمند پیگیری سروی: " + table.Rows.Count;
            dgvReminders.DataSource = table;

            LoadCustomReminders();
        }

        private void LoadQuality()
        {
            int cid = SecurityContext.CenterFilterId;
            DataTable table = GetTableCidF(@"
SELECT
    CasID,
    FormNo,
    Code,
    HeadFullName,
    Phone,
    CASE WHEN NULLIF(Code, '') IS NULL THEN 'بله' ELSE '' END AS [بدون کد],
    CASE WHEN NULLIF(Phone, '') IS NULL THEN 'بله' ELSE '' END AS [بدون تماس],
    CASE WHEN NULLIF(PhotoPath, '') IS NULL AND NULLIF(FamilyPhotoPath, '') IS NULL THEN 'بله' ELSE '' END AS [بدون عکس],
    CASE WHEN NULLIF(HeadFullName, '') IS NULL OR NULLIF(Code, '') IS NULL OR NULLIF(Phone, '') IS NULL THEN 'بله' ELSE '' END AS [ناقص]
FROM TblCase
WHERE (NULLIF(Code, '') IS NULL
   OR NULLIF(Phone, '') IS NULL
   OR (NULLIF(PhotoPath, '') IS NULL AND NULLIF(FamilyPhotoPath, '') IS NULL)
   OR NULLIF(HeadFullName, '') IS NULL)
  AND (@CID = 0 OR CenterID = @CID)" + CaseFilterSql("") + @"
ORDER BY CasID DESC", cid);

            lblQualitySummary.Text = "پرونده‌های دارای مشکل کیفیت داده: " + table.Rows.Count;
            dgvQuality.DataSource = table;
        }

        private void LoadAudit()
        {
            if (dgvAudit == null)
                return;

            int cid = SecurityContext.CenterFilterId;
            dgvAudit.DataSource = GetTableCid(@"
SELECT LogID, CreatedAt, Username, Operation, EntityName, EntityID, OldValue, NewValue
FROM TblAuditLog
WHERE (@CID = 0 OR CenterID = @CID)
ORDER BY LogID DESC", cid);
        }

        // ─── جزوه آموزشی ─────────────────────────────────────────────────────
        private void OpenTrainingManual(object sender, EventArgs e)
        {
            string manualPath = Path.Combine(Application.StartupPath, "Manual", "TrainingManual.pdf");

            if (File.Exists(manualPath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = manualPath,
                        UseShellExecute = true
                    });
                    return;
                }
                catch (Exception ex)
                {
                    UiTheme.ShowError(this, "خطا در بازکردن جزوه آموزشی: " + ex.Message);
                    return;
                }
            }

            UiTheme.ShowWarning(this,
                "فایل جزوه آموزشی پیدا نشد." + Environment.NewLine +
                "برای فعال‌سازی، فایل PDF جزوه را در مسیر زیر قرار دهید:" + Environment.NewLine +
                manualPath);
        }

        // ─── ارتباط با ما ────────────────────────────────────────────────────
        private void OpenContactUs(object sender, EventArgs e)
        {
            string orgName = SettingsHelper.Get(SettingsHelper.OrgName);
            string address = SettingsHelper.Get(SettingsHelper.Address);
            string phone   = SettingsHelper.Get(SettingsHelper.Phone);
            string email   = SettingsHelper.Get(SettingsHelper.Email);

            string message =
                (string.IsNullOrWhiteSpace(orgName) ? "" : "مؤسسه: " + orgName + Environment.NewLine) +
                (string.IsNullOrWhiteSpace(address) ? "" : "آدرس: " + address + Environment.NewLine) +
                (string.IsNullOrWhiteSpace(phone)   ? "" : "تلفن: " + phone + Environment.NewLine) +
                (string.IsNullOrWhiteSpace(email)   ? "" : "ایمیل: " + email + Environment.NewLine);

            if (string.IsNullOrWhiteSpace(message))
                message = "اطلاعات تماس هنوز در بخش تنظیمات ثبت نشده است.";

            Msg.Show(message, "ارتباط با ما", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }

        private void OpenUsers(object sender, EventArgs e)
        {
            if (!SecurityContext.IsAdmin())
            {
                UiTheme.ShowWarning(this, "مدیریت کاربران فقط برای مدیر مجاز است.");
                return;
            }

            using (var frm = new FrmUsers())
                frm.ShowDialog(this);
        }

        private void CleanupFiles(object sender, EventArgs e)
        {
            if (!SecurityContext.CanDelete())
            {
                UiTheme.ShowWarning(this, "پاکسازی فایل‌ها فقط برای مدیر مجاز است.");
                return;
            }

            FileCleanupHelper helper = new FileCleanupHelper();
            List<string> unusedFiles = helper.FindUnusedFiles();

            if (unusedFiles.Count == 0)
            {
                UiTheme.ShowSuccess(this, "فایل اضافه‌ای پیدا نشد.");
                return;
            }

            bool confirmed = UiTheme.ShowConfirm(this,
                unusedFiles.Count + " فایل بدون استفاده پیدا شد. حذف شوند؟",
                "پاکسازی فایل‌ها");

            if (!confirmed)
                return;

            int deleted = helper.DeleteFiles(unusedFiles);
            UiTheme.ShowSuccess(this, "تعداد فایل حذف‌شده: " + deleted);
        }

        private DataTable GetTable(string query)
        {
            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();
                return GetTable(con, query);
            }
        }

        private DataTable GetTable(SQLiteConnection con, string query)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                DataTable table = new DataTable();
                da.Fill(table);
                return table;
            }
        }

        // نسخه با پارامتر @CID برای فیلتر مرکز در کوئری‌هایی که FROM TblCase دارند.
        private DataTable GetTableCid(string query, int centerId)
        {
            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();
                return GetTableCid(con, query, centerId);
            }
        }

        private DataTable GetTableCid(SQLiteConnection con, string query, int centerId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            {
                cmd.Parameters.AddWithValue("@CID", centerId);
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable table = new DataTable();
                    da.Fill(table);
                    return table;
                }
            }
        }

        // نسخه با @CID + پارامترهای فیلتر ولایت/ولسوالی (@Prov/@Dist) — برای
        // کوئری‌هایی که علاوه بر فیلتر مرکز، CaseFilterSql هم در آن‌ها تزریق شده.
        private DataTable GetTableCidF(string query, int centerId)
        {
            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();
                return GetTableCidF(con, query, centerId);
            }
        }

        private DataTable GetTableCidF(SQLiteConnection con, string query, int centerId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            {
                cmd.Parameters.AddWithValue("@CID", centerId);
                AddCaseFilterParams(cmd);
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable table = new DataTable();
                    da.Fill(table);
                    return table;
                }
            }
        }

        // ─── تنظیمات نرم‌افزار ──────────────────────────────────────────────────
        private void OpenSettings(object sender, EventArgs e)
        {
            if (!SecurityContext.IsAdmin())
            {
                UiTheme.ShowWarning(this, "تنظیمات فقط برای مدیر مجاز است.");
                return;
            }
            using (var frm = new FrmSettings())
                frm.ShowDialog(this);
        }

        // ─── بارگذاری ComboBox تغییر مرکز (فقط SuperAdmin) ──────────────────
        private void LoadCenterSwitcher()
        {
            if (_cmbCenterSwitch == null) return;
            _cmbCenterSwitch.Items.Clear();
            _cmbCenterSwitch.Items.Add(new CenterSwitchItem(0, "", "★  همه مراکز"));

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT CenterID, CenterCode, CenterName FROM TblCenter
WHERE IsActive = 1 ORDER BY CenterCode", con))
                {
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                            _cmbCenterSwitch.Items.Add(new CenterSwitchItem(
                                Convert.ToInt32(dr["CenterID"]),
                                dr["CenterCode"].ToString(),
                                dr["CenterCode"] + " - " + dr["CenterName"]));
                    }
                }
            }
            catch { }

            // انتخاب مرکز فعلی در ComboBox
            for (int i = 0; i < _cmbCenterSwitch.Items.Count; i++)
            {
                CenterSwitchItem item = (CenterSwitchItem)_cmbCenterSwitch.Items[i];
                if (SecurityContext.IsAllCenters && item.CenterId == 0)
                { _cmbCenterSwitch.SelectedIndex = i; break; }
                if (!SecurityContext.IsAllCenters && item.CenterId == SecurityContext.CurrentCenterId)
                { _cmbCenterSwitch.SelectedIndex = i; break; }
            }

            if (_cmbCenterSwitch.SelectedIndex < 0 && _cmbCenterSwitch.Items.Count > 0)
                _cmbCenterSwitch.SelectedIndex = 0;
        }

        private void CmbCenterSwitch_Changed(object sender, EventArgs e)
        {
            CenterSwitchItem item = _cmbCenterSwitch.SelectedItem as CenterSwitchItem;
            if (item == null) return;

            if (item.CenterId == 0)
                SecurityContext.SelectCenter(0, "", "همه مراکز", allCenters: true);
            else
            {
                string[] parts = item.Display.Split(new[] { " - " }, 2, StringSplitOptions.None);
                string name = parts.Length > 1 ? parts[1] : item.Display;
                SecurityContext.SelectCenter(item.CenterId, item.CenterCode, name);
            }

            RefreshAll();
        }

        private class CenterSwitchItem
        {
            public int    CenterId   { get; }
            public string CenterCode { get; }
            public string Display    { get; }
            public CenterSwitchItem(int id, string code, string display)
            { CenterId = id; CenterCode = code; Display = display; }
            public override string ToString() { return Display; }
        }

        private Button CreateToolButton(string text, string icon, EventHandler handler)
        {
            Button button = UiTheme.CreateButton(text, icon, UiTheme.PrimaryDark);
            button.FlatAppearance.MouseOverBackColor = UiTheme.Primary;
            button.FlatAppearance.MouseDownBackColor = UiTheme.PrimaryLight;
            // فشرده‌تر از دکمه‌های معمول برنامه (فونت/پدینگ کوچک‌تر) تا همه در
            // یک ردیف نوار ابزار جا شوند و مدرن/منظم دیده شوند.
            button.Font = UiTheme.FontBold(8.75f);
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(0, 34);
            button.Padding = new Padding(7, 4, 7, 4);
            button.Margin = new Padding(3, 6, 3, 6);
            button.Click += handler;
            return button;
        }

        private DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            UiTheme.StyleGrid(grid);
            return grid;
        }

        private static readonly Color[] ChartPalette =
        {
            UiTheme.Primary, UiTheme.Success, UiTheme.Warning, UiTheme.Danger,
            UiTheme.PrimaryLight, ColorTranslator.FromHtml("#8E6BAF")
        };

        private Chart CreateChart(string title, SeriesChartType chartType)
        {
            Chart chart = new Chart();
            chart.Dock = DockStyle.Fill;
            chart.BackColor = UiTheme.CardBack;
            chart.Palette = ChartColorPalette.None;
            chart.PaletteCustomColors = ChartPalette;

            ChartArea area = new ChartArea("Main");
            area.BackColor = UiTheme.CardBack;
            area.AxisX.LineColor = UiTheme.Border;
            area.AxisY.LineColor = UiTheme.Border;
            area.AxisX.MajorGrid.LineColor = UiTheme.Border;
            area.AxisY.MajorGrid.LineColor = UiTheme.Border;
            chart.ChartAreas.Add(area);

            Title chartTitle = chart.Titles.Add(title);
            chartTitle.Font = UiTheme.FontBold(11F);
            chartTitle.ForeColor = UiTheme.TextDark;

            Series series = new Series("Data");
            series.ChartType = chartType;
            chart.Series.Add(series);
            return chart;
        }

        private void FillChart(Chart chart, DataTable table, string xColumn, string yColumn, SeriesChartType chartType)
        {
            chart.Series.Clear();
            Series series = new Series("Data");
            series.ChartType = chartType;
            series.IsValueShownAsLabel = true;

            foreach (DataRow row in table.Rows)
                series.Points.AddXY(row[xColumn].ToString(), Convert.ToDecimal(row[yColumn]));

            chart.Series.Add(series);
        }

        private void AddLineSeries(Chart chart, string name, DataTable table)
        {
            Series series = new Series(name);
            series.ChartType = SeriesChartType.Line;
            series.BorderWidth = 3;
            series.MarkerStyle = MarkerStyle.Circle;

            foreach (DataRow row in table.Rows)
                series.Points.AddXY(row["Period"].ToString(), Convert.ToInt32(row["CountValue"]));

            chart.Series.Add(series);
        }

        private void AddSummaryCard(string title, int value, string icon, Color accent)
        {
            AddSummaryCard(summaryPanel, title, value, icon, accent);
        }

        private void AddSummaryCard(FlowLayoutPanel targetPanel, string title, int value, string icon, Color accent)
        {
            Panel card = new Panel();
            card.Width = 200;
            card.Height = 100;
            card.Margin = new Padding(8);
            card.BackColor = UiTheme.CardBack;
            UiTheme.RoundCorners(card, 12);

            Panel stripe = new Panel();
            stripe.Dock = DockStyle.Right;
            stripe.Width = 6;
            stripe.BackColor = accent;
            card.Controls.Add(stripe);

            Label lblIcon = new Label();
            lblIcon.Text = icon;
            lblIcon.Font = new Font("Segoe UI", 16F);
            lblIcon.ForeColor = accent;
            lblIcon.AutoSize = false;
            lblIcon.SetBounds(14, 12, 40, 34);
            lblIcon.TextAlign = ContentAlignment.MiddleCenter;
            card.Controls.Add(lblIcon);

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.Font = UiTheme.Font(9.5F);
            lblTitle.ForeColor = UiTheme.TextMuted;
            lblTitle.AutoSize = false;
            lblTitle.SetBounds(14, 46, card.Width - 30, 20);
            lblTitle.TextAlign = ContentAlignment.MiddleRight;
            card.Controls.Add(lblTitle);

            Label lblValue = new Label();
            lblValue.Text = value.ToString("N0");
            lblValue.Font = UiTheme.FontBold(16F);
            lblValue.ForeColor = UiTheme.TextDark;
            lblValue.AutoSize = false;
            lblValue.SetBounds(14, 64, card.Width - 30, 30);
            lblValue.TextAlign = ContentAlignment.MiddleRight;
            card.Controls.Add(lblValue);

            targetPanel.Controls.Add(card);
        }

        private Label CreateHeaderLabel()
        {
            Label label = new Label();
            label.AutoSize = false;
            label.Dock = DockStyle.Top;
            label.Height = 45;
            label.BackColor = UiTheme.CardBack;
            label.ForeColor = UiTheme.TextDark;
            label.Font = UiTheme.FontBold(11F);
            label.TextAlign = ContentAlignment.MiddleCenter;
            return label;
        }

        private Label CreateLabel(string text, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = UiTheme.Font(9.5F);
            label.ForeColor = UiTheme.TextDark;
            label.SetBounds(x, y, width, 25);
            label.TextAlign = ContentAlignment.MiddleRight;
            return label;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // FrmDashboard
            // 
            this.ClientSize = new System.Drawing.Size(278, 244);
            this.Name = "FrmDashboard";
            this.Load += new System.EventHandler(this.FrmDashboard_Load);
            this.ResumeLayout(false);

        }

        private void FrmDashboard_Load(object sender, EventArgs e)
        {

        }
    }
}

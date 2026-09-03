using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CaseManagement.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // چاپ جمعی کارت شناسایی سرپرست — بازطراحیِ کامل به‌درخواستِ کاربر («ID CARD
    // FILTERING BEFORE PRINT»): قبل از تولیدِ واقعیِ کارت‌ها (که کند و
    // پرمصرف است)، کاربر ابتدا با فیلترهای متعدد فهرست را محدود می‌کند، یک
    // پیش‌نمایشِ فهرستی (نه کارتِ گرافیکی) می‌بیند، و فقط بعد از تأیید صریح
    // (دیالوگِ «N کارت چاپ می‌شود؟») رندرِ واقعی شروع می‌شود.
    //
    // جریانِ دو مرحله‌ای:
    //   ۱) فیلتر + پیش‌نمایشِ گرید (این فرم را باز می‌کند)
    //   ۲) تأیید → رندرِ واقعیِ کارت‌ها در WebView2 (دقیقاً همان مسیرِ قبلی:
    //      CardService.BuildCardDataForCaseIds → GuardianCardRenderer)
    // همان قالب A5 دورو (روی/پشت) که برای یک کارت استفاده می‌شود؛ هر پرونده
    // یک جفت صفحه‌ی روی/پشتِ جداگانه می‌گیرد (نگاه کنید print.css)، پس نتیجه
    // یک سند چندصفحه‌ای پیاپی روی/پشت/روی/پشت/... است.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmGuardianCardBatchPrint : Form
    {
        private WebView2 _webView;
        private Panel _toolbar;
        private DataGridView _gridPreview;

        private ComboBox _cmbProvince;
        private TextBox _txtDistrict;
        private ComboBox _cmbCaseType;
        private ComboBox _cmbServiceStatus;
        private ComboBox _cmbMemberRole;
        private ComboBox _cmbCoveredByOrg;
        private TextBox _txtSearch;
        private CheckBox _chkUseFormRange;
        private NumericUpDown _numFrom;
        private NumericUpDown _numTo;

        private ComboBox _cmbTemplate;
        private List<CardTemplate> _templates = new List<CardTemplate>();
        private Label _lblPrintProfile;

        private Button _btnPreview;
        private Button _btnConfirmPrint;
        private Button _btnBackToFilters;
        private Button _btnPrint;
        private Button _btnPdf;
        private Label _lblStatus;

        private List<int> _previewCaseIds = new List<int>();

        private static readonly string[] Provinces =
        {
            "همه ولایات",
            "کابل", "هرات", "بلخ", "قندهار", "ننگرهار", "بدخشان", "بغلان", "تخار",
            "غزنی", "هلمند", "لغمان", "کندز", "فاریاب", "جوزجان", "سمنگان", "بامیان",
            "پکتیا", "لوگر", "وردک", "غور", "فراه", "خوست", "کاپیسا", "پروان",
            "زابل", "ارزگان", "نیمروز", "نورستان", "کنر", "سرپل", "دایکندی",
            "پکتیکا", "بادغیس", "پنجشیر"
        };

        public FrmGuardianCardBatchPrint()
        {
            // دفاع لایه‌ای: حتی اگر این فرم مستقیم ساخته شود، بدون مجوز
            // «چاپ کارت شناسایی» بلافاصله بسته می‌شود.
            if (!CaseManagement.Enterprise.PermissionService.Require("GuardianCard.Print"))
            {
                Load += delegate { Close(); };
                return;
            }

            BuildUi();
        }

        private void BuildUi()
        {
            Text = "چاپ جمعی کارت شناسایی سرپرست";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1240, 860);
            MinimumSize = new Size(860, 600);

            _toolbar = new Panel { Dock = DockStyle.Top, Height = 152, BackColor = UiTheme.PrimaryDark };
            BuildFilterControls();

            _gridPreview = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            UiTheme.StyleGrid(_gridPreview);
            _gridPreview.Columns.Add("CaseCode", "کد پرونده");
            _gridPreview.Columns.Add("GuardianName", "نام سرپرست");
            _gridPreview.Columns.Add("Province", "ولایت");
            _gridPreview.Columns.Add("District", "ولسوالی");
            _gridPreview.Columns.Add("CaseType", "نوع پرونده");
            _gridPreview.Columns.Add("ServiceStatus", "وضعیت خدمات");
            _gridPreview.Columns.Add("RoleSummary", "وضعیت ایتام (نقش)");

            _webView = new WebView2 { Dock = DockStyle.Fill, Visible = false };

            Controls.Add(_gridPreview);
            Controls.Add(_webView);
            Controls.Add(_toolbar);
        }

        // آموزش — رفعِ ناهماهنگیِ گزارش‌شده («فیلدهایش بسیار نامنظم است»):
        // نسخهٔ قبلی مختصاتِ هر کنترل را دستی و جداگانه حساب می‌کرد و فاصلهٔ
        // برچسب‌تاکنترل/بینِ گروه‌ها بینِ ۲ تا ۱۰ پیکسل نوسان داشت. حالا یک
        // مکان‌یابِ واحد (PlaceField) با گپِ ثابت، هر ردیف را با یک نشانگرِ x
        // پشتِ‌سرِهم می‌چیند — نتیجه یک شبکهٔ کاملاً یکدست است.
        private const int FieldGap = 4;   // فاصلهٔ برچسب تا کنترل
        private const int GroupGap = 18;  // فاصلهٔ بینِ گروه‌های فیلد
        private const int MarginX = 14;

        private void BuildFilterControls()
        {
            int x;

            // ─── ردیف اول ────────────────────────────────────────────────────
            const int row1Y = 12;
            x = MarginX;

            _cmbProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbProvince.Items.AddRange(Provinces);
            _cmbProvince.SelectedIndex = 0;
            Label lblProvince = PlaceField(ref x, row1Y, "ولایت", 46, _cmbProvince, 150);

            _txtDistrict = new TextBox { RightToLeft = RightToLeft.Yes };
            Label lblDistrict = PlaceField(ref x, row1Y, "ولسوالی", 56, _txtDistrict, 140);

            _cmbCaseType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCaseType.Items.Add("همه انواع");
            foreach (string v in LookupHelper.GetValues("RequestType")) _cmbCaseType.Items.Add(v);
            _cmbCaseType.SelectedIndex = 0;
            Label lblCaseType = PlaceField(ref x, row1Y, "نوع پرونده", 68, _cmbCaseType, 150);

            _cmbServiceStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbServiceStatus.Items.Add("همه وضعیت‌ها");
            foreach (string v in LookupHelper.GetValues("ServiceStatus")) _cmbServiceStatus.Items.Add(v);
            _cmbServiceStatus.SelectedIndex = 0;
            Label lblSvc = PlaceField(ref x, row1Y, "وضعیت خدمات", 82, _cmbServiceStatus, 150);

            // ─── ردیف دوم ────────────────────────────────────────────────────
            const int row2Y = 52;
            x = MarginX;

            _cmbMemberRole = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMemberRole.Items.Add("همه نقش‌ها");
            foreach (string v in LookupHelper.GetValues("MemberRole")) _cmbMemberRole.Items.Add(v);
            _cmbMemberRole.SelectedIndex = 0;
            Label lblRole = PlaceField(ref x, row2Y, "نقش عضو (خانواده)", 100, _cmbMemberRole, 140);

            // آموزش — این پروژه موجودیتِ ساختاریافته‌ی «اهداکننده» ندارد؛
            // نزدیک‌ترین معادلِ موجود CoveredByOrg («تحت پوشش مؤسسه دیگر») است.
            _cmbCoveredByOrg = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCoveredByOrg.Items.Add("فرقی نمی‌کند");
            foreach (string v in LookupHelper.GetValues("CoveredByOrg")) _cmbCoveredByOrg.Items.Add(v);
            _cmbCoveredByOrg.SelectedIndex = 0;
            Label lblDonor = PlaceField(ref x, row2Y, "تحت پوشش مؤسسه دیگر", 120, _cmbCoveredByOrg, 120);

            _txtSearch = new TextBox { RightToLeft = RightToLeft.Yes };
            Label lblSearch = PlaceField(ref x, row2Y, "جستجوی آزاد", 78, _txtSearch, 180);

            _chkUseFormRange = new CheckBox
            {
                Text = "محدود به بازهٔ شماره فرم", ForeColor = Color.White, AutoSize = true,
                Font = UiTheme.Font(9.5F)
            };
            _chkUseFormRange.SetBounds(x, row2Y + 2, 172, 22);
            x += 172 + FieldGap;
            _chkUseFormRange.CheckedChanged += delegate
            {
                _numFrom.Enabled = _chkUseFormRange.Checked;
                _numTo.Enabled = _chkUseFormRange.Checked;
            };

            _numFrom = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 1, Enabled = false };
            _numFrom.SetBounds(x, row2Y, 68, 26);
            x += 68 + 6;

            Label lblRangeTo = MakeLabel("تا", x, row2Y, 18);
            x += 18 + 6;

            _numTo = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 999999, Enabled = false };
            _numTo.SetBounds(x, row2Y, 68, 26);

            // ─── ردیف سوم: دکمه‌ها + قالب + وضعیت ──────────────────────────────
            const int row3Y = 92;
            const int btnH = 36;
            x = MarginX;

            _btnPreview = UiTheme.CreateButton("پیش‌نمایش فهرست", "⌕", UiTheme.Primary);
            _btnPreview.SetBounds(x, row3Y, 150, btnH);
            _btnPreview.Click += delegate { RunPreview(); };
            x += 150 + 8;

            _btnConfirmPrint = UiTheme.CreateButton("تأیید و آماده‌سازی چاپ", "✓", UiTheme.Success);
            _btnConfirmPrint.SetBounds(x, row3Y, 175, btnH);
            _btnConfirmPrint.Enabled = false;
            _btnConfirmPrint.Click += async delegate { await ConfirmAndRenderAsync(); };
            x += 175 + 8;

            _btnBackToFilters = UiTheme.CreateSecondaryButton("بازگشت به فیلترها", "↺");
            _btnBackToFilters.SetBounds(x, row3Y, 150, btnH);
            _btnBackToFilters.Visible = false;
            _btnBackToFilters.Click += delegate { ShowFilterStage(); };
            x += 150 + 8;

            _btnPrint = UiTheme.CreateButton("چاپ", "🖨", UiTheme.Success);
            _btnPrint.SetBounds(x, row3Y, 85, btnH);
            _btnPrint.Enabled = false;
            _btnPrint.Click += delegate { ShowPrintDialog(); };
            x += 85 + 8;

            _btnPdf = UiTheme.CreateButton("ذخیره PDF", "📄", UiTheme.Primary);
            _btnPdf.SetBounds(x, row3Y, 115, btnH);
            _btnPdf.Enabled = false;
            _btnPdf.Click += async delegate { await SaveAsPdfAsync(); };
            x += 115 + GroupGap;

            // آموزش — برچسب/کمبویِ قالب حالا دقیقاً هم‌مرکز با دکمه‌های همین
            // ردیف‌اند (به‌جای y=۸۴/۸۶ی قبلی که با دکمه‌های y=۷۸ هم‌ترازِ
            // واقعی نبود).
            int fieldY = row3Y + (btnH - 26) / 2;
            _cmbTemplate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            Label lblTemplate = MakeLabel("قالب کارت", x, fieldY, 62);
            x += 62 + FieldGap;
            _cmbTemplate.SetBounds(x, fieldY, 150, 26);
            x += 150 + GroupGap;

            // آموزش — Phase 2 («پروفایل‌هایِ چاپ»): این فاز فقط پروفایلِ PVC
            // (رفتارِ امروزیِ print.css، بدونِ تغییر) پیاده‌سازی شده — این
            // برچسبِ فقط‌خواندنی صرفاً نشان می‌دهد کدام پروفایل استفاده
            // می‌شود؛ یک سوییچِ واقعیِ چندپروفایلی (A4/چندکارتی) به فازِ
            // بعدی موکول شد تا print.css/صفحه‌بندیِ تثبیت‌شده لمس نشود.
            _lblPrintProfile = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White, Font = UiTheme.Font(8.5F)
            };
            _lblPrintProfile.SetBounds(x, fieldY, 150, 26);
            x += 150 + GroupGap;
            _cmbTemplate.SelectedIndexChanged += delegate { UpdatePrintProfileLabel(); };

            LoadTemplateList();
            UpdatePrintProfileLabel();

            _lblStatus = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White, Font = UiTheme.Font(9.5F)
            };
            _lblStatus.SetBounds(x, row3Y, 260, btnH);

            _toolbar.Controls.AddRange(new Control[]
            {
                lblProvince, _cmbProvince, lblDistrict, _txtDistrict, lblCaseType, _cmbCaseType, lblSvc, _cmbServiceStatus,
                lblRole, _cmbMemberRole, lblDonor, _cmbCoveredByOrg, lblSearch, _txtSearch,
                _chkUseFormRange, _numFrom, lblRangeTo, _numTo,
                _btnPreview, _btnConfirmPrint, _btnBackToFilters, _btnPrint, _btnPdf,
                lblTemplate, _cmbTemplate, _lblPrintProfile, _lblStatus
            });
        }

        // آموزش — Phase 2: فقط یک برچسبِ نمایشی؛ هیچ منطقِ چاپ/صفحه‌بندی
        // بر اساسِ این تغییر نمی‌کند (فقط پروفایلِ PVC پیاده‌سازی شده است).
        private void UpdatePrintProfileLabel()
        {
            if (_lblPrintProfile == null) return;
            int idx = _cmbTemplate.SelectedIndex;
            CardTemplate t = (idx >= 0 && idx < _templates.Count) ? _templates[idx] : null;
            string profile = t != null && !string.IsNullOrWhiteSpace(t.PrintProfile) ? t.PrintProfile : "PVC";
            _lblPrintProfile.Text = "پروفایلِ چاپ: " + profile + " (کارتِ واقعی)";
        }

        // مکان‌یابِ یک گروهِ «برچسب + کنترل» روی نشانگرِ x، با گپِ ثابت
        // (FieldGap بینِ برچسب‌وکنترل، GroupGap بعد از پایانِ گروه). x را
        // برایِ گروهِ بعدی جلو می‌برد.
        private static Label PlaceField(ref int x, int y, string label, int labelW, Control control, int controlW)
        {
            Label lbl = MakeLabel(label, x, y, labelW);
            x += labelW + FieldGap;
            control.SetBounds(x, y, controlW, 26);
            x += controlW + GroupGap;
            return lbl;
        }

        private static Label MakeLabel(string text, int x, int y, int width)
        {
            var lbl = new Label
            {
                Text = text, ForeColor = Color.White, AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.Font(9.5F)
            };
            lbl.SetBounds(x, y, width, 24);
            return lbl;
        }

        private void LoadTemplateList()
        {
            try
            {
                _templates = new CardTemplateRepository().GetAll();
            }
            catch
            {
                _templates = new List<CardTemplate>();
            }

            _cmbTemplate.Items.Clear();
            foreach (var t in _templates)
                _cmbTemplate.Items.Add(t.Name);
            if (_cmbTemplate.Items.Count > 0)
                _cmbTemplate.SelectedIndex = 0;
        }

        // ─── مرحله ۱: پیش‌نمایش فهرستی (بدون تولید کارت واقعی) ────────────────
        private void RunPreview()
        {
            SetStatus("در حال جستجو...");
            _gridPreview.Rows.Clear();
            _previewCaseIds.Clear();
            _btnConfirmPrint.Enabled = false;

            try
            {
                var filter = new GuardianCardBatchFilter
                {
                    Province = _cmbProvince.SelectedIndex <= 0 ? "" : _cmbProvince.Text,
                    District = _txtDistrict.Text.Trim(),
                    CaseType = _cmbCaseType.SelectedIndex <= 0 ? "" : _cmbCaseType.Text,
                    ServiceStatus = _cmbServiceStatus.SelectedIndex <= 0 ? "" : _cmbServiceStatus.Text,
                    MemberRole = _cmbMemberRole.SelectedIndex <= 0 ? "" : _cmbMemberRole.Text,
                    CoveredByOrg = _cmbCoveredByOrg.SelectedIndex <= 0 ? "" : _cmbCoveredByOrg.Text,
                    SearchText = _txtSearch.Text.Trim(),
                    UseFormNoRange = _chkUseFormRange.Checked,
                    FromFormNo = (int)_numFrom.Value,
                    ToFormNo = (int)_numTo.Value
                };

                if (filter.UseFormNoRange && filter.ToFormNo < filter.FromFormNo)
                {
                    SetStatus("");
                    Msg.Show("«تا شماره فرم» باید بزرگ‌تر یا مساوی «از شماره فرم» باشد.");
                    return;
                }

                var repo = new CaseCardRepository();
                bool truncated;
                var rows = repo.PreviewBatch(filter, out truncated);

                foreach (var row in rows)
                {
                    _previewCaseIds.Add(row.CasID);
                    _gridPreview.Rows.Add(row.CaseCode, row.GuardianName, row.Province, row.District,
                        row.CaseType, row.ServiceStatus, row.RoleSummary);
                }

                if (rows.Count == 0)
                {
                    SetStatus("");
                    Msg.Show("با این فیلترها هیچ پرونده‌ای پیدا نشد.");
                    return;
                }

                _btnConfirmPrint.Enabled = true;
                string statusText = rows.Count + " پرونده یافت شد";
                if (truncated) statusText += " (بیش از ۵۰۰ نتیجه — فیلتر را محدودتر کنید تا فهرست کامل دیده شود)";
                SetStatus(statusText);
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("خطا در پیش‌نمایش فهرست: " + ex.Message);
            }
        }

        // ─── مرحله ۲: تأیید صریح کاربر، سپس رندرِ واقعیِ کارت‌ها ───────────────
        private async System.Threading.Tasks.Task ConfirmAndRenderAsync()
        {
            if (_previewCaseIds.Count == 0) return;

            DialogResult confirm = MessageBox.Show(
                _previewCaseIds.Count + " کارت شناسایی چاپ/آماده می‌شود. ادامه می‌دهید؟",
                "تأیید چاپ جمعی", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

            if (confirm != DialogResult.Yes) return;

            SetStatus("در حال آماده‌سازی کارت‌ها...");

            try
            {
                string runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrEmpty(runtimeVersion))
                    throw new WebView2RuntimeNotFoundException();
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("برای نمایش کارت‌ها به «Microsoft Edge WebView2 Runtime» نیاز است.\n" + ex.Message);
                return;
            }

            try
            {
                await EnsureWebViewReadyAsync();

                var cardService = new CardService();
                int failedCount;
                var items = cardService.BuildCardDataForCaseIds(_previewCaseIds, out failedCount);

                if (items.Count == 0)
                {
                    SetStatus("");
                    Msg.Show("هیچ کارتی قابل تولید نبود.");
                    return;
                }

                var renderer = new GuardianCardRenderer();
                string orgLogoPath = SettingsHelper.Get(SettingsHelper.LogoPath);
                string signaturePath = SettingsHelper.Get(SettingsHelper.SignaturePath);
                string stampPath = SettingsHelper.Get(SettingsHelper.StampPath);

                // آموزش — اِعمالِ قالبِ انتخاب‌شده («CARD TEMPLATE MANAGEMENT»):
                // فیلدهای متنی روی خودِ هر آیتم خالی می‌شوند؛ Logo/Signature/
                // Stamp (که مؤسسه‌ای و مشترکِ کل دسته‌اند) با خالی‌کردنِ همان
                // سه مسیرِ مبدأ که به StageAndPopulateBatch داده می‌شود خاموش
                // می‌شوند — نه data.Logo (که Renderer آن را دوباره از رویِ
                // همین مسیرها می‌سازد؛ نگاه کنید توضیحِ ApplyTextFields).
                int templateIdx = _cmbTemplate.SelectedIndex;
                CardTemplate template = (templateIdx >= 0 && templateIdx < _templates.Count) ? _templates[templateIdx] : null;
                string layoutVariant = template != null ? template.LayoutVariant : "Full";
                var disabledFields = new List<string>();

                // آموزش — رفعِ ناهماهنگیِ گزارش‌شده («چاپِ جمعی با تنظیماتِ
                // قالب هماهنگ نیست»): این فرم تا الان همیشه طرحِ «کامل» را
                // فرض می‌کرد؛ اگر قالبِ انتخاب‌شده «ساده» بود، انتخابش عملاً
                // نادیده گرفته می‌شد. حالا دقیقاً هم‌الگویِ
                // FrmGuardianCardPreview.LoadCardAsync شاخه می‌زند.
                if (template != null && layoutVariant == "Simple")
                {
                    foreach (string field in CardTemplateRepository.ToggleableFieldsSimple)
                        if (!CardTemplateRepository.IsFieldEnabled(template, field))
                            disabledFields.Add(field);
                }
                else if (template != null)
                {
                    foreach (var item in items)
                        CardTemplateRepository.ApplyTextFields(item, template);

                    if (!CardTemplateRepository.IsFieldEnabled(template, "Logo")) orgLogoPath = "";
                    if (!CardTemplateRepository.IsFieldEnabled(template, "Signature")) signaturePath = "";
                    if (!CardTemplateRepository.IsFieldEnabled(template, "Stamp")) stampPath = "";

                    foreach (string field in CardTemplateRepository.ToggleableFields)
                        if (!CardTemplateRepository.IsFieldEnabled(template, field))
                            disabledFields.Add(field);

                    if (!template.Design.ShowQRCode) disabledFields.Add("QRCode");
                    if (!template.Design.ShowBarcode) disabledFields.Add("Barcode");
                    if (!template.Design.HologramEnabled) disabledFields.Add("Hologram");
                }

                string workingFolder = renderer.StageAndPopulateBatch(
                    items,
                    data => data.Photo,
                    orgLogoPath,
                    signaturePath,
                    stampPath,
                    disabledFields: disabledFields,
                    design: template != null ? template.Design : null,
                    layoutVariant: layoutVariant);

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    GuardianCardRenderer.VirtualHostName, workingFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                // آموزش — رفعِ باگِ «سیاهیِ بالای کارت در پیش‌نمایشِ چاپِ جمعی»
                // + رفعِ باگِ حیاتیِ گزارش‌شده («چاپِ جمعی از صفحهٔ دوم به بعد
                // قالبِ قدیمی/خام برمی‌گرداند»): NavigationCompleted فقط یعنی
                // سندِ HTML بارگذاری شده — نه اینکه fetch(SAMPLE_DATA.json) +
                // populateBatch (که در guardian-card.js آسنکرون است و برایِ
                // دسته‌هایِ بزرگ می‌تواند صدها میلی‌ثانیه طول بکشد) هم واقعاً
                // تمام شده باشد. اگر چاپ/PDF زودتر از این فعال می‌شد، ممکن
                // بود کاربر پیش از اتمامِ واقعیِ رندر کلیک کند و صفحاتِ
                // انتهاییِ سند را هنوز به‌صورتِ Cloneِ خام/پرنشده ببیند. حالا
                // دقیقاً هم‌الگویِ راه‌حلِ از قبل اثبات‌شدهٔ همین پروژه برایِ
                // همین دقیقاً همین باگ عمل می‌کنیم (نگاه کنید
                // AssistanceReceiptIntegration/FrmAssistancePackageBatchPrint.
                // cs:NavigateAndWaitForRenderAsync) — علاوه بر
                // NavigationCompleted، منتظرِ پیامِ صریحِ
                // «guardiancard:renderComplete» هم می‌مانیم که guardian-card.js
                // بعد از اتمامِ کاملِ populateBatch/populateCard (چه موفق چه
                // با خطا) با postMessage می‌فرستد. هر دو handler پیش از
                // Navigate ثبت می‌شوند تا رویدادی که خیلی زود رخ می‌دهد از
                // دست نرود. Task.Delay فقط شبکهٔ ایمنی است تا اگر پیام هرگز
                // نرسید (مثلاً خطای غیرمنتظرهٔ جاوااسکریپت)، UI برای همیشه
                // گیر نکند.
                var navDone = new System.Threading.Tasks.TaskCompletionSource<bool>();
                EventHandler<CoreWebView2NavigationCompletedEventArgs> onNavDone = null;
                onNavDone = delegate
                {
                    _webView.CoreWebView2.NavigationCompleted -= onNavDone;
                    navDone.TrySetResult(true);
                };
                _webView.CoreWebView2.NavigationCompleted += onNavDone;

                var renderDone = new System.Threading.Tasks.TaskCompletionSource<bool>();
                EventHandler<CoreWebView2WebMessageReceivedEventArgs> onRenderDone = null;
                onRenderDone = delegate (object s, CoreWebView2WebMessageReceivedEventArgs e)
                {
                    if (e.TryGetWebMessageAsString() == "guardiancard:renderComplete")
                    {
                        _webView.CoreWebView2.WebMessageReceived -= onRenderDone;
                        renderDone.TrySetResult(true);
                    }
                };
                _webView.CoreWebView2.WebMessageReceived += onRenderDone;

                string page = layoutVariant == "Simple" ? "simple.html" : "index.html";
                _webView.CoreWebView2.Navigate("https://" + GuardianCardRenderer.VirtualHostName + "/" + page);

                await navDone.Task;
                await System.Threading.Tasks.Task.WhenAny(renderDone.Task, System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(20)));
                if (!renderDone.Task.IsCompleted)
                    _webView.CoreWebView2.WebMessageReceived -= onRenderDone;

                ShowRenderStage();

                string statusText = items.Count + " کارت آماده شد";
                if (failedCount > 0)
                    statusText += " — " + failedCount + " پرونده با خطا رد شد";
                SetStatus(statusText);

                _btnPrint.Enabled = true;
                _btnPdf.Enabled = true;
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("خطا در آماده‌سازی چاپ جمعی: " + ex.Message);
            }
        }

        private void ShowRenderStage()
        {
            _gridPreview.Visible = false;
            _webView.Visible = true;
            _btnBackToFilters.Visible = true;
        }

        private void ShowFilterStage()
        {
            _webView.Visible = false;
            _gridPreview.Visible = true;
            _btnBackToFilters.Visible = false;
            _btnPrint.Enabled = false;
            _btnPdf.Enabled = false;
        }

        private async System.Threading.Tasks.Task EnsureWebViewReadyAsync()
        {
            if (_webView.CoreWebView2 != null) return;

            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CaseManagement", "WebView2UserData");
            Directory.CreateDirectory(userDataFolder);

            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);
        }

        private void ShowPrintDialog()
        {
            if (_webView.CoreWebView2 == null) return;
            try
            {
                _webView.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در چاپ: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task SaveAsPdfAsync()
        {
            if (_webView.CoreWebView2 == null) return;

            using (var sfd = new SaveFileDialog
            {
                Filter = "فایل PDF|*.pdf",
                FileName = "کارت-شناسایی-جمعی.pdf"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    SetStatus("در حال ساخت PDF...");
                    bool ok = await _webView.CoreWebView2.PrintToPdfAsync(sfd.FileName);
                    SetStatus("");
                    if (ok)
                        UiTheme.ShowSuccess(this, "فایل PDF ذخیره شد:\n" + sfd.FileName);
                    else
                        Msg.Show("ساخت PDF ناموفق بود.");
                }
                catch (Exception ex)
                {
                    SetStatus("");
                    Msg.Show("خطا در ساخت PDF: " + ex.Message);
                }
            }
        }

        private void SetStatus(string text)
        {
            if (_lblStatus.InvokeRequired) { _lblStatus.Invoke((Action)(() => _lblStatus.Text = text)); return; }
            _lblStatus.Text = text;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_webView != null)
            {
                _webView.Dispose();
                _webView = null;
            }
        }
    }
}

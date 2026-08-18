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

            _toolbar = new Panel { Dock = DockStyle.Top, Height = 140, BackColor = UiTheme.PrimaryDark };
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

        private void BuildFilterControls()
        {
            Label lblProvince = MakeLabel("ولایت", 14, 10, 60);
            _cmbProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbProvince.Items.AddRange(Provinces);
            _cmbProvince.SelectedIndex = 0;
            _cmbProvince.SetBounds(76, 8, 140, 26);

            Label lblDistrict = MakeLabel("ولسوالی", 224, 10, 60);
            _txtDistrict = new TextBox { RightToLeft = RightToLeft.Yes };
            _txtDistrict.SetBounds(286, 8, 130, 26);

            Label lblCaseType = MakeLabel("نوع پرونده", 424, 10, 68);
            _cmbCaseType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCaseType.Items.Add("همه انواع");
            foreach (string v in LookupHelper.GetValues("RequestType")) _cmbCaseType.Items.Add(v);
            _cmbCaseType.SelectedIndex = 0;
            _cmbCaseType.SetBounds(496, 8, 140, 26);

            Label lblSvc = MakeLabel("وضعیت خدمات", 644, 10, 82);
            _cmbServiceStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbServiceStatus.Items.Add("همه وضعیت‌ها");
            foreach (string v in LookupHelper.GetValues("ServiceStatus")) _cmbServiceStatus.Items.Add(v);
            _cmbServiceStatus.SelectedIndex = 0;
            _cmbServiceStatus.SetBounds(730, 8, 130, 26);

            // ─── ردیف دوم ────────────────────────────────────────────────────
            Label lblRole = MakeLabel("نقش عضو (خانواده)", 14, 44, 100);
            _cmbMemberRole = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMemberRole.Items.Add("همه نقش‌ها");
            foreach (string v in LookupHelper.GetValues("MemberRole")) _cmbMemberRole.Items.Add(v);
            _cmbMemberRole.SelectedIndex = 0;
            _cmbMemberRole.SetBounds(116, 42, 130, 26);

            // آموزش — این پروژه موجودیتِ ساختاریافته‌ی «اهداکننده» ندارد؛
            // نزدیک‌ترین معادلِ موجود CoveredByOrg («تحت پوشش مؤسسه دیگر») است.
            Label lblDonor = MakeLabel("تحت پوشش مؤسسه دیگر", 254, 44, 116);
            _cmbCoveredByOrg = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCoveredByOrg.Items.Add("فرقی نمی‌کند");
            foreach (string v in LookupHelper.GetValues("CoveredByOrg")) _cmbCoveredByOrg.Items.Add(v);
            _cmbCoveredByOrg.SelectedIndex = 0;
            _cmbCoveredByOrg.SetBounds(374, 42, 110, 26);

            Label lblSearch = MakeLabel("جستجوی آزاد", 492, 44, 76);
            _txtSearch = new TextBox { RightToLeft = RightToLeft.Yes };
            _txtSearch.SetBounds(572, 42, 160, 26);

            _chkUseFormRange = new CheckBox
            {
                Text = "محدود به بازهٔ شماره فرم", ForeColor = Color.White, AutoSize = true,
                Font = UiTheme.Font(9.5F)
            };
            _chkUseFormRange.SetBounds(742, 44, 170, 24);
            _chkUseFormRange.CheckedChanged += delegate
            {
                _numFrom.Enabled = _chkUseFormRange.Checked;
                _numTo.Enabled = _chkUseFormRange.Checked;
            };

            _numFrom = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 1, Enabled = false };
            _numFrom.SetBounds(918, 42, 70, 26);
            _numTo = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 999999, Enabled = false };
            _numTo.SetBounds(994, 42, 70, 26);

            // ─── ردیف سوم: دکمه‌ها ───────────────────────────────────────────
            _btnPreview = UiTheme.CreateButton("پیش‌نمایش فهرست", "⌕", UiTheme.Primary);
            _btnPreview.SetBounds(14, 78, 150, 36);
            _btnPreview.Click += delegate { RunPreview(); };

            _btnConfirmPrint = UiTheme.CreateButton("تأیید و آماده‌سازی چاپ", "✓", UiTheme.Success);
            _btnConfirmPrint.SetBounds(172, 78, 175, 36);
            _btnConfirmPrint.Enabled = false;
            _btnConfirmPrint.Click += async delegate { await ConfirmAndRenderAsync(); };

            _btnBackToFilters = UiTheme.CreateSecondaryButton("بازگشت به فیلترها", "↺");
            _btnBackToFilters.SetBounds(355, 78, 150, 36);
            _btnBackToFilters.Visible = false;
            _btnBackToFilters.Click += delegate { ShowFilterStage(); };

            _btnPrint = UiTheme.CreateButton("چاپ", "🖨", UiTheme.Success);
            _btnPrint.SetBounds(515, 78, 85, 36);
            _btnPrint.Enabled = false;
            _btnPrint.Click += delegate { ShowPrintDialog(); };

            _btnPdf = UiTheme.CreateButton("ذخیره PDF", "📄", UiTheme.Primary);
            _btnPdf.SetBounds(606, 78, 115, 36);
            _btnPdf.Enabled = false;
            _btnPdf.Click += async delegate { await SaveAsPdfAsync(); };

            Label lblTemplate = MakeLabel("قالب کارت", 730, 86, 62);
            _cmbTemplate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbTemplate.SetBounds(795, 84, 140, 26);
            LoadTemplateList();

            _lblStatus = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White, Font = UiTheme.Font(9.5F)
            };
            _lblStatus.SetBounds(944, 78, 270, 36);

            _toolbar.Controls.AddRange(new Control[]
            {
                lblProvince, _cmbProvince, lblDistrict, _txtDistrict, lblCaseType, _cmbCaseType, lblSvc, _cmbServiceStatus,
                lblRole, _cmbMemberRole, lblDonor, _cmbCoveredByOrg, lblSearch, _txtSearch,
                _chkUseFormRange, _numFrom, _numTo,
                _btnPreview, _btnConfirmPrint, _btnBackToFilters, _btnPrint, _btnPdf,
                lblTemplate, _cmbTemplate, _lblStatus
            });
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
                var disabledFields = new List<string>();
                if (template != null)
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
                    design: template != null ? template.Design : null);

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    GuardianCardRenderer.VirtualHostName, workingFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                _webView.CoreWebView2.Navigate("https://" + GuardianCardRenderer.VirtualHostName + "/index.html");

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

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CaseManagement.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه نمایش (Presentation) — میزبان WebView2 برای پیش‌نمایش/چاپ/PDF کارت
    // شناسایی سرپرست. این فرم هیچ منطق تجاری/دیتابیسی ندارد (آن‌ها در
    // CardService/CaseCardRepository/GuardianCardRenderer هستند) — تنها
    // مسئولیتش نمایش و کنترل‌های چاپ است.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmGuardianCardPreview : Form
    {
        private readonly int _caseId;
        private WebView2 _webView;
        private Panel _toolbar;
        private Label _lblStatus;
        // آموزش — آخرین دادهٔ رندرشده روی صفحه؛ لازم برای حالتِ «فقط برای این
        // چاپ» در FrmCardNoticesEdit (که باید همین آبجکت را، نه یک نسخهٔ تازه
        // از دیتابیس، override و دوباره رندر کند).
        private GuardianCardData _currentData;

        // آموزش — رفعِ باگِ بالقوه: GuardianCardRenderer.StageAndPopulate مقدارِ
        // data.Photo/Logo/Signature/Stamp را از مسیرِ مطلقِ مبدأ به مسیرِ نسبیِ
        // staged شده تغییر می‌دهد (هر دو در همان پراپرتی). اگر «فقط برای این
        // چاپ» یک کپیِ همان آبجکت را دوباره رندر کند، این پراپرتی‌ها دیگر
        // مسیرِ مطلقِ اصلی نیستند و StageImage با «فایل پیدا نشد» شکست
        // می‌خورد (عکس از کارت گم می‌شود). پس مسیرهای مطلقِ اصلی یک‌بار، در
        // اولین بارگذاریِ واقعی از DB، اینجا ذخیره می‌شوند و RenderDataAsync
        // همیشه از همین‌ها استفاده می‌کند — نه از data.Photo/Logo/...
        private string _originalPhotoPath;
        private string _originalLogoPath;
        private string _originalSignaturePath;
        private string _originalStampPath;

        private ComboBox _cmbTemplate;
        private System.Collections.Generic.List<CardTemplate> _templates = new System.Collections.Generic.List<CardTemplate>();
        // آموزش — فیلدهایی که قالبِ انتخاب‌شده خاموش کرده؛ RenderDataAsync
        // همیشه از همین فهرست استفاده می‌کند (نه از مقدارِ خالیِ data)، چون
        // مسیرِ «فقط برای این چاپ» (FrmCardNoticesEdit) هم باید همان قالب را
        // حفظ کند. نگاه کنید GuardianCardRenderer.ApplyDisabledFieldCleanup.
        private System.Collections.Generic.List<string> _disabledFields = new System.Collections.Generic.List<string>();
        // آموزش — طراحِ (رنگ/فونت/پس‌زمینه/واترمارک) قالبِ انتخاب‌شدهٔ فعلی؛
        // RenderDataAsync همیشه از همین استفاده می‌کند تا هم LoadCardAsync
        // هم مسیرِ «فقط برای این چاپ» یکسان رفتار کنند.
        private CardTemplateDesign _currentDesign;

        // آموزش — قالبِ اولیهٔ اختیاری: فقط از FrmCardTemplateManager (دکمهٔ
        // «پیش‌نمایش») استفاده می‌شود تا قالبِ در حالِ ویرایش، نه اولین قالبِ
        // فهرست، از همان اول انتخاب‌شده باشد.
        private readonly int _initialTemplateId;

        public FrmGuardianCardPreview(int caseId, int initialTemplateId = 0)
        {
            _caseId = caseId;
            _initialTemplateId = initialTemplateId;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "کارت شناسایی سرپرست";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 820);
            MinimumSize = new Size(760, 560);

            _toolbar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.PrimaryDark };

            var btnPrint = UiTheme.CreateButton("چاپ", "🖨", UiTheme.Success);
            btnPrint.SetBounds(14, 8, 110, 36);
            btnPrint.Click += delegate { ShowPrintDialog(); };

            var btnPdf = UiTheme.CreateButton("ذخیره PDF", "📄", UiTheme.Primary);
            btnPdf.SetBounds(132, 8, 130, 36);
            btnPdf.Click += async delegate { await SaveAsPdfAsync(); };

            var btnRefresh = UiTheme.CreateSecondaryButton("بازخوانی", "↺");
            btnRefresh.SetBounds(270, 8, 110, 36);
            btnRefresh.Click += async delegate { await LoadCardAsync(); };

            // آموزش — پاسخ به درخواست کاربر: شرایط/هشدارهای کارت باید برای هر
            // پرونده قابل ویرایش باشد، نه فقط یک متن سراسری. این دکمه دیالوگ
            // ویرایش (FrmCardNoticesEdit) را باز می‌کند و بعد از ذخیره، کارت
            // را دوباره می‌سازد تا تغییر فوراً دیده شود.
            var btnEditNotices = UiTheme.CreateSecondaryButton("ویرایش محتوای کارت", "✎");
            btnEditNotices.SetBounds(388, 8, 150, 36);
            btnEditNotices.Click += async delegate
            {
                if (_currentData == null)
                {
                    Msg.Show("اول کارت را بارگذاری کن.");
                    return;
                }

                using (var frm = new FrmCardNoticesEdit(_caseId, _currentData))
                {
                    if (frm.ShowDialog(this) != DialogResult.OK) return;

                    if (frm.SavedPermanently)
                        // ذخیرهٔ دائمی — از دیتابیس دوباره می‌خوانیم تا مطمئن
                        // شویم دقیقاً همان چیزی که از این پس همیشه چاپ می‌شود
                        // را می‌بینیم.
                        await LoadCardAsync();
                    else
                        // فقط برای این چاپ — بدون هیچ خواندنِ دوباره از DB،
                        // مستقیم همان آبجکتِ override‌شده رندر می‌شود.
                        await RenderDataAsync(frm.PrintOnlyData);
                }
            };

            // آموزش — انتخابِ قالب («CARD TEMPLATE MANAGEMENT»): فهرست از
            // TblCardTemplate پر می‌شود؛ تغییرش یعنی بازسازیِ کامل از DB (تا
            // فیلدهایی که قالبِ قبلی خاموش کرده بود، اگر قالبِ جدید روشن‌شان
            // می‌کند، درست دوباره پر شوند).
            _cmbTemplate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbTemplate.SetBounds(544, 8, 160, 36);
            LoadTemplateList(); // بدون handler — رندرِ اول با رویدادِ Load انجام می‌شود
            _cmbTemplate.SelectedIndexChanged += async delegate { await LoadCardAsync(); };

            _lblStatus = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White, Font = UiTheme.Font(9.5F)
            };
            _lblStatus.SetBounds(712, 8, 380, 36);

            _toolbar.Controls.Add(btnPrint);
            _toolbar.Controls.Add(btnPdf);
            _toolbar.Controls.Add(btnRefresh);
            _toolbar.Controls.Add(btnEditNotices);
            _toolbar.Controls.Add(_cmbTemplate);
            _toolbar.Controls.Add(_lblStatus);

            _webView = new WebView2 { Dock = DockStyle.Fill };

            Controls.Add(_webView);
            Controls.Add(_toolbar);

            Load += async delegate { await LoadCardAsync(); };
        }

        private void LoadTemplateList()
        {
            try
            {
                _templates = new CardTemplateRepository().GetAll();
            }
            catch
            {
                _templates = new System.Collections.Generic.List<CardTemplate>();
            }

            _cmbTemplate.Items.Clear();
            foreach (var t in _templates)
                _cmbTemplate.Items.Add(t.Name);

            if (_cmbTemplate.Items.Count == 0) return;

            int selectIdx = 0;
            if (_initialTemplateId > 0)
                for (int i = 0; i < _templates.Count; i++)
                    if (_templates[i].TemplateID == _initialTemplateId) { selectIdx = i; break; }

            _cmbTemplate.SelectedIndex = selectIdx;
        }

        private async System.Threading.Tasks.Task LoadCardAsync()
        {
            SetStatus("در حال آماده‌سازی کارت...");

            // آموزش — بررسی نصب‌بودن WebView2 Runtime پیش از هر کاری. اگر نصب
            // نباشد، CreateAsync/GetAvailableBrowserVersionString استثنا
            // می‌دهد؛ پیام دوستانه با راهنمای نصب نمایش داده می‌شود (نیازمندی
            // صریح کاربر) به‌جای کرش خام یا پیام فنی نامفهوم.
            string runtimeVersion;
            try
            {
                runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrEmpty(runtimeVersion))
                    throw new WebView2RuntimeNotFoundException();
            }
            catch (WebView2RuntimeNotFoundException)
            {
                ShowRuntimeMissingMessage();
                return;
            }
            catch (Exception ex)
            {
                ShowRuntimeMissingMessage(ex.Message);
                return;
            }

            try
            {
                var cardService = new CardService();
                GuardianCardData data = cardService.BuildCardData(_caseId);

                // مسیرهای مطلقِ مبدأ فقط همین‌جا (بارگذاریِ تازه از DB) درست‌اند؛
                // نگاه کنید توضیحِ فیلدهای _original* بالا.
                _originalPhotoPath = data.Photo;
                _originalLogoPath = data.Logo;
                _originalSignaturePath = data.Signature;
                _originalStampPath = data.Stamp;

                int templateIdx = _cmbTemplate.SelectedIndex;
                CardTemplate template = (templateIdx >= 0 && templateIdx < _templates.Count) ? _templates[templateIdx] : null;
                _disabledFields.Clear();
                _currentDesign = template != null ? template.Design : null;
                if (template != null)
                {
                    CardTemplateRepository.ApplyTextFields(data, template);
                    // آموزش — Logo/Signature/Stamp باید در سطحِ *مسیرِ مبدأ*
                    // خاموش شوند، نه data.Logo (نگاه کنید توضیحِ ApplyTextFields).
                    if (!CardTemplateRepository.IsFieldEnabled(template, "Logo")) _originalLogoPath = "";
                    if (!CardTemplateRepository.IsFieldEnabled(template, "Signature")) _originalSignaturePath = "";
                    if (!CardTemplateRepository.IsFieldEnabled(template, "Stamp")) _originalStampPath = "";

                    foreach (string field in CardTemplateRepository.ToggleableFields)
                        if (!CardTemplateRepository.IsFieldEnabled(template, field))
                            _disabledFields.Add(field);

                    // آموزش — QRCode/Barcode/Hologram جدا از ToggleableFields
                    // متنی‌اند (نگاه کنید CardTemplateDesign)؛ پیش‌فرضشان با
                    // متن‌ها فرق دارد (QR پیش‌فرض خاموش، بارکد/هولوگرام
                    // پیش‌فرض روشن) پس مستقیم از Design خوانده می‌شوند.
                    if (!template.Design.ShowQRCode) _disabledFields.Add("QRCode");
                    if (!template.Design.ShowBarcode) _disabledFields.Add("Barcode");
                    if (!template.Design.HologramEnabled) _disabledFields.Add("Hologram");
                }

                await RenderDataAsync(data);
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("خطا در نمایش کارت شناسایی: " + ex.Message);
            }
        }

        // آموزش — لایهٔ رندرِ خالص: هیچ‌جا خودش داده از DB نمی‌خواند؛ فقط یک
        // GuardianCardData می‌گیرد و روی WebView2 نشان می‌دهد. استخراج شد تا
        // هم مسیرِ عادی (LoadCardAsync، از DB) و هم مسیرِ «فقط برای این چاپ»
        // (از FrmCardNoticesEdit، بدون DB) از همین یک نقطه رد شوند.
        private async System.Threading.Tasks.Task RenderDataAsync(GuardianCardData data)
        {
            if (data == null) return;

            try
            {
                if (_webView.CoreWebView2 == null)
                {
                    string userDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CaseManagement", "WebView2UserData");
                    Directory.CreateDirectory(userDataFolder);

                    CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    await _webView.EnsureCoreWebView2Async(env);
                }

                var renderer = new GuardianCardRenderer();

                // عکس سرپرست از خودِ پرونده؛ لوگوی مؤسسه از تنظیمات — هردو در
                // GuardianCardRenderer به‌صورت نسبی داخل پوشه کاری کپی می‌شوند
                // (مقادیر فعلی data.Photo/data.Logo مسیر مطلق مبدأ‌اند؛ همان‌جا
                // به مسیر نسبی جدید بازنویسی می‌شوند).
                string workingFolder = renderer.StageAndPopulate(
                    data,
                    guardianPhotoPath: _originalPhotoPath,
                    orgLogoPath: _originalLogoPath,
                    signaturePath: _originalSignaturePath,
                    stampPath: _originalStampPath,
                    disabledFields: _disabledFields,
                    design: _currentDesign);

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    GuardianCardRenderer.VirtualHostName, workingFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                _webView.CoreWebView2.Navigate("https://" + GuardianCardRenderer.VirtualHostName + "/index.html");
                _currentData = data;
                SetStatus("کارت آماده شد — «" + data.GuardianName + "»");
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("خطا در نمایش کارت شناسایی: " + ex.Message);
            }
        }

        private void ShowRuntimeMissingMessage(string detail = null)
        {
            SetStatus("");
            string msg =
                "برای نمایش کارت شناسایی به «Microsoft Edge WebView2 Runtime» نیاز است که روی این سیستم نصب نیست.\n\n" +
                "نصب آن رایگان و سریع است:\n" +
                "۱) به آدرس زیر بروید:\n" +
                "    https://developer.microsoft.com/microsoft-edge/webview2/\n" +
                "۲) گزینه «Evergreen Bootstrapper» را دانلود و اجرا کنید.\n" +
                "۳) بعد از نصب، دوباره برنامه را باز کنید.\n" +
                (string.IsNullOrEmpty(detail) ? "" : ("\nجزئیات فنی: " + detail));
            Msg.Show(msg);
        }

        private void SetStatus(string text)
        {
            if (_lblStatus.InvokeRequired) { _lblStatus.Invoke((Action)(() => _lblStatus.Text = text)); return; }
            _lblStatus.Text = text;
        }

        // آموزش — CoreWebView2.ShowPrintUI همزمان (void) است، نه Async؛ خودش
        // در پس‌زمینه دیالوگ چاپ مرورگر را باز می‌کند، پس نیازی به await نیست.
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
                FileName = "کارت-شناسایی-سرپرست.pdf"
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

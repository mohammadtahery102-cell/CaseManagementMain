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

        public FrmGuardianCardPreview(int caseId)
        {
            _caseId = caseId;
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

            _lblStatus = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White, Font = UiTheme.Font(9.5F)
            };
            _lblStatus.SetBounds(392, 8, 700, 36);

            _toolbar.Controls.Add(btnPrint);
            _toolbar.Controls.Add(btnPdf);
            _toolbar.Controls.Add(btnRefresh);
            _toolbar.Controls.Add(_lblStatus);

            _webView = new WebView2 { Dock = DockStyle.Fill };

            Controls.Add(_webView);
            Controls.Add(_toolbar);

            Load += async delegate { await LoadCardAsync(); };
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
                var cardService = new CardService();
                GuardianCardData data = cardService.BuildCardData(_caseId);

                // عکس سرپرست از خودِ پرونده؛ لوگوی مؤسسه از تنظیمات — هردو در
                // GuardianCardRenderer به‌صورت نسبی داخل پوشه کاری کپی می‌شوند
                // (مقادیر فعلی data.Photo/data.Logo مسیر مطلق مبدأ‌اند؛ همان‌جا
                // به مسیر نسبی جدید بازنویسی می‌شوند).
                string workingFolder = renderer.StageAndPopulate(
                    data,
                    guardianPhotoPath: data.Photo,
                    orgLogoPath: data.Logo,
                    signaturePath: data.Signature,
                    stampPath: data.Stamp);

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    GuardianCardRenderer.VirtualHostName, workingFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                _webView.CoreWebView2.Navigate("https://" + GuardianCardRenderer.VirtualHostName + "/index.html");
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

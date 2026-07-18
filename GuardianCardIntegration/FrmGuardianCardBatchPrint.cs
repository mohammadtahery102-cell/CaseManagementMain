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
    // چاپ جمعی کارت شناسایی سرپرست — بر اساس بازه‌ی شماره فرم («از شماره فرم
    // X تا Y»). همان قالب A5 دورو (روی/پشت) که برای یک کارت استفاده می‌شود؛
    // هر پرونده در بازه، یک جفت صفحه‌ی روی/پشتِ جداگانه می‌گیرد (نگاه کنید
    // print.css: هر «.card» با page-break-after چاپ می‌شود)، پس نتیجه یک سند
    // چندصفحه‌ای پیاپی روی/پشت/روی/پشت/... است — دقیقاً مناسب چاپ دورو روی
    // چاپگرهایی که Duplex دارند یا برای چاپ دستیِ دو مرحله‌ای (اول صفحات فرد،
    // برگرداندن کاغذ، بعد صفحات زوج).
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmGuardianCardBatchPrint : Form
    {
        private WebView2 _webView;
        private Panel _toolbar;
        private NumericUpDown _numFrom;
        private NumericUpDown _numTo;
        private Label _lblStatus;

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
            Size = new Size(1180, 820);
            MinimumSize = new Size(760, 560);

            _toolbar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.PrimaryDark };

            Label lblFrom = new Label
            {
                Text = "از شماره فرم", ForeColor = Color.White, AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.Font(9.5F)
            };
            lblFrom.SetBounds(14, 14, 90, 28);

            _numFrom = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 1 };
            _numFrom.SetBounds(106, 12, 90, 28);

            Label lblTo = new Label
            {
                Text = "تا شماره فرم", ForeColor = Color.White, AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.Font(9.5F)
            };
            lblTo.SetBounds(204, 14, 90, 28);

            _numTo = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 20 };
            _numTo.SetBounds(296, 12, 90, 28);

            var btnLoad = UiTheme.CreateButton("بارگذاری پیش‌نمایش", "⌕", UiTheme.Primary);
            btnLoad.SetBounds(398, 8, 160, 36);
            btnLoad.Click += async delegate { await LoadBatchAsync(); };

            var btnPrint = UiTheme.CreateButton("چاپ", "🖨", UiTheme.Success);
            btnPrint.SetBounds(566, 8, 100, 36);
            btnPrint.Click += delegate { ShowPrintDialog(); };

            var btnPdf = UiTheme.CreateButton("ذخیره PDF", "📄", UiTheme.Primary);
            btnPdf.SetBounds(674, 8, 130, 36);
            btnPdf.Click += async delegate { await SaveAsPdfAsync(); };

            _lblStatus = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White, Font = UiTheme.Font(9.5F)
            };
            _lblStatus.SetBounds(812, 8, 340, 36);

            _toolbar.Controls.Add(lblFrom);
            _toolbar.Controls.Add(_numFrom);
            _toolbar.Controls.Add(lblTo);
            _toolbar.Controls.Add(_numTo);
            _toolbar.Controls.Add(btnLoad);
            _toolbar.Controls.Add(btnPrint);
            _toolbar.Controls.Add(btnPdf);
            _toolbar.Controls.Add(_lblStatus);

            _webView = new WebView2 { Dock = DockStyle.Fill };

            Controls.Add(_webView);
            Controls.Add(_toolbar);
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

        private async System.Threading.Tasks.Task LoadBatchAsync()
        {
            int from = (int)_numFrom.Value;
            int to = (int)_numTo.Value;
            if (to < from)
            {
                Msg.Show("«تا شماره فرم» باید بزرگ‌تر یا مساوی «از شماره فرم» باشد.");
                return;
            }

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
                var items = cardService.BuildCardDataRange(from, to, out failedCount);

                if (items.Count == 0)
                {
                    SetStatus("");
                    Msg.Show("در این بازه‌ی شماره فرم، هیچ پرونده‌ای پیدا نشد.");
                    return;
                }

                var renderer = new GuardianCardRenderer();
                string orgLogoPath = SettingsHelper.Get(SettingsHelper.LogoPath);
                string signaturePath = SettingsHelper.Get(SettingsHelper.SignaturePath);
                string stampPath = SettingsHelper.Get(SettingsHelper.StampPath);

                string workingFolder = renderer.StageAndPopulateBatch(
                    items,
                    data => data.Photo,
                    orgLogoPath,
                    signaturePath,
                    stampPath);

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    GuardianCardRenderer.VirtualHostName, workingFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                _webView.CoreWebView2.Navigate("https://" + GuardianCardRenderer.VirtualHostName + "/index.html");

                string statusText = items.Count + " کارت آماده شد (شماره فرم " + from + " تا " + to + ")";
                if (failedCount > 0)
                    statusText += " — " + failedCount + " پرونده با خطا رد شد";
                SetStatus(statusText);
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("خطا در آماده‌سازی چاپ جمعی: " + ex.Message);
            }
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

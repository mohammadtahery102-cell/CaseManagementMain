using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CaseManagement.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CaseManagement.AssistanceReceiptIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه نمایش — میزبان WebView2 برای پیش‌نمایش/چاپ/PDF یک برگه دریافت
    // مساعدت. آینه FrmGuardianCardPreview، با یک تفاوت مهم: طبق تصمیمِ صریحِ
    // کاربر، ReceiptNo/PrintedAt فقط در اولینِ چاپِ واقعی (چاپ یا PDF) ثبت
    // می‌شود، نه در بازِ کردنِ پیش‌نمایش؛ بنابراین پیش‌نمایش از
    // AssistanceReceiptService.PreviewReceiptData (بدونِ نوشتن) استفاده
    // می‌کند و دکمه‌های چاپ/PDF پیش از چاپ، یک‌بار داده را با
    // BuildReceiptData (که می‌نویسد) دوباره Render می‌کنند.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmAssistanceReceiptSinglePrint : Form
    {
        private readonly int _assistanceId;
        private WebView2 _webView;
        private Panel _toolbar;
        private Label _lblStatus;

        public FrmAssistanceReceiptSinglePrint(int assistanceId)
        {
            _assistanceId = assistanceId;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "برگه دریافت مساعدت";
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
            btnPrint.Click += async delegate { await PrintAsync(); };

            var btnPdf = UiTheme.CreateButton("ذخیره PDF", "📄", UiTheme.Primary);
            btnPdf.SetBounds(132, 8, 130, 36);
            btnPdf.Click += async delegate { await SaveAsPdfAsync(); };

            var btnRefresh = UiTheme.CreateSecondaryButton("بازخوانی", "↺");
            btnRefresh.SetBounds(270, 8, 110, 36);
            btnRefresh.Click += async delegate { await LoadReceiptAsync(commit: false); };

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

            Load += async delegate { await LoadReceiptAsync(commit: false); };
        }

        // commit=false: پیش‌نمایش بدونِ نوشتن. commit=true: تخصیص/ثبتِ واقعیِ
        // ReceiptNo و PrintedAt (فقط بارِ اول اثر می‌کند، بعدها idempotent است).
        private async Task LoadReceiptAsync(bool commit)
        {
            SetStatus("در حال آماده‌سازی برگه...");

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
                await EnsureWebViewReadyAsync();

                var service = new AssistanceReceiptService();
                AssistanceReceiptData data = commit
                    ? service.BuildReceiptData(_assistanceId)
                    : service.PreviewReceiptData(_assistanceId);

                await RenderAndNavigateAsync(data);

                SetStatus(string.IsNullOrEmpty(data.ReceiptCode)
                    ? "پیش‌نمایش — «" + data.RecipientName + "» (شماره برگه هنوز ثبت نشده)"
                    : "آماده — «" + data.RecipientName + "» — " + data.ReceiptCode);
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("خطا در نمایش برگه دریافت مساعدت: " + ex.Message);
            }
        }

        private async Task EnsureWebViewReadyAsync()
        {
            if (_webView.CoreWebView2 != null) return;

            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CaseManagement", "WebView2UserData");
            Directory.CreateDirectory(userDataFolder);

            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);
        }

        private async Task RenderAndNavigateAsync(AssistanceReceiptData data)
        {
            var renderer = new AssistanceReceiptRenderer();
            string workingFolder = renderer.StageAndPopulate(data, data.Photo, data.Logo);

            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                AssistanceReceiptRenderer.VirtualHostName, workingFolder,
                CoreWebView2HostResourceAccessKind.Allow);

            await NavigateAndWaitForRenderAsync("https://" + AssistanceReceiptRenderer.VirtualHostName + "/index.html");
        }

        // آموزش — رفعِ باگِ بالقوه: NavigationCompleted فقط یعنی سندِ HTML
        // بارگذاری شده، نه اینکه fetch(SAMPLE_DATA.json)+render (که در
        // receipt.js آسنکرون است) هم تمام شده — چاپ/PDF می‌توانست زودتر از
        // رندرِ واقعی اجرا شود. علاوه بر NavigationCompleted، منتظرِ پیامِ
        // صریحِ «assistancereceipt:populated» هم می‌مانیم که receipt.js بعد از
        // اتمامِ کاملِ render (چه موفق چه با خطا) با postMessage می‌فرستد.
        // هر دو handler پیش از Navigate ثبت می‌شوند تا رویدادی که خیلی زود
        // رخ می‌دهد از دست نرود. Task.Delay فقط شبکهٔ ایمنی است تا اگر پیام
        // هرگز نرسید (مثلاً خطای غیرمنتظرهٔ جاوااسکریپت)، UI برای همیشه گیر نکند.
        private async Task NavigateAndWaitForRenderAsync(string url)
        {
            var navTcs = new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> navHandler = null;
            navHandler = delegate (object s, CoreWebView2NavigationCompletedEventArgs e)
            {
                _webView.CoreWebView2.NavigationCompleted -= navHandler;
                navTcs.TrySetResult(e.IsSuccess);
            };

            var renderTcs = new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2WebMessageReceivedEventArgs> msgHandler = null;
            msgHandler = delegate (object s, CoreWebView2WebMessageReceivedEventArgs e)
            {
                if (e.TryGetWebMessageAsString() == "assistancereceipt:populated")
                {
                    _webView.CoreWebView2.WebMessageReceived -= msgHandler;
                    renderTcs.TrySetResult(true);
                }
            };

            _webView.CoreWebView2.NavigationCompleted += navHandler;
            _webView.CoreWebView2.WebMessageReceived += msgHandler;
            _webView.CoreWebView2.Navigate(url);

            await navTcs.Task;
            await Task.WhenAny(renderTcs.Task, Task.Delay(TimeSpan.FromSeconds(8)));

            if (!renderTcs.Task.IsCompleted)
                _webView.CoreWebView2.WebMessageReceived -= msgHandler;
        }

        private void ShowRuntimeMissingMessage(string detail = null)
        {
            SetStatus("");
            string msg =
                "برای نمایش برگه دریافت مساعدت به «Microsoft Edge WebView2 Runtime» نیاز است که روی این سیستم نصب نیست.\n\n" +
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

        private async Task PrintAsync()
        {
            if (_webView.CoreWebView2 == null) return;
            try
            {
                SetStatus("در حال ثبت شماره برگه...");
                await LoadReceiptAsync(commit: true);
                _webView.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در چاپ: " + ex.Message);
            }
        }

        private async Task SaveAsPdfAsync()
        {
            if (_webView.CoreWebView2 == null) return;

            using (var sfd = new SaveFileDialog
            {
                Filter = "فایل PDF|*.pdf",
                FileName = "برگه-دریافت-مساعدت.pdf"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    SetStatus("در حال ثبت شماره برگه...");
                    await LoadReceiptAsync(commit: true);

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

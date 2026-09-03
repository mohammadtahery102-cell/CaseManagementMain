using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CaseManagement.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CaseManagement.AssistanceReceiptIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // چاپ گروهیِ برگهٔ دریافتِ مساعدتِ غیرنقدی، بر اساسِ یک «بستهٔ مساعدت»
    // (تنظیمات → بسته‌های مساعدت) + فیلترهای ولایت/ولسوالی/شماره‌فرم. طبقِ
    // خواستهٔ کاربر، گرید تا اجرای فیلتر خالی می‌ماند؛ اندازهٔ کاغذِ چاپ/PDF
    // (نصفِ A4 یا اندازهٔ کارتِ قابل‌تنظیم) از طریقِ CoreWebView2PrintSettings
    // اعمال می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmAssistancePackageBatchPrint : Form
    {
        private readonly AssistancePackageRepository _packageRepo = new AssistancePackageRepository();

        private ComboBox _cmbPackage;
        private ComboBox _cmbProvince;
        private TextBox _txtDistrict;
        private NumericUpDown _numFormNo;
        private DataGridView _grid;
        private RadioButton _rdoHalfA4;
        private RadioButton _rdoCardSize;
        private NumericUpDown _numCardWidthMm;
        private NumericUpDown _numCardHeightMm;
        private WebView2 _webView;
        private Label _lblStatus;

        private DataTable _filteredTable;

        private static readonly string[] Provinces =
        {
            "همه ولایات",
            "کابل", "هرات", "بلخ", "قندهار", "ننگرهار", "بدخشان", "بغلان", "تخار",
            "غزنی", "هلمند", "لغمان", "کندز", "فاریاب", "جوزجان", "سمنگان", "بامیان",
            "پکتیا", "لوگر", "وردک", "غور", "فراه", "خوست", "کاپیسا", "پروان",
            "زابل", "ارزگان", "نیمروز", "نورستان", "کنر", "سرپل", "دایکندی",
            "پکتیکا", "بادغیس", "پنجشیر"
        };

        public FrmAssistancePackageBatchPrint()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "چاپ گروهیِ بستهٔ مساعدت";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 820);
            MinimumSize = new Size(860, 600);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = UiTheme.PrimaryDark };

            Label lblPackage = MakeLabel("بسته", 14, 10, 44);
            _cmbPackage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
            _cmbPackage.SetBounds(62, 8, 150, 28);

            Label lblProvince = MakeLabel("ولایت", 222, 10, 50);
            _cmbProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbProvince.Items.AddRange(Provinces);
            _cmbProvince.SelectedIndex = 0;
            _cmbProvince.SetBounds(276, 8, 130, 28);

            Label lblDistrict = MakeLabel("ولسوالی", 416, 10, 56);
            _txtDistrict = new TextBox { RightToLeft = RightToLeft.Yes };
            _txtDistrict.SetBounds(474, 8, 120, 28);

            Label lblFormNo = MakeLabel("شماره فرم", 604, 10, 66);
            _numFormNo = new NumericUpDown { Minimum = 0, Maximum = 999999, Value = 0 };
            _numFormNo.SetBounds(672, 8, 80, 28);

            Button btnApplyFilter = UiTheme.CreateButton("اعمالِ فیلتر", "⌕", UiTheme.Primary);
            btnApplyFilter.SetBounds(762, 6, 120, 34);
            btnApplyFilter.Click += delegate { ApplyFilter(); };

            // ردیفِ دوم: اندازهٔ کاغذ + چاپ/PDF
            _rdoHalfA4 = new RadioButton { Text = "نصفِ A4", ForeColor = Color.White, Checked = true, AutoSize = true };
            _rdoHalfA4.SetBounds(14, 52, 90, 24);

            _rdoCardSize = new RadioButton { Text = "اندازهٔ کارت", ForeColor = Color.White, AutoSize = true };
            _rdoCardSize.SetBounds(110, 52, 100, 24);
            _rdoCardSize.CheckedChanged += delegate { UpdateCardSizeEnabled(); };

            Label lblWidth = MakeLabel("عرض (mm)", 216, 52, 76);
            _numCardWidthMm = new NumericUpDown { Minimum = 20, Maximum = 500, Value = 90 };
            _numCardWidthMm.SetBounds(294, 50, 70, 26);

            Label lblHeight = MakeLabel("ارتفاع (mm)", 370, 52, 82);
            _numCardHeightMm = new NumericUpDown { Minimum = 20, Maximum = 500, Value = 55 };
            _numCardHeightMm.SetBounds(454, 50, 70, 26);

            Button btnPrint = UiTheme.CreateButton("چاپ", "🖨", UiTheme.Success);
            btnPrint.SetBounds(540, 48, 90, 34);
            btnPrint.Click += async delegate { await PrintAsync(); };

            Button btnPdf = UiTheme.CreateButton("ذخیره PDF", "📄", UiTheme.Primary);
            btnPdf.SetBounds(636, 48, 115, 34);
            btnPdf.Click += async delegate { await SaveAsPdfAsync(); };

            _lblStatus = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White, Font = UiTheme.Font(9.5F)
            };
            _lblStatus.SetBounds(760, 50, 380, 30);

            toolbar.Controls.Add(lblPackage);
            toolbar.Controls.Add(_cmbPackage);
            toolbar.Controls.Add(lblProvince);
            toolbar.Controls.Add(_cmbProvince);
            toolbar.Controls.Add(lblDistrict);
            toolbar.Controls.Add(_txtDistrict);
            toolbar.Controls.Add(lblFormNo);
            toolbar.Controls.Add(_numFormNo);
            toolbar.Controls.Add(btnApplyFilter);
            toolbar.Controls.Add(_rdoHalfA4);
            toolbar.Controls.Add(_rdoCardSize);
            toolbar.Controls.Add(lblWidth);
            toolbar.Controls.Add(_numCardWidthMm);
            toolbar.Controls.Add(lblHeight);
            toolbar.Controls.Add(_numCardHeightMm);
            toolbar.Controls.Add(btnPrint);
            toolbar.Controls.Add(btnPdf);
            toolbar.Controls.Add(_lblStatus);

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260
            };

            // آموزش — طبقِ خواستهٔ کاربر: گرید تا اجرای فیلتر خالی می‌ماند.
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, ReadOnly = true,
                AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            _webView = new WebView2 { Dock = DockStyle.Fill };

            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(_webView);

            Controls.Add(split);
            Controls.Add(toolbar);

            UpdateCardSizeEnabled();
            Load += delegate { LoadPackages(); };
        }

        private static Label MakeLabel(string text, int x, int y, int width)
        {
            var lbl = new Label
            {
                Text = text, ForeColor = Color.White, AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.Font(9.5F)
            };
            lbl.SetBounds(x, y, width, 26);
            return lbl;
        }

        private void UpdateCardSizeEnabled()
        {
            _numCardWidthMm.Enabled = _rdoCardSize.Checked;
            _numCardHeightMm.Enabled = _rdoCardSize.Checked;
        }

        private void LoadPackages()
        {
            _cmbPackage.Items.Clear();
            foreach (AssistancePackage pkg in _packageRepo.GetAllPackages())
                _cmbPackage.Items.Add(pkg);
            if (_cmbPackage.Items.Count > 0) _cmbPackage.SelectedIndex = 0;
        }

        // طبقِ خواستهٔ کاربر: گرید فقط با کلیکِ «اعمالِ فیلتر» پر می‌شود؛ پیش
        // از آن (بازِ کردنِ فرم) خالی است.
        private void ApplyFilter()
        {
            AssistancePackage pkg = _cmbPackage.SelectedItem as AssistancePackage;
            if (pkg == null)
            {
                Msg.Show("اول یک بسته انتخاب کنید (یا از تنظیمات → بسته‌های مساعدت تعریف کنید).");
                return;
            }

            string province = _cmbProvince.SelectedIndex <= 0 ? "" : _cmbProvince.Text;
            string district = _txtDistrict.Text.Trim();
            int formNo = (int)_numFormNo.Value;

            _filteredTable = _packageRepo.GetFilteredTable(pkg.PackageID, province, district, formNo);
            _grid.DataSource = _filteredTable;
            SetStatus(_filteredTable.Rows.Count + " رکورد پیدا شد.");
        }

        private List<int> GetFilteredAssistanceIds()
        {
            var ids = new List<int>();
            if (_filteredTable == null) return ids;
            foreach (DataRow row in _filteredTable.Rows)
                ids.Add(Convert.ToInt32(row["AssistanceID"]));
            return ids;
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

        // آماده‌سازی/رندرِ واقعی + ثبتِ ReceiptNo/PrintedAt (فقط بارِ اول اثر
        // می‌کند)، دقیقاً هم‌الگو با بقیهٔ فرم‌های این ماژول.
        private async Task<bool> PrepareRenderAsync()
        {
            List<int> ids = GetFilteredAssistanceIds();
            if (ids.Count == 0)
            {
                Msg.Show("اول فیلتر را اعمال کنید تا رکوردی برای چاپ وجود داشته باشد.");
                return false;
            }

            SetStatus("در حال آماده‌سازی چاپ...");

            try
            {
                string runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrEmpty(runtimeVersion))
                    throw new WebView2RuntimeNotFoundException();
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("برای چاپ به «Microsoft Edge WebView2 Runtime» نیاز است.\n" + ex.Message);
                return false;
            }

            try
            {
                await EnsureWebViewReadyAsync();

                var service = new AssistanceReceiptService();
                int failedCount;
                List<AssistanceReceiptData> items = service.BuildReceiptDataForIds(ids, commit: true, failedCount: out failedCount);

                if (items.Count == 0)
                {
                    SetStatus("");
                    Msg.Show("هیچ برگه‌ای آماده نشد.");
                    return false;
                }

                var renderer = new AssistanceReceiptRenderer();
                string orgLogoPath = SettingsHelper.Get(SettingsHelper.LogoPath);
                string workingFolder = renderer.StageAndPopulateBatch(items, data => data.Photo, orgLogoPath);

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    AssistanceReceiptRenderer.VirtualHostName, workingFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                await NavigateAndWaitForRenderAsync("https://" + AssistanceReceiptRenderer.VirtualHostName + "/index.html");

                string statusText = items.Count + " برگه آماده شد";
                if (failedCount > 0) statusText += " — " + failedCount + " رکورد با خطا رد شد";
                SetStatus(statusText);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("خطا در آماده‌سازی چاپ گروهی: " + ex.Message);
                return false;
            }
        }

        // اندازهٔ کاغذِ انتخاب‌شده (نصفِ A4 = اندازهٔ A5 استاندارد، یا اندازهٔ
        // کارتِ قابل‌تنظیم) روی تنظیماتِ چاپِ WebView2 اعمال می‌شود.
        private CoreWebView2PrintSettings BuildPrintSettings(CoreWebView2Environment env)
        {
            CoreWebView2PrintSettings settings = env.CreatePrintSettings();
            settings.MediaSize = CoreWebView2PrintMediaSize.Custom;

            double widthMm, heightMm;
            if (_rdoCardSize.Checked)
            {
                widthMm = (double)_numCardWidthMm.Value;
                heightMm = (double)_numCardHeightMm.Value;
            }
            else
            {
                // نصفِ A4 (A5 استاندارد): 148×210 میلی‌متر.
                widthMm = 148;
                heightMm = 210;
            }

            settings.PageWidth = widthMm / 25.4;
            settings.PageHeight = heightMm / 25.4;
            settings.ShouldPrintBackgrounds = true;
            return settings;
        }

        private async Task PrintAsync()
        {
            bool ready = await PrepareRenderAsync();
            if (!ready || _webView.CoreWebView2 == null) return;

            // آموزش — رفعِ باگ: CoreWebView2.PrintAsync (برخلافِ ShowPrintUI که
            // در فرم‌های خواهرِ دیگر استفاده می‌شود) کاملاً بی‌صدا چاپ می‌کند و
            // هیچ دیالوگی نشان نمی‌دهد؛ چون اندازهٔ کاغذِ سفارشی (کارت/نصفِ A4)
            // فقط از همین API پشتیبانی می‌شود، نمی‌توان به‌جایش ShowPrintUI صدا
            // زد. به‌جایش همین‌جا دیالوگِ چاپِ ویندوز نشان داده می‌شود تا کاربر
            // پیش از چاپِ واقعی، پرینتر/تعداد نسخه را انتخاب کند؛ اندازهٔ کاغذِ
            // سفارشی بعد از آن روی همان انتخاب اعمال می‌شود.
            PrinterSettings chosenPrinter;
            using (var pd = new PrintDialog { AllowSomePages = false, AllowPrintToFile = false })
            {
                if (pd.ShowDialog(this) != DialogResult.OK) return;
                chosenPrinter = pd.PrinterSettings;
            }

            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CaseManagement", "WebView2UserData");
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                CoreWebView2PrintSettings settings = BuildPrintSettings(env);
                settings.PrinterName = chosenPrinter.PrinterName;
                settings.Copies = chosenPrinter.Copies;

                SetStatus("در حال چاپ...");
                CoreWebView2PrintStatus result = await _webView.CoreWebView2.PrintAsync(settings);
                bool ok = result == CoreWebView2PrintStatus.Succeeded;
                SetStatus(ok ? "چاپ ارسال شد." : "");
                if (!ok) Msg.Show("چاپ ناموفق بود (" + result + ").");
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در چاپ: " + ex.Message);
            }
        }

        private async Task SaveAsPdfAsync()
        {
            bool ready = await PrepareRenderAsync();
            if (!ready || _webView.CoreWebView2 == null) return;

            using (var sfd = new SaveFileDialog
            {
                Filter = "فایل PDF|*.pdf",
                FileName = "بستهٔ-مساعدت-گروهی.pdf"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string userDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CaseManagement", "WebView2UserData");
                    CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    CoreWebView2PrintSettings settings = BuildPrintSettings(env);

                    SetStatus("در حال ساختِ PDF...");
                    bool ok = await _webView.CoreWebView2.PrintToPdfAsync(sfd.FileName, settings);
                    SetStatus("");
                    if (ok)
                        UiTheme.ShowSuccess(this, "فایل PDF ذخیره شد:\n" + sfd.FileName);
                    else
                        Msg.Show("ساختِ PDF ناموفق بود.");
                }
                catch (Exception ex)
                {
                    SetStatus("");
                    Msg.Show("خطا در ساختِ PDF: " + ex.Message);
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

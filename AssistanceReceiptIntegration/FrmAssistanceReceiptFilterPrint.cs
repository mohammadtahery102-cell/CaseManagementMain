using System;
using System.Collections.Generic;
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
    // چاپ گروهی برگه دریافت مساعدت — بر اساس فیلترهای موجودِ سیستم (ولایت،
    // ولسوالی، شماره فرم، برنامه، بازه‌ی تاریخ، نوع مساعدت، وضعیتِ چاپ‌شده).
    // آینه FrmGuardianCardBatchPrint، با همان تصمیمِ commit-at-print-time
    // که در FrmAssistanceReceiptSinglePrint توضیح داده شده.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmAssistanceReceiptFilterPrint : Form
    {
        private WebView2 _webView;
        private Panel _toolbar;
        private ComboBox _cmbProvince;
        private TextBox _txtDistrict;
        private NumericUpDown _numFormNo;
        private TextBox _txtProgramName;
        private TextBox _txtAssistanceType;
        private ComboBox _cmbPrintedStatus;
        private CheckBox _chkDateFrom;
        private PersianDatePicker _dtpFrom;
        private CheckBox _chkDateTo;
        private PersianDatePicker _dtpTo;
        private Label _lblStatus;

        private static readonly string[] Provinces =
        {
            "همه ولایات",
            "کابل", "هرات", "بلخ", "قندهار", "ننگرهار", "بدخشان", "بغلان", "تخار",
            "غزنی", "هلمند", "لغمان", "کندز", "فاریاب", "جوزجان", "سمنگان", "بامیان",
            "پکتیا", "لوگر", "وردک", "غور", "فراه", "خوست", "کاپیسا", "پروان",
            "زابل", "ارزگان", "نیمروز", "نورستان", "کنر", "سرپل", "دایکندی",
            "پکتیکا", "بادغیس", "پنجشیر"
        };

        public FrmAssistanceReceiptFilterPrint()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "چاپ گروهی برگه دریافت مساعدت";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 820);
            MinimumSize = new Size(820, 560);

            _toolbar = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = UiTheme.PrimaryDark };

            // ردیف اول: ولایت / ولسوالی / شماره فرم / برنامه
            Label lblProvince = MakeLabel("ولایت", 14, 10, 50);
            _cmbProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbProvince.Items.AddRange(Provinces);
            _cmbProvince.SelectedIndex = 0;
            _cmbProvince.SetBounds(66, 8, 120, 28);

            Label lblDistrict = MakeLabel("ولسوالی", 194, 10, 56);
            _txtDistrict = new TextBox { RightToLeft = RightToLeft.Yes };
            _txtDistrict.SetBounds(252, 8, 110, 28);

            Label lblFormNo = MakeLabel("شماره فرم", 370, 10, 66);
            _numFormNo = new NumericUpDown { Minimum = 0, Maximum = 999999, Value = 0 };
            _numFormNo.SetBounds(438, 8, 80, 28);

            Label lblProgram = MakeLabel("برنامه", 526, 10, 44);
            _txtProgramName = new TextBox { RightToLeft = RightToLeft.Yes };
            _txtProgramName.SetBounds(572, 8, 130, 28);

            Label lblType = MakeLabel("نوع مساعدت", 710, 10, 78);
            _txtAssistanceType = new TextBox { RightToLeft = RightToLeft.Yes };
            _txtAssistanceType.SetBounds(790, 8, 110, 28);

            Label lblPrinted = MakeLabel("وضعیت چاپ", 908, 10, 76);
            _cmbPrintedStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbPrintedStatus.Items.AddRange(new object[] { "همه", "چاپ‌شده", "چاپ‌نشده" });
            _cmbPrintedStatus.SelectedIndex = 0;
            _cmbPrintedStatus.SetBounds(986, 8, 100, 28);

            // ردیف دوم: بازه‌ی تاریخ + دکمه‌ها
            _chkDateFrom = new CheckBox { Text = "از تاریخ", ForeColor = Color.White, AutoSize = true };
            _chkDateFrom.SetBounds(14, 50, 78, 24);
            _dtpFrom = new PersianDatePicker();
            _dtpFrom.SetBounds(94, 48, 110, 28);

            _chkDateTo = new CheckBox { Text = "تا تاریخ", ForeColor = Color.White, AutoSize = true };
            _chkDateTo.SetBounds(214, 50, 78, 24);
            _dtpTo = new PersianDatePicker();
            _dtpTo.SetBounds(294, 48, 110, 28);

            var btnLoad = UiTheme.CreateButton("بارگذاری پیش‌نمایش", "⌕", UiTheme.Primary);
            btnLoad.SetBounds(420, 46, 150, 36);
            btnLoad.Click += async delegate { await LoadBatchAsync(commit: false); };

            var btnPrint = UiTheme.CreateButton("چاپ", "🖨", UiTheme.Success);
            btnPrint.SetBounds(576, 46, 85, 36);
            btnPrint.Click += async delegate { await PrintAsync(); };

            var btnPdf = UiTheme.CreateButton("ذخیره PDF", "📄", UiTheme.Primary);
            btnPdf.SetBounds(667, 46, 115, 36);
            btnPdf.Click += async delegate { await SaveAsPdfAsync(); };

            _lblStatus = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White, Font = UiTheme.Font(9.5F)
            };
            _lblStatus.SetBounds(788, 50, 380, 30);

            _toolbar.Controls.Add(lblProvince);
            _toolbar.Controls.Add(_cmbProvince);
            _toolbar.Controls.Add(lblDistrict);
            _toolbar.Controls.Add(_txtDistrict);
            _toolbar.Controls.Add(lblFormNo);
            _toolbar.Controls.Add(_numFormNo);
            _toolbar.Controls.Add(lblProgram);
            _toolbar.Controls.Add(_txtProgramName);
            _toolbar.Controls.Add(lblType);
            _toolbar.Controls.Add(_txtAssistanceType);
            _toolbar.Controls.Add(lblPrinted);
            _toolbar.Controls.Add(_cmbPrintedStatus);
            _toolbar.Controls.Add(_chkDateFrom);
            _toolbar.Controls.Add(_dtpFrom);
            _toolbar.Controls.Add(_chkDateTo);
            _toolbar.Controls.Add(_dtpTo);
            _toolbar.Controls.Add(btnLoad);
            _toolbar.Controls.Add(btnPrint);
            _toolbar.Controls.Add(btnPdf);
            _toolbar.Controls.Add(_lblStatus);

            _webView = new WebView2 { Dock = DockStyle.Fill };

            Controls.Add(_webView);
            Controls.Add(_toolbar);
        }

        private static Label MakeLabel(string text, int x, int y, int width)
        {
            var lbl = new Label
            {
                Text = text, ForeColor = Color.White, AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.Font(9.5F)
            };
            lbl.SetBounds(x, y, width, 28);
            return lbl;
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

        // آموزش — خواندنِ فیلترهای فعلیِ فرم، یک‌جا. هم LoadBatchAsync و هم
        // محافظِ تأییدِ چاپِ گروهی (ConfirmBatchPrintAsync) از همین یک نقطه
        // می‌خوانند تا شمارشِ نمایش‌داده‌شده در دیالوگِ تأیید دقیقاً همان
        // فیلترهایی باشد که واقعاً commit می‌شوند (بدونِ خطرِ ناهم‌خوانیِ دو
        // نسخهٔ جداگانه از همین کد).
        private (string Province, string District, int FormNo, string ProgramName, string AssistanceType,
            DateTime? DateFrom, DateTime? DateTo, bool? IsPrinted) ReadFilters()
        {
            return (
                Province: _cmbProvince.SelectedIndex <= 0 ? "" : _cmbProvince.Text,
                District: _txtDistrict.Text.Trim(),
                FormNo: (int)_numFormNo.Value,
                ProgramName: _txtProgramName.Text.Trim(),
                AssistanceType: _txtAssistanceType.Text.Trim(),
                DateFrom: _chkDateFrom.Checked ? _dtpFrom.Value.Date : (DateTime?)null,
                DateTo: _chkDateTo.Checked ? _dtpTo.Value.Date : (DateTime?)null,
                IsPrinted: _cmbPrintedStatus.SelectedIndex == 1 ? true
                    : _cmbPrintedStatus.SelectedIndex == 2 ? false : (bool?)null
            );
        }

        // آموزش — محافظِ چاپِ گروهیِ ناخواسته (به‌درخواستِ کاربر): پیش از هر
        // commit:true (که ReceiptNo/PrintedAt را دائمی ثبت می‌کند)، تعدادِ
        // دقیقِ رکوردهای منطبق با فیلترهای فعلی را با یک فراخوانیِ commit:false
        // (بدونِ هیچ نوشتنی در دیتابیس) می‌شمارد و دیالوگِ تأیید نشان می‌دهد؛
        // همان الگوی FrmGuardianCardBatchPrint.ConfirmAndRenderAsync. اگر
        // کاربر لغو کند، false برمی‌گردد و فراخوان (PrintAsync/SaveAsPdfAsync)
        // اصلاً LoadBatchAsync(commit:true) را صدا نمی‌زند — پس هیچ ReceiptNo/
        // PrintedAt ثبت نمی‌شود.
        private async Task<bool> ConfirmBatchPrintAsync()
        {
            SetStatus("در حال شمارش رکوردها...");

            int count;
            try
            {
                var f = ReadFilters();
                var service = new AssistanceReceiptService();
                int failedCount;
                List<AssistanceReceiptData> items = service.PreviewReceiptDataBatch(
                    f.Province, f.District, f.FormNo, f.ProgramName, f.DateFrom, f.DateTo, f.AssistanceType, f.IsPrinted, out failedCount);
                count = items.Count;
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("خطا در شمارش رکوردها: " + ex.Message);
                return false;
            }

            SetStatus("");
            if (count == 0)
            {
                Msg.Show("با این فیلترها هیچ رکورد مساعدتی پیدا نشد.");
                return false;
            }

            DialogResult confirm = MessageBox.Show(
                count + " برگهٔ دریافت مساعدت آماده/چاپ می‌شود و شمارهٔ برگه برای آن‌ها ثبت خواهد شد. ادامه می‌دهید؟",
                "تأیید چاپ گروهی", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

            return confirm == DialogResult.Yes;
        }

        // commit=false: پیش‌نمایشِ گروهی بدونِ نوشتن در دیتابیس.
        // commit=true: تخصیص/ثبتِ واقعیِ ReceiptNo/PrintedAt برای هر رکورد
        // (idempotent — رکوردهایی که قبلاً شماره گرفته‌اند دست‌نخورده می‌مانند).
        private async Task LoadBatchAsync(bool commit)
        {
            SetStatus("در حال آماده‌سازی برگه‌ها...");

            try
            {
                string runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrEmpty(runtimeVersion))
                    throw new WebView2RuntimeNotFoundException();
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("برای نمایش برگه‌ها به «Microsoft Edge WebView2 Runtime» نیاز است.\n" + ex.Message);
                return;
            }

            try
            {
                await EnsureWebViewReadyAsync();

                var f = ReadFilters();

                var service = new AssistanceReceiptService();
                int failedCount;
                List<AssistanceReceiptData> items = commit
                    ? service.BuildReceiptDataBatch(f.Province, f.District, f.FormNo, f.ProgramName, f.DateFrom, f.DateTo, f.AssistanceType, f.IsPrinted, out failedCount)
                    : service.PreviewReceiptDataBatch(f.Province, f.District, f.FormNo, f.ProgramName, f.DateFrom, f.DateTo, f.AssistanceType, f.IsPrinted, out failedCount);

                if (items.Count == 0)
                {
                    SetStatus("");
                    Msg.Show("با این فیلترها هیچ رکورد مساعدتی پیدا نشد.");
                    return;
                }

                var renderer = new AssistanceReceiptRenderer();
                string orgLogoPath = SettingsHelper.Get(SettingsHelper.LogoPath);

                string workingFolder = renderer.StageAndPopulateBatch(items, data => data.Photo, orgLogoPath);

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    AssistanceReceiptRenderer.VirtualHostName, workingFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                await NavigateAndWaitForRenderAsync("https://" + AssistanceReceiptRenderer.VirtualHostName + "/index.html");

                string statusText = items.Count + " برگه آماده شد";
                if (failedCount > 0)
                    statusText += " — " + failedCount + " رکورد با خطا رد شد";
                SetStatus(statusText);
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("خطا در آماده‌سازی چاپ گروهی: " + ex.Message);
            }
        }

        private async Task PrintAsync()
        {
            if (!await ConfirmBatchPrintAsync()) return;

            await LoadBatchAsync(commit: true);
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

        private async Task SaveAsPdfAsync()
        {
            if (!await ConfirmBatchPrintAsync()) return;

            await LoadBatchAsync(commit: true);
            if (_webView.CoreWebView2 == null) return;

            using (var sfd = new SaveFileDialog
            {
                Filter = "فایل PDF|*.pdf",
                FileName = "برگه-دریافت-مساعدت-گروهی.pdf"
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

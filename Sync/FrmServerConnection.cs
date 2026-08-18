using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // فرم «اتصال به سرور همگام‌سازی».
    //
    // آموزش — چرا این فرم لازم بود: کلِ لایهٔ آنلاین (HttpSyncTransport،
    // SyncService، BackgroundSyncManager) از قبل ساخته شده بود، ولی دو حلقهٔ
    // گمشده داشت که آن را عملاً غیرقابل‌استفاده می‌کرد:
    //
    //   ۱. «آدرس سرور» فقط از جدول SyncState خوانده می‌شد و هیچ فرمی آن را
    //      نمی‌نوشت — یعنی فقط با ویرایش دستیِ دیتابیس قابل تنظیم بود.
    //   ۲. HttpSyncTransport.Login از هیچ کجای برنامه صدا زده نمی‌شد، پس
    //      RefreshToken هرگز ذخیره نمی‌شد و Authenticate همیشه با پیامِ
    //      «هنوز واردِ سرور نشده‌اید» برمی‌گشت — یعنی هر Push/Pull با خطای
    //      احراز هویت شکست می‌خورد.
    //
    // این فرم فقط همان دو حلقه را می‌بندد. هیچ منطق همگام‌سازیِ تازه‌ای ندارد
    // و هیچ رفتار موجودی را عوض نمی‌کند: اگر کاربر هرگز این فرم را باز نکند،
    // برنامه دقیقاً مثل امروز آفلاین کار می‌کند.
    //
    // ⚠ رمز عبور هرگز ذخیره نمی‌شود — فقط یک بار برای گرفتن توکن فرستاده
    // می‌شود و در حافظه هم نگه داشته نمی‌شود (رفتارِ خودِ HttpSyncTransport).
    // ═════════════════════════════════════════════════════════════════════════
    public class FrmServerConnection : Form
    {
        private TextBox _txtServerUrl;
        private TextBox _txtUsername;
        private TextBox _txtPassword;
        private CheckBox _chkShowPassword;
        private CheckBox _chkAutoSync;
        private NumericUpDown _numInterval;

        private Label _lblStatus;
        private Label _lblDevice;

        private Button _btnTest;
        private Button _btnLogin;
        private Button _btnLogout;
        private Button _btnClose;

        private bool _busy;
        private CancellationTokenSource _cancel;

        public FrmServerConnection()
        {
            Text = "اتصال به سرور همگام‌سازی";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            ClientSize = new Size(560, 520);

            BuildUi();
            LoadCurrentSettings();

            // وضعیت اولیه بدونِ تماسِ شبکه نمایش داده می‌شود تا فرم فوراً باز
            // شود؛ تماسِ واقعی فقط با دکمهٔ «تست اتصال» انجام می‌گیرد.
            ShowLocalStatus();
        }

        // ─────────────────────────────────────────────────────────────────────
        private void BuildUi()
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(16, 14, 16, 14),
                BackColor = UiTheme.Background
            };

            _lblStatus = new Label
            {
                AutoSize = false,
                Width = 500,
                Height = 46,
                Font = UiTheme.FontBold(UiTheme.SizeBody),
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 0, 0, 8)
            };
            flow.Controls.Add(_lblStatus);

            // ─── آدرس سرور ───
            flow.Controls.Add(MakeCaption("آدرس سرور"));
            _txtServerUrl = MakeTextBox();
            flow.Controls.Add(_txtServerUrl);
            flow.Controls.Add(MakeHint(
                "نشانی کامل سرور همگام‌سازی، بدون «/» در انتها." + Environment.NewLine +
                "مثال: https://abc-def.trycloudflare.com"));

            // ─── اعتبارنامه ───
            flow.Controls.Add(MakeCaption("نام کاربری سرور"));
            _txtUsername = MakeTextBox();
            flow.Controls.Add(_txtUsername);

            flow.Controls.Add(MakeCaption("رمز عبور سرور"));
            _txtPassword = MakeTextBox();
            _txtPassword.UseSystemPasswordChar = true;
            flow.Controls.Add(_txtPassword);

            _chkShowPassword = new CheckBox
            {
                Text = "نمایش رمز عبور",
                AutoSize = false,
                Width = 500,
                Height = 26,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.MiddleRight,
                CheckAlign = ContentAlignment.MiddleRight
            };
            _chkShowPassword.CheckedChanged += delegate
            {
                _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;
            };
            flow.Controls.Add(_chkShowPassword);

            flow.Controls.Add(MakeHint(
                "رمز عبور ذخیره نمی‌شود؛ فقط یک بار برای گرفتن توکن به سرور فرستاده می‌شود."));

            // ─── همگام‌سازی خودکار ───
            _chkAutoSync = new CheckBox
            {
                Text = "همگام‌سازی خودکار در پس‌زمینه",
                AutoSize = false,
                Width = 500,
                Height = 30,
                Font = UiTheme.Font(UiTheme.SizeBody),
                TextAlign = ContentAlignment.MiddleRight,
                CheckAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 10, 0, 0)
            };
            flow.Controls.Add(_chkAutoSync);

            flow.Controls.Add(MakeCaption("فاصلهٔ همگام‌سازی خودکار (دقیقه)"));
            _numInterval = new NumericUpDown
            {
                Width = 140,
                Minimum = 1,
                Maximum = 1440,
                Font = UiTheme.Font(UiTheme.SizeBody)
            };
            flow.Controls.Add(_numInterval);

            _lblDevice = new Label
            {
                AutoSize = false,
                Width = 500,
                Height = 34,
                Font = UiTheme.Font(UiTheme.SizeSmall - 1F),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 12, 0, 0)
            };
            flow.Controls.Add(_lblDevice);

            // ─── دکمه‌ها ───
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = UiTheme.CardBack };
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(14, 10, 14, 10),
                BackColor = UiTheme.CardBack
            };

            _btnTest = UiTheme.CreateSecondaryButton("تست اتصال", "⇄");
            _btnTest.Size = new Size(130, 36);
            UiTheme.SetTip(_btnTest, "آدرس را ذخیره می‌کند و فقط سلامت سرور را می‌سنجد — چیزی ارسال نمی‌شود.");
            _btnTest.Click += BtnTest_Click;
            buttons.Controls.Add(_btnTest);

            _btnLogin = UiTheme.CreateButton("ورود و ذخیره", "✔", UiTheme.Success);
            _btnLogin.Size = new Size(150, 36);
            UiTheme.SetTip(_btnLogin, "ورود به سرور و ذخیره‌ی توکن، تا همگام‌سازی بتواند کار کند.");
            _btnLogin.Click += BtnLogin_Click;
            buttons.Controls.Add(_btnLogin);

            _btnLogout = UiTheme.CreateSecondaryButton("قطع اتصال", "✖");
            _btnLogout.Size = new Size(130, 36);
            UiTheme.SetTip(_btnLogout, "پاک‌کردن توکن ذخیره‌شده. داده‌ای حذف نمی‌شود و برنامه آفلاین ادامه می‌دهد.");
            _btnLogout.Click += BtnLogout_Click;
            buttons.Controls.Add(_btnLogout);

            _btnClose = UiTheme.CreateSecondaryButton("بستن", "✕");
            _btnClose.Size = new Size(110, 36);
            _btnClose.Click += delegate { Close(); };
            buttons.Controls.Add(_btnClose);

            bar.Controls.Add(buttons);

            Controls.Add(flow);
            Controls.Add(bar);
        }

        private Label MakeCaption(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = 500,
                Height = 22,
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 8, 0, 0)
            };
        }

        private Label MakeHint(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = 500,
                Height = 36,
                Font = UiTheme.Font(UiTheme.SizeSmall - 1F),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };
        }

        private TextBox MakeTextBox()
        {
            var tb = new TextBox
            {
                Width = 500,
                Font = UiTheme.Font(UiTheme.SizeBody),
                RightToLeft = RightToLeft.No,          // نشانی و نام کاربری لاتین‌اند
                TextAlign = HorizontalAlignment.Left
            };
            UiTheme.StyleTextBox(tb);
            return tb;
        }

        // ─────────────────────────────────────────────────────────────────────
        private void LoadCurrentSettings()
        {
            _txtServerUrl.Text = SyncOutboxService.GetState(HttpSyncTransport.KeyServerUrl, "");
            _chkAutoSync.Checked = BackgroundSyncManager.AutoSyncEnabled;

            int minutes = BackgroundSyncManager.IntervalMinutes;
            if (minutes < _numInterval.Minimum) minutes = (int)_numInterval.Minimum;
            if (minutes > _numInterval.Maximum) minutes = (int)_numInterval.Maximum;
            _numInterval.Value = minutes;

            try
            {
                _lblDevice.Text = "شناسهٔ این دستگاه: " + HttpSyncTransport.DeviceGuid;
            }
            catch { _lblDevice.Text = ""; }
        }

        // وضعیت بدونِ تماسِ شبکه — فقط از روی چیزی که محلی ذخیره شده.
        private void ShowLocalStatus()
        {
            string url = SyncOutboxService.GetState(HttpSyncTransport.KeyServerUrl, "");
            string token = SyncOutboxService.GetState(HttpSyncTransport.KeyRefreshToken, "");

            if (string.IsNullOrWhiteSpace(url))
                SetStatus("آدرس سرور تنظیم نشده — برنامه آفلاین کار می‌کند.", UiTheme.TextMuted);
            else if (string.IsNullOrWhiteSpace(token))
                SetStatus("آدرس ثبت شده ولی هنوز واردِ سرور نشده‌اید.", UiTheme.Warning);
            else
                SetStatus("پیکربندی کامل است. برای اطمینان «تست اتصال» را بزنید.", UiTheme.TextDark);
        }

        private void SetStatus(string text, Color color)
        {
            _lblStatus.Text = text;
            _lblStatus.ForeColor = color;
        }

        // آدرس واردشده را اعتبارسنجی و ذخیره می‌کند. خروجی: آدرسِ پاک‌شده،
        // یا null اگر معتبر نبود (پیام به کاربر نشان داده شده است).
        private string SaveServerUrl()
        {
            string url = (_txtServerUrl.Text ?? "").Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(url))
            {
                UiTheme.ShowWarning(this, "آدرس سرور را وارد کنید.");
                _txtServerUrl.Focus();
                return null;
            }

            // ⚠ بدونِ این بررسی، یک آدرسِ بی‌طرح (مثلاً «myserver.com») در
            // HttpSyncTransport به خطای مبهمِ «آدرس سرور نامعتبر است» تبدیل
            // می‌شود و کاربر نمی‌فهمد مشکل از کجاست.
            Uri parsed;
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                UiTheme.ShowWarning(this,
                    "آدرس باید با http:// یا https:// شروع شود." + Environment.NewLine +
                    "مثال: https://abc-def.trycloudflare.com");
                _txtServerUrl.Focus();
                return null;
            }

            _txtServerUrl.Text = url;
            SyncOutboxService.SetState(HttpSyncTransport.KeyServerUrl, url);
            return url;
        }

        private void SaveAutoSyncSettings()
        {
            BackgroundSyncManager.AutoSyncEnabled = _chkAutoSync.Checked;
            BackgroundSyncManager.IntervalMinutes = (int)_numInterval.Value;
        }

        // ─────────────────────────────────────────────────────────────────────
        // تست اتصال — فقط می‌خواند، چیزی ارسال نمی‌کند.
        // ─────────────────────────────────────────────────────────────────────
        private async void BtnTest_Click(object sender, EventArgs e)
        {
            if (_busy) return;

            string url = SaveServerUrl();
            if (url == null) return;

            SetBusy(true, "در حال بررسی اتصال...");
            try
            {
                _cancel = new CancellationTokenSource();
                CancellationToken token = _cancel.Token;

                // ⚠ روی نخِ پس‌زمینه: تماسِ شبکه‌ای تا ۳۰ ثانیه طول می‌کشد و
                // روی نخِ رابط کاربری پنجره را «پاسخ نمی‌دهد» می‌کند.
                SyncConnectionStatus status = await Task.Run(delegate
                {
                    return new HttpSyncTransport(url).GetStatus(token);
                });

                switch (status.State)
                {
                    case SyncConnectionState.Online:
                        SetStatus("✓ متصل — " + status.Detail, UiTheme.Success);
                        break;
                    case SyncConnectionState.Unauthorized:
                        SetStatus("سرور در دسترس است ولی باید وارد شوید.", UiTheme.Warning);
                        break;
                    default:
                        SetStatus("✗ " + status.DisplayText + " — " + status.Detail, UiTheme.Danger);
                        break;
                }
            }
            catch (Exception ex)
            {
                SetStatus("✗ خطا: " + ex.Message, UiTheme.Danger);
            }
            finally
            {
                DisposeCancel();
                SetBusy(false, null);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ورود — تنها جایی در کلِ برنامه که HttpSyncTransport.Login صدا زده
        // می‌شود و RefreshToken ذخیره می‌گردد.
        // ─────────────────────────────────────────────────────────────────────
        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            if (_busy) return;

            string url = SaveServerUrl();
            if (url == null) return;

            string username = (_txtUsername.Text ?? "").Trim();
            string password = _txtPassword.Text ?? "";

            if (username.Length == 0)
            {
                UiTheme.ShowWarning(this, "نام کاربری سرور را وارد کنید.");
                _txtUsername.Focus();
                return;
            }
            if (password.Length == 0)
            {
                UiTheme.ShowWarning(this, "رمز عبور سرور را وارد کنید.");
                _txtPassword.Focus();
                return;
            }

            SetBusy(true, "در حال ورود به سرور...");
            try
            {
                _cancel = new CancellationTokenSource();
                CancellationToken token = _cancel.Token;

                SyncAuthResult result = await Task.Run(delegate
                {
                    return new HttpSyncTransport(url).Login(username, password, token);
                });

                if (result.Succeeded)
                {
                    SaveAutoSyncSettings();

                    // رمز بلافاصله از فرم پاک می‌شود؛ دیگر لازم نیست.
                    _txtPassword.Text = "";
                    _chkShowPassword.Checked = false;

                    SetStatus("✓ ورود موفق — همگام‌سازی آمادهٔ کار است.", UiTheme.Success);
                    UiTheme.ShowSuccess(this,
                        "ورود به سرور انجام شد و توکن ذخیره شد." + Environment.NewLine +
                        "از این پس همگام‌سازی (دستی و خودکار) کار می‌کند.");
                }
                else if (IsDevicePendingApproval(result.ErrorMessage))
                {
                    // ⚠ این «شکست» نیست، مرحلهٔ دومِ طراحیِ سرور است: هر دستگاه
                    // در نخستین ورود فقط *ثبت* می‌شود و تا تأییدِ مدیر هیچ
                    // توکنی نمی‌گیرد (AuthService.LoginAsync). بدون این توضیح،
                    // کاربر پیام را «رمز غلط» می‌فهمد و بی‌جهت دوباره تلاش
                    // می‌کند — که شمارندهٔ تلاشِ ناموفق را هم بالا می‌برد.
                    SaveAutoSyncSettings();
                    SetStatus("این دستگاه ثبت شد و در انتظار تأیید است.", UiTheme.Warning);

                    UiTheme.ShowWarning(this,
                        "دستگاه شما با موفقیت در سرور ثبت شد، ولی هنوز تأیید نشده است." +
                        Environment.NewLine + Environment.NewLine +
                        "این یک مرحلهٔ امنیتی است، نه خطا. تا وقتی مدیر سرور این دستگاه را " +
                        "تأیید نکند، همگام‌سازی شروع نمی‌شود." +
                        Environment.NewLine + Environment.NewLine +
                        "شناسهٔ این دستگاه:" + Environment.NewLine +
                        HttpSyncTransport.DeviceGuid + Environment.NewLine + Environment.NewLine +
                        "پس از تأیید، دوباره همین دکمهٔ «ورود و ذخیره» را بزنید.");
                }
                else
                {
                    SetStatus("✗ ورود ناموفق: " + result.ErrorMessage, UiTheme.Danger);
                    UiTheme.ShowWarning(this, "ورود ناموفق بود: " + result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                SetStatus("✗ خطا: " + ex.Message, UiTheme.Danger);
                UiTheme.ShowWarning(this, "ورود ممکن نشد: " + ex.Message);
            }
            finally
            {
                DisposeCancel();
                SetBusy(false, null);
            }
        }

        // تشخیصِ حالتِ «دستگاه در انتظار تأیید» از روی پیامِ سرور.
        // ⚠ سرور کدِ ماشین‌خوانی برای این حالت برنمی‌گرداند (هر شکستِ ورود
        // ۴۰۱ است، عمداً — تا «کاربر نیست» از «رمز غلط» تفکیک نشود)، پس تنها
        // نشانهٔ موجود همین متن است. اگر متنِ سرور روزی عوض شود، بدترین اتفاق
        // این است که پیامِ عمومیِ خطا نمایش داده می‌شود — نه رفتار غلط.
        private static bool IsDevicePendingApproval(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage)) return false;

            return errorMessage.IndexOf("تأیید", StringComparison.Ordinal) >= 0 ||
                   errorMessage.IndexOf("تایید", StringComparison.Ordinal) >= 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // قطع اتصال — فقط توکن پاک می‌شود. هیچ داده‌ای حذف نمی‌شود و صفِ
        // ارسال دست‌نخورده می‌ماند؛ برنامه دوباره آفلاین کار می‌کند.
        // ─────────────────────────────────────────────────────────────────────
        private void BtnLogout_Click(object sender, EventArgs e)
        {
            if (_busy) return;

            if (!UiTheme.ShowConfirm(this,
                    "توکنِ ذخیره‌شده پاک می‌شود و همگام‌سازی تا ورودِ دوباره متوقف می‌ماند." +
                    Environment.NewLine + Environment.NewLine +
                    "هیچ داده‌ای حذف نمی‌شود و صفِ ارسال دست‌نخورده می‌ماند." +
                    Environment.NewLine + Environment.NewLine + "ادامه می‌دهید؟",
                    "قطع اتصال از سرور"))
                return;

            SyncOutboxService.SetState(HttpSyncTransport.KeyRefreshToken, "");
            _txtPassword.Text = "";
            ShowLocalStatus();
            UiTheme.ShowSuccess(this, "اتصال قطع شد. برنامه آفلاین ادامه می‌دهد.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private void SetBusy(bool busy, string message)
        {
            _busy = busy;

            _btnTest.Enabled = !busy;
            _btnLogin.Enabled = !busy;
            _btnLogout.Enabled = !busy;
            _txtServerUrl.Enabled = !busy;
            _txtUsername.Enabled = !busy;
            _txtPassword.Enabled = !busy;

            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

            if (busy && !string.IsNullOrEmpty(message))
                SetStatus(message, UiTheme.TextDark);
        }

        private void DisposeCancel()
        {
            if (_cancel == null) return;
            try { _cancel.Dispose(); } catch { }
            _cancel = null;
        }

        // تنظیماتِ همگام‌سازی خودکار حتی بدون ورود هم باید ذخیره شوند، وگرنه
        // کاربر تیک را می‌زند، فرم را می‌بندد و تغییرش بی‌صدا از بین می‌رود.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_busy && _cancel != null)
            {
                try { _cancel.Cancel(); } catch { }
            }

            try { SaveAutoSyncSettings(); } catch { }

            base.OnFormClosing(e);
        }
    }
}

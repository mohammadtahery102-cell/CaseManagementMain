using System;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.DevCenter
{
    // ═════════════════════════════════════════════════════════════════════════
    // «مرکز کنترل توسعه‌دهنده» — پنجرهٔ مخفیِ پشتیبانی فنی.
    //
    // آموزش — این فرم عمداً هیچ‌جا ثبت/معرفی نشده است: نه در نوار کناری، نه در
    // منو، نه در تنظیمات، نه در ماتریس ماژول‌ها. تنها راه رسیدن به آن،
    // DevCenterAccess است که خودش نقش کاربر را بررسی می‌کند. حتی اگر کسی
    // به‌طور مستقیم این فرم را بسازد، سازنده دوباره نقش را بررسی می‌کند
    // (دفاع لایه‌ای).
    //
    // ظاهر عمداً ساده و اداری است — همان اجزای استاندارد UiTheme پروژه، بدون
    // هیچ طراحی جدید یا افکت.
    // ═════════════════════════════════════════════════════════════════════════
    internal sealed class FrmDevCenter : Form
    {
        private TabControl _tabs;

        // نمای کلی
        private FlowLayoutPanel _overviewCards;
        private ProgressBar     _healthBar;
        private Label           _healthLabel;

        // دکتر دیتابیس / نگهداری / لاگ / عیب‌یابی / کاوشگر
        private DataGridView _gridDoctor;
        private TextBox      _maintenanceOutput;
        private DataGridView _gridLog;
        private ComboBox     _cmbLogType, _cmbLogDays;
        private TextBox      _txtLogSearch;
        private DataGridView _gridDiagnostics;
        private ComboBox     _cmbTables;
        private TextBox      _txtExplorerSearch;
        private DataGridView _gridExplorer;
        private Label        _lblExplorerInfo;
        private TextBox      _devToolsOutput;
        private Label        _lblStatus;
        private ProgressBar  _progress;
        private Button       _btnCancel;
        private bool         _busy;
        private bool         _constructed;

        // منبعِ لغوِ عملیاتِ جاری. فقط روی نخِ رابط کاربری خوانده/نوشته می‌شود.
        private System.Threading.CancellationTokenSource _cts;

        // فونت‌های کارت‌های «نمای کلی» یک بار ساخته می‌شوند.
        // آموزش — UiTheme.Font/FontBold هر بار یک Font *جدید* می‌سازد و Font یک
        // منبع GDI است که Control.Dispose آن را آزاد نمی‌کند. ساختِ آن‌ها داخل
        // AddCard یعنی هر «تازه‌سازی» ۲۲ هَندلِ فونت نشت می‌کرد؛ پنجره‌ای که
        // ساعت‌ها باز می‌ماند این را جمع می‌کند.
        private Font _cardTitleFont;
        private Font _cardValueFont;

        public FrmDevCenter()
        {
            // دفاع لایه‌ای: حتی اگر این فرم مستقیم ساخته شود، بدون نقش مدیر کل
            // بلافاصله بسته می‌شود.
            if (!SecurityContext.IsSuperAdmin())
            {
                Load += delegate { Close(); };
                return;
            }

            DoubleBuffered = true;

            // ⚠ ساختِ پنجره هرگز نباید استثنا بدهد — «مرکز کنترل باید همیشه
            // باز شود». هر تب خودش محافظت شده است؛ این لایه آخرین تور ایمنی
            // برای هر چیزی است که از آن‌ها بیرون بزند.
            try
            {
                BuildUi();
            }
            catch (Exception ex)
            {
                try { Enterprise.ErrorLogger.Log(ex, "DevCenter.Construct"); } catch { }
                SetStatus("بخشی از مرکز کنترل بارگذاری نشد: " + ex.Message);
            }

            _constructed = true;

            // آموزش — چرا «نمای کلی» دیگر در سازنده اجرا نمی‌شود: GetOverview
            // یک PRAGMA quick_check، یک COUNT روی *همهٔ* جدول‌ها و یک
            // File.Exists روی *هر* مسیر عکس/سند ذخیره‌شده انجام می‌دهد. روی
            // پایگاه‌دادهٔ بزرگ یا مسیر شبکه، این یعنی پنجره چند ثانیه تا چند
            // دقیقه اصلاً ظاهر نمی‌شد — دقیقاً برعکسِ قولِ «همیشه باز می‌شود».
            // حالا پنجره فوراً می‌آید و داده در پس‌زمینه پر می‌شود.
            Shown += async delegate
            {
                await LoadOverview();
                await LoadDiagnostics();
            };
        }

        // ─── لایهٔ سومِ دفاع: نقش در *هر بار فعال شدن* دوباره بررسی می‌شود ───
        // آموزش — دو لایهٔ موجود (فیلتر صفحه‌کلید و سازنده) فقط لحظهٔ *باز شدن*
        // را می‌سنجند. این پنجره غیرمودال است و می‌تواند ساعت‌ها باز بماند؛ اگر
        // روزی قابلیت «خروج از حساب بدون بستن برنامه» اضافه شود، همین پنجرهٔ
        // بازمانده دسترسی خامِ دیتابیس را به کاربرِ بعدی می‌داد. امروز چنین
        // مسیری وجود ندارد، ولی هزینهٔ این محافظ سه خط است و خطای آیندهٔ
        // پرهزینه را حذف می‌کند.
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            if (!SecurityContext.IsSuperAdmin())
                Close();
        }

        // Fontها منبع GDI هستند و با Dispose فرم آزاد نمی‌شوند؛ اینجا صریحاً
        // آزاد می‌شوند.
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_cardTitleFont != null) { _cardTitleFont.Dispose(); _cardTitleFont = null; }
                if (_cardValueFont != null) { _cardValueFont.Dispose(); _cardValueFont = null; }
            }
            base.Dispose(disposing);
        }

        private void BuildUi()
        {
            Text = "مرکز کنترل توسعه‌دهنده";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeSmall);
            UiTheme.MakeMainWindow(this, 1180, 720);

            // آموزش — ترتیب مهم است و یک باگ واقعی را رفع می‌کند: سازندهٔ چند تب
            // (عیب‌یابی و کاوشگر) در پایان کارشان SetStatus را صدا می‌زنند. اگر
            // نوار وضعیت بعد از تب‌ها ساخته شود، _lblStatus هنوز null است و
            // همان لحظهٔ باز شدن پنجره دو خطای NullReference رخ می‌دهد و آن دو
            // تب خالی می‌مانند. پس نوار وضعیت *پیش از* تب‌ها ساخته می‌شود.
            Panel statusBar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = UiTheme.CardBack };
            _progress = new ProgressBar
            {
                Dock = DockStyle.Right, Width = 180, Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30, Visible = false
            };
            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "آماده — کاربر: " + SecurityContext.Username + " | رایانه: " + Environment.MachineName,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 12, 0)
            };
            // دکمهٔ لغو فقط در حین اجرای یک عملیات دیده می‌شود.
            _btnCancel = MakeButton("لغو عملیات", delegate { CancelCurrentOperation(); });
            _btnCancel.Dock = DockStyle.Right;
            _btnCancel.Margin = new Padding(0);
            _btnCancel.Visible = false;

            statusBar.Controls.Add(_lblStatus);
            statusBar.Controls.Add(_progress);
            statusBar.Controls.Add(_btnCancel);

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = UiTheme.FontBold(UiTheme.SizeBody),
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true
            };

            // آموزش — «مرکز کنترل باید همیشه باز شود»: این ماژول ابزارِ عیب‌یابیِ
            // یک سیستمِ خراب است، پس دقیقاً وقتی بیشترین ارزش را دارد که چیزی
            // سرِجایش نیست. هر تب جداگانه ساخته می‌شود و اگر ساختِ یکی شکست
            // بخورد، به‌جای بسته‌شدنِ کلِ پنجره فقط همان تب پیام خطا نشان
            // می‌دهد و بقیه سالم می‌مانند.
            AddTabSafely("نمای کلی",           BuildOverviewTab);
            AddTabSafely("دکتر دیتابیس",       BuildDoctorTab);
            AddTabSafely("نگهداری",            BuildMaintenanceTab);
            AddTabSafely("مرکز لاگ",           BuildLogTab);
            AddTabSafely("عیب‌یابی",            BuildDiagnosticsTab);
            AddTabSafely("کاوشگر دیتابیس",     BuildExplorerTab);
            AddTabSafely("ابزار توسعه‌دهنده",   BuildDevToolsTab);

            Controls.Add(_tabs);
            Controls.Add(statusBar);
        }

        private void AddTabSafely(string title, Func<TabPage> build)
        {
            try
            {
                _tabs.TabPages.Add(build());
            }
            catch (Exception ex)
            {
                // خطا فقط لاگ می‌شود؛ هیچ دیالوگی نشان داده نمی‌شود چون فرم
                // هنوز نمایش داده نشده و یک پیامِ مودال، باز شدن را قفل می‌کند.
                try { Enterprise.ErrorLogger.Log(ex, "DevCenter.BuildTab/" + title); } catch { }

                TabPage page = new TabPage(title) { BackColor = UiTheme.Background, Padding = new Padding(12) };
                page.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "این بخش بارگذاری نشد:" + Environment.NewLine + ex.Message,
                    Font = UiTheme.Font(UiTheme.SizeSmall),
                    ForeColor = UiTheme.Danger,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                _tabs.TabPages.Add(page);
            }
        }

        // ─── اجرای عملیات طولانی بدون قفل‌شدن رابط کاربری ─────────────────────
        // آموزش — چرا لازم است: «دکتر دیتابیس» و «بستهٔ پشتیبانی» تشخیص تکراری
        // را روی همهٔ پرونده‌ها اجرا می‌کنند؛ روی پایگاه‌دادهٔ بزرگ این کار
        // ده‌ها ثانیه طول می‌کشد. اگر روی نخِ رابط کاربری اجرا شود، پنجره
        // «پاسخ نمی‌دهد» می‌شود. اینجا کار سنگین به نخ پس‌زمینه می‌رود، نوار
        // پیشرفت نامعین نمایش داده می‌شود و تب‌ها موقتاً غیرفعال می‌شوند تا
        // کاربر نتواند عملیات موازیِ دوم را شروع کند.
        // آموزش — چرا محافظِ IsDisposed حیاتی است: کاربر می‌تواند وسطِ یک
        // عملیاتِ طولانی (دکتر دیتابیس / بستهٔ پشتیبانی) پنجره را ببندد. کارِ
        // پس‌زمینه ادامه دارد و وقتی تمام شد، ادامهٔ await روی نخِ رابط کاربری
        // اجرا می‌شود و می‌خواهد DataSource یک گریدِ *نابودشده* را ست کند ⇒
        // ObjectDisposedException. بدتر: مسیر خطا هم می‌خواست یک دیالوگ روی
        // همان فرمِ نابودشده باز کند ⇒ استثنای مدیریت‌نشده و بسته شدن برنامه.
        private bool Alive
        {
            get { return !IsDisposed && !Disposing; }
        }

        private async System.Threading.Tasks.Task RunBackgroundAsync<T>(
            string busyText,
            Func<IProgress<DevCenterService.DevProgress>, System.Threading.CancellationToken, T> work,
            Action<T> onDone, string source,
            Action<Exception> onError = null,
            Action<TimeSpan> onCancelled = null)
        {
            if (_busy) return;
            _busy = true;

            // Progress<T> بسترِ همگام‌سازی را در *لحظهٔ ساخت* می‌گیرد؛ چون
            // اینجا روی نخِ رابط کاربری ساخته می‌شود، گزارش‌های نخِ پس‌زمینه
            // خودکار به همان نخ برمی‌گردند و نیازی به Invoke دستی نیست.
            var progress = new Progress<DevCenterService.DevProgress>(ReportProgress);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _cts = new System.Threading.CancellationTokenSource();
            System.Threading.CancellationToken token = _cts.Token;

            BeginBusy(busyText);

            try
            {
                T result = await System.Threading.Tasks.Task.Run(
                    delegate { return work(progress, token); }, token);

                stopwatch.Stop();

                // پنجره بسته شده؛ نتیجه بی‌مصرف است و هر دست‌زدنی به کنترل‌ها
                // استثنا می‌دهد.
                if (!Alive) return;

                onDone(result);
            }
            catch (OperationCanceledException)
            {
                // لغو یک خطا نیست: نه در لاگ خطا ثبت می‌شود، نه دیالوگ خطا
                // نشان می‌دهد. فقط گزارشِ روشن به کاربر، همراه زمان سپری‌شده.
                stopwatch.Stop();
                if (!Alive) return;

                if (onCancelled != null) onCancelled(stopwatch.Elapsed);
                else SetStatus(CancelledText(stopwatch.Elapsed));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                if (onError != null) { if (Alive) onError(ex); else TryLog(ex, source); }
                else ShowError(ex, source);
            }
            finally
            {
                stopwatch.Stop();

                // ⚠ بازگرداندن وضعیت رابط کاربری در *هر* مسیر — موفق، خطا یا
                // لغو. هیچ حالتی نباید تب‌ها را غیرفعال یا نوار پیشرفت را روی
                // صفحه رها کند.
                EndBusy();
                _busy = false;

                System.Threading.CancellationTokenSource finished = _cts;
                _cts = null;
                if (finished != null) finished.Dispose();
            }
        }

        internal static string CancelledText(TimeSpan elapsed)
        {
            return "عملیات لغو شد — زمان سپری‌شده: " +
                   string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                 "{0:00}:{1:00}", (int)elapsed.TotalMinutes, elapsed.Seconds);
        }

        private void CancelCurrentOperation()
        {
            System.Threading.CancellationTokenSource cts = _cts;
            if (cts == null) return;

            try { cts.Cancel(); }
            catch (ObjectDisposedException) { return; }   // همین الان تمام شد

            if (_btnCancel != null) _btnCancel.Enabled = false;
            SetStatus("در حال لغو…");
        }

        // نمایش پیشرفتِ *واقعی*. اگر تعداد کل معلوم نباشد، حالت نامعین حفظ
        // می‌شود — درصدِ ساختگی ساخته نمی‌شود.
        private void ReportProgress(DevCenterService.DevProgress p)
        {
            if (!Alive || p == null || _progress == null || _progress.IsDisposed) return;

            // ⚠ Progress<T> گزارش‌ها را *ناهمگام* به نخ رابط کاربری Post می‌کند،
            // پس یک گزارشِ عقب‌مانده می‌تواند پس از پایان عملیات برسد و پیام
            // نهایی («انجام شد» یا «لغو شد») را با یک خطِ پیشرفتِ کهنه بپوشاند.
            // پس از پایان کار، گزارش‌های باقی‌مانده دور ریخته می‌شوند.
            if (!_busy) return;

            if (p.Total <= 0)
            {
                SetStatus(p.Text);
                return;
            }

            if (_progress.Style != ProgressBarStyle.Continuous)
            {
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Minimum = 0;
                _progress.Maximum = 100;
            }

            _progress.Value = Math.Max(0, Math.Min(100, p.Percent));
            SetStatus(p.Current + "/" + p.Total + " — " + p.Text + "   (" + p.Percent + "٪)");
        }

        // بستنِ پنجره در میانهٔ کار = لغو. بدون این، کارِ پس‌زمینه تا پایان
        // ادامه می‌داد و منابع را بی‌دلیل نگه می‌داشت.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            System.Threading.CancellationTokenSource cts = _cts;
            if (cts != null)
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
            }

            base.OnFormClosing(e);
        }

        private static void TryLog(Exception ex, string source)
        {
            try { Enterprise.ErrorLogger.Log(ex, "DevCenter." + source); } catch { }
        }

        private void BeginBusy(string text)
        {
            if (!Alive) return;
            _tabs.Enabled = false;

            // تا اولین گزارشِ پیشرفتِ واقعی، حالت نامعین است.
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.Visible = true;

            if (_btnCancel != null) { _btnCancel.Enabled = true; _btnCancel.Visible = true; }

            Cursor = Cursors.WaitCursor;
            SetStatus(text);
        }

        private void EndBusy()
        {
            if (!Alive) return;
            Cursor = Cursors.Default;

            if (_btnCancel != null) { _btnCancel.Visible = false; _btnCancel.Enabled = true; }

            _progress.Visible = false;
            _progress.Style = ProgressBarStyle.Marquee;   // آمادهٔ عملیات بعدی
            _tabs.Enabled = true;
        }

        // ═════════════════════════════════════════════════════════════════════
        // تب ۱ — نمای کلی سیستم
        // ═════════════════════════════════════════════════════════════════════
        private TabPage BuildOverviewTab()
        {
            TabPage page = new TabPage("نمای کلی") { BackColor = UiTheme.Background, Padding = new Padding(12) };

            Panel healthPanel = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = UiTheme.CardBack, Padding = new Padding(16, 12, 16, 12) };
            _healthLabel = new Label
            {
                Dock = DockStyle.Top, Height = 28, Text = "امتیاز سلامت سیستم",
                Font = UiTheme.FontBold(UiTheme.SizeBody), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _healthBar = new ProgressBar { Dock = DockStyle.Top, Height = 22, Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };
            healthPanel.Controls.Add(_healthBar);
            healthPanel.Controls.Add(_healthLabel);

            _overviewCards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = UiTheme.Background,
                Padding = new Padding(0, 12, 0, 0), AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true
            };

            page.Controls.Add(_overviewCards);
            page.Controls.Add(healthPanel);
            page.Controls.Add(MakeToolbar(
                MakeButton("تازه‌سازی", async delegate { await LoadOverview(); }),
                MakeButton("ساخت بستهٔ پشتیبانی", BuildSupportPackage),
                MakeButton("خروجی گزارش سلامت", ExportHealthReport)));
            return page;
        }

        // جمع‌آوری «نمای کلی» کارِ سنگینِ دیسک و دیتابیس است، پس مثل بقیهٔ
        // عملیات طولانی در پس‌زمینه اجرا می‌شود و رابط کاربری آزاد می‌ماند.
        private async System.Threading.Tasks.Task LoadOverview()
        {
            await RunBackgroundAsync(
                "در حال جمع‌آوری اطلاعات سیستم…",
                delegate (IProgress<DevCenterService.DevProgress> progress,
                          System.Threading.CancellationToken cancel)
                {
                    return DevCenterService.GetOverview();
                },
                RenderOverview,
                "LoadOverview");
        }

        private void RenderOverview(DevCenterService.SystemOverview o)
        {
            try
            {
                _healthBar.Value = o.HealthScore;
                _healthBar.ForeColor = o.HealthScore >= 85 ? UiTheme.Success
                                     : o.HealthScore >= 70 ? UiTheme.Warning : UiTheme.Danger;
                _healthLabel.Text = "امتیاز سلامت سیستم: " + o.HealthScore + " / 100 — " + o.Performance
                    + (o.HealthNotes.Count == 0 ? "" : "   (" + string.Join(" • ", o.HealthNotes) + ")");

                ClearCards();
                AddCard("نسخهٔ نرم‌افزار", o.AppVersion);
                AddCard("نسخهٔ دیتابیس", o.DbVersion);
                AddCard("وضعیت دیتابیس", o.DbStatus, o.DbStatus == "سالم");
                AddCard("حجم دیتابیس", o.DbSize);
                AddCard("مجموع رکوردها", Count(o.TotalRecords));
                // مقدار منفی یعنی جدولِ مربوطه در این پایگاه‌داده وجود ندارد.
                AddCard("تعداد کاربران", Count(o.TotalUsers));
                AddCard("کاربران فعال", Count(o.OnlineUsers));
                AddCard("مصرف حافظه", o.MemoryUsage);
                AddCard("مصرف فضای ذخیره‌سازی", o.StorageUsage);
                AddCard("مدت اجرای برنامه", o.Uptime);
                AddCard("وضعیت کارایی", o.Performance, o.HealthScore >= 70);

                SetStatus("نمای کلی به‌روزرسانی شد.");
            }
            catch (Exception ex) { ShowError(ex, "LoadOverview"); }
        }

        // مقدار منفی ⇒ آن معیار قابل اندازه‌گیری نبوده است.
        private static string Count(long value)
        {
            return value < 0 ? DevCenterService.NotAvailable : value.ToString("N0");
        }

        // Controls.Clear() فقط ارجاع‌ها را برمی‌دارد و کنترل‌ها را Dispose
        // نمی‌کند؛ هَندلِ پنجرهٔ هر Panel/Label تا اجرای Finalizer زنده می‌ماند.
        // با هر «تازه‌سازی» ۳۳ کنترل نشت می‌کرد.
        private void ClearCards()
        {
            while (_overviewCards.Controls.Count > 0)
            {
                Control card = _overviewCards.Controls[0];
                _overviewCards.Controls.RemoveAt(0);
                card.Dispose();
            }
        }

        private void AddCard(string title, string value, bool? ok = null)
        {
            if (_cardValueFont == null) _cardValueFont = UiTheme.FontBold(UiTheme.SizeBody);
            if (_cardTitleFont == null) _cardTitleFont = UiTheme.Font(UiTheme.SizeSmall);

            Panel card = new Panel
            {
                Width = 258, Height = 74, BackColor = UiTheme.CardBack,
                Margin = new Padding(0, 0, 10, 10), Padding = new Padding(12, 8, 12, 8)
            };

            Label lblValue = new Label
            {
                Dock = DockStyle.Fill, Text = value, AutoEllipsis = true,
                Font = _cardValueFont,
                ForeColor = ok == null ? UiTheme.TextDark : (ok.Value ? UiTheme.Success : UiTheme.Danger),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Label lblTitle = new Label
            {
                Dock = DockStyle.Top, Height = 22, Text = title,
                Font = _cardTitleFont, ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            card.Controls.Add(lblValue);
            card.Controls.Add(lblTitle);
            _overviewCards.Controls.Add(card);
        }

        // ═════════════════════════════════════════════════════════════════════
        // تب ۲ — دکتر دیتابیس
        // ═════════════════════════════════════════════════════════════════════
        private TabPage BuildDoctorTab()
        {
            TabPage page = new TabPage("دکتر دیتابیس") { BackColor = UiTheme.Background, Padding = new Padding(12) };

            _gridDoctor = MakeGrid();
            _gridDoctor.CellFormatting += delegate (object s, DataGridViewCellFormattingEventArgs e)
            {
                // ColumnIndex منفی برای سرستون ردیف‌ها می‌آید؛ بدون این محافظ،
                // دسترسی به Columns[-1] استثنا می‌دهد.
                if (e.ColumnIndex < 0 || e.ColumnIndex >= _gridDoctor.Columns.Count) return;
                if (_gridDoctor.Columns[e.ColumnIndex].Name != "وضعیت") return;

                // چهار وضعیت، سه رنگ: «در دسترس نیست» عمداً خاکستری است تا با
                // ایرادِ واقعی اشتباه گرفته نشود.
                switch (Convert.ToString(e.Value))
                {
                    case DevCenterService.StateHealthy:
                        e.CellStyle.ForeColor = UiTheme.Success; break;
                    case DevCenterService.StateUnavailable:
                        e.CellStyle.ForeColor = UiTheme.TextMuted; break;
                    default:
                        e.CellStyle.ForeColor = UiTheme.Danger; break;
                }
            };

            page.Controls.Add(_gridDoctor);
            page.Controls.Add(MakeToolbar(
                MakeButton("اجرای بررسی کامل", delegate { RunDoctor(); }),
                MakeButton("خروجی CSV", delegate { ExportGrid(_gridDoctor, "گزارش_سلامت_دیتابیس"); })));
            return page;
        }

        // بررسیِ تکراری‌ها روی همهٔ پرونده‌ها اجرا می‌شود و می‌تواند طولانی
        // باشد، پس در پس‌زمینه با نوار پیشرفت اجرا می‌گردد.
        private async void RunDoctor()
        {
            DevCenterService.LogAction("اجرای دکتر دیتابیس");

            DevCenterService.DoctorReport report = null;

            await RunBackgroundAsync(
                "در حال بررسی کامل دیتابیس…",
                delegate (IProgress<DevCenterService.DevProgress> progress,
                          System.Threading.CancellationToken cancel)
                {
                    report = DevCenterService.RunDatabaseDoctor(progress, cancel);

                    // دکتر دیتابیس در لغو استثنا نمی‌دهد (تا نتیجهٔ جزئی حفظ
                    // شود)؛ اینجا پس از ثبتِ نتیجه، لغو صریحاً اعلام می‌شود تا
                    // مسیرِ واحدِ «لغو» با زمان سپری‌شده اجرا گردد.
                    if (report.Cancelled) throw new OperationCanceledException(cancel);
                    return report;
                },
                delegate (DevCenterService.DoctorReport result)
                {
                    _gridDoctor.DataSource = result.Rows;
                    SetStatus(result.Cancelled
                        ? "بررسی ناقص — " + result.Completed + " از " + result.Total + " بررسی انجام شد."
                        : "بررسی کامل دیتابیس انجام شد.");
                },
                "RunDoctor",
                null,
                delegate (TimeSpan elapsed)
                {
                    // نتیجهٔ جزئی حفظ می‌شود: بررسی‌هایی که پیش از لغو تمام
                    // شده‌اند همچنان ارزش تشخیصی دارند.
                    if (report != null && report.Rows != null)
                    {
                        _gridDoctor.DataSource = report.Rows;
                        SetStatus(CancelledText(elapsed) + " — نتیجهٔ جزئی: " +
                                  report.Rows.Rows.Count + " از " + DevCenterService.DoctorCheckCount + " بررسی.");
                    }
                    else SetStatus(CancelledText(elapsed));
                });
        }

        // ═════════════════════════════════════════════════════════════════════
        // تب ۳ — نگهداری
        // ═════════════════════════════════════════════════════════════════════
        private TabPage BuildMaintenanceTab()
        {
            TabPage page = new TabPage("نگهداری") { BackColor = UiTheme.Background, Padding = new Padding(12) };

            _maintenanceOutput = MakeOutputBox();

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 92, BackColor = UiTheme.CardBack,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
                Padding = new Padding(10, 8, 10, 8)
            };

            actions.Controls.Add(MakeButton("بهینه‌سازی دیتابیس", delegate {
                RunMaintenance("بهینه‌سازی (VACUUM) دیتابیس",
                    new DevCenterService.DevOperation(DevCenterService.OptimizeDatabase)); }));
            actions.Controls.Add(MakeButton("بازسازی Indexها", delegate {
                RunMaintenance("بازسازی همهٔ Indexها",
                    new DevCenterService.DevOperation(DevCenterService.RebuildIndexes)); }));
            actions.Controls.Add(MakeButton("به‌روزرسانی آمار", delegate {
                RunMaintenance("به‌روزرسانی آمار بهینه‌ساز (ANALYZE)",
                    new DevCenterService.DevOperation(DevCenterService.RefreshStatistics)); }));
            actions.Controls.Add(MakeButton("بررسی پیوست‌ها", delegate {
                RunMaintenance("بررسی پیوست‌ها",
                    new DevCenterService.DevOperation(DevCenterService.VerifyAttachments)); }));
            actions.Controls.Add(MakeButton("بررسی مسیرهای ذخیره‌سازی", delegate {
                RunMaintenance("بررسی مسیرهای ذخیره‌سازی",
                    new DevCenterService.DevOperation(DevCenterService.VerifyStorage)); }));
            actions.Controls.Add(MakeButton("بررسی بکاپ", delegate { VerifyBackup(); }));
            actions.Controls.Add(MakeButton("پاکسازی فایل‌های موقت", delegate {
                RunMaintenance("پاکسازی فایل‌های موقت",
                    new DevCenterService.DevOperation(DevCenterService.ClearTemporaryFiles)); }));

            // ─── اتصال به سرور ───
            // آموزش — تا پیش از این، «آدرس سرور» و «ورود به سرور» هیچ رابط
            // کاربری نداشتند: آدرس فقط با ویرایشِ دستیِ جدول SyncState قابل
            // تنظیم بود و HttpSyncTransport.Login از هیچ کجا صدا زده نمی‌شد،
            // پس همگام‌سازی همیشه با «هنوز واردِ سرور نشده‌اید» شکست می‌خورد.
            // این دکمه همان حلقهٔ گمشده را می‌بندد و باید *پیش از* دو دکمهٔ
            // بعدی استفاده شود.
            actions.Controls.Add(MakeButton("اتصال به سرور", delegate {
                using (var frm = new CaseManagement.Sync.FrmServerConnection())
                    frm.ShowDialog(this); }));

            // ─── همگام‌سازی دستی (فاز ۶.۲) ───
            // از همان مسیر «نگهداری» استفاده می‌کند: تأیید، اجرای پس‌زمینه،
            // نوار پیشرفت، دکمهٔ لغو و ثبت در لاگ — بدون هیچ الگوی جدیدی.
            actions.Controls.Add(MakeButton("همگام‌سازی با سرور", delegate {
                RunMaintenance("همگام‌سازی با سرور",
                    new DevCenterService.DevOperation(DevCenterService.RunSynchronization)); }));

            // ─── گزارش تشخیصی (فاز C) ───
            // فقط می‌خواند و هیچ چیزی را تغییر نمی‌دهد، ولی از همان مسیرِ
            // نگهداری می‌رود تا خروجی در همان کادرِ متنیِ آشنا بنشیند و
            // کاربر بتواند کپی‌اش کند.
            actions.Controls.Add(MakeButton("گزارش تشخیصی همگام‌سازی", delegate {
                RunMaintenance("گزارش تشخیصی همگام‌سازی",
                    new DevCenterService.DevOperation(DevCenterService.RunSyncDiagnostics)); }));

            page.Controls.Add(_maintenanceOutput);
            page.Controls.Add(actions);
            return page;
        }

        // آموزش — چرا نگهداری *باید* پس‌زمینه باشد: VACUUM کلِ فایل دیتابیس را
        // بازنویسی می‌کند، «بررسی پیوست‌ها» روی هر سند یک File.Exists می‌زند و
        // «پاکسازی موقت» کلِ درختِ پوشه را می‌پیماید. اجرای این‌ها روی نخِ رابط
        // کاربری یعنی پنجرهٔ «پاسخ نمی‌دهد» و کاربری که فکر می‌کند برنامه
        // کرش کرده آن را با Task Manager می‌بندد — درست وسطِ بازنویسی دیتابیس.
        private async void RunMaintenance(string title, DevCenterService.DevOperation operation)
        {
            if (_busy) return;
            if (!UiTheme.ShowConfirm(this, title + " انجام شود؟", "تأیید عملیات نگهداری"))
                return;

            DevCenterService.LogAction("نگهداری: " + title);
            Append(_maintenanceOutput, "▪ " + title);

            await RunBackgroundAsync(
                title + "…",
                delegate (IProgress<DevCenterService.DevProgress> progress,
                          System.Threading.CancellationToken cancel)
                {
                    return operation(progress, cancel);
                },
                delegate (string result)
                {
                    Append(_maintenanceOutput, result);
                    SetStatus(title + " انجام شد.");
                },
                "Maintenance/" + title,
                delegate (Exception ex)
                {
                    // خطا مثل قبل در همان خروجیِ تب گزارش می‌شود، نه با یک
                    // دیالوگِ مودال.
                    Append(_maintenanceOutput, "خطا: " + ex.Message);
                    TryLog(ex, "Maintenance/" + title);
                    SetStatus(title + " با خطا مواجه شد.");
                },
                delegate (TimeSpan elapsed)
                {
                    string text = CancelledText(elapsed);
                    Append(_maintenanceOutput, text);
                    DevCenterService.LogAction("نگهداری لغو شد: " + title);
                    SetStatus(text);
                });
        }

        private void VerifyBackup()
        {
            using (var fbd = new FolderBrowserDialog { Description = "پوشهٔ بکاپ را برای بررسی انتخاب کنید" })
            {
                if (fbd.ShowDialog(this) != DialogResult.OK) return;
                string folder = fbd.SelectedPath;

                // مسیر در عنوان می‌آید تا در ردّ ممیزی هم ثبت شود (چه پوشه‌ای
                // بررسی شده است).
                RunMaintenance("بررسی بکاپ: " + folder,
                    delegate (IProgress<DevCenterService.DevProgress> progress,
                              System.Threading.CancellationToken cancel)
                    {
                        return DevCenterService.VerifyBackup(folder, progress, cancel);
                    });
            }
        }

        // ─── اجرای عملیاتِ کوتاهِ تأییدی (فقط «ابزار توسعه‌دهنده») ─────────────
        // آموزش — چرا این‌ها برخلاف «نگهداری» روی نخِ رابط کاربری می‌مانند:
        // همگی در چند میلی‌ثانیه تمام می‌شوند (پاک کردن کش یا نوشتن یک تنظیم)،
        // ولی مستقیماً به کشِ SettingsHelper/PermissionService دست می‌زنند که
        // Dictionaryهای بدون قفل هستند و همان لحظه ممکن است نخِ رابط کاربری
        // آن‌ها را بخواند. بردنشان به نخِ پس‌زمینه یک «سرعتِ نامحسوس» می‌داد و
        // در عوض یک شرطِ رقابتیِ واقعی روی ساختمان دادهٔ مشترک می‌ساخت.
        private void RunOperation(TextBox output, string logPrefix, string confirmTitle,
                                  string title, Func<string> operation)
        {
            if (_busy) return;
            if (!UiTheme.ShowConfirm(this, title + " انجام شود؟", confirmTitle))
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                DevCenterService.LogAction(logPrefix + ": " + title);
                Append(output, "▪ " + title);
                Append(output, operation());
                SetStatus(title + " انجام شد.");
            }
            catch (Exception ex)
            {
                Append(output, "خطا: " + ex.Message);
                Enterprise.ErrorLogger.Log(ex, "DevCenter/" + title);
                SetStatus(title + " با خطا مواجه شد.");
            }
            finally { Cursor = Cursors.Default; }
        }

        private static void Append(TextBox output, string text)
        {
            output.AppendText(
                DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
        }

        // ═════════════════════════════════════════════════════════════════════
        // تب ۴ — مرکز لاگ
        // ═════════════════════════════════════════════════════════════════════
        private TabPage BuildLogTab()
        {
            TabPage page = new TabPage("مرکز لاگ") { BackColor = UiTheme.Background, Padding = new Padding(12) };

            _cmbLogType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(4) };
            _cmbLogType.Items.AddRange(new object[] { "لاگ خطا", "لاگ ممیزی", "لاگ امنیتی", "لاگ سیستم" });
            _cmbLogType.SelectedIndex = 0;
            _cmbLogType.SelectedIndexChanged += delegate { LoadLog(); };

            _cmbLogDays = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Margin = new Padding(4) };
            _cmbLogDays.Items.AddRange(new object[] { "۷ روز", "۳۰ روز", "۹۰ روز", "همه" });
            _cmbLogDays.SelectedIndex = 1;
            _cmbLogDays.SelectedIndexChanged += delegate { LoadLog(); };

            _txtLogSearch = new TextBox { Width = 220, Margin = new Padding(4) };
            _txtLogSearch.TextChanged += delegate { ApplyLogFilter(); };

            _gridLog = MakeGrid();

            page.Controls.Add(_gridLog);
            page.Controls.Add(MakeToolbar(
                MakeLabel("نوع:"), _cmbLogType,
                MakeLabel("بازه:"), _cmbLogDays,
                MakeLabel("جست‌وجو:"), _txtLogSearch,
                MakeButton("تازه‌سازی", delegate { LoadLog(); }),
                MakeButton("خروجی CSV", delegate { ExportGrid(_gridLog, "لاگ"); })));

            // بارگذاری اولیه — بدون این، تب «مرکز لاگ» تا اولین تغییر فیلتر
            // خالی می‌ماند (مقداردهی SelectedIndex پیش از اتصال رویدادها انجام
            // شده، پس رویداد تغییر شلیک نمی‌شود).
            LoadLog();
            return page;
        }

        private void LoadLog()
        {
            try
            {
                int days = _cmbLogDays.SelectedIndex == 0 ? 7
                         : _cmbLogDays.SelectedIndex == 1 ? 30
                         : _cmbLogDays.SelectedIndex == 2 ? 90 : 0;

                DataTable table;
                switch (_cmbLogType.SelectedIndex)
                {
                    case 1:  table = DevCenterService.GetAuditLog(days);    break;
                    case 2:  table = DevCenterService.GetSecurityLog(days); break;
                    case 3:  table = DevCenterService.GetSystemLog(days);   break;
                    default: table = DevCenterService.GetErrorLog(days);    break;
                }

                _gridLog.DataSource = table;
                ApplyLogFilter();
                SetStatus(_cmbLogType.Text + " بارگذاری شد — " + table.Rows.Count.ToString("N0") + " ردیف.");
            }
            catch (Exception ex) { ShowError(ex, "LoadLog"); }
        }

        // فیلتر روی خودِ DataTable انجام می‌شود (RowFilter)، پس جست‌وجو بدون
        // رفت‌وبرگشت دوباره به دیتابیس و آنی است.
        private void ApplyLogFilter()
        {
            DataTable table = _gridLog.DataSource as DataTable;
            if (table == null) return;

            string raw = _txtLogSearch.Text.Trim();
            if (raw.Length == 0) { table.DefaultView.RowFilter = ""; return; }

            string text = EscapeRowFilterValue(raw);

            var parts = new System.Collections.Generic.List<string>();
            foreach (DataColumn column in table.Columns)
                parts.Add("CONVERT([" + column.ColumnName + "], 'System.String') LIKE '%" + text + "%'");

            try { table.DefaultView.RowFilter = string.Join(" OR ", parts); }
            catch { table.DefaultView.RowFilter = ""; }
        }

        // آموزش — یک باگِ خاموش: در RowFilter کاراکترهای * % [ ] معنی خاص
        // دارند. نسخهٔ قبلی فقط ' را escape می‌کرد، پس جست‌وجویی که شامل «[»
        // بود استثنا می‌داد، catch فیلتر را خالی می‌کرد و گرید *همهٔ* ردیف‌ها را
        // نشان می‌داد — کاربر فکر می‌کرد فیلتر شده در حالی که نشده بود. بدترین
        // نوع باگ: نتیجهٔ غلط بدون هیچ پیام خطا.
        private static string EscapeRowFilterValue(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                switch (c)
                {
                    case '\'': sb.Append("''"); break;
                    case '*':
                    case '%':
                    case '[':
                    case ']': sb.Append('[').Append(c).Append(']'); break;
                    default:  sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // ═════════════════════════════════════════════════════════════════════
        // تب ۵ — عیب‌یابی
        // ═════════════════════════════════════════════════════════════════════
        private TabPage BuildDiagnosticsTab()
        {
            TabPage page = new TabPage("عیب‌یابی") { BackColor = UiTheme.Background, Padding = new Padding(12) };

            _gridDiagnostics = MakeGrid();

            page.Controls.Add(_gridDiagnostics);
            page.Controls.Add(MakeToolbar(
                MakeButton("تازه‌سازی", async delegate { await LoadDiagnostics(); }),
                MakeButton("ماژول‌های نصب‌شده", delegate { ShowTable("ماژول‌های نصب‌شده", DevCenterService.GetInstalledModules()); }),
                MakeButton("پلاگین‌های بارگذاری‌شده", delegate { ShowTable("پلاگین‌های بارگذاری‌شده", DevCenterService.GetLoadedPlugins()); }),
                MakeButton("قفل‌های فعال", delegate { ShowTable("قفل‌های فعال رکورد", DevCenterService.GetActiveLocks()); }),
                MakeButton("تعارض‌های همگام‌سازی", delegate { ShowConflicts(); }),
                MakeButton("خروجی CSV", delegate { ExportGrid(_gridDiagnostics, "عیب‌یابی"); })));

            // بارگذاری در رویداد Shown انجام می‌شود (نه اینجا)، تا با بارگذاری
            // «نمای کلی» تداخل نکند و پنجره فوراً ظاهر شود.
            return page;
        }

        // ⚠ Task برمی‌گرداند (نه async void): «نمای کلی» و «عیب‌یابی» هر دو در
        // زمان باز شدن اجرا می‌شوند و هر دو از همان قفلِ _busy استفاده می‌کنند.
        // اگر هم‌زمان شلیک شوند، دومی بی‌صدا رد می‌شود و تبِ مربوطه خالی
        // می‌ماند. پس در رویداد Shown پشت‌سرهم await می‌شوند.
        private async System.Threading.Tasks.Task LoadDiagnostics()
        {
            await RunBackgroundAsync(
                "در حال جمع‌آوری اطلاعات عیب‌یابی…",
                delegate (IProgress<DevCenterService.DevProgress> progress,
                          System.Threading.CancellationToken cancel)
                {
                    return DevCenterService.GetDiagnostics(progress, cancel);
                },
                delegate (DataTable result)
                {
                    _gridDiagnostics.DataSource = result;
                    SetStatus("اطلاعات عیب‌یابی به‌روزرسانی شد.");
                },
                "LoadDiagnostics");
        }

        // بازبینی تعارض‌های همگام‌سازی (فاز ۴).
        // آموزش — چرا از اینجا و نه از منوی اصلی: افزودن گزینه به ناوبریِ
        // اصلی یعنی تغییر در رابط کاربریِ موجود که صریحاً ممنوع بود. مرکز
        // کنترل از قبل مسیرِ سازمانیِ ابزارهای مدیریتی است و دسترسی‌اش هم
        // محدود به «مدیر کل» است.
        private void ShowConflicts()
        {
            try
            {
                using (var frm = new Sync.FrmSyncConflicts())
                {
                    DevCenterService.LogAction("بازبینی تعارض‌های همگام‌سازی");
                    frm.ShowDialog(this);
                }
            }
            catch (Exception ex) { ShowError(ex, "ShowConflicts"); }
        }

        // پنجرهٔ کوچکِ نمایش یک جدول — برای ماژول‌ها/پلاگین‌ها/قفل‌ها.
        private void ShowTable(string title, DataTable table)
        {
            using (Form frm = new Form())
            {
                frm.Text = title;
                frm.RightToLeft = RightToLeft.Yes;
                frm.RightToLeftLayout = false;
                frm.StartPosition = FormStartPosition.CenterParent;
                frm.ClientSize = new Size(880, 480);
                frm.BackColor = UiTheme.Background;
                frm.Font = UiTheme.Font(UiTheme.SizeSmall);

                DataGridView grid = MakeGrid();
                grid.DataSource = table;
                frm.Controls.Add(grid);
                frm.ShowDialog(this);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // تب ۶ — کاوشگر دیتابیس (فقط خواندنی)
        // ═════════════════════════════════════════════════════════════════════
        private TabPage BuildExplorerTab()
        {
            TabPage page = new TabPage("کاوشگر دیتابیس") { BackColor = UiTheme.Background, Padding = new Padding(12) };

            _cmbTables = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, Margin = new Padding(4) };
            _cmbTables.SelectedIndexChanged += delegate { LoadExplorer(); };

            _txtExplorerSearch = new TextBox { Width = 220, Margin = new Padding(4) };
            _txtExplorerSearch.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadExplorer(); }
            };

            // ⚠ ReadOnly در سه سطح: خودِ گرید، ممنوعیت افزودن/حذف ردیف، و
            // نبودِ هرگونه مسیر نوشتن در DevCenterService.BrowseTable.
            _gridExplorer = MakeGrid();

            _lblExplorerInfo = new Label
            {
                Dock = DockStyle.Bottom, Height = 24,
                Text = "حالت فقط‌خواندنی — ویرایش داده در این بخش ممکن نیست.",
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            page.Controls.Add(_gridExplorer);
            page.Controls.Add(_lblExplorerInfo);
            page.Controls.Add(MakeToolbar(
                MakeLabel("جدول:"), _cmbTables,
                MakeLabel("جست‌وجو:"), _txtExplorerSearch,
                MakeButton("نمایش", delegate { LoadExplorer(); }),
                MakeButton("تعداد رکورد جدول‌ها", delegate { ShowTable("تعداد رکورد جدول‌ها", DevCenterService.GetTableRowCounts()); }),
                MakeButton("خروجی CSV", delegate { ExportGrid(_gridExplorer, "کاوشگر_" + _cmbTables.Text); })));

            try
            {
                foreach (string name in DevCenterService.GetTableNames())
                    _cmbTables.Items.Add(name);
                if (_cmbTables.Items.Count > 0) _cmbTables.SelectedIndex = 0;
            }
            catch (Exception ex) { ShowError(ex, "BuildExplorerTab"); }

            return page;
        }

        private void LoadExplorer()
        {
            if (_cmbTables.SelectedItem == null) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                string table = Convert.ToString(_cmbTables.SelectedItem);

                // مشاهدهٔ خامِ جدول‌ها حساس‌ترین کارِ این ماژول است، پس مثل
                // بقیهٔ عملیات در لاگ امنیتی ثبت می‌شود (بخش SECURITY).
                DevCenterService.LogAction("مشاهدهٔ جدول: " + table);

                DataTable data = DevCenterService.BrowseTable(table, _txtExplorerSearch.Text, 1000);

                _gridExplorer.DataSource = data;
                _lblExplorerInfo.Text = "حالت فقط‌خواندنی — جدول «" + table + "»: "
                                      + data.Rows.Count.ToString("N0") + " ردیف (حداکثر ۱۰۰۰ ردیف نمایش داده می‌شود).";
                SetStatus("جدول " + table + " نمایش داده شد.");
            }
            catch (Exception ex) { ShowError(ex, "LoadExplorer"); }
            finally { Cursor = Cursors.Default; }
        }

        // ═════════════════════════════════════════════════════════════════════
        // تب ۷ — ابزار توسعه‌دهنده
        // ═════════════════════════════════════════════════════════════════════
        private TabPage BuildDevToolsTab()
        {
            TabPage page = new TabPage("ابزار توسعه‌دهنده") { BackColor = UiTheme.Background, Padding = new Padding(12) };

            _devToolsOutput = MakeOutputBox();

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 92, BackColor = UiTheme.CardBack,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
                Padding = new Padding(10, 8, 10, 8)
            };

            actions.Controls.Add(MakeButton("فعال‌سازی حالت اشکال‌زدایی", delegate {
                RunDevTool("فعال‌سازی حالت اشکال‌زدایی", delegate { return DevCenterService.SetDebugMode(true); }); }));
            actions.Controls.Add(MakeButton("غیرفعال‌سازی حالت اشکال‌زدایی", delegate {
                RunDevTool("غیرفعال‌سازی حالت اشکال‌زدایی", delegate { return DevCenterService.SetDebugMode(false); }); }));
            actions.Controls.Add(MakeButton("بارگذاری مجدد پیکربندی", delegate {
                RunDevTool("بارگذاری مجدد پیکربندی", DevCenterService.ReloadConfiguration); }));
            actions.Controls.Add(MakeButton("بارگذاری مجدد مجوزها", delegate {
                RunDevTool("بارگذاری مجدد مجوزها", DevCenterService.ReloadPermissions); }));
            actions.Controls.Add(MakeButton("بارگذاری مجدد جدول‌های مرجع", delegate {
                RunDevTool("بارگذاری مجدد جدول‌های مرجع", DevCenterService.ReloadLookups); }));
            actions.Controls.Add(MakeButton("تست اعلان‌ها", delegate {
                RunDevTool("تست اعلان‌ها", DevCenterService.TestNotifications); }));
            actions.Controls.Add(MakeButton("تست ایمیل", delegate {
                RunDevTool("تست ایمیل", DevCenterService.TestEmail); }));
            actions.Controls.Add(MakeButton("تست پیامک", delegate {
                RunDevTool("تست پیامک", DevCenterService.TestSms); }));

            page.Controls.Add(_devToolsOutput);
            page.Controls.Add(actions);

            Append(_devToolsOutput, "حالت اشکال‌زدایی فعلی: " +
                   (DevCenterService.IsDebugMode() ? "فعال" : "غیرفعال"));
            return page;
        }

        private void RunDevTool(string title, Func<string> operation)
        {
            RunOperation(_devToolsOutput, "ابزار توسعه‌دهنده", "تأیید عملیات", title, operation);
        }

        // ═════════════════════════════════════════════════════════════════════
        // بستهٔ پشتیبانی
        // ═════════════════════════════════════════════════════════════════════
        // ساخت بسته شامل گزارش سلامت کامل است، پس مثل «دکتر دیتابیس» در
        // پس‌زمینه و با نوار پیشرفت اجرا می‌شود.
        private async void BuildSupportPackage(object sender, EventArgs e)
        {
            string target;
            using (var sfd = new SaveFileDialog
            {
                Title = "ذخیرهٔ بستهٔ پشتیبانی",
                Filter = "فایل ZIP|*.zip",
                FileName = "SupportPackage_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                target = sfd.FileName;
            }

            DevCenterService.LogAction("ساخت بستهٔ پشتیبانی");

            await RunBackgroundAsync(
                "در حال ساخت بستهٔ پشتیبانی…",
                delegate (IProgress<DevCenterService.DevProgress> progress,
                          System.Threading.CancellationToken cancel)
                {
                    return DevCenterService.BuildSupportPackage(target, progress, cancel);
                },
                delegate (string path)
                {
                    SetStatus("بستهٔ پشتیبانی ساخته شد.");
                    UiTheme.ShowSuccess(this, "بستهٔ پشتیبانی ساخته شد:" + Environment.NewLine + path);
                },
                "BuildSupportPackage",
                null,
                delegate (TimeSpan elapsed)
                {
                    // بستهٔ ناقص ساخته نمی‌شود؛ فایل موقت پاک شده و مقصد
                    // دست‌نخورده مانده است.
                    DevCenterService.LogAction("ساخت بستهٔ پشتیبانی لغو شد");
                    SetStatus(CancelledText(elapsed) + " — بستهٔ ناقصی ساخته نشد.");
                });
        }

        // ═════════════════════════════════════════════════════════════════════
        // گزارش سلامت سازمانی (PDF)
        // ═════════════════════════════════════════════════════════════════════
        private async void ExportHealthReport(object sender, EventArgs e)
        {
            if (_busy) return;

            string target;
            using (var sfd = new SaveFileDialog
            {
                Title = "ذخیرهٔ گزارش سلامت",
                Filter = "فایل PDF|*.pdf",
                FileName = "HealthReport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                target = sfd.FileName;
            }

            DevCenterService.LogAction("خروجی گزارش سلامت");

            await RunBackgroundAsync(
                "در حال ساخت گزارش سلامت…",
                delegate (IProgress<DevCenterService.DevProgress> progress,
                          System.Threading.CancellationToken cancel)
                {
                    return DevCenterHealthReport.Export(target, progress, cancel);
                },
                delegate (DevCenterHealthReport.HealthReportResult result)
                {
                    SetStatus("گزارش سلامت ساخته شد.");
                    UiTheme.ShowSuccess(this,
                        (result.IsPdf ? "گزارش سلامت (PDF) ساخته شد:" : "گزارش سلامت ساخته شد:")
                        + Environment.NewLine + result.Path
                        + (string.IsNullOrEmpty(result.Note) ? "" : Environment.NewLine + Environment.NewLine + result.Note));
                },
                "ExportHealthReport",
                null,
                delegate (TimeSpan elapsed)
                {
                    DevCenterService.LogAction("خروجی گزارش سلامت لغو شد");
                    SetStatus(CancelledText(elapsed) + " — گزارشی ساخته نشد.");
                });
        }

        // ═════════════════════════════════════════════════════════════════════
        // کمکی‌های UI مشترک
        // ═════════════════════════════════════════════════════════════════════
        private Panel MakeToolbar(params Control[] items)
        {
            Panel bar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, Padding = new Padding(10, 8, 10, 6), AutoScroll = true
            };
            foreach (Control item in items) flow.Controls.Add(item);
            bar.Controls.Add(flow);
            return bar;
        }

        private Button MakeButton(string text, EventHandler onClick)
        {
            Button b = UiTheme.CreateSecondaryButton(text, "");
            b.Size = new Size(Math.Max(120, TextRenderer.MeasureText(text, b.Font).Width + 26), 30);
            b.Margin = new Padding(4, 0, 4, 0);
            b.Click += onClick;
            return b;
        }

        private Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text, AutoSize = false, Width = 58, Height = 30,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                Margin = new Padding(4, 0, 0, 0)
            };
        }

        private DataGridView MakeGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(grid);
            return grid;
        }

        private TextBox MakeOutputBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, BackColor = Color.White,
                RightToLeft = RightToLeft.Yes,
                Font = UiTheme.Font(UiTheme.SizeSmall)
            };
        }

        private void ExportGrid(DataGridView grid, string baseName)
        {
            DataTable table = grid.DataSource as DataTable;
            if (table == null)
            {
                UiTheme.ShowWarning(this, "داده‌ای برای خروجی وجود ندارد.");
                return;
            }

            // آموزش — خروجی باید *همان چیزی* باشد که روی صفحه دیده می‌شود.
            // پیش‌تر خودِ DataTable ذخیره می‌شد، پس وقتی کاربر در «مرکز لاگ»
            // جست‌وجو کرده بود، فایل CSV ردیف‌های *پنهان* را هم شامل می‌شد —
            // هم برخلاف مستندات، هم نشتِ دادهٔ بیشتر از آنچه کاربر قصد داشت.
            DataTable exportTable = table;
            try
            {
                if (!string.IsNullOrEmpty(table.DefaultView.RowFilter))
                    exportTable = table.DefaultView.ToTable();
            }
            catch { exportTable = table; }

            if (exportTable.Rows.Count == 0)
            {
                UiTheme.ShowWarning(this, "داده‌ای برای خروجی وجود ندارد.");
                return;
            }

            using (var sfd = new SaveFileDialog
            {
                Title = "ذخیرهٔ خروجی",
                Filter = "فایل CSV|*.csv",
                FileName = baseName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    DevCenterService.LogAction("خروجی گرفتن: " + baseName +
                                               " (" + exportTable.Rows.Count.ToString("N0") + " ردیف)");
                    DevCenterService.ExportToCsv(exportTable, sfd.FileName);
                    UiTheme.ShowSuccess(this, "خروجی ذخیره شد:" + Environment.NewLine + sfd.FileName);
                }
                catch (Exception ex) { ShowError(ex, "ExportGrid"); }
            }
        }

        private void SetStatus(string text)
        {
            // محافظِ null: بعضی تب‌ها در حین ساختِ فرم SetStatus را صدا می‌زنند؛
            // این محافظ تضمین می‌کند تغییرِ ترتیبِ آینده دوباره باگ نسازد.
            if (_lblStatus == null || !Alive || _lblStatus.IsDisposed) return;

            _lblStatus.Text = DateTime.Now.ToString("HH:mm:ss") + "  " + text
                            + "   |   کاربر: " + SecurityContext.Username
                            + " | رایانه: " + Environment.MachineName;
        }

        // آموزش — تفاوت «حین ساخت» و «حین کار»: پیش از نمایش پنجره هیچ دیالوگ
        // مودالی نباید باز شود، وگرنه باز شدنِ مرکز کنترل قفل می‌شود (و همان
        // چیزی که باید همیشه باز شود، هرگز باز نمی‌شود). در آن مرحله خطا فقط
        // ثبت و در نوار وضعیت گزارش می‌گردد.
        private void ShowError(Exception ex, string source)
        {
            TryLog(ex, source);

            // پنجره بسته شده ⇒ نه دیالوگ، نه نوار وضعیت. فقط لاگ.
            if (!Alive) return;

            if (_constructed)
                UiTheme.ShowError(this, "خطا: " + ex.Message);
            else
                SetStatus("خطا در " + source + ": " + ex.Message);
        }
    }
}

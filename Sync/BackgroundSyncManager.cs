using System;
using System.Text;
using System.Threading;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // مدیرِ همگام‌سازیِ پس‌زمینه — فاز B، بخش ۱.
    //
    // آموزش — این کلاس هیچ منطقِ همگام‌سازیِ تازه‌ای ندارد. تمام کارِ واقعی
    // همچنان در SyncService است؛ اینجا فقط پاسخِ چهار پرسش داده می‌شود:
    // «کِی؟»، «چند تا هم‌زمان؟»، «چطور لغو شود؟» و «چطور گزارش شود؟».
    // اگر منطق را اینجا تکرار می‌کردیم، دو مسیرِ همگام‌سازی به‌وجود می‌آمد که
    // به‌مرور از هم فاصله می‌گرفتند — و باگ‌هایشان فقط در یکی حل می‌شد.
    //
    // چهار محرک (همان چیزی که خواسته شده):
    //   • راه‌اندازی برنامه (با تأخیرِ کوتاه تا پنجرهٔ اصلی بالا بیاید)
    //   • بازگشتِ اینترنت (NetworkAvailabilityChanged)
    //   • زمان‌سنجِ قابل تنظیم
    //   • درخواستِ دستی
    //
    // ⚠ سه قاعدهٔ ایمنی:
    //   ۱. قفلِ سراسری — هرگز دو همگام‌سازی هم‌زمان اجرا نمی‌شود. هر
    //      درخواستِ هم‌زمانِ دیگری بی‌صدا رد می‌شود (نه صف می‌شود، نه خطا
    //      می‌دهد): همگام‌سازی ذاتاً ازسرگیری‌پذیر است و اجرای بعدی هرچه
    //      جامانده را برمی‌دارد.
    //   ۲. هیچ کاری روی نخِ رابط کاربری انجام نمی‌شود و این کلاس هیچ کنترلی
    //      را لمس نمی‌کند. رویدادها روی نخِ پس‌زمینه صدا زده می‌شوند و
    //      مشترک، خودش با Invoke به نخِ فرم می‌رود.
    //   ۳. قطعِ وسطِ کار داده از دست نمی‌دهد: کلِ وضعیت در پایگاه‌داده است
    //      (SyncOutbox، SyncFile، SyncFileDownload) و اجرای بعدی از همان‌جا
    //      ادامه می‌دهد.
    // ═════════════════════════════════════════════════════════════════════════
    // فازهای قابلِ‌انتخاب برای اجرای دستیِ یک بخش از همگام‌سازی (RunManualPhase).
    public enum SyncPhaseKind { Upload, Download, Full }

    public static class BackgroundSyncManager
    {
        // ─── کلیدهای پیکربندی در SyncState (همان جدول کلید/مقدارِ فاز ۲) ────
        public const string KeyAutoSyncEnabled = "AutoSyncEnabled";
        public const string KeyIntervalMinutes = "AutoSyncIntervalMinutes";
        public const string KeyLastAttemptAt   = "AutoSyncLastAttemptAt";
        public const string KeyLastResult      = "AutoSyncLastResult";

        // «آخرین ناموفق» جدا از «آخرین اجرا» نگه داشته می‌شود.
        //
        // آموزش — چرا جدا: اگر فقط نتیجهٔ آخرین اجرا ثبت می‌شد، یک اجرای موفق
        // بلافاصله ردِ شکستِ قبلی را پاک می‌کرد. آن‌وقت الگوی «هر بار یکی
        // درمیان شکست می‌خورد» — که دقیقاً نشانهٔ یک مشکلِ واقعی است — هرگز
        // دیده نمی‌شد.
        public const string KeyLastFailureAt   = "AutoSyncLastFailureAt";
        public const string KeyLastFailure     = "AutoSyncLastFailure";

        public const int DefaultIntervalMinutes = 15;
        public const int MinimumIntervalMinutes = 1;

        // تأخیرِ اجرای آغازین: پنجرهٔ اصلی باید اول بالا بیاید، وگرنه کاربر
        // برنامه‌ای می‌بیند که چند ثانیه «مشغول» است بی‌آنکه کاری کرده باشد.
        private const int StartupDelayMs = 20000;

        // پس از بازگشتِ شبکه کمی صبر می‌شود: ویندوز رویداد را پیش از آماده
        // شدنِ واقعیِ مسیریابی می‌فرستد و تلاشِ فوری تقریباً همیشه شکست
        // می‌خورد.
        private const int ReconnectDelayMs = 8000;

        // ─── وضعیتِ زنده ─────────────────────────────────────────────────────
        private static readonly object Gate = new object();

        // ⚠ قفلِ سراسری: ۰ = بیکار، ۱ = در حال اجرا. با Interlocked گرفته
        // می‌شود تا حتی دو نخی که دقیقاً هم‌زمان می‌رسند نتوانند هر دو وارد
        // شوند.
        private static int _running;

        private static Timer _timer;
        private static Timer _delayed;
        private static CancellationTokenSource _cancellation;
        private static bool _started;
        private static bool _networkHooked;

        private static string _lastPhase = "";
        private static DateTime _lastRunAt = DateTime.MinValue;
        private static SyncRunResult _lastResult;

        // ─── رویدادها (روی نخِ پس‌زمینه) ────────────────────────────────────
        public static event EventHandler<SyncProgress> ProgressChanged;
        public static event EventHandler<SyncRunResult> Completed;

        public static bool IsRunning { get { return Volatile.Read(ref _running) == 1; } }

        public static bool IsStarted { get { lock (Gate) return _started; } }

        public static string LastPhase { get { lock (Gate) return _lastPhase; } }

        public static DateTime LastRunAt { get { lock (Gate) return _lastRunAt; } }

        public static SyncRunResult LastResult { get { lock (Gate) return _lastResult; } }

        // ═════════════════════════════════════════════════════════════════════
        // راه‌اندازی و توقف
        // ═════════════════════════════════════════════════════════════════════
        public static void Start()
        {
            lock (Gate)
            {
                if (_started) return;
                _started = true;

                _cancellation = new CancellationTokenSource();

                if (!_networkHooked)
                {
                    try
                    {
                        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged
                            += OnNetworkAvailabilityChanged;
                        _networkHooked = true;
                    }
                    catch (Exception ex) { Log(ex, "HookNetwork"); }
                }

                // زمان‌سنج همیشه ساخته می‌شود؛ اگر خودکار خاموش باشد، هر تیک
                // بی‌کار برمی‌گردد. این از ساخت/نابودیِ مکررِ زمان‌سنج هنگام
                // روشن و خاموش کردنِ تنظیم جلوگیری می‌کند.
                int period = IntervalMinutes * 60 * 1000;
                _timer = new Timer(OnTimer, null, period, period);

                _delayed = new Timer(OnStartupTick, null, StartupDelayMs, Timeout.Infinite);
            }
        }

        public static void Stop()
        {
            Timer timer, delayed;
            CancellationTokenSource cancellation;

            lock (Gate)
            {
                if (!_started) return;
                _started = false;

                timer = _timer; _timer = null;
                delayed = _delayed; _delayed = null;
                cancellation = _cancellation; _cancellation = null;
            }

            // ⚠ لغو پیش از dispose: اجرای در جریان باید بداند باید بایستد،
            // وگرنه تا پایانِ همان دسته ادامه می‌دهد و بستنِ برنامه کند
            // می‌شود. لغو داده را خراب نمی‌کند — صف دست‌نخورده می‌ماند.
            try { if (cancellation != null) cancellation.Cancel(); } catch { }

            try { if (timer != null) timer.Dispose(); } catch { }
            try { if (delayed != null) delayed.Dispose(); } catch { }

            try
            {
                if (_networkHooked)
                {
                    System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged
                        -= OnNetworkAvailabilityChanged;
                    _networkHooked = false;
                }
            }
            catch { }

            try { if (cancellation != null) cancellation.Dispose(); } catch { }
        }

        // لغوِ اجرای جاری بدون خاموش کردنِ زمان‌سنج.
        public static void CancelCurrent()
        {
            CancellationTokenSource source;
            lock (Gate) source = _cancellation;

            try { if (source != null) source.Cancel(); } catch { }
        }

        // ═════════════════════════════════════════════════════════════════════
        // تنظیمات
        // ═════════════════════════════════════════════════════════════════════
        public static bool AutoSyncEnabled
        {
            get
            {
                string value = SyncOutboxService.GetState(KeyAutoSyncEnabled, "1");
                return value != "0";
            }
            set { SyncOutboxService.SetState(KeyAutoSyncEnabled, value ? "1" : "0"); }
        }

        public static int IntervalMinutes
        {
            get
            {
                int minutes;
                if (!int.TryParse(SyncOutboxService.GetState(KeyIntervalMinutes, ""), out minutes))
                    return DefaultIntervalMinutes;

                return minutes < MinimumIntervalMinutes ? MinimumIntervalMinutes : minutes;
            }
            set
            {
                int minutes = value < MinimumIntervalMinutes ? MinimumIntervalMinutes : value;
                SyncOutboxService.SetState(KeyIntervalMinutes, minutes.ToString());

                lock (Gate)
                {
                    if (_timer == null) return;
                    int period = minutes * 60 * 1000;
                    try { _timer.Change(period, period); } catch { }
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // محرک‌ها
        // ═════════════════════════════════════════════════════════════════════
        private static void OnTimer(object state)
        {
            if (!AutoSyncEnabled) return;
            RequestSync("زمان‌سنج");
        }

        private static void OnStartupTick(object state)
        {
            if (!AutoSyncEnabled) return;
            RequestSync("راه‌اندازی برنامه");
        }

        private static void OnNetworkAvailabilityChanged(object sender,
            System.Net.NetworkInformation.NetworkAvailabilityEventArgs e)
        {
            if (!e.IsAvailable || !AutoSyncEnabled) return;

            lock (Gate)
            {
                if (!_started) return;

                try { if (_delayed != null) _delayed.Dispose(); } catch { }
                _delayed = new Timer(OnReconnectTick, null, ReconnectDelayMs, Timeout.Infinite);
            }
        }

        private static void OnReconnectTick(object state)
        {
            RequestSync("بازگشت اینترنت");
        }

        // ═════════════════════════════════════════════════════════════════════
        // اجرا
        //
        // مقدار بازگشتی: آیا این درخواست واقعاً یک اجرا شروع کرد؟ false یعنی
        // اجرایی از قبل در جریان بود (یا شرایط اولیه برقرار نبود).
        // ═════════════════════════════════════════════════════════════════════
        public static bool RequestSync(string reason)
        {
            // ⚠ قفلِ سراسری. با CompareExchange گرفته می‌شود: دقیقاً یک نخ
            // مقدار ۰ را می‌بیند و بقیه بی‌صدا برمی‌گردند.
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return false;

            bool queued = false;

            try
            {
                if (!Preconditions())
                {
                    Volatile.Write(ref _running, 0);
                    return false;
                }

                queued = ThreadPool.QueueUserWorkItem(delegate { RunCore(reason); });
            }
            catch (Exception ex)
            {
                Log(ex, "RequestSync");
            }
            finally
            {
                // اگر کار اصلاً به صف نرفت، قفل باید همین‌جا آزاد شود وگرنه
                // مدیر برای همیشه «مشغول» می‌ماند و دیگر هیچ همگام‌سازی‌ای
                // انجام نمی‌شود.
                if (!queued) Volatile.Write(ref _running, 0);
            }

            return queued;
        }

        // ═════════════════════════════════════════════════════════════════════
        // اجرای دستی و *همگام* — برای مرکز کنترل توسعه‌دهنده.
        //
        // آموزش — چرا این متد لازم است و چرا RequestSync جایش را نمی‌گیرد:
        // مرکز کنترل نوار پیشرفت و دکمهٔ لغوِ خودش را دارد و باید تا پایانِ
        // کار منتظر بماند. ولی اگر مستقیماً SyncService را صدا می‌زد، از
        // قفلِ سراسری عبور می‌کرد و می‌توانست *هم‌زمان* با اجرای پس‌زمینه
        // بالا برود — دقیقاً همان چیزی که ممنوع است. این متد همان قفل را
        // می‌گیرد و اگر گرفته باشد، صادقانه null برمی‌گرداند.
        //
        // ⚠ null یعنی «اجرایی از قبل در جریان است»، نه «شکست».
        // ═════════════════════════════════════════════════════════════════════
        public static SyncRunResult RunManual(IProgress<SyncProgress> progress,
                                              CancellationToken cancel)
        {
            SyncRunResult result = null;

            bool ran = TryRunExclusive(delegate
            {
                try
                {
                    SetState(KeyLastAttemptAt,
                             DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (دستی)");

                    var service = new SyncService(new HttpSyncTransport(), new HttpFileSyncTransport());

                    result = service.Run(progress, cancel);

                    SetState(KeyLastResult, result.Skipped ? result.Message : result.Summary);
                    RecordOutcome(result, "دستی");
                }
                catch (OperationCanceledException)
                {
                    result = new SyncRunResult { Cancelled = true, Message = "لغو شد" };
                }
                catch (Exception ex)
                {
                    Log(ex, "RunManual");
                    result = new SyncRunResult { Message = ex.Message };
                    RecordFailure(ex.Message, "دستی");
                }
                finally
                {
                    lock (Gate)
                    {
                        _lastRunAt = DateTime.Now;
                        _lastResult = result;
                    }
                }
            });

            return ran ? result : null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // اجرای دستیِ *یک فاز* — برای صفحهٔ جدید «همگام‌سازی ماژول‌ها».
        //
        // آموزش — چرا فاز جدا و نه معماری جدید: معماریِ همگام‌سازی (صفِ واحد،
        // ارسال/دریافت به‌عنوان دو فازِ کاملِ Upload/Download که از قبل در
        // SyncService وجود دارند) دست‌نخورده می‌ماند؛ این متد فقط همان دو فازِ
        // موجود را جداگانه قابلِ‌فراخوانی می‌کند (به‌جای همیشه هر دو با هم،
        // مثل RunManual) تا صفحهٔ جدید بتواند دکمهٔ «آپلود» و «دانلود» جدا
        // داشته باشد. همان قفلِ سراسری (TryRunExclusive) رعایت می‌شود تا هرگز
        // هم‌زمان با همگام‌سازیِ پس‌زمینه اجرا نشود.
        public static SyncRunResult RunManualPhase(SyncPhaseKind phase, IProgress<SyncProgress> progress, CancellationToken cancel)
        {
            SyncRunResult result = null;

            bool ran = TryRunExclusive(delegate
            {
                try
                {
                    SetState(KeyLastAttemptAt,
                             DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (دستی)");

                    var service = new SyncService(new HttpSyncTransport(), new HttpFileSyncTransport());
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    if (phase == SyncPhaseKind.Full)
                    {
                        result = service.Run(progress, cancel);
                    }
                    else
                    {
                        result = new SyncRunResult();
                        if (phase == SyncPhaseKind.Upload)
                            service.Upload(result, progress, cancel);
                        else
                            service.Download(result, progress, cancel);

                        result.Cancelled = cancel.IsCancellationRequested;
                        result.Completed = !result.Cancelled;
                        result.Duration = stopwatch.Elapsed;
                    }

                    SetState(KeyLastResult, result.Skipped ? result.Message : result.Summary);
                    RecordOutcome(result, "دستی");
                }
                catch (OperationCanceledException)
                {
                    result = new SyncRunResult { Cancelled = true, Message = "لغو شد" };
                }
                catch (Exception ex)
                {
                    Log(ex, "RunManualPhase");
                    result = new SyncRunResult { Message = ex.Message };
                    RecordFailure(ex.Message, "دستی");
                }
                finally
                {
                    lock (Gate)
                    {
                        _lastRunAt = DateTime.Now;
                        _lastResult = result;
                    }
                }
            });

            return ran ? result : null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // قفلِ سراسری — تنها دروازهٔ ورود به «کارِ همگام‌سازی».
        //
        // هر مسیری که می‌خواهد همگام‌سازی کند باید از اینجا رد شود. اگر کاری
        // در جریان باشد، false برمی‌گردد و هیچ کاری انجام نمی‌شود — نه صف
        // می‌شود و نه خطا می‌دهد، چون همگام‌سازی ازسرگیری‌پذیر است و اجرای
        // بعدی هرچه جامانده را برمی‌دارد.
        //
        // ⚠ قفل در finally آزاد می‌شود: یک استثنای پیش‌بینی‌نشده نباید
        // همگام‌سازی را تا اجرای بعدیِ برنامه از کار بیندازد.
        // ═════════════════════════════════════════════════════════════════════
        public static bool TryRunExclusive(Action work)
        {
            if (work == null) return false;

            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return false;

            try { work(); }
            finally { Volatile.Write(ref _running, 0); }

            return true;
        }

        // شرایطی که بدون آن‌ها همگام‌سازی بی‌معناست. عمداً *پیش* از هر تماسِ
        // شبکه‌ای بررسی می‌شوند.
        private static bool Preconditions()
        {
            try
            {
                // کاربری وارد نشده ⇒ نه مرکزی مشخص است و نه مجوزی؛ همگام‌سازی
                // در چنین حالتی یعنی نادیده گرفتنِ مرزِ دسترسی.
                if (!SecurityContext.IsLoggedIn) return false;

                if (!TableExists("SyncOutbox")) return false;

                // آدرس سرور تنظیم نشده ⇒ برنامه آفلاین است و هیچ تلاشی لازم
                // نیست. (SyncService خودش هم این را می‌فهمد، ولی بررسیِ محلی
                // از ساختنِ بی‌دلیلِ نخ و اتصال جلوگیری می‌کند.)
                string url = SyncOutboxService.GetState(HttpSyncTransport.KeyServerUrl, "");
                if (string.IsNullOrWhiteSpace(url)) return false;

                return true;
            }
            catch (Exception ex) { Log(ex, "Preconditions"); return false; }
        }

        private static void RunCore(string reason)
        {
            SyncRunResult result = null;

            try
            {
                CancellationToken token;
                lock (Gate)
                {
                    if (_cancellation == null) return;
                    token = _cancellation.Token;
                }

                SetState(KeyLastAttemptAt, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                                           " (" + reason + ")");

                var progress = new Progress<SyncProgress>(OnProgress);

                var transport = new HttpSyncTransport();
                var fileTransport = new HttpFileSyncTransport();

                var service = new SyncService(transport, fileTransport);

                result = service.Run(progress, token);

                SetState(KeyLastResult, result.Skipped ? result.Message : result.Summary);
                RecordOutcome(result, reason);
            }
            catch (OperationCanceledException)
            {
                result = new SyncRunResult { Cancelled = true, Message = "لغو شد" };
            }
            catch (Exception ex)
            {
                result = new SyncRunResult { Message = ex.Message };
                RecordFailure(ex.Message, reason);
                Log(ex, "RunCore");
            }
            finally
            {
                lock (Gate)
                {
                    _lastRunAt = DateTime.Now;
                    _lastResult = result;
                }

                // ⚠ قفل *همیشه* آزاد می‌شود، حتی وقتی همه‌چیز خراب شده. اگر
                // یک استثنای پیش‌بینی‌نشده قفل را نگه می‌داشت، همگام‌سازی تا
                // اجرای بعدیِ برنامه از کار می‌افتاد.
                Volatile.Write(ref _running, 0);

                Raise(Completed, result);
            }
        }

        private static void OnProgress(SyncProgress progress)
        {
            if (progress == null) return;

            lock (Gate) _lastPhase = progress.Phase ?? "";

            EventHandler<SyncProgress> handler = ProgressChanged;
            if (handler == null) return;

            try { handler(null, progress); }
            catch (Exception ex) { Log(ex, "ProgressChanged"); }
        }

        private static void Raise(EventHandler<SyncRunResult> handler, SyncRunResult result)
        {
            if (handler == null) return;

            // ⚠ خطای یک مشترک نباید مدیر را از کار بیندازد.
            try { handler(null, result); }
            catch (Exception ex) { Log(ex, "Completed"); }
        }

        // ═════════════════════════════════════════════════════════════════════
        // وضعیت — برای مرکز کنترل توسعه‌دهنده
        // ═════════════════════════════════════════════════════════════════════
        public sealed class SyncStatus
        {
            public bool   Started;
            public bool   Running;
            public bool   AutoEnabled;
            public int    IntervalMinutes;
            public string CurrentPhase = "";
            public string LastAttemptAt = "";
            public string LastResult = "";
            public string LastSuccessAt = "";
            public string LastFailureAt = "";
            public string LastFailure = "";
        }

        public static SyncStatus GetStatus()
        {
            var status = new SyncStatus();

            try
            {
                status.Started         = IsStarted;
                status.Running         = IsRunning;
                status.AutoEnabled     = AutoSyncEnabled;
                status.IntervalMinutes = IntervalMinutes;
                status.CurrentPhase    = LastPhase;
                status.LastAttemptAt   = SyncOutboxService.GetState(KeyLastAttemptAt, "");
                status.LastResult      = SyncOutboxService.GetState(KeyLastResult, "");
                status.LastSuccessAt   = SyncOutboxService.GetState(SyncOutboxService.KeyLastSyncAt, "");
                status.LastFailureAt   = SyncOutboxService.GetState(KeyLastFailureAt, "");
                status.LastFailure     = SyncOutboxService.GetState(KeyLastFailure, "");
            }
            catch (Exception ex) { Log(ex, "GetStatus"); }

            return status;
        }

        // ═════════════════════════════════════════════════════════════════════
        // گزارش تشخیصی — برای مرکز کنترل توسعه‌دهنده.
        //
        // یک تصویرِ کاملِ متنی از وضعیت همگام‌سازی که بتوان آن را کپی کرد و
        // برای پشتیبانی فرستاد. عمداً متنِ ساده است، نه ساختارِ داده: کسی که
        // این را می‌خواند معمولاً پشتِ تلفن است، نه پشتِ دیباگر.
        // ═════════════════════════════════════════════════════════════════════
        public static string BuildDiagnosticReport()
        {
            var report = new StringBuilder();

            try
            {
                SyncStatus status = GetStatus();

                report.AppendLine("═══ گزارش تشخیصی همگام‌سازی ═══");
                report.AppendLine("زمان گزارش: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                report.AppendLine("ماشین: " + SafeMachineName());
                report.AppendLine("شناسهٔ دستگاه: " + HttpSyncTransport.DeviceGuid);
                report.AppendLine();

                // ─── اتصال ───
                var transport = new HttpSyncTransport();
                report.AppendLine("── سرور ──");
                report.AppendLine("آدرس: " + (transport.IsConfigured
                    ? SyncOutboxService.GetState(HttpSyncTransport.KeyServerUrl, "")
                    : "پیکربندی نشده"));

                if (transport.IsConfigured)
                {
                    SyncConnectionStatus connection = transport.GetStatus(CancellationToken.None);
                    report.AppendLine("وضعیت: " + connection.DisplayText + " — " + connection.Detail);
                }
                report.AppendLine();

                // ─── سرویس پس‌زمینه ───
                report.AppendLine("── سرویس پس‌زمینه ──");
                report.AppendLine("اجرا شده: " + (status.Started ? "بله" : "خیر"));
                report.AppendLine("در حال اجرا: " + (status.Running ? "بله — " + status.CurrentPhase : "خیر"));
                report.AppendLine("خودکار: " + (status.AutoEnabled ? "روشن" : "خاموش")
                                  + " | بازه: هر " + status.IntervalMinutes + " دقیقه");
                report.AppendLine("آخرین تلاش: " + Or(status.LastAttemptAt, "تاکنون انجام نشده"));
                report.AppendLine("نتیجهٔ آخرین تلاش: " + Or(status.LastResult, "—"));
                report.AppendLine("آخرین موفق: " + Or(status.LastSuccessAt, "تاکنون هیچ‌وقت"));
                report.AppendLine("آخرین ناموفق: " + Or(status.LastFailureAt, "تاکنون هیچ‌وقت"));

                if (!string.IsNullOrWhiteSpace(status.LastFailure))
                    report.AppendLine("علت ناموفقی: " + status.LastFailure);

                report.AppendLine();

                // ─── صف رکوردها ───
                report.AppendLine("── صف رکوردها ──");
                report.AppendLine("در انتظار ارسال: " + SyncOutboxService.PendingCount().ToString("N0"));
                report.AppendLine("ناموفق: " + SyncOutboxService.FailedCount().ToString("N0"));
                report.AppendLine("تعارض باز: " + SyncConflictStore.OpenCount().ToString("N0"));
                report.AppendLine("نشانگر دریافت: "
                    + Or(SyncOutboxService.GetState(SyncService.KeyPullCursor, ""), "۰"));
                report.AppendLine();

                // ─── فایل‌ها ───
                SyncFileService.FileHealth files =
                    SyncFileService.GetHealth(new HttpFileSyncTransport());

                report.AppendLine("── فایل‌ها ──");
                report.AppendLine("ردیابی‌شده: " + files.Total.ToString("N0"));
                report.AppendLine("در انتظار ارسال: " + files.Pending.ToString("N0"));
                report.AppendLine("ارسال ناموفق: " + files.Failed.ToString("N0"));
                report.AppendLine("در انتظار دریافت: " + files.PendingDownloads.ToString("N0"));
                report.AppendLine("دریافت ناموفق: " + files.FailedDownloads.ToString("N0"));
                report.AppendLine("دریافت‌شده: " + files.Downloaded.ToString("N0"));
                report.AppendLine("روی دیسک نیست: " + files.Missing.ToString("N0"));
                report.AppendLine("رد شده: " + files.Rejected.ToString("N0"));
                report.AppendLine("تعارض پیوست: " + files.Conflicts.ToString("N0"));
                report.AppendLine("نشانگر فهرست سرور: "
                    + Or(SyncOutboxService.GetState(SyncFileService.KeyManifestCursor, ""), "۰"));
                report.AppendLine("آخرین ارسال: " + Or(files.LastSyncAt, "—"));
                report.AppendLine("آخرین دریافت: " + Or(files.LastDownloadAt, "—"));
                report.AppendLine("امتیاز سلامت: "
                    + (files.Score < 0 ? "قابل سنجش نیست" : files.Score + " / 100"));

                foreach (string note in files.Notes)
                    report.AppendLine("  • " + note);

                report.AppendLine();

                // ─── ذخیره‌سازی ───
                report.AppendLine("── ذخیره‌سازی ──");
                string root = FileHelper.GetBaseRootFolder();

                if (string.IsNullOrWhiteSpace(root))
                {
                    report.AppendLine("ریشه: تعیین نشده");
                }
                else
                {
                    report.AppendLine("ریشه: " + root);
                    report.AppendLine("موجود: " + (System.IO.Directory.Exists(root) ? "بله" : "خیر"));

                    try
                    {
                        var drive = new System.IO.DriveInfo(
                            System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(root)));

                        if (drive.IsReady)
                            report.AppendLine("فضای آزاد: "
                                + (drive.AvailableFreeSpace / 1024 / 1024).ToString("N0") + " مگابایت از "
                                + (drive.TotalSize / 1024 / 1024).ToString("N0") + " مگابایت");
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                report.AppendLine();
                report.AppendLine("⚠ گزارش ناقص ماند: " + ex.Message);
                Log(ex, "BuildDiagnosticReport");
            }

            return report.ToString();
        }

        private static string Or(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value;

        private static string SafeMachineName()
        {
            try { return Environment.MachineName; } catch { return "نامشخص"; }
        }

        // ─── ثبتِ نتیجه ──────────────────────────────────────────────────────
        //
        // ⚠ «موفق» فقط وقتی است که اجرا کامل شده *و* هیچ قلمی شکست نخورده
        // باشد. اجرایی که ده فایل را جا گذاشته، یک اجرای ناموفق است — نه
        // موفقی با چند تذکر.
        private static void RecordOutcome(SyncRunResult result, string reason)
        {
            if (result == null) return;

            // آفلاین بودن شکست نیست؛ لغو کاربر هم.
            if (result.Skipped || result.Cancelled) return;

            int failed = result.UploadFailed + result.FilesFailed + result.FilesDownloadFailed;

            if (result.Completed && failed == 0) return;

            var message = new StringBuilder();

            if (!result.Completed) message.Append("اجرا کامل نشد");
            else message.Append(failed).Append(" قلم ناموفق");

            if (!string.IsNullOrWhiteSpace(result.Message))
                message.Append(" — ").Append(result.Message);

            RecordFailure(message.ToString(), reason);
        }

        private static void RecordFailure(string message, string reason)
        {
            SetState(KeyLastFailureAt,
                     DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (" + reason + ")");

            SetState(KeyLastFailure, message ?? "");
        }

        // ─── کمکی‌ها ─────────────────────────────────────────────────────────
        private static void SetState(string key, string value)
        {
            try { SyncOutboxService.SetState(key, value); } catch { }
        }

        private static bool TableExists(string name)
        {
            try
            {
                object value = new DatabaseHelper().ExecuteScalar(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@n;",
                    new SQLiteParameter("@n", name));

                return value != null && value != DBNull.Value && Convert.ToInt32(value) > 0;
            }
            catch { return false; }
        }

        private static void Log(Exception ex, string source)
        {
            try { Enterprise.ErrorLogger.Log(ex, "BackgroundSyncManager." + source); } catch { }
        }
    }
}

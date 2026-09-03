using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // هماهنگ‌کنندهٔ همگام‌سازی — فاز ۳.
    //
    // آموزش — «ازسرگیری» اینجا یک قابلیتِ جداگانه نیست، نتیجهٔ طراحی است:
    // تمام وضعیتِ پیشرفت در خودِ پایگاه‌داده است (State هر ردیف SyncOutbox و
    // نشانگر دریافت در SyncState). پس قطعِ برق، بستن برنامه یا لغو کاربر هیچ
    // چیزی را «نیمه‌کاره در حافظه» رها نمی‌کند؛ اجرای بعدی دقیقاً از همان‌جا
    // ادامه می‌دهد. به همین دلیل هم هیچ متغیرِ وضعیتی در این کلاس نگهداری
    // نمی‌شود.
    //
    // ⚠ قاعدهٔ «هیچ عملیاتی گم نمی‌شود»: یک ردیف صف فقط و فقط وقتی «ارسال شد»
    // علامت می‌خورد که سرور صراحتاً پذیرشش را اعلام کرده باشد. هر حالت دیگر
    // (خطای شبکه، پاسخ نامفهوم، لغو، آفلاین) ردیف را در صف نگه می‌دارد.
    //
    // الگوی پیشرفت و لغو عیناً همان چیزی است که در «مرکز کنترل توسعه‌دهنده»
    // استفاده می‌شود: IProgress<SyncProgress> + CancellationToken، تا همان
    // نوار پیشرفت و دکمهٔ لغو بدون کد جدید قابل استفاده باشند.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class SyncService
    {
        // اندازهٔ دسته: بزرگ‌تر یعنی رفت‌وبرگشت کمتر، ولی در قطعیِ وسطِ کار
        // دوبارهٔ بیشتری لازم می‌شود. ۵۰ تعادلِ محافظه‌کارانه‌ای است.
        public const int UploadBatchSize   = 50;
        public const int DownloadPageSize  = 100;

        private readonly ISyncTransport _transport;
        private readonly IFileSyncTransport _fileTransport;

        public SyncService() : this(new OfflineSyncTransport(), new OfflineFileSyncTransport()) { }

        public SyncService(ISyncTransport transport)
            : this(transport, new OfflineFileSyncTransport()) { }

        // فاز ۷ — انتقال فایل به‌صورت *جدا* تزریق می‌شود: انتقال رکورد و
        // انتقال فایل ماهیت متفاوتی دارند (یکی JSON کوچک، دیگری جریانِ
        // چندمگابایتی) و ممکن است در استقرار واقعی دو مسیر متفاوت باشند.
        public SyncService(ISyncTransport transport, IFileSyncTransport fileTransport)
        {
            _transport = transport ?? new OfflineSyncTransport();
            _fileTransport = fileTransport ?? new OfflineFileSyncTransport();
        }

        public ISyncTransport Transport { get { return _transport; } }
        public IFileSyncTransport FileTransport { get { return _fileTransport; } }

        // ─────────────────────────────────────────────────────────────────────
        // اجرای کامل: ابتدا ارسال، سپس دریافت.
        //
        // ترتیب عمدی است — اگر اول دریافت می‌کردیم، تغییرِ محلیِ ارسال‌نشده به
        // «تعارض» تبدیل می‌شد در حالی که می‌توانست قبلش ارسال و پذیرفته شود.
        // ─────────────────────────────────────────────────────────────────────
        public SyncRunResult Run(IProgress<SyncProgress> progress, CancellationToken cancel)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new SyncRunResult();

            try
            {
                SyncConnectionStatus status = _transport.GetStatus(cancel);

                // ⚠ در حالت آفلاین صف *اصلاً لمس نمی‌شود*: نه شمارندهٔ تلاش
                // بالا می‌رود و نه خطایی ثبت می‌شود. نبودِ اینترنت شکستِ یک
                // عملیات نیست؛ اگر آن را شکست حساب می‌کردیم، پس از چند روز
                // کارِ آفلاین همهٔ ردیف‌ها صدها «تلاش ناموفق» داشتند و آمار
                // سلامت بی‌معنا می‌شد.
                if (!status.CanSynchronize)
                {
                    result.Skipped = true;
                    result.Message = status.Detail;
                    Report(progress, "همگام‌سازی", 0, 0, status.Detail);
                    return result;
                }

                Upload(result, progress, cancel);

                if (!cancel.IsCancellationRequested)
                    Download(result, progress, cancel);

                // ─── فاز ۷: فایل‌ها ───
                // پس از رکوردها انجام می‌شود تا رکوردِ صاحبِ فایل حتماً در
                // سمت مقابل وجود داشته باشد؛ فایلی که والدش نرسیده باشد
                // نمی‌تواند درست وصل شود.
                if (!cancel.IsCancellationRequested)
                    SynchronizeFiles(result, progress, cancel);

                result.Cancelled = cancel.IsCancellationRequested;
                result.Completed = !result.Cancelled;

                // زمان آخرین همگام‌سازیِ *موفق* ثبت می‌شود.
                if (result.Completed)
                    SyncOutboxService.SetState(SyncOutboxService.KeyLastSyncAt,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (OperationCanceledException)
            {
                result.Cancelled = true;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Log(ex, "Run");
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }

        // ═════════════════════════════════════════════════════════════════════
        // جریان ارسال
        // ═════════════════════════════════════════════════════════════════════
        public void Upload(SyncRunResult result, IProgress<SyncProgress> progress,
                           CancellationToken cancel)
        {
            int total = SyncOutboxService.PendingCount() + SyncOutboxService.FailedCount();
            if (total == 0)
            {
                Report(progress, "ارسال", 0, 0, "چیزی برای ارسال نیست");
                return;
            }

            int processed = 0;

            while (!cancel.IsCancellationRequested)
            {
                DataTable batch = SyncOutboxService.GetPending(UploadBatchSize);
                if (batch.Rows.Count == 0) break;

                List<SyncChange> changes = ToChanges(batch);
                if (changes.Count == 0) break;

                Report(progress, "ارسال", processed, total,
                       "ارسال " + changes.Count + " تغییر");

                SyncPushResult push;
                try
                {
                    push = _transport.Push(changes, cancel);
                }
                catch (OperationCanceledException) { throw; }
                catch (SyncOfflineException ex)
                {
                    // ⚠ قطعِ شبکه در میانهٔ ارسال «شکست» نیست: صف دقیقاً
                    // دست‌نخورده می‌ماند — نه شمارندهٔ تلاش بالا می‌رود و نه
                    // خطایی ثبت می‌شود. اگر آن را شکست حساب می‌کردیم، یک
                    // اینترنتِ ناپایدار در چند روز همهٔ ردیف‌ها را پر از
                    // «تلاش ناموفق» می‌کرد و آمار سلامت بی‌معنا می‌شد.
                    result.Message = ex.Message;
                    return;
                }
                catch (Exception ex)
                {
                    // خطای انتقال ⇒ همهٔ اقلامِ همین دسته ناموفق می‌مانند و در
                    // صف باقی می‌شوند. هیچ‌کدام «ارسال شده» علامت نمی‌خورد.
                    MarkBatchFailed(changes, ex.Message, result);
                    Log(ex, "Upload");
                    return;
                }

                if (push == null || !push.Succeeded)
                {
                    string message = push == null ? "پاسخی از سرور دریافت نشد" : push.ErrorMessage;
                    MarkBatchFailed(changes, message, result);
                    return;   // شکستِ سطحِ دسته ⇒ ادامهٔ ارسال بی‌فایده است
                }

                bool madeProgress = false;

                foreach (SyncChange change in changes)
                {
                    SyncItemOutcome outcome;
                    if (push.Items == null || !push.Items.TryGetValue(change.OutboxId, out outcome) ||
                        outcome == null)
                    {
                        // سرور دربارهٔ این قلم چیزی نگفت ⇒ سرنوشتش نامعلوم است
                        // و *به‌هیچ‌وجه* نباید «ارسال شد» فرض شود.
                        SyncOutboxService.MarkFailed(change.OutboxId,
                            "سرور وضعیت این قلم را اعلام نکرد");
                        result.UploadFailed++;
                        continue;
                    }

                    switch (outcome.State)
                    {
                        case SyncItemState.Accepted:
                            SyncOutboxService.MarkSent(change.OutboxId);

                            // ⚠ نقطهٔ توافق: سرور صراحتاً همین بار را پذیرفت،
                            // پس از این لحظه «آخرین حالتی که دو طرف رویش
                            // توافق دارند» دقیقاً همین است. مبنای ادغامِ
                            // تعارض‌های بعدی از همین‌جا خوانده می‌شود.
                            SyncBaselineStore.Set(change.EntityName, change.GlobalId,
                                change.Payload, change.RowVersion,
                                OfflineSyncInitializer.BaselineFromPush);

                            result.Uploaded++;
                            madeProgress = true;
                            break;

                        case SyncItemState.Conflict:
                            // ثبت می‌شود و از صف بیرون می‌آید تا جریان قفل
                            // نشود؛ حل آن کار فاز ۴ است.
                            SyncConflictStore.Record(change.EntityName, change.GlobalId,
                                OfflineSyncInitializer.ConflictConcurrentEdit,
                                change.OutboxId, change.RowVersion,
                                outcome.RemoteVersion, change.Payload, outcome.RemotePayload);

                            SyncOutboxService.MarkConflict(change.OutboxId, outcome.Message);
                            result.Conflicts++;
                            madeProgress = true;
                            break;

                        default:
                            SyncOutboxService.MarkFailed(change.OutboxId, outcome.Message);
                            result.UploadFailed++;
                            break;
                    }

                    processed++;
                }

                Report(progress, "ارسال", processed, total, "ارسال تغییرات");

                // اگر هیچ قلمی از این دسته پیش نرفت، تکرارِ همان دسته یعنی
                // حلقهٔ بی‌پایان. (اقلامِ ناموفق در صف می‌مانند و در اجرای
                // بعدی دوباره تلاش می‌شوند.)
                if (!madeProgress) break;
            }
        }

        private void MarkBatchFailed(List<SyncChange> changes, string message, SyncRunResult result)
        {
            foreach (SyncChange change in changes)
            {
                SyncOutboxService.MarkFailed(change.OutboxId, message);
                result.UploadFailed++;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // جریان دریافت
        // ═════════════════════════════════════════════════════════════════════
        public void Download(SyncRunResult result, IProgress<SyncProgress> progress,
                             CancellationToken cancel)
        {
            string cursor = SyncOutboxService.GetState(KeyPullCursor, "");
            int page = 0;

            while (!cancel.IsCancellationRequested)
            {
                SyncPullResult pull;
                try
                {
                    pull = _transport.Pull(cursor, DownloadPageSize, cancel);
                }
                catch (OperationCanceledException) { throw; }
                catch (SyncOfflineException)
                {
                    // قطعِ شبکه هنگام دریافت: نشانگر ذخیره‌شده دست‌نخورده
                    // می‌ماند و اجرای بعدی از همان‌جا ادامه می‌دهد.
                    return;
                }
                catch (Exception ex)
                {
                    Log(ex, "Download");
                    return;
                }

                if (pull == null || !pull.Succeeded) return;
                if (pull.Changes == null || pull.Changes.Count == 0)
                {
                    SaveCursor(pull.NextCursor, cursor);
                    return;
                }

                page++;
                int index = 0;

                foreach (SyncChange change in pull.Changes)
                {
                    if (cancel.IsCancellationRequested) break;

                    index++;
                    Report(progress, "دریافت", index, pull.Changes.Count,
                           "اعمال تغییرات (صفحهٔ " + page + ")");

                    try
                    {
                        SyncApplier.ApplyOutcome outcome = SyncApplier.Apply(change);

                        switch (outcome)
                        {
                            case SyncApplier.ApplyOutcome.Inserted:
                            case SyncApplier.ApplyOutcome.Updated:
                            case SyncApplier.ApplyOutcome.Deleted:
                                result.Downloaded++;
                                break;

                            case SyncApplier.ApplyOutcome.SkippedConflict:
                                result.Conflicts++;
                                break;

                            default:
                                result.DownloadSkipped++;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // یک تغییرِ خراب نباید کل دریافت را متوقف کند.
                        result.DownloadSkipped++;
                        Log(ex, "Apply/" + change.EntityName);
                    }
                }

                // ⚠ نشانگر فقط پس از اعمالِ *کاملِ* یک صفحه ذخیره می‌شود.
                // اگر وسط صفحه لغو یا قطع شود، همان صفحه دوباره دریافت
                // می‌گردد — و چون اعمال بر اساس GlobalID و idempotent است،
                // رکورد تکراری ساخته نمی‌شود.
                if (cancel.IsCancellationRequested) return;

                string previousCursor = cursor;
                cursor = SaveCursor(pull.NextCursor, cursor);

                if (!pull.HasMore) return;

                // ⚠ محافظ در برابر «حلقهٔ بی‌پایانِ دریافت».
                //
                // آموزش — باگ واقعی که این بازبینی پیدا کرد: اگر سرور بگوید
                // «صفحهٔ بعدی هم هست» (HasMore) ولی نشانگرِ تازه‌ای ندهد
                // (NextCursor خالی، یا دقیقاً همان نشانگرِ قبلی)، این حلقه
                // تا ابد همان یک صفحه را می‌گیرد و دوباره اعمال می‌کند.
                // چون کلِ اجرا زیر قفلِ سراسریِ BackgroundSyncManager است،
                // نتیجه‌اش این بود که همگام‌سازی برای همیشه «مشغول» می‌ماند
                // و تا اجرای بعدیِ برنامه هیچ همگام‌سازیِ دیگری — نه خودکار
                // و نه دستی — نمی‌توانست شروع شود.
                //
                // چرا «توقف» و نه «تلاش دوباره»: نشانگرِ پیش‌نرونده یعنی
                // ایرادِ سمتِ سرور. اجرای بعدی از همان نشانگرِ ذخیره‌شده
                // ادامه می‌دهد، پس هیچ تغییری گم نمی‌شود — فقط این اجرا
                // زودتر و سالم تمام می‌شود.
                if (cursor == previousCursor)
                {
                    Log(new InvalidOperationException(
                            "سرور «صفحهٔ بعدی» اعلام کرد ولی نشانگر را جلو نبرد (نشانگر: " +
                            (previousCursor ?? "") + "). دریافت در همین‌جا متوقف شد."),
                        "Download/StalledCursor");
                    return;
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // فایل‌ها (فاز ۷)
        // ═════════════════════════════════════════════════════════════════════
        public void SynchronizeFiles(SyncRunResult result, IProgress<SyncProgress> progress,
                                     CancellationToken cancel)
        {
            try
            {
                // ابتدا کشف: فایل‌های تازه‌ای که آفلاین اضافه شده‌اند باید
                // شناسایی و هش شوند، حتی اگر بعداً ارسال ممکن نباشد.
                SyncFileService.ScanLocalFiles(progress, cancel);

                if (cancel.IsCancellationRequested) return;

                SyncFileService.FileSyncResult files =
                    SyncFileService.UploadPending(_fileTransport, progress, cancel);

                result.FilesUploaded  = files.Uploaded;
                result.FilesFailed    = files.Failed;
                result.FilesSkipped   = files.Skipped;
                result.Conflicts     += files.Conflicts;

                if (cancel.IsCancellationRequested) return;

                // ─── فاز B: دریافت ───
                // ⚠ پس از ارسال انجام می‌شود. اگر برعکس بود، فایلِ محلیِ
                // ارسال‌نشده «تغییرِ ارسال‌نشده» تشخیص داده می‌شد و بی‌دلیل
                // تعارض ثبت می‌گردید، در حالی که می‌توانست قبلش ارسال شود.
                //
                // انتقالی که فهرست‌گیری بلد نیست (مثلاً حالت آفلاین یا بدلِ
                // آزمون) به‌سادگی نادیده گرفته می‌شود — هیچ رفتار موجودی
                // عوض نمی‌شود.
                var manifestTransport = _fileTransport as IFileManifestTransport;
                if (manifestTransport == null || !manifestTransport.IsConfigured) return;

                SyncFileService.FileDownloadResult downloads =
                    SyncFileService.DownloadPending(manifestTransport, progress, cancel);

                result.FilesDownloaded      = downloads.Downloaded;
                result.FilesDownloadFailed  = downloads.Failed + downloads.Corrupted + downloads.Denied;
                result.FilesDownloadSkipped = downloads.Skipped;
                result.Conflicts           += downloads.Conflicts;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Log(ex, "SynchronizeFiles"); }
        }

        public const string KeyPullCursor = "PullCursor";

        private static string SaveCursor(string nextCursor, string current)
        {
            if (string.IsNullOrEmpty(nextCursor) || nextCursor == current) return current;

            SyncOutboxService.SetState(KeyPullCursor, nextCursor);
            return nextCursor;
        }

        // ═════════════════════════════════════════════════════════════════════
        // تبدیل ردیف صف به تغییرِ قابل ارسال
        // ═════════════════════════════════════════════════════════════════════
        private static List<SyncChange> ToChanges(DataTable batch)
        {
            var changes = new List<SyncChange>();

            foreach (DataRow row in batch.Rows)
            {
                changes.Add(new SyncChange
                {
                    OutboxId       = GetLong(row, "OutboxID"),
                    EntityName     = GetString(row, "EntityName"),
                    GlobalId       = GetString(row, "EntityGlobalID"),
                    ParentGlobalId = GetString(row, "ParentGlobalID"),
                    OperationType  = GetString(row, "OperationType"),
                    RowVersion     = GetInt(row, "RowVersion"),
                    Payload        = GetString(row, "Payload"),
                    CenterId       = GetInt(row, "CenterID"),
                    Username       = GetString(row, "Username"),
                    MachineName    = GetString(row, "MachineName"),
                    OccurredAt     = GetString(row, "CreatedAt")
                });
            }

            return changes;
        }

        // ═════════════════════════════════════════════════════════════════════
        // وضعیت — برای مرکز کنترل توسعه‌دهنده
        // ═════════════════════════════════════════════════════════════════════
        public sealed class SyncHealth
        {
            public bool   Configured;
            public string ConnectionText = "";
            public string LastSyncAt = "";
            public int    Pending;
            public int    Failed;
            public int    Conflicts;
            public int    Score;              // ۰..۱۰۰
            public List<string> Notes = new List<string>();
        }

        public static SyncHealth GetHealth(ISyncTransport transport)
        {
            var health = new SyncHealth();

            try
            {
                ISyncTransport active = transport ?? new OfflineSyncTransport();

                health.Configured = active.IsConfigured;
                health.ConnectionText = active.GetStatus(CancellationToken.None).DisplayText;

                health.LastSyncAt = SyncOutboxService.GetState(SyncOutboxService.KeyLastSyncAt, "");
                health.Pending    = SyncOutboxService.PendingCount();
                health.Failed     = SyncOutboxService.FailedCount();
                health.Conflicts  = SyncConflictStore.OpenCount();

                // ⚠ امتیاز فقط وقتی معنا دارد که سروری تعریف شده باشد. تا آن
                // زمان «انباشتِ صف» یک ایراد نیست — رفتار طبیعیِ کار آفلاین
                // است. جریمه کردنش یعنی گزارشِ سلامتِ گمراه‌کننده.
                if (!health.Configured)
                {
                    health.Score = -1;
                    health.Notes.Add("سروری پیکربندی نشده — صف به‌درستی محلی نگهداری می‌شود");
                    return health;
                }

                int score = 100;

                if (health.Failed > 0)
                {
                    score -= Math.Min(40, health.Failed * 2);
                    health.Notes.Add(health.Failed + " عملیات ناموفق");
                }

                if (health.Conflicts > 0)
                {
                    score -= Math.Min(30, health.Conflicts * 5);
                    health.Notes.Add(health.Conflicts + " تعارض بازبینی‌نشده");
                }

                if (health.Pending > 500)
                {
                    score -= 15;
                    health.Notes.Add("صف بسیار بزرگ است (" + health.Pending + ")");
                }

                if (string.IsNullOrWhiteSpace(health.LastSyncAt))
                {
                    score -= 20;
                    health.Notes.Add("تاکنون همگام‌سازی موفقی ثبت نشده");
                }

                health.Score = Math.Max(0, Math.Min(100, score));
            }
            catch (Exception ex)
            {
                health.Score = -1;
                Log(ex, "GetHealth");
            }

            return health;
        }

        // ─── کمکی‌ها ─────────────────────────────────────────────────────────
        private static void Report(IProgress<SyncProgress> progress, string phase,
                                   int current, int total, string detail)
        {
            if (progress == null) return;

            progress.Report(new SyncProgress
            {
                Phase = string.IsNullOrEmpty(detail) ? phase : phase + " — " + detail,
                Current = current,
                Total = total
            });
        }

        private static string GetString(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            return Convert.ToString(row[column]);
        }

        private static int GetInt(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return 0;
            try { return Convert.ToInt32(row[column]); } catch { return 0; }
        }

        private static long GetLong(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return 0;
            try { return Convert.ToInt64(row[column]); } catch { return 0; }
        }

        private static void Log(Exception ex, string source)
        {
            try { Enterprise.ErrorLogger.Log(ex, "SyncService." + source); } catch { }
        }
    }
}

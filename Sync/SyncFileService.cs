using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // همگام‌سازی پیوست‌ها — فاز ۷.
    //
    // سه کارِ اصلی:
    //   ۱. کشف و ثبت فایل‌های محلی (از همان ستون‌های مسیرِ موجود)
    //   ۲. صف‌بندی و ارسال دسته‌ای با تلاش دوباره و ازسرگیری
    //   ۳. تشخیص تعارضِ فایل و آماده‌سازی مسیر دریافت
    //
    // ⚠ قاعدهٔ حذف: این سرویس *هرگز* فایل محلی را پاک نمی‌کند — نه بعد از
    // ارسال موفق، نه هنگام تعارض. فایل‌های مددجو تنها نسخهٔ موجودِ سند
    // هستند و تا وقتی سروری واقعی و آزموده وجود ندارد، هر حذفِ خودکار یعنی
    // ریسکِ از دست رفتنِ دائمی. پاکسازی، اگر لازم شد، باید تصمیمِ صریحِ کاربر
    // باشد از مسیرِ موجودِ «پیوست‌های بدون استفاده».
    //
    // ⚠ کارایی: هش با جریانِ بافردار محاسبه می‌شود و فایل هرگز کامل در حافظه
    // بارگذاری نمی‌شود — سقفِ سند در این برنامه ۵۰ مگابایت است و چند فایل
    // هم‌زمان می‌توانست حافظه را ببلعد.
    // ═════════════════════════════════════════════════════════════════════════
    public static class SyncFileService
    {
        private static readonly DatabaseHelper Db = new DatabaseHelper();

        // بافر ۸۰ کیلوبایتی: زیر آستانهٔ Large Object Heap می‌ماند.
        private const int HashBufferSize = 81920;

        public const int UploadBatchSize = 10;

        // منابع فایل — دقیقاً همان ستون‌هایی که از قبل در برنامه وجود دارند.
        private static readonly string[][] FileSources =
        {
            new[] { "TblCase",   "CasID", "PhotoPath",       "عکس سرپرست" },
            new[] { "TblCase",   "CasID", "FamilyPhotoPath", "عکس خانواده" },
            new[] { "TblFamily", "FamID", "MemberPhotoPath", "عکس عضو" },
            new[] { "TblDocs",   "DocID", "DocFilePath",     "سند" }
        };

        // ⚠ امنیت: فقط این پسوندها پذیرفته می‌شوند. یک ردیفِ دستکاری‌شده در
        // دیتابیس نباید بتواند فایل اجرایی را وارد چرخهٔ همگام‌سازی کند.
        private static readonly HashSet<string> AllowedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff",
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".rtf"
            };

        // ═════════════════════════════════════════════════════════════════════
        // ۱) کشف و ثبت
        // ═════════════════════════════════════════════════════════════════════
        public sealed class ScanResult
        {
            public int Registered;   // ردیف تازه
            public int Updated;      // محتوا عوض شده (هش جدید)
            public int Unchanged;
            public int Missing;      // مسیر در دیتابیس هست ولی فایل نیست
            public int Rejected;     // نوع/اندازه/مسیر نامعتبر
            public bool Cancelled;
        }

        public static ScanResult ScanLocalFiles(IProgress<SyncProgress> progress, CancellationToken cancel)
        {
            var result = new ScanResult();

            if (!SecurityContext.IsLoggedIn || !TableExists("SyncFile")) return result;

            foreach (string[] source in FileSources)
            {
                if (cancel.IsCancellationRequested) { result.Cancelled = true; return result; }

                string table = source[0], key = source[1], column = source[2], label = source[3];
                if (!ColumnExists(table, column) || !ColumnExists(table, "GlobalID")) continue;

                DataTable rows;
                try
                {
                    rows = Db.Query(
                        "SELECT " + key + " AS LocalID, GlobalID, [" + column + "] AS FilePath " +
                        "FROM [" + table + "] WHERE NULLIF([" + column + "],'') IS NOT NULL;");
                }
                catch { continue; }

                int index = 0;
                foreach (DataRow row in rows.Rows)
                {
                    if (cancel.IsCancellationRequested) { result.Cancelled = true; return result; }

                    index++;
                    if (index % 20 == 0 || index == rows.Rows.Count)
                        Report(progress, "بررسی فایل‌ها — " + label, index, rows.Rows.Count);

                    string globalId = Convert.ToString(row["GlobalID"]);
                    string path = Convert.ToString(row["FilePath"]);

                    if (string.IsNullOrWhiteSpace(globalId)) continue;

                    Register(table, globalId, column, path, result);
                }
            }

            return result;
        }

        private static void Register(string entityName, string entityGlobalId,
                                     string columnName, string path, ScanResult result)
        {
            try
            {
                DataRow existing = FindRow(entityName, entityGlobalId, columnName);

                // ── اعتبارسنجی امنیتی پیش از هر کاری ──
                string rejection = Validate(path);
                if (rejection != null)
                {
                    Upsert(entityName, entityGlobalId, columnName, path, null, 0,
                           OfflineSyncInitializer.FileRejected, rejection, existing);
                    result.Rejected++;
                    return;
                }

                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    // فایل گم شده — ثبت می‌شود تا در تشخیص دیده شود، ولی
                    // هرگز به‌عنوان «آمادهٔ ارسال» علامت نمی‌خورد.
                    Upsert(entityName, entityGlobalId, columnName, path, null, 0,
                           OfflineSyncInitializer.FileMissing, "فایل روی دیسک پیدا نشد", existing);
                    result.Missing++;
                    return;
                }

                // اگر اندازه و مسیر عوض نشده‌اند، هشِ دوباره لازم نیست —
                // روی هزاران عکس این تفاوتِ چند دقیقه‌ای است.
                if (existing != null &&
                    Convert.ToInt64(existing["SizeBytes"]) == info.Length &&
                    string.Equals(Convert.ToString(existing["LocalPath"]), info.FullName,
                                  StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(Convert.ToString(existing["ContentHash"])))
                {
                    result.Unchanged++;
                    return;
                }

                string hash = ComputeHash(info.FullName);
                if (hash == null)
                {
                    Upsert(entityName, entityGlobalId, columnName, path, null, 0,
                           OfflineSyncInitializer.FileFailed, "خواندن فایل ممکن نشد", existing);
                    result.Rejected++;
                    return;
                }

                bool isNew = existing == null;
                bool changed = !isNew && !string.Equals(
                    Convert.ToString(existing["ContentHash"]), hash, StringComparison.OrdinalIgnoreCase);

                Upsert(entityName, entityGlobalId, columnName, info.FullName, hash, info.Length,
                       OfflineSyncInitializer.FilePending, null, existing);

                if (isNew) result.Registered++;
                else if (changed) result.Updated++;
                else result.Unchanged++;
            }
            catch (Exception ex) { Swallow(ex, "Register"); }
        }

        // ⚠ امنیت — سه بررسی پیش از ورود هر فایل به چرخهٔ همگام‌سازی.
        private static string Validate(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "مسیر خالی";

            string full;
            try { full = Path.GetFullPath(path); }
            catch { return "مسیر نامعتبر"; }

            // ۱) پسوند مجاز
            string extension = Path.GetExtension(full);
            if (!AllowedExtensions.Contains(extension ?? ""))
                return "نوع فایل مجاز نیست (" + extension + ")";

            // ۲) داخل ریشهٔ ذخیره‌سازیِ برنامه — جلوگیری از خروج از مسیر و از
            // کشیده شدنِ فایل‌های سیستمی به داخل همگام‌سازی.
            string root = null;
            try { root = FileHelper.GetBaseRootFolder(); } catch { }

            if (!string.IsNullOrWhiteSpace(root))
            {
                string fullRoot = Path.GetFullPath(root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                    return "فایل خارج از پوشهٔ ذخیره‌سازی برنامه است";
            }

            // ۳) سقف اندازه — همان سقف‌هایی که فرم‌ها از قبل اعمال می‌کنند.
            try
            {
                var info = new FileInfo(full);
                if (info.Exists)
                {
                    bool isDocument = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
                                   || extension == ".doc"  || extension == ".docx"
                                   || extension == ".xls"  || extension == ".xlsx"
                                   || extension == ".txt"  || extension == ".rtf";

                    long limit = isDocument ? FileHelper.MaxDocumentFileSizeBytes
                                            : FileHelper.MaxPhotoFileSizeBytes;

                    if (info.Length > limit)
                        return "اندازهٔ فایل از سقف مجاز بیشتر است";
                }
            }
            catch { }

            return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // هش — جریانی، بدون بارگذاری کامل در حافظه
        // ═════════════════════════════════════════════════════════════════════
        public static string ComputeHash(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                   FileShare.ReadWrite, HashBufferSize, FileOptions.SequentialScan))
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);

                    var sb = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return null; }
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۲) ارسال
        // ═════════════════════════════════════════════════════════════════════
        public sealed class FileSyncResult
        {
            public int Uploaded;
            public int Failed;
            public int Skipped;
            public int Conflicts;
            public bool Cancelled;
            public bool Skipped_NoTransport;
            public string Message = "";
        }

        public static FileSyncResult UploadPending(IFileSyncTransport transport,
            IProgress<SyncProgress> progress, CancellationToken cancel)
        {
            var result = new FileSyncResult();

            if (transport == null || !transport.IsConfigured)
            {
                result.Skipped_NoTransport = true;
                result.Message = OfflineSyncTransport.OfflineMessage;
                return result;
            }

            if (!TableExists("SyncFile")) return result;

            // ⚠ باید *دقیقاً* همان مجموعه‌ای شمرده شود که GetPendingUploads
            // برمی‌گرداند (در انتظار + ناموفق). اگر فقط «در انتظار» شمرده شود،
            // فایلی که یک بار شکست خورده هرگز دوباره تلاش نمی‌شود — یعنی
            // قابلیت «تلاش دوباره» بی‌صدا از کار می‌افتد.
            int total = PendingUploadCount() + FailedUploadCount();
            if (total == 0) return result;

            int processed = 0;

            // ⚠ فایلِ ناموفق عمداً در صف می‌ماند (تا دفعهٔ بعد دوباره تلاش
            // شود)، پس کوئریِ «در انتظار» دوباره همان را برمی‌گرداند. بدون
            // ردیابیِ شناسه‌های دیده‌شده، یک فایلِ همیشه‌ناموفق حلقهٔ بی‌پایان
            // می‌ساخت و همگام‌سازی هرگز تمام نمی‌شد. هر شناسه در *این اجرا*
            // فقط یک بار تلاش می‌شود.
            var attempted = new HashSet<long>();

            while (!cancel.IsCancellationRequested)
            {
                DataTable batch = GetPendingUploads(UploadBatchSize);
                if (batch.Rows.Count == 0) break;

                bool madeProgress = false;

                foreach (DataRow row in batch.Rows)
                {
                    if (cancel.IsCancellationRequested) break;

                    long fileId = Convert.ToInt64(row["FileID"]);
                    if (!attempted.Add(fileId)) continue;   // در همین اجرا تلاش شده

                    string localPath = Convert.ToString(row["LocalPath"]);

                    processed++;
                    Report(progress, "ارسال فایل — " + Convert.ToString(row["FileName"]),
                           processed, total);

                    // فایل بین ثبت و ارسال حذف شده است.
                    if (!File.Exists(localPath))
                    {
                        MarkState(fileId, OfflineSyncInitializer.FileMissing,
                                  "فایل هنگام ارسال روی دیسک نبود", false);
                        result.Skipped++;
                        madeProgress = true;
                        continue;
                    }

                    // ⚠ هش دوباره بررسی می‌شود: اگر فایل از زمان ثبت عوض شده
                    // باشد، ارسالِ هشِ قدیمی یعنی سرور محتوایی را با اثرانگشتِ
                    // غلط ثبت می‌کند و بعداً «خراب» تشخیص داده می‌شود.
                    string currentHash = ComputeHash(localPath);
                    string storedHash = Convert.ToString(row["ContentHash"]);

                    if (currentHash == null)
                    {
                        MarkState(fileId, OfflineSyncInitializer.FileFailed, "خواندن فایل ممکن نشد", true);
                        result.Failed++;
                        madeProgress = true;
                        continue;
                    }

                    if (!string.Equals(currentHash, storedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateHash(fileId, currentHash, new FileInfo(localPath).Length);
                        storedHash = currentHash;
                    }

                    var item = new FileSyncItem
                    {
                        EntityName = Convert.ToString(row["EntityName"]),
                        EntityGlobalId = Convert.ToString(row["EntityGlobalID"]),
                        FileName = Convert.ToString(row["FileName"]),
                        LocalPath = localPath,
                        ContentHash = storedHash,
                        SizeBytes = Convert.ToInt64(row["SizeBytes"]),
                        FileVersion = Convert.ToInt32(row["FileVersion"])
                    };

                    FileTransferResult transfer;
                    try { transfer = transport.UploadFile(item, cancel); }
                    catch (OperationCanceledException) { throw; }
                    catch (SyncOfflineException ex)
                    {
                        // ⚠ سرور در دسترس نیست ⇒ صفِ فایل *دست‌نخورده* می‌ماند:
                        // نه شمارندهٔ تلاش بالا می‌رود و نه چیزی «ناموفق»
                        // علامت می‌خورد. همان قاعده‌ای که در همگام‌سازی رکوردها
                        // اعمال می‌شود؛ کار آفلاین شکست نیست.
                        result.Skipped_NoTransport = true;
                        result.Message = ex.Message;
                        result.Cancelled = cancel.IsCancellationRequested;
                        return result;
                    }
                    catch (Exception ex)
                    {
                        MarkState(fileId, OfflineSyncInitializer.FileFailed, ex.Message, true);
                        result.Failed++;
                        madeProgress = true;
                        continue;
                    }

                    if (transfer != null && transfer.Succeeded)
                    {
                        // اگر سرور هشِ متفاوتی برگرداند، فایل در مسیر خراب شده
                        // است — «موفق» علامت نمی‌خورد.
                        if (!string.IsNullOrEmpty(transfer.ContentHash) &&
                            !string.Equals(transfer.ContentHash, storedHash, StringComparison.OrdinalIgnoreCase))
                        {
                            MarkState(fileId, OfflineSyncInitializer.FileFailed,
                                      "هشِ سرور با هشِ محلی نمی‌خواند (احتمال خرابی در انتقال)", true);
                            result.Failed++;
                        }
                        else
                        {
                            MarkUploaded(fileId);
                            result.Uploaded++;
                        }
                    }
                    else
                    {
                        MarkState(fileId, OfflineSyncInitializer.FileFailed,
                                  transfer == null ? "پاسخی دریافت نشد" : transfer.ErrorMessage, true);
                        result.Failed++;
                    }

                    madeProgress = true;
                }

                // هیچ ردیفِ تازه‌ای در این دور تلاش نشد ⇒ همه را دیده‌ایم.
                if (!madeProgress) break;
            }

            result.Cancelled = cancel.IsCancellationRequested;
            return result;
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۳) دریافت — آماده‌سازی
        // ═════════════════════════════════════════════════════════════════════
        //
        // فایل دریافتی فقط وقتی پذیرفته می‌شود که هشِ محتوای واقعیِ روی دیسک
        // با هشِ اعلام‌شده بخواند. این تنها راهِ تشخیصِ فایلِ خرابِ نیمه‌دانلود
        // است — اندازه به‌تنهایی کافی نیست.
        public sealed class DownloadOutcome
        {
            public bool Applied;
            public bool Conflict;
            public bool Corrupted;
            public string Message = "";
        }

        public static DownloadOutcome ApplyDownloadedFile(string entityName, string entityGlobalId,
            string columnName, string downloadedPath, string expectedHash, int remoteVersion)
        {
            var outcome = new DownloadOutcome();

            try
            {
                if (!File.Exists(downloadedPath))
                {
                    outcome.Message = "فایل دریافتی پیدا نشد.";
                    return outcome;
                }

                string actualHash = ComputeHash(downloadedPath);
                if (actualHash == null ||
                    (!string.IsNullOrEmpty(expectedHash) &&
                     !string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase)))
                {
                    outcome.Corrupted = true;
                    outcome.Message = "هشِ فایل دریافتی نمی‌خواند — فایل خراب یا ناقص است.";
                    return outcome;
                }

                DataRow existing = FindRow(entityName, entityGlobalId, columnName);

                // ── تعارض: هر دو طرف فایل دارند و محتوایشان فرق می‌کند ──
                if (existing != null)
                {
                    string localHash = Convert.ToString(existing["ContentHash"]);
                    string localPath = Convert.ToString(existing["LocalPath"]);
                    bool localExists = !string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath);

                    if (localExists && !string.IsNullOrEmpty(localHash) &&
                        !string.Equals(localHash, actualHash, StringComparison.OrdinalIgnoreCase))
                    {
                        RecordAttachmentConflict(entityName, entityGlobalId, columnName,
                                                 existing, actualHash, remoteVersion, downloadedPath,
                                                 "فایل در دو محل تغییر کرده است");

                        MarkFileState(existing, OfflineSyncInitializer.FileConflict);

                        outcome.Conflict = true;
                        outcome.Message = "تعارض پیوست ثبت شد — فایل محلی دست‌نخورده ماند.";
                        return outcome;
                    }

                    // ── حذف‌شده به‌صورت محلی ولی تغییرکرده در سرور ──
                    if (!localExists && !string.IsNullOrEmpty(localHash) &&
                        !string.Equals(localHash, actualHash, StringComparison.OrdinalIgnoreCase))
                    {
                        RecordAttachmentConflict(entityName, entityGlobalId, columnName,
                                                 existing, actualHash, remoteVersion, downloadedPath,
                                                 "فایل محلی حذف شده ولی در سرور تغییر کرده است");

                        outcome.Conflict = true;
                        outcome.Message = "تعارض حذف/تغییر پیوست ثبت شد.";
                        return outcome;
                    }
                }

                // ── همان محتوا از قبل هست ⇒ کاری لازم نیست (جلوگیری از تکرار) ──
                if (existing != null &&
                    string.Equals(Convert.ToString(existing["ContentHash"]), actualHash,
                                  StringComparison.OrdinalIgnoreCase))
                {
                    outcome.Applied = true;
                    outcome.Message = "همان فایل از قبل موجود است.";
                    return outcome;
                }

                Upsert(entityName, entityGlobalId, columnName, downloadedPath, actualHash,
                       new FileInfo(downloadedPath).Length,
                       OfflineSyncInitializer.FileDownloaded, null, existing);

                // ⚠ اتصال به رکوردِ درست: مسیر روی همان ستونی نوشته می‌شود که
                // مالکش با هویت سراسری پیدا شده — نه با حدسِ نام فایل.
                LinkToRecord(entityName, entityGlobalId, columnName, downloadedPath);

                outcome.Applied = true;
                outcome.Message = "فایل ثبت و به رکورد وصل شد.";
            }
            catch (Exception ex)
            {
                outcome.Message = ex.Message;
                Swallow(ex, "ApplyDownloadedFile");
            }

            return outcome;
        }

        private static void LinkToRecord(string entityName, string entityGlobalId,
                                         string columnName, string path)
        {
            if (!ColumnExists(entityName, columnName)) return;

            Db.ExecuteNonQuery(
                "UPDATE [" + entityName + "] SET [" + columnName + "] = @p WHERE GlobalID = @g;",
                new SQLiteParameter("@p", path),
                new SQLiteParameter("@g", entityGlobalId));
        }

        private static void RecordAttachmentConflict(string entityName, string entityGlobalId,
            string columnName, DataRow local, string remoteHash, int remoteVersion,
            string remotePath, string reason)
        {
            var localPayload = new StringBuilder();
            localPayload.Append("ستون=").Append(columnName).AppendLine();
            localPayload.Append("مسیر=").Append(Convert.ToString(local["LocalPath"])).AppendLine();
            localPayload.Append("هش=").Append(Convert.ToString(local["ContentHash"])).AppendLine();
            localPayload.Append("اندازه=").Append(Convert.ToString(local["SizeBytes"])).AppendLine();
            localPayload.Append("نسخه=").Append(Convert.ToString(local["FileVersion"])).AppendLine();

            var remotePayload = new StringBuilder();
            remotePayload.Append("ستون=").Append(columnName).AppendLine();
            remotePayload.Append("مسیر=").Append(remotePath).AppendLine();
            remotePayload.Append("هش=").Append(remoteHash).AppendLine();
            remotePayload.Append("نسخه=").Append(remoteVersion).AppendLine();
            remotePayload.Append("دلیل=").Append(reason).AppendLine();

            SyncConflictStore.Record(entityName, entityGlobalId,
                OfflineSyncInitializer.ConflictAttachment, 0,
                Convert.ToInt32(local["FileVersion"]), remoteVersion,
                localPayload.ToString(), remotePayload.ToString());
        }

        // ═════════════════════════════════════════════════════════════════════
        // ۴) دریافت — فاز B
        //
        // جریان: فهرستِ سرور ← مقایسه با نسخهٔ محلی ← دریافتِ آنچه کم یا کهنه
        // است ← تأیید هش ← ذخیرهٔ محلی ← به‌روزرسانیِ فراداده.
        //
        // ⚠ سه قاعدهٔ تخطی‌ناپذیر:
        //   • فایلِ محلی تا وقتی هشِ نسخهٔ دریافتی تأیید نشده، حتی یک بایت
        //     دست نمی‌خورد (همه‌چیز در مسیرِ مرحله‌ای نوشته می‌شود).
        //   • اگر هر دو طرف عوض شده باشند، تعارض ثبت می‌شود و فایلِ محلی
        //     دست‌نخورده می‌ماند — هرگز بازنویسی نمی‌شود.
        //   • انتقالِ ناموفق هرگز «موفق» علامت نمی‌خورد و اطلاعاتِ تلاش
        //     (تعداد، زمان، خطا) نگه داشته می‌شود.
        // ═════════════════════════════════════════════════════════════════════
        public const int DownloadBatchSize = 10;

        // پس از این تعداد تلاشِ ناموفق، ردیف در حالت «ناموفق» رها می‌شود تا
        // یک فایلِ همیشه‌خراب، هر همگام‌سازی را کند نکند. ردیف حذف نمی‌شود و
        // اطلاعات تلاش می‌ماند؛ کاربر می‌تواند از مرکز کنترل بازنشانی کند.
        public const int MaxDownloadAttempts = 5;

        public const string KeyManifestCursor = "FileManifestCursor";

        // پوشهٔ مرحله‌ای و مقصدِ فایل‌هایی که همتای محلی ندارند. عمداً داخل
        // ریشهٔ ذخیره‌سازیِ برنامه است تا از همان اعتبارسنجیِ مسیرِ موجود عبور
        // کند.
        public const string IncomingFolderName = "SyncIncoming";

        public sealed class FileDownloadResult
        {
            public int Discovered;        // اقلامِ تازه در فهرست سرور
            public int Downloaded;
            public int Skipped;           // از قبل همین محتوا را داریم
            public int Failed;
            public int Corrupted;
            public int Denied;
            public int Conflicts;
            public int RemovedRemotely;
            public bool Cancelled;
            public bool Skipped_NoTransport;
            public string Message = "";
        }

        // ─── ۴.۱ فهرست‌گیری ──────────────────────────────────────────────────
        public static int RefreshManifest(IFileManifestTransport transport,
            IProgress<SyncProgress> progress, CancellationToken cancel)
        {
            if (transport == null || !transport.IsConfigured) return 0;
            if (!TableExists("SyncFileDownload")) return 0;

            long cursor = ReadCursor();
            int discovered = 0;
            int page = 0;

            while (!cancel.IsCancellationRequested)
            {
                FileManifestResult manifest = transport.GetManifest(cursor, 100, cancel);

                if (manifest == null || !manifest.Succeeded)
                {
                    // ⚠ نشانگر دست‌نخورده می‌ماند: صفحهٔ ناموفق دفعهٔ بعد
                    // دوباره خواسته می‌شود و چیزی از قلم نمی‌افتد.
                    break;
                }

                if (manifest.Files.Count == 0)
                {
                    if (manifest.NextCursor > cursor) SaveCursor(manifest.NextCursor);
                    break;
                }

                page++;
                int index = 0;

                foreach (RemoteFileEntry entry in manifest.Files)
                {
                    if (cancel.IsCancellationRequested) break;

                    index++;
                    if (index % 20 == 0 || index == manifest.Files.Count)
                        Report(progress, "فهرست فایل‌های سرور (صفحهٔ " + page + ")",
                               index, manifest.Files.Count);

                    if (UpsertDownload(entry)) discovered++;
                }

                if (cancel.IsCancellationRequested) break;

                // ⚠ نشانگر فقط پس از ثبتِ *کاملِ* صفحه ذخیره می‌شود؛ قطعِ وسطِ
                // کار یعنی همان صفحه دوباره می‌آید و چون ثبت بر اساس هویت
                // سراسری است، ردیف تکراری ساخته نمی‌شود.
                if (manifest.NextCursor > cursor) cursor = SaveCursor(manifest.NextCursor);

                if (!manifest.HasMore) break;
            }

            return discovered;
        }

        // ─── ۴.۲ پردازش صف ───────────────────────────────────────────────────
        public static FileDownloadResult DownloadPending(IFileManifestTransport transport,
            IProgress<SyncProgress> progress, CancellationToken cancel)
        {
            var result = new FileDownloadResult();

            if (transport == null || !transport.IsConfigured)
            {
                result.Skipped_NoTransport = true;
                result.Message = OfflineSyncTransport.OfflineMessage;
                return result;
            }

            if (!TableExists("SyncFileDownload")) return result;

            try
            {
                result.Discovered = RefreshManifest(transport, progress, cancel);
            }
            catch (OperationCanceledException) { throw; }
            catch (SyncOfflineException ex)
            {
                result.Skipped_NoTransport = true;
                result.Message = ex.Message;
                return result;
            }
            catch (Exception ex) { Swallow(ex, "RefreshManifest"); }

            if (cancel.IsCancellationRequested)
            {
                result.Cancelled = true;
                return result;
            }

            int total = PendingDownloadCount();
            if (total == 0) return result;

            int processed = 0;

            // همان محافظِ ضدِ حلقهٔ بی‌پایانِ مسیر ارسال: ردیفِ ناموفق در صف
            // می‌ماند، پس بدون ردیابیِ دیده‌شده‌ها کوئری همان را بی‌پایان
            // برمی‌گرداند.
            var attempted = new HashSet<long>();

            while (!cancel.IsCancellationRequested)
            {
                DataTable batch = GetPendingDownloads(DownloadBatchSize);
                if (batch.Rows.Count == 0) break;

                bool madeProgress = false;

                foreach (DataRow row in batch.Rows)
                {
                    if (cancel.IsCancellationRequested) break;

                    long id = Convert.ToInt64(row["DownloadID"]);
                    if (!attempted.Add(id)) continue;

                    processed++;
                    Report(progress, "دریافت فایل — " + Convert.ToString(row["FileName"]),
                           processed, total);

                    try
                    {
                        ProcessDownload(transport, row, result, cancel);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (SyncOfflineException ex)
                    {
                        // ⚠ سرور رفت ⇒ صف *دست‌نخورده*: نه تلاشی شمرده می‌شود
                        // و نه چیزی ناموفق علامت می‌خورد. همان قاعدهٔ ارسال.
                        result.Skipped_NoTransport = true;
                        result.Message = ex.Message;
                        result.Cancelled = cancel.IsCancellationRequested;
                        return result;
                    }
                    catch (Exception ex)
                    {
                        MarkDownload(id, OfflineSyncInitializer.DownloadFailed, ex.Message, true);
                        result.Failed++;
                        Swallow(ex, "ProcessDownload");
                    }

                    madeProgress = true;
                }

                if (!madeProgress) break;
            }

            result.Cancelled = cancel.IsCancellationRequested;
            return result;
        }

        private static void ProcessDownload(IFileManifestTransport transport, DataRow row,
                                            FileDownloadResult result, CancellationToken cancel)
        {
            long id            = Convert.ToInt64(row["DownloadID"]);
            string fileGlobalId= Convert.ToString(row["FileGlobalID"]);
            string entityName  = Convert.ToString(row["EntityName"]);
            string entityGuid  = Convert.ToString(row["EntityGlobalID"]);
            string columnName  = Convert.ToString(row["ColumnName"]);
            string fileName    = Convert.ToString(row["FileName"]);
            string remoteHash  = Convert.ToString(row["ContentHash"]);
            long remoteSize    = Convert.ToInt64(row["SizeBytes"]);
            int remoteVersion  = Convert.ToInt32(row["FileVersion"]);
            bool remoteDeleted = Convert.ToInt64(row["RemoteDeleted"]) != 0;

            // ── فایل در سرور حذف شده ──
            // ⚠ فایلِ محلی *پاک نمی‌شود*. حذفِ سرور فقط ثبت می‌شود؛ تنها نسخهٔ
            // موجودِ یک سند نباید با یک تصمیمِ راه دور نابود شود.
            if (remoteDeleted)
            {
                MarkDownload(id, OfflineSyncInitializer.DownloadRemoved, null, false);
                result.RemovedRemotely++;
                return;
            }

            // ── اعتبارسنجی پیش از هر بایتی ──
            // ⚠ امنیت: سرور یک مرزِ اعتماد نیست. فهرستی که پسوندِ اجرایی اعلام
            // می‌کند هرگز نباید دانلود شود.
            string extension = SafeExtension(fileName);
            if (!AllowedExtensions.Contains(extension ?? ""))
            {
                MarkDownload(id, OfflineSyncInitializer.DownloadDenied,
                             "نوع فایل مجاز نیست (" + extension + ")", false);
                result.Denied++;
                return;
            }

            // ── از قبل همین محتوا را داریم؟ ──
            DataRow existing = FindRow(entityName, entityGuid, columnName);

            if (existing != null && !string.IsNullOrEmpty(remoteHash) &&
                string.Equals(Convert.ToString(existing["ContentHash"]), remoteHash,
                              StringComparison.OrdinalIgnoreCase) &&
                LocalFileExists(existing))
            {
                MarkDownloadDone(id, Convert.ToString(existing["LocalPath"]),
                                 OfflineSyncInitializer.DownloadNotNeeded);
                result.Skipped++;
                return;
            }

            // ── فضای دیسک ──
            // شکستِ صریح و زودهنگام بسیار بهتر از نوشتنِ نیمه‌کاره و پر کردنِ
            // دیسکی است که خودِ پایگاه‌داده هم روی آن است.
            string incoming = IncomingFolder();
            if (string.IsNullOrWhiteSpace(incoming))
            {
                MarkDownload(id, OfflineSyncInitializer.DownloadFailed,
                             "پوشهٔ ذخیره‌سازی برنامه در دسترس نیست", true);
                result.Failed++;
                return;
            }

            if (!HasFreeSpace(incoming, remoteSize))
            {
                MarkDownload(id, OfflineSyncInitializer.DownloadFailed,
                             "فضای دیسک کافی نیست", true);
                result.Failed++;
                return;
            }

            string staging = Path.Combine(incoming, "staging",
                                          SafeName(fileGlobalId) + ".part");

            FileTransferResult transfer =
                transport.DownloadToFile(fileGlobalId, staging, remoteHash, remoteSize, cancel);

            if (transfer == null || !transfer.Succeeded)
            {
                if (transfer != null && transfer.Corrupted)
                {
                    MarkDownload(id, OfflineSyncInitializer.DownloadCorrupt,
                                 transfer.ErrorMessage, true);
                    result.Corrupted++;
                }
                else if (transfer != null && transfer.AccessDenied)
                {
                    MarkDownload(id, OfflineSyncInitializer.DownloadDenied,
                                 transfer.ErrorMessage, true);
                    result.Denied++;
                }
                else
                {
                    MarkDownload(id, OfflineSyncInitializer.DownloadFailed,
                                 transfer == null ? "پاسخی دریافت نشد" : transfer.ErrorMessage, true);
                    result.Failed++;
                }

                return;   // ⚠ فایل محلی در همهٔ این مسیرها دست‌نخورده ماند.
            }

            // ── تعارض: هر دو طرف عوض شده‌اند ──
            bool localExists = existing != null && LocalFileExists(existing);
            string localHash = existing == null ? null : Convert.ToString(existing["ContentHash"]);

            if (localExists && !string.IsNullOrEmpty(localHash) &&
                !string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase) &&
                HasUnsentLocalChange(existing))
            {
                RecordAttachmentConflict(entityName, entityGuid, columnName, existing,
                                         remoteHash, remoteVersion, staging,
                                         "فایل در دو محل تغییر کرده است");

                MarkFileState(existing, OfflineSyncInitializer.FileConflict);
                MarkDownload(id, OfflineSyncInitializer.DownloadConflict,
                             "تعارض ثبت شد — فایل محلی دست‌نخورده ماند", false);

                result.Conflicts++;
                return;   // ⚠ فایل مرحله‌ای نگه داشته می‌شود؛ شاهدِ تعارض است.
            }

            // ── جای‌گذاری ──
            string finalPath = localExists
                ? Convert.ToString(existing["LocalPath"])
                : Path.Combine(incoming, SafeName(entityName),
                               SafeName(entityGuid) + "_" + SafeName(columnName) + extension);

            string placement = PlaceFile(staging, finalPath);
            if (placement != null)
            {
                MarkDownload(id, OfflineSyncInitializer.DownloadFailed, placement, true);
                result.Failed++;
                return;
            }

            // ── تأیید دوباره پس از جای‌گذاری ──
            // ⚠ بین تأیید و انتقال، دیسکِ پر یا خطای سیستم‌فایل می‌تواند فایل
            // را ناقص بگذارد. یک بار دیگر سنجیده می‌شود تا «موفق» فقط وقتی
            // ثبت شود که واقعاً محتوای درست روی دیسک نشسته باشد.
            string placedHash = ComputeHash(finalPath);
            if (!string.IsNullOrEmpty(remoteHash) &&
                !string.Equals(placedHash, remoteHash, StringComparison.OrdinalIgnoreCase))
            {
                MarkDownload(id, OfflineSyncInitializer.DownloadCorrupt,
                             "محتوا پس از ذخیره تأیید نشد", true);
                result.Corrupted++;
                return;
            }

            CommitDownload(entityName, entityGuid, columnName, finalPath,
                           placedHash, remoteVersion, existing);

            MarkDownloadDone(id, finalPath, OfflineSyncInitializer.DownloadDone);
            result.Downloaded++;
        }

        // فایلِ تأییدشده را در جای نهایی می‌نشاند. مقدار بازگشتی: پیام خطا یا
        // null در صورت موفقیت.
        //
        // ⚠ بازنویسی فقط پس از رد شدنِ همهٔ بررسی‌های تعارض به اینجا می‌رسد؛
        // یعنی نسخهٔ محلی یا وجود ندارد یا از قبل با سرور همگام بوده.
        private static string PlaceFile(string staging, string finalPath)
        {
            try
            {
                if (!File.Exists(staging)) return "فایل مرحله‌ای پیدا نشد";

                string folder = Path.GetDirectoryName(finalPath);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                if (File.Exists(finalPath))
                {
                    // File.Replace اتمیک است: یا فایل قدیمی سرِ جایش می‌ماند یا
                    // کاملاً با نسخهٔ تازه عوض می‌شود — هیچ حالتِ نیمه‌کاره‌ای
                    // وجود ندارد.
                    try { File.Replace(staging, finalPath, null, true); }
                    catch (Exception)
                    {
                        // بعضی سیستم‌فایل‌ها (اشتراک شبکه) Replace را پشتیبانی
                        // نمی‌کنند؛ مسیرِ جایگزین با حفظِ نسخهٔ قدیمی تا لحظهٔ
                        // آخر.
                        string backup = finalPath + ".previous";
                        try { if (File.Exists(backup)) File.Delete(backup); } catch { }

                        File.Move(finalPath, backup);
                        try
                        {
                            File.Move(staging, finalPath);
                            try { File.Delete(backup); } catch { }
                        }
                        catch
                        {
                            // بازگرداندن نسخهٔ قدیمی — هرگز بدون فایل نمی‌مانیم.
                            try { if (!File.Exists(finalPath)) File.Move(backup, finalPath); } catch { }
                            throw;
                        }
                    }
                }
                else
                {
                    File.Move(staging, finalPath);
                }

                return null;
            }
            catch (IOException ex)
            {
                return "ذخیرهٔ فایل ممکن نشد: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                return "دسترسی نوشتن روی مسیر مقصد نیست: " + ex.Message;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ثبتِ فرادادهٔ فایلِ دریافت‌شده و وصل کردنش به رکورد.
        private static void CommitDownload(string entityName, string entityGlobalId,
            string columnName, string path, string hash, int remoteVersion, DataRow existing)
        {
            long size = 0;
            try { size = new FileInfo(path).Length; } catch { }

            Upsert(entityName, entityGlobalId, columnName, path, hash, size,
                   OfflineSyncInitializer.FileDownloaded, null, existing);

            LinkToRecord(entityName, entityGlobalId, columnName, path);
        }

        // «تغییرِ محلیِ ارسال‌نشده» — تنها حالتی که بازنویسی ممنوع است.
        private static bool HasUnsentLocalChange(DataRow existing)
        {
            string state = Convert.ToString(existing["UploadState"]);

            return state == OfflineSyncInitializer.FilePending
                || state == OfflineSyncInitializer.FileFailed
                || state == OfflineSyncInitializer.FileConflict;
        }

        private static bool LocalFileExists(DataRow existing)
        {
            try
            {
                string path = Convert.ToString(existing["LocalPath"]);
                return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            }
            catch { return false; }
        }

        public static string IncomingFolder()
        {
            try
            {
                string root = FileHelper.GetBaseRootFolder();
                if (string.IsNullOrWhiteSpace(root)) return null;

                string folder = Path.Combine(root, IncomingFolderName);
                Directory.CreateDirectory(folder);
                return folder;
            }
            catch { return null; }
        }

        private static bool HasFreeSpace(string folder, long needed)
        {
            // حاشیهٔ ۱۰ مگابایتی: دیسکی که دقیقاً به اندازهٔ فایل جا دارد،
            // عملاً پر است و نوشتنِ پایگاه‌داده را هم می‌شکند.
            const long Margin = 10L * 1024 * 1024;

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder)));
                if (!drive.IsReady) return true;   // نمی‌دانیم ⇒ مانع نمی‌شویم
                return drive.AvailableFreeSpace > needed + Margin;
            }
            catch { return true; }
        }

        private static string SafeExtension(string fileName)
        {
            try { return Path.GetExtension(fileName ?? "") ?? ""; }
            catch { return ""; }
        }

        // نامِ سرور هرگز مستقیم وارد مسیر نمی‌شود.
        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');

            string result = sb.ToString();
            return result.Length > 80 ? result.Substring(0, 80) : result;
        }

        // ─── صفِ دریافت: خواندن و نوشتن ─────────────────────────────────────
        private static bool UpsertDownload(RemoteFileEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.FileGlobalId)) return false;

            try
            {
                DataTable existing = Db.Query(
                    "SELECT DownloadID, ContentHash, State FROM SyncFileDownload WHERE FileGlobalID = @f LIMIT 1;",
                    new SQLiteParameter("@f", entry.FileGlobalId));

                if (existing.Rows.Count == 0)
                {
                    Db.ExecuteNonQuery(@"
INSERT INTO SyncFileDownload
    (FileGlobalID, EntityName, EntityGlobalID, ColumnName, FileName, FileType,
     ContentHash, SizeBytes, FileVersion, RemoteDeleted, RemoteCursor, State, CenterID)
VALUES
    (@f, @e, @g, @c, @n, @t, @h, @s, @v, @d, @cur, @st, @center);",
                        new SQLiteParameter("@f", entry.FileGlobalId),
                        new SQLiteParameter("@e", entry.EntityName ?? ""),
                        new SQLiteParameter("@g", entry.EntityGlobalId ?? ""),
                        new SQLiteParameter("@c", entry.ColumnName ?? ""),
                        new SQLiteParameter("@n", (object)entry.FileName ?? DBNull.Value),
                        new SQLiteParameter("@t", (object)entry.FileType ?? DBNull.Value),
                        new SQLiteParameter("@h", (object)entry.ContentHash ?? DBNull.Value),
                        new SQLiteParameter("@s", entry.SizeBytes),
                        new SQLiteParameter("@v", entry.FileVersion),
                        new SQLiteParameter("@d", entry.Deleted ? 1 : 0),
                        new SQLiteParameter("@cur", entry.Cursor),
                        new SQLiteParameter("@st", OfflineSyncInitializer.DownloadPending),
                        new SQLiteParameter("@center", SecurityContext.CurrentCenterId > 0
                                                           ? (object)SecurityContext.CurrentCenterId : DBNull.Value));
                    return true;
                }

                DataRow row = existing.Rows[0];

                // محتوای سرور عوض شده ⇒ ردیف دوباره در صف می‌نشیند و شمارندهٔ
                // تلاش صفر می‌شود (این یک انتقالِ *تازه* است، نه تکرارِ همان
                // شکستِ قبلی).
                bool changed = !string.Equals(Convert.ToString(row["ContentHash"]),
                                              entry.ContentHash, StringComparison.OrdinalIgnoreCase);

                Db.ExecuteNonQuery(@"
UPDATE SyncFileDownload
SET EntityName = @e, EntityGlobalID = @g, ColumnName = @c, FileName = @n, FileType = @t,
    ContentHash = @h, SizeBytes = @s, FileVersion = @v, RemoteDeleted = @d,
    RemoteCursor = @cur,
    State    = CASE WHEN @changed = 1 THEN @pending ELSE State END,
    Attempts = CASE WHEN @changed = 1 THEN 0 ELSE Attempts END,
    LastError= CASE WHEN @changed = 1 THEN NULL ELSE LastError END
WHERE DownloadID = @id;",
                    new SQLiteParameter("@e", entry.EntityName ?? ""),
                    new SQLiteParameter("@g", entry.EntityGlobalId ?? ""),
                    new SQLiteParameter("@c", entry.ColumnName ?? ""),
                    new SQLiteParameter("@n", (object)entry.FileName ?? DBNull.Value),
                    new SQLiteParameter("@t", (object)entry.FileType ?? DBNull.Value),
                    new SQLiteParameter("@h", (object)entry.ContentHash ?? DBNull.Value),
                    new SQLiteParameter("@s", entry.SizeBytes),
                    new SQLiteParameter("@v", entry.FileVersion),
                    new SQLiteParameter("@d", entry.Deleted ? 1 : 0),
                    new SQLiteParameter("@cur", entry.Cursor),
                    new SQLiteParameter("@changed", changed || entry.Deleted ? 1 : 0),
                    new SQLiteParameter("@pending", OfflineSyncInitializer.DownloadPending),
                    new SQLiteParameter("@id", Convert.ToInt64(row["DownloadID"])));

                return changed;
            }
            catch (Exception ex) { Swallow(ex, "UpsertDownload"); return false; }
        }

        private static DataTable GetPendingDownloads(int limit)
        {
            try
            {
                return Db.Query(@"
SELECT * FROM SyncFileDownload
WHERE State IN (@p, @f, @c) AND Attempts < @max
ORDER BY DownloadID LIMIT @l;",
                    new SQLiteParameter("@p", OfflineSyncInitializer.DownloadPending),
                    new SQLiteParameter("@f", OfflineSyncInitializer.DownloadFailed),
                    new SQLiteParameter("@c", OfflineSyncInitializer.DownloadCorrupt),
                    new SQLiteParameter("@max", MaxDownloadAttempts),
                    new SQLiteParameter("@l", Math.Max(1, limit)));
            }
            catch { return new DataTable(); }
        }

        private static void MarkDownload(long id, string state, string error, bool countAttempt)
        {
            Try(delegate
            {
                Db.ExecuteNonQuery(@"
UPDATE SyncFileDownload
SET State = @s, LastError = @e, LastAttemptAt = datetime('now'), Attempts = Attempts + @a
WHERE DownloadID = @id;",
                    new SQLiteParameter("@s", state),
                    new SQLiteParameter("@e", (object)error ?? DBNull.Value),
                    new SQLiteParameter("@a", countAttempt ? 1 : 0),
                    new SQLiteParameter("@id", id));
            });
        }

        private static void MarkDownloadDone(long id, string localPath, string state)
        {
            Try(delegate
            {
                Db.ExecuteNonQuery(@"
UPDATE SyncFileDownload
SET State = @s, LocalPath = @p, CompletedAt = datetime('now'),
    LastAttemptAt = datetime('now'), LastError = NULL
WHERE DownloadID = @id;",
                    new SQLiteParameter("@s", state),
                    new SQLiteParameter("@p", (object)localPath ?? DBNull.Value),
                    new SQLiteParameter("@id", id));
            });
        }

        private static long ReadCursor()
        {
            long cursor;
            return long.TryParse(SyncOutboxService.GetState(KeyManifestCursor, "0"), out cursor)
                ? cursor : 0;
        }

        private static long SaveCursor(long cursor)
        {
            SyncOutboxService.SetState(KeyManifestCursor, cursor.ToString());
            return cursor;
        }

        // ═════════════════════════════════════════════════════════════════════
        // شمارنده‌ها و سلامت — برای مرکز کنترل
        // ═════════════════════════════════════════════════════════════════════
        public static int PendingUploadCount() { return CountWhere("UploadState = @v", OfflineSyncInitializer.FilePending); }
        public static int FailedUploadCount()  { return CountWhere("UploadState = @v", OfflineSyncInitializer.FileFailed); }
        public static int MissingCount()       { return CountWhere("SyncStatus = @v",  OfflineSyncInitializer.FileMissing); }
        public static int ConflictCount()      { return CountWhere("SyncStatus = @v",  OfflineSyncInitializer.FileConflict); }
        public static int RejectedCount()      { return CountWhere("SyncStatus = @v",  OfflineSyncInitializer.FileRejected); }

        // ─── صفِ دریافت (فاز B) ─────────────────────────────────────────────
        public static int PendingDownloadCount()
        {
            return CountDownloads(
                "State IN ('" + OfflineSyncInitializer.DownloadPending + "','" +
                OfflineSyncInitializer.DownloadFailed + "','" +
                OfflineSyncInitializer.DownloadCorrupt + "') AND Attempts < " + MaxDownloadAttempts);
        }

        // انتقالِ ناموفق = هر ردیفی که تلاش‌هایش تمام شده یا دسترسی رد شده.
        public static int FailedDownloadCount()
        {
            return CountDownloads(
                "(State IN ('" + OfflineSyncInitializer.DownloadFailed + "','" +
                OfflineSyncInitializer.DownloadCorrupt + "') AND Attempts >= " + MaxDownloadAttempts + ")" +
                " OR State = '" + OfflineSyncInitializer.DownloadDenied + "'");
        }

        public static int DownloadedCount()
        {
            return CountDownloads("State = '" + OfflineSyncInitializer.DownloadDone + "'");
        }

        public static int DownloadConflictCount()
        {
            return CountDownloads("State = '" + OfflineSyncInitializer.DownloadConflict + "'");
        }

        public static string LastDownloadAt()
        {
            try
            {
                if (!TableExists("SyncFileDownload")) return "";
                object value = Db.ExecuteScalar("SELECT MAX(CompletedAt) FROM SyncFileDownload;");
                return value == null || value == DBNull.Value ? "" : Convert.ToString(value);
            }
            catch { return ""; }
        }

        private static int CountDownloads(string where)
        {
            try
            {
                if (!TableExists("SyncFileDownload")) return 0;

                object result = Db.ExecuteScalar(
                    "SELECT COUNT(1) FROM SyncFileDownload WHERE " + where + ";");

                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            catch { return 0; }
        }

        // بازنشانیِ دستیِ انتقال‌های سوخته — از مرکز کنترل. ردیف حذف نمی‌شود؛
        // فقط دوباره واجدِ تلاش می‌شود.
        public static int ResetFailedDownloads()
        {
            try
            {
                if (!TableExists("SyncFileDownload")) return 0;

                return Db.ExecuteNonQuery(
                    "UPDATE SyncFileDownload SET Attempts = 0, State = @p, LastError = NULL " +
                    "WHERE State IN (@f, @c, @d);",
                    new SQLiteParameter("@p", OfflineSyncInitializer.DownloadPending),
                    new SQLiteParameter("@f", OfflineSyncInitializer.DownloadFailed),
                    new SQLiteParameter("@c", OfflineSyncInitializer.DownloadCorrupt),
                    new SQLiteParameter("@d", OfflineSyncInitializer.DownloadDenied));
            }
            catch { return 0; }
        }

        public static int TotalCount()
        {
            try
            {
                if (!TableExists("SyncFile")) return 0;
                object value = Db.ExecuteScalar("SELECT COUNT(1) FROM SyncFile;");
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
            catch { return 0; }
        }

        public static string LastFileSyncAt()
        {
            try
            {
                if (!TableExists("SyncFile")) return "";
                object value = Db.ExecuteScalar("SELECT MAX(UploadedAt) FROM SyncFile;");
                return value == null || value == DBNull.Value ? "" : Convert.ToString(value);
            }
            catch { return ""; }
        }

        public sealed class FileHealth
        {
            public int Total, Pending, Failed, Missing, Conflicts, Rejected;
            public string LastSyncAt = "";
            public int Score = -1;
            public readonly List<string> Notes = new List<string>();

            // ─── فاز B — سمتِ دریافت ───
            public int PendingDownloads, FailedDownloads, Downloaded, DownloadConflicts;
            public string LastDownloadAt = "";
        }

        public static FileHealth GetHealth(IFileSyncTransport transport)
        {
            var health = new FileHealth();

            try
            {
                health.Total      = TotalCount();
                health.Pending    = PendingUploadCount();
                health.Failed     = FailedUploadCount();
                health.Missing    = MissingCount();
                health.Conflicts  = ConflictCount();
                health.Rejected   = RejectedCount();
                health.LastSyncAt = LastFileSyncAt();

                health.PendingDownloads  = PendingDownloadCount();
                health.FailedDownloads   = FailedDownloadCount();
                health.Downloaded        = DownloadedCount();
                health.DownloadConflicts = DownloadConflictCount();
                health.LastDownloadAt    = LastDownloadAt();

                // فایل‌های گمشده و ردشده *مستقل از سرور* ایراد واقعی‌اند و
                // همیشه سنجیده می‌شوند؛ ولی «در انتظار ارسال» تا وقتی سروری
                // نیست ایراد نیست.
                int score = 100;

                if (health.Missing > 0)
                {
                    score -= Math.Min(40, health.Missing * 2);
                    health.Notes.Add(health.Missing + " فایل روی دیسک نیست");
                }

                if (health.Rejected > 0)
                {
                    score -= Math.Min(20, health.Rejected * 2);
                    health.Notes.Add(health.Rejected + " فایل نامعتبر");
                }

                if (health.Conflicts > 0)
                {
                    score -= Math.Min(20, health.Conflicts * 5);
                    health.Notes.Add(health.Conflicts + " تعارض پیوست");
                }

                bool configured = transport != null && transport.IsConfigured;
                if (configured && health.Failed > 0)
                {
                    score -= Math.Min(20, health.Failed * 2);
                    health.Notes.Add(health.Failed + " ارسال ناموفق");
                }

                // ⚠ «در انتظار دریافت» مثل «در انتظار ارسال» ایراد نیست؛ فقط
                // انتقالی که تلاش‌هایش تمام شده جریمه می‌شود.
                if (configured && health.FailedDownloads > 0)
                {
                    score -= Math.Min(20, health.FailedDownloads * 2);
                    health.Notes.Add(health.FailedDownloads + " دریافت ناموفق");
                }

                if (health.DownloadConflicts > 0)
                {
                    score -= Math.Min(10, health.DownloadConflicts * 5);
                    health.Notes.Add(health.DownloadConflicts + " تعارض هنگام دریافت");
                }

                if (!configured)
                    health.Notes.Add("سروری پیکربندی نشده — فایل‌ها محلی نگهداری می‌شوند");

                health.Score = Math.Max(0, Math.Min(100, score));
            }
            catch (Exception ex) { Swallow(ex, "GetHealth"); }

            return health;
        }

        // ═════════════════════════════════════════════════════════════════════
        // درونی
        // ═════════════════════════════════════════════════════════════════════
        private static void Upsert(string entityName, string entityGlobalId, string columnName,
                                   string path, string hash, long size, string status,
                                   string error, DataRow existing)
        {
            string fileName = "";
            try { fileName = string.IsNullOrWhiteSpace(path) ? "" : Path.GetFileName(path); } catch { }

            bool uploadable = status == OfflineSyncInitializer.FilePending;

            if (existing == null)
            {
                Db.ExecuteNonQuery(@"
INSERT INTO SyncFile
    (FileGlobalID, EntityName, EntityGlobalID, ColumnName, FileName, LocalPath,
     ContentHash, SizeBytes, FileVersion, ModifiedAt, SyncStatus, UploadState,
     LastError, CenterID, RegisteredBy, MachineName)
VALUES
    (@fid, @e, @g, @c, @n, @p, @h, @s, 1, datetime('now'), @st, @us, @err, @center, @user, @machine);",
                    new SQLiteParameter("@fid", Guid.NewGuid().ToString()),
                    new SQLiteParameter("@e", entityName),
                    new SQLiteParameter("@g", entityGlobalId),
                    new SQLiteParameter("@c", columnName),
                    new SQLiteParameter("@n", fileName),
                    new SQLiteParameter("@p", (object)path ?? DBNull.Value),
                    new SQLiteParameter("@h", (object)hash ?? DBNull.Value),
                    new SQLiteParameter("@s", size),
                    new SQLiteParameter("@st", status),
                    new SQLiteParameter("@us", uploadable ? OfflineSyncInitializer.FilePending : status),
                    new SQLiteParameter("@err", (object)error ?? DBNull.Value),
                    new SQLiteParameter("@center", SecurityContext.CurrentCenterId > 0
                                                       ? (object)SecurityContext.CurrentCenterId : DBNull.Value),
                    new SQLiteParameter("@user", (object)SecurityContext.Username ?? DBNull.Value),
                    new SQLiteParameter("@machine", SafeMachineName()));
                return;
            }

            // محتوا عوض شده ⇒ نسخهٔ فایل جلو می‌رود و دوباره در صف می‌نشیند.
            bool contentChanged = hash != null &&
                !string.Equals(Convert.ToString(existing["ContentHash"]), hash, StringComparison.OrdinalIgnoreCase);

            Db.ExecuteNonQuery(@"
UPDATE SyncFile
SET FileName = @n, LocalPath = @p, ContentHash = @h, SizeBytes = @s,
    FileVersion = FileVersion + @bump, ModifiedAt = datetime('now'),
    SyncStatus = @st, UploadState = @us, LastError = @err
WHERE FileID = @id;",
                new SQLiteParameter("@n", fileName),
                new SQLiteParameter("@p", (object)path ?? DBNull.Value),
                new SQLiteParameter("@h", (object)hash ?? DBNull.Value),
                new SQLiteParameter("@s", size),
                new SQLiteParameter("@bump", contentChanged ? 1 : 0),
                new SQLiteParameter("@st", status),
                new SQLiteParameter("@us", uploadable ? OfflineSyncInitializer.FilePending : status),
                new SQLiteParameter("@err", (object)error ?? DBNull.Value),
                new SQLiteParameter("@id", Convert.ToInt64(existing["FileID"])));
        }

        private static DataRow FindRow(string entityName, string entityGlobalId, string columnName)
        {
            try
            {
                DataTable table = Db.Query(
                    "SELECT * FROM SyncFile WHERE EntityName = @e AND EntityGlobalID = @g AND ColumnName = @c LIMIT 1;",
                    new SQLiteParameter("@e", entityName),
                    new SQLiteParameter("@g", entityGlobalId),
                    new SQLiteParameter("@c", columnName));

                return table.Rows.Count == 0 ? null : table.Rows[0];
            }
            catch { return null; }
        }

        private static DataTable GetPendingUploads(int limit)
        {
            try
            {
                return Db.Query(
                    "SELECT * FROM SyncFile WHERE UploadState IN (@p, @f) ORDER BY FileID LIMIT @l;",
                    new SQLiteParameter("@p", OfflineSyncInitializer.FilePending),
                    new SQLiteParameter("@f", OfflineSyncInitializer.FileFailed),
                    new SQLiteParameter("@l", Math.Max(1, limit)));
            }
            catch { return new DataTable(); }
        }

        private static void MarkUploaded(long fileId)
        {
            Try(delegate
            {
                Db.ExecuteNonQuery(
                    "UPDATE SyncFile SET UploadState = @u, SyncStatus = @u, UploadedAt = datetime('now'), " +
                    "LastError = NULL WHERE FileID = @id;",
                    new SQLiteParameter("@u", OfflineSyncInitializer.FileUploaded),
                    new SQLiteParameter("@id", fileId));
            });
        }

        private static void MarkState(long fileId, string state, string error, bool countAttempt)
        {
            Try(delegate
            {
                Db.ExecuteNonQuery(
                    "UPDATE SyncFile SET UploadState = @s, SyncStatus = @s, LastError = @e, " +
                    "LastAttemptAt = datetime('now'), Attempts = Attempts + @a WHERE FileID = @id;",
                    new SQLiteParameter("@s", state),
                    new SQLiteParameter("@e", (object)(error ?? "")),
                    new SQLiteParameter("@a", countAttempt ? 1 : 0),
                    new SQLiteParameter("@id", fileId));
            });
        }

        private static void MarkFileState(DataRow row, string state)
        {
            MarkState(Convert.ToInt64(row["FileID"]), state, null, false);
        }

        private static void UpdateHash(long fileId, string hash, long size)
        {
            Try(delegate
            {
                Db.ExecuteNonQuery(
                    "UPDATE SyncFile SET ContentHash = @h, SizeBytes = @s, FileVersion = FileVersion + 1, " +
                    "ModifiedAt = datetime('now') WHERE FileID = @id;",
                    new SQLiteParameter("@h", hash),
                    new SQLiteParameter("@s", size),
                    new SQLiteParameter("@id", fileId));
            });
        }

        private static int CountWhere(string where, string value)
        {
            try
            {
                if (!TableExists("SyncFile")) return 0;

                object result = Db.ExecuteScalar(
                    "SELECT COUNT(1) FROM SyncFile WHERE " + where + ";",
                    new SQLiteParameter("@v", value));

                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            catch { return 0; }
        }

        private static bool TableExists(string name)
        {
            try
            {
                object value = Db.ExecuteScalar(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@n;",
                    new SQLiteParameter("@n", name));
                return value != null && value != DBNull.Value && Convert.ToInt32(value) > 0;
            }
            catch { return false; }
        }

        private static bool ColumnExists(string table, string column)
        {
            try
            {
                DataTable info = Db.Query("PRAGMA table_info([" + table + "]);");
                foreach (DataRow row in info.Rows)
                    if (string.Equals(Convert.ToString(row["name"]), column, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            catch { }
            return false;
        }

        private static void Report(IProgress<SyncProgress> progress, string phase, int current, int total)
        {
            if (progress == null) return;
            progress.Report(new SyncProgress { Phase = phase, Current = current, Total = total });
        }

        private static string SafeMachineName()
        {
            try { return Environment.MachineName; } catch { return ""; }
        }

        private static void Try(Action action)
        {
            try { action(); } catch (Exception ex) { Swallow(ex, "Update"); }
        }

        private static void Swallow(Exception ex, string source)
        {
            try { Enterprise.ErrorLogger.Log(ex, "SyncFileService." + source); } catch { }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // انتقال فایل روی HTTP — فاز ۳.
    //
    // این کلاس قرارداد موجودِ IFileSyncTransport را پیاده می‌کند، پس
    // SyncFileService (که از فاز ۷ آماده و آزموده است) بدون هیچ تغییری با
    // سرور کار می‌کند: کشف فایل، صف، تلاش دوباره و بررسی هش همه همان‌جا
    // می‌مانند و اینجا فقط «بردنِ بایت‌ها» انجام می‌شود.
    //
    // آپلود سه‌مرحله‌ای است:
    //   init     → سرور می‌گوید چند بایت دارد (ازسرگیری) یا اصلاً لازم نیست
    //   chunk    → تکه‌های ۱ مگابایتی، جریانی، بدون بارگذاری فایل در حافظه
    //   complete → سرور هش را دوباره حساب می‌کند و تأیید یا رد می‌کند
    //
    // ⚠ قاعدهٔ آفلاین دست‌نخورده می‌ماند: نبودِ شبکه SyncOfflineException
    // می‌دهد و SyncFileService صف را دست‌نخورده رها می‌کند؛ فایل محلی هرگز
    // حذف نمی‌شود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class HttpFileSyncTransport : IFileSyncTransport, IFileManifestTransport
    {
        // تکهٔ یک مگابایتی: روی اتصالِ ضعیف، قطعیِ وسطِ کار حداکثر همین مقدار
        // را هدر می‌دهد.
        private const int ChunkSize = 1024 * 1024;

        private const int DefaultTimeoutMs = 120000;   // فایل بزرگ روی خط کند

        private readonly string _baseUrl;
        private readonly int _timeoutMs;
        private readonly HttpSyncTransport _auth;

        public HttpFileSyncTransport()
            : this(SyncOutboxService.GetState(HttpSyncTransport.KeyServerUrl, ""), DefaultTimeoutMs) { }

        public HttpFileSyncTransport(string baseUrl, int timeoutMs = DefaultTimeoutMs)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _timeoutMs = timeoutMs <= 0 ? DefaultTimeoutMs : timeoutMs;

            // توکن از همان انتقالِ دادهٔ موجود گرفته می‌شود — یک نشستِ واحد
            // برای رکوردها و فایل‌ها.
            _auth = new HttpSyncTransport(_baseUrl, timeoutMs);
        }

        public bool IsConfigured { get { return !string.IsNullOrWhiteSpace(_baseUrl); } }

        // برای ورود اولیه از بیرون (همان اعتبارنامهٔ انتقال داده).
        public HttpSyncTransport Auth { get { return _auth; } }

        // ═════════════════════════════════════════════════════════════════════
        public FileTransferResult UploadFile(FileSyncItem item, CancellationToken cancel)
        {
            if (!IsConfigured)
                return Failure(OfflineSyncTransport.OfflineMessage);

            if (item == null || string.IsNullOrWhiteSpace(item.LocalPath))
                return Failure("اطلاعات فایل ناقص است.");

            if (!File.Exists(item.LocalPath))
                return Failure("فایل روی دیسک نیست.");

            try
            {
                string token = _auth.AcquireAccessToken(cancel);

                // ⚠ نبودِ توکن یعنی «نمی‌توان با سرور حرف زد» — دقیقاً هم‌ردهٔ
                // نبودِ شبکه، نه شکستِ این فایل. اگر آن را شکست می‌شمردیم،
                // یک بار ورودنکردن باعث می‌شد شمارندهٔ تلاشِ *همهٔ* فایل‌ها
                // بالا برود و آمار سلامت بی‌معنا شود.
                if (token == null)
                    throw new SyncOfflineException("احراز هویت با سرور ممکن نشد.");

                // ── ۱) init ──
                var initRequest = new Dictionary<string, object>
                {
                    { "entityName",     item.EntityName },
                    { "entityGlobalId", item.EntityGlobalId },
                    { "columnName",     item.ColumnName ?? "" },
                    { "fileName",       item.FileName ?? Path.GetFileName(item.LocalPath) },
                    { "fileType",       FileTypeOf(item) },
                    { "contentHash",    item.ContentHash },
                    { "sizeBytes",      item.SizeBytes },
                    { "fileVersion",    item.FileVersion }
                };

                string initBody = Send("POST",
                    "/api/v1/files/upload/init?deviceGuid=" + HttpSyncTransport.DeviceGuid,
                    Serialize(initRequest), token, cancel);

                Dictionary<string, object> init = Deserialize(initBody);

                if (!ReadBool(init, "succeeded"))
                    return Failure(ReadString(init, "errorMessage") ?? "شروع آپلود ممکن نشد.");

                string uploadId = ReadString(init, "uploadId");
                if (string.IsNullOrEmpty(uploadId)) return Failure("شناسهٔ آپلود دریافت نشد.");

                // ⚡ سرور همین محتوا را از قبل دارد ⇒ هیچ بایتی منتقل نمی‌شود.
                if (ReadBool(init, "alreadyExists"))
                    return new FileTransferResult
                    {
                        Succeeded = true,
                        ContentHash = item.ContentHash,
                        FileVersion = item.FileVersion,
                        BytesTransferred = 0
                    };

                long offset = ReadLong(init, "offset");

                // ── ۲) تکه‌ها ──
                long transferred = SendChunks(item.LocalPath, uploadId, offset, token, cancel);

                // ── ۳) complete ──
                string completeBody = Send("POST",
                    "/api/v1/files/upload/" + uploadId + "/complete", "", token, cancel);

                Dictionary<string, object> complete = Deserialize(completeBody);

                if (!ReadBool(complete, "succeeded"))
                    return Failure(ReadString(complete, "errorMessage") ?? "تکمیل آپلود ناموفق بود.");

                return new FileTransferResult
                {
                    Succeeded = true,
                    ContentHash = ReadString(complete, "contentHash"),
                    FileVersion = ReadInt(complete, "fileVersion"),
                    BytesTransferred = transferred
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (SyncOfflineException) { throw; }
            catch (Exception ex)
            {
                return Failure(ex.Message);
            }
        }

        // ازسرگیری: از offsetِ اعلام‌شدهٔ سرور شروع می‌شود، نه از صفر.
        private long SendChunks(string path, string uploadId, long offset, string token,
                                CancellationToken cancel)
        {
            long transferred = 0;

            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                FileShare.ReadWrite, ChunkSize))
            {
                if (offset > 0 && offset <= source.Length) source.Seek(offset, SeekOrigin.Begin);

                var buffer = new byte[ChunkSize];
                long position = offset;

                while (true)
                {
                    cancel.ThrowIfCancellationRequested();

                    int read = source.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    string body = SendBinary(
                        "/api/v1/files/upload/" + uploadId + "?offset=" + position,
                        buffer, read, token, cancel);

                    Dictionary<string, object> chunk = Deserialize(body);

                    if (!ReadBool(chunk, "succeeded"))
                        throw new Exception(ReadString(chunk, "errorMessage") ?? "ارسال تکه ناموفق بود.");

                    position += read;
                    transferred += read;
                }
            }

            return transferred;
        }

        // ═════════════════════════════════════════════════════════════════════
        // این امضای قدیمی (فاز ۷) دست‌نخورده می‌ماند: با «نام فایل» آدرس‌دهی
        // می‌کند و سرور فایل‌ها را با هویت سراسری می‌شناسد. مسیرِ واقعیِ دریافت
        // DownloadToFile است.
        public FileTransferResult DownloadFile(string entityGlobalId, string fileName,
                                               string targetPath, CancellationToken cancel)
        {
            if (!IsConfigured) return Failure(OfflineSyncTransport.OfflineMessage);

            return Failure("دریافت با نام فایل پشتیبانی نمی‌شود — از DownloadToFile استفاده کنید.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // فاز B — فهرستِ افزایشیِ سرور
        // ═════════════════════════════════════════════════════════════════════
        public FileManifestResult GetManifest(long cursor, int max, CancellationToken cancel)
        {
            var result = new FileManifestResult();

            if (!IsConfigured)
                throw new SyncOfflineException(OfflineSyncTransport.OfflineMessage);

            string token = _auth.AcquireAccessToken(cancel);
            if (token == null)
                throw new SyncOfflineException("احراز هویت با سرور ممکن نشد.");

            string body = Send("GET",
                "/api/v1/files/manifest?cursor=" + (cursor < 0 ? 0 : cursor)
                + "&max=" + (max <= 0 ? 100 : max)
                + "&deviceGuid=" + HttpSyncTransport.DeviceGuid,
                null, token, cancel);

            Dictionary<string, object> response = Deserialize(body);

            result.Succeeded    = ReadBool(response, "succeeded");
            result.ErrorMessage = ReadString(response, "errorMessage");
            result.NextCursor   = ReadLong(response, "nextCursor");
            result.HasMore      = ReadBool(response, "hasMore");

            object filesRaw;
            if (response.TryGetValue("files", out filesRaw))
            {
                var list = filesRaw as System.Collections.IEnumerable;
                if (list != null)
                {
                    foreach (object item in list)
                    {
                        var map = item as Dictionary<string, object>;
                        if (map == null) continue;

                        result.Files.Add(new RemoteFileEntry
                        {
                            FileGlobalId   = ReadString(map, "fileGlobalId"),
                            EntityName     = ReadString(map, "entityName"),
                            EntityGlobalId = ReadString(map, "entityGlobalId"),
                            ColumnName     = ReadString(map, "columnName"),
                            FileName       = ReadString(map, "fileName"),
                            FileType       = ReadString(map, "fileType"),
                            ContentHash    = ReadString(map, "contentHash"),
                            SizeBytes      = ReadLong(map, "sizeBytes"),
                            FileVersion    = ReadInt(map, "fileVersion"),
                            Deleted        = ReadBool(map, "deleted"),
                            Cursor         = ReadLong(map, "cursor")
                        });
                    }
                }
            }

            return result;
        }

        // ═════════════════════════════════════════════════════════════════════
        // فاز B — دریافتِ محتوا با ازسرگیری
        //
        // ازسرگیری با سرآیندِ استانداردِ Range انجام می‌شود: اگر فایلِ مرحله‌ای
        // از دفعهٔ قبل مانده باشد، فقط بایت‌های باقی‌مانده خواسته می‌شوند.
        //
        // ⚠ سه قاعده:
        //   ۱. هرگز کل فایل در حافظه نمی‌آید — جریانی نوشته می‌شود.
        //   ۲. اگر سرور Range را نپذیرد (پاسخ ۲۰۰ به‌جای ۲۰۶) فایلِ مرحله‌ای
        //      از صفر بازنویسی می‌شود؛ چسباندنِ پاسخِ کامل به انتهای فایلِ ناقص
        //      یک فایلِ خرابِ *بی‌صدا* می‌ساخت که فقط با هش لو می‌رفت.
        //   ۳. هش پس از کامل شدن سنجیده می‌شود و فایلِ ناموفق پاک می‌گردد تا
        //      دفعهٔ بعد از نو و درست شروع شود.
        // ═════════════════════════════════════════════════════════════════════
        public FileTransferResult DownloadToFile(string fileGlobalId, string stagingPath,
                                                 string expectedHash, long expectedSize,
                                                 CancellationToken cancel)
        {
            if (!IsConfigured) return Failure(OfflineSyncTransport.OfflineMessage);

            if (string.IsNullOrWhiteSpace(fileGlobalId) || string.IsNullOrWhiteSpace(stagingPath))
                return Failure("اطلاعات دریافت ناقص است.");

            try
            {
                string token = _auth.AcquireAccessToken(cancel);
                if (token == null)
                    throw new SyncOfflineException("احراز هویت با سرور ممکن نشد.");

                try { Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)); }
                catch (Exception ex) { return Failure("ساخت پوشهٔ دریافت ممکن نشد: " + ex.Message); }

                long resumeFrom = 0;
                try
                {
                    var partial = new FileInfo(stagingPath);
                    if (partial.Exists) resumeFrom = partial.Length;
                }
                catch { resumeFrom = 0; }

                // فایلِ مرحله‌ایِ بزرگ‌تر از اندازهٔ اعلام‌شده یعنی بازمانده‌ای از
                // نسخهٔ قبلیِ همین فایل — از نو شروع می‌شود.
                if (expectedSize > 0 && resumeFrom >= expectedSize)
                {
                    TryDelete(stagingPath);
                    resumeFrom = 0;
                }

                long received = Fetch(fileGlobalId, stagingPath, resumeFrom, token, cancel);

                // ── تأیید هش ──
                string actual = SyncFileService.ComputeHash(stagingPath);

                if (actual == null)
                    return Failure("خواندن فایل دریافتی ممکن نشد.");

                if (!string.IsNullOrEmpty(expectedHash) &&
                    !string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    // ⚠ فایلِ خراب روی دیسک نمی‌ماند: اگر می‌ماند، تلاش بعدی
                    // آن را «شروعِ معتبر برای ازسرگیری» می‌دید و خرابی جاودانه
                    // می‌شد.
                    TryDelete(stagingPath);

                    return new FileTransferResult
                    {
                        Succeeded = false,
                        Corrupted = true,
                        ContentHash = actual,
                        ErrorMessage = "هشِ فایل دریافتی نمی‌خواند — فایل خراب یا ناقص است."
                    };
                }

                return new FileTransferResult
                {
                    Succeeded = true,
                    ContentHash = actual,
                    BytesTransferred = received
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (SyncOfflineException) { throw; }
            catch (SyncServerException ex)
            {
                // ۴۰۳/۴۰۴ ⇒ اجازه نیست یا وجود ندارد. فایلِ مرحله‌ای بی‌معناست.
                bool denied = ex.StatusCode == 403 || ex.StatusCode == 404 || ex.StatusCode == 401;
                if (denied) TryDelete(stagingPath);

                return new FileTransferResult
                {
                    Succeeded = false,
                    AccessDenied = denied,
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                // ⚠ فایلِ نیمه‌دانلود عمداً *نگه داشته* می‌شود: همان چیزی است
                // که تلاش بعدی از آن ادامه می‌دهد.
                return Failure(ex.Message);
            }
        }

        // یک تلاشِ دریافت. مقدار بازگشتی: بایت‌های نوشته‌شده در این تلاش.
        private long Fetch(string fileGlobalId, string stagingPath, long resumeFrom,
                           string token, CancellationToken cancel)
        {
            HttpWebRequest request = Create("GET", "/api/v1/files/" + fileGlobalId, token);
            request.Accept = "application/octet-stream";

            if (resumeFrom > 0)
                request.AddRange(resumeFrom);

            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                var errorResponse = ex.Response as HttpWebResponse;
                if (errorResponse != null)
                {
                    int status = (int)errorResponse.StatusCode;

                    // ۴۱۶ یعنی درخواستِ ازسرگیری بی‌معنا بود (فایل سمت سرور
                    // عوض شده). فایلِ مرحله‌ای دور ریخته و از صفر شروع می‌شود.
                    if (status == 416 && resumeFrom > 0)
                    {
                        errorResponse.Close();
                        TryDelete(stagingPath);
                        return Fetch(fileGlobalId, stagingPath, 0, token, cancel);
                    }

                    errorResponse.Close();
                    throw new SyncServerException("سرور پاسخ " + status + " داد.", status);
                }

                throw Offline(ex);
            }

            using (response)
            using (Stream source = response.GetResponseStream())
            {
                // ⚠ اگر ازسرگیری خواسته شد ولی سرور کلِ فایل را فرستاد
                // (۲۰۰ به‌جای ۲۰۶)، باید از صفر نوشت.
                bool append = resumeFrom > 0 && (int)response.StatusCode == 206;

                using (var target = new FileStream(stagingPath,
                           append ? FileMode.Append : FileMode.Create,
                           FileAccess.Write, FileShare.None, ChunkSize))
                {
                    var buffer = new byte[ChunkSize];
                    long written = 0;

                    while (true)
                    {
                        cancel.ThrowIfCancellationRequested();

                        int read = source.Read(buffer, 0, buffer.Length);
                        if (read <= 0) break;

                        target.Write(buffer, 0, read);
                        written += read;
                    }

                    return written;
                }
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // ═════════════════════════════════════════════════════════════════════
        private static string FileTypeOf(FileSyncItem item)
        {
            string extension = "";
            try { extension = Path.GetExtension(item.LocalPath ?? "") ?? ""; } catch { }

            switch (extension.ToLowerInvariant())
            {
                case ".pdf": case ".doc": case ".docx":
                case ".xls": case ".xlsx": case ".txt": case ".rtf":
                    return "document";
                default:
                    return "photo";
            }
        }

        private string Send(string method, string path, string jsonBody, string token,
                            CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            HttpWebRequest request = Create(method, path, token);

            if (jsonBody != null)
            {
                byte[] payload = Encoding.UTF8.GetBytes(jsonBody);
                request.ContentType = "application/json; charset=utf-8";
                request.ContentLength = payload.Length;

                try
                {
                    using (Stream stream = request.GetRequestStream())
                        stream.Write(payload, 0, payload.Length);
                }
                catch (WebException ex) { throw Offline(ex); }
            }

            return ReadResponse(request);
        }

        private string SendBinary(string path, byte[] buffer, int count, string token,
                                  CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            HttpWebRequest request = Create("PUT", path, token);
            request.ContentType = "application/octet-stream";
            request.ContentLength = count;
            request.AllowWriteStreamBuffering = false;   // فایل بزرگ در حافظه بافر نشود

            try
            {
                using (Stream stream = request.GetRequestStream())
                    stream.Write(buffer, 0, count);
            }
            catch (WebException ex) { throw Offline(ex); }

            return ReadResponse(request);
        }

        private HttpWebRequest Create(string method, string path, string token)
        {
            HttpWebRequest request;
            try { request = (HttpWebRequest)WebRequest.Create(_baseUrl + path); }
            catch (Exception ex) { throw new SyncOfflineException("آدرس سرور نامعتبر است: " + ex.Message); }

            request.Method = method;
            request.Timeout = _timeoutMs;
            request.ReadWriteTimeout = _timeoutMs;
            request.Accept = "application/json";
            request.UserAgent = "CaseManagement-FileSync";
            request.KeepAlive = false;

            if (!string.IsNullOrEmpty(token))
                request.Headers["Authorization"] = "Bearer " + token;

            return request;
        }

        private static string ReadResponse(HttpWebRequest request)
        {
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                var errorResponse = ex.Response as HttpWebResponse;
                if (errorResponse != null)
                {
                    // پاسخِ خطادارِ سرور آفلاین نیست — بدنه‌اش برگردانده
                    // می‌شود تا پیام واقعی به کاربر برسد.
                    try
                    {
                        using (Stream stream = errorResponse.GetResponseStream())
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            string body = reader.ReadToEnd();
                            if (!string.IsNullOrEmpty(body)) return body;
                        }
                    }
                    catch { }

                    throw new SyncServerException(
                        "سرور پاسخ " + (int)errorResponse.StatusCode + " داد.",
                        (int)errorResponse.StatusCode);
                }

                throw Offline(ex);
            }
        }

        private static SyncOfflineException Offline(WebException ex)
        {
            return new SyncOfflineException("سرور در دسترس نیست: " + FriendlyStatus(ex.Status));
        }

        // آموزش — همان رفعِ نشتِ پیامِ انگلیسیِ HttpSyncTransport.FriendlyStatus،
        // اینجا هم تکرار شده (نه به یک کلاسِ مشترک منتقل شده) تا کوچک‌ترین
        // تغییرِ ممکن باشد و هیچ کلاسِ دیگری لمس نشود.
        private static string FriendlyStatus(WebExceptionStatus status)
        {
            switch (status)
            {
                case WebExceptionStatus.Timeout:
                case WebExceptionStatus.SendFailure:
                case WebExceptionStatus.ReceiveFailure:
                    return "پاسخ سرور به‌موقع نرسید (Timeout).";
                case WebExceptionStatus.ConnectFailure:
                    return "اتصال به سرور برقرار نشد.";
                case WebExceptionStatus.NameResolutionFailure:
                    return "آدرس سرور پیدا نشد. اتصال اینترنت یا تنظیمات آدرس سرور را بررسی کنید.";
                case WebExceptionStatus.TrustFailure:
                case WebExceptionStatus.SecureChannelFailure:
                    return "برقراری ارتباط امن (SSL) با سرور ناموفق بود.";
                case WebExceptionStatus.ConnectionClosed:
                    return "اتصال به سرور قطع شد.";
                default:
                    return status.ToString();
            }
        }

        private static FileTransferResult Failure(string message)
        {
            return new FileTransferResult { Succeeded = false, ErrorMessage = message };
        }

        // ─── JSON ───
        private static string Serialize(object value)
        {
            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            return serializer.Serialize(value);
        }

        private static Dictionary<string, object> Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object>();

            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;

            try
            {
                return serializer.Deserialize<Dictionary<string, object>>(json)
                       ?? new Dictionary<string, object>();
            }
            catch { return new Dictionary<string, object>(); }
        }

        private static string ReadString(Dictionary<string, object> map, string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value) : null;
        }

        private static bool ReadBool(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) return false;
            try { return Convert.ToBoolean(value); } catch { return false; }
        }

        private static int ReadInt(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) return 0;
            try { return Convert.ToInt32(value); } catch { return 0; }
        }

        private static long ReadLong(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) return 0;
            try { return Convert.ToInt64(value); } catch { return 0; }
        }
    }
}

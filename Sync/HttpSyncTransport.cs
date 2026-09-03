using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // اتصال واقعی به سرور همگام‌سازی — فاز ۶.۲.
    //
    // آموزش — چرا HttpWebRequest و JavaScriptSerializer و نه کتابخانه‌های
    // مدرن‌تر: برنامه روی .NET Framework 4.7.2 است و قاعدهٔ پروژه «افزودن
    // کتابخانهٔ غیرضروری ممنوع» است. هر دو مورد از قبل در ارجاع‌های پروژه
    // هستند (System.Net.Http و System.Web.Extensions)، پس این کلاس *هیچ*
    // وابستگی تازه‌ای اضافه نمی‌کند. HttpWebRequest ذاتاً همگام است و با
    // امضای همگامِ ISyncTransport جور درمی‌آید بدون ریسکِ بن‌بستِ
    // sync-over-async.
    //
    // ⚠ قاعدهٔ حاکم: نبودِ سرور یک «خطا» نیست.
    // اگر شبکه یا سرور در دسترس نباشد، این کلاس SyncOfflineException پرتاب
    // می‌کند و SyncService صف را دست‌نخورده رها می‌کند — نه شمارندهٔ تلاش
    // بالا می‌رود و نه چیزی «ارسال‌شده» علامت می‌خورد. برنامه دقیقاً مثل
    // قبل آفلاین کار می‌کند.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class HttpSyncTransport : ISyncTransport
    {
        // کلیدهای پیکربندی در SyncState (همان جدول کلید/مقدارِ فاز ۲).
        public const string KeyServerUrl    = "ServerUrl";
        public const string KeyDeviceGuid   = "DeviceGuid";
        public const string KeyRefreshToken = "RefreshToken";

        private const int DefaultTimeoutMs = 30000;

        private readonly string _baseUrl;
        private readonly int _timeoutMs;

        // توکن دسترسی فقط در حافظه می‌ماند: عمرش ۱۵ دقیقه است و نوشتنش روی
        // دیسک فقط سطح حمله را بزرگ می‌کند.
        private string _accessToken;
        private DateTime _accessExpiresAt;

        public HttpSyncTransport() : this(SyncOutboxService.GetState(KeyServerUrl, ""), DefaultTimeoutMs) { }

        public HttpSyncTransport(string baseUrl, int timeoutMs = DefaultTimeoutMs)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _timeoutMs = timeoutMs <= 0 ? DefaultTimeoutMs : timeoutMs;
        }

        public string Name { get { return "سرور همگام‌سازی"; } }

        public bool IsConfigured { get { return !string.IsNullOrWhiteSpace(_baseUrl); } }

        // شناسهٔ دستگاه یک بار ساخته و برای همیشه نگه داشته می‌شود.
        public static Guid DeviceGuid
        {
            get
            {
                string stored = SyncOutboxService.GetState(KeyDeviceGuid, "");

                Guid guid;
                if (Guid.TryParse(stored, out guid) && guid != Guid.Empty) return guid;

                guid = Guid.NewGuid();
                SyncOutboxService.SetState(KeyDeviceGuid, guid.ToString());
                return guid;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        public SyncConnectionStatus GetStatus(CancellationToken cancel)
        {
            if (!IsConfigured)
                return SyncConnectionStatus.NotConfigured(
                    "آدرس سرور تنظیم نشده است — برنامه آفلاین کار می‌کند.");

            try
            {
                string body = Send("GET", "/api/v1/health", null, false, cancel);

                var health = Deserialize(body);
                bool healthy = ReadBool(health, "healthy");

                if (!healthy)
                    return new SyncConnectionStatus
                    {
                        State = SyncConnectionState.Offline,
                        Detail = "سرور در دسترس است ولی سالم نیست."
                    };

                // سرور سالم است؛ حالا احراز هویت بررسی می‌شود.
                if (!EnsureAccessToken(cancel))
                    return new SyncConnectionStatus
                    {
                        State = SyncConnectionState.Unauthorized,
                        Detail = "ورود لازم است."
                    };

                return new SyncConnectionStatus
                {
                    State = SyncConnectionState.Online,
                    Detail = "متصل به " + _baseUrl
                };
            }
            catch (SyncOfflineException ex)
            {
                // ⚠ این مسیرِ عادیِ کار آفلاین است، نه خطا.
                return new SyncConnectionStatus
                {
                    State = SyncConnectionState.Offline,
                    Detail = ex.Message
                };
            }
            catch (Exception ex)
            {
                return new SyncConnectionStatus
                {
                    State = SyncConnectionState.Offline,
                    Detail = ex.Message
                };
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ورود اولیه — یک بار، با اعتبارنامهٔ کاربر. پس از آن فقط توکن
        // تازه‌سازی نگه داشته می‌شود؛ رمز عبور هرگز ذخیره نمی‌شود.
        // ─────────────────────────────────────────────────────────────────────
        public SyncAuthResult Login(string username, string password, CancellationToken cancel)
        {
            try
            {
                var request = new Dictionary<string, object>
                {
                    { "username",    username ?? "" },
                    { "password",    password ?? "" },
                    { "deviceGuid",  DeviceGuid.ToString() },
                    { "machineName", SafeMachineName() }
                };

                string body = Send("POST", "/api/v1/auth/login", Serialize(request), false, cancel);

                return StoreTokens(body);
            }
            catch (SyncOfflineException ex)
            {
                return new SyncAuthResult { Succeeded = false, ErrorMessage = ex.Message };
            }
            catch (Exception ex)
            {
                return new SyncAuthResult { Succeeded = false, ErrorMessage = ex.Message };
            }
        }

        public SyncAuthResult Authenticate(CancellationToken cancel)
        {
            string refreshToken = SyncOutboxService.GetState(KeyRefreshToken, "");

            if (string.IsNullOrWhiteSpace(refreshToken))
                return new SyncAuthResult
                {
                    Succeeded = false,
                    ErrorMessage = "هنوز واردِ سرور نشده‌اید."
                };

            try
            {
                var request = new Dictionary<string, object>
                {
                    { "refreshToken", refreshToken },
                    { "deviceGuid",   DeviceGuid.ToString() }
                };

                string body = Send("POST", "/api/v1/auth/refresh", Serialize(request), false, cancel);

                return StoreTokens(body);
            }
            catch (SyncOfflineException) { throw; }
            catch (Exception ex)
            {
                return new SyncAuthResult { Succeeded = false, ErrorMessage = ex.Message };
            }
        }

        private SyncAuthResult StoreTokens(string body)
        {
            Dictionary<string, object> response = Deserialize(body);

            bool ok = ReadBool(response, "succeeded");
            if (!ok)
                return new SyncAuthResult
                {
                    Succeeded = false,
                    ErrorMessage = ReadString(response, "errorMessage")
                };

            _accessToken = ReadString(response, "accessToken");

            DateTime expires;
            _accessExpiresAt = DateTime.TryParse(ReadString(response, "expiresAt"), out expires)
                ? expires.ToUniversalTime()
                : DateTime.UtcNow.AddMinutes(10);

            string refresh = ReadString(response, "refreshToken");
            if (!string.IsNullOrWhiteSpace(refresh))
                SyncOutboxService.SetState(KeyRefreshToken, refresh);

            return new SyncAuthResult
            {
                Succeeded = true,
                Token = _accessToken,
                ExpiresAt = _accessExpiresAt
            };
        }

        // توکنِ معتبر برای انتقالِ فایل — تا نشستِ رکوردها و فایل‌ها یکی
        // باشد و منطق ورود/تازه‌سازی در دو جا تکرار نشود.
        // null یعنی احراز هویت ممکن نشد.
        internal string AcquireAccessToken(CancellationToken cancel)
        {
            return EnsureAccessToken(cancel) ? _accessToken : null;
        }

        // توکن با یک دقیقه حاشیه پیش از انقضا تازه می‌شود تا درخواستی وسط
        // راه با ۴۰۱ برنگردد.
        private bool EnsureAccessToken(CancellationToken cancel)
        {
            if (!string.IsNullOrEmpty(_accessToken) &&
                _accessExpiresAt > DateTime.UtcNow.AddMinutes(1))
                return true;

            return Authenticate(cancel).Succeeded;
        }

        // ─────────────────────────────────────────────────────────────────────
        public SyncPushResult Push(IList<SyncChange> batch, CancellationToken cancel)
        {
            var result = new SyncPushResult();

            if (batch == null || batch.Count == 0)
            {
                result.Succeeded = true;
                return result;
            }

            if (!EnsureAccessToken(cancel))
            {
                result.Succeeded = false;
                result.ErrorMessage = "احراز هویت ناموفق بود.";
                return result;
            }

            var changes = new List<object>();
            foreach (SyncChange change in batch)
                changes.Add(new Dictionary<string, object>
                {
                    { "outboxId",       change.OutboxId },
                    { "entityName",     change.EntityName },
                    { "globalId",       change.GlobalId },
                    { "parentGlobalId", change.ParentGlobalId },
                    { "operationType",  change.OperationType },
                    { "rowVersion",     change.RowVersion },
                    { "payload",        change.Payload },
                    { "centerId",       change.CenterId },
                    { "username",       change.Username },
                    { "machineName",    change.MachineName },
                    { "occurredAt",     change.OccurredAt }
                });

            var request = new Dictionary<string, object>
            {
                { "deviceGuid", DeviceGuid.ToString() },
                { "changes",    changes }
            };

            string body = Send("POST", "/api/v1/sync/push", Serialize(request), true, cancel);

            Dictionary<string, object> response = Deserialize(body);

            result.Succeeded = ReadBool(response, "succeeded");
            result.ErrorMessage = ReadString(response, "errorMessage");

            object itemsRaw;
            if (response.TryGetValue("items", out itemsRaw))
            {
                var items = itemsRaw as Dictionary<string, object>;
                if (items != null)
                {
                    foreach (KeyValuePair<string, object> pair in items)
                    {
                        long outboxId;
                        if (!long.TryParse(pair.Key, out outboxId)) continue;

                        var outcome = pair.Value as Dictionary<string, object>;
                        if (outcome == null) continue;

                        result.Items[outboxId] = new SyncItemOutcome
                        {
                            State         = ParseState(ReadString(outcome, "state")),
                            Message       = ReadString(outcome, "message"),
                            RemoteVersion = ReadInt(outcome, "remoteVersion"),
                            RemotePayload = ReadString(outcome, "remotePayload")
                        };
                    }
                }
            }

            return result;
        }

        public SyncPullResult Pull(string cursor, int maxItems, CancellationToken cancel)
        {
            var result = new SyncPullResult();

            if (!EnsureAccessToken(cancel))
            {
                result.Succeeded = false;
                result.ErrorMessage = "احراز هویت ناموفق بود.";
                return result;
            }

            string path = "/api/v1/sync/pull?cursor=" + Uri.EscapeDataString(cursor ?? "0")
                        + "&max=" + maxItems
                        + "&deviceGuid=" + DeviceGuid;

            string body = Send("GET", path, null, true, cancel);

            Dictionary<string, object> response = Deserialize(body);

            result.Succeeded  = ReadBool(response, "succeeded");
            result.ErrorMessage = ReadString(response, "errorMessage");
            result.NextCursor = ReadString(response, "nextCursor");
            result.HasMore    = ReadBool(response, "hasMore");

            object changesRaw;
            if (response.TryGetValue("changes", out changesRaw))
            {
                var list = changesRaw as System.Collections.IEnumerable;
                if (list != null)
                {
                    foreach (object item in list)
                    {
                        var map = item as Dictionary<string, object>;
                        if (map == null) continue;

                        result.Changes.Add(new SyncChange
                        {
                            EntityName     = ReadString(map, "entityName"),
                            GlobalId       = ReadString(map, "globalId"),
                            ParentGlobalId = ReadString(map, "parentGlobalId"),
                            OperationType  = ReadString(map, "operationType"),
                            RowVersion     = ReadInt(map, "rowVersion"),
                            Payload        = ReadString(map, "payload"),
                            CenterId       = ReadInt(map, "centerId"),
                            Username       = ReadString(map, "username"),
                            OccurredAt     = ReadString(map, "occurredAt"),
                            Cursor         = ReadString(map, "cursor")
                        });
                    }
                }
            }

            return result;
        }

        private static SyncItemState ParseState(string state)
        {
            if (string.Equals(state, "Conflict", StringComparison.OrdinalIgnoreCase))
                return SyncItemState.Conflict;

            if (string.Equals(state, "Rejected", StringComparison.OrdinalIgnoreCase))
                return SyncItemState.Rejected;

            return SyncItemState.Accepted;
        }

        // ═════════════════════════════════════════════════════════════════════
        // لایهٔ HTTP
        // ═════════════════════════════════════════════════════════════════════
        private string Send(string method, string path, string jsonBody, bool authenticated,
                            CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            if (!IsConfigured)
                throw new SyncOfflineException("آدرس سرور تنظیم نشده است.");

            HttpWebRequest request;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(_baseUrl + path);
            }
            catch (Exception ex)
            {
                throw new SyncOfflineException("آدرس سرور نامعتبر است: " + ex.Message);
            }

            request.Method = method;
            request.Timeout = _timeoutMs;
            request.ReadWriteTimeout = _timeoutMs;
            request.Accept = "application/json";
            request.UserAgent = "CaseManagement-Sync";
            request.KeepAlive = false;

            if (authenticated && !string.IsNullOrEmpty(_accessToken))
                request.Headers["Authorization"] = "Bearer " + _accessToken;

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
                catch (WebException ex)
                {
                    throw Offline(ex);
                }
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                // پاسخِ خطادارِ سرور (۴۰۱/۴۰۳/۵۰۰) *آفلاین نیست* — بدنه‌اش
                // خوانده و به فراخوان داده می‌شود تا تصمیم درست بگیرد.
                if (ex.Response is HttpWebResponse errorResponse)
                {
                    string body = "";
                    try
                    {
                        using (Stream stream = errorResponse.GetResponseStream())
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                            body = reader.ReadToEnd();
                    }
                    catch { }

                    // توکن منقضی ⇒ یک بار تازه‌سازی و تلاش دوباره.
                    if (errorResponse.StatusCode == HttpStatusCode.Unauthorized && authenticated)
                    {
                        _accessToken = null;
                        if (Authenticate(cancel).Succeeded)
                            return Send(method, path, jsonBody, true, cancel);
                    }

                    if (!string.IsNullOrEmpty(body)) return body;

                    throw new SyncServerException(
                        "سرور پاسخ " + (int)errorResponse.StatusCode + " داد.",
                        (int)errorResponse.StatusCode);
                }

                throw Offline(ex);
            }
        }

        // تشخیصِ «شبکه/سرور در دسترس نیست» از «سرور جواب داد ولی خطا بود».
        // این تفکیک همان چیزی است که تضمین می‌کند کار آفلاین، صف را خراب نکند.
        private static SyncOfflineException Offline(WebException ex)
        {
            return new SyncOfflineException("سرور در دسترس نیست: " + FriendlyStatus(ex.Status));
        }

        // آموزش — رفع نشتِ پیام انگلیسی: قبلاً اینجا ex.Status.ToString() مستقیم
        // به کاربر نشان داده می‌شد (مثلاً "Timeout"، "NameResolutionFailure")،
        // که با «همه پیام‌های همگام‌سازی باید فارسی/کاربرپسند باشند» ناسازگار
        // بود. رایج‌ترین حالت‌ها فارسی می‌شوند؛ بقیه (نادر) به نام فنی خودشان
        // برمی‌گردند تا هیچ اطلاعاتی گم نشود.
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
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private static string ReadString(Dictionary<string, object> map, string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value)
                : null;
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

        private static string SafeMachineName()
        {
            try { return Environment.MachineName; } catch { return ""; }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // «سرور در دسترس نیست» — یک حالتِ عادی، نه شکست.
    // SyncService با دیدن این استثنا صف را دست‌نخورده رها می‌کند.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class SyncOfflineException : Exception
    {
        public SyncOfflineException(string message) : base(message) { }
    }

    // سرور پاسخ داد ولی با خطا — این یک شکستِ واقعی است و شمارندهٔ تلاش
    // باید بالا برود.
    public sealed class SyncServerException : Exception
    {
        public int StatusCode { get; private set; }

        public SyncServerException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}

using System;
using System.Collections.Generic;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // مدل‌های لایهٔ انتقال — فاز ۳.
    //
    // آموزش — چرا این مدل‌ها از SyncModels.cs جدا هستند: آن فایل مدل‌های
    // «همگام‌سازی از روی فایل» (Provider → Comparer → Engine) را دارد که یک
    // جریانِ کاملاً متفاوت است: ورودی‌اش فایل HTML است، کاربر در Wizard
    // تک‌تک تغییرات را تأیید می‌کند و هیچ حذفی انجام نمی‌شود. جریانِ فاز ۳
    // خودکار، دوطرفه و مبتنی بر هویت سراسری است. یکی‌کردنشان یعنی هر دو
    // پیچیده و شکننده می‌شدند. در عوض هر دو از یک نوعِ گزارش پیشرفت
    // (SyncProgress) استفاده می‌کنند تا رابط کاربری یکسان بماند.
    // ═════════════════════════════════════════════════════════════════════════

    // وضعیت اتصال به سرور.
    public enum SyncConnectionState
    {
        NotConfigured,   // هیچ سروری تعریف نشده (حالت پیش‌فرضِ امروز)
        Offline,         // تعریف شده ولی در دسترس نیست
        Unauthorized,    // در دسترس است ولی احراز هویت نشده
        Online
    }

    public sealed class SyncConnectionStatus
    {
        public SyncConnectionState State { get; set; }
        public string Detail { get; set; }

        public bool CanSynchronize { get { return State == SyncConnectionState.Online; } }

        public static SyncConnectionStatus NotConfigured(string detail)
        {
            return new SyncConnectionStatus
            {
                State = SyncConnectionState.NotConfigured,
                Detail = detail ?? "سرور همگام‌سازی پیکربندی نشده است."
            };
        }

        public string DisplayText
        {
            get
            {
                switch (State)
                {
                    case SyncConnectionState.Online:       return "متصل";
                    case SyncConnectionState.Unauthorized: return "احراز هویت نشده";
                    case SyncConnectionState.Offline:      return "آفلاین";
                    default:                               return "پیکربندی نشده";
                }
            }
        }
    }

    // یک تغییر قابل تبادل. عمداً همان قالبِ SyncOutbox است تا تبدیل بین صف و
    // شبکه بدون نگاشتِ اضافی انجام شود.
    public sealed class SyncChange
    {
        public long   OutboxId { get; set; }        // فقط سمت ارسال — کلید idempotency
        public string EntityName { get; set; }
        public string GlobalId { get; set; }
        public string ParentGlobalId { get; set; }
        public string OperationType { get; set; }
        public int    RowVersion { get; set; }
        public string Payload { get; set; }         // «کلید=مقدار» در هر خط
        public int    CenterId { get; set; }
        public string Username { get; set; }
        public string MachineName { get; set; }
        public string OccurredAt { get; set; }

        // نشانگر ترتیبِ سرور (فقط سمت دریافت).
        public string Cursor { get; set; }
    }

    // نتیجهٔ ارسال یک دسته. سرور برای هر قلم جداگانه پاسخ می‌دهد، چون ممکن
    // است بعضی بپذیرند و بعضی تعارض داشته باشند.
    public sealed class SyncPushResult
    {
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; }

        // OutboxID → نتیجهٔ همان قلم
        public Dictionary<long, SyncItemOutcome> Items { get; set; }

        public SyncPushResult()
        {
            Items = new Dictionary<long, SyncItemOutcome>();
        }
    }

    public enum SyncItemState
    {
        Accepted,
        Conflict,
        Rejected
    }

    public sealed class SyncItemOutcome
    {
        public SyncItemState State { get; set; }
        public string Message { get; set; }

        // در حالت تعارض، نسخهٔ سرور برای ثبت در SyncConflict.
        public int    RemoteVersion { get; set; }
        public string RemotePayload { get; set; }
    }

    public sealed class SyncPullResult
    {
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; }
        public List<SyncChange> Changes { get; set; }

        // نشانگر بعدی برای ادامهٔ دریافت (پایهٔ «ازسرگیری»).
        public string NextCursor { get; set; }
        public bool HasMore { get; set; }

        public SyncPullResult()
        {
            Changes = new List<SyncChange>();
        }
    }

    // احراز هویت — فعلاً فقط قرارداد. پیاده‌سازی واقعی همراه سرور می‌آید.
    public sealed class SyncAuthResult
    {
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; }
        public string Token { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    // ─── نتیجهٔ یک اجرای کاملِ همگام‌سازی ────────────────────────────────────
    public sealed class SyncRunResult
    {
        public bool Completed { get; set; }
        public bool Cancelled { get; set; }
        public bool Skipped { get; set; }          // آفلاین/پیکربندی‌نشده
        public string Message { get; set; }

        public int Uploaded { get; set; }
        public int UploadFailed { get; set; }
        public int Downloaded { get; set; }
        public int DownloadSkipped { get; set; }
        public int Conflicts { get; set; }

        // فاز ۷ — پیوست‌ها
        public int FilesUploaded { get; set; }
        public int FilesFailed { get; set; }
        public int FilesSkipped { get; set; }

        // فاز B — سمتِ دریافت
        public int FilesDownloaded { get; set; }
        public int FilesDownloadFailed { get; set; }
        public int FilesDownloadSkipped { get; set; }

        public TimeSpan Duration { get; set; }

        public string Summary
        {
            get
            {
                if (Skipped) return Message;

                return "ارسال‌شده: " + Uploaded
                     + " | ناموفق: " + UploadFailed
                     + " | دریافت‌شده: " + Downloaded
                     + " | فایل ارسالی: " + FilesUploaded + "/" + (FilesUploaded + FilesFailed)
                     + " | فایل دریافتی: " + FilesDownloaded + "/" + (FilesDownloaded + FilesDownloadFailed)
                     + " | تعارض: " + Conflicts
                     + (Cancelled ? " (لغو شد)" : "");
            }
        }
    }
}

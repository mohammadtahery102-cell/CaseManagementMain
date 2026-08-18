using System.Collections.Generic;
using System.Threading;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // قرارداد لایهٔ انتقال — فاز ۳ و آماده‌سازی فاز ۶.
    //
    // آموزش — چرا این interface و نه استفادهٔ مستقیم از IDataSyncProvider:
    // IDataSyncProvider قرارداد «خواندن و اعتبارسنجی یک منبعِ ورودی» است
    // (Parse/Validate روی فایل) و عمداً هیچ مفهومی از ارسال، احراز هویت،
    // نشانگر ادامه یا وضعیت اتصال ندارد. یک انتقالِ دوطرفهٔ شبکه‌ای هر چهار
    // را لازم دارد. تحمیل آن‌ها به interface موجود یعنی شکستنِ HtmlSyncProvider
    // که امروز کار می‌کند. پس این قرارداد *کنارِ* آن می‌نشیند، نه به‌جایش؛ و
    // هر دو از همان SyncProgress برای گزارش پیشرفت استفاده می‌کنند.
    //
    // ⚠ هیچ پیاده‌سازیِ آنلاینی در این فاز ساخته نمی‌شود. تنها پیاده‌سازیِ
    // موجود (OfflineSyncTransport) صادقانه اعلام می‌کند که سروری وجود ندارد.
    // ═════════════════════════════════════════════════════════════════════════
    public interface ISyncTransport
    {
        // نام نمایشی (برای مرکز کنترل و لاگ).
        string Name { get; }

        // آیا اصلاً مقصدی تعریف شده است؟ بدون این، تلاش برای اتصال بی‌معناست.
        bool IsConfigured { get; }

        // وضعیت لحظه‌ای اتصال. باید سریع و بدون عارضه باشد؛ SyncService پیش
        // از هر کاری این را می‌پرسد تا در حالت آفلاین صف را دست نزند.
        SyncConnectionStatus GetStatus(CancellationToken cancel);

        // جای‌نگه‌دار احراز هویت. پیاده‌سازی واقعی همراه سرور می‌آید.
        SyncAuthResult Authenticate(CancellationToken cancel);

        // ارسال یک دسته تغییر. سرور باید بر اساس (شناسهٔ دستگاه + OutboxId)
        // تکراری‌ها را تشخیص دهد تا ارسالِ دوباره پس از قطعی، رکورد تکراری
        // نسازد.
        SyncPushResult Push(IList<SyncChange> batch, CancellationToken cancel);

        // دریافت تغییرات پس از نشانگر داده‌شده.
        SyncPullResult Pull(string cursor, int maxItems, CancellationToken cancel);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // قرارداد همگام‌سازی فایل — فقط آماده‌سازی (فاز ۷).
    //
    // عمداً پیاده‌سازی نمی‌شود. اینجا فقط شکلِ قرارداد تثبیت می‌شود تا لایه‌های
    // بالاتر بتوانند رویش حساب کنند: هر فایل با هویت سراسریِ رکوردِ صاحبش،
    // هشِ محتوا و شمارهٔ نسخه شناخته می‌شود — نه با مسیر محلی که در ماشین
    // دیگر بی‌معناست.
    // ═════════════════════════════════════════════════════════════════════════
    public interface IFileSyncTransport
    {
        bool IsConfigured { get; }

        FileTransferResult UploadFile(FileSyncItem item, CancellationToken cancel);

        FileTransferResult DownloadFile(string entityGlobalId, string fileName,
                                        string targetPath, CancellationToken cancel);
    }

    // توصیف یک فایل در چرخهٔ همگام‌سازی.
    public sealed class FileSyncItem
    {
        public string EntityName { get; set; }
        public string EntityGlobalId { get; set; }
        public string FileName { get; set; }
        public string LocalPath { get; set; }

        // هشِ محتوا — کلید تشخیص «همان فایل» بین دو ماشین. امروز هیچ‌جای
        // برنامه محتوای فایل را هش نمی‌کند؛ محاسبه‌اش کار فاز ۷ است.
        public string ContentHash { get; set; }

        public long SizeBytes { get; set; }
        public int  FileVersion { get; set; }

        // ستونی که مسیر فایل در آن نگهداری می‌شود (PhotoPath، DocFilePath، …).
        // بدون آن، سمت گیرنده نمی‌داند فایل را به کدام فیلدِ رکورد وصل کند.
        public string ColumnName { get; set; }

        // ازسرگیریِ آپلودِ نیمه‌تمام: تعداد بایتی که سرور از قبل دارد.
        // پیاده‌سازیِ واقعی می‌تواند از این نقطه ادامه دهد؛ صفر یعنی از ابتدا.
        public long ResumeOffset { get; set; }
    }

    public sealed class FileTransferResult
    {
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; }
        public string ContentHash { get; set; }
        public int FileVersion { get; set; }

        // برای ازسرگیری: چقدر واقعاً منتقل شد.
        public long BytesTransferred { get; set; }

        // ─── فاز B ───
        // محتوای دریافتی با هشِ اعلام‌شده نخواند. این *یک شکستِ متفاوت* است:
        // تلاش دوباره منطقی است ولی فایل به‌هیچ‌وجه نباید استفاده شود.
        public bool Corrupted { get; set; }

        // سرور صراحتاً اجازه نداد (۴۰۳/۴۰۴). تلاش دوباره بی‌فایده است تا وقتی
        // دسترسی عوض نشود — پس نباید بی‌پایان تکرار شود.
        public bool AccessDenied { get; set; }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // فاز B — قراردادِ دریافت.
    //
    // آموزش — چرا قراردادِ *جدا* و نه افزودن به IFileSyncTransport:
    // IFileSyncTransport از فاز ۷ پیاده‌سازی شده و در آزمون‌ها و در
    // OfflineFileSyncTransport استفاده می‌شود. افزودن عضو به آن، همهٔ
    // پیاده‌سازی‌های موجود را می‌شکند — دقیقاً همان «تغییر ناسازگار با
    // گذشته»‌ای که قاعدهٔ پروژه ممنوع کرده است. با قراردادِ جدا، هر انتقالی که
    // دریافت را پشتیبانی می‌کند آن را *هم* پیاده می‌کند و بقیه دست‌نخورده
    // می‌مانند؛ لایهٔ بالاتر با یک بررسیِ نوع تصمیم می‌گیرد.
    // ═════════════════════════════════════════════════════════════════════════
    public interface IFileManifestTransport
    {
        bool IsConfigured { get; }

        // فهرستِ افزایشیِ فایل‌های سرور پس از نشانگر داده‌شده.
        FileManifestResult GetManifest(long cursor, int max, CancellationToken cancel);

        // دریافتِ محتوای یک فایل در مسیرِ مرحله‌ای (staging).
        //
        // ⚠ مسیرِ نهایی هرگز مستقیماً نوشته نمی‌شود: تا وقتی هش تأیید نشده،
        // فایلِ محلیِ موجود نباید حتی یک بایت دست بخورد.
        FileTransferResult DownloadToFile(string fileGlobalId, string stagingPath,
                                          string expectedHash, long expectedSize,
                                          CancellationToken cancel);
    }

    // یک قلم از فهرستِ سرور.
    public sealed class RemoteFileEntry
    {
        public string FileGlobalId { get; set; }
        public string EntityName { get; set; }
        public string EntityGlobalId { get; set; }
        public string ColumnName { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string ContentHash { get; set; }
        public long   SizeBytes { get; set; }
        public int    FileVersion { get; set; }
        public bool   Deleted { get; set; }
        public long   Cursor { get; set; }
    }

    public sealed class FileManifestResult
    {
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; }
        public System.Collections.Generic.List<RemoteFileEntry> Files { get; set; }
        public long NextCursor { get; set; }
        public bool HasMore { get; set; }

        public FileManifestResult()
        {
            Files = new System.Collections.Generic.List<RemoteFileEntry>();
        }
    }
}

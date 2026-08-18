using System.Collections.Generic;
using System.Threading;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // پیاده‌سازیِ «هیچ سروری وجود ندارد» — حالت پیش‌فرضِ امروزِ برنامه.
    //
    // آموزش — چرا این کلاس لازم است و چرا این‌قدر ساده است:
    // خواستهٔ صریح این بود که «حالت آنلاینِ ساختگی» ساخته نشود. ساده‌ترین راهِ
    // شکستنِ این قاعده، پیاده‌سازیِ ساختگی‌ای است که وانمود کند ارسال موفق
    // بوده؛ آن‌وقت صف خالی می‌شد و تغییرات کاربر *برای همیشه* از بین می‌رفت
    // بدون این‌که هیچ‌جا رفته باشند. پس این کلاس هرگز موفقیت گزارش نمی‌کند.
    //
    // نقش واقعی‌اش دو چیز است:
    //   ۱. برنامه بدون هیچ سروری کامل کار کند (SyncService با همین پیاده‌سازی
    //      ساخته می‌شود و صف را دست‌نخورده باقی می‌گذارد).
    //   ۲. در آزمون‌ها بتوان مسیرِ «آفلاین» را دقیقاً همان‌طور که در واقعیت
    //      رخ می‌دهد آزمود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class OfflineSyncTransport : ISyncTransport
    {
        public const string OfflineMessage =
            "سرور همگام‌سازی پیکربندی نشده است — برنامه به‌صورت آفلاین کار می‌کند.";

        public string Name { get { return "بدون سرور (آفلاین)"; } }

        public bool IsConfigured { get { return false; } }

        public SyncConnectionStatus GetStatus(CancellationToken cancel)
        {
            return SyncConnectionStatus.NotConfigured(OfflineMessage);
        }

        public SyncAuthResult Authenticate(CancellationToken cancel)
        {
            return new SyncAuthResult { Succeeded = false, ErrorMessage = OfflineMessage };
        }

        // ⚠ هرگز Succeeded = true برنمی‌گرداند. اگر روزی کسی این را «برای
        // راحتی آزمون» تغییر دهد، صفِ تغییراتِ کاربر بی‌سروصدا پاک می‌شود.
        public SyncPushResult Push(IList<SyncChange> batch, CancellationToken cancel)
        {
            return new SyncPushResult { Succeeded = false, ErrorMessage = OfflineMessage };
        }

        public SyncPullResult Pull(string cursor, int maxItems, CancellationToken cancel)
        {
            return new SyncPullResult { Succeeded = false, ErrorMessage = OfflineMessage };
        }
    }

    // همتای فایلیِ همان مفهوم — فقط برای اینکه لایه‌های بالاتر همیشه یک
    // پیاده‌سازیِ معتبر در اختیار داشته باشند (فاز ۷ جایگزینش می‌کند).
    public sealed class OfflineFileSyncTransport : IFileSyncTransport
    {
        public bool IsConfigured { get { return false; } }

        public FileTransferResult UploadFile(FileSyncItem item, CancellationToken cancel)
        {
            return new FileTransferResult
            {
                Succeeded = false,
                ErrorMessage = OfflineSyncTransport.OfflineMessage
            };
        }

        public FileTransferResult DownloadFile(string entityGlobalId, string fileName,
                                               string targetPath, CancellationToken cancel)
        {
            return new FileTransferResult
            {
                Succeeded = false,
                ErrorMessage = OfflineSyncTransport.OfflineMessage
            };
        }
    }
}

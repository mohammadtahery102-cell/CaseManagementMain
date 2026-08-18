using System;
using System.Collections.Generic;
using System.Linq;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // مدل‌های «همگام‌سازی عکس و سند» — افزودنی کامل به موتور همگام‌سازی موجود.
    //
    // آموزش — چرا فایل و کلاس‌های جدا و نه تغییر SyncModels/SyncEngine:
    // موتور فعلی (Provider → Comparer → Engine) آزموده و در حال استفاده است و
    // قرارداد IDataSyncProvider ممکن است فردا Providerهای دیگری داشته باشد.
    // اگر عکس/سند را داخل همان مسیر می‌بردیم، هم امضای interface عوض می‌شد و
    // هم منطقِ کارکنندهٔ فعلی دست می‌خورد. این‌طور، همگام‌سازیِ HTML دقیقاً مثل
    // قبل کار می‌کند و رسانه‌ها یک مسیر موازی و مستقل دارند.
    // ═════════════════════════════════════════════════════════════════════════

    public enum MediaKind
    {
        Photo,          // عکس سرپرست (نام فایل = کد اختصاصی پرونده)
        Document,       // سند (نام پوشه = کد اختصاصی پرونده)

        // ─── افزودنی: دو نوع عکسِ دیگر که تا حالا اصلاً وارد نمی‌شدند ───
        // FamilyPhoto: عکسِ جمعیِ خانواده → ستون TblCase.FamilyPhotoPath
        //   پوشه‌ی FamilyPhotos ، نام فایل = کد اختصاصی پرونده
        // MemberPhoto: عکسِ یک عضو → ستون TblFamily.MemberPhotoPath
        //   پوشه‌ی MemberPhotos\<کد پرونده>\<شماره‌ی تذکره‌ی عضو>.jpg
        //   شماره‌ی تذکره کلیدِ اصلیِ نگاشت است؛ برخلافِ نام، یکتاست و با
        //   املای متفاوت یا هم‌نامیِ دو عضو در یک خانواده اشتباه نمی‌شود.
        //   نامِ فارسی همچنان به‌عنوانِ پشتیبان پذیرفته می‌شود تا بسته‌های قدیمی
        //   از کار نیفتند.
        FamilyPhoto,
        MemberPhoto
    }

    // سرنوشتِ تشخیص‌داده‌شده برای هر فایل، پیش از هر نوشتنی.
    public enum MediaAction
    {
        Add,            // پرونده پیدا شد و چیزی از قبل نیست → افزودن
        Replace,        // از قبل چیزی هست → فقط با تأیید صریح کاربر جایگزین می‌شود
        SkipUnchanged,  // دقیقاً همان فایل از قبل ثبت شده
        NoMatch,        // کد اختصاصی در دیتابیس نیست
        OtherCenter,    // کد هست ولی متعلق به مرکزی است که کاربر به آن دسترسی ندارد
        Unsupported,    // پسوند پشتیبانی‌نشده
        InvalidName,    // نام فایل/پوشه کد معتبری نیست
        Corrupt,        // فایل خراب است (امضای بایتی نامعتبر)
        TooLarge,       // از سقف مجاز بزرگ‌تر است
        Duplicate       // برای یک کد، چند فایل عکس وجود دارد
    }

    // یک فایل کشف‌شده روی دیسک، همراه با تشخیص و هشدارهایش.
    public sealed class MediaItem
    {
        public MediaKind Kind { get; set; }
        public MediaAction Action { get; set; }

        public string SourcePath { get; set; }      // مسیر فایل در پوشه‌ی ورودی
        public string CaseCode { get; set; }        // کد اختصاصی استخراج‌شده
        public int CasId { get; set; }              // شناسه‌ی پرونده در دیتابیس (۰ = پیدا نشد)
        public string FileName { get; set; }

        public long SizeBytes { get; set; }
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }

        public bool NeedsRotation { get; set; }     // بر اساس EXIF
        public bool WillCompress { get; set; }
        public string ExistingPath { get; set; }    // مقدار فعلی در دیتابیس (برای Replace)

        // ─── فقط برای عکسِ عضو ───
        public string MemberName { get; set; }      // نامِ فارسیِ عضو (از نام فایل)
        public string MemberTazkira { get; set; }   // شماره‌ی تذکره‌ی نرمال‌شده (از نام فایل)
        public bool MatchedByName { get; set; }     // true = با نام تطبیق شد، نه با تذکره
        public int FamId { get; set; }              // شناسه‌ی عضو در TblFamily (۰ = پیدا نشد)

        public string Message { get; set; }         // توضیح وضعیت برای کاربر
        public List<string> Warnings { get; set; }

        // آیا کاربر این مورد را برای اعمال انتخاب کرده؟
        public bool Selected { get; set; }

        public MediaItem()
        {
            Warnings = new List<string>();
            Selected = true;
        }

        // فقط این دو حالت واقعاً چیزی می‌نویسند.
        public bool IsApplicable
        {
            get { return Action == MediaAction.Add || Action == MediaAction.Replace; }
        }
    }

    // خلاصه‌ی یک پرونده در صفحه‌ی پیش‌نمایش (مطابق نمونه‌ی درخواستی).
    public sealed class MediaCaseSummary
    {
        public string CaseCode { get; set; }
        public bool GuardianFound { get; set; }
        public int MemberCount { get; set; }
        public bool PhotoFound { get; set; }
        public int DocumentCount { get; set; }
        public int Warnings { get; set; }
        public int Errors { get; set; }
    }

    // نتیجه‌ی پویش پوشه — همه‌چیز پیش از نوشتن مشخص است.
    public sealed class MediaPlan
    {
        public string RootFolder { get; set; }
        public string PhotosFolder { get; set; }
        public string DocumentsFolder { get; set; }

        // فایلِ واقعیِ دیتابیسی که این پویش با آن تطبیق داده — برای تشخیصِ
        // «چند نسخه‌ی نصب‌شده» (نگاه کنید PackageValidationReport.DatabasePath).
        public string DatabasePath { get; set; }
        public int TotalCasesInDatabase { get; set; }

        public List<MediaItem> Photos { get; set; }
        public List<MediaItem> Documents { get; set; }

        // اعتبارسنجی‌های سطح پوشه (نبودِ پوشه، کدهای تکراری و ...)
        public List<string> ValidationErrors { get; set; }
        public List<string> ValidationWarnings { get; set; }

        // کدهایی که در دیتابیس هستند ولی عکس/سندی برایشان نیامده.
        public List<string> CasesMissingPhoto { get; set; }

        public MediaPlan()
        {
            Photos = new List<MediaItem>();
            Documents = new List<MediaItem>();
            ValidationErrors = new List<string>();
            ValidationWarnings = new List<string>();
            CasesMissingPhoto = new List<string>();
        }

        private static int Count(IEnumerable<MediaItem> src, MediaAction a)
        {
            return src.Count(i => i.Action == a);
        }

        public int PhotosToAdd      { get { return Count(Photos, MediaAction.Add); } }
        public int PhotosToReplace  { get { return Count(Photos, MediaAction.Replace); } }
        public int PhotosNoMatch    { get { return Count(Photos, MediaAction.NoMatch); } }
        public int PhotosOtherCenter{ get { return Count(Photos, MediaAction.OtherCenter); } }
        public int PhotosDuplicate  { get { return Count(Photos, MediaAction.Duplicate); } }
        public int PhotosUnsupported{ get { return Count(Photos, MediaAction.Unsupported); } }
        public int PhotosCorrupt    { get { return Count(Photos, MediaAction.Corrupt); } }
        public int PhotosTooLarge   { get { return Count(Photos, MediaAction.TooLarge); } }

        public int DocsToAdd        { get { return Count(Documents, MediaAction.Add); } }
        public int DocsToReplace    { get { return Count(Documents, MediaAction.Replace); } }
        public int DocsNoMatch      { get { return Count(Documents, MediaAction.NoMatch); } }
        public int DocsOtherCenter  { get { return Count(Documents, MediaAction.OtherCenter); } }
        public int DocsUnsupported  { get { return Count(Documents, MediaAction.Unsupported); } }
        public int DocsSkipUnchanged{ get { return Count(Documents, MediaAction.SkipUnchanged); } }

        public int TotalApplicable
        {
            get { return Photos.Count(p => p.Selected && p.IsApplicable) +
                         Documents.Count(d => d.Selected && d.IsApplicable); }
        }

        public bool HasAnything { get { return Photos.Count > 0 || Documents.Count > 0; } }
    }

    // گزارش نهایی پس از اعمال.
    public sealed class MediaReport
    {
        public bool Success { get; set; }
        public bool RolledBack { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }

        public int PhotosImported { get; set; }
        public int PhotosReplaced { get; set; }
        public int DocumentsImported { get; set; }
        public int DocumentsReplaced { get; set; }

        public int MissingPhotos { get; set; }
        public int MissingDocuments { get; set; }
        public int DuplicateFiles { get; set; }
        public int UnsupportedFiles { get; set; }
        public int InvalidFileNames { get; set; }
        public int LargeImages { get; set; }
        public int CompressedImages { get; set; }
        public int RotatedImages { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }

        public string ErrorMessage { get; set; }
        public List<string> Log { get; set; }

        public MediaReport()
        {
            Log = new List<string>();
            StartedAt = DateTime.Now;
        }

        public TimeSpan Duration { get { return FinishedAt - StartedAt; } }

        public void Add(string line)
        {
            Log.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line);
        }
    }
}

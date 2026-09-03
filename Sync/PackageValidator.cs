using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // «بررسی بسته‌ی ورودی» — وارسیِ پیش از پرواز (Pre-flight Check).
    //
    // ⚠ قانونِ مطلق: این کلاس هیچ چیزی نمی‌نویسد.
    //   • هیچ رکوردی در دیتابیس ثبت/ویرایش/حذف نمی‌شود (فقط SELECT).
    //   • هیچ فایلی کپی، جابه‌جا یا تغییر داده نمی‌شود (فقط خواندن).
    //   • هیچ پوشه‌ای ساخته نمی‌شود.
    //
    // بازاستفاده به‌جای دوباره‌نویسی: بخش بزرگی از وارسی‌های رسانه‌ای از قبل در
    // MediaScanner وجود دارد (تطبیق کد، تکراری، فرمتِ پشتیبانی‌نشده، فایلِ خراب،
    // ابعاد و حجم). اینجا همان به‌کار گرفته می‌شود و فقط وارسی‌هایی که نبود
    // اضافه می‌گردد: خودِ فایل‌های HTML، کدهای تکراری، پوشه‌ی خالی، فایلِ
    // بی‌صاحب، و تطبیقِ دوطرفه‌ی «پرونده ↔ عکس/سند».
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class PackageValidator
    {
        // برآوردِ زمان بر پایه‌ی اندازه‌گیریِ واقعی روی این پروژه:
        // پویش ~۴٫۳ میلی‌ثانیه و اعمال ~۱٫۱ میلی‌ثانیه برای هر فایل.
        private const double MillisecondsPerFileScan = 4.3;
        private const double MillisecondsPerFileApply = 1.1;
        private const double MillisecondsPerRecord = 2.0;

        private readonly DatabaseHelper _db;

        public PackageValidator(DatabaseHelper db)
        {
            _db = db ?? new DatabaseHelper();
        }

        public PackageValidationReport Validate(string rootFolder, IProgress<SyncProgress> progress,
                                                System.Threading.CancellationToken cancel)
        {
            var report = new PackageValidationReport { RootFolder = rootFolder };

            // آموزش — نمایشِ صریحِ دیتابیسِ متصل، برای رفعِ ابهامِ «چند نسخه از
            // برنامه روی یک سیستم» (نگاه کنید توضیح در ValidationModels.cs).
            try
            {
                report.DatabasePath = _db.GetDatabaseFilePath();
                using (SQLiteConnection con = _db.GetConnection())
                using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM TblCase", con))
                {
                    con.Open();
                    report.TotalCasesInDatabase = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch { /* غیرحیاتی — نبودش گزارش را متوقف نمی‌کند */ }

            // ── ۱) خودِ پوشه ──
            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            {
                report.Add(ValidationSeverity.Critical, "بسته", "", "",
                    "پوشه‌ی بسته‌ی ورودی وجود ندارد یا انتخاب نشده است.",
                    "دکمه‌ی «انتخاب پوشه» را بزنید و پوشه‌ی بسته را انتخاب کنید.");
                report.FinishedAt = DateTime.Now;
                return report;
            }

            Report(progress, "بررسی فایل‌های اطلاعات", 1, 5);

            // ── ۲) فایل‌های اطلاعات ──
            string guardiansFile = FindHtml(rootFolder, "Guardians", "سرپرست");
            // آموزش — «Members» نامِ رسمی/الزامیِ جدید است؛ اول امتحان می‌شود.
            // «FamilyMembers» فقط برای سازگاری با بسته‌های قدیمی نگه داشته شده.
            string membersFile = FindHtml(rootFolder, "Members", "FamilyMembers", "اعضا");

            ValidateHtmlFile(report, guardiansFile, "فایل سرپرستان", true);
            ValidateHtmlFile(report, membersFile, "فایل اعضای خانواده", false);

            // ── ۳) تجزیه و وارسیِ محتوای فایل‌ها ──
            ParsedSyncData parsed = null;
            if (guardiansFile != null || membersFile != null)
            {
                Report(progress, "خواندن اطلاعات پرونده‌ها", 2, 5);
                parsed = ParseAndValidate(report, guardiansFile, membersFile);
            }

            var packageCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (parsed != null)
            {
                report.TotalCasesInPackage = parsed.Guardians.Count;
                report.TotalMembersInPackage = parsed.Members.Count;

                foreach (SyncRecord g in parsed.Guardians)
                    if (!string.IsNullOrWhiteSpace(g.PublicCode)) packageCodes.Add(SyncCodeNormalizer.Normalize(g.PublicCode));

                CheckDuplicateCodes(report, parsed.Guardians);
            }

            cancel.ThrowIfCancellationRequested();

            // ── ۴) رسانه‌ها: از موتورِ موجود استفاده می‌شود ──
            Report(progress, "بررسی عکس‌ها و اسناد", 3, 5);

            var scanner = new MediaScanner(_db);
            MediaPlan media = scanner.Scan(rootFolder, null, cancel);

            report.TotalPhotos = media.Photos.Count;
            report.TotalDocuments = media.Documents.Count;

            bool hasPhotosFolder = Directory.Exists(Path.Combine(rootFolder, MediaScanner.PhotosFolderName));
            bool hasDocsFolder = Directory.Exists(Path.Combine(rootFolder, MediaScanner.DocumentsFolderName));

            if (!hasPhotosFolder)
                report.Add(ValidationSeverity.Warning, "ساختار پوشه", "", MediaScanner.PhotosFolderName,
                    "پوشه‌ی عکس‌ها در بسته نیست.",
                    "اگر قصد ورود عکس دارید، پوشه‌ای به نام Photos بسازید. در غیر این صورت این پیام را نادیده بگیرید.");

            if (!hasDocsFolder)
                report.Add(ValidationSeverity.Warning, "ساختار پوشه", "", MediaScanner.DocumentsFolderName,
                    "پوشه‌ی اسناد در بسته نیست.",
                    "اگر قصد ورود سند دارید، پوشه‌ای به نام Documents بسازید. در غیر این صورت این پیام را نادیده بگیرید.");

            TranslateMediaFindings(report, media);

            cancel.ThrowIfCancellationRequested();

            // ── ۵) وارسی‌هایی که در MediaScanner نبودند ──
            Report(progress, "بررسی ساختار پوشه‌ها", 4, 5);

            if (hasDocsFolder)
            {
                CheckEmptyFolders(report, Path.Combine(rootFolder, MediaScanner.DocumentsFolderName));
                CheckDuplicateDocumentNames(report, Path.Combine(rootFolder, MediaScanner.DocumentsFolderName));
                CheckCorruptDocuments(report, media);
            }

            CheckStrayFiles(report, rootFolder, guardiansFile, membersFile);
            CrossCheckPackageAgainstMedia(report, packageCodes, media, hasPhotosFolder, hasDocsFolder);

            // ── ۶) جمع‌بندی ──
            Report(progress, "جمع‌بندی", 5, 5);
            Summarize(report, media, packageCodes);

            report.FinishedAt = DateTime.Now;
            return report;
        }

        // ─── فایل‌های اطلاعات ────────────────────────────────────────────────
        private static string FindHtml(string folder, params string[] keys)
        {
            try
            {
                foreach (string f in Directory.GetFiles(folder, "*.htm*"))
                {
                    string name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    foreach (string k in keys)
                        if (name.IndexOf(k.ToLowerInvariant(), StringComparison.Ordinal) >= 0) return f;
                }
            }
            catch { }
            return null;
        }

        private void ValidateHtmlFile(PackageValidationReport report, string path, string label, bool required)
        {
            if (path == null)
            {
                report.Add(required ? ValidationSeverity.Critical : ValidationSeverity.Warning,
                    "فایل اطلاعات", "", label,
                    label + " در پوشه پیدا نشد.",
                    "فایلی که از سامانه گرفته‌اید را داخل پوشه‌ی بسته بگذارید.");
                return;
            }

            try
            {
                var info = new FileInfo(path);
                if (info.Length == 0)
                {
                    report.Add(ValidationSeverity.Critical, "فایل اطلاعات", "", Path.GetFileName(path),
                        label + " خالی است.",
                        "فایل را دوباره از سامانه بگیرید.");
                    return;
                }

                // فقط خواندن — برای اطمینان از باز شدن و داشتنِ جدول.
                string head;
                using (var reader = new StreamReader(path, Encoding.UTF8, true))
                {
                    char[] buffer = new char[8192];
                    int n = reader.Read(buffer, 0, buffer.Length);
                    head = new string(buffer, 0, Math.Max(0, n));
                }

                if (head.IndexOf("<table", StringComparison.OrdinalIgnoreCase) < 0 &&
                    head.IndexOf("<tr", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    report.Add(ValidationSeverity.Critical, "فایل اطلاعات", "", Path.GetFileName(path),
                        label + " ساختار مورد انتظار را ندارد (جدولی در آن پیدا نشد).",
                        "مطمئن شوید همان فایلی است که از سامانه خروجی گرفته‌اید و دستکاری نشده.");
                }
            }
            catch (Exception ex)
            {
                report.Add(ValidationSeverity.Critical, "فایل اطلاعات", "", Path.GetFileName(path),
                    label + " قابل خواندن نیست: " + ex.Message,
                    "فایل ممکن است باز باشد یا خراب شده؛ آن را ببندید یا دوباره بگیرید.");
            }
        }

        private ParsedSyncData ParseAndValidate(PackageValidationReport report,
                                                string guardiansFile, string membersFile)
        {
            try
            {
                var provider = new HtmlSyncProvider();
                var source = new SyncSource
                {
                    GuardiansFilePath = guardiansFile,
                    MembersFilePath = membersFile
                };

                ParsedSyncData parsed = provider.Parse(source, null);

                // وارسیِ ساختاریِ خودِ Provider (ستون‌های لازم، کد خالی و ...)
                foreach (string message in provider.Validate(parsed))
                    report.Add(ValidationSeverity.Warning, "محتوای فایل", "", "", message,
                        "ردیف‌های دارای اشکال در همگام‌سازی نادیده گرفته می‌شوند.");

                // رکوردهایی که کدشان خالی است اصلاً قابل استفاده نیستند.
                int missingCode = parsed.Guardians.Count(g => string.IsNullOrWhiteSpace(g.PublicCode));
                if (missingCode > 0)
                    report.Add(ValidationSeverity.Critical, "کد پرونده", "", "",
                        missingCode + " پرونده در فایل، کد اختصاصی ندارد.",
                        "در سامانه برای این پرونده‌ها کد ثبت کنید و دوباره خروجی بگیرید.");

                return parsed;
            }
            catch (Exception ex)
            {
                report.Add(ValidationSeverity.Critical, "محتوای فایل", "", "",
                    "خواندن اطلاعات از فایل ممکن نشد: " + ex.Message,
                    "فایل را دوباره از سامانه خروجی بگیرید.");
                return null;
            }
        }

        private void CheckDuplicateCodes(PackageValidationReport report, List<SyncRecord> guardians)
        {
            var groups = guardians
                .Where(g => !string.IsNullOrWhiteSpace(g.PublicCode))
                .GroupBy(g => g.PublicCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(gr => gr.Count() > 1)
                .ToList();

            report.DuplicateCaseCodes = groups.Count;

            foreach (var gr in groups)
                report.Add(ValidationSeverity.Critical, "کد تکراری", gr.Key, "",
                    "کد «" + gr.Key + "» برای " + gr.Count() + " پرونده تکرار شده است.",
                    "کد اختصاصی باید یکتا باشد. در سامانه اصلاح کنید و دوباره خروجی بگیرید.");
        }

        // ─── تبدیلِ یافته‌های MediaScanner به یافته‌های گزارش ────────────────
        private void TranslateMediaFindings(PackageValidationReport report, MediaPlan media)
        {
            foreach (MediaItem p in media.Photos)
            {
                switch (p.Action)
                {
                    case MediaAction.NoMatch:
                        report.OrphanFiles++;
                        report.Add(ValidationSeverity.Warning, "عکسِ بی‌صاحب", p.CaseCode, p.FileName,
                            "پرونده‌ای با این کد وجود ندارد.",
                            "نام فایل را به کد اختصاصیِ درست تغییر دهید، یا فایل را از پوشه بردارید.");
                        break;

                    case MediaAction.Duplicate:
                        report.DuplicateFiles++;
                        report.Add(ValidationSeverity.Warning, "عکس تکراری", p.CaseCode, p.FileName,
                            "برای این کد بیش از یک عکس وجود دارد؛ هیچ‌کدام وارد نمی‌شود.",
                            "فقط یک عکس برای هر پرونده نگه دارید و بقیه را حذف کنید.");
                        break;

                    case MediaAction.Unsupported:
                        report.UnsupportedFiles++;
                        report.Add(ValidationSeverity.Warning, "فرمت پشتیبانی‌نشده", p.CaseCode, p.FileName,
                            p.Message,
                            "فایل را به یکی از فرمت‌های jpg یا png یا bmp تبدیل کنید.");
                        break;

                    case MediaAction.Corrupt:
                        report.CorruptedFiles++;
                        report.Add(ValidationSeverity.Warning, "عکس خراب", p.CaseCode, p.FileName,
                            "این تصویر باز نمی‌شود و احتمالاً خراب است.",
                            "عکس را دوباره تهیه و جایگزین کنید.");
                        break;

                    case MediaAction.InvalidName:
                        report.InvalidFileNames++;
                        report.Add(ValidationSeverity.Warning, "نام نامعتبر", "", p.FileName,
                            "نام فایل کد معتبری ندارد.",
                            "نام فایل باید دقیقاً کد اختصاصی پرونده باشد. مثال: 100245.jpg");
                        break;

                    case MediaAction.OtherCenter:
                        report.Add(ValidationSeverity.Warning, "مرکز دیگر", p.CaseCode, p.FileName,
                            "این پرونده متعلق به مرکز دیگری است.",
                            "با کاربر همان مرکز وارد کنید، یا مرکز فعال را عوض کنید.");
                        break;
                }

                // هشدارهای کیفیت (حجم و ابعاد) — مانع همگام‌سازی نیستند.
                if (p.SizeBytes > MediaScanner.RecommendedMaxPhotoBytes)
                {
                    report.LargeImages++;
                    report.Add(ValidationSeverity.Warning, "حجم زیاد", p.CaseCode, p.FileName,
                        "حجم عکس " + MediaScanner.FormatSize(p.SizeBytes) + " است (بیشتر از ۵ مگابایت).",
                        "نگران نباشید: هنگام ورود، با کیفیت بالا فشرده می‌شود.");
                }

                if (p.PixelWidth > 0 &&
                    (p.PixelWidth < MediaScanner.RecommendedMinPixels ||
                     p.PixelHeight < MediaScanner.RecommendedMinPixels))
                {
                    report.SmallImages++;
                    report.ResolutionWarnings++;
                    report.Add(ValidationSeverity.Warning, "ابعاد کم", p.CaseCode, p.FileName,
                        "ابعاد " + p.PixelWidth + "×" + p.PixelHeight + " کمتر از ۶۰۰×۶۰۰ است.",
                        "عکس با کیفیت بهتر تهیه کنید تا در کارت و گزارش خوب دیده شود.");
                }
            }

            foreach (MediaItem d in media.Documents)
            {
                switch (d.Action)
                {
                    case MediaAction.NoMatch:
                        report.OrphanFiles++;
                        report.Add(ValidationSeverity.Warning, "سندِ بی‌صاحب", d.CaseCode, d.FileName,
                            "پرونده‌ای با کد «" + d.CaseCode + "» وجود ندارد.",
                            "نام پوشه را به کد اختصاصیِ درست تغییر دهید.");
                        break;

                    case MediaAction.Unsupported:
                        report.UnsupportedFiles++;
                        report.Add(ValidationSeverity.Warning, "فرمت پشتیبانی‌نشده", d.CaseCode, d.FileName,
                            d.Message,
                            "فایل را به pdf یا docx یا عکس تبدیل کنید.");
                        break;

                    case MediaAction.InvalidName:
                        report.InvalidFileNames++;
                        report.Add(ValidationSeverity.Warning, "نام نامعتبر", "", d.FileName,
                            "نام پوشه کد معتبری ندارد.",
                            "نام پوشه باید دقیقاً کد اختصاصی پرونده باشد.");
                        break;

                    case MediaAction.OtherCenter:
                        report.Add(ValidationSeverity.Warning, "مرکز دیگر", d.CaseCode, d.FileName,
                            "این پرونده متعلق به مرکز دیگری است.",
                            "با کاربر همان مرکز وارد کنید، یا مرکز فعال را عوض کنید.");
                        break;
                }

                if (d.SizeBytes > FileHelper.MaxDocumentFileSizeBytes)
                    report.Add(ValidationSeverity.Warning, "حجم سند", d.CaseCode, d.FileName,
                        "حجم سند " + MediaScanner.FormatSize(d.SizeBytes) + " از سقف مجاز بیشتر است.",
                        "فایل را فشرده یا به چند بخش تقسیم کنید.");
            }
        }

        // ─── وارسی‌های تازه ─────────────────────────────────────────────────
        private void CheckEmptyFolders(PackageValidationReport report, string documentsFolder)
        {
            try
            {
                foreach (string folder in Directory.GetDirectories(documentsFolder))
                {
                    if (Directory.GetFiles(folder).Length != 0) continue;

                    report.EmptyFolders++;
                    report.Add(ValidationSeverity.Warning, "پوشه‌ی خالی",
                        new DirectoryInfo(folder).Name, "",
                        "این پوشه هیچ سندی ندارد.",
                        "سندها را داخلش بگذارید یا پوشه را حذف کنید.");
                }
            }
            catch { }
        }

        private void CheckDuplicateDocumentNames(PackageValidationReport report, string documentsFolder)
        {
            try
            {
                foreach (string folder in Directory.GetDirectories(documentsFolder))
                {
                    string code = new DirectoryInfo(folder).Name;

                    var byName = Directory.GetFiles(folder)
                        .GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1);

                    foreach (var g in byName)
                    {
                        report.DuplicateFiles++;
                        report.Add(ValidationSeverity.Warning, "سند تکراری", code, g.Key,
                            "در این پرونده " + g.Count() + " سند با نام یکسان وجود دارد.",
                            "نام‌ها را متفاوت کنید تا هر سند جداگانه ثبت شود.");
                    }
                }
            }
            catch { }
        }

        // سلامتِ سندها از روی امضای بایتیِ ابتدای فایل — بدون بازکردن کامل.
        private void CheckCorruptDocuments(PackageValidationReport report, MediaPlan media)
        {
            foreach (MediaItem d in media.Documents)
            {
                if (!d.IsApplicable) continue;

                string ext = Path.GetExtension(d.SourcePath ?? "").ToLowerInvariant();
                if (ext != ".pdf" && ext != ".docx") continue;

                try
                {
                    byte[] head = new byte[4];
                    using (var fs = new FileStream(d.SourcePath, FileMode.Open, FileAccess.Read))
                    {
                        if (fs.Read(head, 0, 4) < 4)
                        {
                            report.CorruptedFiles++;
                            report.Add(ValidationSeverity.Warning, "سند خراب", d.CaseCode, d.FileName,
                                "این فایل بسیار کوچک یا ناقص است.",
                                "فایل سالم را جایگزین کنید.");
                            continue;
                        }
                    }

                    bool ok = ext == ".pdf"
                        // %PDF
                        ? (head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46)
                        // docx یک فایل زیپ است: PK..
                        : (head[0] == 0x50 && head[1] == 0x4B);

                    if (!ok)
                    {
                        report.CorruptedFiles++;
                        report.Add(ValidationSeverity.Warning, "سند خراب", d.CaseCode, d.FileName,
                            "محتوای فایل با پسوندش نمی‌خواند و احتمالاً خراب است.",
                            "فایل سالم را جایگزین کنید یا پسوند درست را بگذارید.");
                    }
                }
                catch (Exception ex)
                {
                    report.CorruptedFiles++;
                    report.Add(ValidationSeverity.Warning, "سند خراب", d.CaseCode, d.FileName,
                        "خواندن فایل ممکن نشد: " + ex.Message,
                        "مطمئن شوید فایل باز نیست و دسترسی خواندن دارد.");
                }
            }
        }

        // فایل‌هایی که در ریشه‌ی بسته افتاده‌اند و استفاده نمی‌شوند.
        private void CheckStrayFiles(PackageValidationReport report, string rootFolder,
                                     string guardiansFile, string membersFile)
        {
            try
            {
                foreach (string f in Directory.GetFiles(rootFolder))
                {
                    if (PathEquals(f, guardiansFile) || PathEquals(f, membersFile)) continue;

                    report.UnusedFiles++;
                    report.Add(ValidationSeverity.Warning, "فایل بلااستفاده", "", Path.GetFileName(f),
                        "این فایل در ریشه‌ی بسته است و استفاده نمی‌شود.",
                        "عکس‌ها باید در پوشه‌ی Photos و اسناد در پوشه‌ی Documents باشند.");
                }
            }
            catch { }
        }

        private static bool PathEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        // تطبیقِ دوطرفه: پرونده‌های بسته در برابر عکس/سندهای موجود.
        private void CrossCheckPackageAgainstMedia(PackageValidationReport report,
                                                   HashSet<string> packageCodes, MediaPlan media,
                                                   bool hasPhotosFolder, bool hasDocsFolder)
        {
            // آموزش — packageCodes حالا نرمال‌شده است (SyncCodeNormalizer)؛ برای
            // مقایسه‌ی درست، کدهای رسانه هم باید همان‌طور نرمال شوند.
            var photoCodes = new HashSet<string>(
                media.Photos.Where(p => !string.IsNullOrWhiteSpace(p.CaseCode)).Select(p => SyncCodeNormalizer.Normalize(p.CaseCode)),
                StringComparer.OrdinalIgnoreCase);

            var docCodes = new HashSet<string>(
                media.Documents.Where(d => !string.IsNullOrWhiteSpace(d.CaseCode)).Select(d => SyncCodeNormalizer.Normalize(d.CaseCode)),
                StringComparer.OrdinalIgnoreCase);

            if (hasPhotosFolder)
            {
                foreach (string code in packageCodes)
                    if (!photoCodes.Contains(code)) report.HtmlRecordsWithoutPhoto++;

                if (report.HtmlRecordsWithoutPhoto > 0)
                    report.Add(ValidationSeverity.Warning, "بدون عکس", "", "",
                        report.HtmlRecordsWithoutPhoto + " پرونده در فایل هست که عکسی برایش نیامده.",
                        "اگر عکس دارید با نام کد پرونده در پوشه‌ی Photos بگذارید. همگام‌سازی متوقف نمی‌شود.");
            }

            if (hasDocsFolder)
            {
                foreach (string code in packageCodes)
                    if (!docCodes.Contains(code)) report.HtmlRecordsWithoutDocument++;

                if (report.HtmlRecordsWithoutDocument > 0)
                    report.Add(ValidationSeverity.Warning, "بدون سند", "", "",
                        report.HtmlRecordsWithoutDocument + " پرونده در فایل هست که سندی برایش نیامده.",
                        "اگر سند دارید در پوشه‌ای به نام کد پرونده بگذارید. همگام‌سازی متوقف نمی‌شود.");
            }

            // عکس/سندی که کدش در فایل نیست ولی در دیتابیس هست — یعنی پرونده‌ی
            // قدیمی. این اشکال نیست، فقط اطلاع.
            int mediaWithoutRecord = photoCodes.Concat(docCodes)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(c => !packageCodes.Contains(c));

            report.CasesWithoutHtmlRecord = mediaWithoutRecord;
            report.MissingPhotos = media.CasesMissingPhoto.Count;
            report.MissingDocuments = report.HtmlRecordsWithoutDocument;
        }

        // ─── جمع‌بندی ────────────────────────────────────────────────────────
        private void Summarize(PackageValidationReport report, MediaPlan media, HashSet<string> packageCodes)
        {
            // پرونده‌های دارای خطا/هشدار بر اساس کدِ درگیر در یافته‌ها
            var withError = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var withWarning = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ValidationIssue issue in report.Issues)
            {
                if (string.IsNullOrWhiteSpace(issue.CaseCode)) continue;
                if (issue.Severity == ValidationSeverity.Critical) withError.Add(issue.CaseCode);
                else if (issue.Severity == ValidationSeverity.Warning) withWarning.Add(issue.CaseCode);
            }

            withWarning.ExceptWith(withError);

            report.CasesWithErrors = withError.Count;
            report.CasesWithWarnings = withWarning.Count;
            report.ReadyCases = Math.Max(0, packageCodes.Count - withError.Count - withWarning.Count);

            // برآوردِ زمان بر پایه‌ی سرعتِ اندازه‌گیری‌شده‌ی واقعی
            int fileCount = media.Photos.Count + media.Documents.Count;
            int recordCount = report.TotalCasesInPackage + report.TotalMembersInPackage;

            double ms = fileCount * (MillisecondsPerFileScan + MillisecondsPerFileApply)
                      + recordCount * MillisecondsPerRecord;

            report.EstimatedSyncTime = TimeSpan.FromMilliseconds(Math.Max(500, ms));
        }

        private static void Report(IProgress<SyncProgress> progress, string phase, int current, int total)
        {
            if (progress == null) return;
            progress.Report(new SyncProgress { Phase = phase, Current = current, Total = total });
        }
    }
}

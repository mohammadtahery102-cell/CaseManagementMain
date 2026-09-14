using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    public static class FileHelper
    {
        // روی دیسک: پوشهٔ عکس تکی «Photo» است؛ ثابتِ کد همان HeadPhoto می‌ماند
        // تا فراخوان‌های موجود نشکنند.
        public const string SectionHeadPhoto = "HeadPhoto";
        public const string SectionFamilyPhoto = "FamilyPhoto";
        public const string SectionMemberPhotos = "MemberPhotos";
        public const string SectionDocs = "Docs";
        // Phase 5 — عکس‌های بازدید میدانی، در همان ساختارِ بخش‌بندیِ موجود.
        public const string SectionVisitPhotos = "VisitPhotos";
        // Phase 7 — عکسِ نمایندهٔ قانونی. بخشِ جدا و نه SectionHeadPhoto:
        // عکسِ سرپرست یک فایلِ ثابت به‌ازای هر پرونده است و نامِ فایلش
        // «<کد>-Head» است؛ نماینده چند فایل دارد و باید مستقلاً جایگزین/حذف
        // شود بدونِ اینکه عکسِ سرپرست را لمس کند.
        public const string SectionRepresentativePhotos = "RepresentativePhotos";

        // Feature 3 — عکسِ سرپرستِ کودک (TblOrphan.GuardianPhotoPath). بخشِ
        // جدا از SectionHeadPhoto: سرپرستِ کودک با سرپرستِ خانوار یکی نیست
        // و باید مستقلاً جایگزین/حذف شود.
        public const string SectionGuardianPhotos = "GuardianPhotos";
        public const string SectionCaseFiles = "CaseFiles";
        public const string SectionExports = "Exports";
        public const string SectionTemp = "Temp";

        public const string DiskGuardianPhotosFolder = "GuardianPhotos";
        public const string DiskFamilyPhotosFolder = "FamilyPhotos";
        public const string DiskDocumentsFolder = "Documents";
        public const string LegacyDiskPhotoFolder = "Photo";
        public const string LegacyDocsFolder = "Docs";
        public const string DiskPhotoFolder = LegacyDiskPhotoFolder;

        private const int MaxSegmentLength = 100;
        private const int MaxFullPathLength = 320;

        public static long MaxPhotoFileSizeBytes = 15L * 1024 * 1024;
        public static long MaxDocumentFileSizeBytes = 50L * 1024 * 1024;

        private static readonly object SyncRoot = new object();

        private static string _baseRootFolder = "";
        private static string _lastError = "";

        // چیدمانِ چهارسطحی برای پرونده‌ای که هنوز در دیتابیس نیست (مثلاً
        // ذخیرهٔ عکس قبل از INSERT). کلید = کد اختصاصی پاک‌شده.
        private static readonly Dictionary<string, CaseStorageLayout> _pendingLayouts =
            new Dictionary<string, CaseStorageLayout>(StringComparer.OrdinalIgnoreCase);

        public sealed class CaseStorageLayout
        {
            public string Province;
            public string District;
            public string RequestType;
            public string ServiceStatus;
            public string CaseCode;
        }

        private static readonly string AppFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CaseManagement");

        private static readonly string RootConfigPath =
            Path.Combine(AppFolder, "BaseRootFolder.txt");

        // آموزش — قبلاً LogPath یک static readonly ثابت بود (محاسبه‌شده فقط
        // یک‌بار موقع بارگذاری کلاس، قبل از اینکه SettingsHelper حتی آماده
        // باشد). حالا هر بار که لاگ نوشته می‌شود مسیر تنظیمات (اگر مدیر سیستم
        // چیزی انتخاب کرده) بررسی می‌شود؛ در نبود آن، همان مسیر پیش‌فرض قبلی.
        private static string GetLogPath()
        {
            try
            {
                string configured = SettingsHelper.Get(SettingsHelper.LogsPath);
                if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
                    return Path.Combine(configured, "FileHelper.log");
            }
            catch { /* در دسترس نبودن دیتابیس نباید لاگ‌نویسی را بشکند */ }

            return Path.Combine(AppFolder, "FileHelper.log");
        }

        private static string GetDefaultRootFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CaseManagement", "Storage");
        }

        private static readonly HashSet<string> AllowedSections =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                SectionHeadPhoto,
                SectionFamilyPhoto,
                SectionMemberPhotos,
                SectionDocs,
                SectionVisitPhotos,
                SectionRepresentativePhotos,
                SectionGuardianPhotos,
                SectionCaseFiles,
                SectionExports,
                SectionTemp
            };

        private static readonly HashSet<string> AllowedPhotoExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"
            };

        private static readonly HashSet<string> AllowedDocumentExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".rtf",
                ".jpg", ".jpeg", ".png", ".tif", ".tiff"
            };

        public static string LastError
        {
            get
            {
                lock (SyncRoot)
                    return _lastError;
            }
        }

        public static string GetBaseRootFolder()
        {
            lock (SyncRoot)
            {
                if (!string.IsNullOrWhiteSpace(_baseRootFolder) && Directory.Exists(_baseRootFolder))
                    return _baseRootFolder;
            }

            string savedRoot = LoadSavedRootFolder();

            if (IsUsableRootFolder(savedRoot))
            {
                string fullPath = Path.GetFullPath(savedRoot);

                lock (SyncRoot)
                    _baseRootFolder = fullPath;

                return fullPath;
            }

            string defaultRoot = GetDefaultRootFolder();

            try
            {
                Directory.CreateDirectory(defaultRoot);

                if (!IsDirectoryWritable(defaultRoot))
                {
                    SetLastError("پوشه پیش‌فرض ذخیره فایل‌ها قابل نوشتن نیست.", null);
                    return "";
                }

                string fullDefaultRoot = Path.GetFullPath(defaultRoot);

                lock (SyncRoot)
                    _baseRootFolder = fullDefaultRoot;

                return fullDefaultRoot;
            }
            catch (Exception ex)
            {
                SetLastError("خطا در ساخت پوشه پیش‌فرض ذخیره فایل‌ها.", ex);
                return "";
            }
        }
        public static bool SetBaseRootFolder(string folderPath, out string error)
        {
            error = "";

            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    error = "مسیر پوشه اصلی خالی است.";
                    SetLastError(error, null);
                    return false;
                }

                string fullPath = Path.GetFullPath(folderPath);

                Directory.CreateDirectory(fullPath);

                if (!IsDirectoryWritable(fullPath))
                {
                    error = "برنامه اجازه نوشتن در این پوشه را ندارد.";
                    SetLastError(error, null);
                    return false;
                }

                SaveRootFolder(fullPath);

                lock (SyncRoot)
                    _baseRootFolder = fullPath;

                return true;
            }
            catch (Exception ex)
            {
                error = "خطا در تنظیم پوشه اصلی ذخیره فایل‌ها.";
                SetLastError(error, ex);
                return false;
            }
        }

        public static string GetOrChooseBaseRootFolder()
        {
            string currentRoot = GetBaseRootFolder();

            if (!string.IsNullOrWhiteSpace(currentRoot))
                return currentRoot;

            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "محل اصلی ذخیره فایل‌های پروژه را انتخاب کنید";
                fbd.ShowNewFolderButton = true;

                if (fbd.ShowDialog() != DialogResult.OK)
                    return "";

                string error;

                if (SetBaseRootFolder(fbd.SelectedPath, out error))
                    return GetBaseRootFolder();

                Msg.Show(error, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "";
            }
        }

        // چهار پوشهٔ طبقه‌بندی: ولایت / ولسوالی / نوع پرونده / وضعیت خدمات.
        // مقدار خالی → «نامشخص» تا خروجی بی‌صاحب در ریشه پخش نشود.
        public static string ClassifySegment(string value)
        {
            string cleaned = CleanName(string.IsNullOrWhiteSpace(value) ? "نامشخص" : value.Trim());
            return string.IsNullOrWhiteSpace(cleaned) ? "نامشخص" : cleaned;
        }

        public static void RememberLayout(string caseCode, string province, string district,
                                          string requestType, string serviceStatus)
        {
            string key = CleanName(caseCode);
            if (string.IsNullOrWhiteSpace(key) || key == "Unknown") return;

            var layout = new CaseStorageLayout
            {
                CaseCode = key,
                Province = ClassifySegment(province),
                District = ClassifySegment(district),
                RequestType = ClassifySegment(requestType),
                ServiceStatus = ClassifySegment(serviceStatus)
            };

            lock (SyncRoot)
                _pendingLayouts[key] = layout;
        }

        public static string GetClassifiedFolder(string province, string district,
                                                 string requestType, string serviceStatus)
        {
            string root = GetBaseRootFolder();
            if (string.IsNullOrWhiteSpace(root)) return "";

            string folder = Path.Combine(
                root,
                ClassifySegment(province),
                ClassifySegment(district),
                ClassifySegment(requestType),
                ClassifySegment(serviceStatus));

            if (!IsPathInsideFolder(folder, root))
            {
                SetLastError("مسیر طبقه‌بندی نامعتبر است.", null);
                return "";
            }

            Directory.CreateDirectory(folder);
            return folder;
        }

        public static string GetCaseFolderPath(string caseCode)
        {
            string root, clean, folder;
            if (!TryGetCaseContext(caseCode, false, false, out root, out clean, out folder))
                return "";
            return folder;
        }

        public static void OpenFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                SetLastError("باز کردن پوشه ممکن نشد.", ex);
            }
        }

        public static void ClearBaseRootFolder()
        {
            lock (SyncRoot)
                _baseRootFolder = "";

            try
            {
                if (File.Exists(RootConfigPath))
                    File.Delete(RootConfigPath);
            }
            catch (Exception ex)
            {
                SetLastError("خطا در حذف تنظیمات پوشه اصلی.", ex);
            }
        }

        public static string CleanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unknown";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder();
            bool previousWasSpace = false;

            foreach (char ch in value)
            {
                if (Array.IndexOf(invalidChars, ch) >= 0 || char.IsControl(ch))
                    continue;

                if (char.IsWhiteSpace(ch))
                {
                    if (!previousWasSpace)
                    {
                        sb.Append(' ');
                        previousWasSpace = true;
                    }

                    continue;
                }

                sb.Append(ch);
                previousWasSpace = false;
            }

            string result = sb.ToString().Trim().Trim('.');

            if (string.IsNullOrWhiteSpace(result))
                result = "Unknown";

            if (IsReservedWindowsName(result))
                result = "_" + result;

            if (result.Length > MaxSegmentLength)
                result = result.Substring(0, MaxSegmentLength).Trim().Trim('.');

            if (string.IsNullOrWhiteSpace(result))
                return "Unknown";

            return result;
        }

        public static string EnsureCaseStructure(string caseCode)
        {
            try
            {
                string root;
                string cleanCaseCode;
                string caseFolder;

                if (!TryGetCaseContext(caseCode, true, true, out root, out cleanCaseCode, out caseFolder))
                    return "";

                foreach (string section in AllowedSections)
                {
                    string sectionFolder = BuildSectionFolderPath(caseFolder, cleanCaseCode, section);
                    Directory.CreateDirectory(sectionFolder);
                }

                return caseFolder;
            }
            catch (Exception ex)
            {
                SetLastError("خطا در ساخت پوشه‌های پرونده.", ex);
                return "";
            }
        }

        public static string GetSectionFolder(string caseCode, string sectionName)
        {
            try
            {
                string normalizedSection;
                if (!TryNormalizeSectionName(sectionName, out normalizedSection))
                    return "";

                string root;
                string cleanCaseCode;
                string caseFolder;

                if (!TryGetCaseContext(caseCode, true, true, out root, out cleanCaseCode, out caseFolder))
                    return "";

                string sectionFolder = BuildSectionFolderPath(caseFolder, cleanCaseCode, normalizedSection);

                if (!IsPathInsideFolder(sectionFolder, caseFolder))
                {
                    SetLastError("مسیر بخش پرونده نامعتبر است.", null);
                    return "";
                }

                if (sectionFolder.Length >= MaxFullPathLength)
                {
                    SetLastError("مسیر پوشه بخش بیش از حد طولانی است.", null);
                    return "";
                }

                Directory.CreateDirectory(sectionFolder);
                return sectionFolder;
            }
            catch (Exception ex)
            {
                SetLastError("خطا در گرفتن مسیر بخش پرونده.", ex);
                return "";
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 3 — پوشهٔ دسته‌بندیِ سند (زیرِ همان بخشِ Docs موجود، نه یک
        // ساختارِ تازه). categoryFolderName از TblDocumentCategory.FolderName
        // می‌آید و از قبل انگلیسی/امن است (Code-محور)، پس فقط CleanName روی آن
        // اجرا می‌شود تا از ورودیِ نامعتبر محافظت کند.
        // مسیر: <Root>/<ولایت>/<ولسوالی>/<نوع>/<وضعیت>/<کد>/Docs/<CategoryFolderName>/
        // ═══════════════════════════════════════════════════════════════════
        public static string GetDocumentCategoryFolder(string caseCode, string categoryFolderName)
        {
            try
            {
                string docsFolder = GetSectionFolder(caseCode, SectionDocs);
                if (string.IsNullOrEmpty(docsFolder))
                    return "";

                string cleanCategory = CleanName(categoryFolderName);
                string categoryFolder = Path.Combine(docsFolder, cleanCategory);

                if (!IsPathInsideFolder(categoryFolder, docsFolder))
                {
                    SetLastError("مسیر دستهٔ سند نامعتبر است.", null);
                    return "";
                }

                if (categoryFolder.Length >= MaxFullPathLength)
                {
                    SetLastError("مسیر پوشهٔ دستهٔ سند بیش از حد طولانی است.", null);
                    return "";
                }

                Directory.CreateDirectory(categoryFolder);
                return categoryFolder;
            }
            catch (Exception ex)
            {
                SetLastError("خطا در گرفتن پوشهٔ دستهٔ سند.", ex);
                return "";
            }
        }

        // نامِ فایلِ استانداردِ انگلیسی روی دیسک — نامِ اصلیِ فارسی/آزادِ کاربر
        // فقط در دیتابیس (OriginalFileName/DocDescription) نگه داشته می‌شود.
        // الگو: {CaseCode}_{CategoryCode}_{DocID}_{yyyyMMdd}.{ext}
        public static string BuildAsciiDocumentFileName(string caseCode, string categoryCode, int docId, string extension)
        {
            string cleanCase = CleanName(caseCode);
            string cleanCategory = string.IsNullOrWhiteSpace(categoryCode) ? "GENERAL" : categoryCode.Trim().ToUpperInvariant();
            string ext = (extension ?? "").TrimStart('.');
            string datePart = DateTime.Now.ToString("yyyyMMdd");

            return string.Format("{0}_{1}_{2}_{3}.{4}", cleanCase, cleanCategory, docId, datePart, ext);
        }

        public static string SaveFileToCaseFolder(
            string sourceFilePath,
            string caseCode,
            string sectionName,
            string baseFileName,
            string existingStoredPath = "",
            bool replaceExisting = true)
        {
            existingStoredPath = existingStoredPath ?? "";

            try
            {
                if (string.IsNullOrWhiteSpace(sourceFilePath))
                    return existingStoredPath;

                string sourceFullPath = Path.GetFullPath(sourceFilePath);

                if (!File.Exists(sourceFullPath))
                    return existingStoredPath;

                string normalizedSection;
                if (!TryNormalizeSectionName(sectionName, out normalizedSection))
                    return existingStoredPath;

                string root;
                string cleanCaseCode;
                string caseFolder;

                if (!TryGetCaseContext(caseCode, true, true, out root, out cleanCaseCode, out caseFolder))
                    return existingStoredPath;

                string folder = BuildSectionFolderPath(caseFolder, cleanCaseCode, normalizedSection);
                Directory.CreateDirectory(folder);

                string extension = Path.GetExtension(sourceFullPath);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    SetLastError("فایل انتخاب‌شده پسوند ندارد.", null);
                    return existingStoredPath;
                }

                extension = extension.ToLowerInvariant();

                if (!ValidateFileForSection(sourceFullPath, normalizedSection, extension))
                    return existingStoredPath;

                string existingFullPath;
                bool canUseExistingPath = TryGetStoredPathInsideFolder(existingStoredPath, folder, out existingFullPath);

                if (canUseExistingPath &&
                    string.Equals(Path.GetExtension(existingFullPath), extension, StringComparison.OrdinalIgnoreCase))
                {
                    if (AreSamePath(sourceFullPath, existingFullPath))
                    {
                        TryCatalogSavedFile(cleanCaseCode, normalizedSection, existingFullPath, sourceFullPath);
                        return existingFullPath;
                    }

                    // replaceExisting=false: فایل زنده را قبل از Commit دیتابیس
                    // بازنویسی نکن؛ نسخهٔ جدید با نام یکتا کنار قبلی می‌ماند.
                    if (replaceExisting)
                    {
                        try
                        {
                            CopyFileAtomically(sourceFullPath, existingFullPath);
                            TryCatalogSavedFile(cleanCaseCode, normalizedSection, existingFullPath, sourceFullPath);
                            return existingFullPath;
                        }
                        catch (Exception ex)
                        {
                            SetLastError("جایگزینی فایل قبلی انجام نشد؛ فایل جدید ساخته می‌شود.", ex);
                        }
                    }
                }

                string kind = FileKindPolicy.FromSection(normalizedSection);
                string context = NormalizeNamingContext(baseFileName, cleanCaseCode);
                string targetPath = FileNamingPolicy.NextAvailablePath(
                    folder, cleanCaseCode, kind, context, DateTime.Now, extension);
                CopyFileAtomically(sourceFullPath, targetPath);

                if (replaceExisting &&
                    canUseExistingPath && File.Exists(existingFullPath) && !AreSamePath(existingFullPath, targetPath))
                    DeletePreviousFileAfterCommit(existingFullPath, targetPath);

                if (replaceExisting)
                    TryCatalogSavedFile(cleanCaseCode, normalizedSection, targetPath, sourceFullPath);
                return targetPath;
            }
            catch (Exception ex)
            {
                SetLastError("خطا در ذخیره فایل.", ex);
                return existingStoredPath;
            }
        }

        public static Task<string> SaveFileToCaseFolderAsync(
            string sourceFilePath,
            string caseCode,
            string sectionName,
            string baseFileName,
            string existingStoredPath = "",
            bool replaceExisting = true)
        {
            if (string.IsNullOrWhiteSpace(GetBaseRootFolder()))
            {
                SetLastError("برای ذخیره async ابتدا پوشه اصلی را انتخاب یا تنظیم کنید.", null);
                return Task.FromResult(existingStoredPath ?? "");
            }

            return Task.Run(() =>
                SaveFileToCaseFolder(sourceFilePath, caseCode, sectionName, baseFileName,
                    existingStoredPath, replaceExisting));
        }

        /// <summary>
        /// After the database pointer has moved to <paramref name="committedStoredPath"/>,
        /// delete the previous file. No-op when paths are empty or the same.
        /// </summary>
        public static void DeletePreviousFileAfterCommit(string previousStoredPath, string committedStoredPath)
        {
            if (string.IsNullOrWhiteSpace(previousStoredPath))
                return;

            if (!string.IsNullOrWhiteSpace(committedStoredPath))
            {
                try
                {
                    if (AreSamePath(
                        CaseFileInventory.ResolveStoredPath(previousStoredPath),
                        CaseFileInventory.ResolveStoredPath(committedStoredPath)))
                        return;
                }
                catch
                {
                    if (string.Equals(previousStoredPath.Trim(), committedStoredPath.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            DeleteFileAndCatalog(previousStoredPath);
        }

        public static void DiscardStagedFileAfterFailure(string stagedPath, string committedPath)
        {
            if (string.IsNullOrWhiteSpace(stagedPath))
                return;

            if (!string.IsNullOrWhiteSpace(committedPath))
            {
                try
                {
                    if (AreSamePath(
                        CaseFileInventory.ResolveStoredPath(stagedPath),
                        CaseFileInventory.ResolveStoredPath(committedPath)))
                        return;
                }
                catch
                {
                    if (string.Equals(stagedPath.Trim(), committedPath.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            DeleteFileAndCatalog(stagedPath);
        }

        public static void CatalogCommittedFile(string caseCode, string sectionName, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                return;
            string normalizedSection;
            if (!TryNormalizeSectionName(sectionName, out normalizedSection))
                normalizedSection = sectionName ?? "";
            TryCatalogSavedFile(caseCode ?? "", normalizedSection, fullPath, fullPath);
        }

        private static void DeleteFileAndCatalog(string filePath)
        {
            string resolved = "";
            try { resolved = CaseFileInventory.ResolveStoredPath(filePath); }
            catch { resolved = filePath ?? ""; }

            try
            {
                if (!string.IsNullOrWhiteSpace(resolved))
                    new FileCatalogService().MarkPathDeleted(resolved);
            }
            catch { }

            DeleteFileIfExists(filePath);
        }

        private static string NormalizeNamingContext(string requestedName, string caseCode)
        {
            string value = CleanName(requestedName);
            if (string.Equals(value, CleanName(caseCode), StringComparison.OrdinalIgnoreCase))
                return "";
            if (value.StartsWith(CleanName(caseCode) + "_", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(CleanName(caseCode).Length + 1);
            return value;
        }

        private static void TryCatalogSavedFile(string caseCode, string section,
            string fullPath, string originalSource)
        {
            try
            {
                new FileCatalogService().Register(caseCode,
                    FileKindPolicy.FromSection(section), fullPath,
                    "", 0, "", "", Path.GetFileName(originalSource));
            }
            catch
            {
                // catalog is additive metadata. A temporary database problem must
                // not turn a successfully persisted user file into a failed save.
            }
        }

        public static void DeleteFileIfExists(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                string root = GetBaseRootFolder();
                string fullPath = CaseFileInventory.ResolveStoredPath(filePath);

                if (string.IsNullOrWhiteSpace(root) || !IsPathInsideFolder(fullPath, root))
                {
                    SetLastError("حذف فایل خارج از پوشه اصلی مجاز نیست.", null);
                    return;
                }

                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                SetLastError("خطا در حذف فایل.", ex);
            }
        }

        // ─── حذف کامل پوشه‌ی فایل‌های یک پرونده (عکس/سند/خروجی) ─────────────
        // آموزش — امنیت: پوشه فقط وقتی حذف می‌شود که «داخل پوشه‌ی اصلی ذخیره»
        // باشد (IsPathInsideFolder) تا هرگز مسیری بیرون از ریشه پاک نشود. اگر
        // پوشه‌ای وجود نداشته باشد، بی‌خطر true برمی‌گرداند (چیزی برای حذف نبود).
        public static bool DeleteCaseFolder(string caseCode)
        {
            try
            {
                string root, cleanCaseCode, caseFolder;
                // allowChooseDialog=false و create=false → فقط مسیر را محاسبه می‌کند،
                // نه دیالوگ می‌آورد نه پوشه می‌سازد.
                if (!TryGetCaseContext(caseCode, false, false, out root, out cleanCaseCode, out caseFolder))
                    return false;

                if (string.IsNullOrWhiteSpace(caseFolder) || !Directory.Exists(caseFolder))
                    return true; // پوشه‌ای نبود

                if (string.IsNullOrWhiteSpace(root) || !IsPathInsideFolder(caseFolder, root))
                {
                    SetLastError("حذف پوشه خارج از پوشه اصلی مجاز نیست.", null);
                    return false;
                }

                Directory.Delete(caseFolder, true);
                return true;
            }
            catch (Exception ex)
            {
                SetLastError("خطا در حذف پوشه‌ی پرونده.", ex);
                return false;
            }
        }

        private static bool TryGetCaseContext(
            string caseCode,
            bool allowChooseDialog,
            bool create,
            out string root,
            out string cleanCaseCode,
            out string caseFolder)
        {
            root = GetRootFolder(allowChooseDialog);
            cleanCaseCode = CleanName(caseCode);
            caseFolder = "";

            if (string.IsNullOrWhiteSpace(root))
                return false;

            CaseStorageLayout layout = ResolveLayout(cleanCaseCode);
            caseFolder = Path.Combine(
                root,
                ClassifySegment(layout != null ? layout.Province : null),
                ClassifySegment(layout != null ? layout.District : null),
                ClassifySegment(layout != null ? layout.RequestType : null),
                ClassifySegment(layout != null ? layout.ServiceStatus : null),
                cleanCaseCode);

            if (!IsPathInsideFolder(caseFolder, root))
            {
                SetLastError("مسیر پرونده نامعتبر است.", null);
                return false;
            }

            if (caseFolder.Length >= MaxFullPathLength)
            {
                SetLastError("مسیر پوشه پرونده بیش از حد طولانی است.", null);
                return false;
            }

            if (create)
                Directory.CreateDirectory(caseFolder);

            return true;
        }

        private static string GetRootFolder(bool allowChooseDialog)
        {
            string root = GetBaseRootFolder();
            if (!string.IsNullOrWhiteSpace(root))
                return root;

            return allowChooseDialog ? GetOrChooseBaseRootFolder() : "";
        }

        private static bool TryNormalizeSectionName(string sectionName, out string normalizedSection)
        {
            normalizedSection = CleanName(sectionName);

            foreach (string section in AllowedSections)
            {
                if (string.Equals(section, normalizedSection, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedSection = section;
                    return true;
                }
            }

            SetLastError("نام بخش فایل نامعتبر است: " + sectionName, null);
            normalizedSection = "";
            return false;
        }

        // layout نسخهٔ ۲: هر نوع فایل پوشهٔ صریح خودش را دارد. نام‌های logical
        // قدیمی فقط برای سازگاری API در این نقطه ترجمه می‌شوند.
        public static string DiskFolderForSection(string sectionName)
        {
            if (string.Equals(sectionName, SectionHeadPhoto, StringComparison.OrdinalIgnoreCase)
                || string.Equals(sectionName, SectionGuardianPhotos, StringComparison.OrdinalIgnoreCase))
                return DiskGuardianPhotosFolder;
            if (string.Equals(sectionName, SectionFamilyPhoto, StringComparison.OrdinalIgnoreCase))
                return DiskFamilyPhotosFolder;
            if (string.Equals(sectionName, SectionDocs, StringComparison.OrdinalIgnoreCase))
                return DiskDocumentsFolder;
            return sectionName;
        }

        // i=0 → همان نام؛ i>=1 → «نام - 1». برای جلوگیری از overwrite بی‌سؤال.
        public static string UniqueName(string baseName, int duplicateIndex)
        {
            string clean = CleanName(baseName);
            if (duplicateIndex <= 0) return clean;
            return clean + " - " + duplicateIndex.ToString();
        }

        private static string BuildSectionFolderPath(string caseFolder, string cleanCaseCode, string sectionName)
        {
            return Path.Combine(caseFolder, DiskFolderForSection(sectionName));
        }

        private static CaseStorageLayout ResolveLayout(string cleanCaseCode)
        {
            if (string.IsNullOrWhiteSpace(cleanCaseCode)) return null;

            lock (SyncRoot)
            {
                CaseStorageLayout pending;
                if (_pendingLayouts.TryGetValue(cleanCaseCode, out pending) && pending != null)
                    return pending;
            }

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
SELECT IFNULL(c.Province, ''),
       IFNULL(c.District, ''),
       IFNULL(NULLIF(TRIM(rt.Name), ''), IFNULL(c.RequestType, '')),
       IFNULL(NULLIF(TRIM(ss.Name), ''), IFNULL(c.ServiceStatus, ''))
FROM TblCase c
LEFT JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID
LEFT JOIN TblServiceStatus ss ON ss.ServiceStatusID = c.ServiceStatusID
WHERE c.Code = @Code
LIMIT 1;", con))
                {
                    cmd.Parameters.AddWithValue("@Code", cleanCaseCode);
                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        return new CaseStorageLayout
                        {
                            CaseCode = cleanCaseCode,
                            Province = reader.GetString(0),
                            District = reader.GetString(1),
                            RequestType = reader.GetString(2),
                            ServiceStatus = reader.GetString(3)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                SetLastError("خواندن طبقه‌بندی پوشهٔ پرونده ممکن نشد.", ex);
                return null;
            }
        }

        private static bool ValidateFileForSection(string sourceFullPath, string sectionName, string extension)
        {
            FileInfo info = new FileInfo(sourceFullPath);

            if (IsPhotoSection(sectionName))
            {
                if (!AllowedPhotoExtensions.Contains(extension))
                {
                    SetLastError("پسوند فایل عکس مجاز نیست: " + extension, null);
                    return false;
                }

                if (MaxPhotoFileSizeBytes > 0 && info.Length > MaxPhotoFileSizeBytes)
                {
                    SetLastError("حجم فایل عکس بیش از حد مجاز است.", null);
                    return false;
                }

                if (!LooksLikeImageFile(sourceFullPath, extension))
                {
                    SetLastError("محتوای فایل با عکس معتبر سازگار نیست.", null);
                    return false;
                }

                return true;
            }

            if (!AllowedDocumentExtensions.Contains(extension))
            {
                SetLastError("پسوند فایل سند مجاز نیست: " + extension, null);
                return false;
            }

            if (MaxDocumentFileSizeBytes > 0 && info.Length > MaxDocumentFileSizeBytes)
            {
                SetLastError("حجم فایل سند بیش از حد مجاز است.", null);
                return false;
            }

            if (!LooksLikeDeclaredDocumentType(sourceFullPath, extension))
            {
                SetLastError("محتوای فایل با نوع سند اعلام‌شده (پسوند) سازگار نیست.", null);
                return false;
            }

            return true;
        }

        // آموزش — بدون این بررسی، فایلی مثل malware.exe فقط با تغییر پسوند به
        // .pdf از کنترل پسوندِ بالا رد می‌شد (همان ریسکی که LooksLikeImageFile
        // برای عکس‌ها از قبل می‌بست). امضای باینری هر فرمت را می‌سنجد؛ برای
        // فرمت‌هایی که امضای قابل‌اتکا ندارند (txt/rtf) بررسی رد می‌شود (true).
        private static bool LooksLikeDeclaredDocumentType(string path, string extension)
        {
            if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" ||
                extension == ".tif" || extension == ".tiff")
                return LooksLikeImageFile(path, extension);

            if (extension == ".txt" || extension == ".rtf")
                return true;

            byte[] header = new byte[8];

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int read = fs.Read(header, 0, header.Length);
                if (read < 4)
                    return false;
            }

            if (extension == ".pdf")
                return header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;

            // .docx/.xlsx بسته‌های ZIP هستند (PK\x03\x04)؛ .doc/.xls قدیمی OLE هستند.
            if (extension == ".docx" || extension == ".xlsx")
                return header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;

            if (extension == ".doc" || extension == ".xls")
                return header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0;

            return true;
        }

        public static bool IsPhotoSection(string sectionName)
        {
            return string.Equals(sectionName, SectionHeadPhoto, StringComparison.OrdinalIgnoreCase)
                || string.Equals(sectionName, SectionFamilyPhoto, StringComparison.OrdinalIgnoreCase)
                || string.Equals(sectionName, SectionMemberPhotos, StringComparison.OrdinalIgnoreCase)
                // Phase 7 — عکسِ نماینده هم تصویر است، پس همان بررسیِ پسوند و
                // هدرِ واقعیِ فایل بر آن اعمال می‌شود؛ بدونِ این خط یک فایلِ
                // دلخواه با پسوندِ jpg پذیرفته می‌شد.
                || string.Equals(sectionName, SectionRepresentativePhotos, StringComparison.OrdinalIgnoreCase)
                // Feature 3 — عکسِ سرپرستِ کودک، به همان دلیل: بدونِ این خط
                // بررسیِ پسوند و هدرِ واقعیِ تصویر روی آن اجرا نمی‌شد.
                || string.Equals(sectionName, SectionGuardianPhotos, StringComparison.OrdinalIgnoreCase)
                || string.Equals(sectionName, SectionVisitPhotos, StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeImageFile(string path, string extension)
        {
            byte[] header = new byte[12];

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int read = fs.Read(header, 0, header.Length);
                if (read < 4)
                    return false;
            }

            if (extension == ".jpg" || extension == ".jpeg")
                return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            if (extension == ".png")
                return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;

            if (extension == ".gif")
                return header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46;

            if (extension == ".bmp")
                return header[0] == 0x42 && header[1] == 0x4D;

            if (extension == ".tif" || extension == ".tiff")
                return (header[0] == 0x49 && header[1] == 0x49) || (header[0] == 0x4D && header[1] == 0x4D);

            if (extension == ".webp")
                return header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                    && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

            return false;
        }

        private static bool TryGetStoredPathInsideFolder(string storedPath, string folder, out string fullPath)
        {
            fullPath = "";

            if (string.IsNullOrWhiteSpace(storedPath))
                return false;

            try
            {
                string candidate = CaseFileInventory.ResolveStoredPath(storedPath);
                if (string.IsNullOrWhiteSpace(candidate)) return false;

                if (candidate.Length >= MaxFullPathLength)
                    return false;

                if (!IsPathInsideFolder(candidate, folder))
                {
                    SetLastError("مسیر فایل قبلی داخل پوشه همین بخش نیست.", null);
                    return false;
                }

                fullPath = candidate;
                return true;
            }
            catch (Exception ex)
            {
                SetLastError("مسیر فایل قبلی نامعتبر است.", ex);
                return false;
            }
        }

        private static string CopyToUniquePath(string sourceFullPath, string folder, string baseName, string extension)
        {
            Directory.CreateDirectory(folder);

            baseName = CleanBaseFileName(baseName, extension);

            for (int i = 0; i < 10000; i++)
            {
                string suffix = i == 0 ? "" : " - " + i.ToString();
                string candidateBaseName = TrimBaseNameForPath(folder, UniqueName(baseName, 0), suffix, extension);
                string targetPath = Path.Combine(folder, candidateBaseName + suffix + extension);

                if (!IsPathInsideFolder(targetPath, folder))
                    throw new IOException("مسیر مقصد خارج از پوشه مجاز است.");

                if (File.Exists(targetPath))
                    continue;

                string tempPath = Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".tmp");

                try
                {
                    File.Copy(sourceFullPath, tempPath, false);
                    File.Move(tempPath, targetPath);
                    return targetPath;
                }
                catch (IOException)
                {
                    SafeDeleteTempFile(tempPath);

                    if (File.Exists(targetPath))
                        continue;

                    throw;
                }
                catch
                {
                    SafeDeleteTempFile(tempPath);
                    throw;
                }
            }

            throw new IOException("امکان ساخت نام یکتای فایل وجود ندارد.");
        }

        private static void CopyFileAtomically(string sourceFullPath, string targetFullPath)
        {
            if (AreSamePath(sourceFullPath, targetFullPath))
                return;

            if (targetFullPath.Length >= MaxFullPathLength)
                throw new PathTooLongException("مسیر فایل مقصد بیش از حد طولانی است.");

            string targetDir = Path.GetDirectoryName(targetFullPath);

            if (string.IsNullOrWhiteSpace(targetDir))
                throw new IOException("پوشه مقصد نامعتبر است.");

            Directory.CreateDirectory(targetDir);

            string tempPath = Path.Combine(targetDir, "." + Path.GetFileName(targetFullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                File.Copy(sourceFullPath, tempPath, false);

                if (File.Exists(targetFullPath))
                    File.Replace(tempPath, targetFullPath, null, true);
                else
                    File.Move(tempPath, targetFullPath);
            }
            finally
            {
                SafeDeleteTempFile(tempPath);
            }
        }

        private static string CleanBaseFileName(string baseName, string extension)
        {
            string clean = CleanName(baseName);

            string currentExtension = Path.GetExtension(clean);
            if (!string.IsNullOrWhiteSpace(currentExtension) &&
                string.Equals(currentExtension, extension, StringComparison.OrdinalIgnoreCase))
            {
                clean = Path.GetFileNameWithoutExtension(clean);
            }

            clean = CleanName(clean);

            if (string.IsNullOrWhiteSpace(clean))
                clean = "File";

            return clean;
        }

        private static string TrimBaseNameForPath(string folder, string baseName, string suffix, string extension)
        {
            int allowedLength = MaxFullPathLength - folder.Length - 1 - suffix.Length - extension.Length;

            if (allowedLength <= 0)
                throw new PathTooLongException("مسیر پوشه مقصد بیش از حد طولانی است.");

            if (baseName.Length > allowedLength)
                baseName = baseName.Substring(0, allowedLength).Trim().Trim('.');

            if (string.IsNullOrWhiteSpace(baseName))
                baseName = allowedLength >= 4 ? "File" : new string('F', allowedLength);

            return baseName;
        }

        private static bool IsPathInsideFolder(string path, string folder)
        {
            string fullPath = Path.GetFullPath(path);
            string fullFolder = Path.GetFullPath(folder);

            fullFolder = fullFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase);
        }

        private static bool AreSamePath(string firstPath, string secondPath)
        {
            return string.Equals(
                Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReservedWindowsName(string value)
        {
            string name = value.Trim();

            int dotIndex = name.IndexOf('.');
            if (dotIndex >= 0)
                name = name.Substring(0, dotIndex);

            name = name.ToUpperInvariant();

            if (name == "CON" || name == "PRN" || name == "AUX" || name == "NUL" ||
                name == "CONIN$" || name == "CONOUT$")
                return true;

            if (name.Length == 4 &&
                (name.StartsWith("COM") || name.StartsWith("LPT")) &&
                name[3] >= '1' && name[3] <= '9')
                return true;

            return false;
        }

        private static bool IsUsableRootFolder(string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                    return false;

                string fullPath = Path.GetFullPath(folderPath);

                return Directory.Exists(fullPath) && IsDirectoryWritable(fullPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDirectoryWritable(string folder)
        {
            string testFile = Path.Combine(folder, ".write-test-" + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (FileStream fs = new FileStream(testFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = { 0 };
                    fs.Write(buffer, 0, buffer.Length);
                }

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                SafeDeleteTempFile(testFile);
            }
        }

        private static string LoadSavedRootFolder()
        {
            try
            {
                if (!File.Exists(RootConfigPath))
                    return "";

                return File.ReadAllText(RootConfigPath).Trim();
            }
            catch (Exception ex)
            {
                SetLastError("خطا در خواندن تنظیمات پوشه اصلی.", ex);
                return "";
            }
        }

        private static void SaveRootFolder(string folderPath)
        {
            Directory.CreateDirectory(AppFolder);
            File.WriteAllText(RootConfigPath, folderPath);
        }

        private static void SafeDeleteTempFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static void SetLastError(string message, Exception ex)
        {
            string finalMessage = message;

            if (ex != null)
                finalMessage += " " + ex.Message;

            lock (SyncRoot)
                _lastError = finalMessage;

            try
            {
                Directory.CreateDirectory(AppFolder);

                string logText =
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    " | " +
                    finalMessage +
                    Environment.NewLine;

                if (ex != null)
                    logText += ex.ToString() + Environment.NewLine;

                File.AppendAllText(GetLogPath(), logText);
            }
            catch
            {
            }
        }
    }
}
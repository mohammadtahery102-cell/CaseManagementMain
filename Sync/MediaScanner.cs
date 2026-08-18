using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // پویشِ پوشه‌ی ورودی و ساختِ «برنامه‌ی رسانه» — بدونِ نوشتنِ حتی یک بایت.
    //
    // ساختار مورد انتظار:
    //     ImportPackage\
    //         Guardians.html , FamilyMembers.html   (همگام‌سازیِ موجود)
    //         Photos\        100245.jpg             (نام فایل = کد اختصاصی)
    //         Documents\     100245\  NationalID.pdf (نام پوشه = کد اختصاصی)
    //
    // قواعد کلیدی:
    //   • تطبیق فقط با TblCase.Code انجام می‌شود — نه شناسه‌ی دیتابیس، نه ترتیب
    //     فایل‌ها، نه ترتیب ورود. (کد در این دیتابیس ۱۶۶۱ از ۱۶۶۱ پر و یکتاست.)
    //   • نبودِ پوشه‌ی Photos یا Documents خطا نیست؛ فقط اطلاع داده می‌شود تا
    //     همگام‌سازیِ فقط-HTML دقیقاً مثل قبل کار کند.
    //   • هیچ فایلی هرگز خوانده‌شده-و-تغییر داده نمی‌شود؛ منبع فقط خوانده می‌شود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class MediaScanner
    {
        public const string PhotosFolderName = "Photos";
        public const string DocumentsFolderName = "Documents";

        // ─── افزودنی: دو پوشه‌ی اختیاریِ تازه ───
        // نبودشان خطا نیست؛ بسته‌های قدیمی دقیقاً مثل قبل کار می‌کنند.
        //
        // آموزش — نامِ رسمی/الزامیِ عکسِ خانواده «FamilyPhoto» (مفرد) است.
        // نسخه‌ی قبلی «FamilyPhotos» (جمع) بود؛ برای اینکه بسته‌های از قبل
        // آماده‌شده خراب نشوند، هر دو نام پذیرفته می‌شوند — Scan اول پوشه‌ی
        // رسمی را می‌گردد، نبود آن یعنی نامِ قدیمی را هم امتحان می‌کند.
        public const string FamilyPhotosFolderName = "FamilyPhoto";
        public const string LegacyFamilyPhotosFolderName = "FamilyPhotos";
        public const string MemberPhotosFolderName = "MemberPhotos";

        // سقف‌های توصیه‌شده (طبق درخواست). این‌ها «هشدار»اند نه «خطا».
        public const long RecommendedMaxPhotoBytes = 5L * 1024 * 1024;   // ۵ مگابایت
        public const int RecommendedMinPixels = 600;                      // ۶۰۰×۶۰۰

        private static readonly HashSet<string> PhotoExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

        private static readonly HashSet<string> DocumentExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".doc" };

        // آموزش — webp عمداً از بررسی‌های تصویری کنار گذاشته می‌شود: موتور
        // GDI+ در .NET Framework 4.7.2 اصلاً webp را رمزگشایی نمی‌کند. پس
        // ابعاد، چرخش و فشرده‌سازی برایش ممکن نیست. فایل سالم کپی می‌شود و
        // همین محدودیت در گزارش صریح نوشته می‌شود (به‌جای ادعای بررسی‌ای که
        // انجام نشده).
        private static readonly HashSet<string> GdiUndecodable =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".webp" };

        private readonly DatabaseHelper _db;

        public MediaScanner(DatabaseHelper db)
        {
            _db = db ?? new DatabaseHelper();
        }

        public MediaPlan Scan(string rootFolder, IProgress<SyncProgress> progress)
        {
            return Scan(rootFolder, progress, System.Threading.CancellationToken.None);
        }

        // ═════════════════════════════════════════════════════════════════════
        // افزودنی — پویشِ مستقلِ فقط‌یک‌دسته (به درخواستِ کاربر: هر دسته دکمه‌ی
        // آپلود/بروزرسانیِ جداگانه‌ی خودش را دارد، بدون نیاز به پوشه‌ی «بسته»ی
        // ترکیبی و بدون وابستگی به سه دسته‌ی دیگر). هر متد دقیقاً همان منطقِ
        // تطبیق/دوپلیکیت/نامعتبر Scan اصلی را (با فراخوانیِ همان متدهای خصوصی)
        // فقط روی یک پوشه اجرا می‌کند.
        // ═════════════════════════════════════════════════════════════════════
        public MediaPlan ScanPhotosOnly(string folder, IProgress<SyncProgress> progress,
                                        System.Threading.CancellationToken cancel)
        {
            var plan = new MediaPlan { RootFolder = folder, PhotosFolder = folder };
            try { plan.DatabasePath = _db.GetDatabaseFilePath(); } catch { }

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                plan.ValidationErrors.Add("پوشه‌ی عکس‌ها وجود ندارد یا انتخاب نشده است.");
                return plan;
            }

            Dictionary<string, CaseRef> byCode = LoadCaseIndex();
            plan.TotalCasesInDatabase = byCode.Count;
            ScanPhotos(plan, byCode, progress, cancel);
            return plan;
        }

        public MediaPlan ScanDocumentsOnly(string folder, IProgress<SyncProgress> progress,
                                           System.Threading.CancellationToken cancel)
        {
            var plan = new MediaPlan { RootFolder = folder, DocumentsFolder = folder };
            try { plan.DatabasePath = _db.GetDatabaseFilePath(); } catch { }

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                plan.ValidationErrors.Add("پوشه‌ی اسناد وجود ندارد یا انتخاب نشده است.");
                return plan;
            }

            Dictionary<string, CaseRef> byCode = LoadCaseIndex();
            plan.TotalCasesInDatabase = byCode.Count;
            ScanDocuments(plan, byCode, progress, cancel);
            return plan;
        }

        public MediaPlan ScanFamilyPhotoOnly(string folder, IProgress<SyncProgress> progress,
                                             System.Threading.CancellationToken cancel)
        {
            var plan = new MediaPlan { RootFolder = folder };
            try { plan.DatabasePath = _db.GetDatabaseFilePath(); } catch { }

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                plan.ValidationErrors.Add("پوشه‌ی عکسِ جمعی خانواده وجود ندارد یا انتخاب نشده است.");
                return plan;
            }

            Dictionary<string, CaseRef> byCode = LoadCaseIndex();
            plan.TotalCasesInDatabase = byCode.Count;
            ScanFamilyPhotos(plan, folder, byCode, progress, cancel);
            return plan;
        }

        public MediaPlan ScanMemberPhotosOnly(string folder, IProgress<SyncProgress> progress,
                                              System.Threading.CancellationToken cancel)
        {
            var plan = new MediaPlan { RootFolder = folder };
            try { plan.DatabasePath = _db.GetDatabaseFilePath(); } catch { }

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                plan.ValidationErrors.Add("پوشه‌ی عکسِ اعضا وجود ندارد یا انتخاب نشده است.");
                return plan;
            }

            Dictionary<string, CaseRef> byCode = LoadCaseIndex();
            plan.TotalCasesInDatabase = byCode.Count;
            ScanMemberPhotos(plan, folder, byCode, progress, cancel);
            return plan;
        }

        // آموزش — چرا پویش هم باید قابلِ لغو باشد: اندازه‌گیری نشان داد پویش
        // گران‌ترین مرحله است (هر فایل ~۴ میلی‌ثانیه، چون هر تصویر برای بررسیِ
        // ابعاد و جهت باز می‌شود). با ۱۰ هزار عکس یعنی حدود ۴۵ ثانیه؛ کاربر
        // باید بتواند در این مدت منصرف شود.
        public MediaPlan Scan(string rootFolder, IProgress<SyncProgress> progress,
                              System.Threading.CancellationToken cancel)
        {
            var plan = new MediaPlan { RootFolder = rootFolder };
            try { plan.DatabasePath = _db.GetDatabaseFilePath(); } catch { }

            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            {
                plan.ValidationErrors.Add("پوشه‌ی ورودی وجود ندارد یا انتخاب نشده است.");
                return plan;
            }

            plan.PhotosFolder = Path.Combine(rootFolder, PhotosFolderName);
            plan.DocumentsFolder = Path.Combine(rootFolder, DocumentsFolderName);

            // نبودِ این پوشه‌ها خطا نیست — همگام‌سازیِ فقط-HTML باید مثل قبل کار کند.
            bool hasPhotos = Directory.Exists(plan.PhotosFolder);
            bool hasDocs = Directory.Exists(plan.DocumentsFolder);

            if (!hasPhotos)
                plan.ValidationWarnings.Add("پوشه‌ی «" + PhotosFolderName + "» در این بسته نیست؛ عکسی وارد نمی‌شود.");
            if (!hasDocs)
                plan.ValidationWarnings.Add("پوشه‌ی «" + DocumentsFolderName + "» در این بسته نیست؛ سندی وارد نمی‌شود.");

            // آموزش — رفعِ باگِ واقعی (پیدا شده با اجرای واقعیِ آزمون، نه فقط
            // خواندنِ کد): این متد قبلاً اگر Photos/Documents هیچ‌کدام نبودند
            // اینجا فوراً return می‌کرد، *پیش از* اینکه اصلاً به FamilyPhoto/
            // MemberPhotos نگاه کند. یعنی بسته‌ای که فقط پوشه‌ی FamilyPhoto یا
            // فقط MemberPhotos داشت (بدون Photos/Documents)، هیچ‌چیزش اسکن
            // نمی‌شد — دقیقاً برخلافِ قاعده‌ی «هر پوشه مستقل کار کند». حالا هر
            // چهار پوشه پیش از تصمیمِ خروج بررسی می‌شوند.
            string familyFolder = Path.Combine(rootFolder, FamilyPhotosFolderName);
            if (!Directory.Exists(familyFolder))
                familyFolder = Path.Combine(rootFolder, LegacyFamilyPhotosFolderName); // سازگاری با نامِ قدیمی
            bool hasFamilyPhotos = Directory.Exists(familyFolder);

            string memberFolder = Path.Combine(rootFolder, MemberPhotosFolderName);
            bool hasMemberPhotos = Directory.Exists(memberFolder);

            if (!hasPhotos && !hasDocs && !hasFamilyPhotos && !hasMemberPhotos)
                return plan;

            // نگاشتِ کد → (CasID، مرکز، مسیر عکس فعلی) با یک کوئری.
            Dictionary<string, CaseRef> byCode = LoadCaseIndex();
            plan.TotalCasesInDatabase = byCode.Count;

            if (hasPhotos) ScanPhotos(plan, byCode, progress, cancel);
            if (hasDocs) ScanDocuments(plan, byCode, progress, cancel);

            if (hasFamilyPhotos)
                ScanFamilyPhotos(plan, familyFolder, byCode, progress, cancel);

            if (hasMemberPhotos)
                ScanMemberPhotos(plan, memberFolder, byCode, progress, cancel);

            BuildMissingPhotoList(plan, byCode);

            return plan;
        }

        // ─── فهرستِ پرونده‌ها برای تطبیق ─────────────────────────────────────
        private sealed class CaseRef
        {
            public int CasId;
            public int CenterId;
            public string PhotoPath;
            public string FamilyPhotoPath;   // برای عکسِ جمعی
        }

        private Dictionary<string, CaseRef> LoadCaseIndex()
        {
            var map = new Dictionary<string, CaseRef>(StringComparer.OrdinalIgnoreCase);

            using (SQLiteConnection con = _db.GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT CasID, Code, CenterID, COALESCE(PhotoPath,'') AS PhotoPath, " +
                "       COALESCE(FamilyPhotoPath,'') AS FamilyPhotoPath " +
                "FROM TblCase WHERE TRIM(COALESCE(Code,'')) <> ''", con))
            {
                con.Open();
                using (SQLiteDataReader rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        // آموزش — رفعِ رد شدنِ کاذب: کلید با SyncCodeNormalizer
                        // ساخته می‌شود تا کدی که در دیتابیس با ارقامِ فارسی یا
                        // فاصله‌ی اضافه ثبت شده هم با نامِ فایلِ لاتین/بدون‌فاصله
                        // مطابقت پیدا کند.
                        string code = SyncCodeNormalizer.Normalize(Convert.ToString(rd["Code"]));
                        if (code.Length == 0) continue;

                        map[code] = new CaseRef
                        {
                            CasId = Convert.ToInt32(rd["CasID"]),
                            CenterId = rd["CenterID"] == DBNull.Value ? 0 : Convert.ToInt32(rd["CenterID"]),
                            PhotoPath = Convert.ToString(rd["PhotoPath"]),
                            FamilyPhotoPath = Convert.ToString(rd["FamilyPhotoPath"])
                        };
                    }
                }
            }
            return map;
        }

        // ─── عکس‌ها ──────────────────────────────────────────────────────────
        private void ScanPhotos(MediaPlan plan, Dictionary<string, CaseRef> byCode,
                                IProgress<SyncProgress> progress,
                                System.Threading.CancellationToken cancel)
        {
            string[] files;
            try { files = Directory.GetFiles(plan.PhotosFolder, "*.*", SearchOption.TopDirectoryOnly); }
            catch (Exception ex)
            {
                plan.ValidationErrors.Add("خواندنِ پوشه‌ی عکس‌ها ممکن نشد: " + ex.Message);
                return;
            }

            // تشخیصِ چند-عکس-برای-یک-کد پیش از هر کاری.
            var seenCodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in files)
            {
                string code = Path.GetFileNameWithoutExtension(f).Trim();
                if (code.Length == 0) continue;
                seenCodes[code] = seenCodes.ContainsKey(code) ? seenCodes[code] + 1 : 1;
            }

            int done = 0;
            foreach (string file in files)
            {
                cancel.ThrowIfCancellationRequested();

                var item = new MediaItem
                {
                    Kind = MediaKind.Photo,
                    SourcePath = file,
                    FileName = Path.GetFileName(file),
                    CaseCode = Path.GetFileNameWithoutExtension(file).Trim()
                };

                Classify(item, plan, byCode, PhotoExtensions, seenCodes);

                if (item.Action == MediaAction.Add || item.Action == MediaAction.Replace)
                    InspectImage(item);

                plan.Photos.Add(item);

                done++;
                Report(progress, "بررسی عکس‌ها", done, files.Length);
            }
        }

        // ─── اسناد ───────────────────────────────────────────────────────────
        private void ScanDocuments(MediaPlan plan, Dictionary<string, CaseRef> byCode,
                                   IProgress<SyncProgress> progress,
                                   System.Threading.CancellationToken cancel)
        {
            string[] folders;
            try { folders = Directory.GetDirectories(plan.DocumentsFolder); }
            catch (Exception ex)
            {
                plan.ValidationErrors.Add("خواندنِ پوشه‌ی اسناد ممکن نشد: " + ex.Message);
                return;
            }

            // اسنادی که از قبل برای هر پرونده ثبت شده‌اند (برای تشخیصِ تکراری).
            Dictionary<int, HashSet<string>> existingDocs = LoadExistingDocNames();

            int done = 0;
            foreach (string folder in folders)
            {
                cancel.ThrowIfCancellationRequested();

                string code = new DirectoryInfo(folder).Name.Trim();

                string[] files;
                try { files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly); }
                catch { files = new string[0]; }

                foreach (string file in files)
                {
                    var item = new MediaItem
                    {
                        Kind = MediaKind.Document,
                        SourcePath = file,
                        FileName = Path.GetFileName(file),
                        CaseCode = code
                    };

                    Classify(item, plan, byCode, DocumentExtensions, null);

                    // سندی با همین نام از قبل برای همین پرونده ثبت شده؟
                    if (item.Action == MediaAction.Add && item.CasId > 0)
                    {
                        HashSet<string> names;
                        if (existingDocs.TryGetValue(item.CasId, out names) &&
                            names.Contains(item.FileName))
                        {
                            item.Action = MediaAction.Replace;
                            item.Message = "سندی با همین نام از قبل ثبت شده؛ فقط با تأیید شما جایگزین می‌شود.";
                        }
                    }

                    plan.Documents.Add(item);
                }

                done++;
                Report(progress, "بررسی اسناد", done, Math.Max(1, folders.Length));
            }
        }

        // ─── عکسِ جمعیِ خانواده ───────────────────────────────────────────────
        // نام فایل = کد اختصاصیِ پرونده، دقیقاً مثل عکسِ سرپرست.
        private void ScanFamilyPhotos(MediaPlan plan, string folder,
                                      Dictionary<string, CaseRef> byCode,
                                      IProgress<SyncProgress> progress,
                                      System.Threading.CancellationToken cancel)
        {
            string[] files;
            try { files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly); }
            catch { return; }

            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in files)
            {
                string code = Path.GetFileNameWithoutExtension(f).Trim();
                if (code.Length == 0) continue;
                seen[code] = seen.ContainsKey(code) ? seen[code] + 1 : 1;
            }

            int done = 0;
            foreach (string file in files)
            {
                cancel.ThrowIfCancellationRequested();

                var item = new MediaItem
                {
                    Kind = MediaKind.FamilyPhoto,
                    SourcePath = file,
                    FileName = Path.GetFileName(file),
                    CaseCode = Path.GetFileNameWithoutExtension(file).Trim()
                };

                Classify(item, plan, byCode, PhotoExtensions, seen);

                // عکسِ جمعی ستونِ خودش را دارد؛ Classify مقدارِ عکسِ سرپرست را
                // گذاشته، پس اینجا با مقدارِ درست جایگزین می‌شود.
                if (item.CasId > 0)
                {
                    CaseRef found;
                    if (byCode.TryGetValue(SyncCodeNormalizer.Normalize(item.CaseCode), out found))
                    {
                        item.ExistingPath = found.FamilyPhotoPath;
                        if (!string.IsNullOrWhiteSpace(found.FamilyPhotoPath))
                        {
                            item.Action = MediaAction.Replace;
                            item.Selected = false;
                            item.Message = "این پرونده از قبل عکس جمعی دارد؛ فقط با تأیید شما جایگزین می‌شود.";
                        }
                        else if (item.Action == MediaAction.Replace)
                        {
                            // Classify به‌خاطرِ عکسِ سرپرست «جایگزینی» گفته بود
                            item.Action = MediaAction.Add;
                            item.Selected = true;
                            item.Message = null;
                        }
                    }
                }

                if (item.IsApplicable) InspectImage(item);

                plan.Photos.Add(item);
                done++;
                Report(progress, "بررسی عکس‌های جمعی", done, files.Length);
            }
        }

        // ─── عکسِ اعضای خانواده ───────────────────────────────────────────────
        // ساختار:  MemberPhotos\<کد پرونده>\<شماره‌ی تذکره‌ی عضو>.jpg
        // آموزش — چرا تذکره و نه نام: عکس‌های اعضا در پوشه‌ی خودِ برنامه فقط با
        // کدِ پرونده نام‌گذاری می‌شوند (۲۴۳۱۶.jpg، ۲۴۳۱۶_1.jpg ...) و هیچ نشانی
        // از این‌که مالِ کدام عضوند ندارند؛ پس نگاشت باید از نامِ فایل بیاید.
        // نامِ فارسی برای این کار قابل اتکا نیست: دو عضوِ هم‌نام در یک خانواده،
        // «محمد» در برابر «محمّد»، و نیم‌فاصله‌های متفاوت همگی نگاشت را خراب
        // می‌کنند. شماره‌ی تذکره در TblFamily.MemberTazkiraNo یکتاست و پس از
        // نرمال‌سازیِ ارقام (فارسی/عربی → لاتین) و حذفِ جداکننده‌ها بی‌ابهام است.
        // تطبیق با نام حذف نشده و به‌عنوانِ پشتیبان می‌ماند تا بسته‌هایی که
        // پیش از این ساخته شده‌اند همچنان وارد شوند.
        private void ScanMemberPhotos(MediaPlan plan, string folder,
                                      Dictionary<string, CaseRef> byCode,
                                      IProgress<SyncProgress> progress,
                                      System.Threading.CancellationToken cancel)
        {
            string[] caseFolders;
            try { caseFolders = Directory.GetDirectories(folder); }
            catch { return; }

            int done = 0;
            foreach (string caseFolder in caseFolders)
            {
                cancel.ThrowIfCancellationRequested();

                string code = new DirectoryInfo(caseFolder).Name.Trim();

                string[] files;
                try { files = Directory.GetFiles(caseFolder, "*.*", SearchOption.TopDirectoryOnly); }
                catch { files = new string[0]; }

                // اعضای همین پرونده — یک نگاشت بر پایه‌ی تذکره (اصلی) و یکی بر
                // پایه‌ی نام (پشتیبان).
                Dictionary<string, int> membersByTazkira = null;
                Dictionary<string, int> members = null;
                CaseRef caseRef;
                if (byCode.TryGetValue(SyncCodeNormalizer.Normalize(code), out caseRef))
                {
                    membersByTazkira = LoadMembersByTazkira(caseRef.CasId);
                    members = LoadMembers(caseRef.CasId);
                }

                foreach (string file in files)
                {
                    string bare = Path.GetFileNameWithoutExtension(file);
                    var item = new MediaItem
                    {
                        Kind = MediaKind.MemberPhoto,
                        SourcePath = file,
                        FileName = Path.GetFileName(file),
                        CaseCode = code,
                        MemberTazkira = NormalizeTazkira(bare),
                        MemberName = CleanMemberName(bare)
                    };

                    Classify(item, plan, byCode, PhotoExtensions, null);

                    // عکسِ عضو ستونِ جداگانه دارد؛ «جایگزینیِ» ناشی از عکسِ
                    // سرپرست اینجا معنا ندارد و باید بازنشانی شود.
                    if (item.Action == MediaAction.Replace) { item.Action = MediaAction.Add; item.Selected = true; item.Message = null; }

                    if (item.CasId > 0)
                    {
                        int famId;

                        // ۱) تذکره — راهِ اصلی و یکتا.
                        if (item.MemberTazkira.Length > 0 && membersByTazkira != null &&
                            membersByTazkira.TryGetValue(item.MemberTazkira, out famId))
                        {
                            item.FamId = famId;
                        }
                        // ۲) نام — فقط پشتیبانِ بسته‌های قدیمی.
                        else if (members != null && members.TryGetValue(item.MemberName, out famId))
                        {
                            item.FamId = famId;
                            item.MatchedByName = true;
                            item.Warnings.Add("این عکس با «نام» تطبیق شد، نه با شماره‌ی تذکره. "
                                + "برای اطمینان، نامِ فایل را به شماره‌ی تذکره‌ی عضو تغییر دهید.");
                        }
                        else
                        {
                            item.Action = MediaAction.NoMatch;
                            item.Message = item.MemberTazkira.Length > 0
                                ? "عضوی با شماره‌ی تذکره‌ی «" + item.MemberTazkira + "» در این پرونده نیست."
                                : "نامِ فایل «" + bare + "» نه شماره‌ی تذکره‌ی معتبری است و نه با نامِ عضوی در این پرونده می‌خواند.";
                        }
                    }

                    if (item.IsApplicable) InspectImage(item);

                    plan.Photos.Add(item);
                }

                done++;
                Report(progress, "بررسی عکس‌های اعضا", done, Math.Max(1, caseFolders.Length));
            }
        }

        // نامِ عضو از نامِ فایل: پسوندِ شناسه در پرانتز (اگر باشد) و فاصله‌های
        // اضافه حذف می‌شوند تا «فرید احمد (4821)» و «فرید احمد» یکی شوند.
        internal static string CleanMemberName(string fileName)
        {
            string name = (fileName ?? "").Trim();

            int paren = name.LastIndexOf('(');
            if (paren > 0) name = name.Substring(0, paren).Trim();

            while (name.Contains("  ")) name = name.Replace("  ", " ");
            return name;
        }

        // شماره‌ی تذکره را به شکلِ یکتا و قابل مقایسه درمی‌آورد.
        // آموزش — چرا نرمال‌سازی لازم است: همان تذکره ممکن است در دیتابیس
        // «۱۴۰۱-۱۲۳۴-۵۶۷۸۹» ثبت شده باشد و در نامِ فایل «1401 1234 56789» یا
        // «1401/1234/56789». ارقامِ فارسی (۰۶۶۰) و عربی (٠٦٦٠) هم دو بازه‌ی
        // یونیکدِ جدا هستند. با تبدیلِ ارقام به لاتین و نگه‌داشتنِ فقط رقم‌ها،
        // هر سه شکل به یک رشته می‌رسند. اگر هیچ رقمی نماند، رشته‌ی خالی
        // برمی‌گردد؛ یعنی «این نامِ فایل تذکره نیست» و تطبیقِ نام امتحان می‌شود.
        public static string NormalizeTazkira(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (c >= '0' && c <= '9') sb.Append(c);                     // لاتین
                else if (c >= '۰' && c <= '۹')                    // فارسی ۰-۹
                    sb.Append((char)('0' + (c - '۰')));
                else if (c >= '٠' && c <= '٩')                    // عربی ٠-٩
                    sb.Append((char)('0' + (c - '٠')));
                // هر چیز دیگر (خط تیره، اسلش، فاصله، حرف) نادیده گرفته می‌شود.
            }

            // صفرهای ابتدایی حذف نمی‌شوند: در تذکره معنادارند و حذفشان
            // می‌تواند دو شماره‌ی متفاوت را یکی نشان دهد.
            return sb.ToString();
        }

        private Dictionary<string, int> LoadMembersByTazkira(int casId)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT FamID, COALESCE(MemberTazkiraNo,'') AS T FROM TblFamily WHERE CasID = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", casId);
                    con.Open();
                    using (SQLiteDataReader rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            string t = NormalizeTazkira(Convert.ToString(rd["T"]));
                            if (t.Length == 0) continue;

                            // اگر دو عضو تذکره‌ی یکسان داشته باشند نگاشت مبهم
                            // است؛ در آن صورت هیچ‌کدام را برنمی‌گردانیم تا عکس
                            // به عضوِ اشتباه نچسبد.
                            if (map.ContainsKey(t)) map[t] = -1;
                            else map[t] = Convert.ToInt32(rd["FamID"]);
                        }
                    }
                }

                var ambiguous = new List<string>();
                foreach (var kv in map) if (kv.Value <= 0) ambiguous.Add(kv.Key);
                foreach (string k in ambiguous) map.Remove(k);
            }
            catch { }
            return map;
        }

        private Dictionary<string, int> LoadMembers(int casId)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT FamID, COALESCE(MemberName,'') AS N FROM TblFamily WHERE CasID = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", casId);
                    con.Open();
                    using (SQLiteDataReader rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            string name = CleanMemberName(Convert.ToString(rd["N"]));
                            if (name.Length == 0) continue;
                            map[name] = Convert.ToInt32(rd["FamID"]);
                        }
                    }
                }
            }
            catch { }
            return map;
        }

        private Dictionary<int, HashSet<string>> LoadExistingDocNames()
        {
            var map = new Dictionary<int, HashSet<string>>();
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT CasID, COALESCE(OriginalFileName,'') AS N FROM TblDocs", con))
                {
                    con.Open();
                    using (SQLiteDataReader rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            int id = Convert.ToInt32(rd["CasID"]);
                            string name = Convert.ToString(rd["N"]).Trim();
                            if (name.Length == 0) continue;

                            if (!map.ContainsKey(id))
                                map[id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            map[id].Add(name);
                        }
                    }
                }
            }
            catch { /* جدول خالی یا در دسترس نیست — تشخیصِ تکراری غیرفعال می‌ماند */ }
            return map;
        }

        // ─── تشخیصِ سرنوشتِ یک فایل ─────────────────────────────────────────
        private void Classify(MediaItem item, MediaPlan plan, Dictionary<string, CaseRef> byCode,
                              HashSet<string> allowedExtensions, Dictionary<string, int> photoCodeCounts)
        {
            string ext = Path.GetExtension(item.SourcePath ?? "");

            if (string.IsNullOrWhiteSpace(item.CaseCode))
            {
                item.Action = MediaAction.InvalidName;
                item.Message = "نام فایل کدِ معتبری ندارد.";
                return;
            }

            if (!allowedExtensions.Contains(ext))
            {
                item.Action = MediaAction.Unsupported;
                item.Message = "پسوندِ «" + ext + "» پشتیبانی نمی‌شود.";
                return;
            }

            try { item.SizeBytes = new FileInfo(item.SourcePath).Length; }
            catch { item.SizeBytes = 0; }

            // چند عکس برای یک کد → هیچ‌کدام خودکار اعمال نمی‌شود.
            if (photoCodeCounts != null)
            {
                int n;
                if (photoCodeCounts.TryGetValue(item.CaseCode, out n) && n > 1)
                {
                    item.Action = MediaAction.Duplicate;
                    item.Message = "برای این کد " + n + " فایل عکس وجود دارد؛ باید یکی بماند.";
                    return;
                }
            }

            CaseRef found;
            string normalizedCode = SyncCodeNormalizer.Normalize(item.CaseCode);
            if (!byCode.TryGetValue(normalizedCode, out found))
            {
                item.Action = MediaAction.NoMatch;
                // آموزش — تشخیص برای کاربر: اگر نرمال‌سازی خودش چیزی را عوض کرده
                // (رقمِ فارسی/فاصله)، همان مقدارِ جست‌وجوشده هم نشان داده می‌شود
                // تا کاربر بفهمد آیا مسئله رقم/فاصله بوده یا واقعاً کد اشتباه است.
                item.Message = normalizedCode == item.CaseCode
                    ? "پرونده‌ای با کد «" + item.CaseCode + "» در دیتابیس نیست."
                    : "پرونده‌ای با کد «" + item.CaseCode + "» (جست‌وجوشده به‌صورت «" + normalizedCode + "») در دیتابیس نیست.";
                return;
            }

            // ── چندمرکزی: فقط پرونده‌های مرکزِ مجاز ──
            if (!SecurityContext.IsAllCenters &&
                SecurityContext.CurrentCenterId > 0 &&
                found.CenterId != SecurityContext.CurrentCenterId)
            {
                item.Action = MediaAction.OtherCenter;
                item.CasId = 0;
                item.Message = "این پرونده متعلق به مرکز دیگری است و در این نشست قابل تغییر نیست.";
                return;
            }

            item.CasId = found.CasId;

            if (item.Kind == MediaKind.Photo && !string.IsNullOrWhiteSpace(found.PhotoPath))
            {
                item.ExistingPath = found.PhotoPath;
                item.Action = MediaAction.Replace;
                item.Message = "این پرونده از قبل عکس دارد؛ فقط با تأیید شما جایگزین می‌شود.";
                // پیش‌فرضِ محافظه‌کارانه: جایگزینی از ابتدا تیک‌خورده نیست.
                item.Selected = false;
                return;
            }

            item.Action = MediaAction.Add;
        }

        // ─── بررسیِ تصویر: ابعاد، اندازه، چرخش ───────────────────────────────
        private void InspectImage(MediaItem item)
        {
            if (item.Kind != MediaKind.Photo) return;

            if (item.SizeBytes > RecommendedMaxPhotoBytes)
            {
                item.Warnings.Add("حجم فایل " + FormatSize(item.SizeBytes) +
                                  " است (بیشتر از ۵ مگابایتِ توصیه‌شده).");
                item.WillCompress = true;
            }

            string ext = Path.GetExtension(item.SourcePath ?? "");
            if (GdiUndecodable.Contains(ext))
            {
                item.Warnings.Add("فرمت webp در این نسخه‌ی ویندوز/دات‌نت قابل رمزگشایی نیست؛ " +
                                  "فایل بدونِ بررسیِ ابعاد، چرخش و فشرده‌سازی کپی می‌شود.");
                item.WillCompress = false;
                return;
            }

            // فقط خواندن — فایل منبع هرگز تغییر نمی‌کند.
            try
            {
                using (var stream = new FileStream(item.SourcePath, FileMode.Open, FileAccess.Read))
                using (Image img = Image.FromStream(stream, false, false))
                {
                    item.PixelWidth = img.Width;
                    item.PixelHeight = img.Height;

                    if (img.Width < RecommendedMinPixels || img.Height < RecommendedMinPixels)
                        item.Warnings.Add("ابعاد " + img.Width + "×" + img.Height +
                                          " کمتر از ۶۰۰×۶۰۰ توصیه‌شده است.");

                    item.NeedsRotation = ImageOrientationHelper.NeedsRotation(img);
                    if (item.NeedsRotation)
                        item.Warnings.Add("جهتِ تصویر بر اساس EXIF اصلاح خواهد شد.");
                }
            }
            catch (Exception ex)
            {
                item.Action = MediaAction.Corrupt;
                item.Message = "تصویر قابل خواندن نیست (احتمالاً خراب است): " + ex.Message;
            }
        }

        // کدهایی که در دیتابیس هستند ولی عکسی برایشان نیامده.
        private void BuildMissingPhotoList(MediaPlan plan, Dictionary<string, CaseRef> byCode)
        {
            var arrived = new HashSet<string>(
                plan.Photos.Where(p => p.CasId > 0).Select(p => p.CaseCode),
                StringComparer.OrdinalIgnoreCase);

            foreach (var kv in byCode)
            {
                if (arrived.Contains(kv.Key)) continue;

                // پرونده‌هایی که از قبل عکس دارند «کمبود» حساب نمی‌شوند.
                if (!string.IsNullOrWhiteSpace(kv.Value.PhotoPath)) continue;

                if (!SecurityContext.IsAllCenters &&
                    SecurityContext.CurrentCenterId > 0 &&
                    kv.Value.CenterId != SecurityContext.CurrentCenterId) continue;

                plan.CasesMissingPhoto.Add(kv.Key);
            }
        }

        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return (bytes / 1024.0 / 1024.0).ToString("N1", CultureInfo.InvariantCulture) + " MB";
            return (bytes / 1024.0).ToString("N0", CultureInfo.InvariantCulture) + " KB";
        }

        private static void Report(IProgress<SyncProgress> progress, string phase, int current, int total)
        {
            if (progress == null) return;
            progress.Report(new SyncProgress { Phase = phase, Current = current, Total = total });
        }
    }
}

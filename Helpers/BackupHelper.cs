using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;

namespace CaseManagement.Helpers
{
    public class BackupHelper
    {
        private const string DataFileName = "CaseManagementBackup.xml";
        private const string RootFileName = "StorageRoot.txt";
        private const string FilesFolderName = "Files";

        private readonly DatabaseHelper db = new DatabaseHelper();

        // ─────────────────────────────────────────────────────────────────────
        // Tier 4 فاز الف — فهرستِ مجازِ کلیدهای SyncState.
        //
        // فقط پیکربندی. هویتِ دستگاه و اعتبارنامه هرگز اینجا اضافه نشوند:
        //   • DeviceGuid    → هویتِ این ماشین نزدِ سرور (HttpSyncTransport).
        //   • RefreshToken  → توکنِ احرازِ هویتِ ذخیره‌شده.
        // بازگرداندنِ این دو روی ماشینِ دیگر یعنی انتقالِ اعتبارنامه و دو دستگاه
        // با یک هویت. کلیدهای تله‌متری (AutoSyncLast*) هم عمداً بیرون‌اند چون
        // وضعیتِ اجرایی همین دستگاه‌اند و روی نصبِ تازه بی‌معنا می‌شوند.
        //
        // این فهرست در دو جا اعمال می‌شود (دفاع در عمق):
        //   ۱) هنگام صادرات — کلیدِ حساس اصلاً واردِ فایلِ بکاپ نمی‌شود.
        //   ۲) هنگام بازیابی — حتی اگر بکاپِ قدیمی/دست‌کاری‌شده‌ای کلیدِ حساس
        //      داشته باشد، بازگردانده نمی‌شود.
        // ─────────────────────────────────────────────────────────────────────
        private static readonly string[] SyncStateAllowedKeys =
        {
            "ServerUrl",
            "AutoSyncEnabled",
            "AutoSyncIntervalMinutes"
        };

        private static string SyncStateAllowedKeysSql
        {
            get
            {
                var quoted = new List<string>();
                foreach (string key in SyncStateAllowedKeys)
                    quoted.Add("'" + key.Replace("'", "''") + "'");
                return string.Join(", ", quoted.ToArray());
            }
        }

        private static bool IsSyncStateKeyAllowed(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            for (int i = 0; i < SyncStateAllowedKeys.Length; i++)
                if (string.Equals(SyncStateAllowedKeys[i], key, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public string ExportBackup(string parentFolder)
        {
            if (string.IsNullOrWhiteSpace(parentFolder))
                throw new Exception("مسیر بکاپ خالی است.");

            string backupFolder = Path.Combine(
                parentFolder,
                "CaseManagementBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            Directory.CreateDirectory(backupFolder);

            string storageRoot = FileHelper.GetBaseRootFolder();

            DataSet dataSet = new DataSet("CaseManagementBackup");

            using (SQLiteConnection con = db.GetConnection())
            {
                // آموزش — باگ از قبل موجود: این اتصال هرگز Open نمی‌شد و
                // ExportBackup همیشه با خطای "Database is not open" شکست می‌خورد.
                con.Open();

                LoadTable(con, dataSet, "TblCenter",    "SELECT * FROM TblCenter");
                LoadTable(con, dataSet, "TblCase",      "SELECT * FROM TblCase");
                LoadTable(con, dataSet, "TblFamily",    "SELECT * FROM TblFamily");
                LoadTable(con, dataSet, "TblDocs",      "SELECT * FROM TblDocs");
                LoadTable(con, dataSet, "TblAssistance","SELECT * FROM TblAssistance");
                // فاز ۲ — پرونده‌های مرتبط و تاریخچه بایگانی؛ بدون این دو، بکاپ
                // این داده‌ها را بی‌صدا از دست می‌داد.
                LoadTable(con, dataSet, "TblCaseRelation",  "SELECT * FROM TblCaseRelation");
                LoadTable(con, dataSet, "TblArchiveHistory","SELECT * FROM TblArchiveHistory");

                // آموزش — رفع باگ: این جداول قبلاً اصلاً در بکاپ نبودند، پس
                // بازیابی فاجعه (نصب تازه) بی‌صدا همه کاربران/تنظیمات/لیست‌های
                // پایه/تاریخچه وضعیت/لاگ را از دست می‌داد. حالا کامل export
                // می‌شوند (نحوه بازیابی هرکدام را در ImportBackup ببینید).
                LoadTable(con, dataSet, "TblUsers",             "SELECT * FROM TblUsers");
                LoadTable(con, dataSet, "TblLookup",            "SELECT * FROM TblLookup");
                LoadTable(con, dataSet, "TblAppSettings",       "SELECT * FROM TblAppSettings");
                LoadTable(con, dataSet, "TblAuditLog",          "SELECT * FROM TblAuditLog");
                LoadTable(con, dataSet, "TblCaseStatusHistory", "SELECT * FROM TblCaseStatusHistory");

                // آموزش — رفعِ افتِ بی‌صدای داده (ادامهٔ همان باگ بالا): سه جدولِ
                // زیر هرگز در بکاپ نبودند، پس بازیابیِ فاجعه تاریخچهٔ وضعیت و نقشِ
                // اعضا و کلِ فهرستِ متقاضیان را از دست می‌داد.
                //   • TblFamilyStatusHistory / TblFamilyRoleHistory — معادلِ
                //     TblCaseStatusHistory ولی در سطحِ عضو.
                //   • TblApplicant + تاریخچه‌اش — متقاضیانی که هنوز به پرونده
                //     تبدیل نشده‌اند و هیچ‌جای دیگری ذخیره نمی‌شوند.
                //   • EntRecordVersion — عکس‌های فوریِ کاملِ رکوردها.
                LoadTableIfExists(con, dataSet, "TblFamilyStatusHistory",    "SELECT * FROM TblFamilyStatusHistory");
                LoadTableIfExists(con, dataSet, "TblFamilyRoleHistory",      "SELECT * FROM TblFamilyRoleHistory");
                LoadTableIfExists(con, dataSet, "TblApplicant",              "SELECT * FROM TblApplicant");
                LoadTableIfExists(con, dataSet, "TblApplicantStatusHistory", "SELECT * FROM TblApplicantStatusHistory");
                LoadTableIfExists(con, dataSet, "EntRecordVersion",          "SELECT * FROM EntRecordVersion");

                // ─── Tier 2: جدول‌های «پیکربندی» ────────────────────────────
                // آموزش — چرا این‌ها تا امروز در بکاپ نبودند و چرا مهم‌اند:
                // این جدول‌ها «داده»ی روزمره نیستند، «تصمیم‌های مدیریتی»اند —
                // ماتریسِ دسترسی، تعریفِ گردش‌کار، زنجیرهٔ تأیید مالی، قواعد،
                // قالب‌های کارت و گزارش. چون EnterpriseInitializer در هر بار
                // اجرا مقادیرِ پیش‌فرض را دوباره seed می‌کند، نبودشان در بکاپ
                // «خطا» تولید نمی‌کرد — سیستم بعدِ بازیابی سالم به‌نظر می‌رسید
                // ولی هر تصمیمِ مدیر بی‌صدا به پیش‌فرضِ کارخانه برگشته بود.
                // خطرناک‌ترین حالت: مجوزی که مدیر عمداً سلب کرده بود، دوباره
                // اعطا می‌شد (بالا رفتنِ سطحِ دسترسی) بدون هیچ هشداری.
                // همه با LoadTableIfExists خوانده می‌شوند تا نصب‌هایی که هنوز
                // ماژولِ مربوطه را راه‌اندازی نکرده‌اند بی‌صدا رد شوند.

                // دسترسی و مجوزها
                LoadTableIfExists(con, dataSet, "EntPermission",     "SELECT * FROM EntPermission");
                LoadTableIfExists(con, dataSet, "EntRolePermission", "SELECT * FROM EntRolePermission");
                LoadTableIfExists(con, dataSet, "EntUserPermission", "SELECT * FROM EntUserPermission");
                LoadTableIfExists(con, dataSet, "EntModule",         "SELECT * FROM EntModule");
                LoadTableIfExists(con, dataSet, "EntRoleModule",     "SELECT * FROM EntRoleModule");
                LoadTableIfExists(con, dataSet, "EntUserModule",     "SELECT * FROM EntUserModule");

                // تعریفِ گردش‌کار و تأیید و قواعد
                LoadTableIfExists(con, dataSet, "EntWorkflow",           "SELECT * FROM EntWorkflow");
                LoadTableIfExists(con, dataSet, "EntWorkflowState",      "SELECT * FROM EntWorkflowState");
                LoadTableIfExists(con, dataSet, "EntWorkflowTransition", "SELECT * FROM EntWorkflowTransition");
                LoadTableIfExists(con, dataSet, "EntApprovalChain",      "SELECT * FROM EntApprovalChain");
                LoadTableIfExists(con, dataSet, "EntApprovalLevel",      "SELECT * FROM EntApprovalLevel");
                LoadTableIfExists(con, dataSet, "EntRule",               "SELECT * FROM EntRule");

                // قالب‌ها و تعریف‌های کاربرساخته
                LoadTableIfExists(con, dataSet, "TblCardTemplate",          "SELECT * FROM TblCardTemplate");
                LoadTableIfExists(con, dataSet, "TblCardTemplateVersion",   "SELECT * FROM TblCardTemplateVersion");
                LoadTableIfExists(con, dataSet, "TblAssistancePackage",     "SELECT * FROM TblAssistancePackage");
                LoadTableIfExists(con, dataSet, "TblAssistancePackageItem", "SELECT * FROM TblAssistancePackageItem");
                LoadTableIfExists(con, dataSet, "TblReportTemplate",        "SELECT * FROM TblReportTemplate");
                LoadTableIfExists(con, dataSet, "TblScheduledReport",       "SELECT * FROM TblScheduledReport");

                // دادهٔ کاربری که هیچ خانهٔ دیگری ندارد
                LoadTableIfExists(con, dataSet, "TblCaseTransferHistory", "SELECT * FROM TblCaseTransferHistory");
                LoadTableIfExists(con, dataSet, "TblReminder",            "SELECT * FROM TblReminder");

                // ماژولِ اداری/کارمندان (Adm*) — تا پیش از این هیچ پوششی نداشت.
                LoadTableIfExists(con, dataSet, "AdmEmployee", "SELECT * FROM AdmEmployee");
                LoadTableIfExists(con, dataSet, "AdmLeave",    "SELECT * FROM AdmLeave");
                LoadTableIfExists(con, dataSet, "AdmMission",  "SELECT * FROM AdmMission");

                // ─── Tier 3: دادهٔ عملیاتیِ در جریان + تکمیلِ ماژولِ اداری ────
                // آموزش — تفاوتِ این‌ها با Tier 2: آن‌ها «پیکربندی» بودند
                // (تعریفِ گردش‌کار)، این‌ها «وضعیتِ در جریان»اند (کدام پرونده
                // الان کجای همان گردش‌کار است، چه کسی چه وظیفه‌ای دارد، چه
                // تأییدی منتظر امضاست). بدونشان بازیابی، پرونده‌ها را برمی‌گرداند
                // ولی همه بی‌صدا «بدون گردش‌کار» می‌شوند و صفِ کاری کارکنان و
                // تأییدهای مالیِ منتظر کاملاً از بین می‌رود.
                LoadTableIfExists(con, dataSet, "EntWorkflowInstance", "SELECT * FROM EntWorkflowInstance");
                LoadTableIfExists(con, dataSet, "EntWorkflowHistory",  "SELECT * FROM EntWorkflowHistory");
                LoadTableIfExists(con, dataSet, "EntApprovalRequest",  "SELECT * FROM EntApprovalRequest");
                LoadTableIfExists(con, dataSet, "EntApprovalAction",   "SELECT * FROM EntApprovalAction");
                LoadTableIfExists(con, dataSet, "EntTask",             "SELECT * FROM EntTask");

                LoadTableIfExists(con, dataSet, "AdmJobApplication",  "SELECT * FROM AdmJobApplication");
                LoadTableIfExists(con, dataSet, "AdmDriverContract",  "SELECT * FROM AdmDriverContract");

                // ─── Tier 4 فاز ب: لاگ‌های سازمانی ──────────────────────────
                // EntSecurityEvent مهم‌ترینِ این سه است: تنها جای سیستم که
                // ردِ «چه کسی، کِی، به چه چیزی دسترسی نداشت» را نگه می‌دارد.
                // بدونش، بعد از یک حادثه هیچ سابقه‌ای برای بررسی نمی‌ماند.
                LoadTableIfExists(con, dataSet, "EntSecurityEvent", "SELECT * FROM EntSecurityEvent");
                LoadTableIfExists(con, dataSet, "EntErrorLog",      "SELECT * FROM EntErrorLog");
                LoadTableIfExists(con, dataSet, "EntRuleLog",       "SELECT * FROM EntRuleLog");

                // ─── Tier 4 فاز الف: SyncState — فقط کلیدهای مجاز ────────────
                // آموزش — این تنها جدولی در کل سیستم است که «همه یا هیچ» برایش
                // غلط است. SyncState یک جدولِ کلید/مقدار است که سه نوع دادهٔ
                // کاملاً متفاوت را کنار هم نگه می‌دارد:
                //   • پیکربندی (ServerUrl و تنظیماتِ همگام‌سازیِ خودکار) —
                //     نبودشان یعنی دفتر بعدِ بازیابی نمی‌تواند به سرور وصل شود.
                //   • هویتِ دستگاه (DeviceGuid) — اگر روی دستگاهِ دیگری بازیابی
                //     شود، دو ماشین یک هویت را ادعا می‌کنند.
                //   • اعتبارنامه (RefreshToken) — یک توکنِ احرازِ هویتِ واقعی.
                // پس فیلتر در همین کوئری اعمال می‌شود، نه موقعِ بازیابی: کلیدهای
                // حساس اصلاً واردِ فایلِ بکاپ نمی‌شوند — حتی به‌صورتِ رمزنگاری‌شده.
                // فهرست عمداً «مجاز» است نه «ممنوع»: اگر بعداً کسی کلیدِ تازه‌ای
                // اضافه کند، به‌صورتِ پیش‌فرض بیرون می‌ماند، نه اینکه سهواً صادر شود.
                LoadTableIfExists(con, dataSet, "SyncState",
                    "SELECT * FROM SyncState WHERE StateKey IN (" + SyncStateAllowedKeysSql + ")");
            }

            dataSet.WriteXml(Path.Combine(backupFolder, DataFileName), XmlWriteMode.WriteSchema);
            File.WriteAllText(Path.Combine(backupFolder, RootFileName), storageRoot ?? "");

            if (!string.IsNullOrWhiteSpace(storageRoot) && Directory.Exists(storageRoot))
            {
                string backupFilesFolder = Path.Combine(backupFolder, FilesFolderName);
                CopyDirectory(storageRoot, backupFilesFolder, backupFolder, true);
            }

            return backupFolder;
        }

        // نتیجه Import که به UI برمی‌گردانیم
        public class ImportResult
        {
            public int CasesInserted;
            public int CasesSkipped;
        }

        public ImportResult ImportBackup(string backupFolder)
        {
            if (string.IsNullOrWhiteSpace(backupFolder))
                throw new Exception("مسیر بکاپ خالی است.");

            string dataPath = Path.Combine(backupFolder, DataFileName);
            if (!File.Exists(dataPath))
                throw new Exception("فایل اطلاعات بکاپ پیدا نشد: " + dataPath);

            DataSet dataSet = new DataSet();
            dataSet.ReadXml(dataPath, XmlReadMode.ReadSchema);

            EnsureTable(dataSet, "TblCase");
            EnsureTable(dataSet, "TblFamily");
            EnsureTable(dataSet, "TblDocs");

            string oldRoot = "";
            string rootPath = Path.Combine(backupFolder, RootFileName);
            if (File.Exists(rootPath))
                oldRoot = File.ReadAllText(rootPath);

            string newRoot = FileHelper.GetOrChooseBaseRootFolder();
            if (string.IsNullOrWhiteSpace(newRoot))
                throw new Exception("محل ذخیره فایل‌های برنامه مشخص نیست.");

            RemapStoredFilePaths(dataSet, oldRoot, newRoot);

            DataTable caseTable = dataSet.Tables["TblCase"];
            bool hasGlobalId = caseTable.Columns.Contains("GlobalID");

            // آموزش — رفع باگ بحرانی: بکاپ‌های قدیمی (بدون GlobalID) از مسیر
            // "کلاسیک" بازیابی می‌شوند که تمام داده‌های TblCase/Family/Docs/
            // Assistance را برای کل سیستم (همه مراکز) پاک و جایگزین می‌کند —
            // نه فقط مرکز کاربر جاری. قبل از قابلیت چندمرکزی این رفتار درست
            // بود؛ حالا اگر یک کاربر مرکز غیر-SuperAdmin چنین بکاپی را وارد
            // کند، داده‌های همه مراکز دیگر نابود می‌شود. فقط SuperAdmin مجاز
            // به این بازیابی مخرب کل‌سیستمی است. مسیر GlobalID (merge هوشمند
            // و افزایشی) برای همه کاربران دارای دسترسی ویرایش امن باقی می‌ماند.
            if (!hasGlobalId && !SecurityContext.IsSuperAdmin())
                throw new Exception(
                    "این بکاپ قدیمی است و بازیابی آن تمام داده‌های تمام مراکز را پاک و جایگزین می‌کند. " +
                    "این عملیات فقط برای مدیر کل (SuperAdmin) مجاز است.");

            var result = new ImportResult();

            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();

                // ─── تشخیصِ حالتِ بازیابی، پیش از هر نوشتنی ──────────────────
                // آموزش — چرا این لازم است: جدول‌های پیکربندی (ماتریسِ دسترسی،
                // گردش‌کار، ماژول‌ها...) در هر بار اجرای برنامه توسط
                // EnterpriseInitializer با INSERT OR IGNORE دوباره seed می‌شوند.
                // یعنی موقعِ بازیابی هرگز «خالی» نیستند و همهٔ کلیدهایشان از قبل
                // وجود دارد. پس هر دو ترفندی که در Tier 1 کار کرد، اینجا بی‌صدا
                // شکست می‌خورد:
                //   • RestoreWholeTableIfEmpty → چون خالی نیست، رد می‌کند.
                //   • INSERT OR IGNORE        → چون کلید هست، هیچ‌کاری نمی‌کند.
                // نتیجه در هر دو حالت: بازیابی «موفق» گزارش می‌شود ولی ماتریسِ
                // دسترسی به پیش‌فرضِ کارخانه برگشته است.
                //
                // پس به یک سیگنالِ دیگر نیاز داریم که برای جدول‌های seed‌شده هم
                // کار کند: آیا این یک نصبِ تازه است یا یک ادغام؟ خالی‌بودنِ
                // TblCase دقیقاً همین را می‌گوید (TblCase هرگز seed نمی‌شود):
                //   • خالی → بازیابیِ فاجعه روی نصبِ تازه → پیکربندی باید کامل
                //     جایگزین شود تا بر مقادیرِ پیش‌فرضِ تازه‌seed‌شده غلبه کند.
                //   • پر   → ادغامِ بکاپِ دفترِ دیگر در یک سیستمِ زنده →
                //     پیکربندیِ محلی نباید لمس شود (تصمیمِ تأییدشدهٔ کاربر).
                // حتماً باید قبل از شروعِ نوشتن خوانده شود، وگرنه بعدِ درجِ
                // اولین پرونده دیگر «خالی» نیست.
                bool isFreshInstall;
                using (var probe = new SQLiteCommand("SELECT COUNT(1) FROM TblCase", con))
                    isFreshInstall = Convert.ToInt32(probe.ExecuteScalar()) == 0;

                using (var tr = con.BeginTransaction())
                {
                    try
                    {
                        // ── مراکز: INSERT OR IGNORE (بر اساس UNIQUE CenterCode) ──────
                        if (dataSet.Tables.Contains("TblCenter"))
                            MergeCenters(con, tr, dataSet.Tables["TblCenter"]);

                        // ── کاربران/لیست‌های پایه: INSERT OR IGNORE (رفع باگ جاافتادگی
                        // در بکاپ قدیمی) — افزایشی و بی‌خطر: حساب/مقدار موجود دست
                        // نمی‌خورد (رمز عبور کاربر جاری یا تنظیمات محلی خراب نمی‌شود)،
                        // فقط موارد جدید/گمشده (مثلاً در بازیابی روی نصب تازه) اضافه می‌شوند.
                        if (dataSet.Tables.Contains("TblUsers"))
                            MergeUsers(con, tr, dataSet.Tables["TblUsers"]);
                        if (dataSet.Tables.Contains("TblLookup"))
                            MergeLookup(con, tr, dataSet.Tables["TblLookup"]);

                        // آموزش — رفعِ باگِ C-2: TblAppSettings از خط ۵۶ در بکاپ
                        // نوشته می‌شد ولی در هیچ‌یک از دو مسیرِ بازیابی خوانده
                        // نمی‌شد. یعنی اپراتور آن را در خروجیِ VerifyEncryptedBackup
                        // می‌دید و منطقاً نتیجه می‌گرفت محافظت شده است — در حالی
                        // که نبود. این از «اصلاً بکاپ‌نگرفتن» بدتر است.
                        // همان الگویِ MergeLookup: INSERT OR IGNORE روی کلیدِ
                        // طبیعیِ SettingKey، پس تنظیماتِ زندهٔ نصبِ مقصد هرگز
                        // رونویسی نمی‌شود (رمزِ بکاپِ خودکار، مسیرها، و بقیهٔ
                        // مقادیرِ محلی دست‌نخورده می‌مانند) و فقط کلیدهای گمشده
                        // — یعنی حالتِ نصبِ تازه/بازیابیِ فاجعه — پر می‌شوند.
                        if (dataSet.Tables.Contains("TblAppSettings"))
                            MergeAppSettings(con, tr, dataSet.Tables["TblAppSettings"]);

                        // Tier 4 فاز الف — پیکربندیِ همگام‌سازی. مثل TblAppSettings
                        // در هر دو مسیر (کامل و ادغام) اجرا می‌شود؛ توضیحِ رفتار
                        // در خودِ متد.
                        if (dataSet.Tables.Contains("SyncState"))
                            MergeSyncState(con, tr, dataSet.Tables["SyncState"]);

                        if (hasGlobalId)
                        {
                            // ── حالت هوشمند: Merge بر اساس GlobalID ──────────────────
                            var casIdMap = new Dictionary<int, int>(); // origCasId → newCasId
                            var newlyInsertedOrigIds = new HashSet<int>();

                            foreach (DataRow row in caseTable.Rows)
                            {
                                string guid = row["GlobalID"] == DBNull.Value ? "" : row["GlobalID"].ToString();
                                int origId  = Convert.ToInt32(row["CasID"]);

                                if (!string.IsNullOrWhiteSpace(guid))
                                {
                                    // بررسی تکراری بودن بر اساس GlobalID
                                    int existingId = GetCasIdByGlobalId(con, tr, guid);
                                    if (existingId > 0)
                                    {
                                        casIdMap[origId] = existingId;
                                        result.CasesSkipped++;
                                        continue;
                                    }
                                }

                                int newId = InsertCaseRow(con, tr, caseTable, row);
                                casIdMap[origId] = newId;
                                newlyInsertedOrigIds.Add(origId);
                                result.CasesInserted++;
                            }

                            // جداول فرزند: فقط ردیف‌هایی که CasID آن‌ها در casIdMap جدید هستند
                            var famIdMap = new Dictionary<int, int>();
                            MergeChildTable(con, tr, dataSet.Tables["TblFamily"],    "FamID",        casIdMap, famIdMap);
                            MergeChildTable(con, tr, dataSet.Tables["TblDocs"],      "DocID",        casIdMap);
                            if (dataSet.Tables.Contains("TblAssistance"))
                                MergeChildTable(con, tr, dataSet.Tables["TblAssistance"], "AssistanceID", casIdMap);

                            // آموزش — تاریخچه وضعیت فاقد GlobalID است (کلید تشخیص
                            // تکراری ندارد)، پس فقط برای پرونده‌های تازه‌درج‌شده منتقل
                            // می‌شود؛ برای پرونده‌های از‌قبل‌موجود (Skip شده) وارد
                            // نمی‌شود تا تاریخچه در آن سمت تکراری نشود.
                            if (dataSet.Tables.Contains("TblCaseStatusHistory"))
                                MergeCaseStatusHistory(con, tr, dataSet.Tables["TblCaseStatusHistory"], casIdMap, newlyInsertedOrigIds);

                            // تاریخچهٔ سطحِ عضو — همان منطق، ولی با نگاشتِ FamID.
                            // این دو جدول هم GlobalID ندارند، پس فقط برای اعضایی
                            // منتقل می‌شوند که در همین بازیابی واقعاً درج شده‌اند
                            // (کلیدهای موجود در famIdMap) و امکانِ تکرار نیست.
                            if (dataSet.Tables.Contains("TblFamilyStatusHistory"))
                                MergeFamilyHistory(con, tr, dataSet.Tables["TblFamilyStatusHistory"],
                                    "TblFamilyStatusHistory", "FamID", famIdMap);

                            if (dataSet.Tables.Contains("TblFamilyRoleHistory"))
                                MergeFamilyHistory(con, tr, dataSet.Tables["TblFamilyRoleHistory"],
                                    "TblFamilyRoleHistory", "FamilyMemberID", famIdMap);

                            // فاز ۲ — پرونده‌های مرتبط: هر دو سرِ رابطه باید
                            // remap شوند، وگرنه رابطه به پرونده‌ی اشتباه وصل می‌شود.
                            if (dataSet.Tables.Contains("TblCaseRelation"))
                                MergeCaseRelations(con, tr, dataSet.Tables["TblCaseRelation"], casIdMap);

                            if (dataSet.Tables.Contains("TblArchiveHistory"))
                                MergeArchiveHistory(con, tr, dataSet.Tables["TblArchiveHistory"], casIdMap, newlyInsertedOrigIds);

                            // آموزش — رفعِ باگِ C-3: این جدول‌ها فقط در مسیرِ
                            // «کلاسیک» بازیابی می‌شدند (خطوطِ پایین‌تر)، ولی مسیرِ
                            // کلاسیک امروز عملاً مرده است: از وقتی DatabaseInitializer
                            // به هر نصبی GlobalID می‌دهد، هر بکاپی که با نسخهٔ فعلی
                            // گرفته شود از همین مسیرِ merge بازیابی می‌شود. یعنی
                            // پوشش‌دارترین مسیر، همانی بود که دیگر کسی به آن نمی‌رسید.
                            //
                            // چرا اینجا «فقط اگر خالی باشد»؟ این سه جدول (و
                            // TblAuditLog) نه GlobalID دارند نه هیچ کلیدِ طبیعیِ
                            // یکتا، پس درجِ سادهٔ آن‌ها در یک دیتابیسِ پر، هر بار
                            // ردیف‌ها را تکراری می‌کرد. شرطِ خالی‌بودن دقیقاً دو
                            // سناریو را از هم جدا می‌کند:
                            //   • نصبِ تازه بعدِ فاجعه → جدول خالی است → داده
                            //     برمی‌گردد (همان چیزی که تا امروز بی‌صدا گم می‌شد).
                            //   • ادغامِ بکاپِ دفترِ دیگر در یک دیتابیسِ زنده →
                            //     جدول پر است → دست نمی‌خورد، هیچ تکراری ساخته
                            //     نمی‌شود (دقیقاً رفتارِ فعلی، بدون تغییر).
                            // پس این تغییر فقط در حالتی که تا حالا داده گم می‌شد
                            // اثر دارد، و در حالتِ ادغام رفتارِ قبلی را حفظ می‌کند.
                            RestoreWholeTableIfEmpty(con, tr, dataSet, "TblApplicant");
                            RestoreWholeTableIfEmpty(con, tr, dataSet, "TblApplicantStatusHistory");
                            RestoreWholeTableIfEmpty(con, tr, dataSet, "TblAuditLog");

                            // Tier 2/3 — پیکربندی و دادهٔ عملیاتی (فقط روی نصبِ
                            // تازه؛ توضیح در خودِ متد). در این مسیر شناسه‌ها
                            // بازتخصیص می‌شوند، پس نگاشت‌ها را می‌سازیم و
                            // می‌فرستیم تا ارجاع‌ها ترجمه شوند نه کپی.
                            RestoreMaps maps = BuildRestoreMaps(con, tr, dataSet, casIdMap, famIdMap);
                            RestoreConfigurationTables(con, tr, dataSet, isFreshInstall, maps);
                        }
                        else
                        {
                            // ── حالت کلاسیک: پاک کردن و وارد کردن (بکاپ‌های قدیمی) ──
                            DeleteCurrentData(con, tr);
                            InsertTable(con, tr, "TblCase",   caseTable);
                            InsertTable(con, tr, "TblFamily", dataSet.Tables["TblFamily"]);
                            InsertTable(con, tr, "TblDocs",   dataSet.Tables["TblDocs"]);
                            if (dataSet.Tables.Contains("TblAssistance"))
                                InsertTable(con, tr, "TblAssistance", dataSet.Tables["TblAssistance"]);
                            if (dataSet.Tables.Contains("TblCaseStatusHistory"))
                            {
                                ExecuteNonQuery(con, tr, "DELETE FROM TblCaseStatusHistory");
                                InsertTable(con, tr, "TblCaseStatusHistory", dataSet.Tables["TblCaseStatusHistory"]);
                            }

                            // آموزش — در حالتِ کلاسیک شناسه‌های اصلی حفظ می‌شوند
                            // (پاک‌کردن و درجِ مستقیم)، پس هیچ نگاشتی لازم نیست.
                            // این دقیقاً حالتِ «بازیابیِ فاجعه» است — همان جایی که
                            // نبودِ این جداول در بکاپ بیشترین ضرر را می‌زد.
                            RestoreWholeTable(con, tr, dataSet, "TblFamilyStatusHistory");
                            RestoreWholeTable(con, tr, dataSet, "TblFamilyRoleHistory");
                            RestoreWholeTable(con, tr, dataSet, "TblApplicant");
                            RestoreWholeTable(con, tr, dataSet, "TblApplicantStatusHistory");
                            // آموزش — نیمهٔ دومِ باگِ C-2: TblAuditLog هم مثلِ
                            // TblAppSettings export می‌شد ولی هیچ‌جا بازیابی
                            // نمی‌شد. اینجا (مسیرِ کلاسیک = جایگزینیِ کامل)
                            // بازیابیِ بی‌قیدوشرط درست است، چون خودِ این مسیر
                            // اساساً «پاک کن و جایگزین کن» است.
                            RestoreWholeTable(con, tr, dataSet, "TblAuditLog");

                            // Tier 2/3 — پیکربندی و دادهٔ عملیاتی. همان قاعدهٔ
                            // مسیرِ merge اعمال می‌شود تا رفتار در هر دو مسیر
                            // یکسان و قابلِ‌توضیح بماند.
                            // نگاشت = null: در مسیرِ کلاسیک شناسه‌های اصلی حفظ
                            // می‌شوند (درجِ مستقیم با کلیدِ اصلی)، پس هیچ ترجمه‌ای
                            // لازم نیست و ارجاع‌ها همان‌طور که بودند درست می‌مانند.
                            RestoreConfigurationTables(con, tr, dataSet, isFreshInstall, null);
                            // در این حالت CasID اصلی حفظ می‌شود، پس درج مستقیم درست است.
                            if (dataSet.Tables.Contains("TblCaseRelation"))
                                InsertTable(con, tr, "TblCaseRelation", dataSet.Tables["TblCaseRelation"]);
                            if (dataSet.Tables.Contains("TblArchiveHistory"))
                                InsertTable(con, tr, "TblArchiveHistory", dataSet.Tables["TblArchiveHistory"]);
                            result.CasesInserted = caseTable.Rows.Count;
                        }

                        tr.Commit();
                    }
                    catch
                    {
                        try { tr.Rollback(); } catch { }
                        throw;
                    }
                }
            }

            string backupFilesFolder = Path.Combine(backupFolder, FilesFolderName);
            if (Directory.Exists(backupFilesFolder))
                CopyDirectory(backupFilesFolder, newRoot, "");

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // نسخهٔ رمزنگاری‌شده (نسخهٔ ۱٫۰ — Option D از ENCRYPTION_ARCHITECTURE_REVIEW.md):
        // ExportBackup/ImportBackup بالا کاملاً دست‌نخورده می‌مانند — هم برای
        // بازیابیِ بکاپ‌های قدیمیِ رمزنگاری‌نشده، هم به‌عنوان منطقِ داخلیِ
        // اینجا. این دو متد فقط یک لایهٔ Zip+رمزنگاریِ AES-256 دورشان
        // می‌کشند؛ خروجیِ نهایی یک فایلِ تکیِ رمزنگاری‌شده است، نه یک پوشهٔ باز.
        // ─────────────────────────────────────────────────────────────────────
        public string ExportEncryptedBackup(string parentFolder, string password)
        {
            string plainFolder = null;
            string zipPath = null;

            try
            {
                plainFolder = ExportBackup(parentFolder);
                zipPath = plainFolder + ".zip";
                string encryptedPath = plainFolder + BackupEncryption.EncryptedExtension;

                ZipFile.CreateFromDirectory(plainFolder, zipPath, CompressionLevel.Optimal, false);
                BackupEncryption.EncryptFile(zipPath, encryptedPath, password);

                AuditLogger.Log("بکاپ رمزنگاری‌شده", "Backup", 0, "", encryptedPath);
                return encryptedPath;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("خطا در بکاپ رمزنگاری‌شده", "Backup", 0, "", ex.Message);
                throw;
            }
            finally
            {
                // آموزش — «بدون فایلِ متنِ‌سادهٔ باقی‌مانده»: چه موفق چه ناموفق،
                // هم پوشهٔ خامِ اولیه و هم Zipِ میانی حذف می‌شوند؛ فقط فایلِ
                // رمزنگاری‌شدهٔ نهایی مجاز است روی دیسک بماند.
                if (plainFolder != null) BackupEncryption.TryDeleteDirectory(plainFolder);
                if (zipPath != null) BackupEncryption.TryDelete(zipPath);
            }
        }

        public ImportResult ImportEncryptedBackup(string encryptedFilePath, string password)
        {
            if (string.IsNullOrWhiteSpace(encryptedFilePath) || !File.Exists(encryptedFilePath))
                throw new Exception("فایل بکاپ رمزنگاری‌شده پیدا نشد.");

            string tempZip = Path.Combine(Path.GetTempPath(), "CMRestore_" + Guid.NewGuid().ToString("N") + ".zip");
            string tempFolder = Path.Combine(Path.GetTempPath(), "CMRestore_" + Guid.NewGuid().ToString("N"));

            try
            {
                BackupEncryption.DecryptFile(encryptedFilePath, tempZip, password);
                ZipFile.ExtractToDirectory(tempZip, tempFolder);

                ImportResult result = ImportBackup(tempFolder);
                AuditLogger.Log("بازیابیِ بکاپ رمزنگاری‌شده", "Backup", 0, "", encryptedFilePath);
                return result;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("خطا در بازیابیِ بکاپ رمزنگاری‌شده", "Backup", 0, "", ex.Message);
                throw;
            }
            finally
            {
                BackupEncryption.TryDelete(tempZip);
                BackupEncryption.TryDeleteDirectory(tempFolder);
            }
        }

        // بررسیِ صحت/رمزِ یک بکاپِ رمزنگاری‌شده، کاملاً فقط‌خواندنی — هیچ
        // تغییری در دیتابیسِ زندهٔ فعلی اعمال نمی‌شود، فقط XMLِ بکاپ به یک
        // DataSetِ موقت خوانده می‌شود تا شمارِ ردیفِ هر جدول گزارش شود.
        public DataSet VerifyEncryptedBackup(string encryptedFilePath, string password)
        {
            if (string.IsNullOrWhiteSpace(encryptedFilePath) || !File.Exists(encryptedFilePath))
                throw new Exception("فایل بکاپ رمزنگاری‌شده پیدا نشد.");

            string tempZip = Path.Combine(Path.GetTempPath(), "CMVerify_" + Guid.NewGuid().ToString("N") + ".zip");
            string tempFolder = Path.Combine(Path.GetTempPath(), "CMVerify_" + Guid.NewGuid().ToString("N"));

            try
            {
                BackupEncryption.DecryptFile(encryptedFilePath, tempZip, password);
                ZipFile.ExtractToDirectory(tempZip, tempFolder);

                string xmlPath = Path.Combine(tempFolder, DataFileName);
                if (!File.Exists(xmlPath))
                    throw new Exception("فایل CaseManagementBackup.xml در این بکاپ پیدا نشد.");

                DataSet ds = new DataSet();
                ds.ReadXml(xmlPath);
                return ds;
            }
            finally
            {
                BackupEncryption.TryDelete(tempZip);
                BackupEncryption.TryDeleteDirectory(tempFolder);
            }
        }

        // ── مراکز: INSERT OR IGNORE ──────────────────────────────────────────
        // آموزش — GetVal با ستون گمشده در DataTable مقدار پیش‌فرض برمی‌گرداند،
        // پس بکاپ‌های قدیمی‌تر (بدون ستون‌های جدید مرکز) هم بدون خطا merge
        // می‌شوند؛ فقط فیلدهای جدید خالی می‌مانند.
        private static void MergeCenters(SQLiteConnection con, SQLiteTransaction tr, DataTable table)
        {
            foreach (DataRow row in table.Rows)
            {
                using (var cmd = new SQLiteCommand(@"
INSERT OR IGNORE INTO TblCenter
    (CenterCode, CenterName, IsActive, Province, Address, Phone, ManagerName, Email, LogoPath, Color)
VALUES
    (@Code, @Name, @Active, @Province, @Address, @Phone, @Manager, @Email, @Logo, @Color)", con, tr))
                {
                    cmd.Parameters.AddWithValue("@Code",     GetVal(row, "CenterCode", ""));
                    cmd.Parameters.AddWithValue("@Name",     GetVal(row, "CenterName", ""));
                    cmd.Parameters.AddWithValue("@Active",   GetVal(row, "IsActive", 1));
                    cmd.Parameters.AddWithValue("@Province", GetVal(row, "Province", ""));
                    cmd.Parameters.AddWithValue("@Address",  GetVal(row, "Address", ""));
                    cmd.Parameters.AddWithValue("@Phone",    GetVal(row, "Phone", ""));
                    cmd.Parameters.AddWithValue("@Manager",  GetVal(row, "ManagerName", ""));
                    cmd.Parameters.AddWithValue("@Email",    GetVal(row, "Email", ""));
                    cmd.Parameters.AddWithValue("@Logo",     GetVal(row, "LogoPath", ""));
                    cmd.Parameters.AddWithValue("@Color",    GetVal(row, "Color", ""));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── کاربران: INSERT OR IGNORE بر اساس UNIQUE Username ────────────────
        // آموزش: روی نصب تازه (دیتابیس خالی) همه کاربران بازیابی می‌شوند؛ روی
        // دیتابیسی که از قبل کاربر دارد، حساب‌های موجود دست‌نخورده می‌مانند
        // (رمز عبور/جلسه کاربر جاری خراب نمی‌شود).
        private static void MergeUsers(SQLiteConnection con, SQLiteTransaction tr, DataTable table)
        {
            foreach (DataRow row in table.Rows)
            {
                var cols = new List<string>();
                var pnames = new List<string>();
                using (var cmd = new SQLiteCommand())
                {
                    cmd.Connection = con;
                    cmd.Transaction = tr;
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        string col = table.Columns[i].ColumnName;
                        if (string.Equals(col, "UserID", StringComparison.OrdinalIgnoreCase))
                            continue;
                        cols.Add("[" + col + "]");
                        string pname = "@u" + i;
                        pnames.Add(pname);
                        object v = row[col];
                        cmd.Parameters.AddWithValue(pname, v == null ? DBNull.Value : v);
                    }
                    cmd.CommandText =
                        "INSERT OR IGNORE INTO TblUsers (" + string.Join(",", cols.ToArray()) + ")" +
                        " VALUES (" + string.Join(",", pnames.ToArray()) + ")";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── لیست‌های پایه: INSERT OR IGNORE بر اساس UNIQUE(Category, Value) ──
        private static void MergeLookup(SQLiteConnection con, SQLiteTransaction tr, DataTable table)
        {
            foreach (DataRow row in table.Rows)
            {
                using (var cmd = new SQLiteCommand(@"
INSERT OR IGNORE INTO TblLookup (Category, Value, SortOrder, IsActive)
VALUES (@Category, @Value, @SortOrder, @IsActive)", con, tr))
                {
                    cmd.Parameters.AddWithValue("@Category",  GetVal(row, "Category", ""));
                    cmd.Parameters.AddWithValue("@Value",     GetVal(row, "Value", ""));
                    cmd.Parameters.AddWithValue("@SortOrder", GetVal(row, "SortOrder", 0));
                    cmd.Parameters.AddWithValue("@IsActive",  GetVal(row, "IsActive", 1));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── تنظیمات برنامه: INSERT OR IGNORE بر اساس PRIMARY KEY یعنی SettingKey ──
        // آموزش: هم‌الگویِ MergeUsers/MergeLookup — روی نصبِ تازه همهٔ تنظیمات
        // برمی‌گردند؛ روی نصبی که از قبل تنظیمات دارد، مقادیرِ موجود دست‌نخورده
        // می‌مانند. ستون‌ها پویا از خودِ DataTable خوانده می‌شوند (همان درسی که
        // در MergeFamilyHistory گرفته شد) تا اگر بعداً ستونی به TblAppSettings
        // اضافه شود، دوباره باگِ «افتِ ستون» تکرار نشود.
        private static void MergeAppSettings(SQLiteConnection con, SQLiteTransaction tr, DataTable table)
        {
            if (table == null || table.Rows.Count == 0) return;
            if (!table.Columns.Contains("SettingKey")) return;

            foreach (DataRow row in table.Rows)
            {
                var cols = new List<string>();
                var pnames = new List<string>();
                using (var cmd = new SQLiteCommand())
                {
                    cmd.Connection = con;
                    cmd.Transaction = tr;
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        string col = table.Columns[i].ColumnName;
                        cols.Add("[" + col + "]");
                        string pname = "@s" + i;
                        pnames.Add(pname);
                        object v = row[col];
                        cmd.Parameters.AddWithValue(pname, v == null ? DBNull.Value : v);
                    }
                    cmd.CommandText =
                        "INSERT OR IGNORE INTO TblAppSettings (" + string.Join(",", cols.ToArray()) + ")" +
                        " VALUES (" + string.Join(",", pnames.ToArray()) + ")";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── Tier 4 فاز الف: پیکربندیِ همگام‌سازی (SyncState) ──────────────────
        // آموزش — چرا INSERT OR IGNORE و چرا در هر دو مسیر:
        // SyncState کلیدِ اصلیِ متنی دارد (StateKey)، پس دقیقاً همان الگوی
        // MergeLookup/MergeAppSettings اینجا هم درست است و «هر دو حالتِ بازیابی»
        // را پوشش می‌دهد:
        //   • بازیابیِ کامل روی نصبِ تازه → کلیدها وجود ندارند → آدرسِ سرور و
        //     تنظیماتِ همگام‌سازی برمی‌گردند و دفتر بلافاصله قابلِ اتصال است.
        //   • ادغام در سیستمِ زنده → کلیدها از قبل هستند → دست‌نخورده می‌مانند،
        //     یعنی آدرسِ سرورِ همین دفتر با آدرسِ دفترِ دیگر بازنویسی نمی‌شود.
        // فیلترِ دوباره اینجا عمدی است (دفاع در عمق): صادرات از قبل کلیدهای
        // حساس را حذف کرده، ولی یک بکاپِ قدیمی — که پیش از این قابلیت گرفته
        // شده — ممکن است DeviceGuid یا RefreshToken داشته باشد. آن‌ها هرگز
        // نباید روی یک ماشینِ دیگر بنشینند.
        private static void MergeSyncState(SQLiteConnection con, SQLiteTransaction tr, DataTable table)
        {
            if (table == null || table.Rows.Count == 0) return;
            if (!table.Columns.Contains("StateKey")) return;

            int blocked = 0;
            foreach (DataRow row in table.Rows)
            {
                string key = row["StateKey"] == DBNull.Value ? "" : row["StateKey"].ToString();
                if (!IsSyncStateKeyAllowed(key)) { blocked++; continue; }

                var cols = new List<string>();
                var pnames = new List<string>();
                using (var cmd = new SQLiteCommand())
                {
                    cmd.Connection = con;
                    cmd.Transaction = tr;
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        string col = table.Columns[i].ColumnName;
                        cols.Add("[" + col + "]");
                        string pname = "@y" + i;
                        pnames.Add(pname);
                        object v = row[col];
                        cmd.Parameters.AddWithValue(pname, v == null ? DBNull.Value : v);
                    }
                    cmd.CommandText =
                        "INSERT OR IGNORE INTO SyncState (" + string.Join(",", cols.ToArray()) + ")" +
                        " VALUES (" + string.Join(",", pnames.ToArray()) + ")";
                    cmd.ExecuteNonQuery();
                }
            }

            if (blocked > 0)
                System.Diagnostics.Debug.WriteLine(
                    "[BackupHelper.MergeSyncState] " + blocked +
                    " کلیدِ خارج از فهرستِ مجاز بازیابی نشد (هویتِ دستگاه/اعتبارنامه).");
        }

        // ── بازیابیِ کاملِ یک جدول، فقط وقتی جدولِ مقصد خالی است ───────────────
        // آموزش — این متد قلبِ رفعِ باگِ C-3 است. جدول‌هایی مثلِ TblApplicant و
        // TblAuditLog نه GlobalID دارند نه کلیدِ یکتای طبیعی، پس هیچ راهی برای
        // تشخیصِ «این ردیف قبلاً merge شده» وجود ندارد. به‌جایِ حدس‌زدنِ یک
        // کلیدِ مصنوعی (که CLAUDE.md صریحاً منع می‌کند)، از خودِ وضعیتِ مقصد
        // به‌عنوانِ تصمیم‌گیر استفاده می‌کنیم:
        //   • مقصد خالی  → این یک بازیابیِ فاجعه/نصبِ تازه است → کامل برگردان.
        //   • مقصد پر    → این یک ادغام است → دست نزن (رفتارِ قبلی حفظ می‌شود).
        // نتیجه: هیچ ردیفِ تکراری ساخته نمی‌شود و هیچ دادهٔ موجودی رونویسی
        // نمی‌شود، ولی سناریویی که تا امروز بی‌صدا داده را می‌بلعید رفع می‌شود.
        private void RestoreWholeTableIfEmpty(SQLiteConnection con, SQLiteTransaction tr,
            DataSet dataSet, string tableName)
        {
            if (!dataSet.Tables.Contains(tableName)) return;

            try
            {
                // جدول در دیتابیسِ مقصد وجود دارد؟ (ماژولِ راه‌اندازی‌نشده)
                using (var cmd = new SQLiteCommand(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @Name", con, tr))
                {
                    cmd.Parameters.AddWithValue("@Name", tableName);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                        return;
                }

                // خالی است؟ اگر نه، این یک ادغام است — دست نمی‌زنیم.
                using (var cmd = new SQLiteCommand("SELECT COUNT(1) FROM " + tableName, con, tr))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[BackupHelper.RestoreWholeTableIfEmpty " + tableName + "] مقصد خالی نیست — رد شد (ادغام).");
                        return;
                    }
                }

                InsertTable(con, tr, tableName, dataSet.Tables[tableName]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[BackupHelper.RestoreWholeTableIfEmpty " + tableName + "] " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tier 3 — نگاشتِ شناسه‌ها.
        //
        // آموزش — چرا این لازم شد: هنگام بازیابی، شناسه‌های بعضی جدول‌ها
        // بازتخصیص می‌شوند (نه حفظ):
        //   • TblCase.CasID    → InsertCaseRow عمداً ستونِ CasID را نمی‌نویسد.
        //   • TblFamily.FamID  → MergeChildTable شناسهٔ تازه می‌دهد.
        //   • TblUsers.UserID  → MergeUsers ستونِ UserID را کنار می‌گذارد.
        //   • TblCenter.CenterID → MergeCenters بر پایهٔ CenterCode درج می‌کند.
        // در مقابل، جدول‌هایی که با «جایگزینیِ کامل» برمی‌گردند کلیدِ اصلی‌شان
        // حفظ می‌شود (InsertRow همهٔ ستون‌ها را می‌نویسد)، پس زنجیره‌های
        // پدر→فرزندِ درون‌گروهی (مثل EntApprovalRequest→EntApprovalAction) خودبه‌خود
        // سالم می‌مانند و نیازی به نگاشت ندارند.
        //
        // خطرِ واقعی که این کلاس می‌بندد: در دیتابیسِ زندهٔ همین پروژه
        // TblCase شمارِ ۱۶۶۱ ردیف دارد ولی شناسه‌ها تا ۵۰۶۶ می‌روند (حفره‌دار).
        // پس بعدِ بازیابی، شناسهٔ ۵۰۶۶ اصلاً وجود ندارد و هر ارجاعِ خامی که
        // کپی شود یا به پروندهٔ اشتباه می‌چسبد یا به هیچ.
        // ─────────────────────────────────────────────────────────────────────
        private class RestoreMaps
        {
            public Dictionary<int, int> Case   = new Dictionary<int, int>();
            public Dictionary<int, int> Family = new Dictionary<int, int>();
            public Dictionary<int, int> User   = new Dictionary<int, int>();
            public Dictionary<int, int> Center = new Dictionary<int, int>();
        }

        // نگاشتِ کاربر بر پایهٔ Username و نگاشتِ مرکز بر پایهٔ CenterCode ساخته
        // می‌شوند — یعنی همان کلیدهای طبیعیِ یکتایی که MergeUsers/MergeCenters
        // خودشان برای تشخیصِ تکراری استفاده می‌کنند.
        private RestoreMaps BuildRestoreMaps(SQLiteConnection con, SQLiteTransaction tr,
            DataSet dataSet, Dictionary<int, int> casIdMap, Dictionary<int, int> famIdMap)
        {
            var maps = new RestoreMaps();

            if (casIdMap != null) maps.Case   = casIdMap;
            if (famIdMap != null) maps.Family = famIdMap;

            BuildNaturalKeyMap(con, tr, dataSet, "TblUsers",  "UserID",   "Username",   maps.User);
            BuildNaturalKeyMap(con, tr, dataSet, "TblCenter", "CenterID", "CenterCode", maps.Center);

            return maps;
        }

        // شناسهٔ قدیمی (از بکاپ) → شناسهٔ جدید (در مقصد)، از راهِ یک کلیدِ متنیِ یکتا.
        private static void BuildNaturalKeyMap(SQLiteConnection con, SQLiteTransaction tr,
            DataSet dataSet, string tableName, string idColumn, string keyColumn,
            Dictionary<int, int> target)
        {
            try
            {
                if (!dataSet.Tables.Contains(tableName)) return;
                DataTable backup = dataSet.Tables[tableName];
                if (!backup.Columns.Contains(idColumn) || !backup.Columns.Contains(keyColumn)) return;

                var keyToNewId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = new SQLiteCommand(
                    "SELECT " + idColumn + ", " + keyColumn + " FROM " + tableName, con, tr))
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (rd.IsDBNull(1)) continue;
                        keyToNewId[rd.GetValue(1).ToString()] = Convert.ToInt32(rd.GetValue(0));
                    }
                }

                foreach (DataRow row in backup.Rows)
                {
                    if (row[idColumn] == DBNull.Value || row[keyColumn] == DBNull.Value) continue;
                    int newId;
                    if (keyToNewId.TryGetValue(row[keyColumn].ToString(), out newId))
                        target[Convert.ToInt32(row[idColumn])] = newId;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[BackupHelper.BuildNaturalKeyMap " + tableName + "] " + ex.Message);
            }
        }

        // نگاشتِ ستونِ چندریختِ EntityID بر پایهٔ EntityName.
        // فهرستِ موجودیت‌های پشتیبانی‌شده همان چیزی است که VersionService و
        // RuleEngine می‌شناسند. TblApplicant با جایگزینیِ کامل برمی‌گردد و
        // شناسه‌اش حفظ می‌شود، پس ترجمه لازم ندارد.
        // خروجی false یعنی «قابلِ ترجمه نبود» → ردیف رد می‌شود، چون ارجاعِ
        // اشتباه از نبودِ ردیف بدتر است.
        private static bool TryMapEntityId(RestoreMaps maps, string entityName, int oldId, out int newId)
        {
            newId = oldId;
            if (maps == null) return true;

            if (string.Equals(entityName, "TblCase", StringComparison.OrdinalIgnoreCase))
                return maps.Case.TryGetValue(oldId, out newId);

            if (string.Equals(entityName, "TblFamily", StringComparison.OrdinalIgnoreCase))
                return maps.Family.TryGetValue(oldId, out newId);

            if (string.Equals(entityName, "TblApplicant", StringComparison.OrdinalIgnoreCase))
                return true; // شناسه حفظ شده است

            // TblDocs/TblAssistance و هر موجودیتِ ناشناخته: نگاشتی در دست نیست.
            return false;
        }

        // ── Tier 3: بازیابیِ یک جدول با ترجمهٔ ارجاع‌ها ────────────────────────
        // جایگزینیِ کامل (مثل RestoreWholeTable) ولی با ترجمهٔ ستون‌های ارجاعی.
        // ردیفی که ارجاعش قابلِ ترجمه نباشد رد می‌شود و شمرده می‌شود — هرگز با
        // مقدارِ خام درج نمی‌شود.
        private void RestoreRemappedTable(SQLiteConnection con, SQLiteTransaction tr,
            DataSet dataSet, string tableName, RestoreMaps maps,
            string[] userColumns = null, string[] centerColumns = null,
            string[] caseColumns = null, bool polymorphicEntity = false)
        {
            if (!dataSet.Tables.Contains(tableName)) return;

            try
            {
                using (var cmd = new SQLiteCommand(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @Name", con, tr))
                {
                    cmd.Parameters.AddWithValue("@Name", tableName);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                        return;
                }

                DataTable source = dataSet.Tables[tableName];
                ExecuteNonQuery(con, tr, "DELETE FROM " + tableName);
                if (source.Rows.Count == 0) return;

                bool hasEntityName = polymorphicEntity && source.Columns.Contains("EntityName")
                                                       && source.Columns.Contains("EntityID");
                int skipped = 0;

                foreach (DataRow row in source.Rows)
                {
                    var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    bool usable = true;

                    for (int i = 0; i < source.Columns.Count && usable; i++)
                    {
                        string col = source.Columns[i].ColumnName;
                        object v = row[col];

                        if (v != DBNull.Value && v != null && maps != null)
                        {
                            if (ContainsColumn(caseColumns, col))
                            {
                                int mapped;
                                if (!maps.Case.TryGetValue(Convert.ToInt32(v), out mapped)) { usable = false; break; }
                                v = mapped;
                            }
                            else if (ContainsColumn(userColumns, col))
                            {
                                int mapped;
                                // کاربرِ پیدانشده → NULL، نه شناسهٔ اشتباه. این
                                // ستون‌ها nullable هستند و خودِ ردیف (وظیفه/تأیید)
                                // ارزشِ نگه‌داشتن دارد، فقط بدونِ انتسابِ غلط.
                                v = maps.User.TryGetValue(Convert.ToInt32(v), out mapped)
                                    ? (object)mapped : DBNull.Value;
                            }
                            else if (ContainsColumn(centerColumns, col))
                            {
                                int mapped;
                                // مرکزِ پیدانشده → مقدارِ اصلی می‌ماند؛ ردیف حذف
                                // نمی‌شود ولی زیرِ مرکزِ ناشناخته دیده می‌شود
                                // (قابلِ‌مشاهده، نه خطای خاموش).
                                if (maps.Center.TryGetValue(Convert.ToInt32(v), out mapped))
                                    v = mapped;
                            }
                            else if (hasEntityName &&
                                     string.Equals(col, "EntityID", StringComparison.OrdinalIgnoreCase))
                            {
                                string entityName = row["EntityName"] == DBNull.Value
                                    ? "" : row["EntityName"].ToString();
                                int mapped;
                                if (!TryMapEntityId(maps, entityName, Convert.ToInt32(v), out mapped))
                                { usable = false; break; }
                                v = mapped;
                            }
                        }

                        values[col] = v ?? DBNull.Value;
                    }

                    if (!usable) { skipped++; continue; }

                    var cols = new List<string>();
                    var pnames = new List<string>();
                    using (var cmd = new SQLiteCommand())
                    {
                        cmd.Connection = con;
                        cmd.Transaction = tr;
                        int p = 0;
                        foreach (var kv in values)
                        {
                            cols.Add("[" + kv.Key + "]");
                            string pname = "@r" + p++;
                            pnames.Add(pname);
                            cmd.Parameters.AddWithValue(pname, kv.Value);
                        }
                        cmd.CommandText =
                            "INSERT INTO " + tableName + " (" + string.Join(",", cols.ToArray()) + ")" +
                            " VALUES (" + string.Join(",", pnames.ToArray()) + ")";
                        cmd.ExecuteNonQuery();
                    }
                }

                if (skipped > 0)
                    System.Diagnostics.Debug.WriteLine(
                        "[BackupHelper.RestoreRemappedTable " + tableName + "] " + skipped +
                        " ردیف رد شد (ارجاعِ قابلِ ترجمه نبود).");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[BackupHelper.RestoreRemappedTable " + tableName + "] " + ex.Message);
            }
        }

        private static bool ContainsColumn(string[] columns, string name)
        {
            if (columns == null) return false;
            for (int i = 0; i < columns.Length; i++)
                if (string.Equals(columns[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // ── Tier 2: بازیابیِ جدول‌های پیکربندی ────────────────────────────────
        // فقط روی «نصبِ تازه» (بازیابیِ فاجعه) اجرا می‌شود. دلیلِ کاملِ این
        // تصمیم بالای ImageBackup، کنارِ محاسبهٔ isFreshInstall، توضیح داده شده.
        // خلاصه: این جدول‌ها در هر اجرا seed می‌شوند، پس برای غلبه بر مقادیرِ
        // پیش‌فرض باید «جایگزینیِ کامل» شوند؛ و چون جایگزینیِ کامل روی یک
        // سیستمِ زنده خطرناک است، فقط وقتی مجاز است که سیستم تازه باشد.
        //
        // نکتهٔ کلیدی دربارهٔ شناسه‌ها: InsertTable همهٔ ستون‌ها را درج می‌کند،
        // از جمله کلیدِ اصلی. پس در جایگزینیِ کامل، شناسه‌های اصلی حفظ می‌شوند و
        // ارجاع‌های داخلی (TblAssistancePackageItem.PackageID و
        // TblCardTemplateVersion.TemplateID) بدونِ هیچ نگاشتی درست می‌مانند.
        // تنها استثنا دو جدولِ کاربرمحور است که جداگانه مدیریت می‌شوند.
        private void RestoreConfigurationTables(SQLiteConnection con, SQLiteTransaction tr,
            DataSet dataSet, bool isFreshInstall, RestoreMaps maps)
        {
            if (!isFreshInstall)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[BackupHelper.RestoreConfigurationTables] نصبِ تازه نیست — پیکربندیِ محلی دست‌نخورده ماند (ادغام).");
                return;
            }

            // ترتیب: مرجع‌ها قبل از وابسته‌ها (خوانایی؛ FK واقعی در schema نیست).
            RestoreWholeTable(con, tr, dataSet, "EntPermission");
            RestoreWholeTable(con, tr, dataSet, "EntRolePermission");
            RestoreWholeTable(con, tr, dataSet, "EntModule");
            RestoreWholeTable(con, tr, dataSet, "EntRoleModule");

            RestoreWholeTable(con, tr, dataSet, "EntWorkflow");
            RestoreWholeTable(con, tr, dataSet, "EntWorkflowState");
            RestoreWholeTable(con, tr, dataSet, "EntWorkflowTransition");
            RestoreWholeTable(con, tr, dataSet, "EntApprovalChain");
            RestoreWholeTable(con, tr, dataSet, "EntApprovalLevel");
            RestoreWholeTable(con, tr, dataSet, "EntRule");

            RestoreWholeTable(con, tr, dataSet, "TblCardTemplate");
            RestoreWholeTable(con, tr, dataSet, "TblCardTemplateVersion");
            RestoreWholeTable(con, tr, dataSet, "TblAssistancePackage");
            RestoreWholeTable(con, tr, dataSet, "TblAssistancePackageItem");
            RestoreWholeTable(con, tr, dataSet, "TblReportTemplate");
            RestoreWholeTable(con, tr, dataSet, "TblScheduledReport");

            // ─── Tier 3 — رفعِ نقصِ D-1 ──────────────────────────────────────
            // TblCaseTransferHistory از Tier 2 در بکاپ بود ولی خام برگردانده
            // می‌شد، یعنی ستونِ CasID دست‌نخورده کپی می‌شد. اما InsertCaseRow
            // شناسهٔ پرونده‌ها را بازتخصیص می‌کند و شناسه‌های واقعی حفره‌دارند
            // (در دیتابیسِ زنده: ۱۶۶۱ ردیف با شناسه تا ۵۰۶۶). نتیجه: سابقهٔ
            // انتقال — که یک سندِ حقوقیِ سرپرستی است — به پروندهٔ اشتباه یا به
            // هیچ می‌چسبید. حالا با نگاشت ترجمه می‌شود.
            RestoreRemappedTable(con, tr, dataSet, "TblCaseTransferHistory", maps,
                userColumns: new[] { "UserID" },
                centerColumns: new[] { "FromCenterID", "ToCenterID" },
                caseColumns: new[] { "CasID" });

            // ─── Tier 3 — رفعِ نقصِ D-2 ──────────────────────────────────────
            // EntRecordVersion از Tier 1 در بکاپ بود ولی (EntityName, EntityID)
            // خام کپی می‌شد؛ چهار موجودیت از پنج موجودیتِ ممکن شناسه‌شان عوض
            // می‌شود، پس عکس‌های لحظه‌ایِ رکوردها به رکوردِ اشتباه می‌چسبیدند.
            RestoreRemappedTable(con, tr, dataSet, "EntRecordVersion", maps,
                userColumns: new[] { "ChangedByUserID" },
                centerColumns: new[] { "CenterID" },
                polymorphicEntity: true);

            RestoreRemappedTable(con, tr, dataSet, "TblReminder", maps,
                centerColumns: new[] { "CenterID" });

            RestoreRemappedTable(con, tr, dataSet, "AdmEmployee", maps,
                centerColumns: new[] { "CenterID" });
            RestoreRemappedTable(con, tr, dataSet, "AdmLeave", maps,
                centerColumns: new[] { "CenterID" });
            RestoreRemappedTable(con, tr, dataSet, "AdmMission", maps,
                centerColumns: new[] { "CenterID" });

            // ─── Tier 3 — ماژولِ اداری، تکمیل ────────────────────────────────
            RestoreRemappedTable(con, tr, dataSet, "AdmJobApplication", maps,
                centerColumns: new[] { "CenterID" });
            RestoreRemappedTable(con, tr, dataSet, "AdmDriverContract", maps,
                centerColumns: new[] { "CenterID" });

            // ─── Tier 3 — دادهٔ عملیاتیِ در جریان ─────────────────────────────
            // ترتیب مهم است: پدر قبل از فرزند. چون جایگزینیِ کامل کلیدِ اصلی را
            // حفظ می‌کند، ارجاعِ فرزند به پدر (InstanceID / RequestID) خودبه‌خود
            // درست می‌ماند و نگاشت لازم ندارد — فقط ارجاع‌های بیرونی (پرونده،
            // کاربر، مرکز) ترجمه می‌شوند.
            RestoreRemappedTable(con, tr, dataSet, "EntWorkflowInstance", maps,
                centerColumns: new[] { "CenterID" },
                polymorphicEntity: true);
            RestoreRemappedTable(con, tr, dataSet, "EntWorkflowHistory", maps);

            RestoreRemappedTable(con, tr, dataSet, "EntApprovalRequest", maps,
                userColumns: new[] { "RequestedByUserID" },
                centerColumns: new[] { "CenterID" },
                polymorphicEntity: true);
            RestoreRemappedTable(con, tr, dataSet, "EntApprovalAction", maps,
                userColumns: new[] { "ActionByUserID" });

            RestoreRemappedTable(con, tr, dataSet, "EntTask", maps,
                userColumns: new[] { "AssignedToUserID", "CreatedByUserID" },
                centerColumns: new[] { "CenterID" },
                polymorphicEntity: true);

            // ─── Tier 4 فاز ب — لاگ‌های سازمانی ──────────────────────────────
            // آموزش — همان زیرساختِ Tier 3، بدونِ موتورِ تازه. هر سه فقط‌افزودنی‌اند
            // و کلیدِ طبیعیِ یکتا ندارند، پس مثلِ بقیهٔ Tier 2/3 فقط روی نصبِ
            // تازه برمی‌گردند (در ادغام رد می‌شوند تا ردیفِ تکراری ساخته نشود).
            //
            // نکتهٔ ظریف دربارهٔ انتساب: EntSecurityEvent و EntErrorLog هر دو یک
            // ستونِ Username متنی هم دارند، پس حتی اگر UserID قابلِ ترجمه نباشد
            // (کاربری که دیگر وجود ندارد) نامِ او در لاگ باقی می‌ماند — یعنی
            // ردِ حسابرسی به‌جای گم‌شدن، فقط از عدد به نام تنزل می‌کند.
            RestoreRemappedTable(con, tr, dataSet, "EntSecurityEvent", maps,
                userColumns: new[] { "UserID" },
                centerColumns: new[] { "CenterID" },
                polymorphicEntity: true);

            RestoreRemappedTable(con, tr, dataSet, "EntErrorLog", maps,
                userColumns: new[] { "UserID" },
                centerColumns: new[] { "CenterID" });

            RestoreRemappedTable(con, tr, dataSet, "EntRuleLog", maps,
                centerColumns: new[] { "CenterID" },
                polymorphicEntity: true);

            // دو جدولِ کاربرمحور — با نگاشتِ نام کاربری، نه شناسهٔ خام.
            RestoreUserKeyedTable(con, tr, dataSet, "EntUserPermission");
            RestoreUserKeyedTable(con, tr, dataSet, "EntUserModule");
        }

        // ── Tier 2: جدول‌هایی که با UserID کلید می‌خورند ───────────────────────
        // آموزش — این متد جلوی یک فسادِ امنیتیِ واقعی را می‌گیرد.
        // MergeUsers هنگام درجِ کاربران عمداً ستونِ UserID را کنار می‌گذارد، پس
        // کاربرانِ بازیابی‌شده شناسهٔ AUTOINCREMENT جدید می‌گیرند. از طرفی
        // DatabaseInitializer روی نصبِ تازه یک کاربرِ admin می‌سازد که UserID=1
        // را از قبل اشغال کرده است. یعنی شناسه‌ها تقریباً همیشه جابه‌جا می‌شوند.
        // اگر EntUserPermission را خام درج کنیم، استثناهای دسترسیِ یک کاربر به
        // کاربرِ دیگری که حالا آن عدد را دارد می‌چسبد — یعنی فردی بی‌سروصدا
        // دسترسی‌های شخصِ دیگری را می‌گیرد.
        // راه‌حل: از خودِ بکاپ نگاشتِ UserID→Username را می‌سازیم، بعد در
        // دیتابیسِ مقصد همان Username را به شناسهٔ جدیدش ترجمه می‌کنیم.
        // ردیفی که کاربرش پیدا نشود، عمداً رد می‌شود (رد کردن امن است؛ چسباندن
        // به کاربرِ اشتباه نیست).
        private void RestoreUserKeyedTable(SQLiteConnection con, SQLiteTransaction tr,
            DataSet dataSet, string tableName)
        {
            if (!dataSet.Tables.Contains(tableName)) return;
            if (!dataSet.Tables.Contains("TblUsers")) return;

            DataTable source = dataSet.Tables[tableName];
            if (source.Rows.Count == 0) return;
            if (!source.Columns.Contains("UserID")) return;

            try
            {
                using (var cmd = new SQLiteCommand(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @Name", con, tr))
                {
                    cmd.Parameters.AddWithValue("@Name", tableName);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                        return;
                }

                // ۱) از بکاپ: UserID قدیمی → نام کاربری
                DataTable backupUsers = dataSet.Tables["TblUsers"];
                if (!backupUsers.Columns.Contains("UserID") ||
                    !backupUsers.Columns.Contains("Username")) return;

                var oldIdToUsername = new Dictionary<int, string>();
                foreach (DataRow u in backupUsers.Rows)
                {
                    if (u["UserID"] == DBNull.Value || u["Username"] == DBNull.Value) continue;
                    oldIdToUsername[Convert.ToInt32(u["UserID"])] = u["Username"].ToString();
                }

                // ۲) از دیتابیسِ مقصد: نام کاربری → UserID جدید
                var usernameToNewId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = new SQLiteCommand("SELECT UserID, Username FROM TblUsers", con, tr))
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        usernameToNewId[rd.GetValue(1).ToString()] = Convert.ToInt32(rd.GetValue(0));
                }

                ExecuteNonQuery(con, tr, "DELETE FROM " + tableName);

                int skipped = 0;
                foreach (DataRow row in source.Rows)
                {
                    if (row["UserID"] == DBNull.Value) { skipped++; continue; }

                    string username;
                    if (!oldIdToUsername.TryGetValue(Convert.ToInt32(row["UserID"]), out username))
                    { skipped++; continue; }

                    int newUserId;
                    if (!usernameToNewId.TryGetValue(username, out newUserId))
                    { skipped++; continue; }

                    var cols = new List<string>();
                    var pnames = new List<string>();
                    using (var cmd = new SQLiteCommand())
                    {
                        cmd.Connection = con;
                        cmd.Transaction = tr;
                        for (int i = 0; i < source.Columns.Count; i++)
                        {
                            string col = source.Columns[i].ColumnName;
                            cols.Add("[" + col + "]");
                            string pname = "@k" + i;
                            pnames.Add(pname);

                            // تنها ستونی که ترجمه می‌شود همین است.
                            object v = string.Equals(col, "UserID", StringComparison.OrdinalIgnoreCase)
                                ? (object)newUserId
                                : row[col];
                            cmd.Parameters.AddWithValue(pname, v ?? DBNull.Value);
                        }
                        cmd.CommandText =
                            "INSERT OR IGNORE INTO " + tableName + " (" + string.Join(",", cols.ToArray()) + ")" +
                            " VALUES (" + string.Join(",", pnames.ToArray()) + ")";
                        cmd.ExecuteNonQuery();
                    }
                }

                if (skipped > 0)
                    System.Diagnostics.Debug.WriteLine(
                        "[BackupHelper.RestoreUserKeyedTable " + tableName + "] " + skipped +
                        " ردیف رد شد (کاربرِ متناظر در مقصد پیدا نشد).");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[BackupHelper.RestoreUserKeyedTable " + tableName + "] " + ex.Message);
            }
        }

        // ── تاریخچه وضعیت: فقط برای پرونده‌های تازه‌درج‌شده (casIdMap) منتقل
        // می‌شود؛ این جدول GlobalID ندارد پس هیچ کلیدی برای تشخیص «قبلاً
        // merge شده» ندارد — انتقال فقط پرونده‌های نو، امکان تکرار را از بین می‌برد.
        private static void MergeCaseStatusHistory(SQLiteConnection con, SQLiteTransaction tr,
            DataTable table, Dictionary<int, int> casIdMap, HashSet<int> newlyInsertedOrigIds)
        {
            if (table == null || table.Rows.Count == 0) return;

            foreach (DataRow row in table.Rows)
            {
                int origCasId = Convert.ToInt32(row["CasID"]);
                if (!newlyInsertedOrigIds.Contains(origCasId)) continue;

                int newCasId;
                if (!casIdMap.TryGetValue(origCasId, out newCasId)) continue;

                // آموزش — رفعِ افتِ بی‌صدای داده: نسخهٔ قبلی فقط پنج ستون را درج
                // می‌کرد، در حالی که جدول از فازِ «دلیلِ تعلیق» به بعد چهار ستونِ
                // دیگر هم دارد (ChangeType/Reason/Notes/UserID). نتیجه این بود که
                // بازیابی از بکاپ، «چرا»ی هر تغییرِ وضعیت و شناسهٔ کاربرِ ثبت‌کننده
                // را دور می‌ریخت و فقط «چه چیزی» باقی می‌ماند.
                // GetVal برای بکاپ‌های قدیمی‌تر (بدون این ستون‌ها) خودش مقدارِ
                // پیش‌فرض برمی‌گرداند، پس سازگاری با فایل‌های قدیمی حفظ می‌شود.
                using (var cmd = new SQLiteCommand(@"
INSERT INTO TblCaseStatusHistory
    (CasID, OldStatus, NewStatus, ChangeType, Reason, Notes, ChangedAt, ChangedBy, UserID)
VALUES
    (@CasID, @OldStatus, @NewStatus, @ChangeType, @Reason, @Notes, @ChangedAt, @ChangedBy, @UserID)", con, tr))
                {
                    cmd.Parameters.AddWithValue("@CasID",      newCasId);
                    cmd.Parameters.AddWithValue("@OldStatus",  GetVal(row, "OldStatus", DBNull.Value));
                    cmd.Parameters.AddWithValue("@NewStatus",  GetVal(row, "NewStatus", ""));
                    cmd.Parameters.AddWithValue("@ChangeType", GetVal(row, "ChangeType", DBNull.Value));
                    cmd.Parameters.AddWithValue("@Reason",     GetVal(row, "Reason", DBNull.Value));
                    cmd.Parameters.AddWithValue("@Notes",      GetVal(row, "Notes", DBNull.Value));
                    cmd.Parameters.AddWithValue("@ChangedAt",  GetVal(row, "ChangedAt", DBNull.Value));
                    cmd.Parameters.AddWithValue("@ChangedBy",  GetVal(row, "ChangedBy", DBNull.Value));
                    cmd.Parameters.AddWithValue("@UserID",     GetVal(row, "UserID", DBNull.Value));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── بازیابیِ کاملِ یک جدول (حالت کلاسیک) ─────────────────────────────
        // پاک می‌کند و دوباره درج می‌کند؛ اگر جدول در بکاپ نبود (فایلِ قدیمی) یا
        // در دیتابیسِ مقصد وجود نداشت (ماژولِ راه‌اندازی‌نشده) بی‌صدا رد می‌شود تا
        // بازیابی به‌خاطرِ یک جدولِ جانبی کامل شکست نخورد.
        // (غیرِ static، چون InsertTable یک متدِ نمونه است — هم‌الگوی بقیهٔ
        // مسیرِ بازیابی که آن هم از نمونه فراخوانی می‌شود.)
        private void RestoreWholeTable(SQLiteConnection con, SQLiteTransaction tr,
            DataSet dataSet, string tableName)
        {
            if (!dataSet.Tables.Contains(tableName)) return;

            try
            {
                using (var cmd = new SQLiteCommand(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @Name", con, tr))
                {
                    cmd.Parameters.AddWithValue("@Name", tableName);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                        return;
                }

                ExecuteNonQuery(con, tr, "DELETE FROM " + tableName);
                InsertTable(con, tr, tableName, dataSet.Tables[tableName]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[BackupHelper.RestoreWholeTable " + tableName + "] " + ex.Message);
            }
        }

        // ── تاریخچهٔ سطحِ عضو (وضعیت خدمات و نقش) ────────────────────────────
        // آموزش — یک متدِ عمومی برای هر دو جدول: تنها تفاوتشان نامِ ستونِ کلیدِ
        // خارجی است (FamID در تاریخچهٔ وضعیت، FamilyMemberID در تاریخچهٔ نقش).
        // ستون‌ها به‌صورت پویا از خودِ DataTable خوانده می‌شوند، پس اگر بعداً
        // ستونی به این جدول‌ها اضافه شود، اینجا دوباره همان باگِ «افتِ ستون» که
        // در MergeCaseStatusHistory بود تکرار نمی‌شود.
        private static void MergeFamilyHistory(SQLiteConnection con, SQLiteTransaction tr,
            DataTable table, string targetTable, string fkColumn, Dictionary<int, int> famIdMap)
        {
            if (table == null || table.Rows.Count == 0) return;
            if (!table.Columns.Contains(fkColumn)) return;

            foreach (DataRow row in table.Rows)
            {
                if (row[fkColumn] == DBNull.Value) continue;

                int newFamId;
                if (!famIdMap.TryGetValue(Convert.ToInt32(row[fkColumn]), out newFamId))
                    continue; // عضو در این بازیابی درج نشد ⇒ تاریخچه‌اش هم نمی‌آید

                var cols   = new List<string>();
                var pnames = new List<string>();

                using (var cmd = new SQLiteCommand())
                {
                    cmd.Connection  = con;
                    cmd.Transaction = tr;

                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        string col = table.Columns[i].ColumnName;

                        // کلیدِ اصلی از AUTOINCREMENT دوباره ساخته می‌شود.
                        if (string.Equals(col, "FamStatusID",   StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(col, "RoleHistoryID", StringComparison.OrdinalIgnoreCase))
                            continue;

                        cols.Add("[" + col + "]");
                        string pname = "@h" + i;
                        pnames.Add(pname);

                        object v = string.Equals(col, fkColumn, StringComparison.OrdinalIgnoreCase)
                            ? (object)newFamId
                            : row[col];
                        cmd.Parameters.AddWithValue(pname, v == null ? DBNull.Value : v);
                    }

                    if (cols.Count == 0) continue;

                    cmd.CommandText =
                        "INSERT INTO " + targetTable +
                        " (" + string.Join(",", cols.ToArray()) + ")" +
                        " VALUES (" + string.Join(",", pnames.ToArray()) + ")";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── پرونده‌های مرتبط (فاز ۲) ────────────────────────────────────────
        // آموزش — این جدول دو کلید خارجی به TblCase دارد (CasID و RelatedCasID).
        // MergeChildTable عمومی فقط CasID را remap می‌کند، پس اگر از آن استفاده
        // می‌شد، سرِ دوم رابطه به پرونده‌ی اشتباه (یا ناموجود) اشاره می‌کرد.
        // تکراری‌ها با خودِ جفتِ (CasID, RelatedCasID) تشخیص داده می‌شوند، پس
        // وارد کردن چندباره‌ی یک بکاپ رابطه‌ی تکراری نمی‌سازد.
        private static void MergeCaseRelations(SQLiteConnection con, SQLiteTransaction tr,
            DataTable table, Dictionary<int, int> casIdMap)
        {
            if (table == null || table.Rows.Count == 0) return;

            foreach (DataRow row in table.Rows)
            {
                int origCasId = Convert.ToInt32(GetVal(row, "CasID", 0));
                int origRelId = Convert.ToInt32(GetVal(row, "RelatedCasID", 0));

                int newCasId, newRelId;
                if (!casIdMap.TryGetValue(origCasId, out newCasId)) continue;
                if (!casIdMap.TryGetValue(origRelId, out newRelId)) continue;
                if (newCasId == newRelId) continue;

                using (var existsCmd = new SQLiteCommand(
                    "SELECT COUNT(1) FROM TblCaseRelation WHERE CasID = @C AND RelatedCasID = @R", con, tr))
                {
                    existsCmd.Parameters.AddWithValue("@C", newCasId);
                    existsCmd.Parameters.AddWithValue("@R", newRelId);
                    if (Convert.ToInt32(existsCmd.ExecuteScalar()) > 0) continue;
                }

                using (var cmd = new SQLiteCommand(@"
INSERT INTO TblCaseRelation (CasID, RelatedCasID, RelationType, CreatedAt, CreatedBy)
VALUES (@CasID, @RelatedCasID, @RelationType, @CreatedAt, @CreatedBy)", con, tr))
                {
                    cmd.Parameters.AddWithValue("@CasID",        newCasId);
                    cmd.Parameters.AddWithValue("@RelatedCasID", newRelId);
                    cmd.Parameters.AddWithValue("@RelationType", GetVal(row, "RelationType", DBNull.Value));
                    cmd.Parameters.AddWithValue("@CreatedAt",    GetVal(row, "CreatedAt", DBNull.Value));
                    cmd.Parameters.AddWithValue("@CreatedBy",    GetVal(row, "CreatedBy", DBNull.Value));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── تاریخچه بایگانی (فاز ۲) — مثل تاریخچه وضعیت، کلید یکتا ندارد پس
        // فقط برای پرونده‌های تازه‌درج‌شده منتقل می‌شود تا تکراری نشود.
        private static void MergeArchiveHistory(SQLiteConnection con, SQLiteTransaction tr,
            DataTable table, Dictionary<int, int> casIdMap, HashSet<int> newlyInsertedOrigIds)
        {
            if (table == null || table.Rows.Count == 0) return;

            foreach (DataRow row in table.Rows)
            {
                int origCasId = Convert.ToInt32(GetVal(row, "CasID", 0));
                if (!newlyInsertedOrigIds.Contains(origCasId)) continue;

                int newCasId;
                if (!casIdMap.TryGetValue(origCasId, out newCasId)) continue;

                using (var cmd = new SQLiteCommand(@"
INSERT INTO TblArchiveHistory (CasID, Action, ActionAt, ActionBy)
VALUES (@CasID, @Action, @ActionAt, @ActionBy)", con, tr))
                {
                    cmd.Parameters.AddWithValue("@CasID",    newCasId);
                    cmd.Parameters.AddWithValue("@Action",   GetVal(row, "Action", ""));
                    cmd.Parameters.AddWithValue("@ActionAt", GetVal(row, "ActionAt", DBNull.Value));
                    cmd.Parameters.AddWithValue("@ActionBy", GetVal(row, "ActionBy", DBNull.Value));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── بررسی وجود پرونده بر اساس GlobalID ─────────────────────────────
        private static int GetCasIdByGlobalId(SQLiteConnection con, SQLiteTransaction tr, string guid)
        {
            using (var cmd = new SQLiteCommand(
                "SELECT CasID FROM TblCase WHERE GlobalID = @G LIMIT 1", con, tr))
            {
                cmd.Parameters.AddWithValue("@G", guid);
                object val = cmd.ExecuteScalar();
                return val == null || val == DBNull.Value ? 0 : Convert.ToInt32(val);
            }
        }

        // ── درج یک پرونده و برگرداندن CasID جدید ───────────────────────────
        private static int InsertCaseRow(SQLiteConnection con, SQLiteTransaction tr,
                                         DataTable table, DataRow row)
        {
            var cols   = new List<string>();
            var pnames = new List<string>();
            using (var cmd = new SQLiteCommand())
            {
                cmd.Connection  = con;
                cmd.Transaction = tr;
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    string col = table.Columns[i].ColumnName;
                    if (string.Equals(col, "CasID", StringComparison.OrdinalIgnoreCase))
                        continue; // CasID از AUTOINCREMENT می‌آید
                    cols.Add("[" + col + "]");
                    string pname = "@c" + i;
                    pnames.Add(pname);
                    object v = row[col];
                    cmd.Parameters.AddWithValue(pname, v == null ? DBNull.Value : v);
                }
                cmd.CommandText =
                    "INSERT INTO TblCase (" + string.Join(",", cols.ToArray()) + ")" +
                    " VALUES ("           + string.Join(",", pnames.ToArray()) + ");" +
                    " SELECT last_insert_rowid();";
                return Convert.ToInt32((long)cmd.ExecuteScalar());
            }
        }

        // ── درج جدول فرزند با نگاشت CasID ──────────────────────────────────
        // آموزش — رفع باگ تکراری‌شدن: قبلاً وقتی یک پرونده به‌خاطر GlobalID
        // تکراری از قبل موجود بود (Skip می‌شد)، رکوردهای خانواده/سند/کمکِ آن
        // همچنان دوباره درج می‌شدند چون هیچ کلید یکتایی برای تشخیص «قبلاً
        // merge شده» نداشتند. حالا اگر جدول فرزند ستون GlobalID داشته باشد
        // (بکاپ‌های جدید)، هر رکورد بر همان اساس چک تکراری می‌شود. بکاپ‌های
        // قدیمی‌تر (بدون GlobalID روی جدول فرزند) طبق رفتار قبلی درج می‌شوند.
        // آموزش — پارامترِ اختیاریِ outIdMap افزوده شد (پیش‌فرض null ⇒ رفتارِ
        // فراخوان‌های قبلی ذرّه‌ای تغییر نمی‌کند). هدف: جدولِ تاریخچهٔ اعضا با
        // FamID به عضو وصل است، و چون PK اینجا از AUTOINCREMENT دوباره ساخته
        // می‌شود، بدونِ نگاشتِ «FamID قدیم → FamID جدید» تاریخچه به عضوِ اشتباه
        // می‌چسبید. فقط ردیف‌هایی که واقعاً درج شدند وارد نگاشت می‌شوند، پس
        // همان قاعدهٔ «تاریخچه فقط برای رکوردهای نو» خودبه‌خود رعایت می‌گردد.
        private static void MergeChildTable(SQLiteConnection con, SQLiteTransaction tr,
                                            DataTable table, string pkCol,
                                            Dictionary<int, int> casIdMap,
                                            Dictionary<int, int> outIdMap = null)
        {
            if (table == null || table.Rows.Count == 0) return;

            bool hasGlobalId = table.Columns.Contains("GlobalID");
            bool hasPk       = table.Columns.Contains(pkCol);

            foreach (DataRow row in table.Rows)
            {
                int origCasId = row.Table.Columns.Contains("CasID")
                    ? Convert.ToInt32(row["CasID"]) : 0;

                int newCasId;
                if (!casIdMap.TryGetValue(origCasId, out newCasId)) continue;

                if (hasGlobalId)
                {
                    string guid = row["GlobalID"] == DBNull.Value ? "" : row["GlobalID"].ToString();
                    if (!string.IsNullOrWhiteSpace(guid) && ChildRowExists(con, tr, table.TableName, guid))
                        continue; // قبلاً merge شده؛ از رکورد تکراری جلوگیری می‌شود
                }

                var cols   = new List<string>();
                var pnames = new List<string>();
                using (var cmd = new SQLiteCommand())
                {
                    cmd.Connection  = con;
                    cmd.Transaction = tr;
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        string col = table.Columns[i].ColumnName;
                        if (string.Equals(col, pkCol, StringComparison.OrdinalIgnoreCase))
                            continue; // PK از AUTOINCREMENT می‌آید
                        cols.Add("[" + col + "]");
                        string pname = "@f" + i;
                        pnames.Add(pname);
                        object v = string.Equals(col, "CasID", StringComparison.OrdinalIgnoreCase)
                            ? (object)newCasId : row[col];
                        cmd.Parameters.AddWithValue(pname, v == null ? DBNull.Value : v);
                    }
                    cmd.CommandText =
                        "INSERT INTO " + table.TableName +
                        " (" + string.Join(",", cols.ToArray()) + ")" +
                        " VALUES (" + string.Join(",", pnames.ToArray()) + ")";
                    cmd.ExecuteNonQuery();
                }

                // شناسهٔ تازه‌ساخته‌شده روی همان اتصال/تراکنش خوانده می‌شود
                // (هم‌الگوی DatabaseHelper.ExecuteInsertReturningId).
                if (outIdMap != null && hasPk)
                {
                    using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                    {
                        object v = idCmd.ExecuteScalar();
                        if (v != null && v != DBNull.Value)
                            outIdMap[Convert.ToInt32(row[pkCol])] = Convert.ToInt32(v);
                    }
                }
            }
        }

        // ── بررسی وجود رکورد فرزند بر اساس GlobalID (جلوگیری از merge تکراری) ─
        private static bool ChildRowExists(SQLiteConnection con, SQLiteTransaction tr, string tableName, string guid)
        {
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(1) FROM " + tableName + " WHERE GlobalID = @G", con, tr))
            {
                cmd.Parameters.AddWithValue("@G", guid);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static object GetVal(DataRow row, string col, object def)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value)
                return def;
            return row[col];
        }

        private static void LoadTable(SQLiteConnection con, DataSet dataSet, string tableName, string query)
        {
            using (var cmd = new SQLiteCommand(query, con))
            using (var reader = cmd.ExecuteReader())
            {
                DataTable table = new DataTable(tableName);
                table.Load(reader);
                dataSet.Tables.Add(table);
            }
        }

        // آموزش — چرا نسخهٔ «اگر وجود داشت»: LoadTable روی جدولِ ناموجود خطای
        // «no such table» می‌دهد و کلِ گرفتنِ بکاپ را می‌شکند. بعضی از جداولِ
        // تازه‌اضافه‌شده به بکاپ (مثل EntRecordVersion) را ماژولِ Enterprise
        // می‌سازد و ممکن است روی نصبی که هرگز آن ماژول را باز نکرده وجود
        // نداشته باشند. این نسخه در آن حالت فقط از آن جدول صرف‌نظر می‌کند و
        // بقیهٔ بکاپ سالم گرفته می‌شود.
        private static void LoadTableIfExists(SQLiteConnection con, DataSet dataSet, string tableName, string query)
        {
            try
            {
                using (var cmd = new SQLiteCommand(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @Name", con))
                {
                    cmd.Parameters.AddWithValue("@Name", tableName);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                        return;
                }

                LoadTable(con, dataSet, tableName, query);
            }
            catch (Exception ex)
            {
                // نبودِ یک جدولِ جانبی نباید کلِ بکاپ را از بین ببرد.
                System.Diagnostics.Debug.WriteLine("[BackupHelper.LoadTableIfExists " + tableName + "] " + ex.Message);
            }
        }

        private static void EnsureTable(DataSet dataSet, string tableName)
        {
            if (!dataSet.Tables.Contains(tableName))
                throw new Exception("جدول " + tableName + " داخل بکاپ پیدا نشد.");
        }

        // آموزش — TblCenter دیگر اینجا پاک نمی‌شود: مراکز داده مرجع مشترک
        // هستند و از قبل به‌صورت افزایشی توسط MergeCenters (بالاتر) درج
        // می‌شوند؛ پاک کردنشان بدون درج مجدد، فهرست مراکز را برای همیشه از
        // بین می‌برد (باگ قبلی).
        private static void DeleteCurrentData(SQLiteConnection con, SQLiteTransaction tr)
        {
            // فرزندها پیش از والد، صریح (نه با تکیه بر CASCADE) — همان الگوی قبلی.
            ExecuteNonQuery(con, tr, "DELETE FROM TblCaseRelation");
            ExecuteNonQuery(con, tr, "DELETE FROM TblArchiveHistory");
            ExecuteNonQuery(con, tr, "DELETE FROM TblAssistance");
            ExecuteNonQuery(con, tr, "DELETE FROM TblDocs");
            ExecuteNonQuery(con, tr, "DELETE FROM TblFamily");
            ExecuteNonQuery(con, tr, "DELETE FROM TblCase");
        }

        private static void ExecuteNonQuery(SQLiteConnection con, SQLiteTransaction tr, string commandText)
        {
            using (var cmd = new SQLiteCommand(commandText, con, tr))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertTable(
            SQLiteConnection con,
            SQLiteTransaction tr,
            string tableName,
            DataTable table)
        {
            if (table.Rows.Count == 0)
                return;

            foreach (DataRow row in table.Rows)
                InsertRow(con, tr, tableName, table, row);
        }

        private static void InsertRow(
            SQLiteConnection con,
            SQLiteTransaction tr,
            string tableName,
            DataTable table,
            DataRow row)
        {
            List<string> columnNames = new List<string>();
            List<string> parameterNames = new List<string>();
            using (var cmd = new SQLiteCommand())
            {
                cmd.Connection = con;
                cmd.Transaction = tr;

                for (int i = 0; i < table.Columns.Count; i++)
                {
                    string columnName = table.Columns[i].ColumnName;
                    string parameterName = "@p" + i.ToString();

                    columnNames.Add("[" + columnName + "]");
                    parameterNames.Add(parameterName);

                    object value = row[columnName];

                    if (value == null)
                        value = DBNull.Value;

                    cmd.Parameters.AddWithValue(parameterName, value);
                }

                cmd.CommandText =
                    "INSERT INTO " + tableName +
                    " (" + string.Join(",", columnNames.ToArray()) + ")" +
                    " VALUES (" + string.Join(",", parameterNames.ToArray()) + ")";

                cmd.ExecuteNonQuery();
            }
        }



        private static void RemapStoredFilePaths(DataSet dataSet, string oldRoot, string newRoot)
        {
            if (string.IsNullOrWhiteSpace(oldRoot) ||
                string.IsNullOrWhiteSpace(newRoot) ||
                string.Equals(
                    Path.GetFullPath(oldRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(newRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RemapColumn(dataSet.Tables["TblCase"], "PhotoPath", oldRoot, newRoot);
            RemapColumn(dataSet.Tables["TblCase"], "FamilyPhotoPath", oldRoot, newRoot);
            RemapColumn(dataSet.Tables["TblFamily"], "MemberPhotoPath", oldRoot, newRoot);
            RemapColumn(dataSet.Tables["TblDocs"], "DocFilePath", oldRoot, newRoot);
        }

        private static void RemapColumn(DataTable table, string columnName, string oldRoot, string newRoot)
        {
            if (table == null || !table.Columns.Contains(columnName))
                return;

            string oldFullRoot = Path.GetFullPath(oldRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            string newFullRoot = Path.GetFullPath(newRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            foreach (DataRow row in table.Rows)
            {
                if (row[columnName] == null || row[columnName] == DBNull.Value)
                    continue;

                string currentPath = row[columnName].ToString();

                if (string.IsNullOrWhiteSpace(currentPath))
                    continue;

                string fullCurrentPath;

                try
                {
                    fullCurrentPath = Path.GetFullPath(currentPath);
                }
                catch
                {
                    continue;
                }

                if (!fullCurrentPath.StartsWith(oldFullRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                string relativePath = fullCurrentPath.Substring(oldFullRoot.Length);
                row[columnName] = Path.Combine(newFullRoot, relativePath);
            }
        }

        // آموزش — رفع باگ «مسیر طولانی‌تر از ۲۶۰ کاراکتر» هنگام بکاپ:
        // پوشه‌ی بکاپ خودکار (AutoBackups) داخلِ همان storage root ساخته می‌شود
        // (AutoBackupService)، پس کپیِ کاملِ storage root بکاپ‌های قبلی را هم
        // با خود می‌برد: بکاپ ۳ شامل بکاپ ۲ شامل بکاپ ۱ … هر لایه ~۵۴ کاراکتر
        // به طول مسیر اضافه می‌کند تا از سقف ویندوز رد شود (و حجم بکاپ نمایی
        // رشد کند). excludedFolder فقط پوشه‌ی بکاپِ همین اجرا را رد می‌کرد،
        // نه بکاپ‌های قبلی. این تابع نامِ پوشه‌های بکاپ را تشخیص می‌دهد تا در
        // مسیرِ export نادیده گرفته شوند. بازیابی (ImportBackup) دست‌نخورده است.
        private static bool IsBackupFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName)) return false;

            return string.Equals(folderName, "AutoBackups", StringComparison.OrdinalIgnoreCase)
                || string.Equals(folderName, "SyncBackups", StringComparison.OrdinalIgnoreCase)
                || folderName.StartsWith("CaseManagementBackup_", StringComparison.OrdinalIgnoreCase);
        }

        // آیا مسیرِ نسبیِ داده‌شده داخل یکی از پوشه‌های بکاپ قرار دارد؟
        private static bool IsInsideBackupFolder(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return false;

            string[] parts = relativePath.Split(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            for (int i = 0; i < parts.Length; i++)
            {
                if (IsBackupFolderName(parts[i]))
                    return true;
            }
            return false;
        }

        private static void CopyDirectory(string sourceFolder, string targetFolder, string excludedFolder,
                                          bool skipNestedBackups = false)
        {
            Directory.CreateDirectory(targetFolder);

            string sourceFull = Path.GetFullPath(sourceFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            string targetFull = Path.GetFullPath(targetFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            string excludedFull = "";

            if (targetFull.StartsWith(sourceFull, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(excludedFolder))
            {
                throw new IOException("کپی پوشه داخل خودش مجاز نیست.");
            }

            if (!string.IsNullOrWhiteSpace(excludedFolder))
            {
                excludedFull = Path.GetFullPath(excludedFolder)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
            }

            foreach (string directory in Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories))
            {
                string directoryFull = Path.GetFullPath(directory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                if (!string.IsNullOrWhiteSpace(excludedFull) &&
                    directoryFull.StartsWith(excludedFull, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = directoryFull.Substring(sourceFull.Length);

                if (skipNestedBackups && IsInsideBackupFolder(relative))
                    continue;

                Directory.CreateDirectory(Path.Combine(targetFull, relative));
            }

            foreach (string file in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                string fileFull = Path.GetFullPath(file);

                if (!string.IsNullOrWhiteSpace(excludedFull) &&
                    fileFull.StartsWith(excludedFull, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = fileFull.Substring(sourceFull.Length);

                if (skipNestedBackups && IsInsideBackupFolder(relative))
                    continue;

                string targetPath = Path.Combine(targetFull, relative);
                string targetDirectory = Path.GetDirectoryName(targetPath);

                if (!Directory.Exists(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                File.Copy(fileFull, targetPath, true);
            }
        }
    }
}

using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace CaseManagement.Helpers
{
    public class BackupHelper
    {
        private const string DataFileName = "CaseManagementBackup.xml";
        private const string RootFileName = "StorageRoot.txt";
        private const string FilesFolderName = "Files";

        private readonly DatabaseHelper db = new DatabaseHelper();

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
                            MergeChildTable(con, tr, dataSet.Tables["TblFamily"],    "FamID",        casIdMap);
                            MergeChildTable(con, tr, dataSet.Tables["TblDocs"],      "DocID",        casIdMap);
                            if (dataSet.Tables.Contains("TblAssistance"))
                                MergeChildTable(con, tr, dataSet.Tables["TblAssistance"], "AssistanceID", casIdMap);

                            // آموزش — تاریخچه وضعیت فاقد GlobalID است (کلید تشخیص
                            // تکراری ندارد)، پس فقط برای پرونده‌های تازه‌درج‌شده منتقل
                            // می‌شود؛ برای پرونده‌های از‌قبل‌موجود (Skip شده) وارد
                            // نمی‌شود تا تاریخچه در آن سمت تکراری نشود.
                            if (dataSet.Tables.Contains("TblCaseStatusHistory"))
                                MergeCaseStatusHistory(con, tr, dataSet.Tables["TblCaseStatusHistory"], casIdMap, newlyInsertedOrigIds);

                            // فاز ۲ — پرونده‌های مرتبط: هر دو سرِ رابطه باید
                            // remap شوند، وگرنه رابطه به پرونده‌ی اشتباه وصل می‌شود.
                            if (dataSet.Tables.Contains("TblCaseRelation"))
                                MergeCaseRelations(con, tr, dataSet.Tables["TblCaseRelation"], casIdMap);

                            if (dataSet.Tables.Contains("TblArchiveHistory"))
                                MergeArchiveHistory(con, tr, dataSet.Tables["TblArchiveHistory"], casIdMap, newlyInsertedOrigIds);
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

                using (var cmd = new SQLiteCommand(@"
INSERT INTO TblCaseStatusHistory (CasID, OldStatus, NewStatus, ChangedAt, ChangedBy)
VALUES (@CasID, @OldStatus, @NewStatus, @ChangedAt, @ChangedBy)", con, tr))
                {
                    cmd.Parameters.AddWithValue("@CasID",     newCasId);
                    cmd.Parameters.AddWithValue("@OldStatus", GetVal(row, "OldStatus", DBNull.Value));
                    cmd.Parameters.AddWithValue("@NewStatus", GetVal(row, "NewStatus", ""));
                    cmd.Parameters.AddWithValue("@ChangedAt", GetVal(row, "ChangedAt", DBNull.Value));
                    cmd.Parameters.AddWithValue("@ChangedBy", GetVal(row, "ChangedBy", DBNull.Value));
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
        private static void MergeChildTable(SQLiteConnection con, SQLiteTransaction tr,
                                            DataTable table, string pkCol,
                                            Dictionary<int, int> casIdMap)
        {
            if (table == null || table.Rows.Count == 0) return;

            bool hasGlobalId = table.Columns.Contains("GlobalID");

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

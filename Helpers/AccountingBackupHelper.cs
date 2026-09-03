using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // AccountingBackupHelper — بکاپ/بازیابی مستقلِ ماژول «حسابداری داخلی ایتام».
    //
    // آموزش — به‌درخواست جدی کاربر: بکاپ اصلی برنامه (BackupHelper) اصلاً
    // جداول حسابداری (Acc*) را نمی‌گیرد؛ یعنی تا امروز داده‌های مالی هیچ بکاپ
    // مستقلی نداشتند. این کلاس کاملاً جدا از BackupHelper عمومی است تا بتوان
    // فقط بخش حسابداری را (بدون لمس پرونده‌ها/کاربران/تنظیمات) پشتیبان‌گیری،
    // منتقل، یا در دستگاه/نصب دیگری بازیابی کرد.
    //
    // چون جداول Acc* ستون GlobalID (کلید ادغام چندنصبی) ندارند، بازیابی به‌جای
    // «ادغام هوشمند» یک «جایگزینی کامل» (Full Replace) است: دقیقاً مثل
    // برگرداندن یک عکس لحظه‌ای (Snapshot) از حسابداری. برای ایمنی:
    //   ۱) قبل از هرگونه نوشتن، یک بکاپ خودکار از وضعیت فعلی گرفته می‌شود
    //      (تا در صورت اشتباه، قابل بازگشت باشد).
    //   ۲) کل عملیات در یک تراکنش است؛ هر خطا = Rollback کامل بدون تغییر جزئی.
    // ─────────────────────────────────────────────────────────────────────────
    public static class AccountingBackupHelper
    {
        private const string DataFileName = "AccountingBackup.xml";

        // ترتیب جداول: مرجع‌ها (دوره/صندوق/طرف‌حساب/دسته‌بندی) قبل از جداولی
        // که به آن‌ها اشاره می‌کنند (بدون FK واقعی در schema، اما ترتیب منطقی
        // خوانایی/ایمنی را بالا می‌برد).
        private static readonly string[] Tables =
        {
            "AccPeriod", "AccFund", "AccParty", "AccIncomeCategory", "AccExpenseCategory",
            "AccTransaction", "AccStipend", "AccSalary", "AccExpenseItem",
            "AccSettings", "AccAudit"
        };

        // ─── گرفتن بکاپ: همه‌ی جداول Acc* (همه‌ی مراکز، نه فقط مرکز جاری —
        // بکاپ یک عملیات مدیریتیِ کل‌سیستمی است، هم‌راستا با BackupHelper اصلی) ─
        public static string ExportBackup(string parentFolder)
        {
            if (string.IsNullOrWhiteSpace(parentFolder))
                throw new Exception("مسیر بکاپ خالی است.");

            string backupFolder = Path.Combine(
                parentFolder,
                "AccountingBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(backupFolder);

            DataSet dataSet = new DataSet("AccountingBackup");
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                foreach (string table in Tables)
                    LoadTable(con, dataSet, table);
            }

            dataSet.WriteXml(Path.Combine(backupFolder, DataFileName), XmlWriteMode.WriteSchema);
            return backupFolder;
        }

        // ─── بازیابی: جایگزینی کامل (با بکاپ ایمنی خودکار قبل از شروع) ───────
        public static void ImportBackup(string backupFolder)
        {
            ImportBackup(backupFolder, null);
        }

        // اورلودِ داخلی — safetyBackupPassword در صورت پر بودن، بکاپِ ایمنیِ
        // خودکار را هم رمزنگاری می‌کند (مسیرِ فراخوانیِ ImportEncryptedBackup،
        // که قبلاً رمز را از کاربر گرفته). null یعنی رفتارِ قدیمی (بکاپِ ایمنیِ
        // رمزنگاری‌نشده) — برای فراخوان‌های موجودِ این متد بدون تغییر می‌ماند.
        public static void ImportBackup(string backupFolder, string safetyBackupPassword)
        {
            if (string.IsNullOrWhiteSpace(backupFolder))
                throw new Exception("مسیر بکاپ خالی است.");

            string dataPath = Path.Combine(backupFolder, DataFileName);
            if (!File.Exists(dataPath))
                throw new Exception("فایل بکاپ حسابداری پیدا نشد: " + dataPath);

            DataSet dataSet = new DataSet();
            dataSet.ReadXml(dataPath, XmlReadMode.ReadSchema);

            // ۱) بکاپ ایمنیِ خودکار از وضعیت فعلی، قبل از هر تغییری — اگر این
            // مرحله شکست بخورد، بازیابی به‌هیچ‌وجه ادامه پیدا نمی‌کند (ایمنی
            // داده مقدم بر بازیابی است).
            string safetyRoot = Path.Combine(
                Path.GetDirectoryName(backupFolder) ?? backupFolder, "AccountingPreRestoreSafety");
            Directory.CreateDirectory(safetyRoot);

            if (string.IsNullOrEmpty(safetyBackupPassword))
                ExportBackup(safetyRoot);
            else
                ExportEncryptedBackup(safetyRoot, safetyBackupPassword);

            // ۲) جایگزینی کامل داخل یک تراکنش
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                using (SQLiteTransaction tr = con.BeginTransaction())
                {
                    try
                    {
                        // حذف به ترتیبِ معکوسِ وابستگی منطقی (جداولِ وابسته اول)
                        for (int i = Tables.Length - 1; i >= 0; i--)
                            ExecuteNonQuery(con, tr, "DELETE FROM " + Tables[i]);

                        // درج به ترتیبِ مرجع‌ها اول
                        foreach (string table in Tables)
                            if (dataSet.Tables.Contains(table))
                                InsertTable(con, tr, table, dataSet.Tables[table]);

                        tr.Commit();
                    }
                    catch
                    {
                        try { tr.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // نسخهٔ رمزنگاری‌شده (نسخهٔ ۱٫۰ — Option D)، عیناً همان الگوی BackupHelper.
        // ─────────────────────────────────────────────────────────────────────
        public static string ExportEncryptedBackup(string parentFolder, string password)
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

                AuditLogger.Log("بکاپ رمزنگاری‌شده حسابداری", "AccountingBackup", 0, "", encryptedPath);
                return encryptedPath;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("خطا در بکاپ رمزنگاری‌شده حسابداری", "AccountingBackup", 0, "", ex.Message);
                throw;
            }
            finally
            {
                if (plainFolder != null) BackupEncryption.TryDeleteDirectory(plainFolder);
                if (zipPath != null) BackupEncryption.TryDelete(zipPath);
            }
        }

        public static void ImportEncryptedBackup(string encryptedFilePath, string password)
        {
            if (string.IsNullOrWhiteSpace(encryptedFilePath) || !File.Exists(encryptedFilePath))
                throw new Exception("فایل بکاپ رمزنگاری‌شدهٔ حسابداری پیدا نشد.");

            string tempZip = Path.Combine(Path.GetTempPath(), "CMAccRestore_" + Guid.NewGuid().ToString("N") + ".zip");
            string tempFolder = Path.Combine(Path.GetTempPath(), "CMAccRestore_" + Guid.NewGuid().ToString("N"));

            try
            {
                BackupEncryption.DecryptFile(encryptedFilePath, tempZip, password);
                ZipFile.ExtractToDirectory(tempZip, tempFolder);

                // رمزِ همین بازیابی برای بکاپِ ایمنیِ داخلی هم استفاده می‌شود —
                // بدونِ این، آن بکاپِ خودکار به‌صورتِ متنِ‌ساده روی دیسک می‌ماند.
                ImportBackup(tempFolder, password);
                AuditLogger.Log("بازیابیِ بکاپ رمزنگاری‌شدهٔ حسابداری", "AccountingBackup", 0, "", encryptedFilePath);
            }
            catch (Exception ex)
            {
                AuditLogger.Log("خطا در بازیابیِ بکاپ رمزنگاری‌شدهٔ حسابداری", "AccountingBackup", 0, "", ex.Message);
                throw;
            }
            finally
            {
                BackupEncryption.TryDelete(tempZip);
                BackupEncryption.TryDeleteDirectory(tempFolder);
            }
        }

        private static void LoadTable(SQLiteConnection con, DataSet dataSet, string table)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM " + table, con))
            using (var da = new SQLiteDataAdapter(cmd))
            {
                var dt = new DataTable(table);
                da.Fill(dt);
                dataSet.Tables.Add(dt);
            }
        }

        private static void ExecuteNonQuery(SQLiteConnection con, SQLiteTransaction tr, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, con, tr))
                cmd.ExecuteNonQuery();
        }

        // درج تمام ردیف‌های یک DataTable در جدول مقصد؛ فقط ستون‌هایی که واقعاً
        // در جدول دیتابیس فعلی وجود دارند نوشته می‌شوند (تحمل تفاوت نسخه‌ی
        // schema بین بکاپ قدیمی و نصب فعلی، بدون خطا).
        private static void InsertTable(SQLiteConnection con, SQLiteTransaction tr, string table, DataTable data)
        {
            if (data.Rows.Count == 0) return;

            var validCols = new System.Collections.Generic.List<string>();
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + table + ")", con, tr))
            using (var dr = cmd.ExecuteReader())
                while (dr.Read())
                    validCols.Add(dr["name"].ToString());

            var cols = new System.Collections.Generic.List<string>();
            foreach (DataColumn c in data.Columns)
                if (validCols.Contains(c.ColumnName)) cols.Add(c.ColumnName);
            if (cols.Count == 0) return;

            string colList = string.Join(",", cols.ConvertAll(c => "[" + c + "]").ToArray());
            string parList = string.Join(",", cols.ConvertAll(c => "@" + c).ToArray());
            string sql = "INSERT INTO " + table + " (" + colList + ") VALUES (" + parList + ")";

            foreach (DataRow row in data.Rows)
            {
                using (var cmd = new SQLiteCommand(sql, con, tr))
                {
                    foreach (string c in cols)
                        cmd.Parameters.AddWithValue("@" + c, row[c] == null ? DBNull.Value : row[c]);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}

using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace CaseManagement.Helpers
{
    /// <summary>
    /// Resumable copy-first migration. Sources are never moved or deleted.
    /// Database pointers switch only after a byte hash has been verified.
    /// </summary>
    public sealed class FileStorageMigrationService
    {
        private readonly DatabaseHelper _db;
        private readonly CaseFileInventory _inventory;
        private readonly FileCatalogService _catalog;

        public FileStorageMigrationService()
        {
            _db = new DatabaseHelper();
            _inventory = new CaseFileInventory(_db);
            _catalog = new FileCatalogService(_db);
        }

        public sealed class Result
        {
            public int CasesScanned;
            public int FilesCopied;
            public int FilesReused;
            public int Missing;
            public int Failed;
            public string TargetRoot = "";
            public readonly List<string> Errors = new List<string>();
            public bool Succeeded { get { return Failed == 0; } }
        }

        public static bool IsSafeMode { get; private set; }
        public static Result LastStartupResult { get; private set; }

        public static void RecordStartupLayout(Result result)
        {
            if (result == null)
            {
                LastStartupResult = new Result();
                LastStartupResult.Failed = 1;
                LastStartupResult.Errors.Add("نتیجه مهاجرت پوشه پرونده‌ها خالی است.");
            }
            else
            {
                LastStartupResult = result;
            }
            IsSafeMode = LastStartupResult == null || !LastStartupResult.Succeeded;
        }

        public static bool ShouldRunMaintenanceJobs()
        {
            return !IsSafeMode;
        }

        public static Result RetryCurrentLayout()
        {
            string current = FileHelper.GetBaseRootFolder();
            string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            bool volatileRoot = Path.GetFullPath(current)
                .StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase);
            string target = volatileRoot
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CaseManagement", "Storage")
                : current;
            Result result = new FileStorageMigrationService().MigrateAll(target);
            RecordStartupLayout(result);
            RunMaintenanceIfSafe();
            return result;
        }

        public static void RunMaintenanceIfSafe()
        {
            if (!ShouldRunMaintenanceJobs())
                return;

            try { new FileCatalogService().ReconcileUnownedAssets(500); } catch { }
            try { TempArtifactScope.SweepStale(TimeSpan.FromDays(1)); } catch { }
            try { new CaseFileDeletionService().RetryQueue(); } catch { }
        }

        public static string SafeModeMessage()
        {
            if (!IsSafeMode || LastStartupResult == null)
                return "";
            string detail;
            CanContinueStartup(LastStartupResult, out detail);
            return "برنامه در حالت ایمن است: مهاجرت پوشه پرونده‌ها ناتمام ماند. بکاپ خودکار و پاکسازی اجرا نشد. از تنظیمات → فایل‌ها می‌توانید تکرار مهاجرت یا بازگشت اشاره‌گر را بزنید.\n" + detail;
        }

        public static bool CanContinueStartup(Result result, out string message)
        {
            if (result != null && result.Succeeded)
            {
                message = "";
                return true;
            }

            var lines = new List<string>();
            if (result == null)
            {
                lines.Add("نتیجه مهاجرت پوشه پرونده‌ها خالی است.");
            }
            else
            {
                lines.Add("تعداد فایل ناموفق: " + result.Failed);
                foreach (string error in result.Errors)
                {
                    if (!string.IsNullOrWhiteSpace(error))
                        lines.Add(error);
                }
            }

            message = string.Join("\n", lines.ToArray());
            return false;
        }

        public Result MigrateAll(string targetRoot)
        {
            if (string.IsNullOrWhiteSpace(targetRoot)) throw new ArgumentNullException("targetRoot");
            targetRoot = Path.GetFullPath(targetRoot);
            Directory.CreateDirectory(targetRoot);
            FileCatalogService.EnsureSchema();

            var result = new Result { TargetRoot = targetRoot };
            DataTable cases = _db.Query("SELECT CasID FROM TblCase ORDER BY CasID;");
            foreach (DataRow row in cases.Rows)
            {
                result.CasesScanned++;
                MigrateCase(Convert.ToInt32(row["CasID"]), targetRoot, result);
            }

            if (result.Succeeded)
            {
                string error;
                if (!FileHelper.SetBaseRootFolder(targetRoot, out error))
                {
                    result.Failed++;
                    result.Errors.Add(error);
                }
                else
                {
                    try { SettingsHelper.Set("StorageLayoutVersion", "2"); } catch { }
                }
            }
            return result;
        }

        public Result MigrateAll()
        {
            return MigrateAll(FileHelper.GetBaseRootFolder());
        }

        public static Result MigrateFromExecutableFolderIfRequired()
        {
            string current = FileHelper.GetBaseRootFolder();
            string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullCurrent = Path.GetFullPath(current);
            if (!fullCurrent.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
                return new Result { TargetRoot = current };

            string stable = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CaseManagement", "Storage");
            return new FileStorageMigrationService().MigrateAll(stable);
        }

        public static Result EnsureCurrentLayout()
        {
            string current = FileHelper.GetBaseRootFolder();
            string version = "";
            try { version = SettingsHelper.Get("StorageLayoutVersion"); } catch { }

            string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            bool volatileRoot = Path.GetFullPath(current)
                .StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase);

            if (string.Equals(version, "Rollback", StringComparison.OrdinalIgnoreCase))
                return new Result { TargetRoot = current };

            if (string.Equals(version, "2", StringComparison.Ordinal) && !volatileRoot)
                return new Result { TargetRoot = current };

            string target = volatileRoot
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CaseManagement", "Storage")
                : current;
            return new FileStorageMigrationService().MigrateAll(target);
        }

        public int RollbackPointers()
        {
            FileCatalogService.EnsureSchema();
            DataTable rows = _db.Query(@"
SELECT JournalID,OwnerTable,OwnerLocalID,ColumnName,SourcePath,DestinationPath
FROM FileMigrationJournal
WHERE State='Completed' AND NULLIF(OwnerTable,'') IS NOT NULL
ORDER BY JournalID DESC;");
            int restored = 0;
            int failed = 0;
            foreach (DataRow row in rows.Rows)
            {
                string table = Convert.ToString(row["OwnerTable"]);
                string column = Convert.ToString(row["ColumnName"]);
                int id = Convert.ToInt32(row["OwnerLocalID"]);
                string key = PrimaryKey(table);
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(column)) continue;

                try
                {
                    using (SQLiteConnection con = _db.GetConnection())
                    {
                        con.Open();
                        using (SQLiteTransaction tr = con.BeginTransaction())
                        {
                            string source = Convert.ToString(row["SourcePath"]);
                            string destination = Convert.ToString(row["DestinationPath"]);
                            string current;
                            using (SQLiteCommand read = new SQLiteCommand(
                                "SELECT [" + column + "] FROM [" + table +
                                "] WHERE [" + key + "]=@id LIMIT 1;", con, tr))
                            {
                                read.Parameters.AddWithValue("@id", id);
                                object value = read.ExecuteScalar();
                                current = value == null || value == DBNull.Value
                                    ? "" : Convert.ToString(value);
                            }

                            if (!string.Equals(current, source,
                                StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.Equals(current, destination,
                                    StringComparison.OrdinalIgnoreCase))
                                    throw new InvalidOperationException(
                                        "pointer پس از migration تغییر کرده و rollback نشد.");

                                Execute(con, tr, "UPDATE [" + table + "] SET [" + column +
                                    "]=@p WHERE [" + key + "]=@id;",
                                    new SQLiteParameter("@p", source),
                                    new SQLiteParameter("@id", id));
                            }
                            Execute(con, tr, @"UPDATE FileMigrationJournal SET State='RolledBack',
                                CompletedAt=datetime('now') WHERE JournalID=@id;",
                                new SQLiteParameter("@id", Convert.ToInt64(row["JournalID"])));
                            tr.Commit();
                        }
                    }
                    restored++;
                }
                catch { failed++; }
            }
            if (failed == 0)
            {
                try { SettingsHelper.Set("StorageLayoutVersion", "Rollback"); } catch { }
            }
            return restored;
        }

        private void MigrateCase(int caseId, string targetRoot, Result result)
        {
            CaseFileInventory.CaseContext inventory =
                _inventory.ScanCase(caseId, true, true);
            if (inventory == null || string.IsNullOrWhiteSpace(inventory.CaseCode)) return;

            string caseFolder = BuildCaseFolder(targetRoot, inventory);
            Directory.CreateDirectory(caseFolder);
            HarvestLegacyCaseSiblings(inventory);

            foreach (CaseFileInventory.FileEntry entry in inventory.Files)
            {
                if (!entry.Exists)
                {
                    if (entry.Referenced) result.Missing++;
                    continue;
                }

                try { MigrateEntry(inventory, entry, caseFolder, targetRoot, result); }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add(inventory.CaseCode + ": " + ex.Message);
                    JournalFailure(inventory.CaseCode, entry, ex.Message);
                }
            }
        }

        private static void HarvestLegacyCaseSiblings(CaseFileInventory.CaseContext inventory)
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CaseFileInventory.FileEntry entry in inventory.Files)
            {
                string current = entry.FullPath;
                try
                {
                    DirectoryInfo directory = new FileInfo(current).Directory;
                    while (directory != null)
                    {
                        if (string.Equals(directory.Name, FileHelper.CleanName(inventory.CaseCode),
                            StringComparison.OrdinalIgnoreCase))
                        {
                            folders.Add(directory.FullName);
                            break;
                        }
                        directory = directory.Parent;
                    }
                }
                catch { }
            }

            var known = new HashSet<string>(inventory.Files.ConvertAll(f => f.FullPath),
                StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                string[] files;
                try { files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories); }
                catch { continue; }
                foreach (string file in files)
                {
                    string full;
                    try { full = Path.GetFullPath(file); } catch { continue; }
                    if (!known.Add(full) || full.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                    long size = 0;
                    try { size = new FileInfo(full).Length; } catch { }
                    string first = "";
                    try
                    {
                        string relative = full.Substring(folder.TrimEnd('\\', '/').Length)
                            .TrimStart('\\', '/');
                        first = relative.Split(Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar)[0];
                    }
                    catch { }
                    inventory.Files.Add(new CaseFileInventory.FileEntry
                    {
                        Kind = FileKinds.FromFolder(first),
                        StoredPath = full,
                        FullPath = full,
                        Exists = true,
                        Referenced = false,
                        SizeBytes = size,
                        ContentHash = CaseFileInventory.ComputeHash(full)
                    });
                }
            }
        }

        private void MigrateEntry(CaseFileInventory.CaseContext context,
            CaseFileInventory.FileEntry entry, string caseFolder, string targetRoot, Result result)
        {
            string section = FileKindPolicy.SectionForKind(entry.Kind);
            string folder = Path.Combine(caseFolder, FileHelper.DiskFolderForSection(section));
            Directory.CreateDirectory(folder);

            string extension = Path.GetExtension(entry.FullPath);
            string contextName = entry.OwnerId > 0
                ? entry.OwnerId.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            DateTime date;
            try { date = File.GetLastWriteTime(entry.FullPath); }
            catch { date = DateTime.Now; }

            string destination = FindDestination(folder, context.CaseCode, entry.Kind,
                contextName, date, extension, entry.ContentHash);
            long journalId = StartJournal(context.CaseCode, entry, destination);

            string destinationHash;
            if (File.Exists(destination))
            {
                destinationHash = CaseFileInventory.ComputeHash(destination);
                if (!string.Equals(destinationHash, entry.ContentHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("فایل مقصد موجود است ولی هش آن با منبع برابر نیست.");
                result.FilesReused++;
            }
            else
            {
                CopyVerified(entry.FullPath, destination, entry.ContentHash);
                destinationHash = CaseFileInventory.ComputeHash(destination);
                result.FilesCopied++;
            }

            FileCatalogService.Asset asset = _catalog.Register(context.CaseCode,
                entry.Kind, destination, entry.OwnerTable, entry.OwnerId,
                entry.OwnerGlobalId, entry.ColumnName, Path.GetFileName(entry.StoredPath),
                targetRoot);

            CompletePointerAndJournal(entry, destination, asset, destinationHash, journalId);
        }

        private static string BuildCaseFolder(string root, CaseFileInventory.CaseContext c)
        {
            return Path.Combine(root,
                FileHelper.ClassifySegment(c.Province),
                FileHelper.ClassifySegment(c.District),
                FileHelper.ClassifySegment(c.RequestType),
                FileHelper.ClassifySegment(c.ServiceStatus),
                FileHelper.CleanName(c.CaseCode));
        }

        private static string FindDestination(string folder, string code, string kind,
            string context, DateTime date, string extension, string expectedHash)
        {
            for (int sequence = 1; sequence < 10000; sequence++)
            {
                string candidate = Path.Combine(folder,
                    FileNamingPolicy.Build(code, kind, context, date, sequence, extension));
                if (!File.Exists(candidate)) return candidate;
                string hash = CaseFileInventory.ComputeHash(candidate);
                if (!string.IsNullOrWhiteSpace(expectedHash) &&
                    string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            throw new IOException("برای فایل مقصد نام آزاد پیدا نشد.");
        }

        private static void CopyVerified(string source, string destination, string expectedHash)
        {
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, temporary, false);
                string copiedHash = CaseFileInventory.ComputeHash(temporary);
                if (!string.Equals(copiedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("هش فایل کپی‌شده با منبع برابر نیست.");
                File.Move(temporary, destination);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            }
        }

        private void CompletePointerAndJournal(CaseFileInventory.FileEntry entry,
            string destination, FileCatalogService.Asset asset, string destinationHash,
            long journalId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            {
                con.Open();
                using (SQLiteTransaction tr = con.BeginTransaction())
                {
                    if (entry.Referenced)
                    {
                        string key = PrimaryKey(entry.OwnerTable);
                        if (string.IsNullOrWhiteSpace(key) ||
                            string.IsNullOrWhiteSpace(entry.ColumnName))
                            throw new InvalidOperationException("مالک فایل برای تغییر pointer معتبر نیست.");

                        Execute(con, tr, "UPDATE [" + entry.OwnerTable + "] SET [" +
                            entry.ColumnName + "]=@p WHERE [" + key + "]=@id;",
                            new SQLiteParameter("@p", destination),
                            new SQLiteParameter("@id", entry.OwnerId));
                    }

                    Execute(con, tr, @"
UPDATE FileMigrationJournal SET FileGlobalID=@fg,ContentHash=@h,SizeBytes=@s,
 State='Completed',CompletedAt=datetime('now'),ErrorMessage=NULL WHERE JournalID=@id;",
                        new SQLiteParameter("@fg", asset.FileGlobalId),
                        new SQLiteParameter("@h", destinationHash),
                        new SQLiteParameter("@s", asset.SizeBytes),
                        new SQLiteParameter("@id", journalId));
                    tr.Commit();
                }
            }
        }

        private long StartJournal(string caseCode, CaseFileInventory.FileEntry entry, string destination)
        {
            return _db.ExecuteInsertReturningId(@"
INSERT INTO FileMigrationJournal
(CaseCode,OwnerTable,OwnerLocalID,ColumnName,SourcePath,DestinationPath,ContentHash,SizeBytes,State)
VALUES (@c,@t,@i,@col,@src,@dst,@h,@s,'Copying');",
                new SQLiteParameter("@c", caseCode),
                new SQLiteParameter("@t", entry.OwnerTable ?? ""),
                new SQLiteParameter("@i", entry.OwnerId),
                new SQLiteParameter("@col", entry.ColumnName ?? ""),
                new SQLiteParameter("@src", entry.FullPath),
                new SQLiteParameter("@dst", destination),
                new SQLiteParameter("@h", entry.ContentHash ?? ""),
                new SQLiteParameter("@s", entry.SizeBytes));
        }

        private void JournalFailure(string caseCode, CaseFileInventory.FileEntry entry, string error)
        {
            try
            {
                _db.ExecuteNonQuery(@"
UPDATE FileMigrationJournal SET State='Failed',ErrorMessage=@e,CompletedAt=datetime('now')
WHERE JournalID=(SELECT MAX(JournalID) FROM FileMigrationJournal
 WHERE CaseCode=@c AND SourcePath=@p AND State='Copying');",
                    new SQLiteParameter("@e", error ?? ""),
                    new SQLiteParameter("@c", caseCode ?? ""),
                    new SQLiteParameter("@p", entry.FullPath ?? ""));
            }
            catch { }
        }

        private static string PrimaryKey(string table)
        {
            if (string.Equals(table, "TblCase", StringComparison.OrdinalIgnoreCase)) return "CasID";
            if (string.Equals(table, "TblFamily", StringComparison.OrdinalIgnoreCase)) return "FamID";
            if (string.Equals(table, "TblOrphan", StringComparison.OrdinalIgnoreCase)) return "OrphanID";
            if (string.Equals(table, "TblCaseRepresentative", StringComparison.OrdinalIgnoreCase)) return "RepresentativeID";
            if (string.Equals(table, "TblDocs", StringComparison.OrdinalIgnoreCase)) return "DocID";
            if (string.Equals(table, "TblFieldVisitPhoto", StringComparison.OrdinalIgnoreCase)) return "PhotoID";
            return "";
        }

        private static void Execute(SQLiteConnection con, SQLiteTransaction tr,
            string sql, params SQLiteParameter[] parameters)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con, tr))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

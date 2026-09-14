using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CaseManagement.Helpers
{
    public class FileCleanupHelper
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        public List<string> FindUnusedFiles()
        {
            string root = FileHelper.GetBaseRootFolder();
            List<string> unused = new List<string>();

            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return unused;

            HashSet<string> usedFiles = LoadUsedFilePaths();

            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkip(file, root))
                    continue;

                string fullPath = SafeFullPath(file);

                if (!string.IsNullOrWhiteSpace(fullPath) && !usedFiles.Contains(fullPath))
                    unused.Add(fullPath);
            }

            return unused;
        }

        public int DeleteFiles(IEnumerable<string> files)
        {
            int count = 0;
            string root = FileHelper.GetBaseRootFolder();
            var eligible = new HashSet<string>(FindUnusedFiles(),
                StringComparer.OrdinalIgnoreCase);
            string quarantine = Path.Combine(root, "_Quarantine", "Cleanup",
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" +
                Guid.NewGuid().ToString("N"));
            var manifest = new StringBuilder();
            bool quarantineMustRemain = false;

            foreach (string file in files)
            {
                try
                {
                    string fullPath = SafeFullPath(file);

                    if (string.IsNullOrWhiteSpace(fullPath) ||
                        !IsInsideRoot(fullPath, root) ||
                        !eligible.Contains(fullPath) ||
                        ShouldSkip(fullPath, root))
                        continue;

                    if (File.Exists(fullPath))
                    {
                        string hash = CaseFileInventory.ComputeHash(fullPath);
                        if (string.IsNullOrWhiteSpace(hash)) continue;

                        string relative = fullPath.Substring(
                            Path.GetFullPath(root).TrimEnd(
                                Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar).Length)
                            .TrimStart(Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar);
                        string destination = Path.Combine(quarantine, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Move(fullPath, destination);

                        string movedHash = CaseFileInventory.ComputeHash(destination);
                        if (!string.Equals(hash, movedHash,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            bool restored = false;
                            try
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                                File.Move(destination, fullPath);
                                restored = true;
                            }
                            catch { }
                            if (!restored)
                            {
                                quarantineMustRemain = true;
                                manifest.Append(fullPath).Append('\t')
                                    .Append(destination).Append('\t')
                                    .Append(hash).Append('\t')
                                    .Append("HashVerificationFailed").AppendLine();
                            }
                            continue;
                        }

                        manifest.Append(fullPath).Append('\t')
                            .Append(destination).Append('\t')
                            .Append(hash).AppendLine();
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[FileCleanupHelper.DeleteFiles] " + file + " | " + ex.Message);
                }
            }

            if (count > 0 || quarantineMustRemain)
            {
                Directory.CreateDirectory(quarantine);
                File.WriteAllText(Path.Combine(quarantine, "manifest.tsv"),
                    manifest.ToString(), new UTF8Encoding(false));
                AuditLogger.Log("قرنطینه فایل", "Files", 0, "",
                    "تعداد فایل منتقل‌شده به قرنطینه: " + count +
                    " | " + quarantine);
            }
            else
            {
                try
                {
                    if (Directory.Exists(quarantine))
                        Directory.Delete(quarantine, true);
                }
                catch { }
            }

            return count;
        }

        private HashSet<string> LoadUsedFilePaths()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                FileCatalogService.EnsureSchema();
                foreach (System.Data.DataRow row in new FileCatalogService().GetAllActive().Rows)
                {
                    string full = FileCatalogService.ResolveAssetPath(row);
                    if (!string.IsNullOrWhiteSpace(full)) paths.Add(full);
                }
            }
            catch { }

            AddStoredPaths(paths, "TblCase", "PhotoPath");
            AddStoredPaths(paths, "TblCase", "FamilyPhotoPath");
            AddStoredPaths(paths, "TblFamily", "MemberPhotoPath");
            AddStoredPaths(paths, "TblOrphan", "GuardianPhotoPath");
            AddStoredPaths(paths, "TblCaseRepresentative", "PhotoPath");
            AddStoredPaths(paths, "TblFieldVisitPhoto", "FilePath");
            AddStoredPaths(paths, "TblDocs", "DocFilePath");

            return paths;
        }

        private void AddStoredPaths(HashSet<string> paths, string table, string column)
        {
            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT [" + column + "] AS FilePath FROM [" + table +
                    "] WHERE NULLIF([" + column + "],'') IS NOT NULL;", con))
                {
                    con.Open();
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string full = CaseFileInventory.ResolveStoredPath(
                                Convert.ToString(reader["FilePath"]));
                            if (!string.IsNullOrWhiteSpace(full)) paths.Add(full);
                        }
                    }
                }
            }
            catch { }
        }

        private static bool ShouldSkip(string file, string root)
        {
            string relative = file.Substring(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return relative.StartsWith("AutoBackups", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("ExcelReports", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("CaseManagementBackup_", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("_System", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("_Reports", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("_Backups", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("_Quarantine", StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeFullPath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return "";

                return Path.GetFullPath(path);
            }
            catch
            {
                return "";
            }
        }

        private static bool IsInsideRoot(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
                return false;

            string fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return Path.GetFullPath(path).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}

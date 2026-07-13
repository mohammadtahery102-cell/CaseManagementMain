using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

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

            foreach (string file in files)
            {
                try
                {
                    string fullPath = SafeFullPath(file);

                    if (string.IsNullOrWhiteSpace(fullPath) || !IsInsideRoot(fullPath, root))
                        continue;

                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[FileCleanupHelper.DeleteFiles] " + file + " | " + ex.Message);
                }
            }

            if (count > 0)
                AuditLogger.Log("پاکسازی فایل", "Files", 0, "", "تعداد فایل حذف‌شده: " + count);

            return count;
        }

        private HashSet<string> LoadUsedFilePaths()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT PhotoPath AS FilePath FROM TblCase WHERE NULLIF(PhotoPath, '') IS NOT NULL
UNION
SELECT FamilyPhotoPath FROM TblCase WHERE NULLIF(FamilyPhotoPath, '') IS NOT NULL
UNION
SELECT MemberPhotoPath FROM TblFamily WHERE NULLIF(MemberPhotoPath, '') IS NOT NULL
UNION
SELECT DocFilePath FROM TblDocs WHERE NULLIF(DocFilePath, '') IS NOT NULL", con))
            {
                con.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        string path = dr["FilePath"] == DBNull.Value ? "" : dr["FilePath"].ToString();
                        string fullPath = SafeFullPath(path);

                        if (!string.IsNullOrWhiteSpace(fullPath))
                            paths.Add(fullPath);
                    }
                }
            }

            return paths;
        }

        private static bool ShouldSkip(string file, string root)
        {
            string relative = file.Substring(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return relative.StartsWith("AutoBackups", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("ExcelReports", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("CaseManagementBackup_", StringComparison.OrdinalIgnoreCase);
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

using System;
using System.Collections.Generic;
using System.IO;

namespace CaseManagement.Helpers
{
    public sealed class TempArtifactScope : IDisposable
    {
        private readonly List<string> _artifacts = new List<string>();
        private readonly string _folder;
        private bool _disposed;

        public TempArtifactScope(string caseCode)
        {
            _folder = string.IsNullOrWhiteSpace(caseCode)
                ? Path.Combine(FileHelper.GetBaseRootFolder(), "_System", "Temp",
                    Guid.NewGuid().ToString("N"))
                : Path.Combine(FileHelper.GetSectionFolder(caseCode, FileHelper.SectionTemp),
                    Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        public string CreatePath(string extension)
        {
            string ext = (extension ?? "").Trim();
            if (ext.Length > 0 && !ext.StartsWith(".", StringComparison.Ordinal)) ext = "." + ext;
            string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ext);
            _artifacts.Add(path);
            return path;
        }

        public void Track(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !_artifacts.Contains(path))
                _artifacts.Add(path);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _artifacts.Count - 1; i >= 0; i--)
                TryDeleteFile(_artifacts[i]);
            TryDeleteDirectory(_folder);
        }

        public static int SweepStale(TimeSpan minimumAge)
        {
            string root = FileHelper.GetBaseRootFolder();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return 0;
            int removed = 0;
            DateTime cutoff = DateTime.UtcNow.Subtract(minimumAge);

            foreach (string directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(directory);
                string parent = Path.GetFileName(Path.GetDirectoryName(directory) ?? "");
                if (!string.Equals(name, FileHelper.SectionTemp, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(parent, FileHelper.SectionTemp, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(parent, "Temp", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    if (Directory.GetLastWriteTimeUtc(directory) > cutoff) continue;
                    Directory.Delete(directory, true);
                    removed++;
                }
                catch { }
            }
            removed += SweepKnownOsTemp(cutoff);
            return removed;
        }

        private static int SweepKnownOsTemp(DateTime cutoff)
        {
            int removed = 0;
            string temp = Path.GetTempPath();
            string[] patterns =
            {
                "CMRestore_*", "CMVerify_*", "cm_media_*", "geo_poster_*",
                "CaseManagement_GuardianCardWork_*",
                "CaseManagement_AssistanceReceiptWork_*"
            };
            foreach (string pattern in patterns)
            {
                string[] entries;
                try { entries = Directory.GetFileSystemEntries(temp, pattern); }
                catch { continue; }
                foreach (string entry in entries)
                {
                    try
                    {
                        DateTime modified = File.Exists(entry)
                            ? File.GetLastWriteTimeUtc(entry) : Directory.GetLastWriteTimeUtc(entry);
                        if (modified > cutoff) continue;
                        if (File.Exists(entry)) File.Delete(entry);
                        else if (Directory.Exists(entry)) Directory.Delete(entry, true);
                        removed++;
                    }
                    catch { }
                }
            }
            return removed;
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }
    }
}

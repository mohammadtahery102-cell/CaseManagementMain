using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CaseManagement.Helpers
{
    /// <summary>
    /// Read-only inventory of every file owned by a case.  This is the single
    /// discovery source used by migration, deletion, cleanup and backup checks.
    /// It deliberately performs no copy, delete or database update.
    /// </summary>
    public sealed class CaseFileInventory
    {
        private readonly DatabaseHelper _db;

        public CaseFileInventory() : this(new DatabaseHelper()) { }

        public CaseFileInventory(DatabaseHelper db)
        {
            _db = db ?? throw new ArgumentNullException("db");
        }

        public sealed class CaseContext
        {
            public int CaseId;
            public string CaseCode = "";
            public string CaseGlobalId = "";
            public string Province = "";
            public string District = "";
            public string RequestType = "";
            public string ServiceStatus = "";
            public string CaseFolder = "";
            public readonly List<FileEntry> Files = new List<FileEntry>();
        }

        public sealed class FileEntry
        {
            public string OwnerTable = "";
            public int OwnerId;
            public string OwnerGlobalId = "";
            public string ColumnName = "";
            public string Kind = "";
            public string StoredPath = "";
            public string FullPath = "";
            public string RelativePath = "";
            public string ContentHash = "";
            public long SizeBytes;
            public bool Exists;
            public bool Referenced;
        }

        private sealed class Source
        {
            public string Table;
            public string Key;
            public string GlobalId;
            public string Column;
            public string Kind;
            public string Where;
        }

        private static readonly Source[] Sources =
        {
            new Source { Table = "TblCase", Key = "CasID", GlobalId = "GlobalID", Column = "PhotoPath", Kind = FileKinds.HeadGuardian, Where = "CasID = @Id" },
            new Source { Table = "TblCase", Key = "CasID", GlobalId = "GlobalID", Column = "FamilyPhotoPath", Kind = FileKinds.Family, Where = "CasID = @Id" },
            new Source { Table = "TblFamily", Key = "FamID", GlobalId = "GlobalID", Column = "MemberPhotoPath", Kind = FileKinds.Member, Where = "CasID = @Id" },
            new Source { Table = "TblOrphan", Key = "OrphanID", GlobalId = "GlobalID", Column = "GuardianPhotoPath", Kind = FileKinds.OrphanGuardian, Where = "CasID = @Id" },
            new Source { Table = "TblCaseRepresentative", Key = "RepresentativeID", GlobalId = "GlobalID", Column = "PhotoPath", Kind = FileKinds.Representative, Where = "CasID = @Id" },
            new Source { Table = "TblDocs", Key = "DocID", GlobalId = "GlobalID", Column = "DocFilePath", Kind = FileKinds.Document, Where = "CasID = @Id" },
            new Source { Table = "TblFieldVisitPhoto", Key = "PhotoID", GlobalId = "GlobalID", Column = "FilePath", Kind = FileKinds.Visit, Where = "CasID = @Id" }
        };

        public CaseContext ScanCase(int caseId, bool includeDiskFiles, bool computeHashes)
        {
            CaseContext context = LoadContext(caseId);
            if (context == null) return null;

            var byPath = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
            using (SQLiteConnection con = _db.GetConnection())
            {
                con.Open();
                foreach (Source source in Sources)
                    ReadSource(con, caseId, source, context, byPath, computeHashes);
                ReadCatalog(con, context, byPath, computeHashes);
            }

            if (includeDiskFiles && !string.IsNullOrWhiteSpace(context.CaseFolder) &&
                Directory.Exists(context.CaseFolder))
            {
                string[] files;
                try { files = Directory.GetFiles(context.CaseFolder, "*", SearchOption.AllDirectories); }
                catch { files = new string[0]; }

                foreach (string file in files)
                {
                    string full = SafeFullPath(file);
                    if (string.IsNullOrWhiteSpace(full) || IsAtomicTemp(file)) continue;
                    if (byPath.ContainsKey(full)) continue;

                    FileEntry entry = BuildEntry("", 0, "", "", InferKind(context.CaseFolder, full),
                        full, false, computeHashes);
                    context.Files.Add(entry);
                    byPath[full] = entry;
                }
            }

            return context;
        }

        private static void ReadCatalog(SQLiteConnection con, CaseContext context,
            Dictionary<string, FileEntry> byPath, bool computeHashes)
        {
            if (!TableExists(con, "TblFileAsset")) return;
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT IFNULL(OwnerTable,'') AS OwnerTable,IFNULL(OwnerLocalID,0) AS OwnerLocalID,
       IFNULL(OwnerGlobalID,'') AS OwnerGlobalID,IFNULL(ColumnName,'') AS ColumnName,
       IFNULL(Kind,'Other') AS Kind,IFNULL(RelativePath,'') AS RelativePath
FROM TblFileAsset WHERE CaseCode=@c AND Status<>'Deleted';", con))
            {
                cmd.Parameters.AddWithValue("@c", context.CaseCode);
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string stored = Convert.ToString(reader["RelativePath"]);
                        string full = ResolveStoredPath(stored);
                        if (string.IsNullOrWhiteSpace(full) || byPath.ContainsKey(full)) continue;
                        FileEntry entry = BuildEntry(
                            Convert.ToString(reader["OwnerTable"]),
                            Convert.ToInt32(reader["OwnerLocalID"]),
                            Convert.ToString(reader["OwnerGlobalID"]),
                            Convert.ToString(reader["ColumnName"]),
                            Convert.ToString(reader["Kind"]),
                            stored, false, computeHashes);
                        context.Files.Add(entry);
                        byPath[full] = entry;
                    }
                }
            }
        }

        public CaseContext ScanCase(int caseId)
        {
            return ScanCase(caseId, true, false);
        }

        public static string ResolveStoredPath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return "";
            try
            {
                string value = storedPath.Trim();
                const string token = "|FileRoot|";
                if (value.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                {
                    string relative = value.Substring(token.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return Path.GetFullPath(Path.Combine(FileHelper.GetBaseRootFolder(), relative));
                }

                if (!Path.IsPathRooted(value))
                    return Path.GetFullPath(Path.Combine(FileHelper.GetBaseRootFolder(), value));

                return Path.GetFullPath(value);
            }
            catch { return ""; }
        }

        public static string ToRelativePath(string fullPath)
        {
            string root = FileHelper.GetBaseRootFolder();
            string full = SafeFullPath(fullPath);
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(full)) return "";

            try
            {
                string normalizedRoot = Path.GetFullPath(root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!full.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return "";
                return full.Substring(normalizedRoot.Length);
            }
            catch { return ""; }
        }

        public static string ComputeHash(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite, 81920, FileOptions.SequentialScan))
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    StringBuilder text = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash) text.Append(b.ToString("x2"));
                    return text.ToString();
                }
            }
            catch { return ""; }
        }

        private CaseContext LoadContext(int caseId)
        {
            DataTable rows = _db.Query(@"
SELECT c.CasID, IFNULL(c.Code,'') AS Code, IFNULL(c.GlobalID,'') AS GlobalID,
       IFNULL(c.Province,'') AS Province, IFNULL(c.District,'') AS District,
       IFNULL(NULLIF(TRIM(rt.Name),''), IFNULL(c.RequestType,'')) AS RequestType,
       IFNULL(NULLIF(TRIM(ss.Name),''), IFNULL(c.ServiceStatus,'')) AS ServiceStatus
FROM TblCase c
LEFT JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID
LEFT JOIN TblServiceStatus ss ON ss.ServiceStatusID = c.ServiceStatusID
WHERE c.CasID = @Id LIMIT 1;", new SQLiteParameter("@Id", caseId));

            if (rows.Rows.Count == 0) return null;
            DataRow row = rows.Rows[0];
            var result = new CaseContext
            {
                CaseId = caseId,
                CaseCode = Str(row, "Code"),
                CaseGlobalId = Str(row, "GlobalID"),
                Province = Str(row, "Province"),
                District = Str(row, "District"),
                RequestType = Str(row, "RequestType"),
                ServiceStatus = Str(row, "ServiceStatus")
            };

            FileHelper.RememberLayout(result.CaseCode, result.Province, result.District,
                result.RequestType, result.ServiceStatus);
            result.CaseFolder = FileHelper.GetCaseFolderPath(result.CaseCode);
            return result;
        }

        private void ReadSource(SQLiteConnection con, int caseId, Source source,
            CaseContext context, Dictionary<string, FileEntry> byPath, bool computeHashes)
        {
            if (!TableExists(con, source.Table) || !ColumnExists(con, source.Table, source.Column))
                return;

            bool hasGlobal = ColumnExists(con, source.Table, source.GlobalId);
            string sql = "SELECT [" + source.Key + "] AS OwnerId, " +
                (hasGlobal ? "IFNULL([" + source.GlobalId + "],'')" : "''") +
                " AS OwnerGlobalId, IFNULL([" + source.Column + "],'') AS StoredPath " +
                "FROM [" + source.Table + "] WHERE " + source.Where +
                " AND NULLIF([" + source.Column + "],'') IS NOT NULL;";

            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Id", caseId);
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string stored = Convert.ToString(reader["StoredPath"]);
                        FileEntry entry = BuildEntry(source.Table,
                            Convert.ToInt32(reader["OwnerId"]),
                            Convert.ToString(reader["OwnerGlobalId"]),
                            source.Column, source.Kind, stored, true, computeHashes);
                        context.Files.Add(entry);
                        if (!string.IsNullOrWhiteSpace(entry.FullPath))
                            byPath[entry.FullPath] = entry;
                    }
                }
            }
        }

        private static FileEntry BuildEntry(string table, int id, string globalId,
            string column, string kind, string storedPath, bool referenced, bool computeHashes)
        {
            string full = ResolveStoredPath(storedPath);
            var entry = new FileEntry
            {
                OwnerTable = table ?? "",
                OwnerId = id,
                OwnerGlobalId = globalId ?? "",
                ColumnName = column ?? "",
                Kind = kind ?? FileKinds.Other,
                StoredPath = storedPath ?? "",
                FullPath = full,
                RelativePath = ToRelativePath(full),
                Exists = !string.IsNullOrWhiteSpace(full) && File.Exists(full),
                Referenced = referenced
            };

            if (entry.Exists)
            {
                try { entry.SizeBytes = new FileInfo(full).Length; } catch { }
                if (computeHashes) entry.ContentHash = ComputeHash(full);
            }
            return entry;
        }

        private static string InferKind(string caseFolder, string fullPath)
        {
            string relative;
            try { relative = fullPath.Substring(caseFolder.TrimEnd('\\', '/').Length).TrimStart('\\', '/'); }
            catch { return FileKinds.Other; }
            string first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            return FileKinds.FromFolder(first);
        }

        private static bool IsAtomicTemp(string path)
        {
            string name = Path.GetFileName(path);
            return name.StartsWith(".", StringComparison.Ordinal) &&
                   name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeFullPath(string path)
        {
            try { return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path); }
            catch { return ""; }
        }

        private static bool TableExists(SQLiteConnection con, string name)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@n;", con))
            {
                cmd.Parameters.AddWithValue("@n", name);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static bool ColumnExists(SQLiteConnection con, string table, string column)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("PRAGMA table_info([" + table + "]);", con))
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    if (string.Equals(Convert.ToString(reader["name"]), column,
                        StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string Str(DataRow row, string column)
        {
            return row[column] == null || row[column] == DBNull.Value ? "" : Convert.ToString(row[column]);
        }
    }

    public static class FileKinds
    {
        public const string HeadGuardian = "HeadGuardian";
        public const string OrphanGuardian = "OrphanGuardian";
        public const string Family = "Family";
        public const string Member = "Member";
        public const string Representative = "Representative";
        public const string Visit = "Visit";
        public const string Document = "Document";
        public const string CaseFile = "CaseFile";
        public const string Export = "Export";
        public const string Other = "Other";

        public static string FromFolder(string folder)
        {
            if (string.Equals(folder, FileHelper.DiskGuardianPhotosFolder, StringComparison.OrdinalIgnoreCase)) return OrphanGuardian;
            if (string.Equals(folder, FileHelper.DiskFamilyPhotosFolder, StringComparison.OrdinalIgnoreCase)) return Family;
            if (string.Equals(folder, FileHelper.SectionMemberPhotos, StringComparison.OrdinalIgnoreCase)) return Member;
            if (string.Equals(folder, FileHelper.SectionRepresentativePhotos, StringComparison.OrdinalIgnoreCase)) return Representative;
            if (string.Equals(folder, FileHelper.SectionVisitPhotos, StringComparison.OrdinalIgnoreCase)) return Visit;
            if (string.Equals(folder, FileHelper.DiskDocumentsFolder, StringComparison.OrdinalIgnoreCase)) return Document;
            if (string.Equals(folder, FileHelper.SectionCaseFiles, StringComparison.OrdinalIgnoreCase)) return CaseFile;
            if (string.Equals(folder, FileHelper.SectionExports, StringComparison.OrdinalIgnoreCase)) return Export;
            if (string.Equals(folder, FileHelper.LegacyDiskPhotoFolder, StringComparison.OrdinalIgnoreCase)) return HeadGuardian;
            if (string.Equals(folder, FileHelper.LegacyDocsFolder, StringComparison.OrdinalIgnoreCase)) return Document;
            return Other;
        }
    }
}

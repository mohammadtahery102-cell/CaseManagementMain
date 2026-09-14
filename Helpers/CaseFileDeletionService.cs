using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace CaseManagement.Helpers
{
    /// <summary>
    /// Two-phase case file deletion. Capture must happen while the case row and
    /// geo identity still exist; Execute happens only after the DB commit.
    /// </summary>
    public sealed class CaseFileDeletionService
    {
        private readonly DatabaseHelper _db = new DatabaseHelper();

        public sealed class Plan
        {
            public int CaseId;
            public string CaseCode = "";
            public string CaseFolder = "";
            public bool DeleteWholeCase;
            public readonly List<string> Files = new List<string>();
        }

        public sealed class Result
        {
            public int Deleted;
            public int Missing;
            public int Queued;
            public int Rejected;
            public bool FolderDeleted;
        }

        public Plan Capture(int caseId)
        {
            CaseFileInventory.CaseContext inventory =
                new CaseFileInventory(_db).ScanCase(caseId, true, false);
            if (inventory == null) return new Plan { CaseId = caseId };

            var plan = new Plan
            {
                CaseId = caseId,
                CaseCode = inventory.CaseCode,
                CaseFolder = inventory.CaseFolder,
                DeleteWholeCase = true
            };
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CaseFileInventory.FileEntry entry in inventory.Files)
            {
                if (string.IsNullOrWhiteSpace(entry.FullPath) || !unique.Add(entry.FullPath)) continue;
                plan.Files.Add(entry.FullPath);
            }
            return plan;
        }

        public Plan CaptureOwner(string entityName, int localId)
        {
            if (string.Equals(entityName, "TblCase", StringComparison.OrdinalIgnoreCase))
                return Capture(localId);

            if (string.Equals(entityName, "TblFieldVisit", StringComparison.OrdinalIgnoreCase))
            {
                var visitPlan = new Plan();
                try
                {
                    System.Data.DataTable visit = _db.Query(@"
SELECT v.CasID,IFNULL(c.Code,'') AS CaseCode
FROM TblFieldVisit v LEFT JOIN TblCase c ON c.CasID=v.CasID
WHERE v.VisitID=@id LIMIT 1;", new SQLiteParameter("@id", localId));
                    if (visit.Rows.Count == 0) return visitPlan;
                    visitPlan.CaseId = Convert.ToInt32(visit.Rows[0]["CasID"]);
                    visitPlan.CaseCode = Convert.ToString(visit.Rows[0]["CaseCode"]);
                    System.Data.DataTable photos = _db.Query(@"
SELECT FilePath FROM TblFieldVisitPhoto
WHERE VisitID=@id AND NULLIF(FilePath,'') IS NOT NULL;",
                        new SQLiteParameter("@id", localId));
                    foreach (System.Data.DataRow photo in photos.Rows)
                    {
                        string path = CaseFileInventory.ResolveStoredPath(
                            Convert.ToString(photo["FilePath"]));
                        if (!string.IsNullOrWhiteSpace(path)) visitPlan.Files.Add(path);
                    }
                }
                catch { }
                return visitPlan;
            }

            string key = "", column = "";
            if (string.Equals(entityName, "TblFamily", StringComparison.OrdinalIgnoreCase))
            { key = "FamID"; column = "MemberPhotoPath"; }
            else if (string.Equals(entityName, "TblOrphan", StringComparison.OrdinalIgnoreCase))
            { key = "OrphanID"; column = "GuardianPhotoPath"; }
            else if (string.Equals(entityName, "TblCaseRepresentative", StringComparison.OrdinalIgnoreCase))
            { key = "RepresentativeID"; column = "PhotoPath"; }
            else if (string.Equals(entityName, "TblDocs", StringComparison.OrdinalIgnoreCase))
            { key = "DocID"; column = "DocFilePath"; }
            else if (string.Equals(entityName, "TblFieldVisitPhoto", StringComparison.OrdinalIgnoreCase))
            { key = "PhotoID"; column = "FilePath"; }
            else return new Plan();

            var plan = new Plan();
            try
            {
                System.Data.DataTable rows = _db.Query(
                    "SELECT t.CasID,IFNULL(c.Code,'') AS CaseCode,IFNULL(t.[" + column +
                    "],'') AS FilePath FROM [" + entityName + "] t " +
                    "LEFT JOIN TblCase c ON c.CasID=t.CasID WHERE t.[" + key + "]=@id LIMIT 1;",
                    new SQLiteParameter("@id", localId));
                if (rows.Rows.Count == 0) return plan;
                plan.CaseId = Convert.ToInt32(rows.Rows[0]["CasID"]);
                plan.CaseCode = Convert.ToString(rows.Rows[0]["CaseCode"]);
                string path = CaseFileInventory.ResolveStoredPath(Convert.ToString(rows.Rows[0]["FilePath"]));
                if (!string.IsNullOrWhiteSpace(path)) plan.Files.Add(path);
            }
            catch { }
            return plan;
        }

        public Result ExecuteAfterCommit(Plan plan)
        {
            var result = new Result();
            if (plan == null) return result;

            string root = FileHelper.GetBaseRootFolder();
            foreach (string path in plan.Files)
            {
                string full = SafeFull(path);
                if (!IsInside(full, root))
                {
                    result.Rejected++;
                    continue;
                }

                if (!File.Exists(full))
                {
                    result.Missing++;
                    continue;
                }

                try
                {
                    File.Delete(full);
                    result.Deleted++;
                    PruneEmptyParents(Path.GetDirectoryName(full), root);
                }
                catch (Exception ex)
                {
                    Queue(plan.CaseCode, full, ex.Message);
                    result.Queued++;
                }
            }

            string folder = SafeFull(plan.CaseFolder);
            if (plan.DeleteWholeCase && IsInside(folder, root) && Directory.Exists(folder))
            {
                try
                {
                    Directory.Delete(folder, true);
                    result.FolderDeleted = true;
                }
                catch (Exception ex)
                {
                    Queue(plan.CaseCode, folder, ex.Message);
                    result.Queued++;
                }
            }
            else if (plan.DeleteWholeCase && IsInside(folder, root) && !Directory.Exists(folder))
            {
                result.FolderDeleted = true;
            }

            try
            {
                var catalog = new FileCatalogService(_db);
                if (plan.DeleteWholeCase) catalog.MarkCaseDeleted(plan.CaseCode);
                else foreach (string path in plan.Files) catalog.MarkPathDeleted(path);
            }
            catch { }
            return result;
        }

        public int RetryQueue()
        {
            FileCatalogService.EnsureSchema();
            int completed = 0;
            System.Data.DataTable rows = _db.Query(@"
SELECT QueueID,FullPath,IFNULL(ContentHash,'') AS ContentHash FROM FileDeletionQueue
WHERE State IN ('Pending','Failed') ORDER BY QueueID LIMIT 200;");
            string root = FileHelper.GetBaseRootFolder();
            foreach (System.Data.DataRow row in rows.Rows)
            {
                long id = Convert.ToInt64(row["QueueID"]);
                string path = SafeFull(Convert.ToString(row["FullPath"]));
                string expectedHash = Convert.ToString(row["ContentHash"]);
                try
                {
                    if (!IsInside(path, root))
                        throw new InvalidOperationException(
                            "مسیر صف حذف خارج از Root فعلی است و حذف نشد.");

                    if (File.Exists(path) && !string.IsNullOrWhiteSpace(expectedHash))
                    {
                        string actualHash = CaseFileInventory.ComputeHash(path);
                        if (string.IsNullOrWhiteSpace(actualHash) ||
                            !string.Equals(actualHash, expectedHash,
                                StringComparison.OrdinalIgnoreCase))
                            throw new IOException(
                                "محتوای فایل از زمان صف‌شدن تغییر کرده است و حذف نشد.");
                    }

                    if (File.Exists(path)) File.Delete(path);
                    else if (Directory.Exists(path)) Directory.Delete(path, true);
                    _db.ExecuteNonQuery(@"UPDATE FileDeletionQueue SET State='Completed',
                        CompletedAt=datetime('now'),LastError=NULL WHERE QueueID=@id;",
                        new SQLiteParameter("@id", id));
                    completed++;
                }
                catch (Exception ex)
                {
                    _db.ExecuteNonQuery(@"UPDATE FileDeletionQueue SET State='Failed',
                        Attempts=Attempts+1,LastError=@e WHERE QueueID=@id;",
                        new SQLiteParameter("@e", ex.Message),
                        new SQLiteParameter("@id", id));
                }
            }
            return completed;
        }

        private void Queue(string caseCode, string path, string error)
        {
            FileCatalogService.EnsureSchema();
            string hash = File.Exists(path) ? CaseFileInventory.ComputeHash(path) : "";
            _db.ExecuteNonQuery(@"
INSERT INTO FileDeletionQueue (CaseCode,FullPath,ContentHash,State,LastError)
VALUES (@c,@p,@h,'Pending',@e);",
                new SQLiteParameter("@c", caseCode ?? ""),
                new SQLiteParameter("@p", path ?? ""),
                new SQLiteParameter("@h", hash),
                new SQLiteParameter("@e", error ?? ""));
        }

        private static bool IsInside(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            try
            {
                string normalizedRoot = Path.GetFullPath(root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                string normalized = Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return (normalized + Path.DirectorySeparatorChar)
                    .StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string SafeFull(string path)
        {
            try { return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path); }
            catch { return ""; }
        }

        private static void PruneEmptyParents(string folder, string root)
        {
            string current = SafeFull(folder);
            while (IsInside(current, root) &&
                   !string.Equals(current.TrimEnd('\\', '/'),
                       SafeFull(root).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Directory.Exists(current) ||
                        Directory.GetFileSystemEntries(current).Length != 0) return;
                    string parent = Directory.GetParent(current) == null
                        ? "" : Directory.GetParent(current).FullName;
                    Directory.Delete(current, false);
                    current = parent;
                }
                catch { return; }
            }
        }
    }
}

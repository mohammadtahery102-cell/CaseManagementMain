using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace CaseManagement.Helpers
{
    public sealed class FileCatalogService
    {
        private readonly DatabaseHelper _db;

        public FileCatalogService() : this(new DatabaseHelper()) { }
        public FileCatalogService(DatabaseHelper db) { _db = db ?? throw new ArgumentNullException("db"); }

        public sealed class Asset
        {
            public string FileGlobalId = "";
            public string CaseGlobalId = "";
            public string CaseCode = "";
            public string OwnerTable = "";
            public int OwnerLocalId;
            public string OwnerGlobalId = "";
            public string ColumnName = "";
            public string Kind = "";
            public string RelativePath = "";
            public string OriginalName = "";
            public string ContentHash = "";
            public long SizeBytes;
            public int FileVersion = 1;
            public string Status = "Active";
        }

        public static void EnsureSchema()
        {
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                Exec(con, @"
CREATE TABLE IF NOT EXISTS TblFileAsset (
    FileAssetID     INTEGER PRIMARY KEY AUTOINCREMENT,
    FileGlobalID    TEXT NOT NULL UNIQUE,
    CaseGlobalID    TEXT NULL,
    CaseCode        TEXT NOT NULL,
    OwnerTable      TEXT NULL,
    OwnerLocalID    INTEGER NULL,
    OwnerGlobalID   TEXT NULL,
    ColumnName      TEXT NULL,
    Kind            TEXT NOT NULL,
    RelativePath    TEXT NOT NULL,
    OriginalName    TEXT NULL,
    ContentHash     TEXT NULL,
    SizeBytes       INTEGER NOT NULL DEFAULT 0,
    FileVersion     INTEGER NOT NULL DEFAULT 1,
    Status          TEXT NOT NULL DEFAULT 'Active',
    CreatedAt       TEXT NOT NULL DEFAULT (datetime('now')),
    UpdatedAt       TEXT NULL,
    DeletedAt       TEXT NULL
);");
                Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_TblFileAsset_Path ON TblFileAsset(RelativePath);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_TblFileAsset_Case ON TblFileAsset(CaseCode, Status);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_TblFileAsset_Owner ON TblFileAsset(OwnerTable, OwnerGlobalID, ColumnName);");
                Exec(con, @"
CREATE TABLE IF NOT EXISTS FileMigrationJournal (
    JournalID       INTEGER PRIMARY KEY AUTOINCREMENT,
    FileGlobalID    TEXT NULL,
    CaseCode        TEXT NULL,
    OwnerTable      TEXT NULL,
    OwnerLocalID    INTEGER NULL,
    ColumnName      TEXT NULL,
    SourcePath      TEXT NOT NULL,
    DestinationPath TEXT NULL,
    ContentHash     TEXT NULL,
    SizeBytes       INTEGER NOT NULL DEFAULT 0,
    State           TEXT NOT NULL,
    ErrorMessage    TEXT NULL,
    StartedAt       TEXT NOT NULL DEFAULT (datetime('now')),
    CompletedAt     TEXT NULL
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_FileMigrationJournal_State ON FileMigrationJournal(State, JournalID);");
                Exec(con, @"
CREATE TABLE IF NOT EXISTS FileDeletionQueue (
    QueueID        INTEGER PRIMARY KEY AUTOINCREMENT,
    CaseCode       TEXT NOT NULL,
    FullPath       TEXT NOT NULL,
    ContentHash    TEXT NULL,
    State          TEXT NOT NULL DEFAULT 'Pending',
    Attempts       INTEGER NOT NULL DEFAULT 0,
    LastError      TEXT NULL,
    CreatedAt      TEXT NOT NULL DEFAULT (datetime('now')),
    CompletedAt    TEXT NULL
);");
                Exec(con, "CREATE INDEX IF NOT EXISTS IX_FileDeletionQueue_State ON FileDeletionQueue(State, QueueID);");
            }
        }

        public Asset Register(string caseCode, string kind, string fullPath,
            string ownerTable, int ownerLocalId, string ownerGlobalId,
            string columnName, string originalName)
        {
            return Register(caseCode, kind, fullPath, ownerTable, ownerLocalId,
                ownerGlobalId, columnName, originalName, FileHelper.GetBaseRootFolder());
        }

        public Asset Register(string caseCode, string kind, string fullPath,
            string ownerTable, int ownerLocalId, string ownerGlobalId,
            string columnName, string originalName, string relativeRoot)
        {
            return Register(caseCode, kind, fullPath, ownerTable, ownerLocalId,
                ownerGlobalId, columnName, originalName, relativeRoot, "");
        }

        public Asset Register(string caseCode, string kind, string fullPath,
            string ownerTable, int ownerLocalId, string ownerGlobalId,
            string columnName, string originalName, string relativeRoot,
            string preferredFileGlobalId)
        {
            EnsureSchema();
            string relative = RelativeToRoot(fullPath, relativeRoot);
            if (string.IsNullOrWhiteSpace(relative))
                throw new InvalidOperationException("فایل catalog باید داخل Root ذخیره‌سازی باشد.");

            string hash = File.Exists(fullPath) ? CaseFileInventory.ComputeHash(fullPath) : "";
            long size = 0;
            try { if (File.Exists(fullPath)) size = new FileInfo(fullPath).Length; } catch { }
            string caseGlobalId = ResolveCaseGlobalId(caseCode);

            DataTable existing = _db.Query(
                "SELECT * FROM TblFileAsset WHERE RelativePath=@p LIMIT 1;",
                new SQLiteParameter("@p", relative));

            Asset asset = new Asset
            {
                FileGlobalId = existing.Rows.Count == 0
                    ? (string.IsNullOrWhiteSpace(preferredFileGlobalId)
                        ? Guid.NewGuid().ToString("D").ToLowerInvariant()
                        : preferredFileGlobalId.Trim().ToLowerInvariant())
                    : Convert.ToString(existing.Rows[0]["FileGlobalID"]),
                CaseGlobalId = caseGlobalId,
                CaseCode = caseCode ?? "",
                OwnerTable = ownerTable ?? "",
                OwnerLocalId = ownerLocalId,
                OwnerGlobalId = ownerGlobalId ?? "",
                ColumnName = columnName ?? "",
                Kind = kind ?? FileKinds.Other,
                RelativePath = relative,
                OriginalName = string.IsNullOrWhiteSpace(originalName) ? Path.GetFileName(fullPath) : originalName,
                ContentHash = hash,
                SizeBytes = size,
                FileVersion = existing.Rows.Count == 0 ? 1 :
                    Math.Max(1, Convert.ToInt32(existing.Rows[0]["FileVersion"])),
                Status = File.Exists(fullPath) ? "Active" : "Missing"
            };

            if (existing.Rows.Count == 0)
            {
                _db.ExecuteNonQuery(@"
INSERT INTO TblFileAsset
(FileGlobalID,CaseGlobalID,CaseCode,OwnerTable,OwnerLocalID,OwnerGlobalID,ColumnName,
 Kind,RelativePath,OriginalName,ContentHash,SizeBytes,FileVersion,Status)
VALUES (@fg,@cg,@cc,@ot,@oi,@og,@col,@k,@rp,@n,@h,@s,@v,@st);", Parameters(asset));
            }
            else
            {
                string previousHash = Convert.ToString(existing.Rows[0]["ContentHash"]);
                if (!string.IsNullOrWhiteSpace(previousHash) &&
                    !string.Equals(previousHash, hash, StringComparison.OrdinalIgnoreCase))
                    asset.FileVersion++;

                SQLiteParameter[] values = Parameters(asset);
                var all = new List<SQLiteParameter>(values);
                all.Add(new SQLiteParameter("@fgWhere", asset.FileGlobalId));
                _db.ExecuteNonQuery(@"
UPDATE TblFileAsset SET CaseGlobalID=@cg,CaseCode=@cc,OwnerTable=@ot,OwnerLocalID=@oi,
 OwnerGlobalID=@og,ColumnName=@col,Kind=@k,RelativePath=@rp,OriginalName=@n,
 ContentHash=@h,SizeBytes=@s,FileVersion=@v,Status=@st,UpdatedAt=datetime('now'),
 DeletedAt=NULL WHERE FileGlobalID=@fgWhere;", all.ToArray());
            }

            return asset;
        }

        private static string RelativeToRoot(string fullPath, string root)
        {
            try
            {
                string normalizedRoot = Path.GetFullPath(root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(fullPath);
                if (!full.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return "";
                return full.Substring(normalizedRoot.Length);
            }
            catch { return ""; }
        }

        public void RegisterInventory(CaseFileInventory.CaseContext inventory)
        {
            if (inventory == null) return;
            foreach (CaseFileInventory.FileEntry file in inventory.Files)
            {
                if (!file.Exists || string.IsNullOrWhiteSpace(file.RelativePath)) continue;
                Register(inventory.CaseCode, file.Kind, file.FullPath, file.OwnerTable,
                    file.OwnerId, file.OwnerGlobalId, file.ColumnName,
                    Path.GetFileName(file.StoredPath));
            }
        }

        public DataTable GetActiveForCase(string caseCode)
        {
            EnsureSchema();
            return _db.Query("SELECT * FROM TblFileAsset WHERE CaseCode=@c AND Status<>'Deleted' ORDER BY FileAssetID;",
                new SQLiteParameter("@c", caseCode ?? ""));
        }

        public DataTable GetAllActive()
        {
            EnsureSchema();
            return _db.Query("SELECT * FROM TblFileAsset WHERE Status<>'Deleted' ORDER BY FileAssetID;");
        }

        public int ReconcileUnownedAssets(int limit)
        {
            EnsureSchema();
            DataTable assets = _db.Query(@"
SELECT FileGlobalID,RelativePath,Kind FROM TblFileAsset
WHERE Status<>'Deleted' AND NULLIF(OwnerTable,'') IS NULL
  AND Kind NOT IN ('CaseFile','Export','Other')
ORDER BY FileAssetID LIMIT @n;", new SQLiteParameter("@n", Math.Max(1, limit)));
            int linked = 0;
            foreach (DataRow asset in assets.Rows)
            {
                string full = CaseFileInventory.ResolveStoredPath(
                    Convert.ToString(asset["RelativePath"]));
                OwnerMatch match = FindOwner(full);
                if (match == null) continue;
                _db.ExecuteNonQuery(@"
UPDATE TblFileAsset SET OwnerTable=@t,OwnerLocalID=@i,OwnerGlobalID=@g,
 ColumnName=@c,UpdatedAt=datetime('now') WHERE FileGlobalID=@f;",
                    new SQLiteParameter("@t", match.Table),
                    new SQLiteParameter("@i", match.LocalId),
                    new SQLiteParameter("@g", match.GlobalId),
                    new SQLiteParameter("@c", match.Column),
                    new SQLiteParameter("@f", Convert.ToString(asset["FileGlobalID"])));
                linked++;
            }
            return linked;
        }

        private sealed class OwnerMatch
        {
            public string Table;
            public int LocalId;
            public string GlobalId;
            public string Column;
        }

        private OwnerMatch FindOwner(string fullPath)
        {
            string[][] sources =
            {
                new[] { "TblCase", "CasID", "PhotoPath" },
                new[] { "TblCase", "CasID", "FamilyPhotoPath" },
                new[] { "TblFamily", "FamID", "MemberPhotoPath" },
                new[] { "TblOrphan", "OrphanID", "GuardianPhotoPath" },
                new[] { "TblCaseRepresentative", "RepresentativeID", "PhotoPath" },
                new[] { "TblFieldVisitPhoto", "PhotoID", "FilePath" },
                new[] { "TblDocs", "DocID", "DocFilePath" }
            };
            foreach (string[] source in sources)
            {
                try
                {
                    DataTable rows = _db.Query(
                        "SELECT [" + source[1] + "] AS LocalID,IFNULL(GlobalID,'') AS GlobalID " +
                        "FROM [" + source[0] + "] WHERE [" + source[2] + "]=@p LIMIT 1;",
                        new SQLiteParameter("@p", fullPath));
                    if (rows.Rows.Count == 0) continue;
                    return new OwnerMatch
                    {
                        Table = source[0],
                        LocalId = Convert.ToInt32(rows.Rows[0]["LocalID"]),
                        GlobalId = Convert.ToString(rows.Rows[0]["GlobalID"]),
                        Column = source[2]
                    };
                }
                catch { }
            }
            return null;
        }

        public void MarkCaseDeleted(string caseCode)
        {
            EnsureSchema();
            _db.ExecuteNonQuery(@"UPDATE TblFileAsset SET Status='Deleted',
                DeletedAt=datetime('now'),UpdatedAt=datetime('now')
                WHERE CaseCode=@c AND Status<>'Deleted';",
                new SQLiteParameter("@c", caseCode ?? ""));
        }

        public void MarkPathDeleted(string fullPath)
        {
            EnsureSchema();
            string relative = RelativeToRoot(fullPath, FileHelper.GetBaseRootFolder());
            if (string.IsNullOrWhiteSpace(relative)) return;
            _db.ExecuteNonQuery(@"UPDATE TblFileAsset SET Status='Deleted',
                DeletedAt=datetime('now'),UpdatedAt=datetime('now')
                WHERE RelativePath=@p AND Status<>'Deleted';",
                new SQLiteParameter("@p", relative));
        }

        public static string ResolveAssetPath(DataRow row)
        {
            if (row == null || !row.Table.Columns.Contains("RelativePath")) return "";
            return CaseFileInventory.ResolveStoredPath(Convert.ToString(row["RelativePath"]));
        }

        private string ResolveCaseGlobalId(string caseCode)
        {
            try
            {
                object value = _db.ExecuteScalar("SELECT GlobalID FROM TblCase WHERE Code=@c LIMIT 1;",
                    new SQLiteParameter("@c", caseCode ?? ""));
                return value == null || value == DBNull.Value ? "" : Convert.ToString(value);
            }
            catch { return ""; }
        }

        private static SQLiteParameter[] Parameters(Asset asset)
        {
            return new[]
            {
                new SQLiteParameter("@fg", asset.FileGlobalId),
                new SQLiteParameter("@cg", (object)asset.CaseGlobalId ?? DBNull.Value),
                new SQLiteParameter("@cc", asset.CaseCode),
                new SQLiteParameter("@ot", (object)asset.OwnerTable ?? DBNull.Value),
                new SQLiteParameter("@oi", asset.OwnerLocalId),
                new SQLiteParameter("@og", (object)asset.OwnerGlobalId ?? DBNull.Value),
                new SQLiteParameter("@col", (object)asset.ColumnName ?? DBNull.Value),
                new SQLiteParameter("@k", asset.Kind),
                new SQLiteParameter("@rp", asset.RelativePath),
                new SQLiteParameter("@n", (object)asset.OriginalName ?? DBNull.Value),
                new SQLiteParameter("@h", (object)asset.ContentHash ?? DBNull.Value),
                new SQLiteParameter("@s", asset.SizeBytes),
                new SQLiteParameter("@v", asset.FileVersion),
                new SQLiteParameter("@st", asset.Status)
            };
        }

        private static void Exec(SQLiteConnection con, string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con)) cmd.ExecuteNonQuery();
        }
    }
}

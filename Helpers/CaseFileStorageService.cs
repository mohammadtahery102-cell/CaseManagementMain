using System;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace CaseManagement.Helpers
{
    /// <summary>
    /// Canonical write facade. Existing forms can continue through FileHelper;
    /// new/updated call sites use this overload to preserve owner metadata.
    /// </summary>
    public sealed class CaseFileStorageService
    {
        private readonly FileCatalogService _catalog;

        public CaseFileStorageService() : this(new FileCatalogService()) { }
        public CaseFileStorageService(FileCatalogService catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException("catalog");
        }

        public string Save(string sourcePath, string caseCode, string kind, string context,
            string existingStoredPath, string ownerTable, int ownerLocalId,
            string ownerGlobalId, string columnName)
        {
            string section = FileKindPolicy.SectionForKind(kind);
            string path = FileHelper.SaveFileToCaseFolder(sourcePath, caseCode, section,
                string.IsNullOrWhiteSpace(context) ? caseCode : context,
                existingStoredPath ?? "");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return existingStoredPath ?? "";

            _catalog.Register(caseCode, kind, path, ownerTable, ownerLocalId,
                ownerGlobalId, columnName, Path.GetFileName(sourcePath));
            return path;
        }

        public string SaveGenerated(string sourcePath, string caseCode, string context,
            bool explicitExport)
        {
            return Save(sourcePath, caseCode,
                explicitExport ? FileKinds.Export : FileKinds.CaseFile,
                context, "", "", 0, "", "");
        }

        public string Resolve(string storedPath)
        {
            return CaseFileInventory.ResolveStoredPath(storedPath);
        }

        public sealed class RestoreValidationResult
        {
            public int Present;
            public int Relinked;
            public int Missing;
        }

        public RestoreValidationResult ValidateAndRelink(int caseId)
        {
            var result = new RestoreValidationResult();
            CaseFileInventory.CaseContext inventory =
                new CaseFileInventory().ScanCase(caseId, false, false);
            if (inventory == null) return result;
            var db = new CaseManagement.DAL.DatabaseHelper();

            foreach (CaseFileInventory.FileEntry file in inventory.Files)
            {
                if (!file.Referenced) continue;
                if (file.Exists) { result.Present++; continue; }

                DataTable candidates = db.Query(@"
SELECT RelativePath FROM TblFileAsset
WHERE CaseCode=@c AND OwnerTable=@t AND OwnerGlobalID=@g
  AND ColumnName=@col AND Status<>'Deleted'
ORDER BY FileAssetID DESC;",
                    new SQLiteParameter("@c", inventory.CaseCode),
                    new SQLiteParameter("@t", file.OwnerTable ?? ""),
                    new SQLiteParameter("@g", file.OwnerGlobalId ?? ""),
                    new SQLiteParameter("@col", file.ColumnName ?? ""));

                string replacement = "";
                foreach (DataRow row in candidates.Rows)
                {
                    string full = CaseFileInventory.ResolveStoredPath(
                        Convert.ToString(row["RelativePath"]));
                    if (File.Exists(full)) { replacement = full; break; }
                }

                string key = PrimaryKey(file.OwnerTable);
                if (!string.IsNullOrWhiteSpace(replacement) && !string.IsNullOrWhiteSpace(key))
                {
                    db.ExecuteNonQuery("UPDATE [" + file.OwnerTable + "] SET [" +
                        file.ColumnName + "]=@p WHERE [" + key + "]=@id;",
                        new SQLiteParameter("@p", replacement),
                        new SQLiteParameter("@id", file.OwnerId));
                    result.Relinked++;
                }
                else result.Missing++;
            }
            return result;
        }

        private static string PrimaryKey(string table)
        {
            if (string.Equals(table, "TblCase", StringComparison.OrdinalIgnoreCase)) return "CasID";
            if (string.Equals(table, "TblFamily", StringComparison.OrdinalIgnoreCase)) return "FamID";
            if (string.Equals(table, "TblOrphan", StringComparison.OrdinalIgnoreCase)) return "OrphanID";
            if (string.Equals(table, "TblCaseRepresentative", StringComparison.OrdinalIgnoreCase)) return "RepresentativeID";
            if (string.Equals(table, "TblFieldVisitPhoto", StringComparison.OrdinalIgnoreCase)) return "PhotoID";
            if (string.Equals(table, "TblDocs", StringComparison.OrdinalIgnoreCase)) return "DocID";
            return "";
        }
    }
}

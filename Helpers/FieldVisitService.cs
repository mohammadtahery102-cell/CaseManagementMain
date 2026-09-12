using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    public class FieldVisitRow
    {
        public int      VisitID;
        public int      CasID;
        public string   VisitDate;
        public int?     VisitorUserID;
        public string   VisitorName;
        public string   VisitResult;
        public string   Recommendation;
        public string   Notes;
        public string   CreatedDate;
        public int      PhotoCount;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 5 — تنها نویسنده/خوانندهٔ TblFieldVisit و TblFieldVisitPhoto.
    //
    // برخلافِ ماژول‌های فاز ۴ (یک ردیف به‌ازای هر پرونده)، اینجا رابطه
    // یک-به-چند است: هر پرونده چند بازدید، هر بازدید چند عکس.
    //
    // تایم‌لاین و بازمحاسبهٔ کامل‌بودن داخلِ همین کلاس انجام می‌شوند تا هیچ
    // فراخوانی نتواند فراموششان کند — همان قاعدهٔ CaseModuleService.
    // ═══════════════════════════════════════════════════════════════════════
    public static class FieldVisitService
    {
        public const string TableVisit = "TblFieldVisit";
        public const string TablePhoto = "TblFieldVisitPhoto";

        // ─── خواندن ──────────────────────────────────────────────────────────
        public static List<FieldVisitRow> GetVisits(int casId)
        {
            var result = new List<FieldVisitRow>();
            if (casId <= 0) return result;

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT v.VisitID, v.CasID, v.VisitDate, v.VisitorUserID, v.VisitorName,
       v.VisitResult, v.Recommendation, v.Notes, v.CreatedDate,
       (SELECT COUNT(*) FROM TblFieldVisitPhoto p WHERE p.VisitID = v.VisitID) AS PhotoCount
FROM TblFieldVisit v
WHERE v.CasID = @CasID
ORDER BY v.VisitDate DESC, v.VisitID DESC;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        result.Add(new FieldVisitRow
                        {
                            VisitID = Convert.ToInt32(dr["VisitID"]),
                            CasID = Convert.ToInt32(dr["CasID"]),
                            VisitDate = Str(dr["VisitDate"]),
                            VisitorUserID = dr["VisitorUserID"] == DBNull.Value ? (int?)null : Convert.ToInt32(dr["VisitorUserID"]),
                            VisitorName = Str(dr["VisitorName"]),
                            VisitResult = Str(dr["VisitResult"]),
                            Recommendation = Str(dr["Recommendation"]),
                            Notes = Str(dr["Notes"]),
                            CreatedDate = Str(dr["CreatedDate"]),
                            PhotoCount = Convert.ToInt32(dr["PhotoCount"])
                        });
                    }
                }
            }

            return result;
        }

        public static DataTable GetVisitsTable(int casId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT v.VisitID, v.VisitDate, v.VisitorName, v.VisitResult, v.Recommendation, v.Notes,
       (SELECT COUNT(*) FROM TblFieldVisitPhoto p WHERE p.VisitID = v.VisitID) AS PhotoCount
FROM TblFieldVisit v
WHERE v.CasID = @CasID
ORDER BY v.VisitDate DESC, v.VisitID DESC;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                con.Open();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public static FieldVisitRow GetVisit(int visitId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT * FROM TblFieldVisit WHERE VisitID = @Id LIMIT 1;", con))
            {
                cmd.Parameters.AddWithValue("@Id", visitId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) return null;
                    return new FieldVisitRow
                    {
                        VisitID = Convert.ToInt32(dr["VisitID"]),
                        CasID = Convert.ToInt32(dr["CasID"]),
                        VisitDate = Str(dr["VisitDate"]),
                        VisitorUserID = dr["VisitorUserID"] == DBNull.Value ? (int?)null : Convert.ToInt32(dr["VisitorUserID"]),
                        VisitorName = Str(dr["VisitorName"]),
                        VisitResult = Str(dr["VisitResult"]),
                        Recommendation = Str(dr["Recommendation"]),
                        Notes = Str(dr["Notes"]),
                        CreatedDate = Str(dr["CreatedDate"])
                    };
                }
            }
        }

        public static int GetVisitCount(int casId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM TblFieldVisit WHERE CasID = @CasID;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ─── نوشتن ───────────────────────────────────────────────────────────
        // visitId == 0 ⇒ ثبت جدید؛ در غیر این صورت ویرایش.
        public static int SaveVisit(int visitId, int casId, string visitDate, string visitorName,
            string visitResult, string recommendation, string notes)
        {
            if (casId <= 0) return 0;
            bool isNew = visitId <= 0;

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                if (isNew)
                {
                    using (var cmd = new SQLiteCommand(@"
INSERT INTO TblFieldVisit
    (CasID, VisitDate, VisitorUserID, VisitorName, VisitResult, Recommendation, Notes,
     CenterID, CreatedBy, GlobalID)
VALUES
    (@CasID, @VisitDate, @VisitorUserID, @VisitorName, @VisitResult, @Recommendation, @Notes,
     @CenterID, @CreatedBy,
     lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
     lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))));", con))
                    {
                        BindVisit(cmd, casId, visitDate, visitorName, visitResult, recommendation, notes);
                        cmd.ExecuteNonQuery();
                    }

                    using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                        visitId = Convert.ToInt32((long)idCmd.ExecuteScalar());
                }
                else
                {
                    using (var cmd = new SQLiteCommand(@"
UPDATE TblFieldVisit SET
    VisitDate = @VisitDate, VisitorName = @VisitorName, VisitResult = @VisitResult,
    Recommendation = @Recommendation, Notes = @Notes, UpdatedAt = datetime('now')
WHERE VisitID = @VisitID;", con))
                    {
                        BindVisit(cmd, casId, visitDate, visitorName, visitResult, recommendation, notes);
                        cmd.Parameters.AddWithValue("@VisitID", visitId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            if (isNew)
                TimelineService.LogFieldVisitCreated(casId, visitId, visitDate);
            else
                TimelineService.LogFieldVisitUpdated(casId, visitId, visitDate);

            SyncCapture(TableVisit, visitId, isNew);
            CaseCompletionService.RecalculateAndStore(casId);

            return visitId;
        }

        private static void BindVisit(SQLiteCommand cmd, int casId, string visitDate, string visitorName,
            string visitResult, string recommendation, string notes)
        {
            cmd.Parameters.AddWithValue("@CasID", casId);
            cmd.Parameters.AddWithValue("@VisitDate", visitDate ?? DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@VisitorUserID",
                SecurityContext.IsLoggedIn ? (object)SecurityContext.UserId : DBNull.Value);
            cmd.Parameters.AddWithValue("@VisitorName", NullIfEmpty(visitorName));
            cmd.Parameters.AddWithValue("@VisitResult", NullIfEmpty(visitResult));
            cmd.Parameters.AddWithValue("@Recommendation", NullIfEmpty(recommendation));
            cmd.Parameters.AddWithValue("@Notes", NullIfEmpty(notes));
            cmd.Parameters.AddWithValue("@CenterID",
                SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", (object)SecurityContext.Username ?? DBNull.Value);
        }

        // حذفِ بازدید. عکس‌ها با CASCADE در دیتابیس حذف می‌شوند؛ فایل‌های
        // روی دیسک عمداً دست‌نخورده می‌مانند (همان محافظه‌کاریِ FrmDocs:
        // حذفِ رکورد نباید فایلِ کاربر را بی‌بازگشت پاک کند).
        public static bool DeleteVisit(int visitId)
        {
            FieldVisitRow visit = GetVisit(visitId);
            if (visit == null) return false;

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM TblFieldVisit WHERE VisitID = @Id;", con))
            {
                cmd.Parameters.AddWithValue("@Id", visitId);
                con.Open();
                cmd.ExecuteNonQuery();
            }

            TimelineService.LogFieldVisitDeleted(visit.CasID, visitId, visit.VisitDate);
            CaseCompletionService.RecalculateAndStore(visit.CasID);
            return true;
        }

        // ─── عکس‌های بازدید ──────────────────────────────────────────────────
        public static DataTable GetPhotos(int visitId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT PhotoID, FilePath, Description, CreatedDate FROM TblFieldVisitPhoto WHERE VisitID = @Id ORDER BY PhotoID;", con))
            {
                cmd.Parameters.AddWithValue("@Id", visitId);
                con.Open();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public static int AddPhoto(int visitId, int casId, string filePath, string description)
        {
            if (visitId <= 0 || casId <= 0 || string.IsNullOrWhiteSpace(filePath)) return 0;

            int photoId;
            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                using (var cmd = new SQLiteCommand(@"
INSERT INTO TblFieldVisitPhoto (VisitID, CasID, FilePath, Description, CenterID, CreatedBy, GlobalID)
VALUES (@VisitID, @CasID, @FilePath, @Description, @CenterID, @CreatedBy,
     lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
     lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))));", con))
                {
                    cmd.Parameters.AddWithValue("@VisitID", visitId);
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    cmd.Parameters.AddWithValue("@FilePath", filePath);
                    cmd.Parameters.AddWithValue("@Description", NullIfEmpty(description));
                    cmd.Parameters.AddWithValue("@CenterID",
                        SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedBy", (object)SecurityContext.Username ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }

                using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                    photoId = Convert.ToInt32((long)idCmd.ExecuteScalar());
            }

            SyncCapture(TablePhoto, photoId, true);
            return photoId;
        }

        public static bool DeletePhoto(int photoId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM TblFieldVisitPhoto WHERE PhotoID = @Id;", con))
            {
                cmd.Parameters.AddWithValue("@Id", photoId);
                con.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private static string Str(object value)
        {
            return value == DBNull.Value || value == null ? "" : value.ToString();
        }

        private static object NullIfEmpty(string value)
        {
            string trimmed = (value ?? "").Trim();
            return trimmed.Length == 0 ? (object)DBNull.Value : trimmed;
        }

        private static void SyncCapture(string table, int id, bool isNew)
        {
            try
            {
                CaseManagement.Sync.SyncOutboxService.Capture(table, id,
                    isNew
                        ? CaseManagement.Sync.OfflineSyncInitializer.OperationCreate
                        : CaseManagement.Sync.OfflineSyncInitializer.OperationUpdate);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("FieldVisitService.SyncCapture failed: " + ex.Message);
            }
        }
    }
}

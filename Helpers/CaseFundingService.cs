using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // Phase 5 — پیوندِ پرونده با منبعِ تأمینِ مالی و خیّر.
    //
    // TblFundingSource و TblSponsor «دفترچه»اند: هرگز حذف نمی‌شوند، فقط
    // IsActive=0 می‌گیرند (همان قاعدهٔ دادهٔ مرجع در سند معماری). TblCaseFunding
    // پیوندِ پرونده‌محور است و با پرونده CASCADE حذف می‌شود.
    // ═══════════════════════════════════════════════════════════════════════
    public static class CaseFundingService
    {
        public const string TableCaseFunding = "TblCaseFunding";

        // ─── دفترچهٔ منابع مالی ──────────────────────────────────────────────
        public static List<ReferenceOption> GetFundingSources(bool activeOnly = true)
        {
            var list = new List<ReferenceOption>();
            string sql = "SELECT FundingSourceID AS ID, Code, Name FROM TblFundingSource" +
                         (activeOnly ? " WHERE IsActive = 1" : "") + " ORDER BY SortOrder, Name;";

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        list.Add(new ReferenceOption
                        {
                            ID = Convert.ToInt32(dr["ID"]),
                            Code = dr["Code"].ToString(),
                            Name = dr["Name"].ToString()
                        });
                    }
                }
            }

            return list;
        }

        // ─── دفترچهٔ خیّرین ──────────────────────────────────────────────────
        public static List<ReferenceOption> GetSponsors(bool activeOnly = true)
        {
            var list = new List<ReferenceOption>();
            string sql = "SELECT SponsorID AS ID, Name FROM TblSponsor" +
                         (activeOnly ? " WHERE IsActive = 1" : "") + " ORDER BY Name;";

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        list.Add(new ReferenceOption
                        {
                            ID = Convert.ToInt32(dr["ID"]),
                            Code = "",
                            Name = dr["Name"].ToString()
                        });
                    }
                }
            }

            return list;
        }

        public static int SaveSponsor(int sponsorId, string name, string phone, string email,
            string address, string notes, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            bool isNew = sponsorId <= 0;

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                if (isNew)
                {
                    using (var cmd = new SQLiteCommand(@"
INSERT INTO TblSponsor (Name, Phone, Email, Address, Notes, IsActive, CenterID, CreatedBy, GlobalID)
VALUES (@Name, @Phone, @Email, @Address, @Notes, @IsActive, @CenterID, @CreatedBy,
     lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
     lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))));", con))
                    {
                        BindSponsor(cmd, name, phone, email, address, notes, isActive);
                        cmd.ExecuteNonQuery();
                    }

                    using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                        sponsorId = Convert.ToInt32((long)idCmd.ExecuteScalar());
                }
                else
                {
                    using (var cmd = new SQLiteCommand(@"
UPDATE TblSponsor SET Name = @Name, Phone = @Phone, Email = @Email, Address = @Address,
    Notes = @Notes, IsActive = @IsActive, UpdatedAt = datetime('now')
WHERE SponsorID = @SponsorID;", con))
                    {
                        BindSponsor(cmd, name, phone, email, address, notes, isActive);
                        cmd.Parameters.AddWithValue("@SponsorID", sponsorId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            SyncCapture("TblSponsor", sponsorId, isNew);
            return sponsorId;
        }

        private static void BindSponsor(SQLiteCommand cmd, string name, string phone, string email,
            string address, string notes, bool isActive)
        {
            cmd.Parameters.AddWithValue("@Name", name.Trim());
            cmd.Parameters.AddWithValue("@Phone", NullIfEmpty(phone));
            cmd.Parameters.AddWithValue("@Email", NullIfEmpty(email));
            cmd.Parameters.AddWithValue("@Address", NullIfEmpty(address));
            cmd.Parameters.AddWithValue("@Notes", NullIfEmpty(notes));
            cmd.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@CenterID",
                SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", (object)SecurityContext.Username ?? DBNull.Value);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 5.5-C — مدیریتِ دفترچه‌ها (صفحاتِ اداری).
        //
        // «غیرفعال‌سازی» و نه «حذف»: هر دو جدول از جدولِ پیوند (TblCaseFunding)
        // با FKِ RESTRICT ارجاع می‌شوند، پس حذفِ منبعی که روی پرونده‌ای نشسته
        // یا خطا می‌دهد یا تاریخچه را می‌شکند. IsActive=0 هم فهرست‌های انتخاب
        // را تمیز نگه می‌دارد و هم گذشته را دست‌نخورده.
        // ═══════════════════════════════════════════════════════════════════

        // جدولِ کاملِ منابع برای گریدِ مدیریت (شاملِ غیرفعال‌ها + جستجو).
        public static DataTable GetFundingSourceTable(string search, bool includeInactive)
        {
            string sql = @"
SELECT fs.FundingSourceID, fs.Code, fs.Name, IFNULL(fs.Description,'') AS Description,
       CASE WHEN fs.IsActive = 1 THEN 'فعال' ELSE 'غیرفعال' END AS StatusText,
       fs.IsActive,
       (SELECT COUNT(DISTINCT cf.CasID) FROM TblCaseFunding cf
         WHERE cf.FundingSourceID = fs.FundingSourceID AND cf.IsActive = 1) AS UsageCount
FROM TblFundingSource fs
WHERE (@Inactive = 1 OR fs.IsActive = 1)
  AND (@Term = '' OR fs.Name LIKE @Like OR fs.Code LIKE @Like OR IFNULL(fs.Description,'') LIKE @Like)
ORDER BY fs.IsActive DESC, fs.SortOrder, fs.Name;";

            return QuerySearch(sql, search, includeInactive);
        }

        public static DataTable GetSponsorTable(string search, bool includeInactive)
        {
            string sql = @"
SELECT s.SponsorID, s.Name, IFNULL(s.Phone,'') AS Phone, IFNULL(s.Email,'') AS Email,
       IFNULL(s.Address,'') AS Address, IFNULL(s.Notes,'') AS Notes,
       CASE WHEN s.IsActive = 1 THEN 'فعال' ELSE 'غیرفعال' END AS StatusText,
       s.IsActive,
       (SELECT COUNT(DISTINCT cf.CasID) FROM TblCaseFunding cf
         WHERE cf.SponsorID = s.SponsorID AND cf.IsActive = 1) AS UsageCount
FROM TblSponsor s
WHERE (@Inactive = 1 OR s.IsActive = 1)
  AND (@Term = '' OR s.Name LIKE @Like OR IFNULL(s.Phone,'') LIKE @Like OR IFNULL(s.Email,'') LIKE @Like)
ORDER BY s.IsActive DESC, s.Name;";

            return QuerySearch(sql, search, includeInactive);
        }

        private static DataTable QuerySearch(string sql, string search, bool includeInactive)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                string term = (search ?? "").Trim();
                cmd.Parameters.AddWithValue("@Term", term);
                cmd.Parameters.AddWithValue("@Like", "%" + term + "%");
                cmd.Parameters.AddWithValue("@Inactive", includeInactive ? 1 : 0);
                con.Open();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        // ثبت/ویرایشِ منبع تأمین مالی. Code یکتاست و پس از ساخت تغییر
        // نمی‌کند — چون قواعد/گزارش‌های آینده ممکن است به آن ارجاع دهند.
        public static int SaveFundingSource(int fundingSourceId, string code, string name,
            string description, bool isActive, out string error)
        {
            error = "";

            if (string.IsNullOrWhiteSpace(name)) { error = "نام منبع الزامی است."; return 0; }
            if (fundingSourceId <= 0 && string.IsNullOrWhiteSpace(code))
            {
                error = "کد منبع الزامی است.";
                return 0;
            }

            bool isNew = fundingSourceId <= 0;

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                {
                    con.Open();

                    if (isNew)
                    {
                        using (var cmd = new SQLiteCommand(
                            "SELECT COUNT(*) FROM TblFundingSource WHERE Code = @Code;", con))
                        {
                            cmd.Parameters.AddWithValue("@Code", code.Trim());
                            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                            {
                                error = "این کد قبلاً استفاده شده است.";
                                return 0;
                            }
                        }

                        using (var cmd = new SQLiteCommand(@"
INSERT INTO TblFundingSource (Code, Name, Description, IsActive)
VALUES (@Code, @Name, @Desc, @Active);", con))
                        {
                            cmd.Parameters.AddWithValue("@Code", code.Trim());
                            cmd.Parameters.AddWithValue("@Name", name.Trim());
                            cmd.Parameters.AddWithValue("@Desc", NullIfEmpty(description));
                            cmd.Parameters.AddWithValue("@Active", isActive ? 1 : 0);
                            cmd.ExecuteNonQuery();
                        }

                        using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                            fundingSourceId = Convert.ToInt32((long)idCmd.ExecuteScalar());
                    }
                    else
                    {
                        // Code عمداً به‌روزرسانی نمی‌شود.
                        using (var cmd = new SQLiteCommand(@"
UPDATE TblFundingSource SET Name = @Name, Description = @Desc, IsActive = @Active
WHERE FundingSourceID = @Id;", con))
                        {
                            cmd.Parameters.AddWithValue("@Name", name.Trim());
                            cmd.Parameters.AddWithValue("@Desc", NullIfEmpty(description));
                            cmd.Parameters.AddWithValue("@Active", isActive ? 1 : 0);
                            cmd.Parameters.AddWithValue("@Id", fundingSourceId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                error = "خطا در ذخیره منبع تأمین مالی: " + ex.Message;
                return 0;
            }

            return fundingSourceId;
        }

        public static bool SetFundingSourceActive(int fundingSourceId, bool isActive)
        {
            return SetActiveFlag("TblFundingSource", "FundingSourceID", fundingSourceId, isActive);
        }

        public static bool SetSponsorActive(int sponsorId, bool isActive)
        {
            return SetActiveFlag("TblSponsor", "SponsorID", sponsorId, isActive);
        }

        private static bool SetActiveFlag(string table, string idColumn, int id, bool isActive)
        {
            if (id <= 0) return false;

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "UPDATE [" + table + "] SET IsActive = @Active WHERE " + idColumn + " = @Id;", con))
            {
                cmd.Parameters.AddWithValue("@Active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@Id", id);
                con.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // شمارشِ استفادهٔ فعال — پیش از غیرفعال‌سازی به کاربر هشدار داده شود.
        public static int GetFundingSourceUsage(int fundingSourceId)
        {
            return CountUsage("FundingSourceID", fundingSourceId);
        }

        public static int GetSponsorUsage(int sponsorId)
        {
            return CountUsage("SponsorID", sponsorId);
        }

        private static int CountUsage(string column, int id)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(DISTINCT CasID) FROM TblCaseFunding WHERE " + column + " = @Id AND IsActive = 1;", con))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ─── پیوندِ پرونده ↔ منبع/خیّر ───────────────────────────────────────
        public static DataTable GetCaseFunding(int casId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT cf.CaseFundingID, cf.FundingSourceID, cf.SponsorID, cf.StartDate, cf.EndDate,
       cf.Notes, cf.IsActive, fs.Name AS FundingSourceName, s.Name AS SponsorName
FROM TblCaseFunding cf
JOIN TblFundingSource fs ON fs.FundingSourceID = cf.FundingSourceID
LEFT JOIN TblSponsor s ON s.SponsorID = cf.SponsorID
WHERE cf.CasID = @CasID
ORDER BY cf.IsActive DESC, cf.CaseFundingID DESC;", con))
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

        public static int GetActiveFundingCount(int casId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM TblCaseFunding WHERE CasID = @CasID AND IsActive = 1;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // تخصیصِ منبع مالی (و اختیاراً خیّر) به یک پرونده.
        public static int AssignFunding(int casId, int fundingSourceId, int? sponsorId,
            string startDate, string endDate, string notes)
        {
            if (casId <= 0 || fundingSourceId <= 0) return 0;

            int caseFundingId;
            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                using (var cmd = new SQLiteCommand(@"
INSERT INTO TblCaseFunding (CasID, FundingSourceID, SponsorID, StartDate, EndDate, Notes,
                            CenterID, CreatedBy, GlobalID)
VALUES (@CasID, @FundingSourceID, @SponsorID, @StartDate, @EndDate, @Notes,
     @CenterID, @CreatedBy,
     lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
     lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))));", con))
                {
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    cmd.Parameters.AddWithValue("@FundingSourceID", fundingSourceId);
                    cmd.Parameters.AddWithValue("@SponsorID", sponsorId.HasValue ? (object)sponsorId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@StartDate", NullIfEmpty(startDate));
                    cmd.Parameters.AddWithValue("@EndDate", NullIfEmpty(endDate));
                    cmd.Parameters.AddWithValue("@Notes", NullIfEmpty(notes));
                    cmd.Parameters.AddWithValue("@CenterID",
                        SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedBy", (object)SecurityContext.Username ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }

                using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                    caseFundingId = Convert.ToInt32((long)idCmd.ExecuteScalar());
            }

            TimelineService.LogFundingSourceAssigned(casId, caseFundingId, LookupName("TblFundingSource", "FundingSourceID", fundingSourceId));
            if (sponsorId.HasValue)
                TimelineService.LogSponsorAssigned(casId, caseFundingId, LookupName("TblSponsor", "SponsorID", sponsorId.Value));

            SyncCapture(TableCaseFunding, caseFundingId, true);
            CaseCompletionService.RecalculateAndStore(casId);

            return caseFundingId;
        }

        // برداشتنِ تخصیص. پیش‌فرض «غیرفعال‌کردن» است نه حذفِ فیزیکی، تا
        // تاریخچهٔ تأمینِ مالی پرونده باقی بماند (همان منطقِ بایگانیِ اسناد).
        public static bool RemoveFunding(int caseFundingId, bool hardDelete = false)
        {
            int casId = 0;
            string sourceName = "";
            string sponsorName = "";
            int? sponsorId = null;

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                using (var cmd = new SQLiteCommand(@"
SELECT cf.CasID, cf.SponsorID, fs.Name AS FundingSourceName, s.Name AS SponsorName
FROM TblCaseFunding cf
JOIN TblFundingSource fs ON fs.FundingSourceID = cf.FundingSourceID
LEFT JOIN TblSponsor s ON s.SponsorID = cf.SponsorID
WHERE cf.CaseFundingID = @Id LIMIT 1;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", caseFundingId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return false;
                        casId = Convert.ToInt32(dr["CasID"]);
                        sourceName = dr["FundingSourceName"] == DBNull.Value ? "" : dr["FundingSourceName"].ToString();
                        sponsorName = dr["SponsorName"] == DBNull.Value ? "" : dr["SponsorName"].ToString();
                        if (dr["SponsorID"] != DBNull.Value) sponsorId = Convert.ToInt32(dr["SponsorID"]);
                    }
                }

                string sql = hardDelete
                    ? "DELETE FROM TblCaseFunding WHERE CaseFundingID = @Id;"
                    : "UPDATE TblCaseFunding SET IsActive = 0, UpdatedAt = datetime('now') WHERE CaseFundingID = @Id;";

                using (var cmd = new SQLiteCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", caseFundingId);
                    cmd.ExecuteNonQuery();
                }
            }

            TimelineService.LogFundingSourceRemoved(casId, caseFundingId, sourceName);
            if (sponsorId.HasValue)
                TimelineService.LogSponsorRemoved(casId, caseFundingId, sponsorName);

            CaseCompletionService.RecalculateAndStore(casId);
            return true;
        }

        private static string LookupName(string table, string idColumn, int id)
        {
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT Name FROM [" + table + "] WHERE " + idColumn + " = @Id LIMIT 1;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    con.Open();
                    object result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? "" : result.ToString();
                }
            }
            catch { return ""; }
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
                System.Diagnostics.Debug.WriteLine("CaseFundingService.SyncCapture failed: " + ex.Message);
            }
        }
    }
}

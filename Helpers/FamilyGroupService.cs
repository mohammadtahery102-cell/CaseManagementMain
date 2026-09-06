using CaseManagement.DAL;
using System;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // Phase 6 — گروه‌بندیِ خانوادگیِ پرونده‌ها.
    //
    // مدل (تصمیمِ صریحِ کاربر): «پروندهٔ فعلی = ریشهٔ خانوار».
    //   TblCase.FamilyGroupID = CasIDِ پروندهٔ ریشه
    //   NULL                  = پروندهٔ مستقل (خودش ریشهٔ خودش)
    //   FamilyGroupID == CasID = ریشهٔ یک خانوارِ چندپرونده‌ای
    //
    // هیچ جدولِ خانوادهٔ جداگانه‌ای وجود ندارد و نباید ساخته شود: اطلاعاتِ
    // مشترکِ خانوار (آدرس/تلفن/سرپرست) روی پروندهٔ ریشه ذخیره شده و از همان‌جا
    // خوانده می‌شود — «یک‌بار ذخیره، بارها استفاده» بدونِ ساختارِ موازیِ دوم.
    //
    // ⚠ اشتباه نشود با TblFamily که «اعضای خانواده» است (فرزندِ TblCase).
    // این کلاس دربارهٔ گروه‌بندیِ *پرونده‌ها* است، نه اعضا.
    // ═══════════════════════════════════════════════════════════════════════
    public static class FamilyGroupService
    {
        // شناسهٔ گروهِ یک پرونده. اگر NULL باشد، خودِ پرونده ریشهٔ خودش است،
        // پس CasID برگردانده می‌شود — فراخوان هرگز لازم نیست NULL را جدا
        // مدیریت کند.
        public static int GetFamilyGroupId(int casId)
        {
            if (casId <= 0) return 0;

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT FamilyGroupID FROM TblCase WHERE CasID = @CasID LIMIT 1;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                con.Open();
                object result = cmd.ExecuteScalar();
                if (result == null) return 0;                    // پرونده وجود ندارد
                if (result == DBNull.Value) return casId;        // مستقل ⇒ ریشهٔ خودش
                return Convert.ToInt32(result);
            }
        }

        public static bool IsRoot(int casId)
        {
            return GetFamilyGroupId(casId) == casId;
        }

        // پروندهٔ تازه ریشهٔ خانوارِ خودش می‌شود (خواستهٔ صریح: «هنگام ساختِ
        // پروندهٔ ریشه، گروه خانواده خودکار ساخته شود»). چون گروه چیزی جز
        // خودارجاعی نیست، این فقط یک UPDATE است — نه ردیفِ تازه‌ای در جایی.
        public static void EnsureRoot(int casId)
        {
            if (casId <= 0) return;

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "UPDATE TblCase SET FamilyGroupID = @CasID WHERE CasID = @CasID AND FamilyGroupID IS NULL;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // پیوندِ یک پرونده به خانوارِ پروندهٔ دیگر.
        // rootCasId می‌تواند خودش عضوِ خانوارِ دیگری باشد؛ در آن صورت ریشهٔ
        // واقعیِ آن خانوار استفاده می‌شود تا زنجیرهٔ چندسطحی ساخته نشود
        // (ساختار همیشه دو سطحی می‌ماند: ریشه + اعضا).
        public static bool LinkToFamily(int casId, int rootCasId, out string error)
        {
            error = "";

            if (casId <= 0 || rootCasId <= 0)
            {
                error = "پرونده نامعتبر است.";
                return false;
            }

            if (casId == rootCasId)
            {
                error = "پرونده نمی‌تواند به خودش پیوند بخورد.";
                return false;
            }

            int targetGroupId = GetFamilyGroupId(rootCasId);
            if (targetGroupId <= 0)
            {
                error = "پروندهٔ مقصد پیدا نشد.";
                return false;
            }

            // اگر خودِ این پرونده ریشهٔ خانوارِ دیگری با اعضاست، پیوندش
            // اعضایش را بی‌سرپرست می‌گذاشت. جلوگیری صریح، نه اصلاحِ خاموش.
            if (IsRoot(casId) && GetMemberCount(casId) > 1)
            {
                error = "این پرونده ریشهٔ یک خانوارِ چندپرونده‌ای است؛ اول اعضای آن را جدا کنید.";
                return false;
            }

            EnsureRoot(rootCasId);

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "UPDATE TblCase SET FamilyGroupID = @GroupID, UpdatedAt = datetime('now') WHERE CasID = @CasID;", con))
            {
                cmd.Parameters.AddWithValue("@GroupID", targetGroupId);
                cmd.Parameters.AddWithValue("@CasID", casId);
                con.Open();
                cmd.ExecuteNonQuery();
            }

            string rootCode = GetCaseCode(targetGroupId);
            string memberCode = GetCaseCode(casId);
            TimelineService.LogFamilyLinked(casId, targetGroupId, rootCode);
            TimelineService.LogFamilyLinked(targetGroupId, targetGroupId, memberCode);

            SyncCapture(casId);
            return true;
        }

        // جداکردنِ یک پرونده از خانوار ⇒ دوباره مستقل (ریشهٔ خودش).
        public static bool UnlinkFromFamily(int casId, out string error)
        {
            error = "";
            if (casId <= 0) { error = "پرونده نامعتبر است."; return false; }

            int currentGroupId = GetFamilyGroupId(casId);
            if (currentGroupId == casId)
            {
                error = "این پرونده عضو خانوارِ دیگری نیست.";
                return false;
            }

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "UPDATE TblCase SET FamilyGroupID = @CasID, UpdatedAt = datetime('now') WHERE CasID = @CasID;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                con.Open();
                cmd.ExecuteNonQuery();
            }

            string rootCode = GetCaseCode(currentGroupId);
            string memberCode = GetCaseCode(casId);
            TimelineService.LogFamilyUnlinked(casId, currentGroupId, rootCode);
            TimelineService.LogFamilyUnlinked(currentGroupId, currentGroupId, memberCode);

            SyncCapture(casId);
            return true;
        }

        // همهٔ پرونده‌های همان خانوار (شاملِ خودِ ریشه)، برای نمایش در فرم.
        public static DataTable GetFamilyCases(int casId)
        {
            int groupId = GetFamilyGroupId(casId);

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT c.CasID, c.Code, c.HeadFullName, c.ServiceStatus,
       IFNULL(rt.Name, c.RequestType) AS RequestTypeName,
       CASE WHEN c.CasID = @GroupID THEN 'ریشه خانوار' ELSE 'عضو' END AS FamilyRole
FROM TblCase c
LEFT JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID
WHERE IFNULL(c.FamilyGroupID, c.CasID) = @GroupID
  AND IFNULL(c.IsArchived, 0) = 0
ORDER BY CASE WHEN c.CasID = @GroupID THEN 0 ELSE 1 END, c.CasID;", con))
            {
                cmd.Parameters.AddWithValue("@GroupID", groupId);
                con.Open();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public static int GetMemberCount(int casId)
        {
            int groupId = GetFamilyGroupId(casId);
            if (groupId <= 0) return 0;

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM TblCase WHERE IFNULL(FamilyGroupID, CasID) = @GroupID AND IFNULL(IsArchived,0) = 0;", con))
            {
                cmd.Parameters.AddWithValue("@GroupID", groupId);
                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // جستجوی پرونده برای پیوند — بر اساس کد، شماره فرم یا نام سرپرست.
        // فیلترِ مرکز مثل بقیهٔ فرم‌ها اعمال می‌شود.
        public static DataTable SearchCasesForLink(string term, int excludeCasId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT c.CasID, c.Code, c.FormNo, c.HeadFullName, c.HeadFatherName, c.Phone,
       IFNULL(rt.Name, c.RequestType) AS RequestTypeName
FROM TblCase c
LEFT JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID
WHERE c.CasID <> @Exclude
  AND IFNULL(c.IsArchived, 0) = 0
  AND (@CID = 0 OR c.CenterID = @CID)
  AND (@Term = '' OR c.Code LIKE @Like OR CAST(c.FormNo AS TEXT) LIKE @Like
       OR c.HeadFullName LIKE @Like OR c.Phone LIKE @Like)
ORDER BY c.CasID DESC
LIMIT 200;", con))
            {
                string t = (term ?? "").Trim();
                cmd.Parameters.AddWithValue("@Exclude", excludeCasId);
                cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);
                cmd.Parameters.AddWithValue("@Term", t);
                cmd.Parameters.AddWithValue("@Like", "%" + t + "%");
                con.Open();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        private static string GetCaseCode(int casId)
        {
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand("SELECT Code FROM TblCase WHERE CasID = @Id LIMIT 1;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", casId);
                    con.Open();
                    object result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? "" : result.ToString();
                }
            }
            catch { return ""; }
        }

        private static void SyncCapture(int casId)
        {
            try
            {
                CaseManagement.Sync.SyncOutboxService.Capture("TblCase", casId,
                    CaseManagement.Sync.OfflineSyncInitializer.OperationUpdate);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("FamilyGroupService.SyncCapture failed: " + ex.Message);
            }
        }
    }
}

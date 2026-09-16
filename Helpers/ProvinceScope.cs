using System;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // حصارِ ولایتی روی *دفتر ثبت* (TblCenter.Province)، نه آدرس خانوارِ پرونده.
    // SuperAdmin: @PFMode=0 (بدون قید).
    // Admin/Operator/Viewer با ولایت معتبر: @PFMode=1 و IN مراکز همان ولایت.
    // ولایت خالی (fail-closed): @PFMode=2 → هیچ ردیفی.
    public static class ProvinceScope
    {
        public const int ModeAll = 0;
        public const int ModeProvince = 1;
        public const int ModeDeny = 2;

        public static int Mode
        {
            get
            {
                if (SecurityContext.IsSuperAdmin()) return ModeAll;
                if (string.IsNullOrWhiteSpace(SecurityContext.ProvinceFilter)) return ModeDeny;
                return ModeProvince;
            }
        }

        public static string FilterValue
        {
            get
            {
                string value = SecurityContext.ProvinceFilter;
                return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
            }
        }

        // alias خالی یعنی ستون CenterID بدون پیشوند؛ وگرنه مثلاً "c" → c.CenterID.
        public static string Sql(string alias)
        {
            string col = string.IsNullOrWhiteSpace(alias)
                ? "CenterID"
                : alias.Trim() + ".CenterID";
            return @"
 AND (
   @PFMode = 0
   OR (@PFMode = 1 AND " + col + @" IN (
        SELECT pf_c.CenterID FROM TblCenter pf_c
        WHERE pf_c.Province = @PF AND TRIM(IFNULL(pf_c.Province, '')) <> ''))
   OR (@PFMode = 2 AND 1 = 0)
 )";
        }

        public static void Bind(SQLiteCommand cmd)
        {
            if (cmd == null) return;
            if (cmd.Parameters.Contains("@PFMode")) return;
            cmd.Parameters.AddWithValue("@PFMode", Mode);
            cmd.Parameters.AddWithValue("@PF", FilterValue);
        }

        public static SQLiteParameter[] Parameters()
        {
            return new[]
            {
                Param("@PFMode", Mode),
                Param("@PF", FilterValue)
            };
        }

        public static string LoadOfficeProvince(int centerId)
        {
            if (centerId <= 0) return "";
            try
            {
                using (var con = new CaseManagement.DAL.DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT TRIM(IFNULL(Province, '')) FROM TblCenter WHERE CenterID = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", centerId);
                    con.Open();
                    object val = cmd.ExecuteScalar();
                    if (val == null || val == DBNull.Value) return "";
                    return Convert.ToString(val) ?? "";
                }
            }
            catch
            {
                return "";
            }
        }

        private static SQLiteParameter Param(string name, object value)
        {
            return new SQLiteParameter(name, value ?? "");
        }
    }
}

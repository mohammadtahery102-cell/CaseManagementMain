using System;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    public static class SecurityContext
    {
        public static int    UserId   { get; private set; }
        public static string Username { get; private set; }
        public static string Role     { get; private set; }

        public static int    CurrentCenterId   { get; private set; }
        public static string CurrentCenterCode { get; private set; }
        public static string CurrentCenterName { get; private set; }

        // true فقط برای SuperAdmin که گزینه "همه مراکز" را انتخاب کرده
        public static bool IsAllCenters { get; private set; }

        public static string CenterDisplay
        {
            get
            {
                if (IsAllCenters) return "همه مراکز";
                if (CurrentCenterId > 0)
                    return CurrentCenterCode + " - " + CurrentCenterName;
                return "مرکز انتخاب نشده";
            }
        }

        // مقدار برای پارامتر فیلتر کوئری‌ها:
        // 0 = بدون فیلتر (SuperAdmin-همه مراکز)، >0 = فیلتر روی همان مرکز
        public static int CenterFilterId
        {
            get { return IsAllCenters ? 0 : CurrentCenterId; }
        }

        public static bool IsLoggedIn { get { return UserId > 0; } }
        public static bool HasCenter  { get { return CurrentCenterId > 0 || IsAllCenters; } }

        public static void SignIn(int userId, string username, string role)
        {
            UserId   = userId;
            Username = username ?? "";
            Role     = role     ?? "";
        }

        public static void SelectCenter(int centerId, string centerCode, string centerName, bool allCenters = false)
        {
            IsAllCenters      = allCenters && IsSuperAdmin();
            CurrentCenterId   = IsAllCenters ? 0 : centerId;
            CurrentCenterCode = centerCode ?? "";
            CurrentCenterName = centerName ?? "";

            // ذخیره آخرین مرکز انتخاب‌شده در TblUsers
            if (UserId > 0 && !IsAllCenters)
                SaveLastCenter(UserId, centerId);
        }

        public static void SignOut()
        {
            UserId   = 0;
            Username = "";
            Role     = "";
            CurrentCenterId   = 0;
            CurrentCenterCode = "";
            CurrentCenterName = "";
            IsAllCenters      = false;
        }

        public static bool IsAdmin()
        {
            return IsSuperAdmin() ||
                   string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSuperAdmin()
        {
            return string.Equals(Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanEdit()
        {
            return IsAdmin() ||
                   string.Equals(Role, "Operator", StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanDelete()
        {
            return IsAdmin();
        }

        // true اگر کاربر جاری مجاز به دیدن/تغییر رکوردی با این CenterID باشد:
        // SuperAdmin در حالت «همه مراکز» همیشه مجاز است؛ در غیر این صورت فقط
        // اگر CenterID دقیقاً همان مرکز انتخاب‌شده کاربر باشد.
        public static bool CanAccessCenter(int centerId)
        {
            return IsAllCenters || centerId == CurrentCenterId;
        }

        // ─── ذخیره آخرین مرکز در TblUsers ───────────────────────────────────
        private static void SaveLastCenter(int userId, int centerId)
        {
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(
                    "UPDATE TblUsers SET LastCenterID = @CID WHERE UserID = @UID", con))
                {
                    cmd.Parameters.AddWithValue("@CID", centerId);
                    cmd.Parameters.AddWithValue("@UID", userId);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* ذخیره آخرین مرکز غیرحیاتی است؛ خطا نادیده گرفته می‌شود */ }
        }
    }
}

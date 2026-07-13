using System;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    // پرتاب می‌شود وقتی کاربر جاری تلاش می‌کند به رکوردی متعلق به مرکز دیگر
    // دسترسی پیدا کند (خواندن/ویرایش/حذف/Export). فرم‌ها این را می‌گیرند و
    // پیام مناسب نشان می‌دهند؛ اگر گرفته نشود هم عملیات به‌جای اجرا با داده
    // اشتباه، متوقف می‌شود.
    public class CenterAccessDeniedException : Exception
    {
        public CenterAccessDeniedException(string message) : base(message) { }
    }

    // لایه دفاعی دوم و مستقل از UI: حتی اگر یک فرم یا کوئری جدید فراموش کند
    // فیلتر مرکز را در SQL اعمال کند، این کلاس قبل از عملیات حساس (ویرایش،
    // حذف، Export، مدیریت کاربر) مالکیت مرکز رکورد را در لایه داده تأیید
    // می‌کند تا هیچ مسیری (فعلی یا آینده) بتواند این کنترل را دور بزند.
    public static class CenterGuard
    {
        public static int EnsureCaseAccess(DatabaseHelper db, int caseId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT CenterID FROM TblCase WHERE CasID = @CasID", con))
            {
                cmd.Parameters.AddWithValue("@CasID", caseId);
                con.Open();
                object val = cmd.ExecuteScalar();

                if (val == null || val == DBNull.Value)
                    throw new CenterAccessDeniedException("پرونده پیدا نشد.");

                int centerId = Convert.ToInt32(val);
                if (!SecurityContext.CanAccessCenter(centerId))
                    throw new CenterAccessDeniedException("این پرونده متعلق به مرکز دیگری است و شما به آن دسترسی ندارید.");

                return centerId;
            }
        }

        public static int EnsureUserAccess(DatabaseHelper db, int userId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT CenterID FROM TblUsers WHERE UserID = @UserID", con))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                con.Open();
                object val = cmd.ExecuteScalar();

                if (val == null)
                    throw new CenterAccessDeniedException("کاربر پیدا نشد.");

                int centerId = val == DBNull.Value ? 0 : Convert.ToInt32(val);
                if (!SecurityContext.CanAccessCenter(centerId))
                    throw new CenterAccessDeniedException("این کاربر متعلق به مرکز دیگری است و شما به آن دسترسی ندارید.");

                return centerId;
            }
        }
    }
}

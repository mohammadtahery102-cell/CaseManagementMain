using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // بررسیِ کیفیتِ داده — کاملاً فقط‌خواندنی، مثلِ DuplicateDetector.
    //
    // این کلاس فقط قوانینِ ورودیِ همینِ فرم‌ها را (که در FrmCase/FrmFamily از
    // قبل با ValidateForm اجرا می‌شوند) روی داده‌ی موجود در دیتابیس دوباره
    // می‌سنجد — برای پیدا کردنِ رکوردهایی که از راهِ دیگری (مهاجرت/Backup
    // قدیمی) وارد شده‌اند و آن اعتبارسنجی را رد کرده‌اند. هیچ قاعده‌ی
    // کسب‌وکاریِ جدیدی اختراع نمی‌شود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class DataQualityChecker
    {
        private readonly DatabaseHelper _db;

        public DataQualityChecker(DatabaseHelper db)
        {
            _db = db ?? new DatabaseHelper();
        }

        public DataQualityChecker() : this(null) { }

        public List<DataQualityIssue> Check()
        {
            var issues = new List<DataQualityIssue>();
            int centerFilter = SecurityContext.CenterFilterId;

            using (SQLiteConnection con = _db.GetConnection())
            {
                con.Open();
                CheckMissingCaseFields(con, centerFilter, issues);
                CheckInvalidIdentifierFormat(con, centerFilter, issues);
                CheckStopReasonConsistency(con, centerFilter, issues);
                CheckMissingFamilyMemberName(con, centerFilter, issues);
            }

            return issues;
        }

        // ─── داده‌ی گمشده: کد اختصاصی/نام سرپرست — هر دو در FrmCase الزامی‌اند ──
        private void CheckMissingCaseFields(SQLiteConnection con, int centerFilter, List<DataQualityIssue> issues)
        {
            using (var cmd = new SQLiteCommand(@"
SELECT CasID, COALESCE(Code,'') AS Code, COALESCE(HeadFullName,'') AS Nm
FROM TblCase
WHERE (@CID = 0 OR CenterID = @CID)
  AND IsArchived = 0
  AND (TRIM(COALESCE(Code,'')) = '' OR TRIM(COALESCE(HeadFullName,'')) = '')", con))
            {
                cmd.Parameters.AddWithValue("@CID", centerFilter);

                using (SQLiteDataReader rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        int casId = Convert.ToInt32(rd["CasID"]);
                        string code = Convert.ToString(rd["Code"]).Trim();
                        string name = Convert.ToString(rd["Nm"]).Trim();

                        if (code == "")
                            issues.Add(DataQualityIssue.Missing(casId, code, name, "کد اختصاصی خالی است"));

                        if (name == "")
                            issues.Add(DataQualityIssue.Missing(casId, code, name, "نام سرپرست خالی است"));
                    }
                }
            }
        }

        // ─── فرمتِ نامعتبر: تذکره/تلفن باید فقط رقم باشند (همان فرضی که
        // DuplicateDetector.NormalizeIdentifier از قبل روی این دو فیلد دارد) ──
        private void CheckInvalidIdentifierFormat(SQLiteConnection con, int centerFilter, List<DataQualityIssue> issues)
        {
            using (var cmd = new SQLiteCommand(@"
SELECT CasID, COALESCE(Code,'') AS Code, COALESCE(HeadFullName,'') AS Nm,
       COALESCE(HeadTazkiraNo,'') AS Tz, COALESCE(Phone,'') AS Ph
FROM TblCase
WHERE (@CID = 0 OR CenterID = @CID)
  AND IsArchived = 0", con))
            {
                cmd.Parameters.AddWithValue("@CID", centerFilter);

                using (SQLiteDataReader rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        int casId = Convert.ToInt32(rd["CasID"]);
                        string code = Convert.ToString(rd["Code"]).Trim();
                        string name = Convert.ToString(rd["Nm"]).Trim();
                        string tazkira = Convert.ToString(rd["Tz"]).Trim();
                        string phone = Convert.ToString(rd["Ph"]).Trim();

                        if (tazkira != "" && HasNonDigit(tazkira))
                            issues.Add(DataQualityIssue.InvalidFormat(casId, code, name, "شماره تذکره شامل نویسه غیرعددی است: " + tazkira));

                        if (phone != "" && HasNonDigit(phone))
                            issues.Add(DataQualityIssue.InvalidFormat(casId, code, name, "شماره تماس شامل نویسه غیرعددی است: " + phone));
                    }
                }
            }
        }

        // ─── ناسازگاری: وضعیت «قطع موقت» ولی دلیلِ قطع خالی — همان قاعده‌ای
        // که FrmCase.ValidateForm از قبل هنگامِ ثبت اجرا می‌کند ────────────────
        private void CheckStopReasonConsistency(SQLiteConnection con, int centerFilter, List<DataQualityIssue> issues)
        {
            using (var cmd = new SQLiteCommand(@"
SELECT CasID, COALESCE(Code,'') AS Code, COALESCE(HeadFullName,'') AS Nm
FROM TblCase
WHERE (@CID = 0 OR CenterID = @CID)
  AND IsArchived = 0
  AND ServiceStatus = 'قطع موقت'
  AND TRIM(COALESCE(StopReason,'')) = ''", con))
            {
                cmd.Parameters.AddWithValue("@CID", centerFilter);

                using (SQLiteDataReader rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        int casId = Convert.ToInt32(rd["CasID"]);
                        string code = Convert.ToString(rd["Code"]).Trim();
                        string name = Convert.ToString(rd["Nm"]).Trim();

                        issues.Add(DataQualityIssue.Inconsistent(casId, code, name,
                            "وضعیت «قطع موقت» است ولی دلیلِ قطع ثبت نشده"));
                    }
                }
            }
        }

        // ─── داده‌ی گمشده در اعضای خانواده: نام عضو در FrmFamily الزامی است ───
        private void CheckMissingFamilyMemberName(SQLiteConnection con, int centerFilter, List<DataQualityIssue> issues)
        {
            using (var cmd = new SQLiteCommand(@"
SELECT f.CasID, COALESCE(c.Code,'') AS Code, COALESCE(c.HeadFullName,'') AS Nm
FROM TblFamily f
JOIN TblCase c ON c.CasID = f.CasID
WHERE (@CID = 0 OR c.CenterID = @CID)
  AND c.IsArchived = 0
  AND TRIM(COALESCE(f.MemberName,'')) = ''", con))
            {
                cmd.Parameters.AddWithValue("@CID", centerFilter);

                using (SQLiteDataReader rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        int casId = Convert.ToInt32(rd["CasID"]);
                        string code = Convert.ToString(rd["Code"]).Trim();
                        string name = Convert.ToString(rd["Nm"]).Trim();

                        issues.Add(DataQualityIssue.Missing(casId, code, name, "یکی از اعضای خانواده بدون نام است"));
                    }
                }
            }
        }

        private static bool HasNonDigit(string value)
        {
            foreach (char c in value)
                if (!char.IsDigit(c))
                    return true;

            return false;
        }
    }

    public enum DataQualityIssueType
    {
        Missing,
        InvalidFormat,
        Inconsistent
    }

    public sealed class DataQualityIssue
    {
        public int CasId { get; set; }
        public string Code { get; set; }
        public string HeadFullName { get; set; }
        public DataQualityIssueType IssueType { get; set; }
        public string Description { get; set; }

        public string IssueTypeText
        {
            get
            {
                switch (IssueType)
                {
                    case DataQualityIssueType.Missing: return "داده گمشده";
                    case DataQualityIssueType.InvalidFormat: return "فرمت نامعتبر";
                    case DataQualityIssueType.Inconsistent: return "ناسازگاری";
                    default: return "";
                }
            }
        }

        public static DataQualityIssue Missing(int casId, string code, string name, string description)
        {
            return new DataQualityIssue { CasId = casId, Code = code, HeadFullName = name, IssueType = DataQualityIssueType.Missing, Description = description };
        }

        public static DataQualityIssue InvalidFormat(int casId, string code, string name, string description)
        {
            return new DataQualityIssue { CasId = casId, Code = code, HeadFullName = name, IssueType = DataQualityIssueType.InvalidFormat, Description = description };
        }

        public static DataQualityIssue Inconsistent(int casId, string code, string name, string description)
        {
            return new DataQualityIssue { CasId = casId, Code = code, HeadFullName = name, IssueType = DataQualityIssueType.Inconsistent, Description = description };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using CaseManagement.Models;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه داده (Data Access) ماژول کارت شناسایی سرپرست — تنها مسئولیتش
    // خواندن یک پرونده از SQLite و پرکردن مدل موجود CaseModel است (طبق
    // درخواست: «از Models و Repository موجود استفاده شود»؛ این پروژه قبلاً
    // کلاس Repository اختصاصی نداشت، پس همان الگوی رایج پروژه — DatabaseHelper
    // + CenterGuard برای امنیت چندمرکزی — اینجا در قالب یک Repository به‌کار
    // می‌رود، دقیقاً مثل OpenXmlCaseExporter که همین دو را برای Word/PDF export
    // به‌کار می‌برد).
    // ─────────────────────────────────────────────────────────────────────────
    public class CaseCardRepository
    {
        private readonly DatabaseHelper _db = new DatabaseHelper();

        // آموزش — CenterGuard.EnsureCaseAccess همان بررسی امنیتی چندمرکزی است
        // که همه مسیرهای Export دیگر (Word/PDF/Excel) از آن عبور می‌کنند؛ اینجا
        // هم عیناً رعایت می‌شود تا کارت شناسایی نتواند پرونده مرکز دیگر را بخواند.
        public CaseModel GetCase(int caseId)
        {
            if (caseId <= 0)
                throw new ArgumentException("شناسه پرونده معتبر نیست.");

            CenterGuard.EnsureCaseAccess(_db, caseId);

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM TblCase WHERE CasID = @CasID", con))
            {
                cmd.Parameters.AddWithValue("@CasID", caseId);
                con.Open();
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);
                    if (dt.Rows.Count == 0)
                        throw new InvalidOperationException("پرونده پیدا نشد.");
                    return MapCase(dt.Rows[0]);
                }
            }
        }

        // آموزش — چاپ جمعی کارت‌ها: شناسه پرونده‌های دارای شماره فرم در بازه‌ی
        // [fromFormNo, toFormNo] را برمی‌گرداند، مرتب بر اساس شماره فرم. دقیقاً
        // همان الگوی امنیتی چندمرکزیِ بقیه‌ی کوئری‌های پروژه («@CID = 0 یا
        // CenterID = @CID») رعایت می‌شود تا مدیر یک مرکز نتواند کارت پرونده‌ی
        // مرکز دیگر را چاپ کند.
        public List<int> GetCaseIdsByFormNoRange(int fromFormNo, int toFormNo)
        {
            var ids = new List<int>();
            int cid = SecurityContext.CenterFilterId;

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT CasID FROM TblCase
WHERE FormNo IS NOT NULL AND FormNo BETWEEN @From AND @To
  AND (@CID = 0 OR CenterID = @CID)
ORDER BY FormNo", con))
            {
                cmd.Parameters.AddWithValue("@From", fromFormNo);
                cmd.Parameters.AddWithValue("@To", toFormNo);
                cmd.Parameters.AddWithValue("@CID", cid);
                con.Open();
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                    while (dr.Read())
                        ids.Add(Convert.ToInt32(dr["CasID"]));
            }

            return ids;
        }

        // تعداد اعضای خانواده (ایتام) این پرونده — برای فیلد OrphansCount.
        public int GetFamilyMemberCount(int caseId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT COUNT(1) FROM TblFamily WHERE CasID = @CasID", con))
            {
                cmd.Parameters.AddWithValue("@CasID", caseId);
                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static CaseModel MapCase(DataRow row)
        {
            return new CaseModel
            {
                CasID = Convert.ToInt32(row["CasID"]),
                FormNo = row["FormNo"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["FormNo"]),
                Code = GetString(row, "Code"),
                Phone = GetString(row, "Phone"),
                PhotoPath = GetString(row, "PhotoPath"),
                FamilyPhotoPath = GetString(row, "FamilyPhotoPath"),
                CaseNo = GetString(row, "CaseNo"),
                Zone = GetString(row, "Zone"),
                Province = GetString(row, "Province"),
                District = GetString(row, "District"),
                RequestType = GetString(row, "RequestType"),
                PriorityLevel = GetString(row, "PriorityLevel"),
                HeadFullName = GetString(row, "HeadFullName"),
                HeadFatherName = GetString(row, "HeadFatherName"),
                HeadSadat = GetString(row, "HeadSadat"),
                Religion = GetString(row, "Religion"),
                HeadTazkiraNo = GetString(row, "HeadTazkiraNo"),
                HeadOriginalResidence = GetString(row, "HeadOriginalResidence"),
                HeadCurrentResidence = GetString(row, "HeadCurrentResidence"),
                RelationshipToFamily = GetString(row, "RelationshipToFamily"),
                RelativePhone = GetString(row, "RelativePhone"),
                CoveredByOrg = GetString(row, "CoveredByOrg"),
                Job = GetString(row, "Job"),
                Skill = GetString(row, "Skill"),
                DisabilityDegree = GetString(row, "DisabilityDegree"),
                DisabilityType = GetString(row, "DisabilityType"),
                MigrationCardType = GetString(row, "MigrationCardType"),
                MaritalStatus = GetString(row, "MaritalStatus"),
                Surveyors = GetString(row, "Surveyors"),
                LocationAddress = GetString(row, "LocationAddress"),
                EducationLevel = GetString(row, "EducationLevel"),
                ServiceStatus = GetString(row, "ServiceStatus"),
                UrgentSituation = GetString(row, "UrgentSituation")
            };
        }

        private static string GetString(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value) return "";
            return row[col].ToString();
        }
    }
}

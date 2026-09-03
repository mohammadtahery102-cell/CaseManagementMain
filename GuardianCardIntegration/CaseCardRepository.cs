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
    // فیلترِ صفحه «پیش‌نمایش قبل از چاپ جمعی» — به‌درخواست کاربر (بخش «ID CARD
    // FILTERING BEFORE PRINT»). هر رشته خالی یعنی «همه» (بدون محدودیت)، دقیقاً
    // همان قاعده‌ی بقیه‌ی فیلترهای پروژه. UseFormNoRange پیش‌فرض false است تا
    // رفتار قبلی (بازه‌ی اجباری) دیگر تحمیل نشود؛ اگر کاربر بخواهد هنوز هم
    // می‌تواند فعالش کند.
    // ─────────────────────────────────────────────────────────────────────────
    public class GuardianCardBatchFilter
    {
        public string Province { get; set; } = "";
        public string District { get; set; } = "";
        public string CaseType { get; set; } = "";      // TblCase.RequestType
        public string ServiceStatus { get; set; } = "";
        // «Member Role» در سطح خانواده معنا دارد نه سرپرست؛ اینجا یعنی «فقط
        // پرونده‌هایی که حداقل یک عضو با این نقش دارند» (مثلاً فقط خانواده‌های
        // دارای یتیم).
        public string MemberRole { get; set; } = "";
        // معادلِ نزدیکِ «Donor» در این پروژه — نگاه کنید یادداشتِ CoveredByOrg
        // در CardService؛ این پروژه موجودیتِ ساختاریافته‌ی «اهداکننده» ندارد.
        public string CoveredByOrg { get; set; } = "";
        // فیلترِ آزاد («Custom filters») — تطابق با کد پرونده یا نام سرپرست.
        public string SearchText { get; set; } = "";
        public bool UseFormNoRange { get; set; } = false;
        public int FromFormNo { get; set; } = 1;
        public int ToFormNo { get; set; } = 999999;
    }

    // یک ردیفِ پیش‌نمایشِ فهرستِ چاپ جمعی — دقیقاً همان ستون‌هایی که کاربر
    // خواسته: کد پرونده، نام (سرپرست/کارت)، نقش (وضعیتِ ایتام/نقش)، وضعیت.
    public class GuardianCardBatchPreviewRow
    {
        public int CasID { get; set; }
        public string CaseCode { get; set; }
        public string GuardianName { get; set; }
        public string Province { get; set; }
        public string District { get; set; }
        public string CaseType { get; set; }
        public string ServiceStatus { get; set; }
        // نمایشِ «نقش» برای این کارت: چون کارت مخصوصِ سرپرست است نه یک عضو
        // خاص، اینجا وضعیتِ ایتامِ خانواده نمایش داده می‌شود (همان چیزی که
        // واقعاً روی کارت چاپ می‌شود — OrphansCount)، نه یک نقشِ جعلی برای
        // خودِ سرپرست.
        public string RoleSummary { get; set; }
    }

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

        // آموزش — رفعِ باگِ گزارش‌شده: قبلاً COUNT(*) روی همه‌ی اعضای TblFamily
        // بود، یعنی مادر/پدر/سرپرستِ ثبت‌شده در همان جدول هم جزوِ «ایتام»
        // شمرده می‌شد. حالا طبق ستونِ جدیدِ MemberRole (بخش ۱۲) فقط اعضایی که
        // صریحاً «یتیم» علامت خورده‌اند شمرده می‌شوند. رکوردهای قدیمی/بازبینی‌
        // نشده (MemberRole خالی) عمداً شمرده نمی‌شوند — حدس زدن نقششان (که
        // شاید مادر باشد نه یتیم) اشتباه‌تر از کمترنشان‌دادن تا بازبینی است.
        public int GetFamilyMemberCount(int caseId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT COUNT(1) FROM TblFamily WHERE CasID = @CasID AND MemberRole = 'یتیم'", con))
            {
                cmd.Parameters.AddWithValue("@CasID", caseId);
                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // آموزش — به‌درخواست کاربر: تا وقتی نقشِ همه‌ی اعضای این پرونده
        // بازبینی/تعیین نشده، عدد «تعداد ایتام» را نباید بی‌صدا صفر نشان داد
        // (چون شاید واقعاً صفر نباشد، فقط هنوز تعیین نشده). CardService از این
        // متد برای تصمیم «عدد را نشان بده یا نیازِ به‌تعیین‌نقش را» استفاده می‌کند.
        public bool HasUnassignedMemberRoles(int caseId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT COUNT(1) FROM TblFamily WHERE CasID = @CasID AND (MemberRole IS NULL OR MemberRole = '')", con))
            {
                cmd.Parameters.AddWithValue("@CasID", caseId);
                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        // آموزش — پیش‌نمایشِ فهرستِ چاپِ جمعی («ID CARD FILTERING BEFORE PRINT»):
        // یک کوئریِ واحد (نه N+1) که هم فهرستِ پرونده‌های منطبق را می‌دهد هم
        // ستون‌های نمایشیِ پیش‌نمایش را — تا کاربر پیش از تولیدِ واقعیِ کارت‌ها
        // (که کند و پرمصرف است) دقیقاً ببیند چند و کدام کارت چاپ می‌شود.
        // سقفِ ۵۰۰ ردیف دقیقاً مثلِ FrmAssignMemberRole تا UI قفل نکند.
        public List<GuardianCardBatchPreviewRow> PreviewBatch(GuardianCardBatchFilter filter, out bool truncated)
        {
            const int maxRows = 500;
            var result = new List<GuardianCardBatchPreviewRow>();
            int cid = SecurityContext.CenterFilterId;

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT c.CasID, c.Code AS CaseCode, c.HeadFullName AS GuardianName, c.Province, c.District,
       c.RequestType, c.ServiceStatus,
       (SELECT COUNT(1) FROM TblFamily fo WHERE fo.CasID = c.CasID AND fo.MemberRole = 'یتیم') AS OrphanCount,
       CASE WHEN EXISTS (
           SELECT 1 FROM TblFamily fu WHERE fu.CasID = c.CasID AND (fu.MemberRole IS NULL OR fu.MemberRole = '')
       ) THEN 1 ELSE 0 END AS PendingRoleReview
FROM TblCase c
WHERE c.IsArchived = 0
  AND (@CID = 0 OR c.CenterID = @CID)
  AND (@Province = '' OR c.Province = @Province)
  AND (@District = '%%' OR c.District LIKE @District)
  AND (@CaseType = '' OR c.RequestType = @CaseType)
  AND (@Svc = '' OR c.ServiceStatus = @Svc)
  AND (@Covered = '' OR c.CoveredByOrg = @Covered)
  AND (@UseRange = 0 OR (c.FormNo IS NOT NULL AND c.FormNo BETWEEN @From AND @To))
  AND (@Role = '' OR EXISTS (SELECT 1 FROM TblFamily fr WHERE fr.CasID = c.CasID AND fr.MemberRole = @Role))
  AND (@Search = '' OR c.Code LIKE @SearchLike OR c.HeadFullName LIKE @SearchLike)
ORDER BY c.FormNo, c.Code
LIMIT @MaxRows", con))
            {
                string search = (filter.SearchText ?? "").Trim();

                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Province", (filter.Province ?? "").Trim());
                cmd.Parameters.AddWithValue("@District", "%" + (filter.District ?? "").Trim() + "%");
                cmd.Parameters.AddWithValue("@CaseType", (filter.CaseType ?? "").Trim());
                cmd.Parameters.AddWithValue("@Svc", (filter.ServiceStatus ?? "").Trim());
                cmd.Parameters.AddWithValue("@Covered", (filter.CoveredByOrg ?? "").Trim());
                cmd.Parameters.AddWithValue("@UseRange", filter.UseFormNoRange ? 1 : 0);
                cmd.Parameters.AddWithValue("@From", filter.FromFormNo);
                cmd.Parameters.AddWithValue("@To", filter.ToFormNo);
                cmd.Parameters.AddWithValue("@Role", (filter.MemberRole ?? "").Trim());
                cmd.Parameters.AddWithValue("@Search", search);
                cmd.Parameters.AddWithValue("@SearchLike", "%" + search + "%");
                cmd.Parameters.AddWithValue("@MaxRows", maxRows + 1);

                con.Open();
                truncated = false;
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        if (result.Count >= maxRows)
                        {
                            truncated = true;
                            break;
                        }

                        bool pending = Convert.ToInt32(dr["PendingRoleReview"]) != 0;
                        int orphanCount = Convert.ToInt32(dr["OrphanCount"]);
                        string roleSummary = pending ? "نیاز به تعیین نقش" : (orphanCount + " یتیم");

                        result.Add(new GuardianCardBatchPreviewRow
                        {
                            CasID = Convert.ToInt32(dr["CasID"]),
                            CaseCode = dr["CaseCode"] == DBNull.Value ? "" : dr["CaseCode"].ToString(),
                            GuardianName = dr["GuardianName"] == DBNull.Value ? "" : dr["GuardianName"].ToString(),
                            Province = dr["Province"] == DBNull.Value ? "" : dr["Province"].ToString(),
                            District = dr["District"] == DBNull.Value ? "" : dr["District"].ToString(),
                            CaseType = dr["RequestType"] == DBNull.Value ? "" : dr["RequestType"].ToString(),
                            ServiceStatus = dr["ServiceStatus"] == DBNull.Value ? "" : dr["ServiceStatus"].ToString(),
                            RoleSummary = roleSummary
                        });
                    }
                }
            }

            return result;
        }

        // آموزش — فهرستِ اعضای خانواده برای قالبِ «ساده» (نگاه کنید
        // GuardianCardData.Orphans). آموزش — رفعِ باگ: نسخهٔ قبلی با
        // MemberRole='یتیم' فیلتر می‌کرد (مثلِ GetFamilyMemberCount بالا)،
        // ولی در دیتابیسِ واقعیِ کاربر این ستون برای هیچ رکوردی پر نشده
        // (۳۸۰۲ عضوِ موجود، همه MemberRole خالی) — یعنی فهرست همیشه خالی
        // می‌ماند. طبقِ درخواستِ صریحِ کاربر، حالا همهٔ اعضای پرونده (بدونِ
        // فیلترِ نقش) نمایش داده می‌شوند.
        public List<OrphanRow> GetOrphans(int caseId)
        {
            var list = new List<OrphanRow>();
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT MemberName, MemberFatherName, MemberTazkiraNo, MemberPhotoPath, BirthDate FROM TblFamily " +
                "WHERE CasID = @CasID ORDER BY FamID", con))
            {
                cmd.Parameters.AddWithValue("@CasID", caseId);
                con.Open();
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        // آموزش — رفعِ باگِ حیاتی: PersianCalendar فقط تاریخ‌هایِ
                        // بینِ ۶۲۲ تا ۹۹۹۹ میلادی را می‌پذیرد و برایِ هر
                        // مقدارِ خارج از این بازه (که در دادهٔ واقعیِ سال‌ها
                        // وارد‌شده توسط کاربران کاملاً محتمل است — مثلاً
                        // تاریخِ ناقص/خرابِ ثبت‌شده) ArgumentOutOfRangeException
                        // می‌دهد؛ چون این استثنا داخلِ چاپِ جمعی (فراخوانی
                        // برایِ ده‌ها پروندهٔ پشتِ‌سرهم) گرفته نمی‌شد، یک
                        // تاریخِ تولدِ خرابِ تکی می‌توانست کلِ آن پرونده را از
                        // میانهٔ دسته بیندازد و باعثِ جابه‌جاییِ پرونده‌های
                        // بعدی در خروجی شود. حالا مثلِ بقیهٔ متدهایِ همین
                        // کلاس (DescribeAge) با try/catch محافظت می‌شود —
                        // تاریخِ نامعتبر فقط خودش خالی می‌ماند، نه اینکه کلِ
                        // پرونده را بترکاند.
                        string birthDate = "";
                        try
                        {
                            DateTime birthDateRaw = PersianDateHelper.ParseStoredDate(dr["BirthDate"], DateTime.MinValue);
                            if (birthDateRaw != DateTime.MinValue)
                            {
                                // آموزش — به‌درخواستِ کاربر: روی کارت فقط *سالِ*
                                // تولد چاپ می‌شود (مثلاً «۱۳۹۰»)، نه تاریخِ کامل.
                                // دلیل: ستونِ تاریخ بخشِ زیادی از عرضِ جدولِ اعضا
                                // را می‌گرفت و جای «نام پدر» و «شماره تذکره» را
                                // تنگ می‌کرد. مقدارِ ذخیره‌شده در دیتابیس هیچ
                                // تغییری نمی‌کند — فقط نمایشِ روی کارت.
                                string full = PersianDateHelper.ToPersianDateString(birthDateRaw);
                                int slash = full.IndexOf('/');
                                birthDate = slash > 0 ? full.Substring(0, slash) : full;
                            }
                        }
                        catch
                        {
                            birthDate = "";
                        }

                        list.Add(new OrphanRow
                        {
                            Name = dr["MemberName"] == DBNull.Value ? "" : dr["MemberName"].ToString(),
                            FatherName = dr["MemberFatherName"] == DBNull.Value ? "" : dr["MemberFatherName"].ToString(),
                            BirthDate = birthDate,
                            TazkiraNo = dr["MemberTazkiraNo"] == DBNull.Value ? "" : dr["MemberTazkiraNo"].ToString(),
                            Photo = dr["MemberPhotoPath"] == DBNull.Value ? "" : dr["MemberPhotoPath"].ToString()
                        });
                    }
                }
            }
            return list;
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
                UrgentSituation = GetString(row, "UrgentSituation"),
                CardNotice1 = GetString(row, "CardNotice1"),
                CardNotice2 = GetString(row, "CardNotice2"),
                CardNotice3 = GetString(row, "CardNotice3"),
                CardNotice4 = GetString(row, "CardNotice4"),
                CardNotice5 = GetString(row, "CardNotice5"),
                CardLegalLine = GetString(row, "CardLegalLine"),
                CardPhone = GetString(row, "CardPhone"),
                CardAddress = GetString(row, "CardAddress")
            };
        }

        // آموزش — ذخیره‌ی متنِ شرایط/هشدارها/تلفن/آدرسِ اختصاصیِ این پرونده
        // (override روی مقدار سراسریِ تنظیمات مؤسسه). رشته‌ی خالی یعنی «حذف
        // override و بازگشت به مقدار پیش‌فرض سراسری» (همان قاعده‌ی CardText
        // در CardService). این متد فقط برای حالتِ «ذخیره‌ی دائمی» (نه
        // «فقط برای این چاپ») در FrmCardNoticesEdit فراخوانی می‌شود.
        public void UpdateCardOverrides(int caseId, string n1, string n2, string n3, string n4, string n5,
            string legalLine, string phone, string address)
        {
            if (caseId <= 0)
                throw new ArgumentException("شناسه پرونده معتبر نیست.");

            CenterGuard.EnsureCaseAccess(_db, caseId);

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
UPDATE TblCase SET
    CardNotice1 = @N1, CardNotice2 = @N2, CardNotice3 = @N3,
    CardNotice4 = @N4, CardNotice5 = @N5, CardLegalLine = @Legal,
    CardPhone = @Phone, CardAddress = @Address
WHERE CasID = @CasID", con))
            {
                cmd.Parameters.AddWithValue("@N1", (n1 ?? "").Trim());
                cmd.Parameters.AddWithValue("@N2", (n2 ?? "").Trim());
                cmd.Parameters.AddWithValue("@N3", (n3 ?? "").Trim());
                cmd.Parameters.AddWithValue("@N4", (n4 ?? "").Trim());
                cmd.Parameters.AddWithValue("@N5", (n5 ?? "").Trim());
                cmd.Parameters.AddWithValue("@Legal", (legalLine ?? "").Trim());
                cmd.Parameters.AddWithValue("@Phone", (phone ?? "").Trim());
                cmd.Parameters.AddWithValue("@Address", (address ?? "").Trim());
                cmd.Parameters.AddWithValue("@CasID", caseId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // آموزش — همتای تک‌ستونیِ UpdateCardOverrides بالا، فقط برای «تذکرات»
        // قالبِ ساده (که همان CardNotice1 است). چرا متدِ جدا؟ چون
        // UpdateCardOverrides همیشه هر ۸ ستون را UPDATE می‌کند — اگر از آن
        // برای ذخیرهٔ «فقط تذکراتِ قالبِ ساده» استفاده می‌شد، مقادیرِ
        // موجودِ Notice2-5/LegalLine/Phone/Address (که به قالبِ ساده اصلاً
        // نمایش داده نمی‌شوند) با رشتهٔ خالی پاک می‌شدند.
        public void UpdateCardNotice1(int caseId, string notice1)
        {
            if (caseId <= 0)
                throw new ArgumentException("شناسه پرونده معتبر نیست.");

            CenterGuard.EnsureCaseAccess(_db, caseId);

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "UPDATE TblCase SET CardNotice1 = @N1 WHERE CasID = @CasID", con))
            {
                cmd.Parameters.AddWithValue("@N1", (notice1 ?? "").Trim());
                cmd.Parameters.AddWithValue("@CasID", caseId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static string GetString(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value) return "";
            return row[col].ToString();
        }
    }
}

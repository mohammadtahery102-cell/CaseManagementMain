using CaseManagement.DAL;
using System;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // Phase 5.5-C — لایهٔ آماده‌سازیِ دادهٔ خروجی.
    //
    // چرا DataTable و نه DTO/ViewModel: هر سه مصرف‌کنندهٔ این داده عیناً
    // DataTable می‌خواهند —
    //   • ExcelReportExporter.AddTableSheet(workbook, name, DataTable)
    //   • PrintHelper.PrintDataTable(owner, title, DataTable)
    //   • ReportDataSource("Name", DataTable)  ← مسیرِ RDLC در فازِ بعد
    // پس DataTable اینجا «شکلِ طبیعیِ» داده است، نه میان‌بر. اگر DTO بسازیم،
    // هر سه مصرف‌کننده باید دوباره به DataTable تبدیلش کنند.
    //
    // نامِ متدها عمداً با نامِ DataSetهای آیندهٔ RDLC هم‌ریشه است
    // (TimelineData/VisitData/FundingData/ScoreData/DocumentStatusData) تا
    // فازِ اعتبارسنجیِ گزارش فقط یک خط لازم داشته باشد:
    //     localReport.DataSources.Add(new ReportDataSource(
    //         "TimelineData", CaseExportDataProvider.GetTimeline(caseId)));
    //
    // ⚠ این کلاس هیچ فایلِ RDLC/طرحی را تغییر نمی‌دهد و نباید بدهد.
    // ═══════════════════════════════════════════════════════════════════════
    public static class CaseExportDataProvider
    {
        // ─── تایم‌لاین کامل ─────────────────────────────────────────────────
        // شاملِ همهٔ دسته‌ها: ثبت/ویرایش، تغییر وضعیت، اسناد، تأمین مالی،
        // خیّر، مساعدت، بازدید، امتیاز، فعال‌سازی/تعلیق — یعنی دقیقاً همان
        // فهرستی که در الزاماتِ «Timeline Output Integration» آمده.
        public static DataTable GetTimeline(int caseId)
        {
            return Query(@"
SELECT t.EventDate                AS [تاریخ],
       t.Title                    AS [رویداد],
       IFNULL(t.Details, '')      AS [شرح],
       IFNULL(t.OldValue, '')     AS [مقدار قبلی],
       IFNULL(t.NewValue, '')     AS [مقدار جدید],
       IFNULL(t.Username, '')     AS [کاربر],
       t.EventAt                  AS [زمان ثبت]
FROM TblCaseTimeline t
WHERE t.CasID = @CasID
ORDER BY t.EventAt DESC, t.TimelineID DESC;", caseId);
        }

        // ─── اعضای خانواده ──────────────────────────────────────────────────
        // فاز ۵.۵-E — پیش از این، خروجیِ «پروندهٔ کامل» هیچ فهرستی از اعضا
        // نداشت: اعضا فقط در گزارشِ RDLC و خروجیِ Word دیده می‌شدند، پس
        // خروجیِ اکسل/چاپ ناقص بود.
        public static DataTable GetFamilyMembers(int caseId)
        {
            return Query(@"
SELECT IFNULL(f.MemberName, '')             AS [نام عضو],
       IFNULL(f.MemberFatherName, '')       AS [نام پدر],
       IFNULL(f.MemberRole, '')             AS [نسبت],
       IFNULL(f.Gender, '')                 AS [جنسیت],
       IFNULL(f.BirthDate, '')              AS [تاریخ تولد],
       IFNULL(f.MemberTazkiraNo, '')        AS [شماره تذکره],
       IFNULL(f.MemberEducation, '')        AS [تحصیلات],
       IFNULL(f.PhysicalStatus, '')         AS [وضعیت جسمی],
       IFNULL(f.HasDisability, '')          AS [نوع معلولیت],
       IFNULL(f.MemberDisabilityDegree, '') AS [درجه معلولیت],
       IFNULL(f.ServiceStatus, '')          AS [وضعیت خدمات]
FROM TblFamily f
WHERE f.CasID = @CasID
ORDER BY f.FamID;", caseId);
        }

        // ─── اطلاعات معلولیت ────────────────────────────────────────────────
        // یازده فیلدِ کاملِ معلولیت. RDLC و Word فقط نوع و درجه را چاپ
        // می‌کنند؛ این بخش خروجیِ اکسل/چاپ را کامل می‌کند.
        public static DataTable GetDisabilityInfo(int caseId)
        {
            return Query(@"
SELECT IFNULL(d.DisabilityType, '')        AS [نوع معلولیت],
       IFNULL(d.DisabilityDegree, '')      AS [درجه معلولیت],
       IFNULL(d.DisabilityCause, '')       AS [دلیل معلولیت],
       IFNULL(d.DisabilityDescription, '') AS [شرح معلولیت],
       IFNULL(d.SpecialNeeds, '')          AS [نیازهای خاص],
       IFNULL(d.HasDisabilityCard, '')     AS [وضعیت کارت معلولیت],
       IFNULL(d.DisabilityCardNumber, '')  AS [شماره کارت معلولیت],
       IFNULL(d.CardIssuer, '')            AS [صادرکننده کارت],
       IFNULL(d.IssueDate, '')             AS [تاریخ صدور کارت],
       IFNULL(d.ExpiryDate, '')            AS [تاریخ انقضای کارت],
       IFNULL(d.Notes, '')                 AS [یادداشت معلولیت]
FROM TblDisability d
WHERE d.CasID = @CasID;", caseId);
        }

        // ─── اطلاعات ایتام ──────────────────────────────────────────────────
        public static DataTable GetOrphanInfo(int caseId)
        {
            return Query(@"
SELECT IFNULL(o.MainResidenceProvince, '') AS [ولایت اقامتگاه اصلی],
       IFNULL(o.MainResidenceDistrict, '') AS [ولسوالی اقامتگاه اصلی],
       IFNULL(o.MainResidenceVillage, '')  AS [قریه اقامتگاه اصلی],
       IFNULL(o.FatherStatus, '')          AS [وضعیت پدر],
       IFNULL(o.FatherDeathCause, '')      AS [دلیل فوت پدر],
       IFNULL(o.FatherDeathDate, '')       AS [تاریخ فوت پدر],
       IFNULL(o.MotherStatus, '')          AS [وضعیت مادر],
       IFNULL(o.GuardianName, '')          AS [نام سرپرست کودک],
       IFNULL(o.GuardianRelationship, '')  AS [نسبت سرپرست],
       IFNULL(o.SchoolName, '')            AS [نام مکتب],
       IFNULL(o.EducationLevel, '')        AS [سطح تحصیلات کودک],
       IFNULL(o.IsStudent, '')             AS [وضعیت تحصیل],
       IFNULL(o.Notes, '')                 AS [یادداشت ایتام]
FROM TblOrphan o
WHERE o.CasID = @CasID;", caseId);
        }

        // ─── اطلاعات مهاجرت ─────────────────────────────────────────────────
        public static DataTable GetMigrantInfo(int caseId)
        {
            return Query(@"
SELECT IFNULL(m.HasMigrationCard, '')    AS [دارای کارت مهاجرت],
       IFNULL(m.MigrationCardType, '')   AS [نوع برگه مهاجرت],
       IFNULL(m.MigrationCardNumber, '') AS [شماره کارت مهاجرت],
       IFNULL(m.OriginCountry, '')       AS [کشور مبدأ],
       IFNULL(m.DestinationCountry, '')  AS [کشور مقصد],
       IFNULL(m.DepartureDate, '')       AS [تاریخ خروج],
       IFNULL(m.ArrivalDate, '')         AS [تاریخ ورود],
       IFNULL(m.MaritalStatus, '')       AS [وضعیت تأهل],
       IFNULL(m.AssistanceDuration, '')  AS [مدت مساعدت (ماه)],
       IFNULL(m.Notes, '')               AS [یادداشت مهاجرت]
FROM TblMigrant m
WHERE m.CasID = @CasID;", caseId);
        }

        // ─── نمایندهٔ قانونی (فاز ۷) ────────────────────────────────────────
        // در RDLC فقط خلاصهٔ یک‌خطی چاپ می‌شود و هیچ قالبِ Word ای
        // placeholderهای {{Rep*}} را ندارد؛ پس این تنها خروجیِ کاملِ نماینده است.
        public static DataTable GetRepresentatives(int caseId)
        {
            return Query(@"
SELECT r.RepresentativeOrder                      AS [ردیف],
       IFNULL(r.FullName, '')                     AS [نام نماینده],
       IFNULL(r.RelationshipToBeneficiary, '')    AS [نسبت با ذی‌نفع],
       IFNULL(r.IdCardType, '')                   AS [نوع تذکره],
       IFNULL(r.NationalID, '')                   AS [شماره تذکره],
       IFNULL(r.Phone, '')                        AS [شماره تماس],
       IFNULL(r.SecondaryPhone, '')               AS [تماس دوم],
       IFNULL(r.Address, '')                      AS [آدرس],
       IFNULL(r.Notes, '')                        AS [یادداشت]
FROM TblCaseRepresentative r
WHERE r.CasID = @CasID AND IFNULL(r.IsActive, 1) = 1
ORDER BY r.RepresentativeOrder;", caseId);
        }

        // ─── سوابق بازدید میدانی ────────────────────────────────────────────
        public static DataTable GetFieldVisits(int caseId)
        {
            return Query(@"
SELECT v.VisitDate                    AS [تاریخ بازدید],
       IFNULL(v.VisitorName, '')      AS [بازدیدکننده],
       IFNULL(v.VisitResult, '')      AS [نتیجه],
       IFNULL(v.Recommendation, '')   AS [توصیه],
       IFNULL(v.Notes, '')            AS [یادداشت],
       (SELECT COUNT(*) FROM TblFieldVisitPhoto p WHERE p.VisitID = v.VisitID) AS [تعداد عکس]
FROM TblFieldVisit v
WHERE v.CasID = @CasID
ORDER BY v.VisitDate DESC, v.VisitID DESC;", caseId);
        }

        // ─── تأمین مالی و خیّر ──────────────────────────────────────────────
        public static DataTable GetFunding(int caseId)
        {
            return Query(@"
SELECT fs.Name                        AS [منبع تأمین مالی],
       IFNULL(s.Name, '')             AS [خیّر],
       IFNULL(cf.StartDate, '')       AS [از تاریخ],
       IFNULL(cf.EndDate, '')         AS [تا تاریخ],
       CASE WHEN cf.IsActive = 1 THEN 'فعال' ELSE 'غیرفعال' END AS [وضعیت],
       IFNULL(cf.Notes, '')           AS [یادداشت]
FROM TblCaseFunding cf
JOIN TblFundingSource fs ON fs.FundingSourceID = cf.FundingSourceID
LEFT JOIN TblSponsor s   ON s.SponsorID = cf.SponsorID
WHERE cf.CasID = @CasID
ORDER BY cf.IsActive DESC, cf.CaseFundingID DESC;", caseId);
        }

        // ─── سابقهٔ مساعدت / پرداخت ─────────────────────────────────────────
        public static DataTable GetAssistanceHistory(int caseId)
        {
            return Query(@"
SELECT a.AssistanceDate              AS [تاریخ],
       a.AssistanceType              AS [نوع کمک],
       a.Amount                      AS [مبلغ],
       IFNULL(a.Description, '')     AS [شرح],
       IFNULL(a.CreatedBy, '')       AS [ثبت‌کننده]
FROM TblAssistance a
WHERE a.CasID = @CasID
ORDER BY a.AssistanceDate DESC, a.AssistanceID DESC;", caseId);
        }

        // ─── ریزِ امتیاز آسیب‌پذیری ─────────────────────────────────────────
        public static DataTable GetVulnerabilityBreakdown(int caseId)
        {
            return Query(@"
SELECT d.CriteriaName                AS [معیار],
       d.FactValue                   AS [مقدار],
       d.ScoreValue                  AS [امتیاز],
       IFNULL(d.Explanation, '')     AS [توضیح]
FROM TblVulnerabilityScoreDetail d
JOIN TblVulnerabilityScore s ON s.ScoreID = d.ScoreID AND s.IsCurrent = 1
WHERE d.CasID = @CasID
ORDER BY d.ScoreValue DESC, d.DetailID;", caseId);
        }

        // ─── وضعیت اسناد (شاملِ تأییدیه) ────────────────────────────────────
        public static DataTable GetDocumentStatus(int caseId)
        {
            return Query(@"
SELECT IFNULL(dc.Name, IFNULL(d.DocCategory, '')) AS [دسته سند],
       IFNULL(d.DocType, '')                      AS [نوع سند],
       IFNULL(d.OriginalFileName, '')             AS [نام فایل],
       CASE WHEN IFNULL(d.IsVerified, 0) = 1 THEN 'تأیید شده' ELSE 'در انتظار تأیید' END AS [وضعیت تأیید],
       IFNULL(d.VerifiedBy, '')                   AS [تأییدکننده],
       IFNULL(d.VerifiedDate, '')                 AS [تاریخ تأیید]
FROM TblDocs d
LEFT JOIN TblDocumentCategory dc ON dc.DocumentCategoryID = d.DocumentCategoryID
WHERE d.CasID = @CasID AND IFNULL(d.IsArchived, 0) = 0
ORDER BY d.DocID;", caseId);
        }

        // ─── اسنادِ الزامیِ کم ───────────────────────────────────────────────
        // از همان کوئریِ RequiredDocumentService جدا نگه داشته نشده — عمداً
        // اینجا هم برای خروجی تکرار می‌شود چون شکلِ ستون‌ها فرق دارد؛ ولی
        // *منطقِ* «کم بودن» یکی است: تعدادِ موجود < MinCount.
        public static DataTable GetMissingDocuments(int caseId)
        {
            return Query(@"
SELECT dc.Name AS [دسته سند الزامی],
       rd.MinCount AS [حداقل لازم],
       (SELECT COUNT(*) FROM TblDocs d
         WHERE d.CasID = c.CasID
           AND d.DocumentCategoryID = dc.DocumentCategoryID
           AND IFNULL(d.IsArchived, 0) = 0) AS [موجود]
FROM TblCase c
JOIN TblRequiredDocument rd ON rd.RequestTypeID = c.RequestTypeID AND rd.IsActive = 1
JOIN TblDocumentCategory dc ON dc.DocumentCategoryID = rd.DocumentCategoryID AND dc.IsActive = 1
WHERE c.CasID = @CasID AND rd.IsMandatory = 1
  AND (SELECT COUNT(*) FROM TblDocs d
        WHERE d.CasID = c.CasID
          AND d.DocumentCategoryID = dc.DocumentCategoryID
          AND IFNULL(d.IsArchived, 0) = 0) < rd.MinCount
ORDER BY dc.SortOrder;", caseId);
        }

        // ─── عکس‌های بازدید میدانی ──────────────────────────────────────────
        // آموزش — تا امروز عکسِ بازدید در هیچ خروجی‌ای نمی‌آمد. ستونِ «مسیر
        // فایل» عمداً در جدول هست: خروجیِ اکسل به آن نیاز دارد (تا کاربر فایل
        // را پیدا کند) و سندِ چاپی از همان مسیر خودِ عکس را می‌کشد.
        public static DataTable GetFieldVisitPhotos(int caseId)
        {
            return Query(@"
SELECT v.VisitDate                    AS [تاریخ بازدید],
       IFNULL(v.VisitorName, '')      AS [بازدیدکننده],
       IFNULL(p.Description, '')      AS [توضیح],
       IFNULL(p.FilePath, '')         AS [مسیر فایل]
FROM TblFieldVisitPhoto p
JOIN TblFieldVisit v ON v.VisitID = p.VisitID
WHERE p.CasID = @CasID
ORDER BY v.VisitDate DESC, p.PhotoID;", caseId);
        }

        // ─── خلاصهٔ وضعیتِ پرونده (یک ردیف) ─────────────────────────────────
        // همان فیلدهایی که الزاماتِ Word/PDF/Excel می‌خواهند: نوع درخواست،
        // وضعیت خدمات، کامل‌بودن، امتیاز آسیب‌پذیری — با نامِ نمایشی، نه
        // شناسهٔ عددی (نکتهٔ «missing joins» در تحلیلِ RDLC).
        public static DataTable GetCaseSummary(int caseId)
        {
            return Query(@"
SELECT c.Code                              AS [کد پرونده],
       c.HeadFullName                      AS [نام سرپرست],
       IFNULL(rt.Name, IFNULL(c.RequestType, ''))    AS [نوع درخواست],
       IFNULL(ss.Name, IFNULL(c.ServiceStatus, ''))  AS [وضعیت خدمات],
       IFNULL(c.CompletionPercent, 0)      AS [درصد تکمیل],
       CASE IFNULL(c.CompletionStatusCode, '')
            WHEN 'COMPLETE'    THEN 'کامل'
            WHEN 'IN_PROGRESS' THEN 'در حال تکمیل'
            WHEN 'INCOMPLETE'  THEN 'ناقص'
            ELSE '—' END                   AS [وضعیت تکمیل],
       IFNULL(c.VulnerabilityScore, 0)     AS [امتیاز آسیب‌پذیری],
       CASE IFNULL(c.VulnerabilityBand, '')
            WHEN 'HIGH'   THEN 'پرخطر'
            WHEN 'MEDIUM' THEN 'متوسط'
            WHEN 'LOW'    THEN 'کم‌خطر'
            ELSE '—' END                   AS [سطح آسیب‌پذیری],
       IFNULL(c.SuspensionReason, '')      AS [دلیل تعلیق],
       IFNULL(c.SuspensionDate, '')        AS [تاریخ تعلیق]
FROM TblCase c
LEFT JOIN TblRequestType   rt ON rt.RequestTypeID   = c.RequestTypeID
LEFT JOIN TblServiceStatus ss ON ss.ServiceStatusID = c.ServiceStatusID
WHERE c.CasID = @CasID;", caseId);
        }

        private static DataTable Query(string sql, int caseId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CasID", caseId);
                con.Open();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }
    }
}

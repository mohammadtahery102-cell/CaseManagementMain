using CaseManagement.DAL;
using System;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // شمارنده‌های مدیریتیِ داشبورد — یک‌جا خوانده می‌شوند تا داشبورد به‌ازای
    // هر کارت یک رفت‌وبرگشتِ جدا به دیتابیس نزند.
    public class DashboardManagementMetrics
    {
        public int     MissingDocumentCases;
        public int     FieldVisitCount;
        public int     CasesWithFunding;
        public int     ActiveSponsors;
        public int     ActiveFundingSources;
        public int     AssistanceCount;
        public decimal AssistanceTotal;

        // Phase 5.5-D — سنجه‌های ریسک و کیفیت. همه از ستون‌های کش‌شدهٔ
        // ایندکس‌دار می‌آیند؛ هیچ امتیازی اینجا دوباره محاسبه نمی‌شود.
        public int    HighRiskCases;
        public int    MediumRiskCases;
        public int    LowRiskCases;
        public double AverageVulnerabilityScore;
        public int    UnverifiedDocuments;
        public int    CasesRequiringVisit;
        public int    CasesMissingFunding;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 5.5-C — سرویسِ سنجه‌های داشبورد.
    //
    // خواستهٔ صریح: «داشبورد باید سرویسِ تجمیع‌شده مصرف کند، نه اینکه منطقِ
    // کسب‌وکار داخلِ فرم جاسازی شود». پس همهٔ کوئری‌های تجمیعیِ تازه اینجا
    // هستند و FrmDashboard فقط مقدارشان را روی کارت می‌نشاند.
    //
    // نکتهٔ کارایی (از گزارشِ تأثیرِ داشبورد): سنجه‌های عددی در *یک* کوئریِ
    // مرکب جمع می‌شوند، نه هفت کوئریِ جدا — چون RefreshAll همهٔ این‌ها را
    // پشتِ‌سرِ هم صدا می‌زند و هفت رفت‌وبرگشتِ اضافه زمانِ بازخوانیِ داشبورد
    // را محسوس می‌کرد.
    //
    // فیلترِ مرکز همان قرارداد موجود است: centerId == 0 یعنی «همهٔ مراکز».
    // ═══════════════════════════════════════════════════════════════════════
    public static class DashboardMetricsService
    {
        // ─── توزیعِ نوع درخواست ─────────────────────────────────────────────
        // از نامِ مرجع (TblRequestType.Name) استفاده می‌کند نه ستونِ متنیِ
        // قدیمی، چون نوعِ درخواست از فاز ۳ هویتِ ارجاعی دارد.
        public static DataTable GetRequestTypeDistribution(int centerId)
        {
            return Query(@"
SELECT IFNULL(rt.Name, IFNULL(NULLIF(c.RequestType, ''), 'نامشخص')) AS [نوع درخواست],
       COUNT(*) AS [تعداد]
FROM TblCase c
LEFT JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID
WHERE IFNULL(c.IsArchived, 0) = 0 AND (@CID = 0 OR c.CenterID = @CID)
GROUP BY IFNULL(rt.Name, IFNULL(NULLIF(c.RequestType, ''), 'نامشخص'))
ORDER BY COUNT(*) DESC;", centerId);
        }

        // ─── توزیعِ وضعیت خدمات ─────────────────────────────────────────────
        public static DataTable GetServiceStatusDistribution(int centerId)
        {
            return Query(@"
SELECT IFNULL(ss.Name, IFNULL(NULLIF(c.ServiceStatus, ''), 'نامشخص')) AS [وضعیت خدمات],
       COUNT(*) AS [تعداد]
FROM TblCase c
LEFT JOIN TblServiceStatus ss ON ss.ServiceStatusID = c.ServiceStatusID
WHERE IFNULL(c.IsArchived, 0) = 0 AND (@CID = 0 OR c.CenterID = @CID)
GROUP BY IFNULL(ss.Name, IFNULL(NULLIF(c.ServiceStatus, ''), 'نامشخص'))
ORDER BY COUNT(*) DESC;", centerId);
        }

        // ─── توزیعِ وضعیت تکمیل ─────────────────────────────────────────────
        // از ستونِ کشِ ایندکس‌دار می‌خواند (تصمیم #۱۴)، نه محاسبهٔ زنده.
        public static DataTable GetCompletionDistribution(int centerId)
        {
            return Query(@"
SELECT CASE IFNULL(c.CompletionStatusCode, '')
            WHEN 'COMPLETE'    THEN 'کامل'
            WHEN 'IN_PROGRESS' THEN 'در حال تکمیل'
            WHEN 'INCOMPLETE'  THEN 'ناقص'
            ELSE 'محاسبه نشده' END AS [وضعیت تکمیل],
       COUNT(*) AS [تعداد]
FROM TblCase c
WHERE IFNULL(c.IsArchived, 0) = 0 AND (@CID = 0 OR c.CenterID = @CID)
GROUP BY 1
ORDER BY COUNT(*) DESC;", centerId);
        }

        // ─── توزیعِ سطحِ آسیب‌پذیری ─────────────────────────────────────────
        public static DataTable GetVulnerabilityBandDistribution(int centerId)
        {
            return Query(@"
SELECT CASE IFNULL(c.VulnerabilityBand, '')
            WHEN 'HIGH'   THEN 'پرخطر'
            WHEN 'MEDIUM' THEN 'متوسط'
            WHEN 'LOW'    THEN 'کم‌خطر'
            ELSE 'محاسبه نشده' END AS [سطح آسیب‌پذیری],
       COUNT(*) AS [تعداد]
FROM TblCase c
WHERE IFNULL(c.IsArchived, 0) = 0 AND (@CID = 0 OR c.CenterID = @CID)
GROUP BY 1
ORDER BY COUNT(*) DESC;", centerId);
        }

        // ─── توزیعِ منابعِ تأمین مالی (چند پرونده به هر منبع) ────────────────
        public static DataTable GetFundingSourceDistribution(int centerId)
        {
            return Query(@"
SELECT fs.Name AS [منبع تأمین مالی],
       COUNT(DISTINCT cf.CasID) AS [تعداد پرونده]
FROM TblCaseFunding cf
JOIN TblFundingSource fs ON fs.FundingSourceID = cf.FundingSourceID
JOIN TblCase c ON c.CasID = cf.CasID
WHERE cf.IsActive = 1 AND IFNULL(c.IsArchived, 0) = 0
  AND (@CID = 0 OR c.CenterID = @CID)
GROUP BY fs.Name
ORDER BY COUNT(DISTINCT cf.CasID) DESC;", centerId);
        }

        // ─── خیّرینِ فعال و تعدادِ پرونده‌هایشان ─────────────────────────────
        public static DataTable GetSponsorDistribution(int centerId)
        {
            return Query(@"
SELECT s.Name AS [خیّر],
       COUNT(DISTINCT cf.CasID) AS [تعداد پرونده]
FROM TblCaseFunding cf
JOIN TblSponsor s ON s.SponsorID = cf.SponsorID
JOIN TblCase c ON c.CasID = cf.CasID
WHERE cf.IsActive = 1 AND IFNULL(c.IsArchived, 0) = 0
  AND (@CID = 0 OR c.CenterID = @CID)
GROUP BY s.Name
ORDER BY COUNT(DISTINCT cf.CasID) DESC;", centerId);
        }

        // ─── شمارنده‌های مدیریتی — یک کوئریِ مرکب ────────────────────────────
        public static DashboardManagementMetrics GetManagementMetrics(int centerId)
        {
            var metrics = new DashboardManagementMetrics();

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT
  -- پرونده‌هایی که دستِ‌کم یک سندِ الزامیشان کم است.
  -- به‌صورتِ یک کوئریِ گروهی حساب می‌شود، نه حلقهٔ per-case (نکتهٔ کاراییِ
  -- گزارشِ تأثیرِ داشبورد).
  (SELECT COUNT(*) FROM TblCase c
    WHERE IFNULL(c.IsArchived, 0) = 0 AND (@CID = 0 OR c.CenterID = @CID)
      AND EXISTS (
          SELECT 1
          FROM TblRequiredDocument rd
          JOIN TblDocumentCategory dc ON dc.DocumentCategoryID = rd.DocumentCategoryID AND dc.IsActive = 1
          WHERE rd.RequestTypeID = c.RequestTypeID AND rd.IsActive = 1 AND rd.IsMandatory = 1
            AND (SELECT COUNT(*) FROM TblDocs d
                  WHERE d.CasID = c.CasID
                    AND d.DocumentCategoryID = dc.DocumentCategoryID
                    AND IFNULL(d.IsArchived, 0) = 0) < rd.MinCount)
  ) AS MissingDocCases,

  (SELECT COUNT(*) FROM TblFieldVisit v
     JOIN TblCase c ON c.CasID = v.CasID
    WHERE IFNULL(c.IsArchived, 0) = 0 AND (@CID = 0 OR c.CenterID = @CID)) AS VisitCount,

  (SELECT COUNT(DISTINCT cf.CasID) FROM TblCaseFunding cf
     JOIN TblCase c ON c.CasID = cf.CasID
    WHERE cf.IsActive = 1 AND IFNULL(c.IsArchived, 0) = 0
      AND (@CID = 0 OR c.CenterID = @CID)) AS FundedCases,

  (SELECT COUNT(*) FROM TblSponsor WHERE IsActive = 1)             AS ActiveSponsors,
  (SELECT COUNT(*) FROM TblFundingSource WHERE IsActive = 1)       AS ActiveSources,

  (SELECT COUNT(*) FROM TblAssistance a
     JOIN TblCase c ON c.CasID = a.CasID
    WHERE IFNULL(c.IsArchived, 0) = 0 AND (@CID = 0 OR c.CenterID = @CID)) AS AssistCount,

  (SELECT COALESCE(SUM(a.Amount), 0) FROM TblAssistance a
     JOIN TblCase c ON c.CasID = a.CasID
    WHERE IFNULL(c.IsArchived, 0) = 0 AND (@CID = 0 OR c.CenterID = @CID)) AS AssistTotal,

  -- Phase 5.5-D — ریسک و کیفیت. سه شمارشِ باند روی ایندکسِ
  -- IX_TblCase_VulnBand می‌نشینند و میانگین هم از همان ستونِ کش‌شده
  -- می‌آید — هیچ فراخوانیِ VulnerabilityScoreService در مسیرِ داشبورد نیست.
  (SELECT COUNT(*) FROM TblCase c WHERE IFNULL(c.IsArchived,0) = 0
     AND (@CID = 0 OR c.CenterID = @CID) AND c.VulnerabilityBand = 'HIGH')   AS HighRisk,
  (SELECT COUNT(*) FROM TblCase c WHERE IFNULL(c.IsArchived,0) = 0
     AND (@CID = 0 OR c.CenterID = @CID) AND c.VulnerabilityBand = 'MEDIUM') AS MediumRisk,
  (SELECT COUNT(*) FROM TblCase c WHERE IFNULL(c.IsArchived,0) = 0
     AND (@CID = 0 OR c.CenterID = @CID) AND c.VulnerabilityBand = 'LOW')    AS LowRisk,
  (SELECT COALESCE(AVG(c.VulnerabilityScore), 0) FROM TblCase c
    WHERE IFNULL(c.IsArchived,0) = 0 AND (@CID = 0 OR c.CenterID = @CID)
      AND c.VulnerabilityScore IS NOT NULL) AS AvgScore,

  (SELECT COUNT(*) FROM TblDocs d
     JOIN TblCase c ON c.CasID = d.CasID
    WHERE IFNULL(d.IsArchived,0) = 0 AND IFNULL(d.IsVerified,0) = 0
      AND IFNULL(c.IsArchived,0) = 0 AND (@CID = 0 OR c.CenterID = @CID)) AS UnverifiedDocs,

  -- «نیازمندِ بازدید» فقط برای نوع‌هایی معنا دارد که پرچمِ
  -- RequiresFieldVisit را روشن کرده‌اند (پیش‌فرض خاموش است، پس تا وقتی
  -- مدیر روشنش نکند این عدد صفر می‌ماند — همان قرارداد فاز ۵).
  (SELECT COUNT(*) FROM TblCase c
     JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID
    WHERE IFNULL(c.IsArchived,0) = 0 AND (@CID = 0 OR c.CenterID = @CID)
      AND IFNULL(rt.RequiresFieldVisit,0) = 1
      AND NOT EXISTS (SELECT 1 FROM TblFieldVisit v WHERE v.CasID = c.CasID)) AS NeedVisit,

  (SELECT COUNT(*) FROM TblCase c
     JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID
    WHERE IFNULL(c.IsArchived,0) = 0 AND (@CID = 0 OR c.CenterID = @CID)
      AND IFNULL(rt.RequiresFunding,0) = 1
      AND NOT EXISTS (SELECT 1 FROM TblCaseFunding cf
                       WHERE cf.CasID = c.CasID AND cf.IsActive = 1)) AS NeedFunding;", con))
            {
                cmd.Parameters.AddWithValue("@CID", centerId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        metrics.MissingDocumentCases = ToInt(dr["MissingDocCases"]);
                        metrics.FieldVisitCount = ToInt(dr["VisitCount"]);
                        metrics.CasesWithFunding = ToInt(dr["FundedCases"]);
                        metrics.ActiveSponsors = ToInt(dr["ActiveSponsors"]);
                        metrics.ActiveFundingSources = ToInt(dr["ActiveSources"]);
                        metrics.AssistanceCount = ToInt(dr["AssistCount"]);
                        metrics.AssistanceTotal = dr["AssistTotal"] == DBNull.Value
                            ? 0m : Convert.ToDecimal(dr["AssistTotal"]);

                        metrics.HighRiskCases = ToInt(dr["HighRisk"]);
                        metrics.MediumRiskCases = ToInt(dr["MediumRisk"]);
                        metrics.LowRiskCases = ToInt(dr["LowRisk"]);
                        metrics.AverageVulnerabilityScore = dr["AvgScore"] == DBNull.Value
                            ? 0d : Convert.ToDouble(dr["AvgScore"]);
                        metrics.UnverifiedDocuments = ToInt(dr["UnverifiedDocs"]);
                        metrics.CasesRequiringVisit = ToInt(dr["NeedVisit"]);
                        metrics.CasesMissingFunding = ToInt(dr["NeedFunding"]);
                    }
                }
            }

            return metrics;
        }

        private static int ToInt(object value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static DataTable Query(string sql, int centerId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CID", centerId);
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

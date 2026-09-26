using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // GeoAnalyticsService — تنها نقطه‌ای که «مرکز فرماندهی آماری» SQL می‌زند.
    //
    // آموزش — چرا این سرویس وجود دارد: همان قاعده‌ای که در فاز ۵.۵-C برای
    // DashboardMetricsService گذاشته شد؛ منطقِ تجمیع نباید داخلِ فرم باشد.
    // فرم فقط جدول/عدد را می‌گیرد و نشان می‌دهد.
    //
    // آموزش — قاعدهٔ طلایی رده‌بندی (PROJECT_CONTEXT): شمارشِ پرونده همیشه از
    // TblCase.RequestTypeID می‌آید. به TblOrphan/TblDisability/TblMigrant برای
    // شمارشِ رده‌بندی join نمی‌کنیم چون ۱:۰..۱ هستند و شمارش را دوگانه می‌کنند.
    //
    // کارایی: هر متد یک رفت‌وبرگشت است. RegionRollup که سنگین‌ترین است، به‌جای
    // ۳۴ کوئریِ per-province، یک کوئری با زیرکوئری‌های از-پیش-تجمیع‌شده می‌زند
    // که روی IX_TblCase_Province / IX_TblCase_ProvinceDistrict می‌نشیند.
    // ═══════════════════════════════════════════════════════════════════════
    public static class GeoAnalyticsService
    {
        // سطحِ جغرافیایی. سطح ۱ ولایت، سطح ۲ ولسوالی.
        public enum GeoLevel { Province = 1, District = 2 }

        // معیارهای نقشهٔ حرارتی — کاربر می‌تواند بینشان سوئیچ کند بدون کوئری تازه.
        public const string MetricCases = "CASES";
        public const string MetricOrphans = "ORPHANS";
        public const string MetricDisabled = "DISABLED";
        public const string MetricSeverePoverty = "SEVERE_POVERTY";
        public const string MetricVulnerability = "VULNERABILITY";
        public const string MetricNoSponsor = "NO_SPONSOR";
        public const string MetricMembers = "MEMBERS";
        public const string MetricAssistance = "ASSISTANCE";
        public const string MetricActive = "ACTIVE";

        public static readonly string[][] HeatMetrics =
        {
            new[] { MetricCases,         "تعداد پرونده"          },
            new[] { MetricMembers,       "تعداد اعضای خانواده"   },
            new[] { MetricOrphans,       "تعداد یتیم"            },
            new[] { MetricDisabled,      "تعداد معلول"           },
            new[] { MetricActive,        "پرونده‌های فعال"       },
            new[] { MetricSeverePoverty, "فقر شدید"              },
            new[] { MetricVulnerability, "میانگین آسیب‌پذیری"    },
            new[] { MetricNoSponsor,     "فاقد حامی"             },
            new[] { MetricAssistance,    "مجموع کمک‌ها"          }
        };

        public static string HeatMetricDisplay(string code)
        {
            foreach (string[] m in HeatMetrics)
                if (string.Equals(m[0], code, StringComparison.OrdinalIgnoreCase)) return m[1];
            return code ?? "";
        }

        // ───────────────────────────────────────────────────────────────────
        // ردیفِ تجمیعیِ یک منطقه. همان چیزی که هم روی نقشه رنگ می‌شود، هم در
        // جدولِ «۱۰ منطقهٔ برتر» و هم در خروجی‌های اکسل/ورد می‌نشیند.
        // ───────────────────────────────────────────────────────────────────
        public sealed class RegionStats
        {
            public string Region = "";        // نامِ ولایت یا ولسوالی
            public string Parent = "";        // ولایتِ والد (فقط در سطح ولسوالی)

            public int Cases;
            public int Families;              // پرونده‌های ریشهٔ گروهِ خانوادگی
            public int Members;
            public int Male, Female, UnknownGender;
            public int Child, Teen, Youth, Adult, Elder;

            public int Orphans, Disabled, Migrants, Unsupported, BadlySupported, Elderly;
            public int ActiveCases, ApplicantCases, SuspendedCases;

            public int SeverePoverty;         // «خیلی شدید» + «شدید»
            public int HighRisk, MediumRisk, LowRisk;
            public double VulnAvg, VulnMin, VulnMax;

            public int WithSponsor, WithoutSponsor, PendingSponsor, EndedSponsor;
            public decimal AssistanceTotal;
            public int AssistanceCount;

            public double MetricValue(string metric)
            {
                switch (metric)
                {
                    case MetricMembers: return Members;
                    case MetricOrphans: return Orphans;
                    case MetricDisabled: return Disabled;
                    case MetricActive: return ActiveCases;
                    case MetricSeverePoverty: return SeverePoverty;
                    case MetricVulnerability: return VulnAvg;
                    case MetricNoSponsor: return WithoutSponsor;
                    case MetricAssistance: return (double)AssistanceTotal;
                    default: return Cases;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Q1 — تجمیعِ منطقه‌ای. قلبِ ماژول.
        //
        // آموزش — چرا زیرکوئری‌های جدا به‌جای چند LEFT JOIN: اگر TblFamily و
        // TblAssistance و TblCaseFunding مستقیم به TblCase جوین شوند، حاصل‌ضربِ
        // کارتزینی می‌سازند و COUNT(*) چند برابر می‌شود. زیرکوئریِ همبسته
        // (correlated scalar subquery) روی ایندکسِ CasID هر کدام می‌نشیند و
        // عددِ درست می‌دهد. با ۲۰٬۰۰۰ پرونده این همچنان یک اسکنِ ایندکس‌دار است.
        // ═══════════════════════════════════════════════════════════════════
        public static List<RegionStats> GetRegionRollup(GeoFilter filter, GeoLevel level)
        {
            if (filter == null) filter = new GeoFilter();

            // در سطحِ ولسوالی، ولایت باید مشخص باشد وگرنه نام‌های هم‌نام از
            // ولایت‌های مختلف روی هم می‌افتند (مثلاً «سروبی» هم در کابل است هم
            // در پکتیکا).
            string regionExpr = level == GeoLevel.District
                ? "TRIM(IFNULL(c.District, ''))"
                : "TRIM(IFNULL(c.Province, ''))";

            string ageExpr = GeoFilter.AgeExpr("f");
            string genderExpr = "TRIM(IFNULL(f.Gender, ''))";

            string sql = @"
SELECT " + regionExpr + @" AS Region,
       TRIM(IFNULL(c.Province, '')) AS Parent,

       COUNT(*)                                                           AS Cases,
       SUM(CASE WHEN IFNULL(c.FamilyGroupID, c.CasID) = c.CasID THEN 1 ELSE 0 END) AS Families,

       SUM(CASE WHEN IFNULL(rt.Code,'') = 'ORPHAN'                THEN 1 ELSE 0 END) AS Orphans,
       SUM(CASE WHEN IFNULL(rt.Code,'') = 'DISABLED'              THEN 1 ELSE 0 END) AS Disabled,
       SUM(CASE WHEN IFNULL(rt.Code,'') = 'MIGRANT'               THEN 1 ELSE 0 END) AS Migrants,
       SUM(CASE WHEN IFNULL(rt.Code,'') = 'UNSUPPORTED_CHILD'     THEN 1 ELSE 0 END) AS Unsupported,
       SUM(CASE WHEN IFNULL(rt.Code,'') = 'BADLY_SUPPORTED_CHILD' THEN 1 ELSE 0 END) AS BadlySupported,
       SUM(CASE WHEN IFNULL(rt.Code,'') = 'ELDERLY'               THEN 1 ELSE 0 END) AS Elderly,

       SUM(CASE WHEN IFNULL(ss.Code,'') = 'ACTIVE'    THEN 1 ELSE 0 END) AS ActiveCases,
       SUM(CASE WHEN IFNULL(ss.Code,'') = 'APPLICANT' THEN 1 ELSE 0 END) AS ApplicantCases,
       SUM(CASE WHEN IFNULL(ss.Code,'') IN ('SUSPENDED','TEMPORARILY_SUSPENDED') THEN 1 ELSE 0 END) AS SuspendedCases,

       SUM(CASE WHEN IFNULL(c.EconomicPriority,'') IN ('خیلی شدید','شدید') THEN 1 ELSE 0 END) AS SeverePoverty,

       SUM(CASE WHEN IFNULL(c.VulnerabilityBand,'') = 'HIGH'   THEN 1 ELSE 0 END) AS HighRisk,
       SUM(CASE WHEN IFNULL(c.VulnerabilityBand,'') = 'MEDIUM' THEN 1 ELSE 0 END) AS MediumRisk,
       SUM(CASE WHEN IFNULL(c.VulnerabilityBand,'') = 'LOW'    THEN 1 ELSE 0 END) AS LowRisk,
       AVG(c.VulnerabilityScore) AS VulnAvg,
       MIN(c.VulnerabilityScore) AS VulnMin,
       MAX(c.VulnerabilityScore) AS VulnMax,

       SUM(CASE WHEN EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID AND cf.IsActive = 1 AND cf.SponsorID IS NOT NULL)
                THEN 1 ELSE 0 END) AS WithSponsor,
       SUM(CASE WHEN NOT EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID)
                THEN 1 ELSE 0 END) AS WithoutSponsor,
       SUM(CASE WHEN EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID AND cf.IsActive = 1 AND cf.SponsorID IS NULL)
                 AND NOT EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID AND cf.IsActive = 1 AND cf.SponsorID IS NOT NULL)
                THEN 1 ELSE 0 END) AS PendingSponsor,
       SUM(CASE WHEN EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID)
                 AND NOT EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID AND cf.IsActive = 1)
                THEN 1 ELSE 0 END) AS EndedSponsor,

       SUM(IFNULL((SELECT SUM(a.Amount)  FROM TblAssistance a WHERE a.CasID = c.CasID), 0)) AS AssistanceTotal,
       SUM(IFNULL((SELECT COUNT(*)       FROM TblAssistance a WHERE a.CasID = c.CasID), 0)) AS AssistanceCount,

       SUM(IFNULL((SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID), 0)) AS Members,
       SUM(IFNULL((SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID
                    AND " + genderExpr + @" IN ('مرد','مذکر','پسر','آقا')), 0)) AS Male,
       SUM(IFNULL((SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID
                    AND " + genderExpr + @" IN ('زن','مؤنث','مونث','دختر','خانم')), 0)) AS Female,
       SUM(IFNULL((SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID
                    AND " + genderExpr + @" NOT IN ('مرد','مذکر','پسر','آقا','زن','مؤنث','مونث','دختر','خانم')), 0)) AS UnknownGender,

       SUM(IFNULL((SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID AND " + ageExpr + @" BETWEEN  0 AND 12), 0)) AS ChildCnt,
       SUM(IFNULL((SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID AND " + ageExpr + @" BETWEEN 13 AND 17), 0)) AS TeenCnt,
       SUM(IFNULL((SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID AND " + ageExpr + @" BETWEEN 18 AND 30), 0)) AS YouthCnt,
       SUM(IFNULL((SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID AND " + ageExpr + @" BETWEEN 31 AND 59), 0)) AS AdultCnt,
       SUM(IFNULL((SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID AND " + ageExpr + @" >= 60), 0))             AS ElderCnt

FROM TblCase c
LEFT JOIN TblRequestType   rt ON rt.RequestTypeID   = c.RequestTypeID
LEFT JOIN TblServiceStatus ss ON ss.ServiceStatusID = c.ServiceStatusID
WHERE 1 = 1" + filter.BuildWhere("c") + @"
  AND " + regionExpr + @" <> ''
GROUP BY Region" + (level == GeoLevel.District ? ", Parent" : "") + @"
ORDER BY Cases DESC;";

            List<RegionStats> list = new List<RegionStats>();
            DataTable dt = Query(sql, filter);

            foreach (DataRow r in dt.Rows)
            {
                RegionStats s = new RegionStats();
                s.Region = Str(r, "Region");
                s.Parent = Str(r, "Parent");
                s.Cases = Int(r, "Cases");
                s.Families = Int(r, "Families");
                s.Orphans = Int(r, "Orphans");
                s.Disabled = Int(r, "Disabled");
                s.Migrants = Int(r, "Migrants");
                s.Unsupported = Int(r, "Unsupported");
                s.BadlySupported = Int(r, "BadlySupported");
                s.Elderly = Int(r, "Elderly");
                s.ActiveCases = Int(r, "ActiveCases");
                s.ApplicantCases = Int(r, "ApplicantCases");
                s.SuspendedCases = Int(r, "SuspendedCases");
                s.SeverePoverty = Int(r, "SeverePoverty");
                s.HighRisk = Int(r, "HighRisk");
                s.MediumRisk = Int(r, "MediumRisk");
                s.LowRisk = Int(r, "LowRisk");
                s.VulnAvg = Dbl(r, "VulnAvg");
                s.VulnMin = Dbl(r, "VulnMin");
                s.VulnMax = Dbl(r, "VulnMax");
                s.WithSponsor = Int(r, "WithSponsor");
                s.WithoutSponsor = Int(r, "WithoutSponsor");
                s.PendingSponsor = Int(r, "PendingSponsor");
                s.EndedSponsor = Int(r, "EndedSponsor");
                s.AssistanceTotal = Dec(r, "AssistanceTotal");
                s.AssistanceCount = Int(r, "AssistanceCount");
                s.Members = Int(r, "Members");
                s.Male = Int(r, "Male");
                s.Female = Int(r, "Female");
                s.UnknownGender = Int(r, "UnknownGender");
                s.Child = Int(r, "ChildCnt");
                s.Teen = Int(r, "TeenCnt");
                s.Youth = Int(r, "YouthCnt");
                s.Adult = Int(r, "AdultCnt");
                s.Elder = Int(r, "ElderCnt");
                list.Add(s);
            }
            return list;
        }

        // مجموعِ همهٔ مناطق در یک ردیف — کارت‌های بالای فرم و سربرگِ گزارش.
        public static RegionStats Aggregate(List<RegionStats> rows, string title)
        {
            RegionStats t = new RegionStats { Region = title };
            if (rows == null || rows.Count == 0) return t;

            double sumScore = 0; int scoreRegions = 0;
            t.VulnMin = double.MaxValue;

            foreach (RegionStats s in rows)
            {
                t.Cases += s.Cases; t.Families += s.Families; t.Members += s.Members;
                t.Male += s.Male; t.Female += s.Female; t.UnknownGender += s.UnknownGender;
                t.Child += s.Child; t.Teen += s.Teen; t.Youth += s.Youth;
                t.Adult += s.Adult; t.Elder += s.Elder;
                t.Orphans += s.Orphans; t.Disabled += s.Disabled; t.Migrants += s.Migrants;
                t.Unsupported += s.Unsupported; t.BadlySupported += s.BadlySupported; t.Elderly += s.Elderly;
                t.ActiveCases += s.ActiveCases; t.ApplicantCases += s.ApplicantCases; t.SuspendedCases += s.SuspendedCases;
                t.SeverePoverty += s.SeverePoverty;
                t.HighRisk += s.HighRisk; t.MediumRisk += s.MediumRisk; t.LowRisk += s.LowRisk;
                t.WithSponsor += s.WithSponsor; t.WithoutSponsor += s.WithoutSponsor;
                t.PendingSponsor += s.PendingSponsor; t.EndedSponsor += s.EndedSponsor;
                t.AssistanceTotal += s.AssistanceTotal; t.AssistanceCount += s.AssistanceCount;

                // میانگینِ وزنیِ آسیب‌پذیری: میانگینِ میانگین‌ها نادرست است، پس
                // با تعدادِ پرونده وزن داده می‌شود.
                if (s.VulnAvg > 0 && s.Cases > 0) { sumScore += s.VulnAvg * s.Cases; scoreRegions += s.Cases; }
                if (s.VulnMax > t.VulnMax) t.VulnMax = s.VulnMax;
                if (s.VulnMin > 0 && s.VulnMin < t.VulnMin) t.VulnMin = s.VulnMin;
            }

            t.VulnAvg = scoreRegions > 0 ? sumScore / scoreRegions : 0;
            if (t.VulnMin == double.MaxValue) t.VulnMin = 0;
            return t;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Q2 — تفکیکِ نوع پرونده (تعداد + درصد + شناسهٔ Drill-Down)
        // ═══════════════════════════════════════════════════════════════════
        public sealed class BreakdownRow
        {
            public string Label = "";
            public int Count;
            public double Percent;
            public int DrillId;          // شناسهٔ مرجع برای فیلترِ FrmCase
            public string DrillCode = ""; // کدِ متنی (باند آسیب‌پذیری، حمایت، …)
        }

        public static List<BreakdownRow> GetRequestTypeBreakdown(GeoFilter filter)
        {
            return Breakdown(filter, @"
SELECT IFNULL(rt.Name, 'نامشخص') AS Label, c.RequestTypeID AS DrillId, '' AS DrillCode, COUNT(*) AS Cnt
FROM TblCase c
LEFT JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID
WHERE 1 = 1" + filter.BuildWhere("c") + @"
GROUP BY c.RequestTypeID, Label
ORDER BY Cnt DESC;");
        }

        public static List<BreakdownRow> GetServiceStatusBreakdown(GeoFilter filter)
        {
            return Breakdown(filter, @"
SELECT IFNULL(ss.Name, IFNULL(NULLIF(TRIM(c.ServiceStatus), ''), 'نامشخص')) AS Label,
       IFNULL(c.ServiceStatusID, 0) AS DrillId, '' AS DrillCode, COUNT(*) AS Cnt
FROM TblCase c
LEFT JOIN TblServiceStatus ss ON ss.ServiceStatusID = c.ServiceStatusID
WHERE 1 = 1" + filter.BuildWhere("c") + @"
GROUP BY DrillId, Label
ORDER BY Cnt DESC;");
        }

        // ترتیبِ سطوح اقتصادی باید معنایی باشد (خیلی شدید → باثبات)، نه بر
        // اساسِ تعداد؛ وگرنه مدیر نمی‌تواند شیبِ توزیع را ببیند.
        public static List<BreakdownRow> GetEconomicPriorityBreakdown(GeoFilter filter)
        {
            List<BreakdownRow> rows = Breakdown(filter, @"
SELECT CASE WHEN TRIM(IFNULL(c.EconomicPriority,'')) = '' THEN 'ثبت‌نشده'
            ELSE TRIM(c.EconomicPriority) END AS Label,
       0 AS DrillId,
       CASE WHEN TRIM(IFNULL(c.EconomicPriority,'')) = '' THEN ''
            ELSE TRIM(c.EconomicPriority) END AS DrillCode,
       COUNT(*) AS Cnt
FROM TblCase c
WHERE 1 = 1" + filter.BuildWhere("c") + @"
GROUP BY Label, DrillCode;");

            List<BreakdownRow> ordered = new List<BreakdownRow>();
            foreach (string level in GeoFilter.EconomicPriorities)
            {
                BreakdownRow found = rows.Find(delegate (BreakdownRow r) { return r.Label == level; });
                ordered.Add(found ?? new BreakdownRow { Label = level, DrillCode = level });
            }
            BreakdownRow unset = rows.Find(delegate (BreakdownRow r) { return r.Label == "ثبت‌نشده"; });
            if (unset != null) ordered.Add(unset);
            return ordered;
        }

        public static List<BreakdownRow> GetVulnerabilityBreakdown(GeoFilter filter)
        {
            return Breakdown(filter, @"
SELECT CASE IFNULL(c.VulnerabilityBand,'')
            WHEN 'HIGH'   THEN 'پرخطر'
            WHEN 'MEDIUM' THEN 'متوسط'
            WHEN 'LOW'    THEN 'کم‌خطر'
            ELSE 'محاسبه‌نشده' END AS Label,
       0 AS DrillId,
       IFNULL(c.VulnerabilityBand,'') AS DrillCode,
       COUNT(*) AS Cnt
FROM TblCase c
WHERE 1 = 1" + filter.BuildWhere("c") + @"
GROUP BY Label, DrillCode
ORDER BY Cnt DESC;");
        }

        public static List<BreakdownRow> GetSponsorshipBreakdown(GeoFilter filter)
        {
            return Breakdown(filter, @"
SELECT CASE
         WHEN EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID AND cf.IsActive = 1 AND cf.SponsorID IS NOT NULL) THEN 'دارای حامی'
         WHEN EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID AND cf.IsActive = 1) THEN 'در انتظار حامی'
         WHEN EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID) THEN 'حمایت قطع‌شده'
         ELSE 'فاقد حامی' END AS Label,
       0 AS DrillId,
       CASE
         WHEN EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID AND cf.IsActive = 1 AND cf.SponsorID IS NOT NULL) THEN '" + GeoFilter.SponsorHas + @"'
         WHEN EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID AND cf.IsActive = 1) THEN '" + GeoFilter.SponsorPending + @"'
         WHEN EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = c.CasID) THEN '" + GeoFilter.SponsorEnded + @"'
         ELSE '" + GeoFilter.SponsorNone + @"' END AS DrillCode,
       COUNT(*) AS Cnt
FROM TblCase c
WHERE 1 = 1" + filter.BuildWhere("c") + @"
GROUP BY Label, DrillCode
ORDER BY Cnt DESC;");
        }

        public static List<BreakdownRow> GetCompletionBreakdown(GeoFilter filter)
        {
            return Breakdown(filter, @"
SELECT CASE IFNULL(c.CompletionStatusCode,'')
            WHEN 'COMPLETE'    THEN 'کامل'
            WHEN 'IN_PROGRESS' THEN 'در حال تکمیل'
            WHEN 'INCOMPLETE'  THEN 'ناقص'
            ELSE 'محاسبه‌نشده' END AS Label,
       0 AS DrillId, IFNULL(c.CompletionStatusCode,'') AS DrillCode, COUNT(*) AS Cnt
FROM TblCase c
WHERE 1 = 1" + filter.BuildWhere("c") + @"
GROUP BY Label, DrillCode
ORDER BY Cnt DESC;");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Q6 — آمارِ کمک‌های مالی برای محدودهٔ فیلترشده.
        // ═══════════════════════════════════════════════════════════════════
        public sealed class AssistanceStats
        {
            public int Count;
            public decimal Total, Average, Max, Min;
            public int CasesReceiving;
        }

        public static AssistanceStats GetAssistanceStats(GeoFilter filter)
        {
            DataTable dt = Query(@"
SELECT COUNT(*)                  AS Cnt,
       IFNULL(SUM(a.Amount), 0)  AS Total,
       IFNULL(AVG(a.Amount), 0)  AS Avg_,
       IFNULL(MAX(a.Amount), 0)  AS Max_,
       IFNULL(MIN(a.Amount), 0)  AS Min_,
       COUNT(DISTINCT a.CasID)   AS Cases_
FROM TblAssistance a
JOIN TblCase c ON c.CasID = a.CasID
WHERE (@GfAsstType = '' OR a.AssistanceType = @GfAsstType)" + filter.BuildWhere("c") + ";", filter);

            AssistanceStats s = new AssistanceStats();
            if (dt.Rows.Count > 0)
            {
                DataRow r = dt.Rows[0];
                s.Count = Int(r, "Cnt");
                s.Total = Dec(r, "Total");
                s.Average = Dec(r, "Avg_");
                s.Max = Dec(r, "Max_");
                s.Min = Dec(r, "Min_");
                s.CasesReceiving = Int(r, "Cases_");
            }
            return s;
        }

        // ═══════════════════════════════════════════════════════════════════
        // نمایهٔ سنی/جنسیتیِ اعضا — «آمارِ عضو» است، نه رده‌بندیِ پرونده.
        // (قاعدهٔ صریحِ PROJECT_CONTEXT: باید با همین برچسب نشان داده شود.)
        // ═══════════════════════════════════════════════════════════════════
        public static DataTable GetAgeGenderMatrix(GeoFilter filter)
        {
            return Query(@"
SELECT " + GeoFilter.AgeGroupNameExpr("f") + @" AS [گروه سنی],
       SUM(CASE WHEN TRIM(IFNULL(f.Gender,'')) IN ('مرد','مذکر','پسر','آقا')          THEN 1 ELSE 0 END) AS [مرد],
       SUM(CASE WHEN TRIM(IFNULL(f.Gender,'')) IN ('زن','مؤنث','مونث','دختر','خانم') THEN 1 ELSE 0 END) AS [زن],
       COUNT(*) AS [مجموع]
FROM TblFamily f
JOIN TblCase c ON c.CasID = f.CasID
WHERE 1 = 1" + filter.BuildWhere("c") + @"
GROUP BY [گروه سنی]
ORDER BY [مجموع] DESC;", filter);
        }

        // ═══════════════════════════════════════════════════════════════════
        // مناطقی که در فهرست مرجع نیستند — تشخیصِ داده‌های ناهماهنگ (ریسک R3).
        // ═══════════════════════════════════════════════════════════════════
        public static DataTable GetUnknownRegions(GeoFilter filter)
        {
            return Query(@"
SELECT CASE WHEN TRIM(IFNULL(c.Province,'')) = '' THEN '(خالی)' ELSE TRIM(c.Province) END AS [ولایت ثبت‌شده],
       COUNT(*) AS [تعداد پرونده]
FROM TblCase c
WHERE TRIM(IFNULL(c.Province,'')) NOT IN (SELECT Value FROM TblLookup WHERE Category = 'Province')
" + filter.BuildWhere("c") + @"
GROUP BY [ولایت ثبت‌شده]
ORDER BY [تعداد پرونده] DESC;", filter);
        }

        // ─── زیرساخت ────────────────────────────────────────────────────────
        private static List<BreakdownRow> Breakdown(GeoFilter filter, string sql)
        {
            DataTable dt = Query(sql, filter);
            List<BreakdownRow> rows = new List<BreakdownRow>();
            int total = 0;
            foreach (DataRow r in dt.Rows) total += Int(r, "Cnt");

            foreach (DataRow r in dt.Rows)
            {
                int count = Int(r, "Cnt");
                rows.Add(new BreakdownRow
                {
                    Label = Str(r, "Label"),
                    Count = count,
                    Percent = total > 0 ? count * 100.0 / total : 0,
                    DrillId = Int(r, "DrillId"),
                    DrillCode = Str(r, "DrillCode")
                });
            }
            return rows;
        }

        // نتیجهٔ تفکیک را به جدولِ نمایشی/خروجی تبدیل می‌کند.
        public static DataTable ToDataTable(List<BreakdownRow> rows, string labelHeader)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add(labelHeader, typeof(string));
            dt.Columns.Add("تعداد", typeof(int));
            dt.Columns.Add("درصد", typeof(string));
            foreach (BreakdownRow r in rows)
                dt.Rows.Add(r.Label, r.Count, r.Percent.ToString("0.0", CultureInfo.InvariantCulture) + "٪");
            return dt;
        }

        private static DataTable Query(string sql, GeoFilter filter)
        {
            DataTable table = new DataTable();
            DatabaseHelper db = new DatabaseHelper();
            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
                {
                    filter.BindParameters(cmd);
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                        table.Load(reader);
                }
            }
            return table;
        }

        private static string Str(DataRow r, string col)
        {
            if (!r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return "";
            return Convert.ToString(r[col]);
        }

        private static int Int(DataRow r, string col)
        {
            if (!r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return 0;
            return Convert.ToInt32(r[col], CultureInfo.InvariantCulture);
        }

        private static double Dbl(DataRow r, string col)
        {
            if (!r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return 0;
            return Convert.ToDouble(r[col], CultureInfo.InvariantCulture);
        }

        private static decimal Dec(DataRow r, string col)
        {
            if (!r.Table.Columns.Contains(col) || r[col] == DBNull.Value) return 0m;
            return Convert.ToDecimal(r[col], CultureInfo.InvariantCulture);
        }
    }
}

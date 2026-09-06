using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using CaseManagement.DAL;
using Microsoft.Reporting.WinForms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // خروجیِ بی‌واسطه‌ی «الگوی قدیمی» (گزارشِ RDLC) — بدون بازکردنِ پنجره‌ی
    // پیش‌نمایش. دقیقاً همان داده‌سازی و منبعِ تعبیه‌شده‌ای که FrmCaseReport.cs
    // برای پیش‌نمایشِ تعاملی استفاده می‌کند؛ اینجا فقط LocalReport مستقیماً
    // Render می‌شود تا برای خروجیِ تک‌پرونده‌ای و خروجیِ جمعی هم قابل استفاده
    // باشد (ReportViewer/Form برای رندرِ بی‌واسطه لازم نیست).
    // ─────────────────────────────────────────────────────────────────────────
    public static class RdlcExportHelper
    {
        // ═══════════════════════════════════════════════════════════════════
        // Phase 5.5-D — تنها تعریفِ کوئریِ CaseData برای RptFullCase.rdlc.
        //
        // آموزش — باگِ واقعی که این فاز پیدا کرد: گزارش دو مسیرِ تغذیه داشت —
        // FrmCaseReport (نمایشِ روی صفحه) و همین کلاس (خروجیِ PDF/Word/دسته‌ای).
        // فاز ۷ ستون‌های محاسبه‌شدهٔ Rep1Summary/Rep2Summary را به RDLC و به
        // مسیرِ *اول* اضافه کرد ولی نه به دومی؛ چون RDLC آن‌ها را به‌عنوان
        // Field اعلام کرده، خروجیِ PDF/Word/دسته‌ای ستونی را می‌خواست که در
        // DataTable وجود نداشت. روی صفحه سالم دیده می‌شد و فقط خروجی خراب بود
        // — به‌همین دلیل بی‌صدا ماند.
        //
        // راه‌حل: کوئری یک‌جا تعریف می‌شود و هر دو مسیر همین را صدا می‌زنند.
        // افزودنِ ستونِ تازه از این پس خودبه‌خود به هر دو مسیر می‌رسد و
        // واگراییِ دوباره ساختاراً ممکن نیست.
        // ═══════════════════════════════════════════════════════════════════
        public static string CaseDataSql
        {
            get
            {
                return @"
SELECT c.*,
       " + RepresentativeSummarySql(1) + @" AS Rep1Summary,
       " + RepresentativeSummarySql(2) + @" AS Rep2Summary,
       -- Phase 5.5-D — خلاصه‌های وضعیت. همه از ستون‌های کش‌شده/جدول‌های
       -- موجود می‌آیند؛ هیچ محاسبهٔ تازه‌ای در زمانِ گزارش انجام نمی‌شود.
       CASE IFNULL(c.CompletionStatusCode, '')
            WHEN 'COMPLETE'    THEN 'کامل'
            WHEN 'IN_PROGRESS' THEN 'در حال تکمیل'
            WHEN 'INCOMPLETE'  THEN 'ناقص'
            ELSE '' END AS CompletionStatusText,
       CASE IFNULL(c.VulnerabilityBand, '')
            WHEN 'HIGH'   THEN 'پرخطر'
            WHEN 'MEDIUM' THEN 'متوسط'
            WHEN 'LOW'    THEN 'کم‌خطر'
            ELSE '' END AS VulnerabilityBandText,
       (SELECT GROUP_CONCAT(x.Label, ' ، ') FROM (
            SELECT TRIM(fs.Name ||
                   CASE WHEN COALESCE(s.Name, '') <> '' THEN ' (' || s.Name || ')' ELSE '' END) AS Label
            FROM TblCaseFunding cf
            JOIN TblFundingSource fs ON fs.FundingSourceID = cf.FundingSourceID
            LEFT JOIN TblSponsor s ON s.SponsorID = cf.SponsorID
            WHERE cf.CasID = c.CasID AND cf.IsActive = 1
            ORDER BY fs.Name) x) AS FundingSummary,
       (SELECT CAST(SUM(CASE WHEN IFNULL(d.IsVerified,0) = 1 THEN 1 ELSE 0 END) AS TEXT)
               || ' / ' || CAST(COUNT(*) AS TEXT)
          FROM TblDocs d WHERE d.CasID = c.CasID AND IFNULL(d.IsArchived,0) = 0)
       AS VerifiedDocsSummary,
       (SELECT CAST(COUNT(*) AS TEXT) FROM TblFieldVisit v WHERE v.CasID = c.CasID)
       AS FieldVisitCountText
FROM TblCase c
WHERE c.CasID = @CasID";
            }
        }

        // خلاصهٔ یک نماینده: «نام — نسبت — تلفن». عددِ slot از کدِ خودمان
        // می‌آید (نه ورودیِ کاربر)، پس درجِ مستقیمش راهی برای تزریق باز
        // نمی‌کند؛ بقیهٔ کوئری پارامتری است.
        private static string RepresentativeSummarySql(int slot)
        {
            return @"
(SELECT TRIM(COALESCE(r.FullName, '')
          || CASE WHEN COALESCE(r.RelationshipToBeneficiary, '') <> ''
                  THEN ' — ' || r.RelationshipToBeneficiary ELSE '' END
          || CASE WHEN COALESCE(r.Phone, '') <> ''
                  THEN ' — ' || r.Phone ELSE '' END)
   FROM TblCaseRepresentative r
  WHERE r.CasID = c.CasID AND r.IsActive = 1 AND r.RepresentativeOrder = " + slot + ")";
        }

        // Phase 5.5-E — رندر با فرمتِ دلخواه. برای وارسیِ خودکارِ *محتوای*
        // گزارش لازم است: با فرمتِ CSV می‌توان مطمئن شد یک فیلد واقعاً چاپ
        // می‌شود، نه صرفاً اینکه رندر بدونِ خطا تمام شده.
        public static byte[] RenderCase(int caseId, string format)
        {
            return Render(caseId, format);
        }

        public static void ExportCaseToPdf(int caseId, string outputPdfPath)
        {
            byte[] bytes = Render(caseId, "PDF");
            File.WriteAllBytes(outputPdfPath, bytes);
        }

        public static void ExportCaseToWord(int caseId, string outputDocPath)
        {
            byte[] bytes = Render(caseId, "WORD");
            File.WriteAllBytes(outputDocPath, bytes);
        }

        private static byte[] Render(int caseId, string format)
        {
            if (caseId <= 0)
                throw new ArgumentException("شناسه پرونده معتبر نیست.");

            var db = new DatabaseHelper();
            CenterGuard.EnsureCaseAccess(db, caseId);

            var localReport = new LocalReport();
            localReport.ReportEmbeddedResource = "CaseManagement.RptFullCase.rdlc";
            localReport.DataSources.Add(new ReportDataSource("CaseData",
                GetDataTable(db, caseId, CaseDataSql)));
            localReport.DataSources.Add(new ReportDataSource("FamilyData",
                GetDataTable(db, caseId, "SELECT * FROM TblFamily WHERE CasID = @CasID ORDER BY FamID")));
            localReport.DataSources.Add(new ReportDataSource("DocsData",
                GetDataTable(db, caseId, "SELECT * FROM TblDocs WHERE CasID = @CasID ORDER BY DocID")));

            string mimeType, encoding, fileNameExtension;
            string[] streams;
            Warning[] warnings;
            return localReport.Render(format, null, out mimeType, out encoding,
                out fileNameExtension, out streams, out warnings);
        }

        private static DataTable GetDataTable(DatabaseHelper db, int caseId, string query)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            {
                cmd.Parameters.AddWithValue("@CasID", caseId);
                con.Open();
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    DataTable table = new DataTable();
                    table.Load(reader);
                    return table;
                }
            }
        }
    }
}

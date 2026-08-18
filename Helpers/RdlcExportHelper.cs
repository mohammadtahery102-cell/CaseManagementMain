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
                GetDataTable(db, caseId, "SELECT * FROM TblCase WHERE CasID = @CasID")));
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

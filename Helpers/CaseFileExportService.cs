using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // Phase 5.5-C — خروجیِ «پروندهٔ کامل» به اکسل و چاپ.
    //
    // چرا فایل و کلاسِ تازه و نه تغییرِ ExcelReportExporter:
    // آن کلاس گزارشِ *چندپرونده‌ای* است (همهٔ پرونده‌ها در یک فایل). چیزی که
    // این فاز می‌خواهد پروندهٔ *یک* مستفید با همهٔ بخش‌هایش است — دو نیازِ
    // متفاوت با دو شکلِ داده. افزودنِ این به آن کلاس، هر دو را پیچیده می‌کرد.
    //
    // همهٔ داده از CaseExportDataProvider می‌آید؛ اینجا هیچ SQLای نیست — پس
    // وقتی فازِ RDLC همان provider را مصرف کند، خروجی‌ها قطعاً هم‌داستان‌اند.
    //
    // ⚠ محدودیتِ شناخته‌شده: ClosedXML زیرِ میزبانِ آزمون با خطای بایندِ
    // System.Memory می‌افتد (مسئلهٔ محیط، نه برنامه). پس مسیرِ اکسل با آزمونِ
    // خودکار پوشش داده نمی‌شود و باید دستی وارسی شود.
    // ═══════════════════════════════════════════════════════════════════════
    public static class CaseFileExportService
    {
        // ترتیبِ بخش‌ها عمداً همان ترتیبِ روایتِ پرونده است: چیستی، سپس
        // اسناد، سپس آنچه بر آن گذشته.
        private static List<KeyValuePair<string, DataTable>> BuildSections(int caseId)
        {
            return new List<KeyValuePair<string, DataTable>>
            {
                Section("خلاصه پرونده",      CaseExportDataProvider.GetCaseSummary(caseId)),
                // فاز ۵.۵-E — سه بخشِ تازه که پوششِ خروجی را کامل می‌کنند.
                // ترتیب از منطقِ پرونده می‌آید: هویت و وضعیت، بعد افراد، بعد اسناد.
                Section("اطلاعات معلولیت",   CaseExportDataProvider.GetDisabilityInfo(caseId)),
                Section("اطلاعات ایتام",     CaseExportDataProvider.GetOrphanInfo(caseId)),
                Section("اطلاعات مهاجرت",    CaseExportDataProvider.GetMigrantInfo(caseId)),
                Section("اعضای خانواده",     CaseExportDataProvider.GetFamilyMembers(caseId)),
                Section("نماینده قانونی",    CaseExportDataProvider.GetRepresentatives(caseId)),
                Section("وضعیت اسناد",       CaseExportDataProvider.GetDocumentStatus(caseId)),
                Section("اسناد ناقص",        CaseExportDataProvider.GetMissingDocuments(caseId)),
                Section("امتیاز آسیب‌پذیری", CaseExportDataProvider.GetVulnerabilityBreakdown(caseId)),
                Section("تأمین مالی",        CaseExportDataProvider.GetFunding(caseId)),
                Section("بازدید میدانی",     CaseExportDataProvider.GetFieldVisits(caseId)),
                Section("سابقه مساعدت",      CaseExportDataProvider.GetAssistanceHistory(caseId)),
                // تایم‌لاین عمداً آخر است: الزامِ مصوب می‌گوید تاریخچه در
                // *انتهای* خروجی بیاید.
                Section("تایم‌لاین",         CaseExportDataProvider.GetTimeline(caseId))
            };
        }

        private static KeyValuePair<string, DataTable> Section(string title, DataTable table)
        {
            return new KeyValuePair<string, DataTable>(title, table);
        }

        // ─── اکسل ───────────────────────────────────────────────────────────
        public static void ExportCaseToExcel(int caseId, string outputPath)
        {
            if (caseId <= 0) throw new ArgumentException("شناسه پرونده معتبر نیست.");

            using (var workbook = new XLWorkbook())
            {
                foreach (var section in BuildSections(caseId))
                    AddSheet(workbook, section.Key, section.Value);

                workbook.SaveAs(outputPath);
            }
        }

        // هم‌الگوی ExcelReportExporter.AddTableSheet — عمداً تکرارِ کوچک و نه
        // عمومی‌سازیِ آن متدِ private، چون تغییرِ کلاسِ کارکنندهٔ گزارشِ
        // چندپرونده‌ای ریسکِ بی‌دلیل داشت.
        private static void AddSheet(XLWorkbook workbook, string sheetName, DataTable table)
        {
            IXLWorksheet ws = workbook.Worksheets.Add(SafeSheetName(sheetName));
            ws.RightToLeft = true;

            if (table == null || table.Rows.Count == 0)
            {
                ws.Cell(1, 1).Value = "داده‌ای برای نمایش وجود ندارد.";
                ws.Columns().AdjustToContents();
                return;
            }

            IXLTable excelTable = ws.Cell(1, 1).InsertTable(table, SafeTableName(sheetName), true);
            excelTable.Theme = XLTableTheme.TableStyleMedium2;

            ws.SheetView.FreezeRows(1);
            ws.Row(1).Style.Font.Bold = true;
            ws.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.RangeUsed().Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.RangeUsed().Style.Alignment.WrapText = true;
            ws.Columns().AdjustToContents(1, 60);
        }

        // اکسل نامِ شیت را حداکثر ۳۱ نویسه و بدونِ نویسه‌های خاص می‌پذیرد.
        private static string SafeSheetName(string name)
        {
            string clean = (name ?? "Sheet").Replace("/", "-").Replace("\\", "-")
                                            .Replace("*", "").Replace("?", "")
                                            .Replace("[", "").Replace("]", "").Replace(":", "");
            return clean.Length > 31 ? clean.Substring(0, 31) : clean;
        }

        private static string SafeTableName(string name)
        {
            return SafeSheetName(name).Replace(" ", "_").Replace("‌", "_");
        }

        // ─── چاپ ────────────────────────────────────────────────────────────
        // PrintHelper.PrintDataTable هر بار یک جدول چاپ می‌کند؛ پس بخش‌ها
        // پشتِ‌سرِ هم چاپ می‌شوند. بخشِ خالی رد می‌شود تا کاربر صفحهٔ سفید
        // نگیرد — مگر خلاصهٔ پرونده که همیشه باید چاپ شود.
        public static void PrintFullCase(IWin32Window owner, int caseId, string caseTitle)
        {
            if (caseId <= 0) return;

            foreach (var section in BuildSections(caseId))
            {
                DataTable table = section.Value;
                bool isSummary = section.Key == "خلاصه پرونده";

                if (!isSummary && (table == null || table.Rows.Count == 0)) continue;

                PrintHelper.PrintDataTable(owner,
                    (caseTitle ?? "پرونده") + " — " + section.Key, table);
            }
        }

        // فهرستِ بخش‌ها برای فرم‌هایی که می‌خواهند کاربر انتخاب کند کدام
        // بخش چاپ/خروجی شود (فازِ بعد).
        public static List<string> GetSectionNames()
        {
            var names = new List<string>();
            foreach (var section in BuildSections(0))
                names.Add(section.Key);
            return names;
        }
    }
}

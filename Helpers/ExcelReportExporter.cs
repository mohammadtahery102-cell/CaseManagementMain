using CaseManagement.DAL;
using ClosedXML.Excel;
using System;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace CaseManagement.Helpers
{
    public class ExcelReportExporter
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        public void ExportFullReport(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("مسیر خروجی اکسل مشخص نیست.");

            string folder = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            int cid = SecurityContext.CenterFilterId;
            DataTable cases      = GetDataTable(GetCasesQuery(), cid);
            DataTable caseFamily = GetDataTable(GetCaseFamilyQuery(true), cid);
            DataTable family     = GetDataTable(GetCaseFamilyQuery(false), cid);
            DataTable docs       = GetDataTable(GetDocsQuery(), cid);

            // تصمیم «ذخیره میلادی، نمایش شمسی»: تاریخ‌های خروجی اکسل شمسی نمایش
            // داده می‌شوند؛ چون این خروجی یک‌بار مصرف است (نه منبع بازخوانی)،
            // تبدیل مستقیم روی DataTable قبل از نوشتن در شیت انجام می‌شود.
            PersianDateHelper.ConvertDateColumnsToPersian(cases, "تاریخ تشکیل", "تاریخ سروی");
            PersianDateHelper.ConvertDateColumnsToPersian(caseFamily, "تاریخ تولد");
            PersianDateHelper.ConvertDateColumnsToPersian(family, "تاریخ تولد");

            using (XLWorkbook workbook = new XLWorkbook())
            {
                AddSummarySheet(workbook, cases, family, docs);
                AddTableSheet(workbook, "پرونده ها", cases);
                AddTableSheet(workbook, "پرونده و خانواده", caseFamily);
                AddTableSheet(workbook, "اعضای خانواده", family);
                AddTableSheet(workbook, "اسناد", docs);

                workbook.SaveAs(outputPath);
            }
        }

        private void AddSummarySheet(XLWorkbook workbook, DataTable cases, DataTable family, DataTable docs)
        {
            IXLWorksheet ws = workbook.Worksheets.Add("خلاصه");
            ws.RightToLeft = true;

            ws.Cell(1, 1).Value = "خلاصه گزارش کامل";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;

            ws.Cell(3, 1).Value = "تعداد کل پرونده‌ها";
            ws.Cell(3, 2).Value = cases.Rows.Count;

            ws.Cell(4, 1).Value = "تعداد کل اعضای خانواده";
            ws.Cell(4, 2).Value = family.Rows.Count;

            ws.Cell(5, 1).Value = "تعداد کل اسناد";
            ws.Cell(5, 2).Value = docs.Rows.Count;

            ws.Cell(7, 1).Value = "تاریخ ساخت گزارش";
            ws.Cell(7, 2).Value = PersianDateHelper.ToPersianDateTimeString(DateTime.Now);

            IXLRange range = ws.Range(3, 1, 7, 2);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Columns().AdjustToContents();
        }

        private void AddTableSheet(XLWorkbook workbook, string sheetName, DataTable table)
        {
            IXLWorksheet ws = workbook.Worksheets.Add(sheetName);
            ws.RightToLeft = true;

            if (table.Rows.Count == 0)
            {
                ws.Cell(1, 1).Value = "داده‌ای برای نمایش وجود ندارد.";
                ws.Columns().AdjustToContents();
                return;
            }

            IXLTable excelTable = ws.Cell(1, 1).InsertTable(table, sheetName.Replace(" ", "_"), true);
            excelTable.Theme = XLTableTheme.TableStyleMedium2;

            ws.SheetView.FreezeRows(1);
            ws.Row(1).Style.Font.Bold = true;
            ws.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.RangeUsed().Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.RangeUsed().Style.Alignment.WrapText = true;

            ws.Columns().AdjustToContents(1, 60);
        }

        private DataTable GetDataTable(string query, int cid)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                cmd.CommandTimeout = 120;
                cmd.Parameters.AddWithValue("@CID", cid);

                DataTable dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        private string GetCasesQuery()
        {
            return @"
                SELECT
                    c.CasID AS [شناسه پرونده],
                    c.FormNo AS [شماره فرم],
                    c.Code AS [کد اختصاصی],
                    c.CaseNo AS [شماره پرونده],
                    c.CaseDate AS [تاریخ تشکیل],
                    c.Zone AS [زون],
                    c.Province AS [ولایت],
                    c.District AS [ولسوالی],
                    c.RequestType AS [نوع درخواست],
                    c.PriorityLevel AS [اولویت بندی],
                    c.HeadFullName AS [نام سرپرست],
                    c.HeadFatherName AS [نام پدر سرپرست],
                    c.HeadSadat AS [سیادت سرپرست],
                    c.Religion AS [مذهب],
                    c.HeadTazkiraNo AS [شماره تذکره سرپرست],
                    c.HeadOriginalResidence AS [سکونت اصلی],
                    c.HeadCurrentResidence AS [سکونت فعلی],
                    c.RelationshipToFamily AS [نسبت با اعضا],
                    c.Phone AS [شماره تماس],
                    c.RelativePhone AS [شماره تماس اقارب],
                    c.CoveredByOrg AS [تحت پوشش],
                    c.Job AS [شغل],
                    c.Skill AS [مهارت],
                    c.DisabilityDegree AS [درجه معلولیت],
                    c.DisabilityType AS [نوع معلولیت],
                    c.MigrationCardType AS [نوع برگه مهاجرت],
                    c.MaritalStatus AS [وضعیت تأهل],
                    c.Surveyors AS [سروی کننده‌ها],
                    c.SurveyDate AS [تاریخ سروی],
                    c.LocationAddress AS [آدرس لوکیشن],
                    c.EducationLevel AS [تحصیلات],
                    c.ServiceStatus AS [وضعیت خدمات],
                    c.UrgentSituation AS [شرح وضعیت فوری],
                    CASE WHEN NULLIF(c.PhotoPath, '') IS NULL THEN 'ندارد' ELSE 'دارد' END AS [عکس سرپرست],
                    CASE WHEN NULLIF(c.FamilyPhotoPath, '') IS NULL THEN 'ندارد' ELSE 'دارد' END AS [عکس جمعی],
                    c.PhotoPath AS [مسیر عکس سرپرست],
                    c.FamilyPhotoPath AS [مسیر عکس جمعی],
                    (SELECT COUNT(1) FROM TblFamily f WHERE f.CasID = c.CasID) AS [تعداد اعضای خانواده],
                    (SELECT COUNT(1) FROM TblDocs d WHERE d.CasID = c.CasID) AS [تعداد اسناد]
                FROM TblCase c
                WHERE (@CID = 0 OR c.CenterID = @CID)
                ORDER BY c.CasID DESC";
        }

        private string GetCaseFamilyQuery(bool includeCasesWithoutFamily)
        {
            string joinType = includeCasesWithoutFamily ? "LEFT JOIN" : "INNER JOIN";

            return @"
                SELECT
                    c.CasID AS [شناسه پرونده],
                    c.FormNo AS [شماره فرم],
                    c.Code AS [کد اختصاصی],
                    c.CaseNo AS [شماره پرونده],
                    c.Province AS [ولایت],
                    c.District AS [ولسوالی],
                    c.HeadFullName AS [نام سرپرست],
                    c.Phone AS [شماره تماس سرپرست],
                    f.FamID AS [شناسه عضو],
                    f.MemberName AS [نام عضو],
                    f.MemberFatherName AS [نام پدر عضو],
                    f.MemberTazkiraNo AS [شماره تذکره عضو],
                    f.BirthDate AS [تاریخ تولد],
                    CASE
                        WHEN f.BirthDate IS NULL THEN NULL
                        ELSE CAST((julianday('now') - julianday(f.BirthDate)) / 365.25 AS INTEGER)
                    END AS [سن],
                    f.MemberSadat AS [سیادت عضو],
                    f.Gender AS [جنسیت],
                    f.PhysicalStatus AS [وضعیت جسمی],
                    f.HasDisability AS [معلولیت],
                    f.MemberDisabilityDegree AS [درجه معلولیت عضو],
                    f.MemberEducation AS [تحصیلات عضو],
                    f.SchoolName AS [نام مکتب],
                    f.GradeLevel AS [صنف],
                    f.UniversityName AS [نام دانشگاه],
                    f.StudyYear AS [سال تحصیل],
                    f.Major AS [رشته],
                    f.StudyField AS [حوزه/بخش تحصیل],
                    f.OfficialStatus AS [وضعیت رسمی],
                    f.Skill AS [مهارت عضو],
                    f.LeaveReason AS [دلیل ترک تحصیل],
                    f.Details AS [توضیحات عضو],
                    CASE WHEN NULLIF(f.MemberPhotoPath, '') IS NULL THEN 'ندارد' ELSE 'دارد' END AS [عکس عضو],
                    f.MemberPhotoPath AS [مسیر عکس عضو]
                FROM TblCase c
                " + joinType + @" TblFamily f ON f.CasID = c.CasID
                WHERE (@CID = 0 OR c.CenterID = @CID)
                ORDER BY c.CasID DESC, f.FamID";
        }

        private string GetDocsQuery()
        {
            return @"
                SELECT
                    c.CasID AS [شناسه پرونده],
                    c.FormNo AS [شماره فرم],
                    c.Code AS [کد اختصاصی],
                    c.CaseNo AS [شماره پرونده],
                    c.HeadFullName AS [نام سرپرست],
                    c.Phone AS [شماره تماس سرپرست],
                    d.DocID AS [شناسه سند],
                    d.DocType AS [نوع سند],
                    d.OriginalFileName AS [نام فایل اصلی],
                    d.RelatedCaseRef AS [مرجع مرتبط],
                    d.DocDescription AS [توضیحات سند],
                    d.DocFilePath AS [مسیر فایل سند]
                FROM TblCase c
                INNER JOIN TblDocs d ON d.CasID = c.CasID
                WHERE (@CID = 0 OR c.CenterID = @CID)
                ORDER BY c.CasID DESC, d.DocID";
        }
    }
}

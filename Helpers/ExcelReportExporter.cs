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

        // serviceStatus: فیلتر «وضعیت خدمات» روی هر چهار شیت خروجی. مقدار خالی
        // (پیش‌فرض) یعنی «همه وضعیت‌ها» و رفتار دقیقاً مثل قبل می‌ماند.
        //
        // پرونده‌های بایگانی‌شده (IsArchived = 1) از هر چهار شیت کنار گذاشته
        // می‌شوند — همان قاعده‌ای که داشبورد (CaseFilterSql) و گزارش‌ساز از قبل
        // رعایت می‌کردند و فقط همین خروجی از آن جا مانده بود. پرونده‌ی بایگانی
        // فقط از صفحه‌ی «بایگانی» دیده می‌شود.
        // filter: فیلترهای پیشرفته‌ی اختیاری (ولایت/ولسوالی/نوع خانواده/بازه‌ی
        // تاریخ ثبت/فعال-غیرفعال). null یعنی هیچ‌کدام اعمال نشود — رفتار قبلی.
        public void ExportFullReport(string outputPath, string serviceStatus = "", ReportFilterCriteria filter = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("مسیر خروجی اکسل مشخص نیست.");

            string folder = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            string svc = (serviceStatus ?? "").Trim();

            int cid = SecurityContext.CenterFilterId;
            DataTable cases      = GetDataTable(GetCasesQuery(), cid, svc, filter);
            DataTable caseFamily = GetDataTable(GetCaseFamilyQuery(true), cid, svc, filter);
            DataTable family     = GetDataTable(GetCaseFamilyQuery(false), cid, svc, filter);
            DataTable docs       = GetDataTable(GetDocsQuery(), cid, svc, filter);

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

        private DataTable GetDataTable(string query, int cid, string serviceStatus, ReportFilterCriteria filter)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                cmd.CommandTimeout = 120;
                cmd.Parameters.AddWithValue("@CID", cid);
                cmd.Parameters.AddWithValue("@Svc", serviceStatus ?? "");
                cmd.Parameters.AddWithValue("@Province", filter?.Province ?? "");
                // آموزش — رفعِ اشکالِ گزارش‌شده: ولسوالی حالا از یک کمبوی
                // آبشاری (لیستِ ثابتِ Helpers.AfghanGeoData) انتخاب می‌شود، نه
                // تایپِ آزاد؛ پس مقایسه‌ی دقیق (=) درست‌تر از LIKE است — با
                // LIKE ممکن بود یک ولسوالی که نامش زیررشته‌ی نام ولسوالیِ
                // دیگری است هم اشتباهاً مطابقت بخورد.
                cmd.Parameters.AddWithValue("@District", filter?.District ?? "");
                cmd.Parameters.AddWithValue("@FamilyType", filter?.FamilyType ?? "");
                cmd.Parameters.AddWithValue("@DateFrom", filter?.RegistrationDateFrom.HasValue == true
                    ? filter.RegistrationDateFrom.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "");
                cmd.Parameters.AddWithValue("@DateTo", filter?.RegistrationDateTo.HasValue == true
                    ? filter.RegistrationDateTo.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "");
                // ActiveOnly: 0=فقط فعال، 1=فقط غیرفعال، -1=هردو (بدون محدودیت).
                // وقتی فیلتری داده نشده (filter == null، یعنی فراخوانی قدیمی)، رفتار
                // قبلی حفظ می‌شود: همیشه فقط فعال (IsArchived=0). وقتی کاربر از دیالوگِ
                // جدید صراحتاً «همه» را انتخاب کند، هر دو نمایش داده می‌شوند.
                int activeOnlyParam = filter == null ? 0 : (filter.ActiveOnly == true ? 0 : (filter.ActiveOnly == false ? 1 : -1));
                cmd.Parameters.AddWithValue("@ActiveOnly", activeOnlyParam);
                cmd.Parameters.AddWithValue("@MinMembers", filter?.MinMemberCount ?? -1);
                cmd.Parameters.AddWithValue("@MaxMembers", filter?.MaxMemberCount ?? -1);

                DataTable dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        // شرط مشترکِ فیلترهای پیشرفته که به انتهای هر سه کوئری اضافه می‌شود.
        // alias: نامِ مستعارِ جدول TblCase در آن کوئری (همیشه c).
        private const string AdvancedFilterSql = @"
                  AND (@Province = '' OR c.Province = @Province)
                  AND (@District = '' OR c.District = @District)
                  AND (@FamilyType = '' OR c.RequestType = @FamilyType)
                  AND (@DateFrom = '' OR c.CaseDate >= @DateFrom)
                  AND (@DateTo = '' OR c.CaseDate <= @DateTo)
                  AND (@ActiveOnly = -1 OR c.IsArchived = @ActiveOnly)
                  AND (@MinMembers = -1 OR (SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID) >= @MinMembers)
                  AND (@MaxMembers = -1 OR (SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = c.CasID) <= @MaxMembers)";

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
                    c.PriorityLevel AS [اولویت بندی اقتصادی],
                    c.HeadFullName AS [نام سرپرست],
                    c.HeadFatherName AS [نام پدر سرپرست],
                    c.HeadSadat AS [سیادت سرپرست],
                    c.Religion AS [مذهب],
                    c.HeadIdCardType AS [نوع تذکره سرپرست],
                    c.HeadTazkiraNo AS [شماره تذکره سرپرست],
                    c.HeadOriginalResidence AS [سکونت اصلی],
                    c.HeadCurrentResidence AS [سکونت فعلی],
                    c.RelationshipToFamily AS [نسبت با اعضا],
                    c.Phone AS [شماره تماس],
                    c.RelativePhone AS [شماره تماس اقارب],
                    c.CoveredByOrg AS [تحت پوشش دیگر مؤسسات],
                    c.CoveredByOrgNames AS [اسامی مؤسسات تحت پوشش],
                    c.Job AS [شغل],
                    c.Skill AS [مهارت],
                    c.DisabilityDegree AS [درجه معلولیت],
                    c.DisabilityType AS [نوع معلولیت],
                    c.PhysicalStatusNotes AS [یادداشت وضعیت جسمی],
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
                    -- آموزش — ستون‌های «مسیر عکس» عمداً از خروجی حذف شده‌اند:
                    -- مسیر فایل روی دیسکِ یک نصبِ خاص است، برای گیرنده‌ی فایل
                    -- اکسل معنایی ندارد و ساختار پوشه‌های داخلی را لو می‌دهد.
                    -- ستون‌های «دارد/ندارد» بالا همان اطلاعاتِ مفید را می‌رسانند.
                    (SELECT COUNT(1) FROM TblFamily f WHERE f.CasID = c.CasID) AS [تعداد اعضای خانواده],
                    -- آموزش — به‌درخواست کاربر: تا نقشِ همه‌ی اعضا بازبینی نشده،
                    -- عدد «تعداد ایتام» به‌جای صفرِ گمراه‌کننده، «نیاز به تعیین
                    -- نقش» نشان می‌دهد (دقیقاً همان قاعده‌ی کارت شناسایی —
                    -- CardService.BuildCardData / HasUnassignedMemberRoles).
                    CASE
                        WHEN EXISTS (
                            SELECT 1 FROM TblFamily fu
                            WHERE fu.CasID = c.CasID AND (fu.MemberRole IS NULL OR fu.MemberRole = '')
                        ) THEN 'نیاز به تعیین نقش'
                        ELSE CAST((SELECT COUNT(1) FROM TblFamily fo WHERE fo.CasID = c.CasID AND fo.MemberRole = 'یتیم') AS TEXT)
                    END AS [تعداد ایتام],
                    (SELECT COUNT(1) FROM TblDocs d WHERE d.CasID = c.CasID) AS [تعداد اسناد]
                FROM TblCase c
                WHERE (@CID = 0 OR c.CenterID = @CID)
                  AND (@Svc = '' OR c.ServiceStatus = @Svc)" + AdvancedFilterSql + @"
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
                    -- نوع درخواستِ پرونده روی هر سطرِ عضو تکرار می‌شود تا در
                    -- گزارشِ اعضا بتوان ایتام را از سایر بخش‌ها جدا کرد.
                    c.RequestType AS [نوع درخواست],
                    f.FamID AS [شناسه عضو],
                    f.MemberName AS [نام عضو],
                    f.MemberFatherName AS [نام پدر عضو],
                    f.MemberIdCardType AS [نوع تذکره عضو],
                    f.MemberTazkiraNo AS [شماره تذکره عضو],
                    f.BirthDate AS [تاریخ تولد],
                    CASE
                        WHEN f.BirthDate IS NULL THEN NULL
                        ELSE CAST((julianday('now') - julianday(f.BirthDate)) / 365.25 AS INTEGER)
                    END AS [سن],
                    f.MemberSadat AS [سیادت عضو],
                    f.Gender AS [جنسیت],
                    f.MemberRole AS [نقش عضو],
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
                    -- ستون «مسیر عکس عضو» عمداً حذف شده (توضیح در GetCasesQuery).
                    CASE WHEN NULLIF(f.MemberPhotoPath, '') IS NULL THEN 'ندارد' ELSE 'دارد' END AS [عکس عضو]
                FROM TblCase c
                " + joinType + @" TblFamily f ON f.CasID = c.CasID
                WHERE (@CID = 0 OR c.CenterID = @CID)
                  AND (@Svc = '' OR c.ServiceStatus = @Svc)" + AdvancedFilterSql + @"
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
                  AND (@Svc = '' OR c.ServiceStatus = @Svc)" + AdvancedFilterSql + @"
                ORDER BY c.CasID DESC, d.DocID";
        }
    }
}

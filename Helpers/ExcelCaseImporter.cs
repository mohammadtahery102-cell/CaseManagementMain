using CaseManagement.DAL;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace CaseManagement.Helpers
{
    public class ExcelImportResult
    {
        public int Inserted { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; private set; }

        public ExcelImportResult()
        {
            Errors = new List<string>();
        }
    }

    public class ExcelCaseImporter
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        public ExcelImportResult ImportCases(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("فایل اکسل پیدا نشد.", filePath);

            // آموزش — رفع باگ امنیتی چندمرکزی: قبلاً در حالت «همه مراکز»
            // (SuperAdmin بدون انتخاب مرکز مشخص) همه ردیف‌های Import بی‌صدا
            // به CenterID=1 متصل می‌شدند، یعنی پرونده‌های وارد شده می‌توانستند
            // در مرکز اشتباه ثبت شوند و برای مرکز واقعی‌شان نامرئی بمانند.
            // حالا در این حالت Import صراحتاً رد می‌شود و از کاربر می‌خواهد
            // اول یک مرکز مشخص را از داشبورد انتخاب کند.
            if (SecurityContext.CurrentCenterId <= 0)
                throw new Exception(
                    "برای Import پرونده باید ابتدا یک مرکز مشخص (نه «همه مراکز») از داشبورد انتخاب کنید.");

            ExcelImportResult result = new ExcelImportResult();

            using (XLWorkbook workbook = new XLWorkbook(filePath))
            {
                IXLWorksheet sheet = workbook.Worksheet(1);
                Dictionary<string, int> columns = ReadHeaders(sheet);
                int lastRow = sheet.LastRowUsed() == null ? 0 : sheet.LastRowUsed().RowNumber();

                using (SQLiteConnection con = db.GetConnection())
                {
                    con.Open();

                    for (int rowNumber = 2; rowNumber <= lastRow; rowNumber++)
                    {
                        try
                        {
                            string code = ReadCell(sheet, columns, rowNumber, "Code", "کد اختصاصی", "کد");
                            string headFullName = ReadCell(sheet, columns, rowNumber, "HeadFullName", "نام سرپرست", "نام سرپرست و تخلص");

                            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(headFullName))
                            {
                                result.Skipped++;
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(code))
                                code = "Imported-" + Guid.NewGuid().ToString("N").Substring(0, 8);

                            if (IsCodeExists(con, code))
                            {
                                result.Skipped++;
                                continue;
                            }

                            InsertCase(con, sheet, columns, rowNumber, code, headFullName);
                            result.Inserted++;
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add("ردیف " + rowNumber + ": " + ex.Message);
                        }
                    }
                }
            }

            AuditLogger.Log("Import Excel", "TblCase", 0, "", "Inserted=" + result.Inserted + ", Skipped=" + result.Skipped);
            return result;
        }

        private Dictionary<string, int> ReadHeaders(IXLWorksheet sheet)
        {
            Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            IXLRow headerRow = sheet.Row(1);

            foreach (IXLCell cell in headerRow.CellsUsed())
            {
                string header = cell.GetString().Trim();

                if (!string.IsNullOrWhiteSpace(header) && !columns.ContainsKey(header))
                    columns.Add(header, cell.Address.ColumnNumber);
            }

            return columns;
        }

        private void InsertCase(
            SQLiteConnection con,
            IXLWorksheet sheet,
            Dictionary<string, int> columns,
            int rowNumber,
            string code,
            string headFullName)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO TblCase
(
    FormNo, Code, CaseNo, CaseDate,
    Zone, Province, District, RequestType, PriorityLevel,
    HeadFullName, HeadFatherName, HeadSadat, Religion, HeadIdCardType, HeadTazkiraNo,
    HeadOriginalResidence, HeadCurrentResidence, RelationshipToFamily,
    Phone, RelativePhone, CoveredByOrg, CoveredByOrgNames, Job, Skill,
    DisabilityDegree, DisabilityType, MigrationCardType, MaritalStatus,
    Surveyors, SurveyDate, LocationAddress, EducationLevel, ServiceStatus, UrgentSituation,
    PhotoPath, FamilyPhotoPath, CenterID, GlobalID
)
VALUES
(
    @FormNo, @Code, @CaseNo, @CaseDate,
    @Zone, @Province, @District, @RequestType, @PriorityLevel,
    @HeadFullName, @HeadFatherName, @HeadSadat, @Religion, @HeadIdCardType, @HeadTazkiraNo,
    @HeadOriginalResidence, @HeadCurrentResidence, @RelationshipToFamily,
    @Phone, @RelativePhone, @CoveredByOrg, @CoveredByOrgNames, @Job, @Skill,
    @DisabilityDegree, @DisabilityType, @MigrationCardType, @MaritalStatus,
    @Surveyors, @SurveyDate, @LocationAddress, @EducationLevel, @ServiceStatus, @UrgentSituation,
    '', '', @CenterID,
    lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
    lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6)))
);
SELECT last_insert_rowid();", con))
            {
                AddInt(cmd, "@FormNo", GetNextFormNo(con));
                AddText(cmd, "@Code", code);
                AddText(cmd, "@CaseNo", ReadCell(sheet, columns, rowNumber, "CaseNo", "شماره پرونده"));
                AddDate(cmd, "@CaseDate", ReadDate(sheet, columns, rowNumber, "CaseDate", "تاریخ تشکیل"));
                AddText(cmd, "@Zone", ReadCell(sheet, columns, rowNumber, "Zone", "زون"));
                AddText(cmd, "@Province", ReadCell(sheet, columns, rowNumber, "Province", "ولایت"));
                AddText(cmd, "@District", ReadCell(sheet, columns, rowNumber, "District", "ولسوالی"));
                AddText(cmd, "@RequestType", ReadCell(sheet, columns, rowNumber, "RequestType", "نوع درخواست"));
                // آموزش — عنوان‌های قدیمی عمداً کنارِ عنوان جدید نگه داشته شده‌اند:
                // فایل‌های اکسلی که پیش از تغییر نام این دو فیلد ساخته شده‌اند
                // باید همچنان بدون خطا وارد شوند.
                AddText(cmd, "@PriorityLevel", ReadCell(sheet, columns, rowNumber, "PriorityLevel", "اولویت بندی اقتصادی", "اولویت‌بندی اقتصادی", "اولویت بندی", "اولویت‌بندی"));
                AddText(cmd, "@HeadFullName", headFullName);
                AddText(cmd, "@HeadFatherName", ReadCell(sheet, columns, rowNumber, "HeadFatherName", "نام پدر سرپرست"));
                AddText(cmd, "@HeadSadat", ReadCell(sheet, columns, rowNumber, "HeadSadat", "سیادت سرپرست"));
                AddText(cmd, "@Religion", ReadCell(sheet, columns, rowNumber, "Religion", "مذهب"));
                AddText(cmd, "@HeadIdCardType", ReadCell(sheet, columns, rowNumber, "HeadIdCardType", "نوع تذکره سرپرست", "نوع تذکره"));
                AddText(cmd, "@HeadTazkiraNo", ReadCell(sheet, columns, rowNumber, "HeadTazkiraNo", "شماره تذکره سرپرست"));
                AddText(cmd, "@HeadOriginalResidence", ReadCell(sheet, columns, rowNumber, "HeadOriginalResidence", "سکونت اصلی"));
                AddText(cmd, "@HeadCurrentResidence", ReadCell(sheet, columns, rowNumber, "HeadCurrentResidence", "سکونت فعلی"));
                AddText(cmd, "@RelationshipToFamily", ReadCell(sheet, columns, rowNumber, "RelationshipToFamily", "نسبت با اعضا"));
                AddText(cmd, "@Phone", ReadCell(sheet, columns, rowNumber, "Phone", "شماره تماس"));
                AddText(cmd, "@RelativePhone", ReadCell(sheet, columns, rowNumber, "RelativePhone", "شماره تماس اقارب"));
                AddText(cmd, "@CoveredByOrg", ReadCell(sheet, columns, rowNumber, "CoveredByOrg", "تحت پوشش دیگر مؤسسات", "تحت پوشش"));
                AddText(cmd, "@CoveredByOrgNames", ReadCell(sheet, columns, rowNumber, "CoveredByOrgNames", "اسامی مؤسسات تحت پوشش"));
                AddText(cmd, "@Job", ReadCell(sheet, columns, rowNumber, "Job", "شغل"));
                AddText(cmd, "@Skill", ReadCell(sheet, columns, rowNumber, "Skill", "مهارت"));
                AddText(cmd, "@DisabilityDegree", ReadCell(sheet, columns, rowNumber, "DisabilityDegree", "درجه معلولیت"));
                AddText(cmd, "@DisabilityType", ReadCell(sheet, columns, rowNumber, "DisabilityType", "نوع معلولیت"));
                AddText(cmd, "@MigrationCardType", ReadCell(sheet, columns, rowNumber, "MigrationCardType", "نوع برگه مهاجرت"));
                AddText(cmd, "@MaritalStatus", ReadCell(sheet, columns, rowNumber, "MaritalStatus", "وضعیت تأهل"));
                AddText(cmd, "@Surveyors", ReadCell(sheet, columns, rowNumber, "Surveyors", "سروی کننده‌ها", "سروی‌کننده‌ها"));
                AddDate(cmd, "@SurveyDate", ReadDate(sheet, columns, rowNumber, "SurveyDate", "تاریخ سروی"));
                AddText(cmd, "@LocationAddress", ReadCell(sheet, columns, rowNumber, "LocationAddress", "آدرس لوکیشن"));
                AddText(cmd, "@EducationLevel", ReadCell(sheet, columns, rowNumber, "EducationLevel", "تحصیلات"));
                AddText(cmd, "@ServiceStatus", NormalizeStatus(ReadCell(sheet, columns, rowNumber, "ServiceStatus", "وضعیت خدمات")));
                AddText(cmd, "@UrgentSituation", ReadCell(sheet, columns, rowNumber, "UrgentSituation", "شرح وضعیت فوری"));
                cmd.Parameters.AddWithValue("@CenterID", SecurityContext.CurrentCenterId);

                var idObj = cmd.ExecuteScalar();
                int caseId = idObj == null ? 0 : Convert.ToInt32((long)idObj);
                AuditLogger.Log("ثبت از اکسل", "TblCase", caseId, "", "Code=" + code + "; HeadFullName=" + headFullName);
            }
        }

        private bool IsCodeExists(SQLiteConnection con, string code)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT COUNT(1) FROM TblCase WHERE Code = @Code", con))
            {
                cmd.Parameters.AddWithValue("@Code", code ?? "");
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        // آموزش — بدنه به CaseNumbering منتقل شد (منطق واحد برای هر سه مسیرِ
        // ساختِ شماره). حد پایینِ «شماره شروع پرونده» همان‌جا رعایت می‌شود، پس
        // باگی که قبلاً اینجا رفع شده بود دیگر نمی‌تواند تکرار شود.
        private int GetNextFormNo(SQLiteConnection con)
        {
            return CaseManagement.Sync.CaseNumbering.GetNextFormNo(con, null);
        }

        private string ReadCell(IXLWorksheet sheet, Dictionary<string, int> columns, int rowNumber, params string[] names)
        {
            foreach (string name in names)
            {
                int column;

                if (columns.TryGetValue(name, out column))
                    return sheet.Cell(rowNumber, column).GetString().Trim();
            }

            return "";
        }

        private DateTime ReadDate(IXLWorksheet sheet, Dictionary<string, int> columns, int rowNumber, params string[] names)
        {
            foreach (string name in names)
            {
                int column;

                if (!columns.TryGetValue(name, out column))
                    continue;

                IXLCell cell = sheet.Cell(rowNumber, column);

                if (cell.DataType == XLDataType.DateTime)
                    return cell.GetDateTime().Date;

                DateTime parsed;
                if (DateTime.TryParse(cell.GetString(), out parsed))
                    return parsed.Date;
            }

            return DateTime.Today;
        }

        private string NormalizeStatus(string value)
        {
            value = (value ?? "").Trim();

            if (value == "درانتظار" || value == "در انتظار")
                return "در انتظار تأیید";

            if (value == "در حالت قطع")
                return "قطع";

            return string.IsNullOrWhiteSpace(value) ? "فعال" : value;
        }

        private void AddInt(SQLiteCommand cmd, string name, int value)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        private void AddText(SQLiteCommand cmd, string name, string value)
        {
            cmd.Parameters.AddWithValue(name, value ?? "");
        }

        private void AddDate(SQLiteCommand cmd, string name, DateTime value)
        {
            cmd.Parameters.AddWithValue(name, value.Date.ToString("yyyy-MM-dd"));
        }
    }
}

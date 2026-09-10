using ClosedXML.Excel;
using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // «پروندهٔ کامل» — یک سندِ واحد از همهٔ بخش‌های یک پرونده.
    //
    // آموزش — مشکلِ نسخهٔ قبلی: CaseFileExportService.PrintFullCase برای هر
    // بخش یک PrintHelper.PrintDataTable جدا صدا می‌زد، یعنی کاربر برای یک
    // پرونده تا سیزده پنجرهٔ پیش‌نمایشِ پشتِ‌سرِ‌هم می‌گرفت و هر کدام سربرگ و
    // شمارهٔ صفحهٔ مستقل داشت — عملاً غیرقابل استفاده. اینجا همهٔ بخش‌ها
    // بلوک‌های یک ReportDoc واحدند: یک پیش‌نمایش، یک شماره‌گذاریِ پیوسته،
    // یک صفحهٔ جلد.
    //
    // CaseFileExportService دست‌نخورده باقی می‌ماند (کدِ دیگری ممکن است از آن
    // استفاده کند)؛ این کلاس فقط از همان CaseExportDataProvider تغذیه می‌شود،
    // پس داده‌ها بینِ دو مسیر یکی است.
    //
    // ⚠ ClosedXML زیرِ میزبانِ آزمون با خطای بایندِ System.Memory می‌افتد
    // (مسئلهٔ محیط، نه برنامه) — پس مسیرِ اکسل باید دستی وارسی شود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class CaseFileSection
    {
        public string Title;
        public Func<int, DataTable> Load;

        // بخش‌هایی که همیشه یک ردیف دارند، به‌شکلِ «لیبل: مقدار» خواناترند تا
        // جدولی با یازده ستونِ باریک.
        public bool PreferKeyValues;

        // بخشِ عکس: در سندِ چاپی/PDF به‌صورت شبکهٔ تصویر کشیده می‌شود، ولی در
        // اکسل همان جدول (با ستونِ مسیرِ فایل) نوشته می‌شود.
        public bool RenderAsPhotos;
    }

    public static class CaseFileReport
    {
        // ترتیبِ بخش‌ها عمداً همان ترتیبِ روایتِ پرونده است: چیستی، سپس
        // افراد، سپس اسناد، سپس آنچه بر آن گذشته.
        public static List<CaseFileSection> Sections()
        {
            return new List<CaseFileSection>
            {
                S("خلاصه پرونده",      CaseExportDataProvider.GetCaseSummary, true),
                S("اطلاعات معلولیت",   CaseExportDataProvider.GetDisabilityInfo, true),
                S("اطلاعات ایتام",     CaseExportDataProvider.GetOrphanInfo, true),
                S("اطلاعات مهاجرت",    CaseExportDataProvider.GetMigrantInfo, true),
                S("اعضای خانواده",     CaseExportDataProvider.GetFamilyMembers, false),
                S("نماینده قانونی",    CaseExportDataProvider.GetRepresentatives, false),
                S("وضعیت اسناد",       CaseExportDataProvider.GetDocumentStatus, false),
                S("اسناد ناقص",        CaseExportDataProvider.GetMissingDocuments, false),
                S("امتیاز آسیب‌پذیری", CaseExportDataProvider.GetVulnerabilityBreakdown, false),
                S("تأمین مالی",        CaseExportDataProvider.GetFunding, false),
                S("بازدید میدانی",     CaseExportDataProvider.GetFieldVisits, false),
                Photos("عکس‌های بازدید", CaseExportDataProvider.GetFieldVisitPhotos),
                S("سابقه مساعدت",      CaseExportDataProvider.GetAssistanceHistory, false),
                S("تایم‌لاین",         CaseExportDataProvider.GetTimeline, false),
            };
        }

        private static CaseFileSection S(string title, Func<int, DataTable> load, bool kv)
        {
            return new CaseFileSection { Title = title, Load = load, PreferKeyValues = kv };
        }

        private static CaseFileSection Photos(string title, Func<int, DataTable> load)
        {
            return new CaseFileSection { Title = title, Load = load, RenderAsPhotos = true };
        }

        // تعدادِ ردیفِ هر بخش — دیالوگ آن را کنارِ نامِ بخش نشان می‌دهد تا
        // کاربر پیش از خروجی بداند کدام بخش خالی است.
        public static Dictionary<string, int> RowCounts(int caseId)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CaseFileSection s in Sections())
            {
                try
                {
                    DataTable t = s.Load(caseId);
                    counts[s.Title] = t == null ? 0 : t.Rows.Count;
                }
                catch { counts[s.Title] = 0; }
            }
            return counts;
        }

        // ═════════════════════════════════════════════════════════════════════
        // سندِ چاپی / PDF
        // ═════════════════════════════════════════════════════════════════════
        public static ReportDoc Build(DatabaseHelper db, int caseId, string caseCode,
                                      ICollection<string> selectedTitles,
                                      bool coverPage, bool landscape, bool skipEmpty)
        {
            if (db == null) db = new DatabaseHelper();

            DataRow info = CaseInfo(db, caseId);
            string head = Str(info, "HeadFullName");

            var doc = new ReportDoc
            {
                Title = "پروندهٔ کامل مستفید",
                Subtitle = head,
                DocumentCode = caseCode,
                Landscape = landscape,
                ShowCoverPage = coverPage,
                CoverHeadline = "پروندهٔ کامل مستفید",
            };

            doc.HeaderFields.Add(Kv("کد پرونده", caseCode));
            doc.HeaderFields.Add(Kv("نام سرپرست", head));
            doc.HeaderFields.Add(Kv("نوع درخواست", Str(info, "RequestTypeName")));

            doc.CoverFields.Add(Kv("کد پرونده", caseCode));
            doc.CoverFields.Add(Kv("شماره فرم", Str(info, "FormNo")));
            doc.CoverFields.Add(Kv("نام سرپرست", head));
            doc.CoverFields.Add(Kv("نام پدر", Str(info, "HeadFatherName")));
            doc.CoverFields.Add(Kv("نوع درخواست", Str(info, "RequestTypeName")));
            doc.CoverFields.Add(Kv("وضعیت خدمات", Str(info, "ServiceStatusName")));
            doc.CoverFields.Add(Kv("مرکز", SecurityContext.CenterDisplay ?? ""));
            doc.CoverFields.Add(Kv("تاریخ تهیه", PersianDateHelper.ToPersianDateString(DateTime.Now)));
            doc.CoverFields.Add(Kv("تهیه‌کننده", SecurityContext.Username ?? ""));

            foreach (CaseFileSection section in Sections())
            {
                if (selectedTitles != null && !selectedTitles.Contains(section.Title)) continue;

                DataTable table;
                try { table = section.Load(caseId); }
                catch (Exception ex)
                {
                    doc.Blocks.Add(new ReportHeading { Text = section.Title });
                    doc.Blocks.Add(new ReportCallout
                    {
                        Text = "خطا در خواندن این بخش: " + ex.Message,
                        Accent = UiTheme.Danger
                    });
                    continue;
                }

                bool empty = table == null || table.Rows.Count == 0;
                if (empty && skipEmpty) continue;

                doc.Blocks.Add(new ReportHeading
                {
                    Text = section.Title,
                    Note = empty ? "" : ReportDoc.Fa(table.Rows.Count) + " ردیف"
                });

                if (empty)
                {
                    doc.Blocks.Add(new ReportParagraph
                    {
                        Text = "برای این بخش داده‌ای ثبت نشده است.",
                        Muted = true
                    });
                    continue;
                }

                // عکس ⇒ شبکهٔ تصویر؛ تکْ‌ردیفِ عریض ⇒ لیبل:مقدار؛ بقیه ⇒ جدول.
                if (section.RenderAsPhotos)
                    doc.Blocks.Add(PhotoGridOf(table, landscape));
                else if (section.PreferKeyValues && table.Rows.Count == 1)
                    doc.Blocks.Add(KeyValuesOf(table.Rows[0]));
                else
                    doc.Blocks.Add(TableOf(table));
            }

            doc.Blocks.Add(new ReportSpacer { Height = 14f });
            doc.Blocks.Add(new ReportSignatures
            {
                Roles = new[] { "تهیه‌کنندهٔ پرونده", "مسئول بررسی", "مدیر مرکز" }
            });

            return doc;
        }

        // زیرنویسِ هر عکس: تاریخ و بازدیدکننده همیشه، توضیح اگر باشد — تا
        // برگهٔ چاپی بگوید هر عکس مالِ کدام بازدید است.
        private static ReportImageGrid PhotoGridOf(DataTable table, bool landscape)
        {
            var grid = new ReportImageGrid
            {
                Columns = landscape ? 4 : 3,
                CellHeight = landscape ? 150f : 170f,
                EmptyText = "برای بازدیدهای این پرونده عکسی ثبت نشده است.",
            };

            foreach (DataRow row in table.Rows)
            {
                var parts = new List<string>();
                AddIf(parts, Str(row, "تاریخ بازدید"));
                AddIf(parts, Str(row, "بازدیدکننده"));
                AddIf(parts, Str(row, "توضیح"));

                grid.Items.Add(new ReportImageItem
                {
                    Path = Str(row, "مسیر فایل"),
                    Caption = string.Join("  ·  ", parts.ToArray())
                });
            }

            return grid;
        }

        private static void AddIf(List<string> list, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) list.Add(value.Trim());
        }

        private static ReportKeyValues KeyValuesOf(DataRow row)
        {
            var kv = new ReportKeyValues { Columns = 2 };
            foreach (DataColumn c in row.Table.Columns)
            {
                object v = row[c];
                string text = (v == null || v == DBNull.Value) ? "" : Convert.ToString(v);
                kv.Items.Add(new KeyValuePair<string, string>(c.ColumnName, text));
            }
            return kv;
        }

        // وزنِ ستون‌ها از طولِ محتوای واقعی می‌آید — ستونِ «شرح» باید پهن‌تر از
        // ستونِ «تاریخ» باشد، وگرنه متنِ بلند در ستونِ باریک به ده سطر می‌شکند.
        private static ReportTable TableOf(DataTable table)
        {
            var rt = new ReportTable { Data = table, ShowRowNumbers = true };

            foreach (DataColumn c in table.Columns)
            {
                int longest = c.ColumnName.Length;
                int sampled = 0;
                foreach (DataRow r in table.Rows)
                {
                    object v = r[c];
                    if (v != null && v != DBNull.Value)
                    {
                        int len = Convert.ToString(v).Length;
                        if (len > longest) longest = len;
                    }
                    if (++sampled >= 60) break;
                }

                float weight = Math.Min(3.2f, Math.Max(0.8f, longest / 10f));
                bool numeric = c.DataType == typeof(int) || c.DataType == typeof(long) ||
                               c.DataType == typeof(short) || c.DataType == typeof(decimal) ||
                               c.DataType == typeof(double) || c.DataType == typeof(float);

                rt.Columns.Add(ReportDocColumn.Of(c.ColumnName, c.ColumnName, weight,
                    numeric || longest <= 12 ? ReportAlign.Center : ReportAlign.Right));
            }

            return rt;
        }

        // ═════════════════════════════════════════════════════════════════════
        // اکسل — همان شکلِ CaseFileExportService ولی با احترام به انتخابِ کاربر
        // ═════════════════════════════════════════════════════════════════════
        public static void ExportExcel(int caseId, ICollection<string> selectedTitles, string outputPath)
        {
            if (caseId <= 0) throw new ArgumentException("شناسه پرونده معتبر نیست.");

            using (var workbook = new XLWorkbook())
            {
                foreach (CaseFileSection section in Sections())
                {
                    if (selectedTitles != null && !selectedTitles.Contains(section.Title)) continue;

                    DataTable table;
                    try { table = section.Load(caseId); }
                    catch { table = null; }

                    AddSheet(workbook, section.Title, table);
                }

                if (workbook.Worksheets.Count == 0)
                    AddSheet(workbook, "خالی", null);

                workbook.SaveAs(outputPath);
            }
        }

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

        // ═════════════════════════════════════════════════════════════════════
        private static DataRow CaseInfo(DatabaseHelper db, int caseId)
        {
            DataTable t = db.Query(@"
SELECT IFNULL(c.Code,'') AS Code, IFNULL(c.FormNo,'') AS FormNo,
       IFNULL(c.HeadFullName,'') AS HeadFullName,
       IFNULL(c.HeadFatherName,'') AS HeadFatherName,
       IFNULL(rt.Name, IFNULL(c.RequestType, '')) AS RequestTypeName,
       IFNULL(ss.Name, IFNULL(c.ServiceStatus, '')) AS ServiceStatusName
FROM TblCase c
LEFT JOIN TblRequestType   rt ON rt.RequestTypeID   = c.RequestTypeID
LEFT JOIN TblServiceStatus ss ON ss.ServiceStatusID = c.ServiceStatusID
WHERE c.CasID = @id", new SQLiteParameter("@id", caseId));

            return t.Rows.Count > 0 ? t.Rows[0] : null;
        }

        private static string Str(DataRow row, string column)
        {
            if (row == null || !row.Table.Columns.Contains(column)) return "";
            object v = row[column];
            return v == null || v == DBNull.Value ? "" : Convert.ToString(v);
        }

        private static KeyValuePair<string, string> Kv(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value ?? "");
        }
    }
}

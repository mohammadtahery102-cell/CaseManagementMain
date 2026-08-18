using System;
using System.Windows.Forms;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // GridReportExporter — گزارش جمعیِ Word/PDF از نتایجِ فیلترشده‌ی یک گرید.
    // آموزش: فقط ستون‌های «قابل‌نمایش» گرید (به همان ترتیب و با همان عنوان فارسی)
    // و مقادیرِ «نمایش‌داده‌شده» (FormattedValue — پس تاریخ‌ها شمسی و مرتب) خروجی
    // می‌شوند؛ یعنی خروجی دقیقاً همان چیزی است که کاربر با فیلتر ولایت/ولسوالی
    // می‌بیند. PDF از روی همان Word با PdfConversionHelper ساخته می‌شود (اول
    // Microsoft Word، سپس LibreOffice، هرکدام نصب باشد).
    // ─────────────────────────────────────────────────────────────────────────
    public static class GridReportExporter
    {
        private const string HeaderFill = "2C5A85"; // آبیِ سازمانی (UiTheme.Primary)

        public static void ExportToWord(DataGridView grid, string title, string subtitle, string outputPath)
        {
            using (WordprocessingDocument doc = WordprocessingDocument.Create(
                outputPath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart main = doc.AddMainDocumentPart();
                main.Document = new Document();
                Body body = main.Document.AppendChild(new Body());

                // ─── عنوان ───
                body.AppendChild(MakeParagraph(title, bold: true, size: "32",
                    align: JustificationValues.Center, color: "1B3A5C"));
                if (!string.IsNullOrWhiteSpace(subtitle))
                    body.AppendChild(MakeParagraph(subtitle, bold: false, size: "20",
                        align: JustificationValues.Center, color: "68758A"));
                body.AppendChild(MakeParagraph("", false, "8", JustificationValues.Center, null));

                // ─── ستون‌های قابل‌نمایش به ترتیب DisplayIndex ───
                var cols = new System.Collections.Generic.List<DataGridViewColumn>();
                foreach (DataGridViewColumn c in grid.Columns)
                    if (c.Visible) cols.Add(c);
                cols.Sort((a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));

                Table tbl = new Table();
                tbl.AppendChild(new TableProperties(
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4, Color = "B0B8C4" },
                        new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "B0B8C4" },
                        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "B0B8C4" },
                        new RightBorder { Val = BorderValues.Single, Size = 4, Color = "B0B8C4" },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "D6DCE5" },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "D6DCE5" }),
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new BiDiVisual())); // جدول راست‌به‌چپ

                // ردیف سرستون
                TableRow header = new TableRow();
                foreach (DataGridViewColumn c in cols)
                    header.Append(MakeCell(c.HeaderText, isHeader: true));
                tbl.Append(header);

                // ردیف‌های داده (مقدارِ نمایش‌داده‌شده)
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    TableRow tr = new TableRow();
                    foreach (DataGridViewColumn c in cols)
                    {
                        object fv = row.Cells[c.Index].FormattedValue;
                        tr.Append(MakeCell(fv == null ? "" : fv.ToString(), isHeader: false));
                    }
                    tbl.Append(tr);
                }

                body.AppendChild(tbl);

                // ─── تنظیمات صفحه: افقی (Landscape) + راست‌به‌چپ ───
                body.AppendChild(new SectionProperties(
                    new PageSize { Width = 16838U, Height = 11906U, Orient = PageOrientationValues.Landscape },
                    new PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720, Header = 360, Footer = 360, Gutter = 0 },
                    new BiDi()));

                main.Document.Save();
            }
        }

        // internal (نه private): «مرکز کنترل توسعه‌دهنده» برای گزارش سلامت از
        // همین دو سازندهٔ قالب استفاده می‌کند تا ظاهر اسناد یکسان بماند و منطق
        // قالب‌بندی Word در دو جا تکرار نشود. رفتار برای فراخوانی‌های موجود
        // ذره‌ای تغییر نکرده است.
        internal static Paragraph MakeParagraph(string text, bool bold, string size,
            JustificationValues align, string color)
        {
            RunProperties rp = new RunProperties(new RunFonts { Ascii = "Tahoma", HighAnsi = "Tahoma", ComplexScript = "Tahoma" });
            if (bold) { rp.Append(new Bold()); rp.Append(new BoldComplexScript()); }
            rp.Append(new FontSize { Val = size });
            rp.Append(new FontSizeComplexScript { Val = size });
            if (!string.IsNullOrEmpty(color)) rp.Append(new Color { Val = color });

            return new Paragraph(
                new ParagraphProperties(new BiDi(), new Justification { Val = align }),
                new Run(rp, new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve }));
        }

        internal static TableCell MakeCell(string text, bool isHeader)
        {
            RunProperties rp = new RunProperties(new RunFonts { Ascii = "Tahoma", HighAnsi = "Tahoma", ComplexScript = "Tahoma" });
            if (isHeader)
            {
                rp.Append(new Bold());
                rp.Append(new BoldComplexScript());
                rp.Append(new Color { Val = "FFFFFF" });
            }
            rp.Append(new FontSize { Val = isHeader ? "20" : "18" });
            rp.Append(new FontSizeComplexScript { Val = isHeader ? "20" : "18" });

            Paragraph para = new Paragraph(
                new ParagraphProperties(new BiDi(),
                    new Justification { Val = isHeader ? JustificationValues.Center : JustificationValues.Right },
                    new SpacingBetweenLines { After = "20", Before = "20" }),
                new Run(rp, new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve }));

            TableCellProperties tcp = new TableCellProperties(
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            if (isHeader)
                tcp.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = HeaderFill });

            return new TableCell(tcp, para);
        }

        // ─── تبدیل Word → PDF — Word (اگر نصب باشد) سپس LibreOffice ──────────
        public static bool IsPdfAvailable()
        {
            return PdfConversionHelper.IsAvailable();
        }

        public static string ConvertDocxToPdf(string docxPath)
        {
            return PdfConversionHelper.ConvertDocxToPdf(docxPath);
        }
    }
}

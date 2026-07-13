using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using CaseManagement.DAL;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace CaseManagement.Helpers
{
    public class OpenXmlCaseExporter
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        public void ExportFullCaseToWord(int caseId, string templatePath, string outputPath)
        {
            if (caseId <= 0)
                throw new Exception("پرونده مشخص نیست.");

            // آموزش — رفع باگ امنیتی چندمرکزی: این متد قبلاً پرونده را بدون
            // بررسی مالکیت مرکز می‌خواند؛ چون همه مسیرهای Export (تکی/PDF/جمعی)
            // از همین‌جا عبور می‌کنند، اینجا یک‌بار مرکز پرونده تأیید می‌شود.
            CenterGuard.EnsureCaseAccess(db, caseId);

            if (!File.Exists(templatePath))
                throw new Exception("فایل قالب Word پیدا نشد.");

            string folder = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            File.Copy(templatePath, outputPath, true);

            DataTable caseData = GetDataTable("SELECT * FROM TblCase WHERE CasID = @CasID", caseId);
            DataTable familyData = GetDataTable("SELECT * FROM TblFamily WHERE CasID = @CasID ORDER BY FamID", caseId);
            DataTable docsData = GetDataTable("SELECT * FROM TblDocs WHERE CasID = @CasID ORDER BY DocID", caseId);

            if (caseData.Rows.Count == 0)
                throw new Exception("پرونده پیدا نشد.");

            using (WordprocessingDocument doc = WordprocessingDocument.Open(outputPath, true))
            {
                FillFamilyBlock(doc, familyData);
                FillDocsBlock(doc, docsData);

                DataRow row = caseData.Rows[0];

                ReplaceTextEverywhere(doc, BuildCaseValues(row, familyData.Rows.Count, docsData.Rows.Count));
                ReplaceImageEverywhere(doc, "{{HeadPhoto}}", GetValue(row, "PhotoPath"), 90f, 110f);
                ReplaceImageEverywhere(doc, "{{FamilyPhoto}}", GetValue(row, "FamilyPhotoPath"), 250f, 160f);

                RemoveUnusedPlaceholdersEverywhere(doc);

                doc.MainDocumentPart.Document.Save();
            }
        }

        private Dictionary<string, string> BuildCaseValues(DataRow row, int familyCount, int docsCount)
        {
            return new Dictionary<string, string>
            {
                { "{{CasID}}", GetValue(row, "CasID") },
                { "{{FormNo}}", GetValue(row, "FormNo") },
                { "{{Code}}", GetValue(row, "Code") },
                { "{{CaseNo}}", GetValue(row, "CaseNo") },
                { "{{CaseDate}}", GetDate(row, "CaseDate") },
                { "{{Zone}}", GetValue(row, "Zone") },
                { "{{Province}}", GetValue(row, "Province") },
                { "{{District}}", GetValue(row, "District") },
                { "{{RequestType}}", GetValue(row, "RequestType") },
                { "{{PriorityLevel}}", GetValue(row, "PriorityLevel") },
                { "{{HeadFullName}}", GetValue(row, "HeadFullName") },
                { "{{HeadFatherName}}", GetValue(row, "HeadFatherName") },
                { "{{HeadSadat}}", GetValue(row, "HeadSadat") },
                { "{{Religion}}", GetValue(row, "Religion") },
                { "{{HeadTazkiraNo}}", GetValue(row, "HeadTazkiraNo") },
                { "{{HeadOriginalResidence}}", GetValue(row, "HeadOriginalResidence") },
                { "{{HeadCurrentResidence}}", GetValue(row, "HeadCurrentResidence") },
                { "{{RelationshipToFamily}}", GetValue(row, "RelationshipToFamily") },
                { "{{Phone}}", GetValue(row, "Phone") },
                { "{{RelativePhone}}", GetValue(row, "RelativePhone") },
                { "{{CoveredByOrg}}", GetValue(row, "CoveredByOrg") },
                { "{{Job}}", GetValue(row, "Job") },
                { "{{Skill}}", GetValue(row, "Skill") },
                { "{{DisabilityDegree}}", GetValue(row, "DisabilityDegree") },
                { "{{DisabilityType}}", GetValue(row, "DisabilityType") },
                { "{{MigrationCardType}}", GetValue(row, "MigrationCardType") },
                { "{{MaritalStatus}}", GetValue(row, "MaritalStatus") },
                { "{{Surveyors}}", GetValue(row, "Surveyors") },
                { "{{SurveyDate}}", GetDate(row, "SurveyDate") },
                { "{{LocationAddress}}", GetValue(row, "LocationAddress") },
                { "{{EducationLevel}}", GetValue(row, "EducationLevel") },
                { "{{ServiceStatus}}", GetValue(row, "ServiceStatus") },
                { "{{UrgentSituation}}", GetValue(row, "UrgentSituation") },
                { "{{FamilyCount}}", familyCount.ToString() },
                { "{{DocsCount}}", docsCount.ToString() }
            };
        }

        private void FillFamilyBlock(WordprocessingDocument doc, DataTable familyData)
        {
            Body body = doc.MainDocumentPart.Document.Body;
            int startIndex;
            int endIndex;

            if (!FindBlock(body, "{{FamilyBlockStart}}", "{{FamilyBlockEnd}}", out startIndex, out endIndex))
                return;

            OpenXmlElement insertBefore = body.ChildElements[startIndex];
            List<OpenXmlElement> template = GetBlockElements(body, startIndex, endIndex);

            if (familyData.Rows.Count == 0)
            {
                body.InsertBefore(CreateParagraph("هیچ عضو خانواده ثبت نشده است."), insertBefore);
                RemoveBlockElements(body, startIndex, endIndex);
                return;
            }

            int index = 1;

            foreach (DataRow row in familyData.Rows)
            {
                foreach (OpenXmlElement sourceElement in template)
                {
                    OpenXmlElement clone = sourceElement.CloneNode(true);

                    ReplaceTextInElement(clone, BuildFamilyValues(row, index));
                    ReplaceImageInElement(doc.MainDocumentPart, clone, "{{MemberPhoto}}", GetValue(row, "MemberPhotoPath"), 85f, 105f);

                    body.InsertBefore(clone, insertBefore);
                }

                index++;
            }

            RemoveBlockElements(body, startIndex + (template.Count * familyData.Rows.Count), endIndex + (template.Count * familyData.Rows.Count));
        }

        private Dictionary<string, string> BuildFamilyValues(DataRow row, int index)
        {
            return new Dictionary<string, string>
            {
                { "{{FamilyBlockStart}}", "" },
                { "{{FamilyBlockEnd}}", "" },
                { "{{MemberTitle}}", "عضو شماره " + index },
                { "{{FamID}}", GetValue(row, "FamID") },
                { "{{MemberName}}", GetValue(row, "MemberName") },
                { "{{MemberFatherName}}", GetValue(row, "MemberFatherName") },
                { "{{MemberTazkiraNo}}", GetValue(row, "MemberTazkiraNo") },
                { "{{BirthDate}}", GetDate(row, "BirthDate") },
                { "{{MemberSadat}}", GetValue(row, "MemberSadat") },
                { "{{Gender}}", GetValue(row, "Gender") },
                { "{{PhysicalStatus}}", GetValue(row, "PhysicalStatus") },
                { "{{HasDisability}}", GetValue(row, "HasDisability") },
                { "{{MemberDisabilityDegree}}", GetValue(row, "MemberDisabilityDegree") },
                { "{{MemberEducation}}", GetValue(row, "MemberEducation") },
                { "{{SchoolName}}", GetValue(row, "SchoolName") },
                { "{{GradeLevel}}", GetValue(row, "GradeLevel") },
                { "{{UniversityName}}", GetValue(row, "UniversityName") },
                { "{{StudyYear}}", GetValue(row, "StudyYear") },
                { "{{Major}}", GetValue(row, "Major") },
                { "{{StudyField}}", GetValue(row, "StudyField") },
                // آموزش — {{OfficialStatus}} در قالب دقیقاً در جای «دلیل ترک
                // تحصیل» است؛ چون فیلد OfficialStatus از فرم حذف شد، این
                // placeholder با مقدار واقعی «دلیل ترک تحصیل» (LeaveReason) پر
                // می‌شود تا جای درست قالب خالی نماند.
                { "{{OfficialStatus}}", GetValue(row, "LeaveReason") },
                { "{{Skill}}", GetValue(row, "Skill") },
                { "{{MemberSkill}}", GetValue(row, "Skill") },
                { "{{LeaveReason}}", GetValue(row, "LeaveReason") },
                { "{{Details}}", GetValue(row, "Details") },
                // ─── فیلدهای تحصیلی جدید (placeholderهای اضافه‌شده به قالب) ────
                { "{{SchoolType}}", GetValue(row, "SchoolType") },
                { "{{SchoolPrevGrade}}", GetValue(row, "SchoolPrevGrade") },
                { "{{UniversityType}}", GetValue(row, "UniversityType") },
                { "{{UniversityPrevGrade}}", GetValue(row, "UniversityPrevGrade") },
                { "{{SeminaryLevel}}", GetValue(row, "SeminaryLevel") },
                { "{{EducationCoverage}}", GetValue(row, "EducationCoverage") }
            };
        }
        private void FillDocsBlock(WordprocessingDocument doc, DataTable docsData)
        {
            Body body = doc.MainDocumentPart.Document.Body;

            body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
            body.AppendChild(CreateParagraph("بخش اسناد پرونده"));

            if (docsData.Rows.Count == 0)
            {
                body.AppendChild(CreateParagraph("هیچ سندی ثبت نشده است."));
                return;
            }

            int index = 1;

            foreach (DataRow row in docsData.Rows)
            {
                body.AppendChild(CreateParagraph("سند شماره " + index));
                body.AppendChild(CreateParagraph("نوع سند: " + GetValue(row, "DocType")));
                body.AppendChild(CreateParagraph("نام فایل: " + GetValue(row, "OriginalFileName")));
                body.AppendChild(CreateParagraph("مرجع مرتبط: " + GetValue(row, "RelatedCaseRef")));
                body.AppendChild(CreateParagraph("توضیحات: " + GetValue(row, "DocDescription")));

                string filePath = GetValue(row, "DocFilePath");

                if (IsImageFile(filePath))
                {
                    Paragraph imageParagraph = new Paragraph();
                    imageParagraph.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }));
                    imageParagraph.AppendChild(new Run(CreateImageDrawing(doc.MainDocumentPart, filePath, 420f, 560f)));
                    body.AppendChild(imageParagraph);
                }
                else
                {
                    body.AppendChild(CreateParagraph("این سند عکس نیست و فقط مشخصات آن ثبت شد."));
                }

                if (index < docsData.Rows.Count)
                    body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));

                index++;
            }
        }


        private bool FindBlock(Body body, string startText, string endText, out int startIndex, out int endIndex)
        {
            startIndex = -1;
            endIndex = -1;

            for (int i = 0; i < body.ChildElements.Count; i++)
            {
                if (ElementContainsText(body.ChildElements[i], startText))
                {
                    startIndex = i;
                    break;
                }
            }

            if (startIndex < 0)
                return false;

            for (int i = startIndex; i < body.ChildElements.Count; i++)
            {
                if (ElementContainsText(body.ChildElements[i], endText))
                {
                    endIndex = i;
                    return true;
                }
            }

            return false;
        }

        private List<OpenXmlElement> GetBlockElements(Body body, int startIndex, int endIndex)
        {
            List<OpenXmlElement> elements = new List<OpenXmlElement>();

            for (int i = startIndex; i <= endIndex; i++)
                elements.Add(body.ChildElements[i].CloneNode(true));

            return elements;
        }

        private void RemoveBlockElements(Body body, int startIndex, int endIndex)
        {
            for (int i = endIndex; i >= startIndex; i--)
                body.ChildElements[i].Remove();
        }

        private bool ElementContainsText(OpenXmlElement element, string text)
        {
            string allText = string.Concat(element.Descendants<Text>().Select(t => t.Text));
            return allText.Contains(text);
        }

        private Paragraph CreateParagraph(string text)
        {
            return new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }

        private void ReplaceTextEverywhere(WordprocessingDocument doc, Dictionary<string, string> values)
        {
            ReplaceTextInElement(doc.MainDocumentPart.Document, values);

            foreach (HeaderPart part in doc.MainDocumentPart.HeaderParts)
            {
                ReplaceTextInElement(part.Header, values);
                part.Header.Save();
            }

            foreach (FooterPart part in doc.MainDocumentPart.FooterParts)
            {
                ReplaceTextInElement(part.Footer, values);
                part.Footer.Save();
            }
        }

        private void ReplaceTextInElement(OpenXmlElement root, Dictionary<string, string> values)
        {
            foreach (Paragraph paragraph in root.Descendants<Paragraph>().ToList())
            {
                string oldText = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));

                if (string.IsNullOrEmpty(oldText) || !oldText.Contains("{{"))
                    continue;

                string newText = oldText;

                foreach (KeyValuePair<string, string> item in values)
                    newText = newText.Replace(item.Key, item.Value ?? "");

                if (newText == oldText)
                    continue;

                ReplaceParagraphText(paragraph, newText);
            }
        }

        private void ReplaceParagraphText(Paragraph paragraph, string text)
        {
            ParagraphProperties paragraphProperties = paragraph.ParagraphProperties == null ? null : (ParagraphProperties)paragraph.ParagraphProperties.CloneNode(true);
            Run firstRun = paragraph.Descendants<Run>().FirstOrDefault();
            RunProperties runProperties = firstRun == null || firstRun.RunProperties == null ? null : (RunProperties)firstRun.RunProperties.CloneNode(true);

            paragraph.RemoveAllChildren();

            if (paragraphProperties != null)
                paragraph.AppendChild(paragraphProperties);

            Run run = new Run();

            if (runProperties != null)
                run.AppendChild(runProperties);

            run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.AppendChild(run);
        }

        private void ReplaceImageEverywhere(WordprocessingDocument doc, string placeholder, string imagePath, float maxWidthPt, float maxHeightPt)
        {
            ReplaceImageInElement(doc.MainDocumentPart, doc.MainDocumentPart.Document, placeholder, imagePath, maxWidthPt, maxHeightPt);

            foreach (HeaderPart part in doc.MainDocumentPart.HeaderParts)
            {
                ReplaceImageInElement(part, part.Header, placeholder, imagePath, maxWidthPt, maxHeightPt);
                part.Header.Save();
            }

            foreach (FooterPart part in doc.MainDocumentPart.FooterParts)
            {
                ReplaceImageInElement(part, part.Footer, placeholder, imagePath, maxWidthPt, maxHeightPt);
                part.Footer.Save();
            }
        }

        private void ReplaceImageInElement(OpenXmlPart part, OpenXmlElement root, string placeholder, string imagePath, float maxWidthPt, float maxHeightPt)
        {
            foreach (Paragraph paragraph in root.Descendants<Paragraph>().ToList())
            {
                string text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));

                if (!text.Contains(placeholder))
                    continue;

                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    ReplaceParagraphText(paragraph, "");
                    continue;
                }

                paragraph.RemoveAllChildren();
                paragraph.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }));
                paragraph.AppendChild(new Run(CreateImageDrawing(part, imagePath, maxWidthPt, maxHeightPt)));
            }
        }

        private Drawing CreateImageDrawing(OpenXmlPart part, string imagePath, float maxWidthPt, float maxHeightPt)
        {
            ImagePart imagePart = AddImagePartToWordPart(part, GetImagePartType(imagePath));

            using (FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                imagePart.FeedData(stream);

            string relationshipId = part.GetIdOfPart(imagePart);

            long widthEmu;
            long heightEmu;
            GetScaledImageSize(imagePath, maxWidthPt, maxHeightPt, out widthEmu, out heightEmu);

            return new Drawing(
                new DW.Inline(
                    new DW.Extent() { Cx = widthEmu, Cy = heightEmu },
                    new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties() { Id = 1U, Name = Path.GetFileName(imagePath) },
                    new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties() { Id = 0U, Name = Path.GetFileName(imagePath) },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip() { Embed = relationshipId, CompressionState = A.BlipCompressionValues.Print },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset() { X = 0L, Y = 0L },
                                        new A.Extents() { Cx = widthEmu, Cy = heightEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })));
        }

        private ImagePart AddImagePartToWordPart(OpenXmlPart part, PartTypeInfo imagePartType)
        {
            MainDocumentPart mainPart = part as MainDocumentPart;
            if (mainPart != null)
                return mainPart.AddImagePart(imagePartType);

            HeaderPart headerPart = part as HeaderPart;
            if (headerPart != null)
                return headerPart.AddImagePart(imagePartType);

            FooterPart footerPart = part as FooterPart;
            if (footerPart != null)
                return footerPart.AddImagePart(imagePartType);

            throw new InvalidOperationException("امکان افزودن عکس به این بخش از فایل Word وجود ندارد.");
        }

        private void GetScaledImageSize(string imagePath, float maxWidthPt, float maxHeightPt, out long widthEmu, out long heightEmu)
        {
            const long emuPerPoint = 12700L;

            using (Image image = Image.FromFile(imagePath))
            {
                float widthPt = image.Width * 72f / image.HorizontalResolution;
                float heightPt = image.Height * 72f / image.VerticalResolution;
                float ratio = Math.Min(maxWidthPt / widthPt, maxHeightPt / heightPt);

                if (ratio > 1f)
                    ratio = 1f;

                widthEmu = (long)(widthPt * ratio * emuPerPoint);
                heightEmu = (long)(heightPt * ratio * emuPerPoint);
            }
        }

        private PartTypeInfo GetImagePartType(string imagePath)
        {
            string ext = Path.GetExtension(imagePath).ToLowerInvariant();

            if (ext == ".png")
                return ImagePartType.Png;

            if (ext == ".gif")
                return ImagePartType.Gif;

            if (ext == ".bmp")
                return ImagePartType.Bmp;

            if (ext == ".tif" || ext == ".tiff")
                return ImagePartType.Tiff;

            return ImagePartType.Jpeg;
        }

        private void RemoveUnusedPlaceholdersEverywhere(WordprocessingDocument doc)
        {
            RemoveUnusedPlaceholdersInElement(doc.MainDocumentPart.Document);
        }

        private void RemoveUnusedPlaceholdersInElement(OpenXmlElement root)
        {
            foreach (Paragraph paragraph in root.Descendants<Paragraph>().ToList())
            {
                string text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));

                if (text.Contains("{{") && text.Contains("}}"))
                    ReplaceParagraphText(paragraph, "");
            }
        }

        private bool IsImageFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" ||
                   ext == ".bmp" || ext == ".gif" || ext == ".tif" || ext == ".tiff";
        }

        private DataTable GetDataTable(string query, int caseId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@CasID", caseId);

                DataTable dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        private string GetValue(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName))
                return "";

            object value = row[columnName];

            if (value == null || value == DBNull.Value)
                return "";

            return value.ToString();
        }

        private string GetDate(DataRow row, string columnName)
        {
            string value = GetValue(row, columnName);
            DateTime dt;

            if (DateTime.TryParse(value, out dt))
                return PersianDateHelper.ToPersianDateString(dt);

            return value;
        }
    }
}

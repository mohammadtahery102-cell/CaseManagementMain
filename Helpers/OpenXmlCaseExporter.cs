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

            // آموزش — اصلاحاتِ صفحه‌بندی (بزرگ‌ترشدنِ عکسِ سرپرست، حداکثر ۳ عضو
            // در هر صفحه، حداکثر ۴ سند در هر صفحه) فقط برای «الگوی شماره ۱»
            // اعمال می‌شود؛ الگوی پیش‌فرض (FullCaseTemplate.docx) دقیقاً همان
            // رفتارِ قبلی‌اش را حفظ می‌کند (طبقِ درخواستِ صریحِ کاربر).
            bool isTemplate1 = IsTemplate1(templatePath);
            // آموزش — «الگوی شماره ۲» (فرمِ فشردهٔ تک‌صفحه‌ایِ «پرونده
            // نیازمندان») هم مثلِ الگوی شماره ۱ با نامِ فایل تشخیص داده
            // می‌شود. عمداً از isTemplate1 در FillFamilyBlock/FillDocsBlock
            // استفاده نمی‌کند (پایین همان مقدار isTemplate1 بدونِ تغییر پاس
            // داده می‌شود) چون گروه‌بندیِ ۳عضو/صفحه و ۴سند/صفحهٔ الگوی ۱ برای
            // این الگو لازم نیست — جدولِ فشرده‌اش خودش چند عضو را در یک صفحه
            // جا می‌دهد. فقط اندازهٔ دو عکس (سرپرست/جمعی) که در همین الگو
            // کنارِ هم و در یک صفحه باید جا شوند، مخصوصِ آن تنظیم شده.
            bool isTemplate2 = IsTemplate2(templatePath);

            using (WordprocessingDocument doc = WordprocessingDocument.Open(outputPath, true))
            {
                FillFamilyBlock(doc, familyData, isTemplate1);
                FillDocsBlock(doc, docsData, isTemplate1);

                DataRow row = caseData.Rows[0];

                Dictionary<string, string> caseValues = BuildCaseValues(row, familyData.Rows.Count, docsData.Rows.Count);
                if (isTemplate2)
                    // آموزش — فایلِ واقعیِ کاربر برای «الگوی شماره ۲» یک جدولِ
                    // *ثابتِ* ۹ردیفی است (نه {{FamilyBlockStart}}/{{FamilyBlockEnd}}
                    // مثلِ بقیهٔ الگوها) — FillFamilyBlock بالا برایش کاری نمی‌کند
                    // (چون نشانگر پیدا نمی‌کند). این‌جا هر ردیف با شمارهٔ ثابتِ
                    // خودش (MemberName1..9 و...) پر می‌شود؛ فقط ۹ عضوِ اول یک
                    // پرونده چاپ می‌شوند (اندازهٔ جدول در فایلِ خودِ کاربر).
                    foreach (KeyValuePair<string, string> item in BuildTemplate2FixedMemberValues(familyData))
                        caseValues[item.Key] = item.Value;
                ReplaceTextEverywhere(doc, caseValues);
                // آموزش — عکسِ سرپرست در «الگوی شماره ۱»: پس از بازخوردِ کاربر
                // (خیلی بزرگ شده بود)، به نصفِ اندازهٔ قبلی (۴۰۰×۳۸۰ → ۲۰۰×۱۹۰
                // نقطه، معادلِ ۲.۵ برابرِ اندازهٔ اصلیِ ۸۰×۷۶) برگشت.
                // الگوی پیش‌فرض دست‌نخورده ماند. فایلِ واقعیِ «الگوی شماره ۲»ی
                // کاربر {{HeadPhoto}} ندارد (فقط {{FamilyPhoto}})، پس این
                // فراخوانی برای آن الگو بی‌اثر است (هیچ placeholderـی پیدا
                // نمی‌شود) — بی‌خطر، چون ReplaceImageEverywhere وقتی متنِ
                // placeholder را در سند پیدا نکند کاری نمی‌کند.
                float headW = isTemplate1 ? 200f : 80f;
                float headH = isTemplate1 ? 190f : 76f;
                ReplaceImageEverywhere(doc, "{{HeadPhoto}}", GetValue(row, "PhotoPath"), headW, headH);
                // عکس جمعی دو برابر شد (درخواست کاربر). اندازه‌اش دست‌نخورده
                // می‌ماند؛ حالا که جدولِ مشخصات + عکسِ سرپرست + عکسِ جمعی در
                // الگوی شماره ۱ یک بلوکِ اتمیک است، این سه با هم به صفحهٔ بعد
                // می‌روند نه اینکه جدولِ مشخصات وسط شکسته شود. الگوی شماره ۲:
                // اندازه‌اش با جعبهٔ واقعیِ «عکس جمعی» در فایلِ کاربر (حدودِ
                // ۲۲۶×۱۵۷ نقطه) هم‌خوان است.
                float familyW = isTemplate2 ? 200f : 460f;
                float familyH = isTemplate2 ? 120f : 132f;
                ReplaceImageEverywhere(doc, "{{FamilyPhoto}}", GetValue(row, "FamilyPhotoPath"), familyW, familyH);

                // آموزش — لینک موقعیت باید *پس از* جای‌گزینی متن و *پیش از*
                // پاک‌سازی placeholderهای بی‌مصرف اجرا شود؛ وگرنه یا متنِ خام
                // چاپ می‌شود یا کادر لینک خالی می‌ماند.
                ReplaceLocationLink(doc, GetValue(row, "LocationAddress"));

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
                { "{{HeadIdCardType}}", GetValue(row, "HeadIdCardType") },
                { "{{HeadTazkiraNo}}", GetValue(row, "HeadTazkiraNo") },
                { "{{PhysicalStatusNotes}}", GetValue(row, "PhysicalStatusNotes") },
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
                { "{{StopReason}}", GetValue(row, "StopReason") },
                { "{{FamilyCount}}", familyCount.ToString() },
                { "{{DocsCount}}", docsCount.ToString() },
                // آموزش — به‌درخواستِ کاربر برای «الگوی شماره ۲» (که فرمِ
                // کاغذیِ «پرونده نیازمندان» را عیناً بازتولید می‌کند): ستونِ
                // HeadBirthDate از قبل در TblCase وجود داشت ولی هیچ الگویی
                // آن را چاپ نمی‌کرد؛ چون این‌جا فقط یک ورودیِ اضافه به
                // دیکشنری است، الگوهای دیگر (که اصلاً {{HeadBirthDate}}
                // ندارند) کاملاً بی‌تأثیر می‌مانند.
                { "{{HeadBirthDate}}", GetDate(row, "HeadBirthDate") }
            };
        }

        // آموزش — حداکثر ۳ عضو در هر صفحه (فقط الگوی شماره ۱): پیش از عضوِ
        // چهارم، هفتم، ... یک Page Break صریح درج می‌شود. cantSplit/keepNext
        // خودِ قالب (روی جدولِ ۸‌ردیفیِ هر عضو) تضمین می‌کند که یک عضو هرگز
        // وسط شکسته نشود؛ اینجا فقط گروه‌بندیِ ۳‌تایی اضافه می‌شود.
        private const int MaxMembersPerPageTemplate1 = 3;

        private void FillFamilyBlock(WordprocessingDocument doc, DataTable familyData, bool isTemplate1)
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
            int insertedElementCount = 0;

            foreach (DataRow row in familyData.Rows)
            {
                if (isTemplate1 && index > 1 && (index - 1) % MaxMembersPerPageTemplate1 == 0)
                {
                    body.InsertBefore(new Paragraph(new Run(new Break() { Type = BreakValues.Page })), insertBefore);
                    insertedElementCount++;
                }

                foreach (OpenXmlElement sourceElement in template)
                {
                    OpenXmlElement clone = sourceElement.CloneNode(true);

                    ReplaceTextInElement(clone, BuildFamilyValues(row, index));
                    ReplaceImageInElement(doc.MainDocumentPart, clone, "{{MemberPhoto}}", GetValue(row, "MemberPhotoPath"), 85f, 105f);

                    body.InsertBefore(clone, insertBefore);
                    insertedElementCount++;
                }

                index++;
            }

            RemoveBlockElements(body, startIndex + insertedElementCount, endIndex + insertedElementCount);
        }

        private Dictionary<string, string> BuildFamilyValues(DataRow row, int index)
        {
            return new Dictionary<string, string>
            {
                { "{{FamilyBlockStart}}", "" },
                { "{{FamilyBlockEnd}}", "" },
                { "{{MemberTitle}}", "عضو شماره " + index },
                // آموزش — «الگوی شماره ۲» ردیفِ جدولِ فشرده‌اش را با شمارهٔ خامِ
                // ستونِ «ش» می‌خواهد (نه عبارتِ کاملِ «عضو شماره N» که
                // {{MemberTitle}} می‌دهد)؛ چون فقط یک ورودیِ تازه به همین
                // دیکشنریِ موجود است، هیچ الگوی دیگری تحتِ تأثیر قرار نمی‌گیرد.
                { "{{RowNumber}}", index.ToString() },
                { "{{FamID}}", GetValue(row, "FamID") },
                { "{{MemberName}}", GetValue(row, "MemberName") },
                { "{{MemberFatherName}}", GetValue(row, "MemberFatherName") },
                { "{{MemberIdCardType}}", GetValue(row, "MemberIdCardType") },
                { "{{MemberTazkiraNo}}", GetValue(row, "MemberTazkiraNo") },
                { "{{BirthDate}}", GetDate(row, "BirthDate") },
                { "{{MemberSadat}}", GetValue(row, "MemberSadat") },
                { "{{Gender}}", GetValue(row, "Gender") },
                { "{{MemberRole}}", GetValue(row, "MemberRole") },
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
                { "{{EducationCoverage}}", GetValue(row, "EducationCoverage") },
                // ─── فیلدهای فرم اعضا که تا امروز در گزارش چاپ نمی‌شدند ────────
                { "{{MemberReligion}}", GetValue(row, "Religion") },
                { "{{MemberMaritalStatus}}", GetValue(row, "MaritalStatus") },
                { "{{MemberServiceStatus}}", GetValue(row, "ServiceStatus") },
                { "{{MemberStopReason}}", GetValue(row, "StopReason") },
                { "{{DisabilityDetails}}", GetValue(row, "DisabilityDetails") }
            };
        }

        // آموزش — «الگوی شماره ۲» (فایلِ Word واقعیِ خودِ کاربر) یک جدولِ
        // *ثابتِ* ۹ردیفی برای اعضاست، نه بلوکِ تکرارشوندهٔ {{FamilyBlockStart}}/
        // {{FamilyBlockEnd}} که بقیهٔ الگوها دارند — پس FillFamilyBlock برایش
        // کاری نمی‌کند و هر ردیف باید با شمارهٔ ثابتِ خودش (۱ تا ۹، دقیقاً به
        // تعدادِ ردیف‌های واقعیِ همان جدول در فایلِ کاربر) پر شود. فقط ۹ عضوِ
        // اولِ پرونده چاپ می‌شوند؛ این محدودیتِ خودِ طرحِ جدولِ کاربر است، نه
        // یک تصمیمِ کد.
        private Dictionary<string, string> BuildTemplate2FixedMemberValues(DataTable familyData)
        {
            const int maxRows = 9;
            var values = new Dictionary<string, string>();

            for (int i = 0; i < maxRows; i++)
            {
                string n = (i + 1).ToString();
                bool hasRow = i < familyData.Rows.Count;
                DataRow row = hasRow ? familyData.Rows[i] : null;

                values["{{MemberName" + n + "}}"] = hasRow ? GetValue(row, "MemberName") : "";
                values["{{MemberFatherName" + n + "}}"] = hasRow ? GetValue(row, "MemberFatherName") : "";
                values["{{MemberTazkiraNo" + n + "}}"] = hasRow ? GetValue(row, "MemberTazkiraNo") : "";
                values["{{MemberBirthDate" + n + "}}"] = hasRow ? GetDate(row, "BirthDate") : "";
                values["{{MemberSadat" + n + "}}"] = hasRow ? GetValue(row, "MemberSadat") : "";
                values["{{MemberRole" + n + "}}"] = hasRow ? GetValue(row, "MemberRole") : "";
                values["{{MemberEducation" + n + "}}"] = hasRow ? GetValue(row, "MemberEducation") : "";
                values["{{MemberPhysicalStatus" + n + "}}"] = hasRow ? GetValue(row, "PhysicalStatus") : "";
            }

            return values;
        }

        // آموزش — حداکثر ۴ سند در هر صفحه (فقط الگوی شماره ۱)؛ قبلاً بدونِ قید
        // هر سند صفحهٔ کامل جداگانه می‌گرفت که خودش منبعِ اصلیِ فضای خالیِ
        // زیاد بود. اندازهٔ عکس هم به یک بندانگشتیِ واقعی کوچک شد (به‌جای
        // تقریباً اندازهٔ کاملِ صفحه). الگوی پیش‌فرض دقیقاً رفتارِ قبلی‌اش
        // (هر سند = یک صفحه، عکسِ بزرگ) را حفظ می‌کند.
        private const int MaxDocsPerPageTemplate1 = 4;

        private void FillDocsBlock(WordprocessingDocument doc, DataTable docsData, bool isTemplate1)
        {
            Body body = doc.MainDocumentPart.Document.Body;

            body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
            body.AppendChild(CreateParagraph("بخش اسناد پرونده"));

            if (docsData.Rows.Count == 0)
            {
                body.AppendChild(CreateParagraph("هیچ سندی ثبت نشده است."));
                return;
            }

            float imageW = isTemplate1 ? 150f : 420f;
            float imageH = isTemplate1 ? 180f : 560f;

            int index = 1;

            foreach (DataRow row in docsData.Rows)
            {
                // آموزش — طبقِ خواستهٔ کاربر: عنوان/بندانگشتی/توضیحِ هر سند یک
                // بلوکِ واحد بمانند (keepNext/keepLines روی همهٔ پاراگراف‌های
                // این سند به‌جز آخری، فقط برای الگوی شماره ۱).
                body.AppendChild(CreateParagraph("سند شماره " + index, isTemplate1));
                body.AppendChild(CreateParagraph("نوع سند: " + GetValue(row, "DocType"), isTemplate1));
                body.AppendChild(CreateParagraph("نام فایل: " + GetValue(row, "OriginalFileName"), isTemplate1));
                body.AppendChild(CreateParagraph("مرجع مرتبط: " + GetValue(row, "RelatedCaseRef"), isTemplate1));
                body.AppendChild(CreateParagraph("توضیحات: " + GetValue(row, "DocDescription"), isTemplate1));

                string filePath = GetValue(row, "DocFilePath");

                if (IsImageFile(filePath))
                {
                    Paragraph imageParagraph = new Paragraph();
                    List<OpenXmlElement> pPrChildren = new List<OpenXmlElement> { new Justification() { Val = JustificationValues.Center } };
                    if (isTemplate1) pPrChildren.Insert(0, new KeepLines());
                    imageParagraph.AppendChild(new ParagraphProperties(pPrChildren));
                    imageParagraph.AppendChild(new Run(CreateImageDrawing(doc.MainDocumentPart, filePath, imageW, imageH)));
                    body.AppendChild(imageParagraph);
                }
                else
                {
                    body.AppendChild(CreateParagraph("این سند عکس نیست و فقط مشخصات آن ثبت شد.", isTemplate1));
                }

                bool isLast = index >= docsData.Rows.Count;
                bool needsPageBreak = isTemplate1
                    ? (!isLast && index % MaxDocsPerPageTemplate1 == 0)
                    : !isLast;

                if (needsPageBreak)
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

        // آموزش — نسخهٔ اضافی برای پاراگراف‌هایی که باید با پاراگرافِ بعدی
        // بمانند (keepNext) و خودشان هم بین دو صفحه نشکنند (keepLines) — فقط
        // بخشِ اسنادِ الگوی شماره ۱ از این استفاده می‌کند.
        private Paragraph CreateParagraph(string text, bool keepTogether)
        {
            Paragraph p = CreateParagraph(text);
            if (keepTogether)
                p.InsertAt(new ParagraphProperties(new KeepNext(), new KeepLines()), 0);
            return p;
        }

        // آموزش — «الگوی شماره ۱» بر اساسِ نامِ فایلِ قالب تشخیص داده می‌شود
        // (نه یک پرچمِ جداگانه) تا هیچ فراخوانیِ موجودِ ExportFullCaseToWord
        // نیاز به تغییر نداشته باشد؛ الگوی پیش‌فرض (هر نامِ دیگری) دقیقاً
        // رفتارِ قبلی‌اش را حفظ می‌کند.
        private static bool IsTemplate1(string templatePath)
        {
            return string.Equals(Path.GetFileNameWithoutExtension(templatePath), "الگوی_شماره_1", StringComparison.Ordinal);
        }

        // آموزش — همان الگوی تشخیصِ IsTemplate1، برای «الگوی شماره ۲».
        private static bool IsTemplate2(string templatePath)
        {
            return string.Equals(Path.GetFileNameWithoutExtension(templatePath), "الگوی_شماره_2", StringComparison.Ordinal);
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
            // آموزش — رفعِ باگِ «عنوانِ عضو چاپ نمی‌شد»: Descendants<Paragraph>()
            // خودِ عنصر را شامل نمی‌شود. در FillFamilyBlock هر عنصرِ بلوک جداگانه
            // Clone و پاس داده می‌شود؛ وقتی آن عنصر خودش یک Paragraph بود (مثلِ
            // پاراگرافِ «مشخصات اعضاء خانواده - {{MemberTitle}}»)، هیچ جای‌گزینی
            // انجام نمی‌شد و در پایان RemoveUnusedPlaceholders آن را خالی می‌کرد.
            // این افزوده فقط همان حالتِ ازقلم‌افتاده را پوشش می‌دهد؛ برای
            // Document/Header/Footer/Table رفتار دقیقاً مثل قبل است.
            List<Paragraph> paragraphs = root.Descendants<Paragraph>().ToList();

            Paragraph rootParagraph = root as Paragraph;
            if (rootParagraph != null)
                paragraphs.Insert(0, rootParagraph);

            foreach (Paragraph paragraph in paragraphs)
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
            // همان نکتهٔ ReplaceTextInElement: اگر خودِ عنصرِ پاس‌داده‌شده یک
            // Paragraph باشد (عنصرِ Cloneشدهٔ بلوک)، Descendants آن را نمی‌بیند.
            List<Paragraph> paragraphs = root.Descendants<Paragraph>().ToList();

            Paragraph rootParagraph = root as Paragraph;
            if (rootParagraph != null)
                paragraphs.Insert(0, rootParagraph);

            foreach (Paragraph paragraph in paragraphs)
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

        // ─────────────────────────────────────────────────────────────────────
        // آموزش — «موقعیت جغرافیایی» به‌صورت دکمه/لینک کلیک‌شدنی
        //
        // فیلد LocationAddress در فرم پرونده یا خودش یک نشانی اینترنتی است
        // (لینکی که کارشناس از Google Maps کپی کرده) یا یک آدرس متنی/مختصات.
        // در حالت دوم همان متن به جست‌وجوی نقشه‌ی گوگل داده می‌شود. نتیجه در
        // Word و در PDF خروجی، یک لینک کلیک‌شدنی است.
        // ─────────────────────────────────────────────────────────────────────
        private void ReplaceLocationLink(WordprocessingDocument doc, string locationAddress)
        {
            MainDocumentPart part = doc.MainDocumentPart;
            Uri address = BuildMapUri(locationAddress);

            foreach (Paragraph paragraph in part.Document.Descendants<Paragraph>().ToList())
            {
                string text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));

                if (!text.Contains("{{LocationLink}}"))
                    continue;

                if (address == null)
                {
                    ReplaceParagraphText(paragraph, "");
                    continue;
                }

                HyperlinkRelationship relationship = part.AddHyperlinkRelationship(address, true);

                ParagraphProperties paragraphProperties = paragraph.ParagraphProperties == null
                    ? null
                    : (ParagraphProperties)paragraph.ParagraphProperties.CloneNode(true);

                paragraph.RemoveAllChildren();

                if (paragraphProperties != null)
                    paragraph.AppendChild(paragraphProperties);

                Run run = new Run(
                    new RunProperties(
                        new RunFonts() { Ascii = "Segoe UI", HighAnsi = "Segoe UI", ComplexScript = "B Nazanin" },
                        new Bold(),
                        new BoldComplexScript(),
                        new DocumentFormat.OpenXml.Wordprocessing.Color() { Val = "0563C1" },
                        new FontSize() { Val = "21" },
                        new FontSizeComplexScript() { Val = "21" },
                        new Underline() { Val = UnderlineValues.Single },
                        new RightToLeftText()),
                    new Text("نمایش موقعیت روی نقشه") { Space = SpaceProcessingModeValues.Preserve });

                paragraph.AppendChild(new Hyperlink(run) { Id = relationship.Id });
            }
        }

        private Uri BuildMapUri(string locationAddress)
        {
            if (string.IsNullOrWhiteSpace(locationAddress))
                return null;

            string value = locationAddress.Trim();

            try
            {
                if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return new Uri(value, UriKind.Absolute);

                return new Uri("https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(value),
                    UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                // نشانی خراب نباید کل خروجی را از کار بیندازد؛ کادر خالی می‌ماند.
                return null;
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

        // تاریخ‌های خروجی Word/PDF شمسی و به‌صورت «روز/ماه/سال» (مثل 18/05/1405)
        // نوشته می‌شوند — به درخواست کاربر.
        //
        // آموزش — چرا اینجا و نه داخل PersianDateHelper: متد مشترکِ
        // ToPersianDateString ترتیبِ «سال/ماه/روز» می‌دهد و ده‌ها جای دیگر
        // (گریدها، خروجی اکسل، کارت شناسایی) به همان ترتیب متکی‌اند؛ تغییرِ آن
        // متد همه‌ی آن‌ها را هم عوض می‌کرد. پس فقط ترتیبِ نمایش در همین خروجی
        // برعکس می‌شود و هیچ جای دیگری دست نمی‌خورد.
        private string GetDate(DataRow row, string columnName)
        {
            return ToPersianExportDate(GetValue(row, columnName));
        }

        public static string ToPersianExportDate(string value)
        {
            DateTime dt;
            if (!DateTime.TryParse(value, out dt))
                return value;   // مقدارِ غیرتاریخ دست‌نخورده می‌ماند

            // ToPersianDateString → «1405/05/18»؛ برعکس می‌شود به «18/05/1405».
            string persian = PersianDateHelper.ToPersianDateString(dt);
            string[] parts = persian.Split('/');

            return parts.Length == 3 ? parts[2] + "/" + parts[1] + "/" + parts[0] : persian;
        }
    }
}

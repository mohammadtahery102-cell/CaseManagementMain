using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // موتورِ مشترکِ «خروجی روی فورم‌های رسمیِ Word».
    //
    // هر فورمِ رسمیِ مؤسسه (نامهٔ انتقالی، وکالت موقت، قرارداد ترانسپورت،
    // دریافت حقوق، رخصتی، ماموریت، استخدام) یک فایل .docx در
    // Templates\Forms است که خانه‌های متغیرش با توکنِ {{Name}} علامت خورده.
    // این کلاس قالب را کپی می‌کند، توکن‌ها را با مقدار عوض می‌کند و در صورت
    // درخواست، خروجیِ PDF هم می‌سازد.
    //
    // آموزش — چرا زیرپوشهٔ Forms: ReportTemplateHelper.DiscoverCaseTemplates
    // همهٔ *.docx های ریشهٔ Templates را به‌عنوان «الگوی خروجی پرونده» به
    // کاربر نشان می‌دهد و فقط TopDirectoryOnly را می‌خواند. قرار دادن این
    // قالب‌ها در یک زیرپوشه یعنی هیچ‌کدام در آن فهرست ظاهر نمی‌شوند، بدون
    // آنکه یک خط از کدِ موجود عوض شود.
    //
    // آموزش — چرا جایگزینیِ ساده روی هر <w:t> کافی است: Word معمولاً یک جمله
    // را در چند <w:r> تکه می‌کند و آن‌وقت جست‌وجوی عبارت شکست می‌خورد. این
    // قالب‌ها توسط خودمان ساخته شده‌اند و هر توکن کاملاً داخلِ یک ران است.
    // برای اطمینان، AssertNoTokensLeft در پایان بررسی می‌کند چیزی جا نمانده
    // باشد — پس خروجیِ خراب هرگز بی‌صدا از برنامه بیرون نمی‌رود.
    // ─────────────────────────────────────────────────────────────────────────
    public static class DocxFormExport
    {
        public const string TemplateSubFolder = "Forms";

        // نامِ فایلِ قالبِ هر فورم. یک‌جا نگه داشته می‌شوند تا اگر نامِ فایلی
        // عوض شد، فقط همین‌جا اصلاح گردد.
        public const string TplTransferLetter = "نامه انتقالی.docx";
        public const string TplGuardianProxy  = "وکالت موقت.docx";
        public const string TplDriverContract = "قرارداد خدمات راننده.docx";
        public const string TplSalaryReceipt  = "دریافت حقوق ماهانه.docx";
        public const string TplLeaveRequest   = "درخواست رخصتی.docx";
        public const string TplMissionForm    = "فورم شروع و ختم ماموریت.docx";
        public const string TplJobApplication = "فورم درخواست استخدام.docx";

        // ── یافتنِ قالب ──────────────────────────────────────────────────────
        public static string ResolveTemplate(string templateFileName)
        {
            foreach (string dir in CandidateFolders())
            {
                string candidate = Path.Combine(dir, templateFileName);
                if (File.Exists(candidate)) return candidate;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                "Templates", TemplateSubFolder, templateFileName);
        }

        public static bool TemplateExists(string templateFileName)
        {
            return File.Exists(ResolveTemplate(templateFileName));
        }

        // BaseDirectory و StartupPath هر دو بررسی می‌شوند: در برنامه یکی‌اند،
        // ولی در محیط آزمون میزبان testhost است و پوشه‌اش جای دیگری — همان
        // دلیلی که ReportTemplateHelper هم هر دو را نگاه می‌کند.
        private static IEnumerable<string> CandidateFolders()
        {
            string bd = AppDomain.CurrentDomain.BaseDirectory;
            yield return Path.Combine(bd, "Templates", TemplateSubFolder);
            yield return Path.Combine(bd, "Templates");

            string sp = System.Windows.Forms.Application.StartupPath;
            yield return Path.Combine(sp, "Templates", TemplateSubFolder);
            yield return Path.Combine(sp, "Templates");
        }

        // ── ساختِ خروجیِ Word ────────────────────────────────────────────────
        public static void WriteDocx(string templateFileName, string outPath,
                                     IDictionary<string, string> tokens)
        {
            if (tokens == null) throw new ArgumentNullException("tokens");

            string template = ResolveTemplate(templateFileName);
            if (!File.Exists(template))
                throw new FileNotFoundException(
                    "قالب «" + templateFileName + "» پیدا نشد:" + Environment.NewLine + template);

            string folder = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            File.Copy(template, outPath, true);

            using (WordprocessingDocument doc = WordprocessingDocument.Open(outPath, true))
            {
                ReplaceInPart(doc.MainDocumentPart, tokens);
                foreach (var header in doc.MainDocumentPart.HeaderParts) ReplaceInPart(header, tokens);
                foreach (var footer in doc.MainDocumentPart.FooterParts) ReplaceInPart(footer, tokens);
                doc.MainDocumentPart.Document.Save();
            }

            AssertNoTokensLeft(outPath, template);
        }

        // ── ساختِ خروجیِ PDF ─────────────────────────────────────────────────
        // فایلِ Word میانی در پوشهٔ موقت ساخته و در پایان پاک می‌شود، تا کنارِ
        // PDF یک docx اضافه باقی نماند.
        public static void WritePdf(string templateFileName, string outPdfPath,
                                    IDictionary<string, string> tokens)
        {
            string tempDocx = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".docx");
            try
            {
                WriteDocx(templateFileName, tempDocx, tokens);

                string producedPdf = PdfConversionHelper.ConvertDocxToPdf(tempDocx);

                string folder = Path.GetDirectoryName(outPdfPath);
                if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
                File.Copy(producedPdf, outPdfPath, true);

                TryDelete(producedPdf);
            }
            finally
            {
                TryDelete(tempDocx);
            }
        }

        public static void TryDelete(string path)
        {
            try { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); }
            catch { }
        }

        // ── درونی‌ها ─────────────────────────────────────────────────────────
        private static void ReplaceInPart(OpenXmlPart part, IDictionary<string, string> tokens)
        {
            if (part == null || part.RootElement == null) return;

            foreach (Text t in part.RootElement.Descendants<Text>())
            {
                string original = t.Text;
                if (original == null || original.IndexOf("{{", StringComparison.Ordinal) < 0) continue;

                string updated = original;
                foreach (var pair in tokens)
                    updated = updated.Replace(pair.Key, pair.Value ?? "");

                if (!string.Equals(updated, original, StringComparison.Ordinal))
                {
                    t.Text = updated;

                    // بدونِ این، Word فاصله‌های ابتدا/انتهای متن را حذف می‌کند
                    // و کلمات به هم می‌چسبند.
                    t.Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve;
                }
            }
        }

        // اگر روزی قالب در Word ویرایش شود، ممکن است Word یک توکن را وسطش
        // بشکند (مثلاً {{Head و Name}}). آن‌وقت جایگزینی بی‌صدا انجام نمی‌شود
        // و فورم با {{...}} چاپ می‌گردد. اینجا به‌جای خروجیِ خراب، خطای روشن.
        private static void AssertNoTokensLeft(string path, string templatePath)
        {
            using (WordprocessingDocument doc = WordprocessingDocument.Open(path, false))
            {
                string all = string.Join("", doc.MainDocumentPart.Document.Body
                    .Descendants<Text>().Select(t => t.Text));

                if (all.IndexOf("{{", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException(
                        "قالب ناسازگار است: بعضی توکن‌ها جایگزین نشدند." + Environment.NewLine +
                        "احتمالاً قالب در Word ویرایش شده و یک توکن شکسته است." + Environment.NewLine +
                        "مسیر قالب: " + templatePath);
            }
        }

        // کمکیِ ساختِ نگاشت: DocxFormExport.Tokens().Put("Name", x).Put("Code", y)
        public static Dictionary<string, string> Tokens()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public static Dictionary<string, string> Put(this Dictionary<string, string> map,
                                                     string name, string value)
        {
            map["{{" + name + "}}"] = (value ?? "").Trim();
            return map;
        }
    }
}

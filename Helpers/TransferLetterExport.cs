using System.Collections.Generic;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // «نامهٔ انتقالی» — نگاشتِ داده‌های پرونده به توکن‌های قالب.
    //
    // کلِ کارِ باز کردنِ قالب، جایگزینیِ توکن‌ها، ساختِ PDF و بررسیِ جا نماندنِ
    // توکن، در DocxFormExport انجام می‌شود که بینِ همهٔ فورم‌های رسمی مشترک
    // است. این کلاس فقط می‌داند «کدام مقدار در کدام توکن می‌نشیند».
    // ─────────────────────────────────────────────────────────────────────────
    public static class TransferLetterExport
    {
        public const string TemplateFileName = DocxFormExport.TplTransferLetter;

        public static string TemplatePath { get { return DocxFormExport.ResolveTemplate(TemplateFileName); } }
        public static bool TemplateExists { get { return DocxFormExport.TemplateExists(TemplateFileName); } }

        public sealed class LetterData
        {
            public string Honorific;      // محترم / محترمه
            public string LetterNo;
            public string LetterDate;
            public string HeadName;
            public string FatherName;
            public string Code;
            public string OrphanCount;
            public string FromProvince;
            public string ToProvince;
            public string LastMonth;
            public string LastYear;
            public string PageCount;
        }

        public static void Write(string outPath, LetterData d)
        {
            DocxFormExport.WriteDocx(TemplateFileName, outPath, BuildTokens(d));
        }

        public static void WritePdf(string outPdfPath, LetterData d)
        {
            DocxFormExport.WritePdf(TemplateFileName, outPdfPath, BuildTokens(d));
        }

        private static Dictionary<string, string> BuildTokens(LetterData d)
        {
            if (d == null) throw new System.ArgumentNullException("d");

            return DocxFormExport.Tokens()
                .Put("Honorific", d.Honorific)
                .Put("LetterNo", d.LetterNo)
                .Put("LetterDate", d.LetterDate)
                .Put("HeadName", d.HeadName)
                .Put("FatherName", d.FatherName)
                .Put("Code", d.Code)
                .Put("OrphanCount", d.OrphanCount)
                .Put("FromProvince", d.FromProvince)
                .Put("ToProvince", d.ToProvince)
                .Put("LastMonth", d.LastMonth)
                .Put("LastYear", d.LastYear)
                .Put("PageCount", d.PageCount);
        }
    }
}

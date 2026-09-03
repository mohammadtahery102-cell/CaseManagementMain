using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // انتخابِ الگوی گزارشِ پرونده.
    //
    // آموزش — چرا این کلاس اضافه شد: مسیر قالب Word پیش از این ثابت و
    // تک‌گزینه‌ای بود (فقط FullCaseTemplate.docx). خواسته‌ی کاربر این است که
    // الگوی قدیمیِ خودمان هم در دسترس بماند و هنگام خروجی بتوان یکی را
    // انتخاب کرد. پس به‌جای عوض کردنِ قالبِ فعلی، همه‌ی قالب‌های موجود در
    // پوشه‌ی Templates کشف می‌شوند و اگر بیش از یکی بود از کاربر پرسیده
    // می‌شود.
    //
    // رفتارِ قبلی دست‌نخورده می‌ماند: اگر فقط یک قالب باشد (حالتِ فعلیِ نصب‌ها)
    // هیچ پنجره‌ای باز نمی‌شود و همان قالب بی‌درنگ استفاده می‌گردد.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ReportTemplateHelper
    {
        public const string LastCaseTemplate = "LastCaseTemplate";

        // شناسه‌ی الگوی قدیمیِ RDLC. فایل نیست (منبعِ توکار است)، پس FilePath
        // ندارد و با همین مقدار شناخته می‌شود.
        public const string RdlcKey = "::RDLC::";

        public sealed class TemplateOption
        {
            public string DisplayName { get; set; }
            public string FilePath { get; set; }

            // true = الگوی قدیمیِ RDLC؛ مسیرِ ساختِ خروجی‌اش کاملاً جداست
            // (پنجره‌ی پیش‌نمایشِ ReportViewer، نه موتورِ Word).
            public bool IsRdlc { get; set; }

            public override string ToString() { return DisplayName; }
        }

        // قالب‌های موجود، به ترتیب الفبا. فایل‌های نمونه کنار گذاشته می‌شوند
        // چون خروجیِ واقعی نیستند و انتخابشان فقط باعث اشتباه می‌شود.
        //
        // includeRdlc: فقط جاهایی true است که خروجیِ «یک پرونده» گرفته می‌شود.
        // برای خروجیِ PDF و خروجیِ دسته‌ای false می‌ماند، چون RDLC در آن دو
        // مسیر معادلی ندارد و نشان دادنش فقط انتظارِ برآورده‌نشدنی می‌سازد.
        public static List<TemplateOption> DiscoverCaseTemplates(bool includeRdlc = false)
        {
            var found = new List<TemplateOption>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string dir in CandidateFolders())
            {
                if (!Directory.Exists(dir)) continue;

                string[] files;
                try { files = Directory.GetFiles(dir, "*.docx", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (string f in files)
                {
                    string name = Path.GetFileNameWithoutExtension(f);

                    // ~$ = فایل موقتِ باز بودنِ Word؛ قالب نیست.
                    if (name.StartsWith("~$")) continue;
                    if (name.IndexOf("_Sample", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (!seen.Add(name)) continue;

                    found.Add(new TemplateOption { DisplayName = Friendly(name), FilePath = f });
                }
            }

            found.Sort(delegate (TemplateOption a, TemplateOption b)
            {
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCulture);
            });

            // آموزش — به درخواستِ صریحِ کاربر، الگوی قدیمیِ RDLC از فهرستِ
            // انتخاب حذف شد (کیفیتِ خروجی‌اش برایِ کاربر رضایت‌بخش نبود).
            // includeRdlc دیگر اثری ندارد؛ پارامتر برای سازگاریِ فراخوانی‌های
            // موجود نگه داشته شده.

            return found;
        }

        private static IEnumerable<string> CandidateFolders()
        {
            // همان دو مسیری که GetWordTemplatePath از قدیم نگاه می‌کرد، به
            // همان ترتیب، تا هیچ نصبی رفتارش عوض نشود.
            yield return Application.StartupPath;
            yield return Path.Combine(Application.StartupPath, "Templates");

            // آموزش — چرا BaseDirectory هم لازم است: Application.StartupPath
            // پوشه‌ی فایلِ اجراییِ *میزبان* است. در برنامه همان پوشه‌ی exe است
            // و فرقی نمی‌کند، ولی در محیط آزمون میزبان testhost.exe است و
            // پوشه‌اش جای دیگری است. با افزودن BaseDirectory، همان منطق در
            // آزمون هم قابل راستی‌آزمایی می‌شود بدون آنکه رفتارِ برنامه عوض شود
            // (تکراری‌ها با HashSet کنار گذاشته می‌شوند).
            string bd = AppDomain.CurrentDomain.BaseDirectory;
            yield return bd;
            yield return Path.Combine(bd, "Templates");
        }

        private static string Friendly(string fileName)
        {
            if (string.Equals(fileName, "FullCaseTemplate", StringComparison.OrdinalIgnoreCase))
                return "الگوی کامل پرونده (پیش‌فرض)";

            if (string.Equals(fileName, "NewTemplate", StringComparison.OrdinalIgnoreCase))
                return "الگوی جدید پرونده";

            return fileName.Replace("_", " ");
        }

        // آخرین انتخابِ کاربر، اگر هنوز روی دیسک باشد.
        public static string GetRememberedPath()
        {
            string p = SettingsHelper.Get(LastCaseTemplate, "");
            if (string.IsNullOrWhiteSpace(p)) return "";

            // RDLC فایل نیست، پس نباید با File.Exists سنجیده شود.
            if (string.Equals(p, RdlcKey, StringComparison.Ordinal)) return p;

            return File.Exists(p) ? p : "";
        }

        public static void Remember(string path)
        {
            try { SettingsHelper.Set(LastCaseTemplate, path ?? ""); }
            catch { /* ذخیره‌نشدنِ ترجیح نباید جلوی گرفتنِ خروجی را بگیرد. */ }
        }
    }
}

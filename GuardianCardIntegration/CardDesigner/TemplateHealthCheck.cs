using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;

namespace CaseManagement.GuardianCardIntegration.CardDesigner
{
    public enum HealthLevel { Ok = 0, Suggestion = 1, Warning = 2, Error = 3 }

    public class HealthIssue
    {
        public HealthLevel Level;
        public string Text;

        public HealthIssue(HealthLevel level, string text)
        {
            Level = level; Text = text;
        }

        public string Glyph
        {
            get
            {
                switch (Level)
                {
                    case HealthLevel.Error: return "✕";
                    case HealthLevel.Warning: return "⚠";
                    case HealthLevel.Suggestion: return "ⓘ";
                    default: return "✓";
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // «سلامتِ قالب» — بررسیِ سبک و کاملاً بی‌عارضه (هیچ‌چیزی نمی‌نویسد،
    // نه در دیتابیس نه روی دیسک). فقط چیزهایی را گزارش می‌کند که *قبل از
    // چاپ* قابلِ‌تشخیص‌اند، چون هزینهٔ کشفِ آن‌ها بعد از چاپ روی کارتِ PVC
    // واقعی است.
    //
    // بررسیِ نگاشتِ ستون‌ها از طریقِ یک delegate بیرونی انجام می‌شود تا این
    // کلاس هیچ وابستگی‌ای به لایهٔ داده نداشته باشد و بتوان بدونِ دیتابیس
    // آزمونش کرد.
    // ─────────────────────────────────────────────────────────────────────────
    public static class TemplateHealthCheck
    {
        public static List<HealthIssue> Run(
            string templateName,
            CardTemplateDesign design,
            Dictionary<string, bool> fields,
            bool simple,
            IEnumerable<string> fieldKeys,
            Func<string, string, bool> columnExists)
        {
            var list = new List<HealthIssue>();
            if (design == null) design = new CardTemplateDesign();

            if (string.IsNullOrWhiteSpace(templateName))
                list.Add(new HealthIssue(HealthLevel.Error, "نام کارت خالی است."));

            // ─── فایل‌های تصویری ───────────────────────────────────────────
            CheckImage(list, design.BackgroundFrontPath, "پس‌زمینهٔ روی کارت");
            CheckImage(list, design.BackgroundBackPath, "پس‌زمینهٔ پشت کارت");
            CheckImage(list, design.WatermarkPath, "واترمارک");

            // ─── قلم ───────────────────────────────────────────────────────
            string font = (design.FontFamily ?? "").Trim();
            if (font.Length > 0 && !FontInstalled(font))
                list.Add(new HealthIssue(HealthLevel.Warning,
                    "قلم «" + font + "» روی این کامپیوتر نصب نیست؛ کارت با قلم جایگزین چاپ می‌شود."));

            // ─── اندازهٔ متن ────────────────────────────────────────────────
            if (design.FontScalePercent > 130)
                list.Add(new HealthIssue(HealthLevel.Warning,
                    "اندازهٔ متن " + design.FontScalePercent + "٪ است؛ احتمال سرریز متن روی کارت چاپی وجود دارد."));
            if (design.FontScalePercent > 0 && design.FontScalePercent < 70)
                list.Add(new HealthIssue(HealthLevel.Warning,
                    "اندازهٔ متن " + design.FontScalePercent + "٪ است؛ ممکن است روی کارت چاپی خوانا نباشد."));

            // ─── عناصر امنیتی ──────────────────────────────────────────────
            if (!simple && !design.ShowQRCode && !design.ShowBarcode && !design.HologramEnabled)
                list.Add(new HealthIssue(HealthLevel.Suggestion,
                    "هیچ عنصر امنیتی (QR، بارکد، هولوگرام) روشن نیست."));
            else if (!simple && !design.ShowQRCode)
                list.Add(new HealthIssue(HealthLevel.Suggestion,
                    "QR Code خاموش است. برای اعتبارسنجی سریع کارت می‌توانید روشنش کنید."));

            // ─── جدول پرداخت ───────────────────────────────────────────────
            if (!simple && !string.IsNullOrWhiteSpace(design.LedgerMonthsCsv))
            {
                List<int> months = CardTemplateRepository.ParseLedgerMonths(design.LedgerMonthsCsv);
                if (months == null || months.Count == 0)
                    list.Add(new HealthIssue(HealthLevel.Warning,
                        "هیچ ماهی برای جدول پرداخت انتخاب نشده؛ جدول خالی چاپ می‌شود."));
            }

            // ─── نگاشتِ ستون‌های موردهای روشن ──────────────────────────────
            if (columnExists != null && fieldKeys != null)
            {
                foreach (string key in fieldKeys)
                {
                    bool on = fields == null || !fields.ContainsKey(key) || fields[key];
                    if (!on) continue;

                    CardFieldInfo info = CardFieldCatalog.Get(key, simple);
                    string tech = info.SourceTech ?? "";
                    int dot = tech.IndexOf('.');
                    if (dot <= 0 || !tech.StartsWith("Tbl", StringComparison.Ordinal)) continue;

                    string table = tech.Substring(0, dot);
                    string column = tech.Substring(dot + 1);
                    // فقط ستون‌های ساده؛ عبارت‌هایی مثل MemberRole='یتیم' رد می‌شوند.
                    if (column.IndexOf('=') >= 0 || column.IndexOf(' ') >= 0) continue;

                    bool exists;
                    try { exists = columnExists(table, column); }
                    catch { continue; }

                    if (!exists)
                        list.Add(new HealthIssue(HealthLevel.Error,
                            "«" + info.Label + "» روشن است ولی ستون آن در پایگاه داده پیدا نشد (" + tech + ")."));
                }
            }

            if (list.Count == 0)
                list.Add(new HealthIssue(HealthLevel.Ok, "قالب سالم است — مشکلی پیدا نشد."));

            list.Sort(delegate (HealthIssue a, HealthIssue b) { return b.Level.CompareTo(a.Level); });
            return list;
        }

        private static void CheckImage(List<HealthIssue> list, string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            bool ok;
            try { ok = File.Exists(path); }
            catch { ok = false; }
            if (!ok)
                list.Add(new HealthIssue(HealthLevel.Error, "فایل «" + label + "» پیدا نشد: " + path));
        }

        private static bool FontInstalled(string family)
        {
            try
            {
                using (InstalledFontCollection c = new InstalledFontCollection())
                {
                    FontFamily[] all = c.Families;
                    for (int i = 0; i < all.Length; i++)
                        if (string.Equals(all[i].Name, family, StringComparison.OrdinalIgnoreCase))
                            return true;
                }
                return false;
            }
            catch { return true; } // اگر نتوانستیم بررسی کنیم، هشدارِ الکی نده.
        }
    }
}

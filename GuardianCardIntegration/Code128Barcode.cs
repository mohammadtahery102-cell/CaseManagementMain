using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // مولد بارکد Code128 (زیرمجموعه B) — بدون وابستگی به هیچ بسته خارجی.
    // به‌جای انتظار برای آپلود دستیِ یک تصویر بارکد برای هر پرونده (که عملاً
    // ممکن نیست چون هر کارت شماره‌ی یکتای خودش را دارد)، این کلاس بارکدِ
    // واقعی و معتبر همان «شماره کارت» را در لحظه‌ی چاپ می‌سازد — دقیقاً همان
    // چیزی که جای «بارکد» روی کارت (upload-slot barcode در index.html)
    // انتظارش را دارد.
    // ─────────────────────────────────────────────────────────────────────────
    public static class Code128Barcode
    {
        // جدول استاندارد الگوهای Code128 (مقدار ۰ تا ۱۰۲ = کاراکترها،
        // ۱۰۳/۱۰۴/۱۰۵ = START A/B/C، ۱۰۶ = STOP). هر رشته عرض ماژول‌های
        // متناوب میله/فاصله را نشان می‌دهد (شروع همیشه با میله).
        private static readonly string[] Patterns =
        {
            "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
            "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
            "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
            "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
            "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
            "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
            "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
            "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
            "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
            "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
            "114131","311141","411131","211412","211214","211232","2331112"
        };

        private const int StartB = 104;
        private const int Stop = 106;

        // متن ورودی به بارکدِ Code128-B معتبر تبدیل می‌شود؛ کاراکترهای خارج از
        // بازه‌ی قابل‌پشتیبانی (ASCII 32-126) نادیده گرفته می‌شوند تا تولید
        // بارکد هرگز با استثنا متوقف نشود.
        public static Bitmap Generate(string data, int width = 480, int height = 220)
        {
            string clean = CleanForCode128B(data);
            if (clean.Length == 0) clean = "0";

            List<int> symbolValues = BuildSymbolSequence(clean);

            int totalUnits = 0;
            foreach (int value in symbolValues)
                totalUnits += Patterns[value].Length == 7 ? 13 : 11;

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);

                int quietPx = Math.Max(6, width / 25);
                double moduleWidth = (double)(width - quietPx * 2) / totalUnits;
                int barHeight = (int)(height * 0.72);
                int barTop = (height - barHeight) / 2;

                double x = quietPx;
                using (Brush black = new SolidBrush(Color.Black))
                {
                    foreach (int value in symbolValues)
                    {
                        string pattern = Patterns[value];
                        bool isBar = true;
                        foreach (char digitChar in pattern)
                        {
                            int units = digitChar - '0';
                            double w = units * moduleWidth;
                            if (isBar)
                                g.FillRectangle(black, (float)x, barTop, (float)Math.Ceiling(w), barHeight);
                            x += w;
                            isBar = !isBar;
                        }
                    }
                }
            }

            return bmp;
        }

        // ذخیره مستقیم به فایل PNG — برای مرحله‌ی Stage در GuardianCardRenderer.
        public static void SaveToFile(string data, string filePath, int width = 480, int height = 220)
        {
            using (Bitmap bmp = Generate(data, width, height))
                bmp.Save(filePath, ImageFormat.Png);
        }

        private static string CleanForCode128B(string data)
        {
            if (string.IsNullOrEmpty(data)) return "";
            StringBuilder sb = new StringBuilder(data.Length);
            foreach (char c in data)
                if (c >= 32 && c <= 126) sb.Append(c);
            return sb.ToString();
        }

        private static List<int> BuildSymbolSequence(string clean)
        {
            List<int> dataValues = new List<int>(clean.Length);
            foreach (char c in clean)
                dataValues.Add(c - 32);

            int checksum = StartB;
            for (int i = 0; i < dataValues.Count; i++)
                checksum += dataValues[i] * (i + 1);
            checksum %= 103;

            List<int> sequence = new List<int>(dataValues.Count + 3);
            sequence.Add(StartB);
            sequence.AddRange(dataValues);
            sequence.Add(checksum);
            sequence.Add(Stop);
            return sequence;
        }
    }
}

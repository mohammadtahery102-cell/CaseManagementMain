using System;
using System.Collections.Generic;
using System.Drawing;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // AfghanMapGeometry — هندسهٔ نقشهٔ افغانستان، کاملاً آفلاین و بدونِ
    // کتابخانهٔ بیرونی.
    //
    // آموزش — چرا اینطور و نه GeoJSON: پروژه هیچ فایلِ جغرافیایی ندارد،
    // آفلاین اجرا می‌شود و CLAUDE.md افزودنِ کتابخانهٔ تازه را منع کرده.
    // پس مرزِ کشور به‌صورت یک چندضلعیِ ساده‌شده در خودِ کد است و مرزِ داخلیِ
    // ولایات با «دیاگرامِ توان» (Power Diagram) ساخته می‌شود:
    //
    //     ولایتِ هر پیکسل = ولایتی که کمترین مقدارِ (فاصله² − وزن²) را دارد
    //
    // وزنِ هر ولایت متناسب با ریشهٔ مساحتِ واقعی‌اش است، پس هلمند و قندهار
    // واقعاً پهن دیده می‌شوند و کاپیسا و پنجشیر کوچک — چیزی که Voronoiِ سادهٔ
    // بدونِ وزن نمی‌دهد.
    //
    // ⚠ نتیجه «شِماتیک» است نه مرزِ حقوقی: موقعیت، همسایگی و اندازهٔ نسبی
    // درست است، ولی خطِ مرزِ دقیقِ ولایت نیست. فرم همین را برچسب می‌زند.
    //
    // کارایی: نگاشتِ پیکسل→منطقه یک‌بار ساخته و کش می‌شود. رنگ‌آمیزیِ دوباره
    // (تعویضِ معیارِ نقشهٔ حرارتی) فقط یک پیمایشِ آرایه است، نه ساختِ دوبارهٔ
    // هندسه. hit-test هم O(1) است: خواندنِ یک خانه از همان آرایه.
    // ═══════════════════════════════════════════════════════════════════════
    public static class AfghanMapGeometry
    {
        // ───────────────────────────────────────────────────────────────────
        // مرزِ کشور — نقاط (طولِ جغرافیایی، عرضِ جغرافیایی) در جهتِ ساعتگرد.
        // شاملِ دالانِ واخان در شمالِ شرق که شکلِ شناخته‌شدهٔ کشور را می‌سازد.
        // ───────────────────────────────────────────────────────────────────
        private static readonly double[,] BorderLonLat =
        {
            { 61.27, 35.61 }, { 62.20, 35.25 }, { 63.10, 35.67 }, { 63.98, 36.03 },
            { 64.75, 36.30 }, { 65.55, 37.25 }, { 66.52, 37.35 }, { 67.00, 37.23 },
            { 67.75, 37.17 }, { 68.30, 37.00 }, { 68.85, 37.33 }, { 69.30, 37.11 },
            { 69.95, 37.60 }, { 70.20, 37.58 }, { 70.60, 37.95 }, { 71.10, 38.40 },
            { 71.35, 38.26 }, { 71.60, 38.05 }, { 72.20, 37.95 }, { 72.60, 37.25 },
            { 73.20, 37.45 }, { 73.80, 37.30 }, { 74.55, 37.20 }, { 74.89, 37.05 },
            { 74.50, 36.95 }, { 73.70, 37.02 }, { 72.90, 37.08 }, { 72.20, 36.80 },
            { 71.60, 36.50 }, { 71.30, 36.10 }, { 71.55, 35.65 }, { 71.10, 35.25 },
            { 71.65, 35.05 }, { 71.10, 34.55 }, { 70.90, 34.05 }, { 70.30, 33.70 },
            { 69.90, 33.20 }, { 69.50, 32.80 }, { 69.30, 32.10 }, { 68.80, 31.70 },
            { 68.30, 31.75 }, { 67.80, 31.30 }, { 67.30, 31.22 }, { 66.60, 31.00 },
            { 66.35, 30.95 }, { 65.10, 29.55 }, { 64.20, 29.50 }, { 63.60, 29.50 },
            { 62.50, 29.42 }, { 60.87, 29.86 }, { 61.80, 30.90 }, { 61.70, 31.40 },
            { 60.85, 31.50 }, { 60.55, 32.00 }, { 60.60, 33.10 }, { 60.90, 33.55 },
            { 60.50, 34.10 }, { 60.90, 34.60 }, { 61.10, 35.10 }
        };

        // ───────────────────────────────────────────────────────────────────
        // ۳۴ ولایت: نام (دقیقاً مطابق TblLookup و AfghanGeoData)، مرکزِ اداری
        // و مساحتِ تقریبی (کیلومترِ مربع) که وزنِ دیاگرامِ توان از آن می‌آید.
        // ───────────────────────────────────────────────────────────────────
        private static readonly object[][] ProvinceSeed =
        {
            //        نام          عرض     طول    مساحت
            new object[] { "کابل",     34.53, 69.17,  4462.0 },
            new object[] { "هرات",     34.35, 62.20, 54778.0 },
            new object[] { "بلخ",      36.71, 67.11, 17249.0 },
            new object[] { "قندهار",   31.62, 65.72, 54022.0 },
            new object[] { "ننگرهار",  34.43, 70.45,  7727.0 },
            new object[] { "بدخشان",   37.12, 70.58, 44059.0 },
            new object[] { "بغلان",    35.95, 68.70, 21118.0 },
            new object[] { "تخار",     36.73, 69.53, 12333.0 },
            new object[] { "غزنی",     33.55, 68.42, 22915.0 },
            new object[] { "هلمند",    31.59, 64.37, 58584.0 },
            new object[] { "لغمان",    34.66, 70.21,  3843.0 },
            new object[] { "کندز",     36.73, 68.86,  8040.0 },
            new object[] { "فاریاب",   35.92, 64.78, 20293.0 },
            new object[] { "جوزجان",   36.67, 65.75, 11798.0 },
            new object[] { "سمنگان",   36.26, 68.02, 11262.0 },
            new object[] { "بامیان",   34.82, 67.83, 14175.0 },
            new object[] { "پکتیا",    33.60, 69.23,  6432.0 },
            new object[] { "لوگر",     34.00, 69.04,  4568.0 },
            new object[] { "وردک",     34.40, 68.57,  9023.0 },
            new object[] { "غور",      34.52, 65.25, 36479.0 },
            new object[] { "فراه",     32.37, 62.12, 48471.0 },
            new object[] { "خوست",     33.34, 69.92,  4152.0 },
            new object[] { "کاپیسا",   34.99, 69.60,  1842.0 },
            new object[] { "پروان",    35.01, 69.17,  5974.0 },
            new object[] { "زابل",     32.11, 66.91, 17343.0 },
            new object[] { "ارزگان",   32.90, 65.87, 22696.0 },
            new object[] { "نیمروز",   30.96, 62.30, 41005.0 },
            new object[] { "نورستان",  35.42, 70.92,  9225.0 },
            new object[] { "کنر",      34.87, 71.15,  4942.0 },
            new object[] { "سرپل",     36.00, 65.93, 16360.0 },
            new object[] { "دایکندی",  33.72, 66.13, 18088.0 },
            new object[] { "پکتیکا",   32.75, 68.73, 19482.0 },
            new object[] { "بادغیس",   35.17, 63.13, 20591.0 },
            new object[] { "پنجشیر",   35.31, 69.90,  3610.0 }
        };

        public sealed class ProvinceSite
        {
            public string Name;
            public double Lat, Lon, AreaKm2;
            public double Weight;   // وزنِ دیاگرامِ توان (واحدِ درجه)
        }

        private static readonly List<ProvinceSite> _sites = BuildSites();

        private static List<ProvinceSite> BuildSites()
        {
            List<ProvinceSite> list = new List<ProvinceSite>();
            foreach (object[] row in ProvinceSeed)
            {
                double area = (double)row[3];
                list.Add(new ProvinceSite
                {
                    Name = (string)row[0],
                    Lat = (double)row[1],
                    Lon = (double)row[2],
                    AreaKm2 = area,
                    // آموزش — ضریب ۰٫۰۰۳۵ تجربی است: وزن باید آن‌قدر باشد که
                    // ولایتِ بزرگ واقعاً بزرگ شود، ولی نه آن‌قدر که ولایتِ
                    // کوچکِ همسایه را کاملاً ببلعد و مرکزِ خودش را از دست بدهد.
                    Weight = Math.Sqrt(area) * 0.0035
                });
            }
            return list;
        }

        public static IList<ProvinceSite> Sites { get { return _sites; } }

        public static string[] ProvinceNames()
        {
            string[] names = new string[_sites.Count];
            for (int i = 0; i < _sites.Count; i++) names[i] = _sites[i].Name;
            return names;
        }

        public static int IndexOfProvince(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;
            string n = name.Trim();
            for (int i = 0; i < _sites.Count; i++)
                if (string.Equals(_sites[i].Name, n, StringComparison.Ordinal)) return i;
            return -1;
        }

        // ═══════════════════════════════════════════════════════════════════
        // تصویرسازی (Projection)
        //
        // آموزش — تصویرِ استوانه‌ایِ ساده با تصحیحِ عرضِ جغرافیایی: طولِ
        // جغرافیایی در cos(عرضِ میانه) ضرب می‌شود وگرنه کشور کشیده و پهن
        // دیده می‌شود. برای کشوری با این وسعت، خطای این روش چشمی نیست.
        // ═══════════════════════════════════════════════════════════════════
        private const double MinLon = 60.40, MaxLon = 75.00;
        private const double MinLat = 29.30, MaxLat = 38.55;
        private static readonly double LonScale = Math.Cos((MinLat + MaxLat) / 2.0 * Math.PI / 180.0);

        public sealed class Projection
        {
            public float Scale;
            public float OffsetX, OffsetY;
            public int Width, Height;

            public PointF ToScreen(double lon, double lat)
            {
                float x = OffsetX + (float)((lon - MinLon) * LonScale * Scale);
                float y = OffsetY + (float)((MaxLat - lat) * Scale);
                return new PointF(x, y);
            }
        }

        public static Projection BuildProjection(int width, int height, int padding)
        {
            double spanX = (MaxLon - MinLon) * LonScale;
            double spanY = (MaxLat - MinLat);

            double usableW = Math.Max(10, width - padding * 2);
            double usableH = Math.Max(10, height - padding * 2);
            double scale = Math.Min(usableW / spanX, usableH / spanY);

            Projection p = new Projection
            {
                Scale = (float)scale,
                Width = width,
                Height = height
            };
            p.OffsetX = (float)((width - spanX * scale) / 2.0);
            p.OffsetY = (float)((height - spanY * scale) / 2.0);
            return p;
        }

        public static PointF[] BorderPolygon(Projection p)
        {
            PointF[] pts = new PointF[BorderLonLat.GetLength(0)];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = p.ToScreen(BorderLonLat[i, 0], BorderLonLat[i, 1]);
            return pts;
        }

        // ═══════════════════════════════════════════════════════════════════
        // پارتیشن — نگاشتِ پیکسل → شاخصِ منطقه.
        //
        // Cells[y * Width + x] برابرِ شاخصِ منطقه است؛ -1 یعنی بیرونِ کشور.
        // این ساختار هم برای رنگ‌آمیزی به کار می‌رود، هم برای hit-test و هم
        // برای پیدا کردنِ مرزها (پیکسلی که همسایه‌اش شاخصِ دیگری دارد).
        // ═══════════════════════════════════════════════════════════════════
        public sealed class Partition
        {
            public int Width, Height;
            public short[] Cells;
            public string[] Names;
            public PointF[] LabelAnchors;   // نقطهٔ امنِ نوشتنِ برچسب در هر ناحیه
            public int[] PixelCounts;
            // کادرِ محیطیِ هر ناحیه. کنترل هنگامِ برجسته‌کردنِ مرزِ یک ولایت
            // فقط همین کادر را می‌پیماید، نه کلِ صفحه — وگرنه هر حرکتِ ماوس
            // یک پیمایشِ ششصدهزارتایی می‌شد.
            public Rectangle[] Bounds;
            public Projection Proj;

            public int RegionAt(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return -1;
                return Cells[y * Width + x];
            }

            public string NameAt(int x, int y)
            {
                int i = RegionAt(x, y);
                return (i < 0 || i >= Names.Length) ? "" : Names[i];
            }
        }

        // ─── سطح ۱: کلِ کشور، ۳۴ ولایت ──────────────────────────────────────
        public static Partition BuildProvincePartition(int width, int height, int padding)
        {
            return BuildProvincePartition(width, height, BuildProjection(width, height, padding));
        }

        public static Partition BuildProvincePartition(int width, int height, Projection proj)
        {
            PointF[] border = BorderPolygon(proj);

            int n = _sites.Count;
            float[] sx = new float[n], sy = new float[n];
            float[] w2 = new float[n];
            for (int i = 0; i < n; i++)
            {
                PointF pt = proj.ToScreen(_sites[i].Lon, _sites[i].Lat);
                sx[i] = pt.X; sy[i] = pt.Y;
                float wpx = (float)(_sites[i].Weight * proj.Scale);
                w2[i] = wpx * wpx;
            }

            Partition part = new Partition
            {
                Width = width,
                Height = height,
                Cells = new short[width * height],
                Names = ProvinceNames(),
                PixelCounts = new int[n],
                Proj = proj
            };

            bool[] inside = RasterizePolygon(border, width, height);

            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = rowBase + x;
                    if (!inside[idx]) { part.Cells[idx] = -1; continue; }

                    int best = 0;
                    float bestVal = float.MaxValue;
                    for (int i = 0; i < n; i++)
                    {
                        float dx = x - sx[i], dy = y - sy[i];
                        float val = dx * dx + dy * dy - w2[i];
                        if (val < bestVal) { bestVal = val; best = i; }
                    }
                    part.Cells[idx] = (short)best;
                    part.PixelCounts[best]++;
                }
            }

            part.LabelAnchors = ComputeLabelAnchors(part, n);
            return part;
        }

        // ═══════════════════════════════════════════════════════════════════
        // سطح ۲ با بزرگ‌نمایی — ولایتِ انتخاب‌شده کلِ بوم را پر می‌کند.
        //
        // آموزش — چرا این لازم است: اگر ولسوالی‌ها روی همان تصویرِ کلِ کشور
        // رسم شوند، ولایتی مثل بلخ فقط چند درصدِ بوم را می‌گیرد و برچسب‌های
        // ولسوالی روی هم می‌افتند و خوانده نمی‌شوند. پس در دو گذر کار می‌کنیم:
        // گذرِ اول کادرِ محیطیِ ولایت را پیدا می‌کند، گذرِ دوم با تصویرسازیِ
        // بزرگ‌نمایی‌شده دوباره می‌سازد تا همان کادر تمامِ بوم را پر کند.
        // ═══════════════════════════════════════════════════════════════════
        public static Partition BuildZoomedDistrictPartition(int width, int height, int padding,
                                                            string provinceName)
        {
            int index = IndexOfProvince(provinceName);
            if (index < 0) return null;

            Projection baseProj = BuildProjection(width, height, padding);
            Partition pass1 = BuildProvincePartition(width, height, baseProj);

            Rectangle box = pass1.Bounds[index];
            if (box.IsEmpty || box.Width < 2 || box.Height < 2) return null;

            double usableW = Math.Max(10, width - padding * 2.0);
            double usableH = Math.Max(10, height - padding * 2.0);
            double zoom = Math.Min(usableW / box.Width, usableH / box.Height);
            if (zoom < 1.0) zoom = 1.0;          // ولایتِ بزرگ‌تر از بوم بزرگ‌نمایی نمی‌خواهد

            double centerX = box.X + box.Width / 2.0;
            double centerY = box.Y + box.Height / 2.0;

            // نگاشتِ خطی: x' = OffsetX' + (x - OffsetX) * zoom، و مرکزِ کادر
            // باید روی مرکزِ بوم بیفتد.
            Projection zoomed = new Projection
            {
                Scale = (float)(baseProj.Scale * zoom),
                Width = width,
                Height = height,
                OffsetX = (float)(width / 2.0 - (centerX - baseProj.OffsetX) * zoom),
                OffsetY = (float)(height / 2.0 - (centerY - baseProj.OffsetY) * zoom)
            };

            Partition pass2 = BuildProvincePartition(width, height, zoomed);
            return BuildDistrictPartition(pass2, provinceName);
        }

        // ─── سطح ۲: ولسوالی‌های یک ولایت ────────────────────────────────────
        //
        // آموزش — مختصاتِ ولسوالی‌ها در هیچ‌جای پروژه نیست و ساختنِ دستیِ
        // چند صد نقطه نه شدنی است نه قابلِ نگهداری. پس ناحیهٔ ولایت به تعدادِ
        // ولسوالی‌هایش تقسیم می‌شود و نقاطِ اولیه با الگوریتمِ Lloyd (k-means)
        // آن‌قدر جابه‌جا می‌شوند تا ناحیه‌ها هم‌اندازه و به‌هم‌پیوسته شوند.
        // ولسوالیِ مرکزی (اولین عضوِ فهرست) عمداً نزدیکِ مرکزِ ولایت می‌ماند.
        // این «تقسیمِ شِماتیک» است و در فرم هم با همین عبارت برچسب می‌خورد.
        public static Partition BuildDistrictPartition(Partition provinces, string provinceName)
        {
            int pIdx = IndexOfProvince(provinceName);
            string[] districts = AfghanGeoData.GetDistricts(provinceName);
            if (pIdx < 0 || districts.Length == 0) return null;

            int w = provinces.Width, h = provinces.Height;

            List<int> pixels = new List<int>();
            for (int i = 0; i < provinces.Cells.Length; i++)
                if (provinces.Cells[i] == pIdx) pixels.Add(i);
            if (pixels.Count < districts.Length * 4) return null;

            int k = districts.Length;
            float[] cx = new float[k], cy = new float[k];

            // بذرهای اولیه: مرکزِ ولایت برای ولسوالیِ مرکزی، بقیه روی یک
            // مارپیچِ فیبوناچی تا از اول پخش باشند و به همگرایی کمک کنند.
            PointF anchor = provinces.LabelAnchors[pIdx];
            double radius = Math.Sqrt(pixels.Count / Math.PI) * 0.62;
            for (int i = 0; i < k; i++)
            {
                if (i == 0) { cx[i] = anchor.X; cy[i] = anchor.Y; continue; }
                double t = i / (double)k;
                double ang = i * 2.3999632297;          // زاویهٔ طلایی
                double r = radius * Math.Sqrt(t);
                cx[i] = anchor.X + (float)(r * Math.Cos(ang));
                cy[i] = anchor.Y + (float)(r * Math.Sin(ang));
            }

            short[] owner = new short[pixels.Count];

            for (int iter = 0; iter < 12; iter++)
            {
                double[] sumX = new double[k], sumY = new double[k];
                int[] count = new int[k];

                for (int p = 0; p < pixels.Count; p++)
                {
                    int idx = pixels[p];
                    int x = idx % w, y = idx / w;
                    int best = 0; float bestD = float.MaxValue;
                    for (int i = 0; i < k; i++)
                    {
                        float dx = x - cx[i], dy = y - cy[i];
                        float d = dx * dx + dy * dy;
                        if (d < bestD) { bestD = d; best = i; }
                    }
                    owner[p] = (short)best;
                    sumX[best] += x; sumY[best] += y; count[best]++;
                }

                for (int i = 0; i < k; i++)
                {
                    if (count[i] == 0) continue;    // خوشهٔ خالی سرِ جایش می‌ماند
                    cx[i] = (float)(sumX[i] / count[i]);
                    cy[i] = (float)(sumY[i] / count[i]);
                }
            }

            Partition part = new Partition
            {
                Width = w,
                Height = h,
                Cells = new short[w * h],
                Names = districts,
                PixelCounts = new int[k],
                Proj = provinces.Proj
            };
            for (int i = 0; i < part.Cells.Length; i++) part.Cells[i] = -1;
            for (int p = 0; p < pixels.Count; p++)
            {
                part.Cells[pixels[p]] = owner[p];
                part.PixelCounts[owner[p]]++;
            }

            part.LabelAnchors = ComputeLabelAnchors(part, k);
            return part;
        }

        // ═══════════════════════════════════════════════════════════════════
        // جای امنِ برچسب — «قطبِ دسترسی‌ناپذیری» به‌صورت تقریبی.
        //
        // آموزش — مرکزِ ثقلِ یک ناحیهٔ هلالی می‌تواند بیرونِ خودِ ناحیه بیفتد و
        // برچسب روی ولایتِ همسایه بنشیند. پس به‌جای مرکزِ ثقل، پیکسلی انتخاب
        // می‌شود که بیشترین فاصله را تا مرزِ ناحیه دارد (با تبدیلِ فاصلهٔ
        // دومرحله‌ایِ چبیشف که سریع و کافی است).
        // ═══════════════════════════════════════════════════════════════════
        private static PointF[] ComputeLabelAnchors(Partition part, int regionCount)
        {
            int w = part.Width, h = part.Height;
            int[] dist = new int[w * h];
            const int Far = 1 << 20;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    short r = part.Cells[i];
                    if (r < 0) { dist[i] = 0; continue; }

                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                             || part.Cells[i - 1] != r || part.Cells[i + 1] != r
                             || part.Cells[i - w] != r || part.Cells[i + w] != r;
                    dist[i] = edge ? 0 : Far;
                }

            // گذرِ رفت (بالا-چپ به پایین-راست)
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    if (dist[i] == 0) continue;
                    int m = Math.Min(Math.Min(dist[i - 1], dist[i - w]),
                                     Math.Min(dist[i - w - 1], dist[i - w + 1]));
                    if (m + 1 < dist[i]) dist[i] = m + 1;
                }
            // گذرِ برگشت
            for (int y = h - 2; y >= 1; y--)
                for (int x = w - 2; x >= 1; x--)
                {
                    int i = y * w + x;
                    if (dist[i] == 0) continue;
                    int m = Math.Min(Math.Min(dist[i + 1], dist[i + w]),
                                     Math.Min(dist[i + w - 1], dist[i + w + 1]));
                    if (m + 1 < dist[i]) dist[i] = m + 1;
                }

            PointF[] anchors = new PointF[regionCount];

            // آموزش — چرا مقدارِ اولیه ۱- است و نه ۰: ناحیهٔ خیلی باریک ممکن
            // است هیچ پیکسلِ «غیرمرزی» نداشته باشد و فاصلهٔ همهٔ پیکسل‌هایش
            // صفر شود. با مقدارِ اولیهٔ صفر، شرطِ «بزرگ‌تر» هرگز برقرار
            // نمی‌شد و برچسبِ آن ناحیه روی (۰،۰) — یعنی گوشهٔ نقشه — می‌افتاد.
            int[] bestDist = new int[regionCount];
            for (int r = 0; r < regionCount; r++) bestDist[r] = -1;

            int[] minX = new int[regionCount], minY = new int[regionCount];
            int[] maxX = new int[regionCount], maxY = new int[regionCount];
            for (int r = 0; r < regionCount; r++)
            {
                minX[r] = int.MaxValue; minY[r] = int.MaxValue;
                maxX[r] = int.MinValue; maxY[r] = int.MinValue;
            }

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    short r = part.Cells[i];
                    if (r < 0 || r >= regionCount) continue;

                    if (x < minX[r]) minX[r] = x;
                    if (y < minY[r]) minY[r] = y;
                    if (x > maxX[r]) maxX[r] = x;
                    if (y > maxY[r]) maxY[r] = y;

                    if (dist[i] > bestDist[r])
                    {
                        bestDist[r] = dist[i];
                        anchors[r] = new PointF(x, y);
                    }
                }

            part.Bounds = new Rectangle[regionCount];
            for (int r = 0; r < regionCount; r++)
            {
                part.Bounds[r] = maxX[r] < minX[r]
                    ? Rectangle.Empty
                    : Rectangle.FromLTRB(minX[r], minY[r], maxX[r] + 1, maxY[r] + 1);
            }
            return anchors;
        }

        // ═══════════════════════════════════════════════════════════════════
        // رستر کردنِ چندضلعی با قاعدهٔ زوج-فرد (scanline).
        // ═══════════════════════════════════════════════════════════════════
        private static bool[] RasterizePolygon(PointF[] poly, int width, int height)
        {
            bool[] inside = new bool[width * height];
            int n = poly.Length;
            List<float> xs = new List<float>(16);

            for (int y = 0; y < height; y++)
            {
                float scanY = y + 0.5f;
                xs.Clear();

                for (int i = 0; i < n; i++)
                {
                    PointF a = poly[i];
                    PointF b = poly[(i + 1) % n];
                    if (a.Y == b.Y) continue;
                    if ((scanY >= a.Y && scanY < b.Y) || (scanY >= b.Y && scanY < a.Y))
                        xs.Add(a.X + (scanY - a.Y) / (b.Y - a.Y) * (b.X - a.X));
                }
                if (xs.Count < 2) continue;
                xs.Sort();

                int rowBase = y * width;
                for (int i = 0; i + 1 < xs.Count; i += 2)
                {
                    int x0 = (int)Math.Ceiling(xs[i] - 0.5f);
                    int x1 = (int)Math.Floor(xs[i + 1] - 0.5f);
                    if (x1 < 0 || x0 >= width) continue;
                    if (x0 < 0) x0 = 0;
                    if (x1 >= width) x1 = width - 1;
                    for (int x = x0; x <= x1; x++) inside[rowBase + x] = true;
                }
            }
            return inside;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // GeoReportComposer — ساختِ گزارشِ «نقشه + داده، کنارِ هم».
    //
    // خواستهٔ صریحِ مالکِ محصول: خروجیِ PDF باید هم تصویرِ نقشه را داشته باشد
    // هم دادهٔ همان صفحه را، کنارِ هم — نه دو فایلِ جدا.
    //
    // آموزش — چرا «پوستر» به‌صورت یک تصویرِ واحد ساخته می‌شود و نه با
    // بلوک‌های ReportDoc: ReportDoc یک موتورِ جریانیِ تک‌ستونه است و بلوک‌ها
    // پشتِ سرِ هم می‌آیند؛ «کنارِ هم» در آن وجود ندارد. به‌جای دستکاریِ آن
    // موتورِ مشترک (که همهٔ گزارش‌های دیگرِ سیستم رویش سوارند و ریسکِ
    // بی‌مورد دارد)، صفحهٔ اولِ گزارش به‌صورت یک بیت‌مپِ باکیفیتِ افقی
    // ترسیم می‌شود و همان تصویر داخلِ سند می‌نشیند. صفحه‌های بعد، جدول‌های
    // کاملِ داده‌اند که با همان ReportDoc چیده می‌شوند.
    //
    // نتیجه: یک PDF با صفحهٔ اولِ «داشبوردِ چاپی» و صفحه‌های بعدیِ دادهٔ کامل.
    // ═══════════════════════════════════════════════════════════════════════
    public static class GeoReportComposer
    {
        // ═══════════════════════════════════════════════════════════════════
        // ابعادِ پوستر.
        //
        // آموزش — چرا این نسبت و این اندازه: ReportDoc تصویر را با حفظِ نسبتِ
        // ابعاد داخلِ کادرش می‌نشاند (DrawFitted). اگر نسبتِ پوستر با نسبتِ
        // کادرِ چاپ فرق کند، دو نوارِ خالی کنارش می‌ماند و نقشه بی‌دلیل کوچک
        // می‌شود. کادرِ چاپ روی A4 افقی تقریباً ۱۰۷۹×۵۴۰ (یک‌صدمِ اینچ) است،
        // یعنی نسبتِ ۲:۱ — پس پوستر هم دقیقاً ۲:۱ ساخته می‌شود تا تمامِ عرض
        // و ارتفاعِ صفحه را بگیرد.
        //
        // اندازهٔ پیکسلی روی ۲۰۰ نقطه‌بر‌اینچ حساب شده (نه ۱۵۰): خواستهٔ صریحِ
        // مالکِ محصول این بود که «خروجی PDF بسیار واضح بیاید». با ۲۰۰dpi
        // متنِ ۸ پوینتی در چاپ و در بزرگ‌نماییِ صفحه هر دو تیز می‌ماند.
        // نسبتِ ۱٫۹۰ عمداً بینِ دو اندازهٔ کاغذ است: کادرِ تصویر روی A4 افقی
        // نسبتِ ~۱٫۹۸ دارد و روی Letter افقی ~۱٫۸۵. با یک نسبتِ میانه، روی هر
        // دو کاغذ کمتر از ۴٪ از صفحه هدر می‌رود — و چون چاپگرِ پیش‌فرضِ هر
        // دستگاه فرق می‌کند، نمی‌توان یکی را فرض گرفت.
        private const int PosterWidth = 2560;
        private const int PosterHeight = 1347;

        // آموزش — این عدد «DPIِ فضای طراحی» است، نه وضوحِ خروجی. قلم‌ها بر حسبِ
        // پوینت‌اند، پس اندازهٔ پیکسلی‌شان = points × DPI ÷ ۷۲. اگر این عدد را
        // بالا ببریم، متن نسبت به مختصاتِ ثابتِ چیدمان بزرگ می‌شود و از کادرش
        // بیرون می‌زند (همین اشتباه یک بار رخ داد: با ۲۰۰، برچسب‌ها ۴۳٪ بزرگ
        // شدند و همه با سه‌نقطه بریده می‌شدند).
        //
        // وضوحِ واقعیِ خروجی از ScaleTransform می‌آید: ۱۵۰ × (۲۵۶۰ ÷ ۱۷۵۰) ≈
        // ۲۱۹ نقطه‌بر‌اینچ مؤثر — بیش از کافی برای چاپ و بزرگ‌نمایی.
        private const float DesignDpi = 150f;

        // فضای مختصاتِ طراحی. کلِ کدِ رسم با این اعداد نوشته شده و در پایان با
        // یک ScaleTransform به اندازهٔ واقعیِ بیت‌مپ بزرگ می‌شود.
        //
        // آموزش — چرا این کار لازم است: اگر مستقیم روی بیت‌مپِ ۲۰۰dpi رسم
        // کنیم، قلم‌ها (که بر حسبِ پوینت‌اند) با DPI بزرگ می‌شوند ولی مختصاتِ
        // ثابتِ چیدمان (که پیکسل‌اند) نه — و متن از کادرش بیرون می‌زند. با
        // فضای طراحیِ ثابت، نسبتِ متن به کادر همیشه یکی می‌ماند و تغییرِ DPI
        // فقط وضوح را بالا می‌برد، نه چیدمان را خراب.
        private const float DesignWidth = 1750f;
        private const float DesignHeight = 921f;       // همان نسبتِ ۱٫۹۰ پوستر

        // ارتفاعِ کادرِ تصویر در سندِ ReportDoc (یک‌صدمِ اینچ). با سربرگِ
        // کوتاهِ سه‌فیلدی، این بیشترین مقداری است که در بدنهٔ صفحه جا می‌شود.
        private const float PosterCellHeight = 540f;

        // سهمِ نقشه از عرضِ پوستر. خواستهٔ مالکِ محصول: «نقشه تا می‌شود بزرگ
        // باشد، حتی نصفِ صفحه هم مشکلی ندارد».
        private const float MapShare = 0.54f;

        private static readonly Color Ink = ColorTranslator.FromHtml("#1F2A36");
        private static readonly Color InkMuted = ColorTranslator.FromHtml("#69778A");
        private static readonly Color Rule = ColorTranslator.FromHtml("#D8E0EA");
        private static readonly Color Band = ColorTranslator.FromHtml("#1B3A5C");
        private static readonly Color Accent = ColorTranslator.FromHtml("#2C5A85");
        private static readonly Color SoftFill = ColorTranslator.FromHtml("#F4F7FA");

        public sealed class ReportInput
        {
            public string Title = "گزارش تحلیلی جغرافیایی";
            public string ScopeLabel = "کل کشور";
            public string FilterDescription = "";
            public string MetricTitle = "";

            public Bitmap MapImage;                                   // تصویرِ نقشه (مالکیتش با فراخواننده)
            public GeoAnalyticsService.RegionStats Totals;
            public List<GeoAnalyticsService.RegionStats> Regions;
            public List<GeoAnalyticsService.BreakdownRow> RequestTypes;
            public List<GeoAnalyticsService.BreakdownRow> ServiceStatuses;
            public List<GeoAnalyticsService.BreakdownRow> EconomicPriorities;
            public List<GeoAnalyticsService.BreakdownRow> Sponsorship;
            public List<GeoInsightEngine.Insight> Insights;
            public GeoAnalyticsService.AssistanceStats Assistance;
            public bool DistrictLevel;

            // برچسبِ گوشهٔ سربرگ در گزارشِ چندولایتی، مثلاً «ولایت ۳ از ۷».
            public string PageTag = "";
        }

        // ═══════════════════════════════════════════════════════════════════
        // پوستر: نقشه در سمتِ راست (جهتِ خواندنِ RTL)، داده در سمتِ چپ.
        // ═══════════════════════════════════════════════════════════════════
        public static Bitmap BuildPoster(ReportInput input)
        {
            Bitmap bmp = new Bitmap(PosterWidth, PosterHeight);
            bmp.SetResolution(DesignDpi, DesignDpi);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // از اینجا به بعد همه‌چیز در فضای طراحی است.
                g.ScaleTransform(PosterWidth / DesignWidth, PosterHeight / DesignHeight);

                int width = (int)DesignWidth;
                int height = (int)DesignHeight;

                int margin = 26;
                int headerH = DrawHeader(g, input, margin, width - margin * 2);

                int contentTop = margin + headerH + 12;
                int contentH = height - contentTop - margin - 20;

                // ستونِ راست = نقشه، ستونِ چپ = داده.
                int mapW = (int)((width - margin * 2) * MapShare);
                int dataW = width - margin * 2 - mapW - 16;

                Rectangle mapBox = new Rectangle(width - margin - mapW, contentTop, mapW, contentH);
                Rectangle dataBox = new Rectangle(margin, contentTop, dataW, contentH);

                DrawMapPanel(g, input, mapBox);
                DrawDataColumn(g, input, dataBox);
                DrawFooter(g, input, margin, width, height);
            }
            return bmp;
        }

        private static int DrawHeader(Graphics g, ReportInput input, int x, int width)
        {
            int h = 84;
            Rectangle box = new Rectangle(x, 22, width, h);

            using (LinearGradientBrush lg = new LinearGradientBrush(
                       box, Band, Accent, LinearGradientMode.Horizontal))
                g.FillRectangle(lg, box);

            using (Font title = Fnt(19f, FontStyle.Bold))
            using (Font sub = Fnt(10.5f))
            using (Font tiny = Fnt(8.5f))
            using (Brush white = new SolidBrush(Color.White))
            using (Brush faint = new SolidBrush(Color.FromArgb(205, Color.White)))
            using (StringFormat sf = Rtl(StringAlignment.Near))
            using (StringFormat sfLeft = Rtl(StringAlignment.Far))
            {
                g.DrawString(input.Title, title, white,
                    new RectangleF(box.X + 16, box.Y + 8, box.Width - 32, 28), sf);

                g.DrawString("محدوده: " + input.ScopeLabel + "   •   معیار نقشه: " + input.MetricTitle,
                    sub, faint, new RectangleF(box.X + 16, box.Y + 38, box.Width - 32, 20), sf);

                string filters = string.IsNullOrWhiteSpace(input.FilterDescription)
                               ? "بدون فیلتر" : input.FilterDescription;
                g.DrawString("فیلترها: " + filters, tiny, faint,
                    new RectangleF(box.X + 16, box.Y + 58, box.Width - 32, 18), sf);

                g.DrawString(PersianDateHelper.ToPersianDateTimeStringSafe(DateTime.Now),
                    tiny, faint, new RectangleF(box.X + 16, box.Y + 10, box.Width - 32, 18), sfLeft);

                // شمارهٔ صفحه/بخش در گزارش‌های چندولایتی، تا خواننده بداند
                // کدام ولایت را می‌بیند و کل چند تاست.
                if (!string.IsNullOrWhiteSpace(input.PageTag))
                    g.DrawString(input.PageTag, tiny, faint,
                        new RectangleF(box.X + 16, box.Y + 58, box.Width - 32, 18), sfLeft);
            }
            return h;
        }

        private static void DrawMapPanel(Graphics g, ReportInput input, Rectangle box)
        {
            Panel(g, box, "نقشهٔ توزیع — " + input.MetricTitle);

            // فضای جدولِ رتبه‌بندی از پیش کنار گذاشته می‌شود، وگرنه جدول روی
            // پایینِ نقشه (و روی طیفِ رنگ) می‌افتد.
            int rankRows = input.Regions == null ? 0 : Math.Min(8, input.Regions.Count);
            int rankHeight = rankRows == 0 ? 0 : 22 + rankRows * 20 + 30;

            Rectangle inner = new Rectangle(box.X + 10, box.Y + 38,
                                            box.Width - 20, box.Height - 48 - rankHeight);
            if (input.MapImage != null)
            {
                // حفظِ نسبتِ ابعاد تا نقشه کشیده نشود.
                double sx = inner.Width / (double)input.MapImage.Width;
                double sy = inner.Height / (double)input.MapImage.Height;
                double s = Math.Min(sx, sy);
                int w = (int)(input.MapImage.Width * s);
                int h = (int)(input.MapImage.Height * s);
                g.DrawImage(input.MapImage,
                    new Rectangle(inner.X + (inner.Width - w) / 2,
                                  inner.Y + (inner.Height - h) / 2, w, h));
            }

            // جدولِ کوچکِ «۸ منطقهٔ برتر» زیرِ نقشه، داخلِ همان پنل.
            if (input.Regions != null && input.Regions.Count > 0)
            {
                int rows = Math.Min(8, input.Regions.Count);
                int tableH = 22 + rows * 20;
                Rectangle t = new Rectangle(box.X + 12, box.Bottom - tableH - 12,
                                            box.Width - 24, tableH);

                using (Brush bg = new SolidBrush(Color.FromArgb(238, Color.White)))
                    g.FillRectangle(bg, Rectangle.Inflate(t, 6, 6));
                using (Pen p = new Pen(Rule))
                    g.DrawRectangle(p, Rectangle.Inflate(t, 6, 6));

                DrawRankTable(g, t, input.Regions, rows, input.DistrictLevel);
            }
        }

        private static void DrawRankTable(Graphics g, Rectangle box,
                                          List<GeoAnalyticsService.RegionStats> regions,
                                          int rows, bool districtLevel)
        {
            using (Font head = Fnt(8f, FontStyle.Bold))
            using (Font cell = Fnt(8f))
            using (Font cellB = Fnt(8f, FontStyle.Bold))
            using (Brush ink = new SolidBrush(Ink))
            using (Brush muted = new SolidBrush(InkMuted))
            using (StringFormat sf = Rtl(StringAlignment.Near))
            using (StringFormat sfC = Rtl(StringAlignment.Center))
            {
                float y = box.Y;
                float nameW = box.Width * 0.30f;
                float barW = box.Width * 0.38f;
                float numW = box.Width * 0.16f;

                g.DrawString(districtLevel ? "ولسوالی" : "ولایت", head, muted,
                    new RectangleF(box.Right - nameW, y, nameW, 18), sf);
                g.DrawString("پرونده", head, muted,
                    new RectangleF(box.Right - nameW - barW - numW, y, numW, 18), sfC);
                g.DrawString("اعضا", head, muted,
                    new RectangleF(box.X, y, numW, 18), sfC);
                y += 20;

                int max = 1;
                for (int i = 0; i < rows; i++)
                    if (regions[i].Cases > max) max = regions[i].Cases;

                for (int i = 0; i < rows; i++)
                {
                    GeoAnalyticsService.RegionStats s = regions[i];

                    g.DrawString(s.Region, cellB, ink,
                        new RectangleF(box.Right - nameW, y, nameW, 18), sf);

                    float w = (float)(barW * s.Cases / max);
                    RectangleF bar = new RectangleF(box.Right - nameW - barW, y + 5, Math.Max(2, w), 8);
                    using (Brush b = new SolidBrush(AfghanMapControl.HeatColor(s.Cases, 0, max)))
                        g.FillRectangle(b, bar);
                    using (Pen p = new Pen(Color.FromArgb(60, Ink)))
                        g.DrawRectangle(p, bar.X, bar.Y, bar.Width, bar.Height);

                    g.DrawString(Num(s.Cases), cell, ink,
                        new RectangleF(box.Right - nameW - barW - numW, y, numW, 18), sfC);
                    g.DrawString(Num(s.Members), cell, muted,
                        new RectangleF(box.X, y, numW, 18), sfC);
                    y += 20;
                }
            }
        }

        // ستونِ دادهٔ سمتِ چپ: کارت‌های KPI، جمعیت، و سه تفکیکِ اصلی.
        private static void DrawDataColumn(Graphics g, ReportInput input, Rectangle box)
        {
            GeoAnalyticsService.RegionStats t = input.Totals ?? new GeoAnalyticsService.RegionStats();

            int y = box.Y;

            // ارتفاع‌ها از فضای واقعیِ ستون حساب می‌شوند، نه عددِ ثابت: پوستر
            // در قالبِ ۲:۱ کوتاه‌تر از قالبِ قبلی است و اعدادِ ثابت باعث
            // می‌شدند پنلِ آخر از پایینِ صفحه بیرون بزند.
            int available = box.Height;
            int cardH = Clamp((int)(available * 0.108f), 56, 76);
            int stripH = Clamp((int)(available * 0.100f), 52, 70);
            int gap = 8;

            string[][] kpis =
            {
                new[] { "پرونده‌ها",    Num(t.Cases)    },
                new[] { "خانواده‌ها",   Num(t.Families) },
                new[] { "اعضای خانواده",Num(t.Members)  },
                new[] { "مرد",          Num(t.Male)     },
                new[] { "زن",           Num(t.Female)   }
            };
            DrawCardRow(g, new Rectangle(box.X, y, box.Width, cardH), kpis, Accent);
            y += cardH + gap;

            // ─── ردیفِ گروه‌های سنی ─────────────────────────────────────────
            string[][] ages =
            {
                new[] { "کودک",   Num(t.Child) },
                new[] { "نوجوان", Num(t.Teen)  },
                new[] { "جوان",   Num(t.Youth) },
                new[] { "بزرگسال",Num(t.Adult) },
                new[] { "سالمند", Num(t.Elder) }
            };
            DrawCardRow(g, new Rectangle(box.X, y, box.Width, cardH), ages, ColorTranslator.FromHtml("#4E8CBE"));
            y += cardH + gap + 2;

            // ─── چهار تفکیک در شبکهٔ ۲×۲ ────────────────────────────────────
            // ارتفاعِ پنل از فضای باقی‌مانده حساب می‌شود تا نوارِ ریسک و پنلِ
            // یافته‌ها همیشه جا شوند.
            int half = (box.Width - 12) / 2;
            // ارتفاعِ پنل دقیقاً به‌اندازهٔ ردیف‌هایش است؛ فضای اضافه به پنلِ
            // یافته‌ها می‌رسد که همیشه بیشتر از اینها متن دارد.
            int rowsNeeded = Math.Max(RowCount(input.RequestTypes), RowCount(input.ServiceStatuses));
            int rowsNeeded2 = Math.Max(RowCount(input.EconomicPriorities), RowCount(input.Sponsorship));
            int blockH = 40 + Math.Max(rowsNeeded, rowsNeeded2) * 21;

            int remaining = box.Bottom - y - (stripH + gap) - 92;   // ۹۲ کفِ پنلِ یافته‌ها
            blockH = Clamp(blockH, 110, Math.Max(110, remaining / 2 - gap));

            DrawBreakdownPanel(g, new Rectangle(box.X + half + 12, y, half, blockH),
                               "نوع پرونده", input.RequestTypes, 6);
            DrawBreakdownPanel(g, new Rectangle(box.X, y, half, blockH),
                               "وضعیت خدمات", input.ServiceStatuses, 6);
            y += blockH + gap;

            DrawBreakdownPanel(g, new Rectangle(box.X + half + 12, y, half, blockH),
                               "اولویت اقتصادی", input.EconomicPriorities, 6);
            DrawBreakdownPanel(g, new Rectangle(box.X, y, half, blockH),
                               "وضعیت حمایت مالی", input.Sponsorship, 6);
            y += blockH + gap;

            // ─── نوارِ ریسک و مالی ──────────────────────────────────────────
            GeoAnalyticsService.AssistanceStats a =
                input.Assistance ?? new GeoAnalyticsService.AssistanceStats();
            string[][] risk =
            {
                new[] { "پرخطر",           Num(t.HighRisk) },
                new[] { "میانگین آسیب",    Dec1(t.VulnAvg) },
                new[] { "دارای حامی",      Num(t.WithSponsor) },
                new[] { "فاقد حامی",       Num(t.WithoutSponsor) },
                new[] { "مجموع کمک",       Money(a.Total) }
            };
            DrawCardRow(g, new Rectangle(box.X, y, box.Width, stripH), risk, ColorTranslator.FromHtml("#7A5EA8"));
            y += stripH + gap;

            // ─── هوشِ مدیریتی ───────────────────────────────────────────────
            int left = box.Bottom - y;
            if (left > 60 && input.Insights != null)
                DrawInsightPanel(g, new Rectangle(box.X, y, box.Width, left), input.Insights);
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        private static int RowCount(List<GeoAnalyticsService.BreakdownRow> rows)
        {
            return rows == null ? 0 : Math.Min(6, rows.Count);
        }

        private static void DrawCardRow(Graphics g, Rectangle box, string[][] items, Color accent)
        {
            int gap = 8;
            int w = (box.Width - gap * (items.Length - 1)) / items.Length;

            using (Font label = Fnt(8f))
            using (Brush muted = new SolidBrush(InkMuted))
            using (Brush ink = new SolidBrush(Ink))
            using (StringFormat sf = Rtl(StringAlignment.Center))
            {
                for (int i = 0; i < items.Length; i++)
                {
                    // ترتیبِ کارت‌ها از راست به چپ چیده می‌شود تا با جهتِ
                    // خواندنِ فارسی هم‌خوان باشد.
                    int x = box.Right - (i + 1) * w - i * gap;
                    Rectangle card = new Rectangle(x, box.Y, w, box.Height);

                    using (Brush bg = new SolidBrush(SoftFill))
                        g.FillRectangle(bg, card);
                    using (Brush stripe = new SolidBrush(accent))
                        g.FillRectangle(stripe, card.X, card.Y, card.Width, 3);
                    using (Pen p = new Pen(Rule))
                        g.DrawRectangle(p, card);

                    // اندازهٔ عدد با عرضِ واقعیِ کارت تطبیق داده می‌شود.
                    // عددهای بلند (مثلِ «مجموع کمک‌ها») وگرنه با سه‌نقطه
                    // بریده می‌شدند و گزارش عددِ ناقص نشان می‌داد.
                    using (Font value = FitFont(g, items[i][1], card.Width - 12, 15f, 8.5f))
                        g.DrawString(items[i][1], value, ink,
                            new RectangleF(card.X, card.Y + 14, card.Width, card.Height - 34), sf);

                    g.DrawString(items[i][0], label, muted,
                        new RectangleF(card.X, card.Bottom - 20, card.Width, 16), sf);
                }
            }
        }

        // بزرگ‌ترین اندازه‌ای که متن در عرضِ داده‌شده جا می‌شود.
        // آموزش — بریدنِ عدد در یک گزارشِ مدیریتی بدترین حالت است: کاربر
        // «۲۱٬۸۵…» را می‌بیند و نمی‌داند میلیون است یا میلیارد. پس به‌جای
        // Trimming، خودِ قلم کوچک می‌شود.
        private static Font FitFont(Graphics g, string text, float maxWidth,
                                    float startSize, float minSize)
        {
            float size = startSize;
            while (size > minSize)
            {
                Font candidate = Fnt(size, FontStyle.Bold);
                if (g.MeasureString(text, candidate).Width <= maxWidth) return candidate;
                candidate.Dispose();
                size -= 0.5f;
            }
            return Fnt(minSize, FontStyle.Bold);
        }

        private static void DrawBreakdownPanel(Graphics g, Rectangle box, string title,
                                               List<GeoAnalyticsService.BreakdownRow> rows, int maxRows)
        {
            Panel(g, box, title);
            if (rows == null || rows.Count == 0) return;

            int max = 1;
            foreach (GeoAnalyticsService.BreakdownRow r in rows)
                if (r.Count > max) max = r.Count;

            using (Font cell = Fnt(8.5f))
            using (Brush ink = new SolidBrush(Ink))
            using (Brush muted = new SolidBrush(InkMuted))
            using (StringFormat sf = Rtl(StringAlignment.Near))
            using (StringFormat sfL = Rtl(StringAlignment.Far))
            {
                // سه ستون: برچسب (راست) · میلهٔ نسبت (وسط) · عدد و درصد (چپ).
                // سهمِ ستونِ چپ عمداً سخاوتمندانه است چون «۱۰٬۵۶۵ (۶۷٫۹٪)»
                // طولانی‌ترین رشتهٔ این پنل است و بریده‌شدنش عدد را بی‌معنا می‌کند.
                float y = box.Y + 34;
                float pad = 7f;
                float usable = box.Width - pad * 2;
                float labelW = usable * 0.40f;
                float barW = usable * 0.20f;
                float valueW = usable * 0.40f;

                int count = Math.Min(maxRows, rows.Count);
                for (int i = 0; i < count; i++)
                {
                    GeoAnalyticsService.BreakdownRow r = rows[i];

                    g.DrawString(r.Label, cell, ink,
                        new RectangleF(box.Right - pad - labelW, y, labelW, 18), sf);

                    float w = (float)(barW * r.Count / max);
                    RectangleF bar = new RectangleF(box.Right - pad - labelW - barW, y + 5,
                                                    Math.Max(2, w), 8);
                    using (Brush b = new SolidBrush(AfghanMapControl.HeatColor(r.Count, 0, max)))
                        g.FillRectangle(b, bar);

                    // عدد و درصد هرگز نباید بریده شوند؛ اگر جا نشد قلم کوچک
                    // می‌شود، نه اینکه «(۷۲٫۰٪)» نصفه بماند.
                    string valueText = Num(r.Count) + "  (" +
                        ReportDoc.Fa(r.Percent.ToString("0.0", CultureInfo.InvariantCulture)) + "٪)";
                    using (Font valueFont = FitFont(g, valueText, valueW, 8.5f, 6.5f))
                        g.DrawString(valueText, valueFont, muted,
                            new RectangleF(box.X + pad, y, valueW, 18), sfL);
                    y += 21;
                }
            }
        }

        private static void DrawInsightPanel(Graphics g, Rectangle box,
                                             List<GeoInsightEngine.Insight> insights)
        {
            Panel(g, box, "هوش مدیریتی — یافته‌های خودکار");

            using (Font f = Fnt(8.5f))
            using (StringFormat sf = RtlWrap())
            {
                float y = box.Y + 32;
                foreach (GeoInsightEngine.Insight ins in insights)
                {
                    if (y > box.Bottom - 20) break;

                    using (Brush dot = new SolidBrush(ins.Accent))
                        g.FillEllipse(dot, box.Right - 16, y + 5, 6, 6);

                    RectangleF r = new RectangleF(box.X + 10, y, box.Width - 34, 34);
                    using (Brush ink = new SolidBrush(Ink))
                        g.DrawString(ins.Text, f, ink, r, sf);

                    SizeF size = g.MeasureString(ins.Text, f, (int)r.Width, sf);
                    y += Math.Max(19, size.Height + 4);
                }
            }
        }

        private static void Panel(Graphics g, Rectangle box, string title)
        {
            using (Brush bg = new SolidBrush(Color.White))
                g.FillRectangle(bg, box);
            using (Pen p = new Pen(Rule))
                g.DrawRectangle(p, box);
            using (Brush head = new SolidBrush(SoftFill))
                g.FillRectangle(head, box.X + 1, box.Y + 1, box.Width - 1, 26);
            using (Pen p = new Pen(Rule))
                g.DrawLine(p, box.X, box.Y + 27, box.Right, box.Y + 27);

            using (Font f = Fnt(9.5f, FontStyle.Bold))
            using (Brush ink = new SolidBrush(Band))
            using (StringFormat sf = Rtl(StringAlignment.Near))
                g.DrawString(title, f, ink, new RectangleF(box.X + 10, box.Y + 5, box.Width - 20, 20), sf);
        }

        private static void DrawFooter(Graphics g, ReportInput input, int margin, int width, int height)
        {
            using (Font f = Fnt(7.5f))
            using (Brush muted = new SolidBrush(InkMuted))
            using (Pen p = new Pen(Rule))
            using (StringFormat sf = Rtl(StringAlignment.Near))
            using (StringFormat sfL = Rtl(StringAlignment.Far))
            {
                int y = height - margin + 2;
                g.DrawLine(p, margin, y - 6, width - margin, y - 6);
                g.DrawString("نقشه شِماتیک است؛ مرزهای داخلی تقریبی‌اند و مبنای حقوقی ندارند. همهٔ ارقام لحظه‌ای از پرونده‌های ثبت‌شده محاسبه شده‌اند.",
                    f, muted, new RectangleF(margin, y - 2, width - margin * 2, 16), sf);
                g.DrawString("سامانهٔ مدیریت پرونده‌ها", f, muted,
                    new RectangleF(margin, y - 2, width - margin * 2, 16), sfL);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // سندِ کامل: صفحهٔ اولِ پوستر + صفحه‌های جدولِ داده.
        // ═══════════════════════════════════════════════════════════════════
        public static ReportDoc BuildDocument(ReportInput input, out string posterPath)
        {
            List<string> paths;
            ReportDoc doc = BuildDocument(input, null, out paths);
            posterPath = paths.Count > 0 ? paths[0] : null;
            return doc;
        }

        // ═══════════════════════════════════════════════════════════════════
        // گزارشِ چندولایتی.
        //
        // خواستهٔ صریحِ مالکِ محصول: «اگر کسی خواست چندین ولایت ببیند، هر
        // ولایت در یک صفحه با اطلاعاتش کنارِ نقشه‌اش بیاید، و آمارِ کلیِ همهٔ
        // ولایات هم اول یا آخر درج شود.»
        //
        // ساختارِ سند:
        //   صفحهٔ ۱      : پوسترِ کلِ کشور (نقشهٔ ولایتی + آمارِ تجمیعی)
        //   صفحهٔ ۲..n+۱ : یک صفحه به‌ازای هر ولایت (نقشهٔ ولسوالی‌هایش + آمارِ خودش)
        //   صفحهٔ n+۲ به بعد: جدول‌های کاملِ داده و یافته‌های تحلیلی
        //
        // فایل‌های موقتِ تصویر در `posterPaths` برگردانده می‌شوند تا فراخواننده
        // بعد از ساختِ PDF پاکشان کند (ReportDoc تصویرها را از مسیرِ فایل
        // می‌خواند، پس تا پایانِ چاپ باید بمانند).
        // ═══════════════════════════════════════════════════════════════════
        public static ReportDoc BuildDocument(ReportInput input,
                                              List<ReportInput> provincePages,
                                              out List<string> posterPaths)
        {
            posterPaths = new List<string>();

            ReportDoc doc = new ReportDoc
            {
                Title = input.Title,
                Subtitle = input.ScopeLabel,
                Landscape = true,
                DocumentCode = "GEO-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)
            };

            GeoAnalyticsService.RegionStats t = input.Totals ?? new GeoAnalyticsService.RegionStats();

            // سربرگ عمداً کوتاه است (سه فیلد): هرچه سربرگ کوتاه‌تر، کادرِ
            // تصویر بلندتر و نقشه بزرگ‌تر. بقیهٔ شناسنامه داخلِ خودِ پوستر هست.
            doc.HeaderFields.Add(Kv("محدوده", input.ScopeLabel));
            doc.HeaderFields.Add(Kv("تعداد پرونده", Num(t.Cases)));
            doc.HeaderFields.Add(Kv("تاریخ گزارش", PersianDateHelper.ToPersianDateStringSafe(DateTime.Now)));

            // ── صفحهٔ ۱: پوسترِ کلی ─────────────────────────────────────────
            AddPosterPage(doc, input, posterPaths, false);

            // ── یک صفحه به‌ازای هر ولایت ────────────────────────────────────
            if (provincePages != null)
            {
                foreach (ReportInput page in provincePages)
                    AddPosterPage(doc, page, posterPaths, true);
            }

            // ── جدول‌های کاملِ داده ─────────────────────────────────────────
            doc.Blocks.Add(new ReportPageBreak());

            string levelHeader = input.DistrictLevel ? "ولسوالی" : "ولایت";
            DataTable regions = RegionsToTable(input.Regions, levelHeader);
            doc.Blocks.Add(new ReportHeading
            {
                Text = "جدول تفصیلی مناطق",
                Note = ReportDoc.Fa(regions.Rows.Count) + " ردیف"
            });
            doc.Blocks.Add(new ReportTable
            {
                Data = regions,
                ShowRowNumbers = true,
                EmptyText = "منطقه‌ای با این فیلتر یافت نشد.",
                Columns =
                {
                    ReportDocColumn.Of(levelHeader, levelHeader, 1.7f),
                    ReportDocColumn.Of("پرونده", "پرونده", 1.0f, ReportAlign.Center),
                    ReportDocColumn.Of("خانواده", "خانواده", 1.0f, ReportAlign.Center),
                    ReportDocColumn.Of("اعضا", "اعضا", 1.0f, ReportAlign.Center),
                    ReportDocColumn.Of("مرد", "مرد", 0.9f, ReportAlign.Center),
                    ReportDocColumn.Of("زن", "زن", 0.9f, ReportAlign.Center),
                    ReportDocColumn.Of("کودک", "کودک", 0.9f, ReportAlign.Center),
                    ReportDocColumn.Of("سالمند", "سالمند", 0.9f, ReportAlign.Center),
                    ReportDocColumn.Of("یتیم", "یتیم", 0.9f, ReportAlign.Center),
                    ReportDocColumn.Of("معلول", "معلول", 0.9f, ReportAlign.Center),
                    ReportDocColumn.Of("فعال", "فعال", 0.9f, ReportAlign.Center),
                    ReportDocColumn.Of("فاقد حامی", "فاقد حامی", 1.1f, ReportAlign.Center),
                    ReportDocColumn.Of("میانگین آسیب", "میانگین آسیب", 1.2f, ReportAlign.Center),
                }
            });

            AddBreakdownTable(doc, "تفکیک نوع پرونده", "نوع پرونده", input.RequestTypes);
            AddBreakdownTable(doc, "تفکیک وضعیت خدمات", "وضعیت خدمات", input.ServiceStatuses);
            AddBreakdownTable(doc, "تفکیک اولویت اقتصادی", "اولویت اقتصادی", input.EconomicPriorities);
            AddBreakdownTable(doc, "تفکیک وضعیت حمایت مالی", "وضعیت حمایت", input.Sponsorship);

            // ── خلاصهٔ مالی ─────────────────────────────────────────────────
            if (input.Assistance != null)
            {
                ReportKeyValues money = new ReportKeyValues { Columns = 3 };
                money.Items.Add(Kv("تعداد پرداخت", Num(input.Assistance.Count)));
                money.Items.Add(Kv("پرونده‌های دریافت‌کننده", Num(input.Assistance.CasesReceiving)));
                money.Items.Add(Kv("مجموع کمک‌ها", Money(input.Assistance.Total)));
                money.Items.Add(Kv("میانگین کمک", Money(input.Assistance.Average)));
                money.Items.Add(Kv("بیشترین کمک", Money(input.Assistance.Max)));
                money.Items.Add(Kv("کمترین کمک", Money(input.Assistance.Min)));

                doc.Blocks.Add(new ReportHeading { Text = "خلاصهٔ کمک‌های مالی" });
                doc.Blocks.Add(money);
            }

            // ── یافته‌های خودکار ────────────────────────────────────────────
            if (input.Insights != null && input.Insights.Count > 0)
            {
                doc.Blocks.Add(new ReportHeading { Text = "یافته‌های تحلیلی", PageBreakBefore = false });
                foreach (GeoInsightEngine.Insight ins in input.Insights)
                {
                    if (ins.Tone == GeoInsightEngine.InsightTone.Critical ||
                        ins.Tone == GeoInsightEngine.InsightTone.Warning)
                        doc.Blocks.Add(new ReportCallout { Text = ins.Text, Accent = ins.Accent });
                    else
                        doc.Blocks.Add(new ReportParagraph { Text = "• " + ins.Text });
                }
            }

            doc.Blocks.Add(new ReportSpacer { Height = 14 });
            doc.Blocks.Add(new ReportParagraph
            {
                Text = "یادآوری: نقشهٔ این گزارش شِماتیک است — موقعیت، همسایگی و اندازهٔ نسبی ولایات درست است، ولی خط مرزها تقریبی‌اند و مبنای حقوقی ندارند.",
                Muted = true
            });
            doc.Blocks.Add(new ReportSignatures());

            return doc;
        }

        // یک صفحهٔ پوستر به سند اضافه می‌کند. هر پوستر یک صفحهٔ کامل است، پس
        // قبل از پوسترهای دوم به بعد شکستِ صفحه گذاشته می‌شود.
        private static void AddPosterPage(ReportDoc doc, ReportInput page,
                                          List<string> posterPaths, bool pageBreakBefore)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "geo_poster_" + Guid.NewGuid().ToString("N") + ".png");

            using (Bitmap poster = BuildPoster(page))
                poster.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            posterPaths.Add(path);

            if (pageBreakBefore) doc.Blocks.Add(new ReportPageBreak());

            doc.Blocks.Add(new ReportImageGrid
            {
                Columns = 1,
                CellHeight = PosterCellHeight,
                EmptyText = "تصویر نقشه ساخته نشد.",
                Items = { new ReportImageItem { Path = path, Caption = "" } }
            });
        }

        private static void AddBreakdownTable(ReportDoc doc, string heading, string labelHeader,
                                              List<GeoAnalyticsService.BreakdownRow> rows)
        {
            if (rows == null || rows.Count == 0) return;

            DataTable dt = GeoAnalyticsService.ToDataTable(rows, labelHeader);
            doc.Blocks.Add(new ReportHeading { Text = heading, Note = ReportDoc.Fa(rows.Count) + " ردیف" });
            doc.Blocks.Add(new ReportTable
            {
                Data = dt,
                ShowRowNumbers = true,
                Columns =
                {
                    ReportDocColumn.Of(labelHeader, labelHeader, 2.4f),
                    ReportDocColumn.Of("تعداد", "تعداد", 1.0f, ReportAlign.Center),
                    ReportDocColumn.Of("درصد", "درصد", 1.0f, ReportAlign.Center),
                }
            });
        }

        public static DataTable RegionsToTable(List<GeoAnalyticsService.RegionStats> regions,
                                               string levelHeader)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add(levelHeader, typeof(string));
            dt.Columns.Add("پرونده", typeof(int));
            dt.Columns.Add("خانواده", typeof(int));
            dt.Columns.Add("اعضا", typeof(int));
            dt.Columns.Add("مرد", typeof(int));
            dt.Columns.Add("زن", typeof(int));
            dt.Columns.Add("کودک", typeof(int));
            dt.Columns.Add("نوجوان", typeof(int));
            dt.Columns.Add("جوان", typeof(int));
            dt.Columns.Add("بزرگسال", typeof(int));
            dt.Columns.Add("سالمند", typeof(int));
            dt.Columns.Add("یتیم", typeof(int));
            dt.Columns.Add("معلول", typeof(int));
            dt.Columns.Add("مهاجر", typeof(int));
            dt.Columns.Add("فعال", typeof(int));
            dt.Columns.Add("متقاضی", typeof(int));
            dt.Columns.Add("فقر شدید", typeof(int));
            dt.Columns.Add("پرخطر", typeof(int));
            dt.Columns.Add("میانگین آسیب", typeof(string));
            dt.Columns.Add("دارای حامی", typeof(int));
            dt.Columns.Add("فاقد حامی", typeof(int));
            dt.Columns.Add("مجموع کمک", typeof(string));

            if (regions == null) return dt;
            foreach (GeoAnalyticsService.RegionStats s in regions)
            {
                dt.Rows.Add(s.Region, s.Cases, s.Families, s.Members, s.Male, s.Female,
                            s.Child, s.Teen, s.Youth, s.Adult, s.Elder,
                            s.Orphans, s.Disabled, s.Migrants,
                            s.ActiveCases, s.ApplicantCases, s.SeverePoverty, s.HighRisk,
                            s.VulnAvg.ToString("0.0", CultureInfo.InvariantCulture),
                            s.WithSponsor, s.WithoutSponsor,
                            s.AssistanceTotal.ToString("#,0", CultureInfo.InvariantCulture));
            }
            return dt;
        }

        // ─── کمکی‌های ظاهری ─────────────────────────────────────────────────
        private static readonly string FaFamily = ResolveFamily();

        private static string ResolveFamily()
        {
            string[] wanted = { "IRANSans", "Vazirmatn", "Sahel", "Tahoma" };
            foreach (string w in wanted)
            {
                try
                {
                    foreach (FontFamily f in FontFamily.Families)
                        if (string.Equals(f.Name, w, StringComparison.OrdinalIgnoreCase)) return w;
                }
                catch { }
            }
            return "Tahoma";
        }

        private static Font Fnt(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(FaFamily, size, style, GraphicsUnit.Point);
        }

        private static StringFormat Rtl(StringAlignment align)
        {
            return new StringFormat(StringFormatFlags.DirectionRightToLeft)
            {
                Alignment = align,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
        }

        private static StringFormat RtlWrap()
        {
            return new StringFormat(StringFormatFlags.DirectionRightToLeft)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.Word
            };
        }

        private static KeyValuePair<string, string> Kv(string k, string v)
        {
            return new KeyValuePair<string, string>(k, v);
        }

        private static string Num(int v)
        {
            return ReportDoc.Fa(v.ToString("#,0", CultureInfo.InvariantCulture));
        }

        private static string Dec1(double v)
        {
            return ReportDoc.Fa(v.ToString("0.0", CultureInfo.InvariantCulture));
        }

        private static string Money(decimal v)
        {
            return ReportDoc.Fa(v.ToString("#,0", CultureInfo.InvariantCulture));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // GeoInsightEngine — «پنلِ هوشِ مدیریتی».
    //
    // آموزش — این موتور هیچ چیزی را حدس نمی‌زند و هیچ مدلی ندارد: فقط روی
    // همان ردیف‌هایی که GeoAnalyticsService برگردانده قاعده‌های ساده و قابلِ
    // بازبینی اجرا می‌کند (بیشینه، کمینه، نسبت، تمرکز). هر جمله باید از روی
    // عددهای همان صفحه قابلِ راستی‌آزمایی باشد؛ وگرنه مدیر نمی‌تواند به آن
    // اعتماد کند و پنل به «متنِ تزئینی» تبدیل می‌شود.
    //
    // هر یافته یک «کدِ فیلتر» همراه دارد تا کلیک روی جمله همان پرونده‌ها را
    // در FrmCase باز کند — تحلیل بدونِ مسیرِ اقدام بی‌فایده است.
    // ═══════════════════════════════════════════════════════════════════════
    public static class GeoInsightEngine
    {
        public enum InsightTone { Neutral, Positive, Warning, Critical }

        public sealed class Insight
        {
            public string Text = "";
            public InsightTone Tone = InsightTone.Neutral;
            public string Region = "";          // منطقهٔ مرتبط (برای Drill-Down)
            public GeoFilter DrillFilter;       // اگر null باشد، جمله کلیک‌پذیر نیست

            public Color Accent
            {
                get
                {
                    switch (Tone)
                    {
                        case InsightTone.Critical: return UiTheme.Danger;
                        case InsightTone.Warning: return UiTheme.Warning;
                        case InsightTone.Positive: return UiTheme.Success;
                        default: return UiTheme.Primary;
                    }
                }
            }

            public string Glyph
            {
                get
                {
                    switch (Tone)
                    {
                        case InsightTone.Critical: return "!";
                        case InsightTone.Warning: return "▲";
                        case InsightTone.Positive: return "✓";
                        default: return "•";
                    }
                }
            }
        }

        private const int MinRegionCases = 15;   // زیرِ این عدد، درصدها گمراه‌کننده‌اند

        public static List<Insight> Build(List<GeoAnalyticsService.RegionStats> regions,
                                          GeoFilter baseFilter,
                                          bool districtLevel,
                                          string parentProvince)
        {
            List<Insight> list = new List<Insight>();
            if (regions == null || regions.Count == 0)
            {
                list.Add(new Insight
                {
                    Text = "برای این فیلتر هیچ پرونده‌ای یافت نشد. فیلترها را بازتر کنید.",
                    Tone = InsightTone.Warning
                });
                return list;
            }

            string levelWord = districtLevel ? "ولسوالی" : "ولایت";
            GeoAnalyticsService.RegionStats total =
                GeoAnalyticsService.Aggregate(regions, "کل");

            // ─── ۱) تمرکزِ جغرافیایی ────────────────────────────────────────
            GeoAnalyticsService.RegionStats top = Max(regions, delegate (GeoAnalyticsService.RegionStats s) { return s.Cases; });
            if (top != null && total.Cases > 0)
            {
                double share = top.Cases * 100.0 / total.Cases;
                list.Add(new Insight
                {
                    Text = string.Format(CultureInfo.InvariantCulture,
                        "{0} با {1} پرونده ({2}٪ از کل) بیشترین تراکم پرونده را دارد.",
                        top.Region, Fa(top.Cases), Fa1(share)),
                    Tone = share >= 40 ? InsightTone.Warning : InsightTone.Neutral,
                    Region = top.Region,
                    DrillFilter = Region(baseFilter, top, districtLevel, parentProvince)
                });
            }

            // ─── ۲) بیشترین یتیم ────────────────────────────────────────────
            AddMax(list, regions, baseFilter, districtLevel, parentProvince,
                delegate (GeoAnalyticsService.RegionStats s) { return s.Orphans; },
                "{0} با {1} پرونده، بیشترین تعداد یتیم را دارد.", InsightTone.Neutral);

            // ─── ۳) بیشترین معلول ───────────────────────────────────────────
            AddMax(list, regions, baseFilter, districtLevel, parentProvince,
                delegate (GeoAnalyticsService.RegionStats s) { return s.Disabled; },
                "{0} با {1} پرونده، بیشترین تعداد معلول را دارد.", InsightTone.Neutral);

            // ─── ۴) بیشترین پروندهٔ فعال ────────────────────────────────────
            AddMax(list, regions, baseFilter, districtLevel, parentProvince,
                delegate (GeoAnalyticsService.RegionStats s) { return s.ActiveCases; },
                "{0} با {1} پرونده، بیشترین پروندهٔ فعال را دارد.", InsightTone.Positive);

            // ─── ۵) بالاترین درصدِ فقرِ شدید ────────────────────────────────
            GeoAnalyticsService.RegionStats poorest = MaxRatio(regions,
                delegate (GeoAnalyticsService.RegionStats s) { return s.SeverePoverty; },
                delegate (GeoAnalyticsService.RegionStats s) { return s.Cases; });
            if (poorest != null && poorest.SeverePoverty > 0)
            {
                double pct = poorest.SeverePoverty * 100.0 / poorest.Cases;
                list.Add(new Insight
                {
                    Text = string.Format(CultureInfo.InvariantCulture,
                        "{0} با {1}٪ ({2} پرونده) بالاترین نسبت فقر شدید را دارد.",
                        poorest.Region, Fa1(pct), Fa(poorest.SeverePoverty)),
                    Tone = pct >= 50 ? InsightTone.Critical : InsightTone.Warning,
                    Region = poorest.Region,
                    DrillFilter = Region(baseFilter, poorest, districtLevel, parentProvince)
                });
            }

            // ─── ۶) کمترین پوششِ حمایتی ─────────────────────────────────────
            GeoAnalyticsService.RegionStats worstCoverage = MaxRatio(regions,
                delegate (GeoAnalyticsService.RegionStats s) { return s.WithoutSponsor; },
                delegate (GeoAnalyticsService.RegionStats s) { return s.Cases; });
            if (worstCoverage != null && worstCoverage.WithoutSponsor > 0)
            {
                double pct = worstCoverage.WithoutSponsor * 100.0 / worstCoverage.Cases;
                GeoFilter f = Region(baseFilter, worstCoverage, districtLevel, parentProvince);
                f.Sponsorship = GeoFilter.SponsorNone;
                list.Add(new Insight
                {
                    Text = string.Format(CultureInfo.InvariantCulture,
                        "{0} کمترین پوشش حمایتی را دارد: {1}٪ ({2} پرونده) فاقد حامی‌اند.",
                        worstCoverage.Region, Fa1(pct), Fa(worstCoverage.WithoutSponsor)),
                    Tone = pct >= 60 ? InsightTone.Critical : InsightTone.Warning,
                    Region = worstCoverage.Region,
                    DrillFilter = f
                });
            }

            // ─── ۷) شمارشِ مناطقِ بحرانیِ بی‌حامی ───────────────────────────
            List<GeoAnalyticsService.RegionStats> uncovered =
                new List<GeoAnalyticsService.RegionStats>();
            foreach (GeoAnalyticsService.RegionStats s in regions)
                if (s.Cases >= MinRegionCases && s.WithoutSponsor * 2 > s.Cases) uncovered.Add(s);

            if (uncovered.Count > 0)
            {
                list.Add(new Insight
                {
                    Text = string.Format(CultureInfo.InvariantCulture,
                        "{0} {1} بیش از ۵۰٪ پرونده‌های فاقد حامی دارند و نیازمند اقدام فوری‌اند.",
                        Fa(uncovered.Count), levelWord),
                    Tone = InsightTone.Critical
                });
            }

            // ─── ۸) بالاترین میانگینِ آسیب‌پذیری ────────────────────────────
            GeoAnalyticsService.RegionStats risky = Max(regions,
                delegate (GeoAnalyticsService.RegionStats s) { return s.Cases >= MinRegionCases ? s.VulnAvg : 0; });
            if (risky != null && risky.VulnAvg > 0)
            {
                GeoFilter f = Region(baseFilter, risky, districtLevel, parentProvince);
                f.VulnerabilityBand = "HIGH";
                list.Add(new Insight
                {
                    Text = string.Format(CultureInfo.InvariantCulture,
                        "{0} با میانگین امتیاز {1} بالاترین سطح آسیب‌پذیری را دارد ({2} پروندهٔ پرخطر).",
                        risky.Region, Fa1(risky.VulnAvg), Fa(risky.HighRisk)),
                    Tone = InsightTone.Warning,
                    Region = risky.Region,
                    DrillFilter = f
                });
            }

            // ─── ۹) بزرگ‌ترین بُعدِ خانوار ──────────────────────────────────
            GeoAnalyticsService.RegionStats biggest = MaxRatio(regions,
                delegate (GeoAnalyticsService.RegionStats s) { return s.Cases >= MinRegionCases ? s.Members : 0; },
                delegate (GeoAnalyticsService.RegionStats s) { return s.Cases; });
            if (biggest != null && biggest.Members > 0)
            {
                double avgSize = biggest.Members / (double)biggest.Cases;
                list.Add(new Insight
                {
                    Text = string.Format(CultureInfo.InvariantCulture,
                        "بزرگ‌ترین بُعد خانوار در {0} است: به‌طور میانگین {1} نفر در هر پرونده.",
                        biggest.Region, Fa1(avgSize)),
                    Tone = InsightTone.Neutral,
                    Region = biggest.Region,
                    DrillFilter = Region(baseFilter, biggest, districtLevel, parentProvince)
                });
            }

            // ─── ۱۰) نسبتِ کودکان ───────────────────────────────────────────
            if (total.Members > 0)
            {
                double childPct = (total.Child + total.Teen) * 100.0 / total.Members;
                list.Add(new Insight
                {
                    Text = string.Format(CultureInfo.InvariantCulture,
                        "{0}٪ از {1} عضو ثبت‌شده زیر ۱۸ سال‌اند ({2} کودک و {3} نوجوان).",
                        Fa1(childPct), Fa(total.Members), Fa(total.Child), Fa(total.Teen)),
                    Tone = InsightTone.Neutral
                });
            }

            // ─── ۱۱) مناطقِ بدونِ امتیازِ آسیب‌پذیری ────────────────────────
            int unscored = total.Cases - (total.HighRisk + total.MediumRisk + total.LowRisk);
            if (unscored > 0 && total.Cases > 0)
            {
                double pct = unscored * 100.0 / total.Cases;
                list.Add(new Insight
                {
                    Text = string.Format(CultureInfo.InvariantCulture,
                        "امتیاز آسیب‌پذیری برای {0} پرونده ({1}٪) هنوز محاسبه نشده؛ تحلیل ریسک ناقص است.",
                        Fa(unscored), Fa1(pct)),
                    Tone = pct >= 30 ? InsightTone.Warning : InsightTone.Neutral
                });
            }

            // ─── ۱۲) اولویتِ اقتصادیِ ثبت‌نشده ──────────────────────────────
            if (total.SeverePoverty == 0 && total.Cases > 0)
            {
                list.Add(new Insight
                {
                    Text = "هیچ پرونده‌ای اولویت اقتصادی «شدید» یا «خیلی شدید» ندارد — احتمالاً این فیلد هنوز در پرونده‌ها ثبت نشده است.",
                    Tone = InsightTone.Warning
                });
            }

            // ─── ۱۳) مناطقِ بدونِ هیچ پرونده ────────────────────────────────
            if (!districtLevel)
            {
                List<string> empty = new List<string>();
                foreach (string province in AfghanMapGeometry.ProvinceNames())
                {
                    bool found = false;
                    foreach (GeoAnalyticsService.RegionStats s in regions)
                        if (string.Equals(s.Region, province, StringComparison.Ordinal)) { found = true; break; }
                    if (!found) empty.Add(province);
                }
                if (empty.Count > 0)
                {
                    list.Add(new Insight
                    {
                        Text = string.Format(CultureInfo.InvariantCulture,
                            "{0} ولایت هیچ پرونده‌ای در این محدوده ندارند: {1}",
                            Fa(empty.Count),
                            string.Join("، ", empty.ToArray(), 0, Math.Min(6, empty.Count)) +
                            (empty.Count > 6 ? " و …" : "")),
                        Tone = InsightTone.Neutral
                    });
                }
            }

            return list;
        }

        // ─── کمکی‌ها ────────────────────────────────────────────────────────
        private delegate double Selector(GeoAnalyticsService.RegionStats s);

        private static void AddMax(List<Insight> list,
                                   List<GeoAnalyticsService.RegionStats> regions,
                                   GeoFilter baseFilter, bool districtLevel, string parentProvince,
                                   Selector selector, string template, InsightTone tone)
        {
            GeoAnalyticsService.RegionStats best = Max(regions, selector);
            if (best == null || selector(best) <= 0) return;

            list.Add(new Insight
            {
                Text = string.Format(CultureInfo.InvariantCulture, template,
                                     best.Region, Fa((int)selector(best))),
                Tone = tone,
                Region = best.Region,
                DrillFilter = Region(baseFilter, best, districtLevel, parentProvince)
            });
        }

        private static GeoAnalyticsService.RegionStats Max(
            List<GeoAnalyticsService.RegionStats> regions, Selector selector)
        {
            GeoAnalyticsService.RegionStats best = null;
            double bestValue = double.MinValue;
            foreach (GeoAnalyticsService.RegionStats s in regions)
            {
                double v = selector(s);
                if (v > bestValue) { bestValue = v; best = s; }
            }
            return bestValue <= 0 ? null : best;
        }

        // بیشترین *نسبت*، با کفِ حداقلِ تعداد تا یک منطقهٔ سه‌پرونده‌ای صدرنشین نشود.
        private static GeoAnalyticsService.RegionStats MaxRatio(
            List<GeoAnalyticsService.RegionStats> regions, Selector numerator, Selector denominator)
        {
            GeoAnalyticsService.RegionStats best = null;
            double bestRatio = -1;
            foreach (GeoAnalyticsService.RegionStats s in regions)
            {
                double den = denominator(s);
                if (den < MinRegionCases) continue;
                double ratio = numerator(s) / den;
                if (ratio > bestRatio) { bestRatio = ratio; best = s; }
            }
            return bestRatio <= 0 ? null : best;
        }

        private static GeoFilter Region(GeoFilter baseFilter,
                                        GeoAnalyticsService.RegionStats stats,
                                        bool districtLevel, string parentProvince)
        {
            GeoFilter f = (baseFilter ?? new GeoFilter()).Clone();
            if (districtLevel)
            {
                f.Province = string.IsNullOrWhiteSpace(stats.Parent) ? parentProvince : stats.Parent;
                f.District = stats.Region;
            }
            else
            {
                f.Province = stats.Region;
                f.District = "";
            }
            return f;
        }

        private static string Fa(int value)
        {
            return ReportDoc.Fa(value.ToString("#,0", CultureInfo.InvariantCulture));
        }

        private static string Fa1(double value)
        {
            return ReportDoc.Fa(value.ToString("0.0", CultureInfo.InvariantCulture));
        }
    }
}

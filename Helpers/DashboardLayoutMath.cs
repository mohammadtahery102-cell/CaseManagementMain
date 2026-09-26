using System;

namespace CaseManagement.Helpers
{
    // محاسبه‌ی ارتفاع ردیف کارت‌های آماری داشبورد.
    //
    // علت کرش «Height must be greater than 0px»:
    // Chart مایکروسافت در OnResize اگر Height<=0 باشد استثنا می‌دهد.
    // با DashboardSummaryRows=4، ارتفاع summaryPanel برابر باقی‌مانده‌ی
    // TabPage در اندازه‌ی طراحی (۷۳۰) می‌شد و ردیف نمودار صفر می‌ماند.
    // این کلاس تضمین می‌کند ردیف نمودار هرگز از MinChartsRowHeight کمتر نشود.
    public static class DashboardLayoutMath
    {
        // ارتفاع هر ردیف کارت آماری. کارت‌ها Dock=Fill هستند؛ این عدد ارتفاع
        // واقعی را تعیین می‌کند. SummaryPanelHeight ردیف نمودار را صفر نمی‌کند.
        public const int SummaryRowHeight = 128;
        public const int SummaryBottomPad = 10;
        public const int MinChartsRowHeight = 160;
        public const int MinChartPx = 32;
        public const int CardHeaderHeight = 46;
        public const int CardContentPadBottom = 12;
        public const int MinSummaryRows = 2;
        public const int MaxSummaryRows = 4;

        // حداقلِ منطقیِ فضای واقعیِ صفحه. حین Minimize/Restore پنجره، ویندوز
        // یک پیامِ WM_SIZE گذرا با اندازه‌ی خیلی کوچک می‌فرستد؛ اگر Layout همان
        // لحظه محاسبه شود، این ارتفاعِ کاذب روی summaryPanel «قفل» می‌ماند و بعد
        // از بازگشتِ پنجره به اندازه‌ی واقعی هم اصلاح نمی‌شود (کارت‌ها ~۲۴px
        // می‌شوند). چون MinimumSize فرم هرگز کمتر از ۷۳۰ نیست، هر عددِ واقعی
        // همیشه بیشتر از این آستانه است.
        public const int MinSaneAvailableHeight = 300;

        public static int ClampSummaryRows(int rows)
        {
            if (rows < MinSummaryRows) return MinSummaryRows;
            if (rows > MaxSummaryRows) return MaxSummaryRows;
            return rows;
        }

        public static int DesiredSummaryHeight(int rows)
        {
            return ClampSummaryRows(rows) * SummaryRowHeight + SummaryBottomPad;
        }

        public static int SummaryPanelHeight(int rows, int availablePageClientHeight)
        {
            int desired = DesiredSummaryHeight(rows);
            if (availablePageClientHeight <= 0)
                return desired;

            int maxSummary = availablePageClientHeight - MinChartsRowHeight;
            if (maxSummary < 1)
                return 0;

            return Math.Min(desired, maxSummary);
        }

        public static int RemainingForCharts(int availablePageClientHeight, int summaryHeight)
        {
            return Math.Max(0, availablePageClientHeight - summaryHeight);
        }

        public static int ChartContentHeight(int chartsRowHeight)
        {
            return chartsRowHeight - CardHeaderHeight - CardContentPadBottom;
        }
    }
}

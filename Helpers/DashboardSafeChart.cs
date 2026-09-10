using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace CaseManagement.Helpers
{
    // Chart مایکروسافت قرارداد دارد که Width/Height باید > 0px باشد؛
    // InspectChartDimensions در OnResize در غیر این صورت ArgumentException
    // می‌دهد. Dock=Fill داخل TabPage پنهان یا سلول TableLayout با ارتفاع صفر
    // همان مقدار نامعتبر را می‌فرستد. این کنترل اندازه را به حداقل قانونی
    // می‌رساند تا قرارداد Chart نقض نشود — نه اینکه استثنا را ببلعد.
    public class DashboardSafeChart : Chart
    {
        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (width < DashboardLayoutMath.MinChartPx)
                width = DashboardLayoutMath.MinChartPx;
            if (height < DashboardLayoutMath.MinChartPx)
                height = DashboardLayoutMath.MinChartPx;
            base.SetBoundsCore(x, y, width, height, specified);
        }
    }
}

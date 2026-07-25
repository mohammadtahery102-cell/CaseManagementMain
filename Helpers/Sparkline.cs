using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // نمودار خطیِ کوچک (Sparkline) داخل کارت‌های آماری — طبق طرح تصویریِ
    // درخواستی کاربر. کاملاً دستی رسم می‌شود (بدون کنترل Chart سنگین)، چون
    // در هر کارت فقط یک خط روند بدون محور/عنوان/لجند لازم است و ساختن پنج
    // نمونه Chart کامل هم کند است و هم ظاهرِ شلوغ می‌دهد.
    // ─────────────────────────────────────────────────────────────────────────
    public class Sparkline : Control
    {
        private double[] _values = new double[0];
        private Color _lineColor = UiTheme.Primary;

        public Sparkline()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                      ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 46;
        }

        public Color LineColor
        {
            get { return _lineColor; }
            set { _lineColor = value; Invalidate(); }
        }

        public void SetValues(double[] values)
        {
            _values = values ?? new double[0];
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_values.Length < 2 || Width < 4 || Height < 4)
                return;

            double min = _values[0], max = _values[0];
            foreach (double v in _values)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }
            // اگر همه‌ی مقادیر برابر باشند (مثلاً همه صفر)، یک خط صافِ وسط
            // رسم می‌شود به‌جای تقسیم بر صفر.
            double range = max - min;
            if (range <= 0.0000001) range = 1;

            int pad = 3;
            float usableW = Width - pad * 2;
            float usableH = Height - pad * 2;

            PointF[] points = new PointF[_values.Length];
            for (int i = 0; i < _values.Length; i++)
            {
                float x = pad + usableW * i / (_values.Length - 1);
                float y = pad + (float)(usableH * (1 - (_values[i] - min) / range));
                points[i] = new PointF(x, y);
            }

            // ناحیه‌ی زیر خط با رنگِ کم‌رنگ پر می‌شود (حس عمق، مثل طرح تصویری).
            PointF[] area = new PointF[points.Length + 2];
            Array.Copy(points, area, points.Length);
            area[points.Length] = new PointF(points[points.Length - 1].X, Height);
            area[points.Length + 1] = new PointF(points[0].X, Height);

            using (Brush fill = new SolidBrush(Color.FromArgb(38, _lineColor)))
                g.FillPolygon(fill, area);

            using (Pen pen = new Pen(_lineColor, 1.8f) { LineJoin = LineJoin.Round })
                g.DrawLines(pen, points);
        }
    }
}

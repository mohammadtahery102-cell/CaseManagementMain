using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // سوییچ روشن/خاموش گرد (به‌جای CheckBox معمولی) — طبق طرح تصویریِ درخواستی
    // کاربر (تنظیمات به سبک وب مدرن). کاملاً دستی رسم می‌شود (بدون کنترل بومی
    // ویندوز) تا در همه‌ی نسخه‌های ویندوز یکسان دیده شود. رنگ «روشن» از
    // UiTheme.Primary می‌آید، پس با رنگ سازمانی/تم انتخابی هماهنگ می‌ماند.
    // ─────────────────────────────────────────────────────────────────────────
    public class ToggleSwitch : Control
    {
        private bool _checked;
        private bool _hover;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(46, 24);
            Cursor = Cursors.Hand;
            RightToLeft = RightToLeft.Yes;
            TabStop = true;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
                Checked = !Checked;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackH = Height - 2;
            Rectangle track = new Rectangle(0, 1, Width - 1, trackH);
            int radius = trackH;

            Color trackColor = _checked
                ? (_hover ? ControlPaint.Light(UiTheme.Primary, 0.10f) : UiTheme.Primary)
                : (_hover ? ControlPaint.Dark(UiTheme.Border, 0.05f) : UiTheme.Border);

            using (GraphicsPath path = RoundedPill(track, radius))
            using (Brush b = new SolidBrush(trackColor))
                g.FillPath(b, path);

            int knobDiameter = trackH - 6;
            int knobY = track.Y + 3;
            // آموزش — چون کل برنامه RTL است، حالت «روشن» دایره را به سمت چپِ
            // بصری می‌برد (نقطه‌ی شروعِ خواندن در RTL)؛ اگر این کنترل جایی با
            // RightToLeft=No استفاده شود، خودش را خودکار آینه می‌کند.
            bool knobOnLeft = _checked == (RightToLeft == RightToLeft.Yes);
            int knobX = knobOnLeft ? track.X + 3 : track.Right - knobDiameter - 3;

            using (Brush knobBrush = new SolidBrush(Color.White))
                g.FillEllipse(knobBrush, knobX, knobY, knobDiameter, knobDiameter);

            if (Focused)
            {
                using (Pen focusPen = new Pen(ControlPaint.Dark(UiTheme.Primary, 0.15f), 1.4f))
                {
                    Rectangle focusRect = Rectangle.Inflate(track, -1, -1);
                    using (GraphicsPath fp = RoundedPill(focusRect, radius))
                        g.DrawPath(focusPen, fp);
                }
            }
        }

        private static GraphicsPath RoundedPill(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius, radius, 90, 180);
            path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 180);
            path.CloseFigure();
            return path;
        }
    }
}

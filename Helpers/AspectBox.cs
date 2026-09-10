using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // میزبانی که فرزندش را با نسبتِ ابعادِ ثابت، وسط‌چین نگه می‌دارد.
    //
    // آموزش — چرا لازم شد: کادرِ «عکس جمعی خانواده» با Dock=Fill هر شکلی که
    // کارت داشت می‌گرفت؛ چون کارت پهن و کوتاه بود، عکس عملاً یک نوارِ باریک
    // می‌شد (گزارشِ کاربر: «خیلی کوچک شده»). عکسِ خانوادگی معمولاً با موبایل
    // و به‌صورت عمودی گرفته می‌شود، یعنی ۹:۱۶ — پس کادر هم باید همان نسبت را
    // داشته باشد تا عکس بدونِ حاشیهٔ سیاهِ بزرگ و در بیشترین اندازهٔ ممکن
    // دیده شود.
    //
    // این کنترل هیچ چیزی نمی‌کشد و هیچ دانشی از عکس ندارد — فقط چیدمان.
    // ─────────────────────────────────────────────────────────────────────────
    public class AspectBox : Panel
    {
        private float _aspectWidth = 9f;
        private float _aspectHeight = 16f;

        public AspectBox()
        {
            BackColor = Color.Transparent;
        }

        // نسبتِ عرض به ارتفاع. پیش‌فرض ۹:۱۶ (عمودی، مثل عکسِ موبایل).
        public float AspectWidth
        {
            get { return _aspectWidth; }
            set { _aspectWidth = value <= 0 ? 1f : value; PerformLayout(); }
        }

        public float AspectHeight
        {
            get { return _aspectHeight; }
            set { _aspectHeight = value <= 0 ? 1f : value; PerformLayout(); }
        }

        // ضخامتِ قابِ دورِ محتوا (همان قابِ یک‌پیکسلیِ بقیهٔ کادرهای عکس).
        public int FrameThickness { get; set; }

        public Color FrameColor { get; set; }

        public AspectBox WithFrame(Color color, int thickness)
        {
            FrameColor = color;
            FrameThickness = thickness;
            return this;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            ArrangeChild();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ArrangeChild();
        }

        private void ArrangeChild()
        {
            if (Controls.Count == 0) return;

            Control child = Controls[0];

            int availableWidth = ClientSize.Width - Padding.Horizontal;
            int availableHeight = ClientSize.Height - Padding.Vertical;
            if (availableWidth <= 0 || availableHeight <= 0) return;

            // بزرگ‌ترین مستطیلِ با همین نسبت که داخلِ فضای موجود جا می‌شود.
            float ratio = _aspectWidth / _aspectHeight;
            int width = availableWidth;
            int height = (int)Math.Round(width / ratio);

            if (height > availableHeight)
            {
                height = availableHeight;
                width = (int)Math.Round(height * ratio);
            }

            if (width < 1) width = 1;
            if (height < 1) height = 1;

            child.Bounds = new Rectangle(
                Padding.Left + (availableWidth - width) / 2,
                Padding.Top + (availableHeight - height) / 2,
                width, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (FrameThickness <= 0 || Controls.Count == 0) return;

            Rectangle r = Controls[0].Bounds;
            r.Inflate(FrameThickness, FrameThickness);

            using (var pen = new Pen(FrameColor.IsEmpty ? UiTheme.Border : FrameColor, FrameThickness))
                e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
        }
    }
}

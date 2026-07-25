using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // «کارت» سفید گردگوشه با یک نوار عنوان (آیکون + متن) — بلوک بصری اصلیِ
    // طرحِ تصویریِ درخواستی کاربر برای صفحه‌ی تنظیمات. به‌جای Region-clipping
    // (که فرزندان را هم به شکل ناخواسته می‌بُرد)، گوشه‌های گرد با رسم مستقیم
    // (Fill رنگ سفید داخل یک مستطیلِ گردشده، روی زمینه‌ی خاکستریِ صفحه) کشیده
    // می‌شوند — چهار مثلثِ کوچکِ باقی‌مانده در گوشه‌ها همان رنگِ زمینه‌ی صفحه
    // (UiTheme.Background) را نشان می‌دهند، پس گوشه‌ی گرد بدون بریدنِ محتوا
    // دیده می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public class SettingsCardPanel : Panel
    {
        public Panel Content { get; private set; }

        private const int Radius = 14;

        public SettingsCardPanel(string icon, string title)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Background;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Transparent };

            Label lblIcon = new Label
            {
                Text = icon, Dock = DockStyle.Right, Width = 34,
                Font = new Font("Segoe UI Emoji", 12.5F), ForeColor = UiTheme.Primary,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Label lblTitle = new Label
            {
                Text = title, Dock = DockStyle.Fill,
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 14, 6, 0)
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblIcon);
            header.Padding = new Padding(18, 0, 18, 0);

            Content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 4, 18, 16), BackColor = Color.Transparent };

            Controls.Add(Content);
            Controls.Add(header);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            using (GraphicsPath path = RoundedRect(rect, Radius))
            {
                using (Brush fill = new SolidBrush(UiTheme.CardBack))
                    e.Graphics.FillPath(fill, path);
                using (Pen border = new Pen(UiTheme.Border, 1f))
                    e.Graphics.DrawPath(border, path);
            }

            base.OnPaint(e);
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

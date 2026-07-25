using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // کارت سفیدِ گردگوشه‌ی داشبورد با نوار عنوان: عنوان راست‌چین + یک کنترلِ
    // اختیاری در سمت چپ (مثل «مشاهده همه» یا کمبوی «۶ ماه اخیر» در طرح
    // تصویری). محتوای اصلی داخل Content قرار می‌گیرد.
    //
    // آموزش — چرا جدا از SettingsCardPanel: آن یکی عنوان + آیکون دارد ولی جای
    // «اکشن» در سمت مقابل ندارد، و کارت‌های داشبورد تقریباً همه یک کنترلِ
    // کنارِ عنوان دارند. به‌جای شلوغ‌کردنِ آن کلاس با پارامترهای اختیاری،
    // یک کارتِ مخصوصِ داشبورد ساخته شد.
    // ─────────────────────────────────────────────────────────────────────────
    public class DashboardCard : Panel
    {
        public Panel Content { get; private set; }
        public Panel HeaderRight { get; private set; }

        private const int Radius = 14;

        public DashboardCard(string title)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Background;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.Transparent, Padding = new Padding(16, 0, 16, 0) };

            // سمت چپِ سربرگ: جای کنترلِ اکشن (لینک/کمبو). خالی بماند اشکالی ندارد.
            HeaderRight = new Panel { Dock = DockStyle.Left, Width = 130, BackColor = Color.Transparent };

            Label lblTitle = new Label
            {
                Text = title, Dock = DockStyle.Fill, BackColor = Color.Transparent,
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            };

            header.Controls.Add(lblTitle);
            header.Controls.Add(HeaderRight);

            Content = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(14, 0, 14, 12) };

            Controls.Add(Content);
            Controls.Add(header);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = StatCard.RoundedRect(rect, Radius))
            {
                using (Brush fill = new SolidBrush(UiTheme.CardBack))
                    e.Graphics.FillPath(fill, path);
                using (Pen border = new Pen(UiTheme.Border, 1f))
                    e.Graphics.DrawPath(border, path);
            }
            base.OnPaint(e);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // یک ردیف از «آخرین فعالیت‌ها»: نشانِ گردِ رنگی + عنوان + شرح + زمان.
    // ─────────────────────────────────────────────────────────────────────────
    public class ActivityRow : Panel
    {
        public ActivityRow(string glyph, Color accent, string title, string detail, string time)
        {
            Dock = DockStyle.Top;
            Height = 62;
            BackColor = Color.Transparent;
            Padding = new Padding(0, 6, 0, 6);

            IconBadge badge = new IconBadge(glyph, accent) { Dock = DockStyle.Right, Width = 46 };

            Panel texts = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 6, 0) };

            Label lblTitle = new Label
            {
                Text = title, Dock = DockStyle.Top, Height = 20, BackColor = Color.Transparent,
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true
            };
            Label lblDetail = new Label
            {
                Text = detail, Dock = DockStyle.Top, Height = 17, BackColor = Color.Transparent,
                Font = UiTheme.Font(UiTheme.SizeSmall - 1F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true
            };
            Label lblTime = new Label
            {
                Text = time, Dock = DockStyle.Top, Height = 15, BackColor = Color.Transparent,
                Font = UiTheme.Font(UiTheme.SizeSmall - 2F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };

            texts.Controls.Add(lblTime);
            texts.Controls.Add(lblDetail);
            texts.Controls.Add(lblTitle);

            Controls.Add(texts);
            Controls.Add(badge);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // یک ردیف از «گزارش سریع»: نشانِ رنگی + عنوان/زیرعنوان + فلشِ باز کردن.
    // ─────────────────────────────────────────────────────────────────────────
    public class QuickReportRow : Panel
    {
        public QuickReportRow(string glyph, Color accent, string title, string subtitle, EventHandler onClick)
        {
            Dock = DockStyle.Top;
            Height = 56;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Padding = new Padding(0, 5, 0, 5);

            IconBadge badge = new IconBadge(glyph, accent) { Dock = DockStyle.Right, Width = 44 };

            Label lblChevron = new Label
            {
                Text = "‹", Dock = DockStyle.Left, Width = 22, BackColor = Color.Transparent,
                Font = UiTheme.FontBold(14F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter
            };

            Panel texts = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 4, 6, 0) };
            Label lblTitle = new Label
            {
                Text = title, Dock = DockStyle.Top, Height = 21, BackColor = Color.Transparent,
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            };
            Label lblSub = new Label
            {
                Text = subtitle, Dock = DockStyle.Top, Height = 17, BackColor = Color.Transparent,
                Font = UiTheme.Font(UiTheme.SizeSmall - 2F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };
            texts.Controls.Add(lblSub);
            texts.Controls.Add(lblTitle);

            Controls.Add(texts);
            Controls.Add(lblChevron);
            Controls.Add(badge);

            // کلیک روی هر جای ردیف کار کند (نه فقط روی خودِ پنل).
            EventHandler handler = delegate (object s, EventArgs e) { if (onClick != null) onClick(this, EventArgs.Empty); };
            Click += handler;
            foreach (Control c in new Control[] { texts, lblTitle, lblSub, lblChevron, badge })
                c.Click += handler;
        }
    }
}

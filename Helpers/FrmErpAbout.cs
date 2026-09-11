using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    public sealed class FrmErpAbout : Form
    {
        public FrmErpAbout()
        {
            Text = "درباره ما";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            ClientSize = new Size(520, 460);

            Panel card = new Panel
            {
                BackColor = UiTheme.CardBack,
                Location = new Point(24, 24),
                Size = new Size(472, 360)
            };
            UiTheme.RoundCorners(card, 12);

            Label brand = new Label
            {
                Text = ProductBranding.CommercialName,
                Font = UiTheme.FontBold(UiTheme.SizeTitle),
                ForeColor = UiTheme.PrimaryDark,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 24),
                Size = new Size(432, 36)
            };

            Label hint = new Label
            {
                Text = "نرم‌افزار یکپارچه مالی، انبار، فروش و مدیریت سازمان",
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 60),
                Size = new Size(432, 24)
            };

            int y = 108;
            card.Controls.Add(brand);
            card.Controls.Add(hint);
            card.Controls.Add(Row("نسخه نرم‌افزار", "v1.0.0", ref y));
            card.Controls.Add(Row("توسعه‌دهنده", "محمد طاهری", ref y));
            card.Controls.Add(Row("تلفن", "0728018000", ref y));
            card.Controls.Add(Row("ایمیل", "mohammadtahery103@gmail.com", ref y));

            Button close = UiTheme.CreateButton("بستن", "", UiTheme.Primary);
            close.Size = new Size(160, 40);
            close.Location = new Point((ClientSize.Width - 160) / 2, 400);
            close.Click += delegate { Close(); };

            Controls.Add(card);
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
        }

        private static Panel Row(string label, string value, ref int y)
        {
            Panel row = new Panel
            {
                Location = new Point(28, y),
                Size = new Size(416, 52),
                BackColor = Color.FromArgb(248, 250, 252)
            };
            Label k = new Label
            {
                Text = label,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Top,
                Height = 20,
                Padding = new Padding(12, 6, 12, 0)
            };
            Label v = new Label
            {
                Text = value,
                Font = UiTheme.FontBold(UiTheme.SizeBody),
                ForeColor = UiTheme.TextDark,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 0, 12, 8)
            };
            row.Controls.Add(v);
            row.Controls.Add(k);
            y += 58;
            return row;
        }
    }
}

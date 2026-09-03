using System;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.AI
{
    // دستیار هوشمند — فاز ۱. کارتِ نمایشِ یک نتیجه (پرونده/عضو خانواده/
    // یادآوری) در گفتگو — راست‌چین، با دکمه‌ی «باز کردن پرونده».
    public class AiResultCard : Panel
    {
        public AiResultCard(AiResultItem item, Action<AiResultItem> onOpen)
        {
            RightToLeft = RightToLeft.Yes;
            BackColor = Color.White;
            Width = 420;
            Height = 64;
            Padding = new Padding(10, 6, 10, 6);
            Margin = new Padding(4);
            BorderStyle = BorderStyle.FixedSingle;

            Label lblTitle = new Label
            {
                Text = item.DisplayTitle,
                Font = UiTheme.FontBold(9.5f),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleRight,
                RightToLeft = RightToLeft.Yes
            };

            Label lblSubtitle = new Label
            {
                Text = item.DisplaySubtitle,
                Font = UiTheme.Font(8.5f),
                ForeColor = Color.DimGray,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 18,
                TextAlign = ContentAlignment.MiddleRight,
                RightToLeft = RightToLeft.Yes
            };

            if (string.Equals(item.EntityType, "Reminder", StringComparison.OrdinalIgnoreCase))
            {
                Label lblBadge = new Label
                {
                    Text = "🤖 ایجادشده توسط دستیار هوشمند",
                    Font = UiTheme.Font(8f),
                    ForeColor = Color.SeaGreen,
                    AutoSize = false,
                    Dock = DockStyle.Bottom,
                    Height = 18,
                    TextAlign = ContentAlignment.MiddleRight,
                    RightToLeft = RightToLeft.Yes
                };
                Controls.Add(lblBadge);
            }
            else
            {
                Button btnOpen = new Button
                {
                    Text = "باز کردن پرونده",
                    Dock = DockStyle.Bottom,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    RightToLeft = RightToLeft.Yes
                };
                btnOpen.Click += delegate { if (onOpen != null) onOpen(item); };
                Controls.Add(btnOpen);
            }

            Controls.Add(lblSubtitle);
            Controls.Add(lblTitle);
        }
    }
}

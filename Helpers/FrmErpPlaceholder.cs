using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    public sealed class FrmErpPlaceholder : Form
    {
        public FrmErpPlaceholder(string title)
        {
            Text = string.IsNullOrWhiteSpace(title) ? ProductBranding.Brand : title;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            ClientSize = new Size(480, 220);

            Label msg = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(24),
                ForeColor = UiTheme.TextDark,
                Text = (title ?? "") + Environment.NewLine + Environment.NewLine +
                       "«" + ReportingCatalog.SoonHint + "»"
            };
            Button ok = UiTheme.CreateButton("بستن", "", UiTheme.Primary);
            ok.Dock = DockStyle.Bottom;
            ok.Height = 44;
            ok.Click += delegate { Close(); };
            Controls.Add(msg);
            Controls.Add(ok);
        }
    }
}

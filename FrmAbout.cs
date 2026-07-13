using CaseManagement.Helpers;
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace CaseManagement
{
    // درباره برنامه — لوگو، نام مؤسسه/نرم‌افزار و نسخه (بند ۹ بازطراحی ظاهری)
    public class FrmAbout : Form
    {
        public FrmAbout()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text              = "درباره برنامه";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.CardBack;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeFixedSize(this, 420, 420);

            PictureBox picLogo = new PictureBox();
            picLogo.Image = LogoHelper.GetLogoImage();
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.Size = new Size(110, 110);
            picLogo.Location = new Point((ClientSize.Width - 110) / 2, 30);
            UiTheme.RoundCorners(picLogo, 110);
            Controls.Add(picLogo);

            string orgName = SettingsHelper.Get(SettingsHelper.OrgName);

            Label lblAppName = new Label();
            lblAppName.Text = string.IsNullOrWhiteSpace(orgName) ? "سیستم مدیریت پرونده‌ها" : orgName;
            lblAppName.Font = UiTheme.FontBold(UiTheme.SizeTitle);
            lblAppName.ForeColor = UiTheme.PrimaryDark;
            lblAppName.AutoSize = false;
            lblAppName.TextAlign = ContentAlignment.MiddleCenter;
            lblAppName.SetBounds(20, 155, ClientSize.Width - 40, 32);
            Controls.Add(lblAppName);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "سیستم مدیریت پرونده‌های اجتماعی";
            lblSubtitle.Font = UiTheme.Font(UiTheme.SizeMedium);
            lblSubtitle.ForeColor = UiTheme.TextMuted;
            lblSubtitle.AutoSize = false;
            lblSubtitle.TextAlign = ContentAlignment.MiddleCenter;
            lblSubtitle.SetBounds(20, 190, ClientSize.Width - 40, 26);
            Controls.Add(lblSubtitle);

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            Label lblVersion = new Label();
            lblVersion.Text = "نسخه " + version.ToString();
            lblVersion.Font = UiTheme.Font(UiTheme.SizeBody);
            lblVersion.ForeColor = UiTheme.TextMuted;
            lblVersion.AutoSize = false;
            lblVersion.TextAlign = ContentAlignment.MiddleCenter;
            lblVersion.SetBounds(20, 226, ClientSize.Width - 40, 24);
            Controls.Add(lblVersion);

            string address = SettingsHelper.Get(SettingsHelper.Address);
            string phone   = SettingsHelper.Get(SettingsHelper.Phone);
            string email   = SettingsHelper.Get(SettingsHelper.Email);

            Label lblContact = new Label();
            lblContact.Text =
                (string.IsNullOrWhiteSpace(address) ? "" : "آدرس: " + address + Environment.NewLine) +
                (string.IsNullOrWhiteSpace(phone)   ? "" : "تلفن: " + phone + Environment.NewLine) +
                (string.IsNullOrWhiteSpace(email)   ? "" : "ایمیل: " + email);
            lblContact.Font = UiTheme.Font(UiTheme.SizeSmall);
            lblContact.ForeColor = UiTheme.TextMuted;
            lblContact.AutoSize = false;
            lblContact.TextAlign = ContentAlignment.TopCenter;
            lblContact.SetBounds(20, 260, ClientSize.Width - 40, 80);
            Controls.Add(lblContact);

            Button btnClose = UiTheme.CreateButton("بستن", "", UiTheme.Primary);
            btnClose.SetBounds((ClientSize.Width - 140) / 2, 350, 140, 38);
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;
        }
    }
}

using CaseManagement.Helpers;
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace CaseManagement
{
    // درباره برنامه — لوگو، نام مؤسسه/نرم‌افزار، نسخه و وضعیت لایسنس.
    public class FrmAbout : Form
    {
        private Label _lblLicenseStatus;
        private TextBox _txtHardwareId;

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
            UiTheme.MakeFixedSize(this, 440, 560);

            PictureBox picLogo = new PictureBox();
            picLogo.Image = LogoHelper.GetLogoImage();
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.Size = new Size(104, 104);
            picLogo.Location = new Point((ClientSize.Width - 104) / 2, 24);
            UiTheme.RoundCorners(picLogo, 104);
            Controls.Add(picLogo);

            string orgName = SettingsHelper.Get(SettingsHelper.OrgName);

            Label lblAppName = new Label();
            lblAppName.Text = string.IsNullOrWhiteSpace(orgName) ? "سیستم مدیریت پرونده‌ها" : orgName;
            lblAppName.Font = UiTheme.FontBold(UiTheme.SizeTitle);
            lblAppName.ForeColor = UiTheme.PrimaryDark;
            lblAppName.AutoSize = false;
            lblAppName.TextAlign = ContentAlignment.MiddleCenter;
            lblAppName.SetBounds(20, 138, ClientSize.Width - 40, 30);
            Controls.Add(lblAppName);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "سیستم مدیریت پرونده‌های اجتماعی";
            lblSubtitle.Font = UiTheme.Font(UiTheme.SizeMedium);
            lblSubtitle.ForeColor = UiTheme.TextMuted;
            lblSubtitle.AutoSize = false;
            lblSubtitle.TextAlign = ContentAlignment.MiddleCenter;
            lblSubtitle.SetBounds(20, 170, ClientSize.Width - 40, 24);
            Controls.Add(lblSubtitle);

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            Label lblVersion = new Label();
            lblVersion.Text = string.Format(Lang.T("نسخه {0}"), version.ToString());
            lblVersion.Font = UiTheme.Font(UiTheme.SizeBody);
            lblVersion.ForeColor = UiTheme.TextMuted;
            lblVersion.AutoSize = false;
            lblVersion.TextAlign = ContentAlignment.MiddleCenter;
            lblVersion.SetBounds(20, 196, ClientSize.Width - 40, 22);
            Controls.Add(lblVersion);

            // ─── کارت لایسنس ─────────────────────────────────────────────────
            Panel licenseCard = new Panel();
            licenseCard.SetBounds(20, 228, ClientSize.Width - 40, 156);
            licenseCard.BackColor = UiTheme.Background;
            licenseCard.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(licenseCard);

            Label lblLicTitle = new Label();
            lblLicTitle.Text = "وضعیت لایسنس";
            lblLicTitle.Font = UiTheme.FontBold(UiTheme.SizeSmall);
            lblLicTitle.ForeColor = UiTheme.TextDark;
            lblLicTitle.AutoSize = false;
            lblLicTitle.TextAlign = ContentAlignment.MiddleRight;
            lblLicTitle.SetBounds(12, 10, licenseCard.Width - 24, 22);
            licenseCard.Controls.Add(lblLicTitle);

            _lblLicenseStatus = new Label();
            _lblLicenseStatus.Font = UiTheme.FontBold(UiTheme.SizeBody);
            _lblLicenseStatus.AutoSize = false;
            _lblLicenseStatus.TextAlign = ContentAlignment.MiddleRight;
            _lblLicenseStatus.SetBounds(12, 34, licenseCard.Width - 24, 24);
            licenseCard.Controls.Add(_lblLicenseStatus);

            Label lblHwTitle = new Label();
            lblHwTitle.Text = "شناسه این دستگاه:";
            lblHwTitle.Font = UiTheme.Font(UiTheme.SizeSmall);
            lblHwTitle.ForeColor = UiTheme.TextMuted;
            lblHwTitle.AutoSize = false;
            lblHwTitle.TextAlign = ContentAlignment.MiddleRight;
            lblHwTitle.SetBounds(12, 66, licenseCard.Width - 24, 20);
            licenseCard.Controls.Add(lblHwTitle);

            // شناسه سخت‌افزار — TextBox فقط‌خواندنی و قابل انتخاب/کپی (برای ارسال به فروشنده).
            _txtHardwareId = new TextBox();
            _txtHardwareId.ReadOnly = true;
            _txtHardwareId.Text = LicenseManager.GetHardwareId();
            _txtHardwareId.Font = new Font("Consolas", 10F, FontStyle.Bold);
            _txtHardwareId.TextAlign = HorizontalAlignment.Center;
            _txtHardwareId.BorderStyle = BorderStyle.FixedSingle;
            _txtHardwareId.SetBounds(12, 88, licenseCard.Width - 130, 26);
            licenseCard.Controls.Add(_txtHardwareId);

            Button btnCopy = UiTheme.CreateSecondaryButton("کپی", "⧉");
            btnCopy.SetBounds(licenseCard.Width - 110, 88, 96, 26);
            btnCopy.Click += delegate
            {
                try { Clipboard.SetText(_txtHardwareId.Text); UiTheme.ShowSuccess(this, "شناسه دستگاه کپی شد."); }
                catch { }
            };
            licenseCard.Controls.Add(btnCopy);

            Button btnActivate = UiTheme.CreateButton("فعال‌سازی لایسنس", "🔑", UiTheme.Primary);
            btnActivate.SetBounds(12, 120, licenseCard.Width - 24, 30);
            btnActivate.Click += BtnActivate_Click;
            licenseCard.Controls.Add(btnActivate);

            RefreshLicenseStatus();

            // ─── تماس ────────────────────────────────────────────────────────
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
            lblContact.SetBounds(20, 392, ClientSize.Width - 40, 66);
            Controls.Add(lblContact);

            Button btnClose = UiTheme.CreateButton("بستن", "", UiTheme.Primary);
            btnClose.SetBounds((ClientSize.Width - 140) / 2, 470, 140, 36);
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;
        }

        private void RefreshLicenseStatus()
        {
            LicenseManager.Invalidate();
            LicenseInfo lic = LicenseManager.Current;
            _lblLicenseStatus.Text = lic.StatusDisplay +
                (string.IsNullOrWhiteSpace(lic.LicensedTo) ? "" : "  —  " + lic.LicensedTo);

            switch (lic.Status)
            {
                case LicenseStatus.Active:
                    _lblLicenseStatus.ForeColor = UiTheme.Success; break;
                case LicenseStatus.Expired:
                case LicenseStatus.Invalid:
                case LicenseStatus.MachineMismatch:
                    _lblLicenseStatus.ForeColor = UiTheme.Danger; break;
                default:
                    _lblLicenseStatus.ForeColor = UiTheme.Warning; break;
            }
        }

        private void BtnActivate_Click(object sender, EventArgs e)
        {
            string token = PromptForToken();
            if (string.IsNullOrWhiteSpace(token))
                return;

            string message;
            bool ok = LicenseManager.Activate(token, out message);
            if (ok) UiTheme.ShowSuccess(this, message);
            else UiTheme.ShowError(this, message);

            RefreshLicenseStatus();
        }

        // دیالوگ کوچک دریافت توکن لایسنس (چون WinForms InputBox ندارد).
        private string PromptForToken()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "فعال‌سازی لایسنس";
                dlg.RightToLeft = RightToLeft.Yes;
                dlg.RightToLeftLayout = true;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(460, 220);
                dlg.BackColor = UiTheme.CardBack;
                dlg.Font = UiTheme.Font(UiTheme.SizeBody);

                Label lbl = new Label();
                lbl.Text = "توکن لایسنس دریافتی از فروشنده را وارد کنید:";
                lbl.Font = UiTheme.FontBold(UiTheme.SizeSmall);
                lbl.ForeColor = UiTheme.TextDark;
                lbl.AutoSize = false;
                lbl.TextAlign = ContentAlignment.MiddleRight;
                lbl.SetBounds(16, 12, dlg.ClientSize.Width - 32, 24);
                dlg.Controls.Add(lbl);

                TextBox txt = new TextBox();
                txt.Multiline = true;
                txt.ScrollBars = ScrollBars.Vertical;
                txt.BorderStyle = BorderStyle.FixedSingle;
                txt.Font = new Font("Consolas", 9.5F);
                txt.SetBounds(16, 42, dlg.ClientSize.Width - 32, 110);
                dlg.Controls.Add(txt);

                Button ok = UiTheme.CreateButton("فعال‌سازی", "", UiTheme.Primary);
                ok.SetBounds(dlg.ClientSize.Width - 150, 164, 134, 36);
                ok.DialogResult = DialogResult.OK;
                dlg.Controls.Add(ok);

                Button cancel = UiTheme.CreateSecondaryButton("انصراف", "");
                cancel.SetBounds(dlg.ClientSize.Width - 290, 164, 130, 36);
                cancel.DialogResult = DialogResult.Cancel;
                dlg.Controls.Add(cancel);

                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;

                return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }
    }
}

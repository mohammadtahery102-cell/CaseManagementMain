using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // FrmPasswordPrompt — دیالوگِ کوچکِ گرفتنِ رمزِ بکاپ (نسخهٔ ۱٫۰ — Option D).
    //
    // دو حالت:
    //   requireConfirmation = true  → ساختِ بکاپِ تازه (رمز + تکرارِ رمز، با
    //                                   حداقلِ طول، همان الگویی که FrmChangePassword دارد)
    //   requireConfirmation = false → بازیابی/بررسیِ یک بکاپِ موجود (فقط یک فیلدِ رمز)
    //
    // آموزش — چرا فرمِ جدا و نه MessageBox: هیچ‌کدام از کادرهای پیامِ WinForms
    // فیلدِ ورودیِ رمز ندارند؛ این کوچک‌ترین فرمِ ممکن برای همین یک کار است،
    // دقیقاً هم‌سبک با FrmChangePassword.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class FrmPasswordPrompt : Form
    {
        private readonly bool _requireConfirmation;

        private TextBox _txtPassword;
        private TextBox _txtConfirm;
        private Label   _lblMessage;
        private Button  _btnOk;
        private Button  _btnCancel;

        public string Password { get; private set; } = "";

        private FrmPasswordPrompt(string title, string instruction, bool requireConfirmation)
        {
            _requireConfirmation = requireConfirmation;
            BuildUi(title, instruction);
        }

        // خروجی true یعنی کاربر OK زد و Password پر شده است.
        public static bool TryPrompt(IWin32Window owner, string title, string instruction,
                                      bool requireConfirmation, out string password)
        {
            using (var frm = new FrmPasswordPrompt(title, instruction, requireConfirmation))
            {
                bool ok = frm.ShowDialog(owner) == DialogResult.OK;
                password = ok ? frm.Password : "";
                return ok;
            }
        }

        private void BuildUi(string title, string instruction)
        {
            Text              = title;
            StartPosition     = FormStartPosition.CenterParent;
            ClientSize        = new Size(420, _requireConfirmation ? 260 : 200);
            FormBorderStyle   = FormBorderStyle.FixedDialog;
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            MinimizeBox       = false;
            MaximizeBox       = false;
            try { Icon = LogoHelper.GetAppIcon(); } catch { }

            int labelX = 285;
            int inputX = 30;
            int inputW = 240;
            int y = 20;

            if (!string.IsNullOrWhiteSpace(instruction))
            {
                Controls.Add(new Label
                {
                    Text = instruction,
                    Bounds = new Rectangle(20, y, 380, 40),
                    TextAlign = ContentAlignment.MiddleRight
                });
                y += 45;
            }

            Controls.Add(new Label { Text = "رمز عبور بکاپ", Location = new Point(labelX, y + 5), AutoSize = true });
            _txtPassword = new TextBox { Bounds = new Rectangle(inputX, y, inputW, 25), PasswordChar = '*' };
            Controls.Add(_txtPassword);
            y += 45;

            if (_requireConfirmation)
            {
                Controls.Add(new Label { Text = "تکرار رمز عبور", Location = new Point(labelX, y + 5), AutoSize = true });
                _txtConfirm = new TextBox { Bounds = new Rectangle(inputX, y, inputW, 25), PasswordChar = '*' };
                Controls.Add(_txtConfirm);
                y += 45;
            }

            _lblMessage = new Label
            {
                Bounds    = new Rectangle(20, y, 380, 40),
                ForeColor = Color.Maroon,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(_lblMessage);
            y += 45;

            _btnOk = new Button { Text = "تأیید", Bounds = new Rectangle(inputX, y, 130, 35) };
            _btnOk.Click += BtnOk_Click;
            Controls.Add(_btnOk);

            _btnCancel = new Button { Text = "انصراف", Bounds = new Rectangle(175, y, 95, 35) };
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnCancel);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            _lblMessage.Text = "";

            string pwd = _txtPassword.Text;

            // آموزش — همان حداقلِ طولِ رمزِ کاربری (SettingsHelper.MinPasswordLength)
            // اینجا هم به‌کار می‌رود؛ یک سیاستِ رمز برای کل برنامه، نه دو قاعدهٔ جدا.
            int minLength = SettingsHelper.GetInt(SettingsHelper.MinPasswordLength, 6);

            if (string.IsNullOrEmpty(pwd))
            {
                _lblMessage.Text = "رمز عبور را وارد کنید.";
                return;
            }

            if (_requireConfirmation)
            {
                if (pwd.Length < minLength)
                {
                    _lblMessage.Text = "رمز عبور باید حداقل " + minLength + " کاراکتر باشد.";
                    return;
                }

                if (pwd != _txtConfirm.Text)
                {
                    _lblMessage.Text = "تکرار رمز با رمز واردشده مطابقت ندارد.";
                    return;
                }
            }

            Password = pwd;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement
{
    // ─────────────────────────────────────────────────────────────────────────
    // FrmChangePassword — دیالوگ امن تغییر رمز عبور
    //
    // آموزش — دو حالت استفاده:
    //   forced=true  → تغییر رمز اجباری (اولین ورود) — کاربر نمی‌تواند رد کند
    //   forced=false → تغییر ارادی — کاربر می‌تواند انصراف دهد
    //
    // اصل امنیتی: در هر دو حالت، رمز فعلی الزامی است.
    //   دلیل: جلوگیری از تغییر رمز توسط کسی که Session باز را پیدا کرده.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmChangePassword : Form
    {
        private readonly DatabaseHelper _db  = new DatabaseHelper();
        private readonly string         _username;
        private readonly bool           _forced;

        private TextBox _txtCurrent;
        private TextBox _txtNew;
        private TextBox _txtConfirm;
        private Label   _lblMessage;
        private Button  _btnSave;
        private Button  _btnCancel;

        public FrmChangePassword(string username, bool forced)
        {
            _username = (username ?? "").Trim();
            _forced   = forced;
            BuildUi();
        }

        // ─── ساخت رابط کاربری ────────────────────────────────────────────────
        private void BuildUi()
        {
            Text              = _forced ? "تغییر رمز اجباری — لطفاً رمز را عوض کنید" : "تغییر رمز عبور";
            StartPosition     = FormStartPosition.CenterParent;
            ClientSize        = new Size(420, 290);
            FormBorderStyle   = FormBorderStyle.FixedDialog;
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            MinimizeBox       = true;
            MaximizeBox       = false;
            try { Icon = LogoHelper.GetAppIcon(); } catch { }

            // اگر اجباری است، کاربر با ضربدر هم نمی‌تواند ببندد
            FormClosing += (s, e) =>
            {
                if (_forced && DialogResult != DialogResult.OK)
                    e.Cancel = true;
            };

            int labelX  = 285;
            int inputX  = 30;
            int inputW  = 240;

            // ─── رمز فعلی ─────────────────────────────────────────────────
            // آموزش: این مهم‌ترین بخش است.
            // بدون این فیلد، هر کسی که پای سیستم نشسته می‌تواند رمز را عوض کند.
            Controls.Add(new Label { Text = "رمز فعلی", Location = new Point(labelX, 30), AutoSize = true });
            _txtCurrent = new TextBox { Bounds = new Rectangle(inputX, 25, inputW, 25), PasswordChar = '*' };
            Controls.Add(_txtCurrent);

            // ─── رمز جدید ─────────────────────────────────────────────────
            Controls.Add(new Label { Text = "رمز جدید (حداقل ۶ کاراکتر)", Location = new Point(labelX, 75), AutoSize = true });
            _txtNew = new TextBox { Bounds = new Rectangle(inputX, 70, inputW, 25), PasswordChar = '*' };
            Controls.Add(_txtNew);

            // ─── تکرار رمز جدید ───────────────────────────────────────────
            Controls.Add(new Label { Text = "تکرار رمز جدید", Location = new Point(labelX, 120), AutoSize = true });
            _txtConfirm = new TextBox { Bounds = new Rectangle(inputX, 115, inputW, 25), PasswordChar = '*' };
            Controls.Add(_txtConfirm);

            // ─── پیام خطا ─────────────────────────────────────────────────
            _lblMessage = new Label
            {
                Bounds    = new Rectangle(20, 155, 380, 50),
                ForeColor = Color.Maroon,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(_lblMessage);

            // ─── دکمه ذخیره ───────────────────────────────────────────────
            _btnSave = new Button
            {
                Text   = "ذخیره رمز جدید",
                Bounds = new Rectangle(inputX, 215, 130, 35)
            };
            _btnSave.Click += BtnSave_Click;
            Controls.Add(_btnSave);

            // ─── دکمه انصراف (فقط در حالت غیراجباری) ───────────────────
            _btnCancel = new Button
            {
                Text    = "انصراف",
                Bounds  = new Rectangle(175, 215, 95, 35),
                Enabled = !_forced
            };
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
        }

        // ─── منطق ذخیره رمز ──────────────────────────────────────────────────
        private void BtnSave_Click(object sender, EventArgs e)
        {
            _lblMessage.ForeColor = Color.Maroon;
            _lblMessage.Text      = "";

            string current = _txtCurrent.Text;
            string newPass = _txtNew.Text;
            string confirm = _txtConfirm.Text;

            // ─── اعتبارسنجی ورودی ─────────────────────────────────────────
            if (string.IsNullOrEmpty(current))
            {
                _lblMessage.Text = "رمز فعلی را وارد کنید.";
                return;
            }

            // آموزش — سیاست رمز عبور (قابل تنظیم از تب امنیت در Control Center):
            int minPasswordLength = SettingsHelper.GetInt(SettingsHelper.MinPasswordLength, 6);
            if (string.IsNullOrWhiteSpace(newPass) || newPass.Length < minPasswordLength)
            {
                _lblMessage.Text = "رمز جدید باید حداقل " + minPasswordLength + " کاراکتر باشد.";
                return;
            }

            if (newPass != confirm)
            {
                _lblMessage.Text = "تکرار رمز با رمز جدید مطابقت ندارد.";
                return;
            }

            if (newPass == current)
            {
                _lblMessage.Text = "رمز جدید نباید با رمز فعلی یکسان باشد.";
                return;
            }

            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                {
                    con.Open();

                    // ─── خواندن hash/salt فعلی ─────────────────────────────
                    // آموزش: هرگز رمز خام در دیتابیس ذخیره نمی‌شود.
                    // فقط hash و salt را می‌خوانیم تا رمز فعلی را تأیید کنیم.
                    byte[] oldHash;
                    byte[] oldSalt;
                    int    oldIterations;
                    int    userId;

                    using (var readCmd = new SQLiteCommand(@"
SELECT UserID, PasswordHash, PasswordSalt, PasswordIterations
FROM   TblUsers
WHERE  Username = @u AND IsActive = 1
LIMIT  1", con))
                    {
                        readCmd.Parameters.AddWithValue("@u", _username);
                        using (var dr = readCmd.ExecuteReader())
                        {
                            if (!dr.Read())
                            {
                                _lblMessage.Text = "کاربر یافت نشد یا غیرفعال است.";
                                return;
                            }
                            userId  = Convert.ToInt32(dr["UserID"]);
                            oldHash = (byte[])dr["PasswordHash"];
                            oldSalt = (byte[])dr["PasswordSalt"];
                            oldIterations = dr["PasswordIterations"] == DBNull.Value
                                ? 0 : Convert.ToInt32(dr["PasswordIterations"]);
                        }
                    }

                    // ─── تأیید رمز فعلی — قلب امنیت این فرم ─────────────
                    // آموزش: PasswordHelper.Verify رمز وارد شده را با همان
                    // الگوریتم PBKDF2 هش می‌کند و با hash ذخیره‌شده مقایسه می‌کند.
                    // از FixedTimeEquals استفاده می‌شود تا Timing Attack ناممکن باشد.
                    if (!PasswordHelper.Verify(current, oldHash, oldSalt, oldIterations))
                    {
                        _lblMessage.Text = "رمز فعلی نادرست است.";
                        return;
                    }

                    // ─── ساخت hash جدید ────────────────────────────────────
                    // آموزش: هر بار salt تازه تولید می‌شود (با RNG واقعی).
                    // این یعنی حتی اگر دو کاربر رمز یکسان داشته باشند،
                    // hash‌های آن‌ها کاملاً متفاوت است. همچنین با تغییر رمز،
                    // کاربر به‌طور خودکار به تعداد Iteration جدید و امن‌تر ارتقا می‌یابد.
                    byte[] newHash;
                    byte[] newSalt;
                    int    newIterations;
                    PasswordHelper.CreateHash(newPass, out newHash, out newSalt, out newIterations);

                    // ─── به‌روزرسانی دیتابیس ───────────────────────────────
                    // MustChangePassword = 0 یعنی کاربر رمز را تغییر داده
                    using (var updCmd = new SQLiteCommand(@"
UPDATE TblUsers
SET    PasswordHash       = @h,
       PasswordSalt       = @s,
       PasswordIterations = @it,
       MustChangePassword = 0,
       LastPasswordChangeAt = datetime('now')
WHERE  UserID = @id", con))
                    {
                        updCmd.Parameters.AddWithValue("@h",  newHash);
                        updCmd.Parameters.AddWithValue("@s",  newSalt);
                        updCmd.Parameters.AddWithValue("@it", newIterations);
                        updCmd.Parameters.AddWithValue("@id", userId);
                        updCmd.ExecuteNonQuery();
                    }

                    AuditLogger.Log("تغییر رمز", "TblUsers", userId, "", _username);
                }

                Msg.Show(
                    "رمز با موفقیت تغییر کرد.",
                    "موفق",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _lblMessage.Text = "خطا: " + ex.Message;
            }
        }
    }
}

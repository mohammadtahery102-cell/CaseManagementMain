using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement
{
    public class FrmLogin : Form
    {
        private readonly DatabaseHelper _db = new DatabaseHelper();

        private ComboBox _cmbCenter;
        private TextBox  _txtUsername;
        private TextBox  _txtPassword;
        private Button   _btnLogin;
        private Button   _btnShowPass;
        private Button   _btnChangePass;
        private Label    _lblMessage;

        public FrmLogin()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text              = "ورود به سیستم";
            StartPosition     = FormStartPosition.CenterScreen;
            ClientSize        = new Size(460, 700);
            FormBorderStyle   = FormBorderStyle.FixedDialog;
            MaximizeBox       = false;
            MinimizeBox       = true;
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(10.5f);
            try { Icon = LogoHelper.GetAppIcon(); } catch { }

            // ─── کارت مرکزی ورود ─────────────────────────────────────────
            Panel card = new Panel();
            card.BackColor = UiTheme.CardBack;
            card.Size = new Size(370, 626);
            card.Location = new Point((ClientSize.Width - card.Width) / 2, 32);
            UiTheme.RoundCorners(card, 18);
            Controls.Add(card);

            // ─── لوگوی نرم‌افزار (بزرگ‌شده به‌درخواست کاربر ~۲ برابر) ───────
            PictureBox logo = new PictureBox();
            logo.Image = LogoHelper.GetLogoImage();
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = UiTheme.Primary;
            logo.Size = new Size(140, 140);
            logo.Location = new Point((card.Width - logo.Width) / 2, 20);
            UiTheme.RoundCorners(logo, 140);
            card.Controls.Add(logo);

            Label title = new Label();
            title.Text = "سیستم مدیریت پرونده";
            title.Font = UiTheme.FontBold(14F);
            title.ForeColor = UiTheme.TextDark;
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.SetBounds(20, 172, card.Width - 40, 28);
            card.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "برای ادامه وارد حساب کاربری خود شوید";
            subtitle.Font = UiTheme.Font(9.5F);
            subtitle.ForeColor = UiTheme.TextMuted;
            subtitle.TextAlign = ContentAlignment.MiddleCenter;
            subtitle.SetBounds(20, 202, card.Width - 40, 22);
            card.Controls.Add(subtitle);

            int fieldX = 30;
            int fieldW = card.Width - 60;

            // ─── مرکز ────────────────────────────────────────────────────
            Label lblCenter = new Label();
            lblCenter.Text = "مرکز";
            lblCenter.Font = UiTheme.FontBold(9.5F);
            lblCenter.ForeColor = UiTheme.TextDark;
            lblCenter.SetBounds(fieldX, 238, fieldW, 20);
            card.Controls.Add(lblCenter);

            _cmbCenter = new ComboBox();
            _cmbCenter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbCenter.Font = UiTheme.Font(10.5F);
            _cmbCenter.Bounds = new Rectangle(fieldX, 260, fieldW, 30);
            card.Controls.Add(_cmbCenter);

            // ─── نام کاربری ──────────────────────────────────────────────
            Label lblUser = new Label();
            lblUser.Text = "نام کاربری";
            lblUser.Font = UiTheme.FontBold(9.5F);
            lblUser.ForeColor = UiTheme.TextDark;
            lblUser.SetBounds(fieldX, 306, fieldW, 20);
            card.Controls.Add(lblUser);

            _txtUsername = new TextBox { Bounds = new Rectangle(fieldX, 328, fieldW, 30) };
            UiTheme.StyleTextBox(_txtUsername);
            card.Controls.Add(_txtUsername);

            // ─── رمز عبور ────────────────────────────────────────────────
            Label lblPass = new Label();
            lblPass.Text = "رمز عبور";
            lblPass.Font = UiTheme.FontBold(9.5F);
            lblPass.ForeColor = UiTheme.TextDark;
            lblPass.SetBounds(fieldX, 374, fieldW, 20);
            card.Controls.Add(lblPass);

            _txtPassword = new TextBox
            {
                Bounds       = new Rectangle(fieldX, 396, fieldW - 45, 30),
                PasswordChar = '*'
            };
            UiTheme.StyleTextBox(_txtPassword);
            card.Controls.Add(_txtPassword);

            _btnShowPass = UiTheme.CreateSecondaryButton("", "⊙");
            _btnShowPass.Bounds = new Rectangle(fieldX + fieldW - 40, 396, 40, 30);
            _btnShowPass.Click += (s, e) =>
                _txtPassword.PasswordChar = _txtPassword.PasswordChar == '*' ? '\0' : '*';
            card.Controls.Add(_btnShowPass);

            // ─── دکمه‌های اقدام ──────────────────────────────────────────
            _btnLogin = UiTheme.CreateButton("ورود به سیستم", "⚿", UiTheme.Primary);
            _btnLogin.Bounds = new Rectangle(fieldX, 452, fieldW, 42);
            _btnLogin.Click += BtnLogin_Click;
            card.Controls.Add(_btnLogin);

            _btnChangePass = UiTheme.CreateSecondaryButton("تغییر رمز عبور", "↻");
            _btnChangePass.Bounds = new Rectangle(fieldX, 502, fieldW, 38);
            _btnChangePass.Click += BtnChangePass_Click;
            card.Controls.Add(_btnChangePass);

            _lblMessage = new Label
            {
                Bounds    = new Rectangle(fieldX, 548, fieldW, 64),
                Font      = UiTheme.Font(9.5F),
                ForeColor = UiTheme.Danger,
                TextAlign = ContentAlignment.TopCenter
            };
            card.Controls.Add(_lblMessage);

            AcceptButton = _btnLogin;

            // بارگذاری مراکز پس از ساخت UI
            LoadCenters();
        }

        // ─── بارگذاری مراکز از دیتابیس ──────────────────────────────────────
        private void LoadCenters()
        {
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT CenterID, CenterCode, CenterName
FROM   TblCenter
WHERE  IsActive = 1
ORDER  BY CenterCode", con))
                {
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            _cmbCenter.Items.Add(new CenterItem(
                                Convert.ToInt32(dr["CenterID"]),
                                dr["CenterCode"].ToString(),
                                dr["CenterName"].ToString()));
                        }
                    }
                }

                // SuperAdmin گزینه "همه مراکز" هم دارد — بعد از ورود موفق اضافه می‌شود
                if (_cmbCenter.Items.Count > 0)
                    _cmbCenter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _lblMessage.Text = "خطا در بارگذاری مراکز: " + ex.Message;
            }
        }

        // ─── بازگرداندن آخرین مرکز کاربر ────────────────────────────────────
        private int GetLastCenterId(int userId)
        {
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT LastCenterID FROM TblUsers WHERE UserID = @UID", con))
                {
                    cmd.Parameters.AddWithValue("@UID", userId);
                    con.Open();
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value)
                        return Convert.ToInt32(val);
                }
            }
            catch { }
            return 0;
        }

        // ─── رفع باگ امنیتی: مرکز اختصاصی کاربر از سرور (نه از انتخاب کاربر
        // در ComboBox) خوانده می‌شود. کاربران غیر از SuperAdmin نباید بتوانند
        // با انتخاب گزینه‌ای دیگر در فرم ورود، وارد مرکز دیگری شوند —
        // اعتبارسنجی همیشه روی TblUsers.CenterID انجام می‌شود.
        private class AssignedCenter
        {
            public int    CenterId   = 0;
            public string CenterCode = "";
            public string CenterName = "";
        }

        private AssignedCenter GetAssignedCenter(int userId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT c.CenterID, c.CenterCode, c.CenterName
FROM   TblUsers u
JOIN   TblCenter c ON c.CenterID = u.CenterID
WHERE  u.UserID = @UID", con))
            {
                cmd.Parameters.AddWithValue("@UID", userId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                        return new AssignedCenter
                        {
                            CenterId   = Convert.ToInt32(dr["CenterID"]),
                            CenterCode = dr["CenterCode"].ToString(),
                            CenterName = dr["CenterName"].ToString()
                        };
                }
            }
            return null;
        }

        // ─── ورود به سیستم ────────────────────────────────────────────────────
        private void BtnLogin_Click(object sender, EventArgs e)
        {
            _lblMessage.ForeColor = UiTheme.Danger;
            _lblMessage.Text      = "";

            // اعتبارسنجی مرکز
            if (_cmbCenter.SelectedItem == null)
            {
                _lblMessage.Text = "ابتدا مرکز را انتخاب کنید.";
                return;
            }

            string username = _txtUsername.Text.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                _lblMessage.Text = "نام کاربری را وارد کنید.";
                return;
            }

            int    userId;
            string role;
            bool   mustChange;

            // آموزش — محدودیت تلاش ورود ناموفق (Lockout): بعد از چند رمز اشتباه
            // پیاپی، حساب برای مدتی قفل می‌شود تا حدس زدن رمز (Brute Force)
            // عملاً بی‌فایده شود. با هر ورود موفق شمارنده صفر می‌شود. مقادیر
            // از تب «امنیت» تنظیمات قابل تغییرند (پیش‌فرض همان ۵/۱۵ قبلی).
            int MaxFailedAttempts = SettingsHelper.GetInt(SettingsHelper.MaxFailedAttempts, 5);
            int LockoutMinutes = SettingsHelper.GetInt(SettingsHelper.LockoutMinutes, 15);

            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                {
                    con.Open();

                    int    dbUserId = 0;
                    byte[] passwordHash = null;
                    byte[] passwordSalt = null;
                    int    passwordIterations = 0;
                    int    failedCount = 0;
                    DateTime? lockoutUntil = null;

                    using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT UserID, Role, PasswordHash, PasswordSalt, PasswordIterations,
       MustChangePassword, FailedLoginCount, LockoutUntil,
       LastPasswordChangeAt, CreatedAt
FROM   TblUsers
WHERE  Username = @u AND IsActive = 1
LIMIT  1", con))
                    {
                        cmd.Parameters.AddWithValue("@u", username);

                        using (var dr = cmd.ExecuteReader())
                        {
                            if (!dr.Read())
                            {
                                _lblMessage.Text = "نام کاربری یا رمز عبور اشتباه است.";
                                return;
                            }

                            dbUserId = Convert.ToInt32(dr["UserID"]);
                            role = dr["Role"].ToString();
                            mustChange = Convert.ToInt32(dr["MustChangePassword"]) == 1;
                            passwordHash = (byte[])dr["PasswordHash"];
                            passwordSalt = (byte[])dr["PasswordSalt"];
                            passwordIterations = dr["PasswordIterations"] == DBNull.Value
                                ? 0 : Convert.ToInt32(dr["PasswordIterations"]);
                            failedCount = Convert.ToInt32(dr["FailedLoginCount"]);

                            // ─── اجبار تغییر دوره‌ای رمز (تب امنیت) ─────────────
                            // اگر مدت مشخصی گذشته و رمز عوض نشده، MustChangePassword
                            // اعمال می‌شود (۰ = خاموش = رفتار قبلی بدون تغییر).
                            int forceDays = SettingsHelper.GetInt(SettingsHelper.ForcePasswordChangeDays, 0);
                            if (!mustChange && forceDays > 0)
                            {
                                object refDateObj = dr["LastPasswordChangeAt"] != DBNull.Value
                                    ? dr["LastPasswordChangeAt"] : dr["CreatedAt"];
                                DateTime refDate;
                                if (DateTime.TryParse(Convert.ToString(refDateObj), out refDate) &&
                                    (DateTime.Now - refDate).TotalDays >= forceDays)
                                    mustChange = true;
                            }

                            if (dr["LockoutUntil"] != DBNull.Value)
                            {
                                DateTime parsed;
                                if (DateTime.TryParse(dr["LockoutUntil"].ToString(), out parsed))
                                    lockoutUntil = parsed;
                            }
                        }
                    }

                    // ─── بررسی قفل بودن حساب ─────────────────────────────────
                    if (lockoutUntil.HasValue && lockoutUntil.Value > DateTime.Now)
                    {
                        int minutesLeft = (int)Math.Ceiling((lockoutUntil.Value - DateTime.Now).TotalMinutes);
                        _lblMessage.Text = "حساب به‌دلیل تلاش‌های ناموفق پیاپی قفل شده. حدود " +
                            minutesLeft + " دقیقه دیگر دوباره امتحان کنید.";
                        return;
                    }

                    if (!PasswordHelper.Verify(_txtPassword.Text, passwordHash, passwordSalt, passwordIterations))
                    {
                        // ─── ثبت تلاش ناموفق + قفل در صورت رسیدن به سقف ──────
                        int newFailedCount = failedCount + 1;
                        bool shouldLock = newFailedCount >= MaxFailedAttempts;

                        using (SQLiteCommand updCmd = new SQLiteCommand(@"
UPDATE TblUsers
SET    FailedLoginCount = @fc,
       LockoutUntil = @lu
WHERE  UserID = @id", con))
                        {
                            updCmd.Parameters.AddWithValue("@fc", newFailedCount);
                            updCmd.Parameters.AddWithValue("@lu", shouldLock
                                ? (object)DateTime.Now.AddMinutes(LockoutMinutes).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                                : DBNull.Value);
                            updCmd.Parameters.AddWithValue("@id", dbUserId);
                            updCmd.ExecuteNonQuery();
                        }

                        _lblMessage.Text = shouldLock
                            ? "به‌دلیل تلاش‌های ناموفق پیاپی، حساب برای " + LockoutMinutes + " دقیقه قفل شد."
                            : "نام کاربری یا رمز عبور اشتباه است.";
                        return;
                    }

                    // ─── ورود موفق: شمارنده تلاش ناموفق صفر می‌شود ───────────
                    using (SQLiteCommand resetCmd = new SQLiteCommand(@"
UPDATE TblUsers
SET    FailedLoginCount = 0,
       LockoutUntil = NULL
WHERE  UserID = @id", con))
                    {
                        resetCmd.Parameters.AddWithValue("@id", dbUserId);
                        resetCmd.ExecuteNonQuery();
                    }

                    userId = dbUserId;
                }
            }
            catch (Exception ex)
            {
                _lblMessage.Text = "خطا در اتصال به دیتابیس: " + ex.Message;
                return;
            }

            // ─── اجبار تغییر رمز اولیه ────────────────────────────────────
            if (mustChange)
            {
                _lblMessage.ForeColor = UiTheme.Warning;
                _lblMessage.Text      = "برای ادامه باید رمز اولیه را تغییر دهید.";

                using (FrmChangePassword frm = new FrmChangePassword(username, forced: true))
                {
                    if (frm.ShowDialog(this) != DialogResult.OK)
                    {
                        _lblMessage.ForeColor = UiTheme.Danger;
                        _lblMessage.Text      = "برای ورود باید رمز اولیه تغییر کند.";
                        return;
                    }
                }
            }

            // ─── ورود موفق — SignIn ثبت‌نام در SecurityContext ─────────────
            SecurityContext.SignIn(userId, username, role);

            if (SecurityContext.IsSuperAdmin())
            {
                // اگر SuperAdmin است، گزینه "همه مراکز" به ComboBox اضافه می‌شود
                // و مرکز انتخابی خودِ کاربر در فرم ورود معتبر است.
                if (!(_cmbCenter.Items[0] is AllCentersItem))
                    _cmbCenter.Items.Insert(0, new AllCentersItem());

                int lastCenterId = GetLastCenterId(userId);
                if (lastCenterId > 0)
                {
                    for (int i = 0; i < _cmbCenter.Items.Count; i++)
                    {
                        CenterItem ci = _cmbCenter.Items[i] as CenterItem;
                        if (ci != null && ci.CenterId == lastCenterId)
                        {
                            _cmbCenter.SelectedIndex = i;
                            break;
                        }
                    }
                }

                ApplySelectedCenter();
            }
            else
            {
                // رفع باگ امنیتی: برای کاربر غیر از SuperAdmin، هرچه در
                // ComboBox انتخاب شده باشد نادیده گرفته می‌شود؛ مرکز فقط از
                // TblUsers.CenterID (تنظیم‌شده توسط مدیر) خوانده می‌شود.
                AssignedCenter assigned = GetAssignedCenter(userId);
                if (assigned == null)
                {
                    _lblMessage.ForeColor = UiTheme.Danger;
                    _lblMessage.Text = "مرکز کاربر تنظیم نشده است. با مدیر سیستم تماس بگیرید.";
                    return;
                }

                SecurityContext.SelectCenter(assigned.CenterId, assigned.CenterCode, assigned.CenterName);
            }

            AuditLogger.Log("ورود", "TblUsers", userId, "", username + " / " + SecurityContext.CenterDisplay);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ApplySelectedCenter()
        {
            if (_cmbCenter.SelectedItem is AllCentersItem)
            {
                SecurityContext.SelectCenter(0, "", "همه مراکز", allCenters: true);
                return;
            }

            CenterItem ci = _cmbCenter.SelectedItem as CenterItem;
            if (ci != null)
                SecurityContext.SelectCenter(ci.CenterId, ci.CenterCode, ci.CenterName);
        }

        // ─── تغییر ارادی رمز (از صفحه Login) ────────────────────────────────
        private void BtnChangePass_Click(object sender, EventArgs e)
        {
            string username = _txtUsername.Text.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                _lblMessage.ForeColor = UiTheme.Danger;
                _lblMessage.Text      = "ابتدا نام کاربری را وارد کنید.";
                return;
            }

            using (FrmChangePassword frm = new FrmChangePassword(username, forced: false))
            {
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    _lblMessage.ForeColor = UiTheme.Success;
                    _lblMessage.Text      = "رمز با موفقیت تغییر کرد.";
                }
            }
        }

        // ─── کلاس‌های کمکی برای آیتم‌های ComboBox مرکز ────────────────────────
        private class CenterItem
        {
            public int    CenterId   { get; }
            public string CenterCode { get; }
            public string CenterName { get; }

            public CenterItem(int id, string code, string name)
            {
                CenterId   = id;
                CenterCode = code;
                CenterName = name;
            }

            public override string ToString() { return CenterCode + " - " + CenterName; }
        }

        private class AllCentersItem
        {
            public override string ToString() { return "★  همه مراکز"; }
        }
    }
}

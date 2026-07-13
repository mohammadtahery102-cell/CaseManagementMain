using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement
{
    public class FrmUsers : Form
    {
        private readonly DatabaseHelper _db = new DatabaseHelper();

        private DataGridView _dgvUsers;
        private TextBox      _txtUsername;
        private TextBox      _txtPassword;
        private ComboBox     _cmbRole;
        private ComboBox     _cmbCenter;
        private TextBox      _txtPhone;
        private TextBox      _txtWhatsApp;

        public FrmUsers()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text              = "مدیریت کاربران";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeFixedSize(this, 880, 590);

            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 190, BackColor = UiTheme.CardBack };

            // ─── فیلدها: FlowLayoutPanel راست‌به‌چپ (برچسب بالا + ورودی پایین
            // هر فیلد) — به‌جای مختصات مطلق قبلی، خودش با اندازه فرم سازگار
            // می‌شود و ترتیب راست‌چین طبیعی دارد (مطابق الگوی FrmAdvancedSearch).
            FlowLayoutPanel fieldsFlow = new FlowLayoutPanel();
            fieldsFlow.Dock = DockStyle.Top;
            fieldsFlow.Height = 136;
            fieldsFlow.FlowDirection = FlowDirection.RightToLeft;
            fieldsFlow.WrapContents = true;
            fieldsFlow.Padding = new Padding(14, 10, 14, 4);

            _txtUsername = new TextBox();
            UiTheme.StyleTextBox(_txtUsername);
            fieldsFlow.Controls.Add(MakeFieldPanel("نام کاربری", _txtUsername));

            // آموزش — رمز موقت:
            // ادمین برای کاربر جدید یک رمز موقت می‌گذارد.
            // MustChangePassword=1 در INSERT یعنی کاربر در اولین ورود مجبور به تغییر آن است.
            _txtPassword = new TextBox();
            UiTheme.StyleTextBox(_txtPassword);
            fieldsFlow.Controls.Add(MakeFieldPanel("رمز موقت", _txtPassword));

            _cmbRole = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = UiTheme.Font(UiTheme.SizeBody)
            };
            // آموزش — نمایش فارسی/ذخیره انگلیسی: مقدار نمایشی نقش فارسی است،
            // اما مقدار ذخیره‌شده در دیتابیس و منطق SecurityContext.IsAdmin/...
            // همچنان انگلیسی می‌ماند (بدون تغییر منطق احراز هویت).
            _cmbRole.Items.AddRange(new object[]
            {
                new RoleItem("Admin", "مدیر سیستم"),
                new RoleItem("Operator", "کاربر عملیاتی"),
                new RoleItem("Viewer", "ناظر")
            });
            _cmbRole.SelectedIndex = 1;
            fieldsFlow.Controls.Add(MakeFieldPanel("نقش", _cmbRole));

            // ─── مرکز کاربر (رفع باگ امنیتی): هر کاربر باید به یک مرکز
            // مشخص محدود شود تا در صفحه ورود نتواند مرکز دیگری را انتخاب کند.
            _cmbCenter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = UiTheme.Font(UiTheme.SizeBody)
            };
            fieldsFlow.Controls.Add(MakeFieldPanel("مرکز", _cmbCenter));
            LoadCenters();

            _txtPhone = new TextBox();
            UiTheme.StyleTextBox(_txtPhone);
            fieldsFlow.Controls.Add(MakeFieldPanel("شماره تماس", _txtPhone));

            _txtWhatsApp = new TextBox();
            UiTheme.StyleTextBox(_txtWhatsApp);
            fieldsFlow.Controls.Add(MakeFieldPanel("شماره واتساپ", _txtWhatsApp));

            // ─── دکمه‌ها: نوار راست‌به‌چپ زیر فیلدها ─────────────────────────
            FlowLayoutPanel buttonFlow = new FlowLayoutPanel();
            buttonFlow.Dock = DockStyle.Top;
            buttonFlow.Height = 50;
            buttonFlow.FlowDirection = FlowDirection.RightToLeft;
            buttonFlow.Padding = new Padding(14, 6, 14, 6);

            Button btnAdd = UiTheme.CreateButton("افزودن کاربر", "+", UiTheme.Primary);
            btnAdd.Size = new Size(160, 34);
            btnAdd.Margin = new Padding(4);
            btnAdd.Click += BtnAdd_Click;
            buttonFlow.Controls.Add(btnAdd);

            Button btnToggle = UiTheme.CreateSecondaryButton("فعال / غیرفعال", "⊙");
            btnToggle.Size = new Size(160, 34);
            btnToggle.Margin = new Padding(4);
            btnToggle.Click += BtnToggle_Click;
            buttonFlow.Controls.Add(btnToggle);

            topPanel.Controls.Add(buttonFlow);
            topPanel.Controls.Add(fieldsFlow);

            _dgvUsers = new DataGridView
            {
                Dock                 = DockStyle.Fill,
                ReadOnly             = true,
                AllowUserToAddRows   = false,
                AllowUserToDeleteRows= false,
                AutoSizeColumnsMode  = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode        = DataGridViewSelectionMode.FullRowSelect
            };
            UiTheme.StyleGrid(_dgvUsers);

            Controls.Add(_dgvUsers);
            Controls.Add(topPanel);

            LoadUsers();
        }

        // یک فیلد فیلتر: برچسب بالا + کنترل پایین، با اندازه یکسان (الگوی FrmAdvancedSearch).
        private Panel MakeFieldPanel(string labelText, Control input)
        {
            Panel p = new Panel();
            p.Width = 170;
            p.Height = 58;
            p.Margin = new Padding(6, 4, 6, 4);

            Label lbl = new Label();
            lbl.Text = labelText;
            lbl.AutoSize = false;
            lbl.Dock = DockStyle.Top;
            lbl.Height = 22;
            lbl.TextAlign = ContentAlignment.MiddleRight;
            lbl.Font = UiTheme.FontBold(UiTheme.SizeSmall);
            lbl.ForeColor = UiTheme.TextDark;

            input.Dock = DockStyle.Top;
            input.Width = 164;
            input.Font = UiTheme.Font(UiTheme.SizeBody);
            if (input is TextBox tb)
                tb.Height = 28;

            p.Controls.Add(input);
            p.Controls.Add(lbl);
            return p;
        }

        // ─── بارگذاری فهرست مراکز برای ComboBox ─────────────────────────────
        // آموزش — رفع باگ امنیتی چندمرکزی: قبلاً همه مراکز به هر Admin نشان
        // داده می‌شد و یک Admin مرکز هرات می‌توانست کاربر جدید را به مرکز
        // دیگری (مثلاً کابل) متصل کند. حالا Admin غیر-SuperAdmin فقط مرکز
        // خودش را می‌بیند؛ فقط SuperAdmin همه مراکز را می‌بیند.
        private void LoadCenters()
        {
            _cmbCenter.Items.Clear();

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT CenterID, CenterCode, CenterName FROM TblCenter " +
                "WHERE IsActive = 1 AND (@CID = 0 OR CenterID = @CID) ORDER BY CenterCode", con))
            {
                cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                        _cmbCenter.Items.Add(new CenterItem(
                            Convert.ToInt32(dr["CenterID"]),
                            dr["CenterCode"] + " - " + dr["CenterName"]));
                }
            }

            if (_cmbCenter.Items.Count > 0)
                _cmbCenter.SelectedIndex = 0;
        }

        private class CenterItem
        {
            public int    CenterId { get; }
            public string Display  { get; }
            public CenterItem(int id, string display) { CenterId = id; Display = display; }
            public override string ToString() { return Display; }
        }

        // ─── افزودن کاربر جدید ───────────────────────────────────────────────
        private void BtnAdd_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.IsAdmin())
            {
                UiTheme.ShowWarning(this, "مدیریت کاربران فقط برای مدیر مجاز است.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtUsername.Text) ||
                string.IsNullOrWhiteSpace(_txtPassword.Text))
            {
                UiTheme.ShowWarning(this, "نام کاربری و رمز را وارد کنید.");
                return;
            }

            // آموزش — سیاست رمز عبور برای کاربر جدید (قابل تنظیم از تب امنیت):
            int minPasswordLength = SettingsHelper.GetInt(SettingsHelper.MinPasswordLength, 6);
            if (_txtPassword.Text.Length < minPasswordLength)
            {
                UiTheme.ShowWarning(this, "رمز باید حداقل " + minPasswordLength + " کاراکتر باشد.");
                return;
            }

            CenterItem selectedCenter = _cmbCenter.SelectedItem as CenterItem;
            if (selectedCenter == null)
            {
                UiTheme.ShowWarning(this, "مرکز کاربر را انتخاب کنید.");
                return;
            }

            // آموزش — لایه دوم دفاعی: حتی اگر ComboBox به هر دلیلی مرکز دیگری
            // نشان دهد، Admin غیر-SuperAdmin هرگز نمی‌تواند کاربر را به مرکزی
            // غیر از مرکز فعال خودش متصل کند؛ فقط SuperAdmin مقدار ComboBox را
            // آزادانه انتخاب می‌کند.
            int centerIdToUse = SecurityContext.IsSuperAdmin()
                ? selectedCenter.CenterId
                : SecurityContext.CurrentCenterId;

            if (!SecurityContext.IsSuperAdmin() && !SecurityContext.CanAccessCenter(selectedCenter.CenterId))
            {
                UiTheme.ShowWarning(this, "شما فقط می‌توانید کاربر مرکز خودتان را ثبت کنید.");
                return;
            }

            byte[] hash;
            byte[] salt;
            int iterations;
            PasswordHelper.CreateHash(_txtPassword.Text, out hash, out salt, out iterations);

            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO TblUsers
    (Username, PasswordHash, PasswordSalt, PasswordIterations, Role, IsActive, MustChangePassword, Phone, WhatsApp, CenterID, LastCenterID, LastPasswordChangeAt)
VALUES
    (@Username, @PasswordHash, @PasswordSalt, @PasswordIterations, @Role, 1, 1, @Phone, @WhatsApp, @CenterID, @CenterID, datetime('now'))", con))
                {
                    // آموزش — MustChangePassword=1:
                    // رمزی که ادمین می‌گذارد موقت است.
                    // کاربر در اولین ورود مجبور به تغییر آن می‌شود.
                    cmd.Parameters.AddWithValue("@Username",     _txtUsername.Text.Trim());
                    cmd.Parameters.AddWithValue("@PasswordHash", hash);
                    cmd.Parameters.AddWithValue("@PasswordSalt", salt);
                    cmd.Parameters.AddWithValue("@PasswordIterations", iterations);
                    string roleValue = ((RoleItem)_cmbRole.SelectedItem).Value;
                    cmd.Parameters.AddWithValue("@Role",         roleValue);
                    cmd.Parameters.AddWithValue("@Phone",        _txtPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@WhatsApp",     _txtWhatsApp.Text.Trim());
                    cmd.Parameters.AddWithValue("@CenterID",     centerIdToUse);

                    con.Open();
                    cmd.ExecuteNonQuery();
                }

                AuditLogger.Log(
                    "ثبت کاربر", "TblUsers", 0, "",
                    _txtUsername.Text.Trim() + "/" + ((RoleItem)_cmbRole.SelectedItem).Value);

                _txtUsername.Text = "";
                _txtPhone.Text = "";
                _txtWhatsApp.Text = "";
                LoadUsers();
                UiTheme.ShowSuccess(this, "کاربر جدید با موفقیت ثبت شد.");
            }
            catch (SQLiteException ex)
            {
                // آموزش — تشخیص UNIQUE violation در SQLite:
                // SqlServer از ex.Number استفاده می‌کرد.
                // SQLite پیام خطا دارد که کلمه "UNIQUE" را شامل می‌شود.
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                    UiTheme.ShowError(this, "نام کاربری تکراری است.");
                else
                    UiTheme.ShowError(this, "خطا: " + ex.Message);
            }
        }

        // ─── فعال/غیرفعال کردن کاربر ─────────────────────────────────────────
        private void BtnToggle_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.IsAdmin())
            {
                UiTheme.ShowWarning(this, "مدیریت کاربران فقط برای مدیر مجاز است.");
                return;
            }

            if (_dgvUsers.CurrentRow == null || !_dgvUsers.Columns.Contains("UserID"))
                return;

            int userId = Convert.ToInt32(_dgvUsers.CurrentRow.Cells["UserID"].Value);

            if (userId == SecurityContext.UserId)
            {
                UiTheme.ShowWarning(this, "کاربر جاری را نمی‌توان غیرفعال کرد.");
                return;
            }

            // آموزش — لایه دفاعی دیتا: حتی اگر گرید به هر دلیلی ردیف مرکز
            // دیگری را نشان دهد، اینجا و در WHERE کوئری زیر هر دو مالکیت مرکز
            // کاربر هدف تأیید می‌شود؛ Admin غیر-SuperAdmin نمی‌تواند کاربر
            // مرکز دیگر را فعال/غیرفعال کند.
            try
            {
                CenterGuard.EnsureUserAccess(_db, userId);
            }
            catch (CenterAccessDeniedException ex)
            {
                UiTheme.ShowWarning(this, ex.Message);
                return;
            }

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
UPDATE TblUsers
SET    IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END
WHERE  UserID = @UserID AND (@CID = 0 OR CenterID = @CID)", con))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);
                con.Open();
                int affected = cmd.ExecuteNonQuery();

                if (affected == 0)
                {
                    UiTheme.ShowWarning(this, "این کاربر متعلق به مرکز دیگری است.");
                    return;
                }
            }

            AuditLogger.Log("تغییر وضعیت کاربر", "TblUsers", userId, "", "");
            LoadUsers();
        }

        // ─── بارگذاری لیست کاربران ───────────────────────────────────────────
        // آموزش — رفع باگ امنیتی چندمرکزی: قبلاً همه کاربران همه مراکز به هر
        // Admin نشان داده می‌شد. حالا Admin غیر-SuperAdmin فقط کاربران مرکز
        // خودش را می‌بیند؛ SuperAdmin (یا SuperAdmin در حالت «همه مراکز») همه را می‌بیند.
        private void LoadUsers()
        {
            // آموزش — SQLiteDataAdapter به جای SqlDataAdapter:
            // هر دو یک DataTable را پر می‌کنند ولی از Provider متفاوت استفاده می‌کنند.
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT u.UserID, u.Username,
       CASE u.Role
           WHEN 'SuperAdmin' THEN 'مدیر کل'
           WHEN 'Admin'      THEN 'مدیر سیستم'
           WHEN 'Operator'   THEN 'کاربر عملیاتی'
           WHEN 'Viewer'     THEN 'ناظر'
           ELSE u.Role
       END AS Role,
       COALESCE(c.CenterCode || ' - ' || c.CenterName, 'همه مراکز') AS [مرکز],
       u.Phone AS [شماره تماس], u.WhatsApp AS [واتساپ], u.IsActive, u.MustChangePassword, u.CreatedAt
FROM   TblUsers u
LEFT JOIN TblCenter c ON c.CenterID = u.CenterID
WHERE  (@CID = 0 OR u.CenterID = @CID)
ORDER  BY u.UserID", con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);
                DataTable table = new DataTable();
                da.Fill(table);
                _dgvUsers.DataSource = table;
            }
        }

        // آیتم کمبوی نقش: مقدار ذخیره‌شده (انگلیسی) + متن نمایشی (فارسی)
        private class RoleItem
        {
            public string Value { get; }
            public string Display { get; }
            public RoleItem(string value, string display) { Value = value; Display = display; }
            public override string ToString() { return Display; }
        }

        private static Label MakeLabel(string text, int x, int y, int width)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.AutoSize = false;
            lbl.Font = UiTheme.FontBold(UiTheme.SizeSmall);
            lbl.ForeColor = UiTheme.TextDark;
            lbl.TextAlign = ContentAlignment.MiddleRight;
            lbl.SetBounds(x, y, width, 20);
            return lbl;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement
{
    // ─────────────────────────────────────────────────────────────────────────
    // ابزار مدیریتی «تعیین نقش اعضا» — به‌درخواست کاربر، پاسخ به این که وقتی
    // ستون MemberRole اضافه شد، رکوردهای قدیمی همه خالی ماندند و کارت
    // شناسایی/گزارش‌ها «۰ نفر ایتام» نشان می‌دادند. این فرم:
    //   ۱) گزارش بازبینی: اعضایی که MemberRole هنوز تعیین نشده.
    //   ۲) ابزار تخصیص دسته‌ای: کاربر مجاز می‌تواند نقش چند عضو را یک‌جا
    //      تعیین/اصلاح کند.
    //   ۳) ستون «پیشنهاد»: فقط برای سه حالتِ صریحِ خواسته‌شده (فرزند/پدر/مادر)
    //      از روی Relation پیشنهاد می‌دهد — هیچ نقشی حدس زده/خودکار ذخیره
    //      نمی‌شود؛ کاربر باید هر ردیف را تأیید کند (دکمه «اعمال تغییرات»).
    //   ۴) هر تغییرِ MemberRole در جدولِ اختصاصیِ TblFamilyRoleHistory ثبت
    //      می‌شود (عمداً جدا از TblFamilyStatusHistory که فقط برای وضعیتِ
    //      خدمات است) — نقشِ قبلی/جدید/کاربر/تاریخ، دقیقاً طبق درخواست.
    // دسترسی: فقط مدیر (SecurityContext.IsAdmin) — بررسی در نقطهٔ فراخوانی
    // (FrmDashboard)، دقیقاً مثل OpenUsers.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmAssignMemberRole : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        private static readonly string[] Provinces =
        {
            "کابل", "هرات", "بلخ", "قندهار", "ننگرهار", "بدخشان", "بغلان", "تخار",
            "غزنی", "هلمند", "لغمان", "کندز", "فاریاب", "جوزجان", "سمنگان", "بامیان",
            "پکتیا", "لوگر", "وردک", "غور", "فراه", "خوست", "کاپیسا", "پروان",
            "زابل", "ارزگان", "نیمروز", "نورستان", "کنر", "سرپل", "دایکندی",
            "پکتیکا", "بادغیس", "پنجشیر"
        };

        // نگاشتِ صریحِ Relation → پیشنهادِ MemberRole (دقیقاً طبق مثال کاربر).
        // عمداً محدود به همین سه مورد — حدس‌زدنِ بقیه (برادر/خواهر/همسر/نوه)
        // چون معلوم نیست خودشان یتیم هستند یا نه، انجام نمی‌شود؛ ستون پیشنهاد
        // برای آن‌ها خالی می‌ماند تا کاربر دستی تصمیم بگیرد.
        private static readonly Dictionary<string, string> RelationToSuggestedRole =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "فرزند", "فرزند" },
                { "پدر", "پدر" },
                { "مادر", "مادر" }
            };

        private ComboBox _cmbProvince;
        private TextBox _txtDistrict;
        private ComboBox _cmbCaseType;
        private CheckBox _chkOnlyEmpty;
        private Label _lblStatus;
        private DataGridView _grid;

        private const int ColFamId = 0;
        private const int ColCaseCode = 1;
        private const int ColMemberName = 2;
        private const int ColFatherName = 3;
        private const int ColRelation = 4;
        private const int ColGender = 5;
        private const int ColAge = 6;
        private const int ColCurrentRole = 7;
        private const int ColSuggestedRole = 8;
        private const int ColNewRole = 9;
        private const int ColOrigRelation = 10; // مخفی — برای تشخیص تغییر واقعی
        private const int ColOrigRole = 11;      // مخفی — برای تشخیص تغییر واقعی

        public FrmAssignMemberRole()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "تعیین نقش اعضای خانواده (بازبینی/تخصیص دسته‌ای)";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1280, 820);
            MinimumSize = new Size(900, 560);

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = UiTheme.PrimaryDark };

            Label lblProvince = new Label
            {
                Text = "ولایت", ForeColor = Color.White, AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.Font(9.5F)
            };
            lblProvince.SetBounds(14, 12, 60, 26);

            _cmbProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbProvince.Items.Add("همه ولایات");
            _cmbProvince.Items.AddRange(Provinces);
            _cmbProvince.SelectedIndex = 0;
            _cmbProvince.SetBounds(76, 10, 150, 26);

            Label lblDistrict = new Label
            {
                Text = "ولسوالی", ForeColor = Color.White, AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.Font(9.5F)
            };
            lblDistrict.SetBounds(236, 12, 60, 26);

            _txtDistrict = new TextBox { RightToLeft = RightToLeft.Yes };
            _txtDistrict.SetBounds(298, 10, 140, 26);

            Label lblCaseType = new Label
            {
                Text = "نوع پرونده", ForeColor = Color.White, AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.Font(9.5F)
            };
            lblCaseType.SetBounds(448, 12, 68, 26);

            _cmbCaseType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCaseType.Items.Add("همه انواع");
            foreach (string v in LookupHelper.GetValues("RequestType")) _cmbCaseType.Items.Add(v);
            _cmbCaseType.SelectedIndex = 0;
            _cmbCaseType.SetBounds(520, 10, 150, 26);

            _chkOnlyEmpty = new CheckBox
            {
                Text = "فقط نقش تعیین‌نشده", ForeColor = Color.White, AutoSize = true,
                Font = UiTheme.Font(9.5F), Checked = true
            };
            _chkOnlyEmpty.SetBounds(680, 12, 170, 26);

            var btnLoad = UiTheme.CreateButton("بارگذاری", "⌕", UiTheme.Primary);
            btnLoad.SetBounds(14, 48, 120, 34);
            btnLoad.Click += delegate { LoadCandidates(); };

            var btnApply = UiTheme.CreateButton("اعمال تغییرات", "✓", UiTheme.Success);
            btnApply.SetBounds(142, 48, 140, 34);
            btnApply.Click += delegate { ApplyChanges(); };

            _lblStatus = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White, Font = UiTheme.Font(9.5F)
            };
            _lblStatus.SetBounds(292, 48, 800, 34);

            toolbar.Controls.Add(lblProvince);
            toolbar.Controls.Add(_cmbProvince);
            toolbar.Controls.Add(lblDistrict);
            toolbar.Controls.Add(_txtDistrict);
            toolbar.Controls.Add(lblCaseType);
            toolbar.Controls.Add(_cmbCaseType);
            toolbar.Controls.Add(_chkOnlyEmpty);
            toolbar.Controls.Add(btnLoad);
            toolbar.Controls.Add(btnApply);
            toolbar.Controls.Add(_lblStatus);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            UiTheme.StyleGrid(_grid);
            BuildGridColumns();
            _grid.CellValueChanged += Grid_CellValueChanged;
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            Controls.Add(_grid);
            Controls.Add(toolbar);
        }

        private void BuildGridColumns()
        {
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FamID", HeaderText = "شناسه", Visible = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CaseCode", HeaderText = "کد پرونده", ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MemberName", HeaderText = "نام عضو", ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FatherName", HeaderText = "نام پدر", ReadOnly = true });

            var colRelation = new DataGridViewComboBoxColumn
            {
                Name = "Relation", HeaderText = "نسبت خانوادگی", FlatStyle = FlatStyle.Flat
            };
            colRelation.Items.Add("");
            foreach (string v in LookupHelper.GetValues("FamilyRelation")) colRelation.Items.Add(v);
            _grid.Columns.Add(colRelation);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Gender", HeaderText = "جنسیت", ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Age", HeaderText = "سن", ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentRole", HeaderText = "نقش فعلی", ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SuggestedRole", HeaderText = "نقش پیشنهادی", ReadOnly = true });

            var colNewRole = new DataGridViewComboBoxColumn
            {
                Name = "NewRole", HeaderText = "نقش جدید (برای ذخیره)", FlatStyle = FlatStyle.Flat
            };
            colNewRole.Items.Add("");
            foreach (string v in LookupHelper.GetValues("MemberRole")) colNewRole.Items.Add(v);
            _grid.Columns.Add(colNewRole);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "OrigRelation", Visible = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "OrigRole", Visible = false });

            _grid.Columns["SuggestedRole"].DefaultCellStyle.ForeColor = UiTheme.TextMuted;
        }

        private void LoadCandidates()
        {
            _grid.Rows.Clear();
            SetStatus("در حال بارگذاری...");

            try
            {
                string province = _cmbProvince.SelectedIndex <= 0 ? "" : _cmbProvince.Text;
                string district = _txtDistrict.Text.Trim();
                string caseType = _cmbCaseType.SelectedIndex <= 0 ? "" : _cmbCaseType.Text;
                bool onlyEmpty = _chkOnlyEmpty.Checked;
                int cid = SecurityContext.CenterFilterId;

                const int maxRows = 500;

                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT f.FamID, c.Code AS CaseCode, f.MemberName, f.MemberFatherName, f.Relation, f.Gender, f.MemberRole,
       CASE WHEN f.BirthDate IS NULL THEN NULL
            ELSE CAST((julianday('now') - julianday(f.BirthDate)) / 365.25 AS INTEGER) END AS Age
FROM TblFamily f
JOIN TblCase c ON c.CasID = f.CasID
WHERE c.IsArchived = 0
  AND (@CID = 0 OR c.CenterID = @CID)
  AND (@OnlyEmpty = 0 OR f.MemberRole IS NULL OR f.MemberRole = '')
  AND (@Province = '' OR c.Province = @Province)
  AND (@District = '' OR c.District LIKE @District)
  AND (@CaseType = '' OR c.RequestType = @CaseType)
ORDER BY c.Code, f.FamID
LIMIT @MaxRows", con))
                {
                    cmd.Parameters.AddWithValue("@CID", cid);
                    cmd.Parameters.AddWithValue("@OnlyEmpty", onlyEmpty ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Province", province);
                    cmd.Parameters.AddWithValue("@District", "%" + district + "%");
                    cmd.Parameters.AddWithValue("@CaseType", caseType);
                    cmd.Parameters.AddWithValue("@MaxRows", maxRows + 1);

                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        int count = 0;
                        while (dr.Read())
                        {
                            count++;
                            if (count > maxRows) break;

                            string relation = dr["Relation"] == DBNull.Value ? "" : dr["Relation"].ToString();
                            string currentRole = dr["MemberRole"] == DBNull.Value ? "" : dr["MemberRole"].ToString();
                            string age = dr["Age"] == DBNull.Value ? "" : dr["Age"].ToString();
                            string suggested = SuggestRole(relation);

                            int rowIndex = _grid.Rows.Add(
                                dr["FamID"], dr["CaseCode"], dr["MemberName"], dr["MemberFatherName"],
                                relation, dr["Gender"], age, currentRole, suggested,
                                // نقش جدید: اگر نقش فعلی خالی باشد و پیشنهادی داشته باشیم، از پیشنهاد
                                // پیش‌پر می‌شود — ولی هنوز ذخیره نشده؛ کاربر باید «اعمال تغییرات» بزند.
                                string.IsNullOrEmpty(currentRole) ? suggested : currentRole,
                                relation, currentRole);
                        }

                        if (count > maxRows)
                            SetStatus(maxRows + " ردیف نمایش داده شد (نتایج بیشتری هست — فیلتر را محدودتر کنید)");
                        else
                            SetStatus(count + " عضو یافت شد");
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus("");
                Msg.Show("خطا در بارگذاری فهرست: " + ex.Message);
            }
        }

        // پیشنهاد را فقط برای سه نگاشتِ صریح می‌دهد؛ در غیر این صورت رشته خالی
        // (یعنی «بدون پیشنهاد» — کاربر باید خودش انتخاب کند).
        private static string SuggestRole(string relation)
        {
            string key = (relation ?? "").Trim();
            return RelationToSuggestedRole.TryGetValue(key, out string suggested) ? suggested : "";
        }

        private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex != ColRelation) return;

            DataGridViewRow row = _grid.Rows[e.RowIndex];
            string relation = Convert.ToString(row.Cells[ColRelation].Value ?? "");
            string suggested = SuggestRole(relation);
            row.Cells[ColSuggestedRole].Value = suggested;

            // فقط اگر «نقش جدید» هنوز خالی است، پیشنهاد را پیش‌پر می‌کند —
            // هرگز یک انتخاب دستیِ قبلیِ کاربر را رونویسی نمی‌کند.
            string newRole = Convert.ToString(row.Cells[ColNewRole].Value ?? "");
            if (string.IsNullOrEmpty(newRole) && !string.IsNullOrEmpty(suggested))
                row.Cells[ColNewRole].Value = suggested;
        }

        private void ApplyChanges()
        {
            if (!SecurityContext.CanEdit())
            {
                Msg.Show("کاربر فقط مشاهده اجازه ثبت تغییرات ندارد.");
                return;
            }

            var pending = new List<(int FamId, string OrigRole, string NewRole, string OrigRelation, string NewRelation, string Label)>();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;

                int famId = Convert.ToInt32(row.Cells[ColFamId].Value);
                string origRole = Convert.ToString(row.Cells[ColOrigRole].Value ?? "");
                string newRole = Convert.ToString(row.Cells[ColNewRole].Value ?? "").Trim();
                string origRelation = Convert.ToString(row.Cells[ColOrigRelation].Value ?? "");
                string newRelation = Convert.ToString(row.Cells[ColRelation].Value ?? "").Trim();

                bool roleChanged = newRole != origRole && newRole.Length > 0;
                bool relationChanged = newRelation != origRelation;

                if (roleChanged || relationChanged)
                {
                    string label = Convert.ToString(row.Cells[ColCaseCode].Value) + " — " +
                                   Convert.ToString(row.Cells[ColMemberName].Value);
                    pending.Add((famId, origRole, roleChanged ? newRole : origRole, origRelation, newRelation, label));
                }
            }

            if (pending.Count == 0)
            {
                Msg.Show("هیچ تغییری برای اعمال وجود ندارد.");
                return;
            }

            DialogResult confirm = MessageBox.Show(
                pending.Count + " تغییر (نقش/نسبت) روی اعضای خانواده اعمال می‌شود. ادامه می‌دهید؟",
                "تأیید اعمال تغییرات", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

            if (confirm != DialogResult.Yes) return;

            int applied = 0;
            int failed = 0;

            foreach (var change in pending)
            {
                try
                {
                    using (SQLiteConnection con = db.GetConnection())
                    using (SQLiteCommand cmd = new SQLiteCommand(
                        "UPDATE TblFamily SET MemberRole = @Role, Relation = @Relation WHERE FamID = @FamID", con))
                    {
                        cmd.Parameters.AddWithValue("@Role", string.IsNullOrEmpty(change.NewRole) ? (object)DBNull.Value : change.NewRole);
                        cmd.Parameters.AddWithValue("@Relation", string.IsNullOrEmpty(change.NewRelation) ? (object)DBNull.Value : change.NewRelation);
                        cmd.Parameters.AddWithValue("@FamID", change.FamId);
                        con.Open();
                        cmd.ExecuteNonQuery();
                    }

                    if (change.NewRole != change.OrigRole)
                        AuditLogger.RecordFamilyRoleChange(change.FamId, change.OrigRole, change.NewRole);

                    applied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Debug_LogFailure(change.Label, ex);
                }
            }

            SetStatus(applied + " مورد ذخیره شد" + (failed > 0 ? " — " + failed + " مورد با خطا" : ""));
            UiTheme.ShowSuccess(this, applied + " تغییر با موفقیت ذخیره شد." +
                (failed > 0 ? "\n" + failed + " مورد ذخیره نشد (جزئیات در audit_errors.log)." : ""));

            LoadCandidates();
        }

        private static void Debug_LogFailure(string label, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[FrmAssignMemberRole] " + label + " → " + ex.Message);
        }

        private void SetStatus(string text)
        {
            if (_lblStatus.InvokeRequired) { _lblStatus.Invoke((Action)(() => _lblStatus.Text = text)); return; }
            _lblStatus.Text = text;
        }
    }
}

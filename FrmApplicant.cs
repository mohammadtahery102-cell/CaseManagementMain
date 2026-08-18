using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CaseManagement
{
    // آموزش — فرم «متقاضی/درخواستی» (به درخواست کاربر): یک یادداشت اولیه از
    // شخص متقاضی که هنوز پرونده کامل نشده. می‌توان آن را ذخیره کرد، چاپ گرفت،
    // یا با یک کلیک به «پرونده» تبدیل کرد (فرم پرونده با اطلاعات اولیه باز
    // می‌شود). داده در TblApplicant با فیلتر مرکز ذخیره می‌شود.
    public class FrmApplicant : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        private DataGridView _grid;
        private TextBox _txtFullName, _txtFatherName, _txtPhone, _txtDistrict, _txtNote;
        private TextBox _txtTazkira;
        private ComboBox _cmbProvince, _cmbRequestType, _cmbStatus;
        private int _currentId;

        // ─── سند پیوستِ متقاضی (درخواست کاربر) ───────────────────────────────
        // فقط یک سند، با نوعِ ثابت. حجم باید بین ۵۰ تا ۳۰۰ کیلوبایت باشد تا
        // اسکنِ بی‌کیفیت یا فایلِ سنگین وارد سیستم نشود.
        public const string ApplicantDocType = "چاپ و امضاء و تایید همین فرم درخواستی";
        public const int MinDocBytes = 50 * 1024;
        public const int MaxDocBytes = 300 * 1024;

        // مسیرِ فایلِ انتخاب‌شده‌ی کاربر (مبدأ). هنگام ذخیره از او پرسیده می‌شود
        // که سند کجا نگهداری شود و همان‌جا کپی می‌گردد.
        private string _pendingDocSourcePath = "";
        // مسیرِ نهاییِ ذخیره‌شده در دیتابیس (مقصد).
        private string _savedDocPath = "";
        private TextBox _txtDocPath;

        // اعتبارسنجی حجم — جدا از UI تا قابل آزمون باشد.
        public static bool IsAcceptableDocSize(long bytes)
        {
            return bytes >= MinDocBytes && bytes <= MaxDocBytes;
        }

        public static string DescribeDocSizeLimit()
        {
            return "حجم سند باید بین " + (MinDocBytes / 1024) + " تا " + (MaxDocBytes / 1024) + " کیلوبایت باشد.";
        }

        private static readonly string[] Provinces =
        {
            "کابل", "هرات", "بلخ", "قندهار", "ننگرهار", "بدخشان", "بغلان", "تخار",
            "غزنی", "هلمند", "لغمان", "کندز", "فاریاب", "جوزجان", "سمنگان", "بامیان",
            "پکتیا", "لوگر", "وردک", "غور", "فراه", "خوست", "کاپیسا", "پروان",
            "زابل", "ارزگان", "نیمروز", "نورستان", "کنر", "سرپل", "دایکندی",
            "پکتیکا", "بادغیس", "پنجشیر"
        };

        public FrmApplicant()
        {
            BuildUi();
            LoadApplicants();
            ClearForm();
        }

        private void BuildUi()
        {
            Text = "متقاضیان (فرم درخواستی)  —  " + SecurityContext.CenterDisplay;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1080, 620);

            // ─── فرم ورودی (سمت راست) ────────────────────────────────────────
            Panel form = new Panel { Dock = DockStyle.Right, Width = 420, BackColor = UiTheme.CardBack, Padding = new Padding(14) };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true
            };

            _txtFullName = new TextBox();
            _txtFatherName = new TextBox();
            _txtPhone = new TextBox();
            _txtDistrict = new TextBox();
            _cmbProvince = MakeCombo(Provinces);
            // منبع واحد: به‌جای آرایه‌ی هاردکد، از TblLookup (دسته RequestType/نوع پرونده) خوانده می‌شود.
            _cmbRequestType = MakeCombo(Helpers.LookupHelper.GetValues("RequestType").ToArray());
            _cmbStatus = MakeCombo(new[] { "در انتظار", "در حال بررسی", "تأیید اولیه", "رد شده" });

            _txtNote = new TextBox { Multiline = true, RightToLeft = RightToLeft.Yes, ScrollBars = ScrollBars.Vertical };
            _txtNote.TextAlign = HorizontalAlignment.Right;

            _txtTazkira = new TextBox();

            flow.Controls.Add(MakeField("نام و تخلص متقاضی", _txtFullName));
            flow.Controls.Add(MakeField("نام پدر", _txtFatherName));
            flow.Controls.Add(MakeField("شماره تذکره", _txtTazkira));
            flow.Controls.Add(MakeField("شماره تماس", _txtPhone));
            flow.Controls.Add(MakeField("ولایت", _cmbProvince));
            flow.Controls.Add(MakeField("ولسوالی", _txtDistrict));
            flow.Controls.Add(MakeField("نوع درخواست", _cmbRequestType));
            flow.Controls.Add(MakeField("وضعیت", _cmbStatus));
            flow.Controls.Add(MakeNoteField("یادداشت / شرح درخواست", _txtNote));
            flow.Controls.Add(MakeDocumentField());

            form.Controls.Add(flow);

            // ─── نوار دکمه‌ها (پایین فرم) ────────────────────────────────────
            Panel buttonBar = new Panel { Dock = DockStyle.Bottom, Height = 100, BackColor = UiTheme.CardBack, Padding = new Padding(8) };
            FlowLayoutPanel btns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = true };

            Button btnNew = UiTheme.CreateButton("جدید", "＋", UiTheme.Primary); btnNew.Size = new Size(96, 38); btnNew.Margin = new Padding(4);
            Button btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.Size = new Size(96, 38); btnSave.Margin = new Padding(4);
            Button btnDelete = UiTheme.CreateButton("حذف", "✕", UiTheme.Danger); btnDelete.Size = new Size(96, 38); btnDelete.Margin = new Padding(4);
            Button btnPrint = UiTheme.CreateSecondaryButton("چاپ", "🖨"); btnPrint.Size = new Size(96, 38); btnPrint.Margin = new Padding(4);
            Button btnConvert = UiTheme.CreateButton("تبدیل به پرونده", "➜", UiTheme.PrimaryDark); btnConvert.Size = new Size(150, 38); btnConvert.Margin = new Padding(4);

            btnNew.Click += delegate { ClearForm(); };
            btnSave.Click += delegate { SaveApplicant(); };
            btnDelete.Click += delegate { DeleteApplicant(); };
            btnPrint.Click += delegate { PrintApplicant(); };
            btnConvert.Click += delegate { ConvertToCase(); };

            // میان‌بُرها همین‌جا بسته می‌شوند، نه در سازنده: این دکمه‌ها متغیرِ
            // محلی‌اند و بیرون از این متد در دسترس نیستند.
            Helpers.FormShortcuts.For(this)
                .Save(btnSave)
                .New(btnNew)
                .Delete(btnDelete)
                .Print(btnPrint)
                .Bind(Keys.Control | Keys.T, "تبدیل به پرونده", btnConvert);

            btns.Controls.Add(btnNew);
            btns.Controls.Add(btnSave);
            btns.Controls.Add(btnDelete);
            btns.Controls.Add(btnPrint);
            btns.Controls.Add(btnConvert);
            buttonBar.Controls.Add(btns);
            form.Controls.Add(buttonBar);

            // ─── گرید متقاضیان (سمت چپ/پرکننده) ─────────────────────────────
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            UiTheme.StyleGrid(_grid);
            _grid.CellClick += Grid_CellClick;

            Panel gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            gridWrap.Controls.Add(_grid);

            Controls.Add(gridWrap);
            Controls.Add(form);
        }

        private ComboBox MakeCombo(string[] items)
        {
            ComboBox c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.Add("");
            c.Items.AddRange(items);
            c.SelectedIndex = 0;
            return c;
        }

        private Panel MakeField(string label, Control input)
        {
            Panel p = new Panel { Width = 185, Height = 58, Margin = new Padding(4, 2, 4, 2) };
            Label l = new Label { Text = label, AutoSize = false, Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark };
            input.Dock = DockStyle.Top;
            input.Width = 179;
            TextBox tb = input as TextBox;
            if (tb != null) { tb.Height = 28; UiTheme.StyleTextBox(tb); }
            p.Controls.Add(input);
            p.Controls.Add(l);
            return p;
        }

        // ─── بخش سند پیوست ───────────────────────────────────────────────────
        // نوع سند ثابت است (قابل تغییر توسط کاربر نیست)، پس به‌جای کمبو فقط
        // نمایش داده می‌شود. مسیرِ ذخیره هنگام «ذخیره» پرسیده می‌شود، نه اینجا.
        private Panel MakeDocumentField()
        {
            Panel p = new Panel { Width = 378, Height = 104, Margin = new Padding(4, 6, 4, 2) };

            p.Controls.Add(new Label
            {
                Text = "سند: " + ApplicantDocType + " — " + DescribeDocSizeLimit(),
                AutoSize = false, Dock = DockStyle.Top, Height = 34,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted
            });

            Panel row = new Panel { Dock = DockStyle.Top, Height = 30 };

            Button btnBrowse = UiTheme.CreateSecondaryButton("انتخاب سند", "▤");
            btnBrowse.Dock = DockStyle.Left;
            btnBrowse.Width = 110;
            btnBrowse.Click += delegate { BrowseDocument(); };

            Button btnClear = UiTheme.CreateSecondaryButton("حذف", "✕");
            btnClear.Dock = DockStyle.Right;
            btnClear.Width = 70;
            btnClear.Click += delegate
            {
                _pendingDocSourcePath = "";
                _savedDocPath = "";
                _txtDocPath.Text = "";
            };

            _txtDocPath = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
            UiTheme.StyleTextBox(_txtDocPath);

            row.Controls.Add(_txtDocPath);
            row.Controls.Add(btnClear);
            row.Controls.Add(btnBrowse);

            p.Controls.Add(row);

            p.Controls.Add(new Label
            {
                Text = "سند پیوست", AutoSize = false, Dock = DockStyle.Top, Height = 22,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark
            });

            return p;
        }

        private void BrowseDocument()
        {
            using (OpenFileDialog ofd = new OpenFileDialog
            {
                Title = ApplicantDocType,
                Filter = "سند اسکن‌شده|*.pdf;*.jpg;*.jpeg;*.png|همه فایل‌ها|*.*",
                CheckFileExists = true
            })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK)
                    return;

                long size;
                try
                {
                    size = new System.IO.FileInfo(ofd.FileName).Length;
                }
                catch (Exception ex)
                {
                    UiTheme.ShowError(this, "خواندن فایل ممکن نشد: " + ex.Message);
                    return;
                }

                if (!IsAcceptableDocSize(size))
                {
                    UiTheme.ShowWarning(this,
                        DescribeDocSizeLimit() + Environment.NewLine +
                        "حجم فایل انتخاب‌شده: " + Math.Round(size / 1024d) + " کیلوبایت.");
                    return;
                }

                _pendingDocSourcePath = ofd.FileName;
                _txtDocPath.Text = ofd.FileName;
            }
        }

        // هنگام ذخیره: از کاربر پرسیده می‌شود سند کجا نگهداری شود، بعد کپی
        // می‌گردد. اگر انصراف بدهد، ذخیره‌ی متقاضی ادامه پیدا می‌کند ولی سند
        // پیوست نمی‌شود (به‌جای اینکه کل ذخیره لغو شود).
        private bool TryStorePendingDocument(out string storedPath)
        {
            storedPath = _savedDocPath;

            if (string.IsNullOrWhiteSpace(_pendingDocSourcePath) ||
                !System.IO.File.Exists(_pendingDocSourcePath))
                return true;

            string extension = System.IO.Path.GetExtension(_pendingDocSourcePath);
            string suggested = SafeFileName(_txtFullName.Text.Trim()) + "_فرم_درخواستی" + extension;

            using (SaveFileDialog sfd = new SaveFileDialog
            {
                Title = "محل ذخیره‌سازی سند را انتخاب کنید",
                FileName = suggested,
                Filter = "همان نوع فایل|*" + extension + "|همه فایل‌ها|*.*",
                OverwritePrompt = true
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK)
                {
                    UiTheme.ShowWarning(this, "مسیر ذخیره‌ی سند انتخاب نشد؛ متقاضی بدون سند ذخیره می‌شود.");
                    return true;
                }

                try
                {
                    string folder = System.IO.Path.GetDirectoryName(sfd.FileName);
                    if (!string.IsNullOrWhiteSpace(folder))
                        System.IO.Directory.CreateDirectory(folder);

                    System.IO.File.Copy(_pendingDocSourcePath, sfd.FileName, true);
                    storedPath = sfd.FileName;
                    return true;
                }
                catch (Exception ex)
                {
                    UiTheme.ShowError(this, "ذخیره‌ی سند ممکن نشد: " + ex.Message);
                    return false;
                }
            }
        }

        private static string SafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "متقاضی";

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                value = value.Replace(c.ToString(), "");

            value = value.Trim();
            return string.IsNullOrWhiteSpace(value) ? "متقاضی" : value;
        }

        private Panel MakeNoteField(string label, TextBox input)
        {
            Panel p = new Panel { Width = 378, Height = 110, Margin = new Padding(4, 2, 4, 2) };
            Label l = new Label { Text = label, AutoSize = false, Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark };
            input.Dock = DockStyle.Fill;
            UiTheme.StyleTextBox(input);
            p.Controls.Add(input);
            p.Controls.Add(l);
            return p;
        }

        private void LoadApplicants()
        {
            int cid = SecurityContext.CenterFilterId;
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT ApplicantID,
       FullName    AS [نام متقاضی],
       FatherName  AS [نام پدر],
       Phone       AS [تماس],
       Province    AS [ولایت],
       RequestType AS [نوع درخواست],
       Status      AS [وضعیت],
       CreatedAt   AS [تاریخ ثبت]
FROM TblApplicant
WHERE (@CID = 0 OR CenterID = @CID OR CenterID IS NULL)
ORDER BY ApplicantID DESC", con))
            using (var da = new SQLiteDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                DataTable t = new DataTable();
                da.Fill(t);
                _grid.DataSource = t;
                if (_grid.Columns.Contains("ApplicantID"))
                    _grid.Columns["ApplicantID"].Visible = false;
            }
        }

        private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || !_grid.Columns.Contains("ApplicantID")) return;
            object idv = _grid.Rows[e.RowIndex].Cells["ApplicantID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            LoadApplicant(Convert.ToInt32(idv));
        }

        private void LoadApplicant(int id)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("SELECT * FROM TblApplicant WHERE ApplicantID = @ID", con))
            {
                cmd.Parameters.AddWithValue("@ID", id);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) return;
                    _currentId = id;
                    _txtFullName.Text = S(dr, "FullName");
                    _txtFatherName.Text = S(dr, "FatherName");
                    _txtPhone.Text = S(dr, "Phone");
                    _cmbProvince.Text = S(dr, "Province");
                    _txtDistrict.Text = S(dr, "District");
                    _cmbRequestType.Text = S(dr, "RequestType");
                    _cmbStatus.Text = S(dr, "Status");
                    _txtNote.Text = S(dr, "Note");
                    _txtTazkira.Text = S(dr, "TazkiraNo");

                    // سندِ ذخیره‌شده نمایش داده می‌شود؛ تا وقتی کاربر سند تازه‌ای
                    // انتخاب نکند، همین مسیر حفظ می‌شود.
                    _savedDocPath = S(dr, "DocPath");
                    _pendingDocSourcePath = "";
                    _txtDocPath.Text = _savedDocPath;
                }
            }
        }

        private static string S(IDataReader dr, string col)
        {
            int i = dr.GetOrdinal(col);
            return dr.IsDBNull(i) ? "" : dr.GetValue(i).ToString();
        }

        private void ClearForm()
        {
            _currentId = 0;
            _txtFullName.Text = "";
            _txtFatherName.Text = "";
            _txtPhone.Text = "";
            _cmbProvince.SelectedIndex = 0;
            _txtDistrict.Text = "";
            _cmbRequestType.SelectedIndex = 0;
            _cmbStatus.SelectedIndex = 0;
            _txtNote.Text = "";
            _txtTazkira.Text = "";
            _pendingDocSourcePath = "";
            _savedDocPath = "";
            if (_txtDocPath != null) _txtDocPath.Text = "";
            _txtFullName.Focus();
        }

        private void SaveApplicant()
        {
            if (!SecurityContext.CanEdit())
            {
                UiTheme.ShowWarning(this, "کاربر فقط مشاهده اجازه ثبت متقاضی ندارد.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_txtFullName.Text))
            {
                UiTheme.ShowWarning(this, "نام متقاضی را وارد کنید.");
                _txtFullName.Focus();
                return;
            }

            // مسیر ذخیره‌ی سند در همین مرحله‌ی تأیید پرسیده می‌شود (درخواست
            // کاربر) — پیش از نوشتن در دیتابیس، تا مسیرِ نهایی ثبت شود.
            string docPath;
            if (!TryStorePendingDocument(out docPath))
                return;

            _savedDocPath = docPath;

            try
            {
                using (var con = db.GetConnection())
                {
                    con.Open();
                    if (_currentId == 0)
                    {
                        using (var cmd = new SQLiteCommand(@"
INSERT INTO TblApplicant (FullName, FatherName, TazkiraNo, Phone, Province, District, RequestType, Note, Status, DocPath, DocType, CenterID, CreatedBy)
VALUES (@Full, @Father, @Tazkira, @Phone, @Prov, @Dist, @Req, @Note, @Status, @DocPath, @DocType, @CID, @By)", con))
                        {
                            AddParams(cmd);
                            cmd.Parameters.AddWithValue("@CID", SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value);
                            cmd.Parameters.AddWithValue("@By", (object)SecurityContext.Username ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        using (var cmd = new SQLiteCommand(@"
UPDATE TblApplicant SET
    FullName = @Full, FatherName = @Father, TazkiraNo = @Tazkira, Phone = @Phone, Province = @Prov,
    District = @Dist, RequestType = @Req, Note = @Note, Status = @Status,
    DocPath = @DocPath, DocType = @DocType
WHERE ApplicantID = @ID AND (@CID = 0 OR CenterID = @CID)", con))
                        {
                            AddParams(cmd);
                            cmd.Parameters.AddWithValue("@ID", _currentId);
                            // آموزش — رفع نشت/بازنویسی بین‌مرکزی: Admin غیر-SuperAdmin
                            // فقط می‌تواند متقاضی مرکز خودش را ویرایش کند (هم‌راستا با
                            // همان الگوی امنیتی که در FrmUsers/FrmCase اعمال شده است).
                            cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                UiTheme.ShowSuccess(this, "متقاضی ذخیره شد.");
                LoadApplicants();
                ClearForm();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ذخیره: " + ex.Message);
            }
        }

        private void AddParams(SQLiteCommand cmd)
        {
            cmd.Parameters.AddWithValue("@Full", _txtFullName.Text.Trim());
            cmd.Parameters.AddWithValue("@Father", (object)_txtFatherName.Text.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Phone", (object)_txtPhone.Text.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prov", (object)_cmbProvince.Text.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Dist", (object)_txtDistrict.Text.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Req", (object)_cmbRequestType.Text.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Note", (object)_txtNote.Text.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(_cmbStatus.Text) ? "در انتظار" : _cmbStatus.Text.Trim());
            cmd.Parameters.AddWithValue("@Tazkira", (object)_txtTazkira.Text.Trim() ?? DBNull.Value);

            // نوع سند فقط وقتی ثبت می‌شود که سندی وجود داشته باشد، تا رکوردهای
            // بدون سند مقدارِ گمراه‌کننده نگیرند.
            bool hasDoc = !string.IsNullOrWhiteSpace(_savedDocPath);
            cmd.Parameters.AddWithValue("@DocPath", hasDoc ? (object)_savedDocPath : DBNull.Value);
            cmd.Parameters.AddWithValue("@DocType", hasDoc ? (object)ApplicantDocType : DBNull.Value);
        }

        private void DeleteApplicant()
        {
            if (!SecurityContext.CanDelete())
            {
                UiTheme.ShowWarning(this, "حذف فقط برای مدیر مجاز است.");
                return;
            }
            if (_currentId == 0)
            {
                UiTheme.ShowWarning(this, "ابتدا یک متقاضی را انتخاب کنید.");
                return;
            }
            if (!UiTheme.ShowConfirm(this, "این متقاضی حذف شود؟", "حذف متقاضی"))
                return;

            int affected;
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand(
                "DELETE FROM TblApplicant WHERE ApplicantID = @ID AND (@CID = 0 OR CenterID = @CID)", con))
            {
                cmd.Parameters.AddWithValue("@ID", _currentId);
                // آموزش — رفع حذف بین‌مرکزی: Admin غیر-SuperAdmin نمی‌تواند متقاضی
                // مرکز دیگری را حذف کند؛ فقط رکورد متعلق به مرکز فعال حذف می‌شود.
                cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);
                con.Open();
                affected = cmd.ExecuteNonQuery();
            }
            if (affected == 0)
            {
                UiTheme.ShowWarning(this, "این متقاضی متعلق به مرکز دیگری است و حذف نشد.");
                return;
            }
            LoadApplicants();
            ClearForm();
        }

        private void PrintApplicant()
        {
            if (string.IsNullOrWhiteSpace(_txtFullName.Text))
            {
                UiTheme.ShowWarning(this, "ابتدا اطلاعات متقاضی را وارد یا انتخاب کنید.");
                return;
            }
            var fields = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("نام و تخلص متقاضی", _txtFullName.Text.Trim()),
                new KeyValuePair<string, string>("نام پدر", _txtFatherName.Text.Trim()),
                new KeyValuePair<string, string>("شماره تماس", _txtPhone.Text.Trim()),
                new KeyValuePair<string, string>("ولایت", _cmbProvince.Text.Trim()),
                new KeyValuePair<string, string>("ولسوالی", _txtDistrict.Text.Trim()),
                new KeyValuePair<string, string>("نوع درخواست", _cmbRequestType.Text.Trim()),
                new KeyValuePair<string, string>("وضعیت", _cmbStatus.Text.Trim()),
                new KeyValuePair<string, string>("یادداشت / شرح درخواست", _txtNote.Text.Trim()),
            };
            PrintHelper.PrintKeyValueDocument(this, "فرم درخواستی متقاضی — " + _txtFullName.Text.Trim(), fields);
        }

        // تبدیل به پرونده: فرم پرونده باز می‌شود و اطلاعات اولیه متقاضی به‌صورت
        // پیش‌فرض روی کلیپ‌بورد گذاشته می‌شود تا کاربر سریع بچسباند؛ سپس وضعیت
        // متقاضی به «تبدیل‌شده» تغییر می‌کند. (روش امن و بدون وابستگی به داخلی
        // FrmCase؛ کاربر پرونده را با شماره فرم اتوماتیک تکمیل می‌کند.)
        private void ConvertToCase()
        {
            if (_currentId == 0)
            {
                UiTheme.ShowWarning(this, "ابتدا یک متقاضی را از فهرست انتخاب کنید.");
                return;
            }

            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("UPDATE TblApplicant SET Status = 'تبدیل به پرونده' WHERE ApplicantID = @ID", con))
            {
                cmd.Parameters.AddWithValue("@ID", _currentId);
                con.Open();
                cmd.ExecuteNonQuery();
            }

            try
            {
                Clipboard.SetText(
                    "نام سرپرست: " + _txtFullName.Text.Trim() + Environment.NewLine +
                    "نام پدر: " + _txtFatherName.Text.Trim() + Environment.NewLine +
                    "تماس: " + _txtPhone.Text.Trim() + Environment.NewLine +
                    "ولایت: " + _cmbProvince.Text.Trim() + " / ولسوالی: " + _txtDistrict.Text.Trim() + Environment.NewLine +
                    "نوع درخواست: " + _cmbRequestType.Text.Trim() + Environment.NewLine +
                    "شرح: " + _txtNote.Text.Trim());
            }
            catch { /* کلیپ‌بورد غیرحیاتی است */ }

            LoadApplicants();

            using (var frm = new FrmCase())
                frm.ShowDialog(this);
        }
    }
}

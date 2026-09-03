using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement
{
    // ─────────────────────────────────────────────────────────────────────────
    // ماژول «اداری و کارمندان».
    //
    // چهار تب: کارمندان · درخواست رخصتی · شروع و ختم ماموریت · درخواست استخدام.
    // هر تب یک گرید و چند خانه دارد، و یک دکمهٔ «خروجی فورم» که همان رکورد را
    // روی فورمِ رسمیِ Word می‌نشاند (خروجی Word و PDF).
    //
    // آموزش — چرا جدول‌های Adm* و نه افزودن به Acc*: بخش مالی در حال استفادهٔ
    // روزمره است. رخصتی و ماموریت و استخدام سندهای اداری‌اند نه مالی؛ قاطی
    // کردنشان با جدول‌های حسابداری یعنی هر تغییر اداری به گزارش‌های مالی ریسک
    // وارد می‌کند. اتصالِ حق‌الماموریت به AccSalary عمداً انجام نشده و منتظر
    // تصمیم کاربر است.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmEmployees : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        private static readonly string[] LeaveTypes =
            { "رخصتی عادی", "رخصتی مریضی", "رخصتی اضطراری", "سایر" };

        public FrmEmployees()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "اداری و کارمندان  —  " + SecurityContext.CenterDisplay;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1040, 650);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);

            var tabs = new TabControl { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            tabs.TabPages.Add(BuildEmployeesTab());
            tabs.TabPages.Add(BuildLeaveTab());
            tabs.TabPages.Add(BuildMissionTab());
            tabs.TabPages.Add(BuildJobApplicationTab());
            Controls.Add(tabs);
        }

        // ═══════════════════════════════════════════════════════════════════
        // زیرساخت مشترک تب‌ها
        // ═══════════════════════════════════════════════════════════════════
        private sealed class TabParts
        {
            public TabPage Page;
            public DataGridView Grid;
            public FlowLayoutPanel Editors;
            public FlowLayoutPanel Buttons;
        }

        private TabParts NewTab(string title)
        {
            var page = new TabPage(title) { BackColor = UiTheme.Background };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RightToLeft = RightToLeft.Yes,
                BackgroundColor = UiTheme.CardBack
            };

            var editors = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 130,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                AutoScroll = true,
                BackColor = UiTheme.CardBack,
                Padding = new Padding(8, 6, 8, 4)
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = UiTheme.CardBack,
                Padding = new Padding(8, 8, 8, 8)
            };

            page.Controls.Add(grid);
            page.Controls.Add(editors);
            page.Controls.Add(buttons);

            return new TabParts { Page = page, Grid = grid, Editors = editors, Buttons = buttons };
        }

        // یک خانهٔ ورودی با برچسب بالای آن.
        private static Control Field(string caption, Control editor, int width)
        {
            var panel = new Panel { Width = width, Height = 52, Margin = new Padding(4, 2, 4, 2) };
            var lbl = new Label
            {
                Text = caption,
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted
            };
            editor.Dock = DockStyle.Top;
            panel.Controls.Add(editor);
            panel.Controls.Add(lbl);
            return panel;
        }

        private static TextBox NewText() { return new TextBox { RightToLeft = RightToLeft.Yes }; }

        private static ComboBox NewCombo(IEnumerable<string> items)
        {
            var c = new ComboBox { RightToLeft = RightToLeft.Yes, DropDownStyle = ComboBoxStyle.DropDown };
            foreach (string i in items) c.Items.Add(i);
            return c;
        }

        private static string Txt(Control c) { return (c.Text ?? "").Trim(); }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        private int? SelectedId(DataGridView grid, string idColumn)
        {
            if (grid.CurrentRow == null) return null;
            var row = grid.CurrentRow.DataBoundItem as DataRowView;
            if (row == null || !row.Row.Table.Columns.Contains(idColumn)) return null;
            return Convert.ToInt32(row[idColumn]);
        }

        private void Reload(DataGridView grid, string sql, string idColumn)
        {
            grid.DataSource = db.Query(sql, P("@cid", SecurityContext.CenterFilterId));
            if (grid.Columns.Contains(idColumn)) grid.Columns[idColumn].Visible = false;
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب ۱: کارمندان
        // ═══════════════════════════════════════════════════════════════════
        private TextBox _empName, _empFather, _empTazkira, _empPosition, _empDept, _empPhone, _empDistrict;
        private ComboBox _empProvince, _empStatus;
        private DataGridView _gridEmp;

        private TabPage BuildEmployeesTab()
        {
            TabParts t = NewTab("کارمندان");
            _gridEmp = t.Grid;

            _empName = NewText(); _empFather = NewText(); _empTazkira = NewText();
            _empPosition = NewText(); _empDept = NewText(); _empPhone = NewText(); _empDistrict = NewText();
            _empProvince = NewCombo(ProvinceList());
            _empStatus = NewCombo(new[] { "فعال", "غیرفعال" });
            _empStatus.Text = "فعال";

            t.Editors.Controls.Add(Field("نام و تخلص", _empName, 190));
            t.Editors.Controls.Add(Field("نام پدر", _empFather, 160));
            t.Editors.Controls.Add(Field("شماره تذکره", _empTazkira, 140));
            t.Editors.Controls.Add(Field("وظیفه", _empPosition, 160));
            t.Editors.Controls.Add(Field("بخش", _empDept, 140));
            t.Editors.Controls.Add(Field("شماره تماس", _empPhone, 130));
            t.Editors.Controls.Add(Field("ولایت", _empProvince, 140));
            t.Editors.Controls.Add(Field("ولسوالی", _empDistrict, 130));
            t.Editors.Controls.Add(Field("وضعیت", _empStatus, 110));

            var btnSave = UiTheme.CreateButton("ثبت کارمند", "✔", UiTheme.Success);
            btnSave.Size = new Size(130, 34);
            btnSave.Click += delegate { SaveEmployee(); };

            var btnClear = UiTheme.CreateSecondaryButton("خانه‌ها را خالی کن", "✕");
            btnClear.Size = new Size(160, 34);
            btnClear.Click += delegate { ClearEmployeeFields(); };

            t.Buttons.Controls.Add(btnSave);
            t.Buttons.Controls.Add(btnClear);

            t.Page.HandleCreated += delegate { LoadEmployees(); };
            return t.Page;
        }

        private static string[] ProvinceList()
        {
            try { return LookupHelper.GetValues("Province").ToArray(); }
            catch { return new string[0]; }
        }

        private void LoadEmployees()
        {
            Reload(_gridEmp, @"
SELECT EmployeeID, FullName AS [نام و تخلص], FatherName AS [نام پدر], Position AS [وظیفه],
       Department AS [بخش], Phone AS [تماس], Province AS [ولایت], Status AS [وضعیت]
FROM AdmEmployee
WHERE (@cid = 0 OR CenterID = @cid)
ORDER BY FullName", "EmployeeID");
        }

        private void ClearEmployeeFields()
        {
            _empName.Clear(); _empFather.Clear(); _empTazkira.Clear();
            _empPosition.Clear(); _empDept.Clear(); _empPhone.Clear(); _empDistrict.Clear();
            _empProvince.Text = ""; _empStatus.Text = "فعال";
        }

        private void SaveEmployee()
        {
            if (Txt(_empName).Length == 0)
            {
                UiTheme.ShowWarning(this, "نام کارمند نمی‌تواند خالی باشد.");
                _empName.Focus();
                return;
            }

            try
            {
                db.ExecuteNonQuery(@"
INSERT INTO AdmEmployee
    (FullName, FatherName, TazkiraNo, Position, Department, Phone, Province, District, Status, CenterID, CreatedBy)
VALUES
    (@n, @f, @t, @pos, @dep, @ph, @prov, @dist, @st, @cid, @by)",
                    P("@n", Txt(_empName)), P("@f", Txt(_empFather)), P("@t", Txt(_empTazkira)),
                    P("@pos", Txt(_empPosition)), P("@dep", Txt(_empDept)), P("@ph", Txt(_empPhone)),
                    P("@prov", Txt(_empProvince)), P("@dist", Txt(_empDistrict)), P("@st", Txt(_empStatus)),
                    P("@cid", SecurityContext.CurrentCenterId), P("@by", SecurityContext.Username));

                ClearEmployeeFields();
                LoadEmployees();
                UiTheme.ShowSuccess(this, "کارمند ثبت شد.");
            }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ثبت کارمند: " + ex.Message); }
        }

        // فهرست کارمندان برای کمبوها؛ نامِ نمایشی و شناسه.
        private DataTable EmployeeLookup()
        {
            return db.Query(@"
SELECT EmployeeID, FullName AS Display, COALESCE(FatherName,'') AS FatherName,
       COALESCE(Position,'') AS Position, COALESCE(Department,'') AS Department,
       COALESCE(Province,'') AS Province, COALESCE(District,'') AS District
FROM AdmEmployee
WHERE (@cid = 0 OR CenterID = @cid) AND Status = 'فعال'
ORDER BY FullName", P("@cid", SecurityContext.CenterFilterId));
        }

        private void BindEmployeeCombo(ComboBox cmb)
        {
            DataTable dt = EmployeeLookup();
            cmb.DisplayMember = "Display";
            cmb.ValueMember = "EmployeeID";
            cmb.DataSource = dt;
            cmb.SelectedIndex = dt.Rows.Count > 0 ? 0 : -1;
        }

        private DataRow SelectedEmployeeRow(ComboBox cmb)
        {
            var view = cmb.SelectedItem as DataRowView;
            return view == null ? null : view.Row;
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب ۲: درخواست رخصتی
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _lvEmployee, _lvType;
        private TextBox _lvOther, _lvFrom, _lvTo, _lvDays, _lvReason, _lvContact;
        private DataGridView _gridLeave;

        private TabPage BuildLeaveTab()
        {
            TabParts t = NewTab("درخواست رخصتی");
            _gridLeave = t.Grid;

            _lvEmployee = NewCombo(new string[0]);
            _lvEmployee.DropDownStyle = ComboBoxStyle.DropDownList;
            _lvType = NewCombo(LeaveTypes); _lvType.Text = LeaveTypes[0];
            _lvOther = NewText(); _lvFrom = NewText(); _lvTo = NewText();
            _lvDays = NewText(); _lvReason = NewText(); _lvContact = NewText();
            _lvFrom.Text = PersianDateHelper.ToPersianDateString(DateTime.Now);

            t.Editors.Controls.Add(Field("کارمند", _lvEmployee, 200));
            t.Editors.Controls.Add(Field("نوع رخصتی", _lvType, 150));
            t.Editors.Controls.Add(Field("سایر (اگر نوعش سایر است)", _lvOther, 170));
            t.Editors.Controls.Add(Field("از تاریخ", _lvFrom, 120));
            t.Editors.Controls.Add(Field("الی تاریخ", _lvTo, 120));
            t.Editors.Controls.Add(Field("جمعاً (روز)", _lvDays, 90));
            t.Editors.Controls.Add(Field("دلیل", _lvReason, 220));
            t.Editors.Controls.Add(Field("آدرس و تماس در رخصتی", _lvContact, 220));

            var btnSave = UiTheme.CreateButton("ثبت رخصتی", "✔", UiTheme.Success);
            btnSave.Size = new Size(130, 34);
            btnSave.Click += delegate { SaveLeave(); };

            var btnForm = UiTheme.CreateButton("خروجی فورم رخصتی", "➤", UiTheme.Primary);
            btnForm.Size = new Size(180, 34);
            btnForm.Click += delegate { ExportLeaveForm(); };

            t.Buttons.Controls.Add(btnSave);
            t.Buttons.Controls.Add(btnForm);

            t.Page.HandleCreated += delegate { BindEmployeeCombo(_lvEmployee); LoadLeaves(); };
            return t.Page;
        }

        private void LoadLeaves()
        {
            Reload(_gridLeave, @"
SELECT l.LeaveID, e.FullName AS [کارمند], l.LeaveType AS [نوع], l.FromDate AS [از تاریخ],
       l.ToDate AS [الی تاریخ], l.TotalDays AS [روز], l.Reason AS [دلیل]
FROM AdmLeave l
LEFT JOIN AdmEmployee e ON e.EmployeeID = l.EmployeeID
WHERE (@cid = 0 OR l.CenterID = @cid)
ORDER BY l.LeaveID DESC", "LeaveID");
        }

        private void SaveLeave()
        {
            DataRow emp = SelectedEmployeeRow(_lvEmployee);
            if (emp == null) { UiTheme.ShowWarning(this, "ابتدا کارمند را انتخاب کنید."); return; }

            try
            {
                db.ExecuteNonQuery(@"
INSERT INTO AdmLeave
    (EmployeeID, LeaveType, OtherType, FromDate, ToDate, TotalDays, Reason, ContactInfo, CenterID, CreatedBy)
VALUES
    (@e, @t, @o, @f, @to, @d, @r, @c, @cid, @by)",
                    P("@e", emp["EmployeeID"]), P("@t", Txt(_lvType)), P("@o", Txt(_lvOther)),
                    P("@f", Txt(_lvFrom)), P("@to", Txt(_lvTo)), P("@d", Txt(_lvDays)),
                    P("@r", Txt(_lvReason)), P("@c", Txt(_lvContact)),
                    P("@cid", SecurityContext.CurrentCenterId), P("@by", SecurityContext.Username));

                LoadLeaves();
                UiTheme.ShowSuccess(this, "درخواست رخصتی ثبت شد.");
            }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ثبت رخصتی: " + ex.Message); }
        }

        private void ExportLeaveForm()
        {
            DataRow emp = SelectedEmployeeRow(_lvEmployee);
            if (emp == null) { UiTheme.ShowWarning(this, "ابتدا کارمند را انتخاب کنید."); return; }

            string name = Convert.ToString(emp["Display"]);

            // نوعِ رخصتی در فورمِ اصلی چهار مربعِ تیک‌خور است. چون خروجیِ Word
            // مربع تیک‌خور ندارد، همان چهار گزینه با علامتِ ☑ / ☐ در یک سطر
            // نوشته می‌شود — دقیقاً همان معلومات، بدون ادعای چیزی که نیست.
            string chosen = Txt(_lvType);
            var parts = new List<string>();
            foreach (string t in LeaveTypes)
                parts.Add((t == chosen ? "☑  " : "☐  ") + t);
            string typeLine = string.Join("        ", parts.ToArray());

            var fields = new List<FrmDocxForm.FieldDef>
            {
                FrmDocxForm.FieldDef.Section("کارمند"),
                FrmDocxForm.FieldDef.Text("نام و نام خانوادگی", "EmployeeName", name, true),
                FrmDocxForm.FieldDef.Text("سمت / وظیفه", "Position", Convert.ToString(emp["Position"])),
                FrmDocxForm.FieldDef.Text("بخش", "Department", Convert.ToString(emp["Department"])),

                FrmDocxForm.FieldDef.Section("رخصتی"),
                FrmDocxForm.FieldDef.Text("سایر", "OtherType", Txt(_lvOther)),
                FrmDocxForm.FieldDef.Text("از تاریخ", "FromDate", Txt(_lvFrom)),
                FrmDocxForm.FieldDef.Text("الی تاریخ", "ToDate", Txt(_lvTo)),
                FrmDocxForm.FieldDef.Text("جمعاً (روز)", "TotalDays", Txt(_lvDays)),
                FrmDocxForm.FieldDef.Text("دلیل رخصتی", "Reason", Txt(_lvReason)),
                FrmDocxForm.FieldDef.Text("آدرس و تماس", "ContactInfo", Txt(_lvContact)),
                FrmDocxForm.FieldDef.Text("تاریخ تأیید مدیریت", "ApprovalDate",
                    PersianDateHelper.ToPersianDateString(DateTime.Now))
            };

            using (var frm = new FrmDocxForm("فرم درخواست رخصتی", DocxFormExport.TplLeaveRequest,
                                             fields, "درخواست رخصتی - " + name))
            {
                frm.Hidden("LeaveTypeLine", typeLine);
                frm.Require("FromDate", "ToDate");
                frm.ShowDialog(this);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب ۳: شروع و ختم ماموریت
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _msEmployee;
        private TextBox _msPlace, _msPurpose, _msStart, _msEnd, _msAllowance;
        private DataGridView _gridMission;

        private TabPage BuildMissionTab()
        {
            TabParts t = NewTab("شروع و ختم ماموریت");
            _gridMission = t.Grid;

            _msEmployee = NewCombo(new string[0]);
            _msEmployee.DropDownStyle = ComboBoxStyle.DropDownList;
            _msPlace = NewText(); _msPurpose = NewText(); _msStart = NewText();
            _msEnd = NewText(); _msAllowance = NewText();
            _msStart.Text = PersianDateHelper.ToPersianDateString(DateTime.Now);

            t.Editors.Controls.Add(Field("کارمند", _msEmployee, 200));
            t.Editors.Controls.Add(Field("ولایت / ولسوالی ماموریت", _msPlace, 190));
            t.Editors.Controls.Add(Field("هدف ماموریت", _msPurpose, 260));
            t.Editors.Controls.Add(Field("تاریخ شروع", _msStart, 120));
            t.Editors.Controls.Add(Field("تاریخ ختم", _msEnd, 120));
            t.Editors.Controls.Add(Field("حق‌الماموریت (افغانی)", _msAllowance, 150));

            var btnSave = UiTheme.CreateButton("ثبت ماموریت", "✔", UiTheme.Success);
            btnSave.Size = new Size(130, 34);
            btnSave.Click += delegate { SaveMission(); };

            var btnForm = UiTheme.CreateButton("خروجی فورم ماموریت", "➤", UiTheme.Primary);
            btnForm.Size = new Size(190, 34);
            btnForm.Click += delegate { ExportMissionForm(); };

            t.Buttons.Controls.Add(btnSave);
            t.Buttons.Controls.Add(btnForm);

            t.Page.HandleCreated += delegate { BindEmployeeCombo(_msEmployee); LoadMissions(); };
            return t.Page;
        }

        private void LoadMissions()
        {
            Reload(_gridMission, @"
SELECT m.MissionID, e.FullName AS [کارمند], m.MissionPlace AS [محل ماموریت],
       m.Purpose AS [هدف], m.StartDate AS [شروع], m.EndDate AS [ختم],
       m.Allowance AS [حق‌الماموریت]
FROM AdmMission m
LEFT JOIN AdmEmployee e ON e.EmployeeID = m.EmployeeID
WHERE (@cid = 0 OR m.CenterID = @cid)
ORDER BY m.MissionID DESC", "MissionID");
        }

        private void SaveMission()
        {
            DataRow emp = SelectedEmployeeRow(_msEmployee);
            if (emp == null) { UiTheme.ShowWarning(this, "ابتدا کارمند را انتخاب کنید."); return; }

            double allowance;
            if (!double.TryParse(Txt(_msAllowance), out allowance)) allowance = 0;

            try
            {
                db.ExecuteNonQuery(@"
INSERT INTO AdmMission
    (EmployeeID, MissionPlace, Purpose, StartDate, EndDate, Allowance, CenterID, CreatedBy)
VALUES
    (@e, @p, @pur, @s, @en, @a, @cid, @by)",
                    P("@e", emp["EmployeeID"]), P("@p", Txt(_msPlace)), P("@pur", Txt(_msPurpose)),
                    P("@s", Txt(_msStart)), P("@en", Txt(_msEnd)), P("@a", allowance),
                    P("@cid", SecurityContext.CurrentCenterId), P("@by", SecurityContext.Username));

                LoadMissions();
                UiTheme.ShowSuccess(this, "ماموریت ثبت شد.");
            }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ثبت ماموریت: " + ex.Message); }
        }

        private void ExportMissionForm()
        {
            DataRow emp = SelectedEmployeeRow(_msEmployee);
            if (emp == null) { UiTheme.ShowWarning(this, "ابتدا کارمند را انتخاب کنید."); return; }

            string name = Convert.ToString(emp["Display"]);

            var fields = new List<FrmDocxForm.FieldDef>
            {
                FrmDocxForm.FieldDef.Text("نام و تخلص کارمند", "EmployeeName", name, true),
                FrmDocxForm.FieldDef.Text("ولایت / ولسوالی محل ماموریت", "MissionPlace", Txt(_msPlace)),
                FrmDocxForm.FieldDef.Text("هدف ماموریت", "MissionPurpose", Txt(_msPurpose)),
                FrmDocxForm.FieldDef.Text("تاریخ شروع ماموریت", "StartDate", Txt(_msStart)),
                FrmDocxForm.FieldDef.Text("تاریخ ختم ماموریت", "EndDate", Txt(_msEnd))
            };

            using (var frm = new FrmDocxForm("فورم شروع و ختم ماموریت", DocxFormExport.TplMissionForm,
                                             fields, "فورم ماموریت - " + name))
            {
                frm.Require("MissionPlace", "StartDate");
                frm.ShowDialog(this);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب ۴: درخواست استخدام
        // ═══════════════════════════════════════════════════════════════════
        private TextBox _jaName, _jaFather, _jaTazkira, _jaBirth, _jaAddress, _jaPhone,
                        _jaChildren, _jaDept, _jaTitle, _jaEducation, _jaField, _jaSalary;
        private ComboBox _jaMarital, _jaCooperation;
        private DataGridView _gridJobApp;

        private TabPage BuildJobApplicationTab()
        {
            TabParts t = NewTab("درخواست استخدام");
            _gridJobApp = t.Grid;

            _jaName = NewText(); _jaFather = NewText(); _jaTazkira = NewText(); _jaBirth = NewText();
            _jaAddress = NewText(); _jaPhone = NewText(); _jaChildren = NewText();
            _jaDept = NewText(); _jaTitle = NewText(); _jaEducation = NewText();
            _jaField = NewText(); _jaSalary = NewText();
            _jaMarital = NewCombo(new[] { "مجرد", "متاهل" });
            _jaCooperation = NewCombo(new[] { "دائمی", "موقت" });

            t.Editors.Controls.Add(Field("نام و نام خانوادگی", _jaName, 190));
            t.Editors.Controls.Add(Field("نام پدر", _jaFather, 150));
            t.Editors.Controls.Add(Field("شماره تذکره", _jaTazkira, 130));
            t.Editors.Controls.Add(Field("تاریخ تولد", _jaBirth, 120));
            t.Editors.Controls.Add(Field("شماره تماس", _jaPhone, 130));
            t.Editors.Controls.Add(Field("آدرس", _jaAddress, 220));
            t.Editors.Controls.Add(Field("وضعیت تاهل", _jaMarital, 110));
            t.Editors.Controls.Add(Field("تعداد فرزندان", _jaChildren, 100));
            t.Editors.Controls.Add(Field("بخش درخواست‌کننده", _jaDept, 160));
            t.Editors.Controls.Add(Field("عنوان وظیفه", _jaTitle, 160));
            t.Editors.Controls.Add(Field("میزان تحصیلات", _jaEducation, 140));
            t.Editors.Controls.Add(Field("رشته تحصیلی", _jaField, 140));
            t.Editors.Controls.Add(Field("نحوه همکاری", _jaCooperation, 110));
            t.Editors.Controls.Add(Field("حقوق پیشنهادی (عدد)", _jaSalary, 140));

            var btnSave = UiTheme.CreateButton("ثبت درخواست", "✔", UiTheme.Success);
            btnSave.Size = new Size(130, 34);
            btnSave.Click += delegate { SaveJobApplication(); };

            var btnForm = UiTheme.CreateButton("خروجی فورم استخدام", "➤", UiTheme.Primary);
            btnForm.Size = new Size(190, 34);
            btnForm.Click += delegate { ExportJobApplicationForm(); };

            t.Buttons.Controls.Add(btnSave);
            t.Buttons.Controls.Add(btnForm);

            t.Page.HandleCreated += delegate { LoadJobApplications(); };
            return t.Page;
        }

        private void LoadJobApplications()
        {
            Reload(_gridJobApp, @"
SELECT ApplicationID, FullName AS [نام متقاضی], FatherName AS [نام پدر],
       JobTitle AS [عنوان وظیفه], Department AS [بخش], Phone AS [تماس], Status AS [وضعیت]
FROM AdmJobApplication
WHERE (@cid = 0 OR CenterID = @cid)
ORDER BY ApplicationID DESC", "ApplicationID");
        }

        private void SaveJobApplication()
        {
            if (Txt(_jaName).Length == 0)
            {
                UiTheme.ShowWarning(this, "نام متقاضی نمی‌تواند خالی باشد.");
                _jaName.Focus();
                return;
            }

            try
            {
                db.ExecuteNonQuery(@"
INSERT INTO AdmJobApplication
    (FullName, FatherName, TazkiraNo, BirthDate, Address, Phone, MaritalStatus, ChildrenCount,
     Department, JobTitle, EducationLevel, FieldOfStudy, CooperationType, SalaryFigure, CenterID, CreatedBy)
VALUES
    (@n, @f, @t, @b, @a, @ph, @m, @ch, @dep, @jt, @ed, @fld, @co, @sal, @cid, @by)",
                    P("@n", Txt(_jaName)), P("@f", Txt(_jaFather)), P("@t", Txt(_jaTazkira)),
                    P("@b", Txt(_jaBirth)), P("@a", Txt(_jaAddress)), P("@ph", Txt(_jaPhone)),
                    P("@m", Txt(_jaMarital)), P("@ch", Txt(_jaChildren)), P("@dep", Txt(_jaDept)),
                    P("@jt", Txt(_jaTitle)), P("@ed", Txt(_jaEducation)), P("@fld", Txt(_jaField)),
                    P("@co", Txt(_jaCooperation)), P("@sal", Txt(_jaSalary)),
                    P("@cid", SecurityContext.CurrentCenterId), P("@by", SecurityContext.Username));

                LoadJobApplications();
                UiTheme.ShowSuccess(this, "درخواست استخدام ثبت شد.");
            }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ثبت درخواست: " + ex.Message); }
        }

        private void ExportJobApplicationForm()
        {
            if (Txt(_jaName).Length == 0)
            {
                UiTheme.ShowWarning(this, "ابتدا نام متقاضی را وارد کنید.");
                _jaName.Focus();
                return;
            }

            var fields = new List<FrmDocxForm.FieldDef>
            {
                FrmDocxForm.FieldDef.Section("وظیفهٔ درخواستی"),
                FrmDocxForm.FieldDef.Text("بخش درخواست‌کننده", "Department", Txt(_jaDept)),
                FrmDocxForm.FieldDef.Text("عنوان وظیفه", "JobTitle", Txt(_jaTitle)),

                FrmDocxForm.FieldDef.Section("۱ — اطلاعات شخصی"),
                FrmDocxForm.FieldDef.Text("نام و نام خانوادگی", "FullName", Txt(_jaName), true),
                FrmDocxForm.FieldDef.Text("نام پدر", "FatherName", Txt(_jaFather)),
                FrmDocxForm.FieldDef.Text("شماره تذکره", "TazkiraNo", Txt(_jaTazkira)),
                FrmDocxForm.FieldDef.Text("تاریخ تولد", "BirthDate", Txt(_jaBirth)),
                FrmDocxForm.FieldDef.Text("آدرس محل سکونت", "Address", Txt(_jaAddress)),
                FrmDocxForm.FieldDef.Text("شماره تماس", "Phone", Txt(_jaPhone)),
                FrmDocxForm.FieldDef.Text("وضعیت تاهل", "MaritalStatus", Txt(_jaMarital)),
                FrmDocxForm.FieldDef.Text("تعداد فرزندان", "ChildrenCount", Txt(_jaChildren)),

                FrmDocxForm.FieldDef.Section("۲ — سوابق تحصیلی"),
                FrmDocxForm.FieldDef.Text("میزان تحصیلات", "EducationLevel", Txt(_jaEducation)),
                FrmDocxForm.FieldDef.Text("رشته تحصیلی", "FieldOfStudy", Txt(_jaField)),
                FrmDocxForm.FieldDef.Text("نام مؤسسه آموزشی", "Institute"),
                FrmDocxForm.FieldDef.Text("شهر — کشور", "InstituteCity"),
                FrmDocxForm.FieldDef.Text("تاریخ شروع", "StudyFrom"),
                FrmDocxForm.FieldDef.Text("تاریخ پایان", "StudyTo"),

                FrmDocxForm.FieldDef.Section("۳ — تجربیات شغلی"),
                FrmDocxForm.FieldDef.Text("نام سازمان / شرکت", "LastOrg"),
                FrmDocxForm.FieldDef.Text("سمت / شغل", "LastPosition"),
                FrmDocxForm.FieldDef.Text("مدت سابقه", "ExperienceYears"),
                FrmDocxForm.FieldDef.Text("تاریخ پایان", "ExperienceEnd"),
                FrmDocxForm.FieldDef.Text("علت ترک کار", "LeavingReason"),

                FrmDocxForm.FieldDef.Section("۴ — زبان‌ها و مهارت‌ها"),
                FrmDocxForm.FieldDef.Text("زبان اول", "Language1"),
                FrmDocxForm.FieldDef.Choice("سطح زبان اول", "Language1Level",
                    new[] { "ضعیف", "متوسط", "خوب", "عالی" }),
                FrmDocxForm.FieldDef.Text("زبان دوم", "Language2"),
                FrmDocxForm.FieldDef.Choice("سطح زبان دوم", "Language2Level",
                    new[] { "ضعیف", "متوسط", "خوب", "عالی" }),
                FrmDocxForm.FieldDef.Text("آشنایی با کامپیوتر", "ComputerSkills"),
                FrmDocxForm.FieldDef.Text("مهارت‌ها و توانایی‌ها", "Skills"),

                FrmDocxForm.FieldDef.Section("۵ — معرف و شرایط"),
                FrmDocxForm.FieldDef.Text("نام معرف", "RefName"),
                FrmDocxForm.FieldDef.Text("شماره تماس معرف", "RefPhone"),
                FrmDocxForm.FieldDef.Text("موقف معرف در موسسه", "RefPosition"),
                FrmDocxForm.FieldDef.Text("نحوه همکاری", "CooperationType", Txt(_jaCooperation)),
                FrmDocxForm.FieldDef.Text("توضیحات همکاری", "CooperationNote"),
                FrmDocxForm.FieldDef.Text("حقوق پیشنهادی (عدد)", "SalaryFigure", Txt(_jaSalary)),
                FrmDocxForm.FieldDef.Text("حقوق پیشنهادی (حروف)", "SalaryWords"),
                FrmDocxForm.FieldDef.Text("شرح وظایف", "JobDescription")
            };

            using (var frm = new FrmDocxForm("فورم درخواست استخدام", DocxFormExport.TplJobApplication,
                                             fields, "درخواست استخدام - " + Txt(_jaName)))
            {
                frm.Require("JobTitle");
                frm.ShowDialog(this);
            }
        }
    }
}

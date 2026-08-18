using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «وظایف» — وظایف کاربر جاری و (برای مدیر سیستم) همه وظایف.
    //
    // وظایف خودکار (مثلاً «تأیید ...») توسط سامانه تأیید ساخته و بسته می‌شوند؛
    // این پنجره فقط آن‌ها را نشان می‌دهد و اجازه تغییر وضعیت دستی می‌دهد.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmTasks : Form
    {
        private DataGridView _gridMine, _gridAll;
        private CheckBox     _chkOpenOnly;
        private ComboBox     _cmbStatus;

        public FrmTasks()
        {
            BuildUi();
            LoadMine();
        }

        private void BuildUi()
        {
            Text              = "وظایف";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1120, 680);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
            tabs.TabPages.Add(BuildMineTab());

            if (SecurityContext.IsAdmin())
                tabs.TabPages.Add(BuildAllTab());

            Controls.Add(tabs);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.PrimaryDark };
            header.Controls.Add(new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(0xCF, 0xDD, 0xEE),
                Font      = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopLeft,
                Padding   = new Padding(0, 0, 20, 0),
                Text      = "وظایفی که به شما یا به نقش شما تخصیص یافته‌اند."
            });
            header.Controls.Add(new Label
            {
                Text      = "مدیریت وظایف",
                Dock      = DockStyle.Top,
                Height    = 38,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            });
            Controls.Add(header);
        }

        private TabPage BuildMineTab()
        {
            TabPage page = NewPage("وظایف من");

            _gridMine = CreateGrid();

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_gridMine);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };

            _chkOpenOnly = new CheckBox
            {
                Dock        = DockStyle.Right,
                Width       = 150,
                Text        = "فقط وظایف باز",
                Checked     = true,
                RightToLeft = RightToLeft.Yes
            };
            _chkOpenOnly.CheckedChanged += delegate { LoadMine(); };

            FlowLayoutPanel buttons = NewButtonBar();
            buttons.Controls.Add(MakeButton("وظیفه جدید", "＋", UiTheme.Primary, NewTask));
            buttons.Controls.Add(MakeButton("شروع کار", "▶", UiTheme.PrimaryLight,
                delegate { SetStatus(_gridMine, TaskService.StatusInProgress); }));
            buttons.Controls.Add(MakeButton("انجام شد", "✔", UiTheme.Success,
                delegate { SetStatus(_gridMine, TaskService.StatusDone); }));
            buttons.Controls.Add(MakeButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight, delegate { LoadMine(); }));

            toolbar.Controls.Add(buttons);
            toolbar.Controls.Add(_chkOpenOnly);
            main.Controls.Add(toolbar);

            page.Controls.Add(main);
            page.Enter += delegate { LoadMine(); };
            return page;
        }

        private TabPage BuildAllTab()
        {
            TabPage page = NewPage("همه وظایف");

            _gridAll = CreateGrid();

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_gridAll);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };

            _cmbStatus = new ComboBox
            {
                Dock          = DockStyle.Right,
                Width         = 170,
                DropDownStyle = ComboBoxStyle.DropDownList,
                RightToLeft   = RightToLeft.Yes
            };
            _cmbStatus.Items.AddRange(new object[]
            {
                "همه", TaskService.StatusOpen, TaskService.StatusInProgress,
                TaskService.StatusDone, TaskService.StatusCanceled
            });
            _cmbStatus.SelectedIndex = 0;
            _cmbStatus.SelectedIndexChanged += delegate { LoadAll(); };

            FlowLayoutPanel buttons = NewButtonBar();
            buttons.Controls.Add(MakeButton("تخصیص", "👤", UiTheme.Primary, AssignTask));
            buttons.Controls.Add(MakeButton("لغو",   "✖", UiTheme.Warning,
                delegate { SetStatus(_gridAll, TaskService.StatusCanceled); }));
            buttons.Controls.Add(MakeButton("حذف",   "🗑", UiTheme.Danger, DeleteTask));
            buttons.Controls.Add(MakeButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight, delegate { LoadAll(); }));

            toolbar.Controls.Add(buttons);
            toolbar.Controls.Add(_cmbStatus);
            main.Controls.Add(toolbar);

            page.Controls.Add(main);
            page.Enter += delegate { LoadAll(); };
            return page;
        }

        // ─── بارگذاری ──────────────────────────────────────────────────────
        private void LoadMine()
        {
            _gridMine.DataSource = TaskService.GetMyTasks(_chkOpenOnly.Checked);
            HideIdColumn(_gridMine);
        }

        private void LoadAll()
        {
            if (_gridAll == null) return;

            string status = _cmbStatus.SelectedIndex <= 0 ? "" : Convert.ToString(_cmbStatus.SelectedItem);
            _gridAll.DataSource = TaskService.GetAll(status);
            HideIdColumn(_gridAll);
        }

        // ─── عملیات ────────────────────────────────────────────────────────
        private void NewTask()
        {
            Dictionary<string, string> values = EntPrompt.Edit(this, "وظیفه جدید",
                EntField.Text("Title", "عنوان", ""),
                EntField.Multiline("Desc", "توضیح", ""),
                EntField.Combo("Priority", "اولویت", "متوسط", PriorityItems()),
                EntField.Combo("Role", "تخصیص به نقش", "", RoleItems()),
                EntField.Number("UserID", "یا شناسه کاربر", "0"),
                EntField.Text("Due", "مهلت (yyyy-MM-dd)", ""),
                EntField.Text("Entity", "موجودیت (اختیاری)", ""),
                EntField.Number("EntityID", "شناسه رکورد", "0"));

            if (values == null) return;

            if (values["Title"].Length == 0)
            {
                UiTheme.ShowWarning(this, "عنوان وظیفه الزامی است.");
                return;
            }

            int userId = EntDb.ToInt(values["UserID"]);

            // اگر هیچ مسئولی مشخص نشده، وظیفه به خود کاربر جاری تخصیص می‌یابد
            // تا وظیفه بی‌صاحب نماند.
            if (userId <= 0 && values["Role"].Length == 0)
                userId = SecurityContext.UserId;

            try
            {
                TaskService.Create(
                    values["Title"], values["Desc"],
                    values["Entity"], EntDb.ToInt(values["EntityID"]),
                    userId, values["Role"],
                    values["Priority"], values["Due"],
                    TaskService.SourceManual, 0);

                LoadMine();
                LoadAll();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ثبت وظیفه انجام نشد: " + ex.Message);
            }
        }

        private void SetStatus(DataGridView grid, string status)
        {
            int taskId = SelectedId(grid);

            if (taskId <= 0)
            {
                UiTheme.ShowWarning(this, "یک وظیفه را انتخاب کنید.");
                return;
            }

            WorkflowActionResult result = TaskService.ChangeStatus(taskId, status);

            if (!result.Applied)
                UiTheme.ShowWarning(this, result.Message);

            LoadMine();
            LoadAll();
        }

        private void AssignTask()
        {
            int taskId = SelectedId(_gridAll);

            if (taskId <= 0)
            {
                UiTheme.ShowWarning(this, "یک وظیفه را انتخاب کنید.");
                return;
            }

            Dictionary<string, string> values = EntPrompt.Edit(this, "تخصیص وظیفه",
                EntField.Number("UserID", "شناسه کاربر", "0"),
                EntField.Combo("Role", "یا نقش", "", RoleItems()));

            if (values == null) return;

            WorkflowActionResult result = TaskService.Assign(
                taskId, EntDb.ToInt(values["UserID"]), values["Role"]);

            if (!result.Applied) UiTheme.ShowWarning(this, result.Message);

            LoadAll();
        }

        private void DeleteTask()
        {
            int taskId = SelectedId(_gridAll);

            if (taskId <= 0)
            {
                UiTheme.ShowWarning(this, "یک وظیفه را انتخاب کنید.");
                return;
            }

            if (!UiTheme.ShowConfirm(this, "وظیفه انتخاب‌شده حذف شود؟", "حذف وظیفه"))
                return;

            WorkflowActionResult result = TaskService.Delete(taskId);

            if (!result.Applied) UiTheme.ShowWarning(this, result.Message);

            LoadAll();
        }

        // ─── کمکی‌ها ───────────────────────────────────────────────────────
        private static List<KeyValuePair<string, string>> PriorityItems()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("زیاد",   "زیاد"),
                new KeyValuePair<string, string>("متوسط", "متوسط"),
                new KeyValuePair<string, string>("کم",     "کم")
            };
        }

        private static List<KeyValuePair<string, string>> RoleItems()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("",           "— بدون نقش —"),
                new KeyValuePair<string, string>("SuperAdmin", "مدیر کل"),
                new KeyValuePair<string, string>("Admin",      "مدیر سیستم"),
                new KeyValuePair<string, string>("Operator",   "کاربر عملیاتی"),
                new KeyValuePair<string, string>("Viewer",     "ناظر")
            };
        }

        private static TabPage NewPage(string title)
        {
            return new TabPage(title)
            {
                BackColor   = UiTheme.Background,
                RightToLeft = RightToLeft.Yes,
                Padding     = new Padding(10)
            };
        }

        private static FlowLayoutPanel NewButtonBar()
        {
            return new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft   = RightToLeft.Yes,
                WrapContents  = false
            };
        }

        private static Button MakeButton(string text, string icon, Color color, Action action)
        {
            Button button = UiTheme.CreateButton(text, icon, color);
            button.Width  = 118;
            button.Margin = new Padding(4, 2, 4, 2);
            button.Click += delegate { action(); };
            return button;
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                RowTemplate           = { Height = 28 }
            };
            UiTheme.StyleGrid(grid);
            return grid;
        }

        private static void HideIdColumn(DataGridView grid)
        {
            if (grid != null && grid.Columns.Contains("شناسه"))
                grid.Columns["شناسه"].Visible = false;
        }

        private static int SelectedId(DataGridView grid)
        {
            if (grid == null || grid.CurrentRow == null) return 0;
            if (!grid.Columns.Contains("شناسه")) return 0;
            return EntDb.ToInt(grid.CurrentRow.Cells["شناسه"].Value);
        }
    }
}

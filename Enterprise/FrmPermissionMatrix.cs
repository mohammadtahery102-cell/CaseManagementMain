using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «ماتریس مجوزها» — تعیین مجوز هر نقش و استثناهای هر کاربر.
    //
    // ستون «مدیر کل» عمداً فقط خواندنی است: اگر مجوزهای آن قابل برداشتن بود،
    // یک اشتباه می‌توانست کل سیستم را برای همیشه قفل کند.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmPermissionMatrix : Form
    {
        private DataGridView _gridMatrix, _gridUsers, _gridUserPerms;
        private Label        _lblInfo;

        public FrmPermissionMatrix()
        {
            BuildUi();
            LoadMatrix();
        }

        private void BuildUi()
        {
            Text              = "ماتریس مجوزها";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1180, 720);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
            tabs.TabPages.Add(BuildRoleTab());
            tabs.TabPages.Add(BuildUserTab());
            Controls.Add(tabs);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.PrimaryDark };
            _lblInfo = new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(0xCF, 0xDD, 0xEE),
                Font      = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopLeft,
                Padding   = new Padding(0, 0, 20, 0),
                Text      = "تیک هر خانه را بزنید تا مجوز آن نقش تغییر کند. مجوزهای «مدیر کل» قابل تغییر نیست."
            };
            header.Controls.Add(_lblInfo);
            header.Controls.Add(new Label
            {
                Text      = "ماتریس نقش و مجوز",
                Dock      = DockStyle.Top,
                Height    = 38,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            });
            Controls.Add(header);
        }

        // ─── تب نقش‌ها ─────────────────────────────────────────────────────
        private TabPage BuildRoleTab()
        {
            TabPage page = NewPage("مجوز نقش‌ها");

            _gridMatrix = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode         = DataGridViewSelectionMode.CellSelect,
                MultiSelect           = false,
                RowTemplate           = { Height = 28 }
            };
            UiTheme.StyleGrid(_gridMatrix);

            // تیک با یک کلیک اعمال شود (نه بعد از خروج از خانه).
            _gridMatrix.CurrentCellDirtyStateChanged += delegate
            {
                if (_gridMatrix.IsCurrentCellDirty)
                    _gridMatrix.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _gridMatrix.CellValueChanged += Matrix_CellValueChanged;

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_gridMatrix);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            FlowLayoutPanel buttons = NewButtonBar();
            buttons.Controls.Add(MakeButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight, delegate { LoadMatrix(); }));
            toolbar.Controls.Add(buttons);
            main.Controls.Add(toolbar);

            page.Controls.Add(main);
            return page;
        }

        private void LoadMatrix()
        {
            _gridMatrix.CellValueChanged -= Matrix_CellValueChanged;

            _gridMatrix.DataSource = PermissionService.GetRoleMatrix();

            if (_gridMatrix.Columns.Contains("کلید"))
                _gridMatrix.Columns["کلید"].ReadOnly = true;
            if (_gridMatrix.Columns.Contains("دسته"))
                _gridMatrix.Columns["دسته"].ReadOnly = true;
            if (_gridMatrix.Columns.Contains("عنوان"))
                _gridMatrix.Columns["عنوان"].ReadOnly = true;

            // ستون مدیر کل فقط خواندنی و کم‌رنگ.
            if (_gridMatrix.Columns.Contains("SuperAdmin"))
            {
                _gridMatrix.Columns["SuperAdmin"].ReadOnly = true;
                _gridMatrix.Columns["SuperAdmin"].DefaultCellStyle.BackColor = UiTheme.Background;
                _gridMatrix.Columns["SuperAdmin"].HeaderText = "مدیر کل (ثابت)";
            }

            RenameHeader("Admin",    "مدیر سیستم");
            RenameHeader("Operator", "کاربر عملیاتی");
            RenameHeader("Viewer",   "ناظر");

            _gridMatrix.CellValueChanged += Matrix_CellValueChanged;
        }

        private void RenameHeader(string columnName, string title)
        {
            if (_gridMatrix.Columns.Contains(columnName))
                _gridMatrix.Columns[columnName].HeaderText = title;
        }

        private void Matrix_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string role = _gridMatrix.Columns[e.ColumnIndex].Name;
            if (!PermissionService.GetRoles().Contains(role)) return;

            string key = Convert.ToString(_gridMatrix.Rows[e.RowIndex].Cells["کلید"].Value);
            object raw = _gridMatrix.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            bool granted = raw != null && raw != DBNull.Value && Convert.ToBoolean(raw);

            WorkflowActionResult result = PermissionService.SetRolePermission(role, key, granted);

            if (!result.Applied)
            {
                UiTheme.ShowWarning(this, result.Message);
                LoadMatrix();   // مقدار روی صفحه به وضعیت واقعی برگردانده می‌شود
            }
        }

        // ─── تب کاربران ────────────────────────────────────────────────────
        private TabPage BuildUserTab()
        {
            TabPage page = NewPage("استثنای کاربران");

            SplitContainer split = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Vertical,
                SplitterDistance = 330,
                RightToLeft      = RightToLeft.Yes
            };

            _gridUsers = CreateReadOnlyGrid();
            _gridUsers.SelectionChanged += delegate { LoadUserPermissions(); };

            Panel usersPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            usersPanel.Controls.Add(_gridUsers);
            usersPanel.Controls.Add(SectionLabel("کاربران"));
            split.Panel1.Controls.Add(usersPanel);

            _gridUserPerms = CreateReadOnlyGrid();

            Panel permsPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            permsPanel.Controls.Add(_gridUserPerms);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(6, 4, 6, 4) };
            toolbar.Controls.Add(SectionLabel("مجوزهای کاربر انتخاب‌شده"));

            FlowLayoutPanel buttons = NewButtonBar();
            buttons.Controls.Add(MakeButton("اجازه صریح", "✔", UiTheme.Success,
                delegate { SetOverride(true); }));
            buttons.Controls.Add(MakeButton("منع صریح", "✖", UiTheme.Danger,
                delegate { SetOverride(false); }));
            buttons.Controls.Add(MakeButton("حذف استثنا", "↺", UiTheme.PrimaryLight,
                delegate { SetOverride(null); }));
            toolbar.Controls.Add(buttons);
            permsPanel.Controls.Add(toolbar);

            split.Panel2.Controls.Add(permsPanel);
            page.Controls.Add(split);

            page.Enter += delegate { LoadUsers(); };
            return page;
        }

        private void LoadUsers()
        {
            _gridUsers.DataSource = PermissionService.GetUsers();

            if (_gridUsers.Columns.Contains("شناسه"))
                _gridUsers.Columns["شناسه"].Visible = false;

            LoadUserPermissions();
        }

        private void LoadUserPermissions()
        {
            int userId = SelectedUserId();

            if (userId <= 0)
            {
                _gridUserPerms.DataSource = null;
                return;
            }

            _gridUserPerms.DataSource = PermissionService.GetUserOverrides(userId, SelectedUserRole());
        }

        private void SetOverride(bool? granted)
        {
            int userId = SelectedUserId();

            if (userId <= 0)
            {
                UiTheme.ShowWarning(this, "یک کاربر را انتخاب کنید.");
                return;
            }

            if (_gridUserPerms.CurrentRow == null)
            {
                UiTheme.ShowWarning(this, "یک مجوز را انتخاب کنید.");
                return;
            }

            string key = Convert.ToString(_gridUserPerms.CurrentRow.Cells["کلید"].Value);

            WorkflowActionResult result = PermissionService.SetUserPermission(userId, key, granted);

            if (!result.Applied) UiTheme.ShowWarning(this, result.Message);

            LoadUserPermissions();
        }

        private int SelectedUserId()
        {
            if (_gridUsers == null || _gridUsers.CurrentRow == null) return 0;
            if (!_gridUsers.Columns.Contains("شناسه")) return 0;
            return EntDb.ToInt(_gridUsers.CurrentRow.Cells["شناسه"].Value);
        }

        private string SelectedUserRole()
        {
            if (_gridUsers == null || _gridUsers.CurrentRow == null) return "";
            if (!_gridUsers.Columns.Contains("نقش")) return "";
            return Convert.ToString(_gridUsers.CurrentRow.Cells["نقش"].Value);
        }

        // ─── کمکی‌ها ───────────────────────────────────────────────────────
        private static TabPage NewPage(string title)
        {
            return new TabPage(title)
            {
                BackColor   = UiTheme.Background,
                RightToLeft = RightToLeft.Yes,
                Padding     = new Padding(10)
            };
        }

        private static Label SectionLabel(string text)
        {
            return new Label
            {
                Dock      = DockStyle.Top,
                Height    = 28,
                Text      = "  " + text,
                Font      = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
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
            button.Width  = 124;
            button.Margin = new Padding(4, 2, 4, 2);
            button.Click += delegate { action(); };
            return button;
        }

        private static DataGridView CreateReadOnlyGrid()
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
    }
}

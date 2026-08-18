using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «مدیریت ماژول‌ها» — فعال/غیرفعال کردن بخش‌های نرم‌افزار به‌صورت سراسری،
    // برای هر نقش و برای هر کاربر.
    //
    // ماژول‌های «پایه» (کاربران، مجوزها، ماژول‌ها، تنظیمات) عمداً قابل خاموش
    // کردن نیستند تا هرگز راه بازگشت به مدیریت سیستم بسته نشود.
    //
    // تغییرات پس از باز شدن دوباره برنامه در منو دیده می‌شوند (منوی کناری یک
    // بار هنگام ساخت داشبورد ساخته می‌شود) — همین نکته به کاربر گفته می‌شود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmModules : Form
    {
        private DataGridView _gridGlobal, _gridRoleModules, _gridUsers, _gridUserModules;
        private ComboBox     _cmbRole;

        public FrmModules()
        {
            BuildUi();
            LoadGlobal();
        }

        private void BuildUi()
        {
            Text              = "مدیریت ماژول‌ها";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1150, 700);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
            tabs.TabPages.Add(BuildGlobalTab());
            tabs.TabPages.Add(BuildRoleTab());
            tabs.TabPages.Add(BuildUserTab());
            Controls.Add(tabs);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.PrimaryDark };
            header.Controls.Add(new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(0xCF, 0xDD, 0xEE),
                Font      = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopLeft,
                Padding   = new Padding(0, 0, 20, 0),
                Text      = "تغییرات پس از باز کردن دوباره برنامه در منوی کناری اعمال می‌شود. ماژول‌های پایه قابل خاموش کردن نیستند."
            });
            header.Controls.Add(new Label
            {
                Text      = "مدیریت ماژول‌ها",
                Dock      = DockStyle.Top,
                Height    = 38,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            });
            Controls.Add(header);
        }

        // ─── تب سراسری ─────────────────────────────────────────────────────
        private TabPage BuildGlobalTab()
        {
            TabPage page = NewPage("وضعیت سراسری");

            _gridGlobal = CreateGrid();

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_gridGlobal);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            FlowLayoutPanel buttons = NewButtonBar();
            buttons.Controls.Add(MakeButton("فعال کردن", "✔", UiTheme.Success,
                delegate { SetGlobal(true); }));
            buttons.Controls.Add(MakeButton("غیرفعال کردن", "✖", UiTheme.Danger,
                delegate { SetGlobal(false); }));
            buttons.Controls.Add(MakeButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight,
                delegate { LoadGlobal(); }));
            toolbar.Controls.Add(buttons);
            main.Controls.Add(toolbar);

            page.Controls.Add(main);
            return page;
        }

        private void LoadGlobal()
        {
            _gridGlobal.DataSource = ModuleService.GetModules();
            HideId(_gridGlobal);
        }

        private void SetGlobal(bool enabled)
        {
            string key = SelectedKey(_gridGlobal);
            if (key == null) return;

            Apply(ModuleService.SetGlobal(key, enabled));
            LoadGlobal();
        }

        // ─── تب نقش‌ها ─────────────────────────────────────────────────────
        private TabPage BuildRoleTab()
        {
            TabPage page = NewPage("به تفکیک نقش");

            _gridRoleModules = CreateGrid();

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_gridRoleModules);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };

            _cmbRole = new ComboBox
            {
                Dock          = DockStyle.Right,
                Width         = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                RightToLeft   = RightToLeft.Yes
            };
            foreach (string role in PermissionService.GetRoles())
                _cmbRole.Items.Add(role);
            _cmbRole.SelectedIndex = 1;   // Admin
            _cmbRole.SelectedIndexChanged += delegate { LoadRoleModules(); };

            FlowLayoutPanel buttons = NewButtonBar();
            buttons.Controls.Add(MakeButton("فعال", "✔", UiTheme.Success,
                delegate { SetForRole(true); }));
            buttons.Controls.Add(MakeButton("غیرفعال", "✖", UiTheme.Danger,
                delegate { SetForRole(false); }));
            buttons.Controls.Add(MakeButton("پیش‌فرض", "↺", UiTheme.PrimaryLight,
                delegate { SetForRole(null); }));

            toolbar.Controls.Add(buttons);
            toolbar.Controls.Add(_cmbRole);
            main.Controls.Add(toolbar);

            page.Controls.Add(main);
            page.Enter += delegate { LoadRoleModules(); };
            return page;
        }

        private void LoadRoleModules()
        {
            _gridRoleModules.DataSource = ModuleService.GetRoleModules(SelectedRole());
            HideId(_gridRoleModules);
        }

        private void SetForRole(bool? enabled)
        {
            string key = SelectedKey(_gridRoleModules);
            if (key == null) return;

            Apply(ModuleService.SetForRole(SelectedRole(), key, enabled));
            LoadRoleModules();
        }

        private string SelectedRole()
        {
            return _cmbRole == null || _cmbRole.SelectedItem == null
                ? "" : Convert.ToString(_cmbRole.SelectedItem);
        }

        // ─── تب کاربران ────────────────────────────────────────────────────
        private TabPage BuildUserTab()
        {
            TabPage page = NewPage("به تفکیک کاربر");

            SplitContainer split = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Vertical,
                SplitterDistance = 320,
                RightToLeft      = RightToLeft.Yes
            };

            _gridUsers = CreateGrid();
            _gridUsers.SelectionChanged += delegate { LoadUserModules(); };

            Panel usersPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            usersPanel.Controls.Add(_gridUsers);
            usersPanel.Controls.Add(SectionLabel("کاربران"));
            split.Panel1.Controls.Add(usersPanel);

            _gridUserModules = CreateGrid();

            Panel modulesPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            modulesPanel.Controls.Add(_gridUserModules);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(6, 4, 6, 4) };
            toolbar.Controls.Add(SectionLabel("ماژول‌های کاربر انتخاب‌شده"));

            FlowLayoutPanel buttons = NewButtonBar();
            buttons.Controls.Add(MakeButton("فعال", "✔", UiTheme.Success,
                delegate { SetForUser(true); }));
            buttons.Controls.Add(MakeButton("غیرفعال", "✖", UiTheme.Danger,
                delegate { SetForUser(false); }));
            buttons.Controls.Add(MakeButton("حذف استثنا", "↺", UiTheme.PrimaryLight,
                delegate { SetForUser(null); }));
            toolbar.Controls.Add(buttons);
            modulesPanel.Controls.Add(toolbar);

            split.Panel2.Controls.Add(modulesPanel);
            page.Controls.Add(split);

            page.Enter += delegate { LoadUsers(); };
            return page;
        }

        private void LoadUsers()
        {
            _gridUsers.DataSource = PermissionService.GetUsers();
            HideId(_gridUsers);
            LoadUserModules();
        }

        private void LoadUserModules()
        {
            int userId = SelectedUserId();

            _gridUserModules.DataSource = userId <= 0
                ? null
                : ModuleService.GetUserModules(userId, SelectedUserRole());

            HideId(_gridUserModules);
        }

        private void SetForUser(bool? enabled)
        {
            int userId = SelectedUserId();

            if (userId <= 0)
            {
                UiTheme.ShowWarning(this, "یک کاربر را انتخاب کنید.");
                return;
            }

            string key = SelectedKey(_gridUserModules);
            if (key == null) return;

            Apply(ModuleService.SetForUser(userId, key, enabled));
            LoadUserModules();
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
        private string SelectedKey(DataGridView grid)
        {
            if (grid == null || grid.CurrentRow == null || !grid.Columns.Contains("کلید"))
            {
                UiTheme.ShowWarning(this, "یک ماژول را انتخاب کنید.");
                return null;
            }

            return Convert.ToString(grid.CurrentRow.Cells["کلید"].Value);
        }

        private void Apply(WorkflowActionResult result)
        {
            if (result == null) return;

            if (result.Applied) UiTheme.ShowSuccess(this, result.Message);
            else                UiTheme.ShowWarning(this, result.Message);
        }

        private static void HideId(DataGridView grid)
        {
            if (grid != null && grid.Columns.Contains("شناسه"))
                grid.Columns["شناسه"].Visible = false;
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
    }
}

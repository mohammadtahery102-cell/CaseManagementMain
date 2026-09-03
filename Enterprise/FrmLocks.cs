using System;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «قفل رکوردها» — نمایش قفل‌های فعال و آزادسازی اجباری قفل جامانده.
    //
    // آزادسازی اجباری فقط برای مدیر سیستم مجاز است و همیشه در رویدادها ثبت
    // می‌شود، چون می‌تواند باعث از دست رفتن کار کاربر دیگر شود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmLocks : Form
    {
        private DataGridView _grid;
        private Label        _lblInfo;

        public FrmLocks()
        {
            BuildUi();
            LoadLocks();
        }

        private void BuildUi()
        {
            Text              = "قفل رکوردها";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1000, 600);

            _grid = new DataGridView
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
            UiTheme.StyleGrid(_grid);

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_grid);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft   = RightToLeft.Yes,
                WrapContents  = false
            };
            buttons.Controls.Add(MakeButton("آزادسازی اجباری", "🔓", UiTheme.Danger,       ForceRelease));
            buttons.Controls.Add(MakeButton("پاک‌سازی منقضی‌ها", "🧹", UiTheme.Warning,     PurgeExpired));
            buttons.Controls.Add(MakeButton("تازه‌سازی",        "⟳", UiTheme.PrimaryLight, delegate { LoadLocks(); }));
            toolbar.Controls.Add(buttons);
            main.Controls.Add(toolbar);

            Controls.Add(main);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.PrimaryDark };
            _lblInfo = new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(0xCF, 0xDD, 0xEE),
                Font      = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopLeft,
                Padding   = new Padding(0, 0, 20, 0)
            };
            header.Controls.Add(_lblInfo);
            header.Controls.Add(new Label
            {
                Text      = "قفل رکوردها",
                Dock      = DockStyle.Top,
                Height    = 38,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            });
            Controls.Add(header);
        }

        private void LoadLocks()
        {
            _grid.DataSource = LockService.GetActiveLocks();

            if (_grid.Columns.Contains("شناسه"))
                _grid.Columns["شناسه"].Visible = false;

            _lblInfo.Text = "قفل‌های فعال: " + _grid.Rows.Count +
                            "   |   مدت اعتبار هر قفل: " + LockService.LockMinutes + " دقیقه";
        }

        private void ForceRelease()
        {
            int lockId = SelectedId();

            if (lockId <= 0)
            {
                UiTheme.ShowWarning(this, "یک قفل را انتخاب کنید.");
                return;
            }

            if (!UiTheme.ShowConfirm(this,
                    "قفل انتخاب‌شده آزاد شود؟ اگر کاربر دیگری در حال ویرایش باشد، ممکن است کارش از بین برود.",
                    "آزادسازی اجباری قفل"))
                return;

            WorkflowActionResult result = LockService.ForceRelease(lockId);

            if (result.Applied) UiTheme.ShowSuccess(this, result.Message);
            else                UiTheme.ShowWarning(this, result.Message);

            LoadLocks();
        }

        private void PurgeExpired()
        {
            int removed = LockService.PurgeExpired();
            UiTheme.ShowSuccess(this, removed + " قفل منقضی پاک شد.");
            LoadLocks();
        }

        private int SelectedId()
        {
            if (_grid.CurrentRow == null) return 0;
            if (!_grid.Columns.Contains("شناسه")) return 0;
            return EntDb.ToInt(_grid.CurrentRow.Cells["شناسه"].Value);
        }

        private static Button MakeButton(string text, string icon, Color color, Action action)
        {
            Button button = UiTheme.CreateButton(text, icon, color);
            button.Width  = 150;
            button.Margin = new Padding(4, 2, 4, 2);
            button.Click += delegate { action(); };
            return button;
        }
    }
}

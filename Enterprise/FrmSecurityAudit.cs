using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «ممیزی امنیتی» — مشاهده رویدادهای امنیتی با فیلتر.
    //
    // فقط خواندنی است. دسترسی به آن محدود به مدیر سیستم است، چون فهرست
    // تلاش‌های ورود و نام کاربران، خودش اطلاعات حساسی است.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmSecurityAudit : Form
    {
        private DataGridView _grid;
        private ComboBox     _cmbType, _cmbSeverity, _cmbDays;
        private TextBox      _txtUser;
        private Label        _lblInfo;

        public FrmSecurityAudit()
        {
            BuildUi();
            LoadEvents();
        }

        private void BuildUi()
        {
            Text              = "ممیزی امنیتی";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1180, 680);

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

            // رویدادهای ناموفق/بحرانی رنگی می‌شوند تا در یک نگاه دیده شوند.
            _grid.CellFormatting += Grid_CellFormatting;

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_grid);
            main.Controls.Add(BuildFilterBar());
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
                Text      = "ممیزی امنیتی",
                Dock      = DockStyle.Top,
                Height    = 38,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            });
            Controls.Add(header);
        }

        private Panel BuildFilterBar()
        {
            Panel bar = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(6, 10, 6, 8) };

            _cmbType = NewCombo(170);
            _cmbType.Items.AddRange(new object[]
            {
                "همه رویدادها",
                SecurityAudit.EventLogin, SecurityAudit.EventLoginFailed, SecurityAudit.EventLogout,
                SecurityAudit.EventPermissionDenied, SecurityAudit.EventPasswordChanged,
                SecurityAudit.EventUserChanged, SecurityAudit.EventLockOverride,
                SecurityAudit.EventSensitive
            });
            _cmbType.SelectedIndex = 0;
            _cmbType.SelectedIndexChanged += delegate { LoadEvents(); };

            _cmbSeverity = NewCombo(120);
            _cmbSeverity.Items.AddRange(new object[]
            {
                "همه سطوح", SecurityAudit.SeverityInfo,
                SecurityAudit.SeverityWarning, SecurityAudit.SeverityCritical
            });
            _cmbSeverity.SelectedIndex = 0;
            _cmbSeverity.SelectedIndexChanged += delegate { LoadEvents(); };

            _cmbDays = NewCombo(120);
            _cmbDays.Items.AddRange(new object[] { "۷ روز اخیر", "۳۰ روز اخیر", "۹۰ روز اخیر", "همه" });
            _cmbDays.SelectedIndex = 1;
            _cmbDays.SelectedIndexChanged += delegate { LoadEvents(); };

            _txtUser = new TextBox { Dock = DockStyle.Right, Width = 140, RightToLeft = RightToLeft.Yes };
            UiTheme.StyleTextBox(_txtUser);
            _txtUser.TextChanged += delegate { LoadEvents(); };

            Button refresh = UiTheme.CreateButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight);
            refresh.Width = 118;
            refresh.Dock  = DockStyle.Left;
            refresh.Click += delegate { LoadEvents(); };

            bar.Controls.Add(refresh);
            bar.Controls.Add(_txtUser);
            bar.Controls.Add(NewLabel("کاربر:", 60));
            bar.Controls.Add(_cmbDays);
            bar.Controls.Add(NewLabel("بازه:", 50));
            bar.Controls.Add(_cmbSeverity);
            bar.Controls.Add(NewLabel("اهمیت:", 60));
            bar.Controls.Add(_cmbType);
            bar.Controls.Add(NewLabel("رویداد:", 60));

            return bar;
        }

        private void LoadEvents()
        {
            string type = _cmbType.SelectedIndex <= 0 ? "" : Convert.ToString(_cmbType.SelectedItem);
            string severity = _cmbSeverity.SelectedIndex <= 0 ? "" : Convert.ToString(_cmbSeverity.SelectedItem);

            int days;
            switch (_cmbDays.SelectedIndex)
            {
                case 0:  days = 7;   break;
                case 1:  days = 30;  break;
                case 2:  days = 90;  break;
                default: days = 0;   break;
            }

            _grid.DataSource = SecurityAudit.GetEvents(type, severity, _txtUser.Text.Trim(), days);

            if (_grid.Columns.Contains("شناسه"))
                _grid.Columns["شناسه"].Visible = false;

            _lblInfo.Text = "رویدادهای نمایش‌داده‌شده: " + _grid.Rows.Count +
                            "   |   این پنجره فقط خواندنی است.";
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;

            DataGridViewRow row = _grid.Rows[e.RowIndex];

            string severity = CellText(row, "اهمیت");
            string result   = CellText(row, "نتیجه");

            if (severity == SecurityAudit.SeverityCritical)
            {
                row.DefaultCellStyle.BackColor = UiTheme.DangerLight;
                row.DefaultCellStyle.ForeColor = UiTheme.Danger;
            }
            else if (result == "ناموفق" || severity == SecurityAudit.SeverityWarning)
            {
                row.DefaultCellStyle.BackColor = UiTheme.WarningLight;
            }
        }

        private static string CellText(DataGridViewRow row, string columnName)
        {
            if (!row.DataGridView.Columns.Contains(columnName)) return "";
            object value = row.Cells[columnName].Value;
            return value == null ? "" : Convert.ToString(value);
        }

        private static ComboBox NewCombo(int width)
        {
            return new ComboBox
            {
                Dock          = DockStyle.Right,
                Width         = width,
                DropDownStyle = ComboBoxStyle.DropDownList,
                RightToLeft   = RightToLeft.Yes
            };
        }

        private static Label NewLabel(string text, int width)
        {
            return new Label
            {
                Dock      = DockStyle.Right,
                Width     = width,
                Text      = text,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextDark
            };
        }
    }
}

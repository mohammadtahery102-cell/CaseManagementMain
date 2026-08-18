using System;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «گزارش خطاها» — فهرست خطاهای ثبت‌شده، جزئیات فنی و علامت‌گذاری بررسی‌شده.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmErrorLog : Form
    {
        private DataGridView _grid;
        private TextBox      _txtStack;
        private ComboBox     _cmbSeverity, _cmbDays;
        private CheckBox     _chkUnresolved;
        private Label        _lblInfo;

        public FrmErrorLog()
        {
            BuildUi();
            LoadErrors();
        }

        private void BuildUi()
        {
            Text              = "گزارش خطاها";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1180, 700);

            // ── جزئیات فنی ──
            _txtStack = new TextBox
            {
                Dock        = DockStyle.Fill,
                Multiline   = true,
                ReadOnly    = true,
                ScrollBars  = ScrollBars.Both,
                WordWrap    = false,
                RightToLeft = RightToLeft.No,   // متن استثنا انگلیسی است
                Font        = new Font(FontFamily.GenericMonospace, 9F),
                BackColor   = Color.White
            };

            Panel stackPanel = new Panel { Dock = DockStyle.Bottom, Height = 220, BackColor = UiTheme.CardBack };
            stackPanel.Controls.Add(_txtStack);
            stackPanel.Controls.Add(new Label
            {
                Dock      = DockStyle.Top,
                Height    = 28,
                Text      = "  جزئیات فنی خطای انتخاب‌شده",
                Font      = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            });

            // ── فهرست ──
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
            _grid.SelectionChanged += delegate { LoadStack(); };
            _grid.CellFormatting += Grid_CellFormatting;

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_grid);
            main.Controls.Add(BuildToolbar());

            Controls.Add(main);
            Controls.Add(stackPanel);

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
                Text      = "گزارش خطاها",
                Dock      = DockStyle.Top,
                Height    = 38,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            });
            Controls.Add(header);
        }

        private Panel BuildToolbar()
        {
            Panel bar = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(6, 10, 6, 8) };

            _cmbSeverity = new ComboBox
            {
                Dock          = DockStyle.Right,
                Width         = 130,
                DropDownStyle = ComboBoxStyle.DropDownList,
                RightToLeft   = RightToLeft.Yes
            };
            _cmbSeverity.Items.AddRange(new object[]
            {
                "همه سطوح", ErrorLogger.SeverityCritical,
                ErrorLogger.SeverityError, ErrorLogger.SeverityWarning
            });
            _cmbSeverity.SelectedIndex = 0;
            _cmbSeverity.SelectedIndexChanged += delegate { LoadErrors(); };

            _cmbDays = new ComboBox
            {
                Dock          = DockStyle.Right,
                Width         = 130,
                DropDownStyle = ComboBoxStyle.DropDownList,
                RightToLeft   = RightToLeft.Yes
            };
            _cmbDays.Items.AddRange(new object[] { "۷ روز اخیر", "۳۰ روز اخیر", "همه" });
            _cmbDays.SelectedIndex = 1;
            _cmbDays.SelectedIndexChanged += delegate { LoadErrors(); };

            _chkUnresolved = new CheckBox
            {
                Dock        = DockStyle.Right,
                Width       = 150,
                Text        = "فقط بررسی‌نشده‌ها",
                Checked     = true,
                RightToLeft = RightToLeft.Yes
            };
            _chkUnresolved.CheckedChanged += delegate { LoadErrors(); };

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock          = DockStyle.Left,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft   = RightToLeft.Yes,
                WrapContents  = false,
                AutoSize      = true
            };
            buttons.Controls.Add(MakeButton("بررسی شد", "✔", UiTheme.Success,      MarkResolved));
            buttons.Controls.Add(MakeButton("کپی جزئیات", "⧉", UiTheme.PrimaryLight, CopyDetails));
            buttons.Controls.Add(MakeButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight, delegate { LoadErrors(); }));

            bar.Controls.Add(buttons);
            bar.Controls.Add(_chkUnresolved);
            bar.Controls.Add(_cmbDays);
            bar.Controls.Add(_cmbSeverity);
            return bar;
        }

        private void LoadErrors()
        {
            string severity = _cmbSeverity.SelectedIndex <= 0
                ? "" : Convert.ToString(_cmbSeverity.SelectedItem);

            int days;
            switch (_cmbDays.SelectedIndex)
            {
                case 0:  days = 7;  break;
                case 1:  days = 30; break;
                default: days = 0;  break;
            }

            _grid.DataSource = ErrorLogger.GetErrors(severity, _chkUnresolved.Checked, days);

            if (_grid.Columns.Contains("شناسه"))
                _grid.Columns["شناسه"].Visible = false;

            _lblInfo.Text = "نمایش‌داده‌شده: " + _grid.Rows.Count +
                            "   |   کل خطاهای بررسی‌نشده: " + ErrorLogger.UnresolvedCount();

            LoadStack();
        }

        private void LoadStack()
        {
            int errorId = SelectedId();
            _txtStack.Text = errorId <= 0 ? "" : ErrorLogger.GetStackTrace(errorId);
        }

        private void MarkResolved()
        {
            int errorId = SelectedId();

            if (errorId <= 0)
            {
                UiTheme.ShowWarning(this, "یک خطا را انتخاب کنید.");
                return;
            }

            string note = EntPrompt.AskText(this, "بررسی خطا", "یادداشت (اختیاری)", "");
            if (note == null) return;

            WorkflowActionResult result = ErrorLogger.MarkResolved(errorId, note);

            if (result.Applied) UiTheme.ShowSuccess(this, result.Message);
            else                UiTheme.ShowWarning(this, result.Message);

            LoadErrors();
        }

        private void CopyDetails()
        {
            if (string.IsNullOrEmpty(_txtStack.Text))
            {
                UiTheme.ShowWarning(this, "جزئیاتی برای کپی وجود ندارد.");
                return;
            }

            try
            {
                Clipboard.SetText(_txtStack.Text);
                UiTheme.ShowSuccess(this, "جزئیات در حافظه کپی شد.");
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "کپی انجام نشد: " + ex.Message);
            }
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;

            DataGridViewRow row = _grid.Rows[e.RowIndex];

            if (!_grid.Columns.Contains("اهمیت")) return;

            object value = row.Cells["اهمیت"].Value;
            string severity = value == null ? "" : Convert.ToString(value);

            if (severity == ErrorLogger.SeverityCritical)
            {
                row.DefaultCellStyle.BackColor = UiTheme.DangerLight;
                row.DefaultCellStyle.ForeColor = UiTheme.Danger;
            }
            else if (severity == ErrorLogger.SeverityWarning)
            {
                row.DefaultCellStyle.BackColor = UiTheme.WarningLight;
            }
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
            button.Width  = 124;
            button.Margin = new Padding(4, 0, 4, 0);
            button.Click += delegate { action(); };
            return button;
        }
    }
}

using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «تاریخچه نسخه‌ها» — نسخه‌های یک رکورد (یا آخرین تغییرات همه رکوردها)،
    // محتوای هر نسخه و مقایسه آن با نسخه قبل.
    //
    // این پنجره فقط می‌خواند؛ هیچ داده‌ای را تغییر یا بازگردانی نمی‌کند
    // (دلیل در VersionService توضیح داده شده است).
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmVersions : Form
    {
        private readonly string _entityName;
        private readonly int    _entityId;

        private DataGridView _gridVersions, _gridDetail;
        private RadioButton  _radDiff, _radFull;
        private Label        _lblInfo;

        // نمایش تاریخچه یک رکورد مشخص.
        public FrmVersions(string entityName, int entityId)
        {
            _entityName = entityName ?? "";
            _entityId   = entityId;

            BuildUi();
            LoadVersions();
        }

        // نمایش آخرین تغییرات همه رکوردها.
        public FrmVersions() : this("", 0)
        {
        }

        private void BuildUi()
        {
            Text              = "تاریخچه نسخه‌ها";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1080, 660);

            // ── جزئیات نسخه ──
            _gridDetail = CreateGrid();

            Panel detailPanel = new Panel { Dock = DockStyle.Bottom, Height = 260, BackColor = UiTheme.CardBack };
            detailPanel.Controls.Add(_gridDetail);

            Panel detailBar = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(6, 4, 6, 4) };

            _radDiff = new RadioButton
            {
                Text        = "فقط تغییرات نسبت به نسخه قبل",
                Dock        = DockStyle.Right,
                Width       = 230,
                Checked     = true,
                RightToLeft = RightToLeft.Yes
            };
            _radFull = new RadioButton
            {
                Text        = "محتوای کامل نسخه",
                Dock        = DockStyle.Right,
                Width       = 170,
                RightToLeft = RightToLeft.Yes
            };
            _radDiff.CheckedChanged += delegate { LoadDetail(); };

            detailBar.Controls.Add(_radFull);
            detailBar.Controls.Add(_radDiff);
            detailPanel.Controls.Add(detailBar);

            // ── فهرست نسخه‌ها ──
            _gridVersions = CreateGrid();
            _gridVersions.SelectionChanged += delegate { LoadDetail(); };

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_gridVersions);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft   = RightToLeft.Yes,
                WrapContents  = false
            };
            Button refresh = UiTheme.CreateButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight);
            refresh.Width  = 118;
            refresh.Margin = new Padding(4, 2, 4, 2);
            refresh.Click += delegate { LoadVersions(); };
            buttons.Controls.Add(refresh);
            toolbar.Controls.Add(buttons);
            main.Controls.Add(toolbar);

            Controls.Add(main);
            Controls.Add(detailPanel);

            // ── سربرگ ──
            Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.PrimaryDark };
            _lblInfo = new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(0xCF, 0xDD, 0xEE),
                Font      = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopLeft,
                Padding   = new Padding(0, 0, 20, 0),
                Text      = "این پنجره فقط نمایش می‌دهد و هیچ داده‌ای را تغییر نمی‌دهد."
            };
            header.Controls.Add(_lblInfo);
            header.Controls.Add(new Label
            {
                Text      = _entityId > 0
                                ? "تاریخچه نسخه‌ها — " + _entityName + " #" + _entityId
                                : "تاریخچه نسخه‌ها",
                Dock      = DockStyle.Top,
                Height    = 38,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            });
            Controls.Add(header);
        }

        private void LoadVersions()
        {
            _gridVersions.DataSource = _entityId > 0
                ? VersionService.GetVersions(_entityName, _entityId)
                : VersionService.GetRecent("", 500);

            if (_gridVersions.Columns.Contains("شناسه"))
                _gridVersions.Columns["شناسه"].Visible = false;

            _lblInfo.Text = "تعداد نسخه‌های نمایش‌داده‌شده: " + _gridVersions.Rows.Count +
                            "   |   این پنجره هیچ داده‌ای را تغییر نمی‌دهد.";

            LoadDetail();
        }

        private void LoadDetail()
        {
            int versionId = SelectedId();

            if (versionId <= 0)
            {
                _gridDetail.DataSource = null;
                return;
            }

            _gridDetail.DataSource = _radDiff.Checked
                ? VersionService.GetDiffTable(versionId)
                : VersionService.GetSnapshotTable(versionId);
        }

        private int SelectedId()
        {
            if (_gridVersions.CurrentRow == null) return 0;
            if (!_gridVersions.Columns.Contains("شناسه")) return 0;
            return EntDb.ToInt(_gridVersions.CurrentRow.Cells["شناسه"].Value);
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

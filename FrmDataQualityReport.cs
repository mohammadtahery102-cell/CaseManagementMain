using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement
{
    // ═════════════════════════════════════════════════════════════════════════
    // «گزارش کیفیت داده» — کاملاً فقط‌خواندنی، هم‌سبک با FrmDuplicates.
    //
    // داده‌ی گمشده/فرمت نامعتبر/ناسازگاری را نشان می‌دهد. برای «تکراری‌ها»
    // به‌جای بازسازیِ همان منطق، مستقیماً از DuplicateDetector موجود شمارش
    // می‌گیرد و دکمه‌ای برای بازکردنِ FrmDuplicates (بررسیِ کامل) می‌گذارد.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmDataQualityReport : Form
    {
        private readonly DataTable _table = new DataTable();

        private DataGridView _grid;
        private Label _lblSummary;
        private Button _btnRun, _btnOpenCase, _btnOpenDuplicates, _btnExcel;
        private ProgressBar _progress;

        private System.Collections.Generic.List<DataQualityIssue> _issues =
            new System.Collections.Generic.List<DataQualityIssue>();

        private int _duplicateCount = -1;

        public FrmDataQualityReport()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "گزارش کیفیت داده";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1100, 680);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowTemplate = { Height = 30 }
            };
            UiTheme.StyleGrid(_grid);
            _grid.CellDoubleClick += delegate { OpenCase(); };

            Panel gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 6) };
            gridWrap.Controls.Add(_grid);
            Controls.Add(gridWrap);

            Controls.Add(BuildToolbar());

            Panel header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = UiTheme.PrimaryDark };
            var title = new Label
            {
                Text = "گزارش کیفیت داده",
                Dock = DockStyle.Top, Height = 40, ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 6, 20, 0)
            };
            _lblSummary = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Color.FromArgb(0xCF, 0xDD, 0xEE),
                Font = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 0, 20, 0),
                Text = "برای شروع، «اجرای بررسی» را بزنید. هیچ داده‌ای تغییر نمی‌کند."
            };
            header.Controls.Add(_lblSummary);
            header.Controls.Add(title);
            Controls.Add(header);
        }

        private Control BuildToolbar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.CardBack };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, Padding = new Padding(12, 8, 12, 8)
            };

            _btnRun = UiTheme.CreateButton("اجرای بررسی", "⌕", UiTheme.Primary);
            _btnRun.Size = new Size(150, 34); _btnRun.Margin = new Padding(0, 0, 8, 0);
            _btnRun.Click += async delegate { await RunCheck(); };
            flow.Controls.Add(_btnRun);

            _btnOpenCase = UiTheme.CreateSecondaryButton("باز کردن پرونده", "▤");
            _btnOpenCase.Size = new Size(150, 34); _btnOpenCase.Margin = new Padding(6, 0, 4, 0);
            _btnOpenCase.Click += delegate { OpenCase(); };
            flow.Controls.Add(_btnOpenCase);

            _btnOpenDuplicates = UiTheme.CreateSecondaryButton("پرونده‌های تکراری", "⧉");
            _btnOpenDuplicates.Size = new Size(160, 34); _btnOpenDuplicates.Margin = new Padding(4, 0, 4, 0);
            _btnOpenDuplicates.Click += delegate
            {
                var frm = new FrmDuplicates();
                frm.ShowDialog(this);
            };
            flow.Controls.Add(_btnOpenDuplicates);

            _btnExcel = UiTheme.CreateSecondaryButton("خروجی Excel", "⇑");
            _btnExcel.Size = new Size(140, 34); _btnExcel.Margin = new Padding(4, 0, 4, 0);
            _btnExcel.Click += delegate { ExportExcel(); };
            flow.Controls.Add(_btnExcel);

            _progress = new ProgressBar
            {
                Width = 170, Height = 20,
                Margin = new Padding(10, 8, 0, 0), Visible = false, Style = ProgressBarStyle.Marquee
            };
            flow.Controls.Add(_progress);

            bar.Controls.Add(flow);
            return bar;
        }

        private async Task RunCheck()
        {
            _btnRun.Enabled = false;
            _progress.Visible = true;
            _lblSummary.Text = "در حال بررسی...";

            try
            {
                _issues = await Task.Run(() => new DataQualityChecker(new DatabaseHelper()).Check());
                _duplicateCount = await Task.Run(() =>
                    new DuplicateDetector(new DatabaseHelper())
                        .Detect(null, CancellationToken.None).Count);

                FillTable();

                int missing = _issues.Count(i => i.IssueType == DataQualityIssueType.Missing);
                int invalid = _issues.Count(i => i.IssueType == DataQualityIssueType.InvalidFormat);
                int inconsistent = _issues.Count(i => i.IssueType == DataQualityIssueType.Inconsistent);

                _lblSummary.Text =
                    "داده گمشده: " + missing.ToString("N0") +
                    "   ·   فرمت نامعتبر: " + invalid.ToString("N0") +
                    "   ·   ناسازگاری: " + inconsistent.ToString("N0") +
                    "   ·   جفت‌های تکراری: " + _duplicateCount.ToString("N0") +
                    "   ·   هیچ داده‌ای تغییر نکرده است.";
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "بررسی کیفیت داده ممکن نشد: " + ex.Message);
                _lblSummary.Text = "بررسی ناموفق بود.";
            }
            finally
            {
                _progress.Visible = false;
                _btnRun.Enabled = true;
            }
        }

        private void FillTable()
        {
            _table.Clear();
            if (_table.Columns.Count == 0)
            {
                _table.Columns.Add("نوع مشکل");
                _table.Columns.Add("کد پرونده");
                _table.Columns.Add("نام سرپرست");
                _table.Columns.Add("توضیح");
                _table.Columns.Add("CasId", typeof(int));
            }

            foreach (DataQualityIssue i in _issues)
                _table.Rows.Add(i.IssueTypeText, i.Code, i.HeadFullName, i.Description, i.CasId);

            var view = new DataView(_table) { Sort = "[نوع مشکل]" };
            _grid.DataSource = view;

            if (_grid.Columns.Contains("CasId")) _grid.Columns["CasId"].Visible = false;
            if (_grid.Columns.Contains("نوع مشکل")) _grid.Columns["نوع مشکل"].FillWeight = 60;
            if (_grid.Columns.Contains("کد پرونده")) _grid.Columns["کد پرونده"].FillWeight = 50;
            if (_grid.Columns.Contains("نام سرپرست")) _grid.Columns["نام سرپرست"].FillWeight = 80;
            if (_grid.Columns.Contains("توضیح")) _grid.Columns["توضیح"].FillWeight = 160;
        }

        private void OpenCase()
        {
            if (_grid.CurrentRow == null)
            {
                UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید.");
                return;
            }

            object val = _grid.CurrentRow.Cells["CasId"].Value;
            if (val == null) return;

            try
            {
                var frm = new FrmCase(Convert.ToInt32(val));
                frm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "باز کردن پرونده ممکن نشد: " + ex.Message);
            }
        }

        private void ExportExcel()
        {
            if (_grid.DataSource == null || _grid.Rows.Count == 0)
            {
                UiTheme.ShowWarning(this, "داده‌ای برای خروجی وجود ندارد.");
                return;
            }

            try
            {
                string root = FileHelper.GetOrChooseBaseRootFolder();
                if (string.IsNullOrWhiteSpace(root))
                {
                    UiTheme.ShowWarning(this, "محل ذخیره فایل‌ها مشخص نیست.");
                    return;
                }

                string folder = Path.Combine(root, "Reports");
                Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, "کیفیت_داده_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss",
                        System.Globalization.CultureInfo.InvariantCulture) + ".xlsx");

                DataTable export = ((DataView)_grid.DataSource).ToTable();
                if (export.Columns.Contains("CasId")) export.Columns.Remove("CasId");

                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add(export, "کیفیت داده");
                    sheet.RightToLeft = true;
                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(path);
                }

                UiTheme.ShowSuccess(this, "خروجی ساخته شد:" + Environment.NewLine + path);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ساخت خروجی ممکن نشد: " + ex.Message);
            }
        }
    }
}

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
    // «پرونده‌های تکراری» — تشخیص و بررسی، بدون هیچ تغییری در داده.
    //
    // این پنجره فقط نشان می‌دهد و راهنمایی می‌کند؛ هیچ پرونده‌ای را ادغام یا
    // حذف نمی‌کند. تصمیم با کاربر است و برای اصلاح، پرونده در فرمِ خودش باز
    // می‌شود. (ادغامِ خودکار عمداً پیاده نشده: برگشت‌ناپذیر است و می‌تواند
    // داده‌ی درست را از بین ببرد.)
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmDuplicates : Form
    {
        private readonly DataTable _table = new DataTable();

        private DataGridView _grid;
        private ComboBox _cmbConfidence;
        private TextBox _txtSearch;
        private Label _lblSummary;
        private Button _btnScan, _btnOpenA, _btnOpenB, _btnExcel;
        private ProgressBar _progress;
        private Panel _comparePanel;
        private Label _lblCompare;

        private System.Collections.Generic.List<DuplicateMatch> _matches =
            new System.Collections.Generic.List<DuplicateMatch>();

        public FrmDuplicates()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "پرونده‌های تکراری";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1240, 720);

            // ترتیبِ افزودن از پایین به بالا (در WinForms کنترلِ دیرتر بالاتر
            // Dock می‌شود) — همان نکته‌ای که در پنجره‌ی بررسی بسته هم بود.

            // ── جدول (اول اضافه می‌شود تا فضای باقی‌مانده را بگیرد، نه همه را) ──
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
            _grid.SelectionChanged += delegate { ShowComparison(); };
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellDoubleClick += delegate { OpenCase(true); };

            Panel gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 6) };
            gridWrap.Controls.Add(_grid);
            Controls.Add(gridWrap);

            // ── مقایسه‌ی کنارِ هم ──
            // بعد از جدول اضافه می‌شود تا سهمش را پیش از پرشدنِ فضا بگیرد
            // (در اجرای اول برعکس بود و این پنل اصلاً دیده نمی‌شد).
            _comparePanel = new Panel
            {
                Dock = DockStyle.Bottom, Height = 140,
                BackColor = UiTheme.CardBack, Padding = new Padding(14, 8, 14, 8)
            };
            _lblCompare = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 9F),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.TopLeft,   // در بافت راست‌به‌چپ، راست دیده می‌شود
                Text = "برای دیدنِ مقایسه، یک ردیف را انتخاب کنید."
            };
            _comparePanel.Controls.Add(_lblCompare);
            Controls.Add(_comparePanel);

            // ── نوار ابزار ──
            Controls.Add(BuildToolbar());

            // ── سربرگ ──
            Panel header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = UiTheme.PrimaryDark };
            var title = new Label
            {
                Text = "پرونده‌های تکراری",
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
                Text = "برای شروع، «جستجوی تکراری‌ها» را بزنید. هیچ داده‌ای تغییر نمی‌کند."
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

            _btnScan = UiTheme.CreateButton("جستجوی تکراری‌ها", "⌕", UiTheme.Primary);
            _btnScan.Size = new Size(180, 34); _btnScan.Margin = new Padding(0, 0, 8, 0);
            _btnScan.Click += async delegate { await RunScan(); };
            flow.Controls.Add(_btnScan);

            _cmbConfidence = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 170, Font = UiTheme.Font(UiTheme.SizeBody),
                Margin = new Padding(0, 2, 8, 0)
            };
            _cmbConfidence.Items.AddRange(new object[]
            { "همه", "فقط قطعی", "بسیار محتمل و بالاتر", "محتمل و بالاتر" });
            _cmbConfidence.SelectedIndex = 0;
            _cmbConfidence.SelectedIndexChanged += delegate { ApplyFilter(); };
            flow.Controls.Add(_cmbConfidence);

            _txtSearch = new TextBox
            {
                Width = 200, Font = UiTheme.Font(UiTheme.SizeBody),
                Margin = new Padding(0, 2, 6, 0)
            };
            _txtSearch.TextChanged += delegate { ApplyFilter(); };
            flow.Controls.Add(_txtSearch);

            flow.Controls.Add(new Label
            {
                Text = "جستجوی نام یا کد:", AutoSize = true,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                Margin = new Padding(0, 8, 10, 0)
            });

            _btnOpenA = UiTheme.CreateSecondaryButton("باز کردن پرونده‌ی اول", "▤");
            _btnOpenA.Size = new Size(180, 34); _btnOpenA.Margin = new Padding(6, 0, 4, 0);
            _btnOpenA.Click += delegate { OpenCase(true); };
            flow.Controls.Add(_btnOpenA);

            _btnOpenB = UiTheme.CreateSecondaryButton("باز کردن پرونده‌ی دوم", "▤");
            _btnOpenB.Size = new Size(180, 34); _btnOpenB.Margin = new Padding(4, 0, 4, 0);
            _btnOpenB.Click += delegate { OpenCase(false); };
            flow.Controls.Add(_btnOpenB);

            _btnExcel = UiTheme.CreateSecondaryButton("خروجی Excel", "⇑");
            _btnExcel.Size = new Size(140, 34); _btnExcel.Margin = new Padding(4, 0, 4, 0);
            _btnExcel.Click += delegate { ExportExcel(); };
            flow.Controls.Add(_btnExcel);

            _progress = new ProgressBar
            {
                Width = 170, Height = 20,
                Margin = new Padding(10, 8, 0, 0), Visible = false
            };
            flow.Controls.Add(_progress);

            bar.Controls.Add(flow);
            return bar;
        }

        // ─── اجرای جستجو ────────────────────────────────────────────────────
        private async Task RunScan()
        {
            _btnScan.Enabled = false;
            _progress.Visible = true;
            _progress.Value = 0;
            _lblSummary.Text = "در حال بررسی...";

            try
            {
                var detector = new DuplicateDetector(new DatabaseHelper());
                var progress = new Progress<int>(p =>
                {
                    _progress.Value = Math.Max(0, Math.Min(100, p));
                });

                var token = CancellationToken.None;
                _matches = await Task.Run(() => detector.Detect(progress, token));

                FillTable();
                ApplyFilter();

                int certain = _matches.Count(m => m.SimilarityPercent >= 100);
                _lblSummary.Text =
                    "جمعاً " + _matches.Count.ToString("N0") + " جفتِ مشکوک پیدا شد" +
                    "   ·   قطعی: " + certain.ToString("N0") +
                    "   ·   هیچ داده‌ای تغییر نکرده است.";
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "جستجوی تکراری‌ها ممکن نشد: " + ex.Message);
                _lblSummary.Text = "جستجو ناموفق بود.";
            }
            finally
            {
                _progress.Visible = false;
                _btnScan.Enabled = true;
            }
        }

        private void FillTable()
        {
            _table.Clear();
            if (_table.Columns.Count == 0)
            {
                _table.Columns.Add("شباهت", typeof(int));
                _table.Columns.Add("اطمینان");
                _table.Columns.Add("مطابقت بر اساس");
                _table.Columns.Add("کد اول");
                _table.Columns.Add("نام اول");
                _table.Columns.Add("کد دوم");
                _table.Columns.Add("نام دوم");
                _table.Columns.Add("CasIdA", typeof(int));
                _table.Columns.Add("CasIdB", typeof(int));
            }

            foreach (DuplicateMatch m in _matches)
            {
                _table.Rows.Add(m.SimilarityPercent, m.ConfidenceText, m.MatchedFieldsText,
                                m.CodeA, m.NameA, m.CodeB, m.NameB, m.CasIdA, m.CasIdB);
            }
        }

        private void ApplyFilter()
        {
            if (_table.Columns.Count == 0) return;

            var view = new DataView(_table);
            var parts = new System.Collections.Generic.List<string>();

            switch (_cmbConfidence.SelectedIndex)
            {
                case 1: parts.Add("[شباهت] >= 100"); break;
                case 2: parts.Add("[شباهت] >= 92"); break;
                case 3: parts.Add("[شباهت] >= 85"); break;
            }

            string search = _txtSearch.Text.Trim().Replace("'", "''");
            if (search.Length > 0)
                parts.Add("([کد اول] LIKE '%" + search + "%' OR [کد دوم] LIKE '%" + search + "%' " +
                          "OR [نام اول] LIKE '%" + search + "%' OR [نام دوم] LIKE '%" + search + "%')");

            view.RowFilter = string.Join(" AND ", parts.ToArray());
            view.Sort = "[شباهت] DESC";
            _grid.DataSource = view;

            if (_grid.Columns.Contains("CasIdA")) _grid.Columns["CasIdA"].Visible = false;
            if (_grid.Columns.Contains("CasIdB")) _grid.Columns["CasIdB"].Visible = false;

            if (_grid.Columns.Contains("شباهت")) _grid.Columns["شباهت"].FillWeight = 32;
            if (_grid.Columns.Contains("اطمینان")) _grid.Columns["اطمینان"].FillWeight = 46;
            if (_grid.Columns.Contains("مطابقت بر اساس")) _grid.Columns["مطابقت بر اساس"].FillWeight = 78;
            if (_grid.Columns.Contains("کد اول")) _grid.Columns["کد اول"].FillWeight = 38;
            if (_grid.Columns.Contains("نام اول")) _grid.Columns["نام اول"].FillWeight = 82;
            if (_grid.Columns.Contains("کد دوم")) _grid.Columns["کد دوم"].FillWeight = 38;
            if (_grid.Columns.Contains("نام دوم")) _grid.Columns["نام دوم"].FillWeight = 82;

            ShowComparison();
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;

            object val = _grid.Rows[e.RowIndex].Cells["شباهت"].Value;
            int score;
            if (val == null || !int.TryParse(Convert.ToString(val), out score)) return;

            DataGridViewRow row = _grid.Rows[e.RowIndex];
            if (score >= 100) row.DefaultCellStyle.ForeColor = Color.FromArgb(0xC0, 0x39, 0x2B);
            else if (score >= 92) row.DefaultCellStyle.ForeColor = Color.FromArgb(0xA9, 0x6A, 0x0B);
            else row.DefaultCellStyle.ForeColor = UiTheme.TextDark;
        }

        // ─── مقایسه‌ی دو پرونده ─────────────────────────────────────────────
        private DuplicateMatch CurrentMatch()
        {
            if (_grid.CurrentRow == null) return null;

            object a = _grid.CurrentRow.Cells["CasIdA"].Value;
            object b = _grid.CurrentRow.Cells["CasIdB"].Value;
            if (a == null || b == null) return null;

            int idA = Convert.ToInt32(a), idB = Convert.ToInt32(b);
            return _matches.FirstOrDefault(m => m.CasIdA == idA && m.CasIdB == idB);
        }

        private void ShowComparison()
        {
            DuplicateMatch m = CurrentMatch();
            if (m == null)
            {
                _lblCompare.Text = "برای دیدنِ مقایسه، یک ردیف را انتخاب کنید.";
                return;
            }

            _lblCompare.Text =
                "شباهت " + m.SimilarityPercent + "٪  (" + m.ConfidenceText + ")   ·   مطابقت بر اساس: " +
                m.MatchedFieldsText + Environment.NewLine + Environment.NewLine +
                Row("کد اختصاصی", m.CodeA, m.CodeB) +
                Row("شماره فرم", m.FormNoA, m.FormNoB) +
                Row("نام سرپرست", m.NameA, m.NameB) +
                Row("نام پدر", m.FatherA, m.FatherB) +
                Row("شماره تذکره", m.TazkiraA, m.TazkiraB) +
                Row("شماره تماس", m.PhoneA, m.PhoneB);
        }

        // هر سطرِ مقایسه؛ اگر دو مقدار فرق داشته باشند علامت می‌خورد.
        private static string Row(string label, string a, string b)
        {
            string mark = string.Equals((a ?? "").Trim(), (b ?? "").Trim(),
                StringComparison.OrdinalIgnoreCase) ? "=" : "≠";

            return label.PadRight(14) + " :   " + Fit(a) + "   " + mark + "   " + Fit(b) + Environment.NewLine;
        }

        private static string Fit(string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0) value = "—";
            return value.Length > 28 ? value.Substring(0, 28) : value.PadRight(28);
        }

        // ─── باز کردنِ پرونده برای اصلاح ────────────────────────────────────
        private void OpenCase(bool first)
        {
            DuplicateMatch m = CurrentMatch();
            if (m == null)
            {
                UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید.");
                return;
            }

            int casId = first ? m.CasIdA : m.CasIdB;

            try
            {
                // پرونده در فرمِ خودش باز می‌شود تا کاربر خودش تصمیم بگیرد؛
                // این پنجره هیچ تغییری اعمال نمی‌کند.
                var frm = new FrmCase(casId);
                frm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "باز کردن پرونده ممکن نشد: " + ex.Message);
            }
        }

        // ─── خروجی ──────────────────────────────────────────────────────────
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

                string path = Path.Combine(folder, "پرونده‌های_تکراری_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss",
                        System.Globalization.CultureInfo.InvariantCulture) + ".xlsx");

                DataTable export = ((DataView)_grid.DataSource).ToTable();
                if (export.Columns.Contains("CasIdA")) export.Columns.Remove("CasIdA");
                if (export.Columns.Contains("CasIdB")) export.Columns.Remove("CasIdB");

                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add(export, "تکراری‌ها");
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

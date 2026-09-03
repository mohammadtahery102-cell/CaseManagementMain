using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // داشبوردِ نتیجه‌ی «بررسی بسته‌ی ورودی».
    //
    // این پنجره فقط نمایش‌دهنده است — هیچ عملیاتی روی دیتابیس یا فایل‌ها
    // انجام نمی‌دهد. خروجی‌ها (Excel/PDF/چاپ) از همان کمک‌کننده‌های موجودِ
    // پروژه استفاده می‌کنند تا کدِ تکراری ساخته نشود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmValidationReport : Form
    {
        private readonly PackageValidationReport _report;
        private readonly DataTable _issues = new DataTable();

        private DataGridView _grid;
        private TextBox _txtSearch;
        private ComboBox _cmbFilter;
        private Label _lblVerdict;
        private Panel _verdictBar;

        public FrmValidationReport(PackageValidationReport report)
        {
            _report = report ?? new PackageValidationReport();
            BuildUi();
            LoadData();
            ApplyFilter();
        }

        private void BuildUi()
        {
            Text = "نتیجه‌ی بررسی بسته‌ی ورودی";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1180, 720);

            // آموزش — ترتیبِ افزودن عمداً وارونه است: در WinForms کنترلی که
            // دیرتر اضافه شود زودتر Dock می‌شود و بالاتر می‌نشیند. پس برای
            // چیدمانِ «حکم → خلاصه → نوار ابزار → جدول» باید از پایین به بالا
            // اضافه کنیم. (در اجرای اول همین اشتباه شد و نوار ابزار بالای
            // نوارِ حکم افتاد.)

            // ── جدول یافته‌ها (اول اضافه می‌شود تا فضای باقی‌مانده را بگیرد) ──
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
            _grid.CellFormatting += Grid_CellFormatting;

            Panel gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 12) };
            gridWrap.Controls.Add(_grid);
            Controls.Add(gridWrap);

            // ── نوار ابزار: فیلتر، جستجو، خروجی ──
            Controls.Add(BuildToolbar());

            // ── کارت‌های خلاصه ──
            Controls.Add(BuildSummaryStrip());

            // ── نوارِ دیتابیسِ متصل — برای رفعِ ابهامِ «چند نسخه از برنامه روی
            // یک سیستم» (نگاه کنید توضیح در ValidationModels.cs). دقیقاً زیرِ
            // نوارِ حکمِ نهایی، همیشه پیدا، بدونِ نیاز به اسکرول یا خروجیِ Word.
            Controls.Add(BuildDatabaseInfoStrip());

            // ── نوارِ حکمِ نهایی (سبز/زرد/قرمز) — بالاترین و پیداترین ──
            _verdictBar = new Panel { Dock = DockStyle.Top, Height = 68 };
            _lblVerdict = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(24, 0, 24, 0)
            };
            _verdictBar.Controls.Add(_lblVerdict);
            Controls.Add(_verdictBar);

            ApplyVerdictStyle();
        }

        private void ApplyVerdictStyle()
        {
            switch (_report.OverallSeverity)
            {
                case ValidationSeverity.Critical:
                    _verdictBar.BackColor = Color.FromArgb(0xC0, 0x39, 0x2B);
                    _lblVerdict.Text = "✕    " + _report.OverallText;
                    break;
                case ValidationSeverity.Warning:
                    _verdictBar.BackColor = Color.FromArgb(0xE0, 0x8B, 0x1A);
                    _lblVerdict.Text = "!    " + _report.OverallText;
                    break;
                default:
                    _verdictBar.BackColor = Color.FromArgb(0x1E, 0x8E, 0x4A);
                    _lblVerdict.Text = "✓    " + _report.OverallText;
                    break;
            }
        }

        // ─── نوارِ دیتابیسِ متصل ─────────────────────────────────────────────
        private Control BuildDatabaseInfoStrip()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = UiTheme.CardBack };
            var lbl = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.Font(9F),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(24, 0, 24, 0),
                Text = "دیتابیس: " + (string.IsNullOrWhiteSpace(_report.DatabasePath) ? "؟" : _report.DatabasePath) +
                       "   (" + _report.TotalCasesInDatabase + " پرونده در دیتابیس)"
            };
            bar.Controls.Add(lbl);
            return bar;
        }

        // ─── نوارِ کارت‌های خلاصه ─────────────────────────────────────────────
        private Control BuildSummaryStrip()
        {
            var host = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 168,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                BackColor = UiTheme.Background,
                Padding = new Padding(12, 10, 12, 6)
            };

            AddTile(host, "پرونده‌های بسته", _report.TotalCasesInPackage, UiTheme.Primary);
            AddTile(host, "اعضای خانواده", _report.TotalMembersInPackage, UiTheme.Primary);
            AddTile(host, "عکس‌ها", _report.TotalPhotos, UiTheme.Primary);
            AddTile(host, "اسناد", _report.TotalDocuments, UiTheme.Primary);

            AddTile(host, "آماده", _report.ReadyCases, Color.FromArgb(0x1E, 0x8E, 0x4A));
            AddTile(host, "دارای هشدار", _report.CasesWithWarnings, Color.FromArgb(0xE0, 0x8B, 0x1A));
            AddTile(host, "دارای خطا", _report.CasesWithErrors, Color.FromArgb(0xC0, 0x39, 0x2B));

            AddTile(host, "بدون عکس", _report.MissingPhotos, UiTheme.TextMuted);
            AddTile(host, "بدون سند", _report.MissingDocuments, UiTheme.TextMuted);
            AddTile(host, "فایل تکراری", _report.DuplicateFiles, UiTheme.TextMuted);
            AddTile(host, "کد تکراری", _report.DuplicateCaseCodes, UiTheme.TextMuted);
            AddTile(host, "فرمت نامعتبر", _report.UnsupportedFiles, UiTheme.TextMuted);
            AddTile(host, "فایل خراب", _report.CorruptedFiles, UiTheme.TextMuted);
            AddTile(host, "نام نامعتبر", _report.InvalidFileNames, UiTheme.TextMuted);
            AddTile(host, "عکس بزرگ", _report.LargeImages, UiTheme.TextMuted);
            AddTile(host, "عکس کوچک", _report.SmallImages, UiTheme.TextMuted);
            AddTile(host, "فایل بلااستفاده", _report.UnusedFiles, UiTheme.TextMuted);
            AddTile(host, "فایل بی‌صاحب", _report.OrphanFiles, UiTheme.TextMuted);

            AddTextTile(host, "زمان تقریبی همگام‌سازی", FormatDuration(_report.EstimatedSyncTime));

            return host;
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalMinutes >= 1)
                return ((int)span.TotalMinutes) + " دقیقه و " + span.Seconds + " ثانیه";
            return Math.Max(1, (int)Math.Ceiling(span.TotalSeconds)) + " ثانیه";
        }

        private void AddTile(FlowLayoutPanel host, string caption, int value, Color accent)
        {
            AddTextTile(host, caption, value.ToString("N0"), accent);
        }

        private void AddTextTile(FlowLayoutPanel host, string caption, string value)
        {
            AddTextTile(host, caption, value, UiTheme.Primary);
        }

        private void AddTextTile(FlowLayoutPanel host, string caption, string value, Color accent)
        {
            var tile = new Panel
            {
                Width = 138, Height = 68,
                BackColor = UiTheme.CardBack,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(4),
                RightToLeft = RightToLeft.Yes
            };

            var lblValue = new Label
            {
                Text = value, Dock = DockStyle.Top, Height = 34,
                Font = UiTheme.FontBold(UiTheme.SizeLarge), ForeColor = accent,
                TextAlign = ContentAlignment.MiddleCenter
            };
            var lblCaption = new Label
            {
                Text = caption, Dock = DockStyle.Fill,
                Font = UiTheme.Font(UiTheme.SizeSmall - 1F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter
            };

            tile.Controls.Add(lblCaption);
            tile.Controls.Add(lblValue);
            host.Controls.Add(tile);
        }

        // ─── نوار ابزار ─────────────────────────────────────────────────────
        private Control BuildToolbar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = UiTheme.CardBack };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(12, 9, 12, 9)
            };

            _cmbFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 180, Font = UiTheme.Font(UiTheme.SizeBody),
                Margin = new Padding(0, 2, 8, 0)
            };
            _cmbFilter.Items.AddRange(new object[]
            { "همه‌ی موارد", "فقط خطاهای بحرانی", "فقط هشدارها", "فقط موارد آماده" });
            _cmbFilter.SelectedIndex = 0;
            _cmbFilter.SelectedIndexChanged += delegate { ApplyFilter(); };
            flow.Controls.Add(_cmbFilter);

            _txtSearch = new TextBox
            {
                Width = 220, Font = UiTheme.Font(UiTheme.SizeBody),
                Margin = new Padding(0, 2, 8, 0)
            };
            _txtSearch.TextChanged += delegate { ApplyFilter(); };
            flow.Controls.Add(_txtSearch);

            var lblSearch = new Label
            {
                Text = "جستجوی کد پرونده:", AutoSize = true,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                Margin = new Padding(0, 8, 8, 0)
            };
            flow.Controls.Add(lblSearch);

            Button btnExcel = UiTheme.CreateSecondaryButton("خروجی Excel", "⇑");
            btnExcel.Size = new Size(140, 34); btnExcel.Margin = new Padding(8, 0, 4, 0);
            btnExcel.Click += delegate { ExportExcel(); };
            flow.Controls.Add(btnExcel);

            Button btnPdf = UiTheme.CreateSecondaryButton("خروجی PDF", "➤");
            btnPdf.Size = new Size(140, 34); btnPdf.Margin = new Padding(4, 0, 4, 0);
            btnPdf.Click += delegate { ExportPdf(); };
            flow.Controls.Add(btnPdf);

            Button btnPrint = UiTheme.CreateSecondaryButton("چاپ", "▤");
            btnPrint.Size = new Size(110, 34); btnPrint.Margin = new Padding(4, 0, 4, 0);
            btnPrint.Click += delegate { PrintReport(); };
            flow.Controls.Add(btnPrint);

            bar.Controls.Add(flow);
            return bar;
        }

        // ─── داده ────────────────────────────────────────────────────────────
        private void LoadData()
        {
            _issues.Columns.Add("نشانه");
            _issues.Columns.Add("شدت");
            _issues.Columns.Add("دسته");
            _issues.Columns.Add("کد پرونده");
            _issues.Columns.Add("نام فایل");
            _issues.Columns.Add("شرح");
            _issues.Columns.Add("راه‌حل پیشنهادی");

            foreach (ValidationIssue i in _report.Issues
                     .OrderByDescending(x => (int)x.Severity))
            {
                _issues.Rows.Add(i.Icon, i.SeverityText, i.Category,
                                 i.CaseCode, Ltr(i.FileName), i.Description, i.Suggestion);
            }

            // اگر هیچ ایرادی نبود، یک ردیفِ «آماده» نمایش داده می‌شود تا صفحه
            // خالی و گیج‌کننده نباشد.
            if (_issues.Rows.Count == 0)
                _issues.Rows.Add("✓", "آماده", "بسته", "", "",
                    "هیچ مشکلی پیدا نشد؛ بسته آماده‌ی همگام‌سازی است.", "");
        }

        // آموزش — نامِ فایل بین نشانه‌های «چپ‌به‌راست» گذاشته می‌شود، وگرنه در
        // ستونِ راست‌به‌چپ، «24278.jpg» وارونه و به‌صورت «jpg.24278» دیده
        // می‌شود (در تصویرِ آزمون دیده شد).
        private static string Ltr(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return "‎" + value + "‎";
        }

        private void ApplyFilter()
        {
            string search = (_txtSearch == null ? "" : _txtSearch.Text.Trim());
            int mode = _cmbFilter == null ? 0 : _cmbFilter.SelectedIndex;

            var view = new DataView(_issues);
            var parts = new System.Collections.Generic.List<string>();

            if (mode == 1) parts.Add("[شدت] = 'خطای بحرانی'");
            else if (mode == 2) parts.Add("[شدت] = 'هشدار'");
            else if (mode == 3) parts.Add("[شدت] = 'آماده'");

            if (search.Length > 0)
                parts.Add("[کد پرونده] LIKE '%" + search.Replace("'", "''") + "%'");

            view.RowFilter = string.Join(" AND ", parts.ToArray());
            _grid.DataSource = view;

            if (_grid.Columns.Contains("نشانه")) _grid.Columns["نشانه"].FillWeight = 22;
            if (_grid.Columns.Contains("شدت")) _grid.Columns["شدت"].FillWeight = 45;
            if (_grid.Columns.Contains("دسته")) _grid.Columns["دسته"].FillWeight = 55;
            if (_grid.Columns.Contains("کد پرونده")) _grid.Columns["کد پرونده"].FillWeight = 48;
            if (_grid.Columns.Contains("نام فایل")) _grid.Columns["نام فایل"].FillWeight = 70;
            if (_grid.Columns.Contains("شرح")) _grid.Columns["شرح"].FillWeight = 135;
            if (_grid.Columns.Contains("راه‌حل پیشنهادی")) _grid.Columns["راه‌حل پیشنهادی"].FillWeight = 135;
        }

        // رنگ‌آمیزیِ ردیف بر اساس شدت — تشخیصِ سریعِ چشمی.
        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;

            DataGridViewRow row = _grid.Rows[e.RowIndex];
            object severity = row.Cells["شدت"].Value;
            string s = Convert.ToString(severity);

            if (s == "خطای بحرانی")
                row.DefaultCellStyle.ForeColor = Color.FromArgb(0xC0, 0x39, 0x2B);
            else if (s == "هشدار")
                row.DefaultCellStyle.ForeColor = Color.FromArgb(0xA9, 0x6A, 0x0B);
            else
                row.DefaultCellStyle.ForeColor = Color.FromArgb(0x1E, 0x8E, 0x4A);
        }

        // ─── خروجی‌ها ───────────────────────────────────────────────────────
        private string BuildSubtitle()
        {
            return "بسته: " + (_report.RootFolder ?? "") +
                   "   |   " + _report.OverallText +
                   "   |   خطا: " + _report.CriticalCount +
                   "   هشدار: " + _report.WarningCount +
                   "   |   دیتابیس: " + (_report.DatabasePath ?? "؟") +
                   " (" + _report.TotalCasesInDatabase + " پرونده)";
        }

        private string BuildOutputPath(string extension)
        {
            try
            {
                string root = FileHelper.GetOrChooseBaseRootFolder();
                if (string.IsNullOrWhiteSpace(root))
                {
                    UiTheme.ShowWarning(this, "محل ذخیره فایل‌ها مشخص نیست.");
                    return null;
                }

                string folder = Path.Combine(root, "Reports");
                Directory.CreateDirectory(folder);

                return Path.Combine(folder, "بررسی_بسته_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss",
                        System.Globalization.CultureInfo.InvariantCulture) + extension);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ساخت مسیر خروجی ممکن نشد: " + ex.Message);
                return null;
            }
        }

        private void ExportExcel()
        {
            string path = BuildOutputPath(".xlsx");
            if (path == null) return;

            try
            {
                DataTable table = ((DataView)_grid.DataSource).ToTable();

                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add(table, "بررسی بسته");
                    sheet.RightToLeft = true;
                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(path);
                }

                UiTheme.ShowSuccess(this, "خروجی Excel ساخته شد:" + Environment.NewLine + path);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ساخت خروجی Excel ممکن نشد: " + ex.Message);
            }
        }

        private void ExportPdf()
        {
            string docx = BuildOutputPath(".docx");
            if (docx == null) return;

            try
            {
                GridReportExporter.ExportToWord(_grid, "گزارش بررسی بسته‌ی ورودی", BuildSubtitle(), docx);

                if (!GridReportExporter.IsPdfAvailable())
                {
                    UiTheme.ShowWarning(this,
                        "برای ساخت PDF باید LibreOffice نصب باشد." + Environment.NewLine +
                        "فایل Word ساخته شد:" + Environment.NewLine + docx);
                    return;
                }

                string pdf = GridReportExporter.ConvertDocxToPdf(docx);
                UiTheme.ShowSuccess(this, "خروجی PDF ساخته شد:" + Environment.NewLine + pdf);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ساخت خروجی PDF ممکن نشد: " + ex.Message);
            }
        }

        private void PrintReport()
        {
            try
            {
                DataTable table = ((DataView)_grid.DataSource).ToTable();
                PrintHelper.PrintDataTable(this, "گزارش بررسی بسته‌ی ورودی", table);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "چاپ ممکن نشد: " + ex.Message);
            }
        }
    }
}

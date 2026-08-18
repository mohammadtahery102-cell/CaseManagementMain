using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement
{
    // ═════════════════════════════════════════════════════════════════════════
    // «گزارش‌ساز پویا» — انتخاب منبع/ستون/فیلتر/گروه‌بندی/مرتب‌سازی و اجرای
    // گزارش، با امکان ذخیره/بارگذاریِ الگو. نامِ ستون/جدول همیشه از
    // ReportCatalog (فهرستِ ثابت) می‌آید، نه از ورودیِ آزادِ کاربر —
    // ReportRunner در Helpers\ReportDefinitions.cs این را تضمین می‌کند.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmReportBuilder : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();

        private ComboBox _cmbSource;
        private CheckedListBox _clbColumns;
        private ListBox _lstFilters;
        private ComboBox _cmbFilterColumn, _cmbFilterOperator, _cmbGroupBy, _cmbSortColumn;
        private TextBox _txtFilterValue;
        private CheckBox _chkSortDesc;
        private ComboBox _cmbTemplates;
        private DataGridView _grid;
        private Label _lblSummary;

        private List<ReportFilterDto> _filters = new List<ReportFilterDto>();
        private DataTable _resultTable;

        public FrmReportBuilder()
        {
            BuildUi();
            Load += delegate { LoadTemplateList(); OnSourceChanged(); };
        }

        private void BuildUi()
        {
            Text = "گزارش‌ساز پویا";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1280, 760);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.PrimaryDark };
            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill, Text = "گزارش‌ساز پویا",
                ForeColor = Color.White, Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 0, 20, 0)
            });

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 26 }
            };
            UiTheme.StyleGrid(_grid);
            Panel gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 6) };
            gridWrap.Controls.Add(_grid);

            _lblSummary = new Label
            {
                Dock = DockStyle.Bottom, Height = 26, ForeColor = UiTheme.TextMuted,
                Font = UiTheme.Font(UiTheme.SizeSmall), TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(12, 0, 12, 0), Text = "برای اجرا، «اجرای گزارش» را بزنید."
            };

            Panel configPanel = BuildConfigPanel();

            Controls.Add(gridWrap);
            Controls.Add(_lblSummary);
            Controls.Add(configPanel);
            Controls.Add(header);
        }

        private Panel BuildConfigPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Top, Height = 250, BackColor = UiTheme.CardBack, Padding = new Padding(12, 8, 12, 8) };

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            // ── ستون ۱: منبع + ستون‌ها ──
            var col1 = new Panel { Dock = DockStyle.Fill };
            col1.Controls.Add(new Label { Text = "منبع گزارش:", Dock = DockStyle.Top, Height = 20, Font = UiTheme.Font(UiTheme.SizeSmall) });
            _cmbSource = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (ReportSource s in ReportCatalog.Sources)
                _cmbSource.Items.Add(s.DisplayName);
            _cmbSource.SelectedIndex = 0;
            _cmbSource.SelectedIndexChanged += delegate { OnSourceChanged(); };
            col1.Controls.Add(_cmbSource);
            col1.Controls.Add(new Label { Text = "ستون‌ها:", Dock = DockStyle.Top, Height = 20, Font = UiTheme.Font(UiTheme.SizeSmall), Margin = new Padding(0, 6, 0, 0) });
            _clbColumns = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
            col1.Controls.Add(_clbColumns);
            root.Controls.Add(col1, 3, 0);

            // ── ستون ۲: فیلترها ──
            var col2 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
            var filterRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, WrapContents = false };
            _cmbFilterColumn = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbFilterOperator = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (ReportFilterOperator op in Enum.GetValues(typeof(ReportFilterOperator)))
                _cmbFilterOperator.Items.Add(ReportCatalog.OperatorDisplayName(op));
            _cmbFilterOperator.SelectedIndex = 0;
            _txtFilterValue = new TextBox { Width = 110 };
            Button btnAddFilter = UiTheme.CreateSecondaryButton("+ افزودن فیلتر", "");
            btnAddFilter.Size = new Size(110, 28);
            btnAddFilter.Click += btnAddFilter_Click;
            filterRow.Controls.Add(_cmbFilterColumn);
            filterRow.Controls.Add(_cmbFilterOperator);
            filterRow.Controls.Add(_txtFilterValue);
            filterRow.Controls.Add(btnAddFilter);
            col2.Controls.Add(new Label { Text = "فیلترهای فعال (دوبار کلیک برای حذف):", Dock = DockStyle.Bottom, Height = 20, Font = UiTheme.Font(UiTheme.SizeSmall) });
            _lstFilters = new ListBox { Dock = DockStyle.Fill };
            _lstFilters.DoubleClick += delegate { RemoveSelectedFilter(); };
            col2.Controls.Add(_lstFilters);
            col2.Controls.Add(filterRow);
            col2.Controls.Add(new Label { Text = "فیلتر:", Dock = DockStyle.Top, Height = 20, Font = UiTheme.Font(UiTheme.SizeSmall) });
            root.Controls.Add(col2, 2, 0);

            // ── ستون ۳: گروه‌بندی/مرتب‌سازی ──
            var col3 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
            col3.Controls.Add(new Label { Text = "گروه‌بندی بر اساس (اختیاری):", Dock = DockStyle.Top, Height = 20, Font = UiTheme.Font(UiTheme.SizeSmall) });
            _cmbGroupBy = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            col3.Controls.Add(_cmbGroupBy);
            col3.Controls.Add(new Label { Text = "مرتب‌سازی بر اساس:", Dock = DockStyle.Top, Height = 20, Font = UiTheme.Font(UiTheme.SizeSmall), Margin = new Padding(0, 8, 0, 0) });
            _cmbSortColumn = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            col3.Controls.Add(_cmbSortColumn);
            _chkSortDesc = new CheckBox { Text = "نزولی", Dock = DockStyle.Top, Height = 24, Margin = new Padding(0, 4, 0, 0) };
            col3.Controls.Add(_chkSortDesc);
            root.Controls.Add(col3, 1, 0);

            // ── ستون ۴: الگوها + اجرا ──
            var col4 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
            col4.Controls.Add(new Label { Text = "الگوهای ذخیره‌شده:", Dock = DockStyle.Top, Height = 20, Font = UiTheme.Font(UiTheme.SizeSmall) });
            _cmbTemplates = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            col4.Controls.Add(_cmbTemplates);

            Button btnLoad = UiTheme.CreateSecondaryButton("بارگذاری الگو", "");
            btnLoad.Dock = DockStyle.Top; btnLoad.Height = 30; btnLoad.Margin = new Padding(0, 6, 0, 0);
            btnLoad.Click += delegate { LoadSelectedTemplate(); };
            col4.Controls.Add(btnLoad);

            Button btnSave = UiTheme.CreateSecondaryButton("ذخیره الگو", "");
            btnSave.Dock = DockStyle.Top; btnSave.Height = 30; btnSave.Margin = new Padding(0, 4, 0, 0);
            btnSave.Click += delegate { SaveTemplate(); };
            col4.Controls.Add(btnSave);

            Button btnDelete = UiTheme.CreateSecondaryButton("حذف الگو", "");
            btnDelete.Dock = DockStyle.Top; btnDelete.Height = 30; btnDelete.Margin = new Padding(0, 4, 0, 0);
            btnDelete.Click += delegate { DeleteSelectedTemplate(); };
            col4.Controls.Add(btnDelete);

            Button btnRun = UiTheme.CreateButton("اجرای گزارش", "▶", UiTheme.Primary);
            btnRun.Dock = DockStyle.Top; btnRun.Height = 34; btnRun.Margin = new Padding(0, 10, 0, 0);
            btnRun.Click += delegate { RunReport(); };
            col4.Controls.Add(btnRun);

            var exportRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
            Button btnExcel = UiTheme.CreateSecondaryButton("خروجی Excel", "⇑");
            btnExcel.Size = new Size(110, 28);
            btnExcel.Click += delegate { ExportExcel(); };
            Button btnPrint = UiTheme.CreateSecondaryButton("چاپ", "⎙");
            btnPrint.Size = new Size(90, 28);
            btnPrint.Click += delegate { PrintReport(); };

            // میان‌بُرها همین‌جا بسته می‌شوند چون این دکمه‌ها متغیرِ محلی‌اند.
            // F5 روی «اجرای گزارش» است، نه تازه‌سازی: پرکاربردترین کارِ این
            // صفحه همان اجراست و کاربر انتظارِ همین را دارد.
            Helpers.FormShortcuts.For(this)
                .Bind(Keys.F5, "اجرای گزارش", btnRun)
                .Bind(Keys.Control | Keys.S, "ذخیره الگو", btnSave)
                .Bind(Keys.Control | Keys.O, "بارگذاری الگو", btnLoad)
                .Bind(Keys.Control | Keys.D, "حذف الگو", btnDelete)
                .Print(btnPrint)
                .Bind(Keys.Control | Keys.M, "خروجی Excel", btnExcel);

            exportRow.Controls.Add(btnExcel);
            exportRow.Controls.Add(btnPrint);
            col4.Controls.Add(exportRow);

            root.Controls.Add(col4, 0, 0);

            panel.Controls.Add(root);
            return panel;
        }

        // ─── تعامل بین کنترل‌ها ─────────────────────────────────────────────
        private ReportSource CurrentSource()
        {
            if (_cmbSource.SelectedIndex < 0) return ReportCatalog.Sources[0];
            return ReportCatalog.Sources[_cmbSource.SelectedIndex];
        }

        private void OnSourceChanged()
        {
            ReportSource source = CurrentSource();

            _clbColumns.Items.Clear();
            _cmbFilterColumn.Items.Clear();
            _cmbGroupBy.Items.Clear();
            _cmbSortColumn.Items.Clear();

            _cmbGroupBy.Items.Add("(هیچ‌کدام)");
            foreach (ReportColumn col in source.Columns)
            {
                _clbColumns.Items.Add(col.DisplayName, true);
                _cmbFilterColumn.Items.Add(col.DisplayName);
                _cmbGroupBy.Items.Add(col.DisplayName);
                _cmbSortColumn.Items.Add(col.DisplayName);
            }

            if (_cmbFilterColumn.Items.Count > 0) _cmbFilterColumn.SelectedIndex = 0;
            _cmbGroupBy.SelectedIndex = 0;
            if (_cmbSortColumn.Items.Count > 0) _cmbSortColumn.SelectedIndex = 0;

            _filters.Clear();
            _lstFilters.Items.Clear();
        }

        private void btnAddFilter_Click(object sender, EventArgs e)
        {
            if (_cmbFilterColumn.SelectedIndex < 0 || string.IsNullOrWhiteSpace(_txtFilterValue.Text))
            {
                Msg.Show("ستون و مقدار فیلتر را مشخص کنید");
                return;
            }

            ReportSource source = CurrentSource();
            ReportColumn col = source.Columns[_cmbFilterColumn.SelectedIndex];
            ReportFilterOperator op = (ReportFilterOperator)_cmbFilterOperator.SelectedIndex;

            var dto = new ReportFilterDto { ColumnKey = col.Key, Op = op.ToString(), Value = _txtFilterValue.Text.Trim() };
            _filters.Add(dto);
            _lstFilters.Items.Add(col.DisplayName + " " + ReportCatalog.OperatorDisplayName(op) + " \"" + dto.Value + "\"");
            _txtFilterValue.Text = "";
        }

        private void RemoveSelectedFilter()
        {
            int idx = _lstFilters.SelectedIndex;
            if (idx < 0) return;
            _filters.RemoveAt(idx);
            _lstFilters.Items.RemoveAt(idx);
        }

        private ReportDefinition BuildDefinitionFromUi()
        {
            ReportSource source = CurrentSource();

            var def = new ReportDefinition { SourceKey = source.Key };
            for (int i = 0; i < _clbColumns.Items.Count; i++)
                if (_clbColumns.GetItemChecked(i))
                    def.ColumnKeys.Add(source.Columns[i].Key);

            def.Filters = _filters.ToList();

            if (_cmbGroupBy.SelectedIndex > 0)
                def.GroupByKey = source.Columns[_cmbGroupBy.SelectedIndex - 1].Key;

            if (_cmbSortColumn.SelectedIndex >= 0)
                def.SortColumnKey = source.Columns[_cmbSortColumn.SelectedIndex].Key;

            def.SortDescending = _chkSortDesc.Checked;
            return def;
        }

        private void RunReport()
        {
            try
            {
                ReportDefinition def = BuildDefinitionFromUi();

                if (def.ColumnKeys.Count == 0 && string.IsNullOrEmpty(def.GroupByKey))
                {
                    Msg.Show("حداقل یک ستون را انتخاب کنید");
                    return;
                }

                _resultTable = ReportRunner.Run(def, db);
                _grid.DataSource = _resultTable;
                _lblSummary.Text = "تعداد ردیف: " + _resultTable.Rows.Count.ToString("N0");
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در اجرای گزارش: " + ex.Message);
            }
        }

        // ─── الگوها ──────────────────────────────────────────────────────────
        private void LoadTemplateList()
        {
            _cmbTemplates.Items.Clear();
            _cmbTemplates.Items.Add("—");

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT TemplateID, TemplateName FROM TblReportTemplate ORDER BY TemplateName", con))
            {
                con.Open();
                using (var dr = cmd.ExecuteReader())
                    while (dr.Read())
                        _cmbTemplates.Items.Add(new TemplateItem(Convert.ToInt32(dr["TemplateID"]), Convert.ToString(dr["TemplateName"])));
            }

            _cmbTemplates.SelectedIndex = 0;
        }

        private sealed class TemplateItem
        {
            public int Id; public string Name;
            public TemplateItem(int id, string name) { Id = id; Name = name; }
            public override string ToString() { return Name; }
        }

        private void SaveTemplate()
        {
            // از EntPrompt (دیالوگ ورودیِ خودِ پروژه) استفاده می‌شود، نه
            // Microsoft.VisualBasic.Interaction.InputBox — تا ارجاع به اسمبلیِ
            // VisualBasic به پروژه اضافه نشود و دیالوگ هم RTL و هم‌شکلِ بقیه‌ی
            // برنامه بماند.
            string name = Enterprise.EntPrompt.AskText(
                this, "ذخیره الگوی گزارش", "نام الگو را وارد کنید:", "");
            if (string.IsNullOrWhiteSpace(name))
                return;

            ReportDefinition def = BuildDefinitionFromUi();

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO TblReportTemplate (TemplateName, SourceKey, ColumnsJson, FiltersJson, GroupByKey, SortColumnKey, SortDescending, CreatedBy)
VALUES (@Name, @Source, @Cols, @Filters, @Group, @Sort, @Desc, @By)", con))
                {
                    cmd.Parameters.AddWithValue("@Name", name.Trim());
                    cmd.Parameters.AddWithValue("@Source", def.SourceKey);
                    cmd.Parameters.AddWithValue("@Cols", json.Serialize(def.ColumnKeys));
                    cmd.Parameters.AddWithValue("@Filters", json.Serialize(def.Filters));
                    cmd.Parameters.AddWithValue("@Group", (object)def.GroupByKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Sort", (object)def.SortColumnKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Desc", def.SortDescending ? 1 : 0);
                    cmd.Parameters.AddWithValue("@By", SecurityContext.Username);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }

                Msg.Show("الگو ذخیره شد");
                LoadTemplateList();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ذخیره الگو: " + ex.Message);
            }
        }

        private void LoadSelectedTemplate()
        {
            TemplateItem item = _cmbTemplates.SelectedItem as TemplateItem;
            if (item == null)
            {
                Msg.Show("یک الگو را انتخاب کنید");
                return;
            }

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT * FROM TblReportTemplate WHERE TemplateID = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", item.Id);
                    con.Open();

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read())
                            return;

                        string sourceKey = Convert.ToString(dr["SourceKey"]);
                        int sourceIndex = ReportCatalog.Sources.FindIndex(s => s.Key == sourceKey);
                        if (sourceIndex < 0) { Msg.Show("منبع این الگو دیگر معتبر نیست"); return; }

                        _cmbSource.SelectedIndex = sourceIndex;
                        OnSourceChanged();

                        ReportSource source = CurrentSource();

                        List<string> colKeys = json.Deserialize<List<string>>(Convert.ToString(dr["ColumnsJson"])) ?? new List<string>();
                        for (int i = 0; i < _clbColumns.Items.Count; i++)
                            _clbColumns.SetItemChecked(i, colKeys.Contains(source.Columns[i].Key));

                        string filtersJson = dr["FiltersJson"] == DBNull.Value ? "" : Convert.ToString(dr["FiltersJson"]);
                        _filters = string.IsNullOrWhiteSpace(filtersJson)
                            ? new List<ReportFilterDto>()
                            : json.Deserialize<List<ReportFilterDto>>(filtersJson) ?? new List<ReportFilterDto>();

                        _lstFilters.Items.Clear();
                        foreach (ReportFilterDto f in _filters)
                        {
                            ReportColumn col = source.FindColumn(f.ColumnKey);
                            ReportFilterOperator op;
                            Enum.TryParse(f.Op, out op);
                            _lstFilters.Items.Add((col != null ? col.DisplayName : f.ColumnKey) + " " +
                                ReportCatalog.OperatorDisplayName(op) + " \"" + f.Value + "\"");
                        }

                        string groupKey = dr["GroupByKey"] == DBNull.Value ? "" : Convert.ToString(dr["GroupByKey"]);
                        _cmbGroupBy.SelectedIndex = string.IsNullOrEmpty(groupKey)
                            ? 0 : (source.Columns.FindIndex(c => c.Key == groupKey) + 1);

                        string sortKey = dr["SortColumnKey"] == DBNull.Value ? "" : Convert.ToString(dr["SortColumnKey"]);
                        int sortIdx = source.Columns.FindIndex(c => c.Key == sortKey);
                        _cmbSortColumn.SelectedIndex = sortIdx >= 0 ? sortIdx : (_cmbSortColumn.Items.Count > 0 ? 0 : -1);

                        _chkSortDesc.Checked = Convert.ToInt32(dr["SortDescending"]) != 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری الگو: " + ex.Message);
            }
        }

        private void DeleteSelectedTemplate()
        {
            TemplateItem item = _cmbTemplates.SelectedItem as TemplateItem;
            if (item == null)
            {
                Msg.Show("یک الگو را انتخاب کنید");
                return;
            }

            if (Msg.Show("این الگو حذف شود؟", "حذف", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM TblReportTemplate WHERE TemplateID = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Id", item.Id);
                con.Open();
                cmd.ExecuteNonQuery();
            }

            LoadTemplateList();
        }

        // ─── خروجی ──────────────────────────────────────────────────────────
        private void ExportExcel()
        {
            if (_resultTable == null || _resultTable.Rows.Count == 0)
            {
                Msg.Show("ابتدا گزارش را اجرا کنید");
                return;
            }

            try
            {
                string root = FileHelper.GetOrChooseBaseRootFolder();
                if (string.IsNullOrWhiteSpace(root)) { Msg.Show("محل ذخیره فایل‌ها مشخص نیست."); return; }

                string folder = Path.Combine(root, "Reports");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "گزارش_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".xlsx");

                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add(_resultTable, "گزارش");
                    sheet.RightToLeft = true;
                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(path);
                }

                Msg.Show("خروجی ساخته شد:" + Environment.NewLine + path);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ساخت خروجی: " + ex.Message);
            }
        }

        private void PrintReport()
        {
            if (_resultTable == null || _resultTable.Rows.Count == 0)
            {
                Msg.Show("ابتدا گزارش را اجرا کنید");
                return;
            }

            PrintHelper.PrintDataTable(this, "گزارش‌ساز پویا", _resultTable);
        }
    }
}

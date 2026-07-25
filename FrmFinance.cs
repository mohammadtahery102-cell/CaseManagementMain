using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement
{
    public class FrmFinance : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        private TextBox txtSearch;
        private DataGridView dgvCases;
        private DataGridView dgvAssistance;
        private DataGridView dgvMonthly;
        private DataGridView dgvWithoutAssistance;
        private NumericUpDown numAmount;
        private Helpers.PersianDatePicker dtpAssistanceDate;
        private TextBox txtType;
        private TextBox txtDescription;
        private Label lblSelectedCase;
        private Label lblTotal;

        private int selectedCaseId;

        public FrmFinance()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "سیستم مالی و کمک‌ها  —  " + SecurityContext.CenterDisplay;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1080, 690);

            // به‌جای SplitContainer (که در RTL و اندازه ثابت سهم پنل‌ها را
            // نامتوازن می‌کرد) از TableLayoutPanel با دو ستون استفاده می‌شود:
            // ستون گرید پرونده‌ها و ستون تب‌های ثبت/گزارش، با سهم مشخص.
            Panel searchPanel = new Panel();
            searchPanel.Dock = DockStyle.Top;
            searchPanel.Height = 66;
            searchPanel.BackColor = UiTheme.CardBack;

            FlowLayoutPanel searchFlow = new FlowLayoutPanel();
            searchFlow.Dock = DockStyle.Fill;
            searchFlow.FlowDirection = FlowDirection.RightToLeft;
            searchFlow.Padding = new Padding(10, 6, 10, 4);

            txtSearch = new TextBox();
            UiTheme.StyleTextBox(txtSearch);
            searchFlow.Controls.Add(MakeFieldPanel("نام / کد", txtSearch));

            Button btnSearch = UiTheme.CreateButton("جستجو", "⌕", UiTheme.Primary);
            btnSearch.Size = new Size(100, 32);
            btnSearch.Margin = new Padding(4, 28, 4, 4);
            btnSearch.Click += delegate { LoadCases(); };
            searchFlow.Controls.Add(btnSearch);

            searchPanel.Controls.Add(searchFlow);

            dgvCases = new DataGridView();
            dgvCases.Dock = DockStyle.Fill;
            ConfigureGrid(dgvCases);
            dgvCases.CellClick += dgvCases_CellClick;

            Panel gridSide = new Panel();
            gridSide.Dock = DockStyle.Fill;
            gridSide.Controls.Add(dgvCases);
            gridSide.Controls.Add(searchPanel);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = UiTheme.FontBold(UiTheme.SizeSmall);

            TabPage tabEntry = new TabPage("ثبت کمک");
            TabPage tabReports = new TabPage("گزارش‌ها");
            tabEntry.BackColor = UiTheme.Background;
            tabReports.BackColor = UiTheme.Background;

            BuildEntryTab(tabEntry);
            BuildReportsTab(tabReports);

            tabs.TabPages.Add(tabEntry);
            tabs.TabPages.Add(tabReports);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 2;
            root.RowCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            root.Controls.Add(gridSide, 0, 0);
            root.Controls.Add(tabs, 1, 0);

            Controls.Add(root);

            LoadCases();
            LoadReports();
        }

        private void BuildEntryTab(TabPage tab)
        {
            Panel entryPanel = new Panel();
            entryPanel.Dock = DockStyle.Top;
            entryPanel.Height = 220;
            entryPanel.BackColor = UiTheme.CardBack;

            lblSelectedCase = new Label();
            lblSelectedCase.Text = "پرونده انتخاب نشده";
            lblSelectedCase.AutoSize = false;
            lblSelectedCase.Dock = DockStyle.Top;
            lblSelectedCase.Height = 32;
            lblSelectedCase.TextAlign = ContentAlignment.MiddleRight;
            lblSelectedCase.Padding = new Padding(14, 0, 0, 0);
            lblSelectedCase.Font = UiTheme.FontBold(UiTheme.SizeMedium);
            lblSelectedCase.ForeColor = UiTheme.Primary;

            FlowLayoutPanel fieldsFlow = new FlowLayoutPanel();
            fieldsFlow.Dock = DockStyle.Top;
            fieldsFlow.Height = 130;
            fieldsFlow.FlowDirection = FlowDirection.RightToLeft;
            fieldsFlow.WrapContents = true;
            fieldsFlow.Padding = new Padding(14, 4, 14, 4);

            numAmount = new NumericUpDown();
            numAmount.Maximum = 1000000000;
            numAmount.DecimalPlaces = 2;
            numAmount.ThousandsSeparator = true;
            fieldsFlow.Controls.Add(MakeFieldPanel("مبلغ", numAmount));

            dtpAssistanceDate = new Helpers.PersianDatePicker();
            dtpAssistanceDate.Value = DateTime.Today;
            fieldsFlow.Controls.Add(MakeFieldPanel("تاریخ", dtpAssistanceDate));

            txtType = new TextBox();
            txtType.Text = "نقدی";
            UiTheme.StyleTextBox(txtType);
            fieldsFlow.Controls.Add(MakeFieldPanel("نوع کمک", txtType));

            txtDescription = new TextBox();
            UiTheme.StyleTextBox(txtDescription);
            fieldsFlow.Controls.Add(MakeFieldPanel("توضیح", txtDescription));

            FlowLayoutPanel buttonFlow = new FlowLayoutPanel();
            buttonFlow.Dock = DockStyle.Top;
            buttonFlow.Height = 50;
            buttonFlow.FlowDirection = FlowDirection.RightToLeft;
            buttonFlow.Padding = new Padding(14, 6, 14, 6);

            Button btnSave = UiTheme.CreateButton("ثبت کمک", "+", UiTheme.Primary);
            btnSave.Size = new Size(130, 36);
            btnSave.Margin = new Padding(4);
            btnSave.Click += btnSave_Click;
            buttonFlow.Controls.Add(btnSave);

            Button btnPrintAssistance = UiTheme.CreateSecondaryButton("چاپ فهرست کمک‌ها", "🖨");
            btnPrintAssistance.Size = new Size(170, 36);
            btnPrintAssistance.Margin = new Padding(4);
            btnPrintAssistance.Click += delegate { PrintAssistanceHistory(); };
            buttonFlow.Controls.Add(btnPrintAssistance);

            // آموزش — ترتیب Add با Dock=Top: آخرین کنترلی که اضافه می‌شود در
            // بالاترین موقعیت قرار می‌گیرد؛ پس ترتیب Add برعکس ترتیب نمایش
            // است (دکمه‌ها اول، فیلدها بعد، عنوان آخر تا در بالا بماند).
            entryPanel.Controls.Add(buttonFlow);
            entryPanel.Controls.Add(fieldsFlow);
            entryPanel.Controls.Add(lblSelectedCase);

            dgvAssistance = new DataGridView();
            dgvAssistance.Dock = DockStyle.Fill;
            ConfigureGrid(dgvAssistance);
            UiTheme.ApplyPersianDateColumns(dgvAssistance, "AssistanceDate");

            tab.Controls.Add(dgvAssistance);
            tab.Controls.Add(entryPanel);
        }

        private void BuildReportsTab(TabPage tab)
        {
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 260;

            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 50;
            top.BackColor = UiTheme.CardBack;

            // ─── دکمه‌ها سمت راست (عرض ثابت)، خلاصه مبلغ در فضای باقی‌مانده ──
            FlowLayoutPanel topFlow = new FlowLayoutPanel();
            topFlow.Dock = DockStyle.Right;
            topFlow.Width = 300;
            topFlow.FlowDirection = FlowDirection.RightToLeft;
            topFlow.Padding = new Padding(6, 8, 6, 8);

            Button btnRefresh = UiTheme.CreateButton("تازه‌سازی", "↻", UiTheme.Primary);
            btnRefresh.Size = new Size(110, 32);
            btnRefresh.Margin = new Padding(4);
            btnRefresh.Click += delegate { LoadReports(); };
            topFlow.Controls.Add(btnRefresh);

            Button btnPrintReport = UiTheme.CreateSecondaryButton("چاپ گزارش ماهانه", "🖨");
            btnPrintReport.Size = new Size(150, 32);
            btnPrintReport.Margin = new Padding(4);
            btnPrintReport.Click += delegate { PrintMonthlyReport(); };
            topFlow.Controls.Add(btnPrintReport);

            lblTotal = new Label();
            lblTotal.Text = "";
            lblTotal.AutoSize = false;
            lblTotal.Dock = DockStyle.Fill;
            lblTotal.TextAlign = ContentAlignment.MiddleRight;
            lblTotal.Padding = new Padding(0, 0, 8, 0);
            lblTotal.Font = UiTheme.FontBold(UiTheme.SizeMedium);
            lblTotal.ForeColor = UiTheme.Success;

            top.Controls.Add(lblTotal);
            top.Controls.Add(topFlow);

            dgvMonthly = new DataGridView();
            dgvMonthly.Dock = DockStyle.Fill;
            ConfigureGrid(dgvMonthly);

            dgvWithoutAssistance = new DataGridView();
            dgvWithoutAssistance.Dock = DockStyle.Fill;
            ConfigureGrid(dgvWithoutAssistance);

            split.Panel1.Controls.Add(dgvMonthly);
            split.Panel1.Controls.Add(top);
            split.Panel2.Controls.Add(dgvWithoutAssistance);
            tab.Controls.Add(split);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.CanEdit())
            {
                UiTheme.ShowWarning(this, "کاربر فقط مشاهده اجازه ثبت کمک ندارد.");
                return;
            }

            if (selectedCaseId <= 0)
            {
                UiTheme.ShowWarning(this, "ابتدا پرونده را انتخاب کنید.");
                return;
            }

            if (numAmount.Value <= 0)
            {
                UiTheme.ShowWarning(this, "مبلغ کمک باید بیشتر از صفر باشد.");
                return;
            }

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO TblAssistance (CasID, AssistanceDate, Amount, AssistanceType, Description, CreatedBy, GlobalID)
VALUES (@CasID, @AssistanceDate, @Amount, @AssistanceType, @Description, @CreatedBy,
    lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
    lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))))", con))
            {
                cmd.Parameters.Add("@CasID", DbType.Int32).Value = selectedCaseId;
                cmd.Parameters.AddWithValue("@AssistanceDate", dtpAssistanceDate.Value.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@Amount", numAmount.Value);
                cmd.Parameters.Add("@AssistanceType", DbType.String, 100).Value = txtType.Text.Trim();
                cmd.Parameters.Add("@Description", DbType.String, -1).Value = txtDescription.Text.Trim();
                cmd.Parameters.Add("@CreatedBy", DbType.String, 50).Value = SecurityContext.Username ?? "";

                con.Open();
                cmd.ExecuteNonQuery();
            }

            AuditLogger.Log("ثبت کمک", "TblAssistance", selectedCaseId, "", "Amount=" + numAmount.Value);
            LoadAssistance();
            LoadReports();
            UiTheme.ShowSuccess(this, "کمک ثبت شد.");
        }

        private void PrintAssistanceHistory()
        {
            DataTable table = dgvAssistance.DataSource as DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                Msg.Show("داده‌ای برای چاپ وجود ندارد");
                return;
            }

            DataTable printTable = table.Copy();
            Helpers.PersianDateHelper.ConvertDateColumnsToPersian(printTable, "AssistanceDate");
            PrintHelper.PrintDataTable(this, "فهرست کمک‌ها — " + lblSelectedCase.Text, printTable);
        }

        private void PrintMonthlyReport()
        {
            DataTable table = dgvMonthly.DataSource as DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                Msg.Show("داده‌ای برای چاپ وجود ندارد");
                return;
            }

            PrintHelper.PrintDataTable(this, "گزارش ماهانه کمک‌ها", table);
        }

        private void dgvCases_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || !dgvCases.Columns.Contains("CasID"))
                return;

            object value = dgvCases.Rows[e.RowIndex].Cells["CasID"].Value;
            if (value == null || value == DBNull.Value)
                return;

            selectedCaseId = Convert.ToInt32(value);
            string code = dgvCases.Rows[e.RowIndex].Cells["Code"].Value.ToString();
            string name = dgvCases.Rows[e.RowIndex].Cells["HeadFullName"].Value.ToString();
            lblSelectedCase.Text = "پرونده انتخاب‌شده: " + code + " - " + name;
            LoadAssistance();
        }

        private void LoadCases()
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT CasID, FormNo, Code, HeadFullName, Phone, ServiceStatus
FROM TblCase
WHERE (@Search = ''
   OR Code LIKE @LikeSearch
   OR HeadFullName LIKE @LikeSearch)
  AND (@CID = 0 OR CenterID = @CID)
ORDER BY CasID DESC", con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                string search = txtSearch == null ? "" : txtSearch.Text.Trim();
                cmd.Parameters.Add("@Search", DbType.String, 4000).Value = search;
                cmd.Parameters.Add("@LikeSearch", DbType.String, 4000).Value = "%" + search + "%";
                cmd.Parameters.AddWithValue("@CID", Helpers.SecurityContext.CenterFilterId);

                DataTable table = new DataTable();
                da.Fill(table);
                dgvCases.DataSource = table;
            }
        }

        private void LoadAssistance()
        {
            if (selectedCaseId <= 0)
                return;

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT AssistanceID, AssistanceDate, Amount, AssistanceType, Description, CreatedBy
FROM TblAssistance
WHERE CasID = @CasID
ORDER BY AssistanceDate DESC, AssistanceID DESC", con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                cmd.Parameters.Add("@CasID", DbType.Int32).Value = selectedCaseId;
                DataTable table = new DataTable();
                da.Fill(table);
                dgvAssistance.DataSource = table;
            }
        }

        private void LoadReports()
        {
            int cid = Helpers.SecurityContext.CenterFilterId;

            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();

                // مجموع کل کمک‌ها — فیلتر شده بر اساس مرکز فعال
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT COALESCE(SUM(a.Amount), 0)
FROM TblAssistance a
JOIN TblCase c ON c.CasID = a.CasID
WHERE (@CID = 0 OR c.CenterID = @CID)", con))
                {
                    cmd.Parameters.AddWithValue("@CID", cid);
                    lblTotal.Text = "مجموع کل کمک‌ها: " +
                        Convert.ToDecimal(cmd.ExecuteScalar()).ToString("N2");
                }

                // گزارش ماهانه — فیلتر شده بر اساس مرکز
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT strftime('%Y-%m', a.AssistanceDate) AS [ماه], SUM(a.Amount) AS [مجموع کمک]
FROM TblAssistance a
JOIN TblCase c ON c.CasID = a.CasID
WHERE (@CID = 0 OR c.CenterID = @CID)
GROUP BY strftime('%Y-%m', a.AssistanceDate)
ORDER BY [ماه] DESC", con))
                {
                    cmd.Parameters.AddWithValue("@CID", cid);
                    using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                    {
                        DataTable t = new DataTable();
                        da.Fill(t);
                        dgvMonthly.DataSource = t;
                    }
                }

                // پرونده‌های بدون کمک — فیلتر شده بر اساس مرکز
                using (SQLiteCommand cmdF = new SQLiteCommand(@"
SELECT c.CasID, c.FormNo, c.Code, c.HeadFullName, c.Phone, c.ServiceStatus
FROM TblCase c
WHERE NOT EXISTS (SELECT 1 FROM TblAssistance a WHERE a.CasID = c.CasID)
  AND (@CID = 0 OR c.CenterID = @CID)
ORDER BY c.CasID DESC", con))
                {
                    cmdF.Parameters.AddWithValue("@CID", cid);
                    using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmdF))
                    {
                        DataTable t = new DataTable();
                        da.Fill(t);
                        dgvWithoutAssistance.DataSource = t;
                    }
                }
            }
        }

        private void FillGrid(SQLiteConnection con, DataGridView grid, string query)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                DataTable table = new DataTable();
                da.Fill(table);
                grid.DataSource = table;
            }
        }

        private void ConfigureGrid(DataGridView grid)
        {
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            UiTheme.StyleGrid(grid);
        }

        private Label CreateLabel(string text, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Font = UiTheme.FontBold(UiTheme.SizeSmall);
            label.ForeColor = UiTheme.TextDark;
            label.TextAlign = ContentAlignment.MiddleRight;
            label.SetBounds(x, y, width, 25);
            return label;
        }

        // یک فیلد فیلتر: برچسب بالا + کنترل پایین، با اندازه یکسان (الگوی FrmAdvancedSearch).
        private Panel MakeFieldPanel(string labelText, Control input)
        {
            Panel p = new Panel();
            p.Width = 180;
            p.Height = 58;
            p.Margin = new Padding(6, 4, 6, 4);

            Label lbl = new Label();
            lbl.Text = labelText;
            lbl.AutoSize = false;
            lbl.Dock = DockStyle.Top;
            lbl.Height = 22;
            lbl.TextAlign = ContentAlignment.MiddleRight;
            lbl.Font = UiTheme.FontBold(UiTheme.SizeSmall);
            lbl.ForeColor = UiTheme.TextDark;

            input.Dock = DockStyle.Top;
            input.Width = 174;
            input.Font = UiTheme.Font(UiTheme.SizeBody);
            if (input is TextBox tb)
                tb.Height = 28;
            else if (input is NumericUpDown nud)
                nud.Height = 28;

            p.Controls.Add(input);
            p.Controls.Add(lbl);
            return p;
        }
    }
}

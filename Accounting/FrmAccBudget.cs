using System;
using System.Data;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    public sealed class FrmAccBudget : Form
    {
        private readonly AccountingRepo _repo = new AccountingRepo();
        private DataGridView _grid, _vs;
        private TextBox _title, _note;
        private ComboBox _period, _type, _cat;
        private NumericUpDown _amount;
        private int _id;

        public FrmAccBudget()
        {
            ErpAccess.RequirePermission(this, "Accounting.View");
            Text = "بودجه  ·  " + ProductBranding.CommercialName;
            AccountingChrome.MakeWorkspace(this, 1100, 680);

            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 320 };
            _grid = Grid(); _vs = Grid();
            _grid.CellClick += GridClick;
            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(_vs);
            split.Panel2.Controls.Add(new Label
            {
                Dock = DockStyle.Top, Height = 28, Text = "بودجه در برابر عملکرد",
                TextAlign = System.Drawing.ContentAlignment.MiddleRight, Padding = new Padding(8, 0, 8, 0)
            });

            FlowLayoutPanel form = ErpFormChrome.Toolbar();
            form.Height = 86; form.WrapContents = true;
            _title = new TextBox { Width = 180 }; _note = new TextBox { Width = 180 };
            _period = Combo(); _type = Combo(); _cat = Combo();
            _type.Items.AddRange(new object[] { "درآمد", "هزینه" });
            _type.SelectedIndex = 0;
            _type.SelectedIndexChanged += delegate { ReloadCats(); };
            _amount = new NumericUpDown { DecimalPlaces = 0, Maximum = 999999999, ThousandsSeparator = true, Width = 120 };
            form.Controls.Add(Lbl("عنوان")); form.Controls.Add(_title);
            form.Controls.Add(Lbl("دوره")); form.Controls.Add(_period);
            form.Controls.Add(Lbl("نوع")); form.Controls.Add(_type);
            form.Controls.Add(Lbl("دسته")); form.Controls.Add(_cat);
            form.Controls.Add(Lbl("مبلغ بودجه")); form.Controls.Add(_amount);
            form.Controls.Add(Lbl("توضیح")); form.Controls.Add(_note);

            FlowLayoutPanel tools = ErpFormChrome.Toolbar();
            tools.Controls.Add(Btn("ذخیره", delegate { Save(); }));
            tools.Controls.Add(Btn("جدید", delegate { _id = 0; _title.Text = ""; _note.Text = ""; _amount.Value = 0; }));
            tools.Controls.Add(Btn("حذف", delegate { Delete(); }));
            tools.Controls.Add(Btn("تازه‌سازی", delegate { Reload(); }));
            tools.Controls.Add(Btn("خروجی اکسل", delegate { AccountingChrome.ExportGrid(_grid); }));
            AccountingChrome.AttachQuickSearch(tools, _grid, "جستجوی بودجه...");

            Controls.Add(split);
            Controls.Add(AccountingChrome.BuildStatusBar());
            Controls.Add(tools);
            Controls.Add(form);
            Controls.Add(AccountingChrome.BuildHeader("بودجه و انحراف از عملکرد", AccountingChrome.Breadcrumb("بودجه")));
            AccountingChrome.Polish(this);

            Bind(_period, _repo.GetPeriodsForCombo(), "PeriodID", "Display");
            ReloadCats();
            Reload();
        }

        private void ReloadCats()
        {
            bool income = BudgetTypeCode() == "Income";
            Bind(_cat, _repo.GetCategoriesForCombo(income), "CatID", "Display");
        }

        private void Reload()
        {
            int? period = IntVal(_period);
            _grid.DataSource = _repo.GetBudgets(period);
            if (_grid.Columns.Contains("BudgetID")) _grid.Columns["BudgetID"].Visible = false;
            _vs.DataSource = _repo.GetBudgetVsActual(period);
        }

        private void GridClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || !_grid.Columns.Contains("BudgetID")) return;
            DataGridViewRow row = _grid.Rows[e.RowIndex];
            _id = Convert.ToInt32(row.Cells["BudgetID"].Value);
            _title.Text = Convert.ToString(row.Cells["عنوان"].Value);
            _amount.Value = Convert.ToDecimal(row.Cells["بودجه"].Value ?? 0);
            _note.Text = Convert.ToString(row.Cells["توضیح"].Value);
            string typ = Convert.ToString(row.Cells["نوع"].Value);
            _type.SelectedIndex = (typ == "هزینه" || typ == "Expense") ? 1 : 0;
        }

        private void Save()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Edit"))
            { UiTheme.ShowWarning(this, "اجازه ثبت ندارید."); return; }
            try
            {
                if (_id == 0)
                    _repo.AddBudget(IntVal(_period), BudgetTypeCode(), IntVal(_cat), _title.Text, (double)_amount.Value, _note.Text);
                else
                    _repo.UpdateBudget(_id, IntVal(_period), BudgetTypeCode(), IntVal(_cat), _title.Text, (double)_amount.Value, _note.Text);
                UiTheme.ShowSuccess(this, "بودجه ذخیره شد.");
                _id = 0;
                Reload();
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); }
            catch (Exception ex) { UiTheme.ShowError(this, ex.Message); }
        }

        private void Delete()
        {
            if (_id <= 0) { UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید."); return; }
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Edit"))
            { UiTheme.ShowWarning(this, "اجازه حذف ندارید."); return; }
            try { _repo.DeleteBudget(_id); _id = 0; Reload(); }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); }
        }

        private static DataGridView Grid()
        {
            DataGridView g = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(g);
            AccountingChrome.PolishGrid(g);
            return g;
        }
        private string BudgetTypeCode()
        {
            string t = _type != null ? (_type.Text ?? "") : "";
            if (t == "درآمد" || t == "Income") return "Income";
            return "Expense";
        }
        private static ComboBox Combo() { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 }; }
        private static Label Lbl(string t) { return new Label { Text = t, AutoSize = true, Padding = new Padding(8, 8, 4, 0) }; }
        private Button Btn(string t, EventHandler click)
        {
            Button b = UiTheme.CreateButton(t, "", UiTheme.Primary); b.AutoSize = true; b.Click += click; return b;
        }
        private static void Bind(ComboBox cmb, DataTable dt, string value, string display)
        {
            DataTable copy = dt.Copy();
            DataRow empty = copy.NewRow();
            empty[value] = DBNull.Value;
            empty[display] = "";
            copy.Rows.InsertAt(empty, 0);
            cmb.DataSource = copy;
            cmb.ValueMember = value;
            cmb.DisplayMember = display;
        }
        private static int? IntVal(ComboBox cmb)
        {
            if (cmb == null || cmb.SelectedValue == null || cmb.SelectedValue == DBNull.Value) return null;
            try { return Convert.ToInt32(cmb.SelectedValue); } catch { return null; }
        }
    }
}

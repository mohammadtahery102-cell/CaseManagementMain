using System;
using System.Data;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    public sealed class FrmAccCheques : Form
    {
        private readonly AccountingRepo _repo = new AccountingRepo();
        private DataGridView _grid;
        private TextBox _no, _date, _due, _note;
        private ComboBox _dir, _party, _fund, _period;
        private NumericUpDown _amount;
        private int _id;

        public FrmAccCheques()
        {
            ErpAccess.RequirePermission(this, "Accounting.View");
            Text = "چک‌ها  ·  " + ProductBranding.CommercialName;
            AccountingChrome.MakeWorkspace(this, 1100, 640);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(_grid);
            AccountingChrome.PolishGrid(_grid);
            _grid.CellClick += GridClick;

            FlowLayoutPanel form = ErpFormChrome.Toolbar();
            form.Height = 92;
            form.WrapContents = true;
            _no = Box(120); _date = Box(100); _due = Box(100); _note = Box(180);
            _dir = Combo(); _dir.Items.AddRange(new object[] { "دریافتی", "پرداختی" }); _dir.SelectedIndex = 0;
            _party = Combo(); _fund = Combo(); _period = Combo();
            _amount = new NumericUpDown { DecimalPlaces = 0, Maximum = 999999999, ThousandsSeparator = true, Width = 120 };
            form.Controls.Add(Lbl("شماره")); form.Controls.Add(_no);
            form.Controls.Add(Lbl("تاریخ")); form.Controls.Add(_date);
            form.Controls.Add(Lbl("سررسید")); form.Controls.Add(_due);
            form.Controls.Add(Lbl("نوع")); form.Controls.Add(_dir);
            form.Controls.Add(Lbl("طرف حساب")); form.Controls.Add(_party);
            form.Controls.Add(Lbl("صندوق")); form.Controls.Add(_fund);
            form.Controls.Add(Lbl("مبلغ")); form.Controls.Add(_amount);
            form.Controls.Add(Lbl("توضیح")); form.Controls.Add(_note);
            form.Controls.Add(Lbl("دوره وصول")); form.Controls.Add(_period);

            FlowLayoutPanel tools = ErpFormChrome.Toolbar();
            tools.Controls.Add(Btn("ذخیره", delegate { Save(); }, UiTheme.Success));
            tools.Controls.Add(Btn("جدید", delegate { _id = 0; _no.Text = ""; _note.Text = ""; _amount.Value = 0; }, UiTheme.Primary));
            tools.Controls.Add(Btn("وصول", delegate { SetStatus("وصول"); }, UiTheme.Primary));
            tools.Controls.Add(Btn("برگشت", delegate { SetStatus("برگشت"); }, UiTheme.Warning));
            tools.Controls.Add(Btn("باطل", delegate { SetStatus("باطل"); }, UiTheme.Danger));
            tools.Controls.Add(Btn("تازه‌سازی", delegate { Reload(); }, UiTheme.Primary));
            tools.Controls.Add(Btn("خروجی اکسل", delegate { AccountingChrome.ExportGrid(_grid); }, UiTheme.Primary));
            AccountingChrome.AttachQuickSearch(tools, _grid, "جستجوی چک...");

            Controls.Add(_grid);
            Controls.Add(AccountingChrome.BuildStatusBar());
            Controls.Add(tools);
            Controls.Add(form);
            Controls.Add(AccountingChrome.BuildHeader("مدیریت چک", AccountingChrome.Breadcrumb("چک‌ها")));
            AccountingChrome.Polish(this);

            Bind(_party, _repo.GetPartiesForCombo(), "PartyID", "Display");
            Bind(_fund, _repo.GetFundsForCombo(), "FundID", "Display");
            Bind(_period, _repo.GetPeriodsForCombo(), "PeriodID", "Display");
            Reload();
        }

        private void Reload()
        {
            _grid.DataSource = _repo.GetCheques();
            if (_grid.Columns.Contains("ChequeID")) _grid.Columns["ChequeID"].Visible = false;
            if (_grid.Columns.Contains("TxnID")) _grid.Columns["TxnID"].Visible = false;
        }

        private void GridClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || !_grid.Columns.Contains("ChequeID")) return;
            DataGridViewRow row = _grid.Rows[e.RowIndex];
            _id = Convert.ToInt32(row.Cells["ChequeID"].Value);
            _no.Text = Convert.ToString(row.Cells["شماره چک"].Value);
            _date.Text = Convert.ToString(row.Cells["تاریخ"].Value);
            _due.Text = Convert.ToString(row.Cells["سررسید"].Value);
            _dir.Text = Convert.ToString(row.Cells["نوع"].Value);
            _amount.Value = Convert.ToDecimal(row.Cells["مبلغ"].Value ?? 0);
            _note.Text = Convert.ToString(row.Cells["توضیح"].Value);
        }

        private void Save()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Edit"))
            { UiTheme.ShowWarning(this, "اجازه ثبت ندارید."); return; }
            try
            {
                if (_id == 0)
                    _repo.AddCheque(_no.Text, _date.Text, _due.Text, _dir.Text, IntVal(_party), IntVal(_fund),
                        (double)_amount.Value, _note.Text);
                else
                    _repo.UpdateCheque(_id, _no.Text, _date.Text, _due.Text, _dir.Text, IntVal(_party), IntVal(_fund),
                        (double)_amount.Value, _note.Text);
                UiTheme.ShowSuccess(this, "چک ذخیره شد.");
                _id = 0;
                Reload();
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); }
            catch (Exception ex) { UiTheme.ShowError(this, ex.Message); }
        }

        private void SetStatus(string status)
        {
            if (_id <= 0) { UiTheme.ShowWarning(this, "ابتدا یک چک را انتخاب کنید."); return; }
            string perm = status == "باطل" ? "Accounting.Reverse" : "Accounting.Edit";
            if (!CaseManagement.Enterprise.PermissionService.Require(perm))
            { UiTheme.ShowWarning(this, "اجازه این عملیات را ندارید."); return; }
            try
            {
                _repo.SetChequeStatus(_id, status, IntVal(_period));
                AccGlOutboxDrain.AfterAccCommit();
                UiTheme.ShowSuccess(this, "وضعیت چک به «" + status + "» تغییر کرد.");
                Reload();
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); }
            catch (Exception ex) { UiTheme.ShowError(this, ex.Message); }
        }

        private static TextBox Box(int w) { return new TextBox { Width = w }; }
        private static ComboBox Combo() { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 }; }
        private static Label Lbl(string t) { return new Label { Text = t, AutoSize = true, Padding = new Padding(8, 8, 4, 0) }; }
        private Button Btn(string t, EventHandler click, System.Drawing.Color color)
        {
            Button b = UiTheme.CreateButton(t, "", color); b.AutoSize = true; b.Click += click; return b;
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
            if (cmb.SelectedValue == null || cmb.SelectedValue == DBNull.Value) return null;
            try { return Convert.ToInt32(cmb.SelectedValue); } catch { return null; }
        }
    }
}

using System;
using System.Data;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    public sealed class FrmAccReturns : Form
    {
        private readonly AccountingRepo _repo = new AccountingRepo();
        private readonly string _kind;
        private DataGridView _grid;
        private TextBox _date, _orig, _desc;
        private ComboBox _party, _fund, _period;
        private NumericUpDown _amount;
        private int _id;

        public FrmAccReturns() : this("Sale") { }

        public FrmAccReturns(string kind)
        {
            _kind = kind == "Purchase" ? "Purchase" : "Sale";
            ErpAccess.RequirePermission(this, "Accounting.View");
            string title = _kind == "Sale" ? "برگشت فروش" : "برگشت خرید";
            Text = title + "  ·  " + ProductBranding.CommercialName;
            AccountingChrome.MakeWorkspace(this, 1000, 600);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(_grid);
            AccountingChrome.PolishGrid(_grid);
            _grid.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_grid.Columns.Contains("ReturnID")) return;
                _id = Convert.ToInt32(_grid.Rows[e.RowIndex].Cells["ReturnID"].Value);
            };

            FlowLayoutPanel form = ErpFormChrome.Toolbar();
            form.Height = 86; form.WrapContents = true;
            _date = new TextBox { Width = 110 }; _orig = new TextBox { Width = 120 }; _desc = new TextBox { Width = 200 };
            _party = Combo(); _fund = Combo(); _period = Combo();
            _amount = new NumericUpDown { DecimalPlaces = 0, Maximum = 999999999, ThousandsSeparator = true, Width = 120 };
            form.Controls.Add(Lbl("تاریخ")); form.Controls.Add(_date);
            form.Controls.Add(Lbl("دوره")); form.Controls.Add(_period);
            form.Controls.Add(Lbl("طرف حساب")); form.Controls.Add(_party);
            form.Controls.Add(Lbl("صندوق")); form.Controls.Add(_fund);
            form.Controls.Add(Lbl("مبلغ")); form.Controls.Add(_amount);
            form.Controls.Add(Lbl("سند اصلی")); form.Controls.Add(_orig);
            form.Controls.Add(Lbl("توضیح")); form.Controls.Add(_desc);

            FlowLayoutPanel tools = ErpFormChrome.Toolbar();
            tools.Controls.Add(Btn("ثبت برگشت", delegate { Save(); }, UiTheme.Success));
            tools.Controls.Add(Btn("ابطال برگشت", delegate { Void(); }, UiTheme.Danger));
            tools.Controls.Add(Btn("تازه‌سازی", delegate { Reload(); }, UiTheme.Primary));
            tools.Controls.Add(Btn("خروجی اکسل", delegate { AccountingChrome.ExportGrid(_grid); }, UiTheme.Primary));
            AccountingChrome.AttachQuickSearch(tools, _grid, "جستجوی برگشت...");

            Controls.Add(_grid);
            Controls.Add(AccountingChrome.BuildStatusBar());
            Controls.Add(tools);
            Controls.Add(form);
            Controls.Add(AccountingChrome.BuildHeader(title, AccountingChrome.Breadcrumb(title)));
            AccountingChrome.Polish(this);

            Bind(_party, _repo.GetPartiesForCombo(), "PartyID", "Display");
            Bind(_fund, _repo.GetFundsForCombo(), "FundID", "Display");
            Bind(_period, _repo.GetPeriodsForCombo(), "PeriodID", "Display");
            Reload();
        }

        private void Reload()
        {
            _grid.DataSource = _repo.GetTradeReturns(_kind);
            if (_grid.Columns.Contains("ReturnID")) _grid.Columns["ReturnID"].Visible = false;
            if (_grid.Columns.Contains("LinkedTxnID")) _grid.Columns["LinkedTxnID"].Visible = false;
        }

        private void Save()
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Edit"))
            { UiTheme.ShowWarning(this, "اجازه ثبت ندارید."); return; }
            int? fund = IntVal(_fund);
            int? period = IntVal(_period);
            if (fund == null || period == null)
            { UiTheme.ShowWarning(this, "صندوق و دوره مالی الزامی است."); return; }
            try
            {
                _repo.AddTradeReturn(_kind, _date.Text, IntVal(_party), fund.Value, period.Value,
                    (double)_amount.Value, _orig.Text, _desc.Text);
                AccGlOutboxDrain.AfterAccCommit();
                UiTheme.ShowSuccess(this, "برگشت ثبت شد و سند صندوق صادر گردید.");
                Reload();
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); }
            catch (Exception ex) { UiTheme.ShowError(this, ex.Message); }
        }

        private void Void()
        {
            if (_id <= 0) { UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید."); return; }
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Reverse"))
            { UiTheme.ShowWarning(this, "ابطال فقط برای مدیر مجاز است."); return; }
            try
            {
                _repo.VoidTradeReturn(_id, "ابطال برگشت از فرم حسابداری");
                AccGlOutboxDrain.AfterAccCommit();
                _id = 0;
                Reload();
            }
            catch (AccountingRuleException ex) { UiTheme.ShowWarning(this, ex.Message); }
            catch (Exception ex) { UiTheme.ShowError(this, ex.Message); }
        }

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

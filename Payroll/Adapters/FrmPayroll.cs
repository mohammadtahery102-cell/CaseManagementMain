using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Helpers;
using CaseManagement.Payroll.Application;
using CaseManagement.Payroll.Domain;

namespace CaseManagement.Payroll.Adapters
{
    public sealed class FrmPayroll : Form
    {
        private readonly ILedgerIdentity _identity;
        private readonly EmployeeService _emps;
        private readonly PayrollService _pay;
        private DataGridView _grid;
        private ComboBox _cmb;

        public FrmPayroll()
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _emps = new EmployeeService();
            _pay = new PayrollService();
            ErpAccess.RequirePermission(this, "Payroll.View");
            Text = "حقوق و دستمزد";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1000, 600);
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(_grid);
            FlowLayoutPanel flow = ErpFormChrome.Toolbar();
            _cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            _cmb.Items.AddRange(new object[] { "فهرست کارکنان", "خلاصه حقوق", "ثبت حقوق" });
            _cmb.SelectedIndex = 0;
            _cmb.SelectedIndexChanged += delegate { Reload(); };
            flow.Controls.Add(_cmb);
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { Reload(); }));
            Controls.Add(ErpFormChrome.WrapGrid(_grid, ProductBranding.EmptyList));
            Controls.Add(flow);
            Controls.Add(ErpFormChrome.Header("حقوق و دستمزد"));
            Reload();
        }

        private void Reload()
        {
            DataTable t = new DataTable();
            string kind = _cmb.SelectedItem == null ? "" : _cmb.SelectedItem.ToString();
            if (kind == "خلاصه حقوق")
            {
                t.Columns.Add("شماره"); t.Columns.Add("وضعیت"); t.Columns.Add("مبلغ", typeof(long));
                IList<PayrollSummaryRow> rows = _pay.Summary(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].DocNo, rows[i].Status, rows[i].TotalMinor);
            }
            else if (kind == "ثبت حقوق")
            {
                t.Columns.Add("شماره"); t.Columns.Add("وضعیت"); t.Columns.Add("مبلغ", typeof(long));
                IList<PayrollSummaryRow> rows = _pay.Summary(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].DocNo, rows[i].Status, rows[i].TotalMinor);
            }
            else
            {
                t.Columns.Add("کد"); t.Columns.Add("نام"); t.Columns.Add("حقوق", typeof(long));
                IList<EmployeeListRow> rows = _emps.List(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].Code, rows[i].Name, rows[i].GrossMinor);
            }
            _grid.DataSource = t;
        }
    }
}

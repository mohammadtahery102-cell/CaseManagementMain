using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Helpers;
using CaseManagement.Sales.Application;
using CaseManagement.Sales.Domain;

namespace CaseManagement.Sales.Adapters
{
    public sealed class FrmSales : Form
    {
        private readonly ILedgerIdentity _identity;
        private readonly SalesService _svc;
        private DataGridView _grid;
        private ComboBox _cmb;

        public FrmSales()
            : this(null)
        {
        }

        public FrmSales(string startKind)
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _svc = new SalesService();
            ErpAccess.RequirePermission(this, "Sales.View");
            Text = "فروش";
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
            _cmb.Items.AddRange(new object[] { "سفارش‌های باز", "فروش مشتری", "تاریخچه فروش", "تحلیل درآمد" });
            _cmb.SelectedIndex = 0;
            _cmb.SelectedIndexChanged += delegate { Reload(); };
            flow.Controls.Add(_cmb);
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { Reload(); }));
            Controls.Add(ErpFormChrome.WrapGrid(_grid, ProductBranding.EmptyList));
            Controls.Add(flow);
            Controls.Add(ErpFormChrome.Header("فروش"));
            ErpFormChrome.SelectListItem(_cmb, startKind);
            Reload();
        }

        private void Reload()
        {
            DataTable t = new DataTable();
            string kind = _cmb.SelectedItem == null ? "" : _cmb.SelectedItem.ToString();
            if (kind == "فروش مشتری")
            {
                t.Columns.Add("کد"); t.Columns.Add("نام"); t.Columns.Add("مبلغ", typeof(long));
                IList<CustomerSalesRow> rows = _svc.CustomerSales(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].CustomerCode, rows[i].CustomerName, rows[i].AmountMinor);
            }
            else if (kind == "تاریخچه فروش")
            {
                t.Columns.Add("شماره"); t.Columns.Add("وضعیت"); t.Columns.Add("تاریخ"); t.Columns.Add("مبلغ", typeof(long));
                IList<SalInvoice> rows = _svc.SalesHistory(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].DocNo, rows[i].Status, rows[i].InvoiceDate, rows[i].AmountMinor);
            }
            else if (kind == "تحلیل درآمد")
            {
                t.Columns.Add("شماره"); t.Columns.Add("تاریخ"); t.Columns.Add("مبلغ", typeof(long));
                IList<RevenueRow> rows = _svc.RevenueAnalysis(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].InvoiceNo, rows[i].InvoiceDate, rows[i].AmountMinor);
            }
            else
            {
                t.Columns.Add("شماره"); t.Columns.Add("مشتری"); t.Columns.Add("وضعیت"); t.Columns.Add("تاریخ");
                IList<OpenSalesOrderRow> rows = _svc.OpenOrders(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].DocNo, rows[i].CustomerName, rows[i].Status, rows[i].OrderDate);
            }
            _grid.DataSource = t;
        }
    }
}

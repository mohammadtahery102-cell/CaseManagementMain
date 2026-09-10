using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Helpers;
using CaseManagement.Pos.Application;
using CaseManagement.Pos.Domain;

namespace CaseManagement.Pos.Adapters
{
    public sealed class FrmPos : Form
    {
        private readonly ILedgerIdentity _identity;
        private readonly PosService _svc;
        private DataGridView _grid;
        private ComboBox _cmb;

        public FrmPos()
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _svc = new PosService();
            ErpAccess.RequirePermission(this, "POS.View");
            Text = "صندوق فروش";
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
            _cmb.Items.AddRange(new object[] { "فروش روزانه", "خلاصه نقد", "تراکنش‌ها" });
            _cmb.SelectedIndex = 0;
            _cmb.SelectedIndexChanged += delegate { Reload(); };
            flow.Controls.Add(_cmb);
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { Reload(); }));
            Controls.Add(ErpFormChrome.WrapGrid(_grid, ProductBranding.EmptyList));
            Controls.Add(flow);
            Controls.Add(ErpFormChrome.Header("صندوق فروش"));
            Reload();
        }

        private void Reload()
        {
            DataTable t = new DataTable();
            string kind = _cmb.SelectedItem == null ? "" : _cmb.SelectedItem.ToString();
            if (kind == "خلاصه نقد")
            {
                t.Columns.Add("صندوق"); t.Columns.Add("مبلغ", typeof(long));
                IList<CashSummaryRow> rows = _svc.CashSummary(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].TerminalCode, rows[i].AmountMinor);
            }
            else if (kind == "تراکنش‌ها")
            {
                t.Columns.Add("نوع"); t.Columns.Add("شماره"); t.Columns.Add("مبلغ", typeof(long)); t.Columns.Add("وضعیت");
                IList<PosTxnRow> rows = _svc.Transactions(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].Kind, rows[i].DocNo, rows[i].AmountMinor, rows[i].Status);
            }
            else
            {
                t.Columns.Add("شماره"); t.Columns.Add("تاریخ"); t.Columns.Add("مبلغ", typeof(long)); t.Columns.Add("وضعیت");
                IList<DailySalesRow> rows = _svc.DailySales(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].DocNo, rows[i].SaleDate, rows[i].AmountMinor, rows[i].Status);
            }
            _grid.DataSource = t;
        }
    }
}

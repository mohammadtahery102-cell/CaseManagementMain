using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Helpers;
using CaseManagement.Purchase.Application;
using CaseManagement.Purchase.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Purchase.Adapters
{
    public sealed class FrmPurchase : Form
    {
        private readonly ILedgerIdentity _identity;
        private readonly PurchaseService _svc;
        private DataGridView _grid;
        private ComboBox _cmb;

        public FrmPurchase()
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _svc = new PurchaseService();
            Text = "خرید";
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
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, RightToLeft = RightToLeft.Yes };
            _cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            _cmb.Items.AddRange(new object[] { "سفارش‌های باز", "خرید تامین‌کننده", "تاریخچه خرید", "گزارش GR/IR" });
            _cmb.SelectedIndex = 0;
            _cmb.SelectedIndexChanged += delegate { Reload(); };
            flow.Controls.Add(_cmb);
            Button b = UiTheme.CreateButton("تازه‌سازی", "", UiTheme.PrimaryLight);
            b.Click += delegate { Reload(); };
            flow.Controls.Add(b);
            Controls.Add(_grid);
            Controls.Add(flow);
            Reload();
        }

        private void Reload()
        {
            DataTable t = new DataTable();
            string kind = _cmb.SelectedItem == null ? "" : _cmb.SelectedItem.ToString();
            if (kind == "خرید تامین‌کننده")
            {
                t.Columns.Add("کد"); t.Columns.Add("نام"); t.Columns.Add("مبلغ", typeof(long));
                IList<VendorPurchaseRow> rows = _svc.VendorPurchases(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].VendorCode, rows[i].VendorName, rows[i].AmountMinor);
            }
            else if (kind == "تاریخچه خرید")
            {
                t.Columns.Add("شماره"); t.Columns.Add("وضعیت"); t.Columns.Add("تاریخ"); t.Columns.Add("مبلغ", typeof(long));
                IList<PurInvoice> rows = _svc.PurchaseHistory(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].DocNo, rows[i].Status, rows[i].InvoiceDate, rows[i].AmountMinor);
            }
            else if (kind == "گزارش GR/IR")
            {
                t.Columns.Add("رسید"); t.Columns.Add("فاکتور"); t.Columns.Add("مبلغ فاکتور", typeof(long));
                IList<GrIrRow> rows = _svc.GrIrReport(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].ReceiptNo, rows[i].InvoiceNo, rows[i].InvoiceAmountMinor);
            }
            else
            {
                t.Columns.Add("شماره"); t.Columns.Add("تامین‌کننده"); t.Columns.Add("وضعیت"); t.Columns.Add("تاریخ");
                IList<OpenPurchaseOrderRow> rows = _svc.OpenOrders(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].DocNo, rows[i].VendorName, rows[i].Status, rows[i].OrderDate);
            }
            _grid.DataSource = t;
        }
    }
}

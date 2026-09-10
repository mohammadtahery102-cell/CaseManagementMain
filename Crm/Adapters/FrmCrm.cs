using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Crm.Application;
using CaseManagement.Crm.Domain;
using CaseManagement.Helpers;

namespace CaseManagement.Crm.Adapters
{
    public sealed class FrmCrm : Form
    {
        private readonly ILedgerIdentity _identity;
        private readonly CrmReporting _reports;
        private DataGridView _grid;
        private ComboBox _cmb;

        public FrmCrm()
            : this(null)
        {
        }

        public FrmCrm(string startKind)
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _reports = new CrmReporting();
            ErpAccess.RequirePermission(this, "CRM.View");
            Text = ProductMode.IsErp ? "ارتباط با مشتریان" : "CRM";
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
            _cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            _cmb.Items.AddRange(new object[] { "پایپلاین سرنخ", "پایپلاین فرصت", "قیف فروش", "فعالیت مشتری", "پیگیری" });
            _cmb.SelectedIndex = 0;
            _cmb.SelectedIndexChanged += delegate { Reload(); };
            flow.Controls.Add(_cmb);
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { Reload(); }));
            Controls.Add(ErpFormChrome.WrapGrid(_grid, ProductBranding.EmptyList));
            Controls.Add(flow);
            Controls.Add(ErpFormChrome.Header("ارتباط با مشتریان"));
            ErpFormChrome.SelectListItem(_cmb, startKind);
            Reload();
        }

        private void Reload()
        {
            DataTable t = new DataTable();
            string kind = _cmb.SelectedItem == null ? "" : _cmb.SelectedItem.ToString();
            if (kind == "پایپلاین فرصت")
            {
                t.Columns.Add("مرحله"); t.Columns.Add("وضعیت"); t.Columns.Add("کد"); t.Columns.Add("نام"); t.Columns.Add("مبلغ", typeof(long));
                IList<OpportunityPipelineRow> rows = _reports.OpportunityPipeline(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].Stage, rows[i].Status, rows[i].Code, rows[i].Name, rows[i].ExpectedAmountMinor);
            }
            else if (kind == "قیف فروش")
            {
                t.Columns.Add("مرحله"); t.Columns.Add("کد"); t.Columns.Add("نام"); t.Columns.Add("پیشنهاد"); t.Columns.Add("سفارش"); t.Columns.Add("فاکتور"); t.Columns.Add("مبلغ", typeof(long));
                IList<SalesFunnelRow> rows = _reports.SalesFunnel(_identity);
                for (int i = 0; i < rows.Count; i++)
                    t.Rows.Add(rows[i].Stage, rows[i].Code, rows[i].Name, rows[i].QuoteId, rows[i].OrderId, rows[i].InvoiceId, rows[i].InvoiceAmountMinor);
            }
            else if (kind == "فعالیت مشتری")
            {
                t.Columns.Add("زمان"); t.Columns.Add("نوع"); t.Columns.Add("شرح");
                IList<CustomerActivityRow> rows = _reports.CustomerActivity(0, _identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].OccurredAt, rows[i].EventType, rows[i].Summary);
            }
            else if (kind == "پیگیری")
            {
                t.Columns.Add("عنوان"); t.Columns.Add("موعد"); t.Columns.Add("وضعیت"); t.Columns.Add("سررسید");
                IList<FollowUpRow> rows = _reports.FollowUps(System.DateTime.UtcNow.ToString("yyyy-MM-dd"), _identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].Title, rows[i].DueDate, rows[i].Status, rows[i].Overdue);
            }
            else
            {
                t.Columns.Add("وضعیت"); t.Columns.Add("کد"); t.Columns.Add("نام");
                IList<LeadPipelineRow> rows = _reports.LeadPipeline(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].Status, rows[i].Code, rows[i].Name);
            }
            _grid.DataSource = t;
        }
    }
}

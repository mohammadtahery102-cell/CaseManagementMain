using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Assets.Application;
using CaseManagement.Assets.Domain;
using CaseManagement.Helpers;

namespace CaseManagement.Assets.Adapters
{
    public sealed class FrmAssets : Form
    {
        private readonly ILedgerIdentity _identity;
        private readonly AssetService _svc;
        private readonly DepreciationService _dep;
        private DataGridView _grid;
        private ComboBox _cmb;

        public FrmAssets()
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _svc = new AssetService();
            _dep = new DepreciationService();
            Text = "دارایی ثابت";
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
            _cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            _cmb.Items.AddRange(new object[] { "ثبت دارایی", "گزارش استهلاک", "حرکت دارایی" });
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
            if (kind == "گزارش استهلاک")
            {
                t.Columns.Add("شماره"); t.Columns.Add("تاریخ"); t.Columns.Add("مبلغ", typeof(long)); t.Columns.Add("وضعیت");
                IList<DepreciationReportRow> rows = _dep.Report(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].DocNo, rows[i].DocDate, rows[i].TotalMinor, rows[i].Status);
            }
            else if (kind == "حرکت دارایی")
            {
                t.Columns.Add("نوع"); t.Columns.Add("شماره"); t.Columns.Add("تاریخ"); t.Columns.Add("دارایی");
                IList<AssetMovementRow> rows = _svc.Movements(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].Kind, rows[i].DocNo, rows[i].DocDate, rows[i].AssetCode);
            }
            else
            {
                t.Columns.Add("کد"); t.Columns.Add("نام"); t.Columns.Add("وضعیت"); t.Columns.Add("بهای تمام‌شده", typeof(long)); t.Columns.Add("استهلاک", typeof(long)); t.Columns.Add("ارزش دفتری", typeof(long));
                IList<AssetRegisterRow> rows = _svc.Register(_identity);
                for (int i = 0; i < rows.Count; i++) t.Rows.Add(rows[i].Code, rows[i].Name, rows[i].Status, rows[i].CostMinor, rows[i].AccumDepMinor, rows[i].NbvMinor);
            }
            _grid.DataSource = t;
        }
    }
}

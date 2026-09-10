using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting.Ledger.Adapters
{
    public sealed class FrmCashBookGl : Form
    {
        private readonly ILedgerIdentity _identity;
        private readonly ICashBookGlService _svc;
        private readonly IChartOfAccountsService _coa;
        private DataGridView _gridTxn;
        private DataGridView _gridMap;
        private ComboBox _cmbKind;

        public FrmCashBookGl()
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _svc = new CashBookGlService();
            _coa = new ChartOfAccountsService();
            Text = "صندوق → دفتر کل";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1100, 640);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            tabs.TabPages.Add(BuildTxnTab());
            tabs.TabPages.Add(BuildMapTab());
            Controls.Add(tabs);
            ReloadTxn();
            ReloadMap();
        }

        private TabPage BuildTxnTab()
        {
            TabPage p = new TabPage("اسناد صندوق");
            _gridTxn = Grid();
            p.Controls.Add(_gridTxn);
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, RightToLeft = RightToLeft.Yes };
            flow.Controls.Add(Btn("ارسال به دفتر کل", PostSelected));
            flow.Controls.Add(Btn("برگشت در صورت ابطال", ReverseSelected));
            flow.Controls.Add(Btn("تازه‌سازی", ReloadTxn));
            p.Controls.Add(flow);
            return p;
        }

        private TabPage BuildMapTab()
        {
            TabPage p = new TabPage("نگاشت حساب");
            _gridMap = Grid();
            p.Controls.Add(_gridMap);
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, RightToLeft = RightToLeft.Yes };
            _cmbKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            _cmbKind.Items.Add(LedgerCodes.MapFund);
            _cmbKind.Items.Add(LedgerCodes.MapIncome);
            _cmbKind.Items.Add(LedgerCodes.MapExpense);
            _cmbKind.SelectedIndex = 0;
            _cmbKind.SelectedIndexChanged += delegate { ReloadMap(); };
            flow.Controls.Add(new Label { Text = "نوع", AutoSize = true, Padding = new Padding(8, 8, 4, 0) });
            flow.Controls.Add(_cmbKind);
            flow.Controls.Add(Btn("نگاشت جدید", AddMap));
            p.Controls.Add(flow);
            return p;
        }

        private void ReloadTxn()
        {
            IList<CashBookTxn> list = _svc.ListTransactions(_identity);
            DataTable t = new DataTable();
            t.Columns.Add("Id", typeof(long));
            t.Columns.Add("شماره");
            t.Columns.Add("تاریخ");
            t.Columns.Add("ISO");
            t.Columns.Add("نوع");
            t.Columns.Add("مبلغ");
            t.Columns.Add("وضعیت Acc");
            t.Columns.Add("دفتر کل");
            for (int i = 0; i < list.Count; i++)
            {
                CashBookTxn x = list[i];
                t.Rows.Add(x.TxnId, x.DocNo, x.TxnDateRaw, x.PostingDateIso, x.Direction, x.AmountMajor,
                    x.IsReversed ? "باطل" : "معتبر", x.GlPosted ? "ثبت شده" : "نشده");
            }
            _gridTxn.DataSource = t;
            if (_gridTxn.Columns.Contains("Id")) _gridTxn.Columns["Id"].Visible = false;
        }

        private void ReloadMap()
        {
            string kind = _cmbKind.SelectedItem != null ? _cmbKind.SelectedItem.ToString() : "";
            IList<GlCashBookMap> maps = _svc.ListMaps(Company(), kind);
            DataTable t = new DataTable();
            t.Columns.Add("Id", typeof(long));
            t.Columns.Add("RV", typeof(long));
            t.Columns.Add("منبع", typeof(long));
            t.Columns.Add("حساب دفتر کل");
            for (int i = 0; i < maps.Count; i++)
                t.Rows.Add(maps[i].MapId, maps[i].RowVersion, maps[i].SourceId, maps[i].SourceName);
            _gridMap.DataSource = t;
            if (_gridMap.Columns.Contains("Id")) _gridMap.Columns["Id"].Visible = false;
            if (_gridMap.Columns.Contains("RV")) _gridMap.Columns["RV"].Visible = false;
        }

        private void PostSelected()
        {
            long id = SelectedId(_gridTxn);
            if (id <= 0) return;
            Show(_svc.PostTransaction(id, _identity));
            ReloadTxn();
        }

        private void ReverseSelected()
        {
            long id = SelectedId(_gridTxn);
            if (id <= 0) return;
            Show(_svc.ReverseIfVoided(id, _identity));
            ReloadTxn();
        }

        private void AddMap()
        {
            string kind = _cmbKind.SelectedItem != null ? _cmbKind.SelectedItem.ToString() : LedgerCodes.MapFund;
            using (Form dlg = new Form
            {
                Text = "نگاشت", Width = 420, Height = 220, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true,
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false
            })
            {
                ComboBox src = new ComboBox { Left = 20, Top = 40, Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };
                ComboBox acc = new ComboBox { Left = 20, Top = 100, Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };
                dlg.Controls.Add(new Label { Text = "منبع Acc", Left = 20, Top = 18, AutoSize = true });
                dlg.Controls.Add(new Label { Text = "حساب برگ", Left = 20, Top = 78, AutoSize = true });
                IList<KeyValuePair<long, string>> sources = _svc.ListMappableSources(kind, _identity);
                for (int i = 0; i < sources.Count; i++)
                    src.Items.Add(new PairItem(sources[i].Key, sources[i].Value));
                IList<GlAccount> accounts = _coa.List(Company(), false);
                for (int i = 0; i < accounts.Count; i++)
                    if (accounts[i].IsLeaf && accounts[i].AllowPosting)
                        acc.Items.Add(new PairItem(accounts[i].AccountId, accounts[i].AccountCode + " " + accounts[i].AccountName));
                if (src.Items.Count > 0) src.SelectedIndex = 0;
                if (acc.Items.Count > 0) acc.SelectedIndex = 0;
                dlg.Controls.Add(src);
                dlg.Controls.Add(acc);
                Button ok = UiTheme.CreateButton("ثبت", "", UiTheme.PrimaryLight);
                ok.Left = 20; ok.Top = 140;
                ok.Click += delegate
                {
                    PairItem s = src.SelectedItem as PairItem;
                    PairItem a = acc.SelectedItem as PairItem;
                    if (s == null || a == null) return;
                    LedgerResult r = _svc.UpsertMap(new UpsertCashBookMapCommand
                    {
                        CompanyId = Company(),
                        MapKind = kind,
                        SourceId = s.Id,
                        AccountId = a.Id
                    }, _identity);
                    Show(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            ReloadMap();
        }

        private int Company()
        {
            return _identity.CompanyId > 0 ? _identity.CompanyId : LedgerCodes.DefaultCompanyId;
        }

        private void Show(LedgerResult r)
        {
            if (r == null) return;
            if (r.Ok) UiTheme.ShowSuccess(this, "انجام شد.");
            else UiTheme.ShowWarning(this, LedgerUiText.Error(r.ErrorCode, r.Message));
        }

        private static long SelectedId(DataGridView grid)
        {
            if (grid.CurrentRow == null || grid.CurrentRow.Cells["Id"].Value == null) return 0;
            return Convert.ToInt64(grid.CurrentRow.Cells["Id"].Value);
        }

        private Button Btn(string text, Action click)
        {
            Button b = UiTheme.CreateButton(text, "", UiTheme.PrimaryLight);
            b.AutoSize = true;
            b.Click += delegate { click(); };
            return b;
        }

        private static DataGridView Grid()
        {
            DataGridView g = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(g);
            return g;
        }

        private sealed class PairItem
        {
            public long Id;
            public string Name;
            public PairItem(long id, string name) { Id = id; Name = name; }
            public override string ToString() { return Name; }
        }
    }
}

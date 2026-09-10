using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Helpers;
using CaseManagement.Inventory.Application;
using CaseManagement.Inventory.Domain;

namespace CaseManagement.Inventory.Adapters
{
    public sealed class FrmInventory : Form
    {
        private readonly ILedgerIdentity _identity;
        private readonly InventoryPostingService _posting;
        private readonly InventoryQueryService _query;
        private DataGridView _gridItems;
        private DataGridView _gridDocs;
        private DataGridView _gridReport;
        private ComboBox _cmbReport;

        public const int TabItems = 0;
        public const int TabDocuments = 1;
        public const int TabReports = 2;
        public const string ReportStockOnHand = "موجودی انبار";
        public const string ReportValuation = "ارزش‌گذاری";
        public const string ReportKardex = "دفتر موجودی";
        public const string ReportReorder = "نقطه سفارش";
        public const string ReportVsGl = "مغایرت موجودی و دفتر کل";

        public FrmInventory()
            : this("موجودی کالا", TabItems, null)
        {
        }

        public FrmInventory(string title, int startTab, string reportKind)
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _posting = new InventoryPostingService();
            _query = new InventoryQueryService();
            ErpAccess.RequirePermission(this, "Inventory.View");
            string heading = string.IsNullOrWhiteSpace(title) ? "موجودی کالا" : title;
            Text = heading;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1100, 640);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            tabs.TabPages.Add(BuildItemsTab());
            tabs.TabPages.Add(BuildDocsTab());
            tabs.TabPages.Add(BuildReportTab());
            if (startTab >= 0 && startTab < tabs.TabPages.Count)
                tabs.SelectedIndex = startTab;
            SelectReport(reportKind);
            Controls.Add(tabs);
            Controls.Add(ErpFormChrome.Header(heading));
            ReloadItems();
            ReloadDocs();
            ReloadReport();
        }

        private void SelectReport(string reportKind)
        {
            if (_cmbReport == null || string.IsNullOrWhiteSpace(reportKind)) return;
            int idx = _cmbReport.Items.IndexOf(reportKind);
            if (idx >= 0)
                _cmbReport.SelectedIndex = idx;
        }

        private TabPage BuildItemsTab()
        {
            TabPage p = new TabPage("کالا");
            _gridItems = Grid();
            FlowLayoutPanel flow = ErpFormChrome.Toolbar();
            flow.Controls.Add(Btn("کالای جدید", NewItem));
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { ReloadItems(); }));
            p.Controls.Add(ErpFormChrome.WrapGrid(_gridItems, ProductBranding.EmptyList));
            p.Controls.Add(flow);
            return p;
        }

        private TabPage BuildDocsTab()
        {
            TabPage p = new TabPage("اسناد");
            _gridDocs = Grid();
            FlowLayoutPanel flow = ErpFormChrome.Toolbar();
            flow.Controls.Add(Btn("رسید", delegate { NewDoc(InventoryCodes.TypeReceipt); }));
            flow.Controls.Add(Btn("حواله", delegate { NewDoc(InventoryCodes.TypeIssue); }));
            flow.Controls.Add(Btn("تعدیل", delegate { NewDoc(InventoryCodes.TypeAdjustment); }));
            flow.Controls.Add(Btn("ثبت قطعی", PostSelected));
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { ReloadDocs(); }));
            p.Controls.Add(ErpFormChrome.WrapGrid(_gridDocs, ProductBranding.EmptyList));
            p.Controls.Add(flow);
            return p;
        }

        private TabPage BuildReportTab()
        {
            TabPage p = new TabPage("گزارش");
            _gridReport = Grid();
            FlowLayoutPanel flow = ErpFormChrome.Toolbar();
            _cmbReport = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            _cmbReport.Items.Add(ReportStockOnHand);
            _cmbReport.Items.Add(ReportValuation);
            _cmbReport.Items.Add(ReportKardex);
            _cmbReport.Items.Add(ReportReorder);
            _cmbReport.Items.Add(ReportVsGl);
            _cmbReport.SelectedIndex = 0;
            _cmbReport.SelectedIndexChanged += delegate { ReloadReport(); };
            flow.Controls.Add(_cmbReport);
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { ReloadReport(); }));
            p.Controls.Add(ErpFormChrome.WrapGrid(_gridReport, ProductBranding.EmptyList));
            p.Controls.Add(flow);
            return p;
        }

        private void NewItem()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "کالای جدید";
                dlg.RightToLeft = RightToLeft.Yes;
                dlg.Width = 360;
                dlg.Height = 220;
                TextBox code = Field(dlg, "کد", 20, 20);
                TextBox name = Field(dlg, "نام", 20, 70);
                Button ok = UiTheme.CreateButton("ثبت", "", UiTheme.PrimaryLight);
                ok.Left = 20;
                ok.Top = 120;
                ok.Click += delegate
                {
                    InventoryResult r = _posting.CreateItem(new InvItem
                    {
                        Code = code.Text.Trim(),
                        Name = name.Text.Trim(),
                        CompanyId = Company()
                    }, _identity);
                    Show(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            ReloadItems();
        }

        private void NewDoc(string type)
        {
            IList<InvItem> items = _query.ListItems(_identity);
            if (items.Count == 0)
            {
                UiTheme.ShowWarning(this, "ابتدا یک کالا تعریف کنید.");
                return;
            }
            using (Form dlg = new Form())
            {
                dlg.Text = type;
                dlg.RightToLeft = RightToLeft.Yes;
                dlg.Width = 400;
                dlg.Height = 320;
                ComboBox cmb = new ComboBox { Left = 20, Top = 40, Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
                for (int i = 0; i < items.Count; i++)
                    cmb.Items.Add(new PairItem(items[i].ItemId, items[i].Code + " " + items[i].Name));
                cmb.SelectedIndex = 0;
                dlg.Controls.Add(new Label { Text = "کالا", Left = 20, Top = 18, AutoSize = true });
                dlg.Controls.Add(cmb);
                TextBox qty = Field(dlg, "مقدار", 20, 80);
                qty.Text = "1";
                TextBox cost = Field(dlg, "بهای واحد (minor)", 20, 130);
                cost.Text = type == InventoryCodes.TypeIssue ? "0" : "100";
                Button ok = UiTheme.CreateButton("ثبت و ارسال", "", UiTheme.PrimaryLight);
                ok.Left = 20;
                ok.Top = 200;
                ok.Click += delegate
                {
                    PairItem it = cmb.SelectedItem as PairItem;
                    long q;
                    long c;
                    if (it == null || !long.TryParse(qty.Text.Trim(), out q) || !long.TryParse(cost.Text.Trim(), out c))
                    {
                        UiTheme.ShowWarning(dlg, "مقدار و بهای واحد را به‌صورت عدد معتبر وارد کنید.");
                        return;
                    }
                    if (type == InventoryCodes.TypeAdjustment && q == 0)
                    {
                        UiTheme.ShowWarning(dlg, "مقدار تعدیل نمی‌تواند صفر باشد.");
                        return;
                    }
                    InventoryPostCommand cmd = new InventoryPostCommand();
                    cmd.CompanyId = Company();
                    cmd.DocumentType = type;
                    cmd.PostingDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    cmd.WarehouseId = _query.DefaultWarehouseId(Company());
                    cmd.Description = type;
                    InvDocumentLine line = new InvDocumentLine();
                    line.ItemId = it.Id;
                    line.QtyBase = q;
                    line.QtyDoc = q;
                    line.UnitCostMinor = c;
                    cmd.Lines = new List<InvDocumentLine> { line };
                    InventoryResult r = _posting.CreateAndPost(cmd, _identity);
                    ShowInv(r);
                    if (r.Ok)
                    {
                        AccGlOutboxDrain.AfterAccCommit();
                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    }
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            ReloadDocs();
            ReloadReport();
        }

        private void PostSelected()
        {
            long id = SelectedId(_gridDocs);
            if (id <= 0) return;
            InventoryResult r = _posting.Post(id, _identity);
            ShowInv(r);
            if (r.Ok) AccGlOutboxDrain.AfterAccCommit();
            ReloadDocs();
            ReloadReport();
        }

        private void ReloadItems()
        {
            DataTable t = new DataTable();
            t.Columns.Add("Id", typeof(long));
            t.Columns.Add("کد");
            t.Columns.Add("نام");
            IList<InvItem> list = _query.ListItems(_identity);
            for (int i = 0; i < list.Count; i++)
                t.Rows.Add(list[i].ItemId, list[i].Code, list[i].Name);
            _gridItems.DataSource = t;
        }

        private void ReloadDocs()
        {
            DataTable t = new DataTable();
            t.Columns.Add("Id", typeof(long));
            t.Columns.Add("شماره");
            t.Columns.Add("نوع");
            t.Columns.Add("وضعیت");
            t.Columns.Add("تاریخ");
            IList<InvDocument> list = _query.ListDocuments(_identity);
            for (int i = 0; i < list.Count; i++)
                t.Rows.Add(list[i].DocumentId, list[i].DocNo, list[i].DocumentType, list[i].Status, list[i].PostingDate);
            _gridDocs.DataSource = t;
        }

        private void ReloadReport()
        {
            DataTable t = new DataTable();
            string kind = _cmbReport.SelectedItem == null ? ReportStockOnHand : _cmbReport.SelectedItem.ToString();
            if (kind == ReportKardex)
            {
                t.Columns.Add("کالا", typeof(long));
                t.Columns.Add("نوع");
                t.Columns.Add("مقدار", typeof(long));
                t.Columns.Add("ارزش", typeof(long));
                t.Columns.Add("تاریخ");
                IList<InvItemLedger> rows = _query.Ledger(0, _identity);
                for (int i = 0; i < rows.Count; i++)
                    t.Rows.Add(rows[i].ItemId, rows[i].DocumentType, rows[i].QtyBase, rows[i].ValueMinor, rows[i].PostingDate);
            }
            else if (kind == ReportReorder)
            {
                t.Columns.Add("کد");
                t.Columns.Add("نام");
                t.Columns.Add("حداقل", typeof(long));
                t.Columns.Add("موجودی", typeof(long));
                IList<ReorderRow> rows = _query.Reorder(_identity);
                for (int i = 0; i < rows.Count; i++)
                    t.Rows.Add(rows[i].ItemCode, rows[i].ItemName, rows[i].MinQtyBase, rows[i].QuantityOnHand);
            }
            else if (kind == ReportVsGl)
            {
                t.Columns.Add("حساب");
                t.Columns.Add("ارزش موجودی", typeof(long));
                t.Columns.Add("مانده دفتر کل", typeof(long));
                t.Columns.Add("اختلاف", typeof(long));
                InventoryVsGlRow row = _query.VsGl(_identity);
                t.Rows.Add(row.AccountCode, row.InventoryValueMinor, row.GlNetMinor, row.DifferenceMinor);
            }
            else
            {
                t.Columns.Add("کد");
                t.Columns.Add("نام");
                t.Columns.Add("انبار");
                t.Columns.Add("محل");
                t.Columns.Add("مقدار", typeof(long));
                t.Columns.Add("ارزش", typeof(long));
                t.Columns.Add("میانگین", typeof(long));
                IList<StockOnHandRow> rows = _query.StockOnHand(_identity);
                for (int i = 0; i < rows.Count; i++)
                    t.Rows.Add(rows[i].ItemCode, rows[i].ItemName, rows[i].WarehouseCode, rows[i].LocationCode,
                        rows[i].QuantityOnHand, rows[i].InventoryValueMinor, rows[i].AverageCostMinor);
            }
            _gridReport.DataSource = t;
        }

        private int Company()
        {
            return _identity.CompanyId > 0 ? _identity.CompanyId : LedgerCodes.DefaultCompanyId;
        }

        private void ShowInv(InventoryResult r)
        {
            if (r == null) return;
            if (r.Ok) UiTheme.ShowSuccess(this, ProductBranding.SavedOk);
            else UiTheme.ShowWarning(this, r.ErrorCode + " " + r.Message);
        }

        private void Show(InventoryResult r)
        {
            ShowInv(r);
        }

        private static long SelectedId(DataGridView grid)
        {
            if (grid.CurrentRow == null || grid.CurrentRow.Cells["Id"].Value == null) return 0;
            return Convert.ToInt64(grid.CurrentRow.Cells["Id"].Value);
        }

        private static TextBox Field(Form dlg, string label, int left, int top)
        {
            dlg.Controls.Add(new Label { Text = label, Left = left, Top = top, AutoSize = true });
            TextBox t = new TextBox { Left = left, Top = top + 18, Width = 340 };
            dlg.Controls.Add(t);
            return t;
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
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
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

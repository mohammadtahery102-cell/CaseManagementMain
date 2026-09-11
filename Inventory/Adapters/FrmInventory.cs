using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
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
        private readonly InventoryItemService _items;
        private readonly string _masterFocus;
        private DataGridView _gridItems;
        private DataGridView _gridDocs;
        private DataGridView _gridWh;
        private DataGridView _gridReport;
        private ComboBox _cmbReport;
        private ComboBox _cmbDocType;
        private TextBox _txtItemSearch;
        private ComboBox _cmbItemStatus;
        private ComboBox _cmbItemCategory;
        private string _startDocType;
        private TabControl _tabs;

        public const int TabItems = 0;
        public const int TabDocuments = 1;
        public const int TabWarehouses = 2;
        public const int TabReports = 3;
        public const string MasterCategories = "categories";
        public const string MasterUoms = "uoms";
        public const string MasterSpecs = "specs";
        public const string MasterMinMax = "minmax";
        public const string ReportStockOnHand = "گزارش موجودی";
        public const string ReportValuation = "گزارش ارزش انبار";
        public const string ReportKardex = "گزارش ورود و خروج";
        public const string ReportReorder = "گزارش کالاهای کم‌موجود";
        public const string ReportSlow = "کالای کم‌گردش";
        public const string ReportFast = "کالای پرگردش";
        public const string ReportIntegrity = "صحت موجودی";
        public const string ReportVsGl = "سایر گزارشات انبار";

        public FrmInventory()
            : this("موجودی کالا", TabItems, null)
        {
        }

        public FrmInventory(string title, int startTab, string reportKind)
            : this(title, startTab, reportKind, null)
        {
        }

        public FrmInventory(string title, int startTab, string reportKind, string masterFocus)
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _posting = new InventoryPostingService();
            _query = new InventoryQueryService();
            _items = new InventoryItemService();
            _masterFocus = masterFocus;
            ErpAccess.RequirePermission(this, "Inventory.View");
            string heading = string.IsNullOrWhiteSpace(title) ? "انبارداری" : title;
            Text = heading;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            AutoScaleMode = AutoScaleMode.Dpi;
            UiTheme.MakeMainWindow(this, 1180, 700);

            if (startTab == TabDocuments && IsDocType(reportKind))
                _startDocType = reportKind;

            _tabs = new TabControl { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            _tabs.TabPages.Add(BuildItemsTab());
            _tabs.TabPages.Add(BuildDocsTab());
            _tabs.TabPages.Add(BuildWarehouseTab());
            _tabs.TabPages.Add(BuildReportTab());
            if (startTab >= 0 && startTab < _tabs.TabPages.Count)
                _tabs.SelectedIndex = startTab;
            SelectReport(reportKind);
            Controls.Add(_tabs);
            Controls.Add(ErpFormChrome.Header(heading));
            ReloadItems();
            ReloadDocs();
            ReloadWarehouses();
            ReloadReport();
            Shown += delegate
            {
                if (_masterFocus == MasterCategories) ShowCategoryManager();
                else if (_masterFocus == MasterUoms) ShowUomManager();
            };
        }

        private static bool IsDocType(string value)
        {
            return value == InventoryCodes.TypeReceipt || value == InventoryCodes.TypeIssue
                || value == InventoryCodes.TypeTransfer || value == InventoryCodes.TypeAdjustment
                || value == InventoryCodes.TypeCount || value == InventoryCodes.TypeOpening;
        }

        private bool Can(string permission)
        {
            return _identity != null && _identity.HasPermission(permission);
        }

        private void SelectReport(string reportKind)
        {
            if (_cmbReport == null || string.IsNullOrWhiteSpace(reportKind) || IsDocType(reportKind)) return;
            int idx = _cmbReport.Items.IndexOf(reportKind);
            if (idx >= 0) _cmbReport.SelectedIndex = idx;
        }

        private TabPage BuildItemsTab()
        {
            TabPage p = new TabPage("کالاها");
            _gridItems = Grid();
            FlowLayoutPanel flow = ErpFormChrome.Toolbar();
            flow.WrapContents = true;
            flow.Height = 88;
            _txtItemSearch = new TextBox { Width = 160 };
            _txtItemSearch.GotFocus += delegate
            {
                if (_txtItemSearch.ForeColor == UiTheme.TextMuted) { _txtItemSearch.Text = ""; _txtItemSearch.ForeColor = UiTheme.TextDark; }
            };
            _txtItemSearch.ForeColor = UiTheme.TextMuted;
            _txtItemSearch.Text = "جستجوی کد / نام / بارکد";
            _txtItemSearch.TextChanged += delegate { ReloadItems(); };
            _cmbItemStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
            _cmbItemStatus.Items.Add("همه");
            _cmbItemStatus.Items.Add("فعال");
            _cmbItemStatus.Items.Add("غیرفعال");
            _cmbItemStatus.SelectedIndex = 0;
            _cmbItemStatus.SelectedIndexChanged += delegate { ReloadItems(); };
            _cmbItemCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            FillCategoryFilter();
            _cmbItemCategory.SelectedIndexChanged += delegate { ReloadItems(); };
            flow.Controls.Add(_txtItemSearch);
            flow.Controls.Add(_cmbItemStatus);
            flow.Controls.Add(_cmbItemCategory);
            if (Can(InventoryPermissions.ManageItem) || Can(InventoryPermissions.Create))
                flow.Controls.Add(Btn("کالای جدید", NewItem));
            if (Can(InventoryPermissions.ManageItem))
            {
                flow.Controls.Add(Btn("ویرایش", EditItem));
                flow.Controls.Add(Btn("غیرفعال", delegate { SetItemActive(false); }));
                flow.Controls.Add(Btn("فعال", delegate { SetItemActive(true); }));
                flow.Controls.Add(Btn("حداقل / حداکثر", EditMinQty));
                flow.Controls.Add(Btn("حذف", DeleteItem));
                flow.Controls.Add(Btn("دسته‌بندی", ShowCategoryManager));
                flow.Controls.Add(Btn("واحد اندازه", ShowUomManager));
            }
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { ReloadItems(); }));
            p.Controls.Add(ErpFormChrome.WrapGrid(_gridItems, ProductBranding.EmptyList));
            p.Controls.Add(flow);
            _gridItems.CellDoubleClick += delegate { if (Can(InventoryPermissions.ManageItem)) EditItem(); };
            return p;
        }

        private TabPage BuildDocsTab()
        {
            TabPage p = new TabPage("ورود و خروج");
            _gridDocs = Grid();
            FlowLayoutPanel flow = ErpFormChrome.Toolbar();
            flow.WrapContents = true;
            flow.Height = 84;
            _cmbDocType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            _cmbDocType.Items.Add("همه");
            _cmbDocType.Items.Add(LabelOf(InventoryCodes.TypeReceipt));
            _cmbDocType.Items.Add(LabelOf(InventoryCodes.TypeIssue));
            _cmbDocType.Items.Add(LabelOf(InventoryCodes.TypeTransfer));
            _cmbDocType.Items.Add(LabelOf(InventoryCodes.TypeAdjustment));
            _cmbDocType.Items.Add(LabelOf(InventoryCodes.TypeCount));
            _cmbDocType.Items.Add(LabelOf(InventoryCodes.TypeOpening));
            _cmbDocType.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(_startDocType))
            {
                int i = _cmbDocType.Items.IndexOf(LabelOf(_startDocType));
                if (i >= 0) _cmbDocType.SelectedIndex = i;
            }
            _cmbDocType.SelectedIndexChanged += delegate { ReloadDocs(); };
            flow.Controls.Add(_cmbDocType);
            if (Can(InventoryPermissions.Create))
            {
                flow.Controls.Add(Btn("ورود کالا", delegate { NewDoc(InventoryCodes.TypeReceipt); }));
                flow.Controls.Add(Btn("خروج کالا", delegate { NewDoc(InventoryCodes.TypeIssue); }));
                flow.Controls.Add(Btn("انتقال کالا", TransferDoc));
                flow.Controls.Add(Btn("موجودی اول دوره", delegate { NewDoc(InventoryCodes.TypeOpening); }));
            }
            if (Can(InventoryPermissions.Adjust))
            {
                flow.Controls.Add(Btn("اصلاح موجودی", delegate { NewDoc(InventoryCodes.TypeAdjustment); }));
                flow.Controls.Add(Btn("شمارش موجودی", CountDoc));
            }
            if (Can(InventoryPermissions.Post) || Can(InventoryPermissions.Adjust))
                flow.Controls.Add(Btn("ثبت قطعی", PostSelected));
            if (Can(InventoryPermissions.Approve))
                flow.Controls.Add(Btn("ارسال تأیید", SubmitSelected));
            if (Can(InventoryPermissions.Create) || Can(InventoryPermissions.Adjust))
                flow.Controls.Add(Btn("ویرایش پیش‌نویس", EditDraft));
            if (Can(InventoryPermissions.Reverse))
                flow.Controls.Add(Btn("برگشت", ReverseSelected));
            flow.Controls.Add(AccountingChrome.AttachQuickSearch(null, _gridDocs, "جستجوی سند..."));
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { ReloadDocs(); }));
            p.Controls.Add(ErpFormChrome.WrapGrid(_gridDocs, ProductBranding.EmptyList));
            p.Controls.Add(flow);
            return p;
        }

        private TabPage BuildWarehouseTab()
        {
            TabPage p = new TabPage("انبارها");
            _gridWh = Grid();
            FlowLayoutPanel flow = ErpFormChrome.Toolbar();
            if (Can(InventoryPermissions.ManageWarehouse))
            {
                flow.Controls.Add(Btn("انبار جدید", NewWarehouse));
                flow.Controls.Add(Btn("غیرفعال", delegate { SetWarehouseActive(false); }));
                flow.Controls.Add(Btn("فعال", delegate { SetWarehouseActive(true); }));
            }
            flow.Controls.Add(AccountingChrome.AttachQuickSearch(null, _gridWh, "جستجوی انبار..."));
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { ReloadWarehouses(); }));
            p.Controls.Add(ErpFormChrome.WrapGrid(_gridWh, ProductBranding.EmptyList));
            p.Controls.Add(flow);
            return p;
        }

        private TabPage BuildReportTab()
        {
            TabPage p = new TabPage("گزارشات انبار");
            _gridReport = Grid();
            FlowLayoutPanel flow = ErpFormChrome.Toolbar();
            flow.WrapContents = true;
            flow.Height = 84;
            _cmbReport = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            _cmbReport.Items.Add(ReportStockOnHand);
            _cmbReport.Items.Add(ReportValuation);
            _cmbReport.Items.Add(ReportKardex);
            _cmbReport.Items.Add(ReportReorder);
            _cmbReport.Items.Add(ReportSlow);
            _cmbReport.Items.Add(ReportFast);
            _cmbReport.Items.Add(ReportIntegrity);
            _cmbReport.Items.Add(ReportVsGl);
            _cmbReport.SelectedIndex = 0;
            _cmbReport.SelectedIndexChanged += delegate { ReloadReport(); };
            flow.Controls.Add(_cmbReport);
            flow.Controls.Add(Btn("اکسل", ExportExcel));
            flow.Controls.Add(Btn("چاپ / PDF", PrintReport));
            flow.Controls.Add(AccountingChrome.AttachQuickSearch(null, _gridReport, "جستجوی گزارش..."));
            flow.Controls.Add(ErpFormChrome.RefreshButton(delegate { ReloadReport(); }));
            p.Controls.Add(ErpFormChrome.WrapGrid(_gridReport, ProductBranding.EmptyList));
            p.Controls.Add(flow);
            return p;
        }

        private void NewItem()
        {
            using (Form dlg = ItemDialog(null))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
            }
            ReloadItems();
        }

        private void EditItem()
        {
            long id = SelectedId(_gridItems);
            if (id <= 0) return;
            InvItem live = _query.GetItem(id, _identity);
            if (live == null) return;
            using (Form dlg = ItemDialog(live))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
            }
            ReloadItems();
        }

        private Form ItemDialog(InvItem live)
        {
            Form dlg = AccountingChrome.PrepareDialog(new Form());
            dlg.Text = live == null ? "کالای جدید" : "ویرایش کالا";
            dlg.Width = 440;
            dlg.Height = 560;
            TextBox code = Field(dlg, "کد", 20, 16);
            TextBox name = Field(dlg, "نام", 20, 64);
            TextBox barcode = Field(dlg, "بارکد", 20, 112);
            ComboBox cmbCat = Combo(dlg, "دسته‌بندی", 20, 160, 340);
            ComboBox cmbUom = Combo(dlg, "واحد اندازه‌گیری", 20, 212, 340);
            FillCategoryCombo(cmbCat, live == null ? 0 : live.CategoryId);
            FillUomCombo(cmbUom, live == null ? 0 : live.BaseUomId);
            TextBox min = Field(dlg, "حداقل موجودی", 20, 264);
            TextBox max = Field(dlg, "حداکثر موجودی (۰ = نامحدود)", 20, 312);
            min.Text = "0";
            max.Text = "0";
            if (live != null)
            {
                code.Text = live.Code;
                name.Text = live.Name;
                barcode.Text = live.Barcode ?? "";
                min.Text = live.MinQtyBase.ToString();
                max.Text = live.MaxQtyBase.ToString();
            }
            Button ok = UiTheme.CreateButton("ثبت", "", UiTheme.PrimaryLight);
            ok.Left = 20;
            ok.Top = 380;
            ok.Click += delegate
            {
                long minQty;
                long maxQty;
                if (!long.TryParse(min.Text.Trim(), out minQty) || !long.TryParse(max.Text.Trim(), out maxQty))
                {
                    UiTheme.ShowWarning(dlg, "حداقل و حداکثر را به‌صورت عدد وارد کنید.");
                    return;
                }
                PairItem cat = cmbCat.SelectedItem as PairItem;
                PairItem uom = cmbUom.SelectedItem as PairItem;
                InventoryResult r;
                if (live == null)
                {
                    r = _posting.CreateItem(new InvItem
                    {
                        Code = code.Text.Trim(),
                        Name = name.Text.Trim(),
                        Barcode = barcode.Text.Trim(),
                        MinQtyBase = minQty,
                        MaxQtyBase = maxQty,
                        CategoryId = cat == null ? 0 : cat.Id,
                        BaseUomId = uom == null ? 0 : uom.Id,
                        CompanyId = Company()
                    }, _identity);
                }
                else
                {
                    live.Code = code.Text.Trim();
                    live.Name = name.Text.Trim();
                    live.Barcode = barcode.Text.Trim();
                    live.MinQtyBase = minQty;
                    live.MaxQtyBase = maxQty;
                    live.CategoryId = cat == null ? live.CategoryId : cat.Id;
                    live.BaseUomId = uom == null ? live.BaseUomId : uom.Id;
                    r = _posting.UpdateItem(live, _identity);
                }
                ShowInv(r);
                if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
            };
            dlg.Controls.Add(ok);
            return dlg;
        }

        private void EditMinQty()
        {
            EditItem();
        }

        private void SetItemActive(bool active)
        {
            long id = SelectedId(_gridItems);
            if (id <= 0) return;
            ShowInv(_posting.SetItemActive(id, active, _identity));
            ReloadItems();
        }

        private void DeleteItem()
        {
            long id = SelectedId(_gridItems);
            if (id <= 0) return;
            if (!UiTheme.ShowConfirm(this, "کالای بدون موجودی حذف نرم می‌شود. ادامه؟", "حذف کالا")) return;
            ShowInv(_posting.DeleteItem(id, _identity));
            ReloadItems();
        }

        private void NewWarehouse()
        {
            using (Form dlg = AccountingChrome.PrepareDialog(new Form()))
            {
                dlg.Text = "انبار جدید";
                dlg.Width = 380;
                dlg.Height = 240;
                TextBox code = Field(dlg, "کد", 20, 20);
                TextBox name = Field(dlg, "نام", 20, 70);
                Button ok = UiTheme.CreateButton("ثبت", "", UiTheme.PrimaryLight);
                ok.Left = 20;
                ok.Top = 130;
                ok.Click += delegate
                {
                    InventoryResult r = _posting.CreateWarehouse(new InvWarehouse
                    {
                        Code = code.Text.Trim(),
                        Name = name.Text.Trim(),
                        CompanyId = Company(),
                        CenterId = _identity.CenterId > 0 ? _identity.CenterId : 1
                    }, _identity);
                    ShowInv(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            ReloadWarehouses();
        }

        private void SetWarehouseActive(bool active)
        {
            long id = SelectedId(_gridWh);
            if (id <= 0) return;
            ShowInv(_posting.SetWarehouseActive(id, active, _identity));
            ReloadWarehouses();
        }

        private void NewDoc(string type)
        {
            IList<InvItem> items = ActiveItems();
            if (items.Count == 0)
            {
                UiTheme.ShowWarning(this, "ابتدا یک کالای فعال تعریف کنید.");
                return;
            }
            IList<InvWarehouse> warehouses = _query.ListWarehouses(_identity);
            using (Form dlg = AccountingChrome.PrepareDialog(new Form()))
            {
                dlg.Text = LabelOf(type);
                dlg.Width = 420;
                dlg.Height = 380;
                ComboBox cmbItem = Combo(dlg, "کالا", 20, 18, 340);
                for (int i = 0; i < items.Count; i++)
                    cmbItem.Items.Add(new PairItem(items[i].ItemId, items[i].Code + " " + items[i].Name));
                cmbItem.SelectedIndex = 0;
                ComboBox cmbWh = Combo(dlg, "انبار", 20, 70, 340);
                FillWarehouses(cmbWh, warehouses, 0);
                TextBox qty = Field(dlg, type == InventoryCodes.TypeAdjustment ? "مقدار (+ افزایش / − کاهش)" : "مقدار", 20, 130);
                qty.Text = "1";
                TextBox cost = Field(dlg, "بهای واحد", 20, 180);
                cost.Text = (type == InventoryCodes.TypeIssue || type == InventoryCodes.TypeAdjustment) ? "0" : "100";
                Button ok = UiTheme.CreateButton("ثبت", "", UiTheme.PrimaryLight);
                ok.Left = 20;
                ok.Top = 250;
                ok.Click += delegate
                {
                    PairItem it = cmbItem.SelectedItem as PairItem;
                    PairItem wh = cmbWh.SelectedItem as PairItem;
                    long q;
                    long c;
                    if (it == null || wh == null || !long.TryParse(qty.Text.Trim(), out q) || !long.TryParse(cost.Text.Trim(), out c))
                    {
                        UiTheme.ShowWarning(dlg, "مقدار و بها را به‌صورت عدد معتبر وارد کنید.");
                        return;
                    }
                    if (type == InventoryCodes.TypeAdjustment && q == 0)
                    {
                        UiTheme.ShowWarning(dlg, "مقدار اصلاح نمی‌تواند صفر باشد.");
                        return;
                    }
                    Submit(dlg, BuildCmd(type, it.Id, wh.Id, _query.DefaultLocationId(wh.Id), q, c, 0, 0));
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            AfterDocs();
        }

        private void TransferDoc()
        {
            IList<InvItem> items = ActiveItems();
            IList<InvWarehouse> warehouses = _query.ListWarehouses(_identity);
            if (items.Count == 0 || warehouses.Count == 0)
            {
                UiTheme.ShowWarning(this, "برای انتقال، حداقل یک کالا و یک انبار لازم است.");
                return;
            }
            using (Form dlg = AccountingChrome.PrepareDialog(new Form()))
            {
                dlg.Text = "انتقال کالا";
                dlg.Width = 420;
                dlg.Height = 480;
                ComboBox cmbItem = Combo(dlg, "کالا", 20, 18, 340);
                for (int i = 0; i < items.Count; i++)
                    cmbItem.Items.Add(new PairItem(items[i].ItemId, items[i].Code + " " + items[i].Name));
                cmbItem.SelectedIndex = 0;
                ComboBox cmbFrom = Combo(dlg, "انبار مبدأ", 20, 70, 340);
                ComboBox cmbFromLoc = Combo(dlg, "محل مبدأ", 20, 122, 340);
                ComboBox cmbTo = Combo(dlg, "انبار مقصد", 20, 174, 340);
                ComboBox cmbToLoc = Combo(dlg, "محل مقصد", 20, 226, 340);
                FillWarehouses(cmbFrom, warehouses, 0);
                FillWarehouses(cmbTo, warehouses, warehouses.Count > 1 ? 1 : 0);
                Action fillLoc = delegate
                {
                    PairItem fromWh = cmbFrom.SelectedItem as PairItem;
                    PairItem toWh = cmbTo.SelectedItem as PairItem;
                    FillLocations(cmbFromLoc, fromWh == null ? 0 : fromWh.Id, 0);
                    FillLocations(cmbToLoc, toWh == null ? 0 : toWh.Id, cmbToLoc.Items.Count > 1 ? 1 : 0);
                };
                cmbFrom.SelectedIndexChanged += delegate { fillLoc(); };
                cmbTo.SelectedIndexChanged += delegate { fillLoc(); };
                fillLoc();
                TextBox qty = Field(dlg, "مقدار", 20, 280);
                qty.Text = "1";
                Button ok = UiTheme.CreateButton("ثبت انتقال", "", UiTheme.PrimaryLight);
                ok.Left = 20;
                ok.Top = 350;
                ok.Click += delegate
                {
                    PairItem it = cmbItem.SelectedItem as PairItem;
                    PairItem from = cmbFrom.SelectedItem as PairItem;
                    PairItem to = cmbTo.SelectedItem as PairItem;
                    PairItem fromLoc = cmbFromLoc.SelectedItem as PairItem;
                    PairItem toLoc = cmbToLoc.SelectedItem as PairItem;
                    long q;
                    if (it == null || from == null || to == null || fromLoc == null || toLoc == null
                        || !long.TryParse(qty.Text.Trim(), out q) || q <= 0)
                    {
                        UiTheme.ShowWarning(dlg, "مقدار و محل مبدأ/مقصد را کامل کنید.");
                        return;
                    }
                    if (from.Id == to.Id && fromLoc.Id == toLoc.Id)
                    {
                        UiTheme.ShowWarning(dlg, "مبدأ و مقصد نمی‌توانند یکی باشند.");
                        return;
                    }
                    Submit(dlg, BuildCmd(InventoryCodes.TypeTransfer, it.Id, from.Id, fromLoc.Id, q, 0, to.Id, toLoc.Id));
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            AfterDocs();
        }

        private void CountDoc()
        {
            IList<InvItem> items = ActiveItems();
            IList<InvWarehouse> warehouses = _query.ListWarehouses(_identity);
            if (items.Count == 0)
            {
                UiTheme.ShowWarning(this, "ابتدا یک کالا تعریف کنید.");
                return;
            }
            using (Form dlg = AccountingChrome.PrepareDialog(new Form()))
            {
                dlg.Text = "شمارش موجودی";
                dlg.Width = 420;
                dlg.Height = 360;
                ComboBox cmbItem = Combo(dlg, "کالا", 20, 18, 340);
                for (int i = 0; i < items.Count; i++)
                    cmbItem.Items.Add(new PairItem(items[i].ItemId, items[i].Code + " " + items[i].Name));
                cmbItem.SelectedIndex = 0;
                ComboBox cmbWh = Combo(dlg, "انبار", 20, 70, 340);
                FillWarehouses(cmbWh, warehouses, 0);
                Label onHand = new Label { Left = 20, Top = 130, AutoSize = true, Text = "موجودی سیستم: —" };
                dlg.Controls.Add(onHand);
                Action refresh = delegate
                {
                    PairItem it = cmbItem.SelectedItem as PairItem;
                    PairItem wh = cmbWh.SelectedItem as PairItem;
                    if (it == null || wh == null) return;
                    long loc = _query.DefaultLocationId(wh.Id);
                    onHand.Text = "موجودی سیستم: " + _query.OnHandAt(Company(), it.Id, wh.Id, loc).ToString();
                };
                cmbItem.SelectedIndexChanged += delegate { refresh(); };
                cmbWh.SelectedIndexChanged += delegate { refresh(); };
                refresh();
                TextBox counted = Field(dlg, "مقدار شمارش‌شده", 20, 160);
                counted.Text = "0";
                Button ok = UiTheme.CreateButton("ثبت شمارش", "", UiTheme.PrimaryLight);
                ok.Left = 20;
                ok.Top = 230;
                ok.Click += delegate
                {
                    PairItem it = cmbItem.SelectedItem as PairItem;
                    PairItem wh = cmbWh.SelectedItem as PairItem;
                    long physical;
                    if (it == null || wh == null || !long.TryParse(counted.Text.Trim(), out physical) || physical < 0)
                    {
                        UiTheme.ShowWarning(dlg, "مقدار شمارش را به‌صورت عدد نامنفی وارد کنید.");
                        return;
                    }
                    long loc = _query.DefaultLocationId(wh.Id);
                    long system = _query.OnHandAt(Company(), it.Id, wh.Id, loc);
                    long delta = physical - system;
                    if (delta == 0)
                    {
                        UiTheme.ShowWarning(dlg, "شمارش با موجودی سیستم برابر است. سندی ثبت نشد.");
                        return;
                    }
                    Submit(dlg, BuildCmd(InventoryCodes.TypeCount, it.Id, wh.Id, loc, delta, 0, 0, 0));
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            AfterDocs();
        }

        private InventoryPostCommand BuildCmd(string type, long itemId, long warehouseId, long locId, long qty, long unit, long toWh, long toLoc)
        {
            InvDocumentLine line = new InvDocumentLine();
            line.ItemId = itemId;
            line.LocationId = locId;
            line.QtyBase = qty;
            line.QtyDoc = qty < 0 ? -qty : qty;
            line.UnitCostMinor = unit;
            InventoryPostCommand cmd = new InventoryPostCommand();
            cmd.CompanyId = Company();
            cmd.CenterId = _identity.CenterId > 0 ? _identity.CenterId : 1;
            cmd.DocumentType = type;
            cmd.PostingDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            cmd.WarehouseId = warehouseId;
            cmd.ToWarehouseId = toWh;
            cmd.ToLocationId = toLoc;
            cmd.Description = LabelOf(type);
            cmd.Lines = new List<InvDocumentLine> { line };
            return cmd;
        }

        private void Submit(Form dlg, InventoryPostCommand cmd)
        {
            InventoryResult r;
            bool postNow = Can(InventoryPermissions.Post)
                || ((cmd.DocumentType == InventoryCodes.TypeAdjustment || cmd.DocumentType == InventoryCodes.TypeCount)
                    && Can(InventoryPermissions.Adjust));
            if (postNow) r = _posting.CreateAndPost(cmd, _identity);
            else r = _posting.CreateDraft(cmd, _identity);
            ShowInv(r);
            if (r.Ok)
            {
                if (postNow) AccGlOutboxDrain.AfterAccCommit();
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
            }
        }

        private void PostSelected()
        {
            long id = SelectedId(_gridDocs);
            if (id <= 0) return;
            InventoryResult r = _posting.Post(id, _identity);
            ShowInv(r);
            if (r.Ok) AccGlOutboxDrain.AfterAccCommit();
            AfterDocs();
        }

        private void SubmitSelected()
        {
            long id = SelectedId(_gridDocs);
            if (id <= 0) return;
            ShowInv(_posting.Submit(id, _identity));
            AfterDocs();
        }

        private void EditDraft()
        {
            long id = SelectedId(_gridDocs);
            if (id <= 0) return;
            InvDocument doc = _query.GetDocument(id, _identity);
            if (doc == null || doc.Status != InventoryCodes.StatusDraft)
            {
                UiTheme.ShowWarning(this, "فقط پیش‌نویس قابل ویرایش است.");
                return;
            }
            IList<InvDocumentLine> lines = _query.ListLines(id, _identity);
            if (lines.Count == 0)
            {
                UiTheme.ShowWarning(this, "سطر سند پیدا نشد.");
                return;
            }
            using (Form dlg = AccountingChrome.PrepareDialog(new Form()))
            {
                dlg.Text = "ویرایش پیش‌نویس";
                dlg.Width = 400;
                dlg.Height = 240;
                TextBox qty = Field(dlg, "مقدار", 20, 20);
                qty.Text = lines[0].QtyBase.ToString();
                TextBox cost = Field(dlg, "بهای واحد", 20, 70);
                cost.Text = lines[0].UnitCostMinor.ToString();
                Button ok = UiTheme.CreateButton("ذخیره", "", UiTheme.PrimaryLight);
                ok.Left = 20;
                ok.Top = 130;
                ok.Click += delegate
                {
                    long q;
                    long c;
                    if (!long.TryParse(qty.Text.Trim(), out q) || !long.TryParse(cost.Text.Trim(), out c))
                    {
                        UiTheme.ShowWarning(dlg, "مقدار و بها را به‌صورت عدد معتبر وارد کنید.");
                        return;
                    }
                    InventoryPostCommand cmd = new InventoryPostCommand();
                    cmd.DocumentId = doc.DocumentId;
                    cmd.ExpectedRowVersion = doc.RowVersion;
                    cmd.CompanyId = doc.CompanyId;
                    cmd.CenterId = doc.CenterId;
                    cmd.DocumentType = doc.DocumentType;
                    cmd.PostingDate = doc.PostingDate;
                    cmd.WarehouseId = doc.WarehouseId;
                    cmd.ToWarehouseId = doc.ToWarehouseId.HasValue ? doc.ToWarehouseId.Value : 0;
                    cmd.ToLocationId = doc.ToLocationId.HasValue ? doc.ToLocationId.Value : 0;
                    cmd.Description = doc.Description;
                    InvDocumentLine line = new InvDocumentLine();
                    line.ItemId = lines[0].ItemId;
                    line.LocationId = lines[0].LocationId;
                    line.QtyBase = q;
                    line.QtyDoc = q < 0 ? -q : q;
                    line.UnitCostMinor = c;
                    cmd.Lines = new List<InvDocumentLine> { line };
                    InventoryResult r = _posting.UpdateDraft(cmd, _identity);
                    ShowInv(r);
                    if (r.Ok)
                    {
                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    }
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            AfterDocs();
        }

        private void ReverseSelected()
        {
            long id = SelectedId(_gridDocs);
            if (id <= 0) return;
            InventoryResult r = _posting.Reverse(id, _identity);
            ShowInv(r);
            if (r.Ok) AccGlOutboxDrain.AfterAccCommit();
            AfterDocs();
        }

        private void AfterDocs()
        {
            ReloadDocs();
            ReloadReport();
            ReloadItems();
        }

        private void ReloadItems()
        {
            DataTable t = new DataTable();
            t.Columns.Add("Id", typeof(long));
            t.Columns.Add("کد");
            t.Columns.Add("نام");
            t.Columns.Add("بارکد");
            t.Columns.Add("دسته");
            t.Columns.Add("واحد");
            t.Columns.Add("حداقل", typeof(long));
            t.Columns.Add("حداکثر", typeof(long));
            t.Columns.Add("موجودی", typeof(long));
            t.Columns.Add("وضعیت");
            InvItemFilter filter = new InvItemFilter();
            filter.Query = ItemSearchText();
            filter.CategoryId = SelectedComboId(_cmbItemCategory);
            filter.ActiveMode = _cmbItemStatus == null || _cmbItemStatus.SelectedIndex <= 0
                ? 0
                : (_cmbItemStatus.SelectedIndex == 1 ? 1 : -1);
            IList<InvItem> list = _query.ListItems(_identity, filter);
            for (int i = 0; i < list.Count; i++)
            {
                InvItem it = list[i];
                t.Rows.Add(it.ItemId, it.Code, it.Name, it.Barcode ?? "", it.CategoryName ?? "", it.UomName ?? "",
                    it.MinQtyBase, it.MaxQtyBase, _query.OnHand(Company(), it.ItemId), it.IsActive ? "فعال" : "غیرفعال");
            }
            _gridItems.DataSource = t;
            HideId(_gridItems);
        }

        private void ReloadDocs()
        {
            string type = null;
            if (_cmbDocType != null && _cmbDocType.SelectedIndex > 0)
                type = TypeOf(_cmbDocType.SelectedItem.ToString());
            DataTable t = new DataTable();
            t.Columns.Add("Id", typeof(long));
            t.Columns.Add("شماره");
            t.Columns.Add("نوع");
            t.Columns.Add("وضعیت");
            t.Columns.Add("تاریخ");
            t.Columns.Add("شرح");
            IList<InvDocument> list = _query.ListDocuments(_identity, type, null);
            for (int i = 0; i < list.Count; i++)
            {
                InvDocument d = list[i];
                t.Rows.Add(d.DocumentId, d.DocNo, LabelOf(d.DocumentType), StatusFa(d.Status), d.PostingDate, d.Description ?? "");
            }
            _gridDocs.DataSource = t;
            HideId(_gridDocs);
        }

        private void ReloadWarehouses()
        {
            DataTable t = new DataTable();
            t.Columns.Add("Id", typeof(long));
            t.Columns.Add("کد");
            t.Columns.Add("نام");
            t.Columns.Add("شعبه", typeof(int));
            t.Columns.Add("وضعیت");
            IList<InvWarehouse> list = _query.ListWarehouses(_identity);
            for (int i = 0; i < list.Count; i++)
            {
                InvWarehouse w = list[i];
                t.Rows.Add(w.WarehouseId, w.Code, w.Name, w.CenterId, w.IsActive ? "فعال" : "غیرفعال");
            }
            _gridWh.DataSource = t;
            HideId(_gridWh);
        }

        private void ReloadReport()
        {
            DataTable t = new DataTable();
            string kind = _cmbReport.SelectedItem == null ? ReportStockOnHand : _cmbReport.SelectedItem.ToString();
            if (kind == ReportKardex)
            {
                t.Columns.Add("کالا", typeof(long));
                t.Columns.Add("انبار", typeof(long));
                t.Columns.Add("نوع");
                t.Columns.Add("حرکت");
                t.Columns.Add("مقدار", typeof(long));
                t.Columns.Add("ارزش", typeof(long));
                t.Columns.Add("تاریخ");
                IList<InvItemLedger> rows = _query.Ledger(0, _identity);
                for (int i = 0; i < rows.Count; i++)
                    t.Rows.Add(rows[i].ItemId, rows[i].WarehouseId, LabelOf(rows[i].DocumentType),
                        rows[i].MovementType == InventoryCodes.MoveIn ? "ورود" : "خروج",
                        rows[i].QtyBase, rows[i].ValueMinor, rows[i].PostingDate);
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
            else if (kind == ReportSlow || kind == ReportFast)
            {
                t.Columns.Add("کد");
                t.Columns.Add("نام");
                t.Columns.Add("موجودی", typeof(long));
                t.Columns.Add("خروج", typeof(long));
                t.Columns.Add("ورود", typeof(long));
                IList<InventoryVelocityRow> rows = _query.Velocity(_identity, kind == ReportFast);
                for (int i = 0; i < rows.Count; i++)
                    t.Rows.Add(rows[i].ItemCode, rows[i].ItemName, rows[i].QuantityOnHand, rows[i].IssuedQty, rows[i].ReceiptQty);
            }
            else if (kind == ReportIntegrity)
            {
                t.Columns.Add("کد");
                t.Columns.Add("نام");
                t.Columns.Add("افتتاح", typeof(long));
                t.Columns.Add("ورود", typeof(long));
                t.Columns.Add("خروج", typeof(long));
                t.Columns.Add("اصلاح", typeof(long));
                t.Columns.Add("شمارش", typeof(long));
                t.Columns.Add("انتقال ورود", typeof(long));
                t.Columns.Add("انتقال خروج", typeof(long));
                t.Columns.Add("محاسبه‌شده", typeof(long));
                t.Columns.Add("موجودی", typeof(long));
                t.Columns.Add("دفتر", typeof(long));
                t.Columns.Add("وضعیت");
                InventoryIntegritySummary summary = _query.IntegritySummary(_identity);
                IList<InventoryIntegrityRow> rows = summary.Rows;
                t.Rows.Add("%", summary.IntegrityPercent.ToString("0.00"), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    summary.BrokenItems == 0 ? "تراز" : "مغایرت");
                for (int i = 0; i < rows.Count; i++)
                {
                    InventoryIntegrityRow r = rows[i];
                    t.Rows.Add(r.ItemCode, r.ItemName, r.OpeningQty, r.ReceiptQty, r.IssueQty, r.AdjustmentQty, r.CountQty,
                        r.TransferInQty, r.TransferOutQty,
                        r.ComputedQty, r.BalanceQty, r.LedgerQty, r.Balanced ? "تراز" : "مغایرت");
                }
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

        private void ExportExcel()
        {
            AccountingChrome.ExportGrid(_gridReport);
        }

        private void PrintReport()
        {
            DataTable table = _gridReport.DataSource as DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                UiTheme.ShowWarning(this, "ابتدا گزارش را اجرا کنید.");
                return;
            }
            string title = _cmbReport.SelectedItem != null ? _cmbReport.SelectedItem.ToString() : "گزارش انبار";
            int rowIndex = 0;
            using (PrintDocument doc = new PrintDocument())
            {
                doc.DocumentName = title;
                doc.PrintPage += delegate (object s, PrintPageEventArgs e)
                {
                    float y = e.MarginBounds.Top;
                    using (StringFormat rtl = new StringFormat(StringFormatFlags.DirectionRightToLeft))
                    using (Font f = UiTheme.FontBold(14F))
                    {
                        rtl.Alignment = StringAlignment.Far;
                        e.Graphics.DrawString(title, f, Brushes.Black, e.MarginBounds, rtl);
                    }
                    y += 36;
                    int cols = table.Columns.Count;
                    float colW = e.MarginBounds.Width / Math.Max(1, cols);
                    using (Font hf = UiTheme.FontBold(8F))
                    using (Font bf = UiTheme.Font(8F))
                    {
                        for (int c = 0; c < cols; c++)
                            e.Graphics.DrawString(table.Columns[c].ColumnName, hf, Brushes.Black, e.MarginBounds.Left + c * colW, y);
                        y += 20;
                        while (rowIndex < table.Rows.Count)
                        {
                            if (y > e.MarginBounds.Bottom - 24) { e.HasMorePages = true; return; }
                            for (int c = 0; c < cols; c++)
                                e.Graphics.DrawString(Convert.ToString(table.Rows[rowIndex][c]), bf, Brushes.Black,
                                    e.MarginBounds.Left + c * colW, y);
                            y += 18;
                            rowIndex++;
                        }
                    }
                };
                using (PrintPreviewDialog preview = new PrintPreviewDialog())
                {
                    preview.Document = doc;
                    preview.Width = 900;
                    preview.Height = 700;
                    preview.ShowDialog(this);
                }
            }
        }

        private IList<InvItem> ActiveItems()
        {
            IList<InvItem> all = _query.ListItems(_identity, null, false);
            return all;
        }

        private static void FillWarehouses(ComboBox cmb, IList<InvWarehouse> list, int selected)
        {
            cmb.Items.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].IsActive) continue;
                cmb.Items.Add(new PairItem(list[i].WarehouseId, list[i].Code + " " + list[i].Name));
            }
            if (cmb.Items.Count > 0)
                cmb.SelectedIndex = selected < cmb.Items.Count ? selected : 0;
        }

        private void FillLocations(ComboBox cmb, long warehouseId, int selected)
        {
            cmb.Items.Clear();
            if (warehouseId <= 0) return;
            IList<InvLocation> list = _query.ListLocations(warehouseId, _identity);
            for (int i = 0; i < list.Count; i++)
                cmb.Items.Add(new PairItem(list[i].LocationId, list[i].Code + " " + list[i].Name));
            if (cmb.Items.Count > 0)
                cmb.SelectedIndex = selected < cmb.Items.Count ? selected : 0;
        }

        private string ItemSearchText()
        {
            if (_txtItemSearch == null) return null;
            if (_txtItemSearch.ForeColor == UiTheme.TextMuted) return null;
            return _txtItemSearch.Text == null ? null : _txtItemSearch.Text.Trim();
        }

        private static long SelectedComboId(ComboBox combo)
        {
            if (combo == null) return 0;
            PairItem item = combo.SelectedItem as PairItem;
            return item == null ? 0 : item.Id;
        }

        private void FillCategoryFilter()
        {
            if (_cmbItemCategory == null) return;
            object keep = _cmbItemCategory.SelectedItem;
            _cmbItemCategory.Items.Clear();
            _cmbItemCategory.Items.Add(new PairItem(0, "همه دسته‌ها"));
            IList<InvItemCategory> cats = _query.ListCategories(_identity);
            for (int i = 0; i < cats.Count; i++)
            {
                string indent = new string(' ', Math.Max(0, cats[i].Level - 1) * 2);
                _cmbItemCategory.Items.Add(new PairItem(cats[i].CategoryId, indent + cats[i].Code + " " + cats[i].Name));
            }
            _cmbItemCategory.SelectedIndex = 0;
            if (keep is PairItem)
            {
                PairItem prev = (PairItem)keep;
                for (int i = 0; i < _cmbItemCategory.Items.Count; i++)
                {
                    PairItem p = _cmbItemCategory.Items[i] as PairItem;
                    if (p != null && p.Id == prev.Id) { _cmbItemCategory.SelectedIndex = i; break; }
                }
            }
        }

        private void FillCategoryCombo(ComboBox combo, long selectedId)
        {
            combo.Items.Clear();
            IList<InvItemCategory> cats = _query.ListCategories(_identity);
            int sel = 0;
            for (int i = 0; i < cats.Count; i++)
            {
                string indent = new string(' ', Math.Max(0, cats[i].Level - 1) * 2);
                combo.Items.Add(new PairItem(cats[i].CategoryId, indent + cats[i].Code + " " + cats[i].Name));
                if (cats[i].CategoryId == selectedId) sel = combo.Items.Count - 1;
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = sel;
        }

        private void FillUomCombo(ComboBox combo, long selectedId)
        {
            combo.Items.Clear();
            IList<InvUnitOfMeasure> uoms = _query.ListUoms(_identity);
            int sel = 0;
            for (int i = 0; i < uoms.Count; i++)
            {
                combo.Items.Add(new PairItem(uoms[i].UomId, uoms[i].Code + " " + uoms[i].Name));
                if (uoms[i].UomId == selectedId) sel = combo.Items.Count - 1;
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = sel;
        }

        private void ShowCategoryManager()
        {
            using (Form dlg = AccountingChrome.PrepareDialog(new Form()))
            {
                dlg.Text = "دسته‌بندی کالا";
                dlg.Width = 640;
                dlg.Height = 480;
                DataGridView grid = Grid();
                grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                grid.Left = 12;
                grid.Top = 56;
                grid.Width = 600;
                grid.Height = 320;
                FlowLayoutPanel flow = new FlowLayoutPanel
                {
                    Left = 12, Top = 12, Width = 600, Height = 40,
                    RightToLeft = RightToLeft.Yes, FlowDirection = FlowDirection.LeftToRight
                };
                Action reload = delegate
                {
                    DataTable t = new DataTable();
                    t.Columns.Add("Id", typeof(long));
                    t.Columns.Add("کد");
                    t.Columns.Add("نام");
                    t.Columns.Add("والد");
                    t.Columns.Add("سطح", typeof(int));
                    IList<InvItemCategory> list = _query.ListCategories(_identity);
                    for (int i = 0; i < list.Count; i++)
                    {
                        string parent = "";
                        for (int j = 0; j < list.Count; j++)
                            if (list[j].CategoryId == list[i].ParentCategoryId) parent = list[j].Name;
                        t.Rows.Add(list[i].CategoryId, list[i].Code, list[i].Name, parent, list[i].Level);
                    }
                    grid.DataSource = t;
                    HideId(grid);
                };
                flow.Controls.Add(Btn("دسته جدید", delegate { EditCategory(dlg, null); reload(); }));
                flow.Controls.Add(Btn("ویرایش", delegate
                {
                    long id = SelectedId(grid);
                    if (id <= 0) return;
                    IList<InvItemCategory> list = _query.ListCategories(_identity);
                    InvItemCategory hit = null;
                    for (int i = 0; i < list.Count; i++)
                        if (list[i].CategoryId == id) hit = list[i];
                    EditCategory(dlg, hit);
                    reload();
                }));
                flow.Controls.Add(Btn("حذف", delegate
                {
                    long id = SelectedId(grid);
                    if (id <= 0) return;
                    if (!UiTheme.ShowConfirm(dlg, "دسته بدون کالا و بدون زیرمجموعه حذف می‌شود. ادامه؟", "حذف دسته")) return;
                    ShowInv(_items.DeleteCategory(id, _identity));
                    reload();
                }));
                dlg.Controls.Add(grid);
                dlg.Controls.Add(flow);
                reload();
                dlg.ShowDialog(this);
            }
            FillCategoryFilter();
            ReloadItems();
        }

        private void EditCategory(Form owner, InvItemCategory live)
        {
            using (Form dlg = AccountingChrome.PrepareDialog(new Form()))
            {
                dlg.Text = live == null ? "دسته جدید" : "ویرایش دسته";
                dlg.Width = 400;
                dlg.Height = 320;
                TextBox code = Field(dlg, "کد", 20, 16);
                TextBox name = Field(dlg, "نام", 20, 64);
                ComboBox parent = Combo(dlg, "دسته والد", 20, 112, 340);
                parent.Items.Add(new PairItem(0, "بدون والد"));
                IList<InvItemCategory> cats = _query.ListCategories(_identity);
                int sel = 0;
                for (int i = 0; i < cats.Count; i++)
                {
                    if (live != null && cats[i].CategoryId == live.CategoryId) continue;
                    parent.Items.Add(new PairItem(cats[i].CategoryId, cats[i].Code + " " + cats[i].Name));
                    if (live != null && cats[i].CategoryId == live.ParentCategoryId)
                        sel = parent.Items.Count - 1;
                }
                parent.SelectedIndex = sel;
                if (live != null)
                {
                    code.Text = live.Code;
                    name.Text = live.Name;
                }
                Button ok = UiTheme.CreateButton("ثبت", "", UiTheme.PrimaryLight);
                ok.Left = 20;
                ok.Top = 200;
                ok.Click += delegate
                {
                    PairItem p = parent.SelectedItem as PairItem;
                    InvItemCategory cat = live ?? new InvItemCategory();
                    cat.Code = code.Text.Trim();
                    cat.Name = name.Text.Trim();
                    cat.ParentCategoryId = p == null ? 0 : p.Id;
                    cat.CompanyId = Company();
                    InventoryResult r = _items.SaveCategory(cat, _identity);
                    ShowInv(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(owner);
            }
        }

        private void ShowUomManager()
        {
            using (Form dlg = AccountingChrome.PrepareDialog(new Form()))
            {
                dlg.Text = "واحدهای اندازه‌گیری";
                dlg.Width = 560;
                dlg.Height = 440;
                DataGridView grid = Grid();
                grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                grid.Left = 12;
                grid.Top = 56;
                grid.Width = 520;
                grid.Height = 280;
                FlowLayoutPanel flow = new FlowLayoutPanel
                {
                    Left = 12, Top = 12, Width = 520, Height = 40,
                    RightToLeft = RightToLeft.Yes, FlowDirection = FlowDirection.LeftToRight
                };
                Action reload = delegate
                {
                    DataTable t = new DataTable();
                    t.Columns.Add("Id", typeof(long));
                    t.Columns.Add("کد");
                    t.Columns.Add("نام");
                    t.Columns.Add("اعشار", typeof(int));
                    IList<InvUnitOfMeasure> list = _query.ListUoms(_identity);
                    for (int i = 0; i < list.Count; i++)
                        t.Rows.Add(list[i].UomId, list[i].Code, list[i].Name, list[i].DecimalPlaces);
                    grid.DataSource = t;
                    HideId(grid);
                };
                flow.Controls.Add(Btn("واحد جدید", delegate { EditUom(dlg, null); reload(); }));
                flow.Controls.Add(Btn("ویرایش", delegate
                {
                    long id = SelectedId(grid);
                    if (id <= 0) return;
                    IList<InvUnitOfMeasure> list = _query.ListUoms(_identity);
                    InvUnitOfMeasure hit = null;
                    for (int i = 0; i < list.Count; i++)
                        if (list[i].UomId == id) hit = list[i];
                    EditUom(dlg, hit);
                    reload();
                }));
                flow.Controls.Add(Btn("حذف", delegate
                {
                    long id = SelectedId(grid);
                    if (id <= 0) return;
                    if (!UiTheme.ShowConfirm(dlg, "واحد بدون کالا حذف می‌شود. ادامه؟", "حذف واحد")) return;
                    ShowInv(_items.DeleteUom(id, _identity));
                    reload();
                }));
                dlg.Controls.Add(grid);
                dlg.Controls.Add(flow);
                reload();
                dlg.ShowDialog(this);
            }
        }

        private void EditUom(Form owner, InvUnitOfMeasure live)
        {
            using (Form dlg = AccountingChrome.PrepareDialog(new Form()))
            {
                dlg.Text = live == null ? "واحد جدید" : "ویرایش واحد";
                dlg.Width = 380;
                dlg.Height = 280;
                TextBox code = Field(dlg, "کد", 20, 16);
                TextBox name = Field(dlg, "نام", 20, 64);
                TextBox dp = Field(dlg, "تعداد اعشار", 20, 112);
                dp.Text = "0";
                if (live != null)
                {
                    code.Text = live.Code;
                    name.Text = live.Name;
                    dp.Text = live.DecimalPlaces.ToString();
                }
                Button ok = UiTheme.CreateButton("ثبت", "", UiTheme.PrimaryLight);
                ok.Left = 20;
                ok.Top = 180;
                ok.Click += delegate
                {
                    int places;
                    if (!int.TryParse(dp.Text.Trim(), out places))
                    {
                        UiTheme.ShowWarning(dlg, "تعداد اعشار را به‌صورت عدد وارد کنید.");
                        return;
                    }
                    InvUnitOfMeasure uom = live ?? new InvUnitOfMeasure();
                    uom.Code = code.Text.Trim();
                    uom.Name = name.Text.Trim();
                    uom.DecimalPlaces = places;
                    uom.CompanyId = Company();
                    InventoryResult r = _items.SaveUom(uom, _identity);
                    ShowInv(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(owner);
            }
        }

        private int Company()
        {
            return _identity.CompanyId > 0 ? _identity.CompanyId : LedgerCodes.DefaultCompanyId;
        }

        private void ShowInv(InventoryResult r)
        {
            if (r == null) return;
            if (r.Ok) UiTheme.ShowSuccess(this, ProductBranding.SavedOk);
            else UiTheme.ShowWarning(this, FaError(r));
        }

        private static string FaError(InventoryResult r)
        {
            if (r.ErrorCode == InventoryCodes.NegativeStock) return "موجودی کافی نیست. موجودی منفی مجاز نیست.";
            if (r.ErrorCode == InventoryCodes.DuplicateCode) return "کد کالا تکراری است.";
            if (r.ErrorCode == InventoryCodes.DuplicateName) return "نام کالا تکراری است.";
            if (r.ErrorCode == InventoryCodes.DuplicateBarcode) return "بارکد تکراری است.";
            if (r.ErrorCode == InventoryCodes.DuplicateMovement) return "این سند قبلاً در دفتر موجودی ثبت شده است.";
            if (r.ErrorCode == InventoryCodes.InactiveItem) return "کالا غیرفعال است.";
            if (r.ErrorCode == InventoryCodes.InterBranch) return "انتقال بین شعب در این نسخه مجاز نیست.";
            if (r.ErrorCode == "PERMISSION") return "مجوز این عملیات را ندارید.";
            if (r.ErrorCode == "PERIOD_CLOSED") return "دوره مالی برای این تاریخ باز نیست.";
            return (r.ErrorCode + " " + r.Message).Trim();
        }

        private static string LabelOf(string type)
        {
            if (type == InventoryCodes.TypeReceipt) return "ورود کالا";
            if (type == InventoryCodes.TypeIssue) return "خروج کالا";
            if (type == InventoryCodes.TypeTransfer) return "انتقال کالا";
            if (type == InventoryCodes.TypeAdjustment) return "اصلاح موجودی";
            if (type == InventoryCodes.TypeCount) return "شمارش موجودی";
            if (type == InventoryCodes.TypeOpening) return "موجودی اول دوره";
            if (type == InventoryCodes.TypeRevalue) return "تجدید ارزیابی";
            return type ?? "";
        }

        private static string TypeOf(string label)
        {
            if (label == "ورود کالا") return InventoryCodes.TypeReceipt;
            if (label == "خروج کالا") return InventoryCodes.TypeIssue;
            if (label == "انتقال کالا") return InventoryCodes.TypeTransfer;
            if (label == "اصلاح موجودی") return InventoryCodes.TypeAdjustment;
            if (label == "شمارش موجودی") return InventoryCodes.TypeCount;
            if (label == "موجودی اول دوره") return InventoryCodes.TypeOpening;
            return null;
        }

        private static string StatusFa(string status)
        {
            if (status == InventoryCodes.StatusDraft) return "پیش‌نویس";
            if (status == InventoryCodes.StatusPosted) return "ثبت‌شده";
            if (status == InventoryCodes.StatusReversed) return "برگشت‌خورده";
            if (status == InventoryCodes.StatusSubmitted) return "ارسال‌شده";
            return status ?? "";
        }

        private static long SelectedId(DataGridView grid)
        {
            if (grid == null || grid.CurrentRow == null || grid.CurrentRow.Cells["Id"].Value == null) return 0;
            return Convert.ToInt64(grid.CurrentRow.Cells["Id"].Value);
        }

        private static void HideId(DataGridView grid)
        {
            if (grid.Columns.Contains("Id")) grid.Columns["Id"].Visible = false;
        }

        private static TextBox Field(Form dlg, string label, int left, int top)
        {
            dlg.Controls.Add(new Label { Text = label, Left = left, Top = top, AutoSize = true });
            TextBox t = new TextBox { Left = left, Top = top + 18, Width = 340 };
            dlg.Controls.Add(t);
            return t;
        }

        private static ComboBox Combo(Form dlg, string label, int left, int top, int width)
        {
            dlg.Controls.Add(new Label { Text = label, Left = left, Top = top, AutoSize = true });
            ComboBox c = new ComboBox { Left = left, Top = top + 18, Width = width, DropDownStyle = ComboBoxStyle.DropDownList };
            dlg.Controls.Add(c);
            return c;
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

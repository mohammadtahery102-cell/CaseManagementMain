using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting.Ledger.Adapters
{
    /// <summary>
    /// WinForms adapter for the General Ledger. No SQL and no posting rules.
    /// </summary>
    public sealed class FrmLedger : Form
    {
        private readonly ILedgerIdentity _identity;
        private readonly IGeneralLedger _gl;
        private readonly IChartOfAccountsService _coa;
        private readonly IFiscalCalendarService _calendar;
        private readonly IYearEndService _yearEnd;
        private readonly ICurrencyAccountingService _fx;
        private readonly ILedgerReporting _reports;
        private readonly int _minorUnits;

        private TabControl _tabs;
        private TreeView _tree;
        private DataGridView _gridYears;
        private DataGridView _gridPeriods;
        private DataGridView _gridJournals;
        private DataGridView _gridReport;
        private ComboBox _cmbReport;
        private TextBox _txtFrom;
        private TextBox _txtTo;
        private ComboBox _cmbGlAccount;
        private ComboBox _cmbYear;
        private ComboBox _cmbCostCenter;
        private ComboBox _cmbProject;
        private TextBox _txtCenter;
        private Label _lblReportNote;

        public const int TabCoa = 0;
        public const int TabCalendar = 1;
        public const int TabJournals = 2;
        public const int TabReports = 3;
        public const string ReportGl = "دفتر کل";
        public const string ReportTrial = "تراز آزمایشی";
        public const string ReportBalance = "ترازنامه";
        public const string ReportPnl = "سود و زیان";
        public const string ReportFx = "موقعیت ارزی";
        public const string ReportDaybook = "دفتر روزنامه";
        public const string ReportSubsidiary = "دفتر معین";
        public const string ReportCashFlow = "جریان نقدی";
        public const string ReportDocFlow = "گردش اسناد";
        public const string ReportDetail = "دفتر تفصیلی";

        private ComboBox _cmbJournalStatus;
        private ComboBox _cmbDetailKind;
        private TextBox _txtCoaSearch;
        private ComboBox _cmbCoaType;
        private Label _lblCoaInfo;
        private string _journalStatusFilter;

        public FrmLedger()
            : this("دفتر کل", TabCoa, null)
        {
        }

        public FrmLedger(string title, int startTab, string reportKind)
        {
            _identity = DesktopLedgerIdentity.FromSession();
            _gl = new PostingEngine();
            _coa = new ChartOfAccountsService();
            _calendar = new FiscalCalendarService();
            _yearEnd = new YearEndCloseService();
            _fx = new CurrencyAccountingService();
            _reports = new LedgerReportingService();
            GlCompany company = _coa.GetCompany(_identity.CompanyId > 0 ? _identity.CompanyId : LedgerCodes.DefaultCompanyId);
            _minorUnits = company != null ? company.MinorUnits : 2;

            BuildUi(title);
            ReloadCoa();
            ReloadCalendar();
            ReloadJournals();
            ReloadReportFilters();
            if (!string.IsNullOrWhiteSpace(title) && title.IndexOf("پیش‌نویس", StringComparison.Ordinal) >= 0)
            {
                _journalStatusFilter = LedgerCodes.JournalDraft;
                if (_cmbJournalStatus != null) _cmbJournalStatus.SelectedItem = "پیش‌نویس";
                ReloadJournals();
            }
            if (_tabs != null && startTab >= 0 && startTab < _tabs.TabPages.Count)
                _tabs.SelectedIndex = startTab;
            SelectReport(reportKind);
            ErpAccess.RequirePermission(this, "Ledger.View");
        }

        private void SelectReport(string reportKind)
        {
            if (_cmbReport == null || string.IsNullOrWhiteSpace(reportKind)) return;
            int idx = _cmbReport.Items.IndexOf(reportKind);
            if (idx >= 0)
                _cmbReport.SelectedIndex = idx;
        }

        private void BuildUi(string title)
        {
            string heading = string.IsNullOrWhiteSpace(title) ? "دفتر کل" : title;
            Text = ProductMode.IsErp
                ? heading + "  ·  " + ProductBranding.CommercialName + "  —  " + SecurityContext.CenterDisplay
                : heading + "  —  " + SecurityContext.CenterDisplay;
            AccountingChrome.MakeWorkspace(this, 1280, 760);

            Panel header = AccountingChrome.BuildHeader(heading, AccountingChrome.Breadcrumb("دفتر کل", heading));
            Panel status = AccountingChrome.BuildStatusBar();

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Font = UiTheme.FontBold(10F)
            };
            _tabs.TabPages.Add(BuildCoaTab());
            _tabs.TabPages.Add(BuildCalendarTab());
            _tabs.TabPages.Add(BuildJournalTab());
            _tabs.TabPages.Add(BuildReportTab());

            Controls.Add(_tabs);
            Controls.Add(status);
            Controls.Add(header);
            AccountingChrome.Polish(this);
        }

        private TabPage BuildCoaTab()
        {
            TabPage page = new TabPage("سرفصل حساب‌ها");
            _tree = new TreeView { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, Font = UiTheme.Font(10F) };
            _tree.AfterSelect += delegate { ShowCoaUsage(); };
            _lblCoaInfo = new Label
            {
                Dock = DockStyle.Bottom, Height = 32, TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(8, 0, 8, 0), Font = UiTheme.Font(9F)
            };
            _txtCoaSearch = new TextBox { Width = 180 };
            _txtCoaSearch.TextChanged += delegate { ReloadCoa(); };
            _cmbCoaType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            _cmbCoaType.Items.AddRange(new object[] { "همه انواع", "دارایی", "بدهی", "حقوق مالکانه", "درآمد", "هزینه" });
            _cmbCoaType.SelectedIndex = 0;
            _cmbCoaType.SelectedIndexChanged += delegate { ReloadCoa(); };
            Panel tools = Toolbar(
                Lbl("جستجو"),
                _txtCoaSearch,
                Lbl("نوع"),
                _cmbCoaType,
                Btn("حساب فرزند", AddChildAccount),
                Btn("ویرایش", EditAccount),
                Btn("غیرفعال", DeactivateAccount),
                Btn("فعال", ActivateAccount),
                Btn("حذف بدون گردش", DeleteAccountSafe),
                Btn("وابستگی‌ها", ShowAccountDependencies),
                Btn("مراکز هزینه", delegate { using (FrmDimensionMaster f = FrmDimensionMaster.CostCenters()) f.ShowDialog(this); }),
                Btn("پروژه‌ها", delegate { using (FrmDimensionMaster f = FrmDimensionMaster.Projects()) f.ShowDialog(this); }),
                Btn("تازه‌سازی", delegate { ReloadCoa(); }));
            page.Controls.Add(_tree);
            page.Controls.Add(_lblCoaInfo);
            page.Controls.Add(tools);
            return page;
        }

        private TabPage BuildCalendarTab()
        {
            TabPage page = new TabPage("سال و دوره مالی");
            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            _gridYears = Grid();
            _gridYears.SelectionChanged += delegate { ReloadPeriods(); };
            _gridPeriods = Grid();
            split.Panel1.Controls.Add(_gridYears);
            split.Panel1.Controls.Add(Toolbar(
                Btn("سال جدید (باز)", AddYear),
                Btn("بستن سال", delegate { YearAction("close"); }),
                Btn("قفل سال", delegate { YearAction("lock"); }),
                Btn("بازگشایی سال", delegate { YearAction("reopen"); }),
                Btn("رفع قفل سال", delegate { YearAction("unlock"); }),
                Btn("سال بعد + افتتاحیه", InitNextYear),
                Btn("نرخ ارز", UpsertRate),
                Btn("تسعیر ارز", RevalueFx)));
            split.Panel2.Controls.Add(_gridPeriods);
            split.Panel2.Controls.Add(Toolbar(
                Btn("دوره جدید", AddPeriod),
                Btn("بستن دوره", delegate { PeriodAction(true); }),
                Btn("قفل دوره", delegate { PeriodAction(false); }),
                Btn("رفع قفل دوره", UnlockSelectedPeriod),
                Btn("بازگشایی دوره", ReopenSelectedPeriod)));
            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildJournalTab()
        {
            TabPage page = new TabPage("اسناد حسابداری");
            _gridJournals = Grid();
            _gridJournals.CellDoubleClick += delegate { OpenSelectedJournal(); };
            TextBox journalSearch = AccountingChrome.AttachQuickSearch(null, _gridJournals, "جستجوی سند...");
            page.Controls.Add(_gridJournals);
            _cmbJournalStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            _cmbJournalStatus.Items.AddRange(new object[] { "همه", "پیش‌نویس", "تأییدشده", "ثبت‌شده", "برگشت‌خورده" });
            _cmbJournalStatus.SelectedIndex = 0;
            _cmbJournalStatus.SelectedIndexChanged += delegate { ReloadJournals(); };
            page.Controls.Add(Toolbar(
                Lbl("وضعیت"),
                _cmbJournalStatus,
                Btn("سند جدید", NewJournal),
                Btn("پیش‌نویس/ویرایش", EditDraft),
                Btn("حذف پیش‌نویس", DeleteDraft),
                Btn("تأیید", ApproveJournal),
                Btn("رد تأیید", RejectJournal),
                Btn("ثبت قطعی", PostJournal),
                Btn("برگشت", ReverseJournal),
                Btn("ردیابی سند", OpenSelectedJournal),
                Btn("صندوق → دفتر کل", delegate { using (FrmCashBookGl f = new FrmCashBookGl()) f.ShowDialog(this); ReloadJournals(); }),
                Btn("تازه‌سازی", delegate { ReloadJournals(); }),
                journalSearch));
            return page;
        }

        private TabPage BuildReportTab()
        {
            TabPage page = new TabPage("گزارش‌ها");
            _gridReport = Grid();
            _lblReportNote = new Label { Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(8, 0, 8, 0) };
            Panel filters = new Panel { Dock = DockStyle.Top, Height = 112 };
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft = RightToLeft.Yes,
                WrapContents = true,
                Padding = new Padding(6)
            };
            _cmbReport = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            _cmbReport.Items.AddRange(new object[] {
                "دفتر کل", "دفتر روزنامه", "دفتر معین", "دفتر تفصیلی", "گردش اسناد",
                "تراز آزمایشی", "ترازنامه", "سود و زیان", "جریان نقدی", "موقعیت ارزی" });
            _cmbReport.SelectedIndex = 1;
            _txtFrom = new TextBox { Width = 90, Text = DateTime.UtcNow.Year + "-01-01" };
            _txtTo = new TextBox { Width = 90, Text = DateTime.UtcNow.ToString("yyyy-MM-dd") };
            _cmbGlAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
            _txtCenter = new TextBox { Width = 50, Text = _identity.CenterId > 0 ? _identity.CenterId.ToString() : "" };
            _cmbYear = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            _cmbCostCenter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            _cmbProject = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            _cmbDetailKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            _cmbDetailKind.Items.AddRange(new object[] { "طرف‌حساب", "صندوق/بانک", "مرکز هزینه", "پروژه" });
            _cmbDetailKind.SelectedIndex = 0;
            flow.Controls.Add(Lbl("گزارش"));
            flow.Controls.Add(_cmbReport);
            flow.Controls.Add(Lbl("سال مالی"));
            flow.Controls.Add(_cmbYear);
            flow.Controls.Add(Lbl("از"));
            flow.Controls.Add(_txtFrom);
            flow.Controls.Add(Lbl("تا"));
            flow.Controls.Add(_txtTo);
            flow.Controls.Add(Lbl("شعبه"));
            flow.Controls.Add(_txtCenter);
            flow.Controls.Add(Lbl("مرکز هزینه"));
            flow.Controls.Add(_cmbCostCenter);
            flow.Controls.Add(Lbl("پروژه"));
            flow.Controls.Add(_cmbProject);
            flow.Controls.Add(Lbl("حساب"));
            flow.Controls.Add(_cmbGlAccount);
            flow.Controls.Add(Lbl("تفصیل"));
            flow.Controls.Add(_cmbDetailKind);
            flow.Controls.Add(Btn("اجرا", RunReport));
            flow.Controls.Add(Btn("چاپ", PrintReport));
            flow.Controls.Add(Btn("چاپ مستقیم", PrintReportDirect));
            flow.Controls.Add(Btn("خروجی اکسل", ExportReportExcel));
            flow.Controls.Add(Btn("ردیابی", DrillFromReport));
            flow.Controls.Add(AccountingChrome.AttachQuickSearch(null, _gridReport, "جستجوی گزارش..."));
            filters.Controls.Add(flow);
            page.Controls.Add(_gridReport);
            page.Controls.Add(_lblReportNote);
            page.Controls.Add(filters);
            _gridReport.CellDoubleClick += delegate { DrillFromReport(); };
            _gridReport.CellFormatting += ColorLedgerCells;
            return page;
        }

        private void ReloadCoa()
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            IList<GlAccount> accounts = _coa.List(CompanyId(), false);
            Dictionary<long, TreeNode> map = new Dictionary<long, TreeNode>();
            List<GlAccount> pending = new List<GlAccount>();
            for (int i = 0; i < accounts.Count; i++)
                if (CoaMatchesFilter(accounts[i])) pending.Add(accounts[i]);
            int guard = 0;
            while (pending.Count > 0 && guard++ < 64)
            {
                List<GlAccount> next = new List<GlAccount>();
                for (int i = 0; i < pending.Count; i++)
                {
                    GlAccount a = pending[i];
                    if (a.ParentAccountId.HasValue && !map.ContainsKey(a.ParentAccountId.Value))
                    {
                        next.Add(a);
                        continue;
                    }
                    TreeNode node = CoaNode(a, false);
                    if (!a.ParentAccountId.HasValue) _tree.Nodes.Add(node);
                    else map[a.ParentAccountId.Value].Nodes.Add(node);
                    map[a.AccountId] = node;
                }
                if (next.Count == pending.Count) break;
                pending = next;
            }
            for (int i = 0; i < pending.Count; i++)
            {
                if (map.ContainsKey(pending[i].AccountId)) continue;
                TreeNode orphan = CoaNode(pending[i], true);
                _tree.Nodes.Add(orphan);
                map[pending[i].AccountId] = orphan;
            }
            _tree.ExpandAll();
            _tree.EndUpdate();

            _cmbGlAccount.Items.Clear();
            for (int i = 0; i < accounts.Count; i++)
            {
                if (accounts[i].IsLeaf)
                    _cmbGlAccount.Items.Add(new AccountItem(accounts[i]));
            }
            if (_cmbGlAccount.Items.Count > 0) _cmbGlAccount.SelectedIndex = 0;
            _cmbGlAccount.DropDownStyle = ComboBoxStyle.DropDown;
            _cmbGlAccount.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cmbGlAccount.AutoCompleteSource = AutoCompleteSource.ListItems;
        }

        private TreeNode CoaNode(GlAccount a, bool orphan)
        {
            TreeNode node = new TreeNode(a.AccountCode + "  " + a.AccountName +
                (a.IsActive ? "" : "  [غیرفعال]") +
                (a.AllowPosting ? "" : "  [سرفصل]") +
                (orphan ? "  [بدون والد]" : ""));
            node.Tag = a;
            if (!a.IsActive) node.ForeColor = UiTheme.TextMuted;
            else if (!a.IsLeaf) node.ForeColor = UiTheme.Primary;
            return node;
        }

        private bool CoaMatchesFilter(GlAccount a)
        {
            if (a == null) return false;
            string typeSel = _cmbCoaType != null && _cmbCoaType.SelectedItem != null
                ? _cmbCoaType.SelectedItem.ToString() : "همه انواع";
            if (typeSel == "دارایی" && a.AccountTypeCode != LedgerCodes.TypeAsset) return false;
            if (typeSel == "بدهی" && a.AccountTypeCode != LedgerCodes.TypeLiability) return false;
            if (typeSel == "حقوق مالکانه" && a.AccountTypeCode != LedgerCodes.TypeEquity) return false;
            if (typeSel == "درآمد" && a.AccountTypeCode != LedgerCodes.TypeRevenue) return false;
            if (typeSel == "هزینه" && a.AccountTypeCode != LedgerCodes.TypeExpense) return false;
            string q = _txtCoaSearch != null ? (_txtCoaSearch.Text ?? "").Trim() : "";
            if (q.Length == 0) return true;
            return (a.AccountCode ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                || (a.AccountName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ShowCoaUsage()
        {
            if (_lblCoaInfo == null) return;
            GlAccount a = SelectedAccount();
            if (a == null) { _lblCoaInfo.Text = ""; return; }
            AccountUsageInfo u = _coa.GetUsage(a.AccountId, _identity);
            _lblCoaInfo.Text = a.AccountCode + "  " + a.AccountName
                + "  ·  نوع " + LedgerUiText.TypeName(a.AccountTypeCode)
                + "  ·  زیرمجموعه " + u.ChildCount
                + "  ·  گردش " + u.PostedLineCount
                + (string.IsNullOrEmpty(u.LastPostingDate) ? "" : ("  ·  آخرین سند " + u.LastPostingDate))
                + (u.CanDelete ? "  ·  قابل حذف" : "  ·  حذف مجاز نیست (غیرفعال کنید)");
        }

        private void EditAccount()
        {
            GlAccount a = SelectedAccount();
            if (a == null) return;
            using (Form dlg = SmallDialog("ویرایش حساب", 420, 260))
            {
                TextBox code = Field(dlg, "کد", 16, 40, 240);
                code.Text = a.AccountCode;
                TextBox name = Field(dlg, "نام", 16, 90, 240);
                name.Text = a.AccountName;
                ComboBox type = Combo(dlg, "نوع", 16, 140, 240);
                IList<GlAccountType> types = _coa.ListTypes(CompanyId());
                for (int i = 0; i < types.Count; i++)
                    type.Items.Add(types[i].AccountTypeCode + " — " + LedgerUiText.TypeName(types[i].AccountTypeCode));
                for (int i = 0; i < type.Items.Count; i++)
                    if (type.Items[i].ToString().StartsWith(a.AccountTypeCode))
                        type.SelectedIndex = i;
                Button ok = Btn("ذخیره", delegate
                {
                    string typeCode = type.SelectedItem != null ? type.SelectedItem.ToString().Split('—')[0].Trim() : a.AccountTypeCode;
                    LedgerResult r = _coa.Update(new UpdateAccountCommand
                    {
                        AccountId = a.AccountId,
                        ExpectedRowVersion = a.RowVersion,
                        AccountCode = code.Text,
                        AccountName = name.Text,
                        AccountTypeCode = typeCode,
                        ParentAccountId = a.ParentAccountId,
                        IsContra = a.IsContra
                    }, _identity);
                    ShowResult(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                });
                ok.Left = 16; ok.Top = 190; dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            ReloadCoa();
        }

        private void DeleteAccountSafe()
        {
            GlAccount a = SelectedAccount();
            if (a == null) return;
            if (!UiTheme.ShowConfirm(this, "اگر حساب گردش یا زیرمجموعه داشته باشد حذف نمی‌شود. ادامه؟", "حذف حساب"))
                return;
            ShowResult(_coa.SoftDelete(new SoftDeleteCommand { EntityId = a.AccountId, ExpectedRowVersion = a.RowVersion }, _identity));
            ReloadCoa();
        }

        private void ShowAccountDependencies()
        {
            GlAccount a = SelectedAccount();
            if (a == null) return;
            AccountUsageInfo u = _coa.GetUsage(a.AccountId, _identity);
            string msg = "حساب: " + a.AccountCode + "  " + a.AccountName + Environment.NewLine
                + "نوع: " + LedgerUiText.TypeName(a.AccountTypeCode) + Environment.NewLine
                + "زیرمجموعه: " + u.ChildCount + Environment.NewLine
                + "خطوط دفتر کل: " + u.PostedLineCount + Environment.NewLine
                + "آخرین سند: " + (string.IsNullOrEmpty(u.LastPostingDate) ? "—" : u.LastPostingDate) + Environment.NewLine
                + (u.CanDelete ? "حذف نرم مجاز است." : "حذف مجاز نیست؛ در صورت نیاز غیرفعال کنید.");
            UiTheme.ShowInfo(this, msg);
        }

        private void AddChildAccount()
        {
            GlAccount parent = SelectedAccount();
            using (Form dlg = SmallDialog("حساب جدید", 420, 280))
            {
                TextBox code = Field(dlg, "کد", 16, 40, 240);
                TextBox name = Field(dlg, "نام", 16, 90, 240);
                ComboBox type = Combo(dlg, "نوع", 16, 140, 240);
                IList<GlAccountType> types = _coa.ListTypes(CompanyId());
                for (int i = 0; i < types.Count; i++)
                    type.Items.Add(types[i].AccountTypeCode + " — " + LedgerUiText.TypeName(types[i].AccountTypeCode));
                if (parent != null)
                {
                    for (int i = 0; i < type.Items.Count; i++)
                        if (type.Items[i].ToString().StartsWith(parent.AccountTypeCode))
                            type.SelectedIndex = i;
                }
                if (type.SelectedIndex < 0 && type.Items.Count > 0) type.SelectedIndex = 0;
                CheckBox leaf = new CheckBox { Text = "حساب برگ (قابل ثبت)", Left = 16, Top = 185, Width = 240, Checked = true, Parent = dlg };
                Button ok = Btn("ثبت", delegate
                {
                    string typeCode = type.SelectedItem != null ? type.SelectedItem.ToString().Split('—')[0].Trim() : LedgerCodes.TypeAsset;
                    LedgerResult r = _coa.Create(new CreateAccountCommand
                    {
                        CompanyId = CompanyId(),
                        AccountCode = code.Text,
                        AccountName = name.Text,
                        AccountTypeCode = typeCode,
                        ParentAccountId = parent == null ? (long?)null : parent.AccountId,
                        IsLeaf = leaf.Checked,
                        AllowPosting = leaf.Checked
                    }, _identity);
                    ShowResult(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                });
                ok.Left = 16; ok.Top = 220; dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            ReloadCoa();
        }

        private void DeactivateAccount()
        {
            GlAccount a = SelectedAccount();
            if (a == null) return;
            ShowResult(_coa.Deactivate(new SoftDeleteCommand { EntityId = a.AccountId, ExpectedRowVersion = a.RowVersion }, _identity));
            ReloadCoa();
        }

        private void ActivateAccount()
        {
            GlAccount a = SelectedAccount();
            if (a == null) return;
            ShowResult(_coa.Activate(new SoftDeleteCommand { EntityId = a.AccountId, ExpectedRowVersion = a.RowVersion }, _identity));
            ReloadCoa();
        }

        private void ReloadCalendar()
        {
            IList<GlFiscalYear> years = _calendar.ListYears(CompanyId());
            DataTable table = new System.Data.DataTable();
            table.Columns.Add("Id", typeof(long));
            table.Columns.Add("کد");
            table.Columns.Add("نام");
            table.Columns.Add("از");
            table.Columns.Add("تا");
            table.Columns.Add("وضعیت");
            table.Columns.Add("RV", typeof(long));
            for (int i = 0; i < years.Count; i++)
            {
                GlFiscalYear y = years[i];
                table.Rows.Add(y.FiscalYearId, y.Code, y.Name, y.StartDate, y.EndDate, AccountingChrome.StatusFa(y.Status), y.RowVersion);
            }
            _gridYears.DataSource = table;
            HideCol(_gridYears, "Id"); HideCol(_gridYears, "RV");
            ReloadPeriods();
        }

        private void ReloadPeriods()
        {
            long yearId = SelectedId(_gridYears);
            DataTable table = new System.Data.DataTable();
            table.Columns.Add("Id", typeof(long));
            table.Columns.Add("شماره", typeof(int));
            table.Columns.Add("نام");
            table.Columns.Add("از");
            table.Columns.Add("تا");
            table.Columns.Add("وضعیت");
            table.Columns.Add("RV", typeof(long));
            if (yearId > 0)
            {
                IList<GlFiscalPeriod> periods = _calendar.ListPeriods(yearId);
                for (int i = 0; i < periods.Count; i++)
                {
                    GlFiscalPeriod p = periods[i];
                    table.Rows.Add(p.FiscalPeriodId, p.PeriodNo, p.Name, p.StartDate, p.EndDate, AccountingChrome.StatusFa(p.Status), p.RowVersion);
                }
            }
            _gridPeriods.DataSource = table;
            HideCol(_gridPeriods, "Id"); HideCol(_gridPeriods, "RV");
        }

        private void AddYear()
        {
            using (Form dlg = SmallDialog("سال مالی", 400, 260))
            {
                TextBox code = Field(dlg, "کد", 16, 40, 220);
                TextBox name = Field(dlg, "نام", 16, 90, 220);
                TextBox start = Field(dlg, "شروع (yyyy-MM-dd)", 16, 140, 220);
                TextBox end = Field(dlg, "پایان", 16, 190, 220);
                start.Text = DateTime.UtcNow.Year + "-01-01";
                end.Text = DateTime.UtcNow.Year + "-12-31";
                Button ok = Btn("ثبت", delegate
                {
                    LedgerResult r = _calendar.CreateYear(new CreateFiscalYearCommand
                    {
                        CompanyId = CompanyId(),
                        Code = code.Text,
                        Name = name.Text,
                        StartDate = start.Text,
                        EndDate = end.Text
                    }, _identity);
                    ShowResult(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                });
                ok.Left = 250; ok.Top = 190; dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            ReloadCalendar();
        }

        private void AddPeriod()
        {
            long yearId = SelectedId(_gridYears);
            if (yearId <= 0) return;
            using (Form dlg = SmallDialog("دوره مالی", 400, 260))
            {
                TextBox no = Field(dlg, "شماره دوره", 16, 40, 220);
                TextBox name = Field(dlg, "نام", 16, 90, 220);
                TextBox start = Field(dlg, "شروع", 16, 140, 220);
                TextBox end = Field(dlg, "پایان", 16, 190, 220);
                Button ok = Btn("ثبت", delegate
                {
                    int n;
                    int.TryParse(no.Text, out n);
                    LedgerResult r = _calendar.AddPeriod(new CreateFiscalPeriodCommand
                    {
                        FiscalYearId = yearId,
                        PeriodNo = n,
                        Name = name.Text,
                        StartDate = start.Text,
                        EndDate = end.Text
                    }, _identity);
                    ShowResult(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                });
                ok.Left = 250; ok.Top = 190; dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            ReloadCalendar();
        }

        private void YearAction(string action)
        {
            long id = SelectedId(_gridYears);
            long rv = SelectedRv(_gridYears);
            if (id <= 0) return;
            CalendarStatusCommand cmd = new CalendarStatusCommand { EntityId = id, ExpectedRowVersion = rv };
            LedgerResult r;
            YearEndCloseCommand ye = new YearEndCloseCommand { FiscalYearId = id, ExpectedRowVersion = rv, CenterId = _identity.CenterId };
            if (action == "close") r = _yearEnd.Close(ye, _identity);
            else if (action == "lock") r = _calendar.LockYear(cmd, _identity);
            else if (action == "reopen") r = _yearEnd.Reopen(ye, _identity);
            else r = _calendar.UnlockYear(cmd, _identity);
            ShowResult(r);
            ReloadCalendar();
        }

        private void PeriodAction(bool close)
        {
            long id = SelectedId(_gridPeriods);
            long rv = SelectedRv(_gridPeriods);
            if (id <= 0) return;
            CalendarStatusCommand cmd = new CalendarStatusCommand { EntityId = id, ExpectedRowVersion = rv };
            ShowResult(close ? _calendar.ClosePeriod(cmd, _identity) : _calendar.LockPeriod(cmd, _identity));
            ReloadCalendar();
        }

        private void ReopenSelectedPeriod()
        {
            long id = SelectedId(_gridPeriods);
            long rv = SelectedRv(_gridPeriods);
            if (id <= 0) return;
            ShowResult(_calendar.ReopenPeriod(new CalendarStatusCommand { EntityId = id, ExpectedRowVersion = rv }, _identity));
            ReloadCalendar();
        }

        private void UnlockSelectedPeriod()
        {
            long id = SelectedId(_gridPeriods);
            long rv = SelectedRv(_gridPeriods);
            if (id <= 0) return;
            ShowResult(_calendar.UnlockPeriod(new CalendarStatusCommand { EntityId = id, ExpectedRowVersion = rv }, _identity));
            ReloadCalendar();
        }

        private void InitNextYear()
        {
            long id = SelectedId(_gridYears);
            if (id <= 0) return;
            using (Form dlg = SmallDialog("سال بعد", 400, 200))
            {
                TextBox code = Field(dlg, "کد سال بعد", 16, 40, 220);
                CheckBox open = new CheckBox { Text = "افتتاحیه از سال انتخاب‌شده", Left = 16, Top = 90, Width = 260, Checked = true, Parent = dlg };
                Button ok = Btn("اجرا", delegate
                {
                    LedgerResult r = _yearEnd.InitializeNextYear(new InitializeNextYearCommand
                    {
                        PriorFiscalYearId = id,
                        Code = code.Text,
                        PostOpeningFromPrior = open.Checked,
                        CenterId = _identity.CenterId
                    }, _identity);
                    ShowResult(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                });
                ok.Left = 250; ok.Top = 130; dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            ReloadCalendar();
            ReloadJournals();
        }

        private void UpsertRate()
        {
            using (Form dlg = SmallDialog("نرخ ارز", 400, 240))
            {
                TextBox ccy = Field(dlg, "ارز", 16, 40, 220);
                ccy.Text = "USD";
                TextBox date = Field(dlg, "تاریخ", 16, 90, 220);
                date.Text = DateTime.UtcNow.ToString("yyyy-MM-dd");
                TextBox rate = Field(dlg, "نرخ به ارز عملیاتی (مثلاً 70)", 16, 140, 220);
                Button ok = Btn("ثبت", delegate
                {
                    decimal major;
                    decimal.TryParse(rate.Text, out major);
                    long micros = (long)Math.Round(major * LedgerCodes.RateOne, MidpointRounding.AwayFromZero);
                    ShowResult(_fx.UpsertRate(new UpsertExchangeRateCommand
                    {
                        CompanyId = CompanyId(),
                        CurrencyCode = ccy.Text,
                        RateDate = date.Text,
                        RateToBaseMicros = micros
                    }, _identity));
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                });
                ok.Left = 250; ok.Top = 180; dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
        }

        private void RevalueFx()
        {
            string asOf = _txtTo != null ? _txtTo.Text.Trim() : DateTime.UtcNow.ToString("yyyy-MM-dd");
            ShowResult(_fx.Revalue(new RevalueCommand
            {
                CompanyId = CompanyId(),
                CenterId = _identity.CenterId,
                AsOfDate = asOf
            }, _identity));
            ReloadJournals();
        }

        private void ReloadJournals()
        {
            string status = JournalStatusFilter();
            IList<GlJournal> list = _gl.ListJournals(
                _txtFrom != null ? _txtFrom.Text : "",
                _txtTo != null ? _txtTo.Text : "",
                status,
                _identity);
            DataTable table = new System.Data.DataTable();
            table.Columns.Add("Id", typeof(long));
            table.Columns.Add("شماره");
            table.Columns.Add("تاریخ");
            table.Columns.Add("شرح");
            table.Columns.Add("وضعیت");
            table.Columns.Add("منبع");
            table.Columns.Add("ماژول");
            table.Columns.Add("سند مبدأ");
            table.Columns.Add("RV", typeof(long));
            for (int i = 0; i < list.Count; i++)
            {
                GlJournal j = list[i];
                table.Rows.Add(j.JournalId, j.JournalNumber, j.PostingDate, j.Description,
                    AccountingChrome.StatusFa(j.Status), AccountingChrome.SourceFa(j.JournalSource),
                    j.SourceModule ?? "",
                    j.SourceDocumentId.HasValue ? j.SourceDocumentType + " #" + j.SourceDocumentId.Value : "",
                    j.RowVersion);
            }
            _gridJournals.DataSource = table;
            HideCol(_gridJournals, "Id"); HideCol(_gridJournals, "RV");
        }

        private string JournalStatusFilter()
        {
            if (!string.IsNullOrWhiteSpace(_journalStatusFilter)) return _journalStatusFilter;
            string s = _cmbJournalStatus != null && _cmbJournalStatus.SelectedItem != null
                ? _cmbJournalStatus.SelectedItem.ToString() : "همه";
            if (s == "پیش‌نویس") return LedgerCodes.JournalDraft;
            if (s == "تأییدشده") return LedgerCodes.JournalApproved;
            if (s == "ثبت‌شده") return LedgerCodes.JournalPosted;
            if (s == "برگشت‌خورده") return LedgerCodes.JournalReversed;
            return "";
        }

        private void NewJournal()
        {
            EditJournal(0, 0);
        }

        private void EditDraft()
        {
            long id = SelectedId(_gridJournals);
            if (id <= 0) return;
            LedgerResult loaded = _gl.GetJournal(id, _identity);
            if (!loaded.Ok) { ShowResult(loaded); return; }
            if (loaded.Journal.Status != LedgerCodes.JournalDraft)
            {
                ShowJournalLines(loaded.Journal);
                return;
            }
            EditJournal(loaded.Journal.JournalId, loaded.Journal.RowVersion);
        }

        private void DeleteDraft()
        {
            long id = SelectedId(_gridJournals);
            long rv = SelectedRv(_gridJournals);
            if (id <= 0) return;
            if (!UiTheme.ShowConfirm(this, "پیش‌نویس انتخاب‌شده حذف شود؟", "حذف پیش‌نویس")) return;
            ShowResult(_gl.SoftDeleteDraft(new JournalStatusCommand
            {
                JournalId = id,
                ExpectedRowVersion = rv
            }, _identity));
            ReloadJournals();
        }

        private void OpenSelectedJournal()
        {
            long id = SelectedId(_gridJournals);
            if (id <= 0) return;
            LedgerResult loaded = _gl.GetJournal(id, _identity);
            if (!loaded.Ok) { ShowResult(loaded); return; }
            ShowJournalLines(loaded.Journal);
        }

        private void ShowJournalLines(GlJournal journal)
        {
            using (Form dlg = SmallDialog("گردش سند " + journal.JournalNumber, 720, 420))
            {
                DataGridView g = Grid();
                g.Dock = DockStyle.Fill;
                DataTable t = new DataTable();
                t.Columns.Add("ردیف");
                t.Columns.Add("حساب");
                t.Columns.Add("شرح");
                t.Columns.Add("بدهکار");
                t.Columns.Add("بستانکار");
                t.Columns.Add("مرکز هزینه");
                t.Columns.Add("پروژه");
                t.Columns.Add("طرف‌حساب");
                t.Columns.Add("صندوق");
                long dr = 0, cr = 0;
                if (journal.Lines != null)
                {
                    for (int i = 0; i < journal.Lines.Count; i++)
                    {
                        GlJournalLine line = journal.Lines[i];
                        if (line.DebitMinor == 0 && line.CreditMinor == 0) continue;
                        dr += line.DebitBaseMinor;
                        cr += line.CreditBaseMinor;
                        t.Rows.Add(line.LineNo, line.AccountId, line.Description,
                            LedgerUiText.Money(line.DebitBaseMinor, _minorUnits),
                            LedgerUiText.Money(line.CreditBaseMinor, _minorUnits),
                            line.CostCenterId.HasValue ? line.CostCenterId.Value.ToString() : "",
                            line.ProjectId.HasValue ? line.ProjectId.Value.ToString() : "",
                            line.PartyId.HasValue ? line.PartyId.Value.ToString() : "",
                            line.FundId.HasValue ? line.FundId.Value.ToString() : "");
                    }
                }
                g.DataSource = t;
                g.CellFormatting += ColorLedgerCells;
                dlg.Controls.Add(g);
                string src = "";
                if (!string.IsNullOrWhiteSpace(journal.SourceModule))
                    src = "   ·   مبدأ: " + journal.SourceModule + " / " + journal.SourceDocumentType +
                        (journal.SourceDocumentId.HasValue ? " #" + journal.SourceDocumentId.Value : "");
                if (journal.ReversesJournalId.HasValue)
                    src += "   ·   برگشت سند #" + journal.ReversesJournalId.Value;
                Label info = new Label
                {
                    Dock = DockStyle.Top, Height = 52, TextAlign = ContentAlignment.MiddleRight,
                    Text = "وضعیت: " + AccountingChrome.StatusFa(journal.Status)
                        + "   ·   تاریخ: " + journal.PostingDate
                        + "   ·   منبع: " + AccountingChrome.SourceFa(journal.JournalSource)
                        + src
                        + "   ·   بدهکار " + Money(dr) + " / بستانکار " + Money(cr)
                        + (dr == cr ? "  —  تراز" : "  —  نامتراز")
                };
                dlg.Controls.Add(info);
                dlg.ShowDialog(this);
            }
        }

        private void EditJournal(long journalId, long expected)
        {
            using (Form dlg = SmallDialog(journalId == 0 ? "سند جدید" : "ویرایش پیش‌نویس", 820, 480))
            {
                TextBox date = Field(dlg, "تاریخ ثبت", 16, 16, 140);
                date.Text = DateTime.UtcNow.ToString("yyyy-MM-dd");
                TextBox desc = Field(dlg, "شرح", 180, 16, 400);
                DataGridView lines = Grid();
                lines.AllowUserToAddRows = true;
                lines.ReadOnly = false;
                lines.Top = 70; lines.Left = 16; lines.Width = 770; lines.Height = 320; lines.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                dlg.Controls.Add(lines);
                DataTable lt = new System.Data.DataTable();
                lt.Columns.Add("AccountId", typeof(long));
                lt.Columns.Add("حساب");
                lt.Columns.Add("بدهکار", typeof(long));
                lt.Columns.Add("بستانکار", typeof(long));
                lt.Columns.Add("شرح سطر");
                IList<GlAccount> accounts = _coa.List(CompanyId(), false);
                if (journalId > 0)
                {
                    LedgerResult loaded = _gl.GetJournal(journalId, _identity);
                    if (loaded.Ok && loaded.Journal.Lines != null)
                    {
                        date.Text = loaded.Journal.PostingDate;
                        desc.Text = loaded.Journal.Description ?? "";
                        expected = loaded.Journal.RowVersion;
                        for (int i = 0; i < loaded.Journal.Lines.Count; i++)
                        {
                            GlJournalLine ln = loaded.Journal.Lines[i];
                            if (ln.DebitMinor == 0 && ln.CreditMinor == 0) continue;
                            GlAccount acc = FindAccount(accounts, ln.AccountId);
                            lt.Rows.Add(ln.AccountId, acc == null ? ln.AccountId.ToString() : acc.AccountCode + " " + acc.AccountName,
                                ln.DebitMinor, ln.CreditMinor, ln.Description ?? "");
                        }
                    }
                }
                lines.DataSource = lt;
                HideCol(lines, "AccountId");
                Button pick = Btn("حساب برگ برای سطر جاری", delegate
                {
                    if (lines.CurrentRow == null) return;
                    AccountItem item = PickLeaf(accounts);
                    if (item == null) return;
                    lines.CurrentRow.Cells["AccountId"].Value = item.Account.AccountId;
                    lines.CurrentRow.Cells["حساب"].Value = item.ToString();
                });
                pick.Left = 16; pick.Top = 400; dlg.Controls.Add(pick);
                Button save = Btn("ذخیره پیش‌نویس", delegate
                {
                    SaveDraftJournalCommand cmd = BuildDraft(date.Text, desc.Text, lt, journalId, expected);
                    LedgerResult r = _gl.SaveDraft(cmd, _identity);
                    ShowResult(r);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                });
                save.Left = 220; save.Top = 400; dlg.Controls.Add(save);
                dlg.ShowDialog(this);
            }
            ReloadJournals();
        }

        private void ApproveJournal() { StatusOp(j => _gl.Approve(j, _identity)); }
        private void RejectJournal() { StatusOp(j => _gl.Reject(j, _identity)); }
        private void PostJournal()
        {
            long id = SelectedId(_gridJournals);
            long rv = SelectedRv(_gridJournals);
            if (id <= 0) return;
            ShowResult(_gl.Post(new PostJournalCommand
            {
                JournalId = id,
                ExpectedRowVersion = rv,
                CompanyId = CompanyId(),
                CenterId = _identity.CenterId
            }, _identity));
            ReloadJournals();
        }

        private void ReverseJournal()
        {
            long id = SelectedId(_gridJournals);
            long rv = SelectedRv(_gridJournals);
            if (id <= 0) return;
            if (!UiTheme.ShowConfirm(this, "سند برگشت برای سند انتخاب‌شده صادر شود؟ سند اصلی باطل نمی‌شود؛ یک سند معکوس ثبت می‌گردد.", "برگشت سند"))
                return;
            ShowResult(_gl.Reverse(new ReverseJournalCommand
            {
                JournalId = id,
                ExpectedRowVersion = rv,
                PostingDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
            }, _identity));
            ReloadJournals();
        }

        private void RunReport()
        {
            if (!_identity.HasPermission(LedgerPermissions.View))
            {
                UiTheme.ShowWarning(this, LedgerUiText.Error(LedgerErrorCodes.PermissionDenied, null));
                return;
            }
            LedgerReportQuery q = new LedgerReportQuery
            {
                CompanyId = CompanyId(),
                FromDate = _txtFrom.Text.Trim(),
                ToDate = _txtTo.Text.Trim(),
                AccountId = SelectedReportAccountId(),
                CenterId = ParseLong(_txtCenter.Text),
                FiscalYearId = SelectedFilterId(_cmbYear),
                CostCenterId = SelectedFilterId(_cmbCostCenter),
                ProjectId = SelectedFilterId(_cmbProject),
                DetailKind = SelectedDetailKind()
            };
            string kind = _cmbReport.SelectedItem != null ? _cmbReport.SelectedItem.ToString() : "";
            DataTable table = new System.Data.DataTable();
            _lblReportNote.Text = "";
            if (kind == "دفتر کل")
            {
                table.Columns.Add("JournalId", typeof(long));
                table.Columns.Add("LineKind");
                table.Columns.Add("تاریخ"); table.Columns.Add("شماره"); table.Columns.Add("شرح");
                table.Columns.Add("بدهکار"); table.Columns.Add("بستانکار"); table.Columns.Add("مانده");
                IList<GeneralLedgerLineRow> rows = _reports.GetGeneralLedger(q, _identity);
                for (int i = 0; i < rows.Count; i++)
                {
                    GeneralLedgerLineRow r = rows[i];
                    table.Rows.Add(r.JournalId, r.LineKind ?? "", r.PostingDate, r.JournalNumber, r.Description,
                        LedgerUiText.Money(r.DebitBaseMinor, _minorUnits),
                        LedgerUiText.Money(r.CreditBaseMinor, _minorUnits),
                        LedgerUiText.Money(r.RunningNet, _minorUnits));
                }
                AccountLedgerSummary sum = _reports.GetAccountSummary(q, _identity);
                _lblReportNote.Text = q.AccountId <= 0
                    ? "برای دفتر کل یک حساب برگ انتخاب کنید."
                    : ("افتتاح " + Money(sum.OpeningNet)
                        + "  ·  گردش بدهکار " + Money(sum.PeriodDebit)
                        + " / بستانکار " + Money(sum.PeriodCredit)
                        + "  ·  اختتام " + Money(sum.ClosingNet)
                        + (sum.OpeningPlusMovementEqualsClosing ? "  —  افتتاح + گردش = اختتام" : "  —  هشدار مانده"));
            }
            else if (kind == "دفتر روزنامه" || kind == "دفتر معین" || kind == "دفتر تفصیلی")
            {
                table.Columns.Add("JournalId", typeof(long));
                table.Columns.Add("LineKind");
                table.Columns.Add("تاریخ"); table.Columns.Add("شماره");
                if (kind == "دفتر تفصیلی") table.Columns.Add("تفصیل");
                table.Columns.Add("حساب"); table.Columns.Add("نام حساب");
                table.Columns.Add("شرح"); table.Columns.Add("بدهکار"); table.Columns.Add("بستانکار");
                if (kind != "دفتر روزنامه") table.Columns.Add("مانده");
                IList<DaybookLineRow> rows = kind == "دفتر تفصیلی"
                    ? _reports.GetDetailLedger(q, _identity)
                    : (kind == "دفتر معین" ? _reports.GetSubsidiary(q, _identity) : _reports.GetDaybook(q, _identity));
                long dr = 0, cr = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    DaybookLineRow r = rows[i];
                    if (r.LineKind == LedgerCodes.LineMovement)
                    {
                        dr += r.DebitBaseMinor;
                        cr += r.CreditBaseMinor;
                    }
                    if (kind == "دفتر تفصیلی")
                        table.Rows.Add(r.JournalId, r.LineKind ?? "", r.PostingDate, r.JournalNumber, r.DimensionName,
                            r.AccountCode, r.AccountName, r.Description,
                            LedgerUiText.Money(r.DebitBaseMinor, _minorUnits),
                            LedgerUiText.Money(r.CreditBaseMinor, _minorUnits),
                            LedgerUiText.Money(r.RunningNet, _minorUnits));
                    else if (kind == "دفتر معین")
                        table.Rows.Add(r.JournalId, r.LineKind ?? "", r.PostingDate, r.JournalNumber, r.AccountCode, r.AccountName, r.Description,
                            LedgerUiText.Money(r.DebitBaseMinor, _minorUnits),
                            LedgerUiText.Money(r.CreditBaseMinor, _minorUnits),
                            LedgerUiText.Money(r.RunningNet, _minorUnits));
                    else
                        table.Rows.Add(r.JournalId, r.LineKind ?? "", r.PostingDate, r.JournalNumber, r.AccountCode, r.AccountName, r.Description,
                            LedgerUiText.Money(r.DebitBaseMinor, _minorUnits),
                            LedgerUiText.Money(r.CreditBaseMinor, _minorUnits));
                }
                _lblReportNote.Text = rows.Count.ToString() + " ردیف  ·  بدهکار " + Money(dr) + " / بستانکار " + Money(cr)
                    + (kind == "دفتر روزنامه" ? (dr == cr ? "  —  تراز روزنامه برقرار است." : "  —  هشدار: بدهکار ≠ بستانکار") : "");
            }
            else if (kind == "گردش اسناد")
            {
                table.Columns.Add("JournalId", typeof(long));
                table.Columns.Add("شماره"); table.Columns.Add("تاریخ"); table.Columns.Add("شرح");
                table.Columns.Add("وضعیت"); table.Columns.Add("منبع"); table.Columns.Add("ماژول");
                table.Columns.Add("سند مبدأ"); table.Columns.Add("بدهکار"); table.Columns.Add("بستانکار");
                IList<DocumentFlowRow> docs = _reports.GetDocumentFlow(q, _identity);
                int unbalanced = 0;
                for (int i = 0; i < docs.Count; i++)
                {
                    DocumentFlowRow d = docs[i];
                    if (!d.Balanced) unbalanced++;
                    table.Rows.Add(d.JournalId, d.JournalNumber, d.PostingDate, d.Description,
                        AccountingChrome.StatusFa(d.Status), AccountingChrome.SourceFa(d.JournalSource),
                        d.SourceModule ?? "",
                        d.SourceDocumentId.HasValue ? d.SourceDocumentType + " #" + d.SourceDocumentId.Value : "",
                        Money(d.DebitBaseMinor), Money(d.CreditBaseMinor));
                }
                _lblReportNote.Text = docs.Count.ToString() + " سند"
                    + (unbalanced == 0 ? "  —  همهٔ اسناد تراز هستند." : ("  —  هشدار: " + unbalanced + " سند نامتراز"));
            }
            else if (kind == "جریان نقدی")
            {
                table.Columns.Add("بخش"); table.Columns.Add("کد"); table.Columns.Add("حساب"); table.Columns.Add("مبلغ");
                CashFlowResult cf = _reports.GetCashFlow(q, _identity);
                table.Rows.Add("افتتاح نقد", "", "", Money(cf.OpeningMinor));
                table.Rows.Add("ورود نقد", "", "", Money(cf.InflowMinor));
                table.Rows.Add("خروج نقد", "", "", Money(cf.OutflowMinor));
                table.Rows.Add("اختتام نقد", "", "", Money(cf.ClosingMinor));
                FillStatement(table, "حساب نقدی", cf.Lines);
                _lblReportNote.Text = "مانده نقد: " + Money(cf.ClosingMinor);
            }
            else if (kind == "تراز آزمایشی")
            {
                table.Columns.Add("کد"); table.Columns.Add("حساب"); table.Columns.Add("نوع");
                table.Columns.Add("افتتاح بدهکار"); table.Columns.Add("افتتاح بستانکار");
                table.Columns.Add("گردش بدهکار"); table.Columns.Add("گردش بستانکار");
                table.Columns.Add("اختتام بدهکار"); table.Columns.Add("اختتام بستانکار");
                IList<TrialBalanceRow> rows = _reports.GetTrialBalance(q, _identity);
                long dr = 0, cr = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    TrialBalanceRow r = rows[i];
                    dr += r.PeriodDebit; cr += r.PeriodCredit;
                    table.Rows.Add(r.AccountCode, r.AccountName, LedgerUiText.TypeName(r.AccountTypeCode),
                        Money(r.OpeningDebit), Money(r.OpeningCredit),
                        Money(r.PeriodDebit), Money(r.PeriodCredit),
                        Money(r.ClosingDebit), Money(r.ClosingCredit));
                }
                _lblReportNote.Text = dr == cr ? "تراز برقرار است." : ("هشدار: گردش بدهکار ≠ بستانکار  " + Money(dr) + " / " + Money(cr));
            }
            else if (kind == "ترازنامه")
            {
                table.Columns.Add("بخش"); table.Columns.Add("کد"); table.Columns.Add("حساب"); table.Columns.Add("مبلغ");
                BalanceSheetResult bs = _reports.GetBalanceSheet(q, _identity);
                FillStatement(table, "دارایی", bs.Assets);
                FillStatement(table, "بدهی", bs.Liabilities);
                FillStatement(table, "حقوق مالکانه", bs.Equity);
                _lblReportNote.Text = (bs.EquationHolds ? "دارایی = بدهی + حقوق مالکانه. " : "هشدار: معادله ترازنامه برقرار نیست. ")
                    + "جمع دارایی " + Money(bs.AssetTotal);
            }
            else if (kind == "موقعیت ارزی")
            {
                table.Columns.Add("کد"); table.Columns.Add("حساب"); table.Columns.Add("ارز");
                table.Columns.Add("مانده ارز"); table.Columns.Add("مبنای دفتری"); table.Columns.Add("ارزش‌گذاری"); table.Columns.Add("تسعیر نشده");
                IList<CurrencyPositionRow> fx = _reports.GetCurrencyPositions(q, _identity);
                for (int i = 0; i < fx.Count; i++)
                {
                    CurrencyPositionRow r = fx[i];
                    table.Rows.Add(r.AccountCode, r.AccountName, r.CurrencyCode,
                        LedgerUiText.Money(r.TransactionNetMinor, _minorUnits),
                        Money(r.BookedBaseMinor), Money(r.RevaluedBaseMinor), Money(r.UnrealizedBaseMinor));
                }
                _lblReportNote.Text = "ارز عملیاتی: " + _fx.FunctionalCurrency(CompanyId());
            }
            else
            {
                table.Columns.Add("بخش"); table.Columns.Add("کد"); table.Columns.Add("حساب"); table.Columns.Add("مبلغ");
                ProfitAndLossResult pl = _reports.GetProfitAndLoss(q, _identity);
                FillStatement(table, "درآمد", pl.Revenue);
                FillStatement(table, "هزینه", pl.Expenses);
                _lblReportNote.Text = "سود (زیان) خالص: " + Money(pl.NetIncome);
            }
            _gridReport.DataSource = table;
            HideCol(_gridReport, "JournalId");
            HideCol(_gridReport, "LineKind");
            HideCol(_gridReport, "Id");
            HideCol(_gridReport, "RV");
        }

        private void ExportReportExcel()
        {
            if (_gridReport.DataSource == null) RunReport();
            DataTable table = _gridReport.DataSource as System.Data.DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                UiTheme.ShowWarning(this, "ابتدا گزارش را اجرا کنید.");
                return;
            }
            string title = _cmbReport.SelectedItem != null ? _cmbReport.SelectedItem.ToString() : "گزارش";
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "فایل اکسل|*.xlsx", FileName = title + ".xlsx" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (ClosedXML.Excel.XLWorkbook wb = new ClosedXML.Excel.XLWorkbook())
                    {
                        ClosedXML.Excel.IXLWorksheet ws = wb.Worksheets.Add("گزارش");
                        List<int> vis = VisibleColumnIndexes(table);
                        for (int c = 0; c < vis.Count; c++)
                            ws.Cell(1, c + 1).Value = table.Columns[vis[c]].ColumnName;
                        for (int r = 0; r < table.Rows.Count; r++)
                            for (int c = 0; c < vis.Count; c++)
                                ws.Cell(r + 2, c + 1).Value = Convert.ToString(table.Rows[r][vis[c]]);
                        wb.SaveAs(sfd.FileName);
                    }
                    UiTheme.ShowSuccess(this, "فایل اکسل ذخیره شد:\n" + sfd.FileName);
                }
                catch (Exception ex) { UiTheme.ShowError(this, "خطا در خروجی اکسل: " + ex.Message); }
            }
        }

        private void PrintReport()
        {
            if (_gridReport.DataSource == null) { RunReport(); }
            DataTable table = _gridReport.DataSource as System.Data.DataTable;
            if (table == null) return;
            string title = _cmbReport.SelectedItem != null ? _cmbReport.SelectedItem.ToString() : "گزارش";
            PrintLedgerTable(table, title, true);
        }

        private void PrintReportDirect()
        {
            if (_gridReport.DataSource == null) RunReport();
            DataTable table = _gridReport.DataSource as System.Data.DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                UiTheme.ShowWarning(this, "ابتدا گزارش را اجرا کنید.");
                return;
            }
            string title = _cmbReport.SelectedItem != null ? _cmbReport.SelectedItem.ToString() : "گزارش";
            PrintLedgerTable(table, title, false);
        }

        private void PrintLedgerTable(DataTable table, string title, bool preview)
        {
            List<int> vis = VisibleColumnIndexes(table);
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
                    int cols = vis.Count;
                    float colW = e.MarginBounds.Width / Math.Max(1, cols);
                    using (Font hf = UiTheme.FontBold(9F))
                    using (Font bf = UiTheme.Font(9F))
                    using (StringFormat rtl = new StringFormat(StringFormatFlags.DirectionRightToLeft))
                    {
                        rtl.Trimming = StringTrimming.EllipsisCharacter;
                        for (int c = 0; c < cols; c++)
                            e.Graphics.DrawString(table.Columns[vis[c]].ColumnName, hf, Brushes.Black,
                                e.MarginBounds.Left + c * colW, y);
                        y += 22;
                        while (rowIndex < table.Rows.Count)
                        {
                            if (y > e.MarginBounds.Bottom - 24) { e.HasMorePages = true; return; }
                            for (int c = 0; c < cols; c++)
                                e.Graphics.DrawString(Convert.ToString(table.Rows[rowIndex][vis[c]]), bf, Brushes.Black,
                                    new RectangleF(e.MarginBounds.Left + c * colW, y, colW, 18), rtl);
                            y += 18;
                            rowIndex++;
                        }
                    }
                    e.HasMorePages = false;
                };
                if (preview)
                {
                    using (PrintPreviewDialog previewDlg = new PrintPreviewDialog { Document = doc, Width = 900, Height = 700 })
                        previewDlg.ShowDialog(this);
                }
                else
                {
                    using (PrintDialog dlg = new PrintDialog { Document = doc, UseEXDialog = true })
                    {
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                            doc.Print();
                    }
                }
            }
        }

        private List<int> VisibleColumnIndexes(DataTable table)
        {
            List<int> vis = new List<int>();
            if (table == null) return vis;
            for (int c = 0; c < table.Columns.Count; c++)
            {
                string name = table.Columns[c].ColumnName;
                if (name == "JournalId" || name == "LineKind" || name == "Id" || name == "RV") continue;
                vis.Add(c);
            }
            return vis;
        }

        private void DrillFromReport()
        {
            if (_gridReport == null || _gridReport.CurrentRow == null) return;
            if (!_gridReport.Columns.Contains("JournalId")) return;
            object v = _gridReport.CurrentRow.Cells["JournalId"].Value;
            if (v == null || v == DBNull.Value) return;
            long id = Convert.ToInt64(v);
            if (id <= 0) return;
            LedgerResult loaded = _gl.GetJournal(id, _identity);
            if (!loaded.Ok) { ShowResult(loaded); return; }
            ShowJournalLines(loaded.Journal);
        }

        private void ColorLedgerCells(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            DataGridViewRow row = grid.Rows[e.RowIndex];
            string kind = "";
            if (grid.Columns.Contains("LineKind") && row.Cells["LineKind"].Value != null)
                kind = Convert.ToString(row.Cells["LineKind"].Value);
            if (kind == LedgerCodes.LineOpening || kind == LedgerCodes.LineClosing)
            {
                e.CellStyle.BackColor = kind == LedgerCodes.LineOpening ? UiTheme.HoverTint : UiTheme.WarningLight;
                e.CellStyle.Font = UiTheme.FontBold(9F);
            }
            string col = grid.Columns[e.ColumnIndex].Name;
            if (col == "بدهکار" && !IsZeroMoney(e.Value))
                e.CellStyle.ForeColor = UiTheme.Success;
            if (col == "بستانکار" && !IsZeroMoney(e.Value))
                e.CellStyle.ForeColor = UiTheme.Danger;
        }

        private static bool IsZeroMoney(object value)
        {
            string s = Convert.ToString(value) ?? "";
            s = s.Replace("۰", "0").Replace("٫", "").Replace(",", "").Replace(" ", "");
            return s.Length == 0 || s == "0" || s == "0.00" || s == "۰";
        }

        private string SelectedDetailKind()
        {
            string s = _cmbDetailKind != null && _cmbDetailKind.SelectedItem != null
                ? _cmbDetailKind.SelectedItem.ToString() : "طرف‌حساب";
            if (s == "صندوق/بانک") return LedgerCodes.DetailFund;
            if (s == "مرکز هزینه") return LedgerCodes.DetailCostCenter;
            if (s == "پروژه") return LedgerCodes.DetailProject;
            return LedgerCodes.DetailParty;
        }

        private SaveDraftJournalCommand BuildDraft(string date, string desc, System.Data.DataTable lines, long journalId, long expected)
        {
            List<JournalLineDraft> drafts = new List<JournalLineDraft>();
            int n = 0;
            foreach (System.Data.DataRow row in lines.Rows)
            {
                if (row.RowState == System.Data.DataRowState.Deleted) continue;
                long acc = row["AccountId"] == DBNull.Value ? 0 : Convert.ToInt64(row["AccountId"]);
                long dr = row["بدهکار"] == DBNull.Value ? 0 : Convert.ToInt64(row["بدهکار"]);
                long cr = row["بستانکار"] == DBNull.Value ? 0 : Convert.ToInt64(row["بستانکار"]);
                if (acc == 0 && dr == 0 && cr == 0) continue;
                n++;
                drafts.Add(new JournalLineDraft
                {
                    LineNo = n,
                    AccountId = acc,
                    DebitMinor = dr,
                    CreditMinor = cr,
                    Description = Convert.ToString(row["شرح سطر"])
                });
            }
            int center = _identity.CenterId > 0 ? _identity.CenterId : 1;
            return new SaveDraftJournalCommand
            {
                JournalId = journalId,
                ExpectedRowVersion = expected,
                CompanyId = CompanyId(),
                CenterId = center,
                PostingDate = date,
                Description = desc,
                JournalSource = LedgerCodes.SourceManual,
                Lines = drafts
            };
        }

        private void StatusOp(Func<JournalStatusCommand, LedgerResult> op)
        {
            long id = SelectedId(_gridJournals);
            long rv = SelectedRv(_gridJournals);
            if (id <= 0) return;
            ShowResult(op(new JournalStatusCommand { JournalId = id, ExpectedRowVersion = rv }));
            ReloadJournals();
        }

        private void FillStatement(System.Data.DataTable table, string section, IList<StatementLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
                table.Rows.Add(section, lines[i].AccountCode, lines[i].AccountName, Money(lines[i].AmountMinor));
        }

        private string Money(long minor) { return LedgerUiText.Money(minor, _minorUnits); }

        private void ShowResult(LedgerResult r)
        {
            if (r == null) return;
            if (r.Ok) UiTheme.ShowSuccess(this, "انجام شد.");
            else UiTheme.ShowWarning(this, LedgerUiText.Error(r.ErrorCode, r.Message));
        }

        private GlAccount SelectedAccount()
        {
            if (_tree.SelectedNode == null) return null;
            return _tree.SelectedNode.Tag as GlAccount;
        }

        private long SelectedReportAccountId()
        {
            AccountItem item = _cmbGlAccount.SelectedItem as AccountItem;
            return item == null ? 0 : item.Account.AccountId;
        }

        private int CompanyId()
        {
            return _identity.CompanyId > 0 ? _identity.CompanyId : LedgerCodes.DefaultCompanyId;
        }

        private void ReloadReportFilters()
        {
            if (_cmbYear == null) return;
            _cmbYear.Items.Clear();
            _cmbYear.Items.Add(new FilterItem(0, "(همه سال‌ها)"));
            IList<GlFiscalYear> years = _calendar.ListYears(CompanyId());
            for (int i = 0; i < years.Count; i++)
                _cmbYear.Items.Add(new FilterItem(years[i].FiscalYearId, years[i].Code + " " + years[i].Status));
            _cmbYear.SelectedIndex = 0;

            _cmbCostCenter.Items.Clear();
            _cmbCostCenter.Items.Add(new FilterItem(0, "(همه)"));
            IList<GlCostCenter> ccs = new CostCenterService().List(CompanyId(), false);
            for (int i = 0; i < ccs.Count; i++)
                if (ccs[i].IsLeaf)
                    _cmbCostCenter.Items.Add(new FilterItem(ccs[i].CostCenterId, ccs[i].Code + " " + ccs[i].Name));
            _cmbCostCenter.SelectedIndex = 0;

            _cmbProject.Items.Clear();
            _cmbProject.Items.Add(new FilterItem(0, "(همه)"));
            IList<GlProject> prs = new ProjectService().List(CompanyId(), false);
            for (int i = 0; i < prs.Count; i++)
                if (prs[i].IsLeaf)
                    _cmbProject.Items.Add(new FilterItem(prs[i].ProjectId, prs[i].Code + " " + prs[i].Name));
            _cmbProject.SelectedIndex = 0;
        }

        private static int ParseLong(string text)
        {
            int n;
            int.TryParse((text ?? "").Trim(), out n);
            return n;
        }

        private static long SelectedFilterId(ComboBox cmb)
        {
            FilterItem item = cmb != null ? cmb.SelectedItem as FilterItem : null;
            return item == null ? 0 : item.Id;
        }

        private static GlAccount FindAccount(IList<GlAccount> accounts, long id)
        {
            for (int i = 0; i < accounts.Count; i++)
                if (accounts[i].AccountId == id) return accounts[i];
            return null;
        }

        private AccountItem PickLeaf(IList<GlAccount> accounts)
        {
            using (Form dlg = SmallDialog("انتخاب حساب برگ", 420, 360))
            {
                ListBox box = new ListBox { Left = 16, Top = 16, Width = 370, Height = 260 };
                for (int i = 0; i < accounts.Count; i++)
                    if (accounts[i].IsLeaf && accounts[i].AllowPosting && accounts[i].IsActive)
                        box.Items.Add(new AccountItem(accounts[i]));
                dlg.Controls.Add(box);
                AccountItem chosen = null;
                Button ok = Btn("انتخاب", delegate
                {
                    chosen = box.SelectedItem as AccountItem;
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                });
                ok.Left = 16; ok.Top = 290; dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
                return chosen;
            }
        }

        private static long SelectedId(DataGridView grid)
        {
            if (grid.CurrentRow == null || grid.CurrentRow.Cells["Id"].Value == null) return 0;
            return Convert.ToInt64(grid.CurrentRow.Cells["Id"].Value);
        }

        private static long SelectedRv(DataGridView grid)
        {
            if (grid.CurrentRow == null || grid.CurrentRow.Cells["RV"].Value == null) return 0;
            return Convert.ToInt64(grid.CurrentRow.Cells["RV"].Value);
        }

        private static void HideCol(DataGridView grid, string name)
        {
            if (grid.Columns.Contains(name)) grid.Columns[name].Visible = false;
        }

        private static DataGridView Grid()
        {
            DataGridView g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            UiTheme.StyleGrid(g);
            AccountingChrome.PolishGrid(g);
            return g;
        }

        private Panel Toolbar(params Control[] buttons)
        {
            Panel p = new Panel { Dock = DockStyle.Top, Height = 84, Padding = new Padding(6) };
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft = RightToLeft.Yes,
                WrapContents = true
            };
            for (int i = 0; i < buttons.Length; i++) flow.Controls.Add(buttons[i]);
            p.Controls.Add(flow);
            return p;
        }

        private Button Btn(string text, Action click)
        {
            Button b = UiTheme.CreateButton(text, "", UiTheme.PrimaryLight);
            b.AutoSize = true;
            b.Click += delegate { click(); };
            return b;
        }

        private static Form SmallDialog(string title, int w, int h)
        {
            Form f = new Form
            {
                Text = title,
                Width = w,
                Height = h,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            AccountingChrome.PrepareDialog(f);
            f.Shown += delegate { AccountingChrome.Polish(f); };
            return f;
        }

        private static TextBox Field(Form dlg, string label, int x, int y, int w)
        {
            dlg.Controls.Add(new Label { Text = label, Left = x, Top = y - 16, Width = w });
            TextBox t = new TextBox { Left = x, Top = y, Width = w };
            dlg.Controls.Add(t);
            return t;
        }

        private static ComboBox Combo(Form dlg, string label, int x, int y, int w)
        {
            dlg.Controls.Add(new Label { Text = label, Left = x, Top = y - 16, Width = w });
            ComboBox c = new ComboBox { Left = x, Top = y, Width = w, DropDownStyle = ComboBoxStyle.DropDownList };
            dlg.Controls.Add(c);
            return c;
        }

        private static Label Lbl(string t)
        {
            return new Label { Text = t, AutoSize = true, Padding = new Padding(8, 8, 4, 0) };
        }

        private sealed class FilterItem
        {
            public long Id;
            public string Text;
            public FilterItem(long id, string text) { Id = id; Text = text; }
            public override string ToString() { return Text; }
        }

        private sealed class AccountItem
        {
            public GlAccount Account;
            public AccountItem(GlAccount a) { Account = a; }
            public override string ToString()
            {
                return Account.AccountCode + "  " + Account.AccountName;
            }
        }
    }
}

using CaseManagement.Enterprise;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // Phase 5.5-C — مدیریتِ منابع تأمین مالی و خیّرین.
    //
    // چرا یک فرم با دو تب و نه دو فرمِ جدا: هر دو دفترچهٔ مرجعِ کوچک با دقیقاً
    // یک الگوی کار (فهرست + جستجو + افزودن/ویرایش/فعال‌غیرفعال) هستند، و
    // کاربر معمولاً پشتِ‌سرِ هم سراغشان می‌رود (تخصیصِ مالی نیاز به هر دو دارد).
    //
    // الگو عیناً از Enterprise/FrmRules گرفته شده: فرمِ بدونِ Designer، گریدِ
    // CreateGrid، دکمه‌های MakeButton، و دیالوگِ ویرایشِ EntPrompt.Edit — پس
    // هیچ الگوی رابطِ کاربریِ تازه‌ای وارد پروژه نمی‌شود.
    //
    // ⚠ حذفِ فیزیکی عمداً وجود ندارد. هر دو جدول از TblCaseFunding با FKِ
    // RESTRICT ارجاع می‌شوند؛ «غیرفعال‌سازی» هم فهرست‌های انتخاب را تمیز
    // می‌کند و هم تاریخچهٔ تأمینِ مالیِ پرونده‌ها را سالم نگه می‌دارد.
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class FrmFundingAdmin : Form
    {
        private DataGridView _gridSources, _gridSponsors;
        private TextBox _txtSourceSearch, _txtSponsorSearch;
        private CheckBox _chkSourceInactive, _chkSponsorInactive;

        public FrmFundingAdmin()
        {
            BuildUi();
            LoadSources();
            LoadSponsors();
        }

        private void BuildUi()
        {
            Text = "منابع تأمین مالی و خیّرین";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1100, 680);

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Font = UiTheme.FontBold(UiTheme.SizeSmall)
            };

            tabs.TabPages.Add(BuildSourcesTab());
            tabs.TabPages.Add(BuildSponsorsTab());
            Controls.Add(tabs);
        }

        // ─── تبِ منابع تأمین مالی ───────────────────────────────────────────
        private TabPage BuildSourcesTab()
        {
            _gridSources = CreateGrid();
            _txtSourceSearch = new TextBox { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
            _txtSourceSearch.TextChanged += delegate { LoadSources(); };

            _chkSourceInactive = new CheckBox
            {
                Text = "نمایش غیرفعال‌ها",
                AutoSize = true,
                Margin = new Padding(10, 8, 10, 0)
            };
            _chkSourceInactive.CheckedChanged += delegate { LoadSources(); };

            var buttons = MakeButtonBar(
                MakeButton("منبع جدید", "＋", UiTheme.Primary, AddSource),
                MakeButton("ویرایش", "✎", UiTheme.PrimaryLight, EditSource),
                MakeButton("فعال/غیرفعال", "⏻", UiTheme.Warning, ToggleSource),
                MakeButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight, delegate { LoadSources(); }));

            return BuildTab("منابع تأمین مالی", _gridSources, _txtSourceSearch, _chkSourceInactive, buttons);
        }

        // ─── تبِ خیّرین ──────────────────────────────────────────────────────
        private TabPage BuildSponsorsTab()
        {
            _gridSponsors = CreateGrid();
            _txtSponsorSearch = new TextBox { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
            _txtSponsorSearch.TextChanged += delegate { LoadSponsors(); };

            _chkSponsorInactive = new CheckBox
            {
                Text = "نمایش غیرفعال‌ها",
                AutoSize = true,
                Margin = new Padding(10, 8, 10, 0)
            };
            _chkSponsorInactive.CheckedChanged += delegate { LoadSponsors(); };

            var buttons = MakeButtonBar(
                MakeButton("خیّر جدید", "＋", UiTheme.Primary, AddSponsor),
                MakeButton("ویرایش", "✎", UiTheme.PrimaryLight, EditSponsor),
                MakeButton("فعال/غیرفعال", "⏻", UiTheme.Warning, ToggleSponsor),
                MakeButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight, delegate { LoadSponsors(); }));

            return BuildTab("خیّرین", _gridSponsors, _txtSponsorSearch, _chkSponsorInactive, buttons);
        }

        private static TabPage BuildTab(string title, DataGridView grid, TextBox search,
            CheckBox inactive, Control buttons)
        {
            var main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(grid);

            var searchBar = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(6) };
            var searchLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RightToLeft = RightToLeft.Yes
            };
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchLayout.Controls.Add(new Label
            {
                Text = "جستجو:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            }, 0, 0);
            searchLayout.Controls.Add(search, 1, 0);
            searchLayout.Controls.Add(inactive, 2, 0);
            searchBar.Controls.Add(searchLayout);

            main.Controls.Add(searchBar);
            main.Controls.Add(buttons);

            var page = new TabPage(title) { BackColor = UiTheme.CardBack };
            page.Controls.Add(main);
            return page;
        }

        // ─── بارگذاری ───────────────────────────────────────────────────────
        private void LoadSources()
        {
            try
            {
                _gridSources.DataSource = CaseFundingService.GetFundingSourceTable(
                    _txtSourceSearch.Text, _chkSourceInactive.Checked);
                HideColumns(_gridSources, "FundingSourceID", "IsActive");
                Header(_gridSources, "Code", "کد");
                Header(_gridSources, "Name", "نام منبع");
                Header(_gridSources, "Description", "توضیح");
                Header(_gridSources, "StatusText", "وضعیت");
                Header(_gridSources, "UsageCount", "تعداد پرونده");
            }
            catch (Exception ex) { Msg.Show("خطا در بارگذاری منابع: " + ex.Message); }
        }

        private void LoadSponsors()
        {
            try
            {
                _gridSponsors.DataSource = CaseFundingService.GetSponsorTable(
                    _txtSponsorSearch.Text, _chkSponsorInactive.Checked);
                HideColumns(_gridSponsors, "SponsorID", "IsActive");
                Header(_gridSponsors, "Name", "نام خیّر");
                Header(_gridSponsors, "Phone", "تلفن");
                Header(_gridSponsors, "Email", "ایمیل");
                Header(_gridSponsors, "Address", "آدرس");
                Header(_gridSponsors, "Notes", "یادداشت");
                Header(_gridSponsors, "StatusText", "وضعیت");
                Header(_gridSponsors, "UsageCount", "تعداد پرونده");
            }
            catch (Exception ex) { Msg.Show("خطا در بارگذاری خیّرین: " + ex.Message); }
        }

        // ─── منابع: افزودن/ویرایش/فعال‌سازی ─────────────────────────────────
        private void AddSource()
        {
            if (!RequireAdmin()) return;

            Dictionary<string, string> values = EntPrompt.Edit(this, "منبع تأمین مالی جدید",
                EntField.Text("Code", "کد (لاتین، یکتا)", ""),
                EntField.Text("Name", "نام منبع", ""),
                EntField.Multiline("Desc", "توضیح", ""),
                EntField.Check("Active", "فعال", true));

            if (values == null) return;

            string error;
            CaseFundingService.SaveFundingSource(0, values["Code"], values["Name"],
                values["Desc"], values["Active"] == "1", out error);

            if (!string.IsNullOrEmpty(error)) { Msg.Show(error); return; }
            LoadSources();
        }

        private void EditSource()
        {
            if (!RequireAdmin()) return;

            int id = SelectedId(_gridSources, "FundingSourceID");
            if (id <= 0) { Msg.Show("اول یک منبع را انتخاب کنید."); return; }

            DataRowView row = (DataRowView)_gridSources.CurrentRow.DataBoundItem;

            Dictionary<string, string> values = EntPrompt.Edit(this, "ویرایش منبع تأمین مالی",
                EntField.Text("Name", "نام منبع", Convert.ToString(row["Name"])),
                EntField.Multiline("Desc", "توضیح", Convert.ToString(row["Description"])),
                EntField.Check("Active", "فعال", Convert.ToInt32(row["IsActive"]) != 0));

            if (values == null) return;

            string error;
            CaseFundingService.SaveFundingSource(id, "", values["Name"],
                values["Desc"], values["Active"] == "1", out error);

            if (!string.IsNullOrEmpty(error)) { Msg.Show(error); return; }
            LoadSources();
        }

        private void ToggleSource()
        {
            if (!RequireAdmin()) return;

            int id = SelectedId(_gridSources, "FundingSourceID");
            if (id <= 0) { Msg.Show("اول یک منبع را انتخاب کنید."); return; }

            DataRowView row = (DataRowView)_gridSources.CurrentRow.DataBoundItem;
            bool active = Convert.ToInt32(row["IsActive"]) != 0;

            // هشدارِ استفادهٔ فعال: غیرفعال‌کردنِ منبعی که روی پرونده‌ها نشسته
            // آن تخصیص‌ها را حذف نمی‌کند، ولی از فهرستِ انتخاب حذفش می‌کند.
            if (active)
            {
                int usage = CaseFundingService.GetFundingSourceUsage(id);
                if (usage > 0 && !UiTheme.ShowConfirm(this,
                        "این منبع در " + usage + " پرونده فعال است. تخصیص‌های موجود دست‌نخورده می‌مانند " +
                        "ولی این منبع دیگر برای تخصیص تازه در دسترس نخواهد بود. ادامه؟",
                        "غیرفعال‌سازی منبع"))
                    return;
            }

            CaseFundingService.SetFundingSourceActive(id, !active);
            LoadSources();
        }

        // ─── خیّرین: افزودن/ویرایش/فعال‌سازی ────────────────────────────────
        private void AddSponsor()
        {
            if (!RequireAdmin()) return;

            Dictionary<string, string> values = EntPrompt.Edit(this, "خیّر جدید",
                EntField.Text("Name", "نام خیّر", ""),
                EntField.Text("Phone", "تلفن", ""),
                EntField.Text("Email", "ایمیل", ""),
                EntField.Multiline("Address", "آدرس", ""),
                EntField.Multiline("Notes", "یادداشت", ""),
                EntField.Check("Active", "فعال", true));

            if (values == null) return;

            if (string.IsNullOrWhiteSpace(values["Name"]))
            {
                Msg.Show("نام خیّر الزامی است.");
                return;
            }

            CaseFundingService.SaveSponsor(0, values["Name"], values["Phone"], values["Email"],
                values["Address"], values["Notes"], values["Active"] == "1");
            LoadSponsors();
        }

        private void EditSponsor()
        {
            if (!RequireAdmin()) return;

            int id = SelectedId(_gridSponsors, "SponsorID");
            if (id <= 0) { Msg.Show("اول یک خیّر را انتخاب کنید."); return; }

            DataRowView row = (DataRowView)_gridSponsors.CurrentRow.DataBoundItem;

            Dictionary<string, string> values = EntPrompt.Edit(this, "ویرایش خیّر",
                EntField.Text("Name", "نام خیّر", Convert.ToString(row["Name"])),
                EntField.Text("Phone", "تلفن", Convert.ToString(row["Phone"])),
                EntField.Text("Email", "ایمیل", Convert.ToString(row["Email"])),
                EntField.Multiline("Address", "آدرس", Convert.ToString(row["Address"])),
                EntField.Multiline("Notes", "یادداشت", Convert.ToString(row["Notes"])),
                EntField.Check("Active", "فعال", Convert.ToInt32(row["IsActive"]) != 0));

            if (values == null) return;

            if (string.IsNullOrWhiteSpace(values["Name"]))
            {
                Msg.Show("نام خیّر الزامی است.");
                return;
            }

            CaseFundingService.SaveSponsor(id, values["Name"], values["Phone"], values["Email"],
                values["Address"], values["Notes"], values["Active"] == "1");
            LoadSponsors();
        }

        private void ToggleSponsor()
        {
            if (!RequireAdmin()) return;

            int id = SelectedId(_gridSponsors, "SponsorID");
            if (id <= 0) { Msg.Show("اول یک خیّر را انتخاب کنید."); return; }

            DataRowView row = (DataRowView)_gridSponsors.CurrentRow.DataBoundItem;
            bool active = Convert.ToInt32(row["IsActive"]) != 0;

            if (active)
            {
                int usage = CaseFundingService.GetSponsorUsage(id);
                if (usage > 0 && !UiTheme.ShowConfirm(this,
                        "این خیّر در " + usage + " پرونده فعال است. تخصیص‌های موجود دست‌نخورده می‌مانند. ادامه؟",
                        "غیرفعال‌سازی خیّر"))
                    return;
            }

            CaseFundingService.SetSponsorActive(id, !active);
            LoadSponsors();
        }

        // ─── کمکی‌ها (هم‌الگوی Enterprise/FrmRules) ─────────────────────────
        private bool RequireAdmin()
        {
            if (SecurityContext.IsAdmin() || SecurityContext.IsSuperAdmin()) return true;
            Msg.Show("این بخش فقط برای مدیر سیستم در دسترس است.");
            return false;
        }

        private static int SelectedId(DataGridView grid, string column)
        {
            if (grid.CurrentRow == null || !grid.Columns.Contains(column)) return 0;
            object value = grid.CurrentRow.Cells[column].Value;
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static void HideColumns(DataGridView grid, params string[] columns)
        {
            foreach (string column in columns)
                if (grid.Columns.Contains(column)) grid.Columns[column].Visible = false;
        }

        private static void Header(DataGridView grid, string column, string text)
        {
            if (grid.Columns.Contains(column)) grid.Columns[column].HeaderText = text;
        }

        private static Panel MakeButtonBar(params Button[] buttons)
        {
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft = RightToLeft.Yes,
                WrapContents = false
            };
            foreach (Button button in buttons) flow.Controls.Add(button);
            toolbar.Controls.Add(flow);
            return toolbar;
        }

        private static DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RightToLeft = RightToLeft.Yes,
                RowTemplate = { Height = 28 }
            };
            UiTheme.StyleGrid(grid);
            return grid;
        }

        private static Button MakeButton(string text, string icon, Color color, Action action)
        {
            Button button = UiTheme.CreateButton(text, icon, color);
            button.Width = 124;
            button.Margin = new Padding(4, 2, 4, 2);
            button.Click += delegate { action(); };
            return button;
        }
    }
}

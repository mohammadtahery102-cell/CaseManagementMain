using CaseManagement.Enterprise;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // Phase 5.5-C — مدیریتِ قواعدِ مساعدت.
    //
    // ساختار: گریدِ قواعد (بالا) + گریدِ شرط‌های قاعدهٔ انتخاب‌شده (پایین) +
    // کادرِ «منطقِ محاسبه» به‌صورتِ متنِ خوانا — دقیقاً همان چیدمانِ
    // Enterprise/FrmRules که کاربر از قبل با آن آشناست.
    //
    // «شرح قاعده» از خودِ شرط‌ها ساخته می‌شود (AssistanceRuleService.DescribeRule)،
    // نه از یک فیلدِ توضیحِ دستی — وگرنه با ویرایشِ شرط‌ها، توضیح کهنه می‌ماند
    // و مدیر را دقیقاً همان‌جا گمراه می‌کند که به آن تکیه کرده.
    //
    // ⚠ این صفحه هیچ پرداختی نمی‌سازد. قواعد فقط «مبلغِ پیشنهادی» تولید
    // می‌کنند؛ تصمیمِ نهایی همیشه با کاربرِ پرونده است.
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class FrmAssistanceRuleAdmin : Form
    {
        private DataGridView _gridRules, _gridConditions;
        private TextBox _txtSearch;
        private CheckBox _chkInactive;
        private TextBox _txtExplanation;

        public FrmAssistanceRuleAdmin()
        {
            BuildUi();
            LoadRules();
        }

        private void BuildUi()
        {
            Text = "قواعد مساعدت";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1180, 720);

            // ─── پایین: شرط‌ها + منطقِ محاسبه ───────────────────────────────
            _gridConditions = CreateGrid();

            _txtExplanation = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                RightToLeft = RightToLeft.Yes,
                BackColor = UiTheme.CardBack,
                Font = UiTheme.Font(UiTheme.SizeSmall)
            };

            var explanationPanel = new Panel { Dock = DockStyle.Right, Width = 380, BackColor = UiTheme.CardBack };
            explanationPanel.Controls.Add(_txtExplanation);
            explanationPanel.Controls.Add(SectionLabel("منطق محاسبه"));

            var conditionsPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            conditionsPanel.Controls.Add(_gridConditions);
            conditionsPanel.Controls.Add(MakeButtonBar(
                MakeButton("شرط جدید", "＋", UiTheme.Primary, AddCondition),
                MakeButton("حذف شرط", "🗑", UiTheme.Danger, DeleteCondition)));
            conditionsPanel.Controls.Add(SectionLabel("شرط‌های قاعده انتخاب‌شده (همه باید برقرار باشند)"));

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 260 };
            bottom.Controls.Add(conditionsPanel);
            bottom.Controls.Add(explanationPanel);

            // ─── بالا: قواعد ────────────────────────────────────────────────
            _gridRules = CreateGrid();
            _gridRules.SelectionChanged += delegate { LoadConditions(); };

            _txtSearch = new TextBox { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
            _txtSearch.TextChanged += delegate { LoadRules(); };

            _chkInactive = new CheckBox
            {
                Text = "نمایش غیرفعال‌ها",
                AutoSize = true,
                Margin = new Padding(10, 8, 10, 0)
            };
            _chkInactive.CheckedChanged += delegate { LoadRules(); };

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
            searchLayout.Controls.Add(_txtSearch, 1, 0);
            searchLayout.Controls.Add(_chkInactive, 2, 0);
            searchBar.Controls.Add(searchLayout);

            var main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_gridRules);
            main.Controls.Add(searchBar);
            main.Controls.Add(MakeButtonBar(
                MakeButton("قاعده جدید", "＋", UiTheme.Primary, AddRule),
                MakeButton("ویرایش", "✎", UiTheme.PrimaryLight, EditRule),
                MakeButton("فعال/غیرفعال", "⏻", UiTheme.Warning, ToggleRule),
                MakeButton("حذف", "🗑", UiTheme.Danger, DeleteRule),
                MakeButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight, delegate { LoadRules(); })));

            Controls.Add(main);
            Controls.Add(bottom);
        }

        // ─── بارگذاری ───────────────────────────────────────────────────────
        private void LoadRules()
        {
            try
            {
                _gridRules.DataSource = AssistanceRuleService.GetRuleTable(_txtSearch.Text, _chkInactive.Checked);
                HideColumns(_gridRules, "RuleID", "IsActive");
                Header(_gridRules, "Name", "نام قاعده");
                Header(_gridRules, "Priority", "اولویت");
                Header(_gridRules, "Amount", "مبلغ پیشنهادی");
                Header(_gridRules, "ConditionCount", "تعداد شرط");
                Header(_gridRules, "Notes", "یادداشت");
                Header(_gridRules, "StatusText", "وضعیت");
                LoadConditions();
            }
            catch (Exception ex) { Msg.Show("خطا در بارگذاری قواعد: " + ex.Message); }
        }

        private void LoadConditions()
        {
            int ruleId = SelectedRuleId();
            if (ruleId <= 0)
            {
                _gridConditions.DataSource = null;
                _txtExplanation.Text = "";
                return;
            }

            try
            {
                _gridConditions.DataSource = AssistanceRuleService.GetConditionTable(ruleId);
                HideColumns(_gridConditions, "ConditionID");
                Header(_gridConditions, "FieldName", "فیلد");
                Header(_gridConditions, "Operator", "عملگر");
                Header(_gridConditions, "Value", "مقدار");

                _txtExplanation.Text = AssistanceRuleService.DescribeRule(ruleId);
            }
            catch (Exception ex) { Msg.Show("خطا در بارگذاری شرط‌ها: " + ex.Message); }
        }

        // ─── قواعد ──────────────────────────────────────────────────────────
        private void AddRule()
        {
            if (!RequireAdmin()) return;

            Dictionary<string, string> values = EntPrompt.Edit(this, "قاعده مساعدت جدید",
                EntField.Text("Name", "نام قاعده", ""),
                EntField.Number("Priority", "اولویت (عدد کمتر = مهم‌تر)", "100"),
                EntField.Number("Amount", "مبلغ پیشنهادی", "0"),
                EntField.Multiline("Notes", "یادداشت", ""),
                EntField.Check("Active", "فعال", true));

            if (values == null) return;

            if (string.IsNullOrWhiteSpace(values["Name"]))
            {
                Msg.Show("نام قاعده الزامی است.");
                return;
            }

            int ruleId = AssistanceRuleService.SaveRule(0, values["Name"],
                ParseInt(values["Priority"], 100), ParseDecimal(values["Amount"]),
                values["Notes"], values["Active"] == "1");

            LoadRules();

            if (ruleId > 0)
                Msg.Show("قاعده ساخته شد. توجه: قاعده‌ای که هیچ شرطی ندارد هرگز تطبیق نمی‌کند — " +
                         "برایش شرط تعریف کنید.");
        }

        private void EditRule()
        {
            if (!RequireAdmin()) return;

            int ruleId = SelectedRuleId();
            if (ruleId <= 0) { Msg.Show("اول یک قاعده را انتخاب کنید."); return; }

            DataRowView row = (DataRowView)_gridRules.CurrentRow.DataBoundItem;

            Dictionary<string, string> values = EntPrompt.Edit(this, "ویرایش قاعده مساعدت",
                EntField.Text("Name", "نام قاعده", Convert.ToString(row["Name"])),
                EntField.Number("Priority", "اولویت (عدد کمتر = مهم‌تر)", Convert.ToString(row["Priority"])),
                EntField.Number("Amount", "مبلغ پیشنهادی", Convert.ToString(row["Amount"])),
                EntField.Multiline("Notes", "یادداشت", Convert.ToString(row["Notes"])),
                EntField.Check("Active", "فعال", Convert.ToInt32(row["IsActive"]) != 0));

            if (values == null) return;

            AssistanceRuleService.SaveRule(ruleId, values["Name"],
                ParseInt(values["Priority"], 100), ParseDecimal(values["Amount"]),
                values["Notes"], values["Active"] == "1");

            LoadRules();
        }

        private void ToggleRule()
        {
            if (!RequireAdmin()) return;

            int ruleId = SelectedRuleId();
            if (ruleId <= 0) { Msg.Show("اول یک قاعده را انتخاب کنید."); return; }

            DataRowView row = (DataRowView)_gridRules.CurrentRow.DataBoundItem;
            bool active = Convert.ToInt32(row["IsActive"]) != 0;

            AssistanceRuleService.SetRuleActive(ruleId, !active);
            LoadRules();
        }

        private void DeleteRule()
        {
            if (!RequireAdmin()) return;

            int ruleId = SelectedRuleId();
            if (ruleId <= 0) { Msg.Show("اول یک قاعده را انتخاب کنید."); return; }

            if (!UiTheme.ShowConfirm(this,
                    "این قاعده و همهٔ شرط‌هایش حذف شوند؟ مبالغِ پیشنهادیِ قبلی تغییر نمی‌کنند.",
                    "حذف قاعده"))
                return;

            AssistanceRuleService.DeleteRule(ruleId);
            LoadRules();
        }

        // ─── شرط‌ها ─────────────────────────────────────────────────────────
        private void AddCondition()
        {
            if (!RequireAdmin()) return;

            int ruleId = SelectedRuleId();
            if (ruleId <= 0) { Msg.Show("اول یک قاعده را انتخاب کنید."); return; }

            // فهرستِ فیلدها و عملگرها از خودِ موتور می‌آید — نه فهرستِ هاردکد،
            // تا افزودنِ حقیقتِ تازه به CaseFacts خودبه‌خود اینجا دیده شود.
            Dictionary<string, string> values = EntPrompt.Edit(this, "شرط جدید",
                EntField.Combo("Field", "فیلد", CaseFacts.FactRequestType,
                    AssistanceRuleService.FactItems()),
                EntField.Combo("Operator", "عملگر", AssistanceRuleService.OpEquals,
                    AssistanceRuleService.OperatorItems()),
                EntField.Text("Value", "مقدار", ""));

            if (values == null) return;

            AssistanceRuleService.AddCondition(ruleId, values["Field"], values["Operator"], values["Value"]);
            LoadConditions();
        }

        private void DeleteCondition()
        {
            if (!RequireAdmin()) return;

            if (_gridConditions.CurrentRow == null || !_gridConditions.Columns.Contains("ConditionID"))
            {
                Msg.Show("اول یک شرط را انتخاب کنید.");
                return;
            }

            object value = _gridConditions.CurrentRow.Cells["ConditionID"].Value;
            if (value == null || value == DBNull.Value) return;

            AssistanceRuleService.DeleteCondition(Convert.ToInt32(value));
            LoadConditions();
        }

        // ─── کمکی‌ها ────────────────────────────────────────────────────────
        private int SelectedRuleId()
        {
            if (_gridRules.CurrentRow == null || !_gridRules.Columns.Contains("RuleID")) return 0;
            object value = _gridRules.CurrentRow.Cells["RuleID"].Value;
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private bool RequireAdmin()
        {
            if (SecurityContext.IsAdmin() || SecurityContext.IsSuperAdmin()) return true;
            Msg.Show("این بخش فقط برای مدیر سیستم در دسترس است.");
            return false;
        }

        private static int ParseInt(string value, int fallback)
        {
            int result;
            return int.TryParse((value ?? "").Trim(), out result) ? result : fallback;
        }

        private static decimal ParseDecimal(string value)
        {
            decimal result;
            return decimal.TryParse((value ?? "").Trim(), out result) ? result : 0m;
        }

        private static Label SectionLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "  " + text,
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            };
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

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «مدیریت گردش‌کار» — تعریف مراحل و گذارها + مشاهده رکوردهای در گردش.
    //
    // این پنجره فقط جداول Ent* را تغییر می‌دهد و هیچ داده‌ای از پرونده‌ها،
    // مالی یا حسابداری را دست نمی‌زند.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmWorkflowAdmin : Form
    {
        private ComboBox     _cmbWorkflow;
        private DataGridView _gridStates, _gridTransitions, _gridInstances, _gridHistory;
        private Label        _lblInfo;

        private List<WorkflowModel> _workflows = new List<WorkflowModel>();

        public FrmWorkflowAdmin()
        {
            BuildUi();
            LoadWorkflows();
        }

        private WorkflowModel Current
        {
            get { return _cmbWorkflow.SelectedItem as WorkflowModel; }
        }

        // ─── ساخت رابط ─────────────────────────────────────────────────────
        private void BuildUi()
        {
            Text              = "مدیریت گردش‌کار";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1180, 700);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
            tabs.TabPages.Add(BuildDefinitionTab());
            tabs.TabPages.Add(BuildInstancesTab());
            Controls.Add(tabs);

            Controls.Add(BuildHeader());
        }

        private Panel BuildHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.PrimaryDark };

            Label title = new Label
            {
                Text      = "مدیریت گردش‌کار",
                Dock      = DockStyle.Top,
                Height    = 38,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            };

            _lblInfo = new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(0xCF, 0xDD, 0xEE),
                Font      = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopLeft,
                Padding   = new Padding(0, 0, 20, 0),
                Text      = "مراحل و گذارهای هر گردش‌کار را این‌جا تعریف کنید."
            };

            header.Controls.Add(_lblInfo);
            header.Controls.Add(title);
            return header;
        }

        private TabPage BuildDefinitionTab()
        {
            TabPage page = new TabPage("تعریف گردش‌کار")
            {
                BackColor   = UiTheme.Background,
                RightToLeft = RightToLeft.Yes,
                Padding     = new Padding(10)
            };

            SplitContainer split = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Vertical,
                SplitterDistance = 430,
                RightToLeft      = RightToLeft.Yes
            };

            // ── مراحل ──
            _gridStates = CreateGrid();
            _gridStates.SelectionChanged += delegate { };

            Panel statePanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            statePanel.Controls.Add(_gridStates);
            statePanel.Controls.Add(CreateGridToolbar("مراحل",
                AddState, EditState, DeleteState));
            split.Panel1.Controls.Add(statePanel);

            // ── گذارها ──
            _gridTransitions = CreateGrid();

            Panel transitionPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            transitionPanel.Controls.Add(_gridTransitions);
            transitionPanel.Controls.Add(CreateGridToolbar("گذارها",
                AddTransition, EditTransition, DeleteTransition));
            split.Panel2.Controls.Add(transitionPanel);

            page.Controls.Add(split);
            page.Controls.Add(BuildWorkflowBar());
            return page;
        }

        private Panel BuildWorkflowBar()
        {
            Panel bar = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(4, 10, 4, 10) };

            _cmbWorkflow = new ComboBox
            {
                Dock          = DockStyle.Right,
                Width         = 320,
                DropDownStyle = ComboBoxStyle.DropDownList,
                RightToLeft   = RightToLeft.Yes
            };
            _cmbWorkflow.SelectedIndexChanged += delegate { LoadDefinition(); };

            Label label = new Label
            {
                Dock      = DockStyle.Right,
                Width     = 90,
                Text      = "گردش‌کار:",
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextDark
            };

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft   = RightToLeft.Yes,
                WrapContents  = false
            };
            buttons.Controls.Add(MakeButton("گردش‌کار جدید", "＋", UiTheme.Primary, NewWorkflow));
            buttons.Controls.Add(MakeButton("ویرایش", "✎", UiTheme.PrimaryLight, EditWorkflow));

            bar.Controls.Add(buttons);
            bar.Controls.Add(_cmbWorkflow);
            bar.Controls.Add(label);
            return bar;
        }

        private TabPage BuildInstancesTab()
        {
            TabPage page = new TabPage("رکوردهای در گردش")
            {
                BackColor   = UiTheme.Background,
                RightToLeft = RightToLeft.Yes,
                Padding     = new Padding(10)
            };

            _gridHistory = CreateGrid();
            Panel historyPanel = new Panel { Dock = DockStyle.Bottom, Height = 200, BackColor = UiTheme.CardBack };
            historyPanel.Controls.Add(_gridHistory);
            historyPanel.Controls.Add(new Label
            {
                Dock      = DockStyle.Top,
                Height    = 28,
                Text      = "  تاریخچه گذارهای رکورد انتخاب‌شده",
                Font      = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            });

            _gridInstances = CreateGrid();
            _gridInstances.SelectionChanged += delegate { LoadHistory(); };

            Panel instancePanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            instancePanel.Controls.Add(_gridInstances);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(6) };
            toolbar.Controls.Add(MakeButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight, delegate { LoadInstances(); }));
            instancePanel.Controls.Add(toolbar);

            page.Controls.Add(instancePanel);
            page.Controls.Add(historyPanel);

            page.Enter += delegate { LoadInstances(); };
            return page;
        }

        private Panel CreateGridToolbar(string title, Action add, Action edit, Action remove)
        {
            Panel bar = new Panel { Dock = DockStyle.Top, Height = 78, Padding = new Padding(6, 4, 6, 4) };

            bar.Controls.Add(new Label
            {
                Dock      = DockStyle.Top,
                Height    = 26,
                Text      = title,
                Font      = UiTheme.FontBold(UiTheme.SizeMedium),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            });

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft   = RightToLeft.Yes,
                WrapContents  = false
            };
            buttons.Controls.Add(MakeButton("افزودن", "＋", UiTheme.Primary,      add));
            buttons.Controls.Add(MakeButton("ویرایش", "✎", UiTheme.PrimaryLight, edit));
            buttons.Controls.Add(MakeButton("حذف",   "🗑", UiTheme.Danger,       remove));

            bar.Controls.Add(buttons);
            return bar;
        }

        private static Button MakeButton(string text, string icon, Color color, Action action)
        {
            Button button = UiTheme.CreateButton(text, icon, color);
            button.Width  = 118;
            button.Margin = new Padding(4, 2, 4, 2);
            button.Click += delegate { action(); };
            return button;
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                RowTemplate           = { Height = 28 }
            };
            UiTheme.StyleGrid(grid);
            return grid;
        }

        // ─── بارگذاری داده ─────────────────────────────────────────────────
        private void LoadWorkflows()
        {
            _workflows = WorkflowService.GetWorkflows();

            _cmbWorkflow.Items.Clear();
            foreach (WorkflowModel workflow in _workflows)
                _cmbWorkflow.Items.Add(workflow);

            _cmbWorkflow.DisplayMember = "Name";

            if (_cmbWorkflow.Items.Count > 0)
                _cmbWorkflow.SelectedIndex = 0;
            else
                LoadDefinition();
        }

        private void LoadDefinition()
        {
            WorkflowModel workflow = Current;

            if (workflow == null)
            {
                _gridStates.DataSource      = null;
                _gridTransitions.DataSource = null;
                return;
            }

            _lblInfo.Text = "موجودیت: " + workflow.EntityName +
                            "   |   وضعیت: " + (workflow.IsActive ? "فعال" : "غیرفعال");

            _gridStates.DataSource = EntDb.Query(@"
SELECT StateID    AS 'شناسه',
       Name       AS 'نام مرحله',
       Code       AS 'کد',
       CASE IsInitial WHEN 1 THEN 'بله' ELSE '' END AS 'شروع',
       CASE IsFinal   WHEN 1 THEN 'بله' ELSE '' END AS 'پایانی',
       SortOrder  AS 'ترتیب'
FROM   EntWorkflowState
WHERE  WorkflowID = @WF
ORDER  BY SortOrder, StateID;", "@WF", workflow.WorkflowID);

            _gridTransitions.DataSource = EntDb.Query(@"
SELECT t.TransitionID AS 'شناسه',
       t.Name         AS 'نام گذار',
       sf.Name        AS 'از مرحله',
       st.Name        AS 'به مرحله',
       COALESCE(t.RequiredPermission, '') AS 'مجوز لازم',
       CASE t.RequiresApproval WHEN 1 THEN 'بله' ELSE '' END AS 'نیازمند تأیید',
       t.SortOrder    AS 'ترتیب'
FROM   EntWorkflowTransition t
LEFT   JOIN EntWorkflowState sf ON sf.StateID = t.FromStateID
LEFT   JOIN EntWorkflowState st ON st.StateID = t.ToStateID
WHERE  t.WorkflowID = @WF
ORDER  BY t.SortOrder, t.TransitionID;", "@WF", workflow.WorkflowID);

            HideIdColumn(_gridStates);
            HideIdColumn(_gridTransitions);
        }

        private void LoadInstances()
        {
            // فیلتر مرکز مطابق معماری موجود: ۰ یعنی «همه مراکز» (فقط مدیر کل).
            int centerFilter = SecurityContext.CenterFilterId;

            _gridInstances.DataSource = EntDb.Query(@"
SELECT i.InstanceID AS 'شناسه',
       w.Name       AS 'گردش‌کار',
       i.EntityName AS 'موجودیت',
       i.EntityID   AS 'شناسه رکورد',
       s.Name       AS 'مرحله جاری',
       i.Status     AS 'وضعیت',
       i.StartedBy  AS 'شروع‌کننده',
       i.StartedAt  AS 'تاریخ شروع'
FROM   EntWorkflowInstance i
LEFT   JOIN EntWorkflow w      ON w.WorkflowID = i.WorkflowID
LEFT   JOIN EntWorkflowState s ON s.StateID    = i.CurrentStateID
WHERE  (@Center = 0 OR IFNULL(i.CenterID, 0) = @Center)
ORDER  BY i.InstanceID DESC;", "@Center", centerFilter);

            HideIdColumn(_gridInstances);
            LoadHistory();
        }

        private void LoadHistory()
        {
            int instanceId = SelectedId(_gridInstances);

            _gridHistory.DataSource = instanceId <= 0
                ? null
                : WorkflowService.GetHistory(instanceId);
        }

        private static void HideIdColumn(DataGridView grid)
        {
            if (grid.Columns.Contains("شناسه"))
                grid.Columns["شناسه"].Visible = false;
        }

        private static int SelectedId(DataGridView grid)
        {
            if (grid.CurrentRow == null) return 0;
            if (!grid.Columns.Contains("شناسه")) return 0;
            return EntDb.ToInt(grid.CurrentRow.Cells["شناسه"].Value);
        }

        // ─── عملیات گردش‌کار ───────────────────────────────────────────────
        private void NewWorkflow()
        {
            if (!RequireAdmin()) return;

            Dictionary<string, string> values = EntPrompt.Edit(this, "گردش‌کار جدید",
                EntField.Text("Code",   "کد یکتا",  ""),
                EntField.Text("Name",   "نام",      ""),
                EntField.Text("Entity", "موجودیت",  EnterpriseInitializer.EntityCase),
                EntField.Multiline("Description", "توضیح", ""),
                EntField.Check("IsActive", "فعال", true));

            if (values == null) return;

            if (values["Code"].Length == 0 || values["Name"].Length == 0)
            {
                UiTheme.ShowWarning(this, "کد و نام الزامی است.");
                return;
            }

            try
            {
                EntDb.Exec(@"
INSERT INTO EntWorkflow (Code, Name, EntityName, Description, IsActive, CenterID, CreatedBy)
VALUES (@Code, @Name, @Entity, @Desc, @Active, @Center, @By);",
                    "@Code",   values["Code"],
                    "@Name",   values["Name"],
                    "@Entity", values["Entity"],
                    "@Desc",   values["Description"],
                    "@Active", values["IsActive"] == "1" ? 1 : 0,
                    "@Center", SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : null,
                    "@By",     SecurityContext.Username);

                LoadWorkflows();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ثبت گردش‌کار انجام نشد: " + ex.Message);
            }
        }

        private void EditWorkflow()
        {
            if (!RequireAdmin()) return;

            WorkflowModel workflow = Current;
            if (workflow == null) return;

            Dictionary<string, string> values = EntPrompt.Edit(this, "ویرایش گردش‌کار",
                EntField.Text("Name", "نام", workflow.Name),
                EntField.Multiline("Description", "توضیح", workflow.Description),
                EntField.Check("IsActive", "فعال", workflow.IsActive));

            if (values == null) return;

            EntDb.Exec(@"
UPDATE EntWorkflow SET Name = @Name, Description = @Desc, IsActive = @Active
WHERE  WorkflowID = @Id;",
                "@Name",   values["Name"],
                "@Desc",   values["Description"],
                "@Active", values["IsActive"] == "1" ? 1 : 0,
                "@Id",     workflow.WorkflowID);

            LoadWorkflows();
        }

        // ─── عملیات مرحله ──────────────────────────────────────────────────
        private void AddState()
        {
            if (!RequireAdmin() || Current == null) return;

            Dictionary<string, string> values = EntPrompt.Edit(this, "مرحله جدید",
                EntField.Text("Code", "کد", ""),
                EntField.Text("Name", "نام", ""),
                EntField.Check("IsInitial", "مرحله شروع", false),
                EntField.Check("IsFinal",   "مرحله پایانی", false),
                EntField.Number("SortOrder", "ترتیب", "0"));

            if (values == null) return;

            if (values["Code"].Length == 0 || values["Name"].Length == 0)
            {
                UiTheme.ShowWarning(this, "کد و نام الزامی است.");
                return;
            }

            try
            {
                // فقط یک مرحله شروع مجاز است.
                if (values["IsInitial"] == "1")
                    EntDb.Exec("UPDATE EntWorkflowState SET IsInitial = 0 WHERE WorkflowID = @WF;",
                        "@WF", Current.WorkflowID);

                EntDb.Exec(@"
INSERT INTO EntWorkflowState (WorkflowID, Code, Name, IsInitial, IsFinal, SortOrder)
VALUES (@WF, @Code, @Name, @Init, @Final, @Sort);",
                    "@WF",    Current.WorkflowID,
                    "@Code",  values["Code"],
                    "@Name",  values["Name"],
                    "@Init",  values["IsInitial"] == "1" ? 1 : 0,
                    "@Final", values["IsFinal"]   == "1" ? 1 : 0,
                    "@Sort",  EntDb.ToInt(values["SortOrder"]));

                LoadDefinition();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ثبت مرحله انجام نشد: " + ex.Message);
            }
        }

        private void EditState()
        {
            if (!RequireAdmin()) return;

            int stateId = SelectedId(_gridStates);
            if (stateId <= 0) { UiTheme.ShowWarning(this, "یک مرحله را انتخاب کنید."); return; }

            WorkflowStateModel state = WorkflowService.GetState(stateId);
            if (state == null) return;

            Dictionary<string, string> values = EntPrompt.Edit(this, "ویرایش مرحله",
                EntField.Text("Name", "نام", state.Name),
                EntField.Check("IsInitial", "مرحله شروع", state.IsInitial),
                EntField.Check("IsFinal",   "مرحله پایانی", state.IsFinal),
                EntField.Number("SortOrder", "ترتیب", state.SortOrder.ToString()));

            if (values == null) return;

            if (values["IsInitial"] == "1")
                EntDb.Exec("UPDATE EntWorkflowState SET IsInitial = 0 WHERE WorkflowID = @WF;",
                    "@WF", state.WorkflowID);

            EntDb.Exec(@"
UPDATE EntWorkflowState
SET    Name = @Name, IsInitial = @Init, IsFinal = @Final, SortOrder = @Sort
WHERE  StateID = @Id;",
                "@Name",  values["Name"],
                "@Init",  values["IsInitial"] == "1" ? 1 : 0,
                "@Final", values["IsFinal"]   == "1" ? 1 : 0,
                "@Sort",  EntDb.ToInt(values["SortOrder"]),
                "@Id",    stateId);

            LoadDefinition();
        }

        private void DeleteState()
        {
            if (!RequireAdmin()) return;

            int stateId = SelectedId(_gridStates);
            if (stateId <= 0) { UiTheme.ShowWarning(this, "یک مرحله را انتخاب کنید."); return; }

            // مرحله‌ای که رکوردی در آن ایستاده یا گذاری به آن وصل است حذف نمی‌شود.
            long inUse = EntDb.ToInt64(EntDb.Scalar(
                "SELECT COUNT(*) FROM EntWorkflowInstance WHERE CurrentStateID = @Id;", "@Id", stateId));

            if (inUse > 0)
            {
                UiTheme.ShowWarning(this, "این مرحله برای " + inUse + " رکورد در حال استفاده است و حذف نمی‌شود.");
                return;
            }

            if (!UiTheme.ShowConfirm(this, "مرحله انتخاب‌شده و گذارهای مرتبط با آن حذف شوند؟", "حذف مرحله"))
                return;

            EntDb.Exec("DELETE FROM EntWorkflowTransition WHERE FromStateID = @Id OR ToStateID = @Id;", "@Id", stateId);
            EntDb.Exec("DELETE FROM EntWorkflowState WHERE StateID = @Id;", "@Id", stateId);

            LoadDefinition();
        }

        // ─── عملیات گذار ───────────────────────────────────────────────────
        private void AddTransition()
        {
            if (!RequireAdmin() || Current == null) return;

            List<KeyValuePair<string, string>> states = StateItems();
            if (states.Count < 2)
            {
                UiTheme.ShowWarning(this, "برای تعریف گذار حداقل دو مرحله لازم است.");
                return;
            }

            Dictionary<string, string> values = EntPrompt.Edit(this, "گذار جدید",
                EntField.Text("Name", "نام گذار", ""),
                EntField.Combo("From", "از مرحله", states[0].Key, states),
                EntField.Combo("To",   "به مرحله", states[1].Key, states),
                EntField.Text("Perm", "مجوز لازم", ""),
                EntField.Check("Approval", "نیازمند تأیید", false),
                EntField.Number("SortOrder", "ترتیب", "0"));

            if (values == null) return;

            if (values["Name"].Length == 0)
            {
                UiTheme.ShowWarning(this, "نام گذار الزامی است.");
                return;
            }

            if (values["From"] == values["To"])
            {
                UiTheme.ShowWarning(this, "مرحله مبدأ و مقصد نمی‌توانند یکسان باشند.");
                return;
            }

            EntDb.Exec(@"
INSERT INTO EntWorkflowTransition
    (WorkflowID, FromStateID, ToStateID, Name, RequiredPermission, RequiresApproval, SortOrder)
VALUES
    (@WF, @From, @To, @Name, @Perm, @Appr, @Sort);",
                "@WF",   Current.WorkflowID,
                "@From", EntDb.ToInt(values["From"]),
                "@To",   EntDb.ToInt(values["To"]),
                "@Name", values["Name"],
                "@Perm", values["Perm"],
                "@Appr", values["Approval"] == "1" ? 1 : 0,
                "@Sort", EntDb.ToInt(values["SortOrder"]));

            LoadDefinition();
        }

        private void EditTransition()
        {
            if (!RequireAdmin()) return;

            int transitionId = SelectedId(_gridTransitions);
            if (transitionId <= 0) { UiTheme.ShowWarning(this, "یک گذار را انتخاب کنید."); return; }

            WorkflowTransitionModel transition = WorkflowService.GetTransition(transitionId);
            if (transition == null) return;

            List<KeyValuePair<string, string>> states = StateItems();

            Dictionary<string, string> values = EntPrompt.Edit(this, "ویرایش گذار",
                EntField.Text("Name", "نام گذار", transition.Name),
                EntField.Combo("From", "از مرحله", transition.FromStateID.ToString(), states),
                EntField.Combo("To",   "به مرحله", transition.ToStateID.ToString(), states),
                EntField.Text("Perm", "مجوز لازم", transition.RequiredPermission),
                EntField.Check("Approval", "نیازمند تأیید", transition.RequiresApproval),
                EntField.Number("SortOrder", "ترتیب", transition.SortOrder.ToString()));

            if (values == null) return;

            if (values["From"] == values["To"])
            {
                UiTheme.ShowWarning(this, "مرحله مبدأ و مقصد نمی‌توانند یکسان باشند.");
                return;
            }

            EntDb.Exec(@"
UPDATE EntWorkflowTransition
SET    Name = @Name, FromStateID = @From, ToStateID = @To,
       RequiredPermission = @Perm, RequiresApproval = @Appr, SortOrder = @Sort
WHERE  TransitionID = @Id;",
                "@Name", values["Name"],
                "@From", EntDb.ToInt(values["From"]),
                "@To",   EntDb.ToInt(values["To"]),
                "@Perm", values["Perm"],
                "@Appr", values["Approval"] == "1" ? 1 : 0,
                "@Sort", EntDb.ToInt(values["SortOrder"]),
                "@Id",   transitionId);

            LoadDefinition();
        }

        private void DeleteTransition()
        {
            if (!RequireAdmin()) return;

            int transitionId = SelectedId(_gridTransitions);
            if (transitionId <= 0) { UiTheme.ShowWarning(this, "یک گذار را انتخاب کنید."); return; }

            if (!UiTheme.ShowConfirm(this, "گذار انتخاب‌شده حذف شود؟", "حذف گذار"))
                return;

            EntDb.Exec("DELETE FROM EntWorkflowTransition WHERE TransitionID = @Id;", "@Id", transitionId);
            LoadDefinition();
        }

        private List<KeyValuePair<string, string>> StateItems()
        {
            List<KeyValuePair<string, string>> items = new List<KeyValuePair<string, string>>();
            if (Current == null) return items;

            foreach (WorkflowStateModel state in WorkflowService.GetStates(Current.WorkflowID))
                items.Add(new KeyValuePair<string, string>(state.StateID.ToString(), state.Name));

            return items;
        }

        private bool RequireAdmin()
        {
            if (SecurityContext.IsAdmin()) return true;

            UiTheme.ShowWarning(this, "تعریف گردش‌کار فقط برای مدیر سیستم مجاز است.");
            return false;
        }
    }
}

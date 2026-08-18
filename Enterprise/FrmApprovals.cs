using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «تأییدها» — کارتابل تأییدکننده، فهرست همه درخواست‌ها و تعریف زنجیره‌ها.
    //
    // این پنجره فقط جداول EntApproval* را تغییر می‌دهد. اعمال نتیجه تأیید روی
    // گردش‌کار از طریق ApprovalService انجام می‌شود، نه مستقیم از فرم.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmApprovals : Form
    {
        private DataGridView _gridInbox, _gridAll, _gridChains, _gridLevels, _gridActions;
        private ComboBox     _cmbStatus;
        private Label        _lblInfo;

        public FrmApprovals()
        {
            BuildUi();
            LoadInbox();
        }

        private void BuildUi()
        {
            Text              = "تأییدها";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1180, 720);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
            tabs.TabPages.Add(BuildInboxTab());
            tabs.TabPages.Add(BuildAllTab());
            tabs.TabPages.Add(BuildChainsTab());
            Controls.Add(tabs);

            Controls.Add(BuildHeader());
        }

        private Panel BuildHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.PrimaryDark };

            _lblInfo = new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(0xCF, 0xDD, 0xEE),
                Font      = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopLeft,
                Padding   = new Padding(0, 0, 20, 0),
                Text      = "درخواست‌هایی که تأیید آن‌ها با شماست در «کارتابل من» دیده می‌شود."
            };

            header.Controls.Add(_lblInfo);
            header.Controls.Add(new Label
            {
                Text      = "تأییدهای چندسطحی",
                Dock      = DockStyle.Top,
                Height    = 38,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            });
            return header;
        }

        // ─── کارتابل من ────────────────────────────────────────────────────
        private TabPage BuildInboxTab()
        {
            TabPage page = NewPage("کارتابل من");

            _gridActions = CreateGrid();
            Panel actionsPanel = new Panel { Dock = DockStyle.Bottom, Height = 170, BackColor = UiTheme.CardBack };
            actionsPanel.Controls.Add(_gridActions);
            actionsPanel.Controls.Add(SectionLabel("تصمیم‌های ثبت‌شده روی درخواست انتخاب‌شده"));

            _gridInbox = CreateGrid();
            _gridInbox.SelectionChanged += delegate { LoadActions(_gridInbox); };

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_gridInbox);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            FlowLayoutPanel buttons = NewButtonBar();
            buttons.Controls.Add(MakeButton("تأیید", "✔", UiTheme.Success,      delegate { Decide(true);  }));
            buttons.Controls.Add(MakeButton("رد",    "✖", UiTheme.Danger,       delegate { Decide(false); }));
            buttons.Controls.Add(MakeButton("تازه‌سازی", "⟳", UiTheme.PrimaryLight, delegate { LoadInbox(); }));
            toolbar.Controls.Add(buttons);
            main.Controls.Add(toolbar);

            page.Controls.Add(main);
            page.Controls.Add(actionsPanel);
            page.Enter += delegate { LoadInbox(); };
            return page;
        }

        // ─── همه درخواست‌ها ────────────────────────────────────────────────
        private TabPage BuildAllTab()
        {
            TabPage page = NewPage("همه درخواست‌ها");

            _gridAll = CreateGrid();

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            main.Controls.Add(_gridAll);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };

            _cmbStatus = new ComboBox
            {
                Dock          = DockStyle.Right,
                Width         = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                RightToLeft   = RightToLeft.Yes
            };
            _cmbStatus.Items.AddRange(new object[]
            {
                "همه", ApprovalService.StatusPending, ApprovalService.StatusApproved,
                ApprovalService.StatusRejected, ApprovalService.StatusCanceled
            });
            _cmbStatus.SelectedIndex = 0;
            _cmbStatus.SelectedIndexChanged += delegate { LoadAll(); };

            FlowLayoutPanel buttons = NewButtonBar();
            buttons.Controls.Add(MakeButton("لغو درخواست", "✖", UiTheme.Danger,       CancelRequest));
            buttons.Controls.Add(MakeButton("تازه‌سازی",   "⟳", UiTheme.PrimaryLight, delegate { LoadAll(); }));

            toolbar.Controls.Add(buttons);
            toolbar.Controls.Add(_cmbStatus);
            main.Controls.Add(toolbar);

            page.Controls.Add(main);
            page.Enter += delegate { LoadAll(); };
            return page;
        }

        // ─── زنجیره‌ها و سطوح ──────────────────────────────────────────────
        private TabPage BuildChainsTab()
        {
            TabPage page = NewPage("زنجیره‌های تأیید");

            SplitContainer split = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Vertical,
                SplitterDistance = 430,
                RightToLeft      = RightToLeft.Yes
            };

            _gridChains = CreateGrid();
            _gridChains.SelectionChanged += delegate { LoadLevels(); };

            Panel chainPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            chainPanel.Controls.Add(_gridChains);
            chainPanel.Controls.Add(GridToolbar("زنجیره‌ها", AddChain, EditChain, DeleteChain));
            split.Panel1.Controls.Add(chainPanel);

            _gridLevels = CreateGrid();

            Panel levelPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack };
            levelPanel.Controls.Add(_gridLevels);
            levelPanel.Controls.Add(GridToolbar("سطوح", AddLevel, EditLevel, DeleteLevel));
            split.Panel2.Controls.Add(levelPanel);

            page.Controls.Add(split);
            page.Enter += delegate { LoadChains(); };
            return page;
        }

        // ─── بارگذاری ──────────────────────────────────────────────────────
        private void LoadInbox()
        {
            _gridInbox.DataSource = ApprovalService.GetMyInbox();
            HideHelperColumns(_gridInbox);
            LoadActions(_gridInbox);
        }

        private void LoadAll()
        {
            string status = _cmbStatus.SelectedIndex <= 0 ? "" : Convert.ToString(_cmbStatus.SelectedItem);
            _gridAll.DataSource = ApprovalService.GetRequests(status);
            HideHelperColumns(_gridAll);
        }

        private void LoadActions(DataGridView source)
        {
            int requestId = SelectedId(source);

            _gridActions.DataSource = requestId <= 0
                ? null
                : ApprovalService.GetActions(requestId);
        }

        private void LoadChains()
        {
            _gridChains.DataSource = EntDb.Query(@"
SELECT ChainID    AS 'شناسه',
       Name       AS 'نام زنجیره',
       Code       AS 'کد',
       EntityName AS 'موجودیت',
       CASE IsActive WHEN 1 THEN 'فعال' ELSE 'غیرفعال' END AS 'وضعیت'
FROM   EntApprovalChain ORDER BY EntityName, Name;");

            HideHelperColumns(_gridChains);
            LoadLevels();
        }

        private void LoadLevels()
        {
            int chainId = SelectedId(_gridChains);

            if (chainId <= 0)
            {
                _gridLevels.DataSource = null;
                return;
            }

            _gridLevels.DataSource = EntDb.Query(@"
SELECT LevelID AS 'شناسه',
       LevelNo AS 'سطح',
       Name    AS 'نام سطح',
       COALESCE(ApproverRole, '')       AS 'نقش تأییدکننده',
       COALESCE(ApproverUserID, 0)      AS 'کاربر تأییدکننده',
       COALESCE(RequiredPermission, '') AS 'مجوز لازم'
FROM   EntApprovalLevel WHERE ChainID = @Id ORDER BY LevelNo;", "@Id", chainId);

            HideHelperColumns(_gridLevels);
        }

        // ─── عملیات کارتابل ────────────────────────────────────────────────
        private void Decide(bool approve)
        {
            int requestId = SelectedId(_gridInbox);

            if (requestId <= 0)
            {
                UiTheme.ShowWarning(this, "یک درخواست را انتخاب کنید.");
                return;
            }

            string comment = EntPrompt.AskText(this,
                approve ? "تأیید درخواست" : "رد درخواست", "توضیح (اختیاری)", "");

            // انصراف کاربر از دیالوگ
            if (comment == null) return;

            try
            {
                WorkflowActionResult result = ApprovalService.Decide(requestId, approve, comment);

                if (result.Applied) UiTheme.ShowSuccess(this, result.Message);
                else                UiTheme.ShowWarning(this, result.Message);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ثبت تصمیم انجام نشد: " + ex.Message);
            }

            LoadInbox();
        }

        private void CancelRequest()
        {
            int requestId = SelectedId(_gridAll);

            if (requestId <= 0)
            {
                UiTheme.ShowWarning(this, "یک درخواست را انتخاب کنید.");
                return;
            }

            if (!UiTheme.ShowConfirm(this, "درخواست انتخاب‌شده لغو شود؟", "لغو درخواست"))
                return;

            WorkflowActionResult result = ApprovalService.Cancel(requestId);

            if (result.Applied) UiTheme.ShowSuccess(this, result.Message);
            else                UiTheme.ShowWarning(this, result.Message);

            LoadAll();
        }

        // ─── عملیات زنجیره ─────────────────────────────────────────────────
        private void AddChain()
        {
            if (!RequireAdmin()) return;

            Dictionary<string, string> values = EntPrompt.Edit(this, "زنجیره تأیید جدید",
                EntField.Text("Code",   "کد یکتا", ""),
                EntField.Text("Name",   "نام",     ""),
                EntField.Text("Entity", "موجودیت", EnterpriseInitializer.EntityCase),
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
INSERT INTO EntApprovalChain (Code, Name, EntityName, IsActive, CenterID, CreatedBy)
VALUES (@Code, @Name, @Entity, @Active, @Center, @By);",
                    "@Code",   values["Code"],
                    "@Name",   values["Name"],
                    "@Entity", values["Entity"],
                    "@Active", values["IsActive"] == "1" ? 1 : 0,
                    "@Center", SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : null,
                    "@By",     SecurityContext.Username);

                LoadChains();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ثبت زنجیره انجام نشد: " + ex.Message);
            }
        }

        private void EditChain()
        {
            if (!RequireAdmin()) return;

            int chainId = SelectedId(_gridChains);
            if (chainId <= 0) { UiTheme.ShowWarning(this, "یک زنجیره را انتخاب کنید."); return; }

            ApprovalChainModel chain = ApprovalService.GetChain(chainId);
            if (chain == null) return;

            Dictionary<string, string> values = EntPrompt.Edit(this, "ویرایش زنجیره",
                EntField.Text("Name", "نام", chain.Name),
                EntField.Check("IsActive", "فعال", chain.IsActive));

            if (values == null) return;

            EntDb.Exec("UPDATE EntApprovalChain SET Name = @Name, IsActive = @Active WHERE ChainID = @Id;",
                "@Name",   values["Name"],
                "@Active", values["IsActive"] == "1" ? 1 : 0,
                "@Id",     chainId);

            LoadChains();
        }

        private void DeleteChain()
        {
            if (!RequireAdmin()) return;

            int chainId = SelectedId(_gridChains);
            if (chainId <= 0) { UiTheme.ShowWarning(this, "یک زنجیره را انتخاب کنید."); return; }

            long openRequests = EntDb.ToInt64(EntDb.Scalar(
                "SELECT COUNT(*) FROM EntApprovalRequest WHERE ChainID = @Id AND Status = @Status;",
                "@Id", chainId, "@Status", ApprovalService.StatusPending));

            if (openRequests > 0)
            {
                UiTheme.ShowWarning(this, "این زنجیره " + openRequests + " درخواست باز دارد و حذف نمی‌شود.");
                return;
            }

            if (!UiTheme.ShowConfirm(this, "زنجیره و سطوح آن حذف شوند؟", "حذف زنجیره"))
                return;

            EntDb.Exec("DELETE FROM EntApprovalLevel WHERE ChainID = @Id;", "@Id", chainId);
            EntDb.Exec("DELETE FROM EntApprovalChain WHERE ChainID = @Id;", "@Id", chainId);

            LoadChains();
        }

        // ─── عملیات سطح ────────────────────────────────────────────────────
        private void AddLevel()
        {
            if (!RequireAdmin()) return;

            int chainId = SelectedId(_gridChains);
            if (chainId <= 0) { UiTheme.ShowWarning(this, "اول یک زنجیره را انتخاب کنید."); return; }

            int nextNo = EntDb.ToInt(EntDb.Scalar(
                "SELECT IFNULL(MAX(LevelNo), 0) + 1 FROM EntApprovalLevel WHERE ChainID = @Id;",
                "@Id", chainId));

            Dictionary<string, string> values = EntPrompt.Edit(this, "سطح تأیید جدید",
                EntField.Number("LevelNo", "شماره سطح", nextNo.ToString()),
                EntField.Text("Name", "نام سطح", ""),
                EntField.Combo("Role", "نقش تأییدکننده", "", RoleItems()),
                EntField.Number("UserID", "شناسه کاربر (اختیاری)", "0"),
                EntField.Text("Perm", "مجوز لازم (اختیاری)", ""));

            if (values == null) return;

            if (values["Name"].Length == 0)
            {
                UiTheme.ShowWarning(this, "نام سطح الزامی است.");
                return;
            }

            try
            {
                EntDb.Exec(@"
INSERT INTO EntApprovalLevel
    (ChainID, LevelNo, Name, ApproverRole, ApproverUserID, RequiredPermission)
VALUES
    (@Chain, @No, @Name, @Role, @UserID, @Perm);",
                    "@Chain",  chainId,
                    "@No",     EntDb.ToInt(values["LevelNo"]),
                    "@Name",   values["Name"],
                    "@Role",   values["Role"],
                    "@UserID", EntDb.ToInt(values["UserID"]),
                    "@Perm",   values["Perm"]);

                LoadLevels();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ثبت سطح انجام نشد (شماره سطح تکراری؟): " + ex.Message);
            }
        }

        private void EditLevel()
        {
            if (!RequireAdmin()) return;

            int levelId = SelectedId(_gridLevels);
            if (levelId <= 0) { UiTheme.ShowWarning(this, "یک سطح را انتخاب کنید."); return; }

            DataTable table = EntDb.Query(@"
SELECT LevelID, ChainID, LevelNo, Name, ApproverRole, ApproverUserID, RequiredPermission
FROM   EntApprovalLevel WHERE LevelID = @Id;", "@Id", levelId);

            if (table.Rows.Count == 0) return;
            DataRow row = table.Rows[0];

            Dictionary<string, string> values = EntPrompt.Edit(this, "ویرایش سطح",
                EntField.Number("LevelNo", "شماره سطح", EntDb.ToText(row["LevelNo"])),
                EntField.Text("Name", "نام سطح", EntDb.ToText(row["Name"])),
                EntField.Combo("Role", "نقش تأییدکننده", EntDb.ToText(row["ApproverRole"]), RoleItems()),
                EntField.Number("UserID", "شناسه کاربر (اختیاری)", EntDb.ToInt(row["ApproverUserID"]).ToString()),
                EntField.Text("Perm", "مجوز لازم (اختیاری)", EntDb.ToText(row["RequiredPermission"])));

            if (values == null) return;

            try
            {
                EntDb.Exec(@"
UPDATE EntApprovalLevel
SET    LevelNo = @No, Name = @Name, ApproverRole = @Role,
       ApproverUserID = @UserID, RequiredPermission = @Perm
WHERE  LevelID = @Id;",
                    "@No",     EntDb.ToInt(values["LevelNo"]),
                    "@Name",   values["Name"],
                    "@Role",   values["Role"],
                    "@UserID", EntDb.ToInt(values["UserID"]),
                    "@Perm",   values["Perm"],
                    "@Id",     levelId);

                LoadLevels();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ویرایش سطح انجام نشد (شماره سطح تکراری؟): " + ex.Message);
            }
        }

        private void DeleteLevel()
        {
            if (!RequireAdmin()) return;

            int levelId = SelectedId(_gridLevels);
            if (levelId <= 0) { UiTheme.ShowWarning(this, "یک سطح را انتخاب کنید."); return; }

            if (!UiTheme.ShowConfirm(this, "سطح انتخاب‌شده حذف شود؟", "حذف سطح"))
                return;

            EntDb.Exec("DELETE FROM EntApprovalLevel WHERE LevelID = @Id;", "@Id", levelId);
            LoadLevels();
        }

        // ─── کمکی‌های رابط ─────────────────────────────────────────────────
        private static List<KeyValuePair<string, string>> RoleItems()
        {
            // همان نقش‌های موجود در FrmUsers — بدون افزودن نقش جدید.
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("",           "— بدون نقش مشخص —"),
                new KeyValuePair<string, string>("SuperAdmin", "مدیر کل"),
                new KeyValuePair<string, string>("Admin",      "مدیر سیستم"),
                new KeyValuePair<string, string>("Operator",   "کاربر عملیاتی"),
                new KeyValuePair<string, string>("Viewer",     "ناظر")
            };
        }

        private static TabPage NewPage(string title)
        {
            return new TabPage(title)
            {
                BackColor   = UiTheme.Background,
                RightToLeft = RightToLeft.Yes,
                Padding     = new Padding(10)
            };
        }

        private static Label SectionLabel(string text)
        {
            return new Label
            {
                Dock      = DockStyle.Top,
                Height    = 28,
                Text      = "  " + text,
                Font      = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            };
        }

        private static FlowLayoutPanel NewButtonBar()
        {
            return new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft   = RightToLeft.Yes,
                WrapContents  = false
            };
        }

        private Panel GridToolbar(string title, Action add, Action edit, Action remove)
        {
            Panel bar = new Panel { Dock = DockStyle.Top, Height = 78, Padding = new Padding(6, 4, 6, 4) };
            bar.Controls.Add(SectionLabel(title));

            FlowLayoutPanel buttons = NewButtonBar();
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

        // ستون‌های فنی (شناسه/ChainID/LevelNo) از دید کاربر پنهان می‌شوند اما
        // در DataSource باقی می‌مانند چون کد به آن‌ها نیاز دارد.
        private static void HideHelperColumns(DataGridView grid)
        {
            string[] hidden = { "شناسه", "ChainID", "LevelNo" };

            foreach (string name in hidden)
                if (grid.Columns.Contains(name))
                    grid.Columns[name].Visible = false;
        }

        private static int SelectedId(DataGridView grid)
        {
            if (grid == null || grid.CurrentRow == null) return 0;
            if (!grid.Columns.Contains("شناسه")) return 0;
            return EntDb.ToInt(grid.CurrentRow.Cells["شناسه"].Value);
        }

        private bool RequireAdmin()
        {
            if (SecurityContext.IsAdmin()) return true;

            UiTheme.ShowWarning(this, "تعریف زنجیره تأیید فقط برای مدیر سیستم مجاز است.");
            return false;
        }
    }
}

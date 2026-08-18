using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «قواعد سازمانی» — تعریف قواعد داده‌محور و مشاهده تاریخچه اجرای آن‌ها.
    //
    // تعریف قاعده فقط برای مدیر سیستم مجاز است. قاعده‌ای که «جلوگیری» باشد
    // می‌تواند ثبت را متوقف کند، پس هنگام ساخت به کاربر تذکر داده می‌شود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmRules : Form
    {
        private DataGridView _gridRules, _gridLog;

        public FrmRules()
        {
            BuildUi();
            LoadRules();
        }

        private void BuildUi()
        {
            Text              = "قواعد سازمانی";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1180, 700);

            _gridLog = CreateGrid();
            Panel logPanel = new Panel { Dock = DockStyle.Bottom, Height = 210, BackColor = UiTheme.CardBack };
            logPanel.Controls.Add(_gridLog);
            logPanel.Controls.Add(new Label
            {
                Dock      = DockStyle.Top,
                Height    = 28,
                Text      = "  تاریخچه اجرای قاعده انتخاب‌شده",
                Font      = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            });

            _gridRules = CreateGrid();
            _gridRules.SelectionChanged += delegate { LoadLog(); };

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBack, Padding = new Padding(0) };
            main.Controls.Add(_gridRules);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft   = RightToLeft.Yes,
                WrapContents  = false
            };
            buttons.Controls.Add(MakeButton("قاعده جدید", "＋", UiTheme.Primary,      AddRule));
            buttons.Controls.Add(MakeButton("ویرایش",     "✎", UiTheme.PrimaryLight, EditRule));
            buttons.Controls.Add(MakeButton("فعال/غیرفعال", "⏻", UiTheme.Warning,    ToggleRule));
            buttons.Controls.Add(MakeButton("حذف",        "🗑", UiTheme.Danger,      DeleteRule));
            buttons.Controls.Add(MakeButton("آزمایش",     "▶", UiTheme.Success,      TestRule));
            buttons.Controls.Add(MakeButton("تازه‌سازی",  "⟳", UiTheme.PrimaryLight, delegate { LoadRules(); }));
            toolbar.Controls.Add(buttons);
            main.Controls.Add(toolbar);

            Controls.Add(main);
            Controls.Add(logPanel);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.PrimaryDark };
            header.Controls.Add(new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(0xCF, 0xDD, 0xEE),
                Font      = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopLeft,
                Padding   = new Padding(0, 0, 20, 0),
                Text      = "قواعد بدون تغییر کد اجرا می‌شوند. قاعده معیوب نادیده گرفته می‌شود و کار کاربر متوقف نمی‌گردد."
            });
            header.Controls.Add(new Label
            {
                Text      = "قواعد سازمانی",
                Dock      = DockStyle.Top,
                Height    = 38,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            });
            Controls.Add(header);
        }

        // ─── بارگذاری ──────────────────────────────────────────────────────
        private void LoadRules()
        {
            _gridRules.DataSource = EntDb.Query(@"
SELECT RuleID     AS 'شناسه',
       Name       AS 'نام قاعده',
       Code       AS 'کد',
       EntityName AS 'موجودیت',
       EventName  AS 'رویداد',
       COALESCE(ConditionField, '') || ' ' || COALESCE(Operator, '') || ' ' ||
       COALESCE(ConditionValue, '')  AS 'شرط',
       ActionType AS 'کار',
       COALESCE(ActionParam, '')     AS 'پارامتر',
       Priority   AS 'ترتیب',
       CASE IsActive WHEN 1 THEN 'فعال' ELSE 'غیرفعال' END AS 'وضعیت'
FROM   EntRule
ORDER  BY EntityName, EventName, Priority, RuleID;");

            HideIdColumn(_gridRules);
            LoadLog();
        }

        private void LoadLog()
        {
            int ruleId = SelectedId(_gridRules);

            _gridLog.DataSource = ruleId <= 0 ? null : EntDb.Query(@"
SELECT RunAt      AS 'تاریخ اجرا',
       EntityName AS 'موجودیت',
       EntityID   AS 'شناسه رکورد',
       EventName  AS 'رویداد',
       CASE Matched WHEN 1 THEN 'بله' ELSE 'خیر' END AS 'برقرار شد',
       Outcome    AS 'نتیجه',
       RunBy      AS 'کاربر'
FROM   EntRuleLog
WHERE  RuleID = @Id
ORDER  BY LogID DESC
LIMIT  300;", "@Id", ruleId);
        }

        // ─── عملیات ────────────────────────────────────────────────────────
        private void AddRule()
        {
            if (!RequireAdmin()) return;

            Dictionary<string, string> values = EditDialog("قاعده جدید", null);
            if (values == null) return;

            if (values["Code"].Length == 0 || values["Name"].Length == 0)
            {
                UiTheme.ShowWarning(this, "کد و نام الزامی است.");
                return;
            }

            try
            {
                EntDb.Exec(@"
INSERT INTO EntRule
    (Code, Name, EntityName, EventName, ConditionField, Operator, ConditionValue,
     ActionType, ActionParam, Message, Priority, StopOnMatch, IsActive, CenterID, CreatedBy)
VALUES
    (@Code, @Name, @Entity, @Event, @Field, @Op, @Value,
     @Action, @Param, @Message, @Priority, @Stop, @Active, @Center, @By);",
                    "@Code",     values["Code"],
                    "@Name",     values["Name"],
                    "@Entity",   values["Entity"],
                    "@Event",    values["Event"],
                    "@Field",    values["Field"],
                    "@Op",       values["Operator"],
                    "@Value",    values["Value"],
                    "@Action",   values["Action"],
                    "@Param",    values["Param"],
                    "@Message",  values["Message"],
                    "@Priority", EntDb.ToInt(values["Priority"]),
                    "@Stop",     values["Stop"]   == "1" ? 1 : 0,
                    "@Active",   values["Active"] == "1" ? 1 : 0,
                    "@Center",   SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : null,
                    "@By",       SecurityContext.Username);

                LoadRules();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ثبت قاعده انجام نشد (کد تکراری؟): " + ex.Message);
            }
        }

        private void EditRule()
        {
            if (!RequireAdmin()) return;

            int ruleId = SelectedId(_gridRules);
            if (ruleId <= 0) { UiTheme.ShowWarning(this, "یک قاعده را انتخاب کنید."); return; }

            DataTable table = EntDb.Query("SELECT * FROM EntRule WHERE RuleID = @Id;", "@Id", ruleId);
            if (table.Rows.Count == 0) return;

            Dictionary<string, string> values = EditDialog("ویرایش قاعده", table.Rows[0]);
            if (values == null) return;

            EntDb.Exec(@"
UPDATE EntRule
SET    Name = @Name, EntityName = @Entity, EventName = @Event,
       ConditionField = @Field, Operator = @Op, ConditionValue = @Value,
       ActionType = @Action, ActionParam = @Param, Message = @Message,
       Priority = @Priority, StopOnMatch = @Stop, IsActive = @Active
WHERE  RuleID = @Id;",
                "@Name",     values["Name"],
                "@Entity",   values["Entity"],
                "@Event",    values["Event"],
                "@Field",    values["Field"],
                "@Op",       values["Operator"],
                "@Value",    values["Value"],
                "@Action",   values["Action"],
                "@Param",    values["Param"],
                "@Message",  values["Message"],
                "@Priority", EntDb.ToInt(values["Priority"]),
                "@Stop",     values["Stop"]   == "1" ? 1 : 0,
                "@Active",   values["Active"] == "1" ? 1 : 0,
                "@Id",       ruleId);

            LoadRules();
        }

        private void ToggleRule()
        {
            if (!RequireAdmin()) return;

            int ruleId = SelectedId(_gridRules);
            if (ruleId <= 0) { UiTheme.ShowWarning(this, "یک قاعده را انتخاب کنید."); return; }

            EntDb.Exec("UPDATE EntRule SET IsActive = 1 - IsActive WHERE RuleID = @Id;", "@Id", ruleId);
            LoadRules();
        }

        private void DeleteRule()
        {
            if (!RequireAdmin()) return;

            int ruleId = SelectedId(_gridRules);
            if (ruleId <= 0) { UiTheme.ShowWarning(this, "یک قاعده را انتخاب کنید."); return; }

            if (!UiTheme.ShowConfirm(this, "قاعده انتخاب‌شده و تاریخچه اجرای آن حذف شود؟", "حذف قاعده"))
                return;

            EntDb.Exec("DELETE FROM EntRuleLog WHERE RuleID = @Id;", "@Id", ruleId);
            EntDb.Exec("DELETE FROM EntRule WHERE RuleID = @Id;", "@Id", ruleId);

            LoadRules();
        }

        // اجرای آزمایشی قواعد یک رویداد روی یک رکورد واقعی — بدون تغییر داده
        // آن رکورد (فقط ممکن است وظیفه/رویداد ثبت شود، مطابق تعریف خود قاعده).
        private void TestRule()
        {
            int ruleId = SelectedId(_gridRules);
            if (ruleId <= 0) { UiTheme.ShowWarning(this, "یک قاعده را انتخاب کنید."); return; }

            DataTable table = EntDb.Query(
                "SELECT EntityName, EventName FROM EntRule WHERE RuleID = @Id;", "@Id", ruleId);

            if (table.Rows.Count == 0) return;

            string entity = EntDb.ToText(table.Rows[0]["EntityName"]);
            string eventName = EntDb.ToText(table.Rows[0]["EventName"]);

            string idText = EntPrompt.AskText(this, "آزمایش قاعده",
                "شناسه رکورد " + entity, "");

            if (idText == null) return;

            int entityId = EntDb.ToInt(idText);
            if (entityId <= 0) { UiTheme.ShowWarning(this, "شناسه معتبر وارد کنید."); return; }

            RuleRunResult result = RuleEngine.Run(entity, eventName, entityId);

            string summary = "قواعد فعال‌شده: " + result.Matched +
                             (result.Blocked ? Environment.NewLine + "نتیجه: عملیات متوقف می‌شد." : "");

            if (result.HasMessages)
                summary += Environment.NewLine + Environment.NewLine + result.MessageText;

            UiTheme.ShowSuccess(this, summary);
            LoadLog();
        }

        // ─── دیالوگ ویرایش ─────────────────────────────────────────────────
        private Dictionary<string, string> EditDialog(string title, DataRow existing)
        {
            string entity = existing == null
                ? EnterpriseInitializer.EntityCase
                : EntDb.ToText(existing["EntityName"]);

            List<KeyValuePair<string, string>> entityItems = new List<KeyValuePair<string, string>>();
            foreach (string name in RuleEngine.SupportedEntities())
                entityItems.Add(new KeyValuePair<string, string>(name, name));

            List<EntField> fields = new List<EntField>();

            if (existing == null)
                fields.Add(EntField.Text("Code", "کد یکتا", ""));

            fields.Add(EntField.Text("Name", "نام قاعده", existing == null ? "" : EntDb.ToText(existing["Name"])));
            fields.Add(EntField.Combo("Entity", "موجودیت", entity, entityItems));
            fields.Add(EntField.Combo("Event", "رویداد",
                existing == null ? RuleEngine.EventAfterSave : EntDb.ToText(existing["EventName"]),
                RuleEngine.EventItems()));
            fields.Add(EntField.Combo("Field", "فیلد شرط",
                existing == null ? "" : EntDb.ToText(existing["ConditionField"]),
                RuleEngine.FieldItems(entity)));
            fields.Add(EntField.Combo("Operator", "عملگر",
                existing == null ? RuleEngine.OpEquals : EntDb.ToText(existing["Operator"]),
                RuleEngine.OperatorItems()));
            fields.Add(EntField.Text("Value", "مقدار",
                existing == null ? "" : EntDb.ToText(existing["ConditionValue"])));
            fields.Add(EntField.Combo("Action", "کار",
                existing == null ? RuleEngine.ActionWarn : EntDb.ToText(existing["ActionType"]),
                RuleEngine.ActionItems()));
            fields.Add(EntField.Text("Param", "پارامتر کار (نقش برای وظیفه)",
                existing == null ? "" : EntDb.ToText(existing["ActionParam"])));
            fields.Add(EntField.Multiline("Message", "پیام",
                existing == null ? "" : EntDb.ToText(existing["Message"])));
            fields.Add(EntField.Number("Priority", "ترتیب اجرا",
                existing == null ? "0" : EntDb.ToInt(existing["Priority"]).ToString()));
            fields.Add(EntField.Check("Stop", "توقف پس از برقراری",
                existing != null && EntDb.ToBool(existing["StopOnMatch"])));
            fields.Add(EntField.Check("Active", "فعال",
                existing == null || EntDb.ToBool(existing["IsActive"])));

            Dictionary<string, string> values = EntPrompt.Edit(this, title, fields.ToArray());

            if (values != null && !values.ContainsKey("Code"))
                values["Code"] = "";

            return values;
        }

        // ─── کمکی‌ها ───────────────────────────────────────────────────────
        private static Button MakeButton(string text, string icon, Color color, Action action)
        {
            Button button = UiTheme.CreateButton(text, icon, color);
            button.Width  = 124;
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

        private static void HideIdColumn(DataGridView grid)
        {
            if (grid != null && grid.Columns.Contains("شناسه"))
                grid.Columns["شناسه"].Visible = false;
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

            UiTheme.ShowWarning(this, "تعریف قواعد فقط برای مدیر سیستم مجاز است.");
            return false;
        }
    }
}

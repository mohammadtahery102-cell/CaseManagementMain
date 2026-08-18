using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ═════════════════════════════════════════════════════════════════════════
    // «گردش‌کار رکورد» — پنجره کوچکی که مرحله جاری یک رکورد، گذارهای مجاز و
    // تاریخچه آن را نشان می‌دهد و اجازه اجرای گذار می‌دهد.
    //
    // این پنجره عمومی است: با (EntityName, EntityID) کار می‌کند و هیچ وابستگی
    // به فرم پرونده ندارد؛ بنابراین هر فرم دیگری هم می‌تواند آن را باز کند.
    // اگر برای آن موجودیت گردش‌کار فعالی تعریف نشده باشد، پیام مناسب می‌دهد و
    // هیچ تغییری در داده ایجاد نمی‌کند.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmWorkflowAction : Form
    {
        private readonly string _entityName;
        private readonly int    _entityId;
        private readonly string _entityTitle;

        private WorkflowInstanceModel _instance;

        private Label           _lblState;
        private FlowLayoutPanel _actions;
        private DataGridView    _gridHistory;
        private TextBox         _txtNote;

        // true اگر در این پنجره مرحله رکورد واقعاً تغییر کرده باشد (فرم فراخوان
        // می‌تواند بر اساس آن نمایش خودش را تازه کند).
        public bool Changed { get; private set; }

        public FrmWorkflowAction(string entityName, int entityId, string entityTitle)
        {
            _entityName  = entityName;
            _entityId    = entityId;
            _entityTitle = entityTitle ?? "";

            BuildUi();
            LoadState();
        }

        private void BuildUi()
        {
            Text              = "گردش‌کار رکورد";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeFixedSize(this, 720, 560);

            // ── تاریخچه ──
            _gridHistory = new DataGridView
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
            UiTheme.StyleGrid(_gridHistory);

            Panel historyWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 8) };
            historyWrap.Controls.Add(_gridHistory);
            historyWrap.Controls.Add(new Label
            {
                Dock      = DockStyle.Top,
                Height    = 26,
                Text      = "تاریخچه",
                Font      = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            });
            Controls.Add(historyWrap);

            // ── گذارهای مجاز + یادداشت ──
            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 150, Padding = new Padding(12, 8, 12, 12) };

            _actions = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                RightToLeft   = RightToLeft.Yes,
                WrapContents  = true,
                AutoScroll    = true
            };

            _txtNote = new TextBox
            {
                Dock        = DockStyle.Top,
                Height      = 26,
                RightToLeft = RightToLeft.Yes
            };
            UiTheme.StyleTextBox(_txtNote);

            bottom.Controls.Add(_actions);
            bottom.Controls.Add(_txtNote);
            bottom.Controls.Add(new Label
            {
                Dock      = DockStyle.Top,
                Height    = 24,
                Text      = "یادداشت (اختیاری)",
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            });
            Controls.Add(bottom);

            // ── سربرگ ──
            Panel header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = UiTheme.PrimaryDark };

            _lblState = new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeMedium),
                TextAlign = ContentAlignment.TopLeft,
                Padding   = new Padding(0, 4, 20, 0)
            };

            header.Controls.Add(_lblState);
            header.Controls.Add(new Label
            {
                Text      = string.IsNullOrWhiteSpace(_entityTitle) ? "گردش‌کار رکورد" : _entityTitle,
                Dock      = DockStyle.Top,
                Height    = 36,
                ForeColor = Color.White,
                Font      = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(0, 6, 20, 0)
            });
            Controls.Add(header);
        }

        private void LoadState()
        {
            _instance = WorkflowService.EnsureInstance(
                _entityName, _entityId, SecurityContext.CurrentCenterId);

            _actions.Controls.Clear();

            if (_instance == null)
            {
                _lblState.Text        = "برای این موجودیت گردش‌کار فعالی تعریف نشده است.";
                _gridHistory.DataSource = null;
                return;
            }

            _lblState.Text = "مرحله جاری: " + _instance.CurrentStateName +
                             "     |     وضعیت: " + _instance.Status;

            _gridHistory.DataSource = WorkflowService.GetHistory(_instance.InstanceID);

            if (_instance.IsFinal)
            {
                _actions.Controls.Add(new Label
                {
                    Text      = "گردش‌کار این رکورد پایان یافته است.",
                    AutoSize  = true,
                    ForeColor = UiTheme.TextMuted,
                    Padding   = new Padding(6)
                });
                return;
            }

            List<WorkflowTransitionModel> transitions = WorkflowService.GetAvailableTransitions(_instance);

            if (transitions.Count == 0)
            {
                _actions.Controls.Add(new Label
                {
                    Text      = "گذار مجازی از مرحله فعلی برای شما تعریف نشده است.",
                    AutoSize  = true,
                    ForeColor = UiTheme.TextMuted,
                    Padding   = new Padding(6)
                });
                return;
            }

            foreach (WorkflowTransitionModel transition in transitions)
            {
                WorkflowTransitionModel captured = transition;

                Button button = UiTheme.CreateButton(
                    transition.Name,
                    transition.RequiresApproval ? "🛡" : "➜",
                    transition.RequiresApproval ? UiTheme.Warning : UiTheme.Primary);

                button.Width  = 170;
                button.Margin = new Padding(4);
                button.Click += delegate { Apply(captured); };

                _actions.Controls.Add(button);
            }
        }

        private void Apply(WorkflowTransitionModel transition)
        {
            if (!UiTheme.ShowConfirm(this, "«" + transition.Name + "» اجرا شود؟", "اجرای گذار"))
                return;

            try
            {
                WorkflowActionResult result = WorkflowService.ApplyTransition(
                    _instance, transition.TransitionID, _txtNote.Text);

                if (result.Applied || result.PendingApproval)
                {
                    Changed = Changed || result.Applied;
                    _txtNote.Clear();
                    UiTheme.ShowSuccess(this, result.Message);
                }
                else
                {
                    UiTheme.ShowWarning(this, result.Message);
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "اجرای گذار انجام نشد: " + ex.Message);
            }

            LoadState();
        }
    }
}

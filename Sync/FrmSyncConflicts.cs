using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // بازبینی تعارض‌ها — فاز ۴.
    //
    // آموزش — چرا این فرم عمداً ساده است: الگوی بازبینیِ برنامه از قبل در
    // FrmSyncWizard تثبیت شده — یک فهرست بالا، جزئیاتِ ردیفِ انتخاب‌شده پایین،
    // و دکمه‌های تصمیم در یک نوار. همان ساختار اینجا تکرار می‌شود تا کاربری
    // که با Wizard کار کرده، بدون آموزش تازه بتواند استفاده کند. هیچ کنترل
    // سفارشی و هیچ تمِ جدیدی ساخته نشده است؛ همه‌چیز از UiTheme می‌آید.
    //
    // ⚠ هیچ دکمه‌ای بدون تأیید صریح چیزی را تغییر نمی‌دهد، و هر تصمیم در
    // لاگ ممیزی و تاریخچهٔ نسخه‌ها ثبت می‌شود.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmSyncConflicts : Form
    {
        private DataGridView _gridConflicts;
        private DataGridView _gridFields;
        private Label _lblSummary;
        private Button _btnLocal, _btnRemote, _btnManual, _btnAuto;

        private DataTable _conflicts;
        private SyncConflictAnalyzer.ConflictAnalysis _current;

        public FrmSyncConflicts()
        {
            Text = "بازبینی تعارض‌های همگام‌سازی";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeSmall);
            UiTheme.MakeMainWindow(this, 1100, 680);

            BuildUi();
            LoadConflicts();
        }

        private void BuildUi()
        {
            _lblSummary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                Text = "",
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(10, 0, 10, 0)
            };

            _gridFields = MakeGrid();
            _gridFields.Dock = DockStyle.Fill;

            Panel fieldsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
            fieldsPanel.Controls.Add(_gridFields);

            _gridConflicts = MakeGrid();
            _gridConflicts.Dock = DockStyle.Top;
            _gridConflicts.Height = 220;
            _gridConflicts.SelectionChanged += delegate { ShowSelected(); };

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(10, 8, 10, 6)
            };

            _btnAuto   = MakeButton("تلاش برای ادغام خودکار", delegate { DoAuto(); });
            _btnLocal  = MakeButton("پذیرش نسخهٔ محلی",       delegate { DoAcceptLocal(); });
            _btnRemote = MakeButton("پذیرش نسخهٔ سرور",        delegate { DoAcceptRemote(); });
            _btnManual = MakeButton("ادغام دستی…",            delegate { DoManual(); });

            flow.Controls.Add(_btnAuto);
            flow.Controls.Add(_btnLocal);
            flow.Controls.Add(_btnRemote);
            flow.Controls.Add(_btnManual);
            flow.Controls.Add(MakeButton("تازه‌سازی", delegate { LoadConflicts(); }));

            toolbar.Controls.Add(flow);

            Controls.Add(fieldsPanel);
            Controls.Add(_gridConflicts);
            Controls.Add(toolbar);
            Controls.Add(_lblSummary);
        }

        // ─────────────────────────────────────────────────────────────────────
        private void LoadConflicts()
        {
            try
            {
                _conflicts = SyncConflictStore.GetOpen(500);

                DataTable view = new DataTable();
                view.Columns.Add("شناسه");
                view.Columns.Add("رکورد");
                view.Columns.Add("نوع تعارض");
                view.Columns.Add("نسخهٔ محلی");
                view.Columns.Add("نسخهٔ سرور");
                view.Columns.Add("تشخیص توسط");
                view.Columns.Add("تاریخ");
                view.Columns.Add("وضعیت");

                foreach (DataRow row in _conflicts.Rows)
                    view.Rows.Add(
                        row["ConflictID"],
                        Convert.ToString(row["EntityName"]) + " — " +
                            Shorten(Convert.ToString(row["EntityGlobalID"])),
                        row["ConflictType"],
                        row["LocalVersion"],
                        row["RemoteVersion"],
                        row["DetectedBy"],
                        row["DetectedAt"],
                        row["Status"]);

                _gridConflicts.DataSource = view;
                _lblSummary.Text = "تعارض‌های باز: " + view.Rows.Count.ToString("N0");

                ShowSelected();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ShowSelected()
        {
            try
            {
                _current = null;
                _gridFields.DataSource = null;

                DataRow conflict = SelectedConflict();
                if (conflict == null) { UpdateButtons(); return; }

                _current = SyncConflictAnalyzer.Analyze(conflict);

                DataTable table = new DataTable();
                table.Columns.Add("فیلد");
                table.Columns.Add("مقدار محلی");
                table.Columns.Add("مقدار سرور");
                table.Columns.Add("نتیجه");

                foreach (SyncConflictAnalyzer.FieldDecision field in _current.Fields)
                {
                    // فیلدهای یکسان نمایش داده نمی‌شوند تا تفاوت‌ها گم نشوند.
                    if (field.Outcome == SyncConflictAnalyzer.FieldOutcome.Same) continue;
                    table.Rows.Add(field.Field, field.LocalValue, field.RemoteValue, field.OutcomeText);
                }

                _gridFields.DataSource = table;
                UpdateButtons();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void UpdateButtons()
        {
            bool has = _current != null;

            _btnAuto.Enabled   = has && _current.CanAutoMerge;
            _btnLocal.Enabled  = has && !_current.IsDuplicateCode;
            _btnRemote.Enabled = has && !_current.IsDuplicateCode;
            _btnManual.Enabled = has && !_current.IsDuplicateCode && _current.Contested.Count > 0;

            if (has && _current.IsDuplicateCode)
                _lblSummary.Text = "کد اختصاصی تکراری — این تعارض با ادغام حل نمی‌شود؛ " +
                                   "یکی از دو پرونده باید کد دیگری بگیرد.";
        }

        // ─────────────────────────────────────────────────────────────────────
        private void DoAuto()        { Run(delegate { return SyncConflictResolver.TryAutoResolve(_current.ConflictId); }, "ادغام خودکار"); }
        private void DoAcceptLocal() { Run(delegate { return SyncConflictResolver.AcceptLocal(_current.ConflictId); },  "پذیرش نسخهٔ محلی"); }
        private void DoAcceptRemote(){ Run(delegate { return SyncConflictResolver.AcceptRemote(_current.ConflictId); }, "پذیرش نسخهٔ سرور"); }

        private void DoManual()
        {
            if (_current == null) return;

            var chosen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // برای هر فیلدِ متعارض، یک انتخاب صریح از مدیر گرفته می‌شود.
            foreach (SyncConflictAnalyzer.FieldDecision field in _current.Contested)
            {
                DialogResult answer = Msg.Show(
                    "فیلد: " + field.Field + Environment.NewLine + Environment.NewLine +
                    "مقدار محلی: " + field.LocalValue + Environment.NewLine +
                    "مقدار سرور: " + field.RemoteValue + Environment.NewLine + Environment.NewLine +
                    "«بله» = نگه‌داشتن مقدار محلی، «خیر» = پذیرش مقدار سرور، «انصراف» = توقف",
                    "ادغام دستی",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (answer == DialogResult.Cancel) return;

                chosen[field.Field] = answer == DialogResult.Yes ? field.LocalValue : field.RemoteValue;
            }

            Run(delegate { return SyncConflictResolver.ResolveManual(_current.ConflictId, chosen); },
                "ادغام دستی");
        }

        private void Run(Func<SyncConflictResolver.ResolutionResult> action, string title)
        {
            if (_current == null) return;

            if (!UiTheme.ShowConfirm(this, title + " انجام شود؟", "تأیید حل تعارض"))
                return;

            try
            {
                Cursor = Cursors.WaitCursor;

                SyncConflictResolver.ResolutionResult result = action();

                if (result.Succeeded) UiTheme.ShowSuccess(this, result.Message);
                else UiTheme.ShowWarning(this, result.Message);

                LoadConflicts();
            }
            catch (Exception ex) { ShowError(ex); }
            finally { Cursor = Cursors.Default; }
        }

        // ─────────────────────────────────────────────────────────────────────
        private DataRow SelectedConflict()
        {
            if (_conflicts == null || _gridConflicts.CurrentRow == null) return null;

            int index = _gridConflicts.CurrentRow.Index;
            if (index < 0 || index >= _conflicts.Rows.Count) return null;

            return _conflicts.Rows[index];
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= 12) return text;
            return text.Substring(0, 8) + "…";
        }

        private DataGridView MakeGrid()
        {
            DataGridView grid = new DataGridView
            {
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(grid);
            return grid;
        }

        private Button MakeButton(string text, EventHandler onClick)
        {
            Button button = UiTheme.CreateSecondaryButton(text, "");
            button.Size = new Size(Math.Max(140, TextRenderer.MeasureText(text, button.Font).Width + 26), 30);
            button.Margin = new Padding(4, 0, 4, 0);
            button.Click += onClick;
            return button;
        }

        private void ShowError(Exception ex)
        {
            try { Enterprise.ErrorLogger.Log(ex, "FrmSyncConflicts"); } catch { }
            UiTheme.ShowError(this, "خطا: " + ex.Message);
        }
    }
}

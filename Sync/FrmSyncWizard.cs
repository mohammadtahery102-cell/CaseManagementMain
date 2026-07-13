using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ─────────────────────────────────────────────────────────────────────────
    // FrmSyncWizard — رابط کاربری ۸ مرحله‌ای موتور همگام‌سازی HTML.
    // مرحله‌ها: ۱ انتخاب فایل · ۲ تحلیل · ۳ اعتبارسنجی · ۴ مقایسه · ۵ پیش‌نمایش
    //          · ۶ تأیید · ۷ همگام‌سازی · ۸ گزارش.
    // هیچ داده‌ای پیش از مرحله ۷ نوشته نمی‌شود؛ قبل از نوشتن بکاپ کامل گرفته
    // می‌شود و کل عملیات اتمیک (Transaction/Rollback) است.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class FrmSyncWizard : Form
    {
        private readonly string[] _stepTitles =
        {
            "۱ انتخاب فایل", "۲ تحلیل", "۳ اعتبارسنجی", "۴ مقایسه",
            "۵ پیش‌نمایش", "۶ تأیید", "۷ همگام‌سازی", "۸ گزارش"
        };

        private int _step;
        private readonly Panel[] _pages = new Panel[8];

        private Label _lblStepHeader;
        private Button _btnBack, _btnNext, _btnCancel;
        private ProgressBar _progress;
        private Label _lblProgress;

        // داده‌های جریان کار
        private readonly SyncSource _source = new SyncSource();
        private ParsedSyncData _parsed;
        private SyncPlan _plan;
        private SyncReport _report;
        private bool _takeBackup = true;

        // کنترل‌های صفحه‌ها
        private TextBox _txtGuardians, _txtMembers;
        private CheckBox _chkBackup;
        private Label _lblParseSummary, _lblCompareSummary, _lblConfirmSummary;
        private ListBox _lstValidation;
        private DataGridView _grdPreview;
        private Label _lblPreviewInfo;
        private TextBox _txtReport;

        private List<SyncRecord> _previewRecords = new List<SyncRecord>();

        public FrmSyncWizard()
        {
            BuildUi();
            ShowStep(0);
        }

        private void BuildUi()
        {
            Text = "همگام‌سازی اطلاعات از سامانه مرکزی (HTML)";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeFixedSize(this, 940, 660);

            // ── هدر مرحله ──
            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = UiTheme.PrimaryDark };
            _lblStepHeader = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(20, 0, 20, 0)
            };
            header.Controls.Add(_lblStepHeader);
            Controls.Add(header);

            // ── نوار پایین: پیشرفت + دکمه‌ها ──
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 96, BackColor = UiTheme.CardBack };

            _progress = new ProgressBar { Dock = DockStyle.Top, Height = 18, Visible = false };
            _lblProgress = new Label
            {
                Dock = DockStyle.Top, Height = 20, ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(20, 0, 20, 0), Visible = false
            };

            FlowLayoutPanel navFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(20, 10, 20, 10)
            };
            _btnCancel = UiTheme.CreateSecondaryButton("انصراف", "✕");
            _btnCancel.Size = new Size(120, 36); _btnCancel.Margin = new Padding(6, 0, 6, 0);
            _btnCancel.Click += delegate { Close(); };

            _btnBack = UiTheme.CreateSecondaryButton("قبلی", "▶");
            _btnBack.Size = new Size(120, 36); _btnBack.Margin = new Padding(6, 0, 6, 0);
            _btnBack.Click += delegate { GoBack(); };

            _btnNext = UiTheme.CreateButton("بعدی", "◀", UiTheme.Primary);
            _btnNext.Size = new Size(150, 36); _btnNext.Margin = new Padding(6, 0, 6, 0);
            _btnNext.Click += async delegate { await GoNext(); };

            // ترتیب بصری راست‌به‌چپ: بعدی (راست) ، قبلی ، انصراف
            navFlow.Controls.Add(_btnNext);
            navFlow.Controls.Add(_btnBack);
            navFlow.Controls.Add(_btnCancel);

            footer.Controls.Add(navFlow);
            footer.Controls.Add(_lblProgress);
            footer.Controls.Add(_progress);
            Controls.Add(footer);

            // ── ناحیه محتوا ──
            Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            Controls.Add(content);
            content.BringToFront();

            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i] = new Panel { Dock = DockStyle.Fill, Visible = false };
                content.Controls.Add(_pages[i]);
            }

            BuildPage0(); BuildPage1(); BuildPage2(); BuildPage3();
            BuildPage4(); BuildPage5(); BuildPage6(); BuildPage7();
        }

        // ─── مرحله ۱: انتخاب فایل ────────────────────────────────────────────
        private void BuildPage0()
        {
            Panel p = _pages[0];
            AddHint(p, "دو فایل HTML خروجی سامانه مرکزی را انتخاب کنید. «کد عمومی» موجود در هر دو فایل، شناسه‌ی یکتای خانواده است و تنها مبنای ارتباط داده‌هاست.", 0);

            _txtGuardians = AddFilePicker(p, "فایل سرپرستان:", 70, path => _source.GuardiansFilePath = path);
            _txtMembers = AddFilePicker(p, "فایل اعضای خانواده:", 150, path => _source.MembersFilePath = path);

            _chkBackup = new CheckBox
            {
                Text = "قبل از همگام‌سازی، بکاپ کامل گرفته شود (به‌شدت توصیه می‌شود)",
                Checked = true, AutoSize = true, Location = new Point(20, 240),
                Font = UiTheme.Font(UiTheme.SizeBody), RightToLeft = RightToLeft.Yes
            };
            _chkBackup.CheckedChanged += delegate { _takeBackup = _chkBackup.Checked; };
            p.Controls.Add(_chkBackup);
        }

        // ─── مرحله ۲: تحلیل ──────────────────────────────────────────────────
        private void BuildPage1()
        {
            AddHint(_pages[1], "فایل‌ها تجزیه می‌شوند و رکوردها استخراج می‌گردند. هیچ چیزی در این مرحله در دیتابیس نوشته نمی‌شود.", 0);
            _lblParseSummary = AddBigLabel(_pages[1], 90);
        }

        // ─── مرحله ۳: اعتبارسنجی ─────────────────────────────────────────────
        private void BuildPage2()
        {
            AddHint(_pages[2], "بررسی ساختاری داده‌ها. هشدارها مانع ادامه نیستند؛ رکوردهای دارای خطا در همگام‌سازی نادیده گرفته می‌شوند.", 0);
            _lstValidation = new ListBox
            {
                Location = new Point(20, 70), Size = new Size(860, 380),
                Font = UiTheme.Font(UiTheme.SizeBody), RightToLeft = RightToLeft.Yes,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pages[2].Controls.Add(_lstValidation);
        }

        // ─── مرحله ۴: مقایسه ─────────────────────────────────────────────────
        private void BuildPage3()
        {
            AddHint(_pages[3], "رکوردهای فایل با دیتابیس مقایسه می‌شوند تا مشخص شود کدام جدید، کدام بروزرسانی و کدام بدون تغییر است.", 0);
            _lblCompareSummary = AddBigLabel(_pages[3], 90);
        }

        // ─── مرحله ۵: پیش‌نمایش (با جزئیات و انتخاب) ─────────────────────────
        private void BuildPage4()
        {
            Panel p = _pages[4];
            _lblPreviewInfo = AddHint(p, "فقط رکوردهای قابل‌اعمال (جدید/بروزرسانی) نمایش داده می‌شوند. با تیک هر ردیف مشخص کنید اعمال شود یا نه؛ برای دیدن تغییرات فیلدی روی «جزئیات» کلیک کنید.", 0);

            _grdPreview = new DataGridView
            {
                Location = new Point(20, 70), Size = new Size(860, 380),
                AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(_grdPreview);

            var colSel = new DataGridViewCheckBoxColumn { Name = "sel", HeaderText = "اعمال", FillWeight = 40 };
            var colType = new DataGridViewTextBoxColumn { Name = "type", HeaderText = "نوع", ReadOnly = true, FillWeight = 60 };
            var colCode = new DataGridViewTextBoxColumn { Name = "code", HeaderText = "کد عمومی", ReadOnly = true, FillWeight = 70 };
            var colName = new DataGridViewTextBoxColumn { Name = "name", HeaderText = "نام", ReadOnly = true, FillWeight = 140 };
            var colAct = new DataGridViewTextBoxColumn { Name = "act", HeaderText = "عملیات", ReadOnly = true, FillWeight = 70 };
            var colCnt = new DataGridViewTextBoxColumn { Name = "cnt", HeaderText = "تغییرات", ReadOnly = true, FillWeight = 60 };
            var colDetail = new DataGridViewButtonColumn { Name = "detail", HeaderText = "", Text = "جزئیات", UseColumnTextForButtonValue = true, FillWeight = 60 };
            _grdPreview.Columns.AddRange(colSel, colType, colCode, colName, colAct, colCnt, colDetail);

            _grdPreview.CellClick += Preview_CellClick;
            _grdPreview.CellValueChanged += Preview_CellValueChanged;
            _grdPreview.CurrentCellDirtyStateChanged += delegate
            {
                if (_grdPreview.IsCurrentCellDirty)
                    _grdPreview.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            p.Controls.Add(_grdPreview);
        }

        // ─── مرحله ۶: تأیید ──────────────────────────────────────────────────
        private void BuildPage5()
        {
            AddHint(_pages[5], "خلاصه‌ی نهایی آنچه اعمال خواهد شد. با زدن «بعدی» همگام‌سازی آغاز می‌شود (پس از بکاپ).", 0);
            _lblConfirmSummary = AddBigLabel(_pages[5], 90);
        }

        // ─── مرحله ۷: همگام‌سازی ─────────────────────────────────────────────
        private void BuildPage6()
        {
            AddHint(_pages[6], "در حال بکاپ‌گیری و اعمال تغییرات در یک تراکنش اتمیک. لطفاً صبر کنید...", 0);
        }

        // ─── مرحله ۸: گزارش ──────────────────────────────────────────────────
        private void BuildPage7()
        {
            AddHint(_pages[7], "گزارش نهایی همگام‌سازی:", 0);
            _txtReport = new TextBox
            {
                Location = new Point(20, 70), Size = new Size(860, 380),
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9.5F), RightToLeft = RightToLeft.No,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pages[7].Controls.Add(_txtReport);
        }

        // ═══ ناوبری ═══════════════════════════════════════════════════════════
        private void ShowStep(int step)
        {
            _step = Math.Max(0, Math.Min(_pages.Length - 1, step));
            for (int i = 0; i < _pages.Length; i++)
                _pages[i].Visible = (i == _step);

            _lblStepHeader.Text = _stepTitles[_step];
            _btnBack.Enabled = _step > 0 && _step != 6 && _step != 7;
            _btnNext.Enabled = true;

            _btnNext.Text = _step == 5 ? "◀  شروع همگام‌سازی"
                          : _step == 7 ? "پایان"
                          : "بعدی  ◀";
            _btnCancel.Enabled = _step != 6;
        }

        private void GoBack()
        {
            if (_step > 0) ShowStep(_step - 1);
        }

        private async Task GoNext()
        {
            switch (_step)
            {
                case 0: // → تحلیل
                    if (!ValidateFilesChosen()) return;
                    ShowStep(1);
                    await RunParse();
                    break;
                case 1: ShowStep(2); RunValidation(); break;
                case 2: ShowStep(3); await RunCompare(); break;
                case 3: ShowStep(4); BuildPreview(); break;
                case 4: ShowStep(5); BuildConfirm(); break;
                case 5: ShowStep(6); await RunSync(); break;
                case 6: ShowStep(7); break; // معمولاً خودکار پیش می‌رود
                case 7: Close(); break;
            }
        }

        private bool ValidateFilesChosen()
        {
            if (string.IsNullOrWhiteSpace(_source.GuardiansFilePath) &&
                string.IsNullOrWhiteSpace(_source.MembersFilePath))
            {
                UiTheme.ShowWarning(this, "حداقل یکی از دو فایل را انتخاب کنید.");
                return false;
            }
            return true;
        }

        // ═══ اجرای مرحله‌ها ═══════════════════════════════════════════════════
        private async Task RunParse()
        {
            SetBusy(true, "در حال تجزیه فایل‌ها...");
            var provider = new HtmlSyncProvider();
            var progress = new Progress<SyncProgress>(UpdateProgress);
            try
            {
                _parsed = await Task.Run(() => provider.Parse(_source, progress));
                _lblParseSummary.Text =
                    "سرپرستان استخراج‌شده: " + _parsed.Guardians.Count + "\n" +
                    "اعضای خانواده استخراج‌شده: " + _parsed.Members.Count;
            }
            catch (Exception ex)
            {
                _lblParseSummary.Text = "خطا در تجزیه: " + ex.Message;
                _btnNext.Enabled = false;
            }
            finally { SetBusy(false, null); }
        }

        private void RunValidation()
        {
            _lstValidation.Items.Clear();
            var provider = new HtmlSyncProvider();
            var errors = provider.Validate(_parsed);
            if (errors.Count == 0)
                _lstValidation.Items.Add("✓ هیچ اخطاری یافت نشد.");
            else
                foreach (var e in errors) _lstValidation.Items.Add("• " + e);
        }

        private async Task RunCompare()
        {
            SetBusy(true, "در حال مقایسه با دیتابیس...");
            var comparer = new SyncComparer(new DatabaseHelper());
            var progress = new Progress<SyncProgress>(UpdateProgress);
            try
            {
                _plan = await Task.Run(() => comparer.BuildPlan(_parsed, progress));
                _lblCompareSummary.Text =
                    "── سرپرستان ──\n" +
                    "جدید: " + _plan.NewGuardians + "   بروزرسانی: " + _plan.UpdatedGuardians +
                    "   بدون تغییر: " + _plan.UnchangedGuardians +
                    "   تکراری: " + _plan.DuplicateGuardians + "   خطا: " + _plan.ErrorGuardians + "\n\n" +
                    "── اعضای خانواده ──\n" +
                    "جدید: " + _plan.NewMembers + "   بروزرسانی: " + _plan.UpdatedMembers +
                    "   بدون تغییر: " + _plan.UnchangedMembers +
                    "   تکراری: " + _plan.DuplicateMembers + "   خطا: " + _plan.ErrorMembers;
            }
            catch (Exception ex)
            {
                _lblCompareSummary.Text = "خطا در مقایسه: " + ex.Message;
                _btnNext.Enabled = false;
            }
            finally { SetBusy(false, null); }
        }

        private void BuildPreview()
        {
            _grdPreview.Rows.Clear();
            _previewRecords.Clear();
            if (_plan == null) return;

            // فقط رکوردهای قابل‌اعمال (جدید/بروزرسانی) نمایش داده می‌شوند.
            var actionable = _plan.Guardians.Concat(_plan.Members)
                .Where(r => r.Action == SyncAction.New || r.Action == SyncAction.Update)
                .ToList();

            const int MaxPreviewRows = 5000;
            foreach (var rec in actionable.Take(MaxPreviewRows))
            {
                _previewRecords.Add(rec);
                int idx = _grdPreview.Rows.Add(
                    rec.Selected,
                    rec.Entity == SyncEntity.Guardian ? "سرپرست" : "عضو",
                    rec.PublicCode,
                    rec.Title,
                    rec.Action == SyncAction.New ? "جدید" : "بروزرسانی",
                    rec.Action == SyncAction.New ? "—" : rec.Changes.Count(c => c.Selected).ToString());
                _grdPreview.Rows[idx].Tag = rec;
            }

            _lblPreviewInfo.Text = "رکوردهای قابل‌اعمال: " + actionable.Count +
                (actionable.Count > MaxPreviewRows ? "  (نمایش " + MaxPreviewRows + " ردیف اول؛ همه اعمال می‌شوند)" : "") +
                "  —  با تیک هر ردیف اعمال/عدم‌اعمال را مشخص کنید.";
        }

        private void Preview_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != _grdPreview.Columns["sel"].Index) return;
            var rec = _grdPreview.Rows[e.RowIndex].Tag as SyncRecord;
            if (rec != null)
                rec.Selected = Convert.ToBoolean(_grdPreview.Rows[e.RowIndex].Cells["sel"].Value);
        }

        private void Preview_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex != _grdPreview.Columns["detail"].Index) return;

            var rec = _grdPreview.Rows[e.RowIndex].Tag as SyncRecord;
            if (rec == null) return;
            ShowDetailDialog(rec);
            // به‌روزرسانی شمارش تغییرات پس از احتمال تغییر انتخاب‌ها
            if (rec.Action == SyncAction.Update)
                _grdPreview.Rows[e.RowIndex].Cells["cnt"].Value = rec.Changes.Count(c => c.Selected).ToString();
        }

        // دیالوگ جزئیات تغییرات: کد عمومی، نام فیلد، مقدار قبلی، مقدار جدید + چک‌باکس.
        private void ShowDetailDialog(SyncRecord rec)
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "جزئیات تغییرات — کد " + rec.PublicCode;
                dlg.RightToLeft = RightToLeft.Yes; dlg.RightToLeftLayout = true;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false; dlg.MinimizeBox = false; dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(680, 420);
                dlg.BackColor = UiTheme.CardBack; dlg.Font = UiTheme.Font(UiTheme.SizeBody);

                var grd = new DataGridView
                {
                    Location = new Point(14, 14), Size = new Size(652, 350),
                    AllowUserToAddRows = false, RowHeadersVisible = false, MultiSelect = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };
                UiTheme.StyleGrid(grd);

                if (rec.Action == SyncAction.New)
                {
                    grd.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "فیلد", Name = "f", ReadOnly = true, FillWeight = 40 });
                    grd.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "مقدار جدید", Name = "n", ReadOnly = true, FillWeight = 60 });
                    foreach (var kv in rec.SourceValues)
                        if (!string.Equals(kv.Key, "Code", StringComparison.OrdinalIgnoreCase))
                            grd.Rows.Add(UiTheme.TranslateHeader(kv.Key), kv.Value);
                }
                else
                {
                    var cApply = new DataGridViewCheckBoxColumn { HeaderText = "اعمال", Name = "a", FillWeight = 22 };
                    grd.Columns.Add(cApply);
                    grd.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "فیلد", Name = "f", ReadOnly = true, FillWeight = 26 });
                    grd.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "مقدار قبلی", Name = "o", ReadOnly = true, FillWeight = 26 });
                    grd.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "مقدار جدید", Name = "n", ReadOnly = true, FillWeight = 26 });
                    foreach (var ch in rec.Changes)
                    {
                        int i = grd.Rows.Add(ch.Selected, ch.DisplayName, ch.OldValue, ch.NewValue);
                        grd.Rows[i].Tag = ch;
                    }
                    grd.CurrentCellDirtyStateChanged += delegate
                    {
                        if (grd.IsCurrentCellDirty) grd.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    };
                    grd.CellValueChanged += delegate (object s, DataGridViewCellEventArgs ev)
                    {
                        if (ev.RowIndex < 0 || ev.ColumnIndex != 0) return;
                        var ch = grd.Rows[ev.RowIndex].Tag as FieldChange;
                        if (ch != null) ch.Selected = Convert.ToBoolean(grd.Rows[ev.RowIndex].Cells[0].Value);
                    };
                }

                dlg.Controls.Add(grd);

                Button ok = UiTheme.CreateButton("بستن", "", UiTheme.Primary);
                ok.SetBounds(dlg.ClientSize.Width - 134, 374, 120, 34);
                ok.DialogResult = DialogResult.OK;
                dlg.Controls.Add(ok);
                dlg.AcceptButton = ok;

                dlg.ShowDialog(this);
            }
        }

        private void BuildConfirm()
        {
            int gNew = _plan.Guardians.Count(r => r.Selected && r.Action == SyncAction.New);
            int gUpd = _plan.Guardians.Count(r => r.Selected && r.Action == SyncAction.Update && r.HasApplicableWork);
            int mNew = _plan.Members.Count(r => r.Selected && r.Action == SyncAction.New);
            int mUpd = _plan.Members.Count(r => r.Selected && r.Action == SyncAction.Update && r.HasApplicableWork);

            _lblConfirmSummary.Text =
                "با ادامه، موارد زیر اعمال می‌شوند:\n\n" +
                "• سرپرستان جدید: " + gNew + "\n" +
                "• سرپرستان بروزرسانی: " + gUpd + "\n" +
                "• اعضای جدید: " + mNew + "\n" +
                "• اعضای بروزرسانی: " + mUpd + "\n\n" +
                (_takeBackup ? "✓ ابتدا بکاپ کامل گرفته می‌شود." : "⚠ بدون بکاپ (توصیه نمی‌شود).") + "\n" +
                "همه‌ی تغییرات در یک تراکنش اتمیک انجام می‌شود؛ در صورت خطا کاملاً برگردانده می‌شود.";
        }

        private async Task RunSync()
        {
            SetBusy(true, "در حال همگام‌سازی...");
            _btnNext.Enabled = false; _btnBack.Enabled = false; _btnCancel.Enabled = false;

            var engine = new SyncEngine(new DatabaseHelper());
            var options = new SyncOptions { TakeBackup = _takeBackup };
            var progress = new Progress<SyncProgress>(UpdateProgress);
            try
            {
                _report = await Task.Run(() => engine.Apply(_plan, options, progress));
            }
            catch (Exception ex)
            {
                _report = new SyncReport { Success = false, ErrorMessage = ex.Message };
                _report.Add("خطای غیرمنتظره: " + ex.Message);
            }
            finally { SetBusy(false, null); }

            ShowStep(7);
            RenderReport();
        }

        private void RenderReport()
        {
            var r = _report;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(r.Success ? "✓ همگام‌سازی با موفقیت انجام شد." :
                          (r.RolledBack ? "✗ خطا رخ داد — همه‌ی تغییرات برگردانده شد (Rollback)." : "✗ همگام‌سازی ناموفق."));
            sb.AppendLine();
            sb.AppendLine("سرپرستان جدید      : " + r.GuardiansInserted);
            sb.AppendLine("سرپرستان بروزرسانی : " + r.GuardiansUpdated);
            sb.AppendLine("اعضای جدید         : " + r.MembersInserted);
            sb.AppendLine("اعضای بروزرسانی    : " + r.MembersUpdated);
            sb.AppendLine("رد شده             : " + r.Skipped);
            sb.AppendLine("خطا                : " + r.Errors);
            sb.AppendLine("مدت                : " + r.Duration.TotalSeconds.ToString("0.0") + " ثانیه");
            if (!string.IsNullOrWhiteSpace(r.BackupPath))
                sb.AppendLine("بکاپ               : " + r.BackupPath);
            if (!string.IsNullOrWhiteSpace(r.ErrorMessage))
                sb.AppendLine("پیام خطا           : " + r.ErrorMessage);
            sb.AppendLine();
            sb.AppendLine("── رویدادنگار ──");
            foreach (var line in r.Log) sb.AppendLine(line);

            _txtReport.Text = sb.ToString();
            _btnNext.Enabled = true; _btnCancel.Enabled = true;
        }

        // ═══ کمکی‌های UI ═════════════════════════════════════════════════════
        private void UpdateProgress(SyncProgress sp)
        {
            _progress.Value = Math.Max(0, Math.Min(100, sp.Percent));
            _lblProgress.Text = sp.Phase + " — " + sp.Current + " / " + sp.Total;
        }

        private void SetBusy(bool busy, string phase)
        {
            _progress.Visible = busy;
            _lblProgress.Visible = busy;
            if (busy) { _progress.Value = 0; _lblProgress.Text = phase ?? ""; }
            _btnNext.Enabled = !busy;
            _btnBack.Enabled = !busy && _step > 0;
            Application.DoEvents();
        }

        private Label AddHint(Panel p, string text, int y)
        {
            Label l = new Label
            {
                Text = text, Location = new Point(20, 12 + y), Size = new Size(860, 48),
                Font = UiTheme.Font(UiTheme.SizeBody), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.TopRight
            };
            p.Controls.Add(l);
            return l;
        }

        private Label AddBigLabel(Panel p, int y)
        {
            Label l = new Label
            {
                Location = new Point(20, y), Size = new Size(860, 360),
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.TopRight
            };
            p.Controls.Add(l);
            return l;
        }

        private TextBox AddFilePicker(Panel p, string label, int y, Action<string> onPick)
        {
            Label lbl = new Label
            {
                Text = label, Location = new Point(20, y), Size = new Size(860, 22),
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            };
            p.Controls.Add(lbl);

            TextBox txt = new TextBox
            {
                Location = new Point(140, y + 26), Size = new Size(600, 26),
                ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.No
            };
            p.Controls.Add(txt);

            Button btn = UiTheme.CreateButton("انتخاب...", "▤", UiTheme.Primary);
            btn.SetBounds(20, y + 25, 110, 28);
            btn.Click += delegate
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = label;
                    ofd.Filter = "فایل HTML|*.html;*.htm|همه فایل‌ها|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        txt.Text = ofd.FileName;
                        onPick(ofd.FileName);
                    }
                }
            };
            p.Controls.Add(btn);
            return txt;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    // ─────────────────────────────────────────────────────────────────────────
    // FrmAccountingRepair — «ابزار اصلاح داده‌های تاریخی حسابداری».
    //
    // آموزش — قواعد رفتاری این فرم (عمدی و غیرقابل‌مذاکره):
    //
    //   ۱) هیچ اصلاحی خودکار نیست. هیچ دکمه‌ی «اصلاح همه»ای وجود ندارد.
    //      کاربر باید هر مورد را جداگانه انتخاب، بازبینی و تأیید کند.
    //
    //   ۲) اصلاح پیشنهادی همیشه نمایش داده می‌شود، همراه با «مبنای پیشنهاد»
    //      تا حسابدار بداند سیستم از کجا به آن رسیده و بتواند ردش کند.
    //
    //   ۳) «دلیل اصلاح» اجباری است و در ردّ حسابرسی ثبت می‌شود.
    //
    //   ۴) پیش از اعمال، یک پیام تأیید دقیقاً می‌گوید چه چیزی از چه مقداری
    //      به چه مقداری تغییر می‌کند.
    //
    //   ۵) این فرم هیچ محاسبه‌ی حسابداری‌ای انجام نمی‌دهد و هیچ منطق تجاری‌ای
    //      را تغییر نمی‌دهد — فقط داده‌ی معیوبِ تاریخی را اصلاح می‌کند.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmAccountingRepair : Form
    {
        private readonly AccountingRepo _repo;
        private readonly AccRepair _repair;

        private List<AccRepair.RepairItem> _items = new List<AccRepair.RepairItem>();

        private DataGridView _grid;
        private Label _lblSummary;

        // پنل جزئیات
        private Label _lblProblem, _lblCurrent, _lblBasis, _lblKind;
        private ComboBox _cmbPeriod, _cmbCenter;
        private TextBox _txtDate, _txtReason;
        private Label _lblVoidNote;
        private Button _btnApply;

        public FrmAccountingRepair(AccountingRepo repo)
        {
            _repo = repo;
            _repair = new AccRepair(repo);
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "ابزار اصلاح داده‌های تاریخی حسابداری";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1180, 760);

            var banner = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = UiTheme.PrimaryDark };
            banner.Controls.Add(new Label
            {
                Text = "🛠  ابزار اصلاح داده‌های تاریخی حسابداری",
                Dock = DockStyle.Fill, ForeColor = Color.White, Font = UiTheme.FontBold(15F),
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 20, 0)
            });

            var warn = new Label
            {
                Text = "این ابزار فقط داده‌های تاریخیِ معیوب را اصلاح می‌کند و هیچ محاسبه یا منطق حسابداری را تغییر نمی‌دهد. " +
                       "هیچ اصلاحی خودکار انجام نمی‌شود: هر مورد باید جداگانه بازبینی و تأیید شود. " +
                       "پیش از شروع، حتماً یک بکاپ حسابداری بگیرید.",
                Dock = DockStyle.Top, Height = 52, AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMuted, Padding = new Padding(14, 6, 14, 4)
            };

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.CardBack };

            var btnScan = UiTheme.CreateButton("بررسی و یافتن اشکالات", "🔍", UiTheme.Primary);
            btnScan.SetBounds(14, 10, 210, 36);
            btnScan.Click += delegate { Rescan(); };

            // خروجی اکسل — برای بازبینی فهرست اشکالات بیرون از برنامه و
            // هماهنگی با حسابدار پیش از اعمال هر اصلاحی.
            var btnExport = UiTheme.CreateSecondaryButton("خروجی اکسل", "📊");
            btnExport.SetBounds(234, 10, 150, 36);
            btnExport.Click += delegate { ExportIssues(); };

            _lblSummary = new Label
            {
                AutoSize = false, Font = UiTheme.FontBold(11F), ForeColor = UiTheme.PrimaryDark,
                TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
            };
            _lblSummary.SetBounds(394, 10, 560, 36);

            btnBar.Controls.Add(btnScan);
            btnBar.Controls.Add(btnExport);
            btnBar.Controls.Add(_lblSummary);

            // ─── جدول موارد ───────────────────────────────────────────────
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false
            };
            UiTheme.StyleGrid(_grid);

            // هر دو رویداد لازم‌اند: بسته به این‌که کاربر با ماوس کلیک کند یا با
            // کلید جابه‌جا شود، ترتیب شلیک این دو فرق می‌کند. با گوش‌دادن به هر
            // دو، پنل جزئیات در هر مسیری با ردیفِ واقعاً انتخاب‌شده هماهنگ می‌ماند.
            _grid.SelectionChanged += delegate { ShowSelected(); };
            _grid.CurrentCellChanged += delegate { ShowSelected(); };

            var gridCard = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 6), BackColor = Color.Transparent };
            gridCard.Controls.Add(_grid);

            Controls.Add(gridCard);
            Controls.Add(BuildDetailPanel());
            Controls.Add(btnBar);
            Controls.Add(warn);
            Controls.Add(banner);

            ForceRtl(this);
            Rescan();
        }

        private static void ForceRtl(Control root)
        {
            root.RightToLeft = RightToLeft.Yes;
            var p = root.GetType().GetProperty("RightToLeftLayout");
            if (p != null && p.CanWrite) p.SetValue(root, true, null);
            foreach (Control c in root.Controls) ForceRtl(c);
        }

        // ═══════════════════════════════════════════════════════════════════
        // پنل جزئیات و تأیید
        // ═══════════════════════════════════════════════════════════════════
        private Panel BuildDetailPanel()
        {
            var panel = new Panel { Dock = DockStyle.Bottom, Height = 268, BackColor = UiTheme.CardBack, Padding = new Padding(14, 8, 14, 8) };

            _lblKind = new Label
            {
                Dock = DockStyle.Top, Height = 28, Text = "جزئیات مورد انتخاب‌شده",
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
            };

            _lblProblem = MakeInfoLabel(42);
            _lblCurrent = MakeInfoLabel(24);
            _lblBasis = MakeInfoLabel(38);
            _lblBasis.ForeColor = UiTheme.TextMuted;

            // ─ کنترل‌های اصلاح؛ بسته به نوع مورد یکی نمایش داده می‌شود ─
            _cmbPeriod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 460, Font = UiTheme.Font(UiTheme.SizeBody) };
            _cmbCenter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 460, Font = UiTheme.Font(UiTheme.SizeBody) };
            _txtDate = new TextBox { Width = 200, Font = UiTheme.Font(UiTheme.SizeBody), TextAlign = HorizontalAlignment.Center };
            _lblVoidNote = new Label
            {
                AutoSize = false, Width = 620, Height = 26, Font = UiTheme.FontBold(10F),
                ForeColor = UiTheme.Danger, TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
            };

            var fixRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false, BackColor = Color.Transparent
            };
            fixRow.Controls.Add(new Label
            {
                Text = "اصلاح پیشنهادی:", AutoSize = false, Width = 110, Height = 28,
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
            });
            fixRow.Controls.Add(_cmbPeriod);
            fixRow.Controls.Add(_cmbCenter);
            fixRow.Controls.Add(_txtDate);
            fixRow.Controls.Add(_lblVoidNote);

            // ─ دلیل اصلاح (اجباری) ─
            _txtReason = new TextBox { Width = 620, Font = UiTheme.Font(UiTheme.SizeBody) };
            var reasonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false, BackColor = Color.Transparent
            };
            reasonRow.Controls.Add(new Label
            {
                Text = "دلیل اصلاح (اجباری):", AutoSize = false, Width = 130, Height = 28,
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
            });
            reasonRow.Controls.Add(_txtReason);

            var actionRow = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent };
            _btnApply = UiTheme.CreateButton("تأیید و اعمال این اصلاح", "✔", UiTheme.Success);
            _btnApply.SetBounds(0, 6, 230, 36);
            _btnApply.Enabled = false;
            _btnApply.Click += delegate { ApplySelected(); };
            actionRow.Controls.Add(_btnApply);

            // ترتیب افزودن معکوس است چون همه Dock=Top هستند.
            panel.Controls.Add(actionRow);
            panel.Controls.Add(reasonRow);
            panel.Controls.Add(fixRow);
            panel.Controls.Add(_lblBasis);
            panel.Controls.Add(_lblCurrent);
            panel.Controls.Add(_lblProblem);
            panel.Controls.Add(_lblKind);

            return panel;
        }

        private static Label MakeInfoLabel(int height)
        {
            return new Label
            {
                Dock = DockStyle.Top, Height = height, AutoSize = false,
                Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // بررسی (فقط‌خواندنی)
        // ═══════════════════════════════════════════════════════════════════
        private void Rescan()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _items = _repair.Detect();
                _grid.DataSource = BuildTable(_items);

                if (_grid.Columns.Contains("Idx")) _grid.Columns["Idx"].Visible = false;
                if (_grid.Columns.Contains("شرح اشکال"))
                    _grid.Columns["شرح اشکال"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                if (_grid.Columns.Contains("مبلغ"))
                {
                    _grid.Columns["مبلغ"].DefaultCellStyle.Format = "N0";
                    _grid.Columns["مبلغ"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                }

                int noSuggestion = 0;
                foreach (var i in _items) if (!i.HasSuggestion) noSuggestion++;

                _lblSummary.Text = _items.Count == 0
                    ? "✔ هیچ اشکال قابل اصلاحی پیدا نشد."
                    : _items.Count + " مورد قابل اصلاح پیدا شد" +
                      (noSuggestion > 0 ? "  —  " + noSuggestion + " مورد نیاز به تصمیم دستی دارد." : ".");

                _grid.ClearSelection();
                ShowSelected();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در بررسی: " + ex.Message);
            }
            finally { Cursor = Cursors.Default; }
        }

        private static DataTable BuildTable(List<AccRepair.RepairItem> items)
        {
            var dt = new DataTable("RepairItems");
            dt.Columns.Add("Idx", typeof(int));
            dt.Columns.Add("دسته", typeof(string));
            dt.Columns.Add("جدول", typeof(string));
            dt.Columns.Add("شناسه", typeof(int));
            dt.Columns.Add("شرح اشکال", typeof(string));
            dt.Columns.Add("اصلاح پیشنهادی", typeof(string));
            dt.Columns.Add("مبلغ", typeof(double));

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                dt.Rows.Add(i, it.Category, it.Table, it.RecordId, it.Problem, it.Suggestion, it.Amount);
            }
            return dt;
        }

        // آموزش — چرا SelectedRows خوانده می‌شود و نه CurrentRow:
        //
        // رویداد SelectionChanged در DataGridView گاهی *پیش از* به‌روزرسانی
        // CurrentRow شلیک می‌شود. نتیجه‌اش این بود که پنل جزئیات همیشه «یک
        // انتخاب عقب» می‌ماند: با انتخاب ردیفی از نوع «تاریخ بدقالب»، هنوز
        // کمبوی مرکزِ ردیف قبلی نمایش داده می‌شد. روی یک ابزار مالی این یعنی
        // حسابدار ممکن است اصلاحی را تأیید کند در حالی که فکر می‌کند دارد چیز
        // دیگری را اصلاح می‌کند.
        //
        // ضمناً پس از ClearSelection مقدار CurrentRow همچنان ردیف صفر را
        // برمی‌گرداند، پس دکمه‌ی «اعمال» بدون هیچ انتخاب مرئی‌ای فعال می‌ماند.
        // SelectedRows هر دو مشکل را هم‌زمان حل می‌کند، چون دقیقاً همان چیزی
        // را می‌گوید که کاربر روی صفحه می‌بیند.
        // خروجی اکسل از فهرست اشکالات — کاملاً فقط‌خواندنی، هیچ اصلاحی انجام
        // نمی‌دهد. ستون «مبنای پیشنهاد» هم اضافه می‌شود (در گرید جا نمی‌شد) تا
        // بازبینِ بیرونی بداند هر پیشنهاد از کجا آمده است.
        private void ExportIssues()
        {
            if (_items.Count == 0)
            { UiTheme.ShowWarning(this, "ابتدا بررسی را اجرا کنید — موردی برای خروجی وجود ندارد."); return; }

            using (var sfd = new SaveFileDialog
            {
                Filter = "فایل اکسل|*.xlsx",
                FileName = "اشکالات داده حسابداری.xlsx"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    using (var wb = new ClosedXML.Excel.XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("اشکالات داده");
                        ws.RightToLeft = true;

                        string[] headers =
                        {
                            "دسته", "جدول", "شناسه", "شرح اشکال",
                            "مقدار فعلی", "اصلاح پیشنهادی", "مبنای پیشنهاد",
                            "پیشنهاد خودکار دارد؟", "مبلغ"
                        };

                        for (int c = 0; c < headers.Length; c++)
                        {
                            ws.Cell(1, c + 1).Value = headers[c];
                            ws.Cell(1, c + 1).Style.Font.Bold = true;
                            ws.Cell(1, c + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2C5A85");
                            ws.Cell(1, c + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                        }

                        for (int i = 0; i < _items.Count; i++)
                        {
                            var it = _items[i];
                            int r = i + 2;
                            ws.Cell(r, 1).Value = it.Category;
                            ws.Cell(r, 2).Value = it.Table;
                            ws.Cell(r, 3).Value = it.RecordId;
                            ws.Cell(r, 4).Value = it.Problem;
                            ws.Cell(r, 5).Value = it.CurrentValue;
                            ws.Cell(r, 6).Value = it.Suggestion;
                            ws.Cell(r, 7).Value = it.Basis;
                            ws.Cell(r, 8).Value = it.HasSuggestion ? "بله" : "خیر — تصمیم دستی";
                            ws.Cell(r, 9).Value = it.Amount;
                            ws.Cell(r, 9).Style.NumberFormat.Format = "#,##0";

                            // موارد نیازمند تصمیم دستی برجسته می‌شوند.
                            if (!it.HasSuggestion)
                                ws.Range(r, 1, r, headers.Length).Style.Fill.BackgroundColor =
                                    ClosedXML.Excel.XLColor.FromHtml("#FFF4CE");
                        }

                        ws.Columns().AdjustToContents();
                        wb.SaveAs(sfd.FileName);
                    }

                    UiTheme.ShowSuccess(this, "فهرست اشکالات ذخیره شد:\n" + sfd.FileName);
                }
                catch (Exception ex)
                {
                    UiTheme.ShowError(this, "خطا در ساخت اکسل: " + ex.Message);
                }
            }
        }

        private AccRepair.RepairItem Selected()
        {
            if (_grid.SelectedRows.Count == 0 || !_grid.Columns.Contains("Idx")) return null;

            object v = _grid.SelectedRows[0].Cells["Idx"].Value;
            if (v == null || v == DBNull.Value) return null;

            int idx = Convert.ToInt32(v);
            return idx >= 0 && idx < _items.Count ? _items[idx] : null;
        }

        // نمایش جزئیات مورد انتخاب‌شده و آماده‌سازی کنترل اصلاحِ متناسب با نوع آن.
        private void ShowSelected()
        {
            var item = Selected();

            _cmbPeriod.Visible = false;
            _cmbCenter.Visible = false;
            _txtDate.Visible = false;
            _lblVoidNote.Visible = false;

            if (item == null)
            {
                _lblKind.Text = "جزئیات مورد انتخاب‌شده";
                _lblProblem.Text = "برای دیدن اصلاح پیشنهادی، یک مورد را از جدول بالا انتخاب کنید.";
                _lblCurrent.Text = "";
                _lblBasis.Text = "";
                _btnApply.Enabled = false;
                return;
            }

            _lblKind.Text = item.Category + "  —  " + item.Table + " شماره " + item.RecordId;
            _lblProblem.Text = "اشکال:  " + item.Problem;
            _lblCurrent.Text = "مقدار فعلی:  " + item.CurrentValue +
                               (item.Amount > 0 ? "     |     مبلغ مرتبط: " + item.Amount.ToString("N0") + " افغانی" : "");
            _lblBasis.Text = "مبنای پیشنهاد:  " + (item.Basis ?? "");

            switch (item.Kind)
            {
                case AccRepair.KindAssignPeriod:
                    BindCombo(_cmbPeriod, _repair.GetAllPeriodsForCombo(), "PeriodID", "Display", item.SuggestedPeriodId);
                    _cmbPeriod.Visible = true;
                    break;

                case AccRepair.KindAssignCenter:
                    BindCombo(_cmbCenter, _repair.GetCentersForCombo(), "CenterID", "Display", item.SuggestedCenterId);
                    _cmbCenter.Visible = true;
                    break;

                case AccRepair.KindFixDate:
                    _txtDate.Text = item.SuggestedDate ?? "";
                    _txtDate.Visible = true;
                    break;

                case AccRepair.KindVoidDuplicate:
                    _lblVoidNote.Text = item.Suggestion + "   (رکورد حذف نمی‌شود، فقط باطل می‌شود)";
                    _lblVoidNote.Visible = true;
                    break;
            }

            _btnApply.Enabled = true;
        }

        private static void BindCombo(ComboBox cmb, DataTable dt, string valueCol, string displayCol, int? preselect)
        {
            cmb.DataSource = dt;
            cmb.ValueMember = valueCol;
            cmb.DisplayMember = displayCol;

            if (preselect.HasValue)
            {
                try { cmb.SelectedValue = preselect.Value; }
                catch { cmb.SelectedIndex = -1; }
            }
            else cmb.SelectedIndex = -1;
        }

        // ═══════════════════════════════════════════════════════════════════
        // اعمال — فقط پس از تأیید صریح کاربر
        // ═══════════════════════════════════════════════════════════════════
        private void ApplySelected()
        {
            var item = Selected();
            if (item == null) { UiTheme.ShowWarning(this, "ابتدا یک مورد را انتخاب کنید."); return; }

            string reason = (_txtReason.Text ?? "").Trim();
            if (string.IsNullOrEmpty(reason))
            {
                UiTheme.ShowWarning(this, "ثبت «دلیل اصلاح» اجباری است. لطفاً دلیل را وارد کنید.");
                _txtReason.Focus();
                return;
            }

            // مقدار انتخاب‌شده‌ی کاربر جایگزین پیشنهاد سیستم می‌شود.
            string changeText;
            switch (item.Kind)
            {
                case AccRepair.KindAssignPeriod:
                    if (_cmbPeriod.SelectedValue == null || _cmbPeriod.SelectedValue == DBNull.Value)
                    { UiTheme.ShowWarning(this, "دوره مالی مقصد را انتخاب کنید."); return; }
                    item.SuggestedPeriodId = Convert.ToInt32(_cmbPeriod.SelectedValue);
                    changeText = "دوره مالی:  «بدون دوره»  ←  «" + _cmbPeriod.Text + "»";
                    break;

                case AccRepair.KindAssignCenter:
                    if (_cmbCenter.SelectedValue == null || _cmbCenter.SelectedValue == DBNull.Value)
                    { UiTheme.ShowWarning(this, "مرکز مقصد را انتخاب کنید."); return; }
                    item.SuggestedCenterId = Convert.ToInt32(_cmbCenter.SelectedValue);
                    changeText = "مرکز:  «بدون مرکز»  ←  «" + _cmbCenter.Text + "»";
                    break;

                case AccRepair.KindFixDate:
                    item.SuggestedDate = (_txtDate.Text ?? "").Trim();
                    changeText = item.DateColumn + ":  «" + item.CurrentValue + "»  ←  «" + item.SuggestedDate + "»";
                    break;

                case AccRepair.KindVoidDuplicate:
                    changeText = "رکورد شماره " + item.RecordId + " باطل می‌شود (حذف نمی‌شود و در حسابرسی می‌ماند).";
                    break;

                default:
                    UiTheme.ShowWarning(this, "نوع اصلاح پشتیبانی نمی‌شود.");
                    return;
            }

            if (!UiTheme.ShowConfirm(this,
                    "این اصلاح روی داده‌ی مالی اعمال می‌شود:\n\n" +
                    item.Table + "  —  شناسه " + item.RecordId + "\n" +
                    changeText + "\n\n" +
                    "دلیل: " + reason + "\n\n" +
                    "آیا مطمئن هستید؟",
                    "تأیید اصلاح داده"))
                return;

            try
            {
                _repair.Apply(item, reason);
                UiTheme.ShowSuccess(this, "اصلاح انجام و در ردّ حسابرسی ثبت شد.");
                _txtReason.Text = "";
                Rescan();
            }
            catch (AccountingRuleException ex)
            {
                UiTheme.ShowWarning(this, ex.Message);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در اعمال اصلاح: " + ex.Message);
            }
        }
    }
}

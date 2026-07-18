using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    // ─────────────────────────────────────────────────────────────────────────
    // فرم هاب «حسابداری داخلی ایتام» — تب‌های اطلاعات پایه و عملیات مالی.
    // آموزش: مطابق الگوی فرم‌های موجود (FrmDashboard/FrmSettings) با TabControl
    // راست‌به‌چپ و کنترل‌های UiTheme ساخته شده تا ظاهر یکسان و حرفه‌ای باشد.
    // منطق داده در AccountingRepo است (جدایی لایه‌ها).
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmAccounting : Form
    {
        private readonly AccountingRepo _repo = new AccountingRepo();

        public FrmAccounting()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "حسابداری داخلی ایتام  —  " + SecurityContext.CenterDisplay;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeFixedSize(this, 1280, 740);

            // سربرگ
            Panel banner = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = UiTheme.PrimaryDark };
            Label lblTitle = new Label
            {
                Text = "💰  حسابداری داخلی ایتام",
                Dock = DockStyle.Fill, ForeColor = Color.White, Font = UiTheme.FontBold(15F),
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 20, 0)
            };
            banner.Controls.Add(lblTitle);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = UiTheme.FontBold(10F);
            tabs.RightToLeft = RightToLeft.Yes;
            tabs.RightToLeftLayout = true;

            tabs.TabPages.Add(BuildTransactionsTab());   // دریافت/پرداخت + دفتر صندوق
            tabs.TabPages.Add(BuildStipendTab());        // شهریه ایتام
            tabs.TabPages.Add(BuildSalaryTab());         // حقوق کارکنان
            tabs.TabPages.Add(BuildExpenseItemsTab());   // هزینه‌های جاری
            tabs.TabPages.Add(BuildReportsTab());        // گزارش‌ها
            tabs.TabPages.Add(BuildPeriodsTab());        // دوره مالی
            tabs.TabPages.Add(BuildFundsTab());          // صندوق
            tabs.TabPages.Add(BuildPartiesTab());        // طرف حساب
            tabs.TabPages.Add(BuildCategoriesTab(true)); // دسته‌بندی درآمد
            tabs.TabPages.Add(BuildCategoriesTab(false));// دسته‌بندی هزینه
            tabs.TabPages.Add(BuildSettingsTab());       // تنظیمات گزارش

            Controls.Add(tabs);
            Controls.Add(banner);

            // آموزش — رفع باگ «متن‌ها چپ رفته»: تنظیم RightToLeft فقط روی
            // Form/TabControl کافی نیست. برخی کنترل‌ها (به‌خصوص FlowLayoutPanel
            // با FlowDirection.RightToLeft) طبق مستندات مایکروسافت فقط زمانی
            // جهت‌شان معکوس می‌شود که RightToLeft آن‌ها واقعاً روی Yes resolve
            // شده باشد — و در این پروژه با ساخت پویا (نه Designer)، Inherit
            // همیشه به‌موقع اعمال نمی‌شود. راه‌حل مطمئن: بعد از ساخت کامل UI،
            // یک‌بار کل درخت کنترل‌ها را پیمایش و RightToLeft/RightToLeftLayout
            // را صریحاً روی هرکدام تنظیم می‌کنیم.
            ForceRtl(this);
        }

        private static void ForceRtl(Control root)
        {
            root.RightToLeft = RightToLeft.Yes;
            var rtlLayoutProp = root.GetType().GetProperty("RightToLeftLayout");
            if (rtlLayoutProp != null && rtlLayoutProp.CanWrite)
                rtlLayoutProp.SetValue(root, true, null);

            foreach (Control child in root.Controls)
                ForceRtl(child);
        }

        // ═══════════════════════════════════════════════════════════════════
        // کمکی‌های ساخت کنترل
        // ═══════════════════════════════════════════════════════════════════
        private DataGridView NewGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false
            };
            UiTheme.StyleGrid(g);
            return g;
        }

        private Panel Field(string label, Control input, int width)
        {
            var p = new Panel { Width = width, Height = 58, Margin = new Padding(6, 4, 6, 4) };
            var l = new Label { Text = label, AutoSize = false, Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark };
            input.Dock = DockStyle.Top;
            if (input is TextBox tb) { tb.Height = 28; UiTheme.StyleTextBox(tb); }
            if (input is NumericUpDown nud) { nud.Height = 28; nud.TextAlign = HorizontalAlignment.Right; }
            p.Controls.Add(input); p.Controls.Add(l);
            return p;
        }

        // مبلغ با جداکننده هزارگان (مثل الگوی موجود در FrmFinance) — مثلاً
        // 12000 هنگام تایپ به‌صورت 12,000 نمایش داده می‌شود تا خواناتر باشد.
        // آموزش — allowNegative فقط برای «مانده ابتدای دوره/صندوق» true است،
        // چون یک دوره ممکن است با کسری (مانده منفی) بسته شده باشد و این کسری
        // باید واقعی منتقل شود، نه با صفر جایگزین شود.
        private NumericUpDown NewAmountBox(int decimals = 0, bool allowNegative = false)
        {
            return new NumericUpDown
            {
                Maximum = 1000000000,
                Minimum = allowNegative ? -1000000000 : 0,
                DecimalPlaces = decimals,
                ThousandsSeparator = true,
                TextAlign = HorizontalAlignment.Right
            };
        }

        // ورودی دستی تاریخ شمسی با ماسک اسلش‌دار (سال/ماه/روز) + راهنمای فرمت
        // زیر فیلد — به‌جای پاپ‌آپ تقویم، برای ثبت سریع چند سند پشت‌سرهم.
        private MaskedTextBox NewDateBox()
        {
            var mtb = new MaskedTextBox("0000/00/00");
            mtb.PromptChar = '_';
            mtb.Text = PersianDateHelper.ToPersianDateString(DateTime.Today);
            mtb.TextAlign = HorizontalAlignment.Center;
            mtb.RightToLeft = RightToLeft.Yes;
            mtb.Font = UiTheme.Font(UiTheme.SizeBody);
            mtb.BorderStyle = BorderStyle.FixedSingle;
            return mtb;
        }

        private Panel DateField(string label, MaskedTextBox box, int width = 190)
        {
            var p = new Panel { Width = width, Height = 74, Margin = new Padding(6, 4, 6, 4) };
            var l = new Label { Text = label, AutoSize = false, Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark };
            var hint = new Label { Text = "فرمت: سال/ماه/روز — مثال 1404/05/12", AutoSize = false, Dock = DockStyle.Top, Height = 16, Font = UiTheme.Font(7.5F), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleRight };
            box.Dock = DockStyle.Top; box.Height = 28;
            p.Controls.Add(hint); p.Controls.Add(box); p.Controls.Add(l);
            return p;
        }

        private ComboBox NewCombo(bool dropDownList = true)
        {
            return new ComboBox { DropDownStyle = dropDownList ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown, Font = UiTheme.Font(UiTheme.SizeBody) };
        }

        private void BindCombo(ComboBox cmb, DataTable dt, string valueCol, string displayCol, bool addEmpty = false)
        {
            if (addEmpty)
            {
                DataRow r = dt.NewRow();
                r[valueCol] = DBNull.Value; r[displayCol] = "";
                dt.Rows.InsertAt(r, 0);
            }
            cmb.DataSource = dt;
            cmb.ValueMember = valueCol;
            cmb.DisplayMember = displayCol;
            cmb.SelectedIndex = -1;
        }

        private static double ParseNum(string s)
        {
            double d;
            s = (s ?? "").Trim().Replace(",", "");
            return double.TryParse(s, out d) ? d : 0;
        }

        // آموزش — رفع باگ بحرانی: System.Data.SQLite ستون‌های INTEGER را به‌صورت
        // Int64 (long) برمی‌گرداند، نه Int32 (int). به همین دلیل الگوی قبلی
        // «SelectedValue is int» همیشه false می‌شد و مقدار انتخاب‌شده‌ی کمبو
        // (دوره/طرف‌حساب/صندوق/دسته) به‌عنوان null تفسیر می‌شد؛ در نتیجه همه
        // رکوردها با PeriodID=NULL ذخیره می‌شدند و فیلتر گزارش‌ها بی‌اثر بود.
        // این helper هر نوع عددی (int/long) و DBNull/null را درست مدیریت می‌کند.
        private static int? ComboIntValue(ComboBox cmb)
        {
            object v = cmb.SelectedValue;
            if (v == null || v == DBNull.Value) return null;
            try { return Convert.ToInt32(v); }
            catch { return null; }
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: دریافت / پرداخت + دفتر صندوق
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _txnPeriod, _txnParty, _txnFund, _txnCategory;
        private ComboBox _txnDirection;
        private TextBox _txnDocNo, _txnQty, _txnDesc;
        private NumericUpDown _txnAmount, _txnDollar, _txnRate;
        private MaskedTextBox _txnDate;
        private DataGridView _gridTxn;
        private Label _lblFundBalance;

        private TabPage BuildTransactionsTab()
        {
            var page = new TabPage("دریافت / پرداخت") { BackColor = UiTheme.Background };

            // فرم ورودی (بالا)
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 160, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 8, 10, 4), AutoScroll = true };

            _txnDirection = NewCombo(); _txnDirection.Items.AddRange(new object[] { "دریافت", "پرداخت" }); _txnDirection.SelectedIndex = 0;
            _txnDirection.SelectedIndexChanged += delegate { ReloadTxnCategories(); };
            // آموزش — شماره سند مسلسل، خودکار و غیرقابل ویرایش (به‌درخواست کاربر
            // و طبق اصول حسابداری). ReadOnly + رنگ متمایز تا کاربر متوجه شود دستی نیست.
            // با تغییر «دوره مالی» (رویداد پایین‌تر پس از ساخت _txnPeriod)، این
            // شماره دوباره محاسبه می‌شود تا در هر دوره از ۱ ریستارت شود.
            _txnDocNo = new TextBox { Text = _repo.NextDocNo(null), ReadOnly = true, BackColor = UiTheme.Background, TabStop = false };
            _txnDate = NewDateBox();
            _txnPeriod = NewCombo(); _txnParty = NewCombo(); _txnFund = NewCombo(); _txnCategory = NewCombo();
            _txnAmount = NewAmountBox(); _txnQty = new TextBox(); _txnDollar = NewAmountBox(2); _txnRate = NewAmountBox(2); _txnDesc = new TextBox();

            form.Controls.Add(Field("نوع", _txnDirection, 120));
            form.Controls.Add(Field("شماره سند", _txnDocNo, 120));
            form.Controls.Add(DateField("تاریخ", _txnDate));
            form.Controls.Add(Field("دوره مالی", _txnPeriod, 180));
            form.Controls.Add(Field("طرف حساب", _txnParty, 180));
            form.Controls.Add(Field("صندوق", _txnFund, 160));
            form.Controls.Add(Field("دسته‌بندی", _txnCategory, 180));
            form.Controls.Add(Field("مبلغ (افغانی)", _txnAmount, 140));
            form.Controls.Add(Field("تعداد/مقدار", _txnQty, 120));
            form.Controls.Add(Field("مبلغ دلاری", _txnDollar, 110));
            form.Controls.Add(Field("نرخ دلار", _txnRate, 100));
            form.Controls.Add(Field("توضیح", _txnDesc, 260));

            // محاسبه خودکار: مبلغ افغانی = دلاری × نرخ (اگر هر دو وارد شوند)
            EventHandler calc = delegate
            {
                if (_txnDollar.Value > 0 && _txnRate.Value > 0) _txnAmount.Value = Math.Min(_txnAmount.Maximum, Math.Round(_txnDollar.Value * _txnRate.Value));
            };
            _txnDollar.ValueChanged += calc;
            _txnRate.ValueChanged += calc;

            // نوار دکمه‌ها
            var btnBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ثبت تراکنش", "✔", UiTheme.Success); btnSave.SetBounds(14, 7, 150, 34);
            btnSave.Click += delegate { SaveTransaction(); };
            var btnNew = UiTheme.CreateSecondaryButton("فرم جدید", "＋"); btnNew.SetBounds(172, 7, 120, 34);
            btnNew.Click += delegate { ResetTxnForm(); };
            var btnVoucher = UiTheme.CreateButton("نمایش/چاپ فاکتور", "🧾", UiTheme.Primary); btnVoucher.SetBounds(300, 7, 160, 34);
            btnVoucher.Click += delegate { PrintSelectedVoucher(); };
            var btnDelete = UiTheme.CreateButton("حذف انتخاب‌شده", "✕", UiTheme.Danger); btnDelete.SetBounds(468, 7, 150, 34);
            btnDelete.Click += delegate { DeleteSelectedTxn(); };
            _lblFundBalance = new Label { Text = "", AutoSize = false, Font = UiTheme.FontBold(11F), ForeColor = UiTheme.PrimaryDark, TextAlign = ContentAlignment.MiddleRight };
            _lblFundBalance.SetBounds(636, 7, 600, 34);
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnVoucher); btnBar.Controls.Add(btnDelete); btnBar.Controls.Add(_lblFundBalance);

            _gridTxn = NewGrid();
            _gridTxn.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) PrintSelectedVoucher(); };
            var gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            gridWrap.Controls.Add(_gridTxn);

            page.Controls.Add(gridWrap);
            page.Controls.Add(btnBar);
            page.Controls.Add(form);

            LoadTxnCombos();
            ReloadTxnCategories();
            LoadTransactions();
            return page;
        }

        // شماره سند را برای دوره‌ی فعلاً انتخاب‌شده دوباره محاسبه می‌کند
        // (بدون دوره انتخاب‌شده = شماره‌ی سراسری فقط برای پیش‌نمایش اولیه).
        private void RefreshDocNo()
        {
            _txnDocNo.Text = _repo.NextDocNo(ComboIntValue(_txnPeriod));
        }

        private void LoadTxnCombos()
        {
            BindCombo(_txnPeriod, _repo.GetPeriodsForCombo(), "PeriodID", "Display", true);
            _txnPeriod.SelectedIndexChanged += delegate { RefreshDocNo(); };
            BindCombo(_txnParty, _repo.GetPartiesForCombo(), "PartyID", "Display", true);
            BindCombo(_txnFund, _repo.GetFundsForCombo(), "FundID", "Display", true);
            _txnFund.SelectedIndexChanged += delegate { UpdateFundBalanceLabel(); };
        }

        private void ReloadTxnCategories()
        {
            bool income = _txnDirection.Text == "دریافت";
            BindCombo(_txnCategory, _repo.GetCategoriesForCombo(income), "CatID", "Display", true);

            // فیلدهای دلاری فقط برای دریافت معنی دارند
            bool showDollar = income;
            _txnDollar.Enabled = showDollar;
            _txnRate.Enabled = showDollar;
        }

        private void UpdateFundBalanceLabel()
        {
            if (_txnFund.SelectedValue == null || _txnFund.SelectedValue == DBNull.Value)
            {
                _lblFundBalance.Text = "";
                return;
            }
            int fundId = Convert.ToInt32(_txnFund.SelectedValue);
            double bal = _repo.GetFundBalance(fundId);
            _lblFundBalance.Text = "مانده فعلی صندوق «" + _txnFund.Text + "»:  " + bal.ToString("N0") + " افغانی";
        }

        // پاک‌سازی کامل فرم (دکمه «فرم جدید») — همه‌چیز خالی می‌شود، از جمله دوره
        // (پس شماره سند به حالت «بدون دوره» = پیش‌نمایش سراسری برمی‌گردد تا
        // کاربر دوباره دوره انتخاب کند و شماره‌ی واقعیِ همان دوره محاسبه شود).
        private void ResetTxnForm()
        {
            _txnPeriod.SelectedIndex = -1; _txnParty.SelectedIndex = -1; _txnFund.SelectedIndex = -1; _txnCategory.SelectedIndex = -1;
            RefreshDocNo();
            _txnDate.Text = PersianDateHelper.ToPersianDateString(DateTime.Today);
            _txnAmount.Value = 0; _txnQty.Text = ""; _txnDollar.Value = 0; _txnRate.Value = 0; _txnDesc.Text = "";
            _txnDirection.Focus();
        }

        // آموزش — «پاک‌سازی نرم» بعد از ثبت موفق: به‌درخواست کاربر، فیلدهای پایه
        // (دوره/طرف‌حساب/صندوق/دسته/نوع/تاریخ) که معمولاً برای چند سند پشت‌سرهم
        // یکسان‌اند حفظ می‌شوند و فقط شماره سند (به بعدیِ همین دوره) و فیلدهای
        // جزئیات (مبلغ/تعداد/دلاری/نرخ/توضیح) پاک می‌شوند تا دوباره‌کاری نشود.
        private void SoftResetTxnForm()
        {
            RefreshDocNo();
            _txnAmount.Value = 0; _txnQty.Text = ""; _txnDollar.Value = 0; _txnRate.Value = 0; _txnDesc.Text = "";
            _txnAmount.Focus();
        }

        private void SaveTransaction()
        {
            if (!SecurityContext.CanEdit()) { UiTheme.ShowWarning(this, "کاربر فقط مشاهده اجازه ثبت ندارد."); return; }
            double amount = (double)_txnAmount.Value;
            if (amount <= 0) { UiTheme.ShowWarning(this, "مبلغ معتبر وارد کنید."); _txnAmount.Focus(); return; }

            // اصول حسابداری: هر رویداد مالی باید به یک دوره مالی متصل باشد.
            int? period = ComboIntValue(_txnPeriod);
            if (period == null) { UiTheme.ShowWarning(this, "دوره مالی را انتخاب کنید. هر تراکنش باید به یک دوره متصل باشد."); _txnPeriod.Focus(); return; }
            if (!_repo.IsPeriodOpen(period.Value)) { UiTheme.ShowWarning(this, "این دوره مالی «بسته» است و امکان ثبت تراکنش جدید در آن وجود ندارد."); return; }

            if (_txnFund.SelectedValue == null || _txnFund.SelectedValue == DBNull.Value)
            { UiTheme.ShowWarning(this, "صندوق را انتخاب کنید."); return; }
            if (!_txnDate.MaskCompleted) { UiTheme.ShowWarning(this, "تاریخ را کامل وارد کنید (سال/ماه/روز)."); _txnDate.Focus(); return; }

            try
            {
                bool income = _txnDirection.Text == "دریافت";
                int? party = ComboIntValue(_txnParty);
                int fund = Convert.ToInt32(_txnFund.SelectedValue);
                int? cat = ComboIntValue(_txnCategory);
                double? dollar = (double)_txnDollar.Value; if (dollar <= 0) dollar = null;
                double? rate = (double)_txnRate.Value; if (rate <= 0) rate = null;

                // اطمینان از یکتا بودن شماره سند «در همین دوره مالی» (اگر پنجره
                // مدتی باز مانده و سند دیگری هم‌زمان در همین دوره ثبت شده باشد،
                // شماره بعدیِ همان دوره تازه گرفته می‌شود).
                string docNo = _txnDocNo.Text.Trim();
                if (_repo.DocNoExists(docNo, period)) docNo = _repo.NextDocNo(period);

                // هشدار (نه ممانعت) در صورت منفی شدن مانده صندوق بعد از پرداخت
                if (!income)
                {
                    double balAfter = _repo.GetFundBalance(fund) - amount;
                    if (balAfter < 0 &&
                        !UiTheme.ShowConfirm(this, "بعد از این پرداخت، مانده صندوق منفی می‌شود (" + balAfter.ToString("N0") + " افغانی).\nآیا ادامه می‌دهید؟", "هشدار کسری صندوق"))
                        return;
                }

                _repo.AddTransaction(docNo, _txnDate.Text, _txnDirection.Text,
                    period, party, fund, income ? "Income" : "Expense", cat, amount, _txnQty.Text.Trim(),
                    dollar, rate, _txnDesc.Text.Trim(), "");

                UiTheme.ShowSuccess(this, "تراکنش با شماره سند " + docNo + " ثبت شد.");
                SoftResetTxnForm();
                LoadTransactions();
                UpdateFundBalanceLabel();
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ثبت: " + ex.Message);
            }
        }

        // نمایش و چاپ فاکتور/سند تراکنش انتخاب‌شده در گرید
        private void PrintSelectedVoucher()
        {
            if (_gridTxn.CurrentRow == null || !_gridTxn.Columns.Contains("TxnID"))
            { UiTheme.ShowWarning(this, "ابتدا یک تراکنش را از جدول انتخاب کنید."); return; }
            object idv = _gridTxn.CurrentRow.Cells["TxnID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            try
            {
                new AccReports(_repo).PrintVoucher(this, Convert.ToInt32(idv));
            }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت فاکتور: " + ex.Message); }
        }

        private void DeleteSelectedTxn()
        {
            if (!SecurityContext.CanDelete()) { UiTheme.ShowWarning(this, "حذف فقط برای مدیر مجاز است."); return; }
            if (_gridTxn.CurrentRow == null || !_gridTxn.Columns.Contains("TxnID")) { UiTheme.ShowWarning(this, "ابتدا یک تراکنش را انتخاب کنید."); return; }
            object idv = _gridTxn.CurrentRow.Cells["TxnID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            if (!UiTheme.ShowConfirm(this, "این تراکنش حذف شود؟", "حذف تراکنش")) return;
            _repo.DeleteTransaction(Convert.ToInt32(idv));
            LoadTransactions();
            UpdateFundBalanceLabel();
        }

        private void LoadTransactions()
        {
            _gridTxn.DataSource = _repo.GetTransactions(null, null);
            if (_gridTxn.Columns.Contains("TxnID")) _gridTxn.Columns["TxnID"].Visible = false;
            FormatAmountColumn(_gridTxn, "مبلغ");
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: دوره مالی
        // ═══════════════════════════════════════════════════════════════════
        private DataGridView _gridPeriod;
        private TextBox _pYear, _pTitle;
        private NumericUpDown _pMonthFrom, _pMonthTo, _pOpening;
        private MaskedTextBox _pStart, _pEnd;
        private int _editingPeriodId;

        private TabPage BuildPeriodsTab()
        {
            var page = new TabPage("دوره مالی") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 100, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };

            _pYear = new TextBox { Text = "1404" };
            _pMonthFrom = new NumericUpDown { Minimum = 1, Maximum = 12, Value = 1 };
            _pMonthTo = new NumericUpDown { Minimum = 1, Maximum = 12, Value = 1 };
            _pTitle = new TextBox();
            _pOpening = NewAmountBox(0, true);
            _pStart = NewDateBox(); _pEnd = NewDateBox();

            form.Controls.Add(Field("سال", _pYear, 90));
            form.Controls.Add(Field("از برج", _pMonthFrom, 90));
            form.Controls.Add(Field("تا برج (چندماهه)", _pMonthTo, 130));
            form.Controls.Add(Field("عنوان دوره (اختیاری)", _pTitle, 220));
            form.Controls.Add(DateField("تاریخ شروع", _pStart));
            form.Controls.Add(DateField("تاریخ پایان", _pEnd));
            form.Controls.Add(Field("مانده ابتدای دوره", _pOpening, 160));

            // اگر «تا برج» کمتر از «از برج» شود، خودکار برابرش می‌کنیم (بازه معتبر)
            _pMonthFrom.ValueChanged += delegate { if (_pMonthTo.Value < _pMonthFrom.Value) _pMonthTo.Value = _pMonthFrom.Value; };

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره دوره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 130, 34); btnSave.Click += delegate { SavePeriod(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(152, 6, 90, 34); btnNew.Click += delegate { ClearPeriodForm(); };
            var btnCarry = UiTheme.CreateSecondaryButton("انتقال مانده از دوره قبل", "↺"); btnCarry.SetBounds(250, 6, 200, 34); btnCarry.Click += delegate { CarryForwardOpening(); };
            var btnClose = UiTheme.CreateButton("بستن دوره انتخاب‌شده", "🔒", UiTheme.Warning); btnClose.SetBounds(458, 6, 190, 34); btnClose.Click += delegate { CloseSelectedPeriod(); };
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnCarry); btnBar.Controls.Add(btnClose);

            _gridPeriod = NewGrid();
            _gridPeriod.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridPeriod.Columns.Contains("PeriodID")) return;
                var row = _gridPeriod.Rows[e.RowIndex];
                _editingPeriodId = Convert.ToInt32(row.Cells["PeriodID"].Value);
                _pYear.Text = row.Cells["سال"].Value?.ToString() ?? "";
                _pMonthFrom.Value = ParseMonth(row.Cells["از برج"].Value, 1);
                int monthTo = ParseMonth(row.Cells["تا برج"].Value, 0);
                _pMonthTo.Value = monthTo > 0 ? monthTo : _pMonthFrom.Value;
                _pTitle.Text = row.Cells["عنوان"].Value?.ToString() ?? "";
                _pOpening.Value = (decimal)ParseNum(row.Cells["مانده ابتدای دوره"].Value?.ToString());
            };
            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            gw.Controls.Add(_gridPeriod);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);

            LoadPeriods();
            return page;
        }

        private static int ParseMonth(object value, int fallback)
        {
            int m;
            return value != null && int.TryParse(value.ToString(), out m) ? m : fallback;
        }

        private void ClearPeriodForm()
        {
            _editingPeriodId = 0; _pMonthFrom.Value = 1; _pMonthTo.Value = 1; _pTitle.Text = ""; _pOpening.Value = 0;
        }

        private void SavePeriod()
        {
            int year = (int)ParseNum(_pYear.Text);
            int monthFrom = (int)_pMonthFrom.Value, monthTo = (int)_pMonthTo.Value;
            if (year < 1300) { UiTheme.ShowWarning(this, "سال معتبر وارد کنید."); return; }
            if (!_pStart.MaskCompleted || !_pEnd.MaskCompleted) { UiTheme.ShowWarning(this, "تاریخ شروع و پایان دوره را کامل وارد کنید (سال/ماه/روز)."); return; }

            string title;
            if (!string.IsNullOrWhiteSpace(_pTitle.Text)) title = _pTitle.Text.Trim();
            else if (monthTo != monthFrom) title = "برج " + monthFrom + " تا " + monthTo + " سال " + year;
            else title = "برج " + monthFrom + " سال " + year;

            double opening = (double)_pOpening.Value;
            if (_editingPeriodId == 0)
                _repo.AddPeriod(year, monthFrom, monthTo, title, _pStart.Text, _pEnd.Text, opening);
            else
            {
                if (!_repo.IsPeriodOpen(_editingPeriodId)) { UiTheme.ShowWarning(this, "دوره بسته‌شده قابل ویرایش نیست."); return; }
                _repo.UpdatePeriod(_editingPeriodId, year, monthFrom, monthTo, title, _pStart.Text, _pEnd.Text, opening);
            }
            UiTheme.ShowSuccess(this, "دوره مالی ذخیره شد.");
            ClearPeriodForm(); LoadPeriods();
        }

        private void CarryForwardOpening()
        {
            // مانده پایان آخرین دوره را به‌عنوان مانده ابتدای دوره جدید بگذارد
            DataTable dt = _repo.GetPeriodsForCombo();
            if (dt.Rows.Count == 0) { UiTheme.ShowWarning(this, "دوره‌ای برای انتقال مانده وجود ندارد."); return; }
            int lastId = Convert.ToInt32(dt.Rows[0]["PeriodID"]);
            double closing = _repo.GetPeriodClosing(lastId);
            double clamped = Math.Max((double)_pOpening.Minimum, Math.Min((double)_pOpening.Maximum, closing));
            _pOpening.Value = (decimal)Math.Round(clamped);
            UiTheme.ShowSuccess(this, "مانده پایان دوره «" + dt.Rows[0]["Display"] + "» ( " + closing.ToString("N0") + " ) به‌عنوان مانده ابتدای دوره جدید قرار گرفت.");
        }

        private void CloseSelectedPeriod()
        {
            if (_editingPeriodId == 0) { UiTheme.ShowWarning(this, "ابتدا یک دوره را از جدول انتخاب کنید."); return; }
            double closing = _repo.GetPeriodClosing(_editingPeriodId);
            if (!UiTheme.ShowConfirm(this, "با بستن دوره دیگر قابل ویرایش نیست.\nمانده پایان دوره: " + closing.ToString("N0") + " افغانی\nادامه می‌دهید؟", "بستن دوره")) return;
            _repo.SetPeriodStatus(_editingPeriodId, "بسته");
            LoadPeriods();
        }

        private void LoadPeriods()
        {
            _gridPeriod.DataSource = _repo.GetPeriods();
            if (_gridPeriod.Columns.Contains("PeriodID")) _gridPeriod.Columns["PeriodID"].Visible = false;
            FormatAmountColumn(_gridPeriod, "مانده ابتدای دوره");
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: صندوق
        // ═══════════════════════════════════════════════════════════════════
        private DataGridView _gridFund;
        private TextBox _fName;
        private NumericUpDown _fOpening;
        private ComboBox _fType;
        private int _editingFundId;

        private TabPage BuildFundsTab()
        {
            var page = new TabPage("صندوق") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 74, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2) };
            _fName = new TextBox(); _fOpening = NewAmountBox(0, true);
            _fType = NewCombo(); _fType.Items.AddRange(new object[] { "نقدی", "بانک", "کردیت", "مرکزی" });
            form.Controls.Add(Field("نام صندوق", _fName, 220));
            form.Controls.Add(Field("نوع", _fType, 140));
            form.Controls.Add(Field("مانده اولیه", _fOpening, 150));

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34); btnSave.Click += delegate { SaveFund(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34); btnNew.Click += delegate { _editingFundId = 0; _fName.Text = ""; _fOpening.Value = 0; _fType.SelectedIndex = -1; };
            var btnToggle = UiTheme.CreateSecondaryButton("فعال/غیرفعال", "⊙"); btnToggle.SetBounds(230, 6, 140, 34); btnToggle.Click += delegate { ToggleFund(); };
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnToggle);

            _gridFund = NewGrid();
            _gridFund.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridFund.Columns.Contains("FundID")) return;
                var row = _gridFund.Rows[e.RowIndex];
                _editingFundId = Convert.ToInt32(row.Cells["FundID"].Value);
                _fName.Text = row.Cells["نام صندوق"].Value?.ToString() ?? "";
                _fType.Text = row.Cells["نوع"].Value?.ToString() ?? "";
                _fOpening.Value = (decimal)ParseNum(row.Cells["مانده اولیه"].Value?.ToString());
            };
            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(_gridFund);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);
            LoadFunds();
            return page;
        }

        private void SaveFund()
        {
            if (string.IsNullOrWhiteSpace(_fName.Text)) { UiTheme.ShowWarning(this, "نام صندوق را وارد کنید."); return; }
            double opening = (double)_fOpening.Value;
            if (_editingFundId == 0) _repo.AddFund(_fName.Text.Trim(), _fType.Text.Trim(), opening);
            else _repo.UpdateFund(_editingFundId, _fName.Text.Trim(), _fType.Text.Trim(), opening);
            UiTheme.ShowSuccess(this, "صندوق ذخیره شد."); _editingFundId = 0; _fName.Text = ""; _fOpening.Value = 0; _fType.SelectedIndex = -1;
            LoadFunds();
        }

        private void ToggleFund()
        {
            if (_editingFundId == 0) { UiTheme.ShowWarning(this, "ابتدا یک صندوق را انتخاب کنید."); return; }
            _repo.ToggleFund(_editingFundId); LoadFunds();
        }

        private void LoadFunds()
        {
            _gridFund.DataSource = _repo.GetFunds();
            if (_gridFund.Columns.Contains("FundID")) _gridFund.Columns["FundID"].Visible = false;
            FormatAmountColumn(_gridFund, "مانده اولیه");
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: طرف حساب
        // ═══════════════════════════════════════════════════════════════════
        private DataGridView _gridParty;
        private TextBox _paName, _paPhone, _paNote;
        private ComboBox _paType;
        private int _editingPartyId;

        private TabPage BuildPartiesTab()
        {
            var page = new TabPage("طرف حساب") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 74, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2) };
            _paName = new TextBox(); _paPhone = new TextBox(); _paNote = new TextBox();
            _paType = NewCombo(); _paType.Items.AddRange(new object[] { "خیر", "دفتر مرکزی", "ولایت", "ولسوالی", "مرکز", "کارمند", "فروشنده", "شخص" });
            form.Controls.Add(Field("نام طرف حساب", _paName, 220));
            form.Controls.Add(Field("نوع", _paType, 150));
            form.Controls.Add(Field("شماره تماس", _paPhone, 150));
            form.Controls.Add(Field("توضیح", _paNote, 240));

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34); btnSave.Click += delegate { SaveParty(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34); btnNew.Click += delegate { _editingPartyId = 0; _paName.Text = ""; _paPhone.Text = ""; _paNote.Text = ""; _paType.SelectedIndex = -1; };
            var btnToggle = UiTheme.CreateSecondaryButton("فعال/غیرفعال", "⊙"); btnToggle.SetBounds(230, 6, 140, 34); btnToggle.Click += delegate { if (_editingPartyId > 0) { _repo.ToggleParty(_editingPartyId); LoadParties(); } };
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnToggle);

            _gridParty = NewGrid();
            _gridParty.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridParty.Columns.Contains("PartyID")) return;
                var row = _gridParty.Rows[e.RowIndex];
                _editingPartyId = Convert.ToInt32(row.Cells["PartyID"].Value);
                _paName.Text = row.Cells["نام طرف حساب"].Value?.ToString() ?? "";
                _paType.Text = row.Cells["نوع"].Value?.ToString() ?? "";
                _paPhone.Text = row.Cells["تماس"].Value?.ToString() ?? "";
                _paNote.Text = row.Cells["توضیح"].Value?.ToString() ?? "";
            };
            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(_gridParty);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);
            LoadParties();
            return page;
        }

        private void SaveParty()
        {
            if (string.IsNullOrWhiteSpace(_paName.Text)) { UiTheme.ShowWarning(this, "نام طرف حساب را وارد کنید."); return; }
            if (_editingPartyId == 0) _repo.AddParty(_paName.Text.Trim(), _paType.Text.Trim(), _paPhone.Text.Trim(), _paNote.Text.Trim());
            else _repo.UpdateParty(_editingPartyId, _paName.Text.Trim(), _paType.Text.Trim(), _paPhone.Text.Trim(), _paNote.Text.Trim());
            UiTheme.ShowSuccess(this, "طرف حساب ذخیره شد."); _editingPartyId = 0; _paName.Text = ""; _paPhone.Text = ""; _paNote.Text = ""; _paType.SelectedIndex = -1;
            LoadParties();
        }

        private void LoadParties()
        {
            _gridParty.DataSource = _repo.GetParties();
            if (_gridParty.Columns.Contains("PartyID")) _gridParty.Columns["PartyID"].Visible = false;
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: دسته‌بندی درآمد / هزینه (مشترک)
        // ═══════════════════════════════════════════════════════════════════
        private TabPage BuildCategoriesTab(bool income)
        {
            var page = new TabPage(income ? "دسته‌بندی درآمد" : "دسته‌بندی هزینه") { BackColor = UiTheme.Background };
            var grid = NewGrid();
            var txtName = new TextBox();
            int editingId = 0;

            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 74, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2) };
            form.Controls.Add(Field(income ? "عنوان دسته درآمد" : "عنوان دسته هزینه", txtName, 260));

            Action reload = delegate
            {
                grid.DataSource = _repo.GetCategories(income);
                if (grid.Columns.Contains("CatID")) grid.Columns["CatID"].Visible = false;
            };

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34);
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34);
            var btnToggle = UiTheme.CreateSecondaryButton("فعال/غیرفعال", "⊙"); btnToggle.SetBounds(230, 6, 140, 34);
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnToggle);

            btnSave.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(txtName.Text)) { UiTheme.ShowWarning(this, "عنوان را وارد کنید."); return; }
                if (editingId == 0) _repo.AddCategory(income, txtName.Text.Trim());
                else _repo.UpdateCategory(income, editingId, txtName.Text.Trim());
                editingId = 0; txtName.Text = ""; reload();
            };
            btnNew.Click += delegate { editingId = 0; txtName.Text = ""; };
            btnToggle.Click += delegate { if (editingId > 0) { _repo.ToggleCategory(income, editingId); reload(); } };
            grid.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !grid.Columns.Contains("CatID")) return;
                editingId = Convert.ToInt32(grid.Rows[e.RowIndex].Cells["CatID"].Value);
                txtName.Text = grid.Rows[e.RowIndex].Cells["عنوان"].Value?.ToString() ?? "";
            };

            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(grid);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);
            reload();
            return page;
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: شهریه ایتام (مطابق شیت «فرمت جزیی»)
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _stPeriod, _stSadat, _stSize;
        private TextBox _stProvince, _stDistrict, _stCenter;
        private NumericUpDown _stFamilyCount, _stOrphanCount, _stAmount;
        private DataGridView _gridStipend;
        private Label _lblStipendTotal;
        private int _editingStipendId;

        private TabPage BuildStipendTab()
        {
            var page = new TabPage("شهریه ایتام") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 120, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };

            _stPeriod = NewCombo(); _stProvince = new TextBox(); _stDistrict = new TextBox(); _stCenter = new TextBox();
            _stSadat = NewCombo(); _stSadat.Items.AddRange(new object[] { "عام", "سادات", "اهل سنت", "غیرحاضران" });
            _stSize = NewCombo(); for (int i = 1; i <= 8; i++) _stSize.Items.Add(i);
            _stFamilyCount = new NumericUpDown { Maximum = 100000 }; _stOrphanCount = new NumericUpDown { Maximum = 100000 }; _stAmount = NewAmountBox();

            form.Controls.Add(Field("دوره مالی", _stPeriod, 180));
            form.Controls.Add(Field("ولایت", _stProvince, 130));
            form.Controls.Add(Field("ولسوالی", _stDistrict, 130));
            form.Controls.Add(Field("مرکز", _stCenter, 150));
            form.Controls.Add(Field("نوع", _stSadat, 130));
            form.Controls.Add(Field("چند نفره", _stSize, 100));
            form.Controls.Add(Field("تعداد خانوار", _stFamilyCount, 110));
            form.Controls.Add(Field("تعداد یتیم", _stOrphanCount, 110));
            form.Controls.Add(Field("مبلغ شهریه هر خانواده", _stAmount, 160));

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34); btnSave.Click += delegate { SaveStipend(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34); btnNew.Click += delegate { ClearStipendForm(); };
            var btnDelete = UiTheme.CreateButton("حذف", "✕", UiTheme.Danger); btnDelete.SetBounds(230, 6, 100, 34); btnDelete.Click += delegate { DeleteSelectedStipend(); };
            // آموزش — به‌درخواست کاربر: هر رویداد مالی (از جمله ردیف شهریه) باید
            // یک فاکتور/رسید رسمی قابل چاپ داشته باشد.
            var btnVoucherSt = UiTheme.CreateButton("چاپ رسید شهریه", "🧾", UiTheme.Primary); btnVoucherSt.SetBounds(340, 6, 150, 34);
            btnVoucherSt.Click += delegate { PrintSelectedStipendVoucher(); };
            _lblStipendTotal = new Label { AutoSize = false, Font = UiTheme.FontBold(11F), ForeColor = UiTheme.PrimaryDark, TextAlign = ContentAlignment.MiddleRight };
            _lblStipendTotal.SetBounds(500, 6, 500, 34);
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnDelete); btnBar.Controls.Add(btnVoucherSt); btnBar.Controls.Add(_lblStipendTotal);

            _gridStipend = NewGrid();
            _gridStipend.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) PrintSelectedStipendVoucher(); };
            _gridStipend.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridStipend.Columns.Contains("StipendID")) return;
                var row = _gridStipend.Rows[e.RowIndex];
                _editingStipendId = Convert.ToInt32(row.Cells["StipendID"].Value);
                _stProvince.Text = row.Cells["ولایت"].Value?.ToString() ?? "";
                _stDistrict.Text = row.Cells["ولسوالی"].Value?.ToString() ?? "";
                _stCenter.Text = row.Cells["مرکز"].Value?.ToString() ?? "";
                _stSadat.Text = row.Cells["نوع"].Value?.ToString() ?? "";
                _stSize.Text = row.Cells["چند نفره"].Value?.ToString() ?? "";
                _stFamilyCount.Value = (decimal)ParseNum(row.Cells["تعداد خانوار"].Value?.ToString());
                _stOrphanCount.Value = (decimal)ParseNum(row.Cells["تعداد یتیم"].Value?.ToString());
                _stAmount.Value = (decimal)ParseNum(row.Cells["مبلغ شهریه"].Value?.ToString());
            };

            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(_gridStipend);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);

            BindCombo(_stPeriod, _repo.GetPeriodsForCombo(), "PeriodID", "Display", true);
            _stPeriod.SelectedIndexChanged += delegate { LoadStipends(); };
            LoadStipends();
            return page;
        }

        // پاک‌سازی کامل (دکمه «جدید»)
        private void ClearStipendForm()
        {
            _editingStipendId = 0; _stProvince.Text = ""; _stDistrict.Text = ""; _stCenter.Text = "";
            _stSadat.SelectedIndex = -1; _stSize.SelectedIndex = -1; _stFamilyCount.Value = 0; _stOrphanCount.Value = 0; _stAmount.Value = 0;
        }

        // پاک‌سازی نرم بعد از ثبت: ولایت/ولسوالی/مرکز/نوع حفظ می‌شوند (چون
        // معمولاً برای ردیف‌های متوالی یک منطقه یکسان‌اند)، فقط چندنفره/تعداد/مبلغ
        // پاک می‌شوند تا کاربر سریع ردیف بعدی همان منطقه را ثبت کند.
        private void SoftResetStipendForm()
        {
            _editingStipendId = 0;
            _stSize.SelectedIndex = -1; _stFamilyCount.Value = 0; _stOrphanCount.Value = 0; _stAmount.Value = 0;
        }

        private void SaveStipend()
        {
            int? period = ComboIntValue(_stPeriod);
            if (period == null) { UiTheme.ShowWarning(this, "دوره مالی را انتخاب کنید. هر ردیف شهریه باید به یک دوره متصل باشد."); _stPeriod.Focus(); return; }
            if (!_repo.IsPeriodOpen(period.Value)) { UiTheme.ShowWarning(this, "این دوره مالی «بسته» است و امکان ثبت شهریه در آن وجود ندارد."); return; }
            if (string.IsNullOrWhiteSpace(_stSadat.Text) || _stSize.SelectedIndex < 0)
            { UiTheme.ShowWarning(this, "نوع و چندنفره را انتخاب کنید."); return; }

            int size = (int)ParseNum(_stSize.Text);
            int familyCount = (int)_stFamilyCount.Value;
            int orphanCount = (int)_stOrphanCount.Value;
            double amount = (double)_stAmount.Value;

            if (_editingStipendId == 0)
                _repo.AddStipend(period, _stProvince.Text.Trim(), _stDistrict.Text.Trim(), _stCenter.Text.Trim(), _stSadat.Text, size, familyCount, orphanCount, amount);
            else
                _repo.UpdateStipend(_editingStipendId, _stProvince.Text.Trim(), _stDistrict.Text.Trim(), _stCenter.Text.Trim(), _stSadat.Text, size, familyCount, orphanCount, amount);

            UiTheme.ShowSuccess(this, "شهریه ذخیره شد.");
            SoftResetStipendForm();
            LoadStipends();
        }

        private void DeleteSelectedStipend()
        {
            if (_editingStipendId == 0) { UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید."); return; }
            if (!UiTheme.ShowConfirm(this, "این ردیف شهریه حذف شود؟", "حذف شهریه")) return;
            _repo.DeleteStipend(_editingStipendId);
            ClearStipendForm();
            LoadStipends();
        }

        private void PrintSelectedStipendVoucher()
        {
            if (_gridStipend.CurrentRow == null || !_gridStipend.Columns.Contains("StipendID"))
            { UiTheme.ShowWarning(this, "ابتدا یک ردیف شهریه را از جدول انتخاب کنید."); return; }
            object idv = _gridStipend.CurrentRow.Cells["StipendID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            try { new AccReports(_repo).PrintStipendVoucher(this, Convert.ToInt32(idv)); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت رسید: " + ex.Message); }
        }

        private void LoadStipends()
        {
            int? period = ComboIntValue(_stPeriod);
            _gridStipend.DataSource = _repo.GetStipends(period);
            if (_gridStipend.Columns.Contains("StipendID")) _gridStipend.Columns["StipendID"].Visible = false;
            if (_gridStipend.Columns.Contains("PeriodID")) _gridStipend.Columns["PeriodID"].Visible = false;
            FormatAmountColumn(_gridStipend, "مبلغ شهریه");
            FormatAmountColumn(_gridStipend, "جمع پرداختی");

            double total = 0;
            var dt = (DataTable)_gridStipend.DataSource;
            foreach (DataRow r in dt.Rows) total += Convert.ToDouble(r["جمع پرداختی"]);
            _lblStipendTotal.Text = "جمع کل پرداختی شهریه:  " + total.ToString("N0") + "  افغانی";
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: حقوق کارکنان
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _saPeriod;
        private TextBox _saName, _saPosition, _saNote;
        private NumericUpDown _saAmount;
        private DataGridView _gridSalary;
        private Label _lblSalaryTotal;
        private int _editingSalaryId;

        private TabPage BuildSalaryTab()
        {
            var page = new TabPage("حقوق کارکنان") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 78, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2) };

            _saPeriod = NewCombo(); _saName = new TextBox(); _saPosition = new TextBox(); _saAmount = NewAmountBox(); _saNote = new TextBox();
            form.Controls.Add(Field("دوره مالی", _saPeriod, 180));
            form.Controls.Add(Field("نام", _saName, 180));
            form.Controls.Add(Field("سمت", _saPosition, 150));
            form.Controls.Add(Field("مبلغ", _saAmount, 130));
            form.Controls.Add(Field("توضیح", _saNote, 220));

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34); btnSave.Click += delegate { SaveSalary(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34); btnNew.Click += delegate { ClearSalaryForm(); };
            var btnDelete = UiTheme.CreateButton("حذف", "✕", UiTheme.Danger); btnDelete.SetBounds(230, 6, 100, 34); btnDelete.Click += delegate { DeleteSelectedSalary(); };
            var btnVoucherSa = UiTheme.CreateButton("چاپ فیش حقوق", "🧾", UiTheme.Primary); btnVoucherSa.SetBounds(340, 6, 150, 34);
            btnVoucherSa.Click += delegate { PrintSelectedSalaryVoucher(); };
            _lblSalaryTotal = new Label { AutoSize = false, Font = UiTheme.FontBold(11F), ForeColor = UiTheme.PrimaryDark, TextAlign = ContentAlignment.MiddleRight };
            _lblSalaryTotal.SetBounds(500, 6, 500, 34);
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnDelete); btnBar.Controls.Add(btnVoucherSa); btnBar.Controls.Add(_lblSalaryTotal);

            _gridSalary = NewGrid();
            _gridSalary.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) PrintSelectedSalaryVoucher(); };
            _gridSalary.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridSalary.Columns.Contains("SalaryID")) return;
                var row = _gridSalary.Rows[e.RowIndex];
                _editingSalaryId = Convert.ToInt32(row.Cells["SalaryID"].Value);
                _saName.Text = row.Cells["نام"].Value?.ToString() ?? "";
                _saPosition.Text = row.Cells["سمت"].Value?.ToString() ?? "";
                _saAmount.Value = (decimal)ParseNum(row.Cells["مبلغ"].Value?.ToString());
                _saNote.Text = row.Cells["توضیح"].Value?.ToString() ?? "";
            };

            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(_gridSalary);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);

            BindCombo(_saPeriod, _repo.GetPeriodsForCombo(), "PeriodID", "Display", true);
            _saPeriod.SelectedIndexChanged += delegate { LoadSalaries(); };
            LoadSalaries();
            return page;
        }

        private void ClearSalaryForm() { _editingSalaryId = 0; _saName.Text = ""; _saPosition.Text = ""; _saAmount.Value = 0; _saNote.Text = ""; }

        private void SaveSalary()
        {
            int? period = ComboIntValue(_saPeriod);
            if (period == null) { UiTheme.ShowWarning(this, "دوره مالی را انتخاب کنید. هر ردیف حقوق باید به یک دوره متصل باشد."); _saPeriod.Focus(); return; }
            if (!_repo.IsPeriodOpen(period.Value)) { UiTheme.ShowWarning(this, "این دوره مالی «بسته» است و امکان ثبت حقوق در آن وجود ندارد."); return; }
            if (string.IsNullOrWhiteSpace(_saName.Text)) { UiTheme.ShowWarning(this, "نام کارمند را وارد کنید."); return; }
            double amount = (double)_saAmount.Value;
            if (_editingSalaryId == 0) _repo.AddSalary(period, _saName.Text.Trim(), _saPosition.Text.Trim(), amount, _saNote.Text.Trim());
            else _repo.UpdateSalary(_editingSalaryId, _saName.Text.Trim(), _saPosition.Text.Trim(), amount, _saNote.Text.Trim());
            // دوره مالی حفظ می‌شود؛ فقط فیلدهای هر کارمند پاک می‌شوند.
            UiTheme.ShowSuccess(this, "حقوق ذخیره شد."); ClearSalaryForm(); LoadSalaries();
        }

        private void DeleteSelectedSalary()
        {
            if (_editingSalaryId == 0) { UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید."); return; }
            if (!UiTheme.ShowConfirm(this, "این ردیف حقوق حذف شود؟", "حذف حقوق")) return;
            _repo.DeleteSalary(_editingSalaryId); ClearSalaryForm(); LoadSalaries();
        }

        private void PrintSelectedSalaryVoucher()
        {
            if (_gridSalary.CurrentRow == null || !_gridSalary.Columns.Contains("SalaryID"))
            { UiTheme.ShowWarning(this, "ابتدا یک ردیف حقوق را از جدول انتخاب کنید."); return; }
            object idv = _gridSalary.CurrentRow.Cells["SalaryID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            try { new AccReports(_repo).PrintSalaryVoucher(this, Convert.ToInt32(idv)); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت فیش: " + ex.Message); }
        }

        private void LoadSalaries()
        {
            int? period = ComboIntValue(_saPeriod);
            _gridSalary.DataSource = _repo.GetSalaries(period);
            if (_gridSalary.Columns.Contains("SalaryID")) _gridSalary.Columns["SalaryID"].Visible = false;
            if (_gridSalary.Columns.Contains("PeriodID")) _gridSalary.Columns["PeriodID"].Visible = false;
            FormatAmountColumn(_gridSalary, "مبلغ");

            double total = 0;
            var dt = (DataTable)_gridSalary.DataSource;
            foreach (DataRow r in dt.Rows) total += Convert.ToDouble(r["مبلغ"]);
            _lblSalaryTotal.Text = "جمع کل حقوق:  " + total.ToString("N0") + "  افغانی";
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: هزینه‌های جاری (مطابق شیت «حساب جاری»)
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _exPeriod, _exCategory;
        private TextBox _exDesc, _exQty, _exDocNo;
        private NumericUpDown _exPrice;
        private MaskedTextBox _exDate;
        private DataGridView _gridExpense;
        private Label _lblExpenseTotal;
        private int _editingExpenseId;

        private TabPage BuildExpenseItemsTab()
        {
            var page = new TabPage("هزینه‌های جاری") { BackColor = UiTheme.Background };
            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 110, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2), AutoScroll = true };

            _exPeriod = NewCombo(); _exCategory = NewCombo(); _exDesc = new TextBox(); _exQty = new TextBox(); _exPrice = NewAmountBox(); _exDocNo = new TextBox();
            _exDate = NewDateBox();

            form.Controls.Add(Field("دوره مالی", _exPeriod, 170));
            form.Controls.Add(Field("دسته‌بندی", _exCategory, 180));
            form.Controls.Add(Field("شرح", _exDesc, 320));
            form.Controls.Add(Field("تعداد/مقدار", _exQty, 120));
            form.Controls.Add(Field("قیمت", _exPrice, 120));
            form.Controls.Add(Field("شماره سند", _exDocNo, 110));
            form.Controls.Add(DateField("تاریخ", _exDate));

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.CardBack };
            var btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success); btnSave.SetBounds(14, 6, 110, 34); btnSave.Click += delegate { SaveExpenseItem(); };
            var btnNew = UiTheme.CreateSecondaryButton("جدید", "＋"); btnNew.SetBounds(132, 6, 90, 34); btnNew.Click += delegate { ClearExpenseForm(); };
            var btnDelete = UiTheme.CreateButton("حذف", "✕", UiTheme.Danger); btnDelete.SetBounds(230, 6, 100, 34); btnDelete.Click += delegate { DeleteSelectedExpenseItem(); };
            var btnVoucherEx = UiTheme.CreateButton("چاپ سند هزینه", "🧾", UiTheme.Primary); btnVoucherEx.SetBounds(340, 6, 150, 34);
            btnVoucherEx.Click += delegate { PrintSelectedExpenseVoucher(); };
            _lblExpenseTotal = new Label { AutoSize = false, Font = UiTheme.FontBold(11F), ForeColor = UiTheme.PrimaryDark, TextAlign = ContentAlignment.MiddleRight };
            _lblExpenseTotal.SetBounds(500, 6, 500, 34);
            btnBar.Controls.Add(btnSave); btnBar.Controls.Add(btnNew); btnBar.Controls.Add(btnDelete); btnBar.Controls.Add(btnVoucherEx); btnBar.Controls.Add(_lblExpenseTotal);

            _gridExpense = NewGrid();
            _gridExpense.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) PrintSelectedExpenseVoucher(); };
            _gridExpense.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || !_gridExpense.Columns.Contains("ItemID")) return;
                var row = _gridExpense.Rows[e.RowIndex];
                _editingExpenseId = Convert.ToInt32(row.Cells["ItemID"].Value);
                _exCategory.Text = row.Cells["دسته‌بندی"].Value?.ToString() ?? "";
                _exDesc.Text = row.Cells["شرح"].Value?.ToString() ?? "";
                _exQty.Text = row.Cells["تعداد/مقدار"].Value?.ToString() ?? "";
                _exPrice.Value = (decimal)ParseNum(row.Cells["قیمت"].Value?.ToString());
                _exDocNo.Text = row.Cells["شماره سند"].Value?.ToString() ?? "";
            };

            var gw = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; gw.Controls.Add(_gridExpense);
            page.Controls.Add(gw); page.Controls.Add(btnBar); page.Controls.Add(form);

            BindCombo(_exPeriod, _repo.GetPeriodsForCombo(), "PeriodID", "Display", true);
            BindCombo(_exCategory, _repo.GetCategoriesForCombo(false), "CatID", "Display", true);
            _exPeriod.SelectedIndexChanged += delegate { LoadExpenseItems(); };
            LoadExpenseItems();
            return page;
        }

        // پاک‌سازی کامل (دکمه «جدید»)
        private void ClearExpenseForm()
        {
            _editingExpenseId = 0; _exCategory.SelectedIndex = -1; _exDesc.Text = ""; _exQty.Text = ""; _exPrice.Value = 0; _exDocNo.Text = "";
            _exDate.Text = PersianDateHelper.ToPersianDateString(DateTime.Today);
        }

        // پاک‌سازی نرم بعد از ثبت: دوره/دسته‌بندی/تاریخ حفظ می‌شوند، فقط
        // شرح/تعداد/قیمت/شماره سند پاک می‌شوند تا ثبت اقلام متوالی سریع باشد.
        private void SoftResetExpenseForm()
        {
            _editingExpenseId = 0; _exDesc.Text = ""; _exQty.Text = ""; _exPrice.Value = 0; _exDocNo.Text = "";
        }

        private void SaveExpenseItem()
        {
            int? period = ComboIntValue(_exPeriod);
            if (period == null) { UiTheme.ShowWarning(this, "دوره مالی را انتخاب کنید. هر قلم هزینه باید به یک دوره متصل باشد."); _exPeriod.Focus(); return; }
            if (!_repo.IsPeriodOpen(period.Value)) { UiTheme.ShowWarning(this, "این دوره مالی «بسته» است و امکان ثبت هزینه در آن وجود ندارد."); return; }
            if (string.IsNullOrWhiteSpace(_exDesc.Text)) { UiTheme.ShowWarning(this, "شرح هزینه را وارد کنید."); return; }
            if (!_exDate.MaskCompleted) { UiTheme.ShowWarning(this, "تاریخ را کامل وارد کنید (سال/ماه/روز)."); _exDate.Focus(); return; }
            int? cat = ComboIntValue(_exCategory);
            double price = (double)_exPrice.Value;

            if (_editingExpenseId == 0)
                _repo.AddExpenseItem(period, cat, _exCategory.Text, _exDesc.Text.Trim(), _exQty.Text.Trim(), price, _exDocNo.Text.Trim(), _exDate.Text);
            else
                _repo.UpdateExpenseItem(_editingExpenseId, cat, _exCategory.Text, _exDesc.Text.Trim(), _exQty.Text.Trim(), price, _exDocNo.Text.Trim(), _exDate.Text);

            UiTheme.ShowSuccess(this, "هزینه ذخیره شد."); SoftResetExpenseForm(); LoadExpenseItems();
        }

        private void DeleteSelectedExpenseItem()
        {
            if (_editingExpenseId == 0) { UiTheme.ShowWarning(this, "ابتدا یک ردیف را انتخاب کنید."); return; }
            if (!UiTheme.ShowConfirm(this, "این هزینه حذف شود؟", "حذف هزینه")) return;
            _repo.DeleteExpenseItem(_editingExpenseId); ClearExpenseForm(); LoadExpenseItems();
        }

        private void PrintSelectedExpenseVoucher()
        {
            if (_gridExpense.CurrentRow == null || !_gridExpense.Columns.Contains("ItemID"))
            { UiTheme.ShowWarning(this, "ابتدا یک قلم هزینه را از جدول انتخاب کنید."); return; }
            object idv = _gridExpense.CurrentRow.Cells["ItemID"].Value;
            if (idv == null || idv == DBNull.Value) return;
            try { new AccReports(_repo).PrintExpenseVoucher(this, Convert.ToInt32(idv)); }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت سند: " + ex.Message); }
        }

        private void LoadExpenseItems()
        {
            int? period = ComboIntValue(_exPeriod);
            _gridExpense.DataSource = _repo.GetExpenseItems(period);
            if (_gridExpense.Columns.Contains("ItemID")) _gridExpense.Columns["ItemID"].Visible = false;
            if (_gridExpense.Columns.Contains("PeriodID")) _gridExpense.Columns["PeriodID"].Visible = false;
            FormatAmountColumn(_gridExpense, "قیمت");

            double total = 0;
            var dt = (DataTable)_gridExpense.DataSource;
            foreach (DataRow r in dt.Rows) total += Convert.ToDouble(r["قیمت"]);
            _lblExpenseTotal.Text = "جمع کل هزینه‌های جاری:  " + total.ToString("N0") + "  افغانی";
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: گزارش‌ها (۸ گزارش با چاپ/PDF/اکسل)
        // ═══════════════════════════════════════════════════════════════════
        private ComboBox _repPeriod, _repFund, _repParty;

        private TabPage BuildReportsTab()
        {
            var page = new TabPage("گزارش‌ها") { BackColor = UiTheme.Background };
            var filterPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 68, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, BackColor = UiTheme.CardBack, Padding = new Padding(10, 6, 10, 2) };
            _repPeriod = NewCombo(); _repFund = NewCombo(); _repParty = NewCombo();
            filterPanel.Controls.Add(Field("دوره مالی گزارش", _repPeriod, 200));
            filterPanel.Controls.Add(Field("صندوق (برای دفتر صندوق)", _repFund, 200));
            filterPanel.Controls.Add(Field("طرف حساب (برای دفتر طرف حساب)", _repParty, 220));

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Padding = new Padding(16), AutoScroll = true };

            AddReportButton(flow, "۱) صورت حساب کلی", delegate { RunReport(1); });
            AddReportButton(flow, "۲) صورت حساب جزیی شهریه", delegate { RunReport(2); });
            AddReportButton(flow, "۳) صورت حساب هزینه‌ها", delegate { RunReport(3); });
            AddReportButton(flow, "۴) صورت حساب حقوق", delegate { RunReport(4); });
            AddReportButton(flow, "۵) صورت حساب دریافت بودجه", delegate { RunReport(5); });
            AddReportButton(flow, "۶) دفتر صندوق", delegate { RunReport(6); });
            AddReportButton(flow, "۷) دفتر طرف حساب", delegate { RunReport(7); });
            AddReportButton(flow, "۸) ریز تراکنش‌های دوره", delegate { RunReport(8); });

            page.Controls.Add(flow);
            page.Controls.Add(filterPanel);

            BindCombo(_repPeriod, _repo.GetPeriodsForCombo(), "PeriodID", "Display", true);
            BindCombo(_repFund, _repo.GetFundsForCombo(), "FundID", "Display", true);
            BindCombo(_repParty, _repo.GetPartiesForCombo(), "PartyID", "Display", true);
            return page;
        }

        private void AddReportButton(FlowLayoutPanel flow, string title, EventHandler onClick)
        {
            var card = new Panel { Width = 260, Height = 110, Margin = new Padding(10), BackColor = UiTheme.CardBack, BorderStyle = BorderStyle.FixedSingle };
            var lbl = new Label { Text = title, AutoSize = false, Dock = DockStyle.Top, Height = 44, TextAlign = ContentAlignment.MiddleCenter, Font = UiTheme.FontBold(10.5F), ForeColor = UiTheme.TextDark };
            var btnPrint = UiTheme.CreateButton("پیش‌نمایش/چاپ/PDF", "🖨", UiTheme.Primary); btnPrint.SetBounds(10, 50, 240, 26); btnPrint.Click += onClick;
            var btnExcel = UiTheme.CreateSecondaryButton("خروجی اکسل", "📊"); btnExcel.SetBounds(10, 80, 240, 26);
            btnExcel.Tag = title;
            btnExcel.Click += delegate { RunReportExcel(title); };
            card.Controls.Add(lbl); card.Controls.Add(btnPrint); card.Controls.Add(btnExcel);
            flow.Controls.Add(card);
        }

        private int? SelectedReportPeriod { get { return ComboIntValue(_repPeriod); } }
        private int? SelectedReportFund { get { return ComboIntValue(_repFund); } }
        private int? SelectedReportParty { get { return ComboIntValue(_repParty); } }

        private void RunReport(int reportNo)
        {
            try
            {
                var reports = new AccReports(_repo);
                switch (reportNo)
                {
                    case 1: reports.PrintGeneralStatement(this, SelectedReportPeriod); break;
                    case 2: reports.PrintDetailedStatement(this, SelectedReportPeriod); break;
                    case 3: reports.PrintExpenseStatement(this, SelectedReportPeriod); break;
                    case 4: reports.PrintSalaryStatement(this, SelectedReportPeriod); break;
                    case 5: reports.PrintBudgetReceiptStatement(this, SelectedReportPeriod); break;
                    case 6:
                        if (SelectedReportFund == null) { UiTheme.ShowWarning(this, "ابتدا صندوق را انتخاب کنید."); return; }
                        reports.PrintFundLedger(this, SelectedReportFund.Value, SelectedReportPeriod); break;
                    case 7:
                        if (SelectedReportParty == null) { UiTheme.ShowWarning(this, "ابتدا طرف حساب را انتخاب کنید."); return; }
                        reports.PrintPartyLedger(this, SelectedReportParty.Value); break;
                    case 8:
                        if (SelectedReportPeriod == null) { UiTheme.ShowWarning(this, "ابتدا دوره مالی را انتخاب کنید."); return; }
                        reports.PrintPeriodDetail(this, SelectedReportPeriod.Value); break;
                }
            }
            catch (Exception ex) { UiTheme.ShowError(this, "خطا در تولید گزارش: " + ex.Message); }
        }

        private void RunReportExcel(string title)
        {
            using (var sfd = new SaveFileDialog { Filter = "فایل اکسل|*.xlsx", FileName = title.Replace("/", "-") + ".xlsx" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var reports = new AccReports(_repo);
                    if (title.StartsWith("۱")) reports.ExportGeneralStatementExcel(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۲")) reports.ExportDetailedStatementExcel(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۳")) reports.ExportExpenseStatementExcel(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۴")) reports.ExportSalaryStatementExcel(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۵")) reports.ExportBudgetReceiptExcel(sfd.FileName, SelectedReportPeriod);
                    else if (title.StartsWith("۶"))
                    {
                        if (SelectedReportFund == null) { UiTheme.ShowWarning(this, "ابتدا صندوق را انتخاب کنید."); return; }
                        reports.ExportFundLedgerExcel(sfd.FileName, SelectedReportFund.Value, SelectedReportPeriod);
                    }
                    else if (title.StartsWith("۷"))
                    {
                        if (SelectedReportParty == null) { UiTheme.ShowWarning(this, "ابتدا طرف حساب را انتخاب کنید."); return; }
                        reports.ExportPartyLedgerExcel(sfd.FileName, SelectedReportParty.Value);
                    }
                    else if (title.StartsWith("۸"))
                    {
                        if (SelectedReportPeriod == null) { UiTheme.ShowWarning(this, "ابتدا دوره مالی را انتخاب کنید."); return; }
                        reports.ExportPeriodDetailExcel(sfd.FileName, SelectedReportPeriod.Value);
                    }
                    UiTheme.ShowSuccess(this, "فایل اکسل ذخیره شد:\n" + sfd.FileName);
                }
                catch (Exception ex) { UiTheme.ShowError(this, "خطا در ساخت اکسل: " + ex.Message); }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // تب: تنظیمات گزارش (سربرگ/پاورقی/امضاها/مهر)
        // ═══════════════════════════════════════════════════════════════════
        private TextBox _setOrgName, _setHeader, _setFooter;
        private TextBox _setLogoPath, _setStampPath, _setAccSign, _setMgrSign, _setPrepSign;

        private TabPage BuildSettingsTab()
        {
            var page = new TabPage("تنظیمات گزارش") { BackColor = UiTheme.Background };
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, AutoScroll = true, Padding = new Padding(14) };

            _setOrgName = new TextBox(); _setHeader = new TextBox(); _setFooter = new TextBox();
            _setLogoPath = new TextBox { ReadOnly = true }; _setStampPath = new TextBox { ReadOnly = true };
            _setAccSign = new TextBox { ReadOnly = true }; _setMgrSign = new TextBox { ReadOnly = true }; _setPrepSign = new TextBox { ReadOnly = true };

            panel.Controls.Add(Field("نام مؤسسه", _setOrgName, 400));
            panel.Controls.Add(Field("متن سربرگ", _setHeader, 400));
            panel.Controls.Add(Field("متن پاورقی", _setFooter, 400));
            panel.Controls.Add(BuildImagePicker("مسیر لوگو", _setLogoPath));
            panel.Controls.Add(BuildImagePicker("مسیر تصویر مهر", _setStampPath));
            panel.Controls.Add(BuildImagePicker("امضای حسابدار", _setAccSign));
            panel.Controls.Add(BuildImagePicker("امضای مسئول", _setMgrSign));
            panel.Controls.Add(BuildImagePicker("امضای تهیه‌کننده", _setPrepSign));

            var btnSave = UiTheme.CreateButton("ذخیره تنظیمات", "✔", UiTheme.Success);
            btnSave.SetBounds(0, 0, 180, 36);
            btnSave.Margin = new Padding(6, 20, 6, 4);
            btnSave.Click += delegate { SaveAccSettings(); };
            panel.Controls.Add(btnSave);

            page.Controls.Add(panel);
            LoadAccSettings();
            return page;
        }

        private Panel BuildImagePicker(string label, TextBox pathBox)
        {
            var p = new Panel { Width = 460, Height = 58, Margin = new Padding(6, 4, 6, 4) };
            var l = new Label { Text = label, AutoSize = false, Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark };
            pathBox.Width = 330; pathBox.Height = 28; pathBox.Location = new Point(90, 24);
            var btn = UiTheme.CreateSecondaryButton("انتخاب...", "🖼"); btn.SetBounds(0, 22, 80, 30);
            btn.Click += delegate
            {
                using (var ofd = new OpenFileDialog { Filter = "تصویر|*.jpg;*.jpeg;*.png" })
                    if (ofd.ShowDialog(this) == DialogResult.OK) pathBox.Text = ofd.FileName;
            };
            p.Controls.Add(l); p.Controls.Add(pathBox); p.Controls.Add(btn);
            return p;
        }

        private void LoadAccSettings()
        {
            _setOrgName.Text = _repo.GetSetting("OrgName");
            _setHeader.Text = _repo.GetSetting("HeaderText");
            _setFooter.Text = _repo.GetSetting("FooterText");
            _setLogoPath.Text = _repo.GetSetting("LogoPath");
            _setStampPath.Text = _repo.GetSetting("StampPath");
            _setAccSign.Text = _repo.GetSetting("AccountantSignature");
            _setMgrSign.Text = _repo.GetSetting("ManagerSignature");
            _setPrepSign.Text = _repo.GetSetting("PreparerSignature");
        }

        private void SaveAccSettings()
        {
            _repo.SetSetting("OrgName", _setOrgName.Text.Trim());
            _repo.SetSetting("HeaderText", _setHeader.Text.Trim());
            _repo.SetSetting("FooterText", _setFooter.Text.Trim());
            _repo.SetSetting("LogoPath", _setLogoPath.Text.Trim());
            _repo.SetSetting("StampPath", _setStampPath.Text.Trim());
            _repo.SetSetting("AccountantSignature", _setAccSign.Text.Trim());
            _repo.SetSetting("ManagerSignature", _setMgrSign.Text.Trim());
            _repo.SetSetting("PreparerSignature", _setPrepSign.Text.Trim());
            UiTheme.ShowSuccess(this, "تنظیمات گزارش ذخیره شد.");
        }

        // قالب‌بندی ستون مبلغ به‌صورت سه‌رقمی
        private void FormatAmountColumn(DataGridView grid, string col)
        {
            if (grid.Columns.Contains(col))
            {
                grid.Columns[col].DefaultCellStyle.Format = "N0";
                grid.Columns[col].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }
        }
    }
}

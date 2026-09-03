using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement
{
    public class FrmFinance : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        private TextBox txtSearch;
        private ComboBox cmbServiceStatus;
        private DataGridView dgvCases;
        private DataGridView dgvAssistance;
        private DataGridView dgvMonthly;
        private DataGridView dgvWithoutAssistance;
        private NumericUpDown numAmount;
        private Helpers.PersianDatePicker dtpAssistanceDate;
        private RadioButton rdoAssistanceCash;
        private RadioButton rdoAssistanceNonCash;
        private Panel pnlAmount;
        private Panel pnlPackage;
        private ComboBox cmbPackage;
        private ComboBox cmbRequestType;
        private TextBox txtDescription;
        private TextBox txtProgramName;
        private TextBox txtPickupLocation;
        private TextBox txtCoordinatorPhone;
        private Label lblSelectedCase;
        private Label lblTotal;

        private readonly AssistanceReceiptIntegration.AssistancePackageRepository _packageRepo =
            new AssistanceReceiptIntegration.AssistancePackageRepository();

        private int selectedCaseId;

        // دکمه‌های این فرم در سه متدِ جدا ساخته می‌شوند و متغیرِ محلی‌اند، پس
        // سازنده‌ی میان‌بُر یک‌بار اینجا ساخته و در هر سه جا استفاده می‌شود.
        private Helpers.FormShortcuts.Builder _shortcuts;

        public FrmFinance()
        {
            _shortcuts = Helpers.FormShortcuts.For(this);
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "سیستم مالی و کمک‌ها  —  " + SecurityContext.CenterDisplay;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1080, 690);

            // به‌جای SplitContainer (که در RTL و اندازه ثابت سهم پنل‌ها را
            // نامتوازن می‌کرد) از TableLayoutPanel با دو ستون استفاده می‌شود:
            // ستون گرید پرونده‌ها و ستون تب‌های ثبت/گزارش، با سهم مشخص.
            Panel searchPanel = new Panel();
            searchPanel.Dock = DockStyle.Top;
            searchPanel.Height = 66;
            searchPanel.BackColor = UiTheme.CardBack;

            FlowLayoutPanel searchFlow = new FlowLayoutPanel();
            searchFlow.Dock = DockStyle.Fill;
            searchFlow.FlowDirection = FlowDirection.RightToLeft;
            searchFlow.Padding = new Padding(10, 6, 10, 4);

            txtSearch = new TextBox();
            UiTheme.StyleTextBox(txtSearch);
            searchFlow.Controls.Add(MakeFieldPanel("نام / کد", txtSearch));

            // فیلتر «وضعیت خدمات» — هم روی گرید پرونده‌ها و هم روی هر سه گزارش
            // مالی (مجموع کل، ماهانه، بدون کمک) اثر می‌گذارد.
            // «همه وضعیت‌ها» = رفتار قبلی.
            cmbServiceStatus = new ComboBox();
            cmbServiceStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbServiceStatus.Items.Add("همه وضعیت‌ها");
            cmbServiceStatus.Items.AddRange(Helpers.CaseDomain.ServiceStatuses);
            cmbServiceStatus.SelectedIndex = 0;
            searchFlow.Controls.Add(MakeFieldPanel("وضعیت خدمات", cmbServiceStatus));

            Button btnSearch = UiTheme.CreateButton("جستجو", "⌕", UiTheme.Primary);
            btnSearch.Size = new Size(100, 32);
            btnSearch.Margin = new Padding(4, 28, 4, 4);
            btnSearch.Click += delegate { LoadCases(); LoadReports(); };
            _shortcuts.Search(btnSearch);
            searchFlow.Controls.Add(btnSearch);

            searchPanel.Controls.Add(searchFlow);

            dgvCases = new DataGridView();
            dgvCases.Dock = DockStyle.Fill;
            ConfigureGrid(dgvCases);
            dgvCases.CellClick += dgvCases_CellClick;

            Panel gridSide = new Panel();
            gridSide.Dock = DockStyle.Fill;
            gridSide.Controls.Add(dgvCases);
            gridSide.Controls.Add(searchPanel);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = UiTheme.FontBold(UiTheme.SizeSmall);

            TabPage tabEntry = new TabPage("ثبت کمک");
            TabPage tabReports = new TabPage("گزارش‌ها");
            tabEntry.BackColor = UiTheme.Background;
            tabReports.BackColor = UiTheme.Background;

            BuildEntryTab(tabEntry);
            BuildReportsTab(tabReports);

            tabs.TabPages.Add(tabEntry);
            tabs.TabPages.Add(tabReports);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 2;
            root.RowCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            root.Controls.Add(gridSide, 0, 0);
            root.Controls.Add(tabs, 1, 0);

            Controls.Add(root);

            LoadCases();
            LoadReports();
        }

        private void BuildEntryTab(TabPage tab)
        {
            Panel entryPanel = new Panel();
            entryPanel.Dock = DockStyle.Top;
            entryPanel.Height = 356;
            entryPanel.BackColor = UiTheme.CardBack;

            lblSelectedCase = new Label();
            lblSelectedCase.Text = "پرونده انتخاب نشده";
            lblSelectedCase.AutoSize = false;
            lblSelectedCase.Dock = DockStyle.Top;
            lblSelectedCase.Height = 32;
            lblSelectedCase.TextAlign = ContentAlignment.MiddleRight;
            lblSelectedCase.Padding = new Padding(14, 0, 0, 0);
            lblSelectedCase.Font = UiTheme.FontBold(UiTheme.SizeMedium);
            lblSelectedCase.ForeColor = UiTheme.Primary;

            FlowLayoutPanel fieldsFlow = new FlowLayoutPanel();
            fieldsFlow.Dock = DockStyle.Top;
            fieldsFlow.Height = 266;
            fieldsFlow.FlowDirection = FlowDirection.RightToLeft;
            fieldsFlow.WrapContents = true;
            fieldsFlow.Padding = new Padding(14, 4, 14, 4);

            // آموزش — نوع کمک قبلاً متنِ آزاد بود؛ طبقِ خواستهٔ کاربر حالا تیکی
            // (نقدی/غیرنقدی) است. نقدی → همان فیلدِ مبلغِ قبلی. غیرنقدی → به‌جای
            // مبلغ، یک بستهٔ از‌پیش‌تعریف‌شده (تنظیمات → بسته‌های مساعدت) انتخاب
            // می‌شود؛ AssistanceType در دیتابیس همچنان «نقدی»/«غیر نقدی» ذخیره
            // می‌شود (بدونِ تغییرِ ستون، فقط تغییرِ روشِ ورودی).
            Panel pnlAssistanceKind = new Panel { Width = 180, Height = 58, Margin = new Padding(6, 4, 6, 4) };
            Label lblKind = new Label
            {
                Text = "نوع کمک", Dock = DockStyle.Top, Height = 22,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark
            };
            rdoAssistanceCash = new RadioButton { Text = "نقدی", Checked = true, AutoSize = false, Width = 174, Height = 18, RightToLeft = RightToLeft.Yes };
            rdoAssistanceNonCash = new RadioButton { Text = "غیر نقدی", AutoSize = false, Width = 174, Height = 18, Top = 20, RightToLeft = RightToLeft.Yes };
            rdoAssistanceCash.CheckedChanged += delegate { UpdateAssistanceKindVisibility(); };
            pnlAssistanceKind.Controls.Add(rdoAssistanceNonCash);
            pnlAssistanceKind.Controls.Add(rdoAssistanceCash);
            pnlAssistanceKind.Controls.Add(lblKind);
            fieldsFlow.Controls.Add(pnlAssistanceKind);

            numAmount = new NumericUpDown();
            numAmount.Maximum = 1000000000;
            numAmount.DecimalPlaces = 2;
            numAmount.ThousandsSeparator = true;
            pnlAmount = MakeFieldPanel("مبلغ", numAmount);
            fieldsFlow.Controls.Add(pnlAmount);

            cmbPackage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            pnlPackage = MakeFieldPanel("بستهٔ مساعدت", cmbPackage);
            pnlPackage.Visible = false;
            fieldsFlow.Controls.Add(pnlPackage);

            dtpAssistanceDate = new Helpers.PersianDatePicker();
            dtpAssistanceDate.Value = DateTime.Today;
            fieldsFlow.Controls.Add(MakeFieldPanel("تاریخ", dtpAssistanceDate));

            // آموزش — نوع درخواستی از همان دستهٔ Lookup سیستمیِ موجود
            // (TblCase.RequestType، همان‌جا که FrmCase استفاده می‌کند) پر
            // می‌شود؛ ذخیره روی خودِ پرونده انجام می‌شود (ستونِ تازه‌ای لازم
            // نیست) — با انتخابِ پرونده مقدارِ فعلی‌اش این‌جا از پیش انتخاب می‌شود.
            cmbRequestType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            LookupHelper.FillCombo(cmbRequestType, "RequestType");
            fieldsFlow.Controls.Add(MakeFieldPanel("نوع درخواستی", cmbRequestType));

            txtDescription = new TextBox();
            UiTheme.StyleTextBox(txtDescription);
            fieldsFlow.Controls.Add(MakeFieldPanel("توضیح", txtDescription));

            // آموزش — این سه فیلد مخصوصِ برگهٔ دریافتِ مساعدت‌اند (طبقِ تصمیمِ
            // کاربر: PickupLocation/CoordinatorPhone ثابت نیستند و باید برای هر
            // مساعدت جداگانه ذخیره شوند). قبلاً فقط ستونِ دیتابیس و خواندنِ آن
            // در AssistanceReceiptService وجود داشت اما هیچ فیلدِ ورودی‌ای برای
            // نوشتنِ آن‌ها نبود؛ در نتیجه همیشه خالی ذخیره می‌شدند.
            txtProgramName = new TextBox();
            UiTheme.StyleTextBox(txtProgramName);
            fieldsFlow.Controls.Add(MakeFieldPanel("نام برنامه", txtProgramName));

            txtPickupLocation = new TextBox();
            UiTheme.StyleTextBox(txtPickupLocation);
            fieldsFlow.Controls.Add(MakeFieldPanel("محل تحویل", txtPickupLocation));

            txtCoordinatorPhone = new TextBox();
            UiTheme.StyleTextBox(txtCoordinatorPhone);
            fieldsFlow.Controls.Add(MakeFieldPanel("تماس هماهنگ‌کننده", txtCoordinatorPhone));

            FlowLayoutPanel buttonFlow = new FlowLayoutPanel();
            buttonFlow.Dock = DockStyle.Top;
            buttonFlow.Height = 50;
            buttonFlow.FlowDirection = FlowDirection.RightToLeft;
            buttonFlow.Padding = new Padding(14, 6, 14, 6);

            Button btnSave = UiTheme.CreateButton("ثبت کمک", "+", UiTheme.Primary);
            btnSave.Size = new Size(130, 36);
            btnSave.Margin = new Padding(4);
            btnSave.Click += btnSave_Click;
            _shortcuts.Save(btnSave);
            buttonFlow.Controls.Add(btnSave);

            Button btnPrintAssistance = UiTheme.CreateSecondaryButton("چاپ فهرست کمک‌ها", "🖨");
            btnPrintAssistance.Size = new Size(170, 36);
            btnPrintAssistance.Margin = new Padding(4);
            btnPrintAssistance.Click += delegate { PrintAssistanceHistory(); };
            buttonFlow.Controls.Add(btnPrintAssistance);

            // آموزش — ماژول برگه دریافت مساعدت؛ دکمه‌ها کنارِ دکمه‌های چاپِ
            // موجود اضافه می‌شوند (نه Designer)، دقیقاً مثل الگوی
            // AddGuardianCardButton در FrmCase. چاپِ تکی روی ردیفِ انتخاب‌شده‌ی
            // dgvAssistance عمل می‌کند؛ چاپِ گروهی فرم فیلترِ مستقل خودش را دارد.
            Button btnAssistanceReceipt = UiTheme.CreateSecondaryButton("برگه دریافت مساعدت", "🧾");
            btnAssistanceReceipt.Size = new Size(170, 36);
            btnAssistanceReceipt.Margin = new Padding(4);
            btnAssistanceReceipt.Click += delegate
            {
                int assistanceId = GetSelectedAssistanceId();
                if (assistanceId <= 0)
                {
                    Msg.Show("اول یک ردیف از فهرست کمک‌ها را انتخاب کن.");
                    return;
                }
                using (var frm = new AssistanceReceiptIntegration.FrmAssistanceReceiptSinglePrint(assistanceId))
                    frm.ShowDialog(this);
            };
            buttonFlow.Controls.Add(btnAssistanceReceipt);

            Button btnAssistanceReceiptBatch = UiTheme.CreateSecondaryButton("چاپ گروهی برگه‌ها", "🧾");
            btnAssistanceReceiptBatch.Size = new Size(160, 36);
            btnAssistanceReceiptBatch.Margin = new Padding(4);
            btnAssistanceReceiptBatch.Click += delegate
            {
                using (var frm = new AssistanceReceiptIntegration.FrmAssistanceReceiptFilterPrint())
                    frm.ShowDialog(this);
            };
            buttonFlow.Controls.Add(btnAssistanceReceiptBatch);

            Button btnPackageBatchPrint = UiTheme.CreateSecondaryButton("چاپ گروهیِ بسته", "📦");
            btnPackageBatchPrint.Size = new Size(150, 36);
            btnPackageBatchPrint.Margin = new Padding(4);
            btnPackageBatchPrint.Click += delegate
            {
                using (var frm = new AssistanceReceiptIntegration.FrmAssistancePackageBatchPrint())
                    frm.ShowDialog(this);
            };
            buttonFlow.Controls.Add(btnPackageBatchPrint);

            // آموزش — ترتیب Add با Dock=Top: آخرین کنترلی که اضافه می‌شود در
            // بالاترین موقعیت قرار می‌گیرد؛ پس ترتیب Add برعکس ترتیب نمایش
            // است (دکمه‌ها اول، فیلدها بعد، عنوان آخر تا در بالا بماند).
            entryPanel.Controls.Add(buttonFlow);
            entryPanel.Controls.Add(fieldsFlow);
            entryPanel.Controls.Add(lblSelectedCase);

            dgvAssistance = new DataGridView();
            dgvAssistance.Dock = DockStyle.Fill;
            ConfigureGrid(dgvAssistance);
            UiTheme.ApplyPersianDateColumns(dgvAssistance, "AssistanceDate");

            tab.Controls.Add(dgvAssistance);
            tab.Controls.Add(entryPanel);

            LoadPackagesCombo();
            UpdateAssistanceKindVisibility();
        }

        private void LoadPackagesCombo()
        {
            cmbPackage.Items.Clear();
            foreach (AssistanceReceiptIntegration.AssistancePackage pkg in _packageRepo.GetAllPackages())
                cmbPackage.Items.Add(pkg);
            cmbPackage.DisplayMember = "Name";
            if (cmbPackage.Items.Count > 0) cmbPackage.SelectedIndex = 0;
        }

        private void UpdateAssistanceKindVisibility()
        {
            pnlAmount.Visible = rdoAssistanceCash.Checked;
            pnlPackage.Visible = !rdoAssistanceCash.Checked;
        }

        private void BuildReportsTab(TabPage tab)
        {
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 260;

            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 50;
            top.BackColor = UiTheme.CardBack;

            // ─── دکمه‌ها سمت راست (عرض ثابت)، خلاصه مبلغ در فضای باقی‌مانده ──
            FlowLayoutPanel topFlow = new FlowLayoutPanel();
            topFlow.Dock = DockStyle.Right;
            topFlow.Width = 300;
            topFlow.FlowDirection = FlowDirection.RightToLeft;
            topFlow.Padding = new Padding(6, 8, 6, 8);

            Button btnRefresh = UiTheme.CreateButton("تازه‌سازی", "↻", UiTheme.Primary);
            btnRefresh.Size = new Size(110, 32);
            btnRefresh.Margin = new Padding(4);
            btnRefresh.Click += delegate { LoadReports(); };
            _shortcuts.Refresh(btnRefresh);
            topFlow.Controls.Add(btnRefresh);

            Button btnPrintReport = UiTheme.CreateSecondaryButton("چاپ گزارش ماهانه", "🖨");
            btnPrintReport.Size = new Size(150, 32);
            btnPrintReport.Margin = new Padding(4);
            btnPrintReport.Click += delegate { PrintMonthlyReport(); };
            _shortcuts.Print(btnPrintReport);
            topFlow.Controls.Add(btnPrintReport);

            lblTotal = new Label();
            lblTotal.Text = "";
            lblTotal.AutoSize = false;
            lblTotal.Dock = DockStyle.Fill;
            lblTotal.TextAlign = ContentAlignment.MiddleRight;
            lblTotal.Padding = new Padding(0, 0, 8, 0);
            lblTotal.Font = UiTheme.FontBold(UiTheme.SizeMedium);
            lblTotal.ForeColor = UiTheme.Success;

            top.Controls.Add(lblTotal);
            top.Controls.Add(topFlow);

            dgvMonthly = new DataGridView();
            dgvMonthly.Dock = DockStyle.Fill;
            ConfigureGrid(dgvMonthly);

            dgvWithoutAssistance = new DataGridView();
            dgvWithoutAssistance.Dock = DockStyle.Fill;
            ConfigureGrid(dgvWithoutAssistance);

            split.Panel1.Controls.Add(dgvMonthly);
            split.Panel1.Controls.Add(top);
            split.Panel2.Controls.Add(dgvWithoutAssistance);
            tab.Controls.Add(split);
        }

        // آموزش — رفعِ باگِ «پیامِ کمک ثبت شد خودبه‌خود تکرار می‌شود»:
        // پس از کلیک روی «ثبت کمک»، فوکوس روی همان دکمه می‌ماند. کاربر پیامِ
        // موفقیت را با Enter/Space می‌بندد و همان کلید به دکمهٔ فوکوس‌دار
        // می‌رسد (دکمهٔ WinForms با Enter/Space شلیک می‌شود) — یعنی یک ثبتِ
        // تکراریِ دیگر و دوباره همان پیام. با آزمونِ واقعی ثابت شد هیچ
        // Timer/فرایندِ پس‌زمینه‌ای این را صدا نمی‌زند؛ فقط همین زنجیرهٔ کلید.
        //
        // سه لایهٔ محافظت (هیچ‌کدام مسیرِ استفادهٔ درست را عوض نمی‌کند):
        //   ۱) قفلِ ورودِ مجدد تا وقتی یک ثبت در جریان است
        //   ۲) بردنِ فوکوس از روی دکمه پس از ثبت
        //   ۳) جلوگیری از رکوردِ کاملاً یکسان در فاصلهٔ کوتاه (با تأییدِ صریح)
        private bool _isSavingAssistance;

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (_isSavingAssistance) return;

            if (!CaseManagement.Enterprise.PermissionService.Require("Finance.Edit"))
            {
                UiTheme.ShowWarning(this, "کاربر فقط مشاهده اجازه ثبت کمک ندارد.");
                return;
            }

            if (selectedCaseId <= 0)
            {
                UiTheme.ShowWarning(this, "ابتدا پرونده را انتخاب کنید.");
                return;
            }

            bool isCash = rdoAssistanceCash.Checked;
            if (isCash && numAmount.Value <= 0)
            {
                UiTheme.ShowWarning(this, "مبلغ کمک باید بیشتر از صفر باشد.");
                return;
            }

            AssistanceReceiptIntegration.AssistancePackage selectedPackage = !isCash
                ? cmbPackage.SelectedItem as AssistanceReceiptIntegration.AssistancePackage : null;
            if (!isCash && selectedPackage == null)
            {
                UiTheme.ShowWarning(this, "برای کمکِ غیرنقدی یک بسته انتخاب کنید.");
                return;
            }

            int newAssistanceId = 0;
            decimal amount = isCash ? numAmount.Value : 0m;
            string assistanceType = isCash ? "نقدی" : "غیر نقدی";
            string assistanceDate = dtpAssistanceDate.Value.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            // لایهٔ سومِ محافظت: اگر همین‌الان دقیقاً همین کمک برای همین پرونده
            // ثبت شده باشد، به‌جای ثبتِ خاموشِ رکوردِ تکراری، از کاربر می‌پرسیم.
            if (IsDuplicateRecentAssistance(selectedCaseId, assistanceDate, amount, assistanceType))
            {
                if (!UiTheme.ShowConfirm(this,
                        "همین کمک چند لحظه پیش برای این پرونده ثبت شده است." +
                        Environment.NewLine + Environment.NewLine +
                        "آیا واقعاً می‌خواهید یک رکورد دیگر هم ثبت شود؟",
                        "ثبت تکراری"))
                    return;
            }

            _isSavingAssistance = true;
            try
            {

            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();

                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO TblAssistance (CasID, AssistanceDate, Amount, AssistanceType, Description, CreatedBy, ProgramName, PickupLocation, CoordinatorPhone, PackageID, GlobalID)
VALUES (@CasID, @AssistanceDate, @Amount, @AssistanceType, @Description, @CreatedBy, @ProgramName, @PickupLocation, @CoordinatorPhone, @PackageID,
    lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
    lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))))", con))
                {
                    cmd.Parameters.Add("@CasID", DbType.Int32).Value = selectedCaseId;
                    cmd.Parameters.AddWithValue("@AssistanceDate", assistanceDate);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.Add("@AssistanceType", DbType.String, 100).Value = assistanceType;
                    cmd.Parameters.Add("@Description", DbType.String, -1).Value = txtDescription.Text.Trim();
                    cmd.Parameters.Add("@CreatedBy", DbType.String, 50).Value = SecurityContext.Username ?? "";
                    cmd.Parameters.Add("@ProgramName", DbType.String, 200).Value = txtProgramName.Text.Trim();
                    cmd.Parameters.Add("@PickupLocation", DbType.String, 300).Value = txtPickupLocation.Text.Trim();
                    cmd.Parameters.Add("@CoordinatorPhone", DbType.String, 50).Value = txtCoordinatorPhone.Text.Trim();
                    cmd.Parameters.AddWithValue("@PackageID", selectedPackage != null ? (object)selectedPackage.PackageID : DBNull.Value);

                    cmd.ExecuteNonQuery();

                    // شناسهٔ رکورد تازه روی *همین اتصال* خوانده می‌شود؛ روی اتصالِ
                    // دیگر مقدار last_insert_rowid همیشه صفر است (همان نکته‌ای که
                    // در DatabaseHelper.ExecuteInsertReturningId توضیح داده شده).
                    using (SQLiteCommand idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                    {
                        object value = idCmd.ExecuteScalar();
                        newAssistanceId = value == null || value == DBNull.Value
                            ? 0 : Convert.ToInt32(value);
                    }
                }

                // آموزش — «نوع درخواستی» از همان ستونِ موجودِ TblCase.RequestType
                // استفاده می‌کند (طبقِ تصمیمِ کاربر: بدونِ ستونِ تازه)؛ اینجا فقط
                // پرونده به‌روزرسانی می‌شود، نه یک ستونِ جدید در TblAssistance.
                using (SQLiteCommand cmdReq = new SQLiteCommand(
                    "UPDATE TblCase SET RequestType=@RequestType WHERE CasID=@CasID", con))
                {
                    cmdReq.Parameters.Add("@RequestType", DbType.String, 100).Value = cmbRequestType.Text.Trim();
                    cmdReq.Parameters.Add("@CasID", DbType.Int32).Value = selectedCaseId;
                    cmdReq.ExecuteNonQuery();
                }
            }

            AuditLogger.Log("ثبت کمک", "TblAssistance", selectedCaseId, "", "Amount=" + amount);

            // صفِ همگام‌سازی — کمکِ ثبت‌شده و به‌روزرسانیِ نوعِ درخواستیِ پرونده
            // هردو باید به شعبهٔ مرکزی برسند.
            if (newAssistanceId > 0)
                CaseManagement.Sync.SyncOutboxService.Capture("TblAssistance", newAssistanceId,
                    CaseManagement.Sync.OfflineSyncInitializer.OperationCreate);
            CaseManagement.Sync.SyncOutboxService.Capture("TblCase", selectedCaseId,
                CaseManagement.Sync.OfflineSyncInitializer.OperationUpdate);

            // تاریخچهٔ کاملِ رکورد — هم برای کمکِ تازه‌ثبت‌شده و هم برای پرونده‌ای
            // که نوعِ درخواستی‌اش تغییر کرد.
            if (newAssistanceId > 0)
                CaseManagement.Enterprise.VersionService.Capture("TblAssistance", newAssistanceId,
                    CaseManagement.Enterprise.VersionService.OperationInsert);
            CaseManagement.Enterprise.VersionService.Capture("TblCase", selectedCaseId,
                CaseManagement.Enterprise.VersionService.OperationUpdate);

            LoadAssistance();
            LoadReports();

            // فوکوس را از روی دکمهٔ «ثبت کمک» برمی‌داریم تا کلیدی که کاربر برای
            // بستنِ پیامِ موفقیت می‌زند (Enter/Space) دوباره همان دکمه را شلیک
            // نکند — ریشهٔ دقیقِ تکرارِ خودبه‌خودِ پیام.
            //
            // آموزش — عمداً ActiveControl = null و نه Focus() روی یک کنترلِ
            // مشخص: در حالتِ «غیر نقدی» کادرِ مبلغ پنهان است و Focus() روی
            // کنترلِ پنهان مسیرِ کندی را در WinForms فعال می‌کرد (زمانِ همین
            // مسیر در آزمون از ۳ ثانیه به ۷۸ ثانیه پرید). این روش هیچ هدفی
            // لازم ندارد و همیشه ارزان است.
            try { ActiveControl = null; } catch { }

            }
            finally
            {
                _isSavingAssistance = false;
            }

            UiTheme.ShowSuccess(this, "کمک ثبت شد.");
        }

        // آیا دقیقاً همین کمک همین‌الان (در ۱۵ ثانیهٔ گذشته) ثبت شده است؟
        // فقط برای هشدار به کاربر استفاده می‌شود، نه مسدودکردنِ قطعی.
        private bool IsDuplicateRecentAssistance(int caseId, string assistanceDate, decimal amount, string assistanceType)
        {
            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT COUNT(*) FROM TblAssistance
WHERE CasID = @CasID
  AND AssistanceDate = @AssistanceDate
  AND Amount = @Amount
  AND AssistanceType = @AssistanceType
  AND CreatedAt IS NOT NULL
  AND CreatedAt >= datetime('now', '-15 seconds')", con))
                {
                    cmd.Parameters.Add("@CasID", DbType.Int32).Value = caseId;
                    cmd.Parameters.AddWithValue("@AssistanceDate", assistanceDate);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.Add("@AssistanceType", DbType.String, 100).Value = assistanceType;
                    con.Open();
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
            catch
            {
                // بررسیِ تکراری‌بودن نباید هرگز مسیرِ ثبت را بشکند.
                return false;
            }
        }

        private void PrintAssistanceHistory()
        {
            DataTable table = dgvAssistance.DataSource as DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                Msg.Show("داده‌ای برای چاپ وجود ندارد");
                return;
            }

            DataTable printTable = table.Copy();
            Helpers.PersianDateHelper.ConvertDateColumnsToPersian(printTable, "AssistanceDate");
            PrintHelper.PrintDataTable(this, "فهرست کمک‌ها — " + lblSelectedCase.Text, printTable);
        }

        private void PrintMonthlyReport()
        {
            DataTable table = dgvMonthly.DataSource as DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                Msg.Show("داده‌ای برای چاپ وجود ندارد");
                return;
            }

            PrintHelper.PrintDataTable(this, "گزارش ماهانه کمک‌ها", table);
        }

        private void dgvCases_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || !dgvCases.Columns.Contains("CasID"))
                return;

            object value = dgvCases.Rows[e.RowIndex].Cells["CasID"].Value;
            if (value == null || value == DBNull.Value)
                return;

            selectedCaseId = Convert.ToInt32(value);
            string code = dgvCases.Rows[e.RowIndex].Cells["Code"].Value.ToString();
            string name = dgvCases.Rows[e.RowIndex].Cells["HeadFullName"].Value.ToString();
            lblSelectedCase.Text = string.Format(Lang.T("پرونده انتخاب‌شده: {0} - {1}"), code, name);

            object requestTypeVal = dgvCases.Rows[e.RowIndex].Cells["RequestType"].Value;
            string requestType = requestTypeVal == null || requestTypeVal == DBNull.Value ? "" : requestTypeVal.ToString();
            cmbRequestType.Text = requestType;

            LoadAssistance();
        }

        private void LoadCases()
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT CasID, FormNo, Code, HeadFullName, Phone, ServiceStatus, RequestType
FROM TblCase
WHERE (@Search = ''
   OR Code LIKE @LikeSearch
   OR HeadFullName LIKE @LikeSearch)
  AND (@CID = 0 OR CenterID = @CID)
  AND (@Svc = '' OR ServiceStatus = @Svc)
ORDER BY CasID DESC", con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                string search = txtSearch == null ? "" : txtSearch.Text.Trim();
                cmd.Parameters.Add("@Search", DbType.String, 4000).Value = search;
                cmd.Parameters.Add("@LikeSearch", DbType.String, 4000).Value = "%" + search + "%";
                cmd.Parameters.AddWithValue("@CID", Helpers.SecurityContext.CenterFilterId);
                cmd.Parameters.AddWithValue("@Svc", GetSelectedServiceStatus());

                DataTable table = new DataTable();
                da.Fill(table);
                dgvCases.DataSource = table;
            }
        }

        private void LoadAssistance()
        {
            if (selectedCaseId <= 0)
                return;

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT AssistanceID, AssistanceDate, Amount, AssistanceType, Description, CreatedBy
FROM TblAssistance
WHERE CasID = @CasID
ORDER BY AssistanceDate DESC, AssistanceID DESC", con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                cmd.Parameters.Add("@CasID", DbType.Int32).Value = selectedCaseId;
                DataTable table = new DataTable();
                da.Fill(table);
                dgvAssistance.DataSource = table;
            }
        }

        // ردیفِ انتخاب‌شده در dgvAssistance را برای ماژول برگه دریافت مساعدت
        // برمی‌گرداند؛ اگر چیزی انتخاب نشده باشد 0 برمی‌گردد.
        private int GetSelectedAssistanceId()
        {
            if (dgvAssistance.CurrentRow == null) return 0;
            object val = dgvAssistance.CurrentRow.Cells["AssistanceID"].Value;
            return val == null || val == DBNull.Value ? 0 : Convert.ToInt32(val);
        }

        // مقدار فیلترِ «وضعیت خدمات» این فرم — رشته‌ی خالی یعنی «همه وضعیت‌ها».
        private string GetSelectedServiceStatus()
        {
            return (cmbServiceStatus == null || cmbServiceStatus.SelectedIndex <= 0)
                ? "" : cmbServiceStatus.Text.Trim();
        }

        // هر سه گزارشِ این متد پرونده‌های بایگانی‌شده را کنار می‌گذارند
        // (IsArchived = 0) — همان قاعده‌ای که داشبورد و خروجی اکسل رعایت
        // می‌کنند. توجه: گریدِ انتخاب پرونده در بالای فرم عمداً این شرط را
        // ندارد، چون ابزار انتخاب برای «ثبت کمک» است نه گزارش؛ اگر فیلتر
        // می‌شد دیگر نمی‌شد برای پرونده‌ی بایگانی‌شده کمک ثبت یا اصلاح کرد.
        private void LoadReports()
        {
            int cid = Helpers.SecurityContext.CenterFilterId;
            string svc = GetSelectedServiceStatus();

            using (SQLiteConnection con = db.GetConnection())
            {
                con.Open();

                // مجموع کل کمک‌ها — فیلتر شده بر اساس مرکز فعال و وضعیت خدمات
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT COALESCE(SUM(a.Amount), 0)
FROM TblAssistance a
JOIN TblCase c ON c.CasID = a.CasID
WHERE (@CID = 0 OR c.CenterID = @CID)
  AND c.IsArchived = 0
  AND (@Svc = '' OR c.ServiceStatus = @Svc)", con))
                {
                    cmd.Parameters.AddWithValue("@CID", cid);
                    cmd.Parameters.AddWithValue("@Svc", svc);
                    lblTotal.Text = string.Format(Lang.T("مجموع کل کمک‌ها: {0}"),
                        Convert.ToDecimal(cmd.ExecuteScalar()).ToString("N2"));
                }

                // گزارش ماهانه — فیلتر شده بر اساس مرکز و وضعیت خدمات
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT strftime('%Y-%m', a.AssistanceDate) AS [ماه], SUM(a.Amount) AS [مجموع کمک]
FROM TblAssistance a
JOIN TblCase c ON c.CasID = a.CasID
WHERE (@CID = 0 OR c.CenterID = @CID)
  AND c.IsArchived = 0
  AND (@Svc = '' OR c.ServiceStatus = @Svc)
GROUP BY strftime('%Y-%m', a.AssistanceDate)
ORDER BY [ماه] DESC", con))
                {
                    cmd.Parameters.AddWithValue("@CID", cid);
                    cmd.Parameters.AddWithValue("@Svc", svc);
                    using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                    {
                        DataTable t = new DataTable();
                        da.Fill(t);
                        dgvMonthly.DataSource = t;
                    }
                }

                // پرونده‌های بدون کمک — فیلتر شده بر اساس مرکز و وضعیت خدمات
                using (SQLiteCommand cmdF = new SQLiteCommand(@"
SELECT c.CasID, c.FormNo, c.Code, c.HeadFullName, c.Phone, c.ServiceStatus
FROM TblCase c
WHERE NOT EXISTS (SELECT 1 FROM TblAssistance a WHERE a.CasID = c.CasID)
  AND (@CID = 0 OR c.CenterID = @CID)
  AND c.IsArchived = 0
  AND (@Svc = '' OR c.ServiceStatus = @Svc)
ORDER BY c.CasID DESC", con))
                {
                    cmdF.Parameters.AddWithValue("@CID", cid);
                    cmdF.Parameters.AddWithValue("@Svc", svc);
                    using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmdF))
                    {
                        DataTable t = new DataTable();
                        da.Fill(t);
                        dgvWithoutAssistance.DataSource = t;
                    }
                }
            }
        }

        private void FillGrid(SQLiteConnection con, DataGridView grid, string query)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                DataTable table = new DataTable();
                da.Fill(table);
                grid.DataSource = table;
            }
        }

        private void ConfigureGrid(DataGridView grid)
        {
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            UiTheme.StyleGrid(grid);
        }

        private Label CreateLabel(string text, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Font = UiTheme.FontBold(UiTheme.SizeSmall);
            label.ForeColor = UiTheme.TextDark;
            label.TextAlign = ContentAlignment.MiddleRight;
            label.SetBounds(x, y, width, 25);
            return label;
        }

        // یک فیلد فیلتر: برچسب بالا + کنترل پایین، با اندازه یکسان (الگوی FrmAdvancedSearch).
        private Panel MakeFieldPanel(string labelText, Control input)
        {
            Panel p = new Panel();
            p.Width = 180;
            p.Height = 58;
            p.Margin = new Padding(6, 4, 6, 4);

            Label lbl = new Label();
            lbl.Text = labelText;
            lbl.AutoSize = false;
            lbl.Dock = DockStyle.Top;
            lbl.Height = 22;
            lbl.TextAlign = ContentAlignment.MiddleRight;
            lbl.Font = UiTheme.FontBold(UiTheme.SizeSmall);
            lbl.ForeColor = UiTheme.TextDark;

            input.Dock = DockStyle.Top;
            input.Width = 174;
            input.Font = UiTheme.Font(UiTheme.SizeBody);
            if (input is TextBox tb)
                tb.Height = 28;
            else if (input is NumericUpDown nud)
                nud.Height = 28;

            p.Controls.Add(input);
            p.Controls.Add(lbl);
            return p;
        }
    }
}

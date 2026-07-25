namespace CaseManagement
{
    partial class FrmCase
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// چیدمان با مختصات مطلق (Location/Size صریح روی هر کنترل) — قابل
        /// ویرایش با موس در Visual Studio Designer. فیلدها در سه گروه
        /// (مشخصات کلی سرپرست / مشخصات جسمی / مشخصات پرونده) دسته‌بندی شده‌اند.
        /// نام کنترل‌ها و رویدادها دست‌نخورده مانده‌اند تا منطق موجود در
        /// FrmCase.cs کار کند. آموزش — چیدمان راست‌به‌چپ دستی: چون
        /// RightToLeftLayout مختصات Location کنترل‌های عادی را خودکار آینه
        /// نمی‌کند، برای هر ردیف، جفتِ «راست» (اول‌خوانده) در X بزرگ‌تر و
        /// جفتِ «چپ» در X کوچک‌تر قرار گرفته است.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnSave = new System.Windows.Forms.Button();
            this.btnNew = new System.Windows.Forms.Button();
            this.btnEdit = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnSearch = new System.Windows.Forms.Button();
            this.dgvCases = new System.Windows.Forms.DataGridView();
            this.txtPhotoPath = new System.Windows.Forms.TextBox();
            this.dtpCaseDate = new CaseManagement.Helpers.PersianDatePicker();
            this.btnBrowsePhoto = new System.Windows.Forms.Button();
            this.btnBrowseFamilyPhoto = new System.Windows.Forms.Button();
            this.txtFamilyPhotoPath = new System.Windows.Forms.TextBox();
            this.picPhoto = new System.Windows.Forms.PictureBox();
            this.picFamilyPhoto = new System.Windows.Forms.PictureBox();
            this.lblCaseDate = new System.Windows.Forms.Label();
            this.txtRelationshipToFamily = new System.Windows.Forms.TextBox();
            this.txtCoveredByOrg = new System.Windows.Forms.ComboBox();
            this.txtJob = new System.Windows.Forms.TextBox();
            this.txtSkill = new System.Windows.Forms.TextBox();
            this.txtDisabilityDegree = new System.Windows.Forms.ComboBox();
            this.txtDisabilityType = new System.Windows.Forms.ComboBox();
            this.txtMigrationCardType = new System.Windows.Forms.TextBox();
            this.txtMaritalStatus = new System.Windows.Forms.ComboBox();
            this.txtEducationLevel = new System.Windows.Forms.ComboBox();
            this.txtServiceStatus = new System.Windows.Forms.ComboBox();
            this.label13 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.label18 = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.label20 = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.label22 = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.label26 = new System.Windows.Forms.Label();
            this.btnFamily = new System.Windows.Forms.Button();
            this.btnDocs = new System.Windows.Forms.Button();
            this.btnChooseStorageFolder = new System.Windows.Forms.Button();
            this.btnExportPdf = new System.Windows.Forms.Button();
            this.btnExportWord = new System.Windows.Forms.Button();
            this.btnExportExcel = new System.Windows.Forms.Button();
            this.btnBatchExport = new System.Windows.Forms.Button();
            this.btnPrint = new System.Windows.Forms.Button();
            this.lblServiceStatusFilter = new System.Windows.Forms.Label();
            this.cmbServiceStatusFilter = new System.Windows.Forms.ComboBox();
            this.lblExportSection = new System.Windows.Forms.Label();
            this.dtpSurveyDate = new CaseManagement.Helpers.PersianDatePicker();
            this.label28 = new System.Windows.Forms.Label();
            this.txtLocationAddress = new System.Windows.Forms.TextBox();
            this.label25 = new System.Windows.Forms.Label();
            this.txtSurveyors = new System.Windows.Forms.TextBox();
            this.label23 = new System.Windows.Forms.Label();
            this.txtUrgentSituation = new System.Windows.Forms.TextBox();
            this.label27 = new System.Windows.Forms.Label();
            this.txtPhone = new System.Windows.Forms.TextBox();
            this.txtRelativePhone = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.txtHeadCurrentResidence = new System.Windows.Forms.TextBox();
            this.txtRequestType = new System.Windows.Forms.ComboBox();
            this.txtPriorityLevel = new System.Windows.Forms.ComboBox();
            this.txtHeadFullName = new System.Windows.Forms.TextBox();
            this.txtHeadFatherName = new System.Windows.Forms.TextBox();
            this.txtHeadSadat = new System.Windows.Forms.ComboBox();
            this.txtReligion = new System.Windows.Forms.ComboBox();
            this.txtHeadTazkiraNo = new System.Windows.Forms.TextBox();
            this.txtHeadOriginalResidence = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.txtProvince = new System.Windows.Forms.ComboBox();
            this.txtDistrict = new System.Windows.Forms.ComboBox();
            this.txtFormNo = new System.Windows.Forms.TextBox();
            this.txtCode = new System.Windows.Forms.TextBox();
            this.txtCaseNo = new System.Windows.Forms.TextBox();
            this.txtZone = new System.Windows.Forms.ComboBox();
            this.lblCode = new System.Windows.Forms.Label();
            this.lblFormNo = new System.Windows.Forms.Label();
            this.lblCaseNo = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.lblStopReason = new System.Windows.Forms.Label();
            this.txtStopReason = new System.Windows.Forms.TextBox();
            this.grpHead = new System.Windows.Forms.GroupBox();
            this.grpPhysical = new System.Windows.Forms.GroupBox();
            this.grpCase = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCases)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPhoto)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picFamilyPhoto)).BeginInit();
            this.SuspendLayout();

            // ═══ کمبوهای وضعیت (آیتم‌ها حفظ می‌شوند) ═══════════════════════════
            this.txtServiceStatus.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtServiceStatus.Items.AddRange(new object[] { "فعال", "در انتظار تأیید", "قطع موقت", "قطع" });
            this.cmbServiceStatusFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbServiceStatusFilter.Items.AddRange(new object[] { "همه", "فعال", "در انتظار تأیید", "قطع موقت", "قطع" });
            this.cmbServiceStatusFilter.SelectedIndexChanged += new System.EventHandler(this.cmbServiceStatusFilter_SelectedIndexChanged);

            this.txtPhotoPath.Visible = false;
            this.txtFamilyPhotoPath.Visible = false;

            // ═══ کمبوهای ثابت (مقادیر مستقیم؛ بدون فراخوانی دیتابیس در
            // InitializeComponent تا Designer همیشه بدون نیاز به دیتابیس
            // باز شود) ═══════════════════════════════════════════════════════
            this.txtZone.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtZone.Items.AddRange(CaseManagement.Helpers.AfghanGeoData.Zones);

            this.txtCoveredByOrg.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtCoveredByOrg.Items.AddRange(new object[] { "بله", "خیر" });

            this.txtReligion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtReligion.Items.AddRange(new object[] { "اهل تشیع", "اهل تسنن" });

            this.txtPriorityLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtPriorityLevel.Items.AddRange(new object[] { "اول", "دوم", "سوم" });

            // آموزش — پیش‌تر ولایت از LookupHelper.FillCombo (فراخوانی زنده
            // دیتابیس) پر می‌شد که Designer را با نیاز به دیتابیس می‌شکست.
            // حالا از همان فهرست پیش‌فرض TblLookup/Province به‌صورت ثابت در
            // کد استفاده می‌شود (اگر کاربر بعداً ولایتی از FrmSettings اضافه
            // کند، LoadCases/دیگر فرم‌ها همچنان از دیتابیس می‌خوانند؛ فقط این
            // کمبوی خاص برای امنیت Designer ثابت شد).
            this.txtProvince.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtProvince.Items.AddRange(new object[]
            {
                "کابل", "هرات", "بلخ", "قندهار", "ننگرهار", "بدخشان", "بغلان", "تخار",
                "غزنی", "هلمند", "لغمان", "کندز", "فاریاب", "جوزجان", "سمنگان", "بامیان",
                "پکتیا", "لوگر", "وردک", "غور", "فراه", "خوست", "کاپیسا", "پروان",
                "زابل", "ارزگان", "نیمروز", "نورستان", "کنر", "سرپل", "دایکندی",
                "پکتیکا", "بادغیس", "پنجشیر"
            });
            this.txtProvince.SelectedIndexChanged += new System.EventHandler(this.txtProvince_SelectedIndexChanged);

            this.txtDistrict.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // ─── نوع درخواست (کشویی جدید طبق درخواست کاربر) ─────────────────
            this.txtRequestType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtRequestType.Items.AddRange(new object[] { "یتیم", "معلول", "مهاجر", "بدسرپرست", "کهولت سن", "بی‌سرپرست" });

            // ─── سیادت سرپرست (کشویی جدید) ───────────────────────────────────
            this.txtHeadSadat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtHeadSadat.Items.AddRange(new object[] { "عام", "سادات" });

            // ─── نوع معلولیت (همان فهرست فرم اعضای خانواده) ──────────────────
            this.txtDisabilityType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtDisabilityType.Items.AddRange(new object[] { "جسمی", "ذهنی", "بینایی", "شنوایی", "گفتاری", "حسی" });

            // ─── وضعیت تأهل (کشویی جدید) ─────────────────────────────────────
            this.txtMaritalStatus.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtMaritalStatus.Items.AddRange(new object[] { "مجرد", "متأهل", "مطلقه" });

            // ─── درجه معلولیت (کشویی جدید) ────────────────────────────────────
            this.txtDisabilityDegree.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtDisabilityDegree.Items.AddRange(new object[] { "اول", "دوم", "سوم" });

            // ─── تحصیلات سرپرست (کشویی جدید) ──────────────────────────────────
            this.txtEducationLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtEducationLevel.Items.AddRange(new object[] { "ابتدایی", "متوسط", "عالی", "لیسانس", "دکترا", "طلبه", "بی‌سواد" });

            // ═══════════════════════════════════════════════════════════════════
            // گروه ۱: مشخصات کلی سرپرست
            // ═══════════════════════════════════════════════════════════════════
            // آموزش — بازطراحی: مختصاتِ ثابتِ قبلی (Location/Size برای تک‌تکِ
            // ۳۰+ فیلد) کاملاً حذف شد. آن الگو در مقیاس‌های ۱۲۵٪ به بالا
            // می‌شکند چون اعداد ثابت با فونت/DPI بزرگ‌شده هم‌خوان نیستند.
            // جایگزین: شبکه‌ی FieldBox (برچسبِ بالا + ورودیِ گردگوشه) داخل یک
            // کارت — همان الگویی که در فرم اعضای خانواده آزموده شد.
            // نام کنترل‌ها، رویدادها و منطق دست‌نخورده‌اند.
            var gridHead = MkCaseFieldGrid();
            AddCaseField(gridHead, this.label6,  "نام سرپرست و تخلص",   this.txtHeadFullName);
            AddCaseField(gridHead, this.label7,  "نام پدر سرپرست",      this.txtHeadFatherName);
            AddCaseField(gridHead, this.label8,  "سیادت سرپرست",        this.txtHeadSadat);
            AddCaseField(gridHead, this.label9,  "مذهب",                this.txtReligion);
            AddCaseField(gridHead, this.label10, "شماره تذکره سرپرست",  this.txtHeadTazkiraNo);
            AddCaseField(gridHead, this.label11, "سکونت اصلی سرپرست",   this.txtHeadOriginalResidence);
            AddCaseField(gridHead, this.label12, "سکونت فعلی سرپرست",   this.txtHeadCurrentResidence);
            AddCaseField(gridHead, this.label13, "نسبت با سایر اعضا",   this.txtRelationshipToFamily);
            AddCaseField(gridHead, this.label14, "شماره تماس",          this.txtPhone);
            AddCaseField(gridHead, this.label15, "شماره تماس اقارب",    this.txtRelativePhone);
            AddCaseField(gridHead, this.label22, "وضعیت تأهل",          this.txtMaritalStatus);
            AddCaseField(gridHead, this.label26, "تحصیلات",             this.txtEducationLevel);
            AddCaseField(gridHead, this.label17, "شغل",                 this.txtJob);
            AddCaseField(gridHead, this.label18, "مهارت",               this.txtSkill);

            this.grpHead.Text = "";
            this.grpHead.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            var cardHead = MkCaseCard("مشخصات کلی سرپرست", gridHead, this.grpHead);

            // ═══════════════════════════════════════════════════════════════════
            // گروه ۲: مشخصات جسمی — چک‌باکس سالم/معلول + نوع/درجه معلولیت
            // ═══════════════════════════════════════════════════════════════════
            // آموزش — به درخواست کاربر: یک چک‌باکس «تیکی» که مشخص می‌کند فرد
            // سالم است یا معلول. با تیک‌زدن «سالم»، فیلدهای نوع/درجه معلولیت
            // غیرفعال و خالی می‌شوند (منطق در FrmCase.cs → UpdateHeadPhysicalState).
            this.chkHeadHealthy = new System.Windows.Forms.CheckBox();
            this.chkHeadHealthy.Text = "سالم است (بدون معلولیت)";
            this.chkHeadHealthy.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.chkHeadHealthy.AutoSize = true;
            this.chkHeadHealthy.Checked = true;
            this.chkHeadHealthy.Margin = new System.Windows.Forms.Padding(6, 10, 6, 10);

            var gridPhysical = MkCaseFieldGrid();
            AddCaseField(gridPhysical, this.label20, "نوع معلولیت",  this.txtDisabilityType);
            AddCaseField(gridPhysical, this.label19, "درجه معلولیت", this.txtDisabilityDegree);

            // چک‌باکس بالای شبکه‌ی فیلدها، تمام‌عرض.
            var physicalHost = new System.Windows.Forms.Panel();
            physicalHost.Dock = System.Windows.Forms.DockStyle.Top;
            physicalHost.AutoSize = true;
            physicalHost.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            physicalHost.BackColor = System.Drawing.Color.Transparent;

            var chkRow = new System.Windows.Forms.Panel();
            chkRow.Dock = System.Windows.Forms.DockStyle.Top;
            chkRow.Height = 38;
            chkRow.BackColor = System.Drawing.Color.Transparent;
            chkRow.Padding = new System.Windows.Forms.Padding(18, 8, 18, 0);
            this.chkHeadHealthy.Dock = System.Windows.Forms.DockStyle.Right;
            chkRow.Controls.Add(this.chkHeadHealthy);

            physicalHost.Controls.Add(gridPhysical);
            physicalHost.Controls.Add(chkRow);

            this.grpPhysical.Text = "";
            this.grpPhysical.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            var cardPhysical = MkCaseCard("مشخصات جسمی", physicalHost, this.grpPhysical);

            // ═══════════════════════════════════════════════════════════════════
            // گروه ۳: مشخصات پرونده
            // ═══════════════════════════════════════════════════════════════════
            var gridCase = MkCaseFieldGrid();
            AddCaseField(gridCase, this.lblCode,      "کد اختصاصی",         this.txtCode);
            AddCaseField(gridCase, this.lblFormNo,    "شماره فرم",          this.txtFormNo);
            AddCaseField(gridCase, this.lblCaseNo,    "شماره پرونده",       this.txtCaseNo);
            AddCaseField(gridCase, this.label1,       "زون",                this.txtZone);
            AddCaseField(gridCase, this.label2,       "ولایت",              this.txtProvince);
            AddCaseField(gridCase, this.label3,       "ولسوالی",            this.txtDistrict);
            AddCaseField(gridCase, this.label4,       "نوع درخواست",        this.txtRequestType);
            AddCaseField(gridCase, this.label5,       "اولویت‌بندی",        this.txtPriorityLevel);
            AddCaseField(gridCase, this.label21,      "نوع برگه مهاجرت",    this.txtMigrationCardType);
            AddCaseField(gridCase, this.label16,      "تحت پوشش",           this.txtCoveredByOrg);
            AddCaseField(gridCase, this.lblCaseDate,  "تاریخ تشکیل پرونده", this.dtpCaseDate);
            AddCaseField(gridCase, this.label24,      "وضعیت خدمات",        this.txtServiceStatus);
            AddCaseField(gridCase, this.label25,      "آدرس لوکیشن",        this.txtLocationAddress);
            AddCaseField(gridCase, this.label23,      "سروی‌کننده‌ها",      this.txtSurveyors);
            AddCaseField(gridCase, this.label28,      "تاریخ سروی",         this.dtpSurveyDate);

            // «دلیل قطع موقت» مثل قبل پنهان است تا وضعیت خدمات «قطع موقت» شود
            // (منطقش در FrmCase.cs دست‌نخورده مانده و روی همین دو کنترل کار می‌کند).
            this.caseFieldStopReason = AddCaseField(gridCase, this.lblStopReason, "دلیل قطع موقت", this.txtStopReason);
            this.lblStopReason.Visible = false;
            this.txtStopReason.Visible = false;
            this.caseFieldStopReason.Visible = false;

            // ─── شرح وضعیت فوری: چندخطی و تمام‌عرض، زیر شبکه ─────────────────
            // آموزش — رفع باگ چپ‌چین بودن: برای TextBox چندخطی صرفِ
            // TextAlign=Right کافی نیست؛ بدون RightToLeft=Yes مکان‌نما و جریان
            // متن از چپ شروع می‌شود.
            this.txtUrgentSituation.Multiline = true;
            this.txtUrgentSituation.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtUrgentSituation.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtUrgentSituation.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            var boxUrgent = new CaseManagement.Helpers.FieldBox(
                this.label27, "شرح وضعیت فوری", this.txtUrgentSituation);
            boxUrgent.Dock = System.Windows.Forms.DockStyle.Top;
            boxUrgent.Height = 132;
            boxUrgent.Margin = new System.Windows.Forms.Padding(18, 4, 18, 12);

            var urgentHost = new System.Windows.Forms.Panel();
            urgentHost.Dock = System.Windows.Forms.DockStyle.Top;
            urgentHost.Height = 140;
            urgentHost.BackColor = System.Drawing.Color.Transparent;
            urgentHost.Padding = new System.Windows.Forms.Padding(18, 0, 18, 10);
            urgentHost.Controls.Add(boxUrgent);

            var caseHost = new System.Windows.Forms.Panel();
            caseHost.Dock = System.Windows.Forms.DockStyle.Top;
            caseHost.AutoSize = true;
            caseHost.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            caseHost.BackColor = System.Drawing.Color.Transparent;
            caseHost.Controls.Add(urgentHost);
            caseHost.Controls.Add(gridCase);

            this.grpCase.Text = "";
            this.grpCase.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            var cardCase = MkCaseCard("مشخصات پرونده", caseHost, this.grpCase);

            // ═══ پانل قابل‌اسکرول فیلدها (سه کارت پشت‌سرهم) ═══════════════════
            // ترتیب افزودن معکوسِ نمایش است (هر Dock=Top بالای قبلی می‌نشیند).
            FieldsScrollPanel fieldsPanel = new FieldsScrollPanel();
            fieldsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            fieldsPanel.AutoScroll = true;
            fieldsPanel.Padding = new System.Windows.Forms.Padding(10, 10, 10, 10);
            fieldsPanel.Controls.Add(cardCase);
            fieldsPanel.Controls.Add(cardPhysical);
            fieldsPanel.Controls.Add(cardHead);

            // آموزش — رفع باگ Tab نامنظم: چون گروه‌ها به این ترتیب (grpCase،
            // grpPhysical، grpHead) اضافه شدند، بدون این سه خط، Tab پیش‌فرض
            // از grpCase (پایین صفحه) شروع می‌شد نه grpHead (بالای صفحه).
            // این مقادیر ترتیب واقعی/بصری از بالا به پایین را تضمین می‌کند.
            this.grpHead.TabIndex = 0;
            this.grpPhysical.TabIndex = 1;
            this.grpCase.TabIndex = 2;

            // ═══ سمت چپ: عکس‌ها (بالا) + فیلتر + گرید ═════════════════════════
            this.picPhoto.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picPhoto.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picPhoto.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.picPhoto.TabStop = false;
            this.btnBrowsePhoto.Text = "عکس پرسنلی";
            this.btnBrowsePhoto.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnBrowsePhoto.Height = 34;
            this.btnBrowsePhoto.Click += new System.EventHandler(this.btnBrowsePhoto_Click);

            System.Windows.Forms.Panel personalPhotoCell = new System.Windows.Forms.Panel();
            personalPhotoCell.Dock = System.Windows.Forms.DockStyle.Fill;
            personalPhotoCell.Padding = new System.Windows.Forms.Padding(4);
            personalPhotoCell.Controls.Add(this.picPhoto);
            personalPhotoCell.Controls.Add(this.btnBrowsePhoto);

            this.picFamilyPhoto.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picFamilyPhoto.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picFamilyPhoto.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.picFamilyPhoto.TabStop = false;
            this.btnBrowseFamilyPhoto.Text = "عکس جمعی";
            this.btnBrowseFamilyPhoto.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnBrowseFamilyPhoto.Height = 34;
            this.btnBrowseFamilyPhoto.Click += new System.EventHandler(this.btnBrowseFamilyPhoto_Click);

            System.Windows.Forms.Panel familyPhotoCell = new System.Windows.Forms.Panel();
            familyPhotoCell.Dock = System.Windows.Forms.DockStyle.Fill;
            familyPhotoCell.Padding = new System.Windows.Forms.Padding(4);
            familyPhotoCell.Controls.Add(this.picFamilyPhoto);
            familyPhotoCell.Controls.Add(this.btnBrowseFamilyPhoto);

            System.Windows.Forms.TableLayoutPanel photoBar = new System.Windows.Forms.TableLayoutPanel();
            photoBar.Dock = System.Windows.Forms.DockStyle.Top;
            photoBar.Height = 190;
            photoBar.ColumnCount = 2;
            photoBar.RowCount = 1;
            photoBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            photoBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            photoBar.Controls.Add(personalPhotoCell, 0, 0);
            photoBar.Controls.Add(familyPhotoCell, 1, 0);

            this.lblServiceStatusFilter.Text = "فیلتر وضعیت خدمات";
            this.lblServiceStatusFilter.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblServiceStatusFilter.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblServiceStatusFilter.Width = 150;
            this.cmbServiceStatusFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbServiceStatusFilter.Margin = new System.Windows.Forms.Padding(0);

            System.Windows.Forms.Panel filterBar = new System.Windows.Forms.Panel();
            filterBar.Dock = System.Windows.Forms.DockStyle.Top;
            filterBar.Height = 40;
            filterBar.Padding = new System.Windows.Forms.Padding(4, 6, 4, 4);
            System.Windows.Forms.Panel filterInner = new System.Windows.Forms.Panel();
            filterInner.Dock = System.Windows.Forms.DockStyle.Fill;
            filterInner.Controls.Add(this.cmbServiceStatusFilter);
            filterInner.Controls.Add(this.lblServiceStatusFilter);
            filterBar.Controls.Add(filterInner);

            this.dgvCases.AllowUserToAddRows = false;
            this.dgvCases.AllowUserToDeleteRows = false;
            this.dgvCases.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvCases.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCases.MultiSelect = false;
            this.dgvCases.ReadOnly = true;
            this.dgvCases.RowHeadersWidth = 51;
            this.dgvCases.RowTemplate.Height = 24;
            this.dgvCases.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvCases.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvCases_CellClick);

            System.Windows.Forms.Panel gridWrap = new System.Windows.Forms.Panel();
            gridWrap.Dock = System.Windows.Forms.DockStyle.Fill;
            gridWrap.Padding = new System.Windows.Forms.Padding(6);
            gridWrap.Controls.Add(this.dgvCases);

            System.Windows.Forms.Panel leftPanel = new System.Windows.Forms.Panel();
            leftPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            leftPanel.Controls.Add(gridWrap);
            leftPanel.Controls.Add(filterBar);
            leftPanel.Controls.Add(photoBar);

            // ═══ نوار ابزار بالا: دو میانبر سریع (بند ۵) ══════════════════════
            // آموزش — به درخواست کاربر: نام «اعضای فامیل» به «اعضاء خانواده»
            // تغییر کرد و هر دو دکمه با ظاهری بزرگ‌تر/رنگی برای دسترسی سریع و
            // مدرن‌تر بازطراحی شدند؛ «انتخاب محل ذخیره» از این نوار به پایین
            // فرم (کنار خروجی‌ها) منتقل شد.
            System.Windows.Forms.FlowLayoutPanel toolbar = new System.Windows.Forms.FlowLayoutPanel();
            toolbar.Dock = System.Windows.Forms.DockStyle.Fill;
            toolbar.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            toolbar.Padding = new System.Windows.Forms.Padding(8, 8, 8, 4);
            StyleBtn(this.btnFamily, "اعضاء خانواده", 150, 36);
            this.btnFamily.Click += new System.EventHandler(this.btnFamily_Click);
            this.btnFamily.BackColor = CaseManagement.Helpers.UiTheme.Success;
            StyleBtn(this.btnDocs, "اسناد", 120, 36);
            this.btnDocs.Click += new System.EventHandler(this.btnDocs_Click);
            this.btnDocs.BackColor = CaseManagement.Helpers.UiTheme.PrimaryLight;
            toolbar.Controls.Add(this.btnFamily);
            toolbar.Controls.Add(this.btnDocs);

            // ═══ نوار پایین: عملیات + خروجی‌ها همه در یک ردیف پیوسته ══════════
            // آموزش — به درخواست کاربر: قبلاً «عملیات» و «خروجی‌ها» دو ردیف
            // جدا بودند. حالا همه در یک FlowLayoutPanel واحد پشت‌سر هم می‌آیند
            // (دقیقاً بعد از دکمه «جستجو») با فاصله یکنواخت؛ اگر عرض فرم کافی
            // نباشد، خودکار به خط بعد می‌شکند (WrapContents) نه اینکه از فرم
            // بیرون بزند.
            bottomActionsRow = new System.Windows.Forms.FlowLayoutPanel();
            bottomActionsRow.Dock = System.Windows.Forms.DockStyle.Fill;
            bottomActionsRow.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            bottomActionsRow.WrapContents = true;
            // آموزش — رفع باگ «دکمه‌ی خروجی جمعی ناپدید شد»: این نوار WrapContents
            // دارد، پس وقتی مجموع عرض دکمه‌ها از عرض فرم بیشتر شود به خط بعد
            // می‌شکند. اما ارتفاعِ ردیفِ نگه‌دارنده ثابت (۵۰px) بود و فقط یک خط
            // جا می‌داد؛ در نتیجه خطِ دومِ دکمه‌ها (خروجی جمعی و محل ذخیره)
            // نامرئی می‌شد — دقیقاً وقتی رخ داد که دو دکمه‌ی کارت شناسایی اضافه
            // شدند. با AutoSize، نوار به‌اندازه‌ی خطوطش بلند می‌شود و هیچ دکمه‌ای
            // هرگز پنهان نمی‌ماند (و با تغییر عرض پنجره هم خودکار تنظیم می‌شود).
            // AutoSize عمداً خاموش است: در FlowLayoutPanel هر دو بُعد را بزرگ
            // می‌کند، یعنی به‌جای شکستنِ خط، خودِ نوار در عرض رشد می‌کرد و
            // دکمه‌های انتهایی از لبه‌ی فرم بیرون می‌زدند (در تست تصویری دیده
            // شد). با Dock=Fill عرض به والد مقید می‌شود ⇒ شکستِ خط درست کار
            // می‌کند، و ارتفاعِ لازم را کدِ فرم (AdjustBottomBarHeight) حساب
            // و به ردیفِ نگه‌دارنده اعمال می‌کند.
            bottomActionsRow.AutoSize = false;
            bottomActionsRow.Padding = new System.Windows.Forms.Padding(8, 6, 8, 6);
            StyleBtn(this.btnNew, "جدید", 82, 32); this.btnNew.Click += new System.EventHandler(this.btnNew_Click);
            StyleBtn(this.btnSave, "ذخیره", 82, 32); this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            StyleBtn(this.btnEdit, "ویرایش", 82, 32); this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            StyleBtn(this.btnDelete, "حذف", 82, 32); this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            StyleBtn(this.btnSearch, "جستجو", 82, 32); this.btnSearch.Click += new System.EventHandler(this.btnSearch_Click);
            this.lblExportSection.Text = "خروجی‌ها:"; this.lblExportSection.AutoSize = false; this.lblExportSection.Size = new System.Drawing.Size(60, 32); this.lblExportSection.TextAlign = System.Drawing.ContentAlignment.MiddleRight; this.lblExportSection.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            StyleBtn(this.btnPrint, "چاپ", 74, 32); this.btnPrint.Click += new System.EventHandler(this.btnPrint_Click);
            StyleBtn(this.btnExportWord, "ورد", 62, 32); this.btnExportWord.Click += new System.EventHandler(this.btnExportWord_Click);
            StyleBtn(this.btnExportPdf, "پی دی اف", 78, 32); this.btnExportPdf.Click += new System.EventHandler(this.btnExportPdf_Click);
            StyleBtn(this.btnExportExcel, "اکسل", 62, 32); this.btnExportExcel.Click += new System.EventHandler(this.btnExportExcel_Click);
            StyleBtn(this.btnBatchExport, "خروجی جمعی", 104, 32); this.btnBatchExport.Click += new System.EventHandler(this.btnBatchExport_Click);
            StyleBtn(this.btnChooseStorageFolder, "محل ذخیره", 96, 32); this.btnChooseStorageFolder.Click += new System.EventHandler(this.btnChooseStorageFolder_Click);
            bottomActionsRow.Controls.Add(this.btnNew);
            bottomActionsRow.Controls.Add(this.btnSave);
            bottomActionsRow.Controls.Add(this.btnEdit);
            bottomActionsRow.Controls.Add(this.btnDelete);
            bottomActionsRow.Controls.Add(this.btnSearch);
            bottomActionsRow.Controls.Add(this.lblExportSection);
            bottomActionsRow.Controls.Add(this.btnPrint);
            bottomActionsRow.Controls.Add(this.btnExportWord);
            bottomActionsRow.Controls.Add(this.btnExportPdf);
            bottomActionsRow.Controls.Add(this.btnExportExcel);
            bottomActionsRow.Controls.Add(this.btnBatchExport);
            bottomActionsRow.Controls.Add(this.btnChooseStorageFolder);

            System.Windows.Forms.TableLayoutPanel bottomBar = new System.Windows.Forms.TableLayoutPanel();
            bottomBar.Dock = System.Windows.Forms.DockStyle.Fill;
            bottomBar.ColumnCount = 1;
            bottomBar.RowCount = 1;
            // ردیف هم‌اندازه‌ی محتوا (نه درصدِ ثابت) تا ارتفاعِ خودکارِ نوارِ
            // دکمه‌ها واقعاً منتقل شود.
            bottomBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            bottomBar.Controls.Add(bottomActionsRow, 0, 0);

            // ═══ ریشه چیدمان ═════════════════════════════════════════════════
            System.Windows.Forms.TableLayoutPanel rootLayout = new System.Windows.Forms.TableLayoutPanel();
            rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            rootLayout.ColumnCount = 2;
            rootLayout.RowCount = 3;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 62F));
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 38F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            // ردیف نوار دکمه‌ها — ارتفاع اولیه برای دو خط دکمه؛ کدِ فرم
            // (AdjustBottomBarHeight) آن را با تعداد خطوطِ واقعی تنظیم می‌کند تا
            // هیچ دکمه‌ای در هیچ عرضی پنهان نماند.
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 96F));
            this.rootLayout = rootLayout;
            rootLayout.Controls.Add(toolbar, 0, 0);
            rootLayout.SetColumnSpan(toolbar, 2);
            rootLayout.Controls.Add(fieldsPanel, 0, 1);
            rootLayout.Controls.Add(leftPanel, 1, 1);
            rootLayout.Controls.Add(bottomBar, 0, 2);
            rootLayout.SetColumnSpan(bottomBar, 2);

            // آموزش — رفع باگ Tab نامنظم (بند ۵): چون leftPanel (گرید/عکس‌ها،
            // سمت چپ) قبلاً بین fieldsPanel و bottomBar در ترتیب پیش‌فرض قرار
            // می‌گرفت، فوکوس بعد از آخرین فیلد به‌جای دکمه «ذخیره» ابتدا به
            // کنترل‌های سمت چپ می‌پرید. با این ترتیب صریح، Tab همیشه:
            // toolbar → fieldsPanel (فیلدها) → bottomBar (ذخیره) → leftPanel
            toolbar.TabIndex = 0;
            fieldsPanel.TabIndex = 1;
            bottomBar.TabIndex = 2;
            leftPanel.TabIndex = 3;

            //
            // FrmCase
            //
            // ─── مهاجرت به مقیاسِ DPI (لایه ۲ چارچوب چیدمان واکنش‌گرا) ────────
            // آموزش — دو خط، و ترتیبشان مهم است:
            //   AutoScaleDimensions مبنای طراحی را اعلام می‌کند (۹۶dpi = مقیاس
            //   ۱۰۰٪). بدون آن، مقدارش (۰،۰) می‌ماند و WinForms هیچ نسبتی برای
            //   مقیاس‌کردن ندارد، پس AutoScaleMode.Dpi عملاً بی‌اثر می‌شود.
            //   AutoScaleMode.Dpi می‌گوید مبنای مقیاس «نمایشگر» است، نه فونت.
            //   (چرایی انتخاب Dpi به‌جای Font در سربرگ ResponsiveLayout.cs)
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            // آموزش — ارتفاع فرم افزایش یافت (۷۲۰→۱۰۴۰) تا سه گروه (سرپرست/
            // جسمی/پرونده) بدون فشردگی و با فاصله حرفه‌ای جا شوند.
            this.ClientSize = new System.Drawing.Size(1180, 880);
            this.Controls.Add(rootLayout);
            // تمام‌صفحه‌ی خودکار (درخواست کاربر). حداقلِ اندازه و بیشینه‌سازی در
            // FrmCase_Load اعمال می‌شود — آن‌جا اندازه‌ی واقعی صفحه در دسترس است.
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "FrmCase";
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ثبت پرونده";
            this.Load += new System.EventHandler(this.FrmCase_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvCases)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPhoto)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picFamilyPhoto)).EndInit();
            this.ResumeLayout(false);

        }

        // برچسب فیلد کوتاه: تنظیم متن/محل/اندازه/تراز در یک خط (بدون Dock/Anchor
        // ژنریک) — هر تماس مستقل و صریح است، مطابق سبک کلاسیک Designer.
        // آموزش — رفع باگ چیدمان: قبلاً MiddleRight بود، یعنی متن به لبه دور
        // از تکست‌باکس (سمت راست جعبه لیبل) می‌چسبید و برای عنوان‌های کوتاه
        // (مثل «مذهب») فاصله خالی بزرگی تا تکست‌باکس ایجاد می‌شد. با MiddleLeft
        // متن به لبه نزدیک به تکست‌باکس می‌چسبد؛ متن فارسی همچنان از راست به
        // چپ خوانده می‌شود (این فقط محل قرارگیری بلوک متن در جعبه است، نه جهت آن)،
        // و بلافاصله بعد از پایان لیبل، تکست‌باکس شروع می‌شود.
        private static void SetLbl(System.Windows.Forms.Label lbl, string text, int x, int y)
        {
            lbl.Text = text;
            lbl.AutoSize = false;
            lbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lbl.Location = new System.Drawing.Point(x, y);
            lbl.Size = new System.Drawing.Size(150, 22);
        }

        // ─── چیدمان کارتیِ فیلدها (بازطراحی) ──────────────────────────────────
        // شبکه‌ی سه‌ستونه‌ی فیلدها؛ ردیف‌ها AutoSize‌اند تا ارتفاع دقیقاً
        // به‌اندازه‌ی محتوا باشد و فضای خالیِ نامتعارف نسازد.
        private static System.Windows.Forms.TableLayoutPanel MkCaseFieldGrid()
        {
            var tlp = new System.Windows.Forms.TableLayoutPanel();
            tlp.Dock = System.Windows.Forms.DockStyle.Top;
            tlp.AutoSize = true;
            tlp.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tlp.Padding = new System.Windows.Forms.Padding(14, 8, 14, 10);
            tlp.ColumnCount = 3;
            tlp.BackColor = System.Drawing.Color.Transparent;
            for (int i = 0; i < 3; i++)
                tlp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                    System.Windows.Forms.SizeType.Percent, 100F / 3F));
            return tlp;
        }

        // افزودن یک فیلد به شبکه. خودِ کنترلِ ورودی همان شیء قبلی می‌ماند، پس
        // نام، رویدادها و هر کدی که با آن کار می‌کند دست‌نخورده باقی است.
        private static CaseManagement.Helpers.FieldBox AddCaseField(
            System.Windows.Forms.TableLayoutPanel grid,
            System.Windows.Forms.Label captionLabel, string captionText,
            System.Windows.Forms.Control field)
        {
            var box = new CaseManagement.Helpers.FieldBox(captionLabel, captionText, field);
            box.Dock = System.Windows.Forms.DockStyle.Top;
            grid.Controls.Add(box);
            return box;
        }

        // کارت سفیدِ گردگوشه با سربرگ عنوان. GroupBoxِ اصلی به‌عنوان میزبانِ
        // محتوا حفظ می‌شود (حذف نمی‌شود) تا هیچ ارجاعی در کد نشکند، فقط
        // قاب/عنوانِ بومی‌اش خاموش شده و کارت جای آن را گرفته است.
        private static System.Windows.Forms.Panel MkCaseCard(
            string title, System.Windows.Forms.Control content, System.Windows.Forms.GroupBox host)
        {
            host.Dock = System.Windows.Forms.DockStyle.Top;
            host.AutoSize = true;
            host.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            host.Padding = new System.Windows.Forms.Padding(0);
            host.BackColor = System.Drawing.Color.Transparent;
            host.Controls.Add(content);

            var header = new System.Windows.Forms.Label();
            header.Dock = System.Windows.Forms.DockStyle.Top;
            header.Height = 40;
            header.Text = title;
            header.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeMedium);
            header.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            header.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            header.Padding = new System.Windows.Forms.Padding(0, 0, 18, 0);
            header.BackColor = System.Drawing.Color.Transparent;

            var card = new CaseManagement.Helpers.SectionCard();
            card.Dock = System.Windows.Forms.DockStyle.Top;
            card.AutoSize = true;
            card.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            card.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
            card.Padding = new System.Windows.Forms.Padding(2, 2, 2, 10);
            card.Controls.Add(host);
            card.Controls.Add(header);
            return card;
        }

        // دکمه نوار ابزار: متن/اندازه/فونت/فاصله در یک خط — دکمه‌ها فونت
        // بزرگ‌تر و بولد و فاصله یکنواخت دارند تا نوار دکمه‌ها منظم و حرفه‌ای
        // دیده شود (به درخواست کاربر برای دسته‌بندی و نظم).
        private static void StyleBtn(System.Windows.Forms.Button btn, string text, int width, int height)
        {
            btn.Text = text;
            btn.Size = new System.Drawing.Size(width, height);
            // فونت کمی کوچک‌تر و سبک‌تر برای ظاهر حرفه‌ای و جمع‌وجورتر (به‌جای
            // دکمه‌های بزرگ و سنگین قبلی) — همه‌ی دکمه‌ها یکدست می‌شوند.
            btn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            btn.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        }

        // آموزش — رفع باگ اسکرول در فرم RTL: چون فرم RightToLeftLayout=true
        // دارد، Windows به‌صورت خودکار WS_EX_LAYOUTRTL را به همه HWNDهای
        // فرزند (از جمله این Panel) به ارث می‌رساند؛ برای عناصر بومی مثل
        // اسکرول‌بار همین باعث می‌شود اسکرول‌بار روی لبه چپِ خودِ این پنل
        // (یعنی مرز مشترک با پنل گرید/عکس‌ها) ظاهر شود، نه روی لبه راست
        // بیرونی فرم که کاربر انتظار دارد. WS_EX_NOINHERITLAYOUT این پنل را
        // از آن ارث‌بری معاف می‌کند تا اسکرول‌بار در سمت راست واقعی بماند؛
        // چیدمان دستی لیبل/تکست‌باکس داخل آن (که از قبل مستقل از این پرچم
        // است) هیچ تغییری نمی‌کند.
        private class FieldsScrollPanel : System.Windows.Forms.Panel
        {
            protected override System.Windows.Forms.CreateParams CreateParams
            {
                get
                {
                    const int WS_EX_NOINHERITLAYOUT = 0x00100000;
                    System.Windows.Forms.CreateParams cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_NOINHERITLAYOUT;
                    return cp;
                }
            }
        }

        // با تغییر ولایت، فهرست ولسوالی‌ها بازسازی می‌شود
        private void txtProvince_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            string province = this.txtProvince.Text;
            this.txtDistrict.Items.Clear();
            this.txtDistrict.Items.AddRange(CaseManagement.Helpers.AfghanGeoData.GetDistricts(province));
        }

        #endregion
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.DataGridView dgvCases;
        private System.Windows.Forms.TextBox txtPhotoPath;
        private CaseManagement.Helpers.PersianDatePicker dtpCaseDate;
        private System.Windows.Forms.Button btnBrowsePhoto;
        private System.Windows.Forms.Button btnBrowseFamilyPhoto;
        private System.Windows.Forms.TextBox txtFamilyPhotoPath;
        private System.Windows.Forms.PictureBox picPhoto;
        private System.Windows.Forms.PictureBox picFamilyPhoto;
        private System.Windows.Forms.Label lblCaseDate;
        private System.Windows.Forms.TextBox txtRelationshipToFamily;
        private System.Windows.Forms.ComboBox txtCoveredByOrg;
        private System.Windows.Forms.TextBox txtJob;
        private System.Windows.Forms.TextBox txtSkill;
        private System.Windows.Forms.ComboBox txtDisabilityDegree;
        private System.Windows.Forms.ComboBox txtDisabilityType;
        private System.Windows.Forms.TextBox txtMigrationCardType;
        private System.Windows.Forms.ComboBox txtMaritalStatus;
        private System.Windows.Forms.ComboBox txtEducationLevel;
        private System.Windows.Forms.ComboBox txtServiceStatus;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Button btnFamily;
        private System.Windows.Forms.Button btnDocs;
        private System.Windows.Forms.Button btnChooseStorageFolder;
        private System.Windows.Forms.Button btnExportPdf;
        private System.Windows.Forms.Button btnExportWord;
        private System.Windows.Forms.Button btnExportExcel;
        private System.Windows.Forms.Button btnBatchExport;
        // آموزش — به فیلد ارتقا یافت تا کد فرم بتواند هنگام تغییر اندازه،
        // بیشینه‌ی عرضش را به عرضِ والد مقید کند (توضیح کامل کنار ساختش).
        internal System.Windows.Forms.FlowLayoutPanel bottomActionsRow;
        internal System.Windows.Forms.TableLayoutPanel rootLayout;
        // کانتینرِ فیلد «دلیل قطع موقت» — برای پنهان/نمایان‌کردن کلِ فیلد
        // هماهنگ با منطقِ موجود در FrmCase.cs.
        private CaseManagement.Helpers.FieldBox caseFieldStopReason;
        private System.Windows.Forms.Button btnPrint;
        private System.Windows.Forms.Label lblServiceStatusFilter;
        private System.Windows.Forms.CheckBox chkHeadHealthy;
        private System.Windows.Forms.ComboBox cmbServiceStatusFilter;
        private System.Windows.Forms.Label lblExportSection;
        private CaseManagement.Helpers.PersianDatePicker dtpSurveyDate;
        private System.Windows.Forms.Label label28;
        private System.Windows.Forms.TextBox txtLocationAddress;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.TextBox txtSurveyors;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.TextBox txtUrgentSituation;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.TextBox txtPhone;
        private System.Windows.Forms.TextBox txtRelativePhone;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TextBox txtHeadCurrentResidence;
        private System.Windows.Forms.ComboBox txtRequestType;
        private System.Windows.Forms.ComboBox txtPriorityLevel;
        private System.Windows.Forms.TextBox txtHeadFullName;
        private System.Windows.Forms.TextBox txtHeadFatherName;
        private System.Windows.Forms.ComboBox txtHeadSadat;
        private System.Windows.Forms.ComboBox txtReligion;
        private System.Windows.Forms.TextBox txtHeadTazkiraNo;
        private System.Windows.Forms.TextBox txtHeadOriginalResidence;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.ComboBox txtProvince;
        private System.Windows.Forms.ComboBox txtDistrict;
        private System.Windows.Forms.TextBox txtFormNo;
        private System.Windows.Forms.TextBox txtCode;
        private System.Windows.Forms.TextBox txtCaseNo;
        private System.Windows.Forms.ComboBox txtZone;
        private System.Windows.Forms.Label lblCode;
        private System.Windows.Forms.Label lblFormNo;
        private System.Windows.Forms.Label lblCaseNo;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblStopReason;
        private System.Windows.Forms.TextBox txtStopReason;
        private System.Windows.Forms.GroupBox grpHead;
        private System.Windows.Forms.GroupBox grpPhysical;
        private System.Windows.Forms.GroupBox grpCase;
    }
}

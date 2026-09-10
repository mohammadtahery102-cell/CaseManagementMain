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
            this.btnHistory = new System.Windows.Forms.Button();
            this.dgvCases = new System.Windows.Forms.DataGridView();
            this.txtPhotoPath = new System.Windows.Forms.TextBox();
            this.dtpCaseDate = new CaseManagement.Helpers.PersianDatePicker();
            this.btnBrowsePhoto = new System.Windows.Forms.Button();
            this.btnClearPhoto = new System.Windows.Forms.Button();
            this.btnBrowseFamilyPhoto = new System.Windows.Forms.Button();
            this.btnClearFamilyPhoto = new System.Windows.Forms.Button();
            this.txtFamilyPhotoPath = new System.Windows.Forms.TextBox();
            this.picPhoto = new System.Windows.Forms.PictureBox();
            this.picFamilyPhoto = new System.Windows.Forms.PictureBox();
            this.lblCaseDate = new System.Windows.Forms.Label();
            this.txtRelationshipToFamily = new System.Windows.Forms.TextBox();
            this.txtCoveredByOrg = new System.Windows.Forms.ComboBox();
            this.txtCoveredByOrgNames = new System.Windows.Forms.TextBox();
            this.lblCoveredByOrgNames = new System.Windows.Forms.Label();
            this.txtJob = new System.Windows.Forms.TextBox();
            this.txtSkill = new System.Windows.Forms.TextBox();
            this.txtDisabilityDegree = new System.Windows.Forms.ComboBox();
            this.txtDisabilityType = new System.Windows.Forms.ComboBox();
            this.txtMigrationCardType = new System.Windows.Forms.TextBox();
            this.txtMaritalStatus = new System.Windows.Forms.ComboBox();
            // Phase 3 (بازبینی) — بخش‌های اختصاصیِ نوع درخواست (ایتام/معلولیت/مهاجرت).
            this.label31 = new System.Windows.Forms.Label();
            this.txtMainResidenceProvince = new System.Windows.Forms.TextBox();
            this.label32 = new System.Windows.Forms.Label();
            this.txtMainResidenceDistrict = new System.Windows.Forms.TextBox();
            this.label33 = new System.Windows.Forms.Label();
            this.txtMainResidenceVillage = new System.Windows.Forms.TextBox();
            this.label34 = new System.Windows.Forms.Label();
            this.txtFatherDeathCause = new System.Windows.Forms.ComboBox();
            this.label35 = new System.Windows.Forms.Label();
            this.txtDisabilityCause = new System.Windows.Forms.ComboBox();
            this.label36 = new System.Windows.Forms.Label();
            this.txtDisabilityDescription = new System.Windows.Forms.TextBox();
            this.label37 = new System.Windows.Forms.Label();
            this.txtSpecialNeeds = new System.Windows.Forms.TextBox();
            this.label38 = new System.Windows.Forms.Label();
            this.txtDisabilityCardStatus = new System.Windows.Forms.ComboBox();
            this.label39 = new System.Windows.Forms.Label();
            this.txtDisabilityCardNumber = new System.Windows.Forms.TextBox();
            this.label40 = new System.Windows.Forms.Label();
            this.txtHasMigrationCard = new System.Windows.Forms.ComboBox();
            this.label41 = new System.Windows.Forms.Label();
            this.txtMigrationCardNumber = new System.Windows.Forms.TextBox();
            this.label42 = new System.Windows.Forms.Label();
            this.dtpDepartureDate = new CaseManagement.Helpers.PersianDatePicker();
            this.label43 = new System.Windows.Forms.Label();
            this.dtpArrivalDate = new CaseManagement.Helpers.PersianDatePicker();
            this.label44 = new System.Windows.Forms.Label();
            this.txtAssistanceDurationMonths = new System.Windows.Forms.TextBox();
            // Phase 4 — فیلدهای ماژولِ تخصصی (TblOrphan/TblDisability/TblMigrant).
            this.label45 = new System.Windows.Forms.Label();
            this.txtFatherStatus = new System.Windows.Forms.ComboBox();
            this.label46 = new System.Windows.Forms.Label();
            this.dtpFatherDeathDate = new CaseManagement.Helpers.PersianDatePicker();
            // همان علتِ ریشه‌ایِ تاریخ‌های کارت معلولیت (نگاه کنید پایین‌تر):
            // بدونِ حالتِ خالی، هر پروندهٔ ایتام تاریخ فوتِ پدر را «امروز» ثبت
            // می‌کرد. متد مشترکِ SetDatePickerValue هر سه را با هم درست می‌کند.
            this.dtpFatherDeathDate.ShowCheckBox = true;
            this.dtpFatherDeathDate.Checked = false;
            this.label47 = new System.Windows.Forms.Label();
            this.txtMotherStatus = new System.Windows.Forms.ComboBox();
            this.label48 = new System.Windows.Forms.Label();
            this.txtOrphanSchoolName = new System.Windows.Forms.TextBox();
            this.label49 = new System.Windows.Forms.Label();
            this.txtOrphanEducationLevel = new System.Windows.Forms.ComboBox();
            this.chkIsStudent = new System.Windows.Forms.CheckBox();
            this.lblIsStudent = new System.Windows.Forms.Label();
            this.label50 = new System.Windows.Forms.Label();
            this.txtOrphanNotes = new System.Windows.Forms.TextBox();
            this.label51 = new System.Windows.Forms.Label();
            this.txtGuardianName = new System.Windows.Forms.TextBox();
            this.label52 = new System.Windows.Forms.Label();
            this.lblGuardianPhoto = new System.Windows.Forms.Label();
            this.txtGuardianRelationship = new System.Windows.Forms.ComboBox();
            this.label53 = new System.Windows.Forms.Label();
            this.txtCardIssuer = new System.Windows.Forms.TextBox();
            this.label54 = new System.Windows.Forms.Label();
            this.dtpDisabilityIssueDate = new CaseManagement.Helpers.PersianDatePicker();
            // آموزش — این سه تاریخ *اختیاری*اند: بسیاری از معلولان اصلاً کارتِ
            // دولتی ندارند (DisabilityCardStatus = «ندارد») و بسیاری از
            // پرونده‌های ایتام تاریخ فوتِ ثبت‌شده ندارند. بدونِ ShowCheckBox
            // کنترل هیچ حالتِ «خالی» نداشت، پس ذخیره همیشه تاریخِ *امروز* را
            // می‌نوشت و هر رکورد ادعا می‌کرد کارتی امروز صادر و امروز منقضی
            // شده است. چک‌باکس همان قراردادِ DateTimePicker.ShowCheckBox است.
            this.dtpDisabilityIssueDate.ShowCheckBox = true;
            this.dtpDisabilityIssueDate.Checked = false;
            this.label55 = new System.Windows.Forms.Label();
            this.dtpDisabilityExpiryDate = new CaseManagement.Helpers.PersianDatePicker();
            this.dtpDisabilityExpiryDate.ShowCheckBox = true;
            this.dtpDisabilityExpiryDate.Checked = false;
            this.label56 = new System.Windows.Forms.Label();
            this.txtDisabilityNotes = new System.Windows.Forms.TextBox();
            this.label57 = new System.Windows.Forms.Label();
            this.txtOriginCountry = new System.Windows.Forms.TextBox();
            this.label58 = new System.Windows.Forms.Label();
            this.txtDestinationCountry = new System.Windows.Forms.TextBox();
            this.label59 = new System.Windows.Forms.Label();
            this.txtMigrantNotes = new System.Windows.Forms.TextBox();
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
            this.btnExportCaseFile = new System.Windows.Forms.Button();
            this.btnExportExcel = new System.Windows.Forms.Button();
            this.btnBatchExport = new System.Windows.Forms.Button();
            this.btnPrint = new System.Windows.Forms.Button();
            this.lblServiceStatusFilter = new System.Windows.Forms.Label();
            this.cmbServiceStatusFilter = new System.Windows.Forms.ComboBox();
            this.lblExportSection = new System.Windows.Forms.Label();
            this.dtpSurveyDate = new CaseManagement.Helpers.PersianDatePicker();
            this.label28 = new System.Windows.Forms.Label();
            this.txtLocationAddress = new System.Windows.Forms.TextBox();
            // الزام نسخهٔ تحویلی (مورد ۷) — «سایت»: محلِ میدانیِ ارائهٔ خدمت.
            // ComboBox با DropDownStyle = DropDown (نه DropDownList): مقادیرِ
            // شناخته‌شده پیشنهاد می‌شوند ولی سایتِ تازه هم تایپ‌شدنی است، چون
            // فهرست هنوز تثبیت نشده و بستنِ آن، ثبتِ پروندهٔ یک سایتِ جدید را
            // غیرممکن می‌کرد.
            this.lblSite = new System.Windows.Forms.Label();
            this.txtSite = new System.Windows.Forms.ComboBox();
            this.txtSite.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.txtSite.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.txtSite.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;

            // Phase 3 — معرف (Referrer): نام و شمارهٔ تماسِ معرف، برای همهٔ انواع پرونده.
            this.label29 = new System.Windows.Forms.Label();
            this.txtReferrerName = new System.Windows.Forms.TextBox();
            this.label30 = new System.Windows.Forms.Label();
            this.txtReferrerPhone = new System.Windows.Forms.TextBox();
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
            this.lblHeadIdCardType = new System.Windows.Forms.Label();
            this.cmbHeadIdCardType = new System.Windows.Forms.ComboBox();
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
            this.lblSuspensionReason = new System.Windows.Forms.Label();
            this.txtSuspensionReason = new System.Windows.Forms.ComboBox();
            this.grpHead = new System.Windows.Forms.GroupBox();
            this.grpPhysical = new System.Windows.Forms.GroupBox();
            this.grpCase = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCases)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picPhoto)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picFamilyPhoto)).BeginInit();
            this.SuspendLayout();

            // ═══ کمبوهای وضعیت ════════════════════════════════════════════════
            // آیتم‌ها دیگر اینجا هاردکد نیستند: در زمانِ اجرا
            // ConfigureServiceStatusControls از TblLookup پرشان می‌کند. این
            // مقادیرِ اولیه فقط برای حالتی است که دیتابیس هنوز آماده نیست، و
            // از CaseDomain (منبع واحد) خوانده می‌شوند تا هرگز از قید دیتابیس
            // و بقیهٔ فرم‌ها عقب نمانند.
            this.txtServiceStatus.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtServiceStatus.Items.AddRange(CaseManagement.Helpers.CaseDomain.ServiceStatuses);
            this.cmbServiceStatusFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbServiceStatusFilter.Items.Add("همه");
            this.cmbServiceStatusFilter.Items.AddRange(CaseManagement.Helpers.CaseDomain.ServiceStatuses);
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

            // ─── نوع پرونده ──────────────────────────────────────────────────
            // فهرست در زمان اجرا از TblLookup پر می‌شود (LoadLookupCombos)؛
            // این مقدارِ اولیه از CaseDomain می‌آید تا با فهرستِ استاندارد یکی
            // بماند. قبلاً «یتیم»/«کهولت سن» اینجا هاردکد بودند و با نقشِ عضو
            // («یتیم» در MemberRole) اشتباه گرفته می‌شدند.
            this.txtRequestType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtRequestType.Items.AddRange(CaseManagement.Helpers.CaseDomain.CaseTypes);

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
            // ─── نوع تذکره سرپرست (بخش ۳) ────────────────────────────────────
            // همان منبع مقادیر که FrmFamily برای عضو استفاده می‌کند، تا
            // اعتبارسنجی و آمار در هر دو فرم دقیقاً یکی بماند.
            CaseManagement.Helpers.IdCardHelper.FillCombo(this.cmbHeadIdCardType);

            var gridHead = MkCaseFieldGrid();
            AddCaseField(gridHead, this.label6,  "نام سرپرست و تخلص",   this.txtHeadFullName);
            AddCaseField(gridHead, this.label7,  "نام پدر سرپرست",      this.txtHeadFatherName);
            AddCaseField(gridHead, this.label8,  "سیادت سرپرست",        this.txtHeadSadat);
            AddCaseField(gridHead, this.label9,  "مذهب",                this.txtReligion);
            AddCaseField(gridHead, this.label10, "شماره تذکره سرپرست",  this.txtHeadTazkiraNo);
            AddCaseField(gridHead, this.lblHeadIdCardType, "نوع تذکره سرپرست", this.cmbHeadIdCardType);
            AddCaseField(gridHead, this.label11, "سکونت اصلی سرپرست",   this.txtHeadOriginalResidence);
            AddCaseField(gridHead, this.label12, "سکونت فعلی سرپرست",   this.txtHeadCurrentResidence);
            AddCaseField(gridHead, this.label13, "نسبت با سایر اعضا",   this.txtRelationshipToFamily);
            AddCaseField(gridHead, this.label14, "شماره تماس",          this.txtPhone);
            AddCaseField(gridHead, this.label15, "شماره تماس اقارب",    this.txtRelativePhone);
            AddCaseField(gridHead, this.label22, "وضعیت تأهل",          this.txtMaritalStatus);
            AddCaseField(gridHead, this.label26, "تحصیلات",             this.txtEducationLevel);
            AddCaseField(gridHead, this.label17, "شغل",                 this.txtJob);
            AddCaseField(gridHead, this.label18, "مهارت",               this.txtSkill);

            // ─── ستونِ عکسِ سرپرست، کنارِ فیلدهای همان کارت ───────────────────
            // هم‌الگوی ستونِ عکسِ «نمایندهٔ قانونی» (ConfigurePhotoCard): قابِ
            // یک‌پیکسلی، عکسِ Zoom، و دو دکمهٔ «انتخاب/حذف» زیرِ آن.
            this.picPhoto.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.picPhoto.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picPhoto.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picPhoto.BackColor = CaseManagement.Helpers.UiTheme.Background;
            this.picPhoto.TabStop = false;

            var headPhotoFrame = new System.Windows.Forms.Panel();
            headPhotoFrame.Dock = System.Windows.Forms.DockStyle.Fill;
            headPhotoFrame.Padding = new System.Windows.Forms.Padding(1);
            headPhotoFrame.BackColor = CaseManagement.Helpers.UiTheme.Border;
            headPhotoFrame.Controls.Add(this.picPhoto);

            this.btnBrowsePhoto.Text = "انتخاب عکس";
            this.btnBrowsePhoto.Size = new System.Drawing.Size(100, 30);
            this.btnBrowsePhoto.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.btnBrowsePhoto.Click += new System.EventHandler(this.btnBrowsePhoto_Click);

            this.btnClearPhoto.Name = "btnClearPhoto";
            this.btnClearPhoto.Text = "حذف عکس";
            this.btnClearPhoto.Size = new System.Drawing.Size(86, 30);
            this.btnClearPhoto.Margin = new System.Windows.Forms.Padding(0);
            this.btnClearPhoto.Click += new System.EventHandler(this.btnClearPhoto_Click);

            var headPhotoButtons = new System.Windows.Forms.FlowLayoutPanel();
            headPhotoButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            headPhotoButtons.Height = 38;
            headPhotoButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            headPhotoButtons.WrapContents = false;
            headPhotoButtons.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            headPhotoButtons.BackColor = System.Drawing.Color.Transparent;
            headPhotoButtons.Controls.Add(this.btnBrowsePhoto);
            headPhotoButtons.Controls.Add(this.btnClearPhoto);

            var headPhotoCaption = new System.Windows.Forms.Label();
            headPhotoCaption.Dock = System.Windows.Forms.DockStyle.Top;
            headPhotoCaption.Height = 20;
            headPhotoCaption.Text = "عکس سرپرست";
            headPhotoCaption.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            headPhotoCaption.Font = CaseManagement.Helpers.UiTheme.FontBold(
                CaseManagement.Helpers.UiTheme.SizeSmall - 0.5F);
            headPhotoCaption.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            headPhotoCaption.BackColor = System.Drawing.Color.Transparent;

            var headPhotoColumn = new System.Windows.Forms.Panel();
            headPhotoColumn.Dock = System.Windows.Forms.DockStyle.Fill;
            headPhotoColumn.Padding = new System.Windows.Forms.Padding(14, 8, 6, 10);
            headPhotoColumn.MinimumSize = new System.Drawing.Size(226, 236);
            headPhotoColumn.BackColor = System.Drawing.Color.Transparent;
            // Fill اول اضافه می‌شود و لبه‌ها بعد از آن: Dock از بالاترین اندیس
            // به پایین‌ترین اعمال می‌شود.
            headPhotoColumn.Controls.Add(headPhotoFrame);
            headPhotoColumn.Controls.Add(headPhotoButtons);
            headPhotoColumn.Controls.Add(headPhotoCaption);

            var headBody = new System.Windows.Forms.TableLayoutPanel();
            headBody.Dock = System.Windows.Forms.DockStyle.Top;
            headBody.AutoSize = true;
            headBody.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            headBody.ColumnCount = 2;
            headBody.RowCount = 1;
            headBody.BackColor = System.Drawing.Color.Transparent;
            // با RightToLeftLayout ستونِ ۰ سمتِ راست کشیده می‌شود — یعنی عکس
            // در آغازِ خط، همان جایی که در فورم‌های رسمی هست.
            headBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Absolute, 232F));
            headBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            headBody.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            gridHead.Dock = System.Windows.Forms.DockStyle.Fill;
            headBody.Controls.Add(headPhotoColumn, 0, 0);
            headBody.Controls.Add(gridHead, 1, 0);

            this.grpHead.Text = "";
            this.grpHead.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            var cardHead = MkCaseCard("مشخصات کلی سرپرست", headBody, this.grpHead);

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
            // «نوع پرونده» اولین فیلد است چون کلیدِ اصلیِ کل فرم است: همین مقدار
            // تعیین می‌کند کدام بخش‌های اختصاصی پایین‌تر ظاهر شوند. اگر پایین‌تر
            // می‌نشست، اپراتور فیلدها را پر می‌کرد و بعد بخش‌های تازه ظاهر
            // می‌شدند — ترتیبِ وارونه‌ی ورودِ اطلاعات.
            AddCaseField(gridCase, this.label4,       "نوع پرونده",         this.txtRequestType);
            // تأکیدِ بصری این فیلد در FrmCase.ApplyCustomTheme اعمال می‌شود،
            // نه اینجا — چون UiTheme.ApplySweep رنگِ همهٔ Labelها را بازنویسی
            // می‌کند و هر مقداری که اینجا داده شود از بین می‌رود.
            AddCaseField(gridCase, this.lblCode,      "کد اختصاصی",         this.txtCode);
            AddCaseField(gridCase, this.lblFormNo,    "شماره فرم",          this.txtFormNo);
            AddCaseField(gridCase, this.lblCaseNo,    "شماره پرونده",       this.txtCaseNo);
            AddCaseField(gridCase, this.label1,       "زون",                this.txtZone);
            AddCaseField(gridCase, this.label2,       "ولایت",              this.txtProvince);
            AddCaseField(gridCase, this.label3,       "ولسوالی",            this.txtDistrict);
            // مورد ۷ — بلافاصله بعد از ولسوالی، چون «سایت» ریزترین سطحِ
            // مکانی است و کاربر همان‌جا ذهنش روی موقعیت است.
            AddCaseField(gridCase, this.lblSite,      "سایت",               this.txtSite);
            AddCaseField(gridCase, this.label5,       "اولویت‌بندی اقتصادی", this.txtPriorityLevel);
            AddCaseField(gridCase, this.label16,      "تحت پوشش دیگر مؤسسات", this.txtCoveredByOrg);
            // آموزش — «اسامی مؤسسات» فقط وقتی معنی دارد که پاسخِ بالا «بله» باشد،
            // پس مثل «دلیل قطع موقت» در فرم اعضا، خودِ کادر و کانتینرش با هم
            // پنهان/آشکار می‌شوند تا جای خالی در شبکه نماند. منطقِ نمایش در
            // FrmCase.cs → UpdateCoveredByOrgNamesVisibility است.
            this.fieldCoveredByOrgNames = AddCaseField(
                gridCase, this.lblCoveredByOrgNames, "اسامی مؤسسات تحت پوشش", this.txtCoveredByOrgNames);
            this.lblCoveredByOrgNames.Visible   = false;
            this.txtCoveredByOrgNames.Visible   = false;
            this.fieldCoveredByOrgNames.Visible = false;
            AddCaseField(gridCase, this.lblCaseDate,  "تاریخ تشکیل پرونده", this.dtpCaseDate);
            AddCaseField(gridCase, this.label24,      "وضعیت خدمات",        this.txtServiceStatus);
            AddCaseField(gridCase, this.label25,      "آدرس لوکیشن",        this.txtLocationAddress);
            AddCaseField(gridCase, this.label23,      "سروی‌کننده‌ها",      this.txtSurveyors);
            AddCaseField(gridCase, this.label28,      "تاریخ سروی",         this.dtpSurveyDate);
            AddCaseField(gridCase, this.label29,      "نام معرف",           this.txtReferrerName);
            AddCaseField(gridCase, this.label30,      "شماره تماس معرف",    this.txtReferrerPhone);

            // ═══ بخش‌های اختصاصیِ نوع درخواست — نمایش/پنهانی با
            //     UpdateRequestTypeSectionVisibility در FrmCase.cs، بر اساسِ
            //     پرچم‌های TblRequestType (نه RequestTypeID هاردکد) ══════════
            this.txtFatherDeathCause.DropDownStyle    = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtDisabilityCause.DropDownStyle     = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtDisabilityCardStatus.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtHasMigrationCard.DropDownStyle    = System.Windows.Forms.ComboBoxStyle.DropDownList;
            // Phase 4
            this.txtFatherStatus.DropDownStyle         = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtMotherStatus.DropDownStyle         = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtOrphanEducationLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtGuardianRelationship.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.chkIsStudent.Text     = "مشغول تحصیل است";
            this.chkIsStudent.AutoSize = true;

            var gridOrphan = MkCaseFieldGrid();
            this.orphanSectionFields = new CaseManagement.Helpers.FieldBox[]
            {
                AddCaseField(gridOrphan, this.label31, "ولایت اقامتگاه اصلی", this.txtMainResidenceProvince),
                AddCaseField(gridOrphan, this.label32, "ولسوالی اقامتگاه اصلی", this.txtMainResidenceDistrict),
                AddCaseField(gridOrphan, this.label33, "قریه اقامتگاه اصلی", this.txtMainResidenceVillage),
                AddCaseField(gridOrphan, this.label45, "وضعیت پدر", this.txtFatherStatus),
                AddCaseField(gridOrphan, this.label34, "دلیل فوت پدر", this.txtFatherDeathCause),
                AddCaseField(gridOrphan, this.label46, "تاریخ فوت پدر", this.dtpFatherDeathDate),
                AddCaseField(gridOrphan, this.label47, "وضعیت مادر", this.txtMotherStatus),
                AddCaseField(gridOrphan, this.label48, "نام مکتب", this.txtOrphanSchoolName),
                AddCaseField(gridOrphan, this.label49, "سطح تحصیلات کودک", this.txtOrphanEducationLevel),
                AddCaseField(gridOrphan, this.lblIsStudent, "وضعیت تحصیل", this.chkIsStudent),
                AddCaseField(gridOrphan, this.label50, "یادداشت ایتام", this.txtOrphanNotes)
            };

            // بخشِ «اطلاعات سرپرست» — جدا از بخشِ ایتام، چون برای سه نوعِ
            // کودک دیده می‌شود ولی فیلدهای فوتِ پدر/مادر فقط برای ایتام.
            // داده‌اش در همان TblOrphan ذخیره می‌شود (تصمیمِ صریحِ کاربر).
            var gridGuardian = MkCaseFieldGrid();

            // Feature 3 — عکسِ سرپرستِ کودک. تنها عکسی بود که مسیرِ ورودِ
            // دستی نداشت. کنترل‌ها همان الگوی عکسِ سرپرستِ خانوار/نماینده‌اند
            // (پیش‌نمایش + انتخاب + حذف)، فقط داخلِ گروهِ «اطلاعات سرپرست».
            this.picGuardianPhoto = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.picGuardianPhoto)).BeginInit();
            this.picGuardianPhoto.Name = "picGuardianPhoto";
            this.picGuardianPhoto.Size = new System.Drawing.Size(110, 120);
            this.picGuardianPhoto.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.picGuardianPhoto.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picGuardianPhoto.BackColor = System.Drawing.Color.White;

            this.btnGuardianBrowsePhoto = new System.Windows.Forms.Button();
            this.btnGuardianBrowsePhoto.Name = "btnGuardianBrowsePhoto";
            this.btnGuardianBrowsePhoto.Text = "انتخاب عکس";
            this.btnGuardianBrowsePhoto.AutoSize = true;
            this.btnGuardianBrowsePhoto.Click += new System.EventHandler(this.btnGuardianBrowsePhoto_Click);

            this.btnGuardianClearPhoto = new System.Windows.Forms.Button();
            this.btnGuardianClearPhoto.Name = "btnGuardianClearPhoto";
            this.btnGuardianClearPhoto.Text = "حذف عکس";
            this.btnGuardianClearPhoto.AutoSize = true;
            this.btnGuardianClearPhoto.Click += new System.EventHandler(this.btnGuardianClearPhoto_Click);

            var guardianPhotoBox = new System.Windows.Forms.FlowLayoutPanel();
            guardianPhotoBox.Name = "guardianPhotoBox";
            guardianPhotoBox.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            guardianPhotoBox.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            guardianPhotoBox.AutoSize = true;
            guardianPhotoBox.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            guardianPhotoBox.WrapContents = false;
            guardianPhotoBox.Controls.Add(this.picGuardianPhoto);
            guardianPhotoBox.Controls.Add(this.btnGuardianBrowsePhoto);
            guardianPhotoBox.Controls.Add(this.btnGuardianClearPhoto);

            this.guardianSectionFields = new CaseManagement.Helpers.FieldBox[]
            {
                AddCaseField(gridGuardian, this.label51, "نام سرپرست کودک", this.txtGuardianName),
                AddCaseField(gridGuardian, this.label52, "نسبت سرپرست", this.txtGuardianRelationship),
                AddCaseField(gridGuardian, this.lblGuardianPhoto, "عکس سرپرست کودک", guardianPhotoBox)
            };
            ((System.ComponentModel.ISupportInitialize)(this.picGuardianPhoto)).EndInit();

            var gridDisability = MkCaseFieldGrid();
            this.disabilitySectionFields = new CaseManagement.Helpers.FieldBox[]
            {
                AddCaseField(gridDisability, this.label35, "دلیل معلولیت", this.txtDisabilityCause),
                AddCaseField(gridDisability, this.label36, "شرح معلولیت", this.txtDisabilityDescription),
                AddCaseField(gridDisability, this.label37, "نیازهای خاص", this.txtSpecialNeeds),
                AddCaseField(gridDisability, this.label38, "وضعیت کارت معلولیت", this.txtDisabilityCardStatus),
                AddCaseField(gridDisability, this.label39, "شماره کارت معلولیت", this.txtDisabilityCardNumber),
                AddCaseField(gridDisability, this.label53, "صادرکننده کارت", this.txtCardIssuer),
                AddCaseField(gridDisability, this.label54, "تاریخ صدور کارت", this.dtpDisabilityIssueDate),
                AddCaseField(gridDisability, this.label55, "تاریخ انقضای کارت", this.dtpDisabilityExpiryDate),
                AddCaseField(gridDisability, this.label56, "یادداشت معلولیت", this.txtDisabilityNotes)
            };

            var gridMigrant = MkCaseFieldGrid();
            // «نوع برگه مهاجرت» حالا داخل کارتِ مهاجرت می‌نشیند — قبلاً میان
            // فیلدهای عمومی بود و برای هر شش نوع پرونده دیده می‌شد.
            this.fieldMigrationCardType = AddCaseField(
                gridMigrant, this.label21, "نوع برگه مهاجرت", this.txtMigrationCardType);
            this.migrantSectionFields = new CaseManagement.Helpers.FieldBox[]
            {
                this.fieldMigrationCardType,
                AddCaseField(gridMigrant, this.label40, "دارای کارت مهاجرت", this.txtHasMigrationCard),
                AddCaseField(gridMigrant, this.label41, "شماره کارت مهاجرت", this.txtMigrationCardNumber),
                AddCaseField(gridMigrant, this.label42, "تاریخ خروج", this.dtpDepartureDate),
                AddCaseField(gridMigrant, this.label43, "تاریخ ورود", this.dtpArrivalDate),
                AddCaseField(gridMigrant, this.label44, "مدت مساعدت (ماه)", this.txtAssistanceDurationMonths),
                AddCaseField(gridMigrant, this.label57, "کشور مبدأ", this.txtOriginCountry),
                AddCaseField(gridMigrant, this.label58, "کشور مقصد", this.txtDestinationCountry),
                AddCaseField(gridMigrant, this.label59, "یادداشت مهاجرت", this.txtMigrantNotes)
            };

            // آموزش — «وضعیت تأهل» عمداً کنترلِ تازه‌ای نگرفت: txtMaritalStatus
            // موجود در بخشِ عمومی از قبل همین فیلد است و در گزارشِ RDLC/جستجو
            // خوانده می‌شود. دو کنترل برای یک واقعیت = دو مقدارِ واگرا؛ پس
            // CaseModuleService مقدارِ همان کنترل را در TblMigrant.MaritalStatus
            // آینه می‌کند (یک‌طرفه: TblCase → TblMigrant).

            // «دلیل تعلیق» (الزامی) و «یادداشت تعلیق» (اختیاری، همان کنترل قدیمی
            // StopReason) هر دو پنهان‌اند تا وضعیت خدمات «قطع» یا «قطع موقت» شود
            // (منطق نمایش/اجبار در FrmCase.cs → UpdateStopReasonVisibility/ValidateForm).
            this.txtSuspensionReason.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.caseFieldSuspensionReason = AddCaseField(gridCase, this.lblSuspensionReason, "دلیل تعلیق", this.txtSuspensionReason);
            this.lblSuspensionReason.Visible = false;
            this.txtSuspensionReason.Visible = false;
            this.caseFieldSuspensionReason.Visible = false;

            this.caseFieldStopReason = AddCaseField(gridCase, this.lblStopReason, "یادداشت تعلیق (اختیاری)", this.txtStopReason);
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

            // ═══════════════════════════════════════════════════════════════════
            // کارت‌های اختصاصیِ نوع پرونده — هرکدام قابِ مستقلِ خود را دارند تا
            // اپراتور ببیند کجا «اطلاعاتِ همیشگی» تمام و «اطلاعاتِ وابسته به نوع
            // پرونده» شروع می‌شود. قبلاً همهٔ این فیلدها در همان شبکهٔ عمومی
            // بودند و مرزی دیده نمی‌شد.
            //
            // نکتهٔ مهم: خودِ کارت هم پنهان/آشکار می‌شود، نه فقط فیلدهایش —
            // وگرنه برای نوعِ نامربوط یک کارتِ خالی با سربرگ باقی می‌ماند.
            // منطقِ آن در FrmCase.cs → UpdateRequestTypeSectionVisibility است.
            // ═══════════════════════════════════════════════════════════════════
            this.cardDisabilityInfo = MkCaseCard("اطلاعات معلولیت",       gridDisability, new System.Windows.Forms.GroupBox());
            this.cardOrphanInfo     = MkCaseCard("اطلاعات ایتام",         gridOrphan,     new System.Windows.Forms.GroupBox());
            this.cardGuardianInfo   = MkCaseCard("اطلاعات سرپرست کودک",   gridGuardian,   new System.Windows.Forms.GroupBox());
            this.cardMigrantInfo    = MkCaseCard("اطلاعات مهاجرت",        gridMigrant,    new System.Windows.Forms.GroupBox());

            // ═══ تب «خلاصه پرونده» (فاز A3) — خلاصهٔ خواندنیِ کل پرونده در یک
            // نگاه. آموزش — همان الگوی MkCaseFieldGrid/AddCaseField/FieldBox که
            // سرپرست/جسمی/پرونده استفاده می‌کنند، فقط با TextBoxِ ReadOnly
            // به‌جای فیلدِ قابل‌ویرایش — چون این تب صرفاً نماینده‌ی خواندنیِ
            // داده‌ی موجود است، نه فرم ورودیِ جدید. مقادیر در
            // UpdateCaseSummaryTab (FrmCase.cs) از همان کنترل‌های موجود کپی
            // می‌شوند؛ فقط سه مقدار (تعداد اعضاء/آخرین کمک/آخرین تغییر) از
            // کوئری‌های سبکِ تازه‌ای می‌آیند که در هیچ‌جای دیگر این فرم نبودند.
            this.txtSummaryCode           = new System.Windows.Forms.TextBox();
            this.txtSummaryHeadName       = new System.Windows.Forms.TextBox();
            this.txtSummaryRequestType    = new System.Windows.Forms.TextBox();
            this.txtSummaryServiceStatus  = new System.Windows.Forms.TextBox();
            this.txtSummaryLocation       = new System.Windows.Forms.TextBox();
            this.txtSummaryMemberCount    = new System.Windows.Forms.TextBox();
            this.txtSummaryLastAssistance = new System.Windows.Forms.TextBox();
            this.txtSummaryLastChange     = new System.Windows.Forms.TextBox();
            // آموزش — هفت فیلدِ زیر تازه‌اند اما هیچ دادهٔ تازه‌ای نمی‌آورند:
            // مقدارشان در UpdateCaseSummaryTab از همان کنترل‌های موجودِ فرم
            // (txtProvince/txtDistrict/txtHeadCurrentResidence/dtpCaseDate/
            // txtPhone/txtHeadTazkiraNo/txtSurveyors) کپی می‌شود. دلیلِ افزودن،
            // ردیف‌بندیِ خواسته‌شده در بخشِ DETAIL SECTION درخواست است.
            this.txtSummaryProvince       = new System.Windows.Forms.TextBox();
            this.txtSummaryDistrict       = new System.Windows.Forms.TextBox();
            this.txtSummaryVillage        = new System.Windows.Forms.TextBox();
            this.txtSummaryStartDate      = new System.Windows.Forms.TextBox();
            this.txtSummaryPhone          = new System.Windows.Forms.TextBox();
            this.txtSummaryTazkira        = new System.Windows.Forms.TextBox();
            this.txtSummaryOfficer        = new System.Windows.Forms.TextBox();
            foreach (System.Windows.Forms.TextBox sBox in new[]
            {
                this.txtSummaryCode, this.txtSummaryHeadName, this.txtSummaryRequestType,
                this.txtSummaryServiceStatus, this.txtSummaryLocation, this.txtSummaryMemberCount,
                this.txtSummaryLastAssistance, this.txtSummaryLastChange,
                this.txtSummaryProvince, this.txtSummaryDistrict, this.txtSummaryVillage,
                this.txtSummaryStartDate, this.txtSummaryPhone, this.txtSummaryTazkira,
                this.txtSummaryOfficer
            })
            {
                sBox.ReadOnly = true;
                sBox.TabStop = false;
                sBox.BackColor = System.Drawing.SystemColors.Control;
            }

            var gridSummary = MkCaseFieldGrid();
            System.Windows.Forms.Label lblSumCode  = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumHead  = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumReq   = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumProv  = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumDist  = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumVill  = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumSvc   = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumStart = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumChg   = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumPhone = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumTazk  = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumLast  = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumLoc   = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumCnt   = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSumOff   = new System.Windows.Forms.Label();
            // ترتیبِ افزودن = ترتیبِ خواسته‌شده در DETAIL SECTION (سه ستون در هر
            // ردیف؛ چون شبکه RTL است، اولین فیلدِ هر ردیف سمت راست می‌نشیند).
            AddCaseField(gridSummary, lblSumCode,  "کد اختصاصی",             this.txtSummaryCode);
            AddCaseField(gridSummary, lblSumHead,  "نام سرپرست",             this.txtSummaryHeadName);
            AddCaseField(gridSummary, lblSumReq,   "نوع پرونده",             this.txtSummaryRequestType);
            AddCaseField(gridSummary, lblSumProv,  "ولایت",                  this.txtSummaryProvince);
            AddCaseField(gridSummary, lblSumDist,  "ولسوالی",                this.txtSummaryDistrict);
            AddCaseField(gridSummary, lblSumVill,  "قریه / محل سکونت",       this.txtSummaryVillage);
            AddCaseField(gridSummary, lblSumSvc,   "وضعیت خدمات",            this.txtSummaryServiceStatus);
            AddCaseField(gridSummary, lblSumStart, "تاریخ شروع خدمات",       this.txtSummaryStartDate);
            AddCaseField(gridSummary, lblSumChg,   "آخرین تغییر",            this.txtSummaryLastChange);
            AddCaseField(gridSummary, lblSumPhone, "شماره تماس",             this.txtSummaryPhone);
            AddCaseField(gridSummary, lblSumTazk,  "شماره تذکره",            this.txtSummaryTazkira);
            AddCaseField(gridSummary, lblSumLast,  "آخرین کمک",              this.txtSummaryLastAssistance);
            AddCaseField(gridSummary, lblSumLoc,   "موقعیت (ولایت/ولسوالی)", this.txtSummaryLocation);
            AddCaseField(gridSummary, lblSumCnt,   "تعداد اعضای خانواده",    this.txtSummaryMemberCount);
            AddCaseField(gridSummary, lblSumOff,   "آمر مسئول",              this.txtSummaryOfficer);

            this.picSummaryPhoto = new System.Windows.Forms.PictureBox();
            this.picSummaryPhoto.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.picSummaryPhoto.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picSummaryPhoto.BackColor = CaseManagement.Helpers.UiTheme.Background;
            this.picSummaryPhoto.TabStop = false;
            this.picSummaryPhoto.Dock = System.Windows.Forms.DockStyle.Fill;

            System.Windows.Forms.Panel summaryPhotoBody = new System.Windows.Forms.Panel();
            summaryPhotoBody.BackColor = System.Drawing.Color.Transparent;
            summaryPhotoBody.Controls.Add(this.picSummaryPhoto);

            // ─── ردیفِ کارت‌های آماریِ بالای تبِ خلاصه ─────────────────────────
            // آموزش — هر پنج عدد از جدول‌های موجود می‌آید و هیچ‌کدام آمارِ تازه
            // نیست: تعداد اعضاء و آخرین کمک از قبل در همین تب بودند؛ سه عددِ
            // دیگر (مجموع کمک‌ها، تعداد اسناد، تعداد مراجعات) با سه کوئریِ سبکِ
            // COUNT/SUM روی همان TblAssistance و TblDocs محاسبه می‌شوند.
            this.lblStatMembers  = new System.Windows.Forms.Label();
            this.lblStatLastAid  = new System.Windows.Forms.Label();
            this.lblStatTotalAid = new System.Windows.Forms.Label();
            this.lblStatDocs     = new System.Windows.Forms.Label();
            this.lblStatVisits   = new System.Windows.Forms.Label();

            System.Windows.Forms.TableLayoutPanel statsRow = new System.Windows.Forms.TableLayoutPanel();
            statsRow.Name = "summaryStatsRow";
            statsRow.Dock = System.Windows.Forms.DockStyle.Top;
            statsRow.Height = 132;
            statsRow.ColumnCount = 6;
            statsRow.RowCount = 1;
            statsRow.BackColor = System.Drawing.Color.Transparent;
            statsRow.Padding = new System.Windows.Forms.Padding(14, 8, 14, 4);
            for (int statCol = 0; statCol < 6; statCol++)
                statsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                    System.Windows.Forms.SizeType.Percent, 100F / 6F));
            statsRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            // ستونِ ۰ در چیدمانِ راست‌به‌چپ سمتِ راست می‌نشیند، پس ترتیبِ زیر
            // دقیقاً همان ترتیبِ تصویرِ مرجع را می‌سازد.
            statsRow.Controls.Add(MkLeftCard("عکس سرپرست", summaryPhotoBody), 0, 0);
            statsRow.Controls.Add(MkSummaryStat("تعداد مراجعات", this.lblStatVisits,   "بار",   CaseManagement.Helpers.UiTheme.Warning),      1, 0);
            statsRow.Controls.Add(MkSummaryStat("تعداد اسناد",   this.lblStatDocs,     "فایل",  CaseManagement.Helpers.UiTheme.PrimaryLight), 2, 0);
            statsRow.Controls.Add(MkSummaryStat("مجموع کمک‌ها",  this.lblStatTotalAid, "افغانی", CaseManagement.Helpers.UiTheme.Primary),     3, 0);
            statsRow.Controls.Add(MkSummaryStat("آخرین کمک دریافتی", this.lblStatLastAid, "افغانی", CaseManagement.Helpers.UiTheme.Success),  4, 0);
            statsRow.Controls.Add(MkSummaryStat("تعداد اعضای خانواده", this.lblStatMembers, "نفر", CaseManagement.Helpers.UiTheme.TextDark),  5, 0);

            // ─── ردیفِ دومِ کارت‌ها: وضعیتِ محاسبه‌شدهٔ پرونده (Phase 5.5-C) ────
            // آموزش — هر پنج عدد *محاسبه‌شده* است و از سرویس‌های موجود می‌آید،
            // نه ورودیِ کاربر: تکمیل و امتیاز از ستون‌های کشِ TblCase،
            // مبلغِ پیشنهادی از AssistanceRuleService، و شمارشِ تأییدِ اسناد از
            // TblDocs. هیچ‌کدام قابلِ ویرایش از این صفحه نیستند — خواستهٔ صریح
            // «read-only from case screen».
            this.lblStatCompletionPct    = new System.Windows.Forms.Label();
            this.lblStatCompletionStatus = new System.Windows.Forms.Label();
            this.lblStatVulnScore        = new System.Windows.Forms.Label();
            this.lblStatSuggestedAid     = new System.Windows.Forms.Label();
            this.lblStatVerifiedDocs     = new System.Windows.Forms.Label();

            System.Windows.Forms.TableLayoutPanel statusRow = new System.Windows.Forms.TableLayoutPanel();
            statusRow.Name = "summaryStatusRow";
            statusRow.Dock = System.Windows.Forms.DockStyle.Top;
            statusRow.Height = 116;
            statusRow.ColumnCount = 5;
            statusRow.RowCount = 1;
            statusRow.BackColor = System.Drawing.Color.Transparent;
            statusRow.Padding = new System.Windows.Forms.Padding(14, 0, 14, 8);
            for (int statusCol = 0; statusCol < 5; statusCol++)
                statusRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                    System.Windows.Forms.SizeType.Percent, 100F / 5F));
            statusRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));

            statusRow.Controls.Add(MkSummaryStat("درصد تکمیل",          this.lblStatCompletionPct,    "٪",     CaseManagement.Helpers.UiTheme.Primary),      0, 0);
            statusRow.Controls.Add(MkSummaryStat("وضعیت تکمیل",         this.lblStatCompletionStatus, "",      CaseManagement.Helpers.UiTheme.PrimaryLight), 1, 0);
            statusRow.Controls.Add(MkSummaryStat("امتیاز آسیب‌پذیری",   this.lblStatVulnScore,        "از ۱۰۰", CaseManagement.Helpers.UiTheme.Warning),     2, 0);
            statusRow.Controls.Add(MkSummaryStat("مبلغ پیشنهادی مساعدت", this.lblStatSuggestedAid,    "افغانی", CaseManagement.Helpers.UiTheme.Success),     3, 0);
            statusRow.Controls.Add(MkSummaryStat("اسناد تأییدشده",      this.lblStatVerifiedDocs,     "سند",   CaseManagement.Helpers.UiTheme.TextDark),     4, 0);

            System.Windows.Forms.Panel summaryHost = new System.Windows.Forms.Panel();
            summaryHost.Dock = System.Windows.Forms.DockStyle.Top;
            summaryHost.AutoSize = true;
            summaryHost.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            summaryHost.BackColor = System.Drawing.Color.Transparent;
            // ترتیبِ افزودنِ عمدی: gridSummary اول، statsRow دوم — طبق همان
            // قاعده‌ی اثبات‌شده‌ی این فایل (کنترلی که آخر اضافه شود، بالاترین جا
            // را در Dock=Top می‌گیرد)، پس کارت‌های آماری بالای فیلدها می‌نشینند.
            summaryHost.Controls.Add(gridSummary);
            // Phase 5.5-C — ردیفِ وضعیت زیرِ ردیفِ آمار می‌نشیند (کنترلی که
            // دیرتر اضافه شود بالاتر می‌رود؛ همان قاعدهٔ اثبات‌شدهٔ این فایل).
            summaryHost.Controls.Add(statusRow);
            summaryHost.Controls.Add(statsRow);

            var cardSummary = MkCaseCard("خلاصه پرونده", summaryHost, new System.Windows.Forms.GroupBox());

            // ═══ فیلدها به‌صورت تب‌دار (به درخواست کاربر — مثل فرم اعضای خانواده) ═══
            // آموزش — قبلاً هر سه کارت پشت‌سرهم داخل یک پانلِ اسکرول‌شونده بودند
            // و کاربر باید تا پایین اسکرول می‌کرد. حالا هر کارت داخل یک تب
            // می‌نشیند؛ دقیقاً همان الگوی FrmFamily.
            //
            // هیچ فیلد/کنترلی حذف یا جابه‌جا نشد: همان cardHead/cardPhysical/
            // cardCase (با همان grpHead/grpPhysical/grpCase و همان نام کنترل‌ها)
            // فقط والدشان عوض شد، پس منطق FrmCase.cs دست‌نخورده کار می‌کند.
            // هر تب پانلِ اسکرولِ خودش را دارد تا محتوای بلند (مثل «مشخصات
            // پرونده» با شرح وضعیت فوری) روی نمایشگر کوچک هم کامل در دسترس باشد.
            // ═══ تب چهارم: اعضاء خانواده (فاز ۱ — قبلاً پنجرهٔ مجزا/مودال بود) ═══
            // آموزش — این تب فقط یک میزبانِ خالی (Panel) + پیام جای‌گزین است.
            // خودِ نمونهٔ FrmFamily و منطق جاسازی/رفرش در FrmCase.cs
            // (EnsureFamilyEmbedded/SyncMembersTab) انجام می‌شود؛ اینجا فقط
            // ظرف ساخته می‌شود تا وقتی پرونده‌ای انتخاب نشده، کاربر پیام روشنی
            // ببیند به‌جای گرید/فرم خالی. برخلاف تب‌های دیگر، AutoScroll ندارد
            // (FrmFamily وقتی embedded باشد، Dock=Fill خودش را با تب هماهنگ می‌کند).
            this.lblMembersPlaceholder = new System.Windows.Forms.Label();
            this.lblMembersPlaceholder.Name = "lblMembersPlaceholder";
            this.lblMembersPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMembersPlaceholder.Text = "برای مشاهده و ویرایش اعضاء، اول پرونده را ذخیره یا جستجو کنید.";
            this.lblMembersPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblMembersPlaceholder.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.lblMembersPlaceholder.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeMedium);
            this.lblMembersPlaceholder.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;

            this.tabMembersHost = new System.Windows.Forms.Panel();
            this.tabMembersHost.Name = "tabMembersHost";
            this.tabMembersHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMembersHost.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            this.tabMembersHost.Controls.Add(this.lblMembersPlaceholder);

            System.Windows.Forms.TabPage tabMembers = new System.Windows.Forms.TabPage("اعضاء خانواده");
            tabMembers.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            tabMembers.Padding   = System.Windows.Forms.Padding.Empty;
            tabMembers.Controls.Add(this.tabMembersHost);

            // ═══ تب «اسناد پرونده» (فاز A4 — قبلاً پنجرهٔ مجزا/مودال بود) ═══
            // آموزش — عیناً همان الگوی تب اعضاء: میزبانِ خالی + پیام جای‌گزین؛
            // نمونهٔ FrmDocs و منطق جاسازی/رفرش در FrmCase.cs
            // (EnsureDocsEmbedded/SyncMembersTab) انجام می‌شود.
            this.lblDocsPlaceholder = new System.Windows.Forms.Label();
            this.lblDocsPlaceholder.Name = "lblDocsPlaceholder";
            this.lblDocsPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDocsPlaceholder.Text = "برای مشاهده و مدیریت اسناد، اول پرونده را ذخیره یا جستجو کنید.";
            this.lblDocsPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblDocsPlaceholder.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.lblDocsPlaceholder.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeMedium);
            this.lblDocsPlaceholder.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;

            this.tabDocsHost = new System.Windows.Forms.Panel();
            this.tabDocsHost.Name = "tabDocsHost";
            this.tabDocsHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabDocsHost.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            this.tabDocsHost.Controls.Add(this.lblDocsPlaceholder);

            System.Windows.Forms.TabPage tabDocs = new System.Windows.Forms.TabPage("اسناد پرونده");
            tabDocs.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            tabDocs.Padding   = System.Windows.Forms.Padding.Empty;
            tabDocs.Controls.Add(this.tabDocsHost);

            // ═══ تب «تأمین مالی» (Phase 5.5-C) ══════════════════════════════
            // همان الگوی تبِ بازدید میدانی: گریدِ سوابق + نوارِ دکمه. تخصیص از
            // طریقِ دیالوگِ EntPrompt انجام می‌شود تا فرمِ تازه‌ای لازم نشود.
            this.dgvCaseFunding = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCaseFunding)).BeginInit();
            this.dgvCaseFunding.Name = "dgvCaseFunding";
            this.dgvCaseFunding.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvCaseFunding.ReadOnly = true;
            this.dgvCaseFunding.AllowUserToAddRows = false;
            this.dgvCaseFunding.AllowUserToDeleteRows = false;
            this.dgvCaseFunding.MultiSelect = false;
            this.dgvCaseFunding.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvCaseFunding.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCaseFunding.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvCaseFunding.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            this.btnFundingAssign = new System.Windows.Forms.Button();
            this.btnFundingAssign.Name = "btnFundingAssign";
            this.btnFundingAssign.Text = "تخصیص منبع مالی";
            this.btnFundingAssign.Size = new System.Drawing.Size(150, 38);
            this.btnFundingAssign.Click += new System.EventHandler(this.btnFundingAssign_Click);

            this.btnFundingRemove = new System.Windows.Forms.Button();
            this.btnFundingRemove.Name = "btnFundingRemove";
            this.btnFundingRemove.Text = "حذف تخصیص";
            this.btnFundingRemove.Size = new System.Drawing.Size(130, 38);
            this.btnFundingRemove.Click += new System.EventHandler(this.btnFundingRemove_Click);

            var fundingButtons = new System.Windows.Forms.FlowLayoutPanel();
            fundingButtons.Name = "fundingButtons";
            fundingButtons.Dock = System.Windows.Forms.DockStyle.Top;
            fundingButtons.Height = 50;
            fundingButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            fundingButtons.WrapContents = false;
            fundingButtons.Padding = new System.Windows.Forms.Padding(16, 2, 16, 6);
            fundingButtons.BackColor = System.Drawing.Color.Transparent;
            fundingButtons.Controls.Add(this.btnFundingAssign);
            fundingButtons.Controls.Add(this.btnFundingRemove);

            var fundingGridHost = new System.Windows.Forms.Panel();
            fundingGridHost.Name = "fundingGridHost";
            fundingGridHost.Dock = System.Windows.Forms.DockStyle.Fill;
            fundingGridHost.Padding = new System.Windows.Forms.Padding(10, 4, 10, 10);
            fundingGridHost.BackColor = System.Drawing.Color.Transparent;
            fundingGridHost.Controls.Add(this.dgvCaseFunding);

            var fundingSplit = new System.Windows.Forms.Panel();
            fundingSplit.Name = "fundingSplit";
            fundingSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            fundingSplit.Controls.Add(fundingGridHost);
            fundingSplit.Controls.Add(fundingButtons);

            this.tabFunding = new System.Windows.Forms.TabPage("تأمین مالی");
            this.tabFunding.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            this.tabFunding.Padding = System.Windows.Forms.Padding.Empty;
            this.tabFunding.Controls.Add(fundingSplit);
            ((System.ComponentModel.ISupportInitialize)(this.dgvCaseFunding)).EndInit();

            // ═══ بخشِ امتیاز آسیب‌پذیری (Phase 5.5-B) — فقط‌خواندنی ═════════
            // خواستهٔ صریح: نمایشِ برجسته، بدونِ امکانِ ویرایش از صفحهٔ پرونده.
            // هیچ کنترلِ ورودی‌ای اینجا نیست — امتیاز فقط محاسبه می‌شود.
            this.lblVulnScoreValue = new System.Windows.Forms.Label();
            this.lblVulnScoreValue.Name = "lblVulnScoreValue";
            this.lblVulnScoreValue.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblVulnScoreValue.Height = 44;
            this.lblVulnScoreValue.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblVulnScoreValue.Padding = new System.Windows.Forms.Padding(18, 0, 18, 0);
            this.lblVulnScoreValue.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeLarge);
            this.lblVulnScoreValue.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;

            this.lblVulnScoreDate = new System.Windows.Forms.Label();
            this.lblVulnScoreDate.Name = "lblVulnScoreDate";
            this.lblVulnScoreDate.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblVulnScoreDate.Height = 28;
            this.lblVulnScoreDate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblVulnScoreDate.Padding = new System.Windows.Forms.Padding(18, 0, 18, 0);
            this.lblVulnScoreDate.Font = CaseManagement.Helpers.UiTheme.Font(CaseManagement.Helpers.UiTheme.SizeSmall);
            this.lblVulnScoreDate.ForeColor = CaseManagement.Helpers.UiTheme.TextMuted;

            this.dgvVulnBreakdown = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dgvVulnBreakdown)).BeginInit();
            this.dgvVulnBreakdown.Name = "dgvVulnBreakdown";
            this.dgvVulnBreakdown.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvVulnBreakdown.ReadOnly = true;
            this.dgvVulnBreakdown.AllowUserToAddRows = false;
            this.dgvVulnBreakdown.AllowUserToDeleteRows = false;
            this.dgvVulnBreakdown.MultiSelect = false;
            this.dgvVulnBreakdown.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvVulnBreakdown.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvVulnBreakdown.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvVulnBreakdown.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            var vulnGridHost = new System.Windows.Forms.Panel();
            vulnGridHost.Name = "vulnGridHost";
            vulnGridHost.Dock = System.Windows.Forms.DockStyle.Fill;
            vulnGridHost.Padding = new System.Windows.Forms.Padding(10, 4, 10, 10);
            vulnGridHost.BackColor = System.Drawing.Color.Transparent;
            vulnGridHost.Controls.Add(this.dgvVulnBreakdown);

            var vulnTop = new System.Windows.Forms.Panel();
            vulnTop.Name = "vulnTop";
            vulnTop.Dock = System.Windows.Forms.DockStyle.Top;
            vulnTop.Height = 76;
            vulnTop.BackColor = System.Drawing.Color.Transparent;
            vulnTop.Controls.Add(this.lblVulnScoreDate);
            vulnTop.Controls.Add(this.lblVulnScoreValue);

            var vulnSplit = new System.Windows.Forms.Panel();
            vulnSplit.Name = "vulnSplit";
            vulnSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            vulnSplit.Controls.Add(vulnGridHost);
            vulnSplit.Controls.Add(vulnTop);

            this.tabVulnerability = new System.Windows.Forms.TabPage("امتیاز آسیب‌پذیری");
            this.tabVulnerability.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            this.tabVulnerability.Padding = System.Windows.Forms.Padding.Empty;
            this.tabVulnerability.Controls.Add(vulnSplit);
            ((System.ComponentModel.ISupportInitialize)(this.dgvVulnBreakdown)).EndInit();

            // ═══ تب «خانواده» (Phase 6) — حداقلی، طبقِ خواستهٔ صریح ═════════
            // فقط: نمایشِ شناسهٔ خانوار، فهرستِ پرونده‌های همان خانوار، و دو
            // دکمهٔ پیوند/جدا کردن. هیچ ماژولِ مدیریتِ خانوادهٔ جداگانه‌ای.
            this.lblFamilyGroupValue = new System.Windows.Forms.Label();
            this.lblFamilyGroupValue.Name = "lblFamilyGroupValue";
            this.lblFamilyGroupValue.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblFamilyGroupValue.Height = 34;
            this.lblFamilyGroupValue.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblFamilyGroupValue.Padding = new System.Windows.Forms.Padding(18, 0, 18, 0);
            this.lblFamilyGroupValue.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);
            this.lblFamilyGroupValue.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;

            this.dgvFamilyCases = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFamilyCases)).BeginInit();
            this.dgvFamilyCases.Name = "dgvFamilyCases";
            this.dgvFamilyCases.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvFamilyCases.ReadOnly = true;
            this.dgvFamilyCases.AllowUserToAddRows = false;
            this.dgvFamilyCases.AllowUserToDeleteRows = false;
            this.dgvFamilyCases.MultiSelect = false;
            this.dgvFamilyCases.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvFamilyCases.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvFamilyCases.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvFamilyCases.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            this.btnFamilyLink = new System.Windows.Forms.Button();
            this.btnFamilyLink.Name = "btnFamilyLink";
            this.btnFamilyLink.Text = "پیوند به خانواده موجود";
            this.btnFamilyLink.Size = new System.Drawing.Size(180, 38);
            this.btnFamilyLink.Click += new System.EventHandler(this.btnFamilyLink_Click);

            this.btnFamilyUnlink = new System.Windows.Forms.Button();
            this.btnFamilyUnlink.Name = "btnFamilyUnlink";
            this.btnFamilyUnlink.Text = "جدا کردن از خانواده";
            this.btnFamilyUnlink.Size = new System.Drawing.Size(160, 38);
            this.btnFamilyUnlink.Click += new System.EventHandler(this.btnFamilyUnlink_Click);

            var familyButtons = new System.Windows.Forms.FlowLayoutPanel();
            familyButtons.Name = "familyButtons";
            familyButtons.Dock = System.Windows.Forms.DockStyle.Top;
            familyButtons.Height = 50;
            familyButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            familyButtons.WrapContents = false;
            familyButtons.Padding = new System.Windows.Forms.Padding(16, 2, 16, 6);
            familyButtons.BackColor = System.Drawing.Color.Transparent;
            familyButtons.Controls.Add(this.btnFamilyLink);
            familyButtons.Controls.Add(this.btnFamilyUnlink);

            var familyGridHost = new System.Windows.Forms.Panel();
            familyGridHost.Name = "familyGridHost";
            familyGridHost.Dock = System.Windows.Forms.DockStyle.Fill;
            familyGridHost.Padding = new System.Windows.Forms.Padding(10, 4, 10, 10);
            familyGridHost.BackColor = System.Drawing.Color.Transparent;
            familyGridHost.Controls.Add(this.dgvFamilyCases);

            var familyTop = new System.Windows.Forms.Panel();
            familyTop.Name = "familyTop";
            familyTop.Dock = System.Windows.Forms.DockStyle.Top;
            familyTop.Height = 90;
            familyTop.BackColor = System.Drawing.Color.Transparent;
            familyTop.Controls.Add(familyButtons);
            familyTop.Controls.Add(this.lblFamilyGroupValue);

            var familySplit = new System.Windows.Forms.Panel();
            familySplit.Name = "familySplit";
            familySplit.Dock = System.Windows.Forms.DockStyle.Fill;
            familySplit.Controls.Add(familyGridHost);
            familySplit.Controls.Add(familyTop);

            this.tabFamilyGroup = new System.Windows.Forms.TabPage("خانواده");
            this.tabFamilyGroup.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            this.tabFamilyGroup.Padding = System.Windows.Forms.Padding.Empty;
            this.tabFamilyGroup.Controls.Add(familySplit);
            ((System.ComponentModel.ISupportInitialize)(this.dgvFamilyCases)).EndInit();

            // ═══ تب «بازدید میدانی» (Phase 5) ═══════════════════════════════
            // برخلافِ تب‌های اعضاء/اسناد که فرمِ جداگانه‌ای را جاسازی می‌کنند،
            // این تب سبک است: یک گرید + چند فیلدِ ورودی، همه در همین فرم.
            // دلیل: بازدید فقط شش فیلد دارد و ساختنِ FrmFieldVisit مجزا برای
            // آن، بدونِ نیازِ ثابت‌شده، پیچیدگیِ اضافه بود.
            this.grpVisitEntry = new System.Windows.Forms.GroupBox();
            this.grpVisitEntry.Text = "";
            this.grpVisitEntry.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.dgvVisits = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dgvVisits)).BeginInit();
            this.dgvVisits.Name = "dgvVisits";
            this.dgvVisits.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvVisits.ReadOnly = true;
            this.dgvVisits.AllowUserToAddRows = false;
            this.dgvVisits.AllowUserToDeleteRows = false;
            this.dgvVisits.MultiSelect = false;
            this.dgvVisits.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvVisits.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvVisits.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvVisits.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.dgvVisits.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvVisits_CellClick);

            this.dtpVisitDate = new CaseManagement.Helpers.PersianDatePicker();
            this.txtVisitorName = new System.Windows.Forms.TextBox();
            this.txtVisitResult = new System.Windows.Forms.ComboBox();
            this.txtVisitResult.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtVisitRecommendation = new System.Windows.Forms.ComboBox();
            this.txtVisitRecommendation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.txtVisitNotes = new System.Windows.Forms.TextBox();
            this.txtVisitNotes.Multiline = true;
            this.txtVisitNotes.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtVisitNotes.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.lblVisitDate = new System.Windows.Forms.Label();
            this.lblVisitorName = new System.Windows.Forms.Label();
            this.lblVisitResult = new System.Windows.Forms.Label();
            this.lblVisitRecommendation = new System.Windows.Forms.Label();
            this.lblVisitNotes = new System.Windows.Forms.Label();

            var gridVisitFields = MkCaseFieldGrid();
            AddCaseField(gridVisitFields, this.lblVisitDate,           "تاریخ بازدید",   this.dtpVisitDate);
            AddCaseField(gridVisitFields, this.lblVisitorName,         "بازدیدکننده",    this.txtVisitorName);
            AddCaseField(gridVisitFields, this.lblVisitResult,         "نتیجه بازدید",   this.txtVisitResult);
            AddCaseField(gridVisitFields, this.lblVisitRecommendation, "توصیه",          this.txtVisitRecommendation);
            AddCaseField(gridVisitFields, this.lblVisitNotes,          "یادداشت",        this.txtVisitNotes);

            this.btnVisitNew = new System.Windows.Forms.Button();
            this.btnVisitNew.Name = "btnVisitNew";
            this.btnVisitNew.Text = "بازدید جدید";
            this.btnVisitNew.Size = new System.Drawing.Size(132, 38);
            this.btnVisitNew.Click += new System.EventHandler(this.btnVisitNew_Click);

            this.btnVisitSave = new System.Windows.Forms.Button();
            this.btnVisitSave.Name = "btnVisitSave";
            this.btnVisitSave.Text = "ذخیره بازدید";
            this.btnVisitSave.Size = new System.Drawing.Size(132, 38);
            this.btnVisitSave.Click += new System.EventHandler(this.btnVisitSave_Click);

            this.btnVisitDelete = new System.Windows.Forms.Button();
            this.btnVisitDelete.Name = "btnVisitDelete";
            this.btnVisitDelete.Text = "حذف بازدید";
            this.btnVisitDelete.Size = new System.Drawing.Size(132, 38);
            this.btnVisitDelete.Click += new System.EventHandler(this.btnVisitDelete_Click);

            var visitButtons = new System.Windows.Forms.FlowLayoutPanel();
            visitButtons.Name = "visitButtons";
            visitButtons.Dock = System.Windows.Forms.DockStyle.Top;
            visitButtons.Height = 50;
            visitButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            visitButtons.WrapContents = false;
            visitButtons.Padding = new System.Windows.Forms.Padding(16, 2, 16, 6);
            visitButtons.BackColor = System.Drawing.Color.Transparent;
            // آموزش — جدول TblFieldVisitPhoto و متدهای FieldVisitService از فاز ۵
            // آماده بودند ولی هیچ دکمه‌ای صدایشان نمی‌زد؛ ستونِ «تعداد عکس»
            // همیشه صفر می‌ماند. این دکمه همان شکاف را می‌بندد.
            this.btnVisitPhotos = new System.Windows.Forms.Button();
            this.btnVisitPhotos.Name = "btnVisitPhotos";
            this.btnVisitPhotos.Text = "عکس‌های بازدید";
            this.btnVisitPhotos.Size = new System.Drawing.Size(140, 38);
            this.btnVisitPhotos.Click += new System.EventHandler(this.btnVisitPhotos_Click);

            visitButtons.Controls.Add(this.btnVisitNew);
            visitButtons.Controls.Add(this.btnVisitSave);
            visitButtons.Controls.Add(this.btnVisitDelete);
            visitButtons.Controls.Add(this.btnVisitPhotos);

            var visitFormContent = new System.Windows.Forms.Panel();
            visitFormContent.Name = "visitFormContent";
            visitFormContent.Dock = System.Windows.Forms.DockStyle.Top;
            visitFormContent.AutoSize = true;
            visitFormContent.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            visitFormContent.BackColor = System.Drawing.Color.Transparent;
            visitFormContent.Controls.Add(visitButtons);
            visitFormContent.Controls.Add(gridVisitFields);

            // گرید پایین (Fill) و فرمِ ورودی بالا (Top) — همان قاعدهٔ Dock که
            // بقیهٔ تب‌ها استفاده می‌کنند: کنترلی که آخر اضافه شود بالاتر می‌نشیند.
            var visitGridHost = new System.Windows.Forms.Panel();
            visitGridHost.Name = "visitGridHost";
            visitGridHost.Dock = System.Windows.Forms.DockStyle.Fill;
            visitGridHost.Padding = new System.Windows.Forms.Padding(10, 4, 10, 10);
            visitGridHost.BackColor = System.Drawing.Color.Transparent;
            visitGridHost.Controls.Add(this.dgvVisits);

            var visitFormHost = new System.Windows.Forms.Panel();
            visitFormHost.Name = "visitFormHost";
            visitFormHost.Dock = System.Windows.Forms.DockStyle.Top;
            visitFormHost.AutoSize = true;
            visitFormHost.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            visitFormHost.Padding = new System.Windows.Forms.Padding(10, 10, 10, 4);
            visitFormHost.BackColor = System.Drawing.Color.Transparent;
            visitFormHost.Controls.Add(MkCaseCard("ثبت بازدید میدانی", visitFormContent, this.grpVisitEntry));

            var visitSplit = new System.Windows.Forms.Panel();
            visitSplit.Name = "visitSplit";
            visitSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            visitSplit.Controls.Add(visitGridHost);
            visitSplit.Controls.Add(visitFormHost);

            this.tabVisits = new System.Windows.Forms.TabPage("بازدید میدانی");
            this.tabVisits.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            this.tabVisits.Padding = System.Windows.Forms.Padding.Empty;
            this.tabVisits.Controls.Add(visitSplit);
            ((System.ComponentModel.ISupportInitialize)(this.dgvVisits)).EndInit();

            // ─── تبِ تاریخچه ─────────────────────────────────────────────────
            // TblCaseTimeline از فاز ۳ نوشته می‌شد ولی هیچ صفحه‌ای آن را نشان
            // نمی‌داد. این تب فقط *خواندنی* است: همان الگوی گرید تبِ بازدید،
            // بدونِ فرمِ ورودی — تاریخچه هرگز دستی ویرایش نمی‌شود.
            this.dgvTimeline = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dgvTimeline)).BeginInit();
            this.dgvTimeline.Name = "dgvTimeline";
            this.dgvTimeline.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvTimeline.ReadOnly = true;
            this.dgvTimeline.AllowUserToAddRows = false;
            this.dgvTimeline.AllowUserToDeleteRows = false;
            this.dgvTimeline.MultiSelect = false;
            this.dgvTimeline.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvTimeline.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvTimeline.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvTimeline.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            var timelineGridHost = new System.Windows.Forms.Panel();
            timelineGridHost.Name = "timelineGridHost";
            timelineGridHost.Dock = System.Windows.Forms.DockStyle.Fill;
            timelineGridHost.Padding = new System.Windows.Forms.Padding(10, 10, 10, 10);
            timelineGridHost.BackColor = System.Drawing.Color.Transparent;
            timelineGridHost.Controls.Add(this.dgvTimeline);

            this.tabTimeline = new System.Windows.Forms.TabPage("تاریخچه");
            this.tabTimeline.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            this.tabTimeline.Padding = System.Windows.Forms.Padding.Empty;
            this.tabTimeline.Controls.Add(timelineGridHost);
            ((System.ComponentModel.ISupportInitialize)(this.dgvTimeline)).EndInit();

            // ═══ تب «نمایندهٔ قانونی» (Phase 7) ═══════════════
            // دو کارتِ هم‌شکل زیرِ هم، هرکدام یک نماینده. چیدمان از دلِ یک
            // سازندهٔ مشترک (MkRepresentativeCard) می‌آید نه دو بلوکِ کپی‌شده:
            // خواستهٔ صریحِ «ساختارِ تکراری نساز» فقط دربارهٔ جدول نبود،
            // دربارهٔ خودِ فرم هم هست — با دو بلوکِ کپی، اولین تغییرِ چیدمان
            // در یکی اعمال می‌شد و در دیگری جا می‌ماند.
            this.grpRepresentative1 = new System.Windows.Forms.GroupBox();
            this.grpRepresentative2 = new System.Windows.Forms.GroupBox();

            this.txtRep1Name = new System.Windows.Forms.TextBox();
            this.txtRep1Relationship = new System.Windows.Forms.ComboBox();
            this.cmbRep1IdCardType = new System.Windows.Forms.ComboBox();
            this.txtRep1NationalID = new System.Windows.Forms.TextBox();
            this.txtRep1Phone = new System.Windows.Forms.TextBox();
            this.txtRep1Phone2 = new System.Windows.Forms.TextBox();
            this.txtRep1Address = new System.Windows.Forms.TextBox();
            this.txtRep1Notes = new System.Windows.Forms.TextBox();
            this.picRep1Photo = new System.Windows.Forms.PictureBox();
            this.btnRep1BrowsePhoto = new System.Windows.Forms.Button();
            this.btnRep1ClearPhoto = new System.Windows.Forms.Button();
            this.lblRep1Name = new System.Windows.Forms.Label();
            this.lblRep1Relationship = new System.Windows.Forms.Label();
            this.lblRep1IdCardType = new System.Windows.Forms.Label();
            this.lblRep1NationalID = new System.Windows.Forms.Label();
            this.lblRep1Phone = new System.Windows.Forms.Label();
            this.lblRep1Phone2 = new System.Windows.Forms.Label();
            this.lblRep1Address = new System.Windows.Forms.Label();
            this.lblRep1Notes = new System.Windows.Forms.Label();

            this.txtRep2Name = new System.Windows.Forms.TextBox();
            this.txtRep2Relationship = new System.Windows.Forms.ComboBox();
            this.cmbRep2IdCardType = new System.Windows.Forms.ComboBox();
            this.txtRep2NationalID = new System.Windows.Forms.TextBox();
            this.txtRep2Phone = new System.Windows.Forms.TextBox();
            this.txtRep2Phone2 = new System.Windows.Forms.TextBox();
            this.txtRep2Address = new System.Windows.Forms.TextBox();
            this.txtRep2Notes = new System.Windows.Forms.TextBox();
            this.picRep2Photo = new System.Windows.Forms.PictureBox();
            this.btnRep2BrowsePhoto = new System.Windows.Forms.Button();
            this.btnRep2ClearPhoto = new System.Windows.Forms.Button();
            this.btnRep2Clear = new System.Windows.Forms.Button();
            this.lblRep2Name = new System.Windows.Forms.Label();
            this.lblRep2Relationship = new System.Windows.Forms.Label();
            this.lblRep2IdCardType = new System.Windows.Forms.Label();
            this.lblRep2NationalID = new System.Windows.Forms.Label();
            this.lblRep2Phone = new System.Windows.Forms.Label();
            this.lblRep2Phone2 = new System.Windows.Forms.Label();
            this.lblRep2Address = new System.Windows.Forms.Label();
            this.lblRep2Notes = new System.Windows.Forms.Label();

            ((System.ComponentModel.ISupportInitialize)(this.picRep1Photo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picRep2Photo)).BeginInit();

            this.btnRep1BrowsePhoto.Click += new System.EventHandler(this.btnRep1BrowsePhoto_Click);
            this.btnRep1ClearPhoto.Click += new System.EventHandler(this.btnRep1ClearPhoto_Click);
            this.btnRep2BrowsePhoto.Click += new System.EventHandler(this.btnRep2BrowsePhoto_Click);
            this.btnRep2ClearPhoto.Click += new System.EventHandler(this.btnRep2ClearPhoto_Click);

            this.btnRep2Clear.Name = "btnRep2Clear";
            this.btnRep2Clear.Text = "حذف نمایندهٔ دوم";
            this.btnRep2Clear.Size = new System.Drawing.Size(150, 36);
            this.btnRep2Clear.Click += new System.EventHandler(this.btnRep2Clear_Click);

            var repCard1 = MkRepresentativeCard(
                "نمایندهٔ اول (الزامی)", this.grpRepresentative1,
                this.lblRep1Name, this.txtRep1Name,
                this.lblRep1Relationship, this.txtRep1Relationship,
                this.lblRep1IdCardType, this.cmbRep1IdCardType,
                this.lblRep1NationalID, this.txtRep1NationalID,
                this.lblRep1Phone, this.txtRep1Phone,
                this.lblRep1Phone2, this.txtRep1Phone2,
                this.lblRep1Address, this.txtRep1Address,
                this.lblRep1Notes, this.txtRep1Notes,
                this.picRep1Photo, this.btnRep1BrowsePhoto, this.btnRep1ClearPhoto, null);

            var repCard2 = MkRepresentativeCard(
                "نمایندهٔ دوم (اختیاری)", this.grpRepresentative2,
                this.lblRep2Name, this.txtRep2Name,
                this.lblRep2Relationship, this.txtRep2Relationship,
                this.lblRep2IdCardType, this.cmbRep2IdCardType,
                this.lblRep2NationalID, this.txtRep2NationalID,
                this.lblRep2Phone, this.txtRep2Phone,
                this.lblRep2Phone2, this.txtRep2Phone2,
                this.lblRep2Address, this.txtRep2Address,
                this.lblRep2Notes, this.txtRep2Notes,
                this.picRep2Photo, this.btnRep2BrowsePhoto, this.btnRep2ClearPhoto, this.btnRep2Clear);

            // راهنمای بالای تب — کاربر باید بداند چرا این بخش اینجاست و
            // کِی الزامی است، بدونِ اینکه لازم باشد ذخیره کند تا خطا ببیند.
            this.lblRepresentativeHint = new System.Windows.Forms.Label();
            this.lblRepresentativeHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblRepresentativeHint.Height = 46;
            this.lblRepresentativeHint.Text =
                "نمایندهٔ قانونی (وکیل/قیّم) کسی است که امور اداری، مالی و حقوقیِ ذینفع را انجام می‌دهد." +
                " ثبتِ «نمایندهٔ اول» الزامی است؛ «نمایندهٔ دوم» اختیاری است.";
            this.lblRepresentativeHint.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblRepresentativeHint.Padding = new System.Windows.Forms.Padding(18, 0, 18, 0);
            this.lblRepresentativeHint.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            this.lblRepresentativeHint.BackColor = System.Drawing.Color.Transparent;
            this.lblRepresentativeHint.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            var repContent = new System.Windows.Forms.Panel();
            repContent.Name = "repContent";
            repContent.Dock = System.Windows.Forms.DockStyle.Top;
            repContent.AutoSize = true;
            repContent.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            repContent.Padding = new System.Windows.Forms.Padding(10, 6, 10, 10);
            repContent.BackColor = System.Drawing.Color.Transparent;
            // ترتیبِ افزودن معکوسِ ترتیبِ بصری است (قاعدهٔ Dock.Top): آخرین
            // کنترلِ افزوده‌شده بالاترین می‌نشیند.
            repContent.Controls.Add(repCard2);
            repContent.Controls.Add(repCard1);
            repContent.Controls.Add(this.lblRepresentativeHint);

            // کارت‌ها روی صفحه‌های کوتاه‌تر باید اسکرول شوند، نه بریده.
            var repScroller = new System.Windows.Forms.Panel();
            repScroller.Name = "repScroller";
            repScroller.Dock = System.Windows.Forms.DockStyle.Fill;
            repScroller.AutoScroll = true;
            repScroller.BackColor = System.Drawing.Color.Transparent;
            repScroller.Controls.Add(repContent);

            this.tabRepresentative = new System.Windows.Forms.TabPage("نمایندهٔ قانونی");
            this.tabRepresentative.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            this.tabRepresentative.Padding = System.Windows.Forms.Padding.Empty;
            this.tabRepresentative.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.tabRepresentative.Controls.Add(repScroller);
            ((System.ComponentModel.ISupportInitialize)(this.picRep1Photo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picRep2Photo)).EndInit();

            this.tabsCase = new RtlTabControl();
            this.tabsCase.Name              = "tabsCase";
            this.tabsCase.Dock              = System.Windows.Forms.DockStyle.Fill;
            this.tabsCase.Font              = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);
            this.tabsCase.RightToLeft       = System.Windows.Forms.RightToLeft.Yes;
            this.tabsCase.RightToLeftLayout = true;
            this.tabsCase.Padding           = new System.Drawing.Point(14, 4);
            this.tabsCase.TabPages.Add(MkCaseTab("خلاصه پرونده", cardSummary));
            // آموزش — این تب در فیلد نگه داشته می‌شود چون «جدید»/«ویرایش» باید
            // کاربر را به یک تبِ واقعاً قابلِ ویرایش ببرند؛ تبِ «خلاصه پرونده»
            // همیشه خواندنی است و اگر کاربر روی آن بماند، بعد از زدنِ «جدید»
            // هیچ فیلدِ قابلِ تایپی نمی‌بیند (باگی که کاربر گزارش کرد).
            this.tabHeadInfo = MkCaseTab("مشخصات کلی سرپرست", cardHead);
            this.tabsCase.TabPages.Add(this.tabHeadInfo);
            // «مشخصات جسمی» تبِ جدا ندارد و به‌صورت کارتِ مستقل داخل همین تب
            // می‌نشیند (خواستهٔ صریح کاربر). ترتیب از بالا: اطلاعاتِ عمومیِ
            // پرونده، سپس وضعیت جسمی، سپس کارت‌های وابسته به نوع پرونده.
            this.tabsCase.TabPages.Add(MkCaseTab("مشخصات پرونده",
                cardCase,
                cardPhysical,
                this.cardDisabilityInfo,
                this.cardOrphanInfo,
                this.cardGuardianInfo,
                this.cardMigrantInfo));
            this.tabsCase.TabPages.Add(tabMembers);
            this.tabsCase.TabPages.Add(tabDocs);
            // Phase 7 — پیش از تبِ بازدید: نماینده دادهٔ هویتیِ پرونده
            // است، نه رویدادِ کاری. نمایش/پنهانی‌اش را
            // UpdateRequestTypeSectionVisibility اداره می‌کند (پیش‌فرض: فقط «معلول»).
            this.tabsCase.TabPages.Add(this.tabRepresentative);
            this.tabsCase.TabPages.Add(this.tabVisits);
            this.tabsCase.TabPages.Add(this.tabFamilyGroup);
            this.tabsCase.TabPages.Add(this.tabFunding);
            this.tabsCase.TabPages.Add(this.tabVulnerability);
            // تاریخچه عمداً آخرین تب است: خواندنی، مرورِ گذشته، نه ورودِ داده.
            this.tabsCase.TabPages.Add(this.tabTimeline);
            this.tabsCase.SelectedIndexChanged += new System.EventHandler(this.tabsCase_SelectedIndexChanged);

            System.Windows.Forms.Panel fieldsPanel = new System.Windows.Forms.Panel();
            fieldsPanel.Name = "fieldsPanel";
            fieldsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            fieldsPanel.Padding = new System.Windows.Forms.Padding(4);
            fieldsPanel.Controls.Add(this.tabsCase);

            // آموزش — رفع باگ Tab نامنظم: چون گروه‌ها به این ترتیب (grpCase،
            // grpPhysical، grpHead) اضافه شدند، بدون این سه خط، Tab پیش‌فرض
            // از grpCase (پایین صفحه) شروع می‌شد نه grpHead (بالای صفحه).
            // این مقادیر ترتیب واقعی/بصری از بالا به پایین را تضمین می‌کند.
            this.grpHead.TabIndex = 0;
            this.grpPhysical.TabIndex = 1;
            this.grpCase.TabIndex = 2;

            // ═══ سمت چپ: کارت‌های بالا + گرید + صفحه‌بندی ═══════════════════
            // آموزش — طبق دو بند صریحِ درخواست کاربر:
            //   ۱) کارتِ «عکس سرپرست» از ستونِ چپ حذف شد؛ فقط «عکس جمعی
            //      خانواده» + «وضعیت خدمات» می‌مانند.
            //   ۲) کلِ ردیفِ فیلترهای بالای گرید حذف شد؛ گرید بلافاصله زیرِ
            //      بخشِ عکس شروع می‌شود.
            // ولی خودِ کنترل‌ها حذف *نشدند*: کدِ FrmCase.cs در ده‌ها نقطه با
            // picPhoto/btnBrowsePhoto (بارگذاری، پاک‌کردن و ذخیرهٔ عکسِ سرپرست)
            // و با cmbServiceStatusFilter (فیلترِ ورودی از داشبورد) کار می‌کند.
            // پس داخل یک میزبانِ نامرئی نگه داشته می‌شوند تا رفتارِ برنامه
            // ذره‌ای عوض نشود و فقط از دیدِ کاربر خارج شوند.
            // آموزش — عکسِ سرپرست از میزبانِ نامرئی بیرون آمد (درخواستِ کاربر:
            // «دکمه‌ها را بگذار»). ولی به ستونِ چپ برنمی‌گردد — همان‌جایی که
            // قبلاً به‌صراحت خواسته بودند خالی بماند؛ جای درستش کنارِ خودِ
            // فیلدهای سرپرست است، مثل هر فورمِ رسمیِ کاغذی.
            this.lblServiceStatusFilter.Text = "فیلتر وضعیت خدمات";
            this.lblServiceStatusFilter.Size = new System.Drawing.Size(2, 2);
            this.cmbServiceStatusFilter.Size = new System.Drawing.Size(2, 2);

            System.Windows.Forms.Panel hiddenCaseControls = new System.Windows.Forms.Panel();
            hiddenCaseControls.Name = "hiddenCaseControls";
            hiddenCaseControls.Size = new System.Drawing.Size(2, 2);
            hiddenCaseControls.Location = new System.Drawing.Point(-4000, -4000);
            hiddenCaseControls.Visible = false;
            hiddenCaseControls.TabStop = false;
            hiddenCaseControls.Controls.Add(this.lblServiceStatusFilter);
            hiddenCaseControls.Controls.Add(this.cmbServiceStatusFilter);

            // ── کارت ۱: عکس جمعی خانواده ─────────────────────────────────────
            this.picFamilyPhoto.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.picFamilyPhoto.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picFamilyPhoto.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picFamilyPhoto.BackColor = CaseManagement.Helpers.UiTheme.Background;
            this.picFamilyPhoto.TabStop = false;
            // آموزش — دو دکمهٔ تمام‌عرضِ روی‌هم (نوارِ آبی + نوارِ قرمز) زیرِ
            // عکس، کارت را شلوغ و نامنظم نشان می‌داد (گزارشِ کاربر). حالا یک
            // ردیفِ جمع‌وجورِ راست‌چین با دو دکمهٔ کوچک است.
            this.btnBrowseFamilyPhoto.Text = "انتخاب عکس";
            this.btnBrowseFamilyPhoto.Size = new System.Drawing.Size(112, 28);
            this.btnBrowseFamilyPhoto.Margin = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.btnBrowseFamilyPhoto.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);
            this.btnBrowseFamilyPhoto.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBrowseFamilyPhoto.FlatAppearance.BorderSize = 0;
            this.btnBrowseFamilyPhoto.BackColor = CaseManagement.Helpers.UiTheme.Primary;
            this.btnBrowseFamilyPhoto.ForeColor = System.Drawing.Color.White;
            this.btnBrowseFamilyPhoto.Click += new System.EventHandler(this.btnBrowseFamilyPhoto_Click);

            this.btnClearFamilyPhoto.Name = "btnClearFamilyPhoto";
            this.btnClearFamilyPhoto.Text = "حذف";
            this.btnClearFamilyPhoto.Size = new System.Drawing.Size(66, 28);
            this.btnClearFamilyPhoto.Margin = new System.Windows.Forms.Padding(0);
            this.btnClearFamilyPhoto.Font = CaseManagement.Helpers.UiTheme.Font(
                CaseManagement.Helpers.UiTheme.SizeSmall);
            this.btnClearFamilyPhoto.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClearFamilyPhoto.FlatAppearance.BorderSize = 1;
            this.btnClearFamilyPhoto.FlatAppearance.BorderColor = CaseManagement.Helpers.UiTheme.Border;
            this.btnClearFamilyPhoto.BackColor = System.Drawing.Color.White;
            this.btnClearFamilyPhoto.ForeColor = CaseManagement.Helpers.UiTheme.Danger;
            this.btnClearFamilyPhoto.Click += new System.EventHandler(this.btnClearFamilyPhoto_Click);

            var familyPhotoButtons = new System.Windows.Forms.FlowLayoutPanel();
            familyPhotoButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            familyPhotoButtons.Height = 34;
            familyPhotoButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            familyPhotoButtons.WrapContents = false;
            familyPhotoButtons.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            familyPhotoButtons.BackColor = System.Drawing.Color.Transparent;
            familyPhotoButtons.Controls.Add(this.btnBrowseFamilyPhoto);
            familyPhotoButtons.Controls.Add(this.btnClearFamilyPhoto);

            // کادرِ ۹:۱۶ — بزرگ‌ترین مستطیلِ عمودی که در کارت جا می‌شود.
            var familyPhotoAspect = new CaseManagement.Helpers.AspectBox();
            familyPhotoAspect.Dock = System.Windows.Forms.DockStyle.Fill;
            // ۱۶:۹ افقی — عکسِ جمعیِ خانواده معمولاً نشسته و در عرض گرفته
            // می‌شود (تصحیحِ کاربر؛ برداشتِ اولِ من عمودی بود).
            familyPhotoAspect.AspectWidth = 16f;
            familyPhotoAspect.AspectHeight = 9f;
            familyPhotoAspect.FrameColor = CaseManagement.Helpers.UiTheme.Border;
            familyPhotoAspect.FrameThickness = 1;
            this.picFamilyPhoto.Dock = System.Windows.Forms.DockStyle.None;
            familyPhotoAspect.Controls.Add(this.picFamilyPhoto);

            System.Windows.Forms.Panel familyPhotoBody = new System.Windows.Forms.Panel();
            familyPhotoBody.BackColor = System.Drawing.Color.Transparent;

            // Fill اول، بعد نوارِ دکمه‌ها.
            familyPhotoBody.Controls.Add(familyPhotoAspect);
            familyPhotoBody.Controls.Add(familyPhotoButtons);

            // ── کارت ۲: وضعیت خدمات ──────────────────────────────────────────
            // آموزش — هیچ دادهٔ تازه‌ای اینجا ساخته نمی‌شود: هر سه مقدار از
            // همان کنترل‌های موجودِ فرم (txtServiceStatus، dtpCaseDate) و همان
            // کوئریِ «آخرین تغییر»ِ تبِ خلاصه می‌آیند.
            this.lblSvcBadge = new System.Windows.Forms.Label();
            this.lblSvcBadge.Name = "lblSvcBadge";
            this.lblSvcBadge.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSvcBadge.Text = "—";
            this.lblSvcBadge.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblSvcBadge.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);
            this.lblSvcBadge.ForeColor = CaseManagement.Helpers.UiTheme.Success;
            this.lblSvcBadge.BackColor = CaseManagement.Helpers.UiTheme.SuccessLight;
            this.lblSvcBadge.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);

            this.lblSvcStartValue  = MkLeftCardValue();
            this.lblSvcChangeValue = MkLeftCardValue();

            System.Windows.Forms.TableLayoutPanel statusBody = new System.Windows.Forms.TableLayoutPanel();
            statusBody.ColumnCount = 1;
            statusBody.RowCount = 5;
            statusBody.BackColor = System.Drawing.Color.Transparent;
            statusBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            statusBody.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            statusBody.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 18F));
            statusBody.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            statusBody.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 18F));
            statusBody.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            statusBody.Controls.Add(this.lblSvcBadge,                       0, 0);
            statusBody.Controls.Add(MkLeftCardCaption("تاریخ شروع"),        0, 1);
            statusBody.Controls.Add(this.lblSvcStartValue,                  0, 2);
            statusBody.Controls.Add(MkLeftCardCaption("آخرین تغییر"),       0, 3);
            statusBody.Controls.Add(this.lblSvcChangeValue,                 0, 4);

            // هر دو کارت در یک ردیفِ دوستونه — پس ارتفاع و حاشیهٔ یکسان دارند
            // (خواستهٔ «کارت‌ها هم‌ارتفاع و با فاصلهٔ یکنواخت»).
            System.Windows.Forms.TableLayoutPanel photoBar = new System.Windows.Forms.TableLayoutPanel();
            photoBar.Dock = System.Windows.Forms.DockStyle.Top;
            photoBar.Height = LeftCardsRowHeight;
            photoBar.ColumnCount = 2;
            photoBar.RowCount = 1;
            photoBar.BackColor = System.Drawing.Color.Transparent;
            photoBar.Padding = new System.Windows.Forms.Padding(LeftGutter - 6, LeftGutter - 6, LeftGutter - 6, 0);
            photoBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 58F));
            photoBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 42F));
            photoBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            photoBar.Controls.Add(MkLeftCard("عکس جمعی خانواده", familyPhotoBody), 0, 0);
            photoBar.Controls.Add(MkLeftCard("وضعیت خدمات", statusBody), 1, 0);

            this.dgvCases.AllowUserToAddRows = false;
            this.dgvCases.AllowUserToDeleteRows = false;
            this.dgvCases.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvCases.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCases.MultiSelect = false;
            this.dgvCases.ReadOnly = true;
            // آموزش — انحرافِ آگاهانه از تصویرِ مرجع: در تصویر، گرید
            // ستونِ شمارهٔ ردیف ندارد؛ اما این برنامه شمارهٔ ردیف را خودش
            // در همین سرآیند رسم می‌کند (DgvCases_RowPostPaint) و پنهان‌کردنش یعنی
            // حذفِ یک قابلیتِ موجود — که درخواست نشده بود. پس سرآیند می‌ماند
            // و فقط از ۵۱ به ۳۴ پیکسل باریک می‌شود تا شش ستونِ داده راحت جا
            // شوند و اسکرولِ افقی لازم نشود.
            this.dgvCases.RowHeadersVisible = true;
            this.dgvCases.RowHeadersWidth = 34;
            this.dgvCases.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dgvCases.RowTemplate.Height = 24;
            this.dgvCases.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dgvCases.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvCases.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvCases_CellClick);

            System.Windows.Forms.Panel gridWrap = new System.Windows.Forms.Panel();
            gridWrap.Dock = System.Windows.Forms.DockStyle.Fill;
            gridWrap.BackColor = System.Drawing.Color.Transparent;
            gridWrap.Padding = new System.Windows.Forms.Padding(LeftGutter, LeftGutter, LeftGutter, 0);
            gridWrap.Controls.Add(this.dgvCases);

            // ── نوار صفحه‌بندی زیرِ گرید ──────────────────────────────────────
            // آموزش — خواستهٔ «۱۰ رکوردِ آخر» بدونِ صفحه‌بندی یعنی دسترسیِ کاربر
            // به بقیهٔ پرونده‌ها قطع می‌شد؛ پس همان کوئریِ موجود با LIMIT/OFFSET
            // صفحه‌بندی شد (منطق در FrmCase.cs). هیچ جدول/کوئریِ تازه‌ای نیست.
            this.btnGridFirst = MkPagerButton("اول");
            this.btnGridPrev  = MkPagerButton("قبلی");
            this.btnGridNext  = MkPagerButton("بعدی");
            this.btnGridLast  = MkPagerButton("آخر");
            this.btnGridFirst.Click += new System.EventHandler(this.btnGridFirst_Click);
            this.btnGridPrev.Click  += new System.EventHandler(this.btnGridPrev_Click);
            this.btnGridNext.Click  += new System.EventHandler(this.btnGridNext_Click);
            this.btnGridLast.Click  += new System.EventHandler(this.btnGridLast_Click);

            this.lblGridPage = new System.Windows.Forms.Label();
            this.lblGridPage.Name = "lblGridPage";
            this.lblGridPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGridPage.Text = "۱ / ۱";
            this.lblGridPage.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblGridPage.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);
            this.lblGridPage.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            this.lblGridPage.Margin = new System.Windows.Forms.Padding(3);

            this.lblGridTotal = new System.Windows.Forms.Label();
            this.lblGridTotal.Name = "lblGridTotal";
            this.lblGridTotal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGridTotal.Text = "تعداد کل: 0";
            this.lblGridTotal.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblGridTotal.Font = CaseManagement.Helpers.UiTheme.Font(CaseManagement.Helpers.UiTheme.SizeSmall);
            this.lblGridTotal.ForeColor = CaseManagement.Helpers.UiTheme.TextMuted;
            this.lblGridTotal.Margin = new System.Windows.Forms.Padding(3);

            System.Windows.Forms.TableLayoutPanel pagerBar = new System.Windows.Forms.TableLayoutPanel();
            pagerBar.Name = "pagerBar";
            pagerBar.Dock = System.Windows.Forms.DockStyle.Bottom;
            pagerBar.Height = PagerBarHeight;
            pagerBar.BackColor = System.Drawing.Color.Transparent;
            pagerBar.Padding = new System.Windows.Forms.Padding(LeftGutter, 4, LeftGutter, LeftGutter);
            pagerBar.ColumnCount = 6;
            pagerBar.RowCount = 1;
            pagerBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, PagerButtonWidth));
            pagerBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, PagerButtonWidth));
            pagerBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 74F));
            pagerBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, PagerButtonWidth));
            pagerBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, PagerButtonWidth));
            pagerBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            pagerBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            pagerBar.Controls.Add(this.btnGridFirst, 0, 0);
            pagerBar.Controls.Add(this.btnGridPrev,  1, 0);
            pagerBar.Controls.Add(this.lblGridPage,  2, 0);
            pagerBar.Controls.Add(this.btnGridNext,  3, 0);
            pagerBar.Controls.Add(this.btnGridLast,  4, 0);
            pagerBar.Controls.Add(this.lblGridTotal, 5, 0);

            System.Windows.Forms.Panel leftPanel = new System.Windows.Forms.Panel();
            leftPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            leftPanel.BackColor = System.Drawing.Color.Transparent;
            // ترتیبِ افزودن مهم است: در Dock، کنترلی که دیرتر اضافه شود لبهٔ
            // بیرونی‌تر را می‌گیرد — پس اول Fill، بعد Bottom، بعد Top.
            leftPanel.Controls.Add(gridWrap);
            leftPanel.Controls.Add(pagerBar);
            leftPanel.Controls.Add(photoBar);
            leftPanel.Controls.Add(hiddenCaseControls);

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
            // آموزش — به درخواست کاربر: این دو میانبرِ آبی/سبزِ بالای جدول
            // حذفِ بصری شدند. از وقتی «اعضاء خانواده» و «اسناد پرونده» تبِ
            // داخلِ همین فرم شده‌اند، این دکمه‌ها فقط همان تب را انتخاب
            // می‌کردند؛ یعنی یک ردیفِ کاملِ گزینهٔ تکراری. خودِ کنترل‌ها حذف
            // *نشدند* (btnFamily_Click/btnDocs_Click و تنظیمِ TabStop در
            // FrmCase.cs به آن‌ها ارجاع دارند) — دقیقاً همان الگوی
            // hiddenCaseControls: نوار پنهان می‌شود و ارتفاعِ ردیفش صفر.
            toolbar.Visible = false;

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
            // آموزش — تا امروز تاریخچهٔ تغییراتِ یک پروندهٔ مشخص از هیچ‌جای برنامه
            // قابل دیدن نبود؛ تنها دسترسی، گریدِ کلیِ ممیزی در داشبورد بود.
            StyleBtn(this.btnHistory, "تاریخچه", 82, 32); this.btnHistory.Click += new System.EventHandler(this.btnHistory_Click);
            // گام ۱ — این دکمه‌ها دیگر روی نوار نمی‌نشینند؛ منوی
            // «خروجی‌ها و چاپ» مستقیماً هندلرهایشان را صدا می‌زند.
            // ساخته‌شدنشان حفظ شد تا هیچ هندلر/رفتاری تغییر نکند.
            this.lblExportSection.Text = "خروجی‌ها:"; this.lblExportSection.AutoSize = false; this.lblExportSection.Size = new System.Drawing.Size(60, 32); this.lblExportSection.TextAlign = System.Drawing.ContentAlignment.MiddleRight; this.lblExportSection.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            StyleBtn(this.btnPrint, "چاپ", 74, 32); this.btnPrint.Click += new System.EventHandler(this.btnPrint_Click);
            StyleBtn(this.btnExportExcel, "اکسل", 62, 32); this.btnExportExcel.Click += new System.EventHandler(this.btnExportExcel_Click);
            StyleBtn(this.btnBatchExport, "خروجی جمعی", 104, 32); this.btnBatchExport.Click += new System.EventHandler(this.btnBatchExport_Click);
            // Phase 5.5-C — «پروندهٔ کامل»: همهٔ بخش‌ها (تایم‌لاین/بازدید/تأمین
            // مالی/امتیاز/اسناد) در یک فایل. متفاوت با «اکسل» که گزارشِ
            // چندپرونده‌ای است.
            StyleBtn(this.btnExportCaseFile, "پرونده کامل", 104, 32); this.btnExportCaseFile.Click += new System.EventHandler(this.btnExportCaseFile_Click);
            StyleBtn(this.btnChooseStorageFolder, "محل ذخیره", 96, 32); this.btnChooseStorageFolder.Click += new System.EventHandler(this.btnChooseStorageFolder_Click);
            // آموزش — برچسبِ گروه، هم‌سبکِ «خروجی‌ها:». بدونِ آن، این چهار دکمه
            // با دکمه‌های «عضو/سند»ِ داخلِ تب‌ها هم‌شکل دیده می‌شدند و معلوم
            // نبود کدام روی پرونده کار می‌کند (گزارشِ کاربر). هیچ دکمه‌ای
            // حذف/جابه‌جا نشد — فقط یک برچسبِ راهنما اضافه شد.
            this.lblCaseSection = new System.Windows.Forms.Label();
            this.lblCaseSection.Text = "پرونده:";
            this.lblCaseSection.AutoSize = false;
            this.lblCaseSection.Size = new System.Drawing.Size(52, 32);
            this.lblCaseSection.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblCaseSection.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            bottomActionsRow.Controls.Add(this.lblCaseSection);
            bottomActionsRow.Controls.Add(this.btnNew);
            bottomActionsRow.Controls.Add(this.btnSave);
            bottomActionsRow.Controls.Add(this.btnEdit);
            bottomActionsRow.Controls.Add(this.btnDelete);
            bottomActionsRow.Controls.Add(this.btnSearch);
            bottomActionsRow.Controls.Add(this.btnHistory);
            // آموزش — ترتیب طبق تصویرِ مرجع (از راست به چپ): پی‌دی‌اف، اکسل،
            // چاپ، خروجی جمعی. «ورد» و «محل ذخیره» در تصویر نیستند ولی حذف
            // نشدند (کارِ موجودِ کاربر را نمی‌شکنیم) و به انتهای همان ردیف
            // منتقل شدند تا ترتیبِ خواسته‌شده به‌هم نخورد.

            System.Windows.Forms.TableLayoutPanel bottomBar = new System.Windows.Forms.TableLayoutPanel();
            bottomBar.Dock = System.Windows.Forms.DockStyle.Fill;
            bottomBar.ColumnCount = 1;
            bottomBar.RowCount = 1;
            // ردیف هم‌اندازه‌ی محتوا (نه درصدِ ثابت) تا ارتفاعِ خودکارِ نوارِ
            // دکمه‌ها واقعاً منتقل شود.
            bottomBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            bottomBar.Controls.Add(bottomActionsRow, 0, 0);

            // ═══ فاز A2 — نوار جستجوی سریع: بخش ثابتِ بالای کل فضای کاری،
            // بالاتر از toolbar. آموزش — چهار فیلد هم‌زمان (کد پرونده/نام
            // سرپرست/شماره تذکره/شماره تماس)، عیناً همان ستون‌های
            // CaseSearchTypeColumns که قبلاً نوار جستجوی بالای گرید هم از آن‌ها
            // استفاده می‌کرد (آن نوار حذف شد تا دو جستجوی موازی نداشته باشیم).
            // منطق واقعی/کوئری در SearchCasesGrid (FrmCase.cs) است؛ اینجا فقط چیدمان.
            this.txtQsCode     = new System.Windows.Forms.TextBox();
            this.txtQsHeadName = new System.Windows.Forms.TextBox();
            this.txtQsTazkira  = new System.Windows.Forms.TextBox();
            this.txtQsPhone    = new System.Windows.Forms.TextBox();
            foreach (System.Windows.Forms.TextBox qsBox in new[] { this.txtQsCode, this.txtQsHeadName, this.txtQsTazkira, this.txtQsPhone })
            {
                CaseManagement.Helpers.UiTheme.StyleTextBox(qsBox);
                qsBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.QuickSearchField_KeyDown);
            }

            System.Windows.Forms.Button btnQuickSearch = CaseManagement.Helpers.UiTheme.CreateButton("جستجو", "⌕", CaseManagement.Helpers.UiTheme.Primary);
            btnQuickSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            btnQuickSearch.Click += new System.EventHandler(this.btnQuickSearch_Click);

            System.Windows.Forms.Button btnQuickSearchClear = CaseManagement.Helpers.UiTheme.CreateSecondaryButton("پاک‌سازی", "✕");
            btnQuickSearchClear.Dock = System.Windows.Forms.DockStyle.Fill;
            btnQuickSearchClear.Click += new System.EventHandler(this.btnQuickSearchClear_Click);

            System.Windows.Forms.Button btnAdvancedSearch = CaseManagement.Helpers.UiTheme.CreateSecondaryButton("جستجوی پیشرفته", "⋯");
            btnAdvancedSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            btnAdvancedSearch.Click += new System.EventHandler(this.btnAdvancedSearch_Click);

            System.Windows.Forms.TableLayoutPanel caseQuickSearchBar = new System.Windows.Forms.TableLayoutPanel();
            caseQuickSearchBar.Name = "caseQuickSearchBar";
            caseQuickSearchBar.Dock = System.Windows.Forms.DockStyle.Fill;
            caseQuickSearchBar.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            caseQuickSearchBar.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            caseQuickSearchBar.ColumnCount = 7;
            caseQuickSearchBar.RowCount = 1;
            caseQuickSearchBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            caseQuickSearchBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            caseQuickSearchBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            caseQuickSearchBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            // آموزش — این سه عرض بعد از بازرسیِ تصویری بزرگ شدند: با ۹۶ و ۱۲۰
            // پیکسل، متنِ «پاک‌سازی» و «جستجوی پیشرفته» روی آیکونشان می‌افتاد و
            // به خطِ دوم می‌شکست (خواستهٔ «No clipped labels»).
            caseQuickSearchBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            caseQuickSearchBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
            caseQuickSearchBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 168F));
            caseQuickSearchBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));

            caseQuickSearchBar.Controls.Add(MkQuickSearchField("کد اختصاصی",   this.txtQsCode),     0, 0);
            caseQuickSearchBar.Controls.Add(MkQuickSearchField("نام سرپرست",   this.txtQsHeadName), 1, 0);
            caseQuickSearchBar.Controls.Add(MkQuickSearchField("شماره تذکره",  this.txtQsTazkira),  2, 0);
            caseQuickSearchBar.Controls.Add(MkQuickSearchField("شماره تماس",   this.txtQsPhone),    3, 0);
            caseQuickSearchBar.Controls.Add(MkQuickSearchButtonCell(btnQuickSearch),      4, 0);
            caseQuickSearchBar.Controls.Add(MkQuickSearchButtonCell(btnQuickSearchClear), 5, 0);
            caseQuickSearchBar.Controls.Add(MkQuickSearchButtonCell(btnAdvancedSearch),   6, 0);

            // ═══ ریشه چیدمان ═════════════════════════════════════════════════
            System.Windows.Forms.TableLayoutPanel rootLayout = new System.Windows.Forms.TableLayoutPanel();
            rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            rootLayout.ColumnCount = 2;
            rootLayout.RowCount = 4;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 62F));
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 38F));
            // ردیف نوار جستجوی سریع (فاز A2) — بالاترین ردیف، همیشه ثابت.
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 76F));
            // ردیفِ نوار میانبر (toolbar) — بعد از حذفِ بصریِ دکمه‌های «اعضاء
            // خانواده»/«اسناد»، این نوار Visible=false است، پس ارتفاعش صفر شد
            // تا نوارِ خالی بالای فیلدها باقی نماند. خودِ ردیف و ایندکس‌های
            // بعدی (fieldsPanel=2، bottomBar=3) دست‌نخورده می‌مانند.
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 0F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            // ردیف نوار دکمه‌ها — ارتفاع اولیه برای دو خط دکمه؛ کدِ فرم
            // (AdjustBottomBarHeight) آن را با تعداد خطوطِ واقعی تنظیم می‌کند تا
            // هیچ دکمه‌ای در هیچ عرضی پنهان نماند.
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 96F));
            this.rootLayout = rootLayout;
            rootLayout.Controls.Add(caseQuickSearchBar, 0, 0);
            rootLayout.SetColumnSpan(caseQuickSearchBar, 2);
            rootLayout.Controls.Add(toolbar, 0, 1);
            rootLayout.SetColumnSpan(toolbar, 2);
            rootLayout.Controls.Add(fieldsPanel, 0, 2);
            rootLayout.Controls.Add(leftPanel, 1, 2);
            // نگه‌داشتنِ ارجاع: تب‌های «اعضاء»/«اسناد» فرم‌های کاملی هستند و در
            // ۶۲٪ عرض له می‌شوند، پس هنگام فعال‌بودنشان این ستون جمع می‌شود.
            this.leftWorkspacePanel = leftPanel;
            rootLayout.Controls.Add(bottomBar, 0, 3);
            rootLayout.SetColumnSpan(bottomBar, 2);

            // آموزش — رفع باگ Tab نامنظم (بند ۵): چون leftPanel (گرید/عکس‌ها،
            // سمت چپ) قبلاً بین fieldsPanel و bottomBar در ترتیب پیش‌فرض قرار
            // می‌گرفت، فوکوس بعد از آخرین فیلد به‌جای دکمه «ذخیره» ابتدا به
            // کنترل‌های سمت چپ می‌پرید. با این ترتیب صریح، Tab همیشه:
            // caseQuickSearchBar → toolbar → fieldsPanel (فیلدها) → bottomBar (ذخیره) → leftPanel
            caseQuickSearchBar.TabIndex = 0;
            toolbar.TabIndex = 1;
            fieldsPanel.TabIndex = 2;
            bottomBar.TabIndex = 3;
            leftPanel.TabIndex = 4;

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
            // آموزش — فاز A (بازخورد کاربر): با افزودنِ نوار جستجوی سریع و نوار
            // سرِ پرونده در بالای فضای کاری، اندازهٔ قبلی (۱۱۸۰×۸۸۰) دیگر جا
            // نمی‌داد و فیلدها/دکمه‌های نوار جستجو روی صفحه‌های کوچک‌تر فشرده/
            // نامرئی می‌شدند. اندازه افزایش یافت تا همه‌چیز با فاصلهٔ راحت جا شود
            // (FitToScreen در FrmCase_Load همچنان روی نمایشگرهای کوچک‌تر آن را
            // متناسب می‌کند، این تغییر فقط اندازهٔ طراحی/پیش‌فرض را بزرگ‌تر می‌کند).
            this.ClientSize = new System.Drawing.Size(1440, 960);
            this.Controls.Add(rootLayout);
            // تمام‌صفحه‌ی خودکار (درخواست کاربر). حداقلِ اندازه و بیشینه‌سازی در
            // FrmCase_Load اعمال می‌شود — آن‌جا اندازه‌ی واقعی صفحه در دسترس است.
            // آموزش — بندِ FORM SIZE درخواست: پنجره نه بیشینه شود و نه با
            // کشیدنِ لبه تغییرِ اندازه بدهد؛ فقط کوچک‌کردن (Minimize) باز
            // بماند. قفلِ MinimumSize/MaximumSize در UiTheme.MakeFixedSize
            // (که FrmCase_Load صدایش می‌زند) اعمال می‌شود؛ اینجا هم همان
            // مقادیر صریح نوشته شد تا خودِ Designer هم همین را نشان دهد.
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
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

        // ─── کارتِ یک نمایندهٔ قانونی (Phase 7) ─────────────────
        // یک سازنده برای هر دو نماینده. هر تغییرِ چیدمان یک‌جا اعمال
        // می‌شود و دو کارت هرگز از هم واگرا نمی‌شوند.
        //
        // چیدمان: ستونِ عکس (چپ) + شبکهٔ سه‌ستونهٔ فیلدها (راست)،
        // درونِ همان SectionCardِ سفیدِ گردگوشهٔ بقیهٔ فرم — پس
        // جداییِ بصریِ دو نماینده از خودِ کارت می‌آید، نه از خط‌کشیِ دستی.
        private System.Windows.Forms.Control MkRepresentativeCard(
            string title, System.Windows.Forms.GroupBox host,
            System.Windows.Forms.Label lblName, System.Windows.Forms.TextBox txtName,
            System.Windows.Forms.Label lblRelationship, System.Windows.Forms.ComboBox cmbRelationship,
            System.Windows.Forms.Label lblIdCardType, System.Windows.Forms.ComboBox cmbIdCardType,
            System.Windows.Forms.Label lblNationalId, System.Windows.Forms.TextBox txtNationalId,
            System.Windows.Forms.Label lblPhone, System.Windows.Forms.TextBox txtPhone,
            System.Windows.Forms.Label lblPhone2, System.Windows.Forms.TextBox txtPhone2,
            System.Windows.Forms.Label lblAddress, System.Windows.Forms.TextBox txtAddress,
            System.Windows.Forms.Label lblNotes, System.Windows.Forms.TextBox txtNotes,
            System.Windows.Forms.PictureBox picPhoto,
            System.Windows.Forms.Button btnBrowsePhoto,
            System.Windows.Forms.Button btnClearPhoto,
            System.Windows.Forms.Button btnClearAll)
        {
            // واژگانِ بسته → DropDownList؛ متنِ آزاد → TextBox.
            // همان قاعدهٔ UI Standards در PROJECT_CONTEXT.
            cmbRelationship.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // نوعِ تذکره + شماره: همان رفتارِ هوشمندِ فرمِ پرونده/اعضا
            // (درجِ خودکارِ خط تیره، فقط رقم، سقفِ طول) — دوباره نوشته
            // نمی‌شود، همان IdCardHelper وصل می‌شود.
            CaseManagement.Helpers.IdCardHelper.FillCombo(cmbIdCardType);
            CaseManagement.Helpers.IdCardHelper.Attach(cmbIdCardType, txtNationalId);

            txtAddress.Multiline = true;
            txtAddress.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtAddress.Height = 58;
            txtAddress.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            txtNotes.Multiline = true;
            txtNotes.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtNotes.Height = 58;
            txtNotes.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            var grid = MkCaseFieldGrid();
            AddCaseField(grid, lblName,         "نام کامل",          txtName);
            AddCaseField(grid, lblRelationship, "نسبت با ذینفع",    cmbRelationship);
            AddCaseField(grid, lblIdCardType,   "نوع تذکره",         cmbIdCardType);
            AddCaseField(grid, lblNationalId,   "شماره تذکره",       txtNationalId);
            AddCaseField(grid, lblPhone,        "شماره تماس",        txtPhone);
            AddCaseField(grid, lblPhone2,       "شماره تماس دوم",   txtPhone2);
            AddCaseField(grid, lblAddress,      "آدرس",              txtAddress);
            AddCaseField(grid, lblNotes,        "یادداشت",           txtNotes);

            // ─── ستونِ عکس ───────────────────────────────────
            picPhoto.BorderStyle = System.Windows.Forms.BorderStyle.None;
            picPhoto.Dock = System.Windows.Forms.DockStyle.Fill;
            picPhoto.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            picPhoto.BackColor = CaseManagement.Helpers.UiTheme.Background;
            picPhoto.TabStop = false;

            var photoFrame = new System.Windows.Forms.Panel();
            photoFrame.Dock = System.Windows.Forms.DockStyle.Fill;
            photoFrame.Padding = new System.Windows.Forms.Padding(1);
            photoFrame.BackColor = CaseManagement.Helpers.UiTheme.Border;
            photoFrame.Controls.Add(picPhoto);

            btnBrowsePhoto.Text = "انتخاب عکس";
            btnBrowsePhoto.Size = new System.Drawing.Size(112, 32);
            btnClearPhoto.Text = "حذف عکس";
            btnClearPhoto.Size = new System.Drawing.Size(96, 32);

            var photoButtons = new System.Windows.Forms.FlowLayoutPanel();
            photoButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            photoButtons.Height = 42;
            photoButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            photoButtons.WrapContents = false;
            photoButtons.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            photoButtons.BackColor = System.Drawing.Color.Transparent;
            photoButtons.Controls.Add(btnBrowsePhoto);
            photoButtons.Controls.Add(btnClearPhoto);

            var photoCaption = new System.Windows.Forms.Label();
            photoCaption.Dock = System.Windows.Forms.DockStyle.Top;
            photoCaption.Height = 20;
            photoCaption.Text = "عکس نماینده";
            photoCaption.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            photoCaption.Font = CaseManagement.Helpers.UiTheme.FontBold(
                CaseManagement.Helpers.UiTheme.SizeSmall - 0.5F);
            photoCaption.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            photoCaption.BackColor = System.Drawing.Color.Transparent;

            var photoColumn = new System.Windows.Forms.Panel();
            photoColumn.Dock = System.Windows.Forms.DockStyle.Left;
            photoColumn.Width = 190;
            photoColumn.Padding = new System.Windows.Forms.Padding(14, 8, 8, 10);
            photoColumn.BackColor = System.Drawing.Color.Transparent;
            photoColumn.Controls.Add(photoFrame);
            photoColumn.Controls.Add(photoButtons);
            photoColumn.Controls.Add(photoCaption);

            var body = new System.Windows.Forms.Panel();
            body.Dock = System.Windows.Forms.DockStyle.Top;
            body.AutoSize = true;
            body.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            body.MinimumSize = new System.Drawing.Size(0, 230);
            body.BackColor = System.Drawing.Color.Transparent;
            body.Controls.Add(grid);
            body.Controls.Add(photoColumn);

            var content = new System.Windows.Forms.Panel();
            content.Dock = System.Windows.Forms.DockStyle.Top;
            content.AutoSize = true;
            content.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            content.BackColor = System.Drawing.Color.Transparent;

            // دکمهٔ «حذف نمایندهٔ دوم» فقط روی کارتِ دوم معنا دارد؛
            // کارتِ اول null می‌فرستد چون نمایندهٔ اول الزامی است و
            // حذفِ تکیِ آن پرونده را نامعتبر می‌کرد.
            if (btnClearAll != null)
            {
                var clearRow = new System.Windows.Forms.FlowLayoutPanel();
                clearRow.Dock = System.Windows.Forms.DockStyle.Top;
                clearRow.Height = 46;
                clearRow.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
                clearRow.WrapContents = false;
                clearRow.Padding = new System.Windows.Forms.Padding(14, 2, 14, 6);
                clearRow.BackColor = System.Drawing.Color.Transparent;
                clearRow.Controls.Add(btnClearAll);
                content.Controls.Add(clearRow);
            }

            content.Controls.Add(body);
            return MkCaseCard(title, content, host);
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
        //
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

        // آموزش — فاز A2 (Quick Search Bar): همان الگوی کپشنِ بالا + ورودی که
        // در FieldBox هم استفاده می‌شود، اما سبک‌تر و مخصوصِ نوار جستجو.
        private static System.Windows.Forms.Panel MkQuickSearchField(
            string captionText, System.Windows.Forms.TextBox valueBox)
        {
            System.Windows.Forms.Label caption = new System.Windows.Forms.Label();
            caption.Text = captionText;
            caption.AutoSize = false;
            caption.Dock = System.Windows.Forms.DockStyle.Top;
            caption.Height = 16;
            caption.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            caption.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall - 0.5F);
            caption.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            caption.BackColor = System.Drawing.Color.Transparent;

            valueBox.Dock = System.Windows.Forms.DockStyle.Fill;
            valueBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            System.Windows.Forms.Panel host = new System.Windows.Forms.Panel();
            host.Dock = System.Windows.Forms.DockStyle.Fill;
            host.Margin = new System.Windows.Forms.Padding(4, 2, 4, 2);
            host.Controls.Add(valueBox);
            host.Controls.Add(caption);
            return host;
        }

        // خانه‌ی یک دکمه در نوار جستجوی سریع — فقط برای هم‌ترازیِ عمودیِ دکمه
        // با فیلدهای کنارش (که کپشن+ورودی دارند)، دکمه داخل یک پنلِ پدینگ‌دار
        // قرار می‌گیرد به‌جای چسبیدن مستقیم به سلولِ TableLayoutPanel.
        private static System.Windows.Forms.Panel MkQuickSearchButtonCell(System.Windows.Forms.Button button)
        {
            System.Windows.Forms.Panel host = new System.Windows.Forms.Panel();
            host.Dock = System.Windows.Forms.DockStyle.Fill;
            host.Padding = new System.Windows.Forms.Padding(4, 18, 4, 4);
            host.Controls.Add(button);
            return host;
        }

        // ─── رفعِ باگِ «چپ‌چین بودن تب‌ها» ───────────────────────────────────
        // آموزش — همان کلاسی که در FrmFamily.Designer.cs استفاده شد: WinForms
        // مقدار TabControl.RightToLeftLayout را می‌پذیرد ولی exstyle بومیِ
        // WS_EX_LAYOUTRTL را به هندلِ پنجره اعمال نمی‌کند، پس نوار سربرگ‌ها از
        // چپ شروع می‌شود و ResponsiveLayout.IsMirrored هم اشتباه محاسبه می‌کند.
        private class RtlTabControl : System.Windows.Forms.TabControl
        {
            protected override System.Windows.Forms.CreateParams CreateParams
            {
                get
                {
                    const int WS_EX_LAYOUTRTL = 0x00400000;
                    System.Windows.Forms.CreateParams cp = base.CreateParams;
                    if (RightToLeftLayout)
                        cp.ExStyle |= WS_EX_LAYOUTRTL;
                    return cp;
                }
            }
        }

        // ─── چرا تب‌ها رنگ‌بندیِ گروهی ندارند ────────────────────────────────
        // آموزش (تصمیمِ ۱۴۰۵/۰۶/۱۶) — یک‌بار با TabDrawMode.OwnerDrawFixed
        // امتحان شد تا هر گروهِ کاری رنگِ خودش را بگیرد. نتیجه خراب بود:
        // این کنترل WS_EX_LAYOUTRTL دارد، پس ویندوز مختصاتِ ترسیم را آینه
        // می‌کند و نوارهای رنگی سرِ جای تبِ همسایه می‌افتادند (گزارشِ کاربر:
        // «رنگ‌ها قاطی آمده»). درستش نگه‌داشتنِ تبِ بومیِ ساده است.
        // اگر روزی رنگ‌بندی لازم شد، راهِ درست جایگزینیِ کلِ نوار با
        // Helpers/PillTabStrip است (کنترلِ خودمان، بدونِ آینهٔ سیستمی) — نه
        // نقاشیِ سفارشیِ روی TabControl.

        // یک تب با پانلِ اسکرولِ اختصاصی که کارتِ داده‌شده را در خود دارد.
        // چند کارت در یک تب: کارت‌ها Dock=Top و AutoSize هستند و از قبل
        // Margin پایین دارند، پس روی‌هم‌چیدنشان همان الگوی جاافتادهٔ این فایل
        // است. ترتیبِ افزودن عمداً معکوس است — طبق قاعدهٔ اثبات‌شدهٔ این فایل،
        // کنترلی که آخر اضافه شود در Dock=Top بالاترین جا را می‌گیرد؛ پس
        // برای نمایشِ cards[0] در بالا باید از انتها به ابتدا افزوده شوند.
        private static System.Windows.Forms.TabPage MkCaseTab(
            string title, params System.Windows.Forms.Control[] cards)
        {
            FieldsScrollPanel scroller = new FieldsScrollPanel();
            scroller.Dock = System.Windows.Forms.DockStyle.Fill;
            scroller.AutoScroll = true;
            scroller.Padding = new System.Windows.Forms.Padding(10, 10, 10, 10);
            scroller.BackColor = System.Drawing.Color.Transparent;
            for (int i = cards.Length - 1; i >= 0; i--)
                if (cards[i] != null)
                    scroller.Controls.Add(cards[i]);

            System.Windows.Forms.TabPage page = new System.Windows.Forms.TabPage(title);
            page.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            page.Padding = System.Windows.Forms.Padding.Empty;
            page.Controls.Add(scroller);
            return page;
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


        // ─── ثابت‌های چیدمانِ ستونِ چپ ────────────────────────────────────────
        // آموزش — خواستهٔ «فاصله‌ها و حاشیه‌ها همه‌جا یکنواخت»: به‌جای پخش‌کردنِ
        // عددهای جادویی در بدنهٔ InitializeComponent، هر فاصله یک نامِ واحد
        // دارد. تغییرِ یک عدد اینجا کلِ ستون را هماهنگ جابه‌جا می‌کند.
        private const int LeftGutter           = 8;    // حاشیهٔ بیرونیِ ستونِ چپ
        // آموزش — بعد از بازرسیِ تصویری از ۱۹۶ به ۱۶۸ کم شد: روی نمایشگرِ
        // ۷۶۸ پیکسلی، ۱۹۶ پیکسل آن‌قدر از ارتفاعِ ستون را می‌گرفت که از ۱۰
        // ردیفِ گرید فقط شش‌تا دیده می‌شد.
        // آموزش — از ۱۶۸ به ۲۴۰ رفت: کادرِ عکسِ خانواده نسبتِ ۱۶:۹ (افقی)
        // دارد و در ارتفاعِ قبلی به نوارِ باریکِ ~۵۶ پیکسلی تبدیل می‌شد —
        // همان چیزی که کاربر «خیلی کوچک» خواندش.
        private const int LeftCardsRowHeight   = 240;  // ارتفاعِ ردیفِ دو کارتِ بالا
        private const int LeftCardButtonHeight = 30;   // ارتفاعِ دکمهٔ داخلِ کارت
        private const int PagerBarHeight       = 44;   // ارتفاعِ نوارِ صفحه‌بندی
        private const float PagerButtonWidth   = 58F;  // عرضِ هر دکمهٔ صفحه‌بندی

        // کارتِ کوچکِ ستونِ چپ: عنوانِ بالا + بدنه. همان زبانِ بصریِ SectionCard
        // که تب‌های سمت راست هم از آن استفاده می‌کنند، در ابعادِ فشرده‌تر — تا
        // دو طرفِ فرم یک‌دست دیده شوند.
        private static CaseManagement.Helpers.SectionCard MkLeftCard(
            string title, System.Windows.Forms.Control body)
        {
            var header = new System.Windows.Forms.Label();
            header.Dock = System.Windows.Forms.DockStyle.Top;
            header.Height = 24;
            header.Text = title;
            header.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);
            header.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            header.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            header.BackColor = System.Drawing.Color.Transparent;

            body.Dock = System.Windows.Forms.DockStyle.Fill;

            var card = new CaseManagement.Helpers.SectionCard();
            card.Dock = System.Windows.Forms.DockStyle.Fill;
            card.Margin = new System.Windows.Forms.Padding(6);
            card.Padding = new System.Windows.Forms.Padding(10, 6, 10, 10);
            card.Controls.Add(body);
            card.Controls.Add(header);
            return card;
        }

        // برچسبِ ریزِ «عنوانِ فیلد» داخلِ کارتِ وضعیت خدمات.
        private static System.Windows.Forms.Label MkLeftCardCaption(string text)
        {
            var lbl = new System.Windows.Forms.Label();
            lbl.Dock = System.Windows.Forms.DockStyle.Fill;
            lbl.Text = text;
            lbl.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            lbl.Font = CaseManagement.Helpers.UiTheme.Font(CaseManagement.Helpers.UiTheme.SizeSmall - 1F);
            lbl.ForeColor = CaseManagement.Helpers.UiTheme.TextMuted;
            lbl.BackColor = System.Drawing.Color.Transparent;
            lbl.Margin = new System.Windows.Forms.Padding(0);
            return lbl;
        }

        // برچسبِ «مقدار» داخلِ کارتِ وضعیت خدمات — مقدارش را FrmCase.cs پر می‌کند.
        private static System.Windows.Forms.Label MkLeftCardValue()
        {
            var lbl = new System.Windows.Forms.Label();
            lbl.Dock = System.Windows.Forms.DockStyle.Fill;
            lbl.Text = "—";
            lbl.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            lbl.Font = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);
            lbl.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            lbl.BackColor = System.Drawing.Color.Transparent;
            lbl.Margin = new System.Windows.Forms.Padding(0);
            return lbl;
        }

        // دکمهٔ نوارِ صفحه‌بندی — همهٔ پنج دکمه از یک جا می‌آیند تا ارتفاع،
        // فونت و حاشیه‌شان قطعاً یکسان باشد.
        private static System.Windows.Forms.Button MkPagerButton(string text)
        {
            var btn = new System.Windows.Forms.Button();
            btn.Text = text;
            btn.Dock = System.Windows.Forms.DockStyle.Fill;
            btn.Margin = new System.Windows.Forms.Padding(3);
            btn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = CaseManagement.Helpers.UiTheme.Border;
            btn.FlatAppearance.BorderSize = 1;
            btn.BackColor = CaseManagement.Helpers.UiTheme.CardBack;
            btn.ForeColor = CaseManagement.Helpers.UiTheme.TextDark;
            btn.Font = CaseManagement.Helpers.UiTheme.Font(CaseManagement.Helpers.UiTheme.SizeSmall);
            btn.TabStop = false;
            return btn;
        }

        // ─── کارتِ آماریِ تبِ «خلاصه پرونده» ──────────────────────────────────
        // آموزش — عمداً از Helpers.StatCard استفاده *نشد*: آن کارت یک Sparkline
        // (نمودارِ خطی) در پایین خود دارد و خواستهٔ صریحِ کاربر «هیچ نموداری
        // اضافه نشود» بود. این کارت فقط سه چیز دارد: عنوان، عددِ درشت، واحد —
        // دقیقاً همان چیزی که در تصویرِ مرجع دیده می‌شود.
        private static CaseManagement.Helpers.SectionCard MkSummaryStat(
            string title, System.Windows.Forms.Label valueLabel, string unit,
            System.Drawing.Color valueColor)
        {
            var lblTitle = new System.Windows.Forms.Label();
            lblTitle.Dock = System.Windows.Forms.DockStyle.Top;
            lblTitle.Height = 20;
            lblTitle.Text = title;
            lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            lblTitle.Font = CaseManagement.Helpers.UiTheme.Font(CaseManagement.Helpers.UiTheme.SizeSmall - 1F);
            lblTitle.ForeColor = CaseManagement.Helpers.UiTheme.TextMuted;
            lblTitle.BackColor = System.Drawing.Color.Transparent;

            var lblUnit = new System.Windows.Forms.Label();
            lblUnit.Dock = System.Windows.Forms.DockStyle.Bottom;
            lblUnit.Height = 18;
            lblUnit.Text = unit;
            lblUnit.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            lblUnit.Font = CaseManagement.Helpers.UiTheme.Font(CaseManagement.Helpers.UiTheme.SizeSmall - 1.5F);
            lblUnit.ForeColor = CaseManagement.Helpers.UiTheme.TextMuted;
            lblUnit.BackColor = System.Drawing.Color.Transparent;

            valueLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            valueLabel.Text = "—";
            valueLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            valueLabel.Font = CaseManagement.Helpers.UiTheme.FontBold(16F);
            valueLabel.ForeColor = valueColor;
            valueLabel.BackColor = System.Drawing.Color.Transparent;
            valueLabel.AutoSize = false;

            var card = new CaseManagement.Helpers.SectionCard();
            card.Dock = System.Windows.Forms.DockStyle.Fill;
            card.Margin = new System.Windows.Forms.Padding(SummaryStatGap);
            card.Padding = new System.Windows.Forms.Padding(6, 8, 6, 6);
            card.Controls.Add(valueLabel);
            card.Controls.Add(lblUnit);
            card.Controls.Add(lblTitle);
            return card;
        }

        // فاصلهٔ یکنواختِ بینِ کارت‌های آماری — همان عددِ حاشیهٔ کارت‌های چپ.
        private const int SummaryStatGap = 5;

        #endregion
        // ─── کنترل‌های تازهٔ چیدمان (فاز بازسازیِ ظاهرِ FrmCase) ─────────────
        // کارتِ «وضعیت خدمات» در ستونِ چپ
        private System.Windows.Forms.Label lblSvcBadge;
        private System.Windows.Forms.Label lblSvcStartValue;
        private System.Windows.Forms.Label lblSvcChangeValue;
        // نوارِ صفحه‌بندیِ زیرِ گرید
        private System.Windows.Forms.Button btnGridFirst;
        private System.Windows.Forms.Button btnGridPrev;
        private System.Windows.Forms.Button btnGridNext;
        private System.Windows.Forms.Button btnGridLast;
        private System.Windows.Forms.Label lblGridPage;
        private System.Windows.Forms.Label lblGridTotal;
        // کارت‌های آماریِ تبِ «خلاصه پرونده»
        private System.Windows.Forms.Label lblStatMembers;
        private System.Windows.Forms.Label lblStatLastAid;
        private System.Windows.Forms.Label lblStatTotalAid;
        private System.Windows.Forms.Label lblStatDocs;
        private System.Windows.Forms.Label lblStatVisits;
        // فیلدهای تازهٔ خواندنیِ تبِ خلاصه (مقادیرشان از همان کنترل‌های موجود
        // کپی می‌شوند — هیچ ستون/جدولِ تازه‌ای در کار نیست)
        private System.Windows.Forms.TextBox txtSummaryProvince;
        private System.Windows.Forms.TextBox txtSummaryDistrict;
        private System.Windows.Forms.TextBox txtSummaryVillage;
        private System.Windows.Forms.TextBox txtSummaryStartDate;
        private System.Windows.Forms.TextBox txtSummaryPhone;
        private System.Windows.Forms.TextBox txtSummaryTazkira;
        private System.Windows.Forms.TextBox txtSummaryOfficer;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnHistory;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.DataGridView dgvCases;
        private System.Windows.Forms.TextBox txtPhotoPath;
        private CaseManagement.Helpers.PersianDatePicker dtpCaseDate;
        private System.Windows.Forms.Button btnBrowsePhoto;
        private System.Windows.Forms.Button btnClearPhoto;
        private System.Windows.Forms.Button btnBrowseFamilyPhoto;
        private System.Windows.Forms.Button btnClearFamilyPhoto;
        private System.Windows.Forms.TextBox txtFamilyPhotoPath;
        private System.Windows.Forms.PictureBox picPhoto;
        private System.Windows.Forms.PictureBox picFamilyPhoto;
        private System.Windows.Forms.Label lblCaseDate;
        private System.Windows.Forms.TextBox txtRelationshipToFamily;
        private System.Windows.Forms.ComboBox txtCoveredByOrg;
        // اسامی مؤسسات تحت پوشش — فقط وقتی «تحت پوشش دیگر مؤسسات = بله» دیده می‌شود.
        private System.Windows.Forms.TextBox txtCoveredByOrgNames;
        private System.Windows.Forms.Label lblCoveredByOrgNames;
        private CaseManagement.Helpers.FieldBox fieldCoveredByOrgNames;
        private CaseManagement.Helpers.FieldBox fieldMigrationCardType;
        // کارت‌های اختصاصیِ نوع پرونده (در تبِ «مشخصات پرونده»).
        private System.Windows.Forms.Panel cardDisabilityInfo;
        private System.Windows.Forms.Panel cardOrphanInfo;
        private System.Windows.Forms.Panel cardGuardianInfo;
        private System.Windows.Forms.Panel cardMigrantInfo;
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
        private System.Windows.Forms.Button btnExportExcel;
        private System.Windows.Forms.Button btnBatchExport;
        // آموزش — به فیلد ارتقا یافت تا کد فرم بتواند هنگام تغییر اندازه،
        // بیشینه‌ی عرضش را به عرضِ والد مقید کند (توضیح کامل کنار ساختش).
        internal System.Windows.Forms.FlowLayoutPanel bottomActionsRow;
        internal System.Windows.Forms.TableLayoutPanel rootLayout;
        // کانتینرِ فیلد «دلیل قطع موقت» — برای پنهان/نمایان‌کردن کلِ فیلد
        // هماهنگ با منطقِ موجود در FrmCase.cs.
        private CaseManagement.Helpers.FieldBox caseFieldStopReason;
        // کانتینرِ فیلد «دلیل تعلیق» — همان الگو، برای وضعیت «قطع»/«قطع موقت».
        private CaseManagement.Helpers.FieldBox caseFieldSuspensionReason;
        private System.Windows.Forms.Label lblSuspensionReason;
        private System.Windows.Forms.ComboBox txtSuspensionReason;
        private System.Windows.Forms.Button btnPrint;
        private System.Windows.Forms.Label lblServiceStatusFilter;
        private System.Windows.Forms.CheckBox chkHeadHealthy;
        private System.Windows.Forms.ComboBox cmbServiceStatusFilter;
        private System.Windows.Forms.Label lblExportSection;
        private CaseManagement.Helpers.PersianDatePicker dtpSurveyDate;
        private System.Windows.Forms.Label label28;
        private System.Windows.Forms.TextBox txtLocationAddress;
        // Phase 3 — معرف
        private System.Windows.Forms.Label lblSite;
        private System.Windows.Forms.ComboBox txtSite;
        private System.Windows.Forms.Label label29;
        private System.Windows.Forms.TextBox txtReferrerName;
        private System.Windows.Forms.Label label30;
        private System.Windows.Forms.TextBox txtReferrerPhone;
        // Phase 3 (بازبینی) — بخش‌های اختصاصیِ نوع درخواست
        private System.Windows.Forms.Label label31;
        private System.Windows.Forms.TextBox txtMainResidenceProvince;
        private System.Windows.Forms.Label label32;
        private System.Windows.Forms.TextBox txtMainResidenceDistrict;
        private System.Windows.Forms.Label label33;
        private System.Windows.Forms.TextBox txtMainResidenceVillage;
        private System.Windows.Forms.Label label34;
        private System.Windows.Forms.ComboBox txtFatherDeathCause;
        private System.Windows.Forms.Label label35;
        private System.Windows.Forms.ComboBox txtDisabilityCause;
        private System.Windows.Forms.Label label36;
        private System.Windows.Forms.TextBox txtDisabilityDescription;
        private System.Windows.Forms.Label label37;
        private System.Windows.Forms.TextBox txtSpecialNeeds;
        private System.Windows.Forms.Label label38;
        private System.Windows.Forms.ComboBox txtDisabilityCardStatus;
        private System.Windows.Forms.Label label39;
        private System.Windows.Forms.TextBox txtDisabilityCardNumber;
        private System.Windows.Forms.Label label40;
        private System.Windows.Forms.ComboBox txtHasMigrationCard;
        private System.Windows.Forms.Label label41;
        private System.Windows.Forms.TextBox txtMigrationCardNumber;
        private System.Windows.Forms.Label label42;
        private CaseManagement.Helpers.PersianDatePicker dtpDepartureDate;
        private System.Windows.Forms.Label label43;
        private CaseManagement.Helpers.PersianDatePicker dtpArrivalDate;
        private System.Windows.Forms.Label label44;
        private System.Windows.Forms.TextBox txtAssistanceDurationMonths;

        // ─── Phase 7 — نمایندهٔ قانونی ───────────────────────────
        private System.Windows.Forms.TabPage tabRepresentative;
        private System.Windows.Forms.Label lblRepresentativeHint;
        private System.Windows.Forms.GroupBox grpRepresentative1;
        private System.Windows.Forms.GroupBox grpRepresentative2;

        private System.Windows.Forms.Label lblRep1Name;
        private System.Windows.Forms.TextBox txtRep1Name;
        private System.Windows.Forms.Label lblRep1Relationship;
        private System.Windows.Forms.ComboBox txtRep1Relationship;
        private System.Windows.Forms.Label lblRep1IdCardType;
        private System.Windows.Forms.ComboBox cmbRep1IdCardType;
        private System.Windows.Forms.Label lblRep1NationalID;
        private System.Windows.Forms.TextBox txtRep1NationalID;
        private System.Windows.Forms.Label lblRep1Phone;
        private System.Windows.Forms.TextBox txtRep1Phone;
        private System.Windows.Forms.Label lblRep1Phone2;
        private System.Windows.Forms.TextBox txtRep1Phone2;
        private System.Windows.Forms.Label lblRep1Address;
        private System.Windows.Forms.TextBox txtRep1Address;
        private System.Windows.Forms.Label lblRep1Notes;
        private System.Windows.Forms.TextBox txtRep1Notes;
        private System.Windows.Forms.PictureBox picRep1Photo;
        private System.Windows.Forms.Button btnRep1BrowsePhoto;
        private System.Windows.Forms.Button btnRep1ClearPhoto;

        private System.Windows.Forms.Label lblRep2Name;
        private System.Windows.Forms.TextBox txtRep2Name;
        private System.Windows.Forms.Label lblRep2Relationship;
        private System.Windows.Forms.ComboBox txtRep2Relationship;
        private System.Windows.Forms.Label lblRep2IdCardType;
        private System.Windows.Forms.ComboBox cmbRep2IdCardType;
        private System.Windows.Forms.Label lblRep2NationalID;
        private System.Windows.Forms.TextBox txtRep2NationalID;
        private System.Windows.Forms.Label lblRep2Phone;
        private System.Windows.Forms.TextBox txtRep2Phone;
        private System.Windows.Forms.Label lblRep2Phone2;
        private System.Windows.Forms.TextBox txtRep2Phone2;
        private System.Windows.Forms.Label lblRep2Address;
        private System.Windows.Forms.TextBox txtRep2Address;
        private System.Windows.Forms.Label lblRep2Notes;
        private System.Windows.Forms.TextBox txtRep2Notes;
        private System.Windows.Forms.PictureBox picRep2Photo;
        private System.Windows.Forms.Button btnRep2BrowsePhoto;
        private System.Windows.Forms.Button btnRep2ClearPhoto;
        private System.Windows.Forms.Button btnRep2Clear;

        private CaseManagement.Helpers.FieldBox[] orphanSectionFields;
        private CaseManagement.Helpers.FieldBox[] disabilitySectionFields;
        private CaseManagement.Helpers.FieldBox[] migrantSectionFields;
        // Phase 4 — بخشِ سرپرست + فیلدهای ماژول‌های تخصصی.
        private CaseManagement.Helpers.FieldBox[] guardianSectionFields;
        // Feature 3 — عکسِ سرپرستِ کودک.
        private System.Windows.Forms.PictureBox picGuardianPhoto;
        private System.Windows.Forms.Button btnGuardianBrowsePhoto;
        private System.Windows.Forms.Button btnGuardianClearPhoto;
        private System.Windows.Forms.Label lblGuardianPhoto;
        private System.Windows.Forms.Label label45;
        private System.Windows.Forms.ComboBox txtFatherStatus;
        private System.Windows.Forms.Label label46;
        private CaseManagement.Helpers.PersianDatePicker dtpFatherDeathDate;
        private System.Windows.Forms.Label label47;
        private System.Windows.Forms.ComboBox txtMotherStatus;
        private System.Windows.Forms.Label label48;
        private System.Windows.Forms.TextBox txtOrphanSchoolName;
        private System.Windows.Forms.Label label49;
        private System.Windows.Forms.ComboBox txtOrphanEducationLevel;
        private System.Windows.Forms.Label lblIsStudent;
        private System.Windows.Forms.CheckBox chkIsStudent;
        private System.Windows.Forms.Label label50;
        private System.Windows.Forms.TextBox txtOrphanNotes;
        private System.Windows.Forms.Label label51;
        private System.Windows.Forms.TextBox txtGuardianName;
        private System.Windows.Forms.Label label52;
        private System.Windows.Forms.ComboBox txtGuardianRelationship;
        private System.Windows.Forms.Label label53;
        private System.Windows.Forms.TextBox txtCardIssuer;
        private System.Windows.Forms.Label label54;
        private CaseManagement.Helpers.PersianDatePicker dtpDisabilityIssueDate;
        private System.Windows.Forms.Label label55;
        private CaseManagement.Helpers.PersianDatePicker dtpDisabilityExpiryDate;
        private System.Windows.Forms.Label label56;
        private System.Windows.Forms.TextBox txtDisabilityNotes;
        private System.Windows.Forms.Label label57;
        private System.Windows.Forms.TextBox txtOriginCountry;
        private System.Windows.Forms.Label label58;
        private System.Windows.Forms.TextBox txtDestinationCountry;
        private System.Windows.Forms.Label label59;
        private System.Windows.Forms.TextBox txtMigrantNotes;
        // Phase 5 — تب بازدید میدانی.
        private System.Windows.Forms.TabPage tabVisits;
        private System.Windows.Forms.GroupBox grpVisitEntry;
        private System.Windows.Forms.DataGridView dgvVisits;
        private System.Windows.Forms.DataGridView dgvTimeline;
        private System.Windows.Forms.TabPage tabTimeline;
        private CaseManagement.Helpers.PersianDatePicker dtpVisitDate;
        private System.Windows.Forms.Label lblVisitDate;
        private System.Windows.Forms.TextBox txtVisitorName;
        private System.Windows.Forms.Label lblVisitorName;
        private System.Windows.Forms.ComboBox txtVisitResult;
        private System.Windows.Forms.Label lblVisitResult;
        private System.Windows.Forms.ComboBox txtVisitRecommendation;
        private System.Windows.Forms.Label lblVisitRecommendation;
        private System.Windows.Forms.TextBox txtVisitNotes;
        private System.Windows.Forms.Label lblVisitNotes;
        private System.Windows.Forms.Button btnVisitNew;
        private System.Windows.Forms.Button btnVisitSave;
        private System.Windows.Forms.Button btnVisitDelete;
        private System.Windows.Forms.Button btnVisitPhotos;
        // گام ۱ — منوهای تجمیعی (خروجی‌ها / فورم‌های رسمی).
        private System.Windows.Forms.ContextMenuStrip _menuExports;
        private System.Windows.Forms.ContextMenuStrip _menuOfficialForms;
        // Phase 5.5-C — کارت‌های وضعیتِ محاسبه‌شده + تب تأمین مالی + خروجی کامل.
        private System.Windows.Forms.Label lblStatCompletionPct;
        private System.Windows.Forms.Label lblStatCompletionStatus;
        private System.Windows.Forms.Label lblStatVulnScore;
        private System.Windows.Forms.Label lblStatSuggestedAid;
        private System.Windows.Forms.Label lblStatVerifiedDocs;
        private System.Windows.Forms.TabPage tabFunding;
        private System.Windows.Forms.DataGridView dgvCaseFunding;
        private System.Windows.Forms.Button btnFundingAssign;
        private System.Windows.Forms.Button btnFundingRemove;
        private System.Windows.Forms.Button btnExportCaseFile;
        // Phase 5.5-B — تب امتیاز آسیب‌پذیری (فقط‌خواندنی).
        private System.Windows.Forms.TabPage tabVulnerability;
        private System.Windows.Forms.Label lblVulnScoreValue;
        private System.Windows.Forms.Label lblVulnScoreDate;
        private System.Windows.Forms.DataGridView dgvVulnBreakdown;
        // Phase 6 — تب خانواده.
        private System.Windows.Forms.TabPage tabFamilyGroup;
        private System.Windows.Forms.Label lblFamilyGroupValue;
        private System.Windows.Forms.DataGridView dgvFamilyCases;
        private System.Windows.Forms.Button btnFamilyLink;
        private System.Windows.Forms.Button btnFamilyUnlink;
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
        private System.Windows.Forms.Label lblHeadIdCardType;
        private System.Windows.Forms.ComboBox cmbHeadIdCardType;
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
        private RtlTabControl tabsCase;
        private System.Windows.Forms.Panel tabMembersHost;
        private System.Windows.Forms.Label lblMembersPlaceholder;

        // فاز A2 — نوار جستجوی سریع (Quick Search Bar)
        private System.Windows.Forms.TextBox txtQsCode;
        private System.Windows.Forms.TextBox txtQsHeadName;
        private System.Windows.Forms.TextBox txtQsTazkira;
        private System.Windows.Forms.TextBox txtQsPhone;

        // فاز A3 — تب «خلاصه پرونده»
        private System.Windows.Forms.PictureBox picSummaryPhoto;
        private System.Windows.Forms.TextBox txtSummaryCode;
        private System.Windows.Forms.TextBox txtSummaryHeadName;
        private System.Windows.Forms.TextBox txtSummaryRequestType;
        private System.Windows.Forms.TextBox txtSummaryServiceStatus;
        private System.Windows.Forms.TextBox txtSummaryLocation;
        private System.Windows.Forms.TextBox txtSummaryMemberCount;
        private System.Windows.Forms.TextBox txtSummaryLastAssistance;
        private System.Windows.Forms.TextBox txtSummaryLastChange;

        // فاز A4 — تب «اسناد پرونده»
        private System.Windows.Forms.Panel tabDocsHost;
        private System.Windows.Forms.Label lblDocsPlaceholder;

        // تبِ «مشخصات کلی سرپرست» — مقصدِ خودکارِ دکمه‌های جدید/ویرایش
        private System.Windows.Forms.TabPage tabHeadInfo;

        // ستونِ سمتِ چپ (عکس‌ها + فیلتر + فهرستِ پرونده‌ها)
        internal System.Windows.Forms.Panel leftWorkspacePanel;

        // برچسبِ گروهِ دکمه‌های پرونده در نوار پایین (هم‌سبکِ lblExportSection)
        private System.Windows.Forms.Label lblCaseSection;
    }
}

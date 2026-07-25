using System.Windows.Forms;
namespace CaseManagement
{
    partial class FrmFamily
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            // ─── ساخت کنترل‌ها ────────────────────────────────────────────────
            this.btnNew                 = new System.Windows.Forms.Button();
            this.btnDelete              = new System.Windows.Forms.Button();
            this.btnEdit                = new System.Windows.Forms.Button();
            this.btnSave                = new System.Windows.Forms.Button();
            this.btnPrint               = new System.Windows.Forms.Button();
            this.dgvFamily              = new System.Windows.Forms.DataGridView();
            this.txtMemberName          = new System.Windows.Forms.TextBox();
            this.txtMemberFatherName    = new System.Windows.Forms.TextBox();
            this.txtMemberTazkiraNo     = new System.Windows.Forms.TextBox();
            this.txtMemberSadat         = new System.Windows.Forms.ComboBox();
            this.txtGender              = new System.Windows.Forms.ComboBox();
            this.txtPhysicalStatus      = new System.Windows.Forms.ComboBox();
            this.txtHasDisability       = new System.Windows.Forms.ComboBox();
            this.txtMemberDisabilityDegree = new System.Windows.Forms.ComboBox();
            this.txtMemberEducation     = new System.Windows.Forms.ComboBox();
            this.txtGradeLevel          = new System.Windows.Forms.ComboBox();
            this.txtStudyYear           = new System.Windows.Forms.ComboBox();
            this.txtSchoolName          = new System.Windows.Forms.TextBox();
            this.txtUniversityName      = new System.Windows.Forms.TextBox();
            this.txtMajor               = new System.Windows.Forms.TextBox();
            this.txtStudyField          = new System.Windows.Forms.TextBox();
            this.txtOfficialStatus      = new System.Windows.Forms.TextBox();
            this.txtSkill               = new System.Windows.Forms.TextBox();
            this.txtLeaveReason         = new System.Windows.Forms.TextBox();
            this.txtDetails             = new System.Windows.Forms.TextBox();
            this.txtDisabilityDetails   = new System.Windows.Forms.TextBox();
            this.txtMemberPhotoPath     = new System.Windows.Forms.TextBox();
            this.txtStopReason          = new System.Windows.Forms.TextBox();
            this.txtSchoolPrevGrade     = new System.Windows.Forms.TextBox();
            this.txtUniversityPrevGrade = new System.Windows.Forms.TextBox();
            this.cmbReligion            = new System.Windows.Forms.ComboBox();
            this.cmbMaritalStatus       = new System.Windows.Forms.ComboBox();
            this.cmbServiceStatus       = new System.Windows.Forms.ComboBox();
            this.cmbSchoolType          = new System.Windows.Forms.ComboBox();
            this.cmbUniversityType      = new System.Windows.Forms.ComboBox();
            this.cmbSeminaryLevel       = new System.Windows.Forms.ComboBox();
            this.cmbEducationCoverage   = new System.Windows.Forms.ComboBox();
            this.picMemberPhoto         = new System.Windows.Forms.PictureBox();
            this.btnBrowseMemberPhoto   = new System.Windows.Forms.Button();
            this.dtpBirthDate           = new CaseManagement.Helpers.PersianDatePicker();
            this.tabsMain               = new System.Windows.Forms.TabControl();
            this.lblHeadInfo            = new System.Windows.Forms.Label();
            this.label1  = new System.Windows.Forms.Label();
            this.label2  = new System.Windows.Forms.Label();
            this.label3  = new System.Windows.Forms.Label();
            this.label4  = new System.Windows.Forms.Label();
            this.label5  = new System.Windows.Forms.Label();
            this.label6  = new System.Windows.Forms.Label();
            this.label7  = new System.Windows.Forms.Label();
            this.label8  = new System.Windows.Forms.Label();
            this.label9  = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.label18 = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.label20 = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.label22 = new System.Windows.Forms.Label();
            this.label23 = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.label27 = new System.Windows.Forms.Label();
            this.lblReligion            = new System.Windows.Forms.Label();
            this.lblMaritalStatus       = new System.Windows.Forms.Label();
            this.lblServiceStatus       = new System.Windows.Forms.Label();
            this.lblStopReason          = new System.Windows.Forms.Label();
            this.lblDisabilityDetails   = new System.Windows.Forms.Label();
            this.lblSchoolType          = new System.Windows.Forms.Label();
            this.lblUniversityType      = new System.Windows.Forms.Label();
            this.lblSeminaryLevel       = new System.Windows.Forms.Label();
            this.lblEducationCoverage   = new System.Windows.Forms.Label();
            this.lblSchoolPrevGrade     = new System.Windows.Forms.Label();
            this.lblUniversityPrevGrade = new System.Windows.Forms.Label();

            ((System.ComponentModel.ISupportInitialize)(this.dgvFamily)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picMemberPhoto)).BeginInit();
            this.SuspendLayout();

            // ─── کمبوها: آیتم‌های ثابت ────────────────────────────────────────
            DdlCombo(this.txtMemberSadat, "عام", "سادات", "همه");
            DdlCombo(this.cmbReligion, "اهل تشیع", "اهل تسنن");
            DdlCombo(this.txtGender, "دختر", "پسر");
            DdlCombo(this.cmbMaritalStatus, "مجرد", "متأهل", "مطلقه");
            DdlCombo(this.txtPhysicalStatus, "سالم", "معلول", "مریض");
            DdlCombo(this.txtMemberDisabilityDegree, "اول", "دوم", "سوم");
            DdlCombo(this.txtMemberEducation, "مکتب", "دانشگاه", "طلبه", "بی‌سواد", "ترک تحصیل");
            this.txtMemberEducation.SelectedIndexChanged += new System.EventHandler(this.txtMemberEducation_SelectedIndexChanged);
            this.txtGradeLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            for (int i = 1; i <= 12; i++) this.txtGradeLevel.Items.Add(i.ToString());
            DdlCombo(this.txtStudyYear,
                "سمستر اول", "سمستر دوم", "سمستر سوم", "سمستر چهارم",
                "سمستر پنجم", "سمستر ششم", "سمستر هفتم", "سمستر هشتم",
                "لیسانس", "ماستری", "دکترا");
            DdlCombo(this.cmbServiceStatus, "فعال", "در انتظار تأیید", "قطع موقت", "قطع");
            this.cmbServiceStatus.SelectedIndexChanged += new System.EventHandler(this.cmbServiceStatus_SelectedIndexChanged);
            DdlCombo(this.txtHasDisability, "", "جسمی", "ذهنی", "بینایی", "شنوایی", "گفتاری", "حسی");
            DdlCombo(this.cmbSchoolType, "", "خصوصی", "دولتی");
            DdlCombo(this.cmbUniversityType, "", "خصوصی", "دولتی");
            DdlCombo(this.cmbSeminaryLevel, "", "سطح ۱", "سطح ۲", "سطح ۳");
            DdlCombo(this.cmbEducationCoverage, "", "بله", "خیر");

            // ─── نوار اطلاعات سرپرست ─────────────────────────────────────────
            this.lblHeadInfo.Name      = "lblHeadInfo";
            this.lblHeadInfo.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblHeadInfo.BackColor = CaseManagement.Helpers.UiTheme.PrimaryDark;
            this.lblHeadInfo.ForeColor = System.Drawing.Color.White;
            this.lblHeadInfo.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblHeadInfo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblHeadInfo.Text      = "سرپرست: —";

            // ═══════════════════════════════════════════════════════════════════
            // تب ۱: مشخصات کلی — دو ستونه، برچسبِ بالای فیلد (طبق طرح مرجع)
            // آموزش — هیچ فیلدی حذف/جابه‌جا نشده؛ فقط سبکِ چیدمان از «برچسبِ
            // کنارِ فیلد» به «برچسبِ بالای فیلد در دو ستون» تغییر کرده تا با
            // طرح مرجع یکی شود و فضای عمودی کمتری بگیرد.
            // ═══════════════════════════════════════════════════════════════════
            var tlpGeneral = MkFieldGrid(2);
            AddField(tlpGeneral, this.label1,            "نام",             this.txtMemberName);
            AddField(tlpGeneral, this.label8,            "سیادت",           this.txtMemberSadat);
            AddField(tlpGeneral, this.label2,            "نام پدر",          this.txtMemberFatherName);
            AddField(tlpGeneral, this.lblReligion,       "مذهب",            this.cmbReligion);
            AddField(tlpGeneral, this.label3,            "شماره تذکره",      this.txtMemberTazkiraNo);
            AddField(tlpGeneral, this.lblMaritalStatus,  "وضعیت تأهل",      this.cmbMaritalStatus);
            AddField(tlpGeneral, this.label4,            "تاریخ تولد",       this.dtpBirthDate);
            AddField(tlpGeneral, this.lblServiceStatus,  "وضعیت خدمات",     this.cmbServiceStatus);
            AddField(tlpGeneral, this.label7,            "جنسیت",           this.txtGender);
            this.fieldStopReason = AddField(tlpGeneral, this.lblStopReason, "دلیل قطع موقت", this.txtStopReason);

            // همان رفتار قبلی: تا وقتی وضعیت خدمات «قطع موقت» نشده، پنهان است.
            // (منطقِ نمایش/پنهان‌سازی در FrmFamily.cs دست‌نخورده مانده و همچنان
            // روی همین دو کنترل کار می‌کند؛ اینجا فقط کانتینرشان هم پنهان می‌شود
            // تا جای خالی در شبکه باقی نماند.)
            this.lblStopReason.Visible  = false;
            this.txtStopReason.Visible  = false;
            this.fieldStopReason.Visible = false;

            var tabGeneral = new System.Windows.Forms.TabPage("مشخصات کلی");
            tabGeneral.BackColor = CaseManagement.Helpers.UiTheme.Background;
            tabGeneral.Padding   = System.Windows.Forms.Padding.Empty;
            tabGeneral.Controls.Add(tlpGeneral);

            // ═══════════════════════════════════════════════════════════════════
            // تب ۲: مشخصات جسمی — ۴ ردیف استاندارد + عنوان + textarea پر
            // ═══════════════════════════════════════════════════════════════════
            var tlpPhysical = MkTlp(4, 150);
            tlpPhysical.RowCount = 6;
            tlpPhysical.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tlpPhysical.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));

            FieldRow(tlpPhysical, 0, SetLbl(this.label6,  "وضعیت جسمی"),    this.txtPhysicalStatus);
            FieldRow(tlpPhysical, 1, SetLbl(this.label5,  "نوع معلولیت"),    this.txtHasDisability);
            FieldRow(tlpPhysical, 2, SetLbl(this.label12, "درجه معلولیت"),   this.txtMemberDisabilityDegree);
            FieldRow(tlpPhysical, 3, SetLbl(this.label17, "مهارت"),          this.txtSkill);

            // ردیف ۴: عنوان تمام‌عرض
            SetLbl(this.lblDisabilityDetails, "توضیحات بیشتر (شرح تفصیلی معلولیت)");
            this.lblDisabilityDetails.Dock    = System.Windows.Forms.DockStyle.Fill;
            this.lblDisabilityDetails.Padding = new System.Windows.Forms.Padding(4, 6, 4, 0);
            tlpPhysical.Controls.Add(this.lblDisabilityDetails, 0, 4);
            tlpPhysical.SetColumnSpan(this.lblDisabilityDetails, 2);

            // ردیف ۵: textarea پر
            this.txtDisabilityDetails.Name       = "txtDisabilityDetails";
            this.txtDisabilityDetails.Multiline  = true;
            this.txtDisabilityDetails.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDisabilityDetails.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtDisabilityDetails.TextAlign  = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtDisabilityDetails.Dock       = System.Windows.Forms.DockStyle.Fill;
            this.txtDisabilityDetails.Margin     = new System.Windows.Forms.Padding(4, 2, 4, 8);
            tlpPhysical.Controls.Add(this.txtDisabilityDetails, 0, 5);
            tlpPhysical.SetColumnSpan(this.txtDisabilityDetails, 2);

            var tabPhysical = new System.Windows.Forms.TabPage("مشخصات جسمی");
            tabPhysical.BackColor = CaseManagement.Helpers.UiTheme.Background;
            tabPhysical.Padding   = System.Windows.Forms.Padding.Empty;
            tabPhysical.Controls.Add(tlpPhysical);

            // ═══════════════════════════════════════════════════════════════════
            // تب ۳: مشخصات تحصیلی — ۱۵ ردیف، AutoScroll، Dock=Top+AutoSize
            // ═══════════════════════════════════════════════════════════════════
            var tlpEdu = MkTlp(15, 160);
            tlpEdu.Dock         = System.Windows.Forms.DockStyle.Top;
            tlpEdu.AutoSize     = true;
            tlpEdu.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;

            FieldRow(tlpEdu,  0, SetLbl(this.label11,           "تحصیلات"),                 this.txtMemberEducation);
            FieldRow(tlpEdu,  1, SetLbl(this.label10,           "نام مکتب"),                  this.txtSchoolName);
            FieldRow(tlpEdu,  2, SetLbl(this.label9,            "صنف"),                      this.txtGradeLevel);
            FieldRow(tlpEdu,  3, SetLbl(this.lblSchoolType,     "نوع مکتب"),                  this.cmbSchoolType);
            FieldRow(tlpEdu,  4, SetLbl(this.lblSchoolPrevGrade,"معدل سال قبل (مکتب)"),      this.txtSchoolPrevGrade);
            FieldRow(tlpEdu,  5, SetLbl(this.lblEducationCoverage,"تحت پوشش آموزشی"),        this.cmbEducationCoverage);
            FieldRow(tlpEdu,  6, SetLbl(this.label24,           "نام دانشگاه"),              this.txtUniversityName);
            FieldRow(tlpEdu,  7, SetLbl(this.label22,           "رشته دانشگاه"),             this.txtMajor);
            FieldRow(tlpEdu,  8, SetLbl(this.label23,           "سمستر/درجه دانشگاه"),      this.txtStudyYear);
            FieldRow(tlpEdu,  9, SetLbl(this.lblUniversityType, "نوع دانشگاه"),              this.cmbUniversityType);
            FieldRow(tlpEdu, 10, SetLbl(this.lblUniversityPrevGrade,"معدل سال قبل (دانشگاه)"), this.txtUniversityPrevGrade);
            FieldRow(tlpEdu, 11, SetLbl(this.label21,           "حوزه علمیه"),               this.txtStudyField);
            FieldRow(tlpEdu, 12, SetLbl(this.lblSeminaryLevel,  "دروس حوزوی (سطح)"),        this.cmbSeminaryLevel);
            FieldRow(tlpEdu, 13, SetLbl(this.label18,           "دلیل ترک تحصیل"),           this.txtLeaveReason);
            FieldRow(tlpEdu, 14, SetLbl(this.label19,           "توضیحات کلی"),              this.txtDetails);

            var tabEdu = new System.Windows.Forms.TabPage("مشخصات تحصیلی");
            tabEdu.BackColor  = CaseManagement.Helpers.UiTheme.Background;
            tabEdu.AutoScroll = true;
            tabEdu.Padding    = System.Windows.Forms.Padding.Empty;
            tabEdu.Controls.Add(tlpEdu);

            // کنترل‌های مخفی که توسط منطق .cs استفاده می‌شوند
            this.txtOfficialStatus.Name    = "txtOfficialStatus";
            this.txtOfficialStatus.Visible = false;
            this.txtMemberPhotoPath.Name   = "txtMemberPhotoPath";
            this.txtMemberPhotoPath.Visible = false;
            this.label20.Visible = false;
            this.label27.Visible = false;
            var hiddenPanel = new System.Windows.Forms.Panel();
            hiddenPanel.Visible = false;
            hiddenPanel.Size    = new System.Drawing.Size(0, 0);
            hiddenPanel.Controls.Add(this.txtOfficialStatus);
            hiddenPanel.Controls.Add(this.txtMemberPhotoPath);
            hiddenPanel.Controls.Add(this.label20);
            hiddenPanel.Controls.Add(this.label27);

            // ─── TabControl ───────────────────────────────────────────────────
            this.tabsMain.Name              = "tabsMain";
            this.tabsMain.Dock              = System.Windows.Forms.DockStyle.Fill;
            this.tabsMain.Font              = CaseManagement.Helpers.UiTheme.FontBold(CaseManagement.Helpers.UiTheme.SizeSmall);
            this.tabsMain.RightToLeft       = System.Windows.Forms.RightToLeft.Yes;
            this.tabsMain.RightToLeftLayout = true;
            this.tabsMain.Padding           = new System.Drawing.Point(14, 4);
            this.tabsMain.TabPages.Add(tabGeneral);
            this.tabsMain.TabPages.Add(tabPhysical);
            this.tabsMain.TabPages.Add(tabEdu);

            var fieldsPanel = new System.Windows.Forms.Panel();
            fieldsPanel.Name    = "fieldsPanel";
            fieldsPanel.Dock    = System.Windows.Forms.DockStyle.Fill;
            fieldsPanel.Padding = new System.Windows.Forms.Padding(4);
            fieldsPanel.Controls.Add(this.tabsMain);

            // ═══════════════════════════════════════════════════════════════════
            // پانل راست: عکس (بالا) + گرید (پرکننده)
            // ═══════════════════════════════════════════════════════════════════
            this.picMemberPhoto.Name        = "picMemberPhoto";
            this.picMemberPhoto.Dock        = System.Windows.Forms.DockStyle.Fill;
            this.picMemberPhoto.SizeMode    = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picMemberPhoto.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.picMemberPhoto.TabStop     = false;
            this.picMemberPhoto.BackColor   = CaseManagement.Helpers.UiTheme.Background;

            this.btnBrowseMemberPhoto.Name   = "btnBrowseMemberPhoto";
            this.btnBrowseMemberPhoto.Text   = "انتخاب عکس";
            this.btnBrowseMemberPhoto.Dock   = System.Windows.Forms.DockStyle.Bottom;
            this.btnBrowseMemberPhoto.Height = 32;
            this.btnBrowseMemberPhoto.Margin = new System.Windows.Forms.Padding(0);
            this.btnBrowseMemberPhoto.Click += new System.EventHandler(this.btnBrowseMemberPhoto_Click);

            var photoPanel = new System.Windows.Forms.Panel();
            photoPanel.Name    = "photoPanel";
            photoPanel.Dock    = System.Windows.Forms.DockStyle.Top;
            photoPanel.Height  = 210;
            photoPanel.Padding = new System.Windows.Forms.Padding(6, 6, 6, 0);
            photoPanel.Controls.Add(this.picMemberPhoto);
            photoPanel.Controls.Add(this.btnBrowseMemberPhoto);

            this.dgvFamily.Name                        = "dgvFamily";
            this.dgvFamily.Dock                        = System.Windows.Forms.DockStyle.Fill;
            this.dgvFamily.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvFamily.RowHeadersWidth             = 51;
            this.dgvFamily.RowTemplate.Height          = 24;
            this.dgvFamily.CellClick                  += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvFamily_CellClick);
            this.dgvFamily.CellContentClick           += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvFamily_CellContentClick);

            var gridPanel = new System.Windows.Forms.Panel();
            gridPanel.Name    = "gridPanel";
            gridPanel.Dock    = System.Windows.Forms.DockStyle.Fill;
            gridPanel.Padding = new System.Windows.Forms.Padding(6, 4, 6, 6);
            gridPanel.Controls.Add(this.dgvFamily);

            var rightPanel = new System.Windows.Forms.Panel();
            rightPanel.Name = "rightPanel";
            rightPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            rightPanel.Controls.Add(gridPanel);
            rightPanel.Controls.Add(photoPanel);

            // ═══════════════════════════════════════════════════════════════════
            // نوار دکمه‌ها (پایین)
            // ═══════════════════════════════════════════════════════════════════
            SetBtn(this.btnNew,    "btnNew",    "جدید",       this.btnNew_Click);
            SetBtn(this.btnSave,   "btnSave",   "ذخیره",      this.btnSave_Click);
            SetBtn(this.btnEdit,   "btnEdit",   "ویرایش",     this.btnEdit_Click);
            SetBtn(this.btnDelete, "btnDelete", "حذف",        this.btnDelete_Click);
            SetBtn(this.btnPrint,  "btnPrint",  "چاپ فهرست",  this.btnPrint_Click);

            var buttonBar = new System.Windows.Forms.FlowLayoutPanel();
            buttonBar.Name          = "buttonBar";
            buttonBar.Dock          = System.Windows.Forms.DockStyle.Fill;
            buttonBar.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            buttonBar.WrapContents  = false;
            buttonBar.Padding       = new System.Windows.Forms.Padding(8, 0, 8, 0);
            buttonBar.Controls.Add(this.btnNew);
            buttonBar.Controls.Add(this.btnSave);
            buttonBar.Controls.Add(this.btnEdit);
            buttonBar.Controls.Add(this.btnDelete);
            buttonBar.Controls.Add(this.btnPrint);

            // ═══════════════════════════════════════════════════════════════════
            // چیدمان ریشه: ۲ ستون (۵۸% | ۴۲%)، ۳ ردیف (header | content | buttons)
            // ═══════════════════════════════════════════════════════════════════
            var rootLayout = new System.Windows.Forms.TableLayoutPanel();
            rootLayout.Name        = "rootLayout";
            rootLayout.Dock        = System.Windows.Forms.DockStyle.Fill;
            rootLayout.ColumnCount = 2;
            rootLayout.RowCount    = 3;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent,  58F));
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent,  42F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  34F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent,  100F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,  56F));

            rootLayout.Controls.Add(this.lblHeadInfo, 0, 0);
            rootLayout.SetColumnSpan(this.lblHeadInfo, 2);
            rootLayout.Controls.Add(fieldsPanel, 0, 1);
            rootLayout.Controls.Add(rightPanel,  1, 1);
            rootLayout.Controls.Add(buttonBar,   0, 2);
            rootLayout.SetColumnSpan(buttonBar, 2);

            // ─── Form ─────────────────────────────────────────────────────────
            this.AutoScaleMode     = System.Windows.Forms.AutoScaleMode.None;
            this.Font              = new System.Drawing.Font("Segoe UI", 9.75F);
            this.RightToLeft       = System.Windows.Forms.RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.ClientSize        = new System.Drawing.Size(1160, 560);
            this.FormBorderStyle   = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox       = false;
            this.MinimizeBox       = true;
            this.StartPosition     = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Name              = "FrmFamily";
            this.Text              = "اعضای خانواده";
            this.Load             += new System.EventHandler(this.FrmFamily_Load);
            this.Controls.Add(rootLayout);
            this.Controls.Add(hiddenPanel);

            ((System.ComponentModel.ISupportInitialize)(this.dgvFamily)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picMemberPhoto)).EndInit();
            this.ResumeLayout(false);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        // ─── شبکه‌ی فیلدها به سبک طرح مرجع ────────────────────────────────────
        // چند ستونِ هم‌عرض؛ هر سلول یک FieldBox (برچسبِ بالا + ورودیِ گردگوشه).
        // ردیف‌ها AutoSize‌اند تا ارتفاع دقیقاً به‌اندازه‌ی محتوا باشد و فضای
        // خالیِ نامتعارف ایجاد نشود.
        private static System.Windows.Forms.TableLayoutPanel MkFieldGrid(int columns)
        {
            var tlp = new System.Windows.Forms.TableLayoutPanel();
            tlp.Dock = System.Windows.Forms.DockStyle.Top;
            tlp.AutoSize = true;
            tlp.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tlp.Padding = new System.Windows.Forms.Padding(18, 14, 18, 10);
            tlp.ColumnCount = columns;
            tlp.BackColor = System.Drawing.Color.Transparent;
            for (int i = 0; i < columns; i++)
                tlp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                    System.Windows.Forms.SizeType.Percent, 100F / columns));
            return tlp;
        }

        // افزودن یک فیلد به شبکه. خروجی، کانتینرِ آن فیلد است تا در صورت نیاز
        // (مثل «دلیل قطع موقت») بتوان کلِ فیلد را پنهان/نمایان کرد.
        // بسیار مهم: خودِ کنترلِ ورودی همان شیء قبلی می‌ماند، پس نام، رویدادها
        // و هر کدی که با آن کار می‌کند دست‌نخورده باقی است.
        private static CaseManagement.Helpers.FieldBox AddField(
            System.Windows.Forms.TableLayoutPanel grid,
            System.Windows.Forms.Label captionLabel, string captionText,
            System.Windows.Forms.Control field)
        {
            var box = new CaseManagement.Helpers.FieldBox(captionLabel, captionText, field);
            box.Dock = System.Windows.Forms.DockStyle.Top;
            grid.Controls.Add(box);
            return box;
        }

        // ساخت TableLayoutPanel دو ستونه: col0=لیبل (عرض ثابت) | col1=فیلد (پر)
        private static System.Windows.Forms.TableLayoutPanel MkTlp(int rowCount, int labelColWidth)
        {
            var tlp = new System.Windows.Forms.TableLayoutPanel();
            tlp.Dock     = System.Windows.Forms.DockStyle.Fill;
            tlp.Padding  = new System.Windows.Forms.Padding(10, 12, 10, 8);
            tlp.ColumnCount = 2;
            tlp.RowCount    = rowCount;
            tlp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, (float)labelColWidth));
            tlp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            for (int i = 0; i < rowCount; i++)
                tlp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            return tlp;
        }

        // پیکربندی لیبل: پر کردن سلول، راست‌چین، بدون AutoSize
        private static System.Windows.Forms.Label SetLbl(System.Windows.Forms.Label lbl, string text)
        {
            lbl.Text      = text;
            lbl.AutoSize  = false;
            lbl.Dock      = System.Windows.Forms.DockStyle.Fill;
            lbl.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            lbl.Padding   = new System.Windows.Forms.Padding(0, 0, 8, 0);
            return lbl;
        }

        // اضافه کردن جفت لیبل+فیلد به یک ردیف مشخص از TLP
        private static void FieldRow(System.Windows.Forms.TableLayoutPanel tlp, int row,
            System.Windows.Forms.Label lbl, System.Windows.Forms.Control field)
        {
            tlp.Controls.Add(lbl, 0, row);

            field.Anchor = System.Windows.Forms.AnchorStyles.Left
                         | System.Windows.Forms.AnchorStyles.Right
                         | System.Windows.Forms.AnchorStyles.Top;
            field.Margin = new System.Windows.Forms.Padding(2, 4, 2, 4);

            var tb = field as System.Windows.Forms.TextBox;
            if (tb != null && !tb.Multiline)
                field.Height = 26;
            else if (!(field is System.Windows.Forms.TextBox))
                field.Height = 26;

            tlp.Controls.Add(field, 1, row);
        }

        // تنظیم کمبو: DropDownList + آیتم‌ها
        private static void DdlCombo(System.Windows.Forms.ComboBox cmb, params string[] items)
        {
            cmb.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmb.Items.AddRange(items);
        }

        // تنظیم دکمه: اندازه، حاشیه، رویداد
        private static void SetBtn(System.Windows.Forms.Button btn, string name, string text,
            System.EventHandler clickHandler)
        {
            btn.Name    = name;
            btn.Text    = text;
            btn.Size    = new System.Drawing.Size(120, 40);
            btn.Margin  = new System.Windows.Forms.Padding(4, 8, 4, 8);
            btn.Click  += clickHandler;
        }

        #endregion

        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnPrint;
        private System.Windows.Forms.DataGridView dgvFamily;
        private System.Windows.Forms.TextBox txtMemberName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtMemberFatherName;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtMemberTazkiraNo;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox txtHasDisability;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox txtPhysicalStatus;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox txtGender;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ComboBox txtMemberSadat;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox txtGradeLevel;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox txtSchoolName;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.ComboBox txtMemberEducation;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.ComboBox txtMemberDisabilityDegree;
        private System.Windows.Forms.TextBox txtMemberPhotoPath;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.TextBox txtSkill;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.TextBox txtLeaveReason;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.TextBox txtDetails;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.TextBox txtOfficialStatus;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.TextBox txtStudyField;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.TextBox txtMajor;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.ComboBox txtStudyYear;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.TextBox txtUniversityName;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.PictureBox picMemberPhoto;
        private System.Windows.Forms.Button btnBrowseMemberPhoto;
        private CaseManagement.Helpers.PersianDatePicker dtpBirthDate;
        private System.Windows.Forms.ComboBox cmbReligion;
        private System.Windows.Forms.Label lblReligion;
        private System.Windows.Forms.ComboBox cmbMaritalStatus;
        private System.Windows.Forms.Label lblMaritalStatus;
        private System.Windows.Forms.ComboBox cmbServiceStatus;
        private System.Windows.Forms.Label lblServiceStatus;
        private System.Windows.Forms.TextBox txtStopReason;
        private System.Windows.Forms.Label lblStopReason;
        private System.Windows.Forms.Label lblHeadInfo;
        private System.Windows.Forms.Label lblDisabilityDetails;
        private System.Windows.Forms.TextBox txtDisabilityDetails;
        private System.Windows.Forms.TabControl tabsMain;
        private System.Windows.Forms.ComboBox cmbSchoolType;
        private System.Windows.Forms.ComboBox cmbUniversityType;
        private System.Windows.Forms.ComboBox cmbSeminaryLevel;
        private System.Windows.Forms.ComboBox cmbEducationCoverage;
        private System.Windows.Forms.TextBox txtSchoolPrevGrade;
        private System.Windows.Forms.TextBox txtUniversityPrevGrade;
        private System.Windows.Forms.Label lblSchoolType;
        private System.Windows.Forms.Label lblUniversityType;
        private System.Windows.Forms.Label lblSeminaryLevel;
        private System.Windows.Forms.Label lblEducationCoverage;
        private System.Windows.Forms.Label lblSchoolPrevGrade;
        private System.Windows.Forms.Label lblUniversityPrevGrade;
        // کانتینرِ فیلد «دلیل قطع موقت» — برای پنهان/نمایان‌کردن کلِ فیلد
        // (برچسب + ورودی) هماهنگ با منطقِ موجود در FrmFamily.cs.
        private CaseManagement.Helpers.FieldBox fieldStopReason;
    }
}

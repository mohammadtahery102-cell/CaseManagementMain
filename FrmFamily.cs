using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using static CaseManagement.Helpers.SqlHelpers;

namespace CaseManagement
{
    public partial class FrmFamily : Form
    {
        private const string MemberPhotosSectionName = "MemberPhotos";

        private const int MemberNameLength = 200;
        private const int MemberFatherNameLength = 200;
        private const int MemberTazkiraNoLength = 100;
        private const int MemberSadatLength = 50;
        private const int GenderLength = 50;
        private const int PhysicalStatusLength = 100;
        private const int HasDisabilityLength = 100;
        private const int MemberDisabilityDegreeLength = 100;
        private const int MemberEducationLength = 100;
        private const int SchoolNameLength = 200;
        private const int GradeLevelLength = 100;
        private const int UniversityNameLength = 200;
        private const int StudyYearLength = 100;
        private const int MajorLength = 200;
        private const int StudyFieldLength = 200;
        private const int OfficialStatusLength = 100;
        private const int SkillLength = 100;
        private const int LeaveReasonLength = 200;
        private const int MemberPhotoPathLength = 500;
        private const int MemberRoleLength = 50;
        private const int RelationLength = 50;

        private const long MaxMemberPhotoBytes = 5L * 1024 * 1024;

        private static readonly HashSet<string> AllowedPhotoExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png"
            };

        private readonly DatabaseHelper db = new DatabaseHelper();

        public int CurrentCaseId { get; set; } = 0;
        public string CurrentCaseCode { get; set; } = "";

        // آموزش — فاز ۱ (تب اعضاء داخل FrmCase): وقتی این فرم به‌جای پنجره‌ی
        // مستقل داخل یک TabPage میزبانی می‌شود، FrmCase این پرچم را قبل از
        // Show() روی true می‌گذارد. هیچ منطق دیگری به این پرچم وابسته نیست
        // جز FrmFamily_Load (زیر) — رفتار حالت مستقل/مودال کاملاً دست‌نخورده می‌ماند.
        public bool IsEmbedded { get; set; } = false;

        // آموزش — پیمایشِ پرونده از داخلِ همین تب (درخواستِ کاربر: «دکمهٔ بعدی
        // و قبلی که نظر به شمارهٔ فرم پرونده بالا و پایین برود»). این فرم
        // نباید خودش پرونده بارگذاری کند (منطقِ پرونده فقط در FrmCase است)، پس
        // FrmCase موقعِ جاسازی این delegate را می‌دهد: ورودی ۱+/۱- و خروجی
        // «آیا پروندهٔ دیگری پیدا و بارگذاری شد». در حالتِ مستقل/مودال null
        // می‌ماند و نوارِ دکمه‌ها اصلاً نمایش داده نمی‌شود.
        public Func<int, bool> CaseNavigator { get; set; }

        private int currentFamilyId = 0;
        private string storedMemberPhotoPath = "";
        private string pendingSourcePhotoPath = "";

        // ─── ویژگی ۵ (فعال‌سازی) — قفل رکورد ────────────────────────────────
        // آموزش: برخلافِ FrmCase که یک حالتِ صریحِ «ویرایش» دارد، اینجا فیلدها
        // همیشه قابلِ تایپ‌اند و بارگذاریِ یک عضو (LoadMemberToForm) همان لحظه‌ای
        // است که ریسکِ هم‌پوشانیِ ویرایش شروع می‌شود؛ پس قفل همان‌جا گرفته
        // می‌شود، نه در دکمه‌ی «ویرایش».
        private int  _familyLockId = 0;
        private bool _familyLockedByOther = false;
        private System.Windows.Forms.Timer _familyLockHeartbeat;

        public FrmFamily()
        {
            InitializeComponent();
            ApplyCustomTheme();

            Helpers.FormShortcuts.For(this)
                .Save(btnSave)
                .New(btnNew)
                .Edit(btnEdit)
                .Delete(btnDelete)
                .Print(btnPrint);
        }

        // ─── اعمال ظاهر یکسان روی فرمی که با طراح (Designer) ساخته شده ──────
        private void ApplyCustomTheme()
        {
            UiTheme.ApplySweep(this);

            // بخش ۳ — ورودی هوشمند تذکره: فقط رقم، درج خودکار «-» در حالت
            // الکترونیکی، و تغییر خودکار قالب با تغییر نوع تذکره.
            IdCardHelper.Attach(cmbMemberIdCardType, txtMemberTazkiraNo);

            // آموزش — چهار دکمه‌ی اصلی متن و آیکون و رنگشان را از خودِ Designer
            // می‌گیرند (PaintBtn). فراخوانی SetButtonIcon برایشان باعث می‌شد
            // آیکون دوباره جلوی متن اضافه شود — و چون فونت فارسیِ برنامه گلیفِ
            // «✎» را ندارد، روی دکمه‌ی ویرایش یک مربعِ خالی «▯» ظاهر می‌شد
            // (در تست تصویری دیده شد). فقط دکمه‌ی انتخاب عکس آیکون می‌گیرد.
            UiTheme.SetButtonIcon(btnBrowseMemberPhoto, "▤");

            // آموزش — ApplySweep روی همه‌ی Labelها ForeColor تیره می‌گذارد؛
            // نوار سرپرست زمینه‌ی سرمه‌ای تیره دارد، پس متنش عملاً ناخوانا
            // می‌شد (در تست تصویری تیره روی تیره دیده شد). رنگ سفیدش بعد از
            // Sweep دوباره اعمال می‌شود.
            lblHeadInfo.ForeColor = Color.White;

            // آموزش — ApplySweep هر Panel را سفید می‌کند؛ نوارِ سرِ فرم و
            // میزبانِ دکمه‌های پیمایش باید سرمه‌ای بمانند (وگرنه دکمه‌های آبی
            // روی زمینهٔ سفید وسطِ نوارِ تیره یک لکهٔ سفید می‌سازند).
            headBarPanel.BackColor = UiTheme.PrimaryDark;
            panCaseNav.BackColor   = UiTheme.PrimaryDark;

            foreach (Button navBtn in new[] { btnPrevCase, btnNextCase })
            {
                navBtn.BackColor = UiTheme.Primary;
                navBtn.ForeColor = Color.White;
                navBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Primary, 0.18f);
                navBtn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Primary, 0.08f);
            }

            btnDelete.BackColor = UiTheme.Danger;
            btnDelete.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Danger, 0.18f);
            btnDelete.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Danger, 0.08f);

            btnSave.BackColor = UiTheme.Success;
            btnSave.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Success, 0.18f);
            btnSave.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Success, 0.08f);
        }

        private void FrmFamily_Load(object sender, EventArgs e)
        {
            // آموزش — وقتی embedded است (TopLevel=false داخل تب FrmCase)، این
            // فرم دیگر یک پنجره‌ی مستقل نیست؛ قفل‌کردن Min/MaxSize و
            // FormBorderStyle توسط MakeFixedSize با Dock=Fill داخل تب تداخل
            // می‌کند (اندازه‌ی تب را نمی‌تواند دنبال کند). در حالت مستقل/مودال
            // رفتار قبلی کاملاً دست‌نخورده می‌ماند.
            if (!IsEmbedded)
            {
                // اندازه‌ی ثابت (نه Maximize) — به درخواست کاربر (توضیح در UiTheme.MakeFixedSize)
                UiTheme.MakeFixedSize(this, ClientSize.Width, ClientSize.Height);
            }
            else
            {
                // آموزش — رفعِ گزارشِ کاربر «دکمه‌ها برای اسناد و اعضاء جداست»:
                // داخلِ فضای کاریِ پرونده، این دکمه‌ها کنارِ دکمه‌های *پرونده*
                // دیده می‌شوند و چون هر دو «جدید/ذخیره/ویرایش/حذف» بودند،
                // تشخیصِ اینکه کدام روی پرونده و کدام روی عضو کار می‌کند ممکن
                // نبود. در حالتِ embedded صریحاً «عضو» به متن اضافه می‌شود؛
                // حالتِ مستقل/مودال دقیقاً مثل قبل می‌ماند.
                btnNew.Text    = "＋   عضو جدید";
                btnSave.Text   = "✔   ذخیره عضو";
                btnEdit.Text   = "ویرایش عضو";
                btnDelete.Text = "✕   حذف عضو";

                // دکمه‌های «پروندهٔ قبلی/بعدی» فقط وقتی معنا دارند که FrmCase
                // میزبان باشد و delegate پیمایش را داده باشد.
                panCaseNav.Visible = CaseNavigator != null;
            }

            Text = "اعضای خانواده" +
                   (string.IsNullOrEmpty(CurrentCaseCode) ? "" : "  —  پرونده: " + CurrentCaseCode) +
                   "  [" + SecurityContext.CenterDisplay + "]";
            txtMemberPhotoPath.ReadOnly = true;
            picMemberPhoto.SizeMode = PictureBoxSizeMode.Zoom;

            dtpBirthDate.ShowCheckBox = true;

            // آموزش — به درخواست کاربر: با انتخاب «سالم» در وضعیت جسمی، بقیه
            // فیلدهای مربوط به معلولیت غیرفعال و خالی شوند.
            txtPhysicalStatus.SelectedIndexChanged += delegate { UpdatePhysicalFieldsState(); };

            LoadLookupCombos();
            LoadHeadInfo();
            ConfigureGrid();
            LoadFamilyMembers();
            ClearForm();
        }

        // آموزش — فاز ۱: وقتی FrmFamily embedded داخل تب FrmCase است، به‌جای
        // ساختن نمونه‌ی تازه هر بار، FrmCase همین نمونه را نگه می‌دارد و وقتی
        // کاربر پرونده‌ی دیگری را در گرید انتخاب می‌کند، این متد را صدا
        // می‌زند. دقیقاً همان مراحلِ انتهای FrmFamily_Load را تکرار می‌کند
        // (بدون LoadLookupCombos/ConfigureGrid چون آن‌ها یک‌بار در Load کافی‌اند
        // و LookupHelper خودش cache شده است).
        // آموزش — دو دکمهٔ پیمایش. خودشان هیچ کوئری‌ای نمی‌زنند؛ فقط delegate
        // را صدا می‌زنند. اگر پرونده‌ای پیدا شود، FrmCase بارگذاری می‌کند و
        // خودش RefreshForCase همین فرم را صدا می‌زند (SyncMembersTab)، پس
        // این‌جا کاری برای به‌روزرسانی لازم نیست.
        private void btnPrevCase_Click(object sender, EventArgs e)
        {
            if (CaseNavigator != null)
                CaseNavigator(-1);
        }

        private void btnNextCase_Click(object sender, EventArgs e)
        {
            if (CaseNavigator != null)
                CaseNavigator(1);
        }

        public void RefreshForCase(int caseId, string caseCode)
        {
            CurrentCaseId = caseId;
            CurrentCaseCode = caseCode;

            Text = "اعضای خانواده" +
                   (string.IsNullOrEmpty(CurrentCaseCode) ? "" : "  —  پرونده: " + CurrentCaseCode) +
                   "  [" + SecurityContext.CenterDisplay + "]";

            LoadHeadInfo();
            LoadFamilyMembers();
            ClearForm();
        }

        // با انتخاب «سالم» به‌عنوان وضعیت جسمی، فیلدهای نوع/درجه معلولیت و
        // توضیحات تفصیلی معلولیت غیرفعال و پاک می‌شوند (چون فرد معلولیتی ندارد).
        //
        // آموزش — دقیقاً همان اصلاحی که برای فیلدهای تحصیلی انجام شد (توضیح
        // کامل بالای UpdateEducationFieldsState): خالی کردنِ فیلدها فقط وقتی
        // درست است که *کاربر* وضعیت جسمی را عوض کند. هنگام *بارگذاری* یک عضو
        // موجود، این کار داده‌ی معتبرِ رکوردهای قدیمی را از بین می‌برد — عضوی
        // که وضعیت جسمی‌اش «سالم» ثبت شده ولی اطلاعات معلولیت هم دارد، با
        // بازکردن و زدنِ «ویرایش» آن اطلاعات را از دست می‌داد. پس مسیر
        // بارگذاری فقط وضعیت فعال/غیرفعال را اعمال می‌کند.
        private void UpdatePhysicalFieldsState()
        {
            UpdatePhysicalFieldsState(true);
        }

        private void UpdatePhysicalFieldsState(bool clearMismatchedFields)
        {
            bool isHealthy = txtPhysicalStatus.Text == "سالم";

            txtHasDisability.Enabled = !isHealthy;
            txtMemberDisabilityDegree.Enabled = !isHealthy;
            txtDisabilityDetails.Enabled = !isHealthy;

            if (isHealthy && clearMismatchedFields)
            {
                if (txtHasDisability.Items.Count > 0) txtHasDisability.SelectedIndex = 0; // گزینه خالی
                txtMemberDisabilityDegree.SelectedIndex = -1;
                txtDisabilityDetails.Text = "";
            }
        }

        // آموزش — همانند FrmCase: کمبوها از TblLookup بارگذاری می‌شوند (نه
        // Designer.cs) تا مدیر سیستم بتواند از تنظیمات ویرایششان کند. مقادیر
        // هاردکد Designer.cs به‌عنوان fallback می‌مانند. cmbServiceStatus عمداً
        // اینجا نیست (به همان دلیل FrmCase — رشته‌های دقیق در منطق «دلیل قطع
        // موقت» استفاده می‌شوند). توجه: تغییر مقادیر دسته MemberEducation از
        // تنظیمات روی منطق فعال/غیرفعال تب تحصیلی اثر می‌گذارد چون آن منطق با
        // مقایسه متن دقیق («مکتب»، «دانشگاه»، «طلبه»، «ترک تحصیل») کار می‌کند.
        private void LoadLookupCombos()
        {
            LookupHelper.FillCombo(txtMemberSadat, "MemberSadat");
            LookupHelper.FillCombo(cmbMemberRole, "MemberRole");
            LookupHelper.FillCombo(cmbRelation, "FamilyRelation");
            LookupHelper.FillCombo(cmbReligion, "Madhab");
            LookupHelper.FillCombo(txtGender, "MemberGender");
            LookupHelper.FillCombo(cmbMaritalStatus, "MaritalStatus");
            LookupHelper.FillCombo(txtPhysicalStatus, "PhysicalStatus");
            LookupHelper.FillCombo(txtMemberDisabilityDegree, "DisabilityDegree");
            LookupHelper.FillCombo(txtMemberEducation, "MemberEducation");

            // آموزش — FillCombo برای گزینه «خالی» اول کار نمی‌کند (allLabel با
            // رشته خالی نادیده گرفته می‌شود)؛ اینجا دستی همان رفتار قبلی
            // Designer.cs (گزینه اول خالی = بدون معلولیت) حفظ می‌شود.
            string currentDisability = txtHasDisability.Text;
            txtHasDisability.Items.Clear();
            txtHasDisability.Items.Add("");
            foreach (string v in LookupHelper.GetValues("DisabilityType"))
                txtHasDisability.Items.Add(v);
            int hdIdx = txtHasDisability.FindStringExact(currentDisability);
            txtHasDisability.SelectedIndex = hdIdx >= 0 ? hdIdx : 0;

            LookupHelper.FillCombo(txtGradeLevel, "GradeLevel");
            LookupHelper.FillCombo(txtStudyYear, "StudyYear");
            LookupHelper.FillCombo(txtSuspensionReason, "SuspensionReason");
        }

        // نمایش کد/نام/نام پدر سرپرست در بالای فرم
        private void LoadHeadInfo()
        {
            if (CurrentCaseId <= 0)
            {
                lblHeadInfo.Text = "سرپرست: —";
                return;
            }

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT HeadFullName, HeadFatherName FROM TblCase WHERE CasID = @CasID", con))
                {
                    AddInt(cmd, "@CasID", CurrentCaseId);
                    con.Open();

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            // آموزش — جداکننده‌ی صریح («·») به‌جای چند فاصله‌ی
                            // پشت‌سرهم: در رندر راست‌به‌چپ، فاصله‌های متوالی
                            // جمع می‌شوند و بخش‌های متن به‌هم می‌چسبند و
                            // درهم دیده می‌شوند (در تست تصویری همین رخ داد).
                            lblHeadInfo.Text =
                                "کد سرپرست: " + CurrentCaseCode +
                                "   ·   نام سرپرست: " + DbString(dr["HeadFullName"]) +
                                "   ·   نام پدر: " + DbString(dr["HeadFatherName"]);
                        }
                    }
                }
            }
            catch { lblHeadInfo.Text = "سرپرست: —"; }
        }

        // فقط فیلدهای مرتبط با نوع تحصیل انتخاب‌شده فعال می‌شوند
        private void txtMemberEducation_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateEducationFieldsState();
        }

        // آموزش — منطق دقیق فعال/غیرفعال طبق درخواست کاربر:
        //   مکتب: فقط نام مکتب + صنف فعال
        //   دانشگاه: فقط نام دانشگاه + رشته + سمستر فعال
        //   طلبه: فقط حوزه علمیه (+ توضیحات) فعال
        //   ترک تحصیل: فقط دلیل ترک تحصیل + توضیحات فعال (همه بقیه غیرفعال)
        //   بی‌سواد: فقط توضیحات فعال (هیچ فیلد تحصیلی دیگری کاربرد ندارد)
        //   توضیحات کلی (txtDetails): همیشه فعال، مستقل از نوع تحصیل
        // آموزش — رفع «پاک شدن ناخواسته‌ی اطلاعات تحصیلی رکوردهای قبلی»:
        // این متد دو کار جدا انجام می‌داد؛ (۱) فعال/غیرفعال کردن فیلدها و
        // (۲) خالی کردنِ فیلدهایی که به نوع تحصیل انتخاب‌شده مربوط نیستند.
        // کار (۲) وقتی *کاربر* نوع تحصیل را عوض می‌کند درست است، ولی هنگام
        // *بارگذاری* یک عضو موجود اشتباه بود: مقادیر خوانده‌شده از دیتابیس
        // روی فرم پاک می‌شدند و با زدنِ «ویرایش» همان خالی‌ها ذخیره می‌شد،
        // یعنی داده‌ی معتبرِ رکوردهای قدیمی از بین می‌رفت. حالا مسیر بارگذاری
        // فقط وضعیت فعال/غیرفعال را اعمال می‌کند و هیچ مقداری را پاک نمی‌کند.
        private void UpdateEducationFieldsState()
        {
            UpdateEducationFieldsState(true);
        }

        private void UpdateEducationFieldsState(bool clearMismatchedFields)
        {
            string edu = txtMemberEducation.Text;

            bool isSchool = edu == "مکتب";
            bool isUniversity = edu == "دانشگاه";
            bool isSeminary = edu == "طلبه";
            bool isDropout = edu == "ترک تحصیل";
            bool isIlliterate = edu == "بی‌سواد";

            txtSchoolName.Enabled = isSchool;
            txtGradeLevel.Enabled = isSchool;
            cmbSchoolType.Enabled = isSchool;
            txtSchoolPrevGrade.Enabled = isSchool;

            txtUniversityName.Enabled = isUniversity;
            txtMajor.Enabled = isUniversity;
            txtStudyYear.Enabled = isUniversity;
            cmbUniversityType.Enabled = isUniversity;
            txtUniversityPrevGrade.Enabled = isUniversity;

            txtStudyField.Enabled = isSeminary;
            cmbSeminaryLevel.Enabled = isSeminary;

            txtLeaveReason.Enabled = isDropout;

            // «تحت پوشش آموزشی» برای مکتب و دانشگاه معنی دارد
            cmbEducationCoverage.Enabled = isSchool || isUniversity;

            // توضیحات کلی همیشه فعال است (طبق درخواست کاربر)
            txtDetails.Enabled = true;

            if (clearMismatchedFields)
            {
                if (!isSchool) { txtSchoolName.Text = ""; txtGradeLevel.SelectedIndex = -1; cmbSchoolType.SelectedIndex = -1; txtSchoolPrevGrade.Text = ""; }
                if (!isUniversity) { txtUniversityName.Text = ""; txtStudyYear.SelectedIndex = -1; txtMajor.Text = ""; cmbUniversityType.SelectedIndex = -1; txtUniversityPrevGrade.Text = ""; }
                if (!isSeminary) { txtStudyField.Text = ""; cmbSeminaryLevel.SelectedIndex = -1; }
                if (!isDropout) txtLeaveReason.Text = "";
                if (!isSchool && !isUniversity) cmbEducationCoverage.SelectedIndex = -1;
            }

            // آموزش — فیلد «وضعیت رسمی تحصیلی» به درخواست کاربر حذف شد؛
            // txtOfficialStatus دیگر در UI نیست و مقدارش همیشه خالی می‌ماند.
            txtOfficialStatus.Text = "";
        }

        // فیلدهای تعلیق («دلیل تعلیق» الزامی + «یادداشت» اختیاری) فقط وقتی
        // وضعیت خدمات «قطع» یا «قطع موقت» است نمایش داده می‌شوند.
        private void cmbServiceStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateStopReasonVisibility();
        }

        private bool IsSuspendedStatus(string status)
        {
            return status == "قطع موقت" || status == "قطع";
        }

        private void UpdateStopReasonVisibility()
        {
            bool isSuspended = IsSuspendedStatus(cmbServiceStatus.Text);

            lblStopReason.Visible = isSuspended;
            txtStopReason.Visible = isSuspended;
            if (fieldStopReason != null) fieldStopReason.Visible = isSuspended;

            lblSuspensionReason.Visible = isSuspended;
            txtSuspensionReason.Visible = isSuspended;
            if (fieldSuspensionReason != null) fieldSuspensionReason.Visible = isSuspended;

            if (!isSuspended)
            {
                txtStopReason.Text = "";
                txtSuspensionReason.SelectedIndex = -1;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ReleaseFamilyLock();
            ClearPicture(picMemberPhoto);
            DisposeAllThumbnails();
            base.OnFormClosed(e);
        }

        private void ConfigureGrid()
        {
            dgvFamily.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFamily.MultiSelect = false;
            dgvFamily.ReadOnly = true;
            dgvFamily.AllowUserToAddRows = false;
            dgvFamily.AllowUserToDeleteRows = false;
            dgvFamily.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            UiTheme.ApplyPersianDateColumns(dgvFamily, "BirthDate");
        }

        // قفلِ عضوِ جاری را آزاد و تایمرِ تمدید را متوقف می‌کند — قبل از
        // بارگذاریِ عضوی دیگر، پاک‌کردنِ فرم، یا بستنِ فرم صدا زده می‌شود.
        private void ReleaseFamilyLock()
        {
            if (_familyLockId > 0)
            {
                CaseManagement.Enterprise.LockService.Release(_familyLockId);
                _familyLockId = 0;
            }

            _familyLockedByOther = false;

            if (_familyLockHeartbeat != null)
                _familyLockHeartbeat.Stop();
        }

        // تلاش برای قفل کردنِ عضوِ تازه‌بارگذاری‌شده. اگر توسط کاربر دیگری
        // قفل باشد، داده همچنان برای مشاهده نمایش داده می‌شود (فقط هشدار داده
        // می‌شود)؛ خودِ ذخیره (btnEdit_Click) با پرچمِ _familyLockedByOther
        // مسدود می‌شود.
        private void TryLockFamilyMember(int famId)
        {
            if (famId <= 0) return;

            CaseManagement.Enterprise.LockResult lockResult =
                CaseManagement.Enterprise.LockService.TryAcquire("TblFamily", famId);

            if (!lockResult.Acquired)
            {
                _familyLockedByOther = true;
                Msg.Show(lockResult.DeniedMessage);
                return;
            }

            _familyLockId = lockResult.LockID;

            if (_familyLockHeartbeat == null)
            {
                _familyLockHeartbeat = new System.Windows.Forms.Timer();
                _familyLockHeartbeat.Interval = 5 * 60 * 1000; // ۵ دقیقه
                _familyLockHeartbeat.Tick += delegate
                {
                    CaseManagement.Enterprise.LockService.Heartbeat(_familyLockId);
                };
            }

            _familyLockHeartbeat.Start();
        }

        private void ClearForm()
        {
            ReleaseFamilyLock();
            currentFamilyId = 0;
            storedMemberPhotoPath = "";
            pendingSourcePhotoPath = "";

            txtMemberName.Text = "";
            txtMemberFatherName.Text = "";
            cmbMemberIdCardType.SelectedIndex = 0;
            txtMemberTazkiraNo.Text = "";
            txtMemberSadat.SelectedIndex = -1;
            cmbMemberRole.SelectedIndex = -1;
            cmbRelation.SelectedIndex = -1;
            txtGender.SelectedIndex = -1;
            txtPhysicalStatus.SelectedIndex = -1;
            txtHasDisability.SelectedIndex = 0;
            txtMemberDisabilityDegree.SelectedIndex = -1;
            txtMemberEducation.SelectedIndex = -1;
            txtSchoolName.Text = "";
            txtGradeLevel.SelectedIndex = -1;
            txtUniversityName.Text = "";
            txtStudyYear.SelectedIndex = -1;
            txtMajor.Text = "";
            txtStudyField.Text = "";
            txtOfficialStatus.Text = "";
            txtSkill.Text = "";
            txtLeaveReason.Text = "";
            txtDetails.Text = "";
            txtDisabilityDetails.Text = "";
            txtMemberPhotoPath.Text = "";

            // فیلدهای تحصیلی جدید
            cmbSchoolType.SelectedIndex = -1;
            txtSchoolPrevGrade.Text = "";
            cmbUniversityType.SelectedIndex = -1;
            txtUniversityPrevGrade.Text = "";
            cmbSeminaryLevel.SelectedIndex = -1;
            cmbEducationCoverage.SelectedIndex = -1;

            cmbReligion.SelectedIndex = -1;
            cmbMaritalStatus.SelectedIndex = -1;
            cmbServiceStatus.SelectedIndex = 0;
            txtStopReason.Text = "";
            txtSuspensionReason.SelectedIndex = -1;
            UpdateStopReasonVisibility();
            UpdateEducationFieldsState();

            dtpBirthDate.Value = DateTime.Today;
            dtpBirthDate.Checked = false;

            ClearPicture(picMemberPhoto);

            // آموزش — به درخواست کاربر: بعد از ذخیره/پاک‌کردن، کرسر به ابتدای
            // فرم (نام عضو) برگردد تا عضو بعدی سریع‌تر ثبت شود. چون «نام عضو»
            // روی تب اول است، ابتدا به تب اول برمی‌گردیم وگرنه Focus روی کنترلِ
            // تبِ غیرفعال اثر نمی‌کند.
            if (tabsMain != null && tabsMain.TabCount > 0)
                tabsMain.SelectedIndex = 0;
            txtMemberName.Focus();
        }

        private bool ValidateForm()
        {
            if (CurrentCaseId <= 0)
            {
                Msg.Show("ابتدا پرونده اصلی را ذخیره یا انتخاب کن");
                return false;
            }

            if (string.IsNullOrWhiteSpace(CurrentCaseCode))
            {
                Msg.Show("کد پرونده اصلی مشخص نیست");
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtMemberName.Text))
            {
                Msg.Show("نام عضو خانواده را وارد کنید");
                txtMemberName.Focus();
                return false;
            }

            // آموزش — به درخواست کاربر: «تحصیلات» و «بخش تحصیلات» اجباری است.
            // بدون آن، عضو در هیچ‌کدام از دسته‌های داشبورد اعضای خانواده
            // (دانشگاهی/متعلمین/طلبه/بی‌سواد/ترک تحصیل) شمرده نمی‌شود و آمار
            // ناقص می‌ماند؛ پس ذخیره تا تکمیل این بخش مجاز نیست.
            string edu = txtMemberEducation.Text.Trim();
            if (string.IsNullOrWhiteSpace(edu))
            {
                Msg.Show("تحصیلات عضو را از تب «مشخصات تحصیلی» انتخاب کنید (برای گزارش‌گیری الزامی است)");
                txtMemberEducation.Focus();
                return false;
            }

            if (edu == "مکتب" &&
                (string.IsNullOrWhiteSpace(txtSchoolName.Text) || string.IsNullOrWhiteSpace(txtGradeLevel.Text)))
            {
                Msg.Show("برای تحصیلات «مکتب»، نام مکتب و صنف را کامل کنید");
                txtSchoolName.Focus();
                return false;
            }

            if (edu == "دانشگاه" &&
                (string.IsNullOrWhiteSpace(txtUniversityName.Text) ||
                 string.IsNullOrWhiteSpace(txtMajor.Text) ||
                 string.IsNullOrWhiteSpace(txtStudyYear.Text)))
            {
                Msg.Show("برای تحصیلات «دانشگاه»، نام دانشگاه، رشته و سمستر/درجه را کامل کنید");
                txtUniversityName.Focus();
                return false;
            }

            if (edu == "طلبه" && string.IsNullOrWhiteSpace(txtStudyField.Text))
            {
                Msg.Show("برای تحصیلات «طلبه»، حوزه علمیه را وارد کنید");
                txtStudyField.Focus();
                return false;
            }

            if (edu == "ترک تحصیل" && string.IsNullOrWhiteSpace(txtLeaveReason.Text))
            {
                Msg.Show("برای «ترک تحصیل»، دلیل ترک تحصیل را وارد کنید");
                txtLeaveReason.Focus();
                return false;
            }

            if (IsSuspendedStatus(cmbServiceStatus.Text) && string.IsNullOrWhiteSpace(txtSuspensionReason.Text))
            {
                Msg.Show("دلیل تعلیق را از لیست انتخاب کنید");
                txtSuspensionReason.Focus();
                return false;
            }

            // ─── بخش ۳: اعتبارسنجی تذکره، وابسته به نوع انتخاب‌شده ───────────
            // قاعده کاملاً در IdCardHelper است تا با فرم پرونده یکی بماند.
            if (txtMemberTazkiraNo.Text.Trim().Length > 0)
            {
                string idCardError;
                if (!IdCardHelper.IsValid(cmbMemberIdCardType.Text, txtMemberTazkiraNo.Text, out idCardError))
                {
                    Msg.Show(idCardError);
                    if (string.IsNullOrWhiteSpace(cmbMemberIdCardType.Text))
                        cmbMemberIdCardType.Focus();
                    else
                        txtMemberTazkiraNo.Focus();
                    return false;
                }
            }

            return ValidateTextLengths();
        }

        private bool ValidateTextLengths()
        {
            return
                ValidateTextLength(txtMemberName, "نام", MemberNameLength) &&
                ValidateTextLength(txtMemberFatherName, "نام پدر", MemberFatherNameLength) &&
                ValidateTextLength(txtMemberTazkiraNo, "شماره تذکره", MemberTazkiraNoLength) &&
                ValidateTextLength(txtSchoolName, "نام مکتب", SchoolNameLength) &&
                ValidateTextLength(txtUniversityName, "نام دانشگاه", UniversityNameLength) &&
                ValidateTextLength(txtMajor, "رشته", MajorLength) &&
                ValidateTextLength(txtStudyField, "بخش تحصیل", StudyFieldLength) &&
                ValidateTextLength(txtOfficialStatus, "وضعیت رسمی", OfficialStatusLength) &&
                ValidateTextLength(txtSkill, "مهارت", SkillLength) &&
                ValidateTextLength(txtLeaveReason, "دلیل ترک تحصیل", LeaveReasonLength);
        }

        private bool ValidateTextLength(TextBox textBox, string fieldName, int maxLength)
        {
            if (textBox.Text.Trim().Length <= maxLength)
                return true;

            Msg.Show(fieldName + " نباید بیشتر از " + maxLength + " کاراکتر باشد");
            textBox.Focus();
            return false;
        }

        private bool IsValidImageFile(string filePath, bool showMessage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    if (showMessage)
                        Msg.Show("فایل عکس پیدا نشد");

                    return false;
                }

                string ext = Path.GetExtension(filePath);
                if (!AllowedPhotoExtensions.Contains(ext))
                {
                    if (showMessage)
                        Msg.Show("فقط فایل JPG، JPEG یا PNG مجاز است");

                    return false;
                }

                FileInfo fi = new FileInfo(filePath);
                if (fi.Length <= 0 || fi.Length > MaxMemberPhotoBytes)
                {
                    if (showMessage)
                        Msg.Show("حجم عکس باید کمتر از 5 مگابایت باشد");

                    return false;
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image.FromStream(fs, false, true))
                {
                    return true;
                }
            }
            catch
            {
                if (showMessage)
                    Msg.Show("فایل انتخاب‌شده عکس معتبر نیست");

                return false;
            }
        }

        private bool LoadImageToPictureBox(string filePath, PictureBox pictureBox, bool showMessage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    ClearPicture(pictureBox);
                    return false;
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image tempImage = Image.FromStream(fs, false, true))
                {
                    SetPictureBoxImage(pictureBox, new Bitmap(tempImage));
                }

                return true;
            }
            catch
            {
                ClearPicture(pictureBox);

                if (showMessage)
                    Msg.Show("نمایش عکس ممکن نیست");

                return false;
            }
        }

        private void SetPictureBoxImage(PictureBox pictureBox, Image image)
        {
            Image oldImage = pictureBox.Image;
            pictureBox.Image = image;

            if (oldImage != null)
                oldImage.Dispose();
        }

        private void ClearPicture(PictureBox pictureBox)
        {
            Image oldImage = pictureBox.Image;
            pictureBox.Image = null;

            if (oldImage != null)
                oldImage.Dispose();
        }

        private void LoadFamilyMembers()
        {
            if (CurrentCaseId <= 0)
            {
                dgvFamily.DataSource = null;
                return;
            }

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    SELECT FamID, MemberName, MemberFatherName, Gender, MemberRole, BirthDate, MemberEducation, Skill, MemberPhotoPath,
                           CASE WHEN BirthDate IS NULL THEN NULL
                                ELSE CAST((julianday('now') - julianday(BirthDate)) / 365.25 AS INTEGER) END AS Age
                    FROM TblFamily
                    WHERE CasID = @CasID
                    ORDER BY FamID DESC", con))
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    AddInt(cmd, "@CasID", CurrentCaseId);

                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    // آموزش — ترتیب مهم است: با عوض شدن DataSource، ردیف‌های
                    // فعلی (و تصاویر کوچکِ داخلشان) از بین می‌روند، پس آزادسازی
                    // باید *قبل* از انتساب انجام شود وگرنه آن Bitmapها نشت می‌کنند.
                    DisposeAllThumbnails();
                    dgvFamily.DataSource = dt;
                }

                ApplyGridHeaders();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری اعضای خانواده: " + ex.Message);
            }
        }

        private const string MemberPhotoThumbColumnName = "colMemberPhotoThumb";

        // آموزش — به درخواست کاربر: گرید اعضای خانواده فقط چهار ستون (نام/
        // نام پدر/سن/عکس) را نشان می‌دهد؛ بقیه ستون‌ها پنهان می‌مانند (نه حذف،
        // چون FamID برای dgvFamily_CellClick لازم است) و AutoSizeColumnsMode
        // Fill باعث می‌شود بدون اسکرول افقی دقیقاً با عرض پنل جفت شود.
        private void ApplyGridHeaders()
        {
            if (dgvFamily.Columns.Count == 0)
                return;

            if (dgvFamily.Columns.Contains("MemberName"))
                dgvFamily.Columns["MemberName"].HeaderText = "نام";

            if (dgvFamily.Columns.Contains("MemberFatherName"))
                dgvFamily.Columns["MemberFatherName"].HeaderText = "نام پدر";

            if (dgvFamily.Columns.Contains("Age"))
                dgvFamily.Columns["Age"].HeaderText = "سن";

            HideFamilyGridColumn("FamID");
            HideFamilyGridColumn("Gender");
            HideFamilyGridColumn("MemberRole");
            HideFamilyGridColumn("BirthDate");
            HideFamilyGridColumn("MemberEducation");
            HideFamilyGridColumn("Skill");
            HideFamilyGridColumn("MemberPhotoPath");

            if (!dgvFamily.Columns.Contains(MemberPhotoThumbColumnName))
            {
                var photoColumn = new DataGridViewImageColumn
                {
                    Name = MemberPhotoThumbColumnName,
                    HeaderText = "عکس",
                    ImageLayout = DataGridViewImageCellLayout.Zoom,
                    Resizable = DataGridViewTriState.False
                };
                // آموزش — رفع «آیکون ✕ قرمز در ستون عکس»: وقتی عضوی عکس ندارد
                // مقدار سلول null می‌ماند و DataGridView به‌صورت پیش‌فرض یک
                // آیکونِ «تصویر خراب» می‌کشد (در تست تصویری برای همه‌ی اعضای
                // بدون عکس دیده شد). با NullValue=null سلول به‌سادگی خالی
                // می‌ماند که هم درست‌تر است و هم تمیزتر.
                photoColumn.DefaultCellStyle.NullValue = null;
                dgvFamily.Columns.Add(photoColumn);
            }

            if (dgvFamily.Columns.Contains("MemberName"))
                dgvFamily.Columns["MemberName"].DisplayIndex = 0;
            if (dgvFamily.Columns.Contains("MemberFatherName"))
                dgvFamily.Columns["MemberFatherName"].DisplayIndex = 1;
            if (dgvFamily.Columns.Contains("Age"))
                dgvFamily.Columns["Age"].DisplayIndex = 2;
            dgvFamily.Columns[MemberPhotoThumbColumnName].DisplayIndex = dgvFamily.Columns.Count - 1;

            dgvFamily.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvFamily.RowTemplate.Height = 44;

            LoadMemberThumbnails();
            UpdateMembersHeader();
            StyleFamilyGrid();
        }

        // ─── شمارنده‌ی اعضا در سربرگ کارت لیست (فقط نمایشی) ──────────────────
        // آموزش — این متد هیچ داده‌ای نمی‌خواند و هیچ کوئری‌ای نمی‌زند؛ فقط
        // تعداد ردیف‌های همان گریدِ از قبل بارگذاری‌شده را نشان می‌دهد، پس
        // کاملاً افزایشی است و روی منطق موجود اثری ندارد.
        private void UpdateMembersHeader()
        {
            if (lblMembersHeader == null) return;
            int count = 0;
            foreach (DataGridViewRow row in dgvFamily.Rows)
                if (!row.IsNewRow) count++;
            lblMembersHeader.Text = "اعضای خانواده  (" + count + ")";
        }

        // ─── ظاهر گرید طبق طرح مرجع (فقط استایل، بدون تغییر داده/ستون) ───────
        private void StyleFamilyGrid()
        {
            dgvFamily.BorderStyle = BorderStyle.None;
            dgvFamily.BackgroundColor = UiTheme.CardBack;
            dgvFamily.GridColor = UiTheme.Border;
            dgvFamily.EnableHeadersVisualStyles = false;
            dgvFamily.RowHeadersVisible = false;
            dgvFamily.AllowUserToResizeRows = false;
            dgvFamily.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvFamily.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFamily.MultiSelect = false;

            dgvFamily.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.CardBack;
            dgvFamily.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.TextMuted;
            dgvFamily.ColumnHeadersDefaultCellStyle.Font = UiTheme.FontBold(UiTheme.SizeSmall - 1F);
            dgvFamily.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgvFamily.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            dgvFamily.ColumnHeadersHeight = 38;
            dgvFamily.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            dgvFamily.DefaultCellStyle.Font = UiTheme.Font(UiTheme.SizeSmall);
            dgvFamily.DefaultCellStyle.ForeColor = UiTheme.TextDark;
            dgvFamily.DefaultCellStyle.BackColor = UiTheme.CardBack;
            dgvFamily.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgvFamily.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            // ردیف انتخاب‌شده: آبیِ کم‌رنگ با متن تیره (مثل طرح مرجع) به‌جای
            // آبیِ پررنگِ پیش‌فرض ویندوز که متن را ناخوانا می‌کند.
            dgvFamily.DefaultCellStyle.SelectionBackColor = UiTheme.HoverTint;
            dgvFamily.DefaultCellStyle.SelectionForeColor = UiTheme.TextDark;
            dgvFamily.AlternatingRowsDefaultCellStyle.BackColor = UiTheme.CardBack;
        }

        private void HideFamilyGridColumn(string columnName)
        {
            if (dgvFamily.Columns.Contains(columnName))
                dgvFamily.Columns[columnName].Visible = false;
        }

        private void LoadMemberThumbnails()
        {
            if (!dgvFamily.Columns.Contains("MemberPhotoPath") || !dgvFamily.Columns.Contains(MemberPhotoThumbColumnName))
                return;

            foreach (DataGridViewRow row in dgvFamily.Rows)
            {
                if (row.IsNewRow)
                    continue;

                object pathValue = row.Cells["MemberPhotoPath"].Value;
                string path = pathValue == null || pathValue == DBNull.Value ? "" : pathValue.ToString();
                row.Height = 44;

                // آموزش — رفع نشت حافظه/GDI: این متد بعد از هر ثبت/ویرایش/حذف
                // دوباره اجرا می‌شود و برای هر ردیف یک Bitmap تازه می‌سازد.
                // قبلاً Bitmapِ قبلیِ همان سلول بدون Dispose رها می‌شد، پس با
                // هر بار بارگذاری فهرست، به‌تعدادِ اعضا handle گرافیکی نشت
                // می‌کرد. حالا تصویر قبلی صریحاً آزاد می‌شود.
                DisposeThumbnailCell(row);
                row.Cells[MemberPhotoThumbColumnName].Value = LoadThumbnail(path, 38);
            }
        }

        // تصویر کوچکِ ذخیره‌شده در سلولِ عکسِ یک ردیف را آزاد می‌کند.
        private void DisposeThumbnailCell(DataGridViewRow row)
        {
            if (!dgvFamily.Columns.Contains(MemberPhotoThumbColumnName))
                return;

            DataGridViewCell cell = row.Cells[MemberPhotoThumbColumnName];
            Image oldThumb = cell.Value as Image;

            if (oldThumb == null)
                return;

            cell.Value = null;
            oldThumb.Dispose();
        }

        // همه‌ی تصاویر کوچکِ گرید را آزاد می‌کند (هنگام بستن فرم).
        private void DisposeAllThumbnails()
        {
            if (!dgvFamily.Columns.Contains(MemberPhotoThumbColumnName))
                return;

            foreach (DataGridViewRow row in dgvFamily.Rows)
            {
                if (row.IsNewRow)
                    continue;

                DisposeThumbnailCell(row);
            }
        }

        private Image LoadThumbnail(string path, int size)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image source = Image.FromStream(fs))
                {
                    Bitmap thumb = new Bitmap(size, size);
                    using (Graphics g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(source, 0, 0, size, size);
                    }
                    return thumb;
                }
            }
            catch
            {
                return null;
            }
        }

        private void btnBrowseMemberPhoto_Click(object sender, EventArgs e)
        {
            if (CurrentCaseId <= 0 || string.IsNullOrWhiteSpace(CurrentCaseCode))
            {
                Msg.Show("اول پرونده اصلی را ذخیره یا انتخاب کن");
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "انتخاب عکس عضو خانواده";
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png|فایل‌های JPG|*.jpg;*.jpeg|فایل‌های PNG|*.png";

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                if (!IsValidImageFile(ofd.FileName, true))
                    return;

                pendingSourcePhotoPath = ofd.FileName;
                txtMemberPhotoPath.Text = pendingSourcePhotoPath;

                LoadImageToPictureBox(pendingSourcePhotoPath, picMemberPhoto, true);
            }
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            ClearForm();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Family.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه ثبت عضو خانواده ندارد.");
                return;
            }

            if (currentFamilyId > 0)
            {
                Msg.Show("برای ثبت عضو جدید ابتدا دکمه جدید را بزنید؛ برای رکورد انتخاب‌شده از دکمه ویرایش استفاده کنید");
                return;
            }

            if (!ValidateForm())
                return;

            string savedPhotoPath = "";
            bool copiedNewPhoto = false;
            string tazkiraAuditNote = null;

            try
            {
                // آموزش — هشدارِ تذکرهٔ تکراری: بررسیِ نرم، نه مانعِ قطعی. همان
                // الگویی که در FrmCase استفاده شده — کاربر باید صراحتاً تأیید
                // کند تا ثبت ادامه یابد.
                List<string> tazkiraMatches = DuplicateDetector.FindByTazkira(txtMemberTazkiraNo.Text.Trim(), "TblFamily", 0);
                if (tazkiraMatches.Count > 0)
                {
                    if (!UiTheme.ShowConfirm(this,
                        "این شماره تذکره قبلاً برای موارد زیر ثبت شده است:\n\n" +
                        string.Join("\n", tazkiraMatches) +
                        "\n\nآیا مطمئن هستید که می‌خواهید ادامه دهید؟", "احتمال ثبت تکراری"))
                        return;

                    tazkiraAuditNote = "تذکره " + txtMemberTazkiraNo.Text.Trim() + " => " + string.Join(" | ", tazkiraMatches);
                }

                if (!string.IsNullOrWhiteSpace(pendingSourcePhotoPath))
                {
                    savedPhotoPath = SavePendingPhotoToCaseFolder();
                    if (string.IsNullOrWhiteSpace(savedPhotoPath))
                    {
                        Msg.Show("عکس عضو خانواده ذخیره نشد: " + FileHelper.LastError);
                        return;
                    }

                    copiedNewPhoto = !AreSamePath(savedPhotoPath, pendingSourcePhotoPath);
                }

                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    INSERT INTO TblFamily
                    (
                        CasID, MemberName, MemberFatherName, MemberIdCardType, MemberTazkiraNo, BirthDate,
                        MemberSadat, Gender, MemberRole, Relation, PhysicalStatus, HasDisability, MemberDisabilityDegree,
                        MemberEducation, SchoolName, GradeLevel, UniversityName, StudyYear,
                        Major, StudyField, OfficialStatus, Skill, LeaveReason, Details, DisabilityDetails, MemberPhotoPath,
                        Religion, MaritalStatus, ServiceStatus, StopReason, SuspensionReason,
                        SchoolType, SchoolPrevGrade, UniversityType, UniversityPrevGrade, SeminaryLevel, EducationCoverage,
                        GlobalID, SuspensionDate, SuspendedByUserId, SuspendedByUsername
                    )
                    VALUES
                    (
                        @CasID, @MemberName, @MemberFatherName, @MemberIdCardType, @MemberTazkiraNo, @BirthDate,
                        @MemberSadat, @Gender, @MemberRole, @Relation, @PhysicalStatus, @HasDisability, @MemberDisabilityDegree,
                        @MemberEducation, @SchoolName, @GradeLevel, @UniversityName, @StudyYear,
                        @Major, @StudyField, @OfficialStatus, @Skill, @LeaveReason, @Details, @DisabilityDetails, @MemberPhotoPath,
                        @Religion, @MaritalStatus, @ServiceStatus, @StopReason, @SuspensionReason,
                        @SchoolType, @SchoolPrevGrade, @UniversityType, @UniversityPrevGrade, @SeminaryLevel, @EducationCoverage,
                        lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
                        lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6))),
                        @SuspensionDate, @SuspendedByUserId, @SuspendedByUsername
                    );
                    SELECT last_insert_rowid();", con))
                {
                    AddInt(cmd, "@CasID", CurrentCaseId);
                    AddFamilyParameters(cmd, savedPhotoPath);
                    AddFamilySuspensionStampParameters(cmd);

                    con.Open();
                    currentFamilyId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                AuditLogger.Log("ثبت", "TblFamily", currentFamilyId, "", BuildFamilyAuditText(savedPhotoPath));
                if (tazkiraAuditNote != null)
                    AuditLogger.Log("هشدار تذکره تکراری - تأیید کاربر", "TblFamily", currentFamilyId, "", tazkiraAuditNote);
                AuditLogger.RecordFamilyStatusChange(currentFamilyId, "", cmbServiceStatus.Text,
                    txtSuspensionReason.Text.Trim(), txtStopReason.Text.Trim());

                // صفِ همگام‌سازی — همان الگوی فرم پرونده.
                CaseManagement.Sync.SyncOutboxService.Capture("TblFamily", currentFamilyId,
                    CaseManagement.Sync.OfflineSyncInitializer.OperationCreate);

                // تاریخچهٔ کاملِ رکورد — BuildFamilyAuditText فقط پنج فیلد را ثبت
                // می‌کند؛ این عکسِ فوری همهٔ ستون‌های عضو را نگه می‌دارد.
                CaseManagement.Enterprise.VersionService.Capture("TblFamily", currentFamilyId,
                    CaseManagement.Enterprise.VersionService.OperationInsert);

                Msg.Show("عضو خانواده ذخیره شد");
                LoadFamilyMembers();
                ClearForm();
            }
            catch (Exception ex)
            {
                if (copiedNewPhoto)
                    DeleteStoredPhotoSafely(savedPhotoPath);

                Msg.Show("خطا در ذخیره عضو خانواده: " + ex.Message);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Family.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه ویرایش عضو خانواده ندارد.");
                return;
            }

            if (currentFamilyId <= 0)
            {
                Msg.Show("اول یک عضو را انتخاب کن");
                return;
            }

            if (_familyLockedByOther)
            {
                Msg.Show("این عضو هم‌اکنون توسط کاربر دیگری در حال ویرایش است. لطفاً بعداً تلاش کنید.");
                return;
            }

            if (!ValidateForm())
                return;

            string oldPhotoPath = "";
            string finalPhotoPath = "";
            string newlyCopiedPhotoPath = "";
            string tazkiraAuditNote = null;

            try
            {
                string oldAuditText = GetFamilyAuditText(currentFamilyId);
                string oldStatus = GetFamilyStatusById(currentFamilyId);
                string oldMemberRole = GetFamilyMemberRoleById(currentFamilyId);
                oldPhotoPath = GetStoredMemberPhotoPath(currentFamilyId);
                if (oldPhotoPath == null)
                {
                    Msg.Show("عضو انتخاب‌شده پیدا نشد");
                    LoadFamilyMembers();
                    ClearForm();
                    return;
                }

                // آموزش — هشدارِ تذکرهٔ تکراری، همان الگویِ مسیرِ ثبتِ رکوردِ جدید.
                List<string> tazkiraMatches = DuplicateDetector.FindByTazkira(txtMemberTazkiraNo.Text.Trim(), "TblFamily", currentFamilyId);
                if (tazkiraMatches.Count > 0)
                {
                    if (!UiTheme.ShowConfirm(this,
                        "این شماره تذکره قبلاً برای موارد زیر ثبت شده است:\n\n" +
                        string.Join("\n", tazkiraMatches) +
                        "\n\nآیا مطمئن هستید که می‌خواهید ادامه دهید؟", "احتمال ثبت تکراری"))
                        return;

                    tazkiraAuditNote = "تذکره " + txtMemberTazkiraNo.Text.Trim() + " => " + string.Join(" | ", tazkiraMatches);
                }

                finalPhotoPath = oldPhotoPath;

                if (!string.IsNullOrWhiteSpace(pendingSourcePhotoPath))
                {
                    if (AreSamePath(pendingSourcePhotoPath, oldPhotoPath))
                    {
                        finalPhotoPath = oldPhotoPath;
                    }
                    else
                    {
                        newlyCopiedPhotoPath = SavePendingPhotoToCaseFolder();
                        if (string.IsNullOrWhiteSpace(newlyCopiedPhotoPath))
                        {
                            Msg.Show("عکس جدید عضو خانواده ذخیره نشد: " + FileHelper.LastError);
                            return;
                        }

                        finalPhotoPath = newlyCopiedPhotoPath;
                    }
                }

                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    UPDATE TblFamily SET
                        MemberName = @MemberName,
                        MemberFatherName = @MemberFatherName,
                        MemberIdCardType = @MemberIdCardType,
                        MemberTazkiraNo = @MemberTazkiraNo,
                        BirthDate = @BirthDate,
                        MemberSadat = @MemberSadat,
                        Gender = @Gender,
                        MemberRole = @MemberRole,
                        Relation = @Relation,
                        PhysicalStatus = @PhysicalStatus,
                        HasDisability = @HasDisability,
                        MemberDisabilityDegree = @MemberDisabilityDegree,
                        MemberEducation = @MemberEducation,
                        SchoolName = @SchoolName,
                        GradeLevel = @GradeLevel,
                        UniversityName = @UniversityName,
                        StudyYear = @StudyYear,
                        Major = @Major,
                        StudyField = @StudyField,
                        OfficialStatus = @OfficialStatus,
                        Skill = @Skill,
                        LeaveReason = @LeaveReason,
                        Details = @Details,
                        DisabilityDetails = @DisabilityDetails,
                        MemberPhotoPath = @MemberPhotoPath,
                        Religion = @Religion,
                        MaritalStatus = @MaritalStatus,
                        ServiceStatus = @ServiceStatus,
                        StopReason = @StopReason,
                        SuspensionReason = @SuspensionReason,
                        SchoolType = @SchoolType,
                        SchoolPrevGrade = @SchoolPrevGrade,
                        UniversityType = @UniversityType,
                        UniversityPrevGrade = @UniversityPrevGrade,
                        SeminaryLevel = @SeminaryLevel,
                        EducationCoverage = @EducationCoverage,
                        SuspensionDate = CASE WHEN ServiceStatus = @ServiceStatus THEN SuspensionDate ELSE @SuspensionDate END,
                        SuspendedByUserId = CASE WHEN ServiceStatus = @ServiceStatus THEN SuspendedByUserId ELSE @SuspendedByUserId END,
                        SuspendedByUsername = CASE WHEN ServiceStatus = @ServiceStatus THEN SuspendedByUsername ELSE @SuspendedByUsername END
                    WHERE FamID = @FamID AND CasID = @CasID", con))
                {
                    AddFamilyParameters(cmd, finalPhotoPath);
                    AddFamilySuspensionStampParameters(cmd);
                    AddInt(cmd, "@FamID", currentFamilyId);
                    AddInt(cmd, "@CasID", CurrentCaseId);

                    con.Open();

                    int affectedRows = cmd.ExecuteNonQuery();
                    if (affectedRows == 0)
                        throw new InvalidOperationException("هیچ عضوی برای ویرایش پیدا نشد.");
                }

                if (!string.IsNullOrWhiteSpace(newlyCopiedPhotoPath) && !AreSamePath(oldPhotoPath, newlyCopiedPhotoPath))
                    DeleteStoredPhotoSafely(oldPhotoPath);

                AuditLogger.Log("ویرایش", "TblFamily", currentFamilyId, oldAuditText, BuildFamilyAuditText(finalPhotoPath));
                if (tazkiraAuditNote != null)
                    AuditLogger.Log("هشدار تذکره تکراری - تأیید کاربر", "TblFamily", currentFamilyId, "", tazkiraAuditNote);
                AuditLogger.RecordFamilyStatusChange(currentFamilyId, oldStatus, cmbServiceStatus.Text,
                    txtSuspensionReason.Text.Trim(), txtStopReason.Text.Trim());
                // آموزش — به‌درخواست کاربر (تاریخچهٔ ممیزی نقش عضو، بخش ۱۲):
                // هر تغییرِ MemberRole در جدولِ اختصاصیِ خودش
                // (TblFamilyRoleHistory) ثبت می‌شود — عمداً جدا از
                // TblFamilyStatusHistory (که فقط برای وضعیت خدمات است).
                AuditLogger.RecordFamilyRoleChange(currentFamilyId, oldMemberRole, cmbMemberRole.Text.Trim());

                CaseManagement.Sync.SyncOutboxService.Capture("TblFamily", currentFamilyId,
                    CaseManagement.Sync.OfflineSyncInitializer.OperationUpdate);

                // تاریخچهٔ کاملِ رکورد — توضیح در مسیر ثبت آمده است.
                CaseManagement.Enterprise.VersionService.Capture("TblFamily", currentFamilyId,
                    CaseManagement.Enterprise.VersionService.OperationUpdate);

                Msg.Show("عضو خانواده ویرایش شد");
                LoadFamilyMembers();
                ClearForm();
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(newlyCopiedPhotoPath))
                    DeleteStoredPhotoSafely(newlyCopiedPhotoPath);

                Msg.Show("خطا در ویرایش عضو خانواده: " + ex.Message);
            }
        }

        // ─── تاریخچهٔ تغییراتِ همین عضو ─────────────────────────────────────
        // هم‌الگوی btnHistory_Click در فرم پرونده.
        private void btnHistory_Click(object sender, EventArgs e)
        {
            if (currentFamilyId == 0)
            {
                Msg.Show("اول عضو خانواده را انتخاب کن");
                return;
            }

            using (var frm = new CaseManagement.Enterprise.FrmVersions("TblFamily", currentFamilyId))
                frm.ShowDialog(this);
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Family.Delete"))
            {
                Msg.Show("حذف عضو خانواده فقط برای مدیر سیستم مجاز است.");
                return;
            }

            if (currentFamilyId <= 0)
            {
                Msg.Show("اول یک عضو را انتخاب کن");
                return;
            }

            DialogResult dr = Msg.Show(
                "آیا این عضو حذف شود؟",
                "حذف",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr == DialogResult.No)
                return;

            string photoPathToDelete = "";
            string oldAuditText = GetFamilyAuditText(currentFamilyId);
            int deletedFamilyId = currentFamilyId;

            // ⚠ هویتِ رکورد پیش از باز شدن تراکنشِ حذف برداشته می‌شود (بعد از
            // DELETE دیگر خواندنی نیست، و خواندن از یک اتصالِ دیگر وسطِ تراکنش
            // می‌تواند به قفل بخورد). ثبت در صف فقط پس از حذفِ موفق انجام
            // می‌شود.
            var pendingDelete =
                CaseManagement.Sync.SyncOutboxService.PrepareDelete("TblFamily", currentFamilyId);

            // به همان دلیلِ بالا، عکسِ فوریِ کاملِ رکورد هم پیش از تراکنشِ حذف
            // برداشته می‌شود؛ ثبتِ نسخهٔ «حذف» فقط پس از حذفِ موفق انجام می‌گیرد.
            string deletedSnapshot = CaseManagement.Enterprise.VersionService
                .ReadSnapshotText("TblFamily", currentFamilyId);

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                {
                    con.Open();

                    using (SQLiteTransaction tr = con.BeginTransaction())
                    {
                        // آموزش — COALESCE لازم است: MemberPhotoPath در دیتابیس
                        // NULL-پذیر است، و قبلاً DBNull با «رکورد پیدا نشد» یکی
                        // گرفته می‌شد؛ نتیجه این بود که عضوی که عکس ندارد (مقدار
                        // NULL، مثلاً از ورودی اکسل یا همگام‌سازی) هرگز حذف
                        // نمی‌شد و پیام «عضو انتخاب‌شده پیدا نشد» می‌گرفت.
                        using (SQLiteCommand selectCmd = new SQLiteCommand(@"
                            SELECT COALESCE(MemberPhotoPath, '')
                            FROM TblFamily
                            WHERE FamID = @FamID AND CasID = @CasID", con, tr))
                        {
                            AddInt(selectCmd, "@FamID", currentFamilyId);
                            AddInt(selectCmd, "@CasID", CurrentCaseId);

                            object value = selectCmd.ExecuteScalar();
                            if (value == null || value == DBNull.Value)
                            {
                                tr.Rollback();
                                Msg.Show("عضو انتخاب‌شده پیدا نشد");
                                LoadFamilyMembers();
                                ClearForm();
                                return;
                            }

                            photoPathToDelete = value.ToString();
                        }

                        using (SQLiteCommand deleteCmd = new SQLiteCommand(@"
                            DELETE FROM TblFamily
                            WHERE FamID = @FamID AND CasID = @CasID", con, tr))
                        {
                            AddInt(deleteCmd, "@FamID", currentFamilyId);
                            AddInt(deleteCmd, "@CasID", CurrentCaseId);

                            int affectedRows = deleteCmd.ExecuteNonQuery();
                            if (affectedRows == 0)
                            {
                                tr.Rollback();
                                Msg.Show("عضو خانواده حذف نشد");
                                return;
                            }
                        }

                        tr.Commit();
                    }
                }

                DeleteStoredPhotoSafely(photoPathToDelete);

                AuditLogger.Log("حذف", "TblFamily", deletedFamilyId, oldAuditText, "");

                // حذف واقعاً انجام شد (مسیرهای ناموفق پیش‌تر return کرده‌اند).
                CaseManagement.Sync.SyncOutboxService.CommitDelete(pendingDelete);

                CaseManagement.Enterprise.VersionService.CaptureDeleted(
                    "TblFamily", deletedFamilyId, deletedSnapshot);

                Msg.Show("عضو خانواده حذف شد");
                LoadFamilyMembers();
                ClearForm();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در حذف عضو خانواده: " + ex.Message);
            }
        }

        private void dgvFamily_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (!dgvFamily.Columns.Contains("FamID"))
                return;

            object idValue = dgvFamily.Rows[e.RowIndex].Cells["FamID"].Value;
            if (idValue == null || idValue == DBNull.Value)
                return;

            int famId = Convert.ToInt32(idValue);
            LoadMemberToForm(famId);
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Family.Print"))
            {
                Msg.Show("کاربر اجازه چاپ فهرست اعضای خانواده را ندارد.");
                return;
            }

            DataTable table = dgvFamily.DataSource as DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                Msg.Show("داده‌ای برای چاپ وجود ندارد");
                return;
            }

            DataTable printTable = table.Copy();
            Helpers.PersianDateHelper.ConvertDateColumnsToPersian(printTable, "BirthDate");
            PrintHelper.PrintDataTable(this, "اعضای خانواده — پرونده " + CurrentCaseCode, printTable);
        }

        // ─── کارت خانواده: اطلاعات سرپرست + فهرست اعضا در یک سند واحد ──────────
        // برخلاف کارت شناسایی سرپرست (GuardianCardIntegration که یک قالب HTML
        // ثابت و بسته‌بندی‌شده دارد و طبق قانون پروژه هرگز تغییر نمی‌کند)، این
        // «کارت خانواده» یک سند چاپیِ ساده از همان زیرساختِ PrintHelper موجود
        // است — بدون نیاز به قالب جدید یا دست‌زدن به بسته‌ی GuardianCard.
        private void btnFamilyCard_Click(object sender, EventArgs e)
        {
            if (CurrentCaseId <= 0)
            {
                Msg.Show("اول پرونده را ذخیره یا از لیست انتخاب کن");
                return;
            }

            DataTable table = dgvFamily.DataSource as DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                Msg.Show("این خانواده هیچ عضوی ندارد");
                return;
            }

            var headFields = new List<KeyValuePair<string, string>>();

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT Code, FormNo, HeadFullName, HeadFatherName, Province, District, Phone, ServiceStatus FROM TblCase WHERE CasID = @CasID", con))
                {
                    AddInt(cmd, "@CasID", CurrentCaseId);
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            headFields.Add(new KeyValuePair<string, string>("کد اختصاصی", dr["Code"] == DBNull.Value ? "" : dr["Code"].ToString()));
                            headFields.Add(new KeyValuePair<string, string>("شماره فرم", dr["FormNo"] == DBNull.Value ? "" : dr["FormNo"].ToString()));
                            headFields.Add(new KeyValuePair<string, string>("نام سرپرست", dr["HeadFullName"] == DBNull.Value ? "" : dr["HeadFullName"].ToString()));
                            headFields.Add(new KeyValuePair<string, string>("نام پدر سرپرست", dr["HeadFatherName"] == DBNull.Value ? "" : dr["HeadFatherName"].ToString()));
                            headFields.Add(new KeyValuePair<string, string>("ولایت", dr["Province"] == DBNull.Value ? "" : dr["Province"].ToString()));
                            headFields.Add(new KeyValuePair<string, string>("ولسوالی", dr["District"] == DBNull.Value ? "" : dr["District"].ToString()));
                            headFields.Add(new KeyValuePair<string, string>("شماره تماس", dr["Phone"] == DBNull.Value ? "" : dr["Phone"].ToString()));
                            headFields.Add(new KeyValuePair<string, string>("وضعیت خدمات", dr["ServiceStatus"] == DBNull.Value ? "" : dr["ServiceStatus"].ToString()));
                            headFields.Add(new KeyValuePair<string, string>("تعداد اعضای خانواده", table.Rows.Count.ToString()));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در خواندن اطلاعات سرپرست: " + ex.Message);
                return;
            }

            DataTable printTable = new DataTable();
            printTable.Columns.Add("MemberName");
            printTable.Columns.Add("MemberFatherName");
            printTable.Columns.Add("Gender");
            printTable.Columns.Add("BirthDate");
            foreach (DataRow row in table.Rows)
            {
                printTable.Rows.Add(
                    table.Columns.Contains("MemberName") ? row["MemberName"] : DBNull.Value,
                    table.Columns.Contains("MemberFatherName") ? row["MemberFatherName"] : DBNull.Value,
                    table.Columns.Contains("Gender") ? row["Gender"] : DBNull.Value,
                    table.Columns.Contains("BirthDate") ? row["BirthDate"] : DBNull.Value);
            }
            Helpers.PersianDateHelper.ConvertDateColumnsToPersian(printTable, "BirthDate");

            PrintHelper.PrintFamilyCard(this, "کارت خانواده — پرونده " + CurrentCaseCode, headFields, printTable);
        }

        private void dgvFamily_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dgvFamily_CellClick(sender, e);
        }

        private void LoadMemberToForm(int famId)
        {
            ReleaseFamilyLock();

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    SELECT
                        FamID, MemberName, MemberFatherName, MemberIdCardType, MemberTazkiraNo, BirthDate,
                        MemberSadat, Gender, MemberRole, Relation, PhysicalStatus, HasDisability, MemberDisabilityDegree,
                        MemberEducation, SchoolName, GradeLevel, UniversityName, StudyYear,
                        Major, StudyField, OfficialStatus, Skill, LeaveReason, Details, DisabilityDetails, MemberPhotoPath,
                        Religion, MaritalStatus, ServiceStatus, StopReason, SuspensionReason,
                        SchoolType, SchoolPrevGrade, UniversityType, UniversityPrevGrade, SeminaryLevel, EducationCoverage
                    FROM TblFamily
                    WHERE FamID = @FamID AND CasID = @CasID", con))
                {
                    AddInt(cmd, "@FamID", famId);
                    AddInt(cmd, "@CasID", CurrentCaseId);

                    con.Open();

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read())
                            return;

                        currentFamilyId = Convert.ToInt32(dr["FamID"]);
                        storedMemberPhotoPath = DbString(dr["MemberPhotoPath"]);
                        pendingSourcePhotoPath = "";

                        txtMemberName.Text = DbString(dr["MemberName"]);
                        txtMemberFatherName.Text = DbString(dr["MemberFatherName"]);
                        // ترتیب مهم است: اول نوع تذکره، بعد شماره — چون
                        // IdCardHelper.Attach با تغییر نوع، شماره را دوباره
                        // قالب‌بندی می‌کند.
                        cmbMemberIdCardType.Text = DbString(dr["MemberIdCardType"]);
                        txtMemberTazkiraNo.Text = DbString(dr["MemberTazkiraNo"]);
                        txtMemberSadat.Text = DbString(dr["MemberSadat"]);
                        txtGender.Text = DbString(dr["Gender"]);
                        cmbMemberRole.Text = DbString(dr["MemberRole"]);
                        cmbRelation.Text = DbString(dr["Relation"]);
                        txtPhysicalStatus.Text = DbString(dr["PhysicalStatus"]);
                        txtHasDisability.Text = DbString(dr["HasDisability"]);
                        txtMemberDisabilityDegree.Text = DbString(dr["MemberDisabilityDegree"]);
                        txtMemberEducation.Text = DbString(dr["MemberEducation"]);
                        txtSchoolName.Text = DbString(dr["SchoolName"]);
                        txtGradeLevel.Text = DbString(dr["GradeLevel"]);
                        txtUniversityName.Text = DbString(dr["UniversityName"]);
                        txtStudyYear.Text = DbString(dr["StudyYear"]);
                        txtMajor.Text = DbString(dr["Major"]);
                        txtStudyField.Text = DbString(dr["StudyField"]);
                        txtOfficialStatus.Text = DbString(dr["OfficialStatus"]);
                        txtSkill.Text = DbString(dr["Skill"]);
                        txtLeaveReason.Text = DbString(dr["LeaveReason"]);
                        txtDetails.Text = DbString(dr["Details"]);
                        txtDisabilityDetails.Text = DbString(dr["DisabilityDetails"]);
                        txtMemberPhotoPath.Text = storedMemberPhotoPath;
                        cmbReligion.Text = DbString(dr["Religion"]);
                        cmbMaritalStatus.Text = DbString(dr["MaritalStatus"]);
                        cmbServiceStatus.Text = DbString(dr["ServiceStatus"]);
                        txtStopReason.Text = DbString(dr["StopReason"]);
                        int suspReasonIdx = txtSuspensionReason.FindStringExact(DbString(dr["SuspensionReason"]));
                        txtSuspensionReason.SelectedIndex = suspReasonIdx;

                        // فیلدهای تحصیلی جدید
                        cmbSchoolType.Text = DbString(dr["SchoolType"]);
                        txtSchoolPrevGrade.Text = DbString(dr["SchoolPrevGrade"]);
                        cmbUniversityType.Text = DbString(dr["UniversityType"]);
                        txtUniversityPrevGrade.Text = DbString(dr["UniversityPrevGrade"]);
                        cmbSeminaryLevel.Text = DbString(dr["SeminaryLevel"]);
                        cmbEducationCoverage.Text = DbString(dr["EducationCoverage"]);

                        UpdateStopReasonVisibility();
                        // false = فقط فعال/غیرفعال؛ مقادیر خوانده‌شده از دیتابیس
                        // پاک نمی‌شوند (توضیح کامل بالای UpdateEducationFieldsState).
                        UpdateEducationFieldsState(false);
                        // false = فقط فعال/غیرفعال؛ اطلاعات معلولیتِ ذخیره‌شده
                        // پاک نمی‌شود (توضیح بالای UpdatePhysicalFieldsState).
                        UpdatePhysicalFieldsState(false);

                        if (dr["BirthDate"] != DBNull.Value)
                        {
                            // آموزش — پارس با InvariantCulture (نه Convert.ToDateTime)
                            // تا تاریخ میلادی ذخیره‌شده با کالچر شمسی ترد اشتباه
                            // تفسیر نشود. جزئیات در PersianDateHelper.ParseStoredDate.
                            dtpBirthDate.Value = CaseManagement.Helpers.PersianDateHelper.ParseStoredDate(dr["BirthDate"], DateTime.Today);
                            dtpBirthDate.Checked = true;
                        }
                        else
                        {
                            dtpBirthDate.Value = DateTime.Today;
                            dtpBirthDate.Checked = false;
                        }

                        if (!string.IsNullOrWhiteSpace(storedMemberPhotoPath))
                            LoadImageToPictureBox(storedMemberPhotoPath, picMemberPhoto, false);
                        else
                            ClearPicture(picMemberPhoto);
                    }
                }

                TryLockFamilyMember(currentFamilyId);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در انتخاب عضو خانواده: " + ex.Message);
            }
        }

        private string SavePendingPhotoToCaseFolder()
        {
            if (string.IsNullOrWhiteSpace(pendingSourcePhotoPath))
                return "";

            if (string.IsNullOrWhiteSpace(CurrentCaseCode))
            {
                Msg.Show("کد اختصاصی پرونده مشخص نیست");
                return "";
            }

            if (!IsValidImageFile(pendingSourcePhotoPath, true))
                return "";

            string cleanCode = FileHelper.CleanName(CurrentCaseCode.Trim());

            string savedPath = FileHelper.SaveFileToCaseFolder(
                pendingSourcePhotoPath,
                CurrentCaseCode,
                FileHelper.SectionMemberPhotos,
                cleanCode,
                "");

            // آموزش — پیام خطا اینجا نشان داده نمی‌شود: هر دو فراخوان (ثبت و
            // ویرایش) خودشان دقیقاً همین پیام را می‌دهند، پس قبلاً کاربر دو
            // پنجره‌ی خطای یکسان پشت‌سرهم می‌دید.
            if (string.IsNullOrWhiteSpace(savedPath))
                return "";

            if (savedPath.Length > MemberPhotoPathLength)
            {
                DeleteStoredPhotoSafely(savedPath);
                Msg.Show("مسیر عکس بیش از حد طولانی است");
                return "";
            }

            return savedPath;
        }

        private string GetStoredMemberPhotoPath(int famId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
                SELECT MemberPhotoPath
                FROM TblFamily
                WHERE FamID = @FamID AND CasID = @CasID", con))
            {
                AddInt(cmd, "@FamID", famId);
                AddInt(cmd, "@CasID", CurrentCaseId);

                con.Open();

                object result = cmd.ExecuteScalar();
                if (result == null)
                    return null;

                if (result == DBNull.Value)
                    return "";

                return result.ToString();
            }
        }

        private string GetFamilyStatusById(int famId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT ServiceStatus FROM TblFamily WHERE FamID = @FamID AND CasID = @CasID", con))
            {
                AddInt(cmd, "@FamID", famId);
                AddInt(cmd, "@CasID", CurrentCaseId);
                con.Open();

                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? "" : result.ToString();
            }
        }

        private string GetFamilyMemberRoleById(int famId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT MemberRole FROM TblFamily WHERE FamID = @FamID AND CasID = @CasID", con))
            {
                AddInt(cmd, "@FamID", famId);
                AddInt(cmd, "@CasID", CurrentCaseId);
                con.Open();

                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? "" : result.ToString();
            }
        }

        private string GetFamilyAuditText(int famId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT MemberName, MemberFatherName, Gender, BirthDate, MemberPhotoPath
FROM TblFamily
WHERE FamID = @FamID AND CasID = @CasID", con))
            {
                AddInt(cmd, "@FamID", famId);
                AddInt(cmd, "@CasID", CurrentCaseId);
                con.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read())
                        return "";

                    return
                        "MemberName=" + DbString(dr["MemberName"]) +
                        "; MemberFatherName=" + DbString(dr["MemberFatherName"]) +
                        "; Gender=" + DbString(dr["Gender"]) +
                        "; BirthDate=" + DbString(dr["BirthDate"]) +
                        "; MemberPhotoPath=" + DbString(dr["MemberPhotoPath"]);
                }
            }
        }

        private string BuildFamilyAuditText(string photoPath)
        {
            return
                "MemberName=" + txtMemberName.Text.Trim() +
                "; MemberFatherName=" + txtMemberFatherName.Text.Trim() +
                "; Gender=" + txtGender.Text.Trim() +
                "; BirthDate=" + (dtpBirthDate.Checked ? CaseManagement.Helpers.PersianDateHelper.ToPersianDateString(dtpBirthDate.Value) : "") +
                "; MemberPhotoPath=" + (photoPath ?? "");
        }

        private void DeleteStoredPhotoSafely(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!IsStoredPhotoPathAllowed(path))
                return;

            FileHelper.DeleteFileIfExists(path);
        }

        private bool IsStoredPhotoPathAllowed(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (string.IsNullOrWhiteSpace(CurrentCaseCode))
                return false;

            string memberPhotosFolder = FileHelper.GetSectionFolder(CurrentCaseCode, MemberPhotosSectionName);
            if (string.IsNullOrWhiteSpace(memberPhotosFolder))
                return false;

            return IsPathInsideFolder(path, memberPhotosFolder);
        }

        private void AddFamilyParameters(SQLiteCommand cmd, string memberPhotoPath)
        {
            AddNVarChar(cmd, "@MemberName", txtMemberName.Text.Trim(), MemberNameLength);
            AddNVarChar(cmd, "@MemberFatherName", txtMemberFatherName.Text.Trim(), MemberFatherNameLength);
            AddNVarChar(cmd, "@MemberIdCardType", cmbMemberIdCardType.Text.Trim(), 50);
            AddNVarChar(cmd, "@MemberTazkiraNo", txtMemberTazkiraNo.Text.Trim(), MemberTazkiraNoLength);
            AddNullableDate(cmd, "@BirthDate", dtpBirthDate.Checked ? (DateTime?)dtpBirthDate.Value.Date : null);
            AddNVarChar(cmd, "@MemberSadat", txtMemberSadat.Text.Trim(), MemberSadatLength);
            AddNVarChar(cmd, "@Gender", txtGender.Text.Trim(), GenderLength);
            AddNVarChar(cmd, "@MemberRole", cmbMemberRole.Text.Trim(), MemberRoleLength);
            AddNVarChar(cmd, "@Relation", cmbRelation.Text.Trim(), RelationLength);
            AddNVarChar(cmd, "@PhysicalStatus", txtPhysicalStatus.Text.Trim(), PhysicalStatusLength);
            AddNVarChar(cmd, "@HasDisability", txtHasDisability.Text.Trim(), HasDisabilityLength);
            AddNVarChar(cmd, "@MemberDisabilityDegree", txtMemberDisabilityDegree.Text.Trim(), MemberDisabilityDegreeLength);
            AddNVarChar(cmd, "@MemberEducation", txtMemberEducation.Text.Trim(), MemberEducationLength);
            AddNVarChar(cmd, "@SchoolName", txtSchoolName.Text.Trim(), SchoolNameLength);
            AddNVarChar(cmd, "@GradeLevel", txtGradeLevel.Text.Trim(), GradeLevelLength);
            AddNVarChar(cmd, "@UniversityName", txtUniversityName.Text.Trim(), UniversityNameLength);
            AddNVarChar(cmd, "@StudyYear", txtStudyYear.Text.Trim(), StudyYearLength);
            AddNVarChar(cmd, "@Major", txtMajor.Text.Trim(), MajorLength);
            AddNVarChar(cmd, "@StudyField", txtStudyField.Text.Trim(), StudyFieldLength);
            AddNVarChar(cmd, "@OfficialStatus", txtOfficialStatus.Text.Trim(), OfficialStatusLength);
            AddNVarChar(cmd, "@Skill", txtSkill.Text.Trim(), SkillLength);
            AddNVarChar(cmd, "@LeaveReason", txtLeaveReason.Text.Trim(), LeaveReasonLength);
            AddNVarCharMax(cmd, "@Details", txtDetails.Text.Trim());
            AddNVarCharMax(cmd, "@DisabilityDetails", txtDisabilityDetails.Text.Trim());
            AddNVarChar(cmd, "@MemberPhotoPath", memberPhotoPath ?? "", MemberPhotoPathLength);
            AddNVarChar(cmd, "@Religion", cmbReligion.Text.Trim(), 50);
            AddNVarChar(cmd, "@MaritalStatus", cmbMaritalStatus.Text.Trim(), 50);
            bool isSuspended = IsSuspendedStatus(cmbServiceStatus.Text);
            AddNVarChar(cmd, "@ServiceStatus", string.IsNullOrEmpty(cmbServiceStatus.Text) ? "فعال" : cmbServiceStatus.Text, 50);
            AddNVarChar(cmd, "@StopReason", isSuspended ? txtStopReason.Text.Trim() : "", 500);
            AddNVarChar(cmd, "@SuspensionReason", isSuspended ? txtSuspensionReason.Text.Trim() : "", 100);

            // ─── فیلدهای تحصیلی جدید (مطابق قالب Word) ───────────────────────
            AddNVarChar(cmd, "@SchoolType", cmbSchoolType.Text.Trim(), 50);
            AddNVarChar(cmd, "@SchoolPrevGrade", txtSchoolPrevGrade.Text.Trim(), 50);
            AddNVarChar(cmd, "@UniversityType", cmbUniversityType.Text.Trim(), 50);
            AddNVarChar(cmd, "@UniversityPrevGrade", txtUniversityPrevGrade.Text.Trim(), 50);
            AddNVarChar(cmd, "@SeminaryLevel", cmbSeminaryLevel.Text.Trim(), 50);
            AddNVarChar(cmd, "@EducationCoverage", cmbEducationCoverage.Text.Trim(), 50);
        }

        // مقادیرِ «مُهرِ تعلیق» عضو خانواده — همان الگوی FrmCase.AddSuspensionStampParameters.
        private void AddFamilySuspensionStampParameters(SQLiteCommand cmd)
        {
            bool isSuspended = IsSuspendedStatus(cmbServiceStatus.Text);

            if (isSuspended)
            {
                cmd.Parameters.AddWithValue("@SuspensionDate", DateTime.Now.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@SuspendedByUserId",
                    SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : DBNull.Value);
                AddNVarChar(cmd, "@SuspendedByUsername", SecurityContext.Username ?? "", 100);
            }
            else
            {
                cmd.Parameters.AddWithValue("@SuspensionDate", DBNull.Value);
                cmd.Parameters.AddWithValue("@SuspendedByUserId", DBNull.Value);
                cmd.Parameters.AddWithValue("@SuspendedByUsername", DBNull.Value);
            }
        }

        private static void AddNullableDate(SQLiteCommand cmd, string name, DateTime? value)
        {
            var paramValue = value.HasValue ? (object)value.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : DBNull.Value;
            cmd.Parameters.AddWithValue(name, paramValue);
        }
    }
}

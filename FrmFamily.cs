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

        private const long MaxMemberPhotoBytes = 5L * 1024 * 1024;

        private static readonly HashSet<string> AllowedPhotoExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png"
            };

        private readonly DatabaseHelper db = new DatabaseHelper();

        public int CurrentCaseId { get; set; } = 0;
        public string CurrentCaseCode { get; set; } = "";

        private int currentFamilyId = 0;
        private string storedMemberPhotoPath = "";
        private string pendingSourcePhotoPath = "";

        public FrmFamily()
        {
            InitializeComponent();
            ApplyCustomTheme();
        }

        // ─── اعمال ظاهر یکسان روی فرمی که با طراح (Designer) ساخته شده ──────
        private void ApplyCustomTheme()
        {
            UiTheme.ApplySweep(this);

            UiTheme.SetButtonIcon(btnNew, "+");
            UiTheme.SetButtonIcon(btnSave, "✔");
            UiTheme.SetButtonIcon(btnEdit, "✎");
            UiTheme.SetButtonIcon(btnDelete, "✕");
            UiTheme.SetButtonIcon(btnBrowseMemberPhoto, "▤");

            btnDelete.BackColor = UiTheme.Danger;
            btnDelete.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Danger, 0.18f);
            btnDelete.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Danger, 0.08f);

            btnSave.BackColor = UiTheme.Success;
            btnSave.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Success, 0.18f);
            btnSave.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Success, 0.08f);
        }

        private void FrmFamily_Load(object sender, EventArgs e)
        {
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

        // با انتخاب «سالم» به‌عنوان وضعیت جسمی، فیلدهای نوع/درجه معلولیت و
        // توضیحات تفصیلی معلولیت غیرفعال و پاک می‌شوند (چون فرد معلولیتی ندارد).
        private void UpdatePhysicalFieldsState()
        {
            bool isHealthy = txtPhysicalStatus.Text == "سالم";

            txtHasDisability.Enabled = !isHealthy;
            txtMemberDisabilityDegree.Enabled = !isHealthy;
            txtDisabilityDetails.Enabled = !isHealthy;

            if (isHealthy)
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
                            lblHeadInfo.Text =
                                "کد سرپرست: " + CurrentCaseCode +
                                "     نام سرپرست: " + DbString(dr["HeadFullName"]) +
                                "     نام پدر سرپرست: " + DbString(dr["HeadFatherName"]);
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
        private void UpdateEducationFieldsState()
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

            if (!isSchool) { txtSchoolName.Text = ""; txtGradeLevel.SelectedIndex = -1; cmbSchoolType.SelectedIndex = -1; txtSchoolPrevGrade.Text = ""; }
            if (!isUniversity) { txtUniversityName.Text = ""; txtStudyYear.SelectedIndex = -1; txtMajor.Text = ""; cmbUniversityType.SelectedIndex = -1; txtUniversityPrevGrade.Text = ""; }
            if (!isSeminary) { txtStudyField.Text = ""; cmbSeminaryLevel.SelectedIndex = -1; }
            if (!isDropout) txtLeaveReason.Text = "";
            if (!isSchool && !isUniversity) cmbEducationCoverage.SelectedIndex = -1;

            // آموزش — فیلد «وضعیت رسمی تحصیلی» به درخواست کاربر حذف شد؛
            // txtOfficialStatus دیگر در UI نیست و مقدارش همیشه خالی می‌ماند.
            txtOfficialStatus.Text = "";
        }

        // "دلیل قطع موقت" فقط وقتی وضعیت "قطع موقت" است نمایش و اجباری می‌شود
        private void cmbServiceStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateStopReasonVisibility();
        }

        private void UpdateStopReasonVisibility()
        {
            bool isTempStopped = cmbServiceStatus.Text == "قطع موقت";
            lblStopReason.Visible = isTempStopped;
            txtStopReason.Visible = isTempStopped;

            if (!isTempStopped)
                txtStopReason.Text = "";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ClearPicture(picMemberPhoto);
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

        private void ClearForm()
        {
            currentFamilyId = 0;
            storedMemberPhotoPath = "";
            pendingSourcePhotoPath = "";

            txtMemberName.Text = "";
            txtMemberFatherName.Text = "";
            txtMemberTazkiraNo.Text = "";
            txtMemberSadat.SelectedIndex = -1;
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

            if (cmbServiceStatus.Text == "قطع موقت" && string.IsNullOrWhiteSpace(txtStopReason.Text))
            {
                Msg.Show("دلیل قطع موقت را وارد کنید");
                txtStopReason.Focus();
                return false;
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
                    SELECT FamID, MemberName, MemberFatherName, Gender, BirthDate, MemberEducation, Skill, MemberPhotoPath,
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
                row.Cells[MemberPhotoThumbColumnName].Value = LoadThumbnail(path, 38);
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
            if (!SecurityContext.CanEdit())
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

            try
            {
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
                        CasID, MemberName, MemberFatherName, MemberTazkiraNo, BirthDate,
                        MemberSadat, Gender, PhysicalStatus, HasDisability, MemberDisabilityDegree,
                        MemberEducation, SchoolName, GradeLevel, UniversityName, StudyYear,
                        Major, StudyField, OfficialStatus, Skill, LeaveReason, Details, DisabilityDetails, MemberPhotoPath,
                        Religion, MaritalStatus, ServiceStatus, StopReason,
                        SchoolType, SchoolPrevGrade, UniversityType, UniversityPrevGrade, SeminaryLevel, EducationCoverage,
                        GlobalID
                    )
                    VALUES
                    (
                        @CasID, @MemberName, @MemberFatherName, @MemberTazkiraNo, @BirthDate,
                        @MemberSadat, @Gender, @PhysicalStatus, @HasDisability, @MemberDisabilityDegree,
                        @MemberEducation, @SchoolName, @GradeLevel, @UniversityName, @StudyYear,
                        @Major, @StudyField, @OfficialStatus, @Skill, @LeaveReason, @Details, @DisabilityDetails, @MemberPhotoPath,
                        @Religion, @MaritalStatus, @ServiceStatus, @StopReason,
                        @SchoolType, @SchoolPrevGrade, @UniversityType, @UniversityPrevGrade, @SeminaryLevel, @EducationCoverage,
                        lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
                        lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6)))
                    );
                    SELECT last_insert_rowid();", con))
                {
                    AddInt(cmd, "@CasID", CurrentCaseId);
                    AddFamilyParameters(cmd, savedPhotoPath);

                    con.Open();
                    currentFamilyId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                AuditLogger.Log("ثبت", "TblFamily", currentFamilyId, "", BuildFamilyAuditText(savedPhotoPath));

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
            if (!SecurityContext.CanEdit())
            {
                Msg.Show("کاربر فقط مشاهده اجازه ویرایش عضو خانواده ندارد.");
                return;
            }

            if (currentFamilyId <= 0)
            {
                Msg.Show("اول یک عضو را انتخاب کن");
                return;
            }

            if (!ValidateForm())
                return;

            string oldPhotoPath = "";
            string finalPhotoPath = "";
            string newlyCopiedPhotoPath = "";

            try
            {
                string oldAuditText = GetFamilyAuditText(currentFamilyId);
                oldPhotoPath = GetStoredMemberPhotoPath(currentFamilyId);
                if (oldPhotoPath == null)
                {
                    Msg.Show("عضو انتخاب‌شده پیدا نشد");
                    LoadFamilyMembers();
                    ClearForm();
                    return;
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
                        MemberTazkiraNo = @MemberTazkiraNo,
                        BirthDate = @BirthDate,
                        MemberSadat = @MemberSadat,
                        Gender = @Gender,
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
                        SchoolType = @SchoolType,
                        SchoolPrevGrade = @SchoolPrevGrade,
                        UniversityType = @UniversityType,
                        UniversityPrevGrade = @UniversityPrevGrade,
                        SeminaryLevel = @SeminaryLevel,
                        EducationCoverage = @EducationCoverage
                    WHERE FamID = @FamID AND CasID = @CasID", con))
                {
                    AddFamilyParameters(cmd, finalPhotoPath);
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

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.CanDelete())
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

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                {
                    con.Open();

                    using (SQLiteTransaction tr = con.BeginTransaction())
                    {
                        using (SQLiteCommand selectCmd = new SQLiteCommand(@"
                            SELECT MemberPhotoPath
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

        private void dgvFamily_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dgvFamily_CellClick(sender, e);
        }

        private void LoadMemberToForm(int famId)
        {
            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    SELECT
                        FamID, MemberName, MemberFatherName, MemberTazkiraNo, BirthDate,
                        MemberSadat, Gender, PhysicalStatus, HasDisability, MemberDisabilityDegree,
                        MemberEducation, SchoolName, GradeLevel, UniversityName, StudyYear,
                        Major, StudyField, OfficialStatus, Skill, LeaveReason, Details, DisabilityDetails, MemberPhotoPath,
                        Religion, MaritalStatus, ServiceStatus, StopReason,
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
                        txtMemberTazkiraNo.Text = DbString(dr["MemberTazkiraNo"]);
                        txtMemberSadat.Text = DbString(dr["MemberSadat"]);
                        txtGender.Text = DbString(dr["Gender"]);
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

                        // فیلدهای تحصیلی جدید
                        cmbSchoolType.Text = DbString(dr["SchoolType"]);
                        txtSchoolPrevGrade.Text = DbString(dr["SchoolPrevGrade"]);
                        cmbUniversityType.Text = DbString(dr["UniversityType"]);
                        txtUniversityPrevGrade.Text = DbString(dr["UniversityPrevGrade"]);
                        cmbSeminaryLevel.Text = DbString(dr["SeminaryLevel"]);
                        cmbEducationCoverage.Text = DbString(dr["EducationCoverage"]);

                        UpdateStopReasonVisibility();
                        UpdateEducationFieldsState();
                        UpdatePhysicalFieldsState();

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

            if (string.IsNullOrWhiteSpace(savedPath))
            {
                Msg.Show("عکس عضو خانواده ذخیره نشد: " + FileHelper.LastError);
                return "";
            }

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
            AddNVarChar(cmd, "@MemberTazkiraNo", txtMemberTazkiraNo.Text.Trim(), MemberTazkiraNoLength);
            AddNullableDate(cmd, "@BirthDate", dtpBirthDate.Checked ? (DateTime?)dtpBirthDate.Value.Date : null);
            AddNVarChar(cmd, "@MemberSadat", txtMemberSadat.Text.Trim(), MemberSadatLength);
            AddNVarChar(cmd, "@Gender", txtGender.Text.Trim(), GenderLength);
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
            AddNVarChar(cmd, "@ServiceStatus", string.IsNullOrEmpty(cmbServiceStatus.Text) ? "فعال" : cmbServiceStatus.Text, 50);
            AddNVarChar(cmd, "@StopReason", cmbServiceStatus.Text == "قطع موقت" ? txtStopReason.Text.Trim() : "", 500);

            // ─── فیلدهای تحصیلی جدید (مطابق قالب Word) ───────────────────────
            AddNVarChar(cmd, "@SchoolType", cmbSchoolType.Text.Trim(), 50);
            AddNVarChar(cmd, "@SchoolPrevGrade", txtSchoolPrevGrade.Text.Trim(), 50);
            AddNVarChar(cmd, "@UniversityType", cmbUniversityType.Text.Trim(), 50);
            AddNVarChar(cmd, "@UniversityPrevGrade", txtUniversityPrevGrade.Text.Trim(), 50);
            AddNVarChar(cmd, "@SeminaryLevel", cmbSeminaryLevel.Text.Trim(), 50);
            AddNVarChar(cmd, "@EducationCoverage", cmbEducationCoverage.Text.Trim(), 50);
        }

        private static void AddNullableDate(SQLiteCommand cmd, string name, DateTime? value)
        {
            var paramValue = value.HasValue ? (object)value.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : DBNull.Value;
            cmd.Parameters.AddWithValue(name, paramValue);
        }
    }
}

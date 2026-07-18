using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using DrawingImage = System.Drawing.Image;

namespace CaseManagement
{
    public partial class FrmCase : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        private int currentCaseId = 0;

        private string selectedHeadPhotoSource = "";
        private string selectedFamilyPhotoSource = "";

        private string savedHeadPhotoPath = "";
        private string savedFamilyPhotoPath = "";

        private readonly string[] serviceStatuses =
        {
            "فعال",
            "در انتظار تأیید",
            "قطع موقت",
            "قطع"
        };

        private const long MinFamilyPhotoFileSizeBytes = 50L * 1024;
        private const long MaxFamilyPhotoFileSizeBytes = 1L * 1024 * 1024;

        private int _pendingOpenCaseId = 0;

        public FrmCase()
        {
            InitializeComponent();
            ApplyCustomTheme();
        }

        // باز کردن مستقیم یک پرونده‌ی مشخص برای ویرایش (مثلاً از تب «کیفیت داده»
        // داشبورد با راست‌کلیک). پرونده پس از بارگذاری فرم به‌طور خودکار لود می‌شود.
        public FrmCase(int openCaseId) : this()
        {
            _pendingOpenCaseId = openCaseId;
        }

        // ─── اعمال ظاهر یکسان روی فرمی که با طراح (Designer) ساخته شده ──────
        // آموزش: چون این فرم ده‌ها کنترل با مکان/اندازه ثابت دارد، به‌جای
        // جابه‌جایی تک‌تک آن‌ها (ریسک شکستن Layout)، فقط رنگ/فونت/آیکون
        // یکسان روی همان چیدمان موجود اعمال می‌شود.
        private void ApplyCustomTheme()
        {
            UiTheme.ApplySweep(this);

            UiTheme.SetButtonIcon(btnSave, "✔");
            UiTheme.SetButtonIcon(btnNew, "+");
            UiTheme.SetButtonIcon(btnEdit, "✎");
            UiTheme.SetButtonIcon(btnDelete, "✕");
            UiTheme.SetButtonIcon(btnSearch, "⌕");
            UiTheme.SetButtonIcon(btnBrowsePhoto, "▤");
            UiTheme.SetButtonIcon(btnBrowseFamilyPhoto, "♥");
            UiTheme.SetButtonIcon(btnFamily, "♥");
            UiTheme.SetButtonIcon(btnDocs, "▤");
            UiTheme.SetButtonIcon(btnChooseStorageFolder, "⚙");
            UiTheme.SetButtonIcon(btnExportWord, "➤");
            UiTheme.SetButtonIcon(btnExportPdf, "➤");
            UiTheme.SetButtonIcon(btnExportExcel, "⇑");
            UiTheme.SetButtonIcon(btnBatchExport, "⇑");
            UiTheme.SetButtonIcon(btnPrint, "🖨");

            btnDelete.BackColor = UiTheme.Danger;
            btnDelete.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Danger, 0.18f);
            btnDelete.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Danger, 0.08f);

            btnSave.BackColor = UiTheme.Success;
            btnSave.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Success, 0.18f);
            btnSave.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Success, 0.08f);

            // آموزش — دسته‌بندی بصری دکمه‌ها (به درخواست کاربر برای نظم و
            // حرفه‌ای بودن): دکمه‌های «عملیات» (جدید/ذخیره/ویرایش/حذف/جستجو)
            // پُررنگ می‌مانند، اما دکمه‌های «خروجی‌ها» به سبک ثانویه (روشن با
            // کادر) درمی‌آیند تا در یک نگاه دو گروه مجزا دیده شوند.
            Button[] exportButtons = { btnPrint, btnExportWord, btnExportPdf, btnExportExcel, btnBatchExport };
            foreach (Button b in exportButtons)
            {
                b.BackColor = Color.White;
                b.ForeColor = UiTheme.Primary;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = UiTheme.Primary;
                b.FlatAppearance.MouseOverBackColor = UiTheme.HoverTint;
                b.FlatAppearance.MouseDownBackColor = ControlPaint.Light(UiTheme.Primary, 0.7f);
            }

            AddGuardianCardButton();
        }

        // آموزش — دکمه «کارت شناسایی سرپرست» به‌صورت پویا کنار دکمه‌های خروجی
        // موجود اضافه می‌شود (نه در Designer) تا چیدمان FlowLayoutPanel دست‌نخورده
        // بماند؛ چون آن پنل WrapContents=true دارد، افزودن یک دکمه دیگر کاملاً امن است.
        private void AddGuardianCardButton()
        {
            Button btnGuardianCard = UiTheme.CreateSecondaryButton("کارت شناسایی", "🪪");
            btnGuardianCard.Size = new Size(128, 32);
            btnGuardianCard.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGuardianCard.Margin = new Padding(3, 3, 3, 3);
            btnGuardianCard.TabStop = false;
            btnGuardianCard.Click += delegate
            {
                if (currentCaseId == 0)
                {
                    Msg.Show("اول پرونده را ذخیره یا جستجو کن");
                    return;
                }
                using (var frm = new GuardianCardIntegration.FrmGuardianCardPreview(currentCaseId))
                    frm.ShowDialog(this);
            };

            Button btnGuardianCardBatch = UiTheme.CreateSecondaryButton("چاپ جمعی کارت‌ها", "🪪");
            btnGuardianCardBatch.Size = new Size(150, 32);
            btnGuardianCardBatch.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGuardianCardBatch.Margin = new Padding(3, 3, 3, 3);
            btnGuardianCardBatch.TabStop = false;
            btnGuardianCardBatch.Click += delegate
            {
                using (var frm = new GuardianCardIntegration.FrmGuardianCardBatchPrint())
                    frm.ShowDialog(this);
            };

            Control parent = btnExportExcel.Parent;
            if (parent != null)
            {
                parent.Controls.Add(btnGuardianCard);
                parent.Controls.SetChildIndex(btnGuardianCard, parent.Controls.IndexOf(btnExportExcel) + 1);
                parent.Controls.Add(btnGuardianCardBatch);
                parent.Controls.SetChildIndex(btnGuardianCardBatch, parent.Controls.IndexOf(btnGuardianCard) + 1);
            }
        }

        // فرم را داخل ناحیه‌ی کاری صفحه جا می‌دهد (مستقل از DPI/اندازه‌ی صفحه).
        private void FitToScreen()
        {
            try
            {
                System.Drawing.Rectangle wa = Screen.FromControl(this).WorkingArea;
                int w = Math.Min(Width, wa.Width - 8);
                int h = Math.Min(Height, wa.Height - 8);
                Size = new Size(w, h);
                Location = new System.Drawing.Point(
                    wa.Left + Math.Max(0, (wa.Width - w) / 2),
                    wa.Top + Math.Max(0, (wa.Height - h) / 2));
            }
            catch { /* اگر به هر دلیل نشد، اندازه‌ی طراحی حفظ می‌شود */ }
        }

        private void FrmCase_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                this.SelectNextControl(this.ActiveControl, true, true, true, true);
            }
        }
        // آموزش — بازنویسی کامل به درخواست کاربر (بند ۵): باگ اصلی این بود که
        // TabIndex یک شمارنده مشترک بین فیلدهای سه گروه (grpHead/grpPhysical/
        // grpCase) بود، اما TabIndex در WinForms فقط داخل هر Container به‌طور
        // مستقل معنی دارد — شمارنده مشترک هیچ ترتیب واقعی‌ای بین گروه‌های
        // مختلف نمی‌ساخت. حالا هر گروه شمارنده محلی خودش را دارد (از صفر) و
        // ترتیب گروه‌ها/کانتینرهای اصلی هم در Designer.cs صریح تنظیم شده
        // (grpHead→grpPhysical→grpCase، و fieldsPanel→bottomBar→leftPanel).
        // نتیجه: Tab دقیقاً به ترتیب بصری بالا-به-پایین/راست-به-چپ هر ردیف
        // طی می‌شود و بعد از آخرین فیلد (شرح وضعیت فوری) مستقیم به «ذخیره»
        // می‌رسد، بدون هیچ پرش به کنترل‌های سمت چپ فرم (گرید/عکس‌ها).
        private void SetTabOrder()
        {
            // فیلدهای اتومات / غیرقابل تایپ
            txtFormNo.ReadOnly = true;
            txtFormNo.TabStop = false;
            txtFormNo.BackColor = SystemColors.Control;

            txtPhotoPath.ReadOnly = true;
            txtPhotoPath.TabStop = false;

            txtFamilyPhotoPath.ReadOnly = true;
            txtFamilyPhotoPath.TabStop = false;

            // ─── گروه ۱: مشخصات کلی سرپرست (ترتیب بصری هر ردیف: راست سپس چپ) ─
            int h = 0;
            txtHeadFullName.TabIndex = h++;
            txtHeadFatherName.TabIndex = h++;
            txtHeadSadat.TabIndex = h++;
            txtReligion.TabIndex = h++;
            txtHeadTazkiraNo.TabIndex = h++;
            txtHeadOriginalResidence.TabIndex = h++;
            txtHeadCurrentResidence.TabIndex = h++;
            txtRelationshipToFamily.TabIndex = h++;
            txtPhone.TabIndex = h++;
            txtRelativePhone.TabIndex = h++;
            txtMaritalStatus.TabIndex = h++;
            txtEducationLevel.TabIndex = h++;
            txtJob.TabIndex = h++;
            txtSkill.TabIndex = h++;

            // ─── گروه ۲: مشخصات جسمی ──────────────────────────────────────────
            int p = 0;
            txtDisabilityType.TabIndex = p++;
            txtDisabilityDegree.TabIndex = p++;

            // ─── گروه ۳: مشخصات پرونده ─────────────────────────────────────────
            int c = 0;
            txtCode.TabIndex = c++;
            txtCaseNo.TabIndex = c++;
            txtZone.TabIndex = c++;
            txtProvince.TabIndex = c++;
            txtDistrict.TabIndex = c++;
            txtRequestType.TabIndex = c++;
            txtPriorityLevel.TabIndex = c++;
            txtMigrationCardType.TabIndex = c++;
            txtCoveredByOrg.TabIndex = c++;
            dtpCaseDate.TabIndex = c++;
            txtServiceStatus.TabIndex = c++;
            txtStopReason.TabIndex = c++;
            txtLocationAddress.TabIndex = c++;
            txtSurveyors.TabIndex = c++;
            dtpSurveyDate.TabIndex = c++;
            txtUrgentSituation.TabIndex = c++;

            // دکمه‌ها از مسیر Tab خارج می‌شوند — به‌جز «ذخیره» که آخرین توقف
            // Tab بعد از فیلدهاست (طبق درخواست کاربر).
            btnSave.TabStop = true;
            btnSave.TabIndex = 0;
            btnEdit.TabStop = false;
            btnDelete.TabStop = false;
            btnNew.TabStop = false;
            btnSearch.TabStop = false;
            btnDocs.TabStop = false;
            btnFamily.TabStop = false;
            btnExportWord.TabStop = false;
            btnExportPdf.TabStop = false;
            btnExportExcel.TabStop = false;
            btnBatchExport.TabStop = false;
            btnPrint.TabStop = false;
            btnChooseStorageFolder.TabStop = false;
            btnBrowsePhoto.TabStop = false;
            btnBrowseFamilyPhoto.TabStop = false;

            // کنترل‌های سمت چپ فرم (گرید/فیلتر/عکس) هرگز نباید در مسیر Tab
            // فیلدها باشند؛ leftPanel در Designer.cs آخرین TabIndex ریشه را
            // دارد، اما این‌ها هم برای اطمینان مضاعف صریحاً خاموش می‌شوند.
            cmbServiceStatusFilter.TabStop = false;
            dgvCases.TabStop = false;
        }

        private void ConfigureServiceStatusControls()
        {
            txtServiceStatus.Items.Clear();
            txtServiceStatus.Items.AddRange(serviceStatuses);

            if (txtServiceStatus.SelectedIndex < 0)
                txtServiceStatus.SelectedIndex = 0;

            cmbServiceStatusFilter.SelectedIndexChanged -= cmbServiceStatusFilter_SelectedIndexChanged;

            cmbServiceStatusFilter.Items.Clear();
            cmbServiceStatusFilter.Items.Add("همه");
            cmbServiceStatusFilter.Items.AddRange(serviceStatuses);

            if (cmbServiceStatusFilter.SelectedIndex < 0)
                cmbServiceStatusFilter.SelectedIndex = 0;

            cmbServiceStatusFilter.SelectedIndexChanged += cmbServiceStatusFilter_SelectedIndexChanged;
        }

        // "دلیل قطع موقت" فقط وقتی وضعیت "قطع موقت" است نمایش و اجباری می‌شود
        private void TxtServiceStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateStopReasonVisibility();
        }

        private void UpdateStopReasonVisibility()
        {
            bool isTempStopped = NormalizeServiceStatus(txtServiceStatus.Text) == "قطع موقت";
            lblStopReason.Visible = isTempStopped;
            txtStopReason.Visible = isTempStopped;

            if (!isTempStopped)
                txtStopReason.Text = "";
        }

        private void SetComboBoxText(ComboBox comboBox, string value)
        {
            value = NormalizeServiceStatus(value);

            int index = comboBox.FindStringExact(value);

            if (index >= 0)
                comboBox.SelectedIndex = index;
            else if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        private string NormalizeServiceStatus(string value)
        {
            value = (value ?? "").Trim();

            if (value == "در حالت قطع")
                return "قطع";

            if (value == "درانتظار" || value == "در انتظار")
                return "در انتظار تأیید";

            if (value == "")
                return "فعال";

            return value;
        }

        private bool IsAllowedServiceStatus(string value)
        {
            value = NormalizeServiceStatus(value);

            foreach (string status in serviceStatuses)
            {
                if (status == value)
                    return true;
            }

            return false;
        }

        private void NormalizeSavedServiceStatuses()
        {
            try
            {
                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(@"
                    UPDATE TblCase
                    SET ServiceStatus =
                        CASE
                            WHEN ServiceStatus = 'در حالت قطع' THEN 'قطع'
                            WHEN ServiceStatus IN ('درانتظار', 'در انتظار') THEN 'در انتظار تأیید'
                            ELSE ServiceStatus
                        END
                    WHERE ServiceStatus IN ('در حالت قطع', 'درانتظار', 'در انتظار')", con))
                {
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[FrmCase.NormalizeSavedServiceStatuses] " + ex.Message);
            }
        }

        private string CleanFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "پرونده";

            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c.ToString(), "");

            value = value.Trim();

            if (string.IsNullOrWhiteSpace(value))
                return "پرونده";

            return value;
        }

        private void FrmCase_Load(object sender, EventArgs e)
        {
            // آموزش — رفع قطعی «دکمه‌ها زیر صفحه می‌روند»: به‌خاطر مقیاس‌بندی DPI
            // ویندوز (۱۲۵٪/۱۵۰٪)، فرم بزرگ‌تر از اندازه‌ی طراحی رندر می‌شود و از
            // ارتفاع صفحه بیرون می‌زند. اینجا فرم را با «ناحیه‌ی کاری واقعیِ صفحه»
            // (زیر نوار عنوان، بالای تسک‌بار) تطبیق می‌دهیم و وسط‌چین می‌کنیم؛ چون
            // ناحیه‌ی فیلدها اسکرول دارد، کوتاه‌شدن ارتفاع مشکلی ایجاد نمی‌کند و
            // دکمه‌های پایین (که Dock=Bottom هستند) همیشه دیده می‌شوند.
            FitToScreen();

            Text = "پرونده‌ها  —  " + SecurityContext.CenterDisplay;
            txtFormNo.ReadOnly = true;
            txtFormNo.Enabled = true;
            txtFormNo.TabStop = false;
            txtPhotoPath.ReadOnly = true;
            txtFamilyPhotoPath.ReadOnly = true;

            picPhoto.SizeMode = PictureBoxSizeMode.StretchImage;
            picFamilyPhoto.SizeMode = PictureBoxSizeMode.StretchImage;

            KeyPreview = true;
            KeyDown -= FrmCase_KeyDown;
            KeyDown += FrmCase_KeyDown;
            ConfigureServiceStatusControls();
            NormalizeSavedServiceStatuses();
            txtServiceStatus.SelectedIndexChanged -= TxtServiceStatus_SelectedIndexChanged;
            txtServiceStatus.SelectedIndexChanged += TxtServiceStatus_SelectedIndexChanged;

            // آموزش — چک‌باکس سالم/معلول (به درخواست کاربر): با تیک «سالم»،
            // فیلدهای نوع/درجه معلولیت غیرفعال و خالی می‌شوند.
            chkHeadHealthy.CheckedChanged -= ChkHeadHealthy_CheckedChanged;
            chkHeadHealthy.CheckedChanged += ChkHeadHealthy_CheckedChanged;

            SetTabOrder();
            btnSave.TabStop = false;
            btnEdit.TabStop = false;
            btnDelete.TabStop = false;
            btnNew.TabStop = false;
            btnSearch.TabStop = false;
            btnDocs.TabStop = false;
            btnFamily.TabStop = false;
            btnExportWord.TabStop = false;
            Helpers.UiTheme.ApplyPersianDateColumns(dgvCases, "CaseDate");
            LoadLookupCombos();
            LoadCases();
            ClearForm();

            // اگر فرم برای «باز کردن یک پرونده‌ی مشخص» فراخوانی شده، همان را لود کن.
            if (_pendingOpenCaseId > 0)
            {
                try { LoadCaseById(_pendingOpenCaseId); }
                catch (Exception ex) { Debug.WriteLine("[FrmCase open pending] " + ex.Message); }
                _pendingOpenCaseId = 0;
            }
        }

        // آموزش — بارگذاری کمبوها از TblLookup در Load (نه InitializeComponent):
        // مقادیر هاردکد داخل Designer.cs به‌عنوان fallback باقی می‌مانند (اگر
        // دیتابیس لحظه‌ای در دسترس نبود یا این متد اجرا نشد، فرم باز هم مقداری
        // برای انتخاب دارد)، ولی اینجا با مقادیر واقعی/قابل‌ویرایش از تنظیمات
        // جایگزین می‌شوند. توجه: txtServiceStatus/cmbServiceStatusFilter عمداً
        // اینجا نیستند — TblCase.ServiceStatus یک CHECK constraint دیتابیسی
        // دارد (فقط ۴ مقدار مجاز)، پس نباید از تنظیمات آزادانه ویرایش شود.
        private void LoadLookupCombos()
        {
            Helpers.LookupHelper.FillCombo(txtProvince, "Province");
            Helpers.LookupHelper.FillCombo(txtRequestType, "RequestType");
            Helpers.LookupHelper.FillCombo(txtPriorityLevel, "PriorityLevel");
            Helpers.LookupHelper.FillCombo(txtReligion, "Madhab");
            Helpers.LookupHelper.FillCombo(txtHeadSadat, "HeadSadat");
            Helpers.LookupHelper.FillCombo(txtDisabilityType, "DisabilityType");
            Helpers.LookupHelper.FillCombo(txtMaritalStatus, "MaritalStatus");
            Helpers.LookupHelper.FillCombo(txtDisabilityDegree, "DisabilityDegree");
            Helpers.LookupHelper.FillCombo(txtEducationLevel, "HeadEducationLevel");
            Helpers.LookupHelper.FillCombo(txtCoveredByOrg, "CoveredByOrg");
        }
        private void btnChooseStorageFolder_Click(object sender, EventArgs e)
        {
            string oldRoot = FileHelper.GetBaseRootFolder();

            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "محل اصلی ذخیره عکس‌ها، اسناد، خروجی Word/PDF و گزارش‌های Excel را انتخاب کنید";
                fbd.ShowNewFolderButton = true;

                if (!string.IsNullOrWhiteSpace(oldRoot) && Directory.Exists(oldRoot))
                    fbd.SelectedPath = oldRoot;

                if (fbd.ShowDialog() != DialogResult.OK)
                    return;

                string newRoot = fbd.SelectedPath;
                string error;

                if (!FileHelper.SetBaseRootFolder(newRoot, out error))
                {
                    Msg.Show(error, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string finalRoot = FileHelper.GetBaseRootFolder();

                if (string.IsNullOrWhiteSpace(finalRoot))
                {
                    Msg.Show("محل ذخیره تنظیم نشد.", "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(txtCode.Text))
                {
                    string caseFolder = FileHelper.EnsureCaseStructure(txtCode.Text.Trim());

                    if (string.IsNullOrWhiteSpace(caseFolder))
                    {
                        Msg.Show(
                            "محل ذخیره تغییر کرد، اما ساخت پوشه‌های پرونده فعلی انجام نشد:" +
                            Environment.NewLine +
                            FileHelper.LastError,
                            "هشدار",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                }

                Msg.Show(
                    "محل ذخیره فایل‌ها با موفقیت تغییر کرد." +
                    Environment.NewLine +
                    Environment.NewLine +
                    "مسیر قبلی:" +
                    Environment.NewLine +
                    (string.IsNullOrWhiteSpace(oldRoot) ? "تنظیم نشده بود" : oldRoot) +
                    Environment.NewLine +
                    Environment.NewLine +
                    "مسیر جدید:" +
                    Environment.NewLine +
                    finalRoot +
                    Environment.NewLine +
                    Environment.NewLine +
                    "توجه: فایل‌های قبلی به صورت خودکار منتقل نمی‌شوند.",
                    "تغییر محل ذخیره",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }


        private void ChkHeadHealthy_CheckedChanged(object sender, EventArgs e)
        {
            UpdateHeadPhysicalState();
        }

        // با تیک «سالم است»، فیلدهای نوع/درجه معلولیت غیرفعال و خالی می‌شوند؛
        // با برداشتن تیک (یعنی معلول) دوباره فعال می‌شوند.
        private void UpdateHeadPhysicalState()
        {
            bool isHealthy = chkHeadHealthy.Checked;

            txtDisabilityType.Enabled = !isHealthy;
            txtDisabilityDegree.Enabled = !isHealthy;

            if (isHealthy)
            {
                txtDisabilityType.Text = "";
                txtDisabilityDegree.Text = "";
            }
        }

        private void ClearForm()
        {
            currentCaseId = 0;

            selectedHeadPhotoSource = "";
            selectedFamilyPhotoSource = "";

            savedHeadPhotoPath = "";
            savedFamilyPhotoPath = "";

            txtFormNo.Text = GetNextFormNo();
            txtCode.Text = "";
            txtCaseNo.Text = "";
            txtZone.Text = "";
            txtProvince.Text = "";
            txtDistrict.Items.Clear();
            txtDistrict.Text = "";
            txtRequestType.Text = "";
            txtPriorityLevel.Text = "";
            txtHeadFullName.Text = "";
            txtHeadFatherName.Text = "";
            txtHeadSadat.Text = "";
            txtReligion.Text = "";
            txtHeadTazkiraNo.Text = "";
            txtHeadOriginalResidence.Text = "";
            txtHeadCurrentResidence.Text = "";
            txtRelationshipToFamily.Text = "";
            txtPhone.Text = "";
            txtRelativePhone.Text = "";
            txtCoveredByOrg.Text = "";
            txtJob.Text = "";
            txtSkill.Text = "";
            txtDisabilityDegree.Text = "";
            txtDisabilityType.Text = "";
            chkHeadHealthy.Checked = true; // پیش‌فرض «سالم»
            UpdateHeadPhysicalState();
            txtMigrationCardType.Text = "";
            txtMaritalStatus.Text = "";
            txtSurveyors.Text = "";
            txtLocationAddress.Text = "";
            txtEducationLevel.Text = "";
            if (txtServiceStatus.Items.Count > 0)
                txtServiceStatus.SelectedIndex = 0;
            else
                txtServiceStatus.Text = "";
            txtStopReason.Text = "";
            UpdateStopReasonVisibility();
            txtUrgentSituation.Text = "";
            txtPhotoPath.Text = "";
            txtFamilyPhotoPath.Text = "";

            // آموزش — رفع باگ «دکمه جدید همه فیلدها را خالی نمی‌کند»: این کمبوها
            // DropDownStyle=DropDownList دارند و برای آن‌ها «.Text = ""» هیچ اثری
            // ندارد (مقدار انتخاب‌شده باقی می‌ماند). پس صریحاً SelectedIndex=-1
            // می‌شوند تا واقعاً خالی شوند. (txtServiceStatus عمداً اینجا نیست چون
            // CHECK constraint دارد و بالاتر روی «فعال» تنظیم می‌شود.)
            ComboBox[] dropdowns =
            {
                txtZone, txtProvince, txtDistrict, txtRequestType, txtPriorityLevel,
                txtHeadSadat, txtReligion, txtCoveredByOrg, txtDisabilityType,
                txtDisabilityDegree, txtMaritalStatus, txtEducationLevel
            };
            foreach (ComboBox cmb in dropdowns)
            {
                cmb.SelectedIndex = -1;
                cmb.Text = "";
            }

            dtpCaseDate.Value = DateTime.Today;
            dtpSurveyDate.Value = DateTime.Today;

            ClearPictureBox(picPhoto);
            ClearPictureBox(picFamilyPhoto);

            // آموزش — به‌درخواست کاربر: شماره فرم همیشه اتومات و یکتا و
            // غیرقابل ویرایش است؛ برخلاف قبل، اینجا دیگر ReadOnly باز نمی‌شود.
            txtFormNo.ReadOnly = true;
            txtFormNo.TabStop = false;

            txtCode.Enabled = true;
            txtCode.Focus();
        }

        private bool ValidateForm()
        {
            if (txtFormNo.Text.Trim() == "")
                txtFormNo.Text = GetNextFormNo();

            if (txtCode.Text.Trim() == "")
            {
                Msg.Show("کد اختصاصی را وارد کنید");
                txtCode.Focus();
                return false;
            }

            if (txtHeadFullName.Text.Trim() == "")
            {
                Msg.Show("نام سرپرست را وارد کنید");
                txtHeadFullName.Focus();
                return false;
            }

            if (!IsAllowedServiceStatus(txtServiceStatus.Text))
            {
                Msg.Show("وضعیت خدمات را از لیست انتخاب کنید");
                txtServiceStatus.Focus();
                return false;
            }

            if (NormalizeServiceStatus(txtServiceStatus.Text) == "قطع موقت" && string.IsNullOrWhiteSpace(txtStopReason.Text))
            {
                Msg.Show("دلیل قطع موقت را وارد کنید");
                txtStopReason.Focus();
                return false;
            }

            return true;
        }

        private bool IsValidImageFile(string filePath)
        {
            return IsValidImageFile(
                filePath,
                1,
                FileHelper.MaxPhotoFileSizeBytes,
                "حجم عکس باید کمتر از 15 مگابایت باشد");
        }

        private bool IsValidFamilyPhotoFile(string filePath)
        {
            return IsValidImageFile(
                filePath,
                MinFamilyPhotoFileSizeBytes,
                MaxFamilyPhotoFileSizeBytes,
                "حجم عکس جمعی باید از 50 کیلوبایت تا 1 مگابایت باشد");
        }

        private bool IsValidImageFile(string filePath, long minBytes, long maxBytes, string sizeErrorMessage)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Msg.Show("فایل عکس پیدا نشد");
                return false;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
            {
                Msg.Show("فقط فایل JPG، JPEG یا PNG مجاز است");
                return false;
            }

            FileInfo fi = new FileInfo(filePath);

            if (fi.Length < minBytes || fi.Length > maxBytes)
            {
                Msg.Show(sizeErrorMessage);
                return false;
            }

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (DrawingImage.FromStream(fs, false, true))
                {
                    return true;
                }
            }
            catch
            {
                Msg.Show("فایل انتخاب‌شده عکس معتبر نیست");
                return false;
            }
        }

        private void ClearPictureBox(PictureBox pictureBox)
        {
            if (pictureBox.Image != null)
            {
                DrawingImage oldImage = pictureBox.Image;
                pictureBox.Image = null;
                oldImage.Dispose();
            }
        }

        private void LoadImageToPictureBox(string filePath, PictureBox pictureBox)
        {
            ClearPictureBox(pictureBox);

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (DrawingImage img = DrawingImage.FromStream(fs))
                {
                    pictureBox.Image = new Bitmap(img);
                }
            }
            catch (Exception ex)
            {
                ClearPictureBox(pictureBox);
                Msg.Show("خطا در بارگذاری عکس: " + ex.Message);
            }
        }

        private void AddStringParameter(SQLiteCommand cmd, string parameterName, string value)
        {
            cmd.Parameters.AddWithValue(parameterName, value ?? "");
        }

        private void AddDateParameter(SQLiteCommand cmd, string parameterName, DateTime value)
        {
            cmd.Parameters.AddWithValue(parameterName, value.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void AddIntParameter(SQLiteCommand cmd, string parameterName, int value)
        {
            cmd.Parameters.AddWithValue(parameterName, value);
        }

        private void AddCaseParameters(SQLiteCommand cmd, bool includeFormNo)
        {
            if (includeFormNo)
            {
                int formNo;
                if (!int.TryParse(txtFormNo.Text.Trim(), out formNo))
                    throw new InvalidOperationException("شماره فرم باید عددی باشد");

                AddIntParameter(cmd, "@FormNo", formNo);
            }

            AddStringParameter(cmd, "@Code", txtCode.Text.Trim());
            AddStringParameter(cmd, "@CaseNo", txtCaseNo.Text.Trim());
            AddDateParameter(cmd, "@CaseDate", dtpCaseDate.Value.Date);
            AddStringParameter(cmd, "@Zone", txtZone.Text.Trim());
            AddStringParameter(cmd, "@Province", txtProvince.Text.Trim());
            AddStringParameter(cmd, "@District", txtDistrict.Text.Trim());
            AddStringParameter(cmd, "@RequestType", txtRequestType.Text.Trim());
            AddStringParameter(cmd, "@PriorityLevel", txtPriorityLevel.Text.Trim());
            AddStringParameter(cmd, "@HeadFullName", txtHeadFullName.Text.Trim());
            AddStringParameter(cmd, "@HeadFatherName", txtHeadFatherName.Text.Trim());
            AddStringParameter(cmd, "@HeadSadat", txtHeadSadat.Text.Trim());
            AddStringParameter(cmd, "@Religion", txtReligion.Text.Trim());
            AddStringParameter(cmd, "@HeadTazkiraNo", txtHeadTazkiraNo.Text.Trim());
            AddStringParameter(cmd, "@HeadOriginalResidence", txtHeadOriginalResidence.Text.Trim());
            AddStringParameter(cmd, "@HeadCurrentResidence", txtHeadCurrentResidence.Text.Trim());
            AddStringParameter(cmd, "@RelationshipToFamily", txtRelationshipToFamily.Text.Trim());
            AddStringParameter(cmd, "@Phone", txtPhone.Text.Trim());
            AddStringParameter(cmd, "@RelativePhone", txtRelativePhone.Text.Trim());
            AddStringParameter(cmd, "@CoveredByOrg", txtCoveredByOrg.Text.Trim());
            AddStringParameter(cmd, "@Job", txtJob.Text.Trim());
            AddStringParameter(cmd, "@Skill", txtSkill.Text.Trim());
            AddStringParameter(cmd, "@DisabilityDegree", txtDisabilityDegree.Text.Trim());
            AddStringParameter(cmd, "@DisabilityType", txtDisabilityType.Text.Trim());
            AddStringParameter(cmd, "@MigrationCardType", txtMigrationCardType.Text.Trim());
            AddStringParameter(cmd, "@MaritalStatus", txtMaritalStatus.Text.Trim());
            AddStringParameter(cmd, "@Surveyors", txtSurveyors.Text.Trim());
            AddDateParameter(cmd, "@SurveyDate", dtpSurveyDate.Value.Date);
            AddStringParameter(cmd, "@LocationAddress", txtLocationAddress.Text.Trim());
            AddStringParameter(cmd, "@EducationLevel", txtEducationLevel.Text.Trim());
            AddStringParameter(cmd, "@ServiceStatus", NormalizeServiceStatus(txtServiceStatus.Text));
            AddStringParameter(cmd, "@StopReason", NormalizeServiceStatus(txtServiceStatus.Text) == "قطع موقت" ? txtStopReason.Text.Trim() : "");
            AddStringParameter(cmd, "@UrgentSituation", txtUrgentSituation.Text.Trim());
            AddStringParameter(cmd, "@PhotoPath", txtPhotoPath.Text.Trim());
            AddStringParameter(cmd, "@FamilyPhotoPath", txtFamilyPhotoPath.Text.Trim());
        }

        private bool IsFormNoExists(string formNo, int excludedCaseId)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("SELECT COUNT(1) FROM TblCase WHERE FormNo = @Value AND CasID <> @CasID", con))
            {
                AddStringParameter(cmd, "@Value", formNo);
                AddIntParameter(cmd, "@CasID", excludedCaseId);

                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private bool IsCodeExists(string code, int excludedCaseId)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("SELECT COUNT(1) FROM TblCase WHERE Code = @Value AND CasID <> @CasID", con))
            {
                AddStringParameter(cmd, "@Value", code);
                AddIntParameter(cmd, "@CasID", excludedCaseId);

                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private bool IsSamePath(string path1, string path2)
        {
            if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2))
                return false;

            try
            {
                return string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void SaveSelectedPhotos()
        {
            string caseCode = txtCode.Text.Trim();

            if (selectedHeadPhotoSource != "")
            {
                if (IsSamePath(selectedHeadPhotoSource, savedHeadPhotoPath))
                {
                    txtPhotoPath.Text = savedHeadPhotoPath;
                }
                else
                {
                    string savedPath = FileHelper.SaveFileToCaseFolder(
                        selectedHeadPhotoSource,
                        caseCode,
                        FileHelper.SectionHeadPhoto,
                        caseCode + "-Head",
                        savedHeadPhotoPath);

                    if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
                        throw new Exception("عکس سرپرست ذخیره نشد: " + FileHelper.LastError);

                    txtPhotoPath.Text = savedPath;
                    savedHeadPhotoPath = savedPath;
                }
            }
            else
            {
                txtPhotoPath.Text = savedHeadPhotoPath;
            }

            if (selectedFamilyPhotoSource != "")
            {
                if (IsSamePath(selectedFamilyPhotoSource, savedFamilyPhotoPath))
                {
                    txtFamilyPhotoPath.Text = savedFamilyPhotoPath;
                }
                else
                {
                    string savedPath = FileHelper.SaveFileToCaseFolder(
                        selectedFamilyPhotoSource,
                        caseCode,
                        FileHelper.SectionFamilyPhoto,
                        caseCode + "-Family",
                        savedFamilyPhotoPath);

                    if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
                        throw new Exception("عکس جمعی خانواده ذخیره نشد: " + FileHelper.LastError);

                    txtFamilyPhotoPath.Text = savedPath;
                    savedFamilyPhotoPath = savedPath;
                }
            }
            else
            {
                txtFamilyPhotoPath.Text = savedFamilyPhotoPath;
            }
        }

        private void LoadCases()
        {
            try
            {
                using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand(@"
                    SELECT CasID, FormNo, Code, CaseNo, HeadFullName, Phone, ServiceStatus, CaseDate, PhotoPath
                    FROM TblCase
                    WHERE (@ServiceStatus = '' OR ServiceStatus = @ServiceStatus)
                      AND (@CID = 0 OR CenterID = @CID)
                    ORDER BY CasID DESC", con))
                {
                    string serviceStatus = "";

                    if (cmbServiceStatusFilter != null &&
                        cmbServiceStatusFilter.SelectedItem != null &&
                        cmbServiceStatusFilter.Text.Trim() != "همه")
                    {
                        serviceStatus = NormalizeServiceStatus(cmbServiceStatusFilter.Text);
                    }

                    AddStringParameter(cmd, "@ServiceStatus", serviceStatus);
                    cmd.Parameters.AddWithValue("@CID", Helpers.SecurityContext.CenterFilterId);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);
                        dgvCases.DataSource = dt;
                    }
                }

                ConfigureCasesGrid();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری لیست: " + ex.Message);
            }
        }

        private const string PhotoThumbColumnName = "colPhotoThumb";

        // آموزش — رفع درخواست کاربر: لیست پرونده‌ها فقط سه ستون (کد اختصاصی/
        // نام سرپرست/عکس) را نشان می‌دهد تا بدون اسکرول افقی کامل در پنل جا
        // شود. بقیه ستون‌های دیتای بارگذاری‌شده (CasID برای انتخاب ردیف، و
        // بقیه برای منطق داخلی) پنهان می‌مانند نه حذف — چون dgvCases_CellClick
        // و بقیه کد همچنان به مقدار CasID هر ردیف نیاز دارند.
        private void ConfigureCasesGrid()
        {
            if (dgvCases.Columns.Count == 0)
                return;

            SetGridHeader("Code", "کد اختصاصی");
            SetGridHeader("HeadFullName", "نام سرپرست");

            HideGridColumn("CasID");
            HideGridColumn("FormNo");
            HideGridColumn("CaseNo");
            HideGridColumn("Phone");
            HideGridColumn("ServiceStatus");
            HideGridColumn("CaseDate");
            HideGridColumn("PhotoPath");

            // آموزش — به درخواست کاربر: ستون «عکس» از لیست ثبت‌شده‌ها حذف شد.
            // عکس‌های کوچک در گرید اغلب به‌صورت آیکون خطا (❌) نمایش داده می‌شدند
            // (فایل پیدا نمی‌شد یا مسیر نامعتبر بود) و ظاهر غیرحرفه‌ای می‌ساختند.
            // عکس اصلی همچنان با انتخاب پرونده در کادرهای بالای فرم دیده می‌شود.
            if (dgvCases.Columns.Contains(PhotoThumbColumnName))
                dgvCases.Columns.Remove(PhotoThumbColumnName);

            if (dgvCases.Columns.Contains("Code"))
                dgvCases.Columns["Code"].DisplayIndex = 0;
            if (dgvCases.Columns.Contains("HeadFullName"))
                dgvCases.Columns["HeadFullName"].DisplayIndex = 1;

            dgvCases.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvCases.MultiSelect = false;
            dgvCases.ReadOnly = true;
            dgvCases.AllowUserToAddRows = false;
            dgvCases.AllowUserToDeleteRows = false;
            dgvCases.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            // ردیف‌های جمع‌وجورتر و حرفه‌ای‌تر (چون دیگر thumbnail عکس نداریم).
            dgvCases.RowTemplate.Height = 32;
            dgvCases.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }

        private void HideGridColumn(string columnName)
        {
            if (dgvCases.Columns.Contains(columnName))
                dgvCases.Columns[columnName].Visible = false;
        }

        // آموزش — بارگذاری عکس کوچک هر ردیف از PhotoPath ذخیره‌شده؛ تصویر در
        // اندازه کوچک (۴۰×۴۰) ساخته می‌شود تا حافظه/کارایی گرید برای تعداد
        // زیاد ردیف مشکل ایجاد نکند، نه تصویر اصلی با رزولوشن کامل.
        // آموزش — سقف ایمن برای جلوگیری از فریز UI: تولید thumbnail برای هر ردیف
        // یک FileStream + Bitmap روی UI thread است. با ده‌ها هزار پرونده این کار
        // برنامه را برای چند ثانیه/دقیقه قفل می‌کرد. با این سقف، همه‌ی ردیف‌های
        // داده همچنان نمایش داده می‌شوند (هیچ رکوردی پنهان نمی‌شود) و فقط برای
        // تعداد زیاد، عکسِ کوچکِ درون‌گرید تولید نمی‌شود؛ عکس اصلی با باز کردن
        // پرونده کاملاً در دسترس است.
        private const int MaxInlineThumbnails = 500;

        private void LoadCaseThumbnails()
        {
            if (!dgvCases.Columns.Contains("PhotoPath") || !dgvCases.Columns.Contains(PhotoThumbColumnName))
                return;

            bool skipImages = dgvCases.Rows.Count > MaxInlineThumbnails;

            foreach (DataGridViewRow row in dgvCases.Rows)
            {
                if (row.IsNewRow)
                    continue;

                row.Height = 46;

                if (skipImages)
                    continue;

                object pathValue = row.Cells["PhotoPath"].Value;
                string path = pathValue == null || pathValue == DBNull.Value ? "" : pathValue.ToString();
                row.Cells[PhotoThumbColumnName].Value = LoadThumbnail(path, 40);
            }
        }

        private DrawingImage LoadThumbnail(string path, int size)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (DrawingImage source = DrawingImage.FromStream(fs))
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

        private void SetGridHeader(string columnName, string headerText)
        {
            if (dgvCases.Columns.Contains(columnName))
                dgvCases.Columns[columnName].HeaderText = headerText;
        }

        private void btnBrowsePhoto_Click(object sender, EventArgs e)
        {
            if (txtCode.Text.Trim() == "")
            {
                Msg.Show("اول کد اختصاصی را وارد کن");
                txtCode.Focus();
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "انتخاب عکس سرپرست";
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png|فایل‌های JPG|*.jpg;*.jpeg|فایل‌های PNG|*.png";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (!IsValidImageFile(ofd.FileName))
                        return;

                    selectedHeadPhotoSource = ofd.FileName;
                    txtPhotoPath.Text = ofd.FileName;
                    LoadImageToPictureBox(ofd.FileName, picPhoto);
                }
            }
        }

        private void btnBrowseFamilyPhoto_Click(object sender, EventArgs e)
        {
            if (txtCode.Text.Trim() == "")
            {
                Msg.Show("اول کد اختصاصی را وارد کن");
                txtCode.Focus();
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "انتخاب عکس جمعی خانواده";
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png|فایل‌های JPG|*.jpg;*.jpeg|فایل‌های PNG|*.png";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (!IsValidFamilyPhotoFile(ofd.FileName))
                        return;

                    selectedFamilyPhotoSource = ofd.FileName;
                    txtFamilyPhotoPath.Text = ofd.FileName;
                    LoadImageToPictureBox(ofd.FileName, picFamilyPhoto);
                }
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
                Msg.Show("کاربر فقط مشاهده اجازه ثبت پرونده ندارد.");
                return;
            }

            if (currentCaseId != 0)
            {
                Msg.Show("این رکورد قبلا ذخیره شده است. برای تغییرات از دکمه ویرایش استفاده کن");
                return;
            }

            if (!ValidateForm())
                return;

            txtFormNo.Text = GetNextFormNo();

            try
            {
                if (IsFormNoExists(txtFormNo.Text.Trim(), 0))
                {
                    txtFormNo.Text = GetNextFormNo();

                    if (IsFormNoExists(txtFormNo.Text.Trim(), 0))
                    {
                        Msg.Show("شماره فرم تکراری است. دوباره دکمه ذخیره را بزنید");
                        return;
                    }
                }

                if (IsCodeExists(txtCode.Text.Trim(), 0))
                {
                    Msg.Show("کد اختصاصی تکراری است");
                    txtCode.Focus();
                    return;
                }

                SaveSelectedPhotos();

                using (SQLiteConnection con = db.GetConnection())
                {
                    string query = @"INSERT INTO TblCase
                    (
                        FormNo, Code, CaseNo, CaseDate,
                        Zone, Province, District, RequestType, PriorityLevel,
                        HeadFullName, HeadFatherName, HeadSadat, Religion, HeadTazkiraNo,
                        HeadOriginalResidence, HeadCurrentResidence, RelationshipToFamily,
                        Phone, RelativePhone, CoveredByOrg, Job, Skill,
                        DisabilityDegree, DisabilityType, MigrationCardType, MaritalStatus,
                        Surveyors, SurveyDate, LocationAddress, EducationLevel, ServiceStatus, StopReason, UrgentSituation,
                        PhotoPath, FamilyPhotoPath, CenterID
                    )
                    VALUES
                    (
                        @FormNo, @Code, @CaseNo, @CaseDate,
                        @Zone, @Province, @District, @RequestType, @PriorityLevel,
                        @HeadFullName, @HeadFatherName, @HeadSadat, @Religion, @HeadTazkiraNo,
                        @HeadOriginalResidence, @HeadCurrentResidence, @RelationshipToFamily,
                        @Phone, @RelativePhone, @CoveredByOrg, @Job, @Skill,
                        @DisabilityDegree, @DisabilityType, @MigrationCardType, @MaritalStatus,
                        @Surveyors, @SurveyDate, @LocationAddress, @EducationLevel, @ServiceStatus, @StopReason, @UrgentSituation,
                        @PhotoPath, @FamilyPhotoPath, @CenterID
                    );";

                    using (var cmd = new SQLiteCommand(query, con))
                    {
                        AddCaseParameters(cmd, true);
                        cmd.Parameters.AddWithValue("@CenterID", Helpers.SecurityContext.CurrentCenterId > 0
                            ? Helpers.SecurityContext.CurrentCenterId : 1);

                        con.Open();
                        cmd.ExecuteNonQuery();

                        // آموزش — رفع نشت resource: قبلاً این SQLiteCommand بدون
                        // Dispose ساخته می‌شد و هر ذخیره یک command رهاشده باقی
                        // می‌گذاشت. حالا داخل using بسته می‌شود.
                        using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", con))
                            currentCaseId = Convert.ToInt32((long)idCmd.ExecuteScalar());
                    }
                }

                selectedHeadPhotoSource = "";
                selectedFamilyPhotoSource = "";

                txtFormNo.ReadOnly = true;
                txtFormNo.TabStop = false;
                txtCode.Enabled = false;

                AuditLogger.Log("ثبت", "TblCase", currentCaseId, "", BuildCurrentCaseAuditText());
                AuditLogger.RecordStatusChange(currentCaseId, "", NormalizeServiceStatus(txtServiceStatus.Text));

                Msg.Show("اطلاعات با موفقیت ذخیره شد");
                LoadCases();
            }
            catch (SQLiteException ex)
            {
                Msg.Show("خطا در ذخیره: " + ex.Message);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ذخیره: " + ex.Message);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.CanEdit())
            {
                Msg.Show("کاربر فقط مشاهده اجازه ویرایش پرونده ندارد.");
                return;
            }

            if (currentCaseId == 0)
            {
                Msg.Show("اول رکورد را انتخاب کن");
                return;
            }

            if (!ValidateForm())
                return;

            try
            {
                CenterGuard.EnsureCaseAccess(db, currentCaseId);

                string oldValue = GetCaseAuditTextFromDb(currentCaseId);
                string oldStatus = GetCaseStatusById(currentCaseId);

                if (IsCodeExists(txtCode.Text.Trim(), currentCaseId))
                {
                    Msg.Show("کد اختصاصی تکراری است");
                    txtCode.Focus();
                    return;
                }

                SaveSelectedPhotos();

                using (SQLiteConnection con = db.GetConnection())
                {
                    string query = @"UPDATE TblCase SET
                        Code = @Code,
                        CaseNo = @CaseNo,
                        CaseDate = @CaseDate,
                        Zone = @Zone,
                        Province = @Province,
                        District = @District,
                        RequestType = @RequestType,
                        PriorityLevel = @PriorityLevel,
                        HeadFullName = @HeadFullName,
                        HeadFatherName = @HeadFatherName,
                        HeadSadat = @HeadSadat,
                        Religion = @Religion,
                        HeadTazkiraNo = @HeadTazkiraNo,
                        HeadOriginalResidence = @HeadOriginalResidence,
                        HeadCurrentResidence = @HeadCurrentResidence,
                        RelationshipToFamily = @RelationshipToFamily,
                        Phone = @Phone,
                        RelativePhone = @RelativePhone,
                        CoveredByOrg = @CoveredByOrg,
                        Job = @Job,
                        Skill = @Skill,
                        DisabilityDegree = @DisabilityDegree,
                        DisabilityType = @DisabilityType,
                        MigrationCardType = @MigrationCardType,
                        MaritalStatus = @MaritalStatus,
                        Surveyors = @Surveyors,
                        SurveyDate = @SurveyDate,
                        LocationAddress = @LocationAddress,
                        EducationLevel = @EducationLevel,
                        ServiceStatus = @ServiceStatus,
                        StopReason = @StopReason,
                        UrgentSituation = @UrgentSituation,
                        PhotoPath = @PhotoPath,
                        FamilyPhotoPath = @FamilyPhotoPath,
                        UpdatedAt = datetime('now')
                    WHERE CasID = @CasID AND (@CID = 0 OR CenterID = @CID)";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                    {
                        AddCaseParameters(cmd, false);
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);

                        con.Open();
                        int affectedRows = cmd.ExecuteNonQuery();

                        if (affectedRows == 0)
                        {
                            Msg.Show("رکورد برای ویرایش پیدا نشد یا متعلق به مرکز دیگری است");
                            currentCaseId = 0;
                            LoadCases();
                            return;
                        }
                    }
                }

                selectedHeadPhotoSource = "";
                selectedFamilyPhotoSource = "";

                AuditLogger.Log("ویرایش", "TblCase", currentCaseId, oldValue, BuildCurrentCaseAuditText());
                AuditLogger.RecordStatusChange(currentCaseId, oldStatus, NormalizeServiceStatus(txtServiceStatus.Text));

                Msg.Show("اطلاعات ویرایش شد");
                LoadCases();
            }
            catch (SQLiteException ex)
            {
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                    Msg.Show("شماره فرم یا کد اختصاصی تکراری است");
                else
                    Msg.Show("خطا در ویرایش: " + ex.Message);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ویرایش: " + ex.Message);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول رکورد را انتخاب کن");
                return;
            }

            if (!SecurityContext.CanDelete())
            {
                Msg.Show("حذف پرونده فقط برای مدیر سیستم مجاز است.");
                return;
            }

            DeleteMode mode = ShowDeleteModeDialog();
            if (mode == DeleteMode.Cancel)
                return;

            try
            {
                CenterGuard.EnsureCaseAccess(db, currentCaseId);

                string oldValue = GetCaseAuditTextFromDb(currentCaseId);
                int deletedCaseId = currentCaseId;
                List<string> filePathsToDelete = mode == DeleteMode.AppAndFiles
                    ? CollectCaseFilePaths(currentCaseId)
                    : new List<string>();

                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(
                    "DELETE FROM TblCase WHERE CasID = @CasID AND (@CID = 0 OR CenterID = @CID)", con))
                {
                    AddIntParameter(cmd, "@CasID", currentCaseId);
                    cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);

                    con.Open();
                    int affectedRows = cmd.ExecuteNonQuery();

                    if (affectedRows == 0)
                    {
                        Msg.Show("رکورد برای حذف پیدا نشد یا متعلق به مرکز دیگری است");
                        currentCaseId = 0;
                        LoadCases();
                        return;
                    }
                }

                // TblFamily/TblDocs/TblAssistance rows are removed by ON DELETE CASCADE.
                // CASCADE only touches the database rows; فایل‌های فیزیکی فقط وقتی
                // پاک می‌شوند که کاربر گزینه «حذف کامل» را انتخاب کرده باشد.
                foreach (string path in filePathsToDelete)
                    FileHelper.DeleteFileIfExists(path);

                AuditLogger.Log(
                    mode == DeleteMode.AppAndFiles ? "حذف کامل" : "حذف (فقط نرم‌افزار)",
                    "TblCase", deletedCaseId, oldValue, "");

                Msg.Show("رکورد حذف شد");
                LoadCases();
                ClearForm();
            }
            catch (SQLiteException ex)
            {
                Msg.Show("خطا در حذف: " + ex.Message);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در حذف: " + ex.Message);
            }
        }

        private enum DeleteMode { Cancel, AppOnly, AppAndFiles }

        // دیالوگ انتخاب نوع حذف: فقط از نرم‌افزار، یا نرم‌افزار + فایل‌های فیزیکی
        private DeleteMode ShowDeleteModeDialog()
        {
            using (Form form = new Form())
            {
                form.Text = "نوع حذف پرونده";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(430, 210);
                form.RightToLeft = RightToLeft.Yes;
                form.RightToLeftLayout = true;
                form.Font = new Font("Segoe UI", 9.75F);

                Label lbl = new Label();
                lbl.Text = "این پرونده چگونه حذف شود؟";
                lbl.AutoSize = false;
                lbl.SetBounds(20, 20, 390, 40);
                lbl.TextAlign = ContentAlignment.MiddleRight;
                form.Controls.Add(lbl);

                Button btnAppOnly = new Button();
                btnAppOnly.Text = "حذف فقط از نرم‌افزار (فایل‌ها باقی می‌مانند)";
                btnAppOnly.SetBounds(30, 70, 370, 38);
                btnAppOnly.DialogResult = DialogResult.No;
                form.Controls.Add(btnAppOnly);

                Button btnAppAndFiles = new Button();
                btnAppAndFiles.Text = "حذف از نرم‌افزار و حذف فایل‌های فیزیکی";
                btnAppAndFiles.SetBounds(30, 116, 370, 38);
                btnAppAndFiles.DialogResult = DialogResult.Yes;
                form.Controls.Add(btnAppAndFiles);

                Button btnCancel = new Button();
                btnCancel.Text = "انصراف";
                btnCancel.SetBounds(150, 164, 130, 32);
                btnCancel.DialogResult = DialogResult.Cancel;
                form.Controls.Add(btnCancel);

                form.CancelButton = btnCancel;

                DialogResult result = form.ShowDialog(this);

                if (result == DialogResult.Yes)
                    return DeleteMode.AppAndFiles;
                if (result == DialogResult.No)
                    return DeleteMode.AppOnly;
                return DeleteMode.Cancel;
            }
        }

        private List<string> CollectCaseFilePaths(int caseId)
        {
            List<string> paths = new List<string>();

            using (var con = db.GetConnection())
            {
                con.Open();

                using (var cmd = new SQLiteCommand("SELECT PhotoPath, FamilyPhotoPath FROM TblCase WHERE CasID = @CasID", con))
                {
                    AddIntParameter(cmd, "@CasID", caseId);

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            AddIfNotEmpty(paths, GetDbString(dr, "PhotoPath"));
                            AddIfNotEmpty(paths, GetDbString(dr, "FamilyPhotoPath"));
                        }
                    }
                }

                using (var cmd = new SQLiteCommand("SELECT MemberPhotoPath FROM TblFamily WHERE CasID = @CasID", con))
                {
                    AddIntParameter(cmd, "@CasID", caseId);

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                            AddIfNotEmpty(paths, GetDbString(dr, "MemberPhotoPath"));
                    }
                }

                using (var cmd = new SQLiteCommand("SELECT DocFilePath FROM TblDocs WHERE CasID = @CasID", con))
                {
                    AddIntParameter(cmd, "@CasID", caseId);

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                            AddIfNotEmpty(paths, GetDbString(dr, "DocFilePath"));
                    }
                }
            }

            return paths;
        }

        private static void AddIfNotEmpty(List<string> list, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                list.Add(value);
        }

        private string BuildCurrentCaseAuditText()
        {
            return
                "FormNo=" + txtFormNo.Text.Trim() +
                "; Code=" + txtCode.Text.Trim() +
                "; HeadFullName=" + txtHeadFullName.Text.Trim() +
                "; Phone=" + txtPhone.Text.Trim() +
                "; Province=" + txtProvince.Text.Trim() +
                "; District=" + txtDistrict.Text.Trim() +
                "; ServiceStatus=" + NormalizeServiceStatus(txtServiceStatus.Text);
        }

        private string GetCaseAuditTextFromDb(int caseId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT FormNo, Code, HeadFullName, Phone, Province, District, ServiceStatus
FROM TblCase
WHERE CasID = @CasID", con))
            {
                AddIntParameter(cmd, "@CasID", caseId);
                con.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read())
                        return "";

                    return
                        "FormNo=" + GetDbString(dr, "FormNo") +
                        "; Code=" + GetDbString(dr, "Code") +
                        "; HeadFullName=" + GetDbString(dr, "HeadFullName") +
                        "; Phone=" + GetDbString(dr, "Phone") +
                        "; Province=" + GetDbString(dr, "Province") +
                        "; District=" + GetDbString(dr, "District") +
                        "; ServiceStatus=" + GetDbString(dr, "ServiceStatus");
                }
            }
        }

        private string GetCaseStatusById(int caseId)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("SELECT ServiceStatus FROM TblCase WHERE CasID = @CasID", con))
            {
                AddIntParameter(cmd, "@CasID", caseId);
                con.Open();

                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? "" : result.ToString();
            }
        }

        private string GetDbString(System.Data.IDataReader dr, string columnName)
        {
            object value = dr[columnName];
            return value == DBNull.Value ? "" : value.ToString();
        }

        private void SetDatePickerValue(Helpers.PersianDatePicker picker, object value)
        {
            // آموزش — پارس با InvariantCulture (نه Convert.ToDateTime که از
            // کالچر شمسی ترد استفاده می‌کند و سال میلادی ذخیره‌شده را اشتباه
            // تفسیر می‌کرد). جزئیات در PersianDateHelper.ParseStoredDate.
            picker.Value = Helpers.PersianDateHelper.ParseStoredDate(value, DateTime.Today);
        }

        private void LoadCaseFromReader(System.Data.IDataReader dr)
        {
            currentCaseId = Convert.ToInt32(dr["CasID"]);

            txtFormNo.Text = GetDbString(dr, "FormNo");
            txtCode.Text = GetDbString(dr, "Code");
            txtCaseNo.Text = GetDbString(dr, "CaseNo");
            txtZone.Text = GetDbString(dr, "Zone");
            txtProvince.Text = GetDbString(dr, "Province");
            txtDistrict.Items.Clear();
            txtDistrict.Items.AddRange(Helpers.AfghanGeoData.GetDistricts(txtProvince.Text));
            txtDistrict.Text = GetDbString(dr, "District");
            txtRequestType.Text = GetDbString(dr, "RequestType");
            txtPriorityLevel.Text = GetDbString(dr, "PriorityLevel");
            txtHeadFullName.Text = GetDbString(dr, "HeadFullName");
            txtHeadFatherName.Text = GetDbString(dr, "HeadFatherName");
            txtHeadSadat.Text = GetDbString(dr, "HeadSadat");
            txtReligion.Text = GetDbString(dr, "Religion");
            txtHeadTazkiraNo.Text = GetDbString(dr, "HeadTazkiraNo");
            txtHeadOriginalResidence.Text = GetDbString(dr, "HeadOriginalResidence");
            txtHeadCurrentResidence.Text = GetDbString(dr, "HeadCurrentResidence");
            txtRelationshipToFamily.Text = GetDbString(dr, "RelationshipToFamily");
            txtPhone.Text = GetDbString(dr, "Phone");
            txtRelativePhone.Text = GetDbString(dr, "RelativePhone");
            txtCoveredByOrg.Text = GetDbString(dr, "CoveredByOrg");
            txtJob.Text = GetDbString(dr, "Job");
            txtSkill.Text = GetDbString(dr, "Skill");
            txtDisabilityDegree.Text = GetDbString(dr, "DisabilityDegree");
            txtDisabilityType.Text = GetDbString(dr, "DisabilityType");
            // آموزش — وضعیت «سالم/معلول» ستون جدا در دیتابیس ندارد؛ از پرشدن
            // نوع معلولیت استنتاج می‌شود: خالی = سالم (تیک‌خورده)، پرشده = معلول.
            chkHeadHealthy.CheckedChanged -= ChkHeadHealthy_CheckedChanged;
            chkHeadHealthy.Checked = string.IsNullOrWhiteSpace(txtDisabilityType.Text);
            chkHeadHealthy.CheckedChanged += ChkHeadHealthy_CheckedChanged;
            txtDisabilityType.Enabled = !chkHeadHealthy.Checked;
            txtDisabilityDegree.Enabled = !chkHeadHealthy.Checked;
            txtMigrationCardType.Text = GetDbString(dr, "MigrationCardType");
            txtMaritalStatus.Text = GetDbString(dr, "MaritalStatus");
            txtSurveyors.Text = GetDbString(dr, "Surveyors");
            txtLocationAddress.Text = GetDbString(dr, "LocationAddress");
            txtEducationLevel.Text = GetDbString(dr, "EducationLevel");
            SetComboBoxText(txtServiceStatus, GetDbString(dr, "ServiceStatus"));
            txtStopReason.Text = GetDbString(dr, "StopReason");
            UpdateStopReasonVisibility();
            txtUrgentSituation.Text = GetDbString(dr, "UrgentSituation");
            txtPhotoPath.Text = GetDbString(dr, "PhotoPath");
            txtFamilyPhotoPath.Text = GetDbString(dr, "FamilyPhotoPath");

            SetDatePickerValue(dtpCaseDate, dr["CaseDate"]);
            SetDatePickerValue(dtpSurveyDate, dr["SurveyDate"]);

            savedHeadPhotoPath = txtPhotoPath.Text.Trim();
            savedFamilyPhotoPath = txtFamilyPhotoPath.Text.Trim();

            if (savedHeadPhotoPath != "")
                LoadImageToPictureBox(savedHeadPhotoPath, picPhoto);
            else
                ClearPictureBox(picPhoto);

            if (savedFamilyPhotoPath != "")
                LoadImageToPictureBox(savedFamilyPhotoPath, picFamilyPhoto);
            else
                ClearPictureBox(picFamilyPhoto);

            selectedHeadPhotoSource = "";
            selectedFamilyPhotoSource = "";

            txtFormNo.ReadOnly = true;
            txtFormNo.TabStop = false;
            txtCode.Enabled = false;
        }

        // آموزش — رفع باگ امنیتی چندمرکزی: تمام جستجوهای پرونده (با CasID/
        // FormNo/Code) از این یک متد عبور می‌کنند، پس فیلتر @CID اینجا یک‌بار
        // اضافه می‌شود و هیچ مسیر جستجوی فعلی یا آینده نمی‌تواند آن را فراموش
        // کند. قبلاً این جستجوها بدون فیلتر مرکز بودند و یک کاربر مرکز دیگر
        // می‌توانست با حدس کد/شماره فرم، پرونده مراکز دیگر را ببیند/ویرایش کند.
        private bool LoadCaseByQuery(string query, Action<SQLiteCommand> addParameters)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            {
                if (addParameters != null)
                    addParameters(cmd);

                cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);

                con.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read())
                        return false;

                    LoadCaseFromReader(dr);
                    return true;
                }
            }
        }

        private bool LoadCaseById(int caseId)
        {
            return LoadCaseByQuery(
                "SELECT * FROM TblCase WHERE CasID = @CasID AND (@CID = 0 OR CenterID = @CID) LIMIT 1",
                cmd => AddIntParameter(cmd, "@CasID", caseId));
        }

        private bool LoadCaseByCode(string code)
        {
            return LoadCaseByQuery(
                "SELECT * FROM TblCase WHERE Code = @Value AND (@CID = 0 OR CenterID = @CID) LIMIT 1",
                cmd => AddStringParameter(cmd, "@Value", code));
        }

        // آموزش — شماره فرم دیگر قابل تایپ نیست (همیشه اتومات/یکتا/قفل)، پس
        // جستجوی سریع این فرم فقط بر اساس کد اختصاصی است؛ جستجو با شماره فرم
        // از طریق «جستجوی پیشرفته» (که فیلد اختصاصی «شماره فرم» دارد) انجام می‌شود.
        private void btnSearch_Click(object sender, EventArgs e)
        {
            string searchedCode = txtCode.Text.Trim();

            if (searchedCode == "")
            {
                Msg.Show("کد اختصاصی را وارد کنید");
                txtCode.Focus();
                return;
            }

            try
            {
                if (LoadCaseByCode(searchedCode))
                {
                    Msg.Show("رکورد پیدا شد");
                }
                else
                {
                    ClearForm();
                    txtCode.Text = searchedCode;
                    Msg.Show("رکورد پیدا نشد");
                }
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در جستجو: " + ex.Message);
            }
        }

        private void dgvCases_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvCases.Rows.Count)
                return;

            DataGridViewRow row = dgvCases.Rows[e.RowIndex];

            if (row.IsNewRow || !dgvCases.Columns.Contains("CasID"))
                return;

            object value = row.Cells["CasID"].Value;

            if (value == null || value == DBNull.Value)
                return;

            int caseId;

            if (!int.TryParse(value.ToString(), out caseId))
                return;

            try
            {
                if (!LoadCaseById(caseId))
                    Msg.Show("رکورد پیدا نشد");
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در انتخاب رکورد: " + ex.Message);
            }
        }

        private void btnFamily_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا جستجو کن");
                return;
            }

            FrmFamily frm = new FrmFamily();
            frm.CurrentCaseId = currentCaseId;
            frm.CurrentCaseCode = txtCode.Text.Trim();
            frm.ShowDialog();
        }

        private void btnDocs_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا جستجو کن");
                return;
            }

            FrmDocs frm = new FrmDocs();
            frm.CurrentCaseId = currentCaseId;
            frm.CurrentCaseCode = txtCode.Text.Trim();
            frm.ShowDialog();
        }

        private string GetNextFormNo()
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
                SELECT COALESCE(MAX(CAST(CASE WHEN FormNo GLOB '*[0-9]*' AND FormNo NOT GLOB '*[^0-9]*' THEN FormNo ELSE '0' END AS INTEGER)), 0) + 1
                FROM TblCase", con))
            {
                con.Open();

                object result = cmd.ExecuteScalar();
                int next = (result == null || result == DBNull.Value) ? 1 : Convert.ToInt32(result);

                int startCaseNo = SettingsHelper.GetInt(SettingsHelper.StartCaseNo, 0);
                if (startCaseNo > next)
                    next = startCaseNo;

                return next.ToString();
            }
        }

        private string GetWordTemplatePath()
        {
            string templatePath = Path.Combine(Application.StartupPath, "FullCaseTemplate.docx");

            if (!File.Exists(templatePath))
                templatePath = Path.Combine(Application.StartupPath, "Templates", "FullCaseTemplate.docx");

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException(
                    "فایل قالب Word پیدا نشد:" +
                    Environment.NewLine +
                    templatePath);
            }

            return templatePath;
        }

        private string SaveGeneratedFileToCaseCodeFolder(string sourceFilePath)
        {
            return SaveGeneratedFileToCaseCodeFolder(sourceFilePath, txtCode.Text.Trim());
        }

        private string SaveGeneratedFileToCaseCodeFolder(string sourceFilePath, string caseCode)
        {
            string safeCode = CleanFileName(caseCode);

            string savedPath = FileHelper.SaveFileToCaseFolder(
                sourceFilePath,
                caseCode,
                FileHelper.SectionDocs,
                safeCode + "_FullCase",
                "");

            if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
                throw new Exception("فایل خروجی ذخیره نشد: " + FileHelper.LastError);

            return savedPath;
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا از لیست انتخاب کن");
                return;
            }

            var fields = new List<System.Collections.Generic.KeyValuePair<string, string>>
            {
                new System.Collections.Generic.KeyValuePair<string, string>("کد اختصاصی", txtCode.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره فرم", txtFormNo.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره پرونده", txtCaseNo.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("تاریخ تشکیل", CaseManagement.Helpers.PersianDateHelper.ToPersianDateString(dtpCaseDate.Value)),
                new System.Collections.Generic.KeyValuePair<string, string>("زون", txtZone.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("ولایت", txtProvince.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("ولسوالی", txtDistrict.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("نوع درخواست", txtRequestType.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("اولویت‌بندی", txtPriorityLevel.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("نام سرپرست", txtHeadFullName.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("نام پدر سرپرست", txtHeadFatherName.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("سیادت سرپرست", txtHeadSadat.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("مذهب", txtReligion.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره تذکره سرپرست", txtHeadTazkiraNo.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("سکونت اصلی", txtHeadOriginalResidence.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("سکونت فعلی", txtHeadCurrentResidence.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره تماس", txtPhone.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره تماس اقارب", txtRelativePhone.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("تحت پوشش", txtCoveredByOrg.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شغل", txtJob.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("مهارت", txtSkill.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("نوع معلولیت", txtDisabilityType.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("درجه معلولیت", txtDisabilityDegree.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("وضعیت تأهل", txtMaritalStatus.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("تحصیلات", txtEducationLevel.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("وضعیت خدمات", txtServiceStatus.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("دلیل قطع موقت", txtStopReason.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شرح وضعیت فوری", txtUrgentSituation.Text.Trim()),
            };

            PrintHelper.PrintKeyValueDocument(this, "پرونده — " + txtCode.Text.Trim(), fields);
        }

        private void btnExportWord_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا از لیست انتخاب کن");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtCode.Text))
            {
                Msg.Show("کد اختصاصی پرونده مشخص نیست");
                txtCode.Focus();
                return;
            }

            string tempDocx = "";

            try
            {
                string templatePath = GetWordTemplatePath();

                tempDocx = Path.Combine(
                    Path.GetTempPath(),
                    CleanFileName(txtCode.Text.Trim()) + "_" + Guid.NewGuid().ToString("N") + ".docx");

                OpenXmlCaseExporter exporter = new OpenXmlCaseExporter();
                exporter.ExportFullCaseToWord(currentCaseId, templatePath, tempDocx);

                string savedPath = SaveGeneratedFileToCaseCodeFolder(tempDocx);

                Msg.Show(
                    "فایل Word پرونده با موفقیت ذخیره شد:" +
                    Environment.NewLine +
                    savedPath);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ساخت Word: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(tempDocx) && File.Exists(tempDocx))
                        File.Delete(tempDocx);
                }
                catch
                {
                }
            }
        }

        private void btnExportPdf_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا از لیست انتخاب کن");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtCode.Text))
            {
                Msg.Show("کد اختصاصی پرونده مشخص نیست");
                txtCode.Focus();
                return;
            }

            string tempDocx = "";
            string tempPdf = "";

            try
            {
                string templatePath = GetWordTemplatePath();
                string safeCode = CleanFileName(txtCode.Text.Trim());

                tempDocx = Path.Combine(
                    Path.GetTempPath(),
                    safeCode + "_" + Guid.NewGuid().ToString("N") + ".docx");

                OpenXmlCaseExporter exporter = new OpenXmlCaseExporter();
                exporter.ExportFullCaseToWord(currentCaseId, templatePath, tempDocx);

                try
                {
                    tempPdf = ConvertDocxToPdfWithLibreOffice(tempDocx);
                }
                catch (Exception pdfEx)
                {
                    string savedWordPath = SaveGeneratedFileToCaseCodeFolder(tempDocx);

                    Msg.Show(
                        "PDF ساخته نشد." +
                        Environment.NewLine +
                        pdfEx.Message +
                        Environment.NewLine +
                        Environment.NewLine +
                        "اما فایل Word پرونده ذخیره شد:" +
                        Environment.NewLine +
                        savedWordPath);

                    return;
                }

                string savedPdfPath = SaveGeneratedFileToCaseCodeFolder(tempPdf);

                Msg.Show(
                    "فایل PDF با موفقیت ساخته شد:" +
                    Environment.NewLine +
                    savedPdfPath);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ساخت خروجی: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(tempDocx) && File.Exists(tempDocx))
                        File.Delete(tempDocx);

                    if (!string.IsNullOrWhiteSpace(tempPdf) && File.Exists(tempPdf))
                        File.Delete(tempPdf);
                }
                catch
                {
                }
            }
        }

        private async void btnExportExcel_Click(object sender, EventArgs e)
        {
            try
            {
                string rootFolder = FileHelper.GetOrChooseBaseRootFolder();

                if (string.IsNullOrWhiteSpace(rootFolder))
                {
                    Msg.Show("محل ذخیره فایل‌ها مشخص نیست");
                    return;
                }

                string reportsFolder = Path.Combine(rootFolder, "ExcelReports");
                Directory.CreateDirectory(reportsFolder);

                string outputPath = Path.Combine(
                    reportsFolder,
                    "FullExcelReport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".xlsx");

                Cursor oldCursor = Cursor;
                Cursor = Cursors.WaitCursor;
                btnExportExcel.Enabled = false;

                try
                {
                    // آموزش — بخش عملکرد: ساخت گزارش کامل اکسل روی همه پرونده‌ها
                    // با رشد داده می‌تواند چند ثانیه طول بکشد؛ Task.Run از فریز
                    // شدن رابط کاربری در این مدت جلوگیری می‌کند.
                    await Task.Run(() =>
                    {
                        ExcelReportExporter exporter = new ExcelReportExporter();
                        exporter.ExportFullReport(outputPath);
                    });
                }
                finally
                {
                    Cursor = oldCursor;
                    btnExportExcel.Enabled = true;
                }

                Msg.Show(
                    "فایل اکسل کامل با موفقیت ساخته شد:" +
                    Environment.NewLine +
                    outputPath);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ساخت اکسل: " + ex.Message);
            }
        }

        private void cmbServiceStatusFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadCases();
        }

        private bool TryGetBatchExportOptions(
            out int startFormNo,
            out int endFormNo,
            out bool exportWord,
            out bool exportPdf)
        {
            startFormNo = 0;
            endFormNo = 0;
            exportWord = true;
            exportPdf = true;

            using (Form form = new Form())
            using (Label lblStart = new Label())
            using (Label lblEnd = new Label())
            using (TextBox txtStart = new TextBox())
            using (TextBox txtEnd = new TextBox())
            using (CheckBox chkWord = new CheckBox())
            using (CheckBox chkPdf = new CheckBox())
            using (Button btnOk = new Button())
            using (Button btnCancel = new Button())
            {
                form.Text = "خروجی جمعی Word و PDF";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(420, 230);
                form.RightToLeft = RightToLeft.Yes;
                form.RightToLeftLayout = true;

                lblStart.AutoSize = true;
                lblStart.Text = "شروع شماره فرم:";
                lblStart.Location = new Point(285, 30);

                txtStart.Location = new Point(35, 27);
                txtStart.Size = new Size(220, 27);

                lblEnd.AutoSize = true;
                lblEnd.Text = "ختم شماره فرم:";
                lblEnd.Location = new Point(285, 72);

                txtEnd.Location = new Point(35, 69);
                txtEnd.Size = new Size(220, 27);

                chkWord.AutoSize = true;
                chkWord.Text = "ساخت Word";
                chkWord.Checked = true;
                chkWord.Location = new Point(250, 115);

                chkPdf.AutoSize = true;
                chkPdf.Text = "ساخت PDF";
                chkPdf.Checked = true;
                chkPdf.Location = new Point(130, 115);

                btnOk.Text = "شروع خروجی";
                btnOk.Location = new Point(218, 165);
                btnOk.Size = new Size(115, 35);
                btnOk.DialogResult = DialogResult.OK;

                btnCancel.Text = "انصراف";
                btnCancel.Location = new Point(82, 165);
                btnCancel.Size = new Size(95, 35);
                btnCancel.DialogResult = DialogResult.Cancel;

                form.Controls.Add(lblStart);
                form.Controls.Add(txtStart);
                form.Controls.Add(lblEnd);
                form.Controls.Add(txtEnd);
                form.Controls.Add(chkWord);
                form.Controls.Add(chkPdf);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                if (form.ShowDialog(this) != DialogResult.OK)
                    return false;

                if (!int.TryParse(txtStart.Text.Trim(), out startFormNo) ||
                    !int.TryParse(txtEnd.Text.Trim(), out endFormNo))
                {
                    Msg.Show("شماره فرم شروع و ختم باید عدد باشد");
                    return false;
                }

                if (startFormNo <= 0 || endFormNo <= 0)
                {
                    Msg.Show("شماره فرم باید بزرگتر از صفر باشد");
                    return false;
                }

                if (startFormNo > endFormNo)
                {
                    int temp = startFormNo;
                    startFormNo = endFormNo;
                    endFormNo = temp;
                }

                exportWord = chkWord.Checked;
                exportPdf = chkPdf.Checked;

                if (!exportWord && !exportPdf)
                {
                    Msg.Show("حداقل Word یا PDF را انتخاب کنید");
                    return false;
                }

                return true;
            }
        }

        private DataTable GetCasesForBatchExport(int startFormNo, int endFormNo)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT CasID, FormNo, Code
                FROM TblCase
                WHERE CAST(FormNo AS INTEGER) BETWEEN @StartFormNo AND @EndFormNo
                  AND (@CID = 0 OR CenterID = @CID)
                ORDER BY CAST(FormNo AS INTEGER), CasID", con))
            {
                AddIntParameter(cmd, "@StartFormNo", startFormNo);
                AddIntParameter(cmd, "@EndFormNo", endFormNo);
                cmd.Parameters.AddWithValue("@CID", Helpers.SecurityContext.CenterFilterId);

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    return dt;
                }
            }
        }

        private async void btnBatchExport_Click(object sender, EventArgs e)
        {
            int startFormNo;
            int endFormNo;
            bool exportWord;
            bool exportPdf;

            if (!TryGetBatchExportOptions(out startFormNo, out endFormNo, out exportWord, out exportPdf))
                return;

            if (exportPdf)
            {
                try
                {
                    GetLibreOfficePath();
                }
                catch (Exception ex)
                {
                    if (!exportWord)
                    {
                        Msg.Show(ex.Message);
                        return;
                    }

                    DialogResult dr = Msg.Show(
                        "PDF فعلاً ساخته نمی‌شود:" +
                        Environment.NewLine +
                        ex.Message +
                        Environment.NewLine +
                        Environment.NewLine +
                        "آیا فقط Word ساخته شود؟",
                        "خروجی جمعی",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (dr == DialogResult.No)
                        return;

                    exportPdf = false;
                }
            }

            string oldText = btnBatchExport.Text;
            Cursor oldCursor = Cursor;

            int wordCount = 0;
            int pdfCount = 0;
            int errorCount = 0;
            List<string> errors = new List<string>();

            try
            {
                DataTable cases = GetCasesForBatchExport(startFormNo, endFormNo);

                if (cases.Rows.Count == 0)
                {
                    Msg.Show("در این بازه شماره فرم هیچ پرونده‌ای پیدا نشد");
                    return;
                }

                string templatePath = GetWordTemplatePath();
                OpenXmlCaseExporter exporter = new OpenXmlCaseExporter();

                Cursor = Cursors.WaitCursor;
                btnBatchExport.Enabled = false;
                btnBatchExport.Text = "در حال ساخت...";

                // آموزش — بخش عملکرد: این حلقه برای هر پرونده Word می‌سازد و
                // (در صورت انتخاب) LibreOffice را برای تبدیل PDF اجرا می‌کند؛
                // برای چند ده پرونده می‌تواند دقیقه‌ها طول بکشد. اجرای آن روی
                // Task.Run از فریز شدن کل برنامه در این مدت جلوگیری می‌کند.
                bool exportWordLocal = exportWord;
                bool exportPdfLocal = exportPdf;
                await Task.Run(() =>
                {
                    foreach (DataRow row in cases.Rows)
                    {
                        string tempDocx = "";
                        string tempPdf = "";
                        string caseCode = "";
                        string formNo = "";

                        try
                        {
                            int caseId = Convert.ToInt32(row["CasID"]);
                            caseCode = row["Code"] == DBNull.Value ? "" : row["Code"].ToString();
                            formNo = row["FormNo"] == DBNull.Value ? "" : row["FormNo"].ToString();

                            if (string.IsNullOrWhiteSpace(caseCode))
                                throw new Exception("کد اختصاصی پرونده خالی است");

                            tempDocx = Path.Combine(
                                Path.GetTempPath(),
                                CleanFileName(caseCode) + "_" + Guid.NewGuid().ToString("N") + ".docx");

                            exporter.ExportFullCaseToWord(caseId, templatePath, tempDocx);

                            if (exportWordLocal)
                            {
                                SaveGeneratedFileToCaseCodeFolder(tempDocx, caseCode);
                                wordCount++;
                            }

                            if (exportPdfLocal)
                            {
                                tempPdf = ConvertDocxToPdfWithLibreOffice(tempDocx);
                                SaveGeneratedFileToCaseCodeFolder(tempPdf, caseCode);
                                pdfCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add("فرم " + formNo + " / کد " + caseCode + ": " + ex.Message);
                        }
                        finally
                        {
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(tempDocx) && File.Exists(tempDocx))
                                    File.Delete(tempDocx);

                                if (!string.IsNullOrWhiteSpace(tempPdf) && File.Exists(tempPdf))
                                    File.Delete(tempPdf);
                            }
                            catch
                            {
                            }
                        }
                    }
                });

                string message =
                    "خروجی جمعی پایان یافت." +
                    Environment.NewLine +
                    "تعداد پرونده‌ها: " + cases.Rows.Count +
                    Environment.NewLine +
                    "Word ساخته‌شده: " + wordCount +
                    Environment.NewLine +
                    "PDF ساخته‌شده: " + pdfCount +
                    Environment.NewLine +
                    "خطاها: " + errorCount;

                if (errors.Count > 0)
                {
                    message += Environment.NewLine + Environment.NewLine + "چند خطای اول:";

                    int max = Math.Min(errors.Count, 5);
                    for (int i = 0; i < max; i++)
                        message += Environment.NewLine + errors[i];
                }

                Msg.Show(message);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در خروجی جمعی: " + ex.Message);
            }
            finally
            {
                Cursor = oldCursor;
                btnBatchExport.Enabled = true;
                btnBatchExport.Text = oldText;
            }
        }

        // آموزش — به درخواست کاربر: بکاپ‌گیری/بازیابی از این فرم حذف شد و
        // فقط از طریق تب «Backup و Restore» در تنظیمات (FrmSettings، مخصوص
        // SuperAdmin) انجام می‌شود. این هم UI را ساده‌تر می‌کند و هم یک نقطه
        // ناامن اضافه را می‌بندد: این دو دکمه قبلاً بدون هیچ محدودیت نقشی
        // مستقیماً BackupHelper را صدا می‌زدند، در حالی‌که مسیر تنظیمات از قبل
        // به SuperAdmin محدود شده است.

        private string ConvertDocxToPdfWithLibreOffice(string docxPath)
        {
            string libreOfficePath = GetLibreOfficePath();

            string outputFolder = Path.GetDirectoryName(docxPath);
            string expectedPdfPath = Path.Combine(
                outputFolder,
                Path.GetFileNameWithoutExtension(docxPath) + ".pdf");

            if (File.Exists(expectedPdfPath))
                File.Delete(expectedPdfPath);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = libreOfficePath;
            psi.Arguments =
                "--headless --nologo --nofirststartwizard --nolockcheck " +
                "--convert-to pdf " +
                "--outdir \"" + outputFolder + "\" " +
                "\"" + docxPath + "\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += delegate(object outputSender, DataReceivedEventArgs args)
                {
                    if (args.Data != null)
                        output.AppendLine(args.Data);
                };
                process.ErrorDataReceived += delegate(object errorSender, DataReceivedEventArgs args)
                {
                    if (args.Data != null)
                        error.AppendLine(args.Data);
                };

                if (!process.Start())
                    throw new Exception("LibreOffice اجرا نشد.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(120000))
                {
                    try { process.Kill(); } catch { }
                    throw new Exception("زمان ساخت PDF بیش از حد طولانی شد.");
                }

                process.WaitForExit();

                if (!File.Exists(expectedPdfPath))
                    throw new Exception("LibreOffice نتوانست PDF بسازد. " + output + " " + error);
            }

            return expectedPdfPath;
        }

        private string GetLibreOfficePath()
        {
            string[] possiblePaths =
            {
        @"C:\Program Files\LibreOffice\program\soffice.exe",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
    };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            string pathFromEnvironment = FindExecutableInPath("soffice.exe");
            if (!string.IsNullOrWhiteSpace(pathFromEnvironment))
                return pathFromEnvironment;

            throw new Exception(
                "برای ساخت PDF باید LibreOffice نصب باشد." +
                Environment.NewLine +
                "فعلاً می‌توانید از دکمه خروجی Word استفاده کنید." +
                Environment.NewLine +
                "مسیر مورد انتظار:" +
                Environment.NewLine +
                @"C:\Program Files\LibreOffice\program\soffice.exe");
        }

        private string FindExecutableInPath(string executableName)
        {
            string pathValue = Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrWhiteSpace(pathValue))
                return "";

            foreach (string folder in pathValue.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                try
                {
                    string candidate = Path.Combine(folder.Trim(), executableName);

                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            return "";
        }

    }
}

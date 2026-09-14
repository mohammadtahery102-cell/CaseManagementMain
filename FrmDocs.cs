using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using static CaseManagement.Helpers.SqlHelpers;

namespace CaseManagement
{
    public partial class FrmDocs : Form
    {
        private const string DocsSectionName = "Docs";

        private static readonly HashSet<string> ImagePreviewExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

        private const int DocTypeLength = 100;
        private const int OriginalFileNameLength = 255;
        private const int DocFilePathLength = 500;
        private const int RelatedCaseRefLength = 100;
        private const int DocCategoryLength = 100;
        private const int DocTagsLength = 300;

        private readonly DatabaseHelper db = new DatabaseHelper();
        private DataTable allDocsTable;

        public int CurrentCaseId { get; set; } = 0;
        public string CurrentCaseCode { get; set; } = "";

        // آموزش — فاز A4: همان الگوی FrmFamily.IsEmbedded — وقتی این فرم
        // به‌جای پنجرهٔ مستقل داخل تب «اسناد پرونده»ی FrmCase میزبانی می‌شود،
        // FrmCase این پرچم را قبل از Show() روی true می‌گذارد.
        public bool IsEmbedded { get; set; } = false;

        // آموزش — همان delegate پیمایشی که FrmFamily دارد (درخواستِ کاربر:
        // «پنجرهٔ اعضاء خانواده و اسناد دکمهٔ بعدی و قبلی داشته باشد که نظر به
        // شمارهٔ فرم پرونده بالا و پایین برود»). FrmCase موقعِ جاسازی مقدارش
        // را می‌دهد؛ در حالتِ مستقل null می‌ماند و نوارِ دکمه‌ها پنهان است.
        public Func<int, bool> CaseNavigator { get; set; }

        private int currentDocId = 0;
        private string storedDocFilePath = "";
        private string pendingSourceFilePath = "";
        private string pendingOriginalFileName = "";

        // ─── ویژگی ۵ (فعال‌سازی) — قفل رکورد، عیناً همان الگوی FrmFamily ────
        private int  _docLockId = 0;
        private bool _docLockedByOther = false;
        private System.Windows.Forms.Timer _docLockHeartbeat;

        public FrmDocs()
        {
            InitializeComponent();
            ApplyCustomTheme();

            Helpers.FormShortcuts.For(this)
                .Save(btnSave)
                .New(btnNew)
                .Edit(btnEdit)
                .Delete(btnDelete)
                .Print(btnPrint)
                .Bind(Keys.Control | Keys.O, "بازکردن سند", btnOpenDoc);
        }

        // ─── اعمال ظاهر یکسان روی فرمی که با طراح (Designer) ساخته شده ──────
        private void ApplyCustomTheme()
        {
            UiTheme.ApplySweep(this);

            UiTheme.SetButtonIcon(btnNew, "+");
            UiTheme.SetButtonIcon(btnSave, "✔");
            UiTheme.SetButtonIcon(btnEdit, "✎");
            UiTheme.SetButtonIcon(btnDelete, "✕");
            UiTheme.SetButtonIcon(btnBrowseDocFile, "▤");
            UiTheme.SetButtonIcon(btnOpenDoc, "➤");

            btnDelete.BackColor = UiTheme.Danger;
            btnDelete.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Danger, 0.18f);
            btnDelete.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Danger, 0.08f);

            btnSave.BackColor = UiTheme.Success;
            btnSave.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Success, 0.18f);
            btnSave.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Success, 0.08f);

            // آموزش — ApplySweep هر Panel را سفید و هر Label را تیره می‌کند؛
            // نوارِ سرمه‌ایِ بالای فرم (و دکمه‌های پیمایشِ داخلش) باید بعد از
            // آن دوباره رنگ بگیرند — همان کاری که FrmFamily برای lblHeadInfo
            // می‌کند.
            headBarPanel.BackColor = UiTheme.PrimaryDark;
            panCaseNav.BackColor   = UiTheme.PrimaryDark;
            lblHeadInfo.BackColor  = UiTheme.PrimaryDark;
            lblHeadInfo.ForeColor  = Color.White;

            foreach (Button navBtn in new[] { btnPrevCase, btnNextCase })
            {
                navBtn.BackColor = UiTheme.Primary;
                navBtn.ForeColor = Color.White;
                navBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Primary, 0.18f);
                navBtn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Primary, 0.08f);
            }
        }

        // دکمه‌های پیمایش فقط delegate را صدا می‌زنند؛ بارگذاری و رفرش را
        // FrmCase انجام می‌دهد (RefreshForCase از SyncMembersTab صدا می‌شود).
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

        // نوارِ سرِ فرم: کدِ پروندهٔ جاری (هم‌الگوی lblHeadInfo در FrmFamily).
        private void UpdateDocsHeadInfo()
        {
            lblHeadInfo.Text = "اسناد پرونده: " +
                (string.IsNullOrWhiteSpace(CurrentCaseCode) ? "—" : CurrentCaseCode);
        }

        private void FrmDocs_Load(object sender, EventArgs e)
        {
            // آموزش — فاز A4: وقتی embedded است (TopLevel=false داخل تب
            // FrmCase)، قفل‌کردن اندازهٔ پنجره با Dock=Fill داخل تب تداخل
            // می‌کند. در حالت مستقل/مودال رفتار قبلی کاملاً دست‌نخورده می‌ماند
            // (همان بلوکی که قبلاً در FrmDocs.Designer.cs بود، بدون تغییر مقدار).
            if (!IsEmbedded)
            {
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.MaximizeBox = false;
                this.MinimizeBox = true;
                this.WindowState = FormWindowState.Normal;
                this.MinimumSize = this.Size;
                this.MaximumSize = this.Size;
                this.StartPosition = FormStartPosition.CenterScreen;
            }
            else
            {
                // آموزش — همان دلیلِ FrmFamily: داخلِ فضای کاریِ پرونده،
                // «جدید/ذخیره/ویرایش/حذف»ِ سند با همان دکمه‌های *پرونده*
                // اشتباه گرفته می‌شد. عرض هم کمی زیاد می‌شود چون متن بلندتر
                // شده و در ۱۱۰ پیکسل بریده می‌شد.
                btnNew.Text    = "سند جدید";
                btnSave.Text   = "ذخیره سند";
                btnEdit.Text   = "ویرایش سند";
                btnDelete.Text = "حذف سند";

                foreach (Button docBtn in new[] { btnNew, btnSave, btnEdit, btnDelete })
                    docBtn.Size = new Size(132, 38);

                // آیکون‌ها بعد از تغییرِ متن دوباره اعمال می‌شوند (SetButtonIcon
                // خودش تکراری اضافه نمی‌کند).
                UiTheme.SetButtonIcon(btnNew, "+");
                UiTheme.SetButtonIcon(btnSave, "✔");
                UiTheme.SetButtonIcon(btnEdit, "✎");
                UiTheme.SetButtonIcon(btnDelete, "✕");

                // دکمه‌های «پروندهٔ قبلی/بعدی» فقط وقتی معنا دارند که FrmCase
                // میزبان باشد و delegate پیمایش را داده باشد.
                panCaseNav.Visible = CaseNavigator != null;
            }

            UpdateDocsHeadInfo();

            Text = "اسناد" +
                   (string.IsNullOrEmpty(CurrentCaseCode) ? "" : "  —  پرونده: " + CurrentCaseCode) +
                   "  [" + SecurityContext.CenterDisplay + "]";
            txtOriginalFileName.ReadOnly = true;
            txtDocFilePath.ReadOnly = true;

            // Phase 3 — دسته‌بندیِ سند از TblDocumentCategory (نه متنِ آزاد).
            Helpers.ReferenceDataService.FillDocumentCategoryCombo(txtDocCategory);

            // Phase 5.5-A — برچسبِ تأییدکننده با تغییرِ تیک به‌روز می‌شود.
            chkIsVerified.CheckedChanged -= chkIsVerified_CheckedChanged;
            chkIsVerified.CheckedChanged += chkIsVerified_CheckedChanged;

            ConfigureGrid();
            LoadDocs();
            ClearForm();
        }

        // آموزش — فاز A4: مشابه FrmFamily.RefreshForCase — وقتی FrmDocs
        // embedded داخل تب FrmCase است و کاربر پروندهٔ دیگری را انتخاب می‌کند،
        // FrmCase به‌جای ساختِ نمونهٔ تازه، همین متد را روی نمونهٔ موجود صدا می‌زند.
        public void RefreshForCase(int caseId, string caseCode)
        {
            CurrentCaseId = caseId;
            CurrentCaseCode = caseCode;

            Text = "اسناد" +
                   (string.IsNullOrEmpty(CurrentCaseCode) ? "" : "  —  پرونده: " + CurrentCaseCode) +
                   "  [" + SecurityContext.CenterDisplay + "]";

            UpdateDocsHeadInfo();
            LoadDocs();
            ClearForm();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ReleaseDocLock();
            UpdatePreview("");
            base.OnFormClosed(e);
        }

        private void ConfigureGrid()
        {
            dgvDocs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvDocs.MultiSelect = false;
            dgvDocs.ReadOnly = true;
            dgvDocs.AllowUserToAddRows = false;
            dgvDocs.AllowUserToDeleteRows = false;
            GridLayout.Apply(dgvDocs);
        }

        // قفلِ سندِ جاری را آزاد و تایمرِ تمدید را متوقف می‌کند — قبل از
        // بارگذاریِ سندی دیگر، پاک‌کردنِ فرم، یا بستنِ فرم صدا زده می‌شود.
        private void ReleaseDocLock()
        {
            if (_docLockId > 0)
            {
                CaseManagement.Enterprise.LockService.Release(_docLockId);
                _docLockId = 0;
            }

            _docLockedByOther = false;

            if (_docLockHeartbeat != null)
                _docLockHeartbeat.Stop();
        }

        // تلاش برای قفل کردنِ سندِ تازه‌بارگذاری‌شده. اگر توسط کاربر دیگری
        // قفل باشد، داده همچنان برای مشاهده نمایش داده می‌شود؛ خودِ ذخیره
        // (btnEdit_Click) با پرچمِ _docLockedByOther مسدود می‌شود.
        private void TryLockDoc(int docId)
        {
            if (docId <= 0) return;

            CaseManagement.Enterprise.LockResult lockResult =
                CaseManagement.Enterprise.LockService.TryAcquire("TblDocs", docId);

            if (!lockResult.Acquired)
            {
                _docLockedByOther = true;
                Msg.Show(lockResult.DeniedMessage);
                return;
            }

            _docLockId = lockResult.LockID;

            if (_docLockHeartbeat == null)
            {
                _docLockHeartbeat = new System.Windows.Forms.Timer();
                _docLockHeartbeat.Interval = 5 * 60 * 1000; // ۵ دقیقه
                _docLockHeartbeat.Tick += delegate
                {
                    CaseManagement.Enterprise.LockService.Heartbeat(_docLockId);
                };
            }

            _docLockHeartbeat.Start();
        }

        private void ClearForm()
        {
            ReleaseDocLock();
            currentDocId = 0;
            storedDocFilePath = "";
            pendingSourceFilePath = "";
            pendingOriginalFileName = "";

            txtDocType.Text = "";
            txtOriginalFileName.Text = "";
            txtDocFilePath.Text = "";
            txtRelatedCaseRef.Text = "";
            txtDocDescription.Text = "";
            txtDocCategory.SelectedIndex = -1; // Phase 3 — حالا ComboBoxِ DropDownList است
            txtDocTags.Text = "";
            // Phase 5.5-A — وضعیتِ تأیید هم باید با فرم خالی شود، وگرنه تأییدِ
            // سندِ قبلی روی سندِ جدید ثبت می‌شد.
            _loadedIsVerified = false;
            _loadedVerifiedBy = "";
            _loadedVerifiedDate = "";
            chkIsVerified.Checked = false;
            txtVerificationNotes.Text = "";
            RefreshVerificationInfo();
            txtDocNo.Text = CurrentCaseId > 0 ? GetNextDocNo() : "";

            UpdatePreview("");

            txtDocType.Focus();
        }

        // پیش‌نمایش بزرگ سند: اگر عکس باشد نمایش داده می‌شود، در غیر اینصورت خالی می‌ماند
        private void UpdatePreview(string filePath)
        {
            if (picPreview.Image != null)
            {
                var old = picPreview.Image;
                picPreview.Image = null;
                old.Dispose();
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            string ext = Path.GetExtension(filePath);
            if (!ImagePreviewExtensions.Contains(ext))
                return;

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image img = Image.FromStream(fs, false, true))
                    picPreview.Image = new Bitmap(img);
            }
            catch
            {
                // پیش‌نمایش غیرحیاتی است؛ خطا نادیده گرفته می‌شود
            }
        }

        private bool ValidateForm(bool isNewRecord)
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

            if (string.IsNullOrWhiteSpace(txtDocType.Text))
            {
                Msg.Show("نوع سند را وارد کنید");
                txtDocType.Focus();
                return false;
            }

            if (txtDocType.Text.Trim().Length > DocTypeLength)
            {
                Msg.Show("نوع سند نباید بیشتر از 100 کاراکتر باشد");
                txtDocType.Focus();
                return false;
            }

            if (txtRelatedCaseRef.Text.Trim().Length > RelatedCaseRefLength)
            {
                Msg.Show("مرجع مرتبط نباید بیشتر از 100 کاراکتر باشد");
                txtRelatedCaseRef.Focus();
                return false;
            }

            if (txtDocCategory.Text.Trim().Length > DocCategoryLength)
            {
                Msg.Show("دسته‌بندی سند نباید بیشتر از 100 کاراکتر باشد");
                txtDocCategory.Focus();
                return false;
            }

            if (txtDocTags.Text.Trim().Length > DocTagsLength)
            {
                Msg.Show("برچسب‌ها نباید بیشتر از 300 کاراکتر باشد");
                txtDocTags.Focus();
                return false;
            }

            if (isNewRecord && string.IsNullOrWhiteSpace(pendingSourceFilePath))
            {
                Msg.Show("فایل سند را انتخاب کنید");
                return false;
            }

            if (!isNewRecord &&
                string.IsNullOrWhiteSpace(pendingSourceFilePath) &&
                string.IsNullOrWhiteSpace(storedDocFilePath))
            {
                Msg.Show("فایل سند را انتخاب کنید");
                return false;
            }

            return true;
        }

        private bool EnsureCurrentCaseCenter()
        {
            string message;
            if (CenterGuard.TryEnsureCaseAccess(db, CurrentCaseId, out message))
                return true;
            Msg.Show(message);
            return false;
        }

        private void LoadDocs()
        {
            if (CurrentCaseId <= 0)
            {
                dgvDocs.DataSource = null;
                RefreshMissingDocumentsIndicator();
                return;
            }

            if (!EnsureCurrentCaseCenter())
            {
                dgvDocs.DataSource = null;
                RefreshMissingDocumentsIndicator();
                return;
            }

            try
            {
                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(@"
                    SELECT d.DocID, d.DocNo, d.DocType, d.DocCategory, d.DocTags, d.OriginalFileName, d.RelatedCaseRef,
                           c.Code, c.HeadFullName
                    FROM TblDocs d
                    JOIN TblCase c ON c.CasID = d.CasID
                    WHERE d.CasID = @CasID AND d.IsArchived = 0
                    ORDER BY d.DocID DESC", con))
                {
                    AddInt(cmd, "@CasID", CurrentCaseId);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        allDocsTable = new DataTable();
                        allDocsTable.Load(reader);
                        dgvDocs.DataSource = allDocsTable;
                    }
                }

                ApplyGridHeaders();
                ApplySearchFilter();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری اسناد: " + ex.Message);
            }

            // Phase 3 — این تنها نقطه‌ای است که پس از افزودن/ویرایش/حذف سند
            // (و پس از تعویض پرونده) صدا زده می‌شود، پس یک‌بار اینجا کافی است.
            RefreshMissingDocumentsIndicator();

            // پیش از فاز ۴ — اسناد بخشی از درصدِ کامل‌بودنِ پرونده‌اند؛ همین‌جا
            // (نه یک هوکِ جداگانه) دوباره محاسبه و در TblCase کش می‌شود.
            if (CurrentCaseId > 0)
                Helpers.CaseCompletionService.RecalculateAndStore(CurrentCaseId);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 5.5-A — زیرساختِ تأییدِ سند.
        //
        // نام و تاریخِ تأییدکننده *خودکار* از SecurityContext گرفته می‌شود، نه
        // ورودیِ دستی: تأییدی که کاربر بتواند نامِ دیگری برایش بنویسد بی‌ارزش
        // است. با برداشتنِ تیک، هر سه فیلد پاک می‌شوند تا رکوردِ گمراه‌کننده
        // («تأییدنشده ولی تأییدکننده دارد») باقی نماند.
        // ═══════════════════════════════════════════════════════════════════
        private void BindVerificationParameters(SQLiteCommand cmd)
        {
            bool verified = chkIsVerified.Checked;

            cmd.Parameters.AddWithValue("@IsVerified", verified ? 1 : 0);

            if (!verified)
            {
                cmd.Parameters.AddWithValue("@VerifiedBy", DBNull.Value);
                cmd.Parameters.AddWithValue("@VerifiedDate", DBNull.Value);
                cmd.Parameters.AddWithValue("@VerificationNotes", DBNull.Value);
                return;
            }

            // اگر همین سند قبلاً تأیید شده بود، تأییدکننده/تاریخِ اصلی حفظ
            // می‌شود؛ فقط تأییدِ تازه مهرِ کاربرِ فعلی را می‌گیرد.
            cmd.Parameters.AddWithValue("@VerifiedBy",
                string.IsNullOrWhiteSpace(_loadedVerifiedBy)
                    ? (object)(SecurityContext.Username ?? "") : _loadedVerifiedBy);
            cmd.Parameters.AddWithValue("@VerifiedDate",
                string.IsNullOrWhiteSpace(_loadedVerifiedDate)
                    ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) : _loadedVerifiedDate);
            cmd.Parameters.AddWithValue("@VerificationNotes",
                string.IsNullOrWhiteSpace(txtVerificationNotes.Text)
                    ? (object)DBNull.Value : txtVerificationNotes.Text.Trim());
        }

        private void chkIsVerified_CheckedChanged(object sender, EventArgs e)
        {
            RefreshVerificationInfo();
        }

        private string _loadedVerifiedBy = "";
        private string _loadedVerifiedDate = "";
        private bool _loadedIsVerified;

        private void RefreshVerificationInfo()
        {
            if (lblVerifiedInfo == null) return;

            if (chkIsVerified.Checked && !string.IsNullOrWhiteSpace(_loadedVerifiedBy))
            {
                lblVerifiedInfo.Text = "تأییدکننده: " + _loadedVerifiedBy +
                                       "   |   تاریخ تأیید: " +
                                       PersianDateHelper.StoredToPersianDisplay(_loadedVerifiedDate);
                lblVerifiedInfo.ForeColor = UiTheme.Success;
            }
            else if (chkIsVerified.Checked)
            {
                lblVerifiedInfo.Text = "با ذخیره، تأیید به نام «" + (SecurityContext.Username ?? "") + "» ثبت می‌شود.";
                lblVerifiedInfo.ForeColor = UiTheme.TextDark;
            }
            else
            {
                lblVerifiedInfo.Text = "";
            }
        }

        // Phase 3 — نوارِ «اسناد الزامیِ کم است»؛ فقط از RequiredDocumentService
        // می‌خواند (منبعِ واحدِ حقیقت)، هیچ منطقِ تکمیل‌بودنِ دیگری اینجا نیست.
        private void RefreshMissingDocumentsIndicator()
        {
            if (lblMissingDocs == null) return;

            if (CurrentCaseId <= 0)
            {
                lblMissingDocs.Visible = false;
                return;
            }

            try
            {
                var missing = Helpers.RequiredDocumentService.GetMissingRequiredCategories(CurrentCaseId);
                lblMissingDocs.Visible = true;

                if (missing.Count == 0)
                {
                    lblMissingDocs.Text = "✔ همهٔ اسناد الزامی کامل است";
                    lblMissingDocs.ForeColor = UiTheme.Success;
                    lblMissingDocs.BackColor = UiTheme.SuccessLight;
                }
                else
                {
                    var names = missing.ConvertAll(m => m.Name);
                    lblMissingDocs.Text = "⚠ اسناد الزامیِ کم: " + string.Join("، ", names);
                    lblMissingDocs.ForeColor = UiTheme.Warning;
                    lblMissingDocs.BackColor = UiTheme.WarningLight;
                }
            }
            catch
            {
                // آموزش — همانند بقیهٔ نوارهای وضعیت: شکستِ این کوئری هرگز نباید
                // فرم اسناد را غیرقابل استفاده کند.
                lblMissingDocs.Visible = false;
            }
        }

        // آموزش — به درخواست کاربر: گرید اسناد (سمت چپ فرم) فقط سه ستون «نوع
        // سند»، «کد اختصاصی پرونده» و «نام سرپرست خانواده» را نشان می‌دهد؛
        // بقیه ستون‌ها (شناسه/نام فایل اصلی/مرجع مرتبط) پنهان می‌مانند نه حذف
        // — چون DocID برای dgvDocs_CellClick و بقیه منطق هنوز لازم است.
        private void ApplyGridHeaders()
        {
            if (dgvDocs.Columns.Count == 0)
                return;

            if (dgvDocs.Columns.Contains("DocNo"))
                dgvDocs.Columns["DocNo"].HeaderText = "شماره سند";

            if (dgvDocs.Columns.Contains("DocType"))
                dgvDocs.Columns["DocType"].HeaderText = "نوع سند";

            if (dgvDocs.Columns.Contains("DocCategory"))
                dgvDocs.Columns["DocCategory"].HeaderText = "دسته‌بندی";

            if (dgvDocs.Columns.Contains("DocTags"))
                dgvDocs.Columns["DocTags"].HeaderText = "برچسب‌ها";

            if (dgvDocs.Columns.Contains("Code"))
                dgvDocs.Columns["Code"].HeaderText = "کد اختصاصی پرونده";

            if (dgvDocs.Columns.Contains("HeadFullName"))
                dgvDocs.Columns["HeadFullName"].HeaderText = "نام سرپرست خانواده";

            if (dgvDocs.Columns.Contains("DocID"))
                dgvDocs.Columns["DocID"].Visible = false;

            if (dgvDocs.Columns.Contains("OriginalFileName"))
                dgvDocs.Columns["OriginalFileName"].Visible = false;

            if (dgvDocs.Columns.Contains("RelatedCaseRef"))
                dgvDocs.Columns["RelatedCaseRef"].Visible = false;

            if (dgvDocs.Columns.Contains("DocNo"))
                dgvDocs.Columns["DocNo"].DisplayIndex = 0;
            if (dgvDocs.Columns.Contains("DocType"))
                dgvDocs.Columns["DocType"].DisplayIndex = 1;
            if (dgvDocs.Columns.Contains("DocCategory"))
                dgvDocs.Columns["DocCategory"].DisplayIndex = 2;
            if (dgvDocs.Columns.Contains("Code"))
                dgvDocs.Columns["Code"].DisplayIndex = 3;
            if (dgvDocs.Columns.Contains("HeadFullName"))
                dgvDocs.Columns["HeadFullName"].DisplayIndex = 4;
            if (dgvDocs.Columns.Contains("DocTags"))
                dgvDocs.Columns["DocTags"].DisplayIndex = 5;

            GridLayout.Apply(dgvDocs);
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplySearchFilter();
        }

        // فیلتر سریع سمت کلاینت روی نوع/دسته‌بندی/برچسب/نام فایل/مرجع مرتبط —
        // بدون کوئری اضافه به دیتابیس در هر کلیدزنی.
        private void ApplySearchFilter()
        {
            if (allDocsTable == null)
                return;

            string term = EscapeDataViewLike(txtSearch.Text.Trim());

            if (string.IsNullOrEmpty(term))
            {
                allDocsTable.DefaultView.RowFilter = "";
                return;
            }

            allDocsTable.DefaultView.RowFilter =
                "DocType LIKE '%" + term + "%'" +
                " OR DocCategory LIKE '%" + term + "%'" +
                " OR DocTags LIKE '%" + term + "%'" +
                " OR OriginalFileName LIKE '%" + term + "%'" +
                " OR RelatedCaseRef LIKE '%" + term + "%'" +
                " OR DocNo LIKE '%" + term + "%'";
        }

        private void btnBrowseDocFile_Click(object sender, EventArgs e)
        {
            if (CurrentCaseId <= 0 || string.IsNullOrWhiteSpace(CurrentCaseCode))
            {
                Msg.Show("اول پرونده اصلی را ذخیره یا انتخاب کن");
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "انتخاب فایل سند";
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.Filter =
                    "اسناد مجاز|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.txt;*.rtf;*.jpg;*.jpeg;*.png;*.tif;*.tiff|" +
                    "فایل‌های PDF|*.pdf|" +
                    "فایل‌های تصویری|*.jpg;*.jpeg;*.png;*.tif;*.tiff";

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                pendingSourceFilePath = ofd.FileName;
                pendingOriginalFileName = Path.GetFileName(ofd.FileName);

                txtOriginalFileName.Text = pendingOriginalFileName;
                txtDocFilePath.Text = pendingSourceFilePath;
                UpdatePreview(pendingSourceFilePath);
            }
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            ClearForm();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Docs.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه ثبت سند ندارد.");
                return;
            }

            if (!EnsureCurrentCaseCenter())
                return;

            // آموزش — رفع باگ حادّ (از بین رفتن فایل سند): وقتی سندی از گرید
            // انتخاب شده بود (currentDocId > 0) و کاربر به‌جای «ویرایش» دکمه
            // «ذخیره» را می‌زد، فایلِ همان سندِ انتخاب‌شده به‌عنوان «فایل قبلی»
            // به FileHelper پاس می‌شد؛ نتیجه این بود که فایل سندِ قدیمی یا
            // بازنویسی می‌شد یا حذف می‌شد، در حالی‌که ردیف قدیمی در دیتابیس
            // هنوز به همان مسیر اشاره داشت (سند قبلی عملاً از بین می‌رفت).
            // مثل FrmFamily، ثبتِ جدید فقط روی فرمِ خالی مجاز است.
            if (currentDocId > 0)
            {
                Msg.Show("برای ثبت سند جدید ابتدا دکمه جدید را بزنید؛ برای رکورد انتخاب‌شده از دکمه ویرایش استفاده کنید");
                return;
            }

            if (!ValidateForm(true))
                return;

            string savedPath = "";
            bool copiedNewFile = false;

            try
            {
                if (string.IsNullOrWhiteSpace(txtDocNo.Text) || IsDocNoExists(txtDocNo.Text.Trim()))
                {
                    txtDocNo.Text = GetNextDocNo();
                    if (IsDocNoExists(txtDocNo.Text.Trim()))
                    {
                        Msg.Show("شماره سند تکراری است. دوباره دکمه ذخیره را بزنید");
                        return;
                    }
                }

                savedPath = SavePendingFileToCaseFolder("");
                if (string.IsNullOrWhiteSpace(savedPath))
                {
                    Msg.Show("فایل سند ذخیره نشد: " + FileHelper.LastError);
                    return;
                }

                copiedNewFile = !AreSamePath(savedPath, pendingSourceFilePath);

                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO TblDocs
                    (
                        CasID, DocType, OriginalFileName, DocFilePath, RelatedCaseRef, DocDescription,
                        DocCategory, DocumentCategoryID, DocTags, DocNo,
                        IsVerified, VerifiedBy, VerifiedDate, VerificationNotes, GlobalID
                    )
                    VALUES
                    (
                        @CasID, @DocType, @OriginalFileName, @DocFilePath, @RelatedCaseRef, @DocDescription,
                        @DocCategory, @DocumentCategoryID, @DocTags, @DocNo,
                        @IsVerified, @VerifiedBy, @VerifiedDate, @VerificationNotes,
                        lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
                        lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6)))
                    )", con))
                {
                    AddInt(cmd, "@CasID", CurrentCaseId);
                    AddNVarChar(cmd, "@DocType", txtDocType.Text.Trim(), DocTypeLength);
                    AddNVarChar(cmd, "@OriginalFileName", pendingOriginalFileName, OriginalFileNameLength);
                    AddNVarChar(cmd, "@DocFilePath", savedPath, DocFilePathLength);
                    AddNVarChar(cmd, "@RelatedCaseRef", txtRelatedCaseRef.Text.Trim(), RelatedCaseRefLength);
                    AddNVarCharMax(cmd, "@DocDescription", txtDocDescription.Text.Trim());
                    AddNVarChar(cmd, "@DocCategory", txtDocCategory.Text.Trim(), DocCategoryLength);
                    var newDocCategoryRef = Helpers.ReferenceDataService.FindDocumentCategoryByName(txtDocCategory.Text.Trim());
                    cmd.Parameters.AddWithValue("@DocumentCategoryID", newDocCategoryRef != null ? (object)newDocCategoryRef.ID : DBNull.Value);
                    AddNVarChar(cmd, "@DocTags", txtDocTags.Text.Trim(), DocTagsLength);
                    AddNVarChar(cmd, "@DocNo", txtDocNo.Text.Trim(), 50);
                    BindVerificationParameters(cmd);
                    con.Open();
                    cmd.ExecuteNonQuery();
                    // آموزش — رفع نشت resource: این SQLiteCommand قبلاً بدون
                    // Dispose ساخته می‌شد (همان اشکالی که در FrmCase رفع شده).
                    using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", con))
                        currentDocId = Convert.ToInt32((long)idCmd.ExecuteScalar());
                }

                AuditLogger.Log("ثبت", "TblDocs", currentDocId, "", BuildDocAuditText(savedPath, pendingOriginalFileName));

                // صفِ همگام‌سازی — همان الگوی فرم پرونده.
                CaseManagement.Sync.SyncOutboxService.Capture("TblDocs", currentDocId,
                    CaseManagement.Sync.OfflineSyncInitializer.OperationCreate);

                // تاریخچهٔ کاملِ رکورد (عکس فوری همهٔ ستون‌های سند).
                CaseManagement.Enterprise.VersionService.Capture("TblDocs", currentDocId,
                    CaseManagement.Enterprise.VersionService.OperationInsert);

                // Phase 3 — تایم‌لاینِ مرکزیِ پرونده.
                Helpers.TimelineService.LogDocumentAdded(CurrentCaseId, currentDocId,
                    txtDocCategory.Text.Trim(), txtDocType.Text.Trim());

                Msg.Show("سند ذخیره شد");
                LoadDocs();
                ClearForm();
            }
            catch (Exception ex)
            {
                if (copiedNewFile)
                    DeleteStoredFileSafely(savedPath);

                Msg.Show("خطا در ذخیره سند: " + ex.Message);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Docs.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه ویرایش سند ندارد.");
                return;
            }

            if (!EnsureCurrentCaseCenter())
                return;

            if (currentDocId <= 0)
            {
                Msg.Show("اول یک سند را انتخاب کن");
                return;
            }

            if (_docLockedByOther)
            {
                Msg.Show("این سند هم‌اکنون توسط کاربر دیگری در حال ویرایش است. لطفاً بعداً تلاش کنید.");
                return;
            }

            if (!ValidateForm(false))
                return;

            string oldPath = "";
            string finalPath = storedDocFilePath;
            string finalOriginalFileName = txtOriginalFileName.Text.Trim();
            string newlyCopiedPath = "";

            try
            {
                string oldAuditText = GetDocAuditText(currentDocId);
                oldPath = GetStoredDocPath(currentDocId);
                if (oldPath == null)
                {
                    Msg.Show("سند انتخاب‌شده پیدا نشد");
                    LoadDocs();
                    ClearForm();
                    return;
                }

                finalPath = oldPath;

                if (!string.IsNullOrWhiteSpace(pendingSourceFilePath))
                {
                    if (AreSamePath(pendingSourceFilePath, oldPath))
                    {
                        finalPath = oldPath;
                    }
                    else
                    {
                        newlyCopiedPath = SavePendingFileToCaseFolder(oldPath);
                        if (string.IsNullOrWhiteSpace(newlyCopiedPath))
                        {
                            Msg.Show("فایل جدید سند ذخیره نشد: " + FileHelper.LastError);
                            return;
                        }

                        finalPath = newlyCopiedPath;
                    }

                    finalOriginalFileName = pendingOriginalFileName;
                }

                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    UPDATE TblDocs SET
                        DocType = @DocType,
                        OriginalFileName = @OriginalFileName,
                        DocFilePath = @DocFilePath,
                        RelatedCaseRef = @RelatedCaseRef,
                        DocDescription = @DocDescription,
                        DocCategory = @DocCategory,
                        DocumentCategoryID = @DocumentCategoryID,
                        DocTags = @DocTags,
                        IsVerified = @IsVerified,
                        VerifiedBy = @VerifiedBy,
                        VerifiedDate = @VerifiedDate,
                        VerificationNotes = @VerificationNotes
                    WHERE DocID = @DocID AND CasID = @CasID", con))
                {
                    AddNVarChar(cmd, "@DocType", txtDocType.Text.Trim(), DocTypeLength);
                    AddNVarChar(cmd, "@OriginalFileName", finalOriginalFileName, OriginalFileNameLength);
                    AddNVarChar(cmd, "@DocFilePath", finalPath, DocFilePathLength);
                    AddNVarChar(cmd, "@RelatedCaseRef", txtRelatedCaseRef.Text.Trim(), RelatedCaseRefLength);
                    AddNVarCharMax(cmd, "@DocDescription", txtDocDescription.Text.Trim());
                    AddNVarChar(cmd, "@DocCategory", txtDocCategory.Text.Trim(), DocCategoryLength);
                    var editDocCategoryRef = Helpers.ReferenceDataService.FindDocumentCategoryByName(txtDocCategory.Text.Trim());
                    cmd.Parameters.AddWithValue("@DocumentCategoryID", editDocCategoryRef != null ? (object)editDocCategoryRef.ID : DBNull.Value);
                    AddNVarChar(cmd, "@DocTags", txtDocTags.Text.Trim(), DocTagsLength);
                    BindVerificationParameters(cmd);
                    AddInt(cmd, "@DocID", currentDocId);
                    AddInt(cmd, "@CasID", CurrentCaseId);

                    con.Open();

                    int affectedRows = cmd.ExecuteNonQuery();
                    if (affectedRows == 0)
                        throw new InvalidOperationException("هیچ سندی برای ویرایش پیدا نشد.");
                }

                if (!string.IsNullOrWhiteSpace(newlyCopiedPath) && !AreSamePath(oldPath, newlyCopiedPath))
                    DeleteStoredFileSafely(oldPath);

                AuditLogger.Log("ویرایش", "TblDocs", currentDocId, oldAuditText, BuildDocAuditText(finalPath, finalOriginalFileName));

                // Phase 5.5-A — فقط وقتی وضعیتِ تأیید واقعاً عوض شده باشد.
                if (chkIsVerified.Checked != _loadedIsVerified)
                {
                    string docTitle = txtDocCategory.Text.Trim();
                    if (string.IsNullOrWhiteSpace(docTitle)) docTitle = txtDocType.Text.Trim();

                    if (chkIsVerified.Checked)
                        Helpers.TimelineService.LogDocumentVerified(CurrentCaseId, currentDocId,
                            docTitle, txtVerificationNotes.Text.Trim());
                    else
                        Helpers.TimelineService.LogDocumentUnverified(CurrentCaseId, currentDocId, docTitle);

                    _loadedIsVerified = chkIsVerified.Checked;
                }

                CaseManagement.Sync.SyncOutboxService.Capture("TblDocs", currentDocId,
                    CaseManagement.Sync.OfflineSyncInitializer.OperationUpdate);

                CaseManagement.Enterprise.VersionService.Capture("TblDocs", currentDocId,
                    CaseManagement.Enterprise.VersionService.OperationUpdate);

                Msg.Show("سند ویرایش شد");
                LoadDocs();
                ClearForm();
            }
            catch (Exception ex)
            {
                // آموزش — پاکسازی پس از خطا فقط وقتی مجاز است که فایل واقعاً
                // «تازه ساخته‌شده» باشد. اگر FileHelper فایل را روی همان مسیرِ
                // خودِ رکورد جایگزین کرده باشد، حذفِ آن یعنی پاک کردنِ فایلِ
                // سندی که هنوز در دیتابیس زنده است.
                if (!string.IsNullOrWhiteSpace(newlyCopiedPath) && !AreSamePath(newlyCopiedPath, oldPath))
                    DeleteStoredFileSafely(newlyCopiedPath);

                Msg.Show("خطا در ویرایش سند: " + ex.Message);
            }
        }

        // آموزش — مثلِ FrmCase.btnDelete_Click: «حذف» دیگر رکورد یا فایل را
        // واقعاً پاک نمی‌کند، فقط بایگانی می‌کند (IsArchived=1) تا از صفحه‌ی
        // «بایگانی» قابلِ بازگردانی باشد. فایلِ فیزیکی سند دست‌نخورده می‌ماند.
        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Docs.Delete"))
            {
                Msg.Show("بایگانی سند فقط برای مدیر سیستم مجاز است.");
                return;
            }

            if (!EnsureCurrentCaseCenter())
                return;

            if (currentDocId <= 0)
            {
                Msg.Show("اول یک سند را انتخاب کن");
                return;
            }

            DialogResult dr = Msg.Show(
                "این سند بایگانی شود؟ اسناد بایگانی‌شده از این فهرست پنهان می‌شوند.",
                "بایگانی",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr == DialogResult.No)
                return;

            string oldAuditText = GetDocAuditText(currentDocId);
            int archivedDocId = currentDocId;
            string archivedDocCategory = txtDocCategory.Text.Trim();
            string archivedDocType = txtDocType.Text.Trim();

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    UPDATE TblDocs SET
                        IsArchived = 1,
                        ArchivedAt = datetime('now'),
                        ArchivedBy = @ArchivedBy
                    WHERE DocID = @DocID AND CasID = @CasID", con))
                {
                    AddNVarChar(cmd, "@ArchivedBy", SecurityContext.Username, 100);
                    AddInt(cmd, "@DocID", currentDocId);
                    AddInt(cmd, "@CasID", CurrentCaseId);

                    con.Open();

                    int affectedRows = cmd.ExecuteNonQuery();
                    if (affectedRows == 0)
                    {
                        Msg.Show("سند انتخاب‌شده پیدا نشد");
                        LoadDocs();
                        ClearForm();
                        return;
                    }
                }

                AuditLogger.Log("بایگانی", "TblDocs", archivedDocId, oldAuditText, "IsArchived=1");

                // بایگانی یک تغییرِ وضعیت است، نه حذف — پس به‌عنوان ویرایش
                // همگام می‌شود و رکورد در سمت مقابل باقی می‌ماند.
                CaseManagement.Sync.SyncOutboxService.Capture("TblDocs", archivedDocId,
                    CaseManagement.Sync.OfflineSyncInitializer.OperationStatus);

                // بایگانی ویرایشِ ستونِ IsArchived است، پس نسخه هم از نوع «ویرایش»
                // ثبت می‌شود (رکورد حذف نشده و همچنان خواندنی است).
                CaseManagement.Enterprise.VersionService.Capture("TblDocs", archivedDocId,
                    CaseManagement.Enterprise.VersionService.OperationUpdate);

                // Phase 3 — بایگانی از دیدِ تایم‌لاین معادلِ «حذف سند» است
                // (سند دیگر جزوِ اسناد فعال/الزامیِ محاسبه‌شده نیست).
                Helpers.TimelineService.LogDocumentRemoved(CurrentCaseId, archivedDocId,
                    archivedDocCategory, archivedDocType);

                Msg.Show("سند بایگانی شد");
                LoadDocs();
                ClearForm();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بایگانی سند: " + ex.Message);
            }
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Docs.Print"))
            {
                Msg.Show("کاربر اجازه چاپ فهرست اسناد را ندارد.");
                return;
            }

            if (!EnsureCurrentCaseCenter())
                return;

            if (CurrentCaseId <= 0)
            {
                Msg.Show("اول یک پرونده را انتخاب کنید.");
                return;
            }

            // آموزش — چرا دیگر dgvDocs.DataSource مستقیم چاپ نمی‌شود: آن جدول
            // برای گرید ساخته شده (ستونِ داخلیِ DocID، بدونِ دستهٔ واقعیِ سند،
            // بدونِ وضعیتِ فایل). گزارش داده‌اش را خودش می‌خواند؛ از گرید فقط
            // «کدام ردیف‌ها الان دیده می‌شوند» گرفته می‌شود تا فیلترِ جستجوی
            // کاربر در برگهٔ چاپی هم رعایت گردد.
            var visibleIds = new List<int>();
            try
            {
                foreach (DataGridViewRow row in dgvDocs.Rows)
                {
                    if (row.IsNewRow) continue;
                    object value = row.Cells["DocID"].Value;
                    if (value != null && value != DBNull.Value)
                        visibleIds.Add(Convert.ToInt32(value));
                }
            }
            catch { visibleIds.Clear(); }

            if (visibleIds.Count == 0)
            {
                Msg.Show("داده‌ای برای چاپ وجود ندارد");
                return;
            }

            try
            {
                Cursor previous = Cursor;
                Cursor = Cursors.WaitCursor;
                ReportDoc report;
                try
                {
                    report = Helpers.CaseDocumentListReport.Build(
                        db, CurrentCaseId, CurrentCaseCode, visibleIds, txtSearch.Text);
                }
                finally { Cursor = previous; }

                report.Preview(this);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ساخت فهرست چاپی اسناد: " + ex.Message);
            }
        }

        // چرخهٔ فورمِ رسمی: تکمیل در سیستم ← چاپ ← امضا ← اسکن ← ثبت در همین
        // فهرست. پس از ضمیمه‌شدنِ سند، LoadDocs صدا زده می‌شود تا اندیکاتورِ
        // «سندِ مفقود» و درصدِ تکمیلِ پرونده همان لحظه به‌روز شوند — همان
        // قلّابی که FrmDocs از قبل برای هر افزودن/حذفِ سند دارد.
        private void btnOfficialForms_Click(object sender, EventArgs e)
        {
            if (CurrentCaseId <= 0)
            {
                Msg.Show("اول یک پرونده را انتخاب کنید.");
                return;
            }

            Helpers.CaseOfficialForms.ShowMenu(btnOfficialForms, db,
                CurrentCaseId, CurrentCaseCode, LoadDocs);
        }

        private void dgvDocs_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (!dgvDocs.Columns.Contains("DocID"))
                return;

            object idValue = dgvDocs.Rows[e.RowIndex].Cells["DocID"].Value;
            if (idValue == null || idValue == DBNull.Value)
                return;

            int docId = Convert.ToInt32(idValue);
            LoadDocToForm(docId);
        }

        private void dgvDocs_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dgvDocs_CellClick(sender, e);
        }

        private void LoadDocToForm(int docId)
        {
            ReleaseDocLock();

            try
            {
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
                    SELECT DocID, DocType, OriginalFileName, DocFilePath, RelatedCaseRef, DocDescription,
                           DocCategory, DocTags, DocNo,
                           IsVerified, VerifiedBy, VerifiedDate, VerificationNotes
                    FROM TblDocs
                    WHERE DocID = @DocID AND CasID = @CasID", con))
                {
                    AddInt(cmd, "@DocID", docId);
                    AddInt(cmd, "@CasID", CurrentCaseId);

                    con.Open();

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read())
                            return;

                        currentDocId = Convert.ToInt32(dr["DocID"]);
                        storedDocFilePath = DbString(dr["DocFilePath"]);

                        pendingSourceFilePath = "";
                        pendingOriginalFileName = "";

                        txtDocType.Text = DbString(dr["DocType"]);
                        txtOriginalFileName.Text = DbString(dr["OriginalFileName"]);
                        txtDocFilePath.Text = storedDocFilePath;
                        txtRelatedCaseRef.Text = DbString(dr["RelatedCaseRef"]);
                        txtDocDescription.Text = DbString(dr["DocDescription"]);
                        txtDocCategory.Text = DbString(dr["DocCategory"]);
                        txtDocTags.Text = DbString(dr["DocTags"]);
                        txtDocNo.Text = DbString(dr["DocNo"]);

                        // Phase 5.5-A — وضعیتِ تأیید.
                        _loadedIsVerified = dr["IsVerified"] != DBNull.Value &&
                                            Convert.ToInt32(dr["IsVerified"]) != 0;
                        _loadedVerifiedBy = DbString(dr["VerifiedBy"]);
                        _loadedVerifiedDate = DbString(dr["VerifiedDate"]);
                        chkIsVerified.Checked = _loadedIsVerified;
                        txtVerificationNotes.Text = DbString(dr["VerificationNotes"]);
                        RefreshVerificationInfo();

                        UpdatePreview(storedDocFilePath);
                    }
                }

                TryLockDoc(currentDocId);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در انتخاب سند: " + ex.Message);
            }
        }

        private void btnOpenDoc_Click(object sender, EventArgs e)
        {
            string path = txtDocFilePath.Text.Trim();

            if (string.IsNullOrWhiteSpace(path))
            {
                Msg.Show("مسیر فایل خالی است");
                return;
            }

            if (!File.Exists(path))
            {
                Msg.Show("فایل پیدا نشد");
                return;
            }

            if (!IsAllowedToOpenPath(path))
            {
                Msg.Show("باز کردن این مسیر مجاز نیست");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در باز کردن فایل: " + ex.Message);
            }
        }

        // آموزش — «مسیر فایل قبلی» صریحاً پاس داده می‌شود (قبلاً همیشه از
        // storedDocFilePath خوانده می‌شد). مسیرِ ثبتِ جدید باید رشته خالی بدهد
        // تا FileHelper هرگز فایل سندِ دیگری را جایگزین/حذف نکند؛ مسیر ویرایش
        // همان فایلِ خودِ رکورد را می‌دهد که رفتار درست و قبلی است.
        private string SavePendingFileToCaseFolder(string existingStoredPath)
        {
            if (string.IsNullOrWhiteSpace(pendingSourceFilePath))
                return "";

            if (string.IsNullOrWhiteSpace(CurrentCaseCode))
            {
                Msg.Show("کد اختصاصی پرونده مشخص نیست");
                return "";
            }

            if (!File.Exists(pendingSourceFilePath))
            {
                Msg.Show("فایل انتخاب‌شده پیدا نشد");
                return "";
            }

            string cleanCode = FileHelper.CleanName(CurrentCaseCode.Trim());

            // Phase 3 — نامِ فایلِ استانداردِ انگلیسی: به‌جای نوعِ سندِ فارسیِ
            // آزاد، از کدِ انگلیسیِ دستهٔ انتخاب‌شده (اگر باشد) + شمارهٔ سند
            // استفاده می‌شود. FileHelper.SaveFileToCaseFolder و منطقِ
            // جایگزینی/حذفِ فایلِ قبلی کاملاً دست‌نخورده می‌مانند — فقط نامِ
            // پایه تغییر کرده است.
            var selectedCategory = Helpers.ReferenceDataService.FindDocumentCategoryByName(txtDocCategory.Text.Trim());
            string baseFileName = selectedCategory != null
                ? cleanCode + "-" + selectedCategory.Code + "-" + txtDocNo.Text.Trim()
                : cleanCode + "-" + FileHelper.CleanName(txtDocType.Text.Trim());

            string savedPath = FileHelper.SaveFileToCaseFolder(
                pendingSourceFilePath,
                CurrentCaseCode,
                FileHelper.SectionDocs,
                baseFileName,
                existingStoredPath ?? "");

            if (string.IsNullOrWhiteSpace(savedPath))
                return "";

            if (savedPath.Length > DocFilePathLength)
            {
                DeleteStoredFileSafely(savedPath);
                Msg.Show("مسیر فایل سند بیش از حد طولانی است");
                return "";
            }

            return savedPath;
        }

        private string GetStoredDocPath(int docId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
                SELECT COALESCE(DocFilePath, '')
                FROM TblDocs
                WHERE DocID = @DocID AND CasID = @CasID", con))
            {
                AddInt(cmd, "@DocID", docId);
                AddInt(cmd, "@CasID", CurrentCaseId);

                con.Open();

                // null یعنی ردیفی وجود ندارد؛ DocFilePath خالی/NULL یک حالت
                // معتبر است و نباید «سند پیدا نشد» تلقی شود (وگرنه ویرایشِ
                // چنین سندی هرگز انجام نمی‌شد).
                object result = cmd.ExecuteScalar();
                if (result == null)
                    return null;

                if (result == DBNull.Value)
                    return "";

                return result.ToString();
            }
        }

        // شماره‌گذاری خودکار سند — دقیقاً همان الگوی GetNextFormNo در FrmCase.cs
        // (MAX عددی + 1) تا با روش موجود در پروژه یکسان بماند.
        private string GetNextDocNo()
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
                SELECT COALESCE(MAX(CAST(CASE WHEN DocNo GLOB '*[0-9]*' AND DocNo NOT GLOB '*[^0-9]*' THEN DocNo ELSE '0' END AS INTEGER)), 0) + 1
                FROM TblDocs", con))
            {
                con.Open();

                object result = cmd.ExecuteScalar();
                int next = (result == null || result == DBNull.Value) ? 1 : Convert.ToInt32(result);

                return next.ToString();
            }
        }

        private bool IsDocNoExists(string docNo)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT COUNT(1) FROM TblDocs WHERE DocNo = @DocNo", con))
            {
                AddNVarChar(cmd, "@DocNo", docNo, 50);
                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private string GetDocAuditText(int docId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT DocType, OriginalFileName, DocFilePath, RelatedCaseRef, DocCategory, DocTags, DocNo
FROM TblDocs
WHERE DocID = @DocID AND CasID = @CasID", con))
            {
                AddInt(cmd, "@DocID", docId);
                AddInt(cmd, "@CasID", CurrentCaseId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read())
                        return "";

                    return
                        "DocType=" + DbString(dr["DocType"]) +
                        "; OriginalFileName=" + DbString(dr["OriginalFileName"]) +
                        "; DocFilePath=" + DbString(dr["DocFilePath"]) +
                        "; RelatedCaseRef=" + DbString(dr["RelatedCaseRef"]) +
                        "; DocCategory=" + DbString(dr["DocCategory"]) +
                        "; DocTags=" + DbString(dr["DocTags"]) +
                        "; DocNo=" + DbString(dr["DocNo"]);
                }
            }
        }

        private string BuildDocAuditText(string filePath, string originalFileName)
        {
            return
                "DocType=" + txtDocType.Text.Trim() +
                "; OriginalFileName=" + (originalFileName ?? "") +
                "; DocFilePath=" + (filePath ?? "") +
                "; RelatedCaseRef=" + txtRelatedCaseRef.Text.Trim() +
                "; DocCategory=" + txtDocCategory.Text.Trim() +
                "; DocTags=" + txtDocTags.Text.Trim() +
                "; DocNo=" + txtDocNo.Text.Trim();
        }

        private bool IsAllowedToOpenPath(string path)
        {
            if (!string.IsNullOrWhiteSpace(pendingSourceFilePath) && AreSamePath(path, pendingSourceFilePath))
                return true;

            return IsStoredFilePathAllowed(path);
        }

        private bool IsStoredFilePathAllowed(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string docsFolder = FileHelper.GetSectionFolder(CurrentCaseCode, DocsSectionName);
            if (string.IsNullOrWhiteSpace(docsFolder))
                return false;

            return IsPathInsideFolder(path, docsFolder);
        }

        private void DeleteStoredFileSafely(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!IsStoredFilePathAllowed(path))
                return;

            FileHelper.DeleteFileIfExists(path);
        }

    }
}

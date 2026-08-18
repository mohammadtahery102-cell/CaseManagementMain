using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
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

        // آموزش — فاز ۱ (تب اعضاء داخل FrmCase): نمونهٔ تک‌عمرِ FrmFamily که
        // به‌جای پنجرهٔ مودالِ قبلی، داخل تب «اعضاء خانواده» جاسازی می‌شود.
        // فقط بار اول که کاربر روی آن تب می‌رود ساخته می‌شود (EnsureFamilyEmbedded)
        // و بعدش تا بسته‌شدنِ FrmCase زنده می‌ماند؛ با تعویض پرونده فقط
        // RefreshForCase صدا زده می‌شود، نه ساخت نمونهٔ تازه.
        private FrmFamily _embeddedFamily;

        // آموزش — رفع بارگذاری تکراری: true یعنی «دیتای نمونهٔ embedded با
        // currentCaseId فعلی هم‌خوان نیست». SyncMembersTab وقتی تب دیده
        // نمی‌شود به‌جای رفرش فوری فقط این پرچم را می‌زند؛ EnsureFamilyEmbedded
        // وقتی کاربر واقعاً به تب می‌رود، فقط اگر این پرچم ست باشد رفرش
        // می‌کند. بدون این پرچم، هم تعویضِ سادهٔ تب (بدون تغییر پرونده) و هم
        // تعویضِ پرونده از تب‌های دیگر، هرکدام یک کوئری اضافهٔ نامرئی می‌زدند.
        private bool _familyDirty = false;

        // آموزش — فاز A4: همان الگوی _embeddedFamily/_familyDirty، برای تب
        // «اسناد پرونده» (FrmDocs embedded).
        private FrmDocs _embeddedDocs;
        private bool _docsDirty = false;

        private string selectedHeadPhotoSource = "";
        private string selectedFamilyPhotoSource = "";

        private string savedHeadPhotoPath = "";
        private string savedFamilyPhotoPath = "";

        // منبع واحد: از TblLookup (دسته ServiceStatus) خوانده می‌شود.
        // اگر دیتابیس هنوز آماده نباشد LookupHelper فهرست خالی برمی‌گرداند؛
        // در آن حالت به فهرست مرجعِ CaseDomain برمی‌گردیم، وگرنه کمبو خالی
        // می‌ماند و IsAllowedServiceStatus هر مقداری را رد می‌کند (ذخیره قفل می‌شود).
        private string[] serviceStatuses
        {
            get
            {
                string[] fromDb = Helpers.LookupHelper.GetValues(Helpers.CaseDomain.CatServiceStatus).ToArray();
                return fromDb.Length > 0 ? fromDb : Helpers.CaseDomain.ServiceStatuses;
            }
        }

        private const long MinFamilyPhotoFileSizeBytes = 50L * 1024;
        private const long MaxFamilyPhotoFileSizeBytes = 1L * 1024 * 1024;

        private int _pendingOpenCaseId = 0;

        // آموزش — فیلترِ واردشده از داشبورد (ولایت/ولسوالی/وضعیت خدمات): وقتی
        // کاربر از داشبورد با یک فیلترِ فعال روی دکمه‌ی «پرونده‌ها» می‌زند،
        // همان فیلتر اینجا اعمال می‌شود — هم روی فهرستِ پیش‌فرض (LoadCases)
        // هم روی جستجوی نوارِ بالای گرید (SearchCasesGrid)، تا کاربر مجبور
        // نباشد دوباره همان فیلتر را تکرار کند.
        private string _incomingFilterProvince = "";
        private string _incomingFilterDistrict = "";
        private string _incomingFilterServiceStatus = "";
        private Panel _dashboardFilterBanner;

        public FrmCase()
        {
            InitializeComponent();
            ApplyCustomTheme();
            AttachShortcuts();
        }

        // باز کردن با همان فیلترِ فعالِ داشبورد (ولایت/ولسوالی/وضعیت خدمات).
        // مقدارِ خالی یعنی «بدون فیلتر»، دقیقاً مثلِ رفتارِ پیش‌فرضِ داشبورد.
        public FrmCase(string filterProvince, string filterDistrict, string filterServiceStatus) : this()
        {
            _incomingFilterProvince = filterProvince ?? "";
            _incomingFilterDistrict = filterDistrict ?? "";
            _incomingFilterServiceStatus = filterServiceStatus ?? "";
        }

        // میان‌بُرهای صفحه‌کلید. Enter (رفتن به فیلد بعدی) جداگانه در
        // FrmCase_KeyDown می‌ماند و دست‌نخورده است.
        private void AttachShortcuts()
        {
            Helpers.FormShortcuts.For(this)
                .Save(btnSave)
                .New(btnNew)
                .Edit(btnEdit)
                .Delete(btnDelete)
                .Search(btnSearch)
                .Print(btnPrint);
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

        // ─── ستون‌های واقعیِ جستجو در TblCase — منبعِ واحد، هم برای نوار
        // جستجوی سریعِ بالای فرم (Quick Search Bar، فاز A2) و هم هرجای دیگری
        // که قبلاً از همین آرایه استفاده می‌کرد.
        private static readonly string[] CaseSearchTypeColumns =
            { "Code", "HeadFullName", "HeadTazkiraNo", "Phone" };

        // ─── مقیدکردن عرضِ نوار دکمه‌ها به عرضِ والد ───────────────────────────
        // آموزش — چرا لازم است: نوار دکمه‌ها AutoSize دارد تا وقتی دکمه‌ها به خط
        // بعد می‌شکنند ارتفاعش زیاد شود و هیچ دکمه‌ای پنهان نماند. اما AutoSize
        // در FlowLayoutPanel هر دو بُعد را بزرگ می‌کند؛ یعنی به‌جای شکستنِ خط،
        // خودِ نوار در عرض رشد می‌کرد و دکمه‌های انتهایی از لبه‌ی فرم بیرون
        // می‌زدند (در تستِ تصویری «چاپ جمعی کارت‌ها» دقیقاً همین‌طور بریده شد).
        // با تعیین MaximumSize.Width برابرِ عرضِ والد، رشدِ افقی متوقف می‌شود و
        // AutoSize فقط ارتفاع را تنظیم می‌کند — یعنی شکستِ خط درست کار می‌کند.
        private void ConstrainBottomActionsWidth()
        {
            AdjustBottomBarHeight();
        }

        // ارتفاعِ نوار دکمه‌ها را با «تعدادِ خطوطی که واقعاً اشغال کرده‌اند»
        // تطبیق می‌دهد. چون Dock=Fill عرض را به والد مقید می‌کند، شکستِ خط درست
        // انجام می‌شود؛ فقط باید ردیفِ نگه‌دارنده به‌اندازه‌ی کافی بلند باشد.
        // این کار در هر تغییر اندازه انجام می‌شود، پس روی هر عرض/رزولوشنی
        // (و با هر تعداد دکمه‌ای که در آینده اضافه شود) هیچ دکمه‌ای پنهان نمی‌ماند.
        private void AdjustBottomBarHeight()
        {
            if (bottomActionsRow == null || rootLayout == null) return;
            // آموزش — فاز A2: با افزودنِ ردیفِ جدیدِ «نوار جستجوی سریع» در بالای
            // rootLayout، نوار دکمه‌ها از ردیفِ اندیس ۲ به ۳ منتقل شد. این
            // اندیس اینجا (و شرطِ Count) همراهش به‌روز شد تا این متد روی ردیفِ
            // درست کار کند — وگرنه بی‌سروصدا روی ردیفِ فیلدها/گرید اثر می‌گذاشت.
            if (rootLayout.RowStyles.Count < 4) return;

            int contentBottom = 0;
            foreach (Control c in bottomActionsRow.Controls)
            {
                int b = c.Bottom + c.Margin.Bottom;
                if (b > contentBottom) contentBottom = b;
            }
            if (contentBottom <= 0) return;

            float needed = contentBottom + bottomActionsRow.Padding.Bottom + 4;
            if (needed < 52f) needed = 52f;   // حداقلِ یک خط

            RowStyle bottomRow = rootLayout.RowStyles[3];
            if (bottomRow.SizeType != SizeType.Absolute || Math.Abs(bottomRow.Height - needed) > 1f)
            {
                bottomRow.SizeType = SizeType.Absolute;
                bottomRow.Height = needed;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            AdjustBottomBarHeight();
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
                // آموزش — فاز A2: چون فرم KeyPreview=true دارد، این متد قبل از
                // KeyDown خودِ کنترلِ فوکوس‌شده اجرا می‌شود. برای چهار فیلدِ نوار
                // جستجوی سریع، Enter باید جستجو را اجرا کند (QuickSearchField_KeyDown)
                // نه اینکه فوکوس را به فیلد بعدی ببرد؛ پس رفتار سراسریِ زیر
                // برای آن‌ها رد می‌شود. از Focused (نه ActiveControl) استفاده شد
                // چون این فیلدها داخل چند لایه Panel/TableLayoutPanel تودرتو
                // هستند و ActiveControl فرم لزوماً کنترلِ واقعیِ تودرتو را برنمی‌گرداند.
                if (txtQsCode.Focused || txtQsHeadName.Focused || txtQsTazkira.Focused || txtQsPhone.Focused)
                    return;

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
            txtCoveredByOrgNames.TabIndex = c++;
            dtpCaseDate.TabIndex = c++;
            txtServiceStatus.TabIndex = c++;
            txtSuspensionReason.TabIndex = c++;
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

            // نمایش/پنهان‌کردن «اسامی مؤسسات تحت پوشش» با تغییر پاسخِ بله/خیر.
            txtCoveredByOrg.SelectedIndexChanged += delegate { UpdateCoveredByOrgNamesVisibility(); };
            UpdateCoveredByOrgNamesVisibility();
        }

        // فیلدهای تعلیق («دلیل تعلیق» الزامی + «یادداشت» اختیاری) فقط وقتی
        // وضعیت «قطع» یا «قطع موقت» است نمایش داده می‌شوند.
        private void TxtServiceStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateStopReasonVisibility();
        }

        private bool IsSuspendedStatus(string normalizedStatus)
        {
            return normalizedStatus == "قطع موقت" || normalizedStatus == "قطع";
        }

        private void UpdateStopReasonVisibility()
        {
            bool isSuspended = IsSuspendedStatus(NormalizeServiceStatus(txtServiceStatus.Text));

            lblStopReason.Visible = isSuspended;
            txtStopReason.Visible = isSuspended;
            if (caseFieldStopReason != null) caseFieldStopReason.Visible = isSuspended;

            lblSuspensionReason.Visible = isSuspended;
            txtSuspensionReason.Visible = isSuspended;
            if (caseFieldSuspensionReason != null) caseFieldSuspensionReason.Visible = isSuspended;

            if (!isSuspended)
            {
                txtStopReason.Text = "";
                txtSuspensionReason.SelectedIndex = -1;
            }
        }

        // «اسامی مؤسسات تحت پوشش» فقط وقتی معنی دارد که پاسخِ «تحت پوشش دیگر
        // مؤسسات» بله باشد — همان الگوی «دلیل قطع موقت» بالا. با انتخاب «خیر»
        // (یا خالی) کادر پنهان و محتوایش پاک می‌شود تا داده‌ی متناقض ذخیره نشود.
        private void UpdateCoveredByOrgNamesVisibility()
        {
            bool isCovered = txtCoveredByOrg.Text.Trim() == "بله";

            lblCoveredByOrgNames.Visible = isCovered;
            txtCoveredByOrgNames.Visible = isCovered;
            if (fieldCoveredByOrgNames != null)
                fieldCoveredByOrgNames.Visible = isCovered;

            if (!isCovered)
                txtCoveredByOrgNames.Text = "";
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

        // نگاشتِ مقادیرِ قدیمی حالا در Helpers.CaseDomain متمرکز است تا فرم،
        // ایمپورتِ اکسل و سینک همگی یک تعریف داشته باشند.
        private string NormalizeServiceStatus(string value)
        {
            value = (value ?? "").Trim();

            if (value == "")
                return Helpers.CaseDomain.StatusActive;

            return Helpers.CaseDomain.NormalizeServiceStatus(value);
        }

        // مقدار فیلترِ «وضعیت خدمات» گرید — رشته‌ی خالی یعنی «همه». هم گرید و هم
        // خروجی‌های جمعی (اکسل/چاپ کارت/خروجی بازه‌ی فرم) از همین یک منبع
        // می‌خوانند تا گزارش با آنچه کاربر روی صفحه می‌بیند یکی باشد.
        private string GetSelectedServiceStatusFilter()
        {
            if (cmbServiceStatusFilter == null ||
                cmbServiceStatusFilter.SelectedItem == null ||
                cmbServiceStatusFilter.Text.Trim() == "همه")
            {
                return "";
            }

            return NormalizeServiceStatus(cmbServiceStatusFilter.Text);
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
                            WHEN ServiceStatus IN ('درانتظار', 'در انتظار', 'در انتظار تأیید') THEN 'در انتظار تایید'
                            ELSE ServiceStatus
                        END
                    WHERE ServiceStatus IN ('در حالت قطع', 'درانتظار', 'در انتظار', 'در انتظار تأیید')", con))
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
            // آموزش — FitToScreen اندازه‌ی پنجره را با ناحیه‌ی کاریِ صفحه تطبیق
            // می‌دهد (برای نمایشگرهای کوچک/مقیاس‌دار)؛ سپس MakeMainWindow همان
            // اندازه را به‌عنوان «حداقل» قفل می‌کند و پنجره را تمام‌صفحه می‌کند.
            // ترتیب مهم است: اول تطبیق با صفحه، بعد قفلِ حداقل.
            FitToScreen();
            UiTheme.MakeMainWindow(this, ClientSize.Width, ClientSize.Height);
            ConstrainBottomActionsWidth();

            // ستون‌های گرید با برگشتن به فرم دوباره بررسی می‌شوند تا تغییرِ
            // تنظیمات بدون بستن/باز کردن فرم اعمال شود.
            Activated -= FrmCase_Activated;
            Activated += FrmCase_Activated;

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
            // آموزش — افزودنِ شماره‌ی ردیف (خواسته‌ی صریح: «شماره ردیف در گرید
            // موجود نیست»): بدون ستونِ دیتاییِ اضافه، فقط رسمِ عدد در ناحیه‌ی
            // RowHeader — بدون هزینه‌ی کوئری/بایندینگِ اضافه.
            dgvCases.RowPostPaint -= DgvCases_RowPostPaint;
            dgvCases.RowPostPaint += DgvCases_RowPostPaint;
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
            ApplyIncomingDashboardFilter();
            LoadCases();
            ClearForm();

            // فرم در حالتِ «فقط نمایش» باز می‌شود؛ برای پروندهٔ جدید کاربر
            // «جدید» و برای تغییرِ پروندهٔ موجود «ویرایش» را می‌زند.
            SetCaseEditMode(false);

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
            Helpers.LookupHelper.FillCombo(txtSuspensionReason, "SuspensionReason");
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
            txtCoveredByOrgNames.Text = "";
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
            txtSuspensionReason.SelectedIndex = -1;
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

            // بعد از خالی‌شدن کمبوی «تحت پوشش»، کادر اسامی هم باید پنهان شود.
            UpdateCoveredByOrgNamesVisibility();

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

            SyncMembersTab();
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

            if (IsSuspendedStatus(NormalizeServiceStatus(txtServiceStatus.Text)) &&
                string.IsNullOrWhiteSpace(txtSuspensionReason.Text))
            {
                Msg.Show("دلیل تعلیق را از لیست انتخاب کنید");
                txtSuspensionReason.Focus();
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
            // اسامی فقط وقتی ذخیره می‌شود که پاسخ «بله» باشد؛ در غیر این صورت
            // خالی ثبت می‌شود تا داده‌ی متناقض («خیر» ولی با اسامی) نماند.
            AddStringParameter(cmd, "@CoveredByOrgNames",
                txtCoveredByOrg.Text.Trim() == "بله" ? txtCoveredByOrgNames.Text.Trim() : "");
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
            bool isSuspended = IsSuspendedStatus(NormalizeServiceStatus(txtServiceStatus.Text));
            AddStringParameter(cmd, "@ServiceStatus", NormalizeServiceStatus(txtServiceStatus.Text));
            AddStringParameter(cmd, "@StopReason", isSuspended ? txtStopReason.Text.Trim() : "");
            AddStringParameter(cmd, "@SuspensionReason", isSuspended ? txtSuspensionReason.Text.Trim() : "");
            AddStringParameter(cmd, "@UrgentSituation", txtUrgentSituation.Text.Trim());
            AddStringParameter(cmd, "@PhotoPath", txtPhotoPath.Text.Trim());
            AddStringParameter(cmd, "@FamilyPhotoPath", txtFamilyPhotoPath.Text.Trim());
        }

        // مقادیرِ «مُهرِ تعلیق» را برای درج/به‌روزرسانی آماده می‌کند: اگر وضعیتِ
        // فعلیِ فرم معلق است، تاریخ/کاربرِ اکنون؛ در غیر این صورت خالی (پاک
        // می‌شود). برای UPDATE، عبارتِ CASE در متنِ کوئری تصمیم می‌گیرد که آیا
        // این مقدارِ تازه واقعاً نوشته شود یا مقدارِ قبلیِ ستون حفظ شود (فقط
        // وقتی وضعیت واقعاً عوض شده باشد بازنویسی می‌شود).
        private void AddSuspensionStampParameters(SQLiteCommand cmd)
        {
            bool isSuspended = IsSuspendedStatus(NormalizeServiceStatus(txtServiceStatus.Text));

            if (isSuspended)
            {
                AddDateParameter(cmd, "@SuspensionDate", DateTime.Now.Date);
                cmd.Parameters.AddWithValue("@SuspendedByUserId",
                    Helpers.SecurityContext.UserId > 0 ? (object)Helpers.SecurityContext.UserId : DBNull.Value);
                AddStringParameter(cmd, "@SuspendedByUsername", Helpers.SecurityContext.Username);
            }
            else
            {
                cmd.Parameters.AddWithValue("@SuspensionDate", DBNull.Value);
                cmd.Parameters.AddWithValue("@SuspendedByUserId", DBNull.Value);
                cmd.Parameters.AddWithValue("@SuspendedByUsername", DBNull.Value);
            }
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

        // اعمالِ فیلترِ واردشده از داشبورد: وضعیتِ خدمات روی همان کمبویِ
        // موجودِ فرم (cmbServiceStatusFilter) گذاشته می‌شود تا از همان مکانیزمِ
        // موجود استفاده شود؛ ولایت/ولسوالی چون کنترلِ متناظری در این فرم
        // نداشتند، به‌صورت نواری خبری بالای گرید نشان داده می‌شوند.
        private void ApplyIncomingDashboardFilter()
        {
            bool hasProvinceOrDistrict = _incomingFilterProvince.Length > 0 || _incomingFilterDistrict.Length > 0;
            bool hasStatus = _incomingFilterServiceStatus.Length > 0;

            if (hasStatus)
            {
                for (int i = 0; i < cmbServiceStatusFilter.Items.Count; i++)
                {
                    if (string.Equals(cmbServiceStatusFilter.Items[i].ToString(), _incomingFilterServiceStatus, StringComparison.Ordinal))
                    {
                        cmbServiceStatusFilter.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (!hasProvinceOrDistrict && !hasStatus) return;

            string text = "فیلترِ داشبورد فعال است: ";
            var parts = new System.Collections.Generic.List<string>();
            if (_incomingFilterProvince.Length > 0) parts.Add("ولایت=" + _incomingFilterProvince);
            if (_incomingFilterDistrict.Length > 0) parts.Add("ولسوالی=" + _incomingFilterDistrict);
            if (hasStatus) parts.Add("وضعیت=" + _incomingFilterServiceStatus);
            text += string.Join("، ", parts) + " — فهرست و جستجو فقط همین محدوده را نشان می‌دهند.";

            _dashboardFilterBanner = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = UiTheme.Warning };
            Label lbl = new Label
            {
                Text = text, Dock = DockStyle.Fill, ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeSmall), TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0)
            };
            Button btnClear = UiTheme.CreateSecondaryButton("نمایش همه", "✕");
            btnClear.Size = new Size(110, 26);
            btnClear.Margin = new Padding(4);
            btnClear.Dock = DockStyle.Left;
            btnClear.Click += delegate
            {
                _incomingFilterProvince = "";
                _incomingFilterDistrict = "";
                _incomingFilterServiceStatus = "";
                cmbServiceStatusFilter.SelectedIndex = 0;
                _dashboardFilterBanner.Visible = false;
                LoadCases();
            };
            _dashboardFilterBanner.Controls.Add(lbl);
            _dashboardFilterBanner.Controls.Add(btnClear);

            Control gridContainer = dgvCases.Parent;
            if (gridContainer != null)
                gridContainer.Controls.Add(_dashboardFilterBanner);
        }

        // آموزش — رفعِ کندیِ بازشدنِ FrmCase با ~۱۲۰۰۰ پرونده: به‌جای بارگذاریِ
        // کل جدول، فقط ۲۰۰ پرونده‌ی آخر (جدیدترین‌ها) در گرید نشان داده
        // می‌شود. جستجو (btnSearch/dgvCases_CellClick از طریق LoadCaseByCode/
        // LoadCaseById) مستقل از این گرید مستقیماً از دیتابیس می‌خواند، پس
        // هیچ پرونده‌ای برای جستجو غیرقابل‌دسترس نمی‌شود.
        private const int MaxGridRows = 200;

        private void LoadCases()
        {
            try
            {
                using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand(@"
                    SELECT CasID, FormNo, Code, CaseNo, HeadFullName, Phone, ServiceStatus, CaseDate, PhotoPath,
                           HeadFatherName, HeadTazkiraNo, HeadCurrentResidence, Province, District
                    FROM TblCase
                    WHERE (@ServiceStatus = '' OR ServiceStatus = @ServiceStatus)
                      AND (@CID = 0 OR CenterID = @CID)
                      AND (@Prov = '' OR Province = @Prov)
                      AND (@Dist = '' OR District LIKE '%' || @Dist || '%')
                    ORDER BY CasID DESC
                    LIMIT " + MaxGridRows, con))
                {
                    AddStringParameter(cmd, "@ServiceStatus", GetSelectedServiceStatusFilter());
                    cmd.Parameters.AddWithValue("@CID", Helpers.SecurityContext.CenterFilterId);
                    AddStringParameter(cmd, "@Prov", _incomingFilterProvince);
                    AddStringParameter(cmd, "@Dist", _incomingFilterDistrict);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);
                        dgvCases.DataSource = dt;
                    }
                }

                ConfigureCasesGrid();
                // فقط وقتی ستون عکس انتخاب شده باشد کاری می‌کند (خودش بررسی
                // می‌کند)، پس برای بقیه‌ی حالت‌ها هزینه‌ای ندارد.
                LoadCaseThumbnails();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری لیست: " + ex.Message);
            }
        }

        // آموزش — جستجوی نوار بالای گرید: برخلافِ LoadCases (که فقط ۲۰۰ پرونده‌ی
        // آخر را می‌آورد)، این متد مستقیماً کل جدول را بر اساسِ ستونِ انتخابی
        // جستجو می‌کند (نه فقط ۲۰۰ ردیفِ نمایش‌داده‌شده)، اما برای امنیت
        // چندمرکزی همچنان با CenterID فیلتر می‌شود و نتیجه هم برای حفظِ
        // کارایی به همان سقفِ MaxGridRows محدود است.
        // آموزش — فاز A2: قبلاً این متد فقط یک ستون (انتخاب‌شده از یک کمبو) را
        // با یک مقدار جستجو می‌کرد. حالا نوار جستجوی سریع بالای فرم چهار فیلد
        // هم‌زمان دارد (کد پرونده/نام سرپرست/شماره تذکره/شماره تماس)؛ هرکدام
        // که پر باشد با AND به بقیه اضافه می‌شود، دقیقاً روی همان ستون‌های
        // CaseSearchTypeColumns (بدون هیچ ستون/کوئری جدید). فیلترِ داشبورد
        // (ServiceStatus/Province/District) و سقفِ MaxGridRows دست‌نخورده ماندند.
        private void SearchCasesGrid()
        {
            string code    = txtQsCode.Text.Trim();
            string head    = txtQsHeadName.Text.Trim();
            string tazkira = txtQsTazkira.Text.Trim();
            string phone   = txtQsPhone.Text.Trim();

            if (code == "" && head == "" && tazkira == "" && phone == "")
            {
                Msg.Show("حداقل یکی از فیلدهای جستجو را پر کنید");
                txtQsCode.Focus();
                return;
            }

            try
            {
                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(@"
                    SELECT CasID, FormNo, Code, CaseNo, HeadFullName, Phone, ServiceStatus, CaseDate, PhotoPath,
                           HeadFatherName, HeadTazkiraNo, HeadCurrentResidence, Province, District
                    FROM TblCase
                    WHERE (@Code = '' OR " + CaseSearchTypeColumns[0] + @" LIKE '%' || @Code || '%')
                      AND (@Head = '' OR " + CaseSearchTypeColumns[1] + @" LIKE '%' || @Head || '%')
                      AND (@Tazkira = '' OR " + CaseSearchTypeColumns[2] + @" LIKE '%' || @Tazkira || '%')
                      AND (@Phone = '' OR " + CaseSearchTypeColumns[3] + @" LIKE '%' || @Phone || '%')
                      AND (@CID = 0 OR CenterID = @CID)
                      AND (@ServiceStatus = '' OR ServiceStatus = @ServiceStatus)
                      AND (@Prov = '' OR Province = @Prov)
                      AND (@Dist = '' OR District LIKE '%' || @Dist || '%')
                    ORDER BY CasID DESC
                    LIMIT " + MaxGridRows, con))
                {
                    AddStringParameter(cmd, "@Code", code);
                    AddStringParameter(cmd, "@Head", head);
                    AddStringParameter(cmd, "@Tazkira", tazkira);
                    AddStringParameter(cmd, "@Phone", phone);
                    cmd.Parameters.AddWithValue("@CID", Helpers.SecurityContext.CenterFilterId);
                    // آموزش — رفعِ درخواستِ کاربر: وقتی فرم با فیلترِ داشبورد
                    // (ولایت/ولسوالی/وضعیت) باز شده، جستجو هم باید همان
                    // محدوده را در نظر بگیرد، نه کلِ دیتابیس را.
                    AddStringParameter(cmd, "@ServiceStatus", GetSelectedServiceStatusFilter());
                    AddStringParameter(cmd, "@Prov", _incomingFilterProvince);
                    AddStringParameter(cmd, "@Dist", _incomingFilterDistrict);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);
                        dgvCases.DataSource = dt;
                    }
                }

                ConfigureCasesGrid();
                LoadCaseThumbnails();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در جستجو: " + ex.Message);
            }
        }

        // بازگشت گرید به حالتِ پیش‌فرض (۲۰۰ پرونده‌ی آخر) — همان LoadCases موجود.
        private void ClearCaseSearchGrid()
        {
            txtQsCode.Text = "";
            txtQsHeadName.Text = "";
            txtQsTazkira.Text = "";
            txtQsPhone.Text = "";
            LoadCases();
        }

        // آموزش — فاز A2: «جستجوی پیشرفته» فرمِ کاملاً موجود FrmAdvancedSearch
        // را باز می‌کند — دقیقاً همان الگویی که از قبل در FrmDashboard.cs برای
        // همین دکمه استفاده می‌شود؛ هیچ منطق/فرم جدیدی ساخته نشد.
        private void btnAdvancedSearch_Click(object sender, EventArgs e)
        {
            using (var frm = new FrmAdvancedSearch())
                frm.ShowDialog(this);
        }

        private void btnQuickSearch_Click(object sender, EventArgs e)
        {
            SearchCasesGrid();
        }

        private void btnQuickSearchClear_Click(object sender, EventArgs e)
        {
            ClearCaseSearchGrid();
        }

        // Enter در هرکدام از چهار فیلدِ نوار جستجوی سریع، جستجو را اجرا می‌کند —
        // همان رفتاری که نوارِ قدیمیِ بالای گرید داشت.
        private void QuickSearchField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SearchCasesGrid();
            }
        }

        private const string PhotoThumbColumnName = "colPhotoThumb";

        // آموزش — رفع درخواست کاربر: لیست پرونده‌ها فقط سه ستون (کد اختصاصی/
        // نام سرپرست/عکس) را نشان می‌دهد تا بدون اسکرول افقی کامل در پنل جا
        // شود. بقیه ستون‌های دیتای بارگذاری‌شده (CasID برای انتخاب ردیف، و
        // بقیه برای منطق داخلی) پنهان می‌مانند نه حذف — چون dgvCases_CellClick
        // و بقیه کد همچنان به مقدار CasID هر ردیف نیاز دارند.
        // آموزش — ستون‌های این گرید حالا از تنظیمات می‌آیند (درخواست کاربر):
        // «کد اختصاصی» همیشه ثابت است و علاوه بر آن حداکثر چهار ستون از فهرست
        // CaseGridColumns انتخاب می‌شود، تا گرید بدون اسکرول افقی جا شود.
        // بقیه‌ی ستون‌های کوئری (مثل CasID) پنهان می‌شوند نه حذف — چون
        // dgvCases_CellClick و بقیه‌ی کد به مقدارشان نیاز دارند.
        private void ConfigureCasesGrid()
        {
            if (dgvCases.Columns.Count == 0)
                return;

            List<CaseGridColumn> selected = CaseGridColumns.GetSelected();
            _appliedGridColumnsCsv = CaseGridColumns.ToCsv(selected);

            // ۱) همه‌ی ستون‌های داده‌ای پنهان می‌شوند، بعد فقط انتخاب‌شده‌ها
            //    دوباره روشن می‌گردند. این‌طوری افزودن ستون جدید به کوئری هرگز
            //    باعث نشتِ یک ستونِ ناخواسته به گرید نمی‌شود.
            foreach (DataGridViewColumn column in dgvCases.Columns)
                column.Visible = false;

            // ۲) ستون تصویری: فقط وقتی کاربر «عکس» را انتخاب کرده باشد ساخته
            //    می‌شود. اگر انتخاب نشده باشد، حذفش می‌کنیم تا ردیف‌ها کوتاه
            //    بمانند و هزینه‌ی ساخت thumbnail هم پرداخت نشود.
            bool wantsPhoto = selected.Any(c => c.IsPhoto);

            if (!wantsPhoto)
            {
                if (dgvCases.Columns.Contains(PhotoThumbColumnName))
                    dgvCases.Columns.Remove(PhotoThumbColumnName);
            }
            else if (!dgvCases.Columns.Contains(PhotoThumbColumnName))
            {
                var photoColumn = new DataGridViewImageColumn
                {
                    Name = PhotoThumbColumnName,
                    HeaderText = "عکس",
                    ImageLayout = DataGridViewImageCellLayout.Zoom,
                    // بدون این، سلول‌های بدون عکس آیکونِ «تصویر خراب» نشان می‌دهند
                    // — همان ظاهر غیرحرفه‌ای که قبلاً باعث حذف این ستون شده بود.
                    DefaultCellStyle = { NullValue = null }
                };
                dgvCases.Columns.Add(photoColumn);
            }

            // ۳) ستون ثابت + انتخاب‌شده‌ها به ترتیب.
            SetGridHeader(CaseGridColumns.FixedColumn, CaseGridColumns.FixedColumnTitle);
            ShowGridColumn(CaseGridColumns.FixedColumn, 0);

            int displayIndex = 1;
            foreach (CaseGridColumn column in selected)
            {
                string name = column.IsPhoto ? PhotoThumbColumnName : column.DataColumn;

                if (!column.IsPhoto)
                    SetGridHeader(name, column.DisplayName);

                ShowGridColumn(name, displayIndex);
                displayIndex++;
            }

            dgvCases.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvCases.MultiSelect = false;
            dgvCases.ReadOnly = true;
            dgvCases.AllowUserToAddRows = false;
            dgvCases.AllowUserToDeleteRows = false;
            dgvCases.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            // با عکس، ردیف بلندتر لازم است تا thumbnail بریده نشود.
            dgvCases.RowTemplate.Height = wantsPhoto ? 46 : 32;
            dgvCases.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }

        // آخرین ترکیبِ ستونی که روی گرید اعمال شده. برای اینکه تغییرِ تنظیمات
        // «فوری» اعمال شود بدون اینکه هر بار فعال‌شدنِ فرم، گرید بی‌دلیل از نو
        // ساخته و thumbnailها دوباره تولید شوند.
        private string _appliedGridColumnsCsv;

        // اگر کاربر ستون‌ها را در فرم تنظیمات عوض کرده باشد، با برگشتن به این
        // فرم بلافاصله اعمال می‌شود (نیازی به بستن و باز کردن فرم نیست).
        private void FrmCase_Activated(object sender, EventArgs e)
        {
            string current = CaseGridColumns.ToCsv(CaseGridColumns.GetSelected());

            if (string.Equals(current, _appliedGridColumnsCsv, StringComparison.OrdinalIgnoreCase))
                return;

            ConfigureCasesGrid();
            LoadCaseThumbnails();
        }

        private void ShowGridColumn(string columnName, int displayIndex)
        {
            if (!dgvCases.Columns.Contains(columnName))
                return;

            DataGridViewColumn column = dgvCases.Columns[columnName];
            column.Visible = true;

            if (displayIndex < dgvCases.Columns.Count)
                column.DisplayIndex = displayIndex;
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

            // آموزش — رفعِ کندیِ واقعیِ باز شدنِ FrmCase با ~۱۲۰۰۰ پرونده (با
            // آزمونِ زمان‌سنجیِ واقعی تأیید شد): وقتی skipImages=true هیچ
            // thumbnail‌ای نمایش داده نمی‌شود، پس بلندترکردنِ ردیف (Height=46)
            // هیچ فایده‌ی بصری‌ای ندارد و صرفاً هزینه‌ی بی‌مصرف است. حلقه‌ی
            // پیمایشِ ۱۲۰۰۰ ردیف (فقط برای همین Height) خودِ علتِ اصلیِ کندی
            // بود، نه بارگذاریِ فایل‌های تصویری (که از قبل با MaxInlineThumbnails
            // درست skip می‌شد). برای حالتِ ≤۵۰۰ ردیف هیچ تغییری نکرده.
            if (skipImages)
                return;

            foreach (DataGridViewRow row in dgvCases.Rows)
            {
                if (row.IsNewRow)
                    continue;

                row.Height = 46;

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
            // پروندهٔ جدید طبیعتاً باید بلافاصله قابلِ تایپ باشد.
            SetCaseEditMode(true);
            EnsureEditableTabVisible();
            txtCode.Focus();
        }

        // آموزش — رفعِ باگِ «دکمه جدید کار نمی‌کند»: تب‌های «خلاصه پرونده»،
        // «اعضاء خانواده» و «اسناد پرونده» هیچ‌کدام فیلدِ قابلِ ویرایشِ *پرونده*
        // ندارند (اولی همیشه خواندنی است و دوتای بعدی فرم‌های مستقلِ خودشان
        // هستند). اگر کاربر روی یکی از این‌ها باشد و «جدید»/«ویرایش» بزند،
        // ظاهراً هیچ اتفاقی نمی‌افتد چون فیلدهای بازشده اصلاً دیده نمی‌شوند.
        // پس در آن حالت خودکار به تب «مشخصات کلی سرپرست» می‌رویم.
        private void EnsureEditableTabVisible()
        {
            if (tabsCase == null || tabHeadInfo == null)
                return;

            TabPage current = tabsCase.SelectedTab;

            bool onNonEditableTab =
                current == null ||
                current == tabsCase.TabPages[0] ||               // خلاصه پرونده
                current == tabMembersHost.Parent ||              // اعضاء خانواده
                current == tabDocsHost.Parent;                   // اسناد پرونده

            if (onNonEditableTab)
                tabsCase.SelectedTab = tabHeadInfo;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.CanEdit())
            {
                Msg.Show("کاربر فقط مشاهده اجازه ثبت پرونده ندارد.");
                return;
            }

            // آموزش — حالتِ نمایش/ویرایش: قبلاً این دکمه فقط برای رکوردِ جدید
            // بود و برای رکوردِ موجود پیام «از دکمه ویرایش استفاده کن» می‌داد.
            // حالا «ویرایش» فقط قفل را باز می‌کند و ثبتِ نهایی — چه درج و چه
            // به‌روزرسانی — با همین دکمه انجام می‌شود. منطقِ SQL هر دو مسیر
            // دست‌نخورده است؛ فقط مسیرِ به‌روزرسانی به UpdateCurrentCase منتقل شد.
            if (currentCaseId != 0)
            {
                if (UpdateCurrentCase())
                    SetCaseEditMode(false);
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
                        Phone, RelativePhone, CoveredByOrg, CoveredByOrgNames, Job, Skill,
                        DisabilityDegree, DisabilityType, MigrationCardType, MaritalStatus,
                        Surveyors, SurveyDate, LocationAddress, EducationLevel, ServiceStatus, StopReason, SuspensionReason, UrgentSituation,
                        PhotoPath, FamilyPhotoPath, CenterID, SuspensionDate, SuspendedByUserId, SuspendedByUsername
                    )
                    VALUES
                    (
                        @FormNo, @Code, @CaseNo, @CaseDate,
                        @Zone, @Province, @District, @RequestType, @PriorityLevel,
                        @HeadFullName, @HeadFatherName, @HeadSadat, @Religion, @HeadTazkiraNo,
                        @HeadOriginalResidence, @HeadCurrentResidence, @RelationshipToFamily,
                        @Phone, @RelativePhone, @CoveredByOrg, @CoveredByOrgNames, @Job, @Skill,
                        @DisabilityDegree, @DisabilityType, @MigrationCardType, @MaritalStatus,
                        @Surveyors, @SurveyDate, @LocationAddress, @EducationLevel, @ServiceStatus, @StopReason, @SuspensionReason, @UrgentSituation,
                        @PhotoPath, @FamilyPhotoPath, @CenterID, @SuspensionDate, @SuspendedByUserId, @SuspendedByUsername
                    );";

                    using (var cmd = new SQLiteCommand(query, con))
                    {
                        AddCaseParameters(cmd, true);
                        cmd.Parameters.AddWithValue("@CenterID", Helpers.SecurityContext.CurrentCenterId > 0
                            ? Helpers.SecurityContext.CurrentCenterId : 1);
                        AddSuspensionStampParameters(cmd);

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
                AuditLogger.RecordStatusChange(currentCaseId, "", NormalizeServiceStatus(txtServiceStatus.Text),
                    txtSuspensionReason.Text.Trim(), txtStopReason.Text.Trim());

                Msg.Show("اطلاعات با موفقیت ذخیره شد");
                LoadCases();
                SyncMembersTab();
                SetCaseEditMode(false);   // درج موفق → بازگشت به حالت نمایش
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

        // آموزش — حالتِ نمایش/ویرایش: این دکمه دیگر مستقیماً روی دیتابیس
        // نمی‌نویسد؛ فقط قفلِ فیلدها را باز می‌کند. ثبتِ نهایی با دکمه‌ی
        // «ذخیره» انجام می‌شود که به UpdateCurrentCase (همان منطقِ قبلیِ این
        // متد، بدون تغییر) می‌رسد.
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

            SetCaseEditMode(true);
            EnsureEditableTabVisible();
        }

        // منطقِ به‌روزرسانی — عیناً همان کدی که قبلاً داخل btnEdit_Click بود.
        // خروجی: true یعنی ذخیره موفق بود (تا فراخوان بتواند فرم را قفل کند).
        private bool UpdateCurrentCase()
        {
            if (!ValidateForm())
                return false;

            try
            {
                CenterGuard.EnsureCaseAccess(db, currentCaseId);

                string oldValue = GetCaseAuditTextFromDb(currentCaseId);
                string oldStatus = GetCaseStatusById(currentCaseId);

                if (IsCodeExists(txtCode.Text.Trim(), currentCaseId))
                {
                    Msg.Show("کد اختصاصی تکراری است");
                    txtCode.Focus();
                    return false;
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
                        CoveredByOrgNames = @CoveredByOrgNames,
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
                        SuspensionReason = @SuspensionReason,
                        UrgentSituation = @UrgentSituation,
                        PhotoPath = @PhotoPath,
                        FamilyPhotoPath = @FamilyPhotoPath,
                        UpdatedAt = datetime('now'),
                        SuspensionDate = CASE WHEN ServiceStatus = @ServiceStatus THEN SuspensionDate ELSE @SuspensionDate END,
                        SuspendedByUserId = CASE WHEN ServiceStatus = @ServiceStatus THEN SuspendedByUserId ELSE @SuspendedByUserId END,
                        SuspendedByUsername = CASE WHEN ServiceStatus = @ServiceStatus THEN SuspendedByUsername ELSE @SuspendedByUsername END
                    WHERE CasID = @CasID AND (@CID = 0 OR CenterID = @CID)";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                    {
                        AddCaseParameters(cmd, false);
                        AddSuspensionStampParameters(cmd);
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);

                        con.Open();
                        int affectedRows = cmd.ExecuteNonQuery();

                        if (affectedRows == 0)
                        {
                            Msg.Show("رکورد برای ویرایش پیدا نشد یا متعلق به مرکز دیگری است");
                            currentCaseId = 0;
                            LoadCases();
                            SyncMembersTab();
                            return false;
                        }
                    }
                }

                selectedHeadPhotoSource = "";
                selectedFamilyPhotoSource = "";

                AuditLogger.Log("ویرایش", "TblCase", currentCaseId, oldValue, BuildCurrentCaseAuditText());
                AuditLogger.RecordStatusChange(currentCaseId, oldStatus, NormalizeServiceStatus(txtServiceStatus.Text),
                    txtSuspensionReason.Text.Trim(), txtStopReason.Text.Trim());

                Msg.Show("اطلاعات ویرایش شد");
                LoadCases();
                SyncMembersTab();
                return true;
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

            return false;
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
                        SyncMembersTab();
                        SetCaseEditMode(false);
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
                SetCaseEditMode(false);   // بعد از حذف، فرم خالی و قفل می‌ماند
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
            txtCoveredByOrgNames.Text = GetDbString(dr, "CoveredByOrgNames");
            // بعد از پرشدن هر دو فیلد، وضعیت نمایشِ کادر اسامی را هماهنگ کن.
            // (باید بعد از مقداردهی باشد، وگرنه متنِ تازه‌خوانده‌شده پاک می‌شود.)
            UpdateCoveredByOrgNamesVisibility();
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
            string savedSuspensionReason = GetDbString(dr, "SuspensionReason");
            int suspReasonIdx = txtSuspensionReason.FindStringExact(savedSuspensionReason);
            txtSuspensionReason.SelectedIndex = suspReasonIdx;
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

            SyncMembersTab();

            // پروندهٔ بارگذاری‌شده همیشه در حالتِ نمایش باز می‌شود؛ برای تغییر
            // باید کاربر صریحاً «ویرایش» را بزند.
            SetCaseEditMode(false);
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

        // رسمِ شماره‌ی ردیف (۱-پایه) در ناحیه‌ی RowHeader — الگوی استانداردِ
        // WinForms برای شماره‌گذاری، بدون افزودنِ ستونِ دیتاییِ اضافه.
        //
        // آموزش — رفعِ باگِ واقعی (با آزمونِ واقعیِ مقایسه‌ی مختصات تأیید شد):
        // e.RowBounds ناحیه‌ی سلول‌های داده را می‌دهد، *بدونِ* هدر — و چون این
        // فرم RightToLeft=Yes دارد، هدرِ ردیف فیزیکاً سمتِ *راستِ* همان ناحیه
        // می‌نشیند، نه چپش. نسخه‌ی قبلی از e.RowBounds.Left استفاده می‌کرد که
        // دقیقاً روی ستونِ اولِ داده (ستونِ عکس) می‌افتاد — یعنی شماره‌ی ردیف
        // با خودِ عکس همپوشانی داشت. با آزمون تأیید شد: مستطیلِ چپ‌مبنا با
        // ستونِ اول همپوشانی دارد، مستطیلِ راست‌مبنا ندارد.
        private void DgvCases_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            string rowNumber = (e.RowIndex + 1).ToString();
            SizeF size = e.Graphics.MeasureString(rowNumber, dgvCases.RowHeadersDefaultCellStyle.Font ?? dgvCases.Font);

            int headerLeft = dgvCases.RightToLeft == RightToLeft.Yes
                ? e.RowBounds.Right
                : e.RowBounds.Left - dgvCases.RowHeadersWidth;
            Rectangle headerBounds = new Rectangle(headerLeft, e.RowBounds.Top, dgvCases.RowHeadersWidth, e.RowBounds.Height);

            e.Graphics.DrawString(rowNumber, dgvCases.Font, SystemBrushes.ControlText,
                headerBounds.Left + (headerBounds.Width - size.Width) / 2,
                headerBounds.Top + (headerBounds.Height - size.Height) / 2);
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

        // آموزش — فاز ۱: قبلاً اینجا FrmFamily را با ShowDialog به‌صورت
        // پنجرهٔ مجزا باز می‌کرد. حالا اعضاء یک تب داخل همین فرم است، پس این
        // دکمه فقط کاربر را به آن تب می‌برد؛ ساخت/رفرشِ واقعیِ فرم embedded
        // در EnsureFamilyEmbedded (فراخوانی‌شده از tabsCase_SelectedIndexChanged) انجام می‌شود.
        private void btnFamily_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا جستجو کن");
                return;
            }

            tabsCase.SelectedTab = (TabPage)tabMembersHost.Parent;
        }

        // تک نقطهٔ هماهنگ‌سازی تب «اعضاء خانواده» با currentCaseId. هرجا
        // currentCaseId تغییر می‌کند (بارگذاری/ذخیره/ویرایش/حذف/پاک‌شدن فرم)
        // همین‌جا صدا زده می‌شود. اگر نمونهٔ embedded هنوز ساخته نشده (کاربر
        // هنوز روی تب نرفته)، کاری نمی‌کند — ساختش به EnsureFamilyEmbedded
        // واگذار شده تا هزینه‌اش فقط وقتی پرداخت شود که واقعاً لازم است.
        private void SyncMembersTab()
        {
            if (lblMembersPlaceholder == null)
                return; // InitializeComponent هنوز کامل اجرا نشده

            // آموزش — همین نقطه‌ی هماهنگ‌سازی که فاز ۱ برای تب اعضاء ساخت،
            // دقیقاً همان لحظه‌ای است که تب «خلاصه پرونده» هم باید به‌روز شود
            // (بارگذاری/ذخیره/ویرایش/حذف/پاک‌شدن فرم). یک نقطه‌ی صدازنی،
            // به‌جای پخش‌کردن این متد در چند جای FrmCase.cs.
            UpdateCaseSummaryTab();

            if (currentCaseId == 0)
            {
                if (_embeddedFamily != null)
                    _embeddedFamily.Visible = false;
                lblMembersPlaceholder.Visible = true;
                _familyDirty = false;

                if (_embeddedDocs != null)
                    _embeddedDocs.Visible = false;
                lblDocsPlaceholder.Visible = true;
                _docsDirty = false;
                return;
            }

            if (_embeddedFamily != null)
            {
                if (tabsCase.SelectedTab == tabMembersHost.Parent)
                {
                    // تب همین الان دیده می‌شود → رفرش فوری لازم است.
                    _embeddedFamily.RefreshForCase(currentCaseId, txtCode.Text.Trim());
                    _embeddedFamily.Visible = true;
                    lblMembersPlaceholder.Visible = false;
                    _familyDirty = false;
                }
                else
                {
                    // تب دیده نمی‌شود؛ کوئری نمی‌زنیم — فقط علامت می‌زنیم تا
                    // EnsureFamilyEmbedded یک‌بار، دقیقاً وقتی کاربر به تب برود، رفرش کند.
                    _familyDirty = true;
                }
            }

            // آموزش — فاز A4: عیناً همان منطقِ بالا برای تب اعضاء، برای تب اسناد.
            if (_embeddedDocs != null)
            {
                if (tabsCase.SelectedTab == tabDocsHost.Parent)
                {
                    _embeddedDocs.RefreshForCase(currentCaseId, txtCode.Text.Trim());
                    _embeddedDocs.Visible = true;
                    lblDocsPlaceholder.Visible = false;
                    _docsDirty = false;
                }
                else
                {
                    _docsDirty = true;
                }
            }
        }

        // ═══ حالتِ نمایش/ویرایش (فقط برای فیلدهای خودِ پرونده) ═════════════
        // آموزش — فرم به‌صورت پیش‌فرض «فقط نمایش» است و با دکمه‌ی «ویرایش»
        // (یا «جدید») باز می‌شود. عمداً فقط سه کارتِ فیلدهای پرونده
        // (grpHead/grpPhysical/grpCase) پیمایش می‌شوند، نه کلِ فرم — چون:
        //   • تب‌های «اعضاء» و «اسناد» فرم‌های embeddedِ مستقل‌اند و دکمه‌های
        //     جدید/ذخیره/ویرایشِ خودشان را دارند؛ قفل‌کردنشان از این‌جا
        //     کارکردشان را می‌شکست.
        //   • تب «خلاصه پرونده» از قبل و همیشه خواندنی است.
        //   • نوار جستجوی سریع باید همیشه فعال بماند (وگرنه کاربر در حالتِ
        //     قفل نمی‌توانست پروندهٔ بعدی را پیدا کند).
        private bool _caseEditMode = false;

        private void SetCaseEditMode(bool editable)
        {
            _caseEditMode = editable;

            ApplyReadOnlyToFieldBoxes(grpHead, !editable);
            ApplyReadOnlyToFieldBoxes(grpPhysical, !editable);
            ApplyReadOnlyToFieldBoxes(grpCase, !editable);

            // چک‌باکسِ «سالم است» داخل FieldBox نیست، پس جدا کنترل می‌شود.
            chkHeadHealthy.Enabled = editable;

            // انتخابِ عکس هم بخشی از ویرایش است.
            btnBrowsePhoto.Enabled = editable;
            btnBrowseFamilyPhoto.Enabled = editable;

            btnSave.Enabled = editable;

            if (editable)
            {
                // آموزش — این سه متد وضعیتِ فعال/غیرفعالِ فیلدهای وابسته را
                // تعیین می‌کنند (معلولیت، دلیل تعلیق، اسامی مؤسسات). چون
                // پیمایشِ بالا همه‌ی فیلدها را یکجا باز کرد، باید دوباره اعمال
                // شوند وگرنه فیلدی که باید بسته بماند (مثل نوع معلولیتِ فردِ
                // سالم) باز می‌شود.
                UpdateHeadPhysicalState();
                UpdateStopReasonVisibility();
                UpdateCoveredByOrgNamesVisibility();
            }

            // شماره فرم همیشه اتومات/قفل است — مستقل از حالتِ ویرایش.
            txtFormNo.ReadOnly = true;
            txtFormNo.TabStop = false;
        }

        private static void ApplyReadOnlyToFieldBoxes(Control parent, bool readOnly)
        {
            if (parent == null)
                return;

            foreach (Control child in parent.Controls)
            {
                FieldBox box = child as FieldBox;
                if (box != null)
                {
                    box.SetReadOnly(readOnly);
                    continue;   // داخلِ FieldBox را نگرد؛ خودش هندل کرد
                }

                if (child.HasChildren)
                    ApplyReadOnlyToFieldBoxes(child, readOnly);
            }
        }

        private static string TextOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        // آموزش — فاز A3 (تب خلاصه پرونده): پنج فیلدِ اول و عکس عیناً مثلِ
        // UpdateCaseHeader از همان کنترل‌های موجود کپی می‌شوند — بدون کوئری.
        // سه مقدارِ باقی‌مانده (تعداد اعضاء/آخرین کمک/آخرین تغییر) در هیچ‌جای
        // دیگر این فرم موجود نبودند، پس با سه کوئری سبک (COUNT/۱ردیف/MAX) روی
        // جدول‌های موجود (TblFamily/TblAssistance/TblAuditLog) پر می‌شوند —
        // بدون هیچ جدول یا منطق تکراری. مثل SyncMembersTab، این متد هم از
        // همان یک نقطهٔ هماهنگ‌سازی صدا زده می‌شود.
        private void UpdateCaseSummaryTab()
        {
            txtSummaryCode.Text          = TextOrDash(txtCode.Text);
            txtSummaryHeadName.Text      = TextOrDash(txtHeadFullName.Text);
            txtSummaryRequestType.Text   = TextOrDash(txtRequestType.Text);
            txtSummaryServiceStatus.Text = TextOrDash(txtServiceStatus.Text);

            string province = txtProvince.Text.Trim();
            string district = txtDistrict.Text.Trim();
            txtSummaryLocation.Text = (province == "" && district == "")
                ? "—"
                : province + (district == "" ? "" : " / " + district);

            LoadImageToPictureBox(savedHeadPhotoPath, picSummaryPhoto);

            if (currentCaseId == 0)
            {
                txtSummaryMemberCount.Text = "—";
                txtSummaryLastAssistance.Text = "—";
                txtSummaryLastChange.Text = "—";
                return;
            }

            try
            {
                using (var con = db.GetConnection())
                {
                    con.Open();

                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM TblFamily WHERE CasID = @CasID", con))
                    {
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        txtSummaryMemberCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
                    }

                    using (var cmd = new SQLiteCommand(
                        "SELECT AssistanceDate, Amount FROM TblAssistance WHERE CasID = @CasID ORDER BY AssistanceID DESC LIMIT 1", con))
                    {
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        using (var dr = cmd.ExecuteReader())
                        {
                            txtSummaryLastAssistance.Text = dr.Read()
                                ? GetDbString(dr, "AssistanceDate") + " — " + Convert.ToDecimal(dr["Amount"]).ToString("N0")
                                : "—";
                        }
                    }

                    using (var cmd = new SQLiteCommand(
                        "SELECT MAX(CreatedAt) FROM TblAuditLog WHERE EntityName = 'TblCase' AND EntityID = @CasID", con))
                    {
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        object result = cmd.ExecuteScalar();
                        txtSummaryLastChange.Text = (result == null || result == DBNull.Value) ? "—" : result.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری خلاصهٔ پرونده: " + ex.Message);
            }
        }

        // فقط وقتی کاربر واقعاً روی تب «اعضاء خانواده» می‌رود صدا زده می‌شود
        // (از tabsCase_SelectedIndexChanged). بار اول نمونهٔ FrmFamily را
        // embedded می‌سازد؛ بارهای بعد فقط نمایانش می‌کند/رفرش می‌کند.
        private void EnsureFamilyEmbedded()
        {
            if (currentCaseId == 0)
            {
                lblMembersPlaceholder.Visible = true;
                if (_embeddedFamily != null)
                    _embeddedFamily.Visible = false;
                return;
            }

            if (_embeddedFamily == null)
            {
                _embeddedFamily = new FrmFamily();
                _embeddedFamily.IsEmbedded = true;
                _embeddedFamily.CurrentCaseId = currentCaseId;
                _embeddedFamily.CurrentCaseCode = txtCode.Text.Trim();
                _embeddedFamily.TopLevel = false;
                _embeddedFamily.FormBorderStyle = FormBorderStyle.None;
                _embeddedFamily.Dock = DockStyle.Fill;
                tabMembersHost.Controls.Add(_embeddedFamily);
                _embeddedFamily.Show(); // FrmFamily_Load خودش با CurrentCaseId فعلی بار می‌کند — رفرشِ دوباره لازم نیست
                _familyDirty = false;
            }
            else if (_familyDirty)
            {
                // فقط وقتی پرونده واقعاً از زمان آخرین بازدید از این تب عوض
                // شده رفرش می‌کنیم؛ رفت‌وبرگشتِ سادهٔ بین تب‌ها دیگر کوئری
                // اضافه نمی‌زند.
                _embeddedFamily.RefreshForCase(currentCaseId, txtCode.Text.Trim());
                _familyDirty = false;
            }

            _embeddedFamily.Visible = true;
            lblMembersPlaceholder.Visible = false;
        }

        // آموزش — فاز A4: عیناً همان الگوی EnsureFamilyEmbedded، برای FrmDocs.
        private void EnsureDocsEmbedded()
        {
            if (currentCaseId == 0)
            {
                lblDocsPlaceholder.Visible = true;
                if (_embeddedDocs != null)
                    _embeddedDocs.Visible = false;
                return;
            }

            if (_embeddedDocs == null)
            {
                _embeddedDocs = new FrmDocs();
                _embeddedDocs.IsEmbedded = true;
                _embeddedDocs.CurrentCaseId = currentCaseId;
                _embeddedDocs.CurrentCaseCode = txtCode.Text.Trim();
                _embeddedDocs.TopLevel = false;
                _embeddedDocs.FormBorderStyle = FormBorderStyle.None;
                _embeddedDocs.Dock = DockStyle.Fill;
                tabDocsHost.Controls.Add(_embeddedDocs);
                _embeddedDocs.Show(); // FrmDocs_Load خودش با CurrentCaseId فعلی بار می‌کند
                _docsDirty = false;
            }
            else if (_docsDirty)
            {
                _embeddedDocs.RefreshForCase(currentCaseId, txtCode.Text.Trim());
                _docsDirty = false;
            }

            _embeddedDocs.Visible = true;
            lblDocsPlaceholder.Visible = false;
        }

        private void tabsCase_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabsCase.SelectedTab == tabMembersHost.Parent)
                EnsureFamilyEmbedded();
            else if (tabsCase.SelectedTab == tabDocsHost.Parent)
                EnsureDocsEmbedded();

            UpdateWorkspaceWidth();
        }

        // آموزش — رفعِ «تب اعضاء کوچک شده»: تب‌های اعضاء و اسناد فرم‌های کاملی
        // (FrmFamily/FrmDocs) هستند که برای عرضِ ~۱۱۶۰ و ~۱۲۴۰ طراحی شده‌اند،
        // ولی ستونِ فیلدها فقط ۶۲٪ عرضِ فرم است، پس داخلِ تب له می‌شدند.
        // وقتی یکی از این دو تب فعال است، ستونِ سمتِ چپ (عکس‌ها + فهرستِ
        // پرونده‌ها) موقتاً جمع می‌شود تا تب تمامِ عرض را بگیرد؛ با برگشتن به
        // بقیه‌ی تب‌ها دقیقاً به همان ۶۲/۳۸ قبلی برمی‌گردد. هیچ کنترلی حذف
        // نمی‌شود — فقط عرضِ ستون عوض می‌شود.
        private const float FieldsColumnNormalPercent = 62F;
        private const float LeftColumnNormalPercent   = 38F;

        private void UpdateWorkspaceWidth()
        {
            if (rootLayout == null || rootLayout.ColumnStyles.Count < 2 || tabsCase == null)
                return;

            bool needsFullWidth =
                tabsCase.SelectedTab == tabMembersHost.Parent ||
                tabsCase.SelectedTab == tabDocsHost.Parent;

            float fieldsWidth = needsFullWidth ? 100F : FieldsColumnNormalPercent;
            float leftWidth   = needsFullWidth ? 0F   : LeftColumnNormalPercent;

            if (Math.Abs(rootLayout.ColumnStyles[1].Width - leftWidth) < 0.01F)
                return;   // از قبل در همین حالت است — چیدمان بی‌دلیل دوباره حساب نشود

            rootLayout.SuspendLayout();
            rootLayout.ColumnStyles[0].Width = fieldsWidth;
            rootLayout.ColumnStyles[1].Width = leftWidth;
            if (leftWorkspacePanel != null)
                leftWorkspacePanel.Visible = !needsFullWidth;
            rootLayout.ResumeLayout(true);
        }

        // آموزش — فاز A4: قبلاً اینجا FrmDocs را با ShowDialog به‌صورت پنجرهٔ
        // مجزا باز می‌کرد. حالا اسناد یک تب داخل همین فرم است؛ این دکمه فقط
        // کاربر را به آن تب می‌برد (همان تغییری که در فاز ۱ برای btnFamily شد).
        private void btnDocs_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا جستجو کن");
                return;
            }

            tabsCase.SelectedTab = (TabPage)tabDocsHost.Parent;
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

        // انتخابِ الگو هنگام خروجی.
        // آموزش — چرا اینجا و نه داخل GetWordTemplatePath: آن متد قرارداد
        // «مسیرِ قالبِ پیش‌فرض را بده یا خطا بینداز» را دارد و ممکن است جای
        // دیگری هم روی همان حساب کند. پس رفتارش دست‌نخورده ماند و انتخاب یک
        // لایه بالاتر انجام می‌شود. اگر فقط یک الگو روی دیسک باشد هیچ پنجره‌ای
        // باز نمی‌شود و مسیر دقیقاً مثل قبل است.
        // خروجیِ خالی یعنی کاربر انصراف داد.
        private string ChooseWordTemplatePath()
        {
            return ChooseWordTemplatePath(false);
        }

        private string ChooseWordTemplatePath(bool allowRdlc)
        {
            var options = Helpers.ReportTemplateHelper.DiscoverCaseTemplates(allowRdlc);

            if (options.Count == 0) return GetWordTemplatePath();   // همان استثنای قبلی
            if (options.Count == 1) return options[0].FilePath;

            var picked = Helpers.FrmTemplatePicker.Ask(this, options,
                Helpers.ReportTemplateHelper.GetRememberedPath());

            if (picked == null) return "";

            Helpers.ReportTemplateHelper.Remember(picked.FilePath);
            return picked.FilePath;
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

            // آموزش — به درخواست کاربر: نام فایل خروجی فقط «شماره اختصاصی»
            // پرونده باشد و هیچ پسوند/برچسب اضافه‌ای (مثل «_FullCase») نداشته
            // باشد. پسوندِ نوع فایل (.docx/.pdf) خودش توسط SaveFileToCaseFolder
            // از فایل مبدأ حفظ می‌شود، پس فقط همین برچسب حذف می‌شود.
            string savedPath = FileHelper.SaveFileToCaseFolder(
                sourceFilePath,
                caseCode,
                FileHelper.SectionDocs,
                safeCode,
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
                new System.Collections.Generic.KeyValuePair<string, string>("اولویت‌بندی اقتصادی", txtPriorityLevel.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("نام سرپرست", txtHeadFullName.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("نام پدر سرپرست", txtHeadFatherName.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("سیادت سرپرست", txtHeadSadat.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("مذهب", txtReligion.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره تذکره سرپرست", txtHeadTazkiraNo.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("سکونت اصلی", txtHeadOriginalResidence.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("سکونت فعلی", txtHeadCurrentResidence.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره تماس", txtPhone.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره تماس اقارب", txtRelativePhone.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("تحت پوشش دیگر مؤسسات", txtCoveredByOrg.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("اسامی مؤسسات تحت پوشش", txtCoveredByOrgNames.Text.Trim()),
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
                // اینجا (خروجیِ یک پرونده) الگوی قدیمیِ RDLC هم قابل انتخاب است.
                string templatePath = ChooseWordTemplatePath(true);
                if (string.IsNullOrEmpty(templatePath)) return;     // انصراف کاربر

                if (templatePath == Helpers.ReportTemplateHelper.RdlcKey)
                {
                    // مسیرِ کاملاً جدا: گزارشِ قدیمی در پنجره‌ی پیش‌نمایشِ خودش
                    // باز می‌شود و کاربر از همان‌جا چاپ یا ذخیره می‌کند.
                    using (var rpt = new FrmCaseReport(currentCaseId))
                        rpt.ShowDialog(this);
                    return;
                }

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
                // آموزش — به درخواست کاربر: الگوی قدیمی (RDLC) هم برای PDF قابل
                // انتخاب است، نه فقط برای Word. خروجی‌اش مستقیم و بی‌واسطه
                // است (RdlcExportHelper)، بدون نیاز به Word/LibreOffice.
                string templatePath = ChooseWordTemplatePath(true);
                if (string.IsNullOrEmpty(templatePath)) return;     // انصراف کاربر

                string safeCode = CleanFileName(txtCode.Text.Trim());

                if (templatePath == Helpers.ReportTemplateHelper.RdlcKey)
                {
                    tempPdf = Path.Combine(Path.GetTempPath(), safeCode + "_" + Guid.NewGuid().ToString("N") + ".pdf");
                    Helpers.RdlcExportHelper.ExportCaseToPdf(currentCaseId, tempPdf);

                    string savedRdlcPdfPath = SaveGeneratedFileToCaseCodeFolder(tempPdf);
                    Msg.Show("فایل PDF (الگوی قدیمی) با موفقیت ساخته شد:" + Environment.NewLine + savedRdlcPdfPath);
                    return;
                }

                tempDocx = Path.Combine(
                    Path.GetTempPath(),
                    safeCode + "_" + Guid.NewGuid().ToString("N") + ".docx");

                OpenXmlCaseExporter exporter = new OpenXmlCaseExporter();
                exporter.ExportFullCaseToWord(currentCaseId, templatePath, tempDocx);

                try
                {
                    // آموزش — رفع باگ «دکمه‌ی PDF فایل Word می‌داد»: این‌جا فقط
                    // LibreOffice امتحان می‌شد و روی سیستمی که فقط Word دارد
                    // همیشه خطا می‌خورد و به ذخیره‌ی docx برمی‌گشت.
                    // PdfConversionHelper مسیر استانداردِ خودِ پروژه است:
                    // اول Microsoft Word، بعد LibreOffice.
                    tempPdf = PdfConversionHelper.ConvertDocxToPdf(tempDocx);
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
                // پیش از ساخت خروجی، دیالوگِ فیلترهای پیشرفته نمایش داده می‌شود.
                // انصراف از این دیالوگ = انصراف از کل خروجی.
                Helpers.ReportFilterCriteria filter = Helpers.FrmReportFilter.Ask(this);
                if (filter == null) return;

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

                // خروجی اکسل همان فیلترِ «وضعیت خدمات» گرید را دنبال می‌کند مگر
                // اینکه کاربر در دیالوگِ فیلترِ پیشرفته مقدارِ دیگری انتخاب کرده
                // باشد. مقدار باید پیش از Task.Run خوانده شود (دسترسی به کنترل
                // از نخِ پس‌زمینه مجاز نیست).
                string exportServiceStatus = string.IsNullOrWhiteSpace(filter.ServiceStatus)
                    ? GetSelectedServiceStatusFilter()
                    : filter.ServiceStatus;

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
                        exporter.ExportFullReport(outputPath, exportServiceStatus, filter);
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

        // یک فایلِ docx ساخته‌شده که هنوز منتظرِ تبدیلِ دسته‌ایِ PDF است.
        private sealed class PendingBatchFile
        {
            public string CaseCode;
            public string FormNo;
            public string TempDocx;
        }

        private DataTable GetCasesForBatchExport(int startFormNo, int endFormNo, Helpers.ReportFilterCriteria filter)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT CasID, FormNo, Code
                FROM TblCase
                WHERE CAST(FormNo AS INTEGER) BETWEEN @StartFormNo AND @EndFormNo
                  AND (@CID = 0 OR CenterID = @CID)
                  AND (@ServiceStatus = '' OR ServiceStatus = @ServiceStatus)
                  AND (@Province = '' OR Province = @Province)
                  AND (@District = '' OR District = @District)
                  AND (@FamilyType = '' OR RequestType = @FamilyType)
                  AND (@DateFrom = '' OR CaseDate >= @DateFrom)
                  AND (@DateTo = '' OR CaseDate <= @DateTo)
                  AND (@ActiveOnly = -1 OR IsArchived = @ActiveOnly)
                  AND (@MinMembers = -1 OR (SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = TblCase.CasID) >= @MinMembers)
                  AND (@MaxMembers = -1 OR (SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = TblCase.CasID) <= @MaxMembers)
                ORDER BY CAST(FormNo AS INTEGER), CasID", con))
            {
                AddIntParameter(cmd, "@StartFormNo", startFormNo);
                AddIntParameter(cmd, "@EndFormNo", endFormNo);
                cmd.Parameters.AddWithValue("@CID", Helpers.SecurityContext.CenterFilterId);
                cmd.Parameters.AddWithValue("@MinMembers", filter?.MinMemberCount ?? -1);
                cmd.Parameters.AddWithValue("@MaxMembers", filter?.MaxMemberCount ?? -1);
                // خروجی جمعیِ بازه‌ی شماره فرم هم از فیلترِ وضعیت خدماتِ گرید پیروی
                // می‌کند مگر اینکه کاربر در دیالوگِ فیلترِ پیشرفته مقدارِ دیگری داده باشد.
                AddStringParameter(cmd, "@ServiceStatus",
                    string.IsNullOrWhiteSpace(filter?.ServiceStatus) ? GetSelectedServiceStatusFilter() : filter.ServiceStatus);
                cmd.Parameters.AddWithValue("@Province", filter?.Province ?? "");
                // ولسوالی حالا از کمبوی آبشاری (نه تایپِ آزاد) می‌آید، پس
                // مقایسه‌ی دقیق (=) به‌جای LIKE استفاده می‌شود.
                cmd.Parameters.AddWithValue("@District", filter?.District ?? "");
                cmd.Parameters.AddWithValue("@FamilyType", filter?.FamilyType ?? "");
                cmd.Parameters.AddWithValue("@DateFrom", filter?.RegistrationDateFrom.HasValue == true
                    ? filter.RegistrationDateFrom.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "");
                cmd.Parameters.AddWithValue("@DateTo", filter?.RegistrationDateTo.HasValue == true
                    ? filter.RegistrationDateTo.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "");
                cmd.Parameters.AddWithValue("@ActiveOnly", filter == null ? 0 : (filter.ActiveOnly == true ? 0 : (filter.ActiveOnly == false ? 1 : -1)));

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
            // پیش از هر خروجی جمعی، دیالوگِ فیلترهای پیشرفته نمایش داده می‌شود.
            Helpers.ReportFilterCriteria filter = Helpers.FrmReportFilter.Ask(this);
            if (filter == null) return;

            int startFormNo;
            int endFormNo;
            bool exportWord;
            bool exportPdf;

            if (!TryGetBatchExportOptions(out startFormNo, out endFormNo, out exportWord, out exportPdf))
                return;

            if (exportPdf)
            {
                // آموزش — رفعِ اشکالِ واقعیِ گزارش‌شده («خروجی جمعیِ PDF خراب است»):
                // این بررسیِ پیشین قبلاً فقط GetLibreOfficePath (متدِ خصوصیِ همین
                // فرم، مخصوصِ LibreOffice) را صدا می‌زد؛ روی سیستمی که Word نصب
                // دارد ولی LibreOffice ندارد، همیشه استثنا می‌داد و خروجیِ جمعیِ
                // PDF را متوقف می‌کرد — حتی وقتی PdfConversionHelper (که واقعاً
                // برای تبدیلِ هر فایل استفاده می‌شود) با همان Word به‌خوبی کار
                // می‌کرد. حالا دقیقاً همان بررسی‌ای انجام می‌شود که واقعاً برای
                // تبدیل استفاده خواهد شد: PdfConversionHelper.IsAvailable()
                // (Word یا LibreOffice، هرکدام موجود بود).
                if (!PdfConversionHelper.IsAvailable())
                {
                    string msg =
                        "برای ساخت PDF باید Microsoft Word یا LibreOffice نصب باشد." +
                        Environment.NewLine +
                        "فعلاً می‌توانید از خروجی Word استفاده کنید.";

                    if (!exportWord)
                    {
                        Msg.Show(msg);
                        return;
                    }

                    DialogResult dr = Msg.Show(
                        "PDF فعلاً ساخته نمی‌شود:" +
                        Environment.NewLine +
                        msg +
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
                DataTable cases = GetCasesForBatchExport(startFormNo, endFormNo, filter);

                if (cases.Rows.Count == 0)
                {
                    Msg.Show("در این بازه شماره فرم/فیلترها هیچ پرونده‌ای پیدا نشد");
                    return;
                }

                // یک‌بار برای کلِ دسته پرسیده می‌شود، نه برای هر پرونده. همان
                // دیالوگِ انتخابِ الگوی خروجیِ تک‌پرونده‌ای استفاده می‌شود تا
                // منطقِ انتخابِ الگو بین خروجیِ تکی و جمعی یکسان بماند — به
                // درخواستِ کاربر، الگوی قدیمیِ RDLC هم برای خروجیِ جمعی قابلِ
                // انتخاب است (هر پرونده مستقیم رندر می‌شود، بدون فایلِ میانی).
                string templatePath = ChooseWordTemplatePath(true);
                if (string.IsNullOrEmpty(templatePath)) return;     // انصراف کاربر

                bool isRdlc = templatePath == ReportTemplateHelper.RdlcKey;
                OpenXmlCaseExporter exporter = new OpenXmlCaseExporter();

                Cursor = Cursors.WaitCursor;
                btnBatchExport.Enabled = false;
                btnBatchExport.Text = "در حال ساخت...";

                bool exportWordLocal = exportWord;
                bool exportPdfLocal = exportPdf;

                await Task.Run(() =>
                {
                    if (isRdlc)
                    {
                        // الگوی قدیمی: هر پرونده مستقیماً از RDLC رندر می‌شود
                        // (RdlcExportHelper)، بدون Word و بدون فایلِ میانیِ docx.
                        foreach (DataRow row in cases.Rows)
                        {
                            string caseCode = "", formNo = "";
                            try
                            {
                                int caseId = Convert.ToInt32(row["CasID"]);
                                caseCode = row["Code"] == DBNull.Value ? "" : row["Code"].ToString();
                                formNo = row["FormNo"] == DBNull.Value ? "" : row["FormNo"].ToString();
                                if (string.IsNullOrWhiteSpace(caseCode))
                                    throw new Exception("کد اختصاصی پرونده خالی است");

                                if (exportWordLocal)
                                {
                                    string tempDoc = Path.Combine(Path.GetTempPath(),
                                        CleanFileName(caseCode) + "_" + Guid.NewGuid().ToString("N") + ".doc");
                                    try
                                    {
                                        RdlcExportHelper.ExportCaseToWord(caseId, tempDoc);
                                        SaveGeneratedFileToCaseCodeFolder(tempDoc, caseCode);
                                        wordCount++;
                                    }
                                    finally
                                    {
                                        try { if (File.Exists(tempDoc)) File.Delete(tempDoc); } catch { }
                                    }
                                }

                                if (exportPdfLocal)
                                {
                                    string tempPdf = Path.Combine(Path.GetTempPath(),
                                        CleanFileName(caseCode) + "_" + Guid.NewGuid().ToString("N") + ".pdf");
                                    try
                                    {
                                        RdlcExportHelper.ExportCaseToPdf(caseId, tempPdf);
                                        SaveGeneratedFileToCaseCodeFolder(tempPdf, caseCode);
                                        pdfCount++;
                                    }
                                    finally
                                    {
                                        try { if (File.Exists(tempPdf)) File.Delete(tempPdf); } catch { }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add("فرم " + formNo + " / کد " + caseCode + ": " + ex.Message);
                            }
                        }
                        return;
                    }

                    // آموزش — رفعِ ناپایداریِ گزارش‌شده («خروجی جمعیِ PDF خراب
                    // است»): قبلاً هر پرونده PdfConversionHelper.ConvertDocxToPdf
                    // را جداگانه صدا می‌زد و آن متد برای *هر فایل* یک نمونه‌ی
                    // تازه‌ی Word باز و بسته می‌کرد — روی دسته‌های بزرگ کند و
                    // مستعدِ خطا/فرآیندِ باقی‌مانده بود. حالا: اول همه‌ی فایل‌های
                    // docx ساخته می‌شوند، بعد (اگر PDF خواسته شده) یک‌جا با
                    // ConvertManyDocxToPdf که فقط *یک* نمونه‌ی Word برای کلِ
                    // دسته باز می‌کند تبدیل می‌شوند.
                    var pending = new List<PendingBatchFile>();
                    foreach (DataRow row in cases.Rows)
                    {
                        string caseCode = "", formNo = "", tempDocx = "";
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
                                pending.Add(new PendingBatchFile
                                {
                                    CaseCode = caseCode,
                                    FormNo = formNo,
                                    TempDocx = tempDocx
                                });
                            }
                            else
                            {
                                try { if (File.Exists(tempDocx)) File.Delete(tempDocx); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add("فرم " + formNo + " / کد " + caseCode + ": " + ex.Message);
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(tempDocx) && File.Exists(tempDocx))
                                    File.Delete(tempDocx);
                            }
                            catch { }
                        }
                    }

                    if (exportPdfLocal && pending.Count > 0)
                    {
                        var byPath = new Dictionary<string, PendingBatchFile>(StringComparer.OrdinalIgnoreCase);
                        var docxPaths = new List<string>();
                        foreach (PendingBatchFile p in pending)
                        {
                            docxPaths.Add(p.TempDocx);
                            byPath[p.TempDocx] = p;
                        }

                        PdfConversionHelper.ConvertManyDocxToPdf(docxPaths, delegate (string docxPath, string pdfPath, Exception exOne)
                        {
                            PendingBatchFile info = byPath[docxPath];
                            if (exOne != null)
                            {
                                errorCount++;
                                errors.Add("فرم " + info.FormNo + " / کد " + info.CaseCode + " (PDF): " + exOne.Message);
                                return;
                            }
                            try
                            {
                                SaveGeneratedFileToCaseCodeFolder(pdfPath, info.CaseCode);
                                pdfCount++;
                            }
                            catch (Exception exSave)
                            {
                                errorCount++;
                                errors.Add("فرم " + info.FormNo + " / کد " + info.CaseCode + " (ذخیره PDF): " + exSave.Message);
                            }
                            finally
                            {
                                try { if (File.Exists(pdfPath)) File.Delete(pdfPath); } catch { }
                            }
                        }, null);

                        foreach (PendingBatchFile p in pending)
                        {
                            try { if (File.Exists(p.TempDocx)) File.Delete(p.TempDocx); } catch { }
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

using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CaseManagement
{
    /// <summary>
    /// فرم تنظیمات نرم‌افزار (Control Center) — مرکز مدیریت کل نرم‌افزار.
    /// تب‌های فعلی: اطلاعات مؤسسه، مدیریت مراکز، اطلاعات پایه (Lookup).
    /// تب‌های باقی‌مانده (شماره‌گذاری/مسیرها/ظاهر/چاپ/امنیت/Backup/اعلان‌ها/
    /// نگهداری/درباره) در دسته‌های بعدی اضافه می‌شوند.
    /// </summary>
    public class FrmSettings : Form
    {
        private readonly DatabaseHelper _db = new DatabaseHelper();

        // ─── مراکز ─────────────────────────────────────────────────────────
        private DataGridView _gridCenters;
        private TextBox  _txtCenterCode;
        private TextBox  _txtCenterName;
        private ComboBox _cmbCenterProvince;
        private TextBox  _txtCenterAddress;
        private TextBox  _txtCenterPhone;
        private TextBox  _txtCenterManager;
        private TextBox  _txtCenterEmail;
        private TextBox  _txtCenterLogoPath;
        private PictureBox _picCenterLogo;
        private Panel    _pnlCenterColorSwatch;
        private Color    _selectedCenterColor = UiTheme.Primary;
        private TextBox  _txtCenterSearch;
        private int      _editingCenterId = 0;

        // ─── مقادیر پایه ───────────────────────────────────────────────────
        private ComboBox _cmbCategory;
        private DataGridView _gridLookup;
        private TextBox _txtLookupValue;
        private TextBox _txtLookupSearch;
        private int     _editingLookupId = 0;

        // ─── شماره‌گذاری ───────────────────────────────────────────────────
        private NumericUpDown _numStartFamilyNo;
        private NumericUpDown _numStartDocNo;
        private NumericUpDown _numStartReportNo;

        // ─── مسیرها و فایل‌ها ──────────────────────────────────────────────
        private TextBox _txtReportsPath;
        private TextBox _txtLogsPath;
        private TextBox _txtTempPath;

        // ─── نگهداری سیستم ─────────────────────────────────────────────────
        private Label _lblMaintenanceStats;
        private TextBox _txtMaintenanceOutput;

        // ─── امنیت ─────────────────────────────────────────────────────────
        private NumericUpDown _numMinPasswordLength;
        private NumericUpDown _numMaxFailedAttempts;
        private NumericUpDown _numLockoutMinutes;
        private NumericUpDown _numSessionTimeoutMinutes;
        private NumericUpDown _numForcePasswordChangeDays;
        private CheckBox      _chkAuditEnabled;

        // ─── Backup ────────────────────────────────────────────────────────
        private RadioButton _radBackupDaily;
        private RadioButton _radBackupWeekly;
        private RadioButton _radBackupMonthly;
        private NumericUpDown _numBackupRetention;
        private Label _lblBackupStatus;
        private TextBox _txtBackupOutput;

        // ─── اعلان‌ها ───────────────────────────────────────────────────────
        private CheckBox _chkNotifyBackupMissing;
        private CheckBox _chkNotifyLowDisk;
        private CheckBox _chkNotifyIncompleteCase;
        private CheckBox _chkNotifyNoPhoto;
        private CheckBox _chkNotifyNoDocs;
        private CheckBox _chkNotifyIncompleteFamily;
        private CheckBox _chkNotifyIncompleteFinance;

        // ─── ظاهر نرم‌افزار ────────────────────────────────────────────────
        private Panel _pnlSuccessSwatch, _pnlDangerSwatch, _pnlWarningSwatch;
        private Color _selectedSuccessColor = UiTheme.Success;
        private Color _selectedDangerColor = UiTheme.Danger;
        private Color _selectedWarningColor = UiTheme.Warning;
        private RadioButton _radThemeLight, _radThemeDark;
        private ComboBox _cmbFontFamily;
        private NumericUpDown _numFontSize;
        private Panel _pnlAppearancePreview;

        // ─── چاپ و گزارش ────────────────────────────────────────────────────
        private ComboBox _cmbDefaultPrinter;
        private ComboBox _cmbPaperSize;
        private NumericUpDown _numMarginTop, _numMarginBottom, _numMarginLeft, _numMarginRight;
        private CheckBox _chkShowLogoOnPrint, _chkShowStamp, _chkShowSignature;
        private TextBox _txtStampPath, _txtSignaturePath;

        // ─── درباره نرم‌افزار ──────────────────────────────────────────────
        private TextBox _txtDeveloperName;
        private TextBox _txtLicenseInfo;
        private TextBox _txtMachineId;

        // ─── تنظیمات عمومی / اطلاعات مؤسسه ───────────────────────────────
        private TextBox _txtOrgName;
        private TextBox _txtOrgNameEn;
        private TextBox _txtSlogan;
        private TextBox _txtOrgCode;
        private TextBox _txtRegNumber;
        private TextBox _txtManagerName;
        private TextBox _txtLogoPath;
        private TextBox _txtAddress;
        private TextBox _txtGeneralPhone;
        private TextBox _txtMobile;
        private TextBox _txtWhatsApp;
        private TextBox _txtEmail;
        private TextBox _txtWebsite;
        private TextBox _txtDescription;
        private TextBox _txtBackupPath;
        private TextBox _txtManualPath;
        private CheckedListBox _clbCaseGridColumns;
        private TextBox _txtPhotoStoragePath;
        private NumericUpDown _numStartCaseNo;
        private NumericUpDown _numStartReceiptNo;
        private Panel _pnlColorSwatch;
        private Color _selectedThemeColor = UiTheme.Primary;
        private Panel _pnlFontColorSwatch;
        private Color _selectedFontColor = UiTheme.TextDark;
        private ComboBox _cmbDashboardRows;
        private PictureBox _picLogoPreview;
        // آموزش — امضا/مهر مؤسسه (برای کارت شناسایی سرپرست و اسناد چاپی):
        // فیلدهای _txtSignaturePath/_txtStampPath از قبل برای یک تب «چاپ و
        // گزارش» نیمه‌کاره تعریف شده بودند ولی هیچ UI برایشان ساخته نشده بود
        // (warning CS0169). همان دو فیلد اینجا با UI واقعی (کنار لوگو) استفاده
        // می‌شوند؛ کلید تنظیمات هم همانی است که از قبل وجود داشت.
        private PictureBox _picSignaturePreview, _picStampPreview;

        // آموزش — ردیف پیش‌فرض‌های رنگ (بازطراحی ظاهری): علاوه بر دکمه‌ی «انتخاب
        // رنگ» موجود (که هر رنگ دلخواه را می‌دهد و دست‌نخورده مانده)، چند رنگ
        // پرکاربرد به‌صورت دکمه‌ی گرد قابل‌کلیک اضافه شد تا انتخاب سریع‌تر باشد.
        private readonly List<ColorSwatchButton> _themeColorSwatches = new List<ColorSwatchButton>();
        private static readonly Color[] PresetThemeColors =
        {
            ColorTranslator.FromHtml("#2F6FED"), ColorTranslator.FromHtml("#8B5CF6"),
            ColorTranslator.FromHtml("#14B8A6"), ColorTranslator.FromHtml("#F97316"),
            ColorTranslator.FromHtml("#EF4444"), ColorTranslator.FromHtml("#334155")
        };

        public FrmSettings()
        {
            BuildUi();

            // ⚠ این فرم ۱۱ تب دارد و هر تب دکمه‌ی «ذخیره»ی خودش را. پس Ctrl+S
            // عمداً به یک دکمه‌ی ثابت بسته نشده، بلکه در لحظه‌ی فشردن، دکمه‌ی
            // ذخیره‌ی *تبِ نمایان* را پیدا می‌کند. بستنش به دکمه‌ای مشخص یعنی
            // ذخیره‌شدنِ تنظیماتِ تبی که کاربر اصلاً در آن نیست.
            Helpers.FormShortcuts.For(this).SaveVisible();
        }

        private void BuildUi()
        {
            Text              = "تنظیمات نرم‌افزار — مرکز مدیریت";
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor         = UiTheme.Background;
            Font              = UiTheme.Font(UiTheme.SizeBody);
            // آموزش — پنجره عریض‌تر/بلندتر شد تا (الف) ۱۱ تب در نوار Pill بدون
            // بریدگی جا شوند و (ب) محتوای کارت‌ها بدون اسکرول دیده شود — هر دو
            // ایرادی که کاربر روی نسخه‌ی قبلی گرفت.
            UiTheme.MakeMainWindow(this, 1160, 730);

            // آموزش — بازطراحی ظاهری (به درخواست کاربر): TabControl بومیِ ویندوز
            // با یک نوار «دکمه‌های بیضی‌شکل» (PillTabStrip) جایگزین شد. WinForms
            // صریحاً اجازه نمی‌دهد TabPage به هر کانتینری غیر از TabControl
            // اضافه شود (ArgumentException در زمان اجرا) — پس این ۱۱ صفحه حالا
            // Panel ساده‌اند، نه TabPage؛ چون همه‌ی متدهای BuildXxxTab فقط از
            // اعضای سطح Panel (Controls.Add/BackColor) استفاده می‌کردند، این
            // تغییرِ نوع پارامتر است، نه تغییرِ منطق — هیچ متدی داخلش عوض نشده.
            Panel tabGeneral      = new Panel();
            Panel tabCenters      = new Panel();
            Panel tabNumbering    = new Panel();
            Panel tabPaths        = new Panel();
            Panel tabSecurity     = new Panel();
            Panel tabBackup       = new Panel();
            Panel tabNotify       = new Panel();
            Panel tabLookup       = new Panel();
            Panel tabMaintenance  = new Panel();
            Panel tabDeleteCases  = new Panel();
            Panel tabAbout        = new Panel();
            Panel tabLanguage     = new Panel();   // تبِ تازه‌ی زبان (افزایشی)
            Panel tabGuardianCard = new Panel();   // متن‌های کارت شناسایی (افزایشی)
            Panel tabAssistancePackages = new Panel(); // بسته‌های مساعدتِ غیرنقدی (افزایشی)
            tabGuardianCard.BackColor = UiTheme.Background;
            tabAssistancePackages.BackColor = UiTheme.Background;
            tabDeleteCases.BackColor = UiTheme.Background;
            tabAbout.BackColor       = UiTheme.Background;
            tabGeneral.BackColor     = UiTheme.Background;
            tabCenters.BackColor     = UiTheme.Background;
            tabNumbering.BackColor   = UiTheme.Background;
            tabPaths.BackColor       = UiTheme.Background;
            tabSecurity.BackColor    = UiTheme.Background;
            tabBackup.BackColor      = UiTheme.Background;
            tabNotify.BackColor      = UiTheme.Background;
            tabLookup.BackColor      = UiTheme.Background;
            tabMaintenance.BackColor = UiTheme.Background;

            BuildGeneralTab(tabGeneral);
            BuildCentersTab(tabCenters);
            BuildNumberingTab(tabNumbering);
            BuildPathsTab(tabPaths);
            BuildSecurityTab(tabSecurity);
            BuildBackupTab(tabBackup);
            BuildNotificationsTab(tabNotify);
            BuildLookupTab(tabLookup);
            BuildMaintenanceTab(tabMaintenance);
            if (SecurityContext.CanDelete())
                BuildDeleteCasesTab(tabDeleteCases);
            BuildAboutTab(tabAbout);
            BuildLanguageTab(tabLanguage);
            BuildGuardianCardTab(tabGuardianCard);
            BuildAssistancePackagesTab(tabAssistancePackages);

            Panel contentHost = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background };
            PillTabStrip pillTabs = new PillTabStrip();
            List<Panel> pages = new List<Panel>();

            Action<string, Panel> addPage = delegate (string label, Panel page)
            {
                page.Dock = DockStyle.Fill;
                page.Visible = false;
                contentHost.Controls.Add(page);
                pages.Add(page);
                pillTabs.AddTab(label);
            };

            // آموزش — برچسب‌ها عمداً کوتاه‌اند تا هر ۱۱ تب در یک ردیف جا شوند
            // (با برچسب‌های بلند، نوار به دو ردیف می‌شکست و نامرتب می‌شد) —
            // همان الگوی عکسِ نمونه که برچسب‌های تک‌کلمه‌ای دارد.
            addPage("مؤسسه", tabGeneral);

            // آموزش — رفع باگ امنیتی چندمرکزی: «مدیریت مراکز» و «Backup/Restore»
            // کل سیستم را تحت تأثیر قرار می‌دهند (همه مراکز)، پس فقط SuperAdmin
            // این دو تب را می‌بیند؛ Admin مرکز (نه SuperAdmin) اصلاً این
            // امکانات را نمی‌بیند، نه فقط دکمه‌هایش غیرفعال باشد.
            if (SecurityContext.IsSuperAdmin())
                addPage("مراکز", tabCenters);

            addPage("شماره‌گذاری", tabNumbering);
            addPage("فایل‌ها", tabPaths);
            addPage("امنیت", tabSecurity);

            if (SecurityContext.IsSuperAdmin())
                addPage("پشتیبان‌گیری", tabBackup);

            addPage("اعلان‌ها", tabNotify);
            addPage("کارت شناسایی", tabGuardianCard);
            addPage("بسته‌های مساعدت", tabAssistancePackages);
            addPage("زبان", tabLanguage);
            addPage("اطلاعات پایه", tabLookup);
            addPage("نگهداری", tabMaintenance);
            // حذف پرونده‌ها فقط برای کاربر دارای مجوز حذف (مدیر) نمایش داده می‌شود.
            if (SecurityContext.CanDelete())
                addPage("حذف پرونده", tabDeleteCases);
            addPage("درباره", tabAbout);

            pillTabs.SelectedIndexChanged += delegate
            {
                for (int i = 0; i < pages.Count; i++)
                    pages[i].Visible = (i == pillTabs.SelectedIndex);
            };

            Controls.Add(contentHost);
            Controls.Add(pillTabs);

            pillTabs.SelectedIndex = 0; // تب اول پیش‌فرض (همان رفتار قبلی)
        }

        // ══════════════════════════════════════════════════════════════════
        // تب: حذف پرونده‌ها (دسته‌جمعی/تکی) — فقط برای مدیر (CanDelete)
        // آموزش — عملیات پرخطر: با تیک هر ردیف (یا «انتخاب همه») پرونده‌ها
        // انتخاب و پس از تأیید صریح حذف می‌شوند. دو حالت: «فقط دیتابیس» یا
        // «کامل (همراه پوشه‌ی فایل‌ها)». حذف TblCase با FK آبشاری، اعضا/اسناد/
        // کمک‌های همان پرونده را هم پاک می‌کند. همه‌چیز به مرکز کاربر محدود است.
        //
        // آموزش — بازبینیِ کامل (به درخواستِ کاربر، پس از یک گزارشِ بررسی):
        //   ۱. بکاپِ کاملِ *اجباری* پیش از هر حذف — دقیقاً همان الگوی
        //      SyncEngine.Apply («اگر بکاپ نشد، ادامه نده»). این مهم‌ترین
        //      نقصِ نسخه‌ی قبلی بود: حذف «قابل‌بازگشت نیست» ولی هیچ بکاپی
        //      نمی‌گرفت.
        //   ۲. فیلترِ ولایت/ولسوالی/کدِاختصاصی/شماره‌فرم — کنارِ جست‌جوی سریعِ
        //      قبلی (کد+نام)، نه به‌جایش.
        //   ۳. سقفِ نمایش (همان MaxGridRows که در FrmCase برای رفعِ کندیِ
        //      ~۱۲هزار پرونده استفاده شد) تا این تب هم بدونِ فیلتر کند نشود.
        //   ۴. دیالوگِ تأیید حالا فهرستِ واقعیِ کد/نامِ انتخاب‌شده‌ها را نشان
        //      می‌دهد (تا سقفی معقول)، نه فقط یک عدد.
        //   ۵. برای حذفِ «کامل»، یک تأییدِ دوم با تایپِ عددِ دقیقِ تعداد.
        //   ۶. هشدارِ صریح کنارِ گزینه‌ی «فقط دیتابیس» دربارهٔ فایل‌های یتیم.
        //   ۷. یک اتصالِ مشترک برای کلِ دسته به‌جای بازکردنِ اتصالِ جدید به
        //      ازای هر پرونده.
        //   ۸. کلیدِ Enter در جعبه‌ی جست‌جو.
        //   ۹. شماره‌ی ردیف (با همان منطقِ راست‌به‌چپِ درست‌شده‌ی FrmCase).
        //  ۱۰. رنگ‌آمیزیِ ردیفِ تیک‌خورده (هشدارِ بصری).
        // ══════════════════════════════════════════════════════════════════
        private DataGridView _gridDeleteCases;
        private TextBox _txtDeleteSearch;
        private ComboBox _cmbDeleteProvince;
        private ComboBox _cmbDeleteDistrict;
        private TextBox _txtDeleteCodeFilter;
        private TextBox _txtDeleteFormNoFilter;
        private RadioButton _radDeleteDbOnly;
        private RadioButton _radDeleteComplete;
        private Label _lblDeleteCount;

        private const string DelSelCol = "sel";
        private const string DelIdCol = "CasID";
        private const string DelCodeCol = "Code";
        private const int MaxDeleteGridRows = 500;

        private void BuildDeleteCasesTab(Panel tab)
        {
            Panel top = new Panel { Dock = DockStyle.Top, Height = 178, BackColor = UiTheme.CardBack, Padding = new Padding(14, 6, 14, 4) };

            // ── ردیف ۱: جست‌جوی سریع (کد یا نام) ────────────────────────────
            FlowLayoutPanel searchFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.RightToLeft
            };
            searchFlow.Controls.Add(new Label
            {
                Text = "جست‌جوی سریع (کد یا نام):", AutoSize = false, Width = 150, Height = 28,
                TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(4, 10, 2, 4)
            });
            _txtDeleteSearch = new TextBox { Width = 220, Height = 28 };
            UiTheme.StyleTextBox(_txtDeleteSearch);
            _txtDeleteSearch.Margin = new Padding(2, 8, 4, 4);
            _txtDeleteSearch.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadDeleteCases(); }
            };
            searchFlow.Controls.Add(_txtDeleteSearch);

            Button btnSelectAll = UiTheme.CreateSecondaryButton("انتخاب همه", "☑");
            btnSelectAll.Size = new Size(120, 30); btnSelectAll.Margin = new Padding(4, 7, 4, 4);
            btnSelectAll.Click += delegate { SetAllDeleteSelection(true); };
            searchFlow.Controls.Add(btnSelectAll);

            Button btnSelectNone = UiTheme.CreateSecondaryButton("لغو انتخاب", "☐");
            btnSelectNone.Size = new Size(120, 30); btnSelectNone.Margin = new Padding(4, 7, 4, 4);
            btnSelectNone.Click += delegate { SetAllDeleteSelection(false); };
            searchFlow.Controls.Add(btnSelectNone);

            top.Controls.Add(searchFlow);

            // ── ردیف ۲: فیلترِ ولایت/ولسوالی/کدِاختصاصی/شماره‌فرم ────────────
            FlowLayoutPanel filterFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.RightToLeft
            };

            _cmbDeleteProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Height = 28, Margin = new Padding(2, 6, 4, 4) };
            LookupHelper.FillCombo(_cmbDeleteProvince, "Province", "همه ولایت‌ها");
            _cmbDeleteProvince.SelectedIndexChanged += delegate { LoadDeleteDistrictFilter(); };
            filterFlow.Controls.Add(_cmbDeleteProvince);

            _cmbDeleteDistrict = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Height = 28, Margin = new Padding(2, 6, 8, 4) };
            filterFlow.Controls.Add(_cmbDeleteDistrict);
            LoadDeleteDistrictFilter();

            _txtDeleteCodeFilter = new TextBox { Width = 130, Height = 28 };
            UiTheme.StyleTextBox(_txtDeleteCodeFilter);
            _txtDeleteCodeFilter.Margin = new Padding(2, 6, 2, 4);
            filterFlow.Controls.Add(_txtDeleteCodeFilter);
            filterFlow.Controls.Add(new Label { Text = "کد اختصاصی:", AutoSize = false, Width = 80, Height = 28, TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(4, 10, 0, 4) });

            _txtDeleteFormNoFilter = new TextBox { Width = 100, Height = 28 };
            UiTheme.StyleTextBox(_txtDeleteFormNoFilter);
            _txtDeleteFormNoFilter.Margin = new Padding(2, 6, 2, 4);
            filterFlow.Controls.Add(_txtDeleteFormNoFilter);
            filterFlow.Controls.Add(new Label { Text = "شماره فرم:", AutoSize = false, Width = 76, Height = 28, TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(4, 10, 0, 4) });

            Button btnApplyFilter = UiTheme.CreateButton("اعمال فیلتر", "⌕", UiTheme.Primary);
            btnApplyFilter.Size = new Size(110, 28); btnApplyFilter.Margin = new Padding(8, 6, 4, 4);
            btnApplyFilter.Click += delegate { LoadDeleteCases(); };
            filterFlow.Controls.Add(btnApplyFilter);

            Button btnClearFilter = UiTheme.CreateSecondaryButton("پاک‌سازی فیلتر", "✕");
            btnClearFilter.Size = new Size(110, 28); btnClearFilter.Margin = new Padding(2, 6, 4, 4);
            btnClearFilter.Click += delegate
            {
                _cmbDeleteProvince.SelectedIndex = 0;
                LoadDeleteDistrictFilter();
                _txtDeleteCodeFilter.Text = "";
                _txtDeleteFormNoFilter.Text = "";
                _txtDeleteSearch.Text = "";
                LoadDeleteCases();
            };
            filterFlow.Controls.Add(btnClearFilter);

            top.Controls.Add(filterFlow);

            // نوار گزینه‌ها + دکمه حذف
            FlowLayoutPanel optFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 64, FlowDirection = FlowDirection.RightToLeft, WrapContents = true
            };
            _radDeleteDbOnly = new RadioButton
            {
                Text = "فقط از نرم‌افزار (دیتابیس)", Checked = true, AutoSize = true,
                Font = UiTheme.Font(UiTheme.SizeBody), Margin = new Padding(6, 10, 6, 4)
            };
            _radDeleteComplete = new RadioButton
            {
                Text = "کامل (همراه پوشه‌ی فایل‌ها)", AutoSize = true,
                Font = UiTheme.Font(UiTheme.SizeBody), Margin = new Padding(6, 10, 6, 4)
            };
            optFlow.Controls.Add(_radDeleteDbOnly);
            optFlow.Controls.Add(_radDeleteComplete);

            Button btnDelete = UiTheme.CreateButton("حذف انتخاب‌شده‌ها", "✕", UiTheme.Danger);
            btnDelete.Size = new Size(180, 32); btnDelete.Margin = new Padding(16, 6, 6, 4);
            btnDelete.Click += BtnDeleteCases_Click;
            optFlow.Controls.Add(btnDelete);

            _lblDeleteCount = new Label
            {
                AutoSize = false, Width = 180, Height = 30, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(UiTheme.SizeSmall), Margin = new Padding(6, 8, 6, 4)
            };
            optFlow.Controls.Add(_lblDeleteCount);

            Label lblOrphanWarning = new Label
            {
                Text = "توجه: در حالتِ «فقط دیتابیس»، عکس/سند/خروجیِ همان پرونده روی دیسک باقی می‌ماند (بدونِ ارتباط با هیچ پرونده‌ای).",
                AutoSize = false, Width = 900, Height = 30, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.Warning, Font = UiTheme.Font(UiTheme.SizeSmall), Margin = new Padding(6, 8, 6, 4)
            };
            optFlow.Controls.Add(lblOrphanWarning);

            top.Controls.Add(optFlow);

            _gridDeleteCases = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = true, RowHeadersWidth = 44, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(_gridDeleteCases);
            _gridDeleteCases.Columns.Add(new DataGridViewCheckBoxColumn { Name = DelSelCol, HeaderText = "انتخاب", FillWeight = 30 });
            _gridDeleteCases.Columns.Add(new DataGridViewTextBoxColumn { Name = DelIdCol, HeaderText = "شناسه", ReadOnly = true, FillWeight = 30, Visible = false });
            _gridDeleteCases.Columns.Add(new DataGridViewTextBoxColumn { Name = DelCodeCol, HeaderText = "کد اختصاصی", ReadOnly = true, FillWeight = 60 });
            _gridDeleteCases.Columns.Add(new DataGridViewTextBoxColumn { Name = "HeadFullName", HeaderText = "نام سرپرست", ReadOnly = true, FillWeight = 110 });
            _gridDeleteCases.Columns.Add(new DataGridViewTextBoxColumn { Name = "Province", HeaderText = "ولایت", ReadOnly = true, FillWeight = 60 });
            _gridDeleteCases.Columns.Add(new DataGridViewTextBoxColumn { Name = "District", HeaderText = "ولسوالی", ReadOnly = true, FillWeight = 60 });
            _gridDeleteCases.Columns.Add(new DataGridViewTextBoxColumn { Name = "FormNo", HeaderText = "شماره فرم", ReadOnly = true, FillWeight = 50 });
            _gridDeleteCases.Columns.Add(new DataGridViewTextBoxColumn { Name = "ServiceStatus", HeaderText = "وضعیت", ReadOnly = true, FillWeight = 60 });
            _gridDeleteCases.CurrentCellDirtyStateChanged += delegate
            {
                if (_gridDeleteCases.IsCurrentCellDirty)
                    _gridDeleteCases.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _gridDeleteCases.CellValueChanged += delegate (object s, DataGridViewCellEventArgs e)
            {
                UpdateDeleteCount();
                if (e.ColumnIndex >= 0 && e.RowIndex >= 0 && _gridDeleteCases.Columns[e.ColumnIndex].Name == DelSelCol)
                    ColorDeleteRow(_gridDeleteCases.Rows[e.RowIndex]);
            };
            _gridDeleteCases.RowPostPaint += DgvDeleteCases_RowPostPaint;

            tab.Controls.Add(_gridDeleteCases);
            tab.Controls.Add(top);

            LoadDeleteCases();
        }

        // پر کردنِ کمبوی ولسوالی بر اساسِ ولایتِ انتخاب‌شده (همان الگوی
        // FrmDashboard.LoadFilterDistricts).
        private void LoadDeleteDistrictFilter()
        {
            if (_cmbDeleteDistrict == null) return;
            _cmbDeleteDistrict.Items.Clear();
            _cmbDeleteDistrict.Items.Add("همه ولسوالی‌ها");

            if (_cmbDeleteProvince.SelectedIndex > 0)
            {
                try
                {
                    foreach (string d in AfghanGeoData.GetDistricts(_cmbDeleteProvince.Text.Trim()))
                        _cmbDeleteDistrict.Items.Add(d);
                }
                catch { }
            }
            _cmbDeleteDistrict.SelectedIndex = 0;
        }

        // رسمِ شماره‌ی ردیف — همان منطقِ راست‌به‌چپِ درست‌شده‌ی FrmCase
        // (DgvCases_RowPostPaint)، چون این گرید هم RightToLeft=Yes دارد.
        private void DgvDeleteCases_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            string rowNumber = (e.RowIndex + 1).ToString();
            SizeF size = e.Graphics.MeasureString(rowNumber, _gridDeleteCases.RowHeadersDefaultCellStyle.Font ?? _gridDeleteCases.Font);

            int headerLeft = _gridDeleteCases.RightToLeft == RightToLeft.Yes
                ? e.RowBounds.Right
                : e.RowBounds.Left - _gridDeleteCases.RowHeadersWidth;
            Rectangle headerBounds = new Rectangle(headerLeft, e.RowBounds.Top, _gridDeleteCases.RowHeadersWidth, e.RowBounds.Height);

            e.Graphics.DrawString(rowNumber, _gridDeleteCases.Font, SystemBrushes.ControlText,
                headerBounds.Left + (headerBounds.Width - size.Width) / 2,
                headerBounds.Top + (headerBounds.Height - size.Height) / 2);
        }

        // هشدارِ بصری: ردیفی که برای حذف تیک خورده، پس‌زمینه‌ی قرمزِ کم‌رنگ می‌گیرد.
        private void ColorDeleteRow(DataGridViewRow row)
        {
            bool selected = Convert.ToBoolean(row.Cells[DelSelCol].Value ?? false);
            row.DefaultCellStyle.BackColor = selected ? Color.FromArgb(255, 235, 235) : Color.Empty;
        }

        private void LoadDeleteCases()
        {
            string term = _txtDeleteSearch == null ? "" : _txtDeleteSearch.Text.Trim();
            string province = (_cmbDeleteProvince == null || _cmbDeleteProvince.SelectedIndex <= 0) ? "" : _cmbDeleteProvince.Text.Trim();
            string district = (_cmbDeleteDistrict == null || _cmbDeleteDistrict.SelectedIndex <= 0) ? "" : _cmbDeleteDistrict.Text.Trim();
            string code = _txtDeleteCodeFilter == null ? "" : _txtDeleteCodeFilter.Text.Trim();
            string formNo = _txtDeleteFormNoFilter == null ? "" : _txtDeleteFormNoFilter.Text.Trim();

            _gridDeleteCases.Rows.Clear();

            using (var con = _db.GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT CasID, Code, HeadFullName, Province, District, FormNo, ServiceStatus
FROM TblCase
WHERE (@CID = 0 OR CenterID = @CID)
  AND (@Term = '' OR Code LIKE '%' || @Term || '%' OR HeadFullName LIKE '%' || @Term || '%')
  AND (@Prov = '' OR Province = @Prov)
  AND (@Dist = '' OR District LIKE '%' || @Dist || '%')
  AND (@Code = '' OR Code LIKE '%' || @Code || '%')
  AND (@FormNo = '' OR CAST(FormNo AS TEXT) LIKE '%' || @FormNo || '%')
ORDER BY CasID DESC
LIMIT " + MaxDeleteGridRows, con))
            {
                cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);
                cmd.Parameters.AddWithValue("@Term", term);
                cmd.Parameters.AddWithValue("@Prov", province);
                cmd.Parameters.AddWithValue("@Dist", district);
                cmd.Parameters.AddWithValue("@Code", code);
                cmd.Parameters.AddWithValue("@FormNo", formNo);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        _gridDeleteCases.Rows.Add(
                            false,
                            Convert.ToInt32(dr["CasID"]),
                            dr["Code"] == DBNull.Value ? "" : dr["Code"].ToString(),
                            dr["HeadFullName"] == DBNull.Value ? "" : dr["HeadFullName"].ToString(),
                            dr["Province"] == DBNull.Value ? "" : dr["Province"].ToString(),
                            dr["District"] == DBNull.Value ? "" : dr["District"].ToString(),
                            dr["FormNo"] == DBNull.Value ? "" : dr["FormNo"].ToString(),
                            dr["ServiceStatus"] == DBNull.Value ? "" : dr["ServiceStatus"].ToString());
                    }
                }
            }
            UpdateDeleteCount();
        }

        private void SetAllDeleteSelection(bool selected)
        {
            foreach (DataGridViewRow row in _gridDeleteCases.Rows)
            {
                if (row.IsNewRow) continue;
                row.Cells[DelSelCol].Value = selected;
                ColorDeleteRow(row);
            }
            UpdateDeleteCount();
        }

        private void UpdateDeleteCount()
        {
            if (_lblDeleteCount == null) return;
            int n = 0;
            foreach (DataGridViewRow row in _gridDeleteCases.Rows)
                if (!row.IsNewRow && Convert.ToBoolean(row.Cells[DelSelCol].Value ?? false)) n++;
            _lblDeleteCount.Text = string.Format(Lang.T("انتخاب‌شده: {0}"), n);
        }

        private void BtnDeleteCases_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.CanDelete())
            {
                UiTheme.ShowWarning(this, "حذف پرونده فقط برای مدیر مجاز است.");
                return;
            }

            var targets = new List<KeyValuePair<int, string>>(); // CasID -> Code
            var targetNames = new List<string>();
            foreach (DataGridViewRow row in _gridDeleteCases.Rows)
            {
                if (row.IsNewRow) continue;
                if (!Convert.ToBoolean(row.Cells[DelSelCol].Value ?? false)) continue;
                string code = (row.Cells[DelCodeCol].Value ?? "").ToString();
                targets.Add(new KeyValuePair<int, string>(Convert.ToInt32(row.Cells[DelIdCol].Value), code));
                targetNames.Add(code + " — " + (row.Cells["HeadFullName"].Value ?? ""));
            }

            if (targets.Count == 0)
            {
                UiTheme.ShowWarning(this, "هیچ پرونده‌ای انتخاب نشده است.");
                return;
            }

            bool complete = _radDeleteComplete.Checked;
            string mode = complete ? "به‌همراه پوشه‌ی فایل‌ها (عکس/سند/خروجی)" : "فقط از دیتابیس نرم‌افزار";

            // فهرستِ واقعیِ پرونده‌های انتخاب‌شده (تا سقفی معقول) در دیالوگِ تأیید.
            const int previewCap = 15;
            string preview = string.Join("\n", targetNames.Take(previewCap));
            if (targetNames.Count > previewCap)
                preview += "\n… و " + (targetNames.Count - previewCap) + " موردِ دیگر";

            if (!UiTheme.ShowConfirm(this,
                    "تعداد " + targets.Count + " پرونده " + mode + " حذف می‌شود:\n\n" + preview + "\n\n" +
                    "این عملیات همه‌ی اعضا، اسناد و کمک‌های همان پرونده‌ها را نیز حذف می‌کند و قابل بازگشت نیست.\n" +
                    "پیش از حذف، یک بکاپِ کامل خودکار گرفته می‌شود.\n" +
                    "آیا مطمئن هستید؟",
                    "تأیید حذف پرونده‌ها"))
                return;

            // برای حذفِ «کامل»، یک تأییدِ دومِ صریح (تایپِ عددِ دقیقِ تعداد) —
            // ریسکِ بیشتری دارد چون فایل‌ها هم برای همیشه پاک می‌شوند.
            if (complete && !ConfirmByTypingCount(targets.Count))
                return;

            // ── بکاپِ کاملِ *اجباری* پیش از هر حذف ──────────────────────────
            // آموزش — دقیقاً همان الگوی SyncEngine.Apply: اگر بکاپ نشد، اصلاً
            // ادامه نده. حذف «قابل بازگشت نیست»، پس این تنها تورِ ایمنی است.
            string backupPath;
            try
            {
                UseWaitCursor = true;
                backupPath = new BackupHelper().ExportBackup(CaseManagement.Sync.SyncEngine.ResolveBackupFolder());
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "بکاپ‌گیریِ اجباری پیش از حذف ناموفق بود؛ برای ایمنیِ داده، هیچ پرونده‌ای حذف نشد:\n" + ex.Message);
                return;
            }
            finally { UseWaitCursor = false; }

            int deleted = 0, folderFailed = 0;

            // ── یک اتصالِ مشترک برای کلِ دسته (به‌جای بازکردنِ اتصالِ جدید
            // به ازای هر پرونده) — کاراییِ بهتر برای دسته‌های بزرگ.
            using (var con = _db.GetConnection())
            {
                con.Open();

                foreach (var t in targets)
                {
                    try
                    {
                        // هویتِ رکورد *پیش از* حذف برداشته می‌شود (بعد از DELETE
                        // دیگر خواندنی نیست)، ولی فقط در صورتِ حذفِ واقعی ثبت
                        // می‌گردد — این حذف ممکن است به‌خاطرِ تعلق به مرکزِ
                        // دیگر انجام نشود.
                        var pendingDelete =
                            CaseManagement.Sync.SyncOutboxService.PrepareDelete("TblCase", t.Key);

                        int affected;
                        using (var cmd = new SQLiteCommand(
                            "DELETE FROM TblCase WHERE CasID = @Id AND (@CID = 0 OR CenterID = @CID)", con))
                        {
                            cmd.Parameters.AddWithValue("@Id", t.Key);
                            cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);
                            affected = cmd.ExecuteNonQuery();
                        }
                        if (affected == 0) continue; // متعلق به مرکز دیگر — رد شد

                        // حذف واقعاً انجام شد ⇒ حالا در صفِ همگام‌سازی ثبت می‌شود.
                        CaseManagement.Sync.SyncOutboxService.CommitDelete(pendingDelete);

                        // در حالتِ «کامل»، پوشه‌ی فایل‌های پرونده هم حذف می‌شود.
                        if (complete && !string.IsNullOrWhiteSpace(t.Value))
                        {
                            if (!FileHelper.DeleteCaseFolder(t.Value))
                                folderFailed++;
                        }

                        AuditLogger.Log("حذف پرونده", "TblCase", t.Key, "Code=" + t.Value,
                            complete ? "کامل (با پوشه)" : "فقط دیتابیس");
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[DeleteCase " + t.Key + "] " + ex.Message);
                    }
                }
            }

            string msg = deleted + " پرونده حذف شد.\nمسیرِ بکاپِ گرفته‌شده پیش از حذف:\n" + backupPath;
            if (folderFailed > 0)
                msg += "\nتوجه: پوشه‌ی " + folderFailed + " پرونده حذف نشد (شاید در حال استفاده بود).";
            UiTheme.ShowSuccess(this, msg);

            LoadDeleteCases();
        }

        // تأییدِ دومِ صریح برای حذفِ «کامل»: کاربر باید عددِ دقیقِ تعداد را
        // تایپ کند. یک دیالوگِ کوچکِ ساده، هم‌راستا با سبکِ بقیه‌ی فرم‌های
        // کوچکِ این پروژه (مثلِ ShowAddReminderDialog در FrmDashboard).
        private bool ConfirmByTypingCount(int expectedCount)
        {
            using (Form frm = new Form())
            {
                frm.Text = "تأییدِ نهاییِ حذفِ کامل";
                frm.RightToLeft = RightToLeft.Yes;
                frm.RightToLeftLayout = true;
                frm.FormBorderStyle = FormBorderStyle.FixedDialog;
                frm.StartPosition = FormStartPosition.CenterParent;
                frm.MaximizeBox = false;
                frm.MinimizeBox = false;
                frm.BackColor = UiTheme.Background;
                frm.Font = UiTheme.Font(UiTheme.SizeBody);
                UiTheme.MakeFixedSize(frm, 420, 190);

                Label lbl = new Label
                {
                    Text = "برای تأییدِ نهاییِ حذفِ کامل (همراه با فایل‌ها)، عددِ «" + expectedCount + "» را دقیقاً تایپ کنید:",
                    AutoSize = false, Location = new Point(20, 20), Size = new Size(370, 50),
                    TextAlign = ContentAlignment.TopRight
                };
                frm.Controls.Add(lbl);

                TextBox txt = new TextBox { Location = new Point(20, 76), Size = new Size(370, 30), TextAlign = HorizontalAlignment.Center };
                UiTheme.StyleTextBox(txt);
                frm.Controls.Add(txt);

                bool confirmed = false;
                Button btnOk = UiTheme.CreateButton("تأیید نهایی", "✔", UiTheme.Danger);
                btnOk.SetBounds(230, 122, 160, 34);
                btnOk.Click += delegate
                {
                    if (txt.Text.Trim() == expectedCount.ToString())
                    {
                        confirmed = true;
                        frm.DialogResult = DialogResult.OK;
                    }
                    else
                    {
                        UiTheme.ShowWarning(frm, "عددِ واردشده با تعدادِ انتخاب‌شده یکی نیست.");
                    }
                };
                frm.Controls.Add(btnOk);

                Button btnCancel = UiTheme.CreateSecondaryButton("انصراف", "✕");
                btnCancel.SetBounds(30, 122, 160, 34);
                btnCancel.Click += delegate { frm.DialogResult = DialogResult.Cancel; };
                frm.Controls.Add(btnCancel);

                frm.CancelButton = btnCancel;
                frm.ShowDialog(this);
                return confirmed;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // تب: درباره و لایسنس
        // آموزش — این تب سه فیلد قبلاً «اعلام‌شده ولی بلااستفاده» را فعال می‌کند
        // (_txtDeveloperName / _txtLicenseInfo / _txtMachineId) و وضعیت لایسنس را
        // با زیرساخت LicenseManager نمایش/فعال می‌کند. بدون هیچ enforcement.
        // ══════════════════════════════════════════════════════════════════
        private Label _lblAboutLicenseStatus;

        private void BuildAboutTab(Panel tab)
        {
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true, AutoScroll = true, Padding = new Padding(14, 12, 14, 12)
            };

            // نسخه‌ی نرم‌افزار (فقط‌خواندنی)
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            TextBox txtVersion = new TextBox { ReadOnly = true, Text = ver == null ? "" : ver.ToString() };
            flow.Controls.Add(MakeFieldPanel("نسخه نرم‌افزار", txtVersion));

            _txtDeveloperName = new TextBox();
            flow.Controls.Add(MakeFieldPanel("توسعه‌دهنده", _txtDeveloperName));

            _txtLicenseInfo = new TextBox();
            flow.Controls.Add(MakeFieldPanel("اطلاعات لایسنس (یادداشت)", _txtLicenseInfo));

            // شناسه دستگاه (فقط‌خواندنی) — برای ارسال به فروشنده جهت صدور لایسنس.
            _txtMachineId = new TextBox { ReadOnly = true, Text = LicenseManager.GetHardwareId() };
            flow.Controls.Add(MakeFieldPanel("شناسه این دستگاه", _txtMachineId));

            // ─── وضعیت لایسنس ─────────────────────────────────────────────────
            Panel statusField = new Panel { Width = 430, Height = 58, Margin = new Padding(6, 26, 6, 4) };
            _lblAboutLicenseStatus = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.FontBold(UiTheme.SizeBody)
            };
            statusField.Controls.Add(_lblAboutLicenseStatus);
            flow.Controls.Add(statusField);

            // ─── دکمه‌های لایسنس ──────────────────────────────────────────────
            Button btnActivate = UiTheme.CreateButton("فعال‌سازی لایسنس", "🔑", UiTheme.Primary);
            btnActivate.Size = new Size(190, 34); btnActivate.Margin = new Padding(6, 30, 6, 4);
            btnActivate.Click += delegate
            {
                string token = PromptForLicenseToken();
                if (string.IsNullOrWhiteSpace(token)) return;
                string message;
                bool ok = LicenseManager.Activate(token, out message);
                if (ok) UiTheme.ShowSuccess(this, message); else UiTheme.ShowError(this, message);
                RefreshAboutLicenseStatus();
            };
            flow.Controls.Add(btnActivate);

            Button btnCopyId = UiTheme.CreateSecondaryButton("کپی شناسه دستگاه", "⧉");
            btnCopyId.Size = new Size(190, 34); btnCopyId.Margin = new Padding(6, 30, 6, 4);
            btnCopyId.Click += delegate
            {
                try { Clipboard.SetText(_txtMachineId.Text); UiTheme.ShowSuccess(this, "شناسه دستگاه کپی شد."); } catch { }
            };
            flow.Controls.Add(btnCopyId);

            // ─── نوار ذخیره ───────────────────────────────────────────────────
            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = UiTheme.CardBack };
            FlowLayoutPanel saveFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(14, 8, 14, 8)
            };
            Button btnSave = UiTheme.CreateButton("ذخیره", "✔", UiTheme.Success);
            btnSave.Size = new Size(150, 38);
            btnSave.Click += delegate
            {
                SettingsHelper.Set(SettingsHelper.DeveloperName, _txtDeveloperName.Text.Trim());
                SettingsHelper.Set(SettingsHelper.LicenseInfo, _txtLicenseInfo.Text.Trim());
                SettingsHelper.Set(SettingsHelper.MachineId, _txtMachineId.Text.Trim());
                UiTheme.ShowSuccess(this, "اطلاعات ذخیره شد.");
            };
            saveFlow.Controls.Add(btnSave);
            bottomBar.Controls.Add(saveFlow);

            tab.Controls.Add(flow);
            tab.Controls.Add(bottomBar);

            _txtDeveloperName.Text = SettingsHelper.Get(SettingsHelper.DeveloperName);
            _txtLicenseInfo.Text   = SettingsHelper.Get(SettingsHelper.LicenseInfo);
            RefreshAboutLicenseStatus();
        }

        private void RefreshAboutLicenseStatus()
        {
            LicenseManager.Invalidate();
            LicenseInfo lic = LicenseManager.Current;
            _lblAboutLicenseStatus.Text = "وضعیت لایسنس: " + lic.StatusDisplay +
                (string.IsNullOrWhiteSpace(lic.LicensedTo) ? "" : "  —  " + lic.LicensedTo);
            _lblAboutLicenseStatus.ForeColor =
                lic.Status == LicenseStatus.Active ? UiTheme.Success :
                lic.Status == LicenseStatus.Trial ? UiTheme.Warning : UiTheme.Danger;
        }

        private string PromptForLicenseToken()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "فعال‌سازی لایسنس";
                dlg.RightToLeft = RightToLeft.Yes; dlg.RightToLeftLayout = true;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false; dlg.MinimizeBox = false; dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(460, 220);
                dlg.BackColor = UiTheme.CardBack; dlg.Font = UiTheme.Font(UiTheme.SizeBody);

                Label lbl = new Label
                {
                    Text = "توکن لایسنس دریافتی از فروشنده را وارد کنید:",
                    Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                    AutoSize = false, TextAlign = ContentAlignment.MiddleRight
                };
                lbl.SetBounds(16, 12, dlg.ClientSize.Width - 32, 24);
                dlg.Controls.Add(lbl);

                TextBox txt = new TextBox
                {
                    Multiline = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Consolas", 9.5F)
                };
                txt.SetBounds(16, 42, dlg.ClientSize.Width - 32, 110);
                dlg.Controls.Add(txt);

                Button ok = UiTheme.CreateButton("فعال‌سازی", "", UiTheme.Primary);
                ok.SetBounds(dlg.ClientSize.Width - 150, 164, 134, 36);
                ok.DialogResult = DialogResult.OK; dlg.Controls.Add(ok);

                Button cancel = UiTheme.CreateSecondaryButton("انصراف", "");
                cancel.SetBounds(dlg.ClientSize.Width - 290, 164, 130, 36);
                cancel.DialogResult = DialogResult.Cancel; dlg.Controls.Add(cancel);

                dlg.AcceptButton = ok; dlg.CancelButton = cancel;
                return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // تب ۱: اطلاعات مؤسسه
        // ══════════════════════════════════════════════════════════════════
        private void BuildGeneralTab(Panel tab)
        {
            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = UiTheme.CardBack };
            FlowLayoutPanel saveFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight, // + RightToLeft ارثی فرم = یک‌بار آینه (راست‌چین درست)
                Padding = new Padding(14, 8, 14, 8)
            };
            Button btnSaveGeneral = UiTheme.CreateButton("ذخیره تنظیمات", "✔", UiTheme.Success);
            btnSaveGeneral.Size = new Size(180, 38);
            btnSaveGeneral.Click += BtnSaveGeneral_Click;
            saveFlow.Controls.Add(btnSaveGeneral);
            bottomBar.Controls.Add(saveFlow);

            // آموزش — بازطراحی ظاهری (طبق عکسِ درخواستی کاربر): فیلدها دیگر در
            // یک FlowLayoutPanel شناور تنها نیستند، بلکه داخل دو «کارت» سفید
            // گردگوشه (SettingsCardPanel) گروه‌بندی شده‌اند: «تنظیمات مؤسسه»
            // (همان فیلدهای قبلی، بدون کم/زیاد) و «ظاهر و نمایش» (رنگ‌ها).
            // هیچ فیلد/کلید تنظیماتی جدید اضافه نشده — فقط چیدمانِ بصری عوض شده.
            //
            // آموزش — چرا Panel با Dock به‌جای FlowLayoutPanel+AutoSize: ترکیب
            // Dock=Top با AutoSize روی FlowLayoutPanel در عمل قابل‌اعتماد نبود
            // (با تست واقعی/اسکرین‌شات کشف شد: کارتِ دوم اصلاً رندر نمی‌شد،
            // چون FlowLayoutPanel عرضش را به‌جای عرضِ والد، بر اساس محتوا
            // محاسبه می‌کرد). دو Panel با Dock=Fill/Right رفتارِ قابل‌پیش‌بینی
            // و همان الگوی اثبات‌شده در این پروژه (userPanel/logoArea در
            // داشبورد) را دارد.
            Panel cardsHost = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background, Padding = new Padding(16) };

            // ─── کارت ۱: تنظیمات مؤسسه ──────────────────────────────────────
            SettingsCardPanel cardInstitution = new SettingsCardPanel("🏢", "تنظیمات مؤسسه")
            {
                Dock = DockStyle.Fill
            };
            // آموزش — AutoScroll عمداً خاموش است: خواسته‌ی صریح کاربر این بود که
            // این صفحه «اسکرول نداشته باشد». برای همین همه‌ی فیلدها با ابعاد
            // فشرده (MakeCompactField/MakeCompactImageField) ساخته می‌شوند تا
            // مجموعشان از ارتفاع کارت کمتر بماند.
            FlowLayoutPanel instFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true, AutoScroll = false
            };

            _txtOrgName = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("نام مؤسسه", _txtOrgName));

            _txtOrgNameEn = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("نام انگلیسی مؤسسه", _txtOrgNameEn));

            _txtSlogan = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("شعار", _txtSlogan));

            _txtOrgCode = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("کد مؤسسه", _txtOrgCode));

            _txtRegNumber = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("شماره ثبت", _txtRegNumber));

            _txtManagerName = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("نام مسئول", _txtManagerName));

            _txtAddress = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("آدرس کامل", _txtAddress));

            _txtGeneralPhone = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("تلفن", _txtGeneralPhone));

            _txtMobile = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("موبایل", _txtMobile));

            _txtWhatsApp = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("واتساپ", _txtWhatsApp));

            _txtEmail = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("ایمیل", _txtEmail));

            _txtWebsite = NewStyledTextBox();
            instFlow.Controls.Add(MakeCompactField("وب‌سایت", _txtWebsite));

            // آموزش — شماره شروع پرونده/رسید و مسیر Backup/تصاویر به تب‌های
            // اختصاصی «شماره‌گذاری» و «مسیرها و فایل‌ها» منتقل شدند (کنترل‌ها
            // اینجا حذف شدند، ولی کلید تنظیمات همان قبلی مانده — بدون افت داده).

            // ─── توضیحات (چندخطی، تمام‌عرضِ کارت) ────────────────────────────
            _txtDescription = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, TextAlign = HorizontalAlignment.Right };
            Panel descField = new Panel
            {
                Width = CompactFieldWidth * 2 + 12, Height = 74, Margin = new Padding(3, 2, 3, 6)
            };
            _txtDescription.Dock = DockStyle.Fill;
            _txtDescription.Font = UiTheme.Font(UiTheme.SizeSmall);
            UiTheme.StyleTextBox(_txtDescription);
            descField.Controls.Add(_txtDescription);
            descField.Controls.Add(new Label
            {
                Text = "توضیحات", AutoSize = false, Dock = DockStyle.Top, Height = 17,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.FontBold(UiTheme.SizeSmall - 1F), ForeColor = UiTheme.TextMuted
            });
            instFlow.Controls.Add(descField);

            // ─── لوگو / امضای مسئول / مهر رسمی — سه فیلدِ فشرده در یک ردیف ────
            _txtLogoPath = new TextBox();
            _picLogoPreview = new PictureBox();
            instFlow.Controls.Add(MakeCompactImageField(
                "لوگوی مؤسسه", _txtLogoPath, _picLogoPreview, BtnBrowseLogo_Click));

            _txtSignaturePath = new TextBox();
            _picSignaturePreview = new PictureBox();
            instFlow.Controls.Add(MakeCompactImageField(
                "امضای مسئول دفتر", _txtSignaturePath, _picSignaturePreview, BtnBrowseSignature_Click));

            _txtStampPath = new TextBox();
            _picStampPreview = new PictureBox();
            instFlow.Controls.Add(MakeCompactImageField(
                "مهر رسمی", _txtStampPath, _picStampPreview, BtnBrowseStamp_Click));

            cardInstitution.Content.Controls.Add(instFlow);

            // ─── کارت ۲: ظاهر و نمایش ────────────────────────────────────────
            // آموزش — چون FrmSettings.RightToLeftLayout=true است، Dock=Right
            // به‌صورت هندسی آینه می‌شود و بصراً سمت چپ می‌نشیند (کارتِ کوچک‌تر،
            // مثل عکس)؛ Padding سمت راستِ rightHost فاصله‌ی بینِ دو کارت را
            // می‌سازد (Margin روی یک Panelِ ساده — برخلاف FlowLayoutPanel —
            // اثری ندارد، پس با Padding والد جایگزین شده).
            Panel rightHost = new Panel { Dock = DockStyle.Right, Width = 396, Padding = new Padding(16, 0, 0, 0) };
            SettingsCardPanel cardAppearance = new SettingsCardPanel("🎨", "ظاهر و نمایش")
            {
                Dock = DockStyle.Fill
            };
            FlowLayoutPanel appFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true
            };

            appFlow.Controls.Add(new Label
            {
                Text = "رنگ اصلی نرم‌افزار", AutoSize = false, Width = 320, Height = 22,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark, Margin = new Padding(0, 8, 0, 4)
            });

            FlowLayoutPanel swatchRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0, 0, 0, 8)
            };
            _themeColorSwatches.Clear();
            foreach (Color preset in PresetThemeColors)
            {
                ColorSwatchButton swatch = new ColorSwatchButton(preset) { Margin = new Padding(3) };
                swatch.Click += delegate { SelectThemeColor(preset); };
                _themeColorSwatches.Add(swatch);
                swatchRow.Controls.Add(swatch);
            }
            appFlow.Controls.Add(swatchRow);

            // دکمه‌ی «رنگ دلخواه» + مربعِ رنگِ فعلی: برای رنگی که در ردیف
            // پیش‌فرض‌ها نیست (دست‌نخورده از قبل، فقط چیدمانش عوض شده).
            Panel customColorRow = new Panel { Height = 36, Width = 320, Margin = new Padding(0, 0, 0, 16) };
            _pnlColorSwatch = new Panel { Dock = DockStyle.Right, Width = 36, Height = 30, BorderStyle = BorderStyle.FixedSingle, BackColor = _selectedThemeColor };
            Button btnPickColor = UiTheme.CreateSecondaryButton("رنگ دلخواه...", "◐");
            btnPickColor.Dock = DockStyle.Fill;
            btnPickColor.Click += BtnPickColor_Click;
            customColorRow.Controls.Add(btnPickColor);
            customColorRow.Controls.Add(_pnlColorSwatch);
            appFlow.Controls.Add(customColorRow);

            appFlow.Controls.Add(new Label
            {
                Text = "رنگ فونت نرم‌افزار", AutoSize = false, Width = 320, Height = 22,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark, Margin = new Padding(0, 8, 0, 4)
            });
            Panel fontColorRow = new Panel { Height = 36, Width = 320, Margin = new Padding(0, 0, 0, 8) };
            _pnlFontColorSwatch = new Panel { Dock = DockStyle.Right, Width = 36, Height = 30, BorderStyle = BorderStyle.FixedSingle, BackColor = _selectedFontColor };
            Button btnPickFontColor = UiTheme.CreateSecondaryButton("انتخاب رنگ...", "◐");
            btnPickFontColor.Dock = DockStyle.Fill;
            btnPickFontColor.Click += BtnPickFontColor_Click;
            fontColorRow.Controls.Add(btnPickFontColor);
            fontColorRow.Controls.Add(_pnlFontColorSwatch);
            appFlow.Controls.Add(fontColorRow);

            appFlow.Controls.Add(new Label
            {
                Text = "چیدمان کارت‌های آماری داشبورد", AutoSize = false, Width = 320, Height = 22,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextDark, Margin = new Padding(0, 8, 0, 4)
            });
            _cmbDashboardRows = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, Width = 320, Height = 30,
                Font = UiTheme.Font(UiTheme.SizeBody), Margin = new Padding(0, 0, 0, 8)
            };
            _cmbDashboardRows.Items.AddRange(new object[] { "۲ ردیف (فشرده‌تر)", "۳ ردیف", "۴ ردیف (کارت‌های بزرگ‌تر)" });
            appFlow.Controls.Add(_cmbDashboardRows);

            cardAppearance.Content.Controls.Add(appFlow);
            rightHost.Controls.Add(cardAppearance);

            // آموزش — Fill باید قبل از Right اضافه شود (همان الگوی اثبات‌شده‌ی
            // userPanel/toolButtons در داشبورد) تا کارتِ نهادی بقیه‌ی فضا را
            // بگیرد و rightHost فضای خودش را از کنار آن جدا کند.
            cardsHost.Controls.Add(cardInstitution);
            cardsHost.Controls.Add(rightHost);

            tab.Controls.Add(cardsHost);
            tab.Controls.Add(bottomBar);

            LoadGeneralSettings();
        }

        // انتخاب یکی از رنگ‌های پیش‌فرض: علامت‌گذاری همان دکمه + به‌روزرسانی
        // مربعِ رنگِ دلخواه (که Save همچنان از آن/از _selectedThemeColor می‌خواند).
        private void SelectThemeColor(Color color)
        {
            _selectedThemeColor = color;
            if (_pnlColorSwatch != null) _pnlColorSwatch.BackColor = color;
            RefreshThemeColorSwatchSelection();
        }

        private void RefreshThemeColorSwatchSelection()
        {
            foreach (ColorSwatchButton swatch in _themeColorSwatches)
                swatch.Selected = swatch.SwatchColor.ToArgb() == _selectedThemeColor.ToArgb();
        }

        private TextBox NewStyledTextBox()
        {
            TextBox tb = new TextBox();
            UiTheme.StyleTextBox(tb);
            return tb;
        }

        // فیلد ترکیبی: برچسب + تکست‌باکس فقط‌خواندنی + دکمه «انتخاب» مسیر پوشه.
        private Panel MakeBrowseFieldPanel(string labelText, TextBox textBox, EventHandler onBrowse)
        {
            Panel field = new Panel { Width = 260, Height = 58, Margin = new Padding(6, 4, 6, 4) };
            Panel row = new Panel { Dock = DockStyle.Top, Height = 28 };
            Button btnBrowse = UiTheme.CreateSecondaryButton("انتخاب", "▤");
            btnBrowse.Dock = DockStyle.Left;
            btnBrowse.Width = 78;
            btnBrowse.Click += onBrowse;
            textBox.Dock = DockStyle.Fill;
            row.Controls.Add(textBox);
            row.Controls.Add(btnBrowse);
            field.Controls.Add(row);
            field.Controls.Add(new Label
            {
                Text = labelText, AutoSize = false, Dock = DockStyle.Top, Height = 22,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark
            });
            return field;
        }

        private void BrowseFolderInto(TextBox target)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (!string.IsNullOrWhiteSpace(target.Text) && System.IO.Directory.Exists(target.Text))
                    fbd.SelectedPath = target.Text;

                if (fbd.ShowDialog(this) == DialogResult.OK)
                    target.Text = fbd.SelectedPath;
            }
        }

        private void BtnBrowseLogo_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png;*.bmp";
                ofd.CheckFileExists = true;

                if (ofd.ShowDialog(this) != DialogResult.OK)
                    return;

                _txtLogoPath.Text = ofd.FileName;
                ShowImagePreview(_picLogoPreview, ofd.FileName);
            }
        }

        // فیلد ترکیبی مشترک برای آپلود تصویر (لوگو/امضا/مهر): تکست‌باکس
        // فقط‌خواندنی + دکمه انتخاب + پیش‌نمایش — دقیقاً همان الگوی فیلد لوگو.
        private Panel MakeImageUploadField(string labelText, TextBox textBox, PictureBox preview, EventHandler onBrowse)
        {
            Panel field = new Panel { Width = 300, Height = 130, Margin = new Padding(6, 4, 6, 4) };
            Panel row = new Panel { Dock = DockStyle.Top, Height = 100 };
            preview.Dock = DockStyle.Right;
            preview.Width = 100;
            preview.BorderStyle = BorderStyle.FixedSingle;
            preview.SizeMode = PictureBoxSizeMode.Zoom;

            Panel browseRow = new Panel { Dock = DockStyle.Fill };
            Button btnBrowse = UiTheme.CreateSecondaryButton("انتخاب تصویر", "▤");
            btnBrowse.Dock = DockStyle.Top;
            btnBrowse.Height = 30;
            btnBrowse.Click += onBrowse;
            textBox.Dock = DockStyle.Top;
            textBox.ReadOnly = true;
            UiTheme.StyleTextBox(textBox);
            browseRow.Controls.Add(btnBrowse);
            browseRow.Controls.Add(textBox);
            row.Controls.Add(browseRow);
            row.Controls.Add(preview);
            field.Controls.Add(row);
            field.Controls.Add(new Label
            {
                Text = labelText, AutoSize = false, Dock = DockStyle.Top, Height = 22,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark
            });
            return field;
        }

        private void BtnBrowseSignature_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png;*.bmp";
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog(this) != DialogResult.OK) return;

                _txtSignaturePath.Text = ofd.FileName;
                ShowImagePreview(_picSignaturePreview, ofd.FileName);
            }
        }

        private void BtnBrowseStamp_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png;*.bmp";
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog(this) != DialogResult.OK) return;

                _txtStampPath.Text = ofd.FileName;
                ShowImagePreview(_picStampPreview, ofd.FileName);
            }
        }

        private void ShowImagePreview(PictureBox target, string path)
        {
            try
            {
                if (target.Image != null)
                {
                    var old = target.Image;
                    target.Image = null;
                    old.Dispose();
                }

                if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                {
                    using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                    using (var img = System.Drawing.Image.FromStream(fs))
                        target.Image = new System.Drawing.Bitmap(img);
                }
            }
            catch { /* پیش‌نمایش غیرحیاتی است */ }
        }

        private void BtnPickColor_Click(object sender, EventArgs e)
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = _selectedThemeColor;
                if (cd.ShowDialog(this) == DialogResult.OK)
                {
                    _selectedThemeColor = cd.Color;
                    _pnlColorSwatch.BackColor = _selectedThemeColor;
                    RefreshThemeColorSwatchSelection();
                }
            }
        }

        // آموزش — به درخواست کاربر: قابلیت «تغییر رنگ فونت» که قبلاً اضافه
        // نشده بود. زیرساخت آن (UiTheme.TextDark + ApplyFullPalette) از قبل
        // آماده بود، فقط UI/ذخیره‌سازی آن جا افتاده بود. مثل رنگ نرم‌افزار،
        // فقط بعد از بستن و بازکردن دوباره برنامه روی همه فرم‌ها اعمال می‌شود.
        private void BtnPickFontColor_Click(object sender, EventArgs e)
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = _selectedFontColor;
                if (cd.ShowDialog(this) == DialogResult.OK)
                {
                    _selectedFontColor = cd.Color;
                    _pnlFontColorSwatch.BackColor = _selectedFontColor;
                }
            }
        }

        private void LoadGeneralSettings()
        {
            _txtOrgName.Text          = SettingsHelper.Get(SettingsHelper.OrgName);
            _txtOrgNameEn.Text        = SettingsHelper.Get(SettingsHelper.OrgNameEn);
            _txtSlogan.Text           = SettingsHelper.Get(SettingsHelper.Slogan);
            _txtOrgCode.Text          = SettingsHelper.Get(SettingsHelper.OrgCode);
            _txtRegNumber.Text        = SettingsHelper.Get(SettingsHelper.RegNumber);
            _txtManagerName.Text      = SettingsHelper.Get(SettingsHelper.ManagerName);
            _txtLogoPath.Text         = SettingsHelper.Get(SettingsHelper.LogoPath);
            _txtSignaturePath.Text    = SettingsHelper.Get(SettingsHelper.SignaturePath);
            _txtStampPath.Text        = SettingsHelper.Get(SettingsHelper.StampPath);
            _txtAddress.Text          = SettingsHelper.Get(SettingsHelper.Address);
            _txtGeneralPhone.Text     = SettingsHelper.Get(SettingsHelper.Phone);
            _txtMobile.Text           = SettingsHelper.Get(SettingsHelper.Mobile);
            _txtWhatsApp.Text         = SettingsHelper.Get(SettingsHelper.WhatsApp);
            _txtEmail.Text            = SettingsHelper.Get(SettingsHelper.Email);
            _txtWebsite.Text          = SettingsHelper.Get(SettingsHelper.Website);
            _txtDescription.Text      = SettingsHelper.Get(SettingsHelper.Description);

            string colorHex = SettingsHelper.Get(SettingsHelper.ThemeColor);
            if (!string.IsNullOrWhiteSpace(colorHex))
            {
                try { _selectedThemeColor = ColorTranslator.FromHtml(colorHex); }
                catch { _selectedThemeColor = UiTheme.Primary; }
            }
            _pnlColorSwatch.BackColor = _selectedThemeColor;
            RefreshThemeColorSwatchSelection();

            string fontColorHex = SettingsHelper.Get(SettingsHelper.FontColor);
            if (!string.IsNullOrWhiteSpace(fontColorHex))
            {
                try { _selectedFontColor = ColorTranslator.FromHtml(fontColorHex); }
                catch { _selectedFontColor = UiTheme.TextDark; }
            }
            _pnlFontColorSwatch.BackColor = _selectedFontColor;

            int dashRows = SettingsHelper.GetInt(SettingsHelper.DashboardSummaryRows, 2);
            _cmbDashboardRows.SelectedIndex = dashRows == 3 ? 1 : (dashRows == 4 ? 2 : 0);

            ShowImagePreview(_picLogoPreview, _txtLogoPath.Text);
            ShowImagePreview(_picSignaturePreview, _txtSignaturePath.Text);
            ShowImagePreview(_picStampPreview, _txtStampPath.Text);
        }

        private void BtnSaveGeneral_Click(object sender, EventArgs e)
        {
            SettingsHelper.Set(SettingsHelper.OrgName, _txtOrgName.Text.Trim());
            SettingsHelper.Set(SettingsHelper.OrgNameEn, _txtOrgNameEn.Text.Trim());
            SettingsHelper.Set(SettingsHelper.Slogan, _txtSlogan.Text.Trim());
            SettingsHelper.Set(SettingsHelper.OrgCode, _txtOrgCode.Text.Trim());
            SettingsHelper.Set(SettingsHelper.RegNumber, _txtRegNumber.Text.Trim());
            SettingsHelper.Set(SettingsHelper.ManagerName, _txtManagerName.Text.Trim());
            SettingsHelper.Set(SettingsHelper.LogoPath, _txtLogoPath.Text.Trim());
            SettingsHelper.Set(SettingsHelper.SignaturePath, _txtSignaturePath.Text.Trim());
            SettingsHelper.Set(SettingsHelper.StampPath, _txtStampPath.Text.Trim());
            SettingsHelper.Set(SettingsHelper.Address, _txtAddress.Text.Trim());
            SettingsHelper.Set(SettingsHelper.Phone, _txtGeneralPhone.Text.Trim());
            SettingsHelper.Set(SettingsHelper.Mobile, _txtMobile.Text.Trim());
            SettingsHelper.Set(SettingsHelper.WhatsApp, _txtWhatsApp.Text.Trim());
            SettingsHelper.Set(SettingsHelper.Email, _txtEmail.Text.Trim());
            SettingsHelper.Set(SettingsHelper.Website, _txtWebsite.Text.Trim());
            SettingsHelper.Set(SettingsHelper.Description, _txtDescription.Text.Trim());
            SettingsHelper.Set(SettingsHelper.ThemeColor, ColorTranslator.ToHtml(_selectedThemeColor));
            SettingsHelper.Set(SettingsHelper.FontColor, ColorTranslator.ToHtml(_selectedFontColor));

            int dashRowsToSave = _cmbDashboardRows.SelectedIndex == 1 ? 3 : (_cmbDashboardRows.SelectedIndex == 2 ? 4 : 2);
            SettingsHelper.Set(SettingsHelper.DashboardSummaryRows, dashRowsToSave.ToString());

            UiTheme.ShowSuccess(this, "تنظیمات مؤسسه ذخیره شد. برای اعمال کامل رنگ‌ها/فونت/چیدمان داشبورد روی همه پنجره‌ها، برنامه را دوباره باز کنید.");
        }

        // ══════════════════════════════════════════════════════════════════
        // تب ۲: مدیریت مراکز
        // ══════════════════════════════════════════════════════════════════
        private void BuildCentersTab(Panel tab)
        {
            Panel panel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 210,
                BackColor = UiTheme.CardBack
            };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(14, 8, 14, 4)
            };

            _txtCenterCode = NewStyledTextBox();
            flow.Controls.Add(MakeFieldPanel("کد مرکز", _txtCenterCode));

            _txtCenterName = NewStyledTextBox();
            flow.Controls.Add(MakeFieldPanel("نام مرکز", _txtCenterName));

            _cmbCenterProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCenterProvince.Items.AddRange(CaseManagement.Helpers.AfghanGeoData.Zones); // موقتاً؛ پایین با ولایات جایگزین می‌شود
            _cmbCenterProvince.Items.Clear();
            foreach (string p in LookupHelper.GetValues("Province"))
                _cmbCenterProvince.Items.Add(p);
            flow.Controls.Add(MakeFieldPanel("ولایت", _cmbCenterProvince));

            _txtCenterAddress = NewStyledTextBox();
            flow.Controls.Add(MakeFieldPanel("آدرس", _txtCenterAddress));

            _txtCenterPhone = NewStyledTextBox();
            flow.Controls.Add(MakeFieldPanel("تلفن", _txtCenterPhone));

            _txtCenterManager = NewStyledTextBox();
            flow.Controls.Add(MakeFieldPanel("مسئول مرکز", _txtCenterManager));

            _txtCenterEmail = NewStyledTextBox();
            flow.Controls.Add(MakeFieldPanel("ایمیل", _txtCenterEmail));

            // ─── لوگوی مرکز ─────────────────────────────────────────────────
            Panel centerLogoField = new Panel { Width = 220, Height = 58, Margin = new Padding(6, 4, 6, 4) };
            Panel centerLogoRow = new Panel { Dock = DockStyle.Top, Height = 28 };
            _picCenterLogo = new PictureBox { Dock = DockStyle.Right, Width = 28, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
            Button btnBrowseCenterLogo = UiTheme.CreateSecondaryButton("انتخاب", "▤");
            btnBrowseCenterLogo.Dock = DockStyle.Left;
            btnBrowseCenterLogo.Width = 78;
            btnBrowseCenterLogo.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png;*.bmp", CheckFileExists = true })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        _txtCenterLogoPath.Text = ofd.FileName;
                        ShowImagePreview(_picCenterLogo, ofd.FileName);
                    }
                }
            };
            _txtCenterLogoPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            UiTheme.StyleTextBox(_txtCenterLogoPath);
            centerLogoRow.Controls.Add(_txtCenterLogoPath);
            centerLogoRow.Controls.Add(btnBrowseCenterLogo);
            centerLogoRow.Controls.Add(_picCenterLogo);
            centerLogoField.Controls.Add(centerLogoRow);
            centerLogoField.Controls.Add(new Label
            {
                Text = "لوگوی مرکز", AutoSize = false, Dock = DockStyle.Top, Height = 22,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark
            });
            flow.Controls.Add(centerLogoField);

            // ─── رنگ اختصاصی مرکز ───────────────────────────────────────────
            Panel centerColorField = new Panel { Width = 210, Height = 58, Margin = new Padding(6, 4, 6, 4) };
            Panel centerColorRow = new Panel { Dock = DockStyle.Top, Height = 28 };
            _pnlCenterColorSwatch = new Panel { Dock = DockStyle.Right, Width = 40, BorderStyle = BorderStyle.FixedSingle, BackColor = _selectedCenterColor };
            Button btnPickCenterColor = UiTheme.CreateSecondaryButton("انتخاب رنگ", "◐");
            btnPickCenterColor.Dock = DockStyle.Fill;
            btnPickCenterColor.Click += delegate
            {
                using (ColorDialog cd = new ColorDialog { Color = _selectedCenterColor })
                {
                    if (cd.ShowDialog(this) == DialogResult.OK)
                    {
                        _selectedCenterColor = cd.Color;
                        _pnlCenterColorSwatch.BackColor = _selectedCenterColor;
                    }
                }
            };
            centerColorRow.Controls.Add(btnPickCenterColor);
            centerColorRow.Controls.Add(_pnlCenterColorSwatch);
            centerColorField.Controls.Add(centerColorRow);
            centerColorField.Controls.Add(new Label
            {
                Text = "رنگ اختصاصی مرکز", AutoSize = false, Dock = DockStyle.Top, Height = 22,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark
            });
            flow.Controls.Add(centerColorField);

            // ─── دکمه‌های عملیات مرکز ────────────────────────────────────────
            Panel buttonsField = new Panel { Width = 560, Height = 42, Margin = new Padding(6, 4, 6, 4) };
            FlowLayoutPanel buttonsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            Button btnAdd = UiTheme.CreateButton("افزودن مرکز جدید", "+", UiTheme.Primary);
            btnAdd.Size = new Size(150, 34); btnAdd.Margin = new Padding(2);
            btnAdd.Click += BtnCenterAdd_Click;
            buttonsRow.Controls.Add(btnAdd);

            Button btnUpdate = UiTheme.CreateSecondaryButton("ذخیره ویرایش", "✎");
            btnUpdate.Size = new Size(130, 34); btnUpdate.Margin = new Padding(2);
            btnUpdate.Click += BtnCenterUpdate_Click;
            buttonsRow.Controls.Add(btnUpdate);

            Button btnClearCenterForm = UiTheme.CreateSecondaryButton("فرم جدید", "↺");
            btnClearCenterForm.Size = new Size(110, 34); btnClearCenterForm.Margin = new Padding(2);
            btnClearCenterForm.Click += delegate { ClearCenterForm(); };
            buttonsRow.Controls.Add(btnClearCenterForm);

            Button btnToggle = UiTheme.CreateSecondaryButton("فعال/غیرفعال", "⊙");
            btnToggle.Size = new Size(120, 34); btnToggle.Margin = new Padding(2);
            btnToggle.Click += BtnCenterToggle_Click;
            buttonsRow.Controls.Add(btnToggle);

            Button btnDeleteCenter = UiTheme.CreateButton("حذف مرکز", "✕", UiTheme.Danger);
            btnDeleteCenter.Size = new Size(110, 34); btnDeleteCenter.Margin = new Padding(2);
            btnDeleteCenter.Click += BtnCenterDelete_Click;
            buttonsRow.Controls.Add(btnDeleteCenter);

            buttonsField.Controls.Add(buttonsRow);
            flow.Controls.Add(buttonsField);

            // ─── جستجو ──────────────────────────────────────────────────────
            _txtCenterSearch = NewStyledTextBox();
            _txtCenterSearch.TextChanged += delegate { LoadCenters(); };
            flow.Controls.Add(MakeFieldPanel("جستجو (کد یا نام)", _txtCenterSearch));

            panel.Controls.Add(flow);

            _gridCenters = CreateGrid();
            _gridCenters.CellClick += GridCenters_CellClick;
            tab.Controls.Add(_gridCenters);
            tab.Controls.Add(panel);

            LoadCenters();
        }

        private void GridCenters_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || !_gridCenters.Columns.Contains("CenterID")) return;
            DataGridViewRow row = _gridCenters.Rows[e.RowIndex];

            _editingCenterId = Convert.ToInt32(row.Cells["CenterID"].Value);
            _txtCenterCode.Text = row.Cells["کد"].Value?.ToString() ?? "";
            _txtCenterName.Text = row.Cells["نام مرکز"].Value?.ToString() ?? "";
            _cmbCenterProvince.Text = row.Cells["ولایت"].Value?.ToString() ?? "";
            _txtCenterAddress.Text = row.Cells["آدرس"].Value?.ToString() ?? "";
            _txtCenterPhone.Text = row.Cells["تلفن"].Value?.ToString() ?? "";
            _txtCenterManager.Text = row.Cells["مسئول"].Value?.ToString() ?? "";
            _txtCenterEmail.Text = row.Cells["ایمیل"].Value?.ToString() ?? "";
            _txtCenterLogoPath.Text = row.Cells["لوگو"].Value?.ToString() ?? "";
            ShowImagePreview(_picCenterLogo, _txtCenterLogoPath.Text);

            string colorHex = row.Cells["رنگ"].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(colorHex))
            {
                try { _selectedCenterColor = ColorTranslator.FromHtml(colorHex); }
                catch { _selectedCenterColor = UiTheme.Primary; }
            }
            else _selectedCenterColor = UiTheme.Primary;
            _pnlCenterColorSwatch.BackColor = _selectedCenterColor;
        }

        private void ClearCenterForm()
        {
            _editingCenterId = 0;
            _txtCenterCode.Text = "";
            _txtCenterName.Text = "";
            _cmbCenterProvince.SelectedIndex = -1;
            _txtCenterAddress.Text = "";
            _txtCenterPhone.Text = "";
            _txtCenterManager.Text = "";
            _txtCenterEmail.Text = "";
            _txtCenterLogoPath.Text = "";
            ShowImagePreview(_picCenterLogo, "");
            _selectedCenterColor = UiTheme.Primary;
            _pnlCenterColorSwatch.BackColor = _selectedCenterColor;
        }

        private void LoadCenters()
        {
            string search = _txtCenterSearch?.Text.Trim() ?? "";
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT CenterID, CenterCode AS [کد], CenterName AS [نام مرکز], Province AS [ولایت],
       Address AS [آدرس], Phone AS [تلفن], ManagerName AS [مسئول], Email AS [ایمیل],
       LogoPath AS [لوگو], Color AS [رنگ],
       CASE IsActive WHEN 1 THEN 'فعال' ELSE 'غیرفعال' END AS [وضعیت]
FROM TblCenter
WHERE (@Search = '' OR CenterCode LIKE @SearchLike OR CenterName LIKE @SearchLike)
ORDER BY CenterCode", con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@Search", search);
                cmd.Parameters.AddWithValue("@SearchLike", "%" + search + "%");
                DataTable t = new DataTable();
                da.Fill(t);
                _gridCenters.DataSource = t;

                // ستون‌های داخلی (لوگو/رنگ) در جدول اصلی مخفی می‌مانند؛ فقط برای
                // بارگذاری فرم هنگام کلیک استفاده می‌شوند.
                if (_gridCenters.Columns.Contains("لوگو")) _gridCenters.Columns["لوگو"].Visible = false;
                if (_gridCenters.Columns.Contains("رنگ")) _gridCenters.Columns["رنگ"].Visible = false;
            }
        }

        private void BtnCenterAdd_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.IsSuperAdmin())
            {
                UiTheme.ShowWarning(this, "مدیریت مراکز فقط برای مدیر کل (SuperAdmin) مجاز است.");
                return;
            }

            string code = _txtCenterCode.Text.Trim();
            string name = _txtCenterName.Text.Trim();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            { UiTheme.ShowWarning(this, "کد و نام مرکز را وارد کنید."); return; }

            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO TblCenter (CenterCode, CenterName, Province, Address, Phone, ManagerName, Email, LogoPath, Color)
VALUES (@Code, @Name, @Province, @Address, @Phone, @Manager, @Email, @Logo, @Color)", con))
                {
                    AddCenterParameters(cmd, code, name);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
                AuditLogger.Log("ثبت مرکز", "TblCenter", 0, "", code + "/" + name);
                ClearCenterForm();
                LoadCenters();
                UiTheme.ShowSuccess(this, "مرکز جدید اضافه شد.");
            }
            catch (SQLiteException ex)
            {
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                    UiTheme.ShowError(this, "کد مرکز تکراری است.");
                else
                    UiTheme.ShowError(this, "خطا: " + ex.Message);
            }
        }

        private void BtnCenterUpdate_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.IsSuperAdmin())
            {
                UiTheme.ShowWarning(this, "مدیریت مراکز فقط برای مدیر کل (SuperAdmin) مجاز است.");
                return;
            }

            if (_editingCenterId <= 0)
            { UiTheme.ShowWarning(this, "ابتدا یک مرکز را از جدول انتخاب کنید."); return; }

            string code = _txtCenterCode.Text.Trim();
            string name = _txtCenterName.Text.Trim();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            { UiTheme.ShowWarning(this, "کد و نام مرکز را وارد کنید."); return; }

            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
UPDATE TblCenter SET
    CenterCode = @Code, CenterName = @Name, Province = @Province, Address = @Address,
    Phone = @Phone, ManagerName = @Manager, Email = @Email, LogoPath = @Logo, Color = @Color
WHERE CenterID = @ID", con))
                {
                    AddCenterParameters(cmd, code, name);
                    cmd.Parameters.AddWithValue("@ID", _editingCenterId);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
                AuditLogger.Log("ویرایش مرکز", "TblCenter", _editingCenterId, "", code + "/" + name);
                LoadCenters();
                UiTheme.ShowSuccess(this, "تغییرات مرکز ذخیره شد.");
            }
            catch (SQLiteException ex)
            {
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                    UiTheme.ShowError(this, "کد مرکز تکراری است.");
                else
                    UiTheme.ShowError(this, "خطا: " + ex.Message);
            }
        }

        private void AddCenterParameters(SQLiteCommand cmd, string code, string name)
        {
            cmd.Parameters.AddWithValue("@Code", code);
            cmd.Parameters.AddWithValue("@Name", name);
            cmd.Parameters.AddWithValue("@Province", _cmbCenterProvince.Text.Trim());
            cmd.Parameters.AddWithValue("@Address", _txtCenterAddress.Text.Trim());
            cmd.Parameters.AddWithValue("@Phone", _txtCenterPhone.Text.Trim());
            cmd.Parameters.AddWithValue("@Manager", _txtCenterManager.Text.Trim());
            cmd.Parameters.AddWithValue("@Email", _txtCenterEmail.Text.Trim());
            cmd.Parameters.AddWithValue("@Logo", _txtCenterLogoPath.Text.Trim());
            cmd.Parameters.AddWithValue("@Color", ColorTranslator.ToHtml(_selectedCenterColor));
        }

        private void BtnCenterToggle_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.IsSuperAdmin())
            {
                UiTheme.ShowWarning(this, "مدیریت مراکز فقط برای مدیر کل (SuperAdmin) مجاز است.");
                return;
            }

            if (_gridCenters.CurrentRow == null || !_gridCenters.Columns.Contains("CenterID")) return;
            int id = Convert.ToInt32(_gridCenters.CurrentRow.Cells["CenterID"].Value);
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "UPDATE TblCenter SET IsActive = CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE CenterID=@ID", con))
            {
                cmd.Parameters.AddWithValue("@ID", id);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            LoadCenters();
        }

        // حذف مرکز فقط اگر هیچ پرونده‌ای به آن اشاره نمی‌کند (CenterID در TblCase
        // به CenterID مرکز پیش‌فرض 1 اشاره دارد اگر مرکز حذف شود؛ برای جلوگیری
        // از یتیم‌شدن داده، اینجا صراحتاً مسدود می‌شود تا مدیر خودش تصمیم بگیرد).
        private void BtnCenterDelete_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.IsSuperAdmin())
            {
                UiTheme.ShowWarning(this, "مدیریت مراکز فقط برای مدیر کل (SuperAdmin) مجاز است.");
                return;
            }

            if (_gridCenters.CurrentRow == null || !_gridCenters.Columns.Contains("CenterID")) return;
            int id = Convert.ToInt32(_gridCenters.CurrentRow.Cells["CenterID"].Value);
            string name = _gridCenters.CurrentRow.Cells["نام مرکز"].Value?.ToString() ?? "";

            int caseCount;
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT COUNT(1) FROM TblCase WHERE CenterID = @ID", con))
            {
                cmd.Parameters.AddWithValue("@ID", id);
                con.Open();
                caseCount = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (caseCount > 0)
            {
                UiTheme.ShowWarning(this,
                    "این مرکز " + caseCount + " پرونده دارد و قابل حذف نیست.\n" +
                    "به‌جای حذف می‌توانید آن را «غیرفعال» کنید.");
                return;
            }

            if (!UiTheme.ShowConfirm(this, "مرکز «" + name + "» حذف شود؟", "حذف مرکز"))
                return;

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM TblCenter WHERE CenterID = @ID", con))
            {
                cmd.Parameters.AddWithValue("@ID", id);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            AuditLogger.Log("حذف مرکز", "TblCenter", id, name, "");
            ClearCenterForm();
            LoadCenters();
            UiTheme.ShowSuccess(this, "مرکز حذف شد.");
        }

        // ══════════════════════════════════════════════════════════════════
        // تب ۱۰: اطلاعات پایه (مدیریت لیست‌های کشویی)
        // ══════════════════════════════════════════════════════════════════
        private void BuildLookupTab(Panel tab)
        {
            Panel topPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 66,
                BackColor = UiTheme.CardBack
            };
            FlowLayoutPanel topFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(14, 6, 14, 4)
            };

            _cmbCategory = new ComboBox();
            _cmbCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbCategory.Items.AddRange(new object[]
            {
                // دسته‌های اصلی موجود از قبل
                "ServiceStatus", "RequestType", "PriorityLevel", "MaritalStatus",
                "Religion", "DisabilityType", "MigrationCardType", "Gender", "Province",
                // دسته‌های جدید (بخش کنترل سنتر)
                "CoveredByOrg", "Madhab", "HeadSadat", "MemberSadat", "DisabilityDegree",
                "HeadEducationLevel", "MemberEducation", "MemberGender", "PhysicalStatus",
                "StudyYear", "GradeLevel", "AssistanceType", "DocType"
            });
            _cmbCategory.SelectedIndex = 0;
            _cmbCategory.SelectedIndexChanged += delegate { _editingLookupId = 0; _txtLookupValue.Text = ""; LoadLookup(); };
            topFlow.Controls.Add(MakeFieldPanel("دسته‌بندی", _cmbCategory));

            _txtLookupSearch = NewStyledTextBox();
            _txtLookupSearch.TextChanged += delegate { LoadLookup(); };
            topFlow.Controls.Add(MakeFieldPanel("جستجو در مقادیر", _txtLookupSearch));
            topPanel.Controls.Add(topFlow);

            Panel addPanel = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 66,
                BackColor = UiTheme.CardBack
            };
            FlowLayoutPanel addFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(14, 6, 14, 4)
            };

            _txtLookupValue = NewStyledTextBox();
            addFlow.Controls.Add(MakeFieldPanel("مقدار", _txtLookupValue));

            Panel lookupButtonsField = new Panel { Width = 480, Height = 58, Margin = new Padding(6, 26, 6, 4) };
            FlowLayoutPanel lookupButtonsRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            Button btnAdd = UiTheme.CreateButton("افزودن مقدار", "+", UiTheme.Primary);
            btnAdd.Size = new Size(140, 34);
            btnAdd.Margin = new Padding(2);
            btnAdd.Click += BtnLookupAdd_Click;
            lookupButtonsRow.Controls.Add(btnAdd);

            Button btnEditVal = UiTheme.CreateSecondaryButton("ذخیره ویرایش", "✎");
            btnEditVal.Size = new Size(130, 34);
            btnEditVal.Margin = new Padding(2);
            btnEditVal.Click += BtnLookupUpdate_Click;
            lookupButtonsRow.Controls.Add(btnEditVal);

            Button btnDel = UiTheme.CreateButton("حذف", "✕", UiTheme.Danger);
            btnDel.Size = new Size(90, 34);
            btnDel.Margin = new Padding(2);
            btnDel.Click += BtnLookupDelete_Click;
            lookupButtonsRow.Controls.Add(btnDel);

            Button btnToggle = UiTheme.CreateSecondaryButton("فعال/غیرفعال", "⊙");
            btnToggle.Size = new Size(130, 34);
            btnToggle.Margin = new Padding(2);
            btnToggle.Click += BtnLookupToggle_Click;
            lookupButtonsRow.Controls.Add(btnToggle);

            lookupButtonsField.Controls.Add(lookupButtonsRow);
            addFlow.Controls.Add(lookupButtonsField);
            addPanel.Controls.Add(addFlow);

            _gridLookup = CreateGrid();
            _gridLookup.CellClick += GridLookup_CellClick;

            tab.Controls.Add(_gridLookup);
            tab.Controls.Add(addPanel);
            tab.Controls.Add(topPanel);

            LoadLookup();
        }

        private void GridLookup_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || !_gridLookup.Columns.Contains("LookupID")) return;
            DataGridViewRow row = _gridLookup.Rows[e.RowIndex];
            _editingLookupId = Convert.ToInt32(row.Cells["LookupID"].Value);
            _txtLookupValue.Text = row.Cells["مقدار"].Value?.ToString() ?? "";
        }

        private void LoadLookup()
        {
            string cat = _cmbCategory?.Text ?? "";
            if (string.IsNullOrWhiteSpace(cat)) return;
            string search = _txtLookupSearch?.Text.Trim() ?? "";

            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT LookupID, Value AS [مقدار], SortOrder AS [ترتیب],
       CASE IsActive WHEN 1 THEN 'فعال' ELSE 'غیرفعال' END AS [وضعیت]
FROM TblLookup
WHERE Category = @Cat AND (@Search = '' OR Value LIKE @SearchLike)
ORDER BY SortOrder, Value", con))
            using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@Cat", cat);
                cmd.Parameters.AddWithValue("@Search", search);
                cmd.Parameters.AddWithValue("@SearchLike", "%" + search + "%");
                DataTable t = new DataTable();
                da.Fill(t);
                _gridLookup.DataSource = t;
            }
        }

        private void BtnLookupAdd_Click(object sender, EventArgs e)
        {
            string cat = _cmbCategory?.Text;
            string val = _txtLookupValue.Text.Trim();
            if (string.IsNullOrWhiteSpace(cat) || string.IsNullOrWhiteSpace(val))
            { UiTheme.ShowWarning(this, "دسته‌بندی و مقدار را وارد کنید."); return; }

            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "INSERT OR IGNORE INTO TblLookup (Category, Value) VALUES (@Cat, @Val)", con))
                {
                    cmd.Parameters.AddWithValue("@Cat", cat);
                    cmd.Parameters.AddWithValue("@Val", val);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
                _txtLookupValue.Text = "";
                _editingLookupId = 0;
                LookupHelper.ClearCache();
                LoadLookup();
            }
            catch (Exception ex) { UiTheme.ShowError(this, ex.Message); }
        }

        // ویرایش مقدار انتخاب‌شده (تغییر نام) — قبلاً وجود نداشت.
        private void BtnLookupUpdate_Click(object sender, EventArgs e)
        {
            if (_editingLookupId <= 0)
            { UiTheme.ShowWarning(this, "ابتدا یک مقدار را از جدول انتخاب کنید."); return; }

            string val = _txtLookupValue.Text.Trim();
            if (string.IsNullOrWhiteSpace(val))
            { UiTheme.ShowWarning(this, "مقدار را وارد کنید."); return; }

            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "UPDATE TblLookup SET Value = @Val WHERE LookupID = @ID", con))
                {
                    cmd.Parameters.AddWithValue("@Val", val);
                    cmd.Parameters.AddWithValue("@ID", _editingLookupId);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
                LookupHelper.ClearCache();
                LoadLookup();
                UiTheme.ShowSuccess(this, "مقدار ویرایش شد.");
            }
            catch (SQLiteException ex)
            {
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                    UiTheme.ShowError(this, "این مقدار در همین دسته از قبل وجود دارد.");
                else
                    UiTheme.ShowError(this, "خطا: " + ex.Message);
            }
        }

        // آموزش — حذف کامل عمداً حفظ شد (برای مقادیر اشتباهی که هرگز استفاده
        // نشده‌اند)، ولی «فعال/غیرفعال» روش پیشنهادی و امن‌تر برای مقادیری است
        // که احتمالاً قبلاً در رکوردهای موجود استفاده شده‌اند — طبق تأیید کاربر.
        private void BtnLookupDelete_Click(object sender, EventArgs e)
        {
            if (_gridLookup.CurrentRow == null || !_gridLookup.Columns.Contains("LookupID")) return;
            if (!UiTheme.ShowConfirm(this,
                "این مقدار کاملاً حذف شود؟\nاگر ممکن است قبلاً در رکوردی استفاده شده باشد، به‌جای حذف از «فعال/غیرفعال» استفاده کنید.",
                "حذف")) return;

            int id = Convert.ToInt32(_gridLookup.CurrentRow.Cells["LookupID"].Value);
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM TblLookup WHERE LookupID=@ID", con))
            {
                cmd.Parameters.AddWithValue("@ID", id);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            _editingLookupId = 0;
            _txtLookupValue.Text = "";
            LookupHelper.ClearCache();
            LoadLookup();
        }

        private void BtnLookupToggle_Click(object sender, EventArgs e)
        {
            if (_gridLookup.CurrentRow == null || !_gridLookup.Columns.Contains("LookupID")) return;
            int id = Convert.ToInt32(_gridLookup.CurrentRow.Cells["LookupID"].Value);
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "UPDATE TblLookup SET IsActive=CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE LookupID=@ID", con))
            {
                cmd.Parameters.AddWithValue("@ID", id);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            LookupHelper.ClearCache();
            LoadLookup();
        }

        // ══════════════════════════════════════════════════════════════════
        // تب ۳: شماره‌گذاری
        // ══════════════════════════════════════════════════════════════════
        private void BuildNumberingTab(Panel tab)
        {
            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = UiTheme.CardBack };
            FlowLayoutPanel saveFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(14, 8, 14, 8)
            };
            Button btnSave = UiTheme.CreateButton("ذخیره شماره‌گذاری", "✔", UiTheme.Success);
            btnSave.Size = new Size(180, 38);
            btnSave.Click += BtnSaveNumbering_Click;
            saveFlow.Controls.Add(btnSave);
            bottomBar.Controls.Add(saveFlow);

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true, AutoScroll = true, Padding = new Padding(14, 12, 14, 12)
            };

            _numStartCaseNo = new NumericUpDown { Maximum = 1000000 };
            flow.Controls.Add(MakeNumberingFieldPanel("شماره شروع پرونده", _numStartCaseNo,
                delegate { ResetCounter(SettingsHelper.StartCaseNo, _numStartCaseNo, "شماره شروع پرونده"); }));

            _numStartFamilyNo = new NumericUpDown { Maximum = 1000000 };
            flow.Controls.Add(MakeNumberingFieldPanel("شماره شروع اعضای خانواده", _numStartFamilyNo,
                delegate { ResetCounter(SettingsHelper.StartFamilyNo, _numStartFamilyNo, "شماره شروع اعضای خانواده"); }));

            _numStartDocNo = new NumericUpDown { Maximum = 1000000 };
            flow.Controls.Add(MakeNumberingFieldPanel("شماره شروع اسناد", _numStartDocNo,
                delegate { ResetCounter(SettingsHelper.StartDocNo, _numStartDocNo, "شماره شروع اسناد"); }));

            _numStartReceiptNo = new NumericUpDown { Maximum = 1000000 };
            flow.Controls.Add(MakeNumberingFieldPanel("شماره شروع رسید مالی", _numStartReceiptNo,
                delegate { ResetCounter(SettingsHelper.StartReceiptNo, _numStartReceiptNo, "شماره شروع رسید مالی"); }));

            _numStartReportNo = new NumericUpDown { Maximum = 1000000 };
            flow.Controls.Add(MakeNumberingFieldPanel("شماره شروع گزارش‌ها", _numStartReportNo,
                delegate { ResetCounter(SettingsHelper.StartReportNo, _numStartReportNo, "شماره شروع گزارش‌ها"); }));

            Label note = new Label
            {
                Text =
                    "توجه: «شماره شروع پرونده» همین حالا هم در ثبت پرونده جدید رعایت می‌شود.\n" +
                    "«شماره شروع اعضای خانواده/اسناد/گزارش‌ها» ذخیره می‌شوند، ولی چون این موارد در حال حاضر شماره ترتیبی نمایشی جداگانه‌ای در فرم‌هایشان ندارند (بر خلاف شماره فرم پرونده)، هنوز اثر قابل‌مشاهده‌ای ندارند.",
                AutoSize = false,
                Size = new Size(880, 60),
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopRight
            };
            flow.Controls.Add(note);

            tab.Controls.Add(flow);
            tab.Controls.Add(bottomBar);

            LoadNumberingSettings();
        }

        // فیلد شماره‌گذاری: برچسب + NumericUpDown + دکمه Reset کوچک کنار آن.
        private Panel MakeNumberingFieldPanel(string labelText, NumericUpDown input, EventHandler onReset)
        {
            Panel p = new Panel { Width = 280, Height = 58, Margin = new Padding(6, 4, 6, 4) };
            Label lbl = new Label
            {
                Text = labelText, AutoSize = false, Dock = DockStyle.Top, Height = 22,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark
            };
            Panel row = new Panel { Dock = DockStyle.Top, Height = 28 };
            Button btnReset = UiTheme.CreateSecondaryButton("Reset", "↺");
            btnReset.Dock = DockStyle.Left;
            btnReset.Width = 70;
            btnReset.Click += onReset;
            input.Dock = DockStyle.Fill;
            input.Font = UiTheme.Font(UiTheme.SizeBody);
            row.Controls.Add(input);
            row.Controls.Add(btnReset);
            p.Controls.Add(row);
            p.Controls.Add(lbl);
            return p;
        }

        private void ResetCounter(string settingKey, NumericUpDown control, string displayName)
        {
            if (!UiTheme.ShowConfirm(this, "«" + displayName + "» به صفر بازنشانی شود؟", "بازنشانی شماره"))
                return;
            control.Value = 0;
            SettingsHelper.Set(settingKey, "0");
            UiTheme.ShowSuccess(this, displayName + " بازنشانی شد.");
        }

        private void LoadNumberingSettings()
        {
            _numStartCaseNo.Value    = SettingsHelper.GetInt(SettingsHelper.StartCaseNo, 0);
            _numStartFamilyNo.Value  = SettingsHelper.GetInt(SettingsHelper.StartFamilyNo, 0);
            _numStartDocNo.Value     = SettingsHelper.GetInt(SettingsHelper.StartDocNo, 0);
            _numStartReceiptNo.Value = SettingsHelper.GetInt(SettingsHelper.StartReceiptNo, 0);
            _numStartReportNo.Value  = SettingsHelper.GetInt(SettingsHelper.StartReportNo, 0);
        }

        private void BtnSaveNumbering_Click(object sender, EventArgs e)
        {
            SettingsHelper.Set(SettingsHelper.StartCaseNo, ((int)_numStartCaseNo.Value).ToString());
            SettingsHelper.Set(SettingsHelper.StartFamilyNo, ((int)_numStartFamilyNo.Value).ToString());
            SettingsHelper.Set(SettingsHelper.StartDocNo, ((int)_numStartDocNo.Value).ToString());
            SettingsHelper.Set(SettingsHelper.StartReceiptNo, ((int)_numStartReceiptNo.Value).ToString());
            SettingsHelper.Set(SettingsHelper.StartReportNo, ((int)_numStartReportNo.Value).ToString());
            UiTheme.ShowSuccess(this, "شماره‌گذاری ذخیره شد.");
        }

        // ══════════════════════════════════════════════════════════════════
        // تب ۴: مسیرها و فایل‌ها
        // ══════════════════════════════════════════════════════════════════
        private void BuildPathsTab(Panel tab)
        {
            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = UiTheme.CardBack };
            FlowLayoutPanel saveFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(14, 8, 14, 8)
            };
            Button btnSave = UiTheme.CreateButton("ذخیره مسیرها", "✔", UiTheme.Success);
            btnSave.Size = new Size(160, 38);
            btnSave.Click += BtnSavePaths_Click;
            saveFlow.Controls.Add(btnSave);
            bottomBar.Controls.Add(saveFlow);

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true, AutoScroll = true, Padding = new Padding(14, 12, 14, 12)
            };

            Label note = new Label
            {
                Text = "محل اصلی ذخیره پرونده‌ها/عکس‌ها/اسناد از دکمه «⚙» در فرم پرونده تنظیم می‌شود. مسیرهای زیر جداگانه و مکمل آن هستند:",
                AutoSize = false, Size = new Size(880, 26), ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.MiddleRight
            };
            flow.Controls.Add(note);

            _txtBackupPath = new TextBox { ReadOnly = true };
            UiTheme.StyleTextBox(_txtBackupPath);
            flow.Controls.Add(MakeBrowseFieldPanel("مسیر Backup (خودکار/دستی)", _txtBackupPath,
                delegate { BrowseFolderInto(_txtBackupPath); }));

            _txtPhotoStoragePath = new TextBox { ReadOnly = true };
            UiTheme.StyleTextBox(_txtPhotoStoragePath);
            flow.Controls.Add(MakeBrowseFieldPanel("مسیر ذخیره تصاویر", _txtPhotoStoragePath,
                delegate { BrowseFolderInto(_txtPhotoStoragePath); }));

            _txtReportsPath = new TextBox { ReadOnly = true };
            UiTheme.StyleTextBox(_txtReportsPath);
            flow.Controls.Add(MakeBrowseFieldPanel("مسیر گزارش‌ها", _txtReportsPath,
                delegate { BrowseFolderInto(_txtReportsPath); }));

            _txtLogsPath = new TextBox { ReadOnly = true };
            UiTheme.StyleTextBox(_txtLogsPath);
            flow.Controls.Add(MakeBrowseFieldPanel("مسیر لاگ‌ها", _txtLogsPath,
                delegate { BrowseFolderInto(_txtLogsPath); }));

            _txtTempPath = new TextBox { ReadOnly = true };
            UiTheme.StyleTextBox(_txtTempPath);
            flow.Controls.Add(MakeBrowseFieldPanel("مسیر موقت (Temp)", _txtTempPath,
                delegate { BrowseFolderInto(_txtTempPath); }));

            // ─── جزوه آموزشی (به درخواست کاربر: قابل جایگزینی) ───────────────
            // برخلاف بقیه‌ی فیلدهای این تب که «پوشه» می‌گیرند، این یکی «فایل»
            // می‌گیرد. اگر خالی بماند، همان جزوه‌ی همراهِ نصب باز می‌شود، پس
            // رفتار پیش‌فرض هیچ تغییری نمی‌کند.
            _txtManualPath = new TextBox { ReadOnly = true };
            UiTheme.StyleTextBox(_txtManualPath);
            flow.Controls.Add(MakeBrowseFieldPanel("فایل جزوه آموزشی (PDF یا Word)", _txtManualPath,
                BtnBrowseManual_Click));

            Button btnResetManual = UiTheme.CreateSecondaryButton("بازگشت به جزوه پیش‌فرض", "↺");
            btnResetManual.Size = new Size(210, 30);
            btnResetManual.Margin = new Padding(6, 26, 6, 4);
            btnResetManual.Click += delegate { _txtManualPath.Text = ""; };
            flow.Controls.Add(btnResetManual);

            // ─── قالب‌های خروجی Word ─────────────────────────────────────────
            // قابلیت «چند قالب» از قبل کار می‌کند: هر فایل .docx داخل پوشه‌ی
            // Templates خودکار کشف می‌شود و اگر بیش از یکی باشد هنگام خروجی از
            // کاربر پرسیده می‌شود (ReportTemplateHelper/FrmTemplatePicker).
            // تنها چیزی که کم بود، راهی برای رسیدن به آن پوشه بود.
            Button btnOpenTemplates = UiTheme.CreateSecondaryButton("پوشه قالب‌های خروجی Word", "▤");
            btnOpenTemplates.Size = new Size(240, 30);
            btnOpenTemplates.Margin = new Padding(6, 26, 6, 4);
            btnOpenTemplates.Click += delegate { OpenWordTemplatesFolder(); };
            flow.Controls.Add(btnOpenTemplates);

            flow.Controls.Add(new Label
            {
                Text = "هر فایل .docx که در این پوشه بگذارید، هنگام گرفتن خروجی Word " +
                       "به‌عنوان یک الگو قابل انتخاب می‌شود. فایل‌هایی با پسوند _Sample نادیده گرفته می‌شوند.",
                AutoSize = false, Size = new Size(880, 32),
                ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(6, 2, 6, 4)
            });

            flow.Controls.Add(BuildCaseGridColumnsPanel());

            tab.Controls.Add(flow);
            tab.Controls.Add(bottomBar);

            LoadPathsSettings();
        }

        // پوشه‌ی قالب‌های Word را در File Explorer باز می‌کند (اگر نبود، می‌سازد).
        private void OpenWordTemplatesFolder()
        {
            try
            {
                string folder = System.IO.Path.Combine(Application.StartupPath, "Templates");
                System.IO.Directory.CreateDirectory(folder);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "بازکردن پوشه قالب‌ها ممکن نشد: " + ex.Message);
            }
        }

        // ─── ستون‌های گرید لیست پرونده‌ها (درخواست کاربر) ─────────────────────
        // «کد اختصاصی» همیشه نمایش داده می‌شود و در فهرست نیست؛ کاربر علاوه بر
        // آن حداکثر چهار ستون انتخاب می‌کند. سقف همان‌جا هنگام تیک‌زدن اعمال
        // می‌شود (نه هنگام ذخیره) تا بازخورد فوری باشد.
        private Panel BuildCaseGridColumnsPanel()
        {
            Panel field = new Panel { Width = 540, Height = 168, Margin = new Padding(6, 10, 6, 4) };

            field.Controls.Add(new Label
            {
                Text = "ستون‌های لیست پرونده‌ها — «" + CaseGridColumns.FixedColumnTitle +
                       "» همیشه نمایش داده می‌شود؛ حداکثر " + CaseGridColumns.MaxSelectable +
                       " ستون دیگر انتخاب کنید:",
                AutoSize = false, Dock = DockStyle.Top, Height = 34,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark
            });

            _clbCaseGridColumns = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                RightToLeft = RightToLeft.Yes,
                MultiColumn = true,
                ColumnWidth = 170,
                Font = UiTheme.Font(UiTheme.SizeBody),
                IntegralHeight = false
            };

            foreach (CaseGridColumn column in CaseGridColumns.Available)
                _clbCaseGridColumns.Items.Add(column.DisplayName);

            // اعمال سقف: اگر کاربر بخواهد پنجمی را تیک بزند، تیک لغو می‌شود و
            // پیام روشن داده می‌شود (به‌جای اینکه بی‌صدا نادیده گرفته شود).
            _clbCaseGridColumns.ItemCheck += delegate (object s, ItemCheckEventArgs e)
            {
                if (e.NewValue != CheckState.Checked)
                    return;

                if (_clbCaseGridColumns.CheckedIndices.Count >= CaseGridColumns.MaxSelectable)
                {
                    e.NewValue = CheckState.Unchecked;
                    UiTheme.ShowWarning(this,
                        "حداکثر " + CaseGridColumns.MaxSelectable +
                        " ستون می‌توانید انتخاب کنید تا جدول بدون اسکرول افقی جا شود." +
                        Environment.NewLine + "ابتدا یکی از ستون‌های انتخاب‌شده را بردارید.");
                }
            };

            field.Controls.Add(_clbCaseGridColumns);
            return field;
        }

        private void LoadCaseGridColumnsSetting()
        {
            if (_clbCaseGridColumns == null)
                return;

            var selectedKeys = new HashSet<string>(
                CaseGridColumns.GetSelected().Select(c => c.Key), StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < CaseGridColumns.Available.Count; i++)
                _clbCaseGridColumns.SetItemChecked(i, selectedKeys.Contains(CaseGridColumns.Available[i].Key));
        }

        private void SaveCaseGridColumnsSetting()
        {
            if (_clbCaseGridColumns == null)
                return;

            var chosen = new List<CaseGridColumn>();
            foreach (int index in _clbCaseGridColumns.CheckedIndices)
                chosen.Add(CaseGridColumns.Available[index]);

            // اگر کاربر همه را برداشت، به‌جای گریدِ تک‌ستونی به پیش‌فرض برمی‌گردیم.
            string csv = chosen.Count == 0
                ? string.Join(",", CaseGridColumns.DefaultKeys)
                : CaseGridColumns.ToCsv(chosen);

            SettingsHelper.Set(SettingsHelper.CaseGridColumns, csv);
        }

        private void LoadPathsSettings()
        {
            _txtBackupPath.Text       = SettingsHelper.Get(SettingsHelper.BackupPath);
            _txtPhotoStoragePath.Text = SettingsHelper.Get(SettingsHelper.PhotoStoragePath);
            _txtReportsPath.Text      = SettingsHelper.Get(SettingsHelper.ReportsPath);
            _txtLogsPath.Text         = SettingsHelper.Get(SettingsHelper.LogsPath);
            _txtTempPath.Text         = SettingsHelper.Get(SettingsHelper.TempPath);
            _txtManualPath.Text       = SettingsHelper.Get(SettingsHelper.ManualPath);
            LoadCaseGridColumnsSetting();
        }

        // انتخاب فایل جزوه آموزشی. فقط وجود فایل بررسی می‌شود؛ محتوایش هرچه
        // باشد با برنامه‌ی پیش‌فرضِ ویندوز باز خواهد شد.
        private void BtnBrowseManual_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "جزوه آموزشی|*.pdf;*.doc;*.docx|همه فایل‌ها|*.*",
                CheckFileExists = true
            })
            {
                if (!string.IsNullOrWhiteSpace(_txtManualPath.Text) && System.IO.File.Exists(_txtManualPath.Text))
                    ofd.FileName = _txtManualPath.Text;

                if (ofd.ShowDialog(this) == DialogResult.OK)
                    _txtManualPath.Text = ofd.FileName;
            }
        }

        private void BtnSavePaths_Click(object sender, EventArgs e)
        {
            SettingsHelper.Set(SettingsHelper.BackupPath, _txtBackupPath.Text.Trim());
            SettingsHelper.Set(SettingsHelper.PhotoStoragePath, _txtPhotoStoragePath.Text.Trim());
            SettingsHelper.Set(SettingsHelper.ReportsPath, _txtReportsPath.Text.Trim());
            SettingsHelper.Set(SettingsHelper.LogsPath, _txtLogsPath.Text.Trim());
            SettingsHelper.Set(SettingsHelper.TempPath, _txtTempPath.Text.Trim());
            SettingsHelper.Set(SettingsHelper.ManualPath, _txtManualPath.Text.Trim());
            SaveCaseGridColumnsSetting();
            UiTheme.ShowSuccess(this, "تنظیمات ذخیره شد.");
        }

        // ══════════════════════════════════════════════════════════════════
        // تب نگهداری سیستم
        // ══════════════════════════════════════════════════════════════════
        private void BuildMaintenanceTab(Panel tab)
        {
            Panel statsPanel = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = UiTheme.CardBack, Padding = new Padding(14) };
            _lblMaintenanceStats = new Label
            {
                Dock = DockStyle.Fill, Font = UiTheme.Font(UiTheme.SizeBody), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.TopRight
            };
            statsPanel.Controls.Add(_lblMaintenanceStats);

            FlowLayoutPanel buttonFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 56, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(14, 8, 14, 8)
            };

            Button btnRefreshStats = UiTheme.CreateSecondaryButton("تازه‌سازی آمار", "↻");
            btnRefreshStats.Size = new Size(140, 38);
            btnRefreshStats.Click += delegate { LoadMaintenanceStats(); };
            buttonFlow.Controls.Add(btnRefreshStats);

            Button btnCleanup = UiTheme.CreateSecondaryButton("پاکسازی فایل‌های بدون استفاده", "⌫");
            btnCleanup.Size = new Size(230, 38);
            btnCleanup.Click += BtnMaintenanceCleanup_Click;
            buttonFlow.Controls.Add(btnCleanup);

            Button btnReindex = UiTheme.CreateSecondaryButton("بازسازی Index", "▤");
            btnReindex.Size = new Size(140, 38);
            btnReindex.Click += delegate { RunMaintenanceSql("REINDEX;", "بازسازی Index با موفقیت انجام شد."); };
            buttonFlow.Controls.Add(btnReindex);

            Button btnVacuum = UiTheme.CreateSecondaryButton("VACUUM دیتابیس", "◐");
            btnVacuum.Size = new Size(150, 38);
            btnVacuum.Click += delegate { RunMaintenanceSql("VACUUM;", "فشرده‌سازی (VACUUM) دیتابیس انجام شد."); };
            buttonFlow.Controls.Add(btnVacuum);

            Button btnIntegrity = UiTheme.CreateSecondaryButton("بررسی سلامت دیتابیس", "✔");
            btnIntegrity.Size = new Size(180, 38);
            btnIntegrity.Click += BtnCheckIntegrity_Click;
            buttonFlow.Controls.Add(btnIntegrity);

            Button btnMissingFiles = UiTheme.CreateSecondaryButton("بررسی فایل‌های گمشده", "⌕");
            btnMissingFiles.Size = new Size(180, 38);
            btnMissingFiles.Click += BtnCheckMissingFiles_Click;
            buttonFlow.Controls.Add(btnMissingFiles);

            _txtMaintenanceOutput = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                TextAlign = HorizontalAlignment.Right, Font = UiTheme.Font(UiTheme.SizeSmall)
            };
            Panel outputWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 4, 14, 14) };
            outputWrap.Controls.Add(_txtMaintenanceOutput);

            tab.Controls.Add(outputWrap);
            tab.Controls.Add(buttonFlow);
            tab.Controls.Add(statsPanel);

            LoadMaintenanceStats();
        }

        private void LoadMaintenanceStats()
        {
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                {
                    con.Open();
                    long dbSizeBytes = 0;
                    try
                    {
                        string dbPathQuery = "PRAGMA database_list;";
                        using (var cmd = new SQLiteCommand(dbPathQuery, con))
                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                string dbPath = dr["file"].ToString();
                                if (!string.IsNullOrWhiteSpace(dbPath) && System.IO.File.Exists(dbPath))
                                    dbSizeBytes = new System.IO.FileInfo(dbPath).Length;
                            }
                        }
                    }
                    catch { }

                    int cases = Scalar(con, "SELECT COUNT(*) FROM TblCase");
                    int families = Scalar(con, "SELECT COUNT(*) FROM TblFamily");
                    int docs = Scalar(con, "SELECT COUNT(*) FROM TblDocs");
                    int users = Scalar(con, "SELECT COUNT(*) FROM TblUsers");
                    int centers = Scalar(con, "SELECT COUNT(*) FROM TblCenter");

                    string lastBackup = SettingsHelper.Get(SettingsHelper.LastBackupDate, "");
                    string lastRestore = SettingsHelper.Get(SettingsHelper.LastRestoreDate, "");

                    // قالب کامل تا ترجمه‌پذیر باشد (توضیح در GridPager/UpdateInfo).
                    _lblMaintenanceStats.Text = string.Format(
                        Lang.T("حجم دیتابیس: {0} مگابایت     |     تعداد پرونده‌ها: {1}     |     تعداد اعضای خانواده: {2}     |     تعداد اسناد: {3}\nتعداد کاربران: {4}     |     تعداد مراکز: {5}     |     آخرین Backup: {6}     |     آخرین Restore: {7}"),
                        (dbSizeBytes / 1024.0 / 1024.0).ToString("N2"),
                        cases, families, docs, users, centers,
                        string.IsNullOrEmpty(lastBackup) ? "—" : lastBackup,
                        string.IsNullOrEmpty(lastRestore) ? "—" : lastRestore);
                }
            }
            catch (Exception ex)
            {
                _lblMaintenanceStats.Text = "خطا در خواندن آمار: " + ex.Message;
            }
        }

        private int Scalar(SQLiteConnection con, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, con))
                return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private void RunMaintenanceSql(string sql, string successMessage)
        {
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
                {
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
                AppendMaintenanceOutput(successMessage);
                LoadMaintenanceStats();
            }
            catch (Exception ex)
            {
                AppendMaintenanceOutput("خطا: " + ex.Message);
            }
        }

        private void AppendMaintenanceOutput(string text)
        {
            _txtMaintenanceOutput.AppendText(
                "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
        }

        private void BtnMaintenanceCleanup_Click(object sender, EventArgs e)
        {
            try
            {
                FileCleanupHelper helper = new FileCleanupHelper();
                System.Collections.Generic.List<string> unused = helper.FindUnusedFiles();

                if (unused.Count == 0)
                {
                    AppendMaintenanceOutput("فایل بدون استفاده‌ای پیدا نشد.");
                    return;
                }

                if (!UiTheme.ShowConfirm(this, unused.Count + " فایل بدون استفاده پیدا شد. حذف شوند؟", "پاکسازی فایل‌ها"))
                    return;

                int deleted = helper.DeleteFiles(unused);
                AppendMaintenanceOutput("تعداد فایل حذف‌شده: " + deleted);
            }
            catch (Exception ex)
            {
                AppendMaintenanceOutput("خطا در پاکسازی: " + ex.Message);
            }
        }

        private void BtnCheckIntegrity_Click(object sender, EventArgs e)
        {
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand("PRAGMA integrity_check;", con))
                {
                    con.Open();
                    string result = Convert.ToString(cmd.ExecuteScalar());
                    AppendMaintenanceOutput("نتیجه بررسی سلامت دیتابیس: " + result);
                }
            }
            catch (Exception ex)
            {
                AppendMaintenanceOutput("خطا در بررسی سلامت: " + ex.Message);
            }
        }

        // بررسی فایل‌های ثبت‌شده در دیتابیس که دیگر روی دیسک وجود ندارند
        // (فقط گزارش می‌دهد؛ چیزی حذف نمی‌کند).
        private void BtnCheckMissingFiles_Click(object sender, EventArgs e)
        {
            try
            {
                int missing = 0;
                using (SQLiteConnection con = _db.GetConnection())
                {
                    con.Open();
                    missing += ReportMissingFiles(con, "SELECT CasID, PhotoPath AS P FROM TblCase WHERE NULLIF(PhotoPath,'') IS NOT NULL", "CasID");
                    missing += ReportMissingFiles(con, "SELECT CasID, FamilyPhotoPath AS P FROM TblCase WHERE NULLIF(FamilyPhotoPath,'') IS NOT NULL", "CasID");
                    missing += ReportMissingFiles(con, "SELECT FamID, MemberPhotoPath AS P FROM TblFamily WHERE NULLIF(MemberPhotoPath,'') IS NOT NULL", "FamID");
                    missing += ReportMissingFiles(con, "SELECT DocID, DocFilePath AS P FROM TblDocs WHERE NULLIF(DocFilePath,'') IS NOT NULL", "DocID");
                }
                AppendMaintenanceOutput("بررسی فایل‌های گمشده تمام شد — مجموع: " + missing + " فایل ثبت‌شده در دیتابیس روی دیسک پیدا نشد.");
            }
            catch (Exception ex)
            {
                AppendMaintenanceOutput("خطا در بررسی فایل‌های گمشده: " + ex.Message);
            }
        }

        private int ReportMissingFiles(SQLiteConnection con, string sql, string idColumn)
        {
            int count = 0;
            using (var cmd = new SQLiteCommand(sql, con))
            using (var dr = cmd.ExecuteReader())
            {
                while (dr.Read())
                {
                    string path = dr["P"].ToString();
                    if (!string.IsNullOrWhiteSpace(path) && !System.IO.File.Exists(path))
                    {
                        count++;
                        AppendMaintenanceOutput("گمشده — " + idColumn + "=" + dr[idColumn] + " → " + path);
                    }
                }
            }
            return count;
        }

        // ─── بخش «مدیریت حساب مدیر کل (سوپر ادمین)» در تب امنیت ───────────────
        private TextBox _txtSaUsername;
        private TextBox _txtSaNewPassword;
        private int _superAdminUserId;

        private void BuildSuperAdminSection(FlowLayoutPanel flow)
        {
            Panel sep = new Panel { Width = 880, Height = 2, BackColor = UiTheme.Border, Margin = new Padding(6, 10, 6, 10) };
            flow.Controls.Add(sep);

            Label header = new Label
            {
                Text = "مدیریت حساب مدیر کل (سوپر ادمین)",
                AutoSize = false, Size = new Size(880, 26), Font = UiTheme.FontBold(11F),
                ForeColor = UiTheme.PrimaryDark, TextAlign = ContentAlignment.MiddleRight
            };
            flow.Controls.Add(header);

            if (!SecurityContext.IsSuperAdmin())
            {
                Label onlySa = new Label
                {
                    Text = "این بخش فقط برای حساب «مدیر کل (SuperAdmin)» در دسترس است.",
                    AutoSize = false, Size = new Size(880, 24), ForeColor = UiTheme.TextMuted,
                    Font = UiTheme.Font(UiTheme.SizeSmall), TextAlign = ContentAlignment.MiddleRight
                };
                flow.Controls.Add(onlySa);
                return;
            }

            _txtSaUsername = new TextBox();
            flow.Controls.Add(MakeFieldPanel("نام کاربری مدیر کل", _txtSaUsername));

            _txtSaNewPassword = new TextBox { UseSystemPasswordChar = true };
            flow.Controls.Add(MakeFieldPanel("رمز جدید (خالی = بدون تغییر)", _txtSaNewPassword));

            Panel btnField = new Panel { Width = 240, Height = 58, Margin = new Padding(6, 26, 6, 4) };
            Button btnApply = UiTheme.CreateButton("به‌روزرسانی حساب مدیر کل", "✔", UiTheme.PrimaryDark);
            btnApply.Dock = DockStyle.Fill;
            btnApply.Click += BtnUpdateSuperAdmin_Click;
            btnField.Controls.Add(btnApply);
            flow.Controls.Add(btnField);

            LoadSuperAdminInfo();
        }

        private void LoadSuperAdminInfo()
        {
            _superAdminUserId = 0;
            using (var con = _db.GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT UserID, Username FROM TblUsers WHERE Role = 'SuperAdmin' ORDER BY UserID LIMIT 1", con))
            {
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        _superAdminUserId = Convert.ToInt32(dr["UserID"]);
                        if (_txtSaUsername != null)
                            _txtSaUsername.Text = dr["Username"].ToString();
                    }
                }
            }
        }

        private void BtnUpdateSuperAdmin_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.IsSuperAdmin())
            {
                UiTheme.ShowWarning(this, "فقط مدیر کل مجاز به این کار است.");
                return;
            }
            if (_superAdminUserId <= 0)
            {
                UiTheme.ShowWarning(this, "حساب مدیر کل پیدا نشد.");
                return;
            }
            string newUsername = _txtSaUsername.Text.Trim();
            if (string.IsNullOrWhiteSpace(newUsername))
            {
                UiTheme.ShowWarning(this, "نام کاربری نمی‌تواند خالی باشد.");
                return;
            }

            string newPassword = _txtSaNewPassword.Text;
            int minLen = SettingsHelper.GetInt(SettingsHelper.MinPasswordLength, 6);
            if (!string.IsNullOrEmpty(newPassword) && newPassword.Length < minLen)
            {
                UiTheme.ShowWarning(this, "رمز جدید باید حداقل " + minLen + " کاراکتر باشد.");
                return;
            }

            try
            {
                using (var con = _db.GetConnection())
                {
                    con.Open();

                    // تغییر نام کاربری
                    using (var cmd = new SQLiteCommand("UPDATE TblUsers SET Username = @U WHERE UserID = @ID", con))
                    {
                        cmd.Parameters.AddWithValue("@U", newUsername);
                        cmd.Parameters.AddWithValue("@ID", _superAdminUserId);
                        cmd.ExecuteNonQuery();
                    }

                    // تغییر رمز (فقط اگر وارد شده باشد)
                    if (!string.IsNullOrEmpty(newPassword))
                    {
                        byte[] hash, salt; int iterations;
                        PasswordHelper.CreateHash(newPassword, out hash, out salt, out iterations);
                        using (var cmd = new SQLiteCommand(@"
UPDATE TblUsers
SET PasswordHash = @H, PasswordSalt = @S, PasswordIterations = @It,
    MustChangePassword = 0, LastPasswordChangeAt = datetime('now')
WHERE UserID = @ID", con))
                        {
                            cmd.Parameters.AddWithValue("@H", hash);
                            cmd.Parameters.AddWithValue("@S", salt);
                            cmd.Parameters.AddWithValue("@It", iterations);
                            cmd.Parameters.AddWithValue("@ID", _superAdminUserId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                AuditLogger.Log("به‌روزرسانی حساب مدیر کل", "TblUsers", _superAdminUserId, "", newUsername);
                _txtSaNewPassword.Text = "";
                UiTheme.ShowSuccess(this, "حساب مدیر کل به‌روزرسانی شد.");
            }
            catch (SQLiteException ex)
            {
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                    UiTheme.ShowError(this, "این نام کاربری قبلاً استفاده شده است.");
                else
                    UiTheme.ShowError(this, "خطا: " + ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // تب امنیت
        // ══════════════════════════════════════════════════════════════════
        private void BuildSecurityTab(Panel tab)
        {
            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = UiTheme.CardBack };
            FlowLayoutPanel saveFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(14, 8, 14, 8)
            };
            Button btnSave = UiTheme.CreateButton("ذخیره تنظیمات امنیت", "✔", UiTheme.Success);
            btnSave.Size = new Size(180, 38);
            btnSave.Click += BtnSaveSecurity_Click;
            saveFlow.Controls.Add(btnSave);
            bottomBar.Controls.Add(saveFlow);

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true, AutoScroll = true, Padding = new Padding(14, 12, 14, 12)
            };

            _numMinPasswordLength = new NumericUpDown { Minimum = 4, Maximum = 32 };
            flow.Controls.Add(MakeFieldPanel("حداقل طول رمز عبور", _numMinPasswordLength));

            _numMaxFailedAttempts = new NumericUpDown { Minimum = 1, Maximum = 20 };
            flow.Controls.Add(MakeFieldPanel("حداکثر تلاش ورود ناموفق", _numMaxFailedAttempts));

            _numLockoutMinutes = new NumericUpDown { Minimum = 1, Maximum = 1440 };
            flow.Controls.Add(MakeFieldPanel("مدت قفل شدن حساب (دقیقه)", _numLockoutMinutes));

            _numSessionTimeoutMinutes = new NumericUpDown { Minimum = 0, Maximum = 1440 };
            flow.Controls.Add(MakeFieldPanel("Timeout عدم فعالیت (دقیقه — ۰=غیرفعال)", _numSessionTimeoutMinutes));

            _numForcePasswordChangeDays = new NumericUpDown { Minimum = 0, Maximum = 365 };
            flow.Controls.Add(MakeFieldPanel("اجبار تغییر رمز هر (روز — ۰=غیرفعال)", _numForcePasswordChangeDays));

            _chkAuditEnabled = new CheckBox { Text = "ثبت گزارش رویدادها (Audit) فعال باشد", AutoSize = true, Font = UiTheme.Font(UiTheme.SizeBody) };
            Panel auditField = new Panel { Width = 320, Height = 58, Margin = new Padding(6, 26, 6, 4) };
            _chkAuditEnabled.Dock = DockStyle.Fill;
            auditField.Controls.Add(_chkAuditEnabled);
            flow.Controls.Add(auditField);

            Label note = new Label
            {
                Text = "توجه: تغییر «Timeout عدم فعالیت» بعد از ذخیره، از همان بازبینی بعدی (۳۰ ثانیه‌ای) اعمال می‌شود؛ بقیه تنظیمات امنیت بلافاصله برای ورودهای بعدی اعمال می‌شوند.",
                AutoSize = false, Size = new Size(880, 40), ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(UiTheme.SizeSmall),
                TextAlign = ContentAlignment.TopRight
            };
            flow.Controls.Add(note);

            // ─── مدیریت حساب مدیر کل (سوپر ادمین) — به درخواست کاربر ──────────
            // فقط SuperAdmin می‌تواند نام کاربری/رمز حساب مدیر کل را تغییر دهد.
            BuildSuperAdminSection(flow);

            tab.Controls.Add(flow);
            tab.Controls.Add(bottomBar);

            LoadSecuritySettings();
        }

        private void LoadSecuritySettings()
        {
            _numMinPasswordLength.Value      = SettingsHelper.GetInt(SettingsHelper.MinPasswordLength, 6);
            _numMaxFailedAttempts.Value      = SettingsHelper.GetInt(SettingsHelper.MaxFailedAttempts, 5);
            _numLockoutMinutes.Value         = SettingsHelper.GetInt(SettingsHelper.LockoutMinutes, 15);
            _numSessionTimeoutMinutes.Value  = SettingsHelper.GetInt(SettingsHelper.SessionTimeoutMinutes, 0);
            _numForcePasswordChangeDays.Value = SettingsHelper.GetInt(SettingsHelper.ForcePasswordChangeDays, 0);
            _chkAuditEnabled.Checked         = SettingsHelper.GetInt(SettingsHelper.AuditEnabled, 1) == 1;
        }

        private void BtnSaveSecurity_Click(object sender, EventArgs e)
        {
            SettingsHelper.Set(SettingsHelper.MinPasswordLength, ((int)_numMinPasswordLength.Value).ToString());
            SettingsHelper.Set(SettingsHelper.MaxFailedAttempts, ((int)_numMaxFailedAttempts.Value).ToString());
            SettingsHelper.Set(SettingsHelper.LockoutMinutes, ((int)_numLockoutMinutes.Value).ToString());
            SettingsHelper.Set(SettingsHelper.SessionTimeoutMinutes, ((int)_numSessionTimeoutMinutes.Value).ToString());
            SettingsHelper.Set(SettingsHelper.ForcePasswordChangeDays, ((int)_numForcePasswordChangeDays.Value).ToString());
            SettingsHelper.Set(SettingsHelper.AuditEnabled, _chkAuditEnabled.Checked ? "1" : "0");
            UiTheme.ShowSuccess(this, "تنظیمات امنیت ذخیره شد.");
        }

        // ══════════════════════════════════════════════════════════════════
        // تب Backup و Restore
        // ══════════════════════════════════════════════════════════════════
        private void BuildBackupTab(Panel tab)
        {
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 170, BackColor = UiTheme.CardBack, Padding = new Padding(14) };
            FlowLayoutPanel topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };

            Panel scheduleField = new Panel { Width = 260, Height = 90, Margin = new Padding(6, 4, 6, 4) };
            scheduleField.Controls.Add(new Label
            {
                Text = "زمان‌بندی Backup خودکار", AutoSize = false, Dock = DockStyle.Top, Height = 22,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark
            });
            _radBackupDaily = new RadioButton { Text = "روزانه", Dock = DockStyle.Top, Height = 22, Checked = true };
            _radBackupWeekly = new RadioButton { Text = "هفتگی", Dock = DockStyle.Top, Height = 22 };
            _radBackupMonthly = new RadioButton { Text = "ماهانه", Dock = DockStyle.Top, Height = 22 };
            scheduleField.Controls.Add(_radBackupMonthly);
            scheduleField.Controls.Add(_radBackupWeekly);
            scheduleField.Controls.Add(_radBackupDaily);
            topFlow.Controls.Add(scheduleField);

            _numBackupRetention = new NumericUpDown { Minimum = 1, Maximum = 365 };
            topFlow.Controls.Add(MakeFieldPanel("تعداد نسخه‌های نگه‌داری", _numBackupRetention));

            Button btnSaveSchedule = UiTheme.CreateButton("ذخیره تنظیمات Backup", "✔", UiTheme.Success);
            btnSaveSchedule.Size = new Size(180, 34);
            btnSaveSchedule.Margin = new Padding(6, 26, 6, 4);
            btnSaveSchedule.Click += BtnSaveBackupSettings_Click;
            topFlow.Controls.Add(btnSaveSchedule);

            _lblBackupStatus = new Label
            {
                AutoSize = false, Width = 880, Height = 26, Font = UiTheme.Font(UiTheme.SizeBody), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(6, 4, 6, 4)
            };
            topFlow.Controls.Add(_lblBackupStatus);

            topPanel.Controls.Add(topFlow);

            FlowLayoutPanel actionFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 56, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(14, 8, 14, 8)
            };

            Button btnBackupNow = UiTheme.CreateButton("Backup الان", "⇑", UiTheme.Primary);
            btnBackupNow.Size = new Size(140, 38);
            btnBackupNow.Click += BtnBackupNow_Click;
            actionFlow.Controls.Add(btnBackupNow);

            Button btnRestore = UiTheme.CreateSecondaryButton("بارگذاری Backup (Restore)", "⬇");
            btnRestore.Size = new Size(200, 38);
            btnRestore.Click += BtnRestoreBackup_Click;
            actionFlow.Controls.Add(btnRestore);

            Button btnVerify = UiTheme.CreateSecondaryButton("بررسی صحت یک Backup", "✔");
            btnVerify.Size = new Size(180, 38);
            btnVerify.Click += BtnVerifyBackup_Click;
            actionFlow.Controls.Add(btnVerify);

            _txtBackupOutput = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                TextAlign = HorizontalAlignment.Right, Font = UiTheme.Font(UiTheme.SizeSmall)
            };
            Panel outputWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 4, 14, 14) };
            outputWrap.Controls.Add(_txtBackupOutput);

            tab.Controls.Add(outputWrap);
            tab.Controls.Add(actionFlow);
            tab.Controls.Add(topPanel);

            LoadBackupSettings();
        }

        private void LoadBackupSettings()
        {
            string schedule = SettingsHelper.Get(SettingsHelper.BackupSchedule, "Daily");
            _radBackupWeekly.Checked = schedule == "Weekly";
            _radBackupMonthly.Checked = schedule == "Monthly";
            _radBackupDaily.Checked = !_radBackupWeekly.Checked && !_radBackupMonthly.Checked;
            _numBackupRetention.Value = SettingsHelper.GetInt(SettingsHelper.BackupRetentionCount, 14);

            string lastBackup = SettingsHelper.Get(SettingsHelper.LastBackupDate, "");
            _lblBackupStatus.Text = string.Format(Lang.T("آخرین Backup: {0}"), string.IsNullOrEmpty(lastBackup) ? Lang.T("هنوز گرفته نشده") : lastBackup);
        }

        private void BtnSaveBackupSettings_Click(object sender, EventArgs e)
        {
            string schedule = _radBackupWeekly.Checked ? "Weekly" : _radBackupMonthly.Checked ? "Monthly" : "Daily";
            SettingsHelper.Set(SettingsHelper.BackupSchedule, schedule);
            SettingsHelper.Set(SettingsHelper.BackupRetentionCount, ((int)_numBackupRetention.Value).ToString());
            UiTheme.ShowSuccess(this, "تنظیمات Backup ذخیره شد.");
        }

        private void AppendBackupOutput(string text)
        {
            _txtBackupOutput.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
        }

        private void BtnBackupNow_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.IsSuperAdmin())
            {
                UiTheme.ShowWarning(this, "Backup کامل دیتابیس (شامل داده همه مراکز) فقط برای مدیر کل (SuperAdmin) مجاز است.");
                return;
            }

            using (FolderBrowserDialog fbd = new FolderBrowserDialog { Description = "پوشه‌ای برای ساخت Backup انتخاب کنید" })
            {
                if (fbd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    BackupHelper helper = new BackupHelper();
                    string path = helper.ExportBackup(fbd.SelectedPath);
                    SettingsHelper.Set(SettingsHelper.LastBackupDate, DateTime.Today.ToString("yyyy-MM-dd"));
                    AppendBackupOutput("Backup با موفقیت ساخته شد: " + path);
                    LoadBackupSettings();
                }
                catch (Exception ex)
                {
                    AppendBackupOutput("خطا در ساخت Backup: " + ex.Message);
                }
            }
        }

        private void BtnRestoreBackup_Click(object sender, EventArgs e)
        {
            if (!SecurityContext.IsSuperAdmin())
            {
                UiTheme.ShowWarning(this, "Restore کامل دیتابیس (شامل داده همه مراکز) فقط برای مدیر کل (SuperAdmin) مجاز است.");
                return;
            }

            using (FolderBrowserDialog fbd = new FolderBrowserDialog { Description = "پوشه Backup را انتخاب کنید" })
            {
                if (fbd.ShowDialog(this) != DialogResult.OK) return;

                if (!UiTheme.ShowConfirm(this,
                    "بارگذاری Backup ممکن است داده‌های موجود را جایگزین یا ادغام کند.\nادامه می‌دهید؟",
                    "بارگذاری Backup"))
                    return;

                try
                {
                    BackupHelper helper = new BackupHelper();
                    BackupHelper.ImportResult res = helper.ImportBackup(fbd.SelectedPath);
                    SettingsHelper.Set(SettingsHelper.LastRestoreDate, DateTime.Today.ToString("yyyy-MM-dd"));
                    AppendBackupOutput("Restore انجام شد — جدید: " + res.CasesInserted + "، تکراری/رد شده: " + res.CasesSkipped);
                }
                catch (Exception ex)
                {
                    AppendBackupOutput("خطا در Restore: " + ex.Message);
                }
            }
        }

        // بررسی صحت Backup بدون تغییر دیتابیس فعلی — فقط فایل XML بکاپ را در
        // یک DataSet موقت می‌خواند و تعداد ردیف هر جدول را گزارش می‌دهد.
        private void BtnVerifyBackup_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog { Description = "پوشه Backup را برای بررسی انتخاب کنید" })
            {
                if (fbd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string xmlPath = System.IO.Path.Combine(fbd.SelectedPath, "CaseManagementBackup.xml");
                    if (!System.IO.File.Exists(xmlPath))
                    {
                        AppendBackupOutput("نامعتبر: فایل CaseManagementBackup.xml در این پوشه پیدا نشد.");
                        return;
                    }

                    DataSet ds = new DataSet();
                    ds.ReadXml(xmlPath);

                    AppendBackupOutput("Backup معتبر است. جدول‌های موجود:");
                    foreach (DataTable t in ds.Tables)
                        AppendBackupOutput("  " + t.TableName + ": " + t.Rows.Count + " رکورد");
                }
                catch (Exception ex)
                {
                    AppendBackupOutput("نامعتبر یا خراب: " + ex.Message);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // تب اعلان‌ها
        // ══════════════════════════════════════════════════════════════════
        private void BuildNotificationsTab(Panel tab)
        {
            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = UiTheme.CardBack };
            FlowLayoutPanel saveFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(14, 8, 14, 8)
            };
            Button btnSave = UiTheme.CreateButton("ذخیره اعلان‌ها", "✔", UiTheme.Success);
            btnSave.Size = new Size(160, 38);
            btnSave.Click += BtnSaveNotifications_Click;
            saveFlow.Controls.Add(btnSave);
            bottomBar.Controls.Add(saveFlow);

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(14, 12, 14, 12)
            };

            _chkNotifyBackupMissing     = MakeNotifyCheckbox(flow, "بکاپ امروز گرفته نشده");
            _chkNotifyLowDisk           = MakeNotifyCheckbox(flow, "فضای دیسک کم است");
            _chkNotifyIncompleteCase    = MakeNotifyCheckbox(flow, "پرونده ناقص است");
            _chkNotifyNoPhoto           = MakeNotifyCheckbox(flow, "عکس ندارد");
            _chkNotifyNoDocs            = MakeNotifyCheckbox(flow, "سند ندارد");
            _chkNotifyIncompleteFamily  = MakeNotifyCheckbox(flow, "اعضای خانواده ناقص هستند");
            _chkNotifyIncompleteFinance = MakeNotifyCheckbox(flow, "اطلاعات مالی ناقص است (پرونده فعال بدون هیچ کمک ثبت‌شده)");

            tab.Controls.Add(flow);
            tab.Controls.Add(bottomBar);

            LoadNotificationSettings();
        }

        // ─── زبان سیستم ──────────────────────────────────────────────────────
        // تبِ تازه و کاملاً افزایشی: هیچ تب یا گزینه‌ای را جابه‌جا یا حذف
        // نمی‌کند. تعویضِ زبان فوری است و نیازی به بستن برنامه ندارد، چون
        // LanguageSweep همه‌ی پنجره‌های باز را دوباره ترجمه می‌کند.
        private ComboBox _cmbLanguage;

        private void BuildLanguageTab(Panel tab)
        {
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(14, 12, 14, 12)
            };

            Label lblTitle = new Label
            {
                Text = "زبان سیستم", AutoSize = false, Width = 700, Height = 34,
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight
            };
            flow.Controls.Add(lblTitle);

            Label lblHint = new Label
            {
                Text = "زبانِ نمایشِ برنامه. مقادیرِ ذخیره‌شده در دیتابیس (مثل وضعیت خدمات) " +
                       "ترجمه نمی‌شوند تا گزارش‌ها و جستجوها دقیقاً مثل قبل کار کنند.",
                AutoSize = false, Width = 700, Height = 46,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };
            flow.Controls.Add(lblHint);

            _cmbLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 320, Font = UiTheme.Font(UiTheme.SizeBody),
                Margin = new Padding(0, 10, 0, 0)
            };
            foreach (string code in Lang.AllCodes)
                _cmbLanguage.Items.Add(new LanguageItem(code));

            for (int i = 0; i < _cmbLanguage.Items.Count; i++)
            {
                if (((LanguageItem)_cmbLanguage.Items[i]).Code == Lang.Current)
                { _cmbLanguage.SelectedIndex = i; break; }
            }
            if (_cmbLanguage.SelectedIndex < 0) _cmbLanguage.SelectedIndex = 0;

            flow.Controls.Add(new Panel { Width = 700, Height = 6 });
            flow.Controls.Add(_cmbLanguage);

            Button btnApply = UiTheme.CreateButton("اعمال زبان", "✔", UiTheme.Success);
            btnApply.Size = new Size(160, 38);
            btnApply.Margin = new Padding(0, 14, 0, 0);
            btnApply.Click += delegate
            {
                LanguageItem item = _cmbLanguage.SelectedItem as LanguageItem;
                if (item == null) return;

                Lang.SetLanguage(item.Code);
                UiTheme.ShowSuccess(this, "زبان تغییر کرد.");
            };
            flow.Controls.Add(btnApply);

            Label lblFile = new Label
            {
                Text = "برای تکمیل یا اصلاح ترجمه‌ها، فایل متنیِ همان زبان را در پوشه‌ی " +
                       "Languages کنارِ برنامه ویرایش کنید (هر خط: متن فارسی=ترجمه).",
                AutoSize = false, Width = 700, Height = 44,
                Font = UiTheme.Font(UiTheme.SizeSmall - 1F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 16, 0, 0)
            };
            flow.Controls.Add(lblFile);

            tab.Controls.Add(flow);
        }

        // ══════════════════════════════════════════════════════════════════
        // تب: متن‌های کارت شناسایی سرپرست
        //
        // آموزش — چرا این تب اضافه شد: متن‌های مؤسسه‌ایِ کارت (نام مؤسسه، تماس،
        // پنج هشدار پشت کارت، عنوان امضا، پاورقی حقوقی) پیش‌تر داخل
        // CardService.BuildCardData ثابت بودند و تغییرشان به کامپایل مجدد نیاز
        // داشت. حالا هرکدام یک فیلد ویرایش‌پذیر دارند.
        //
        // آنچه عمداً اینجا نیست: مشخصاتِ خودِ سرپرست (نام، نام پدر، تذکره، کد
        // اختصاصی، تعداد ایتام) و بارکد. این‌ها داده‌ی پرونده‌اند؛ بارکد را هم
        // سیستم برای هر خانواده از شماره فرم/کد اختصاصیِ یکتا خودش می‌سازد
        // (GuardianCardRenderer.BarcodeValue) و نباید دستی دست‌کاری شود.
        //
        // هر فیلد که خالی بماند = همان مقدار پیش‌فرضِ قبلی؛ پس نصب‌های موجود
        // بدون هیچ تنظیمی دقیقاً همان کارتِ قبلی را چاپ می‌کنند.
        // ══════════════════════════════════════════════════════════════════
        private readonly List<KeyValuePair<string, TextBox>> _cardTextInputs =
            new List<KeyValuePair<string, TextBox>>();

        private void BuildGuardianCardTab(Panel tab)
        {
            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = UiTheme.CardBack };
            FlowLayoutPanel saveFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(14, 8, 14, 8)
            };
            Button btnSave = UiTheme.CreateButton("ذخیره متن‌های کارت", "✔", UiTheme.Success);
            btnSave.Size = new Size(200, 38);
            UiTheme.SetTip(btnSave, "ذخیره‌ی متن‌های کارت شناسایی؛ از چاپ بعدی اعمال می‌شود.");
            btnSave.Click += BtnSaveGuardianCardTexts_Click;
            saveFlow.Controls.Add(btnSave);

            Button btnResetCard = UiTheme.CreateSecondaryButton("بازگشت به متن‌های پیش‌فرض", "↺");
            btnResetCard.Size = new Size(230, 38);
            UiTheme.SetTip(btnResetCard, "همه‌ی فیلدهای این تب خالی می‌شوند تا متن‌های استاندارد برنامه دوباره چاپ شوند.");
            btnResetCard.Click += delegate
            {
                if (!UiTheme.ShowConfirm(this,
                        "همه‌ی متن‌های سفارشیِ کارت پاک می‌شوند و متن‌های پیش‌فرض برنامه جایشان را می‌گیرند." +
                        Environment.NewLine + Environment.NewLine + "ادامه می‌دهید؟",
                        "بازگشت به پیش‌فرض"))
                    return;

                foreach (KeyValuePair<string, TextBox> pair in _cardTextInputs)
                    pair.Value.Text = "";
                BtnSaveGuardianCardTexts_Click(null, EventArgs.Empty);
            };
            saveFlow.Controls.Add(btnResetCard);
            bottomBar.Controls.Add(saveFlow);

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(14, 12, 14, 12)
            };

            Label lblCardHint = new Label
            {
                Text = "متن‌های زیر روی کارت شناسایی سرپرست چاپ می‌شوند و همگی قابل ویرایش‌اند. " +
                       "هر فیلدی که خالی بماند، متن پیش‌فرضِ نوشته‌شده زیر همان کادر چاپ می‌شود." +
                       Environment.NewLine +
                       "مشخصات خودِ سرپرست و بارکد اینجا نیستند: بارکد را سیستم برای هر خانواده به‌صورت خودکار و یکتا می‌سازد.",
                AutoSize = false, Width = 760, Height = 62,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };
            flow.Controls.Add(lblCardHint);

            AddCardTextField(flow, "نام مؤسسه روی کارت", SettingsHelper.Card_OrgName,
                SettingsHelper.Get(SettingsHelper.OrgName), false);
            AddCardTextField(flow, "نوار تزئینی دور کارت (پیشوند)", SettingsHelper.Card_MicrotextLabel,
                "دفتر نمایندگی", false);
            AddCardTextField(flow, "آدرس دفتر", SettingsHelper.Card_Address,
                SettingsHelper.Get(SettingsHelper.Address), false);
            AddCardTextField(flow, "شماره تماس", SettingsHelper.Card_Phone,
                SettingsHelper.Get(SettingsHelper.Phone), false);
            AddCardTextField(flow, "وب‌سایت", SettingsHelper.Card_Website,
                SettingsHelper.Get(SettingsHelper.Website), false);
            AddCardTextField(flow, "ایمیل", SettingsHelper.Card_Email,
                SettingsHelper.Get(SettingsHelper.Email), false);
            AddCardTextField(flow, "صادرکننده", SettingsHelper.Card_IssuedBy,
                SecurityContext.Username, false);
            AddCardTextField(flow, "سمت صادرکننده", SettingsHelper.Card_Position,
                SecurityContext.Role, false);
            AddCardTextField(flow, "عنوان امضا", SettingsHelper.Card_SignatureLabel,
                "امضای مسئول دفتر", false);
            AddCardTextField(flow, "پاورقی حقوقی", SettingsHelper.Card_LegalLine,
                "این کارت شخصی و غیرقابل انتقال است.", false);

            AddCardTextField(flow, "هشدار ۱", SettingsHelper.Card_Notice1,
                "در هنگام توزیع کمک‌ها باید سرپرست حضور داشته باشد.", true);
            AddCardTextField(flow, "هشدار ۲", SettingsHelper.Card_Notice2,
                "در هنگام توزیع کمک‌ها این کارت و تذکره اصلی را با خود داشته باشید.", true);
            AddCardTextField(flow, "هشدار ۳", SettingsHelper.Card_Notice3,
                "در صورت مفقود و تخریب شدن کارت ۵۰۰ افغانی جریمه می‌شوید.", true);
            AddCardTextField(flow, "هشدار ۴", SettingsHelper.Card_Notice4,
                "در هنگام گرفتن کمک‌ها لطفاً پول خود را شمارش کنید.", true);
            AddCardTextField(flow, "هشدار ۵", SettingsHelper.Card_Notice5,
                "کوشش شود پول کمک ایتام برای خود آنها (خوراک و پوشاک) مصرف گردد.", true);

            tab.Controls.Add(flow);
            tab.Controls.Add(bottomBar);
        }

        // ══════════════════════════════════════════════════════════════════
        // تب: بسته‌های مساعدتِ غیرنقدی (افزایشی) — حداکثر ۵ بسته، هرکدام با
        // چند قلمِ جنس (مثلاً «آرد ۱ بوجی، روغن ۱ بشکه، شکر ۴ کیلو»). ذخیره‌شده
        // در TblAssistancePackage/TblAssistancePackageItem و از تبِ «ثبت کمک»
        // (FrmFinance) و فرمِ چاپِ گروهیِ بسته استفاده می‌شود.
        // ══════════════════════════════════════════════════════════════════
        private ListBox _lstPackages;
        private TextBox _txtPackageName;
        private DataGridView _dgvPackageItems;
        private readonly AssistanceReceiptIntegration.AssistancePackageRepository _packageRepo =
            new AssistanceReceiptIntegration.AssistancePackageRepository();
        private List<AssistanceReceiptIntegration.AssistancePackage> _packagesCache =
            new List<AssistanceReceiptIntegration.AssistancePackage>();
        private int _editingPackageId = 0;

        private void BuildAssistancePackagesTab(Panel tab)
        {
            Label lblHint = new Label
            {
                Text = "برای کمکِ «غیرنقدی» در تبِ «ثبت کمک»، به‌جای مبلغ یکی از این بسته‌ها انتخاب می‌شود. " +
                       "حداکثر " + AssistanceReceiptIntegration.AssistancePackageRepository.MaxPackages + " بسته قابل تعریف است.",
                Dock = DockStyle.Top, Height = 40, Padding = new Padding(14, 10, 14, 0),
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };

            Panel left = new Panel { Dock = DockStyle.Right, Width = 220, Padding = new Padding(10) };

            _lstPackages = new ListBox { Dock = DockStyle.Fill, Font = UiTheme.Font(UiTheme.SizeBody) };
            _lstPackages.SelectedIndexChanged += delegate { LoadPackageIntoEditor(); };

            FlowLayoutPanel leftButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft
            };
            Button btnNewPackage = UiTheme.CreateSecondaryButton("بستهٔ جدید", "+");
            btnNewPackage.Size = new Size(100, 32);
            btnNewPackage.Click += delegate { StartNewPackage(); };
            Button btnDeletePackage = UiTheme.CreateSecondaryButton("حذف", "✕");
            btnDeletePackage.Size = new Size(90, 32);
            btnDeletePackage.Click += delegate { DeleteSelectedPackage(); };
            leftButtons.Controls.Add(btnDeletePackage);
            leftButtons.Controls.Add(btnNewPackage);

            left.Controls.Add(_lstPackages);
            left.Controls.Add(leftButtons);

            Panel right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            Label lblName = new Label
            {
                Text = "نام بسته", Dock = DockStyle.Top, Height = 24,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall)
            };
            _txtPackageName = new TextBox { Dock = DockStyle.Top };
            UiTheme.StyleTextBox(_txtPackageName);

            Label lblItems = new Label
            {
                Text = "اقلامِ بسته", Dock = DockStyle.Top, Height = 28,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeSmall),
                Padding = new Padding(0, 10, 0, 0)
            };

            _dgvPackageItems = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true,
                RightToLeft = RightToLeft.Yes, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _dgvPackageItems.Columns.Add("ItemName", "نام قلم");
            _dgvPackageItems.Columns.Add("Quantity", "مقدار");
            _dgvPackageItems.Columns.Add("Unit", "واحد");

            Button btnSavePackage = UiTheme.CreateButton("ذخیرهٔ بسته", "✔", UiTheme.Success);
            btnSavePackage.Size = new Size(140, 36);
            btnSavePackage.Margin = new Padding(0, 10, 0, 0);
            btnSavePackage.Click += delegate { SaveEditingPackage(); };

            Panel saveBar = new Panel { Dock = DockStyle.Bottom, Height = 50 };
            saveBar.Controls.Add(btnSavePackage);

            right.Controls.Add(_dgvPackageItems);
            right.Controls.Add(lblItems);
            right.Controls.Add(_txtPackageName);
            right.Controls.Add(lblName);
            right.Controls.Add(saveBar);

            tab.Controls.Add(right);
            tab.Controls.Add(left);
            tab.Controls.Add(lblHint);

            ReloadPackagesList();
        }

        private void ReloadPackagesList()
        {
            _packagesCache = _packageRepo.GetAllPackages();
            _lstPackages.Items.Clear();
            foreach (AssistanceReceiptIntegration.AssistancePackage pkg in _packagesCache)
                _lstPackages.Items.Add(pkg.Name);

            _editingPackageId = 0;
            _txtPackageName.Text = "";
            _dgvPackageItems.Rows.Clear();
        }

        private void LoadPackageIntoEditor()
        {
            int idx = _lstPackages.SelectedIndex;
            if (idx < 0 || idx >= _packagesCache.Count) return;

            AssistanceReceiptIntegration.AssistancePackage pkg = _packagesCache[idx];
            _editingPackageId = pkg.PackageID;
            _txtPackageName.Text = pkg.Name;

            _dgvPackageItems.Rows.Clear();
            foreach (AssistanceReceiptIntegration.AssistancePackageItem item in pkg.Items)
                _dgvPackageItems.Rows.Add(item.ItemName, item.Quantity, item.Unit);
        }

        private void StartNewPackage()
        {
            if (_packagesCache.Count >= AssistanceReceiptIntegration.AssistancePackageRepository.MaxPackages)
            {
                Msg.Show("حداکثر " + AssistanceReceiptIntegration.AssistancePackageRepository.MaxPackages + " بسته قابل تعریف است.");
                return;
            }
            _lstPackages.ClearSelected();
            _editingPackageId = 0;
            _txtPackageName.Text = "";
            _dgvPackageItems.Rows.Clear();
            _txtPackageName.Focus();
        }

        private void SaveEditingPackage()
        {
            if (string.IsNullOrWhiteSpace(_txtPackageName.Text))
            {
                Msg.Show("نامِ بسته را وارد کنید.");
                return;
            }

            var pkg = new AssistanceReceiptIntegration.AssistancePackage
            {
                PackageID = _editingPackageId,
                Name = _txtPackageName.Text.Trim(),
                SortOrder = _editingPackageId == 0 ? _packagesCache.Count : 0
            };

            foreach (DataGridViewRow row in _dgvPackageItems.Rows)
            {
                if (row.IsNewRow) continue;
                object nameVal = row.Cells["ItemName"].Value;
                if (nameVal == null || string.IsNullOrWhiteSpace(nameVal.ToString())) continue;

                decimal qty = 0;
                object qtyVal = row.Cells["Quantity"].Value;
                if (qtyVal != null) decimal.TryParse(qtyVal.ToString(), out qty);

                pkg.Items.Add(new AssistanceReceiptIntegration.AssistancePackageItem
                {
                    ItemName = nameVal.ToString().Trim(),
                    Quantity = qty,
                    Unit = row.Cells["Unit"].Value == null ? "" : row.Cells["Unit"].Value.ToString().Trim()
                });
            }

            try
            {
                _packageRepo.SavePackage(pkg);
                UiTheme.ShowSuccess(this, "بسته ذخیره شد.");
                ReloadPackagesList();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ذخیرهٔ بسته: " + ex.Message);
            }
        }

        private void DeleteSelectedPackage()
        {
            if (_editingPackageId <= 0)
            {
                Msg.Show("اول یک بسته را از فهرست انتخاب کنید.");
                return;
            }
            if (!UiTheme.ShowConfirm(this, "این بسته حذف شود؟", "حذفِ بسته"))
                return;

            _packageRepo.DeletePackage(_editingPackageId);
            ReloadPackagesList();
        }

        // یک فیلدِ متنیِ کارت: برچسب + کادر + راهنمای «پیش‌فرض». مقدارِ
        // ذخیره‌شده داخل کادر می‌آید؛ کادرِ خالی یعنی «از پیش‌فرض استفاده کن».
        private void AddCardTextField(FlowLayoutPanel parent, string labelText, string settingKey,
                                      string defaultText, bool multiline)
        {
            Label lbl = new Label
            {
                Text = labelText, AutoSize = false, Width = 760, Height = 22,
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, 10, 0, 0)
            };
            parent.Controls.Add(lbl);

            TextBox txt = new TextBox
            {
                Width = 760, Font = UiTheme.Font(UiTheme.SizeBody),
                RightToLeft = RightToLeft.Yes, TextAlign = HorizontalAlignment.Right,
                Text = SettingsHelper.Get(settingKey)
            };
            if (multiline)
            {
                txt.Multiline = true;
                txt.Height = 46;
                txt.ScrollBars = ScrollBars.Vertical;
            }
            UiTheme.SetTip(txt,
                "خالی بگذارید تا متن پیش‌فرض چاپ شود:" + Environment.NewLine + defaultText);
            parent.Controls.Add(txt);

            Label hint = new Label
            {
                Text = "پیش‌فرض: " + defaultText,
                AutoSize = false, Width = 760, Height = 20,
                Font = UiTheme.Font(UiTheme.SizeSmall - 1F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };
            parent.Controls.Add(hint);

            _cardTextInputs.Add(new KeyValuePair<string, TextBox>(settingKey, txt));
        }

        private void BtnSaveGuardianCardTexts_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (KeyValuePair<string, TextBox> pair in _cardTextInputs)
                    SettingsHelper.Set(pair.Key, pair.Value.Text.Trim());

                UiTheme.ShowSuccess(this, "متن‌های کارت شناسایی ذخیره شد. از چاپ بعدی اعمال می‌شود.");
            }
            catch (Exception ex)
            {
                UiTheme.ShowWarning(this, "ذخیره‌ی متن‌های کارت ممکن نشد: " + ex.Message);
            }
        }

        // آیتمِ کمبوی زبان: نامِ هر زبان به خودِ همان زبان نمایش داده می‌شود.
        private class LanguageItem
        {
            public string Code { get; private set; }
            public LanguageItem(string code) { Code = code; }
            public override string ToString() { return Lang.DisplayName(Code); }
        }

        private CheckBox MakeNotifyCheckbox(FlowLayoutPanel parent, string text)
        {
            CheckBox chk = new CheckBox
            {
                Text = text, AutoSize = false, Width = 700, Height = 32,
                Font = UiTheme.Font(UiTheme.SizeBody), TextAlign = ContentAlignment.MiddleRight
            };
            parent.Controls.Add(chk);
            return chk;
        }

        private void LoadNotificationSettings()
        {
            _chkNotifyBackupMissing.Checked     = SettingsHelper.GetInt(SettingsHelper.Notify_BackupMissing, 1) == 1;
            _chkNotifyLowDisk.Checked           = SettingsHelper.GetInt(SettingsHelper.Notify_LowDisk, 1) == 1;
            _chkNotifyIncompleteCase.Checked    = SettingsHelper.GetInt(SettingsHelper.Notify_IncompleteCase, 1) == 1;
            _chkNotifyNoPhoto.Checked           = SettingsHelper.GetInt(SettingsHelper.Notify_NoPhoto, 1) == 1;
            _chkNotifyNoDocs.Checked            = SettingsHelper.GetInt(SettingsHelper.Notify_NoDocs, 1) == 1;
            _chkNotifyIncompleteFamily.Checked  = SettingsHelper.GetInt(SettingsHelper.Notify_IncompleteFamily, 1) == 1;
            _chkNotifyIncompleteFinance.Checked = SettingsHelper.GetInt(SettingsHelper.Notify_IncompleteFinance, 1) == 1;
        }

        private void BtnSaveNotifications_Click(object sender, EventArgs e)
        {
            SettingsHelper.Set(SettingsHelper.Notify_BackupMissing, _chkNotifyBackupMissing.Checked ? "1" : "0");
            SettingsHelper.Set(SettingsHelper.Notify_LowDisk, _chkNotifyLowDisk.Checked ? "1" : "0");
            SettingsHelper.Set(SettingsHelper.Notify_IncompleteCase, _chkNotifyIncompleteCase.Checked ? "1" : "0");
            SettingsHelper.Set(SettingsHelper.Notify_NoPhoto, _chkNotifyNoPhoto.Checked ? "1" : "0");
            SettingsHelper.Set(SettingsHelper.Notify_NoDocs, _chkNotifyNoDocs.Checked ? "1" : "0");
            SettingsHelper.Set(SettingsHelper.Notify_IncompleteFamily, _chkNotifyIncompleteFamily.Checked ? "1" : "0");
            SettingsHelper.Set(SettingsHelper.Notify_IncompleteFinance, _chkNotifyIncompleteFinance.Checked ? "1" : "0");
            UiTheme.ShowSuccess(this, "تنظیمات اعلان‌ها ذخیره شد.");
        }

        // ─── کمکی‌ها ────────────────────────────────────────────────────────
        private DataGridView CreateGrid()
        {
            DataGridView g = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect
            };
            UiTheme.StyleGrid(g);
            return g;
        }

        // یک فیلد: برچسب بالا + کنترل پایین، با اندازه یکسان برای همه فیلدهای
        // فرم (الگوی یکسان در کل فرم تنظیمات رعایت می‌شود).
        // ─── فیلدِ فشرده (بازطراحی ظاهری تب «اطلاعات مؤسسه») ──────────────────
        // آموزش — چرا یک سازنده‌ی جدا به‌جای تغییر MakeFieldPanel: آن متد را ۱۰
        // تبِ دیگر هم استفاده می‌کنند؛ کوچک‌کردن سراسری‌اش چیدمانِ آن‌ها را
        // به‌هم می‌ریخت. این نسخه فقط برای کارت‌های تب اول است: برچسب و ورودیِ
        // کوتاه‌تر و فونت کوچک‌تر، تا ۱۲ فیلد + توضیحات + سه تصویر بدون هیچ
        // اسکرولی داخل کارت جا شوند (خواسته‌ی صریح کاربر).
        private const int CompactFieldWidth = 330;
        private const int CompactFieldHeight = 46;

        private Panel MakeCompactField(string labelText, Control input)
        {
            Panel p = new Panel
            {
                Width = CompactFieldWidth, Height = CompactFieldHeight,
                Margin = new Padding(3, 2, 3, 2)
            };

            Label lbl = new Label
            {
                Text = labelText, AutoSize = false, Dock = DockStyle.Top, Height = 17,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.FontBold(UiTheme.SizeSmall - 1F), ForeColor = UiTheme.TextMuted
            };

            input.Dock = DockStyle.Top;
            input.Height = 25;
            input.Font = UiTheme.Font(UiTheme.SizeSmall);

            p.Controls.Add(input);
            p.Controls.Add(lbl);
            return p;
        }

        // نسخه‌ی فشرده‌ی فیلد آپلود تصویر (لوگو/امضا/مهر) برای همان کارت.
        private Panel MakeCompactImageField(string labelText, TextBox textBox, PictureBox preview, EventHandler onBrowse)
        {
            Panel p = new Panel { Width = 218, Height = 104, Margin = new Padding(3, 2, 3, 2) };

            Label lbl = new Label
            {
                Text = labelText, AutoSize = false, Dock = DockStyle.Top, Height = 17,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.FontBold(UiTheme.SizeSmall - 1F), ForeColor = UiTheme.TextMuted
            };

            Panel row = new Panel { Dock = DockStyle.Fill };

            preview.Dock = DockStyle.Right;
            preview.Width = 78;
            preview.BorderStyle = BorderStyle.FixedSingle;
            preview.SizeMode = PictureBoxSizeMode.Zoom;

            Panel browseCol = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 6, 0) };
            Button btnBrowse = UiTheme.CreateSecondaryButton("انتخاب...", "▤");
            btnBrowse.Dock = DockStyle.Top;
            btnBrowse.Height = 27;
            btnBrowse.Font = UiTheme.FontBold(UiTheme.SizeSmall - 1F);
            btnBrowse.Click += onBrowse;

            textBox.Dock = DockStyle.Top;
            textBox.ReadOnly = true;
            textBox.Height = 24;
            textBox.Font = UiTheme.Font(UiTheme.SizeSmall - 1F);
            UiTheme.StyleTextBox(textBox);

            browseCol.Controls.Add(btnBrowse);
            browseCol.Controls.Add(textBox);
            row.Controls.Add(browseCol);
            row.Controls.Add(preview);

            p.Controls.Add(row);
            p.Controls.Add(lbl);
            return p;
        }

        private Panel MakeFieldPanel(string labelText, Control input)
        {
            Panel p = new Panel();
            p.Width = 210;
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
            input.Width = 204;
            input.Font = UiTheme.Font(UiTheme.SizeBody);
            if (input is TextBox tb)
                tb.Height = 28;
            else if (input is NumericUpDown nud)
                nud.Height = 28;
            else if (input is ComboBox cb)
                cb.Height = 28;

            p.Controls.Add(input);
            p.Controls.Add(lbl);
            return p;
        }
    }
}

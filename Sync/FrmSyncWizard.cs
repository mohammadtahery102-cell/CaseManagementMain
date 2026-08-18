using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ─────────────────────────────────────────────────────────────────────────
    // FrmSyncWizard — رابط کاربری ۸ مرحله‌ای موتور همگام‌سازی HTML.
    // مرحله‌ها: ۱ انتخاب فایل · ۲ تحلیل · ۳ اعتبارسنجی · ۴ مقایسه · ۵ پیش‌نمایش
    //          · ۶ تأیید · ۷ همگام‌سازی · ۸ گزارش.
    // هیچ داده‌ای پیش از مرحله ۷ نوشته نمی‌شود؛ قبل از نوشتن بکاپ کامل گرفته
    // می‌شود و کل عملیات اتمیک (Transaction/Rollback) است.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class FrmSyncWizard : Form
    {
        private readonly string[] _stepTitles =
        {
            "۱ انتخاب فایل", "۲ تحلیل", "۳ اعتبارسنجی", "۴ مقایسه",
            "۵ پیش‌نمایش", "۶ تأیید", "۷ همگام‌سازی", "۸ گزارش"
        };

        private int _step;
        private readonly Panel[] _pages = new Panel[8];

        private Label _lblStepHeader;
        private Button _btnBack, _btnNext, _btnCancel, _btnStepHelp;
        private ProgressBar _progress;
        private Label _lblProgress;

        // داده‌های جریان کار
        private readonly SyncSource _source = new SyncSource();
        private ParsedSyncData _parsed;
        private SyncPlan _plan;
        private SyncReport _report;
        private bool _takeBackup = true;

        // ── افزودنیِ رسانه (عکس و سند) ──
        private MediaPlan _mediaPlan;
        private MediaReport _mediaReport;
        private System.Threading.CancellationTokenSource _mediaCancel;

        // عملیاتِ در جریان که دکمه‌ی «لغو» باید آن را متوقف کند (پویشِ بسته یا
        // ورودِ رسانه). اگر null باشد، دکمه رفتارِ عادیِ «بستن» را دارد.
        private System.Threading.CancellationTokenSource _activeCancel;

        // ── دکمه‌های پس از پایان و مسیرِ بکاپ ──
        private Button _btnOpenBackup, _btnSaveReport, _btnBackupPath;
        private Label _lblBackupPath;
        private string _backupParentFolder;

        // ── بررسیِ پیش از پرواز ──
        private Button _btnValidate;
        private Label _lblValidationState;
        private PackageValidationReport _validation;
        private bool _warningsAccepted;

        // کنترل‌های صفحه‌ها
        private TextBox _txtGuardians, _txtMembers, _txtPackage;
        private Label _lblPackageFound;
        private CheckBox _chkBackup, _chkImportMedia;
        private Label _lblParseSummary, _lblCompareSummary, _lblConfirmSummary;
        private ListBox _lstValidation;
        private DataGridView _grdPreview;
        private Label _lblPreviewInfo;
        private TextBox _txtReport;

        private List<SyncRecord> _previewRecords = new List<SyncRecord>();

        // ── افزودنی: نمای خلاصه‌ی پرونده‌ها + صفحه‌بندیِ پیش‌نمایش ──
        private DataGridView _grdCaseSummary;
        private ComboBox _cmbPreviewMode;
        private Helpers.GridPager _previewPager;

        // همه‌ی رکوردهای قابل‌اعمال (نه فقط صفحه‌ی جاری) و خلاصه‌ی هر پرونده.
        private readonly List<SyncRecord> _actionableRecords = new List<SyncRecord>();
        private readonly List<MediaCaseSummary> _caseSummaries = new List<MediaCaseSummary>();
        private Dictionary<string, int> _previewChangeCounts;

        public FrmSyncWizard()
        {
            BuildUi();
            ShowStep(0);

            // F1 راهنمای همان مرحله را باز می‌کند (همان کاری که دکمه می‌کند).
            // Esc به دکمه‌ی «انصراف/لغو» وصل است، پس در میانه‌ی ورودِ رسانه
            // عملیات را لغو می‌کند و در بقیه‌ی مرحله‌ها پنجره را می‌بندد —
            // دقیقاً همان منطقی که خودِ دکمه دارد.
            FormShortcuts.For(this)
                .Help(_btnStepHelp)
                .Close(_btnCancel);
        }

        private void BuildUi()
        {
            Text = "همگام‌سازی اطلاعات از سامانه مرکزی (HTML)";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            // ارتفاع بیشتر شد تا راهنمای آماده‌سازی پوشه کامل دیده شود، ولی
            // به ارتفاعِ صفحه‌نمایش محدود می‌شود: پنجره‌ی ثابتِ ۹۰۰ پیکسلی روی
            // نمایشگرِ ۱۳۶۶×۷۶۸ بیرون از صفحه می‌افتاد و دکمه‌های پایین
            // دسترس‌ناپذیر می‌شدند.
            int wanted = 900;
            try
            {
                int usable = Screen.PrimaryScreen.WorkingArea.Height - 40;
                if (usable > 640 && usable < wanted) wanted = usable;
            }
            catch { }
            UiTheme.MakeFixedSize(this, 940, wanted);

            // ── هدر مرحله ──
            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = UiTheme.PrimaryDark };
            _lblStepHeader = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(20, 0, 20, 0)
            };
            header.Controls.Add(_lblStepHeader);
            Controls.Add(header);

            // ── نوار پایین: پیشرفت + دکمه‌ها ──
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 96, BackColor = UiTheme.CardBack };

            _progress = new ProgressBar { Dock = DockStyle.Top, Height = 18, Visible = false };
            _lblProgress = new Label
            {
                Dock = DockStyle.Top, Height = 20, ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(20, 0, 20, 0), Visible = false
            };

            FlowLayoutPanel navFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(20, 10, 20, 10)
            };
            _btnCancel = UiTheme.CreateSecondaryButton("انصراف", "✕");
            _btnCancel.Size = new Size(120, 36); _btnCancel.Margin = new Padding(6, 0, 6, 0);
            // آموزش — باگی که در بازبینی پیدا شد: این دکمه همیشه فقط پنجره را
            // می‌بست. در مرحله‌ی ورودِ عکس/سند، متنش به «لغو» عوض می‌شد و فعال
            // می‌گردید، ولی کلیک‌کردنش عملیاتِ در جریان را لغو نمی‌کرد — پنجره
            // بسته می‌شد و کارِ پس‌زمینه ادامه می‌یافت. یعنی امکانِ لغوی که در
            // موتور ساخته شده بود، از رابط کاربری در دسترس نبود.
            // حالا: اگر عملیاتی در جریان است، همان لغو می‌شود؛ وگرنه پنجره
            // بسته می‌شود (رفتارِ قبلی برای بقیه‌ی مرحله‌ها دست‌نخورده).
            _btnCancel.Click += delegate
            {
                if (_activeCancel != null && !_activeCancel.IsCancellationRequested)
                {
                    _activeCancel.Cancel();
                    SetBusy(true, "در حال لغو کردن...");
                    _btnCancel.Enabled = false;
                    return;
                }
                Close();
            };

            _btnBack = UiTheme.CreateSecondaryButton("قبلی", "▶");
            _btnBack.Size = new Size(120, 36); _btnBack.Margin = new Padding(6, 0, 6, 0);
            _btnBack.Click += delegate { GoBack(); };

            _btnNext = UiTheme.CreateButton("بعدی", "◀", UiTheme.Primary);
            _btnNext.Size = new Size(150, 36); _btnNext.Margin = new Padding(6, 0, 6, 0);
            _btnNext.Click += async delegate { await GoNext(); };

            // ── راهنمای مرحله ──
            // آموزش — چرا اینجا و نه در هدر: این نوار یک FlowLayoutPanel است و
            // آینه‌سازیِ راست‌به‌چپ را خودش درست انجام می‌دهد. جای‌گذاریِ مطلق
            // در هدر به عرضِ پنجره وابسته می‌شد و روی نمایشگرِ کوچک‌تر — که
            // ارتفاع/عرض را محدود می‌کند — بیرون می‌افتاد.
            //
            // چرا در *همهٔ* مرحله‌ها و نه فقط مرحلهٔ ۱: راهنمای قبلی فقط در
            // مرحلهٔ ۱ در دسترس بود، یعنی دقیقاً وقتی کاربر هنوز نمی‌دانست چه
            // سؤالی دارد. کسی که در مرحلهٔ ۵ گیر می‌کند باید بتواند همان‌جا
            // راهنما را باز کند — و مستقیم روی توضیحِ همان مرحله باز شود.
            _btnStepHelp = UiTheme.CreateSecondaryButton("راهنمای این مرحله", "؟");
            _btnStepHelp.Size = new Size(190, 36);
            _btnStepHelp.Margin = new Padding(6, 0, 6, 0);
            _btnStepHelp.Click += delegate
            {
                FrmSyncHelp.ShowHelp(this, "step" + (_step + 1));
            };

            // ترتیب بصری راست‌به‌چپ: بعدی (راست) ، قبلی ، انصراف ، راهنما
            navFlow.Controls.Add(_btnNext);
            navFlow.Controls.Add(_btnBack);
            navFlow.Controls.Add(_btnCancel);
            navFlow.Controls.Add(_btnStepHelp);

            footer.Controls.Add(navFlow);
            footer.Controls.Add(_lblProgress);
            footer.Controls.Add(_progress);
            Controls.Add(footer);

            // ── ناحیه محتوا ──
            Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            Controls.Add(content);
            content.BringToFront();

            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i] = new Panel { Dock = DockStyle.Fill, Visible = false };
                content.Controls.Add(_pages[i]);
            }

            BuildPage0(); BuildPage1(); BuildPage2(); BuildPage3();
            BuildPage4(); BuildPage5(); BuildPage6(); BuildPage7();
        }

        // ═══ ردیف‌های عمودیِ مرحله ۱ ═══════════════════════════════════════
        // آموزش — این ثابت‌ها جایگزینِ عددهای پراکنده‌ی قبلی شدند، چون آن
        // عددها روی هم افتاده بودند و کنترل‌ها یکدیگر را می‌پوشاندند: برچسبِ
        // «مسیر بکاپ» (۱۶۴) داخلِ انتخابگرهای فایل (۱۳۲ تا ۱۸۶) بود، دکمه‌ی
        // «مسیر بکاپ...» (۱۸۶) زیرِ تیکِ بکاپ (۱۹۰) و دکمه‌ی «بررسی بسته‌ی
        // ورودی» (۲۰۰) روی هر دو. در تصویرِ آزمون به‌صورت یک نوار آبیِ بریده و
        // یک کادرِ نصفه دیده می‌شد.
        //
        // با آزاد شدنِ ۳۰۰ پیکسلی که کادرِ راهنما اشغال می‌کرد، هر ردیف جای
        // خودش را دارد و فاصله‌ها یکنواخت است. تعریفِ متمرکز یعنی جابه‌جا
        // کردنِ یک ردیف در آینده، بقیه را نمی‌شکند.
        private const int RowPackagePicker  = 64;    // برچسب ۶۴ ، کادر ۹۰ تا ۱۱۸
        private const int RowPackageFound   = 122;   // ۱۲۲ تا ۱۴۴
        private const int RowFilePickers    = 152;   // برچسب ۱۵۲ ، کادر ۱۷۸ تا ۲۰۶
        private const int RowBackupCheck    = 216;   // تیک + دکمه‌ی مسیر بکاپ
        private const int RowBackupPath     = 246;
        private const int RowMediaCheck     = 272;
        private const int RowValidateButton = 304;
        private const int RowValidateState  = 346;
        private const int RowHelpButton     = 380;
        private const int RowHelpHint       = 426;

        // ─── مرحله ۱: انتخاب فایل ────────────────────────────────────────────
        private void BuildPage0()
        {
            Panel p = _pages[0];
            p.AutoScroll = true;

            AddHint(p, "یک پوشه انتخاب کنید تا فایل‌ها، عکس‌ها و اسناد خودکار پیدا شوند (یا مثل قبل دو فایل را جداگانه بدهید).", 0);

            // ── انتخابِ پوشه (روشِ تازه و توصیه‌شده) ──
            _txtPackage = AddFolderPicker(p, "پوشه‌ی بسته‌ی ورودی (توصیه‌شده):", RowPackagePicker, OnPackageFolderChosen);

            _lblPackageFound = new Label
            {
                Location = new Point(20, RowPackageFound), Size = new Size(860, 22),
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft, Text = ""
            };
            p.Controls.Add(_lblPackageFound);

            // ── انتخابِ دستیِ فایل‌ها ──
            // آموزش — این دو انتخابگر از دو ردیفِ کاملِ عمودی به یک ردیفِ افقی
            // (نصف‌عرض، کنارِ هم) تغییر کردند. دلیل: صفحه بلندتر از بودجه‌ی
            // ارتفاعش بود و در آزمونِ تصویری، خطِ آخرِ راهنما بریده می‌شد. با
            // این چیدمان ~۵۶ پیکسل آزاد شد و راهنما کامل جا گرفت.
            // هیچ قابلیتی حذف نشد: هر دو انتخابگر با همان نام، همان رویداد و
            // همان رفتار سرِ جایشان هستند — فقط کنارِ هم نشستند.
            _txtGuardians = AddFilePickerHalf(p, "فایل سرپرستان:", RowFilePickers, true,
                path => _source.GuardiansFilePath = path);
            _txtMembers = AddFilePickerHalf(p, "فایل اعضای خانواده:", RowFilePickers, false,
                path => _source.MembersFilePath = path);

            _chkBackup = new CheckBox
            {
                Text = "قبل از همگام‌سازی، بکاپ کامل گرفته شود (به‌شدت توصیه می‌شود)",
                // ⚠ AutoSize نه: مختصاتِ Location در فرمِ آینه‌شده آینه نمی‌شود
                // (توضیح کاملش کنارِ ثابت‌های PickerArea)، پس یک تیکِ AutoSize
                // در x=۲۰ به لبه‌ی چپ می‌چسبید. با عرضِ کامل و ترازِ Left —
                // که در این فرم «راست» دیده می‌شود — تیک و متنش در لبه‌ی
                // راست و هم‌راستا با بقیه‌ی برچسب‌ها می‌نشینند.
                Checked = true, AutoSize = false,
                Location = new Point(PickerAreaLeft, RowBackupCheck),
                Size = new Size(PickerAreaRight - PickerAreaLeft - 160, 24),
                CheckAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(UiTheme.SizeBody), RightToLeft = RightToLeft.Yes
            };
            _chkBackup.CheckedChanged += delegate
            {
                _takeBackup = _chkBackup.Checked;
                if (_btnBackupPath != null) _btnBackupPath.Enabled = _takeBackup;
            };
            p.Controls.Add(_chkBackup);

            // ── انتخابِ مسیرِ بکاپ ──
            // آموزش — SyncOptions.BackupParentFolder از قبل وجود داشت ولی هیچ‌جا
            // در رابط کاربری تنظیم نمی‌شد، پس همیشه مسیرِ پیش‌فرض استفاده
            // می‌گردید. حالا کاربر می‌تواند جای بکاپ را خودش انتخاب کند.
            _btnBackupPath = UiTheme.CreateSecondaryButton("مسیر بکاپ...", "▤");
            _btnBackupPath.SetBounds(PickerAreaRight - 150, RowBackupCheck - 3, 150, 28);
            _btnBackupPath.Click += delegate
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "پوشه‌ای که بکاپ در آن ساخته شود";
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        _backupParentFolder = fbd.SelectedPath;
                        if (_lblBackupPath != null)
                            _lblBackupPath.Text = "مسیر بکاپ: " + _backupParentFolder;
                    }
                }
            };
            p.Controls.Add(_btnBackupPath);

            _lblBackupPath = new Label
            {
                Location = new Point(20, RowBackupPath), Size = new Size(PickerAreaRight - 190, 20),
                Font = UiTheme.Font(UiTheme.SizeSmall - 1F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "مسیر بکاپ: پیش‌فرضِ برنامه"
            };
            p.Controls.Add(_lblBackupPath);

            _chkImportMedia = new CheckBox
            {
                Text = "عکس‌ها و اسنادِ داخل بسته هم وارد شوند",
                Checked = true, AutoSize = false,
                Location = new Point(PickerAreaLeft, RowMediaCheck),
                Size = new Size(PickerAreaRight - PickerAreaLeft, 24),
                CheckAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(UiTheme.SizeBody), RightToLeft = RightToLeft.Yes
            };
            p.Controls.Add(_chkImportMedia);

            // ── دکمه‌ی «بررسی بسته‌ی ورودی» ──
            // در لبه‌ی راست، هم‌راستا با بقیه‌ی دکمه‌های این صفحه.
            _btnValidate = UiTheme.CreateButton("بررسی بسته‌ی ورودی", "⌕", UiTheme.Primary);
            _btnValidate.SetBounds(PickerAreaRight - 210, RowValidateButton, 210, 34);
            _btnValidate.Click += async delegate { await RunPackageValidation(); };
            p.Controls.Add(_btnValidate);

            _lblValidationState = new Label
            {
                Location = new Point(20, RowValidateState), Size = new Size(PickerAreaRight - 20, 22),
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "برای اطمینان، پیش از همگام‌سازی بسته را بررسی کنید."
            };
            p.Controls.Add(_lblValidationState);

            // ── راهنما ──
            // آموزش — راهنما از یک کادرِ همیشه‌بازِ ۳۰۰ پیکسلی به یک دکمه منتقل
            // شد. سه دلیل:
            //
            //   ۱. کادرِ قبلی هر خط را با ارتفاعِ ثابتِ ۱۸/۱۶ پیکسل و
            //      AutoSize=false می‌ساخت، پس هر جملهٔ بلندتر از یک خط بی‌صدا
            //      بریده می‌شد. متن‌ها به‌ناچار کوتاه و مبهم نوشته شده بودند تا
            //      جا بشوند — دقیقاً همان «دقیق نبودنِ» گزارش‌شده.
            //   ۲. راهنما نیمی از ارتفاعِ این مرحله را می‌گرفت، در حالی که
            //      کاربرِ باتجربه هر بار آن را می‌خواند و رد می‌شد.
            //   ۳. حالا FrmSyncHelp اسکرول دارد، پس راهنما می‌تواند کامل و
            //      دقیق باشد بدون هیچ محدودیتِ ارتفاع.
            //
            // هیچ قابلیتی حذف نشد؛ همان محتوا کامل‌تر و درست‌تر در پنجرهٔ راهنما
            // هست و با یک کلیک باز می‌شود.
            Button btnHelp = UiTheme.CreateButton("راهنمای کامل آماده‌سازی پوشه", "؟", UiTheme.PrimaryDark);
            btnHelp.SetBounds(PickerAreaRight - 300, RowHelpButton, 300, 40);
            btnHelp.Click += delegate { FrmSyncHelp.ShowHelp(this); };
            p.Controls.Add(btnHelp);

            var lblHelpHint = new Label
            {
                Location = new Point(20, RowHelpHint),
                Size = new Size(PickerAreaRight - 20, 40),
                Font = UiTheme.Font(UiTheme.SizeSmall),
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.TopLeft,
                Text = "بارِ اول حتماً راهنما را بخوانید: ساختارِ دقیقِ پوشه، نام‌گذاری فایل‌ها، " +
                       "قالب‌های مجاز، هشت مرحلهٔ کار، تضمین‌های ایمنی و درمانِ خطاهای رایج."
            };
            p.Controls.Add(lblHelpHint);
        }

        // یک پوشه انتخاب می‌شود و بقیه خودکار کشف می‌گردد.
        private void OnPackageFolderChosen(string folder)
        {
            _source.PackageFolder = folder;

            // با پاک‌کردنِ مسیر، این متد با رشته‌ی خالی صدا زده می‌شود؛ نباید
            // تلاش کند پوشه‌ی خالی را بکاود و فهرستِ گمراه‌کننده نشان دهد.
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                if (_lblPackageFound != null) _lblPackageFound.Text = "";
                return;
            }

            var found = new List<string>();

            string guardians = FindFirstExisting(folder,
                "Guardians.html", "Guardians.htm", "سرپرستان.html");
            // آموزش — «Members» نامِ رسمی/الزامیِ جدید است؛ اول امتحان می‌شود.
            // «FamilyMembers» فقط برای سازگاری با بسته‌های قدیمی نگه داشته شده.
            string members = FindFirstExisting(folder,
                "Members.html", "Members.htm", "FamilyMembers.html", "FamilyMembers.htm", "اعضا.html");

            if (guardians != null)
            {
                _source.GuardiansFilePath = guardians;
                _txtGuardians.Text = guardians;
                found.Add("فایل سرپرستان ✔");
            }
            if (members != null)
            {
                _source.MembersFilePath = members;
                _txtMembers.Text = members;
                found.Add("فایل اعضا ✔");
            }

            string photos = Path.Combine(folder, MediaScanner.PhotosFolderName);
            string docs = Path.Combine(folder, MediaScanner.DocumentsFolderName);

            if (Directory.Exists(photos))
            {
                int n = SafeCount(photos, false);
                found.Add("پوشه‌ی عکس‌ها ✔ (" + n + " فایل)");
            }
            else found.Add("پوشه‌ی عکس‌ها ندارد");

            if (Directory.Exists(docs))
            {
                int n = SafeCount(docs, true);
                found.Add("پوشه‌ی اسناد ✔ (" + n + " پرونده)");
            }
            else found.Add("پوشه‌ی اسناد ندارد");

            _lblPackageFound.Text = "کشف‌شده:   " + string.Join("     ", found.ToArray());
        }

        private static string FindFirstExisting(string folder, params string[] names)
        {
            foreach (string n in names)
            {
                string p = Path.Combine(folder, n);
                if (File.Exists(p)) return p;
            }
            // اگر نام دقیق نبود، هر فایل HTMLی را که نامش نشانه دارد پیدا کن.
            try
            {
                foreach (string f in Directory.GetFiles(folder, "*.htm*"))
                {
                    string baseName = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    foreach (string n in names)
                    {
                        string key = Path.GetFileNameWithoutExtension(n).ToLowerInvariant();
                        if (baseName.Contains(key)) return f;
                    }
                }
            }
            catch { }
            return null;
        }

        private static int SafeCount(string folder, bool directories)
        {
            try
            {
                return directories
                    ? Directory.GetDirectories(folder).Length
                    : Directory.GetFiles(folder).Length;
            }
            catch { return 0; }
        }

        // ─── مرحله ۲: تحلیل ──────────────────────────────────────────────────
        private void BuildPage1()
        {
            AddHint(_pages[1], "فایل‌ها تجزیه می‌شوند و رکوردها استخراج می‌گردند. هیچ چیزی در این مرحله در دیتابیس نوشته نمی‌شود.", 0);
            _lblParseSummary = AddBigLabel(_pages[1], 90);
        }

        // ─── مرحله ۳: اعتبارسنجی ─────────────────────────────────────────────
        private void BuildPage2()
        {
            AddHint(_pages[2], "بررسی ساختاری داده‌ها. هشدارها مانع ادامه نیستند؛ رکوردهای دارای خطا در همگام‌سازی نادیده گرفته می‌شوند.", 0);
            _lstValidation = new ListBox
            {
                Location = new Point(20, 70), Size = new Size(860, 380),
                Font = UiTheme.Font(UiTheme.SizeBody), RightToLeft = RightToLeft.Yes,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pages[2].Controls.Add(_lstValidation);
        }

        // ─── مرحله ۴: مقایسه ─────────────────────────────────────────────────
        private void BuildPage3()
        {
            AddHint(_pages[3], "رکوردهای فایل با دیتابیس مقایسه می‌شوند تا مشخص شود کدام جدید، کدام بروزرسانی و کدام بدون تغییر است.", 0);
            _lblCompareSummary = AddBigLabel(_pages[3], 90);
        }

        // ─── مرحله ۵: پیش‌نمایش (با جزئیات و انتخاب) ─────────────────────────
        private void BuildPage4()
        {
            Panel p = _pages[4];
            _lblPreviewInfo = AddHint(p, "فقط رکوردهای قابل‌اعمال (جدید/بروزرسانی) نمایش داده می‌شوند. با تیک هر ردیف مشخص کنید اعمال شود یا نه؛ برای دیدن تغییرات فیلدی روی «جزئیات» کلیک کنید.", 0);

            _grdPreview = new DataGridView
            {
                Location = new Point(20, 70), Size = new Size(860, 380),
                AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            UiTheme.StyleGrid(_grdPreview);

            var colSel = new DataGridViewCheckBoxColumn { Name = "sel", HeaderText = "اعمال", FillWeight = 40 };
            var colType = new DataGridViewTextBoxColumn { Name = "type", HeaderText = "نوع", ReadOnly = true, FillWeight = 60 };
            var colCode = new DataGridViewTextBoxColumn { Name = "code", HeaderText = "کد عمومی", ReadOnly = true, FillWeight = 70 };
            var colName = new DataGridViewTextBoxColumn { Name = "name", HeaderText = "نام", ReadOnly = true, FillWeight = 140 };
            var colAct = new DataGridViewTextBoxColumn { Name = "act", HeaderText = "عملیات", ReadOnly = true, FillWeight = 70 };
            var colCnt = new DataGridViewTextBoxColumn { Name = "cnt", HeaderText = "تغییرات", ReadOnly = true, FillWeight = 60 };
            var colDetail = new DataGridViewButtonColumn { Name = "detail", HeaderText = "", Text = "جزئیات", UseColumnTextForButtonValue = true, FillWeight = 60 };
            _grdPreview.Columns.AddRange(colSel, colType, colCode, colName, colAct, colCnt, colDetail);

            _grdPreview.CellClick += Preview_CellClick;
            _grdPreview.CellValueChanged += Preview_CellValueChanged;
            _grdPreview.CurrentCellDirtyStateChanged += delegate
            {
                if (_grdPreview.IsCurrentCellDirty)
                    _grdPreview.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            p.Controls.Add(_grdPreview);

            // ═══ افزودنی: نمای «خلاصه‌ی هر پرونده» ══════════════════════════════
            // آموزش — چرا این نما اضافه شد: جدولِ بالا فهرستی تختِ رکوردهاست و
            // کاربر نمی‌تواند ببیند «پرونده‌ی ۱۰۰۲۴۵ چند عضو، چند عکس و چند سند
            // دارد». این نما همان خلاصه‌ی درخواستی را سطر‌به‌سطر می‌دهد.
            // جدولِ قبلی دست‌نخورده ماند و فقط با کلیدِ نما پنهان/آشکار می‌شود.
            _grdCaseSummary = new DataGridView
            {
                Location = new Point(20, 70), Size = new Size(860, 380),
                AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, MultiSelect = false, ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Visible = false
            };
            UiTheme.StyleGrid(_grdCaseSummary);
            _grdCaseSummary.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "sCode",  HeaderText = "کد پرونده",   FillWeight = 70 },
                new DataGridViewTextBoxColumn { Name = "sGuard", HeaderText = "سرپرست",      FillWeight = 70 },
                new DataGridViewTextBoxColumn { Name = "sMem",   HeaderText = "اعضا",        FillWeight = 45 },
                new DataGridViewTextBoxColumn { Name = "sPhoto", HeaderText = "عکس",         FillWeight = 55 },
                new DataGridViewTextBoxColumn { Name = "sDocs",  HeaderText = "اسناد",       FillWeight = 45 },
                new DataGridViewTextBoxColumn { Name = "sChg",   HeaderText = "تغییرات",     FillWeight = 55 },
                new DataGridViewTextBoxColumn { Name = "sWarn",  HeaderText = "هشدار",       FillWeight = 45 },
                new DataGridViewTextBoxColumn { Name = "sErr",   HeaderText = "خطا",         FillWeight = 40 });
            _grdCaseSummary.CellFormatting += CaseSummary_CellFormatting;
            p.Controls.Add(_grdCaseSummary);

            // کلیدِ تعویضِ نما
            _cmbPreviewMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(PickerAreaRight - 220, 40), Size = new Size(220, 26),
                Font = UiTheme.Font(UiTheme.SizeSmall)
            };
            _cmbPreviewMode.Items.AddRange(new object[] { "نمای رکوردها", "خلاصه‌ی هر پرونده" });
            _cmbPreviewMode.SelectedIndex = 0;
            _cmbPreviewMode.SelectedIndexChanged += delegate { ApplyPreviewMode(); };
            p.Controls.Add(_cmbPreviewMode);

            // ── صفحه‌بندی ──
            // آموزش — پیش از این سقفِ ثابتِ ۵۰۰۰ ردیف بود: با ده‌هزار پرونده،
            // بقیه اصلاً دیده نمی‌شدند (هرچند اعمال می‌گشتند) و همان ۵۰۰۰ ردیف
            // یک‌جا در حافظه ساخته می‌شد. حالا صفحه‌به‌صفحه است و همه در دسترس‌اند.
            _previewPager = new Helpers.GridPager
            {
                Dock = DockStyle.None,
                Location = new Point(20, 452),
                Size = new Size(860, 44)
            };
            _previewPager.PageChanged += delegate { RenderPreviewPage(); };
            p.Controls.Add(_previewPager);
        }

        private void ApplyPreviewMode()
        {
            bool summary = _cmbPreviewMode != null && _cmbPreviewMode.SelectedIndex == 1;
            _grdPreview.Visible = !summary;
            _grdCaseSummary.Visible = summary;
            RenderPreviewPage();
        }

        // رنگ‌آمیزی: پرونده‌ی دارای خطا قرمز، دارای هشدار نارنجی.
        private void CaseSummary_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grdCaseSummary.Rows.Count) return;

            DataGridViewRow row = _grdCaseSummary.Rows[e.RowIndex];
            int errors = ToInt(row.Cells["sErr"].Value);
            int warnings = ToInt(row.Cells["sWarn"].Value);

            if (errors > 0) row.DefaultCellStyle.ForeColor = Color.FromArgb(0xC0, 0x39, 0x2B);
            else if (warnings > 0) row.DefaultCellStyle.ForeColor = Color.FromArgb(0xA9, 0x6A, 0x0B);
            else row.DefaultCellStyle.ForeColor = UiTheme.TextDark;
        }

        private static int ToInt(object value)
        {
            int n;
            return int.TryParse(Convert.ToString(value), out n) ? n : 0;
        }

        // ─── مرحله ۶: تأیید ──────────────────────────────────────────────────
        private void BuildPage5()
        {
            AddHint(_pages[5], "خلاصه‌ی نهایی آنچه اعمال خواهد شد. با زدن «بعدی» همگام‌سازی آغاز می‌شود (پس از بکاپ).", 0);
            _lblConfirmSummary = AddBigLabel(_pages[5], 90);
        }

        // ─── مرحله ۷: همگام‌سازی ─────────────────────────────────────────────
        private void BuildPage6()
        {
            AddHint(_pages[6], "در حال بکاپ‌گیری و اعمال تغییرات در یک تراکنش اتمیک. لطفاً صبر کنید...", 0);
        }

        // ─── مرحله ۸: گزارش ──────────────────────────────────────────────────
        private void BuildPage7()
        {
            AddHint(_pages[7], "گزارش نهایی همگام‌سازی:", 0);
            _txtReport = new TextBox
            {
                Location = new Point(20, 70), Size = new Size(860, 380),
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9.5F), RightToLeft = RightToLeft.No,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pages[7].Controls.Add(_txtReport);

            // ── دکمه‌های پس از پایان ──
            // آموزش — پیش از این، گزارش فقط داخل همین کادر می‌ماند: کاربر نه
            // می‌توانست بکاپ را پیدا کند و نه گزارش را برای بایگانی نگه دارد.
            _btnOpenBackup = UiTheme.CreateSecondaryButton("باز کردن پوشه‌ی بکاپ", "▤");
            _btnOpenBackup.SetBounds(PickerAreaRight - 220, 458, 220, 32);
            _btnOpenBackup.Click += delegate { OpenBackupFolder(); };
            _pages[7].Controls.Add(_btnOpenBackup);

            _btnSaveReport = UiTheme.CreateSecondaryButton("ذخیره‌ی گزارش در فایل", "⇑");
            _btnSaveReport.SetBounds(PickerAreaRight - 452, 458, 220, 32);
            _btnSaveReport.Click += delegate { SaveReportToFile(); };
            _pages[7].Controls.Add(_btnSaveReport);
        }

        // ─── باز کردن پوشه‌ی بکاپ ────────────────────────────────────────────
        private void OpenBackupFolder()
        {
            string path = _report == null ? null : _report.BackupPath;

            if (string.IsNullOrWhiteSpace(path))
            {
                UiTheme.ShowWarning(this, "در این اجرا بکاپی گرفته نشده است.");
                return;
            }

            try
            {
                // BackupPath یک پوشه است؛ اگر نبود، پوشه‌ی والدش باز می‌شود.
                string target = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target))
                {
                    UiTheme.ShowWarning(this, "پوشه‌ی بکاپ پیدا نشد:" + Environment.NewLine + path);
                    return;
                }
                System.Diagnostics.Process.Start("explorer.exe", "\"" + target + "\"");
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "باز کردن پوشه ممکن نشد: " + ex.Message);
            }
        }

        // ─── ذخیره‌ی گزارش در فایل متنی ──────────────────────────────────────
        private void SaveReportToFile()
        {
            if (_txtReport == null || string.IsNullOrWhiteSpace(_txtReport.Text))
            {
                UiTheme.ShowWarning(this, "گزارشی برای ذخیره وجود ندارد.");
                return;
            }

            try
            {
                string root = FileHelper.GetOrChooseBaseRootFolder();
                if (string.IsNullOrWhiteSpace(root))
                {
                    UiTheme.ShowWarning(this, "محل ذخیره فایل‌ها مشخص نیست.");
                    return;
                }

                string folder = Path.Combine(root, "Reports");
                Directory.CreateDirectory(folder);

                string file = Path.Combine(folder, "گزارش_همگام‌سازی_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss",
                        System.Globalization.CultureInfo.InvariantCulture) + ".txt");

                // BOM دارد تا Notepad متنِ فارسی را درست باز کند.
                File.WriteAllText(file, _txtReport.Text, new System.Text.UTF8Encoding(true));
                UiTheme.ShowSuccess(this, "گزارش ذخیره شد:" + Environment.NewLine + file);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "ذخیره‌ی گزارش ممکن نشد: " + ex.Message);
            }
        }

        // ═══ ناوبری ═══════════════════════════════════════════════════════════
        private void ShowStep(int step)
        {
            _step = Math.Max(0, Math.Min(_pages.Length - 1, step));
            for (int i = 0; i < _pages.Length; i++)
                _pages[i].Visible = (i == _step);

            _lblStepHeader.Text = _stepTitles[_step];
            _btnBack.Enabled = _step > 0 && _step != 6 && _step != 7;
            _btnNext.Enabled = true;

            _btnNext.Text = _step == 5 ? "◀  شروع همگام‌سازی"
                          : _step == 7 ? "پایان"
                          : "بعدی  ◀";
            _btnCancel.Enabled = _step != 6;
        }

        private void GoBack()
        {
            if (_step > 0) ShowStep(_step - 1);
        }

        private async Task GoNext()
        {
            switch (_step)
            {
                case 0: // → تحلیل
                    if (!ValidateFilesChosen()) return;
                    if (!WarnIfPackageAlreadyImported()) return;
                    if (!ValidationAllowsContinue()) return;   // دروازه‌ی پیش از پرواز
                    ShowStep(1);
                    await RunParse();
                    break;
                case 1: ShowStep(2); RunValidation(); break;
                case 2: ShowStep(3); await RunCompare(); break;
                case 3:
                    // آموزش — پویشِ عکس/سند عمداً «پیش از» ساختِ پیش‌نمایش انجام
                    // می‌شود، نه بعد از آن. دلیل: نمای «خلاصه‌ی هر پرونده» باید
                    // تعدادِ عکس و سندِ هر پرونده را نشان دهد؛ اگر پویش دیرتر
                    // اجرا شود، آن ستون‌ها همیشه خالی می‌مانند.
                    await ScanMediaIfRequested();
                    ShowStep(4);
                    BuildPreview();
                    break;
                case 4: ShowStep(5); BuildConfirm(); break;
                case 5: ShowStep(6); await RunSync(); break;
                case 6: ShowStep(7); break; // معمولاً خودکار پیش می‌رود
                case 7: Close(); break;
            }
        }

        private bool ValidateFilesChosen()
        {
            if (string.IsNullOrWhiteSpace(_source.GuardiansFilePath) &&
                string.IsNullOrWhiteSpace(_source.MembersFilePath))
            {
                UiTheme.ShowWarning(this, "حداقل یکی از دو فایل را انتخاب کنید.");
                return false;
            }
            return true;
        }

        // ═══ اجرای مرحله‌ها ═══════════════════════════════════════════════════
        private async Task RunParse()
        {
            SetBusy(true, "در حال تجزیه فایل‌ها...");
            var provider = new HtmlSyncProvider();
            var progress = new Progress<SyncProgress>(UpdateProgress);
            try
            {
                _parsed = await Task.Run(() => provider.Parse(_source, progress));
                _lblParseSummary.Text =
                    "سرپرستان استخراج‌شده: " + _parsed.Guardians.Count + "\n" +
                    "اعضای خانواده استخراج‌شده: " + _parsed.Members.Count;
            }
            catch (Exception ex)
            {
                _lblParseSummary.Text = "خطا در تجزیه: " + ex.Message;
                _btnNext.Enabled = false;
            }
            finally { SetBusy(false, null); }
        }

        private void RunValidation()
        {
            _lstValidation.Items.Clear();
            var provider = new HtmlSyncProvider();
            var errors = provider.Validate(_parsed);
            if (errors.Count == 0)
                _lstValidation.Items.Add("✓ هیچ اخطاری یافت نشد.");
            else
                foreach (var e in errors) _lstValidation.Items.Add("• " + e);
        }

        private async Task RunCompare()
        {
            SetBusy(true, "در حال مقایسه با دیتابیس...");
            var comparer = new SyncComparer(new DatabaseHelper());
            var progress = new Progress<SyncProgress>(UpdateProgress);
            try
            {
                _plan = await Task.Run(() => comparer.BuildPlan(_parsed, progress));
                _lblCompareSummary.Text =
                    "── سرپرستان ──\n" +
                    "جدید: " + _plan.NewGuardians + "   بروزرسانی: " + _plan.UpdatedGuardians +
                    "   بدون تغییر: " + _plan.UnchangedGuardians +
                    "   تکراری: " + _plan.DuplicateGuardians + "   خطا: " + _plan.ErrorGuardians + "\n\n" +
                    "── اعضای خانواده ──\n" +
                    "جدید: " + _plan.NewMembers + "   بروزرسانی: " + _plan.UpdatedMembers +
                    "   بدون تغییر: " + _plan.UnchangedMembers +
                    "   تکراری: " + _plan.DuplicateMembers + "   خطا: " + _plan.ErrorMembers;
            }
            catch (Exception ex)
            {
                _lblCompareSummary.Text = "خطا در مقایسه: " + ex.Message;
                _btnNext.Enabled = false;
            }
            finally { SetBusy(false, null); }
        }

        private void BuildPreview()
        {
            _actionableRecords.Clear();
            _caseSummaries.Clear();
            if (_plan == null) { RenderPreviewPage(); return; }

            // فقط رکوردهای قابل‌اعمال (جدید/بروزرسانی) نمایش داده می‌شوند.
            _actionableRecords.AddRange(_plan.Guardians.Concat(_plan.Members)
                .Where(r => r.Action == SyncAction.New || r.Action == SyncAction.Update));

            BuildCaseSummaries();
            ApplyPreviewMode();
        }

        // ─── ساختِ خلاصه‌ی هر پرونده ─────────────────────────────────────────
        // آموزش — این متد کلاسِ MediaCaseSummary را که تا حالا بی‌استفاده مانده
        // بود پر می‌کند: رکوردهای متنی بر اساس «کد عمومی» گروه می‌شوند و بعد
        // شمارشِ عکس و سند از برنامه‌ی رسانه (اگر پویش شده باشد) به همان کد
        // وصل می‌گردد. این‌طور کاربر هر پرونده را یک‌جا می‌بیند.
        private void BuildCaseSummaries()
        {
            var byCode = new Dictionary<string, MediaCaseSummary>(StringComparer.OrdinalIgnoreCase);

            foreach (SyncRecord r in _actionableRecords)
            {
                string code = (r.PublicCode ?? "").Trim();
                if (code.Length == 0) continue;

                MediaCaseSummary s;
                if (!byCode.TryGetValue(code, out s))
                {
                    s = new MediaCaseSummary { CaseCode = code };
                    byCode[code] = s;
                }

                if (r.Entity == SyncEntity.Guardian) s.GuardianFound = true;
                else s.MemberCount++;

                if (r.Action == SyncAction.Update)
                    s.Warnings += 0;   // تغییرِ فیلدی هشدار نیست؛ فقط شمرده می‌شود
            }

            // شمارشِ تغییراتِ فیلدی به‌ازای هر پرونده
            var changeCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (SyncRecord r in _actionableRecords)
            {
                string code = (r.PublicCode ?? "").Trim();
                if (code.Length == 0) continue;
                int n = r.Action == SyncAction.Update ? r.Changes.Count(c => c.Selected) : 0;
                changeCount[code] = (changeCount.ContainsKey(code) ? changeCount[code] : 0) + n;
            }
            _previewChangeCounts = changeCount;

            // ── وصل‌کردنِ عکس و سند از برنامه‌ی رسانه ──
            // آموزش — نکته‌ی کلیدی که با آزمون پیدا شد: اینجا باید «آنچه در
            // بسته هست» شمرده شود، نه «آنچه همین حالا قابلِ اتصال است».
            //
            // چرا: MediaScanner کدها را با TblCaseِ فعلی تطبیق می‌دهد. برای
            // پرونده‌های *جدید* که هنوز ثبت نشده‌اند، نتیجه NoMatch است. اگر
            // خلاصه بر پایه‌ی IsApplicable ساخته شود، برای همه‌ی پرونده‌های تازه
            // «۰ سند و بدون عکس» نشان می‌دهد — در حالی که فایل‌ها در بسته
            // موجودند و بعد از ثبتِ متن، وصل خواهند شد.
            // پس شمارش بر پایه‌ی حضور در بسته است و فقط ایرادهای واقعیِ فایل
            // (خرابی/فرمتِ نامعتبر/تکراری) به‌عنوان خطا یا هشدار شمرده می‌شوند.
            if (_mediaPlan != null)
            {
                foreach (MediaItem photo in _mediaPlan.Photos)
                {
                    MediaCaseSummary s = Ensure(byCode, photo.CaseCode);
                    if (s == null) continue;

                    switch (photo.Action)
                    {
                        case MediaAction.Corrupt:
                        case MediaAction.Unsupported:
                        case MediaAction.InvalidName:
                            s.Errors++;
                            break;

                        case MediaAction.Duplicate:
                        case MediaAction.OtherCenter:
                            s.Warnings++;
                            break;

                        default:
                            // Add / Replace / NoMatch(پرونده‌ی تازه) → عکس در بسته هست
                            s.PhotoFound = true;
                            break;
                    }
                }

                foreach (MediaItem doc in _mediaPlan.Documents)
                {
                    MediaCaseSummary s = Ensure(byCode, doc.CaseCode);
                    if (s == null) continue;

                    switch (doc.Action)
                    {
                        case MediaAction.Unsupported:
                        case MediaAction.InvalidName:
                            s.Errors++;
                            break;

                        case MediaAction.OtherCenter:
                            s.Warnings++;
                            break;

                        default:
                            s.DocumentCount++;
                            break;
                    }
                }
            }

            _caseSummaries.AddRange(byCode.Values.OrderBy(s => s.CaseCode, StringComparer.OrdinalIgnoreCase));
        }

        // فقط برای پرونده‌هایی که در بسته‌ی متنی هستند خلاصه می‌سازیم؛ عکسِ
        // بی‌صاحب اینجا ردیف نمی‌گیرد (در گزارشِ بررسی فهرست می‌شود).
        private static MediaCaseSummary Ensure(Dictionary<string, MediaCaseSummary> map, string code)
        {
            code = (code ?? "").Trim();
            if (code.Length == 0) return null;

            MediaCaseSummary s;
            return map.TryGetValue(code, out s) ? s : null;
        }

        // ─── نمایشِ صفحه‌ی جاری ──────────────────────────────────────────────
        private void RenderPreviewPage()
        {
            bool summary = _cmbPreviewMode != null && _cmbPreviewMode.SelectedIndex == 1;
            int total = summary ? _caseSummaries.Count : _actionableRecords.Count;

            if (_previewPager != null) _previewPager.SetTotal(total);
            int pageSize = _previewPager != null ? _previewPager.PageSize : 100;
            int offset = _previewPager != null ? _previewPager.Offset : 0;

            if (summary) RenderSummaryRows(offset, pageSize);
            else RenderRecordRows(offset, pageSize);

            _lblPreviewInfo.Text = summary
                ? "خلاصه‌ی " + _caseSummaries.Count + " پرونده — عکس و سند وقتی دیده می‌شوند که بسته را بررسی کرده باشید."
                : "رکوردهای قابل‌اعمال: " + _actionableRecords.Count +
                  "  —  با تیک هر ردیف اعمال/عدم‌اعمال را مشخص کنید؛ «جزئیات» تغییراتِ فیلدی را نشان می‌دهد.";
        }

        private void RenderRecordRows(int offset, int pageSize)
        {
            _grdPreview.Rows.Clear();
            _previewRecords.Clear();

            for (int i = offset; i < Math.Min(_actionableRecords.Count, offset + pageSize); i++)
            {
                SyncRecord rec = _actionableRecords[i];
                _previewRecords.Add(rec);

                int idx = _grdPreview.Rows.Add(
                    rec.Selected,
                    rec.Entity == SyncEntity.Guardian ? "سرپرست" : "عضو",
                    rec.PublicCode,
                    rec.Title,
                    rec.Action == SyncAction.New ? "جدید" : "بروزرسانی",
                    rec.Action == SyncAction.New ? "—" : rec.Changes.Count(c => c.Selected).ToString());
                _grdPreview.Rows[idx].Tag = rec;
            }
        }

        private void RenderSummaryRows(int offset, int pageSize)
        {
            _grdCaseSummary.Rows.Clear();

            for (int i = offset; i < Math.Min(_caseSummaries.Count, offset + pageSize); i++)
            {
                MediaCaseSummary s = _caseSummaries[i];

                int changes = 0;
                if (_previewChangeCounts != null)
                    _previewChangeCounts.TryGetValue(s.CaseCode, out changes);

                _grdCaseSummary.Rows.Add(
                    s.CaseCode,
                    s.GuardianFound ? "پیدا شد" : "—",
                    s.MemberCount.ToString(),
                    s.PhotoFound ? "پیدا شد" : "—",
                    s.DocumentCount.ToString(),
                    changes.ToString(),
                    s.Warnings.ToString(),
                    s.Errors.ToString());
            }
        }

        private void Preview_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != _grdPreview.Columns["sel"].Index) return;
            var rec = _grdPreview.Rows[e.RowIndex].Tag as SyncRecord;
            if (rec != null)
                rec.Selected = Convert.ToBoolean(_grdPreview.Rows[e.RowIndex].Cells["sel"].Value);
        }

        private void Preview_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex != _grdPreview.Columns["detail"].Index) return;

            var rec = _grdPreview.Rows[e.RowIndex].Tag as SyncRecord;
            if (rec == null) return;
            ShowDetailDialog(rec);
            // به‌روزرسانی شمارش تغییرات پس از احتمال تغییر انتخاب‌ها
            if (rec.Action == SyncAction.Update)
                _grdPreview.Rows[e.RowIndex].Cells["cnt"].Value = rec.Changes.Count(c => c.Selected).ToString();
        }

        // دیالوگ جزئیات تغییرات: کد عمومی، نام فیلد، مقدار قبلی، مقدار جدید + چک‌باکس.
        private void ShowDetailDialog(SyncRecord rec)
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "جزئیات تغییرات — کد " + rec.PublicCode;
                dlg.RightToLeft = RightToLeft.Yes; dlg.RightToLeftLayout = true;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false; dlg.MinimizeBox = false; dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(680, 420);
                dlg.BackColor = UiTheme.CardBack; dlg.Font = UiTheme.Font(UiTheme.SizeBody);

                var grd = new DataGridView
                {
                    Location = new Point(14, 14), Size = new Size(652, 350),
                    AllowUserToAddRows = false, RowHeadersVisible = false, MultiSelect = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };
                UiTheme.StyleGrid(grd);

                if (rec.Action == SyncAction.New)
                {
                    grd.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "فیلد", Name = "f", ReadOnly = true, FillWeight = 40 });
                    grd.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "مقدار جدید", Name = "n", ReadOnly = true, FillWeight = 60 });
                    foreach (var kv in rec.SourceValues)
                        if (!string.Equals(kv.Key, "Code", StringComparison.OrdinalIgnoreCase))
                            grd.Rows.Add(UiTheme.TranslateHeader(kv.Key), kv.Value);
                }
                else
                {
                    var cApply = new DataGridViewCheckBoxColumn { HeaderText = "اعمال", Name = "a", FillWeight = 22 };
                    grd.Columns.Add(cApply);
                    grd.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "فیلد", Name = "f", ReadOnly = true, FillWeight = 26 });
                    grd.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "مقدار قبلی", Name = "o", ReadOnly = true, FillWeight = 26 });
                    grd.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "مقدار جدید", Name = "n", ReadOnly = true, FillWeight = 26 });
                    foreach (var ch in rec.Changes)
                    {
                        int i = grd.Rows.Add(ch.Selected, ch.DisplayName, ch.OldValue, ch.NewValue);
                        grd.Rows[i].Tag = ch;
                    }
                    grd.CurrentCellDirtyStateChanged += delegate
                    {
                        if (grd.IsCurrentCellDirty) grd.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    };
                    grd.CellValueChanged += delegate (object s, DataGridViewCellEventArgs ev)
                    {
                        if (ev.RowIndex < 0 || ev.ColumnIndex != 0) return;
                        var ch = grd.Rows[ev.RowIndex].Tag as FieldChange;
                        if (ch != null) ch.Selected = Convert.ToBoolean(grd.Rows[ev.RowIndex].Cells[0].Value);
                    };
                }

                dlg.Controls.Add(grd);

                Button ok = UiTheme.CreateButton("بستن", "", UiTheme.Primary);
                ok.SetBounds(dlg.ClientSize.Width - 134, 374, 120, 34);
                ok.DialogResult = DialogResult.OK;
                dlg.Controls.Add(ok);
                dlg.AcceptButton = ok;

                dlg.ShowDialog(this);
            }
        }

        private void BuildConfirm()
        {
            int gNew = _plan.Guardians.Count(r => r.Selected && r.Action == SyncAction.New);
            int gUpd = _plan.Guardians.Count(r => r.Selected && r.Action == SyncAction.Update && r.HasApplicableWork);
            int mNew = _plan.Members.Count(r => r.Selected && r.Action == SyncAction.New);
            int mUpd = _plan.Members.Count(r => r.Selected && r.Action == SyncAction.Update && r.HasApplicableWork);

            _lblConfirmSummary.Text =
                "با ادامه، موارد زیر اعمال می‌شوند:\n\n" +
                "• سرپرستان جدید: " + gNew + "\n" +
                "• سرپرستان بروزرسانی: " + gUpd + "\n" +
                "• اعضای جدید: " + mNew + "\n" +
                "• اعضای بروزرسانی: " + mUpd + "\n\n" +
                BuildMediaConfirmText() +
                (_takeBackup ? "✓ ابتدا بکاپ کامل گرفته می‌شود." : "⚠ بدون بکاپ (توصیه نمی‌شود).") + "\n" +
                "همه‌ی تغییرات در یک تراکنش اتمیک انجام می‌شود؛ در صورت خطا کاملاً برگردانده می‌شود.";
        }

        // ═══ بررسیِ بسته (فقط خواندنی) ════════════════════════════════════════
        // آموزش — این مرحله هیچ چیزی نمی‌نویسد: نه در دیتابیس، نه روی دیسک.
        // فقط بسته را می‌خواند و فهرستی از ایرادها می‌سازد تا کاربر پیش از
        // همگام‌سازی اصلاحشان کند.
        private async Task RunPackageValidation()
        {
            if (string.IsNullOrWhiteSpace(_source.PackageFolder))
            {
                UiTheme.ShowWarning(this,
                    "ابتدا پوشه‌ی بسته‌ی ورودی را انتخاب کنید." + Environment.NewLine +
                    "بررسی روی کلِ بسته انجام می‌شود، نه روی تک‌فایل.");
                return;
            }

            SetBusy(true, "در حال بررسی بسته...");
            _btnValidate.Enabled = false;

            // بررسیِ بسته‌ی بزرگ می‌تواند ده‌ها ثانیه طول بکشد؛ باید قابلِ لغو باشد.
            _activeCancel = new System.Threading.CancellationTokenSource();
            _btnCancel.Enabled = true;
            _btnCancel.Text = "لغو";

            try
            {
                var validator = new PackageValidator(new DatabaseHelper());
                var progress = new Progress<SyncProgress>(UpdateProgress);
                string folder = _source.PackageFolder;
                System.Threading.CancellationToken token = _activeCancel.Token;

                _validation = await Task.Run(() => validator.Validate(folder, progress, token));
                _warningsAccepted = false;

                UpdateValidationState();

                using (var dlg = new FrmValidationReport(_validation))
                    dlg.ShowDialog(this);

                // اگر فقط هشدار دارد، تأییدِ صریحِ کاربر گرفته می‌شود.
                if (_validation.NeedsConfirmation)
                {
                    _warningsAccepted = UiTheme.ShowConfirm(this,
                        "بسته " + _validation.WarningCount + " هشدار دارد ولی خطای بحرانی ندارد." +
                        Environment.NewLine + "با ادامه، موارد هشداردار نادیده گرفته می‌شوند." +
                        Environment.NewLine + Environment.NewLine + "ادامه می‌دهید؟",
                        "تأیید ادامه با وجود هشدار");
                    UpdateValidationState();
                }
            }
            catch (OperationCanceledException)
            {
                // لغوِ کاربر خطا نیست؛ فقط نتیجه‌ی نیمه‌کاره نگه داشته نمی‌شود.
                _validation = null;
                UpdateValidationState();
            }
            catch (Exception ex)
            {
                _validation = null;
                UiTheme.ShowError(this, "بررسی بسته ممکن نشد: " + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
                _btnValidate.Enabled = true;

                // دکمه‌ی لغو به رفتارِ عادیِ «انصراف» برمی‌گردد.
                if (_activeCancel != null) { _activeCancel.Dispose(); _activeCancel = null; }
                _btnCancel.Text = "انصراف";
                _btnCancel.Enabled = true;
            }
        }

        // وضعیتِ دکمه‌ی «بعدی» بر اساس نتیجه‌ی بررسی.
        private void UpdateValidationState()
        {
            if (_lblValidationState == null) return;

            if (_validation == null)
            {
                _lblValidationState.Text = "برای اطمینان، پیش از همگام‌سازی بسته را بررسی کنید.";
                _lblValidationState.ForeColor = UiTheme.TextMuted;
                return;
            }

            switch (_validation.OverallSeverity)
            {
                case ValidationSeverity.Critical:
                    _lblValidationState.Text = "✕   " + _validation.CriticalCount +
                        " خطای بحرانی — تا رفع نشوند همگام‌سازی شروع نمی‌شود.";
                    _lblValidationState.ForeColor = Color.FromArgb(0xC0, 0x39, 0x2B);
                    break;

                case ValidationSeverity.Warning:
                    _lblValidationState.Text = _warningsAccepted
                        ? "!   " + _validation.WarningCount + " هشدار — تأیید شد، می‌توانید ادامه دهید."
                        : "!   " + _validation.WarningCount + " هشدار — برای ادامه باید تأیید کنید.";
                    _lblValidationState.ForeColor = Color.FromArgb(0xA9, 0x6A, 0x0B);
                    break;

                default:
                    _lblValidationState.Text = "✓   بسته سالم است و آماده‌ی همگام‌سازی.";
                    _lblValidationState.ForeColor = Color.FromArgb(0x1E, 0x8E, 0x4A);
                    break;
            }
        }

        // ═══ تشخیصِ بسته‌ی تکراری ════════════════════════════════════════════
        // آموزش — «اثر انگشتِ» بسته از مسیر + نام/اندازه/تاریخِ فایل‌های اطلاعات
        // ساخته می‌شود. پس از هر همگام‌سازیِ موفق ذخیره می‌گردد و دفعه‌ی بعد
        // مقایسه می‌شود. هدف فقط هشدار است، نه جلوگیری — چون گاهی کاربر عمداً
        // همان بسته را دوباره وارد می‌کند (مثلاً بعد از رفعِ ایراد).
        private const string LastPackageKey = "LastSyncedPackage";

        private string BuildPackageFingerprint()
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(_source.PackageFolder))
                parts.Add(_source.PackageFolder.Trim().ToLowerInvariant());

            foreach (string path in new[] { _source.GuardiansFilePath, _source.MembersFilePath })
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                try
                {
                    var info = new FileInfo(path);
                    if (!info.Exists) continue;
                    parts.Add(info.Name + "|" + info.Length + "|" +
                              info.LastWriteTimeUtc.ToString("yyyyMMddHHmmss",
                                  System.Globalization.CultureInfo.InvariantCulture));
                }
                catch { }
            }

            return parts.Count == 0 ? "" : string.Join("::", parts.ToArray());
        }

        private bool WarnIfPackageAlreadyImported()
        {
            string fingerprint = BuildPackageFingerprint();
            if (string.IsNullOrWhiteSpace(fingerprint)) return true;

            string previous;
            try { previous = SettingsHelper.Get(LastPackageKey, ""); }
            catch { return true; }

            if (!string.Equals(previous, fingerprint, StringComparison.Ordinal)) return true;

            return UiTheme.ShowConfirm(this,
                "به‌نظر می‌رسد همین بسته قبلاً همگام‌سازی شده است." + Environment.NewLine +
                "اگر ادامه دهید، رکوردهای بدون تغییر دوباره نوشته نمی‌شوند و مشکلی " +
                "پیش نمی‌آید؛ ولی شاید بسته‌ی اشتباهی انتخاب کرده باشید." + Environment.NewLine +
                Environment.NewLine + "ادامه می‌دهید؟",
                "بسته‌ی تکراری");
        }

        private void RememberImportedPackage()
        {
            try
            {
                string fingerprint = BuildPackageFingerprint();
                if (!string.IsNullOrWhiteSpace(fingerprint))
                    SettingsHelper.Set(LastPackageKey, fingerprint);
            }
            catch { /* ذخیره نشدنِ این نشانه، مانعِ کار نیست */ }
        }

        // دروازه‌ی نهایی: آیا اجازه‌ی عبور از مرحله‌ی اول هست؟
        private bool ValidationAllowsContinue()
        {
            // بررسی اجباری نیست مگر پوشه‌ی بسته انتخاب شده باشد — تا جریانِ
            // قدیمیِ «دو فایل دستی» دقیقاً مثل قبل کار کند.
            if (string.IsNullOrWhiteSpace(_source.PackageFolder)) return true;

            if (_validation == null)
            {
                bool proceed = UiTheme.ShowConfirm(this,
                    "هنوز بسته را بررسی نکرده‌اید." + Environment.NewLine +
                    "توصیه می‌شود ابتدا «بررسی بسته‌ی ورودی» را بزنید." + Environment.NewLine +
                    Environment.NewLine + "بدون بررسی ادامه می‌دهید؟",
                    "بررسی انجام نشده");
                return proceed;
            }

            if (!_validation.CanSynchronize)
            {
                UiTheme.ShowWarning(this,
                    "بسته " + _validation.CriticalCount + " خطای بحرانی دارد و همگام‌سازی شروع نمی‌شود." +
                    Environment.NewLine + "ابتدا خطاها را برطرف و دوباره بررسی کنید.");
                return false;
            }

            if (_validation.NeedsConfirmation && !_warningsAccepted)
            {
                _warningsAccepted = UiTheme.ShowConfirm(this,
                    "بسته " + _validation.WarningCount + " هشدار دارد." + Environment.NewLine +
                    "با ادامه، موارد هشداردار نادیده گرفته می‌شوند." + Environment.NewLine +
                    Environment.NewLine + "ادامه می‌دهید؟",
                    "تأیید ادامه با وجود هشدار");

                UpdateValidationState();
                return _warningsAccepted;
            }

            return true;
        }

        // ═══ پویشِ عکس و سند ══════════════════════════════════════════════════
        private async Task ScanMediaIfRequested()
        {
            _mediaPlan = null;

            if (_chkImportMedia == null || !_chkImportMedia.Checked) return;
            if (string.IsNullOrWhiteSpace(_source.PackageFolder)) return;

            SetBusy(true, "در حال بررسی عکس‌ها و اسناد...");
            try
            {
                var scanner = new MediaScanner(new DatabaseHelper());
                var progress = new Progress<SyncProgress>(UpdateProgress);
                string folder = _source.PackageFolder;
                _mediaPlan = await Task.Run(() => scanner.Scan(folder, progress));
            }
            catch (Exception ex)
            {
                _mediaPlan = null;
                UiTheme.ShowWarning(this, "بررسی عکس/سند ممکن نشد: " + ex.Message +
                                          "\nهمگام‌سازیِ متن بدون تغییر ادامه می‌یابد.");
            }
            finally { SetBusy(false, null); }
        }

        // خلاصه‌ی رسانه‌ها در صفحه‌ی تأیید — دقیقاً پیش از نوشتن.
        private string BuildMediaConfirmText()
        {
            if (_mediaPlan == null || !_mediaPlan.HasAnything) return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("── عکس و سند ─────────────────────────");
            sb.AppendLine("• عکسِ جدید: " + _mediaPlan.PhotosToAdd +
                          "   |   جایگزینی (تأییدشده): " + _mediaPlan.Photos.Count(p => p.Selected && p.Action == MediaAction.Replace));
            sb.AppendLine("• سندِ جدید: " + _mediaPlan.DocsToAdd +
                          "   |   جایگزینی (تأییدشده): " + _mediaPlan.Documents.Count(d => d.Selected && d.Action == MediaAction.Replace));

            if (_mediaPlan.PhotosNoMatch > 0)
                sb.AppendLine("• عکس با کدِ ناموجود: " + _mediaPlan.PhotosNoMatch + " (وارد نمی‌شود)");
            if (_mediaPlan.PhotosDuplicate > 0)
                sb.AppendLine("• عکسِ تکراری: " + _mediaPlan.PhotosDuplicate + " (هیچ‌کدام وارد نمی‌شود)");
            if (_mediaPlan.PhotosUnsupported + _mediaPlan.DocsUnsupported > 0)
                sb.AppendLine("• فرمتِ پشتیبانی‌نشده: " + (_mediaPlan.PhotosUnsupported + _mediaPlan.DocsUnsupported));
            if (_mediaPlan.PhotosCorrupt > 0)
                sb.AppendLine("• فایلِ خراب: " + _mediaPlan.PhotosCorrupt + " (وارد نمی‌شود)");
            if (_mediaPlan.PhotosOtherCenter + _mediaPlan.DocsOtherCenter > 0)
                sb.AppendLine("• متعلق به مرکز دیگر: " + (_mediaPlan.PhotosOtherCenter + _mediaPlan.DocsOtherCenter));
            if (_mediaPlan.CasesMissingPhoto.Count > 0)
                sb.AppendLine("• پرونده‌های بدون عکس: " + _mediaPlan.CasesMissingPhoto.Count + " (فقط گزارش می‌شود)");

            sb.AppendLine();
            return sb.ToString();
        }

        private async Task RunSync()
        {
            // رعایت قوانین کاربران: کاربر «فقط مشاهده» (Viewer) اجازه‌ی نوشتن ندارد.
            if (!SecurityContext.CanEdit())
            {
                UiTheme.ShowWarning(this, "کاربر فقط‌مشاهده اجازه‌ی همگام‌سازی (ثبت/ویرایش) ندارد.");
                ShowStep(5);
                return;
            }

            SetBusy(true, "در حال همگام‌سازی...");
            _btnNext.Enabled = false; _btnBack.Enabled = false; _btnCancel.Enabled = false;

            var engine = new SyncEngine(new DatabaseHelper());
            var options = new SyncOptions
            {
                TakeBackup = _takeBackup,
                BackupParentFolder = _backupParentFolder   // خالی = مسیرِ پیش‌فرض
            };
            var progress = new Progress<SyncProgress>(UpdateProgress);
            try
            {
                _report = await Task.Run(() => engine.Apply(_plan, options, progress));
            }
            catch (Exception ex)
            {
                _report = new SyncReport { Success = false, ErrorMessage = ex.Message };
                _report.Add("خطای غیرمنتظره: " + ex.Message);
            }
            finally { SetBusy(false, null); }

            // ── رسانه‌ها بعد از همگام‌سازیِ متن اعمال می‌شوند ──
            // آموزش — ترتیب عمدی است: پرونده‌های جدید باید اول ساخته شوند تا
            // عکس/سندشان کدِ متناظر را در دیتابیس پیدا کند. اگر همگام‌سازیِ متن
            // شکست خورده باشد، رسانه‌ها اصلاً اجرا نمی‌شوند.
            // آموزش — رفع باگ «عکس‌ها هرگز وارد نمی‌شدند»: شرط قبلی
            // _mediaPlan.TotalApplicable > 0 بود، ولی _mediaPlan نتیجه‌ی پویشِ
            // *پیش از* همگام‌سازی است؛ در آن لحظه پرونده‌های جدید هنوز در
            // دیتابیس نیستند، پس همه‌ی عکس‌هایشان NoMatch (غیرقابل‌اعمال) بود و
            // TotalApplicable صفر می‌شد. نتیجه: این بلاک رد می‌شد و پویشِ مجددی
            // که دقیقاً برای وصل‌کردنِ همان عکس‌ها داخلش نوشته شده هرگز اجرا
            // نمی‌شد. حالا شرطِ ورود «فایلی برای بررسی هست» است و تصمیمِ واقعی
            // پس از پویشِ مجدد و روی refreshed گرفته می‌شود (پایین‌تر).
            if (_report != null && _report.Success && _mediaPlan != null && _mediaPlan.HasAnything)
            {
                SetBusy(true, "در حال ورود عکس‌ها و اسناد...");
                // همان منبعِ لغوی که دکمه‌ی «لغو» می‌بیند (توضیح بالای _activeCancel).
                _mediaCancel = new System.Threading.CancellationTokenSource();
                _activeCancel = _mediaCancel;
                _btnCancel.Enabled = true;
                _btnCancel.Text = "لغو";

                try
                {
                    // پس از ثبتِ پرونده‌های جدید، کدها تازه در دیتابیس‌اند؛ پس
                    // دوباره پویش می‌کنیم تا عکس‌های پرونده‌های تازه هم وصل شوند.
                    var scanner = new MediaScanner(new DatabaseHelper());
                    string folder = _source.PackageFolder;
                    var rescanProgress = new Progress<SyncProgress>(UpdateProgress);
                    MediaPlan refreshed = await Task.Run(() => scanner.Scan(folder, rescanProgress));

                    CarrySelections(_mediaPlan, refreshed);

                    // گزارشِ پایانی باید وضعیتِ *پس از* ثبتِ پرونده‌ها را نشان
                    // دهد، وگرنه عکس‌هایی که همین حالا وصل شدند همچنان در فهرستِ
                    // «کدشان در دیتابیس نبود» دیده می‌شوند (پویشِ اول کهنه است).
                    _mediaPlan = refreshed;

                    if (refreshed.TotalApplicable > 0)
                    {
                        var mediaEngine = new MediaSyncEngine(new DatabaseHelper());
                        var mediaProgress = new Progress<SyncProgress>(UpdateProgress);
                        System.Threading.CancellationToken token = _mediaCancel.Token;

                        _mediaReport = await Task.Run(() => mediaEngine.Apply(refreshed, mediaProgress, token));
                    }
                }
                catch (Exception ex)
                {
                    _mediaReport = new MediaReport { Success = false, ErrorMessage = ex.Message };
                    _mediaReport.Add("خطای غیرمنتظره در ورود رسانه: " + ex.Message);
                }
                finally
                {
                    SetBusy(false, null);
                    _btnCancel.Text = "انصراف";
                    _btnCancel.Enabled = true;
                    _activeCancel = null;
                    if (_mediaCancel != null) { _mediaCancel.Dispose(); _mediaCancel = null; }
                }
            }

            // نشانه‌ی این بسته ذخیره می‌شود تا دفعه‌ی بعد هشدارِ تکراری داده شود.
            if (_report != null && _report.Success) RememberImportedPackage();

            ShowStep(7);
            RenderReport();
        }

        // انتخاب‌های کاربر (مثلاً تأییدِ جایگزینی) از پویشِ اول به پویشِ دوم
        // منتقل می‌شود تا تصمیمش از بین نرود.
        private static void CarrySelections(MediaPlan from, MediaPlan to)
        {
            if (from == null || to == null) return;

            var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (MediaItem i in from.Photos) map["P|" + i.SourcePath] = i.Selected;
            foreach (MediaItem i in from.Documents) map["D|" + i.SourcePath] = i.Selected;

            foreach (MediaItem i in to.Photos)
            {
                bool sel;
                if (map.TryGetValue("P|" + i.SourcePath, out sel)) i.Selected = sel;
            }
            foreach (MediaItem i in to.Documents)
            {
                bool sel;
                if (map.TryGetValue("D|" + i.SourcePath, out sel)) i.Selected = sel;
            }
        }

        // گزارشِ کاملِ عکس و سند — همه‌ی اقلامِ درخواستی، حتی وقتی صفرند، تا
        // کاربر بداند چه چیزی بررسی شده و چیزی از قلم نیفتاده.
        private void AppendMediaReport(System.Text.StringBuilder sb)
        {
            if (_mediaPlan == null && _mediaReport == null) return;

            sb.AppendLine();
            sb.AppendLine("══ گزارش عکس‌ها و اسناد ══");

            if (_mediaReport != null)
            {
                sb.AppendLine(_mediaReport.Success
                    ? "✓ ورود عکس و سند انجام شد."
                    : (_mediaReport.RolledBack
                        ? "✗ خطا — فایل‌های نیمه‌کاره پاک و چیزی ثبت نشد."
                        : "✗ ورود عکس و سند ناموفق."));

                // آموزش — شفاف‌سازیِ حالتِ دوگانه: همگام‌سازیِ متن و ورودِ رسانه
                // دو عملیاتِ جدا هستند. اگر متن موفق باشد ولی رسانه لغو/خطا
                // بخورد، کاربر باید دقیقاً بداند چه چیزی ثبت شد و چه چیزی نه —
                // وگرنه گمان می‌کند کلِ کار برگشته است.
                if (!_mediaReport.Success && _report != null && _report.Success)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠ توجه — وضعیتِ دوگانه:");
                    sb.AppendLine("   • اطلاعاتِ متنی (سرپرستان و اعضا) با موفقیت ثبت شد و سرِ جایش است.");
                    sb.AppendLine("   • فقط ورودِ عکس و سند ناتمام ماند؛ فایل‌های نیمه‌کاره پاک شدند.");
                    sb.AppendLine("   • برای تکمیل، کافی است همین بسته را دوباره همگام‌سازی کنید:");
                    sb.AppendLine("     رکوردهای متنی «بدون تغییر» تشخیص داده می‌شوند و دوباره نوشته نمی‌شوند،");
                    sb.AppendLine("     و فقط عکس‌ها و اسنادِ باقی‌مانده وارد خواهند شد.");
                }
                sb.AppendLine();
                sb.AppendLine("عکسِ واردشده        : " + _mediaReport.PhotosImported);
                sb.AppendLine("عکسِ جایگزین‌شده    : " + _mediaReport.PhotosReplaced);
                sb.AppendLine("سندِ واردشده        : " + _mediaReport.DocumentsImported);
                sb.AppendLine("سندِ جایگزین‌شده    : " + _mediaReport.DocumentsReplaced);
                sb.AppendLine("عکسِ چرخانده‌شده    : " + _mediaReport.RotatedImages);
                sb.AppendLine("عکسِ فشرده‌شده      : " + _mediaReport.CompressedImages);
                sb.AppendLine("تصویرِ بزرگ         : " + _mediaReport.LargeImages);
                sb.AppendLine("فایلِ تکراری        : " + _mediaReport.DuplicateFiles);
                sb.AppendLine("فرمتِ پشتیبانی‌نشده : " + _mediaReport.UnsupportedFiles);
                sb.AppendLine("نامِ نامعتبر         : " + _mediaReport.InvalidFileNames);
                sb.AppendLine("پرونده‌ی بدون عکس   : " + _mediaReport.MissingPhotos);
                sb.AppendLine("خطا                 : " + _mediaReport.Errors);
                sb.AppendLine("مدت                 : " + _mediaReport.Duration.TotalSeconds.ToString("0.0") + " ثانیه");

                if (!string.IsNullOrWhiteSpace(_mediaReport.ErrorMessage))
                    sb.AppendLine("پیام                : " + _mediaReport.ErrorMessage);

                foreach (string line in _mediaReport.Log) sb.AppendLine(line);
            }
            else if (_mediaPlan != null)
            {
                sb.AppendLine("(چیزی برای ورود انتخاب نشده بود.)");
            }

            // فهرستِ مواردی که وارد نشدند — تا کاربر بتواند اصلاح کند.
            if (_mediaPlan != null)
            {
                AppendProblemList(sb, "عکس‌هایی که کدشان در دیتابیس نبود",
                    _mediaPlan.Photos.Where(p => p.Action == MediaAction.NoMatch));
                AppendProblemList(sb, "عکس‌های تکراری (باید یکی بماند)",
                    _mediaPlan.Photos.Where(p => p.Action == MediaAction.Duplicate));
                AppendProblemList(sb, "فایل‌های خراب",
                    _mediaPlan.Photos.Where(p => p.Action == MediaAction.Corrupt));
                AppendProblemList(sb, "فرمت‌های پشتیبانی‌نشده",
                    _mediaPlan.Photos.Where(p => p.Action == MediaAction.Unsupported)
                        .Concat(_mediaPlan.Documents.Where(d => d.Action == MediaAction.Unsupported)));
                AppendProblemList(sb, "متعلق به مرکز دیگر",
                    _mediaPlan.Photos.Where(p => p.Action == MediaAction.OtherCenter)
                        .Concat(_mediaPlan.Documents.Where(d => d.Action == MediaAction.OtherCenter)));

                if (_mediaPlan.CasesMissingPhoto.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("── پرونده‌های بدون عکس (" + _mediaPlan.CasesMissingPhoto.Count + ") ──");
                    int shown = Math.Min(50, _mediaPlan.CasesMissingPhoto.Count);
                    for (int i = 0; i < shown; i++) sb.AppendLine("   کد " + _mediaPlan.CasesMissingPhoto[i]);
                    if (_mediaPlan.CasesMissingPhoto.Count > shown)
                        sb.AppendLine("   ... و " + (_mediaPlan.CasesMissingPhoto.Count - shown) + " مورد دیگر");
                }
            }
        }

        private static void AppendProblemList(System.Text.StringBuilder sb, string title,
                                              IEnumerable<MediaItem> items)
        {
            List<MediaItem> list = items.ToList();
            if (list.Count == 0) return;

            sb.AppendLine();
            sb.AppendLine("── " + title + " (" + list.Count + ") ──");
            int shown = Math.Min(30, list.Count);
            for (int i = 0; i < shown; i++)
                sb.AppendLine("   " + list[i].FileName + "   " + (list[i].Message ?? ""));
            if (list.Count > shown)
                sb.AppendLine("   ... و " + (list.Count - shown) + " مورد دیگر");
        }

        private void RenderReport()
        {
            var r = _report;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(r.Success ? "✓ همگام‌سازی با موفقیت انجام شد." :
                          (r.RolledBack ? "✗ خطا رخ داد — همه‌ی تغییرات برگردانده شد (Rollback)." : "✗ همگام‌سازی ناموفق."));
            sb.AppendLine();
            sb.AppendLine("سرپرستان جدید      : " + r.GuardiansInserted);
            sb.AppendLine("سرپرستان بروزرسانی : " + r.GuardiansUpdated);
            sb.AppendLine("اعضای جدید         : " + r.MembersInserted);
            sb.AppendLine("اعضای بروزرسانی    : " + r.MembersUpdated);
            sb.AppendLine("رد شده             : " + r.Skipped);
            sb.AppendLine("خطا                : " + r.Errors);
            sb.AppendLine("مدت                : " + r.Duration.TotalSeconds.ToString("0.0") + " ثانیه");
            if (!string.IsNullOrWhiteSpace(r.BackupPath))
                sb.AppendLine("بکاپ               : " + r.BackupPath);
            if (!string.IsNullOrWhiteSpace(r.ErrorMessage))
                sb.AppendLine("پیام خطا           : " + r.ErrorMessage);
            AppendMediaReport(sb);

            sb.AppendLine();
            sb.AppendLine("── رویدادنگار ──");
            foreach (var line in r.Log) sb.AppendLine(line);

            _txtReport.Text = sb.ToString();
            _btnNext.Enabled = true; _btnCancel.Enabled = true;
        }

        // ═══ کمکی‌های UI ═════════════════════════════════════════════════════
        private void UpdateProgress(SyncProgress sp)
        {
            _progress.Value = Math.Max(0, Math.Min(100, sp.Percent));
            _lblProgress.Text = sp.Phase + " — " + sp.Current + " / " + sp.Total;
        }

        private void SetBusy(bool busy, string phase)
        {
            _progress.Visible = busy;
            _lblProgress.Visible = busy;
            if (busy) { _progress.Value = 0; _lblProgress.Text = phase ?? ""; }
            _btnNext.Enabled = !busy;
            _btnBack.Enabled = !busy && _step > 0;
            Application.DoEvents();
        }

        private Label AddHint(Panel p, string text, int y)
        {
            Label l = new Label
            {
                Text = text, Location = new Point(20, 12 + y), Size = new Size(860, 48),
                Font = UiTheme.Font(UiTheme.SizeBody), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.TopLeft
            };
            p.Controls.Add(l);
            return l;
        }

        private Label AddBigLabel(Panel p, int y)
        {
            Label l = new Label
            {
                Location = new Point(20, y), Size = new Size(860, 360),
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.TopLeft
            };
            p.Controls.Add(l);
            return l;
        }

        // ═══ راست‌چینیِ متنِ برچسب‌ها در فرمِ آینه‌شده ═══════════════════════
        // آموزش — باگی که در آزمونِ تصویری پیدا شد: همه‌ی برچسب‌های این فرم
        // TextAlign = MiddleRight/TopRight داشتند و همگی روی صفحه *چپ‌چین*
        // دیده می‌شدند — از عنوانِ مرحله در سربرگ تا برچسبِ هر انتخابگر.
        //
        // دلیل: این فرم RightToLeftLayout = true دارد، یعنی ویندوز کلِ ناحیه‌ی
        // کاری را آینه می‌کند. در فضای آینه‌شده، «راست» همان چیزی است که
        // کاربر «چپ» می‌بیند. پس برای اینکه متن در لبه‌ی راستِ صفحه بنشیند
        // باید Left نوشت، نه Right. همین قاعده از قبل در همین فایل مستند شده
        // بود (کنارِ عنوانِ کادرِ راهنما) ولی در بقیه‌ی برچسب‌ها رعایت نشده بود.
        //
        // پس همه‌ی MiddleRight/TopRight به MiddleLeft/TopLeft تغییر کرد. هیچ
        // متنی عوض نشد؛ فقط جایی که دیده می‌شود درست شد.

        // ═══ چیدمانِ راست‌به‌چپِ انتخابگرها ═══════════════════════════════════
        // آموزش — چرا مختصات دستی اصلاح شد: RightToLeftLayout مختصاتِ Location
        // کنترل‌های معمولی را آینه نمی‌کند (فقط Dock/Anchor و متن را). بنابراین
        // دکمه‌ای که در X=۲۰ گذاشته شود، در فرمِ راست‌به‌چپ هم سمتِ چپ می‌افتد.
        // چون خواندن از راست شروع می‌شود، دکمه‌ی «انتخاب» باید در لبه‌ی راست
        // باشد و کادرِ مسیر سمتِ چپش. این ثابت‌ها همان ناحیه‌ی محتوای صفحه‌اند
        // (۲۰ تا ۸۸۰) که برچسب‌ها هم از آن استفاده می‌کنند.
        private const int PickerAreaLeft = 20;
        private const int PickerAreaRight = 880;
        private const int PickerButtonWidth = 130;

        private TextBox AddFolderPicker(Panel p, string label, int y, Action<string> onPick)
        {
            Label lbl = new Label
            {
                Text = label, Location = new Point(PickerAreaLeft, y),
                Size = new Size(PickerAreaRight - PickerAreaLeft, 22),
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleLeft
            };
            p.Controls.Add(lbl);

            int buttonX = PickerAreaRight - PickerButtonWidth;      // لبه‌ی راست
            int boxWidth = buttonX - PickerAreaLeft - 10;

            TextBox txt = new TextBox
            {
                Location = new Point(PickerAreaLeft, y + 26), Size = new Size(boxWidth, 26),
                ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.No
            };
            p.Controls.Add(txt);

            Button btn = UiTheme.CreateButton("انتخاب پوشه...", "▤", UiTheme.Primary);
            btn.SetBounds(buttonX, y + 25, PickerButtonWidth, 28);
            btn.Click += delegate
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = label;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txt.Text = fbd.SelectedPath;
                        onPick(fbd.SelectedPath);
                    }
                }
            };
            p.Controls.Add(btn);

            // ── دکمه‌ی پاک کردن مسیر ──
            // آموزش — پیش از این، مسیرِ انتخاب‌شده هیچ راهی برای خالی شدن نداشت
            // و کاربر مجبور بود برنامه را ببندد. این دکمه مسیر و همه‌ی وابسته‌های
            // آن (نتیجه‌ی بررسی، فهرست کشف‌شده‌ها، برنامه‌ی رسانه) را صفر می‌کند
            // تا از نو شروع شود.
            AddClearButton(p, buttonX - 34, y + 25, "پاک کردن مسیرِ پوشه", delegate
            {
                txt.Text = "";
                onPick("");
                ResetPackageState();
            });

            return txt;
        }

        // دکمه‌ی کوچکِ «✕» برای خالی‌کردنِ یک انتخابگر.
        private void AddClearButton(Panel p, int x, int y, string tooltip, Action onClear)
        {
            Button clear = new Button
            {
                Text = "✕",
                Font = UiTheme.FontBold(UiTheme.SizeSmall),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = UiTheme.Danger,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop = false
            };
            clear.SetBounds(x, y, 28, 28);
            clear.FlatAppearance.BorderSize = 1;
            clear.FlatAppearance.BorderColor = UiTheme.Border;
            clear.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xFD, 0xEC, 0xEA);
            clear.Click += delegate { onClear(); };
            new ToolTip().SetToolTip(clear, tooltip);
            p.Controls.Add(clear);
        }

        // ─── صفرکردنِ وضعیتِ بسته ─────────────────────────────────────────────
        // با پاک شدنِ هر مسیر، نتیجه‌ی بررسیِ قبلی دیگر معتبر نیست؛ اگر نگه
        // داشته شود کاربر می‌تواند با تأییدِ کهنه از دروازه عبور کند.
        private void ResetPackageState()
        {
            _validation = null;
            _warningsAccepted = false;
            _mediaPlan = null;
            _mediaReport = null;

            if (_lblPackageFound != null) _lblPackageFound.Text = "";
            UpdateValidationState();
        }

        // ─── انتخابگرِ نصف‌عرض (دو تا کنارِ هم در یک ردیف) ───────────────────
        // rightHalf=true یعنی نیمه‌ی راست، که در خواندنِ راست‌به‌چپ «اولی» است.
        // دکمه‌ی انتخاب در لبه‌ی راستِ همان نیمه می‌نشیند، مطابق قاعده‌ی
        // مستندشده‌ی این فرم (توضیح بالای AddFolderPicker).
        private TextBox AddFilePickerHalf(Panel p, string label, int y, bool rightHalf,
                                          Action<string> onPick)
        {
            const int Gap = 16;
            int halfWidth = (PickerAreaRight - PickerAreaLeft - Gap) / 2;
            int left = rightHalf ? PickerAreaRight - halfWidth : PickerAreaLeft;

            Label lbl = new Label
            {
                Text = label, Location = new Point(left, y), Size = new Size(halfWidth, 22),
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleLeft
            };
            p.Controls.Add(lbl);

            int buttonWidth = 104;
            int buttonX = left + halfWidth - buttonWidth;
            // ۳۴ پیکسل برای دکمه‌ی پاک‌کردن کنار گذاشته می‌شود.
            int boxWidth = halfWidth - buttonWidth - 8 - 34;

            TextBox txt = new TextBox
            {
                Location = new Point(left, y + 26), Size = new Size(boxWidth, 26),
                ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.No
            };
            p.Controls.Add(txt);

            Button btn = UiTheme.CreateButton("انتخاب...", "▤", UiTheme.Primary);
            btn.SetBounds(buttonX, y + 25, buttonWidth, 28);
            btn.Click += delegate
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = label;
                    ofd.Filter = "فایل HTML|*.html;*.htm|همه فایل‌ها|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        txt.Text = ofd.FileName;
                        onPick(ofd.FileName);
                    }
                }
            };
            p.Controls.Add(btn);

            AddClearButton(p, buttonX - 34, y + 25, "پاک کردن مسیرِ فایل", delegate
            {
                txt.Text = "";
                onPick("");
                ResetPackageState();
            });

            return txt;
        }

        private TextBox AddFilePicker(Panel p, string label, int y, Action<string> onPick)
        {
            Label lbl = new Label
            {
                Text = label, Location = new Point(PickerAreaLeft, y),
                Size = new Size(PickerAreaRight - PickerAreaLeft, 22),
                Font = UiTheme.FontBold(UiTheme.SizeSmall), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleLeft
            };
            p.Controls.Add(lbl);

            // دکمه در لبه‌ی راست، کادرِ مسیر سمتِ چپِ آن (توضیح بالای AddFolderPicker).
            int buttonX = PickerAreaRight - PickerButtonWidth;
            int boxWidth = buttonX - PickerAreaLeft - 10;

            TextBox txt = new TextBox
            {
                Location = new Point(PickerAreaLeft, y + 26), Size = new Size(boxWidth, 26),
                ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.No
            };
            p.Controls.Add(txt);

            Button btn = UiTheme.CreateButton("انتخاب...", "▤", UiTheme.Primary);
            btn.SetBounds(buttonX, y + 25, PickerButtonWidth, 28);
            btn.Click += delegate
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = label;
                    ofd.Filter = "فایل HTML|*.html;*.htm|همه فایل‌ها|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        txt.Text = ofd.FileName;
                        onPick(ofd.FileName);
                    }
                }
            };
            p.Controls.Add(btn);
            return txt;
        }
    }
}

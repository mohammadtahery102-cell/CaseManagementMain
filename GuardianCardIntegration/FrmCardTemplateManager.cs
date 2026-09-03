using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.GuardianCardIntegration.CardDesigner;
using CaseManagement.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // مدیریت قالب‌های کارت («CARD TEMPLATE MANAGEMENT» + «Card Designer سبک») —
    // فقط مدیر. به‌خاطرِ محدودیتِ معماریِ طرحِ ثابتِ HTML/CSS (نگاه کنید آموزشِ
    // TblCardTemplate در DatabaseInitializer)، «قالب» یعنی:
    //   ۱) نوعِ طرح (کامل/ساده) + کدام فیلدهای اختیاری روشن باشند.
    //   ۲) رنگ/فونت/پس‌زمینهٔ هر رو/واترمارک/هولوگرام/QR/بارکد — نگاه کنید
    //      CardTemplateDesign و GuardianCardRenderer.ApplyDesignOverrides.
    //   ۳) کدام ماه‌ها در جدولِ پرداختِ برگهٔ دوم باشند.
    // این هنوز طراحِ کاملِ drag-and-drop نیست — عمداً، طبقِ توافق: دستگیرهٔ
    // ⋮⋮ در چک‌لیستِ فیلدها فقط تزئینی‌ست (فقط ترتیبِ نمایش در همان لیست، نه
    // جابه‌جاییِ واقعیِ موقعیتِ فیلد روی کارتِ چاپی — چون index.html/simple.html
    // طرحِ HTML/CSSِ ثابت با موقعیتِ ازپیش‌تعیین‌شده برای هر فیلد دارند).
    //
    // آموزش — بازطراحیِ کاملِ ظاهری (نوبتِ دوم، به‌درخواستِ کاربر: «محیطِ
    // حرفه‌ای/مدرن/سازمانی شبیهِ ERP»): سه‌ستونه — راست=لیستِ قالب‌ها،
    // وسط=تب‌بندیِ تنظیمات (اطلاعات/فیلدها/ظاهر/QR/لوگو/چاپ/متن‌ها)، چپ=
    // پیش‌نمایشِ زندهِ واقعی با WebView2 (نه mockupِ گرافیکیِ قبلی). فقط
    // لایهٔ *ساختِ ظاهر* اینجا عوض شده؛ همهٔ متدهای داده/منطق (Collect/Apply/
    // Save/Delete/Export/Import/…) پایین‌ترِ همین فایل دست‌نخورده مانده‌اند.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmCardTemplateManager : Form
    {
        private readonly CardTemplateRepository _repo = new CardTemplateRepository();
        private readonly DatabaseHelper _db = new DatabaseHelper();

        private ListBox _lstTemplates;
        private TextBox _txtSearchTemplates;
        private TextBox _txtName;
        private ComboBox _cmbLayoutVariant;
        private CheckedListBox _chkFields;
        private CheckedListBox _chkMonths;
        private Label _lblDefaultTag;

        // ─── مدیریتِ حرفه‌ایِ قالب (Phase 2) ──────────────────────────────────
        private ComboBox _cmbTemplateType;
        private TextBox _txtDescription;
        private CheckBox _chkIsActive;
        private bool _suppressActiveToggle;
        private Label _lblMetaInfo;
        // آموزش — تاریخچهٔ نسخه‌ها: فهرست + دکمه‌های بازگردانی/مقایسه.
        private ListBox _lstVersions;
        private Button _btnRestoreVersion;
        private Button _btnCompareVersions;
        private List<CardTemplateVersion> _currentVersions = new List<CardTemplateVersion>();

        // ─── کنترل‌های طراح ────────────────────────────────────────────────
        private Panel _pnlPrimaryColor;
        private Panel _pnlSecondaryColor;
        private Panel _pnlBackgroundColor;
        private Panel _pnlTextColor;
        private NumericUpDown _numFontScale;
        private ComboBox _cmbFont;
        private TextBox _txtBgFront;
        private TextBox _txtBgBack;
        private TextBox _txtWatermark;
        private NumericUpDown _numWatermarkOpacity;
        private CheckBox _chkHologram;
        private CheckBox _chkQRCode;
        private CheckBox _chkBarcode;

        // آموزش — به‌درخواستِ کاربر: رنگِ پس‌زمینهٔ فقط نوارِ هدر، و
        // اندازه/خالی‌بودنِ عکسِ گردِ بالا-راست — نگاه کنید
        // CardTemplateDesign.HeaderBackgroundColor/PortraitScalePercent/PortraitBlank.
        private Panel _pnlHeaderBgColor;
        private NumericUpDown _numPortraitScale;
        private CheckBox _chkPortraitBlank;
        private NumericUpDown _numHeaderHeightScale;
        private ComboBox _cmbFamilyPhotoRatio;
        private NumericUpDown _numFamilyPhotoScale;
        private CheckBox _chkFamilyPhotoFitContain;
        private NumericUpDown _numFamilyListMaxRows;

        // آموزش — ویرایشگرِ عمومیِ «متن‌های قابلِ‌ویرایش» (بسمه‌تعالی/موتو/
        // تیتر/پیام‌های پایینِ کارت/آدرس/تماس/وبسایت/ایمیل): به‌جای یک
        // ردیفِ جداگانه برای هر فیلد، یک کمبوی انتخابِ فیلد + سه کنترلِ
        // محتوا/رنگ/سایز که با تعویضِ فیلد، مقدارِ فیلدِ قبلی را در
        // _textOverrides ذخیره و مقدارِ فیلدِ جدید را بارگذاری می‌کند.
        private ComboBox _cmbTextOverrideField;
        private TextBox _txtTextOverrideContent;
        private Panel _pnlTextOverrideColor;
        private NumericUpDown _numTextOverrideScale;
        private ComboBox _cmbTextOverrideFont;
        private NumericUpDown _numTextOverrideLineHeight;
        // آموزش — Card Designer Phase 1: تراز و وزنِ فونتِ همین فیلدِ
        // انتخاب‌شده — دقیقاً هم‌الگویِ چهار کنترلِ بالا.
        private ComboBox _cmbTextOverrideAlignment;
        private ComboBox _cmbTextOverrideWeight;
        private Dictionary<string, TextFieldOverride> _textOverrides = new Dictionary<string, TextFieldOverride>();
        private string _currentTextOverrideKey = "";

        // آموزش — Card Designer Phase 1: «ترتیبِ واقعیِ فیلدها». _lstFieldOrder
        // فیلدهای متنیِ پنلِ سرپرست (یا در قالبِ ساده، ۳ فیلدِ معادل) را
        // نگه می‌دارد؛ _lstBandOrder نوارِ امنیتیِ پایینِ کارتِ کامل (QR/
        // بارکد/امضا/مهر/هولوگرام) را — فقط برایِ قالبِ کامل، چون قالبِ
        // ساده نوارِ امنیتی ندارد. هر دو با کشیدن-و-رهاکردن (WireDragReorder)
        // قابلِ‌ترتیب‌دهی‌اند. موقعیتِ عکس یک سوییچِ سادهٔ دوحالته است، نه
        // یک لیست — نگاه کنید توضیحِ محدودۀ مصوب در
        // GuardianCardRenderer.ApplyFieldOrderOverrides.
        private ListBox _lstFieldOrder;
        private ListBox _lstBandOrder;
        private Label _lblBandOrderTitle;
        private RadioButton _radPhotoBefore;
        private RadioButton _radPhotoAfter;

        // یک آیتمِ نمایشیِ لیستِ ترتیب — کلیدِ واقعی (برایِ ذخیره) + برچسبِ
        // فارسی (برایِ نمایش، از طریقِ override شدنِ ToString).
        private class OrderItem
        {
            public string Key;
            public string Label;
            public override string ToString() { return Label; }
        }

        private static readonly Dictionary<string, string> SecurityBandFieldLabels = new Dictionary<string, string>
        {
            { "QRCode", "QR Code" }, { "Barcode", "بارکد" }, { "Signature", "امضا" },
            { "Stamp", "مهر" }, { "Hologram", "هولوگرام" }
        };

        private static readonly string[] TextOverrideFieldKeys =
        {
            "OrganizationName", "Besmellah", "MottoArabic", "MottoTranslation", "Kicker",
            "Address", "Phone", "Website", "Email", "ComplaintMessage", "FoundCardMessage",
            // آموزش — به‌درخواستِ کاربر: مشخصاتِ سرپرست/مددجو و تذکراتِ ۱ تا
            // ۵ هم به دیکشنریِ عمومیِ محتوا/رنگ/فونت/سایز/فاصلهٔ‌خط اضافه شدند.
            "GuardianName", "FatherName", "NationalID", "RequestType", "PublicCode",
            "Notice1", "Notice2", "Notice3", "Notice4", "Notice5"
        };
        // آموزش — این فیلدها، مکانیزمِ محتوایِ دیگری دارند (تنظیماتِ
        // سراسری/روشن‌خاموش)؛ اینجا فقط رنگ/سایزشان قابل‌تغییر است، نه
        // محتوا — نگاه کنید CardTemplateRepository.ApplyTextOverrides.
        // Phone/Email به‌درخواستِ صریحِ کاربر از این فهرست خارج شدند (محتوایشان
        // هم از همین‌جا قابل‌ویرایش است؛ نگاه کنید ApplyTextOverrides).
        private static readonly HashSet<string> TextOverrideContentLocked = new HashSet<string>
        {
            "Address", "Website",
            // آموزش — این‌ها همه از دیتابیس/تنظیماتِ دیگر می‌آیند (دادهٔ
            // پرونده یا CardNotice1-5)؛ اینجا فقط رنگ/فونت/سایز/فاصلهٔ‌خط
            // قابل‌تغییر است، نه محتوا — دقیقاً همان دلیلِ Address/Phone بالا.
            "GuardianName", "FatherName", "NationalID", "RequestType", "PublicCode",
            "Notice1", "Notice2", "Notice3", "Notice4", "Notice5"
        };
        private static readonly Dictionary<string, string> TextOverrideFieldLabels = new Dictionary<string, string>
        {
            { "OrganizationName", "تیترِ بزرگِ هدر (کارت هویت ایتام)" },
            { "Besmellah", "بسمه‌تعالی" },
            { "MottoArabic", "الله فی الایتام" },
            { "MottoTranslation", "ترجمه‌ی موتو" },
            { "Kicker", "نوارِ سبزِ پایینِ هدر" },
            { "Address", "آدرس دفتر (فقط رنگ/سایز)" },
            { "Phone", "شماره‌های تماس" },
            { "Website", "وبسایت (فقط رنگ/سایز)" },
            { "Email", "ایمیل" },
            { "ComplaintMessage", "پیامِ شکایت" },
            { "FoundCardMessage", "پیامِ پیداکردنِ کارت" },
            { "GuardianName", "نامِ سرپرست (فقط رنگ/سایز/فونت)" },
            { "FatherName", "نامِ پدر (فقط رنگ/سایز/فونت)" },
            { "NationalID", "شماره تذکره (فقط رنگ/سایز/فونت)" },
            { "RequestType", "نوعِ مددجو (فقط رنگ/سایز/فونت)" },
            { "PublicCode", "کدِ عمومی (فقط رنگ/سایز/فونت)" },
            { "Notice1", "تذکرِ ۱" },
            { "Notice2", "تذکرِ ۲" },
            { "Notice3", "تذکرِ ۳" },
            { "Notice4", "تذکرِ ۴" },
            { "Notice5", "تذکرِ ۵" }
        };

        // آموزش — پیش‌نمایشِ زندهٔ *واقعی*: همان WebView2ای که
        // FrmGuardianCardPreview استفاده می‌کند، اینجا embed شده و با هر
        // تغییرِ تنظیم (بعد از یک تأخیرِ کوتاه) خودش را دوباره می‌سازد — نه
        // یک mockupِ گرافیکیِ تقریبی.
        // آموزش — Card Designer Phase 1: میزبانِ تنظیمات (ستونِ کناری)،
        // نوارِ ناوبریِ بالا و پنج بخش. جایگزینِ TabControl قبلی.
        private Panel _settingsHost;
        private DesignerNav _nav;
        private DesignerSection[] _sections;
        // منبعِ دادهٔ پیش‌نمایش — نگاه کنید ResolvePreviewData.
        private int _previewCaseId;
        private bool _previewUseDemo;
        private Label _lblPreviewRecord;
        private Button _btnPickPreviewRecord;

        private Panel _healthHost;
        private readonly Dictionary<string, bool> _columnCache = new Dictionary<string, bool>();

        private Panel _fieldCardsHost;
        private TextBox _txtFieldSearch;
        private ComboBox _cmbFieldFilter;
        private readonly List<FieldCard> _fieldCards = new List<FieldCard>();
        private readonly Dictionary<string, Label> _fieldGroupHeaders = new Dictionary<string, Label>();
        private bool _syncingCards;

        private Panel _templateListHost;
        private SectionCard _templateListCard;
        private Button _btnCollapseList;
        private bool _templateListCollapsed;

        private WebView2 _webViewPreview;
        private System.Windows.Forms.Timer _previewTimer;
        private bool _previewReady;
        private Label _lblPreviewStatus;

        private static readonly Dictionary<string, string> FieldLabels = new Dictionary<string, string>
        {
            { "PublicCode", "کد عمومی" },
            { "Website", "وبسایت" },
            { "Email", "ایمیل" },
            { "IssuedBy", "نام صادرکننده" },
            { "Position", "سمت صادرکننده" },
            { "Logo", "لوگوی مؤسسه" },
            { "Signature", "امضا" },
            { "Stamp", "مهر" },
            { "CardCode", "شناسه کارت (بالا-چپ)" },
            { "Besmellah", "بسمه‌تعالی" },
            { "OrganizationName", "تیترِ بزرگِ هدر" },
            { "Portrait", "عکسِ گردِ تزئینیِ هدر" },
            { "BranchName", "خطِ ولایتِ زیرِ تیتر" },
            { "Address", "آدرسِ دفتر" },
            { "Phone", "شماره‌های تماس" },
            { "GuardianName", "نامِ سرپرست" },
            { "FatherName", "نامِ پدرِ سرپرست" },
            { "NationalID", "شماره تذکرهٔ سرپرست" },
            { "RequestType", "نوعِ مددجو" },
            { "FamilyList", "فهرستِ اعضای خانواده" },
            { "OrphansCount", "ایتام (شمارش/نیاز به تعیین نقش)" },
            { "FamilyPhoto", "عکسِ جمعیِ خانواده" },
            { "FamilyListPhotos", "عکسِ هر عضو (درونِ فهرستِ اعضا)" }
        };

        // آموزش — برچسبِ فارسیِ فیلدهای قالبِ «ساده» — نگاه کنید
        // CardTemplateRepository.ToggleableFieldsSimple.
        private static readonly Dictionary<string, string> FieldLabelsSimple = new Dictionary<string, string>
        {
            { "Photo", "عکس" },
            { "FamilyPhoto", "عکس جمعی" },
            { "PublicCode", "کد اختصاصی" },
            { "CaseNo", "پرونده" },
            { "Province", "ولایت" },
            { "District", "ولسوالی" },
            { "Phone", "شماره تماس" },
            { "NationalID", "شماره تذکره" },
            { "GuardianName", "نام سرپرست" },
            { "FatherName", "نام پدر سرپرست" },
            { "RelationshipToFamily", "نسبت سرپرست با اعضاء" },
            { "SimpleNotes", "تذکرات" },
            { "Thumbprint", "محل شصت" },
            { "IssueDate", "تاریخ صدور کارت" },
            { "Orphans", "نام‌های ایتام" }
        };

        private const string LayoutFull = "طرحِ کامل";
        private const string LayoutSimple = "طرحِ ساده";

        private static readonly string[] FontChoices = { "Vazirmatn", "Tahoma", "Segoe UI", "B Nazanin", "IRANSans" };

        private List<CardTemplate> _templates = new List<CardTemplate>();
        private int _currentTemplateId = 0;

        public FrmCardTemplateManager()
        {
            // دفاع لایه‌ای: حتی اگر این فرم مستقیم ساخته شود، بدون مجوز
            // «مدیریت قالب کارت» بلافاصله بسته می‌شود.
            if (!CaseManagement.Enterprise.PermissionService.Require("GuardianCard.ManageTemplates"))
            {
                Load += delegate { Close(); };
                return;
            }

            BuildUi();
            Load += delegate { LoadTemplateList(); InitPreviewAsync(); };
        }

        // ─────────────────────────────────────────────────────────────────
        // چیدمانِ فرم — سه‌ستونه: راست=لیستِ قالب‌ها، وسط=تب‌های تنظیمات،
        // چپ=پیش‌نمایشِ زندهٔ WebView2. هدف: بدونِ اسکرولِ کلِ صفحه در Full HD؛
        // فقط محتوای داخلِ هر تب (اگر لازم شد) اسکرول می‌شود.
        // ─────────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            Text = "مدیریت قالب‌ها و طراحِ کارت شناسایی";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1680, 950);
            MinimumSize = new Size(1200, 720);
            WindowState = FormWindowState.Maximized;

            // ─── سربرگِ بالا ────────────────────────────────────────────────
            Panel header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.CardBack };
            Panel headerBorder = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = UiTheme.Border };
            Label lblTitle = new Label
            {
                Text = "💳  مدیریت قالب‌ها و طراحی کارت شناسایی",
                Dock = DockStyle.Right, AutoSize = false, Width = 420,
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(16, 0, 0, 0)
            };
            Label lblBreadcrumb = new Label
            {
                Text = "خانه / تنظیمات / طراحی کارت شناسایی",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(9F), Padding = new Padding(16, 0, 16, 0)
            };
            header.Controls.Add(lblBreadcrumb);
            header.Controls.Add(lblTitle);
            header.Controls.Add(headerBorder);

            // ─── نوارِ پایین: فقط ۴ دکمهٔ اصلی (طبقِ درخواست) ──────────────────
            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = UiTheme.CardBack };
            Panel bottomBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiTheme.Border };

            FlowLayoutPanel mainButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 11, 16, 11), WrapContents = false
            };

            Button btnSave = UiTheme.CreateButton("ذخیرهٔ قالب", "💾", UiTheme.Success);
            btnSave.Size = new Size(130, 36); btnSave.Margin = new Padding(8, 0, 0, 0);
            btnSave.Click += delegate { SaveCurrent(); };

            Button btnPreview = UiTheme.CreateSecondaryButton("پیش‌نمایش", "👁");
            btnPreview.Size = new Size(118, 36); btnPreview.Margin = new Padding(8, 0, 0, 0);
            btnPreview.Click += delegate { PreviewCurrent(); };

            Button btnTestPrint = UiTheme.CreateSecondaryButton("چاپِ آزمایشی", "🖨");
            btnTestPrint.Size = new Size(126, 36); btnTestPrint.Margin = new Padding(8, 0, 0, 0);
            btnTestPrint.Click += delegate { TestPrintFromLivePreview(); };

            Button btnExport = UiTheme.CreateSecondaryButton("خروجی", "⇓");
            btnExport.Size = new Size(96, 36); btnExport.Margin = new Padding(8, 0, 0, 0);
            btnExport.Click += delegate { ExportCurrent(); };

            mainButtons.Controls.Add(btnSave);
            mainButtons.Controls.Add(btnPreview);
            mainButtons.Controls.Add(btnTestPrint);
            mainButtons.Controls.Add(btnExport);

            // آموزش — Import/حذف دکمه‌های اصلی نیستند (طبقِ درخواست فقط ۴ تا
            // بمانند)؛ اینجا به‌صورتِ لینکِ کوچک در گوشهٔ دیگرِ همان نوار.
            Button btnImport = UiTheme.CreateSecondaryButton("Import", "⇑");
            btnImport.Size = new Size(84, 28); btnImport.Margin = new Padding(0, 0, 0, 0);
            btnImport.Font = UiTheme.Font(8.5F);
            btnImport.Click += delegate { ImportTemplate(); };
            Panel importWrap = new Panel { Dock = DockStyle.Left, Width = 100, Padding = new Padding(16, 15, 0, 15) };
            importWrap.Controls.Add(btnImport);

            bottomBar.Controls.Add(mainButtons);
            bottomBar.Controls.Add(importWrap);
            bottomBar.Controls.Add(bottomBorder);

            // ─── راست: لیستِ قالب‌ها (طبقِ درخواست، این‌بار واقعاً سمتِ راست) ───
            Panel right = new Panel { Dock = DockStyle.Right, Width = 220, Padding = new Padding(0, 8, 8, 8), BackColor = UiTheme.Background };
            _templateListHost = right;
            SectionCard listCard = new SectionCard { Dock = DockStyle.Fill, Padding = new Padding(2, 2, 2, 2) };
            _templateListCard = listCard;

            // آموزش — Card Designer Phase 1: دکمهٔ جمع‌کردنِ فهرستِ قالب‌ها.
            // جمع‌شده که باشد، ۲۳۰ پیکسل به پیش‌نمایش اضافه می‌شود (≈۶۰٪ →
            // ≈۷۵٪ روی ۱۶۸۰ پیکسل) — همان «up to 72%» در سندِ تأییدشده.
            _btnCollapseList = UiTheme.CreateSecondaryButton("«", "");
            _btnCollapseList.Dock = DockStyle.Top;
            _btnCollapseList.Height = 26;
            UiTheme.SetTip(_btnCollapseList, "جمع‌کردن فهرست قالب‌ها");
            _btnCollapseList.Click += delegate { ToggleTemplateList(); };

            Panel listHeaderRow = new Panel { Dock = DockStyle.Top, Height = 40 };
            Label lblList = new Label
            {
                Text = "📁 قالب‌ها", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight, Font = UiTheme.FontBold(UiTheme.SizeMedium),
                ForeColor = UiTheme.TextDark, Padding = new Padding(12, 0, 0, 0)
            };
            Button btnAddTemplate = UiTheme.CreateButton("", "+", UiTheme.Success);
            btnAddTemplate.Dock = DockStyle.Left; btnAddTemplate.Width = 32; btnAddTemplate.Margin = new Padding(8, 4, 0, 4);
            UiTheme.SetTip(btnAddTemplate, "قالب جدید");
            btnAddTemplate.Click += delegate { StartNewTemplate(); };
            listHeaderRow.Controls.Add(lblList);
            listHeaderRow.Controls.Add(btnAddTemplate);

            _txtSearchTemplates = new TextBox { RightToLeft = RightToLeft.Yes, Font = UiTheme.Font(9.5F) };
            UiTheme.StyleTextBox(_txtSearchTemplates);
            Panel searchWrap = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 4, 8, 4) };
            _txtSearchTemplates.Dock = DockStyle.Fill;
            searchWrap.Controls.Add(_txtSearchTemplates);
            UiTheme.SetTip(_txtSearchTemplates, "جستجوی قالب...");
            _txtSearchTemplates.TextChanged += delegate { LoadTemplateList(_txtSearchTemplates.Text); };

            _lstTemplates = new ListBox
            {
                Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, BorderStyle = BorderStyle.None,
                Font = UiTheme.Font(10F), IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 46
            };
            _lstTemplates.DrawItem += LstTemplates_DrawItem;
            _lstTemplates.SelectedIndexChanged += delegate { LoadSelectedIntoEditor(); SchedulePreviewRefresh(); };

            listCard.Controls.Add(_lstTemplates);
            listCard.Controls.Add(searchWrap);
            listCard.Controls.Add(listHeaderRow);
            right.Controls.Add(listCard);
            right.Controls.Add(_btnCollapseList);

            // ─── چپ: پیش‌نمایشِ زندهٔ واقعی (WebView2)، بزرگ و همیشه دیده ────────
            Panel previewHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 8, 8, 8), BackColor = UiTheme.Background };
            SectionCard previewCard = new SectionCard { Dock = DockStyle.Fill, Padding = new Padding(2, 2, 2, 2) };

            Panel previewHeaderRow = new Panel { Dock = DockStyle.Top, Height = 38 };
            Label lblPreviewTitle = new Label
            {
                Text = "👁 پیش‌نمایشِ زنده", Dock = DockStyle.Right, Width = 150, TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.FontBold(UiTheme.SizeMedium), ForeColor = UiTheme.TextDark, Padding = new Padding(12, 0, 0, 0)
            };

            // آموزش — «انتخابِ پروندهٔ پیش‌نمایش»: تا پیش از این، طراح خودش
            // آخرین پرونده را برمی‌داشت و کاربر نمی‌توانست کارتِ مددجویِ
            // موردنظرش را ببیند. حالا انتخاب صریح است و وضعیتِ فعلی هم
            // (نامِ پرونده یا «دادهٔ نمونه») همان‌جا نوشته می‌شود.
            _btnPickPreviewRecord = UiTheme.CreateSecondaryButton("انتخاب پرونده", "");
            _btnPickPreviewRecord.Dock = DockStyle.Left;
            _btnPickPreviewRecord.Width = 130;
            _btnPickPreviewRecord.Margin = new Padding(8, 4, 8, 4);
            UiTheme.SetTip(_btnPickPreviewRecord, "انتخاب پروندهٔ پیش‌نمایش");
            _btnPickPreviewRecord.Click += delegate { PickPreviewRecord(); };

            _lblPreviewRecord = new Label
            {
                Text = "", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(9F), Padding = new Padding(8, 0, 8, 0)
            };

            previewHeaderRow.Controls.Add(_lblPreviewRecord);
            previewHeaderRow.Controls.Add(_btnPickPreviewRecord);
            previewHeaderRow.Controls.Add(lblPreviewTitle);

            _webViewPreview = new WebView2 { Dock = DockStyle.Fill };

            _lblPreviewStatus = new Label
            {
                Text = "برای پیش‌نمایشِ زنده، حداقل یک پرونده در سیستم لازم است.",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(9.5F), Visible = false
            };

            previewCard.Controls.Add(_lblPreviewStatus);
            previewCard.Controls.Add(_webViewPreview);
            previewCard.Controls.Add(previewHeaderRow);
            previewHost.Controls.Add(previewCard);

            // ─── وسط: تب‌بندیِ تنظیمات ──────────────────────────────────────────
            // آموزش — Card Designer Phase 1 («پیش‌نمایش در مرکز»): پیش‌نمایش
            // از یک ستونِ ثابتِ ۴۸۰ پیکسلی به Dock=Fill تبدیل شد و تنظیمات
            // به یک ستونِ کناریِ قابل‌کشیدن. نسبتِ پیش‌فرض روی ۱۶۸۰ پیکسل:
            // پیش‌نمایش ≈۶۰٪ (قبلاً ۲۹٪)، و با جمع‌کردنِ فهرستِ قالب‌ها ≈۷۵٪.
            // Splitter اجازه می‌دهد کاربر خودش نسبت را عوض کند.
            _settingsHost = new Panel { Dock = DockStyle.Left, Width = 440, Padding = new Padding(8), BackColor = UiTheme.Background };
            Splitter settingsSplitter = new Splitter
            {
                Dock = DockStyle.Left, Width = 6, BackColor = UiTheme.Background,
                MinSize = 340, MinExtra = 420
            };
            BuildSettingsTabs(_settingsHost);

            // ترتیبِ افزودن = از داخلی‌ترین به بیرونی‌ترین (آخرین افزوده،
            // بیرونی‌ترین جایگاه را می‌گیرد) — پس header بالای nav می‌نشیند.
            Controls.Add(previewHost);
            Controls.Add(settingsSplitter);
            Controls.Add(_settingsHost);
            Controls.Add(right);
            Controls.Add(bottomBar);
            Controls.Add(_nav);
            Controls.Add(header);

            _previewTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _previewTimer.Tick += delegate { _previewTimer.Stop(); RefreshHealth(); RefreshPreviewNowAsync(); };
        }

        // ─── تب‌های وسط: هر تب یک پنلِ AutoScroll مستقل — فقط همان تب اسکرول
        // می‌شود، نه کلِ صفحه ─────────────────────────────────────────────────
        // آموزش — Card Designer Phase 1 («ناوبریِ بالا + ادغامِ تب‌ها»):
        // ۹ تب به ۵ بخش تبدیل شد. نکتهٔ کلیدیِ کم‌ریسک‌بودنِ این تغییر:
        // هیچ‌کدام از متدهای BuildXSection دست نخورده‌اند — همان‌ها با همان
        // ترتیب صدا زده می‌شوند و همان فیلدهای کنترل را می‌سازند، فقط داخلِ
        // «گروه»هایی درونِ ۵ بخش، نه ۹ TabPage. بنابراین CollectDesign/
        // CollectFields/ApplyTemplateToEditor بدونِ هیچ تغییری کار می‌کنند.
        //
        // نگاشتِ تب‌های قبلی به بخش‌های تازه (طبقِ سندِ تأییدشده، بخشِ B2):
        //   کارت      ← اطلاعات کارت
        //   ظاهر      ← ظاهر کارت + QR/امنیت + لوگو و تصویر
        //   محتوا     ← فیلدهای قابل‌نمایش + ترتیبِ فیلدها + متن‌ها
        //   پشت کارت  ← جدولِ پرداخت
        //   تاریخچه   ← تاریخچهٔ نسخه‌ها
        private void BuildSettingsTabs(Panel host)
        {
            _nav = new DesignerNav();
            _sections = new DesignerSection[5];

            for (int i = 0; i < _sections.Length; i++)
            {
                _sections[i] = new DesignerSection();
                host.Controls.Add(_sections[i]);
            }

            // ۱ — کارت
            BuildInfoSection(_sections[0].AddGroup("اطلاعات کارت"));
            BuildHealthSection(_sections[0].AddGroup("سلامت قالب"));

            // ۲ — ظاهر
            BuildAppearanceSection(_sections[1].AddGroup("رنگ و قلم"));
            BuildLogoImageSection(_sections[1].AddGroup("تصویرها", advanced: true));
            BuildQrSection(_sections[1].AddGroup("نوار پایین کارت", advanced: true));

            // ۳ — محتوا
            // آموزش — کارت‌های «مورد» نمای اصلی‌اند؛ سه گروهِ قدیمی هنوز
            // ساخته می‌شوند (کنترل‌هایشان لازمِ CollectFields/CollectDesign
            // است) ولی به‌عنوان «پیشرفته» و پایین‌تر قرار می‌گیرند تا نمای
            // اصلی شلوغ نشود و در عینِ حال هیچ قابلیتی از دست نرود.
            BuildFieldSearchSection(_sections[2].AddGroup("جستجو و فیلتر"));
            BuildFieldCardsSection(_sections[2].AddGroup("موارد کارت"));
            BuildFieldsSection(_sections[2].AddGroup("فهرست سادهٔ نمایش (قدیمی)", advanced: true));
            BuildFieldOrderSection(_sections[2].AddGroup("ترتیب موارد", advanced: true));
            BuildTextOverridesSection(_sections[2].AddGroup("متن و قالب‌بندی (قدیمی)", advanced: true));

            // ۴ — پشت کارت
            BuildPrintSettingsSection(_sections[3].AddGroup("جدول پرداخت"));

            // ۵ — تاریخچه
            BuildVersionHistorySection(_sections[4].AddGroup("نسخه‌ها"));

            _nav.AddItem("کارت");
            _nav.AddItem("ظاهر");
            _nav.AddItem("محتوا");
            _nav.AddItem("پشت کارت");
            _nav.AddItem("تاریخچه");
            _nav.SelectedIndexChanged += delegate { ShowSection(_nav.SelectedIndex); };

            // آموزش — گامِ ۱ عمداً «خنثی از نظرِ رفتار» است: همهٔ گروه‌ها
            // (حتی آن‌هایی که پیشرفته علامت خورده‌اند) دیده می‌شوند، دقیقاً
            // مثلِ قبل. کلیدِ ساده/پیشرفته در گامِ ۲ اضافه می‌شود.
            for (int i = 0; i < _sections.Length; i++)
                _sections[i].SetAdvancedVisible(true);

            // کارت‌ها بعد از ساختهٔ‌شدنِ همهٔ بخش‌ها ساخته می‌شوند، چون به
            // _chkFields و _lstFieldOrder (که در بخش‌های بعدی ساخته می‌شوند)
            // نیاز دارند.
            RebuildFieldCards();

            ShowSection(0);
        }

        // جمع/باز کردنِ فهرستِ قالب‌ها — عرضِ ستون عوض می‌شود و فضای
        // آزادشده مستقیماً به پیش‌نمایش (که Dock=Fill است) می‌رسد.
        private void ToggleTemplateList()
        {
            if (_templateListHost == null || _templateListCard == null) return;

            _templateListCollapsed = !_templateListCollapsed;
            _templateListCard.Visible = !_templateListCollapsed;
            _templateListHost.Width = _templateListCollapsed ? 30 : 220;
            _btnCollapseList.Text = _templateListCollapsed ? "»" : "«";
            UiTheme.SetTip(_btnCollapseList,
                _templateListCollapsed ? "نمایش فهرست قالب‌ها" : "جمع‌کردن فهرست قالب‌ها");
        }

        // ═══════════════════════════════════════════════════════════════════
        // کارت‌های «مورد» — بخشِ محتوا
        //
        // این‌ها *جایگزینِ* منطقِ ذخیره‌سازی نیستند؛ یک نمای تازه روی همان
        // کنترل‌های موجودند: تیکِ هر کارت روی _chkFields می‌نشیند، ▲▼ روی
        // ترتیبِ _lstFieldOrder، و تنظیماتِ متن مستقیماً روی _textOverrides
        // (همان دیکشنری‌ای که CollectDesign از آن می‌خواند). بنابراین مسیرِ
        // ذخیره/بارگذاری/نسخه‌سازی دست‌نخورده می‌ماند.
        // ═══════════════════════════════════════════════════════════════════

        // ─── سلامتِ قالب ───────────────────────────────────────────────────
        private void BuildHealthSection(Panel content)
        {
            _healthHost = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                RightToLeft = RightToLeft.Yes
            };
            content.Controls.Add(_healthHost);
        }

        // بررسی خودکار است: هر بار که پیش‌نمایش زمان‌بندی می‌شود، این هم
        // تازه می‌شود (یعنی عملاً بعد از هر تغییرِ تنظیمات).
        private void RefreshHealth()
        {
            if (_healthHost == null || _txtName == null) return;

            List<HealthIssue> issues;
            try
            {
                bool simple = (_cmbLayoutVariant.SelectedItem as string) == LayoutSimple;
                string[] keys = simple
                    ? CardTemplateRepository.ToggleableFieldsSimple
                    : CardTemplateRepository.ToggleableFields;

                issues = TemplateHealthCheck.Run(
                    _txtName.Text, CollectDesign(), CollectFields(), simple, keys, ColumnExists);
            }
            catch (Exception ex)
            {
                issues = new List<HealthIssue>
                {
                    new HealthIssue(HealthLevel.Warning, "بررسی سلامت انجام نشد: " + ex.Message)
                };
            }

            _healthHost.SuspendLayout();
            Control[] old = new Control[_healthHost.Controls.Count];
            _healthHost.Controls.CopyTo(old, 0);
            _healthHost.Controls.Clear();
            for (int i = 0; i < old.Length; i++) old[i].Dispose();

            for (int i = issues.Count - 1; i >= 0; i--)
            {
                HealthIssue issue = issues[i];
                Label row = new Label
                {
                    Text = issue.Glyph + "  " + issue.Text,
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = issue.Text.Length > 60 ? 38 : 24,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = UiTheme.Font(9F),
                    ForeColor = ColorForHealth(issue.Level)
                };
                _healthHost.Controls.Add(row);
            }

            _healthHost.ResumeLayout(true);
        }

        private static Color ColorForHealth(HealthLevel level)
        {
            switch (level)
            {
                case HealthLevel.Error: return UiTheme.Danger;
                case HealthLevel.Warning: return UiTheme.Warning;
                case HealthLevel.Suggestion: return UiTheme.TextMuted;
                default: return UiTheme.Success;
            }
        }

        // بررسیِ وجودِ ستون — فقط خواندنی (PRAGMA)، با حافظهٔ موقت تا برای
        // هر بار تازه‌سازی دوباره به دیتابیس نرود.
        private bool ColumnExists(string table, string column)
        {
            string key = table + "." + column;
            bool cached;
            if (_columnCache.TryGetValue(key, out cached)) return cached;

            bool found = false;
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                {
                    con.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand("PRAGMA table_info(" + table + ")", con))
                    using (SQLiteDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (string.Equals(Convert.ToString(dr["name"]), column, StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch { found = true; } // در تردید، هشدارِ نادرست نده.

            _columnCache[key] = found;
            return found;
        }

        // آموزش — نوارِ جستجو عمداً در یک «گروهِ» جدا از فهرستِ کارت‌ها است.
        // اگر داخلِ همان گروه باشد، میزبانِ کارت‌ها (که AutoSize است و
        // می‌تواند هزار پیکسل بلند شود) ردیف‌های ۳۰ پیکسلی را به انتهای
        // پنل می‌راند و عملاً از دید خارج می‌شوند — این با آزمونِ عکس‌برداری
        // دیده شد، نه با حدس.
        private void BuildFieldSearchSection(Panel content)
        {
            _cmbFieldFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
            _cmbFieldFilter.Items.AddRange(new object[] { "همه", "فقط روشن", "فقط خاموش" });
            _cmbFieldFilter.SelectedIndex = 0;
            _cmbFieldFilter.SelectedIndexChanged += delegate { ApplyFieldCardFilter(); };

            _txtFieldSearch = new TextBox { RightToLeft = RightToLeft.Yes };
            UiTheme.StyleTextBox(_txtFieldSearch);
            _txtFieldSearch.TextChanged += delegate { ApplyFieldCardFilter(); };
            UiTheme.SetTip(_txtFieldSearch, "نام مورد یا منبع آن را بنویسید");

            MakeRow(content, "جستجوی مورد", _txtFieldSearch, controlWidth: 220);
            MakeRow(content, "نمایش", _cmbFieldFilter, controlWidth: 160);
        }

        private void BuildFieldCardsSection(Panel content)
        {
            _fieldCardsHost = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                RightToLeft = RightToLeft.Yes
            };
            content.Controls.Add(_fieldCardsHost);
        }

        private void RebuildFieldCards()
        {
            if (_fieldCardsHost == null || _cmbLayoutVariant == null) return;

            _fieldCardsHost.SuspendLayout();
            // آموزش — Dispose خودش کنترل را از Controls برمی‌دارد، پس
            // Dispose کردن داخلِ foreach روی همان مجموعه استثنا می‌دهد.
            // اول یک کپی گرفته می‌شود.
            Control[] old = new Control[_fieldCardsHost.Controls.Count];
            _fieldCardsHost.Controls.CopyTo(old, 0);
            _fieldCardsHost.Controls.Clear();
            for (int i = 0; i < old.Length; i++) old[i].Dispose();
            _fieldCards.Clear();
            _fieldGroupHeaders.Clear();

            bool simple = (_cmbLayoutVariant.SelectedItem as string) == LayoutSimple;
            string[] keys = simple
                ? CardTemplateRepository.ToggleableFieldsSimple
                : CardTemplateRepository.ToggleableFields;

            var byGroup = new Dictionary<string, List<string>>();
            foreach (string k in keys)
            {
                CardFieldInfo gi = CardFieldCatalog.Get(k, simple);
                if (!byGroup.ContainsKey(gi.Group)) byGroup[gi.Group] = new List<string>();
                byGroup[gi.Group].Add(k);
            }

            // معکوس اضافه می‌شوند تا ترتیبِ طبیعی روی صفحه حفظ شود.
            for (int g = CardFieldCatalog.GroupOrder.Length - 1; g >= 0; g--)
            {
                string group = CardFieldCatalog.GroupOrder[g];
                List<string> members;
                if (!byGroup.TryGetValue(group, out members) || members.Count == 0) continue;

                for (int i = members.Count - 1; i >= 0; i--)
                {
                    CardFieldInfo info = CardFieldCatalog.Get(members[i], simple);

                    TextFieldOverride ov;
                    if (!_textOverrides.TryGetValue(info.Key, out ov) || ov == null)
                        ov = new TextFieldOverride();

                    FieldCard card = new FieldCard(info, ov);
                    card.VisibleToggled += FieldCard_VisibleToggled;
                    card.OverrideChanged += FieldCard_OverrideChanged;
                    card.MoveUp += FieldCard_MoveUp;
                    card.MoveDown += FieldCard_MoveDown;

                    _fieldCardsHost.Controls.Add(card);
                    _fieldCards.Add(card);
                }

                Label header = new Label
                {
                    Text = group + "  (" + members.Count + ")",
                    Dock = DockStyle.Top,
                    Height = 28,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = UiTheme.FontBold(9F),
                    ForeColor = UiTheme.Primary,
                    Padding = new Padding(0, 6, 2, 0)
                };
                _fieldCardsHost.Controls.Add(header);
                _fieldGroupHeaders[group] = header;
            }

            _fieldCardsHost.ResumeLayout(true);
            SyncFieldCardStates();
            ApplyFieldCardFilter();
        }

        private void SyncFieldCardStates()
        {
            if (_fieldCards.Count == 0 || _chkFields == null) return;

            bool simple = (_cmbLayoutVariant.SelectedItem as string) == LayoutSimple;
            string[] keys = simple
                ? CardTemplateRepository.ToggleableFieldsSimple
                : CardTemplateRepository.ToggleableFields;

            _syncingCards = true;
            try
            {
                int total = _lstFieldOrder == null ? 0 : _lstFieldOrder.Items.Count;

                for (int i = 0; i < _fieldCards.Count; i++)
                {
                    FieldCard card = _fieldCards[i];

                    int idx = Array.IndexOf(keys, card.Key);
                    if (idx >= 0 && idx < _chkFields.Items.Count)
                        card.FieldVisible = _chkFields.GetItemChecked(idx);

                    int pos = -1;
                    for (int j = 0; j < total; j++)
                        if (((OrderItem)_lstFieldOrder.Items[j]).Key == card.Key) { pos = j; break; }
                    card.SetOrderInfo(pos, total);
                }
            }
            finally { _syncingCards = false; }
        }

        private void ApplyFieldCardFilter()
        {
            if (_fieldCardsHost == null) return;

            string q = _txtFieldSearch == null ? "" : _txtFieldSearch.Text.Trim();
            int mode = _cmbFieldFilter == null ? 0 : _cmbFieldFilter.SelectedIndex;

            _fieldCardsHost.SuspendLayout();

            var groupHasVisible = new Dictionary<string, bool>();
            for (int i = 0; i < _fieldCards.Count; i++)
            {
                FieldCard card = _fieldCards[i];

                bool matchText = q.Length == 0
                    || card.Info.Label.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || card.Info.SourceText.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || card.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

                bool matchMode = mode == 0
                    || (mode == 1 && card.FieldVisible)
                    || (mode == 2 && !card.FieldVisible);

                bool show = matchText && matchMode;
                card.Visible = show;

                if (show) groupHasVisible[card.Info.Group] = true;
            }

            foreach (KeyValuePair<string, Label> kv in _fieldGroupHeaders)
                kv.Value.Visible = groupHasVisible.ContainsKey(kv.Key);

            _fieldCardsHost.ResumeLayout(true);
        }

        private void FieldCard_VisibleToggled(object sender, EventArgs e)
        {
            if (_syncingCards) return;
            FieldCard card = sender as FieldCard;
            if (card == null || _chkFields == null) return;

            bool simple = (_cmbLayoutVariant.SelectedItem as string) == LayoutSimple;
            string[] keys = simple
                ? CardTemplateRepository.ToggleableFieldsSimple
                : CardTemplateRepository.ToggleableFields;

            int idx = Array.IndexOf(keys, card.Key);
            if (idx >= 0 && idx < _chkFields.Items.Count)
                _chkFields.SetItemChecked(idx, card.FieldVisible);

            if (_cmbFieldFilter != null && _cmbFieldFilter.SelectedIndex != 0) ApplyFieldCardFilter();
            SchedulePreviewRefresh();
        }

        private void FieldCard_OverrideChanged(object sender, EventArgs e)
        {
            if (_syncingCards) return;
            FieldCard card = sender as FieldCard;
            if (card == null) return;

            _textOverrides[card.Key] = card.Override;
            SyncLegacyOverrideEditorFor(card.Key);
            SchedulePreviewRefresh();
        }

        // آموزش — تلهٔ ظریف: CollectDesign همیشه
        // CommitTextOverrideEditor(_currentTextOverrideKey) را صدا می‌زند و آن
        // متد مقدارِ *کنترل‌های ویرایشگرِ قدیمی* را روی _textOverrides
        // می‌نویسد. اگر کاربر همان مورد را از طریقِ کارت عوض کرده باشد،
        // مقدارِ تازه با مقدارِ کهنهٔ آن کنترل‌ها بازنویسی می‌شد. اینجا اول
        // کلیدِ جاری خالی می‌شود (تا Commit بی‌اثر شود) و بعد ویرایشگرِ قدیمی
        // از روی دیکشنریِ به‌روز دوباره بارگذاری می‌شود.
        private void SyncLegacyOverrideEditorFor(string key)
        {
            if (_cmbTextOverrideField == null) return;
            if (_currentTextOverrideKey != key) return;

            _currentTextOverrideKey = "";
            int idx = _cmbTextOverrideField.SelectedIndex;
            if (idx < 0) return;
            _cmbTextOverrideField.SelectedIndex = -1;
            _cmbTextOverrideField.SelectedIndex = idx;
        }

        private void FieldCard_MoveUp(object sender, EventArgs e)
        {
            FieldCard card = sender as FieldCard;
            if (card != null) MoveFieldOrder(card.Key, -1);
        }

        private void FieldCard_MoveDown(object sender, EventArgs e)
        {
            FieldCard card = sender as FieldCard;
            if (card != null) MoveFieldOrder(card.Key, +1);
        }

        private void MoveFieldOrder(string key, int delta)
        {
            if (_lstFieldOrder == null) return;

            int idx = -1;
            for (int i = 0; i < _lstFieldOrder.Items.Count; i++)
                if (((OrderItem)_lstFieldOrder.Items[i]).Key == key) { idx = i; break; }
            if (idx < 0) return;

            int target = idx + delta;
            if (target < 0 || target >= _lstFieldOrder.Items.Count) return;

            object item = _lstFieldOrder.Items[idx];
            _lstFieldOrder.Items.RemoveAt(idx);
            _lstFieldOrder.Items.Insert(target, item);

            SyncFieldCardStates();
            SchedulePreviewRefresh();
        }

        private void ShowSection(int index)
        {
            if (_sections == null) return;
            for (int i = 0; i < _sections.Length; i++)
                _sections[i].Visible = (i == index);
        }

        private TabPage MakeTab(string title, Action<Panel> build)
        {
            TabPage page = new TabPage(title) { BackColor = UiTheme.Background, Padding = new Padding(4) };
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };
            SectionCard card = new SectionCard { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(16) };
            Panel content = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            build(content);
            card.Controls.Add(content);
            scroll.Controls.Add(card);
            page.Controls.Add(scroll);
            return page;
        }

        private void BuildInfoSection(Panel content)
        {
            _txtName = new TextBox { RightToLeft = RightToLeft.Yes };
            MakeRow(content, "نام قالب", _txtName, controlWidth: 320);

            // آموزش — Phase 2 («مدیریتِ حرفه‌ایِ قالب»): نوعِ کارت — چند
            // مقدارِ رایج پیش‌فرض هستند ولی DropDown (نه DropDownList) است
            // تا متنِ آزادِ دلخواه هم پذیرفته شود؛ خالی = بدونِ نوعِ مشخص
            // (رفتارِ قالب‌های قبلی که این ستون را نداشتند).
            _cmbTemplateType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, RightToLeft = RightToLeft.Yes };
            _cmbTemplateType.Items.AddRange(new object[] { "کارت ایتام", "کارت مددجو", "کارت خانواده", "کارت پرسنل" });
            MakeRow(content, "نوعِ کارت", _cmbTemplateType, controlWidth: 320);

            _txtDescription = new TextBox { RightToLeft = RightToLeft.Yes, Multiline = true, Height = 54 };
            MakeRow(content, "توضیحات", _txtDescription, height: 60, controlWidth: 320);

            _chkIsActive = new CheckBox { Text = "قالب فعال است", Checked = true, AutoSize = true, RightToLeft = RightToLeft.Yes };
            _chkIsActive.CheckedChanged += delegate { ToggleActiveState(); };
            MakeRow(content, "وضعیت", _chkIsActive, controlWidth: 320);

            Button btnDelete = UiTheme.CreateSecondaryButton("حذفِ این قالب", "✕");
            btnDelete.Size = new Size(140, 28);
            btnDelete.Click += delegate { DeleteCurrent(); };

            Button btnDuplicate = UiTheme.CreateSecondaryButton("تکثیرِ این قالب", "⧉");
            btnDuplicate.Size = new Size(140, 28);
            btnDuplicate.Margin = new Padding(0, 0, 8, 0);
            btnDuplicate.Click += delegate { DuplicateCurrent(); };

            Button btnResetDefault = UiTheme.CreateSecondaryButton("بازگردانی به پیش‌فرض", "↺");
            btnResetDefault.Size = new Size(170, 28);
            btnResetDefault.Margin = new Padding(0, 0, 8, 0);
            btnResetDefault.Click += delegate { ResetToDefault(); };

            Panel actionsRow = new Panel { Height = 28 };
            btnDelete.Dock = DockStyle.Right;
            btnDuplicate.Dock = DockStyle.Right;
            btnResetDefault.Dock = DockStyle.Right;
            actionsRow.Controls.Add(btnDelete);
            actionsRow.Controls.Add(btnDuplicate);
            actionsRow.Controls.Add(btnResetDefault);
            MakeRow(content, "", actionsRow, controlWidth: 320);

            // آموزش — «طرحِ ساده» یک HTML کاملاً دیگر است (simple.html، فیلدهای
            // متفاوت، همان اندازهٔ کارت) — نه فقط خاموش/روشن‌کردنِ فیلدهای طرحِ
            // کامل. نگاه کنید FrmGuardianCardPreview.RenderDataAsync.
            _cmbLayoutVariant = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
            _cmbLayoutVariant.Items.Add(LayoutFull);
            _cmbLayoutVariant.Items.Add(LayoutSimple);
            _cmbLayoutVariant.SelectedIndex = 0;
            _cmbLayoutVariant.SelectedIndexChanged += delegate { PopulateFieldChecklist(null); PopulateFieldOrderLists(null); RebuildFieldCards(); SchedulePreviewRefresh(); };
            MakeRow(content, "نوع طرح", _cmbLayoutVariant, controlWidth: 320);

            _lblDefaultTag = new Label
            {
                Text = "", Dock = DockStyle.Top, Height = 22,
                ForeColor = UiTheme.Primary, Font = UiTheme.FontBold(9F),
                TextAlign = ContentAlignment.MiddleRight
            };
            content.Controls.Add(_lblDefaultTag);

            // آموزش — Phase 2: متادیتایِ فقط‌خواندنی (کِی/توسطِ چه کسی ایجاد/
            // ویرایش شد) — برایِ قالب‌های تازه (هنوز ذخیره نشده) خالی می‌ماند.
            _lblMetaInfo = new Label
            {
                Text = "", Dock = DockStyle.Top, Height = 36,
                ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(8.5F),
                TextAlign = ContentAlignment.MiddleRight
            };
            content.Controls.Add(_lblMetaInfo);
        }

        private void BuildFieldsSection(Panel content)
        {
            Label hint = new Label
            {
                Text = "ترتیبِ این لیست فقط برای سازمان‌دهیِ شماست — موقعیتِ واقعیِ فیلد روی کارتِ چاپی ثابت است.",
                Dock = DockStyle.Top, Height = 38, ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(8.5F),
                TextAlign = ContentAlignment.MiddleRight
            };
            content.Controls.Add(hint);

            _chkFields = new CheckedListBox
            {
                Dock = DockStyle.Top, Height = 320, RightToLeft = RightToLeft.Yes,
                CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle, Font = UiTheme.Font(9.5F),
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 28
            };
            _chkFields.DrawItem += ChkFields_DrawItem;
            _chkFields.ItemCheck += delegate { SchedulePreviewRefresh(); };
            content.Controls.Add(_chkFields);
        }

        // آموزش — Card Designer Phase 1، «ترتیبِ واقعیِ فیلدها»: برخلافِ
        // چک‌لیستِ بالا (که ترتیبش فقط تزئینی است)، این تب واقعاً روی
        // پیش‌نمایش و کارتِ چاپی اثر می‌گذارد (نگاه کنید
        // GuardianCardRenderer.ApplyFieldOrderOverrides). محدودۀ مصوب: عکس
        // فقط قبل/بعدِ گروهِ فیلدها (نه در میانِ آن‌ها، چون شکلِ بصری‌اش با
        // ردیف‌های متنی هم‌شکل نیست)؛ فیلدهای متنی و نوارِ امنیتی هرکدام
        // آزادانه در گروهِ خودشان قابلِ‌جابه‌جایی‌اند.
        private void BuildFieldOrderSection(Panel content)
        {
            Label hint = new Label
            {
                Text = "این ترتیب واقعاً روی پیش‌نمایش و کارتِ چاپی اعمال می‌شود — هر ردیف را بکش تا جایش عوض شود.",
                Dock = DockStyle.Top, Height = 38, ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(8.5F),
                TextAlign = ContentAlignment.MiddleRight
            };
            content.Controls.Add(hint);

            Label lblPhoto = new Label
            {
                Text = "موقعیتِ عکس نسبت به فیلدها", Dock = DockStyle.Top, Height = 22,
                ForeColor = UiTheme.TextMuted, TextAlign = ContentAlignment.MiddleRight
            };
            content.Controls.Add(lblPhoto);

            Panel photoRow = new Panel { Dock = DockStyle.Top, Height = 28, Margin = new Padding(0, 0, 0, 10) };
            _radPhotoBefore = new RadioButton { Text = "قبل از فیلدها", Checked = true, AutoSize = true, RightToLeft = RightToLeft.Yes, Dock = DockStyle.Right, Width = 120 };
            _radPhotoAfter = new RadioButton { Text = "بعد از فیلدها", AutoSize = true, RightToLeft = RightToLeft.Yes, Dock = DockStyle.Right, Width = 120 };
            _radPhotoBefore.CheckedChanged += delegate { SchedulePreviewRefresh(); };
            _radPhotoAfter.CheckedChanged += delegate { SchedulePreviewRefresh(); };
            photoRow.Controls.Add(_radPhotoAfter);
            photoRow.Controls.Add(_radPhotoBefore);
            content.Controls.Add(photoRow);

            Label lblFieldOrder = new Label
            {
                Text = "ترتیبِ فیلدهای متنی", Dock = DockStyle.Top, Height = 22,
                ForeColor = UiTheme.TextMuted, TextAlign = ContentAlignment.MiddleRight
            };
            content.Controls.Add(lblFieldOrder);

            _lstFieldOrder = new ListBox
            {
                Dock = DockStyle.Top, Height = 150, RightToLeft = RightToLeft.Yes,
                IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, Font = UiTheme.Font(9.5F)
            };
            WireDragReorder(_lstFieldOrder);
            content.Controls.Add(_lstFieldOrder);

            _lblBandOrderTitle = new Label
            {
                Text = "ترتیبِ نوارِ امنیتیِ پایینِ کارت (QR / بارکد / امضا / مهر / هولوگرام)",
                Dock = DockStyle.Top, Height = 22, ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, 10, 0, 0)
            };
            content.Controls.Add(_lblBandOrderTitle);

            _lstBandOrder = new ListBox
            {
                Dock = DockStyle.Top, Height = 130, RightToLeft = RightToLeft.Yes,
                IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, Font = UiTheme.Font(9.5F)
            };
            WireDragReorder(_lstBandOrder);
            content.Controls.Add(_lstBandOrder);
        }

        // آموزش — الگویِ استانداردِ کشیدن-و-رهاکردنِ ردیف‌های یک ListBox در
        // WinForms (بدونِ کتابخانهٔ اضافه): MouseDown اندیسِ مبدأ را ثبت
        // می‌کند، MouseMove با نگه‌داشتنِ دکمهٔ چپ DoDragDrop را شروع
        // می‌کند، DragDrop آیتم را از جای قبلی برمی‌دارد و در اندیسِ مقصد
        // دوباره می‌گذارد.
        private static void WireDragReorder(ListBox lb)
        {
            lb.AllowDrop = true;
            int dragIndex = -1;

            lb.MouseDown += delegate (object s, MouseEventArgs e)
            {
                dragIndex = lb.IndexFromPoint(e.Location);
            };
            lb.MouseMove += delegate (object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && dragIndex >= 0)
                    lb.DoDragDrop(lb.Items[dragIndex], DragDropEffects.Move);
            };
            lb.DragEnter += delegate (object s, DragEventArgs e) { e.Effect = DragDropEffects.Move; };
            lb.DragOver += delegate (object s, DragEventArgs e) { e.Effect = DragDropEffects.Move; };
            lb.DragDrop += delegate (object s, DragEventArgs e)
            {
                Point p = lb.PointToClient(new Point(e.X, e.Y));
                int targetIndex = lb.IndexFromPoint(p);
                if (targetIndex < 0) targetIndex = lb.Items.Count - 1;
                if (dragIndex < 0 || targetIndex < 0 || dragIndex == targetIndex) { dragIndex = -1; return; }

                object item = lb.Items[dragIndex];
                lb.Items.RemoveAt(dragIndex);
                lb.Items.Insert(targetIndex, item);
                dragIndex = -1;
            };
        }

        // آموزش — هم‌الگویِ PopulateFieldChecklist: با تعویضِ نوعِ طرح
        // (کامل/ساده) دوباره ساخته می‌شود چون فیلدهای قابل‌ترتیب هرکدام
        // فرق دارند (نگاه کنید FieldOrderableKeys/FieldOrderableKeysSimple).
        // design=null یعنی «قالبِ تازه» — همه با ترتیبِ طبیعی/پیش‌فرض.
        private void PopulateFieldOrderLists(CardTemplateDesign design)
        {
            if (_lstFieldOrder == null) return;

            bool isSimple = (_cmbLayoutVariant.SelectedItem as string) == LayoutSimple;
            string[] allowedKeys = isSimple ? CardTemplateRepository.FieldOrderableKeysSimple : CardTemplateRepository.FieldOrderableKeys;
            Dictionary<string, string> labels = isSimple ? FieldLabelsSimple : FieldLabels;

            List<string> order = design != null
                ? CardTemplateRepository.ParseFieldOrder(design.FieldOrderCsv, allowedKeys)
                : new List<string>();
            foreach (string key in allowedKeys)
                if (!order.Contains(key))
                    order.Add(key);

            _lstFieldOrder.Items.Clear();
            foreach (string key in order)
                _lstFieldOrder.Items.Add(new OrderItem { Key = key, Label = labels.ContainsKey(key) ? labels[key] : key });

            bool showBand = !isSimple;
            _lblBandOrderTitle.Visible = showBand;
            _lstBandOrder.Visible = showBand;

            if (showBand)
            {
                List<string> bandOrder = design != null
                    ? CardTemplateRepository.ParseFieldOrder(design.SecurityBandOrderCsv, CardTemplateRepository.SecurityBandOrderableKeys)
                    : new List<string>();
                foreach (string key in CardTemplateRepository.SecurityBandOrderableKeys)
                    if (!bandOrder.Contains(key))
                        bandOrder.Add(key);

                _lstBandOrder.Items.Clear();
                foreach (string key in bandOrder)
                    _lstBandOrder.Items.Add(new OrderItem { Key = key, Label = SecurityBandFieldLabels.ContainsKey(key) ? SecurityBandFieldLabels[key] : key });
            }

            bool photoAfter = design != null && design.PhotoPosition == "After";
            _radPhotoBefore.Checked = !photoAfter;
            _radPhotoAfter.Checked = photoAfter;
        }

        private string CollectFieldOrder()
        {
            var keys = new List<string>();
            foreach (object item in _lstFieldOrder.Items)
                keys.Add(((OrderItem)item).Key);
            return CardTemplateRepository.BuildFieldOrderCsv(keys);
        }

        private string CollectSecurityBandOrder()
        {
            if (!_lstBandOrder.Visible) return "";
            var keys = new List<string>();
            foreach (object item in _lstBandOrder.Items)
                keys.Add(((OrderItem)item).Key);
            return CardTemplateRepository.BuildFieldOrderCsv(keys);
        }

        private string CollectPhotoPosition()
        {
            return _radPhotoAfter.Checked ? "After" : "";
        }

        private void BuildAppearanceSection(Panel content)
        {
            MakeRow(content, "رنگ اصلی", MakeColorSwatch(out _pnlPrimaryColor), controlWidth: 130);
            MakeRow(content, "رنگ فرعی", MakeColorSwatch(out _pnlSecondaryColor), controlWidth: 130);
            MakeRow(content, "رنگ پس‌زمینه", MakeColorSwatch(out _pnlBackgroundColor), controlWidth: 130);
            MakeRow(content, "رنگ متن", MakeColorSwatch(out _pnlTextColor), controlWidth: 130);

            _cmbFont = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbFont.Items.Add("");
            _cmbFont.Items.AddRange(FontChoices);
            _cmbFont.SelectedIndexChanged += delegate { SchedulePreviewRefresh(); };
            MakeRow(content, "فونت", _cmbFont, controlWidth: 200);

            _numFontScale = new NumericUpDown { Minimum = 50, Maximum = 200, Value = 100, TextAlign = HorizontalAlignment.Center };
            MakeRow(content, "اندازهٔ فونت (٪)", MakeStepper(_numFontScale), controlWidth: 110);
        }

        private void BuildQrSection(Panel content)
        {
            _chkQRCode = new CheckBox { Text = "نمایشِ QR Code", AutoSize = true, RightToLeft = RightToLeft.Yes, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
            _chkBarcode = new CheckBox { Text = "نمایشِ بارکد", AutoSize = true, RightToLeft = RightToLeft.Yes, Checked = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
            _chkHologram = new CheckBox { Text = "هولوگرامِ امنیتی", AutoSize = true, RightToLeft = RightToLeft.Yes, Checked = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
            _chkQRCode.CheckedChanged += delegate { SchedulePreviewRefresh(); };
            _chkBarcode.CheckedChanged += delegate { SchedulePreviewRefresh(); };
            _chkHologram.CheckedChanged += delegate { SchedulePreviewRefresh(); };
            content.Controls.Add(_chkHologram);
            content.Controls.Add(_chkBarcode);
            content.Controls.Add(_chkQRCode);
        }

        private void BuildLogoImageSection(Panel content)
        {
            MakeRow(content, "لوگو/پس‌زمینهٔ روی کارت", MakeImagePicker(out _txtBgFront), controlWidth: 260);
            MakeRow(content, "پس‌زمینهٔ پشتِ کارت", MakeImagePicker(out _txtBgBack), controlWidth: 260);
            MakeRow(content, "واترمارک", MakeImagePicker(out _txtWatermark), controlWidth: 260);

            _numWatermarkOpacity = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 15, TextAlign = HorizontalAlignment.Center };
            MakeRow(content, "شفافیتِ واترمارک (٪)", MakeStepper(_numWatermarkOpacity), controlWidth: 110);

            Panel sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiTheme.Border, Margin = new Padding(0, 6, 0, 10) };
            content.Controls.Add(sep);

            MakeRow(content, "رنگِ پس‌زمینهٔ هدر", MakeColorSwatch(out _pnlHeaderBgColor), controlWidth: 130);

            _numPortraitScale = new NumericUpDown { Minimum = 50, Maximum = 300, Value = 100, TextAlign = HorizontalAlignment.Center };
            MakeRow(content, "اندازهٔ عکسِ گردِ هدر (٪)", MakeStepper(_numPortraitScale), controlWidth: 110);

            _chkPortraitBlank = new CheckBox { Text = "عکسِ پیش‌فرضِ هدر حذف شود (فقط قابِ خالی)", AutoSize = true, RightToLeft = RightToLeft.Yes, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 8) };
            _chkPortraitBlank.CheckedChanged += delegate { SchedulePreviewRefresh(); };
            content.Controls.Add(_chkPortraitBlank);

            // آموزش — به‌درخواستِ کاربر: ارتفاعِ نوارِ رنگیِ بالای کارت
            // (پیش‌فرض ۲۲.۴mm) کوچک‌تر شود تا فضای بیشتری برایِ اطلاعاتِ
            // اصلی بماند — مثلاً ۷۵٪ یعنی ۲۵٪ کوچک‌تر.
            _numHeaderHeightScale = new NumericUpDown { Minimum = 30, Maximum = 200, Value = 100, TextAlign = HorizontalAlignment.Center };
            MakeRow(content, "ارتفاعِ هدر (٪)", MakeStepper(_numHeaderHeightScale), controlWidth: 110);

            Panel sepFamilyPhoto = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiTheme.Border, Margin = new Padding(0, 6, 0, 10) };
            content.Controls.Add(sepFamilyPhoto);

            // آموزش — به‌درخواستِ صریحِ کاربر: ابعاد (نسبتِ طول‌به‌عرض) و
            // اندازهٔ قابِ عکسِ جمعیِ خانواده جداگانه قابل‌تنظیم شد — نگاه
            // کنید CardTemplateDesign.FamilyPhotoAspectRatio/
            // FamilyPhotoScalePercent و GuardianCardRenderer.
            // ApplyDesignOverrides. پیش‌فرض «۱:۱ (مربع)، ۱۰۰٪» دقیقاً همان
            // چیزی است که همین حالا روی کارت هست.
            _cmbFamilyPhotoRatio = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
            foreach (var kv in FamilyPhotoRatioLabels) _cmbFamilyPhotoRatio.Items.Add(kv.Value);
            _cmbFamilyPhotoRatio.SelectedIndexChanged += delegate { SchedulePreviewRefresh(); };
            MakeRow(content, "ابعادِ عکسِ جمعی", _cmbFamilyPhotoRatio, controlWidth: 200);

            _numFamilyPhotoScale = new NumericUpDown { Minimum = 50, Maximum = 300, Value = 100, TextAlign = HorizontalAlignment.Center };
            MakeRow(content, "اندازهٔ عکسِ جمعی (٪)", MakeStepper(_numFamilyPhotoScale), controlWidth: 110);

            // آموزش — به‌درخواستِ صریحِ کاربر: وقتی عکسِ آپلودشده با قابِ
            // عکسِ جمعی هم‌نسبت نیست، این تیک به‌جایِ برشِ پیش‌فرض (که ممکن
            // است بخشی از اعضا را از کادر بیرون بیندازد)، کلِ عکس را بدونِ
            // برش نشان می‌دهد (نگاه کنید CardTemplateDesign.
            // FamilyPhotoFitContain/GuardianCardRenderer.ApplyDesignOverrides).
            // پیش‌فرض خاموش — چون بعضی قالب‌ها همان برشِ فعلی را می‌خواهند.
            _chkFamilyPhotoFitContain = new CheckBox { Text = "نمایشِ کاملِ عکسِ جمعی بدونِ برش (اگر نسبت یکی نبود)", AutoSize = true, RightToLeft = RightToLeft.Yes, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 8) };
            _chkFamilyPhotoFitContain.CheckedChanged += delegate { SchedulePreviewRefresh(); };
            content.Controls.Add(_chkFamilyPhotoFitContain);
        }

        // آموزش — کلید=مقدارِ ذخیره‌شده ("W:H")، مقدار=برچسبِ فارسی برایِ UI.
        // ترتیب هم همان ترتیبِ نمایش در کمبو است.
        private static readonly List<KeyValuePair<string, string>> FamilyPhotoRatioLabels = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("1:1", "۱:۱ (مربع)"),
            new KeyValuePair<string, string>("3:4", "۳:۴ (عمودی)"),
            new KeyValuePair<string, string>("4:3", "۴:۳ (افقی)"),
            new KeyValuePair<string, string>("2:3", "۲:۳ (عمودی)"),
            new KeyValuePair<string, string>("3:2", "۳:۲ (افقی)"),
            new KeyValuePair<string, string>("9:16", "۹:۱۶ (عمودی بلند)"),
            new KeyValuePair<string, string>("16:9", "۱۶:۹ (افقی عریض)")
        };

        private static string FamilyPhotoRatioLabelFor(string ratioValue)
        {
            foreach (var kv in FamilyPhotoRatioLabels)
                if (kv.Key == ratioValue) return kv.Value;
            return FamilyPhotoRatioLabels[0].Value;
        }

        private static string FamilyPhotoRatioValueFor(string label)
        {
            foreach (var kv in FamilyPhotoRatioLabels)
                if (kv.Value == label) return kv.Key;
            return FamilyPhotoRatioLabels[0].Key;
        }

        private void BuildPrintSettingsSection(Panel content)
        {
            Label hint = new Label
            {
                Text = "ماه‌هایی که تیک ندارند اصلاً روی جدولِ پرداختِ برگهٔ دوم چاپ نمی‌شوند.",
                Dock = DockStyle.Top, Height = 38, ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(8.5F),
                TextAlign = ContentAlignment.MiddleRight
            };
            content.Controls.Add(hint);

            _chkMonths = new CheckedListBox
            {
                Dock = DockStyle.Top, Height = 130, RightToLeft = RightToLeft.Yes,
                CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle,
                MultiColumn = true, ColumnWidth = 140, Font = UiTheme.Font(9.5F)
            };
            foreach (string month in CardTemplateRepository.AllMonthNames)
                _chkMonths.Items.Add(month, true);
            _chkMonths.ItemCheck += delegate { SchedulePreviewRefresh(); };
            content.Controls.Add(_chkMonths);

            Panel sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiTheme.Border, Margin = new Padding(0, 10, 0, 10) };
            content.Controls.Add(sep);

            // آموزش — به‌درخواستِ کاربر: سقفِ تعدادِ ردیفِ فهرستِ اعضای خانواده
            // روی کارتِ کامل (۰=بدونِ سقف). نگاه کنید
            // GuardianCardRenderer.StageAndPopulate.
            _numFamilyListMaxRows = new NumericUpDown { Minimum = 0, Maximum = 30, Value = 0, TextAlign = HorizontalAlignment.Center };
            MakeRow(content, "سقفِ ردیفِ فهرستِ اعضا (۰=بدونِ سقف)", MakeStepper(_numFamilyListMaxRows), controlWidth: 110);
        }

        // آموزش — ویرایشگرِ عمومیِ محتوا/رنگ/سایزِ متن‌های ثابتِ کارت
        // (بسمه‌تعالی/موتو/تیتر/پیام‌های پایینِ کارت/آدرس/تماس/وبسایت/ایمیل)
        // — دستهٔ «متن پشتِ کارت و حدیث» طبقِ خواستهٔ کاربر؛ نگاه کنید
        // CardTemplateDesign.TextOverrides و GuardianCardRenderer.BuildTextOverrideScript.
        private void BuildTextOverridesSection(Panel content)
        {
            Label hint = new Label
            {
                Text = "یک متن را از فهرست انتخاب کن، بعد محتوا/رنگ/سایزش را عوض کن (خالی/۱۰۰٪ = بدونِ تغییر).",
                Dock = DockStyle.Top, Height = 38, ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(8.5F),
                TextAlign = ContentAlignment.MiddleRight
            };
            content.Controls.Add(hint);

            _cmbTextOverrideField = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
            foreach (string key in TextOverrideFieldKeys)
                _cmbTextOverrideField.Items.Add(TextOverrideFieldLabels[key]);
            MakeRow(content, "متن", _cmbTextOverrideField, controlWidth: 280);

            _txtTextOverrideContent = new TextBox { RightToLeft = RightToLeft.Yes };
            _txtTextOverrideContent.TextChanged += delegate { SchedulePreviewRefresh(); };
            MakeRow(content, "محتوا", _txtTextOverrideContent, controlWidth: 280);

            MakeRow(content, "رنگ", MakeColorSwatch(out _pnlTextOverrideColor), controlWidth: 130);

            _numTextOverrideScale = new NumericUpDown { Minimum = 50, Maximum = 300, Value = 100, TextAlign = HorizontalAlignment.Center };
            MakeRow(content, "اندازهٔ فونت (٪)", MakeStepper(_numTextOverrideScale), controlWidth: 110);

            _cmbTextOverrideFont = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            _cmbTextOverrideFont.Items.Add("");
            _cmbTextOverrideFont.Items.AddRange(FontChoices);
            _cmbTextOverrideFont.SelectedIndexChanged += delegate { SchedulePreviewRefresh(); };
            MakeRow(content, "نوعِ فونت", _cmbTextOverrideFont, controlWidth: 160);

            _numTextOverrideLineHeight = new NumericUpDown { Minimum = 50, Maximum = 300, Value = 100, TextAlign = HorizontalAlignment.Center };
            MakeRow(content, "فاصلهٔ خط (٪)", MakeStepper(_numTextOverrideLineHeight), controlWidth: 110);

            // آموزش — Card Designer Phase 1: تراز و وزنِ همین یک فیلد.
            // آیتمِ اول («پیش‌فرض») در هر دو یعنی رشتهٔ خالی = بدونِ تغییر
            // (ارثِ طراحِ پایه) — دقیقاً هم‌قراردادِ بقیهٔ کنترل‌های این تب.
            _cmbTextOverrideAlignment = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
            _cmbTextOverrideAlignment.Items.AddRange(new object[] { "پیش‌فرض", "راست", "وسط", "چپ" });
            _cmbTextOverrideAlignment.SelectedIndexChanged += delegate { SchedulePreviewRefresh(); };
            MakeRow(content, "ترازِ متن", _cmbTextOverrideAlignment, controlWidth: 140);

            _cmbTextOverrideWeight = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
            _cmbTextOverrideWeight.Items.AddRange(new object[] { "پیش‌فرض", "معمولی", "متوسط", "نیمه‌ضخیم", "ضخیم" });
            _cmbTextOverrideWeight.SelectedIndexChanged += delegate { SchedulePreviewRefresh(); };
            MakeRow(content, "وزنِ فونت", _cmbTextOverrideWeight, controlWidth: 140);

            _cmbTextOverrideField.SelectedIndexChanged += delegate { SwitchTextOverrideField(); };
            _cmbTextOverrideField.SelectedIndex = 0;
        }

        // آموزش — Phase 2 («نسخه‌بندیِ قالب»): هر Save (ایجاد/ویرایش/
        // بازگردانی) یک Snapshot تازه در TblCardTemplateVersion می‌سازد
        // (نگاه کنید CardTemplateRepository.Save/SaveVersionSnapshot).
        // هیچ نسخه‌ای هرگز حذف/بازنویسی نمی‌شود. این تب فقط خواندن/
        // بازگردانی/مقایسه است.
        private void BuildVersionHistorySection(Panel content)
        {
            Label hint = new Label
            {
                Text = "هر بار «ذخیرهٔ قالب» یک نسخهٔ تازه می‌سازد. برایِ مقایسه، دقیقاً دو نسخه را انتخاب کن.",
                Dock = DockStyle.Top, Height = 38, ForeColor = UiTheme.TextMuted, Font = UiTheme.Font(8.5F),
                TextAlign = ContentAlignment.MiddleRight
            };
            content.Controls.Add(hint);

            _lstVersions = new ListBox
            {
                Dock = DockStyle.Top, Height = 220, RightToLeft = RightToLeft.Yes,
                SelectionMode = SelectionMode.MultiExtended, BorderStyle = BorderStyle.FixedSingle, Font = UiTheme.Font(9.5F)
            };
            _lstVersions.SelectedIndexChanged += delegate { UpdateVersionButtonsState(); };
            content.Controls.Add(_lstVersions);

            Panel actionsRow = new Panel { Dock = DockStyle.Top, Height = 32, Margin = new Padding(0, 8, 0, 0) };
            _btnRestoreVersion = UiTheme.CreateSecondaryButton("بازگردانیِ این نسخه", "↺");
            _btnRestoreVersion.Size = new Size(160, 28);
            _btnRestoreVersion.Enabled = false;
            _btnRestoreVersion.Click += delegate { RestoreSelectedVersion(); };

            _btnCompareVersions = UiTheme.CreateSecondaryButton("مقایسهٔ دو نسخه", "⇄");
            _btnCompareVersions.Size = new Size(150, 28);
            _btnCompareVersions.Margin = new Padding(0, 0, 8, 0);
            _btnCompareVersions.Enabled = false;
            _btnCompareVersions.Click += delegate { CompareSelectedVersions(); };

            _btnRestoreVersion.Dock = DockStyle.Right;
            _btnCompareVersions.Dock = DockStyle.Right;
            actionsRow.Controls.Add(_btnRestoreVersion);
            actionsRow.Controls.Add(_btnCompareVersions);
            content.Controls.Add(actionsRow);
        }

        private void UpdateVersionButtonsState()
        {
            if (_btnRestoreVersion == null) return;
            _btnRestoreVersion.Enabled = _lstVersions.SelectedIndices.Count == 1;
            _btnCompareVersions.Enabled = _lstVersions.SelectedIndices.Count == 2;
        }

        private void LoadVersionHistory()
        {
            if (_lstVersions == null) return;
            _lstVersions.Items.Clear();
            _currentVersions.Clear();
            if (_currentTemplateId <= 0) return;

            try
            {
                _currentVersions = _repo.GetVersions(_currentTemplateId);
                foreach (CardTemplateVersion v in _currentVersions)
                {
                    string note = string.IsNullOrWhiteSpace(v.ChangeNote) ? "" : " — " + v.ChangeNote;
                    _lstVersions.Items.Add("نسخهٔ " + v.VersionNumber + " — " +
                        PersianDateHelper.ToPersianDateTimeStringSafe(v.ChangedAt, "تاریخ نامعلوم") +
                        " — " + (string.IsNullOrWhiteSpace(v.ChangedByUsername) ? "نامعلوم" : v.ChangedByUsername) + note);
                }
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاریِ تاریخچهٔ نسخه‌ها: " + ex.Message);
            }
            UpdateVersionButtonsState();
        }

        private void RestoreSelectedVersion()
        {
            if (_lstVersions.SelectedIndices.Count != 1 || _currentTemplateId <= 0) return;
            if (!CaseManagement.Enterprise.PermissionService.Require("GuardianCard.Template.Edit", "CardTemplate", _currentTemplateId))
            {
                Msg.Show("مجوزِ ویرایشِ قالب را نداری.");
                return;
            }

            CardTemplateVersion v = _currentVersions[_lstVersions.SelectedIndices[0]];
            DialogResult confirm = MessageBox.Show(
                "قالب به نسخهٔ " + v.VersionNumber + " بازگردانده شود؟ (نسخهٔ فعلی از بین نمی‌رود — به‌عنوانِ یک نسخهٔ تازه ثبت می‌شود)",
                "تأیید بازگردانی", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            if (confirm != DialogResult.Yes) return;

            try
            {
                _repo.RestoreVersion(_currentTemplateId, v.VersionID);
                AuditLogger.Log("بازگردانی", "CardTemplate", _currentTemplateId, "", "بازگردانی به نسخهٔ " + v.VersionNumber);
                UiTheme.ShowSuccess(this, "قالب به نسخهٔ " + v.VersionNumber + " بازگردانده شد.");
                LoadTemplateList();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بازگردانیِ نسخه: " + ex.Message);
            }
        }

        // آموزش — به‌جای یک موتورِ diff عمومی (که یک وابستگیِ تازه/پیچیدگیِ
        // اضافه می‌سازد)، فقط پراپرتی‌هایِ شناخته‌شدهٔ Fields/Design به‌صورتِ
        // دستی مقایسه می‌شوند — دقیقاً همان چیزی که «Compare versions»
        // برایِ این پروژه لازم دارد، بدونِ ساختنِ یک موتورِ قالبِ دوم.
        private void CompareSelectedVersions()
        {
            if (_lstVersions.SelectedIndices.Count != 2) return;

            CardTemplateVersion a = _currentVersions[_lstVersions.SelectedIndices[0]];
            CardTemplateVersion b = _currentVersions[_lstVersions.SelectedIndices[1]];
            if (a.VersionNumber > b.VersionNumber) { var t = a; a = b; b = t; }

            var lines = new List<string>();
            lines.Add("مقایسهٔ نسخهٔ " + a.VersionNumber + " ← نسخهٔ " + b.VersionNumber);
            lines.Add("");
            CompareText(lines, "نام", a.Name, b.Name);
            CompareText(lines, "نوعِ طرح", a.LayoutVariant, b.LayoutVariant);
            CompareText(lines, "نوعِ کارت", a.TemplateType, b.TemplateType);
            CompareText(lines, "توضیحات", a.Description, b.Description);

            var allFieldKeys = new HashSet<string>(a.Fields.Keys);
            allFieldKeys.UnionWith(b.Fields.Keys);
            foreach (string key in allFieldKeys)
            {
                bool av = a.Fields.ContainsKey(key) && a.Fields[key];
                bool bv = b.Fields.ContainsKey(key) && b.Fields[key];
                if (av != bv)
                    lines.Add("فیلدِ «" + key + "»: " + (av ? "روشن" : "خاموش") + " → " + (bv ? "روشن" : "خاموش"));
            }

            CompareText(lines, "رنگِ اصلی", a.Design.PrimaryColor, b.Design.PrimaryColor);
            CompareText(lines, "رنگِ فرعی", a.Design.SecondaryColor, b.Design.SecondaryColor);
            CompareText(lines, "فونت", a.Design.FontFamily, b.Design.FontFamily);
            CompareNumber(lines, "اندازهٔ فونت (٪)", a.Design.FontScalePercent, b.Design.FontScalePercent);
            CompareText(lines, "ترتیبِ فیلدها", a.Design.FieldOrderCsv, b.Design.FieldOrderCsv);
            CompareText(lines, "موقعیتِ عکس", a.Design.PhotoPosition, b.Design.PhotoPosition);
            CompareText(lines, "ترتیبِ نوارِ امنیتی", a.Design.SecurityBandOrderCsv, b.Design.SecurityBandOrderCsv);

            if (lines.Count == 2)
                lines.Add("(هیچ تفاوتی پیدا نشد.)");

            using (var frm = new Form
            {
                Text = "مقایسهٔ نسخه‌ها", Size = new Size(560, 480), StartPosition = FormStartPosition.CenterParent,
                RightToLeft = RightToLeft.Yes, RightToLeftLayout = true, MinimizeBox = false, MaximizeBox = false
            })
            {
                var txt = new TextBox
                {
                    Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                    RightToLeft = RightToLeft.Yes, Text = string.Join(Environment.NewLine, lines)
                };
                frm.Controls.Add(txt);
                frm.ShowDialog(this);
            }
        }

        private static void CompareText(List<string> lines, string label, string a, string b)
        {
            string av = a ?? "";
            string bv = b ?? "";
            if (av != bv)
                lines.Add(label + ": «" + av + "» → «" + bv + "»");
        }

        private static void CompareNumber(List<string> lines, string label, int a, int b)
        {
            if (a != b)
                lines.Add(label + ": " + a + " → " + b);
        }

        // آموزش — نگاشتِ برچسبِ فارسیِ ترازِ نمایشی به مقدارِ واقعیِ CSS
        // (و برعکس) — «پیش‌فرض» همیشه رشتهٔ خالی است.
        private static readonly string[] AlignmentCssValues = { "", "right", "center", "left" };
        private static readonly string[] WeightCssValues = { "", "400", "500", "600", "700" };

        private static int IndexOfOrZero(string[] values, string value)
        {
            int idx = Array.IndexOf(values, value ?? "");
            return idx >= 0 ? idx : 0;
        }

        // آموزش — قبل از رفتن به فیلدِ دیگر، مقدارِ فعلیِ سه کنترل را در
        // _textOverrides[کلیدِ قبلی] ذخیره می‌کند؛ بعد مقدارِ فیلدِ تازه‌انتخاب‌
        // شده را از _textOverrides (یا پیش‌فرض) در کنترل‌ها می‌ریزد.
        private void SwitchTextOverrideField()
        {
            if (!string.IsNullOrEmpty(_currentTextOverrideKey))
                CommitTextOverrideEditor(_currentTextOverrideKey);

            int idx = _cmbTextOverrideField.SelectedIndex;
            if (idx < 0 || idx >= TextOverrideFieldKeys.Length) return;
            string key = TextOverrideFieldKeys[idx];
            _currentTextOverrideKey = key;

            TextFieldOverride o;
            _textOverrides.TryGetValue(key, out o);
            o = o ?? new TextFieldOverride();

            bool contentLocked = TextOverrideContentLocked.Contains(key);
            _txtTextOverrideContent.Enabled = !contentLocked;
            _txtTextOverrideContent.Text = contentLocked ? "" : (o.Content ?? "");

            SetSwatchColor(_pnlTextOverrideColor, ParseColorOrDefault(o.Color, Color.White));
            _numTextOverrideScale.Value = Math.Max(50, Math.Min(300, o.FontSizePercent == 0 ? 100 : o.FontSizePercent));
            _cmbTextOverrideFont.Text = o.FontFamily ?? "";
            _numTextOverrideLineHeight.Value = Math.Max(50, Math.Min(300, o.LineHeightPercent == 0 ? 100 : o.LineHeightPercent));
            _cmbTextOverrideAlignment.SelectedIndex = IndexOfOrZero(AlignmentCssValues, o.Alignment);
            _cmbTextOverrideWeight.SelectedIndex = IndexOfOrZero(WeightCssValues, o.FontWeight);
        }

        private void CommitTextOverrideEditor(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            int alignIdx = _cmbTextOverrideAlignment.SelectedIndex;
            int weightIdx = _cmbTextOverrideWeight.SelectedIndex;

            // آموزش — عمداً *همان آبجکت* به‌روزرسانی می‌شود، نه یک آبجکتِ تازه:
            // کارت‌های «مورد» (FieldCard) به همین نمونه ارجاع نگه می‌دارند، و
            // اگر اینجا با یک نمونهٔ نو جایگزین شود، کارت به آبجکتی جدا از
            // دیکشنری وصل می‌ماند و ویرایش‌های بعدیِ کاربر بی‌صدا از بین
            // می‌رود. مقدارِ نهایی دقیقاً مثلِ قبل است.
            TextFieldOverride o;
            if (!_textOverrides.TryGetValue(key, out o) || o == null)
            {
                o = new TextFieldOverride();
                _textOverrides[key] = o;
            }

            o.Content = TextOverrideContentLocked.Contains(key) ? "" : _txtTextOverrideContent.Text.Trim();
            o.Color = ColorToHex(_pnlTextOverrideColor.BackColor, Color.White);
            o.FontSizePercent = (int)_numTextOverrideScale.Value;
            o.FontFamily = _cmbTextOverrideFont.Text.Trim();
            o.LineHeightPercent = (int)_numTextOverrideLineHeight.Value;
            o.Alignment = alignIdx >= 0 && alignIdx < AlignmentCssValues.Length ? AlignmentCssValues[alignIdx] : "";
            o.FontWeight = weightIdx >= 0 && weightIdx < WeightCssValues.Length ? WeightCssValues[weightIdx] : "";
        }

        // یک ردیفِ ثابت‌الارتفاع: برچسب (راست) + یک کنترلِ مقدار با عرضِ ثابت.
        // آموزش — labelWidth از ۱۴۰ به ۱۶۰ رفت: در ستونِ باریکِ تنظیمات،
        // برچسب‌های بلند («لوگو/پس‌زمینهٔ روی کارت»، «اندازهٔ عکس گرد هدر»)
        // به خطِ دوم می‌شکستند و از ارتفاعِ ۳۰ پیکسلیِ ردیف بیرون می‌زدند.
        private static Panel MakeRow(Panel parent, string label, Control valueControl, int height = 30, int labelWidth = 160, int controlWidth = 220)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = height, Margin = new Padding(0, 0, 0, 8) };
            Label lbl = new Label
            {
                Text = label, Dock = DockStyle.Right, Width = labelWidth,
                TextAlign = ContentAlignment.MiddleRight, ForeColor = UiTheme.TextMuted
            };
            valueControl.Dock = DockStyle.Right;
            valueControl.Width = controlWidth;
            row.Controls.Add(valueControl);
            row.Controls.Add(lbl);
            parent.Controls.Add(row);

            // آموزش — دو رفعِ چیدمانِ ستونِ باریکِ تنظیمات (Card Designer
            // Phase 1):
            //
            // ۱) ترتیب: در Dock=Top بالاترین z-index بالاترین جایگاه را
            //    می‌گیرد، پس ردیف‌ها برعکسِ ترتیبِ افزودن دیده می‌شدند
            //    («نام قالب» ته فهرست می‌افتاد). BringToFront هر ردیفِ تازه
            //    را به ایندکسِ پایین‌تر می‌بَرد، یعنی ترتیبِ افزودن = ترتیبِ
            //    بالا به پایین.
            //
            // ۲) هم‌پوشانی: عرضِ ثابتِ برچسب+کنترل (تا ۴۶۰px) از عرضِ ستونِ
            //    تنظیمات بیشتر می‌شد و روی هم می‌افتادند. حالا عرضِ کنترل با
            //    عرضِ واقعیِ ردیف تنظیم می‌شود — روی ستونِ پهن همان اندازهٔ
            //    طراحی‌شده، روی ستونِ باریک کوچک‌تر ولی هرگز هم‌پوشان.
            row.BringToFront();

            EventHandler fit = delegate
            {
                int available = row.ClientSize.Width - labelWidth - 8;
                if (available < 60) available = 60;
                valueControl.Width = Math.Min(controlWidth, available);
            };
            row.SizeChanged += fit;
            fit(row, EventArgs.Empty);

            return row;
        }

        // ورودیِ عددی فشرده با دکمهٔ +/- کنارش.
        private Control MakeStepper(NumericUpDown nud)
        {
            Panel wrap = new Panel { Height = 28 };
            nud.Dock = DockStyle.Fill;
            nud.TextAlign = HorizontalAlignment.Center;
            nud.ValueChanged += delegate { SchedulePreviewRefresh(); };

            Button btnMinus = UiTheme.CreateSecondaryButton("−", "");
            btnMinus.Dock = DockStyle.Left; btnMinus.Width = 26;
            btnMinus.Click += delegate { if (nud.Value > nud.Minimum) nud.Value -= 1; };

            Button btnPlus = UiTheme.CreateSecondaryButton("+", "");
            btnPlus.Dock = DockStyle.Right; btnPlus.Width = 26;
            btnPlus.Click += delegate { if (nud.Value < nud.Maximum) nud.Value += 1; };

            wrap.Controls.Add(nud);
            wrap.Controls.Add(btnPlus);
            wrap.Controls.Add(btnMinus);
            return wrap;
        }

        // پیل رنگی: مربعِ رنگ (چپِ پیل) + کدِ Hex (راستِ پیل). کلیک روی هر
        // جای پیل، ColorDialog را باز می‌کند.
        private Panel MakeColorSwatch(out Panel swatch)
        {
            Panel wrap = new Panel { Height = 28, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand, BackColor = Color.White };

            Panel swatchLocal = new Panel { BackColor = Color.White, Dock = DockStyle.Left, Width = 28 };
            Label hexLabel = new Label
            {
                Text = "#FFFFFF", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(9F), ForeColor = UiTheme.TextMuted, Padding = new Padding(0, 0, 8, 0)
            };
            swatchLocal.Tag = hexLabel;

            EventHandler openPicker = delegate
            {
                using (var dlg = new ColorDialog { Color = swatchLocal.BackColor })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        SetSwatchColor(swatchLocal, dlg.Color);
                }
            };
            swatchLocal.Click += openPicker;
            hexLabel.Click += openPicker;
            wrap.Click += openPicker;

            wrap.Controls.Add(hexLabel);
            wrap.Controls.Add(swatchLocal);
            swatch = swatchLocal;
            return wrap;
        }

        // رنگِ یک پیل را (BackColor + برچسبِ Hex + پیش‌نمایشِ زنده) هم‌زمان
        // به‌روز می‌کند — نقطهٔ واحدِ تغییرِ رنگ برای همهٔ فراخوان‌ها.
        private void SetSwatchColor(Panel swatchLocal, Color c)
        {
            swatchLocal.BackColor = c;
            Label hex = swatchLocal.Tag as Label;
            if (hex != null) hex.Text = ColorTranslator.ToHtml(c);
            SchedulePreviewRefresh();
        }

        // برچسب (خارج) + textbox (فقط‌خواندنیِ مسیر) + دکمهٔ انتخاب/حذف.
        private Panel MakeImagePicker(out TextBox txt)
        {
            Panel wrap = new Panel { Height = 28 };

            Button btnClear = UiTheme.CreateSecondaryButton("حذف", "");
            btnClear.Size = new Size(60, 26);
            btnClear.Dock = DockStyle.Left;

            Button btnBrowse = UiTheme.CreateSecondaryButton("انتخاب", "");
            btnBrowse.Size = new Size(70, 26);
            btnBrowse.Dock = DockStyle.Left;
            btnBrowse.Margin = new Padding(0, 0, 4, 0);

            TextBox txtLocal = new TextBox { RightToLeft = RightToLeft.Yes, ReadOnly = true, Dock = DockStyle.Fill };
            txtLocal.TextChanged += delegate { SchedulePreviewRefresh(); };

            btnBrowse.Click += delegate
            {
                using (var ofd = new OpenFileDialog { Filter = "تصویر|*.png;*.jpg;*.jpeg;*.webp;*.bmp" })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                        txtLocal.Text = ofd.FileName;
                }
            };
            btnClear.Click += delegate { txtLocal.Text = ""; };

            wrap.Controls.Add(txtLocal);
            wrap.Controls.Add(btnClear);
            wrap.Controls.Add(btnBrowse);

            txt = txtLocal;
            return wrap;
        }

        // ─── رسمِ ردیف‌های لیستِ قالب‌ها — نامِ قالب + برچسبِ پیش‌فرض + تیکِ
        // سبزِ گزینه‌ی انتخاب‌شده ─────────────────────────────────────────────
        private void LstTemplates_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            ListBox box = (ListBox)sender;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Rectangle bounds = e.Bounds;

            using (Brush back = new SolidBrush(selected ? UiTheme.HoverTint : Color.White))
                e.Graphics.FillRectangle(back, bounds);

            if (selected)
            {
                Rectangle badge = new Rectangle(bounds.Left + 8, bounds.Top + (bounds.Height - 20) / 2, 20, 20);
                using (Brush gb = new SolidBrush(UiTheme.Success)) e.Graphics.FillEllipse(gb, badge);
                TextRenderer.DrawText(e.Graphics, "✓", UiTheme.FontBold(9F), badge, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            CardTemplate t = e.Index < _templates.Count ? _templates[e.Index] : null;
            string name = t != null ? t.Name : box.Items[e.Index].ToString();
            Rectangle textRect = new Rectangle(bounds.Left + 36, bounds.Top, bounds.Width - 44, bounds.Height - 16);
            TextRenderer.DrawText(e.Graphics, name, UiTheme.FontBold(10F), textRect, UiTheme.TextDark,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (t != null && t.IsDefault)
            {
                Rectangle tagRect = new Rectangle(bounds.Left + 36, bounds.Bottom - 20, bounds.Width - 44, 18);
                TextRenderer.DrawText(e.Graphics, "پیش‌فرض", UiTheme.Font(8.5F), tagRect, UiTheme.Primary,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            using (Pen p = new Pen(UiTheme.Border))
                e.Graphics.DrawLine(p, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }

        // ─── رسمِ ردیف‌های چک‌لیستِ فیلدها — دستگیرهٔ تزئینیِ ⋮⋮ (بدونِ قابلیتِ
        // واقعیِ جابه‌جایی — طبقِ توافقِ صریح دربارهٔ drag-and-drop: فقط
        // سازمان‌دهیِ نمایشی، نه ترتیبِ واقعیِ فیلد روی کارتِ چاپی)، برچسب،
        // و چک‌باکس ───────────────────────────────────────────────────────
        private void ChkFields_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            CheckedListBox box = (CheckedListBox)sender;
            Rectangle bounds = e.Bounds;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (Brush back = new SolidBrush(selected ? UiTheme.HoverTint : Color.White))
                e.Graphics.FillRectangle(back, bounds);

            using (Font dotFont = UiTheme.Font(11F))
                TextRenderer.DrawText(e.Graphics, "⋮⋮", dotFont, new Rectangle(bounds.Left + 4, bounds.Top, 22, bounds.Height),
                    UiTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            bool isChecked = box.GetItemChecked(e.Index);
            Rectangle checkRect = new Rectangle(bounds.Right - 26, bounds.Top + (bounds.Height - 16) / 2, 16, 16);
            ControlPaint.DrawCheckBox(e.Graphics, checkRect, isChecked ? ButtonState.Checked : ButtonState.Normal);

            string text = box.Items[e.Index].ToString();
            Rectangle textRect = new Rectangle(bounds.Left + 30, bounds.Top, bounds.Width - 62, bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, box.Font, textRect, UiTheme.TextDark,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            using (Pen p = new Pen(UiTheme.Border))
                e.Graphics.DrawLine(p, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }

        // ─────────────────────────────────────────────────────────────────
        // پیش‌نمایشِ زندهٔ WebView2 — همان مسیرِ رندرِ واقعیِ
        // FrmGuardianCardPreview.RenderDataAsync، فقط embed‌شده و به‌جای یک
        // بار (روی درخواستِ کاربر)، با هر تغییرِ تنظیم (debounce شده) دوباره
        // اجرا می‌شود. هیچ‌چیزی اینجا در دیتابیس ذخیره نمی‌شود.
        // ─────────────────────────────────────────────────────────────────

        private async void InitPreviewAsync()
        {
            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CaseManagement", "WebView2UserData");
                Directory.CreateDirectory(userDataFolder);

                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webViewPreview.EnsureCoreWebView2Async(env);
                _previewReady = true;
                SchedulePreviewRefresh();
            }
            catch
            {
                // آموزش — بی‌صدا: اگر WebView2 Runtime نصب نباشد، پیش‌نمایشِ
                // زنده غیرفعال می‌ماند ولی بقیهٔ فرم (ذخیره/تنظیمات) کار
                // می‌کند؛ دکمهٔ «پیش‌نمایش» پایین همان پیامِ نصبِ Runtime را
                // از قبل نشان می‌دهد (نگاه کنید FrmGuardianCardPreview).
                ShowPreviewStatus("پیش‌نمایشِ زنده در دسترس نیست (WebView2 Runtime نصب نیست).");
            }
        }

        private void SchedulePreviewRefresh()
        {
            if (_previewTimer == null) return;
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        private void ShowPreviewStatus(string text)
        {
            if (_lblPreviewStatus == null) return;
            _lblPreviewStatus.Text = text;
            _lblPreviewStatus.Visible = true;
            if (_webViewPreview != null) _webViewPreview.Visible = false;
        }

        private void HidePreviewStatus()
        {
            if (_lblPreviewStatus == null) return;
            _lblPreviewStatus.Visible = false;
            if (_webViewPreview != null) _webViewPreview.Visible = true;
        }

        private async void RefreshPreviewNowAsync()
        {
            if (!_previewReady || _webViewPreview.CoreWebView2 == null) return;
            if (_txtName == null) return; // هنوز UI کامل ساخته نشده

            try
            {
                // آموزش — پیش‌نمایش دیگر هرگز خالی نمی‌ماند: اگر پروندهٔ
                // انتخاب‌شده/آخرین پروندهٔ در دسترس نبود، دادهٔ نمونهٔ داخلی
                // استفاده می‌شود. نگاه کنید ResolvePreviewData.
                bool usedDemo;
                GuardianCardData data = ResolvePreviewData(out usedDemo);
                UpdatePreviewRecordLabel(data, usedDemo);

                string originalPhotoPath = data.Photo;
                string originalLogoPath = data.Logo;
                string originalSignaturePath = data.Signature;
                string originalStampPath = data.Stamp;

                var template = new CardTemplate
                {
                    LayoutVariant = CollectLayoutVariant(),
                    Fields = CollectFields(),
                    Design = CollectDesign()
                };

                var disabledFields = new List<string>();
                if (template.LayoutVariant == "Simple")
                {
                    foreach (string field in CardTemplateRepository.ToggleableFieldsSimple)
                        if (!CardTemplateRepository.IsFieldEnabled(template, field))
                            disabledFields.Add(field);
                }
                else
                {
                    CardTemplateRepository.ApplyTextFields(data, template);
                    if (!CardTemplateRepository.IsFieldEnabled(template, "Logo")) originalLogoPath = "";
                    if (!CardTemplateRepository.IsFieldEnabled(template, "Signature")) originalSignaturePath = "";
                    if (!CardTemplateRepository.IsFieldEnabled(template, "Stamp")) originalStampPath = "";

                    foreach (string field in CardTemplateRepository.ToggleableFields)
                        if (!CardTemplateRepository.IsFieldEnabled(template, field))
                            disabledFields.Add(field);

                    if (!template.Design.ShowQRCode) disabledFields.Add("QRCode");
                    if (!template.Design.ShowBarcode) disabledFields.Add("Barcode");
                    if (!template.Design.HologramEnabled) disabledFields.Add("Hologram");
                }

                var renderer = new GuardianCardRenderer();
                string workingFolder = renderer.StageAndPopulate(
                    data, originalPhotoPath, originalLogoPath, originalSignaturePath, originalStampPath,
                    disabledFields, template.Design, template.LayoutVariant);

                HidePreviewStatus();

                _webViewPreview.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    GuardianCardRenderer.VirtualHostName, workingFolder, CoreWebView2HostResourceAccessKind.Allow);

                string page = template.LayoutVariant == "Simple" ? "simple.html" : "index.html";
                _webViewPreview.CoreWebView2.Navigate("https://" + GuardianCardRenderer.VirtualHostName + "/" + page);
            }
            catch (Exception ex)
            {
                ShowPreviewStatus("خطا در ساختِ پیش‌نمایش:\n" + ex.Message);
            }
        }

        // آموزش — «چاپِ آزمایشی»: به‌جای بازکردنِ یک پنجرهٔ جداگانه، همان
        // پیش‌نمایشِ زنده‌ای که همین حالا روی صفحه است مستقیماً چاپ می‌شود —
        // دقیقاً همان مکانیزمِ ShowPrintUI در FrmGuardianCardPreview.
        private void TestPrintFromLivePreview()
        {
            if (!_previewReady || _webViewPreview.CoreWebView2 == null)
            {
                Msg.Show("پیش‌نمایشِ زنده هنوز آماده نیست.");
                return;
            }
            try
            {
                _webViewPreview.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در چاپ: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // بارگذاری/ذخیره
        // ─────────────────────────────────────────────────────────────────

        private void LoadTemplateList()
        {
            LoadTemplateList(_txtSearchTemplates != null ? _txtSearchTemplates.Text : "");
        }

        // آموزش — جعبهٔ جستجوی لیستِ قالب‌ها. فیلترِ خالی رفتارِ قبلی را عیناً
        // حفظ می‌کند. وقتی فیلتر خالی نیست و هیچ نتیجه‌ای پیدا نشد، ویرایشگر
        // دست‌نخورده می‌ماند (StartNewTemplate صدا زده نمی‌شود) — چون کاربر
        // فقط دارد جستجو می‌کند، نه می‌خواهد فرم پاک شود.
        private void LoadTemplateList(string filter)
        {
            // آموزش — دفاعِ لایه‌ای (Phase 2): دروازهٔ کلیِ فرم همین حالا هم
            // GuardianCard.ManageTemplates را در سازنده چک می‌کند؛ این فقط
            // یک لایهٔ دقیق‌ترِ اضافه است، بدونِ پیامِ مزاحمِ تکراری (این متد
            // بارها فراخوانی می‌شود) — رد شدن یعنی فهرست خالی می‌ماند.
            if (!CaseManagement.Enterprise.PermissionService.HasPermission("GuardianCard.Template.View"))
            {
                _lstTemplates.Items.Clear();
                _templates = new List<CardTemplate>();
                return;
            }

            var all = _repo.GetAll();
            _templates = string.IsNullOrWhiteSpace(filter)
                ? all
                : all.FindAll(t => t.Name.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

            _lstTemplates.Items.Clear();
            foreach (var t in _templates)
                _lstTemplates.Items.Add(t.Name);

            if (_lstTemplates.Items.Count > 0)
                _lstTemplates.SelectedIndex = 0;
            else if (string.IsNullOrWhiteSpace(filter))
                StartNewTemplate();
        }

        private void LoadSelectedIntoEditor()
        {
            int idx = _lstTemplates.SelectedIndex;
            if (idx < 0 || idx >= _templates.Count) return;
            ApplyTemplateToEditor(_templates[idx]);
        }

        // آموزش — لیستِ چک‌باکس‌ها را بر اساسِ نوعِ طرحِ انتخاب‌شده (کامل/ساده)
        // از نو می‌سازد؛ fields=null یعنی «همه روشن» (قالبِ تازه یا تعویضِ
        // دستیِ نوعِ طرح)، وگرنه وضعیتِ واقعیِ قالب اعمال می‌شود.
        private void PopulateFieldChecklist(Dictionary<string, bool> fields)
        {
            bool isSimple = (_cmbLayoutVariant.SelectedItem as string) == LayoutSimple;
            string[] fieldNames = isSimple ? CardTemplateRepository.ToggleableFieldsSimple : CardTemplateRepository.ToggleableFields;
            Dictionary<string, string> labels = isSimple ? FieldLabelsSimple : FieldLabels;

            _chkFields.Items.Clear();
            for (int i = 0; i < fieldNames.Length; i++)
            {
                string field = fieldNames[i];
                bool enabled = fields == null || !fields.TryGetValue(field, out bool v) || v;
                _chkFields.Items.Add(labels.ContainsKey(field) ? labels[field] : field, enabled);
            }
        }

        private void ApplyTemplateToEditor(CardTemplate t)
        {
            _currentTemplateId = t.TemplateID;
            _txtName.Text = t.Name;
            _lblDefaultTag.Text = t.IsDefault ? "این قالبِ پیش‌فرض است (حذف نمی‌شود)" : "";

            _cmbTemplateType.Text = t.TemplateType ?? "";
            _txtDescription.Text = t.Description ?? "";
            _suppressActiveToggle = true;
            _chkIsActive.Checked = t.IsActive;
            _suppressActiveToggle = false;
            _lblMetaInfo.Text = BuildMetaInfoText(t);
            LoadVersionHistory();

            _cmbLayoutVariant.SelectedItem = t.LayoutVariant == "Simple" ? LayoutSimple : LayoutFull;
            PopulateFieldChecklist(t.Fields);

            CardTemplateDesign d = t.Design ?? new CardTemplateDesign();
            PopulateFieldOrderLists(d);
            SetSwatchColor(_pnlPrimaryColor, ParseColorOrDefault(d.PrimaryColor, Color.White));
            SetSwatchColor(_pnlSecondaryColor, ParseColorOrDefault(d.SecondaryColor, Color.White));
            SetSwatchColor(_pnlBackgroundColor, ParseColorOrDefault(d.BackgroundColor, Color.White));
            SetSwatchColor(_pnlTextColor, ParseColorOrDefault(d.TextColor, Color.White));
            _numFontScale.Value = Math.Max(50, Math.Min(200, d.FontScalePercent == 0 ? 100 : d.FontScalePercent));
            _cmbFont.Text = d.FontFamily ?? "";
            _txtBgFront.Text = d.BackgroundFrontPath ?? "";
            _txtBgBack.Text = d.BackgroundBackPath ?? "";
            _txtWatermark.Text = d.WatermarkPath ?? "";
            _numWatermarkOpacity.Value = Math.Max(0, Math.Min(100, d.WatermarkOpacityPercent));
            _chkHologram.Checked = d.HologramEnabled;
            _chkQRCode.Checked = d.ShowQRCode;
            _chkBarcode.Checked = d.ShowBarcode;

            SetSwatchColor(_pnlHeaderBgColor, ParseColorOrDefault(d.HeaderBackgroundColor, Color.White));
            _numPortraitScale.Value = Math.Max(50, Math.Min(300, d.PortraitScalePercent == 0 ? 100 : d.PortraitScalePercent));
            _chkPortraitBlank.Checked = d.PortraitBlank;
            _numHeaderHeightScale.Value = Math.Max(30, Math.Min(200, d.HeaderHeightScalePercent == 0 ? 100 : d.HeaderHeightScalePercent));
            _numFamilyListMaxRows.Value = Math.Max(0, Math.Min(30, d.FamilyListMaxRows));
            _cmbFamilyPhotoRatio.SelectedItem = FamilyPhotoRatioLabelFor(d.FamilyPhotoAspectRatio);
            _numFamilyPhotoScale.Value = Math.Max(50, Math.Min(300, d.FamilyPhotoScalePercent == 0 ? 100 : d.FamilyPhotoScalePercent));
            _chkFamilyPhotoFitContain.Checked = d.FamilyPhotoFitContain;

            _textOverrides = new Dictionary<string, TextFieldOverride>(d.TextOverrides ?? new Dictionary<string, TextFieldOverride>());
            _currentTextOverrideKey = "";
            _cmbTextOverrideField.SelectedIndex = -1;
            _cmbTextOverrideField.SelectedIndex = 0;

            // آموزش — چون _textOverrides همین بالا با یک دیکشنریِ *تازه*
            // جایگزین شد، آبجکت‌هایی که کارت‌ها نگه داشته‌اند دیگر به
            // دیکشنریِ جاری وصل نیستند؛ پس کارت‌ها باید از نو ساخته شوند،
            // نه فقط همگام‌سازی.
            RebuildFieldCards();

            List<int> months = CardTemplateRepository.ParseLedgerMonths(d.LedgerMonthsCsv);
            for (int i = 0; i < _chkMonths.Items.Count; i++)
                _chkMonths.SetItemChecked(i, months.Count == 0 || months.Contains(i + 1));

            // آموزش — سلامتِ قالب بلافاصله بعد از بارگذاری محاسبه می‌شود، نه
            // فقط روی تیکِ تایمرِ پیش‌نمایش؛ وگرنه تا اولین تغییرِ کاربر،
            // پنلِ «سلامت قالب» خالی می‌ماند.
            RefreshHealth();
            SchedulePreviewRefresh();
        }

        private static Color ParseColorOrDefault(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try { return ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }

        private static string ColorToHex(Color c, Color unsetSentinel)
        {
            return c.ToArgb() == unsetSentinel.ToArgb() ? "" : ColorTranslator.ToHtml(c);
        }

        // آموزش — Phase 2: خلاصهٔ فقط‌خواندنیِ سازنده/ویرایش‌کننده — برایِ
        // رکوردهایِ قدیمی که این ستون‌ها را نداشتند (CreatedBy/ModifiedAt
        // خالی/null)، همان بخش‌ها ساکت حذف می‌شوند، نه اینکه «نامعلوم»
        // چاپ شود در جایی که اصلاً معنی ندارد.
        private static string BuildMetaInfoText(CardTemplate t)
        {
            var parts = new List<string>();
            if (t.CreatedAt.HasValue)
                parts.Add("ایجاد: " + PersianDateHelper.ToPersianDateTimeStringSafe(t.CreatedAt.Value, "نامعلوم") +
                    (string.IsNullOrWhiteSpace(t.CreatedBy) ? "" : " توسطِ " + t.CreatedBy));
            if (t.ModifiedAt.HasValue)
                parts.Add("آخرین ویرایش: " + PersianDateHelper.ToPersianDateTimeStringSafe(t.ModifiedAt.Value, "نامعلوم") +
                    (string.IsNullOrWhiteSpace(t.ModifiedBy) ? "" : " توسطِ " + t.ModifiedBy));
            return string.Join("   |   ", parts);
        }

        private void StartNewTemplate()
        {
            _currentTemplateId = 0;
            _txtName.Text = "";
            _lblDefaultTag.Text = "";
            _cmbTemplateType.Text = "";
            _txtDescription.Text = "";
            _suppressActiveToggle = true;
            _chkIsActive.Checked = true;
            _suppressActiveToggle = false;
            _lblMetaInfo.Text = "";
            LoadVersionHistory(); // _currentTemplateId=0 => فهرست خالی می‌شود
            _cmbLayoutVariant.SelectedItem = LayoutFull;
            PopulateFieldChecklist(null);

            ResetDesignControls();
            RebuildFieldCards();

            _txtName.Focus();
            SchedulePreviewRefresh();
        }

        // آموزش — «بازگردانی به پیش‌فرض» (به‌درخواستِ کاربر): فقط تنظیماتِ
        // ظاهری/طراحی را به مقدارِ پیش‌فرض برمی‌گرداند — نام/شناسه/نوعِ‌طرح/
        // فیلدهای روشن‌خاموش دست‌نخورده می‌مانند (کاربر باید بعد از این هنوز
        // «ذخیره» بزند تا واقعاً روی دیسک بنشیند).
        private void ResetDesignControls()
        {
            SetSwatchColor(_pnlPrimaryColor, Color.White);
            SetSwatchColor(_pnlSecondaryColor, Color.White);
            SetSwatchColor(_pnlBackgroundColor, Color.White);
            SetSwatchColor(_pnlTextColor, Color.White);
            _numFontScale.Value = 100;
            _cmbFont.Text = "";
            _txtBgFront.Text = "";
            _txtBgBack.Text = "";
            _txtWatermark.Text = "";
            _numWatermarkOpacity.Value = 15;
            _chkHologram.Checked = true;
            _chkQRCode.Checked = false;
            _chkBarcode.Checked = true;

            SetSwatchColor(_pnlHeaderBgColor, Color.White);
            _numPortraitScale.Value = 100;
            _chkPortraitBlank.Checked = false;
            _numHeaderHeightScale.Value = 100;
            _numFamilyListMaxRows.Value = 0;
            _cmbFamilyPhotoRatio.SelectedItem = FamilyPhotoRatioLabelFor("1:1");
            _numFamilyPhotoScale.Value = 100;
            _chkFamilyPhotoFitContain.Checked = false;
            _textOverrides = new Dictionary<string, TextFieldOverride>();
            _currentTextOverrideKey = "";
            _cmbTextOverrideField.SelectedIndex = -1;
            _cmbTextOverrideField.SelectedIndex = 0;
            PopulateFieldOrderLists(null);

            for (int i = 0; i < _chkMonths.Items.Count; i++)
                _chkMonths.SetItemChecked(i, true);
        }

        private void ResetToDefault()
        {
            DialogResult confirm = MessageBox.Show(
                "تمامِ تنظیماتِ ظاهری (رنگ/فونت/تصویر/متن‌ها) به پیش‌فرض برگردد؟ نام و فیلدهای روشن/خاموش دست‌نخورده می‌مانند.",
                "بازگردانی به پیش‌فرض", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            if (confirm != DialogResult.Yes) return;

            ResetDesignControls();
            SchedulePreviewRefresh();
        }

        private Dictionary<string, bool> CollectFields()
        {
            bool isSimple = (_cmbLayoutVariant.SelectedItem as string) == LayoutSimple;
            string[] fieldNames = isSimple ? CardTemplateRepository.ToggleableFieldsSimple : CardTemplateRepository.ToggleableFields;

            var fields = new Dictionary<string, bool>();
            for (int i = 0; i < fieldNames.Length; i++)
                fields[fieldNames[i]] = _chkFields.GetItemChecked(i);
            return fields;
        }

        private string CollectLayoutVariant()
        {
            return (_cmbLayoutVariant.SelectedItem as string) == LayoutSimple ? "Simple" : "Full";
        }

        private CardTemplateDesign CollectDesign()
        {
            var months = new List<int>();
            for (int i = 0; i < _chkMonths.Items.Count; i++)
                if (_chkMonths.GetItemChecked(i))
                    months.Add(i + 1);
            // آموزش — اگر همه‌ی ماه‌ها تیک دارند، CSV خالی ذخیره می‌شود (یعنی
            // «بدونِ فیلتر»، رفتارِ پیش‌فرض) — نه یک رشتهٔ ۱۲تایی؛ فقط
            // زیرمجموعه‌ی واقعی باعثِ فیلترشدن می‌شود.
            string csv = months.Count == 12 ? "" : CardTemplateRepository.BuildLedgerMonthsCsv(months);

            // آموزش — مقدارِ فعلاً روی صفحه (فیلدِ انتخاب‌شده در کمبوی
            // متن‌ها) هنوز در _textOverrides ذخیره نشده تا وقتی کاربر فیلد
            // را عوض کند؛ قبل از ساختنِ CardTemplateDesign باید کمیت شود.
            CommitTextOverrideEditor(_currentTextOverrideKey);

            return new CardTemplateDesign
            {
                PrimaryColor = ColorToHex(_pnlPrimaryColor.BackColor, Color.White),
                SecondaryColor = ColorToHex(_pnlSecondaryColor.BackColor, Color.White),
                BackgroundColor = ColorToHex(_pnlBackgroundColor.BackColor, Color.White),
                TextColor = ColorToHex(_pnlTextColor.BackColor, Color.White),
                FontScalePercent = (int)_numFontScale.Value,
                FontFamily = _cmbFont.Text.Trim(),
                BackgroundFrontPath = _txtBgFront.Text.Trim(),
                BackgroundBackPath = _txtBgBack.Text.Trim(),
                WatermarkPath = _txtWatermark.Text.Trim(),
                WatermarkOpacityPercent = (int)_numWatermarkOpacity.Value,
                HologramEnabled = _chkHologram.Checked,
                ShowQRCode = _chkQRCode.Checked,
                ShowBarcode = _chkBarcode.Checked,
                LedgerMonthsCsv = csv,
                HeaderBackgroundColor = ColorToHex(_pnlHeaderBgColor.BackColor, Color.White),
                PortraitScalePercent = (int)_numPortraitScale.Value,
                PortraitBlank = _chkPortraitBlank.Checked,
                HeaderHeightScalePercent = (int)_numHeaderHeightScale.Value,
                FamilyListMaxRows = (int)_numFamilyListMaxRows.Value,
                FamilyPhotoAspectRatio = FamilyPhotoRatioValueFor(_cmbFamilyPhotoRatio.SelectedItem as string),
                FamilyPhotoScalePercent = (int)_numFamilyPhotoScale.Value,
                FamilyPhotoFitContain = _chkFamilyPhotoFitContain.Checked,
                TextOverrides = new Dictionary<string, TextFieldOverride>(_textOverrides),
                FieldOrderCsv = CollectFieldOrder(),
                PhotoPosition = CollectPhotoPosition(),
                SecurityBandOrderCsv = CollectSecurityBandOrder()
            };
        }

        private void SaveCurrent()
        {
            string name = _txtName.Text.Trim();
            if (name.Length == 0)
            {
                Msg.Show("نام قالب را وارد کن.");
                return;
            }

            // آموزش — Phase 2: علاوه بر دروازهٔ کلیِ فرم (GuardianCard.
            // ManageTemplates، در سازنده)، اینجا مجوزِ دقیق‌ترِ ایجاد/ویرایش
            // هم چک می‌شود (دفاعِ لایه‌ای، هم‌الگویِ بقیهٔ فرم‌های برنامه).
            bool isNew = _currentTemplateId <= 0;
            string permKey = isNew ? "GuardianCard.Template.Create" : "GuardianCard.Template.Edit";
            if (!CaseManagement.Enterprise.PermissionService.Require(permKey, "CardTemplate", _currentTemplateId))
            {
                Msg.Show(isNew ? "مجوزِ ایجادِ قالبِ تازه را نداری." : "مجوزِ ویرایشِ این قالب را نداری.");
                return;
            }

            try
            {
                int savedId = _repo.Save(_currentTemplateId, name, CollectFields(), CollectDesign(), CollectLayoutVariant(),
                    _cmbTemplateType.Text.Trim(), _txtDescription.Text.Trim());

                AuditLogger.Log(isNew ? "ایجاد" : "ویرایش", "CardTemplate", savedId, "", "قالب «" + name + "» ذخیره شد.");

                _currentTemplateId = savedId;
                UiTheme.ShowSuccess(this, "قالب ذخیره شد.");
                LoadTemplateList();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ذخیره قالب (شاید نامش تکراری است): " + ex.Message);
            }
        }

        private void DeleteCurrent()
        {
            if (_currentTemplateId <= 0)
            {
                Msg.Show("اول یک قالب را از فهرست انتخاب کن.");
                return;
            }

            var t = _templates.Find(x => x.TemplateID == _currentTemplateId);
            if (t != null && t.IsDefault)
            {
                Msg.Show("قالبِ پیش‌فرض قابل حذف نیست.");
                return;
            }

            if (!CaseManagement.Enterprise.PermissionService.Require("GuardianCard.Template.Delete", "CardTemplate", _currentTemplateId))
            {
                Msg.Show("مجوزِ حذفِ قالب را نداری.");
                return;
            }

            DialogResult confirm = MessageBox.Show("این قالب حذف شود؟", "تأیید حذف",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            if (confirm != DialogResult.Yes) return;

            int deletedId = _currentTemplateId;
            string deletedName = t != null ? t.Name : "";
            _repo.Delete(deletedId);
            AuditLogger.Log("حذف", "CardTemplate", deletedId, "قالب «" + deletedName + "»", "");
            LoadTemplateList();
        }

        // آموزش — تکثیر: یک نسخهٔ کاملاً تازه (با نامِ متفاوت) از قالبِ
        // انتخاب‌شدهٔ فعلی می‌سازد — از همان مسیرِ Save عبور می‌کند، پس
        // قالبِ تازه نسخهٔ ۱ خودش را می‌گیرد.
        private void DuplicateCurrent()
        {
            if (_currentTemplateId <= 0)
            {
                Msg.Show("اول یک قالب را از فهرست انتخاب کن.");
                return;
            }

            if (!CaseManagement.Enterprise.PermissionService.Require("GuardianCard.Template.Create", "CardTemplate", _currentTemplateId))
            {
                Msg.Show("مجوزِ ایجادِ قالبِ تازه را نداری.");
                return;
            }

            var source = _templates.Find(x => x.TemplateID == _currentTemplateId);
            string baseName = source != null ? source.Name : _txtName.Text.Trim();
            string newName = baseName + " (کپی)";

            try
            {
                int newId = _repo.Duplicate(_currentTemplateId, newName);
                AuditLogger.Log("ایجاد", "CardTemplate", newId, "", "تکثیر از قالب #" + _currentTemplateId + " («" + baseName + "»)");
                UiTheme.ShowSuccess(this, "قالبِ «" + newName + "» ساخته شد.");
                LoadTemplateList();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در تکثیرِ قالب (شاید نامش تکراری است): " + ex.Message);
            }
        }

        // آموزش — فعال/غیرفعال‌سازی یک تغییرِ وضعیتِ سبک است (نه ویرایشِ
        // محتوا)، پس مجوزِ جداگانه (Activate) دارد و نسخهٔ تازه نمی‌سازد —
        // نگاه کنید CardTemplateRepository.SetActive.
        private void ToggleActiveState()
        {
            if (_suppressActiveToggle || _currentTemplateId <= 0) return;

            bool wantActive = _chkIsActive.Checked;
            if (!CaseManagement.Enterprise.PermissionService.Require("GuardianCard.Template.Activate", "CardTemplate", _currentTemplateId))
            {
                Msg.Show("مجوزِ فعال/غیرفعال‌سازیِ قالب را نداری.");
                _suppressActiveToggle = true;
                _chkIsActive.Checked = !wantActive;
                _suppressActiveToggle = false;
                return;
            }

            _repo.SetActive(_currentTemplateId, wantActive);
            AuditLogger.Log(wantActive ? "فعال‌سازی" : "غیرفعال‌سازی", "CardTemplate", _currentTemplateId, "", "");
            LoadTemplateList();
        }

        // ─── پیش‌نمایش — روی اولین پروندهٔ در دسترس (بخش محدودهٔ مرکز) ────────
        // آموزش — این همان پنجرهٔ کاملِ قدیمی است (رو-به-رو + دکمه‌های چاپ/
        // PDF واقعی) — برای وقتی که کاربر می‌خواهد پیش‌نمایش را در یک
        // پنجرهٔ بزرگ‌ترِ مستقل ببیند، جدا از پیش‌نمایشِ زندهٔ کوچکِ داخلِ
        // همین فرم. برای اینکه پیش‌نمایش «همان قالبِ در حالِ ویرایش» را نشان
        // دهد (نه نسخهٔ قبلاً ذخیره‌شده)، اول ذخیره می‌شود.
        private void PreviewCurrent()
        {
            SaveCurrent();
            if (_currentTemplateId <= 0) return;

            int caseId = FindAnyAccessibleCaseId();
            if (caseId <= 0)
            {
                Msg.Show("هیچ پرونده‌ای برای پیش‌نمایش پیدا نشد.");
                return;
            }

            using (var frm = new FrmGuardianCardPreview(caseId, _currentTemplateId))
                frm.ShowDialog(this);
        }

        // ─────────────────────────────────────────────────────────────────
        // منبعِ دادهٔ پیش‌نمایش
        // ─────────────────────────────────────────────────────────────────
        // ترتیبِ تلاش: (۱) پروندهٔ صریحاً انتخاب‌شده، (۲) آخرین پروندهٔ در
        // دسترس، (۳) دادهٔ نمونهٔ داخلی. مرحلهٔ ۳ تضمین می‌کند پیش‌نمایش
        // هیچ‌وقت خالی نماند — حتی روی نصبِ تازه بدونِ هیچ پرونده‌ای.
        private GuardianCardData ResolvePreviewData(out bool usedDemo)
        {
            usedDemo = false;

            if (!_previewUseDemo)
            {
                int caseId = _previewCaseId > 0 ? _previewCaseId : FindAnyAccessibleCaseId();
                if (caseId > 0)
                {
                    try
                    {
                        GuardianCardData real = new CardService().BuildCardData(caseId);
                        if (real != null)
                        {
                            _previewCaseId = caseId;
                            return real;
                        }
                    }
                    catch
                    {
                        // پروندهٔ خراب/حذف‌شده نباید طراح را از کار بیندازد —
                        // بی‌صدا می‌افتیم روی دادهٔ نمونه.
                    }
                }
            }

            usedDemo = true;
            return DemoCardData.Build();
        }

        private void UpdatePreviewRecordLabel(GuardianCardData data, bool usedDemo)
        {
            if (_lblPreviewRecord == null) return;

            if (usedDemo)
            {
                _lblPreviewRecord.Text = "دادهٔ نمونه (هیچ پروندهٔ واقعی انتخاب نشده)";
                _lblPreviewRecord.ForeColor = UiTheme.Warning;
            }
            else
            {
                string name = data == null || string.IsNullOrWhiteSpace(data.GuardianName)
                    ? "پروندهٔ " + _previewCaseId
                    : data.GuardianName;
                _lblPreviewRecord.Text = "نمایش برای: " + name;
                _lblPreviewRecord.ForeColor = UiTheme.TextMuted;
            }
        }

        private void PickPreviewRecord()
        {
            using (var picker = new FrmPreviewRecordPicker(_previewCaseId))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                _previewUseDemo = picker.UseDemoData;
                _previewCaseId = picker.SelectedCaseId;
            }
            SchedulePreviewRefresh();
        }

        private int FindAnyAccessibleCaseId()
        {
            int cid = SecurityContext.CenterFilterId;
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT CasID FROM TblCase WHERE IsArchived = 0 AND (@CID = 0 OR CenterID = @CID) ORDER BY CasID DESC LIMIT 1", con))
            {
                cmd.Parameters.AddWithValue("@CID", cid);
                con.Open();
                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        // ─── Export/Import — یک فایلِ JSON خودکفا (تصاویر base64) ────────────
        private void ExportCurrent()
        {
            if (_currentTemplateId <= 0)
            {
                Msg.Show("اول قالب را ذخیره کن.");
                return;
            }

            CardTemplate t = _repo.GetById(_currentTemplateId);
            if (t == null) return;

            using (var sfd = new SaveFileDialog { Filter = "قالبِ کارت|*.cardtemplate.json", FileName = t.Name + ".cardtemplate.json" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var export = new Dictionary<string, object>
                    {
                        { "Name", t.Name },
                        { "Fields", t.Fields },
                        { "LayoutVariant", t.LayoutVariant },
                        { "Design", new Dictionary<string, object>
                            {
                                { "PrimaryColor", t.Design.PrimaryColor },
                                { "SecondaryColor", t.Design.SecondaryColor },
                                { "BackgroundColor", t.Design.BackgroundColor },
                                { "TextColor", t.Design.TextColor },
                                { "FontScalePercent", t.Design.FontScalePercent },
                                { "FontFamily", t.Design.FontFamily },
                                { "WatermarkOpacityPercent", t.Design.WatermarkOpacityPercent },
                                { "HologramEnabled", t.Design.HologramEnabled },
                                { "ShowQRCode", t.Design.ShowQRCode },
                                { "ShowBarcode", t.Design.ShowBarcode },
                                { "LedgerMonthsCsv", t.Design.LedgerMonthsCsv },
                                { "HeaderBackgroundColor", t.Design.HeaderBackgroundColor },
                                { "PortraitScalePercent", t.Design.PortraitScalePercent },
                                { "PortraitBlank", t.Design.PortraitBlank },
                                { "TextOverrides", t.Design.TextOverrides },
                                { "BackgroundFrontImageBase64", EncodeImageFile(t.Design.BackgroundFrontPath) },
                                { "BackgroundBackImageBase64", EncodeImageFile(t.Design.BackgroundBackPath) },
                                { "WatermarkImageBase64", EncodeImageFile(t.Design.WatermarkPath) }
                            }
                        }
                    };

                    string json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(export);
                    File.WriteAllText(sfd.FileName, json, new System.Text.UTF8Encoding(false));
                    UiTheme.ShowSuccess(this, "قالب Export شد:\n" + sfd.FileName);
                }
                catch (Exception ex)
                {
                    Msg.Show("خطا در Export: " + ex.Message);
                }
            }
        }

        private void ImportTemplate()
        {
            using (var ofd = new OpenFileDialog { Filter = "قالبِ کارت|*.json" })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var raw = serializer.Deserialize<Dictionary<string, object>>(json);

                    string name = raw.ContainsKey("Name") ? raw["Name"].ToString() : Path.GetFileNameWithoutExtension(ofd.FileName);
                    name = MakeUniqueTemplateName(name);

                    var fieldsRaw = raw.ContainsKey("Fields")
                        ? serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(raw["Fields"]))
                        : new Dictionary<string, object>();
                    var fields = new Dictionary<string, bool>();
                    foreach (var kv in fieldsRaw) fields[kv.Key] = Convert.ToBoolean(kv.Value);

                    var designRaw = raw.ContainsKey("Design")
                        ? serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(raw["Design"]))
                        : new Dictionary<string, object>();

                    string importFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CaseManagement", "CardTemplateImports", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(importFolder);

                    var design = new CardTemplateDesign
                    {
                        PrimaryColor = GetStr(designRaw, "PrimaryColor"),
                        SecondaryColor = GetStr(designRaw, "SecondaryColor"),
                        BackgroundColor = GetStr(designRaw, "BackgroundColor"),
                        TextColor = GetStr(designRaw, "TextColor"),
                        FontScalePercent = designRaw.ContainsKey("FontScalePercent") ? Convert.ToInt32(designRaw["FontScalePercent"]) : 100,
                        FontFamily = GetStr(designRaw, "FontFamily"),
                        WatermarkOpacityPercent = designRaw.ContainsKey("WatermarkOpacityPercent") ? Convert.ToInt32(designRaw["WatermarkOpacityPercent"]) : 15,
                        HologramEnabled = !designRaw.ContainsKey("HologramEnabled") || Convert.ToBoolean(designRaw["HologramEnabled"]),
                        ShowQRCode = designRaw.ContainsKey("ShowQRCode") && Convert.ToBoolean(designRaw["ShowQRCode"]),
                        ShowBarcode = !designRaw.ContainsKey("ShowBarcode") || Convert.ToBoolean(designRaw["ShowBarcode"]),
                        LedgerMonthsCsv = GetStr(designRaw, "LedgerMonthsCsv"),
                        HeaderBackgroundColor = GetStr(designRaw, "HeaderBackgroundColor"),
                        PortraitScalePercent = designRaw.ContainsKey("PortraitScalePercent") ? Convert.ToInt32(designRaw["PortraitScalePercent"]) : 100,
                        PortraitBlank = designRaw.ContainsKey("PortraitBlank") && Convert.ToBoolean(designRaw["PortraitBlank"]),
                        TextOverrides = ParseTextOverrides(designRaw, serializer),
                        BackgroundFrontPath = DecodeImageToFile(GetStr(designRaw, "BackgroundFrontImageBase64"), importFolder, "bg_front"),
                        BackgroundBackPath = DecodeImageToFile(GetStr(designRaw, "BackgroundBackImageBase64"), importFolder, "bg_back"),
                        WatermarkPath = DecodeImageToFile(GetStr(designRaw, "WatermarkImageBase64"), importFolder, "watermark")
                    };

                    string layoutVariant = raw.ContainsKey("LayoutVariant") ? raw["LayoutVariant"].ToString() : "Full";
                    int newId = _repo.Save(0, name, fields, design, layoutVariant);
                    UiTheme.ShowSuccess(this, "قالب «" + name + "» وارد شد.");
                    LoadTemplateList();

                    for (int i = 0; i < _templates.Count; i++)
                        if (_templates[i].TemplateID == newId) { _lstTemplates.SelectedIndex = i; break; }
                }
                catch (Exception ex)
                {
                    Msg.Show("خطا در Import (فایل معتبر نیست؟): " + ex.Message);
                }
            }
        }

        private string MakeUniqueTemplateName(string baseName)
        {
            var existing = _repo.GetAll();
            string name = baseName;
            int suffix = 2;
            while (existing.Exists(t => t.Name == name))
            {
                name = baseName + " (" + suffix + ")";
                suffix++;
            }
            return name;
        }

        private static string GetStr(Dictionary<string, object> dict, string key)
        {
            return dict.ContainsKey(key) && dict[key] != null ? dict[key].ToString() : "";
        }

        // آموزش — TextOverrides در Export به‌صورتِ آبجکتِ تودرتو (کلید=نامِ
        // فیلد → {Content,Color,FontSizePercent}) سریالایز شده؛ اینجا همان
        // را برمی‌گردانیم. فایل‌های وارداتیِ قدیمی (بدونِ این کلید) فقط
        // دیکشنریِ خالی می‌گیرند — بدونِ خطا.
        private static Dictionary<string, TextFieldOverride> ParseTextOverrides(Dictionary<string, object> designRaw, JavaScriptSerializer serializer)
        {
            var result = new Dictionary<string, TextFieldOverride>();
            if (!designRaw.ContainsKey("TextOverrides") || designRaw["TextOverrides"] == null) return result;

            try
            {
                var outer = serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(designRaw["TextOverrides"]));
                foreach (var kv in outer)
                {
                    var inner = serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(kv.Value));
                    result[kv.Key] = new TextFieldOverride
                    {
                        Content = GetStr(inner, "Content"),
                        Color = GetStr(inner, "Color"),
                        FontSizePercent = inner.ContainsKey("FontSizePercent") ? Convert.ToInt32(inner["FontSizePercent"]) : 100
                    };
                }
            }
            catch { /* فایلِ خراب/ناسازگار — همان دیکشنریِ خالی برمی‌گردد */ }

            return result;
        }

        private static string EncodeImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                return Path.GetExtension(path).TrimStart('.') + ";" + Convert.ToBase64String(bytes);
            }
            catch { return ""; }
        }

        private static string DecodeImageToFile(string encoded, string destFolder, string baseName)
        {
            if (string.IsNullOrWhiteSpace(encoded)) return "";
            int sep = encoded.IndexOf(';');
            if (sep < 0) return "";

            string ext = encoded.Substring(0, sep);
            string base64 = encoded.Substring(sep + 1);
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                string destPath = Path.Combine(destFolder, baseName + "." + ext);
                File.WriteAllBytes(destPath, bytes);
                return destPath;
            }
            catch { return ""; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_previewTimer != null) { _previewTimer.Stop(); _previewTimer.Dispose(); _previewTimer = null; }
            if (_webViewPreview != null) { _webViewPreview.Dispose(); _webViewPreview = null; }
        }
    }
}

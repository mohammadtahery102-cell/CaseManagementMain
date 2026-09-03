using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Web.Script.Serialization;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // طراحِ سبکِ کارت («Card Designer») — رنگ/فونت/پس‌زمینهٔ هر رو/واترمارک/
    // هولوگرام/QR/بارکد. آموزش — همهٔ این‌ها فقط داخلِ پوشهٔ کاریِ یک‌بارمصرف
    // (نه بستهٔ فریزشدهٔ GuardianCard) اعمال می‌شوند؛ نگاه کنید
    // GuardianCardRenderer.ApplyDesignOverrides. مقادیرِ خالی/null یعنی
    // «همان طرحِ اصلیِ GuardianCard، بدون تغییر» — نصب‌های موجود بدونِ هیچ
    // قالبی دقیقاً همان کارتِ قبلی را می‌گیرند.
    //
    // آموزش — محدودیتِ صادقانه: چون طرحِ کارت یک HTML/CSS ثابت است (نه یک
    // canvas پویا)، پس‌زمینه/واترمارک روی این عناصر ست می‌شوند نه «زیرِ همهٔ
    // لایه‌ها»؛ در بخش‌هایی که یک پنل (.panel) پس‌زمینهٔ مات دارد، تصویر پیدا
    // نیست — این دقیقاً همان محدودیتی است که در گزارشِ معماری پیش از این
    // کار توضیح داده شد (نسخهٔ سبک، نه طراحِ کاملِ drag-and-drop).
    // ─────────────────────────────────────────────────────────────────────────
    public class CardTemplateDesign
    {
        public string PrimaryColor { get; set; } = "";
        public string SecondaryColor { get; set; } = "";
        public string FontFamily { get; set; } = "";
        public string BackgroundFrontPath { get; set; } = "";
        public string BackgroundBackPath { get; set; } = "";
        public string WatermarkPath { get; set; } = "";
        public int WatermarkOpacityPercent { get; set; } = 15;
        public bool HologramEnabled { get; set; } = true;
        // آموزش — این دو با ToggleableFields (متنی) قاطی نشدند چون منطقِ
        // اعمالشان تصویری/staging است، نه صرفاً خالی‌کردنِ یک رشته.
        public bool ShowQRCode { get; set; } = false;
        public bool ShowBarcode { get; set; } = true;

        // آموزش — به‌درخواستِ کاربر: تا اینجا فقط PrimaryColor/SecondaryColor
        // (رنگ‌های تزئینیِ برند) قابل‌تنظیم بودند؛ BackgroundColor/TextColor
        // متغیرهای CSS دیگری را override می‌کنند (--surface-color/
        // --surface-muted-color و --text-color/--text-muted-color/
        // --text-faint-color در guardian-card.css — نگاه کنید
        // GuardianCardRenderer.ApplyDesignOverrides) که همان‌جا از قبل
        // متغیر بودند، فقط تا الان هیچ UI/فیلدِ قالبی کنترلشان نمی‌کرد.
        // خالی = بدون تغییر (رفتار قبلی نصب‌های موجود دقیقاً حفظ می‌شود).
        public string BackgroundColor { get; set; } = "";
        public string TextColor { get; set; } = "";

        // آموزش — ضریبِ اندازهٔ فونت (٪) — به --font-scale در
        // guardian-card.css نگاشت می‌شود (تنها ویرایشِ مجازشدهٔ آن فایل:
        // هر font-size ثابت به calc(N * var(--font-scale,1)) تبدیل شد).
        // ۱۰۰ یعنی دقیقاً اندازهٔ اصلیِ طراحی.
        public int FontScalePercent { get; set; } = 100;

        // آموزش — به‌درخواستِ کاربر: کدام ماه‌ها در جدولِ پرداختِ برگهٔ دوم
        // چاپ شوند (مثلاً بدونِ حمل، یا فقط چند ماهِ دلخواه). رشتهٔ CSV از
        // اندیسِ ماه (۱=حمل ... ۱۲=حوت)؛ خالی = همهٔ ۱۲ ماه (رفتارِ قبلی،
        // بدونِ تغییر برای قالب‌های موجود). فیلترِ واقعی در
        // FrmGuardianCardPreview.LoadCardAsync روی data.PaymentLedger اعمال
        // می‌شود؛ نگاه کنید CardTemplateRepository.FilterLedgerMonths.
        public string LedgerMonthsCsv { get; set; } = "";

        // آموزش — رنگِ پس‌زمینهٔ فقط نوارِ هدر (گرادیانِ navy بالای کارت) —
        // جدا از BackgroundColor بالا (که روی --surface-color/سطحِ بدنه اثر
        // می‌گذارد) و جدا از PrimaryColor (که روی چند عنصرِ دیگر هم اثر
        // می‌گذارد)، چون کاربر خواستِ کنترلِ مستقلِ همین یک ناحیه را داشت.
        public string HeaderBackgroundColor { get; set; } = "";

        // آموزش — عکسِ گردِ بالا-راستِ هدر (پیش‌فرضِ GuardianCard یک عکسِ
        // تزئینیِ ثابت دارد). PortraitScalePercent اندازه را عوض می‌کند
        // (۱۰۰=همان اندازهٔ اصلی). PortraitBlank=true یعنی آن عکسِ پیش‌فرض
        // پاک شود و این محل فقط یک قابِ خالیِ آماده‌ی آپلود بماند.
        public int PortraitScalePercent { get; set; } = 100;
        public bool PortraitBlank { get; set; } = false;

        // آموزش — ارتفاعِ نوارِ رنگیِ بالای کارت (min-height در .card-header)
        // — به‌درخواستِ کاربر برایِ آزادکردنِ فضا برایِ اطلاعاتِ اصلی. ۱۰۰=
        // اندازهٔ اصلیِ ۲۲.۴mm؛ ۷۵ یعنی ۲۵٪ کوچک‌تر.
        public int HeaderHeightScalePercent { get; set; } = 100;

        // آموزش — به‌درخواستِ صریحِ کاربر: ابعاد/اندازهٔ قابِ عکسِ جمعیِ
        // خانواده قابل‌تنظیم شد — هم‌الگویِ PortraitScalePercent بالا، با
        // یک تفاوت: چون این عکس می‌تواند عمودی/افقی هم باشد (نه فقط مربع)،
        // یک نسبتِ ابعاد جدا هم دارد. "W:H" (مثلِ "1:1"، "9:16"، "4:3")؛
        // پیش‌فرض "1:1" دقیقاً همان مربعِ فعلی است — قالب‌های موجود بدونِ
        // تنظیمِ صریح، دقیقاً همان شکلِ قبلی را می‌گیرند. اندازهٔ واقعی در
        // GuardianCardRenderer.ApplyDesignOverrides محاسبه می‌شود (ارتفاعِ
        // پایه ۸mm × مقیاس، عرض از رویِ همان نسبت).
        public string FamilyPhotoAspectRatio { get; set; } = "1:1";
        public int FamilyPhotoScalePercent { get; set; } = 100;

        // آموزش — به‌درخواستِ صریحِ کاربر: وقتی عکسِ آپلودشده با نسبتِ ابعادِ
        // قاب یکی نیست، پیش‌فرض (false) همان رفتارِ فعلی (object-fit:cover)
        // را نگه می‌دارد — عکس برشته می‌شود تا قاب را پر کند، ممکن است
        // بخشی از اعضا بیرون از کادر بیفتد. true یعنی object-fit:contain —
        // کلِ عکس بدونِ برش دیده می‌شود (اگر نسبت یکی نباشد، فضایِ خالی/
        // نواری در قاب می‌ماند، ولی هیچ عضوی از عکس گم نمی‌شود). چون این
        // رفتار برایِ همهٔ قالب‌ها یکسان مطلوب نیست («بعضی جاها اسکیچ
        // نمی‌خواهیم»)، به‌جایِ رفتارِ ثابت، یک تیکِ جداگانه در Card Designer
        // شد — قالب‌های موجود بدونِ تنظیمِ صریح دقیقاً همان cover قبلی را
        // می‌گیرند.
        public bool FamilyPhotoFitContain { get; set; }

        // آموزش — سقفِ تعدادِ ردیف‌های نمایش‌دادهٔ فهرستِ اعضای خانواده روی
        // کارتِ کامل (۰=بدونِ سقف، همهٔ اعضا). نگاه کنید
        // GuardianCardRenderer.StageAndPopulate.
        public int FamilyListMaxRows { get; set; } = 0;

        // آموزش — «ترتیب واقعیِ فیلدها» (Card Designer Phase 1): CSV از
        // کلیدهای FieldOrderableKeys/FieldOrderableKeysSimple به ترتیبِ
        // دلخواه. خالی = ترتیبِ پیش‌فرض (همان چیدمانِ فعلی، بدونِ تغییر).
        // اِعمالِ واقعی با تزریقِ CSS `order` در
        // GuardianCardRenderer.ApplyFieldOrderOverrides انجام می‌شود؛ خودِ
        // این رشته فقط داده را نگه می‌دارد.
        public string FieldOrderCsv { get; set; } = "";

        // آموزش — موقعیتِ عکسِ سرپرست نسبت به گروهِ فیلدهای متنی —
        // ""=قبل از فیلدها (پیش‌فرض/فعلی)، "After"=بعد از فیلدها.
        public string PhotoPosition { get; set; } = "";

        // آموزش — ترتیبِ سلول‌های نوارِ امنیتیِ پایینِ کارت (QR/بارکد/امضا/
        // مهر/هولوگرام) — فقط قالبِ کامل (نوارِ امنیتی در قالبِ ساده وجود
        // ندارد). خالی = ترتیبِ پیش‌فرض.
        public string SecurityBandOrderCsv { get; set; } = "";

        // آموزش — «متن‌های قابلِ‌ویرایش» (Besmellah/Kicker/Motto/Organization
        // Name/Address/Phone/Website/Email/ComplaintMessage/FoundCardMessage):
        // به‌جای یک پراپرتیِ جداگانه برای هرکدام (که کدِ تکراری و UI شلوغ
        // می‌سازد)، یک دیکشنری کلید=نامِ فیلد (همان data-field در index.html).
        // خالی/غایب یعنی «همان متن/رنگ/سایزِ پیش‌فرض» — نگاه کنید
        // GuardianCardRenderer.ApplyTextOverrides.
        public Dictionary<string, TextFieldOverride> TextOverrides { get; set; } = new Dictionary<string, TextFieldOverride>();
    }

    // یک override برای یک فیلدِ متنیِ خاص — هر سه اختیاری‌اند (خالی/۱۰۰ = بدونِ تغییر).
    public class TextFieldOverride
    {
        public string Content { get; set; } = "";
        public string Color { get; set; } = "";
        public int FontSizePercent { get; set; } = 100;
        // آموزش — به‌درخواستِ کاربر: نوعِ فونت و فاصلهٔ خط هم قابل‌تنظیمِ
        // هر فیلد شدند، جدا از سراسریِ FontFamily/FontScalePercent در
        // CardTemplateDesign. خالی/۱۰۰ = بدونِ تغییر (ارثِ سراسری).
        public string FontFamily { get; set; } = "";
        public int LineHeightPercent { get; set; } = 100;

        // آموزش — Card Designer Phase 1: تراز و وزنِ فونتِ همین یک فیلد.
        // خالی = بدونِ تغییر (ارثِ سراسری/طراحِ پایه) — دقیقاً همان قراردادِ
        // بقیهٔ پراپرتی‌های این کلاس، پس قالب‌های ذخیره‌شدهٔ قبلی (بدونِ این
        // دو فیلد در JSON) دقیقاً همان رفتارِ قبلی را می‌گیرند.
        public string Alignment { get; set; } = ""; // "", "right", "center", "left"
        public string FontWeight { get; set; } = ""; // "", "400", "500", "600", "700"
    }

    // یک قالبِ کارت — نام + کدام فیلدهای اختیاری روشن‌اند + طراحِ بصری.
    // نگاه کنید آموزشِ کاملِ محدودیت معماری در DatabaseInitializer (بخش
    // TblCardTemplate).
    public class CardTemplate
    {
        public int TemplateID { get; set; }
        public string Name { get; set; }
        public Dictionary<string, bool> Fields { get; set; } = new Dictionary<string, bool>();
        public CardTemplateDesign Design { get; set; } = new CardTemplateDesign();
        public bool IsDefault { get; set; }

        // آموزش — "Full" = طرحِ ثابتِ کاملِ فعلی (index.html، همان رفتارِ
        // همیشگی). "Simple" = طرحِ بسیار سادهٔ جدید (simple.html، فیلدهای
        // متفاوت — نگاه کنید CardTemplateRepository.ToggleableFieldsSimple و
        // FrmGuardianCardPreview.RenderDataAsync که بر اساسِ همین navigate
        // می‌کند). پیش‌فرضِ "Full" یعنی قالب‌های موجود دست‌نخورده می‌مانند.
        public string LayoutVariant { get; set; } = "Full";

        // آموزش — مدیریتِ حرفه‌ایِ قالب (Phase 2): نوع/توضیح/وضعیت/متادیتا.
        // همه اختیاری/دارایِ پیش‌فرضِ امن — قالب‌هایِ ذخیره‌شدهٔ قبلی که این
        // ستون‌ها را نداشتند دقیقاً «فعال، بدونِ نوع/توضیح، سازنده/ویرایش‌
        // کنندهٔ نامعلوم» خوانده می‌شوند (نه خطا، نه رفتارِ متفاوت).
        public string TemplateType { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
        public string ModifiedBy { get; set; } = "";
        public DateTime? ModifiedAt { get; set; }

        // آموزش — پروفایلِ چاپ: این فاز فقط "PVC" پیاده‌سازی شده (دقیقاً
        // رفتارِ امروزیِ print.css)؛ ستون/پراپرتی برایِ توسعهٔ آیندهٔ A4
        // آماده است، بدونِ اینکه چیزی در پایپ‌لاینِ چاپ عوض شود.
        public string PrintProfile { get; set; } = "PVC";
    }

    // یک نسخهٔ تاریخی از یک قالب — Snapshot کامل، نه فقط تفاوت. «چه چیزی
    // عوض شد» با مقایسهٔ دو Snapshot در لحظهٔ نمایش محاسبه می‌شود (نگاه
    // کنید FrmCardTemplateManager)، نه با یک موتورِ diff جداگانه در اینجا.
    public class CardTemplateVersion
    {
        public int VersionID { get; set; }
        public int TemplateID { get; set; }
        public int VersionNumber { get; set; }
        public string Name { get; set; }
        public Dictionary<string, bool> Fields { get; set; } = new Dictionary<string, bool>();
        public CardTemplateDesign Design { get; set; } = new CardTemplateDesign();
        public string LayoutVariant { get; set; } = "Full";
        public string TemplateType { get; set; } = "";
        public string Description { get; set; } = "";
        public string ChangedByUsername { get; set; } = "";
        public DateTime ChangedAt { get; set; }
        public string ChangeNote { get; set; } = "";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // لایه داده برای مدیریت قالب‌های کارت (بخش «CARD TEMPLATE MANAGEMENT»).
    // ─────────────────────────────────────────────────────────────────────────
    public class CardTemplateRepository
    {
        private readonly DatabaseHelper _db = new DatabaseHelper();
        private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        // فهرستِ کاملِ فیلدهای اختیاریِ متنیِ قابل‌کنترل — دقیقاً همان‌هایی که
        // در docs/TEMPLATE_SCHEMA.json پروژهٔ GuardianCard با
        // validation.required برابر false مشخص شده‌اند (یا مطابق تصمیمِ
        // کسب‌وکاریِ این پروژه اختیاری‌اند). فیلدهای الزامی (Photo،
        // GuardianName، FatherName، NationalID، تاریخ‌ها) اصلاً اینجا
        // نیستند — هرگز نباید خاموش شوند.
        public static readonly string[] ToggleableFields =
        {
            "PublicCode", "Website", "Email", "IssuedBy", "Position", "Logo", "Signature", "Stamp",
            // آموزش — به‌درخواستِ کاربر: چیپِ «شناسه کارت» بالا-چپِ هدر هم
            // اختیاری شد (پیش‌فرض روشن، مثلِ همه‌ی فیلدهای دیگر — برای
            // خاموش‌کردنش کاربر خودش این تیک را بردارد).
            "CardCode",
            // آموزش — به‌درخواستِ صریحِ کاربر: عناصرِ هدر (بسمه‌تعالی/تیترِ
            // بزرگ/عکسِ گردِ تزئینی/خطِ «ولایت») هم اختیاری شدند — به‌جای
            // پاک‌کردنِ فیزیکیِ آن‌ها از index.html (که برایِ همیشه رویِ همهٔ
            // قالب‌ها اثر می‌گذاشت)، این‌طور فقط برای قالبی که کاربر خودش
            // خاموششان می‌کند حذف می‌شوند و برایِ بقیه دست‌نخورده می‌ماند.
            "Besmellah", "OrganizationName", "Portrait", "BranchName",
            // آموزش — Address/Phone تا الان toggle نداشتند (فقط Website/
            // Email داشتند)؛ اضافه شد. GuardianName/FatherName/NationalID/
            // RequestType تا الان «الزامی» بودند (هرگز خاموش نمی‌شدند)؛
            // طبقِ درخواستِ صریح این‌بار toggleable شدند.
            "Address", "Phone", "GuardianName", "FatherName", "NationalID", "RequestType",
            // آموزش — فهرستِ اعضای خانواده روی کارتِ کامل (بخشِ تازه — نگاه
            // کنید index.html/guardian-card.js).
            "FamilyList",
            // آموزش — به‌درخواستِ صریحِ کاربر: ردیفِ «ایتام» (شمارشی/متنِ
            // «نیاز به تعیین نقش») هم toggleable شد تا کاربر خودش خاموشش کند.
            "OrphansCount",
            // آموزش — به‌درخواستِ صریحِ کاربر: عکسِ جمعیِ خانواده (یک قابِ
            // مستقل بالایِ فهرستِ اعضا) و ستونِ عکسِ هر عضو (درونِ همان
            // جدول) — هرکدام جدا از FamilyList (که کلِ بخش را کنترل
            // می‌کند) روشن/خاموش می‌شوند. پیش‌فرض هر دو روشن.
            "FamilyPhoto", "FamilyListPhotos"
        };

        // آموزش — فهرستِ فیلدهای قابل‌کنترلِ قالبِ «ساده» (simple.html) —
        // کاملاً جدا از ToggleableFields بالا، چون بعضی نام‌ها مثل Photo/
        // Province/IssueDate روی قالبِ کامل «الزامی» هستند (هرگز خاموش
        // نمی‌شوند) ولی روی قالبِ ساده کاملاً اختیاری‌اند.
        public static readonly string[] ToggleableFieldsSimple =
        {
            "Photo", "FamilyPhoto", "PublicCode", "CaseNo", "Province",
            "District", "Phone", "NationalID", "GuardianName", "FatherName",
            "RelationshipToFamily", "SimpleNotes", "Thumbprint", "IssueDate", "Orphans"
        };

        // آموزش — Card Designer Phase 1 («ترتیبِ واقعیِ فیلدها»): فهرستِ
        // فیلدهایی که واقعاً در HTML با data-order-field="X" علامت‌گذاری
        // شده‌اند (نگاه کنید index.html/simple.html) و ترتیبشان با CSS
        // order قابلِ کنترل است — نگاه کنید GuardianCardRenderer.
        // ApplyFieldOrderOverrides. عمداً زیرمجموعه‌ای از ToggleableFields
        // است، نه همهٔ فیلدها: فقط ۵ ردیفِ متنیِ پنلِ سرپرست، چون این‌ها
        // تنها فیلدهایی‌اند که از نظرِ بصری هم‌شکل‌اند (ردیفِ برچسب:مقدار)
        // و بدونِ شکستنِ طرح می‌توانند جای هم را عوض کنند. Orphans/عکس/QR
        // عمداً اینجا نیستند (نگاه کنید PhotoPosition/SecurityBandOrderCsv
        // بالا برایِ آن‌ها).
        public static readonly string[] FieldOrderableKeys =
        {
            "PublicCode", "GuardianName", "FatherName", "NationalID", "RequestType"
        };

        // آموزش — همتای بالا برایِ قالبِ «ساده»؛ فقط فیلدهایی که آنجا واقعاً
        // data-order-field دارند (نگاه کنید simple.html) — PublicCode/
        // RequestType در قالبِ ساده اصلاً وجود ندارند.
        public static readonly string[] FieldOrderableKeysSimple =
        {
            "GuardianName", "FatherName", "NationalID"
        };

        // آموزش — سلول‌های نوارِ امنیتیِ پایینِ کارتِ کامل — فقط قالبِ کامل
        // (قالبِ ساده نوارِ امنیتی ندارد).
        public static readonly string[] SecurityBandOrderableKeys =
        {
            "QRCode", "Barcode", "Signature", "Stamp", "Hologram"
        };

        // آموزش — ستون‌هایِ Phase 2 با نامِ صریح انتخاب می‌شوند (نه *) تا اگر
        // روزی ستونِ دیگری به TblCardTemplate اضافه شد، این کوئری بی‌صدا
        // چیزِ غیرمنتظره برنگرداند.
        private const string SelectColumns =
            "TemplateID, Name, FieldsJson, DesignJson, IsDefault, LayoutVariant, " +
            "TemplateType, Description, IsActive, CreatedBy, CreatedAt, ModifiedBy, ModifiedAt, PrintProfile";

        public List<CardTemplate> GetAll()
        {
            var list = new List<CardTemplate>();
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT " + SelectColumns + " FROM TblCardTemplate ORDER BY IsDefault DESC, Name", con))
            {
                con.Open();
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                    while (dr.Read())
                        list.Add(MapRow(dr));
            }
            return list;
        }

        public CardTemplate GetById(int templateId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT " + SelectColumns + " FROM TblCardTemplate WHERE TemplateID = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Id", templateId);
                con.Open();
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                    return dr.Read() ? MapRow(dr) : null;
            }
        }

        // templateId=0 یعنی رکورد جدید. نامِ تکراری با خطای UNIQUE مسدود می‌شود
        // (پیام آن در فراخوان مدیریت می‌شود). layoutVariant پیش‌فرضش "Full"
        // است تا فراخوان‌های قدیمی (اگر جایی مانده باشد) دست‌نخورده کار کنند.
        // آموزش — Phase 2: هر Save (چه ایجاد چه ویرایش) بعد از موفقیت یک
        // Snapshot تازه در TblCardTemplateVersion می‌نویسد — همینجا، در همان
        // اتصال، تا هرگز فراموش/دورزده نشود (نگاه کنید SaveVersionSnapshot).
        // templateType/description/changeNote همه اختیاری‌اند (خالی = بدونِ
        // تغییر نسبت به قراردادِ قبلی) تا فراخوان‌هایِ قدیمی‌تر دست‌نخورده کار
        // کنند.
        public int Save(int templateId, string name, Dictionary<string, bool> fields, CardTemplateDesign design,
            string layoutVariant = "Full", string templateType = "", string description = "", string changeNote = "")
        {
            string fieldsJson = _serializer.Serialize(fields);
            string designJson = _serializer.Serialize(design ?? new CardTemplateDesign());
            string variant = string.IsNullOrWhiteSpace(layoutVariant) ? "Full" : layoutVariant;
            string trimmedName = name.Trim();
            string username = SecurityContext.Username ?? "";

            using (SQLiteConnection con = _db.GetConnection())
            {
                con.Open();
                int resultId;
                if (templateId <= 0)
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO TblCardTemplate (Name, FieldsJson, DesignJson, LayoutVariant, TemplateType, Description, CreatedBy, ModifiedBy, ModifiedAt)
VALUES (@Name, @Fields, @Design, @Variant, @Type, @Desc, @User, @User, datetime('now'));
SELECT last_insert_rowid();", con))
                    {
                        cmd.Parameters.AddWithValue("@Name", trimmedName);
                        cmd.Parameters.AddWithValue("@Fields", fieldsJson);
                        cmd.Parameters.AddWithValue("@Design", designJson);
                        cmd.Parameters.AddWithValue("@Variant", variant);
                        cmd.Parameters.AddWithValue("@Type", (object)templateType ?? "");
                        cmd.Parameters.AddWithValue("@Desc", (object)description ?? "");
                        cmd.Parameters.AddWithValue("@User", username);
                        resultId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
                else
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(@"
UPDATE TblCardTemplate SET Name = @Name, FieldsJson = @Fields, DesignJson = @Design, LayoutVariant = @Variant,
       TemplateType = @Type, Description = @Desc, ModifiedBy = @User, ModifiedAt = datetime('now')
WHERE TemplateID = @Id", con))
                    {
                        cmd.Parameters.AddWithValue("@Name", trimmedName);
                        cmd.Parameters.AddWithValue("@Fields", fieldsJson);
                        cmd.Parameters.AddWithValue("@Design", designJson);
                        cmd.Parameters.AddWithValue("@Variant", variant);
                        cmd.Parameters.AddWithValue("@Type", (object)templateType ?? "");
                        cmd.Parameters.AddWithValue("@Desc", (object)description ?? "");
                        cmd.Parameters.AddWithValue("@User", username);
                        cmd.Parameters.AddWithValue("@Id", templateId);
                        cmd.ExecuteNonQuery();
                    }
                    resultId = templateId;
                }

                SaveVersionSnapshot(con, resultId, trimmedName, fieldsJson, designJson, variant, templateType, description, changeNote);
                return resultId;
            }
        }

        // آموزش — یک Snapshot کاملِ تازه؛ VersionNumber = بیشینهٔ فعلی + ۱
        // (۱ برایِ اولین بار). هیچ نسخهٔ قبلی حذف/بازنویسی نمی‌شود — دقیقاً
        // طبقِ الزامِ «تاریخچهٔ مهم نباید از بین برود».
        private static void SaveVersionSnapshot(SQLiteConnection con, int templateId, string name,
            string fieldsJson, string designJson, string layoutVariant, string templateType, string description, string changeNote)
        {
            int nextVersion;
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT IFNULL(MAX(VersionNumber), 0) + 1 FROM TblCardTemplateVersion WHERE TemplateID = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Id", templateId);
                nextVersion = Convert.ToInt32(cmd.ExecuteScalar());
            }

            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO TblCardTemplateVersion
    (TemplateID, VersionNumber, Name, FieldsJson, DesignJson, LayoutVariant, TemplateType, Description,
     ChangedByUserID, ChangedByUsername, ChangeNote)
VALUES
    (@TemplateID, @VersionNumber, @Name, @Fields, @Design, @Variant, @Type, @Desc,
     @UserID, @Username, @Note)", con))
            {
                cmd.Parameters.AddWithValue("@TemplateID", templateId);
                cmd.Parameters.AddWithValue("@VersionNumber", nextVersion);
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Fields", fieldsJson);
                cmd.Parameters.AddWithValue("@Design", (object)designJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Variant", layoutVariant);
                cmd.Parameters.AddWithValue("@Type", (object)templateType ?? "");
                cmd.Parameters.AddWithValue("@Desc", (object)description ?? "");
                cmd.Parameters.AddWithValue("@UserID", SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : DBNull.Value);
                cmd.Parameters.AddWithValue("@Username", (object)SecurityContext.Username ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Note", string.IsNullOrWhiteSpace(changeNote) ? (object)DBNull.Value : changeNote.Trim());
                cmd.ExecuteNonQuery();
            }
        }

        // فهرستِ نسخه‌های یک قالب، تازه‌ترین اول.
        public List<CardTemplateVersion> GetVersions(int templateId)
        {
            var list = new List<CardTemplateVersion>();
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT VersionID, TemplateID, VersionNumber, Name, FieldsJson, DesignJson, LayoutVariant,
       TemplateType, Description, ChangedByUsername, ChangedAt, ChangeNote
FROM TblCardTemplateVersion WHERE TemplateID = @Id ORDER BY VersionNumber DESC", con))
            {
                cmd.Parameters.AddWithValue("@Id", templateId);
                con.Open();
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                    while (dr.Read())
                        list.Add(MapVersionRow(dr));
            }
            return list;
        }

        public CardTemplateVersion GetVersion(int versionId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT VersionID, TemplateID, VersionNumber, Name, FieldsJson, DesignJson, LayoutVariant,
       TemplateType, Description, ChangedByUsername, ChangedAt, ChangeNote
FROM TblCardTemplateVersion WHERE VersionID = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Id", versionId);
                con.Open();
                using (SQLiteDataReader dr = cmd.ExecuteReader())
                    return dr.Read() ? MapVersionRow(dr) : null;
            }
        }

        // آموزش — «بازگردانی» یعنی محتوایِ آن نسخهٔ قدیمی را دوباره از
        // مسیرِ عادیِ Save عبور بده — این یعنی خودِ بازگردانی هم یک نسخهٔ
        // تازه می‌سازد (هرگز نسخه‌های بینابین حذف نمی‌شوند) و دقیقاً همان
        // منطقِ سازگاریِ Save (ستون‌های جدید/امن) را دوباره اجرا نمی‌کند.
        public int RestoreVersion(int templateId, int versionId)
        {
            CardTemplateVersion v = GetVersion(versionId);
            if (v == null || v.TemplateID != templateId)
                throw new InvalidOperationException("نسخهٔ درخواست‌شده برایِ این قالب پیدا نشد.");

            return Save(templateId, v.Name, v.Fields, v.Design, v.LayoutVariant, v.TemplateType, v.Description,
                "بازگردانی از نسخهٔ " + v.VersionNumber);
        }

        // فعال/غیرفعال‌سازی — یک تغییرِ وضعیتِ سبک، نه یک ویرایشِ محتوا؛
        // عمداً نسخهٔ تازه نمی‌سازد (نویزِ بی‌مورد در تاریخچه)، ولی زمان/
        // کاربرِ ویرایش را به‌روز می‌کند.
        public void SetActive(int templateId, bool isActive)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "UPDATE TblCardTemplate SET IsActive = @Active, ModifiedBy = @User, ModifiedAt = datetime('now') WHERE TemplateID = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@User", SecurityContext.Username ?? "");
                cmd.Parameters.AddWithValue("@Id", templateId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // تکثیرِ یک قالبِ موجود با نامِ تازه — از همان مسیرِ Save عبور می‌کند
        // (پس قالبِ تازه هم نسخهٔ ۱ خودش را می‌گیرد، مثلِ هر قالبِ تازهٔ دیگر).
        public int Duplicate(int sourceTemplateId, string newName)
        {
            CardTemplate source = GetById(sourceTemplateId);
            if (source == null)
                throw new InvalidOperationException("قالبِ مبدأ برایِ تکثیر پیدا نشد.");

            return Save(0, newName, source.Fields, source.Design, source.LayoutVariant,
                source.TemplateType, source.Description, "تکثیر از قالب #" + sourceTemplateId + " («" + source.Name + "»)");
        }

        // قالبِ پیش‌فرض (IsDefault=1) هرگز حذف نمی‌شود — نصب‌های بدون انتخابِ
        // صریحِ قالب باید همیشه یک قالبِ معتبر (کامل) داشته باشند.
        public void Delete(int templateId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "DELETE FROM TblCardTemplate WHERE TemplateID = @Id AND IsDefault = 0", con))
            {
                cmd.Parameters.AddWithValue("@Id", templateId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // اگر یک فیلدِ toggleable در FieldsJson نیامده باشد (مثلاً قالبی که
        // قبل از افزودنِ یک فیلدِ جدید به ToggleableFields ساخته شده)،
        // پیش‌فرض «روشن» در نظر گرفته می‌شود — امن‌تر از خاموش‌کردنِ ناخواسته‌ی
        // چیزی که کاربر قصدِ خاموش‌کردنش را نداشته.
        public static bool IsFieldEnabled(CardTemplate template, string field)
        {
            if (template == null) return true;
            return !template.Fields.TryGetValue(field, out bool v) || v;
        }

        // ۱=حمل ... ۱۲=حوت — همان ترتیبِ MONTHS در guardian-card.js.
        public static readonly string[] AllMonthNames =
        {
            "حمل", "ثور", "جوزا", "سرطان", "اسد", "سنبله", "میزان", "عقرب", "قوس", "جدی", "دلو", "حوت"
        };

        public static List<int> ParseLedgerMonths(string csv)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(csv)) return result;
            foreach (string part in csv.Split(','))
                if (int.TryParse(part.Trim(), out int n) && n >= 1 && n <= 12)
                    result.Add(n);
            return result;
        }

        public static string BuildLedgerMonthsCsv(IEnumerable<int> months)
        {
            return string.Join(",", months);
        }

        // آموزش — هم‌الگویِ ParseLedgerMonths/BuildLedgerMonthsCsv بالا، فقط
        // برایِ کلیدهای رشته‌ایِ فیلد (نه عددِ ماه). فیلدهایی که در CSV
        // آمده‌اند ولی در allowedKeys نیستند نادیده گرفته می‌شوند — امن در
        // برابرِ CSVِ قدیمی/خراب یا سوییچِ دستیِ نوعِ طرح.
        public static List<string> ParseFieldOrder(string csv, string[] allowedKeys)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(csv) || allowedKeys == null) return result;
            var allowed = new HashSet<string>(allowedKeys);
            foreach (string part in csv.Split(','))
            {
                string key = part.Trim();
                if (key.Length > 0 && allowed.Contains(key) && !result.Contains(key))
                    result.Add(key);
            }
            return result;
        }

        public static string BuildFieldOrderCsv(IEnumerable<string> orderedKeys)
        {
            return string.Join(",", orderedKeys);
        }

        // آموزش — فیلترِ فهرستِ ۱۲ماهه‌ای که CardService همیشه می‌سازد، بر
        // اساسِ انتخابِ کاربر در قالب. LedgerMonthsCsv خالی = بدونِ فیلتر
        // (همهٔ ۱۲ ماه، رفتارِ قبلی). ترتیبِ ماه‌ها همیشه تقویمی می‌ماند
        // (نه ترتیبِ انتخاب کاربر) تا جفت‌سازیِ «شصت گیرنده» در
        // guardian-card.js همچنان منطقی باشد.
        public static void FilterLedgerMonths(GuardianCardData data, CardTemplateDesign design)
        {
            if (data == null || design == null) return;
            List<int> months = ParseLedgerMonths(design.LedgerMonthsCsv);
            if (months.Count == 0) return;

            data.PaymentLedger = data.PaymentLedger.FindAll(row => months.Contains(row.MonthIndex));
        }

        // آموزش — اِعمالِ قالب روی فیلدهای *متنیِ* یک GuardianCardData از قبل
        // ساخته‌شده. فیلدهای الزامی (Photo/GuardianName/...) اصلاً در این
        // سوییچ نیستند، پس هرگز لمس نمی‌شوند.
        //
        // چرا Logo/Signature/Stamp اینجا نیستند؟ چون GuardianCardRenderer
        // (StageAndPopulate/StageAndPopulateBatch) این سه را از رویِ مسیرِ
        // مطلقِ *مبدأ* (نه data.Logo فعلی) دوباره Stage می‌کند و data.Logo را
        // بی‌قید‌وشرط بازنویسی می‌کند — پس خالی‌کردنِ data.Logo اینجا هیچ اثری
        // نمی‌داشت. فراخوان باید IsFieldEnabled را مستقیماً روی *پارامترِ
        // مسیرِ مبدأ* که به Renderer می‌دهد اعمال کند (نگاه کنید
        // FrmGuardianCardPreview.LoadCardAsync / FrmGuardianCardBatchPrint).
        public static void ApplyTextFields(GuardianCardData data, CardTemplate template)
        {
            if (data == null || template == null) return;

            if (!IsFieldEnabled(template, "PublicCode")) data.PublicCode = "";
            if (!IsFieldEnabled(template, "Website")) data.Website = "";
            if (!IsFieldEnabled(template, "Email")) data.Email = "";
            if (!IsFieldEnabled(template, "IssuedBy")) data.IssuedBy = "";
            if (!IsFieldEnabled(template, "Position")) data.Position = "";
        }

        // آموزش — override محتوای متن‌های ثابتِ کارت (بسمه‌تعالی/موتو/تیتر/
        // پیام‌های پایینِ کارت/نامِ سازمان)، طبقِ CardTemplateDesign.TextOverrides.
        // فقط فیلدهایی که مکانیزمِ دیگری (روشن/خاموش یا تنظیماتِ سراسری) برای
        // «محتوا» ندارند اینجا override می‌شوند؛ Address/Website عمداً اینجا
        // نیستند — محتوایشان از قبل از طریقِ تنظیمات/ApplyTextFields کنترل
        // می‌شود. Phone/Email به‌درخواستِ صریحِ کاربر استثنا شدند: محتوایشان
        // هم از همین‌جا قابل‌ویرایش است. تداخلی با toggle-خاموش‌کردن ندارد،
        // چون خاموش‌کردن یک ردیف (removeSimpleRow در RemoveDisabledFieldsScript)
        // مستقل از محتوایِ data است — ردیف را بدونِ‌قید‌وشرط مخفی می‌کند.
        public static void ApplyTextOverrides(GuardianCardData data, CardTemplateDesign design)
        {
            if (data == null || design == null || design.TextOverrides == null) return;

            foreach (var kv in design.TextOverrides)
            {
                string content = kv.Value != null ? kv.Value.Content : "";
                if (string.IsNullOrEmpty(content)) continue;

                switch (kv.Key)
                {
                    case "OrganizationName": data.OrganizationName = content; break;
                    case "Besmellah": data.Besmellah = content; break;
                    case "MottoArabic": data.MottoArabic = content; break;
                    case "MottoTranslation": data.MottoTranslation = content; break;
                    case "Kicker": data.Kicker = content; break;
                    case "ComplaintMessage": data.ComplaintMessage = content; break;
                    case "FoundCardMessage": data.FoundCardMessage = content; break;
                    case "Phone": data.Phone = content; break;
                    case "Email": data.Email = content; break;
                }
            }
        }

        private static CardTemplate MapRow(SQLiteDataReader dr)
        {
            return new CardTemplate
            {
                TemplateID = Convert.ToInt32(dr["TemplateID"]),
                Name = dr["Name"].ToString(),
                Fields = ParseFieldsBool(dr["FieldsJson"]),
                Design = ParseDesign(dr["DesignJson"]),
                IsDefault = Convert.ToInt32(dr["IsDefault"]) != 0,
                LayoutVariant = dr["LayoutVariant"] == DBNull.Value ? "Full" : dr["LayoutVariant"].ToString(),
                TemplateType = ToText(dr["TemplateType"]),
                Description = ToText(dr["Description"]),
                IsActive = Convert.ToInt32(dr["IsActive"]) != 0,
                CreatedBy = ToText(dr["CreatedBy"]),
                CreatedAt = ToDateTime(dr["CreatedAt"]),
                ModifiedBy = ToText(dr["ModifiedBy"]),
                ModifiedAt = ToDateTime(dr["ModifiedAt"]),
                PrintProfile = dr["PrintProfile"] == DBNull.Value ? "PVC" : dr["PrintProfile"].ToString()
            };
        }

        private static CardTemplateVersion MapVersionRow(SQLiteDataReader dr)
        {
            return new CardTemplateVersion
            {
                VersionID = Convert.ToInt32(dr["VersionID"]),
                TemplateID = Convert.ToInt32(dr["TemplateID"]),
                VersionNumber = Convert.ToInt32(dr["VersionNumber"]),
                Name = dr["Name"].ToString(),
                Fields = ParseFieldsBool(dr["FieldsJson"]),
                Design = ParseDesign(dr["DesignJson"]),
                LayoutVariant = dr["LayoutVariant"] == DBNull.Value ? "Full" : dr["LayoutVariant"].ToString(),
                TemplateType = ToText(dr["TemplateType"]),
                Description = ToText(dr["Description"]),
                ChangedByUsername = ToText(dr["ChangedByUsername"]),
                ChangedAt = ToDateTime(dr["ChangedAt"]) ?? DateTime.MinValue,
                ChangeNote = ToText(dr["ChangeNote"])
            };
        }

        private static Dictionary<string, bool> ParseFieldsBool(object cell)
        {
            var fields = DeserializeDict(cell);
            var fieldsBool = new Dictionary<string, bool>();
            foreach (var kv in fields)
                fieldsBool[kv.Key] = Convert.ToBoolean(kv.Value);
            return fieldsBool;
        }

        private static CardTemplateDesign ParseDesign(object designCell)
        {
            if (designCell == DBNull.Value || designCell == null)
                return new CardTemplateDesign();
            try
            {
                return _serializer.Deserialize<CardTemplateDesign>(designCell.ToString()) ?? new CardTemplateDesign();
            }
            catch
            {
                return new CardTemplateDesign();
            }
        }

        private static string ToText(object cell)
        {
            return cell == DBNull.Value || cell == null ? "" : cell.ToString();
        }

        // آموزش — رفعِ باگ «Specified time is not supported in this calendar»:
        // این متد قبلاً با DateTime.TryParse بدونِ Culture کار می‌کرد، یعنی با
        // CurrentCulture — و Program.cs تقویمِ CurrentCulture را روی
        // PersianCalendar گذاشته است. در نتیجه رشتهٔ میلادیِ ذخیره‌شدهٔ SQLite
        // (`datetime('now')` → "2026-08-31 14:19:40") یا اصلاً پارس نمی‌شد
        // (→ null → DateTime.MinValue) یا سالِ ۲۰۲۶ به‌عنوان سالِ *شمسی*
        // تفسیر می‌شد. بعد، نمایشِ DateTime.MinValue با تقویمِ شمسی استثنا
        // می‌داد چون PersianCalendar فقط از ۶۲۲ میلادی به بعد را می‌پذیرد —
        // و چون هر ردیفِ نسخه همین مسیر را داشت، تاریخچهٔ نسخه‌ها *همیشه*
        // می‌ترکید.
        //
        // راه‌حل همان الگویِ تثبیت‌شدهٔ پروژه است — نگاه کنید
        // PersianDateHelper.ParseStoredDate: مقدارِ ذخیره‌شده همیشه با
        // InvariantCulture (تقویمِ میلادی) پارس می‌شود. تفاوت با
        // ParseStoredDate: اینجا ساعت هم لازم است، پس `.Date` گرفته نمی‌شود.
        private static DateTime? ToDateTime(object cell)
        {
            if (cell == DBNull.Value || cell == null) return null;
            if (cell is DateTime) return (DateTime)cell;

            string s = cell.ToString().Trim();
            if (s.Length == 0) return null;

            DateTime result;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return result;

            // تلاشِ دوم: رشتهٔ شمسیِ احتمالی (رکوردهای خیلی قدیمی).
            try { return PersianDateHelper.ParsePersianDate(s); }
            catch { return null; }
        }

        private static Dictionary<string, object> DeserializeDict(object cell)
        {
            string json = cell == DBNull.Value || cell == null ? "{}" : cell.ToString();
            try
            {
                return _serializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }
    }
}

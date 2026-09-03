using System.Collections.Generic;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // مدل داده کارت شناسایی سرپرست — دقیقاً منطبق بر قرارداد JSON مستندسازی‌شده
    // در GuardianCard\docs\FIELD_MAPPING.md و GuardianCard\docs\TEMPLATE_SCHEMA.json
    // (نام و املای هر پراپرتی عیناً همان کلید JSON مورد انتظار guardian-card.js
    // است، چون این کلاس مستقیماً به همان ساختار سریالایز می‌شود).
    // این پروژه چیزی داخل پوشه GuardianCard را تغییر نمی‌دهد؛ این کلاس فقط
    // «قرارداد ورودی» آن را در سمت C# نمایندگی می‌کند.
    // ─────────────────────────────────────────────────────────────────────────
    public class GuardianCardData
    {
        // شناسه‌ی داخلیِ پرونده — روی کارت چاپ نمی‌شود (هیچ data-field ای با
        // این نام در index.html وجود ندارد)، فقط برای تضمینِ یکتاییِ بارکد
        // در GuardianCardRenderer.BarcodeValue استفاده می‌شود (نگاه کنید همان‌جا).
        public int CasID { get; set; }

        public string OrganizationName { get; set; }
        public string BranchName { get; set; }
        public string BranchCode { get; set; }
        public string CardNumber { get; set; }

        public string PublicCode { get; set; }
        public string GuardianName { get; set; }
        public string FatherName { get; set; }
        public string NationalID { get; set; }
        // آموزش — «نوع مددجو» به‌درخواستِ کاربر — همان TblCase.RequestType
        // که در CaseModel از قبل موجود بود، فقط تا الان روی این کارت
        // چاپ نمی‌شد.
        public string RequestType { get; set; }
        public string OrphansCount { get; set; }
        public string IssueDate { get; set; }
        public string ExpiryDate { get; set; }

        public string Province { get; set; }
        public string District { get; set; }
        public string Village { get; set; }

        // متن ریزِ تزئینی دور کارت (نوار بالا/پایین) — طبق ولایتِ همین پرونده،
        // نه یک نام مرکز ثابت (پیش‌تر در خودِ index.html به‌صورت «بامیان» ثابت
        // بود). نگاه کنید CardService.BuildCardData.
        public string MicrotextLabel { get; set; }

        public string Notice1 { get; set; }
        public string Notice2 { get; set; }
        public string Notice3 { get; set; }
        public string Notice4 { get; set; }
        public string Notice5 { get; set; }

        public string Address { get; set; }
        public string Phone { get; set; }
        public string Website { get; set; }
        public string Email { get; set; }

        public string SignatureLabel { get; set; }
        public string IssuedBy { get; set; }
        public string Position { get; set; }
        public string LegalLine { get; set; }

        // آموزش — این ۶ فیلد قبلاً متنِ ثابتِ داخلِ index.html بودند (هیچ
        // data-field ای نداشتند)؛ به‌درخواستِ کاربر برای این‌که هر کدام از
        // طریقِ CardTemplateDesign.TextOverrides قابلِ ویرایشِ محتوا/رنگ/
        // سایز شوند، حالا فیلدِ داده‌ای شدند (نگاه کنید CardService.BuildCardData
        // برای مقدارِ پیش‌فرضِ دقیقاً همان متنِ قبلی، و GuardianCardRenderer.
        // ApplyTextOverrides برای override محتوا).
        public string Besmellah { get; set; }
        public string MottoArabic { get; set; }
        public string MottoTranslation { get; set; }
        public string Kicker { get; set; }
        public string ComplaintMessage { get; set; }
        public string FoundCardMessage { get; set; }

        // مسیرهای نسبی داخل پوشه کاریِ staged (نسبت به index.html)؛ هرکدام خالی
        // بماند، guardian-card.js خودش placeholder را نگه می‌دارد (رفتار موجود آن).
        public string Portrait { get; set; }
        public string Logo { get; set; }
        public string Photo { get; set; }
        public string QRCode { get; set; }
        public string Barcode { get; set; }
        public string Signature { get; set; }
        public string Stamp { get; set; }

        public List<PaymentLedgerRow> PaymentLedger { get; set; } = new List<PaymentLedgerRow>();

        // ─── فقط برای قالبِ «ساده» (simple.html) — نگاه کنید FIELD_MAPPING
        // بالا برای فیلدهای «کامل». این‌ها روی قالبِ کاملِ فعلی هیچ اثری
        // ندارند (index.html هیچ data-field ای با این نام‌ها ندارد).
        public string CaseNo { get; set; }
        public string FamilyPhoto { get; set; }
        public string SimpleNotes { get; set; }
        // آموزش — نسبتِ سرپرست با اعضای خانواده (مثل «پدربزرگ»/«عمو»/...)؛
        // از ستونِ موجودِ TblCase.RelationshipToFamily می‌آید — ستونِ جدیدی
        // در دیتابیس لازم نبود.
        public string RelationshipToFamily { get; set; }
        public List<OrphanRow> Orphans { get; set; } = new List<OrphanRow>();

        // آموزش — برای حالتِ «فقط برای این چاپ» (FrmCardNoticesEdit): یک کپیِ
        // سطحی لازم است تا ویرایشِ موقتِ کاربر روی آبجکتِ در حالِ نمایشِ فعلی
        // اثر نگذارد مگر خودش صریحاً درخواست کند (و هرگز به دیتابیس نرود).
        public GuardianCardData Clone()
        {
            return (GuardianCardData)MemberwiseClone();
        }
    }

    public class PaymentLedgerRow
    {
        public string Month { get; set; }
        public int MonthIndex { get; set; }
        public string PaymentDate { get; set; }
        public string MonthlyAmount { get; set; }
        public string OfficerSignature { get; set; }
        public string RecipientSignature { get; set; }
        // آموزش — سه ستونِ جدیدِ جدولِ پرداخت، به‌درخواستِ کاربر.
        public string Eidi { get; set; }
        public string Fuel { get; set; }
        public string Iftar { get; set; }
    }

    // یک ردیفِ فهرستِ اعضای خانواده — نام، نام پدر، شماره تذکره، عکس (از
    // همهٔ اعضای TblFamily این پرونده؛ نگاه کنید CaseCardRepository.
    // GetOrphans). هم روی قالبِ «ساده» و هم فهرستِ اعضای خانوادهٔ قالبِ
    // «کامل» استفاده می‌شود.
    public class OrphanRow
    {
        public string Name { get; set; }
        public string FatherName { get; set; }
        // آموزش — به‌درخواستِ کاربر: تاریخِ تولد بینِ نامِ پدر و شماره‌
        // تذکره اضافه شد. از TblFamily.BirthDate می‌آید (ستونِ از قبل
        // موجود، جای‌دیگر هم استفاده می‌شود)؛ به‌صورتِ رشتهٔ شمسیِ آماده
        // (نه DateTime خام) چون قراردادِ همینِ کلاس/JSON برایِ IssueDate/
        // ExpiryDate هم همین است. عضوی که تاریخ تولد ثبت‌شده ندارد، رشتهٔ
        // خالی می‌گیرد (نه یک تاریخِ جعلی).
        public string BirthDate { get; set; } = "";
        public string TazkiraNo { get; set; }
        public string Photo { get; set; }
    }
}

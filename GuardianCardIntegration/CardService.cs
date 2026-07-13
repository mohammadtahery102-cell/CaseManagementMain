using System;
using System.Globalization;
using System.Web.Script.Serialization;
using CaseManagement.Helpers;
using CaseManagement.Models;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه تجاری (Business) ماژول کارت شناسایی سرپرست — طبق SOLID:
    // تنها مسئولیتش «تبدیل یک پرونده به داده کارت شناسایی» است؛ نه دسترسی
    // مستقیم به دیتابیس (آن با CaseCardRepository است) و نه رندر/نمایش HTML
    // (آن با GuardianCardRenderer/FrmGuardianCardPreview است). وابستگی به
    // Repository از طریق سازنده تزریق می‌شود (Dependency Inversion) تا قابل
    // تست/جایگزینی باشد.
    // ─────────────────────────────────────────────────────────────────────────
    public class CardService
    {
        private readonly CaseCardRepository _repo;
        private static readonly PersianCalendar _pc = new PersianCalendar();

        public CardService() : this(new CaseCardRepository()) { }

        public CardService(CaseCardRepository repo)
        {
            _repo = repo;
        }

        // داده کامل کارت (برای استفاده مستقیم/آزمایش)
        public GuardianCardData BuildCardData(int caseId)
        {
            CaseModel c = _repo.GetCase(caseId);
            int orphansCount = _repo.GetFamilyMemberCount(caseId);

            DateTime issueDate = DateTime.Today;
            DateTime expiryDate = _pc.AddYears(issueDate, 1);

            var data = new GuardianCardData
            {
                OrganizationName = SettingsHelper.Get(SettingsHelper.OrgName),
                BranchName = SecurityContext.CurrentCenterName,
                BranchCode = SecurityContext.CurrentCenterCode,
                // شماره فرم پرونده (اتومات/یکتا/غیرقابل ویرایش) همان شماره
                // مسلسل کارت است — هیچ شماره جدیدی اختراع نمی‌شود.
                CardNumber = c.FormNo.HasValue ? c.FormNo.Value.ToString("D6") : "",

                PublicCode = c.Code,
                GuardianName = c.HeadFullName,
                FatherName = c.HeadFatherName,
                NationalID = c.HeadTazkiraNo,
                OrphansCount = orphansCount + " نفر",
                IssueDate = PersianDateHelper.ToPersianDateString(issueDate),
                ExpiryDate = PersianDateHelper.ToPersianDateString(expiryDate),

                Province = c.Province,
                District = c.District,
                // آموزش — این پروژه فیلد مجزای «قریه» ندارد (فقط ولایت/ولسوالی)؛
                // به‌جای حدس زدن مقداری نادرست، خالی می‌ماند تا داده اشتباه روی
                // کارت چاپ نشود.
                Village = "",

                // آموزش — Address/Phone/Website/Email روی کارت یعنی اطلاعات
                // تماسِ «دفتر صادرکننده»، نه سرپرست خانواده — طبق FIELD_MAPPING.md
                // ("آدرس دفتر:")، پس از تنظیمات مؤسسه خوانده می‌شوند، نه از پرونده.
                Address = SettingsHelper.Get(SettingsHelper.Address),
                Phone = SettingsHelper.Get(SettingsHelper.Phone),
                Website = SettingsHelper.Get(SettingsHelper.Website),
                Email = SettingsHelper.Get(SettingsHelper.Email),

                IssuedBy = SecurityContext.Username,
                Position = SecurityContext.Role,

                Logo = SettingsHelper.Get(SettingsHelper.LogoPath),
                Photo = c.PhotoPath,

                // آموزش — Notice1‑5 / SignatureLabel / LegalLine در قالب
                // «dom-text-static» هستند: بر خلاف Portrait (که در index.html
                // خودش یک src پیش‌فرض دارد)، این‌ها در HTML خالی‌اند و کاملاً به
                // مقدار تزریق‌شده وابسته‌اند. طبق FIELD_MAPPING.md این متن‌ها
                // «برای کل یک دسته یکسان» هستند (سیاست مؤسسه، نه داده پرونده)،
                // پس همان متن استاندارد مستندسازی‌شده (docs/TEMPLATE_SCHEMA.json)
                // همیشه فرستاده می‌شود تا کارت هرگز بدون این هشدارهای قانونی چاپ نشود.
                Notice1 = "در هنگام توزیع کمک‌ها باید سرپرست حضور داشته باشد.",
                Notice2 = "در هنگام توزیع کمک‌ها این کارت و تذکره اصلی را با خود داشته باشید.",
                Notice3 = "در صورت مفقود و تخریب شدن کارت ۵۰۰ افغانی جریمه می‌شوید.",
                Notice4 = "در هنگام گرفتن کمک‌ها لطفاً پول خود را شمارش کنید.",
                Notice5 = "کوشش شود پول کمک ایتام برای خود آنها (خوراک و پوشاک) مصرف گردد.",
                SignatureLabel = "امضای مسئول دفتر",
                LegalLine = "این کارت شخصی و غیرقابل انتقال است."
            };

            // آموزش — به‌جای اختراع تاریخچه پرداخت جعلی: در این نسخه از پروژه
            // هیچ جدولی رکورد پرداخت شهریه را به یک CasID مشخص وصل نمی‌کند
            // (جدول AccStipend در ماژول حسابداری تجمیعی/بر اساس منطقه است، نه
            // به‌ازای هر پرونده). پس دفترچه پرداخت خالی (۱۲ ماه بدون تاریخ/مبلغ)
            // ساخته می‌شود — دقیقاً همان رفتاری که guardian-card.js برای
            // ماه‌های «هنوز پرداخت‌نشده» از قبل پشتیبانی می‌کند.
            string[] months = { "حمل", "ثور", "جوزا", "سرطان", "اسد", "سنبله", "میزان", "عقرب", "قوس", "جدی", "دلو", "حوت" };
            for (int i = 0; i < 12; i++)
            {
                data.PaymentLedger.Add(new PaymentLedgerRow
                {
                    Month = months[i],
                    MonthIndex = i + 1,
                    PaymentDate = "",
                    MonthlyAmount = "",
                    OfficerSignature = "",
                    RecipientSignature = ""
                });
            }

            return data;
        }

        // خروجی JSON مطابق دقیق قرارداد GuardianCard (کلید هر پراپرتی، طبق
        // پیش‌فرض JavaScriptSerializer، همان نام public property است — که در
        // GuardianCardData.cs عیناً هم‌نام کلیدهای مورد انتظار guardian-card.js
        // نوشته شده — پس نیازی به Attribute یا نگاشت اضافه نیست).
        public string BuildCardJson(int caseId)
        {
            GuardianCardData data = BuildCardData(caseId);
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(data);
        }
    }
}

using System;
using System.Collections.Generic;
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

        // متنِ قابل‌ویرایشِ کارت: اگر مؤسسه در تنظیمات مقداری وارد کرده باشد
        // همان برمی‌گردد، وگرنه متنِ پیش‌فرضِ قبلی. این باعث می‌شود نصب‌های
        // موجود بدون هیچ تنظیمی دقیقاً همان کارتِ قبلی را چاپ کنند.
        private static string CardText(string settingKey, string fallback)
        {
            string value = SettingsHelper.Get(settingKey);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        // داده کامل کارت (برای استفاده مستقیم/آزمایش)
        public GuardianCardData BuildCardData(int caseId)
        {
            CaseModel c = _repo.GetCase(caseId);
            int orphansCount = _repo.GetFamilyMemberCount(caseId);
            bool pendingRoleReview = _repo.HasUnassignedMemberRoles(caseId);

            DateTime issueDate = DateTime.Today;
            DateTime expiryDate = _pc.AddYears(issueDate, 1);

            var data = new GuardianCardData
            {
                CasID = c.CasID,

                // آموزش — متن‌های «مؤسسه‌ای» کارت (نام مؤسسه، تماس، هشدارها،
                // پاورقی، عنوان امضا) حالا از تنظیمات خوانده می‌شوند تا قابل
                // ویرایش باشند. CardText(...) اگر کاربر چیزی وارد نکرده باشد
                // همان مقدار قبلی را برمی‌گرداند، پس رفتار پیش‌فرض عوض نمی‌شود.
                // مشخصاتِ سرپرست (نام/پدر/تذکره/کد/تعداد ایتام) و بارکد همچنان
                // فقط از دیتابیس می‌آیند و قابل ویرایش دستی نیستند.
                OrganizationName = CardText(SettingsHelper.Card_OrgName, SettingsHelper.Get(SettingsHelper.OrgName)),
                // آموزش — به‌درخواستِ کاربر: این خط (زیرِ نامِ سازمان) باید
                // ولایتِ همین پرونده را نشان دهد، نه نامِ مرکز. اگر پرونده
                // ولایت ثبت‌شده نداشته باشد، مثلِ قبل به نامِ مرکز برمی‌گردیم
                // تا خط هرگز خالی نماند.
                BranchName = !string.IsNullOrWhiteSpace(c.Province) ? c.Province : SecurityContext.CurrentCenterName,
                BranchCode = SecurityContext.CurrentCenterCode,
                // شماره فرم پرونده (اتومات/یکتا/غیرقابل ویرایش) همان شماره
                // مسلسل کارت است — هیچ شماره جدیدی اختراع نمی‌شود.
                CardNumber = c.FormNo.HasValue ? c.FormNo.Value.ToString("D6") : "",

                PublicCode = c.Code,
                GuardianName = c.HeadFullName,
                FatherName = c.HeadFatherName,
                NationalID = c.HeadTazkiraNo,
                RequestType = c.RequestType,
                // آموزش — به‌درخواست کاربر: تا نقشِ همه‌ی اعضای این پرونده
                // بازبینی نشده، به‌جای عددِ (بالقوه گمراه‌کننده‌ی) صفر، پیام
                // صریح «نیاز به تعیین نقش» چاپ می‌شود (نگاه کنید
                // CaseCardRepository.HasUnassignedMemberRoles).
                OrphansCount = pendingRoleReview ? "نیاز به تعیین نقش" : (orphansCount + " نفر"),
                IssueDate = PersianDateHelper.ToPersianDateString(issueDate),
                ExpiryDate = PersianDateHelper.ToPersianDateString(expiryDate),

                Province = c.Province,
                District = c.District,
                // آموزش — رفع باگ «همیشه نوشته می‌شد بامیان»: نوار تزئینی دور
                // کارت قبلاً در خودِ index.html به یک نام مرکز ثابت (بامیان)
                // Hardcode شده بود. حالا طبق ولایتِ واقعیِ همین پرونده ساخته
                // می‌شود؛ اگر ولایت ثبت نشده باشد، به نام مرکز فعال برمی‌گردیم
                // تا نوار هرگز خالی/گمراه‌کننده نماند.
                MicrotextLabel = CardText(SettingsHelper.Card_MicrotextLabel, "دفتر نمایندگی")
                                 + " " + (string.IsNullOrWhiteSpace(c.Province) ? SecurityContext.CurrentCenterName : c.Province),
                // آموزش — این پروژه فیلد مجزای «قریه» ندارد (فقط ولایت/ولسوالی)؛
                // به‌جای حدس زدن مقداری نادرست، خالی می‌ماند تا داده اشتباه روی
                // کارت چاپ نشود.
                Village = "",

                // آموزش — Address/Phone/Website/Email روی کارت یعنی اطلاعات
                // تماسِ «دفتر صادرکننده»، نه سرپرست خانواده — طبق FIELD_MAPPING.md
                // ("آدرس دفتر:")، پس از تنظیمات مؤسسه خوانده می‌شوند، نه از پرونده.
                // استثنا — به‌درخواست کاربر («CARD CUSTOM CONTENT EDITING»): اگر
                // این پرونده override اختصاصی (CardPhone/CardAddress) داشته
                // باشد (مثلاً یک شمارهٔ تماسِ محلیِ متفاوت برای همین خانواده)،
                // همان override اولویت دارد؛ وگرنه دقیقاً رفتار قبلی (تنظیمات
                // مؤسسه) ادامه پیدا می‌کند.
                Address = !string.IsNullOrWhiteSpace(c.CardAddress) ? c.CardAddress
                          : CardText(SettingsHelper.Card_Address, SettingsHelper.Get(SettingsHelper.Address)),
                Phone = !string.IsNullOrWhiteSpace(c.CardPhone) ? c.CardPhone
                          : CardText(SettingsHelper.Card_Phone, SettingsHelper.Get(SettingsHelper.Phone)),
                Website = CardText(SettingsHelper.Card_Website, SettingsHelper.Get(SettingsHelper.Website)),
                Email = CardText(SettingsHelper.Card_Email, SettingsHelper.Get(SettingsHelper.Email)),

                IssuedBy = CardText(SettingsHelper.Card_IssuedBy, SecurityContext.Username),
                Position = CardText(SettingsHelper.Card_Position, SecurityContext.Role),

                Logo = SettingsHelper.Get(SettingsHelper.LogoPath),
                Photo = c.PhotoPath,
                // آموزش — امضا/مهر مؤسسه‌ای هستند (یکسان روی همه کارت‌ها)، نه
                // داده‌ی پرونده؛ از همان کلیدهای تنظیماتِ از قبل موجود خوانده
                // می‌شوند (SignaturePath/StampPath — قبلاً در تب چاپ نیمه‌کاره
                // تعریف شده بودند ولی هیچ UI برایشان ساخته نشده بود؛ اکنون در
                // تب «اطلاعات مؤسسه» آپلود می‌شوند). مسیر مطلق اینجا؛ Stage
                // واقعی (کپی داخل پوشه کاری + مسیر نسبی) در GuardianCardRenderer.
                Signature = SettingsHelper.Get(SettingsHelper.SignaturePath),
                Stamp = SettingsHelper.Get(SettingsHelper.StampPath),

                // آموزش — Notice1‑5 / SignatureLabel / LegalLine در قالب
                // «dom-text-static» هستند: بر خلاف Portrait (که در index.html
                // خودش یک src پیش‌فرض دارد)، این‌ها در HTML خالی‌اند و کاملاً به
                // مقدار تزریق‌شده وابسته‌اند. طبق FIELD_MAPPING.md این متن‌ها
                // «برای کل یک دسته یکسان» هستند (سیاست مؤسسه، نه داده پرونده)،
                // پس همان متن استاندارد مستندسازی‌شده (docs/TEMPLATE_SCHEMA.json)
                // همیشه فرستاده می‌شود تا کارت هرگز بدون این هشدارهای قانونی چاپ نشود.
                // آموزش — قابلیت جدید: هر پرونده می‌تواند شرایط/هشدارهای کارتِ
                // خودش را داشته باشد (CardNoticeN روی TblCase). اگر کاربر برای
                // این پرونده مقداری وارد نکرده باشد (خالی/NULL)، دقیقاً مثل
                // قبل به متنِ سراسریِ تنظیمات مؤسسه برمی‌گردیم — نصب‌های موجود
                // بدون هیچ ویرایشی همان کارتِ قبلی را چاپ می‌کنند.
                Notice1 = !string.IsNullOrWhiteSpace(c.CardNotice1) ? c.CardNotice1
                          : CardText(SettingsHelper.Card_Notice1, "در هنگام توزیع کمک‌ها باید سرپرست حضور داشته باشد."),
                Notice2 = !string.IsNullOrWhiteSpace(c.CardNotice2) ? c.CardNotice2
                          : CardText(SettingsHelper.Card_Notice2, "در هنگام توزیع کمک‌ها این کارت و تذکره اصلی را با خود داشته باشید."),
                Notice3 = !string.IsNullOrWhiteSpace(c.CardNotice3) ? c.CardNotice3
                          : CardText(SettingsHelper.Card_Notice3, "در صورت مفقود و تخریب شدن کارت ۵۰۰ افغانی جریمه می‌شوید."),
                Notice4 = !string.IsNullOrWhiteSpace(c.CardNotice4) ? c.CardNotice4
                          : CardText(SettingsHelper.Card_Notice4, "در هنگام گرفتن کمک‌ها لطفاً پول خود را شمارش کنید."),
                Notice5 = !string.IsNullOrWhiteSpace(c.CardNotice5) ? c.CardNotice5
                          : CardText(SettingsHelper.Card_Notice5, "کوشش شود پول کمک ایتام برای خود آنها (خوراک و پوشاک) مصرف گردد."),
                SignatureLabel = CardText(SettingsHelper.Card_SignatureLabel, "امضای مسئول دفتر"),
                LegalLine = !string.IsNullOrWhiteSpace(c.CardLegalLine) ? c.CardLegalLine
                          : CardText(SettingsHelper.Card_LegalLine, "این کارت شخصی و غیرقابل انتقال است."),

                // آموزش — فقط برای قالبِ «ساده» (simple.html)؛ نگاه کنید
                // GuardianCardData.CaseNo/FamilyPhoto/SimpleNotes/Orphans.
                // SimpleNotes از همان CardNotice1 می‌آید (بدونِ ستونِ جدید در
                // دیتابیس)، دقیقاً طبقِ تصمیمِ تأییدشده.
                CaseNo = c.CaseNo,
                FamilyPhoto = c.FamilyPhotoPath,
                SimpleNotes = c.CardNotice1,
                RelationshipToFamily = c.RelationshipToFamily,
                Orphans = _repo.GetOrphans(caseId),

                // آموزش — پیش‌فرض‌ها دقیقاً همان متنِ ثابتی است که قبلاً در
                // خودِ index.html نوشته شده بود؛ نصب‌های موجود بدونِ override
                // دقیقاً همان کارتِ قبلی را می‌بینند. override واقعی (اگر
                // قالب تنظیم کرده باشد) در GuardianCardRenderer.ApplyTextOverrides
                // اعمال می‌شود.
                Besmellah = "بسمه‌تعالی",
                MottoArabic = "الله فی الایتام",
                MottoTranslation = "(خدا را در رابطه با ایتام در نظر بگیرید) — حضرت علی",
                Kicker = "کارت شناسایی سرپرست ایتام",
                ComplaintMessage = "در صورت داشتن هرگونه شکایت، با شماره‌های فوق تماس حاصل فرمائید.",
                FoundCardMessage = "در صورت پیدا کردن کارت، لطفاً آن را به آدرس یادشده ارسال یا با شماره‌های تماس اطلاع دهید."
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
                    RecipientSignature = "",
                    Eidi = "",
                    Fuel = "",
                    Iftar = ""
                });
            }

            return data;
        }

        // آموزش — نسخه‌ی عمومی‌ترِ چاپ جمعی: به‌جای بازه‌ی شماره فرم، یک
        // فهرستِ صریحِ CasID می‌گیرد (خروجیِ CaseCardRepository.PreviewBatch،
        // بعد از اینکه کاربر پیش‌نمایش را دیده و تأیید کرده). یک پرونده‌ی
        // خراب/ناقص کل دسته را متوقف نمی‌کند — فقط از فهرست حذف می‌شود و
        // شمارنده‌ی خطا بالا می‌رود.
        public List<GuardianCardData> BuildCardDataForCaseIds(IEnumerable<int> caseIds, out int failedCount)
        {
            failedCount = 0;
            var result = new List<GuardianCardData>();
            foreach (int caseId in caseIds)
            {
                try
                {
                    result.Add(BuildCardData(caseId));
                }
                catch
                {
                    failedCount++;
                }
            }
            return result;
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

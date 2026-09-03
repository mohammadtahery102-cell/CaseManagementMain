using System.Collections.Generic;

namespace CaseManagement.GuardianCardIntegration.CardDesigner
{
    // ─────────────────────────────────────────────────────────────────────────
    // دادهٔ نمونهٔ داخلیِ طراحِ کارت.
    //
    // چرا لازم است؟ تا پیش از این، اگر هیچ پروندهٔ فعالی پیدا نمی‌شد،
    // پیش‌نمایش فقط پیامِ «هیچ پرونده‌ای برای پیش‌نمایشِ زنده پیدا نشد» را
    // نشان می‌داد و کلِ طراح عملاً بی‌استفاده می‌شد — دقیقاً همان حالتی که
    // یک نصبِ تازه (بدونِ پرونده) یا کاربری با فیلترِ مرکز دارد.
    //
    // این کلاس یک پروندهٔ ساختگیِ کامل می‌سازد تا:
    //   ۱) طراح همیشه یک کارتِ واقعی نشان دهد، نه یک پنلِ خالی؛
    //   ۲) طول‌های بدترین‌حالت (نامِ بلند، ۱۲ ردیفِ جدولِ پرداخت، چند عضو)
    //      همان‌جا دیده شوند و سرریزِ چاپ قبل از چاپ لو برود.
    //
    // هیچ‌جا ذخیره نمی‌شود و هرگز چاپ نمی‌شود — فقط ورودیِ پیش‌نمایش است.
    // مسیرهای تصویر عمداً خالی‌اند تا guardian-card.js همان placeholderهای
    // خودش را نگه دارد (رفتارِ مستندشدهٔ همان بسته).
    // ─────────────────────────────────────────────────────────────────────────
    public static class DemoCardData
    {
        private static readonly string[] SolarMonths =
        {
            "حمل", "ثور", "جوزا", "سرطان", "اسد", "سنبله",
            "میزان", "عقرب", "قوس", "جدی", "دلو", "حوت"
        };

        public static GuardianCardData Build()
        {
            var d = new GuardianCardData
            {
                CasID = 0,

                OrganizationName = "کارت هویت ایتام",
                BranchName = "ولایت بامیان",
                BranchCode = "BMN",
                CardNumber = "000000",

                PublicCode = "۱۴۰۵-۰۰۰۱",
                GuardianName = "محمدنعیم رضایی",
                FatherName = "عبدالرحمن رضایی",
                NationalID = "۱۴۰۵-۱۱۲۲-۳۳۴۴۵",
                RequestType = "ایتام",
                OrphansCount = "۳",
                IssueDate = "۱۴۰۵/۰۱/۱۵",
                ExpiryDate = "۱۴۰۸/۰۱/۱۵",

                Province = "بامیان",
                District = "یکاولنگ",
                Village = "قریهٔ سرِ آسیاب",
                MicrotextLabel = "بامیان",

                Address = "بامیان، مرکز ولایت، جادهٔ عمومی",
                Phone = "۰۷۰۰ ۰۰۰ ۰۰۰",
                Website = "example.org",
                Email = "info@example.org",

                IssuedBy = "مسئول دفتر",
                Position = "مدیر بخش ایتام",

                CaseNo = "۱۲۴۰",
                RelationshipToFamily = "پدربزرگ",
                SimpleNotes = "نمونهٔ نمایشی — این کارت از دادهٔ واقعی ساخته نشده است.",

                // مسیرهای تصویر خالی = placeholderهای خودِ بسته حفظ می‌شوند.
                Portrait = "", Logo = "", Photo = "",
                QRCode = "", Barcode = "", Signature = "", Stamp = "",
                FamilyPhoto = ""
            };

            for (int i = 0; i < 12; i++)
            {
                d.PaymentLedger.Add(new PaymentLedgerRow
                {
                    Month = SolarMonths[i],
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

            d.Orphans.Add(new OrphanRow
            {
                Name = "زهرا رضایی", FatherName = "محمدعلی رضایی",
                BirthDate = "۱۳۹۵/۰۳/۱۰", TazkiraNo = "۱۳۹۵-۱۰۱", Photo = ""
            });
            d.Orphans.Add(new OrphanRow
            {
                Name = "احمد رضایی", FatherName = "محمدعلی رضایی",
                BirthDate = "۱۳۹۷/۰۷/۲۲", TazkiraNo = "۱۳۹۷-۱۰۲", Photo = ""
            });
            d.Orphans.Add(new OrphanRow
            {
                Name = "فاطمه رضایی", FatherName = "محمدعلی رضایی",
                BirthDate = "۱۴۰۰/۱۱/۰۵", TazkiraNo = "۱۴۰۰-۱۰۳", Photo = ""
            });

            return d;
        }
    }
}

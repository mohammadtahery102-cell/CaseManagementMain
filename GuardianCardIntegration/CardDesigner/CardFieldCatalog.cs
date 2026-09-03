using System;
using System.Collections.Generic;

namespace CaseManagement.GuardianCardIntegration.CardDesigner
{
    // ─────────────────────────────────────────────────────────────────────────
    // فراداده‌ی هر «مورد» روی کارت — برچسبِ فارسی، گروهِ منطقی، منبعِ داده
    // (به زبانِ کاربر) و نامِ فنیِ ستون (فقط برای Tooltip).
    //
    // چرا یک جدولِ ثابتِ درون‌برنامه‌ای و نه خواندن از دیتابیس؟ چون این
    // نگاشت بخشی از *قرارداد رندر* است (کدام data-field از کدام ستون پر
    // می‌شود — نگاه کنید CardService.BuildCardData)، نه دادهٔ کاربر. با
    // ذخیره‌کردنش در دیتابیس، تغییرِ اسکیمای دیتابیس لازم می‌شد که صریحاً
    // ممنوع است.
    //
    // ⚠ نگاشت‌ها از رویِ خودِ CardService.BuildCardData استخراج شده‌اند، نه
    // از رویِ حدس. مثلاً «نام سرپرست» از TblCase.HeadFullName می‌آید
    // (نه GuardianName) و «کد عمومی» از TblCase.Code (نه PublicCode) —
    // نامِ فیلدِ روی کارت با نامِ ستونِ دیتابیس یکی نیست.
    // ─────────────────────────────────────────────────────────────────────────
    public class CardFieldInfo
    {
        public string Key;
        public string Label;
        public string Group;
        public string SourceText;   // متنِ دوستانه برای نمایش
        public string SourceTech;   // نامِ فنی، فقط در Tooltip
        public bool CanEditText;    // آیا محتوای متن اینجا قابلِ تغییر است؟

        public CardFieldInfo(string key, string label, string group,
                             string sourceText, string sourceTech, bool canEditText)
        {
            Key = key; Label = label; Group = group;
            SourceText = sourceText; SourceTech = sourceTech; CanEditText = canEditText;
        }
    }

    public static class CardFieldCatalog
    {
        public const string GroupGuardian = "مشخصات سرپرست";
        public const string GroupPhotos = "عکس‌ها";
        public const string GroupFamily = "خانواده";
        public const string GroupHeader = "هدر کارت";
        public const string GroupOrg = "سازمان و تماس";
        public const string GroupSign = "امضا و مهر";
        public const string GroupOther = "سایر";

        private const string SrcCase = "پروندهٔ مددجو";
        private const string SrcFamily = "اعضای خانواده";
        private const string SrcSettings = "تنظیمات سازمان";
        private const string SrcFixed = "متن ثابت کارت";
        private const string SrcComputed = "محاسبه‌شده";

        // ترتیبِ گروه‌ها در نمایش — عمداً از «چیزی که کاربر بیشتر عوض
        // می‌کند» به «چیزی که کمتر عوض می‌کند».
        public static readonly string[] GroupOrder =
        {
            GroupGuardian, GroupPhotos, GroupFamily, GroupHeader, GroupOrg, GroupSign, GroupOther
        };

        private static readonly Dictionary<string, CardFieldInfo> Full =
            BuildDictionary(new[]
            {
                new CardFieldInfo("GuardianName", "نام سرپرست", GroupGuardian, SrcCase + " ← نام سرپرست", "TblCase.HeadFullName", false),
                new CardFieldInfo("FatherName", "نام پدر سرپرست", GroupGuardian, SrcCase + " ← نام پدر", "TblCase.HeadFatherName", false),
                new CardFieldInfo("NationalID", "شماره تذکره", GroupGuardian, SrcCase + " ← شماره تذکره", "TblCase.HeadTazkiraNo", false),
                new CardFieldInfo("RequestType", "نوع مددجو", GroupGuardian, SrcCase + " ← نوع درخواست", "TblCase.RequestType", false),
                new CardFieldInfo("PublicCode", "کد عمومی", GroupGuardian, SrcCase + " ← کد", "TblCase.Code", false),

                new CardFieldInfo("Portrait", "عکس تزئینی هدر", GroupPhotos, "تصویر ثابت بستهٔ کارت", "GuardianCard/images", false),
                new CardFieldInfo("FamilyPhoto", "عکس جمعی خانواده", GroupPhotos, SrcCase + " ← عکس جمعی", "TblCase.FamilyPhotoPath", false),
                new CardFieldInfo("FamilyListPhotos", "عکس هر عضو", GroupPhotos, SrcFamily + " ← عکس عضو", "TblFamily.MemberPhotoPath", false),

                new CardFieldInfo("FamilyList", "فهرست اعضای خانواده", GroupFamily, SrcFamily + " (چند ردیف)", "TblFamily", false),
                new CardFieldInfo("OrphansCount", "تعداد ایتام", GroupFamily, SrcFamily + " ← شمارش نقش «یتیم»", "TblFamily.MemberRole='یتیم'", false),

                new CardFieldInfo("Besmellah", "بسمه‌تعالی", GroupHeader, SrcFixed, "TextOverrides.Besmellah", true),
                new CardFieldInfo("OrganizationName", "تیتر بزرگ هدر", GroupHeader, SrcSettings + " ← نام سازمان", "Settings.OrganizationName", true),
                new CardFieldInfo("BranchName", "خط ولایت زیر تیتر", GroupHeader, SrcCase + " ← ولایت", "TblCase.Province", false),
                new CardFieldInfo("CardCode", "شناسه کارت", GroupHeader, SrcCase + " ← شمارهٔ فورم", "TblCase.FormNo", false),

                new CardFieldInfo("Address", "آدرس دفتر", GroupOrg, SrcSettings + " ← آدرس", "Settings.Card_Address", false),
                new CardFieldInfo("Phone", "شماره تماس", GroupOrg, SrcCase + " ← تماس کارت، وگرنه تنظیمات", "TblCase.CardPhone", true),
                new CardFieldInfo("Website", "وبسایت", GroupOrg, SrcSettings + " ← وبسایت", "Settings.Card_Website", false),
                new CardFieldInfo("Email", "ایمیل", GroupOrg, SrcSettings + " ← ایمیل", "Settings.Card_Email", true),

                new CardFieldInfo("Logo", "لوگوی مؤسسه", GroupSign, SrcSettings + " ← فایل لوگو", "Settings.LogoPath", false),
                new CardFieldInfo("Signature", "امضا", GroupSign, SrcSettings + " ← فایل امضا", "Settings.SignaturePath", false),
                new CardFieldInfo("Stamp", "مهر", GroupSign, SrcSettings + " ← فایل مهر", "Settings.StampPath", false),
                new CardFieldInfo("IssuedBy", "نام صادرکننده", GroupSign, SrcSettings, "Settings.Card_IssuedBy", false),
                new CardFieldInfo("Position", "سمت صادرکننده", GroupSign, SrcSettings, "Settings.Card_Position", false)
            });

        private static readonly Dictionary<string, CardFieldInfo> Simple =
            BuildDictionary(new[]
            {
                new CardFieldInfo("GuardianName", "نام سرپرست", GroupGuardian, SrcCase + " ← نام سرپرست", "TblCase.HeadFullName", false),
                new CardFieldInfo("FatherName", "نام پدر سرپرست", GroupGuardian, SrcCase + " ← نام پدر", "TblCase.HeadFatherName", false),
                new CardFieldInfo("NationalID", "شماره تذکره", GroupGuardian, SrcCase + " ← شماره تذکره", "TblCase.HeadTazkiraNo", false),
                new CardFieldInfo("PublicCode", "کد اختصاصی", GroupGuardian, SrcCase + " ← کد", "TblCase.Code", false),
                new CardFieldInfo("CaseNo", "شمارهٔ پرونده", GroupGuardian, SrcCase + " ← شمارهٔ پرونده", "TblCase.CaseNo", false),
                new CardFieldInfo("RelationshipToFamily", "نسبت سرپرست با اعضاء", GroupGuardian, SrcCase + " ← نسبت", "TblCase.RelationshipToFamily", false),

                new CardFieldInfo("Photo", "عکس سرپرست", GroupPhotos, SrcCase + " ← عکس", "TblCase.PhotoPath", false),
                new CardFieldInfo("FamilyPhoto", "عکس جمعی", GroupPhotos, SrcCase + " ← عکس جمعی", "TblCase.FamilyPhotoPath", false),

                new CardFieldInfo("Orphans", "نام‌های ایتام", GroupFamily, SrcFamily, "TblFamily", false),

                new CardFieldInfo("Province", "ولایت", GroupOrg, SrcCase + " ← ولایت", "TblCase.Province", false),
                new CardFieldInfo("District", "ولسوالی", GroupOrg, SrcCase + " ← ولسوالی", "TblCase.District", false),
                new CardFieldInfo("Phone", "شماره تماس", GroupOrg, SrcCase + " ← تماس کارت، وگرنه تنظیمات", "TblCase.CardPhone", true),

                new CardFieldInfo("IssueDate", "تاریخ صدور کارت", GroupOther, SrcComputed + " ← تاریخ صدور", "CardService.IssueDate", false),
                new CardFieldInfo("SimpleNotes", "تذکرات", GroupOther, SrcCase + " ← تذکر ۱", "TblCase.CardNotice1", true),
                new CardFieldInfo("Thumbprint", "محل شصت", GroupOther, SrcFixed, "—", false)
            });

        // آموزش — فقط این کلیدها در HTML یک عنصرِ متنیِ نشان‌دار دارند که
        // GuardianCardRenderer.ApplyTextOverrides رنگ/اندازه/قلمش را عوض
        // می‌کند (عیناً همان فهرستِ TextOverrideFieldKeys در فرم). برای
        // بقیه — لوگو، مهر، امضا، عکس‌ها، فهرست اعضا — تنظیمِ رنگ/اندازه
        // هیچ اثری ندارد، پس نباید اصلاً نشان داده شود؛ وگرنه کاربر چیزی
        // تنظیم می‌کند که بی‌صدا نادیده گرفته می‌شود.
        private static readonly HashSet<string> TypographyCapable = new HashSet<string>(StringComparer.Ordinal)
        {
            "OrganizationName", "Besmellah", "MottoArabic", "MottoTranslation", "Kicker",
            "Address", "Phone", "Website", "Email", "ComplaintMessage", "FoundCardMessage",
            "GuardianName", "FatherName", "NationalID", "RequestType", "PublicCode",
            "Notice1", "Notice2", "Notice3", "Notice4", "Notice5"
        };

        public static bool SupportsTypography(string key)
        {
            return key != null && TypographyCapable.Contains(key);
        }

        private static Dictionary<string, CardFieldInfo> BuildDictionary(CardFieldInfo[] items)
        {
            var d = new Dictionary<string, CardFieldInfo>(StringComparer.Ordinal);
            for (int i = 0; i < items.Length; i++) d[items[i].Key] = items[i];
            return d;
        }

        // آموزش — اگر کلیدی در جدول نبود (مثلاً فیلدی در آینده به
        // ToggleableFields اضافه شود ولی اینجا فراموش شود)، به‌جای استثنا یک
        // ردیفِ حداقلی برمی‌گردد تا آن مورد از UI ناپدید نشود.
        public static CardFieldInfo Get(string key, bool simple)
        {
            Dictionary<string, CardFieldInfo> map = simple ? Simple : Full;
            CardFieldInfo info;
            if (map.TryGetValue(key, out info)) return info;
            return new CardFieldInfo(key, key, GroupOther, "—", key, false);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // ستون‌های قابل انتخابِ گریدِ «لیست پرونده‌ها» در فرم پرونده.
    //
    // آموزش — چرا یک کلاس جدا: هم فرم پرونده (که گرید را می‌سازد) و هم فرم
    // تنظیمات (که فهرست انتخاب را نشان می‌دهد) به همین یک فهرست نیاز دارند.
    // اگر هرکدام نسخه‌ی خودش را داشت، با اولین تغییر از هم دور می‌افتادند.
    //
    // «کد اختصاصی» عمداً در این فهرست نیست: همیشه و بدونِ امکانِ برداشتن نمایش
    // داده می‌شود، چون شناسه‌ی اصلیِ پرونده است و بدون آن ردیف‌ها قابل تشخیص
    // نیستند. کاربر علاوه بر آن حداکثر MaxSelectable ستون انتخاب می‌کند تا
    // گرید بدون اسکرول افقی در پنل جا شود.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class CaseGridColumn
    {
        public string Key;          // کلید ذخیره‌شده در تنظیمات
        public string DisplayName;  // عنوان ستون در گرید و فهرست انتخاب
        public string DataColumn;   // نام ستون در نتیجه‌ی کوئری LoadCases
        public bool IsPhoto;        // ستون تصویری (thumbnail) به‌جای متن

        public CaseGridColumn(string key, string displayName, string dataColumn, bool isPhoto)
        {
            Key = key; DisplayName = displayName; DataColumn = dataColumn; IsPhoto = isPhoto;
        }
    }

    public static class CaseGridColumns
    {
        // ستونِ همیشگی — نه در فهرست انتخاب است و نه در سقفِ انتخاب حساب می‌شود.
        public const string FixedColumn = "Code";
        public const string FixedColumnTitle = "کد اختصاصی";

        // آموزش — سقف از ۴ به ۵ رفت چون چیدمانِ خواسته‌شدهٔ گرید (تصویرِ
        // مرجعِ FrmCase) شش ستون دارد: «کد اختصاصی»ِ ثابت + پنج ستونِ انتخابی
        // (نام سرپرست، نوع پرونده، ولسوالی، شماره تذکره، عکس). با سقفِ ۴،
        // یکی از این‌ها همیشه از قلم می‌افتاد.
        public const int MaxSelectable = 5;

        public static readonly List<CaseGridColumn> Available = new List<CaseGridColumn>
        {
            new CaseGridColumn("HeadFullName",         "نام سرپرست",        "HeadFullName",         false),
            // «نوع پرونده» تا امروز در کاتالوگ نبود، پس اصلاً قابلِ انتخاب نبود؛
            // ستونِ TblCase.RequestType از قبل وجود داشت و در فرم هم پر می‌شود.
            new CaseGridColumn("RequestType",          "نوع پرونده",        "RequestType",          false),
            new CaseGridColumn("HeadFatherName",       "نام پدر سرپرست",    "HeadFatherName",       false),
            new CaseGridColumn("HeadTazkiraNo",        "شماره تذکره",       "HeadTazkiraNo",        false),
            new CaseGridColumn("Phone",                "شماره تماس",        "Phone",                false),
            new CaseGridColumn("Photo",                "عکس",               "PhotoPath",            true),
            new CaseGridColumn("HeadCurrentResidence", "آدرس (سکونت فعلی)", "HeadCurrentResidence", false),
            new CaseGridColumn("Province",             "ولایت",             "Province",             false),
            new CaseGridColumn("District",             "ولسوالی",           "District",             false)
        };

        // پیش‌فرض = دقیقاً ستون‌های تصویرِ مرجعِ FrmCase. ترتیب مهم است:
        // ConfigureCasesGrid ستونِ ثابت را در جایگاهِ ۰ می‌گذارد و بقیه را به
        // همین ترتیب پشتِ آن، و چون گرید راست‌به‌چپ است جایگاهِ ۰ سمتِ راست
        // می‌نشیند. نتیجه از راست به چپ:
        //   کد اختصاصی │ نام سرپرست │ نوع پرونده │ ولسوالی │ شماره تذکره │ عکس
        public static readonly string[] DefaultKeys =
        {
            "HeadFullName", "RequestType", "District", "HeadTazkiraNo", "Photo"
        };

        public static CaseGridColumn Find(string key)
        {
            return Available.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        // ستون‌های انتخاب‌شده از تنظیمات. مقدارِ نامعتبر/خالی → پیش‌فرض، و
        // بیش از سقف → فقط MaxSelectable تای اول. پس گرید هرگز به‌خاطر یک
        // مقدارِ خرابِ تنظیمات بدون ستون یا بیش‌ازحد شلوغ نمی‌شود.
        public static List<CaseGridColumn> GetSelected()
        {
            string raw = SettingsHelper.Get(SettingsHelper.CaseGridColumns);

            List<CaseGridColumn> chosen = ParseKeys(raw);

            if (chosen.Count == 0)
                chosen = DefaultKeys.Select(Find).Where(c => c != null).ToList();

            return chosen.Take(MaxSelectable).ToList();
        }

        public static List<CaseGridColumn> ParseKeys(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return new List<CaseGridColumn>();

            return csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(k => Find(k.Trim()))
                      .Where(c => c != null)
                      .Distinct()
                      .ToList();
        }

        public static string ToCsv(IEnumerable<CaseGridColumn> columns)
        {
            return string.Join(",", columns.Take(MaxSelectable).Select(c => c.Key));
        }
    }
}

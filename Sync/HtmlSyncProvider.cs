using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ─────────────────────────────────────────────────────────────────────────
    // HtmlSyncProvider — منبع همگام‌سازی از خروجی HTML سامانه مرکزی.
    //
    // دو فایل ورودی: (۱) سرپرستان (۲) اعضای خانواده. «کد عمومی» = کد اختصاصی
    // سرپرست (TblCase.Code) و تنها مبنای ارتباط است. فایل اعضا کد مستقل ندارد؛
    // همان کد عمومی سرپرست در هر ردیفِ عضو تکرار می‌شود — اگر یک خانواده ۱۰ یتیم
    // داشته باشد، همان کد عمومی ۱۰ بار در فایل اعضا می‌آید و هر ۱۰ ردیف زیر همان
    // پرونده (CasID) اضافه می‌شوند.
    //
    // کالیبره‌شده روی فایل واقعی کاربر (۱۴۰۵/۰۴/۱۴):
    //   • فایل سرپرستان: ردیف, کد عمومی, نام, نام پدر, وضعیت تأهل, تاریخ تولد,
    //     ش تذکره, سیادت, تلفن همراه, آدرس, قطع, شماره پرونده, ولایت, تعداد یتیم,
    //     ولسوالی, مذهب, وضعیت خدمات, وضعیت تذکره
    //     - «آدرس» = محل سکونت فعلی سرپرست.
    //     - «شماره پرونده» → ستون موجود TblCase.CaseNo.
    //     - قطع/تعداد یتیم/وضعیت تذکره/تاریخ تولد سرپرست: بدون ستون متناظر در
    //       دیتابیس فعلی → عمداً نادیده گرفته می‌شوند (طبق تصمیم کاربر).
    //   • فایل اعضا: ردیف, کد عمومی, نام, نام پدر, ش تذکره, تاریخ تولد, تلفن همراه,
    //     نام(۲), تاریخ تولد(۲), ش تذکره(۲), سیادت, جنسیت, وضعیت خدمات
    //     - نام/تاریخ تولد/ش‌تذکره اول = خود عضو.
    //     - نام/تاریخ تولد/ش‌تذکره دوم (تکراری) = کفیل/سرپرست دیگر — چون ستون
    //       متناظر در TblFamily نداریم، به‌صورت یادداشت در ستون عمومی «Details»
    //       نوشته می‌شود (قابل مشاهده و انتخاب/رد در Wizard، نه بازنویسی خاموش).
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class HtmlSyncProvider : IDataSyncProvider
    {
        public string Name { get { return "خروجی HTML سامانه مرکزی"; } }

        // عناوین ممکن ستون «کد عمومی» (= کد اختصاصی سرپرست، کلید خانواده).
        private static readonly string[] PublicCodeHeaders =
            { "کد عمومی", "کد اختصاصی", "کد خانواده", "کد فامیل", "کد" };

        // نگاشت ستون دیتابیس TblCase → عناوین ستون در فایل سرپرستان (occurrence=1).
        private static readonly Dictionary<string, string[]> GuardianFieldMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "HeadFullName",         new[] { "نام", "نام سرپرست", "نام و تخلص", "نام کامل" } },
            { "HeadFatherName",       new[] { "نام پدر", "ولد" } },
            { "HeadTazkiraNo",        new[] { "ش تذکره", "شماره تذکره", "تذکره" } },
            // آموزش — فایل سرپرستان ستون «تاریخ تولد» دارد ولی تا امروز به هیچ
            // ستونی نگاشت نشده بود، پس تاریخ تولد سرپرست هرگز وارد نمی‌شد
            // (با اجرای واقعیِ Provider روی فایل واقعی تأیید شد: ۴۳۵ سرپرست،
            // صفر تاریخ تولد). ستون HeadBirthDate در DatabaseInitializer
            // به‌صورت افزایشی به TblCase اضافه می‌شود.
            { "HeadBirthDate",        new[] { "تاریخ تولد" } },
            { "HeadSadat",            new[] { "سیادت" } },
            { "Phone",                new[] { "تلفن همراه", "شماره تماس", "تلفن", "موبایل" } },
            { "HeadCurrentResidence", new[] { "آدرس" } },
            { "CaseNo",               new[] { "شماره پرونده" } },
            { "Province",             new[] { "ولایت", "استان" } },
            { "District",             new[] { "ولسوالی", "شهرستان" } },
            { "Religion",             new[] { "مذهب" } },
            { "MaritalStatus",        new[] { "وضعیت تأهل", "تاهل" } },
            { "ServiceStatus",        new[] { "وضعیت خدمات" } },
            // سروی‌کننده: در فایل فعلی نیست؛ در فایل بعدی با عنوان «مشخصات سروی
            // کننده» می‌آید و به ستون Surveyors سرپرست نگاشت می‌شود. تا وقتی نیاید
            // خالی می‌ماند (بی‌خطر).
            { "Surveyors",            new[] { "مشخصات سروی کننده", "سروی کننده", "سروی کنندگان", "سروی‌کننده" } },
        };

        // ─── عناوینِ هم‌معنیِ ستون‌های فایل اعضا ──────────────────────────────
        // آموزش — رفع باگ «شماره تذکره و تاریخ تولدِ اعضا وارد نمی‌شد»: فهرست
        // عناوین قبلی بسیار تنگ بود (فقط «ش تذکره»/«تاریخ تولد»). در خروجی‌های
        // واقعی همان ستون با نوشتارهای دیگری می‌آید («نمبر تذکره»، «ش. تذکره»،
        // «سال تولد» و ...) و چون هیچ عنوانی match نمی‌شد، مقدار بی‌صدا حذف
        // می‌شد. این آرایه‌ها مرجع واحدِ هر دو مسیرِ «تشخیص گروه» و «خواندن
        // مقدار» هستند.
        private static readonly string[] NameHeaders =
            { "نام", "نام عضو", "اسم", "نام و تخلص", "نام کامل" };
        private static readonly string[] FatherHeaders =
            { "نام پدر", "ولد", "نام والد", "اسم پدر" };
        // ⚠ واژهٔ عامِ «تذکره» عمداً در این فهرست نیست.
        // آموزش — رفع باگِ «شمارهٔ تذکرهٔ اعضا وارد نمی‌شود»: در خروجیِ سامانهٔ
        // مرکزی یک ستونِ جداگانه با عنوانِ دقیقِ «تذکره» وجود دارد که مقدارش
        // «بلي/خير» است (یعنی «تذکره دارد یا نه»)، نه شماره. اگر «تذکره» در این
        // فهرست باشد، هم ستونِ «ش تذکره» (شمارهٔ واقعی) و هم ستونِ «تذکره»
        // (بلي/خير) تطبیق می‌خورند؛ آن‌وقت چون فیلدهای عضو از «وقوعِ دوم» خوانده
        // می‌شوند، دومین تطبیق یعنی «بلي» انتخاب می‌شد و چون عدد نیست، نه شماره
        // ثبت می‌شد و نه نوع. همین باگ بود که «بلي» را در دیتابیس نوشته بود.
        private static readonly string[] TazkiraHeaders =
            { "ش تذکره", "شماره تذکره", "نمبر تذکره", "ش. تذکره", "ش تذکره تابعیت",
              "شماره تذکره الکترونیکی", "شماره تذکره عضو" };
        private static readonly string[] BirthDateHeaders =
            { "تاریخ تولد", "ت تولد", "ت. تولد", "تولد", "سال تولد", "تاریخ تولد شمسی" };

        // ─── نگاشت اعضا ──────────────────────────────────────────────────────
        // در فایل اعضا عناوینِ «نام/ش تذکره/تاریخ تولد» دو بار تکرار می‌شوند: یک
        // گروه مالِ سرپرست/کفیل و یک گروه مالِ خودِ عضو.
        // آموزش — چرا شماره‌ی وقوع دیگر هاردکد نیست: یک‌بار «گروه اول = عضو»
        // فرض شد و بار بعد «گروه دوم = عضو»؛ هر بار برای ساختارِ دیگر، داده‌ی
        // عضو خالی می‌ماند. حالا گروهِ عضو از روی خودِ داده تشخیص داده می‌شود
        // (DetectMemberOccurrence) و اگر فایل فقط یک گروه داشته باشد، همان گروه
        // استفاده می‌شود (ValueAt).
        // «جنسیت/سیادت/وضعیت خدمات» تک‌ستونه‌اند → همیشه وقوع اول.
        private static readonly MemberFieldRule[] MemberFieldRules =
        {
            new MemberFieldRule("MemberName",       NameHeaders,             true),
            new MemberFieldRule("MemberFatherName", FatherHeaders,           true),
            new MemberFieldRule("MemberTazkiraNo",  TazkiraHeaders,          true),
            new MemberFieldRule("BirthDate",        BirthDateHeaders,        true),
            new MemberFieldRule("Gender",           new[] { "جنسیت" },        false),
            new MemberFieldRule("MemberSadat",      new[] { "سیادت" },        false),
            new MemberFieldRule("ServiceStatus",    new[] { "وضعیت خدمات" },  false),
        };

        private sealed class MemberFieldRule
        {
            public readonly string DbColumn;
            public readonly string[] Headers;
            // true = این فیلد از «گروهِ ستون‌های عضو» خوانده می‌شود؛
            // false = ستون یکتاست و همیشه از وقوع اول خوانده می‌شود.
            public readonly bool UsesMemberGroup;
            public MemberFieldRule(string dbColumn, string[] headers, bool usesMemberGroup)
            { DbColumn = dbColumn; Headers = headers; UsesMemberGroup = usesMemberGroup; }
        }

        // ستون‌های دیتابیس که مقدارشان تاریخ است و باید به فرمت «سال/ماه/روز»
        // نرمال شوند (تشخیص خودکار: عددِ ۴رقمی = سال).
        private static readonly HashSet<string> DateColumns =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BirthDate", "HeadBirthDate" };

        // CHECK constraint واقعی TblCase.ServiceStatus — مقدار خارج از این فهرست
        // هرگز نوشته نمی‌شود (تا کل تراکنش به‌خاطر یک مقدار نامعتبر Rollback نشود).
        // منبع واحد: به‌جای آرایه‌ی هاردکدِ جداگانه، از TblLookup (دسته ServiceStatus —
        // همان منبعی که FrmCase/FrmAdvancedSearch/FrmReportFilter استفاده می‌کنند) خوانده می‌شود.
        private static HashSet<string> AllowedServiceStatuses =>
            new HashSet<string>(LookupHelper.GetValues("ServiceStatus"), StringComparer.Ordinal);

        // ─── مرحله ۲: تجزیه ──────────────────────────────────────────────────
        public ParsedSyncData Parse(SyncSource source, IProgress<SyncProgress> progress)
        {
            if (source == null) throw new ArgumentNullException("source");
            var result = new ParsedSyncData();

            if (!string.IsNullOrWhiteSpace(source.GuardiansFilePath))
            {
                Report(progress, "تجزیه فایل سرپرستان", 0, 1);
                var rows = ParseHtmlTable(source.GuardiansFilePath);
                for (int i = 0; i < rows.Count; i++)
                {
                    result.Guardians.Add(BuildGuardian(rows[i]));
                    if ((i & 0x3FF) == 0) Report(progress, "تجزیه فایل سرپرستان", i + 1, rows.Count);
                }
                Report(progress, "تجزیه فایل سرپرستان", rows.Count, rows.Count);
            }

            if (!string.IsNullOrWhiteSpace(source.MembersFilePath))
            {
                Report(progress, "تجزیه فایل اعضا", 0, 1);
                var rows = ParseHtmlTable(source.MembersFilePath);
                // کدام گروهِ ستون‌های تکراری مالِ خودِ عضو است (یک‌بار برای کل فایل).
                int memberOccurrence = DetectMemberOccurrence(rows);
                for (int i = 0; i < rows.Count; i++)
                {
                    result.Members.Add(BuildMember(rows[i], memberOccurrence));
                    if ((i & 0x3FF) == 0) Report(progress, "تجزیه فایل اعضا", i + 1, rows.Count);
                }
                Report(progress, "تجزیه فایل اعضا", rows.Count, rows.Count);
            }

            return result;
        }

        private SyncRecord BuildGuardian(HtmlRow row)
        {
            var rec = new SyncRecord { Entity = SyncEntity.Guardian };
            rec.PublicCode = ResolvePublicCode(row);
            MapFields(row, GuardianFieldMap, rec.SourceValues, occurrence: 1);

            // نوع تذکره در فایل HTML ستون قابل‌اتکایی ندارد؛ از روی الگوی دقیقِ
            // شماره تعیین می‌شود (IdCardHelper.InferTypeFromNumber) — خودِ شماره
            // دست‌نخورده ذخیره می‌شود (به‌درخواستِ صریحِ کاربر: هرگز بازقالب‌بندی نشود).
            // آموزش — رفع باگ «MemberTazkiraNo/HeadTazkiraNo = بلي/خیر»: اگر ستونِ
            // «ش تذکره» به‌جای شماره، متنِ غیرعددی (مثل «بلي»/«خیر») داشته باشد،
            // IsRealTazkira آن را رد می‌کند و نه شماره نوشته می‌شود نه نوع —
            // دقیقاً همان قاعده‌ای که پایین‌تر برای ComputeMemberKey از قبل هست.
            string headTazkira;
            if (rec.SourceValues.TryGetValue("HeadTazkiraNo", out headTazkira) && !string.IsNullOrWhiteSpace(headTazkira))
            {
                if (IsRealTazkira(headTazkira))
                    rec.SourceValues["HeadIdCardType"] = IdCardHelper.InferTypeFromNumber(headTazkira);
                else
                    rec.SourceValues.Remove("HeadTazkiraNo");
            }

            // کد عمومی همیشه در ستون کلیدی دیتابیس (Code) هم قرار می‌گیرد.
            if (!string.IsNullOrWhiteSpace(rec.PublicCode))
                rec.SourceValues["Code"] = rec.PublicCode;

            // نرمال‌سازی وضعیت خدمات؛ اگر با CHECK constraint سازگار نبود، حذف
            // می‌شود (نه این‌که کل تراکنش را با خطای دیتابیس Rollback کند).
            string status;
            if (rec.SourceValues.TryGetValue("ServiceStatus", out status))
            {
                string normalized = NormalizeServiceStatus(status);
                if (AllowedServiceStatuses.Contains(normalized))
                    rec.SourceValues["ServiceStatus"] = normalized;
                else
                    rec.SourceValues.Remove("ServiceStatus");
            }

            rec.Title = rec.SourceValues.ContainsKey("HeadFullName") ? rec.SourceValues["HeadFullName"] : rec.PublicCode;
            return rec;
        }

        private SyncRecord BuildMember(HtmlRow row, int memberOccurrence)
        {
            var rec = new SyncRecord { Entity = SyncEntity.FamilyMember };
            rec.PublicCode = ResolvePublicCode(row);

            // فقط فیلدهایی که واقعاً در فایل هستند نوشته می‌شوند؛ ستون‌هایی که فایل
            // نمی‌دهد (وضعیت جسمی/تحصیلی/درمانی و ...) اصلاً لمس نمی‌شوند و خالی
            // می‌مانند.
            foreach (MemberFieldRule rule in MemberFieldRules)
            {
                string val = ValueAt(row, rule.Headers, rule.UsesMemberGroup ? memberOccurrence : 1);
                if (string.IsNullOrWhiteSpace(val)) continue;

                val = val.Trim();

                if (DateColumns.Contains(rule.DbColumn))
                {
                    // تاریخِ شمسیِ فایل → میلادیِ ISO (فرمت ذخیره‌ی نرم‌افزار؛ دقیقاً
                    // همان چیزی که فرم خانواده هنگام ذخیره می‌نویسد). اگر تاریخ
                    // به‌هیچ‌وجه قابل‌فهم نبود، فیلد نوشته نمی‌شود (خالی می‌ماند) تا
                    // محاسبه‌ی سن/نمایش خراب نشود.
                    val = ConvertPersianDateToStored(val);
                    if (string.IsNullOrWhiteSpace(val)) continue;
                }
                else if (string.Equals(rule.DbColumn, "MemberTazkiraNo", StringComparison.OrdinalIgnoreCase))
                {
                    // نوع تذکره در فایل HTML ستون قابل‌اتکایی ندارد؛ از روی الگوی
                    // دقیقِ شماره تعیین می‌شود — خودِ شماره دست‌نخورده ذخیره می‌شود
                    // (قاعده‌ی مشترک با سرپرست، هرگز بازقالب‌بندی نشود).
                    // متنِ غیرعددی (مثل «بلي»/«خیر») هرگز به‌عنوانِ شماره ذخیره
                    // نمی‌شود — نه شماره نوشته می‌شود نه نوع (هم‌قاعده با ComputeMemberKey).
                    if (!IsRealTazkira(val)) continue;
                    rec.SourceValues["MemberIdCardType"] = IdCardHelper.InferTypeFromNumber(val);
                }
                else if (string.Equals(rule.DbColumn, "Gender", StringComparison.OrdinalIgnoreCase))
                {
                    // «ذکور/اناث/پسر/دختر/…» → همان دو مقداری که TblLookup و
                    // فیلترهای گزارش‌ها می‌شناسند («مذکر»/«مؤنث»).
                    val = NormalizeGender(val);
                }
                else if (string.Equals(rule.DbColumn, "ServiceStatus", StringComparison.OrdinalIgnoreCase))
                {
                    // مقدار خارج از فهرست مجاز نوشته نمی‌شود (وگرنه کل تراکنش
                    // به‌خاطر یک مقدار نامعتبر Rollback می‌شود) — هم‌منطق با سرپرست.
                    val = NormalizeServiceStatus(val);
                    if (!AllowedServiceStatuses.Contains(val)) continue;
                }

                rec.SourceValues[rule.DbColumn] = val;
            }

            // پیش‌فرضِ «وضعیت جسمی = سالم» فقط برای اعضای جدید (فایل چیزی درباره‌ی
            // معلولیت نمی‌گوید). این مقدار در بروزرسانی‌های بعدی دخالت نمی‌کند، پس
            // اگر کاربر بعداً عضوی را «معلول» ثبت کند، همگام‌سازی آن را بازنویسی نمی‌کند.
            rec.InsertOnlyValues["PhysicalStatus"] = "سالم";

            rec.Title = rec.SourceValues.ContainsKey("MemberName") ? rec.SourceValues["MemberName"] : "";
            rec.MemberKey = ComputeMemberKey(rec.SourceValues);
            return rec;
        }

        // ─── تبدیل تاریخِ شمسیِ فایل → تاریخِ میلادیِ ذخیره‌ای نرم‌افزار ──────
        // نکته‌ی مهم: نرم‌افزار تاریخ‌ها را «میلادی ISO» (yyyy-MM-dd) ذخیره می‌کند
        // (چون سن با julianday(BirthDate) محاسبه می‌شود و نمایش شمسی فقط در
        // لحظه‌ی نمایش انجام می‌شود). پس اگر تاریخ شمسی را مستقیم بنویسیم،
        // محاسبه‌ی سن و نمایش خراب می‌شود.
        // ورودی فایل «روز-ماه-سالِ شمسی» است (مثل 23-04-1405؛ عددِ ۴رقمی = سال).
        // این متد آن را به همان DateTime میلادی‌ای تبدیل می‌کند که خودِ فرم
        // خانواده هنگام ذخیره تولید می‌کند (PersianDateHelper.ParsePersianDate)،
        // سپس به رشته‌ی "yyyy-MM-dd" برمی‌گرداند. تبدیل قطعی است → همگام‌سازیِ
        // مجدد همان مقدار را می‌سازد (idempotent). تاریخ نامعتبر → null (ننوشتن).
        private static string ConvertPersianDateToStored(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var nums = new List<string>();
            foreach (Match m in Regex.Matches(ToLatinDigits(raw), "[0-9]+"))
                nums.Add(m.Value);
            if (nums.Count == 0) return null;

            // سال = اولین عددِ ۴رقمی.
            int yearIdx = -1;
            for (int i = 0; i < nums.Count; i++)
                if (nums[i].Length == 4) { yearIdx = i; break; }
            if (yearIdx < 0) return null;

            int year;
            if (!int.TryParse(nums[yearIdx], out year)) return null;

            // حداکثر دو عددِ دیگر (ماه/روز). آموزش — رفع باگ «تاریخ تولدِ اعضا
            // وارد نمی‌شد»: نسخه‌ی قبلی فقط دقیقاً سه عدد را می‌پذیرفت، پس
            // سلول‌هایی که فقط «سال تولد» داشتند (در فایل واقعی کم نیستند) یا
            // عددِ اضافه‌ای در متن داشتند، کاملاً بی‌صدا حذف می‌شدند. حالا
            // سالِ تنها → اول همان سال، و سال+ماه → اول همان ماه ثبت می‌شود
            // (به‌جای گم‌شدنِ کاملِ تاریخ تولد).
            var others = new List<int>();
            for (int i = 0; i < nums.Count && others.Count < 2; i++)
            {
                if (i == yearIdx) continue;
                int v;
                if (!int.TryParse(nums[i], out v)) return null;
                others.Add(v);
            }

            int month = 1, day = 1;
            if (others.Count >= 2)
            {
                if (yearIdx == 0) { month = others[0]; day = others[1]; } // yyyy-mm-dd
                else              { day = others[0]; month = others[1]; } // dd-mm-yyyy (حالت رایج فایل)
            }
            else if (others.Count == 1)
            {
                month = others[0]; // سال + ماه (بدون روز)
            }

            if (month < 1 || month > 12) return null;
            if (day < 1 || day > 31) return null;

            // تاریخِ میلادیِ آماده در فایل (سالِ ≥۱۷۰۰) بدون تبدیل ذخیره می‌شود.
            if (year >= 1700)
            {
                try { return new DateTime(year, month, day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
                catch { return null; }
            }

            try
            {
                DateTime gregorian = PersianDateHelper.ParsePersianDate(year + "/" + month + "/" + day);
                if (gregorian == DateTime.MinValue) return null;
                return gregorian.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch
            {
                return null; // تاریخ شمسیِ نامعتبر (مثلاً ۳۱ برجِ ۳۰روزه)
            }
        }

        // تبدیل ارقام فارسی/عربی به لاتین (فایل واقعی لاتین است؛ این فقط دفاعی است).
        private static string ToLatinDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c >= '۰' && c <= '۹') sb.Append((char)('0' + (c - '۰')));       // فارسی
                else if (c >= '٠' && c <= '٩') sb.Append((char)('0' + (c - '٠')));  // عربی
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // نرمال‌سازی مقادیر متغیر «وضعیت خدمات» به مقادیر مجاز CHECK constraint.
        // نگاشت‌ها در Helpers.CaseDomain متمرکز شده‌اند تا فرم، ایمپورت اکسل و
        // سینک هرگز از هم جدا نیفتند (قبلاً سه نسخهٔ جداگانه بود).
        private static string NormalizeServiceStatus(string value)
        {
            value = (value ?? "").Trim();
            if (value == "") return CaseManagement.Helpers.CaseDomain.StatusActive;
            return CaseManagement.Helpers.CaseDomain.NormalizeServiceStatus(value);
        }

        // کلید هویت عضو داخل خانواده.
        // آموزش — رفع باگ «۵۰ عضو همیشه بروزرسانی»: در فایل واقعی، خیلی از
        // فرزندان به‌جای شماره تذکره یک «متن توضیحی» یکسان دارند (مثل «الی برج ۶
        // سال ۱۴۰۳ تذکره دریافت خواهد شد»). اگر این متن را به‌عنوان تذکره کلید
        // بگیریم، همه‌ی فرزندانِ آن خانواده کلید یکسان می‌گیرند و فقط یکی match
        // می‌شود؛ بقیه هر بار «بروزرسانی» می‌مانند (idempotent نمی‌شود).
        // پس تذکره فقط وقتی کلید می‌شود که «واقعاً شماره» باشد (فقط رقم/خط‌تیره/
        // اسلش)، وگرنه از نام+نام‌پدر+تاریخ تولد استفاده می‌شود که برای
        // خواهر/برادرها یکتاست (نام یا تاریخ تولدشان فرق دارد).
        public static string ComputeMemberKey(Dictionary<string, string> values)
        {
            string tazkira;
            values.TryGetValue("MemberTazkiraNo", out tazkira);
            string birth = values.ContainsKey("BirthDate") ? values["BirthDate"] : "";

            // تاریخ تولد به کلید تذکره هم افزوده می‌شود تا حتی اگر منبع اشتباهاً
            // یک شماره تذکره را به دو فرزند مختلف داده باشد (خطای داده‌ی واقعی)،
            // باز هم دو کلید مجزا بسازند و همگام‌سازی مجدد «بدون تغییر» بماند.
            if (IsRealTazkira(tazkira))
                return "T:" + Normalize(tazkira) + "|" + Normalize(birth);

            return ComputeMemberNameKey(values);
        }

        // ─── کلیدِ ثابتِ نام+نام‌پدر+تاریخ تولد — مستقل از شماره تذکره ────────
        // آموزش — رفع باگِ «تصحیحِ شماره تذکره = رکورد تکراری»: وقتی تذکرهٔ
        // ذخیره‌شده در دیتابیس قبلاً غیرعددی/خالی بوده (مثل «بلي») و فایلِ جدید
        // شمارهٔ واقعی می‌آورد، ComputeMemberKey برای رکوردِ موجود «N:...» و برای
        // رکوردِ تازه «T:...» می‌سازد — این دو هرگز برابر نمی‌شوند و SyncComparer
        // عضوِ موجود را «جدید» تشخیص می‌دهد (Insert به‌جای Update). این متد
        // همیشه (فارغ از شماره تذکره) همان کلیدِ نام‌محور را می‌سازد تا
        // SyncComparer بتواند به‌عنوان راهِ دومِ تطبیق از آن استفاده کند.
        public static string ComputeMemberNameKey(Dictionary<string, string> values)
        {
            string name = values.ContainsKey("MemberName") ? values["MemberName"] : "";
            string father = values.ContainsKey("MemberFatherName") ? values["MemberFatherName"] : "";
            string birth = values.ContainsKey("BirthDate") ? values["BirthDate"] : "";
            return "N:" + NormalizeName(name) + "|" + NormalizeName(father) + "|" + Normalize(birth);
        }

        // ─── کلیدِ «نام + نام پدر» بدونِ تاریخ تولد ───────────────────────────
        // آموزش — در دادهٔ واقعی، تاریخ تولدِ ذخیره‌شده گاهی یک سال با فایل فرق
        // دارد (تبدیلِ شمسی→میلادیِ نسخه‌های قدیمی‌تر). اگر تاریخ تنها عاملِ
        // نامطابقت باشد، رکورد نباید «جدید» شمرده شود (وگرنه عضوِ تکراری ساخته
        // می‌شود). این کلید فقط به‌عنوان «لایهٔ سومِ» تطبیق در SyncComparer و
        // فقط داخلِ همان خانواده استفاده می‌شود، پس خطرِ برخوردِ بین‌خانوادگی ندارد.
        public static string ComputeMemberNameOnlyKey(Dictionary<string, string> values)
        {
            string name = values.ContainsKey("MemberName") ? values["MemberName"] : "";
            string father = values.ContainsKey("MemberFatherName") ? values["MemberFatherName"] : "";
            return "NF:" + NormalizeName(name) + "|" + NormalizeName(father);
        }

        // ─── یکسان‌سازیِ نوشتارِ نام برای تطبیق ───────────────────────────────
        // آموزش — چرا لازم است: همان فرد در دیتابیس «علي عطا» (ي عربی) و در فایل
        // «علی عطا» (ی فارسی) نوشته شده؛ بدونِ این یکسان‌سازی، تطبیق شکست می‌خورد
        // و عضوِ تکراری ساخته می‌شود. هر دو طرف (رکوردِ دیتابیس و رکوردِ فایل) از
        // همین متد عبور می‌کنند، پس تبدیل متقارن و بی‌خطر است — و هیچ‌وقت روی
        // مقداری که ذخیره می‌شود اثر نمی‌گذارد (فقط کلیدِ تطبیق).
        public static string NormalizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";

            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c == '‌' || c == '​' || c == '‎' || c == '‏' || c == '﻿')
                    continue;                                   // کاراکترهای نامرئی
                if (c == 'ي' || c == 'ى' || c == 'ئ') { sb.Append('ی'); continue; }   // یای عربی
                if (c == 'ك')                          { sb.Append('ک'); continue; }   // کاف عربی
                if (c == 'ة')                          { sb.Append('ه'); continue; }
                if (c == 'أ' || c == 'إ' || c == 'آ')  { sb.Append('ا'); continue; }
                sb.Append(c);
            }
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        // ─── مقایسهٔ نام با تحملِ کاراکترِ خرابِ «؟» ──────────────────────────
        // آموزش — بخشی از نام‌های ذخیره‌شده در دیتابیس کاراکترِ خراب دارند
        // («عز?زگل» به‌جای «عزیزگل»، «محمدعل?» به‌جای «محمدعلی») — نتیجهٔ یک
        // ایمپورتِ قدیمی با انکدینگِ اشتباه. این متد «?» را مانندِ «هر یک
        // کاراکتر» می‌بیند تا همان فرد دوباره درج نشود. فقط به‌عنوان آخرین لایهٔ
        // تطبیق و فقط داخلِ یک خانواده استفاده می‌شود.
        public static bool NamesMatchTolerant(string a, string b)
        {
            string x = NormalizeName(a), y = NormalizeName(b);
            if (x.Length == 0 || y.Length == 0) return false;
            if (x.Length != y.Length) return false;

            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] == y[i]) continue;
                if (x[i] == '?' || y[i] == '?') continue;   // کاراکترِ خراب = هر چیزی
                return false;
            }
            return true;
        }

        // «شماره تذکره واقعی» = شامل حداقل یک رقم و فقط از رقم/خط‌تیره/اسلش/فاصله
        // تشکیل شده (نه متن توضیحی مثل «... تذکره دریافت خواهد شد»).
        private static bool IsRealTazkira(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            string latin = ToLatinDigits(s.Trim());
            bool hasDigit = false;
            foreach (char c in latin)
            {
                if (c >= '0' && c <= '9') { hasDigit = true; continue; }
                if (c == '-' || c == '/' || c == ' ' || c == '‌') continue; // خط‌تیره/اسلش/فاصله/نیم‌فاصله
                return false; // هر کاراکتر دیگر (حرف) = تذکره واقعی نیست
            }
            return hasDigit;
        }

        private static string Normalize(string s)
        {
            return (s ?? "").Trim();
        }

        private string ResolvePublicCode(HtmlRow row)
        {
            foreach (string header in PublicCodeHeaders)
            {
                string val = FindByHeader(row, header, 1);
                if (!string.IsNullOrWhiteSpace(val))
                    return val.Trim();
            }
            return "";
        }

        private void MapFields(HtmlRow row, Dictionary<string, string[]> map,
            Dictionary<string, string> target, int occurrence)
        {
            foreach (var kv in map)
            {
                foreach (string candidate in kv.Value)
                {
                    string val = FindByHeader(row, candidate, occurrence);
                    if (val == null) continue;

                    val = val.Trim();

                    // آموزش — رفع باگ «تاریخ تولد سرپرست وارد نمی‌شد»: این متد
                    // (برخلاف BuildMember) تبدیلِ تاریخِ شمسی→میلادی را انجام
                    // نمی‌داد. تا وقتی نگاشتِ سرپرست هیچ ستون تاریخی نداشت این
                    // بی‌اثر بود، ولی حالا که «تاریخ تولد سرپرست» اضافه شده،
                    // بدون این تبدیل مقدارِ شمسیِ خام در ستونِ میلادی می‌نشست و
                    // محاسبه‌ی سن و نمایش را خراب می‌کرد.
                    if (DateColumns.Contains(kv.Key))
                    {
                        val = ConvertPersianDateToStored(val);
                        if (string.IsNullOrWhiteSpace(val)) break; // تاریخ نامعتبر → اصلاً ننویس
                    }

                    target[kv.Key] = val;
                    break;
                }
            }
        }

        // جستجوی مقدار بر اساس عنوان ستون + شماره‌ی وقوع (برای ستون‌های هم‌نامِ
        // تکراری مثل «نام» که یک‌بار برای خودِ عضو و یک‌بار برای کفیل می‌آید).
        // occurrence=1 یعنی اولین ستونی که این عنوان را دارد (از راست به چپ فایل
        // اصلی که هنگام تجزیه، ترتیب واقعی ستون‌ها حفظ شده است).
        private static string FindByHeader(HtmlRow row, string header, int occurrence)
        {
            string norm = NormalizeHeader(header);
            int seen = 0;
            for (int i = 0; i < row.Keys.Count; i++)
            {
                if (NormalizeHeader(row.Keys[i]) == norm)
                {
                    seen++;
                    if (seen == occurrence)
                        return row.Values[i];
                }
            }
            return null;
        }

        // ─── خواندن مقدار بر اساس «گروهِ ستون» ───────────────────────────────
        // همه‌ی مقادیرِ ستون‌هایی که یکی از این عناوینِ هم‌معنی را دارند، به ترتیبِ
        // واقعیِ ستون‌ها.
        private static List<string> CollectByHeaders(HtmlRow row, string[] headers)
        {
            var norms = new HashSet<string>(StringComparer.Ordinal);
            foreach (string h in headers) norms.Add(NormalizeHeader(h));

            var found = new List<string>();
            for (int i = 0; i < row.Keys.Count; i++)
                if (norms.Contains(NormalizeHeader(row.Keys[i])))
                    found.Add(row.Values[i]);
            return found;
        }

        // مقدارِ «وقوعِ n اُم» (۱-پایه). اگر فایل آن وقوع را نداشته باشد (یعنی
        // ستون‌ها اصلاً تکرار نشده‌اند)، آخرین وقوعِ موجود برگردانده می‌شود — چون
        // در آن حالت همان تک‌گروه، خودِ عضو است.
        private static string ValueAt(HtmlRow row, string[] headers, int occurrence)
        {
            List<string> all = CollectByHeaders(row, headers);
            if (all.Count == 0) return null;

            int idx = occurrence - 1;
            if (idx >= all.Count) idx = all.Count - 1;
            if (idx < 0) idx = 0;
            return all[idx];
        }

        // ─── تشخیصِ داده‌محورِ «کدام گروه مالِ خودِ عضو است» ───────────────────
        // در فایل اعضا هر «کد عمومی» به تعداد فرزندانِ خانواده تکرار می‌شود؛ نامِ
        // سرپرست/کفیل در همه‌ی آن ردیف‌ها یکسان است، ولی نامِ فرزندان فرق دارد.
        // پس گروهی که مقدارش داخلِ یک خانواده تکراری است = سرپرست و گروه دیگر =
        // عضو. اگر ستون‌ها تکرار نشده باشند → وقوع ۱. اگر هیچ خانواده‌ی
        // چندنفره‌ای در فایل نباشد (نمونه‌ی خیلی کوچک) → وقوع ۲ (ساختارِ فایل
        // واقعیِ کاربر).
        private int DetectMemberOccurrence(List<HtmlRow> rows)
        {
            if (rows == null || rows.Count == 0) return 1;

            int maxGroups = 0;
            foreach (HtmlRow r in rows)
            {
                int c = CollectByHeaders(r, NameHeaders).Count;
                if (c > maxGroups) maxGroups = c;
            }
            if (maxGroups < 2) return 1;

            var byCode = new Dictionary<string, List<HtmlRow>>(StringComparer.Ordinal);
            foreach (HtmlRow r in rows)
            {
                string code = ResolvePublicCode(r);
                if (string.IsNullOrWhiteSpace(code)) continue;

                List<HtmlRow> list;
                if (!byCode.TryGetValue(code, out list)) { list = new List<HtmlRow>(); byCode[code] = list; }
                list.Add(r);
            }

            int repeatedIn1 = 0, repeatedIn2 = 0, families = 0;
            foreach (var kv in byCode)
            {
                if (kv.Value.Count < 2) continue;
                families++;
                if (AllSameNonEmptyName(kv.Value, 1)) repeatedIn1++;
                if (AllSameNonEmptyName(kv.Value, 2)) repeatedIn2++;
            }

            if (families == 0) return 2;
            if (repeatedIn1 > repeatedIn2) return 2; // گروه ۱ سرپرست است ⇒ عضو = گروه ۲
            if (repeatedIn2 > repeatedIn1) return 1; // گروه ۲ سرپرست است ⇒ عضو = گروه ۱
            return 2;
        }

        private static bool AllSameNonEmptyName(List<HtmlRow> rows, int occurrence)
        {
            string first = null;
            foreach (HtmlRow r in rows)
            {
                string v = (ValueAt(r, NameHeaders, occurrence) ?? "").Trim();
                if (v.Length == 0) return false;
                if (first == null) first = v;
                else if (!string.Equals(first, v, StringComparison.Ordinal)) return false;
            }
            return first != null;
        }

        // «ذکور/اناث/پسر/دختر/male/female» → مقادیرِ استانداردِ TblLookup.
        // مقدارِ ناشناخته دست‌نخورده برمی‌گردد (تا داده‌ای گم نشود).
        private static string NormalizeGender(string value)
        {
            string v = (value ?? "").Trim().Replace('ي', 'ی').Replace('ك', 'ک');
            string lower = v.ToLowerInvariant();

            if (v == "مذکر" || v == "ذکور" || v == "ذکر" || v == "مرد" || v == "پسر" ||
                lower == "m" || lower == "male")
                return "مذکر";

            if (v == "مؤنث" || v == "مونث" || v == "اناث" || v == "زن" || v == "دختر" ||
                lower == "f" || lower == "female")
                return "مؤنث";

            return v;
        }

        private static string NormalizeHeader(string s)
        {
            if (s == null) return "";
            return s.Replace('ي', 'ی').Replace('ك', 'ک')
                    .Replace("‌", " ")   // نیم‌فاصله → فاصله
                    .Trim()
                    .ToLowerInvariant();
        }

        // ─── مرحله ۳: اعتبارسنجی ─────────────────────────────────────────────
        public List<string> Validate(ParsedSyncData data)
        {
            var errors = new List<string>();
            if (data == null) { errors.Add("داده‌ای برای اعتبارسنجی وجود ندارد."); return errors; }

            int guardianNoCode = data.Guardians.Count(g => string.IsNullOrWhiteSpace(g.PublicCode));
            if (guardianNoCode > 0)
                errors.Add(guardianNoCode + " سرپرست بدون «کد عمومی» است و نادیده گرفته می‌شود.");

            int memberNoCode = data.Members.Count(m => string.IsNullOrWhiteSpace(m.PublicCode));
            if (memberNoCode > 0)
                errors.Add(memberNoCode + " عضو خانواده بدون «کد عمومی» است و قابل ارتباط با خانواده نیست.");

            int memberNoName = data.Members.Count(m => string.IsNullOrWhiteSpace(m.Title));
            if (memberNoName > 0)
                errors.Add(memberNoName + " عضو خانواده بدون «نام» است.");

            if (data.Guardians.Count == 0 && data.Members.Count == 0)
                errors.Add("هیچ رکورد قابل‌خواندنی در فایل‌ها یافت نشد (ساختار جدول HTML شناسایی نشد).");

            return errors;
        }

        // ─── ردیف تجزیه‌شده با حفظ ترتیب دقیق ستون‌ها ────────────────────────
        // آموزش — رفع باگ بحرانی: نسخه‌ی قبلی از Dictionary<string,string> با
        // کلید = نام ستون استفاده می‌کرد؛ وقتی دو ستون هم‌نام بودند (مثل «نام»ِ
        // خودِ عضو و «نام»ِ کفیل)، مقدار دومی به‌خاطر «اگر کلید موجود بود
        // نادیده بگیر» به‌طور کاملاً بی‌صدا گم می‌شد — نه خطا، نه هشدار، فقط
        // حذف داده. حالا هر ردیف یک لیست هم‌ترتیب (Keys/Values) نگه می‌دارد که
        // امکان جستجوی «امین وقوعِ فلان عنوان» را می‌دهد (FindByHeader بالا).
        private sealed class HtmlRow
        {
            public readonly List<string> Keys = new List<string>();
            public readonly List<string> Values = new List<string>();
        }

        // عنوان‌هایی که «قطعاً نشان می‌دهند این ردیف، ردیف سرستون است».
        private static readonly string[] HeaderAnchors = { "کد عمومی", "ردیف" };

        // ─── تجزیه‌ی جدول HTML (تحمل‌پذیر، بدون وابستگی خارجی) ────────────────
        // آموزش — رفع باگ بحرانی «همه بدون کد عمومی»: نسخه‌ی قبلی فرض می‌کرد
        // «اولین <tr> = سرستون». اما در فایل‌های واقعی، بالای جدول اعضا یک عنوان
        // بزرگ («اسماء خانواده») به‌صورت یک ردیف/سلولِ colspan وجود دارد، یا
        // جدول‌ها تودرتو (nested) هستند، یا ردیف سرستون در هر صفحه تکرار می‌شود.
        // در همه‌ی این حالت‌ها ردیف اول، سرستونِ واقعی نبود و همه‌ی ستون‌ها اشتباه
        // نگاشت می‌شدند (کد عمومی/نام «خالی» به‌نظر می‌رسیدند).
        // حالا: همه‌ی <tr>های کل سند خوانده می‌شوند، ردیفِ سرستونِ واقعی با «لنگر»
        // (وجود «کد عمومی» یا «ردیف») پیدا می‌شود، ردیف‌های عنوان/بنر قبل از آن
        // نادیده گرفته می‌شوند، و ردیف‌های سرستونِ تکراری (صفحه‌بندی) هم رد می‌شوند.
        private static List<HtmlRow> ParseHtmlTable(string filePath)
        {
            var rows = new List<HtmlRow>();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return rows;

            string html = File.ReadAllText(filePath, DetectEncoding(filePath));

            // همه‌ی ردیف‌های کل سند (مستقل از تودرتو بودن جدول‌ها).
            var trMatches = Regex.Matches(html, "<tr[^>]*>(.*?)</tr>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (trMatches.Count == 0) return rows;

            var allRows = new List<List<string>>();
            foreach (Match tr in trMatches)
            {
                List<string> cells = ExtractCells(tr.Groups[1].Value);
                if (cells.Count > 0) allRows.Add(cells);
            }
            if (allRows.Count == 0) return rows;

            // ردیف سرستون واقعی را پیدا کن (اولین ردیفی که یک لنگر معتبر دارد).
            int headerIndex = -1;
            for (int r = 0; r < allRows.Count; r++)
            {
                if (IsHeaderRow(allRows[r])) { headerIndex = r; break; }
            }
            // اگر لنگری پیدا نشد، به رفتار قدیمی (اولین ردیف = سرستون) برگرد.
            if (headerIndex < 0) headerIndex = 0;

            List<string> headers = allRows[headerIndex];
            int headerCount = headers.Count;

            for (int r = headerIndex + 1; r < allRows.Count; r++)
            {
                List<string> cells = allRows[r];

                // ردیف سرستونِ تکراری (صفحه‌بندی) را رد کن.
                if (IsHeaderRow(cells)) continue;

                // ردیف‌های پرت/خلاصه با تعداد ستون بسیار کمتر را رد کن، و
                // ردیف‌های کاملاً خالی را هم.
                if (cells.Count < Math.Max(3, headerCount / 2)) continue;
                if (cells.All(c => string.IsNullOrWhiteSpace(c))) continue;

                var row = new HtmlRow();
                for (int i = 0; i < cells.Count && i < headers.Count; i++)
                {
                    string key = string.IsNullOrWhiteSpace(headers[i]) ? ("col" + i) : headers[i];
                    row.Keys.Add(key);
                    row.Values.Add(cells[i]);
                }
                rows.Add(row);
            }

            return rows;
        }

        // آیا این ردیف، ردیف سرستون است؟ (شامل یکی از لنگرهای شناخته‌شده)
        private static bool IsHeaderRow(List<string> cells)
        {
            foreach (string cell in cells)
            {
                string norm = NormalizeHeader(cell);
                foreach (string anchor in HeaderAnchors)
                    if (norm == NormalizeHeader(anchor))
                        return true;
            }
            return false;
        }

        private static List<string> ExtractCells(string trInner)
        {
            var cells = new List<string>();
            var cellMatches = Regex.Matches(trInner, "<t[hd][^>]*>(.*?)</t[hd]>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match c in cellMatches)
                cells.Add(CleanCell(c.Groups[1].Value));
            return cells;
        }

        private static string CleanCell(string raw)
        {
            if (raw == null) return "";
            string noTags = Regex.Replace(raw, "<[^>]+>", " ");
            string decoded = WebUtility.HtmlDecode(noTags);
            string collapsed = Regex.Replace(decoded, "\\s+", " ");
            return collapsed.Trim();
        }

        // تشخیص انکدینگ ساده: BOM یا meta charset؛ پیش‌فرض UTF-8.
        private static Encoding DetectEncoding(string filePath)
        {
            try
            {
                byte[] head = new byte[4];
                using (var fs = File.OpenRead(filePath))
                    fs.Read(head, 0, 4);
                if (head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF) return Encoding.UTF8;
                if (head[0] == 0xFF && head[1] == 0xFE) return Encoding.Unicode;
                if (head[0] == 0xFE && head[1] == 0xFF) return Encoding.BigEndianUnicode;
            }
            catch { }
            return Encoding.UTF8;
        }

        private static void Report(IProgress<SyncProgress> progress, string phase, int current, int total)
        {
            if (progress != null)
                progress.Report(new SyncProgress { Phase = phase, Current = current, Total = total });
        }
    }
}

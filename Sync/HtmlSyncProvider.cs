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

        // ─── نگاشت اعضا: ستون‌های تکراری («نام/نام پدر/ش تذکره/تاریخ تولد») در
        // فایل اعضا دو بار می‌آیند. طبق تصحیح کاربر: گروه اول = سرپرست/والد،
        // گروه دوم = خودِ فرزند (عضو). پس فیلدهای عضو از «وقوع دوم» گرفته می‌شوند.
        // «جنسیت» و «سیادت» تک‌ستونه‌اند (وقوع اول). این نگاشت به‌صورت
        // (ستون دیتابیس، عنوان HTML، شماره‌ی وقوع) در BuildMember اعمال می‌شود.
        private static readonly MemberFieldRule[] MemberFieldRules =
        {
            new MemberFieldRule("MemberName",       new[] { "نام", "نام عضو" },                 2),
            new MemberFieldRule("MemberFatherName", new[] { "نام پدر", "ولد" },                 2),
            new MemberFieldRule("MemberTazkiraNo",  new[] { "ش تذکره", "شماره تذکره", "تذکره" }, 2),
            new MemberFieldRule("BirthDate",        new[] { "تاریخ تولد" },                     2),
            new MemberFieldRule("Gender",           new[] { "جنسیت" },                          1),
            new MemberFieldRule("MemberSadat",      new[] { "سیادت" },                          1),
        };

        private sealed class MemberFieldRule
        {
            public readonly string DbColumn;
            public readonly string[] Headers;
            public readonly int Occurrence;
            public MemberFieldRule(string dbColumn, string[] headers, int occurrence)
            { DbColumn = dbColumn; Headers = headers; Occurrence = occurrence; }
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
                for (int i = 0; i < rows.Count; i++)
                {
                    result.Members.Add(BuildMember(rows[i]));
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

        private SyncRecord BuildMember(HtmlRow row)
        {
            var rec = new SyncRecord { Entity = SyncEntity.FamilyMember };
            rec.PublicCode = ResolvePublicCode(row);

            // فقط فیلدهایی که واقعاً در فایل هستند نوشته می‌شوند؛ ستون‌هایی که فایل
            // نمی‌دهد (وضعیت جسمی/تحصیلی/درمانی و ...) اصلاً لمس نمی‌شوند و خالی
            // می‌مانند. گروه دوم عناوینِ تکراری = خودِ عضو (طبق تصحیح کاربر).
            foreach (MemberFieldRule rule in MemberFieldRules)
            {
                string val = null;
                foreach (string header in rule.Headers)
                {
                    val = FindByHeader(row, header, rule.Occurrence);
                    if (val != null) break;
                }
                if (string.IsNullOrWhiteSpace(val)) continue;

                val = val.Trim();
                if (DateColumns.Contains(rule.DbColumn))
                {
                    // تاریخِ شمسیِ فایل → میلادیِ ISO (فرمت ذخیره‌ی نرم‌افزار). اگر
                    // تاریخ نامعتبر بود، فیلد اصلاً نوشته نمی‌شود (خالی می‌ماند) تا
                    // محاسبه‌ی سن/نمایش خراب نشود.
                    val = ConvertPersianDateToStored(val);
                    if (string.IsNullOrWhiteSpace(val)) continue;
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
            if (nums.Count != 3) return null;

            int yearIdx = -1;
            for (int i = 0; i < 3; i++)
                if (nums[i].Length == 4) { yearIdx = i; break; }
            if (yearIdx < 0) return null;

            string year = nums[yearIdx];
            var others = new List<string>();
            for (int i = 0; i < 3; i++)
                if (i != yearIdx) others.Add(nums[i]);

            string day, month;
            if (yearIdx == 0) { month = others[0]; day = others[1]; } // yyyy-mm-dd
            else               { day = others[0]; month = others[1]; } // dd-mm-yyyy (حالت رایج فایل)

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

        // نرمال‌سازی مقادیر متغیر «وضعیت خدمات» به ۴ مقدار مجاز CHECK constraint
        // (هم‌راستا با FrmCase.NormalizeServiceStatus).
        private static string NormalizeServiceStatus(string value)
        {
            value = (value ?? "").Trim();
            if (value == "در حالت قطع") return "قطع";
            if (value == "درانتظار" || value == "در انتظار" ||
                value == "انتظار تاييد" || value == "انتظار تایید" ||
                value == "در انتظار تاييد")
                return "در انتظار تأیید";
            if (value == "") return "فعال";
            return value;
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

            string name = values.ContainsKey("MemberName") ? values["MemberName"] : "";
            string father = values.ContainsKey("MemberFatherName") ? values["MemberFatherName"] : "";
            return "N:" + Normalize(name) + "|" + Normalize(father) + "|" + Normalize(birth);
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

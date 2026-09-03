using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CaseManagement.AI
{
    // دستیار هوشمند — فاز ۱. تشخیصِ قصد + استخراجِ موجودیت از یک پرسشِ فارسیِ
    // خام. طبق AI_ASSISTANT_PHASE1_FIXES.md §6، امتیازِ اطمینان جمعی و
    // آستانه‌دار است: بالای ۰٫۷۰ پاسخِ مستقیم، ۰٫۴۰ تا ۰٫۶۹ پاسخ + پیشنهادِ
    // اصلاح، زیرِ ۰٫۴۰ فقط سؤالِ روشن‌کننده (بدون هیچ نوشتنی).
    public static class PersianNluEngine
    {
        public const double HighConfidenceThreshold   = 0.70;
        public const double LowConfidenceThreshold     = 0.40;

        private static readonly string[] Provinces =
        {
            "کابل","هرات","بلخ","قندهار","ننگرهار","بدخشان","بغلان","تخار","غزنی","هلمند",
            "لغمان","کندز","فاریاب","جوزجان","سمنگان","بامیان","پکتیا","لوگر","وردک","غور",
            "فراه","خوست","کاپیسا","پروان","زابل","ارزگان","نیمروز","نورستان","کنر","سرپل",
            "دایکندی","پکتیکا","بادغیس","پنجشیر"
        };

        // آموزش — فرهنگِ زنده: TblCase.District متنِ آزاد است (بدونِ Lookup
        // ثابت)، پس این فهرست فقط شهرها/مراکزِ شناخته‌شده‌ی رایج را پوشش
        // می‌دهد و باید بعداً بر اساسِ استفاده‌ی واقعی گسترش یابد (طبقِ
        // یادداشتِ بازبینیِ معمار در بخشِ «دقتِ جست‌وجو»).
        private static readonly string[] KnownDistricts =
        {
            "مزار شریف","جلال آباد","پل خمری","لشکرگاه","تالقان","فیض آباد","میمنه","شبرغان",
            "گردیز","چاریکار","ترینکوت","زرنج","چغچران","اسدآباد","کابل","هرات","قندهار","کندز",
            "غزنی","خوست","بامیان"
        };

        private static readonly Dictionary<string, int> WordNumbers = new Dictionary<string, int>
        {
            {"یک",1},{"دو",2},{"سه",3},{"چهار",4},{"پنج",5},{"شش",6},{"هفت",7},{"هشت",8},{"نه",9},{"ده",10},
            {"یازده",11},{"دوازده",12},{"سیزده",13},{"چهارده",14},{"پانزده",15},{"شانزده",16},{"هفده",17},
            {"هجده",18},{"نوزده",19},{"بیست",20},{"سی",30}
        };

        private static readonly string[] ReminderActionWords =
        {
            "بررسی","پیگیری","تماس","یادآوری","چک"
        };

        // توکن‌هایی که هیچ‌گاه بخشِ نام نیستند — برای استخراجِ «نامِ باقیمانده».
        private static readonly HashSet<string> StopTokens = new HashSet<string>(new[]
        {
            "را","با","به","در","که","باشد","اش","ای","کن","بگیر","بده","پیدا","نام","نامش",
            "داریم","چند","اخیر","نگرفته","نگرفته‌اند","روز","بعد","دیگر","هفته","آینده","امروز",
            "فردا","وضعیت","پیگیری","بررسی","تماس","یادآوری","چک","سرویس","پرونده","فورم","شماره",
            "یتیم","یتیمی","آخر","آخرش","تذکره","تذکره‌اش","ولایت","ولسوالی","و","برای","از","تا",
            "خانواده","باز","اند",
            // آموزش — رفعِ باگ: بدونِ این‌ها، «چند یتیم فعال داریم؟» واژه‌ی
            // «فعال» را هم به‌عنوانِ نامِ باقیمانده نگه می‌داشت؛ پس از رفعِ
            // باگِ بحرانیِ فیلترِ نام (CaseSearchCore)، همین واژه به‌اشتباه
            // در فیلترِ HeadFullName LIKE هم اعمال می‌شد.
            "فعال","غیرفعال","قطع","متوقف","افرادی","کسانی","کسی","کد"
        });

        public static AiNluResult Parse(string rawQuery)
        {
            string normalized = PersianNormalizer.Normalize(rawQuery ?? string.Empty);
            AiEntities entities = new AiEntities();

            ExtractProvince(normalized, entities);
            ExtractDistrict(normalized, entities);
            ExtractTazkira(normalized, entities);
            ExtractPhone(normalized, entities);
            ExtractCaseReference(normalized, entities);
            ExtractServiceStatus(normalized, entities);
            entities.IsCountQuery = Regex.IsMatch(normalized, @"\bچند\b");
            ExtractNoRecentServiceDays(normalized, entities);
            entities.PersonName = ExtractResidualName(normalized, entities);

            DateTime remindAt;
            string matchedDatePhrase;
            bool hasDate = ReminderDateParser.TryParse(normalized, DateTime.Now, out remindAt, out matchedDatePhrase);
            bool hasActionWord = ContainsAny(normalized, ReminderActionWords);

            string intent;
            double confidence = 0;

            bool explicitReminderWord = normalized.Contains("یادآوری");
            bool wantsReminder = hasActionWord &&
                (hasDate || explicitReminderWord || Regex.IsMatch(normalized, @"روز|هفته|فردا|امروز"));

            if (wantsReminder)
            {
                intent = AiIntent.CreateReminder;
                confidence += 0.40;
                if (hasDate)
                {
                    entities.ResolvedDate = remindAt;
                }
            }
            else if (Regex.IsMatch(normalized, @"سرویس") && Regex.IsMatch(normalized, @"نگرفته|اخیر"))
            {
                intent = AiIntent.NoRecentService;
                confidence += 0.40;
            }
            else if (normalized.Contains("خانواده"))
            {
                intent = AiIntent.SearchFamily;
                confidence += 0.40;
            }
            else
            {
                intent = AiIntent.SearchCase;
                if (entities.IsCountQuery || !string.IsNullOrEmpty(entities.CaseReferenceRaw) ||
                    !string.IsNullOrEmpty(entities.TazkiraNo) || !string.IsNullOrEmpty(entities.TazkiraSuffix) ||
                    !string.IsNullOrEmpty(entities.Province) || !string.IsNullOrEmpty(entities.District) ||
                    !string.IsNullOrEmpty(entities.PersonName))
                    confidence += 0.40;
            }

            if (intent == AiIntent.CreateReminder)
                entities.ReminderTitle = BuildReminderTitle(normalized, entities);

            int entityHits = 0;
            if (!string.IsNullOrEmpty(entities.PersonName)) entityHits++;
            if (!string.IsNullOrEmpty(entities.TazkiraNo) || !string.IsNullOrEmpty(entities.TazkiraSuffix)) entityHits++;
            if (!string.IsNullOrEmpty(entities.Province)) entityHits++;
            if (!string.IsNullOrEmpty(entities.District)) entityHits++;
            if (!string.IsNullOrEmpty(entities.Phone)) entityHits++;
            if (!string.IsNullOrEmpty(entities.CaseReferenceRaw)) entityHits++;
            if (entities.ResolvedDate.HasValue) entityHits++;
            confidence += Math.Min(entityHits * 0.15, 0.45);

            if (intent == AiIntent.CreateReminder && !entities.ResolvedDate.HasValue)
                confidence -= 0.50;

            confidence = Math.Max(0, Math.Min(1, confidence));

            return new AiNluResult { Intent = intent, Entities = entities, Confidence = confidence };
        }

        private static bool ContainsAny(string text, string[] words)
        {
            foreach (string w in words)
                if (text.Contains(w)) return true;
            return false;
        }

        private static void ExtractProvince(string text, AiEntities entities)
        {
            foreach (string p in Provinces)
            {
                if (text.Contains(p)) { entities.Province = p; return; }
            }
        }

        private static void ExtractDistrict(string text, AiEntities entities)
        {
            foreach (string d in KnownDistricts)
            {
                if (text.Contains(d)) { entities.District = d; return; }
            }
        }

        private static void ExtractTazkira(string text, AiEntities entities)
        {
            // ترتیب مهم است: الگوهایِ «آخرِ تذکره» باید پیش از الگویِ عمومیِ
            // «تذکره...عدد» بررسی شوند، وگرنه به‌اشتباه به‌عنوانِ شماره‌ی کامل
            // خوانده می‌شود.
            Match suffix = Regex.Match(text, @"آخر[\s\S]{0,15}?تذکره[\s\S]{0,10}?(\d+)");
            if (!suffix.Success) suffix = Regex.Match(text, @"تذکره[\s\S]{0,10}?آخر[\s\S]{0,10}?(\d+)");
            if (!suffix.Success) suffix = Regex.Match(text, @"آخرش\s*(\d+)");
            if (!suffix.Success) suffix = Regex.Match(text, @"به\s*(\d+)\s*ختم");
            if (suffix.Success)
            {
                entities.TazkiraSuffix = suffix.Groups[1].Value;
                return;
            }

            Match full = Regex.Match(text, @"تذکره[\s\S]{0,10}?(\d{3,})");
            if (full.Success)
                entities.TazkiraNo = full.Groups[1].Value;
        }

        private static void ExtractPhone(string text, AiEntities entities)
        {
            Match m = Regex.Match(text, @"(?:\+?93|0)?7\d{8}\b");
            if (m.Success)
                entities.Phone = m.Value;
        }

        private static void ExtractCaseReference(string text, AiEntities entities)
        {
            // آموزش — رفعِ باگِ کشف‌شده در استفاده‌ی واقعی: «کد» عیناً نامِ
            // ستونِ TblCase.Code است و کاربران طبیعتاً همین واژه را به‌کار
            // می‌برند («کد ۴۸۲»)، نه فقط «پرونده»/«فورم». بدونِ آن، توکنِ عددی
            // هرگز به‌عنوانِ CaseReferenceRaw استخراج نمی‌شد و به‌جایش در
            // نامِ باقیمانده گم می‌شد.
            Match m = Regex.Match(text, @"(?:پرونده|فورم|کد)\s+(?:شماره\s+)?(\S+?)(?:\s+را|\s+که|\s|$)");
            if (m.Success)
                entities.CaseReferenceRaw = m.Groups[1].Value.Trim(new[] { '؟', '?', '.', '،' });
        }

        private static void ExtractServiceStatus(string text, AiEntities entities)
        {
            // آموزش — رفعِ باگِ HIGH-01 (اعتبارسنجیِ نهایی): «غیرفعال» اصلاً در
            // میانِ مقادیرِ واقعیِ TblCase.ServiceStatus وجود ندارد (نگاه کنید
            // به CaseDomain.ServiceStatuses: متقاضی/در حال بررسی/در انتظار
            // تایید/فعال/قطع/قطع موقت). قبلاً هر پرسشی دربارهٔ «قطع»/«متوقف»
            // با مقدارِ نامعتبرِ «غیرفعال» فیلتر می‌شد و همیشه صفر نتیجه
            // برمی‌گرداند — نه خطا، فقط پاسخِ اشتباهِ ساکت. حالا مقدارِ واقعیِ
            // «قطع» ثبت می‌شود؛ CaseSearchCore آن را به هر دو حالتِ قطعِ
            // دائم/موقت (CaseDomain.TerminatedStatuses) گسترش می‌دهد.
            if (Regex.IsMatch(text, @"غیرفعال|قطع|متوقف"))
                entities.ServiceStatus = "قطع";
            else if (text.Contains("فعال"))
                entities.ServiceStatus = "فعال";
        }

        private static void ExtractNoRecentServiceDays(string text, AiEntities entities)
        {
            Match m = Regex.Match(text, @"(\d+)\s*روز");
            if (m.Success)
                entities.NoRecentServiceDays = int.Parse(m.Groups[1].Value);
        }

        // آنچه پس از حذفِ توکن‌های ساختاری/عددی/تاریخی/ولایت/ولسوالی باقی
        // می‌ماند — نامزدِ نامِ شخص/خانواده برای جست‌وجو. عبارتِ ولایت/ولسوالیِ
        // شناسایی‌شده باید *پیش* از توکن‌سازی حذف شود، وگرنه ولسوالیِ چندکلمه‌ای
        // («مزار شریف») به توکن‌های جداگانه می‌شکند و در نامِ باقیمانده نشت می‌کند.
        private static string ExtractResidualName(string text, AiEntities entities)
        {
            string working = text;
            if (!string.IsNullOrEmpty(entities.Province))
                working = working.Replace(entities.Province, " ");
            if (!string.IsNullOrEmpty(entities.District))
                working = working.Replace(entities.District, " ");

            string[] tokens = working.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> kept = new List<string>();

            foreach (string raw in tokens)
            {
                string t = raw.Trim(new[] { '؟', '?', '.', '،', '!' });
                if (t.Length == 0) continue;
                if (StopTokens.Contains(t)) continue;
                if (WordNumbers.ContainsKey(t)) continue;
                if (IsDigitsOnly(t)) continue;
                kept.Add(t);
            }

            string result = string.Join(" ", kept).Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }

        private static bool IsDigitsOnly(string s)
        {
            foreach (char c in s)
                if (c < '0' || c > '9') return false;
            return s.Length > 0;
        }

        private static string BuildReminderTitle(string normalized, AiEntities e)
        {
            string subject = !string.IsNullOrEmpty(e.CaseReferenceRaw)
                ? "پرونده " + e.CaseReferenceRaw
                : (!string.IsNullOrEmpty(e.PersonName) ? e.PersonName : "");

            if (normalized.Contains("بررسی"))
                return string.IsNullOrEmpty(subject) ? "بررسی پرونده" : "بررسی " + subject;
            if (normalized.Contains("تماس"))
                return string.IsNullOrEmpty(subject) ? "تماس با خانواده" : "تماس با " + subject;
            if (normalized.Contains("پیگیری"))
                return string.IsNullOrEmpty(subject) ? "پیگیری وضعیت پرونده" : "پیگیری وضعیت " + subject;
            if (normalized.Contains("چک"))
                return string.IsNullOrEmpty(subject) ? "بررسی" : "بررسی " + subject;

            return string.IsNullOrEmpty(subject) ? "یادآوری" : "یادآوری: " + subject;
        }
    }
}

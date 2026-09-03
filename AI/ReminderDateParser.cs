using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CaseManagement.Helpers;

namespace CaseManagement.AI
{
    // دستیار هوشمند — فاز ۱. تبدیلِ عبارتِ فارسیِ تاریخ/زمانِ نسبی یا مطلق به
    // DateTime میلادی — دقیقاً همان قالبی که TblReminder.RemindAt استفاده
    // می‌کند («yyyy-MM-dd HH:mm»). ورودی باید از قبل با PersianNormalizer
    // نرمال شده باشد (ارقام لاتین).
    //
    // طبق AI_ASSISTANT_PHASE1_FIXES.md §6/§9: اگر عبارت قابل‌فهم نبود یا به
    // تاریخِ گذشته حل شد، خروجی «شکست» است — هرگز یادآوریِ حدسی/گذشته ساخته
    // نمی‌شود.
    public static class ReminderDateParser
    {
        private static readonly Dictionary<string, int> WordNumbers = new Dictionary<string, int>
        {
            {"یک",1},{"دو",2},{"سه",3},{"چهار",4},{"پنج",5},{"شش",6},{"هفت",7},{"هشت",8},{"نه",9},{"ده",10},
            {"یازده",11},{"دوازده",12},{"سیزده",13},{"چهارده",14},{"پانزده",15},{"شانزده",16},{"هفده",17},
            {"هجده",18},{"نوزده",19},{"بیست",20},{"سی",30}
        };

        public static bool TryParse(string normalizedText, DateTime now, out DateTime remindAt, out string matchedPhrase)
        {
            remindAt = DateTime.MinValue;
            matchedPhrase = null;

            if (string.IsNullOrWhiteSpace(normalizedText))
                return false;

            string text = normalizedText.Trim();

            // ─── تاریخ مطلق شمسی: yyyy/MM/dd یا yyyy-MM-dd ─────────────────────
            Match absolute = Regex.Match(text, @"(1[3-5]\d{2}[/\-]\d{1,2}[/\-]\d{1,2})");
            if (absolute.Success)
            {
                try
                {
                    DateTime parsed = PersianDateHelper.ParsePersianDate(absolute.Groups[1].Value);
                    if (parsed != DateTime.MinValue)
                    {
                        remindAt = parsed.Date.AddHours(9);
                        matchedPhrase = absolute.Value;
                        return IsFuture(remindAt, now);
                    }
                }
                catch { /* قالبِ نامعتبر → شکست، نه حدس */ }
            }

            // ─── امروز / فردا ────────────────────────────────────────────────
            if (Regex.IsMatch(text, @"\bامروز\b"))
            {
                remindAt = now.Date.AddHours(17);
                matchedPhrase = "امروز";
                return IsFuture(remindAt, now);
            }
            if (Regex.IsMatch(text, @"\bفردا\b"))
            {
                remindAt = now.Date.AddDays(1).AddHours(9);
                matchedPhrase = "فردا";
                return true;
            }

            // ─── هفته آینده / هفته بعد (بدون عدد) ───────────────────────────
            Match weekPlain = Regex.Match(text, @"هفته\s+(آینده|بعد)");
            Match weekNum = Regex.Match(text, @"(\d+|[^\s\d]+)\s*هفته\s+(دیگر|بعد)");
            if (weekNum.Success)
            {
                int weeks = ParseNumberToken(weekNum.Groups[1].Value);
                if (weeks > 0)
                {
                    remindAt = now.Date.AddDays(weeks * 7).AddHours(9);
                    matchedPhrase = weekNum.Value;
                    return true;
                }
            }
            else if (weekPlain.Success)
            {
                remindAt = now.Date.AddDays(7).AddHours(9);
                matchedPhrase = weekPlain.Value;
                return true;
            }

            // ─── N روز دیگر / N روز بعد (عدد یا واژه‌ی عدد) ─────────────────
            Match dayNum = Regex.Match(text, @"(\d+|[^\s\d]+)\s*روز\s+(دیگر|بعد)");
            if (dayNum.Success)
            {
                int days = ParseNumberToken(dayNum.Groups[1].Value);
                if (days > 0)
                {
                    remindAt = now.Date.AddDays(days).AddHours(9);
                    matchedPhrase = dayNum.Value;
                    return true;
                }
            }

            return false;
        }

        private static bool IsFuture(DateTime candidate, DateTime now)
        {
            return candidate > now;
        }

        // می‌تواند رقم («15») یا واژه («سه»، حتی ترکیبِ «بیست و یک») باشد.
        private static int ParseNumberToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return 0;
            token = token.Trim();

            int direct;
            if (int.TryParse(token, out direct))
                return direct;

            string[] parts = token.Split(new[] { " و ", " " }, StringSplitOptions.RemoveEmptyEntries);
            int sum = 0;
            bool any = false;
            foreach (string part in parts)
            {
                int val;
                if (WordNumbers.TryGetValue(part, out val))
                {
                    sum += val;
                    any = true;
                }
            }
            return any ? sum : 0;
        }
    }
}

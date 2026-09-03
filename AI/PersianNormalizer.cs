using System.Text;

namespace CaseManagement.AI
{
    // دستیار هوشمند — فاز ۱. یکسان‌سازی متنِ فارسی/دری پیش از تجزیه یا
    // جست‌وجو. طبق AI_ASSISTANT_PHASE1_FIXES.md §5، تبدیل ارقام باید *اولین*
    // گام باشد چون همه‌ی الگوهای عددی (پسوند تذکره، تلفن، تاریخ) روی خروجیِ
    // این کلاس با ارقام لاتین کار می‌کنند.
    public static class PersianNormalizer
    {
        public static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            StringBuilder sb = new StringBuilder(input.Length);

            foreach (char raw in input)
            {
                char c = raw;

                // ۱) ارقام فارسی/عربی → لاتین (اولین گام — پیش‌نیازِ هر تجزیه‌ی عددی)
                if (c >= '۰' && c <= '۹') { sb.Append((char)('0' + (c - '۰'))); continue; }
                if (c >= '٠' && c <= '٩') { sb.Append((char)('0' + (c - '٠'))); continue; }

                // ۲) یکسان‌سازی حروف عربی/فارسی
                if (c == 'ك') { sb.Append('ک'); continue; }
                if (c == 'ي') { sb.Append('ی'); continue; }
                if (c == 'ة') { sb.Append('ه'); continue; }
                // آموزش — رفعِ باگ: «آ» یک حرفِ مستقلِ فارسی است (نه فقط یک
                // هایِ همزه‌دار)؛ تبدیلِ آن به «ا» عبارتِ محرکِ «آخر» را که
                // ReminderDateParser/الگوهای تذکره به آن وابسته‌اند، خراب
                // می‌کرد. فقط همزه‌های واقعاً معادل عربی یکسان می‌شوند.
                if (c == 'أ' || c == 'إ') { sb.Append('ا'); continue; }
                if (c == 'ؤ') { sb.Append('و'); continue; }
                if (c == 'ئ') { sb.Append('ی'); continue; }

                // ۳) حذف اعراب/تشدید/تنوین عربی و کشیدگی (ـ)
                if (c >= 'ً' && c <= 'ٟ') continue;
                if (c == 'ـ') continue; // tatweel/کشیدگی

                // ۴) نیم‌فاصله و انواع فاصله → فاصلهٔ معمولی (برای تطبیق ساده‌تر)
                if (c == '‌' || c == ' ') { sb.Append(' '); continue; }

                sb.Append(c);
            }

            // فشرده‌سازی چند فاصلهٔ پیاپی و حذف فاصله‌های ابتدا/انتها
            string collapsed = CollapseSpaces(sb.ToString());
            return collapsed.Trim();
        }

        private static string CollapseSpaces(string s)
        {
            StringBuilder result = new StringBuilder(s.Length);
            bool lastWasSpace = false;
            foreach (char c in s)
            {
                bool isSpace = c == ' ';
                if (isSpace && lastWasSpace) continue;
                result.Append(c);
                lastWasSpace = isSpace;
            }
            return result.ToString();
        }
    }
}

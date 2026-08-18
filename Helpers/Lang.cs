using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // هسته‌ی چندزبانگی: فارسی / پشتو / عربی / اردو / انگلیسی
    //
    // آموزش — چرا کلیدِ ترجمه «خودِ متنِ فارسی» است و نه کدهایی مثل BTN_SAVE:
    // این پروژه ۳۳ هزار خط کد و بیش از ۵۸۰ رشته‌ی نمایشی دارد که مستقیم داخل
    // کد نوشته شده‌اند. اگر بخواهیم همه را با کلیدِ نمادین جایگزین کنیم، باید
    // ده‌ها فایل و همه‌ی فایل‌های Designer را دست بزنیم — یعنی همان «بازنویسی»
    // که قرار است انجام ندهیم و بزرگ‌ترین منبعِ ریسک است.
    // با کلید بودنِ خودِ متنِ فارسی:
    //   • وقتی زبان «فارسی» است، هیچ جست‌وجویی لازم نیست و رفتار برنامه
    //     دقیقاً بیت‌به‌بیت مثل قبل است (هیچ ریسکی برای وضعیت فعلی).
    //   • هر رشته‌ای که ترجمه‌اش نباشد، فارسی می‌ماند — یعنی هرگز متنِ خالی یا
    //     کلیدِ خام مثل «BTN_SAVE» به کاربر نشان داده نمی‌شود.
    //
    // ⚠ بسیار مهم — چه چیزی هرگز ترجمه نمی‌شود:
    // در این پروژه ۱۶۳ رشته‌ی فارسی وجود دارد که «مقدارِ داده» هستند، نه متنِ
    // نمایشی: مقادیری مثل «فعال»، «قطع»، «ابتدایی»، «دانشگاه» که در دیتابیس
    // ذخیره می‌شوند و در WHERE و مقایسه‌ها استفاده می‌شوند. اگر این‌ها ترجمه
    // شوند، ذخیره‌ی پرونده مقدارِ انگلیسی در دیتابیس می‌نویسد و همه‌ی
    // کوئری‌ها، فیلترها، داشبورد و همگام‌سازی HTML از کار می‌افتند.
    // برای همین ترجمه فقط روی متنِ «نمایشیِ» کنترل‌ها اعمال می‌شود و
    // LanguageSweep عمداً به ComboBox/TextBox/سلول‌های جدول دست نمی‌زند.
    // ═════════════════════════════════════════════════════════════════════════
    public static class Lang
    {
        public const string Persian = "fa";
        public const string Pashto  = "ps";
        public const string Arabic  = "ar";
        public const string Urdu    = "ur";
        public const string English = "en";

        public const string SettingKey = "AppLanguage";

        private static string _current = Persian;
        private static Dictionary<string, string> _map;
        private static readonly object _lock = new object();

        public static event EventHandler LanguageChanged;

        public static string Current
        {
            get { return _current; }
        }

        // انگلیسی تنها زبانِ چپ‌به‌راست در این فهرست است.
        public static bool IsRightToLeft
        {
            get { return _current != English; }
        }

        public static string[] AllCodes
        {
            get { return new[] { Persian, Pashto, Arabic, Urdu, English }; }
        }

        // نامِ هر زبان، به خودِ همان زبان (قاعده‌ی استانداردِ فهرست‌های زبان).
        public static string DisplayName(string code)
        {
            switch (code)
            {
                case Pashto:  return "پښتو";
                case Arabic:  return "العربية";
                case Urdu:    return "اردو";
                case English: return "English";
                default:      return "فارسی";
            }
        }

        // ─── بارگذاری و تعویض زبان ───────────────────────────────────────────
        public static void Initialize()
        {
            string saved = Persian;
            try { saved = SettingsHelper.Get(SettingKey, Persian); }
            catch { /* اگر تنظیمات هنوز آماده نیست، فارسی می‌ماند */ }

            SetLanguage(string.IsNullOrWhiteSpace(saved) ? Persian : saved, persist: false);
        }

        public static void SetLanguage(string code, bool persist = true)
        {
            if (string.IsNullOrWhiteSpace(code)) code = Persian;
            code = code.Trim().ToLowerInvariant();
            if (Array.IndexOf(AllCodes, code) < 0) code = Persian;

            lock (_lock)
            {
                _current = code;
                _map = code == Persian ? null : LoadMap(code);
            }

            if (persist)
            {
                try { SettingsHelper.Set(SettingKey, code); } catch { }
            }

            ApplyCulture(code);

            if (LanguageChanged != null) LanguageChanged(null, EventArgs.Empty);
        }

        // ─── ترجمه‌ی یک متن ──────────────────────────────────────────────────
        // اگر زبان فارسی باشد یا ترجمه‌ای نباشد، همان ورودی برمی‌گردد.
        public static string T(string persianText)
        {
            if (string.IsNullOrEmpty(persianText)) return persianText;

            Dictionary<string, string> map = _map;
            if (map == null) return persianText;

            // ۱) تطبیق دقیق
            string value;
            if (map.TryGetValue(persianText.Trim(), out value) && !string.IsNullOrEmpty(value))
                return value;

            // ۲) تطبیق با یکسان‌سازیِ شکستِ خط.
            // آموزش — متن‌های چندخطی گاهی با "\n" و گاهی با "\r\n" ساخته
            // می‌شوند؛ بدون این یکسان‌سازی، کلیدِ فرهنگِ لغت با متنِ واقعیِ
            // برچسب تطبیق نمی‌خورد و ترجمه بی‌اثر می‌ماند.
            if (persianText.IndexOf('\n') >= 0)
            {
                string normalized = persianText.Replace("\r\n", "\n").Trim();
                foreach (var pair in map)
                {
                    if (pair.Key.IndexOf('\n') < 0) continue;
                    if (pair.Key.Replace("\r\n", "\n").Trim() == normalized &&
                        !string.IsNullOrEmpty(pair.Value))
                        return pair.Value;
                }
            }

            // ۲) تطبیقِ هوشمند (توضیح در TranslateDecorated)
            return TranslateDecorated(persianText, map);
        }

        // ═════════════════════════════════════════════════════════════════════
        // آموزش — چرا این لایه لازم شد (با اندازه‌گیری پیدا شد، نه حدس):
        //
        // سنجشِ پوششِ ترجمه نشان داد بخش بزرگی از متن‌هایِ «ترجمه‌نشده» در واقع
        // همان کلماتی هستند که در فرهنگ لغت وجود دارند، ولی با یک تزئین:
        //     «✔  ذخیره»   «✕  حذف»   «⇑  اکسل»   «فیلتر:»   «+  جدید»
        // چون UiTheme.CreateButton آیکون را به ابتدای متن می‌چسباند و بعضی
        // برچسب‌ها دونقطه دارند، جست‌وجویِ دقیق شکست می‌خورد.
        //
        // به‌جای افزودنِ صدها مدخلِ تکراری برای هر ترکیب، همان‌جا تزئین جدا
        // می‌شود، هسته ترجمه می‌گردد و تزئین دوباره سرِ جایش برمی‌گردد. یعنی
        // یک مدخلِ «ذخیره» همه‌ی حالت‌هایش را پوشش می‌دهد.
        // ═════════════════════════════════════════════════════════════════════
        private static string TranslateDecorated(string text, Dictionary<string, string> map)
        {
            string working = text;

            // ── پسوند: نقطه‌گذاریِ پایانی ──
            // آموزش — این فهرست فقط «:» نبود و با آزمون کامل شد: بعضی متن‌ها
            // با نقطه ذخیره می‌شوند ولی بدونِ نقطه نمایش داده می‌گردند (یا
            // برعکس)، و بدونِ این یکسان‌سازی تطبیق شکست می‌خورد.
            string suffix = "";
            string trimmed = working.TrimEnd();
            if (trimmed.Length > 1 && IsTrailingPunctuation(trimmed[trimmed.Length - 1]))
            {
                suffix = trimmed.Substring(trimmed.Length - 1);
                working = trimmed.Substring(0, trimmed.Length - 1);
            }

            // ── پیشوند: آیکون/شماره/نشانه + فاصله ──
            // هر چیزی پیش از اولین حرفِ واقعی، تزئین حساب می‌شود.
            int firstLetter = -1;
            for (int i = 0; i < working.Length; i++)
            {
                if (IsWordChar(working[i])) { firstLetter = i; break; }
            }

            if (firstLetter < 0) return text;   // اصلاً حرفی ندارد

            // ── پسوندِ تزئینی: آیکونِ انتهایی مثل «بعدی  ◀» ──
            int lastLetter = -1;
            for (int i = working.Length - 1; i >= firstLetter; i--)
            {
                if (IsWordChar(working[i])) { lastLetter = i; break; }
            }

            string prefix = working.Substring(0, firstLetter);

            // آموزش — چرا چند نامزد به‌ترتیب امتحان می‌شود و نه یک برش ثابت:
            // برشِ «هرچه بعد از آخرین حرف» برای «بعدی  ◀» درست است، ولی برای
            // «بارگذاری Backup (Restore)» پرانتزِ پایانی را هم تزئین حساب
            // می‌کرد و تطبیق می‌شکست. پس اول کاملِ متن امتحان می‌شود و تنها
            // در صورت شکست، دُمِ تزئینی جدا می‌گردد.
            string full = working.Substring(firstLetter).Trim();
            string translated;

            if (map.TryGetValue(full, out translated) && !string.IsNullOrEmpty(translated))
                return prefix + translated + suffix;

            if (lastLetter >= 0 && lastLetter < working.Length - 1)
            {
                string tail = working.Substring(lastLetter + 1);
                string core = working.Substring(firstLetter, lastLetter - firstLetter + 1).Trim();

                if (core.Length > 0 &&
                    map.TryGetValue(core, out translated) && !string.IsNullOrEmpty(translated))
                {
                    return prefix + translated + tail + suffix;
                }
            }

            // آموزش — آخرین تلاش، برای اختلافِ نقطه‌گذاریِ پایانی: بعضی متن‌ها
            // در کد با نقطه ذخیره شده‌اند ولی بدونِ نقطه نمایش داده می‌شوند
            // (مثل «پیام روز» در داشبورد). بدونِ این تطبیق، همان یک نقطه باعث
            // می‌شد ترجمه پیدا نشود.
            foreach (string mark in new[] { ".", "،", "؛", "!", "؟" })
            {
                if (map.TryGetValue(full + mark, out translated) && !string.IsNullOrEmpty(translated))
                    return prefix + TrimTrailingPunctuation(translated) + suffix;
            }

            return text;
        }

        private static bool IsTrailingPunctuation(char c)
        {
            return c == ':' || c == '：' || c == '.' || c == '،' ||
                   c == '؛' || c == ';' || c == '!' || c == '؟' || c == '?';
        }

        private static string TrimTrailingPunctuation(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            string t = value.TrimEnd();
            if (t.Length > 1 && IsTrailingPunctuation(t[t.Length - 1]))
                return t.Substring(0, t.Length - 1);
            return value;
        }

        // آموزش — چرا ارقام «حرف» حساب نمی‌شوند: عنوان‌هایی مثل «۱) صورت حساب
        // کلی» یا «۳.   اسناد را ...» با شماره شروع می‌شوند. ارقامِ فارسی در
        // همان محدوده‌ی یونیکدِ حروفِ فارسی‌اند (U+06F0..U+06F9)، پس اگر حرف
        // حساب شوند، شماره جزوِ متن می‌ماند و جست‌وجو شکست می‌خورد. با کنار
        // گذاشتنشان، یک مدخلِ «صورت حساب کلی» همه‌ی شماره‌ها را پوشش می‌دهد.
        private static bool IsWordChar(char c)
        {
            if (c >= 0x06F0 && c <= 0x06F9) return false;         // ارقام فارسی
            if (c >= 0x0660 && c <= 0x0669) return false;         // ارقام عربی
            if (c >= '0' && c <= '9') return false;               // ارقام لاتین
            if (c >= 0x0600 && c <= 0x06FF) return true;          // حروف فارسی/عربی
            if (c >= 'a' && c <= 'z') return true;
            if (c >= 'A' && c <= 'Z') return true;
            return false;
        }

        // ─── منبعِ ترجمه‌ها ───────────────────────────────────────────────────
        // ابتدا ترجمه‌های داخلی (LangData) بارگذاری می‌شوند، سپس اگر فایلِ
        // بیرونی وجود داشته باشد روی آن‌ها نوشته می‌شود. این‌طور مترجمِ بومی
        // می‌تواند بدون کامپایلِ مجدد، متن‌ها را اصلاح یا تکمیل کند.
        private static Dictionary<string, string> LoadMap(string code)
        {
            Dictionary<string, string> map = LangData.Build(code);

            try
            {
                string path = ExternalFilePath(code);
                if (File.Exists(path))
                {
                    foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;

                        int sep = line.IndexOf('=');
                        if (sep <= 0) continue;

                        string key = line.Substring(0, sep).Trim();
                        string val = line.Substring(sep + 1).Trim();
                        if (key.Length > 0 && val.Length > 0) map[key] = val;
                    }
                }
            }
            catch { /* فایلِ بیرونی خراب باشد، ترجمه‌های داخلی سرِجایشان می‌مانند */ }

            return map;
        }

        public static string ExternalFilePath(string code)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");
            return Path.Combine(folder, code + ".txt");
        }

        // ─── فرهنگِ رشته و عدد ───────────────────────────────────────────────
        // آموزش — عمداً فرهنگِ «فرمت» را دست نمی‌زنیم مگر برای انگلیسی: کلِ
        // برنامه تاریخ‌ها را با PersianDateHelper و اعدادِ ذخیره‌شده را با
        // InvariantCulture می‌نویسد. عوض کردنِ CurrentCulture می‌تواند قالبِ
        // ذخیره‌ی تاریخ/عدد را تغییر دهد و داده را خراب کند.
        private static void ApplyCulture(string code)
        {
            try
            {
                CultureInfo ui;
                switch (code)
                {
                    case Pashto:  ui = new CultureInfo("ps-AF"); break;
                    case Arabic:  ui = new CultureInfo("ar");    break;
                    case Urdu:    ui = new CultureInfo("ur");    break;
                    case English: ui = new CultureInfo("en-US"); break;
                    default:      ui = new CultureInfo("fa-IR"); break;
                }
                System.Threading.Thread.CurrentThread.CurrentUICulture = ui;
            }
            catch { /* اگر ویندوز آن زبان را نداشت، مهم نیست؛ ترجمه‌ها مستقل‌اند */ }
        }
    }
}

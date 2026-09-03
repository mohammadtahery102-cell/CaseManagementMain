using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // بخش ۳ — نوع تذکره (کارت هویت افغانستان)
    //
    // در افغانستان دو نوع تذکره وجود دارد:
    //   • تذکره الکترونیکی — قالب ثابت 0000-0000-00000 (۴-۴-۵ رقم)
    //   • تذکره کاغذی      — فقط رقم، حداکثر ۱۵ رقم
    //
    // آموزش — چرا «نوع» ذخیره می‌شود و از روی شماره تشخیص داده نمی‌شود:
    // تشخیص خودکار از روی طول/قالب شماره شکننده است (شماره کاغذی هم می‌تواند
    // ۱۳ رقم باشد) و داده‌ی قدیمی را اشتباه دسته‌بندی می‌کند. پس نوع، یک
    // ستون مستقل در دیتابیس است و کاربر آن را صریح انتخاب می‌کند.
    //
    // این کلاس مرجع واحدِ هر دو فرم (FrmCase و FrmFamily) است تا قاعده‌ی
    // اعتبارسنجی در دو جا تکرار (و با هم ناهماهنگ) نشود.
    // ─────────────────────────────────────────────────────────────────────────
    public static class IdCardHelper
    {
        // ⚠ این سه مقدار «داده» هستند و در دیتابیس ذخیره می‌شوند؛ هرگز نباید
        // ترجمه شوند (قاعده‌ی توضیح‌داده‌شده در بالای Helpers/Lang.cs).
        public const string Electronic = "تذکره الکترونیکی";
        public const string Paper      = "تذکره کاغذی";

        // فقط برای نمایش در آمار/گزارش‌ها — در دیتابیس ذخیره نمی‌شود
        // (نبودِ تذکره = هم نوع و هم شماره خالی).
        public const string NoneDisplay = "بدون تذکره";

        // رکوردهای قدیمیِ پیش از این قابلیت: شماره دارند ولی نوعشان ثبت نشده.
        // عمداً «کاغذی» فرض نمی‌شوند (خواسته‌ی صریح: نوع از روی شماره تشخیص
        // داده نشود)، پس گروه جداگانه‌ی خودشان را دارند.
        public const string UnknownDisplay = "نوع نامشخص";

        public const int ElectronicDigits = 13;   // ۴ + ۴ + ۵
        public const int ElectronicLength = 15;   // با احتساب دو خط تیره
        public const int PaperMaxDigits   = 15;

        public static readonly string[] Values = { Electronic, Paper };

        // ─── پر کردن کمبوی «نوع تذکره» ───────────────────────────────────────
        // includeAll=true برای فرم‌های جستجو/فیلتر (گزینه‌ی «همه»).
        public static void FillCombo(ComboBox combo, bool includeAll = false, bool includeNone = false)
        {
            if (combo == null) return;

            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            if (includeAll) combo.Items.Add("همه");
            combo.Items.Add("");            // خالی = هنوز مشخص نشده
            combo.Items.Add(Electronic);
            combo.Items.Add(Paper);
            if (includeNone) combo.Items.Add(NoneDisplay);
            combo.SelectedIndex = 0;
        }

        // ─── فقط ارقام (با یکسان‌سازی ارقام فارسی/عربی به لاتین) ──────────────
        // آموزش — کاربر ممکن است با صفحه‌کلید فارسی «۱۲۳» تایپ کند؛ بدون این
        // یکسان‌سازی، آن ارقام «کاراکتر غیرمجاز» شمرده و حذف می‌شدند.
        public static string DigitsOnly(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            StringBuilder sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c >= '0' && c <= '9')            sb.Append(c);
                else if (c >= 0x06F0 && c <= 0x06F9) sb.Append((char)('0' + (c - 0x06F0)));  // ارقام فارسی
                else if (c >= 0x0660 && c <= 0x0669) sb.Append((char)('0' + (c - 0x0660)));  // ارقام عربی
            }
            return sb.ToString();
        }

        // ─── درج خودکار خط تیره: 0000-0000-00000 ─────────────────────────────
        public static string FormatElectronic(string digits)
        {
            digits = DigitsOnly(digits);
            if (digits.Length > ElectronicDigits)
                digits = digits.Substring(0, ElectronicDigits);

            StringBuilder sb = new StringBuilder(ElectronicLength);
            for (int i = 0; i < digits.Length; i++)
            {
                if (i == 4 || i == 8) sb.Append('-');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        // ─── نمایش استاندارد شماره بر اساس نوع (برای چاپ/خروجی) ──────────────
        public static string Normalize(string cardType, string number)
        {
            string raw = (number ?? "").Trim();
            if (raw.Length == 0) return "";

            return (cardType ?? "").Trim() == Electronic
                ? FormatElectronic(raw)
                : DigitsOnly(raw);
        }

        // ─── اعتبارسنجی وابسته به نوع انتخاب‌شده ─────────────────────────────
        // شماره‌ی خالی همیشه مجاز است (این فیلد اجباری نیست و رفتار قبلی فرم‌ها
        // هم همین بود؛ اجباری کردنش یعنی شکستن ثبت پرونده‌های ناقصِ موجود).
        public static bool IsValid(string cardType, string number, out string error)
        {
            error = "";

            string type = (cardType ?? "").Trim();
            string raw  = (number  ?? "").Trim();

            if (raw.Length == 0)
                return true;

            if (type == Electronic)
            {
                string digits = DigitsOnly(raw);
                if (digits.Length != ElectronicDigits || FormatElectronic(digits) != raw)
                {
                    error = "شماره تذکره الکترونیکی باید دقیقاً با قالب 0000-0000-00000 و فقط با رقم وارد شود";
                    return false;
                }
                return true;
            }

            if (type == Paper)
            {
                if (DigitsOnly(raw) != raw)
                {
                    error = "شماره تذکره کاغذی باید فقط شامل رقم باشد (بدون حرف یا علامت)";
                    return false;
                }
                if (raw.Length > PaperMaxDigits)
                {
                    error = "شماره تذکره کاغذی نباید بیشتر از " + PaperMaxDigits + " رقم باشد";
                    return false;
                }
                return true;
            }

            error = "نوع تذکره را انتخاب کنید";
            return false;
        }

        // ─── اتصال کمبوی نوع به فیلد شماره ───────────────────────────────────
        // آموزش — کل رفتار «ورودی هوشمند» در یک نقطه جمع شده است:
        //   • فقط رقم پذیرفته می‌شود (حرف/علامت خودکار حذف می‌شود)
        //   • در حالت الکترونیکی، خط تیره‌ها خودکار درج می‌شوند
        //   • با تغییر نوع تذکره، قالب و سقف طول بلافاصله عوض می‌شود
        // به‌جای KeyPress (که Paste و تغییر برنامه‌ای را پوشش نمی‌دهد) روی
        // TextChanged کار می‌کند و با یک پرچم از بازگشت بی‌نهایت جلوگیری می‌کند.
        public static void Attach(ComboBox typeCombo, TextBox numberBox)
        {
            if (typeCombo == null || numberBox == null) return;

            bool[] busy = { false };

            EventHandler reformat = delegate
            {
                if (busy[0]) return;
                busy[0] = true;
                try
                {
                    string type = typeCombo.Text.Trim();

                    int caret = Math.Max(0, Math.Min(numberBox.SelectionStart, numberBox.Text.Length));
                    int digitsBeforeCaret = DigitsOnly(numberBox.Text.Substring(0, caret)).Length;

                    string digits = DigitsOnly(numberBox.Text);
                    string formatted;

                    if (type == Electronic)
                    {
                        if (digits.Length > ElectronicDigits) digits = digits.Substring(0, ElectronicDigits);
                        formatted = FormatElectronic(digits);
                        numberBox.MaxLength = ElectronicLength;
                    }
                    else
                    {
                        if (digits.Length > PaperMaxDigits) digits = digits.Substring(0, PaperMaxDigits);
                        formatted = digits;
                        numberBox.MaxLength = PaperMaxDigits;
                    }

                    if (numberBox.Text != formatted)
                    {
                        numberBox.Text = formatted;
                        numberBox.SelectionStart  = CaretAfterDigits(formatted, digitsBeforeCaret);
                        numberBox.SelectionLength = 0;
                    }
                }
                finally { busy[0] = false; }
            };

            numberBox.TextChanged            += reformat;
            typeCombo.SelectedIndexChanged   += reformat;
            typeCombo.TextChanged            += reformat;
        }

        // موقعیت مکان‌نما بعد از n اُمین رقمِ متنِ قالب‌بندی‌شده.
        private static int CaretAfterDigits(string formatted, int digitCount)
        {
            if (digitCount <= 0) return 0;

            int seen = 0;
            for (int i = 0; i < formatted.Length; i++)
            {
                if (formatted[i] >= '0' && formatted[i] <= '9')
                {
                    seen++;
                    if (seen == digitCount) return i + 1;
                }
            }
            return formatted.Length;
        }

        // ─── تشخیص نوع از روی الگوی دقیقِ شماره — فقط برای واردات خودکار (HTML Sync) ──
        // آموزش — این متد عمداً برخلاف قاعده‌ی بالای همین کلاس («نوع از روی شماره
        // تشخیص داده نمی‌شود، چون شکننده است») عمل می‌کند: در فایل HTML سامانه‌ی
        // مرکزی هیچ ستون «نوع تذکره»ی قابل‌اتکایی وجود ندارد، پس تنها راهِ پر کردن
        // این فیلد، تطبیق با الگوی دقیقِ شماره‌ی الکترونیکی است (نه صرفاً تعداد
        // رقم — به‌درخواستِ صریحِ کاربر). نتیجه در Wizard سینک پیش از اعمال
        // قابل‌بازبینی/رد است، پس ریسکِ «تشخیصِ اشتباه» با بازبینی دستی کاربر
        // پوشش داده می‌شود. این متد نباید در فرم‌های ورود دستی (FrmCase/FrmFamily)
        // استفاده شود؛ اعتبارسنجیِ ورودِ دستی همچنان طبق IsValid/FormatElectronic
        // بالا (قالبِ 0000-0000-00000) بدون تغییر باقی می‌ماند.
        //
        // ─── الگوهای پذیرفته‌شده‌ی شماره‌ی الکترونیکی ─────────────────────────
        // آموزش — چرا دو الگو: مشخصاتِ اولیه فقط #####-####-#### (۵-۴-۴) بود، اما
        // بررسیِ دادهٔ واقعیِ مؤسسه نشان داد در خروجیِ سامانهٔ مرکزی هیچ شماره‌ای
        // با آن الگو وجود ندارد و همهٔ تذکره‌های الکترونیکی به‌صورت
        // ####-####-##### (۴-۴-۵، مثل 1403-0900-80585) نوشته می‌شوند — گروهِ
        // چهاررقمیِ اول سالِ صدور است. با قاعدهٔ تک‌الگویی، هیچ رکوردی هرگز
        // «الکترونیکی» نمی‌شد. پس هر دو گروه‌بندیِ ۱۳ رقمیِ خط‌تیره‌دار پذیرفته
        // می‌شود (تصمیمِ صریحِ کاربر). شماره‌ی بدونِ خط تیره همچنان «کاغذی» است.
        // این با قالبِ ورودِ دستی (بالا) یکی نیست؛ آن عمداً دست‌نخورده می‌ماند.
        private static readonly Regex[] ElectronicImportPatterns =
        {
            new Regex(@"^\d{5}-\d{4}-\d{4}$", RegexOptions.Compiled),   // ۵-۴-۴
            new Regex(@"^\d{4}-\d{4}-\d{5}$", RegexOptions.Compiled),   // ۴-۴-۵ (دادهٔ واقعی)
        };

        // کاراکترهای نامرئی/جهت‌دهِ رایج در خروجی HTML (نیم‌فاصله، فاصله‌ی صفر،
        // علامت‌های راست‌به‌چپ/چپ‌به‌راست، BOM، فاصله‌ی سخت) — فقط برای تشخیصِ
        // الگو حذف می‌شوند؛ رشته‌ی ذخیره‌شده در دیتابیس هرگز از این متد خارج
        // نمی‌آید و دست‌نخورده می‌ماند.
        private static readonly char[] HiddenChars =
            { '‌', '​', '‎', '‏', '﻿', ' ' };

        // نرمال‌سازیِ یک نسخه‌ی موقت از رشته (فقط برای آزمایشِ الگو): یکسان‌سازیِ
        // ارقامِ فارسی/عربی، حذفِ کاراکترهای نامرئی، یکسان‌سازیِ انواع خط‌تیره
        // (–—‐‑−) به «-»، و حذفِ فاصله‌ی اطرافِ خط‌تیره.
        private static string NormalizeForDetection(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (Array.IndexOf(HiddenChars, c) >= 0) continue;
                if (c >= '0' && c <= '9') { sb.Append(c); continue; }
                if (c >= 0x06F0 && c <= 0x06F9) { sb.Append((char)('0' + (c - 0x06F0))); continue; } // فارسی
                if (c >= 0x0660 && c <= 0x0669) { sb.Append((char)('0' + (c - 0x0660))); continue; } // عربی
                if (c == '‐' || c == '‑' || c == '‒' || c == '–' ||
                    c == '—' || c == '−') { sb.Append('-'); continue; }               // انواع خط‌تیره
                sb.Append(c);
            }

            return Regex.Replace(sb.ToString(), @"\s*-\s*", "-").Trim();
        }

        public static string InferTypeFromNumber(string rawNumber)
        {
            if (string.IsNullOrWhiteSpace(rawNumber)) return null; // شماره خالی → نوع تعیین نمی‌شود

            string normalized = NormalizeForDetection(rawNumber);
            foreach (Regex pattern in ElectronicImportPatterns)
                if (pattern.IsMatch(normalized)) return Electronic;

            return Paper;
        }

        // ─── دسته‌بندی SQL برای آمار/گزارش‌ها ────────────────────────────────
        // یک عبارت CASE که هر رکورد را در یکی از سه گروه «الکترونیکی/کاغذی/
        // بدون تذکره» قرار می‌دهد. alias نامِ جدول (یا "") است.
        public static string CategorySql(string typeColumn, string numberColumn)
        {
            return
                "CASE WHEN " + typeColumn + " = '" + Electronic + "' THEN '" + Electronic + "' " +
                     "WHEN " + typeColumn + " = '" + Paper + "'      THEN '" + Paper + "' " +
                     "WHEN COALESCE(" + numberColumn + ", '') <> ''  THEN '" + UnknownDisplay + "' " +
                     "ELSE '" + NoneDisplay + "' END";
        }
    }
}

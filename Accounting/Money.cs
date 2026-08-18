using System;

namespace CaseManagement.Accounting
{
    // ─────────────────────────────────────────────────────────────────────────
    // Money — تنها نقطه‌ی گِردکردن و مقایسه‌ی مبالغ در ماژول حسابداری.
    //
    // آموزش — چرا این کلاس لازم شد: تمام ستون‌های مبلغ در جداول Acc* از نوع
    // REAL (همان double) هستند. double عدد اعشاری را دودویی نگه می‌دارد، پس
    // مقادیری مثل 0.1 دقیقاً قابل نمایش نیستند و در جمع‌های پی‌درپی (SUM روی
    // صدها ردیف) خطای ریز انباشته می‌شود. چون گزارش‌ها با فرمت N0 چاپ می‌شوند،
    // این خطا در چاپ دیده نمی‌شود اما در «مقایسه»ها خودش را نشان می‌دهد —
    // مثلاً وقتی می‌خواهیم بررسی کنیم «مانده ابتدا + دریافت − پرداخت» دقیقاً
    // برابر «مانده پایان» است یا نه، یک اختلاف 0.0000001 باعث اعلام نادرستِ
    // «مغایرت» می‌شود.
    //
    // تصمیم پروژه (تأییدشده): نوع ستون‌ها تغییر نمی‌کند (بدون مهاجرت پرریسک
    // روی دیتابیس در حال بهره‌برداری)؛ به‌جای آن هر مبلغ در مرزهای ورود،
    // محاسبه و مقایسه از همین‌جا عبور می‌کند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class Money
    {
        // افغانی در عمل واحد خُرد ندارد، اما ۲ رقم اعشار نگه می‌داریم تا
        // تبدیل ارز (دلار × نرخ) و مبالغ وارداتی از بکاپ‌های قدیمی بدون
        // قطع‌شدن رقم نگهداری شوند.
        public const int Decimals = 2;

        // آستانه‌ی برابری: هر اختلافی کمتر از نیمِ آخرین رقمِ معنادار، صرفاً
        // نویزِ ممیز شناور است و «مغایرت حسابداری» محسوب نمی‌شود.
        public const double Epsilon = 0.005;

        // گِردکردن استاندارد بانکی‌نشده (MidpointRounding.AwayFromZero) —
        // یعنی 0.5 به 1 گِرد می‌شود، همان چیزی که حسابدارِ انسانی انتظار دارد
        // و با رفتار Math.Round پیش‌فرض (ToEven) فرق دارد.
        public static double Round(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            return Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
        }

        // گِردکردن به واحد کامل (برای مبالغ افغانیِ اسناد و مانده‌ها).
        public static double RoundWhole(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            return Math.Round(value, 0, MidpointRounding.AwayFromZero);
        }

        // آیا دو مبلغ از نظر حسابداری برابرند؟ هرگز از == روی double استفاده نکنید.
        public static bool AreEqual(double a, double b)
        {
            return Math.Abs(a - b) < Epsilon;
        }

        public static bool IsZero(double value)
        {
            return Math.Abs(value) < Epsilon;
        }

        // مبلغ معتبر برای ثبت یک رویداد مالی: عددِ متناهی و بزرگ‌تر از صفر.
        public static bool IsValidPositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value)
                   && value > 0 && value <= MaxAmount;
        }

        // سقف مبلغ — با Maximum کنترل‌های NumericUpDown فرم حسابداری یکی است.
        // آموزش: این سقف قبلاً فقط در UI بود و در محاسبه‌ی تبدیل ارز به‌صورت
        // «بریدنِ خاموش» (Math.Min) اعمال می‌شد؛ یعنی مبلغ بزرگ‌تر از سقف بدون
        // هیچ پیامی به سقف تبدیل و ذخیره می‌شد. حالا قابل تشخیص و اعلام است.
        public const double MaxAmount = 1000000000d;

        // تبدیل ارز: مبلغ افغانی حاصل از مبلغ دلاری و نرخ.
        // نتیجه به واحد کامل گِرد می‌شود (اسناد افغانی اعشار ندارند).
        public static double Convert(double foreignAmount, double rate)
        {
            return RoundWhole(foreignAmount * rate);
        }

        // آیا سه‌گانه‌ی (مبلغ افغانی، مبلغ ارزی، نرخ) با هم سازگارند؟
        // اختلاف تا ۱ واحد به‌خاطر گِردکردن پذیرفته می‌شود.
        public static bool IsConversionConsistent(double amount, double foreignAmount, double rate)
        {
            if (foreignAmount <= 0 || rate <= 0) return true;   // تبدیل ارزی در کار نیست
            return Math.Abs(amount - Convert(foreignAmount, rate)) <= 1.0;
        }
    }
}

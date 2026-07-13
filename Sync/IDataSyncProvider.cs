using System;

namespace CaseManagement.Sync
{
    // ─────────────────────────────────────────────────────────────────────────
    // قرارداد یک «منبع همگام‌سازی». معماری طوری طراحی شده که فردا بتوان
    // Providerهای دیگری (Excel، JSON، API مرکزی، ...) بدون تغییر Comparer/Engine
    // اضافه کرد؛ فقط کافی است این interface را پیاده‌سازی کنند.
    //
    // مسئولیت Provider فقط «تجزیه و اعتبارسنجی ساختاری» است — نه مقایسه با
    // دیتابیس (کار Comparer) و نه نوشتن (کار Engine). این جداسازی مسئولیت باعث
    // می‌شود منطق مقایسه/نوشتن برای همه‌ی منابع یکسان و آزموده بماند.
    // ─────────────────────────────────────────────────────────────────────────
    public interface IDataSyncProvider
    {
        // نام نمایشی منبع (برای Wizard).
        string Name { get; }

        // مرحله ۲ Wizard: فایل(ها) را می‌خواند و به رکوردهای خام تبدیل می‌کند.
        // progress برای Progress Bar (می‌تواند null باشد).
        ParsedSyncData Parse(SyncSource source, IProgress<SyncProgress> progress);

        // مرحله ۳ Wizard: اعتبارسنجی ساختاری (کد عمومی خالی، ستون‌های ضروری
        // گمشده، ...). فهرست خطاها را برمی‌گرداند (خالی = بدون خطا).
        System.Collections.Generic.List<string> Validate(ParsedSyncData data);
    }
}

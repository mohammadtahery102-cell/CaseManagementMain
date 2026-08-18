using System;

namespace CaseManagement.Accounting
{
    // خطای «قاعده‌ی حسابداری نقض شد» — جدا از خطاهای فنی (SQL/IO) تا فرم
    // بتواند پیام آماده‌ی فارسیِ آن را مستقیم به کاربر نشان دهد، بدون آن‌که
    // جزئیات فنی نشت کند.
    //
    // آموزش: این استثنا زمانی پرتاب می‌شود که عملیات از نظر فنی ممکن است اما
    // از نظر حسابداری مجاز نیست — مثل ویرایش رکوردِ یک دوره‌ی بسته‌شده، دست‌زدن
    // به رکوردِ مرکزی دیگر، یا ثبت مبلغ نامعتبر.
    [Serializable]
    public class AccountingRuleException : Exception
    {
        public AccountingRuleException(string message) : base(message) { }

        protected AccountingRuleException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }

    // حالت خاصِ «سند تکراری» — جدا نگه داشته می‌شود چون برخلاف بقیه‌ی قواعد،
    // این یکی قابل تأیید توسط کاربر است: ممکن است واقعاً دو پرداخت جداگانه با
    // مبلغ و تاریخ یکسان وجود داشته باشد. فرم با دیدن این استثنا از کاربر
    // تأیید می‌گیرد و در صورت تأیید دوباره با confirmedDuplicate=true ثبت می‌کند.
    [Serializable]
    public class AccountingDuplicateException : AccountingRuleException
    {
        public AccountingDuplicateException(string message) : base(message) { }

        protected AccountingDuplicateException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}

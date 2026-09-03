using System;
using System.Collections.Generic;

namespace CaseManagement.AI
{
    // دستیار هوشمند — فاز ۱. مدل‌های داده‌ی مشترک بین PersianNluEngine،
    // AiOrchestrator و Handler ها. طبق AI_ASSISTANT_PHASE1_FIXES.md §1،
    // GrandFatherName در TblCase اصلاً وجود ندارد و اینجا هم نیامده.

    public static class AiIntent
    {
        public const string SearchCase       = "Search.Case";
        public const string SearchFamily     = "Search.Family";
        public const string NoRecentService  = "Query.NoRecentService";
        public const string CreateReminder   = "Command.CreateReminder";
        public const string Unknown          = "Unknown";
    }

    // موجودیت‌های استخراج‌شده از یک پرسشِ زبانِ طبیعی.
    public class AiEntities
    {
        public string PersonName;          // نامی که باید با HeadFullName/HeadFatherName/MemberName مطابقت یابد
        public string TazkiraNo;           // شماره کامل تذکره
        public string TazkiraSuffix;       // فقط چند رقم آخر («آخر تذکره‌اش ۵۴»)
        public string Province;
        public string District;
        public string Phone;
        public string ServiceStatus;       // «فعال» / «غیرفعال»
        public bool   IsCountQuery;        // «چند یتیم ... داریم؟»
        public int?   NoRecentServiceDays; // «۹۰ روز اخیر»
        public string CaseReferenceRaw;    // عدد/متنِ خامِ اشاره به پرونده («پرونده ۲۴۲۴»)
        public DateTime? ResolvedDate;     // تاریخِ حل‌شده برای یادآوری (فقط آینده)
        public string ReminderTitle;       // عنوانِ ساخته‌شده برای یادآوری
    }

    // نتیجه‌ی خامِ PersianNluEngine، پیش از حل مرجعِ پرونده.
    public class AiNluResult
    {
        public string      Intent;
        public AiEntities  Entities;
        public double      Confidence; // ۰..۱، طبق فرمولِ فیکس‌ها §۶
    }

    public class AiResultItem
    {
        public string EntityType; // "Case" | "Family" | "Reminder"
        public int    EntityId;   // همیشه CasID (حتی برای نتیجه‌ی خانواده — چون FrmFamily مستقل نیست)
        public string DisplayTitle;
        public string DisplaySubtitle;
    }

    public class AiResponse
    {
        public string ResponseText;
        public List<AiResultItem> Results = new List<AiResultItem>();
        public string Intent;
        public double Confidence;
        // اگر true باشد یعنی پاسخ فقط یک سؤالِ روشن‌کننده است و هیچ نوشتنی
        // (مثلاً ساختِ یادآوری) انجام نشده — طبق فیکس‌ها §۶، آستانه‌ی سخت‌گیرانه.
        public bool NeedsClarification;
    }

    public interface IAiIntentHandler
    {
        AiResponse Handle(AiEntities entities, string rawQuery, double baseConfidence);
    }
}

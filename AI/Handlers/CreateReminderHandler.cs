using System;
using System.Collections.Generic;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.AI.Handlers
{
    // دستیار هوشمند — فاز ۱. ساختِ یادآوری از دستورِ زبانِ طبیعی.
    //
    // طبق فیکس‌ها §۶/§۷/§۹:
    //   • بدونِ تاریخِ آینده‌یِ حل‌شده، هرگز چیزی نوشته نمی‌شود (قانونِ سخت،
    //     مستقل از امتیازِ اطمینان).
    //   • TblReminder اصلاً ستونِ پیوندِ ساختاریافته به پرونده ندارد (طبق
    //     مشخصه، فقط دو ستونِ CreatedByAI/SourceQueryText تازه‌اند) — پس
    //     تطبیقِ پرونده فقط برای اعتبارسنجی/غنی‌سازیِ عنوان است.
    //   • AuditLogger.Log کانکشنِ خودش را باز می‌کند؛ اتمیک‌بودنِ واقعی با
    //     درجِ TblReminder ممکن نیست — بلافاصله بعد از درج صدا زده می‌شود تا
    //     فاصله‌ی زمانی کمینه شود (نه صفر).
    public class CreateReminderHandler : IAiIntentHandler
    {
        public AiResponse Handle(AiEntities entities, string rawQuery, double baseConfidence)
        {
            if (!entities.ResolvedDate.HasValue)
            {
                return new AiResponse
                {
                    Intent = AiIntent.CreateReminder,
                    NeedsClarification = true,
                    ResponseText = "چه زمانی؟ لطفاً بازه را دقیق‌تر بگویید (مثلاً «سه روز دیگر»، «هفته آینده» یا تاریخِ شمسی مثل «۱۴۰۴/۰۶/۱۰»)."
                };
            }

            DatabaseHelper db = new DatabaseHelper();
            string title = entities.ReminderTitle;
            double confidence = baseConfidence;

            string reference = !string.IsNullOrEmpty(entities.CaseReferenceRaw) ? entities.CaseReferenceRaw : entities.PersonName;
            if (!string.IsNullOrEmpty(reference))
            {
                List<CaseRecord> matches = CaseReferenceResolver.Resolve(db, reference);
                if (matches.Count > 1)
                {
                    var items = new List<AiResultItem>();
                    foreach (CaseRecord m in matches)
                        items.Add(new AiResultItem { EntityType = "Case", EntityId = m.CasID, DisplayTitle = m.HeadFullName, DisplaySubtitle = m.Province + " · " + m.HeadTazkiraNo });

                    return new AiResponse
                    {
                        Intent = AiIntent.CreateReminder,
                        NeedsClarification = true,
                        ResponseText = matches.Count + " پرونده با این مشخصات یافت شد — کدام‌یک؟ لطفاً شماره/کدِ دقیق را بگویید.",
                        Results = items
                    };
                }
                if (matches.Count == 1)
                {
                    confidence = Math.Min(1, confidence + 0.15);
                    title = BuildEnrichedTitle(entities, matches[0]);
                }
                // صفر تطبیق: یادآوریِ متنیِ ساده همچنان ثبت می‌شود — نبودِ پرونده
                // در سیستم نباید مانعِ یک یادداشتِ یادآوریِ معتبر شود.
            }

            if (string.IsNullOrWhiteSpace(title))
                title = Truncate(rawQuery, 120);

            string remindAtText = entities.ResolvedDate.Value.ToString("yyyy-MM-dd HH:mm");
            int reminderId;
            try
            {
                reminderId = (int)db.ExecuteInsertReturningId(
                    "INSERT INTO TblReminder (Title, Note, RemindAt, IsDone, IsNotified, CenterID, CreatedBy, CreatedByAI, SourceQueryText) " +
                    "VALUES (@Title, NULL, @RemindAt, 0, 0, @CenterID, @CreatedBy, 1, @SourceQuery);",
                    SqlParam.P("@Title", title),
                    SqlParam.P("@RemindAt", remindAtText),
                    SqlParam.P("@CenterID", SecurityContext.CurrentCenterId),
                    SqlParam.P("@CreatedBy", SecurityContext.Username),
                    SqlParam.P("@SourceQuery", rawQuery ?? ""));
            }
            catch (Exception)
            {
                return new AiResponse
                {
                    Intent = AiIntent.CreateReminder,
                    NeedsClarification = true,
                    ResponseText = "ثبتِ یادآوری ناموفق بود. لطفاً دوباره تلاش کنید."
                };
            }

            AuditLogger.Log("AI:CreateReminder", "TblReminder", reminderId, null,
                "Title=" + title + "; RemindAt=" + remindAtText + "; SourceQuery=" + rawQuery);

            var response = new AiResponse
            {
                Intent = AiIntent.CreateReminder,
                Confidence = confidence,
                ResponseText = "یادآوری «" + title + "» برای " +
                    PersianDateHelper.ToPersianDateString(entities.ResolvedDate.Value) +
                    " ساعت " + entities.ResolvedDate.Value.ToString("HH:mm") + " ثبت شد."
            };
            response.Results.Add(new AiResultItem
            {
                EntityType = "Reminder",
                EntityId = reminderId,
                DisplayTitle = title,
                DisplaySubtitle = "ایجادشده توسط دستیار هوشمند"
            });

            return response;
        }

        private static string BuildEnrichedTitle(AiEntities entities, CaseRecord match)
        {
            string label = !string.IsNullOrEmpty(match.Code) ? "کد " + match.Code
                : match.FormNo.HasValue ? "فورم " + match.FormNo.Value
                : "پرونده #" + match.CasID;

            string subject = match.HeadFullName + " (" + label + ")";

            if (!string.IsNullOrEmpty(entities.ReminderTitle))
            {
                // عنوانِ ساخته‌شده توسط PersianNluEngine را با هویتِ دقیقِ
                // پرونده جایگزین می‌کنیم (به‌جای شماره‌ی خامِ کاربر).
                if (entities.ReminderTitle.Contains("بررسی")) return "بررسی " + subject;
                if (entities.ReminderTitle.Contains("تماس"))  return "تماس با " + subject;
                if (entities.ReminderTitle.Contains("پیگیری")) return "پیگیری وضعیت " + subject;
            }
            return "یادآوری: " + subject;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}

using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Text;
using CaseManagement.AI.Handlers;
using CaseManagement.DAL;
using CaseManagement.Enterprise;
using CaseManagement.Helpers;

namespace CaseManagement.AI
{
    // دستیار هوشمند — فاز ۱. نقطه‌ی ورودِ واحد: نرمال‌سازی → تشخیصِ قصد →
    // بررسیِ مجوز → اجرا → ثبت. طبق فیکس‌ها §۵، مجوز پیش از هر چیز (حتیِ خودِ
    // NLU) بررسی می‌شود.
    public static class AiOrchestrator
    {
        public static AiResponse Handle(string rawQuery, ref int conversationId)
        {
            Stopwatch sw = Stopwatch.StartNew();
            DatabaseHelper db = new DatabaseHelper();

            if (!PermissionService.Require("AI.Search"))
            {
                return new AiResponse
                {
                    ResponseText = "شما اجازه‌ی استفاده از جست‌وجوی هوشمند را ندارید.",
                    NeedsClarification = true,
                    Intent = AiIntent.Unknown
                };
            }

            AiNluResult nlu = PersianNluEngine.Parse(rawQuery);
            AiResponse response;

            try
            {
                switch (nlu.Intent)
                {
                    case AiIntent.SearchCase:
                        response = new SearchCaseHandler().Handle(nlu.Entities, rawQuery, nlu.Confidence);
                        break;

                    case AiIntent.SearchFamily:
                        response = new SearchFamilyHandler().Handle(nlu.Entities, rawQuery, nlu.Confidence);
                        break;

                    case AiIntent.NoRecentService:
                        response = new NoRecentServiceHandler().Handle(nlu.Entities, rawQuery, nlu.Confidence);
                        break;

                    case AiIntent.CreateReminder:
                        if (!PermissionService.Require("AI.Reminders.Create"))
                        {
                            response = new AiResponse
                            {
                                ResponseText = "شما اجازه‌ی ایجاد یادآوری را ندارید.",
                                NeedsClarification = true
                            };
                        }
                        else
                        {
                            response = new CreateReminderHandler().Handle(nlu.Entities, rawQuery, nlu.Confidence);
                        }
                        break;

                    default:
                        response = new AiResponse
                        {
                            ResponseText = "متوجه نشدم. لطفاً واضح‌تر بپرسید — مثلاً نام، شماره تذکره یا ولایت را ذکر کنید.",
                            NeedsClarification = true
                        };
                        break;
                }
            }
            catch (Exception ex)
            {
                response = new AiResponse
                {
                    ResponseText = "در پردازشِ درخواست خطایی رخ داد. لطفاً دوباره تلاش کنید.",
                    NeedsClarification = true
                };
                Debug.WriteLine("[AiOrchestrator] " + ex);
            }

            response.Intent = string.IsNullOrEmpty(response.Intent) ? nlu.Intent : response.Intent;
            response.Confidence = nlu.Confidence;

            sw.Stop();

            try
            {
                Persist(db, ref conversationId, rawQuery, nlu, response, (int)sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // ثبتِ گفتگو غیرحیاتی است؛ نباید پاسخِ کاربر را خراب کند.
                Debug.WriteLine("[AiOrchestrator.Persist] " + ex);
            }

            return response;
        }

        private static void Persist(DatabaseHelper db, ref int conversationId, string rawQuery,
            AiNluResult nlu, AiResponse response, int elapsedMs)
        {
            if (conversationId <= 0)
            {
                conversationId = (int)db.ExecuteInsertReturningId(
                    "INSERT INTO AiConversation (UserID, CenterID, Title, StartedAt, LastMessageAt) " +
                    "VALUES (@UserID, @CenterID, @Title, datetime('now'), datetime('now'));",
                    SqlParam.P("@UserID", SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : DBNull.Value),
                    SqlParam.P("@CenterID", SecurityContext.CurrentCenterId),
                    SqlParam.P("@Title", (object)Truncate(rawQuery, 60) ?? DBNull.Value));
            }
            else
            {
                db.ExecuteNonQuery(
                    "UPDATE AiConversation SET LastMessageAt = datetime('now') WHERE ConversationID = @ID;",
                    SqlParam.P("@ID", conversationId));
            }

            db.ExecuteNonQuery(
                "INSERT INTO AiMessage (ConversationID, Sender, MessageText, CreatedAt) VALUES (@Cid, 'User', @Text, datetime('now'));",
                SqlParam.P("@Cid", conversationId),
                SqlParam.P("@Text", rawQuery ?? ""));

            db.ExecuteNonQuery(
                "INSERT INTO AiMessage (ConversationID, Sender, MessageText, IntentDetected, EntitiesJson, Confidence, ExecutionMs, CreatedAt) " +
                "VALUES (@Cid, 'Assistant', @Text, @Intent, @Entities, @Conf, @Ms, datetime('now'));",
                SqlParam.P("@Cid", conversationId),
                SqlParam.P("@Text", response.ResponseText ?? ""),
                SqlParam.P("@Intent", (object)response.Intent ?? DBNull.Value),
                SqlParam.P("@Entities", (object)SerializeEntities(nlu.Entities) ?? DBNull.Value),
                SqlParam.P("@Conf", response.Confidence),
                SqlParam.P("@Ms", elapsedMs));

            db.ExecuteNonQuery(
                "INSERT INTO AiIntentLog (UserID, CenterID, RawQuery, ParsedIntent, Confidence, Success, CreatedAt) " +
                "VALUES (@UserID, @CenterID, @Raw, @Intent, @Conf, @Success, datetime('now'));",
                SqlParam.P("@UserID", SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : DBNull.Value),
                SqlParam.P("@CenterID", SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value),
                SqlParam.P("@Raw", rawQuery ?? ""),
                SqlParam.P("@Intent", (object)nlu.Intent ?? DBNull.Value),
                SqlParam.P("@Conf", nlu.Confidence),
                SqlParam.P("@Success", response.NeedsClarification ? 0 : 1));
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static string SerializeEntities(AiEntities e)
        {
            StringBuilder sb = new StringBuilder("{");
            bool any = false;
            any |= AppendField(sb, "personName", e.PersonName, any);
            any |= AppendField(sb, "tazkiraNo", e.TazkiraNo, any);
            any |= AppendField(sb, "tazkiraSuffix", e.TazkiraSuffix, any);
            any |= AppendField(sb, "province", e.Province, any);
            any |= AppendField(sb, "district", e.District, any);
            any |= AppendField(sb, "phone", e.Phone, any);
            any |= AppendField(sb, "caseReference", e.CaseReferenceRaw, any);
            sb.Append("}");
            return sb.ToString();
        }

        private static bool AppendField(StringBuilder sb, string name, string value, bool hadPrevious)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (hadPrevious) sb.Append(",");
            sb.Append("\"").Append(name).Append("\":\"").Append(value.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"");
            return true;
        }
    }
}

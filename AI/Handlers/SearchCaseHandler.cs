using System.Collections.Generic;
using System.Text;
using CaseManagement.DAL;

namespace CaseManagement.AI.Handlers
{
    // دستیار هوشمند — فاز ۱. جست‌وجوی پرونده با نام/تذکره/ولایت/ولسوالی/تلفن/
    // وضعیت، شمارش («چند یتیم داریم؟») و بازکردنِ مستقیم با شماره/کدِ پرونده.
    public class SearchCaseHandler : IAiIntentHandler
    {
        public AiResponse Handle(AiEntities entities, string rawQuery, double baseConfidence)
        {
            DatabaseHelper db = new DatabaseHelper();

            // اشاره‌ی صریح به یک پرونده («پرونده ۲۴۲۴») — حلِ مستقیم، طبق
            // فیکس‌ها §۳: Code → FormNo → (اگر عددی نبود) نام. هرگز CasID خام.
            if (!string.IsNullOrEmpty(entities.CaseReferenceRaw))
            {
                List<CaseRecord> refMatches = CaseReferenceResolver.Resolve(db, entities.CaseReferenceRaw);
                if (refMatches.Count > 1)
                {
                    return new AiResponse
                    {
                        Intent = AiIntent.SearchCase,
                        NeedsClarification = true,
                        ResponseText = refMatches.Count + " پرونده با این مشخصات یافت شد — کدام‌یک؟",
                        Results = ToResultItems(refMatches)
                    };
                }
                if (refMatches.Count == 1)
                {
                    return new AiResponse
                    {
                        Intent = AiIntent.SearchCase,
                        Confidence = System.Math.Min(1, baseConfidence + 0.15),
                        ResponseText = "پرونده یافت شد.",
                        Results = ToResultItems(refMatches)
                    };
                }
                return new AiResponse
                {
                    Intent = AiIntent.SearchCase,
                    NeedsClarification = true,
                    ResponseText = "پرونده‌ای با شماره/کد «" + entities.CaseReferenceRaw + "» در محدوده‌ی دسترسیِ شما یافت نشد."
                };
            }

            if (entities.IsCountQuery)
            {
                int count = CaseSearchCore.CountByEntities(db, entities);
                var countResponse = new AiResponse
                {
                    Intent = AiIntent.SearchCase,
                    ResponseText = FormatCountAnswer(count, entities)
                };
                if (baseConfidence < PersianNluEngine.LowConfidenceThreshold)
                    countResponse.ResponseText += " (اگر منظورِ دیگری داشتید، لطفاً دقیق‌تر بپرسید.)";
                return countResponse;
            }

            bool capHit;
            List<CaseRecord> results = CaseSearchCore.SearchByEntities(db, entities, out capHit);

            if (capHit && string.IsNullOrEmpty(entities.Province) && string.IsNullOrEmpty(entities.District))
            {
                return new AiResponse
                {
                    Intent = AiIntent.SearchCase,
                    NeedsClarification = true,
                    ResponseText = "بیش از ۲۰۰ نتیجه یافت شد — لطفاً ولایت یا شماره تذکره را هم بگویید."
                };
            }

            var response = new AiResponse
            {
                Intent = AiIntent.SearchCase,
                Results = ToResultItems(results),
                ResponseText = FormatListAnswer(results.Count)
            };

            if (baseConfidence < PersianNluEngine.LowConfidenceThreshold)
            {
                response.NeedsClarification = true;
                response.ResponseText = "دقیقاً متوجه نشدم. " +
                    (results.Count > 0 ? "این نتایج نزدیک‌ترین موارد هستند — " : "") +
                    "لطفاً نام، تذکره یا ولایت را دقیق‌تر بگویید.";
            }
            else if (baseConfidence < PersianNluEngine.HighConfidenceThreshold)
            {
                response.ResponseText += " آیا منظور همین بود؟";
            }

            return response;
        }

        private static string FormatCountAnswer(int count, AiEntities entities)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(count).Append(" یتیم");
            if (!string.IsNullOrEmpty(entities.ServiceStatus)) sb.Append(" ").Append(entities.ServiceStatus);
            if (!string.IsNullOrEmpty(entities.Province)) sb.Append(" در ولایت ").Append(entities.Province);
            if (!string.IsNullOrEmpty(entities.District)) sb.Append(" (").Append(entities.District).Append(")");
            sb.Append(" یافت شد.");
            return sb.ToString();
        }

        private static string FormatListAnswer(int count)
        {
            if (count == 0) return "نتیجه‌ای یافت نشد.";
            if (count == 1) return "۱ نتیجه یافت شد.";
            return count + " نتیجه یافت شد.";
        }

        private static List<AiResultItem> ToResultItems(List<CaseRecord> records)
        {
            var items = new List<AiResultItem>();
            foreach (CaseRecord r in records)
            {
                items.Add(new AiResultItem
                {
                    EntityType = "Case",
                    EntityId = r.CasID,
                    DisplayTitle = string.IsNullOrEmpty(r.HeadFullName) ? ("پرونده #" + r.CasID) : r.HeadFullName,
                    DisplaySubtitle = BuildSubtitle(r)
                });
            }
            return items;
        }

        private static string BuildSubtitle(CaseRecord r)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(r.HeadFatherName)) parts.Add("پدر: " + r.HeadFatherName);
            if (!string.IsNullOrEmpty(r.HeadTazkiraNo)) parts.Add("تذکره: " + r.HeadTazkiraNo);
            if (!string.IsNullOrEmpty(r.Province)) parts.Add(r.Province);
            if (!string.IsNullOrEmpty(r.District)) parts.Add(r.District);
            if (!string.IsNullOrEmpty(r.ServiceStatus)) parts.Add(r.ServiceStatus);
            return string.Join(" · ", parts);
        }
    }
}

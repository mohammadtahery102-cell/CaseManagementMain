using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.AI.Handlers
{
    // دستیار هوشمند — فاز ۱. «افرادی که در N روز اخیر سرویس نگرفته‌اند».
    // آخرین تاریخِ کمک هر پرونده از TblAssistance گرفته می‌شود؛ چون AssistanceDate
    // ممکن است میلادیِ ISO یا رشته‌ی شمسی ذخیره شده باشد (مثلِ بقیه‌ی تاریخ‌های
    // متنیِ این پروژه)، با PersianDateHelper.ParseStoredDate (که همین ابهام
    // را از قبل حل کرده) در سمتِ C# مقایسه می‌شود، نه با توابعِ تاریخِ SQLite.
    public class NoRecentServiceHandler : IAiIntentHandler
    {
        private const int DefaultDays = 90;

        public AiResponse Handle(AiEntities entities, string rawQuery, double baseConfidence)
        {
            int days = entities.NoRecentServiceDays.HasValue && entities.NoRecentServiceDays.Value > 0
                ? entities.NoRecentServiceDays.Value
                : DefaultDays;

            DatabaseHelper db = new DatabaseHelper();
            int centerFilter = SecurityContext.CenterFilterId;
            DateTime cutoff = DateTime.Now.AddDays(-days);

            var results = new List<AiResultItem>();

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT c.CasID, c.HeadFullName, c.HeadFatherName, c.Province, c.District,
       (SELECT MAX(a.AssistanceDate) FROM TblAssistance a WHERE a.CasID = c.CasID) AS LastServiceDate
FROM TblCase c
WHERE c.IsArchived = 0 AND c.ServiceStatus = 'فعال'
  AND (@CenterFilter = 0 OR c.CenterID = @CenterFilter);", con))
            {
                cmd.Parameters.AddWithValue("@CenterFilter", centerFilter);
                con.Open();
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        object lastRaw = reader["LastServiceDate"];
                        DateTime last = lastRaw == DBNull.Value
                            ? DateTime.MinValue
                            : PersianDateHelper.ParseStoredDate(lastRaw, DateTime.MinValue);

                        if (last < cutoff)
                        {
                            string full   = reader["HeadFullName"]   == DBNull.Value ? "" : reader["HeadFullName"].ToString();
                            string father = reader["HeadFatherName"] == DBNull.Value ? "" : reader["HeadFatherName"].ToString();
                            string prov   = reader["Province"]       == DBNull.Value ? "" : reader["Province"].ToString();
                            string dist   = reader["District"]       == DBNull.Value ? "" : reader["District"].ToString();
                            int casId     = Convert.ToInt32(reader["CasID"]);

                            var parts = new List<string>();
                            if (!string.IsNullOrEmpty(father)) parts.Add("پدر: " + father);
                            parts.Add(last == DateTime.MinValue ? "بدون سابقه‌ی کمک" : "آخرین کمک: " + PersianDateHelper.ToPersianDateString(last));
                            if (!string.IsNullOrEmpty(prov)) parts.Add(prov);
                            if (!string.IsNullOrEmpty(dist)) parts.Add(dist);

                            results.Add(new AiResultItem
                            {
                                EntityType = "Case",
                                EntityId = casId,
                                DisplayTitle = string.IsNullOrEmpty(full) ? ("پرونده #" + casId) : full,
                                DisplaySubtitle = string.Join(" · ", parts)
                            });
                        }
                    }
                }
            }

            var response = new AiResponse
            {
                Intent = AiIntent.NoRecentService,
                Results = results,
                ResponseText = results.Count == 0
                    ? "همه‌ی پرونده‌های فعال در " + days + " روز اخیر سرویس گرفته‌اند."
                    : results.Count + " پرونده در " + days + " روز اخیر سرویس نگرفته‌اند."
            };

            if (baseConfidence < PersianNluEngine.LowConfidenceThreshold)
                response.NeedsClarification = true;

            return response;
        }
    }
}

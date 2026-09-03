using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.AI.Handlers
{
    // دستیار هوشمند — فاز ۱. جست‌وجوی اعضای خانواده. طبق فیکس‌ها §۱/§۲:
    // TblFamily ستونِ CenterID ندارد، پس فیلترِ مرکز باید از طریقِ JOIN با
    // TblCase انجام شود؛ ستون‌های واقعی MemberName/MemberFatherName/
    // MemberTazkiraNo هستند (نه «Phone» که روی TblFamily وجود ندارد).
    // بدونِ FTS5 (خارج از محدوده‌ی فاز ۱ طبق مشخصات)، فقط LIKE ساختاریافته.
    public class SearchFamilyHandler : IAiIntentHandler
    {
        public AiResponse Handle(AiEntities entities, string rawQuery, double baseConfidence)
        {
            DatabaseHelper db = new DatabaseHelper();
            int centerFilter = SecurityContext.CenterFilterId;

            StringBuilder sql = new StringBuilder(@"
SELECT f.FamID, f.CasID, f.MemberName, f.MemberFatherName, f.MemberTazkiraNo,
       c.Province, c.District, c.ServiceStatus
FROM TblFamily f
JOIN TblCase c ON c.CasID = f.CasID
WHERE 1 = 1 AND c.IsArchived = 0");

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.Connection = con;

                if (centerFilter != 0)
                {
                    sql.Append(" AND c.CenterID = @CenterFilter");
                    cmd.Parameters.AddWithValue("@CenterFilter", centerFilter);
                }

                AddLikeFilter(sql, cmd, "f.MemberName", "@Name", entities.PersonName);
                AddLikeFilter(sql, cmd, "c.Province", "@Province", entities.Province);
                AddLikeFilter(sql, cmd, "c.District", "@District", entities.District);

                if (!string.IsNullOrWhiteSpace(entities.TazkiraSuffix))
                {
                    sql.Append(" AND f.MemberTazkiraNo LIKE @TazkiraSuffix");
                    cmd.Parameters.AddWithValue("@TazkiraSuffix", "%" + entities.TazkiraSuffix.Trim());
                }
                else if (!string.IsNullOrWhiteSpace(entities.TazkiraNo))
                {
                    sql.Append(" AND f.MemberTazkiraNo = @Tazkira");
                    cmd.Parameters.AddWithValue("@Tazkira", entities.TazkiraNo.Trim());
                }

                sql.Append(" ORDER BY f.MemberName LIMIT 201;");
                cmd.CommandText = sql.ToString();

                var results = new List<AiResultItem>();
                con.Open();
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int casId = Convert.ToInt32(reader["CasID"]);
                        string memberName = reader["MemberName"] == DBNull.Value ? "" : reader["MemberName"].ToString();
                        string fatherName = reader["MemberFatherName"] == DBNull.Value ? "" : reader["MemberFatherName"].ToString();
                        string tazkira    = reader["MemberTazkiraNo"] == DBNull.Value ? "" : reader["MemberTazkiraNo"].ToString();
                        string province   = reader["Province"] == DBNull.Value ? "" : reader["Province"].ToString();
                        string district   = reader["District"] == DBNull.Value ? "" : reader["District"].ToString();

                        var parts = new List<string>();
                        if (!string.IsNullOrEmpty(fatherName)) parts.Add("پدر: " + fatherName);
                        if (!string.IsNullOrEmpty(tazkira)) parts.Add("تذکره: " + tazkira);
                        if (!string.IsNullOrEmpty(province)) parts.Add(province);
                        if (!string.IsNullOrEmpty(district)) parts.Add(district);

                        results.Add(new AiResultItem
                        {
                            EntityType = "Case", // بازکردن همیشه از طریقِ FrmCase(CasID) است — FrmFamily مستقل نیست
                            EntityId = casId,
                            DisplayTitle = string.IsNullOrEmpty(memberName) ? ("پرونده #" + casId) : memberName,
                            DisplaySubtitle = string.Join(" · ", parts)
                        });
                    }
                }

                bool capHit = results.Count > 200;
                if (capHit) results.RemoveRange(200, results.Count - 200);

                var response = new AiResponse
                {
                    Intent = AiIntent.SearchFamily,
                    Results = results,
                    ResponseText = results.Count == 0 ? "نتیجه‌ای یافت نشد." :
                        (results.Count == 1 ? "۱ نتیجه یافت شد." : results.Count + " نتیجه یافت شد.")
                };

                if (baseConfidence < PersianNluEngine.LowConfidenceThreshold)
                {
                    response.NeedsClarification = true;
                    response.ResponseText = "لطفاً نامِ خانواده یا ولسوالی را دقیق‌تر بگویید.";
                }
                else if (baseConfidence < PersianNluEngine.HighConfidenceThreshold)
                {
                    response.ResponseText += " آیا منظور همین بود؟";
                }

                return response;
            }
        }

        private static void AddLikeFilter(StringBuilder sql, SQLiteCommand cmd, string column, string parameter, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            sql.Append(" AND ").Append(column).Append(" LIKE ").Append(parameter);
            cmd.Parameters.AddWithValue(parameter, "%" + value.Trim() + "%");
        }
    }
}

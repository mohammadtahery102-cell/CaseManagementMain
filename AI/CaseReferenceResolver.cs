using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.AI
{
    // دستیار هوشمند — فاز ۱. حلِ مرجعِ پرونده از متنِ خام کاربر.
    //
    // طبق AI_ASSISTANT_PHASE1_FIXES.md §3: CasID هرگز مستقیماً از متنِ کاربر
    // تفسیر نمی‌شود (کلید داخلیِ AUTOINCREMENT بدون معنای کاربری است). ترتیب:
    //   ۱) تطبیقِ دقیقِ Code
    //   ۲) اگر چیزی پیدا نشد، تطبیقِ دقیقِ FormNo
    //   ۳) اگر عددی نبود (مثل «کبیر»)، جست‌وجو بر پایه‌ی نام
    // همه در محدوده‌ی مرکزِ کاربر؛ تطبیق بیرون از مرکز به‌معنای «یافت نشد» است،
    // نه «دسترسی ندارید» — تا وجودِ رکورد در مرکزِ دیگر افشا نشود.
    public static class CaseReferenceResolver
    {
        public static List<CaseRecord> Resolve(DatabaseHelper db, string rawReference)
        {
            var results = new List<CaseRecord>();
            if (string.IsNullOrWhiteSpace(rawReference))
                return results;

            string token = PersianNormalizer.Normalize(rawReference).Trim();
            int centerFilter = SecurityContext.CenterFilterId;

            // ۱) Code — دقیق
            results.AddRange(QueryExact(db, "Code", token, centerFilter));
            if (results.Count > 0) return results;

            // ۲) FormNo — دقیق، فقط اگر توکن عددی باشد
            int formNo;
            if (int.TryParse(token, out formNo))
            {
                results.AddRange(QueryExact(db, "FormNo", formNo.ToString(), centerFilter));
                if (results.Count > 0) return results;
            }

            // ۳) نام — فقط اگر توکن عددیِ صرف نبود (یعنی چیزی شبیه «کبیر»)
            if (!IsPurelyNumeric(token))
            {
                var entities = new AiEntities { PersonName = token };
                bool capHit;
                var byName = CaseSearchCore.SearchByEntities(db, entities, out capHit);
                results.AddRange(byName);
            }

            return results;
        }

        private static bool IsPurelyNumeric(string s)
        {
            foreach (char c in s)
                if (c < '0' || c > '9') return false;
            return s.Length > 0;
        }

        private static List<CaseRecord> QueryExact(DatabaseHelper db, string column, string value, int centerFilter)
        {
            var list = new List<CaseRecord>();

            string sql = @"
SELECT CasID, Code, FormNo, HeadFullName, HeadFatherName, HeadTazkiraNo, Province, District, ServiceStatus, CenterID
FROM TblCase
WHERE IsArchived = 0 AND " + column + @" = @Value
  AND (@CenterFilter = 0 OR CenterID = @CenterFilter);";

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Value", value);
                cmd.Parameters.AddWithValue("@CenterFilter", centerFilter);
                con.Open();
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new CaseRecord
                        {
                            CasID          = Convert.ToInt32(reader["CasID"]),
                            Code           = reader["Code"] == DBNull.Value ? null : reader["Code"].ToString(),
                            FormNo         = reader["FormNo"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["FormNo"]),
                            HeadFullName   = reader["HeadFullName"] == DBNull.Value ? "" : reader["HeadFullName"].ToString(),
                            HeadFatherName = reader["HeadFatherName"] == DBNull.Value ? "" : reader["HeadFatherName"].ToString(),
                            HeadTazkiraNo  = reader["HeadTazkiraNo"] == DBNull.Value ? "" : reader["HeadTazkiraNo"].ToString(),
                            Province       = reader["Province"] == DBNull.Value ? "" : reader["Province"].ToString(),
                            District       = reader["District"] == DBNull.Value ? "" : reader["District"].ToString(),
                            ServiceStatus  = reader["ServiceStatus"] == DBNull.Value ? "" : reader["ServiceStatus"].ToString(),
                            CenterID       = reader["CenterID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CenterID"])
                        });
                    }
                }
            }

            return list;
        }
    }
}

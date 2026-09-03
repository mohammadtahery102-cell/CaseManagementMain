using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.AI
{
    public class CaseRecord
    {
        public int    CasID;
        public string Code;
        public int?   FormNo;
        public string HeadFullName;
        public string HeadFatherName;
        public string HeadTazkiraNo;
        public string Province;
        public string District;
        public string ServiceStatus;
        public int    CenterID;
    }

    // دستیار هوشمند — فاز ۱. هسته‌ی مشترکِ جست‌وجوی پرونده — هم برای
    // SearchCaseHandler و هم برای CaseReferenceResolver (پیوند یادآوری با نام)
    // استفاده می‌شود تا منطقِ فیلترِ مرکز/نمایه یک‌بار نوشته شود.
    //
    // طبق AI_ASSISTANT_PHASE1_FIXES.md §2: مرحله‌ی اول (FTS5) از همان ابتدا
    // با CenterID فیلتر می‌شود — سقفِ ۲۰۰ ردیف فقط در محدوده‌ی مرکزِ کاربر
    // مصرف می‌شود، نه در کل دیتابیس.
    public static class CaseSearchCore
    {
        public const int MaxResults = 200;

        public static List<CaseRecord> SearchByEntities(DatabaseHelper db, AiEntities entities, out bool capHit)
        {
            capHit = false;
            int centerFilter = SecurityContext.CenterFilterId;
            List<int> candidateIds = null;

            if (!string.IsNullOrWhiteSpace(entities.PersonName) && AiInitializer.FtsAvailable)
            {
                candidateIds = RunFtsStage(db, entities.PersonName, centerFilter, out capHit);
                if (candidateIds.Count == 0)
                    return new List<CaseRecord>();
            }

            List<CaseRecord> results = RunStructuredStage(db, entities, centerFilter, candidateIds);

            // آموزش — رفعِ MED-01 (اعتبارسنجیِ نهایی): پرچمِ capHit قبلاً فقط
            // داخلِ RunFtsStage تنظیم می‌شد؛ در مسیرِ LIKE-only (وقتی FTS5 در
            // دسترس نیست — که طبق اعتبارسنجیِ نهایی مسیرِ واقعاً فعال است)
            // هرگز true نمی‌شد، پس پیامِ «لطفاً ولایت/تذکره را هم بگویید» هرگز
            // نشان داده نمی‌شد و تا ۲۰۱ نتیجه‌ی خام بدونِ هشدار نمایش داده
            // می‌شد. این بررسی، بدونِ توجه به این‌که کدام مرحله نتایج را
            // برگردانده، درست کار می‌کند — اگر FTS5 فعال بود، candidateIds
            // از قبل به ۲۰۰ محدود شده، پس این شرط هرگز اینجا فعال نمی‌شود.
            if (results.Count > MaxResults)
            {
                capHit = true;
                results.RemoveRange(MaxResults, results.Count - MaxResults);
            }

            return results;
        }

        // ─── مرحله ۱: پیش‌فیلترِ نام با FTS5 (فقط در محدوده‌ی مرکز کاربر) ──────
        private static List<int> RunFtsStage(DatabaseHelper db, string personName, int centerFilter, out bool capHit)
        {
            List<int> ids = new List<int>();
            string matchExpr = BuildFtsMatchExpression(personName);
            capHit = false;
            if (string.IsNullOrEmpty(matchExpr))
                return ids;

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT CasID FROM AiCaseSearchIndex
WHERE AiCaseSearchIndex MATCH @Match
  AND (@CenterFilter = 0 OR CenterID = @CenterFilter)
LIMIT " + (MaxResults + 1) + ";", con))
            {
                cmd.Parameters.AddWithValue("@Match", matchExpr);
                cmd.Parameters.AddWithValue("@CenterFilter", centerFilter);
                con.Open();
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        ids.Add(Convert.ToInt32(reader["CasID"]));
                }
            }

            if (ids.Count > MaxResults)
            {
                capHit = true;
                ids.RemoveRange(MaxResults, ids.Count - MaxResults);
            }

            return ids;
        }

        private static string BuildFtsMatchExpression(string personName)
        {
            string normalized = PersianNormalizer.Normalize(personName);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            string[] tokens = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder();
            foreach (string token in tokens)
            {
                if (sb.Length > 0) sb.Append(' ');
                // فرار از نقل‌قول برای جلوگیری از خطای نحویِ FTS5 (نه SQL — این
                // مقدار به‌صورت پارامترِ مقیدشده به SQLite فرستاده می‌شود، نه
                // به‌صورت رشته‌ی خام SQL).
                string escaped = token.Replace("\"", "\"\"");
                sb.Append('"').Append(escaped).Append("\"*");
            }
            return sb.ToString();
        }

        // ─── مرحله ۲: فیلترِ ساختاریافته روی TblCase ────────────────────────
        private static List<CaseRecord> RunStructuredStage(DatabaseHelper db, AiEntities entities, int centerFilter, List<int> candidateIds)
        {
            var results = new List<CaseRecord>();

            StringBuilder sql = new StringBuilder(@"
SELECT CasID, Code, FormNo, HeadFullName, HeadFatherName, HeadTazkiraNo, Province, District, ServiceStatus, CenterID
FROM TblCase
WHERE 1 = 1 AND IsArchived = 0");

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.Connection = con;

                if (candidateIds != null)
                {
                    if (candidateIds.Count == 0)
                        return results;

                    string[] placeholders = new string[candidateIds.Count];
                    for (int i = 0; i < candidateIds.Count; i++)
                    {
                        string p = "@cid" + i;
                        placeholders[i] = p;
                        cmd.Parameters.AddWithValue(p, candidateIds[i]);
                    }
                    sql.Append(" AND CasID IN (").Append(string.Join(",", placeholders)).Append(")");
                }
                else if (!string.IsNullOrWhiteSpace(entities.PersonName))
                {
                    // آموزش — رفعِ باگِ بحرانیِ کشف‌شده در اعتبارسنجیِ نهایی: وقتی
                    // FTS5 در دسترس نیست (AiInitializer.FtsAvailable=false)،
                    // RunFtsStage اصلاً اجرا نمی‌شود و candidateIds همیشه null
                    // می‌ماند — بدونِ این شاخه، جست‌وجوی نام‌محور (مثلاً «یتیم به
                    // نام کبیر») هیچ فیلترِ نامی اعمال نمی‌کرد و همه‌ی پرونده‌های
                    // مرکز را برمی‌گرداند. این همان مسیرِ LIKE-only ای است که
                    // AI_ASSISTANT_PHASE1_FIXES.md §4 از ابتدا فرض کرده بود.
                    sql.Append(" AND (HeadFullName LIKE @PersonName OR HeadFatherName LIKE @PersonName)");
                    cmd.Parameters.AddWithValue("@PersonName", "%" + entities.PersonName.Trim() + "%");
                }

                AddLikeFilter(sql, cmd, "Province", "@Province", entities.Province);
                AddLikeFilter(sql, cmd, "District", "@District", entities.District);
                AddLikeFilter(sql, cmd, "Phone",    "@Phone",    entities.Phone);

                if (!string.IsNullOrWhiteSpace(entities.TazkiraSuffix))
                {
                    sql.Append(" AND HeadTazkiraNo LIKE @TazkiraSuffix");
                    cmd.Parameters.AddWithValue("@TazkiraSuffix", "%" + entities.TazkiraSuffix.Trim());
                }
                else if (!string.IsNullOrWhiteSpace(entities.TazkiraNo))
                {
                    AddExactFilter(sql, cmd, "HeadTazkiraNo", "@Tazkira", entities.TazkiraNo);
                }

                if (!string.IsNullOrWhiteSpace(entities.ServiceStatus))
                    AddServiceStatusFilter(sql, cmd, entities.ServiceStatus);

                if (centerFilter != 0)
                {
                    sql.Append(" AND CenterID = @CenterFilter");
                    cmd.Parameters.AddWithValue("@CenterFilter", centerFilter);
                }

                sql.Append(" ORDER BY HeadFullName LIMIT ").Append(MaxResults + 1).Append(";");
                cmd.CommandText = sql.ToString();

                con.Open();
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new CaseRecord
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

            return results;
        }

        public static int CountByEntities(DatabaseHelper db, AiEntities entities)
        {
            bool capHit;
            // برای شمارش، سقفِ نمایشی معنا ندارد؛ مستقیم COUNT روی همان فیلترها.
            int centerFilter = SecurityContext.CenterFilterId;
            List<int> candidateIds = null;
            if (!string.IsNullOrWhiteSpace(entities.PersonName) && AiInitializer.FtsAvailable)
                candidateIds = RunFtsStage(db, entities.PersonName, centerFilter, out capHit);

            StringBuilder sql = new StringBuilder("SELECT COUNT(*) FROM TblCase WHERE 1 = 1 AND IsArchived = 0");

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.Connection = con;

                if (candidateIds != null)
                {
                    if (candidateIds.Count == 0) return 0;
                    string[] placeholders = new string[candidateIds.Count];
                    for (int i = 0; i < candidateIds.Count; i++)
                    {
                        string p = "@cid" + i;
                        placeholders[i] = p;
                        cmd.Parameters.AddWithValue(p, candidateIds[i]);
                    }
                    sql.Append(" AND CasID IN (").Append(string.Join(",", placeholders)).Append(")");
                }
                else if (!string.IsNullOrWhiteSpace(entities.PersonName))
                {
                    sql.Append(" AND (HeadFullName LIKE @PersonName OR HeadFatherName LIKE @PersonName)");
                    cmd.Parameters.AddWithValue("@PersonName", "%" + entities.PersonName.Trim() + "%");
                }

                AddLikeFilter(sql, cmd, "Province", "@Province", entities.Province);
                AddLikeFilter(sql, cmd, "District", "@District", entities.District);

                if (!string.IsNullOrWhiteSpace(entities.ServiceStatus))
                    AddServiceStatusFilter(sql, cmd, entities.ServiceStatus);

                if (centerFilter != 0)
                {
                    sql.Append(" AND CenterID = @CenterFilter");
                    cmd.Parameters.AddWithValue("@CenterFilter", centerFilter);
                }

                cmd.CommandText = sql.ToString();
                con.Open();
                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        private static void AddLikeFilter(StringBuilder sql, SQLiteCommand cmd, string column, string parameter, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            sql.Append(" AND ").Append(column).Append(" LIKE ").Append(parameter);
            cmd.Parameters.AddWithValue(parameter, "%" + value.Trim() + "%");
        }

        private static void AddExactFilter(StringBuilder sql, SQLiteCommand cmd, string column, string parameter, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            sql.Append(" AND ").Append(column).Append(" = ").Append(parameter);
            cmd.Parameters.AddWithValue(parameter, value.Trim());
        }

        // آموزش — رفعِ HIGH-01 (اعتبارسنجیِ نهایی): PersianNluEngine اکنون
        // برای «قطع/متوقف/غیرفعال» مقدارِ معتبرِ «قطع» را در entities.ServiceStatus
        // می‌گذارد (نه مقدارِ نامعتبرِ سابقِ «غیرفعال» که هرگز در دیتابیس
        // وجود نداشت و همیشه صفر نتیجه می‌داد). چون «قطع» و «قطع موقت» هر دو
        // یعنی «دیگر فعال نیست» (نگاه کنید CaseDomain.TerminatedStatuses)،
        // این متد آن‌ها را با IN می‌گسترد تا هر دو حالتِ قطعِ خدمات پوشش داده
        // شود؛ برای «فعال» رفتار دقیقاً مثلِ قبل (تطبیقِ دقیق) باقی می‌ماند.
        private static void AddServiceStatusFilter(StringBuilder sql, SQLiteCommand cmd, string status)
        {
            if (string.Equals(status, "قطع", StringComparison.Ordinal))
            {
                string[] terminated = CaseDomain.TerminatedStatuses;
                string[] placeholders = new string[terminated.Length];
                for (int i = 0; i < terminated.Length; i++)
                {
                    string p = "@StatusIn" + i;
                    placeholders[i] = p;
                    cmd.Parameters.AddWithValue(p, terminated[i]);
                }
                sql.Append(" AND ServiceStatus IN (").Append(string.Join(",", placeholders)).Append(")");
                return;
            }

            AddExactFilter(sql, cmd, "ServiceStatus", "@Status", status);
        }
    }
}

using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;

namespace CaseManagement.Helpers
{
    // حقایقِ یک پرونده که شرط‌ها روی آن‌ها ارزیابی می‌شوند. یک‌بار خوانده
    // می‌شود و برای همهٔ قواعد استفاده می‌گردد (قاعدهٔ «هیچ کوئری در حلقه»).
    public class CaseFacts
    {
        public int    CasID;
        public string RequestTypeCode = "";
        public string ServiceStatusCode = "";
        public string DisabilityDegree = "";
        public string DisabilityType = "";
        public string MaritalStatus = "";
        public string SadatStatus = "";
        public int    ChildrenCount;
        public int    FamilyMemberCount;

        // Phase 5.5-B — حقایقِ افزوده برای موتورِ امتیازِ آسیب‌پذیری.
        // همگی از دادهٔ *موجود* خوانده می‌شوند؛ هیچ فیلدِ کسب‌وکاریِ تازه‌ای
        // برای این‌ها ساخته نشده (خواستهٔ صریحِ فاز).
        public int    HeadAge;             // از TblCase.HeadBirthDate
        public bool   HasBreadwinner;      // از TblCase.Job
        public string CompletionStatus = "";
        public int    CompletionPercent;
        public bool   HasOrphanRecord;
        public bool   HasDisabilityRecord;
        public bool   HasMigrantRecord;
        public string FatherStatus = "";
        public string MotherStatus = "";
        public bool   IsStudent;
        public string MigrationCardType = "";

        public string Get(string fieldName)
        {
            switch ((fieldName ?? "").Trim())
            {
                case FactRequestType:       return RequestTypeCode;
                case FactServiceStatus:     return ServiceStatusCode;
                case FactDisabilityDegree:  return DisabilityDegree;
                case FactDisabilityType:    return DisabilityType;
                case FactMaritalStatus:     return MaritalStatus;
                case FactSadatStatus:       return SadatStatus;
                case FactChildrenCount:     return ChildrenCount.ToString(CultureInfo.InvariantCulture);
                case FactFamilyMemberCount: return FamilyMemberCount.ToString(CultureInfo.InvariantCulture);

                // Phase 5.5-B
                case FactHeadAge:             return HeadAge.ToString(CultureInfo.InvariantCulture);
                case FactHasBreadwinner:      return HasBreadwinner ? "1" : "0";
                case FactCompletionStatus:    return CompletionStatus;
                case FactCompletionPercent:   return CompletionPercent.ToString(CultureInfo.InvariantCulture);
                case FactHasOrphanRecord:     return HasOrphanRecord ? "1" : "0";
                case FactHasDisabilityRecord: return HasDisabilityRecord ? "1" : "0";
                case FactHasMigrantRecord:    return HasMigrantRecord ? "1" : "0";
                case FactFatherStatus:        return FatherStatus;
                case FactMotherStatus:        return MotherStatus;
                case FactIsStudent:           return IsStudent ? "1" : "0";
                case FactMigrationCardType:   return MigrationCardType;

                // نامِ ناشناخته ⇒ شرط تطبیق نمی‌کند.
                // این همان چیزی است که «معیارِ جای‌نگهدارِ آینده» را ممکن
                // می‌کند: معیاری که به حقیقتی هنوز‌موجودنشده اشاره دارد
                // (HeadGender / HousingCondition) بی‌صدا صفر می‌ماند تا روزی
                // که آن حقیقت اینجا اضافه شود — بدونِ تغییرِ موتور.
                default: return null;
            }
        }

        // ─── نام‌های مجازِ فیلد در TblAssistanceRuleCondition.FieldName ───────
        // فهرستِ بسته: هر نامِ دیگری در ارزیابی «تطبیق نکرد» می‌شود، پس یک
        // غلطِ املایی در پیکربندی هرگز به مبلغِ اشتباه منجر نمی‌شود.
        public const string FactRequestType       = "RequestType";
        public const string FactServiceStatus     = "ServiceStatus";
        public const string FactDisabilityDegree  = "DisabilityDegree";
        public const string FactDisabilityType    = "DisabilityType";
        public const string FactMaritalStatus     = "MaritalStatus";
        public const string FactSadatStatus       = "SadatStatus";
        public const string FactChildrenCount     = "ChildrenCount";
        public const string FactFamilyMemberCount = "FamilyMemberCount";

        // Phase 5.5-B
        public const string FactHeadAge             = "HeadAge";
        public const string FactHasBreadwinner      = "HasBreadwinner";
        public const string FactCompletionStatus    = "CompletionStatus";
        public const string FactCompletionPercent   = "CompletionPercent";
        public const string FactHasOrphanRecord     = "HasOrphanRecord";
        public const string FactHasDisabilityRecord = "HasDisabilityRecord";
        public const string FactHasMigrantRecord    = "HasMigrantRecord";
        public const string FactFatherStatus        = "FatherStatus";
        public const string FactMotherStatus        = "MotherStatus";
        public const string FactIsStudent           = "IsStudent";
        public const string FactMigrationCardType   = "MigrationCardType";

        public static string[] AllFactNames
        {
            get
            {
                return new[]
                {
                    FactRequestType, FactServiceStatus, FactDisabilityDegree, FactDisabilityType,
                    FactMaritalStatus, FactSadatStatus, FactChildrenCount, FactFamilyMemberCount,
                    FactHeadAge, FactHasBreadwinner, FactCompletionStatus, FactCompletionPercent,
                    FactHasOrphanRecord, FactHasDisabilityRecord, FactHasMigrantRecord,
                    FactFatherStatus, FactMotherStatus, FactIsStudent, FactMigrationCardType
                };
            }
        }
    }

    public class AssistanceRuleMatch
    {
        public int     RuleID;
        public string  RuleName;
        public int     Priority;
        public decimal Amount;
        public List<string> ConditionExplanations = new List<string>();
    }

    public class AssistanceRecommendation
    {
        public int     CasID;
        public bool    HasMatch;
        public decimal RecommendedAmount;
        public AssistanceRuleMatch WinningRule;
        public List<AssistanceRuleMatch> MatchedRules = new List<AssistanceRuleMatch>();
        public List<string> Explanation = new List<string>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 5 — موتورِ «مبلغِ پیشنهادی» مساعدت.
    //
    // این کلاس *فقط پیشنهاد می‌دهد*: هیچ پرداختی نمی‌سازد، هیچ رکوردی در
    // TblAssistance نمی‌نویسد، و هیچ‌چیزی را مسدود نمی‌کند — خواستهٔ صریحِ
    // این فاز.
    //
    // نسبتش با Enterprise/RuleEngine: آن موتور برای «هشدار/جلوگیری/وظیفه»
    // است و هیچ حسابِ مبلغی ندارد؛ این یکی فقط مبلغ حساب می‌کند. عمداً دو
    // جدول و دو کلاسِ جدا، نه یکی با دو مسئولیت.
    //
    // واژگانِ عملگر از RuleEngine وام گرفته شده (=، <>، >، <) تا کاربرِ
    // مدیرِ سیستم دو دستورِ متفاوت یاد نگیرد؛ >= و <= اضافه شدند چون نمونهٔ
    // مصوب («تعداد فرزند >= ۲») به آن‌ها نیاز دارد.
    // ═══════════════════════════════════════════════════════════════════════
    public static class AssistanceRuleService
    {
        public const string OpEquals       = "=";
        public const string OpNotEquals    = "<>";
        public const string OpGreater      = ">";
        public const string OpLess         = "<";
        public const string OpGreaterEqual = ">=";
        public const string OpLessEqual    = "<=";
        public const string OpContains     = "شامل";

        public static string[] AllOperators
        {
            get { return new[] { OpEquals, OpNotEquals, OpGreater, OpGreaterEqual, OpLess, OpLessEqual, OpContains }; }
        }

        // ─── استخراجِ حقایقِ پرونده ───────────────────────────────────────────
        public static CaseFacts LoadFacts(int casId)
        {
            var facts = new CaseFacts { CasID = casId };
            if (casId <= 0) return facts;

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                using (var cmd = new SQLiteCommand(@"
SELECT IFNULL(rt.Code, '')        AS RequestTypeCode,
       IFNULL(ss.Code, '')        AS ServiceStatusCode,
       IFNULL(c.DisabilityDegree, '') AS DisabilityDegree,
       IFNULL(c.DisabilityType, '')   AS DisabilityType,
       IFNULL(c.MaritalStatus, '')    AS MaritalStatus,
       IFNULL(c.HeadSadat, '')        AS SadatStatus
FROM TblCase c
LEFT JOIN TblRequestType   rt ON rt.RequestTypeID   = c.RequestTypeID
LEFT JOIN TblServiceStatus ss ON ss.ServiceStatusID = c.ServiceStatusID
WHERE c.CasID = @CasID LIMIT 1;", con))
                {
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            facts.RequestTypeCode = dr["RequestTypeCode"].ToString();
                            facts.ServiceStatusCode = dr["ServiceStatusCode"].ToString();
                            facts.DisabilityDegree = dr["DisabilityDegree"].ToString();
                            facts.DisabilityType = dr["DisabilityType"].ToString();
                            facts.MaritalStatus = dr["MaritalStatus"].ToString();
                            facts.SadatStatus = dr["SadatStatus"].ToString();
                        }
                    }
                }

                // آموزش — دو شمارندهٔ متفاوت، عمداً هر دو در دسترس:
                //   ChildrenCount      = اعضایی که نقششان «فرزند» یا «یتیم» است
                //   FamilyMemberCount  = همهٔ اعضای خانواده
                // نمونهٔ مصوب («تعداد فرزند >= ۲») به اولی نیاز دارد؛ دومی برای
                // قواعدی است که اندازهٔ خانوار ملاکشان است.
                using (var cmd = new SQLiteCommand(@"
SELECT
  (SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = @CasID
     AND IFNULL(f.MemberRole, '') IN ('فرزند', 'یتیم')) AS ChildrenCount,
  (SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = @CasID) AS FamilyMemberCount;", con))
                {
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            facts.ChildrenCount = Convert.ToInt32(dr["ChildrenCount"]);
                            facts.FamilyMemberCount = Convert.ToInt32(dr["FamilyMemberCount"]);
                        }
                    }
                }

                // Phase 5.5-B — حقایقِ افزوده. یک کوئریِ واحد برای همه
                // (قاعدهٔ «هیچ کوئری در حلقه»؛ ارزیابیِ ده‌ها قاعده نباید
                // ده‌ها رفت‌وبرگشت به دیتابیس بسازد).
                using (var cmd = new SQLiteCommand(@"
SELECT
  CASE WHEN IFNULL(c.HeadBirthDate,'') = '' THEN 0
       ELSE CAST((julianday('now') - julianday(c.HeadBirthDate)) / 365.25 AS INTEGER) END AS HeadAge,
  CASE WHEN IFNULL(c.Job,'') = '' THEN 0 ELSE 1 END               AS HasBreadwinner,
  IFNULL(c.CompletionStatusCode, '')                              AS CompletionStatus,
  IFNULL(c.CompletionPercent, 0)                                  AS CompletionPercent,
  (SELECT COUNT(*) FROM TblOrphan     o WHERE o.CasID = c.CasID)  AS OrphanRows,
  (SELECT COUNT(*) FROM TblDisability d WHERE d.CasID = c.CasID)  AS DisabilityRows,
  (SELECT COUNT(*) FROM TblMigrant    m WHERE m.CasID = c.CasID)  AS MigrantRows,
  IFNULL((SELECT o.FatherStatus FROM TblOrphan o WHERE o.CasID = c.CasID LIMIT 1), '') AS FatherStatus,
  IFNULL((SELECT o.MotherStatus FROM TblOrphan o WHERE o.CasID = c.CasID LIMIT 1), '') AS MotherStatus,
  IFNULL((SELECT o.IsStudent    FROM TblOrphan o WHERE o.CasID = c.CasID LIMIT 1), 0)  AS IsStudent,
  IFNULL((SELECT m.MigrationCardType FROM TblMigrant m WHERE m.CasID = c.CasID LIMIT 1), '') AS MigrationCardType
FROM TblCase c WHERE c.CasID = @CasID LIMIT 1;", con))
                {
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            facts.HeadAge = Convert.ToInt32(dr["HeadAge"]);
                            facts.HasBreadwinner = Convert.ToInt32(dr["HasBreadwinner"]) != 0;
                            facts.CompletionStatus = dr["CompletionStatus"].ToString();
                            facts.CompletionPercent = Convert.ToInt32(dr["CompletionPercent"]);
                            facts.HasOrphanRecord = Convert.ToInt32(dr["OrphanRows"]) > 0;
                            facts.HasDisabilityRecord = Convert.ToInt32(dr["DisabilityRows"]) > 0;
                            facts.HasMigrantRecord = Convert.ToInt32(dr["MigrantRows"]) > 0;
                            facts.FatherStatus = dr["FatherStatus"].ToString();
                            facts.MotherStatus = dr["MotherStatus"].ToString();
                            facts.IsStudent = Convert.ToInt32(dr["IsStudent"]) != 0;
                            facts.MigrationCardType = dr["MigrationCardType"].ToString();
                        }
                    }
                }
            }

            return facts;
        }

        // ─── ارزیابی ────────────────────────────────────────────────────────
        // قاعده وقتی «تطبیق» می‌کند که *همهٔ* شرط‌هایش درست باشند (AND).
        // قاعدهٔ بدونِ شرط عمداً تطبیق نمی‌کند — وگرنه یک ردیفِ ناقصِ
        // پیکربندی به همهٔ پرونده‌ها مبلغ می‌داد.
        public static AssistanceRecommendation Evaluate(int casId)
        {
            return Evaluate(LoadFacts(casId));
        }

        public static AssistanceRecommendation Evaluate(CaseFacts facts)
        {
            var recommendation = new AssistanceRecommendation { CasID = facts.CasID };

            var rules = LoadActiveRules();
            if (rules.Count == 0)
            {
                recommendation.Explanation.Add("هیچ قاعدهٔ فعالی تعریف نشده است.");
                return recommendation;
            }

            foreach (var rule in rules)
            {
                var conditions = rule.Value;
                var match = rule.Key;

                if (conditions.Count == 0)
                {
                    recommendation.Explanation.Add(
                        "قاعدهٔ «" + match.RuleName + "» شرطی ندارد و نادیده گرفته شد.");
                    continue;
                }

                bool allMatched = true;
                var explanations = new List<string>();

                foreach (var condition in conditions)
                {
                    string actual = facts.Get(condition.FieldName);
                    bool ok = actual != null && Compare(actual, condition.Operator, condition.Value);

                    explanations.Add(string.Format("{0} {1} {2} ⇒ مقدار فعلی: {3} ⇒ {4}",
                        condition.FieldName, condition.Operator, condition.Value,
                        actual == null ? "(فیلد ناشناخته)" : (actual.Length == 0 ? "(خالی)" : actual),
                        ok ? "درست" : "نادرست"));

                    if (!ok) { allMatched = false; break; }
                }

                if (!allMatched)
                {
                    recommendation.Explanation.Add("قاعدهٔ «" + match.RuleName + "» تطبیق نکرد.");
                    continue;
                }

                match.ConditionExplanations = explanations;
                recommendation.MatchedRules.Add(match);
                recommendation.Explanation.Add(string.Format(
                    "قاعدهٔ «{0}» تطبیق کرد (اولویت {1}، مبلغ {2}).",
                    match.RuleName, match.Priority, match.Amount));
            }

            if (recommendation.MatchedRules.Count == 0)
            {
                recommendation.Explanation.Add("هیچ قاعده‌ای با این پرونده تطبیق نکرد؛ مبلغِ پیشنهادی صفر است.");
                return recommendation;
            }

            // برنده = کمترین Priority (عددِ کوچک‌تر = مهم‌تر، همان قراردادِ
            // EntRule)، و در تساوی، قاعده‌ای که زودتر تعریف شده (RuleID کمتر).
            AssistanceRuleMatch winner = recommendation.MatchedRules[0];
            foreach (var candidate in recommendation.MatchedRules)
            {
                if (candidate.Priority < winner.Priority ||
                    (candidate.Priority == winner.Priority && candidate.RuleID < winner.RuleID))
                    winner = candidate;
            }

            recommendation.HasMatch = true;
            recommendation.WinningRule = winner;
            recommendation.RecommendedAmount = winner.Amount;
            recommendation.Explanation.Add(string.Format(
                "قاعدهٔ برنده: «{0}» — مبلغِ پیشنهادی {1}.", winner.RuleName, winner.Amount));

            return recommendation;
        }

        // ارزیابی + ثبت در تایم‌لاین. جدا از Evaluate نگه داشته شده چون
        // Evaluate باید بی‌عارضه (side-effect free) بماند تا بتوان آن را در
        // پیش‌نمایش/گزارش هم بدونِ آلوده‌کردنِ تایم‌لاین صدا زد.
        public static AssistanceRecommendation EvaluateAndLog(int casId)
        {
            AssistanceRecommendation recommendation = Evaluate(casId);

            if (recommendation.HasMatch && recommendation.WinningRule != null)
            {
                TimelineService.LogAssistanceRuleMatched(casId,
                    recommendation.WinningRule.RuleID,
                    recommendation.WinningRule.RuleName,
                    recommendation.RecommendedAmount);
            }

            return recommendation;
        }

        private class RuleCondition
        {
            public string FieldName;
            public string Operator;
            public string Value;
        }

        private static List<KeyValuePair<AssistanceRuleMatch, List<RuleCondition>>> LoadActiveRules()
        {
            var rules = new List<KeyValuePair<AssistanceRuleMatch, List<RuleCondition>>>();
            var byId = new Dictionary<int, List<RuleCondition>>();

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                using (var cmd = new SQLiteCommand(
                    "SELECT RuleID, Name, Priority, Amount FROM TblAssistanceRule WHERE IsActive = 1 ORDER BY Priority, RuleID;", con))
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        int ruleId = Convert.ToInt32(dr["RuleID"]);
                        var conditions = new List<RuleCondition>();
                        byId[ruleId] = conditions;

                        rules.Add(new KeyValuePair<AssistanceRuleMatch, List<RuleCondition>>(
                            new AssistanceRuleMatch
                            {
                                RuleID = ruleId,
                                RuleName = dr["Name"].ToString(),
                                Priority = Convert.ToInt32(dr["Priority"]),
                                Amount = Convert.ToDecimal(dr["Amount"])
                            },
                            conditions));
                    }
                }

                // همهٔ شرط‌ها با یک کوئری (نه یکی به‌ازای هر قاعده).
                using (var cmd = new SQLiteCommand(@"
SELECT c.RuleID, c.FieldName, c.Operator, c.Value
FROM TblAssistanceRuleCondition c
JOIN TblAssistanceRule r ON r.RuleID = c.RuleID AND r.IsActive = 1
ORDER BY c.RuleID, c.ConditionID;", con))
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        int ruleId = Convert.ToInt32(dr["RuleID"]);
                        if (!byId.ContainsKey(ruleId)) continue;

                        byId[ruleId].Add(new RuleCondition
                        {
                            FieldName = dr["FieldName"].ToString(),
                            Operator = dr["Operator"].ToString(),
                            Value = dr["Value"] == DBNull.Value ? "" : dr["Value"].ToString()
                        });
                    }
                }
            }

            return rules;
        }

        // مقایسه: اگر هر دو طرف عددی باشند عددی مقایسه می‌شود، وگرنه متنی.
        // این تنها جایی است که معنیِ عملگرها تعریف شده.
        //
        // Phase 5.5-B — از private به public تغییر کرد تا موتورِ امتیازِ
        // آسیب‌پذیری هم *همین* معنا را به‌کار ببرد. اگر هرکدام نسخهٔ خودش را
        // می‌داشت، دو موتور می‌توانستند ">=" را متفاوت تفسیر کنند — دقیقاً آن
        // نوع اختلافِ خاموشی که بعداً کشفش گران تمام می‌شود.
        public static bool Compare(string actual, string op, string expected)
        {
            actual = actual ?? "";
            expected = expected ?? "";

            // هر دو TryParse جداگانه اجرا می‌شوند (نه با && که کوتاه‌مدار است
            // و متغیرِ دوم را بدونِ مقدار می‌گذارد).
            decimal actualNumber;
            decimal expectedNumber;
            bool actualIsNumber = decimal.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out actualNumber);
            bool expectedIsNumber = decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out expectedNumber);
            bool numeric = actualIsNumber && expectedIsNumber;

            switch ((op ?? "").Trim())
            {
                case OpEquals:
                    return numeric ? actualNumber == expectedNumber
                                   : string.Equals(actual, expected, StringComparison.Ordinal);
                case OpNotEquals:
                    return numeric ? actualNumber != expectedNumber
                                   : !string.Equals(actual, expected, StringComparison.Ordinal);
                case OpGreater:      return numeric && actualNumber >  expectedNumber;
                case OpGreaterEqual: return numeric && actualNumber >= expectedNumber;
                case OpLess:         return numeric && actualNumber <  expectedNumber;
                case OpLessEqual:    return numeric && actualNumber <= expectedNumber;
                case OpContains:     return actual.IndexOf(expected, StringComparison.Ordinal) >= 0;
                default:             return false;   // عملگرِ ناشناخته ⇒ تطبیق نمی‌کند
            }
        }

        // ─── فهرست‌های کشویی برای صفحهٔ مدیریت ──────────────────────────────
        // هم‌الگوی RuleEngine.OperatorItems(): جفتِ (مقدارِ ذخیره‌شده، متنِ
        // نمایشی). در سرویس است نه در فرم، تا افزودنِ حقیقتِ تازه به CaseFacts
        // فقط یک‌جا تغییر لازم داشته باشد.
        public static List<KeyValuePair<string, string>> OperatorItems()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(OpEquals,       "برابر است با"),
                new KeyValuePair<string, string>(OpNotEquals,    "برابر نیست با"),
                new KeyValuePair<string, string>(OpGreater,      "بزرگ‌تر از"),
                new KeyValuePair<string, string>(OpGreaterEqual, "بزرگ‌تر یا برابر"),
                new KeyValuePair<string, string>(OpLess,         "کوچک‌تر از"),
                new KeyValuePair<string, string>(OpLessEqual,    "کوچک‌تر یا برابر"),
                new KeyValuePair<string, string>(OpContains,     "شامل باشد")
            };
        }

        public static List<KeyValuePair<string, string>> FactItems()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(CaseFacts.FactRequestType,       "نوع درخواست"),
                new KeyValuePair<string, string>(CaseFacts.FactServiceStatus,     "وضعیت خدمات"),
                new KeyValuePair<string, string>(CaseFacts.FactDisabilityDegree,  "درجه معلولیت"),
                new KeyValuePair<string, string>(CaseFacts.FactDisabilityType,    "نوع معلولیت"),
                new KeyValuePair<string, string>(CaseFacts.FactMaritalStatus,     "وضعیت تأهل"),
                new KeyValuePair<string, string>(CaseFacts.FactSadatStatus,       "سادات"),
                new KeyValuePair<string, string>(CaseFacts.FactChildrenCount,     "تعداد فرزند/یتیم"),
                new KeyValuePair<string, string>(CaseFacts.FactFamilyMemberCount, "تعداد کل اعضا"),
                new KeyValuePair<string, string>(CaseFacts.FactHeadAge,           "سن سرپرست"),
                new KeyValuePair<string, string>(CaseFacts.FactHasBreadwinner,    "نان‌آور دارد (۱/۰)"),
                new KeyValuePair<string, string>(CaseFacts.FactCompletionStatus,  "وضعیت تکمیل"),
                new KeyValuePair<string, string>(CaseFacts.FactCompletionPercent, "درصد تکمیل"),
                new KeyValuePair<string, string>(CaseFacts.FactHasOrphanRecord,     "رکورد ایتام دارد (۱/۰)"),
                new KeyValuePair<string, string>(CaseFacts.FactHasDisabilityRecord, "رکورد معلولیت دارد (۱/۰)"),
                new KeyValuePair<string, string>(CaseFacts.FactHasMigrantRecord,    "رکورد مهاجرت دارد (۱/۰)"),
                new KeyValuePair<string, string>(CaseFacts.FactFatherStatus,      "وضعیت پدر"),
                new KeyValuePair<string, string>(CaseFacts.FactMotherStatus,      "وضعیت مادر"),
                new KeyValuePair<string, string>(CaseFacts.FactIsStudent,         "محصل است (۱/۰)"),
                new KeyValuePair<string, string>(CaseFacts.FactMigrationCardType, "نوع کارت مهاجرت")
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 5.5-C — خواندن برای صفحهٔ مدیریت.
        //
        // «شرح قاعده» به‌صورتِ متنِ خوانا از خودِ شرط‌ها ساخته می‌شود، نه یک
        // ستونِ توضیحِ دستی — وگرنه با تغییرِ شرط‌ها، توضیح کهنه می‌ماند و
        // دقیقاً همان‌جایی گمراه می‌کند که مدیر به آن اعتماد کرده.
        // ═══════════════════════════════════════════════════════════════════
        public static DataTable GetRuleTable(string search, bool includeInactive)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT r.RuleID, r.Name, r.Priority, r.Amount, IFNULL(r.Notes,'') AS Notes,
       CASE WHEN r.IsActive = 1 THEN 'فعال' ELSE 'غیرفعال' END AS StatusText,
       r.IsActive,
       (SELECT COUNT(*) FROM TblAssistanceRuleCondition c WHERE c.RuleID = r.RuleID) AS ConditionCount
FROM TblAssistanceRule r
WHERE (@Inactive = 1 OR r.IsActive = 1)
  AND (@Term = '' OR r.Name LIKE @Like OR IFNULL(r.Notes,'') LIKE @Like)
ORDER BY r.IsActive DESC, r.Priority, r.RuleID;", con))
            {
                string term = (search ?? "").Trim();
                cmd.Parameters.AddWithValue("@Term", term);
                cmd.Parameters.AddWithValue("@Like", "%" + term + "%");
                cmd.Parameters.AddWithValue("@Inactive", includeInactive ? 1 : 0);
                con.Open();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public static DataTable GetConditionTable(int ruleId)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT ConditionID, FieldName, Operator, IFNULL(Value,'') AS Value
FROM TblAssistanceRuleCondition
WHERE RuleID = @RuleID
ORDER BY ConditionID;", con))
            {
                cmd.Parameters.AddWithValue("@RuleID", ruleId);
                con.Open();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        // متنِ خوانای «منطقِ محاسبه» برای نمایش در صفحهٔ مدیریت و در پروندهٔ
        // فردی: همهٔ شرط‌ها با «و» و سپس مبلغ.
        public static string DescribeRule(int ruleId)
        {
            string name = "";
            decimal amount = 0;
            int priority = 0;
            var parts = new List<string>();

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                using (var cmd = new SQLiteCommand(
                    "SELECT Name, Amount, Priority FROM TblAssistanceRule WHERE RuleID = @Id;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", ruleId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return "";
                        name = dr["Name"].ToString();
                        amount = Convert.ToDecimal(dr["Amount"]);
                        priority = Convert.ToInt32(dr["Priority"]);
                    }
                }

                using (var cmd = new SQLiteCommand(
                    "SELECT FieldName, Operator, IFNULL(Value,'') AS Value FROM TblAssistanceRuleCondition WHERE RuleID = @Id ORDER BY ConditionID;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", ruleId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                            parts.Add(dr["FieldName"] + " " + dr["Operator"] + " " + dr["Value"]);
                    }
                }
            }

            string conditionText = parts.Count == 0
                ? "بدون شرط (این قاعده هرگز تطبیق نمی‌کند)"
                : string.Join("  و  ", parts.ToArray());

            return string.Format("{0}\r\nاولویت: {1}\r\nشرط‌ها: {2}\r\nمبلغ پیشنهادی: {3}",
                name, priority, conditionText, amount);
        }

        public static bool SetRuleActive(int ruleId, bool isActive)
        {
            if (ruleId <= 0) return false;

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "UPDATE TblAssistanceRule SET IsActive = @Active, UpdatedAt = datetime('now') WHERE RuleID = @Id;", con))
            {
                cmd.Parameters.AddWithValue("@Active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@Id", ruleId);
                con.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // حذفِ قاعده — شرط‌ها با CASCADE می‌روند. برخلافِ دفترچه‌های تأمین
        // مالی، اینجا حذفِ فیزیکی بی‌خطر است: هیچ جدولِ دیگری به RuleID
        // ارجاعِ دائمی ندارد (TblAssistanceRuleLog فقط لاگ است و در این فاز
        // ساخته نشده).
        public static bool DeleteRule(int ruleId)
        {
            if (ruleId <= 0) return false;

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM TblAssistanceRule WHERE RuleID = @Id;", con))
            {
                cmd.Parameters.AddWithValue("@Id", ruleId);
                con.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public static bool DeleteCondition(int conditionId)
        {
            if (conditionId <= 0) return false;

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "DELETE FROM TblAssistanceRuleCondition WHERE ConditionID = @Id;", con))
            {
                cmd.Parameters.AddWithValue("@Id", conditionId);
                con.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // ─── مدیریتِ قواعد (برای صفحهٔ مدیریتیِ فازِ بعدی) ────────────────────
        public static int SaveRule(int ruleId, string name, int priority, decimal amount,
            string notes, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            bool isNew = ruleId <= 0;
            decimal oldAmount = 0;

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();

                if (!isNew)
                {
                    using (var cmd = new SQLiteCommand("SELECT Amount FROM TblAssistanceRule WHERE RuleID = @Id;", con))
                    {
                        cmd.Parameters.AddWithValue("@Id", ruleId);
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value) oldAmount = Convert.ToDecimal(result);
                    }
                }

                string sql = isNew
                    ? @"INSERT INTO TblAssistanceRule (Name, Priority, Amount, Notes, IsActive, CreatedBy)
                        VALUES (@Name, @Priority, @Amount, @Notes, @IsActive, @CreatedBy);"
                    : @"UPDATE TblAssistanceRule SET Name = @Name, Priority = @Priority, Amount = @Amount,
                        Notes = @Notes, IsActive = @IsActive, UpdatedAt = datetime('now') WHERE RuleID = @RuleID;";

                using (var cmd = new SQLiteCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Name", name.Trim());
                    cmd.Parameters.AddWithValue("@Priority", priority);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes.Trim());
                    cmd.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
                    if (isNew) cmd.Parameters.AddWithValue("@CreatedBy", (object)SecurityContext.Username ?? DBNull.Value);
                    else cmd.Parameters.AddWithValue("@RuleID", ruleId);
                    cmd.ExecuteNonQuery();
                }

                if (isNew)
                {
                    using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                        ruleId = Convert.ToInt32((long)idCmd.ExecuteScalar());
                }
            }

            // تغییرِ مبلغِ یک قاعده رویدادِ سطحِ پیکربندی است، نه سطحِ پرونده؛
            // چون تایم‌لاین پرونده‌محور است، اینجا ثبت نمی‌شود. فراخوان
            // می‌تواند برای پرونده‌های متأثر LogAssistanceRuleChanged را صدا بزند.
            return ruleId;
        }

        public static int AddCondition(int ruleId, string fieldName, string op, string value)
        {
            if (ruleId <= 0 || string.IsNullOrWhiteSpace(fieldName)) return 0;

            using (var con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                using (var cmd = new SQLiteCommand(@"
INSERT INTO TblAssistanceRuleCondition (RuleID, FieldName, Operator, Value)
VALUES (@RuleID, @FieldName, @Operator, @Value);", con))
                {
                    cmd.Parameters.AddWithValue("@RuleID", ruleId);
                    cmd.Parameters.AddWithValue("@FieldName", fieldName.Trim());
                    cmd.Parameters.AddWithValue("@Operator", (op ?? OpEquals).Trim());
                    cmd.Parameters.AddWithValue("@Value", value == null ? (object)DBNull.Value : value.Trim());
                    cmd.ExecuteNonQuery();
                }

                using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                    return Convert.ToInt32((long)idCmd.ExecuteScalar());
            }
        }
    }
}

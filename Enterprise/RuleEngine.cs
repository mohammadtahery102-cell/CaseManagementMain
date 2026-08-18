using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // نتیجه اجرای مجموعه قواعد یک رویداد.
    public class RuleRunResult
    {
        public bool         Blocked  { get; set; }          // حداقل یک قاعده «جلوگیری» فعال شد
        public List<string> Messages { get; private set; }  // پیام‌های قابل نمایش به کاربر
        public int          Matched  { get; set; }          // تعداد قواعد فعال‌شده

        public RuleRunResult()
        {
            Messages = new List<string>();
        }

        public bool HasMessages { get { return Messages.Count > 0; } }

        public string MessageText
        {
            get { return string.Join(Environment.NewLine, Messages.ToArray()); }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ویژگی ۴ — موتور قواعد.
    //
    // قواعد در جدول EntRule ذخیره می‌شوند و مدیر سیستم می‌تواند بدون تغییر کد
    // آن‌ها را تعریف/غیرفعال کند. هر قاعده یک شرط ساده روی یک ستون دارد و در
    // صورت برقراری، یک «کار» انجام می‌دهد.
    //
    // اصل طراحی محافظه‌کارانه: اگر ارزیابی قاعده‌ای با خطا مواجه شود، آن قاعده
    // نادیده گرفته می‌شود و بقیه عملیات کاربر ادامه پیدا می‌کند؛ یک قاعده
    // اشتباه هرگز نباید ثبت پرونده را از کار بیندازد.
    // ─────────────────────────────────────────────────────────────────────────
    public static class RuleEngine
    {
        // رویدادها
        public const string EventBeforeSave = "قبل از ذخیره";
        public const string EventAfterSave  = "بعد از ذخیره";
        public const string EventTransition = "گذار گردش‌کار";

        // عملگرها
        public const string OpEquals    = "=";
        public const string OpNotEquals = "<>";
        public const string OpGreater   = ">";
        public const string OpLess      = "<";
        public const string OpContains  = "شامل";
        public const string OpEmpty     = "خالی";
        public const string OpNotEmpty  = "پرشده";

        // کارها
        public const string ActionWarn  = "هشدار";
        public const string ActionBlock = "جلوگیری";
        public const string ActionTask  = "وظیفه";
        public const string ActionAudit = "ثبت رویداد";

        // نگاشت نام جدول به ستون کلید اصلی — برای خواندن ردیف موجودیت.
        // موجودیت‌های جدید فقط با افزودن یک سطر این‌جا پشتیبانی می‌شوند.
        private static readonly Dictionary<string, string> PrimaryKeys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "TblCase",       "CasID" },
                { "TblFamily",     "FamID" },
                { "TblDocs",       "DocID" },
                { "TblAssistance", "AssistanceID" },
                { "TblApplicant",  "ApplicantID" }
            };

        // ─── اجرای قواعد یک رویداد ────────────────────────────────────────
        // fields: اگر null باشد، ردیف موجودیت از دیتابیس خوانده می‌شود.
        public static RuleRunResult Run(string entityName, string eventName, int entityId,
                                        IDictionary<string, object> fields = null)
        {
            RuleRunResult result = new RuleRunResult();

            try
            {
                DataTable rules = EntDb.Query(@"
SELECT RuleID, Code, Name, ConditionField, Operator, ConditionValue,
       ActionType, ActionParam, Message, StopOnMatch
FROM   EntRule
WHERE  EntityName = @Entity AND EventName = @Event AND IsActive = 1
  AND  (CenterID IS NULL OR @Center = 0 OR CenterID = @Center)
ORDER  BY Priority, RuleID;",
                    "@Entity", entityName,
                    "@Event",  eventName,
                    "@Center", SecurityContext.CenterFilterId);

                if (rules.Rows.Count == 0) return result;

                IDictionary<string, object> values = fields ?? LoadEntityRow(entityName, entityId);

                foreach (DataRow rule in rules.Rows)
                {
                    bool matched;

                    try
                    {
                        matched = Evaluate(
                            values,
                            EntDb.ToText(rule["ConditionField"]),
                            EntDb.ToText(rule["Operator"]),
                            EntDb.ToText(rule["ConditionValue"]));
                    }
                    catch
                    {
                        // قاعده معیوب: نادیده گرفته می‌شود تا کار کاربر متوقف نشود.
                        continue;
                    }

                    string outcome = matched ? RunAction(rule, entityName, entityId, result) : "";

                    LogRun(EntDb.ToInt(rule["RuleID"]), entityName, entityId, eventName, matched, outcome);

                    if (matched && EntDb.ToBool(rule["StopOnMatch"])) break;
                }
            }
            catch
            {
                // خطای کلی موتور قواعد نباید عملیات اصلی را متوقف کند.
            }

            return result;
        }

        // ─── ارزیابی شرط ─────────────────────────────────────────────────
        private static bool Evaluate(IDictionary<string, object> values,
                                     string field, string op, string expected)
        {
            // قاعده بدون شرط = همیشه برقرار.
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op))
                return true;

            object raw = null;
            if (values != null) values.TryGetValue(field, out raw);

            string actual = raw == null || raw == DBNull.Value ? "" : Convert.ToString(raw).Trim();
            expected = (expected ?? "").Trim();

            switch (op)
            {
                case OpEmpty:    return actual.Length == 0;
                case OpNotEmpty: return actual.Length > 0;
                case OpEquals:   return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                case OpNotEquals:return !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                case OpContains: return actual.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;

                case OpGreater:
                case OpLess:
                    {
                        double left, right;

                        // مقایسه عددی در صورت امکان، وگرنه مقایسه متنی.
                        if (TryNumber(actual, out left) && TryNumber(expected, out right))
                            return op == OpGreater ? left > right : left < right;

                        int compare = string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase);
                        return op == OpGreater ? compare > 0 : compare < 0;
                    }

                default: return false;
            }
        }

        private static bool TryNumber(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out value);
        }

        // ─── اجرای کار ───────────────────────────────────────────────────
        private static string RunAction(DataRow rule, string entityName, int entityId, RuleRunResult result)
        {
            string actionType = EntDb.ToText(rule["ActionType"]);
            string actionParam = EntDb.ToText(rule["ActionParam"]);
            string message = EntDb.ToText(rule["Message"]);

            if (message.Length == 0) message = EntDb.ToText(rule["Name"]);

            result.Matched++;

            switch (actionType)
            {
                case ActionBlock:
                    result.Blocked = true;
                    result.Messages.Add(message);
                    return "جلوگیری شد";

                case ActionWarn:
                    result.Messages.Add(message);
                    return "هشدار داده شد";

                case ActionTask:
                    {
                        // ActionParam = نقش مسئول (خالی یعنی کاربر جاری)
                        int userId  = actionParam.Length == 0 ? SecurityContext.UserId : 0;
                        string role = actionParam.Length == 0 ? null : actionParam;

                        long taskId = TaskService.CreateAuto(
                            message,
                            "ساخته‌شده توسط قاعده «" + EntDb.ToText(rule["Name"]) + "»",
                            entityName, entityId, userId, role,
                            TaskService.SourceRule, EntDb.ToInt(rule["RuleID"]));

                        return taskId > 0 ? "وظیفه #" + taskId + " ساخته شد" : "وظیفه باز از قبل وجود داشت";
                    }

                case ActionAudit:
                    AuditLogger.Log("Rule", entityName, entityId, EntDb.ToText(rule["Code"]), message);
                    return "رویداد ثبت شد";

                default:
                    return "کار ناشناخته";
            }
        }

        // ─── خواندن ردیف موجودیت ─────────────────────────────────────────
        private static IDictionary<string, object> LoadEntityRow(string entityName, int entityId)
        {
            Dictionary<string, object> values =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            string keyColumn;
            if (entityId <= 0 || !PrimaryKeys.TryGetValue(entityName, out keyColumn))
                return values;

            // نام جدول/ستون از نگاشت داخلی می‌آید (نه از ورودی کاربر)، بنابراین
            // چسباندن آن به SQL امن است؛ مقدار همچنان پارامتری می‌ماند.
            DataTable table = EntDb.Query(
                "SELECT * FROM " + entityName + " WHERE " + keyColumn + " = @Id;", "@Id", entityId);

            if (table.Rows.Count == 0) return values;

            DataRow row = table.Rows[0];
            foreach (DataColumn column in table.Columns)
                values[column.ColumnName] = row[column];

            return values;
        }

        private static void LogRun(int ruleId, string entityName, int entityId,
                                   string eventName, bool matched, string outcome)
        {
            try
            {
                EntDb.Exec(@"
INSERT INTO EntRuleLog
    (RuleID, EntityName, EntityID, EventName, Matched, Outcome, CenterID, RunBy)
VALUES
    (@Rule, @Entity, @Id, @Event, @Matched, @Outcome, @Center, @By);",
                    "@Rule",    ruleId,
                    "@Entity",  entityName,
                    "@Id",      entityId > 0 ? (object)entityId : null,
                    "@Event",   eventName,
                    "@Matched", matched ? 1 : 0,
                    "@Outcome", outcome,
                    "@Center",  SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : null,
                    "@By",      SecurityContext.Username);
            }
            catch
            {
                // ثبت تاریخچه قواعد غیرحیاتی است.
            }
        }

        // ─── فهرست‌های کمکی برای فرم مدیریت ──────────────────────────────
        public static List<KeyValuePair<string, string>> EventItems()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(EventBeforeSave, EventBeforeSave),
                new KeyValuePair<string, string>(EventAfterSave,  EventAfterSave),
                new KeyValuePair<string, string>(EventTransition, EventTransition)
            };
        }

        public static List<KeyValuePair<string, string>> OperatorItems()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(OpEquals,    "برابر است با"),
                new KeyValuePair<string, string>(OpNotEquals, "برابر نیست با"),
                new KeyValuePair<string, string>(OpGreater,   "بزرگ‌تر از"),
                new KeyValuePair<string, string>(OpLess,      "کوچک‌تر از"),
                new KeyValuePair<string, string>(OpContains,  "شامل باشد"),
                new KeyValuePair<string, string>(OpEmpty,     "خالی باشد"),
                new KeyValuePair<string, string>(OpNotEmpty,  "خالی نباشد")
            };
        }

        public static List<KeyValuePair<string, string>> ActionItems()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(ActionWarn,  "نمایش هشدار"),
                new KeyValuePair<string, string>(ActionBlock, "جلوگیری از عملیات"),
                new KeyValuePair<string, string>(ActionTask,  "ساخت وظیفه"),
                new KeyValuePair<string, string>(ActionAudit, "ثبت در رویدادها")
            };
        }

        // نام ستون‌های یک جدول — برای انتخاب «فیلد شرط» در فرم مدیریت.
        public static List<KeyValuePair<string, string>> FieldItems(string entityName)
        {
            List<KeyValuePair<string, string>> items = new List<KeyValuePair<string, string>>();
            items.Add(new KeyValuePair<string, string>("", "— بدون شرط (همیشه) —"));

            if (!PrimaryKeys.ContainsKey(entityName ?? "")) return items;

            try
            {
                DataTable table = EntDb.Query("SELECT * FROM " + entityName + " LIMIT 0;");

                foreach (DataColumn column in table.Columns)
                    items.Add(new KeyValuePair<string, string>(column.ColumnName, column.ColumnName));
            }
            catch
            {
                // جدول ناشناخته: فقط گزینه «بدون شرط» می‌ماند.
            }

            return items;
        }

        public static List<string> SupportedEntities()
        {
            return new List<string>(PrimaryKeys.Keys);
        }
    }
}

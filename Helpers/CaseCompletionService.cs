using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    public enum CaseCompletionStatus
    {
        Incomplete,
        InProgress,
        Complete
    }

    public class CaseCompletionResult
    {
        public int CasID;
        public int FieldPercent;
        public int DocumentPercent;
        public int OverallPercent;
        // Phase 5 — بُعدهای اختیاری. *Required نشان می‌دهد آیا این بُعد اصلاً
        // در محاسبه شرکت کرده یا نه (پیش‌فرض: نه).
        public bool VisitRequired;
        public int  VisitPercent;
        public bool FundingRequired;
        public int  FundingPercent;
        public CaseCompletionStatus Status;
        public List<string> MissingFieldDisplayNames = new List<string>();
        public List<string> MissingDocumentCategoryNames = new List<string>();

        public string StatusCode
        {
            get
            {
                switch (Status)
                {
                    case CaseCompletionStatus.Complete: return "COMPLETE";
                    case CaseCompletionStatus.InProgress: return "IN_PROGRESS";
                    default: return "INCOMPLETE";
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // پیش از فاز ۴ — زیرساختِ «کامل‌بودنِ پرونده»: درصد و وضعیت (کامل/در حال
    // تکمیل/ناقص)، بر اساسِ فیلدهای الزامی (TblRequiredField) و اسنادِ الزامی
    // (TblRequiredDocument) برای نوعِ درخواستِ همان پرونده.
    //
    // منبعِ واحدِ حقیقت: هر داشبورد/گزارش/فیلترِ آینده باید از همین سرویس (یا
    // از ستون‌های کش‌شدهٔ TblCase که RecalculateAndStore می‌نویسد) بخواند، نه
    // اینکه منطقِ «کامل‌بودن» را دوباره پیاده کند.
    // ═══════════════════════════════════════════════════════════════════════
    public static class CaseCompletionService
    {
        // وزنِ هرکدام در محاسبهٔ درصدِ کلی — ثابتِ نام‌دار، نه عددِ هاردکد
        // پراکنده؛ تغییرِ وزن‌دهی در آینده فقط همین دو خط را لازم دارد.
        private const double FieldWeight = 0.5;
        private const double DocumentWeight = 0.5;
        // Phase 5 — وزنِ بُعدهای اختیاری. فقط وقتی پرچمِ نوعِ درخواست روشن
        // باشد وارد محاسبه می‌شوند (وگرنه اثرشان صفرِ مطلق است، نه صفرِ وزنی).
        private const double VisitWeight = 0.25;
        private const double FundingWeight = 0.25;

        private static void LoadOptionalRequirementFlags(int requestTypeId,
            out bool requiresFieldVisit, out bool requiresFunding)
        {
            requiresFieldVisit = false;
            requiresFunding = false;

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT RequiresFieldVisit, RequiresFunding FROM TblRequestType WHERE RequestTypeID = @Id;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", requestTypeId);
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            requiresFieldVisit = Convert.ToInt32(dr["RequiresFieldVisit"]) != 0;
                            requiresFunding = Convert.ToInt32(dr["RequiresFunding"]) != 0;
                        }
                    }
                }
            }
            catch
            {
                // اسکیمای قدیمی‌تر (بدونِ این ستون‌ها) ⇒ هر دو خاموش، یعنی
                // رفتارِ پیش از فاز ۵.
            }
        }

        public static CaseCompletionResult Calculate(int casId)
        {
            var result = new CaseCompletionResult { CasID = casId };

            int requestTypeId;
            DataRow caseRow = LoadCaseRow(casId, out requestTypeId);
            if (caseRow == null)
            {
                result.FieldPercent = 100;
                result.DocumentPercent = 100;
                result.OverallPercent = 100;
                result.Status = CaseCompletionStatus.Complete;
                return result;
            }

            CalculateFieldCompletion(caseRow, casId, requestTypeId, result);
            CalculateDocumentCompletion(casId, requestTypeId, result);

            // ─── Phase 5: بُعدهای اختیاریِ بازدید و تأمینِ مالی ───────────────
            // هر دو پرچم پیش‌فرضِ 0 دارند، پس تا وقتی مدیرِ سیستم روشنشان نکند،
            // requiresVisit/requiresFunding هر دو false و فرمول *دقیقاً* همان
            // ۵۰/۵۰ قبلی است — عددِ هیچ پرونده‌ای تکان نمی‌خورد.
            bool requiresVisit, requiresFunding;
            LoadOptionalRequirementFlags(requestTypeId, out requiresVisit, out requiresFunding);

            if (requiresVisit)
            {
                result.VisitRequired = true;
                result.VisitPercent = FieldVisitService.GetVisitCount(casId) > 0 ? 100 : 0;
                if (result.VisitPercent == 0)
                    result.MissingFieldDisplayNames.Add("بازدید میدانی");
            }

            if (requiresFunding)
            {
                result.FundingRequired = true;
                result.FundingPercent = CaseFundingService.GetActiveFundingCount(casId) > 0 ? 100 : 0;
                if (result.FundingPercent == 0)
                    result.MissingFieldDisplayNames.Add("منبع تأمین مالی");
            }

            // وزن‌ها نسبت به بُعدهای *فعال* نرمال می‌شوند؛ افزودنِ یک بُعد،
            // سهمِ بقیه را متناسب کم می‌کند و جمع همیشه ۱۰۰٪ می‌ماند.
            double totalWeight = FieldWeight + DocumentWeight;
            double weightedSum = (result.FieldPercent * FieldWeight) + (result.DocumentPercent * DocumentWeight);

            if (requiresVisit)
            {
                totalWeight += VisitWeight;
                weightedSum += result.VisitPercent * VisitWeight;
            }

            if (requiresFunding)
            {
                totalWeight += FundingWeight;
                weightedSum += result.FundingPercent * FundingWeight;
            }

            result.OverallPercent = (int)Math.Round(weightedSum / totalWeight);

            if (result.OverallPercent >= 100)
                result.Status = CaseCompletionStatus.Complete;
            else if (result.OverallPercent <= 0)
                result.Status = CaseCompletionStatus.Incomplete;
            else
                result.Status = CaseCompletionStatus.InProgress;

            return result;
        }

        // محاسبه می‌کند و نتیجه را روی ستون‌های کشِ TblCase می‌نویسد — تا
        // داشبورد/گزارش/فیلترِ فازِ بعدی بدونِ JOIN یا محاسبهٔ دوباره کار کنند.
        public static CaseCompletionResult RecalculateAndStore(int casId)
        {
            CaseCompletionResult result = Calculate(casId);

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
UPDATE TblCase
SET CompletionPercent = @Percent, CompletionStatusCode = @Status, CompletionCalculatedAt = @CalculatedAt
WHERE CasID = @CasID;", con))
                {
                    cmd.Parameters.AddWithValue("@Percent", result.OverallPercent);
                    cmd.Parameters.AddWithValue("@Status", result.StatusCode);
                    cmd.Parameters.AddWithValue("@CalculatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // آموزش — همانند TimelineService: شکستِ محاسبهٔ کامل‌بودن هرگز
                // نباید ذخیرهٔ پرونده/سند را متوقف کند.
                System.Diagnostics.Debug.WriteLine("CaseCompletionService.RecalculateAndStore failed: " + ex.Message);
            }

            return result;
        }

        // آموزش — «تغییرِ نوع درخواستِ یک پرونده» نیازی به قلابِ جداگانه ندارد:
        // چنین تغییری فقط از راهِ ذخیرهٔ FrmCase ممکن است، و FrmCase همان‌جا
        // RecalculateAndStore را صدا می‌زند (بعد از commit، پس RequestTypeID
        // تازه از دیتابیس خوانده می‌شود). این متد برای دو محرکِ دیگر است —
        // تغییرِ خودِ ماتریسِ TblRequiredField/TblRequiredDocument — که روی
        // *همهٔ* پرونده‌های آن نوع اثر می‌گذارد، نه فقط یکی. فعلاً هیچ صفحهٔ
        // مدیریتی این ماتریس‌ها را ویرایش نمی‌کند؛ این متد همان قلابِ آماده
        // برای وقتی است که چنین صفحه‌ای ساخته شود — زیرساخت، نه سیم‌کشیِ UI.
        public static int RecalculateForRequestType(int requestTypeId)
        {
            var caseIds = new List<int>();
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT CasID FROM TblCase WHERE RequestTypeID = @RequestTypeID AND IFNULL(IsArchived, 0) = 0;", con))
            {
                cmd.Parameters.AddWithValue("@RequestTypeID", requestTypeId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                        caseIds.Add(Convert.ToInt32(dr["CasID"]));
                }
            }

            foreach (int casId in caseIds)
                RecalculateAndStore(casId);

            return caseIds.Count;
        }

        // پوششِ کاملِ بازمحاسبه — همان قلاب برای تغییراتی که همهٔ انواعِ
        // درخواست را با هم تحت تأثیر قرار می‌دهند (مثلاً وزن‌دهیِ کلی عوض شود).
        public static int RecalculateAll()
        {
            var caseIds = new List<int>();
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT CasID FROM TblCase WHERE IFNULL(IsArchived, 0) = 0;", con))
            {
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                        caseIds.Add(Convert.ToInt32(dr["CasID"]));
                }
            }

            foreach (int casId in caseIds)
                RecalculateAndStore(casId);

            return caseIds.Count;
        }

        // آموزش — شکلِ دقیقاً همان چیزی که یک کارتِ KPI در داشبورد لازم دارد
        // (تعدادِ کامل/در حال تکمیل/ناقص)؛ از ستونِ کش‌شده و ایندکس‌شدهٔ
        // TblCase.CompletionStatusCode می‌خواند، نه محاسبهٔ زنده — تا فازِ
        // بعدی فقط این متد را صدا بزند، منطقِ تجمیع را دوباره ننویسد.
        public static Dictionary<string, int> GetStatusCounts(int centerFilterId = 0)
        {
            var counts = new Dictionary<string, int>
            {
                { "COMPLETE", 0 }, { "IN_PROGRESS", 0 }, { "INCOMPLETE", 0 }
            };

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT CompletionStatusCode, COUNT(*) AS Cnt
FROM TblCase
WHERE IFNULL(IsArchived, 0) = 0
  AND (@CenterID = 0 OR CenterID = @CenterID)
  AND CompletionStatusCode IS NOT NULL
GROUP BY CompletionStatusCode;", con))
            {
                cmd.Parameters.AddWithValue("@CenterID", centerFilterId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        string code = dr["CompletionStatusCode"].ToString();
                        if (counts.ContainsKey(code))
                            counts[code] = Convert.ToInt32(dr["Cnt"]);
                    }
                }
            }

            return counts;
        }

        private static DataRow LoadCaseRow(int casId, out int requestTypeId)
        {
            requestTypeId = 0;
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand("SELECT * FROM TblCase WHERE CasID = @CasID;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                con.Open();
                using (var adapter = new SQLiteDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    if (table.Rows.Count == 0) return null;

                    DataRow row = table.Rows[0];
                    requestTypeId = row["RequestTypeID"] == DBNull.Value ? 0 : Convert.ToInt32(row["RequestTypeID"]);
                    return row;
                }
            }
        }

        // آموزش — تضمینِ «نادیده‌گرفتنِ فیلدهای نامرتبط با نوع درخواست» دو لایه
        // دارد، نه یکی: (۱) لایهٔ داده — WHERE RequestTypeID = @RequestTypeID
        // یعنی فیلدِ نوعِ دیگر اصلاً وارد requiredFields نمی‌شود؛ حتی اگر
        // ستونش روی این پرونده مقدار داشته باشد (مثلاً پروندهٔ مهاجرِ سابق که
        // بعداً به ایتام تغییر نوع داده)، آن مقدار هرگز خوانده/بررسی نمی‌شود.
        // (۲) لایهٔ اسکیما — applicableFields پایین‌تر، فیلدهایی که در
        // TblRequiredField تعریف شده‌اند ولی هنوز ستونِ متناظر روی TblCase
        // نیامده (پایگاه‌دادهٔ قدیمی‌تر) را هم از صورت و هم از مخرجِ کسر حذف
        // می‌کند — قبلاً فقط از صورت حذف می‌شدند و مخرج ثابت می‌ماند، یعنی
        // چنین فیلدِ «شبح»‌ای درصد را برای همیشه زیرِ ۱۰۰٪ نگه می‌داشت.
        // یک ردیفِ ماتریسِ فیلدهای الزامی، همراه با جدولِ منبعش (Phase 4).
        private class RequiredFieldSpec
        {
            public string FieldName;
            public string DisplayName;
            public string SourceTable;
        }

        private static void CalculateFieldCompletion(DataRow caseRow, int casId, int requestTypeId, CaseCompletionResult result)
        {
            var requiredFields = new List<RequiredFieldSpec>();

            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT FieldName, DisplayName, IFNULL(SourceTable, 'TblCase') AS SourceTable
FROM TblRequiredField
WHERE RequestTypeID = @RequestTypeID AND IsMandatory = 1 AND IsActive = 1;", con))
            {
                cmd.Parameters.AddWithValue("@RequestTypeID", requestTypeId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        requiredFields.Add(new RequiredFieldSpec
                        {
                            FieldName = dr["FieldName"].ToString(),
                            DisplayName = dr["DisplayName"].ToString(),
                            SourceTable = dr["SourceTable"].ToString()
                        });
                    }
                }
            }

            // Phase 4 — ردیف‌های ماژول یک‌بار خوانده می‌شوند (نه یک کوئری به
            // ازای هر فیلد)، مطابق قاعدهٔ «هیچ کوئری در حلقه» در استانداردها.
            var moduleRows = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var spec in requiredFields)
            {
                if (string.Equals(spec.SourceTable, "TblCase", StringComparison.OrdinalIgnoreCase)) continue;
                if (moduleRows.ContainsKey(spec.SourceTable)) continue;
                moduleRows[spec.SourceTable] = CaseModuleService.Load(spec.SourceTable, casId);
            }

            // فیلدی که ستونش در جدولِ منبع وجود ندارد از صورت *و* مخرج حذف
            // می‌شود (توضیح در بخشِ Case Completion سندِ PROJECT_CONTEXT).
            var applicableFields = new List<RequiredFieldSpec>();
            foreach (var spec in requiredFields)
            {
                if (string.Equals(spec.SourceTable, "TblCase", StringComparison.OrdinalIgnoreCase))
                {
                    if (caseRow.Table.Columns.Contains(spec.FieldName))
                        applicableFields.Add(spec);
                    continue;
                }

                // ردیفِ ماژول هنوز ساخته نشده ⇒ فیلد در امتیاز می‌ماند و
                // «خالی» حساب می‌شود (یعنی پرونده واقعاً ناقص است) — برخلافِ
                // ستونِ ناموجود که یک نقصِ اسکیماست، نه نقصِ داده.
                DataRow moduleRow = moduleRows[spec.SourceTable];
                if (moduleRow == null || moduleRow.Table.Columns.Contains(spec.FieldName))
                    applicableFields.Add(spec);
            }

            if (applicableFields.Count == 0)
            {
                result.FieldPercent = 100;
                return;
            }

            int filled = 0;
            foreach (var spec in applicableFields)
            {
                object value = null;

                if (string.Equals(spec.SourceTable, "TblCase", StringComparison.OrdinalIgnoreCase))
                {
                    value = caseRow[spec.FieldName];
                }
                else
                {
                    DataRow moduleRow = moduleRows[spec.SourceTable];
                    if (moduleRow != null && moduleRow.Table.Columns.Contains(spec.FieldName))
                        value = moduleRow[spec.FieldName];
                }

                bool hasValue = value != null && value != DBNull.Value && !string.IsNullOrWhiteSpace(value.ToString());

                if (hasValue)
                    filled++;
                else
                    result.MissingFieldDisplayNames.Add(spec.DisplayName);
            }

            result.FieldPercent = (int)Math.Round(100.0 * filled / applicableFields.Count);
        }

        private static void CalculateDocumentCompletion(int casId, int requestTypeId, CaseCompletionResult result)
        {
            using (var con = new DatabaseHelper().GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT dc.Name, rd.MinCount,
       (SELECT COUNT(*) FROM TblDocs d
         WHERE d.CasID = @CasID
           AND d.DocumentCategoryID = dc.DocumentCategoryID
           AND IFNULL(d.IsArchived, 0) = 0) AS ExistingCount
FROM TblRequiredDocument rd
JOIN TblDocumentCategory dc ON dc.DocumentCategoryID = rd.DocumentCategoryID AND dc.IsActive = 1
WHERE rd.RequestTypeID = @RequestTypeID AND rd.IsMandatory = 1 AND rd.IsActive = 1" +
// همان قاعدهٔ مشروطِ RequiredDocumentService — اگر اینجا اعمال نمی‌شد،
// پرونده‌ای که «کارت ندارد» در دروازهٔ فعال‌سازی قبول می‌شد ولی درصدِ
// تکمیلش هرگز به ۱۰۰ نمی‌رسید.
RequiredDocumentService.ConditionalFilterByCaseId + @";", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                cmd.Parameters.AddWithValue("@RequestTypeID", requestTypeId);
                RequiredDocumentService.AddConditionalParameters(cmd);
                con.Open();

                int total = 0;
                int satisfied = 0;

                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        total++;
                        int required = Convert.ToInt32(dr["MinCount"]);
                        int existing = Convert.ToInt32(dr["ExistingCount"]);

                        if (existing >= required)
                            satisfied++;
                        else
                            result.MissingDocumentCategoryNames.Add(dr["Name"].ToString());
                    }
                }

                result.DocumentPercent = total == 0 ? 100 : (int)Math.Round(100.0 * satisfied / total);
            }
        }
    }
}

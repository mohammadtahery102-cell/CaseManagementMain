using CaseManagement.DAL;
using System;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // Phase 3 — تایم‌لاین پرونده (Foundation).
    //
    // تنها نویسندهٔ TblCaseTimeline. جداولِ تاریخچهٔ موجود (TblCaseStatusHistory
    // و مشابه) دست‌نخورده می‌مانند و به‌صورتِ موازی همچنان نوشته می‌شوند — این
    // کلاس آن‌ها را جایگزین نمی‌کند، فقط یک نمای یکپارچهٔ تازه برای فازهای بعدی
    // (پرداخت، بازدید میدانی، خروجی Word/PDF/Excel) فراهم می‌کند.
    // ═══════════════════════════════════════════════════════════════════════
    public static class TimelineService
    {
        public const string CategoryCase     = "CASE";
        public const string CategoryStatus   = "STATUS";
        public const string CategoryDocument = "DOCUMENT";
        public const string CategoryPayment  = "PAYMENT";
        public const string CategoryVisit    = "VISIT";
        public const string CategorySystem   = "SYSTEM";

        public const string CategoryAssistance = "ASSISTANCE";
        public const string CategoryFunding    = "FUNDING";

        public const string EventCaseCreated          = "CASE_CREATED";
        public const string EventCaseUpdated           = "CASE_UPDATED";
        public const string EventRequestTypeChanged    = "REQUEST_TYPE_CHANGED";
        public const string EventServiceStatusChanged  = "SERVICE_STATUS_CHANGED";
        public const string EventDocumentAdded         = "DOCUMENT_ADDED";
        public const string EventDocumentRemoved       = "DOCUMENT_REMOVED";

        // Phase 3 (بازبینی) — زیرمجموعهٔ خاصِ تغییرِ وضعیت خدمات؛ همیشه در
        // کنارِ EventServiceStatusChanged ثبت می‌شود (LogServiceStatusChanged
        // پایین‌تر)، نه به‌جای آن.
        public const string EventServiceActivated  = "SERVICE_ACTIVATED";
        public const string EventServiceSuspended  = "SERVICE_SUSPENDED";
        public const string EventServiceTerminated = "SERVICE_TERMINATED";

        // Phase 3 (بازبینی) — رویدادهای فازهای آینده (پرداخت/مساعدت/بازدید
        // میدانی/منبعِ تأمینِ مالی). شِمای TblCaseTimeline از قبل عمومی است
        // (SourceTable/SourceID/Amount/FieldName)، پس این ماژول‌ها بدونِ هیچ
        // تغییرِ جدول، فقط با فراخوانیِ Log(...) کار می‌کنند.
        public const string EventPaymentCreated        = "PAYMENT_CREATED";
        public const string EventPaymentUpdated        = "PAYMENT_UPDATED";
        public const string EventAssistanceGranted     = "ASSISTANCE_GRANTED";
        public const string EventAssistanceModified    = "ASSISTANCE_MODIFIED";
        public const string EventFieldVisitCreated     = "FIELD_VISIT_CREATED";
        public const string EventFieldVisitUpdated     = "FIELD_VISIT_UPDATED";
        public const string EventFundingSourceAssigned = "FUNDING_SOURCE_ASSIGNED";
        public const string EventFundingSourceChanged  = "FUNDING_SOURCE_CHANGED";

        // Phase 5 — تکمیلِ فهرست. شش موردِ زیر در فاز ۳ پیش‌بینی نشده بودند؛
        // بقیهٔ رویدادهای این فاز از قبل بالا تعریف شده‌اند و همان‌ها به‌کار
        // می‌روند (هیچ تغییری در شِمای TblCaseTimeline لازم نشد — دقیقاً همان
        // چیزی که تصمیم #۱۳ وعده داده بود).
        public const string EventFieldVisitDeleted     = "FIELD_VISIT_DELETED";
        public const string EventFundingSourceRemoved  = "FUNDING_SOURCE_REMOVED";
        public const string EventSponsorAssigned       = "SPONSOR_ASSIGNED";
        public const string EventSponsorRemoved        = "SPONSOR_REMOVED";
        public const string EventAssistanceRuleMatched = "ASSISTANCE_RULE_MATCHED";
        public const string EventAssistanceRuleChanged = "ASSISTANCE_RULE_CHANGED";

        // Phase 5.5-A — ویرایشِ دلیلِ تعلیق بدونِ تغییرِ وضعیت.
        // چرا لازم شد: AuditLogger.RecordStatusChange وقتی وضعیت عوض نشده
        // زود برمی‌گردد، پس ویرایشِ دلیل روی پروندهٔ از قبل معلق هیچ ردی
        // نمی‌گذاشت — همان شکافی که «Suspension Reason Updated» می‌بندد.
        public const string EventSuspensionReasonUpdated = "SUSPENSION_REASON_UPDATED";
        public const string EventDocumentVerified        = "DOCUMENT_VERIFIED";
        public const string EventDocumentUnverified      = "DOCUMENT_UNVERIFIED";

        // Phase 5.5-B — امتیاز آسیب‌پذیری.
        public const string CategoryScore          = "SCORE";
        public const string EventScoreCalculated   = "SCORE_CALCULATED";
        public const string EventScoreRecalculated = "SCORE_RECALCULATED";
        public const string EventScoreRuleChanged  = "SCORE_RULE_CHANGED";

        // Phase 6 — گروهِ خانواده.
        public const string CategoryFamily        = "FAMILY";
        public const string EventFamilyLinked     = "FAMILY_LINKED";
        public const string EventFamilyUnlinked   = "FAMILY_UNLINKED";

        // Phase 4 — ماژول‌های تخصصی (ایتام/معلولیت/مهاجرت). یک مجموعهٔ عمومی
        // برای هر سه، چون شِمای TblCaseTimeline از قبل SourceTable/SourceID
        // دارد؛ نامِ جدول در SourceTable می‌نشیند، پس سه‌گانه کردنِ ثابت‌ها
        // (ORPHAN_CREATED/DISABILITY_CREATED/…) هیچ اطلاعاتِ تازه‌ای نمی‌داد.
        public const string CategoryModule = "MODULE";

        // ─── Phase 7: نمایندهٔ قانونی ───────────────────────────────────────
        // دستهٔ جدا و نه CategoryModule: نماینده «ماژولِ تخصصیِ نوعِ درخواست»
        // نیست، یک موجودیتِ فرزندِ چندتاییِ پرونده است — و کاربر باید بتواند
        // تاریخچهٔ نمایندگان را جدا از تاریخچهٔ ماژول‌ها فیلتر کند.
        public const string CategoryRepresentative = "REPRESENTATIVE";

        public const string EventRepresentativeCreated = "REPRESENTATIVE_CREATED";
        public const string EventRepresentativeUpdated = "REPRESENTATIVE_UPDATED";
        public const string EventRepresentativeDeleted = "REPRESENTATIVE_DELETED";

        public const string EventModuleRecordCreated = "MODULE_RECORD_CREATED";
        public const string EventModuleRecordUpdated = "MODULE_RECORD_UPDATED";
        public const string EventModuleRecordDeleted = "MODULE_RECORD_DELETED";

        public static void LogCaseCreated(int casId, string caseCode)
        {
            Log(casId, CategoryCase, EventCaseCreated, null, null, null,
                "ثبت پرونده", "پروندهٔ " + (caseCode ?? "") + " ایجاد شد.");
        }

        public static void LogCaseUpdated(int casId, string details = null)
        {
            Log(casId, CategoryCase, EventCaseUpdated, null, null, null,
                "ویرایش پرونده", details);
        }

        public static void LogRequestTypeChanged(int casId, string oldName, string newName)
        {
            Log(casId, CategoryCase, EventRequestTypeChanged, "RequestType", oldName, newName,
                "تغییر نوع درخواست", oldName + " ← " + newName);
        }

        public static void LogServiceStatusChanged(int casId, string oldName, string newName)
        {
            Log(casId, CategoryStatus, EventServiceStatusChanged, "ServiceStatus", oldName, newName,
                "تغییر وضعیت خدمات", oldName + " ← " + newName);

            // Phase 3 (بازبینی) — رویدادِ اختصاصیِ فعال/معلق/قطع، از رویِ
            // پرچم‌های TblServiceStatus (نه رشتهٔ فارسی هاردکد) استنتاج می‌شود.
            var flags = FindServiceStatusFlagsByName(newName);
            if (flags == null) return;

            if (flags.IsActiveService)
                Log(casId, CategoryStatus, EventServiceActivated, "ServiceStatus", oldName, newName,
                    "فعال‌سازی خدمات", newName);
            else if (flags.IsTerminal)
                Log(casId, CategoryStatus, EventServiceTerminated, "ServiceStatus", oldName, newName,
                    "قطع خدمات", newName);
            else if (!flags.IsPreService)
                Log(casId, CategoryStatus, EventServiceSuspended, "ServiceStatus", oldName, newName,
                    "تعلیقِ خدمات", newName);
        }

        private class ServiceStatusFlags
        {
            public bool IsPreService;
            public bool IsActiveService;
            public bool IsTerminal;
        }

        private static ServiceStatusFlags FindServiceStatusFlagsByName(string name)
        {
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
SELECT IsPreService, IsActiveService, IsTerminal FROM TblServiceStatus WHERE Name = @Name;", con))
                {
                    cmd.Parameters.AddWithValue("@Name", name ?? "");
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return null;
                        return new ServiceStatusFlags
                        {
                            IsPreService = Convert.ToInt32(dr["IsPreService"]) != 0,
                            IsActiveService = Convert.ToInt32(dr["IsActiveService"]) != 0,
                            IsTerminal = Convert.ToInt32(dr["IsTerminal"]) != 0
                        };
                    }
                }
            }
            catch { return null; }
        }

        public static void LogDocumentAdded(int casId, int docId, string categoryName, string title)
        {
            Log(casId, CategoryDocument, EventDocumentAdded, null, null, null,
                "افزودن سند", (categoryName ?? "") + (string.IsNullOrEmpty(title) ? "" : " — " + title),
                "TblDocs", docId);
        }

        public static void LogDocumentRemoved(int casId, int docId, string categoryName, string title)
        {
            Log(casId, CategoryDocument, EventDocumentRemoved, null, null, null,
                "حذف سند", (categoryName ?? "") + (string.IsNullOrEmpty(title) ? "" : " — " + title),
                "TblDocs", docId);
        }

        // Phase 4 — رویدادهای ماژولِ تخصصی. moduleTitle متنِ فارسیِ نامِ ماژول
        // است («اطلاعات ایتام»/«اطلاعات معلولیت»/«اطلاعات مهاجرت») و
        // sourceTable نامِ جدول، تا تایم‌لاین بدونِ join قابلِ خواندن بماند.
        public static void LogModuleRecordCreated(int casId, string sourceTable, int sourceId, string moduleTitle)
        {
            Log(casId, CategoryModule, EventModuleRecordCreated, null, null, null,
                "ثبت " + moduleTitle, null, sourceTable, sourceId);
        }

        public static void LogModuleRecordUpdated(int casId, string sourceTable, int sourceId, string moduleTitle)
        {
            Log(casId, CategoryModule, EventModuleRecordUpdated, null, null, null,
                "ویرایش " + moduleTitle, null, sourceTable, sourceId);
        }

        public static void LogModuleRecordDeleted(int casId, string sourceTable, int sourceId, string moduleTitle)
        {
            Log(casId, CategoryModule, EventModuleRecordDeleted, null, null, null,
                "حذف " + moduleTitle, null, sourceTable, sourceId);
        }

        // تغییرِ یک فیلدِ مشخص در جدولِ ماژول — «از چه» به «چه».
        //
        // آموزش — چرا جدا از LogModuleRecordUpdated: آن رویداد فقط می‌گوید
        // «چیزی در این ماژول عوض شد» و سه ستونِ FieldName/OldValue/NewValue را
        // خالی می‌گذارد. برای حسابرسیِ واقعی باید بشود پرسید «درجهٔ معلولیت
        // این پرونده کِی، توسطِ چه کسی، از اول به سوم تغییر کرد؟» — که پاسخش
        // فقط با پرشدنِ همین سه ستون ممکن است. اسکیمای TblCaseTimeline از
        // ابتدا این ستون‌ها را داشت؛ تنها جای خالی همین متد بود.
        //
        // هر دو رویداد با هم ثبت می‌شوند: یکی سرصفحهٔ «ویرایش شد» برای نمایشِ
        // خلاصه، و یکی ردیف به‌ازای هر فیلدِ تغییریافته برای جزئیات.
        public static void LogModuleFieldChanged(int casId, string sourceTable, int sourceId,
            string moduleTitle, string fieldName, string oldValue, string newValue)
        {
            Log(casId, CategoryModule, EventModuleRecordUpdated, fieldName, oldValue, newValue,
                "ویرایش " + moduleTitle, null, sourceTable, sourceId);
        }

        // ─── Phase 7: نمایندهٔ قانونی ───────────────────────────────────────
        public static void LogRepresentativeCreated(int casId, int representativeId,
            string slotLabel, string fullName)
        {
            Log(casId, CategoryRepresentative, EventRepresentativeCreated, null, null, null,
                "ثبت " + slotLabel, fullName, "TblCaseRepresentative", representativeId);
        }

        public static void LogRepresentativeUpdated(int casId, int representativeId,
            string slotLabel, string fullName)
        {
            Log(casId, CategoryRepresentative, EventRepresentativeUpdated, null, null, null,
                "ویرایش " + slotLabel, fullName, "TblCaseRepresentative", representativeId);
        }

        public static void LogRepresentativeDeleted(int casId, int representativeId,
            string slotLabel, string fullName)
        {
            Log(casId, CategoryRepresentative, EventRepresentativeDeleted, null, null, null,
                "حذف " + slotLabel, fullName, "TblCaseRepresentative", representativeId);
        }

        // تغییرِ یک فیلدِ مشخص — همان تفکیکِ LogModuleFieldChanged: رویدادِ
        // سرصفحه می‌گوید «چیزی عوض شد»، این ردیف‌ها می‌گویند «چه، از چه، به چه».
        // بدونِ این، پرسشِ حسابرسیِ «شمارهٔ تماسِ نماینده کِی و توسطِ چه کسی
        // عوض شد؟» پاسخی نداشت.
        public static void LogRepresentativeFieldChanged(int casId, int representativeId,
            string slotLabel, string fieldLabel, string oldValue, string newValue)
        {
            Log(casId, CategoryRepresentative, EventRepresentativeUpdated,
                fieldLabel, oldValue, newValue,
                "ویرایش " + slotLabel, null, "TblCaseRepresentative", representativeId);
        }

        // ─── Phase 5: بازدید میدانی ─────────────────────────────────────────
        public static void LogFieldVisitCreated(int casId, int visitId, string visitDate)
        {
            Log(casId, CategoryVisit, EventFieldVisitCreated, null, null, null,
                "ثبت بازدید میدانی", visitDate, "TblFieldVisit", visitId);
        }

        public static void LogFieldVisitUpdated(int casId, int visitId, string visitDate)
        {
            Log(casId, CategoryVisit, EventFieldVisitUpdated, null, null, null,
                "ویرایش بازدید میدانی", visitDate, "TblFieldVisit", visitId);
        }

        public static void LogFieldVisitDeleted(int casId, int visitId, string visitDate)
        {
            Log(casId, CategoryVisit, EventFieldVisitDeleted, null, null, null,
                "حذف بازدید میدانی", visitDate, "TblFieldVisit", visitId);
        }

        // ─── Phase 5: تأمین مالی و خیّر ─────────────────────────────────────
        public static void LogFundingSourceAssigned(int casId, int caseFundingId, string sourceName)
        {
            Log(casId, CategoryFunding, EventFundingSourceAssigned, null, null, sourceName,
                "تخصیص منبع تأمین مالی", sourceName, "TblCaseFunding", caseFundingId);
        }

        public static void LogFundingSourceRemoved(int casId, int caseFundingId, string sourceName)
        {
            Log(casId, CategoryFunding, EventFundingSourceRemoved, null, sourceName, null,
                "حذف منبع تأمین مالی", sourceName, "TblCaseFunding", caseFundingId);
        }

        public static void LogSponsorAssigned(int casId, int caseFundingId, string sponsorName)
        {
            Log(casId, CategoryFunding, EventSponsorAssigned, null, null, sponsorName,
                "تخصیص خیّر", sponsorName, "TblCaseFunding", caseFundingId);
        }

        public static void LogSponsorRemoved(int casId, int caseFundingId, string sponsorName)
        {
            Log(casId, CategoryFunding, EventSponsorRemoved, null, sponsorName, null,
                "حذف خیّر", sponsorName, "TblCaseFunding", caseFundingId);
        }

        // ─── Phase 5: قواعد مساعدت ──────────────────────────────────────────
        // amount مبلغِ پیشنهادی است، نه پرداختی — این فاز هیچ پرداختی نمی‌سازد.
        public static void LogAssistanceRuleMatched(int casId, int ruleId, string ruleName, decimal amount)
        {
            Log(casId, CategoryAssistance, EventAssistanceRuleMatched, null, null, null,
                "تطبیق قاعده مساعدت", ruleName, "TblAssistanceRule", ruleId, amount);
        }

        public static void LogAssistanceRuleChanged(int casId, int ruleId, string ruleName,
            string oldAmount, string newAmount)
        {
            Log(casId, CategoryAssistance, EventAssistanceRuleChanged, "Amount", oldAmount, newAmount,
                "تغییر مبلغ پیشنهادی", ruleName, "TblAssistanceRule", ruleId);
        }

        // ─── Phase 5.5-A: تعلیق و تأییدِ سند ────────────────────────────────
        public static void LogSuspensionReasonUpdated(int casId, string oldReason, string newReason)
        {
            Log(casId, CategoryStatus, EventSuspensionReasonUpdated, "SuspensionReason",
                oldReason, newReason,
                "ویرایش دلیل تعلیق",
                (string.IsNullOrWhiteSpace(oldReason) ? "(خالی)" : oldReason) + " ← " +
                (string.IsNullOrWhiteSpace(newReason) ? "(خالی)" : newReason));
        }

        public static void LogDocumentVerified(int casId, int docId, string docTitle, string notes)
        {
            Log(casId, CategoryDocument, EventDocumentVerified, null, null, null,
                "تأیید سند", string.IsNullOrWhiteSpace(notes) ? docTitle : docTitle + " — " + notes,
                "TblDocs", docId);
        }

        public static void LogDocumentUnverified(int casId, int docId, string docTitle)
        {
            Log(casId, CategoryDocument, EventDocumentUnverified, null, null, null,
                "لغو تأیید سند", docTitle, "TblDocs", docId);
        }

        // ─── Phase 5.5-B: امتیاز آسیب‌پذیری ─────────────────────────────────
        // اولین محاسبه CALCULATED است و محاسبه‌های بعدی RECALCULATED — تا در
        // تایم‌لاین بتوان «کِی اولین بار امتیاز گرفت» را از «کِی عوض شد» جدا کرد.
        public static void LogScoreCalculated(int casId, double score, string band, string reason, bool isFirst)
        {
            Log(casId, CategoryScore,
                isFirst ? EventScoreCalculated : EventScoreRecalculated,
                "VulnerabilityScore", null, score.ToString("0.#"),
                isFirst ? "محاسبه امتیاز آسیب‌پذیری" : "بازمحاسبه امتیاز آسیب‌پذیری",
                (band ?? "") + (string.IsNullOrEmpty(reason) ? "" : " — " + reason),
                "TblVulnerabilityScore", null, (decimal)score);
        }

        public static void LogScoreChanged(int casId, double oldScore, double newScore, string band, string reason)
        {
            Log(casId, CategoryScore, EventScoreRecalculated,
                "VulnerabilityScore", oldScore.ToString("0.#"), newScore.ToString("0.#"),
                "تغییر امتیاز آسیب‌پذیری",
                (band ?? "") + (string.IsNullOrEmpty(reason) ? "" : " — " + reason),
                "TblVulnerabilityScore", null, (decimal)newScore);
        }

        // تغییرِ خودِ قواعد رویدادِ سطحِ پیکربندی است و پرونده‌محور نیست؛ ولی
        // چون تایم‌لاین پرونده‌محور است، برای هر پروندهٔ متأثر ثبت می‌شود تا
        // «چرا امتیازِ این پرونده عوض شد» قابلِ ردیابی بماند.
        public static void LogScoreRuleChanged(int casId, string ruleDescription)
        {
            Log(casId, CategoryScore, EventScoreRuleChanged, null, null, null,
                "تغییر قواعد امتیازدهی", ruleDescription);
        }

        // ─── Phase 6: گروهِ خانواده ─────────────────────────────────────────
        // رویداد روی *هر دو* پرونده ثبت می‌شود (ریشه و عضو)، چون تایم‌لاین
        // پرونده‌محور است و کاربر باید پیوند را از هر دو طرف ببیند.
        public static void LogFamilyLinked(int casId, int familyGroupId, string relatedCaseCode)
        {
            Log(casId, CategoryFamily, EventFamilyLinked, "FamilyGroupID", null,
                familyGroupId.ToString(),
                "پیوند به خانواده", relatedCaseCode, "TblCase", familyGroupId);
        }

        public static void LogFamilyUnlinked(int casId, int familyGroupId, string relatedCaseCode)
        {
            Log(casId, CategoryFamily, EventFamilyUnlinked, "FamilyGroupID",
                familyGroupId.ToString(), null,
                "حذف پیوند خانواده", relatedCaseCode, "TblCase", familyGroupId);
        }

        // نقطهٔ ورودِ عمومی — فازهای بعدی (پرداخت/بازدید میدانی) هم از همین
        // متد استفاده می‌کنند، بدون نیاز به تغییرِ شِمای جدول.
        // ─── خواندن (H5) ─────────────────────────────────────────────────────
        // آموزش — تا پیش از این، TblCaseTimeline شش نویسنده داشت و *هیچ*
        // خواننده‌ای: کامل‌ترین تاریخچه‌ای که سامانه نگه می‌داشت هرگز به چشمِ
        // کاربر نمی‌رسید. این متد همان قلابِ خواندن است که تبِ «تاریخچه» در
        // FrmCase از آن استفاده می‌کند.
        //
        // ترتیب و ایندکس: IX_TblCaseTimeline_Case دقیقاً (CasID, EventAt DESC)
        // است، پس این کوئری روی همان ایندکس می‌نشیند و صفحه‌بندی لازم ندارد.
        // limit یک سقفِ ایمنی است تا پرونده‌ای با هزاران رویداد فرم را کند نکند.
        public static DataTable GetCaseTimeline(int casId, int limit = 500)
        {
            var table = new DataTable();
            table.Columns.Add("زمان", typeof(string));
            table.Columns.Add("رویداد", typeof(string));
            table.Columns.Add("فیلد", typeof(string));
            table.Columns.Add("مقدار قبلی", typeof(string));
            table.Columns.Add("مقدار جدید", typeof(string));
            table.Columns.Add("کاربر", typeof(string));

            if (casId <= 0) return table;

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
SELECT EventAt, Title, FieldName, OldValue, NewValue, Username
FROM TblCaseTimeline
WHERE CasID = @CasID
ORDER BY EventAt DESC, TimelineID DESC
LIMIT @Limit;", con))
                {
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    cmd.Parameters.AddWithValue("@Limit", limit);
                    con.Open();

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            table.Rows.Add(
                                FormatInstant(dr["EventAt"]),
                                Str(dr["Title"]),
                                Str(dr["FieldName"]),
                                Str(dr["OldValue"]),
                                Str(dr["NewValue"]),
                                Str(dr["Username"]));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // نمایشِ تاریخچه هرگز نباید بازکردنِ پرونده را بشکند.
                System.Diagnostics.Debug.WriteLine("TimelineService.GetCaseTimeline failed: " + ex.Message);
            }

            return table;
        }

        private static string Str(object value)
        {
            return value == null || value == DBNull.Value ? "" : value.ToString();
        }

        // EventAt به‌صورت «yyyy-MM-dd HH:mm:ss» میلادی ذخیره شده است؛ نمایش
        // طبقِ قاعدهٔ پروژه شمسی است (تبدیل فقط در لایهٔ UI).
        private static string FormatInstant(object value)
        {
            string raw = Str(value);
            if (raw.Length == 0) return "";

            DateTime parsed;
            if (!DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out parsed))
                return raw;

            return PersianDateHelper.ToPersianDateString(parsed) + " " + parsed.ToString("HH:mm");
        }

        public static void Log(
            int casId,
            string eventCategoryCode,
            string eventTypeCode,
            string fieldName,
            string oldValue,
            string newValue,
            string title,
            string details = null,
            string sourceTable = null,
            int? sourceId = null,
            decimal? amount = null,
            int? famId = null)
        {
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(@"
INSERT INTO TblCaseTimeline
    (CasID, EventCategoryCode, EventTypeCode, EventDate, SourceTable, SourceID, FamID,
     FieldName, OldValue, NewValue, Amount, Title, Details, IsSystemGenerated,
     UserID, Username, CenterID)
VALUES
    (@CasID, @Category, @Type, @EventDate, @SourceTable, @SourceID, @FamID,
     @FieldName, @OldValue, @NewValue, @Amount, @Title, @Details, 1,
     @UserID, @Username, @CenterID);", con))
                {
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    cmd.Parameters.AddWithValue("@Category", eventCategoryCode ?? CategorySystem);
                    cmd.Parameters.AddWithValue("@Type", eventTypeCode ?? "");
                    cmd.Parameters.AddWithValue("@EventDate", DateTime.Now.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@SourceTable", (object)sourceTable ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SourceID", (object)sourceId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FamID", (object)famId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FieldName", (object)fieldName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OldValue", (object)oldValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@NewValue", (object)newValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Amount", (object)amount ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Title", title ?? "");
                    cmd.Parameters.AddWithValue("@Details", (object)details ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserID", SecurityContext.IsLoggedIn ? (object)SecurityContext.UserId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Username", (object)SecurityContext.Username ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CenterID", SecurityContext.HasCenter ? (object)SecurityContext.CurrentCenterId : DBNull.Value);

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // آموزش — همانند AuditLogger: شکستِ ثبتِ تایم‌لاین هرگز نباید
                // ذخیرهٔ پرونده/سند را متوقف کند.
                System.Diagnostics.Debug.WriteLine("TimelineService.Log failed: " + ex.Message);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace CaseManagement.Helpers
{
    public enum ReportColumnType { Text, Number, Date }

    // یک ستونِ قابل‌انتخاب در گزارش‌ساز. Expression همیشه از یک لیستِ ثابتِ
    // سفیدِ از پیش‌نوشته‌شده می‌آید — هرگز از ورودیِ کاربر ساخته نمی‌شود، پس
    // تزریق SQL از طریق انتخابِ ستون ممکن نیست.
    public sealed class ReportColumn
    {
        public string Key;
        public string DisplayName;
        public string Expression; // مثل c.HeadFullName یا c.Code
        public ReportColumnType Type;

        public ReportColumn(string key, string displayName, string expression, ReportColumnType type)
        {
            Key = key; DisplayName = displayName; Expression = expression; Type = type;
        }
    }

    // یک منبعِ گزارش (Cases/Family/Documents/Assistance) با FROM/JOIN ثابت و
    // فهرستِ ستون‌های مجاز.
    public sealed class ReportSource
    {
        public string Key;
        public string DisplayName;
        public string FromClause;      // مثل "TblCase c"
        public string CenterColumn;    // برای فیلترِ مرکز، مثل "c.CenterID"
        public string ArchivedColumn;  // اگر null باشد یعنی این منبع IsArchived ندارد
        public List<ReportColumn> Columns = new List<ReportColumn>();

        public ReportColumn FindColumn(string key)
        {
            return Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
        }
    }

    public enum ReportFilterOperator { Equals, Contains, GreaterThan, LessThan, GreaterOrEqual, LessOrEqual }

    public sealed class ReportFilter
    {
        public string ColumnKey;
        public ReportFilterOperator Op;
        public string Value;
    }

    // تعریفِ کاملِ یک گزارش، قابلِ سریالایز به JSON برای ذخیره در TblReportTemplate.
    public sealed class ReportDefinition
    {
        public string SourceKey;
        public List<string> ColumnKeys = new List<string>();
        public List<ReportFilterDto> Filters = new List<ReportFilterDto>();
        public string GroupByKey;
        public string SortColumnKey;
        public bool SortDescending;
    }

    // نسخه‌ی ساده‌ی ReportFilter برای سریالایزِ JSON (Enum به‌صورت رشته).
    public sealed class ReportFilterDto
    {
        public string ColumnKey;
        public string Op;
        public string Value;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // فهرستِ ثابتِ منابع/ستون‌های مجاز گزارش‌ساز. هر منبعِ جدید فقط اینجا اضافه
    // می‌شود — فرم و اجراکننده‌ی کوئری هیچ نامِ ستون/جدولی را از کاربر مستقیماً
    // در SQL نمی‌گذارند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ReportCatalog
    {
        // آموزش — چرا ثابت و نه رشتهٔ درجا: این دو زیرکوئری در چند ستون
        // تکرار می‌شوند و هر واگراییِ کوچکی بینشان یعنی «تعداد کم» با
        // «کامل است؟» همدیگر را نقض کنند.
        private const string REQUIRED_DOC_COUNT =
            "(SELECT COUNT(*) FROM TblRequiredDocument rd0 WHERE rd0.RequestTypeID = c.RequestTypeID AND rd0.IsActive = 1 AND rd0.IsMandatory = 1)";

        private const string SATISFIED_DOC_COUNT =
            "(SELECT COUNT(*) FROM TblRequiredDocument rd1 JOIN TblDocumentCategory dc1 ON dc1.DocumentCategoryID = rd1.DocumentCategoryID AND dc1.IsActive = 1 WHERE rd1.RequestTypeID = c.RequestTypeID AND rd1.IsActive = 1 AND rd1.IsMandatory = 1 AND (SELECT COUNT(*) FROM TblDocs d1 WHERE d1.CasID = c.CasID AND d1.DocumentCategoryID = rd1.DocumentCategoryID AND IFNULL(d1.IsArchived,0) = 0) >= rd1.MinCount)";

        public static readonly List<ReportSource> Sources = BuildSources();

        public static List<ReportSource> VisibleSources()
        {
            if (!ProductMode.IsErp) return Sources;

            var visible = new List<ReportSource>();
            foreach (ReportSource source in Sources)
            {
                if (!ProductMode.IsCharityReportSource(source.Key))
                    visible.Add(source);
            }
            return visible;
        }

        public static ReportSource FindSource(string key)
        {
            ReportSource source = Sources.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
            if (source == null) return null;
            if (ProductMode.IsErp && ProductMode.IsCharityReportSource(source.Key))
                return null;
            return source;
        }

        private static List<ReportSource> BuildSources()
        {
            var list = new List<ReportSource>();

            var cases = new ReportSource
            {
                Key = "Cases", DisplayName = "پرونده‌ها",
                FromClause = "TblCase c",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            cases.Columns.Add(new ReportColumn("Code", "کد اختصاصی", "c.Code", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("FormNo", "شماره فرم", "c.FormNo", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("HeadFatherName", "نام پدر سرپرست", "c.HeadFatherName", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("HeadIdCardType", "نوع تذکره", "c.HeadIdCardType", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("HeadTazkiraNo", "شماره تذکره", "c.HeadTazkiraNo", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("PhysicalStatusNotes", "یادداشت وضعیت جسمی", "c.PhysicalStatusNotes", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("Phone", "شماره تماس", "c.Phone", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("Province", "ولایت", "c.Province", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("District", "ولسوالی", "c.District", ReportColumnType.Text));
            // مورد ۷ — «سایت» به گزارش‌سازِ پویا. فقط افزودنی است، پس
            // قالب‌های ذخیره‌شدهٔ کاربران دست‌نخورده کار می‌کنند.
            cases.Columns.Add(new ReportColumn("Site", "سایت", "c.Site", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("ServiceStatus", "وضعیت خدمات", "c.ServiceStatus", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("RequestType", "نوع درخواست", "c.RequestType", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("PriorityLevel", "اولویت بندی اقتصادی", "c.PriorityLevel", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("CoveredByOrg", "تحت پوشش دیگر مؤسسات", "c.CoveredByOrg", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("CoveredByOrgNames", "اسامی مؤسسات تحت پوشش", "c.CoveredByOrgNames", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("CaseDate", "تاریخ پرونده", "c.CaseDate", ReportColumnType.Date));
            list.Add(cases);

            var family = new ReportSource
            {
                Key = "Family", DisplayName = "اعضای خانواده",
                FromClause = "TblFamily f JOIN TblCase c ON c.CasID = f.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            family.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("MemberName", "نام عضو", "f.MemberName", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("MemberFatherName", "نام پدر عضو", "f.MemberFatherName", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("MemberIdCardType", "نوع تذکره", "f.MemberIdCardType", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("MemberTazkiraNo", "شماره تذکره", "f.MemberTazkiraNo", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("Gender", "جنسیت", "f.Gender", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("BirthDate", "تاریخ تولد", "f.BirthDate", ReportColumnType.Date));
            family.Columns.Add(new ReportColumn("MemberEducation", "تحصیلات", "f.MemberEducation", ReportColumnType.Text));
            family.Columns.Add(new ReportColumn("HasDisability", "معلولیت", "f.HasDisability", ReportColumnType.Text));
            // وضعیت خدماتِ پرونده — محورِ اصلی گزارش‌گیری سامانه. با افزوده شدن
            // اینجا، کاربر می‌تواند در گزارش اعضای خانواده هم آن را نمایش دهد،
            // هم رویش فیلتر بگذارد و هم بر اساسش گروه‌بندی کند.
            family.Columns.Add(new ReportColumn("ServiceStatus", "وضعیت خدمات", "c.ServiceStatus", ReportColumnType.Text));
            // نوع درخواستِ پرونده — تا در گزارشِ اعضا بتوان ایتام را از سایر
            // بخش‌ها (معلول، مهاجر، بدسرپرست، …) جدا کرد؛ هم به‌عنوان ستون، هم
            // فیلتر و هم محورِ گروه‌بندی.
            family.Columns.Add(new ReportColumn("RequestType", "نوع درخواست", "c.RequestType", ReportColumnType.Text));
            list.Add(family);

            var docs = new ReportSource
            {
                Key = "Documents", DisplayName = "اسناد",
                FromClause = "TblDocs d JOIN TblCase c ON c.CasID = d.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            docs.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("DocNo", "شماره سند", "d.DocNo", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("DocType", "نوع سند", "d.DocType", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("DocCategory", "دسته‌بندی سند", "d.DocCategory", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("OriginalFileName", "نام فایل", "d.OriginalFileName", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("ServiceStatus", "وضعیت خدمات", "c.ServiceStatus", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("RequestType", "نوع درخواست", "c.RequestType", ReportColumnType.Text));
            // Phase 5.5-D — وضعیتِ تأییدِ سند (ستون‌های فاز ۵.۵-الف).
            docs.Columns.Add(new ReportColumn("VerificationStatus", "وضعیت تأیید",
                "CASE WHEN IFNULL(d.IsVerified,0) = 1 THEN 'تأیید شده' ELSE 'در انتظار تأیید' END",
                ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("VerifiedBy", "تأییدکننده", "d.VerifiedBy", ReportColumnType.Text));
            docs.Columns.Add(new ReportColumn("VerifiedDate", "تاریخ تأیید", "d.VerifiedDate", ReportColumnType.Date));
            list.Add(docs);

            var assistance = new ReportSource
            {
                Key = "Assistance", DisplayName = "کمک‌های مالی",
                FromClause = "TblAssistance a JOIN TblCase c ON c.CasID = a.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            assistance.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            assistance.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            assistance.Columns.Add(new ReportColumn("AssistanceDate", "تاریخ کمک", "a.AssistanceDate", ReportColumnType.Date));
            assistance.Columns.Add(new ReportColumn("Amount", "مبلغ", "a.Amount", ReportColumnType.Number));
            assistance.Columns.Add(new ReportColumn("AssistanceType", "نوع کمک", "a.AssistanceType", ReportColumnType.Text));
            assistance.Columns.Add(new ReportColumn("ServiceStatus", "وضعیت خدمات", "c.ServiceStatus", ReportColumnType.Text));
            list.Add(assistance);

            // آموزش — دو منبعِ تازه: تا امروز گزارش‌ساز فقط «وضعیتِ فعلی» را
            // می‌دید و هیچ راهی برای گزارش‌گیری از «چه چیزی کِی و توسطِ چه کسی
            // عوض شد» نبود (مثلاً: همهٔ پرونده‌هایی که برج گذشته قطع شدند، یا
            // کارِ یک کاربرِ مشخص). هر دو منبع از همان زیرساختِ موجودِ فیلتر و
            // گروه‌بندی استفاده می‌کنند و JOIN به TblCase دارند تا فیلترِ مرکز و
            // بایگانی مثلِ بقیهٔ منابع اعمال شود.
            var caseHistory = new ReportSource
            {
                Key = "CaseHistory", DisplayName = "تاریخچه وضعیت پرونده",
                FromClause = "TblCaseStatusHistory sh JOIN TblCase c ON c.CasID = sh.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            caseHistory.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            caseHistory.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            caseHistory.Columns.Add(new ReportColumn("Province", "ولایت", "c.Province", ReportColumnType.Text));
            caseHistory.Columns.Add(new ReportColumn("District", "ولسوالی", "c.District", ReportColumnType.Text));
            caseHistory.Columns.Add(new ReportColumn("OldStatus", "وضعیت قبلی", "sh.OldStatus", ReportColumnType.Text));
            caseHistory.Columns.Add(new ReportColumn("NewStatus", "وضعیت جدید", "sh.NewStatus", ReportColumnType.Text));
            caseHistory.Columns.Add(new ReportColumn("ChangeType", "نوع تغییر", "sh.ChangeType", ReportColumnType.Text));
            caseHistory.Columns.Add(new ReportColumn("Reason", "دلیل", "sh.Reason", ReportColumnType.Text));
            caseHistory.Columns.Add(new ReportColumn("Notes", "یادداشت", "sh.Notes", ReportColumnType.Text));
            caseHistory.Columns.Add(new ReportColumn("ChangedAt", "تاریخ تغییر", "sh.ChangedAt", ReportColumnType.Date));
            caseHistory.Columns.Add(new ReportColumn("ChangedBy", "تغییر توسط", "sh.ChangedBy", ReportColumnType.Text));
            list.Add(caseHistory);

            var familyHistory = new ReportSource
            {
                Key = "FamilyHistory", DisplayName = "تاریخچه وضعیت اعضا",
                FromClause = "TblFamilyStatusHistory fh " +
                             "JOIN TblFamily f ON f.FamID = fh.FamID " +
                             "JOIN TblCase   c ON c.CasID = f.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            familyHistory.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            familyHistory.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            familyHistory.Columns.Add(new ReportColumn("MemberName", "نام عضو", "f.MemberName", ReportColumnType.Text));
            familyHistory.Columns.Add(new ReportColumn("OldValue", "مقدار قبلی", "fh.OldValue", ReportColumnType.Text));
            familyHistory.Columns.Add(new ReportColumn("NewValue", "مقدار جدید", "fh.NewValue", ReportColumnType.Text));
            familyHistory.Columns.Add(new ReportColumn("ChangeType", "نوع تغییر", "fh.ChangeType", ReportColumnType.Text));
            familyHistory.Columns.Add(new ReportColumn("Reason", "دلیل", "fh.Reason", ReportColumnType.Text));
            familyHistory.Columns.Add(new ReportColumn("Notes", "یادداشت", "fh.Notes", ReportColumnType.Text));
            familyHistory.Columns.Add(new ReportColumn("ChangedAt", "تاریخ تغییر", "fh.ChangedAt", ReportColumnType.Date));
            familyHistory.Columns.Add(new ReportColumn("ChangedBy", "تغییر توسط", "fh.ChangedBy", ReportColumnType.Text));
            list.Add(familyHistory);

            // ═══════════════════════════════════════════════════════════════
            // Phase 5.5-C — منابعِ گزارشِ پیشرفته.
            //
            // همه از همان زیرساختِ موجود (فیلتر/گروه‌بندی/فیلترِ مرکز/بایگانی)
            // استفاده می‌کنند و فقط داده تعریف می‌کنند — هیچ کدِ اجراییِ تازه‌ای
            // لازم نیست. کلیدهای ستونِ موجود هرگز تغییر نکردند، پس قالب‌های
            // ذخیره‌شده در TblReportTemplate سالم می‌مانند.
            // ═══════════════════════════════════════════════════════════════

            // «پرونده‌ها» با محورهای تازه: تکمیل و آسیب‌پذیری. روی ستون‌های
            // کشِ ایندکس‌دار کار می‌کنند، نه محاسبهٔ زنده.
            cases.Columns.Add(new ReportColumn("CompletionPercent", "درصد تکمیل", "c.CompletionPercent", ReportColumnType.Number));
            cases.Columns.Add(new ReportColumn("CompletionStatus", "وضعیت تکمیل",
                "CASE IFNULL(c.CompletionStatusCode,'') WHEN 'COMPLETE' THEN 'کامل' " +
                "WHEN 'IN_PROGRESS' THEN 'در حال تکمیل' WHEN 'INCOMPLETE' THEN 'ناقص' ELSE 'محاسبه نشده' END",
                ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("VulnerabilityScore", "امتیاز آسیب‌پذیری", "c.VulnerabilityScore", ReportColumnType.Number));
            cases.Columns.Add(new ReportColumn("VulnerabilityBand", "سطح آسیب‌پذیری",
                "CASE IFNULL(c.VulnerabilityBand,'') WHEN 'HIGH' THEN 'پرخطر' " +
                "WHEN 'MEDIUM' THEN 'متوسط' WHEN 'LOW' THEN 'کم‌خطر' ELSE 'محاسبه نشده' END",
                ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("SuspensionReason", "دلیل تعلیق", "c.SuspensionReason", ReportColumnType.Text));

            // ─── خلاصهٔ اسنادِ الزامی در سطحِ پرونده ─────────────────────────
            // منبعِ MissingDocuments یک ردیف به‌ازای هر «دسته» می‌دهد؛ برای
            // پاسخِ «این پرونده چند سند دارد و چند تا کم دارد» یک شمارشِ
            // سطحِ پرونده لازم است، وگرنه باید ردیف‌ها را دستی جمع می‌زدند.
            cases.Columns.Add(new ReportColumn("RequiredDocsTotal", "اسناد الزامی (تعداد)",
                REQUIRED_DOC_COUNT, ReportColumnType.Number));
            cases.Columns.Add(new ReportColumn("RequiredDocsHave", "اسناد الزامی موجود",
                SATISFIED_DOC_COUNT, ReportColumnType.Number));
            cases.Columns.Add(new ReportColumn("RequiredDocsMissing", "اسناد الزامی کم",
                REQUIRED_DOC_COUNT + " - " + SATISFIED_DOC_COUNT, ReportColumnType.Number));
            cases.Columns.Add(new ReportColumn("DocsComplete", "اسناد کامل است؟",
                "CASE WHEN " + SATISFIED_DOC_COUNT + " >= " + REQUIRED_DOC_COUNT +
                " THEN 'بلی' ELSE 'خیر' END", ReportColumnType.Text));
            cases.Columns.Add(new ReportColumn("DocsUploaded", "تعداد کل اسناد آپلودشده",
                "(SELECT COUNT(*) FROM TblDocs d2 WHERE d2.CasID = c.CasID " +
                "AND IFNULL(d2.IsArchived,0) = 0 AND IFNULL(TRIM(d2.DocFilePath), '') <> '')",
                ReportColumnType.Number));

            // ─── پوششِ فورمِ بررسی نسبت به بازدیدها ─────────────────────────
            // قاعدهٔ کاری: فقط بازدیدِ اول فورمِ بررسیِ الزامی دارد (MinCount=1
            // در ماتریس)، ولی حالتِ پیش‌فرضِ موردِ انتظار «یک فورم به‌ازای هر
            // بازدید» است. این دو ستون فاصله را نشان می‌دهند بی‌آنکه چیزی را
            // مسدود کنند — قضاوت با کاربر است.
            cases.Columns.Add(new ReportColumn("FieldVisitCount", "تعداد بازدید میدانی",
                "(SELECT COUNT(*) FROM TblFieldVisit fv WHERE fv.CasID = c.CasID)",
                ReportColumnType.Number));
            cases.Columns.Add(new ReportColumn("SurveyFormCount", "فورم بررسی آپلودشده",
                "(SELECT COUNT(*) FROM TblDocs d3 " +
                "JOIN TblDocumentCategory dc3 ON dc3.DocumentCategoryID = d3.DocumentCategoryID " +
                "WHERE d3.CasID = c.CasID AND dc3.Code = 'INVESTIGATION_FORMS' " +
                "AND IFNULL(d3.IsArchived,0) = 0 AND IFNULL(TRIM(d3.DocFilePath), '') <> '')",
                ReportColumnType.Number));

            // ─── اسنادِ الزامیِ کم ───────────────────────────────────────────
            // هر ردیف = یک دستهٔ الزامیِ کم در یک پرونده؛ پس گزارشِ «اسناد
            // ناقص» مستقیماً از همین منبع ساخته می‌شود.
            var missingDocs = new ReportSource
            {
                Key = "MissingDocuments", DisplayName = "اسناد الزامی ناقص",
                FromClause =
                    "TblCase c " +
                    "JOIN TblRequiredDocument rd ON rd.RequestTypeID = c.RequestTypeID AND rd.IsActive = 1 AND rd.IsMandatory = 1 " +
                    "JOIN TblDocumentCategory dc ON dc.DocumentCategoryID = rd.DocumentCategoryID AND dc.IsActive = 1",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            missingDocs.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            missingDocs.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            missingDocs.Columns.Add(new ReportColumn("Province", "ولایت", "c.Province", ReportColumnType.Text));
            missingDocs.Columns.Add(new ReportColumn("RequestType", "نوع درخواست", "IFNULL(rt.Name, c.RequestType)", ReportColumnType.Text));
            missingDocs.Columns.Add(new ReportColumn("CategoryName", "دسته سند الزامی", "dc.Name", ReportColumnType.Text));
            missingDocs.Columns.Add(new ReportColumn("MinCount", "حداقل لازم", "rd.MinCount", ReportColumnType.Number));
            missingDocs.Columns.Add(new ReportColumn("ExistingCount", "موجود",
                "(SELECT COUNT(*) FROM TblDocs d WHERE d.CasID = c.CasID " +
                "AND d.DocumentCategoryID = dc.DocumentCategoryID AND IFNULL(d.IsArchived,0) = 0)",
                ReportColumnType.Number));
            // ستونِ وضعیت تا کاربر بتواند همین منبع را روی «ناقص» فیلتر کند.
            // بدونِ آن، گزارش هر دستهٔ الزامیِ هر پرونده را ردیف می‌کند و
            // تشخیصِ «کدام‌ها کم است» با چشم انجام می‌شد.
            missingDocs.Columns.Add(new ReportColumn("DocStatus", "وضعیت سند",
                "CASE WHEN (SELECT COUNT(*) FROM TblDocs d WHERE d.CasID = c.CasID " +
                "AND d.DocumentCategoryID = dc.DocumentCategoryID AND IFNULL(d.IsArchived,0) = 0) " +
                ">= rd.MinCount THEN 'کامل' ELSE 'ناقص' END", ReportColumnType.Text));
            // «ثبت شده ولی فایل ندارد» — همان تعریفِ رسمیِ فاز ۵.۵ از سندِ ناقص.
            missingDocs.Columns.Add(new ReportColumn("NoFileCount", "بدون فایل پیوست",
                "(SELECT COUNT(*) FROM TblDocs d WHERE d.CasID = c.CasID " +
                "AND d.DocumentCategoryID = dc.DocumentCategoryID AND IFNULL(d.IsArchived,0) = 0 " +
                "AND IFNULL(TRIM(d.DocFilePath), '') = '')", ReportColumnType.Number));
            missingDocs.FromClause += " LEFT JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID";
            list.Add(missingDocs);

            // ─── تأمین مالی و خیّر ──────────────────────────────────────────
            var funding = new ReportSource
            {
                Key = "Funding", DisplayName = "تأمین مالی و خیّرین",
                FromClause =
                    "TblCaseFunding cf " +
                    "JOIN TblCase c ON c.CasID = cf.CasID " +
                    "JOIN TblFundingSource fs ON fs.FundingSourceID = cf.FundingSourceID " +
                    "LEFT JOIN TblSponsor s ON s.SponsorID = cf.SponsorID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            funding.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            funding.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            funding.Columns.Add(new ReportColumn("Province", "ولایت", "c.Province", ReportColumnType.Text));
            funding.Columns.Add(new ReportColumn("FundingSource", "منبع تأمین مالی", "fs.Name", ReportColumnType.Text));
            funding.Columns.Add(new ReportColumn("FundingCode", "کد منبع", "fs.Code", ReportColumnType.Text));
            funding.Columns.Add(new ReportColumn("SponsorName", "خیّر", "s.Name", ReportColumnType.Text));
            funding.Columns.Add(new ReportColumn("StartDate", "از تاریخ", "cf.StartDate", ReportColumnType.Date));
            funding.Columns.Add(new ReportColumn("EndDate", "تا تاریخ", "cf.EndDate", ReportColumnType.Date));
            funding.Columns.Add(new ReportColumn("FundingStatus", "وضعیت",
                "CASE WHEN cf.IsActive = 1 THEN 'فعال' ELSE 'غیرفعال' END", ReportColumnType.Text));
            list.Add(funding);

            // ─── بازدید میدانی ──────────────────────────────────────────────
            var visits = new ReportSource
            {
                Key = "FieldVisits", DisplayName = "بازدیدهای میدانی",
                FromClause = "TblFieldVisit v JOIN TblCase c ON c.CasID = v.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            visits.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            visits.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            visits.Columns.Add(new ReportColumn("Province", "ولایت", "c.Province", ReportColumnType.Text));
            visits.Columns.Add(new ReportColumn("VisitDate", "تاریخ بازدید", "v.VisitDate", ReportColumnType.Date));
            visits.Columns.Add(new ReportColumn("VisitorName", "بازدیدکننده", "v.VisitorName", ReportColumnType.Text));
            visits.Columns.Add(new ReportColumn("VisitResult", "نتیجه", "v.VisitResult", ReportColumnType.Text));
            visits.Columns.Add(new ReportColumn("Recommendation", "توصیه", "v.Recommendation", ReportColumnType.Text));
            visits.Columns.Add(new ReportColumn("VisitNotes", "یادداشت", "v.Notes", ReportColumnType.Text));
            // Feature 2 — «ولسوالی» برای گزارشِ ولایت/ولسوالی.
            visits.Columns.Add(new ReportColumn("District", "ولسوالی", "c.District", ReportColumnType.Text));
            // Feature 2 — «در انتظار / انجام‌شده».
            //
            // TblFieldVisit ستونِ وضعیت ندارد و افزودنش برای این گزارش یک
            // تغییرِ اسکیما بود که این فاز اجازه‌اش را نمی‌دهد. وضعیت از
            // خودِ داده استنتاج می‌شود: بازدیدی که نتیجه‌اش ثبت شده
            // «انجام‌شده» است و بازدیدی که هنوز نتیجه ندارد «در انتظار».
            // چون یک ستونِ معمولیِ گزارش است، هم فیلتر می‌شود (گزارشِ
            // «بازدیدهای در انتظار» / «انجام‌شده») و هم گروه‌بندی.
            visits.Columns.Add(new ReportColumn("VisitStatus", "وضعیت بازدید",
                "CASE WHEN TRIM(COALESCE(v.VisitResult, '')) = '' THEN 'در انتظار' ELSE 'انجام‌شده' END",
                ReportColumnType.Text));
            // شمارنده برای گزارش‌های تجمیعی (تعداد بازدید در هر گروه).
            visits.Columns.Add(new ReportColumn("VisitCount", "تعداد بازدید", "1", ReportColumnType.Number));
            list.Add(visits);

            // ─── تایم‌لاین پرونده ───────────────────────────────────────────
            var timeline = new ReportSource
            {
                Key = "Timeline", DisplayName = "تایم‌لاین پرونده",
                FromClause = "TblCaseTimeline t JOIN TblCase c ON c.CasID = t.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            timeline.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            timeline.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            timeline.Columns.Add(new ReportColumn("EventDate", "تاریخ", "t.EventDate", ReportColumnType.Date));
            timeline.Columns.Add(new ReportColumn("EventCategory", "دسته رویداد", "t.EventCategoryCode", ReportColumnType.Text));
            timeline.Columns.Add(new ReportColumn("EventType", "نوع رویداد", "t.EventTypeCode", ReportColumnType.Text));
            timeline.Columns.Add(new ReportColumn("EventTitle", "رویداد", "t.Title", ReportColumnType.Text));
            timeline.Columns.Add(new ReportColumn("EventDetails", "شرح", "t.Details", ReportColumnType.Text));
            timeline.Columns.Add(new ReportColumn("EventUser", "کاربر", "t.Username", ReportColumnType.Text));
            list.Add(timeline);

            // ─── امتیاز آسیب‌پذیری (ریزِ سهمِ معیارها) ───────────────────────
            var score = new ReportSource
            {
                Key = "VulnerabilityScore", DisplayName = "امتیاز آسیب‌پذیری",
                FromClause =
                    "TblVulnerabilityScoreDetail vd " +
                    "JOIN TblVulnerabilityScore vs ON vs.ScoreID = vd.ScoreID AND vs.IsCurrent = 1 " +
                    "JOIN TblCase c ON c.CasID = vd.CasID",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            score.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            score.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            score.Columns.Add(new ReportColumn("TotalScore", "امتیاز کل", "vs.Score", ReportColumnType.Number));
            score.Columns.Add(new ReportColumn("Band", "سطح خطر",
                "CASE vs.Band WHEN 'HIGH' THEN 'پرخطر' WHEN 'MEDIUM' THEN 'متوسط' " +
                "WHEN 'LOW' THEN 'کم‌خطر' ELSE '—' END", ReportColumnType.Text));
            score.Columns.Add(new ReportColumn("CriteriaName", "معیار", "vd.CriteriaName", ReportColumnType.Text));
            score.Columns.Add(new ReportColumn("CriteriaScore", "امتیاز معیار", "vd.ScoreValue", ReportColumnType.Number));
            score.Columns.Add(new ReportColumn("CalculatedDate", "تاریخ محاسبه", "vs.CalculatedDate", ReportColumnType.Date));
            list.Add(score);

            // ─── الزام نسخهٔ تحویلی (مورد ۱۳) — وضعیتِ اسناد و تکمیلِ پرونده ──
            //
            // چرا منبعِ تازه لازم بود: همهٔ منابعِ بالا *ردیف‌محور*ند — یک ردیف
            // به‌ازای هر پرونده یا هر سندِ ناقص. آنچه گزارشِ مدیریتی می‌خواهد
            // پاسخِ «چند درصدِ پرونده‌های هر سایت/ولایت/نوع، اسنادشان کامل
            // است» است. با گروه‌بندیِ همین منبع روی ولایت یا سایت یا نوع
            // درخواست، مستقیماً همان جدول به دست می‌آید.
            //
            // همهٔ ستون‌ها از ستون‌های *کش‌شده* و نمایه‌شده می‌آیند
            // (`CompletionPercent`، `CompletionStatusCode`، `VulnerabilityBand`)
            // و هیچ محاسبهٔ زنده‌ای انجام نمی‌شود — همان قاعده‌ای که داشبورد
            // و جستجوی پیشرفته رعایت می‌کنند. شمارشِ اسناد زیرپرس‌وجوی
            // اسکالر است، نه JOIN: جوین ردیفِ پرونده را تکثیر می‌کرد و هر
            // گروه‌بندی و شمارشی را خراب.
            //
            // ⚠ وابستگی: تا وقتی «بازمحاسبهٔ تکمیل و امتیاز» در تبِ نگهداری
            // یک‌بار اجرا نشده باشد، ستون‌های کش برای پرونده‌های قدیمی NULL‌اند
            // و این گزارش برایشان خالی درمی‌آید. این نقصِ گزارش نیست.
            var completionStatus = new ReportSource
            {
                Key = "CompletionStatus", DisplayName = "وضعیت تکمیل و اسناد",
                FromClause = "TblCase c",
                CenterColumn = "c.CenterID",
                ArchivedColumn = "c.IsArchived"
            };
            completionStatus.Columns.Add(new ReportColumn("CaseCode", "کد پرونده", "c.Code", ReportColumnType.Text));
            completionStatus.Columns.Add(new ReportColumn("FormNo", "شماره فرم", "c.FormNo", ReportColumnType.Text));
            completionStatus.Columns.Add(new ReportColumn("HeadFullName", "نام سرپرست", "c.HeadFullName", ReportColumnType.Text));
            completionStatus.Columns.Add(new ReportColumn("Province", "ولایت", "c.Province", ReportColumnType.Text));
            completionStatus.Columns.Add(new ReportColumn("District", "ولسوالی", "c.District", ReportColumnType.Text));
            completionStatus.Columns.Add(new ReportColumn("Site", "سایت", "c.Site", ReportColumnType.Text));
            completionStatus.Columns.Add(new ReportColumn("RequestType", "نوع درخواست", "c.RequestType", ReportColumnType.Text));
            completionStatus.Columns.Add(new ReportColumn("ServiceStatus", "وضعیت خدمات", "c.ServiceStatus", ReportColumnType.Text));
            completionStatus.Columns.Add(new ReportColumn("CompletionPercent", "درصد تکمیل", "c.CompletionPercent", ReportColumnType.Number));
            completionStatus.Columns.Add(new ReportColumn("CompletionStatus", "وضعیت تکمیل",
                "CASE IFNULL(c.CompletionStatusCode,'') " +
                "WHEN 'COMPLETE' THEN 'کامل' WHEN 'IN_PROGRESS' THEN 'در حال تکمیل' " +
                "WHEN 'INCOMPLETE' THEN 'ناقص' ELSE 'محاسبه نشده' END", ReportColumnType.Text));
            completionStatus.Columns.Add(new ReportColumn("VulnerabilityBand", "سطح خطر",
                "CASE IFNULL(c.VulnerabilityBand,'') " +
                "WHEN 'HIGH' THEN 'پرخطر' WHEN 'MEDIUM' THEN 'متوسط' " +
                "WHEN 'LOW' THEN 'کم‌خطر' ELSE 'محاسبه نشده' END", ReportColumnType.Text));
            completionStatus.Columns.Add(new ReportColumn("DocsTotal", "تعداد اسناد",
                "(SELECT COUNT(*) FROM TblDocs d WHERE d.CasID = c.CasID AND IFNULL(d.IsArchived,0) = 0)",
                ReportColumnType.Number));
            completionStatus.Columns.Add(new ReportColumn("DocsVerified", "اسناد تأییدشده",
                "(SELECT COUNT(*) FROM TblDocs d WHERE d.CasID = c.CasID AND IFNULL(d.IsArchived,0) = 0 " +
                "AND IFNULL(d.IsVerified,0) = 1)", ReportColumnType.Number));
            completionStatus.Columns.Add(new ReportColumn("DocsWithoutFile", "سند بدون فایل",
                "(SELECT COUNT(*) FROM TblDocs d WHERE d.CasID = c.CasID AND IFNULL(d.IsArchived,0) = 0 " +
                "AND IFNULL(TRIM(d.DocFilePath),'') = '')", ReportColumnType.Number));
            // شمارِ دسته‌های الزامیِ کم — همان تعریفی که دروازهٔ فعال‌سازی
            // به کار می‌برد، از جمله قاعدهٔ شرطیِ «عکس کارت معلولیت»، تا
            // گزارش و دروازه دربارهٔ یک پرونده حرفِ متفاوت نزنند.
            completionStatus.Columns.Add(new ReportColumn("MissingRequiredDocs", "دستهٔ الزامیِ کم",
                "(SELECT COUNT(*) FROM TblRequiredDocument rd " +
                " JOIN TblDocumentCategory dc ON dc.DocumentCategoryID = rd.DocumentCategoryID AND dc.IsActive = 1 " +
                " WHERE rd.RequestTypeID = c.RequestTypeID AND rd.IsActive = 1 AND rd.IsMandatory = 1 " +
                "   AND NOT (dc.Code = 'DISABILITY_CARD_PHOTO' " +
                "            AND IFNULL(TRIM(c.DisabilityCardStatus),'') <> 'دارد') " +
                "   AND (SELECT COUNT(*) FROM TblDocs d2 " +
                "         WHERE d2.CasID = c.CasID AND d2.DocumentCategoryID = rd.DocumentCategoryID " +
                "           AND IFNULL(d2.IsArchived,0) = 0) < rd.MinCount)",
                ReportColumnType.Number));
            completionStatus.Columns.Add(new ReportColumn("CalculatedAt", "تاریخ محاسبه", "c.CompletionCalculatedAt", ReportColumnType.Date));
            list.Add(completionStatus);

            return list;
        }

        public static string OperatorDisplayName(ReportFilterOperator op)
        {
            switch (op)
            {
                case ReportFilterOperator.Equals: return "برابر است با";
                case ReportFilterOperator.Contains: return "شامل";
                case ReportFilterOperator.GreaterThan: return "بزرگ‌تر از";
                case ReportFilterOperator.LessThan: return "کوچک‌تر از";
                case ReportFilterOperator.GreaterOrEqual: return "بزرگ‌تر یا مساوی";
                case ReportFilterOperator.LessOrEqual: return "کوچک‌تر یا مساوی";
                default: return op.ToString();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // اجرای گزارش: از تعریفِ بالا یک SQL پارامتری‌شده می‌سازد و اجرا می‌کند.
    // نامِ ستون/جدول هرگز از ورودیِ آزادِ کاربر نمی‌آید (فقط از ReportCatalog)،
    // مقدارهای فیلتر همیشه با SQLiteParameter پاس داده می‌شوند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ReportRunner
    {
        public static DataTable Run(ReportDefinition def, DAL.DatabaseHelper db)
        {
            ReportSource source = ReportCatalog.FindSource(def.SourceKey);
            if (source == null)
            {
                if (ProductMode.IsErp && ProductMode.IsCharityReportSource(def.SourceKey))
                    throw new InvalidOperationException("این گزارش در حالت ERP در دسترس نیست.");
                throw new InvalidOperationException("منبع گزارش نامعتبر است.");
            }

            List<ReportColumn> selectedColumns = def.ColumnKeys
                .Select(source.FindColumn)
                .Where(c => c != null)
                .ToList();

            if (selectedColumns.Count == 0)
                throw new InvalidOperationException("حداقل یک ستون را انتخاب کنید.");

            ReportColumn groupColumn = string.IsNullOrEmpty(def.GroupByKey) ? null : source.FindColumn(def.GroupByKey);

            var sql = new System.Text.StringBuilder();
            var parameters = new List<SQLiteParameter>();

            if (groupColumn != null)
            {
                sql.Append("SELECT ").Append(groupColumn.Expression).Append(" AS [").Append(groupColumn.DisplayName).Append("], ")
                   .Append("COUNT(1) AS [تعداد]");
            }
            else
            {
                sql.Append("SELECT ");
                sql.Append(string.Join(", ", selectedColumns.Select(c => c.Expression + " AS [" + c.DisplayName + "]")));
            }

            sql.Append(" FROM ").Append(source.FromClause).Append(" WHERE 1 = 1");

            if (source.ArchivedColumn != null)
                sql.Append(" AND ").Append(source.ArchivedColumn).Append(" = 0");

            if (source.CenterColumn != null)
            {
                sql.Append(" AND (@CID = 0 OR ").Append(source.CenterColumn).Append(" = @CID)");
                parameters.Add(new SQLiteParameter("@CID", SecurityContext.CenterFilterId));
            }

            int paramIndex = 0;
            foreach (ReportFilterDto filterDto in def.Filters ?? new List<ReportFilterDto>())
            {
                ReportColumn col = source.FindColumn(filterDto.ColumnKey);
                if (col == null || string.IsNullOrWhiteSpace(filterDto.Value)) continue;

                ReportFilterOperator op;
                if (!Enum.TryParse(filterDto.Op, out op)) continue;

                string pname = "@p" + (paramIndex++);
                string sqlOp;
                object value = filterDto.Value;

                switch (op)
                {
                    case ReportFilterOperator.Equals: sqlOp = "="; break;
                    case ReportFilterOperator.Contains:
                        sqlOp = "LIKE"; value = "%" + filterDto.Value + "%";
                        break;
                    case ReportFilterOperator.GreaterThan: sqlOp = ">"; break;
                    case ReportFilterOperator.LessThan: sqlOp = "<"; break;
                    case ReportFilterOperator.GreaterOrEqual: sqlOp = ">="; break;
                    case ReportFilterOperator.LessOrEqual: sqlOp = "<="; break;
                    default: continue;
                }

                if (col.Type == ReportColumnType.Number && op != ReportFilterOperator.Contains)
                {
                    decimal numVal;
                    if (!decimal.TryParse(filterDto.Value, out numVal)) continue;
                    value = numVal;
                }

                sql.Append(" AND ").Append(col.Expression).Append(' ').Append(sqlOp).Append(' ').Append(pname);
                parameters.Add(new SQLiteParameter(pname, value));
            }

            if (groupColumn != null)
            {
                sql.Append(" GROUP BY ").Append(groupColumn.Expression);
                sql.Append(" ORDER BY ");
                sql.Append(string.Equals(def.SortColumnKey, "Count", StringComparison.OrdinalIgnoreCase)
                    ? "[تعداد]" : groupColumn.Expression);
                sql.Append(def.SortDescending ? " DESC" : " ASC");
            }
            else
            {
                ReportColumn sortColumn = string.IsNullOrEmpty(def.SortColumnKey) ? null : source.FindColumn(def.SortColumnKey);
                if (sortColumn != null)
                    sql.Append(" ORDER BY ").Append(sortColumn.Expression).Append(def.SortDescending ? " DESC" : " ASC");
            }

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(sql.ToString(), con))
            {
                cmd.Parameters.AddRange(parameters.ToArray());
                con.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    DataTable table = new DataTable();
                    table.Load(reader);
                    return table;
                }
            }
        }
    }
}

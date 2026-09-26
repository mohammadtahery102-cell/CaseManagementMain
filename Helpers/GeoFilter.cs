using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Text;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // GeoFilter — قرارداد واحدِ فیلتر برای «مرکز فرماندهی آماری».
    //
    // آموزش — چرا یک شیء و نه چند پارامتر رشته‌ای: همین فیلتر در دو جای
    // کاملاً متفاوت مصرف می‌شود؛ یکی سرویسِ تجمیع (GeoAnalyticsService) که
    // شمارش می‌کند و یکی FrmCase که با همان شرط‌ها فهرستِ پرونده‌ها را نشان
    // می‌دهد (Drill-Down). اگر شرط‌ها دو جا نوشته می‌شدند، عددِ روی نقشه و
    // تعدادِ ردیف‌های FrmCase دیر یا زود از هم واگرا می‌شدند — همان مشکلی که
    // در داشبورد قبلی با سه رشتهٔ جدا (ولایت/ولسوالی/وضعیت) شروع شده بود.
    //
    // قرارداد مقادیر: رشتهٔ خالی و عددِ صفر یعنی «بدون فیلتر» — دقیقاً همان
    // قراردادی که DashboardMetricsService با centerId == 0 دارد.
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class GeoFilter
    {
        // ─── جغرافیا ────────────────────────────────────────────────────────
        public string Province = "";
        public string District = "";

        // ─── مرکز (سازمانی) ─────────────────────────────────────────────────
        // صفر یعنی «همهٔ مراکزی که کاربر اجازه دارد». مقدار پیش‌فرض از
        // SecurityContext.CenterFilterId گرفته می‌شود تا حصارِ مرکز حفظ شود.
        public int CenterId = 0;

        // ─── طبقه‌بندی ──────────────────────────────────────────────────────
        public int RequestTypeId = 0;      // TblRequestType.RequestTypeID
        public int ServiceStatusId = 0;    // TblServiceStatus.ServiceStatusID

        // ─── ریسک و اقتصاد ─────────────────────────────────────────────────
        public string VulnerabilityBand = "";   // HIGH / MEDIUM / LOW
        public string EconomicPriority = "";    // مقدارِ نمایشیِ فارسی از TblLookup

        // ─── تکمیل پرونده ──────────────────────────────────────────────────
        public int MinCompletionPercent = 0;

        // ─── بازهٔ تاریخ ثبت پرونده (میلادیِ ذخیره‌شده: yyyy-MM-dd) ─────────
        public string DateFrom = "";
        public string DateTo = "";

        // ─── وضعیت حمایت مالی ──────────────────────────────────────────────
        // یکی از SponsorshipCodes پایین؛ خالی یعنی بدون فیلتر.
        public string Sponsorship = "";

        // ─── نوع کمک (TblAssistance.AssistanceType: نقدی/غیرنقدی) ──────────
        public string AssistanceType = "";

        // ─── منبع تأمین مالی ───────────────────────────────────────────────
        public int FundingSourceId = 0;

        // ─── فیلترهای سطحِ عضو (روی TblFamily، با EXISTS) ────────────────────
        public string Gender = "";     // GenderMale / GenderFemale
        public string AgeGroup = "";   // یکی از AgeGroupCodes

        // ─── فعال/غیرفعال (بایگانی) ────────────────────────────────────────
        // پیش‌فرضِ کلِ ماژول: فقط پرونده‌های غیربایگانی — همان قراردادِ
        // DashboardMetricsService. با IncludeArchived می‌توان بایگانی را هم آورد.
        public bool IncludeArchived = false;
        public bool OnlyArchived = false;

        // ─── ثابت‌های دامنه ─────────────────────────────────────────────────
        public const string SponsorHas = "HAS";          // دارای حامی فعال
        public const string SponsorNone = "NONE";        // فاقد حامی
        public const string SponsorPending = "PENDING";  // منبع مالی دارد ولی حامی مشخص نیست
        public const string SponsorEnded = "ENDED";      // حمایت قطع‌شده

        public const string GenderMale = "MALE";
        public const string GenderFemale = "FEMALE";

        public const string AgeChild = "CHILD";        // ۰ تا ۱۲
        public const string AgeTeen = "TEEN";          // ۱۳ تا ۱۷
        public const string AgeYouth = "YOUTH";        // ۱۸ تا ۳۰
        public const string AgeAdult = "ADULT";        // ۳۱ تا ۵۹
        public const string AgeElder = "ELDER";        // ۶۰ به بالا

        // نامِ نمایشیِ فارسی برای هر کد — تنها منبعِ این برچسب‌ها.
        public static readonly string[][] AgeGroups =
        {
            new[] { AgeChild, "کودک",   "0",  "12"  },
            new[] { AgeTeen,  "نوجوان", "13", "17"  },
            new[] { AgeYouth, "جوان",   "18", "30"  },
            new[] { AgeAdult, "بزرگسال","31", "59"  },
            new[] { AgeElder, "سالمند", "60", "200" }
        };

        public static readonly string[][] SponsorshipStates =
        {
            new[] { SponsorHas,     "دارای حامی"     },
            new[] { SponsorNone,    "فاقد حامی"      },
            new[] { SponsorPending, "در انتظار حامی" },
            new[] { SponsorEnded,   "حمایت قطع‌شده"  }
        };

        // پنج سطحِ «اولویت اقتصادی» — همان مقادیری که مالکِ محصول تعیین کرد.
        // دستهٔ TblLookup با همین فهرست seed می‌شود (DatabaseInitializer).
        public const string LookupEconomicPriority = "EconomicPriority";
        public static readonly string[] EconomicPriorities =
        {
            "خیلی شدید", "شدید", "متوسط", "کم", "باثبات"
        };

        // نشانهٔ «هنوز ثبت نشده». رشتهٔ خالی نمی‌تواند این نقش را بازی کند
        // چون در این کلاس خالی یعنی «بدون فیلتر»؛ پس یک مقدارِ نگهبان لازم
        // است که هیچ‌وقت داخلِ دیتابیس دیده نمی‌شود.
        public const string EconomicUnset = "#NONE#";

        // ─── ساخت ───────────────────────────────────────────────────────────
        public GeoFilter() { }

        // فیلترِ پیش‌فرضِ نشستِ کاربر: فقط حصارِ مرکز.
        public static GeoFilter ForCurrentUser()
        {
            return new GeoFilter { CenterId = SecurityContext.CenterFilterId };
        }

        public GeoFilter Clone()
        {
            return (GeoFilter)MemberwiseClone();
        }

        // همان فیلتر، ولی محدود به یک ولایت/ولسوالی — برای Drill-Down روی نقشه.
        public GeoFilter WithRegion(string province, string district)
        {
            GeoFilter copy = Clone();
            copy.Province = province ?? "";
            copy.District = district ?? "";
            return copy;
        }

        // ═══════════════════════════════════════════════════════════════════
        // ساختِ شرطِ SQL
        //
        // آموزش — چرا الگویِ «(@P = '' OR ستون = @P)» به‌جای چسباندنِ رشته:
        // یک متنِ SQL ثابت تولید می‌شود، پس SQLite می‌تواند پلنِ آماده را
        // دوباره استفاده کند و هیچ راهی برای تزریق باقی نمی‌ماند. تمامِ
        // مقادیر پارامتر هستند، حتی وقتی خالی‌اند.
        //
        // پارامترها همیشه *همه* بایند می‌شوند (BindParameters)، حتی اگر شرطشان
        // در متن نیامده باشد؟ نه — برای اینکه SQLite از «پارامترِ بایندشدهٔ
        // بلااستفاده» ایراد نگیرد، شرط‌ها همیشه در متن هستند و مقدارِ خالی
        // خودش یعنی «صرف‌نظر کن». این تنها راهِ همگام ماندنِ متن و پارامترهاست.
        // ═══════════════════════════════════════════════════════════════════

        // alias نامِ مستعارِ TblCase در کوئریِ میزبان است (مثلاً "c").
        // اگر جدول بدونِ alias آمده باشد، رشتهٔ خالی بدهید.
        public string BuildWhere(string alias)
        {
            string a = string.IsNullOrEmpty(alias) ? "" : alias.Trim() + ".";
            StringBuilder sb = new StringBuilder();

            if (OnlyArchived)
                sb.Append(" AND IFNULL(").Append(a).Append("IsArchived, 0) = 1");
            else if (!IncludeArchived)
                sb.Append(" AND IFNULL(").Append(a).Append("IsArchived, 0) = 0");

            sb.Append(@"
  AND (@GfCenter = 0  OR ").Append(a).Append(@"CenterID = @GfCenter)");
            sb.Append(ProvinceScope.Sql(string.IsNullOrEmpty(alias) ? "" : alias.Trim()));
            sb.Append(@"
  AND (@GfProv   = '' OR TRIM(IFNULL(").Append(a).Append(@"Province, '')) = @GfProv)
  AND (@GfDist   = '' OR TRIM(IFNULL(").Append(a).Append(@"District, '')) = @GfDist)
  AND (@GfRt     = 0  OR ").Append(a).Append(@"RequestTypeID   = @GfRt)
  AND (@GfSs     = 0  OR ").Append(a).Append(@"ServiceStatusID = @GfSs)
  AND (@GfBand   = '' OR IFNULL(").Append(a).Append(@"VulnerabilityBand, '') = @GfBand)
  AND (@GfEcon   = ''
       OR (@GfEcon = '").Append(EconomicUnset).Append(@"' AND TRIM(IFNULL(").Append(a).Append(@"EconomicPriority, '')) = '')
       OR TRIM(IFNULL(").Append(a).Append(@"EconomicPriority, '')) = @GfEcon)
  AND (@GfMinCmp = 0  OR IFNULL(").Append(a).Append(@"CompletionPercent, 0) >= @GfMinCmp)
  AND (@GfFrom   = '' OR IFNULL(").Append(a).Append(@"CaseDate, '') >= @GfFrom)
  AND (@GfTo     = '' OR IFNULL(").Append(a).Append(@"CaseDate, '') <= @GfTo)");

            // ── وضعیت حمایت مالی ────────────────────────────────────────────
            // «دارای حامی» یعنی یک ردیفِ فعالِ TblCaseFunding با SponsorID.
            // «در انتظار» یعنی منبعِ مالی ثبت شده ولی حامیِ مشخصی ندارد.
            // «قطع‌شده» یعنی ردیفی هست ولی هیچ‌کدام فعال نیستند.
            sb.Append(@"
  AND (@GfSpons = '' OR (
        (@GfSpons = '").Append(SponsorHas).Append(@"'     AND EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = ").Append(a).Append(@"CasID AND cf.IsActive = 1 AND cf.SponsorID IS NOT NULL))
     OR (@GfSpons = '").Append(SponsorNone).Append(@"'    AND NOT EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = ").Append(a).Append(@"CasID))
     OR (@GfSpons = '").Append(SponsorPending).Append(@"' AND EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = ").Append(a).Append(@"CasID AND cf.IsActive = 1 AND cf.SponsorID IS NULL)
                                     AND NOT EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = ").Append(a).Append(@"CasID AND cf.IsActive = 1 AND cf.SponsorID IS NOT NULL))
     OR (@GfSpons = '").Append(SponsorEnded).Append(@"'   AND EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = ").Append(a).Append(@"CasID)
                                     AND NOT EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = ").Append(a).Append(@"CasID AND cf.IsActive = 1))
  ))");

            // ── منبع تأمین مالی ─────────────────────────────────────────────
            sb.Append(@"
  AND (@GfFund = 0 OR EXISTS (SELECT 1 FROM TblCaseFunding cf WHERE cf.CasID = ").Append(a).Append(@"CasID AND cf.FundingSourceID = @GfFund AND cf.IsActive = 1))");

            // ── نوع کمک ─────────────────────────────────────────────────────
            sb.Append(@"
  AND (@GfAsstType = '' OR EXISTS (SELECT 1 FROM TblAssistance asx WHERE asx.CasID = ").Append(a).Append(@"CasID AND asx.AssistanceType = @GfAsstType))");

            // ── جنسیت و گروه سنیِ عضو ────────────────────────────────────────
            // آموزش — این دو فیلتر «پرونده‌ای که دستِ‌کم یک عضو با این ویژگی
            // دارد» را برمی‌گردانند، نه «پرونده‌ای که خودش این ویژگی را دارد».
            // در UI هم دقیقاً با همین عبارت برچسب می‌خورند تا با رده‌بندیِ
            // پرونده اشتباه نشوند (قاعدهٔ آمارِ عضو در PROJECT_CONTEXT).
            sb.Append(@"
  AND (@GfGender = '' OR EXISTS (SELECT 1 FROM TblFamily fg WHERE fg.CasID = ").Append(a).Append(@"CasID AND ").Append(GenderSqlPredicate("fg")).Append(@"))
  AND (@GfAge    = '' OR EXISTS (SELECT 1 FROM TblFamily fa WHERE fa.CasID = ").Append(a).Append(@"CasID AND ").Append(AgeGroupSqlPredicate("fa")).Append(@"))");

            return sb.ToString();
        }

        // ─── نرمال‌سازیِ جنسیت ───────────────────────────────────────────────
        // آموزش — ستون TblFamily.Gender متنِ آزاد است و در طولِ عمرِ سیستم با
        // واژگانِ مختلفی پر شده: مذکر/مؤنث (دستهٔ Gender در TblLookup) و
        // پسر/دختر (دستهٔ MemberGender). هر دو باید شمرده شوند وگرنه آمارِ
        // جنسیتی نصفِ واقعیت را نشان می‌دهد.
        public static string GenderSqlPredicate(string familyAlias)
        {
            string f = familyAlias + ".Gender";
            return "(" +
                   "(@GfGender = '" + GenderMale + "'   AND TRIM(IFNULL(" + f + ",'')) IN ('مرد','مذکر','پسر','آقا')) OR " +
                   "(@GfGender = '" + GenderFemale + "' AND TRIM(IFNULL(" + f + ",'')) IN ('زن','مؤنث','مونث','دختر','خانم'))" +
                   ")";
        }

        // عبارتِ SQL که جنسیتِ نرمال‌شده را به‌صورت متن برمی‌گرداند (برای GROUP BY).
        public static string GenderNormalizedExpr(string familyAlias)
        {
            string f = "TRIM(IFNULL(" + familyAlias + ".Gender,''))";
            return "CASE WHEN " + f + " IN ('مرد','مذکر','پسر','آقا')            THEN 'مرد' " +
                   "     WHEN " + f + " IN ('زن','مؤنث','مونث','دختر','خانم')   THEN 'زن'  " +
                   "     ELSE 'نامشخص' END";
        }

        // ─── سن از روی سالِ تولد، با تشخیصِ تقویم ────────────────────────────
        // آموزش — خطرِ مستندشده در PROJECT_CONTEXT: چون کالچرِ رشتهٔ UI روی
        // fa-IR است ولی نخ‌های پس‌زمینه نیستند، یک ستونِ تاریخ می‌تواند هم
        // «۱۴۰۵-۰۶-۲۱» داشته باشد و هم «۲۰۲۶-۰۹-۱۲». تشخیص با بازهٔ سال قطعی
        // است چون ۱۳۰۰–۱۵۰۰ و ۱۹۰۰–۲۱۰۰ هیچ هم‌پوشانی ندارند.
        public static string BirthYearGregorianExpr(string familyAlias)
        {
            string y = "CAST(substr(TRIM(IFNULL(" + familyAlias + ".BirthDate,'')), 1, 4) AS INTEGER)";
            return "(CASE WHEN " + y + " BETWEEN 1300 AND 1500 THEN " + y + " + 621 " +
                   "      WHEN " + y + " BETWEEN 1900 AND 2100 THEN " + y + " " +
                   "      ELSE NULL END)";
        }

        public static string AgeExpr(string familyAlias)
        {
            int thisYear = DateTime.Now.Year; // نخِ UI روی fa-IR است؛ .Year خودش شمسی نمی‌شود
            return "(" + thisYear.ToString(CultureInfo.InvariantCulture) + " - " +
                   BirthYearGregorianExpr(familyAlias) + ")";
        }

        public static string AgeGroupSqlPredicate(string familyAlias)
        {
            string age = AgeExpr(familyAlias);
            StringBuilder sb = new StringBuilder("(");
            for (int i = 0; i < AgeGroups.Length; i++)
            {
                if (i > 0) sb.Append(" OR ");
                sb.Append("(@GfAge = '").Append(AgeGroups[i][0]).Append("' AND ")
                  .Append(age).Append(" BETWEEN ").Append(AgeGroups[i][2])
                  .Append(" AND ").Append(AgeGroups[i][3]).Append(")");
            }
            return sb.Append(")").ToString();
        }

        // عبارتِ نامِ گروهِ سنی برای GROUP BY.
        public static string AgeGroupNameExpr(string familyAlias)
        {
            string age = AgeExpr(familyAlias);
            StringBuilder sb = new StringBuilder("CASE ");
            foreach (string[] g in AgeGroups)
                sb.Append("WHEN ").Append(age).Append(" BETWEEN ").Append(g[2])
                  .Append(" AND ").Append(g[3]).Append(" THEN '").Append(g[1]).Append("' ");
            return sb.Append("ELSE 'نامشخص' END").ToString();
        }

        // ═══════════════════════════════════════════════════════════════════
        // بایندِ پارامترها — باید *دقیقاً* با BuildWhere هم‌خوان بماند.
        // ═══════════════════════════════════════════════════════════════════
        public void BindParameters(SQLiteCommand cmd)
        {
            cmd.Parameters.AddWithValue("@GfCenter", CenterId);
            ProvinceScope.Bind(cmd);
            cmd.Parameters.AddWithValue("@GfProv", Trim(Province));
            cmd.Parameters.AddWithValue("@GfDist", Trim(District));
            cmd.Parameters.AddWithValue("@GfRt", RequestTypeId);
            cmd.Parameters.AddWithValue("@GfSs", ServiceStatusId);
            cmd.Parameters.AddWithValue("@GfBand", Trim(VulnerabilityBand));
            cmd.Parameters.AddWithValue("@GfEcon", Trim(EconomicPriority));
            cmd.Parameters.AddWithValue("@GfMinCmp", MinCompletionPercent);
            cmd.Parameters.AddWithValue("@GfFrom", Trim(DateFrom));
            cmd.Parameters.AddWithValue("@GfTo", Trim(DateTo));
            cmd.Parameters.AddWithValue("@GfSpons", Trim(Sponsorship));
            cmd.Parameters.AddWithValue("@GfFund", FundingSourceId);
            cmd.Parameters.AddWithValue("@GfAsstType", Trim(AssistanceType));
            cmd.Parameters.AddWithValue("@GfGender", Trim(Gender));
            cmd.Parameters.AddWithValue("@GfAge", Trim(AgeGroup));
        }

        private static string Trim(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? "" : s.Trim();
        }

        // ─── امضا: کلیدِ کش و مقایسهٔ «آیا فیلتر عوض شد؟» ────────────────────
        public string Signature()
        {
            return string.Join("|", new[]
            {
                Trim(Province), Trim(District),
                CenterId.ToString(CultureInfo.InvariantCulture),
                RequestTypeId.ToString(CultureInfo.InvariantCulture),
                ServiceStatusId.ToString(CultureInfo.InvariantCulture),
                Trim(VulnerabilityBand), Trim(EconomicPriority),
                MinCompletionPercent.ToString(CultureInfo.InvariantCulture),
                Trim(DateFrom), Trim(DateTo), Trim(Sponsorship),
                FundingSourceId.ToString(CultureInfo.InvariantCulture),
                Trim(AssistanceType), Trim(Gender), Trim(AgeGroup),
                IncludeArchived ? "1" : "0", OnlyArchived ? "1" : "0"
            });
        }

        // ─── توضیحِ خوانا برای نوارِ فیلتر و سربرگِ گزارش ─────────────────────
        public string Describe()
        {
            List<string> parts = new List<string>();
            if (Trim(Province).Length > 0) parts.Add("ولایت: " + Province.Trim());
            if (Trim(District).Length > 0) parts.Add("ولسوالی: " + District.Trim());
            if (RequestTypeId > 0) parts.Add("نوع پرونده: " + NameOf(ReferenceDataService.FindRequestTypeById(RequestTypeId), RequestTypeId));
            if (ServiceStatusId > 0) parts.Add("وضعیت خدمات: " + NameOf(ReferenceDataService.FindServiceStatusById(ServiceStatusId), ServiceStatusId));
            if (Trim(VulnerabilityBand).Length > 0) parts.Add("سطح آسیب‌پذیری: " + BandDisplay(VulnerabilityBand));
            if (Trim(EconomicPriority).Length > 0)
                parts.Add("اولویت اقتصادی: " + (EconomicPriority.Trim() == EconomicUnset
                                                ? "ثبت‌نشده" : EconomicPriority.Trim()));
            if (MinCompletionPercent > 0) parts.Add("تکمیل پرونده از " + MinCompletionPercent + "٪ به بالا");
            if (Trim(DateFrom).Length > 0) parts.Add("از تاریخ " + StoredToPersian(DateFrom));
            if (Trim(DateTo).Length > 0) parts.Add("تا تاریخ " + StoredToPersian(DateTo));
            if (Trim(Sponsorship).Length > 0) parts.Add("حمایت: " + SponsorshipDisplay(Sponsorship));
            if (FundingSourceId > 0) parts.Add("منبع مالی #" + FundingSourceId);
            if (Trim(AssistanceType).Length > 0) parts.Add("نوع کمک: " + AssistanceType.Trim());
            if (Trim(Gender).Length > 0) parts.Add("دارای عضو " + (Gender == GenderMale ? "مرد" : "زن"));
            if (Trim(AgeGroup).Length > 0) parts.Add("دارای عضو " + AgeGroupDisplay(AgeGroup));
            if (OnlyArchived) parts.Add("فقط بایگانی");
            else if (IncludeArchived) parts.Add("شاملِ بایگانی");

            return parts.Count == 0 ? "بدون فیلتر (همهٔ پرونده‌های فعال)" : string.Join("، ", parts);
        }

        private static string NameOf(ReferenceOption option, int id)
        {
            return option != null && !string.IsNullOrWhiteSpace(option.Name)
                 ? option.Name
                 : "#" + id.ToString(CultureInfo.InvariantCulture);
        }

        // تاریخِ ذخیره‌شده (میلادی) را برای نمایش به شمسی می‌برد. اگر مقدار
        // خراب بود، خودِ متن برگردانده می‌شود تا کاربر ببیند چه ثبت شده است.
        private static string StoredToPersian(string stored)
        {
            DateTime dt = PersianDateHelper.ParseStoredDate(stored, DateTime.MinValue);
            return dt == DateTime.MinValue ? stored.Trim() : PersianDateHelper.ToPersianDateStringSafe(dt);
        }

        public static string BandDisplay(string code)
        {
            if (string.Equals(code, "HIGH", StringComparison.OrdinalIgnoreCase)) return "پرخطر";
            if (string.Equals(code, "MEDIUM", StringComparison.OrdinalIgnoreCase)) return "متوسط";
            if (string.Equals(code, "LOW", StringComparison.OrdinalIgnoreCase)) return "کم‌خطر";
            return "محاسبه‌نشده";
        }

        public static string SponsorshipDisplay(string code)
        {
            foreach (string[] s in SponsorshipStates)
                if (string.Equals(s[0], code, StringComparison.OrdinalIgnoreCase)) return s[1];
            return code ?? "";
        }

        public static string AgeGroupDisplay(string code)
        {
            foreach (string[] g in AgeGroups)
                if (string.Equals(g[0], code, StringComparison.OrdinalIgnoreCase)) return g[1];
            return code ?? "";
        }
    }
}

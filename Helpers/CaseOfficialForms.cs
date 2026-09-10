using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // فورم‌های رسمیِ پرونده — چرخهٔ «تکمیل در سیستم ← چاپ ← امضا ← اسکن ←
    // ثبت در اسنادِ پرونده».
    //
    // آموزش — چرا در زمانِ *ساختِ* فورم هیچ ردیفی در TblDocs درج نمی‌شود:
    // طبقِ قاعدهٔ فاز ۵.۵، ردیفِ سندِ بدونِ فایل «ناقص» و ردیفِ سندِ دارای فایل
    // «کامل» شمرده می‌شود. اگر فایلِ تازه‌چاپ‌شده و امضانشده همان‌جا ثبت شود،
    // شمارشگرِ اسنادِ اجباری آن را «کامل» می‌بیند و گیتِ فعال‌سازی بی‌اثر
    // می‌گردد. پس تا وقتی نسخهٔ امضاشده ضمیمه نشده، پرونده به‌درستی
    // «سندِ اجباری ندارد» می‌ماند — بدونِ یک خط کدِ اضافه.
    //
    // آموزش — چرا CaseFormTokens جدا است: این کلاس فقط «کدام فورم، کدام
    // دسته سند، کدام خانه‌ها دستی» را می‌داند؛ خواندنِ دیتابیس و ساختنِ
    // نگاشتِ توکن‌ها کارِ CaseFormTokens است و دیالوگ و خروجیِ Word/PDF کارِ
    // FrmOfficialForms و DocxFormExport. هیچ‌کدام از این سه چیزی از آن دوتای
    // دیگر نمی‌دانند.
    //
    // آموزش — چرا «وکالت موقت» و «نامهٔ انتقالی» هم به همین رجیستری آمدند
    // (درخواستِ کاربر: «همه فورم‌ها یک‌جا»): تا امروز سه مسیرِ متفاوت برای
    // یک کار وجود داشت — منویِ FrmDocs، دکمهٔ وکالتِ FrmCase، و دکمهٔ نامهٔ
    // انتقالیِ FrmCase. یک رجیستری یعنی یک جای واحد برای افزودنِ فورمِ بعدی.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class CaseFormDef
    {
        public string Key;                   // شناسهٔ یکتا (برای پیش‌انتخاب در دیالوگ)
        public string Title;                 // عنوانِ دیالوگ و نامِ پیشنهادیِ فایل
        public string Description;           // یک خط توضیح در فهرست
        public string TemplateFile;          // نامِ فایل در Templates\Forms
        public string DocumentCategoryCode;  // دستهٔ سند برای نسخهٔ امضاشده
        public string DocType;               // متنِ ستون DocType در TblDocs
        public string[] ManualTokens;        // خانه‌هایی که پشتوانهٔ دیتابیسی ندارند
        public string[] RequiredTokens;      // خانه‌هایی که خالی‌ماندنشان مجاز نیست
        public string[] MultilineTokens;     // خانه‌هایی که کادرِ چندخطی می‌خواهند
        public Dictionary<string, string[]> Choices;   // خانه‌های فهرستی

        // فورمی که فرمِ اختصاصیِ خودش را دارد (نامهٔ انتقالی). اگر پر باشد،
        // دیالوگِ عمومی کنار می‌رود و همین صدا زده می‌شود.
        public Action<IWin32Window, int> CustomOpen;

        public string[] ManualOrEmpty { get { return ManualTokens ?? new string[0]; } }

        public bool IsAvailable
        {
            get
            {
                return CustomOpen != null ||
                       (!string.IsNullOrEmpty(TemplateFile) && DocxFormExport.TemplateExists(TemplateFile));
            }
        }
    }

    public static class CaseOfficialForms
    {
        public const string CatRequestForms       = "REQUEST_FORMS";
        public const string CatInvestigationForms = "INVESTIGATION_FORMS";
        public const string CatIdentity           = "IDENTITY";

        // ── تعریفِ فورم‌ها ──────────────────────────────────────────────────
        private static readonly CaseFormDef OrphanRequest = new CaseFormDef
        {
            Key = "F1",
            Title = "فورم ۱ — درخواست ایتام",
            Description = "فورمِ رسمیِ پذیرشِ پروندهٔ ایتام؛ مشخصاتِ پدرِ متوفی و اعضای خانواده از پرونده پر می‌شود.",
            TemplateFile = DocxFormExport.TplOrphanRequest,
            DocumentCategoryCode = CatRequestForms,
            DocType = "فورم درخواست ایتام",
            ManualTokens = new string[0],
        };

        private static readonly CaseFormDef NeedyRequest = new CaseFormDef
        {
            Key = "F2",
            Title = "فورم ۲ — درخواست نیازمندان",
            Description = "فورمِ پذیرشِ معلول/مهاجر/کهن‌سال و سایرِ نیازمندان.",
            TemplateFile = DocxFormExport.TplNeedyRequest,
            DocumentCategoryCode = CatRequestForms,
            DocType = "فورم درخواست نیازمندان",
            ManualTokens = new[] { "RequestDesc" },
            MultilineTokens = new[] { "RequestDesc" },
        };

        private static readonly CaseFormDef TreatmentRequest = new CaseFormDef
        {
            Key = "F3",
            Title = "فورم ۳ — درخواست درمان",
            Description = "درخواستِ کمکِ درمانی؛ سوابقِ طبی دستی وارد می‌شود.",
            TemplateFile = DocxFormExport.TplTreatmentRequest,
            DocumentCategoryCode = CatRequestForms,
            DocType = "فورم درخواست درمان",
            ManualTokens = new[]
            {
                "DiseaseType", "DrugAllergy", "HeartHistory", "BloodGroup",
                "SurgeryHistory", "CurrentCondition", "CurrentMeds",
                "TreatmentNeeded", "RequestDesc",
            },
            MultilineTokens = new[] { "CurrentCondition", "TreatmentNeeded", "RequestDesc" },
            Choices = new Dictionary<string, string[]>
            {
                { "BloodGroup", new[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" } },
            },
        };

        private static readonly CaseFormDef Survey = new CaseFormDef
        {
            Key = "F4",
            Title = "فورم ۴ — تحقیق و بررسی",
            Description = "نتیجهٔ بازدیدِ میدانی و بررسیِ وضعیتِ اقتصادیِ خانواده.",
            TemplateFile = DocxFormExport.TplSurvey,
            DocumentCategoryCode = CatInvestigationForms,
            DocType = "فورم تحقیق و بررسی",
            ManualTokens = new[]
            {
                "VisitNo", "Surveyors", "SurveyManager", "SurveyDate", "SurveyResult",
            },
            RequiredTokens = new[] { "Surveyors", "SurveyDate" },
            MultilineTokens = new[] { "SurveyResult" },
        };

        private static readonly CaseFormDef TreatmentFile = new CaseFormDef
        {
            Key = "F7",
            Title = "فورم ۷ — پرونده بخش درمان",
            Description = "پروندهٔ داخلیِ بخشِ درمان؛ نظرِ داکترِ معالج دستی ثبت می‌شود.",
            TemplateFile = DocxFormExport.TplTreatmentFile,
            DocumentCategoryCode = CatRequestForms,
            DocType = "فورم پرونده بخش درمان",
            ManualTokens = new[]
            {
                "DoctorName", "DoctorSpecialty", "DoctorDate", "DoctorOpinion",
            },
            MultilineTokens = new[] { "DoctorOpinion" },
        };

        // ── وکالت موقت — تا امروز فقط از دکمهٔ FrmCase باز می‌شد ─────────────
        private static readonly CaseFormDef GuardianProxy = new CaseFormDef
        {
            Key = "PROXY",
            Title = "ورقهٔ وکالت موقت سرپرستی",
            Description = "سرپرستِ پرونده، شخصِ دیگری را برای مدتی معین وکیلِ دریافتِ شهریه می‌کند.",
            TemplateFile = DocxFormExport.TplGuardianProxy,
            DocumentCategoryCode = CatIdentity,
            DocType = "وکالت موقت",
            ManualTokens = new[]
            {
                "ProxyName", "ProxyFather", "ProxyTazkira",
                "Reason", "FromDate", "ToDate", "IssueDate",
            },
            RequiredTokens = new[] { "ProxyName", "ProxyTazkira", "ToDate" },
            Choices = new Dictionary<string, string[]>
            {
                { "Reason", new[] { "سفر", "بیماری", "کهولت سن", "غیبت موقت", "سایر" } },
            },
        };

        // ── نامهٔ انتقالی — فرمِ اختصاصیِ خودش را دارد ────────────────────────
        private static readonly CaseFormDef TransferLetter = new CaseFormDef
        {
            Key = "TRANSFER",
            Title = "نامهٔ انتقالی پرونده",
            Description = "انتقالِ پرونده به ولایتِ دیگر؛ در فرمِ اختصاصیِ خودش تکمیل می‌شود.",
            TemplateFile = DocxFormExport.TplTransferLetter,
            DocumentCategoryCode = CatIdentity,
            DocType = "نامه انتقالی",
            ManualTokens = new string[0],
            CustomOpen = delegate (IWin32Window owner, int caseId)
            {
                using (var frm = new FrmTransferLetter(caseId))
                    frm.ShowDialog(owner);
            },
        };

        // ── کدامیک برای این پرونده؟ ─────────────────────────────────────────
        // فورمِ درخواست بر اساسِ نوعِ پرونده انتخاب می‌شود؛ بقیه برای همهٔ
        // انواع در دسترس‌اند. «وکالت موقت» فقط جایی معنی دارد که سرپرست
        // شهریهٔ کسی را می‌گیرد — یعنی سه نوعِ کودک‌محور — ولی چون هیچ قاعدهٔ
        // مصوبی آن را محدود نکرده، برای همه باز است و تصمیم با کاربر می‌ماند.
        public static List<CaseFormDef> Available(string requestTypeCode)
        {
            var list = new List<CaseFormDef>();
            list.Add(string.Equals(requestTypeCode, "ORPHAN", StringComparison.OrdinalIgnoreCase)
                     ? OrphanRequest : NeedyRequest);
            list.Add(Survey);
            list.Add(TreatmentRequest);
            list.Add(TreatmentFile);
            list.Add(GuardianProxy);
            list.Add(TransferLetter);
            return list;
        }

        public static CaseFormDef ByKey(string key)
        {
            foreach (CaseFormDef d in Available(""))
                if (string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }

        public static string RequestTypeCodeOf(DatabaseHelper db, int caseId)
        {
            try
            {
                object v = db.ExecuteScalar(@"
SELECT rt.Code FROM TblCase c
JOIN TblRequestType rt ON rt.RequestTypeID = c.RequestTypeID
WHERE c.CasID = @id", new SQLiteParameter("@id", caseId));
                return v == null || v == DBNull.Value ? "" : Convert.ToString(v);
            }
            catch { return ""; }
        }

        // ── نقطهٔ ورودِ واحد ─────────────────────────────────────────────────
        // نامِ متد و امضایش دست‌نخورده ماند (FrmDocs از قبل همین را صدا
        // می‌زند)، ولی به‌جای منویِ کرکره‌ای، مرکزِ فورم‌های رسمی باز می‌شود.
        public static void ShowMenu(Control anchor, DatabaseHelper db,
                                    int caseId, string caseCode, Action onAttached)
        {
            ShowCenter(anchor, db, caseId, caseCode, onAttached, null);
        }

        public static void ShowCenter(IWin32Window owner, DatabaseHelper db,
                                      int caseId, string caseCode, Action onAttached,
                                      string preselectKey)
        {
            if (caseId <= 0)
            {
                UiTheme.ShowWarning(owner, "اول یک پرونده را انتخاب کنید.");
                return;
            }

            using (var frm = new FrmOfficialForms(db, caseId, caseCode, preselectKey))
            {
                frm.OnDocumentAttached = onAttached;
                frm.ShowDialog(owner);
            }
        }

        // ── برچسبِ فارسیِ خانه‌های دستی ───────────────────────────────────────
        public static string ManualCaption(string token)
        {
            switch (token)
            {
                case "RequestDesc":      return "شرح درخواست";
                case "DiseaseType":      return "نوع بیماری";
                case "DrugAllergy":      return "حساسیت دارویی و غذایی";
                case "HeartHistory":     return "سوابق بیماری قلبی";
                case "BloodGroup":       return "گروه خونی";
                case "SurgeryHistory":   return "سوابق عمل جراحی";
                case "CurrentCondition": return "وضعیت فعلی";
                case "CurrentMeds":      return "داروهای در حال مصرف";
                case "TreatmentNeeded":  return "نوع درمان مورد نیاز";
                case "DoctorName":       return "نام داکتر";
                case "DoctorSpecialty":  return "تخصص";
                case "DoctorDate":       return "تاریخ";
                case "DoctorOpinion":    return "نظر نهایی داکتر معالج";
                case "VisitNo":          return "نوبت بازدید";
                case "Surveyors":        return "اسامی بررسی کننده‌ها";
                case "SurveyManager":    return "مسئول بررسی";
                case "SurveyDate":       return "تاریخ بررسی";
                case "SurveyResult":     return "شرح نتیجه تحقیق و بررسی";
                case "ProxyName":        return "نام وکیل موقت";
                case "ProxyFather":      return "نام پدر وکیل";
                case "ProxyTazkira":     return "شماره تذکره وکیل";
                case "Reason":           return "دلیل وکالت";
                case "FromDate":         return "از تاریخ";
                case "ToDate":           return "الی تاریخ (انقضا)";
                case "IssueDate":        return "تاریخ تنظیم";
                default:                 return token;
            }
        }

        // خلاصهٔ «این فورم از پرونده چه می‌خواند» — در پنلِ راستِ دیالوگ
        // به‌عنوان اطلاعاتِ خودکار نشان داده می‌شود تا کاربر ببیند چه چیزی
        // بدونِ تایپ پر می‌شود.
        public static List<KeyValuePair<string, string>> AutoSummary(
            IDictionary<string, string> tokens, CaseFormDef def)
        {
            var list = new List<KeyValuePair<string, string>>();
            string[] keys;

            if (def != null && def.Key == "PROXY")
                keys = new[] { "GuardianName", "GuardianFather", "GuardianTazkira", "Code", "OrphanCount" };
            else
                keys = new[] { "Code", "CaseNo", "HeadName", "HeadFather", "HeadTazkira",
                               "RequestTypeName", "Province", "District", "Dependents" };

            foreach (string k in keys)
            {
                string v;
                if (!tokens.TryGetValue(k, out v) || string.IsNullOrWhiteSpace(v)) continue;
                list.Add(new KeyValuePair<string, string>(AutoCaption(k), v.Trim()));
            }
            return list;
        }

        private static string AutoCaption(string token)
        {
            switch (token)
            {
                case "Code":            return "کد پرونده";
                case "CaseNo":          return "شماره فرم";
                case "HeadName":        return "نام سرپرست";
                case "HeadFather":      return "نام پدر";
                case "HeadTazkira":     return "شماره تذکره";
                case "RequestTypeName": return "نوع درخواست";
                case "Province":        return "ولایت";
                case "District":        return "ناحیه/ولسوالی";
                case "Dependents":      return "تعداد اعضا";
                case "GuardianName":    return "سرپرست";
                case "GuardianFather":  return "نام پدر سرپرست";
                case "GuardianTazkira": return "تذکره سرپرست";
                case "OrphanCount":     return "تعداد ایتام";
                default:                return token;
            }
        }

        // ── وضعیتِ «نسخهٔ امضاشده ضمیمه شده یا نه» ───────────────────────────
        // برای هر فورم، آخرین سندی که با همان DocType در پرونده ثبت شده.
        public static string AttachedSummary(DatabaseHelper db, int caseId, string docType)
        {
            if (caseId <= 0 || string.IsNullOrWhiteSpace(docType)) return "";
            try
            {
                object v = db.ExecuteScalar(@"
SELECT COUNT(*) FROM TblDocs
WHERE CasID = @id AND DocType = @type AND IFNULL(IsArchived, 0) = 0",
                    new SQLiteParameter("@id", caseId),
                    new SQLiteParameter("@type", docType));
                int n = (v == null || v == DBNull.Value) ? 0 : Convert.ToInt32(v);
                return n == 0 ? "" : ReportDoc.Fa(n) + " نسخه ضمیمه";
            }
            catch { return ""; }
        }

        // ── مسیرِ قدیمیِ بازکردنِ یک فورم ────────────────────────────────────
        // نگه داشته شد تا اگر جایی مستقیم صدا می‌زند نشکند؛ اکنون هم به
        // همان دیالوگِ تازه می‌رسد.
        public static void Open(IWin32Window owner, DatabaseHelper db, int caseId,
                                string caseCode, CaseFormDef def, Action onAttached)
        {
            ShowCenter(owner, db, caseId, caseCode, onAttached, def == null ? null : def.Key);
        }
    }
}

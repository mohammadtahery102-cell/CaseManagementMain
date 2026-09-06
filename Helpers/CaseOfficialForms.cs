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
    // FrmDocxForm و DocxFormExport. هیچ‌کدام از این سه چیزی از آن دوتای دیگر
    // نمی‌دانند.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class CaseFormDef
    {
        public string Title;                 // عنوانِ دیالوگ و نامِ پیشنهادیِ فایل
        public string TemplateFile;          // نامِ فایل در Templates\Forms
        public string DocumentCategoryCode;  // دستهٔ سند برای نسخهٔ امضاشده
        public string DocType;               // متنِ ستون DocType در TblDocs
        public string[] ManualTokens;        // خانه‌هایی که پشتوانهٔ دیتابیسی ندارند
    }

    public static class CaseOfficialForms
    {
        public const string CatRequestForms       = "REQUEST_FORMS";
        public const string CatInvestigationForms = "INVESTIGATION_FORMS";

        // ── تعریفِ پنج فورم ──────────────────────────────────────────────────
        private static readonly CaseFormDef OrphanRequest = new CaseFormDef
        {
            Title = "فورم ۱ — درخواست ایتام",
            TemplateFile = DocxFormExport.TplOrphanRequest,
            DocumentCategoryCode = CatRequestForms,
            DocType = "فورم درخواست ایتام",
            ManualTokens = new string[0],
        };

        private static readonly CaseFormDef NeedyRequest = new CaseFormDef
        {
            Title = "فورم ۲ — درخواست نیازمندان",
            TemplateFile = DocxFormExport.TplNeedyRequest,
            DocumentCategoryCode = CatRequestForms,
            DocType = "فورم درخواست نیازمندان",
            ManualTokens = new[] { "RequestDesc" },
        };

        private static readonly CaseFormDef TreatmentRequest = new CaseFormDef
        {
            Title = "فورم ۳ — درخواست درمان",
            TemplateFile = DocxFormExport.TplTreatmentRequest,
            DocumentCategoryCode = CatRequestForms,
            DocType = "فورم درخواست درمان",
            ManualTokens = new[]
            {
                "DiseaseType", "DrugAllergy", "HeartHistory", "BloodGroup",
                "SurgeryHistory", "CurrentCondition", "CurrentMeds",
                "TreatmentNeeded", "RequestDesc",
            },
        };

        private static readonly CaseFormDef Survey = new CaseFormDef
        {
            Title = "فورم ۴ — تحقیق و بررسی",
            TemplateFile = DocxFormExport.TplSurvey,
            DocumentCategoryCode = CatInvestigationForms,
            DocType = "فورم تحقیق و بررسی",
            ManualTokens = new[]
            {
                "VisitNo", "Surveyors", "SurveyManager", "SurveyDate", "SurveyResult",
            },
        };

        private static readonly CaseFormDef TreatmentFile = new CaseFormDef
        {
            Title = "فورم ۷ — پرونده بخش درمان",
            TemplateFile = DocxFormExport.TplTreatmentFile,
            DocumentCategoryCode = CatRequestForms,
            DocType = "فورم پرونده بخش درمان",
            ManualTokens = new[]
            {
                "DoctorName", "DoctorSpecialty", "DoctorDate", "DoctorOpinion",
            },
        };

        // ── کدامیک برای این پرونده؟ ─────────────────────────────────────────
        // فورمِ درخواست بر اساسِ نوعِ پرونده انتخاب می‌شود؛ فورمِ بررسی برای
        // همهٔ انواع هست. دو فورمِ درمانی به هیچ نوعِ پرونده گره نخورده‌اند
        // چون در TblRequestType نوعی به نامِ «درمان» وجود ندارد — پس برای همهٔ
        // پرونده‌ها به‌عنوانِ فورمِ اختیاری در دسترس‌اند.
        public static List<CaseFormDef> Available(string requestTypeCode)
        {
            var list = new List<CaseFormDef>();
            list.Add(string.Equals(requestTypeCode, "ORPHAN", StringComparison.OrdinalIgnoreCase)
                     ? OrphanRequest : NeedyRequest);
            list.Add(Survey);
            list.Add(TreatmentRequest);
            list.Add(TreatmentFile);
            return list;
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

        // ── منویِ انتخابِ فورم ───────────────────────────────────────────────
        public static void ShowMenu(Control anchor, DatabaseHelper db,
                                    int caseId, string caseCode, Action onAttached)
        {
            if (caseId <= 0)
            {
                UiTheme.ShowWarning(anchor, "اول یک پرونده را انتخاب کنید.");
                return;
            }

            var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
            foreach (CaseFormDef def in Available(RequestTypeCodeOf(db, caseId)))
            {
                CaseFormDef captured = def;
                var item = new ToolStripMenuItem(def.Title);
                if (!DocxFormExport.TemplateExists(def.TemplateFile))
                {
                    item.Enabled = false;
                    item.Text += "   (قالب پیدا نشد)";
                }
                item.Click += delegate { Open(anchor, db, caseId, caseCode, captured, onAttached); };
                menu.Items.Add(item);
            }
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        // ── بازکردنِ دیالوگِ یک فورم ─────────────────────────────────────────
        public static void Open(IWin32Window owner, DatabaseHelper db, int caseId,
                                string caseCode, CaseFormDef def, Action onAttached)
        {
            Dictionary<string, string> tokens;
            try
            {
                tokens = CaseFormTokens.Build(db, caseId);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(owner, "خطا در خواندن اطلاعات پرونده: " + ex.Message);
                return;
            }

            // خانه‌های دستی از فهرستِ خودکار بیرون کشیده می‌شوند تا دوبار
            // نوشته نشوند؛ مقدارِ فعلی‌شان (اگر باشد) پیش‌فرضِ کادر می‌گردد.
            var fields = new List<FrmDocxForm.FieldDef>();
            foreach (string token in def.ManualTokens)
            {
                string current;
                tokens.TryGetValue(token, out current);
                fields.Add(FrmDocxForm.FieldDef.Text(ManualCaption(token), token, current ?? ""));
            }

            using (var frm = new FrmDocxForm(def.Title, def.TemplateFile, fields,
                                             FileHelper.CleanName(caseCode) + " - " + def.Title))
            {
                foreach (var pair in tokens)
                {
                    if (Array.IndexOf(def.ManualTokens, pair.Key) >= 0) continue;
                    frm.Hidden(pair.Key, pair.Value);
                }

                frm.ExtraButtonText = "ضمیمهٔ نسخهٔ امضاشده";
                frm.OnExtraButton = delegate(string lastPath)
                {
                    string startFolder = "";
                    try
                    {
                        startFolder = string.IsNullOrWhiteSpace(lastPath)
                            ? FileHelper.GetSectionFolder(caseCode, FileHelper.SectionDocs)
                            : Path.GetDirectoryName(lastPath);
                    }
                    catch { }

                    bool saved = FrmDocxForm.AttachToCase(owner, db, caseId, caseCode,
                        def.DocType, def.Title + " — نسخهٔ امضاشده", startFolder,
                        def.DocumentCategoryCode);

                    if (saved && onAttached != null) onAttached();
                };

                frm.ShowDialog(owner);
            }
        }

        // برچسبِ فارسیِ خانه‌های دستی. جایی جز همین دیالوگ دیده نمی‌شود، پس
        // یک نگاشتِ ساده کافی است.
        private static string ManualCaption(string token)
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
                default:                 return token;
            }
        }
    }
}

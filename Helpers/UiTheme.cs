using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // UiTheme — منبع واحد رنگ، فونت و ظاهر برای همه فرم‌ها.
    // هدف: یکسان‌سازی ظاهر (رنگ اداری/مدرن، فونت فارسی استاندارد، دکمه‌های
    // هم‌شکل با Hover، ورودی‌های یکسان) بدون تغییر در منطق یا قابلیت‌های فرم‌ها.
    // ─────────────────────────────────────────────────────────────────────────
    public static class UiTheme
    {
        // آموزش — رنگ سازمانی: این چهار رنگ (بر خلاف بقیه) readonly نیستند تا
        // ApplyOrgColor بتواند آن‌ها را بر اساس تنظیمات «رنگ نرم‌افزار» بازسازی کند.
        public static Color PrimaryDark  = ColorTranslator.FromHtml("#1B3A5C");
        public static Color Primary      = ColorTranslator.FromHtml("#2C5A85");
        public static Color PrimaryLight = ColorTranslator.FromHtml("#3E7CB1");
        public static Color HoverTint    = ColorTranslator.FromHtml("#EAF1F7");

        // آموزش — تب «ظاهر نرم‌افزار» (بخش کنترل سنتر): این رنگ‌ها قبلاً
        // readonly بودند (فقط رنگ اصلی/سازمانی قابل تغییر بود). حالا mutable
        // شده‌اند تا ApplyFullPalette بتواند از تنظیمات بازسازی‌شان کند؛ مقدار
        // پیش‌فرض همان hex قبلی است، پس بدون تنظیم دستی هیچ تغییری در ظاهر
        // برنامه دیده نمی‌شود.
        public static Color Background   = ColorTranslator.FromHtml("#F2F4F7");
        public static Color CardBack     = Color.White;
        public static Color Success      = ColorTranslator.FromHtml("#2E8B57");
        public static Color SuccessLight = ColorTranslator.FromHtml("#EAF7EF");
        public static Color Danger       = ColorTranslator.FromHtml("#C0392B");
        public static Color DangerLight  = ColorTranslator.FromHtml("#FBEAE9");
        public static Color Warning      = ColorTranslator.FromHtml("#B8860B");
        public static Color WarningLight = ColorTranslator.FromHtml("#FCF3DD");
        public static Color TextDark     = ColorTranslator.FromHtml("#25313F");
        public static Color TextMuted    = ColorTranslator.FromHtml("#68758A");
        public static Color Border       = ColorTranslator.FromHtml("#D6DCE5");

        // ─── رنگ سازمانی: از تنظیمات نرم‌افزار (بخش تنظیمات عمومی) ───────────
        // یک رنگ پایه می‌گیرد و کل خانواده رنگ اصلی (تیره/روشن/Hover) را از آن
        // می‌سازد تا همه فرم‌ها (که از UiTheme.Primary/PrimaryDark استفاده می‌کنند)
        // به‌طور خودکار هم‌رنگ سازمان شوند.
        public static void ApplyOrgColor(Color baseColor)
        {
            Primary      = baseColor;
            PrimaryDark  = ControlPaint.Dark(baseColor, 0.25f);
            PrimaryLight = ControlPaint.Light(baseColor, 0.20f);
            HoverTint    = ControlPaint.Light(baseColor, 0.85f);
        }

        // ─── تب ظاهر نرم‌افزار: رنگ‌های ثانویه هم قابل شخصی‌سازی ────────────────
        // فقط رنگ‌هایی که واقعاً در تنظیمات مقداردهی شده‌اند بازنویسی می‌شوند؛
        // در غیر این صورت مقدار پیش‌فرض (hex بالا) دست‌نخورده می‌ماند.
        public static void ApplyFullPalette(
            Color? success, Color? danger, Color? warning, Color? textDark, Color? textMuted, Color? border)
        {
            if (success.HasValue)
            {
                Success = success.Value;
                SuccessLight = ControlPaint.Light(success.Value, 0.85f);
            }
            if (danger.HasValue)
            {
                Danger = danger.Value;
                DangerLight = ControlPaint.Light(danger.Value, 0.85f);
            }
            if (warning.HasValue)
            {
                Warning = warning.Value;
                WarningLight = ControlPaint.Light(warning.Value, 0.85f);
            }
            if (textDark.HasValue) TextDark = textDark.Value;
            if (textMuted.HasValue) TextMuted = textMuted.Value;
            if (border.HasValue) Border = border.Value;
        }

        // ─── نمایش فارسی نقش کاربر (فقط نمایشی؛ مقدار ذخیره‌شده در دیتابیس
        // و منطق احراز هویت در SecurityContext همچنان انگلیسی می‌ماند) ──────
        public static string RoleDisplay(string role)
        {
            switch ((role ?? "").Trim())
            {
                case "SuperAdmin": return "مدیر کل";
                case "Admin":      return "مدیر سیستم";
                case "Operator":   return "کاربر عملیاتی";
                case "Viewer":     return "ناظر";
                default:           return role ?? "";
            }
        }

        // ─── فونت با Fallback خودکار ────────────────────────────────────────
        // آموزش: به‌جای وابستگی به یک فونت خاص (که ممکن است روی سیستم کاربر
        // نصب نباشد و ظاهر برنامه را به‌هم بریزد)، این بخش:
        //   ۱. اول بررسی می‌کند آیا فایل فونت اختصاصی (مثلاً Vazirmatn.ttf)
        //      در پوشه Fonts\ کنار برنامه هست؛ اگر بود بدون نیاز به نصب لود می‌شود.
        //   ۲. در غیر این صورت از بین فونت‌های نصب‌شده روی سیستم، اولین گزینه
        //      از یک لیست ترجیحی را انتخاب می‌کند. Segoe UI و Tahoma روی هر
        //      نصب ویندوزی از قبل موجودند، پس نتیجه همیشه معتبر و یکسان است.
        private static readonly string[] PreferredInstalledFonts =
        {
            "Vazirmatn", "IRANSansX", "IRANSans", "Segoe UI", "Tahoma"
        };

        private static readonly object FontLock = new object();
        private static FontFamily _resolvedFamily;
        private static PrivateFontCollection _privateFonts;

        private static FontFamily ResolvedFamily
        {
            get
            {
                if (_resolvedFamily == null)
                {
                    lock (FontLock)
                    {
                        if (_resolvedFamily == null)
                            _resolvedFamily = ResolveFontFamily();
                    }
                }
                return _resolvedFamily;
            }
        }

        private static FontFamily ResolveFontFamily()
        {
            FontFamily bundled = TryLoadBundledFont();
            if (bundled != null)
                return bundled;

            foreach (string candidate in PreferredInstalledFonts)
            {
                try
                {
                    return new FontFamily(candidate);
                }
                catch (ArgumentException)
                {
                    // این فونت روی سیستم نصب نیست؛ گزینه بعدی امتحان می‌شود.
                }
            }

            return FontFamily.GenericSansSerif;
        }

        private static FontFamily TryLoadBundledFont()
        {
            try
            {
                string fontsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
                if (!Directory.Exists(fontsFolder))
                    return null;

                string[] files = Directory.GetFiles(fontsFolder, "*.ttf");
                if (files.Length == 0)
                    return null;

                _privateFonts = new PrivateFontCollection();
                foreach (string file in files)
                    _privateFonts.AddFontFile(file);

                return _privateFonts.Families.Length > 0 ? _privateFonts.Families[0] : null;
            }
            catch
            {
                return null;
            }
        }

        // ─── اندازه‌های استاندارد فونت — نه خیلی کوچک نه خیلی بزرگ ──────────
        public const float SizeSmall  = 9.5F;
        public const float SizeBody   = 10.5F;
        public const float SizeMedium = 11.5F;
        public const float SizeLarge  = 13F;
        public const float SizeTitle  = 16F;

        // ضریب اندازه فونت — تب «ظاهر نرم‌افزار». پیش‌فرض ۱ = بدون تغییر.
        public static float SizeScale = 1.0f;

        public static Font Font(float size)
        {
            return new Font(ResolvedFamily, size * SizeScale, FontStyle.Regular);
        }

        public static Font FontBold(float size)
        {
            return new Font(ResolvedFamily, size * SizeScale, FontStyle.Bold);
        }

        // ─── انتخاب فونت از تنظیمات (تب ظاهر نرم‌افزار) ────────────────────────
        // اگر فونت اختصاصی در پوشه Fonts\ بسته شده باشد، همیشه اولویت دارد
        // (کیفیت/سازگاری تضمین‌شده)؛ در غیر این صورت اگر مدیر سیستم یکی از
        // فونت‌های نصب‌شده را انتخاب کرده باشد همان استفاده می‌شود.
        public static void ApplyFontPreference(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName)) return;
            if (TryLoadBundledFont() != null) return; // فونت اختصاصی همیشه اولویت دارد

            try
            {
                lock (FontLock)
                {
                    _resolvedFamily = new FontFamily(familyName);
                }
            }
            catch (ArgumentException)
            {
                // فونت درخواستی نصب نیست؛ انتخاب خودکار فعلی دست‌نخورده می‌ماند
            }
        }

        // ─── دکمه اصلی (رنگی، پر) ───────────────────────────────────────────
        // ─── راهنمای لحظه‌ای دکمه‌ها (Tooltip) ───────────────────────────────
        // آموزش — چرا یک نمونه‌ی مشترک ToolTip و نه یکی برای هر دکمه:
        // اگر دو کامپوننت ToolTip جدا برای یک کنترل متن داشته باشند، رفتار
        // نمایششان در WinForms غیرقابل‌پیش‌بینی می‌شود. با یک نمونه‌ی مشترک،
        // فراخوانیِ بعدیِ SetTip روی همان دکمه صرفاً متن قبلی را جایگزین
        // می‌کند — پس فرم‌ها می‌توانند راهنمای دقیق‌ترِ خودشان را بگذارند.
        private static readonly ToolTip ButtonTips = new ToolTip
        {
            InitialDelay = 450,
            ReshowDelay  = 200,
            AutoPopDelay = 8000,
            ShowAlways   = true
        };

        // راهنمای لحظه‌ای یک کنترل را تنظیم می‌کند (یا با متن خالی برمی‌دارد).
        public static void SetTip(Control control, string tip)
        {
            if (control == null) return;
            ButtonTips.SetToolTip(control, tip ?? "");
        }

        // متنِ راهنما بر اساس عنوانِ دکمه. عنوان‌های پرتکرار جدولِ اختصاصی
        // دارند؛ برای بقیه، از پیشوندِ عنوان یک توضیح عمومی ساخته می‌شود.
        // اگر هیچ‌کدام نخورد رشته‌ی خالی برمی‌گردد و دکمه بدون راهنما می‌ماند
        // (بهتر از راهنمایی که فقط عنوان را تکرار کند).
        private static readonly System.Collections.Generic.Dictionary<string, string> ButtonTipTexts =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ذخیره",            "ثبت تغییرهای واردشده در دیتابیس." },
            { "جدید",             "پاک‌کردن فرم برای ثبت یک رکورد تازه." },
            { "فرم جدید",         "پاک‌کردن فرم برای ثبت یک رکورد تازه." },
            { "ویرایش",           "ویرایش رکورد انتخاب‌شده." },
            { "حذف",              "حذف رکورد انتخاب‌شده. این کار برگشت‌پذیر نیست." },
            { "جستجو",            "اجرای جستجو با فیلترهای واردشده." },
            { "بستن",             "بستن این پنجره." },
            { "انصراف",           "بستن بدون ذخیره‌ی تغییرها." },
            { "بازنشانی",         "خالی‌کردن فیلترها و بازگشت به حالت اولیه." },
            { "تازه‌سازی",        "خواندن دوباره‌ی اطلاعات از دیتابیس." },
            { "بازخوانی",         "خواندن دوباره‌ی اطلاعات از دیتابیس." },
            { "چاپ",              "ارسال به چاپگر." },
            { "خروجی Excel",      "ذخیره‌ی نتایج در یک فایل اکسل." },
            { "خروجی اکسل",       "ذخیره‌ی نتایج در یک فایل اکسل." },
            { "خروجی Word",       "ذخیره‌ی نتایج در یک فایل Word." },
            { "خروجی PDF",        "ذخیره‌ی نتایج در یک فایل PDF." },
            { "انتخاب",           "انتخاب مورد مشخص‌شده." },
            { "انتخاب...",        "باز کردن پنجره‌ی انتخاب." },
            { "لغو انتخاب",       "برداشتن همه‌ی انتخاب‌ها." },
            { "فعال/غیرفعال",     "تغییر وضعیت فعال یا غیرفعال بودن مورد انتخاب‌شده." },
            { "فعال / غیرفعال",   "تغییر وضعیت فعال یا غیرفعال بودن مورد انتخاب‌شده." },
            { "کپی",              "کپی در حافظه‌ی سیستم (Clipboard)." },
            { "قبلی",             "بازگشت به مرحله‌ی قبل." },
            { "بعدی",             "رفتن به مرحله‌ی بعد." },
            { "کارت شناسایی",     "ساخت و نمایش کارت شناسایی این سرپرست." },
            { "مشاهده پرونده",    "باز کردن پرونده‌ی مربوط به ردیف انتخاب‌شده." },
        };

        private static string TipForButton(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            string caption = text.Trim();
            string exact;
            if (ButtonTipTexts.TryGetValue(caption, out exact)) return exact;

            // دنباله‌ی بلندِ عنوان‌های اختصاصی («ذخیره تنظیمات امنیت»، «چاپ رسید
            // شهریه»، «خروجی جمعی …») با همین چند قاعده پوشش داده می‌شود.
            if (caption.StartsWith("ذخیره", StringComparison.Ordinal))
                return "ثبت و ذخیره‌ی «" + caption.Substring("ذخیره".Length).Trim() + "».";
            if (caption.StartsWith("چاپ", StringComparison.Ordinal))
                return "ارسال «" + caption.Substring("چاپ".Length).Trim() + "» به چاپگر.";
            if (caption.StartsWith("خروجی", StringComparison.Ordinal))
                return "ساخت فایل خروجی: " + caption.Substring("خروجی".Length).Trim() + ".";
            if (caption.StartsWith("حذف", StringComparison.Ordinal))
                return "حذف «" + caption.Substring("حذف".Length).Trim() + "». این کار برگشت‌پذیر نیست.";
            if (caption.StartsWith("راهنما", StringComparison.Ordinal))
                return "نمایش راهنمای این بخش.";

            return "";
        }

        public static Button CreateButton(string text, string icon, Color backColor)
        {
            Button b = new Button();
            b.Text = string.IsNullOrEmpty(icon) ? text : (icon + "   " + text);

            // راهنمای لحظه‌ای: فقط وقتی متنِ مفیدی برای این عنوان داریم. فرم‌ها
            // می‌توانند بعداً با UiTheme.SetTip متنِ دقیق‌ترِ خودشان را بگذارند.
            string tip = TipForButton(text);
            if (tip.Length > 0) ButtonTips.SetToolTip(b, tip);

            b.BackColor = backColor;
            b.ForeColor = Color.White;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.18f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.08f);
            b.Font = FontBold(10.5f);
            b.Cursor = Cursors.Hand;
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        // ─── دکمه ثانویه (حاشیه‌دار، سفید) ──────────────────────────────────
        public static Button CreateSecondaryButton(string text, string icon)
        {
            Button b = CreateButton(text, icon, Color.White);
            b.ForeColor = Primary;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.MouseOverBackColor = HoverTint;
            b.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#D9E6F0");
            return b;
        }

        // ─── ورودی متن با استایل Focus یکسان ────────────────────────────────
        public static void StyleTextBox(TextBox tb)
        {
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = Font(10.5f);

            Color normal = Color.White;
            Color focusColor = HoverTint;

            tb.BackColor = normal;
            tb.Enter += delegate { tb.BackColor = focusColor; };
            tb.Leave += delegate { tb.BackColor = normal; };
        }

        // ─── ظاهر یکسان برای همه گریدها ──────────────────────────────────────
        public static void StyleGrid(DataGridView grid)
        {
            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = CardBack;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Primary;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = FontBold(10f);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersHeight = 34;
            grid.DefaultCellStyle.Font = Font(9.5f);
            grid.DefaultCellStyle.Padding = new Padding(3, 0, 3, 0);
            grid.DefaultCellStyle.SelectionBackColor = PrimaryLight;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F7F9FB");
            grid.RowTemplate.Height = 30;
            grid.RowHeadersVisible = false;
            grid.AllowUserToResizeRows = false;
            grid.GridColor = Border;

            // هدر فارسی خودکار: بعد از هر بار bind، ستون‌هایی که هنوز نام
            // انگلیسی دیتابیس را نشان می‌دهند به فارسی ترجمه می‌شوند. ستون‌هایی
            // که فرم خودش هدر فارسی برایشان گذاشته دست‌نخورده می‌مانند.
            grid.DataBindingComplete -= LocalizeHeadersHandler;
            grid.DataBindingComplete += LocalizeHeadersHandler;
        }

        private static void LocalizeHeadersHandler(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null)
                return;

            foreach (DataGridViewColumn col in grid.Columns)
            {
                // فقط ستون‌هایی که هنوز ترجمه نشده‌اند (هدر = نام انگلیسی ستون).
                if (col.HeaderText == col.Name && HeaderMap.ContainsKey(col.Name))
                    col.HeaderText = HeaderMap[col.Name];
            }
        }

        // ─── ترجمه نام ستون به فارسی (برای استفاده خارج از DataGridView،
        // مثلاً چاپ). اگر ستون از قبل فارسی باشد یا در نگاشت نباشد، همان
        // مقدار اصلی بدون تغییر برگردانده می‌شود — آموزش: این دقیقاً همان
        // مکانیزم LocalizeHeadersHandler است، فقط بدون وابستگی به DataGridView
        // تا PrintHelper هم بتواند قبل از چاپ همان ترجمه را روی ستون‌های
        // خام DataTable (که هنوز به فارسی alias نشده‌اند) اعمال کند.
        public static string TranslateHeader(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                return columnName;

            string translated;
            return HeaderMap.TryGetValue(columnName, out translated) ? translated : columnName;
        }

        // نگاشت نام ستون‌های دیتابیس به عنوان فارسی — مرجع واحد برای همه گریدها.
        private static readonly System.Collections.Generic.Dictionary<string, string> HeaderMap =
            new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "CasID", "شناسه" }, { "FormNo", "شماره فرم" }, { "Code", "کد اختصاصی" },
            { "CaseNo", "شماره پرونده" }, { "CaseDate", "تاریخ تشکیل" }, { "HeadFullName", "نام سرپرست" },
            { "Phone", "شماره تماس" }, { "ServiceStatus", "وضعیت خدمات" }, { "Province", "ولایت" },
            { "District", "ولسوالی" }, { "UrgentSituation", "شرح وضعیت فوری" }, { "SurveyDate", "تاریخ سروی" },
            { "Zone", "زون" }, { "RequestType", "نوع درخواست" },
            { "FamID", "شناسه" }, { "MemberName", "نام" }, { "MemberFatherName", "نام پدر" },
            { "MemberTazkiraNo", "شماره تذکره" }, { "BirthDate", "تاریخ تولد" }, { "Gender", "جنسیت" },
            { "MemberEducation", "تحصیلات" }, { "Skill", "مهارت" },
            { "DocID", "شناسه سند" }, { "DocType", "نوع سند" }, { "OriginalFileName", "نام فایل" },
            { "RelatedCaseRef", "مرجع مرتبط" }, { "DocFilePath", "مسیر فایل" }, { "DocDescription", "توضیحات" },
            { "AssistanceID", "شناسه کمک" }, { "AssistanceDate", "تاریخ کمک" }, { "Amount", "مبلغ" },
            { "AssistanceType", "نوع کمک" }, { "Description", "توضیحات" }, { "CreatedBy", "ثبت‌کننده" },
            { "LogID", "شناسه" }, { "CreatedAt", "تاریخ" }, { "Username", "کاربر" },
            { "Operation", "عملیات" }, { "EntityName", "جدول" }, { "EntityID", "شناسه رکورد" },
            { "OldValue", "مقدار قبلی" }, { "NewValue", "مقدار جدید" },
            { "UserID", "شناسه" }, { "Role", "نقش" }, { "IsActive", "فعال" },
            { "MustChangePassword", "تغییر رمز اجباری" },
            { "StopReason", "دلیل قطع موقت" }, { "Religion", "مذهب" }, { "MaritalStatus", "وضعیت تأهل" },
            { "GlobalID", "شناسه سراسری" }, { "CenterID", "شناسه مرکز" }, { "LastCenterID", "آخرین مرکز" },
            { "WhatsApp", "واتساپ" }, { "SortOrder", "ترتیب" }, { "LookupID", "شناسه" },
            { "Category", "دسته‌بندی" }, { "Value", "مقدار" }, { "CenterCode", "کد مرکز" },
            { "CenterName", "نام مرکز" }, { "ChangedAt", "تاریخ تغییر" }, { "ChangedBy", "تغییردهنده" },
            { "OldStatus", "وضعیت قبلی" }, { "NewStatus", "وضعیت جدید" }, { "StatusID", "شناسه" },
            { "SettingKey", "کلید تنظیم" }, { "SettingValue", "مقدار تنظیم" }, { "UpdatedAt", "تاریخ به‌روزرسانی" },
            { "TableName", "جدول" }, { "ActionType", "نوع عملیات" }, { "RecordID", "شناسه رکورد" },
            { "OldData", "داده قبلی" }, { "NewData", "داده جدید" }, { "ActionDate", "تاریخ عملیات" },
            { "CaseID", "شناسه پرونده" }, { "GradeLevel", "صنف" }, { "SchoolName", "نام مکتب" },
            { "UniversityName", "نام دانشگاه" }, { "StudyYear", "سمستر/درجه دانشگاه" }, { "Major", "رشته دانشگاه" },
            { "StudyField", "حوزه علمیه" }, { "OfficialStatus", "وضعیت رسمی تحصیلی" }, { "LeaveReason", "دلیل ترک تحصیل" },
            { "HasDisability", "نوع معلولیت" }, { "MemberDisabilityDegree", "درجه معلولیت" }, { "MemberSadat", "سیادت" },
            { "PhysicalStatus", "وضعیت جسمی" }, { "DisabilityDegree", "درجه معلولیت" }, { "DisabilityType", "نوع معلولیت" },
            { "MigrationCardType", "نوع برگه مهاجرت" }, { "CoveredByOrg", "تحت پوشش دیگر مؤسسات" }, { "PriorityLevel", "اولویت‌بندی اقتصادی" },
            { "CoveredByOrgNames", "اسامی مؤسسات تحت پوشش" },
            { "HeadFatherName", "نام پدر سرپرست" }, { "HeadSadat", "سیادت سرپرست" }, { "HeadTazkiraNo", "شماره تذکره سرپرست" },
            { "HeadOriginalResidence", "سکونت اصلی" }, { "HeadCurrentResidence", "سکونت فعلی" },
            { "RelationshipToFamily", "نسبت با اعضا" }, { "RelativePhone", "شماره تماس اقارب" }, { "Job", "شغل" },
            { "EducationLevel", "تحصیلات" }, { "Surveyors", "سروی‌کننده‌ها" }, { "LocationAddress", "آدرس لوکیشن" },
            // بخش ۳ و ۵ — نوع تذکره و یادداشت وضعیت جسمی
            { "HeadIdCardType", "نوع تذکره سرپرست" }, { "MemberIdCardType", "نوع تذکره" },
            { "PhysicalStatusNotes", "یادداشت وضعیت جسمی" }
        };

        // ─── نمایش شمسی ستون‌های تاریخ در گرید (بدون تغییر مقدار واقعی) ──────
        // آموزش: مقدار ذخیره‌شده در دیتابیس/DataTable همچنان رشته میلادی
        // yyyy-MM-dd می‌ماند (تصمیم: «ذخیره میلادی، نمایش شمسی») — این متد
        // فقط در لحظه نمایش سلول (CellFormatting) رشته را به شمسی تبدیل
        // می‌کند، پس Sort/Query/Export مبتنی بر DataTable دست‌نخورده می‌ماند.
        public static void ApplyPersianDateColumns(DataGridView grid, params string[] columnNames)
        {
            if (grid == null || columnNames == null || columnNames.Length == 0)
                return;

            grid.CellFormatting += delegate (object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.ColumnIndex < 0 || e.Value == null || e.Value == DBNull.Value)
                    return;

                string columnName = grid.Columns[e.ColumnIndex].Name;

                bool isDateColumn = false;
                foreach (string name in columnNames)
                {
                    if (string.Equals(columnName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        isDateColumn = true;
                        break;
                    }
                }
                if (!isDateColumn)
                    return;

                string raw = e.Value.ToString();
                DateTime dt;
                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                {
                    // اگر رشته اصلی ساعت هم داشت (مثلاً CreatedAt گزارش رویدادها)،
                    // ساعت در نمایش شمسی هم حفظ می‌شود؛ برای ستون‌های فقط-تاریخ
                    // (CaseDate/BirthDate/...) فقط yyyy/MM/dd نمایش داده می‌شود.
                    e.Value = raw.Trim().Length > 10
                        ? PersianDateHelper.ToPersianDateTimeString(dt)
                        : PersianDateHelper.ToPersianDateString(dt);
                    e.FormattingApplied = true;
                }
            };
        }

        // ─── ورودی فشرده و یکسان (تکست‌باکس/کمبو/تاریخ) ─────────────────────
        // حدود ۲۵٪ کوچک‌تر از حالت پرکننده، با اندازه ثابت و یکسان و راست‌چین
        // در سلول تا فضای بیشتری ایجاد شود.
        // آموزش — کوچک‌سازی ۳۰٪: قبلاً ۱۷۵px بود؛ طبق درخواست بازطراحی ظاهری
        // همه TextBox/ComboBox حدود ۳۰٪ کوچک‌تر شدند (فشرده‌تر، مدرن‌تر).
        public const int InputWidth = 122;
        public const int InputHeight = 26;

        public static void CompactInput(Control input)
        {
            input.Dock = DockStyle.None;
            input.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            input.Width = InputWidth;
            input.Margin = new Padding(3, 5, 3, 5);

            TextBox tb = input as TextBox;
            if (tb != null && !tb.Multiline)
                input.Height = InputHeight;
        }

        // ─── ورودی پرکننده سلول (چیدمان مرتب: لیبل راست، ورودی چسبیده کنارش) ──
        // آموزش — رفع باگ فاصله‌افتادن: CompactInput ورودی را با عرض ثابت ۱۲۲px و
        // Anchor=Right می‌گذارد؛ در فرم‌های RightToLeftLayout=true این Anchor آینه
        // می‌شود و ورودی به لبه دورِ سلولِ پهن (۵۰٪) می‌چسبد و از لیبل فاصله زیاد
        // می‌گیرد. FieldInput با Anchor=Left|Right ورودی را در کل عرض سلول کش
        // می‌دهد تا دقیقاً کنار لیبلِ راست‌چین قرار گیرد (فیت و منظم). ارتفاع
        // ثابت می‌ماند و ورودی به‌صورت عمودی وسط سلول می‌نشیند (بدون Top/Bottom).
        public static void FieldInput(Control input)
        {
            input.Dock = DockStyle.None;
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            input.Margin = new Padding(3, 5, 3, 5);

            TextBox tb = input as TextBox;
            if (tb != null && !tb.Multiline)
                input.Height = InputHeight;
        }

        // ─── اندازه‌ی پایه‌ی فرم + امکان بزرگ‌نمایی (Maximize) ────────────────
        // آموزش — به درخواست کاربر، همه‌ی فرم‌ها باید بتوانند بین «اندازه‌ی
        // معمولی» و «تمام‌صفحه» جابه‌جا شوند. اما نکته‌ی مهم: بخش بزرگی از
        // فرم‌های این پروژه کنترل‌هایشان را با مختصات مطلق (SetBounds) چیده‌اند؛
        // اگر فرم بتواند کوچک‌تر از اندازه‌ی طراحی شود، آن کنترل‌ها بریده/پنهان
        // می‌شوند. راه‌حل: MinimumSize دقیقاً روی همان اندازه‌ی طراحی قفل می‌شود
        // تا فرم فقط بتواند بزرگ‌تر شود، هرگز کوچک‌تر. پس بزرگ‌نمایی امن است و
        // هیچ چیدمانی نمی‌شکند.
        //
        // نام متد عمداً تغییر نکرد چون در ده‌ها فرم فراخوانی شده و تغییر نام
        // فقط شلوغی و ریسک بی‌دلیل می‌سازد.
        public static void MakeFixedSize(Form form, int width, int height)
        {
            form.WindowState = FormWindowState.Normal;
            form.FormBorderStyle = FormBorderStyle.Sizable;

            // آموزش — خواسته‌ی کاربر: پنجره‌ها در «یک حالتِ ثابت» بمانند و
            // اندازه‌شان با دکمه‌ی بیشینه/بازگردانی عوض نشود؛ ولی کوچک‌کردن
            // (Minimize) باید مثل هر برنامه‌ی ویندوزی در دسترس باشد.
            //   • MaximizeBox = false  → دکمه‌ی تغییرِ حالت برداشته می‌شود
            //   • MinimizeBox = true   → کوچک‌کردن سرِجایش می‌ماند
            // پنجره‌های کاری از طریق MakeMainWindow همچنان تمام‌صفحه باز
            // می‌شوند؛ فقط دیگر قابلِ جابه‌جا شدن بین دو حالت نیستند.
            form.MaximizeBox = false;
            form.MinimizeBox = true;

            form.StartPosition = FormStartPosition.CenterScreen;
            form.ClientSize = new Size(width, height);

            // آموزش — خواسته‌ی کاربر: پنجره‌ها نه تمام‌صفحه باز شوند و نه با
            // کشیدنِ لبه بزرگ/کوچک شوند؛ همیشه یک اندازه‌ی متوسطِ ثابت.
            // FixedSingle لبه‌ی غیرقابل‌کشیدن می‌دهد و برابر کردن
            // MinimumSize/MaximumSize تضمین می‌کند هیچ کدی هم اندازه را عوض
            // نکند. اگر اندازه‌ی طراحی از خودِ صفحه بزرگ‌تر باشد به ناحیه‌ی
            // کاری محدود می‌شود، وگرنه دکمه‌های پایینِ فرم بیرون از صفحه
            // می‌مانند.
            form.FormBorderStyle = FormBorderStyle.FixedSingle;
            LockToFixedSize(form);
            TryApplyIcon(form);
        }

        // ─── پنجره‌ی اصلیِ کاری: تمام‌صفحه‌ی خودکار ────────────────────────────
        // آموزش — به درخواست کاربر، فرم‌های کاریِ اصلی باید خودکار تمام‌صفحه
        // باز شوند. دو نکته‌ی مهم:
        //   ۱) MinimumSize (که MakeFixedSize روی اندازه‌ی طراحی قفل می‌کند)
        //      همچنان برقرار می‌ماند، پس اگر کاربر از تمام‌صفحه خارج شد،
        //      پنجره نمی‌تواند کوچک‌تر از اندازه‌ی طراحی شود و چیدمان
        //      نمی‌شکند — دقیقاً همان چیزی که کاربر خواست.
        //   ۲) اگر اندازه‌ی طراحی از خودِ صفحه‌نمایش بزرگ‌تر باشد (نمایشگرهای
        //      کوچک یا مقیاس ۱۵۰٪ ویندوز)، MinimumSize به ناحیه‌ی کاریِ صفحه
        //      محدود می‌شود؛ وگرنه پنجره بزرگ‌تر از صفحه می‌ماند و دکمه‌های
        //      پایینش دór دسترس نخواهند بود.
        public static void MakeMainWindow(Form form, int width, int height)
        {
            // آموزش — تا نسخه‌ی قبل این متد فرم را تمام‌صفحه باز می‌کرد و کاربر
            // گزارش داد که دکمه‌ها در آن حالت پیدا نیستند. حالا همان اندازه‌ی
            // ثابتِ متوسطِ MakeFixedSize استفاده می‌شود؛ متد و همه‌ی فراخوانی‌هایش
            // سرِجای خود ماندند تا هیچ فرمی تغییر نکند.
            MakeFixedSize(form, width, height);
        }

        // اندازه‌ی پنجره را قفل می‌کند و در صورت لزوم به ناحیه‌ی کاریِ صفحه
        // محدود می‌سازد. از دو متد بالا فراخوانی می‌شود.
        private static void LockToFixedSize(Form form)
        {
            Size target = form.Size;

            try
            {
                Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
                target = new Size(
                    Math.Min(target.Width, workingArea.Width),
                    Math.Min(target.Height, workingArea.Height));
            }
            catch { /* اگر اطلاعات صفحه در دسترس نبود، همان اندازه‌ی طراحی می‌ماند */ }

            // ترتیب مهم است: اول سقف را برمی‌داریم، اندازه را می‌گذاریم، بعد قفل.
            form.MaximumSize = Size.Empty;
            form.MinimumSize = Size.Empty;
            form.Size = target;
            form.MinimumSize = target;
            form.MaximumSize = target;
        }

        // آیکون فرم = لوگوی نرم‌افزار (بند ۹ بازطراحی ظاهری)
        private static void TryApplyIcon(Form form)
        {
            try { form.Icon = LogoHelper.GetAppIcon(); }
            catch { /* آیکون غیرحیاتی است */ }
        }

        // ─── اعمال یکجای ظاهر روی فرم‌های قدیمی (Designer-based) ────────────
        // آموزش: به‌جای جابه‌جا کردن تک‌تک ده‌ها کنترلی که با طراح فرم
        // ساخته شده‌اند (ریسک بالا برای شکستن Layout)، این متد به‌صورت
        // بازگشتی روی همه کنترل‌های فرم رنگ/استایل یکسان اعمال می‌کند؛
        // مکان/اندازه کنترل‌ها دست‌نخورده می‌ماند.
        //
        // نکته مهم — اندازه فونت هر کنترل دست‌نخورده می‌ماند (فقط خانواده
        // فونت عوض می‌شود). طراح فرم عمداً لیبل‌ها را بزرگ‌تر/بولدتر
        // (مثلاً ۱۲ پوینت) از ورودی‌ها (مثلاً ۹ پوینت) طراحی کرده بود؛
        // یکسان‌سازی اندازه فونت این تناسب را به‌هم می‌ریخت و باعث می‌شد
        // ورودی‌ها نسبت به لیبل‌ها بزرگ و نامتوازن به‌نظر برسند.
        public static void ApplySweep(Control root)
        {
            root.Font = new Font(ResolvedFamily, root.Font.Size, root.Font.Style);

            if (root is Form)
            {
                root.BackColor = Background;
                TryApplyIcon((Form)root);
            }
            else if (root is Panel || root is GroupBox)
            {
                root.BackColor = CardBack;
            }
            else if (root is TextBox)
            {
                TextBox tb = (TextBox)root;
                tb.BorderStyle = BorderStyle.FixedSingle;

                Color normal = Color.White;
                Color focusColor = HoverTint;
                tb.BackColor = normal;
                tb.Enter += delegate { tb.BackColor = focusColor; };
                tb.Leave += delegate { tb.BackColor = normal; };
            }
            else if (root is ComboBox || root is NumericUpDown || root is DateTimePicker)
            {
                root.BackColor = Color.White;
            }
            else if (root is DataGridView)
            {
                StyleGrid((DataGridView)root);
            }
            else if (root is Button && ((Button)root).FlatStyle != FlatStyle.Flat)
            {
                Button button = (Button)root;
                button.BackColor = Primary;
                button.ForeColor = Color.White;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(Primary, 0.18f);
                button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(Primary, 0.08f);
                button.Cursor = Cursors.Hand;
                button.UseVisualStyleBackColor = false;
            }
            else if (root is Label)
            {
                ((Label)root).ForeColor = TextDark;
            }

            // ToList نیست چون Control.ControlCollection خودش IEnumerable است؛
            // چون در ApplySweep چیزی از Controls حذف/اضافه نمی‌شود ایمن است.
            foreach (Control child in root.Controls)
                ApplySweep(child);
        }

        // ─── افزودن آیکون به دکمه‌ای که از قبل متن دارد (بدون تغییر متن) ────
        public static void SetButtonIcon(Button button, string icon)
        {
            if (string.IsNullOrEmpty(icon))
                return;
            if (button == null)
                return;
            if (!button.Text.StartsWith(icon))
                button.Text = icon + "  " + button.Text;
        }

        public static void RoundCorners(Control control, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, 0, radius, radius, 180, 90);
            path.AddArc(control.Width - radius, 0, radius, radius, 270, 90);
            path.AddArc(control.Width - radius, control.Height - radius, radius, radius, 0, 90);
            path.AddArc(0, control.Height - radius, radius, radius, 90, 90);
            path.CloseFigure();
            control.Region = new Region(path);
        }

        // ─── حالت بی‌دیالوگ (فقط برای آزمون خودکار) ──────────────────────────
        // آموزش — چرا این کلید لازم شد: مسیرهای واقعی برنامه (مثل ثبت کمک در
        // FrmFinance.btnSave_Click) در پایان کارشان یک دیالوگ مودال نشان
        // می‌دهند. آزمون خودکار همان متد را مستقیم صدا می‌زند، ولی ShowDialog
        // حلقه‌ی پیامِ خودش را باز می‌کند و منتظر کلیک می‌ماند — کسی نیست که
        // کلیک کند، پس نخِ آزمون تا ابد بلوکه می‌شود و کل اجرای مجموعه‌ی آزمون
        // نیمه‌کاره رها می‌شود (با /Blame اثبات شد: ۴ آزمون FrmFinance مجموعه
        // را متوقف می‌کردند و ~۲۲۰ آزمون اصلاً اجرا نمی‌شدند).
        //
        // این کلید فقط در آزمون روشن می‌شود. در اجرای عادی برنامه مقدارش
        // false است و هیچ رفتاری برای کاربر تغییر نمی‌کند — نه پیامی حذف شده
        // و نه منطقی جابه‌جا شده است.
        public static bool SuppressDialogs = false;

        // پاسخِ فرضی برای دیالوگ‌های «تأیید» وقتی SuppressDialogs روشن است.
        // پیش‌فرض عمداً «نه» است تا آزمونی که ناخواسته به یک تأییدِ خطرناک
        // (حذف/بازنویسی) می‌رسد، آن را خاموش تأیید نکند. آزمونی که واقعاً
        // می‌خواهد مسیرِ «بله» را بسنجد، خودش این مقدار را عوض می‌کند.
        public static DialogResult SuppressedConfirmResult = DialogResult.No;

        // ─── دیالوگ‌های پیام سفارشی — جایگزین زیباتر MessageBox.Show ────────
        public static void ShowSuccess(IWin32Window owner, string message)
        {
            ShowMessage(owner, message, "موفق", Success, SuccessLight, "✓");
        }

        // ─── بخش ۶: جایگزین کاملاً فارسیِ MessageBox.Show ────────────────────
        // آموزش — چرا این متد لازم شد: دکمه‌های OK/Cancel/Yes/No را خودِ ویندوز
        // می‌سازد و از زبانِ ویندوز می‌آید، نه از تنظیمات برنامه. این متد همان
        // امضای معنایی MessageBox را می‌پذیرد (متن، عنوان، دکمه‌ها، آیکون) و
        // همان DialogResult را برمی‌گرداند، ولی با دیالوگ فارسیِ خودِ پروژه —
        // پس هیچ کد فراخواننده‌ای نیاز به تغییر ندارد.
        //
        // نگاشت دکمه‌ها به دو حالتِ موجودِ FrmMessage:
        //   YesNo / OKCancel / YesNoCancel  → حالت تأیید (بله / انصراف)
        //   بقیه (OK, RetryCancel, ...)     → حالت اطلاع‌رسانی (متوجه شدم)
        public static DialogResult ShowLocalizedDialog(
            IWin32Window owner, string message, string title,
            MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Color accent; Color accentLight; string glyph; string defaultTitle;

            switch (icon)
            {
                case MessageBoxIcon.Error:
                    accent = Danger;  accentLight = DangerLight;  glyph = "✕"; defaultTitle = "خطا";   break;
                case MessageBoxIcon.Warning:
                    accent = Warning; accentLight = WarningLight; glyph = "!"; defaultTitle = "هشدار"; break;
                case MessageBoxIcon.Question:
                    accent = Primary; accentLight = HoverTint;    glyph = "؟"; defaultTitle = "تأیید"; break;
                default:
                    accent = Primary; accentLight = HoverTint;    glyph = "i"; defaultTitle = "پیام";  break;
            }

            bool isConfirm =
                buttons == MessageBoxButtons.YesNo ||
                buttons == MessageBoxButtons.OKCancel ||
                buttons == MessageBoxButtons.YesNoCancel;

            if (isConfirm && icon == MessageBoxIcon.None)
            {
                accent = Primary; accentLight = HoverTint; glyph = "؟"; defaultTitle = "تأیید";
            }

            string shownTitle = string.IsNullOrWhiteSpace(title) ? defaultTitle : title;

            // حالت بی‌دیالوگِ آزمون — دقیقاً همان نگاشتِ پایینِ متد، فقط بدون
            // باز کردنِ پنجره؛ پس هیچ فراخواننده‌ای رفتار متفاوتی نمی‌بیند.
            if (SuppressDialogs)
            {
                if (!isConfirm)
                    return DialogResult.OK;

                if (buttons == MessageBoxButtons.OKCancel)
                    return SuppressedConfirmResult == DialogResult.Yes ? DialogResult.OK : DialogResult.Cancel;

                return SuppressedConfirmResult == DialogResult.Yes ? DialogResult.Yes : DialogResult.No;
            }

            using (FrmMessage frm = new FrmMessage(message, shownTitle, accent, accentLight, glyph, isConfirm))
            {
                DialogResult result = frm.ShowDialog(owner);

                if (!isConfirm)
                    return DialogResult.OK;

                // فراخوانندگانی که OKCancel داده‌اند، OK/Cancel انتظار دارند؛
                // آن‌هایی که YesNo داده‌اند، Yes/No. یک نگاشت ساده هر دو را
                // دقیقاً مثل MessageBox راضی می‌کند.
                if (buttons == MessageBoxButtons.OKCancel)
                    return result == DialogResult.Yes ? DialogResult.OK : DialogResult.Cancel;

                return result == DialogResult.Yes ? DialogResult.Yes : DialogResult.No;
            }
        }

        public static void ShowError(IWin32Window owner, string message)
        {
            ShowMessage(owner, message, "خطا", Danger, DangerLight, "✕");
        }

        public static void ShowWarning(IWin32Window owner, string message)
        {
            ShowMessage(owner, message, "هشدار", Warning, WarningLight, "!");
        }

        public static bool ShowConfirm(IWin32Window owner, string message, string title)
        {
            if (SuppressDialogs) return SuppressedConfirmResult == DialogResult.Yes;

            using (FrmMessage frm = new FrmMessage(message, string.IsNullOrEmpty(title) ? "تأیید" : title, Primary, HoverTint, "؟", true))
            {
                return frm.ShowDialog(owner) == DialogResult.Yes;
            }
        }

        private static void ShowMessage(IWin32Window owner, string message, string title, Color accent, Color accentLight, string glyph)
        {
            if (SuppressDialogs) return;

            using (FrmMessage frm = new FrmMessage(message, title, accent, accentLight, glyph, false))
            {
                frm.ShowDialog(owner);
            }
        }

        // دیالوگ پیام ساده و یکنواخت — هدر رنگی، آیکون گرد، عنوان و متن پیام.
        private class FrmMessage : Form
        {
            public FrmMessage(string message, string title, Color accent, Color accentLight, string glyph, bool isConfirm)
            {
                // آموزش — بازنویسی چیدمان با Dock به‌جای مختصات مطلق:
                // نسخه‌ی قبلی همه‌چیز را با SetBounds می‌چید و همزمان
                // RightToLeftLayout=true داشت؛ یعنی همان مختصات هم آینه می‌شد.
                // نتیجه این بود که عنوان و متن به‌جای چسبیدن به لبه‌ی راست، وسطِ
                // پنجره می‌افتادند و متنِ بلند هم بریده می‌شد (در اسکرین‌شات
                // کاربر دیده شد). حالا آینه‌ی هندسی خاموش است و هر ناحیه با
                // Dock جای خودش را می‌گیرد: آیکون چپ، متن‌ها راست — پایدار و
                // مستقل از عرضِ پنجره.
                // بخش ۶ — عنوان و برچسب دکمه‌ها از فرهنگ لغت برنامه می‌گذرند:
                // در فارسی (پیش‌فرض) عیناً همین متن‌ها می‌مانند و رفتار تغییری
                // نمی‌کند؛ فقط اگر کاربر از «تنظیمات» زبان را عوض کند، همراه
                // بقیه‌ی برنامه ترجمه می‌شوند.
                title = Lang.T(title);
                message = Lang.T(message);

                Text = title;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MinimizeBox = false;
                MaximizeBox = false;
                ShowIcon = false;
                ShowInTaskbar = false;
                RightToLeft = Lang.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
                RightToLeftLayout = false;
                BackColor = Color.White;
                Font = UiTheme.Font(10.5f);

                const int DialogWidth = 430;
                const int IconArea = 96;
                const int SidePad = 22;

                // ارتفاع لازم برای متن، پیش از ساختِ کنترل‌ها اندازه‌گیری می‌شود
                // تا فرم دقیقاً به‌اندازه‌ی محتوا بلند شود (نه بیشتر، نه کمتر).
                int textWidth = DialogWidth - IconArea - SidePad * 2;
                Font msgFont = UiTheme.Font(10.5f);
                int measuredHeight = TextRenderer.MeasureText(
                    string.IsNullOrEmpty(message) ? " " : message,
                    msgFont,
                    new Size(textWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.RightToLeft).Height;

                int msgHeight = Math.Max(44, Math.Min(measuredHeight + 10, 420));
                int bodyHeight = 34 /*عنوان*/ + msgHeight + 24;

                ClientSize = new Size(DialogWidth, 8 /*نوار رنگی*/ + bodyHeight + 62 /*نوار دکمه*/);

                // ── نوار رنگی بالا ──
                Panel header = new Panel();
                header.Dock = DockStyle.Top;
                header.Height = 8;
                header.BackColor = accent;

                // ── نوار دکمه‌ها (پایین) ──
                Panel buttonBar = new Panel();
                buttonBar.Dock = DockStyle.Bottom;
                buttonBar.Height = 62;
                buttonBar.Padding = new Padding(SidePad, 12, SidePad, 16);

                FlowLayoutPanel buttons = new FlowLayoutPanel();
                buttons.Dock = DockStyle.Fill;
                // LeftToRight همراه با RightToLeft=Yes ارثی، دقیقاً یک‌بار آینه
                // می‌شود ⇒ دکمه‌ها از سمت راست شروع می‌شوند (الگوی رایج پروژه).
                buttons.FlowDirection = FlowDirection.LeftToRight;
                buttons.WrapContents = false;
                buttonBar.Controls.Add(buttons);

                // ── بدنه: آیکون (چپ) + متن‌ها (راست) ──
                Panel body = new Panel();
                body.Dock = DockStyle.Fill;
                body.Padding = new Padding(SidePad, 18, SidePad, 0);

                Panel iconArea = new Panel();
                iconArea.Dock = DockStyle.Left;
                iconArea.Width = IconArea - SidePad;

                Label lblGlyph = new Label();
                lblGlyph.Text = glyph;
                lblGlyph.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
                lblGlyph.ForeColor = accent;
                lblGlyph.BackColor = accentLight;
                lblGlyph.TextAlign = ContentAlignment.MiddleCenter;
                lblGlyph.SetBounds(0, 0, 56, 56);
                RoundCorners(lblGlyph, 56);
                iconArea.Controls.Add(lblGlyph);

                Panel textArea = new Panel();
                textArea.Dock = DockStyle.Fill;

                Label lblTitle = new Label();
                lblTitle.Text = title;
                lblTitle.Dock = DockStyle.Top;
                lblTitle.Height = 30;
                lblTitle.Font = UiTheme.FontBold(12.5f);
                lblTitle.ForeColor = UiTheme.TextDark;
                // آموزش — با RightToLeft=Yes، تراز متنِ Label آینه می‌شود:
                // MiddleRight بصراً «چپ» رندر می‌شود و MiddleLeft بصراً «راست».
                // برای چسبیدن عنوان به لبه‌ی راستِ پنجره (خواسته‌ی کاربر) باید
                // MiddleLeft داد. همین تله قبلاً در بنر داشبورد هم دیده شد.
                lblTitle.TextAlign = ContentAlignment.MiddleLeft;

                Label lblMessage = new Label();
                lblMessage.Text = message;
                lblMessage.Dock = DockStyle.Fill;
                lblMessage.Font = msgFont;
                lblMessage.ForeColor = UiTheme.TextMuted;
                // TopLeft ⇒ بصراً بالا-راست (به‌دلیل آینه‌شدنِ تراز در RightToLeft)
                lblMessage.TextAlign = ContentAlignment.TopLeft;

                textArea.Controls.Add(lblMessage);
                textArea.Controls.Add(lblTitle);

                body.Controls.Add(textArea);
                body.Controls.Add(iconArea);

                if (isConfirm)
                {
                    Button btnYes = UiTheme.CreateButton(Lang.T("بله"), "", accent);
                    btnYes.Size = new Size(104, 34);
                    btnYes.Margin = new Padding(0, 0, 8, 0);
                    btnYes.DialogResult = DialogResult.Yes;
                    buttons.Controls.Add(btnYes);

                    Button btnNo = UiTheme.CreateSecondaryButton(Lang.T("انصراف"), "");
                    btnNo.Size = new Size(104, 34);
                    btnNo.Margin = new Padding(0, 0, 8, 0);
                    btnNo.DialogResult = DialogResult.No;
                    buttons.Controls.Add(btnNo);

                    AcceptButton = btnYes;
                    CancelButton = btnNo;
                }
                else
                {
                    Button btnOk = UiTheme.CreateButton(Lang.T("متوجه شدم"), "", accent);
                    btnOk.Size = new Size(124, 34);
                    btnOk.Margin = new Padding(0, 0, 8, 0);
                    btnOk.DialogResult = DialogResult.OK;
                    buttons.Controls.Add(btnOk);

                    AcceptButton = btnOk;
                    CancelButton = btnOk;
                }

                // ترتیب افزودن: Fill آخر اضافه نمی‌شود تا نوارهای Top/Bottom
                // اول جایشان را بگیرند و بدنه بقیه را پر کند.
                Controls.Add(body);
                Controls.Add(buttonBar);
                Controls.Add(header);
            }
        }
    }
}

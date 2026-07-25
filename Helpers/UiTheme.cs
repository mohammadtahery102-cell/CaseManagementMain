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
        public static Button CreateButton(string text, string icon, Color backColor)
        {
            Button b = new Button();
            b.Text = string.IsNullOrEmpty(icon) ? text : (icon + "   " + text);
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
            { "MigrationCardType", "نوع برگه مهاجرت" }, { "CoveredByOrg", "تحت پوشش" }, { "PriorityLevel", "اولویت‌بندی" },
            { "HeadFatherName", "نام پدر سرپرست" }, { "HeadSadat", "سیادت سرپرست" }, { "HeadTazkiraNo", "شماره تذکره سرپرست" },
            { "HeadOriginalResidence", "سکونت اصلی" }, { "HeadCurrentResidence", "سکونت فعلی" },
            { "RelationshipToFamily", "نسبت با اعضا" }, { "RelativePhone", "شماره تماس اقارب" }, { "Job", "شغل" },
            { "EducationLevel", "تحصیلات" }, { "Surveyors", "سروی‌کننده‌ها" }, { "LocationAddress", "آدرس لوکیشن" }
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
            form.MaximizeBox = true;
            form.MinimizeBox = true;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ClientSize = new Size(width, height);
            form.MinimumSize = form.Size;   // بعد از ClientSize محاسبه می‌شود (اندازه‌ی بیرونی)
            form.MaximumSize = Size.Empty;  // بدون سقف — تمام‌صفحه آزاد است
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
            MakeFixedSize(form, width, height);

            try
            {
                Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
                int minW = Math.Min(form.MinimumSize.Width, workingArea.Width);
                int minH = Math.Min(form.MinimumSize.Height, workingArea.Height);
                form.MinimumSize = new Size(minW, minH);
            }
            catch { /* اگر اطلاعات صفحه در دسترس نبود، همان MinimumSize قبلی می‌ماند */ }

            form.WindowState = FormWindowState.Maximized;
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

        // ─── دیالوگ‌های پیام سفارشی — جایگزین زیباتر MessageBox.Show ────────
        public static void ShowSuccess(IWin32Window owner, string message)
        {
            ShowMessage(owner, message, "موفق", Success, SuccessLight, "✓");
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
            using (FrmMessage frm = new FrmMessage(message, string.IsNullOrEmpty(title) ? "تأیید" : title, Primary, HoverTint, "؟", true))
            {
                return frm.ShowDialog(owner) == DialogResult.Yes;
            }
        }

        private static void ShowMessage(IWin32Window owner, string message, string title, Color accent, Color accentLight, string glyph)
        {
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
                Text = title;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MinimizeBox = false;
                MaximizeBox = false;
                ShowIcon = false;
                ShowInTaskbar = false;
                RightToLeft = RightToLeft.Yes;
                RightToLeftLayout = true;
                BackColor = Color.White;
                ClientSize = new Size(400, 190);
                Font = UiTheme.Font(10.5f);

                Panel header = new Panel();
                header.Dock = DockStyle.Top;
                header.Height = 8;
                header.BackColor = accent;
                Controls.Add(header);

                Label lblGlyph = new Label();
                lblGlyph.Text = glyph;
                lblGlyph.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
                lblGlyph.ForeColor = accent;
                lblGlyph.BackColor = accentLight;
                lblGlyph.TextAlign = ContentAlignment.MiddleCenter;
                lblGlyph.SetBounds(ClientSize.Width - 90, 28, 56, 56);
                RoundCorners(lblGlyph, 56);
                Controls.Add(lblGlyph);

                Label lblTitle = new Label();
                lblTitle.Text = title;
                lblTitle.Font = UiTheme.FontBold(12.5f);
                lblTitle.ForeColor = UiTheme.TextDark;
                lblTitle.SetBounds(30, 30, ClientSize.Width - 140, 26);
                lblTitle.TextAlign = ContentAlignment.MiddleRight;
                Controls.Add(lblTitle);

                Label lblMessage = new Label();
                lblMessage.Text = message;
                lblMessage.Font = UiTheme.Font(10.5f);
                lblMessage.ForeColor = UiTheme.TextMuted;
                lblMessage.TextAlign = ContentAlignment.TopRight;
                Controls.Add(lblMessage);

                // آموزش — رفع «خطوط روی هم»: قبلاً ارتفاع پیام ثابت (۶۸px) بود و
                // پیام‌های چندخطی/بلند سرریز و روی هم می‌افتادند. حالا ارتفاعِ لازم
                // با اندازه‌گیری متنِ wrap‌شده در همان عرض محاسبه و فرم به‌اندازه‌ی
                // متن بزرگ می‌شود (با حداقل و حداکثر معقول).
                int msgWidth = ClientSize.Width - 60;
                int measuredHeight = TextRenderer.MeasureText(
                    string.IsNullOrEmpty(message) ? " " : message,
                    lblMessage.Font,
                    new Size(msgWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
                int msgHeight = Math.Max(56, Math.Min(measuredHeight + 8, 420));
                lblMessage.SetBounds(30, 62, msgWidth, msgHeight);

                int buttonsTop = lblMessage.Bottom + 16;
                ClientSize = new Size(ClientSize.Width, buttonsTop + 34 + 16);

                if (isConfirm)
                {
                    Button btnYes = UiTheme.CreateButton("بله", "", accent);
                    btnYes.SetBounds(ClientSize.Width - 130, buttonsTop, 100, 34);
                    btnYes.DialogResult = DialogResult.Yes;
                    Controls.Add(btnYes);

                    Button btnNo = UiTheme.CreateSecondaryButton("انصراف", "");
                    btnNo.SetBounds(ClientSize.Width - 240, buttonsTop, 100, 34);
                    btnNo.DialogResult = DialogResult.No;
                    Controls.Add(btnNo);

                    AcceptButton = btnYes;
                    CancelButton = btnNo;
                }
                else
                {
                    Button btnOk = UiTheme.CreateButton("متوجه شدم", "", accent);
                    btnOk.SetBounds(ClientSize.Width - 150, buttonsTop, 120, 34);
                    btnOk.DialogResult = DialogResult.OK;
                    Controls.Add(btnOk);

                    AcceptButton = btnOk;
                    CancelButton = btnOk;
                }
            }
        }
    }
}

using CaseManagement.DAL;
using CaseManagement.Helpers;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CaseManagement
{
    // آموزش — بازطراحی کامل (به درخواست کاربر): قبلاً این فرم فقط سرپرست
    // خانواده را جستجو می‌کرد. حالا دو بخش کاملاً مجزا در دو تب دارد:
    //   ۱) جستجوی سرپرست — تمام اطلاعات اصلی TblCase
    //   ۲) جستجوی اعضاء خانواده — تمام اطلاعات اصلی TblFamily (+ اطلاعات پرونده والد)
    // هر دو بخش فیلتر «ولایت» و «تحصیلات» (دانشگاهی/مکتبی/سایر مقاطع) و دکمه
    // «خروجی Excel» برای نتیجه فیلترشده دارند.
    public class FrmAdvancedSearch : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        private static readonly string[] Provinces =
        {
            "کابل", "هرات", "بلخ", "قندهار", "ننگرهار", "بدخشان", "بغلان", "تخار",
            "غزنی", "هلمند", "لغمان", "کندز", "فاریاب", "جوزجان", "سمنگان", "بامیان",
            "پکتیا", "لوگر", "وردک", "غور", "فراه", "خوست", "کاپیسا", "پروان",
            "زابل", "ارزگان", "نیمروز", "نورستان", "کنر", "سرپل", "دایکندی",
            "پکتیکا", "بادغیس", "پنجشیر"
        };

        // ─── تب ۱: جستجوی سرپرست ─────────────────────────────────────────────
        private TextBox txtHCode, txtHFormNo, txtHName, txtHFatherName, txtHTazkira, txtHPhone, txtHDistrict, txtHJob;
        // «تحت پوشش دیگر مؤسسات» (بله/خیر) و «اسامی مؤسسات تحت پوشش» (جستجوی متنی).
        private ComboBox cmbHCoveredByOrg;
        private TextBox txtHCoveredByOrgNames;
        private ComboBox cmbHProvince, cmbHRequestType, cmbHPriority, cmbHSadat, cmbHReligion, cmbHMarital, cmbHEducationTier, cmbHDisabilityType, cmbHStatus;
        // بخش ۴ — فیلتر نوع تذکره سرپرست
        private ComboBox cmbHIdCardType;
        private Helpers.PersianDatePicker dtpHFrom, dtpHTo;
        private DataGridView dgvHeadResults;

        // ─── تب ۲: جستجوی اعضاء خانواده ──────────────────────────────────────
        private TextBox txtMName, txtMFatherName, txtMTazkira, txtMSkill, txtMDistrict;
        private ComboBox cmbMProvince, cmbMGender, cmbMEducationTier, cmbMMarital, cmbMReligion, cmbMPhysical, cmbMDisabilityType, cmbMStatus;
        // بخش ۴ — فیلتر نوع تذکره عضو
        private ComboBox cmbMIdCardType;
        private ComboBox cmbMGrade;   // فیلتر صنف (درخواست کاربر)
        // فیلتر «نوع درخواست» پرونده در تبِ اعضا — تا بتوان ایتام را از سایر
        // بخش‌ها (معلول، مهاجر، بدسرپرست، …) در سطحِ عضو هم جدا کرد.
        private ComboBox cmbMRequestType;
        // بخش ۱۲ — فیلتر «نقش عضو» (یتیم/پدر/مادر/فرزند/سرپرست/سایر)؛ برخلاف
        // RequestType (که نوعِ کل پرونده است)، این دقیقاً نقش خودِ همین عضو
        // را مشخص می‌کند تا بشود مثلاً فقط «یتیم»‌ها را جستجو کرد.
        private ComboBox cmbMRole;

        // نوارهای صفحه‌بندیِ نتایج (زیرساخت مقیاس‌پذیری — Helpers/GridPager).
        private Helpers.GridPager _headPager;
        private Helpers.GridPager _memberPager;

        // آموزش — چرا این پرچم لازم شد: خروجی‌های Excel/Word/PDF در این فرم از
        // روی محتوای همان جدولِ روی صفحه ساخته می‌شوند. با آمدنِ صفحه‌بندی،
        // جدول فقط یک صفحه (مثلاً ۱۰۰ ردیف) دارد، پس خروجی‌ها بی‌سروصدا ناقص
        // می‌شدند — یعنی قابلیتی که کاربر داشت از بین می‌رفت. با این پرچم،
        // هنگام گرفتن خروجی یک‌بار همه‌ی نتایج بارگذاری می‌شود، فایل ساخته
        // می‌شود، و بعد جدول به همان صفحه‌ی قبلی برمی‌گردد.
        private bool _loadAllRows;

        // همه‌ی نتایج را موقتاً بارگذاری کن، خروجی را بگیر، بعد به حالت
        // صفحه‌بندی‌شده برگرد.
        private void ExportWithAllRows(Action reload, Action doExport)
        {
            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            _loadAllRows = true;
            try
            {
                reload();
                doExport();
            }
            finally
            {
                _loadAllRows = false;
                Cursor = previous;
                reload();   // جدول به همان صفحه‌ای که کاربر بود برمی‌گردد
            }
        }
        private DataGridView dgvMemberResults;

        public FrmAdvancedSearch()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "جستجوی پیشرفته  —  " + SecurityContext.CenterDisplay;
            RightToLeft = RightToLeft.Yes;
            // دکمه‌های نوار عنوان به راست؛ محتوا (FlowLayoutPanel + تب‌ها) با
            // RightToLeft=Yes و tabs.RightToLeftLayout=true درست RTL می‌ماند.
            RightToLeftLayout = false;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 1280, 720);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = UiTheme.FontBold(10F);
            tabs.RightToLeft = RightToLeft.Yes;
            tabs.RightToLeftLayout = true;

            TabPage tabHead = new TabPage("👤  جستجوی سرپرست");
            TabPage tabMember = new TabPage("👪  جستجوی اعضاء خانواده");
            tabHead.BackColor = UiTheme.Background;
            tabMember.BackColor = UiTheme.Background;

            BuildHeadSearchTab(tabHead);
            BuildMemberSearchTab(tabMember);

            tabs.TabPages.Add(tabHead);
            tabs.TabPages.Add(tabMember);

            Controls.Add(tabs);

            LoadHeadResults();
            LoadMemberResults();
        }

        // ══════════════════════════════════════════════════════════════════
        // تب ۱: جستجوی سرپرست (TblCase)
        // ══════════════════════════════════════════════════════════════════
        private void BuildHeadSearchTab(TabPage page)
        {
            txtHCode = new TextBox();
            txtHFormNo = new TextBox();
            txtHName = new TextBox();
            txtHFatherName = new TextBox();
            txtHTazkira = new TextBox();
            txtHPhone = new TextBox();
            txtHDistrict = new TextBox();
            txtHJob = new TextBox();
            txtHCoveredByOrgNames = new TextBox();

            cmbHProvince = MakeCombo("همه", Provinces);
            // منبع واحد: به‌جای آرایه‌ی هاردکد، از TblLookup (دسته RequestType/نوع پرونده) خوانده می‌شود.
            cmbHRequestType = MakeCombo("همه", Helpers.LookupHelper.GetValues("RequestType").ToArray());
            cmbHPriority = MakeCombo("همه", new[] { "اول", "دوم", "سوم" });
            // همان دو گزینه‌ی فرم پرونده، تا فیلتر دقیقاً با مقدار ذخیره‌شده بخواند.
            cmbHCoveredByOrg = MakeCombo("همه", new[] { "بله", "خیر" });
            cmbHSadat = MakeCombo("همه", new[] { "عام", "سادات" });
            cmbHReligion = MakeCombo("همه", new[] { "اهل تشیع", "اهل تسنن" });
            cmbHMarital = MakeCombo("همه", new[] { "مجرد", "متأهل", "مطلقه" });
            // آموزش — فیلتر «تحصیلات» به درخواست کاربر: چون مقادیر EducationLevel
            // سرپرست (ابتدایی/متوسط/عالی/لیسانس/دکترا/طلبه/بی‌سواد) با دسته‌های
            // دانشگاه/مکتب اعضای خانواده یکی نیست، این سه گزینه یک «رده‌بندی»
            // منطقی روی همان مقادیر هستند (نگاه کنید به GetHeadEducationTierSql).
            cmbHEducationTier = MakeCombo("همه", new[] { "دانشگاهی", "مکتبی", "سایر مقاطع" });
            cmbHDisabilityType = MakeCombo("همه", new[] { "جسمی", "ذهنی", "بینایی", "شنوایی", "گفتاری", "حسی" });
            // منبع واحد: به‌جای آرایه‌ی هاردکد، از TblLookup (دسته ServiceStatus) خوانده می‌شود.
            cmbHStatus = MakeCombo("همه", Helpers.LookupHelper.GetValues("ServiceStatus").ToArray());
            cmbHIdCardType = MakeCombo("همه", new[]
            {
                Helpers.IdCardHelper.Electronic, Helpers.IdCardHelper.Paper,
                Helpers.IdCardHelper.UnknownDisplay, Helpers.IdCardHelper.NoneDisplay
            });

            dtpHFrom = new Helpers.PersianDatePicker { ShowCheckBox = true, Checked = false };
            dtpHTo = new Helpers.PersianDatePicker { ShowCheckBox = true, Checked = false };

            var fields = new List<KeyValuePair<string, Control>>
            {
                new KeyValuePair<string, Control>("کد اختصاصی", txtHCode),
                new KeyValuePair<string, Control>("شماره فرم", txtHFormNo),
                new KeyValuePair<string, Control>("نام سرپرست", txtHName),
                new KeyValuePair<string, Control>("نام پدر سرپرست", txtHFatherName),
                new KeyValuePair<string, Control>("نوع تذکره", cmbHIdCardType),
                new KeyValuePair<string, Control>("شماره تذکره", txtHTazkira),
                new KeyValuePair<string, Control>("شماره تماس", txtHPhone),
                new KeyValuePair<string, Control>("ولایت", cmbHProvince),
                new KeyValuePair<string, Control>("ولسوالی", txtHDistrict),
                new KeyValuePair<string, Control>("نوع درخواست", cmbHRequestType),
                new KeyValuePair<string, Control>("اولویت‌بندی اقتصادی", cmbHPriority),
                new KeyValuePair<string, Control>("تحت پوشش دیگر مؤسسات", cmbHCoveredByOrg),
                new KeyValuePair<string, Control>("اسامی مؤسسات تحت پوشش", txtHCoveredByOrgNames),
                new KeyValuePair<string, Control>("سیادت", cmbHSadat),
                new KeyValuePair<string, Control>("مذهب", cmbHReligion),
                new KeyValuePair<string, Control>("وضعیت تأهل", cmbHMarital),
                new KeyValuePair<string, Control>("تحصیلات", cmbHEducationTier),
                new KeyValuePair<string, Control>("شغل", txtHJob),
                new KeyValuePair<string, Control>("نوع معلولیت", cmbHDisabilityType),
                new KeyValuePair<string, Control>("وضعیت خدمات", cmbHStatus),
                new KeyValuePair<string, Control>("از تاریخ", dtpHFrom),
                new KeyValuePair<string, Control>("تا تاریخ", dtpHTo),
            };

            TableLayoutPanel filterGrid = BuildFilterGrid(fields, 6);

            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 42, FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10, 4, 10, 4), BackColor = UiTheme.CardBack
            };

            Button btnSearch = UiTheme.CreateButton("جستجو", "⌕", UiTheme.Primary);
            btnSearch.Size = new Size(96, 32); btnSearch.Margin = new Padding(3);
            // جستجوی تازه یعنی مجموعه‌ی نتایج عوض شده؛ پس از صفحه‌ی اول شروع کن.
            btnSearch.Click += delegate
            {
                if (_headPager != null) _headPager.Reset();
                LoadHeadResults();
            };
            buttonRow.Controls.Add(btnSearch);

            Button btnExport = UiTheme.CreateSecondaryButton("خروجی Excel", "⇑");
            btnExport.Size = new Size(140, 32); btnExport.Margin = new Padding(3);
            btnExport.Click += delegate
            {
                ExportWithAllRows(LoadHeadResults,
                    delegate { ExportGridToExcel(dgvHeadResults, "جستجوی_سرپرست"); });
            };
            buttonRow.Controls.Add(btnExport);

            Button btnWordH = UiTheme.CreateSecondaryButton("خروجی Word", "➤");
            btnWordH.Size = new Size(140, 32); btnWordH.Margin = new Padding(3);
            btnWordH.Click += delegate
            {
                ExportWithAllRows(LoadHeadResults,
                    delegate { ExportGridToWord(dgvHeadResults, "گزارش_سرپرستان", "گزارش سرپرستان", HeadFilterSubtitle()); });
            };
            buttonRow.Controls.Add(btnWordH);

            Button btnPdfH = UiTheme.CreateSecondaryButton("خروجی PDF", "➤");
            btnPdfH.Size = new Size(140, 32); btnPdfH.Margin = new Padding(3);
            btnPdfH.Click += delegate
            {
                ExportWithAllRows(LoadHeadResults,
                    delegate { ExportGridToPdf(dgvHeadResults, "گزارش_سرپرستان", "گزارش سرپرستان", HeadFilterSubtitle()); });
            };
            buttonRow.Controls.Add(btnPdfH);

            dgvHeadResults = new DataGridView();
            dgvHeadResults.Dock = DockStyle.Fill;
            dgvHeadResults.ReadOnly = true;
            dgvHeadResults.AllowUserToAddRows = false;
            dgvHeadResults.AllowUserToDeleteRows = false;
            dgvHeadResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvHeadResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            UiTheme.StyleGrid(dgvHeadResults);
            UiTheme.ApplyPersianDateColumns(dgvHeadResults, "CaseDate");

            // صفحه‌بندیِ نتایج — جستجوی بدونِ فیلتر می‌تواند کلِ جدول را برگرداند؛
            // با صدهزار پرونده این یعنی چند ثانیه انتظار و ده‌ها مگابایت حافظه.
            // هیچ نتیجه‌ای حذف نمی‌شود؛ فقط صفحه‌به‌صفحه خوانده می‌شود.
            _headPager = new Helpers.GridPager();
            _headPager.PageChanged += delegate { LoadHeadResults(); };

            Panel gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            gridWrap.Controls.Add(dgvHeadResults);
            gridWrap.Controls.Add(_headPager);

            page.Controls.Add(gridWrap);
            page.Controls.Add(buttonRow);
            page.Controls.Add(filterGrid);
        }

        // ─── شبکه‌ی فیلترِ جمع‌وجور و بدون فضای خالی ─────────────────────────
        // آموزش — رفع بی‌نظمی/فضای‌خالی/اسکرول جستجوی پیشرفته: به‌جای
        // FlowLayoutPanel با Margin/ارتفاع ثابت (که فضای خالی و ردیف نصفه‌کات‌شده
        // می‌ساخت)، از TableLayoutPanel با ستون‌های Percent مساوی استفاده می‌شود؛
        // هر سلول با Dock=Fill دقیقاً کنار سلول بعدی می‌چسبد، و ارتفاع کانتینر
        // دقیقاً بر اساس تعداد ردیف‌های واقعی محاسبه می‌شود (هرگز اسکرول لازم
        // نیست، هرگز ردیف آخر کات نمی‌شود). چون این فرم RightToLeftLayout=false
        // دارد (فقط RightToLeft=Yes)، TableLayoutPanel ستون‌ها را خودکار آینه
        // نمی‌کند؛ پس فیلد اول را در دورترین ستونِ راست می‌گذاریم و به چپ می‌رویم.
        private TableLayoutPanel BuildFilterGrid(List<KeyValuePair<string, Control>> fields, int columns)
        {
            // ارتفاع ردیف باید دقیقاً به‌اندازه‌ی FieldBox باشد، وگرنه فیلدها
            // بریده می‌شوند (مقدار قبلی ۴۶ برای برچسب+ورودیِ ساده بود؛ ورودیِ
            // گردگوشه‌ی جدید بلندتر است).
            int RowHeight = Helpers.FieldBox.TotalHeight;
            int rows = (int)Math.Ceiling(fields.Count / (double)columns);

            var tbl = new TableLayoutPanel();
            tbl.Dock = DockStyle.Top;
            tbl.ColumnCount = columns;
            tbl.RowCount = rows;
            tbl.BackColor = UiTheme.CardBack;
            tbl.Padding = new Padding(10, 6, 10, 2);
            for (int c = 0; c < columns; c++)
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
            for (int r = 0; r < rows; r++)
                tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
            tbl.Height = rows * RowHeight + tbl.Padding.Top + tbl.Padding.Bottom;

            for (int i = 0; i < fields.Count; i++)
            {
                int row = i / columns;
                int colFromRight = i % columns;
                int col = columns - 1 - colFromRight; // شروع پرشدن از راست به چپ
                Panel cell = MakeCompactFieldPanel(fields[i].Key, fields[i].Value);
                tbl.Controls.Add(cell, col, row);
            }
            return tbl;
        }

        // یک فیلد جمع‌وجور: برچسب کوچک بالا + ورودی پایین، با فاصله‌ی یکنواخت و
        // کمِ ۳px از سلولِ جدول (بدون فضای خالی اضافه).
        private Panel MakeCompactFieldPanel(string labelText, Control input)
        {
            // آموزش — بازطراحی: به‌جای برچسبِ ساده + ورودیِ مربعی، همان
            // FieldBox بقیه‌ی فرم‌ها استفاده می‌شود (ورودیِ گردگوشه با حالت
            // Focus/Hover) تا ظاهر کل برنامه یکدست شود.
            var box = new Helpers.FieldBox(new Label(), labelText, input);
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(3, 2, 3, 2);

            // تراز برچسب دیگر اینجا دستی تنظیم نمی‌شود: خودِ FieldBox پس از
            // اتصال به فرم، وضعیتِ آینه‌بودنِ زنجیره‌ی والدها را می‌سنجد و
            // «راستِ بصری» را اعمال می‌کند (ResponsiveLayout.VisualRight).
            box.Caption.Font = UiTheme.FontBold(8.5F);

            return box;
        }

        // آموزش — رده‌بندی تحصیلات سرپرست به سه گروه، چون مقادیر خام
        // EducationLevel با دسته‌های دانشگاه/مکتب اعضای خانواده یکی نیست.
        private static string GetHeadEducationTierSql(string tier, SQLiteCommand cmd)
        {
            if (tier == "دانشگاهی")
            {
                return " AND EducationLevel IN ('لیسانس', 'دکترا', 'عالی')";
            }
            if (tier == "مکتبی")
            {
                return " AND EducationLevel IN ('ابتدایی', 'متوسط')";
            }
            if (tier == "سایر مقاطع")
            {
                return " AND (EducationLevel IS NULL OR EducationLevel NOT IN ('لیسانس', 'دکترا', 'عالی', 'ابتدایی', 'متوسط'))";
            }
            return "";
        }

        private void LoadHeadResults()
        {
            const string headSelect = @"
SELECT CasID, FormNo, Code, CaseNo, HeadFullName, HeadFatherName, HeadIdCardType, HeadTazkiraNo, Phone,
       Province, District, RequestType, PriorityLevel, HeadSadat, Religion, MaritalStatus,
       EducationLevel, Job, DisabilityType, CoveredByOrg, CoveredByOrgNames, ServiceStatus, CaseDate
FROM TblCase
WHERE 1 = 1 AND IsArchived = 0";

            StringBuilder sql = new StringBuilder(headSelect);

            // طولِ بخشِ ثابتِ اول را نگه می‌داریم تا بعداً بتوانیم فقط شرط‌های
            // اضافه‌شده را جدا کنیم و همان‌ها را به کوئریِ شمارش بدهیم.
            int wherePrefixLength = sql.Length;

            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand())
            {
                cmd.Connection = con;

                AddLikeFilter(sql, cmd, "Code", "@Code", txtHCode.Text);
                AddLikeFilter(sql, cmd, "FormNo", "@FormNo", txtHFormNo.Text);
                AddLikeFilter(sql, cmd, "HeadFullName", "@Name", txtHName.Text);
                AddLikeFilter(sql, cmd, "HeadFatherName", "@FatherName", txtHFatherName.Text);
                AddLikeFilter(sql, cmd, "HeadTazkiraNo", "@Tazkira", txtHTazkira.Text);
                AddLikeFilter(sql, cmd, "Phone", "@Phone", txtHPhone.Text);
                AddLikeFilter(sql, cmd, "District", "@District", txtHDistrict.Text);
                AddLikeFilter(sql, cmd, "Job", "@Job", txtHJob.Text);
                AddLikeFilter(sql, cmd, "CoveredByOrgNames", "@CovNames", txtHCoveredByOrgNames.Text);

                AddExactFilter(sql, cmd, "Province", "@Province", cmbHProvince.Text);
                AddExactFilter(sql, cmd, "RequestType", "@ReqType", cmbHRequestType.Text);
                AddExactFilter(sql, cmd, "PriorityLevel", "@Priority", cmbHPriority.Text);
                AddExactFilter(sql, cmd, "HeadSadat", "@Sadat", cmbHSadat.Text);
                AddExactFilter(sql, cmd, "Religion", "@Religion", cmbHReligion.Text);
                AddExactFilter(sql, cmd, "MaritalStatus", "@Marital", cmbHMarital.Text);
                AddExactFilter(sql, cmd, "DisabilityType", "@DisabType", cmbHDisabilityType.Text);
                AddExactFilter(sql, cmd, "CoveredByOrg", "@CovByOrg", cmbHCoveredByOrg.Text);
                AddExactFilter(sql, cmd, "ServiceStatus", "@Status", cmbHStatus.Text);
                AddIdCardFilter(sql, cmd, "HeadIdCardType", "HeadTazkiraNo", "@HIdCard", cmbHIdCardType.Text);

                if (cmbHEducationTier.Text != "همه")
                    sql.Append(GetHeadEducationTierSql(cmbHEducationTier.Text, cmd));

                if (dtpHFrom.Checked)
                {
                    sql.Append(" AND CaseDate >= @FromDate");
                    cmd.Parameters.AddWithValue("@FromDate", dtpHFrom.Value.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                }
                if (dtpHTo.Checked)
                {
                    sql.Append(" AND CaseDate <= @ToDate");
                    cmd.Parameters.AddWithValue("@ToDate", dtpHTo.Value.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                }

                int cid = SecurityContext.CenterFilterId;
                if (cid != 0)
                {
                    sql.Append(" AND CenterID = @CenterID");
                    cmd.Parameters.AddWithValue("@CenterID", cid);
                }

                // فقط شرط‌هایی که فیلترها اضافه کرده‌اند (بدونِ SELECT و FROM)
                string whereTail = sql.ToString().Substring(wherePrefixLength);

                con.Open();

                // ابتدا تعدادِ کلِ نتایج — تا نوارِ صفحه‌بندی بداند چند صفحه هست.
                // همان cmd و همان پارامترها استفاده می‌شود، پس شرط‌ها دقیقاً یکی است.
                long total = 0;
                cmd.CommandText = "SELECT COUNT(1) FROM TblCase WHERE 1 = 1 AND IsArchived = 0" + whereTail;
                object scalar = cmd.ExecuteScalar();
                if (scalar != null && scalar != DBNull.Value) total = Convert.ToInt64(scalar);

                if (_headPager != null) _headPager.SetTotal(total);

                sql.Append(" ORDER BY CasID DESC");

                // هنگام گرفتن خروجی، همه‌ی نتایج لازم است (توضیح در _loadAllRows).
                if (!_loadAllRows)
                {
                    int take = _headPager != null ? _headPager.PageSize : 100;
                    int skip = _headPager != null ? _headPager.Offset : 0;
                    sql.Append(" LIMIT @Take OFFSET @Skip");
                    cmd.Parameters.AddWithValue("@Take", take);
                    cmd.Parameters.AddWithValue("@Skip", skip);
                }

                cmd.CommandText = sql.ToString();

                using (var reader = cmd.ExecuteReader())
                {
                    DataTable table = new DataTable();
                    table.Load(reader);
                    dgvHeadResults.DataSource = table;
                }
            }

            ApplyHeadGridHeaders();
        }

        private void ApplyHeadGridHeaders()
        {
            SetHeader(dgvHeadResults, "FormNo", "شماره فرم");
            SetHeader(dgvHeadResults, "Code", "کد اختصاصی");
            SetHeader(dgvHeadResults, "CaseNo", "شماره پرونده");
            SetHeader(dgvHeadResults, "HeadFullName", "نام سرپرست");
            SetHeader(dgvHeadResults, "HeadFatherName", "نام پدر سرپرست");
            SetHeader(dgvHeadResults, "HeadIdCardType", "نوع تذکره");
            SetHeader(dgvHeadResults, "HeadTazkiraNo", "شماره تذکره");
            SetHeader(dgvHeadResults, "Phone", "شماره تماس");
            SetHeader(dgvHeadResults, "Province", "ولایت");
            SetHeader(dgvHeadResults, "District", "ولسوالی");
            SetHeader(dgvHeadResults, "RequestType", "نوع درخواست");
            SetHeader(dgvHeadResults, "PriorityLevel", "اولویت‌بندی اقتصادی");
            SetHeader(dgvHeadResults, "HeadSadat", "سیادت");
            SetHeader(dgvHeadResults, "Religion", "مذهب");
            SetHeader(dgvHeadResults, "MaritalStatus", "وضعیت تأهل");
            SetHeader(dgvHeadResults, "EducationLevel", "تحصیلات");
            SetHeader(dgvHeadResults, "Job", "شغل");
            SetHeader(dgvHeadResults, "DisabilityType", "نوع معلولیت");
            SetHeader(dgvHeadResults, "CoveredByOrg", "تحت پوشش دیگر مؤسسات");
            SetHeader(dgvHeadResults, "CoveredByOrgNames", "اسامی مؤسسات تحت پوشش");
            SetHeader(dgvHeadResults, "ServiceStatus", "وضعیت خدمات");
            SetHeader(dgvHeadResults, "CaseDate", "تاریخ تشکیل");
            if (dgvHeadResults.Columns.Contains("CasID"))
                dgvHeadResults.Columns["CasID"].Visible = false;
        }

        // ══════════════════════════════════════════════════════════════════
        // تب ۲: جستجوی اعضاء خانواده (TblFamily + TblCase والد)
        // ══════════════════════════════════════════════════════════════════
        private void BuildMemberSearchTab(TabPage page)
        {
            txtMName = new TextBox();
            txtMFatherName = new TextBox();
            txtMTazkira = new TextBox();
            txtMSkill = new TextBox();
            txtMDistrict = new TextBox();

            cmbMProvince = MakeCombo("همه", Provinces);
            cmbMIdCardType = MakeCombo("همه", new[]
            {
                Helpers.IdCardHelper.Electronic, Helpers.IdCardHelper.Paper,
                Helpers.IdCardHelper.UnknownDisplay, Helpers.IdCardHelper.NoneDisplay
            });
            cmbMGender = MakeCombo("همه", new[] { "دختر", "پسر" });
            cmbMEducationTier = MakeCombo("همه", new[] { "دانشگاهی", "مکتبی", "سایر مقاطع" });
            cmbMMarital = MakeCombo("همه", new[] { "مجرد", "متأهل", "مطلقه" });
            cmbMReligion = MakeCombo("همه", new[] { "اهل تشیع", "اهل تسنن" });
            cmbMPhysical = MakeCombo("همه", new[] { "سالم", "معلول", "مریض" });
            cmbMDisabilityType = MakeCombo("همه", new[] { "جسمی", "ذهنی", "بینایی", "شنوایی", "گفتاری", "حسی" });
            // منبع واحد: به‌جای آرایه‌ی هاردکد، از TblLookup (دسته ServiceStatus) خوانده می‌شود.
            cmbMStatus = MakeCombo("همه", Helpers.LookupHelper.GetValues("ServiceStatus").ToArray());

            // آموزش — فیلتر «صنف» (درخواست کاربر): مقادیر همان ۱ تا ۱۲ هستند
            // که فرم اعضای خانواده هم برای GradeLevel استفاده می‌کند، تا
            // جستجو دقیقاً با داده‌ی ثبت‌شده هم‌خوان باشد.
            var grades = new List<string>();
            for (int g = 1; g <= 12; g++) grades.Add(g.ToString());
            cmbMGrade = MakeCombo("همه", grades.ToArray());

            // همان فهرستِ تبِ سرپرست، تا دو تب هم‌خوان بمانند.
            // منبع واحد: به‌جای آرایه‌ی هاردکد، از TblLookup (دسته RequestType/نوع پرونده) خوانده می‌شود.
            cmbMRequestType = MakeCombo("همه", Helpers.LookupHelper.GetValues("RequestType").ToArray());
            cmbMRole = MakeCombo("همه", Helpers.LookupHelper.GetValues("MemberRole").ToArray());

            var fields = new List<KeyValuePair<string, Control>>
            {
                new KeyValuePair<string, Control>("نام عضو", txtMName),
                new KeyValuePair<string, Control>("نام پدر عضو", txtMFatherName),
                new KeyValuePair<string, Control>("نوع تذکره", cmbMIdCardType),
                new KeyValuePair<string, Control>("شماره تذکره", txtMTazkira),
                new KeyValuePair<string, Control>("جنسیت", cmbMGender),
                new KeyValuePair<string, Control>("نقش عضو", cmbMRole),
                new KeyValuePair<string, Control>("ولایت", cmbMProvince),
                new KeyValuePair<string, Control>("ولسوالی", txtMDistrict),
                new KeyValuePair<string, Control>("تحصیلات", cmbMEducationTier),
                new KeyValuePair<string, Control>("صنف", cmbMGrade),
                new KeyValuePair<string, Control>("وضعیت تأهل", cmbMMarital),
                new KeyValuePair<string, Control>("مذهب", cmbMReligion),
                new KeyValuePair<string, Control>("وضعیت جسمی", cmbMPhysical),
                new KeyValuePair<string, Control>("نوع معلولیت", cmbMDisabilityType),
                new KeyValuePair<string, Control>("مهارت", txtMSkill),
                new KeyValuePair<string, Control>("نوع درخواست", cmbMRequestType),
                new KeyValuePair<string, Control>("وضعیت خدمات", cmbMStatus),
            };

            TableLayoutPanel filterGrid = BuildFilterGrid(fields, 6);

            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 42, FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10, 4, 10, 4), BackColor = UiTheme.CardBack
            };

            Button btnSearch = UiTheme.CreateButton("جستجو", "⌕", UiTheme.Primary);
            btnSearch.Size = new Size(96, 32); btnSearch.Margin = new Padding(3);
            btnSearch.Click += delegate
            {
                if (_memberPager != null) _memberPager.Reset();
                LoadMemberResults();
            };
            buttonRow.Controls.Add(btnSearch);

            Button btnExport = UiTheme.CreateSecondaryButton("خروجی Excel", "⇑");
            btnExport.Size = new Size(140, 32); btnExport.Margin = new Padding(3);
            btnExport.Click += delegate
            {
                ExportWithAllRows(LoadMemberResults,
                    delegate { ExportGridToExcel(dgvMemberResults, "جستجوی_اعضاء_خانواده"); });
            };
            buttonRow.Controls.Add(btnExport);

            Button btnWordM = UiTheme.CreateSecondaryButton("خروجی Word", "➤");
            btnWordM.Size = new Size(140, 32); btnWordM.Margin = new Padding(3);
            btnWordM.Click += delegate
            {
                ExportWithAllRows(LoadMemberResults,
                    delegate { ExportGridToWord(dgvMemberResults, "گزارش_اعضای_خانواده", "گزارش اعضای خانواده", MemberFilterSubtitle()); });
            };
            buttonRow.Controls.Add(btnWordM);

            Button btnPdfM = UiTheme.CreateSecondaryButton("خروجی PDF", "➤");
            btnPdfM.Size = new Size(140, 32); btnPdfM.Margin = new Padding(3);
            btnPdfM.Click += delegate
            {
                ExportWithAllRows(LoadMemberResults,
                    delegate { ExportGridToPdf(dgvMemberResults, "گزارش_اعضای_خانواده", "گزارش اعضای خانواده", MemberFilterSubtitle()); });
            };
            buttonRow.Controls.Add(btnPdfM);

            dgvMemberResults = new DataGridView();
            dgvMemberResults.Dock = DockStyle.Fill;
            dgvMemberResults.ReadOnly = true;
            dgvMemberResults.AllowUserToAddRows = false;
            dgvMemberResults.AllowUserToDeleteRows = false;
            dgvMemberResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvMemberResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            UiTheme.StyleGrid(dgvMemberResults);

            _memberPager = new Helpers.GridPager();
            _memberPager.PageChanged += delegate { LoadMemberResults(); };

            Panel gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            gridWrap.Controls.Add(dgvMemberResults);
            gridWrap.Controls.Add(_memberPager);

            page.Controls.Add(gridWrap);
            page.Controls.Add(buttonRow);
            page.Controls.Add(filterGrid);
        }

        private void LoadMemberResults()
        {
            StringBuilder sql = new StringBuilder(@"
SELECT f.FamID, c.Code AS [کد پرونده], c.HeadFullName AS [نام سرپرست],
       f.MemberName, f.MemberFatherName, f.MemberIdCardType, f.MemberTazkiraNo, f.Gender,
       f.MemberRole,
       c.Province, c.District, f.MemberEducation, f.GradeLevel AS [صنف],
       f.MaritalStatus, f.Religion,
       f.PhysicalStatus, f.HasDisability, f.Skill,
       -- نوع درخواستِ پرونده روی هر سطرِ عضو، تا ایتام از سایر بخش‌ها جدا شود.
       c.RequestType,
       -- «وضعیت خدمات» همیشه از سطحِ پرونده (c) خوانده می‌شود، نه از ستونِ
       -- هم‌نامِ TblFamily. تحت حمایت بودن یک ویژگیِ پرونده است؛ ستونِ سطحِ
       -- عضو می‌تواند مقدارِ متفاوت و گمراه‌کننده داشته باشد. فیلترِ پایین هم
       -- روی همین ستون اعمال می‌شود تا گرید و فیلتر با هم بخوانند.
       c.ServiceStatus
FROM TblFamily f
JOIN TblCase c ON c.CasID = f.CasID
WHERE 1 = 1 AND c.IsArchived = 0");

            // مثل تبِ سرپرست: طولِ بخشِ ثابت را نگه می‌داریم تا شرط‌ها را جدا
            // کنیم و همان‌ها را به کوئریِ شمارش بدهیم.
            int wherePrefixLength = sql.Length;

            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand())
            {
                cmd.Connection = con;

                AddLikeFilter(sql, cmd, "f.MemberName", "@Name", txtMName.Text);
                AddLikeFilter(sql, cmd, "f.MemberFatherName", "@FatherName", txtMFatherName.Text);
                AddLikeFilter(sql, cmd, "f.MemberTazkiraNo", "@Tazkira", txtMTazkira.Text);
                AddLikeFilter(sql, cmd, "c.District", "@District", txtMDistrict.Text);
                AddLikeFilter(sql, cmd, "f.Skill", "@Skill", txtMSkill.Text);

                AddExactFilter(sql, cmd, "f.Gender", "@Gender", cmbMGender.Text);
                AddExactFilter(sql, cmd, "f.MemberRole", "@MRole", cmbMRole.Text);
                AddExactFilter(sql, cmd, "c.Province", "@Province", cmbMProvince.Text);
                AddExactFilter(sql, cmd, "f.MaritalStatus", "@Marital", cmbMMarital.Text);
                AddExactFilter(sql, cmd, "f.Religion", "@Religion", cmbMReligion.Text);
                AddExactFilter(sql, cmd, "f.PhysicalStatus", "@Physical", cmbMPhysical.Text);
                AddExactFilter(sql, cmd, "f.HasDisability", "@DisabType", cmbMDisabilityType.Text);
                AddExactFilter(sql, cmd, "c.ServiceStatus", "@Status", cmbMStatus.Text);
                AddExactFilter(sql, cmd, "c.RequestType", "@MReqType", cmbMRequestType.Text);
                AddIdCardFilter(sql, cmd, "f.MemberIdCardType", "f.MemberTazkiraNo", "@MIdCard", cmbMIdCardType.Text);
                // فیلتر صنف (درخواست کاربر) — دقیقاً مثل بقیه‌ی فیلترهای
                // «تطابق کامل»؛ مقدار «همه» توسط AddExactFilter نادیده گرفته
                // می‌شود، پس رفتار پیش‌فرض هیچ تغییری نمی‌کند.
                AddExactFilter(sql, cmd, "f.GradeLevel", "@Grade", cmbMGrade.Text);

                // آموزش — فیلتر تحصیلات اعضا مستقیماً روی مقادیر واقعی MemberEducation
                // است (بر خلاف سرپرست) چون این‌جا دسته «دانشگاه»/«مکتب» عیناً وجود دارد.
                if (cmbMEducationTier.Text == "دانشگاهی")
                {
                    sql.Append(" AND f.MemberEducation = @Edu");
                    cmd.Parameters.AddWithValue("@Edu", "دانشگاه");
                }
                else if (cmbMEducationTier.Text == "مکتبی")
                {
                    sql.Append(" AND f.MemberEducation = @Edu");
                    cmd.Parameters.AddWithValue("@Edu", "مکتب");
                }
                else if (cmbMEducationTier.Text == "سایر مقاطع")
                {
                    sql.Append(" AND (f.MemberEducation IS NULL OR f.MemberEducation NOT IN ('دانشگاه', 'مکتب'))");
                }

                int cid = SecurityContext.CenterFilterId;
                if (cid != 0)
                {
                    sql.Append(" AND c.CenterID = @CenterID");
                    cmd.Parameters.AddWithValue("@CenterID", cid);
                }

                string whereTail = sql.ToString().Substring(wherePrefixLength);

                con.Open();

                long total = 0;
                cmd.CommandText = @"
SELECT COUNT(1)
FROM TblFamily f
JOIN TblCase c ON c.CasID = f.CasID
WHERE 1 = 1 AND c.IsArchived = 0" + whereTail;
                object scalar = cmd.ExecuteScalar();
                if (scalar != null && scalar != DBNull.Value) total = Convert.ToInt64(scalar);

                if (_memberPager != null) _memberPager.SetTotal(total);

                sql.Append(" ORDER BY f.FamID DESC");

                if (!_loadAllRows)
                {
                    int take = _memberPager != null ? _memberPager.PageSize : 100;
                    int skip = _memberPager != null ? _memberPager.Offset : 0;
                    sql.Append(" LIMIT @Take OFFSET @Skip");
                    cmd.Parameters.AddWithValue("@Take", take);
                    cmd.Parameters.AddWithValue("@Skip", skip);
                }

                cmd.CommandText = sql.ToString();

                using (var reader = cmd.ExecuteReader())
                {
                    DataTable table = new DataTable();
                    table.Load(reader);
                    dgvMemberResults.DataSource = table;
                }
            }

            ApplyMemberGridHeaders();
        }

        private void ApplyMemberGridHeaders()
        {
            SetHeader(dgvMemberResults, "MemberName", "نام عضو");
            SetHeader(dgvMemberResults, "MemberFatherName", "نام پدر عضو");
            SetHeader(dgvMemberResults, "MemberIdCardType", "نوع تذکره");
            SetHeader(dgvMemberResults, "MemberTazkiraNo", "شماره تذکره");
            SetHeader(dgvMemberResults, "Gender", "جنسیت");
            SetHeader(dgvMemberResults, "MemberRole", "نقش عضو");
            SetHeader(dgvMemberResults, "Province", "ولایت");
            SetHeader(dgvMemberResults, "District", "ولسوالی");
            SetHeader(dgvMemberResults, "MemberEducation", "تحصیلات");
            SetHeader(dgvMemberResults, "MaritalStatus", "وضعیت تأهل");
            SetHeader(dgvMemberResults, "Religion", "مذهب");
            SetHeader(dgvMemberResults, "PhysicalStatus", "وضعیت جسمی");
            SetHeader(dgvMemberResults, "HasDisability", "نوع معلولیت");
            SetHeader(dgvMemberResults, "Skill", "مهارت");
            SetHeader(dgvMemberResults, "RequestType", "نوع درخواست");
            SetHeader(dgvMemberResults, "ServiceStatus", "وضعیت خدمات");
            if (dgvMemberResults.Columns.Contains("FamID"))
                dgvMemberResults.Columns["FamID"].Visible = false;
        }

        // ══════════════════════════════════════════════════════════════════
        // کمکی‌های مشترک
        // ══════════════════════════════════════════════════════════════════
        // ─── فیلتر «نوع تذکره» (بخش ۴) ────────────────────────────────────
        // آموزش — چرا AddExactFilter کافی نبود: دو گزینه‌ی «بدون تذکره» و
        // «نوع نامشخص» مقدارِ ذخیره‌شده در دیتابیس نیستند بلکه «نبودِ مقدار»
        // را توصیف می‌کنند، پس شرطشان باید روی هر دو ستونِ نوع و شماره باشد.
        private static void AddIdCardFilter(
            StringBuilder sql, SQLiteCommand cmd,
            string typeColumn, string numberColumn, string parameterName, string value)
        {
            string v = (value ?? "").Trim();
            if (v.Length == 0 || v == "همه") return;

            if (v == Helpers.IdCardHelper.NoneDisplay)
            {
                sql.Append(" AND COALESCE(" + typeColumn + ", '') = ''" +
                           " AND COALESCE(" + numberColumn + ", '') = ''");
                return;
            }

            if (v == Helpers.IdCardHelper.UnknownDisplay)
            {
                sql.Append(" AND COALESCE(" + typeColumn + ", '') = ''" +
                           " AND COALESCE(" + numberColumn + ", '') <> ''");
                return;
            }

            sql.Append(" AND " + typeColumn + " = " + parameterName);
            cmd.Parameters.AddWithValue(parameterName, v);
        }

        private ComboBox MakeCombo(string firstItem, string[] items)
        {
            ComboBox cmb = new ComboBox();
            cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            cmb.Items.Add(firstItem);
            cmb.Items.AddRange(items);
            cmb.SelectedIndex = 0;
            return cmb;
        }

        private static void SetHeader(DataGridView grid, string column, string header)
        {
            if (grid.Columns.Contains(column))
                grid.Columns[column].HeaderText = header;
        }

        private void AddLikeFilter(StringBuilder sql, SQLiteCommand cmd, string column, string parameter, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            sql.Append(" AND " + column + " LIKE " + parameter);
            cmd.Parameters.AddWithValue(parameter, "%" + value.Trim() + "%");
        }

        private void AddExactFilter(StringBuilder sql, SQLiteCommand cmd, string column, string parameter, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "همه")
                return;

            sql.Append(" AND " + column + " = " + parameter);
            cmd.Parameters.AddWithValue(parameter, value.Trim());
        }

        // خروجی اکسل نتیجه فیلترشده فعلی (همان چیزی که در گرید نمایش داده می‌شود).
        // ─── زیرعنوان گزارش: خلاصه‌ی فیلترِ ولایت/ولسوالی + تعداد + تاریخ ───
        private string HeadFilterSubtitle()
        {
            return BuildSubtitle(cmbHProvince.Text, txtHDistrict.Text, dgvHeadResults);
        }

        private string MemberFilterSubtitle()
        {
            return BuildSubtitle(cmbMProvince.Text, txtMDistrict.Text, dgvMemberResults);
        }

        private string BuildSubtitle(string province, string district, DataGridView grid)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(province) && province.Trim() != "همه")
                parts.Add("ولایت: " + province.Trim());
            if (!string.IsNullOrWhiteSpace(district))
                parts.Add("ولسوالی: " + district.Trim());
            parts.Add("مرکز: " + SecurityContext.CenterDisplay);
            int count = grid.Rows.Count - (grid.Rows.Count > 0 && grid.Rows[grid.Rows.Count - 1].IsNewRow ? 1 : 0);
            parts.Add("تعداد: " + count);
            parts.Add("تاریخ: " + Helpers.PersianDateHelper.ToPersianDateString(DateTime.Now));
            return string.Join("   |   ", parts);
        }

        // ─── خروجی جمعی Word از نتایج فیلترشده ───────────────────────────────
        private void ExportGridToWord(DataGridView grid, string suggestedName, string title, string subtitle)
        {
            if (!HasRows(grid))
            {
                UiTheme.ShowWarning(this, "داده‌ای برای خروجی Word وجود ندارد.");
                return;
            }
            try
            {
                string outputPath = BuildReportPath(suggestedName, "WordReports", ".docx");
                if (outputPath == null) return;

                Cursor old = Cursor; Cursor = Cursors.WaitCursor;
                try { GridReportExporter.ExportToWord(grid, title, subtitle, outputPath); }
                finally { Cursor = old; }

                UiTheme.ShowSuccess(this, "خروجی Word ذخیره شد:" + Environment.NewLine + outputPath);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ساخت خروجی Word: " + ex.Message);
            }
        }

        // ─── خروجی جمعی PDF (از روی Word با LibreOffice) ─────────────────────
        private void ExportGridToPdf(DataGridView grid, string suggestedName, string title, string subtitle)
        {
            if (!HasRows(grid))
            {
                UiTheme.ShowWarning(this, "داده‌ای برای خروجی PDF وجود ندارد.");
                return;
            }
            if (!GridReportExporter.IsPdfAvailable())
            {
                UiTheme.ShowWarning(this,
                    "برای خروجی PDF باید LibreOffice نصب باشد. می‌توانید خروجی Word بگیرید و آن را به PDF تبدیل کنید.");
                return;
            }
            try
            {
                string docxPath = BuildReportPath(suggestedName, "PdfReports", ".docx");
                if (docxPath == null) return;

                Cursor old = Cursor; Cursor = Cursors.WaitCursor;
                string pdfPath;
                try
                {
                    GridReportExporter.ExportToWord(grid, title, subtitle, docxPath);
                    pdfPath = GridReportExporter.ConvertDocxToPdf(docxPath);
                }
                finally { Cursor = old; }

                try { if (File.Exists(docxPath)) File.Delete(docxPath); } catch { }

                UiTheme.ShowSuccess(this, "خروجی PDF ذخیره شد:" + Environment.NewLine + pdfPath);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ساخت خروجی PDF: " + ex.Message);
            }
        }

        private static bool HasRows(DataGridView grid)
        {
            foreach (DataGridViewRow r in grid.Rows)
                if (!r.IsNewRow) return true;
            return false;
        }

        private string BuildReportPath(string suggestedName, string subFolder, string extension)
        {
            string rootFolder = FileHelper.GetOrChooseBaseRootFolder();
            if (string.IsNullOrWhiteSpace(rootFolder))
            {
                UiTheme.ShowWarning(this, "محل ذخیره فایل‌ها مشخص نیست");
                return null;
            }
            string reportsFolder = Path.Combine(rootFolder, subFolder);
            Directory.CreateDirectory(reportsFolder);
            string safeName = FileHelper.CleanName(suggestedName);
            return Path.Combine(reportsFolder,
                safeName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + extension);
        }

        private void ExportGridToExcel(DataGridView grid, string suggestedName)
        {
            DataTable table = grid.DataSource as DataTable;
            if (table == null || table.Rows.Count == 0)
            {
                UiTheme.ShowWarning(this, "داده‌ای برای خروجی اکسل وجود ندارد.");
                return;
            }

            try
            {
                string rootFolder = FileHelper.GetOrChooseBaseRootFolder();
                if (string.IsNullOrWhiteSpace(rootFolder))
                {
                    UiTheme.ShowWarning(this, "محل ذخیره فایل‌ها مشخص نیست");
                    return;
                }

                string reportsFolder = Path.Combine(rootFolder, "ExcelReports");
                Directory.CreateDirectory(reportsFolder);

                string safeName = FileHelper.CleanName(suggestedName);
                string outputPath = Path.Combine(reportsFolder,
                    safeName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".xlsx");

                using (XLWorkbook workbook = new XLWorkbook())
                {
                    IXLWorksheet ws = workbook.Worksheets.Add("نتیجه جستجو");
                    ws.RightToLeft = true;
                    IXLTable excelTable = ws.Cell(1, 1).InsertTable(table, "Data", true);
                    excelTable.Theme = XLTableTheme.TableStyleMedium2;
                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(outputPath);
                }

                UiTheme.ShowSuccess(this, "خروجی اکسل ذخیره شد:" + Environment.NewLine + outputPath);
            }
            catch (Exception ex)
            {
                UiTheme.ShowError(this, "خطا در ساخت خروجی اکسل: " + ex.Message);
            }
        }
    }
}

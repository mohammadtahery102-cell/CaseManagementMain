using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using DrawingImage = System.Drawing.Image;

namespace CaseManagement
{
    public partial class FrmCase : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        private int currentCaseId = 0;

        // آموزش — فاز ۱ (تب اعضاء داخل FrmCase): نمونهٔ تک‌عمرِ FrmFamily که
        // به‌جای پنجرهٔ مودالِ قبلی، داخل تب «اعضاء خانواده» جاسازی می‌شود.
        // فقط بار اول که کاربر روی آن تب می‌رود ساخته می‌شود (EnsureFamilyEmbedded)
        // و بعدش تا بسته‌شدنِ FrmCase زنده می‌ماند؛ با تعویض پرونده فقط
        // RefreshForCase صدا زده می‌شود، نه ساخت نمونهٔ تازه.
        private FrmFamily _embeddedFamily;

        // آموزش — رفع بارگذاری تکراری: true یعنی «دیتای نمونهٔ embedded با
        // currentCaseId فعلی هم‌خوان نیست». SyncMembersTab وقتی تب دیده
        // نمی‌شود به‌جای رفرش فوری فقط این پرچم را می‌زند؛ EnsureFamilyEmbedded
        // وقتی کاربر واقعاً به تب می‌رود، فقط اگر این پرچم ست باشد رفرش
        // می‌کند. بدون این پرچم، هم تعویضِ سادهٔ تب (بدون تغییر پرونده) و هم
        // تعویضِ پرونده از تب‌های دیگر، هرکدام یک کوئری اضافهٔ نامرئی می‌زدند.
        private bool _familyDirty = false;

        // آموزش — فاز A4: همان الگوی _embeddedFamily/_familyDirty، برای تب
        // «اسناد پرونده» (FrmDocs embedded).
        private FrmDocs _embeddedDocs;
        private bool _docsDirty = false;

        private string selectedHeadPhotoSource = "";
        private string selectedFamilyPhotoSource = "";

        private string savedHeadPhotoPath = "";
        private string savedFamilyPhotoPath = "";

        // منبع واحد: از TblLookup (دسته ServiceStatus) خوانده می‌شود.
        // اگر دیتابیس هنوز آماده نباشد LookupHelper فهرست خالی برمی‌گرداند؛
        // در آن حالت به فهرست مرجعِ CaseDomain برمی‌گردیم، وگرنه کمبو خالی
        // می‌ماند و IsAllowedServiceStatus هر مقداری را رد می‌کند (ذخیره قفل می‌شود).
        private string[] serviceStatuses
        {
            get
            {
                string[] fromDb = Helpers.LookupHelper.GetValues(Helpers.CaseDomain.CatServiceStatus).ToArray();
                return fromDb.Length > 0 ? fromDb : Helpers.CaseDomain.ServiceStatuses;
            }
        }

        // آموزش — این دو ثابت به Helpers/PhotoRules منتقل شدند تا محدودیتِ
        // حجمِ عکس فقط یک جا تعریف شود (قبلاً هر فرم عددِ خودش را داشت).
        private const long MinFamilyPhotoFileSizeBytes = Helpers.PhotoRules.MinGroupBytes;
        private const long MaxFamilyPhotoFileSizeBytes = Helpers.PhotoRules.MaxGroupBytes;

        private int _pendingOpenCaseId = 0;

        // آموزش — فیلترِ واردشده از داشبورد (ولایت/ولسوالی/وضعیت خدمات): وقتی
        // کاربر از داشبورد با یک فیلترِ فعال روی دکمه‌ی «پرونده‌ها» می‌زند،
        // همان فیلتر اینجا اعمال می‌شود — هم روی فهرستِ پیش‌فرض (LoadCases)
        // هم روی جستجوی نوارِ بالای گرید (SearchCasesGrid)، تا کاربر مجبور
        // نباشد دوباره همان فیلتر را تکرار کند.
        private string _incomingFilterProvince = "";
        private string _incomingFilterDistrict = "";
        private string _incomingFilterServiceStatus = "";
        private Panel _dashboardFilterBanner;

        public FrmCase()
        {
            InitializeComponent();
            IdCardHelper.Attach(cmbHeadIdCardType, txtHeadTazkiraNo);
            ApplyCustomTheme();
            AttachShortcuts();
        }

        // باز کردن با همان فیلترِ فعالِ داشبورد (ولایت/ولسوالی/وضعیت خدمات).
        // مقدارِ خالی یعنی «بدون فیلتر»، دقیقاً مثلِ رفتارِ پیش‌فرضِ داشبورد.
        public FrmCase(string filterProvince, string filterDistrict, string filterServiceStatus) : this()
        {
            _incomingFilterProvince = filterProvince ?? "";
            _incomingFilterDistrict = filterDistrict ?? "";
            _incomingFilterServiceStatus = filterServiceStatus ?? "";
        }

        // میان‌بُرهای صفحه‌کلید. Enter (رفتن به فیلد بعدی) جداگانه در
        // FrmCase_KeyDown می‌ماند و دست‌نخورده است.
        private void AttachShortcuts()
        {
            Helpers.FormShortcuts.For(this)
                .Save(btnSave)
                .New(btnNew)
                .Edit(btnEdit)
                .Delete(btnDelete)
                .Search(btnSearch)
                .Print(btnPrint);
        }

        // باز کردن مستقیم یک پرونده‌ی مشخص برای ویرایش (مثلاً از تب «کیفیت داده»
        // داشبورد با راست‌کلیک). پرونده پس از بارگذاری فرم به‌طور خودکار لود می‌شود.
        public FrmCase(int openCaseId) : this()
        {
            _pendingOpenCaseId = openCaseId;
        }

        // ─── اعمال ظاهر یکسان روی فرمی که با طراح (Designer) ساخته شده ──────
        // آموزش: چون این فرم ده‌ها کنترل با مکان/اندازه ثابت دارد، به‌جای
        // جابه‌جایی تک‌تک آن‌ها (ریسک شکستن Layout)، فقط رنگ/فونت/آیکون
        // یکسان روی همان چیدمان موجود اعمال می‌شود.
        private void ApplyCustomTheme()
        {
            UiTheme.ApplySweep(this);

            // تأکیدِ بصری روی «نوع پرونده» باید *بعد از* ApplySweep اعمال شود:
            // آن متد رنگِ همهٔ Labelها را روی TextDark می‌نشاند، پس هر رنگی که
            // در Designer داده شود بی‌صدا پاک می‌گردد.
            if (label4 != null)
            {
                label4.Font      = UiTheme.FontBold(UiTheme.SizeSmall);
                label4.ForeColor = UiTheme.Primary;
            }

            UiTheme.SetButtonIcon(btnSave, "✔");
            UiTheme.SetButtonIcon(btnNew, "+");
            UiTheme.SetButtonIcon(btnEdit, "✎");
            UiTheme.SetButtonIcon(btnDelete, "✕");
            UiTheme.SetButtonIcon(btnSearch, "⌕");
            UiTheme.SetButtonIcon(btnBrowsePhoto, "▤");
            UiTheme.SetButtonIcon(btnBrowseFamilyPhoto, "♥");
            UiTheme.SetButtonIcon(btnFamily, "♥");
            UiTheme.SetButtonIcon(btnDocs, "▤");
            UiTheme.SetButtonIcon(btnChooseStorageFolder, "⚙");
            UiTheme.SetButtonIcon(btnExportExcel, "⇑");
            UiTheme.SetButtonIcon(btnBatchExport, "⇑");
            UiTheme.SetButtonIcon(btnPrint, "🖨");

            btnDelete.BackColor = UiTheme.Danger;
            btnDelete.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Danger, 0.18f);
            btnDelete.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Danger, 0.08f);

            btnSave.BackColor = UiTheme.Success;
            btnSave.FlatAppearance.MouseOverBackColor = ControlPaint.Light(UiTheme.Success, 0.18f);
            btnSave.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(UiTheme.Success, 0.08f);

            // آموزش — دسته‌بندی بصری دکمه‌ها (به درخواست کاربر برای نظم و
            // حرفه‌ای بودن): دکمه‌های «عملیات» (جدید/ذخیره/ویرایش/حذف/جستجو)
            // پُررنگ می‌مانند، اما دکمه‌های «خروجی‌ها» به سبک ثانویه (روشن با
            // کادر) درمی‌آیند تا در یک نگاه دو گروه مجزا دیده شوند.
            // خروجی‌های تکیِ «ورد» و «پی دی اف» حذف شدند؛ همان کار را
            // «خروجی جمعی» با بازهٔ شمارهٔ فرم انجام می‌دهد.
            Button[] exportButtons = { btnPrint, btnExportExcel, btnBatchExport };
            foreach (Button b in exportButtons)
            {
                b.BackColor = Color.White;
                b.ForeColor = UiTheme.Primary;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = UiTheme.Primary;
                b.FlatAppearance.MouseOverBackColor = UiTheme.HoverTint;
                b.FlatAppearance.MouseDownBackColor = ControlPaint.Light(UiTheme.Primary, 0.7f);
            }

            AddGuardianCardButton();
        }

        // آموزش — دکمه «کارت شناسایی سرپرست» به‌صورت پویا کنار دکمه‌های خروجی
        // موجود اضافه می‌شود (نه در Designer) تا چیدمان FlowLayoutPanel دست‌نخورده
        // بماند؛ چون آن پنل WrapContents=true دارد، افزودن یک دکمه دیگر کاملاً امن است.
        private void AddGuardianCardButton()
        {
            Button btnGuardianCard = UiTheme.CreateSecondaryButton("کارت شناسایی", "🪪");
            btnGuardianCard.Size = new Size(128, 32);
            btnGuardianCard.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGuardianCard.Margin = new Padding(3, 3, 3, 3);
            btnGuardianCard.TabStop = false;
            btnGuardianCard.Click += delegate
            {
                if (!CaseManagement.Enterprise.PermissionService.Require("GuardianCard.Print"))
                {
                    Msg.Show("کاربر اجازه چاپ کارت شناسایی را ندارد.");
                    return;
                }

                if (currentCaseId == 0)
                {
                    Msg.Show("اول پرونده را ذخیره یا جستجو کن");
                    return;
                }
                using (var frm = new GuardianCardIntegration.FrmGuardianCardPreview(currentCaseId))
                    frm.ShowDialog(this);
            };

            Button btnGuardianCardBatch = UiTheme.CreateSecondaryButton("چاپ جمعی کارت‌ها", "🪪");
            btnGuardianCardBatch.Size = new Size(150, 32);
            btnGuardianCardBatch.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGuardianCardBatch.Margin = new Padding(3, 3, 3, 3);
            btnGuardianCardBatch.TabStop = false;
            btnGuardianCardBatch.Click += delegate
            {
                using (var frm = new GuardianCardIntegration.FrmGuardianCardBatchPrint())
                    frm.ShowDialog(this);
            };

            // ─── گام ۱: دو منوی تجمیعی به‌جای دکمه‌های پراکنده ──────────────
            // «نامهٔ انتقالی» و «وکالت موقت» دکمهٔ مستقل نیستند؛ هر دو داخلِ
            // منوی فورم‌های رسمی‌اند (از رجیستریِ CaseOfficialForms می‌آیند).
            _menuOfficialForms = BuildOfficialFormsMenu();
            Button btnFormsCenter = MenuButton("فرم‌ها و برگه‌های رسمی", "📄", _menuOfficialForms);
            btnFormsCenter.Size = new Size(200, 32);

            _menuExports = BuildExportMenu();
            Button btnExportsMenu = MenuButton("خروجی‌ها و چاپ", "🖨", _menuExports);
            btnExportsMenu.Size = new Size(160, 32);

            // ⚠ لنگر مستقیماً خودِ نوارِ پایین است، نه Parentِ یک دکمهٔ دیگر.
            // از گام ۱ به بعد btnExportExcel روی هیچ کانتینری نمی‌نشیند (منوی
            // «خروجی‌ها و چاپ» جایش را گرفته)، پس Parent آن null بود و این
            // چهار کنترل — دو منو و دو دکمهٔ کارت — هرگز به فرم اضافه
            // نمی‌شدند؛ یعنی خروجی‌ها، فورم‌های رسمی و چاپ کارت از این فرم
            // در دسترس نبودند.
            Control parent = bottomActionsRow;
            if (parent != null)
            {
                // ترتیبِ نهاییِ گروهِ دوم: خروجی‌ها ← فورم‌ها ← کارت‌ها.
                parent.Controls.Add(btnExportsMenu);
                parent.Controls.SetChildIndex(btnExportsMenu, parent.Controls.IndexOf(btnHistory) + 1);
                parent.Controls.Add(btnFormsCenter);
                parent.Controls.SetChildIndex(btnFormsCenter, parent.Controls.IndexOf(btnExportsMenu) + 1);
                parent.Controls.Add(btnGuardianCard);
                parent.Controls.SetChildIndex(btnGuardianCard, parent.Controls.IndexOf(btnFormsCenter) + 1);
                parent.Controls.Add(btnGuardianCardBatch);
                parent.Controls.SetChildIndex(btnGuardianCardBatch, parent.Controls.IndexOf(btnGuardianCard) + 1);

                // ⚠ رفعِ باگِ «دکمهٔ آخر کار نمی‌کند»: با این چهار دکمه، نوارِ
                // پایین ۱۹ کنترل دارد و در عرض‌های معمول به سه خط می‌شکند.
                // AdjustBottomBarHeight فقط در OnResize صدا زده می‌شد، یعنی
                // ارتفاعِ ردیف با موقعیتِ *قبلیِ* کنترل‌ها حساب می‌گردید و
                // آخرین دکمه (وکالت موقت) بیرونِ ناحیهٔ دیدهٔ ردیف می‌افتاد —
                // دیده نمی‌شد و کلیک هم نمی‌گرفت. رویدادِ Layout بعد از
                // چیدمانِ واقعیِ FlowLayoutPanel اجرا می‌شود، پس اندازه‌گیری
                // درست است.
                parent.Layout += delegate { AdjustBottomBarHeight(); };
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // گام ۱ — تجمیعِ دکمه‌ها در دو منوی واحد.
        //
        // آموزش — چرا منو و نه حذف: هیچ قابلیتی حذف نشده. هر آیتمِ منو دقیقاً
        // همان هندلرِ قبلی را صدا می‌زند (btnPrint_Click، btnExportCaseFile_Click،
        // …) پس رفتار عوض نمی‌شود و ریسکِ گم‌شدنِ کارکرد صفر است — فقط مسیرِ
        // دسترسی از «۱۸ دکمهٔ پهلوی‌هم» به «۲ منوی دسته‌بندی‌شده» تغییر کرد.
        //
        // چرا ContextMenuStrip: کنترلِ استانداردِ ویندوز است، RTL را بومی
        // پشتیبانی می‌کند و هیچ کنترلِ سفارشیِ تازه‌ای وارد پروژه نمی‌کند.
        // ═══════════════════════════════════════════════════════════════════
        private ContextMenuStrip BuildExportMenu()
        {
            var menu = new ContextMenuStrip
            {
                RightToLeft = RightToLeft.Yes,
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 9F)
            };

            menu.Items.Add(MenuItem("چاپ خلاصهٔ پرونده", delegate { btnPrint_Click(this, EventArgs.Empty); }));
            menu.Items.Add(MenuItem("پروندهٔ کامل (اکسل / چاپ)…", delegate { btnExportCaseFile_Click(this, EventArgs.Empty); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuItem("خروجی جمعی (ورد / پی‌دی‌اف)…", delegate { btnBatchExport_Click(this, EventArgs.Empty); }));
            menu.Items.Add(MenuItem("گزارش اکسل همهٔ پرونده‌ها…", delegate { btnExportExcel_Click(this, EventArgs.Empty); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuItem("فهرست اسناد پرونده", delegate { btnDocs_Click(this, EventArgs.Empty); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuItem("محل ذخیرهٔ فایل‌ها…", delegate { btnChooseStorageFolder_Click(this, EventArgs.Empty); }));

            return menu;
        }

        // منوی فورم‌های رسمی. فهرست از CaseOfficialForms.Available می‌آید —
        // یعنی افزودنِ فورمِ تازه در آینده هیچ تغییری در این فرم لازم ندارد.
        private ContextMenuStrip BuildOfficialFormsMenu()
        {
            var menu = new ContextMenuStrip
            {
                RightToLeft = RightToLeft.Yes,
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 9F)
            };

            menu.Opening += delegate { RebuildOfficialFormsMenu(menu); };
            return menu;
        }

        // هر بار که منو باز می‌شود از نو ساخته می‌شود، چون فورمِ پیشنهادی به
        // نوعِ پروندهٔ *جاری* بستگی دارد و کاربر ممکن است بینِ دو باز کردن،
        // پروندهٔ دیگری را انتخاب کرده باشد.
        private void RebuildOfficialFormsMenu(ContextMenuStrip menu)
        {
            menu.Items.Clear();

            var db = new DAL.DatabaseHelper();
            string requestTypeCode = "";
            try
            {
                if (currentCaseId > 0)
                    requestTypeCode = Helpers.CaseOfficialForms.RequestTypeCodeOf(db, currentCaseId);
            }
            catch { /* نوعِ پرونده ناشناخته ⇒ فهرستِ پیش‌فرض */ }

            List<Helpers.CaseFormDef> forms = Helpers.CaseOfficialForms.Available(requestTypeCode);

            bool first = true;
            foreach (Helpers.CaseFormDef def in forms)
            {
                if (def == null) continue;

                Helpers.CaseFormDef captured = def;
                string title = def.Title ?? "فورم";

                // فورمِ نخستِ فهرست همان پیشنهادِ سیستم بر پایهٔ نوعِ پرونده
                // است؛ با ★ مشخص می‌شود ولی کاربر آزاد است هرکدام را بزند.
                if (first) title = "★  " + title;

                var item = MenuItem(title, delegate { OpenOfficialForm(captured.Key); });
                item.Enabled = captured.IsAvailable;
                if (!captured.IsAvailable)
                    item.ToolTipText = "فایل قالب این فورم در پوشهٔ Templates/Forms موجود نیست.";

                menu.Items.Add(item);

                if (first)
                {
                    menu.Items.Add(new ToolStripSeparator());
                    first = false;
                }
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuItem("مرکز فورم‌های رسمی…", delegate { OpenOfficialForm(null); }));
        }

        private void OpenOfficialForm(string preselectKey)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا جستجو کن");
                return;
            }

            Helpers.CaseOfficialForms.ShowCenter(this, new DAL.DatabaseHelper(),
                currentCaseId, txtCode.Text.Trim(), null, preselectKey);
        }

        private static ToolStripMenuItem MenuItem(string text, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += onClick;
            return item;
        }

        // دکمه‌ای که منو را زیرِ خودش باز می‌کند.
        private static Button MenuButton(string text, string icon, ContextMenuStrip menu)
        {
            Button button = UiTheme.CreateSecondaryButton(text, icon);
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            button.Margin = new Padding(3, 3, 3, 3);
            button.TabStop = false;
            button.Click += delegate
            {
                menu.Show(button, new Point(0, button.Height));
            };
            return button;
        }

        // ─── ورقهٔ وکالت موقت سرپرستی ایتام ──────────────────────────────────
        // آموزش — این متد قبلاً خودش دیالوگِ FrmDocxForm را می‌ساخت و خانه‌ها
        // را از کادرهای روی همین فرم می‌خواند. حالا داده‌اش از CaseFormTokens
        // (یعنی مستقیم از دیتابیس) می‌آید و دیالوگش همان «مرکز فورم‌های رسمی»
        // است که FrmDocs هم از آن استفاده می‌کند — درخواستِ کاربر: همهٔ
        // فورم‌ها یک‌جا. رفتارِ دکمه عوض نشده: هنوز وکالت موقتِ همین پرونده را
        // باز می‌کند، فقط از پیش انتخاب‌شده در فهرستِ فورم‌ها.
        private void ShowGuardianProxyForm()
        {
            Helpers.CaseOfficialForms.ShowCenter(this, new DAL.DatabaseHelper(),
                currentCaseId, txtCode.Text.Trim(), null, "PROXY");
        }

        // ─── ستون‌های واقعیِ جستجو در TblCase — منبعِ واحد، هم برای نوار
        // جستجوی سریعِ بالای فرم (Quick Search Bar، فاز A2) و هم هرجای دیگری
        // که قبلاً از همین آرایه استفاده می‌کرد.
        private static readonly string[] CaseSearchTypeColumns =
            { "Code", "HeadFullName", "HeadTazkiraNo", "Phone" };

        // ─── مقیدکردن عرضِ نوار دکمه‌ها به عرضِ والد ───────────────────────────
        // آموزش — چرا لازم است: نوار دکمه‌ها AutoSize دارد تا وقتی دکمه‌ها به خط
        // بعد می‌شکنند ارتفاعش زیاد شود و هیچ دکمه‌ای پنهان نماند. اما AutoSize
        // در FlowLayoutPanel هر دو بُعد را بزرگ می‌کند؛ یعنی به‌جای شکستنِ خط،
        // خودِ نوار در عرض رشد می‌کرد و دکمه‌های انتهایی از لبه‌ی فرم بیرون
        // می‌زدند (در تستِ تصویری «چاپ جمعی کارت‌ها» دقیقاً همین‌طور بریده شد).
        // با تعیین MaximumSize.Width برابرِ عرضِ والد، رشدِ افقی متوقف می‌شود و
        // AutoSize فقط ارتفاع را تنظیم می‌کند — یعنی شکستِ خط درست کار می‌کند.
        private void ConstrainBottomActionsWidth()
        {
            AdjustBottomBarHeight();
        }

        // ارتفاعِ نوار دکمه‌ها را با «تعدادِ خطوطی که واقعاً اشغال کرده‌اند»
        // تطبیق می‌دهد. چون Dock=Fill عرض را به والد مقید می‌کند، شکستِ خط درست
        // انجام می‌شود؛ فقط باید ردیفِ نگه‌دارنده به‌اندازه‌ی کافی بلند باشد.
        // این کار در هر تغییر اندازه انجام می‌شود، پس روی هر عرض/رزولوشنی
        // (و با هر تعداد دکمه‌ای که در آینده اضافه شود) هیچ دکمه‌ای پنهان نمی‌ماند.
        private bool _adjustingBottomBar;

        private void AdjustBottomBarHeight()
        {
            if (bottomActionsRow == null || rootLayout == null) return;

            // تغییرِ ارتفاعِ ردیف خودش یک Layout تازه می‌سازد؛ بدونِ این نگهبان
            // رویداد Layout بی‌نهایت بار خودش را صدا می‌زند.
            if (_adjustingBottomBar) return;
            // آموزش — فاز A2: با افزودنِ ردیفِ جدیدِ «نوار جستجوی سریع» در بالای
            // rootLayout، نوار دکمه‌ها از ردیفِ اندیس ۲ به ۳ منتقل شد. این
            // اندیس اینجا (و شرطِ Count) همراهش به‌روز شد تا این متد روی ردیفِ
            // درست کار کند — وگرنه بی‌سروصدا روی ردیفِ فیلدها/گرید اثر می‌گذاشت.
            if (rootLayout.RowStyles.Count < 4) return;

            int contentBottom = 0;
            foreach (Control c in bottomActionsRow.Controls)
            {
                int b = c.Bottom + c.Margin.Bottom;
                if (b > contentBottom) contentBottom = b;
            }
            if (contentBottom <= 0) return;

            float needed = contentBottom + bottomActionsRow.Padding.Bottom + 4;
            if (needed < 52f) needed = 52f;   // حداقلِ یک خط

            RowStyle bottomRow = rootLayout.RowStyles[3];
            if (bottomRow.SizeType != SizeType.Absolute || Math.Abs(bottomRow.Height - needed) > 1f)
            {
                _adjustingBottomBar = true;
                try
                {
                    bottomRow.SizeType = SizeType.Absolute;
                    bottomRow.Height = needed;
                }
                finally { _adjustingBottomBar = false; }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            AdjustBottomBarHeight();
        }

        // فرم را داخل ناحیه‌ی کاری صفحه جا می‌دهد (مستقل از DPI/اندازه‌ی صفحه).
        private void FitToScreen()
        {
            try
            {
                System.Drawing.Rectangle wa = Screen.FromControl(this).WorkingArea;
                int w = Math.Min(Width, wa.Width - 8);
                int h = Math.Min(Height, wa.Height - 8);
                Size = new Size(w, h);
                Location = new System.Drawing.Point(
                    wa.Left + Math.Max(0, (wa.Width - w) / 2),
                    wa.Top + Math.Max(0, (wa.Height - h) / 2));
            }
            catch { /* اگر به هر دلیل نشد، اندازه‌ی طراحی حفظ می‌شود */ }
        }

        private void FrmCase_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // آموزش — فاز A2: چون فرم KeyPreview=true دارد، این متد قبل از
                // KeyDown خودِ کنترلِ فوکوس‌شده اجرا می‌شود. برای چهار فیلدِ نوار
                // جستجوی سریع، Enter باید جستجو را اجرا کند (QuickSearchField_KeyDown)
                // نه اینکه فوکوس را به فیلد بعدی ببرد؛ پس رفتار سراسریِ زیر
                // برای آن‌ها رد می‌شود. از Focused (نه ActiveControl) استفاده شد
                // چون این فیلدها داخل چند لایه Panel/TableLayoutPanel تودرتو
                // هستند و ActiveControl فرم لزوماً کنترلِ واقعیِ تودرتو را برنمی‌گرداند.
                if (txtQsCode.Focused || txtQsHeadName.Focused || txtQsTazkira.Focused || txtQsPhone.Focused)
                    return;

                e.SuppressKeyPress = true;
                this.SelectNextControl(this.ActiveControl, true, true, true, true);
            }
        }
        // آموزش — بازنویسی کامل به درخواست کاربر (بند ۵): باگ اصلی این بود که
        // TabIndex یک شمارنده مشترک بین فیلدهای سه گروه (grpHead/grpPhysical/
        // grpCase) بود، اما TabIndex در WinForms فقط داخل هر Container به‌طور
        // مستقل معنی دارد — شمارنده مشترک هیچ ترتیب واقعی‌ای بین گروه‌های
        // مختلف نمی‌ساخت. حالا هر گروه شمارنده محلی خودش را دارد (از صفر) و
        // ترتیب گروه‌ها/کانتینرهای اصلی هم در Designer.cs صریح تنظیم شده
        // (grpHead→grpPhysical→grpCase، و fieldsPanel→bottomBar→leftPanel).
        // نتیجه: Tab دقیقاً به ترتیب بصری بالا-به-پایین/راست-به-چپ هر ردیف
        // طی می‌شود و بعد از آخرین فیلد (شرح وضعیت فوری) مستقیم به «ذخیره»
        // می‌رسد، بدون هیچ پرش به کنترل‌های سمت چپ فرم (گرید/عکس‌ها).
        private void SetTabOrder()
        {
            // فیلدهای اتومات / غیرقابل تایپ
            txtFormNo.ReadOnly = true;
            txtFormNo.TabStop = false;
            txtFormNo.BackColor = SystemColors.Control;

            txtPhotoPath.ReadOnly = true;
            txtPhotoPath.TabStop = false;

            txtFamilyPhotoPath.ReadOnly = true;
            txtFamilyPhotoPath.TabStop = false;

            // ─── گروه ۱: مشخصات کلی سرپرست (ترتیب بصری هر ردیف: راست سپس چپ) ─
            int h = 0;
            txtHeadFullName.TabIndex = h++;
            txtHeadFatherName.TabIndex = h++;
            txtHeadSadat.TabIndex = h++;
            txtReligion.TabIndex = h++;
            txtHeadTazkiraNo.TabIndex = h++;
            txtHeadOriginalResidence.TabIndex = h++;
            txtHeadCurrentResidence.TabIndex = h++;
            txtRelationshipToFamily.TabIndex = h++;
            txtPhone.TabIndex = h++;
            txtRelativePhone.TabIndex = h++;
            txtMaritalStatus.TabIndex = h++;
            txtEducationLevel.TabIndex = h++;
            txtJob.TabIndex = h++;
            txtSkill.TabIndex = h++;

            // ─── گروه ۲: مشخصات جسمی ──────────────────────────────────────────
            int p = 0;
            txtDisabilityType.TabIndex = p++;
            txtDisabilityDegree.TabIndex = p++;

            // ─── گروه ۳: مشخصات پرونده ─────────────────────────────────────────
            int c = 0;
            txtCode.TabIndex = c++;
            txtCaseNo.TabIndex = c++;
            txtZone.TabIndex = c++;
            txtProvince.TabIndex = c++;
            txtDistrict.TabIndex = c++;
            txtSite.TabIndex = c++;
            txtRequestType.TabIndex = c++;
            txtPriorityLevel.TabIndex = c++;
            txtMigrationCardType.TabIndex = c++;
            txtCoveredByOrg.TabIndex = c++;
            txtCoveredByOrgNames.TabIndex = c++;
            dtpCaseDate.TabIndex = c++;
            txtServiceStatus.TabIndex = c++;
            txtSuspensionReason.TabIndex = c++;
            txtStopReason.TabIndex = c++;
            txtLocationAddress.TabIndex = c++;
            txtSurveyors.TabIndex = c++;
            dtpSurveyDate.TabIndex = c++;
            txtUrgentSituation.TabIndex = c++;

            // دکمه‌ها از مسیر Tab خارج می‌شوند — به‌جز «ذخیره» که آخرین توقف
            // Tab بعد از فیلدهاست (طبق درخواست کاربر).
            btnSave.TabStop = true;
            btnSave.TabIndex = 0;
            btnEdit.TabStop = false;
            btnDelete.TabStop = false;
            btnNew.TabStop = false;
            btnSearch.TabStop = false;
            btnDocs.TabStop = false;
            btnFamily.TabStop = false;
            btnExportExcel.TabStop = false;
            btnBatchExport.TabStop = false;
            btnPrint.TabStop = false;
            btnChooseStorageFolder.TabStop = false;
            btnBrowsePhoto.TabStop = false;
            btnBrowseFamilyPhoto.TabStop = false;

            // کنترل‌های سمت چپ فرم (گرید/فیلتر/عکس) هرگز نباید در مسیر Tab
            // فیلدها باشند؛ leftPanel در Designer.cs آخرین TabIndex ریشه را
            // دارد، اما این‌ها هم برای اطمینان مضاعف صریحاً خاموش می‌شوند.
            cmbServiceStatusFilter.TabStop = false;
            dgvCases.TabStop = false;
        }

        private void ConfigureServiceStatusControls()
        {
            txtServiceStatus.Items.Clear();
            txtServiceStatus.Items.AddRange(serviceStatuses);

            if (txtServiceStatus.SelectedIndex < 0)
                txtServiceStatus.SelectedIndex = 0;

            cmbServiceStatusFilter.SelectedIndexChanged -= cmbServiceStatusFilter_SelectedIndexChanged;

            cmbServiceStatusFilter.Items.Clear();
            cmbServiceStatusFilter.Items.Add("همه");
            cmbServiceStatusFilter.Items.AddRange(serviceStatuses);

            if (cmbServiceStatusFilter.SelectedIndex < 0)
                cmbServiceStatusFilter.SelectedIndex = 0;

            cmbServiceStatusFilter.SelectedIndexChanged += cmbServiceStatusFilter_SelectedIndexChanged;

            // نمایش/پنهان‌کردن «اسامی مؤسسات تحت پوشش» با تغییر پاسخِ بله/خیر.
            txtCoveredByOrg.SelectedIndexChanged += delegate { UpdateCoveredByOrgNamesVisibility(); };
            UpdateCoveredByOrgNamesVisibility();
        }

        // فیلدهای تعلیق («دلیل تعلیق» الزامی + «یادداشت» اختیاری) فقط وقتی
        // وضعیت «قطع» یا «قطع موقت» است نمایش داده می‌شوند.
        private void TxtServiceStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateStopReasonVisibility();
        }

        private void TxtRequestType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRequestTypeSectionVisibility();
        }

        // Phase 3 (بازبینی) — منبعِ واحدِ نمایش/پنهانیِ بخش‌های اختصاصیِ نوع
        // درخواست. فقط پرچم‌های TblRequestType (از طریق ReferenceDataService)
        // را می‌خواند — هیچ RequestTypeID/Code ای اینجا هاردکد نیست، پس افزودنِ
        // نوعِ جدید یا تغییرِ پرچم‌ها از تنظیمات، بدونِ کامپایلِ دوباره اثر می‌کند.
        // فیلدهای موجودِ DisabilityType/DisabilityDegree/MigrationCardType
        // عمداً دست‌نخورده می‌مانند (رفتارِ فعلی‌شان حفظ می‌شود، فقط فیلدهای
        // *تازه* در سه گروهِ زیر پنهان/آشکار می‌شوند).
        private void UpdateRequestTypeSectionVisibility()
        {
            var sections = Helpers.ReferenceDataService.GetRequestTypeSectionsByName(txtRequestType.Text.Trim());

            SetFieldGroupVisible(orphanSectionFields, sections.ShowOrphanSection);
            SetFieldGroupVisible(disabilitySectionFields, sections.ShowDisabilitySection);
            SetFieldGroupVisible(migrantSectionFields, sections.ShowMigrantSection);

            // کارتِ هر بخش هم با خودِ بخش پنهان/آشکار می‌شود، وگرنه برای نوعِ
            // نامربوط یک قابِ خالی با سربرگ روی صفحه می‌ماند. فیلدها بالاتر
            // جداگانه هم پنهان می‌شوند تا اگر کارتی وجود نداشت رفتار قبلی حفظ شود.
            SetCardVisible(cardOrphanInfo,     sections.ShowOrphanSection);
            SetCardVisible(cardDisabilityInfo, sections.ShowDisabilitySection);
            SetCardVisible(cardMigrantInfo,    sections.ShowMigrantSection);
            // Phase 4 — بخشِ «اطلاعات سرپرست» (ایتام/بی‌سرپرست/بدسرپرست).
            SetFieldGroupVisible(guardianSectionFields, sections.ShowGuardianSection);
            SetCardVisible(cardGuardianInfo, sections.ShowGuardianSection);
            // Phase 7 — تبِ «نمایندهٔ قانونی».
            SetRepresentativeTabVisible(sections.ShowRepresentativeSection);

            // A1 — تضادِ «سالم است» با نوعِ پروندهٔ معلول.
            ApplyDisabledHeadRule(txtRequestType.Text.Trim());
        }

        // کدِ پایدارِ نوعِ درخواست. طبقِ قاعدهٔ پروژه منطقِ برنامه با Code
        // مقایسه می‌کند، نه با Name (که فقط نمایشی و قابلِ ترجمه است).
        private const string RequestTypeCodeDisabled = "DISABLED";

        // ─── A1 ──────────────────────────────────────────────────────────────
        // در پروندهٔ «معلول» سرپرست خودش همان ذینفعِ معلول است (قانونِ
        // طبقه‌بندی: پرونده با نوعِ درخواستِ سرپرستش شناخته می‌شود)، پس تیکِ
        // «سالم است» با نوعِ پرونده در تضادِ منطقی است.
        //
        // باگی که رفع می‌کند: ClearForm تیک را پیش‌فرض «سالم» می‌گذارد و
        // UpdateHeadPhysicalState دو فیلدِ نوع/درجهٔ معلولیت را غیرفعال *و
        // خالی* می‌کند. این دو کنترل در تبِ دیگری («مشخصات جسمی») هستند و
        // طبقِ تصمیم #۱۲ عمداً تابعِ نمایشِ بخشِ معلولیت نیستند — پس کاربری
        // که «معلول» را انتخاب کرده هیچ نشانه‌ای نمی‌بیند و پرونده با
        // DisabilityType/Degree خالی ذخیره می‌شود. همین دو فیلد:
        //   • در TblRequiredField برای DISABLED الزامی‌اند ⇒ درصدِ تکمیل
        //     هرگز به ۱۰۰ نمی‌رسید،
        //   • تنها فیلدهای معلولیتی‌اند که به RDLC/کارت/جستجو/داشبورد
        //     می‌رسند ⇒ پرونده در جستجوی معلولیت پیدا نمی‌شد و روی گزارش
        //     خالی چاپ می‌شد.
        //
        // عمداً فقط تیک برداشته و دو فیلد فعال می‌شوند؛ Enabledِ خودِ
        // چک‌باکس دست‌نخورده می‌ماند تا منطقِ حالتِ فقط‌خواندنی
        // (SetCaseEditMode) هیچ تغییری نکند.
        private void ApplyDisabledHeadRule(string requestTypeName)
        {
            var option = Helpers.ReferenceDataService.FindRequestTypeByName(requestTypeName);

            bool isDisabledCase = option != null && string.Equals(
                option.Code, RequestTypeCodeDisabled, StringComparison.OrdinalIgnoreCase);

            if (!isDisabledCase) return;

            if (chkHeadHealthy.Checked)
            {
                // بدونِ هندلر، وگرنه UpdateHeadPhysicalState دوباره صدا زده
                // می‌شود و ترتیبِ فعال‌سازی پیچیده می‌گردد.
                chkHeadHealthy.CheckedChanged -= ChkHeadHealthy_CheckedChanged;
                chkHeadHealthy.Checked = false;
                chkHeadHealthy.CheckedChanged += ChkHeadHealthy_CheckedChanged;
            }

            txtDisabilityType.Enabled = true;
            txtDisabilityDegree.Enabled = true;
        }

        // برخلافِ فیلدها، TabPage خاصیتِ Visibleِ کارایی ندارد — WinForms
        // آن را می‌پذیرد ولی تب روی نوار می‌ماند؛ تنها راهِ واقعی
        // حذف/افزودن از TabPages است. جایگاهِ اصلیِ تب (پیش از بازدید
        // میدانی) حفظ می‌شود، وگرنه با هر تغییرِ نوعِ درخواست
        // تب به انتهای نوار پرتاب می‌شد.
        private void SetRepresentativeTabVisible(bool visible)
        {
            if (tabsCase == null || tabRepresentative == null) return;

            // مجوزِ اختصاصیِ مشاهده (Phase 7) — دادهٔ نماینده مشخصاتِ هویتیِ
            // شخصِ ثالث است. پیش‌فرض برای هر سه نقش روشن است، پس تبِ امروز
            // برای هیچ‌کس ناپدید نمی‌شود؛ ولی حالا می‌توان آن را از یک نقش
            // گرفت بدونِ دست‌زدن به دسترسیِ پرونده.
            if (visible && !CaseManagement.Enterprise.PermissionService.HasPermission("Representative.View"))
                visible = false;

            bool present = tabsCase.TabPages.Contains(tabRepresentative);
            if (visible == present) return;

            if (visible)
            {
                int index = tabVisits != null && tabsCase.TabPages.Contains(tabVisits)
                    ? tabsCase.TabPages.IndexOf(tabVisits)
                    : tabsCase.TabPages.Count;
                tabsCase.TabPages.Insert(index, tabRepresentative);
            }
            else
            {
                // اگر کاربر دقیقاً روی همین تب باشد، حذفِ آن بدونِ
                // جابجاییِ انتخاب، فرم را روی تبِ خالی رها می‌کرد.
                if (tabsCase.SelectedTab == tabRepresentative && tabsCase.TabPages.Count > 1)
                    tabsCase.SelectedIndex = 0;
                tabsCase.TabPages.Remove(tabRepresentative);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 4 — ماژول‌های تخصصی: خواندن/نوشتن از طریق CaseModuleService.
        //
        // قاعدهٔ اعتبارسنجی و امتیازدهی (خواستهٔ صریح): فقط بخش‌های *دیده‌شده*
        // شرکت می‌کنند. پس ذخیره هم فقط برای ماژولی انجام می‌شود که پرچمش
        // روشن است؛ بخشِ پنهان نه ذخیره می‌شود، نه خطا می‌دهد، و چون ردیفش
        // ساخته نمی‌شود در درصدِ کامل‌بودن هم اثری ندارد.
        // ═══════════════════════════════════════════════════════════════════
        private void SaveCaseModules(int casId)
        {
            if (casId <= 0) return;

            var sections = Helpers.ReferenceDataService.GetRequestTypeSectionsByName(txtRequestType.Text.Trim());

            // ─── ایتام + سرپرست (هر دو در TblOrphan) ─────────────────────────
            if (sections.ShowOrphanSection || sections.ShowGuardianSection)
            {
                var values = new Dictionary<string, object>();
                var mirror = new Dictionary<string, object>();

                if (sections.ShowOrphanSection)
                {
                    values["MainResidenceProvince"] = TextOrNull(txtMainResidenceProvince.Text);
                    values["MainResidenceDistrict"] = TextOrNull(txtMainResidenceDistrict.Text);
                    values["MainResidenceVillage"]  = TextOrNull(txtMainResidenceVillage.Text);
                    values["FatherStatus"]          = TextOrNull(txtFatherStatus.Text);
                    values["FatherDeathCause"]      = TextOrNull(txtFatherDeathCause.Text);
                    values["FatherDeathDate"]       = DateOrNull(dtpFatherDeathDate);
                    values["MotherStatus"]          = TextOrNull(txtMotherStatus.Text);
                    values["SchoolName"]            = TextOrNull(txtOrphanSchoolName.Text);
                    values["EducationLevel"]        = TextOrNull(txtOrphanEducationLevel.Text);
                    values["IsStudent"]             = chkIsStudent.Checked ? 1 : 0;
                    values["Notes"]                 = TextOrNull(txtOrphanNotes.Text);

                    // آینه روی TblCase — فقط فیلدهایی که ستونِ هم‌معنا دارند.
                    // EducationLevel و SchoolName عمداً نیستند (توضیح در
                    // CaseModuleService: تحصیلاتِ سرپرست ≠ تحصیلاتِ کودک).
                    mirror["MainResidenceProvince"] = values["MainResidenceProvince"];
                    mirror["MainResidenceDistrict"] = values["MainResidenceDistrict"];
                    mirror["MainResidenceVillage"]  = values["MainResidenceVillage"];
                    mirror["FatherDeathCause"]      = values["FatherDeathCause"];
                }

                if (sections.ShowGuardianSection)
                {
                    values["GuardianName"]         = TextOrNull(txtGuardianName.Text);
                    values["GuardianRelationship"] = TextOrNull(txtGuardianRelationship.Text);

                    // Feature 3 — عکس پیش از نوشتنِ ردیف کپی می‌شود، وگرنه
                    // مسیرِ ذخیره‌شده به فایلِ موقتِ مبدأ (مثلاً Desktop)
                    // اشاره می‌کرد. حسابرسیِ تغییرِ عکس رایگان به‌دست می‌آید:
                    // CaseModuleService خودش تفاوتِ هر فیلد را در تایم‌لاین
                    // ثبت می‌کند، پس GuardianPhotoPath هم مثل بقیه دیده می‌شود.
                    savedGuardianPhotoPath = StoreGuardianPhoto(txtCode.Text.Trim());
                    values["GuardianPhotoPath"] = TextOrNull(savedGuardianPhotoPath);
                }

                Helpers.CaseModuleService.Save(Helpers.CaseModuleService.TableOrphan,
                    Helpers.CaseModuleService.TitleOrphan, casId, values, mirror);
            }

            // ─── معلولیت ─────────────────────────────────────────────────────
            if (sections.ShowDisabilitySection)
            {
                var values = new Dictionary<string, object>
                {
                    { "DisabilityType",        TextOrNull(txtDisabilityType.Text) },
                    { "DisabilityDegree",      TextOrNull(txtDisabilityDegree.Text) },
                    { "DisabilityCause",       TextOrNull(txtDisabilityCause.Text) },
                    { "DisabilityDescription", TextOrNull(txtDisabilityDescription.Text) },
                    { "SpecialNeeds",          TextOrNull(txtSpecialNeeds.Text) },
                    { "HasDisabilityCard",     TextOrNull(txtDisabilityCardStatus.Text) },
                    { "DisabilityCardNumber",  TextOrNull(txtDisabilityCardNumber.Text) },
                    { "CardIssuer",            TextOrNull(txtCardIssuer.Text) },
                    { "IssueDate",             DateOrNull(dtpDisabilityIssueDate) },
                    { "ExpiryDate",            DateOrNull(dtpDisabilityExpiryDate) },
                    { "Notes",                 TextOrNull(txtDisabilityNotes.Text) }
                };

                var mirror = new Dictionary<string, object>
                {
                    { "DisabilityType",        values["DisabilityType"] },
                    { "DisabilityDegree",      values["DisabilityDegree"] },
                    { "DisabilityCause",       values["DisabilityCause"] },
                    { "DisabilityDescription", values["DisabilityDescription"] },
                    { "SpecialNeeds",          values["SpecialNeeds"] },
                    { "DisabilityCardStatus",  values["HasDisabilityCard"] },
                    { "DisabilityCardNumber",  values["DisabilityCardNumber"] }
                };

                Helpers.CaseModuleService.Save(Helpers.CaseModuleService.TableDisability,
                    Helpers.CaseModuleService.TitleDisability, casId, values, mirror);
            }

            // ─── مهاجرت ──────────────────────────────────────────────────────
            if (sections.ShowMigrantSection)
            {
                int duration;
                bool hasDuration = int.TryParse(txtAssistanceDurationMonths.Text.Trim(), out duration);

                var values = new Dictionary<string, object>
                {
                    { "HasMigrationCard",    TextOrNull(txtHasMigrationCard.Text) },
                    { "MigrationCardType",   TextOrNull(txtMigrationCardType.Text) },
                    { "MigrationCardNumber", TextOrNull(txtMigrationCardNumber.Text) },
                    { "OriginCountry",       TextOrNull(txtOriginCountry.Text) },
                    { "DestinationCountry",  TextOrNull(txtDestinationCountry.Text) },
                    { "DepartureDate",       dtpDepartureDate.Value.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) },
                    { "ArrivalDate",         dtpArrivalDate.Value.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) },
                    // یک‌طرفه از کنترلِ عمومیِ موجود (توضیح در Designer).
                    { "MaritalStatus",       TextOrNull(txtMaritalStatus.Text) },
                    { "AssistanceDuration",  hasDuration ? (object)duration : null },
                    { "Notes",               TextOrNull(txtMigrantNotes.Text) }
                };

                var mirror = new Dictionary<string, object>
                {
                    { "HasMigrationCard",         values["HasMigrationCard"] },
                    { "MigrationCardNumber",      values["MigrationCardNumber"] },
                    { "DepartureDate",            values["DepartureDate"] },
                    { "ArrivalDate",              values["ArrivalDate"] },
                    { "AssistanceDurationMonths", values["AssistanceDuration"] }
                };

                Helpers.CaseModuleService.Save(Helpers.CaseModuleService.TableMigrant,
                    Helpers.CaseModuleService.TitleMigrant, casId, values, mirror);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Phase 7 — نمایندهٔ قانونی.
        //
        // همان قاعدهٔ ماژول‌های فاز ۴: ذخیره فقط وقتی بخش دیده
        // می‌شود؛ بخشِ پنهان نه ذخیره می‌شود و نه اعتبارسنجی.
        //
        // ذخیره عمداً دکمهٔ جداگانه ندارد و به ذخیرهٔ خودِ پرونده
        // گره خورده است: نمایندهٔ اول برای پروندهٔ معلولیت *الزامی*
        // است، پس اگر دکمهٔ جدا داشت، کاربر می‌توانست پرونده را
        // ذخیره کند و نماینده را نه — دقیقاً همان دادهٔ نامعتبری که
        // خواستهٔ کاربر می‌خواهد جلویش گرفته شود.
        // ════════════════════════════════════════════════════════════════════

        // مسیرِ فایلِ عکسِ ذخیره‌شده (درونِ پوشهٔ پرونده) و مسیرِ
        // فایلی که کاربر تازه انتخاب کرده و هنوز کپی نشده — همان
        // تفکیکی که عکسِ سرپرست/جمعی دارند.
        private string savedRep1PhotoPath = "";
        private string savedRep2PhotoPath = "";
        private string selectedRep1PhotoSource = "";
        private string selectedRep2PhotoSource = "";

        // ─── Feature 3: عکسِ سرپرستِ کودک ────────────────────────────────────
        // همان تفکیکِ «مسیرِ ذخیره‌شده» و «فایلِ تازه‌انتخاب‌شده» که عکسِ
        // سرپرستِ خانوار و نماینده دارند: کپی فقط هنگامِ ذخیرهٔ پرونده انجام
        // می‌شود، نه هنگامِ انتخاب.
        private string savedGuardianPhotoPath = "";
        private string selectedGuardianPhotoSource = "";

        private void btnGuardianBrowsePhoto_Click(object sender, EventArgs e)
        {
            if (txtCode.Text.Trim() == "")
            {
                Msg.Show("اول کد اختصاصی را وارد کنید");
                txtCode.Focus();
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "انتخاب عکس سرپرست کودک";
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png|فایل‌های JPG|*.jpg;*.jpeg|فایل‌های PNG|*.png";

                if (ofd.ShowDialog() != DialogResult.OK) return;
                // همان سقف/قالبِ عکسِ سرپرست — قاعدهٔ دومی ساخته نمی‌شود.
                if (!IsValidImageFile(ofd.FileName)) return;

                selectedGuardianPhotoSource = ofd.FileName;
                LoadImageToPictureBox(ofd.FileName, picGuardianPhoto);
            }
        }

        private void btnGuardianClearPhoto_Click(object sender, EventArgs e)
        {
            ClearPictureBox(picGuardianPhoto);
            selectedGuardianPhotoSource = "";
            savedGuardianPhotoPath = "";
        }

        // کپی به پوشهٔ پرونده — دقیقاً هم‌الگوی StoreRepresentativePhoto.
        private string StoreGuardianPhoto(string caseCode)
        {
            if (string.IsNullOrEmpty(selectedGuardianPhotoSource)) return savedGuardianPhotoPath;
            if (IsSamePath(selectedGuardianPhotoSource, savedGuardianPhotoPath)) return savedGuardianPhotoPath;

            string savedPath = FileHelper.SaveFileToCaseFolder(
                selectedGuardianPhotoSource,
                caseCode,
                FileHelper.SectionGuardianPhotos,
                caseCode + "-Guardian",
                savedGuardianPhotoPath);

            if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
                throw new Exception("عکس سرپرست کودک ذخیره نشد: " + FileHelper.LastError);

            selectedGuardianPhotoSource = "";
            return savedPath;
        }

        private void btnRep1BrowsePhoto_Click(object sender, EventArgs e)
        {
            BrowseRepresentativePhoto(1);
        }

        private void btnRep2BrowsePhoto_Click(object sender, EventArgs e)
        {
            BrowseRepresentativePhoto(2);
        }

        private void BrowseRepresentativePhoto(int slot)
        {
            if (txtCode.Text.Trim() == "")
            {
                Msg.Show("اول کد اختصاصی را وارد کنید");
                txtCode.Focus();
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "انتخاب عکس " + Helpers.CaseRepresentativeService.LabelFor(slot);
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png|فایل‌های JPG|*.jpg;*.jpeg|فایل‌های PNG|*.png";

                if (ofd.ShowDialog() != DialogResult.OK) return;
                // همان سقف/قالبِ عکسِ سرپرست — قاعدهٔ دومی ساخته نمی‌شود.
                if (!IsValidImageFile(ofd.FileName)) return;

                if (slot == 1)
                {
                    selectedRep1PhotoSource = ofd.FileName;
                    LoadImageToPictureBox(ofd.FileName, picRep1Photo);
                }
                else
                {
                    selectedRep2PhotoSource = ofd.FileName;
                    LoadImageToPictureBox(ofd.FileName, picRep2Photo);
                }
            }
        }

        private void btnRep1ClearPhoto_Click(object sender, EventArgs e)
        {
            ClearRepresentativePhoto(1);
        }

        private void btnRep2ClearPhoto_Click(object sender, EventArgs e)
        {
            ClearRepresentativePhoto(2);
        }

        // فقط ارجاعِ عکس پاک می‌شود؛ فایلِ روی دیسک عمداً می‌ماند
        // — همان محافظه‌کاریِ FrmDocs/FieldVisitService (حذفِ رکورد نباید
        // فایلِ کاربر را بی‌بازگشت پاک کند).
        private void ClearRepresentativePhoto(int slot)
        {
            if (slot == 1)
            {
                ClearPictureBox(picRep1Photo);
                selectedRep1PhotoSource = "";
                savedRep1PhotoPath = "";
            }
            else
            {
                ClearPictureBox(picRep2Photo);
                selectedRep2PhotoSource = "";
                savedRep2PhotoPath = "";
            }
        }

        // خالی‌کردنِ کاملِ کارتِ نمایندهٔ دوم + حذفِ ردیفِ ثبت‌شده.
        private void btnRep2Clear_Click(object sender, EventArgs e)
        {
            // مجوزِ اختصاصی (Phase 7). پیش‌فرضش دقیقاً مثل Case.Delete است
            // (فقط مدیر)، پس رفتارِ امروز عوض نمی‌شود؛ ولی از این پس مستقل
            // از حذفِ پرونده قابلِ تنظیم است.
            if (!CaseManagement.Enterprise.PermissionService.Require("Representative.Delete"))
            {
                Msg.Show("حذف نمایندهٔ قانونی فقط برای مدیر سیستم مجاز است.");
                return;
            }

            if (!UiTheme.ShowConfirm(this, "اطلاعات نمایندهٔ دوم حذف شود؟", "حذف نمایندهٔ دوم"))
                return;

            try
            {
                // در پروندهٔ ذخیره‌نشده ردیفی وجود ندارد؛ فقط کنترل‌ها
                // پاک می‌شوند و همین کافی است.
                if (currentCaseId > 0)
                {
                    Helpers.CaseRepresentativeService.Delete(
                        currentCaseId, Helpers.CaseRepresentativeService.OrderSecondary);
                }

                ClearRepresentativeSlot(2);
                Msg.Show("اطلاعات نمایندهٔ دوم حذف شد");
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در حذف نمایندهٔ دوم: " + ex.Message);
            }
        }

        // ─── خواندنِ کنترل‌ها → مدل ────────────────────────────
        private Helpers.RepresentativeRow BuildRepresentative(int slot)
        {
            bool first = slot == Helpers.CaseRepresentativeService.OrderPrimary;

            return new Helpers.RepresentativeRow
            {
                RepresentativeOrder = slot,
                FullName = (first ? txtRep1Name.Text : txtRep2Name.Text).Trim(),
                RelationshipToBeneficiary = (first ? txtRep1Relationship.Text : txtRep2Relationship.Text).Trim(),
                IdCardType = (first ? cmbRep1IdCardType.Text : cmbRep2IdCardType.Text).Trim(),
                NationalID = (first ? txtRep1NationalID.Text : txtRep2NationalID.Text).Trim(),
                Phone = (first ? txtRep1Phone.Text : txtRep2Phone.Text).Trim(),
                SecondaryPhone = (first ? txtRep1Phone2.Text : txtRep2Phone2.Text).Trim(),
                Address = (first ? txtRep1Address.Text : txtRep2Address.Text).Trim(),
                Notes = (first ? txtRep1Notes.Text : txtRep2Notes.Text).Trim(),
                // مسیرِ عکس در زمانِ ذخیره نهایی می‌شود (پس از کپی به
                // پوشهٔ پرونده)؛ اینجا مقدارِ فعلی نگه داشته می‌شود تا
                // اعتبارسنجیِ «خالی بودن» عکسِ انتخاب‌شده را هم ببیند.
                PhotoPath = first
                    ? (selectedRep1PhotoSource.Length > 0 ? selectedRep1PhotoSource : savedRep1PhotoPath)
                    : (selectedRep2PhotoSource.Length > 0 ? selectedRep2PhotoSource : savedRep2PhotoPath)
            };
        }

        // ─── اعتبارسنجی قبل از ذخیره ────────────────────────
        // قاعده‌ها در CaseRepresentativeService.Validate اند، نه اینجا — تا
        // همان قاعده از آزمون و هر مسیرِ آینده‌ای هم اعمال شود.
        private bool ValidateRepresentatives()
        {
            var sections = Helpers.ReferenceDataService.GetRequestTypeSectionsByName(txtRequestType.Text.Trim());
            if (!sections.ShowRepresentativeSection) return true;

            Helpers.RepresentativeRow primary = BuildRepresentative(
                Helpers.CaseRepresentativeService.OrderPrimary);
            Helpers.RepresentativeRow secondary = BuildRepresentative(
                Helpers.CaseRepresentativeService.OrderSecondary);

            // پروندهٔ جدید ⇒ نمایندهٔ اول الزامی؛ پروندهٔ موجود ⇒ اختیاری.
            //
            // این تنها جایی است که تفاوتِ «تازه» و «قدیمی» معنا دارد:
            // پروندهٔ موجود ممکن است پیش از وجودِ این قابلیت ثبت شده
            // باشد و نماینده‌اش اصلاً نامعلوم باشد؛ بستنِ ذخیرهٔ آن
            // یعنی کاربر نتواند حتی یک شماره تلفنِ بی‌ربط را اصلاح کند.
            bool isNewCase = currentCaseId <= 0;
            var requirement = isNewCase
                ? Helpers.RepresentativeRequirement.Required
                : Helpers.RepresentativeRequirement.Optional;

            string error = Helpers.CaseRepresentativeService.Validate(
                currentCaseId, primary, secondary, requirement);
            if (error != null)
            {
                Msg.Show(error);
                if (tabsCase != null && tabsCase.TabPages.Contains(tabRepresentative))
                    tabsCase.SelectedTab = tabRepresentative;
                return false;
            }

            // پروندهٔ قدیمیِ بدون نماینده: فقط اطلاع‌رسانی، نه پرسش.
            //
            // عمداً Msg.Show است نه ShowConfirm: دیالوگِ تأیید یعنی کاربر
            // می‌تواند «خیر» بزند و ذخیره لغو شود — که دوباره همان
            // مسدودکردنی است که قرار بود برداشته شود. و فقط یک بار
            // در هر بارِ بازکردنِ پرونده نشان داده می‌شود؛ هشداری که
            // با هر ذخیره تکرار شود خوانده نمی‌شود، فقط رد می‌شود.
            if (!isNewCase && primary.IsEmpty && !_legacyRepWarningShown)
            {
                _legacyRepWarningShown = true;
                Msg.Show("این پروندهٔ معلولیت «نمایندهٔ اول» ثبت‌شده ندارد." +
                    Environment.NewLine +
                    "ذخیره انجام می‌شود؛ ولی برای فعال‌کردنِ خدمات، ثبتِ نمایندهٔ اول الزامی است.");
            }

            // همان تذکره در پرونده‌های دیگر: عمداً فقط هشدار و قابلِ
            // ادامه است — یک وکیلِ رسمی یا قیّمِ خانوادگی قانوناً
            // می‌تواند نمایندهٔ چند ذینفع باشد، پس بستنِ آن دادهٔ
            // درست را غیرقابلِ ثبت می‌کرد.
            return ConfirmDuplicateRepresentativeIds(primary, secondary);
        }

        // هشدارِ پروندهٔ قدیمی در هر بازکردن یک بار نشان داده می‌شود.
        private bool _legacyRepWarningShown;


        private bool ConfirmDuplicateRepresentativeIds(
            Helpers.RepresentativeRow primary, Helpers.RepresentativeRow secondary)
        {
            var messages = new System.Collections.Generic.List<string>();

            // پروندهٔ قدیمی ممکن است نمایندهٔ اول هم نداشته باشد.
            if (primary != null && !primary.IsEmpty) AppendDuplicateIdWarning(messages, primary);
            if (secondary != null && !secondary.IsEmpty) AppendDuplicateIdWarning(messages, secondary);

            if (messages.Count == 0) return true;

            return UiTheme.ShowConfirm(this,
                string.Join(Environment.NewLine, messages.ToArray()) + Environment.NewLine +
                "ذخیره ادامه یابد؟",
                "نمایندهٔ مشترک");
        }

        private void AppendDuplicateIdWarning(
            System.Collections.Generic.List<string> messages, Helpers.RepresentativeRow row)
        {
            System.Collections.Generic.List<string> others =
                Helpers.CaseRepresentativeService.FindOtherCasesWithSameId(currentCaseId, row.NationalID);
            if (others.Count == 0) return;

            messages.Add(Helpers.CaseRepresentativeService.LabelFor(row.RepresentativeOrder) +
                " با همین شمارهٔ تذکره در پروندهٔ دیگری هم ثبت شده است: " +
                string.Join("، ", others.ToArray()));
        }

        // ─── ذخیره ──────────────────────────────────────────
        // پس از ثبت/ویرایشِ پرونده فراخوانی می‌شود — کنارِ
        // SaveCaseModules و با همان قاعدهٔ «فقط بخشِ دیده‌شده».
        private void SaveCaseRepresentatives(int casId)
        {
            if (casId <= 0) return;

            var sections = Helpers.ReferenceDataService.GetRequestTypeSectionsByName(txtRequestType.Text.Trim());
            if (!sections.ShowRepresentativeSection) return;

            // مجوزِ اختصاصیِ ویرایش (Phase 7). پیش‌فرض برابرِ Case.Edit است، پس
            // امروز هیچ کاربری تفاوتی نمی‌بیند. عمداً ذخیرهٔ *پرونده* را
            // نمی‌شکند — فقط بخشِ نماینده رد می‌شود و کاربر مطلع می‌گردد،
            // وگرنه گرفتنِ یک مجوزِ فرعی کلِ ثبتِ پرونده را از کار می‌انداخت.
            if (!CaseManagement.Enterprise.PermissionService.HasPermission("Representative.Edit"))
            {
                Msg.Show("شما اجازهٔ ثبت/ویرایش نمایندهٔ قانونی را ندارید؛ بقیهٔ پرونده ذخیره شد.");
                return;
            }

            // عکس‌ها باید پیش از نوشتنِ ردیف در دیتابیس به پوشهٔ
            // پرونده کپی شوند، وگرنه PhotoPath به مسیرِ موقتِ مبدأ
            // (مثلاً Desktop کاربر) اشاره می‌کرد.
            SaveRepresentativePhotos();

            Helpers.RepresentativeRow primary = BuildRepresentative(
                Helpers.CaseRepresentativeService.OrderPrimary);
            primary.PhotoPath = savedRep1PhotoPath;

            // پروندهٔ قدیمی که کاربر نماینده‌ای برایش وارد نکرده:
            // نه ردیفِ تهی ساخته می‌شود و نه ردیفِ موجود دست‌خورده
            // می‌شود. خواستهٔ صریح: «دادهٔ موجود را خودکار نساز و
            // تغییر نده.» حذفِ نمایندهٔ اول فقط از مسیرِ صریحِ
            // دکمهٔ حذف انجام می‌شود، نه از خالی‌ماندنِ کادرها.
            if (!primary.IsEmpty)
                Helpers.CaseRepresentativeService.Save(casId, primary);

            Helpers.RepresentativeRow secondary = BuildRepresentative(
                Helpers.CaseRepresentativeService.OrderSecondary);
            secondary.PhotoPath = savedRep2PhotoPath;

            if (secondary.IsEmpty)
            {
                // کاربر نمایندهٔ دوم را خالی گذاشته: اگر قبلاً ردیفی
                // داشته، پاک‌کردنِ کادرها باید واقعاً حذفش کند — وگرنه
                // ردیفِ کهنه در گزارش/جستجو زنده می‌ماند.
                Helpers.CaseRepresentativeService.Delete(
                    casId, Helpers.CaseRepresentativeService.OrderSecondary);
            }
            else
            {
                Helpers.CaseRepresentativeService.Save(casId, secondary);
            }
        }

        private void SaveRepresentativePhotos()
        {
            string caseCode = txtCode.Text.Trim();

            savedRep1PhotoPath = StoreRepresentativePhoto(
                caseCode, 1, selectedRep1PhotoSource, savedRep1PhotoPath);
            savedRep2PhotoPath = StoreRepresentativePhoto(
                caseCode, 2, selectedRep2PhotoSource, savedRep2PhotoPath);
        }

        private string StoreRepresentativePhoto(string caseCode, int slot, string source, string existing)
        {
            if (string.IsNullOrEmpty(source)) return existing;
            if (IsSamePath(source, existing)) return existing;

            string savedPath = FileHelper.SaveFileToCaseFolder(
                source,
                caseCode,
                FileHelper.SectionRepresentativePhotos,
                caseCode + "-Rep" + slot,
                existing);

            if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
                throw new Exception("عکس " + Helpers.CaseRepresentativeService.LabelFor(slot) +
                    " ذخیره نشد: " + FileHelper.LastError);

            if (slot == 1) selectedRep1PhotoSource = ""; else selectedRep2PhotoSource = "";
            return savedPath;
        }

        // ─── بارگذاری ───────────────────────────────────────
        private void LoadCaseRepresentatives(int casId)
        {
            ClearRepresentativeSlot(1);
            ClearRepresentativeSlot(2);
            if (casId <= 0) return;

            foreach (Helpers.RepresentativeRow row in Helpers.CaseRepresentativeService.GetAll(casId))
                FillRepresentativeSlot(row);
        }

        private void FillRepresentativeSlot(Helpers.RepresentativeRow row)
        {
            bool first = row.RepresentativeOrder == Helpers.CaseRepresentativeService.OrderPrimary;
            if (!first && row.RepresentativeOrder != Helpers.CaseRepresentativeService.OrderSecondary)
                return;   // جایگاهِ سوم هنوز کنترلی ندارد (فقط داده پذیرفته می‌شود)

            if (first)
            {
                txtRep1Name.Text = row.FullName;
                SetComboBoxText(txtRep1Relationship, row.RelationshipToBeneficiary);
                SetComboBoxText(cmbRep1IdCardType, row.IdCardType);
                txtRep1NationalID.Text = row.NationalID;
                txtRep1Phone.Text = row.Phone;
                txtRep1Phone2.Text = row.SecondaryPhone;
                txtRep1Address.Text = row.Address;
                txtRep1Notes.Text = row.Notes;
                savedRep1PhotoPath = row.PhotoPath ?? "";
                selectedRep1PhotoSource = "";
                LoadRepresentativePhoto(savedRep1PhotoPath, picRep1Photo);
            }
            else
            {
                txtRep2Name.Text = row.FullName;
                SetComboBoxText(txtRep2Relationship, row.RelationshipToBeneficiary);
                SetComboBoxText(cmbRep2IdCardType, row.IdCardType);
                txtRep2NationalID.Text = row.NationalID;
                txtRep2Phone.Text = row.Phone;
                txtRep2Phone2.Text = row.SecondaryPhone;
                txtRep2Address.Text = row.Address;
                txtRep2Notes.Text = row.Notes;
                savedRep2PhotoPath = row.PhotoPath ?? "";
                selectedRep2PhotoSource = "";
                LoadRepresentativePhoto(savedRep2PhotoPath, picRep2Photo);
            }
        }

        // فایلِ گم‌شده نباید بازکردنِ پرونده را بشکند — کادر خالی
        // می‌ماند و مسیرِ ثبت‌شده دست‌نخورده می‌ماند.
        private void LoadRepresentativePhoto(string path, PictureBox target)
        {
            ClearPictureBox(target);
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (File.Exists(path)) LoadImageToPictureBox(path, target);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadRepresentativePhoto failed: " + ex.Message);
            }
        }

        private void ClearRepresentativeSlot(int slot)
        {
            if (slot == 1)
            {
                txtRep1Name.Text = "";
                txtRep1Relationship.SelectedIndex = -1; txtRep1Relationship.Text = "";
                cmbRep1IdCardType.SelectedIndex = -1; cmbRep1IdCardType.Text = "";
                txtRep1NationalID.Text = "";
                txtRep1Phone.Text = "";
                txtRep1Phone2.Text = "";
                txtRep1Address.Text = "";
                txtRep1Notes.Text = "";
                ClearRepresentativePhoto(1);
            }
            else
            {
                txtRep2Name.Text = "";
                txtRep2Relationship.SelectedIndex = -1; txtRep2Relationship.Text = "";
                cmbRep2IdCardType.SelectedIndex = -1; cmbRep2IdCardType.Text = "";
                txtRep2NationalID.Text = "";
                txtRep2Phone.Text = "";
                txtRep2Phone2.Text = "";
                txtRep2Address.Text = "";
                txtRep2Notes.Text = "";
                ClearRepresentativePhoto(2);
            }
        }

        // فهرستِ نسبت از همان TblLookup می‌آید که اعتبارسنجی مرجعِ
        // خود می‌گیرد — یک منبع، پس گزینهٔ قابلِ انتخاب هرگز
        // نمی‌تواند مقدارِ نامعتبر باشد.
        //
        // آموزش — چرا LookupHelper.FillCombo استفاده نشد: آن متد وقتی
        // مقدارِ فعلی در فهرست نباشد، خودبه‌خود گزینهٔ اول را
        // انتخاب می‌کند. برای فیلدی که *الزامی* است یعنی هر
        // نمایندهٔ خالی بی‌سروصدا نسبتِ «پدر» می‌گرفت و کاربر
        // هرگز مجبور نمی‌شد آن را آگاهانه انتخاب کند — دقیقاً
        // همان دادهٔ نادرستی که اعتبارسنجی می‌خواهد جلویش را بگیرد.
        private void LoadRepresentativeLookups()
        {
            string[] values = Helpers.LookupHelper.GetValues(
                Helpers.CaseRepresentativeService.LookupRelationship).ToArray();

            txtRep1Relationship.Items.Clear();
            txtRep1Relationship.Items.AddRange(values);
            txtRep2Relationship.Items.Clear();
            txtRep2Relationship.Items.AddRange(values);
        }

        private static object TextOrNull(string value)
        {
            string trimmed = (value ?? "").Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        // معادلِ TextOrNull برای تاریخ‌های *اختیاری*.
        //
        // آموزش — چرا لازم است: PersianDatePicker همیشه یک DateTime دارد
        // (پیش‌فرض: امروز)، پس نوشتنِ بی‌قیدِ .Value یعنی «هیچ تاریخی ثبت
        // نشده» و «امروز ثبت شده» در دیتابیس یکسان می‌شوند. با ShowCheckBox
        // فعال، Checked دقیقاً همان تفکیک را می‌دهد: تیک‌نخورده ⇒ NULL.
        // قالبِ رشته همان قراردادِ پروژه است (yyyy-MM-dd میلادی؛ نمایشِ شمسی
        // فقط در لایهٔ UI).
        private static object DateOrNull(Helpers.PersianDatePicker picker)
        {
            if (picker == null || !picker.Checked) return null;
            return picker.Value.Date.ToString("yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        // بارگذاریِ ماژول‌ها هنگام باز کردن یک پرونده. اگر ردیفی نباشد
        // (پروندهٔ قدیمی یا نوعی که این ماژول را ندارد) کنترل‌ها خالی می‌مانند.
        private void LoadCaseModules(int casId)
        {
            ClearCaseModuleFields();
            if (casId <= 0) return;

            System.Data.DataRow orphan = Helpers.CaseModuleService.Load(Helpers.CaseModuleService.TableOrphan, casId);
            if (orphan != null)
            {
                txtMainResidenceProvince.Text = ModuleString(orphan, "MainResidenceProvince");
                txtMainResidenceDistrict.Text = ModuleString(orphan, "MainResidenceDistrict");
                txtMainResidenceVillage.Text  = ModuleString(orphan, "MainResidenceVillage");
                SetComboBoxText(txtFatherStatus, ModuleString(orphan, "FatherStatus"));
                SetComboBoxText(txtFatherDeathCause, ModuleString(orphan, "FatherDeathCause"));
                SetDatePickerValue(dtpFatherDeathDate, orphan["FatherDeathDate"]);
                SetComboBoxText(txtMotherStatus, ModuleString(orphan, "MotherStatus"));
                txtOrphanSchoolName.Text = ModuleString(orphan, "SchoolName");
                SetComboBoxText(txtOrphanEducationLevel, ModuleString(orphan, "EducationLevel"));
                chkIsStudent.Checked = orphan["IsStudent"] != DBNull.Value && Convert.ToInt32(orphan["IsStudent"]) != 0;
                txtOrphanNotes.Text = ModuleString(orphan, "Notes");
                txtGuardianName.Text = ModuleString(orphan, "GuardianName");
                SetComboBoxText(txtGuardianRelationship, ModuleString(orphan, "GuardianRelationship"));

                // Feature 3 — عکسِ سرپرستِ کودک.
                savedGuardianPhotoPath = ModuleString(orphan, "GuardianPhotoPath");
                selectedGuardianPhotoSource = "";
                if (!string.IsNullOrWhiteSpace(savedGuardianPhotoPath) && File.Exists(savedGuardianPhotoPath))
                    LoadImageToPictureBox(savedGuardianPhotoPath, picGuardianPhoto);
                else
                    ClearPictureBox(picGuardianPhoto);
            }

            System.Data.DataRow disability = Helpers.CaseModuleService.Load(Helpers.CaseModuleService.TableDisability, casId);
            if (disability != null)
            {
                SetComboBoxText(txtDisabilityCause, ModuleString(disability, "DisabilityCause"));
                txtDisabilityDescription.Text = ModuleString(disability, "DisabilityDescription");
                txtSpecialNeeds.Text = ModuleString(disability, "SpecialNeeds");
                SetComboBoxText(txtDisabilityCardStatus, ModuleString(disability, "HasDisabilityCard"));
                txtDisabilityCardNumber.Text = ModuleString(disability, "DisabilityCardNumber");
                txtCardIssuer.Text = ModuleString(disability, "CardIssuer");
                SetDatePickerValue(dtpDisabilityIssueDate, disability["IssueDate"]);
                SetDatePickerValue(dtpDisabilityExpiryDate, disability["ExpiryDate"]);
                txtDisabilityNotes.Text = ModuleString(disability, "Notes");
            }

            System.Data.DataRow migrant = Helpers.CaseModuleService.Load(Helpers.CaseModuleService.TableMigrant, casId);
            if (migrant != null)
            {
                SetComboBoxText(txtHasMigrationCard, ModuleString(migrant, "HasMigrationCard"));
                txtMigrationCardNumber.Text = ModuleString(migrant, "MigrationCardNumber");
                txtOriginCountry.Text = ModuleString(migrant, "OriginCountry");
                txtDestinationCountry.Text = ModuleString(migrant, "DestinationCountry");
                SetDatePickerValue(dtpDepartureDate, migrant["DepartureDate"]);
                SetDatePickerValue(dtpArrivalDate, migrant["ArrivalDate"]);
                object duration = migrant["AssistanceDuration"];
                txtAssistanceDurationMonths.Text = duration == DBNull.Value ? "" : duration.ToString();
                txtMigrantNotes.Text = ModuleString(migrant, "Notes");
            }
        }

        private static string ModuleString(System.Data.DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName)) return "";
            object value = row[columnName];
            return value == DBNull.Value ? "" : value.ToString();
        }

        private void ClearCaseModuleFields()
        {
            txtFatherStatus.SelectedIndex = -1; txtFatherStatus.Text = "";
            txtMotherStatus.SelectedIndex = -1; txtMotherStatus.Text = "";
            txtGuardianRelationship.SelectedIndex = -1; txtGuardianRelationship.Text = "";
            txtOrphanEducationLevel.SelectedIndex = -1; txtOrphanEducationLevel.Text = "";

            txtOrphanSchoolName.Text = "";
            txtOrphanNotes.Text = "";
            txtGuardianName.Text = "";
            // Feature 3 — عکسِ سرپرستِ کودک هم باید با بقیهٔ فیلدهای ماژول
            // خالی شود، وگرنه عکسِ پروندهٔ قبلی روی پروندهٔ تازه می‌ماند و
            // ذخیره می‌شد (همان کلاسِ باگی که ClearCaseModuleFields برایش هست).
            ClearPictureBox(picGuardianPhoto);
            savedGuardianPhotoPath = "";
            selectedGuardianPhotoSource = "";
            chkIsStudent.Checked = false;
            txtCardIssuer.Text = "";
            txtDisabilityNotes.Text = "";
            txtOriginCountry.Text = "";
            txtDestinationCountry.Text = "";
            txtMigrantNotes.Text = "";

            // پروندهٔ تازه هیچ‌کدام از این تاریخ‌های اختیاری را ندارد؛ تیک
            // برداشته می‌شود تا ذخیره NULL بنویسد نه «امروز».
            dtpFatherDeathDate.Value = DateTime.Today;
            dtpFatherDeathDate.Checked = false;
            dtpDisabilityIssueDate.Value = DateTime.Today;
            dtpDisabilityIssueDate.Checked = false;
            dtpDisabilityExpiryDate.Value = DateTime.Today;
            dtpDisabilityExpiryDate.Checked = false;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 5 — تب بازدید میدانی. همهٔ دسترسی به داده از طریق
        // FieldVisitService است (تایم‌لاین و بازمحاسبهٔ کامل‌بودن داخلِ خودِ
        // سرویس انجام می‌شود، پس اینجا تکرار نمی‌شوند).
        // ═══════════════════════════════════════════════════════════════════
        private int currentVisitId;

        // ─── تبِ تاریخچه (H5) ────────────────────────────────────────────────
        // فقط خواندنی. TblCaseTimeline از فاز ۳ پر می‌شد ولی هیچ صفحه‌ای آن را
        // نشان نمی‌داد؛ این متد همان نمایشِ نبوده است.
        private void RefreshTimelineTab()
        {
            if (dgvTimeline == null) return;

            if (currentCaseId <= 0)
            {
                dgvTimeline.DataSource = null;
                return;
            }

            try
            {
                dgvTimeline.DataSource = Helpers.TimelineService.GetCaseTimeline(currentCaseId);
            }
            catch (Exception ex)
            {
                // نمایشِ تاریخچه هرگز نباید بازکردنِ پرونده را بشکند.
                System.Diagnostics.Debug.WriteLine("RefreshTimelineTab failed: " + ex.Message);
                dgvTimeline.DataSource = null;
            }
        }

        private void RefreshVisitsTab()
        {
            if (dgvVisits == null) return;

            if (currentCaseId <= 0)
            {
                dgvVisits.DataSource = null;
                ClearVisitEntry();
                return;
            }

            try
            {
                dgvVisits.DataSource = Helpers.FieldVisitService.GetVisitsTable(currentCaseId);
                ApplyVisitGridHeaders();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری بازدیدها: " + ex.Message);
            }
        }

        private void ApplyVisitGridHeaders()
        {
            if (dgvVisits.Columns.Count == 0) return;

            if (dgvVisits.Columns.Contains("VisitID"))
                dgvVisits.Columns["VisitID"].Visible = false;
            if (dgvVisits.Columns.Contains("VisitDate"))
                dgvVisits.Columns["VisitDate"].HeaderText = "تاریخ بازدید";
            if (dgvVisits.Columns.Contains("VisitorName"))
                dgvVisits.Columns["VisitorName"].HeaderText = "بازدیدکننده";
            if (dgvVisits.Columns.Contains("VisitResult"))
                dgvVisits.Columns["VisitResult"].HeaderText = "نتیجه";
            if (dgvVisits.Columns.Contains("Recommendation"))
                dgvVisits.Columns["Recommendation"].HeaderText = "توصیه";
            if (dgvVisits.Columns.Contains("Notes"))
                dgvVisits.Columns["Notes"].HeaderText = "یادداشت";
            if (dgvVisits.Columns.Contains("PhotoCount"))
                dgvVisits.Columns["PhotoCount"].HeaderText = "تعداد عکس";
        }

        private void ClearVisitEntry()
        {
            currentVisitId = 0;
            dtpVisitDate.Value = DateTime.Today;
            txtVisitorName.Text = Helpers.SecurityContext.Username ?? "";
            txtVisitResult.SelectedIndex = -1;
            txtVisitResult.Text = "";
            txtVisitRecommendation.SelectedIndex = -1;
            txtVisitRecommendation.Text = "";
            txtVisitNotes.Text = "";
        }

        private void dgvVisits_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || !dgvVisits.Columns.Contains("VisitID")) return;

            object idValue = dgvVisits.Rows[e.RowIndex].Cells["VisitID"].Value;
            if (idValue == null || idValue == DBNull.Value) return;

            var visit = Helpers.FieldVisitService.GetVisit(Convert.ToInt32(idValue));
            if (visit == null) return;

            currentVisitId = visit.VisitID;
            SetDatePickerValue(dtpVisitDate, visit.VisitDate);
            txtVisitorName.Text = visit.VisitorName;
            SetComboBoxText(txtVisitResult, visit.VisitResult);
            SetComboBoxText(txtVisitRecommendation, visit.Recommendation);
            txtVisitNotes.Text = visit.Notes;
        }

        private void btnVisitNew_Click(object sender, EventArgs e)
        {
            ClearVisitEntry();
        }

        private void btnVisitSave_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه ثبت بازدید ندارد.");
                return;
            }

            if (currentCaseId <= 0)
            {
                Msg.Show("اول پرونده را ذخیره یا انتخاب کنید");
                return;
            }

            try
            {
                Helpers.FieldVisitService.SaveVisit(
                    currentVisitId,
                    currentCaseId,
                    dtpVisitDate.Value.Date.ToString("yyyy-MM-dd"),
                    txtVisitorName.Text.Trim(),
                    txtVisitResult.Text.Trim(),
                    txtVisitRecommendation.Text.Trim(),
                    txtVisitNotes.Text.Trim());

                Msg.Show(currentVisitId > 0 ? "بازدید ویرایش شد" : "بازدید ثبت شد");
                RefreshVisitsTab();
                ClearVisitEntry();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ذخیره بازدید: " + ex.Message);
            }
        }

        private void btnVisitDelete_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Delete"))
            {
                Msg.Show("حذف بازدید فقط برای مدیر سیستم مجاز است.");
                return;
            }

            if (currentVisitId <= 0)
            {
                Msg.Show("اول یک بازدید را از فهرست انتخاب کنید");
                return;
            }

            if (!UiTheme.ShowConfirm(this, "این بازدید حذف شود؟", "حذف بازدید"))
                return;

            try
            {
                Helpers.FieldVisitService.DeleteVisit(currentVisitId);
                Msg.Show("بازدید حذف شد");
                RefreshVisitsTab();
                ClearVisitEntry();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در حذف بازدید: " + ex.Message);
            }
        }

        // ─── عکس‌های بازدید میدانی ───────────────────────────────────────────
        // آموزش — تا امروز TblFieldVisitPhoto هیچ راهِ ورودی نداشت: سرویس،
        // پوشه، بکاپ و همگام‌سازی‌اش آماده بود ولی هیچ فرمی صدایش نمی‌زد و
        // ستونِ «تعداد عکس» همیشه صفر می‌ماند. دیالوگ کارِ افزودن/دیدن/حذف
        // را می‌کند؛ اینجا فقط باز می‌شود و گرید بعدش تازه می‌گردد.
        private void btnVisitPhotos_Click(object sender, EventArgs e)
        {
            if (currentCaseId <= 0)
            {
                Msg.Show("اول پرونده را ذخیره یا انتخاب کنید");
                return;
            }

            if (currentVisitId <= 0)
            {
                Msg.Show("اول یک بازدید را از فهرست انتخاب کنید؛ عکس به همان بازدید بسته می‌شود.");
                return;
            }

            string title = Helpers.PersianDateHelper.ToPersianDateString(dtpVisitDate.Value);
            string visitor = txtVisitorName.Text.Trim();
            if (visitor.Length > 0) title += "  ·  " + visitor;

            try
            {
                using (var frm = new Helpers.FrmFieldVisitPhotos(
                           currentVisitId, currentCaseId, txtCode.Text.Trim(), title))
                    frm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در باز کردن عکس‌های بازدید: " + ex.Message);
            }

            // «تعداد عکس» در گرید از همین جدول خوانده می‌شود، پس باید تازه شود.
            RefreshVisitsTab();
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 6 — تب خانواده. حداقلی و عمداً بدونِ ماژولِ مدیریتِ جداگانه:
        // نمایشِ شناسهٔ خانوار + فهرستِ پرونده‌های هم‌خانوار + پیوند/جدا کردن.
        // ═══════════════════════════════════════════════════════════════════
        private void RefreshFamilyTab()
        {
            if (dgvFamilyCases == null) return;

            if (currentCaseId <= 0)
            {
                dgvFamilyCases.DataSource = null;
                lblFamilyGroupValue.Text = "شناسه خانوار: —  (اول پرونده را ذخیره یا انتخاب کنید)";
                return;
            }

            try
            {
                int groupId = Helpers.FamilyGroupService.GetFamilyGroupId(currentCaseId);
                bool isRoot = groupId == currentCaseId;
                int memberCount = Helpers.FamilyGroupService.GetMemberCount(currentCaseId);

                lblFamilyGroupValue.Text = string.Format(
                    "شناسه خانوار: {0}   |   نقش این پرونده: {1}   |   تعداد پرونده‌های خانوار: {2}",
                    groupId, isRoot ? "ریشه خانوار" : "عضو", memberCount);

                dgvFamilyCases.DataSource = Helpers.FamilyGroupService.GetFamilyCases(currentCaseId);
                ApplyFamilyGridHeaders();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری اطلاعات خانواده: " + ex.Message);
            }
        }

        private void ApplyFamilyGridHeaders()
        {
            if (dgvFamilyCases.Columns.Count == 0) return;

            if (dgvFamilyCases.Columns.Contains("CasID"))
                dgvFamilyCases.Columns["CasID"].Visible = false;
            if (dgvFamilyCases.Columns.Contains("Code"))
                dgvFamilyCases.Columns["Code"].HeaderText = "کد پرونده";
            if (dgvFamilyCases.Columns.Contains("HeadFullName"))
                dgvFamilyCases.Columns["HeadFullName"].HeaderText = "نام سرپرست";
            if (dgvFamilyCases.Columns.Contains("ServiceStatus"))
                dgvFamilyCases.Columns["ServiceStatus"].HeaderText = "وضعیت خدمات";
            if (dgvFamilyCases.Columns.Contains("RequestTypeName"))
                dgvFamilyCases.Columns["RequestTypeName"].HeaderText = "نوع درخواست";
            if (dgvFamilyCases.Columns.Contains("FamilyRole"))
                dgvFamilyCases.Columns["FamilyRole"].HeaderText = "نقش در خانوار";
        }

        private void btnFamilyLink_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه تغییر خانواده ندارد.");
                return;
            }

            if (currentCaseId <= 0)
            {
                Msg.Show("اول پرونده را ذخیره یا انتخاب کنید");
                return;
            }

            int selectedCasId = ShowFamilyLinkPicker();
            if (selectedCasId <= 0) return;

            string error;
            if (!Helpers.FamilyGroupService.LinkToFamily(currentCaseId, selectedCasId, out error))
            {
                Msg.Show(error);
                return;
            }

            Msg.Show("پرونده به خانواده پیوند خورد");
            RefreshFamilyTab();
        }

        private void btnFamilyUnlink_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه تغییر خانواده ندارد.");
                return;
            }

            if (currentCaseId <= 0)
            {
                Msg.Show("اول پرونده را ذخیره یا انتخاب کنید");
                return;
            }

            if (!UiTheme.ShowConfirm(this,
                "این پرونده از خانوار جدا شود و مستقل گردد؟", "جدا کردن از خانواده"))
                return;

            string error;
            if (!Helpers.FamilyGroupService.UnlinkFromFamily(currentCaseId, out error))
            {
                Msg.Show(error);
                return;
            }

            Msg.Show("پرونده از خانواده جدا شد");
            RefreshFamilyTab();
        }

        // انتخابگرِ سبک — یک دیالوگِ ساخته‌شده در کد، نه یک فرمِ تازه در پروژه
        // (خواستهٔ صریح: بدونِ صفحاتِ مدیریتیِ پیچیده).
        private int ShowFamilyLinkPicker()
        {
            using (var dialog = new Form())
            {
                dialog.Text = "انتخاب پرونده ریشه خانوار";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new System.Drawing.Size(820, 520);
                dialog.RightToLeft = RightToLeft.Yes;
                dialog.RightToLeftLayout = true;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;

                var txtSearch = new TextBox { Dock = DockStyle.Top, Height = 30 };
                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    MultiSelect = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    RightToLeft = RightToLeft.Yes
                };

                var btnOk = new Button { Text = "پیوند", DialogResult = DialogResult.OK, Width = 120, Height = 36 };
                var btnCancel = new Button { Text = "انصراف", DialogResult = DialogResult.Cancel, Width = 120, Height = 36 };
                var buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 48,
                    FlowDirection = FlowDirection.LeftToRight,
                    Padding = new Padding(10, 6, 10, 6)
                };
                buttons.Controls.Add(btnOk);
                buttons.Controls.Add(btnCancel);

                EventHandler reload = delegate
                {
                    try
                    {
                        grid.DataSource = Helpers.FamilyGroupService.SearchCasesForLink(
                            txtSearch.Text, currentCaseId);
                        if (grid.Columns.Contains("CasID")) grid.Columns["CasID"].Visible = false;
                        if (grid.Columns.Contains("Code")) grid.Columns["Code"].HeaderText = "کد پرونده";
                        if (grid.Columns.Contains("FormNo")) grid.Columns["FormNo"].HeaderText = "شماره فرم";
                        if (grid.Columns.Contains("HeadFullName")) grid.Columns["HeadFullName"].HeaderText = "نام سرپرست";
                        if (grid.Columns.Contains("HeadFatherName")) grid.Columns["HeadFatherName"].HeaderText = "نام پدر";
                        if (grid.Columns.Contains("Phone")) grid.Columns["Phone"].HeaderText = "تلفن";
                        if (grid.Columns.Contains("RequestTypeName")) grid.Columns["RequestTypeName"].HeaderText = "نوع درخواست";
                    }
                    catch (Exception ex)
                    {
                        Msg.Show("خطا در جستجو: " + ex.Message);
                    }
                };

                txtSearch.TextChanged += reload;
                dialog.Controls.Add(grid);
                dialog.Controls.Add(txtSearch);
                dialog.Controls.Add(buttons);
                dialog.AcceptButton = btnOk;
                dialog.CancelButton = btnCancel;

                reload(null, EventArgs.Empty);

                if (dialog.ShowDialog(this) != DialogResult.OK) return 0;
                if (grid.CurrentRow == null || !grid.Columns.Contains("CasID")) return 0;

                object idValue = grid.CurrentRow.Cells["CasID"].Value;
                return idValue == null || idValue == DBNull.Value ? 0 : Convert.ToInt32(idValue);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 5.5-B — نمایشِ فقط‌خواندنیِ امتیاز آسیب‌پذیری.
        // هیچ مسیرِ ویرایشی وجود ندارد: امتیاز فقط از موتور می‌آید.
        // ═══════════════════════════════════════════════════════════════════
        private void RefreshVulnerabilityTab()
        {
            if (lblVulnScoreValue == null) return;

            if (currentCaseId <= 0)
            {
                lblVulnScoreValue.Text = "امتیاز آسیب‌پذیری: —";
                lblVulnScoreDate.Text = "برای مشاهده، اول پرونده را ذخیره یا انتخاب کنید.";
                dgvVulnBreakdown.DataSource = null;
                return;
            }

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT VulnerabilityScore, VulnerabilityBand, VulnerabilityScoreDate FROM TblCase WHERE CasID = @Id;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", currentCaseId);
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read() && dr["VulnerabilityScore"] != DBNull.Value)
                        {
                            double score = Convert.ToDouble(dr["VulnerabilityScore"]);
                            string band = dr["VulnerabilityBand"] == DBNull.Value ? "" : dr["VulnerabilityBand"].ToString();
                            string date = dr["VulnerabilityScoreDate"] == DBNull.Value ? "" : dr["VulnerabilityScoreDate"].ToString();

                            lblVulnScoreValue.Text = string.Format("امتیاز آسیب‌پذیری: {0} از ۱۰۰   |   سطح: {1}",
                                score.ToString("0.#"),
                                Helpers.VulnerabilityScoreService.GetBandDisplayName(band));
                            lblVulnScoreDate.Text = string.IsNullOrEmpty(date)
                                ? "" : "تاریخ محاسبه: " + date;

                            lblVulnScoreValue.ForeColor = BandColor(band);
                        }
                        else
                        {
                            lblVulnScoreValue.Text = "امتیاز آسیب‌پذیری: هنوز محاسبه نشده";
                            lblVulnScoreValue.ForeColor = UiTheme.TextDark;
                            lblVulnScoreDate.Text = "با ذخیرهٔ پرونده به‌صورت خودکار محاسبه می‌شود.";
                        }
                    }
                }

                dgvVulnBreakdown.DataSource =
                    Helpers.VulnerabilityScoreService.GetCurrentBreakdown(currentCaseId);
                ApplyVulnGridHeaders();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری امتیاز آسیب‌پذیری: " + ex.Message);
            }
        }

        private static System.Drawing.Color BandColor(string band)
        {
            switch ((band ?? "").Trim())
            {
                case Helpers.VulnerabilityScoreService.BandHigh:   return UiTheme.Danger;
                case Helpers.VulnerabilityScoreService.BandMedium: return UiTheme.Warning;
                default: return UiTheme.TextDark;
            }
        }

        private void ApplyVulnGridHeaders()
        {
            if (dgvVulnBreakdown.Columns.Count == 0) return;

            if (dgvVulnBreakdown.Columns.Contains("CriteriaName"))
                dgvVulnBreakdown.Columns["CriteriaName"].HeaderText = "معیار";
            if (dgvVulnBreakdown.Columns.Contains("FactValue"))
                dgvVulnBreakdown.Columns["FactValue"].HeaderText = "مقدار";
            if (dgvVulnBreakdown.Columns.Contains("ScoreValue"))
                dgvVulnBreakdown.Columns["ScoreValue"].HeaderText = "امتیاز";
            if (dgvVulnBreakdown.Columns.Contains("Explanation"))
                dgvVulnBreakdown.Columns["Explanation"].HeaderText = "توضیح";
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 5.5-C — وضعیتِ محاسبه‌شدهٔ پرونده (همه فقط‌خواندنی).
        //
        // هیچ محاسبه‌ای اینجا انجام نمی‌شود: تکمیل و امتیاز از ستون‌های کشِ
        // TblCase خوانده می‌شوند (که سرویس‌ها هنگام ذخیره نوشته‌اند) و مبلغِ
        // پیشنهادی از AssistanceRuleService.Evaluate — که عمداً بی‌عارضه است،
        // پس صرفِ نمایش هیچ رویدادی در تایم‌لاین ثبت نمی‌کند.
        // ═══════════════════════════════════════════════════════════════════
        private readonly ToolTip _suggestedAidTip = new ToolTip();

        private void RefreshCaseStatusStats()
        {
            if (lblStatCompletionPct == null) return;

            if (currentCaseId <= 0)
            {
                lblStatCompletionPct.Text = "—";
                lblStatCompletionStatus.Text = "—";
                lblStatVulnScore.Text = "—";
                lblStatSuggestedAid.Text = "—";
                lblStatVerifiedDocs.Text = "—";
                return;
            }

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                {
                    con.Open();

                    using (var cmd = new SQLiteCommand(
                        "SELECT IFNULL(CompletionPercent, -1) AS Pct, " +
                        "IFNULL(CompletionStatusCode, '') AS StatusCode, " +
                        "IFNULL(VulnerabilityScore, -1) AS Score, " +
                        "IFNULL(VulnerabilityBand, '') AS Band " +
                        "FROM TblCase WHERE CasID = @Id;", con))
                    {
                        cmd.Parameters.AddWithValue("@Id", currentCaseId);
                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                int pct = Convert.ToInt32(dr["Pct"]);
                                lblStatCompletionPct.Text = pct < 0 ? "—" : pct.ToString();

                                string statusCode = dr["StatusCode"].ToString();
                                lblStatCompletionStatus.Text = CompletionStatusDisplay(statusCode);
                                lblStatCompletionStatus.ForeColor = CompletionStatusColor(statusCode);

                                double score = Convert.ToDouble(dr["Score"]);
                                lblStatVulnScore.Text = score < 0 ? "—" : score.ToString("0.#");
                                lblStatVulnScore.ForeColor = BandColor(dr["Band"].ToString());
                            }
                        }
                    }

                    // شمارشِ تأییدِ اسناد — «تأییدشده از کل» (بخشِ تأییدِ اسناد).
                    using (var cmd = new SQLiteCommand(
                        "SELECT COUNT(*) AS Total, " +
                        "SUM(CASE WHEN IFNULL(IsVerified, 0) = 1 THEN 1 ELSE 0 END) AS Verified " +
                        "FROM TblDocs WHERE CasID = @Id AND IFNULL(IsArchived, 0) = 0;", con))
                    {
                        cmd.Parameters.AddWithValue("@Id", currentCaseId);
                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                int total = Convert.ToInt32(dr["Total"]);
                                int verified = dr["Verified"] == DBNull.Value ? 0 : Convert.ToInt32(dr["Verified"]);
                                lblStatVerifiedDocs.Text = verified + " / " + total;
                                lblStatVerifiedDocs.ForeColor = (total > 0 && verified < total)
                                    ? UiTheme.Warning : UiTheme.TextDark;
                            }
                        }
                    }
                }

                RefreshSuggestedAssistance();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RefreshCaseStatusStats failed: " + ex.Message);
            }
        }

        // مبلغِ پیشنهادی + توضیحِ قاعده. Evaluate (نه EvaluateAndLog) عمداً:
        // بازکردنِ پرونده نباید رویدادِ «قاعده تطبیق کرد» در تایم‌لاین بسازد.
        private void RefreshSuggestedAssistance()
        {
            try
            {
                Helpers.AssistanceRecommendation recommendation =
                    Helpers.AssistanceRuleService.Evaluate(currentCaseId);

                if (recommendation.HasMatch)
                {
                    lblStatSuggestedAid.Text = recommendation.RecommendedAmount.ToString("#,0");
                    lblStatSuggestedAid.ForeColor = UiTheme.Success;

                    // شرحِ محاسبه به‌صورتِ tooltip — بدونِ گرفتنِ فضای صفحه.
                    _suggestedAidTip.SetToolTip(lblStatSuggestedAid,
                        string.Join(Environment.NewLine, recommendation.Explanation.ToArray()));
                }
                else
                {
                    lblStatSuggestedAid.Text = "—";
                    lblStatSuggestedAid.ForeColor = UiTheme.TextMuted;
                    _suggestedAidTip.SetToolTip(lblStatSuggestedAid,
                        "هیچ قاعدهٔ مساعدتی با این پرونده تطبیق نکرد.");
                }
            }
            catch (Exception ex)
            {
                lblStatSuggestedAid.Text = "—";
                System.Diagnostics.Debug.WriteLine("RefreshSuggestedAssistance failed: " + ex.Message);
            }
        }

        private static string CompletionStatusDisplay(string code)
        {
            switch ((code ?? "").Trim())
            {
                case "COMPLETE":    return "کامل";
                case "IN_PROGRESS": return "در حال تکمیل";
                case "INCOMPLETE":  return "ناقص";
                default:            return "—";
            }
        }

        private static System.Drawing.Color CompletionStatusColor(string code)
        {
            switch ((code ?? "").Trim())
            {
                case "COMPLETE":    return UiTheme.Success;
                case "IN_PROGRESS": return UiTheme.Warning;
                case "INCOMPLETE":  return UiTheme.Danger;
                default:            return UiTheme.TextDark;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 5.5-C — تب تأمین مالی. همهٔ نوشتن‌ها از CaseFundingService
        // می‌گذرد (تایم‌لاین و بازمحاسبهٔ کامل‌بودن داخلِ خودِ سرویس‌اند).
        // ═══════════════════════════════════════════════════════════════════
        private void RefreshFundingTab()
        {
            if (dgvCaseFunding == null) return;

            if (currentCaseId <= 0)
            {
                dgvCaseFunding.DataSource = null;
                return;
            }

            try
            {
                dgvCaseFunding.DataSource = Helpers.CaseFundingService.GetCaseFunding(currentCaseId);

                HideFundingColumn("CaseFundingID");
                HideFundingColumn("FundingSourceID");
                HideFundingColumn("SponsorID");
                HideFundingColumn("IsActive");
                FundingHeader("FundingSourceName", "منبع تأمین مالی");
                FundingHeader("SponsorName", "خیّر");
                FundingHeader("StartDate", "از تاریخ");
                FundingHeader("EndDate", "تا تاریخ");
                FundingHeader("Notes", "یادداشت");
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری تأمین مالی: " + ex.Message);
            }
        }

        private void HideFundingColumn(string column)
        {
            if (dgvCaseFunding.Columns.Contains(column))
                dgvCaseFunding.Columns[column].Visible = false;
        }

        private void FundingHeader(string column, string text)
        {
            if (dgvCaseFunding.Columns.Contains(column))
                dgvCaseFunding.Columns[column].HeaderText = text;
        }

        private void btnFundingAssign_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه تخصیص منبع مالی ندارد.");
                return;
            }

            if (currentCaseId <= 0)
            {
                Msg.Show("اول پرونده را ذخیره یا انتخاب کنید");
                return;
            }

            var sources = Helpers.CaseFundingService.GetFundingSources(true);
            if (sources.Count == 0)
            {
                Msg.Show("هیچ منبع تأمین مالیِ فعالی تعریف نشده است. " +
                         "از داشبورد ← «منابع مالی و خیّرین» یکی بسازید.");
                return;
            }

            var sourceItems = new List<KeyValuePair<string, string>>();
            foreach (var option in sources)
                sourceItems.Add(new KeyValuePair<string, string>(option.ID.ToString(), option.Name));

            // «بدون خیّر» گزینهٔ نخست است چون تخصیصِ منبع بدونِ خیّر حالتِ رایج است.
            var sponsorItems = new List<KeyValuePair<string, string>>();
            sponsorItems.Add(new KeyValuePair<string, string>("0", "— بدون خیّر —"));
            foreach (var option in Helpers.CaseFundingService.GetSponsors(true))
                sponsorItems.Add(new KeyValuePair<string, string>(option.ID.ToString(), option.Name));

            Dictionary<string, string> values = CaseManagement.Enterprise.EntPrompt.Edit(this,
                "تخصیص منبع تأمین مالی",
                CaseManagement.Enterprise.EntField.Combo("Source", "منبع تأمین مالی", sourceItems[0].Key, sourceItems),
                CaseManagement.Enterprise.EntField.Combo("Sponsor", "خیّر", "0", sponsorItems),
                CaseManagement.Enterprise.EntField.Text("Start", "از تاریخ (yyyy-MM-dd)", DateTime.Today.ToString("yyyy-MM-dd")),
                CaseManagement.Enterprise.EntField.Text("End", "تا تاریخ (اختیاری)", ""),
                CaseManagement.Enterprise.EntField.Multiline("Notes", "یادداشت", ""));

            if (values == null) return;

            int sourceId, sponsorId;
            int.TryParse(values["Source"], out sourceId);
            int.TryParse(values["Sponsor"], out sponsorId);

            if (sourceId <= 0) { Msg.Show("منبع تأمین مالی انتخاب نشد."); return; }

            try
            {
                Helpers.CaseFundingService.AssignFunding(currentCaseId, sourceId,
                    sponsorId > 0 ? (int?)sponsorId : null,
                    values["Start"], values["End"], values["Notes"]);

                Msg.Show("منبع تأمین مالی تخصیص یافت");
                RefreshFundingTab();
                RefreshCaseStatusStats();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در تخصیص منبع مالی: " + ex.Message);
            }
        }

        private void btnFundingRemove_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه تغییر تأمین مالی ندارد.");
                return;
            }

            if (dgvCaseFunding.CurrentRow == null || !dgvCaseFunding.Columns.Contains("CaseFundingID"))
            {
                Msg.Show("اول یک تخصیص را از فهرست انتخاب کنید");
                return;
            }

            object idValue = dgvCaseFunding.CurrentRow.Cells["CaseFundingID"].Value;
            if (idValue == null || idValue == DBNull.Value) return;

            if (!UiTheme.ShowConfirm(this,
                    "این تخصیص غیرفعال شود؟ سابقهٔ آن در پرونده باقی می‌ماند.",
                    "حذف تخصیص تأمین مالی"))
                return;

            try
            {
                Helpers.CaseFundingService.RemoveFunding(Convert.ToInt32(idValue));
                Msg.Show("تخصیص غیرفعال شد");
                RefreshFundingTab();
                RefreshCaseStatusStats();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در حذف تخصیص: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Phase 5.5-C — خروجیِ «پروندهٔ کامل».
        //
        // متفاوت با btnExportExcel که گزارشِ *چندپرونده‌ای* با فیلتر است: این
        // یکی همهٔ بخش‌های همین پرونده را (خلاصه، اسناد، اسنادِ ناقص، امتیاز،
        // تأمین مالی، بازدید، مساعدت، و در پایان تایم‌لاین) در یک فایل می‌ریزد.
        // ═══════════════════════════════════════════════════════════════════
        private void btnExportCaseFile_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Print"))
            {
                Msg.Show("کاربر اجازه خروجی گرفتن از پرونده را ندارد.");
                return;
            }

            if (currentCaseId <= 0)
            {
                Msg.Show("اول پرونده را ذخیره یا از لیست انتخاب کن");
                return;
            }

            // آموزش — قبلاً اینجا یک کرکرهٔ دوگزینه‌ای بود و گزینهٔ «چاپ» برای
            // هر یک از سیزده بخش یک پیش‌نمایشِ جدا باز می‌کرد. حالا دیالوگِ
            // اختصاصی، انتخابِ بخش‌ها و چهار نوع خروجی را در یک جا می‌دهد و
            // سند پیوسته و صفحه‌بندی‌شده ساخته می‌شود.
            try
            {
                using (var frm = new Helpers.FrmCaseFileExport(
                           new DAL.DatabaseHelper(), currentCaseId, txtCode.Text.Trim()))
                    frm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در باز کردن خروجی پروندهٔ کامل: " + ex.Message);
            }
        }

        // کارتِ یک بخش را کامل (قاب + سربرگ) پنهان/آشکار می‌کند.
        private void SetCardVisible(System.Windows.Forms.Panel card, bool visible)
        {
            if (card == null) return;
            card.Visible = visible;
        }

        private void SetFieldGroupVisible(Helpers.FieldBox[] fields, bool visible)
        {
            if (fields == null) return;
            foreach (var box in fields)
            {
                if (box == null) continue;
                box.Visible = visible;
            }
        }

        private bool IsSuspendedStatus(string normalizedStatus)
        {
            return normalizedStatus == "قطع موقت" || normalizedStatus == "قطع";
        }

        private void UpdateStopReasonVisibility()
        {
            bool isSuspended = IsSuspendedStatus(NormalizeServiceStatus(txtServiceStatus.Text));

            lblStopReason.Visible = isSuspended;
            txtStopReason.Visible = isSuspended;
            if (caseFieldStopReason != null) caseFieldStopReason.Visible = isSuspended;

            lblSuspensionReason.Visible = isSuspended;
            txtSuspensionReason.Visible = isSuspended;
            if (caseFieldSuspensionReason != null) caseFieldSuspensionReason.Visible = isSuspended;

            if (!isSuspended)
            {
                txtStopReason.Text = "";
                txtSuspensionReason.SelectedIndex = -1;
            }
        }

        // «اسامی مؤسسات تحت پوشش» فقط وقتی معنی دارد که پاسخِ «تحت پوشش دیگر
        // مؤسسات» بله باشد — همان الگوی «دلیل قطع موقت» بالا. با انتخاب «خیر»
        // (یا خالی) کادر پنهان و محتوایش پاک می‌شود تا داده‌ی متناقض ذخیره نشود.
        private void UpdateCoveredByOrgNamesVisibility()
        {
            bool isCovered = txtCoveredByOrg.Text.Trim() == "بله";

            lblCoveredByOrgNames.Visible = isCovered;
            txtCoveredByOrgNames.Visible = isCovered;
            if (fieldCoveredByOrgNames != null)
                fieldCoveredByOrgNames.Visible = isCovered;

            if (!isCovered)
                txtCoveredByOrgNames.Text = "";
        }

        // آموزش — چرا مقدارِ ناشناخته به کشویی *افزوده* می‌شود و روی خانهٔ صفر
        // نمی‌افتد: رفتار قبلی (SelectedIndex = 0) وضعیتِ واقعیِ پرونده را بی‌صدا
        // با اولین آیتمِ فهرست جایگزین می‌کرد و با اولین ذخیره، همان مقدارِ غلط
        // در دیتابیس می‌نشست. تا وقتی خانهٔ صفر «فعال» بود این خطا بی‌ضرر به‌نظر
        // می‌رسید، ولی حالا خانهٔ صفر «متقاضی» است — یعنی یک پروندهٔ فعال می‌توانست
        // به «متقاضی» برگردد. نمایشِ مقدارِ واقعی همیشه از حدس زدن امن‌تر است:
        // کاربر خودش می‌بیند و در صورت نیاز تصحیح می‌کند.
        private void SetComboBoxText(ComboBox comboBox, string value)
        {
            value = NormalizeServiceStatus(value);

            int index = comboBox.FindStringExact(value);

            if (index >= 0)
            {
                comboBox.SelectedIndex = index;
                return;
            }

            if (!string.IsNullOrEmpty(value))
            {
                comboBox.SelectedIndex = comboBox.Items.Add(value);
                return;
            }

            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        // نگاشتِ مقادیرِ قدیمی حالا در Helpers.CaseDomain متمرکز است تا فرم،
        // ایمپورتِ اکسل و سینک همگی یک تعریف داشته باشند.
        private string NormalizeServiceStatus(string value)
        {
            value = (value ?? "").Trim();

            if (value == "")
                return Helpers.CaseDomain.StatusActive;

            return Helpers.CaseDomain.NormalizeServiceStatus(value);
        }

        // مقدار فیلترِ «وضعیت خدمات» گرید — رشته‌ی خالی یعنی «همه». هم گرید و هم
        // خروجی‌های جمعی (اکسل/چاپ کارت/خروجی بازه‌ی فرم) از همین یک منبع
        // می‌خوانند تا گزارش با آنچه کاربر روی صفحه می‌بیند یکی باشد.
        private string GetSelectedServiceStatusFilter()
        {
            if (cmbServiceStatusFilter == null ||
                cmbServiceStatusFilter.SelectedItem == null ||
                cmbServiceStatusFilter.Text.Trim() == "همه")
            {
                return "";
            }

            return NormalizeServiceStatus(cmbServiceStatusFilter.Text);
        }

        private bool IsAllowedServiceStatus(string value)
        {
            value = NormalizeServiceStatus(value);

            foreach (string status in serviceStatuses)
            {
                if (status == value)
                    return true;
            }

            return false;
        }

        private void NormalizeSavedServiceStatuses()
        {
            try
            {
                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(@"
                    UPDATE TblCase
                    SET ServiceStatus =
                        CASE
                            WHEN ServiceStatus = 'در حالت قطع' THEN 'قطع'
                            WHEN ServiceStatus IN ('درانتظار', 'در انتظار', 'در انتظار تأیید') THEN 'در انتظار تایید'
                            ELSE ServiceStatus
                        END
                    WHERE ServiceStatus IN ('در حالت قطع', 'درانتظار', 'در انتظار', 'در انتظار تأیید')", con))
                {
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[FrmCase.NormalizeSavedServiceStatuses] " + ex.Message);
            }
        }

        private string CleanFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "پرونده";

            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c.ToString(), "");

            value = value.Trim();

            if (string.IsNullOrWhiteSpace(value))
                return "پرونده";

            return value;
        }

        private void FrmCase_Load(object sender, EventArgs e)
        {
            // آموزش — رفع قطعی «دکمه‌ها زیر صفحه می‌روند»: به‌خاطر مقیاس‌بندی DPI
            // ویندوز (۱۲۵٪/۱۵۰٪)، فرم بزرگ‌تر از اندازه‌ی طراحی رندر می‌شود و از
            // ارتفاع صفحه بیرون می‌زند. اینجا فرم را با «ناحیه‌ی کاری واقعیِ صفحه»
            // (زیر نوار عنوان، بالای تسک‌بار) تطبیق می‌دهیم و وسط‌چین می‌کنیم؛ چون
            // ناحیه‌ی فیلدها اسکرول دارد، کوتاه‌شدن ارتفاع مشکلی ایجاد نمی‌کند و
            // دکمه‌های پایین (که Dock=Bottom هستند) همیشه دیده می‌شوند.
            // آموزش — FitToScreen اندازه‌ی پنجره را با ناحیه‌ی کاریِ صفحه تطبیق
            // می‌دهد (برای نمایشگرهای کوچک/مقیاس‌دار)؛ سپس MakeMainWindow همان
            // اندازه را به‌عنوان «حداقل» قفل می‌کند و پنجره را تمام‌صفحه می‌کند.
            // ترتیب مهم است: اول تطبیق با صفحه، بعد قفلِ حداقل.
            FitToScreen();
            UiTheme.MakeMainWindow(this, ClientSize.Width, ClientSize.Height);
            ConstrainBottomActionsWidth();

            // ستون‌های گرید با برگشتن به فرم دوباره بررسی می‌شوند تا تغییرِ
            // تنظیمات بدون بستن/باز کردن فرم اعمال شود.
            Activated -= FrmCase_Activated;
            Activated += FrmCase_Activated;

            Text = "پرونده‌ها  —  " + SecurityContext.CenterDisplay;
            txtFormNo.ReadOnly = true;
            txtFormNo.Enabled = true;
            txtFormNo.TabStop = false;
            txtPhotoPath.ReadOnly = true;
            txtFamilyPhotoPath.ReadOnly = true;

            picPhoto.SizeMode = PictureBoxSizeMode.StretchImage;
            picFamilyPhoto.SizeMode = PictureBoxSizeMode.StretchImage;

            KeyPreview = true;
            KeyDown -= FrmCase_KeyDown;
            KeyDown += FrmCase_KeyDown;
            // آموزش — افزودنِ شماره‌ی ردیف (خواسته‌ی صریح: «شماره ردیف در گرید
            // موجود نیست»): بدون ستونِ دیتاییِ اضافه، فقط رسمِ عدد در ناحیه‌ی
            // RowHeader — بدون هزینه‌ی کوئری/بایندینگِ اضافه.
            dgvCases.RowPostPaint -= DgvCases_RowPostPaint;
            dgvCases.RowPostPaint += DgvCases_RowPostPaint;
            ConfigureServiceStatusControls();
            NormalizeSavedServiceStatuses();
            txtServiceStatus.SelectedIndexChanged -= TxtServiceStatus_SelectedIndexChanged;
            txtServiceStatus.SelectedIndexChanged += TxtServiceStatus_SelectedIndexChanged;

            // Phase 3 (بازبینی) — بخش‌های اختصاصیِ نوع درخواست.
            txtRequestType.SelectedIndexChanged -= TxtRequestType_SelectedIndexChanged;
            txtRequestType.SelectedIndexChanged += TxtRequestType_SelectedIndexChanged;
            UpdateRequestTypeSectionVisibility();

            // آموزش — چک‌باکس سالم/معلول (به درخواست کاربر): با تیک «سالم»،
            // فیلدهای نوع/درجه معلولیت غیرفعال و خالی می‌شوند.
            chkHeadHealthy.CheckedChanged -= ChkHeadHealthy_CheckedChanged;
            chkHeadHealthy.CheckedChanged += ChkHeadHealthy_CheckedChanged;

            SetTabOrder();
            btnSave.TabStop = false;
            btnEdit.TabStop = false;
            btnDelete.TabStop = false;
            btnNew.TabStop = false;
            btnSearch.TabStop = false;
            btnDocs.TabStop = false;
            btnFamily.TabStop = false;
            Helpers.UiTheme.ApplyPersianDateColumns(dgvCases, "CaseDate");
            LoadLookupCombos();
            ApplyIncomingDashboardFilter();
            LoadCases();
            ClearForm();

            // فرم در حالتِ «فقط نمایش» باز می‌شود؛ برای پروندهٔ جدید کاربر
            // «جدید» و برای تغییرِ پروندهٔ موجود «ویرایش» را می‌زند.
            SetCaseEditMode(false);

            // اگر فرم برای «باز کردن یک پرونده‌ی مشخص» فراخوانی شده، همان را لود کن.
            if (_pendingOpenCaseId > 0)
            {
                try { LoadCaseById(_pendingOpenCaseId); }
                catch (Exception ex) { Debug.WriteLine("[FrmCase open pending] " + ex.Message); }
                _pendingOpenCaseId = 0;
            }
        }

        // آموزش — بارگذاری کمبوها از TblLookup در Load (نه InitializeComponent):
        // مقادیر هاردکد داخل Designer.cs به‌عنوان fallback باقی می‌مانند (اگر
        // دیتابیس لحظه‌ای در دسترس نبود یا این متد اجرا نشد، فرم باز هم مقداری
        // برای انتخاب دارد)، ولی اینجا با مقادیر واقعی/قابل‌ویرایش از تنظیمات
        // جایگزین می‌شوند. توجه: txtServiceStatus/cmbServiceStatusFilter عمداً
        // اینجا نیستند — TblCase.ServiceStatus یک CHECK constraint دیتابیسی
        // دارد (فقط ۴ مقدار مجاز)، پس نباید از تنظیمات آزادانه ویرایش شود.
        private void LoadLookupCombos()
        {
            Helpers.LookupHelper.FillCombo(txtProvince, "Province");
            Helpers.LookupHelper.FillCombo(txtRequestType, "RequestType");
            Helpers.LookupHelper.FillCombo(txtPriorityLevel, "PriorityLevel");
            Helpers.LookupHelper.FillCombo(txtReligion, "Madhab");
            Helpers.LookupHelper.FillCombo(txtHeadSadat, "HeadSadat");
            Helpers.LookupHelper.FillCombo(txtDisabilityType, "DisabilityType");
            Helpers.LookupHelper.FillCombo(txtMaritalStatus, "MaritalStatus");
            Helpers.LookupHelper.FillCombo(txtDisabilityDegree, "DisabilityDegree");
            Helpers.LookupHelper.FillCombo(txtEducationLevel, "HeadEducationLevel");
            Helpers.LookupHelper.FillCombo(txtCoveredByOrg, "CoveredByOrg");
            Helpers.LookupHelper.FillCombo(txtSuspensionReason, "SuspensionReason");

            // Phase 3 (بازبینی) — بخش‌های اختصاصیِ نوع درخواست.
            Helpers.LookupHelper.FillCombo(txtFatherDeathCause, "FatherDeathCause");
            Helpers.LookupHelper.FillCombo(txtDisabilityCause, "DisabilityCause");
            Helpers.LookupHelper.FillCombo(txtDisabilityCardStatus, "DisabilityCardStatus");
            Helpers.LookupHelper.FillCombo(txtHasMigrationCard, "HasMigrationCard");

            // Phase 4 — ماژول‌های تخصصی.
            Helpers.LookupHelper.FillCombo(txtFatherStatus, "FatherStatus");
            Helpers.LookupHelper.FillCombo(txtMotherStatus, "MotherStatus");
            Helpers.LookupHelper.FillCombo(txtGuardianRelationship, "GuardianRelationship");
            // تحصیلاتِ کودک از همان فهرستِ تحصیلاتِ عضو خانواده می‌آید (مکتب/
            // دانشگاه/…)، نه فهرستِ سرپرست — دو مفهومِ متفاوت با دو دستهٔ جدا.
            Helpers.LookupHelper.FillCombo(txtOrphanEducationLevel, "MemberEducation");

            // Phase 7 — نسبتِ نمایندهٔ قانونی.
            LoadRepresentativeLookups();

            // Phase 5 — تب بازدید میدانی.
            Helpers.LookupHelper.FillCombo(txtVisitResult, "VisitResult");
            Helpers.LookupHelper.FillCombo(txtVisitRecommendation, "VisitRecommendation");
        }
        private void btnChooseStorageFolder_Click(object sender, EventArgs e)
        {
            string oldRoot = FileHelper.GetBaseRootFolder();

            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "محل اصلی ذخیره عکس‌ها، اسناد، خروجی Word/PDF و گزارش‌های Excel را انتخاب کنید";
                fbd.ShowNewFolderButton = true;

                if (!string.IsNullOrWhiteSpace(oldRoot) && Directory.Exists(oldRoot))
                    fbd.SelectedPath = oldRoot;

                if (fbd.ShowDialog() != DialogResult.OK)
                    return;

                string newRoot = fbd.SelectedPath;
                string error;

                if (!FileHelper.SetBaseRootFolder(newRoot, out error))
                {
                    Msg.Show(error, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string finalRoot = FileHelper.GetBaseRootFolder();

                if (string.IsNullOrWhiteSpace(finalRoot))
                {
                    Msg.Show("محل ذخیره تنظیم نشد.", "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(txtCode.Text))
                {
                    string caseFolder = FileHelper.EnsureCaseStructure(txtCode.Text.Trim());

                    if (string.IsNullOrWhiteSpace(caseFolder))
                    {
                        Msg.Show(
                            "محل ذخیره تغییر کرد، اما ساخت پوشه‌های پرونده فعلی انجام نشد:" +
                            Environment.NewLine +
                            FileHelper.LastError,
                            "هشدار",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                }

                Msg.Show(
                    "محل ذخیره فایل‌ها با موفقیت تغییر کرد." +
                    Environment.NewLine +
                    Environment.NewLine +
                    "مسیر قبلی:" +
                    Environment.NewLine +
                    (string.IsNullOrWhiteSpace(oldRoot) ? "تنظیم نشده بود" : oldRoot) +
                    Environment.NewLine +
                    Environment.NewLine +
                    "مسیر جدید:" +
                    Environment.NewLine +
                    finalRoot +
                    Environment.NewLine +
                    Environment.NewLine +
                    "توجه: فایل‌های قبلی به صورت خودکار منتقل نمی‌شوند.",
                    "تغییر محل ذخیره",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }


        private void ChkHeadHealthy_CheckedChanged(object sender, EventArgs e)
        {
            UpdateHeadPhysicalState();
        }

        // با تیک «سالم است»، فیلدهای نوع/درجه معلولیت غیرفعال و خالی می‌شوند؛
        // با برداشتن تیک (یعنی معلول) دوباره فعال می‌شوند.
        private void UpdateHeadPhysicalState()
        {
            bool isHealthy = chkHeadHealthy.Checked;

            txtDisabilityType.Enabled = !isHealthy;
            txtDisabilityDegree.Enabled = !isHealthy;

            if (isHealthy)
            {
                txtDisabilityType.Text = "";
                txtDisabilityDegree.Text = "";
            }
        }

        private void ClearForm()
        {
            currentCaseId = 0;

            selectedHeadPhotoSource = "";
            selectedFamilyPhotoSource = "";

            savedHeadPhotoPath = "";
            savedFamilyPhotoPath = "";

            txtFormNo.Text = GetNextFormNo();
            txtCode.Text = "";
            txtCaseNo.Text = "";
            txtZone.Text = "";
            txtProvince.Text = "";
            txtDistrict.Items.Clear();
            txtDistrict.Text = "";
            txtSite.Text = "";
            txtRequestType.Text = "";
            txtPriorityLevel.Text = "";
            txtHeadFullName.Text = "";
            txtHeadFatherName.Text = "";
            txtHeadSadat.Text = "";
            txtReligion.Text = "";
            txtHeadTazkiraNo.Text = "";
            // Feature 4 — پروندهٔ تازه هرگز بدونِ وضعیتِ تذکره نمی‌ماند؛
            // پیش‌فرض «بدون تذکره» است نه خالی.
            cmbHeadIdCardType.Text = Helpers.IdCardHelper.NoneDisplay;
            txtHeadOriginalResidence.Text = "";
            txtHeadCurrentResidence.Text = "";
            txtRelationshipToFamily.Text = "";
            txtPhone.Text = "";
            txtRelativePhone.Text = "";
            txtCoveredByOrg.Text = "";
            txtCoveredByOrgNames.Text = "";
            txtJob.Text = "";
            txtSkill.Text = "";
            txtDisabilityDegree.Text = "";
            txtDisabilityType.Text = "";
            chkHeadHealthy.Checked = true; // پیش‌فرض «سالم»
            UpdateHeadPhysicalState();
            txtMigrationCardType.Text = "";
            txtMaritalStatus.Text = "";
            txtSurveyors.Text = "";
            txtLocationAddress.Text = "";
            txtEducationLevel.Text = "";
            if (txtServiceStatus.Items.Count > 0)
                txtServiceStatus.SelectedIndex = 0;
            else
                txtServiceStatus.Text = "";
            txtStopReason.Text = "";
            txtSuspensionReason.SelectedIndex = -1;
            UpdateStopReasonVisibility();
            txtUrgentSituation.Text = "";
            txtPhotoPath.Text = "";
            txtFamilyPhotoPath.Text = "";
            txtReferrerName.Text = "";
            txtReferrerPhone.Text = "";

            // Phase 3 (بازبینی) — بخش‌های اختصاصیِ نوع درخواست.
            txtMainResidenceProvince.Text = "";
            txtMainResidenceDistrict.Text = "";
            txtMainResidenceVillage.Text = "";
            txtDisabilityDescription.Text = "";
            txtSpecialNeeds.Text = "";
            txtDisabilityCardNumber.Text = "";
            txtMigrationCardNumber.Text = "";
            txtAssistanceDurationMonths.Text = "";
            dtpDepartureDate.Value = DateTime.Today;
            dtpArrivalDate.Value = DateTime.Today;

            // آموزش — رفع باگ «دکمه جدید همه فیلدها را خالی نمی‌کند»: این کمبوها
            // DropDownStyle=DropDownList دارند و برای آن‌ها «.Text = ""» هیچ اثری
            // ندارد (مقدار انتخاب‌شده باقی می‌ماند). پس صریحاً SelectedIndex=-1
            // می‌شوند تا واقعاً خالی شوند. (txtServiceStatus عمداً اینجا نیست چون
            // CHECK constraint دارد و بالاتر روی «فعال» تنظیم می‌شود.)
            ComboBox[] dropdowns =
            {
                txtZone, txtProvince, txtDistrict, txtRequestType, txtPriorityLevel,
                txtHeadSadat, txtReligion, txtCoveredByOrg, txtDisabilityType,
                txtDisabilityDegree, txtMaritalStatus, txtEducationLevel,
                // Phase 3 (بازبینی)
                txtFatherDeathCause, txtDisabilityCause, txtDisabilityCardStatus, txtHasMigrationCard
            };
            foreach (ComboBox cmb in dropdowns)
            {
                cmb.SelectedIndex = -1;
                cmb.Text = "";
            }

            // بعد از خالی‌شدن کمبوی «تحت پوشش»، کادر اسامی هم باید پنهان شود.
            UpdateCoveredByOrgNamesVisibility();

            // Phase 4 — فیلدهای ماژول هم باید خالی شوند، وگرنه مقدارِ پروندهٔ
            // قبلی روی «پروندهٔ جدید» باقی می‌ماند و ذخیره می‌شد.
            ClearCaseModuleFields();

            // Phase 7 — وگرنه نمایندهٔ پروندهٔ قبلی روی پروندهٔ جدید
            // می‌ماند و — چون نمایندهٔ اول الزامی است و اعتبارسنجی
            // را می‌گذراند — بی‌سروصدا ذخیره می‌شد.
            ClearRepresentativeSlot(1);
            ClearRepresentativeSlot(2);

            // Phase 5 — پروندهٔ جدید ⇒ فهرست و فرمِ بازدید هم باید خالی شوند.
            RefreshVisitsTab();
            // Phase 6 — همچنین تب خانواده.
            RefreshFamilyTab();
            RefreshVulnerabilityTab();
            // Phase 5.5-C — تأمین مالی و کارت‌های وضعیت.
            RefreshFundingTab();
            RefreshCaseStatusStats();
            // پروندهٔ جدید هنوز تاریخچه‌ای ندارد.
            RefreshTimelineTab();

            // Phase 3 (بازبینی) — نوع درخواست خالی است ⇒ هر سه بخشِ اختصاصی پنهان.
            UpdateRequestTypeSectionVisibility();

            dtpCaseDate.Value = DateTime.Today;
            dtpSurveyDate.Value = DateTime.Today;

            ClearPictureBox(picPhoto);
            ClearPictureBox(picFamilyPhoto);

            // آموزش — به‌درخواست کاربر: شماره فرم همیشه اتومات و یکتا و
            // غیرقابل ویرایش است؛ برخلاف قبل، اینجا دیگر ReadOnly باز نمی‌شود.
            txtFormNo.ReadOnly = true;
            txtFormNo.TabStop = false;

            txtCode.Enabled = true;
            txtCode.Focus();

            SyncMembersTab();
        }

        private bool ValidateForm()
        {
            if (txtFormNo.Text.Trim() == "")
                txtFormNo.Text = GetNextFormNo();

            if (txtCode.Text.Trim() == "")
            {
                Msg.Show("کد اختصاصی را وارد کنید");
                txtCode.Focus();
                return false;
            }

            if (txtHeadFullName.Text.Trim() == "")
            {
                Msg.Show("نام سرپرست را وارد کنید");
                txtHeadFullName.Focus();
                return false;
            }

            if (!IsAllowedServiceStatus(txtServiceStatus.Text))
            {
                Msg.Show("وضعیت خدمات را از لیست انتخاب کنید");
                txtServiceStatus.Focus();
                return false;
            }

            // Phase 3 — نوع درخواست هویتِ اصلیِ پرونده است؛ باید از فهرستِ
            // شش نوعِ مصوب (TblRequestType) انتخاب شود، نه متنِ آزاد.
            if (Helpers.ReferenceDataService.FindRequestTypeByName(txtRequestType.Text.Trim()) == null)
            {
                Msg.Show("نوع درخواست را از لیست انتخاب کنید");
                txtRequestType.Focus();
                return false;
            }

            if (IsSuspendedStatus(NormalizeServiceStatus(txtServiceStatus.Text)) &&
                string.IsNullOrWhiteSpace(txtSuspensionReason.Text))
            {
                Msg.Show("دلیل تعلیق را از لیست انتخاب کنید");
                txtSuspensionReason.Focus();
                return false;
            }

            // ─── اعتبارسنجی تذکره، وابسته به نوع انتخاب‌شده ───────────────────
            // قاعده کاملاً در IdCardHelper است تا با فرم اعضای خانواده یکی بماند.
            if (txtHeadTazkiraNo.Text.Trim().Length > 0)
            {
                string idCardError;
                if (!IdCardHelper.IsValid(cmbHeadIdCardType.Text, txtHeadTazkiraNo.Text, out idCardError))
                {
                    Msg.Show(idCardError);
                    if (string.IsNullOrWhiteSpace(cmbHeadIdCardType.Text))
                        cmbHeadIdCardType.Focus();
                    else
                        txtHeadTazkiraNo.Focus();
                    return false;
                }
            }

            // Phase 7 — نمایندهٔ قانونی: خواستهٔ صریحِ «از ذخیرهٔ دادهٔ
            // نامعتبر جلوگیری کن». آخرین بررسی است چون ممکن است
            // کاربر را به تبِ دیگری ببرد؛ خطاهای همین تب اول دیده
            // می‌شوند.
            if (!ValidateRepresentatives())
                return false;

            return true;
        }

        // عکسِ پرسنلی: سرپرست خانوار، سرپرست کودک و نمایندهٔ قانونی — هر سه
        // یک جنس‌اند و یک قاعده دارند (تصمیمِ کاربر: ۵۰ تا ۵۰۰ کیلوبایت).
        private bool IsValidImageFile(string filePath)
        {
            return IsValidImageFile(
                filePath,
                Helpers.PhotoRules.MinPortraitBytes,
                Helpers.PhotoRules.MaxPortraitBytes,
                null);
        }

        private bool IsValidFamilyPhotoFile(string filePath)
        {
            return IsValidImageFile(
                filePath,
                MinFamilyPhotoFileSizeBytes,
                MaxFamilyPhotoFileSizeBytes,
                null);
        }

        // وارسیِ واقعی در Helpers/PhotoRules است؛ اینجا فقط پیامش نشان داده
        // می‌شود. sizeErrorMessage اختیاری مانده تا فراخوانندهٔ قدیمی نشکند.
        private bool IsValidImageFile(string filePath, long minBytes, long maxBytes, string sizeErrorMessage)
        {
            string reason;
            if (Helpers.PhotoRules.IsValidPhoto(filePath, minBytes, maxBytes, out reason))
                return true;

            Msg.Show(string.IsNullOrWhiteSpace(sizeErrorMessage) ? reason : sizeErrorMessage);
            return false;
        }

        private void ClearPictureBox(PictureBox pictureBox)
        {
            if (pictureBox.Image != null)
            {
                DrawingImage oldImage = pictureBox.Image;
                pictureBox.Image = null;
                oldImage.Dispose();
            }
        }

        private void LoadImageToPictureBox(string filePath, PictureBox pictureBox)
        {
            ClearPictureBox(pictureBox);

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (DrawingImage img = DrawingImage.FromStream(fs))
                {
                    pictureBox.Image = new Bitmap(img);
                }
            }
            catch (Exception ex)
            {
                ClearPictureBox(pictureBox);
                Msg.Show("خطا در بارگذاری عکس: " + ex.Message);
            }
        }

        private void AddStringParameter(SQLiteCommand cmd, string parameterName, string value)
        {
            cmd.Parameters.AddWithValue(parameterName, value ?? "");
        }

        private void AddDateParameter(SQLiteCommand cmd, string parameterName, DateTime value)
        {
            cmd.Parameters.AddWithValue(parameterName, value.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void AddIntParameter(SQLiteCommand cmd, string parameterName, int value)
        {
            cmd.Parameters.AddWithValue(parameterName, value);
        }

        private void AddCaseParameters(SQLiteCommand cmd, bool includeFormNo)
        {
            if (includeFormNo)
            {
                int formNo;
                if (!int.TryParse(txtFormNo.Text.Trim(), out formNo))
                    throw new InvalidOperationException("شماره فرم باید عددی باشد");

                AddIntParameter(cmd, "@FormNo", formNo);
            }

            AddStringParameter(cmd, "@Code", txtCode.Text.Trim());
            AddStringParameter(cmd, "@CaseNo", txtCaseNo.Text.Trim());
            AddDateParameter(cmd, "@CaseDate", dtpCaseDate.Value.Date);
            AddStringParameter(cmd, "@Zone", txtZone.Text.Trim());
            AddStringParameter(cmd, "@Province", txtProvince.Text.Trim());
            AddStringParameter(cmd, "@District", txtDistrict.Text.Trim());
            AddStringParameter(cmd, "@Site", txtSite.Text.Trim());
            AddStringParameter(cmd, "@RequestType", txtRequestType.Text.Trim());
            // Phase 3 — هویتِ مرجعِ تازه، در کنارِ ستونِ متنیِ قدیمی (dual-write).
            // ValidateForm از قبل تضمین می‌کند این مقدار پیدا می‌شود.
            var requestTypeRef = Helpers.ReferenceDataService.FindRequestTypeByName(txtRequestType.Text.Trim());
            AddIntParameter(cmd, "@RequestTypeID", requestTypeRef != null ? requestTypeRef.ID : 1);
            AddStringParameter(cmd, "@PriorityLevel", txtPriorityLevel.Text.Trim());
            AddStringParameter(cmd, "@HeadFullName", txtHeadFullName.Text.Trim());
            AddStringParameter(cmd, "@HeadFatherName", txtHeadFatherName.Text.Trim());
            AddStringParameter(cmd, "@HeadSadat", txtHeadSadat.Text.Trim());
            AddStringParameter(cmd, "@Religion", txtReligion.Text.Trim());
            AddStringParameter(cmd, "@HeadTazkiraNo", txtHeadTazkiraNo.Text.Trim());
            AddStringParameter(cmd, "@HeadIdCardType", cmbHeadIdCardType.Text.Trim());
            AddStringParameter(cmd, "@HeadOriginalResidence", txtHeadOriginalResidence.Text.Trim());
            AddStringParameter(cmd, "@HeadCurrentResidence", txtHeadCurrentResidence.Text.Trim());
            AddStringParameter(cmd, "@RelationshipToFamily", txtRelationshipToFamily.Text.Trim());
            AddStringParameter(cmd, "@Phone", txtPhone.Text.Trim());
            AddStringParameter(cmd, "@RelativePhone", txtRelativePhone.Text.Trim());
            AddStringParameter(cmd, "@CoveredByOrg", txtCoveredByOrg.Text.Trim());
            // اسامی فقط وقتی ذخیره می‌شود که پاسخ «بله» باشد؛ در غیر این صورت
            // خالی ثبت می‌شود تا داده‌ی متناقض («خیر» ولی با اسامی) نماند.
            AddStringParameter(cmd, "@CoveredByOrgNames",
                txtCoveredByOrg.Text.Trim() == "بله" ? txtCoveredByOrgNames.Text.Trim() : "");
            AddStringParameter(cmd, "@Job", txtJob.Text.Trim());
            AddStringParameter(cmd, "@Skill", txtSkill.Text.Trim());
            AddStringParameter(cmd, "@DisabilityDegree", txtDisabilityDegree.Text.Trim());
            AddStringParameter(cmd, "@DisabilityType", txtDisabilityType.Text.Trim());
            AddStringParameter(cmd, "@MigrationCardType", txtMigrationCardType.Text.Trim());
            AddStringParameter(cmd, "@MaritalStatus", txtMaritalStatus.Text.Trim());
            AddStringParameter(cmd, "@Surveyors", txtSurveyors.Text.Trim());
            AddDateParameter(cmd, "@SurveyDate", dtpSurveyDate.Value.Date);
            AddStringParameter(cmd, "@LocationAddress", txtLocationAddress.Text.Trim());
            AddStringParameter(cmd, "@EducationLevel", txtEducationLevel.Text.Trim());
            bool isSuspended = IsSuspendedStatus(NormalizeServiceStatus(txtServiceStatus.Text));
            AddStringParameter(cmd, "@ServiceStatus", NormalizeServiceStatus(txtServiceStatus.Text));
            var serviceStatusRef = Helpers.ReferenceDataService.FindServiceStatusByName(NormalizeServiceStatus(txtServiceStatus.Text));
            AddIntParameter(cmd, "@ServiceStatusID", serviceStatusRef != null ? serviceStatusRef.ID : 1);
            AddStringParameter(cmd, "@StopReason", isSuspended ? txtStopReason.Text.Trim() : "");
            AddStringParameter(cmd, "@SuspensionReason", isSuspended ? txtSuspensionReason.Text.Trim() : "");
            AddStringParameter(cmd, "@UrgentSituation", txtUrgentSituation.Text.Trim());
            AddStringParameter(cmd, "@PhotoPath", txtPhotoPath.Text.Trim());
            AddStringParameter(cmd, "@FamilyPhotoPath", txtFamilyPhotoPath.Text.Trim());
            // Phase 3 — معرف (اختیاری، برای همهٔ انواع پرونده).
            AddStringParameter(cmd, "@ReferrerName", txtReferrerName.Text.Trim());
            AddStringParameter(cmd, "@ReferrerPhone", txtReferrerPhone.Text.Trim());

            // Phase 3 (بازبینی) — بخش‌های اختصاصیِ نوع درخواست. همیشه پارامتر
            // ارسال می‌شود (حتی اگر بخش پنهان باشد)؛ چون UpdateRequestTypeSectionVisibility
            // فقط کنترل‌ها را پنهان می‌کند، پاک نمی‌کند، مقدارِ خالی طبیعی است.
            AddStringParameter(cmd, "@MainResidenceProvince", txtMainResidenceProvince.Text.Trim());
            AddStringParameter(cmd, "@MainResidenceDistrict", txtMainResidenceDistrict.Text.Trim());
            AddStringParameter(cmd, "@MainResidenceVillage", txtMainResidenceVillage.Text.Trim());
            AddStringParameter(cmd, "@FatherDeathCause", txtFatherDeathCause.Text.Trim());

            AddStringParameter(cmd, "@DisabilityCause", txtDisabilityCause.Text.Trim());
            AddStringParameter(cmd, "@DisabilityDescription", txtDisabilityDescription.Text.Trim());
            AddStringParameter(cmd, "@SpecialNeeds", txtSpecialNeeds.Text.Trim());
            AddStringParameter(cmd, "@DisabilityCardStatus", txtDisabilityCardStatus.Text.Trim());
            AddStringParameter(cmd, "@DisabilityCardNumber", txtDisabilityCardNumber.Text.Trim());

            AddStringParameter(cmd, "@HasMigrationCard", txtHasMigrationCard.Text.Trim());
            AddStringParameter(cmd, "@MigrationCardNumber", txtMigrationCardNumber.Text.Trim());
            AddDateParameter(cmd, "@DepartureDate", dtpDepartureDate.Value.Date);
            AddDateParameter(cmd, "@ArrivalDate", dtpArrivalDate.Value.Date);

            int assistanceDurationMonths;
            int.TryParse(txtAssistanceDurationMonths.Text.Trim(), out assistanceDurationMonths);
            if (string.IsNullOrWhiteSpace(txtAssistanceDurationMonths.Text.Trim()))
                cmd.Parameters.AddWithValue("@AssistanceDurationMonths", DBNull.Value);
            else
                AddIntParameter(cmd, "@AssistanceDurationMonths", assistanceDurationMonths);
        }

        // مقادیرِ «مُهرِ تعلیق» را برای درج/به‌روزرسانی آماده می‌کند: اگر وضعیتِ
        // فعلیِ فرم معلق است، تاریخ/کاربرِ اکنون؛ در غیر این صورت خالی (پاک
        // می‌شود). برای UPDATE، عبارتِ CASE در متنِ کوئری تصمیم می‌گیرد که آیا
        // این مقدارِ تازه واقعاً نوشته شود یا مقدارِ قبلیِ ستون حفظ شود (فقط
        // وقتی وضعیت واقعاً عوض شده باشد بازنویسی می‌شود).
        private void AddSuspensionStampParameters(SQLiteCommand cmd)
        {
            bool isSuspended = IsSuspendedStatus(NormalizeServiceStatus(txtServiceStatus.Text));

            if (isSuspended)
            {
                AddDateParameter(cmd, "@SuspensionDate", DateTime.Now.Date);
                cmd.Parameters.AddWithValue("@SuspendedByUserId",
                    Helpers.SecurityContext.UserId > 0 ? (object)Helpers.SecurityContext.UserId : DBNull.Value);
                AddStringParameter(cmd, "@SuspendedByUsername", Helpers.SecurityContext.Username);
            }
            else
            {
                cmd.Parameters.AddWithValue("@SuspensionDate", DBNull.Value);
                cmd.Parameters.AddWithValue("@SuspendedByUserId", DBNull.Value);
                cmd.Parameters.AddWithValue("@SuspendedByUsername", DBNull.Value);
            }
        }

        private bool IsFormNoExists(string formNo, int excludedCaseId)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("SELECT COUNT(1) FROM TblCase WHERE FormNo = @Value AND CasID <> @CasID", con))
            {
                AddStringParameter(cmd, "@Value", formNo);
                AddIntParameter(cmd, "@CasID", excludedCaseId);

                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private bool IsCodeExists(string code, int excludedCaseId)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("SELECT COUNT(1) FROM TblCase WHERE Code = @Value AND CasID <> @CasID", con))
            {
                AddStringParameter(cmd, "@Value", code);
                AddIntParameter(cmd, "@CasID", excludedCaseId);

                con.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private bool IsSamePath(string path1, string path2)
        {
            if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2))
                return false;

            try
            {
                return string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void SaveSelectedPhotos()
        {
            string caseCode = txtCode.Text.Trim();

            if (selectedHeadPhotoSource != "")
            {
                if (IsSamePath(selectedHeadPhotoSource, savedHeadPhotoPath))
                {
                    txtPhotoPath.Text = savedHeadPhotoPath;
                }
                else
                {
                    string savedPath = FileHelper.SaveFileToCaseFolder(
                        selectedHeadPhotoSource,
                        caseCode,
                        FileHelper.SectionHeadPhoto,
                        caseCode + "-Head",
                        savedHeadPhotoPath);

                    if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
                        throw new Exception("عکس سرپرست ذخیره نشد: " + FileHelper.LastError);

                    txtPhotoPath.Text = savedPath;
                    savedHeadPhotoPath = savedPath;
                }
            }
            else
            {
                txtPhotoPath.Text = savedHeadPhotoPath;
            }

            if (selectedFamilyPhotoSource != "")
            {
                if (IsSamePath(selectedFamilyPhotoSource, savedFamilyPhotoPath))
                {
                    txtFamilyPhotoPath.Text = savedFamilyPhotoPath;
                }
                else
                {
                    string savedPath = FileHelper.SaveFileToCaseFolder(
                        selectedFamilyPhotoSource,
                        caseCode,
                        FileHelper.SectionFamilyPhoto,
                        caseCode + "-Family",
                        savedFamilyPhotoPath);

                    if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
                        throw new Exception("عکس جمعی خانواده ذخیره نشد: " + FileHelper.LastError);

                    txtFamilyPhotoPath.Text = savedPath;
                    savedFamilyPhotoPath = savedPath;
                }
            }
            else
            {
                txtFamilyPhotoPath.Text = savedFamilyPhotoPath;
            }
        }

        // اعمالِ فیلترِ واردشده از داشبورد: وضعیتِ خدمات روی همان کمبویِ
        // موجودِ فرم (cmbServiceStatusFilter) گذاشته می‌شود تا از همان مکانیزمِ
        // موجود استفاده شود؛ ولایت/ولسوالی چون کنترلِ متناظری در این فرم
        // نداشتند، به‌صورت نواری خبری بالای گرید نشان داده می‌شوند.
        private void ApplyIncomingDashboardFilter()
        {
            bool hasProvinceOrDistrict = _incomingFilterProvince.Length > 0 || _incomingFilterDistrict.Length > 0;
            bool hasStatus = _incomingFilterServiceStatus.Length > 0;

            if (hasStatus)
            {
                for (int i = 0; i < cmbServiceStatusFilter.Items.Count; i++)
                {
                    if (string.Equals(cmbServiceStatusFilter.Items[i].ToString(), _incomingFilterServiceStatus, StringComparison.Ordinal))
                    {
                        cmbServiceStatusFilter.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (!hasProvinceOrDistrict && !hasStatus) return;

            string text = "فیلترِ داشبورد فعال است: ";
            var parts = new System.Collections.Generic.List<string>();
            if (_incomingFilterProvince.Length > 0) parts.Add("ولایت=" + _incomingFilterProvince);
            if (_incomingFilterDistrict.Length > 0) parts.Add("ولسوالی=" + _incomingFilterDistrict);
            if (hasStatus) parts.Add("وضعیت=" + _incomingFilterServiceStatus);
            text += string.Join("، ", parts) + " — فهرست و جستجو فقط همین محدوده را نشان می‌دهند.";

            _dashboardFilterBanner = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = UiTheme.Warning };
            Label lbl = new Label
            {
                Text = text, Dock = DockStyle.Fill, ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeSmall), TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0)
            };
            Button btnClear = UiTheme.CreateSecondaryButton("نمایش همه", "✕");
            btnClear.Size = new Size(110, 26);
            btnClear.Margin = new Padding(4);
            btnClear.Dock = DockStyle.Left;
            btnClear.Click += delegate
            {
                _incomingFilterProvince = "";
                _incomingFilterDistrict = "";
                _incomingFilterServiceStatus = "";
                cmbServiceStatusFilter.SelectedIndex = 0;
                _dashboardFilterBanner.Visible = false;
                LoadCases();
            };
            _dashboardFilterBanner.Controls.Add(lbl);
            _dashboardFilterBanner.Controls.Add(btnClear);

            Control gridContainer = dgvCases.Parent;
            if (gridContainer != null)
                gridContainer.Controls.Add(_dashboardFilterBanner);
        }

        // آموزش — رفعِ کندیِ بازشدنِ FrmCase با ~۱۲۰۰۰ پرونده: به‌جای بارگذاریِ
        // کل جدول، فقط ۲۰۰ پرونده‌ی آخر (جدیدترین‌ها) در گرید نشان داده
        // می‌شود. جستجو (btnSearch/dgvCases_CellClick از طریق LoadCaseByCode/
        // LoadCaseById) مستقل از این گرید مستقیماً از دیتابیس می‌خواند، پس
        // هیچ پرونده‌ای برای جستجو غیرقابل‌دسترس نمی‌شود.
        // آموزش — خواستهٔ صریحِ کاربر (بندِ GRID DATA REQUIREMENT): گرید هرگز
        // صدها ردیف را یک‌جا بار نکند؛ فقط ۱۰ پروندهٔ آخر بر اساسِ «شمارهٔ فرم»
        // نزولی نشان داده شود. سقفِ قدیمیِ MaxGridRows=200 جایش را به
        // GridPageSize + LIMIT/OFFSET داد؛ بقیهٔ پرونده‌ها از راهِ نوارِ
        // صفحه‌بندیِ زیرِ گرید در دسترس می‌مانند (وگرنه دسترسی قطع می‌شد).
        //
        // آموزش — چرا CAST: ستون FormNo متنی است، پس مرتب‌سازیِ متنی «۹» را
        // بعد از «۱۰» می‌گذارد. CAST به عدد، «جدیدترین شمارهٔ فرم» را واقعاً
        // اول می‌آورد. CasID به‌عنوان مرتب‌سازیِ دوم می‌ماند تا ترتیب برای
        // فرم‌های بی‌شماره/تکراری قطعی و پایدار باشد (وگرنه صفحه‌بندی با
        // OFFSET می‌تواند یک ردیف را دو بار یا هرگز نشان ندهد).
        private const int GridPageSize = 10;

        private int _gridPage;        // شمارهٔ صفحهٔ جاری، از صفر
        private int _gridTotalRows;   // تعداد کلِ ردیف‌های منطبق با فیلترهای فعلی

        // مقادیرِ نوارِ جستجوی سریع که آخرین بار اعمال شده‌اند. نگه‌داشتنشان لازم
        // است چون با رفتن به صفحهٔ بعد باید همان جستجو دوباره اجرا شود.
        private string _qsCode = "", _qsHead = "", _qsTazkira = "", _qsPhone = "";

        // شرطِ مشترکِ هر دو کوئری (شمارش و صفحه). یک‌جا نوشته شده تا شمارشِ کل
        // و ردیف‌های نمایش‌داده‌شده هرگز از هم واگرا نشوند.
        private string BuildCasesWhere()
        {
            return @"
                    WHERE (@Code = '' OR " + CaseSearchTypeColumns[0] + @" LIKE '%' || @Code || '%')
                      AND (@Head = '' OR " + CaseSearchTypeColumns[1] + @" LIKE '%' || @Head || '%')
                      AND (@Tazkira = '' OR " + CaseSearchTypeColumns[2] + @" LIKE '%' || @Tazkira || '%')
                      AND (@Phone = '' OR " + CaseSearchTypeColumns[3] + @" LIKE '%' || @Phone || '%')
                      AND (@CID = 0 OR CenterID = @CID)
                      AND (@ServiceStatus = '' OR ServiceStatus = @ServiceStatus)
                      AND (@Prov = '' OR Province = @Prov)
                      AND (@Dist = '' OR District LIKE '%' || @Dist || '%')";
        }

        private void BindCasesParameters(SQLiteCommand cmd)
        {
            AddStringParameter(cmd, "@Code", _qsCode);
            AddStringParameter(cmd, "@Head", _qsHead);
            AddStringParameter(cmd, "@Tazkira", _qsTazkira);
            AddStringParameter(cmd, "@Phone", _qsPhone);
            cmd.Parameters.AddWithValue("@CID", Helpers.SecurityContext.CenterFilterId);
            // فیلترِ وضعیت خدمات از داشبورد می‌آید؛ کنترلش از چیدمان حذف شد اما
            // خودش (و این مسیر) دست‌نخورده باقی مانده است.
            AddStringParameter(cmd, "@ServiceStatus", GetSelectedServiceStatusFilter());
            AddStringParameter(cmd, "@Prov", _incomingFilterProvince);
            AddStringParameter(cmd, "@Dist", _incomingFilterDistrict);
        }

        // بارگذاریِ پیش‌فرضِ گرید: بدونِ جستجو، از صفحهٔ اول.
        private void LoadCases()
        {
            _qsCode = _qsHead = _qsTazkira = _qsPhone = "";
            _gridPage = 0;
            RunCasesQuery();
        }

        // اجرای واقعیِ کوئری برای صفحهٔ جاری. تنها نقطه‌ای که گرید پر می‌شود.
        private void RunCasesQuery()
        {
            try
            {
                using (var con = db.GetConnection())
                {
                    con.Open();

                    using (var counter = new SQLiteCommand(
                        "SELECT COUNT(*) FROM TblCase " + BuildCasesWhere(), con))
                    {
                        BindCasesParameters(counter);
                        _gridTotalRows = Convert.ToInt32(counter.ExecuteScalar());
                    }

                    int lastPage = _gridTotalRows <= 0 ? 0 : (_gridTotalRows - 1) / GridPageSize;
                    if (_gridPage > lastPage) _gridPage = lastPage;
                    if (_gridPage < 0) _gridPage = 0;

                    using (var cmd = new SQLiteCommand(@"
                    SELECT CasID, FormNo, Code, CaseNo, HeadFullName, Phone, ServiceStatus, CaseDate, PhotoPath,
                           HeadFatherName, HeadTazkiraNo, HeadCurrentResidence, Province, District, RequestType
                    FROM TblCase " + BuildCasesWhere() + @"
                    ORDER BY CAST(FormNo AS INTEGER) DESC, CasID DESC
                    LIMIT @Take OFFSET @Skip", con))
                    {
                        BindCasesParameters(cmd);
                        cmd.Parameters.AddWithValue("@Take", GridPageSize);
                        cmd.Parameters.AddWithValue("@Skip", _gridPage * GridPageSize);

                        using (var reader = cmd.ExecuteReader())
                        {
                            DataTable dt = new DataTable();
                            dt.Load(reader);
                            dgvCases.DataSource = dt;
                        }
                    }
                }

                ConfigureCasesGrid();
                // فقط وقتی ستون عکس انتخاب شده باشد کاری می‌کند (خودش بررسی
                // می‌کند)، پس برای بقیه‌ی حالت‌ها هزینه‌ای ندارد.
                LoadCaseThumbnails();
                UpdatePagerUi();
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری لیست: " + ex.Message);
            }
        }

        // ─── نوارِ صفحه‌بندی ───────────────────────────────────────────────────
        private void UpdatePagerUi()
        {
            if (lblGridPage == null || lblGridTotal == null) return;

            int pageCount = _gridTotalRows <= 0 ? 1 : (_gridTotalRows + GridPageSize - 1) / GridPageSize;
            lblGridPage.Text  = (_gridPage + 1) + " / " + pageCount;
            lblGridTotal.Text = "تعداد کل: " + _gridTotalRows.ToString("N0");

            btnGridFirst.Enabled = btnGridPrev.Enabled = _gridPage > 0;
            btnGridNext.Enabled  = btnGridLast.Enabled = _gridPage < pageCount - 1;
        }

        private void GoToGridPage(int page)
        {
            int pageCount = _gridTotalRows <= 0 ? 1 : (_gridTotalRows + GridPageSize - 1) / GridPageSize;
            if (page < 0) page = 0;
            if (page > pageCount - 1) page = pageCount - 1;
            if (page == _gridPage) return;

            _gridPage = page;
            RunCasesQuery();
        }

        private void btnGridFirst_Click(object sender, EventArgs e) { GoToGridPage(0); }
        private void btnGridPrev_Click(object sender, EventArgs e)  { GoToGridPage(_gridPage - 1); }
        private void btnGridNext_Click(object sender, EventArgs e)  { GoToGridPage(_gridPage + 1); }
        private void btnGridLast_Click(object sender, EventArgs e)  { GoToGridPage(int.MaxValue); }

        // آموزش — جستجوی نوارِ بالای فرم: چهار فیلد (کد پرونده/نام سرپرست/
        // شمارهٔ تذکره/شمارهٔ تماس)؛ هرکدام که پر باشد با AND اضافه می‌شود،
        // دقیقاً روی همان ستون‌های CaseSearchTypeColumns. تنها تفاوت با قبل این
        // است که نتیجه هم صفحه‌بندی می‌شود، نه اینکه تا ۲۰۰ ردیف یک‌جا بیاید.
        private void SearchCasesGrid()
        {
            string code    = txtQsCode.Text.Trim();
            string head    = txtQsHeadName.Text.Trim();
            string tazkira = txtQsTazkira.Text.Trim();
            string phone   = txtQsPhone.Text.Trim();

            if (code == "" && head == "" && tazkira == "" && phone == "")
            {
                Msg.Show("حداقل یکی از فیلدهای جستجو را پر کنید");
                txtQsCode.Focus();
                return;
            }

            _qsCode    = code;
            _qsHead    = head;
            _qsTazkira = tazkira;
            _qsPhone   = phone;
            _gridPage  = 0;
            RunCasesQuery();
        }

        // بازگشت گرید به حالتِ پیش‌فرض (۱۰ پروندهٔ آخر، صفحهٔ اول) — همان LoadCases.
        private void ClearCaseSearchGrid()
        {
            txtQsCode.Text = "";
            txtQsHeadName.Text = "";
            txtQsTazkira.Text = "";
            txtQsPhone.Text = "";
            LoadCases();
        }

        // آموزش — فاز A2: «جستجوی پیشرفته» فرمِ کاملاً موجود FrmAdvancedSearch
        // را باز می‌کند — دقیقاً همان الگویی که از قبل در FrmDashboard.cs برای
        // همین دکمه استفاده می‌شود؛ هیچ منطق/فرم جدیدی ساخته نشد.
        private void btnAdvancedSearch_Click(object sender, EventArgs e)
        {
            using (var frm = new FrmAdvancedSearch())
                frm.ShowDialog(this);
        }

        private void btnQuickSearch_Click(object sender, EventArgs e)
        {
            SearchCasesGrid();
        }

        private void btnQuickSearchClear_Click(object sender, EventArgs e)
        {
            ClearCaseSearchGrid();
        }

        // Enter در هرکدام از چهار فیلدِ نوار جستجوی سریع، جستجو را اجرا می‌کند —
        // همان رفتاری که نوارِ قدیمیِ بالای گرید داشت.
        private void QuickSearchField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SearchCasesGrid();
            }
        }

        private const string PhotoThumbColumnName = "colPhotoThumb";

        // آموزش — رفع درخواست کاربر: لیست پرونده‌ها فقط سه ستون (کد اختصاصی/
        // نام سرپرست/عکس) را نشان می‌دهد تا بدون اسکرول افقی کامل در پنل جا
        // شود. بقیه ستون‌های دیتای بارگذاری‌شده (CasID برای انتخاب ردیف، و
        // بقیه برای منطق داخلی) پنهان می‌مانند نه حذف — چون dgvCases_CellClick
        // و بقیه کد همچنان به مقدار CasID هر ردیف نیاز دارند.
        // آموزش — ستون‌های این گرید حالا از تنظیمات می‌آیند (درخواست کاربر):
        // «کد اختصاصی» همیشه ثابت است و علاوه بر آن حداکثر چهار ستون از فهرست
        // CaseGridColumns انتخاب می‌شود، تا گرید بدون اسکرول افقی جا شود.
        // بقیه‌ی ستون‌های کوئری (مثل CasID) پنهان می‌شوند نه حذف — چون
        // dgvCases_CellClick و بقیه‌ی کد به مقدارشان نیاز دارند.
        private void ConfigureCasesGrid()
        {
            if (dgvCases.Columns.Count == 0)
                return;

            List<CaseGridColumn> selected = CaseGridColumns.GetSelected();
            _appliedGridColumnsCsv = CaseGridColumns.ToCsv(selected);

            // ۱) همه‌ی ستون‌های داده‌ای پنهان می‌شوند، بعد فقط انتخاب‌شده‌ها
            //    دوباره روشن می‌گردند. این‌طوری افزودن ستون جدید به کوئری هرگز
            //    باعث نشتِ یک ستونِ ناخواسته به گرید نمی‌شود.
            foreach (DataGridViewColumn column in dgvCases.Columns)
                column.Visible = false;

            // ۲) ستون تصویری: فقط وقتی کاربر «عکس» را انتخاب کرده باشد ساخته
            //    می‌شود. اگر انتخاب نشده باشد، حذفش می‌کنیم تا ردیف‌ها کوتاه
            //    بمانند و هزینه‌ی ساخت thumbnail هم پرداخت نشود.
            bool wantsPhoto = selected.Any(c => c.IsPhoto);

            if (!wantsPhoto)
            {
                if (dgvCases.Columns.Contains(PhotoThumbColumnName))
                    dgvCases.Columns.Remove(PhotoThumbColumnName);
            }
            else if (!dgvCases.Columns.Contains(PhotoThumbColumnName))
            {
                var photoColumn = new DataGridViewImageColumn
                {
                    Name = PhotoThumbColumnName,
                    HeaderText = "عکس",
                    ImageLayout = DataGridViewImageCellLayout.Zoom,
                    // بدون این، سلول‌های بدون عکس آیکونِ «تصویر خراب» نشان می‌دهند
                    // — همان ظاهر غیرحرفه‌ای که قبلاً باعث حذف این ستون شده بود.
                    DefaultCellStyle = { NullValue = null }
                };
                dgvCases.Columns.Add(photoColumn);
            }

            // ۳) ستون ثابت + انتخاب‌شده‌ها به ترتیب.
            SetGridHeader(CaseGridColumns.FixedColumn, CaseGridColumns.FixedColumnTitle);
            ShowGridColumn(CaseGridColumns.FixedColumn, 0);

            int displayIndex = 1;
            foreach (CaseGridColumn column in selected)
            {
                string name = column.IsPhoto ? PhotoThumbColumnName : column.DataColumn;

                if (!column.IsPhoto)
                    SetGridHeader(name, column.DisplayName);

                ShowGridColumn(name, displayIndex);
                displayIndex++;
            }

            dgvCases.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvCases.MultiSelect = false;
            dgvCases.ReadOnly = true;
            dgvCases.AllowUserToAddRows = false;
            dgvCases.AllowUserToDeleteRows = false;
            dgvCases.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            // با عکس، ردیف بلندتر لازم است تا thumbnail بریده نشود.
            dgvCases.RowTemplate.Height = wantsPhoto ? 46 : 32;

            // آموزش — با شش ستون در ستونِ باریکِ سمت چپ، حالتِ Fill عرض را
            // مساوی پخش می‌کرد و عنوان‌ها بریده می‌شدند («کد اختصاصی» → «کد»).
            // دو کار این را حل می‌کند بدون اینکه اسکرولِ افقی لازم شود:
            //   ۱) عنوان‌ها اجازهٔ شکستنِ خط دارند و ارتفاعِ سرستون خودکار
            //      می‌شود، پس عنوانِ دوکلمه‌ای در دو خط کامل دیده می‌شود.
            //   ۲) ستونِ عکس فقط به‌اندازهٔ خودِ thumbnail وزن می‌گیرد، نه یک
            //      ششمِ عرض؛ فضای آزادشده به ستون‌های متنی می‌رسد.
            dgvCases.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvCases.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

            foreach (DataGridViewColumn column in dgvCases.Columns)
            {
                if (!column.Visible) continue;
                column.FillWeight = column is DataGridViewImageColumn ? 46f : 100f;
            }
        }

        // آخرین ترکیبِ ستونی که روی گرید اعمال شده. برای اینکه تغییرِ تنظیمات
        // «فوری» اعمال شود بدون اینکه هر بار فعال‌شدنِ فرم، گرید بی‌دلیل از نو
        // ساخته و thumbnailها دوباره تولید شوند.
        private string _appliedGridColumnsCsv;

        // اگر کاربر ستون‌ها را در فرم تنظیمات عوض کرده باشد، با برگشتن به این
        // فرم بلافاصله اعمال می‌شود (نیازی به بستن و باز کردن فرم نیست).
        private void FrmCase_Activated(object sender, EventArgs e)
        {
            string current = CaseGridColumns.ToCsv(CaseGridColumns.GetSelected());

            if (string.Equals(current, _appliedGridColumnsCsv, StringComparison.OrdinalIgnoreCase))
                return;

            ConfigureCasesGrid();
            LoadCaseThumbnails();
        }

        private void ShowGridColumn(string columnName, int displayIndex)
        {
            if (!dgvCases.Columns.Contains(columnName))
                return;

            DataGridViewColumn column = dgvCases.Columns[columnName];
            column.Visible = true;

            if (displayIndex < dgvCases.Columns.Count)
                column.DisplayIndex = displayIndex;
        }

        private void HideGridColumn(string columnName)
        {
            if (dgvCases.Columns.Contains(columnName))
                dgvCases.Columns[columnName].Visible = false;
        }

        // آموزش — بارگذاری عکس کوچک هر ردیف از PhotoPath ذخیره‌شده؛ تصویر در
        // اندازه کوچک (۴۰×۴۰) ساخته می‌شود تا حافظه/کارایی گرید برای تعداد
        // زیاد ردیف مشکل ایجاد نکند، نه تصویر اصلی با رزولوشن کامل.
        // آموزش — سقف ایمن برای جلوگیری از فریز UI: تولید thumbnail برای هر ردیف
        // یک FileStream + Bitmap روی UI thread است. با ده‌ها هزار پرونده این کار
        // برنامه را برای چند ثانیه/دقیقه قفل می‌کرد. با این سقف، همه‌ی ردیف‌های
        // داده همچنان نمایش داده می‌شوند (هیچ رکوردی پنهان نمی‌شود) و فقط برای
        // تعداد زیاد، عکسِ کوچکِ درون‌گرید تولید نمی‌شود؛ عکس اصلی با باز کردن
        // پرونده کاملاً در دسترس است.
        private const int MaxInlineThumbnails = 500;

        private void LoadCaseThumbnails()
        {
            if (!dgvCases.Columns.Contains("PhotoPath") || !dgvCases.Columns.Contains(PhotoThumbColumnName))
                return;

            bool skipImages = dgvCases.Rows.Count > MaxInlineThumbnails;

            // آموزش — رفعِ کندیِ واقعیِ باز شدنِ FrmCase با ~۱۲۰۰۰ پرونده (با
            // آزمونِ زمان‌سنجیِ واقعی تأیید شد): وقتی skipImages=true هیچ
            // thumbnail‌ای نمایش داده نمی‌شود، پس بلندترکردنِ ردیف (Height=46)
            // هیچ فایده‌ی بصری‌ای ندارد و صرفاً هزینه‌ی بی‌مصرف است. حلقه‌ی
            // پیمایشِ ۱۲۰۰۰ ردیف (فقط برای همین Height) خودِ علتِ اصلیِ کندی
            // بود، نه بارگذاریِ فایل‌های تصویری (که از قبل با MaxInlineThumbnails
            // درست skip می‌شد). برای حالتِ ≤۵۰۰ ردیف هیچ تغییری نکرده.
            if (skipImages)
                return;

            foreach (DataGridViewRow row in dgvCases.Rows)
            {
                if (row.IsNewRow)
                    continue;

                row.Height = 46;

                object pathValue = row.Cells["PhotoPath"].Value;
                string path = pathValue == null || pathValue == DBNull.Value ? "" : pathValue.ToString();
                row.Cells[PhotoThumbColumnName].Value = LoadThumbnail(path, 40);
            }
        }

        private DrawingImage LoadThumbnail(string path, int size)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (DrawingImage source = DrawingImage.FromStream(fs))
                {
                    Bitmap thumb = new Bitmap(size, size);
                    using (Graphics g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(source, 0, 0, size, size);
                    }
                    return thumb;
                }
            }
            catch
            {
                return null;
            }
        }

        private void SetGridHeader(string columnName, string headerText)
        {
            if (dgvCases.Columns.Contains(columnName))
                dgvCases.Columns[columnName].HeaderText = headerText;
        }

        private void btnBrowsePhoto_Click(object sender, EventArgs e)
        {
            if (txtCode.Text.Trim() == "")
            {
                Msg.Show("اول کد اختصاصی را وارد کن");
                txtCode.Focus();
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "انتخاب عکس سرپرست";
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png|فایل‌های JPG|*.jpg;*.jpeg|فایل‌های PNG|*.png";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (!IsValidImageFile(ofd.FileName))
                        return;

                    selectedHeadPhotoSource = ofd.FileName;
                    txtPhotoPath.Text = ofd.FileName;
                    LoadImageToPictureBox(ofd.FileName, picPhoto);
                }
            }
        }

        private void btnBrowseFamilyPhoto_Click(object sender, EventArgs e)
        {
            if (txtCode.Text.Trim() == "")
            {
                Msg.Show("اول کد اختصاصی را وارد کن");
                txtCode.Focus();
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "انتخاب عکس جمعی خانواده";
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;
                ofd.Filter = "فایل‌های تصویری|*.jpg;*.jpeg;*.png|فایل‌های JPG|*.jpg;*.jpeg|فایل‌های PNG|*.png";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (!IsValidFamilyPhotoFile(ofd.FileName))
                        return;

                    selectedFamilyPhotoSource = ofd.FileName;
                    txtFamilyPhotoPath.Text = ofd.FileName;
                    LoadImageToPictureBox(ofd.FileName, picFamilyPhoto);
                }
            }
        }

        // ─── حذفِ عکس ────────────────────────────────────────────────────────
        // آموزش — تا امروز عکسِ سرپرست و عکسِ جمعی را فقط می‌شد *عوض* کرد، نه
        // برداشت؛ اگر کاربر اشتباهی عکس می‌گذاشت هیچ راهی برای پاک‌کردنش
        // نداشت (قیّم و نماینده از قبل دکمهٔ حذف داشتند). با خالی‌شدنِ
        // savedHeadPhotoPath، متدِ SaveSelectedPhotos ستونِ مسیر را خالی
        // می‌نویسد. فایلِ روی دیسک عمداً پاک نمی‌شود — همان محافظه‌کاریِ
        // FrmDocs و عکسِ نماینده.
        private void btnClearPhoto_Click(object sender, EventArgs e)
        {
            if (picPhoto.Image == null && savedHeadPhotoPath == "" && selectedHeadPhotoSource == "")
                return;

            if (!UiTheme.ShowConfirm(this, "عکس سرپرست از این پرونده برداشته شود؟", "حذف عکس"))
                return;

            ClearPictureBox(picPhoto);
            selectedHeadPhotoSource = "";
            savedHeadPhotoPath = "";
            txtPhotoPath.Text = "";
        }

        private void btnClearFamilyPhoto_Click(object sender, EventArgs e)
        {
            if (picFamilyPhoto.Image == null && savedFamilyPhotoPath == "" && selectedFamilyPhotoSource == "")
                return;

            if (!UiTheme.ShowConfirm(this, "عکس جمعی خانواده از این پرونده برداشته شود؟", "حذف عکس"))
                return;

            ClearPictureBox(picFamilyPhoto);
            selectedFamilyPhotoSource = "";
            savedFamilyPhotoPath = "";
            txtFamilyPhotoPath.Text = "";
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            ClearForm();
            // پروندهٔ جدید طبیعتاً باید بلافاصله قابلِ تایپ باشد.
            SetCaseEditMode(true);
            EnsureEditableTabVisible();
            txtCode.Focus();
        }

        // آموزش — رفعِ باگِ «دکمه جدید کار نمی‌کند»: تب‌های «خلاصه پرونده»،
        // «اعضاء خانواده» و «اسناد پرونده» هیچ‌کدام فیلدِ قابلِ ویرایشِ *پرونده*
        // ندارند (اولی همیشه خواندنی است و دوتای بعدی فرم‌های مستقلِ خودشان
        // هستند). اگر کاربر روی یکی از این‌ها باشد و «جدید»/«ویرایش» بزند،
        // ظاهراً هیچ اتفاقی نمی‌افتد چون فیلدهای بازشده اصلاً دیده نمی‌شوند.
        // پس در آن حالت خودکار به تب «مشخصات کلی سرپرست» می‌رویم.
        private void EnsureEditableTabVisible()
        {
            if (tabsCase == null || tabHeadInfo == null)
                return;

            TabPage current = tabsCase.SelectedTab;

            bool onNonEditableTab =
                current == null ||
                current == tabsCase.TabPages[0] ||               // خلاصه پرونده
                current == tabMembersHost.Parent ||              // اعضاء خانواده
                current == tabDocsHost.Parent;                   // اسناد پرونده

            if (onNonEditableTab)
                tabsCase.SelectedTab = tabHeadInfo;
        }

        // ═══════════════════════════════════════════════════════════════════
        // هشدارِ «سندِ الزامی آپلود نشده» در لحظهٔ ذخیره.
        //
        // آموزش — چرا هشدار و نه مانع: مانعِ واقعی از قبل هست و جای درستش
        // فعال‌سازیِ خدمت است (CaseActivationValidator پروندهٔ بدونِ سندِ
        // اجباری را «فعال» نمی‌کند). ذخیرهٔ خودِ پرونده نباید بسته باشد،
        // وگرنه کاربر نمی‌تواند اطلاعاتِ نیمه‌کاره را نگه دارد و بعداً سند را
        // اسکن کند — همان چرخه‌ای که «چاپ ← امضا ← اسکن» ذاتاً چندمرحله‌ای
        // است. پس اینجا فقط یادآوری می‌شود، آن‌هم یک‌بار و بدون تکرار.
        //
        // آموزش — دو فهرستِ جدا: «مفقود» یعنی هیچ ردیفی در آن دسته نیست؛
        // «ناقص» یعنی ردیف هست ولی فایلی به آن پیوست نشده (DocFilePath خالی).
        // هر دو مانعِ فعال‌سازی‌اند، پس هر دو باید دیده شوند.
        // ═══════════════════════════════════════════════════════════════════
        private void WarnMissingRequiredDocuments(int caseId)
        {
            if (caseId <= 0) return;

            try
            {
                var missing = Helpers.RequiredDocumentService.GetMissingRequiredCategories(caseId);
                var incomplete = Helpers.RequiredDocumentService.GetIncompleteRequiredCategories(caseId);
                if ((missing == null || missing.Count == 0) &&
                    (incomplete == null || incomplete.Count == 0)) return;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("پرونده ذخیره شد، ولی اسناد الزامی کامل نیست:");
                sb.AppendLine();

                if (missing != null && missing.Count > 0)
                {
                    sb.AppendLine("• آپلود نشده:");
                    foreach (var m in missing)
                        sb.AppendLine("   – " + m.Name);
                }

                if (incomplete != null && incomplete.Count > 0)
                {
                    sb.AppendLine("• ثبت شده ولی فایلی پیوست نشده:");
                    foreach (var m in incomplete)
                        sb.AppendLine("   – " + m.Name);
                }

                sb.AppendLine();
                sb.Append("تا تکمیل نشدن این اسناد، خدمت این پرونده فعال نمی‌شود. " +
                          "برای چاپ فورم و ضمیمهٔ نسخهٔ امضاشده، تب «اسناد» → دکمهٔ «چاپ فورم رسمی».");

                Msg.Show(sb.ToString());
            }
            catch
            {
                // هشدار هرگز نباید ذخیرهٔ موفق را به خطا تبدیل کند.
            }
        }

        // الزام نسخهٔ تحویلی (مورد ۱) — هشدارِ «پروندهٔ مشابه» پیش از ذخیره.
        // خروجی true یعنی «ادامه بده».
        //
        // هشدار عمداً *نرم* است، نه مانعِ قطعی: دو مددجوی هم‌نامِ واقعی در این
        // جامعه کاملاً محتمل‌اند و مسدودکردنِ ثبت، دادهٔ درست را غیرقابلِ ورود
        // می‌کرد. ولی کاربر باید آگاهانه تصمیم بگیرد و ردِ تصمیمش بماند —
        // برای همین تأییدِ ادامه در حسابرسی ثبت می‌شود.
        private bool ConfirmNoSimilarCase(int excludeCasId)
        {
            List<Helpers.DuplicateMatch> similar;
            try
            {
                similar = Helpers.DuplicateDetector.FindSimilarCases(
                    txtHeadFullName.Text, txtHeadFatherName.Text,
                    txtPhone.Text, txtHeadCurrentResidence.Text, excludeCasId);
            }
            catch
            {
                return true;   // هشدار نباید ذخیره را بشکند.
            }

            if (similar == null || similar.Count == 0) return true;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("پروندهٔ زیر با اطلاعات واردشده شباهت بالا دارد:");
            sb.AppendLine();

            foreach (var m in similar.Take(5))
            {
                sb.Append("• ").Append(m.SimilarityPercent).Append("٪ — ");
                sb.Append(string.IsNullOrWhiteSpace(m.CodeB) ? ("فرم " + m.FormNoB) : ("کد " + m.CodeB));
                sb.Append(" — ").Append(m.NameB);
                if (!string.IsNullOrWhiteSpace(m.FatherB)) sb.Append(" فرزند ").Append(m.FatherB);
                sb.Append("   (تطابق: ").Append(string.Join(" + ", m.MatchedFields)).AppendLine(")");
            }

            if (similar.Count > 5)
                sb.AppendLine("• … و " + (similar.Count - 5) + " مورد دیگر");

            sb.AppendLine();
            sb.Append("اگر این همان شخص است، پروندهٔ موجود را ویرایش کنید نه اینکه پروندهٔ تازه بسازید." +
                      Environment.NewLine + "آیا مطمئن هستید که می‌خواهید ادامه دهید؟");

            if (!UiTheme.ShowConfirm(this, sb.ToString(), "احتمال پروندهٔ تکراری"))
                return false;

            try
            {
                AuditLogger.Log("ثبت با وجود شباهت", "TblCase", excludeCasId, "",
                    string.Join(" | ", similar.Take(5).Select(
                        m => m.SimilarityPercent + "% => " + m.CodeB + " " + m.NameB)));
            }
            catch { /* حسابرسی نباید ذخیره را بشکند. */ }

            return true;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه ثبت پرونده ندارد.");
                return;
            }

            // آموزش — حالتِ نمایش/ویرایش: قبلاً این دکمه فقط برای رکوردِ جدید
            // بود و برای رکوردِ موجود پیام «از دکمه ویرایش استفاده کن» می‌داد.
            // حالا «ویرایش» فقط قفل را باز می‌کند و ثبتِ نهایی — چه درج و چه
            // به‌روزرسانی — با همین دکمه انجام می‌شود. منطقِ SQL هر دو مسیر
            // دست‌نخورده است؛ فقط مسیرِ به‌روزرسانی به UpdateCurrentCase منتقل شد.
            if (currentCaseId != 0)
            {
                if (UpdateCurrentCase())
                    SetCaseEditMode(false);
                return;
            }

            if (!ValidateForm())
                return;

            // Phase 5.5-A — دروازهٔ فعال‌سازی. برای پروندهٔ *جدید* هنوز سندی
            // ثبت نشده، پس اگر کاربر مستقیماً «فعال» را انتخاب کرده باشد
            // مسدود می‌شود و راهنماییِ دقیق می‌گیرد.
            if (!PassesActivationGate(0, ""))
                return;

            txtFormNo.Text = GetNextFormNo();

            try
            {
                if (IsFormNoExists(txtFormNo.Text.Trim(), 0))
                {
                    txtFormNo.Text = GetNextFormNo();

                    if (IsFormNoExists(txtFormNo.Text.Trim(), 0))
                    {
                        Msg.Show("شماره فرم تکراری است. دوباره دکمه ذخیره را بزنید");
                        return;
                    }
                }

                if (IsCodeExists(txtCode.Text.Trim(), 0))
                {
                    Msg.Show("کد اختصاصی تکراری است");
                    txtCode.Focus();
                    return;
                }

                // آموزش — هشدارِ تذکرهٔ تکراری: بررسیِ نرم، نه مانعِ قطعی. اگر
                // همین شماره در پرونده/خانواده/متقاضیِ دیگری هم ثبت شده باشد،
                // کاربر باید صراحتاً تأیید کند تا ثبت ادامه یابد؛ دلیلِ لغو
                // شدن با هیچ ردی ثبت نمی‌شود چون هنوز رکوردی ساخته نشده است.
                string tazkiraAuditNote = null;
                List<string> tazkiraMatches = DuplicateDetector.FindByTazkira(txtHeadTazkiraNo.Text.Trim(), "TblCase", 0);
                if (tazkiraMatches.Count > 0)
                {
                    if (!UiTheme.ShowConfirm(this,
                        "این شماره تذکره قبلاً برای موارد زیر ثبت شده است:\n\n" +
                        string.Join("\n", tazkiraMatches) +
                        "\n\nآیا مطمئن هستید که می‌خواهید ادامه دهید؟", "احتمال ثبت تکراری"))
                        return;

                    tazkiraAuditNote = "تذکره " + txtHeadTazkiraNo.Text.Trim() + " => " + string.Join(" | ", tazkiraMatches);
                }

                // الزام نسخهٔ تحویلی (مورد ۱) — هشدارِ پروندهٔ مشابه.
                // مکملِ بررسیِ تذکره بالا، نه جایگزینِ آن: تذکره مقایسهٔ دقیق
                // است و این یکی شباهت. پروندهٔ تکراری معمولاً تذکره ندارد یا
                // تذکره‌اش با یک رقم اختلاف وارد شده، پس دقیقاً از کنارِ آن
                // بررسی رد می‌شد.
                if (!ConfirmNoSimilarCase(0))
                    return;

                SaveSelectedPhotos();

                using (SQLiteConnection con = db.GetConnection())
                {
                    string query = @"INSERT INTO TblCase
                    (
                        FormNo, Code, CaseNo, CaseDate,
                        Zone, Province, District, Site, RequestType, RequestTypeID, PriorityLevel,
                        HeadFullName, HeadFatherName, HeadSadat, Religion, HeadTazkiraNo, HeadIdCardType,
                        HeadOriginalResidence, HeadCurrentResidence, RelationshipToFamily,
                        Phone, RelativePhone, CoveredByOrg, CoveredByOrgNames, Job, Skill,
                        DisabilityDegree, DisabilityType, MigrationCardType, MaritalStatus,
                        Surveyors, SurveyDate, LocationAddress, EducationLevel, ServiceStatus, ServiceStatusID, StopReason, SuspensionReason, UrgentSituation,
                        PhotoPath, FamilyPhotoPath, CenterID, SuspensionDate, SuspendedByUserId, SuspendedByUsername,
                        ReferrerName, ReferrerPhone, EntrySourceCode, CreatedByUserId, CreatedByUsername,
                        MainResidenceProvince, MainResidenceDistrict, MainResidenceVillage, FatherDeathCause,
                        DisabilityCause, DisabilityDescription, SpecialNeeds, DisabilityCardStatus, DisabilityCardNumber,
                        HasMigrationCard, MigrationCardNumber, DepartureDate, ArrivalDate, AssistanceDurationMonths
                    )
                    VALUES
                    (
                        @FormNo, @Code, @CaseNo, @CaseDate,
                        @Zone, @Province, @District, @Site, @RequestType, @RequestTypeID, @PriorityLevel,
                        @HeadFullName, @HeadFatherName, @HeadSadat, @Religion, @HeadTazkiraNo, @HeadIdCardType,
                        @HeadOriginalResidence, @HeadCurrentResidence, @RelationshipToFamily,
                        @Phone, @RelativePhone, @CoveredByOrg, @CoveredByOrgNames, @Job, @Skill,
                        @DisabilityDegree, @DisabilityType, @MigrationCardType, @MaritalStatus,
                        @Surveyors, @SurveyDate, @LocationAddress, @EducationLevel, @ServiceStatus, @ServiceStatusID, @StopReason, @SuspensionReason, @UrgentSituation,
                        @PhotoPath, @FamilyPhotoPath, @CenterID, @SuspensionDate, @SuspendedByUserId, @SuspendedByUsername,
                        @ReferrerName, @ReferrerPhone, @EntrySourceCode, @CreatedByUserId, @CreatedByUsername,
                        @MainResidenceProvince, @MainResidenceDistrict, @MainResidenceVillage, @FatherDeathCause,
                        @DisabilityCause, @DisabilityDescription, @SpecialNeeds, @DisabilityCardStatus, @DisabilityCardNumber,
                        @HasMigrationCard, @MigrationCardNumber, @DepartureDate, @ArrivalDate, @AssistanceDurationMonths
                    );";

                    using (var cmd = new SQLiteCommand(query, con))
                    {
                        AddCaseParameters(cmd, true);
                        cmd.Parameters.AddWithValue("@CenterID", Helpers.SecurityContext.CurrentCenterId > 0
                            ? Helpers.SecurityContext.CurrentCenterId : 1);
                        AddSuspensionStampParameters(cmd);

                        // Phase 3 — منشأ ثبت: فقط اینجا (مسیرِ درج دستی) نوشته
                        // می‌شود؛ مسیرِ سینک مقدار خودش را در SyncApplier می‌زند.
                        AddStringParameter(cmd, "@EntrySourceCode", "MANUAL");
                        cmd.Parameters.AddWithValue("@CreatedByUserId",
                            Helpers.SecurityContext.IsLoggedIn ? (object)Helpers.SecurityContext.UserId : DBNull.Value);
                        AddStringParameter(cmd, "@CreatedByUsername", Helpers.SecurityContext.Username ?? "");

                        con.Open();
                        cmd.ExecuteNonQuery();

                        // آموزش — رفع نشت resource: قبلاً این SQLiteCommand بدون
                        // Dispose ساخته می‌شد و هر ذخیره یک command رهاشده باقی
                        // می‌گذاشت. حالا داخل using بسته می‌شود.
                        using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", con))
                            currentCaseId = Convert.ToInt32((long)idCmd.ExecuteScalar());
                    }
                }

                selectedHeadPhotoSource = "";
                selectedFamilyPhotoSource = "";

                txtFormNo.ReadOnly = true;
                txtFormNo.TabStop = false;
                txtCode.Enabled = false;

                AuditLogger.Log("ثبت", "TblCase", currentCaseId, "", BuildCurrentCaseAuditText());
                AuditLogger.RecordStatusChange(currentCaseId, "", NormalizeServiceStatus(txtServiceStatus.Text),
                    txtSuspensionReason.Text.Trim(), txtStopReason.Text.Trim());

                // Phase 3 — تایم‌لاینِ مرکزی (در کنارِ AuditLogger/RecordStatusChange
                // موجود، نه جایگزینِ آن‌ها).
                Helpers.TimelineService.LogCaseCreated(currentCaseId, txtCode.Text.Trim());

                // Phase 6 — پروندهٔ تازه ریشهٔ خانوارِ خودش می‌شود
                // (FamilyGroupID = CasID). فقط برای رکوردِ *جدید*؛ ویرایش
                // پیوندِ خانوادگیِ موجود را دست نمی‌زند.
                Helpers.FamilyGroupService.EnsureRoot(currentCaseId);

                // Phase 4 — ماژول‌های تخصصی (فقط بخش‌های دیده‌شده). خودش
                // تایم‌لاین و بازمحاسبهٔ کامل‌بودن را انجام می‌دهد.
                SaveCaseModules(currentCaseId);
                // Phase 7 — همان قاعده: فقط وقتی بخش دیده می‌شود.
                SaveCaseRepresentatives(currentCaseId);

                // پیش از فاز ۴ — زیرساختِ کامل‌بودنِ پرونده: محاسبه و ذخیره در
                // ستون‌های کشِ TblCase (برای داشبورد/گزارش/فیلترِ فازِ بعدی).
                Helpers.CaseCompletionService.RecalculateAndStore(currentCaseId);
                // Phase 5.5-B — امتیاز پس از کامل‌بودن حساب می‌شود، چون
                // CompletionStatus خودش یکی از حقایقِ امتیازدهی است.
                Helpers.VulnerabilityScoreService.RecalculateAndStore(
                    currentCaseId, Helpers.VulnerabilityScoreService.ReasonCaseSaved);

                if (tazkiraAuditNote != null)
                    AuditLogger.Log("هشدار تذکره تکراری - تأیید کاربر", "TblCase", currentCaseId, "", tazkiraAuditNote);

                // آموزش — تاریخچهٔ کاملِ رکورد: AuditLogger فقط هفت فیلدِ منتخب را
                // ثبت می‌کند (BuildCurrentCaseAuditText)، پس تغییرِ باقیِ فیلدهای
                // پرونده هیچ ردی نداشت. VersionService یک «عکس فوریِ کاملِ ردیف»
                // در EntRecordVersion نگه می‌دارد و فیلدهای تغییرکرده را خودش
                // نسبت به نسخهٔ قبل حساب می‌کند. زیرساخت از قبل موجود بود ولی به
                // هیچ مسیر ذخیره‌ای وصل نشده بود. خطای این متد هرگز بالا نمی‌آید
                // (داخل خودش catch دارد)، پس ذخیرهٔ پرونده تحت تأثیر قرار نمی‌گیرد.
                CaseManagement.Enterprise.VersionService.Capture("TblCase", currentCaseId,
                    CaseManagement.Enterprise.VersionService.OperationInsert);

                // آموزش — رفعِ یک شکافِ جدی در همگام‌سازی: SyncService فقط از صفِ
                // SyncOutbox می‌خواند (GetPending) و هیچ مسیرِ پویشیِ جایگزینی
                // ندارد. این فرم — برخلافِ FrmFamily/FrmDocs/FrmFinance — هیچ‌وقت
                // چیزی در صف ثبت نمی‌کرد، یعنی پروندهٔ تازه‌ثبت‌شده به سرور
                // نمی‌رسید مگر اتفاقی کسی برایش کمکِ مالی ثبت می‌کرد
                // (تنها جایی که TblCase در صف ثبت می‌شد: FrmFinance).
                CaseManagement.Sync.SyncOutboxService.Capture("TblCase", currentCaseId,
                    CaseManagement.Sync.OfflineSyncInitializer.OperationCreate);

                Msg.Show("اطلاعات با موفقیت ذخیره شد");
                WarnMissingRequiredDocuments(currentCaseId);
                LoadCases();
                SyncMembersTab();
                SetCaseEditMode(false);   // درج موفق → بازگشت به حالت نمایش
            }
            catch (SQLiteException ex)
            {
                Msg.Show("خطا در ذخیره: " + ex.Message);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ذخیره: " + ex.Message);
            }
        }

        // آموزش — حالتِ نمایش/ویرایش: این دکمه دیگر مستقیماً روی دیتابیس
        // نمی‌نویسد؛ فقط قفلِ فیلدها را باز می‌کند. ثبتِ نهایی با دکمه‌ی
        // «ذخیره» انجام می‌شود که به UpdateCurrentCase (همان منطقِ قبلیِ این
        // متد، بدون تغییر) می‌رسد.
        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Edit"))
            {
                Msg.Show("کاربر فقط مشاهده اجازه ویرایش پرونده ندارد.");
                return;
            }

            if (currentCaseId == 0)
            {
                Msg.Show("اول رکورد را انتخاب کن");
                return;
            }

            if (!SetCaseEditMode(true))
                return;

            EnsureEditableTabVisible();
        }

        // منطقِ به‌روزرسانی — عیناً همان کدی که قبلاً داخل btnEdit_Click بود.
        // خروجی: true یعنی ذخیره موفق بود (تا فراخوان بتواند فرم را قفل کند).
        private bool UpdateCurrentCase()
        {
            if (!ValidateForm())
                return false;

            try
            {
                CenterGuard.EnsureCaseAccess(db, currentCaseId);

                string oldValue = GetCaseAuditTextFromDb(currentCaseId);
                string oldStatus = GetCaseStatusById(currentCaseId);
                string oldRequestType = GetCaseRequestTypeById(currentCaseId);
                string oldSuspensionReason = GetCaseSuspensionReasonById(currentCaseId);

                // Phase 5.5-A — دروازهٔ فعال‌سازی. فقط وقتی وضعیت واقعاً به
                // «فعال» *تغییر* می‌کند اجرا می‌شود؛ ویرایشِ عادیِ پروندهٔ از
                // قبل فعال دست‌نخورده می‌ماند.
                if (!PassesActivationGate(currentCaseId, oldStatus))
                    return false;

                if (IsCodeExists(txtCode.Text.Trim(), currentCaseId))
                {
                    Msg.Show("کد اختصاصی تکراری است");
                    txtCode.Focus();
                    return false;
                }

                // آموزش — هشدارِ تذکرهٔ تکراری، همان الگویِ مسیرِ ثبتِ رکوردِ جدید.
                string tazkiraAuditNote = null;
                List<string> tazkiraMatches = DuplicateDetector.FindByTazkira(txtHeadTazkiraNo.Text.Trim(), "TblCase", currentCaseId);
                if (tazkiraMatches.Count > 0)
                {
                    if (!UiTheme.ShowConfirm(this,
                        "این شماره تذکره قبلاً برای موارد زیر ثبت شده است:\n\n" +
                        string.Join("\n", tazkiraMatches) +
                        "\n\nآیا مطمئن هستید که می‌خواهید ادامه دهید؟", "احتمال ثبت تکراری"))
                        return false;

                    tazkiraAuditNote = "تذکره " + txtHeadTazkiraNo.Text.Trim() + " => " + string.Join(" | ", tazkiraMatches);
                }

                // الزام نسخهٔ تحویلی (مورد ۱) — همان هشدارِ شباهت در مسیرِ ویرایش.
                // رکوردِ جاری از مقایسه کنار گذاشته می‌شود تا پرونده با خودش
                // مشابه گزارش نشود.
                if (!ConfirmNoSimilarCase(currentCaseId))
                    return false;

                SaveSelectedPhotos();

                using (SQLiteConnection con = db.GetConnection())
                {
                    string query = @"UPDATE TblCase SET
                        Code = @Code,
                        CaseNo = @CaseNo,
                        CaseDate = @CaseDate,
                        Zone = @Zone,
                        Province = @Province,
                        District = @District,
                        Site = @Site,
                        RequestType = @RequestType,
                        RequestTypeID = @RequestTypeID,
                        PriorityLevel = @PriorityLevel,
                        HeadFullName = @HeadFullName,
                        HeadFatherName = @HeadFatherName,
                        HeadSadat = @HeadSadat,
                        Religion = @Religion,
                        HeadTazkiraNo = @HeadTazkiraNo,
                        HeadIdCardType = @HeadIdCardType,
                        HeadOriginalResidence = @HeadOriginalResidence,
                        HeadCurrentResidence = @HeadCurrentResidence,
                        RelationshipToFamily = @RelationshipToFamily,
                        Phone = @Phone,
                        RelativePhone = @RelativePhone,
                        CoveredByOrg = @CoveredByOrg,
                        CoveredByOrgNames = @CoveredByOrgNames,
                        Job = @Job,
                        Skill = @Skill,
                        DisabilityDegree = @DisabilityDegree,
                        DisabilityType = @DisabilityType,
                        MigrationCardType = @MigrationCardType,
                        MaritalStatus = @MaritalStatus,
                        Surveyors = @Surveyors,
                        SurveyDate = @SurveyDate,
                        LocationAddress = @LocationAddress,
                        EducationLevel = @EducationLevel,
                        ServiceStatus = @ServiceStatus,
                        ServiceStatusID = @ServiceStatusID,
                        StopReason = @StopReason,
                        SuspensionReason = @SuspensionReason,
                        UrgentSituation = @UrgentSituation,
                        PhotoPath = @PhotoPath,
                        FamilyPhotoPath = @FamilyPhotoPath,
                        ReferrerName = @ReferrerName,
                        ReferrerPhone = @ReferrerPhone,
                        MainResidenceProvince = @MainResidenceProvince,
                        MainResidenceDistrict = @MainResidenceDistrict,
                        MainResidenceVillage = @MainResidenceVillage,
                        FatherDeathCause = @FatherDeathCause,
                        DisabilityCause = @DisabilityCause,
                        DisabilityDescription = @DisabilityDescription,
                        SpecialNeeds = @SpecialNeeds,
                        DisabilityCardStatus = @DisabilityCardStatus,
                        DisabilityCardNumber = @DisabilityCardNumber,
                        HasMigrationCard = @HasMigrationCard,
                        MigrationCardNumber = @MigrationCardNumber,
                        DepartureDate = @DepartureDate,
                        ArrivalDate = @ArrivalDate,
                        AssistanceDurationMonths = @AssistanceDurationMonths,
                        UpdatedAt = datetime('now'),
                        SuspensionDate = CASE WHEN ServiceStatus = @ServiceStatus THEN SuspensionDate ELSE @SuspensionDate END,
                        SuspendedByUserId = CASE WHEN ServiceStatus = @ServiceStatus THEN SuspendedByUserId ELSE @SuspendedByUserId END,
                        SuspendedByUsername = CASE WHEN ServiceStatus = @ServiceStatus THEN SuspendedByUsername ELSE @SuspendedByUsername END
                    WHERE CasID = @CasID AND (@CID = 0 OR CenterID = @CID)";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                    {
                        AddCaseParameters(cmd, false);
                        AddSuspensionStampParameters(cmd);
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);

                        con.Open();
                        int affectedRows = cmd.ExecuteNonQuery();

                        if (affectedRows == 0)
                        {
                            Msg.Show("رکورد برای ویرایش پیدا نشد یا متعلق به مرکز دیگری است");
                            currentCaseId = 0;
                            LoadCases();
                            SyncMembersTab();
                            return false;
                        }
                    }
                }

                selectedHeadPhotoSource = "";
                selectedFamilyPhotoSource = "";

                AuditLogger.Log("ویرایش", "TblCase", currentCaseId, oldValue, BuildCurrentCaseAuditText());
                if (tazkiraAuditNote != null)
                    AuditLogger.Log("هشدار تذکره تکراری - تأیید کاربر", "TblCase", currentCaseId, "", tazkiraAuditNote);
                AuditLogger.RecordStatusChange(currentCaseId, oldStatus, NormalizeServiceStatus(txtServiceStatus.Text),
                    txtSuspensionReason.Text.Trim(), txtStopReason.Text.Trim());

                // Phase 3 — تایم‌لاینِ مرکزی: ویرایشِ عمومی + تغییرِ نوع درخواست/
                // وضعیت خدمات (فقط وقتی واقعاً عوض شده باشند).
                string newStatusText = NormalizeServiceStatus(txtServiceStatus.Text);
                string newRequestTypeText = txtRequestType.Text.Trim();
                Helpers.TimelineService.LogCaseUpdated(currentCaseId);
                if (!string.Equals(oldStatus, newStatusText, StringComparison.Ordinal))
                    Helpers.TimelineService.LogServiceStatusChanged(currentCaseId, oldStatus, newStatusText);
                if (!string.Equals(oldRequestType, newRequestTypeText, StringComparison.Ordinal))
                    Helpers.TimelineService.LogRequestTypeChanged(currentCaseId, oldRequestType, newRequestTypeText);

                // Phase 5.5-A — ویرایشِ دلیلِ تعلیق *بدونِ* تغییرِ وضعیت.
                // AuditLogger.RecordStatusChange در این حالت زود برمی‌گردد
                // (وضعیت عوض نشده)، پس بدونِ این خط چنین ویرایشی هیچ ردی
                // در تاریخچه نمی‌گذاشت.
                string newSuspensionReason = IsSuspendedStatus(newStatusText)
                    ? txtSuspensionReason.Text.Trim() : "";
                if (string.Equals(oldStatus, newStatusText, StringComparison.Ordinal) &&
                    !string.Equals(oldSuspensionReason ?? "", newSuspensionReason, StringComparison.Ordinal))
                {
                    Helpers.TimelineService.LogSuspensionReasonUpdated(
                        currentCaseId, oldSuspensionReason, newSuspensionReason);
                    AuditLogger.Log("ویرایش دلیل تعلیق", "TblCase", currentCaseId,
                        oldSuspensionReason, newSuspensionReason);
                }

                // Phase 4 — ماژول‌های تخصصی (فقط بخش‌های دیده‌شده).
                SaveCaseModules(currentCaseId);
                // Phase 7 — همان قاعده: فقط وقتی بخش دیده می‌شود.
                SaveCaseRepresentatives(currentCaseId);

                // پیش از فاز ۴ — زیرساختِ کامل‌بودنِ پرونده.
                Helpers.CaseCompletionService.RecalculateAndStore(currentCaseId);
                // Phase 5.5-B — امتیاز پس از کامل‌بودن حساب می‌شود، چون
                // CompletionStatus خودش یکی از حقایقِ امتیازدهی است.
                Helpers.VulnerabilityScoreService.RecalculateAndStore(
                    currentCaseId, Helpers.VulnerabilityScoreService.ReasonCaseSaved);

                // تاریخچهٔ کاملِ رکورد — توضیح در مسیر ثبت (SaveNewCase) آمده است.
                // اگر ویرایش هیچ فیلدی را عوض نکرده باشد، VersionService خودش
                // نسخهٔ تکراری نمی‌سازد.
                CaseManagement.Enterprise.VersionService.Capture("TblCase", currentCaseId,
                    CaseManagement.Enterprise.VersionService.OperationUpdate);

                // صفِ همگام‌سازی — توضیح در مسیر ثبت آمده است.
                CaseManagement.Sync.SyncOutboxService.Capture("TblCase", currentCaseId,
                    CaseManagement.Sync.OfflineSyncInitializer.OperationUpdate);

                Msg.Show("اطلاعات ویرایش شد");
                WarnMissingRequiredDocuments(currentCaseId);
                LoadCases();
                SyncMembersTab();
                // همین ویرایش رویدادهای تازه‌ای در تایم‌لاین ساخته است؛ تب
                // تاریخچه باید بلافاصله آن‌ها را نشان دهد، نه بعد از بازکردنِ
                // دوبارهٔ پرونده.
                RefreshTimelineTab();
                return true;
            }
            catch (SQLiteException ex)
            {
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                    Msg.Show("شماره فرم یا کد اختصاصی تکراری است");
                else
                    Msg.Show("خطا در ویرایش: " + ex.Message);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ویرایش: " + ex.Message);
            }

            return false;
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول رکورد را انتخاب کن");
                return;
            }

            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Delete"))
            {
                Msg.Show("حذف پرونده فقط برای مدیر سیستم مجاز است.");
                return;
            }

            // F-13 — حذف پرونده با ON DELETE CASCADE کلِ ردیف‌های TblAssistance
            // را هم می‌برد؛ یعنی سابقهٔ مالیِ مددجو، از جمله رسیدهایی که با
            // شمارهٔ سریالِ دائمی چاپ و امضا شده‌اند. برگهٔ کاغذی بیرون از
            // سامانه باقی می‌ماند و دیگر هیچ رکوردی پشتش نیست. نسخهٔ
            // VersionService فقط از خودِ TblCase عکس می‌گیرد، نه از فرزندان،
            // پس بازگشت جز از پشتیبان ممکن نیست.
            //
            // قاعده: وجودِ *رسیدِ چاپ‌شده* حذف را می‌بندد (نه صرفِ وجودِ کمک) —
            // چون همان است که سندِ کاغذیِ بیرونی ساخته. کمکِ بدونِ رسید فقط
            // هشدار می‌گیرد و کاربر می‌تواند ادامه دهد.
            if (!ConfirmDeleteAgainstAssistanceHistory(currentCaseId))
                return;

            DeleteMode mode = ShowDeleteModeDialog();
            if (mode == DeleteMode.Cancel)
                return;

            try
            {
                CenterGuard.EnsureCaseAccess(db, currentCaseId);

                string oldValue = GetCaseAuditTextFromDb(currentCaseId);
                // آموزش — حذف دومرحله‌ای: بعد از DELETE دیگر ردیفی نیست تا محتوایش
                // خوانده شود، پس عکسِ فوری «قبل» از حذف گرفته می‌شود؛ ولی نسخهٔ
                // «حذف» فقط «بعد» از حذفِ موفق ثبت می‌گردد تا اگر حذف انجام نشد،
                // نسخهٔ نادرست در تاریخچه نماند.
                string deletedSnapshot = CaseManagement.Enterprise.VersionService
                    .ReadSnapshotText("TblCase", currentCaseId);

                // هویتِ رکورد برای صفِ همگام‌سازی هم پیش از DELETE برداشته می‌شود
                // (بعد از حذف GlobalID خواندنی نیست)؛ ثبتِ نهایی فقط پس از حذفِ
                // موفق — همان الگوی FrmArchive و FrmSettings.
                var pendingDelete =
                    CaseManagement.Sync.SyncOutboxService.PrepareDelete("TblCase", currentCaseId);
                int deletedCaseId = currentCaseId;
                List<string> filePathsToDelete = mode == DeleteMode.AppAndFiles
                    ? CollectCaseFilePaths(currentCaseId)
                    : new List<string>();

                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(
                    "DELETE FROM TblCase WHERE CasID = @CasID AND (@CID = 0 OR CenterID = @CID)", con))
                {
                    AddIntParameter(cmd, "@CasID", currentCaseId);
                    cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);

                    con.Open();
                    int affectedRows = cmd.ExecuteNonQuery();

                    if (affectedRows == 0)
                    {
                        Msg.Show("رکورد برای حذف پیدا نشد یا متعلق به مرکز دیگری است");
                        currentCaseId = 0;
                        LoadCases();
                        SyncMembersTab();
                        SetCaseEditMode(false);
                        return;
                    }
                }

                // TblFamily/TblDocs/TblAssistance rows are removed by ON DELETE CASCADE.
                // CASCADE only touches the database rows; فایل‌های فیزیکی فقط وقتی
                // پاک می‌شوند که کاربر گزینه «حذف کامل» را انتخاب کرده باشد.
                foreach (string path in filePathsToDelete)
                    FileHelper.DeleteFileIfExists(path);

                AuditLogger.Log(
                    mode == DeleteMode.AppAndFiles ? "حذف کامل" : "حذف (فقط نرم‌افزار)",
                    "TblCase", deletedCaseId, oldValue, "");

                CaseManagement.Enterprise.VersionService.CaptureDeleted(
                    "TblCase", deletedCaseId, deletedSnapshot);

                CaseManagement.Sync.SyncOutboxService.CommitDelete(pendingDelete);

                Msg.Show("رکورد حذف شد");
                LoadCases();
                ClearForm();
                SetCaseEditMode(false);   // بعد از حذف، فرم خالی و قفل می‌ماند
            }
            catch (SQLiteException ex)
            {
                Msg.Show("خطا در حذف: " + ex.Message);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در حذف: " + ex.Message);
            }
        }

        // F-13 — دروازهٔ سابقهٔ مالی پیش از حذف پرونده.
        // خروجی true یعنی «ادامه بده»، false یعنی «حذف انجام نشود».
        //
        // دو سطح دارد، چون دو ریسک متفاوت‌اند:
        //   • رسیدِ چاپ‌شده (ReceiptNo دارد)  ⇒ حذف مسدود. سندِ کاغذیِ
        //     شماره‌دار بیرون از سامانه وجود دارد و بی‌پشتوانه می‌شود.
        //   • کمکِ بدونِ رسید                  ⇒ فقط تأیید صریح کاربر.
        //
        // شکستِ کوئری عمداً حذف را متوقف می‌کند (fail-closed): اگر نتوانیم
        // ثابت کنیم رسیدی نیست، حق نداریم سابقهٔ مالی را نابود کنیم.
        private bool ConfirmDeleteAgainstAssistanceHistory(int casId)
        {
            int assistanceCount = 0;
            int printedReceiptCount = 0;

            try
            {
                using (var con = db.GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT COUNT(*), SUM(CASE WHEN ReceiptNo IS NOT NULL THEN 1 ELSE 0 END) " +
                    "FROM TblAssistance WHERE CasID = @CasID", con))
                {
                    AddIntParameter(cmd, "@CasID", casId);
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            assistanceCount = dr.IsDBNull(0) ? 0 : Convert.ToInt32(dr.GetValue(0));
                            printedReceiptCount = dr.IsDBNull(1) ? 0 : Convert.ToInt32(dr.GetValue(1));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Msg.Show("بررسی سابقهٔ مالی این پرونده ممکن نشد، پس حذف انجام نمی‌شود: " + ex.Message);
                return false;
            }

            if (printedReceiptCount > 0)
            {
                Msg.Show(
                    "این پرونده " + printedReceiptCount + " برگه دریافت مساعدتِ چاپ‌شده دارد و قابل حذف نیست." +
                    Environment.NewLine + Environment.NewLine +
                    "برگه‌های چاپ‌شده شمارهٔ سریالِ دائمی دارند و بیرون از سامانه در دست مددجو یا در بایگانی‌اند؛ " +
                    "حذف پرونده آن‌ها را بی‌پشتوانه می‌کند و سابقهٔ مالی بازگشت‌ناپذیر از بین می‌رود." +
                    Environment.NewLine + Environment.NewLine +
                    "به‌جای حذف، وضعیت خدمات را روی «قطع» بگذارید یا پرونده را بایگانی کنید.");
                return false;
            }

            if (assistanceCount > 0)
            {
                return UiTheme.ShowConfirm(this,
                    "این پرونده " + assistanceCount + " رکورد کمک مالی دارد که با حذف پرونده برای همیشه پاک می‌شود " +
                    "(هنوز هیچ برگه‌ای برایشان چاپ نشده)." +
                    Environment.NewLine + Environment.NewLine +
                    "این رکوردها در نسخهٔ پشتیبان قابل بازیابی‌اند، ولی از خودِ برنامه نه." +
                    Environment.NewLine + Environment.NewLine +
                    "آیا حذف پرونده ادامه یابد؟",
                    "حذف پرونده دارای سابقهٔ مالی");
            }

            return true;
        }

        // ─── تاریخچهٔ تغییراتِ همین پرونده ──────────────────────────────────
        // آموزش: FrmVersions از قبل سازندهٔ (entityName, entityId) را داشت ولی
        // هیچ فرمی از آن استفاده نمی‌کرد — فقط حالتِ «همهٔ رکوردها» از داشبورد
        // باز می‌شد. این دکمه همان پنجره را روی پروندهٔ جاری فیلتر می‌کند.
        private void btnHistory_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول رکورد را انتخاب کن");
                return;
            }

            using (var frm = new CaseManagement.Enterprise.FrmVersions("TblCase", currentCaseId))
                frm.ShowDialog(this);
        }

        private enum DeleteMode { Cancel, AppOnly, AppAndFiles }

        // دیالوگ انتخاب نوع حذف: فقط از نرم‌افزار، یا نرم‌افزار + فایل‌های فیزیکی
        private DeleteMode ShowDeleteModeDialog()
        {
            using (Form form = new Form())
            {
                form.Text = "نوع حذف پرونده";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(430, 210);
                form.RightToLeft = RightToLeft.Yes;
                form.RightToLeftLayout = true;
                form.Font = new Font("Segoe UI", 9.75F);

                Label lbl = new Label();
                lbl.Text = "این پرونده چگونه حذف شود؟";
                lbl.AutoSize = false;
                lbl.SetBounds(20, 20, 390, 40);
                lbl.TextAlign = ContentAlignment.MiddleRight;
                form.Controls.Add(lbl);

                Button btnAppOnly = new Button();
                btnAppOnly.Text = "حذف فقط از نرم‌افزار (فایل‌ها باقی می‌مانند)";
                btnAppOnly.SetBounds(30, 70, 370, 38);
                btnAppOnly.DialogResult = DialogResult.No;
                form.Controls.Add(btnAppOnly);

                Button btnAppAndFiles = new Button();
                btnAppAndFiles.Text = "حذف از نرم‌افزار و حذف فایل‌های فیزیکی";
                btnAppAndFiles.SetBounds(30, 116, 370, 38);
                btnAppAndFiles.DialogResult = DialogResult.Yes;
                form.Controls.Add(btnAppAndFiles);

                Button btnCancel = new Button();
                btnCancel.Text = "انصراف";
                btnCancel.SetBounds(150, 164, 130, 32);
                btnCancel.DialogResult = DialogResult.Cancel;
                form.Controls.Add(btnCancel);

                form.CancelButton = btnCancel;

                DialogResult result = form.ShowDialog(this);

                if (result == DialogResult.Yes)
                    return DeleteMode.AppAndFiles;
                if (result == DialogResult.No)
                    return DeleteMode.AppOnly;
                return DeleteMode.Cancel;
            }
        }

        private List<string> CollectCaseFilePaths(int caseId)
        {
            List<string> paths = new List<string>();

            using (var con = db.GetConnection())
            {
                con.Open();

                using (var cmd = new SQLiteCommand("SELECT PhotoPath, FamilyPhotoPath FROM TblCase WHERE CasID = @CasID", con))
                {
                    AddIntParameter(cmd, "@CasID", caseId);

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            AddIfNotEmpty(paths, GetDbString(dr, "PhotoPath"));
                            AddIfNotEmpty(paths, GetDbString(dr, "FamilyPhotoPath"));
                        }
                    }
                }

                using (var cmd = new SQLiteCommand("SELECT MemberPhotoPath FROM TblFamily WHERE CasID = @CasID", con))
                {
                    AddIntParameter(cmd, "@CasID", caseId);

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                            AddIfNotEmpty(paths, GetDbString(dr, "MemberPhotoPath"));
                    }
                }

                using (var cmd = new SQLiteCommand("SELECT DocFilePath FROM TblDocs WHERE CasID = @CasID", con))
                {
                    AddIntParameter(cmd, "@CasID", caseId);

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                            AddIfNotEmpty(paths, GetDbString(dr, "DocFilePath"));
                    }
                }
            }

            return paths;
        }

        private static void AddIfNotEmpty(List<string> list, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                list.Add(value);
        }

        private string BuildCurrentCaseAuditText()
        {
            return
                "FormNo=" + txtFormNo.Text.Trim() +
                "; Code=" + txtCode.Text.Trim() +
                "; HeadFullName=" + txtHeadFullName.Text.Trim() +
                "; Phone=" + txtPhone.Text.Trim() +
                "; Province=" + txtProvince.Text.Trim() +
                "; District=" + txtDistrict.Text.Trim() +
                "; ServiceStatus=" + NormalizeServiceStatus(txtServiceStatus.Text);
        }

        private string GetCaseAuditTextFromDb(int caseId)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT FormNo, Code, HeadFullName, Phone, Province, District, ServiceStatus
FROM TblCase
WHERE CasID = @CasID", con))
            {
                AddIntParameter(cmd, "@CasID", caseId);
                con.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read())
                        return "";

                    return
                        "FormNo=" + GetDbString(dr, "FormNo") +
                        "; Code=" + GetDbString(dr, "Code") +
                        "; HeadFullName=" + GetDbString(dr, "HeadFullName") +
                        "; Phone=" + GetDbString(dr, "Phone") +
                        "; Province=" + GetDbString(dr, "Province") +
                        "; District=" + GetDbString(dr, "District") +
                        "; ServiceStatus=" + GetDbString(dr, "ServiceStatus");
                }
            }
        }

        private string GetCaseStatusById(int caseId)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("SELECT ServiceStatus FROM TblCase WHERE CasID = @CasID", con))
            {
                AddIntParameter(cmd, "@CasID", caseId);
                con.Open();

                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? "" : result.ToString();
            }
        }

        // Phase 5.5-A — دلیلِ تعلیقِ ذخیره‌شده، برای تشخیصِ «ویرایشِ دلیل بدونِ
        // تغییرِ وضعیت» (که RecordStatusChange زود برمی‌گردد و ثبتش نمی‌کند).
        private string GetCaseSuspensionReasonById(int caseId)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("SELECT SuspensionReason FROM TblCase WHERE CasID = @CasID", con))
            {
                AddIntParameter(cmd, "@CasID", caseId);
                con.Open();
                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? "" : result.ToString();
            }
        }

        // Phase 5.5-A — دروازهٔ مرحله‌ایِ فعال‌سازی.
        // خروجی false ⇒ ذخیره متوقف شود.
        private bool PassesActivationGate(int casId, string oldStatusName)
        {
            // Phase 7 — پرچم از نوعِ *انتخاب‌شدهٔ فرم* می‌آید، نه از
            // دیتابیس: دروازه پیش از UPDATE اجرا می‌شود، پس اگر کاربر
            // همزمان نوع را به «معلول» عوض کرده باشد، دیتابیس هنوز
            // مقدارِ کهنه را دارد.
            bool requiresRepresentative = Helpers.ReferenceDataService
                .GetRequestTypeSectionsByName(txtRequestType.Text.Trim()).ShowRepresentativeSection;

            var gate = Helpers.CaseActivationValidator.Evaluate(
                casId, oldStatusName, NormalizeServiceStatus(txtServiceStatus.Text),
                requiresRepresentative);

            // کاربری که نماینده را در همین نشست تایپ کرده ولی هنوز
            // ذخیره نشده، نباید پیامِ «نماینده ثبت نشده» ببیند —
            // SaveCaseRepresentatives پس از همین دروازه اجرا می‌شود، پس
            // در لحظهٔ ارزیابی هنوز ردیفی در دیتابیس نیست.
            if (gate.MissingRepresentative && requiresRepresentative &&
                !BuildRepresentative(Helpers.CaseRepresentativeService.OrderPrimary).IsEmpty)
            {
                gate.MissingRepresentative = false;
                if (!gate.HasAnyFinding)
                    gate.Outcome = Helpers.ActivationGateOutcome.Allowed;
            }

            if (gate.IsBlocked)
            {
                Msg.Show(gate.BuildMessage(
                    "برای فعال‌کردن پرونده، موارد الزامی باید کامل باشند. موارد زیر باقی است:"));
                return false;
            }

            if (gate.HasWarning)
            {
                // «در انتظار تایید» فقط هشدار می‌دهد — کاربر می‌تواند ادامه دهد.
                return UiTheme.ShowConfirm(this,
                    gate.BuildMessage("این پرونده مواردِ الزامیِ ناتمام دارد:") +
                    "\n\nآیا با این وجود ذخیره شود؟",
                    "موارد الزامی ناتمام");
            }

            return true;
        }

        // Phase 3 — برای تشخیصِ تغییرِ نوع درخواست هنگام ویرایش (تایم‌لاین).
        private string GetCaseRequestTypeById(int caseId)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand("SELECT RequestType FROM TblCase WHERE CasID = @CasID", con))
            {
                AddIntParameter(cmd, "@CasID", caseId);
                con.Open();

                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? "" : result.ToString();
            }
        }

        private string GetDbString(System.Data.IDataReader dr, string columnName)
        {
            object value = dr[columnName];
            return value == DBNull.Value ? "" : value.ToString();
        }

        private void SetDatePickerValue(Helpers.PersianDatePicker picker, object value)
        {
            // آموزش — پارس با InvariantCulture (نه Convert.ToDateTime که از
            // کالچر شمسی ترد استفاده می‌کند و سال میلادی ذخیره‌شده را اشتباه
            // تفسیر می‌کرد). جزئیات در PersianDateHelper.ParseStoredDate.
            //
            // NULL/خالیِ دیتابیس باید به حالتِ «تیک‌نخورده» برگردد، نه به
            // «امروز». قبلاً هر دو حالت یکسان بارگذاری می‌شدند و کاربر
            // نمی‌توانست تشخیص دهد تاریخی ثبت شده یا نه — و ذخیرهٔ بعدی همان
            // «امروز» را دائمی می‌کرد. برای کنترل‌هایی که ShowCheckBox ندارند
            // رفتار دقیقاً مثل قبل است (Checked همیشه true برمی‌گردد).
            bool hasValue = value != null && value != DBNull.Value
                            && !string.IsNullOrWhiteSpace(value.ToString());

            if (!hasValue)
            {
                picker.Checked = false;
                picker.Value = DateTime.Today;
                return;
            }

            picker.Value = Helpers.PersianDateHelper.ParseStoredDate(value, DateTime.Today);
            picker.Checked = true;
        }

        private void LoadCaseFromReader(System.Data.IDataReader dr)
        {
            currentCaseId = Convert.ToInt32(dr["CasID"]);

            txtFormNo.Text = GetDbString(dr, "FormNo");
            txtCode.Text = GetDbString(dr, "Code");
            txtCaseNo.Text = GetDbString(dr, "CaseNo");
            txtZone.Text = GetDbString(dr, "Zone");
            txtProvince.Text = GetDbString(dr, "Province");
            txtDistrict.Items.Clear();
            txtDistrict.Items.AddRange(Helpers.AfghanGeoData.GetDistricts(txtProvince.Text));
            txtDistrict.Text = GetDbString(dr, "District");
            txtSite.Text = GetDbString(dr, "Site");
            txtRequestType.Text = GetDbString(dr, "RequestType");
            txtPriorityLevel.Text = GetDbString(dr, "PriorityLevel");
            txtHeadFullName.Text = GetDbString(dr, "HeadFullName");
            txtHeadFatherName.Text = GetDbString(dr, "HeadFatherName");
            txtHeadSadat.Text = GetDbString(dr, "HeadSadat");
            txtReligion.Text = GetDbString(dr, "Religion");
            // ترتیب مهم است: اول نوع تذکره، بعد شماره — چون IdCardHelper.Attach
            // با تغییر نوع، شماره را دوباره قالب‌بندی می‌کند (همان قاعده‌ی FrmFamily).
            cmbHeadIdCardType.Text = GetDbString(dr, "HeadIdCardType");
            txtHeadTazkiraNo.Text = GetDbString(dr, "HeadTazkiraNo");
            txtHeadOriginalResidence.Text = GetDbString(dr, "HeadOriginalResidence");
            txtHeadCurrentResidence.Text = GetDbString(dr, "HeadCurrentResidence");
            txtRelationshipToFamily.Text = GetDbString(dr, "RelationshipToFamily");
            txtPhone.Text = GetDbString(dr, "Phone");
            txtRelativePhone.Text = GetDbString(dr, "RelativePhone");
            txtCoveredByOrg.Text = GetDbString(dr, "CoveredByOrg");
            txtCoveredByOrgNames.Text = GetDbString(dr, "CoveredByOrgNames");
            // بعد از پرشدن هر دو فیلد، وضعیت نمایشِ کادر اسامی را هماهنگ کن.
            // (باید بعد از مقداردهی باشد، وگرنه متنِ تازه‌خوانده‌شده پاک می‌شود.)
            UpdateCoveredByOrgNamesVisibility();
            txtJob.Text = GetDbString(dr, "Job");
            txtSkill.Text = GetDbString(dr, "Skill");
            txtDisabilityDegree.Text = GetDbString(dr, "DisabilityDegree");
            txtDisabilityType.Text = GetDbString(dr, "DisabilityType");
            // آموزش — وضعیت «سالم/معلول» ستون جدا در دیتابیس ندارد؛ از پرشدن
            // نوع معلولیت استنتاج می‌شود: خالی = سالم (تیک‌خورده)، پرشده = معلول.
            chkHeadHealthy.CheckedChanged -= ChkHeadHealthy_CheckedChanged;
            chkHeadHealthy.Checked = string.IsNullOrWhiteSpace(txtDisabilityType.Text);
            chkHeadHealthy.CheckedChanged += ChkHeadHealthy_CheckedChanged;
            txtDisabilityType.Enabled = !chkHeadHealthy.Checked;
            txtDisabilityDegree.Enabled = !chkHeadHealthy.Checked;
            txtMigrationCardType.Text = GetDbString(dr, "MigrationCardType");
            txtMaritalStatus.Text = GetDbString(dr, "MaritalStatus");
            txtSurveyors.Text = GetDbString(dr, "Surveyors");
            txtLocationAddress.Text = GetDbString(dr, "LocationAddress");
            txtEducationLevel.Text = GetDbString(dr, "EducationLevel");
            SetComboBoxText(txtServiceStatus, GetDbString(dr, "ServiceStatus"));
            txtStopReason.Text = GetDbString(dr, "StopReason");
            string savedSuspensionReason = GetDbString(dr, "SuspensionReason");
            int suspReasonIdx = txtSuspensionReason.FindStringExact(savedSuspensionReason);
            txtSuspensionReason.SelectedIndex = suspReasonIdx;
            UpdateStopReasonVisibility();
            txtUrgentSituation.Text = GetDbString(dr, "UrgentSituation");
            txtPhotoPath.Text = GetDbString(dr, "PhotoPath");
            txtFamilyPhotoPath.Text = GetDbString(dr, "FamilyPhotoPath");
            txtReferrerName.Text = GetDbString(dr, "ReferrerName");
            txtReferrerPhone.Text = GetDbString(dr, "ReferrerPhone");

            // Phase 3 (بازبینی) — بخش‌های اختصاصیِ نوع درخواست.
            txtMainResidenceProvince.Text = GetDbString(dr, "MainResidenceProvince");
            txtMainResidenceDistrict.Text = GetDbString(dr, "MainResidenceDistrict");
            txtMainResidenceVillage.Text = GetDbString(dr, "MainResidenceVillage");
            SetComboBoxText(txtFatherDeathCause, GetDbString(dr, "FatherDeathCause"));

            SetComboBoxText(txtDisabilityCause, GetDbString(dr, "DisabilityCause"));
            txtDisabilityDescription.Text = GetDbString(dr, "DisabilityDescription");
            txtSpecialNeeds.Text = GetDbString(dr, "SpecialNeeds");
            SetComboBoxText(txtDisabilityCardStatus, GetDbString(dr, "DisabilityCardStatus"));
            txtDisabilityCardNumber.Text = GetDbString(dr, "DisabilityCardNumber");

            SetComboBoxText(txtHasMigrationCard, GetDbString(dr, "HasMigrationCard"));
            txtMigrationCardNumber.Text = GetDbString(dr, "MigrationCardNumber");
            SetDatePickerValue(dtpDepartureDate, dr["DepartureDate"]);
            SetDatePickerValue(dtpArrivalDate, dr["ArrivalDate"]);
            object durationValue = dr["AssistanceDurationMonths"];
            txtAssistanceDurationMonths.Text = durationValue == DBNull.Value ? "" : durationValue.ToString();

            // Phase 4 — ماژول‌های تخصصی بعد از ستون‌های TblCase بارگذاری
            // می‌شوند، چون جدولِ ماژول مرجعِ منطقِ تازه است و باید حرفِ آخر را
            // بزند اگر آینه و ماژول (به هر دلیل) واگرا شده باشند.
            LoadCaseModules(currentCaseId);
            LoadCaseRepresentatives(currentCaseId);
            // هر پروندهٔ تازه‌بازشده یک بار حقِّ هشدار دارد.
            _legacyRepWarningShown = false;
            // Phase 5 — سوابق بازدیدِ همین پرونده.
            RefreshVisitsTab();
            // Phase 6 — وضعیتِ خانوارِ همین پرونده.
            RefreshFamilyTab();
            // Phase 5.5-B — امتیاز آسیب‌پذیریِ ذخیره‌شده.
            RefreshVulnerabilityTab();
            // Phase 5.5-C — تأمین مالی و کارت‌های وضعیتِ محاسبه‌شده.
            RefreshFundingTab();
            RefreshCaseStatusStats();
            // تاریخچهٔ کاملِ همین پرونده (آخرین تب).
            RefreshTimelineTab();

            UpdateRequestTypeSectionVisibility();

            SetDatePickerValue(dtpCaseDate, dr["CaseDate"]);
            SetDatePickerValue(dtpSurveyDate, dr["SurveyDate"]);

            savedHeadPhotoPath = txtPhotoPath.Text.Trim();
            savedFamilyPhotoPath = txtFamilyPhotoPath.Text.Trim();

            if (savedHeadPhotoPath != "")
                LoadImageToPictureBox(savedHeadPhotoPath, picPhoto);
            else
                ClearPictureBox(picPhoto);

            if (savedFamilyPhotoPath != "")
                LoadImageToPictureBox(savedFamilyPhotoPath, picFamilyPhoto);
            else
                ClearPictureBox(picFamilyPhoto);

            selectedHeadPhotoSource = "";
            selectedFamilyPhotoSource = "";

            txtFormNo.ReadOnly = true;
            txtFormNo.TabStop = false;
            txtCode.Enabled = false;

            SyncMembersTab();

            // پروندهٔ بارگذاری‌شده همیشه در حالتِ نمایش باز می‌شود؛ برای تغییر
            // باید کاربر صریحاً «ویرایش» را بزند.
            SetCaseEditMode(false);
        }

        // آموزش — رفع باگ امنیتی چندمرکزی: تمام جستجوهای پرونده (با CasID/
        // FormNo/Code) از این یک متد عبور می‌کنند، پس فیلتر @CID اینجا یک‌بار
        // اضافه می‌شود و هیچ مسیر جستجوی فعلی یا آینده نمی‌تواند آن را فراموش
        // کند. قبلاً این جستجوها بدون فیلتر مرکز بودند و یک کاربر مرکز دیگر
        // می‌توانست با حدس کد/شماره فرم، پرونده مراکز دیگر را ببیند/ویرایش کند.
        private bool LoadCaseByQuery(string query, Action<SQLiteCommand> addParameters)
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(query, con))
            {
                if (addParameters != null)
                    addParameters(cmd);

                cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);

                con.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read())
                        return false;

                    LoadCaseFromReader(dr);
                    GoToSummaryTab();
                    return true;
                }
            }
        }

        // ─── با انتخابِ هر پرونده، «خلاصه پرونده» جلو می‌آید ─────────────────
        // آموزش — پیشنهادِ کاربر: کاربر پس از انتخابِ پرونده اول می‌خواهد
        // *ببیند* نه اینکه ویرایش کند، پس تبِ خلاصه نقطهٔ فرودِ درستی است.
        // اگر همان لحظه در حالِ ویرایش باشد (فرم قفل نیست)، تب عوض نمی‌شود —
        // وگرنه کاربرِ در حالِ تایپ از روی فیلدهایش پرت می‌شد.
        private void GoToSummaryTab()
        {
            if (tabsCase == null || tabsCase.TabPages.Count == 0) return;
            if (_caseEditMode) return;

            try { tabsCase.SelectedTab = tabsCase.TabPages[0]; }
            catch { }
        }

        private bool LoadCaseById(int caseId)
        {
            return LoadCaseByQuery(
                "SELECT * FROM TblCase WHERE CasID = @CasID AND (@CID = 0 OR CenterID = @CID) LIMIT 1",
                cmd => AddIntParameter(cmd, "@CasID", caseId));
        }

        private bool LoadCaseByCode(string code)
        {
            return LoadCaseByQuery(
                "SELECT * FROM TblCase WHERE Code = @Value AND (@CID = 0 OR CenterID = @CID) LIMIT 1",
                cmd => AddStringParameter(cmd, "@Value", code));
        }

        // آموزش — شماره فرم دیگر قابل تایپ نیست (همیشه اتومات/یکتا/قفل)، پس
        // جستجوی سریع این فرم فقط بر اساس کد اختصاصی است؛ جستجو با شماره فرم
        // از طریق «جستجوی پیشرفته» (که فیلد اختصاصی «شماره فرم» دارد) انجام می‌شود.
        private void btnSearch_Click(object sender, EventArgs e)
        {
            string searchedCode = txtCode.Text.Trim();

            if (searchedCode == "")
            {
                Msg.Show("کد اختصاصی را وارد کنید");
                txtCode.Focus();
                return;
            }

            try
            {
                if (LoadCaseByCode(searchedCode))
                {
                    Msg.Show("رکورد پیدا شد");
                }
                else
                {
                    ClearForm();
                    txtCode.Text = searchedCode;
                    Msg.Show("رکورد پیدا نشد");
                }
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در جستجو: " + ex.Message);
            }
        }

        // رسمِ شماره‌ی ردیف (۱-پایه) در ناحیه‌ی RowHeader — الگوی استانداردِ
        // WinForms برای شماره‌گذاری، بدون افزودنِ ستونِ دیتاییِ اضافه.
        //
        // آموزش — رفعِ باگِ واقعی (با آزمونِ واقعیِ مقایسه‌ی مختصات تأیید شد):
        // e.RowBounds ناحیه‌ی سلول‌های داده را می‌دهد، *بدونِ* هدر — و چون این
        // فرم RightToLeft=Yes دارد، هدرِ ردیف فیزیکاً سمتِ *راستِ* همان ناحیه
        // می‌نشیند، نه چپش. نسخه‌ی قبلی از e.RowBounds.Left استفاده می‌کرد که
        // دقیقاً روی ستونِ اولِ داده (ستونِ عکس) می‌افتاد — یعنی شماره‌ی ردیف
        // با خودِ عکس همپوشانی داشت. با آزمون تأیید شد: مستطیلِ چپ‌مبنا با
        // ستونِ اول همپوشانی دارد، مستطیلِ راست‌مبنا ندارد.
        private void DgvCases_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            string rowNumber = (e.RowIndex + 1).ToString();
            SizeF size = e.Graphics.MeasureString(rowNumber, dgvCases.RowHeadersDefaultCellStyle.Font ?? dgvCases.Font);

            int headerLeft = dgvCases.RightToLeft == RightToLeft.Yes
                ? e.RowBounds.Right
                : e.RowBounds.Left - dgvCases.RowHeadersWidth;
            Rectangle headerBounds = new Rectangle(headerLeft, e.RowBounds.Top, dgvCases.RowHeadersWidth, e.RowBounds.Height);

            e.Graphics.DrawString(rowNumber, dgvCases.Font, SystemBrushes.ControlText,
                headerBounds.Left + (headerBounds.Width - size.Width) / 2,
                headerBounds.Top + (headerBounds.Height - size.Height) / 2);
        }

        private void dgvCases_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvCases.Rows.Count)
                return;

            DataGridViewRow row = dgvCases.Rows[e.RowIndex];

            if (row.IsNewRow || !dgvCases.Columns.Contains("CasID"))
                return;

            object value = row.Cells["CasID"].Value;

            if (value == null || value == DBNull.Value)
                return;

            int caseId;

            if (!int.TryParse(value.ToString(), out caseId))
                return;

            try
            {
                if (!LoadCaseById(caseId))
                    Msg.Show("رکورد پیدا نشد");
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در انتخاب رکورد: " + ex.Message);
            }
        }

        // آموزش — فاز ۱: قبلاً اینجا FrmFamily را با ShowDialog به‌صورت
        // پنجرهٔ مجزا باز می‌کرد. حالا اعضاء یک تب داخل همین فرم است، پس این
        // دکمه فقط کاربر را به آن تب می‌برد؛ ساخت/رفرشِ واقعیِ فرم embedded
        // در EnsureFamilyEmbedded (فراخوانی‌شده از tabsCase_SelectedIndexChanged) انجام می‌شود.
        private void btnFamily_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا جستجو کن");
                return;
            }

            tabsCase.SelectedTab = (TabPage)tabMembersHost.Parent;
        }

        // تک نقطهٔ هماهنگ‌سازی تب «اعضاء خانواده» با currentCaseId. هرجا
        // currentCaseId تغییر می‌کند (بارگذاری/ذخیره/ویرایش/حذف/پاک‌شدن فرم)
        // همین‌جا صدا زده می‌شود. اگر نمونهٔ embedded هنوز ساخته نشده (کاربر
        // هنوز روی تب نرفته)، کاری نمی‌کند — ساختش به EnsureFamilyEmbedded
        // واگذار شده تا هزینه‌اش فقط وقتی پرداخت شود که واقعاً لازم است.
        private void SyncMembersTab()
        {
            if (lblMembersPlaceholder == null)
                return; // InitializeComponent هنوز کامل اجرا نشده

            // آموزش — همین نقطه‌ی هماهنگ‌سازی که فاز ۱ برای تب اعضاء ساخت،
            // دقیقاً همان لحظه‌ای است که تب «خلاصه پرونده» هم باید به‌روز شود
            // (بارگذاری/ذخیره/ویرایش/حذف/پاک‌شدن فرم). یک نقطه‌ی صدازنی،
            // به‌جای پخش‌کردن این متد در چند جای FrmCase.cs.
            UpdateCaseSummaryTab();

            if (currentCaseId == 0)
            {
                if (_embeddedFamily != null)
                    _embeddedFamily.Visible = false;
                lblMembersPlaceholder.Visible = true;
                _familyDirty = false;

                if (_embeddedDocs != null)
                    _embeddedDocs.Visible = false;
                lblDocsPlaceholder.Visible = true;
                _docsDirty = false;
                return;
            }

            if (_embeddedFamily != null)
            {
                if (tabsCase.SelectedTab == tabMembersHost.Parent)
                {
                    // تب همین الان دیده می‌شود → رفرش فوری لازم است.
                    _embeddedFamily.RefreshForCase(currentCaseId, txtCode.Text.Trim());
                    _embeddedFamily.Visible = true;
                    lblMembersPlaceholder.Visible = false;
                    _familyDirty = false;
                }
                else
                {
                    // تب دیده نمی‌شود؛ کوئری نمی‌زنیم — فقط علامت می‌زنیم تا
                    // EnsureFamilyEmbedded یک‌بار، دقیقاً وقتی کاربر به تب برود، رفرش کند.
                    _familyDirty = true;
                }
            }

            // آموزش — فاز A4: عیناً همان منطقِ بالا برای تب اعضاء، برای تب اسناد.
            if (_embeddedDocs != null)
            {
                if (tabsCase.SelectedTab == tabDocsHost.Parent)
                {
                    _embeddedDocs.RefreshForCase(currentCaseId, txtCode.Text.Trim());
                    _embeddedDocs.Visible = true;
                    lblDocsPlaceholder.Visible = false;
                    _docsDirty = false;
                }
                else
                {
                    _docsDirty = true;
                }
            }
        }

        // ═══ حالتِ نمایش/ویرایش (فقط برای فیلدهای خودِ پرونده) ═════════════
        // آموزش — فرم به‌صورت پیش‌فرض «فقط نمایش» است و با دکمه‌ی «ویرایش»
        // (یا «جدید») باز می‌شود. عمداً فقط سه کارتِ فیلدهای پرونده
        // (grpHead/grpPhysical/grpCase) پیمایش می‌شوند، نه کلِ فرم — چون:
        //   • تب‌های «اعضاء» و «اسناد» فرم‌های embeddedِ مستقل‌اند و دکمه‌های
        //     جدید/ذخیره/ویرایشِ خودشان را دارند؛ قفل‌کردنشان از این‌جا
        //     کارکردشان را می‌شکست.
        //   • تب «خلاصه پرونده» از قبل و همیشه خواندنی است.
        //   • نوار جستجوی سریع باید همیشه فعال بماند (وگرنه کاربر در حالتِ
        //     قفل نمی‌توانست پروندهٔ بعدی را پیدا کند).
        private bool _caseEditMode = false;

        // ─── ویژگی ۵ (فعال‌سازی) — قفل رکورد برای جلوگیری از ویرایش هم‌زمان ───
        private int _caseLockId = 0;
        private System.Windows.Forms.Timer _caseLockHeartbeat;

        // خروجی: false یعنی رکورد توسط کاربر دیگری قفل است و حالتِ ویرایش
        // باز نشد؛ فراخوان (btnEdit_Click) باید متوقف شود.
        private bool SetCaseEditMode(bool editable)
        {
            if (editable)
            {
                // پروندهٔ تازه (currentCaseId == 0) هنوز چیزی برای قفل کردن
                // ندارد؛ TryAcquire خودش این حالت را بدونِ قفلِ واقعی موفق
                // برمی‌گرداند.
                CaseManagement.Enterprise.LockResult lockResult =
                    CaseManagement.Enterprise.LockService.TryAcquire("TblCase", currentCaseId);

                if (!lockResult.Acquired)
                {
                    Msg.Show(lockResult.DeniedMessage);
                    return false;
                }

                _caseLockId = lockResult.LockID;
                StartCaseLockHeartbeat();
            }
            else if (_caseLockId > 0)
            {
                CaseManagement.Enterprise.LockService.Release(_caseLockId);
                _caseLockId = 0;
                StopCaseLockHeartbeat();
            }

            _caseEditMode = editable;

            ApplyReadOnlyToFieldBoxes(grpHead, !editable);
            ApplyReadOnlyToFieldBoxes(grpPhysical, !editable);
            ApplyReadOnlyToFieldBoxes(grpCase, !editable);

            // چک‌باکسِ «سالم است» داخل FieldBox نیست، پس جدا کنترل می‌شود.
            chkHeadHealthy.Enabled = editable;

            // انتخابِ عکس هم بخشی از ویرایش است.
            btnBrowsePhoto.Enabled = editable;
            btnBrowseFamilyPhoto.Enabled = editable;
            btnClearPhoto.Enabled = editable;
            btnClearFamilyPhoto.Enabled = editable;

            btnSave.Enabled = editable;

            if (editable)
            {
                // آموزش — این سه متد وضعیتِ فعال/غیرفعالِ فیلدهای وابسته را
                // تعیین می‌کنند (معلولیت، دلیل تعلیق، اسامی مؤسسات). چون
                // پیمایشِ بالا همه‌ی فیلدها را یکجا باز کرد، باید دوباره اعمال
                // شوند وگرنه فیلدی که باید بسته بماند (مثل نوع معلولیتِ فردِ
                // سالم) باز می‌شود.
                UpdateHeadPhysicalState();
                UpdateStopReasonVisibility();
                UpdateCoveredByOrgNamesVisibility();
            }

            // شماره فرم همیشه اتومات/قفل است — مستقل از حالتِ ویرایش.
            txtFormNo.ReadOnly = true;
            txtFormNo.TabStop = false;

            return true;
        }

        // تمدیدِ دوره‌ای قفل تا وقتی فرم در حالتِ ویرایش باز است — وگرنه یک
        // ویرایشِ طولانی بعد از ۱۵ دقیقه (پیش‌فرض LockMinutes) بی‌صدا منقضی
        // می‌شد و کاربر دیگری می‌توانست هم‌زمان قفل بگیرد.
        private void StartCaseLockHeartbeat()
        {
            if (_caseLockHeartbeat == null)
            {
                _caseLockHeartbeat = new System.Windows.Forms.Timer();
                _caseLockHeartbeat.Interval = 5 * 60 * 1000; // ۵ دقیقه
                _caseLockHeartbeat.Tick += delegate
                {
                    CaseManagement.Enterprise.LockService.Heartbeat(_caseLockId);
                };
            }

            _caseLockHeartbeat.Start();
        }

        private void StopCaseLockHeartbeat()
        {
            if (_caseLockHeartbeat != null)
                _caseLockHeartbeat.Stop();
        }

        // آموزش — دفاعِ آخر: اگر کاربر با دکمهٔ × پنجره را ببندد (نه از مسیرِ
        // معمولِ SetCaseEditMode(false))، قفل باید همین‌جا آزاد شود. اگر این
        // مرحله هم انجام نشود، ExpiresAt/PurgeExpired موجود در LockService
        // بازیابیِ خودکار در برابرِ خرابی/بستنِ ناگهانی را تضمین می‌کند.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_caseLockId > 0)
            {
                CaseManagement.Enterprise.LockService.Release(_caseLockId);
                _caseLockId = 0;
            }

            StopCaseLockHeartbeat();

            base.OnFormClosing(e);
        }

        private static void ApplyReadOnlyToFieldBoxes(Control parent, bool readOnly)
        {
            if (parent == null)
                return;

            foreach (Control child in parent.Controls)
            {
                FieldBox box = child as FieldBox;
                if (box != null)
                {
                    box.SetReadOnly(readOnly);
                    continue;   // داخلِ FieldBox را نگرد؛ خودش هندل کرد
                }

                if (child.HasChildren)
                    ApplyReadOnlyToFieldBoxes(child, readOnly);
            }
        }

        private static string TextOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        // آموزش — فاز A3 (تب خلاصه پرونده): پنج فیلدِ اول و عکس عیناً مثلِ
        // UpdateCaseHeader از همان کنترل‌های موجود کپی می‌شوند — بدون کوئری.
        // سه مقدارِ باقی‌مانده (تعداد اعضاء/آخرین کمک/آخرین تغییر) در هیچ‌جای
        // دیگر این فرم موجود نبودند، پس با سه کوئری سبک (COUNT/۱ردیف/MAX) روی
        // جدول‌های موجود (TblFamily/TblAssistance/TblAuditLog) پر می‌شوند —
        // بدون هیچ جدول یا منطق تکراری. مثل SyncMembersTab، این متد هم از
        // همان یک نقطهٔ هماهنگ‌سازی صدا زده می‌شود.
        private void UpdateCaseSummaryTab()
        {
            txtSummaryCode.Text          = TextOrDash(txtCode.Text);
            txtSummaryHeadName.Text      = TextOrDash(txtHeadFullName.Text);
            txtSummaryRequestType.Text   = TextOrDash(txtRequestType.Text);
            txtSummaryServiceStatus.Text = TextOrDash(txtServiceStatus.Text);

            string province = txtProvince.Text.Trim();
            string district = txtDistrict.Text.Trim();
            txtSummaryLocation.Text = (province == "" && district == "")
                ? "—"
                : province + (district == "" ? "" : " / " + district);

            // آموزش — هفت فیلدِ زیر فقط بازتابِ خواندنیِ همان کنترل‌های موجودِ
            // فرم‌اند (هیچ کوئری/ستونِ تازه‌ای ندارند)؛ ترتیبِ ردیف‌هایشان در
            // Designer طبق بندِ DETAIL SECTION درخواست چیده شده است.
            txtSummaryProvince.Text  = TextOrDash(province);
            txtSummaryDistrict.Text  = TextOrDash(district);
            txtSummaryVillage.Text   = TextOrDash(txtHeadCurrentResidence.Text);
            txtSummaryPhone.Text     = TextOrDash(txtPhone.Text);
            txtSummaryTazkira.Text   = TextOrDash(txtHeadTazkiraNo.Text);
            // «آمر مسئول» در دیتابیس ستونِ اختصاصی ندارد؛ نزدیک‌ترین دادهٔ موجود
            // همان «سروی‌کنندگان» (Surveyors) است که مسئولِ ثبتِ پرونده را
            // نگه می‌دارد. این نگاشت در گزارشِ ممیزی هم صریح ذکر شده است.
            txtSummaryOfficer.Text   = TextOrDash(txtSurveyors.Text);

            string startDate = Helpers.PersianDateHelper.ToPersianDateString(dtpCaseDate.Value);
            txtSummaryStartDate.Text = TextOrDash(startDate);

            LoadImageToPictureBox(savedHeadPhotoPath, picSummaryPhoto);

            // کارتِ «وضعیت خدمات» در ستونِ چپ — همان سه مقدارِ بالا، بدونِ
            // کوئریِ اضافه.
            UpdateServiceStatusCard(startDate);

            if (currentCaseId == 0)
            {
                txtSummaryMemberCount.Text = "—";
                txtSummaryLastAssistance.Text = "—";
                txtSummaryLastChange.Text = "—";
                lblStatMembers.Text  = "—";
                lblStatLastAid.Text  = "—";
                lblStatTotalAid.Text = "—";
                lblStatDocs.Text     = "—";
                lblStatVisits.Text   = "—";
                lblSvcChangeValue.Text = "—";
                return;
            }

            try
            {
                using (var con = db.GetConnection())
                {
                    con.Open();

                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM TblFamily WHERE CasID = @CasID", con))
                    {
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        int members = Convert.ToInt32(cmd.ExecuteScalar());
                        txtSummaryMemberCount.Text = members.ToString();
                        lblStatMembers.Text = members.ToString("N0");
                    }

                    using (var cmd = new SQLiteCommand(
                        "SELECT AssistanceDate, Amount FROM TblAssistance WHERE CasID = @CasID ORDER BY AssistanceID DESC LIMIT 1", con))
                    {
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                decimal amount = Convert.ToDecimal(dr["Amount"]);
                                txtSummaryLastAssistance.Text =
                                    GetDbString(dr, "AssistanceDate") + " — " + amount.ToString("N0");
                                lblStatLastAid.Text = amount.ToString("N0");
                            }
                            else
                            {
                                txtSummaryLastAssistance.Text = "—";
                                lblStatLastAid.Text = "0";
                            }
                        }
                    }

                    // آموزش — دو عددِ زیر (مجموعِ کمک‌ها و تعدادِ مراجعات) از
                    // همان TblAssistance می‌آیند؛ «مراجعه» یعنی هر ردیفِ کمکِ
                    // ثبت‌شده برای این پرونده. جدول یا مفهومِ تازه‌ای ساخته نشد.
                    using (var cmd = new SQLiteCommand(
                        "SELECT COUNT(*), IFNULL(SUM(Amount), 0) FROM TblAssistance WHERE CasID = @CasID", con))
                    {
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                lblStatVisits.Text   = Convert.ToInt32(dr.GetValue(0)).ToString("N0");
                                lblStatTotalAid.Text = Convert.ToDecimal(dr.GetValue(1)).ToString("N0");
                            }
                        }
                    }

                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM TblDocs WHERE CasID = @CasID", con))
                    {
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        lblStatDocs.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString("N0");
                    }

                    using (var cmd = new SQLiteCommand(
                        "SELECT MAX(CreatedAt) FROM TblAuditLog WHERE EntityName = 'TblCase' AND EntityID = @CasID", con))
                    {
                        AddIntParameter(cmd, "@CasID", currentCaseId);
                        object result = cmd.ExecuteScalar();
                        string lastChange = (result == null || result == DBNull.Value) ? "—" : result.ToString();
                        txtSummaryLastChange.Text = lastChange;
                        lblSvcChangeValue.Text = lastChange;
                    }
                }
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در بارگذاری خلاصهٔ پرونده: " + ex.Message);
            }
        }

        // کارتِ «وضعیت خدمات» بالای گرید. رنگِ نشان از خودِ وضعیت می‌آید تا
        // کاربر بدونِ خواندنِ متن هم حالِ پرونده را ببیند — همان سه رنگی که
        // UiTheme از قبل برای موفق/هشدار/خطا تعریف کرده است.
        private void UpdateServiceStatusCard(string startDate)
        {
            if (lblSvcBadge == null) return;

            string status = txtServiceStatus.Text.Trim();
            lblSvcBadge.Text = status == "" ? "—" : status;

            // آموزش — رنگ از واژگانِ رسمیِ CaseDomain می‌آید، نه از حدسِ
            // زیررشته‌ای. با حدسِ قبلی «متقاضی» (وضعیتِ پیش‌فرضِ پروندهٔ نو)
            // قرمزِ خطا می‌گرفت، در حالی‌که یک مرحلهٔ عادیِ پیش از فعال‌شدن است.
            if (status == "")
            {
                lblSvcBadge.ForeColor = Helpers.UiTheme.TextMuted;
                lblSvcBadge.BackColor = Helpers.UiTheme.Background;
            }
            else if (status == Helpers.CaseDomain.StatusActive)
            {
                lblSvcBadge.ForeColor = Helpers.UiTheme.Success;
                lblSvcBadge.BackColor = Helpers.UiTheme.SuccessLight;
            }
            else if (Array.IndexOf(Helpers.CaseDomain.TerminatedStatuses, status) >= 0)
            {
                lblSvcBadge.ForeColor = Helpers.UiTheme.Danger;
                lblSvcBadge.BackColor = Helpers.UiTheme.DangerLight;
            }
            else
            {
                // متقاضی / در حال بررسی / در انتظار تایید و هر مقدارِ ناشناخته
                lblSvcBadge.ForeColor = Helpers.UiTheme.Warning;
                lblSvcBadge.BackColor = Helpers.UiTheme.WarningLight;
            }

            lblSvcStartValue.Text = string.IsNullOrWhiteSpace(startDate) ? "—" : startDate;
        }

        // فقط وقتی کاربر واقعاً روی تب «اعضاء خانواده» می‌رود صدا زده می‌شود
        // (از tabsCase_SelectedIndexChanged). بار اول نمونهٔ FrmFamily را
        // embedded می‌سازد؛ بارهای بعد فقط نمایانش می‌کند/رفرش می‌کند.
        private void EnsureFamilyEmbedded()
        {
            if (currentCaseId == 0)
            {
                lblMembersPlaceholder.Visible = true;
                if (_embeddedFamily != null)
                    _embeddedFamily.Visible = false;
                return;
            }

            if (_embeddedFamily == null)
            {
                _embeddedFamily = new FrmFamily();
                _embeddedFamily.IsEmbedded = true;
                // پیمایشِ پرونده‌ها از داخلِ خودِ تب (دکمه‌های «پروندهٔ قبلی/بعدی»)
                _embeddedFamily.CaseNavigator = NavigateAdjacentCase;
                _embeddedFamily.CurrentCaseId = currentCaseId;
                _embeddedFamily.CurrentCaseCode = txtCode.Text.Trim();
                _embeddedFamily.TopLevel = false;
                _embeddedFamily.FormBorderStyle = FormBorderStyle.None;
                _embeddedFamily.Dock = DockStyle.Fill;
                tabMembersHost.Controls.Add(_embeddedFamily);
                _embeddedFamily.Show(); // FrmFamily_Load خودش با CurrentCaseId فعلی بار می‌کند — رفرشِ دوباره لازم نیست
                _familyDirty = false;
            }
            else if (_familyDirty)
            {
                // فقط وقتی پرونده واقعاً از زمان آخرین بازدید از این تب عوض
                // شده رفرش می‌کنیم؛ رفت‌وبرگشتِ سادهٔ بین تب‌ها دیگر کوئری
                // اضافه نمی‌زند.
                _embeddedFamily.RefreshForCase(currentCaseId, txtCode.Text.Trim());
                _familyDirty = false;
            }

            _embeddedFamily.Visible = true;
            lblMembersPlaceholder.Visible = false;
        }

        // آموزش — فاز A4: عیناً همان الگوی EnsureFamilyEmbedded، برای FrmDocs.
        private void EnsureDocsEmbedded()
        {
            if (currentCaseId == 0)
            {
                lblDocsPlaceholder.Visible = true;
                if (_embeddedDocs != null)
                    _embeddedDocs.Visible = false;
                return;
            }

            if (_embeddedDocs == null)
            {
                _embeddedDocs = new FrmDocs();
                _embeddedDocs.IsEmbedded = true;
                // همان دکمه‌های «پروندهٔ قبلی/بعدی» که تبِ اعضاء دارد
                _embeddedDocs.CaseNavigator = NavigateAdjacentCase;
                _embeddedDocs.CurrentCaseId = currentCaseId;
                _embeddedDocs.CurrentCaseCode = txtCode.Text.Trim();
                _embeddedDocs.TopLevel = false;
                _embeddedDocs.FormBorderStyle = FormBorderStyle.None;
                _embeddedDocs.Dock = DockStyle.Fill;
                tabDocsHost.Controls.Add(_embeddedDocs);
                _embeddedDocs.Show(); // FrmDocs_Load خودش با CurrentCaseId فعلی بار می‌کند
                _docsDirty = false;
            }
            else if (_docsDirty)
            {
                _embeddedDocs.RefreshForCase(currentCaseId, txtCode.Text.Trim());
                _docsDirty = false;
            }

            _embeddedDocs.Visible = true;
            lblDocsPlaceholder.Visible = false;
        }

        // آموزش — درخواستِ کاربر: تب‌های «اعضاء خانواده» و «اسناد» باید دکمهٔ
        // «پروندهٔ قبلی/بعدی» داشته باشند که نظر به شمارهٔ فرم بالا/پایین برود
        // و پرونده‌های دیگر را نشان دهد. آن دو فرم embedded هیچ راهی برای
        // بارگذاریِ پرونده ندارند (و نباید داشته باشند — منطقِ پرونده فقط
        // این‌جاست)، پس FrmCase همین متد را به‌صورت یک delegate در اختیارشان
        // می‌گذارد و خودش تنها مالکِ بارگذاری باقی می‌ماند.
        //
        // direction: ۱ = شمارهٔ فرمِ بزرگ‌تر (بعدی)، ۱- = کوچک‌تر (قبلی).
        // مرتب‌سازی روی CAST(FormNo AS INTEGER) است تا «۱۰» بعد از «۹» بیاید
        // نه بعد از «۱»؛ CasID فقط شکنندهٔ تساوی است (شماره‌های غیرعددی همگی
        // ۰ می‌شوند و بدون آن ترتیبشان قطعی نبود). فیلترِ @CID دقیقاً همان
        // فیلترِ چندمرکزیِ LoadCaseByQuery است تا این مسیر هم پروندهٔ مرکزِ
        // دیگر را نشان ندهد.
        private bool NavigateAdjacentCase(int direction)
        {
            if (currentCaseId == 0)
                return false;

            // در حالتِ ویرایش، پرش به پروندهٔ دیگر تغییراتِ ذخیره‌نشده را
            // بی‌صدا دور می‌ریخت؛ پس اول باید ذخیره/انصراف شود.
            //
            // آموزش — رفعِ باگِ گزارش‌شدهٔ کاربر «دکمهٔ قبلی/بعدی هنگ می‌کند»:
            // این پیام از داخلِ کلیکِ دکمهٔ روی تبِ اعضاء/اسناد صدا زده می‌شود
            // (یک فرمِ embedded، نه FrmCase مستقیماً). Msg.Show بدونِ owner
            // (همان الگویی که ده‌ها جای دیگرِ این فایل دارند) دیالوگ را بدونِ
            // مالکِ صریح باز می‌کند؛ در این مسیرِ خاص (کلیک از فرمِ فرزندِ
            // جاسازی‌شده) دیالوگ گاهی پشتِ پنجرهٔ اصلی گم می‌شد و کاربر فکر
            // می‌کرد برنامه فریز کرده، چون در واقع منتظرِ بستنِ دیالوگِ نامرئی
            // بود. owner=this (خودِ FrmCase) دیالوگ را قطعاً روی پنجرهٔ اصلی و
            // در پیش‌زمینه نگه می‌دارد.
            if (_caseEditMode)
            {
                Msg.Show(this, "اول تغییرات پرونده را ذخیره یا لغو کنید", "",
                    MessageBoxButtons.OK, MessageBoxIcon.None);
                return false;
            }

            int currentFormNo;

            if (!int.TryParse(txtFormNo.Text.Trim(), out currentFormNo))
                currentFormNo = 0;

            bool forward = direction >= 0;

            string comparison = forward
                ? "(CAST(FormNo AS INTEGER) > @FormNo OR (CAST(FormNo AS INTEGER) = @FormNo AND CasID > @CasID))"
                : "(CAST(FormNo AS INTEGER) < @FormNo OR (CAST(FormNo AS INTEGER) = @FormNo AND CasID < @CasID))";

            string order = forward ? "ASC" : "DESC";

            int targetCaseId;

            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT CasID FROM TblCase WHERE (@CID = 0 OR CenterID = @CID) AND " + comparison +
                " ORDER BY CAST(FormNo AS INTEGER) " + order + ", CasID " + order + " LIMIT 1", con))
            {
                AddIntParameter(cmd, "@FormNo", currentFormNo);
                AddIntParameter(cmd, "@CasID", currentCaseId);
                cmd.Parameters.AddWithValue("@CID", SecurityContext.CenterFilterId);
                con.Open();

                object result = cmd.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    Msg.Show(this, forward ? "پروندهٔ بعدی وجود ندارد" : "پروندهٔ قبلی وجود ندارد", "",
                        MessageBoxButtons.OK, MessageBoxIcon.None);
                    return false;
                }

                targetCaseId = Convert.ToInt32(result);
            }

            // LoadCaseById خودش SyncMembersTab را صدا می‌زند، و چون تبِ اعضاء/
            // اسناد همین الان دیده می‌شود، همان‌جا RefreshForCase اجرا می‌شود.
            return LoadCaseById(targetCaseId);
        }

        private void tabsCase_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabsCase.SelectedTab == tabMembersHost.Parent)
                EnsureFamilyEmbedded();
            else if (tabsCase.SelectedTab == tabDocsHost.Parent)
                EnsureDocsEmbedded();

            UpdateWorkspaceWidth();
            UpdateCaseActionsVisibility();
        }

        // آموزش — رفعِ ریشه‌ایِ گزارشِ کاربر «دکمه‌ها برای اسناد و اعضاء جداست».
        // قبلاً دو راهکارِ مسکّن اعمال شده بود: تغییرِ نامِ دکمه‌های embedded
        // (FrmFamily/FrmDocs → «عضو جدید»/«ذخیره سند»...) و افزودنِ برچسبِ
        // «پرونده:» به نوارِ پایین. هر دو ابهام را کم کردند ولی برنداشتند، چون
        // مسئله نامِ دکمه‌ها نبود: روی تبِ اعضاء/اسناد **دو نوارِ فرمان با
        // فعل‌های یکسان** (جدید/ذخیره/ویرایش/حذف) هم‌زمان دیده می‌شدند و کاربر
        // مجبور بود متن بخواند تا بفهمد کدام روی پرونده کار می‌کند.
        //
        // شاهدِ اینکه این دکمه‌ها روی آن دو تب واقعاً بی‌معنا هستند، خودِ کدِ
        // موجود است: EnsureEditableTabVisible اگر کاربر روی این تب‌ها «جدید»/
        // «ویرایش» بزند، او را خودکار به تبِ «مشخصات کلی سرپرست» پرت می‌کند.
        // یعنی سیستم از قبل می‌دانست این فرمان‌ها اینجا قابلِ اجرا نیستند.
        //
        // نسخهٔ اول این رفع، گروهِ «پرونده:» را با Visible=false کاملاً پنهان
        // می‌کرد. به‌درخواستِ کاربر، رفتار به «خیره/غیرفعال» تغییر کرد: دکمه‌ها
        // در جای خودشان می‌مانند ولی کم‌رنگ/غیرقابل‌کلیک می‌شوند — چیدمانِ
        // نوار پایین هیچ‌وقت نمی‌پرد و همچنان روشن است که این چهار فرمان روی
        // این دو تب موقتاً کار نمی‌کنند. عمداً Enabled (نه رنگِ دستی) تغییر
        // می‌کند: چون دکمه‌ها FlatStyle=Flat هستند، WinForms خودش موقعِ
        // Enabled=false ظاهرِ استانداردِ خیره/کم‌رنگ را می‌کشد (بدونِ کدِ رنگِ
        // اضافه) — همان الگویی که Label هم برای برچسبِ «پرونده:» دارد.
        //   • جستجو/تاریخچه و کلِ گروهِ «خروجی‌ها:» دست‌نخورده و فعال می‌مانند،
        //     چون روی خودِ پرونده کار می‌کنند و در هر تبی معنا دارند.
        //   • EnsureEditableTabVisible دست‌نخورده می‌ماند (شبکهٔ ایمنی).
        //   • Enabled=false خودکار میان‌برهای صفحه‌کلید (Ctrl+N/E/D) را هم
        //     روی این دو تب غیرفعال می‌کند — FormShortcuts از قبل Enabled را
        //     چک می‌کند، پس نیازی به تغییرِ جداگانه نبود.
        //
        // ⚠ btnSave طبق قاعدهٔ همیشگیِ SetCaseEditMode فقط در حالتِ ویرایش
        // فعال است؛ پس با برگشتن به تب‌های دیگر، به‌جای «همیشه فعال»، دقیقاً
        // همان وضعیتِ فعلیِ _caseEditMode بازگردانده می‌شود — وگرنه خروج از تبِ
        // اعضاء می‌توانست دکمهٔ ذخیره را در حالتِ غیرِویرایش هم فعال کند.
        private void UpdateCaseActionsVisibility()
        {
            if (tabsCase == null || tabMembersHost == null || tabDocsHost == null)
                return;

            bool onChildEntityTab =
                tabsCase.SelectedTab == tabMembersHost.Parent ||
                tabsCase.SelectedTab == tabDocsHost.Parent;

            bool enable = !onChildEntityTab;

            if (lblCaseSection != null) lblCaseSection.Enabled = enable;
            if (btnNew    != null) btnNew.Enabled    = enable;
            if (btnEdit   != null) btnEdit.Enabled   = enable;
            if (btnDelete != null) btnDelete.Enabled = enable;
            if (btnSave   != null) btnSave.Enabled    = enable && _caseEditMode;
        }

        // آموزش — رفعِ «تب اعضاء کوچک شده»: تب‌های اعضاء و اسناد فرم‌های کاملی
        // (FrmFamily/FrmDocs) هستند که برای عرضِ ~۱۱۶۰ و ~۱۲۴۰ طراحی شده‌اند،
        // ولی ستونِ فیلدها فقط ۶۲٪ عرضِ فرم است، پس داخلِ تب له می‌شدند.
        // وقتی یکی از این دو تب فعال است، ستونِ سمتِ چپ (عکس‌ها + فهرستِ
        // پرونده‌ها) موقتاً جمع می‌شود تا تب تمامِ عرض را بگیرد؛ با برگشتن به
        // بقیه‌ی تب‌ها دقیقاً به همان ۶۲/۳۸ قبلی برمی‌گردد. هیچ کنترلی حذف
        // نمی‌شود — فقط عرضِ ستون عوض می‌شود.
        private const float FieldsColumnNormalPercent = 62F;
        private const float LeftColumnNormalPercent   = 38F;

        private void UpdateWorkspaceWidth()
        {
            if (rootLayout == null || rootLayout.ColumnStyles.Count < 2 || tabsCase == null)
                return;

            bool needsFullWidth =
                tabsCase.SelectedTab == tabMembersHost.Parent ||
                tabsCase.SelectedTab == tabDocsHost.Parent;

            float fieldsWidth = needsFullWidth ? 100F : FieldsColumnNormalPercent;
            float leftWidth   = needsFullWidth ? 0F   : LeftColumnNormalPercent;

            if (Math.Abs(rootLayout.ColumnStyles[1].Width - leftWidth) < 0.01F)
                return;   // از قبل در همین حالت است — چیدمان بی‌دلیل دوباره حساب نشود

            rootLayout.SuspendLayout();
            rootLayout.ColumnStyles[0].Width = fieldsWidth;
            rootLayout.ColumnStyles[1].Width = leftWidth;
            if (leftWorkspacePanel != null)
                leftWorkspacePanel.Visible = !needsFullWidth;
            rootLayout.ResumeLayout(true);
        }

        // آموزش — فاز A4: قبلاً اینجا FrmDocs را با ShowDialog به‌صورت پنجرهٔ
        // مجزا باز می‌کرد. حالا اسناد یک تب داخل همین فرم است؛ این دکمه فقط
        // کاربر را به آن تب می‌برد (همان تغییری که در فاز ۱ برای btnFamily شد).
        private void btnDocs_Click(object sender, EventArgs e)
        {
            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا جستجو کن");
                return;
            }

            tabsCase.SelectedTab = (TabPage)tabDocsHost.Parent;
        }

        private string GetNextFormNo()
        {
            using (SQLiteConnection con = db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
                SELECT COALESCE(MAX(CAST(CASE WHEN FormNo GLOB '*[0-9]*' AND FormNo NOT GLOB '*[^0-9]*' THEN FormNo ELSE '0' END AS INTEGER)), 0) + 1
                FROM TblCase", con))
            {
                con.Open();

                object result = cmd.ExecuteScalar();
                int next = (result == null || result == DBNull.Value) ? 1 : Convert.ToInt32(result);

                int startCaseNo = SettingsHelper.GetInt(SettingsHelper.StartCaseNo, 0);
                if (startCaseNo > next)
                    next = startCaseNo;

                return next.ToString();
            }
        }

        // انتخابِ الگو هنگام خروجی.
        // آموزش — چرا اینجا و نه داخل GetWordTemplatePath: آن متد قرارداد
        // «مسیرِ قالبِ پیش‌فرض را بده یا خطا بینداز» را دارد و ممکن است جای
        // دیگری هم روی همان حساب کند. پس رفتارش دست‌نخورده ماند و انتخاب یک
        // لایه بالاتر انجام می‌شود. اگر فقط یک الگو روی دیسک باشد هیچ پنجره‌ای
        // باز نمی‌شود و مسیر دقیقاً مثل قبل است.
        // خروجیِ خالی یعنی کاربر انصراف داد.
        private string ChooseWordTemplatePath()
        {
            return ChooseWordTemplatePath(false);
        }

        private string ChooseWordTemplatePath(bool allowRdlc)
        {
            var options = Helpers.ReportTemplateHelper.DiscoverCaseTemplates(allowRdlc);

            if (options.Count == 0) return GetWordTemplatePath();   // همان استثنای قبلی
            if (options.Count == 1) return options[0].FilePath;

            var picked = Helpers.FrmTemplatePicker.Ask(this, options,
                Helpers.ReportTemplateHelper.GetRememberedPath());

            if (picked == null) return "";

            Helpers.ReportTemplateHelper.Remember(picked.FilePath);
            return picked.FilePath;
        }

        private string GetWordTemplatePath()
        {
            string templatePath = Path.Combine(Application.StartupPath, "FullCaseTemplate.docx");

            if (!File.Exists(templatePath))
                templatePath = Path.Combine(Application.StartupPath, "Templates", "FullCaseTemplate.docx");

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException(
                    "فایل قالب Word پیدا نشد:" +
                    Environment.NewLine +
                    templatePath);
            }

            return templatePath;
        }

        private string SaveGeneratedFileToCaseCodeFolder(string sourceFilePath)
        {
            return SaveGeneratedFileToCaseCodeFolder(sourceFilePath, txtCode.Text.Trim());
        }

        private string SaveGeneratedFileToCaseCodeFolder(string sourceFilePath, string caseCode)
        {
            string safeCode = CleanFileName(caseCode);

            // آموزش — به درخواست کاربر: نام فایل خروجی فقط «شماره اختصاصی»
            // پرونده باشد و هیچ پسوند/برچسب اضافه‌ای (مثل «_FullCase») نداشته
            // باشد. پسوندِ نوع فایل (.docx/.pdf) خودش توسط SaveFileToCaseFolder
            // از فایل مبدأ حفظ می‌شود، پس فقط همین برچسب حذف می‌شود.
            string savedPath = FileHelper.SaveFileToCaseFolder(
                sourceFilePath,
                caseCode,
                FileHelper.SectionDocs,
                safeCode,
                "");

            if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
                throw new Exception("فایل خروجی ذخیره نشد: " + FileHelper.LastError);

            return savedPath;
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Case.Print"))
            {
                Msg.Show("کاربر اجازه چاپ پرونده را ندارد.");
                return;
            }

            if (currentCaseId == 0)
            {
                Msg.Show("اول پرونده را ذخیره یا از لیست انتخاب کن");
                return;
            }

            var fields = new List<System.Collections.Generic.KeyValuePair<string, string>>
            {
                new System.Collections.Generic.KeyValuePair<string, string>("کد اختصاصی", txtCode.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره فرم", txtFormNo.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره پرونده", txtCaseNo.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("تاریخ تشکیل", CaseManagement.Helpers.PersianDateHelper.ToPersianDateString(dtpCaseDate.Value)),
                new System.Collections.Generic.KeyValuePair<string, string>("زون", txtZone.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("ولایت", txtProvince.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("ولسوالی", txtDistrict.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("نوع درخواست", txtRequestType.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("اولویت‌بندی اقتصادی", txtPriorityLevel.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("نام سرپرست", txtHeadFullName.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("نام پدر سرپرست", txtHeadFatherName.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("سیادت سرپرست", txtHeadSadat.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("مذهب", txtReligion.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره تذکره سرپرست", txtHeadTazkiraNo.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("سکونت اصلی", txtHeadOriginalResidence.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("سکونت فعلی", txtHeadCurrentResidence.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره تماس", txtPhone.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شماره تماس اقارب", txtRelativePhone.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("تحت پوشش دیگر مؤسسات", txtCoveredByOrg.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("اسامی مؤسسات تحت پوشش", txtCoveredByOrgNames.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شغل", txtJob.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("مهارت", txtSkill.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("نوع معلولیت", txtDisabilityType.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("درجه معلولیت", txtDisabilityDegree.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("وضعیت تأهل", txtMaritalStatus.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("تحصیلات", txtEducationLevel.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("وضعیت خدمات", txtServiceStatus.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("دلیل قطع موقت", txtStopReason.Text.Trim()),
                new System.Collections.Generic.KeyValuePair<string, string>("شرح وضعیت فوری", txtUrgentSituation.Text.Trim()),
            };

            PrintHelper.PrintKeyValueDocument(this, "پرونده — " + txtCode.Text.Trim(), fields);
        }

        private async void btnExportExcel_Click(object sender, EventArgs e)
        {
            try
            {
                // پیش از ساخت خروجی، دیالوگِ فیلترهای پیشرفته نمایش داده می‌شود.
                // انصراف از این دیالوگ = انصراف از کل خروجی.
                Helpers.ReportFilterCriteria filter = Helpers.FrmReportFilter.Ask(this);
                if (filter == null) return;

                string rootFolder = FileHelper.GetOrChooseBaseRootFolder();

                if (string.IsNullOrWhiteSpace(rootFolder))
                {
                    Msg.Show("محل ذخیره فایل‌ها مشخص نیست");
                    return;
                }

                string reportsFolder = Path.Combine(rootFolder, "ExcelReports");
                Directory.CreateDirectory(reportsFolder);

                string outputPath = Path.Combine(
                    reportsFolder,
                    "FullExcelReport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".xlsx");

                // خروجی اکسل همان فیلترِ «وضعیت خدمات» گرید را دنبال می‌کند مگر
                // اینکه کاربر در دیالوگِ فیلترِ پیشرفته مقدارِ دیگری انتخاب کرده
                // باشد. مقدار باید پیش از Task.Run خوانده شود (دسترسی به کنترل
                // از نخِ پس‌زمینه مجاز نیست).
                string exportServiceStatus = string.IsNullOrWhiteSpace(filter.ServiceStatus)
                    ? GetSelectedServiceStatusFilter()
                    : filter.ServiceStatus;

                Cursor oldCursor = Cursor;
                Cursor = Cursors.WaitCursor;
                btnExportExcel.Enabled = false;

                try
                {
                    // آموزش — بخش عملکرد: ساخت گزارش کامل اکسل روی همه پرونده‌ها
                    // با رشد داده می‌تواند چند ثانیه طول بکشد؛ Task.Run از فریز
                    // شدن رابط کاربری در این مدت جلوگیری می‌کند.
                    await Task.Run(() =>
                    {
                        ExcelReportExporter exporter = new ExcelReportExporter();
                        exporter.ExportFullReport(outputPath, exportServiceStatus, filter);
                    });
                }
                finally
                {
                    Cursor = oldCursor;
                    btnExportExcel.Enabled = true;
                }

                Msg.Show(
                    "فایل اکسل کامل با موفقیت ساخته شد:" +
                    Environment.NewLine +
                    outputPath);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در ساخت اکسل: " + ex.Message);
            }
        }

        private void cmbServiceStatusFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadCases();
        }

        private bool TryGetBatchExportOptions(
            out int startFormNo,
            out int endFormNo,
            out bool exportWord,
            out bool exportPdf)
        {
            startFormNo = 0;
            endFormNo = 0;
            exportWord = true;
            exportPdf = true;

            using (Form form = new Form())
            using (Label lblStart = new Label())
            using (Label lblEnd = new Label())
            using (TextBox txtStart = new TextBox())
            using (TextBox txtEnd = new TextBox())
            using (CheckBox chkWord = new CheckBox())
            using (CheckBox chkPdf = new CheckBox())
            using (Button btnOk = new Button())
            using (Button btnCancel = new Button())
            {
                form.Text = "خروجی جمعی Word و PDF";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(420, 230);
                form.RightToLeft = RightToLeft.Yes;
                form.RightToLeftLayout = true;

                lblStart.AutoSize = true;
                lblStart.Text = "شروع شماره فرم:";
                lblStart.Location = new Point(285, 30);

                txtStart.Location = new Point(35, 27);
                txtStart.Size = new Size(220, 27);

                lblEnd.AutoSize = true;
                lblEnd.Text = "ختم شماره فرم:";
                lblEnd.Location = new Point(285, 72);

                txtEnd.Location = new Point(35, 69);
                txtEnd.Size = new Size(220, 27);

                chkWord.AutoSize = true;
                chkWord.Text = "ساخت Word";
                chkWord.Checked = true;
                chkWord.Location = new Point(250, 115);

                chkPdf.AutoSize = true;
                chkPdf.Text = "ساخت PDF";
                chkPdf.Checked = true;
                chkPdf.Location = new Point(130, 115);

                btnOk.Text = "شروع خروجی";
                btnOk.Location = new Point(218, 165);
                btnOk.Size = new Size(115, 35);
                btnOk.DialogResult = DialogResult.OK;

                btnCancel.Text = "انصراف";
                btnCancel.Location = new Point(82, 165);
                btnCancel.Size = new Size(95, 35);
                btnCancel.DialogResult = DialogResult.Cancel;

                form.Controls.Add(lblStart);
                form.Controls.Add(txtStart);
                form.Controls.Add(lblEnd);
                form.Controls.Add(txtEnd);
                form.Controls.Add(chkWord);
                form.Controls.Add(chkPdf);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                if (form.ShowDialog(this) != DialogResult.OK)
                    return false;

                if (!int.TryParse(txtStart.Text.Trim(), out startFormNo) ||
                    !int.TryParse(txtEnd.Text.Trim(), out endFormNo))
                {
                    Msg.Show("شماره فرم شروع و ختم باید عدد باشد");
                    return false;
                }

                if (startFormNo <= 0 || endFormNo <= 0)
                {
                    Msg.Show("شماره فرم باید بزرگتر از صفر باشد");
                    return false;
                }

                if (startFormNo > endFormNo)
                {
                    int temp = startFormNo;
                    startFormNo = endFormNo;
                    endFormNo = temp;
                }

                exportWord = chkWord.Checked;
                exportPdf = chkPdf.Checked;

                if (!exportWord && !exportPdf)
                {
                    Msg.Show("حداقل Word یا PDF را انتخاب کنید");
                    return false;
                }

                return true;
            }
        }

        // یک فایلِ docx ساخته‌شده که هنوز منتظرِ تبدیلِ دسته‌ایِ PDF است.
        private sealed class PendingBatchFile
        {
            public string CaseCode;
            public string FormNo;
            public string TempDocx;
        }

        private DataTable GetCasesForBatchExport(int startFormNo, int endFormNo, Helpers.ReportFilterCriteria filter)
        {
            using (var con = db.GetConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT CasID, FormNo, Code
                FROM TblCase
                WHERE CAST(FormNo AS INTEGER) BETWEEN @StartFormNo AND @EndFormNo
                  AND (@CID = 0 OR CenterID = @CID)
                  AND (@ServiceStatus = '' OR ServiceStatus = @ServiceStatus)
                  AND (@Province = '' OR Province = @Province)
                  AND (@District = '' OR District = @District)
                  AND (@FamilyType = '' OR RequestType = @FamilyType)
                  AND (@DateFrom = '' OR CaseDate >= @DateFrom)
                  AND (@DateTo = '' OR CaseDate <= @DateTo)
                  AND (@ActiveOnly = -1 OR IsArchived = @ActiveOnly)
                  AND (@MinMembers = -1 OR (SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = TblCase.CasID) >= @MinMembers)
                  AND (@MaxMembers = -1 OR (SELECT COUNT(*) FROM TblFamily f WHERE f.CasID = TblCase.CasID) <= @MaxMembers)
                ORDER BY CAST(FormNo AS INTEGER), CasID", con))
            {
                AddIntParameter(cmd, "@StartFormNo", startFormNo);
                AddIntParameter(cmd, "@EndFormNo", endFormNo);
                cmd.Parameters.AddWithValue("@CID", Helpers.SecurityContext.CenterFilterId);
                cmd.Parameters.AddWithValue("@MinMembers", filter?.MinMemberCount ?? -1);
                cmd.Parameters.AddWithValue("@MaxMembers", filter?.MaxMemberCount ?? -1);
                // خروجی جمعیِ بازه‌ی شماره فرم هم از فیلترِ وضعیت خدماتِ گرید پیروی
                // می‌کند مگر اینکه کاربر در دیالوگِ فیلترِ پیشرفته مقدارِ دیگری داده باشد.
                AddStringParameter(cmd, "@ServiceStatus",
                    string.IsNullOrWhiteSpace(filter?.ServiceStatus) ? GetSelectedServiceStatusFilter() : filter.ServiceStatus);
                cmd.Parameters.AddWithValue("@Province", filter?.Province ?? "");
                // ولسوالی حالا از کمبوی آبشاری (نه تایپِ آزاد) می‌آید، پس
                // مقایسه‌ی دقیق (=) به‌جای LIKE استفاده می‌شود.
                cmd.Parameters.AddWithValue("@District", filter?.District ?? "");
                cmd.Parameters.AddWithValue("@FamilyType", filter?.FamilyType ?? "");
                cmd.Parameters.AddWithValue("@DateFrom", filter?.RegistrationDateFrom.HasValue == true
                    ? filter.RegistrationDateFrom.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "");
                cmd.Parameters.AddWithValue("@DateTo", filter?.RegistrationDateTo.HasValue == true
                    ? filter.RegistrationDateTo.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "");
                cmd.Parameters.AddWithValue("@ActiveOnly", filter == null ? 0 : (filter.ActiveOnly == true ? 0 : (filter.ActiveOnly == false ? 1 : -1)));

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    return dt;
                }
            }
        }

        private async void btnBatchExport_Click(object sender, EventArgs e)
        {
            // پیش از هر خروجی جمعی، دیالوگِ فیلترهای پیشرفته نمایش داده می‌شود.
            Helpers.ReportFilterCriteria filter = Helpers.FrmReportFilter.Ask(this);
            if (filter == null) return;

            int startFormNo;
            int endFormNo;
            bool exportWord;
            bool exportPdf;

            if (!TryGetBatchExportOptions(out startFormNo, out endFormNo, out exportWord, out exportPdf))
                return;

            if (exportPdf)
            {
                // آموزش — رفعِ اشکالِ واقعیِ گزارش‌شده («خروجی جمعیِ PDF خراب است»):
                // این بررسیِ پیشین قبلاً فقط GetLibreOfficePath (متدِ خصوصیِ همین
                // فرم، مخصوصِ LibreOffice) را صدا می‌زد؛ روی سیستمی که Word نصب
                // دارد ولی LibreOffice ندارد، همیشه استثنا می‌داد و خروجیِ جمعیِ
                // PDF را متوقف می‌کرد — حتی وقتی PdfConversionHelper (که واقعاً
                // برای تبدیلِ هر فایل استفاده می‌شود) با همان Word به‌خوبی کار
                // می‌کرد. حالا دقیقاً همان بررسی‌ای انجام می‌شود که واقعاً برای
                // تبدیل استفاده خواهد شد: PdfConversionHelper.IsAvailable()
                // (Word یا LibreOffice، هرکدام موجود بود).
                if (!PdfConversionHelper.IsAvailable())
                {
                    string msg =
                        "برای ساخت PDF باید Microsoft Word یا LibreOffice نصب باشد." +
                        Environment.NewLine +
                        "فعلاً می‌توانید از خروجی Word استفاده کنید.";

                    if (!exportWord)
                    {
                        Msg.Show(msg);
                        return;
                    }

                    DialogResult dr = Msg.Show(
                        "PDF فعلاً ساخته نمی‌شود:" +
                        Environment.NewLine +
                        msg +
                        Environment.NewLine +
                        Environment.NewLine +
                        "آیا فقط Word ساخته شود؟",
                        "خروجی جمعی",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (dr == DialogResult.No)
                        return;

                    exportPdf = false;
                }
            }

            string oldText = btnBatchExport.Text;
            Cursor oldCursor = Cursor;

            int wordCount = 0;
            int pdfCount = 0;
            int errorCount = 0;
            List<string> errors = new List<string>();

            try
            {
                DataTable cases = GetCasesForBatchExport(startFormNo, endFormNo, filter);

                if (cases.Rows.Count == 0)
                {
                    Msg.Show("در این بازه شماره فرم/فیلترها هیچ پرونده‌ای پیدا نشد");
                    return;
                }

                // یک‌بار برای کلِ دسته پرسیده می‌شود، نه برای هر پرونده. همان
                // دیالوگِ انتخابِ الگوی خروجیِ تک‌پرونده‌ای استفاده می‌شود تا
                // منطقِ انتخابِ الگو بین خروجیِ تکی و جمعی یکسان بماند — به
                // درخواستِ کاربر، الگوی قدیمیِ RDLC هم برای خروجیِ جمعی قابلِ
                // انتخاب است (هر پرونده مستقیم رندر می‌شود، بدون فایلِ میانی).
                string templatePath = ChooseWordTemplatePath(true);
                if (string.IsNullOrEmpty(templatePath)) return;     // انصراف کاربر

                bool isRdlc = templatePath == ReportTemplateHelper.RdlcKey;
                OpenXmlCaseExporter exporter = new OpenXmlCaseExporter();

                Cursor = Cursors.WaitCursor;
                btnBatchExport.Enabled = false;
                btnBatchExport.Text = "در حال ساخت...";

                bool exportWordLocal = exportWord;
                bool exportPdfLocal = exportPdf;

                await Task.Run(() =>
                {
                    if (isRdlc)
                    {
                        // الگوی قدیمی: هر پرونده مستقیماً از RDLC رندر می‌شود
                        // (RdlcExportHelper)، بدون Word و بدون فایلِ میانیِ docx.
                        foreach (DataRow row in cases.Rows)
                        {
                            string caseCode = "", formNo = "";
                            try
                            {
                                int caseId = Convert.ToInt32(row["CasID"]);
                                caseCode = row["Code"] == DBNull.Value ? "" : row["Code"].ToString();
                                formNo = row["FormNo"] == DBNull.Value ? "" : row["FormNo"].ToString();
                                if (string.IsNullOrWhiteSpace(caseCode))
                                    throw new Exception("کد اختصاصی پرونده خالی است");

                                if (exportWordLocal)
                                {
                                    string tempDoc = Path.Combine(Path.GetTempPath(),
                                        CleanFileName(caseCode) + "_" + Guid.NewGuid().ToString("N") + ".doc");
                                    try
                                    {
                                        RdlcExportHelper.ExportCaseToWord(caseId, tempDoc);
                                        SaveGeneratedFileToCaseCodeFolder(tempDoc, caseCode);
                                        wordCount++;
                                    }
                                    finally
                                    {
                                        try { if (File.Exists(tempDoc)) File.Delete(tempDoc); } catch { }
                                    }
                                }

                                if (exportPdfLocal)
                                {
                                    string tempPdf = Path.Combine(Path.GetTempPath(),
                                        CleanFileName(caseCode) + "_" + Guid.NewGuid().ToString("N") + ".pdf");
                                    try
                                    {
                                        RdlcExportHelper.ExportCaseToPdf(caseId, tempPdf);
                                        SaveGeneratedFileToCaseCodeFolder(tempPdf, caseCode);
                                        pdfCount++;
                                    }
                                    finally
                                    {
                                        try { if (File.Exists(tempPdf)) File.Delete(tempPdf); } catch { }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add("فرم " + formNo + " / کد " + caseCode + ": " + ex.Message);
                            }
                        }
                        return;
                    }

                    // آموزش — رفعِ ناپایداریِ گزارش‌شده («خروجی جمعیِ PDF خراب
                    // است»): قبلاً هر پرونده PdfConversionHelper.ConvertDocxToPdf
                    // را جداگانه صدا می‌زد و آن متد برای *هر فایل* یک نمونه‌ی
                    // تازه‌ی Word باز و بسته می‌کرد — روی دسته‌های بزرگ کند و
                    // مستعدِ خطا/فرآیندِ باقی‌مانده بود. حالا: اول همه‌ی فایل‌های
                    // docx ساخته می‌شوند، بعد (اگر PDF خواسته شده) یک‌جا با
                    // ConvertManyDocxToPdf که فقط *یک* نمونه‌ی Word برای کلِ
                    // دسته باز می‌کند تبدیل می‌شوند.
                    var pending = new List<PendingBatchFile>();
                    foreach (DataRow row in cases.Rows)
                    {
                        string caseCode = "", formNo = "", tempDocx = "";
                        try
                        {
                            int caseId = Convert.ToInt32(row["CasID"]);
                            caseCode = row["Code"] == DBNull.Value ? "" : row["Code"].ToString();
                            formNo = row["FormNo"] == DBNull.Value ? "" : row["FormNo"].ToString();

                            if (string.IsNullOrWhiteSpace(caseCode))
                                throw new Exception("کد اختصاصی پرونده خالی است");

                            tempDocx = Path.Combine(
                                Path.GetTempPath(),
                                CleanFileName(caseCode) + "_" + Guid.NewGuid().ToString("N") + ".docx");

                            exporter.ExportFullCaseToWord(caseId, templatePath, tempDocx);

                            if (exportWordLocal)
                            {
                                SaveGeneratedFileToCaseCodeFolder(tempDocx, caseCode);
                                wordCount++;
                            }

                            if (exportPdfLocal)
                            {
                                pending.Add(new PendingBatchFile
                                {
                                    CaseCode = caseCode,
                                    FormNo = formNo,
                                    TempDocx = tempDocx
                                });
                            }
                            else
                            {
                                try { if (File.Exists(tempDocx)) File.Delete(tempDocx); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add("فرم " + formNo + " / کد " + caseCode + ": " + ex.Message);
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(tempDocx) && File.Exists(tempDocx))
                                    File.Delete(tempDocx);
                            }
                            catch { }
                        }
                    }

                    if (exportPdfLocal && pending.Count > 0)
                    {
                        var byPath = new Dictionary<string, PendingBatchFile>(StringComparer.OrdinalIgnoreCase);
                        var docxPaths = new List<string>();
                        foreach (PendingBatchFile p in pending)
                        {
                            docxPaths.Add(p.TempDocx);
                            byPath[p.TempDocx] = p;
                        }

                        PdfConversionHelper.ConvertManyDocxToPdf(docxPaths, delegate (string docxPath, string pdfPath, Exception exOne)
                        {
                            PendingBatchFile info = byPath[docxPath];
                            if (exOne != null)
                            {
                                errorCount++;
                                errors.Add("فرم " + info.FormNo + " / کد " + info.CaseCode + " (PDF): " + exOne.Message);
                                return;
                            }
                            try
                            {
                                SaveGeneratedFileToCaseCodeFolder(pdfPath, info.CaseCode);
                                pdfCount++;
                            }
                            catch (Exception exSave)
                            {
                                errorCount++;
                                errors.Add("فرم " + info.FormNo + " / کد " + info.CaseCode + " (ذخیره PDF): " + exSave.Message);
                            }
                            finally
                            {
                                try { if (File.Exists(pdfPath)) File.Delete(pdfPath); } catch { }
                            }
                        }, null);

                        foreach (PendingBatchFile p in pending)
                        {
                            try { if (File.Exists(p.TempDocx)) File.Delete(p.TempDocx); } catch { }
                        }
                    }
                });

                string message =
                    "خروجی جمعی پایان یافت." +
                    Environment.NewLine +
                    "تعداد پرونده‌ها: " + cases.Rows.Count +
                    Environment.NewLine +
                    "Word ساخته‌شده: " + wordCount +
                    Environment.NewLine +
                    "PDF ساخته‌شده: " + pdfCount +
                    Environment.NewLine +
                    "خطاها: " + errorCount;

                if (errors.Count > 0)
                {
                    message += Environment.NewLine + Environment.NewLine + "چند خطای اول:";

                    int max = Math.Min(errors.Count, 5);
                    for (int i = 0; i < max; i++)
                        message += Environment.NewLine + errors[i];
                }

                Msg.Show(message);
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در خروجی جمعی: " + ex.Message);
            }
            finally
            {
                Cursor = oldCursor;
                btnBatchExport.Enabled = true;
                btnBatchExport.Text = oldText;
            }
        }

        // آموزش — به درخواست کاربر: بکاپ‌گیری/بازیابی از این فرم حذف شد و
        // فقط از طریق تب «Backup و Restore» در تنظیمات (FrmSettings، مخصوص
        // SuperAdmin) انجام می‌شود. این هم UI را ساده‌تر می‌کند و هم یک نقطه
        // ناامن اضافه را می‌بندد: این دو دکمه قبلاً بدون هیچ محدودیت نقشی
        // مستقیماً BackupHelper را صدا می‌زدند، در حالی‌که مسیر تنظیمات از قبل
        // به SuperAdmin محدود شده است.

        private string ConvertDocxToPdfWithLibreOffice(string docxPath)
        {
            string libreOfficePath = GetLibreOfficePath();

            string outputFolder = Path.GetDirectoryName(docxPath);
            string expectedPdfPath = Path.Combine(
                outputFolder,
                Path.GetFileNameWithoutExtension(docxPath) + ".pdf");

            if (File.Exists(expectedPdfPath))
                File.Delete(expectedPdfPath);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = libreOfficePath;
            psi.Arguments =
                "--headless --nologo --nofirststartwizard --nolockcheck " +
                "--convert-to pdf " +
                "--outdir \"" + outputFolder + "\" " +
                "\"" + docxPath + "\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += delegate(object outputSender, DataReceivedEventArgs args)
                {
                    if (args.Data != null)
                        output.AppendLine(args.Data);
                };
                process.ErrorDataReceived += delegate(object errorSender, DataReceivedEventArgs args)
                {
                    if (args.Data != null)
                        error.AppendLine(args.Data);
                };

                if (!process.Start())
                    throw new Exception("LibreOffice اجرا نشد.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(120000))
                {
                    try { process.Kill(); } catch { }
                    throw new Exception("زمان ساخت PDF بیش از حد طولانی شد.");
                }

                process.WaitForExit();

                if (!File.Exists(expectedPdfPath))
                    throw new Exception("LibreOffice نتوانست PDF بسازد. " + output + " " + error);
            }

            return expectedPdfPath;
        }

        private string GetLibreOfficePath()
        {
            string[] possiblePaths =
            {
        @"C:\Program Files\LibreOffice\program\soffice.exe",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
    };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            string pathFromEnvironment = FindExecutableInPath("soffice.exe");
            if (!string.IsNullOrWhiteSpace(pathFromEnvironment))
                return pathFromEnvironment;

            throw new Exception(
                "برای ساخت PDF باید LibreOffice نصب باشد." +
                Environment.NewLine +
                "فعلاً می‌توانید از دکمه خروجی Word استفاده کنید." +
                Environment.NewLine +
                "مسیر مورد انتظار:" +
                Environment.NewLine +
                @"C:\Program Files\LibreOffice\program\soffice.exe");
        }

        private string FindExecutableInPath(string executableName)
        {
            string pathValue = Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrWhiteSpace(pathValue))
                return "";

            foreach (string folder in pathValue.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                try
                {
                    string candidate = Path.Combine(folder.Trim(), executableName);

                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            return "";
        }

    }
}

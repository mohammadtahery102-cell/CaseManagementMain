using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // FrmSyncHelp — راهنمای کاملِ «همگام‌سازی از سامانه مرکزی».
    //
    // آموزش — چرا این پنجره ساخته شد:
    // راهنما پیش از این یک کادرِ همیشه‌بازِ ۳۰۰ پیکسلی داخلِ مرحلهٔ ۱ بود. سه
    // ایراد داشت که هر سه اینجا برطرف شده‌اند:
    //
    //   ۱. بریدگیِ متن — هر خطِ راهنما با AutoSize=false و ارتفاعِ ثابتِ ۱۸/۱۶
    //      پیکسل ساخته می‌شد. هر جمله‌ای که از یک خط بلندتر می‌شد بی‌صدا بریده
    //      می‌شد و کاربر نیمهٔ دوم را هرگز نمی‌دید. اینجا همهٔ برچسب‌ها
    //      AutoSize=true با MaximumSize عرض‌دار هستند: متن می‌پیچد و ارتفاع
    //      خودش را می‌گیرد، پس هیچ‌وقت بریده نمی‌شود — هر قدر هم بلند باشد.
    //
    //   ۲. بودجهٔ ارتفاع — چون راهنما جای ثابتی از مرحلهٔ ۱ را می‌گرفت، هر
    //      جملهٔ تازه‌ای باید به قیمتِ حذفِ جملهٔ دیگری نوشته می‌شد. متن‌ها به
    //      همین دلیل خلاصه و مبهم شده بودند. اینجا صفحه اسکرول دارد، پس
    //      راهنما می‌تواند *کامل و دقیق* باشد.
    //
    //   ۳. نادرستیِ محتوا — راهنمای قبلی می‌گفت «عکس: فقط JPG»، در حالی که
    //      MediaScanner در واقع jpg/jpeg/png/bmp/webp را می‌پذیرد و سند را با
    //      pdf/doc/docx/jpg/jpeg/png. متنِ اینجا مستقیماً از همان ثابت‌های
    //      MediaScanner ساخته می‌شود تا دیگر نتواند از کد عقب بیفتد.
    //
    // این پنجره فقط متن نمایش می‌دهد؛ هیچ داده‌ای نمی‌خواند و نمی‌نویسد.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmSyncHelp : Form
    {
        private const int ContentWidth = 830;

        // نمایش به‌صورت مودال روی صاحبِ پنجره. تنها راه باز کردن.
        public static void ShowHelp(IWin32Window owner)
        {
            ShowHelp(owner, null);
        }

        // ═════════════════════════════════════════════════════════════════════
        // نمایشِ راهنما با پرشِ مستقیم به یک بخش.
        //
        // آموزش — چرا این overload لازم شد: راهنما حالا بلند است و کاربری که
        // در مرحلهٔ ۵ گیر کرده، نباید مجبور شود از بخش ۱ اسکرول کند تا به
        // توضیحِ مرحلهٔ ۵ برسد. ویزارد کلیدِ مرحلهٔ جاری را می‌فرستد و راهنما
        // دقیقاً همان‌جا باز می‌شود.
        //
        // ⚠ کلیدِ ناشناخته خطا نیست: راهنما از ابتدا باز می‌شود. یعنی اگر روزی
        // مرحله‌ای اضافه شود و کلیدش اینجا تعریف نشده باشد، بدترین اتفاق این
        // است که کاربر خودش اسکرول کند — نه اینکه پنجره باز نشود.
        // ═════════════════════════════════════════════════════════════════════
        public static void ShowHelp(IWin32Window owner, string anchorKey)
        {
            try
            {
                using (var form = new FrmSyncHelp())
                {
                    form._pendingAnchor = anchorKey;
                    form.ShowDialog(owner);
                }
            }
            catch (Exception ex)
            {
                try { Enterprise.ErrorLogger.Log(ex, "FrmSyncHelp.ShowHelp"); } catch { }
            }
        }

        private FlowLayoutPanel _body;
        private Panel _scroller;
        private string _pendingAnchor;

        // کلیدِ بخش → نوارِ عنوانِ همان بخش. برای پرشِ مستقیم.
        private readonly Dictionary<string, Control> _anchors =
            new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);

        private FrmSyncHelp()
        {
            Text = "راهنمای کامل همگام‌سازی — آماده‌سازی پوشه و مراحل کار";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            // ارتفاع به صفحه‌نمایش محدود می‌شود — همان محافظی که ویزارد دارد،
            // وگرنه روی نمایشگرِ ۱۳۶۶×۷۶۸ دکمهٔ «بستن» بیرون از صفحه می‌افتد.
            int wanted = 760;
            try
            {
                int usable = Screen.PrimaryScreen.WorkingArea.Height - 60;
                if (usable > 420 && usable < wanted) wanted = usable;
            }
            catch { }
            UiTheme.MakeFixedSize(this, 900, wanted);

            BuildUi();
        }

        private void BuildUi()
        {
            // ── سربرگ ──
            var header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = UiTheme.PrimaryDark };
            header.Controls.Add(new Label
            {
                Text = "راهنمای کامل — همگام‌سازی اطلاعات از سامانه مرکزی",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 20, 0)
            });
            Controls.Add(header);

            // ── نوار پایین ──
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = UiTheme.CardBack };
            var nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(20, 10, 20, 10)
            };

            Button btnClose = UiTheme.CreateButton("بستن", "✕", UiTheme.Primary);
            btnClose.Size = new Size(130, 34);
            btnClose.Margin = new Padding(6, 0, 6, 0);
            btnClose.Click += delegate { Close(); };

            // کپیِ متنِ راهنما — برای چاپ، ارسال به همکار یا چسباندن در پیام.
            Button btnCopy = UiTheme.CreateSecondaryButton("کپی متن راهنما", "⎘");
            btnCopy.Size = new Size(180, 34);
            btnCopy.Margin = new Padding(6, 0, 6, 0);
            btnCopy.Click += delegate { CopyToClipboard(); };

            nav.Controls.Add(btnClose);
            nav.Controls.Add(btnCopy);
            footer.Controls.Add(nav);
            Controls.Add(footer);

            AcceptButton = btnClose;
            CancelButton = btnClose;

            // ── بدنهٔ اسکرول‌شونده ──
            // آموزش — FlowLayoutPanel با جهتِ TopDown انتخاب شد چون ترتیبِ
            // عمودی در فرم‌های راست‌به‌چپ آینه نمی‌شود؛ Dock وارونه می‌شد.
            _scroller = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiTheme.Background,
                Padding = new Padding(16, 12, 16, 12)
            };
            Panel scroller = _scroller;

            _body = new FlowLayoutPanel
            {
                Location = new Point(16, 12),
                Width = ContentWidth + 16,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = UiTheme.Background
            };

            scroller.Controls.Add(_body);
            Controls.Add(scroller);
            scroller.BringToFront();

            BuildContent();
        }

        // پرش به بخشِ خواسته‌شده — پس از اینکه چیدمان نهایی شد.
        //
        // ⚠ چرا در OnShown و نه در سازنده: تا وقتی پنجره نمایش داده نشده،
        // ارتفاعِ واقعیِ کارت‌های AutoSize محاسبه نشده و موقعیتِ نوارِ عنوان
        // هنوز صفر است؛ پرش در آن لحظه همیشه به بالای صفحه می‌رفت.
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (string.IsNullOrWhiteSpace(_pendingAnchor)) return;

            try
            {
                Control target;
                if (_anchors.TryGetValue(_pendingAnchor, out target) && target != null)
                    _scroller.ScrollControlIntoView(target);
            }
            catch (Exception ex)
            {
                try { Enterprise.ErrorLogger.Log(ex, "FrmSyncHelp.OnShown"); } catch { }
            }
            finally { _pendingAnchor = null; }
        }

        // ═════════════════════════════════════════════════════════════════════
        // محتوای راهنما
        //
        // آموزش — بازنویسیِ دوم (به درخواستِ کاربر): صفحه‌ی همگام‌سازی از یک
        // ویزاردِ ۸مرحله‌ای به «۵ دکمه‌ی مستقل» تغییر کرد (FrmSyncSimple).
        // راهنما هم هماهنگ با همان طراحیِ تازه، خلاصه‌تر و ساده‌تر بازنویسی شد؛
        // هر بخش دقیقاً روی یکی از همان ۵ دکمه است. نام‌های پوشه از ثابت‌های
        // MediaScanner خوانده می‌شوند تا هرگز از کد عقب نیفتد.
        // ═════════════════════════════════════════════════════════════════════
        private void BuildContent()
        {
            string photos = MediaScanner.PhotosFolderName;
            string familyPhotos = MediaScanner.FamilyPhotosFolderName;
            string memberPhotos = MediaScanner.MemberPhotosFolderName;
            string docs = MediaScanner.DocumentsFolderName;

            // ─────────────────────────────────────────────────────────────────
            Section("این صفحه چه کاری می‌کند؟");

            Para("شش دکمه‌ی جداگانه دارید. هر دکمه کارِ خودش را می‌کند و به بقیه کاری " +
                 "ندارد — می‌توانید فقط یکی را بزنید، یا هر شش‌تا را، به هر ترتیبی که " +
                 "خواستید.");

            Note("✓", "چیزی پاک نمی‌شود",
                 "این ابزار فقط اضافه می‌کند یا به‌روز می‌کند. هیچ پرونده یا عکسی حذف " +
                 "نمی‌شود.", UiTheme.Success);

            Note("✓", "پیش از هر آپلود، یک نسخه‌ی پشتیبان گرفته می‌شود",
                 "تیکِ «قبل از هر آپلود بکاپ گرفته شود» را روشن نگه دارید (پیش‌فرض روشن " +
                 "است). اگر لازم شد، همیشه می‌توانید برگردید.", UiTheme.Success);

            // ─────────────────────────────────────────────────────────────────
            Section("دکمه ۱ — فایل سرپرستان (Guardians)", "guardians");

            Step("۱", "چه فایلی؟",
                 "همان فایلِ Guardians.html که از سامانه مرکزی می‌گیرید.");

            Step("۲", "چه کاری می‌کند؟",
                 "پرونده‌های تازه می‌سازد و پرونده‌های موجود را به‌روز می‌کند. تطبیق فقط از " +
                 "روی «کدِ اختصاصیِ» پرونده انجام می‌شود.");

            Step("۳", "چطور استفاده کنم؟",
                 "روی «انتخاب فایل» بزنید، Guardians.html را نشان دهید، بعد «آپلود / " +
                 "بروزرسانی» را بزنید. یک خلاصه می‌بینید (چند تازه، چند به‌روزرسانی) و باید " +
                 "تأیید کنید.");

            // ─────────────────────────────────────────────────────────────────
            Section("دکمه ۲ — فایل اعضای خانواده (Members)", "members");

            Step("۱", "چه فایلی؟", "فایلِ Members.html از سامانه مرکزی.");

            Note("⚠", "این دکمه به تنهایی هم کار می‌کند",
                 "اگر خانواده‌ای که عضوش را می‌فرستید از قبل در برنامه ثبت باشد (یا هم‌زمان " +
                 "با دکمه‌ی «سرپرستان» ساخته شود)، نیازی نیست الزاماً هر دو را با هم بزنید.",
                 UiTheme.Primary);

            // ─────────────────────────────────────────────────────────────────
            Section("دکمه ۳ — عکسِ تکیِ سرپرست (Photos)", "photos");

            Step("۱", "چه پوشه‌ای؟",
                 "پوشه‌ای که داخلش عکسِ هر سرپرست است. نامِ هر عکس باید دقیقاً «کدِ " +
                 "اختصاصیِ» همان پرونده باشد؛ مثلاً 100245.jpg");

            Note("⚠", "پرونده باید از قبل وجود داشته باشد",
                 "این دکمه فقط عکس را به پرونده‌ای که از قبل هست وصل می‌کند. اگر پرونده هنوز " +
                 "ساخته نشده (اول باید دکمه‌ی «سرپرستان» را بزنید)، عکسش «بدون صاحب» " +
                 "می‌ماند.", UiTheme.Danger);

            // ─────────────────────────────────────────────────────────────────
            Section("دکمه ۴ — عکسِ جمعیِ خانواده (FamilyPhoto)", "familyphoto");

            Step("۱", "چه پوشه‌ای؟",
                 "پوشه‌ای با عکسِ جمعیِ هر خانواده. نامِ فایل، مثلِ بالا، کدِ اختصاصیِ پرونده " +
                 "است.");

            // ─────────────────────────────────────────────────────────────────
            Section("دکمه ۵ — عکسِ تک‌تکِ اعضا (MemberPhotos)", "memberphotos");

            Step("۱", "چه پوشه‌ای؟",
                 "داخلِ این پوشه، به‌ازای هر پرونده یک پوشه با نامِ کدِ اختصاصیِ همان پرونده " +
                 "بسازید، و عکسِ هر عضو را با نامِ همان عضو در آن بگذارید.");

            // ─────────────────────────────────────────────────────────────────
            Section("دکمه ۶ — اسنادِ پرونده‌ها (Documents)", "documents");

            Step("۱", "چه پوشه‌ای؟",
                 "پوشه‌ای به نام «" + docs + "». داخلِ آن، به‌ازای هر پرونده یک پوشه با نامِ " +
                 "کدِ اختصاصیِ همان پرونده بسازید، و اسنادش (تذکره، قباله و مانند آن) را در " +
                 "همان پوشه بگذارید.");

            Note("⚠", "پرونده باید از قبل وجود داشته باشد",
                 "درست مثلِ عکس‌ها: اول باید پرونده ساخته شده باشد (دکمه‌ی «سرپرستان») تا " +
                 "سندش پیدا کند.", UiTheme.Danger);

            // ─────────────────────────────────────────────────────────────────
            Section("همین یک قانون را همیشه رعایت کنید");

            Note("✓", "نام = کدِ اختصاصی، همیشه",
                 "نامِ هر عکس یا هر پوشه باید دقیقاً «کدِ اختصاصیِ» همان پرونده باشد — نه " +
                 "نامِ شخص، نه شماره‌ی فرم. اگر این کد اشتباه یا فرق داشته باشد، آن فایل به " +
                 "هیچ پرونده‌ای وصل نمی‌شود.", UiTheme.Success);

            Note("⚠", "نامِ پوشه‌ها باید دقیقاً همین‌طور و انگلیسی باشد",
                 "«" + photos + "»، «" + familyPhotos + "»، «" + memberPhotos + "»، «" + docs + "» — همین " +
                 "املا. اگر پوشه‌ای را با پوشه‌ی سیستم انتخاب می‌کنید فرقی نمی‌کند، این فقط " +
                 "برای زمانی است که خودتان پوشه‌ی بسته را دستی می‌سازید.", UiTheme.Danger);

            Note("✓", "شما نیازی به آماده‌سازیِ دستیِ کد ندارید",
                 "سیستم پیش از هر مقایسه، کدها را خودش یکدست می‌کند: رقم فارسی/عربی به لاتین " +
                 "تبدیل می‌شود، فاصله‌های اضافه (ابتدا/انتها/میان رقم‌ها) حذف می‌شوند، و " +
                 "نشانه‌های نامرئیِ جهت‌دهیِ متن که فایل‌های HTML خروجیِ Excel/Word معمولاً " +
                 "دورِ اعداد فارسی می‌گذارند (کاملاً نامرئی‌اند، نه در فایل دیده می‌شوند نه در " +
                 "پیام خطا) هم نادیده گرفته می‌شوند. یعنی کدِ «۲۰۹۹» در فایل HTML و «2099.jpg» " +
                 "در پوشه‌ی عکس، با هر رسم‌الخطی نوشته شده باشند، یک کد شناخته می‌شوند — " +
                 "کاری از دستِ شما لازم نیست.", UiTheme.Success);

            // ─────────────────────────────────────────────────────────────────
            Section("مشکلاتِ رایج");

            Trouble("کدِ فایل و کدِ پرونده برای من دقیقاً یکی به‌نظر می‌رسند ولی پیامِ «پیدا نشد» می‌بینم",
                    "پیامِ خطا کدی را که واقعاً جست‌وجو شده هم نشان می‌دهد (مثلاً «کد «2099» " +
                    "(جست‌وجوشده به‌صورت «2099») در دیتابیس نیست»). اگر آن دو مقدار در پیام با هم " +
                    "فرق داشتند، مشکل از رسم‌الخط بوده و سیستم داشت درست کار می‌کرد ولی کدِ واقعیِ " +
                    "پرونده چیزِ دیگری است. اگر دقیقاً یکی بودند، پرونده‌ای با آن کد در دیتابیسِ " +
                    "فعلی نیست — از تبِ «پرونده‌ها» با همان کد جست‌وجو کنید تا مطمئن شوید پرونده " +
                    "واقعاً ثبت شده و مربوط به همین دیتابیس/مرکز است.");

            Trouble("عکس‌ها وارد نمی‌شوند و پیامِ «بدون صاحب»/«پرونده در دیتابیس نیست» می‌بینم",
                    "یکی از این دو مورد است: (۱) پرونده‌ای که کدش با نامِ عکس یکی است هنوز " +
                    "ساخته نشده — اول دکمه‌ی «سرپرستان» را بزنید. (۲) برنامه‌ای که باز است به " +
                    "یک نسخه‌ی دیگر/خالی از دیتابیس وصل است — از مدیرِ سیستم بپرسید کدام " +
                    "نسخه‌ی برنامه را همیشه باز کنید.");

            Trouble("عکس‌ها وارد شدند ولی به هیچ پرونده‌ای وصل نشدند",
                    "نامِ فایل، کدِ اختصاصیِ پرونده نیست. مثلاً «100245 (1).jpg» شناخته " +
                    "نمی‌شود. نامِ فایل باید دقیقاً همان کد باشد، بدون هیچ حرفِ اضافه.");

            Trouble("«کد در فایل تکراری است»",
                    "دو ردیف در فایلِ سرپرستان کدِ یکسان دارند. فقط ردیفِ اول ثبت می‌شود. " +
                    "این را در سامانه مرکزی اصلاح کنید.");

            Trouble("«خانواده‌ای با این کد وجود ندارد»",
                    "عضوی در فایلِ اعضا هست که سرپرستش نه در برنامه است و نه هم‌زمان با دکمه‌ی " +
                    "«سرپرستان» فرستاده شده. اول دکمه‌ی «سرپرستان» را بزنید، بعد «اعضا» را.");

            Trouble("چیزی ثبت نشد و پیامِ خطا آمد",
                    "نگران نباشید — تا آن لحظه هیچ‌چیز ثبت نشده. متنِ خطا را ذخیره کنید و به " +
                    "پشتیبانی نشان دهید.");

            // ─────────────────────────────────────────────────────────────────
            Section("روشِ پیشرفته (بستهٔ ترکیبی)");

            Para("اگر یک‌جا، هزاران پرونده و عکس با هم دارید، دکمه‌ی «روشِ پیشرفته» یک " +
                 "پنجره‌ی جداگانه با مراحلِ بیشتر (پیش‌نمایشِ کاملِ تغییرات پیش از ثبت) باز " +
                 "می‌کند — برای کارِ روزمره و مقدارهای کم لازم نیست از آن استفاده کنید.");

            Spacer(18);
        }

        // ═════════════════════════════════════════════════════════════════════
        // سازنده‌های عنصرهای راهنما
        //
        // ⚠ قاعدهٔ مشترکِ همه: AutoSize = true به‌همراه MaximumSize عرض‌دار.
        // این ترکیب باعث می‌شود متن بپیچد و ارتفاعِ لازم را خودش بگیرد — یعنی
        // هیچ جمله‌ای هرگز بریده نمی‌شود، هر قدر هم بلند باشد.
        // ═════════════════════════════════════════════════════════════════════
        private void Section(string title)
        {
            Section(title, null);
        }

        // کلیدِ اختیاری، برای پرشِ مستقیم از ویزارد (ShowHelp با anchorKey).
        private void Section(string title, string anchorKey)
        {
            Spacer(14);

            var bar = new Panel
            {
                Width = ContentWidth,
                Height = 34,
                BackColor = UiTheme.PrimaryDark,
                Margin = new Padding(0, 0, 0, 6)
            };

            if (!string.IsNullOrWhiteSpace(anchorKey)) _anchors[anchorKey] = bar;
            bar.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = UiTheme.FontBold(UiTheme.SizeMedium),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 12, 0)
            });
            _body.Controls.Add(bar);
        }

        private void Para(string text)
        {
            _body.Controls.Add(Card(new[]
            {
                Line(text, UiTheme.Font(UiTheme.SizeBody), UiTheme.TextDark, ContentWidth - 28)
            }));
        }

        private void Step(string number, string heading, string detail)
        {
            _body.Controls.Add(Card(new[]
            {
                Line(number + " ·   " + heading, UiTheme.FontBold(UiTheme.SizeBody),
                     UiTheme.TextDark, ContentWidth - 28),
                Line(detail, UiTheme.Font(UiTheme.SizeSmall), UiTheme.TextMuted, ContentWidth - 40)
            }));
        }

        private void Note(string glyph, string heading, string detail, Color accent)
        {
            _body.Controls.Add(Card(new[]
            {
                Line(glyph + "   " + heading, UiTheme.FontBold(UiTheme.SizeBody), accent,
                     ContentWidth - 28),
                Line(detail, UiTheme.Font(UiTheme.SizeSmall), UiTheme.TextMuted, ContentWidth - 40)
            }));
        }

        // ⚠ نشانهٔ ابتدای خط با «نشانگر راست‌به‌چپ» (U+200F) قاب می‌شود.
        // بدون آن، ویندوز نشانهٔ خنثی (• یا ☐) را عضوِ جملهٔ بعدی می‌شمارد و
        // بسته به اینکه جمله با کلمهٔ فارسی شروع شود یا لاتین، آن را وسطِ خط
        // می‌اندازد — در آزمونِ تصویری دقیقاً همین اتفاق افتاد. RLM جهتِ آن
        // نشانه را صریحاً راست‌به‌چپ می‌کند تا همیشه در لبهٔ راست بنشیند.
        private const string Rlm = "‏";

        private void Bullet(string text)
        {
            _body.Controls.Add(Card(new[]
            {
                Line(Rlm + "•" + Rlm + "   " + text,
                     UiTheme.Font(UiTheme.SizeBody), UiTheme.TextDark, ContentWidth - 28)
            }));
        }

        private void Check(string text)
        {
            _body.Controls.Add(Card(new[]
            {
                Line(Rlm + "☐" + Rlm + "   " + text,
                     UiTheme.Font(UiTheme.SizeBody), UiTheme.TextDark, ContentWidth - 28)
            }));
        }

        private void Trouble(string symptom, string cure)
        {
            _body.Controls.Add(Card(new[]
            {
                Line("✖   " + symptom, UiTheme.FontBold(UiTheme.SizeBody), UiTheme.Danger,
                     ContentWidth - 28),
                Line("درمان:  " + cure, UiTheme.Font(UiTheme.SizeSmall), UiTheme.TextMuted,
                     ContentWidth - 40)
            }));
        }

        // نمودارِ درختیِ پوشه — چپ‌به‌راست و با فونتِ عرضِ ثابت.
        //
        // آموزش — این تنها بخشی است که عمداً RightToLeft.No دارد: خطوطِ ‎│ ├ └‎
        // و نام‌های انگلیسیِ پوشه یک نقشهٔ چپ‌به‌راست‌اند و در بافتِ راست‌به‌چپ
        // درهم می‌ریزند. فونتِ Consolas ستون‌ها را هم‌تراز نگه می‌دارد.
        private void Tree(params string[] lines)
        {
            var box = new Panel
            {
                BackColor = Color.FromArgb(0xF7, 0xF9, 0xFC),
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                // ⚠ عرض با Min/Max پین می‌شود، نه با Width: کنترلی که AutoSize
                // دارد مقدارِ Width را نادیده می‌گیرد و به اندازهٔ محتوایش جمع
                // می‌شود. نتیجه‌اش کارت‌هایی با عرض‌های ناهمگون بود که در
                // چیدمانِ راست‌به‌چپ ظاهری دندانه‌دار می‌ساخت (در آزمونِ تصویری
                // دیده شد). با Min=Max عرض ثابت می‌ماند و فقط ارتفاع رشد می‌کند.
                MinimumSize = new Size(ContentWidth, 0),
                MaximumSize = new Size(ContentWidth, 0),
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(12, 10, 12, 10),
                RightToLeft = RightToLeft.No
            };

            var stack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(12, 10),
                BackColor = Color.Transparent,
                RightToLeft = RightToLeft.No
            };

            foreach (string line in lines)
            {
                stack.Controls.Add(new Label
                {
                    Text = line,
                    AutoSize = true,
                    Font = new Font("Consolas", 9.5F),
                    ForeColor = UiTheme.TextDark,
                    Margin = new Padding(0, 0, 0, 1),
                    RightToLeft = RightToLeft.No
                });
            }

            box.Controls.Add(stack);
            _body.Controls.Add(box);
        }

        // کارتِ سفیدِ مشترکِ همهٔ بندها.
        private Panel Card(Label[] lines)
        {
            var box = new Panel
            {
                BackColor = UiTheme.CardBack,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(ContentWidth, 0),   // توضیح در Tree
                MaximumSize = new Size(ContentWidth, 0),
                Margin = new Padding(0, 0, 0, 5),
                Padding = new Padding(12, 8, 12, 8)
            };

            var stack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(ContentWidth - 28, 0),
                MaximumSize = new Size(ContentWidth - 28, 0),
                Location = new Point(12, 8),
                BackColor = Color.Transparent
            };

            foreach (Label line in lines) stack.Controls.Add(line);

            box.Controls.Add(stack);
            return box;
        }

        private static Label Line(string text, Font font, Color color, int width)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                // عرض با Min=Max پین می‌شود تا برچسب همیشه تمام‌عرض باشد: برچسبِ
                // AutoSize به اندازهٔ متنش جمع می‌شود و در FlowLayoutPanel به لبهٔ چپ
                // می‌چسبد — یعنی جمله‌های کوتاه وسطِ کارت رها می‌شدند و نشانهٔ
                // ابتدای خط در میانهٔ کارت می‌افتاد (در آزمونِ تصویری دیده شد).
                // ارتفاع همچنان آزاد است، پس متن می‌پیچد و هرگز بریده نمی‌شود.
                MinimumSize = new Size(width, 0),
                MaximumSize = new Size(width, 0),
                Font = font,
                ForeColor = color,
                // ⚠ TopLeft عمدی است، نه اشتباه: در فرمی که RightToLeftLayout
                // دارد، چیدمانِ برچسبِ تمام‌عرض آینه می‌شود و «چپ» روی صفحه
                // «راست» دیده می‌شود. با TopRight متن به لبهٔ چپ می‌چسبید
                // (در آزمونِ تصویری دیده شد). همان قاعده‌ای که در بقیهٔ فرم‌های
                // این پروژه هم مستند شده است.
                TextAlign = ContentAlignment.TopLeft,
                Margin = new Padding(0, 0, 0, 2)
            };
        }

        private void Spacer(int height)
        {
            _body.Controls.Add(new Panel
            {
                Width = ContentWidth,
                Height = height,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        // کپیِ متنِ راهنما
        //
        // متن از خودِ برچسب‌های ساخته‌شده جمع می‌شود، نه از یک نسخهٔ دومِ
        // دستی — وگرنه دو متن با هم اختلاف پیدا می‌کردند.
        // ═════════════════════════════════════════════════════════════════════
        private void CopyToClipboard()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("راهنمای کامل — همگام‌سازی اطلاعات از سامانه مرکزی");
                sb.AppendLine(new string('─', 60));
                sb.AppendLine();

                Collect(_body, sb);

                Clipboard.SetText(sb.ToString());
                Msg.Show(this, "متن کامل راهنما در حافظه کپی شد.", "کپی شد",
                         MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                try { Enterprise.ErrorLogger.Log(ex, "FrmSyncHelp.CopyToClipboard"); } catch { }
                UiTheme.ShowError(this, "کپی کردن متن ممکن نشد: " + ex.Message);
            }
        }

        private static void Collect(Control parent, StringBuilder sb)
        {
            foreach (Control child in parent.Controls)
            {
                var label = child as Label;
                if (label != null && !string.IsNullOrWhiteSpace(label.Text))
                    sb.AppendLine(label.Text);
                else
                    Collect(child, sb);
            }
        }
    }
}

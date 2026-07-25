using CaseManagement.DAL;
using CaseManagement.Helpers;
using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CaseManagement
{
    public class FrmLogin : Form
    {
        private readonly DatabaseHelper _db = new DatabaseHelper();

        private ComboBox _cmbCenter;
        private TextBox  _txtUsername;
        private TextBox  _txtPassword;
        private Button   _btnLogin;
        private Button   _btnShowPass;
        private Button   _btnChangePass;
        private Label    _lblMessage;

        public FrmLogin()
        {
            BuildUi();
        }

        // ─── پالت رنگ صفحه‌ی ورود (طبق طرح تصویریِ تأییدشده) ────────────────
        internal static readonly Color CanvasTop    = ColorTranslator.FromHtml("#0F1B33");
        internal static readonly Color CanvasBottom = ColorTranslator.FromHtml("#16294D");
        internal static readonly Color RoyalBlue    = ColorTranslator.FromHtml("#1D4ED8");
        internal static readonly Color GoldAccent   = ColorTranslator.FromHtml("#C89B3C");

        private void BuildUi()
        {
            // ═══ فاز ۱ — پنجره ═══════════════════════════════════════════════
            // قابِ ویندوز حذف شد و نوار عنوانِ سفارشی جایگزین آن شد؛ اندازه طبق
            // تصمیم تأییدشده ۱۱۰۰×۷۲۰ (افقی، مناسب دسکتاپ). هیچ منطق ورود/
            // دیتابیس/رویدادی در این فاز تغییر نکرده است.
            Text              = "ورود به سیستم";
            StartPosition     = FormStartPosition.CenterScreen;
            // آموزش — اندازه‌ی طراحی با DPI مقیاس می‌شود، اما هرگز از ناحیه‌ی
            // کاریِ صفحه بزرگ‌تر نمی‌شود. تست مقیاس نشان داد بدون این محدودیت،
            // در ۱۵۰٪ و ۲۰۰٪ ارتفاع فرم (۱۰۸۰ و ۱۴۴۰) از ارتفاع صفحه بیشتر
            // می‌شد و کنترل‌های پایین (دکمه‌ی تغییر رمز و فیلدها) بیرون می‌زدند.
            Size designSize = ResponsiveLayout.Scale(new Size(1100, 720));
            Size minSize    = ResponsiveLayout.Scale(new Size(980, 660));
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                designSize = new Size(Math.Min(designSize.Width, wa.Width), Math.Min(designSize.Height, wa.Height));
                minSize    = new Size(Math.Min(minSize.Width, wa.Width),    Math.Min(minSize.Height, wa.Height));
            }
            catch { }

            // آموزش — به درخواست کاربر، صفحه‌ی ورود اندازه‌ی ثابت دارد و
            // بزرگ/تمام‌صفحه نمی‌شود: این صفحه محتوای ثابتی دارد و بزرگ‌شدنش
            // فقط فضای خالی می‌سازد (برخلاف فرم‌های کاری که جدول و فهرست
            // دارند). MinimumSize و MaximumSize روی همان اندازه قفل می‌شوند تا
            // با کشیدن لبه هم تغییر نکند.
            ClientSize        = designSize;
            FormBorderStyle   = FormBorderStyle.None;
            MinimumSize       = Size;
            MaximumSize       = Size;
            MaximizeBox       = false;
            MinimizeBox       = true;
            RightToLeft       = RightToLeft.Yes;
            RightToLeftLayout = false; // چیدمان دستی است؛ آینه‌ی هندسی لازم نیست
            BackColor         = CanvasTop;
            Font              = UiTheme.Font(10.5f);
            DoubleBuffered    = true;
            try { Icon = LogoHelper.GetAppIcon(); } catch { }

            // گوشه‌های گردِ بومیِ ویندوز ۱۱ (روی ویندوز ۱۰ بی‌اثر و بی‌ضرر).
            HandleCreated += delegate { WindowChrome.ApplyRoundedCorners(this); };

            // نوار عنوان سفارشی
            // showMaximize:false — چون صفحه‌ی ورود اندازه‌ی ثابت دارد، دکمه‌ی
            // بیشینه‌سازی هم نباید نمایش داده شود (وگرنه دکمه‌ای که کاری
            // نمی‌کند روی نوار عنوان می‌ماند).
            ModernTitleBar titleBar = new ModernTitleBar(this, "سیستم مدیریت پرونده گنجینه", CanvasTop, showMaximize: false);
            Controls.Add(titleBar);

            // ═══ چیدمان دو ستونه، کاملاً واکنش‌گرا ═══════════════════════════
            // آموزش — همه‌ی مختصات مطلقِ قبلی حذف شدند و جایشان Dock/TableLayout
            // آمد. دلیل: با مختصات ثابت، در مقیاس ۱۲۵٪/۱۵۰٪/۲۰۰٪ کنترل‌ها روی
            // هم می‌افتند یا از کارت بیرون می‌زنند. حالا هر ناحیه سهمش را از
            // چیدمان می‌گیرد و اعدادِ باقی‌مانده هم از ResponsiveLayout.Scale
            // عبور می‌کنند تا با DPI بزرگ شوند.
            //
            // بسیار مهم: نام کنترل‌ها، رویدادها و منطق ورود دست‌نخورده‌اند —
            // _cmbCenter/_txtUsername/_txtPassword/_btnLogin/_btnChangePass/
            // _btnShowPass/_lblMessage همان اشیای قبلی با همان هندلرها هستند.
            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            // ── ستون راست: کارت ورود (عرضِ ثابتِ مقیاس‌شده) ──
            int cardHostWidth = ResponsiveLayout.Scale(470);
            Panel cardHost = new Panel
            {
                Dock = DockStyle.Right,
                Width = cardHostWidth,
                BackColor = Color.Transparent,
                Padding = ResponsiveLayout.Scale(new Padding(20, 26, 40, 26))
            };

            LoginCard card = new LoginCard { Dock = DockStyle.Fill };

            // محتوای کارت، از بالا به پایین
            // AutoScroll آخرین خطِ دفاع است: اگر روی نمایشگرِ کوتاه یا مقیاسِ
            // ۲۰۰٪ ارتفاع کافی نبود، محتوا اسکرول می‌شود به‌جای اینکه از کارت
            // بیرون بزند (تست مقیاس دقیقاً همین سرریز را پیدا کرد).
            Panel cardInner = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                AutoScroll = true,
                Padding = ResponsiveLayout.Scale(new Padding(34, 30, 34, 22))
            };

            // ─── پاورقی کارت: نسخه و پشتیبانی (خواسته‌ی کاربر) ───
            // ارتفاع از ۴۶ به ۵۶ افزایش یافت: در رندر واقعی، خطِ «نسخه» زیر
            // لبه‌ی کارت می‌افتاد و نصفه دیده می‌شد.
            Panel cardFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = ResponsiveLayout.Scale(56),
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 0, ResponsiveLayout.Scale(10))
            };
            Label lblSupport = new Label
            {
                Dock = DockStyle.Bottom, Height = ResponsiveLayout.Scale(20),
                Text = SupportLine(),
                Font = UiTheme.Font(8.5F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true
            };
            Label lblVersion = new Label
            {
                Dock = DockStyle.Bottom, Height = ResponsiveLayout.Scale(20),
                Text = "نسخه " + AppVersionText(),
                Font = UiTheme.FontBold(8.5F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter
            };
            cardFooter.Controls.Add(lblSupport);
            cardFooter.Controls.Add(lblVersion);

            // ─── پیام خطا (بالای پاورقی) ───
            _lblMessage = new Label
            {
                Dock = DockStyle.Bottom,
                Height = ResponsiveLayout.Scale(40),
                Font = UiTheme.Font(9.5F),
                ForeColor = UiTheme.Danger,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ─── سربرگ کارت ───
            Label title = new Label
            {
                Dock = DockStyle.Top, Height = ResponsiveLayout.Scale(38),
                Text = "خوش آمدید",
                Font = UiTheme.FontBold(17F), ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleCenter
            };
            // آموزش — رفع باگی که تست مقیاس پیدا کرد: نسخه‌ی اول این خط را با
            // Padding چپ/راستِ بزرگ وسط‌چین می‌کرد. اگر آن Padding از عرضِ
            // در دسترس بیشتر شود (کارت باریک یا مقیاس بالا)، عرضِ خط صفر
            // می‌شد — در هر چهار مقیاس به‌عنوان «کنترل با اندازه‌ی صفر» گزارش
            // شد. حالا عرضِ ثابت دارد و با Anchor وسط می‌ماند، پس هرگز صفر
            // نمی‌شود.
            Panel goldRuleHost = new Panel
            {
                Dock = DockStyle.Top, Height = ResponsiveLayout.Scale(12),
                BackColor = Color.Transparent
            };
            Panel goldRule = new Panel
            {
                Width = ResponsiveLayout.Scale(56),
                Height = ResponsiveLayout.Scale(3),
                BackColor = GoldAccent,
                Anchor = AnchorStyles.Top
            };
            goldRuleHost.Controls.Add(goldRule);
            goldRuleHost.Resize += delegate
            {
                goldRule.Left = Math.Max(0, (goldRuleHost.ClientSize.Width - goldRule.Width) / 2);
                goldRule.Top = ResponsiveLayout.Scale(4);
            };

            Label subtitle = new Label
            {
                Dock = DockStyle.Top, Height = ResponsiveLayout.Scale(34),
                Text = "برای ادامه وارد حساب کاربری خود شوید",
                Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ─── فیلدها (همان کنترل‌های قبلی، فقط با پوسته‌ی گردگوشه) ───
            _cmbCenter = new ComboBox();
            _cmbCenter.DropDownStyle = ComboBoxStyle.DropDownList;
            FieldBox boxCenter = new FieldBox(new Label(), "مرکز", _cmbCenter) { Dock = DockStyle.Top };

            _txtUsername = new TextBox();
            FieldBox boxUser = new FieldBox(new Label(), "نام کاربری", _txtUsername) { Dock = DockStyle.Top };

            // آموزش — رفع دو ایراد که کاربر دید:
            // ۱) ستاره‌ی «*» زشت و ناخوانا بود. UseSystemPasswordChar همان
            //    نقطه‌ی توپرِ استاندارد ویندوز (●) را می‌گذارد.
            // ۲) متن هنگام تایپ کج می‌رفت: رمز عبور متنِ فارسی نیست و با
            //    RightToLeft=Yes، مکان‌نما و کاراکترها برعکس جابه‌جا می‌شدند.
            //    با RightToLeft=No همراه با TextAlign=Center، ماسک دقیقاً از
            //    وسط و پایدار رشد می‌کند.
            _txtPassword = new TextBox { UseSystemPasswordChar = true };
            FieldBox boxPass = new FieldBox(new Label(), "رمز عبور", _txtPassword) { Dock = DockStyle.Top };

            // به درخواست کاربر: برچسب و متنِ همه‌ی فیلدهای ورود وسط‌چین باشند.
            boxCenter.CenterContent();
            boxUser.CenterContent();
            boxPass.CenterContent();

            // دکمه‌ی نمایش رمز، داخل همان قابِ فیلدِ رمز (سمت چپِ بصری)
            // آیکون چشم از فونتِ آیکونیِ ویندوز گرفته می‌شود، نه از کاراکترِ
            // «⊙» که در فونت فارسیِ برنامه وجود ندارد و به‌صورت مربعِ خالی رسم
            // می‌شد (همان مشکلی که در نوار تب‌ها و دکمه‌های فرم خانواده هم بود).
            _btnShowPass = UiTheme.CreateSecondaryButton("", "");
            // U+E7B3 = آیکونِ «چشم» در فونت Segoe MDL2 Assets. به‌صورت کدِ
            // یونیکد نوشته شده تا مستقل از رمزگذاریِ فایل، درست باقی بماند.
            _btnShowPass.Text = "";
            _btnShowPass.Font = IconFont.Get(10.5F);
            _btnShowPass.ForeColor = UiTheme.TextMuted;
            _btnShowPass.Dock = DockStyle.Left;
            _btnShowPass.Width = ResponsiveLayout.Scale(40);
            _btnShowPass.FlatAppearance.BorderSize = 0;
            _btnShowPass.BackColor = Color.White;
            _btnShowPass.Click += (s, e) =>
                // با UseSystemPasswordChar، خاموش/روشن‌کردنِ همان خاصیت، رمز را
                // نمایان یا پنهان می‌کند (رفتار قبلی دقیقاً حفظ شده است).
                _txtPassword.UseSystemPasswordChar = !_txtPassword.UseSystemPasswordChar;
            new ToolTip().SetToolTip(_btnShowPass, "نمایش/پنهان‌کردن رمز عبور");
            AttachInsideField(boxPass, _btnShowPass);

            // ─── دکمه‌های اقدام ───
            _btnLogin = UiTheme.CreateButton("ورود به سیستم", "⚿", UiTheme.Primary);
            _btnLogin.Dock = DockStyle.Top;
            _btnLogin.Height = ResponsiveLayout.Scale(48);
            _btnLogin.Font = UiTheme.FontBold(11.5F);
            _btnLogin.Click += BtnLogin_Click;
            _btnLogin.SizeChanged += delegate { UiTheme.RoundCorners(_btnLogin, ResponsiveLayout.Scale(12)); };

            Panel loginSpacer = new Panel { Dock = DockStyle.Top, Height = ResponsiveLayout.Scale(10), BackColor = Color.Transparent };

            _btnChangePass = UiTheme.CreateSecondaryButton("تغییر رمز عبور", "↻");
            _btnChangePass.Dock = DockStyle.Top;
            _btnChangePass.Height = ResponsiveLayout.Scale(42);
            _btnChangePass.Click += BtnChangePass_Click;
            _btnChangePass.SizeChanged += delegate { UiTheme.RoundCorners(_btnChangePass, ResponsiveLayout.Scale(12)); };

            Panel changeSpacer = new Panel { Dock = DockStyle.Top, Height = ResponsiveLayout.Scale(8), BackColor = Color.Transparent };

            // ترتیب افزودن معکوسِ نمایش است (هر Dock=Top بالای قبلی می‌نشیند).
            cardInner.Controls.Add(_btnChangePass);
            cardInner.Controls.Add(changeSpacer);
            cardInner.Controls.Add(_btnLogin);
            cardInner.Controls.Add(loginSpacer);
            cardInner.Controls.Add(boxPass);
            cardInner.Controls.Add(boxUser);
            cardInner.Controls.Add(boxCenter);
            cardInner.Controls.Add(subtitle);
            cardInner.Controls.Add(goldRuleHost);
            cardInner.Controls.Add(title);

            card.Controls.Add(cardInner);
            card.Controls.Add(_lblMessage);
            card.Controls.Add(cardFooter);
            cardHost.Controls.Add(card);

            // ── ستون چپ: پنل معرفی سیستم ──
            LoginHeroPanel hero = new LoginHeroPanel { Dock = DockStyle.Fill };

            body.Controls.Add(hero);
            body.Controls.Add(cardHost);

            // آموزش — ترتیب افزودن کافی است و BringToFront نباید صدا زده شود:
            // در چیدمان Dock، کنترل‌ها از «بالاترین ایندکس» به پایین پردازش
            // می‌شوند، یعنی کنترلی که دیرتر اضافه شده اول سهمش را می‌گیرد.
            // چون body (Fill) اول و titleBar (Top) بعد اضافه می‌شوند، نوار
            // عنوان اول ۴۶ پیکسل بالا را برمی‌دارد و بدنه بقیه را پر می‌کند.
            // در نسخه‌ی قبلی titleBar.BringToFront() آن را به ایندکس ۰ می‌برد،
            // یعنی «آخر» پردازش می‌شد و چیزی برایش نمی‌ماند — نوار عنوان کاملاً
            // ناپدید شده بود (در رندر واقعی دیده شد).
            Controls.Add(body);
            Controls.Add(titleBar);

            AcceptButton = _btnLogin;

            // ─── ترتیب حرکت با Tab ───────────────────────────────────────────
            // آموزش — در WinForms ترتیب Tab «سلسله‌مراتبی» است: اول بین
            // کانتینرها بر اساس TabIndex آن‌ها، بعد داخل هر کانتینر بین
            // فرزندانش. چون هر فیلد داخل یک FieldBox (کانتینر) نشسته، تنظیم
            // TabIndex فقط روی خودِ ورودی‌ها کافی نیست — TabIndex کانتینرها هم
            // باید به همان ترتیب باشد، وگرنه فوکوس بین کارت‌ها می‌پرد.
            // آموزش — تمام فرزندانِ کارت باید TabIndex صریح بگیرند، نه فقط
            // فیلدها. در تستِ واقعی معلوم شد اگر پنل‌های تزئینی (عنوان، خط
            // طلایی، زیرنویس، فاصله‌ها) روی مقدارِ پیش‌فرضِ صفر بمانند، با
            // فیلدِ «مرکز» (که آن هم صفر بود) تداخل می‌کنند و WinForms برای
            // رفعِ تساوی به ترتیبِ داخلیِ z برمی‌گردد — نتیجه این بود که
            // «مرکز» به‌جای اول، بعد از دکمه‌ها فوکوس می‌گرفت.
            title.TabIndex        = 0;
            goldRuleHost.TabIndex = 1;
            subtitle.TabIndex     = 2;
            boxCenter.TabIndex    = 3; _cmbCenter.TabIndex   = 0;
            boxUser.TabIndex      = 4; _txtUsername.TabIndex = 0;
            boxPass.TabIndex      = 5; _txtPassword.TabIndex = 0;
            loginSpacer.TabIndex  = 6;
            _btnLogin.TabIndex    = 7;
            changeSpacer.TabIndex = 8;
            _btnChangePass.TabIndex = 9;

            // دکمه‌ی نمایش رمز از چرخه‌ی Tab خارج است تا بین «رمز» و «ورود»
            // یک توقفِ اضافه ایجاد نکند (با ماوس کار می‌کند).
            _btnShowPass.TabStop = false;

            // بارگذاری مراکز پس از ساخت UI
            LoadCenters();

            // فوکوس اولیه روی نام کاربری (مرکز معمولاً از قبل انتخاب است).
            Shown += delegate
            {
                try { _txtUsername.Focus(); } catch { }
            };
        }

        // دکمه‌ی کوچکِ داخلِ یک فیلد (مثل چشمِ نمایش رمز) را کنارِ ورودی
        // می‌نشاند بدون اینکه ساختار FieldBox بشکند.
        private static void AttachInsideField(FieldBox box, Control button)
        {
            Control shell = box.Field.Parent;   // پوسته‌ی گردگوشه‌ی FieldBox
            if (shell == null) return;
            shell.Controls.Add(button);
            button.BringToFront();
        }

        private static string AppVersionText()
        {
            try
            {
                Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
            catch { return "1.1.0"; }
        }

        // اطلاعات پشتیبانی از تنظیمات مؤسسه خوانده می‌شود؛ اگر ثبت نشده باشد
        // یک متن عمومی نشان داده می‌شود (نه رشته‌ی خالی).
        private static string SupportLine()
        {
            try
            {
                string phone = SettingsHelper.Get(SettingsHelper.Phone);
                string org = SettingsHelper.Get(SettingsHelper.OrgName);
                if (!string.IsNullOrWhiteSpace(phone))
                    return "پشتیبانی: " + phone + (string.IsNullOrWhiteSpace(org) ? "" : "  ·  " + org);
                if (!string.IsNullOrWhiteSpace(org)) return org;
            }
            catch { }
            return "پشتیبانی فنی سامانه گنجینه";
        }

        // کارت سفیدِ گردگوشه با سایه‌ی نرم — پس‌زمینه‌ی ناحیه‌ی ورود.
        private class LoginCard : Panel
        {
            public LoginCard()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.Transparent;
            }

            // حافظه‌ی نهان — مثل پنل هنری، قابِ کارت هم ثابت است و نباید در هر
            // رسم از نو ساخته شود (چند لایه مسیرِ گردگوشه + سایه).
            private Bitmap _cache;
            private Size _cacheSize;

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (_cache != null) { _cache.Dispose(); _cache = null; }
                Invalidate();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _cache != null) { _cache.Dispose(); _cache = null; }
                base.Dispose(disposing);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 0 || Height <= 0) return;

                if (_cache == null || _cacheSize != Size)
                {
                    if (_cache != null) _cache.Dispose();
                    _cache = new Bitmap(Width, Height);
                    using (Graphics cg = Graphics.FromImage(_cache))
                        RenderFrame(cg);
                    _cacheSize = Size;
                }

                e.Graphics.DrawImageUnscaled(_cache, 0, 0);
                base.OnPaint(e);
            }

            private void RenderFrame(Graphics g)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                int radius = ResponsiveLayout.Scale(18);
                int shadow = ResponsiveLayout.Scale(7);

                // آموزش — WinForms سایه‌ی نرمِ بومی ندارد؛ با چند لایه‌ی نیمه‌شفافِ
                // تودرتو تقریب زده می‌شود (هرچه بیرونی‌تر، کم‌رنگ‌تر).
                for (int i = shadow; i > 0; i--)
                {
                    var r = new Rectangle(i, i + 1, Width - 1 - i * 2, Height - 2 - i * 2);
                    if (r.Width <= 0 || r.Height <= 0) continue;
                    using (var path = StatCard.RoundedRect(r, radius))
                    using (var pen = new Pen(Color.FromArgb(7, 0, 0, 0), 1f))
                        g.DrawPath(pen, path);
                }

                var body = new Rectangle(shadow, shadow, Width - 1 - shadow * 2, Height - 1 - shadow * 2);
                if (body.Width <= 0 || body.Height <= 0) return;

                using (var path = StatCard.RoundedRect(body, radius))
                {
                    using (var b = new SolidBrush(Color.White))
                        g.FillPath(b, path);
                    using (var p = new Pen(Color.FromArgb(30, 0, 0, 0), 1f))
                        g.DrawPath(p, path);
                }
            }
        }

        // ─── بومِ ناوی با گرادیانِ ملایم (پس‌زمینه‌ی کل پنجره) ────────────────
        // آموزش — گرادیان روی خودِ فرم رسم می‌شود، نه با یک PictureBox: هم
        // سبک‌تر است و هم هنگام تغییر اندازه‌ی پنجره خودکار کشیده می‌شود.
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) { base.OnPaintBackground(e); return; }

            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                       new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
                       CanvasTop, CanvasBottom, System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                e.Graphics.FillRectangle(brush, 0, 0, ClientSize.Width, ClientSize.Height);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate(); // گرادیان باید با اندازه‌ی جدید دوباره کشیده شود
        }

        // ─── بارگذاری مراکز از دیتابیس ──────────────────────────────────────
        private void LoadCenters()
        {
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT CenterID, CenterCode, CenterName
FROM   TblCenter
WHERE  IsActive = 1
ORDER  BY CenterCode", con))
                {
                    con.Open();
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            _cmbCenter.Items.Add(new CenterItem(
                                Convert.ToInt32(dr["CenterID"]),
                                dr["CenterCode"].ToString(),
                                dr["CenterName"].ToString()));
                        }
                    }
                }

                // SuperAdmin گزینه "همه مراکز" هم دارد — بعد از ورود موفق اضافه می‌شود
                if (_cmbCenter.Items.Count > 0)
                    _cmbCenter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _lblMessage.Text = "خطا در بارگذاری مراکز: " + ex.Message;
            }
        }

        // ─── بازگرداندن آخرین مرکز کاربر ────────────────────────────────────
        private int GetLastCenterId(int userId)
        {
            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT LastCenterID FROM TblUsers WHERE UserID = @UID", con))
                {
                    cmd.Parameters.AddWithValue("@UID", userId);
                    con.Open();
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value)
                        return Convert.ToInt32(val);
                }
            }
            catch { }
            return 0;
        }

        // ─── رفع باگ امنیتی: مرکز اختصاصی کاربر از سرور (نه از انتخاب کاربر
        // در ComboBox) خوانده می‌شود. کاربران غیر از SuperAdmin نباید بتوانند
        // با انتخاب گزینه‌ای دیگر در فرم ورود، وارد مرکز دیگری شوند —
        // اعتبارسنجی همیشه روی TblUsers.CenterID انجام می‌شود.
        private class AssignedCenter
        {
            public int    CenterId   = 0;
            public string CenterCode = "";
            public string CenterName = "";
        }

        private AssignedCenter GetAssignedCenter(int userId)
        {
            using (SQLiteConnection con = _db.GetConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT c.CenterID, c.CenterCode, c.CenterName
FROM   TblUsers u
JOIN   TblCenter c ON c.CenterID = u.CenterID
WHERE  u.UserID = @UID", con))
            {
                cmd.Parameters.AddWithValue("@UID", userId);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                        return new AssignedCenter
                        {
                            CenterId   = Convert.ToInt32(dr["CenterID"]),
                            CenterCode = dr["CenterCode"].ToString(),
                            CenterName = dr["CenterName"].ToString()
                        };
                }
            }
            return null;
        }

        // ─── ورود به سیستم ────────────────────────────────────────────────────
        private void BtnLogin_Click(object sender, EventArgs e)
        {
            _lblMessage.ForeColor = UiTheme.Danger;
            _lblMessage.Text      = "";

            // اعتبارسنجی مرکز
            if (_cmbCenter.SelectedItem == null)
            {
                _lblMessage.Text = "ابتدا مرکز را انتخاب کنید.";
                return;
            }

            string username = _txtUsername.Text.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                _lblMessage.Text = "نام کاربری را وارد کنید.";
                return;
            }

            int    userId;
            string role;
            bool   mustChange;

            // آموزش — محدودیت تلاش ورود ناموفق (Lockout): بعد از چند رمز اشتباه
            // پیاپی، حساب برای مدتی قفل می‌شود تا حدس زدن رمز (Brute Force)
            // عملاً بی‌فایده شود. با هر ورود موفق شمارنده صفر می‌شود. مقادیر
            // از تب «امنیت» تنظیمات قابل تغییرند (پیش‌فرض همان ۵/۱۵ قبلی).
            int MaxFailedAttempts = SettingsHelper.GetInt(SettingsHelper.MaxFailedAttempts, 5);
            int LockoutMinutes = SettingsHelper.GetInt(SettingsHelper.LockoutMinutes, 15);

            try
            {
                using (SQLiteConnection con = _db.GetConnection())
                {
                    con.Open();

                    int    dbUserId = 0;
                    byte[] passwordHash = null;
                    byte[] passwordSalt = null;
                    int    passwordIterations = 0;
                    int    failedCount = 0;
                    DateTime? lockoutUntil = null;

                    using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT UserID, Role, PasswordHash, PasswordSalt, PasswordIterations,
       MustChangePassword, FailedLoginCount, LockoutUntil,
       LastPasswordChangeAt, CreatedAt
FROM   TblUsers
WHERE  Username = @u AND IsActive = 1
LIMIT  1", con))
                    {
                        cmd.Parameters.AddWithValue("@u", username);

                        using (var dr = cmd.ExecuteReader())
                        {
                            if (!dr.Read())
                            {
                                _lblMessage.Text = "نام کاربری یا رمز عبور اشتباه است.";
                                return;
                            }

                            dbUserId = Convert.ToInt32(dr["UserID"]);
                            role = dr["Role"].ToString();
                            mustChange = Convert.ToInt32(dr["MustChangePassword"]) == 1;
                            passwordHash = (byte[])dr["PasswordHash"];
                            passwordSalt = (byte[])dr["PasswordSalt"];
                            passwordIterations = dr["PasswordIterations"] == DBNull.Value
                                ? 0 : Convert.ToInt32(dr["PasswordIterations"]);
                            failedCount = Convert.ToInt32(dr["FailedLoginCount"]);

                            // ─── اجبار تغییر دوره‌ای رمز (تب امنیت) ─────────────
                            // اگر مدت مشخصی گذشته و رمز عوض نشده، MustChangePassword
                            // اعمال می‌شود (۰ = خاموش = رفتار قبلی بدون تغییر).
                            int forceDays = SettingsHelper.GetInt(SettingsHelper.ForcePasswordChangeDays, 0);
                            if (!mustChange && forceDays > 0)
                            {
                                object refDateObj = dr["LastPasswordChangeAt"] != DBNull.Value
                                    ? dr["LastPasswordChangeAt"] : dr["CreatedAt"];
                                // آموزش — رفع باگ بحرانی «۳۲۶ میلیون دقیقه قفل»: DateTime.TryParse
                                // بدون فرهنگِ صریح از فرهنگِ نخِ جاری (که در کل برنامه روی تقویم
                                // شمسی تنظیم شده — نگاه کنید Program.cs) استفاده می‌کند؛ اما این
                                // مقدار در دیتابیس همیشه میلادی (Invariant) ذخیره شده. نتیجه: سال
                                // میلادی (مثلاً ۲۰۲۶) به‌اشتباه «سال شمسی» تفسیر می‌شد که معادل
                                // حدود سال ۲۶۴۷ میلادی است — تفاوتِ ~۶۲۱ سال، دقیقاً همان چیزی که
                                // به‌صورت «۳۲۶٬۷۳۴٬۵۱۳ دقیقه» نمایش داده می‌شد. PersianDateHelper.
                                // ParseStoredDate همیشه با InvariantCulture (میلادی) می‌خواند.
                                DateTime refDate = PersianDateHelper.ParseStoredDate(refDateObj, DateTime.Now);
                                if ((DateTime.Now - refDate).TotalDays >= forceDays)
                                    mustChange = true;
                            }

                            if (dr["LockoutUntil"] != DBNull.Value)
                            {
                                // توجه: از ParseStoredDate استفاده نمی‌شود چون آن تابع مخصوص
                                // فیلدهای «تاریخ» است و ساعت را به نیمه‌شب گرد می‌کند (dt.Date).
                                // LockoutUntil به دقتِ ساعت/دقیقه نیاز دارد وگرنه شمارشِ دقیقه‌های
                                // باقی‌مانده کاملاً غلط می‌شود؛ اینجا مستقیماً با InvariantCulture
                                // و بدون از دست دادن جزء ساعت پارس می‌کنیم.
                                DateTime parsed;
                                if (DateTime.TryParse(
                                        dr["LockoutUntil"].ToString(),
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out parsed))
                                    lockoutUntil = parsed;
                            }
                        }
                    }

                    // ─── بررسی قفل بودن حساب ─────────────────────────────────
                    if (lockoutUntil.HasValue && lockoutUntil.Value > DateTime.Now)
                    {
                        int minutesLeft = (int)Math.Ceiling((lockoutUntil.Value - DateTime.Now).TotalMinutes);
                        _lblMessage.Text = "حساب به‌دلیل تلاش‌های ناموفق پیاپی قفل شده. حدود " +
                            minutesLeft + " دقیقه دیگر دوباره امتحان کنید.";
                        return;
                    }

                    if (!PasswordHelper.Verify(_txtPassword.Text, passwordHash, passwordSalt, passwordIterations))
                    {
                        // ─── ثبت تلاش ناموفق + قفل در صورت رسیدن به سقف ──────
                        int newFailedCount = failedCount + 1;
                        bool shouldLock = newFailedCount >= MaxFailedAttempts;

                        using (SQLiteCommand updCmd = new SQLiteCommand(@"
UPDATE TblUsers
SET    FailedLoginCount = @fc,
       LockoutUntil = @lu
WHERE  UserID = @id", con))
                        {
                            updCmd.Parameters.AddWithValue("@fc", newFailedCount);
                            updCmd.Parameters.AddWithValue("@lu", shouldLock
                                ? (object)DateTime.Now.AddMinutes(LockoutMinutes).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                                : DBNull.Value);
                            updCmd.Parameters.AddWithValue("@id", dbUserId);
                            updCmd.ExecuteNonQuery();
                        }

                        _lblMessage.Text = shouldLock
                            ? "به‌دلیل تلاش‌های ناموفق پیاپی، حساب برای " + LockoutMinutes + " دقیقه قفل شد."
                            : "نام کاربری یا رمز عبور اشتباه است.";
                        return;
                    }

                    // ─── ورود موفق: شمارنده تلاش ناموفق صفر می‌شود ───────────
                    using (SQLiteCommand resetCmd = new SQLiteCommand(@"
UPDATE TblUsers
SET    FailedLoginCount = 0,
       LockoutUntil = NULL
WHERE  UserID = @id", con))
                    {
                        resetCmd.Parameters.AddWithValue("@id", dbUserId);
                        resetCmd.ExecuteNonQuery();
                    }

                    userId = dbUserId;
                }
            }
            catch (Exception ex)
            {
                _lblMessage.Text = "خطا در اتصال به دیتابیس: " + ex.Message;
                return;
            }

            // ─── اجبار تغییر رمز اولیه ────────────────────────────────────
            if (mustChange)
            {
                _lblMessage.ForeColor = UiTheme.Warning;
                _lblMessage.Text      = "برای ادامه باید رمز اولیه را تغییر دهید.";

                using (FrmChangePassword frm = new FrmChangePassword(username, forced: true))
                {
                    if (frm.ShowDialog(this) != DialogResult.OK)
                    {
                        _lblMessage.ForeColor = UiTheme.Danger;
                        _lblMessage.Text      = "برای ورود باید رمز اولیه تغییر کند.";
                        return;
                    }
                }
            }

            // ─── ورود موفق — SignIn ثبت‌نام در SecurityContext ─────────────
            SecurityContext.SignIn(userId, username, role);

            if (SecurityContext.IsSuperAdmin())
            {
                // اگر SuperAdmin است، گزینه "همه مراکز" به ComboBox اضافه می‌شود
                // و مرکز انتخابی خودِ کاربر در فرم ورود معتبر است.
                if (!(_cmbCenter.Items[0] is AllCentersItem))
                    _cmbCenter.Items.Insert(0, new AllCentersItem());

                int lastCenterId = GetLastCenterId(userId);
                if (lastCenterId > 0)
                {
                    for (int i = 0; i < _cmbCenter.Items.Count; i++)
                    {
                        CenterItem ci = _cmbCenter.Items[i] as CenterItem;
                        if (ci != null && ci.CenterId == lastCenterId)
                        {
                            _cmbCenter.SelectedIndex = i;
                            break;
                        }
                    }
                }

                ApplySelectedCenter();
            }
            else
            {
                // رفع باگ امنیتی: برای کاربر غیر از SuperAdmin، هرچه در
                // ComboBox انتخاب شده باشد نادیده گرفته می‌شود؛ مرکز فقط از
                // TblUsers.CenterID (تنظیم‌شده توسط مدیر) خوانده می‌شود.
                AssignedCenter assigned = GetAssignedCenter(userId);
                if (assigned == null)
                {
                    _lblMessage.ForeColor = UiTheme.Danger;
                    _lblMessage.Text = "مرکز کاربر تنظیم نشده است. با مدیر سیستم تماس بگیرید.";
                    return;
                }

                SecurityContext.SelectCenter(assigned.CenterId, assigned.CenterCode, assigned.CenterName);
            }

            AuditLogger.Log("ورود", "TblUsers", userId, "", username + " / " + SecurityContext.CenterDisplay);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ApplySelectedCenter()
        {
            if (_cmbCenter.SelectedItem is AllCentersItem)
            {
                SecurityContext.SelectCenter(0, "", "همه مراکز", allCenters: true);
                return;
            }

            CenterItem ci = _cmbCenter.SelectedItem as CenterItem;
            if (ci != null)
                SecurityContext.SelectCenter(ci.CenterId, ci.CenterCode, ci.CenterName);
        }

        // ─── تغییر ارادی رمز (از صفحه Login) ────────────────────────────────
        private void BtnChangePass_Click(object sender, EventArgs e)
        {
            string username = _txtUsername.Text.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                _lblMessage.ForeColor = UiTheme.Danger;
                _lblMessage.Text      = "ابتدا نام کاربری را وارد کنید.";
                return;
            }

            using (FrmChangePassword frm = new FrmChangePassword(username, forced: false))
            {
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    _lblMessage.ForeColor = UiTheme.Success;
                    _lblMessage.Text      = "رمز با موفقیت تغییر کرد.";
                }
            }
        }

        // ─── کلاس‌های کمکی برای آیتم‌های ComboBox مرکز ────────────────────────
        private class CenterItem
        {
            public int    CenterId   { get; }
            public string CenterCode { get; }
            public string CenterName { get; }

            public CenterItem(int id, string code, string name)
            {
                CenterId   = id;
                CenterCode = code;
                CenterName = name;
            }

            public override string ToString() { return CenterCode + " - " + CenterName; }
        }

        private class AllCentersItem
        {
            public override string ToString() { return "★  همه مراکز"; }
        }
    }
}

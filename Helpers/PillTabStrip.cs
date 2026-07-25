using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // نوار ناوبری «دکمه‌های بیضی‌شکل» (Pill Tabs) — جایگزین ظاهریِ TabControl
    // بومیِ ویندوز، طبق طرح تصویریِ درخواستی کاربر. این کنترل هیچ منطقی از
    // صفحات را نگه نمی‌دارد؛ فقط نمایش/انتخاب تب فعال است — نگاه کنید
    // FrmSettings.BuildUi برای نحوه‌ی هماهنگ‌شدنش با صفحاتِ موجود.
    //
    // آموزش — الگوی RTL این پروژه: FlowDirection=LeftToRight همراه با
    // RightToLeft=Yes ارثی، دقیقاً یک‌بار آینه می‌شود و ترتیب از راست شروع
    // می‌گردد (همان الگویی که در نوار ابزار داشبورد/دکمه‌های جستجوی پیشرفته
    // استفاده شده)؛ اگر اینجا هم RightToLeft روی خودِ Flow گذاشته شود، دو
    // آینه یکدیگر را خنثی می‌کنند و ترتیب دوباره چپ‌به‌راست می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public class PillTabStrip : Panel
    {
        private readonly FlowLayoutPanel _flow;
        private readonly List<Button> _buttons = new List<Button>();
        private int _selectedIndex = -1;

        public event EventHandler SelectedIndexChanged;

        // ارتفاع هر دکمه و فاصله‌ها — ثابت و از پیش معلوم، تا ارتفاعِ لازمِ نوار
        // دقیقاً محاسبه‌شدنی باشد (نه حدسی).
        private const int PillHeight = 32;
        private const int PillMarginV = 3;
        private const int RowHeight = PillHeight + PillMarginV * 2;

        public PillTabStrip()
        {
            Dock = DockStyle.Top;
            BackColor = UiTheme.CardBack;
            Padding = new Padding(10, 8, 10, 8);
            RightToLeft = RightToLeft.Yes;

            // آموزش — رفع باگ «تب‌ها از بالا بریده می‌شدند و بعضی‌ها دیده نمی‌شدند»:
            // نسخه‌ی قبلی WrapContents=false + AutoScroll=true داشت؛ ۱۱ تب در عرض
            // پنجره جا نمی‌شد، پس یک اسکرول‌بار افقی اضافه می‌شد که ارتفاع مفید را
            // می‌خورد و دکمه‌ها (که AutoSize بودند و ارتفاعشان از فونت محاسبه
            // می‌شد) از بالا و پایین بریده می‌شدند. حالا تب‌ها به خط بعد می‌روند
            // (WrapContents=true) و اسکرول کاملاً حذف شده — همه‌ی تب‌ها همیشه دیده
            // می‌شوند و ارتفاع نوار خودش را با تعداد ردیف‌ها تطبیق می‌دهد.
            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false
            };
            Controls.Add(_flow);

            // خط نازک جداکننده زیر نوار (شبیه طرح تصویری).
            Panel divider = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = UiTheme.Border };
            Controls.Add(divider);

            Height = RowHeight + Padding.Vertical + 1;
            _flow.Layout += delegate { AdjustHeight(); };
        }

        // آموزش — چرا اینجا آیکون نداریم: فونت فارسیِ برنامه (Vazirmatn/Tahoma)
        // گلیفِ ایموجی ندارد و کاراکترهای ایموجی به‌صورت مربعِ خالی «▯» رندر
        // می‌شدند (در تستِ تصویری دیده شد). یک Button فقط یک فونت می‌پذیرد، پس
        // نمی‌شود متن فارسی و ایموجی را با دو فونت در یک دکمه ترکیب کرد. در
        // عوض، آیکون‌ها آن‌جا که کار می‌کنند (سربرگ کارت‌ها، با فونت صریحِ
        // Segoe UI Emoji) حفظ شده‌اند.
        public int AddTab(string text)
        {
            string caption = text;
            Font font = UiTheme.FontBold(UiTheme.SizeSmall);

            // آموزش — عرض دقیقاً به اندازه‌ی متن اندازه‌گیری می‌شود (نه AutoSize):
            // با AutoSize، ارتفاع هم خودکار از فونت محاسبه می‌شد و کنترلِ ارتفاعِ
            // ثابت را از دست می‌دادیم (ریشه‌ی بریدگیِ بالای دکمه‌ها).
            int textWidth = TextRenderer.MeasureText(caption, font).Width;

            Button btn = new Button
            {
                Text = caption,
                FlatStyle = FlatStyle.Flat,
                Font = font,
                AutoSize = false,
                Size = new Size(textWidth + 26, PillHeight),
                Margin = new Padding(3, PillMarginV, 3, PillMarginV),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 0;
            UiTheme.RoundCorners(btn, PillHeight);

            int index = _buttons.Count;
            btn.Click += delegate { SelectedIndex = index; };

            _buttons.Add(btn);
            _flow.Controls.Add(btn);
            ApplyStyle(btn, false);
            return index;
        }

        // ارتفاع نوار = تعداد ردیف‌هایی که تب‌ها واقعاً اشغال کرده‌اند. با تغییر
        // عرض پنجره/تعداد تب‌ها خودکار تنظیم می‌شود، پس هیچ تبی پنهان نمی‌ماند.
        private void AdjustHeight()
        {
            if (_buttons.Count == 0) return;

            int maxBottom = 0;
            foreach (Button b in _buttons)
                if (b.Bottom > maxBottom) maxBottom = b.Bottom;

            int desired = maxBottom + PillMarginV + Padding.Vertical + 1;
            if (Height != desired) Height = desired;
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value < 0 || value >= _buttons.Count || value == _selectedIndex) return;

                _selectedIndex = value;
                for (int i = 0; i < _buttons.Count; i++)
                    ApplyStyle(_buttons[i], i == _selectedIndex);

                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        private void ApplyStyle(Button btn, bool active)
        {
            if (active)
            {
                btn.BackColor = UiTheme.Primary;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.MouseOverBackColor = UiTheme.Primary;
            }
            else
            {
                btn.BackColor = UiTheme.CardBack;
                btn.ForeColor = UiTheme.TextMuted;
                btn.FlatAppearance.MouseOverBackColor = UiTheme.HoverTint;
            }
        }
    }
}

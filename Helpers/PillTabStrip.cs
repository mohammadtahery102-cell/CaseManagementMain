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

        public PillTabStrip()
        {
            Dock = DockStyle.Top;
            Height = 58;
            BackColor = UiTheme.CardBack;
            Padding = new Padding(12, 10, 12, 10);
            RightToLeft = RightToLeft.Yes;

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true
            };
            Controls.Add(_flow);

            // خط نازک جداکننده زیر نوار (شبیه طرح تصویری).
            Panel divider = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = UiTheme.Border };
            Controls.Add(divider);
        }

        public int AddTab(string icon, string text)
        {
            Button btn = new Button
            {
                Text = string.IsNullOrEmpty(icon) ? text : (icon + "  " + text),
                FlatStyle = FlatStyle.Flat,
                Font = UiTheme.FontBold(UiTheme.SizeBody),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 36,
                Padding = new Padding(16, 0, 16, 0),
                Margin = new Padding(3, 2, 3, 2),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 0;

            int index = _buttons.Count;
            btn.Click += delegate { SelectedIndex = index; };
            btn.SizeChanged += delegate { UiTheme.RoundCorners(btn, btn.Height); };

            _buttons.Add(btn);
            _flow.Controls.Add(btn);
            ApplyStyle(btn, false);
            return index;
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

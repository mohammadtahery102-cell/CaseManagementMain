using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.GuardianCardIntegration.CardDesigner
{
    // ─────────────────────────────────────────────────────────────────────────
    // نوارِ ناوبریِ بالای طراحِ کارت — جایگزینِ TabControl قبلی.
    //
    // چرا یک کنترلِ جدا و نه TabControl؟ چون TabControl در RTL برچسب‌های
    // فارسیِ بلند را می‌بُرد و ترتیبِ تب‌ها را چپ‌به‌راست می‌چیند. اینجا هر
    // آیتم یک دکمهٔ Dock=Right است، پس اولین آیتمِ اضافه‌شده راست‌ترین است —
    // یعنی ترتیبِ خواندنِ فارسی، بدونِ هیچ ترفندِ اضافه.
    //
    // این کنترل هیچ داده‌ای از قالب نمی‌داند؛ فقط ایندکسِ انتخاب‌شده را
    // اعلام می‌کند (SelectedIndexChanged) و خودِ فرم تصمیم می‌گیرد کدام
    // پنل را نشان دهد. نگاه کنید FrmCardTemplateManager.BuildSettingsTabs.
    // ─────────────────────────────────────────────────────────────────────────
    public class DesignerNav : Panel
    {
        private readonly List<Button> _items = new List<Button>();
        private readonly List<Panel> _underlines = new List<Panel>();
        private int _selectedIndex = -1;

        public event EventHandler SelectedIndexChanged;

        public DesignerNav()
        {
            Dock = DockStyle.Top;
            Height = 44;
            BackColor = UiTheme.CardBack;
            RightToLeft = RightToLeft.Yes;

            Panel bottomBorder = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = UiTheme.Border };
            Controls.Add(bottomBorder);
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { Select(value); }
        }

        // آموزش — هر آیتم یک دکمهٔ تخت با زیرخطِ رنگی است. عرضِ ثابت نداریم:
        // از روی متن محاسبه می‌شود تا برچسبِ فارسی هیچ‌وقت بریده نشود (همان
        // مشکلی که TabControl داشت).
        public void AddItem(string text)
        {
            int index = _items.Count;

            Button btn = new Button
            {
                Text = text,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.CardBack,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.Font(10F),
                Cursor = Cursors.Hand,
                TabStop = true,
                RightToLeft = RightToLeft.Yes
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = UiTheme.HoverTint;

            using (Graphics g = CreateGraphics())
            {
                SizeF size = g.MeasureString(text, btn.Font);
                // ۴۴ پیکسل فاصلهٔ دو طرف — هدفِ «large click target» در
                // مشخصاتِ UX (حداقل ۴۴px برای آیتم‌های ناوبری).
                btn.Width = (int)Math.Ceiling(size.Width) + 44;
            }

            Panel underline = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 3,
                BackColor = Color.Transparent
            };
            btn.Controls.Add(underline);

            btn.Click += delegate { Select(index); };

            _items.Add(btn);
            _underlines.Add(underline);
            Controls.Add(btn);
            btn.BringToFront();

            if (_selectedIndex < 0) Select(0);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            if (_selectedIndex == index) return;

            _selectedIndex = index;

            for (int i = 0; i < _items.Count; i++)
            {
                bool on = (i == index);
                _items[i].ForeColor = on ? UiTheme.Primary : UiTheme.TextMuted;
                _items[i].Font = on ? UiTheme.FontBold(10F) : UiTheme.Font(10F);
                _underlines[i].BackColor = on ? UiTheme.Primary : Color.Transparent;
            }

            EventHandler handler = SelectedIndexChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}

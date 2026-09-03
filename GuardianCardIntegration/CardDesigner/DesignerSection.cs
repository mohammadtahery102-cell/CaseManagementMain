using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.GuardianCardIntegration.CardDesigner
{
    // ─────────────────────────────────────────────────────────────────────────
    // میزبانِ یک «بخش» از طراحِ کارت (کارت / ظاهر / محتوا / پشت کارت /
    // تاریخچه). هر بخش از چند «گروه» تشکیل می‌شود و هر گروه یک SectionCard
    // با سربرگِ یکسان است — تا فاصله‌ها/سربرگ‌ها در هر پنج بخش دقیقاً یکی
    // باشند (الزامِ «Consistent section headers» در مشخصاتِ UX).
    //
    // حالتِ ساده/پیشرفته: گروه‌ها و ردیف‌هایی که Advanced علامت خورده‌اند در
    // حالتِ ساده پنهان می‌شوند. نکتهٔ مهمِ مشخصات: پنهان‌شدن یعنی «نمایش داده
    // نشود»، نه «جابه‌جا شود» — هیچ کنترلی بینِ دو حالت مکانش عوض نمی‌شود،
    // پس کاربر چیزی را که یاد گرفته دوباره یاد نمی‌گیرد.
    // ─────────────────────────────────────────────────────────────────────────
    public class DesignerSection : Panel
    {
        // گروه‌های کاملاً پیشرفته (کلِ کارت در حالتِ ساده پنهان است)
        private readonly List<Control> _advancedGroups = new List<Control>();
        // ردیف‌های پیشرفته درونِ یک گروهِ ساده (فقط همان ردیف‌ها پنهان می‌شوند)
        private readonly List<Control> _advancedRows = new List<Control>();

        private bool _advancedVisible;

        public DesignerSection()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = UiTheme.Background;
            Padding = new Padding(8);
            RightToLeft = RightToLeft.Yes;
            Visible = false;
        }

        public bool AdvancedVisible
        {
            get { return _advancedVisible; }
        }

        // آموزش — گروه‌ها با Dock=Top چیده می‌شوند، پس ترتیبِ نمایش
        // برعکسِ ترتیبِ افزودن است. برای این‌که فراخوان بتواند به ترتیبِ
        // طبیعی بنویسد، هر گروه بعد از افزودن SendToBack می‌شود.
        public Panel AddGroup(string title, bool advanced = false)
        {
            SectionCard card = new SectionCard
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 12)
            };

            Panel content = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                RightToLeft = RightToLeft.Yes
            };

            // آموزش — چیدمانِ Dock=Top در WinForms بر پایهٔ z-order است و
            // «بالاترین ایندکس = بالاترین جایگاه». پس برای اینکه سربرگ بالای
            // محتوا بنشیند، باید *بعد از* محتوا اضافه شود (ایندکسِ بالاتر) —
            // نه با BringToFront که ایندکس را صفر و آن را به پایین می‌بَرد.
            card.Controls.Add(content);

            if (!string.IsNullOrEmpty(title))
            {
                Panel headerWrap = new Panel { Dock = DockStyle.Top, Height = 34 };
                Label lbl = new Label
                {
                    Text = title,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = UiTheme.FontBold(UiTheme.SizeMedium),
                    ForeColor = UiTheme.TextDark
                };
                Panel rule = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = UiTheme.Border };
                headerWrap.Controls.Add(lbl);
                headerWrap.Controls.Add(rule);
                card.Controls.Add(headerWrap);
            }

            // همان قاعده برای خودِ گروه‌ها: گروهی که زودتر اضافه شده باید
            // بالاتر بماند، پس هر گروهِ تازه به ایندکسِ پایین‌تر می‌رود.
            Controls.Add(card);
            card.BringToFront();

            if (advanced)
            {
                _advancedGroups.Add(card);
                card.Visible = _advancedVisible;
            }

            return content;
        }

        // جداکنندهٔ «پیشرفته» درونِ یک گروهِ ساده — ظاهرِ یکسان در همهٔ
        // بخش‌ها (الزامِ مشخصات: «Always ── پیشرفته ── rule + chevron»).
        public Panel AddAdvancedDivider(Panel groupContent)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 30 };

            Label lbl = new Label
            {
                Text = "پیشرفته",
                Dock = DockStyle.Right,
                Width = 70,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(9F),
                ForeColor = UiTheme.TextMuted
            };
            Panel rule = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 14, 8, 0) };
            Panel line = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiTheme.Border };
            rule.Controls.Add(line);

            row.Controls.Add(rule);
            row.Controls.Add(lbl);
            groupContent.Controls.Add(row);

            MarkAdvanced(row);
            return row;
        }

        // یک ردیفِ موجود را «پیشرفته» علامت می‌زند تا در حالتِ ساده پنهان شود.
        public void MarkAdvanced(Control row)
        {
            if (row == null) return;
            _advancedRows.Add(row);
            row.Visible = _advancedVisible;
        }

        public void SetAdvancedVisible(bool visible)
        {
            _advancedVisible = visible;

            SuspendLayout();
            for (int i = 0; i < _advancedGroups.Count; i++)
                _advancedGroups[i].Visible = visible;
            for (int i = 0; i < _advancedRows.Count; i++)
                _advancedRows[i].Visible = visible;
            ResumeLayout(true);
        }

        // شمارشِ تنظیماتِ پیشرفته‌ای که مقدارِ غیرپیش‌فرض دارند — برای
        // نشانِ «ⓘ N تنظیم پیشرفته فعال است» در حالتِ ساده. خودِ شمارش را
        // فرم انجام می‌دهد (چون مقدارِ پیش‌فرض را فقط او می‌داند)؛ اینجا
        // فقط تعدادِ ردیف‌های پیشرفته برای بررسیِ سلامتِ چیدمان است.
        public int AdvancedRowCount
        {
            get { return _advancedGroups.Count + _advancedRows.Count; }
        }
    }
}

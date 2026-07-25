using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // «فیلد» استانداردِ فرم‌ها طبق طرح مرجع: برچسبِ بالا + ورودیِ گردگوشه با
    // حالت‌های Focus/Hover/خطا.
    //
    // آموزش — چرا کانتینر و نه استایل مستقیم روی خودِ TextBox: کنترل‌های بومیِ
    // ویندوز (TextBox/ComboBox) گوشه‌ی گرد و حاشیه‌ی سفارشی نمی‌پذیرند. راه‌حل
    // استانداردِ دسکتاپ این است که ورودیِ بدون‌حاشیه داخل یک پنلِ گردگوشه
    // بنشیند و پنل، حاشیه/فوکوس را خودش رسم کند.
    //
    // بسیار مهم — این کلاس هیچ ارجاعی را نمی‌شکند: خودِ کنترلِ ورودی همان
    // شیء قبلی می‌ماند (فقط والدش عوض می‌شود)، پس نام کنترل، رویدادها،
    // DataBinding و هر کدی که با نامش کار می‌کند دقیقاً مثل قبل کار می‌کند.
    // ─────────────────────────────────────────────────────────────────────────
    public class FieldBox : Panel
    {
        public const int LabelHeight = 20;
        public const int InputHeight = 38;
        public const int TotalHeight = LabelHeight + InputHeight + 10; // + فاصله‌ی پایین

        private readonly InputShell _shell;

        public Control Field { get; private set; }
        public Label Caption { get; private set; }

        public FieldBox(Label captionLabel, string captionText, Control field)
        {
            Field = field;
            Caption = captionLabel;

            BackColor = Color.Transparent;
            Margin = new Padding(6, 4, 6, 4);
            Height = TotalHeight;

            _shell = new InputShell(field) { Dock = DockStyle.Top, Height = InputHeight };

            captionLabel.Text = captionText;
            captionLabel.AutoSize = false;
            captionLabel.Dock = DockStyle.Top;
            captionLabel.Height = LabelHeight;
            captionLabel.TextAlign = ContentAlignment.MiddleRight;
            captionLabel.Font = UiTheme.FontBold(UiTheme.SizeSmall - 0.5F);
            captionLabel.ForeColor = UiTheme.TextDark;
            captionLabel.BackColor = Color.Transparent;
            captionLabel.Padding = new Padding(0, 0, 2, 3);

            Controls.Add(_shell);
            Controls.Add(captionLabel);
        }

        // حالت خطا (Validation) — قاب قرمز می‌شود.
        public bool HasError
        {
            get { return _shell.HasError; }
            set { _shell.HasError = value; }
        }

        // ─── وسط‌چین‌کردن برچسب و متنِ ورودی ─────────────────────────────────
        // برای فرم‌هایی مثل صفحه‌ی ورود که فیلدها در یک ستونِ باریکِ وسط‌چین
        // قرار دارند و راست‌چینی در آن‌ها نامتوازن دیده می‌شود.
        public void CenterContent()
        {
            Caption.TextAlign = ContentAlignment.MiddleCenter;
            Caption.Padding = new Padding(0, 0, 0, 3);
            _shell.CenterText = true;
        }

        // پوسته‌ی گردگوشه‌ی دورِ ورودی.
        private class InputShell : Panel
        {
            private readonly Control _inner;
            private bool _focused;
            private bool _hover;
            private bool _hasError;

            public InputShell(Control inner)
            {
                _inner = inner;

                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
                Padding = new Padding(10, 0, 10, 0);

                // ورودی بدون حاشیه، تا قابِ پنل تنها حاشیه‌ی دیده‌شده باشد.
                TextBox tb = inner as TextBox;
                if (tb != null)
                {
                    tb.BorderStyle = BorderStyle.None;
                    tb.BackColor = Color.White;

                    // آموزش — رفع باگی که کاربر در فرم پرونده دید: «متن از چپ
                    // شروع می‌شود». علت همان آینه‌شدن است، این‌بار داخل خودِ
                    // TextBox: وقتی RightToLeft=Yes باشد، HorizontalAlignment
                    // هم آینه می‌شود و Right بصراً «چپ» رندر می‌کند. برای
                    // شروعِ متن از راست (رفتار درستِ فارسی) باید Left داد.
                    // فیلدهای رمز استثنا هستند (پایین‌تر RightToLeft=No
                    // می‌گیرند)، پس آن‌ها همان Center واقعی را نگه می‌دارند.
                    tb.TextAlign = HorizontalAlignment.Left;
                }
                ComboBox cb = inner as ComboBox;
                if (cb != null)
                {
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.BackColor = Color.White;

                    // آموزش — رفع باگ «نام مرکز با نوار آبی روی متن»: در حالت
                    // DropDownList، ویندوز آیتمِ انتخاب‌شده را با رنگِ هایلایتِ
                    // سیستم (آبیِ پررنگ) پر می‌کند و متن سفید می‌شود؛ داخل قابِ
                    // سفیدِ ما این کاملاً ناجور و ناخوانا دیده می‌شد.
                    // با رسمِ دستیِ آیتم، پس‌زمینه سفید و متن تیره می‌ماند و
                    // فقط هنگام بازبودن فهرست، آیتمِ زیر ماوس ته‌رنگ می‌گیرد.
                    cb.DrawMode = DrawMode.OwnerDrawFixed;
                    cb.ItemHeight = Math.Max(18, cb.ItemHeight);
                    cb.DrawItem += ComboDrawItem;   // متد نمونه‌ای، تا CenterText را ببیند
                }

                inner.Font = UiTheme.Font(UiTheme.SizeBody);

                // آموزش — فیلدهای رمز عبور استثنا هستند: محتوایشان متنِ فارسی
                // نیست و با RightToLeft=Yes، مکان‌نما و کاراکترهای ماسک هنگام
                // تایپ جابه‌جا و «کج» دیده می‌شوند (کاربر همین را گزارش کرد).
                bool isPassword = tb != null && (tb.UseSystemPasswordChar || tb.PasswordChar != '\0');
                inner.RightToLeft = isPassword ? RightToLeft.No : RightToLeft.Yes;

                // ورودی‌های تک‌خطی به‌صورت عمودی وسط‌چین می‌نشینند؛ چندخطی‌ها پر می‌کنند.
                bool multiline = tb != null && tb.Multiline;
                inner.Dock = multiline ? DockStyle.Fill : DockStyle.None;
                Controls.Add(inner);

                if (!multiline)
                {
                    Resize += delegate { CenterInner(); };
                    CenterInner();
                }

                inner.Enter += delegate { _focused = true; Invalidate(); };
                inner.Leave += delegate { _focused = false; Invalidate(); };
                inner.MouseEnter += delegate { _hover = true; Invalidate(); };
                inner.MouseLeave += delegate { _hover = false; Invalidate(); };
                MouseEnter += delegate { _hover = true; Invalidate(); };
                MouseLeave += delegate { _hover = false; Invalidate(); };
                // کلیک روی هرجای قاب، فوکوس را به خودِ ورودی می‌دهد.
                Click += delegate { try { inner.Focus(); } catch { } };
            }

            // وقتی true باشد، متنِ ورودی وسط‌چین می‌شود (نه راست‌چین).
            private bool _centerText;
            public bool CenterText
            {
                get { return _centerText; }
                set
                {
                    _centerText = value;
                    TextBox tb = _inner as TextBox;
                    // حالت غیرِ وسط‌چین باید Left باشد، نه Right: زیر
                    // RightToLeft=Yes آینه می‌شود و Left بصراً «راست» می‌دهد
                    // (توضیح کامل در سازنده‌ی InputShell).
                    if (tb != null) tb.TextAlign = value ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                    Invalidate();
                }
            }

            // رسم دستیِ آیتمِ ComboBox — بدون هایلایتِ آبیِ سیستم.
            private void ComboDrawItem(object sender, DrawItemEventArgs e)
            {
                ComboBox cb = sender as ComboBox;
                if (cb == null) return;

                // ناحیه‌ی بسته‌ی کمبو (متنِ انتخاب‌شده) هرگز هایلایت نمی‌گیرد؛
                // فقط آیتم‌های داخلِ فهرستِ بازشده حالت Hover دارند.
                bool inDropDown = (e.State & DrawItemState.ComboBoxEdit) != DrawItemState.ComboBoxEdit;
                bool hot = inDropDown && (e.State & DrawItemState.Selected) == DrawItemState.Selected;

                using (Brush back = new SolidBrush(hot ? UiTheme.HoverTint : Color.White))
                    e.Graphics.FillRectangle(back, e.Bounds);

                if (e.Index >= 0 && e.Index < cb.Items.Count)
                {
                    string text = cb.GetItemText(cb.Items[e.Index]);
                    // آموزش — با پرچمِ RightToLeft، ترازِ افقی هم آینه می‌شود؛
                    // پس برای «راستِ بصری» باید Left داده شود (هم‌راستا با
                    // همان قاعده‌ای که برای TextBox توضیح داده شد).
                    TextFormatFlags align = _centerText
                        ? TextFormatFlags.HorizontalCenter
                        : TextFormatFlags.Left;
                    TextRenderer.DrawText(
                        e.Graphics, text, cb.Font, e.Bounds, UiTheme.TextDark,
                        align | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
                }
            }

            private void CenterInner()
            {
                if (_inner == null) return;

                // آموزش — عرضِ هر کنترلِ Dock‌شده‌ی دیگری که داخل همین پوسته
                // نشسته (مثل دکمه‌ی «نمایش رمز») باید از عرضِ ورودی کم شود،
                // وگرنه ورودی زیرِ آن دکمه ادامه پیدا می‌کند و متن پشتش پنهان
                // می‌ماند. (تست مقیاس صفحه‌ی ورود همین سرریز را نشان داد.)
                int reserved = 0;
                foreach (Control c in Controls)
                    if (c != _inner && c.Visible && c.Dock != DockStyle.None)
                        reserved += c.Width;

                int w = Math.Max(0, ClientSize.Width - Padding.Horizontal - reserved);
                int h = _inner.PreferredSize.Height;
                if (h <= 0 || h > ClientSize.Height) h = Math.Min(ClientSize.Height - 6, 22);
                _inner.SetBounds(Padding.Left, Math.Max(0, (ClientSize.Height - h) / 2), w, h);
            }

            public bool HasError
            {
                get { return _hasError; }
                set { if (_hasError == value) return; _hasError = value; Invalidate(); }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = StatCard.RoundedRect(rect, 10))
                {
                    using (Brush fill = new SolidBrush(Enabled ? Color.White : UiTheme.Background))
                        g.FillPath(fill, path);

                    Color border =
                        _hasError ? UiTheme.Danger :
                        _focused  ? UiTheme.Primary :
                        _hover    ? ControlPaint.Dark(UiTheme.Border, 0.10f) :
                                    UiTheme.Border;

                    using (Pen pen = new Pen(border, _focused || _hasError ? 1.6f : 1f))
                        g.DrawPath(pen, path);
                }

                base.OnPaint(e);
            }
        }
    }
}

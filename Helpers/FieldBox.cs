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
                    tb.TextAlign = HorizontalAlignment.Right;
                }
                ComboBox cb = inner as ComboBox;
                if (cb != null)
                {
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.BackColor = Color.White;
                }

                inner.Font = UiTheme.Font(UiTheme.SizeBody);
                inner.RightToLeft = RightToLeft.Yes;

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

            private void CenterInner()
            {
                if (_inner == null) return;
                int w = Math.Max(0, ClientSize.Width - Padding.Horizontal);
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

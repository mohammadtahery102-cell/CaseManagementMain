using System;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.GuardianCardIntegration.CardDesigner
{
    // ─────────────────────────────────────────────────────────────────────────
    // «کارتِ یک مورد» — همهٔ تنظیماتِ یک فیلد در یک جا (سندِ UX، W3b):
    // نمایش/عدم‌نمایش، جای آن در ترتیب، رنگ/اندازه/قلم/ضخامت/چینش/فاصلهٔ‌خط،
    // متنِ دلخواه (اگر مجاز باشد) و منبعِ داده‌اش.
    //
    // پیش از این، همین تنظیمات بینِ سه تبِ جدا پخش بود (نمایش / ترتیب /
    // متن‌ها) و کاربر باید برای یک فیلد سه‌جا سر می‌زد.
    //
    // نکتهٔ کارایی: کنترل‌های داخلِ بازشو **تنبل** ساخته می‌شوند — تا وقتی
    // کاربر ⚙ را نزده، هیچ کنترلی برای آن بخش ساخته نمی‌شود. با ۲۳ مورد،
    // ساختنِ همه از ابتدا حدود ۲۰۰ کنترل می‌شد و بازشدنِ فرم را کند می‌کرد.
    //
    // این کنترل هیچ‌چیز دربارهٔ فرم نمی‌داند: تغییرها را با رویداد اعلام
    // می‌کند و خودِ فرم آن‌ها را روی _chkFields/_lstFieldOrder/_textOverrides
    // می‌نشاند — یعنی مسیرِ ذخیره‌سازیِ موجود دست‌نخورده می‌ماند.
    // ─────────────────────────────────────────────────────────────────────────
    public class FieldCard : Panel
    {
        private readonly CardFieldInfo _info;
        private readonly TextFieldOverride _ov;

        private readonly CheckBox _chkVisible;
        private readonly Button _btnExpand;
        private readonly Button _btnUp;
        private readonly Button _btnDown;
        private readonly Label _lblOrder;
        private Panel _editor;          // تنبل
        private bool _expanded;
        private bool _suppress;

        public event EventHandler VisibleToggled;
        public event EventHandler OverrideChanged;
        public event EventHandler MoveUp;
        public event EventHandler MoveDown;

        public string Key { get { return _info.Key; } }
        public CardFieldInfo Info { get { return _info; } }

        // آموزش — فرم فقط وقتی این آبجکت را داخلِ _textOverrides می‌گذارد که
        // کاربر واقعاً چیزی را عوض کرده باشد؛ وگرنه DesignJson هر قالب با
        // ۲۳ ورودیِ خالی باد می‌کرد.
        public TextFieldOverride Override { get { return _ov; } }

        public bool FieldVisible
        {
            get { return _chkVisible.Checked; }
            set
            {
                _suppress = true;
                _chkVisible.Checked = value;
                _suppress = false;
                UpdateDimState();
            }
        }

        public FieldCard(CardFieldInfo info, TextFieldOverride ov)
        {
            _info = info;
            _ov = ov;

            Dock = DockStyle.Top;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            RightToLeft = RightToLeft.Yes;
            BackColor = UiTheme.CardBack;
            Margin = new Padding(0, 0, 0, 6);
            Padding = new Padding(1);

            // ─── ردیفِ همیشه‌دیده ───────────────────────────────────────────
            Panel head = new Panel { Dock = DockStyle.Top, Height = 40, RightToLeft = RightToLeft.Yes };

            _chkVisible = new CheckBox
            {
                Dock = DockStyle.Right,
                Width = 26,
                RightToLeft = RightToLeft.Yes,
                Checked = true,
                Margin = new Padding(0)
            };
            _chkVisible.CheckedChanged += delegate
            {
                if (_suppress) return;
                UpdateDimState();
                Raise(VisibleToggled);
            };
            UiTheme.SetTip(_chkVisible, "نمایش این مورد روی کارت");

            Label lblName = new Label
            {
                Text = info.Label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(9.75F),
                ForeColor = UiTheme.TextDark,
                Padding = new Padding(0, 0, 6, 0)
            };
            UiTheme.SetTip(lblName, info.SourceText + "\n" + info.SourceTech);

            _btnExpand = UiTheme.CreateSecondaryButton("⚙", "");
            _btnExpand.Dock = DockStyle.Left;
            _btnExpand.Width = 34;
            _btnExpand.Click += delegate { ToggleEditor(); };
            UiTheme.SetTip(_btnExpand, "تنظیمات این مورد");

            _btnDown = UiTheme.CreateSecondaryButton("▼", "");
            _btnDown.Dock = DockStyle.Left;
            _btnDown.Width = 30;
            _btnDown.Click += delegate { Raise(MoveDown); };
            UiTheme.SetTip(_btnDown, "پایین‌تر");

            _btnUp = UiTheme.CreateSecondaryButton("▲", "");
            _btnUp.Dock = DockStyle.Left;
            _btnUp.Width = 30;
            _btnUp.Click += delegate { Raise(MoveUp); };
            UiTheme.SetTip(_btnUp, "بالاتر");

            _lblOrder = new Label
            {
                Dock = DockStyle.Left,
                Width = 42,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UiTheme.Font(8.5F),
                ForeColor = UiTheme.TextMuted
            };

            head.Controls.Add(lblName);
            head.Controls.Add(_chkVisible);
            head.Controls.Add(_btnExpand);
            head.Controls.Add(_lblOrder);
            head.Controls.Add(_btnDown);
            head.Controls.Add(_btnUp);

            Panel border = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = UiTheme.Border };

            Controls.Add(head);
            Controls.Add(border);

            SetOrderInfo(-1, 0);
        }

        // موقعیت در ترتیب — pos<0 یعنی این مورد اصلاً قابلِ جابه‌جایی نیست
        // (فقط ۵ ردیفِ متنیِ پنلِ سرپرست در HTML قابلِ ترتیب‌دهی‌اند).
        public void SetOrderInfo(int pos, int total)
        {
            bool orderable = pos >= 0 && total > 0;
            _btnUp.Visible = orderable;
            _btnDown.Visible = orderable;
            _lblOrder.Visible = orderable;
            if (orderable)
            {
                _lblOrder.Text = (pos + 1) + "/" + total;
                _btnUp.Enabled = pos > 0;
                _btnDown.Enabled = pos < total - 1;
            }
        }

        private void UpdateDimState()
        {
            _btnExpand.Enabled = _chkVisible.Checked;
            if (!_chkVisible.Checked && _expanded) ToggleEditor();
        }

        private void ToggleEditor()
        {
            if (_editor == null) _editor = BuildEditor();
            _expanded = !_expanded;
            _editor.Visible = _expanded;
            _btnExpand.Text = _expanded ? "⚙▲" : "⚙";
        }

        // ─── ویرایشگرِ تنبل ────────────────────────────────────────────────
        private Panel BuildEditor()
        {
            Panel p = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                RightToLeft = RightToLeft.Yes,
                Padding = new Padding(10, 6, 10, 10),
                BackColor = UiTheme.Background,
                Visible = false
            };

            // منبعِ داده — متنِ دوستانه؛ نامِ فنی فقط در Tooltip.
            Label src = new Label
            {
                Text = "منبع: " + _info.SourceText,
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(8.5F),
                ForeColor = UiTheme.TextMuted
            };
            UiTheme.SetTip(src, _info.SourceTech);

            // آموزش — اگر این مورد در HTML عنصرِ متنیِ نشان‌دار ندارد (لوگو،
            // مهر، امضا، عکس‌ها، فهرست اعضا …)، تنظیمِ رنگ/اندازه/قلم روی آن
            // بی‌اثر است. به‌جای نشان‌دادنِ کنترل‌هایی که کاری نمی‌کنند، فقط
            // منبع و یک توضیحِ کوتاه نشان داده می‌شود.
            if (!CardFieldCatalog.SupportsTypography(_info.Key))
            {
                Label onlyVisibility = new Label
                {
                    Text = "برای این مورد فقط «نمایش/عدم‌نمایش» معنا دارد؛\nرنگ و اندازه روی آن اثری ندارند.",
                    Dock = DockStyle.Top,
                    Height = 40,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = UiTheme.Font(8.5F),
                    ForeColor = UiTheme.TextMuted
                };
                p.Controls.Add(onlyVisibility);
                p.Controls.Add(src);
                Controls.Add(p);
                p.BringToFront();
                return p;
            }

            // متنِ دلخواه — فقط اگر این مورد اجازه دهد.
            Control textRow;
            if (_info.CanEditText)
            {
                TextBox txt = new TextBox { RightToLeft = RightToLeft.Yes, Text = _ov.Content ?? "" };
                UiTheme.StyleTextBox(txt);
                txt.TextChanged += delegate { _ov.Content = txt.Text; Raise(OverrideChanged); };
                textRow = Row("متن دلخواه", txt, 220);
            }
            else
            {
                Label info = new Label
                {
                    Text = "از " + _info.SourceText + " می‌آید و اینجا تغییر نمی‌کند.",
                    ForeColor = UiTheme.TextMuted,
                    Font = UiTheme.Font(8.5F),
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleRight
                };
                textRow = Row("متن", info, 260);
            }

            // رنگ
            Panel swatch;
            Panel colorPill = ColorPill(out swatch, _ov.Color);
            Control colorRow = Row("رنگ", colorPill, 130);

            // اندازه
            NumericUpDown size = new NumericUpDown
            {
                Minimum = 50, Maximum = 200, Value = Clamp(_ov.FontSizePercent, 50, 200),
                TextAlign = HorizontalAlignment.Center
            };
            size.ValueChanged += delegate { _ov.FontSizePercent = (int)size.Value; Raise(OverrideChanged); };
            Control sizeRow = Row("اندازه (٪)", size, 90);

            // فاصلهٔ خط
            NumericUpDown line = new NumericUpDown
            {
                Minimum = 50, Maximum = 200, Value = Clamp(_ov.LineHeightPercent, 50, 200),
                TextAlign = HorizontalAlignment.Center
            };
            line.ValueChanged += delegate { _ov.LineHeightPercent = (int)line.Value; Raise(OverrideChanged); };
            Control lineRow = Row("فاصلهٔ خط (٪)", line, 90);

            // ضخامت
            ComboBox weight = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
            weight.Items.AddRange(new object[] { "پیش‌فرض", "نازک", "متوسط", "نیمه‌ضخیم", "ضخیم" });
            weight.SelectedIndex = WeightToIndex(_ov.FontWeight);
            weight.SelectedIndexChanged += delegate { _ov.FontWeight = IndexToWeight(weight.SelectedIndex); Raise(OverrideChanged); };
            Control weightRow = Row("ضخامت", weight, 130);

            // چینش
            ComboBox align = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
            align.Items.AddRange(new object[] { "پیش‌فرض", "راست", "وسط", "چپ" });
            align.SelectedIndex = AlignToIndex(_ov.Alignment);
            align.SelectedIndexChanged += delegate { _ov.Alignment = IndexToAlign(align.SelectedIndex); Raise(OverrideChanged); };
            Control alignRow = Row("چینش", align, 130);

            Button reset = UiTheme.CreateSecondaryButton("بازنشانی این مورد", "");
            reset.Dock = DockStyle.Top;
            reset.Height = 28;
            reset.Click += delegate
            {
                _ov.Content = ""; _ov.Color = ""; _ov.FontFamily = "";
                _ov.FontSizePercent = 100; _ov.LineHeightPercent = 100;
                _ov.Alignment = ""; _ov.FontWeight = "";
                size.Value = 100; line.Value = 100;
                weight.SelectedIndex = 0; align.SelectedIndex = 0;
                SetSwatch(swatch, "");
                Raise(OverrideChanged);
            };

            // ترتیبِ افزودن معکوسِ نمایش است (Dock=Top بر پایهٔ z-order).
            p.Controls.Add(reset);
            p.Controls.Add(alignRow);
            p.Controls.Add(weightRow);
            p.Controls.Add(lineRow);
            p.Controls.Add(sizeRow);
            p.Controls.Add(colorRow);
            p.Controls.Add(textRow);
            p.Controls.Add(src);

            Controls.Add(p);
            p.BringToFront();
            return p;
        }

        private static int Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        private Panel Row(string label, Control value, int valueWidth)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 30, RightToLeft = RightToLeft.Yes };
            Label lbl = new Label
            {
                Text = label,
                Dock = DockStyle.Right,
                Width = 100,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted,
                Font = UiTheme.Font(9F)
            };
            value.Dock = DockStyle.Right;
            value.Width = valueWidth;
            row.Controls.Add(value);
            row.Controls.Add(lbl);
            return row;
        }

        private Panel ColorPill(out Panel swatch, string hex)
        {
            Panel wrap = new Panel { Height = 26, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand, BackColor = Color.White };
            Panel sw = new Panel { Dock = DockStyle.Left, Width = 26 };
            Label hexLbl = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(8.5F),
                ForeColor = UiTheme.TextMuted,
                Padding = new Padding(0, 0, 6, 0)
            };
            wrap.Controls.Add(hexLbl);
            wrap.Controls.Add(sw);
            swatch = sw;

            EventHandler pick = delegate
            {
                using (ColorDialog dlg = new ColorDialog { FullOpen = true })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    string h = "#" + dlg.Color.R.ToString("X2") + dlg.Color.G.ToString("X2") + dlg.Color.B.ToString("X2");
                    _ov.Color = h;
                    SetSwatchOn(sw, hexLbl, h);
                    Raise(OverrideChanged);
                }
            };
            wrap.Click += pick;
            sw.Click += pick;
            hexLbl.Click += pick;

            sw.Tag = hexLbl;
            SetSwatchOn(sw, hexLbl, hex);
            return wrap;
        }

        private static void SetSwatchOn(Panel sw, Label hexLbl, string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                sw.BackColor = Color.White;
                hexLbl.Text = "پیش‌فرض";
                return;
            }
            try { sw.BackColor = ColorTranslator.FromHtml(hex); hexLbl.Text = hex; }
            catch { sw.BackColor = Color.White; hexLbl.Text = "پیش‌فرض"; }
        }

        private static void SetSwatch(Panel sw, string hex)
        {
            Label lbl = sw.Tag as Label;
            if (lbl != null) SetSwatchOn(sw, lbl, hex);
        }

        private static int WeightToIndex(string w)
        {
            switch ((w ?? "").Trim())
            {
                case "400": return 1;
                case "500": return 2;
                case "600": return 3;
                case "700": return 4;
                default: return 0;
            }
        }

        private static string IndexToWeight(int i)
        {
            switch (i)
            {
                case 1: return "400";
                case 2: return "500";
                case 3: return "600";
                case 4: return "700";
                default: return "";
            }
        }

        private static int AlignToIndex(string a)
        {
            switch ((a ?? "").Trim().ToLowerInvariant())
            {
                case "right": return 1;
                case "center": return 2;
                case "left": return 3;
                default: return 0;
            }
        }

        private static string IndexToAlign(int i)
        {
            switch (i)
            {
                case 1: return "right";
                case 2: return "center";
                case 3: return "left";
                default: return "";
            }
        }

        private void Raise(EventHandler h)
        {
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}

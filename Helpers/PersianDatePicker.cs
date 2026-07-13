using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // کنترل انتخاب تاریخ شمسی — جایگزین DateTimePicker استاندارد.
    // آموزش: کنترل بومی ویندوز (SysDateTimePick32 که DateTimePicker آن را
    // wrap می‌کند) توانایی نمایش تقویم جلالی را ندارد؛ این یک محدودیت
    // شناخته‌شده Win32 است — حتی با تنظیم Thread.CurrentCulture.Calendar
    // روی PersianCalendar (که Program.cs از قبل انجام می‌دهد)، کنترل بومی
    // همچنان تقویم میلادی رسم می‌کند. این کنترل جایگزین از یک TextBox
    // راست‌چین (فقط‌خواندنی، نمایش yyyy/MM/dd شمسی) + دکمه پاپ‌آپ تقویم
    // تشکیل شده و برای تبدیل از PersianDateHelper استفاده می‌کند. ذخیره
    // داخلی (Value) همچنان یک DateTime معمولی (میلادی) است — فقط نمایش
    // شمسی است، مطابق تصمیم «ذخیره میلادی، نمایش شمسی».
    // ─────────────────────────────────────────────────────────────────────────
    public class PersianDatePicker : UserControl
    {
        private readonly MaskedTextBox _txt;
        private readonly Button _btnCalendar;
        private readonly CheckBox _chk;
        private readonly ToolTip _tip;

        private DateTime _value = DateTime.Today;
        private bool _checked = true;
        private bool _showCheckBox;

        public event EventHandler ValueChanged;

        public PersianDatePicker()
        {
            Height = UiTheme.InputHeight;
            Width = UiTheme.InputWidth + 30;

            // آموزش — به‌درخواست کاربر: فیلد تاریخ «قابل تایپ» است (دقیقاً مثل
            // فرم حسابداری) با ماسک اسلش‌دار «سال/ماه/روز». کاربر می‌تواند
            // مستقیماً عدد تایپ کند (اسلش‌ها خودکار جا می‌افتند) یا از دکمه
            // تقویم 📅 انتخاب کند. کاراکتر راهنمای «_» خودِ فرمت را نشان می‌دهد و
            // یک ToolTip هم فرمت و مثال را توضیح می‌دهد. اگر مقدار تایپ‌شده
            // ناقص/نامعتبر باشد، هنگام خروج از فیلد به آخرین تاریخ معتبر برمی‌گردد.
            _txt = new MaskedTextBox();
            _txt.Mask = "0000/00/00";
            _txt.PromptChar = '_';
            _txt.RightToLeft = RightToLeft.Yes;
            _txt.TextAlign = HorizontalAlignment.Center;
            _txt.BorderStyle = BorderStyle.FixedSingle;
            _txt.Font = UiTheme.Font(UiTheme.SizeBody);
            _txt.Dock = DockStyle.Fill;
            _txt.Enter += delegate { if (_txt.Enabled) _txt.BackColor = UiTheme.HoverTint; };
            _txt.Leave += delegate { _txt.BackColor = System.Drawing.Color.White; TryCommitTypedValue(); };
            _txt.KeyDown += Txt_KeyDown;

            _btnCalendar = new Button();
            _btnCalendar.Text = "📅";
            _btnCalendar.Dock = DockStyle.Right;
            _btnCalendar.Width = 30;
            _btnCalendar.FlatStyle = FlatStyle.Flat;
            _btnCalendar.FlatAppearance.BorderSize = 1;
            _btnCalendar.FlatAppearance.BorderColor = UiTheme.Border;
            _btnCalendar.Cursor = Cursors.Hand;
            _btnCalendar.TabStop = false;
            _btnCalendar.Click += delegate { OpenCalendar(); };

            _chk = new CheckBox();
            _chk.Dock = DockStyle.Left;
            _chk.Width = 22;
            _chk.Visible = false;
            _chk.Checked = true;
            _chk.CheckedChanged += delegate
            {
                _checked = _chk.Checked;
                UpdateEnabledState();
                RaiseValueChanged();
            };

            _tip = new ToolTip();
            _tip.SetToolTip(_txt, "فرمت تاریخ: سال/ماه/روز — مثال 1404/05/12" + Environment.NewLine +
                                  "می‌توانید مستقیماً تایپ کنید یا از دکمه 📅 انتخاب کنید.");
            _tip.SetToolTip(_btnCalendar, "انتخاب از تقویم شمسی");

            Controls.Add(_txt);
            Controls.Add(_btnCalendar);
            Controls.Add(_chk);

            RefreshText();
        }

        private void Txt_KeyDown(object sender, KeyEventArgs e)
        {
            // Enter = تأیید مقدار تایپ‌شده
            if (e.KeyCode == Keys.Enter)
            {
                TryCommitTypedValue();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        // تبدیل متن تایپ‌شده به تاریخ معتبر. اگر ناقص یا نامعتبر بود، به آخرین
        // مقدار معتبر برمی‌گردد (revert) تا فیلد هیچ‌وقت تاریخ نامعتبر نگه ندارد.
        private void TryCommitTypedValue()
        {
            if (!_txt.MaskCompleted)
            {
                // اگر کاربر چیزی وارد نکرده (کل فیلد خالی است) و در حالت اختیاری
                // هستیم، دست نمی‌زنیم؛ در غیر این صورت متن را به مقدار قبلی برمی‌گردانیم.
                RefreshText();
                return;
            }
            try
            {
                DateTime parsed = PersianDateHelper.ParsePersianDate(_txt.Text).Date;
                _value = parsed;
                if (_showCheckBox) Checked = true;
                RaiseValueChanged();
            }
            catch
            {
                RefreshText(); // تاریخ نامعتبر (مثلاً ماه ۱۳ یا روز ۳۲) → بازگشت
            }
        }

        // مطابق DateTimePicker.ShowCheckBox: اگر true باشد، یک چک‌باکس کنار
        // کنترل نمایش داده می‌شود که فعال/غیرفعال بودن تاریخ را کنترل می‌کند
        // (برای فیلترهای اختیاری مثل «از تاریخ / تا تاریخ» یا تاریخ تولد اختیاری).
        public bool ShowCheckBox
        {
            get { return _showCheckBox; }
            set
            {
                _showCheckBox = value;
                _chk.Visible = value;
                UpdateEnabledState();
            }
        }

        // مطابق DateTimePicker.Checked
        public bool Checked
        {
            get { return !_showCheckBox || _checked; }
            set
            {
                _checked = value;
                if (_showCheckBox)
                    _chk.Checked = value;
                UpdateEnabledState();
            }
        }

        public DateTime Value
        {
            get { return _value; }
            set
            {
                _value = value.Date;
                RefreshText();
                RaiseValueChanged();
            }
        }

        private void UpdateEnabledState()
        {
            bool enabled = !_showCheckBox || _checked;
            _txt.Enabled = enabled;
            _btnCalendar.Enabled = enabled;
        }

        private void RefreshText()
        {
            // ماسک «0000/00/00» فقط ارقام می‌پذیرد و خودش اسلش‌ها را می‌گذارد؛
            // پس ۸ رقم سال/ماه/روز را (بدون اسلش) به آن می‌دهیم.
            _txt.Text = PersianDateHelper.ToPersianDateString(_value).Replace("/", "");
        }

        private void RaiseValueChanged()
        {
            if (ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
        }

        private void OpenCalendar()
        {
            if (_showCheckBox && !_checked)
                return;

            using (PersianCalendarPopup popup = new PersianCalendarPopup(_value))
            {
                Point screenPoint = PointToScreen(new Point(0, Height));
                popup.StartPosition = FormStartPosition.Manual;
                popup.Location = new Point(screenPoint.X, screenPoint.Y);

                if (popup.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    if (_showCheckBox)
                        Checked = true;
                    Value = popup.SelectedDate;
                }
            }
        }

        // ─── پاپ‌آپ تقویم جلالی — ماه/سال قابل تغییر، شبکه ۷×۶ روز ───────────
        private class PersianCalendarPopup : Form
        {
            private static readonly PersianCalendar Pc = new PersianCalendar();

            private static readonly string[] MonthNames =
            {
                "حمل", "ثور", "جوزا", "سرطان", "اسد", "سنبله",
                "میزان", "عقرب", "قوس", "جدی", "دلو", "حوت"
            };

            // آموزش — هفته شمسی از شنبه شروع می‌شود؛ .NET DayOfWeek با
            // یکشنبه=0 شروع می‌شود، پس نگاشت ستون با فرمول ((dow+1)%7) انجام
            // می‌شود (شنبه=6 → ستون ۰).
            private static readonly string[] DayHeaders = { "ش", "ی", "د", "س", "چ", "پ", "ج" };

            public DateTime SelectedDate { get; private set; }

            private int _year;
            private int _month;
            private Label _lblHeader;
            private TableLayoutPanel _grid;

            public PersianCalendarPopup(DateTime initial)
            {
                SelectedDate = initial.Date;
                _year = Pc.GetYear(SelectedDate);
                _month = Pc.GetMonth(SelectedDate);

                FormBorderStyle = FormBorderStyle.FixedToolWindow;
                ShowInTaskbar = false;
                RightToLeft = RightToLeft.Yes;
                RightToLeftLayout = true;
                Font = UiTheme.Font(9.5f);
                BackColor = Color.White;
                ClientSize = new Size(266, 270);
                Text = "انتخاب تاریخ";

                BuildUi();
                RenderMonth();
            }

            private void BuildUi()
            {
                Panel nav = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = UiTheme.Primary };

                Button btnPrev = new Button
                {
                    Text = "‹", Dock = DockStyle.Right, Width = 40,
                    FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
                    Font = UiTheme.FontBold(12f)
                };
                btnPrev.FlatAppearance.BorderSize = 0;
                btnPrev.Click += delegate { ChangeMonth(-1); };

                Button btnNext = new Button
                {
                    Text = "›", Dock = DockStyle.Left, Width = 40,
                    FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
                    Font = UiTheme.FontBold(12f)
                };
                btnNext.FlatAppearance.BorderSize = 0;
                btnNext.Click += delegate { ChangeMonth(1); };

                _lblHeader = new Label
                {
                    Dock = DockStyle.Fill,
                    ForeColor = Color.White,
                    Font = UiTheme.FontBold(10.5f),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                nav.Controls.Add(_lblHeader);
                nav.Controls.Add(btnPrev);
                nav.Controls.Add(btnNext);

                Panel dowRow = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = UiTheme.HoverTint };
                TableLayoutPanel dowTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1 };
                for (int i = 0; i < 7; i++)
                {
                    dowTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
                    dowTable.Controls.Add(new Label
                    {
                        Text = DayHeaders[i],
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = UiTheme.FontBold(9f),
                        ForeColor = UiTheme.TextMuted
                    }, i, 0);
                }
                dowRow.Controls.Add(dowTable);

                _grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 6, Padding = new Padding(2) };
                for (int i = 0; i < 7; i++)
                    _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
                for (int i = 0; i < 6; i++)
                    _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6));

                Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(4) };
                Button btnToday = UiTheme.CreateSecondaryButton("امروز", "");
                btnToday.Dock = DockStyle.Fill;
                btnToday.Click += delegate
                {
                    SelectedDate = DateTime.Today;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                footer.Controls.Add(btnToday);

                Controls.Add(_grid);
                Controls.Add(dowRow);
                Controls.Add(footer);
                Controls.Add(nav);
            }

            private void ChangeMonth(int delta)
            {
                _month += delta;
                if (_month < 1) { _month = 12; _year--; }
                else if (_month > 12) { _month = 1; _year++; }
                RenderMonth();
            }

            private void RenderMonth()
            {
                _lblHeader.Text = MonthNames[_month - 1] + "   " + _year;
                _grid.Controls.Clear();

                int daysInMonth = Pc.GetDaysInMonth(_year, _month);
                DateTime firstOfMonth = Pc.ToDateTime(_year, _month, 1, 0, 0, 0, 0);
                int offset = ((int)firstOfMonth.DayOfWeek + 1) % 7;

                int day = 1;
                for (int row = 0; row < 6 && day <= daysInMonth; row++)
                {
                    for (int col = 0; col < 7; col++)
                    {
                        if (row == 0 && col < offset)
                            continue;
                        if (day > daysInMonth)
                            continue;

                        int thisDay = day;
                        DateTime cellDate = Pc.ToDateTime(_year, _month, thisDay, 0, 0, 0, 0);

                        Button btnDay = new Button
                        {
                            Text = thisDay.ToString(CultureInfo.InvariantCulture),
                            Dock = DockStyle.Fill,
                            FlatStyle = FlatStyle.Flat,
                            Margin = new Padding(1),
                            Font = UiTheme.Font(9.5f)
                        };
                        btnDay.FlatAppearance.BorderSize = 0;

                        bool isSelected = cellDate.Date == SelectedDate.Date;
                        bool isToday = cellDate.Date == DateTime.Today;

                        if (isSelected)
                        {
                            btnDay.BackColor = UiTheme.Primary;
                            btnDay.ForeColor = Color.White;
                        }
                        else if (isToday)
                        {
                            btnDay.BackColor = UiTheme.HoverTint;
                            btnDay.ForeColor = UiTheme.Primary;
                        }
                        else
                        {
                            btnDay.BackColor = Color.White;
                            btnDay.ForeColor = UiTheme.TextDark;
                        }

                        btnDay.Click += delegate
                        {
                            SelectedDate = cellDate;
                            DialogResult = DialogResult.OK;
                            Close();
                        };

                        _grid.Controls.Add(btnDay, col, row);
                        day++;
                    }
                }
            }

            protected override void OnDeactivate(EventArgs e)
            {
                base.OnDeactivate(e);
                if (DialogResult != DialogResult.OK)
                    Close();
            }
        }
    }
}

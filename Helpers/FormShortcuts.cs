using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // میان‌بُرهای صفحه‌کلید — یک زیرساختِ مشترک برای همه‌ی فرم‌ها.
    //
    // آموزش — چرا یک کلاسِ مشترک و نه چند خط کد در هر فرم:
    // اگر هر فرم خودش KeyDown می‌نوشت، سه مشکل تکرار می‌شد که هر سه در
    // برنامه‌های واقعی باگ می‌سازند:
    //
    //   ۱. میان‌بُری که دکمه‌ی *غیرفعال* را می‌زند. مثلاً Ctrl+S وقتی فرم در
    //      حالت ویرایش نیست: دکمه خاموش است ولی میان‌بُر همچنان کار می‌کند و
    //      کاربر رفتاری می‌بیند که با ظاهرِ فرم نمی‌خوانَد. اینجا هر میان‌بُر
    //      پیش از اجرا، Enabled و Visible هدفش را بررسی می‌کند.
    //
    //   ۲. بلعیدنِ کلیدهای ویرایشِ متن. Ctrl+C/V/X/A/Z هرگز نباید گرفته شوند،
    //      وگرنه کپی/چسباندن داخل فیلدها از کار می‌افتد. این کلاس عمداً
    //      هیچ‌کدامشان را نمی‌پذیرد و اگر کسی اشتباهاً ثبتشان کند، رد می‌شوند.
    //
    //   ۳. میان‌بُرهای نامستند. کاربر از کجا بداند چه کلیدهایی هست؟ هر فرمی
    //      که میان‌بُر داشته باشد خودکار F1 می‌گیرد و فهرستِ همان فرم را
    //      نشان می‌دهد — فهرست از خودِ ثبت‌ها ساخته می‌شود، پس هرگز کهنه
    //      نمی‌شود.
    //
    // ⚠ از KeyPreview + KeyDown استفاده می‌شود (نه IMessageFilter): فرم پیش از
    // کنترلِ فوکوس‌دار کلید را می‌بیند، ولی دامنه‌اش به همان فرم محدود می‌ماند.
    // IMessageFilter سراسری است و روی همه‌ی فرم‌ها اثر می‌گذاشت — همان چیزی که
    // DevCenterAccess عمداً برای یک تک‌کلیدِ مدیرکل به کار می‌برد و اینجا
    // مطلوب نیست.
    // ═════════════════════════════════════════════════════════════════════════
    public static class FormShortcuts
    {
        // کلیدهایی که هرگز نباید به‌عنوان میان‌بُر گرفته شوند (ویرایشِ متن).
        private static readonly Keys[] Reserved =
        {
            Keys.Control | Keys.C, Keys.Control | Keys.V, Keys.Control | Keys.X,
            Keys.Control | Keys.A, Keys.Control | Keys.Z, Keys.Control | Keys.Y
        };

        public sealed class Binding
        {
            public Keys Key;
            public string Title = "";
            public Button Target;      // یکی از این دو پر می‌شود
            public Action Action;

            public string KeyText { get { return Describe(Key); } }
        }

        // ─────────────────────────────────────────────────────────────────────
        // نقطه‌ی ورود: FormShortcuts.For(this).Save(btnSave).New(btnNew) ...
        // ─────────────────────────────────────────────────────────────────────
        public static Builder For(Form form) { return new Builder(form); }

        public sealed class Builder
        {
            private readonly Form _form;
            private readonly List<Binding> _bindings = new List<Binding>();

            internal Builder(Form form)
            {
                _form = form;
                if (_form == null) return;

                _form.KeyPreview = true;
                _form.KeyDown += OnKeyDown;

                // فهرست میان‌بُرها با F1 — بدون نیاز به ثبتِ دستی.
                _form.FormClosed += delegate { Registry.Remove(_form); };
                Registry[_form] = _bindings;
            }

            // ─── میان‌بُرهای استاندارد ───────────────────────────────────────
            public Builder Save(Button b)    { return Bind(Keys.Control | Keys.S, "ذخیره", b); }
            public Builder New(Button b)     { return Bind(Keys.Control | Keys.N, "جدید", b); }
            public Builder Edit(Button b)    { return Bind(Keys.Control | Keys.E, "ویرایش", b); }
            public Builder Delete(Button b)  { return Bind(Keys.Control | Keys.D, "حذف", b); }
            public Builder Search(Button b)  { return Bind(Keys.Control | Keys.F, "جستجو", b); }
            public Builder Print(Button b)   { return Bind(Keys.Control | Keys.P, "چاپ", b); }
            public Builder Refresh(Button b) { return Bind(Keys.F5, "تازه‌سازی", b); }
            public Builder Help(Button b)    { return Bind(Keys.F1, "راهنما", b); }
            public Builder Close(Button b)   { return Bind(Keys.Escape, "بستن", b); }

            // فوکوس‌دادن به یک کادر (مثلاً کادر جستجو) به‌جای زدنِ دکمه.
            public Builder FocusSearch(Control box)
            {
                if (box == null) return this;
                return Bind(Keys.Control | Keys.F, "رفتن به کادر جستجو",
                            delegate { try { box.Focus(); } catch { } });
            }

            // ─── میان‌بُرِ «آگاه از تبِ جاری» ────────────────────────────────
            //
            // آموزش — چرا این لازم شد: فرم تنظیمات و حسابداری هرکدام چندین
            // دکمه‌ی «ذخیره» دارند، یکی برای هر تب. بستنِ Ctrl+S به یکی از
            // آن‌ها یعنی کاربر در تبِ «مسیرها» کلید بزند و تنظیماتِ «امنیت»
            // ذخیره شود — یک باگِ داده‌ای، نه یک ناراحتیِ ظاهری.
            //
            // راه‌حل: هدف در *لحظه‌ی فشردن* پیدا می‌شود، نه هنگام ثبت. چون
            // در هر لحظه فقط یک تب نمایان است و Control.Visible برای کنترلی
            // که والدش پنهان است false برمی‌گرداند، «دکمه‌ی نمایانِ فعالی که
            // عنوانش با این کلمه شروع می‌شود» دقیقاً همان دکمه‌ی تبِ جاری است.
            public Builder BindVisible(Keys key, string title, string captionPrefix)
            {
                if (string.IsNullOrWhiteSpace(captionPrefix)) return this;

                Form form = _form;
                return Bind(key, title, delegate
                {
                    Button target = FindVisibleButton(form, captionPrefix);
                    if (target != null) target.PerformClick();
                });
            }

            public Builder SaveVisible() { return BindVisible(Keys.Control | Keys.S, "ذخیره (تبِ جاری)", "ذخیره"); }

            public Builder Bind(Keys key, string title, Button target)
            {
                if (target == null || IsReserved(key)) return this;
                _bindings.Add(new Binding { Key = key, Title = title, Target = target });
                return this;
            }

            public Builder Bind(Keys key, string title, Action action)
            {
                if (action == null || IsReserved(key)) return this;
                _bindings.Add(new Binding { Key = key, Title = title, Action = action });
                return this;
            }

            // ─────────────────────────────────────────────────────────────────
            private void OnKeyDown(object sender, KeyEventArgs e)
            {
                try
                {
                    // F1 همیشه فهرستِ میان‌بُرهای همین فرم را نشان می‌دهد، مگر
                    // اینکه خودِ فرم F1 را به راهنمای اختصاصی‌اش بسته باشد.
                    if (e.KeyData == Keys.F1 && !HasBinding(Keys.F1))
                    {
                        ShowList(_form);
                        e.SuppressKeyPress = true;
                        e.Handled = true;
                        return;
                    }

                    foreach (Binding binding in _bindings)
                    {
                        if (binding.Key != e.KeyData) continue;

                        // ⚠ اگر هدف در دسترس نیست، کلید *مصرف نمی‌شود* و به
                        // مسیر عادی‌اش می‌رود. میان‌بُری که کارِ نامرئی انجام
                        // دهد بدتر از نبودنش است.
                        if (binding.Target != null)
                        {
                            if (!binding.Target.Enabled || !binding.Target.Visible) return;
                            binding.Target.PerformClick();
                        }
                        else if (binding.Action != null)
                        {
                            binding.Action();
                        }
                        else continue;

                        e.SuppressKeyPress = true;
                        e.Handled = true;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    try { Enterprise.ErrorLogger.Log(ex, "FormShortcuts.OnKeyDown"); } catch { }
                }
            }

            private bool HasBinding(Keys key)
            {
                foreach (Binding b in _bindings) if (b.Key == key) return true;
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        private static readonly Dictionary<Form, List<Binding>> Registry =
            new Dictionary<Form, List<Binding>>();

        // اولین دکمه‌ی *نمایان و فعالِ* درختِ کنترل‌ها که عنوانش با این کلمه
        // شروع می‌شود. چون تب‌های پنهان Visible=false دارند، نتیجه همیشه
        // متعلق به تبِ جاری است.
        internal static Button FindVisibleButton(Control root, string captionPrefix)
        {
            if (root == null) return null;

            Button match = root as Button;
            if (match != null && match.Visible && match.Enabled &&
                !string.IsNullOrEmpty(match.Text) &&
                Strip(match.Text).StartsWith(captionPrefix, StringComparison.Ordinal))
                return match;

            foreach (Control child in root.Controls)
            {
                if (!child.Visible) continue;   // شاخه‌ی پنهان اصلاً پیمایش نمی‌شود
                Button found = FindVisibleButton(child, captionPrefix);
                if (found != null) return found;
            }
            return null;
        }

        // دکمه‌های این برنامه معمولاً «آیکن + فاصله + متن» دارند
        // (UiTheme.CreateButton). برای تطبیقِ عنوان، نویسه‌های غیرفارسیِ ابتدای
        // متن کنار گذاشته می‌شوند تا «✔  ذخیره» هم با «ذخیره» بخواند.
        private static string Strip(string text)
        {
            int i = 0;
            while (i < text.Length && !IsPersianLetter(text[i])) i++;
            return text.Substring(i);
        }

        private static bool IsPersianLetter(char c)
        {
            return (c >= '؀' && c <= 'ۿ') || (c >= 'ﭐ' && c <= '﻿');
        }

        private static bool IsReserved(Keys key)
        {
            foreach (Keys r in Reserved) if (r == key) return true;
            return false;
        }

        // نامِ خواندنیِ کلید — برای فهرستِ راهنما.
        private static string Describe(Keys key)
        {
            var parts = new List<string>();
            if ((key & Keys.Control) == Keys.Control) parts.Add("Ctrl");
            if ((key & Keys.Shift) == Keys.Shift) parts.Add("Shift");
            if ((key & Keys.Alt) == Keys.Alt) parts.Add("Alt");

            Keys code = key & Keys.KeyCode;
            parts.Add(code == Keys.Escape ? "Esc" : code.ToString());

            return string.Join(" + ", parts.ToArray());
        }

        // ═════════════════════════════════════════════════════════════════════
        // فهرستِ میان‌بُرهای فرمِ جاری (F1)
        //
        // از خودِ ثبت‌ها ساخته می‌شود، نه از یک متنِ دستی — پس اگر روزی
        // میان‌بُری اضافه/حذف شود، این فهرست خودکار درست می‌ماند.
        // ═════════════════════════════════════════════════════════════════════
        public static void ShowList(Form form)
        {
            List<Binding> bindings;
            if (form == null || !Registry.TryGetValue(form, out bindings) || bindings.Count == 0)
                return;

            try
            {
                using (var dlg = new Form())
                {
                    dlg.Text = "میان‌بُرهای این صفحه";
                    dlg.RightToLeft = RightToLeft.Yes;
                    dlg.RightToLeftLayout = true;
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.MinimizeBox = false;
                    dlg.MaximizeBox = false;
                    dlg.ShowInTaskbar = false;
                    dlg.BackColor = UiTheme.Background;
                    dlg.Font = UiTheme.Font(UiTheme.SizeBody);
                    dlg.ClientSize = new Size(430, Math.Min(560, 96 + bindings.Count * 34));

                    var header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = UiTheme.PrimaryDark };
                    header.Controls.Add(new Label
                    {
                        Text = "میان‌بُرهای صفحه‌کلید",
                        Dock = DockStyle.Fill,
                        ForeColor = Color.White,
                        Font = UiTheme.FontBold(UiTheme.SizeMedium),
                        TextAlign = ContentAlignment.MiddleRight,
                        Padding = new Padding(0, 0, 16, 0)
                    });
                    dlg.Controls.Add(header);

                    var footer = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = UiTheme.CardBack };
                    Button ok = UiTheme.CreateButton("بستن", "✕", UiTheme.Primary);
                    ok.Size = new Size(120, 32);
                    ok.Location = new Point(16, 8);
                    ok.Click += delegate { dlg.Close(); };
                    footer.Controls.Add(ok);
                    dlg.Controls.Add(footer);
                    dlg.AcceptButton = ok;
                    dlg.CancelButton = ok;

                    var list = new ListView
                    {
                        Dock = DockStyle.Fill,
                        View = View.Details,
                        FullRowSelect = true,
                        GridLines = false,
                        HeaderStyle = ColumnHeaderStyle.Nonclickable,
                        RightToLeft = RightToLeft.Yes,
                        RightToLeftLayout = true,
                        BackColor = Color.White,
                        Font = UiTheme.Font(UiTheme.SizeBody)
                    };
                    list.Columns.Add("کار", 250, HorizontalAlignment.Right);
                    list.Columns.Add("کلید", 150, HorizontalAlignment.Right);

                    foreach (Binding b in bindings)
                    {
                        bool available = b.Target == null || (b.Target.Enabled && b.Target.Visible);
                        var item = new ListViewItem(b.Title);
                        item.SubItems.Add(b.KeyText);
                        if (!available) item.ForeColor = UiTheme.TextMuted;   // فعلاً در دسترس نیست
                        list.Items.Add(item);
                    }

                    var f1 = new ListViewItem("نمایش همین فهرست");
                    f1.SubItems.Add("F1");
                    list.Items.Add(f1);

                    dlg.Controls.Add(list);
                    list.BringToFront();

                    dlg.ShowDialog(form);
                }
            }
            catch (Exception ex)
            {
                try { Enterprise.ErrorLogger.Log(ex, "FormShortcuts.ShowList"); } catch { }
            }
        }
    }
}

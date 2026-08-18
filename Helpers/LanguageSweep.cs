using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // اعمالِ زبانِ انتخاب‌شده روی هر پنجره‌ای که باز می‌شود.
    //
    // مثل RtlCaptions، یک‌بار در Program نصب می‌شود و بقیه خودکار است؛ پس
    // هیچ‌کدام از ۱۷ فرمِ موجود (و فرم‌هایی که بعداً اضافه شوند) نیاز به
    // ویرایش ندارند.
    //
    // ⚠ دو تصمیمِ ایمنیِ کلیدی:
    //
    // ۱) چه چیزی ترجمه می‌شود: فقط متنِ «نمایشی» — عنوانِ پنجره، Label،
    //    Button، GroupBox، TabPage، CheckBox/RadioButton و سرستونِ جدول‌ها.
    //
    //    چه چیزی ترجمه نمی‌شود: ComboBox، TextBox، سلول‌های جدول و هر کنترلی
    //    که «مقدار» نگه می‌دارد. دلیلش حیاتی است: مقادیری مثل «فعال» یا «قطع»
    //    در ComboBoxها هستند، در دیتابیس ذخیره می‌شوند و در WHERE مقایسه
    //    می‌شوند. اگر ترجمه شوند، ذخیره‌ی پرونده مقدارِ انگلیسی می‌نویسد و
    //    فیلترها، داشبورد، خروجی‌ها و همگام‌سازی HTML همه از کار می‌افتند.
    //
    // ۲) متنِ اصلیِ فارسیِ هر کنترل نگه داشته می‌شود و ترجمه همیشه از روی همان
    //    انجام می‌گیرد. بدون این کار، تعویضِ فارسی → انگلیسی → عربی تلاش
    //    می‌کرد متنِ انگلیسی را به عربی ترجمه کند و شکست می‌خورد.
    // ═════════════════════════════════════════════════════════════════════════
    public static class LanguageSweep
    {
        private static readonly HashSet<Form> _hooked = new HashSet<Form>();
        private static bool _installed;

        // متنِ اصلیِ فارسیِ هر کنترل. ConditionalWeakTable مانعِ نشتِ حافظه
        // می‌شود: وقتی کنترل جمع‌آوری شد، مدخلش هم خودکار می‌رود.
        private static readonly ConditionalWeakTable<object, string[]> _original =
            new ConditionalWeakTable<object, string[]>();

        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            Application.Idle += OnIdle;
            Lang.LanguageChanged += delegate { ReapplyToOpenForms(); };
        }

        private static void OnIdle(object sender, EventArgs e)
        {
            List<Form> open = new List<Form>();
            foreach (Form f in Application.OpenForms) open.Add(f);

            foreach (Form f in open)
            {
                if (f == null || f.IsDisposed || _hooked.Contains(f)) continue;

                _hooked.Add(f);
                Form captured = f;
                f.Disposed += delegate { _hooked.Remove(captured); };

                Apply(f);
            }
        }

        // بعد از تعویضِ زبان، همه‌ی پنجره‌های باز دوباره ترجمه می‌شوند.
        private static void ReapplyToOpenForms()
        {
            List<Form> open = new List<Form>();
            foreach (Form f in Application.OpenForms) open.Add(f);

            foreach (Form f in open)
            {
                if (f == null || f.IsDisposed) continue;
                Apply(f);
                ApplyDirection(f);
            }
        }

        public static void Apply(Control root)
        {
            if (root == null) return;

            Form form = root as Form;
            if (form != null) form.Text = Translate(form, 0, form.Text);

            Walk(root);
        }

        private static void Walk(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                TranslateControl(c);
                Walk(c);
            }
        }

        private static void TranslateControl(Control c)
        {
            // ── کنترل‌هایی که «مقدار» نگه می‌دارند: هرگز دست نمی‌خورند ──
            if (c is TextBox || c is ComboBox || c is MaskedTextBox ||
                c is NumericUpDown || c is DateTimePicker || c is ListBox)
                return;

            DataGridView grid = c as DataGridView;
            if (grid != null) { TranslateGridHeaders(grid); return; }

            TabControl tabs = c as TabControl;
            if (tabs != null)
            {
                foreach (TabPage page in tabs.TabPages)
                    page.Text = Translate(page, 0, page.Text);
                return;
            }

            if (c is Label || c is Button || c is CheckBox || c is RadioButton ||
                c is GroupBox || c is LinkLabel)
            {
                c.Text = Translate(c, 0, c.Text);
            }
        }

        // سرستون‌ها ترجمه می‌شوند ولی نامِ ستون (DataPropertyName) دست‌نخورده
        // می‌ماند، چون کدهای دیگر با نامِ ستون کار می‌کنند.
        private static void TranslateGridHeaders(DataGridView grid)
        {
            foreach (DataGridViewColumn col in grid.Columns)
                col.HeaderText = Translate(col, 0, col.HeaderText);
        }

        // ═════════════════════════════════════════════════════════════════════
        // متنِ اصلیِ فارسی نگه داشته می‌شود و ترجمه همیشه از روی همان انجام
        // می‌گیرد — وگرنه تعویضِ فارسی → انگلیسی → عربی تلاش می‌کرد متنِ
        // انگلیسی را به عربی ترجمه کند.
        //
        // ⚠ باگی که این‌جا رفع شد (با آزمونِ تصویری پیدا شد): نسخه‌ی اول فقط
        // متنِ اولیه را ذخیره می‌کرد و در هر اجرا همان را برمی‌گرداند. نتیجه
        // این بود که هر برچسبی که برنامه در زمانِ اجرا عوضش می‌کند، به مقدارِ
        // اولش برمی‌گشت — مثلاً سرصفحه‌ی ویزارد در مرحله‌ی ۵ همچنان «۱ انتخاب
        // فایل» را نشان می‌داد.
        //
        // راه‌حل: علاوه بر متنِ مبدأ، آخرین متنی که خودمان نوشتیم هم ذخیره
        // می‌شود. اگر متنِ فعلیِ کنترل با نوشته‌ی خودمان یکی بود، یعنی برنامه
        // دستش نزده و می‌توانیم از مبدأ ترجمه کنیم. اگر فرق داشت، یعنی برنامه
        // متنِ تازه‌ای گذاشته؛ پس همان می‌شود مبدأِ جدید.
        // ═════════════════════════════════════════════════════════════════════
        private const int SlotSource = 0;   // متنِ فارسیِ مبدأ
        private const int SlotWritten = 1;  // آخرین متنی که این کلاس نوشت

        private static string Translate(object owner, int slot, string currentText)
        {
            if (string.IsNullOrWhiteSpace(currentText)) return currentText;

            string[] box;
            if (!_original.TryGetValue(owner, out box))
            {
                box = new string[2];
                box[SlotSource] = currentText;
                box[SlotWritten] = null;
                _original.Add(owner, box);
            }

            // برنامه متن را عوض کرده؟ آن‌وقت همان متنِ تازه مبدأ می‌شود.
            if (box[SlotWritten] != null && currentText != box[SlotWritten])
                box[SlotSource] = currentText;

            string translated = Lang.T(box[SlotSource] ?? currentText);
            box[SlotWritten] = translated;
            return translated;
        }

        // ─── جهتِ چیدمان ─────────────────────────────────────────────────────
        // انگلیسی چپ‌به‌راست است و بقیه راست‌به‌چپ. فقط RightToLeft عوض می‌شود؛
        // RightToLeftLayout عمداً دست نمی‌خورد، چون چیدمانِ دستیِ این پروژه
        // (مختصاتِ صریحِ کنترل‌ها در فایل‌های Designer) بر مبنای همان تنظیم شده
        // و تغییرش باعثِ جابه‌جاییِ ناخواسته‌ی کنترل‌ها می‌شود.
        public static void ApplyDirection(Form form)
        {
            if (form == null || form.IsDisposed) return;

            RightToLeft want = Lang.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
            if (form.RightToLeft != want) form.RightToLeft = want;
        }
    }
}

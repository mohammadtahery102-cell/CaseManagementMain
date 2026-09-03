using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // راست‌چین‌کردنِ همه‌ی عنوان‌ها، در همه‌ی فرم‌ها و پنجره‌ها — یکجا.
    //
    // آموزش — چرا سراسری و نه فرم‌به‌فرم: پروژه ۱۷ فرم دارد و فقط سه‌تایشان
    // UiTheme.ApplySweep را صدا می‌زدند. اگر بخواهیم در هر فرم دستی اضافه کنیم،
    // هم پرخطاست و هم فرم/دیالوگِ بعدی که ساخته شود دوباره جا می‌ماند. این کلاس
    // یک‌بار در Program نصب می‌شود و از آن به بعد هر پنجره‌ای که باز شود
    // خودکار اصلاح می‌شود.
    //
    // آموزش — دامِ آینه‌شدن (مهم‌ترین نکته‌ی این پروژه): وقتی روی فرم یا
    // TabControl مقدار RightToLeftLayout=true باشد، دستگاهِ مختصات آینه می‌شود و
    // ContentAlignment.MiddleRight بصراً «چپ» رندر می‌کند. چون بعضی فرم‌های این
    // پروژه آینه‌اند و بعضی نه (و TabControlِ آینه داخل فرمِ غیرآینه، آینه‌شدن را
    // خنثی می‌کند)، هیچ مقدارِ ثابتی درست نیست. برای همین به‌جای نوشتنِ
    // MiddleRight از ResponsiveLayout.VisualRight استفاده می‌شود که زنجیره‌ی
    // والدها را می‌شمارد و مقدارِ درست را برای همان کنترل برمی‌گرداند.
    //
    // چه چیزی دست نمی‌خورد (تا چیزی از طرحِ تأییدشده خراب نشود):
    //   • عنوان‌هایی که عمداً وسط‌چین‌اند (مثل صفحه‌ی ورود، عددِ وسطِ نمودار
    //     دونات، متنِ نوارِ صفحه‌بندی) — چون فقط عنوان‌هایی که «بصراً از چپ
    //     شروع می‌شوند» تغییر می‌کنند.
    //   • هیچ کنترلی جابه‌جا، حذف یا تغییرِ اندازه نمی‌شود؛ فقط TextAlign.
    // ═════════════════════════════════════════════════════════════════════════
    public static class RtlCaptions
    {
        private static readonly HashSet<Form> _processed = new HashSet<Form>();
        private static bool _installed;

        // یک‌بار در Program صدا زده می‌شود.
        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            Application.Idle += OnIdle;

            // با تعویضِ زبان، جهتِ خواندن عوض می‌شود؛ پس همه‌ی پنجره‌های باز
            // باید دوباره تراز شوند (وگرنه انگلیسی با عنوان‌های راست‌چین می‌ماند).
            Lang.LanguageChanged += delegate
            {
                List<Form> open = new List<Form>();
                foreach (Form f in Application.OpenForms) open.Add(f);
                foreach (Form f in open)
                    if (f != null && !f.IsDisposed) Apply(f);
            };
        }

        private static void OnIdle(object sender, EventArgs e)
        {
            // از روی مجموعه یک کپی می‌گیریم: Apply ممکن است باعثِ باز/بسته شدنِ
            // پنجره شود و تغییر مجموعه حین پیمایش، استثنا می‌دهد.
            List<Form> open = new List<Form>();
            foreach (Form f in Application.OpenForms) open.Add(f);

            foreach (Form f in open)
            {
                if (f == null || f.IsDisposed || _processed.Contains(f)) continue;

                _processed.Add(f);
                Form captured = f;
                f.Disposed += delegate { _processed.Remove(captured); };

                Apply(f);
            }
        }

        // اصلاحِ یک درختِ کنترل. عمومی است تا اگر فرمی بعد از باز شدن کنترل‌های
        // تازه ساخت، بتواند دستی هم صدا بزند.
        public static void Apply(Control root)
        {
            if (root == null) return;

            foreach (Control c in root.Controls)
            {
                AlignIfCaption(c);
                Apply(c);   // بازگشتی، تا هیچ سطحی جا نماند
            }
        }

        private static void AlignIfCaption(Control c)
        {
            Label label = c as Label;
            if (label != null) { AlignLabel(label); return; }

            // چک‌باکس و رادیو هم متنِ عنوان‌گونه دارند و باید از راست شروع شوند.
            CheckBox check = c as CheckBox;
            if (check != null) { AlignButtonBase(check, check.TextAlign, delegate (ContentAlignment a) { check.TextAlign = a; }); return; }

            RadioButton radio = c as RadioButton;
            if (radio != null) { AlignButtonBase(radio, radio.TextAlign, delegate (ContentAlignment a) { radio.TextAlign = a; }); return; }

            GroupBox group = c as GroupBox;
            if (group != null)
            {
                RightToLeft want = Lang.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
                if (group.RightToLeft != want) group.RightToLeft = want;
            }
        }

        private static void AlignLabel(Label label)
        {
            if (!ShouldMove(label, label.TextAlign)) return;
            label.TextAlign = VisualReadingEdge(label, label.TextAlign);
        }

        private static void AlignButtonBase(Control c, ContentAlignment current, Action<ContentAlignment> set)
        {
            if (!ShouldMove(c, current)) return;
            set(VisualReadingEdge(c, current));
        }

        // ═════════════════════════════════════════════════════════════════════
        // آموزش — قانونِ درست، که تجربی به‌دست آمد نه از روی حدس:
        //
        // برای متنِ داخلِ Label/CheckBox/RadioButton، آنچه ترازِ افقی را آینه
        // می‌کند خاصیتِ RightToLeft است، نه RightToLeftLayout. یعنی وقتی
        // RightToLeft=Yes باشد:
        //        MiddleLeft   →  بصراً راست رندر می‌شود
        //        MiddleRight  →  بصراً چپ  رندر می‌شود
        //
        // این با اندازه‌گیریِ پیکسلیِ متنِ رسم‌شده در چهار فرم (FrmUsers،
        // FrmApplicant، FrmCase، FrmFinance) تأیید شد و در هر چهار مورد یکسان
        // بود — چه RightToLeftLayout روشن بود چه خاموش.
        //
        // چرا این نکته مهم است: منطقِ قبلی بر پایه‌ی RightToLeftLayout بود و
        // در فرم‌هایی که آن را روشن دارند «تصادفاً» درست کار می‌کرد؛ ولی در
        // فرم‌هایی مثل FrmUsers که RightToLeftLayout=False دارند، عنوان‌ها با
        // وجود TextAlign=MiddleRight همچنان از چپ شروع می‌شدند.
        // ═════════════════════════════════════════════════════════════════════
        // ترازی که متن را به «لبه‌ی شروعِ خواندن» می‌چسباند:
        // در زبان‌های راست‌به‌چپ سمت راست، و در انگلیسی سمت چپ.
        //
        // آموزش — چرا وابسته به زبان است و نه همیشه راست: وقتی چندزبانگی اضافه
        // شد، این کلاس عنوان‌ها را در حالتِ انگلیسی هم به راست می‌چسباند، در
        // حالی که انگلیسی از چپ خوانده می‌شود؛ یعنی دو قابلیت با هم می‌جنگیدند
        // و نتیجه در تصویرِ آزمون دیده شد. حالا مبنا جهتِ زبانِ جاری است.
        private static ContentAlignment VisualReadingEdge(Control c, ContentAlignment current)
        {
            bool top = current == ContentAlignment.TopLeft ||
                       current == ContentAlignment.TopCenter ||
                       current == ContentAlignment.TopRight;
            bool bottom = current == ContentAlignment.BottomLeft ||
                          current == ContentAlignment.BottomCenter ||
                          current == ContentAlignment.BottomRight;

            // مقدارِ خامی که «راستِ بصری» می‌دهد، با توجه به آینه‌شدنِ RightToLeft
            bool mirrored = c.RightToLeft == RightToLeft.Yes;
            bool wantVisualRight = Lang.IsRightToLeft;

            // اگر آینه باشد، معنیِ مقدارها برعکس می‌شود.
            bool useLeftValue = mirrored ? wantVisualRight : !wantVisualRight;

            if (useLeftValue)
                return top ? ContentAlignment.TopLeft
                     : bottom ? ContentAlignment.BottomLeft
                     : ContentAlignment.MiddleLeft;

            return top ? ContentAlignment.TopRight
                 : bottom ? ContentAlignment.BottomRight
                 : ContentAlignment.MiddleRight;
        }

        // فقط عنوان‌هایی که در لبه‌ی «اشتباه» نشسته‌اند جابه‌جا می‌شوند.
        // وسط‌چین‌ها همیشه دست‌نخورده می‌مانند.
        private static bool ShouldMove(Control c, ContentAlignment current)
        {
            if (IsCentered(current)) return false;

            bool alignedLeftValue = current == ContentAlignment.TopLeft ||
                                    current == ContentAlignment.MiddleLeft ||
                                    current == ContentAlignment.BottomLeft;

            // با RightToLeft=Yes مقدارها آینه می‌شوند (توضیح بالا).
            bool looksLeft = c.RightToLeft == RightToLeft.Yes ? !alignedLeftValue : alignedLeftValue;

            // در زبانِ راست‌به‌چپ، عنوانی که چپ دیده می‌شود باید برود راست؛
            // در انگلیسی برعکس.
            return Lang.IsRightToLeft ? looksLeft : !looksLeft;
        }

        private static bool IsCentered(ContentAlignment a)
        {
            return a == ContentAlignment.TopCenter ||
                   a == ContentAlignment.MiddleCenter ||
                   a == ContentAlignment.BottomCenter;
        }
    }
}

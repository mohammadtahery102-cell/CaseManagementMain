using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // چارچوب چیدمانِ واکنش‌گرا (Responsive Layout Framework)
    //
    // هدف: همه‌ی فرم‌های برنامه روی هر رزولوشن (۱۳۶۶×۷۶۸ تا ۴K) و هر مقیاسِ
    // ویندوز (۱۰۰٪ تا ۲۰۰٪) بدون به‌هم‌ریختگی، بریدگی یا فضای خالیِ نامتعارف
    // نمایش داده شوند.
    //
    // ─── سه لایه‌ی این چارچوب ────────────────────────────────────────────────
    //
    // ۱) مقیاسِ DPI: هر عددِ ثابتی که در کد نوشته می‌شود (عرض ستون، ارتفاع
    //    ردیف، اندازه‌ی دکمه) بر پایه‌ی «۹۶ نقطه بر اینچ» طراحی شده است. روی
    //    نمایشگرِ ۱۵۰٪ همان عدد باید ۱٫۵ برابر شود وگرنه همه‌چیز ریز می‌شود.
    //    متدهای Scale(...) دقیقاً همین کار را می‌کنند.
    //
    // ۲) نقاط شکست (Breakpoints): بعضی تصمیم‌های چیدمانی با «مقیاس‌کردن» حل
    //    نمی‌شوند و باید در عرض‌های مختلف «متفاوت» باشند — مثلاً شبکه‌ی فیلدها
    //    روی نمایشگر باریک دو ستون و روی عریض چهار ستون. Attach با هر تغییرِ
    //    اندازه، نقطه‌ی شکستِ فعلی را محاسبه می‌کند و فقط وقتی واقعاً عوض شد
    //    به فرم خبر می‌دهد (نه در هر پیکسل تغییر، که باعث لرزش چیدمان می‌شود).
    //
    // ۳) قواعد پنجره: تمام‌صفحه‌ی خودکار + قفلِ حداقل‌اندازه، طوری که پنجره
    //    هرگز کوچک‌تر از اندازه‌ی طراحی نشود (چیدمان نشکند) و هرگز بزرگ‌تر از
    //    خودِ صفحه هم نماند (دکمه‌ها از دسترس خارج نشوند).
    //
    // ─── چرا AutoScaleMode.Dpi و نه Font ────────────────────────────────────
    // این برنامه فونتِ فارسیِ اختصاصی دارد که ممکن است روی دستگاه مشتری نصب
    // نباشد و به فونتِ جایگزین برگردد (نگاه کنید UiTheme.ResolveFontFamily).
    // با AutoScaleMode.Font، تغییرِ فونت باعث تغییرِ مقیاسِ کلِ چیدمان می‌شود —
    // یعنی روی دو دستگاه با فونت متفاوت، چیدمان متفاوت. با Dpi مبنا فقط
    // نمایشگر است که پایدار و قابل‌پیش‌بینی است.
    // ═════════════════════════════════════════════════════════════════════════
    public static class ResponsiveLayout
    {
        // نقاط شکست بر پایه‌ی عرضِ ناحیه‌ی کاریِ فرم (بر حسب پیکسلِ منطقیِ ۹۶dpi)
        public enum Breakpoint
        {
            Compact,    // < ۱۴۰۰ — لپ‌تاپ ۱۳۶۶×۷۶۸
            Medium,     // ۱۴۰۰ تا ۱۷۰۰ — ۱۶۰۰×۹۰۰
            Wide,       // ۱۷۰۰ تا ۲۲۰۰ — ۱۹۲۰×۱۰۸۰
            UltraWide   // ≥ ۲۲۰۰ — 2K/4K
        }

        public const int DesignDpi = 96;

        // ─── مقیاس DPI ───────────────────────────────────────────────────────
        private static float _cachedScale;
        private static readonly object _scaleLock = new object();

        // نسبتِ DPI فعلی به DPI طراحی. ۱٫۰ در ۱۰۰٪، ۱٫۲۵ در ۱۲۵٪، ۱٫۵ در ۱۵۰٪.
        public static float DpiScale
        {
            get
            {
                if (_cachedScale <= 0f)
                {
                    lock (_scaleLock)
                    {
                        if (_cachedScale <= 0f)
                        {
                            try
                            {
                                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                                    _cachedScale = g.DpiX / DesignDpi;
                            }
                            catch { _cachedScale = 1f; }

                            if (_cachedScale <= 0f) _cachedScale = 1f;
                        }
                    }
                }
                return _cachedScale;
            }
        }

        public static int Scale(int designPixels)
        {
            return (int)Math.Round(designPixels * DpiScale);
        }

        public static Size Scale(Size designSize)
        {
            return new Size(Scale(designSize.Width), Scale(designSize.Height));
        }

        public static Padding Scale(Padding designPadding)
        {
            return new Padding(
                Scale(designPadding.Left), Scale(designPadding.Top),
                Scale(designPadding.Right), Scale(designPadding.Bottom));
        }

        // ─── تشخیص نقطه‌ی شکست ───────────────────────────────────────────────
        // ورودی بر حسب پیکسلِ فیزیکی است و پیش از مقایسه به پیکسلِ منطقی تبدیل
        // می‌شود؛ وگرنه روی نمایشگرِ ۱۵۰٪ یک صفحه‌ی ۱۹۲۰ به‌اشتباه «خیلی عریض»
        // تشخیص داده می‌شد در حالی که فضای مفیدش معادلِ ۱۲۸۰ است.
        public static Breakpoint GetBreakpoint(int physicalWidth)
        {
            int logical = (int)Math.Round(physicalWidth / DpiScale);
            if (logical < 1400) return Breakpoint.Compact;
            if (logical < 1700) return Breakpoint.Medium;
            if (logical < 2200) return Breakpoint.Wide;
            return Breakpoint.UltraWide;
        }

        // تعداد ستون‌های پیشنهادی برای شبکه‌های فیلد — تا همه‌ی فرم‌ها یکسان
        // تصمیم بگیرند و ظاهر برنامه یکدست بماند.
        public static int ColumnsFor(Breakpoint bp)
        {
            switch (bp)
            {
                case Breakpoint.Compact:   return 2;
                case Breakpoint.Medium:    return 3;
                case Breakpoint.Wide:      return 3;
                default:                   return 4;
            }
        }

        // ─── اتصال یک فرم به چارچوب ──────────────────────────────────────────
        // designWidth/designHeight: اندازه‌ای که فرم برایش طراحی شده (۹۶dpi).
        // onBreakpointChanged: اختیاری — فقط وقتی نقطه‌ی شکست واقعاً عوض شود
        // صدا زده می‌شود (نه در هر پیکسلِ تغییرِ اندازه).
        public static void Attach(Form form, int designWidth, int designHeight,
            Action<Breakpoint> onBreakpointChanged = null)
        {
            if (form == null) return;

            // مبنای مقیاس = نمایشگر، نه فونت (دلیلش در سربرگ همین فایل).
            form.AutoScaleMode = AutoScaleMode.Dpi;

            Size scaledDesign = Scale(new Size(designWidth, designHeight));

            form.ClientSize = scaledDesign;
            form.FormBorderStyle = FormBorderStyle.Sizable;
            form.MaximizeBox = true;
            form.MinimizeBox = true;
            form.StartPosition = FormStartPosition.CenterScreen;

            // حداقل‌اندازه = اندازه‌ی طراحیِ مقیاس‌شده، ولی هرگز بزرگ‌تر از خودِ
            // صفحه (وگرنه روی نمایشگر کوچک/مقیاس بالا، بخشی از پنجره بیرون
            // می‌ماند و دسترس‌ناپذیر می‌شود).
            try
            {
                Rectangle workingArea = Screen.FromControl(form).WorkingArea;
                form.MinimumSize = new Size(
                    Math.Min(form.Size.Width, workingArea.Width),
                    Math.Min(form.Size.Height, workingArea.Height));
            }
            catch
            {
                form.MinimumSize = form.Size;
            }

            form.MaximumSize = Size.Empty;
            form.WindowState = FormWindowState.Maximized;

            if (onBreakpointChanged != null)
                HookBreakpoint(form, onBreakpointChanged);
        }

        // ردیابی نقطه‌ی شکستِ هر فرم. از جدولِ ضعیف استفاده نمی‌کنیم چون تعداد
        // فرم‌ها کم است و با بسته‌شدن فرم، ورودی‌اش حذف می‌شود.
        private static readonly Dictionary<Form, Breakpoint> _current =
            new Dictionary<Form, Breakpoint>();

        private static void HookBreakpoint(Form form, Action<Breakpoint> callback)
        {
            EventHandler handler = delegate
            {
                if (form.ClientSize.Width <= 0) return;

                Breakpoint next = GetBreakpoint(form.ClientSize.Width);

                Breakpoint previous;
                bool known = _current.TryGetValue(form, out previous);
                if (known && previous == next) return;   // تغییری نکرده؛ کاری نکن

                _current[form] = next;
                try { callback(next); }
                catch { /* خطای چیدمان نباید فرم را از کار بیندازد */ }
            };

            form.Resize += handler;
            form.Shown += handler;
            form.FormClosed += delegate { _current.Remove(form); };
        }
    }
}

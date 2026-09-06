using System.Collections.Generic;
using System.Text;

namespace CaseManagement.Helpers
{
    public enum ActivationGateOutcome
    {
        Allowed,        // ذخیره مجاز است
        AllowedWithWarning, // ذخیره مجاز است ولی هشدار نمایش داده می‌شود
        Blocked         // گذار به «فعال» مسدود است
    }

    public class ActivationGateResult
    {
        public ActivationGateOutcome Outcome = ActivationGateOutcome.Allowed;
        public List<string> MissingDocuments = new List<string>();
        public List<string> IncompleteDocuments = new List<string>();

        // Phase 7 — نمایندهٔ قانونی. پرچمِ جدا و نه یک ردیفِ دیگر در
        // MissingDocuments: نماینده سند نیست، و درهم‌آمیختنِ آن با فهرستِ
        // اسناد، پیامِ کاربر را گمراه‌کننده می‌کرد («سندِ ثبت‌نشده:
        // نمایندهٔ قانونی») و گزارش‌های آینده را هم خراب می‌کرد.
        public bool MissingRepresentative = false;

        public bool IsBlocked { get { return Outcome == ActivationGateOutcome.Blocked; } }
        public bool HasWarning { get { return Outcome == ActivationGateOutcome.AllowedWithWarning; } }

        public bool HasAnyFinding
        {
            get
            {
                return MissingDocuments.Count > 0
                    || IncompleteDocuments.Count > 0
                    || MissingRepresentative;
            }
        }

        // پیامِ واحد و دقیق — کاربر باید *همهٔ* موارد را یک‌جا ببیند، نه یکی
        // یکی با هر بار ذخیره.
        public string BuildMessage(string header)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);

            if (MissingDocuments.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("اسناد الزامیِ ثبت‌نشده:");
                foreach (string name in MissingDocuments)
                    sb.AppendLine("  • " + name);
            }

            if (IncompleteDocuments.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("اسناد الزامیِ ناقص (فایل پیوست نشده):");
                foreach (string name in IncompleteDocuments)
                    sb.AppendLine("  • " + name);
            }

            if (MissingRepresentative)
            {
                sb.AppendLine();
                sb.AppendLine("نمایندهٔ قانونی:");
                sb.AppendLine("  • «نمایندهٔ اول» ثبت نشده است.");
            }

            return sb.ToString().TrimEnd();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 5.5-A — اعتبارسنجیِ مرحله‌ایِ وضعیت خدمات.
    //
    // قاعدهٔ مصوبِ کاربر — سخت‌گیری فقط روی گذار به «فعال»:
    //
    //   متقاضی            → ذخیره آزاد
    //   در حال بررسی      → ذخیره آزاد
    //   در انتظار تایید   → ذخیره آزاد، با هشدار
    //   فعال              → اعتبارسنجیِ سخت (مسدودکننده)
    //   قطع موقت          → ذخیره آزاد
    //   قطع               → ذخیره آزاد
    //
    // «فقط گذار مسدود می‌شود، نه ویرایشِ عادیِ پرونده»: اگر پرونده از قبل
    // فعال است و کاربر فیلدِ دیگری را ویرایش می‌کند، این دروازه اجرا نمی‌شود.
    //
    // وضعیتِ تأیید (IsVerified) در این فاز عمداً بررسی نمی‌شود — زیرساختش
    // ساخته شده ولی گردشِ کاریِ تأیید هنوز وجود ندارد، پس شرط‌کردنِ آن
    // فعال‌سازیِ هر پرونده‌ای را غیرممکن می‌کرد.
    // ═══════════════════════════════════════════════════════════════════════
    public static class CaseActivationValidator
    {
        // oldStatusName: وضعیتِ ذخیره‌شدهٔ فعلی (خالی برای پروندهٔ جدید)
        // newStatusName: وضعیتی که کاربر انتخاب کرده
        //
        // requiresRepresentative (Phase 7): آیا این نوعِ درخواست بخشِ
        // نمایندهٔ قانونی دارد؟
        //
        // آموزش — چرا پارامتر، نه خواندنِ نوعِ درخواست از دیتابیس
        // در همین متد: این دروازه *پیش از* نوشتنِ UPDATE اجرا می‌شود،
        // پس دیتابیس هنوز نوعِ *قدیم* را دارد. اگر کاربر همین حالا
        // نوع را به «معلول» عوض کرده و همزمان «فعال» را زده، خواندن از
        // دیتابیس پاسخِ کهنه می‌داد. فرم نوعِ *انتخاب‌شده* را
        // می‌داند، پس همان را می‌فرستد.
        //
        // پیش‌فرض false است تا هر فراخوانِ موجود (و هشت آزمونِ فاز
        // ۵.۵-الف) دقیقاً رفتارِ قبلی‌اش را حفظ کند.
        public static ActivationGateResult Evaluate(int casId, string oldStatusName, string newStatusName,
            bool requiresRepresentative = false)
        {
            var result = new ActivationGateResult();

            var target = ReferenceDataService.FindServiceStatusByName((newStatusName ?? "").Trim());
            if (target == null) return result;   // وضعیتِ ناشناخته ⇒ ValidateForm جداگانه رد می‌کند

            bool goingActive = string.Equals(target.Code, "ACTIVE", System.StringComparison.OrdinalIgnoreCase);
            bool pendingApproval = string.Equals(target.Code, "PENDING_APPROVAL", System.StringComparison.OrdinalIgnoreCase);

            if (!goingActive && !pendingApproval) return result;   // بقیهٔ وضعیت‌ها آزادند

            // ویرایشِ عادیِ پروندهٔ از قبل فعال ⇒ گذاری در کار نیست.
            if (goingActive && string.Equals((oldStatusName ?? "").Trim(), (newStatusName ?? "").Trim(),
                    System.StringComparison.Ordinal))
                return result;

            if (casId <= 0) return result;   // پروندهٔ هنوز ذخیره‌نشده؛ سندی هم ندارد

            foreach (var missing in RequiredDocumentService.GetMissingRequiredCategories(casId))
                result.MissingDocuments.Add(missing.Name);

            foreach (var incomplete in RequiredDocumentService.GetIncompleteRequiredCategories(casId))
                result.IncompleteDocuments.Add(incomplete.Name);

            // Phase 7 — نمایندهٔ قانونی فقط برای نوعی سنجیده می‌شود که
            // بخشش را دارد؛ وگرنه فعال‌کردنِ پروندهٔ ایتام هم نماینده
            // می‌خواست. ملاک، وجودِ ردیفِ ثبت‌شده در دیتابیس است —
            // نه محتوای کادرهای فرم — چون SaveCaseRepresentatives پیش از
            // این دروازه اجرا نشده و فرم ممکن است دادهٔ ذخیره‌نشده
            // داشته باشد؛ فرم خودش پیش از فراخوانی، کادرها را هم
            // بررسی می‌کند (نگاه کنید FrmCase.PassesActivationGate).
            if (requiresRepresentative && !CaseRepresentativeService.HasPrimary(casId))
                result.MissingRepresentative = true;

            if (!result.HasAnyFinding) return result;

            result.Outcome = goingActive
                ? ActivationGateOutcome.Blocked
                : ActivationGateOutcome.AllowedWithWarning;

            return result;
        }
    }
}

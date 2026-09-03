using System;
using System.Collections.Generic;
using System.Linq;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // مدل‌های «بررسی بسته‌ی ورودی» — یک وارسیِ کاملاً فقط-خواندنی.
    //
    // هیچ چیزی نوشته نمی‌شود: نه رکوردی در دیتابیس، نه فایلی روی دیسک. تنها
    // خروجی، فهرستی از یافته‌هاست تا کاربر پیش از همگام‌سازی مشکلات را ببیند
    // و اصلاح کند.
    // ═════════════════════════════════════════════════════════════════════════

    public enum ValidationSeverity
    {
        Ready,      // سبز — مشکلی نیست
        Warning,    // زرد — می‌توان ادامه داد
        Critical    // قرمز — همگام‌سازی نباید شروع شود
    }

    // یک یافته‌ی مشخص، همراه با راهِ حلِ پیشنهادی.
    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Category { get; set; }     // دسته (مثلاً «عکس‌ها»)
        public string CaseCode { get; set; }
        public string FileName { get; set; }
        public string Description { get; set; }
        public string Suggestion { get; set; }   // راه‌حل پیشنهادی

        public string SeverityText
        {
            get
            {
                switch (Severity)
                {
                    case ValidationSeverity.Critical: return "خطای بحرانی";
                    case ValidationSeverity.Warning:  return "هشدار";
                    default:                          return "آماده";
                }
            }
        }

        // نشانه‌ی متنی — عمداً از گلیف‌های ساده استفاده می‌شود چون فونت‌های
        // فارسی معمولاً ایموجی ندارند و به‌جایش مربعِ خالی نشان می‌دهند.
        public string Icon
        {
            get
            {
                switch (Severity)
                {
                    case ValidationSeverity.Critical: return "✕";
                    case ValidationSeverity.Warning:  return "!";
                    default:                          return "✓";
                }
            }
        }
    }

    // گزارشِ کاملِ بررسی.
    public sealed class PackageValidationReport
    {
        public string RootFolder { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }

        // آموزش — رفعِ سردرگمیِ واقعیِ کاربر: چند نسخه‌ی جداگانه از این برنامه
        // (هر کدام با دیتابیسِ خودش) روی یک سیستم نصب بود؛ پرونده‌ای که در یک
        // نسخه ثبت می‌شد، در نسخه‌ی دیگر اصلاً وجود نداشت، پس عکسش «بدون صاحب»
        // نشان داده می‌شد. این دو فیلد دقیقاً می‌گویند این اجرا به کدام فایل و
        // با چند پرونده وصل است، تا این مسئله بدونِ نیاز به پرس‌وجوی جداگانه
        // بلافاصله روی همان صفحه‌ی گزارش دیده شود.
        public string DatabasePath { get; set; }
        public int TotalCasesInDatabase { get; set; }

        public List<ValidationIssue> Issues { get; set; }

        // ─── شمارنده‌های خلاصه ───────────────────────────────────────────────
        public int TotalCasesInPackage { get; set; }
        public int TotalMembersInPackage { get; set; }
        public int TotalPhotos { get; set; }
        public int TotalDocuments { get; set; }

        public int ReadyCases { get; set; }
        public int CasesWithWarnings { get; set; }
        public int CasesWithErrors { get; set; }

        public int MissingPhotos { get; set; }
        public int MissingDocuments { get; set; }
        public int DuplicateFiles { get; set; }
        public int DuplicateCaseCodes { get; set; }
        public int UnsupportedFiles { get; set; }
        public int CorruptedFiles { get; set; }
        public int InvalidFileNames { get; set; }
        public int LargeImages { get; set; }
        public int SmallImages { get; set; }
        public int ResolutionWarnings { get; set; }
        public int UnusedFiles { get; set; }
        public int OrphanFiles { get; set; }
        public int EmptyFolders { get; set; }
        public int CasesWithoutHtmlRecord { get; set; }
        public int HtmlRecordsWithoutPhoto { get; set; }
        public int HtmlRecordsWithoutDocument { get; set; }

        // برآوردِ زمانِ همگام‌سازی، بر پایه‌ی اندازه‌گیریِ واقعی.
        public TimeSpan EstimatedSyncTime { get; set; }

        public PackageValidationReport()
        {
            Issues = new List<ValidationIssue>();
            StartedAt = DateTime.Now;
        }

        public TimeSpan Duration { get { return FinishedAt - StartedAt; } }

        public int CriticalCount { get { return Issues.Count(i => i.Severity == ValidationSeverity.Critical); } }
        public int WarningCount  { get { return Issues.Count(i => i.Severity == ValidationSeverity.Warning); } }

        // آیا همگام‌سازی مجاز است؟ فقط خطای بحرانی مانع است.
        public bool CanSynchronize { get { return CriticalCount == 0; } }

        // آیا نیاز به تأییدِ صریحِ کاربر هست؟ (فقط هشدار دارد)
        public bool NeedsConfirmation { get { return CriticalCount == 0 && WarningCount > 0; } }

        public ValidationSeverity OverallSeverity
        {
            get
            {
                if (CriticalCount > 0) return ValidationSeverity.Critical;
                if (WarningCount > 0) return ValidationSeverity.Warning;
                return ValidationSeverity.Ready;
            }
        }

        public string OverallText
        {
            get
            {
                switch (OverallSeverity)
                {
                    case ValidationSeverity.Critical:
                        return "خطای بحرانی — همگام‌سازی نباید شروع شود";
                    case ValidationSeverity.Warning:
                        return "با هشدار — می‌توانید با تأیید خودتان ادامه دهید";
                    default:
                        return "آماده‌ی همگام‌سازی";
                }
            }
        }

        public void Add(ValidationSeverity severity, string category, string caseCode,
                        string fileName, string description, string suggestion)
        {
            Issues.Add(new ValidationIssue
            {
                Severity = severity,
                Category = category,
                CaseCode = caseCode ?? "",
                FileName = fileName ?? "",
                Description = description ?? "",
                Suggestion = suggestion ?? ""
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace CaseManagement.Sync
{
    // ─────────────────────────────────────────────────────────────────────────
    // مدل‌های مشترک موتور همگام‌سازی داده (Data Synchronization Engine).
    //
    // معماری کلی (مستقل از منبع): یک Provider (فعلاً HTML) فایل ورودی را به
    // «رکوردهای خام» تبدیل می‌کند → Comparer آن‌ها را با دیتابیس مقایسه و یک
    // «SyncPlan» می‌سازد → کاربر در Wizard تغییرات را بازبینی/انتخاب می‌کند →
    // SyncEngine با Backup + Transaction + Rollback آن‌ها را اعمال و یک
    // «SyncReport» می‌سازد. هیچ داده‌ای بدون تأیید نوشته نمی‌شود و هیچ رکوردی
    // حذف نمی‌شود (فقط New/Update).
    // ─────────────────────────────────────────────────────────────────────────

    // نوع موجودیت هر رکورد همگام‌سازی.
    public enum SyncEntity
    {
        Guardian,     // سرپرست (نگاشت به TblCase)
        FamilyMember  // عضو خانواده (نگاشت به TblFamily)
    }

    // اقدام تشخیص‌داده‌شده برای هر رکورد پس از مقایسه با دیتابیس.
    public enum SyncAction
    {
        New,        // در دیتابیس نیست → ثبت جدید
        Update,     // هست و حداقل یک فیلد تغییر کرده → بروزرسانی
        Unchanged,  // هست و هیچ فرقی ندارد → کاری انجام نمی‌شود
        Duplicate,  // کد عمومی در خود فایل تکراری است
        Error       // رکورد دارای خطای اعتبارسنجی (مثلاً کد عمومی خالی)
    }

    // تغییر یک فیلد مشخص (برای نمایش «مقدار قبلی/جدید» و انتخاب توسط کاربر).
    public sealed class FieldChange
    {
        public string FieldName { get; set; }     // نام ستون دیتابیس
        public string DisplayName { get; set; }    // عنوان فارسی
        public string OldValue { get; set; }       // مقدار فعلی در دیتابیس
        public string NewValue { get; set; }       // مقدار جدید از فایل
        public bool Selected { get; set; }         // آیا این تغییر اعمال شود؟

        public FieldChange()
        {
            Selected = true; // پیش‌فرض: اعمال شود (کاربر می‌تواند بردارد)
        }
    }

    // یک رکورد همگام‌سازی (یک سرپرست یا یک عضو خانواده).
    public sealed class SyncRecord
    {
        public SyncEntity Entity { get; set; }
        public SyncAction Action { get; set; }

        public string PublicCode { get; set; }     // کد عمومی خانواده (کلید ارتباط)
        public string Title { get; set; }          // متن نمایشی (نام)
        public string MemberKey { get; set; }      // کلید هویت عضو (تذکره یا نام+نام‌پدر)

        // مقادیر منبع نگاشت‌شده به نام ستون دیتابیس (فقط فیلدهای قابل‌همگام‌سازی).
        public Dictionary<string, string> SourceValues { get; set; }

        // مقادیر «پیش‌فرضِ فقط‌هنگام‌ثبت»: فقط برای رکوردهای جدید (Insert) نوشته
        // می‌شوند و هرگز در مقایسه/بروزرسانی دخالت نمی‌کنند. کاربرد: تعیین یک
        // مقدار پیش‌فرض منطقی برای ستونی که در فایل نیست (مثل PhysicalStatus="سالم")
        // بدون این‌که در همگام‌سازی‌های بعدی ویرایش دستی کاربر را بازنویسی کند.
        public Dictionary<string, string> InsertOnlyValues { get; set; }

        // تغییرات فیلدی (برای Update). برای New این لیست خالی است و همه‌ی
        // SourceValues درج می‌شوند.
        public List<FieldChange> Changes { get; set; }

        public int ExistingDbId { get; set; }      // CasID/FamID موجود (۰ اگر جدید)
        public int ParentCasId { get; set; }       // برای اعضا: CasID خانواده در دیتابیس (۰ اگر خانواده جدید)
        public string ErrorMessage { get; set; }

        // آیا این رکورد اعمال شود؟ (چک‌باکس سطح رکورد در Wizard)
        public bool Selected { get; set; }

        public SyncRecord()
        {
            SourceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            InsertOnlyValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Changes = new List<FieldChange>();
            Selected = true;
        }

        // آیا این رکورد چیزی برای اعمال دارد؟ (New یا Update با حداقل یک تغییر انتخاب‌شده)
        public bool HasApplicableWork
        {
            get
            {
                if (Action == SyncAction.New) return true;
                if (Action == SyncAction.Update) return Changes.Any(c => c.Selected);
                return false;
            }
        }
    }

    // نتیجه‌ی تجزیه‌ی فایل توسط Provider — رکوردهای خام قبل از مقایسه با دیتابیس.
    public sealed class ParsedSyncData
    {
        public List<SyncRecord> Guardians { get; set; }
        public List<SyncRecord> Members { get; set; }

        public ParsedSyncData()
        {
            Guardians = new List<SyncRecord>();
            Members = new List<SyncRecord>();
        }
    }

    // برنامه‌ی نهایی همگام‌سازی — خروجی Comparer، ورودی Engine.
    public sealed class SyncPlan
    {
        public List<SyncRecord> Guardians { get; set; }
        public List<SyncRecord> Members { get; set; }
        public List<string> ValidationErrors { get; set; }

        public SyncPlan()
        {
            Guardians = new List<SyncRecord>();
            Members = new List<SyncRecord>();
            ValidationErrors = new List<string>();
        }

        private static int Count(IEnumerable<SyncRecord> src, SyncAction a)
        {
            return src.Count(r => r.Action == a);
        }

        // ─── خلاصه‌ی آماری (برای صفحه‌ی گزارش پیش از همگام‌سازی) ─────────────
        public int TotalGuardians { get { return Guardians.Count; } }
        public int TotalMembers { get { return Members.Count; } }

        public int NewGuardians { get { return Count(Guardians, SyncAction.New); } }
        public int UpdatedGuardians { get { return Count(Guardians, SyncAction.Update); } }
        public int UnchangedGuardians { get { return Count(Guardians, SyncAction.Unchanged); } }
        public int DuplicateGuardians { get { return Count(Guardians, SyncAction.Duplicate); } }
        public int ErrorGuardians { get { return Count(Guardians, SyncAction.Error); } }

        public int NewMembers { get { return Count(Members, SyncAction.New); } }
        public int UpdatedMembers { get { return Count(Members, SyncAction.Update); } }
        public int UnchangedMembers { get { return Count(Members, SyncAction.Unchanged); } }
        public int DuplicateMembers { get { return Count(Members, SyncAction.Duplicate); } }
        public int ErrorMembers { get { return Count(Members, SyncAction.Error); } }

        public int TotalRecords { get { return TotalGuardians + TotalMembers; } }
        public bool HasErrors { get { return ValidationErrors.Count > 0 || ErrorGuardians > 0 || ErrorMembers > 0; } }
    }

    // گزارش نتیجه‌ی همگام‌سازی (صفحه‌ی آخر Wizard).
    public sealed class SyncReport
    {
        public bool Success { get; set; }
        public bool RolledBack { get; set; }
        public string BackupPath { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }

        public int GuardiansInserted { get; set; }
        public int GuardiansUpdated { get; set; }
        public int MembersInserted { get; set; }
        public int MembersUpdated { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }

        public string ErrorMessage { get; set; }
        public List<string> Log { get; set; }

        public SyncReport()
        {
            Log = new List<string>();
            StartedAt = DateTime.Now;
        }

        public TimeSpan Duration { get { return FinishedAt - StartedAt; } }

        public void Add(string line)
        {
            Log.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line);
        }
    }

    // گزارش پیشرفت برای Progress Bar (پردازش ده‌ها هزار رکورد).
    public sealed class SyncProgress
    {
        public string Phase { get; set; }   // مثل: «تجزیه فایل»، «مقایسه»، «اعمال»
        public int Current { get; set; }
        public int Total { get; set; }

        public int Percent
        {
            get { return Total <= 0 ? 0 : (int)(100L * Current / Total); }
        }
    }

    // منبع ورودی همگام‌سازی (برای Provider HTML: دو فایل).
    public sealed class SyncSource
    {
        public string GuardiansFilePath { get; set; }
        public string MembersFilePath { get; set; }
    }
}

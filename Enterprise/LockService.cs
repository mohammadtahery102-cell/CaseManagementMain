using System;
using System.Data;
using System.Data.SQLite;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // نتیجه تلاش برای گرفتن قفل.
    public class LockResult
    {
        public bool     Acquired  { get; set; }
        public int      LockID    { get; set; }
        public string   HeldBy    { get; set; }   // اگر قفل دست کس دیگری باشد
        public string   Machine   { get; set; }
        public DateTime? Since    { get; set; }

        public string DeniedMessage
        {
            get
            {
                string who = string.IsNullOrWhiteSpace(HeldBy) ? "کاربر دیگری" : HeldBy;
                string when = Since.HasValue ? " از " + Since.Value.ToString("HH:mm") : "";
                string pc = string.IsNullOrWhiteSpace(Machine) ? "" : " (" + Machine + ")";

                return "این رکورد هم‌اکنون توسط " + who + pc + when +
                       " در حال ویرایش است. لطفاً بعداً تلاش کنید.";
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ویژگی ۵ — قفل رکورد.
    //
    // هدف: جلوگیری از «آخرین ذخیره برنده است» وقتی دو کاربر هم‌زمان یک پرونده
    // را ویرایش می‌کنند.
    //
    // نکات طراحی:
    //  • قفل مدت‌دار است (پیش‌فرض ۱۵ دقیقه) و با Heartbeat تمدید می‌شود؛ اگر
    //    برنامه ناگهانی بسته شود، قفل خودبه‌خود منقضی می‌شود و رکورد آزاد
    //    می‌گردد — سیستم هرگز برای همیشه قفل نمی‌ماند.
    //  • گرفتن قفل روی رکوردی که خودِ همان کاربر قفل کرده، موفق است (تمدید).
    //  • هیچ فرم موجودی مجبور به استفاده از این سرویس نیست؛ فرم‌هایی که آن را
    //    صدا نزنند دقیقاً مثل قبل کار می‌کنند (سازگاری عقب‌رو).
    // ─────────────────────────────────────────────────────────────────────────
    public static class LockService
    {
        // مدت اعتبار قفل به دقیقه — از تنظیمات خوانده می‌شود تا قابل تغییر باشد.
        public const string SettingLockMinutes = "RecordLockMinutes";

        public static int LockMinutes
        {
            get
            {
                int minutes = SettingsHelper.GetInt(SettingLockMinutes, 15);
                return minutes < 1 ? 15 : minutes;
            }
        }

        // ─── گرفتن قفل ────────────────────────────────────────────────────
        public static LockResult TryAcquire(string entityName, int entityId)
        {
            LockResult result = new LockResult();

            if (string.IsNullOrWhiteSpace(entityName) || entityId <= 0)
            {
                // چیزی برای قفل کردن نیست (مثلاً رکورد هنوز ذخیره نشده).
                result.Acquired = true;
                return result;
            }

            try
            {
                // قفل‌های منقضی این رکورد کنار می‌روند تا مسیر برای قفل جدید باز شود.
                EntDb.Exec(@"
DELETE FROM EntRecordLock
WHERE  EntityName = @Entity AND EntityID = @Id AND ExpiresAt <= datetime('now');",
                    "@Entity", entityName, "@Id", entityId);

                DataTable existing = EntDb.Query(@"
SELECT LockID, UserID, Username, MachineName, AcquiredAt
FROM   EntRecordLock
WHERE  EntityName = @Entity AND EntityID = @Id;",
                    "@Entity", entityName, "@Id", entityId);

                if (existing.Rows.Count > 0)
                {
                    DataRow row = existing.Rows[0];
                    int ownerId = EntDb.ToInt(row["UserID"]);

                    // قفلِ خودِ همان کاربر → تمدید می‌شود.
                    if (ownerId == SecurityContext.UserId && ownerId > 0)
                    {
                        result.LockID = EntDb.ToInt(row["LockID"]);
                        result.Acquired = true;
                        Heartbeat(result.LockID);
                        return result;
                    }

                    result.Acquired = false;
                    result.LockID   = EntDb.ToInt(row["LockID"]);
                    result.HeldBy   = EntDb.ToText(row["Username"]);
                    result.Machine  = EntDb.ToText(row["MachineName"]);
                    result.Since    = EntDb.ToDate(row["AcquiredAt"]);
                    return result;
                }

                long lockId = EntDb.Insert(@"
INSERT INTO EntRecordLock
    (EntityName, EntityID, UserID, Username, MachineName, CenterID, ExpiresAt)
VALUES
    (@Entity, @Id, @UserId, @User, @Machine, @Center,
     datetime('now', '+' || @Minutes || ' minutes'));",
                    "@Entity",  entityName,
                    "@Id",      entityId,
                    "@UserId",  SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : null,
                    "@User",    SecurityContext.Username,
                    "@Machine", SafeMachineName(),
                    "@Center",  SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : null,
                    "@Minutes", LockMinutes);

                result.Acquired = lockId > 0;
                result.LockID   = (int)lockId;
                return result;
            }
            catch (SQLiteException)
            {
                // برخورد هم‌زمانی: کاربر دیگری دقیقاً در همین لحظه قفل را گرفت.
                // (UNIQUE روی EntityName/EntityID این حالت را تضمین می‌کند.)
                return Describe(entityName, entityId);
            }
            catch
            {
                // خطای غیرمنتظره نباید کاربر را از کار بیندازد؛ قفل نادیده
                // گرفته می‌شود و رفتار مثل قبلِ این ویژگی خواهد بود.
                result.Acquired = true;
                return result;
            }
        }

        // تمدید قفل — از تایمر فرم صدا زده می‌شود تا ویرایش طولانی قطع نشود.
        public static void Heartbeat(int lockId)
        {
            if (lockId <= 0) return;

            try
            {
                EntDb.Exec(@"
UPDATE EntRecordLock
SET    HeartbeatAt = datetime('now'),
       ExpiresAt   = datetime('now', '+' || @Minutes || ' minutes')
WHERE  LockID = @Id;", "@Minutes", LockMinutes, "@Id", lockId);
            }
            catch { /* تمدید قفل غیرحیاتی است */ }
        }

        // ─── آزادسازی ─────────────────────────────────────────────────────
        public static void Release(int lockId)
        {
            if (lockId <= 0) return;

            try
            {
                // فقط صاحب قفل می‌تواند آن را آزاد کند (مگر از مسیر ForceRelease).
                EntDb.Exec(@"
DELETE FROM EntRecordLock
WHERE  LockID = @Id AND (IFNULL(UserID, 0) = @UserId OR IFNULL(UserID, 0) = 0);",
                    "@Id", lockId, "@UserId", SecurityContext.UserId);
            }
            catch { /* آزادسازی ناموفق: قفل با انقضا آزاد خواهد شد */ }
        }

        public static void Release(string entityName, int entityId)
        {
            if (string.IsNullOrWhiteSpace(entityName) || entityId <= 0) return;

            try
            {
                EntDb.Exec(@"
DELETE FROM EntRecordLock
WHERE  EntityName = @Entity AND EntityID = @Id AND IFNULL(UserID, 0) = @UserId;",
                    "@Entity", entityName, "@Id", entityId, "@UserId", SecurityContext.UserId);
            }
            catch { }
        }

        // آزادسازی اجباری توسط مدیر سیستم — برای قفل جامانده.
        public static WorkflowActionResult ForceRelease(int lockId)
        {
            if (!SecurityContext.IsAdmin())
                return WorkflowActionResult.Fail("آزادسازی اجباری قفل فقط برای مدیر سیستم مجاز است.");

            DataTable table = EntDb.Query(
                "SELECT EntityName, EntityID, Username FROM EntRecordLock WHERE LockID = @Id;",
                "@Id", lockId);

            if (table.Rows.Count == 0)
                return WorkflowActionResult.Fail("قفل پیدا نشد (شاید خودش آزاد شده است).");

            DataRow row = table.Rows[0];

            EntDb.Exec("DELETE FROM EntRecordLock WHERE LockID = @Id;", "@Id", lockId);

            // این یک اقدام حساس امنیتی است و در هر دو سامانه ثبت می‌شود.
            AuditLogger.Log("LockForceRelease",
                            EntDb.ToText(row["EntityName"]),
                            EntDb.ToInt(row["EntityID"]),
                            EntDb.ToText(row["Username"]),
                            SecurityContext.Username);

            SecurityAudit.LockOverride(EntDb.ToText(row["EntityName"]),
                                       EntDb.ToInt(row["EntityID"]),
                                       EntDb.ToText(row["Username"]));

            return new WorkflowActionResult { Applied = true, Message = "قفل آزاد شد." };
        }

        // آزادسازی همه قفل‌های کاربر جاری — هنگام خروج از حساب.
        public static void ReleaseAllForCurrentUser()
        {
            if (SecurityContext.UserId <= 0) return;

            try
            {
                EntDb.Exec("DELETE FROM EntRecordLock WHERE UserID = @UserId;",
                    "@UserId", SecurityContext.UserId);
            }
            catch { }
        }

        // ─── وضعیت ────────────────────────────────────────────────────────
        // آیا رکورد توسط کاربر دیگری قفل است؟ (بدون تلاش برای گرفتن قفل)
        public static LockResult Describe(string entityName, int entityId)
        {
            LockResult result = new LockResult { Acquired = true };

            try
            {
                DataTable table = EntDb.Query(@"
SELECT LockID, UserID, Username, MachineName, AcquiredAt
FROM   EntRecordLock
WHERE  EntityName = @Entity AND EntityID = @Id AND ExpiresAt > datetime('now');",
                    "@Entity", entityName, "@Id", entityId);

                if (table.Rows.Count == 0) return result;

                DataRow row = table.Rows[0];
                int ownerId = EntDb.ToInt(row["UserID"]);

                result.LockID  = EntDb.ToInt(row["LockID"]);
                result.HeldBy  = EntDb.ToText(row["Username"]);
                result.Machine = EntDb.ToText(row["MachineName"]);
                result.Since   = EntDb.ToDate(row["AcquiredAt"]);

                // قفلِ خود کاربر مانع کار او نیست.
                result.Acquired = ownerId == SecurityContext.UserId;
            }
            catch
            {
                result.Acquired = true;
            }

            return result;
        }

        // فهرست قفل‌های فعال — برای فرم مدیریت.
        public static DataTable GetActiveLocks()
        {
            return EntDb.Query(@"
SELECT LockID      AS 'شناسه',
       EntityName  AS 'موجودیت',
       EntityID    AS 'شناسه رکورد',
       Username    AS 'کاربر',
       MachineName AS 'رایانه',
       AcquiredAt  AS 'زمان گرفتن',
       HeartbeatAt AS 'آخرین فعالیت',
       ExpiresAt   AS 'انقضا'
FROM   EntRecordLock
WHERE  ExpiresAt > datetime('now')
  AND  (@Center = 0 OR IFNULL(CenterID, 0) = @Center)
ORDER  BY LockID DESC;", "@Center", SecurityContext.CenterFilterId);
        }

        // پاک‌سازی قفل‌های منقضی — بی‌خطر و idempotent.
        public static int PurgeExpired()
        {
            try
            {
                return EntDb.Exec("DELETE FROM EntRecordLock WHERE ExpiresAt <= datetime('now');");
            }
            catch
            {
                return 0;
            }
        }

        private static string SafeMachineName()
        {
            try { return Environment.MachineName; }
            catch { return ""; }
        }
    }
}

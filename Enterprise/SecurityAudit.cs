using System;
using System.Data;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ─────────────────────────────────────────────────────────────────────────
    // ویژگی ۷ — ممیزی امنیتی.
    //
    // رویدادهای امنیتی (ورود، ورود ناموفق، خروج، رد دسترسی، تغییر رمز/نقش،
    // آزادسازی اجباری قفل) جداگانه از رویدادهای کاری ثبت می‌شوند.
    //
    // مثل AuditLogger موجود، هیچ خطایی از این کلاس به بیرون درز نمی‌کند؛ ثبت
    // ممیزی هرگز نباید باعث شکست ورود کاربر یا هر عملیات دیگری شود.
    // ─────────────────────────────────────────────────────────────────────────
    public static class SecurityAudit
    {
        // انواع رویداد
        public const string EventLogin            = "ورود";
        public const string EventLoginFailed      = "ورود ناموفق";
        public const string EventLogout           = "خروج";
        public const string EventPermissionDenied = "رد دسترسی";
        public const string EventPasswordChanged  = "تغییر رمز";
        public const string EventUserChanged      = "تغییر کاربر";
        public const string EventLockOverride     = "آزادسازی اجباری قفل";
        public const string EventSensitive        = "عملیات حساس";

        // سطح اهمیت
        public const string SeverityInfo     = "عادی";
        public const string SeverityWarning  = "هشدار";
        public const string SeverityCritical = "بحرانی";

        // ─── ثبت ──────────────────────────────────────────────────────────
        public static void Log(string eventType, string severity, bool success,
                               string detail, string entityName = null, int entityId = 0,
                               string username = null, int userId = 0)
        {
            try
            {
                // اگر نام کاربر داده نشده، از کاربر جاری استفاده می‌شود. در
                // «ورود ناموفق» هنوز کسی وارد نشده، پس نام صراحتاً پاس می‌شود.
                string user = string.IsNullOrWhiteSpace(username)
                    ? SecurityContext.Username
                    : username;

                int id = userId > 0 ? userId : SecurityContext.UserId;

                EntDb.Exec(@"
INSERT INTO EntSecurityEvent
    (EventType, Severity, Success, UserID, Username, MachineName,
     EntityName, EntityID, Detail, CenterID)
VALUES
    (@Type, @Severity, @Success, @UserId, @User, @Machine,
     @Entity, @EntityId, @Detail, @Center);",
                    "@Type",     eventType,
                    "@Severity", string.IsNullOrWhiteSpace(severity) ? SeverityInfo : severity,
                    "@Success",  success ? 1 : 0,
                    "@UserId",   id > 0 ? (object)id : null,
                    "@User",     user,
                    "@Machine",  SafeMachineName(),
                    "@Entity",   entityName,
                    "@EntityId", entityId > 0 ? (object)entityId : null,
                    "@Detail",   detail,
                    "@Center",   SecurityContext.CurrentCenterId > 0
                                     ? (object)SecurityContext.CurrentCenterId : null);
            }
            catch
            {
                // ثبت ممیزی هرگز نباید عملیات اصلی را متوقف کند.
            }
        }

        // ─── میان‌برهای پرکاربرد ─────────────────────────────────────────
        public static void LoginSucceeded(int userId, string username, string role)
        {
            Log(EventLogin, SeverityInfo, true, "نقش: " + (role ?? ""),
                null, 0, username, userId);
        }

        public static void LoginFailed(string username, string reason)
        {
            Log(EventLoginFailed, SeverityWarning, false, reason, null, 0, username, 0);
        }

        public static void Logout()
        {
            Log(EventLogout, SeverityInfo, true, null);
        }

        public static void PermissionDenied(string permissionKey, string entityName = null, int entityId = 0)
        {
            Log(EventPermissionDenied, SeverityWarning, false,
                "مجوز لازم: " + (permissionKey ?? ""), entityName, entityId);
        }

        public static void PasswordChanged(string targetUsername)
        {
            Log(EventPasswordChanged, SeverityWarning, true,
                "کاربر هدف: " + (targetUsername ?? ""));
        }

        public static void UserChanged(string detail)
        {
            Log(EventUserChanged, SeverityWarning, true, detail);
        }

        public static void LockOverride(string entityName, int entityId, string previousHolder)
        {
            Log(EventLockOverride, SeverityCritical, true,
                "قفل کاربر «" + (previousHolder ?? "") + "» به‌اجبار آزاد شد",
                entityName, entityId);
        }

        public static void Sensitive(string detail, string entityName = null, int entityId = 0)
        {
            Log(EventSensitive, SeverityCritical, true, detail, entityName, entityId);
        }

        // ─── خواندن ───────────────────────────────────────────────────────
        public static DataTable GetEvents(string eventType, string severity,
                                          string username, int lastDays)
        {
            return EntDb.Query(@"
SELECT EventID     AS 'شناسه',
       CreatedAt   AS 'تاریخ',
       EventType   AS 'رویداد',
       Severity    AS 'اهمیت',
       CASE Success WHEN 1 THEN 'موفق' ELSE 'ناموفق' END AS 'نتیجه',
       Username    AS 'کاربر',
       MachineName AS 'رایانه',
       EntityName  AS 'موجودیت',
       EntityID    AS 'شناسه رکورد',
       Detail      AS 'جزئیات'
FROM   EntSecurityEvent
WHERE  (IFNULL(@Type, '')     = '' OR EventType = @Type)
  AND  (IFNULL(@Severity, '') = '' OR Severity  = @Severity)
  AND  (IFNULL(@User, '')     = '' OR Username LIKE '%' || @User || '%')
  AND  (@Days <= 0 OR CreatedAt >= datetime('now', '-' || @Days || ' days'))
  -- آموزش — چرا رویدادهای بدون مرکز همیشه دیده می‌شوند: رویدادهایی مثل
  -- «ورود ناموفق» پیش از انتخاب مرکز رخ می‌دهند و CenterID ندارند. اگر
  -- مثل داده‌های کاری فیلتر می‌شدند، هیچ مدیری هرگز تلاش‌های ورود ناموفق
  -- را نمی‌دید — یعنی دقیقاً مهم‌ترین رویداد امنیتی پنهان می‌ماند.
  AND  (@Center = 0 OR IFNULL(CenterID, 0) = 0 OR CenterID = @Center)
ORDER  BY EventID DESC
LIMIT  2000;",
                "@Type",     eventType,
                "@Severity", severity,
                "@User",     username,
                "@Days",     lastDays,
                "@Center",   SecurityContext.CenterFilterId);
        }

        // تعداد ورودهای ناموفق یک کاربر در بازه اخیر — برای تشخیص تلاش نفوذ.
        public static int RecentFailedLogins(string username, int minutes)
        {
            return EntDb.ToInt(EntDb.Scalar(@"
SELECT COUNT(*) FROM EntSecurityEvent
WHERE  EventType = @Type AND Username = @User
  AND  CreatedAt >= datetime('now', '-' || @Minutes || ' minutes');",
                "@Type", EventLoginFailed, "@User", username, "@Minutes", minutes <= 0 ? 15 : minutes));
        }

        private static string SafeMachineName()
        {
            try { return Environment.MachineName; }
            catch { return ""; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ─────────────────────────────────────────────────────────────────────────
    // ویژگی ۱۰ — مدیریت ماژول‌ها (فعال/غیرفعال کردن بخش‌های نرم‌افزار).
    //
    // ترتیب تصمیم‌گیری:
    //   ۱. ماژول ناشناخته → فعال (سازگاری عقب‌رو؛ بخش‌های ثبت‌نشده هرگز پنهان
    //      نمی‌شوند).
    //   ۲. ماژول پایه (IsCore) → همیشه فعال، تا راه بازگشت به تنظیمات و
    //      مدیریت کاربران هرگز بسته نشود.
    //   ۳. مدیر کل → همیشه فعال (جلوگیری از قفل شدن کامل سیستم).
    //   ۴. خاموشی سراسری ماژول → غیرفعال برای همه.
    //   ۵. تنظیم اختصاصی کاربر → بر نقش اولویت دارد.
    //   ۶. تنظیم نقش.
    //   ۷. پیش‌فرض → فعال.
    //
    // تفاوت این ویژگی با ماتریس مجوزها (ویژگی ۹): این‌جا «در دسترس بودن بخش»
    // تعیین می‌شود (چه چیزی در منو دیده شود)، آن‌جا «اجازه انجام کار» بررسی
    // می‌گردد. این دو مکمل‌اند و جای هم را نمی‌گیرند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ModuleService
    {
        // کلیدهای ماژول — همان بخش‌های منوی کناری.
        public const string ModuleCases       = "Cases";
        public const string ModuleApplicants  = "Applicants";
        public const string ModuleSearch      = "Search";
        public const string ModuleFinance     = "Finance";
        public const string ModuleAccounting  = "Accounting";
        public const string ModuleSync        = "Sync";
        public const string ModuleDuplicates  = "Duplicates";
        public const string ModuleDataQuality = "DataQuality";
        public const string ModuleBarcode     = "Barcode";
        public const string ModuleArchive     = "Archive";
        public const string ModuleAuditReport = "AuditReport";
        public const string ModuleReportBuilder = "ReportBuilder";

        public const string ModuleWorkflow    = "Workflow";
        public const string ModuleApprovals   = "Approvals";
        public const string ModuleTasks       = "Tasks";
        public const string ModuleRules       = "Rules";
        public const string ModuleLocks       = "Locks";
        public const string ModuleVersions    = "Versions";
        public const string ModuleSecurity    = "SecurityAudit";
        public const string ModuleErrors      = "ErrorLog";

        public const string ModuleUsers       = "Users";
        public const string ModulePermissions = "Permissions";
        public const string ModuleModules     = "Modules";
        public const string ModuleSettings    = "Settings";

        private static readonly object Sync = new object();

        private static Dictionary<string, bool> _cache;
        private static int    _cacheUserId = -1;
        private static string _cacheRole;

        // ─── پرسش اصلی ────────────────────────────────────────────────────
        public static bool IsEnabled(string moduleKey)
        {
            if (string.IsNullOrWhiteSpace(moduleKey)) return true;

            try
            {
                Dictionary<string, bool> cache = GetCache();

                bool enabled;
                if (cache.TryGetValue(moduleKey, out enabled))
                    return enabled;

                // ۱) ماژول ثبت‌نشده همیشه در دسترس است.
                return true;
            }
            catch
            {
                // خطا نباید باعث ناپدید شدن بخشی از برنامه شود.
                return true;
            }
        }

        private static Dictionary<string, bool> GetCache()
        {
            lock (Sync)
            {
                bool sameUser = _cacheUserId == SecurityContext.UserId &&
                                string.Equals(_cacheRole, SecurityContext.Role, StringComparison.Ordinal);

                if (_cache != null && sameUser) return _cache;

                Dictionary<string, bool> map =
                    new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                DataTable modules = EntDb.Query(
                    "SELECT ModuleKey, IsCore, IsEnabled FROM EntModule;");

                DataTable roleRows = EntDb.Query(
                    "SELECT ModuleKey, IsEnabled FROM EntRoleModule WHERE RoleName = @Role;",
                    "@Role", SecurityContext.Role ?? "");

                DataTable userRows = SecurityContext.UserId > 0
                    ? EntDb.Query("SELECT ModuleKey, IsEnabled FROM EntUserModule WHERE UserID = @Id;",
                                  "@Id", SecurityContext.UserId)
                    : new DataTable();

                bool isSuperAdmin = SecurityContext.IsSuperAdmin();

                foreach (DataRow module in modules.Rows)
                {
                    string key = EntDb.ToText(module["ModuleKey"]);

                    // ۲) ماژول پایه و ۳) مدیر کل
                    if (EntDb.ToBool(module["IsCore"]) || isSuperAdmin)
                    {
                        map[key] = true;
                        continue;
                    }

                    // ۴) خاموشی سراسری
                    if (!EntDb.ToBool(module["IsEnabled"]))
                    {
                        map[key] = false;
                        continue;
                    }

                    // ۶) نقش، سپس ۵) کاربر (کاربر بر نقش اولویت دارد)
                    bool enabled = true;

                    bool? roleValue = Find(roleRows, "ModuleKey", key);
                    if (roleValue.HasValue) enabled = roleValue.Value;

                    bool? userValue = Find(userRows, "ModuleKey", key);
                    if (userValue.HasValue) enabled = userValue.Value;

                    map[key] = enabled;
                }

                _cache       = map;
                _cacheUserId = SecurityContext.UserId;
                _cacheRole   = SecurityContext.Role;

                return _cache;
            }
        }

        private static bool? Find(DataTable table, string keyColumn, string key)
        {
            if (table == null || table.Rows.Count == 0) return null;

            foreach (DataRow row in table.Rows)
                if (string.Equals(EntDb.ToText(row[keyColumn]), key, StringComparison.Ordinal))
                    return EntDb.ToBool(row["IsEnabled"]);

            return null;
        }

        public static void InvalidateCache()
        {
            lock (Sync)
            {
                _cache       = null;
                _cacheUserId = -1;
                _cacheRole   = null;
            }
        }

        // ─── خواندن برای فرم مدیریت ───────────────────────────────────────
        public static DataTable GetModules()
        {
            return EntDb.Query(@"
SELECT ModuleID  AS 'شناسه',
       ModuleKey AS 'کلید',
       Name      AS 'نام ماژول',
       CASE IsCore    WHEN 1 THEN 'بله' ELSE '' END   AS 'پایه',
       CASE IsEnabled WHEN 1 THEN 'فعال' ELSE 'غیرفعال' END AS 'وضعیت سراسری'
FROM   EntModule
ORDER  BY SortOrder, ModuleID;");
        }

        public static DataTable GetRoleModules(string role)
        {
            return EntDb.Query(@"
SELECT m.ModuleID  AS 'شناسه',
       m.ModuleKey AS 'کلید',
       m.Name      AS 'نام ماژول',
       CASE
         WHEN m.IsCore = 1        THEN 'همیشه فعال (پایه)'
         WHEN m.IsEnabled = 0     THEN 'خاموش سراسری'
         WHEN rm.ModuleKey IS NULL THEN 'فعال (پیش‌فرض)'
         WHEN rm.IsEnabled = 1    THEN 'فعال'
         ELSE                          'غیرفعال'
       END AS 'وضعیت برای این نقش'
FROM   EntModule m
LEFT   JOIN EntRoleModule rm ON rm.ModuleKey = m.ModuleKey AND rm.RoleName = @Role
ORDER  BY m.SortOrder, m.ModuleID;", "@Role", role ?? "");
        }

        public static DataTable GetUserModules(int userId, string role)
        {
            return EntDb.Query(@"
SELECT m.ModuleID  AS 'شناسه',
       m.ModuleKey AS 'کلید',
       m.Name      AS 'نام ماژول',
       CASE
         WHEN m.IsCore = 1         THEN 'همیشه فعال (پایه)'
         WHEN m.IsEnabled = 0      THEN 'خاموش سراسری'
         WHEN rm.ModuleKey IS NULL THEN 'فعال'
         WHEN rm.IsEnabled = 1     THEN 'فعال'
         ELSE                           'غیرفعال'
       END AS 'از طریق نقش',
       CASE
         WHEN um.ModuleKey IS NULL THEN 'بدون استثنا'
         WHEN um.IsEnabled = 1     THEN 'فعال صریح'
         ELSE                           'غیرفعال صریح'
       END AS 'استثنای کاربر'
FROM   EntModule m
LEFT   JOIN EntRoleModule rm ON rm.ModuleKey = m.ModuleKey AND rm.RoleName = @Role
LEFT   JOIN EntUserModule um ON um.ModuleKey = m.ModuleKey AND um.UserID   = @Id
ORDER  BY m.SortOrder, m.ModuleID;", "@Role", role ?? "", "@Id", userId);
        }

        // ─── تغییر ────────────────────────────────────────────────────────
        public static WorkflowActionResult SetGlobal(string moduleKey, bool enabled)
        {
            WorkflowActionResult guard = EnsureManageable(moduleKey);
            if (guard != null) return guard;

            EntDb.Exec("UPDATE EntModule SET IsEnabled = @Enabled WHERE ModuleKey = @Key;",
                "@Enabled", enabled ? 1 : 0, "@Key", moduleKey);

            AfterChange("ماژول «" + moduleKey + "» به‌صورت سراسری " +
                        (enabled ? "فعال" : "غیرفعال") + " شد");

            return new WorkflowActionResult { Applied = true, Message = "وضعیت سراسری ماژول تغییر کرد." };
        }

        public static WorkflowActionResult SetForRole(string role, string moduleKey, bool? enabled)
        {
            WorkflowActionResult guard = EnsureManageable(moduleKey);
            if (guard != null) return guard;

            if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return WorkflowActionResult.Fail("ماژول‌های «مدیر کل» قابل تغییر نیست.");

            if (!enabled.HasValue)
                EntDb.Exec("DELETE FROM EntRoleModule WHERE RoleName = @Role AND ModuleKey = @Key;",
                    "@Role", role, "@Key", moduleKey);
            else
                EntDb.Exec(@"
INSERT INTO EntRoleModule (RoleName, ModuleKey, IsEnabled)
VALUES (@Role, @Key, @Enabled)
ON CONFLICT(RoleName, ModuleKey) DO UPDATE SET IsEnabled = @Enabled;",
                    "@Role", role, "@Key", moduleKey, "@Enabled", enabled.Value ? 1 : 0);

            AfterChange("ماژول «" + moduleKey + "» برای نقش «" + role + "»: " +
                        (enabled.HasValue ? (enabled.Value ? "فعال" : "غیرفعال") : "پیش‌فرض"));

            return new WorkflowActionResult { Applied = true, Message = "وضعیت ماژول برای نقش تغییر کرد." };
        }

        public static WorkflowActionResult SetForUser(int userId, string moduleKey, bool? enabled)
        {
            WorkflowActionResult guard = EnsureManageable(moduleKey);
            if (guard != null) return guard;

            if (userId <= 0)
                return WorkflowActionResult.Fail("کاربر معتبر انتخاب نشده است.");

            if (!enabled.HasValue)
                EntDb.Exec("DELETE FROM EntUserModule WHERE UserID = @Id AND ModuleKey = @Key;",
                    "@Id", userId, "@Key", moduleKey);
            else
                EntDb.Exec(@"
INSERT INTO EntUserModule (UserID, ModuleKey, IsEnabled)
VALUES (@Id, @Key, @Enabled)
ON CONFLICT(UserID, ModuleKey) DO UPDATE SET IsEnabled = @Enabled;",
                    "@Id", userId, "@Key", moduleKey, "@Enabled", enabled.Value ? 1 : 0);

            AfterChange("ماژول «" + moduleKey + "» برای کاربر #" + userId + ": " +
                        (enabled.HasValue ? (enabled.Value ? "فعال" : "غیرفعال") : "پیش‌فرض"));

            return new WorkflowActionResult { Applied = true, Message = "وضعیت ماژول برای کاربر تغییر کرد." };
        }

        // بررسی‌های مشترک: دسترسی مدیر و ممنوعیت تغییر ماژول پایه.
        private static WorkflowActionResult EnsureManageable(string moduleKey)
        {
            if (!PermissionService.Require("Module.Manage"))
                return WorkflowActionResult.Fail("مدیریت ماژول‌ها فقط برای مدیر سیستم مجاز است.");

            if (string.IsNullOrWhiteSpace(moduleKey))
                return WorkflowActionResult.Fail("ماژول انتخاب نشده است.");

            bool isCore = EntDb.ToInt(EntDb.Scalar(
                "SELECT IFNULL(IsCore, 0) FROM EntModule WHERE ModuleKey = @Key;", "@Key", moduleKey)) == 1;

            if (isCore)
                return WorkflowActionResult.Fail(
                    "این ماژول پایه است و غیرفعال کردن آن مجاز نیست (در غیر این صورت راه بازگشت به تنظیمات بسته می‌شود).");

            return null;
        }

        private static void AfterChange(string detail)
        {
            InvalidateCache();

            SecurityAudit.Log(SecurityAudit.EventUserChanged,
                              SecurityAudit.SeverityWarning, true, detail);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ─────────────────────────────────────────────────────────────────────────
    // ویژگی ۹ — ماتریس نقش و مجوز.
    //
    // ترتیب تصمیم‌گیری (اولین موردی که پاسخ داد، برنده است):
    //   ۱. مدیر کل (SuperAdmin) → همیشه مجاز. عمداً غیرقابل تغییر است تا هیچ
    //      تنظیم اشتباهی نتواند کل سیستم را برای همیشه قفل کند.
    //   ۲. استثنای کاربر (EntUserPermission) → اجازه یا منع صریح.
    //   ۳. مجوز نقش (EntRolePermission).
    //   ۴. اگر مجوز اصلاً در سیستم تعریف نشده باشد → رفتار قدیمی
    //      SecurityContext (سازگاری کامل عقب‌رو؛ کلید ناشناخته باعث قفل شدن
    //      قابلیت‌های موجود نمی‌شود).
    //
    // نتیجه‌ها برای هر کاربر یک‌بار خوانده و در حافظه نگه داشته می‌شوند، چون
    // در فرم‌ها ممکن است ده‌ها بار پشت سر هم پرسیده شوند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class PermissionService
    {
        private static readonly object Sync = new object();

        private static Dictionary<string, bool> _cache;
        private static int    _cacheUserId = -1;
        private static string _cacheRole;

        // اتصال به موتورهای گردش‌کار و تأیید (ویژگی‌های ۱ و ۲).
        public static void Install()
        {
            WorkflowService.PermissionGate = HasPermission;
        }

        // ─── پرسش اصلی ────────────────────────────────────────────────────
        public static bool HasPermission(string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(permissionKey)) return true;

            // ۱) مدیر کل همیشه مجاز است.
            if (SecurityContext.IsSuperAdmin()) return true;

            try
            {
                Dictionary<string, bool> cache = GetCache();

                bool granted;
                if (cache.TryGetValue(permissionKey, out granted))
                    return granted;

                // ۴) مجوز ناشناخته → رفتار قدیمی بر پایه نقش.
                return LegacyFallback(permissionKey);
            }
            catch
            {
                // در صورت هر خطا، رفتار قدیمی ملاک است تا کاربر بی‌دلیل مسدود نشود.
                return LegacyFallback(permissionKey);
            }
        }

        // مثل HasPermission، اما در صورت نبود مجوز، رویداد امنیتی ثبت می‌کند.
        // برای نقاط حساس (حذف، تأیید، تنظیمات) استفاده می‌شود.
        public static bool Require(string permissionKey, string entityName = null, int entityId = 0)
        {
            if (HasPermission(permissionKey)) return true;

            SecurityAudit.PermissionDenied(permissionKey, entityName, entityId);
            return false;
        }

        // ─── حافظه موقت ───────────────────────────────────────────────────
        private static Dictionary<string, bool> GetCache()
        {
            lock (Sync)
            {
                bool sameUser = _cacheUserId == SecurityContext.UserId &&
                                string.Equals(_cacheRole, SecurityContext.Role, StringComparison.Ordinal);

                if (_cache != null && sameUser) return _cache;

                Dictionary<string, bool> map =
                    new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                // ۳) مجوزهای نقش
                DataTable roleRows = EntDb.Query(
                    "SELECT PermKey, IsGranted FROM EntRolePermission WHERE RoleName = @Role;",
                    "@Role", SecurityContext.Role ?? "");

                foreach (DataRow row in roleRows.Rows)
                    map[EntDb.ToText(row["PermKey"])] = EntDb.ToBool(row["IsGranted"]);

                // ۲) استثناهای کاربر — بر نقش اولویت دارند.
                if (SecurityContext.UserId > 0)
                {
                    DataTable userRows = EntDb.Query(
                        "SELECT PermKey, IsGranted FROM EntUserPermission WHERE UserID = @Id;",
                        "@Id", SecurityContext.UserId);

                    foreach (DataRow row in userRows.Rows)
                        map[EntDb.ToText(row["PermKey"])] = EntDb.ToBool(row["IsGranted"]);
                }

                _cache       = map;
                _cacheUserId = SecurityContext.UserId;
                _cacheRole   = SecurityContext.Role;

                return _cache;
            }
        }

        // پس از هر تغییر در ماتریس یا تعویض کاربر باید صدا زده شود.
        public static void InvalidateCache()
        {
            lock (Sync)
            {
                _cache       = null;
                _cacheUserId = -1;
                _cacheRole   = null;
            }
        }

        // رفتار پیش از این ویژگی — بر پایه نقش‌های موجود SecurityContext.
        private static bool LegacyFallback(string permissionKey)
        {
            string key = permissionKey ?? "";

            if (key.EndsWith(".Delete", StringComparison.OrdinalIgnoreCase))
                return SecurityContext.CanDelete();

            if (key.EndsWith(".Manage", StringComparison.OrdinalIgnoreCase) ||
                key.EndsWith(".Override", StringComparison.OrdinalIgnoreCase) ||
                key.EndsWith(".Approve", StringComparison.OrdinalIgnoreCase))
                return SecurityContext.IsAdmin();

            if (key.EndsWith(".Create", StringComparison.OrdinalIgnoreCase) ||
                key.EndsWith(".Edit", StringComparison.OrdinalIgnoreCase) ||
                key.EndsWith(".Review", StringComparison.OrdinalIgnoreCase))
                return SecurityContext.CanEdit();

            // مشاهده و موارد ناشناخته: هر کاربر واردشده مجاز است (رفتار قبلی).
            return SecurityContext.IsLoggedIn;
        }

        // ─── خواندن برای فرم مدیریت ───────────────────────────────────────
        public static DataTable GetPermissions()
        {
            return EntDb.Query(@"
SELECT PermissionID AS 'شناسه',
       PermKey      AS 'کلید',
       Name         AS 'عنوان',
       Category     AS 'دسته'
FROM   EntPermission
ORDER  BY SortOrder, PermissionID;");
        }

        public static List<string> GetRoles()
        {
            // نقش‌های موجود سیستم — همان‌هایی که FrmUsers استفاده می‌کند.
            return new List<string> { "SuperAdmin", "Admin", "Operator", "Viewer" };
        }

        // ماتریس کامل: هر سطر یک مجوز، هر ستون یک نقش.
        public static DataTable GetRoleMatrix()
        {
            DataTable matrix = new DataTable();
            matrix.Columns.Add("کلید");
            matrix.Columns.Add("دسته");
            matrix.Columns.Add("عنوان");

            List<string> roles = GetRoles();
            foreach (string role in roles)
                matrix.Columns.Add(role, typeof(bool));

            DataTable permissions = EntDb.Query(
                "SELECT PermKey, Name, Category FROM EntPermission ORDER BY SortOrder, PermissionID;");

            DataTable grants = EntDb.Query(
                "SELECT RoleName, PermKey, IsGranted FROM EntRolePermission;");

            foreach (DataRow permission in permissions.Rows)
            {
                string key = EntDb.ToText(permission["PermKey"]);

                DataRow row = matrix.NewRow();
                row["کلید"]  = key;
                row["دسته"]  = EntDb.ToText(permission["Category"]);
                row["عنوان"] = EntDb.ToText(permission["Name"]);

                foreach (string role in roles)
                    row[role] = IsGranted(grants, role, key);

                matrix.Rows.Add(row);
            }

            return matrix;
        }

        private static bool IsGranted(DataTable grants, string role, string key)
        {
            foreach (DataRow row in grants.Rows)
                if (string.Equals(EntDb.ToText(row["RoleName"]), role, StringComparison.Ordinal) &&
                    string.Equals(EntDb.ToText(row["PermKey"]),  key,  StringComparison.Ordinal))
                    return EntDb.ToBool(row["IsGranted"]);

            return false;
        }

        // ─── تغییر ماتریس ─────────────────────────────────────────────────
        public static WorkflowActionResult SetRolePermission(string role, string permissionKey, bool granted)
        {
            if (!SecurityContext.IsAdmin())
                return WorkflowActionResult.Fail("تغییر مجوزها فقط برای مدیر سیستم مجاز است.");

            // مجوزهای مدیر کل قابل تغییر نیستند تا امکان قفل شدن کامل سیستم
            // از بین برود.
            if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return WorkflowActionResult.Fail("مجوزهای «مدیر کل» قابل تغییر نیست.");

            EntDb.Exec(@"
INSERT INTO EntRolePermission (RoleName, PermKey, IsGranted)
VALUES (@Role, @Key, @Granted)
ON CONFLICT(RoleName, PermKey) DO UPDATE SET IsGranted = @Granted;",
                "@Role", role, "@Key", permissionKey, "@Granted", granted ? 1 : 0);

            InvalidateCache();

            SecurityAudit.Log(SecurityAudit.EventUserChanged, SecurityAudit.SeverityWarning, true,
                "مجوز «" + permissionKey + "» برای نقش «" + role + "» " +
                (granted ? "داده شد" : "گرفته شد"));

            return new WorkflowActionResult { Applied = true, Message = "مجوز نقش به‌روزرسانی شد." };
        }

        public static WorkflowActionResult SetUserPermission(int userId, string permissionKey, bool? granted)
        {
            if (!SecurityContext.IsAdmin())
                return WorkflowActionResult.Fail("تغییر مجوزها فقط برای مدیر سیستم مجاز است.");

            if (userId <= 0)
                return WorkflowActionResult.Fail("کاربر معتبر انتخاب نشده است.");

            if (!granted.HasValue)
            {
                // حذف استثنا → کاربر دوباره از مجوز نقش خودش پیروی می‌کند.
                EntDb.Exec("DELETE FROM EntUserPermission WHERE UserID = @Id AND PermKey = @Key;",
                    "@Id", userId, "@Key", permissionKey);
            }
            else
            {
                EntDb.Exec(@"
INSERT INTO EntUserPermission (UserID, PermKey, IsGranted)
VALUES (@Id, @Key, @Granted)
ON CONFLICT(UserID, PermKey) DO UPDATE SET IsGranted = @Granted;",
                    "@Id", userId, "@Key", permissionKey, "@Granted", granted.Value ? 1 : 0);
            }

            InvalidateCache();

            SecurityAudit.Log(SecurityAudit.EventUserChanged, SecurityAudit.SeverityWarning, true,
                "استثنای مجوز «" + permissionKey + "» برای کاربر #" + userId + ": " +
                (granted.HasValue ? (granted.Value ? "اجازه" : "منع") : "حذف استثنا"));

            return new WorkflowActionResult { Applied = true, Message = "استثنای کاربر به‌روزرسانی شد." };
        }

        // استثناهای یک کاربر، همراه با مقدار مؤثر نقش او.
        public static DataTable GetUserOverrides(int userId, string role)
        {
            return EntDb.Query(@"
SELECT p.PermKey  AS 'کلید',
       p.Name     AS 'عنوان',
       p.Category AS 'دسته',
       CASE IFNULL(rp.IsGranted, 0) WHEN 1 THEN 'دارد' ELSE 'ندارد' END AS 'از طریق نقش',
       CASE
         WHEN up.PermKey IS NULL   THEN 'بدون استثنا'
         WHEN up.IsGranted = 1     THEN 'اجازه صریح'
         ELSE                           'منع صریح'
       END AS 'استثنای کاربر'
FROM   EntPermission p
LEFT   JOIN EntRolePermission rp ON rp.PermKey = p.PermKey AND rp.RoleName = @Role
LEFT   JOIN EntUserPermission up ON up.PermKey = p.PermKey AND up.UserID   = @Id
ORDER  BY p.SortOrder, p.PermissionID;",
                "@Role", role ?? "", "@Id", userId);
        }

        // فهرست کاربران برای فرم مدیریت (با رعایت فیلتر مرکز مثل FrmUsers).
        public static DataTable GetUsers()
        {
            return EntDb.Query(@"
SELECT UserID   AS 'شناسه',
       Username AS 'نام کاربری',
       Role     AS 'نقش'
FROM   TblUsers
WHERE  IsActive = 1
  AND  (@Center = 0 OR IFNULL(CenterID, 0) = @Center)
ORDER  BY Username;", "@Center", SecurityContext.CenterFilterId);
        }
    }
}

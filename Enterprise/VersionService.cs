using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ─────────────────────────────────────────────────────────────────────────
    // ویژگی ۶ — تاریخچه نسخه‌ها.
    //
    // در هر ثبت/ویرایش/حذف، یک «عکس فوری» از ردیف رکورد در EntRecordVersion
    // ذخیره می‌شود و فیلدهای تغییرکرده نسبت به نسخه قبل محاسبه می‌گردد.
    //
    // نکته طراحی مهم — «بازگردانی نسخه» عمداً پیاده نشده است:
    // بازنویسی خودکار همه ستون‌های یک رکورد با مقادیر قدیمی می‌تواند داده‌ی
    // درستِ فعلی را از بین ببرد و برگشت‌ناپذیر است (همان دلیلی که در پنجره
    // «پرونده‌های تکراری» ادغام خودکار پیاده نشده). این‌جا فقط مشاهده و مقایسه
    // فراهم است و اصلاح با تصمیم کاربر در فرم خود رکورد انجام می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public static class VersionService
    {
        public const string OperationInsert = "ثبت";
        public const string OperationUpdate = "ویرایش";
        public const string OperationDelete = "حذف";

        // ستون‌هایی که ذخیره‌شان در تاریخچه بی‌فایده یا سنگین است.
        private static readonly string[] SkippedColumns = { "HeadPhoto", "FamilyPhoto" };

        // نگاشت جدول → ستون کلید اصلی (همان مجموعه‌ای که موتور قواعد پشتیبانی می‌کند).
        private static readonly Dictionary<string, string> PrimaryKeys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "TblCase",       "CasID" },
                { "TblFamily",     "FamID" },
                { "TblDocs",       "DocID" },
                { "TblAssistance", "AssistanceID" },
                { "TblApplicant",  "ApplicantID" }
            };

        // ─── ثبت نسخه ─────────────────────────────────────────────────────
        // خطای این متد هرگز به فراخوان نمی‌رسد: نگهداری تاریخچه نباید باعث
        // شکست ثبت پرونده شود.
        public static void Capture(string entityName, int entityId, string operation)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityName) || entityId <= 0) return;

                IDictionary<string, string> current = ReadRow(entityName, entityId);

                // رکورد وجود ندارد (شناسه اشتباه یا جدول پشتیبانی‌نشده): هیچ
                // نسخه‌ای ثبت نمی‌شود تا تاریخچه با ردیف‌های خالی آلوده نشود.
                // برای حذف، از CaptureDeleted استفاده می‌شود که محتوا را از
                // پیش خوانده است.
                if (current.Count == 0) return;

                string snapshot = Serialize(current);

                IDictionary<string, string> previous = LatestSnapshot(entityName, entityId);
                string changed = operation == OperationInsert
                    ? ""
                    : DescribeChanges(previous, current);

                // اگر ویرایشی هیچ تغییری نداشته، نسخه تکراری ساخته نمی‌شود.
                if (operation == OperationUpdate && previous.Count > 0 && changed.Length == 0)
                    return;

                int nextNo = EntDb.ToInt(EntDb.Scalar(@"
SELECT IFNULL(MAX(VersionNo), 0) + 1 FROM EntRecordVersion
WHERE  EntityName = @Entity AND EntityID = @Id;",
                    "@Entity", entityName, "@Id", entityId));

                EntDb.Exec(@"
INSERT INTO EntRecordVersion
    (EntityName, EntityID, VersionNo, Operation, Snapshot, ChangedFields,
     CenterID, ChangedBy, ChangedByUserID)
VALUES
    (@Entity, @Id, @No, @Op, @Snapshot, @Changed, @Center, @By, @ByID);",
                    "@Entity",   entityName,
                    "@Id",       entityId,
                    "@No",       nextNo,
                    "@Op",       operation,
                    "@Snapshot", snapshot,
                    "@Changed",  changed,
                    "@Center",   SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : null,
                    "@By",       SecurityContext.Username,
                    "@ByID",     SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : null);
            }
            catch
            {
                // تاریخچه نسخه غیرحیاتی است و نباید عملیات اصلی را متوقف کند.
            }
        }

        // ─── حذف: خواندن پیش از حذف، ثبت پس از حذف ────────────────────────
        // آموزش — چرا دو مرحله‌ای: هنگام حذف، ردیف دیگر وجود ندارد تا محتوایش
        // خوانده شود. پس محتوا «قبل» از اجرای DELETE خوانده می‌شود، اما نسخه
        // فقط «بعد» از حذفِ موفق ثبت می‌گردد تا اگر حذف انجام نشد، نسخه‌ی
        // نادرست «حذف» در تاریخچه نماند.
        public static string ReadSnapshotText(string entityName, int entityId)
        {
            try
            {
                return Serialize(ReadRow(entityName, entityId));
            }
            catch
            {
                return "";
            }
        }

        public static void CaptureDeleted(string entityName, int entityId, string snapshotText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityName) || entityId <= 0) return;

                int nextNo = EntDb.ToInt(EntDb.Scalar(@"
SELECT IFNULL(MAX(VersionNo), 0) + 1 FROM EntRecordVersion
WHERE  EntityName = @Entity AND EntityID = @Id;",
                    "@Entity", entityName, "@Id", entityId));

                EntDb.Exec(@"
INSERT INTO EntRecordVersion
    (EntityName, EntityID, VersionNo, Operation, Snapshot, ChangedFields,
     CenterID, ChangedBy, ChangedByUserID)
VALUES
    (@Entity, @Id, @No, @Op, @Snapshot, '', @Center, @By, @ByID);",
                    "@Entity",   entityName,
                    "@Id",       entityId,
                    "@No",       nextNo,
                    "@Op",       OperationDelete,
                    "@Snapshot", snapshotText,
                    "@Center",   SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : null,
                    "@By",       SecurityContext.Username,
                    "@ByID",     SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : null);
            }
            catch
            {
                // تاریخچه نسخه غیرحیاتی است.
            }
        }

        // ─── خواندن ───────────────────────────────────────────────────────
        public static DataTable GetVersions(string entityName, int entityId)
        {
            return EntDb.Query(@"
SELECT VersionID     AS 'شناسه',
       VersionNo     AS 'نسخه',
       Operation     AS 'عملیات',
       ChangedBy     AS 'کاربر',
       ChangedAt     AS 'تاریخ',
       ChangedFields AS 'فیلدهای تغییرکرده'
FROM   EntRecordVersion
WHERE  EntityName = @Entity AND EntityID = @Id
ORDER  BY VersionNo DESC;", "@Entity", entityName, "@Id", entityId);
        }

        // همه نسخه‌های اخیر همه رکوردها — برای فرم مرور کلی.
        public static DataTable GetRecent(string entityName, int limit)
        {
            return EntDb.Query(@"
SELECT VersionID     AS 'شناسه',
       EntityName    AS 'موجودیت',
       EntityID      AS 'شناسه رکورد',
       VersionNo     AS 'نسخه',
       Operation     AS 'عملیات',
       ChangedBy     AS 'کاربر',
       ChangedAt     AS 'تاریخ',
       ChangedFields AS 'فیلدهای تغییرکرده'
FROM   EntRecordVersion
WHERE  (IFNULL(@Entity, '') = '' OR EntityName = @Entity)
  AND  (@Center = 0 OR IFNULL(CenterID, 0) = @Center)
ORDER  BY VersionID DESC
LIMIT  @Limit;",
                "@Entity", entityName ?? "",
                "@Center", SecurityContext.CenterFilterId,
                "@Limit",  limit <= 0 ? 500 : limit);
        }

        // محتوای یک نسخه به شکل جدول «فیلد / مقدار».
        public static DataTable GetSnapshotTable(int versionId)
        {
            DataTable result = new DataTable();
            result.Columns.Add("فیلد");
            result.Columns.Add("مقدار");

            IDictionary<string, string> values = Deserialize(EntDb.ToText(EntDb.Scalar(
                "SELECT Snapshot FROM EntRecordVersion WHERE VersionID = @Id;", "@Id", versionId)));

            foreach (KeyValuePair<string, string> pair in values)
                result.Rows.Add(pair.Key, pair.Value);

            return result;
        }

        // مقایسه یک نسخه با نسخه قبلی همان رکورد.
        public static DataTable GetDiffTable(int versionId)
        {
            DataTable result = new DataTable();
            result.Columns.Add("فیلد");
            result.Columns.Add("مقدار قبلی");
            result.Columns.Add("مقدار جدید");

            DataTable info = EntDb.Query(@"
SELECT EntityName, EntityID, VersionNo, Snapshot
FROM   EntRecordVersion WHERE VersionID = @Id;", "@Id", versionId);

            if (info.Rows.Count == 0) return result;

            DataRow row = info.Rows[0];

            IDictionary<string, string> current = Deserialize(EntDb.ToText(row["Snapshot"]));

            IDictionary<string, string> previous = Deserialize(EntDb.ToText(EntDb.Scalar(@"
SELECT Snapshot FROM EntRecordVersion
WHERE  EntityName = @Entity AND EntityID = @Id AND VersionNo < @No
ORDER  BY VersionNo DESC LIMIT 1;",
                "@Entity", EntDb.ToText(row["EntityName"]),
                "@Id",     EntDb.ToInt(row["EntityID"]),
                "@No",     EntDb.ToInt(row["VersionNo"]))));

            foreach (string key in AllKeys(previous, current))
            {
                string before = Value(previous, key);
                string after  = Value(current,  key);

                if (!string.Equals(before, after, StringComparison.Ordinal))
                    result.Rows.Add(key, before, after);
            }

            return result;
        }

        public static int CountVersions(string entityName, int entityId)
        {
            return EntDb.ToInt(EntDb.Scalar(@"
SELECT COUNT(*) FROM EntRecordVersion WHERE EntityName = @Entity AND EntityID = @Id;",
                "@Entity", entityName, "@Id", entityId));
        }

        // ─── کمکی‌های داخلی ───────────────────────────────────────────────
        private static IDictionary<string, string> ReadRow(string entityName, int entityId)
        {
            Dictionary<string, string> values =
                new Dictionary<string, string>(StringComparer.Ordinal);

            string keyColumn;
            if (!PrimaryKeys.TryGetValue(entityName ?? "", out keyColumn)) return values;

            // نام جدول/ستون از نگاشت داخلی می‌آید، نه از ورودی کاربر.
            DataTable table = EntDb.Query(
                "SELECT * FROM " + entityName + " WHERE " + keyColumn + " = @Id;", "@Id", entityId);

            if (table.Rows.Count == 0) return values;

            DataRow row = table.Rows[0];

            foreach (DataColumn column in table.Columns)
            {
                if (Array.IndexOf(SkippedColumns, column.ColumnName) >= 0) continue;

                object raw = row[column];

                // ستون‌های دودویی (عکس) ذخیره نمی‌شوند؛ فقط وجود/نبودشان.
                if (raw is byte[])
                {
                    values[column.ColumnName] = ((byte[])raw).Length > 0 ? "(فایل)" : "";
                    continue;
                }

                values[column.ColumnName] =
                    raw == null || raw == DBNull.Value ? "" : Convert.ToString(raw);
            }

            return values;
        }

        private static IDictionary<string, string> LatestSnapshot(string entityName, int entityId)
        {
            return Deserialize(EntDb.ToText(EntDb.Scalar(@"
SELECT Snapshot FROM EntRecordVersion
WHERE  EntityName = @Entity AND EntityID = @Id
ORDER  BY VersionNo DESC LIMIT 1;", "@Entity", entityName, "@Id", entityId)));
        }

        // قالب ذخیره: هر خط «کلید=مقدار»؛ خط جدید داخل مقدار با \n جایگزین
        // می‌شود تا ساختار خط‌به‌خط نشکند.
        private static string Serialize(IDictionary<string, string> values)
        {
            StringBuilder builder = new StringBuilder();

            foreach (KeyValuePair<string, string> pair in values)
                builder.Append(pair.Key)
                       .Append('=')
                       .Append((pair.Value ?? "").Replace("\r", "").Replace("\n", "\\n"))
                       .Append('\n');

            return builder.ToString();
        }

        private static IDictionary<string, string> Deserialize(string text)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);

            if (string.IsNullOrEmpty(text)) return values;

            foreach (string line in text.Split('\n'))
            {
                if (line.Length == 0) continue;

                int separator = line.IndexOf('=');
                if (separator <= 0) continue;

                values[line.Substring(0, separator)] =
                    line.Substring(separator + 1).Replace("\\n", "\n");
            }

            return values;
        }

        private static string DescribeChanges(
            IDictionary<string, string> previous, IDictionary<string, string> current)
        {
            List<string> changed = new List<string>();

            foreach (string key in AllKeys(previous, current))
                if (!string.Equals(Value(previous, key), Value(current, key), StringComparison.Ordinal))
                    changed.Add(key);

            return string.Join("، ", changed.ToArray());
        }

        private static IEnumerable<string> AllKeys(
            IDictionary<string, string> first, IDictionary<string, string> second)
        {
            List<string> keys = new List<string>(first.Keys);

            foreach (string key in second.Keys)
                if (!keys.Contains(key)) keys.Add(key);

            return keys;
        }

        private static string Value(IDictionary<string, string> values, string key)
        {
            string result;
            return values.TryGetValue(key, out result) ? (result ?? "") : "";
        }
    }
}

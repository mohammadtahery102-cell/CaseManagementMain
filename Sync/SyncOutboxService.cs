using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Text;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // صفِ محلیِ عملیات — فاز ۲.
    //
    // نقش: هر تغییرِ قابل‌همگام‌سازی (ثبت/ویرایش/حذف/پیوست/تغییر وضعیت) را با
    // هویت سراسری، نسخه، کاربر، رایانه و زمان در SyncOutbox ثبت می‌کند تا وقتی
    // اینترنت برقرار شد بتوان آن را — به همان ترتیبِ وقوع — ارسال کرد.
    //
    // ⚠ سه قاعدهٔ غیرقابل‌مذاکره:
    //
    // ۱. این سرویس هرگز استثنا به فراخوان نمی‌دهد. ثبت یک پروندهٔ واقعی هرگز
    //    نباید به‌خاطر شکستِ صفِ همگام‌سازی از بین برود. همان سیاستی که
    //    VersionService و AuditLogger از قبل دارند.
    //
    // ۲. حذف باید *پیش از* پاک شدن رکورد ثبت شود. پس از DELETE، هیچ راهی برای
    //    دانستن GlobalIDِ رکوردِ رفته وجود ندارد و آن حذف برای همیشه
    //    غیرقابل‌همگام‌سازی می‌شود (یکی از ایرادهای اصلیِ گزارشِ فاز ۱).
    //
    // ۳. اینجا هیچ ارتباط شبکه‌ای نیست. صف فقط پر می‌شود؛ خالی کردنش کار
    //    لایهٔ انتقال است که هنوز ساخته نشده. این عمدی است — «حالت آنلاینِ
    //    ساختگی» ساخته نمی‌شود.
    // ═════════════════════════════════════════════════════════════════════════
    public static class SyncOutboxService
    {
        private static readonly DatabaseHelper Db = new DatabaseHelper();

        // ستون‌هایی که در بارِ همگام‌سازی بی‌فایده یا حجیم‌اند.
        private static readonly HashSet<string> SkippedColumns =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "RowVersion", "LastModifiedAt", "LastModifiedBy", "SyncStatus"
            };

        // ─────────────────────────────────────────────────────────────────────
        // ثبت یک عملیاتِ ایجاد/ویرایش
        // ─────────────────────────────────────────────────────────────────────
        public static void Capture(string entityName, int localId, string operation)
        {
            try
            {
                if (!IsReady() || string.IsNullOrWhiteSpace(entityName) || localId <= 0) return;

                string primaryKey = PrimaryKeyOf(entityName);
                if (primaryKey == null) return;

                DataRow row = ReadRow(entityName, primaryKey, localId);
                if (row == null) return;

                // «آخرین تغییر» همیشه ثبت می‌شود، ولی شمارندهٔ نسخه فقط در
                // ویرایش جلو می‌رود: رکوردِ تازه‌ساخته‌شده باید نسخهٔ ۱ باشد،
                // نه ۲. اگر اینجا هم افزایش می‌دادیم، هر رکورد از همان ابتدا
                // یک نسخه جلوتر از واقعیت گزارش می‌شد و مقایسهٔ نسخه‌ها با
                // سرور همیشه یک واحد خطا داشت.
                bool isCreate = string.Equals(operation, OfflineSyncInitializer.OperationCreate,
                                              StringComparison.Ordinal);

                int version = Touch(entityName, primaryKey, localId, !isCreate);

                Insert(operation, entityName,
                       GetString(row, "GlobalID"), localId,
                       ParentGlobalId(entityName, row),
                       version, Serialize(row));
            }
            catch (Exception ex) { Swallow(ex, "Capture/" + entityName); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ثبت یک حذف — باید *پیش از* اجرای DELETE صدا زده شود
        // ─────────────────────────────────────────────────────────────────────
        public static void CaptureDelete(string entityName, int localId)
        {
            try
            {
                if (!IsReady() || string.IsNullOrWhiteSpace(entityName) || localId <= 0) return;

                string primaryKey = PrimaryKeyOf(entityName);
                if (primaryKey == null) return;

                DataRow row = ReadRow(entityName, primaryKey, localId);

                // رکورد از قبل رفته است ⇒ سرویس دیر صدا زده شده. یک ردیفِ حذفِ
                // بدون هویت بی‌فایده است و صف را آلوده می‌کند، پس ثبت نمی‌شود
                // ولی در لاگ خطا به‌عنوان اشکالِ برنامه‌نویسی ثبت می‌گردد.
                if (row == null)
                {
                    Swallow(new InvalidOperationException(
                        "CaptureDelete پس از حذف رکورد صدا زده شد: " + entityName + "#" + localId),
                        "CaptureDelete/" + entityName);
                    return;
                }

                int version = GetInt(row, "RowVersion", 1);

                Insert(OfflineSyncInitializer.OperationDelete, entityName,
                       GetString(row, "GlobalID"), localId,
                       ParentGlobalId(entityName, row),
                       version, Serialize(row));
            }
            catch (Exception ex) { Swallow(ex, "CaptureDelete/" + entityName); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // حذف در دو گام: «آماده‌سازی» پیش از DELETE و «ثبت» پس از موفقیت آن
        //
        // آموزش — چرا دو گام و نه یک: هویتِ رکورد فقط *پیش از* حذف خواندنی
        // است، ولی خودِ حذف ممکن است انجام نشود (مثلاً پرونده به مرکز دیگری
        // تعلق دارد یا بایگانی نیست و شرطِ WHERE رد می‌کند). اگر یک‌مرحله‌ای
        // ثبت می‌کردیم، صف پر می‌شد از حذف‌هایی که هرگز رخ نداده‌اند و سمت
        // مقابل رکوردهای سالم را پاک می‌کرد. حالا داده پیش از حذف خوانده و
        // فقط در صورت موفقیتِ واقعی ثبت می‌شود.
        // ─────────────────────────────────────────────────────────────────────
        public sealed class PendingDelete
        {
            internal readonly List<object[]> Rows = new List<object[]>();
            public int Count { get { return Rows.Count; } }
        }

        // پیش از DELETE صدا زده می‌شود. برای TblCase، فرزندانی که FK آبشاری
        // بی‌صدا پاکشان می‌کند هم برداشت می‌شوند.
        public static PendingDelete PrepareDelete(string entityName, int localId)
        {
            var pending = new PendingDelete();

            try
            {
                if (!IsReady() || localId <= 0) return pending;

                if (string.Equals(entityName, "TblCase", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string[] table in OfflineSyncInitializer.SyncedTables)
                    {
                        string child = table[0];
                        string childKey = table[1];
                        if (string.Equals(child, "TblCase", StringComparison.OrdinalIgnoreCase)) continue;

                        DataTable children = SafeQuery(
                            "SELECT " + childKey + " FROM " + child + " WHERE CasID = @Id;",
                            new SQLiteParameter("@Id", localId));

                        if (children == null) continue;

                        foreach (DataRow childRow in children.Rows)
                            AddPending(pending, child, Convert.ToInt32(childRow[childKey]));
                    }
                }

                AddPending(pending, entityName, localId);
            }
            catch (Exception ex) { Swallow(ex, "PrepareDelete/" + entityName); }

            return pending;
        }

        // فقط پس از حذفِ *واقعاً انجام‌شده* صدا زده می‌شود.
        public static void CommitDelete(PendingDelete pending)
        {
            try
            {
                if (pending == null || pending.Rows.Count == 0) return;

                foreach (object[] row in pending.Rows)
                    Insert(OfflineSyncInitializer.OperationDelete,
                           (string)row[0], (string)row[1], (int)row[2],
                           (string)row[3], (int)row[4], (string)row[5]);
            }
            catch (Exception ex) { Swallow(ex, "CommitDelete"); }
        }

        private static void AddPending(PendingDelete pending, string entityName, int localId)
        {
            string primaryKey = PrimaryKeyOf(entityName);
            if (primaryKey == null) return;

            DataRow row = ReadRow(entityName, primaryKey, localId);
            if (row == null) return;

            pending.Rows.Add(new object[]
            {
                entityName,
                GetString(row, "GlobalID"),
                localId,
                ParentGlobalId(entityName, row),
                GetInt(row, "RowVersion", 1),
                Serialize(row)
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // ثبت یک پیوست (عکس/سند) — فاز ۷ روی همین ردیف‌ها سوار می‌شود
        // ─────────────────────────────────────────────────────────────────────
        public static void CaptureAttachment(string entityName, int localId, string filePath)
        {
            try
            {
                if (!IsReady() || string.IsNullOrWhiteSpace(filePath)) return;

                string primaryKey = PrimaryKeyOf(entityName);
                if (primaryKey == null) return;

                DataRow row = ReadRow(entityName, primaryKey, localId);
                if (row == null) return;

                // فعلاً فقط نام فایل و اندازه ثبت می‌شود. هش و وضعیت آپلود در
                // فاز ۷ اضافه می‌گردد؛ عمداً اینجا هشِ ساختگی تولید نمی‌شود.
                var payload = new StringBuilder();
                payload.Append("FilePath=").Append(filePath).AppendLine();
                try
                {
                    var info = new System.IO.FileInfo(filePath);
                    if (info.Exists)
                    {
                        payload.Append("FileName=").Append(info.Name).AppendLine();
                        payload.Append("SizeBytes=").Append(info.Length).AppendLine();
                    }
                }
                catch { }

                Insert(OfflineSyncInitializer.OperationAttachment, entityName,
                       GetString(row, "GlobalID"), localId,
                       ParentGlobalId(entityName, row),
                       GetInt(row, "RowVersion", 1), payload.ToString());
            }
            catch (Exception ex) { Swallow(ex, "CaptureAttachment"); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // خواندن صف (برای لایهٔ انتقال و پایش در مرکز کنترل)
        // ─────────────────────────────────────────────────────────────────────
        public static int PendingCount()  { return CountByState(OfflineSyncInitializer.StatePending); }
        public static int FailedCount()   { return CountByState(OfflineSyncInitializer.StateFailed); }
        public static int ConflictCount() { return CountByState(OfflineSyncInitializer.StateConflict); }

        // شمارشِ ردیف‌های «در انتظار/ناموفق» فقط برای یک جدول — برای نمایش
        // وضعیتِ جداگانهٔ هر ماژول در صفحهٔ همگام‌سازی. صفِ واحد (SyncOutbox)
        // دست‌نخورده می‌ماند؛ این فقط یک شمارشِ فیلترشدهٔ فقط‌خواندنی است.
        public static int PendingCountByEntity(string entityName)
        {
            try
            {
                if (!OutboxExists()) return 0;

                object value = Db.ExecuteScalar(
                    "SELECT COUNT(1) FROM SyncOutbox WHERE EntityName = @E AND State IN (@P, @F);",
                    new SQLiteParameter("@E", entityName),
                    new SQLiteParameter("@P", OfflineSyncInitializer.StatePending),
                    new SQLiteParameter("@F", OfflineSyncInitializer.StateFailed));

                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
            catch { return 0; }
        }

        private static int CountByState(string state)
        {
            try
            {
                if (!OutboxExists()) return 0;

                object value = Db.ExecuteScalar(
                    "SELECT COUNT(1) FROM SyncOutbox WHERE State = @S;",
                    new SQLiteParameter("@S", state));

                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
            catch { return 0; }
        }

        // صفِ بعدی برای ارسال — همیشه به ترتیب وقوع (OutboxID).
        public static DataTable GetPending(int limit)
        {
            try
            {
                if (!OutboxExists()) return new DataTable();

                return Db.Query(
                    "SELECT * FROM SyncOutbox WHERE State IN (@P, @F) ORDER BY OutboxID LIMIT @L;",
                    new SQLiteParameter("@P", OfflineSyncInitializer.StatePending),
                    new SQLiteParameter("@F", OfflineSyncInitializer.StateFailed),
                    new SQLiteParameter("@L", Math.Max(1, limit)));
            }
            catch { return new DataTable(); }
        }

        public static void MarkSent(long outboxId)
        {
            Try(delegate
            {
                Db.ExecuteNonQuery(
                    "UPDATE SyncOutbox SET State = @S, SentAt = datetime('now'), LastError = NULL " +
                    "WHERE OutboxID = @Id;",
                    new SQLiteParameter("@S", OfflineSyncInitializer.StateSent),
                    new SQLiteParameter("@Id", outboxId));
            }, "MarkSent");
        }

        // شکست، ردیف را از صف بیرون نمی‌اندازد: با افزایش شمارندهٔ تلاش در صف
        // می‌ماند تا دفعهٔ بعد دوباره تلاش شود (الزامِ «retry failed sync»).
        public static void MarkFailed(long outboxId, string error)
        {
            Try(delegate
            {
                Db.ExecuteNonQuery(
                    "UPDATE SyncOutbox SET State = @S, Attempts = Attempts + 1, " +
                    "LastAttemptAt = datetime('now'), LastError = @E WHERE OutboxID = @Id;",
                    new SQLiteParameter("@S", OfflineSyncInitializer.StateFailed),
                    new SQLiteParameter("@E", (object)(error ?? "") ),
                    new SQLiteParameter("@Id", outboxId));
            }, "MarkFailed");
        }

        // کنار گذاشتن عملیاتِ معلقِ یک رکورد پس از تصمیم مدیر (فاز ۴).
        // ⚠ عمداً حذف نمی‌شوند: ردّ اینکه چه چیزی ارسال *نشد* و چرا، برای
        // بازرسیِ بعدی لازم است.
        public static void Discard(string entityName, string globalId, string reason)
        {
            Try(delegate
            {
                Db.ExecuteNonQuery(
                    "UPDATE SyncOutbox SET State = @S, LastError = @E, LastAttemptAt = datetime('now') " +
                    "WHERE EntityName = @N AND EntityGlobalID = @G AND State IN (@P, @F, @C);",
                    new SQLiteParameter("@S", OfflineSyncInitializer.StateDiscarded),
                    new SQLiteParameter("@E", (object)(reason ?? "")),
                    new SQLiteParameter("@N", entityName),
                    new SQLiteParameter("@G", globalId),
                    new SQLiteParameter("@P", OfflineSyncInitializer.StatePending),
                    new SQLiteParameter("@F", OfflineSyncInitializer.StateFailed),
                    new SQLiteParameter("@C", OfflineSyncInitializer.StateConflict));
            }, "Discard");
        }

        public static void MarkConflict(long outboxId, string detail)
        {
            Try(delegate
            {
                Db.ExecuteNonQuery(
                    "UPDATE SyncOutbox SET State = @S, LastAttemptAt = datetime('now'), " +
                    "LastError = @E WHERE OutboxID = @Id;",
                    new SQLiteParameter("@S", OfflineSyncInitializer.StateConflict),
                    new SQLiteParameter("@E", (object)(detail ?? "")),
                    new SQLiteParameter("@Id", outboxId));
            }, "MarkConflict");
        }

        // ─────────────────────────────────────────────────────────────────────
        // وضعیت همگام‌سازی (کلید/مقدار)
        // ─────────────────────────────────────────────────────────────────────
        public const string KeyLastSyncAt = "LastSyncAt";
        public const string KeyDeviceId   = "DeviceId";

        public static string GetState(string key, string defaultValue = "")
        {
            try
            {
                if (!TableExists("SyncState")) return defaultValue;

                object value = Db.ExecuteScalar(
                    "SELECT StateValue FROM SyncState WHERE StateKey = @K;",
                    new SQLiteParameter("@K", key));

                return value == null || value == DBNull.Value ? defaultValue : Convert.ToString(value);
            }
            catch { return defaultValue; }
        }

        public static void SetState(string key, string value)
        {
            Try(delegate
            {
                Db.ExecuteNonQuery(@"
INSERT INTO SyncState (StateKey, StateValue, UpdatedAt)
VALUES (@K, @V, datetime('now'))
ON CONFLICT(StateKey) DO UPDATE SET StateValue = @V, UpdatedAt = datetime('now');",
                    new SQLiteParameter("@K", key),
                    new SQLiteParameter("@V", (object)(value ?? "")));
            }, "SetState");
        }

        // ─────────────────────────────────────────────────────────────────────
        // درونی
        // ─────────────────────────────────────────────────────────────────────
        private static void Insert(string operation, string entityName, string globalId,
                                   int localId, string parentGlobalId, int version, string payload)
        {
            // ⚠ امنیت (فاز ۸): هر ردیف صف، کاربر/رایانه/مرکزِ ثبت‌کننده را
            // نگه می‌دارد. بدون این، یک ردیفِ همگام‌سازی قابل انتساب نیست و
            // سرور نمی‌تواند مجوز ارسال را بررسی کند.
            Db.ExecuteNonQuery(@"
INSERT INTO SyncOutbox
    (OperationType, EntityName, EntityGlobalID, EntityLocalID, ParentGlobalID,
     RowVersion, Payload, CenterID, UserID, Username, MachineName, State)
VALUES
    (@Op, @Entity, @Guid, @LocalId, @Parent, @Version, @Payload, @Center, @UserId, @User, @Machine, @State);",
                new SQLiteParameter("@Op",      operation),
                new SQLiteParameter("@Entity",  entityName),
                new SQLiteParameter("@Guid",    (object)globalId ?? DBNull.Value),
                new SQLiteParameter("@LocalId", localId),
                new SQLiteParameter("@Parent",  (object)parentGlobalId ?? DBNull.Value),
                new SQLiteParameter("@Version", version),
                new SQLiteParameter("@Payload", (object)payload ?? DBNull.Value),
                new SQLiteParameter("@Center",  SecurityContext.CurrentCenterId > 0
                                                    ? (object)SecurityContext.CurrentCenterId : DBNull.Value),
                new SQLiteParameter("@UserId",  SecurityContext.UserId > 0
                                                    ? (object)SecurityContext.UserId : DBNull.Value),
                new SQLiteParameter("@User",    (object)SecurityContext.Username ?? DBNull.Value),
                new SQLiteParameter("@Machine", SafeMachineName()),
                new SQLiteParameter("@State",   OfflineSyncInitializer.StatePending));
        }

        private static int Touch(string entityName, string primaryKey, int localId, bool increment)
        {
            try
            {
                Db.ExecuteNonQuery(
                    "UPDATE " + entityName + " SET RowVersion = IFNULL(RowVersion, 1)" +
                    (increment ? " + 1" : "") + ", " +
                    "LastModifiedAt = datetime('now'), LastModifiedBy = @U WHERE " + primaryKey + " = @Id;",
                    new SQLiteParameter("@U", (object)SecurityContext.Username ?? DBNull.Value),
                    new SQLiteParameter("@Id", localId));

                object value = Db.ExecuteScalar(
                    "SELECT RowVersion FROM " + entityName + " WHERE " + primaryKey + " = @Id;",
                    new SQLiteParameter("@Id", localId));

                return value == null || value == DBNull.Value ? 1 : Convert.ToInt32(value);
            }
            catch { return 1; }
        }

        // برای رکوردهای فرزند، GlobalIDِ پروندهٔ والد هم ثبت می‌شود: سمت گیرنده
        // شناسهٔ عددیِ CasID ما را ندارد و باید والد را با هویت سراسری پیدا کند.
        private static string ParentGlobalId(string entityName, DataRow row)
        {
            if (string.Equals(entityName, "TblCase", StringComparison.OrdinalIgnoreCase)) return null;
            if (!row.Table.Columns.Contains("CasID")) return null;
            if (row["CasID"] == DBNull.Value) return null;

            try
            {
                object value = Db.ExecuteScalar(
                    "SELECT GlobalID FROM TblCase WHERE CasID = @Id;",
                    new SQLiteParameter("@Id", Convert.ToInt32(row["CasID"])));

                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
            catch { return null; }
        }

        // قالب «کلید=مقدار» در هر خط — همان قالبی که VersionService برای
        // Snapshot استفاده می‌کند، تا خواندن/مقایسه در کل برنامه یکسان بماند و
        // هیچ وابستگی JSON اضافه نشود.
        private static string Serialize(DataRow row)
        {
            var sb = new StringBuilder();
            foreach (DataColumn column in row.Table.Columns)
            {
                if (SkippedColumns.Contains(column.ColumnName)) continue;

                string value = row[column] == DBNull.Value ? "" : Convert.ToString(row[column]);
                value = value.Replace("\r", " ").Replace("\n", " ");
                sb.Append(column.ColumnName).Append('=').Append(value).AppendLine();
            }
            return sb.ToString();
        }

        private static DataRow ReadRow(string entityName, string primaryKey, int localId)
        {
            DataTable table = SafeQuery(
                "SELECT * FROM " + entityName + " WHERE " + primaryKey + " = @Id LIMIT 1;",
                new SQLiteParameter("@Id", localId));

            return table == null || table.Rows.Count == 0 ? null : table.Rows[0];
        }

        private static DataTable SafeQuery(string sql, params SQLiteParameter[] parameters)
        {
            try { return Db.Query(sql, parameters); }
            catch { return null; }
        }

        private static string PrimaryKeyOf(string entityName)
        {
            foreach (string[] table in OfflineSyncInitializer.SyncedTables)
                if (string.Equals(table[0], entityName, StringComparison.OrdinalIgnoreCase))
                    return table[1];

            return null;   // جدولِ پشتیبانی‌نشده — بی‌صدا رد می‌شود
        }

        // صف فقط وقتی پر می‌شود که زیرساختش موجود و کاربری وارد شده باشد.
        // ⚠ امنیت: بدون کاربرِ وارد شده، ردیفِ صف قابل انتساب نیست.
        private static bool IsReady()
        {
            return SecurityContext.IsLoggedIn && OutboxExists();
        }

        private static bool OutboxExists() { return TableExists("SyncOutbox"); }

        private static bool TableExists(string tableName)
        {
            try
            {
                object value = Db.ExecuteScalar(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@n;",
                    new SQLiteParameter("@n", tableName));

                return value != null && value != DBNull.Value && Convert.ToInt32(value) > 0;
            }
            catch { return false; }
        }

        private static string GetString(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            return Convert.ToString(row[column]);
        }

        private static int GetInt(DataRow row, string column, int fallback)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return fallback;
            try { return Convert.ToInt32(row[column]); } catch { return fallback; }
        }

        private static string SafeMachineName()
        {
            try { return Environment.MachineName; } catch { return ""; }
        }

        private static void Try(Action action, string source)
        {
            try { action(); } catch (Exception ex) { Swallow(ex, source); }
        }

        private static void Swallow(Exception ex, string source)
        {
            try { Enterprise.ErrorLogger.Log(ex, "SyncOutbox." + source); } catch { }
        }
    }
}

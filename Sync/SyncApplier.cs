using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // اعمالِ تغییراتِ دریافتی از سرور — جریان دریافت (فاز ۳).
    //
    // چهار قاعدهٔ حاکم بر این کلاس:
    //
    // ۱. «شناسایی فقط با GlobalID». شناسهٔ عددیِ محلی (CasID/FamID/…) هرگز
    //    از شبکه نمی‌آید و هرگز بازنویسی نمی‌شود؛ رکوردِ جدید شناسهٔ محلیِ
    //    خودش را از AUTOINCREMENT می‌گیرد. یعنی هویت محلی حفظ می‌شود و هیچ
    //    ارجاعِ داخلی (FK، مسیر فایل، بارکد) نمی‌شکند.
    //
    // ۲. «هرگز بازنویسیِ خاموش». اگر رکورد محلی تغییرِ ارسال‌نشده داشته باشد
    //    یا نسخه‌ها نخوانند، تغییرِ سرور *اعمال نمی‌شود*؛ تعارض ثبت می‌گردد و
    //    دادهٔ محلی دست‌نخورده می‌ماند. حل آن کار فاز ۴ است.
    //
    // ۳. «مدارا با اسکیما». فقط ستون‌هایی نوشته می‌شوند که واقعاً در این
    //    پایگاه‌داده وجود دارند. یک شعبه با نسخهٔ قدیمی‌تر باید بتواند
    //    داده بگیرد بدون این‌که ستونِ ناشناخته کل عملیات را بشکند.
    //
    // ۴. «بدون پژواک». نوشتن اینجا مستقیم و بدون SyncOutboxService انجام
    //    می‌شود، وگرنه هر تغییرِ دریافتی دوباره در صفِ ارسال می‌افتاد و بین
    //    دو شعبه حلقهٔ بی‌پایان می‌ساخت.
    // ═════════════════════════════════════════════════════════════════════════
    public static class SyncApplier
    {
        public enum ApplyOutcome
        {
            Inserted,
            Updated,
            Deleted,
            SkippedConflict,
            SkippedUnchanged,
            SkippedUnsupported
        }

        private static readonly DatabaseHelper Db = new DatabaseHelper();

        // ستون‌هایی که هرگز از روی داده‌ی دریافتی نوشته نمی‌شوند.
        private static readonly HashSet<string> NeverWrite =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GlobalID",        // هویت — کلید تطبیق است، نه دادهٔ قابل تغییر
                "RowVersion",      // جداگانه و کنترل‌شده ست می‌شود
                "LastModifiedAt",
                "LastModifiedBy",
                "SyncStatus",
                "CasID", "FamID", "DocID", "AssistanceID"   // شناسه‌های محلی
            };

        public static ApplyOutcome Apply(SyncChange change)
        {
            if (change == null || string.IsNullOrWhiteSpace(change.GlobalId))
                return ApplyOutcome.SkippedUnsupported;

            string primaryKey = PrimaryKeyOf(change.EntityName);
            if (primaryKey == null) return ApplyOutcome.SkippedUnsupported;

            DataRow local = FindByGlobalId(change.EntityName, change.GlobalId);

            bool isDelete = string.Equals(change.OperationType,
                OfflineSyncInitializer.OperationDelete, StringComparison.Ordinal);

            // ─── رکورد محلی وجود ندارد ───────────────────────────────────────
            if (local == null)
            {
                // حذفی که قبلاً هم اینجا نبوده ⇒ کاری لازم نیست (idempotent).
                if (isDelete) return ApplyOutcome.SkippedUnchanged;

                // ⚠ «کد اختصاصی» را کاربر تایپ می‌کند، پس دو شعبهٔ آفلاین
                // می‌توانند مستقلاً یک کد بسازند. این دو، دو *پروندهٔ متفاوت*
                // با دو هویت سراسری‌اند — نه دو نسخه از یک رکورد. درج آن
                // با خطای یکتایی شکست می‌خورد و بازنویسی‌اش فاجعه است، پس
                // به‌عنوان تعارض ثبت و برای تصمیم مدیر کنار گذاشته می‌شود.
                if (IsDuplicateUserCode(change)) return ApplyOutcome.SkippedConflict;

                if (!Insert(change, primaryKey)) return ApplyOutcome.SkippedUnsupported;

                RecordBaseline(change);
                return ApplyOutcome.Inserted;
            }

            int localVersion = GetInt(local, "RowVersion", 1);
            int localId      = GetInt(local, primaryKey, 0);

            // ─── تعارض ۱: تغییرِ محلیِ هنوز ارسال‌نشده ───────────────────────
            // اگر همین رکورد در صفِ ارسال منتظر است، یعنی کاربر محلی هم آن را
            // عوض کرده. اعمالِ نسخهٔ سرور یعنی نابود کردنِ کارِ او.
            if (HasPendingLocalChange(change.EntityName, change.GlobalId))
            {
                long conflictId = SyncConflictStore.Record(change.EntityName, change.GlobalId,
                    isDelete ? OfflineSyncInitializer.ConflictDeletedRemote
                             : OfflineSyncInitializer.ConflictConcurrentEdit,
                    0, localVersion, change.RowVersion,
                    Serialize(local), change.Payload);

                // سیاست پیش‌فرض فاز ۴: اگر تغییرات دو طرف روی فیلدهای متفاوتی
                // بوده، همین‌جا خودکار ادغام می‌شود و هرگز به میز مدیر نمی‌رسد.
                // فقط تعارضِ *واقعی* (یک فیلد، دو مقدار متفاوت) باقی می‌ماند.
                if (!isDelete && conflictId > 0 &&
                    SyncConflictResolver.TryAutoResolve(conflictId).Succeeded)
                    return ApplyOutcome.Updated;

                return ApplyOutcome.SkippedConflict;
            }

            // ─── تعارض ۲: نسخهٔ محلی از نسخهٔ سرور جلوتر است ─────────────────
            // نسخهٔ سرور کهنه است؛ اعمالش یعنی عقب بردنِ داده.
            if (change.RowVersion > 0 && localVersion > change.RowVersion)
            {
                SyncConflictStore.Record(change.EntityName, change.GlobalId,
                    OfflineSyncInitializer.ConflictVersion,
                    0, localVersion, change.RowVersion,
                    Serialize(local), change.Payload);

                return ApplyOutcome.SkippedConflict;
            }

            if (isDelete)
                return Delete(change.EntityName, primaryKey, localId) ? ApplyOutcome.Deleted
                                                                     : ApplyOutcome.SkippedUnsupported;

            // نسخهٔ یکسان و بدون تغییرِ محلی ⇒ چیزی برای اعمال نیست.
            if (change.RowVersion > 0 && localVersion == change.RowVersion)
                return ApplyOutcome.SkippedUnchanged;

            if (!Update(change, primaryKey, localId)) return ApplyOutcome.SkippedUnsupported;

            RecordBaseline(change);
            return ApplyOutcome.Updated;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ثبتِ «نقطهٔ توافق» پس از اعمالِ موفقِ یک تغییرِ دریافتی.
        //
        // در این لحظه رکوردِ محلی دقیقاً همان چیزی است که سرور فرستاده، پس
        // همین بار مبنای درستِ ادغامِ تعارض‌های بعدی است. (سمتِ ارسال هم در
        // SyncService پس از پاسخِ Accepted همین کار را می‌کند.)
        //
        // ⚠ عمداً *پس از* موفقیتِ نوشتن صدا زده می‌شود: مبنایی که اعمال نشده
        // باشد یعنی ادعای توافقی که هرگز رخ نداده.
        private static void RecordBaseline(SyncChange change)
        {
            SyncBaselineStore.Set(change.EntityName, change.GlobalId,
                change.Payload, change.RowVersion,
                OfflineSyncInitializer.BaselineFromPull);
        }

        // ─────────────────────────────────────────────────────────────────────
        private static bool Insert(SyncChange change, string primaryKey)
        {
            Dictionary<string, string> values = Deserialize(change.Payload);
            if (values.Count == 0) return false;

            HashSet<string> columns = GetColumns(change.EntityName);
            if (columns.Count == 0) return false;

            // رکوردِ فرزند باید به والدِ *محلی* وصل شود؛ شناسهٔ والد در مبدأ
            // با اینجا فرق دارد، پس از روی هویت سراسریِ والد پیدا می‌شود.
            int parentLocalId = ResolveParent(change);
            if (parentLocalId < 0) return false;   // والد هنوز نرسیده — بعداً دوباره تلاش می‌شود

            var names = new List<string>();
            var parameters = new List<SQLiteParameter>();
            int index = 0;

            foreach (var pair in values)
            {
                if (NeverWrite.Contains(pair.Key)) continue;
                if (!columns.Contains(pair.Key)) continue;   // مدارا با اسکیمای قدیمی‌تر

                string parameterName = "@p" + index++;
                names.Add("[" + pair.Key + "]");
                parameters.Add(new SQLiteParameter(parameterName, (object)pair.Value ?? DBNull.Value));
            }

            if (names.Count == 0) return false;

            // هویت سراسری و نسخه صریحاً نوشته می‌شوند.
            names.Add("[GlobalID]");
            parameters.Add(new SQLiteParameter("@guid", change.GlobalId));

            if (columns.Contains("RowVersion"))
            {
                names.Add("[RowVersion]");
                parameters.Add(new SQLiteParameter("@ver", Math.Max(1, change.RowVersion)));
            }

            if (parentLocalId > 0 && columns.Contains("CasID") && !names.Contains("[CasID]"))
            {
                names.Add("[CasID]");
                parameters.Add(new SQLiteParameter("@parent", parentLocalId));
            }

            var placeholders = new List<string>();
            foreach (SQLiteParameter parameter in parameters) placeholders.Add(parameter.ParameterName);

            string sql = "INSERT INTO [" + change.EntityName + "] (" + string.Join(", ", names.ToArray()) +
                         ") VALUES (" + string.Join(", ", placeholders.ToArray()) + ");";

            Db.ExecuteNonQuery(sql, parameters.ToArray());
            return true;
        }

        private static bool Update(SyncChange change, string primaryKey, int localId)
        {
            Dictionary<string, string> values = Deserialize(change.Payload);
            if (values.Count == 0) return false;

            HashSet<string> columns = GetColumns(change.EntityName);

            var assignments = new List<string>();
            var parameters = new List<SQLiteParameter>();
            int index = 0;

            foreach (var pair in values)
            {
                if (NeverWrite.Contains(pair.Key)) continue;
                if (!columns.Contains(pair.Key)) continue;

                string parameterName = "@p" + index++;
                assignments.Add("[" + pair.Key + "] = " + parameterName);
                parameters.Add(new SQLiteParameter(parameterName, (object)pair.Value ?? DBNull.Value));
            }

            if (assignments.Count == 0) return false;

            if (columns.Contains("RowVersion"))
            {
                assignments.Add("[RowVersion] = @ver");
                parameters.Add(new SQLiteParameter("@ver", Math.Max(1, change.RowVersion)));
            }

            if (columns.Contains("LastModifiedAt"))
                assignments.Add("[LastModifiedAt] = datetime('now')");

            if (columns.Contains("LastModifiedBy"))
            {
                assignments.Add("[LastModifiedBy] = @by");
                parameters.Add(new SQLiteParameter("@by", (object)change.Username ?? DBNull.Value));
            }

            parameters.Add(new SQLiteParameter("@id", localId));

            Db.ExecuteNonQuery(
                "UPDATE [" + change.EntityName + "] SET " + string.Join(", ", assignments.ToArray()) +
                " WHERE [" + primaryKey + "] = @id;",
                parameters.ToArray());

            return true;
        }

        private static bool Delete(string entityName, string primaryKey, int localId)
        {
            if (localId <= 0) return false;

            Db.ExecuteNonQuery(
                "DELETE FROM [" + entityName + "] WHERE [" + primaryKey + "] = @id;",
                new SQLiteParameter("@id", localId));

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // کدِ اختصاصیِ تکراری بین دو شعبه.
        //
        // تعارض با هر دو رکورد کامل ثبت می‌شود (شاملِ CenterID و CreatedAt که
        // هر دو در بارِ داده هستند) تا مدیر بتواند تصمیم بگیرد کدام پرونده کد
        // را نگه دارد.
        private static bool IsDuplicateUserCode(SyncChange change)
        {
            if (!string.Equals(change.EntityName, "TblCase", StringComparison.OrdinalIgnoreCase))
                return false;

            Dictionary<string, string> incoming = Deserialize(change.Payload);

            string code;
            if (!incoming.TryGetValue("Code", out code) || string.IsNullOrWhiteSpace(code))
                return false;

            try
            {
                DataTable existing = Db.Query(
                    "SELECT * FROM TblCase WHERE Code = @C AND IFNULL(GlobalID,'') <> @G LIMIT 1;",
                    new SQLiteParameter("@C", code),
                    new SQLiteParameter("@G", change.GlobalId ?? ""));

                if (existing.Rows.Count == 0) return false;

                DataRow local = existing.Rows[0];

                SyncConflictStore.Record(change.EntityName, change.GlobalId,
                    OfflineSyncInitializer.ConflictDuplicateCode, 0,
                    GetInt(local, "RowVersion", 1), change.RowVersion,
                    Serialize(local), change.Payload);

                return true;
            }
            catch { return false; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // آیا این رکورد تغییرِ محلیِ ارسال‌نشده دارد؟
        private static bool HasPendingLocalChange(string entityName, string globalId)
        {
            try
            {
                object value = Db.ExecuteScalar(
                    "SELECT COUNT(1) FROM SyncOutbox WHERE EntityName = @E AND EntityGlobalID = @G " +
                    "AND State IN (@P, @F);",
                    new SQLiteParameter("@E", entityName),
                    new SQLiteParameter("@G", globalId),
                    new SQLiteParameter("@P", OfflineSyncInitializer.StatePending),
                    new SQLiteParameter("@F", OfflineSyncInitializer.StateFailed));

                return value != null && value != DBNull.Value && Convert.ToInt32(value) > 0;
            }
            catch { return false; }
        }

        // شناسهٔ محلیِ والد از روی هویت سراسری.
        //   0  ⇒ والد لازم نیست (خودِ پرونده)
        //   >0 ⇒ پیدا شد
        //   -1 ⇒ والد هنوز در این پایگاه‌داده نیست
        private static int ResolveParent(SyncChange change)
        {
            if (string.Equals(change.EntityName, "TblCase", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.IsNullOrWhiteSpace(change.ParentGlobalId)) return 0;

            try
            {
                object value = Db.ExecuteScalar(
                    "SELECT CasID FROM TblCase WHERE GlobalID = @G LIMIT 1;",
                    new SQLiteParameter("@G", change.ParentGlobalId));

                if (value == null || value == DBNull.Value) return -1;
                return Convert.ToInt32(value);
            }
            catch { return -1; }
        }

        private static DataRow FindByGlobalId(string entityName, string globalId)
        {
            try
            {
                DataTable table = Db.Query(
                    "SELECT * FROM [" + entityName + "] WHERE GlobalID = @G LIMIT 1;",
                    new SQLiteParameter("@G", globalId));

                return table.Rows.Count == 0 ? null : table.Rows[0];
            }
            catch { return null; }
        }

        private static HashSet<string> GetColumns(string entityName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                DataTable info = Db.Query("PRAGMA table_info([" + entityName + "]);");
                foreach (DataRow row in info.Rows) columns.Add(Convert.ToString(row["name"]));
            }
            catch { }
            return columns;
        }

        // ─── قالبِ «کلید=مقدار» — همان قالبِ SyncOutbox و VersionService ─────
        public static Dictionary<string, string> Deserialize(string payload)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(payload)) return values;

            foreach (string line in payload.Split('\n'))
            {
                string trimmed = line.Trim('\r', ' ');
                if (trimmed.Length == 0) continue;

                int separator = trimmed.IndexOf('=');
                if (separator <= 0) continue;

                string key = trimmed.Substring(0, separator).Trim();
                string value = trimmed.Substring(separator + 1);
                if (key.Length > 0) values[key] = value;
            }
            return values;
        }

        private static string Serialize(DataRow row)
        {
            var sb = new System.Text.StringBuilder();
            foreach (DataColumn column in row.Table.Columns)
            {
                string value = row[column] == DBNull.Value ? "" : Convert.ToString(row[column]);
                sb.Append(column.ColumnName).Append('=')
                  .Append(value.Replace("\r", " ").Replace("\n", " ")).AppendLine();
            }
            return sb.ToString();
        }

        private static string PrimaryKeyOf(string entityName)
        {
            foreach (string[] table in OfflineSyncInitializer.SyncedTables)
                if (string.Equals(table[0], entityName, StringComparison.OrdinalIgnoreCase))
                    return table[1];
            return null;
        }

        private static int GetInt(DataRow row, string column, int fallback)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return fallback;
            try { return Convert.ToInt32(row[column]); } catch { return fallback; }
        }
    }
}

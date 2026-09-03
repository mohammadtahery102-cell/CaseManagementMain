using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Enterprise;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // حل تعارض — فاز ۴.
    //
    // چهار مسیر حل:
    //   • خودکار      — فقط وقتی هیچ فیلدِ متعارضی وجود ندارد (ادغام فیلدی)
    //   • پذیرش محلی  — نسخهٔ این شعبه معتبر است
    //   • پذیرش سرور  — نسخهٔ سرور معتبر است
    //   • ادغام دستی  — مدیر برای هر فیلد جداگانه تصمیم می‌گیرد
    //
    // در هر چهار حالت، پس از اعمال:
    //   ۱. RowVersion از هر دو طرف بالاتر می‌رود (تا نتیجه دوباره «کهنه» دیده نشود)
    //   ۲. یک رکورد ممیزی با سرویس موجود ثبت می‌شود
    //   ۳. یک نسخه در تاریخچهٔ موجود (VersionService) ثبت می‌شود
    //   ۴. تعارض «حل شد» علامت می‌خورد
    //   ۵. نتیجه دوباره در صفِ ارسال می‌رود تا سرور هم همان را ببیند
    //
    // ⚠ هیچ مسیری دادهٔ طرف مقابل را «بی‌صدا» دور نمی‌ریزد: هر دو نسخه در
    // SyncConflict باقی می‌مانند و متن ممیزی می‌گوید چه چیزی به نفع چه کسی
    // تصمیم گرفته شد.
    // ═════════════════════════════════════════════════════════════════════════
    public static class SyncConflictResolver
    {
        private static readonly DatabaseHelper Db = new DatabaseHelper();

        public const string ResolutionAuto   = "ادغام خودکار";
        public const string ResolutionLocal  = "پذیرش نسخهٔ محلی";
        public const string ResolutionRemote = "پذیرش نسخهٔ سرور";
        public const string ResolutionManual = "ادغام دستی";
        public const string ResolutionDelete = "پذیرش حذف سرور";

        public sealed class ResolutionResult
        {
            public bool   Succeeded;
            public bool   RequiresReview;
            public string Message = "";
            public int    MergedFields;
            public int    ContestedFields;
        }

        // ─────────────────────────────────────────────────────────────────────
        // تلاش برای حل خودکار — مسیر پیش‌فرض پس از هر تشخیص تعارض.
        // ─────────────────────────────────────────────────────────────────────
        public static ResolutionResult TryAutoResolve(long conflictId)
        {
            var result = new ResolutionResult();

            try
            {
                DataRow conflict = Load(conflictId);
                if (conflict == null)
                {
                    result.Message = "تعارض پیدا نشد.";
                    return result;
                }

                SyncConflictAnalyzer.ConflictAnalysis analysis =
                    SyncConflictAnalyzer.Analyze(conflict);

                result.ContestedFields = analysis.Contested.Count;

                if (!analysis.CanAutoMerge)
                {
                    result.RequiresReview = true;
                    result.Message = analysis.IsDeleteConflict
                        ? "تعارض حذف — تصمیم مدیر لازم است."
                        : analysis.IsDuplicateCode
                            ? "کد اختصاصی تکراری — تصمیم مدیر لازم است."
                            : analysis.Contested.Count + " فیلد نیازمند تصمیم مدیر است.";
                    return result;
                }

                Dictionary<string, string> merged = analysis.MergedValues();

                // هیچ تغییری برای اعمال نیست ⇒ صرفاً تعارض بسته می‌شود.
                Apply(analysis, merged, ResolutionAuto, out result.MergedFields);

                result.Succeeded = true;
                result.Message = "ادغام خودکار انجام شد (" + result.MergedFields + " فیلد).";
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Log(ex, "TryAutoResolve");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        public static ResolutionResult AcceptLocal(long conflictId)
        {
            return Resolve(conflictId, ResolutionLocal, delegate (SyncConflictAnalyzer.ConflictAnalysis a)
            {
                // نسخهٔ محلی از قبل روی دیسک است؛ فقط نسخه بالا می‌رود و
                // دوباره ارسال می‌شود تا سرور همین را بپذیرد.
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            });
        }

        public static ResolutionResult AcceptRemote(long conflictId)
        {
            return Resolve(conflictId, ResolutionRemote, delegate (SyncConflictAnalyzer.ConflictAnalysis a)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // ⚠ فقط فیلدهایی که سرور واقعاً دربارهٔ آن‌ها حرفی زده است.
                // اگر فیلدهای غایب را هم «مقدار سرور» می‌گرفتیم، بارِ جزئیِ
                // سرور بقیهٔ ستون‌ها را خالی می‌کرد — که هم دادهٔ واقعی را
                // نابود می‌کرد و هم قیدهای NOT NULL/CHECK را می‌شکست.
                foreach (SyncConflictAnalyzer.FieldDecision field in a.Fields)
                    if (field.InRemote && field.Outcome != SyncConflictAnalyzer.FieldOutcome.Same)
                        values[field.Field] = field.RemoteValue;

                return values;
            });
        }

        // ادغام دستی: مدیر برای هر فیلدِ متعارض مقدار نهایی را می‌دهد.
        public static ResolutionResult ResolveManual(long conflictId,
            IDictionary<string, string> chosenValues)
        {
            return Resolve(conflictId, ResolutionManual, delegate (SyncConflictAnalyzer.ConflictAnalysis a)
            {
                // ابتدا ادغامِ خودکارِ فیلدهای غیرمتعارض، سپس انتخاب‌های مدیر.
                Dictionary<string, string> values = a.MergedValues();

                if (chosenValues != null)
                    foreach (var pair in chosenValues) values[pair.Key] = pair.Value;

                return values;
            });
        }

        // پذیرش حذفِ سرور — رکورد محلی حذف می‌شود.
        public static ResolutionResult AcceptRemoteDelete(long conflictId)
        {
            var result = new ResolutionResult();

            try
            {
                DataRow conflict = Load(conflictId);
                if (conflict == null) { result.Message = "تعارض پیدا نشد."; return result; }

                SyncConflictAnalyzer.ConflictAnalysis analysis = SyncConflictAnalyzer.Analyze(conflict);

                string primaryKey = PrimaryKeyOf(analysis.EntityName);
                if (primaryKey == null) { result.Message = "جدول پشتیبانی نمی‌شود."; return result; }

                DataRow local = FindByGlobalId(analysis.EntityName, analysis.GlobalId);
                if (local != null)
                {
                    int localId = Convert.ToInt32(local[primaryKey]);

                    AuditLogger.Log("حل تعارض — حذف", analysis.EntityName, localId,
                                    Serialize(local), "حذف بر اساس تصمیم سرور");

                    try { VersionService.CaptureDeleted(analysis.EntityName, localId, Serialize(local)); }
                    catch { }

                    Db.ExecuteNonQuery(
                        "DELETE FROM [" + analysis.EntityName + "] WHERE [" + primaryKey + "] = @id;",
                        new SQLiteParameter("@id", localId));
                }

                // تغییرات محلیِ منتظرِ ارسال دیگر بی‌معنا هستند: رکورد رفته و
                // سرور هم آن را ندارد. ارسالشان فقط خطا تولید می‌کند.
                SyncOutboxService.Discard(analysis.EntityName, analysis.GlobalId,
                                          "رکورد بر اساس تصمیم مدیر حذف شد");

                SyncConflictStore.MarkResolved(conflictId, ResolutionDelete);

                result.Succeeded = true;
                result.Message = "حذف سرور پذیرفته شد.";
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Log(ex, "AcceptRemoteDelete");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        private static ResolutionResult Resolve(long conflictId, string resolution,
            Func<SyncConflictAnalyzer.ConflictAnalysis, Dictionary<string, string>> chooser)
        {
            var result = new ResolutionResult();

            try
            {
                DataRow conflict = Load(conflictId);
                if (conflict == null) { result.Message = "تعارض پیدا نشد."; return result; }

                SyncConflictAnalyzer.ConflictAnalysis analysis = SyncConflictAnalyzer.Analyze(conflict);

                // تعارضِ «کد تکراری» با این مسیرها حل نمی‌شود: هر دو طرف
                // رکوردهای *متفاوتی* هستند، نه دو نسخه از یک رکورد.
                if (analysis.IsDuplicateCode)
                {
                    result.RequiresReview = true;
                    result.Message = "کد تکراری با ادغام حل نمی‌شود؛ یکی از دو پرونده باید کد دیگری بگیرد.";
                    return result;
                }

                if (analysis.IsDeleteConflict && resolution == ResolutionRemote)
                    return AcceptRemoteDelete(conflictId);

                Apply(analysis, chooser(analysis), resolution, out result.MergedFields);

                result.Succeeded = true;
                result.Message = resolution + " انجام شد.";
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Log(ex, "Resolve/" + resolution);
            }

            return result;
        }

        // هستهٔ اعمال — مشترک بین همهٔ مسیرها.
        private static void Apply(SyncConflictAnalyzer.ConflictAnalysis analysis,
                                  Dictionary<string, string> values, string resolution,
                                  out int appliedFields)
        {
            appliedFields = 0;

            string primaryKey = PrimaryKeyOf(analysis.EntityName);
            if (primaryKey == null) throw new InvalidOperationException("جدول پشتیبانی نمی‌شود.");

            DataRow local = FindByGlobalId(analysis.EntityName, analysis.GlobalId);
            if (local == null) throw new InvalidOperationException("رکورد محلی پیدا نشد.");

            int localId = Convert.ToInt32(local[primaryKey]);
            string before = Serialize(local);

            HashSet<string> columns = GetColumns(analysis.EntityName);

            var assignments = new List<string>();
            var parameters = new List<SQLiteParameter>();
            int index = 0;

            foreach (var pair in values)
            {
                if (!columns.Contains(pair.Key)) continue;   // مدارا با اسکیمای متفاوت

                string parameterName = "@p" + index++;
                assignments.Add("[" + pair.Key + "] = " + parameterName);
                parameters.Add(new SQLiteParameter(parameterName, (object)pair.Value ?? DBNull.Value));
                appliedFields++;
            }

            // ⚠ نسخه از *هر دو* طرف بالاتر می‌رود. اگر فقط یکی را مبنا
            // می‌گرفتیم، نتیجهٔ حل‌شده می‌توانست در همگام‌سازی بعدی دوباره
            // «کهنه» تشخیص داده و رد شود.
            int nextVersion = Math.Max(analysis.LocalVersion, analysis.RemoteVersion);
            if (nextVersion < 1) nextVersion = 1;

            if (columns.Contains("RowVersion"))
            {
                assignments.Add("[RowVersion] = @ver");
                parameters.Add(new SQLiteParameter("@ver", nextVersion));
            }

            if (assignments.Count > 0)
            {
                parameters.Add(new SQLiteParameter("@id", localId));

                Db.ExecuteNonQuery(
                    "UPDATE [" + analysis.EntityName + "] SET " + string.Join(", ", assignments.ToArray()) +
                    " WHERE [" + primaryKey + "] = @id;",
                    parameters.ToArray());
            }

            DataRow after = FindByGlobalId(analysis.EntityName, analysis.GlobalId);

            // ─── ردّ ممیزی با سرویس موجود ───
            AuditLogger.Log("حل تعارض همگام‌سازی — " + resolution,
                            analysis.EntityName, localId, before,
                            after == null ? "" : Serialize(after));

            // ─── تاریخچهٔ نسخه با سرویس موجود ───
            try { VersionService.Capture(analysis.EntityName, localId, VersionService.OperationUpdate); }
            catch { }

            // ─── ثبت امنیتی ───
            try
            {
                SecurityAudit.Log("همگام‌سازی", "بالا", true,
                    "حل تعارض " + analysis.EntityName + " (" + resolution + ")",
                    analysis.EntityName, localId);
            }
            catch { }

            SyncConflictStore.MarkResolved(analysis.ConflictId, resolution);

            // ─── ارسال دوبارهٔ نتیجه ───
            // Capture خودش RowVersion را یک واحد جلو می‌برد، پس نسخهٔ نهایی
            // قطعاً از هر دو طرف بزرگ‌تر است.
            SyncOutboxService.Capture(analysis.EntityName, localId,
                                      OfflineSyncInitializer.OperationUpdate);
        }

        // ─────────────────────────────────────────────────────────────────────
        public static DataRow Load(long conflictId)
        {
            try
            {
                DataTable table = Db.Query(
                    "SELECT * FROM SyncConflict WHERE ConflictID = @Id;",
                    new SQLiteParameter("@Id", conflictId));

                return table.Rows.Count == 0 ? null : table.Rows[0];
            }
            catch { return null; }
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

        private static void Log(Exception ex, string source)
        {
            try { ErrorLogger.Log(ex, "SyncConflictResolver." + source); } catch { }
        }
    }
}

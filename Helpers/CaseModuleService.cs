using CaseManagement.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace CaseManagement.Helpers
{
    // ═══════════════════════════════════════════════════════════════════════
    // Phase 4 — تنها نویسنده/خوانندهٔ سه جدولِ ماژولِ تخصصی
    // (TblOrphan / TblDisability / TblMigrant).
    //
    // چرا همه‌چیز اینجاست و نه در FrmCase: راهبردِ dual-write یعنی هر فیلدِ
    // مشترک باید هم در جدولِ ماژول و هم در ستونِ هم‌نامِ TblCase نوشته شود.
    // اگر این دو نوشتن در کدِ فرم پخش می‌شد، دیر یا زود یکی به‌روز می‌شد و
    // دیگری جا می‌ماند (همان کلاسِ باگی که در فاز ۱ برای مقادیرِ فارسیِ
    // هاردکد پیدا شد). با تمرکز در این کلاس، «هر دو نوشتن» یک تصمیمِ واحد
    // در یک فایل است.
    //
    // ستون‌های TblCase که *عمداً* dual-write نمی‌شوند:
    //   • TblOrphan.EducationLevel — تحصیلاتِ خودِ یتیم است، در حالی که
    //     TblCase.EducationLevel تحصیلاتِ سرپرستِ خانوار است (و در گزارشِ
    //     RDLC/جستجو با همین معنا خوانده می‌شود). یکی‌کردنشان مقدارِ سرپرست
    //     را با مقدارِ کودک بازنویسی می‌کرد.
    //   • TblOrphan.SchoolName — TblFamily.SchoolName مکتبِ هر عضو است
    //     (سطحِ عضو، نه سطحِ پرونده)؛ ستونی روی TblCase وجود ندارد.
    // ═══════════════════════════════════════════════════════════════════════
    public static class CaseModuleService
    {
        public const string TableOrphan     = "TblOrphan";
        public const string TableDisability = "TblDisability";
        public const string TableMigrant    = "TblMigrant";

        public const string TitleOrphan     = "اطلاعات ایتام";
        public const string TitleDisability = "اطلاعات معلولیت";
        public const string TitleMigrant    = "اطلاعات مهاجرت";

        // ─── خواندن ──────────────────────────────────────────────────────────
        // اگر ردیفی وجود نداشته باشد null برمی‌گردد (پروندهٔ تازه، یا نوعی که
        // این ماژول را ندارد) — فراخوان باید همین را «خالی» تفسیر کند.
        public static DataRow Load(string moduleTable, int casId)
        {
            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                using (var cmd = new SQLiteCommand(
                    "SELECT * FROM [" + moduleTable + "] WHERE CasID = @CasID LIMIT 1;", con))
                {
                    cmd.Parameters.AddWithValue("@CasID", casId);
                    con.Open();
                    using (var adapter = new SQLiteDataAdapter(cmd))
                    {
                        var table = new DataTable();
                        adapter.Fill(table);
                        return table.Rows.Count == 0 ? null : table.Rows[0];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CaseModuleService.Load failed: " + ex.Message);
                return null;
            }
        }

        public static bool Exists(string moduleTable, int casId)
        {
            return Load(moduleTable, casId) != null;
        }

        // ─── نوشتن (UPSERT) ──────────────────────────────────────────────────
        // values: نامِ ستونِ جدولِ ماژول → مقدار.
        // caseMirror: نامِ ستونِ TblCase → مقدار (سطحِ سازگاری؛ ممکن است خالی
        // باشد اگر ماژولی فیلدِ مشترکی نداشته باشد).
        //
        // خروجی: شناسهٔ ردیفِ ماژول. تایم‌لاین و بازمحاسبهٔ کامل‌بودن همین‌جا
        // انجام می‌شوند تا هیچ فراخوانی نتواند فراموششان کند.
        public static int Save(string moduleTable, string moduleTitle, int casId,
            Dictionary<string, object> values, Dictionary<string, object> caseMirror)
        {
            if (casId <= 0 || values == null || values.Count == 0) return 0;

            int moduleId = 0;
            bool isNew;
            // مقادیرِ پیش از تغییر — برای ثبتِ «چه فیلدی از چه به چه» در
            // تایم‌لاین. داخلِ همان تراکنش خوانده می‌شود تا دقیقاً همان چیزی
            // باشد که بازنویسی می‌شود.
            Dictionary<string, string> before = null;

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                {
                    con.Open();

                    // ── تراکنش: نوشتنِ جدولِ ماژول و آینهٔ TblCase یک واحدِ
                    // تجزیه‌ناپذیر است.
                    //
                    // چرا لازم است: کلِ دلیلِ وجودِ این کلاس (نگاه کنید سرآیندِ
                    // فایل) این است که این دو نوشتن هرگز از هم جدا نیفتند.
                    // بدونِ تراکنش، شکستِ MirrorToCase بعد از موفقیتِ INSERT
                    // همان واگراییِ dual-write را می‌ساخت که قرار بود جلویش
                    // گرفته شود — و چون RDLC/خروجی‌ها/جستجو/داشبورد ستون‌های
                    // آینه را می‌خوانند، پرونده روی صفحه یک درجهٔ معلولیت
                    // نشان می‌داد و در هر گزارش درجهٔ دیگری.
                    //
                    // امضای متدهای کمکی عوض نشده: تراکنشِ SQLite در سطحِ
                    // *اتصال* است، پس هر SQLiteCommand روی همین con خودبه‌خود
                    // داخلِ همین تراکنش اجرا می‌شود.
                    using (var tr = con.BeginTransaction())
                    {
                        moduleId = GetExistingId(con, moduleTable, casId);
                        isNew = moduleId == 0;

                        if (isNew)
                        {
                            moduleId = Insert(con, moduleTable, casId, values);
                        }
                        else
                        {
                            before = ReadCurrentValues(con, moduleTable, moduleId, values);
                            Update(con, moduleTable, moduleId, values);
                        }

                        // سطحِ سازگاری: همان فیلدها روی TblCase.
                        if (caseMirror != null && caseMirror.Count > 0)
                            MirrorToCase(con, casId, caseMirror);

                        tr.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                // برخلافِ تایم‌لاین، شکستِ اینجا دادهٔ کسب‌وکاری را از دست
                // می‌دهد؛ پس بی‌صدا رد نمی‌شود — به فراخوان برمی‌گردد.
                // خروج از using بدونِ Commit خودش تراکنش را برمی‌گرداند.
                System.Diagnostics.Debug.WriteLine("CaseModuleService.Save failed: " + ex.Message);
                throw;
            }

            if (isNew)
            {
                TimelineService.LogModuleRecordCreated(casId, moduleTable, moduleId, moduleTitle);
            }
            else
            {
                TimelineService.LogModuleRecordUpdated(casId, moduleTable, moduleId, moduleTitle);
                // ردیفِ «ویرایش شد» به‌تنهایی برای حسابرسی کافی نیست. ستون‌های
                // FieldName/OldValue/NewValue از قبل در TblCaseTimeline بودند و
                // این مسیر تا امروز خالی رهایشان می‌کرد.
                LogFieldChanges(casId, moduleTable, moduleId, moduleTitle, before, values);
            }

            SyncOutboxCapture(moduleTable, moduleId, isNew);
            CaseCompletionService.RecalculateAndStore(casId);
            // Phase 5.5-B — دادهٔ ماژول مستقیماً روی امتیاز اثر دارد.
            VulnerabilityScoreService.RecalculateAndStore(casId, VulnerabilityScoreService.ReasonModuleSaved);

            return moduleId;
        }

        // ─── حذف ────────────────────────────────────────────────────────────
        // ستون‌های آینهٔ TblCase عمداً پاک *نمی‌شوند*: گزارشِ RDLC/جستجو/
        // خروجی‌ها همان ستون‌ها را می‌خوانند و خالی‌کردنشان تغییرِ رفتارِ
        // آن سامانه‌ها بود — که در دامنهٔ این فاز نیست.
        public static bool Delete(string moduleTable, string moduleTitle, int casId)
        {
            int moduleId;
            // صفِ همگام‌سازیِ حذف — باید *پیش از* DELETE برداشته شود، چون
            // هویتِ رکورد (GlobalID/ParentGlobalID) فقط تا وقتی ردیف هست
            // خواندنی است. همان الگوی دومرحله‌ایِ SyncOutboxService که
            // FrmCase/FrmDocs هم استفاده می‌کنند.
            CaseManagement.Sync.SyncOutboxService.PendingDelete pending = null;

            try
            {
                using (var con = new DatabaseHelper().GetConnection())
                {
                    con.Open();
                    moduleId = GetExistingId(con, moduleTable, casId);
                    if (moduleId == 0) return false;

                    pending = CaseManagement.Sync.SyncOutboxService.PrepareDelete(moduleTable, moduleId);

                    using (var cmd = new SQLiteCommand(
                        "DELETE FROM [" + moduleTable + "] WHERE " + PrimaryKeyOf(moduleTable) + " = @Id;", con))
                    {
                        cmd.Parameters.AddWithValue("@Id", moduleId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CaseModuleService.Delete failed: " + ex.Message);
                throw;
            }

            // فقط بعد از حذفِ واقعاً انجام‌شده ثبت می‌شود.
            //
            // چرا این خط نبود و چرا مهم است: Save حذف را ثبت نمی‌کرد در حالی
            // که ایجاد/ویرایش را ثبت می‌کند، و TblDisability/TblOrphan/
            // TblMigrant هر سه در SyncedTables ثبت‌شده‌اند. نتیجه‌اش این بود
            // که پاک‌کردنِ اطلاعات معلولیت در یک شعبه هرگز به دفترِ مرکزی
            // نمی‌رسید؛ ردیفِ کهنه آنجا می‌ماند و در همگام‌سازیِ بعدی دوباره
            // به همین شعبه برمی‌گشت.
            CaseManagement.Sync.SyncOutboxService.CommitDelete(pending);

            TimelineService.LogModuleRecordDeleted(casId, moduleTable, moduleId, moduleTitle);
            CaseCompletionService.RecalculateAndStore(casId);
            // Phase 5.5-B — دادهٔ ماژول مستقیماً روی امتیاز اثر دارد.
            VulnerabilityScoreService.RecalculateAndStore(casId, VulnerabilityScoreService.ReasonModuleSaved);
            return true;
        }

        // ─── درونی ──────────────────────────────────────────────────────────
        public static string PrimaryKeyOf(string moduleTable)
        {
            if (string.Equals(moduleTable, TableOrphan, StringComparison.OrdinalIgnoreCase)) return "OrphanID";
            if (string.Equals(moduleTable, TableDisability, StringComparison.OrdinalIgnoreCase)) return "DisabilityID";
            if (string.Equals(moduleTable, TableMigrant, StringComparison.OrdinalIgnoreCase)) return "MigrantID";
            throw new ArgumentException("جدولِ ماژولِ ناشناخته: " + moduleTable);
        }

        // ─── حسابرسیِ سطحِ فیلد (H3) ─────────────────────────────────────────
        // فقط فیلدهایی که واقعاً عوض شده‌اند ثبت می‌شوند؛ ذخیرهٔ بدونِ تغییر
        // نباید تایم‌لاین را با ردیف‌های بی‌محتوا پر کند.
        private static void LogFieldChanges(int casId, string moduleTable, int moduleId,
            string moduleTitle, Dictionary<string, string> before, Dictionary<string, object> values)
        {
            if (before == null) return;

            foreach (var pair in values)
            {
                string oldValue;
                if (!before.TryGetValue(pair.Key, out oldValue)) oldValue = "";
                string newValue = pair.Value == null ? "" : pair.Value.ToString();

                if (string.Equals(oldValue ?? "", newValue, StringComparison.Ordinal)) continue;

                TimelineService.LogModuleFieldChanged(casId, moduleTable, moduleId, moduleTitle,
                    pair.Key, oldValue ?? "", newValue);
            }
        }

        // مقدارِ فعلیِ همان ستون‌هایی که قرار است نوشته شوند.
        private static Dictionary<string, string> ReadCurrentValues(SQLiteConnection con,
            string moduleTable, int moduleId, Dictionary<string, object> values)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (values.Count == 0) return result;

            var wanted = new List<string>(values.Keys);
            var quoted = new List<string>();
            foreach (string column in wanted) quoted.Add("[" + column + "]");

            try
            {
                using (var cmd = new SQLiteCommand("SELECT " + string.Join(", ", quoted.ToArray()) +
                    " FROM [" + moduleTable + "] WHERE " + PrimaryKeyOf(moduleTable) + " = @Id;", con))
                {
                    cmd.Parameters.AddWithValue("@Id", moduleId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return result;
                        foreach (string column in wanted)
                        {
                            object raw = dr[column];
                            result[column] = raw == null || raw == DBNull.Value ? "" : raw.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // خواندنِ مقدارِ قبلی فقط برای حسابرسی است — شکستش نباید
                // ذخیرهٔ خودِ داده را بشکند. در این حالت ردیفِ «ویرایش شد»
                // ثبت می‌شود ولی جزئیاتِ فیلدها نه.
                System.Diagnostics.Debug.WriteLine("CaseModuleService.ReadCurrentValues failed: " + ex.Message);
                return null;
            }

            return result;
        }

        private static int GetExistingId(SQLiteConnection con, string moduleTable, int casId)
        {
            using (var cmd = new SQLiteCommand(
                "SELECT " + PrimaryKeyOf(moduleTable) + " FROM [" + moduleTable + "] WHERE CasID = @CasID LIMIT 1;", con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        private static int Insert(SQLiteConnection con, string moduleTable, int casId, Dictionary<string, object> values)
        {
            var columns = new List<string> { "CasID", "CenterID", "CreatedBy", "GlobalID" };
            var placeholders = new List<string>
            {
                "@CasID", "@CenterID", "@CreatedBy",
                // همان تولیدکنندهٔ GlobalID که EnsureChildGlobalId و FrmDocs
                // استفاده می‌کنند — تا رکوردِ تازه از همان ابتدا قابلِ سینک باشد.
                "lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' || " +
                "lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6)))"
            };

            foreach (var pair in values)
            {
                columns.Add("[" + pair.Key + "]");
                placeholders.Add("@" + pair.Key);
            }

            string sql = "INSERT INTO [" + moduleTable + "] (" + string.Join(", ", columns.ToArray()) +
                         ") VALUES (" + string.Join(", ", placeholders.ToArray()) + ");";

            using (var cmd = new SQLiteCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CasID", casId);
                cmd.Parameters.AddWithValue("@CenterID",
                    SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", (object)SecurityContext.Username ?? DBNull.Value);
                foreach (var pair in values)
                    cmd.Parameters.AddWithValue("@" + pair.Key, pair.Value ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }

            using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con))
                return Convert.ToInt32((long)idCmd.ExecuteScalar());
        }

        private static void Update(SQLiteConnection con, string moduleTable, int moduleId, Dictionary<string, object> values)
        {
            var assignments = new List<string>();
            foreach (var pair in values)
                assignments.Add("[" + pair.Key + "] = @" + pair.Key);

            assignments.Add("UpdatedAt = datetime('now')");

            string sql = "UPDATE [" + moduleTable + "] SET " + string.Join(", ", assignments.ToArray()) +
                         " WHERE " + PrimaryKeyOf(moduleTable) + " = @Id;";

            using (var cmd = new SQLiteCommand(sql, con))
            {
                foreach (var pair in values)
                    cmd.Parameters.AddWithValue("@" + pair.Key, pair.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Id", moduleId);
                cmd.ExecuteNonQuery();
            }
        }

        // نوشتنِ همان مقادیر روی ستون‌های هم‌نامِ TblCase. ستونی که وجود
        // نداشته باشد بی‌صدا رد می‌شود (مدارا با اسکیمای قدیمی‌تر — همان
        // قاعدهٔ SyncApplier).
        private static void MirrorToCase(SQLiteConnection con, int casId, Dictionary<string, object> caseMirror)
        {
            HashSet<string> caseColumns = GetColumns(con, "TblCase");

            var assignments = new List<string>();
            var applicable = new List<KeyValuePair<string, object>>();

            foreach (var pair in caseMirror)
            {
                if (!caseColumns.Contains(pair.Key)) continue;
                assignments.Add("[" + pair.Key + "] = @" + pair.Key);
                applicable.Add(pair);
            }

            if (assignments.Count == 0) return;

            string sql = "UPDATE TblCase SET " + string.Join(", ", assignments.ToArray()) +
                         ", UpdatedAt = datetime('now') WHERE CasID = @CasID;";

            using (var cmd = new SQLiteCommand(sql, con))
            {
                foreach (var pair in applicable)
                    cmd.Parameters.AddWithValue("@" + pair.Key, pair.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CasID", casId);
                cmd.ExecuteNonQuery();
            }
        }

        private static HashSet<string> GetColumns(SQLiteConnection con, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SQLiteCommand("PRAGMA table_info([" + tableName + "]);", con))
            using (var dr = cmd.ExecuteReader())
            {
                while (dr.Read())
                    columns.Add(dr["name"].ToString());
            }
            return columns;
        }

        // صفِ همگام‌سازی — همان الگوی FrmCase/FrmDocs. خطا هرگز ذخیره را
        // نمی‌شکند (SyncOutboxService خودش هم داخلی catch دارد).
        private static void SyncOutboxCapture(string moduleTable, int moduleId, bool isNew)
        {
            try
            {
                CaseManagement.Sync.SyncOutboxService.Capture(moduleTable, moduleId,
                    isNew
                        ? CaseManagement.Sync.OfflineSyncInitializer.OperationCreate
                        : CaseManagement.Sync.OfflineSyncInitializer.OperationUpdate);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CaseModuleService.SyncOutboxCapture failed: " + ex.Message);
            }
        }
    }
}

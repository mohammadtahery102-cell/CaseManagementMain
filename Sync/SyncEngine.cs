using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // گزینه‌های اجرای همگام‌سازی.
    public sealed class SyncOptions
    {
        public bool TakeBackup { get; set; }
        public string BackupParentFolder { get; set; }
        public int NewCaseCenterId { get; set; }

        public SyncOptions()
        {
            TakeBackup = true; // پیش‌فرض: همیشه قبل از اعمال بکاپ کامل گرفته شود
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SyncEngine — مرحله ۷ Wizard. تغییرات انتخاب‌شده‌ی SyncPlan را اعمال می‌کند.
    //
    // تضمین‌ها (مطابق قوانین پروژه):
    //   • قبل از هر نوشتن، Backup کامل گرفته می‌شود (اختیاری، پیش‌فرض روشن).
    //   • کل عملیات در یک Transaction است → یا همه با هم موفق، یا Rollback کامل.
    //   • هیچ رکوردی حذف نمی‌شود؛ فقط New/Update.
    //   • فقط ستون‌هایی که واقعاً در جدول وجود دارند نوشته می‌شوند (مقاوم در
    //     برابر تفاوت schema بین نصب‌ها).
    //   • فقط فیلدهایی که کاربر انتخاب کرده (FieldChange.Selected) بروزرسانی می‌شوند.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class SyncEngine
    {
        private readonly DatabaseHelper _db;
        private Dictionary<string, HashSet<string>> _tableColumnsCache;

        public SyncEngine(DatabaseHelper db)
        {
            _db = db ?? new DatabaseHelper();
            _tableColumnsCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        }

        public SyncReport Apply(SyncPlan plan, SyncOptions options, IProgress<SyncProgress> progress)
        {
            var report = new SyncReport();
            options = options ?? new SyncOptions();
            if (plan == null) { report.ErrorMessage = "برنامه‌ی همگام‌سازی خالی است."; return report; }

            // ── مرحله بکاپ (قبل از هر نوشتن) ────────────────────────────────
            if (options.TakeBackup)
            {
                try
                {
                    string parent = string.IsNullOrWhiteSpace(options.BackupParentFolder)
                        ? ResolveBackupFolder()
                        : options.BackupParentFolder;
                    report.BackupPath = new BackupHelper().ExportBackup(parent);
                    report.Add("بکاپ کامل قبل از همگام‌سازی گرفته شد: " + report.BackupPath);
                }
                catch (Exception ex)
                {
                    // اگر بکاپ نشد، به‌هیچ‌وجه ادامه نمی‌دهیم (ایمنی داده مقدم است).
                    report.Success = false;
                    report.ErrorMessage = "بکاپ‌گیری قبل از همگام‌سازی ناموفق بود؛ عملیات لغو شد: " + ex.Message;
                    report.FinishedAt = DateTime.Now;
                    return report;
                }
            }

            int centerId = options.NewCaseCenterId > 0
                ? options.NewCaseCenterId
                : (SecurityContext.CurrentCenterId > 0 ? SecurityContext.CurrentCenterId : 1);

            var guardians = plan.Guardians.Where(g => g.Selected && g.HasApplicableWork).ToList();
            var members = plan.Members.Where(m => m.Selected && m.HasApplicableWork).ToList();
            int total = Math.Max(1, guardians.Count + members.Count);
            int done = 0;

            // نگاشت کد عمومی → CasID (موجود + تازه‌ساخته) برای اتصال اعضا.
            var codeToCasId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            using (SQLiteConnection con = _db.GetConnection())
            {
                con.Open();
                using (var tr = con.BeginTransaction())
                {
                    try
                    {
                        HashSet<string> caseCols = GetColumns(con, "TblCase");
                        HashSet<string> famCols = GetColumns(con, "TblFamily");

                        // ── سرپرستان ──────────────────────────────────────────
                        foreach (SyncRecord g in guardians)
                        {
                            if (g.Action == SyncAction.New)
                            {
                                int newId = InsertCase(con, tr, g, caseCols, centerId);
                                codeToCasId[g.PublicCode] = newId;
                                report.GuardiansInserted++;
                            }
                            else if (g.Action == SyncAction.Update)
                            {
                                UpdateRow(con, tr, "TblCase", "CasID", g.ExistingDbId, g.Changes, caseCols);
                                codeToCasId[g.PublicCode] = g.ExistingDbId;
                                report.GuardiansUpdated++;
                            }
                            done++;
                            Report(progress, "اعمال سرپرستان", done, total);
                        }

                        // اطمینان از این‌که کد خانواده‌های موجود هم در نگاشت هست
                        // (برای اعضایی که خانواده‌شان Update/Unchanged بوده).
                        foreach (SyncRecord m in members)
                        {
                            if (m.ParentCasId > 0 && !codeToCasId.ContainsKey(m.PublicCode))
                                codeToCasId[m.PublicCode] = m.ParentCasId;
                        }

                        // ── اعضای خانواده ─────────────────────────────────────
                        foreach (SyncRecord m in members)
                        {
                            int casId;
                            if (!codeToCasId.TryGetValue(m.PublicCode, out casId) || casId <= 0)
                            {
                                // خانواده‌اش ساخته/انتخاب نشده → از این عضو صرف‌نظر می‌شود.
                                report.Skipped++;
                                report.Add("عضو «" + m.Title + "» رد شد: خانواده با کد " + m.PublicCode + " موجود نیست.");
                                done++;
                                Report(progress, "اعمال اعضا", done, total);
                                continue;
                            }

                            if (m.Action == SyncAction.New)
                            {
                                InsertMember(con, tr, m, famCols, casId);
                                report.MembersInserted++;
                            }
                            else if (m.Action == SyncAction.Update)
                            {
                                UpdateRow(con, tr, "TblFamily", "FamID", m.ExistingDbId, m.Changes, famCols);
                                report.MembersUpdated++;
                            }
                            done++;
                            Report(progress, "اعمال اعضا", done, total);
                        }

                        tr.Commit();
                        report.Success = true;
                        report.Add(string.Format(
                            "همگام‌سازی موفق: {0} سرپرست جدید، {1} سرپرست بروزرسانی، {2} عضو جدید، {3} عضو بروزرسانی، {4} رد شده.",
                            report.GuardiansInserted, report.GuardiansUpdated,
                            report.MembersInserted, report.MembersUpdated, report.Skipped));
                    }
                    catch (Exception ex)
                    {
                        try { tr.Rollback(); } catch { }
                        report.Success = false;
                        report.RolledBack = true;
                        report.Errors++;
                        report.ErrorMessage = ex.Message;
                        report.Add("خطا در حین همگام‌سازی؛ همه‌ی تغییرات برگردانده شد (Rollback): " + ex.Message);
                    }
                }
            }

            report.FinishedAt = DateTime.Now;

            // ثبت یک رکورد خلاصه در audit (نه هر رکورد — برای کارایی). AuditLogger
            // خودش نام کاربر جاری و مرکزش را از SecurityContext ثبت می‌کند، پس
            // همگام‌سازی «به نام همان کاربر و با عملیات ویرایش» ثبت می‌شود.
            try
            {
                AuditLogger.Log(report.Success ? "ویرایش (همگام‌سازی HTML)" : "ویرایش (همگام‌سازی HTML — ناموفق)",
                    "TblCase", 0, "",
                    string.Format("توسط {0} در مرکز {1} — جدید سرپرست={2}، بروزرسانی سرپرست={3}، جدید عضو={4}، بروزرسانی عضو={5}",
                        SecurityContext.Username, SecurityContext.CurrentCenterName,
                        report.GuardiansInserted, report.GuardiansUpdated,
                        report.MembersInserted, report.MembersUpdated));
            }
            catch { }

            return report;
        }

        // ─── درج یک سرپرست جدید (فقط ستون‌های موجود در جدول) ─────────────────
        private int InsertCase(SQLiteConnection con, SQLiteTransaction tr,
            SyncRecord rec, HashSet<string> tableCols, int centerId)
        {
            var cols = new List<string>();
            var pars = new List<string>();
            var cmd = new SQLiteCommand { Connection = con, Transaction = tr };

            int p = 0;
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in rec.SourceValues)
            {
                if (!tableCols.Contains(kv.Key)) continue;
                written.Add(kv.Key);
                cols.Add("[" + kv.Key + "]");
                string pn = "@p" + (p++);
                pars.Add(pn);
                cmd.Parameters.AddWithValue(pn, (object)(kv.Value ?? "") );
            }

            // مقادیر پیش‌فرضِ فقط‌هنگام‌ثبت (اگر ستون موجود و قبلاً از فایل نیامده).
            foreach (var kv in rec.InsertOnlyValues)
            {
                if (!tableCols.Contains(kv.Key) || written.Contains(kv.Key)) continue;
                written.Add(kv.Key);
                cols.Add("[" + kv.Key + "]");
                string pn = "@p" + (p++);
                pars.Add(pn);
                cmd.Parameters.AddWithValue(pn, (object)(kv.Value ?? ""));
            }

            AddIfColumn(tableCols, cols, pars, cmd, "CenterID", centerId, ref p);
            AddNowIfColumn(tableCols, cols, pars, cmd, "CreatedAt", ref p);
            AddNowIfColumn(tableCols, cols, pars, cmd, "UpdatedAt", ref p);
            AddGuidIfColumn(tableCols, cols, pars, cmd, "GlobalID", ref p);

            cmd.CommandText = "INSERT INTO TblCase (" + string.Join(",", cols) + ") VALUES (" +
                              string.Join(",", pars) + ")";
            using (cmd)
            {
                cmd.ExecuteNonQuery();
                using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", con, tr))
                    return Convert.ToInt32((long)idCmd.ExecuteScalar());
            }
        }

        // ─── درج یک عضو خانواده جدید ─────────────────────────────────────────
        private void InsertMember(SQLiteConnection con, SQLiteTransaction tr,
            SyncRecord rec, HashSet<string> tableCols, int casId)
        {
            var cols = new List<string> { "[CasID]" };
            var pars = new List<string> { "@cas" };
            var cmd = new SQLiteCommand { Connection = con, Transaction = tr };
            cmd.Parameters.AddWithValue("@cas", casId);

            int p = 0;
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in rec.SourceValues)
            {
                if (!tableCols.Contains(kv.Key)) continue;
                if (string.Equals(kv.Key, "CasID", StringComparison.OrdinalIgnoreCase)) continue;
                written.Add(kv.Key);
                cols.Add("[" + kv.Key + "]");
                string pn = "@m" + (p++);
                pars.Add(pn);
                cmd.Parameters.AddWithValue(pn, (object)(kv.Value ?? ""));
            }

            // مقادیر پیش‌فرضِ فقط‌هنگام‌ثبت (مثل PhysicalStatus="سالم") — فقط اگر
            // ستون موجود باشد و از فایل نیامده باشد. در بروزرسانی هرگز اعمال نمی‌شوند.
            foreach (var kv in rec.InsertOnlyValues)
            {
                if (!tableCols.Contains(kv.Key) || written.Contains(kv.Key)) continue;
                if (string.Equals(kv.Key, "CasID", StringComparison.OrdinalIgnoreCase)) continue;
                written.Add(kv.Key);
                cols.Add("[" + kv.Key + "]");
                string pn = "@m" + (p++);
                pars.Add(pn);
                cmd.Parameters.AddWithValue(pn, (object)(kv.Value ?? ""));
            }

            cmd.CommandText = "INSERT INTO TblFamily (" + string.Join(",", cols) + ") VALUES (" +
                              string.Join(",", pars) + ")";
            using (cmd)
                cmd.ExecuteNonQuery();
        }

        // ─── بروزرسانی — فقط تغییرات انتخاب‌شده و فقط ستون‌های موجود ──────────
        private void UpdateRow(SQLiteConnection con, SQLiteTransaction tr,
            string table, string idColumn, int id, List<FieldChange> changes, HashSet<string> tableCols)
        {
            var selected = changes.Where(c => c.Selected && tableCols.Contains(c.FieldName)).ToList();
            if (selected.Count == 0) return;

            var sets = new List<string>();
            var cmd = new SQLiteCommand { Connection = con, Transaction = tr };
            for (int i = 0; i < selected.Count; i++)
            {
                sets.Add("[" + selected[i].FieldName + "] = @v" + i);
                cmd.Parameters.AddWithValue("@v" + i, (object)(selected[i].NewValue ?? ""));
            }
            if (tableCols.Contains("UpdatedAt"))
                sets.Add("[UpdatedAt] = datetime('now')");

            cmd.Parameters.AddWithValue("@id", id);
            cmd.CommandText = "UPDATE " + table + " SET " + string.Join(", ", sets) +
                              " WHERE [" + idColumn + "] = @id";
            using (cmd)
                cmd.ExecuteNonQuery();
        }

        // ─── کمکی‌ها ─────────────────────────────────────────────────────────
        private HashSet<string> GetColumns(SQLiteConnection con, string table)
        {
            HashSet<string> cols;
            if (_tableColumnsCache.TryGetValue(table, out cols)) return cols;

            cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + table + ")", con))
            using (var dr = cmd.ExecuteReader())
                while (dr.Read())
                    cols.Add(dr["name"].ToString());

            _tableColumnsCache[table] = cols;
            return cols;
        }

        private static void AddIfColumn(HashSet<string> tableCols, List<string> cols, List<string> pars,
            SQLiteCommand cmd, string col, object value, ref int p)
        {
            if (!tableCols.Contains(col)) return;
            cols.Add("[" + col + "]");
            string pn = "@p" + (p++);
            pars.Add(pn);
            cmd.Parameters.AddWithValue(pn, value);
        }

        private static void AddNowIfColumn(HashSet<string> tableCols, List<string> cols, List<string> pars,
            SQLiteCommand cmd, string col, ref int p)
        {
            if (!tableCols.Contains(col)) return;
            cols.Add("[" + col + "]");
            pars.Add("datetime('now')"); // مقدار ثابت SQL، نه پارامتر
        }

        private static void AddGuidIfColumn(HashSet<string> tableCols, List<string> cols, List<string> pars,
            SQLiteCommand cmd, string col, ref int p)
        {
            if (!tableCols.Contains(col)) return;
            cols.Add("[" + col + "]");
            string pn = "@p" + (p++);
            pars.Add(pn);
            cmd.Parameters.AddWithValue(pn, Guid.NewGuid().ToString("N"));
        }

        private string ResolveBackupFolder()
        {
            string configured = SettingsHelper.Get(SettingsHelper.BackupPath);
            if (!string.IsNullOrWhiteSpace(configured)) return configured;

            string baseDir = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "SyncBackups");
            System.IO.Directory.CreateDirectory(baseDir);
            return baseDir;
        }

        private static void Report(IProgress<SyncProgress> progress, string phase, int current, int total)
        {
            if (progress != null)
                progress.Report(new SyncProgress { Phase = phase, Current = current, Total = total });
        }
    }
}

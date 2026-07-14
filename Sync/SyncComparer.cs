using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text.RegularExpressions;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Sync
{
    // ─────────────────────────────────────────────────────────────────────────
    // SyncComparer — مرحله ۴ Wizard. رکوردهای تجزیه‌شده را با دیتابیس مقایسه و
    // یک SyncPlan (با تشخیص New/Update/Unchanged/Duplicate/Error و تغییرات
    // فیلدی) می‌سازد. کاملاً مستقل از منبع (Provider) است: فقط با نام ستون‌های
    // دیتابیس که در SourceValues آمده کار می‌کند.
    //
    // اصل ایمنی: هرگز حذف نمی‌کند و فقط فیلدهایی را که در فایل آمده مقایسه
    // می‌کند؛ ستون‌های دیگر دیتابیس هرگز لمس نمی‌شوند.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class SyncComparer
    {
        private readonly DatabaseHelper _db;

        public SyncComparer(DatabaseHelper db)
        {
            _db = db ?? new DatabaseHelper();
        }

        // فقط نام ستون‌های معتبر SQL اجازه دارند وارد کوئری شوند (دفاع در برابر تزریق).
        private static readonly Regex SafeColumn = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public SyncPlan BuildPlan(ParsedSyncData data, IProgress<SyncProgress> progress)
        {
            var plan = new SyncPlan();
            if (data == null) return plan;

            int centerFilter = SecurityContext.CenterFilterId;

            // ── سرپرستان ─────────────────────────────────────────────────────
            var guardianFields = CollectFields(data.Guardians);
            Dictionary<string, ExistingRow> existingCases = LoadExisting(
                "TblCase", "CasID", "Code", guardianFields, centerFilter);

            var codeToCasId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in existingCases)
                codeToCasId[kv.Key] = kv.Value.Id;

            var seenGuardianCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < data.Guardians.Count; i++)
            {
                SyncRecord rec = data.Guardians[i];
                ClassifyGuardian(rec, existingCases, seenGuardianCodes, newCodes);
                if ((i & 0x3FF) == 0) Report(progress, "مقایسه سرپرستان", i + 1, data.Guardians.Count);
            }
            plan.Guardians = data.Guardians;

            // ── اعضای خانواده ────────────────────────────────────────────────
            var memberFields = CollectFields(data.Members);

            // اعضای موجود فقط برای خانواده‌هایی که در دیتابیس هستند بارگذاری می‌شوند.
            var existingMembers = LoadExistingMembers(existingCases.Values.Select(v => v.Id), memberFields);

            for (int i = 0; i < data.Members.Count; i++)
            {
                SyncRecord rec = data.Members[i];
                ClassifyMember(rec, codeToCasId, newCodes, existingMembers);
                if ((i & 0x3FF) == 0) Report(progress, "مقایسه اعضا", i + 1, data.Members.Count);
            }
            plan.Members = data.Members;

            return plan;
        }

        // ─── طبقه‌بندی یک سرپرست ──────────────────────────────────────────────
        private void ClassifyGuardian(SyncRecord rec, Dictionary<string, ExistingRow> existing,
            HashSet<string> seen, HashSet<string> newCodes)
        {
            if (string.IsNullOrWhiteSpace(rec.PublicCode))
            {
                rec.Action = SyncAction.Error;
                rec.ErrorMessage = "کد عمومی خالی است.";
                return;
            }

            if (!seen.Add(rec.PublicCode))
            {
                rec.Action = SyncAction.Duplicate;
                rec.ErrorMessage = "کد عمومی در فایل تکراری است.";
                return;
            }

            ExistingRow row;
            if (!existing.TryGetValue(rec.PublicCode, out row))
            {
                rec.Action = SyncAction.New;
                newCodes.Add(rec.PublicCode);
                return;
            }

            rec.ExistingDbId = row.Id;
            rec.Changes = BuildChanges(rec.SourceValues, row.Values);
            rec.Action = rec.Changes.Count > 0 ? SyncAction.Update : SyncAction.Unchanged;
        }

        // ─── طبقه‌بندی یک عضو خانواده ────────────────────────────────────────
        private void ClassifyMember(SyncRecord rec, Dictionary<string, int> codeToCasId,
            HashSet<string> newCodes, Dictionary<int, Dictionary<string, ExistingRow>> existingMembers)
        {
            if (string.IsNullOrWhiteSpace(rec.PublicCode))
            {
                rec.Action = SyncAction.Error;
                rec.ErrorMessage = "کد عمومی خالی است.";
                return;
            }

            int casId;
            bool familyExists = codeToCasId.TryGetValue(rec.PublicCode, out casId);
            bool familyIsNew = newCodes.Contains(rec.PublicCode);

            if (!familyExists && !familyIsNew)
            {
                rec.Action = SyncAction.Error;
                rec.ErrorMessage = "خانواده‌ای با این کد عمومی وجود ندارد (نه در دیتابیس، نه در فایل سرپرستان).";
                return;
            }

            if (!familyExists)
            {
                // خانواده در همین همگام‌سازی ساخته می‌شود → عضو هم جدید است.
                rec.ParentCasId = 0;
                rec.Action = SyncAction.New;
                return;
            }

            rec.ParentCasId = casId;

            Dictionary<string, ExistingRow> familyMembers;
            if (!existingMembers.TryGetValue(casId, out familyMembers))
            {
                rec.Action = SyncAction.New;
                return;
            }

            ExistingRow member;
            if (!familyMembers.TryGetValue(rec.MemberKey ?? "", out member))
            {
                rec.Action = SyncAction.New;
                return;
            }

            rec.ExistingDbId = member.Id;
            rec.Changes = BuildChanges(rec.SourceValues, member.Values);
            rec.Action = rec.Changes.Count > 0 ? SyncAction.Update : SyncAction.Unchanged;
        }

        // ─── ساخت فهرست تغییرات فیلدی بین منبع و دیتابیس ─────────────────────
        private static List<FieldChange> BuildChanges(
            Dictionary<string, string> source, Dictionary<string, string> dbValues)
        {
            var changes = new List<FieldChange>();
            foreach (var kv in source)
            {
                // «Code» کلید هویت است، نه یک تغییر قابل‌بروزرسانی.
                if (string.Equals(kv.Key, "Code", StringComparison.OrdinalIgnoreCase)) continue;

                string newVal = (kv.Value ?? "").Trim();
                string oldVal = dbValues.ContainsKey(kv.Key) ? (dbValues[kv.Key] ?? "").Trim() : "";

                // آموزش — فقط وقتی فایل مقداری دارد و با دیتابیس فرق دارد، تغییر
                // ثبت می‌شود. اگر فایل خالی باشد، مقدار موجود دیتابیس پاک نمی‌شود
                // (رعایت «اطلاعات داخلی بازنویسی/حذف نشود»).
                if (newVal.Length == 0) continue;
                if (string.Equals(newVal, oldVal, StringComparison.Ordinal)) continue;

                changes.Add(new FieldChange
                {
                    FieldName = kv.Key,
                    DisplayName = UiTheme.TranslateHeader(kv.Key),
                    OldValue = oldVal,
                    NewValue = newVal,
                    Selected = true
                });
            }
            return changes;
        }

        // ─── بارگذاری رکوردهای موجود یک جدول به‌صورت map[کلید] = (Id, مقادیر) ─
        private Dictionary<string, ExistingRow> LoadExisting(
            string table, string idColumn, string keyColumn,
            List<string> fields, int centerFilter)
        {
            var map = new Dictionary<string, ExistingRow>(StringComparer.OrdinalIgnoreCase);
            List<string> cols = SanitizeColumns(fields);
            if (!cols.Contains(keyColumn)) cols.Add(keyColumn);

            string colList = string.Join(", ", cols.Select(c => "[" + c + "]"));
            // آموزش — رفع نوسان شمارش (۴۳۸↔۴۳۵ در آپلودهای یکسان): بدون ORDER BY،
            // SQLite ترتیب ردیف‌ها را تضمین نمی‌کند و بعد از insert/update ممکن است
            // ردیف‌ها را به ترتیب متفاوتی برگرداند. اگر کدِ تکراری در دیتابیس باشد،
            // «آخرین برنده» با ترتیبِ نامعین یعنی نتیجه‌ی نامعین. حالا ORDER BY صعودی
            // + «اولین برنده» → همیشه قطعی و تکرارپذیر.
            string sql = "SELECT [" + idColumn + "], " + colList +
                         " FROM " + table + " WHERE (@CID = 0 OR CenterID = @CID)" +
                         " ORDER BY [" + idColumn + "]";

            using (SQLiteConnection con = _db.GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CID", centerFilter);
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        string key = dr[keyColumn] == DBNull.Value ? "" : dr[keyColumn].ToString().Trim();
                        if (key.Length == 0) continue;
                        if (map.ContainsKey(key)) continue; // اولین برنده (قطعی)

                        var row = new ExistingRow { Id = Convert.ToInt32(dr[idColumn]) };
                        foreach (string c in cols)
                            row.Values[c] = dr[c] == DBNull.Value ? "" : dr[c].ToString();
                        map[key] = row;
                    }
                }
            }
            return map;
        }

        // اعضای موجود گروه‌بندی‌شده بر اساس CasID → (memberKey → row).
        private Dictionary<int, Dictionary<string, ExistingRow>> LoadExistingMembers(
            IEnumerable<int> caseIds, List<string> fields)
        {
            var result = new Dictionary<int, Dictionary<string, ExistingRow>>();
            var idList = caseIds.Distinct().ToList();
            if (idList.Count == 0) return result;

            List<string> cols = SanitizeColumns(fields);
            // ستون‌های لازم برای ساخت memberKey همیشه خوانده می‌شوند (شامل
            // BirthDate چون کلیدِ fallback حالا نام+نام‌پدر+تاریخ تولد است).
            foreach (string need in new[] { "MemberTazkiraNo", "MemberName", "MemberFatherName", "BirthDate" })
                if (!cols.Contains(need)) cols.Add(need);

            string colList = string.Join(", ", cols.Select(c => "[" + c + "]"));

            // برای پرهیز از IN بزرگ، همه‌ی اعضای مراکز مرتبط را می‌خوانیم و در حافظه فیلتر می‌کنیم.
            // ORDER BY FamID → ترتیب قطعی (رفع نوسان شمارش هنگام کلید تکراری عضو).
            var idSet = new HashSet<int>(idList);
            string sql = "SELECT FamID, CasID, " + colList + " FROM TblFamily ORDER BY FamID";

            using (SQLiteConnection con = _db.GetConnection())
            using (var cmd = new SQLiteCommand(sql, con))
            {
                con.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        int casId = Convert.ToInt32(dr["CasID"]);
                        if (!idSet.Contains(casId)) continue;

                        var vals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (string c in cols)
                            vals[c] = dr[c] == DBNull.Value ? "" : dr[c].ToString();

                        string memberKey = HtmlSyncProvider.ComputeMemberKey(vals);

                        Dictionary<string, ExistingRow> family;
                        if (!result.TryGetValue(casId, out family))
                        {
                            family = new Dictionary<string, ExistingRow>(StringComparer.Ordinal);
                            result[casId] = family;
                        }

                        var row = new ExistingRow { Id = Convert.ToInt32(dr["FamID"]) };
                        row.Values = vals;
                        if (!family.ContainsKey(memberKey))
                            family[memberKey] = row;
                    }
                }
            }
            return result;
        }

        private static List<string> CollectFields(IEnumerable<SyncRecord> records)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in records)
                foreach (var k in r.SourceValues.Keys)
                    set.Add(k);
            return set.ToList();
        }

        private static List<string> SanitizeColumns(IEnumerable<string> fields)
        {
            return fields.Where(f => SafeColumn.IsMatch(f)).Distinct().ToList();
        }

        private static void Report(IProgress<SyncProgress> progress, string phase, int current, int total)
        {
            if (progress != null)
                progress.Report(new SyncProgress { Phase = phase, Current = current, Total = total });
        }

        private sealed class ExistingRow
        {
            public int Id;
            public Dictionary<string, string> Values =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

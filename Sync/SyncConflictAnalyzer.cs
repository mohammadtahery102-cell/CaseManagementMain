using System;
using System.Collections.Generic;
using System.Data;
using CaseManagement.Enterprise;

namespace CaseManagement.Sync
{
    // ═════════════════════════════════════════════════════════════════════════
    // تحلیل‌گر تعارض — فاز ۴.
    //
    // سیاست پیش‌فرض: «ادغام در سطح فیلد».
    //   • فیلدی که فقط یک طرف عوضش کرده ⇒ همان طرف برنده است (ادغام خودکار).
    //   • فیلدی که هر دو طرف به مقدارِ متفاوت عوضش کرده‌اند ⇒ تعارضِ واقعی و
    //     نیازمند تصمیم مدیر.
    //
    // آموزش — چرا «مبنا» (نسخهٔ مشترکِ قبلی) لازم است: بدون آن نمی‌توان
    // «تغییر داده شد» را از «فقط فرق دارد» تشخیص داد. اگر تلفن محلی 0700 و
    // تلفن سرور 0700 باشد ولی آدرس فرق کند، مقایسهٔ دوتایی هر دو را «متفاوت»
    // نمی‌بیند — ولی اگر محلی تلفن را عوض کرده و سرور دست نزده، بدون مبنا
    // نمی‌فهمیم کدام‌یک تغییر کرده و ممکن است مقدار قدیمیِ سرور روی مقدار
    // تازهٔ کاربر بنشیند. مبنا از تاریخچهٔ نسخه‌های موجود (EntRecordVersion)
    // خوانده می‌شود — همان سرویسی که از قبل در برنامه هست، بدون تغییر.
    //
    // ⚠ اگر مبنا در دسترس نباشد (رکوردی که تاریخچه ندارد)، محافظه‌کارانه‌ترین
    // رفتار انتخاب می‌شود: هر فیلدی که دو طرف متفاوت دارند «تعارض» است و به
    // بازبینی می‌رود. هرگز حدس زده نمی‌شود.
    // ═════════════════════════════════════════════════════════════════════════
    public static class SyncConflictAnalyzer
    {
        public enum FieldOutcome
        {
            Same,        // هر دو طرف یکی هستند — کاری لازم نیست
            TakeLocal,   // فقط محلی تغییر کرده
            TakeRemote,  // فقط سرور تغییر کرده
            Contested    // هر دو تغییر کرده‌اند و متفاوت‌اند ⇒ تصمیم مدیر
        }

        public sealed class FieldDecision
        {
            public string Field = "";
            public string BaseValue = "";
            public string LocalValue = "";
            public string RemoteValue = "";
            public FieldOutcome Outcome;

            // آیا هر طرف اصلاً دربارهٔ این فیلد چیزی گفته است؟
            // ⚠ «نبودِ فیلد» با «مقدار خالی» یکی نیست — این تمایز حیاتی است
            // (توضیح در Decide).
            public bool InLocal;
            public bool InRemote;

            public string OutcomeText
            {
                get
                {
                    switch (Outcome)
                    {
                        case FieldOutcome.TakeLocal:  return "محلی";
                        case FieldOutcome.TakeRemote: return "سرور";
                        case FieldOutcome.Contested:  return "نیازمند تصمیم";
                        default:                      return "یکسان";
                    }
                }
            }
        }

        public sealed class ConflictAnalysis
        {
            public long   ConflictId;
            public string EntityName = "";
            public string GlobalId = "";
            public string ConflictType = "";
            public int    LocalVersion;
            public int    RemoteVersion;
            public bool   HasBaseline;

            public readonly List<FieldDecision> Fields = new List<FieldDecision>();

            public List<FieldDecision> Contested
            {
                get
                {
                    var list = new List<FieldDecision>();
                    foreach (FieldDecision field in Fields)
                        if (field.Outcome == FieldOutcome.Contested) list.Add(field);
                    return list;
                }
            }

            // حذف و کدِ تکراری هرگز خودکار حل نمی‌شوند — هر دو تصمیمِ انسانی
            // می‌خواهند (یکی رکورد را نابود می‌کند، دیگری دو پروندهٔ واقعی را
            // درگیر می‌کند).
            public bool IsDeleteConflict
            {
                get { return ConflictType == OfflineSyncInitializer.ConflictDeletedRemote; }
            }

            public bool IsDuplicateCode
            {
                get { return ConflictType == OfflineSyncInitializer.ConflictDuplicateCode; }
            }

            public bool CanAutoMerge
            {
                get
                {
                    if (IsDeleteConflict || IsDuplicateCode) return false;
                    if (!HasBaseline) return Contested.Count == 0 && Fields.Count > 0;
                    return Contested.Count == 0;
                }
            }

            // مقادیر نهاییِ ادغامِ خودکار (فقط فیلدهای غیرمتعارض).
            public Dictionary<string, string> MergedValues()
            {
                var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // فقط فیلدهایی که واقعاً باید عوض شوند وارد نتیجه می‌شوند؛
                // نوشتنِ دوبارهٔ مقادیرِ محلی روی خودشان بی‌فایده است و فقط
                // ریسکِ برخورد با قیدهای جدول را بالا می‌برد.
                foreach (FieldDecision field in Fields)
                    if (field.Outcome == FieldOutcome.TakeRemote && field.InRemote)
                        merged[field.Field] = field.RemoteValue;

                return merged;
            }
        }

        // ستون‌هایی که اصلاً وارد ادغام نمی‌شوند.
        private static readonly HashSet<string> Ignored =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GlobalID", "RowVersion", "LastModifiedAt", "LastModifiedBy", "SyncStatus",
                "CasID", "FamID", "DocID", "AssistanceID", "CreatedAt"
            };

        // ⚠ فیلدهای حساس: حتی اگر ظاهراً فقط یک طرف عوضشان کرده باشد، تفاوت
        // در آن‌ها هرگز خودکار اعمال نمی‌شود.
        //
        // آموزش — چرا: «وضعیت خدمت» تعیین می‌کند خانواده کمک می‌گیرد یا نه؛
        // «کد اختصاصی» نام پوشهٔ فایل‌ها و محتوای بارکد است؛ «بایگانی» پرونده
        // را از جریان کاری خارج می‌کند. اشتباه در این‌ها اثر واقعی روی زندگی
        // مددجو یا یکپارچگی فایل‌ها دارد، پس ارزشِ یک تأییدِ انسانی را دارند.
        private static readonly HashSet<string> Sensitive =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ServiceStatus", "Code", "FormNo", "IsArchived", "CenterID"
            };

        // ─────────────────────────────────────────────────────────────────────
        public static ConflictAnalysis Analyze(DataRow conflictRow)
        {
            var analysis = new ConflictAnalysis();
            if (conflictRow == null) return analysis;

            analysis.ConflictId   = GetLong(conflictRow, "ConflictID");
            analysis.EntityName   = GetString(conflictRow, "EntityName");
            analysis.GlobalId     = GetString(conflictRow, "EntityGlobalID");
            analysis.ConflictType = GetString(conflictRow, "ConflictType");
            analysis.LocalVersion  = GetInt(conflictRow, "LocalVersion");
            analysis.RemoteVersion = GetInt(conflictRow, "RemoteVersion");

            Dictionary<string, string> local  = SyncApplier.Deserialize(GetString(conflictRow, "LocalPayload"));
            Dictionary<string, string> remote = SyncApplier.Deserialize(GetString(conflictRow, "RemotePayload"));

            Dictionary<string, string> baseline =
                ReadBaseline(analysis.EntityName, local, analysis.GlobalId);
            analysis.HasBaseline = baseline != null;

            foreach (string field in AllKeys(local, remote))
            {
                if (Ignored.Contains(field)) continue;

                string localValue  = Value(local, field);
                string remoteValue = Value(remote, field);

                var decision = new FieldDecision
                {
                    Field = field,
                    LocalValue = localValue,
                    RemoteValue = remoteValue,
                    BaseValue = baseline == null ? "" : Value(baseline, field),
                    InLocal = local.ContainsKey(field),
                    InRemote = remote.ContainsKey(field)
                };

                decision.Outcome = Decide(decision, baseline != null);

                analysis.Fields.Add(decision);
            }

            return analysis;
        }

        private static FieldOutcome Decide(FieldDecision field, bool hasBaseline)
        {
            // ⚠ نکتهٔ کلیدی: «فیلد در بارِ داده نیست» یعنی آن طرف دربارهٔ این
            // فیلد *حرفی نزده*، نه اینکه خالی‌اش کرده. بارِ سرور می‌تواند فقط
            // شاملِ فیلدهای تغییرکرده باشد؛ اگر نبودن را «خالی» می‌خواندیم،
            // یک به‌روزرسانیِ جزئیِ سرور تمام ستون‌های دیگر را پاک می‌کرد —
            // یا (بهتر ولی باز هم غلط) همه را تعارض اعلام می‌کرد و ادغام
            // خودکار عملاً هرگز اتفاق نمی‌افتاد.
            if (!field.InRemote) return FieldOutcome.TakeLocal;
            if (!field.InLocal)  return FieldOutcome.TakeRemote;

            bool same = string.Equals(field.LocalValue, field.RemoteValue, StringComparison.Ordinal);
            if (same) return FieldOutcome.Same;

            // فیلد حساس و متفاوت ⇒ همیشه تصمیم انسانی.
            if (Sensitive.Contains(field.Field)) return FieldOutcome.Contested;

            // بدون مبنا نمی‌توان فهمید کدام طرف تغییر کرده ⇒ محافظه‌کاری.
            if (!hasBaseline) return FieldOutcome.Contested;

            bool localChanged  = !string.Equals(field.LocalValue,  field.BaseValue, StringComparison.Ordinal);
            bool remoteChanged = !string.Equals(field.RemoteValue, field.BaseValue, StringComparison.Ordinal);

            if (localChanged && remoteChanged) return FieldOutcome.Contested;
            if (localChanged)  return FieldOutcome.TakeLocal;
            if (remoteChanged) return FieldOutcome.TakeRemote;

            // هیچ‌کدام نسبت به مبنا تغییر نکرده ولی متفاوت‌اند (مبنا ناقص است)
            // ⇒ باز هم محافظه‌کاری.
            return FieldOutcome.Contested;
        }

        // ─────────────────────────────────────────────────────────────────────
        // مبنا — با دو منبع، به ترتیبِ دقت.
        //
        // ۱) SyncBaseline: «آخرین نسخه‌ای که واقعاً با سرور توافق شد» — یعنی
        //    باری که سرور پذیرفت یا باری که از سرور گرفتیم و اعمال کردیم.
        //    این تعریفِ *درستِ* مبنای ادغام سه‌طرفه است.
        //
        // ۲) تاریخچهٔ نسخه‌ها (رفتار قبلی): «نسخهٔ پیش از آخرین تغییرِ محلی».
        //    تقریبی است و فقط وقتی به کار می‌آید که هنوز هیچ تبادلِ موفقی
        //    برای این رکورد ثبت نشده باشد (مثلاً رکوردهای پیش از افزودنِ
        //    SyncBaseline، یا اولین همگام‌سازیِ یک نصبِ قدیمی).
        //
        // آموزش — چرا افزودنِ منبع ۱ اهمیت دارد: تقریبِ قدیمی فقط *آخرین*
        // ویرایشِ محلی را «تغییر» می‌دید. اگر کاربر از آخرین همگام‌سازی سه بار
        // ویرایش کرده بود، دو ویرایشِ اولش داخلِ خودِ مبنا جا می‌افتاد و
        // «دست‌نخورده» به‌نظر می‌رسید. حالا مبنا واقعاً همان نقطه‌ای است که دو
        // طرف رویش توافق داشتند، پس تشخیصِ «چه کسی چه چیزی را عوض کرد» درست
        // است و ادغامِ خودکار بسیار بیشتر — و درست‌تر — اتفاق می‌افتد.
        //
        // ⚠ ترتیب برعکس نشود: تاریخچهٔ محلی همیشه مقدار دارد و اگر اول امتحان
        // می‌شد، مبنای دقیق هرگز استفاده نمی‌شد.
        private static Dictionary<string, string> ReadBaseline(
            string entityName, Dictionary<string, string> local, string globalId)
        {
            Dictionary<string, string> agreed = SyncBaselineStore.Get(entityName, globalId);
            if (agreed != null && agreed.Count > 0) return agreed;

            try
            {
                int entityId = LocalIdOf(entityName, local);
                if (entityId <= 0) return null;

                DataTable versions = VersionService.GetVersions(entityName, entityId);
                if (versions.Rows.Count < 2) return null;   // تاریخچه‌ای برای مقایسه نیست

                // ردیف دوم = نسخهٔ پیش از آخرین تغییر (مرتب‌شده نزولی).
                int versionId = Convert.ToInt32(versions.Rows[1]["شناسه"]);

                DataTable snapshot = VersionService.GetSnapshotTable(versionId);
                if (snapshot.Rows.Count == 0) return null;

                var baseline = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (DataRow row in snapshot.Rows)
                    baseline[Convert.ToString(row["فیلد"])] = Convert.ToString(row["مقدار"]);

                return baseline;
            }
            catch { return null; }
        }

        private static int LocalIdOf(string entityName, Dictionary<string, string> values)
        {
            string key = null;
            foreach (string[] table in OfflineSyncInitializer.SyncedTables)
                if (string.Equals(table[0], entityName, StringComparison.OrdinalIgnoreCase))
                    key = table[1];

            if (key == null || !values.ContainsKey(key)) return 0;

            int id;
            return int.TryParse(values[key], out id) ? id : 0;
        }

        // ─── کمکی ────────────────────────────────────────────────────────────
        private static IEnumerable<string> AllKeys(Dictionary<string, string> a,
                                                    Dictionary<string, string> b)
        {
            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string key in a.Keys) if (seen.Add(key)) keys.Add(key);
            foreach (string key in b.Keys) if (seen.Add(key)) keys.Add(key);

            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        private static string Value(Dictionary<string, string> values, string key)
        {
            string result;
            return values != null && values.TryGetValue(key, out result) ? (result ?? "") : "";
        }

        private static string GetString(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return "";
            return Convert.ToString(row[column]);
        }

        private static int GetInt(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return 0;
            try { return Convert.ToInt32(row[column]); } catch { return 0; }
        }

        private static long GetLong(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return 0;
            try { return Convert.ToInt64(row[column]); } catch { return 0; }
        }
    }
}

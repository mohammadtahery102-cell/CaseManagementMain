using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    // ═════════════════════════════════════════════════════════════════════════
    // تشخیصِ پرونده‌های تکراری — کاملاً فقط‌خواندنی.
    //
    // این کلاس هرگز چیزی نمی‌نویسد یا حذف نمی‌کند؛ فقط جفت‌های مشکوک را
    // پیدا و درصدِ شباهت را گزارش می‌کند. تصمیمِ نهایی با خودِ کاربر است.
    //
    // چرا این‌طور طراحی شد (مسئله‌ی مقیاس):
    // مقایسه‌ی همه با همه برای ۱٬۶۶۱ پرونده یعنی ~۱٫۴ میلیون مقایسه که
    // قابل‌قبول است، ولی برای ۱۰۰٬۰۰۰ پرونده به ۵ میلیارد مقایسه می‌رسد و
    // عملاً هرگز تمام نمی‌شود. پس دو مرحله‌ای کار می‌کنیم:
    //   ۱) کلیدهای قطعی (تذکره، تلفن، کد) با گروه‌بندیِ هش — از مرتبه‌ی n
    //   ۲) شباهتِ نام فقط داخلِ «بلوک»های کوچک (هم‌آغاز) مقایسه می‌شود،
    //      نه بین همه‌ی پرونده‌ها.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class DuplicateDetector
    {
        private readonly DatabaseHelper _db;

        public DuplicateDetector(DatabaseHelper db)
        {
            _db = db ?? new DatabaseHelper();
        }

        // آستانه‌ی پیش‌فرضِ شباهتِ نام برای گزارش شدن.
        public int NameSimilarityThreshold { get; set; }

        public DuplicateDetector() : this(null) { }

        // ─── مدلِ یک پرونده در حافظه ────────────────────────────────────────
        private sealed class CaseRow
        {
            public int CasId;
            public string Code;
            public string FormNo;
            public string Name;
            public string FatherName;
            public string Tazkira;
            public string Phone;
            public string Address;
            public int CenterId;

            // نسخه‌های نرمال‌شده (یک‌بار حساب می‌شوند، نه در هر مقایسه)
            public string NName;
            public string NFather;
            public string NTazkira;
            public string NPhone;
            public string NAddress;
        }

        public List<DuplicateMatch> Detect(IProgress<int> progress,
                                           System.Threading.CancellationToken cancel)
        {
            if (NameSimilarityThreshold <= 0) NameSimilarityThreshold = 85;

            List<CaseRow> rows = Load();
            var matches = new Dictionary<string, DuplicateMatch>(StringComparer.Ordinal);

            // ── مرحله ۱: کلیدهای قطعی ──
            GroupExact(rows, r => r.NTazkira, "شماره تذکره", 100, matches, cancel);
            GroupExact(rows, r => r.Code, "کد اختصاصی", 100, matches, cancel);
            GroupExact(rows, r => r.NPhone, "شماره تماس", 90, matches, cancel);

            if (progress != null) progress.Report(40);

            // ── مرحله ۲: شباهتِ نام، فقط داخلِ بلوک‌ها ──
            MatchNamesWithinBlocks(rows, matches, progress, cancel);

            RefinePhoneOnlyMatches(rows, matches);

            if (progress != null) progress.Report(100);

            return matches.Values
                .OrderByDescending(m => m.SimilarityPercent)
                .ThenBy(m => m.CodeA, StringComparer.Ordinal)
                .ToList();
        }

        // ─── خواندن از دیتابیس (فقط SELECT) ─────────────────────────────────
        private List<CaseRow> Load()
        {
            var list = new List<CaseRow>();
            int centerFilter = SecurityContext.CenterFilterId;

            using (SQLiteConnection con = _db.GetConnection())
            using (var cmd = new SQLiteCommand(@"
SELECT CasID, COALESCE(Code,'') AS Code, COALESCE(FormNo,'') AS FormNo,
       COALESCE(HeadFullName,'') AS Nm, COALESCE(HeadFatherName,'') AS Fa,
       COALESCE(HeadTazkiraNo,'') AS Tz, COALESCE(Phone,'') AS Ph,
       COALESCE(HeadCurrentResidence,'') AS Ad, COALESCE(CenterID,0) AS Cid
FROM TblCase
WHERE (@CID = 0 OR CenterID = @CID)
  AND IsArchived = 0", con))
            {
                cmd.Parameters.AddWithValue("@CID", centerFilter);
                con.Open();

                using (SQLiteDataReader rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var r = new CaseRow
                        {
                            CasId = Convert.ToInt32(rd["CasID"]),
                            Code = Convert.ToString(rd["Code"]).Trim(),
                            FormNo = Convert.ToString(rd["FormNo"]).Trim(),
                            Name = Convert.ToString(rd["Nm"]).Trim(),
                            FatherName = Convert.ToString(rd["Fa"]).Trim(),
                            Tazkira = Convert.ToString(rd["Tz"]).Trim(),
                            Phone = Convert.ToString(rd["Ph"]).Trim(),
                            Address = Convert.ToString(rd["Ad"]).Trim(),
                            CenterId = Convert.ToInt32(rd["Cid"])
                        };

                        r.NName = Normalize(r.Name);
                        r.NFather = Normalize(r.FatherName);
                        r.NTazkira = NormalizeIdentifier(r.Tazkira);
                        r.NPhone = NormalizeIdentifier(r.Phone);
                        r.NAddress = Normalize(r.Address);

                        list.Add(r);
                    }
                }
            }
            return list;
        }

        // ─── گروه‌بندی روی یک کلیدِ قطعی ────────────────────────────────────
        private void GroupExact(List<CaseRow> rows, Func<CaseRow, string> keySelector,
                                string fieldLabel, int score,
                                Dictionary<string, DuplicateMatch> matches,
                                System.Threading.CancellationToken cancel)
        {
            var buckets = new Dictionary<string, List<CaseRow>>(StringComparer.Ordinal);

            foreach (CaseRow r in rows)
            {
                string key = keySelector(r);
                if (string.IsNullOrWhiteSpace(key) || key.Length < 3) continue;

                List<CaseRow> bucket;
                if (!buckets.TryGetValue(key, out bucket))
                {
                    bucket = new List<CaseRow>();
                    buckets[key] = bucket;
                }
                bucket.Add(r);
            }

            foreach (var kv in buckets)
            {
                cancel.ThrowIfCancellationRequested();
                if (kv.Value.Count < 2) continue;

                // همه‌ی جفت‌های داخلِ این گروه
                for (int i = 0; i < kv.Value.Count; i++)
                    for (int j = i + 1; j < kv.Value.Count; j++)
                        Merge(matches, kv.Value[i], kv.Value[j], score, fieldLabel, kv.Key);
            }
        }

        // ─── شباهتِ نام داخلِ بلوک‌های کوچک ─────────────────────────────────
        private void MatchNamesWithinBlocks(List<CaseRow> rows,
                                            Dictionary<string, DuplicateMatch> matches,
                                            IProgress<int> progress,
                                            System.Threading.CancellationToken cancel)
        {
            // بلوک‌بندی: پرونده‌هایی که نامشان با همان دو نویسه شروع می‌شود.
            // این کار تعداد مقایسه‌ها را از میلیاردها به ده‌ها هزار می‌رساند و
            // در عمل هیچ جفتِ واقعاً شبیهی را از دست نمی‌دهد، چون نام‌های
            // تکراری تقریباً همیشه هم‌آغازند.
            var blocks = new Dictionary<string, List<CaseRow>>(StringComparer.Ordinal);

            foreach (CaseRow r in rows)
            {
                if (string.IsNullOrWhiteSpace(r.NName) || r.NName.Length < 2) continue;

                string key = r.NName.Substring(0, 2);
                List<CaseRow> block;
                if (!blocks.TryGetValue(key, out block))
                {
                    block = new List<CaseRow>();
                    blocks[key] = block;
                }
                block.Add(r);
            }

            int done = 0;
            foreach (var kv in blocks)
            {
                cancel.ThrowIfCancellationRequested();

                List<CaseRow> block = kv.Value;
                for (int i = 0; i < block.Count; i++)
                {
                    for (int j = i + 1; j < block.Count; j++)
                    {
                        int nameScore = Similarity(block[i].NName, block[j].NName);
                        if (nameScore < NameSimilarityThreshold) continue;

                        // نامِ پدر و آدرس، اطمینان را بالا یا پایین می‌برند.
                        int fatherScore = Similarity(block[i].NFather, block[j].NFather);
                        int addressScore = Similarity(block[i].NAddress, block[j].NAddress);

                        var fields = new List<string> { "نام" };
                        int combined = nameScore;

                        if (fatherScore >= NameSimilarityThreshold)
                        {
                            fields.Add("نام پدر");
                            combined = (nameScore + fatherScore) / 2 + 5;
                        }
                        else if (!string.IsNullOrWhiteSpace(block[i].NFather) &&
                                 !string.IsNullOrWhiteSpace(block[j].NFather))
                        {
                            // نامِ پدر متفاوت است → احتمالاً دو نفرِ هم‌نام‌اند.
                            combined -= 20;
                        }

                        if (addressScore >= NameSimilarityThreshold &&
                            !string.IsNullOrWhiteSpace(block[i].NAddress))
                        {
                            fields.Add("آدرس");
                            combined += 5;
                        }

                        // آموزش — مهم‌ترین قاعده‌ی دقت، که از روی داده‌ی واقعی
                        // به‌دست آمد: اگر هر دو پرونده شماره تذکره دارند و آن دو
                        // شماره **متفاوت‌اند**، این قوی‌ترین نشانه است که دو نفرِ
                        // جدا هستند، نه یک پرونده‌ی تکراری.
                        //
                        // نمونه‌ی واقعی از همین دیتابیس: سه پرونده با نام «فاطمه»
                        // و نام پدر «قربان» ولی سه تذکره‌ی متفاوت — یعنی سه نفرِ
                        // مختلف با نامِ رایج. بدون این قاعده، هر سه «۱۰۰٪ قطعی»
                        // گزارش می‌شدند و فهرست پر از هشدارِ نادرست می‌شد.
                        bool bothHaveId = block[i].NTazkira.Length >= 4 && block[j].NTazkira.Length >= 4;
                        bool idsDiffer = bothHaveId &&
                            !string.Equals(block[i].NTazkira, block[j].NTazkira, StringComparison.Ordinal);

                        if (idsDiffer) combined -= 35;

                        // «قطعی» فقط برای مطابقتِ شناسه‌ای است. شباهتِ اسمی هرچقدر
                        // هم بالا باشد، بدونِ تأییدِ تذکره/تلفن به ۱۰۰ نمی‌رسد.
                        if (combined > 95) combined = 95;

                        combined = Math.Max(0, Math.Min(100, combined));
                        if (combined < NameSimilarityThreshold) continue;

                        Merge(matches, block[i], block[j], combined,
                              string.Join(" + ", fields.ToArray()), block[i].Name);
                    }
                }

                done++;
                if (progress != null && blocks.Count > 0)
                    progress.Report(40 + (int)(55.0 * done / blocks.Count));
            }
        }

        // ─── پالایشِ جفت‌هایی که فقط تلفنشان یکی است ─────────────────────────
        // آموزش — این قاعده هم از داده‌ی واقعی درآمد: در بسیاری از خانواده‌ها
        // یک شماره تلفن بینِ چند پرونده مشترک است (مثلاً «زرلشت» و «قندی گل»
        // با یک شماره). این‌ها «هم‌خانوار» هستند، نه پرونده‌ی تکراری. اگر
        // ۹۰٪ گزارش شوند، فهرست پر از هشدارِ نادرست می‌شود.
        // پس وقتی تنها دلیلِ مطابقت تلفن است، امتیاز بر اساس شباهتِ نام تنظیم
        // می‌شود و برچسبش هم صادقانه عوض می‌گردد.
        private void RefinePhoneOnlyMatches(List<CaseRow> rows,
                                            Dictionary<string, DuplicateMatch> matches)
        {
            var byId = rows.ToDictionary(r => r.CasId);

            foreach (DuplicateMatch m in matches.Values.ToList())
            {
                bool phoneOnly = m.MatchedFields.Count == 1 &&
                                 m.MatchedFields[0] == "شماره تماس";
                if (!phoneOnly) continue;

                CaseRow a, b;
                if (!byId.TryGetValue(m.CasIdA, out a) || !byId.TryGetValue(m.CasIdB, out b)) continue;

                int nameScore = Similarity(a.NName, b.NName);

                if (nameScore >= NameSimilarityThreshold)
                {
                    // تلفنِ یکسان + نامِ نزدیک → واقعاً مشکوک به تکراری
                    m.SimilarityPercent = Math.Min(95, 90 + (nameScore - NameSimilarityThreshold) / 3);
                    if (!m.MatchedFields.Contains("نام")) m.MatchedFields.Add("نام");
                }
                else
                {
                    // تلفنِ یکسان ولی نامِ متفاوت → به احتمال زیاد یک خانواده‌اند
                    m.SimilarityPercent = 55;
                    m.SameHouseholdOnly = true;
                }
            }

            // مواردی که فقط هم‌خانوارند از فهرستِ «تکراری» بیرون نمی‌روند، ولی
            // با امتیازِ پایین و برچسبِ روشن می‌آیند تا کاربر اشتباه نگیرد.
        }

        // یک جفت را ثبت یا تقویت می‌کند (اگر از چند راه تکراری تشخیص داده شود).
        private static void Merge(Dictionary<string, DuplicateMatch> matches,
                                  CaseRow a, CaseRow b, int score, string field, string value)
        {
            if (a.CasId == b.CasId) return;

            int lo = Math.Min(a.CasId, b.CasId);
            int hi = Math.Max(a.CasId, b.CasId);
            string key = lo + "|" + hi;

            DuplicateMatch existing;
            if (matches.TryGetValue(key, out existing))
            {
                if (!existing.MatchedFields.Contains(field)) existing.MatchedFields.Add(field);
                if (score > existing.SimilarityPercent) existing.SimilarityPercent = score;
                return;
            }

            CaseRow first = a.CasId == lo ? a : b;
            CaseRow second = a.CasId == lo ? b : a;

            var match = new DuplicateMatch
            {
                CasIdA = first.CasId,
                CasIdB = second.CasId,
                CodeA = first.Code,
                CodeB = second.Code,
                FormNoA = first.FormNo,
                FormNoB = second.FormNo,
                NameA = first.Name,
                NameB = second.Name,
                FatherA = first.FatherName,
                FatherB = second.FatherName,
                TazkiraA = first.Tazkira,
                TazkiraB = second.Tazkira,
                PhoneA = first.Phone,
                PhoneB = second.Phone,
                SimilarityPercent = score,
                MatchedValue = value
            };
            match.MatchedFields.Add(field);

            matches[key] = match;
        }

        // ─── نرمال‌سازی ─────────────────────────────────────────────────────
        // آموزش — بدون این کار، «محمدیوسف» و «محمد یوسف» و «محمّد یوسف» سه
        // چیزِ متفاوت شمرده می‌شوند. همچنین در متن‌های افغانستان هر دو شکلِ
        // عربی و فارسیِ «ی» و «ک» رایج است و باید یکی شوند.
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                char ch = c;

                switch (ch)
                {
                    case 'ي': ch = 'ی'; break;   // ی عربی
                    case 'ك': ch = 'ک'; break;   // ک عربی
                    case 'ة': ch = 'ه'; break;
                    case 'أ':
                    case 'إ':
                    case 'آ': ch = 'ا'; break;
                    case '‌': continue;      // نیم‌فاصله
                }

                // اعرابِ عربی حذف می‌شود
                if (ch >= 'ً' && ch <= 'ْ') continue;

                // ارقامِ فارسی/عربی به لاتین
                if (ch >= '۰' && ch <= '۹') ch = (char)('0' + (ch - '۰'));
                else if (ch >= '٠' && ch <= '٩') ch = (char)('0' + (ch - '٠'));

                if (char.IsWhiteSpace(ch)) { sb.Append(' '); continue; }
                if (char.IsPunctuation(ch)) continue;

                sb.Append(char.ToLowerInvariant(ch));
            }

            // فاصله‌های پشت‌سرهم یکی می‌شوند
            string result = sb.ToString();
            while (result.IndexOf("  ", StringComparison.Ordinal) >= 0)
                result = result.Replace("  ", " ");

            return result.Trim();
        }

        // برای تذکره و تلفن فقط رقم‌ها اهمیت دارند.
        public static string NormalizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            string normalized = Normalize(value);
            var sb = new StringBuilder(normalized.Length);
            foreach (char c in normalized)
                if (char.IsDigit(c)) sb.Append(c);

            return sb.ToString();
        }

        // ─── شباهتِ دو رشته (۰ تا ۱۰۰) ─────────────────────────────────────
        public static int Similarity(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0;
            if (string.Equals(a, b, StringComparison.Ordinal)) return 100;

            int distance = Levenshtein(a, b);
            int longest = Math.Max(a.Length, b.Length);
            if (longest == 0) return 0;

            return (int)Math.Round(100.0 * (longest - distance) / longest);
        }

        // فاصله‌ی ویرایشی با دو سطر (به‌جای ماتریسِ کامل) — حافظه‌ی کمتر و
        // سرعتِ بیشتر، که در مقایسه‌های انبوه اهمیت دارد.
        private static int Levenshtein(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            int[] previous = new int[b.Length + 1];
            int[] current = new int[b.Length + 1];

            for (int j = 0; j <= b.Length; j++) previous[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(
                        Math.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + cost);
                }

                int[] swap = previous; previous = current; current = swap;
            }

            return previous[b.Length];
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // یک جفتِ مشکوک به تکراری بودن.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class DuplicateMatch
    {
        public int CasIdA { get; set; }
        public int CasIdB { get; set; }

        public string CodeA { get; set; }
        public string CodeB { get; set; }
        public string FormNoA { get; set; }
        public string FormNoB { get; set; }

        public string NameA { get; set; }
        public string NameB { get; set; }
        public string FatherA { get; set; }
        public string FatherB { get; set; }
        public string TazkiraA { get; set; }
        public string TazkiraB { get; set; }
        public string PhoneA { get; set; }
        public string PhoneB { get; set; }

        public int SimilarityPercent { get; set; }
        public string MatchedValue { get; set; }
        public List<string> MatchedFields { get; set; }

        // فقط شماره تلفن مشترک است و نام‌ها فرق دارند → به احتمال زیاد یک
        // خانواده‌اند، نه پرونده‌ی تکراری.
        public bool SameHouseholdOnly { get; set; }

        public DuplicateMatch()
        {
            MatchedFields = new List<string>();
        }

        public string MatchedFieldsText
        {
            get { return string.Join(" ، ", MatchedFields.ToArray()); }
        }

        // سطحِ اطمینان — برای رنگ‌آمیزی و اولویت‌دهی.
        public string ConfidenceText
        {
            get
            {
                if (SameHouseholdOnly) return "احتمالاً یک خانواده";
                if (SimilarityPercent >= 100) return "قطعی";
                if (SimilarityPercent >= 92) return "بسیار محتمل";
                if (SimilarityPercent >= 85) return "محتمل";
                return "احتمالی";
            }
        }
    }
}

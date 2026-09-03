using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    // ─────────────────────────────────────────────────────────────────────────
    // AccRepair — موتور «ابزار اصلاح داده‌های تاریخی حسابداری».
    //
    // آموزش — مرز این کلاس را دقیق بدانید:
    //
    //   • این کلاس هیچ محاسبه‌ی حسابداری‌ای انجام نمی‌دهد و هیچ منطق تجاری‌ای
    //     را تغییر نمی‌دهد. فقط داده‌ی *تاریخی معیوب* را اصلاح می‌کند —
    //     رکوردهایی که پیش از افزوده‌شدن اعتبارسنجی‌ها ثبت شده‌اند.
    //
    //   • Detect() کاملاً فقط‌خواندنی است.
    //
    //   • Apply() هرگز خودبه‌خود صدا زده نمی‌شود. فرم باید آن را برای *یک*
    //     مورد، پس از تأیید صریح کاربر و با یک «دلیل» اجباری، فراخوانی کند.
    //     هیچ متد «اصلاح همه» عمداً وجود ندارد؛ چون یک اصلاح دسته‌جمعیِ اشتباه
    //     روی داده‌ی مالی، یک خطا را به فاجعه تبدیل می‌کند.
    //
    //   • هر اصلاح داخل یک تراکنش پایگاه‌داده انجام و در AccAudit با مقدار
    //     قبلی، مقدار جدید و دلیل ثبت می‌شود.
    //
    //   • همه‌ی دستورهای UPDATE یک «نگهبان خوش‌بینانه» دارند: شرط WHERE شامل
    //     مقدار قبلی است. اگر بین لحظه‌ی بررسی و لحظه‌ی اعمال، کاربر دیگری آن
    //     رکورد را عوض کرده باشد، هیچ ردیفی به‌روز نمی‌شود و خطا داده می‌شود
    //     به‌جای آن‌که تغییر دیگری بی‌صدا بازنویسی شود.
    // ─────────────────────────────────────────────────────────────────────────
    public class AccRepair
    {
        public const string KindAssignPeriod = "AssignPeriod";
        public const string KindAssignCenter = "AssignCenter";
        public const string KindFixDate = "FixDate";
        public const string KindVoidDuplicate = "VoidDuplicate";

        public class RepairItem
        {
            public string Kind;
            public string Category;      // عنوان فارسی دسته
            public string Table;
            public string IdColumn;
            public int RecordId;
            public string Problem;       // شرح اشکال
            public string CurrentValue;  // مقدار فعلی (برای نمایش)
            public string Suggestion;    // اصلاح پیشنهادی (برای نمایش)
            public string Basis;         // پیشنهاد از کجا آمده
            public double Amount;

            public bool HasSuggestion;   // اگر false، کاربر باید خودش انتخاب کند

            // بارِ داده‌ی اصلاح
            public int? SuggestedPeriodId;
            public int? SuggestedCenterId;
            public string DateColumn;
            public string SuggestedDate;
        }

        private readonly AccountingRepo _repo;
        private readonly DatabaseHelper _db = new DatabaseHelper();

        public AccRepair(AccountingRepo repo) { _repo = repo; }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        private static double D(object v)
        {
            return v == null || v == DBNull.Value ? 0 : Money.Round(Convert.ToDouble(v));
        }

        private static string S(object v)
        {
            return v == null || v == DBNull.Value ? "" : v.ToString();
        }

        // جدول‌های مالی و ستون‌های متناظرشان — در یک جا تا تکرار نشود.
        private class TableSpec
        {
            public string Table, IdCol, AmountCol, Label, DateCol;
            public TableSpec(string t, string i, string a, string l, string d)
            { Table = t; IdCol = i; AmountCol = a; Label = l; DateCol = d; }
        }

        private static readonly TableSpec[] Specs =
        {
            new TableSpec("AccTransaction", "TxnID",     "Amount",    "تراکنش",     "TxnDate"),
            new TableSpec("AccStipend",     "StipendID", "TotalPaid", "ردیف شهریه", null),
            new TableSpec("AccSalary",      "SalaryID",  "Amount",    "ردیف حقوق",  null),
            new TableSpec("AccExpenseItem", "ItemID",    "Price",     "قلم هزینه",  "ItemDate")
        };

        // ═══════════════════════════════════════════════════════════════════
        // تشخیص — فقط‌خواندنی، هیچ نوشتنی انجام نمی‌شود
        // ═══════════════════════════════════════════════════════════════════
        public List<RepairItem> Detect()
        {
            var items = new List<RepairItem>();

            DetectOrphanPeriod(items);
            DetectMissingCenter(items);
            DetectMalformedDates(items);
            DetectDuplicates(items);

            return items;
        }

        // ───────────────────────────────────────────────────────────────────
        // ۱) رکوردهای بدون دوره مالی
        //
        // پیشنهاد چگونه ساخته می‌شود (به ترتیب اولویت):
        //   الف) اگر رکورد تاریخ معتبر دارد → دوره‌ای که آن تاریخ داخل بازه‌اش است.
        //   ب)  وگرنه اگر مرکز رکورد مشخص است و آن مرکز فقط یک دوره دارد → همان.
        //   ج)  وگرنه هیچ پیشنهادی داده نمی‌شود و انتخاب کاملاً با حسابدار است.
        //
        // هرگز حدس نمی‌زنیم؛ اگر مبنای روشنی نباشد، پیشنهاد خالی می‌ماند.
        // ───────────────────────────────────────────────────────────────────
        private void DetectOrphanPeriod(List<RepairItem> items)
        {
            foreach (TableSpec sp in Specs)
            {
                string dateSel = sp.DateCol == null ? "NULL" : sp.DateCol;

                DataTable dt = _db.Query(
                    "SELECT " + sp.IdCol + " AS Id, " + sp.AmountCol + " AS Amt, CenterID, " +
                    dateSel + " AS Dt FROM " + sp.Table +
                    " WHERE PeriodID IS NULL AND COALESCE(IsReversed,0)=0 ORDER BY " + sp.IdCol);

                foreach (DataRow r in dt.Rows)
                {
                    var item = new RepairItem
                    {
                        Kind = KindAssignPeriod,
                        Category = "رکورد بدون دوره مالی",
                        Table = sp.Table,
                        IdColumn = sp.IdCol,
                        RecordId = Convert.ToInt32(r["Id"]),
                        Amount = D(r["Amt"]),
                        Problem = sp.Label + " شماره " + r["Id"] + " به هیچ دوره مالی وصل نیست — " +
                                  "در گزارش‌های دوره‌ای شمرده نمی‌شود اما در مانده صندوق هست.",
                        CurrentValue = "بدون دوره"
                    };

                    int? centerId = r["CenterID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["CenterID"]);
                    string date = S(r["Dt"]);

                    int? suggested = SuggestPeriodByDate(date, centerId);
                    if (suggested.HasValue)
                        item.Basis = "تاریخ سند (" + date + ") داخل بازه‌ی این دوره است.";
                    else
                    {
                        suggested = SuggestSinglePeriodOfCenter(centerId);
                        if (suggested.HasValue)
                            item.Basis = "این مرکز فقط یک دوره مالی دارد.";
                    }

                    if (suggested.HasValue)
                    {
                        item.HasSuggestion = true;
                        item.SuggestedPeriodId = suggested;
                        item.Suggestion = _repo.GetPeriodTitle(suggested.Value);
                    }
                    else
                    {
                        item.Suggestion = "— مبنای مطمئنی برای پیشنهاد نیست؛ دوره را خودتان انتخاب کنید —";
                        item.Basis = "نه تاریخ معتبری وجود دارد و نه مرکز رکورد فقط یک دوره دارد.";
                    }

                    items.Add(item);
                }
            }
        }

        // دوره‌ای که تاریخ داده‌شده داخل بازه‌ی آن است. فقط وقتی جواب می‌دهد که
        // هم تاریخ و هم بازه‌ی دوره قالب استاندارد yyyy/MM/dd داشته باشند.
        private int? SuggestPeriodByDate(string date, int? centerId)
        {
            if (!IsStandardDate(date)) return null;

            DataTable dt = _db.Query(@"
SELECT PeriodID FROM AccPeriod
WHERE StartDate GLOB '[0-9][0-9][0-9][0-9]/[0-9][0-9]/[0-9][0-9]'
  AND EndDate   GLOB '[0-9][0-9][0-9][0-9]/[0-9][0-9]/[0-9][0-9]'
  AND @d BETWEEN StartDate AND EndDate
  AND (@cid IS NULL OR CenterID IS NULL OR CenterID = @cid)",
                P("@d", date), P("@cid", (object)centerId ?? DBNull.Value));

            // فقط اگر دقیقاً یک دوره منطبق باشد پیشنهاد می‌دهیم؛ چند دوره‌ی
            // هم‌پوشان یعنی ابهام، و در ابهام حدس نمی‌زنیم.
            return dt.Rows.Count == 1 ? (int?)Convert.ToInt32(dt.Rows[0]["PeriodID"]) : null;
        }

        private int? SuggestSinglePeriodOfCenter(int? centerId)
        {
            if (!centerId.HasValue) return null;

            DataTable dt = _db.Query(
                "SELECT PeriodID FROM AccPeriod WHERE CenterID = @cid", P("@cid", centerId.Value));

            return dt.Rows.Count == 1 ? (int?)Convert.ToInt32(dt.Rows[0]["PeriodID"]) : null;
        }

        // ───────────────────────────────────────────────────────────────────
        // ۲) رکوردهای بدون مرکز (CenterID خالی)
        //
        // پیشنهاد: مرکزِ دوره‌ی مالیِ همان رکورد، وگرنه مرکزِ صندوقِ همان رکورد.
        // ───────────────────────────────────────────────────────────────────
        private void DetectMissingCenter(List<RepairItem> items)
        {
            foreach (TableSpec sp in Specs)
            {
                DataTable dt = _db.Query(
                    "SELECT t." + sp.IdCol + " AS Id, t." + sp.AmountCol + " AS Amt, " +
                    "p.CenterID AS PeriodCenter, f.CenterID AS FundCenter " +
                    "FROM " + sp.Table + " t " +
                    "LEFT JOIN AccPeriod p ON p.PeriodID = t.PeriodID " +
                    "LEFT JOIN AccFund f ON f.FundID = t.FundID " +
                    "WHERE t.CenterID IS NULL AND COALESCE(t.IsReversed,0)=0 ORDER BY t." + sp.IdCol);

                foreach (DataRow r in dt.Rows)
                {
                    var item = new RepairItem
                    {
                        Kind = KindAssignCenter,
                        Category = "رکورد بدون مرکز",
                        Table = sp.Table,
                        IdColumn = sp.IdCol,
                        RecordId = Convert.ToInt32(r["Id"]),
                        Amount = D(r["Amt"]),
                        Problem = sp.Label + " شماره " + r["Id"] + " به هیچ مرکزی وصل نیست — " +
                                  "در هیچ گزارشِ مرکزمحوری دیده نمی‌شود.",
                        CurrentValue = "بدون مرکز"
                    };

                    int? suggested = null;
                    if (r["PeriodCenter"] != DBNull.Value)
                    {
                        suggested = Convert.ToInt32(r["PeriodCenter"]);
                        item.Basis = "مرکزِ دوره مالیِ همین رکورد.";
                    }
                    else if (r["FundCenter"] != DBNull.Value)
                    {
                        suggested = Convert.ToInt32(r["FundCenter"]);
                        item.Basis = "مرکزِ صندوقِ همین رکورد.";
                    }

                    if (suggested.HasValue)
                    {
                        item.HasSuggestion = true;
                        item.SuggestedCenterId = suggested;
                        item.Suggestion = GetCenterName(suggested.Value);
                    }
                    else
                    {
                        item.Suggestion = "— مبنای مطمئنی برای پیشنهاد نیست؛ مرکز را خودتان انتخاب کنید —";
                        item.Basis = "نه دوره‌ی رکورد مرکز دارد و نه صندوقش.";
                    }

                    items.Add(item);
                }
            }
        }

        // ───────────────────────────────────────────────────────────────────
        // ۳) تاریخ‌های بدقالب
        //
        // قالب استاندارد پروژه «yyyy/MM/dd» شمسی است. رکوردهای قدیمی گاهی
        // «21/04/1405 12:00:00 ق.ظ» (یعنی dd/MM/yyyy با بخش ساعت) ذخیره
        // شده‌اند. چنین مقداری همه‌ی مقایسه‌های تاریخی را از کار می‌اندازد،
        // چون مقایسه‌ی تاریخ در این پروژه مقایسه‌ی رشته‌ای است.
        // ───────────────────────────────────────────────────────────────────
        private void DetectMalformedDates(List<RepairItem> items)
        {
            AddDateIssues(items, "AccPeriod", "PeriodID", "StartDate", "تاریخ شروع دوره");
            AddDateIssues(items, "AccPeriod", "PeriodID", "EndDate", "تاریخ پایان دوره");
            AddDateIssues(items, "AccTransaction", "TxnID", "TxnDate", "تاریخ تراکنش");
            AddDateIssues(items, "AccExpenseItem", "ItemID", "ItemDate", "تاریخ قلم هزینه");
        }

        private void AddDateIssues(List<RepairItem> items, string table, string idCol, string dateCol, string label)
        {
            string reversedGuard = table == "AccPeriod" ? "" : " AND COALESCE(IsReversed,0)=0";

            DataTable dt = _db.Query(
                "SELECT " + idCol + " AS Id, " + dateCol + " AS Dt FROM " + table +
                " WHERE " + dateCol + " IS NOT NULL AND " + dateCol + " <> '' " +
                "  AND " + dateCol + " NOT GLOB '[0-9][0-9][0-9][0-9]/[0-9][0-9]/[0-9][0-9]'" +
                reversedGuard + " ORDER BY " + idCol);

            foreach (DataRow r in dt.Rows)
            {
                string current = S(r["Dt"]);
                string fixedDate = TryNormalizePersianDate(current);

                var item = new RepairItem
                {
                    Kind = KindFixDate,
                    Category = "تاریخ بدقالب",
                    Table = table,
                    IdColumn = idCol,
                    RecordId = Convert.ToInt32(r["Id"]),
                    DateColumn = dateCol,
                    CurrentValue = current,
                    Amount = 0,
                    Problem = label + " رکورد شماره " + r["Id"] + " قالب استاندارد «سال/ماه/روز» ندارد — " +
                              "همه‌ی مقایسه‌های تاریخی روی این رکورد از کار می‌افتد."
                };

                if (fixedDate != null)
                {
                    item.HasSuggestion = true;
                    item.SuggestedDate = fixedDate;
                    item.Suggestion = fixedDate;
                    item.Basis = "همان تاریخ، بازنویسی‌شده به قالب استاندارد سال/ماه/روز.";
                }
                else
                {
                    item.Suggestion = "— قابل تشخیص خودکار نیست؛ تاریخ درست را خودتان وارد کنید —";
                    item.Basis = "الگوی این مقدار شناخته‌شده نیست.";
                }

                items.Add(item);
            }
        }

        // تبدیل مقادیر رایجِ بدقالب به «yyyy/MM/dd». اگر مطمئن نباشیم، null
        // برمی‌گردانیم — بهتر است پیشنهادی ندهیم تا این‌که پیشنهاد غلط بدهیم.
        public static string TryNormalizePersianDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // حذف بخش ساعت: «21/04/1405 12:00:00 ق.ظ» → «21/04/1405»
            string s = raw.Trim();
            int sp = s.IndexOf(' ');
            if (sp > 0) s = s.Substring(0, sp);

            s = s.Replace('-', '/').Replace('.', '/');
            string[] parts = s.Split('/');
            if (parts.Length != 3) return null;

            int a, b, c;
            if (!int.TryParse(parts[0], out a) ||
                !int.TryParse(parts[1], out b) ||
                !int.TryParse(parts[2], out c)) return null;

            int year, month, day;

            if (parts[0].Length == 4) { year = a; month = b; day = c; }      // yyyy/M/d
            else if (parts[2].Length == 4) { day = a; month = b; year = c; } // dd/MM/yyyy
            else return null;                                                // مبهم — حدس نمی‌زنیم

            if (year < 1300 || year > 1500) return null;
            if (month < 1 || month > 12) return null;
            if (day < 1 || day > 31) return null;

            return year.ToString("0000") + "/" + month.ToString("00") + "/" + day.ToString("00");
        }

        // ───────────────────────────────────────────────────────────────────
        // ۴) رکوردهای تکراری
        //
        // آموزش: «تکراری» همیشه خطا نیست — ممکن است واقعاً دو پرداخت جدا با
        // مبلغ یکسان در یک روز انجام شده باشد. برای همین ابزار هرگز خودش
        // تصمیم نمی‌گیرد؛ فقط گروه را نشان می‌دهد و پیشنهاد می‌کند «قدیمی‌ترین
        // را نگه دار، بقیه را باطل کن». تصمیم نهایی با حسابدار است، و اصلاح
        // هم «ابطال» است نه حذف — یعنی رکورد برای همیشه قابل ردیابی می‌ماند.
        // ───────────────────────────────────────────────────────────────────
        private void DetectDuplicates(List<RepairItem> items)
        {
            DataTable txn = _db.Query(@"
SELECT TxnDate, Direction, Amount, GROUP_CONCAT(TxnID) AS Ids, COUNT(*) AS C
FROM AccTransaction
WHERE COALESCE(IsReversed,0)=0
GROUP BY TxnDate, Direction, COALESCE(FundID,-1), Amount, COALESCE(PeriodID,-1)
HAVING C > 1");

            foreach (DataRow g in txn.Rows)
                AddDuplicateGroup(items, "AccTransaction", "TxnID", g["Ids"].ToString(),
                    "تراکنش", "تاریخ " + g["TxnDate"] + "، " + g["Direction"] +
                    "، مبلغ " + D(g["Amount"]).ToString("N0"), D(g["Amount"]));

            DataTable stip = _db.Query(@"
SELECT SadatType, FamilySize, Province, District, AmountPerFamily,
       GROUP_CONCAT(StipendID) AS Ids, COUNT(*) AS C, MAX(TotalPaid) AS Tot
FROM AccStipend
WHERE COALESCE(IsReversed,0)=0
GROUP BY COALESCE(PeriodID,-1), SadatType, FamilySize, COALESCE(Province,''), COALESCE(District,''), AmountPerFamily
HAVING C > 1");

            foreach (DataRow g in stip.Rows)
                AddDuplicateGroup(items, "AccStipend", "StipendID", g["Ids"].ToString(),
                    "ردیف شهریه", g["SadatType"] + "، " + g["FamilySize"] + " نفره، " +
                    g["Province"] + "/" + g["District"], D(g["Tot"]));
        }

        // برای هر گروه تکراری، *به‌ازای هر رکورد اضافه* یک مورد جدا ساخته
        // می‌شود تا حسابدار تک‌تک را جداگانه تأیید کند — نه یک‌جا و دسته‌جمعی.
        private void AddDuplicateGroup(List<RepairItem> items, string table, string idCol,
            string ids, string label, string describe, double amount)
        {
            string[] parts = (ids ?? "").Split(',');
            if (parts.Length < 2) return;

            var sorted = new List<int>();
            foreach (string p in parts)
            {
                int v;
                if (int.TryParse(p.Trim(), out v)) sorted.Add(v);
            }
            sorted.Sort();
            if (sorted.Count < 2) return;

            int keep = sorted[0];   // قدیمی‌ترین نگه داشته می‌شود

            for (int i = 1; i < sorted.Count; i++)
            {
                items.Add(new RepairItem
                {
                    Kind = KindVoidDuplicate,
                    Category = "رکورد تکراری",
                    Table = table,
                    IdColumn = idCol,
                    RecordId = sorted[i],
                    Amount = amount,
                    Problem = label + " شماره " + sorted[i] + " با " + label + " شماره " + keep +
                              " کاملاً یکسان است (" + describe + ").",
                    CurrentValue = "معتبر (در مانده‌ها شمرده می‌شود)",
                    HasSuggestion = true,
                    Suggestion = "ابطال رکورد شماره " + sorted[i] + " و نگه‌داشتن شماره " + keep,
                    Basis = "قدیمی‌ترین رکورد گروه (شماره " + keep + ") به‌عنوان اصل نگه داشته می‌شود. " +
                            "اگر این‌ها واقعاً دو رویداد جدا هستند، این مورد را رد کنید."
                });
            }
        }

        private string GetCenterName(int centerId)
        {
            try
            {
                object v = _db.ExecuteScalar(
                    "SELECT CenterCode || ' - ' || CenterName FROM TblCenter WHERE CenterID=@id",
                    P("@id", centerId));
                if (v != null && v != DBNull.Value) return v.ToString();
            }
            catch { /* اگر ساختار مرکز در دسترس نبود، فقط شناسه نشان داده می‌شود */ }
            return "مرکز " + centerId;
        }

        // فهرست همه‌ی دوره‌ها بدون فیلتر مرکز.
        // آموزش: عمداً از AccountingRepo.GetPeriodsForCombo استفاده نمی‌شود،
        // چون آن متد بر اساس مرکزِ فعالِ کاربر فیلتر می‌کند. رکوردی که اصلاً
        // مرکز ندارد ممکن است به دوره‌ی هر مرکزی تعلق داشته باشد، پس ابزار
        // اصلاح باید همه‌ی گزینه‌ها را نشان دهد (و به همین دلیل هم فقط برای
        // مدیر کل باز است).
        public DataTable GetAllPeriodsForCombo()
        {
            try
            {
                return _db.Query(@"
SELECT p.PeriodID,
       COALESCE(NULLIF(p.Title,''), ('برج ' || p.Month || ' سال ' || p.Year))
         || COALESCE(' — ' || c.CenterName, '') AS Display
FROM AccPeriod p
LEFT JOIN TblCenter c ON c.CenterID = p.CenterID
ORDER BY p.Year DESC, p.Month DESC");
            }
            catch
            {
                // اگر جدول مراکز در دسترس نبود، فهرست بدون نام مرکز ساخته می‌شود
                // تا ابزار اصلاح همچنان قابل استفاده بماند.
                return _db.Query(@"
SELECT PeriodID,
       COALESCE(NULLIF(Title,''), ('برج ' || Month || ' سال ' || Year)) AS Display
FROM AccPeriod ORDER BY Year DESC, Month DESC");
            }
        }

        public DataTable GetCentersForCombo()
        {
            try
            {
                return _db.Query("SELECT CenterID, CenterCode || ' - ' || CenterName AS Display FROM TblCenter ORDER BY CenterCode");
            }
            catch
            {
                return new DataTable();
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // اعمال یک اصلاح — فقط با فراخوانی صریح از فرم، پس از تأیید کاربر
        // ═══════════════════════════════════════════════════════════════════
        public void Apply(RepairItem item, string reason)
        {
            if (item == null)
                throw new AccountingRuleException("موردی برای اصلاح انتخاب نشده است.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new AccountingRuleException("ثبت «دلیل اصلاح» اجباری است؛ بدون دلیل، اصلاح انجام نمی‌شود.");

            // اصلاح داده‌ی مالیِ تاریخی یک عملیات مدیریتی است.
            if (!CaseManagement.Enterprise.PermissionService.Require("Accounting.Repair"))
                throw new AccountingRuleException("اصلاح داده‌های تاریخی حسابداری فقط برای مدیر کل مجاز است.");

            switch (item.Kind)
            {
                case KindAssignPeriod: ApplyAssignPeriod(item, reason); break;
                case KindAssignCenter: ApplyAssignCenter(item, reason); break;
                case KindFixDate: ApplyFixDate(item, reason); break;
                case KindVoidDuplicate: ApplyVoidDuplicate(item, reason); break;
                default: throw new AccountingRuleException("نوع اصلاح ناشناخته است: " + item.Kind);
            }
        }

        private void ApplyAssignPeriod(RepairItem item, string reason)
        {
            if (!item.SuggestedPeriodId.HasValue)
                throw new AccountingRuleException("دوره مالی مقصد انتخاب نشده است.");

            int periodId = item.SuggestedPeriodId.Value;

            // دوره‌ی مقصد باید واقعاً وجود داشته باشد.
            object exists = _db.ExecuteScalar("SELECT COUNT(1) FROM AccPeriod WHERE PeriodID=@p", P("@p", periodId));
            if (Convert.ToInt32(exists) == 0)
                throw new AccountingRuleException("دوره مالی انتخاب‌شده وجود ندارد.");

            // دوره‌ی مقصد نباید «بسته» باشد — وگرنه یک رکورد بدون بررسی/تأیید
            // مستقیم وارد دفتر یک دوره‌ی قبلاً بسته‌شده می‌شود.
            if (!_repo.IsPeriodOpen(periodId))
                throw new AccountingRuleException("دوره مالی مقصد «بسته» است؛ تخصیص رکورد به دوره‌ی بسته مجاز نیست.");

            // نگهبان خوش‌بینانه: «AND PeriodID IS NULL» یعنی اگر در این فاصله
            // کسی دوره را ست کرده باشد، اصلاح انجام نمی‌شود.
            int affected = _db.ExecuteNonQuery(
                "UPDATE " + item.Table + " SET PeriodID=@p WHERE " + item.IdColumn + "=@id AND PeriodID IS NULL",
                P("@p", periodId), P("@id", item.RecordId));

            if (affected == 0)
                throw new AccountingRuleException(
                    "اصلاح انجام نشد — این رکورد دیگر «بدون دوره» نیست (احتمالاً کاربر دیگری آن را تغییر داده). " +
                    "بررسی را دوباره اجرا کنید.");

            AccAudit.LogChange("اصلاح داده: تخصیص دوره مالی", item.Table, item.RecordId,
                "بدون دوره", _repo.GetPeriodTitle(periodId) + " (شناسه " + periodId + ")", reason);
        }

        private void ApplyAssignCenter(RepairItem item, string reason)
        {
            if (!item.SuggestedCenterId.HasValue)
                throw new AccountingRuleException("مرکز مقصد انتخاب نشده است.");

            // دوره‌ی فعلیِ خودِ رکورد نباید «بسته» باشد — این ابزار هم مثل هر
            // مسیر ویرایش دیگر باید همین قاعده را رعایت کند.
            if (!_repo.IsRecordPeriodOpen(item.Table, item.IdColumn, item.RecordId))
                throw new AccountingRuleException("دوره مالی این رکورد «بسته» است؛ از طریق ابزار اصلاح هم قابل تغییر نیست.");

            int centerId = item.SuggestedCenterId.Value;

            int affected = _db.ExecuteNonQuery(
                "UPDATE " + item.Table + " SET CenterID=@c WHERE " + item.IdColumn + "=@id AND CenterID IS NULL",
                P("@c", centerId), P("@id", item.RecordId));

            if (affected == 0)
                throw new AccountingRuleException(
                    "اصلاح انجام نشد — این رکورد دیگر «بدون مرکز» نیست. بررسی را دوباره اجرا کنید.");

            AccAudit.LogChange("اصلاح داده: تخصیص مرکز", item.Table, item.RecordId,
                "بدون مرکز", GetCenterName(centerId) + " (شناسه " + centerId + ")", reason);
        }

        private void ApplyFixDate(RepairItem item, string reason)
        {
            if (string.IsNullOrWhiteSpace(item.SuggestedDate))
                throw new AccountingRuleException("تاریخ اصلاح‌شده وارد نشده است.");

            // دوره‌ی فعلیِ خودِ رکورد نباید «بسته» باشد — همان قاعده‌ی بالا.
            if (!_repo.IsRecordPeriodOpen(item.Table, item.IdColumn, item.RecordId))
                throw new AccountingRuleException("دوره مالی این رکورد «بسته» است؛ از طریق ابزار اصلاح هم قابل تغییر نیست.");

            string newDate = item.SuggestedDate.Trim();

            // تاریخ جدید حتماً باید قالب استاندارد داشته باشد، وگرنه یک مشکل
            // را با مشکل دیگری عوض می‌کنیم.
            if (!IsStandardDate(newDate))
                throw new AccountingRuleException("تاریخ باید دقیقاً به قالب «سال/ماه/روز» باشد — مثال: 1405/04/21");

            // نگهبان خوش‌بینانه: مقدار قبلی باید هنوز همان باشد.
            int affected = _db.ExecuteNonQuery(
                "UPDATE " + item.Table + " SET " + item.DateColumn + "=@d " +
                "WHERE " + item.IdColumn + "=@id AND " + item.DateColumn + "=@old",
                P("@d", newDate), P("@id", item.RecordId), P("@old", item.CurrentValue));

            if (affected == 0)
                throw new AccountingRuleException(
                    "اصلاح انجام نشد — مقدار فعلی تاریخ با آنچه بررسی دیده بود فرق دارد. بررسی را دوباره اجرا کنید.");

            AccAudit.LogChange("اصلاح داده: تصحیح تاریخ (" + item.DateColumn + ")", item.Table, item.RecordId,
                item.CurrentValue, newDate, reason);
        }

        // ابطال از مسیر خودِ Repo انجام می‌شود تا همان قواعد همیشگی (دوره‌ی
        // بسته، مرکز، ثبت حسابرسی) دقیقاً رعایت شود — ابزار اصلاح هیچ راه
        // میان‌بری برای دور زدن قواعد ندارد.
        private void ApplyVoidDuplicate(RepairItem item, string reason)
        {
            string full = "اصلاح داده تاریخی — رکورد تکراری. " + reason;

            if (item.Table == "AccTransaction") _repo.VoidTransaction(item.RecordId, full);
            else if (item.Table == "AccStipend") _repo.VoidStipend(item.RecordId, full);
            else if (item.Table == "AccSalary") _repo.VoidSalary(item.RecordId, full);
            else if (item.Table == "AccExpenseItem") _repo.VoidExpenseItem(item.RecordId, full);
            else throw new AccountingRuleException("ابطال برای این جدول پشتیبانی نمی‌شود: " + item.Table);
        }

        private static bool IsStandardDate(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 10) return false;
            if (s[4] != '/' || s[7] != '/') return false;

            for (int i = 0; i < s.Length; i++)
            {
                if (i == 4 || i == 7) continue;
                if (s[i] < '0' || s[i] > '9') return false;
            }
            return true;
        }
    }
}

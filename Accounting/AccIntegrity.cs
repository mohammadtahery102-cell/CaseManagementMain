using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    // ─────────────────────────────────────────────────────────────────────────
    // AccIntegrity — موتور «بررسی خودکار صحت حسابداری».
    //
    // آموزش — این کلاس کاملاً «فقط‌خواندنی» است: هیچ رکوردی را تغییر نمی‌دهد،
    // حذف نمی‌کند و اصلاح نمی‌کند. وظیفه‌اش فقط پیدا کردن و گزارش‌کردن مغایرت
    // است تا تصمیم اصلاح با حسابدار انسانی باشد. دلیل این محافظه‌کاری ساده
    // است: «اصلاح خودکار» روی داده‌ی مالی می‌تواند یک اشتباه را به یک فاجعه‌ی
    // غیرقابل‌بازگشت تبدیل کند.
    //
    // بررسی‌ها بر پایه‌ی سناریوهایی نوشته شده‌اند که واقعاً می‌توانند محاسبه‌ی
    // حسابداری را غلط کنند — و چند مورد از آن‌ها در همین دیتابیس فعلی هم
    // مشاهده شده‌اند (رکوردهای بدون دوره مالی، تراکنش تکراری، مرکز خالی).
    // ─────────────────────────────────────────────────────────────────────────
    public class AccIntegrity
    {
        public const string SeverityCritical = "بحرانی";
        public const string SeverityWarning = "هشدار";
        public const string SeverityInfo = "اطلاع";

        public class Issue
        {
            public string Severity;
            public string Category;
            public string Description;
            public string Entity;
            public int EntityId;
            public double Amount;
        }

        private readonly AccountingRepo _repo;
        private readonly DatabaseHelper _db = new DatabaseHelper();

        public AccIntegrity(AccountingRepo repo) { _repo = repo; }

        private int Cid { get { return SecurityContext.CenterFilterId; } }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        private static double D(object v)
        {
            return v == null || v == DBNull.Value ? 0 : Money.Round(Convert.ToDouble(v));
        }

        // ═══════════════════════════════════════════════════════════════════
        // اجرای همه‌ی بررسی‌ها
        // ═══════════════════════════════════════════════════════════════════
        public List<Issue> RunAllChecks()
        {
            var issues = new List<Issue>();

            CheckPeriodBalanceEquation(issues);
            CheckUnassignedPeriod(issues);
            CheckUnassignedCenter(issues);
            CheckDanglingReferences(issues);
            CheckStipendTotals(issues);
            CheckCurrencyConversion(issues);
            CheckNonPositiveAmounts(issues);
            CheckDuplicateTransactions(issues);
            CheckDuplicateDocNo(issues);
            CheckDuplicateStipends(issues);
            CheckFundBalances(issues);
            CheckPossibleDoubleEntry(issues);
            CheckTransactionDatesInPeriod(issues);

            return issues;
        }

        private static void Add(List<Issue> list, string severity, string category,
            string description, string entity, int entityId, double amount)
        {
            list.Add(new Issue
            {
                Severity = severity,
                Category = category,
                Description = description,
                Entity = entity,
                EntityId = entityId,
                Amount = amount
            });
        }

        // ───────────────────────────────────────────────────────────────────
        // ۱) معادله‌ی پایه: مانده ابتدا + درآمد − هزینه = مانده پایان
        //
        // آموزش — نکته‌ی مهم روش‌شناسی: صرفاً صدازدن GetPeriodClosing و مقایسه
        // با فرمول، یک بررسی بی‌فایده است چون خودِ آن متد همان فرمول را اجرا
        // می‌کند (تُتولوژی). برای این‌که بررسی واقعاً چیزی را اثبات کند، اینجا
        // اجزاء را با یک کوئری *مستقل* از پایگاه داده می‌خوانیم و نتیجه را با
        // خروجی GetPeriodClosing مقایسه می‌کنیم. اگر این دو یکی نبودند، یعنی
        // یکی از مسیرها فیلتری را جا انداخته (مثلاً فیلتر ابطال یا مرکز).
        // ───────────────────────────────────────────────────────────────────
        private void CheckPeriodBalanceEquation(List<Issue> issues)
        {
            DataTable periods = _db.Query(
                "SELECT PeriodID, COALESCE(Title,('برج '||Month||' سال '||Year)) AS Title, OpeningBalance, Status " +
                "FROM AccPeriod WHERE (@cid = 0 OR CenterID = @cid) ORDER BY Year, Month", P("@cid", Cid));

            foreach (DataRow p in periods.Rows)
            {
                int pid = Convert.ToInt32(p["PeriodID"]);
                string title = p["Title"].ToString();
                double opening = D(p["OpeningBalance"]);

                double income = ScalarSum(
                    "SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE PeriodID=@p AND Direction='دریافت' AND (@cid=0 OR CenterID=@cid) AND COALESCE(IsReversed,0)=0", pid);
                double payments = ScalarSum(
                    "SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE PeriodID=@p AND Direction='پرداخت' AND (@cid=0 OR CenterID=@cid) AND COALESCE(IsReversed,0)=0", pid);
                double stipend = ScalarSum(
                    "SELECT COALESCE(SUM(TotalPaid),0) FROM AccStipend WHERE PeriodID=@p AND (@cid=0 OR CenterID=@cid) AND COALESCE(IsReversed,0)=0", pid);
                double salary = ScalarSum(
                    "SELECT COALESCE(SUM(Amount),0) FROM AccSalary WHERE PeriodID=@p AND (@cid=0 OR CenterID=@cid) AND COALESCE(IsReversed,0)=0", pid);
                double items = ScalarSum(
                    "SELECT COALESCE(SUM(Price),0) FROM AccExpenseItem WHERE PeriodID=@p AND (@cid=0 OR CenterID=@cid) AND COALESCE(IsReversed,0)=0", pid);

                double expected = Money.Round(opening + income - payments - stipend - salary - items);
                double reported = _repo.GetPeriodClosing(pid);

                if (!Money.AreEqual(expected, reported))
                    Add(issues, SeverityCritical, "معادله حسابداری",
                        "دوره «" + title + "»: مانده محاسبه‌شده از دفتر (" + expected.ToString("N0") +
                        ") با مانده گزارش‌شده (" + reported.ToString("N0") + ") برابر نیست.",
                        "AccPeriod", pid, expected - reported);
            }
        }

        private double ScalarSum(string sql, int periodId)
        {
            return D(_db.ExecuteScalar(sql, P("@p", periodId), P("@cid", Cid)));
        }

        // ───────────────────────────────────────────────────────────────────
        // ۲) رکوردهای مالیِ بدون دوره مالی
        //
        // آموزش — چرا این بحرانی است: هر گزارشی که بر اساس دوره فیلتر می‌شود
        // این رکوردها را *نمی‌بیند*، ولی «مانده صندوق» (که فیلتر دوره ندارد)
        // آن‌ها را می‌بیند. نتیجه: مانده‌ی صندوق هرگز با جمع مانده‌ی دوره‌ها
        // نمی‌خواند و هیچ‌کس نمی‌فهمد چرا. در دیتابیس فعلی، ۱۷ ردیف از ۲۲ ردیف
        // شهریه در همین وضعیت است.
        // ───────────────────────────────────────────────────────────────────
        private void CheckUnassignedPeriod(List<Issue> issues)
        {
            CheckUnassigned(issues, "AccTransaction", "TxnID", "Amount", "تراکنش", "PeriodID");
            CheckUnassigned(issues, "AccStipend", "StipendID", "TotalPaid", "ردیف شهریه", "PeriodID");
            CheckUnassigned(issues, "AccSalary", "SalaryID", "Amount", "ردیف حقوق", "PeriodID");
            CheckUnassigned(issues, "AccExpenseItem", "ItemID", "Price", "قلم هزینه", "PeriodID");
        }

        private void CheckUnassignedCenter(List<Issue> issues)
        {
            CheckUnassigned(issues, "AccTransaction", "TxnID", "Amount", "تراکنش", "CenterID");
            CheckUnassigned(issues, "AccStipend", "StipendID", "TotalPaid", "ردیف شهریه", "CenterID");
            CheckUnassigned(issues, "AccSalary", "SalaryID", "Amount", "ردیف حقوق", "CenterID");
            CheckUnassigned(issues, "AccExpenseItem", "ItemID", "Price", "قلم هزینه", "CenterID");
        }

        private void CheckUnassigned(List<Issue> issues, string table, string idCol,
            string amountCol, string label, string column)
        {
            // برای بررسی CenterID عمداً فیلتر مرکز اعمال نمی‌شود — رکوردی که
            // CenterID ندارد اصلاً با فیلتر مرکز پیدا نمی‌شود، و همین نکته‌ی
            // اصلی این بررسی است.
            string where = column == "CenterID"
                ? "WHERE CenterID IS NULL"
                : "WHERE " + column + " IS NULL AND (@cid = 0 OR CenterID = @cid)";

            DataTable dt = _db.Query(
                "SELECT " + idCol + " AS Id, " + amountCol + " AS Amt FROM " + table + " " +
                where + " AND COALESCE(IsReversed,0)=0", P("@cid", Cid));

            foreach (DataRow r in dt.Rows)
            {
                string reason = column == "CenterID"
                    ? label + " شماره " + r["Id"] + " به هیچ مرکزی وصل نیست، پس در هیچ گزارشِ مرکز‌محور دیده نمی‌شود."
                    : label + " شماره " + r["Id"] + " به هیچ دوره مالی وصل نیست، پس در گزارش‌های دوره‌ای شمرده نمی‌شود اما در مانده صندوق هست.";

                Add(issues, SeverityCritical, "رکورد بدون " + (column == "CenterID" ? "مرکز" : "دوره"),
                    reason, table, Convert.ToInt32(r["Id"]), D(r["Amt"]));
            }
        }

        // ───────────────────────────────────────────────────────────────────
        // ۳) ارجاع‌های شکسته (کلید خارجی بدون مقصد)
        //
        // آموزش: جداول Acc* کلید خارجی واقعی (FOREIGN KEY) ندارند، پس هیچ‌چیز
        // مانع از باقی‌ماندن یک PeriodID/FundID/PartyID به رکوردی که دیگر وجود
        // ندارد نمی‌شود. چنین رکوردی در JOINها با LEFT JOIN به NULL تبدیل و
        // در گزارش بی‌نام ظاهر می‌شود، یا از فیلترها می‌افتد.
        // ───────────────────────────────────────────────────────────────────
        private void CheckDanglingReferences(List<Issue> issues)
        {
            CheckDangling(issues, "AccTransaction", "TxnID", "PeriodID", "AccPeriod", "PeriodID", "دوره مالی");
            CheckDangling(issues, "AccTransaction", "TxnID", "FundID", "AccFund", "FundID", "صندوق");
            CheckDangling(issues, "AccTransaction", "TxnID", "PartyID", "AccParty", "PartyID", "طرف حساب");
            CheckDangling(issues, "AccStipend", "StipendID", "PeriodID", "AccPeriod", "PeriodID", "دوره مالی");
            CheckDangling(issues, "AccStipend", "StipendID", "FundID", "AccFund", "FundID", "صندوق");
            CheckDangling(issues, "AccSalary", "SalaryID", "PeriodID", "AccPeriod", "PeriodID", "دوره مالی");
            CheckDangling(issues, "AccSalary", "SalaryID", "FundID", "AccFund", "FundID", "صندوق");
            CheckDangling(issues, "AccExpenseItem", "ItemID", "PeriodID", "AccPeriod", "PeriodID", "دوره مالی");
            CheckDangling(issues, "AccExpenseItem", "ItemID", "FundID", "AccFund", "FundID", "صندوق");
        }

        private void CheckDangling(List<Issue> issues, string table, string idCol, string fkCol,
            string refTable, string refCol, string refLabel)
        {
            DataTable dt = _db.Query(
                "SELECT t." + idCol + " AS Id, t." + fkCol + " AS Fk FROM " + table + " t " +
                "LEFT JOIN " + refTable + " r ON r." + refCol + " = t." + fkCol + " " +
                "WHERE t." + fkCol + " IS NOT NULL AND r." + refCol + " IS NULL " +
                "AND (@cid = 0 OR t.CenterID = @cid) AND COALESCE(t.IsReversed,0)=0", P("@cid", Cid));

            foreach (DataRow r in dt.Rows)
                Add(issues, SeverityCritical, "ارجاع شکسته",
                    table + " شماره " + r["Id"] + " به " + refLabel + " شماره " + r["Fk"] +
                    " اشاره می‌کند که وجود ندارد.",
                    table, Convert.ToInt32(r["Id"]), 0);
        }

        // ───────────────────────────────────────────────────────────────────
        // ۴) صحت محاسبه‌ی شهریه: جمع پرداختی = تعداد خانوار × مبلغ هر خانواده
        //
        // آموزش: TotalPaid در زمان ثبت یک‌بار محاسبه و ذخیره می‌شود (مقدار
        // مشتق‌شده). اگر کسی مستقیماً در پایگاه داده یا از طریق بازیابی یک
        // بکاپ قدیمی، FamilyCount یا AmountPerFamily را تغییر دهد، TotalPaid
        // به‌روز نمی‌شود و برای همیشه غلط می‌ماند.
        // ───────────────────────────────────────────────────────────────────
        private void CheckStipendTotals(List<Issue> issues)
        {
            DataTable dt = _db.Query(@"
SELECT StipendID, FamilyCount, AmountPerFamily, TotalPaid
FROM AccStipend
WHERE (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@cid", Cid));

            foreach (DataRow r in dt.Rows)
            {
                double expected = Money.Round(Convert.ToDouble(r["FamilyCount"]) * D(r["AmountPerFamily"]));
                double stored = D(r["TotalPaid"]);

                if (!Money.AreEqual(expected, stored))
                    Add(issues, SeverityCritical, "محاسبه شهریه",
                        "ردیف شهریه شماره " + r["StipendID"] + ": جمع پرداختی ذخیره‌شده " +
                        stored.ToString("N0") + " است اما تعداد خانوار × مبلغ = " + expected.ToString("N0") + ".",
                        "AccStipend", Convert.ToInt32(r["StipendID"]), stored - expected);
            }
        }

        // ───────────────────────────────────────────────────────────────────
        // ۵) تبدیل ارز: مبلغ افغانی باید با مبلغ دلاری × نرخ بخواند
        //
        // آموزش: فرم مبلغ افغانی را خودکار از دلار × نرخ حساب می‌کند، اما
        // کاربر می‌تواند بعد از آن مبلغ افغانی را دستی عوض کند بدون آن‌که
        // دلار/نرخ به‌روز شود. آن‌وقت سند سه عدد ناسازگار را نگه می‌دارد و
        // «نرخ مؤثر» که گزارش صورت‌حساب کلی محاسبه می‌کند غلط درمی‌آید.
        // هم‌چنین مبلغ دلاری بدون نرخ یعنی مستند تبدیل ارز گم شده است — که
        // در دیتابیس فعلی روی تراکنش‌های ۱ و ۲ دیده می‌شود.
        // ───────────────────────────────────────────────────────────────────
        private void CheckCurrencyConversion(List<Issue> issues)
        {
            DataTable dt = _db.Query(@"
SELECT TxnID, DocNo, Amount, DollarAmount, DollarRate
FROM AccTransaction
WHERE DollarAmount IS NOT NULL AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0",
                P("@cid", Cid));

            foreach (DataRow r in dt.Rows)
            {
                int id = Convert.ToInt32(r["TxnID"]);
                double amount = D(r["Amount"]);
                double dollar = D(r["DollarAmount"]);

                if (r["DollarRate"] == DBNull.Value || D(r["DollarRate"]) <= 0)
                {
                    Add(issues, SeverityWarning, "تبدیل ارز",
                        "تراکنش سند " + r["DocNo"] + ": مبلغ دلاری " + dollar.ToString("N2") +
                        " ثبت شده اما نرخ دلار ذخیره نشده — مستند تبدیل ارز ناقص است." +
                        (dollar > 0 ? "  (نرخ ضمنی: " + Money.Round(amount / dollar).ToString("N2") + ")" : ""),
                        "AccTransaction", id, 0);
                    continue;
                }

                double rate = D(r["DollarRate"]);
                if (!Money.IsConversionConsistent(amount, dollar, rate))
                    Add(issues, SeverityCritical, "تبدیل ارز",
                        "تراکنش سند " + r["DocNo"] + ": مبلغ افغانی " + amount.ToString("N0") +
                        " با دلار × نرخ (" + Money.Convert(dollar, rate).ToString("N0") + ") نمی‌خواند.",
                        "AccTransaction", id, amount - Money.Convert(dollar, rate));
            }
        }

        // ───────────────────────────────────────────────────────────────────
        // ۶) مبالغ صفر یا منفی
        // ───────────────────────────────────────────────────────────────────
        private void CheckNonPositiveAmounts(List<Issue> issues)
        {
            CheckNonPositive(issues, "AccTransaction", "TxnID", "Amount", "تراکنش");
            CheckNonPositive(issues, "AccStipend", "StipendID", "TotalPaid", "ردیف شهریه");
            CheckNonPositive(issues, "AccSalary", "SalaryID", "Amount", "ردیف حقوق");
            CheckNonPositive(issues, "AccExpenseItem", "ItemID", "Price", "قلم هزینه");
        }

        private void CheckNonPositive(List<Issue> issues, string table, string idCol, string amountCol, string label)
        {
            DataTable dt = _db.Query(
                "SELECT " + idCol + " AS Id, " + amountCol + " AS Amt FROM " + table +
                " WHERE " + amountCol + " <= 0 AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0",
                P("@cid", Cid));

            foreach (DataRow r in dt.Rows)
                Add(issues, SeverityWarning, "مبلغ نامعتبر",
                    label + " شماره " + r["Id"] + " مبلغ " + D(r["Amt"]).ToString("N0") + " دارد (صفر یا منفی).",
                    table, Convert.ToInt32(r["Id"]), D(r["Amt"]));
        }

        // ───────────────────────────────────────────────────────────────────
        // ۷) تراکنش‌های احتمالاً تکراری
        //
        // آموزش: «تکراری» در حسابداری همیشه خطا نیست — ممکن است واقعاً دو
        // پرداخت جدا با مبلغ یکسان در یک روز انجام شده باشد. برای همین سطح
        // این مورد «هشدار» است نه «بحرانی»: سیستم آن را نشان می‌دهد و قضاوت
        // نهایی با حسابدار است.
        // ───────────────────────────────────────────────────────────────────
        private void CheckDuplicateTransactions(List<Issue> issues)
        {
            DataTable dt = _db.Query(@"
SELECT TxnDate, Direction, FundID, Amount, COUNT(*) AS C, GROUP_CONCAT(TxnID) AS Ids
FROM AccTransaction
WHERE (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0
GROUP BY TxnDate, Direction, COALESCE(FundID,-1), Amount, COALESCE(PeriodID,-1)
HAVING C > 1", P("@cid", Cid));

            foreach (DataRow r in dt.Rows)
                Add(issues, SeverityWarning, "تراکنش تکراری",
                    "تعداد " + r["C"] + " تراکنش کاملاً یکسان (تاریخ " + r["TxnDate"] + "، " +
                    r["Direction"] + "، مبلغ " + D(r["Amount"]).ToString("N0") +
                    ") ثبت شده — شناسه‌ها: " + r["Ids"] + ".",
                    "AccTransaction", 0, D(r["Amount"]) * (Convert.ToInt32(r["C"]) - 1));
        }

        // شماره سند تکراری در یک دوره — این یکی همیشه خطاست.
        private void CheckDuplicateDocNo(List<Issue> issues)
        {
            DataTable dt = _db.Query(@"
SELECT DocNo, PeriodID, COUNT(*) AS C, GROUP_CONCAT(TxnID) AS Ids
FROM AccTransaction
WHERE DocNo IS NOT NULL AND DocNo <> '' AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0
GROUP BY DocNo, COALESCE(PeriodID,-1)
HAVING C > 1", P("@cid", Cid));

            foreach (DataRow r in dt.Rows)
                Add(issues, SeverityCritical, "شماره سند تکراری",
                    "شماره سند «" + r["DocNo"] + "» در یک دوره مالی " + r["C"] +
                    " بار استفاده شده — شناسه‌ها: " + r["Ids"] + ".",
                    "AccTransaction", 0, 0);
        }

        private void CheckDuplicateStipends(List<Issue> issues)
        {
            DataTable dt = _db.Query(@"
SELECT SadatType, FamilySize, Province, District, COUNT(*) AS C, GROUP_CONCAT(StipendID) AS Ids, SUM(TotalPaid) AS Tot
FROM AccStipend
WHERE (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0
GROUP BY COALESCE(PeriodID,-1), SadatType, FamilySize, COALESCE(Province,''), COALESCE(District,'')
HAVING C > 1", P("@cid", Cid));

            foreach (DataRow r in dt.Rows)
                Add(issues, SeverityWarning, "شهریه تکراری",
                    "تعداد " + r["C"] + " ردیف شهریه با مشخصات یکسان (" + r["SadatType"] + "، " +
                    r["FamilySize"] + " نفره، " + r["Province"] + "/" + r["District"] +
                    ") در یک دوره — شناسه‌ها: " + r["Ids"] + ".",
                    "AccStipend", 0, D(r["Tot"]));
        }

        // ───────────────────────────────────────────────────────────────────
        // ۸) مانده صندوق‌ها: منفی نباشد، و مجموع صندوق‌ها با مجموع دوره‌ها بخواند
        // ───────────────────────────────────────────────────────────────────
        private void CheckFundBalances(List<Issue> issues)
        {
            DataTable funds = _db.Query(
                "SELECT FundID, Name FROM AccFund WHERE (@cid = 0 OR CenterID = @cid)", P("@cid", Cid));

            foreach (DataRow f in funds.Rows)
            {
                int fid = Convert.ToInt32(f["FundID"]);
                double bal = _repo.GetFundBalance(fid);

                if (bal < -Money.Epsilon)
                    Add(issues, SeverityCritical, "مانده منفی صندوق",
                        "صندوق «" + f["Name"] + "» مانده منفی دارد: " + bal.ToString("N0") +
                        " افغانی — یعنی بیش از موجودی از آن پرداخت شده است.",
                        "AccFund", fid, bal);
            }

            // مبالغی که به هیچ صندوقی وصل نیستند در مانده‌ی هیچ صندوقی دیده
            // نمی‌شوند، پس جمع صندوق‌ها با جمع کل نمی‌خواند.
            double orphanTxn = D(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE FundID IS NULL AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@cid", Cid)));

            if (!Money.IsZero(orphanTxn))
                Add(issues, SeverityWarning, "تراکنش بدون صندوق",
                    "مجموع " + orphanTxn.ToString("N0") + " افغانی تراکنش بدون صندوق ثبت شده و در مانده هیچ صندوقی شمرده نمی‌شود.",
                    "AccTransaction", 0, orphanTxn);
        }

        // ───────────────────────────────────────────────────────────────────
        // ۹) احتمال ثبت دوباره‌ی یک هزینه از دو مسیر مختلف
        //
        // آموزش — این ظریف‌ترین و خطرناک‌ترین سناریوی این ماژول است:
        // دسته‌بندی‌های پیش‌فرضِ هزینه شامل «شهریه ایتام عام/سادات/اهل سنت» و
        // «حقوق» هستند. یعنی کاربر می‌تواند یک پرداخت شهریه را هم در تب
        // «شهریه ایتام» ثبت کند و هم به‌عنوان یک تراکنش «پرداخت» با همان
        // دسته‌بندی. محاسبه‌ی مانده هر دو را کم می‌کند، پس آن مبلغ دو بار از
        // موجودی کسر می‌شود و هیچ خطایی هم داده نمی‌شود.
        // ───────────────────────────────────────────────────────────────────
        private void CheckPossibleDoubleEntry(List<Issue> issues)
        {
            DataTable dt = _db.Query(@"
SELECT p.PeriodID,
       COALESCE(p.Title,('برج '||p.Month||' سال '||p.Year)) AS Title,
       ec.Name AS CatName,
       COALESCE(SUM(t.Amount),0) AS TxnSum
FROM AccTransaction t
JOIN AccExpenseCategory ec ON ec.CatID = t.CategoryID
LEFT JOIN AccPeriod p ON p.PeriodID = t.PeriodID
WHERE t.Direction = 'پرداخت' AND t.CategoryType = 'Expense'
  AND (ec.Name LIKE 'شهریه%' OR ec.Name = 'حقوق')
  AND (@cid = 0 OR t.CenterID = @cid) AND COALESCE(t.IsReversed,0)=0
GROUP BY t.PeriodID, ec.Name", P("@cid", Cid));

            foreach (DataRow r in dt.Rows)
            {
                bool isSalary = r["CatName"].ToString() == "حقوق";
                int? pid = r["PeriodID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PeriodID"]);

                double otherSum = isSalary ? _repo.SumSalary(pid) : _repo.SumStipend(pid, null);
                if (Money.IsZero(otherSum)) continue;   // فقط یکی از دو مسیر استفاده شده — مشکلی نیست

                Add(issues, SeverityCritical, "احتمال ثبت مضاعف",
                    "دوره «" + (r["Title"] == DBNull.Value ? "بدون دوره" : r["Title"]) + "»: مبلغ " +
                    D(r["TxnSum"]).ToString("N0") + " به‌صورت تراکنش پرداخت با دسته‌بندی «" + r["CatName"] +
                    "» ثبت شده، در حالی که در تب اختصاصی هم " + otherSum.ToString("N0") +
                    " افغانی ثبت شده است. این مبلغ احتمالاً دو بار از موجودی کسر می‌شود.",
                    "AccTransaction", 0, D(r["TxnSum"]));
            }
        }

        // ───────────────────────────────────────────────────────────────────
        // ۱۰) تاریخ سند خارج از بازه‌ی دوره مالی
        //
        // آموزش: تاریخ‌ها به‌صورت متن شمسی «yyyy/MM/dd» ذخیره می‌شوند. چون این
        // قالب صفر‌پیشوند و ثابت‌طول است، مقایسه‌ی رشته‌ای دقیقاً معادل مقایسه‌ی
        // تاریخی است. رکوردهای قدیمی که قالب دیگری دارند (مثل دوره‌ای که در
        // دیتابیس فعلی تاریخش «21/04/1405 12:00:00 ق.ظ» است) از این بررسی
        // کنار گذاشته می‌شوند و جداگانه گزارش می‌شوند.
        // ───────────────────────────────────────────────────────────────────
        private void CheckTransactionDatesInPeriod(List<Issue> issues)
        {
            DataTable bad = _db.Query(@"
SELECT PeriodID, COALESCE(Title,('برج '||Month||' سال '||Year)) AS Title, StartDate, EndDate
FROM AccPeriod
WHERE (@cid = 0 OR CenterID = @cid)
  AND ( StartDate IS NULL OR EndDate IS NULL
     OR StartDate NOT GLOB '[0-9][0-9][0-9][0-9]/[0-9][0-9]/[0-9][0-9]'
     OR EndDate   NOT GLOB '[0-9][0-9][0-9][0-9]/[0-9][0-9]/[0-9][0-9]' )", P("@cid", Cid));

            foreach (DataRow r in bad.Rows)
                Add(issues, SeverityWarning, "تاریخ دوره نامعتبر",
                    "دوره «" + r["Title"] + "» تاریخ شروع/پایان معتبر ندارد (شروع: " +
                    (r["StartDate"] == DBNull.Value ? "خالی" : r["StartDate"].ToString()) + "، پایان: " +
                    (r["EndDate"] == DBNull.Value ? "خالی" : r["EndDate"].ToString()) +
                    ") — بررسی تاریخ اسناد این دوره ممکن نیست.",
                    "AccPeriod", Convert.ToInt32(r["PeriodID"]), 0);

            DataTable outside = _db.Query(@"
SELECT t.TxnID, t.DocNo, t.TxnDate, COALESCE(p.Title,('برج '||p.Month||' سال '||p.Year)) AS Title,
       p.StartDate, p.EndDate
FROM AccTransaction t
JOIN AccPeriod p ON p.PeriodID = t.PeriodID
WHERE (@cid = 0 OR t.CenterID = @cid) AND COALESCE(t.IsReversed,0)=0
  AND t.TxnDate GLOB '[0-9][0-9][0-9][0-9]/[0-9][0-9]/[0-9][0-9]'
  AND p.StartDate GLOB '[0-9][0-9][0-9][0-9]/[0-9][0-9]/[0-9][0-9]'
  AND p.EndDate   GLOB '[0-9][0-9][0-9][0-9]/[0-9][0-9]/[0-9][0-9]'
  AND (t.TxnDate < p.StartDate OR t.TxnDate > p.EndDate)", P("@cid", Cid));

            foreach (DataRow r in outside.Rows)
                Add(issues, SeverityWarning, "تاریخ سند خارج از دوره",
                    "تراکنش سند " + r["DocNo"] + " تاریخ " + r["TxnDate"] +
                    " دارد اما به دوره «" + r["Title"] + "» (" + r["StartDate"] + " تا " + r["EndDate"] +
                    ") وصل است.",
                    "AccTransaction", Convert.ToInt32(r["TxnID"]), 0);
        }
    }
}

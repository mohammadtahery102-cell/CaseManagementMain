using System;
using System.Data;
using System.Data.SQLite;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه دسترسی به داده (Repository) ماژول حسابداری ایتام.
    // آموزش: تمام دسترسی SQL این ماژول از همین‌جا انجام می‌شود تا منطق تجاری/UI
    // مستقیماً کوئری ننویسند (جدایی لایه‌ها). از DatabaseHelper پروژه استفاده
    // می‌کند. فیلتر مرکز (CenterID) مطابق SecurityContext رعایت می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public class AccountingRepo
    {
        private readonly DatabaseHelper _db = new DatabaseHelper();

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        private int Cid { get { return SecurityContext.CenterFilterId; } }         // 0 = همه مراکز
        private object CurrentCid { get { return SecurityContext.CurrentCenterId > 0 ? (object)SecurityContext.CurrentCenterId : DBNull.Value; } }

        // ═══════════════════════════════════════════════════════════════════
        // دوره مالی
        // ═══════════════════════════════════════════════════════════════════
        public DataTable GetPeriods()
        {
            return _db.Query(@"
SELECT PeriodID, Year AS [سال], Month AS [از برج], MonthTo AS [تا برج], Title AS [عنوان],
       StartDate AS [شروع], EndDate AS [پایان], OpeningBalance AS [مانده ابتدای دوره],
       Status AS [وضعیت]
FROM AccPeriod
WHERE (@cid = 0 OR CenterID = @cid)
ORDER BY Year DESC, Month DESC", P("@cid", Cid));
        }

        // فهرست دوره‌ها برای ComboBox (PeriodID + عنوان)
        public DataTable GetPeriodsForCombo()
        {
            return _db.Query(@"
SELECT PeriodID,
       COALESCE(NULLIF(Title,''),
                CASE WHEN MonthTo > 0 AND MonthTo <> Month
                     THEN ('برج ' || Month || ' تا ' || MonthTo || ' سال ' || Year)
                     ELSE ('برج ' || Month || ' سال ' || Year) END) AS Display,
       Status
FROM AccPeriod
WHERE (@cid = 0 OR CenterID = @cid)
ORDER BY Year DESC, Month DESC", P("@cid", Cid));
        }

        public int AddPeriod(int year, int monthFrom, int monthTo, string title, string start, string end, double opening)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO AccPeriod (Year, Month, MonthTo, Title, StartDate, EndDate, OpeningBalance, Status, CenterID, CreatedBy)
VALUES (@y, @m, @mt, @t, @s, @e, @o, 'باز', @cid, @by)",
                P("@y", year), P("@m", monthFrom), P("@mt", monthTo), P("@t", title), P("@s", start), P("@e", end),
                P("@o", opening), P("@cid", CurrentCid), P("@by", SecurityContext.Username));
            return Convert.ToInt32(_db.ExecuteScalar("SELECT last_insert_rowid()"));
        }

        public void UpdatePeriod(int id, int year, int monthFrom, int monthTo, string title, string start, string end, double opening)
        {
            _db.ExecuteNonQuery(@"
UPDATE AccPeriod SET Year=@y, Month=@m, MonthTo=@mt, Title=@t, StartDate=@s, EndDate=@e, OpeningBalance=@o
WHERE PeriodID=@id AND Status='باز'",
                P("@y", year), P("@m", monthFrom), P("@mt", monthTo), P("@t", title), P("@s", start), P("@e", end),
                P("@o", opening), P("@id", id));
        }

        public void SetPeriodStatus(int id, string status)
        {
            _db.ExecuteNonQuery("UPDATE AccPeriod SET Status=@st WHERE PeriodID=@id", P("@st", status), P("@id", id));
        }

        public bool IsPeriodOpen(int id)
        {
            object v = _db.ExecuteScalar("SELECT Status FROM AccPeriod WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", id), P("@cid", Cid));
            return v != null && v.ToString() == "باز";
        }

        public double GetPeriodOpening(int id)
        {
            object v = _db.ExecuteScalar("SELECT OpeningBalance FROM AccPeriod WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", id), P("@cid", Cid));
            return v == null || v == DBNull.Value ? 0 : Convert.ToDouble(v);
        }

        // مانده پایان دوره = مانده ابتدا + دریافت‌ها − پرداخت‌ها − شهریه − حقوق − اقلام هزینه
        // آموزش — رفع نشت چندمرکزی: قبلاً این جمع‌ها فقط با PeriodID فیلتر
        // می‌شدند، بدون فیلتر مرکز؛ یعنی اگر تراکنش/شهریه/حقوق/هزینه‌ای از مرکز
        // دیگر به همین PeriodID وصل بود، در مانده کاربرِ مرکز دیگر هم حساب
        // می‌شد. مطابق همان اصل «هیچ گزارشی نباید خارج از CenterID فعال کاربر
        // داده ببیند» که در بخش مدیریت پرونده رعایت شده، اینجا هم @cid اضافه شد.
        public double GetPeriodClosing(int id)
        {
            double opening = GetPeriodOpening(id);
            double income = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE PeriodID=@id AND Direction='دریافت' AND (@cid = 0 OR CenterID = @cid)", P("@id", id), P("@cid", Cid)));
            double payments = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE PeriodID=@id AND Direction='پرداخت' AND (@cid = 0 OR CenterID = @cid)", P("@id", id), P("@cid", Cid)));
            double stipend = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(TotalPaid),0) FROM AccStipend WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", id), P("@cid", Cid)));
            double salary = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccSalary WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", id), P("@cid", Cid)));
            double items = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Price),0) FROM AccExpenseItem WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", id), P("@cid", Cid)));
            return opening + income - payments - stipend - salary - items;
        }

        // ═══════════════════════════════════════════════════════════════════
        // صندوق
        // ═══════════════════════════════════════════════════════════════════
        public DataTable GetFunds()
        {
            return _db.Query(@"
SELECT FundID, Name AS [نام صندوق], FundType AS [نوع], OpeningBalance AS [مانده اولیه],
       CASE IsActive WHEN 1 THEN 'فعال' ELSE 'غیرفعال' END AS [وضعیت]
FROM AccFund
WHERE (@cid = 0 OR CenterID = @cid)
ORDER BY FundID", P("@cid", Cid));
        }

        public DataTable GetFundsForCombo()
        {
            return _db.Query(@"
SELECT FundID, Name AS Display FROM AccFund
WHERE IsActive=1 AND (@cid = 0 OR CenterID = @cid)
ORDER BY FundID", P("@cid", Cid));
        }

        public void AddFund(string name, string type, double opening)
        {
            _db.ExecuteNonQuery("INSERT INTO AccFund (Name, FundType, OpeningBalance, CenterID) VALUES (@n,@t,@o,@cid)",
                P("@n", name), P("@t", type), P("@o", opening), P("@cid", CurrentCid));
        }

        public void UpdateFund(int id, string name, string type, double opening)
        {
            _db.ExecuteNonQuery("UPDATE AccFund SET Name=@n, FundType=@t, OpeningBalance=@o WHERE FundID=@id",
                P("@n", name), P("@t", type), P("@o", opening), P("@id", id));
        }

        public void ToggleFund(int id)
        {
            _db.ExecuteNonQuery("UPDATE AccFund SET IsActive = CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE FundID=@id", P("@id", id));
        }

        // مانده صندوق = مانده اولیه + دریافت‌ها − پرداخت‌ها − شهریه − حقوق − هزینه‌های همین صندوق
        // آموزش — رفع باگ جدی حسابداری (یافته‌ی حسابرسی): قبلاً این متد فقط
        // AccTransaction را می‌دید. چون شهریه/حقوق/هزینه به هیچ صندوقی وصل
        // نبودند، «مانده صندوق» نمایش‌داده‌شده هرگز با «مانده کل دوره»
        // (GetPeriodClosing که همه‌ی این‌ها را کم می‌کند) هم‌خوان نبود — یعنی
        // دفتر صندوق مبلغی بیشتر از واقعیت نشان می‌داد. حالا با FundID جدید
        // روی این سه جدول، مانده‌ی هر صندوق دقیقاً با مانده‌ی کل تطبیق می‌کند.
        public double GetFundBalance(int fundId)
        {
            double opening = ToDouble(_db.ExecuteScalar("SELECT OpeningBalance FROM AccFund WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", fundId), P("@cid", Cid)));
            double income = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE FundID=@id AND Direction='دریافت' AND (@cid = 0 OR CenterID = @cid)", P("@id", fundId), P("@cid", Cid)));
            double payments = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE FundID=@id AND Direction='پرداخت' AND (@cid = 0 OR CenterID = @cid)", P("@id", fundId), P("@cid", Cid)));
            double stipend = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(TotalPaid),0) FROM AccStipend WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", fundId), P("@cid", Cid)));
            double salary = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccSalary WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", fundId), P("@cid", Cid)));
            double expense = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Price),0) FROM AccExpenseItem WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", fundId), P("@cid", Cid)));
            return opening + income - payments - stipend - salary - expense;
        }

        // ═══════════════════════════════════════════════════════════════════
        // طرف حساب
        // ═══════════════════════════════════════════════════════════════════
        public DataTable GetParties()
        {
            return _db.Query(@"
SELECT PartyID, Name AS [نام طرف حساب], PartyType AS [نوع], Phone AS [تماس], Note AS [توضیح],
       CASE IsActive WHEN 1 THEN 'فعال' ELSE 'غیرفعال' END AS [وضعیت]
FROM AccParty
WHERE (@cid = 0 OR CenterID = @cid)
ORDER BY PartyID DESC", P("@cid", Cid));
        }

        public DataTable GetPartiesForCombo()
        {
            return _db.Query(@"
SELECT PartyID, Name AS Display FROM AccParty
WHERE IsActive=1 AND (@cid = 0 OR CenterID = @cid)
ORDER BY Name", P("@cid", Cid));
        }

        public void AddParty(string name, string type, string phone, string note)
        {
            _db.ExecuteNonQuery("INSERT INTO AccParty (Name, PartyType, Phone, Note, CenterID) VALUES (@n,@t,@p,@no,@cid)",
                P("@n", name), P("@t", type), P("@p", phone), P("@no", note), P("@cid", CurrentCid));
        }

        public void UpdateParty(int id, string name, string type, string phone, string note)
        {
            _db.ExecuteNonQuery("UPDATE AccParty SET Name=@n, PartyType=@t, Phone=@p, Note=@no WHERE PartyID=@id",
                P("@n", name), P("@t", type), P("@p", phone), P("@no", note), P("@id", id));
        }

        public void ToggleParty(int id)
        {
            _db.ExecuteNonQuery("UPDATE AccParty SET IsActive = CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE PartyID=@id", P("@id", id));
        }

        // ═══════════════════════════════════════════════════════════════════
        // دسته‌بندی درآمد / هزینه
        // ═══════════════════════════════════════════════════════════════════
        public DataTable GetCategories(bool income)
        {
            string table = income ? "AccIncomeCategory" : "AccExpenseCategory";
            return _db.Query("SELECT CatID, Name AS [عنوان], CASE IsActive WHEN 1 THEN 'فعال' ELSE 'غیرفعال' END AS [وضعیت] FROM " + table + " ORDER BY SortOrder, Name");
        }

        public DataTable GetCategoriesForCombo(bool income)
        {
            string table = income ? "AccIncomeCategory" : "AccExpenseCategory";
            return _db.Query("SELECT CatID, Name AS Display FROM " + table + " WHERE IsActive=1 ORDER BY SortOrder, Name");
        }

        public void AddCategory(bool income, string name)
        {
            string table = income ? "AccIncomeCategory" : "AccExpenseCategory";
            _db.ExecuteNonQuery("INSERT OR IGNORE INTO " + table + " (Name) VALUES (@n)", P("@n", name));
        }

        public void UpdateCategory(bool income, int id, string name)
        {
            string table = income ? "AccIncomeCategory" : "AccExpenseCategory";
            _db.ExecuteNonQuery("UPDATE " + table + " SET Name=@n WHERE CatID=@id", P("@n", name), P("@id", id));
        }

        public void ToggleCategory(bool income, int id)
        {
            string table = income ? "AccIncomeCategory" : "AccExpenseCategory";
            _db.ExecuteNonQuery("UPDATE " + table + " SET IsActive = CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE CatID=@id", P("@id", id));
        }

        // ═══════════════════════════════════════════════════════════════════
        // تراکنش (دریافت/پرداخت)
        // ═══════════════════════════════════════════════════════════════════
        // شماره سند خودکار بعدی — مسلسل و بزرگ‌ترینِ موجود + ۱.
        // آموزش — رفع درخواست جدی کاربر: شماره سند باید با تغییر دوره مالی
        // «ریستارت» شود (هر دوره از شماره ۱ شروع کند)، نه این‌که برای همیشه
        // مسلسل بماند. پس MAX حالا به‌ازای همان PeriodID محاسبه می‌شود، نه کل
        // تاریخچه‌ی مرکز. اگر هنوز دوره‌ای انتخاب نشده (periodId=null)، محاسبه
        // بدون فیلتر دوره انجام می‌شود (فقط برای نمایش اولیه‌ی فرم، قبل از
        // انتخاب دوره؛ به‌محض انتخاب دوره در UI دوباره محاسبه می‌شود).
        public int NextDocNoInt(int? periodId)
        {
            object v = _db.ExecuteScalar(@"
SELECT COALESCE(MAX(CAST(CASE WHEN DocNo GLOB '*[0-9]*' AND DocNo NOT GLOB '*[^0-9]*' THEN DocNo ELSE '0' END AS INTEGER)),0)+1
FROM AccTransaction WHERE (@cid = 0 OR CenterID = @cid) AND (@per IS NULL OR PeriodID = @per)",
                P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
            return v == null || v == DBNull.Value ? 1 : Convert.ToInt32(v);
        }

        public string NextDocNo(int? periodId)
        {
            return NextDocNoInt(periodId).ToString();
        }

        // آیا این شماره سند در همین دوره مالی قبلاً استفاده شده؟ (برای جلوگیری
        // از تداخل هنگام ثبت هم‌زمان). چون شماره سند اکنون به‌ازای هر دوره از
        // نو شروع می‌شود، تکرار همان عدد در دوره‌های دیگر طبیعی و مجاز است؛
        // فقط یکتایی «در همان دوره» بررسی می‌شود.
        public bool DocNoExists(string docNo, int? periodId)
        {
            object v = _db.ExecuteScalar(
                "SELECT COUNT(1) FROM AccTransaction WHERE DocNo=@d AND (@cid = 0 OR CenterID = @cid) AND (@per IS NULL OR PeriodID = @per)",
                P("@d", docNo), P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
            return Convert.ToInt32(v) > 0;
        }

        // یک تراکنش کامل برای ساخت فاکتور/سند چاپی
        public DataRow GetTransactionById(int txnId)
        {
            DataTable dt = _db.Query(@"
SELECT t.TxnID, t.DocNo, t.TxnDate, t.Direction, t.Amount, t.Qty, t.DollarAmount, t.DollarRate,
       t.Description, t.CreatedBy, t.CreatedAt,
       p.Name AS PartyName, f.Name AS FundName,
       CASE WHEN t.CategoryType='Income' THEN ic.Name ELSE ec.Name END AS CategoryName,
       COALESCE(pr.Title, ('برج ' || pr.Month || ' سال ' || pr.Year)) AS PeriodTitle
FROM AccTransaction t
LEFT JOIN AccParty p ON p.PartyID = t.PartyID
LEFT JOIN AccFund f ON f.FundID = t.FundID
LEFT JOIN AccIncomeCategory ic ON ic.CatID = t.CategoryID AND t.CategoryType='Income'
LEFT JOIN AccExpenseCategory ec ON ec.CatID = t.CategoryID AND t.CategoryType='Expense'
LEFT JOIN AccPeriod pr ON pr.PeriodID = t.PeriodID
WHERE t.TxnID = @id AND (@cid = 0 OR t.CenterID = @cid)", P("@id", txnId), P("@cid", Cid));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public int AddTransaction(string docNo, string date, string direction, int? periodId, int? partyId,
            int? fundId, string categoryType, int? categoryId, double amount, string qty,
            double? dollarAmount, double? dollarRate, string description, string attachment)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO AccTransaction
    (DocNo, TxnDate, Direction, PeriodID, PartyID, FundID, CategoryType, CategoryID,
     Amount, Qty, DollarAmount, DollarRate, Description, AttachmentPath, CenterID, CreatedBy)
VALUES
    (@doc, @date, @dir, @per, @party, @fund, @ctype, @cat,
     @amt, @qty, @damt, @drate, @desc, @att, @cid, @by)",
                P("@doc", docNo), P("@date", date), P("@dir", direction),
                P("@per", (object)periodId ?? DBNull.Value), P("@party", (object)partyId ?? DBNull.Value),
                P("@fund", (object)fundId ?? DBNull.Value), P("@ctype", categoryType),
                P("@cat", (object)categoryId ?? DBNull.Value), P("@amt", amount), P("@qty", qty),
                P("@damt", (object)dollarAmount ?? DBNull.Value), P("@drate", (object)dollarRate ?? DBNull.Value),
                P("@desc", description), P("@att", attachment), P("@cid", CurrentCid), P("@by", SecurityContext.Username));
            int id = Convert.ToInt32(_db.ExecuteScalar("SELECT last_insert_rowid()"));
            AccAudit.Log(direction == "دریافت" ? "ثبت دریافت" : "ثبت پرداخت", "AccTransaction", id, docNo + " / " + amount.ToString("N0"));
            return id;
        }

        public void DeleteTransaction(int id)
        {
            _db.ExecuteNonQuery("DELETE FROM AccTransaction WHERE TxnID=@id", P("@id", id));
            AccAudit.Log("حذف تراکنش", "AccTransaction", id, "");
        }

        // دفتر صندوق: تمام تراکنش‌ها (اختیاری فیلتر دوره/صندوق)
        public DataTable GetTransactions(int? periodId, int? fundId)
        {
            return _db.Query(@"
SELECT t.TxnID, t.DocNo AS [شماره سند], t.TxnDate AS [تاریخ], t.Direction AS [نوع],
       p.Name AS [طرف حساب], f.Name AS [صندوق],
       CASE WHEN t.CategoryType='Income' THEN ic.Name ELSE ec.Name END AS [دسته‌بندی],
       t.Amount AS [مبلغ], t.Description AS [توضیح]
FROM AccTransaction t
LEFT JOIN AccParty p ON p.PartyID = t.PartyID
LEFT JOIN AccFund f ON f.FundID = t.FundID
LEFT JOIN AccIncomeCategory ic ON ic.CatID = t.CategoryID AND t.CategoryType='Income'
LEFT JOIN AccExpenseCategory ec ON ec.CatID = t.CategoryID AND t.CategoryType='Expense'
WHERE (@cid = 0 OR t.CenterID = @cid)
  AND (@per IS NULL OR t.PeriodID = @per)
  AND (@fund IS NULL OR t.FundID = @fund)
ORDER BY t.TxnID DESC",
                P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value), P("@fund", (object)fundId ?? DBNull.Value));
        }

        private static double ToDouble(object v)
        {
            return v == null || v == DBNull.Value ? 0 : Convert.ToDouble(v);
        }

        // ═══════════════════════════════════════════════════════════════════
        // شهریه ایتام (مطابق شیت «فرمت جزیی»): تفکیک سادات/عام/اهل‌سنت × چندنفره
        // ═══════════════════════════════════════════════════════════════════
        public DataTable GetStipends(int? periodId)
        {
            return _db.Query(@"
SELECT s.StipendID, s.PeriodID, s.Province AS [ولایت], s.District AS [ولسوالی], s.Center AS [مرکز],
       s.SadatType AS [نوع], s.FamilySize AS [چند نفره], s.FamilyCount AS [تعداد خانوار],
       s.OrphanCount AS [تعداد یتیم], s.AmountPerFamily AS [مبلغ شهریه], s.TotalPaid AS [جمع پرداختی],
       s.FundID, f.Name AS [صندوق]
FROM AccStipend s
LEFT JOIN AccFund f ON f.FundID = s.FundID
WHERE (@cid = 0 OR s.CenterID = @cid) AND (@per IS NULL OR s.PeriodID = @per)
ORDER BY s.SadatType, s.FamilySize",
                P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
        }

        public int AddStipend(int? periodId, string province, string district, string center, string sadatType,
            int familySize, int familyCount, int orphanCount, double amountPerFamily, int? fundId)
        {
            double total = familyCount * amountPerFamily;
            _db.ExecuteNonQuery(@"
INSERT INTO AccStipend (PeriodID, Province, District, Center, SadatType, FamilySize, FamilyCount, OrphanCount, AmountPerFamily, TotalPaid, FundID, CenterID)
VALUES (@per,@prov,@dist,@cen,@sadat,@size,@fc,@oc,@amt,@tot,@fund,@cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@prov", province), P("@dist", district), P("@cen", center),
                P("@sadat", sadatType), P("@size", familySize), P("@fc", familyCount), P("@oc", orphanCount),
                P("@amt", amountPerFamily), P("@tot", total), P("@fund", (object)fundId ?? DBNull.Value), P("@cid", CurrentCid));
            int id = Convert.ToInt32(_db.ExecuteScalar("SELECT last_insert_rowid()"));
            AccAudit.Log("ثبت شهریه", "AccStipend", id, sadatType + " / " + familySize + "نفره / " + total.ToString("N0"));
            return id;
        }

        public void UpdateStipend(int id, string province, string district, string center, string sadatType,
            int familySize, int familyCount, int orphanCount, double amountPerFamily, int? fundId)
        {
            double total = familyCount * amountPerFamily;
            _db.ExecuteNonQuery(@"
UPDATE AccStipend SET Province=@prov, District=@dist, Center=@cen, SadatType=@sadat, FamilySize=@size,
       FamilyCount=@fc, OrphanCount=@oc, AmountPerFamily=@amt, TotalPaid=@tot, FundID=@fund
WHERE StipendID=@id",
                P("@prov", province), P("@dist", district), P("@cen", center), P("@sadat", sadatType),
                P("@size", familySize), P("@fc", familyCount), P("@oc", orphanCount), P("@amt", amountPerFamily),
                P("@tot", total), P("@fund", (object)fundId ?? DBNull.Value), P("@id", id));
        }

        public void DeleteStipend(int id)
        {
            _db.ExecuteNonQuery("DELETE FROM AccStipend WHERE StipendID=@id", P("@id", id));
            AccAudit.Log("حذف شهریه", "AccStipend", id, "");
        }

        // یک ردیف شهریه کامل برای ساخت رسید/فاکتور چاپی
        public DataRow GetStipendById(int id)
        {
            DataTable dt = _db.Query(@"
SELECT s.StipendID, s.Province, s.District, s.Center, s.SadatType, s.FamilySize,
       s.FamilyCount, s.OrphanCount, s.AmountPerFamily, s.TotalPaid, s.CreatedAt,
       COALESCE(p.Title, ('برج ' || p.Month || ' سال ' || p.Year)) AS PeriodTitle
FROM AccStipend s
LEFT JOIN AccPeriod p ON p.PeriodID = s.PeriodID
WHERE s.StipendID = @id AND (@cid = 0 OR s.CenterID = @cid)", P("@id", id), P("@cid", Cid));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        // ═══════════════════════════════════════════════════════════════════
        // حقوق کارکنان
        // ═══════════════════════════════════════════════════════════════════
        public DataTable GetSalaries(int? periodId)
        {
            return _db.Query(@"
SELECT s.SalaryID, s.PeriodID, s.EmployeeName AS [نام], s.Position AS [سمت], s.Amount AS [مبلغ], s.Note AS [توضیح],
       s.FundID, f.Name AS [صندوق]
FROM AccSalary s
LEFT JOIN AccFund f ON f.FundID = s.FundID
WHERE (@cid = 0 OR s.CenterID = @cid) AND (@per IS NULL OR s.PeriodID = @per)
ORDER BY s.EmployeeName",
                P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
        }

        public int AddSalary(int? periodId, string name, string position, double amount, string note, int? fundId)
        {
            _db.ExecuteNonQuery("INSERT INTO AccSalary (PeriodID, EmployeeName, Position, Amount, Note, FundID, CenterID) VALUES (@per,@n,@p,@a,@note,@fund,@cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@n", name), P("@p", position), P("@a", amount), P("@note", note),
                P("@fund", (object)fundId ?? DBNull.Value), P("@cid", CurrentCid));
            int id = Convert.ToInt32(_db.ExecuteScalar("SELECT last_insert_rowid()"));
            AccAudit.Log("ثبت حقوق", "AccSalary", id, name + " / " + amount.ToString("N0"));
            return id;
        }

        public void UpdateSalary(int id, string name, string position, double amount, string note, int? fundId)
        {
            _db.ExecuteNonQuery("UPDATE AccSalary SET EmployeeName=@n, Position=@p, Amount=@a, Note=@note, FundID=@fund WHERE SalaryID=@id",
                P("@n", name), P("@p", position), P("@a", amount), P("@note", note), P("@fund", (object)fundId ?? DBNull.Value), P("@id", id));
        }

        public void DeleteSalary(int id)
        {
            _db.ExecuteNonQuery("DELETE FROM AccSalary WHERE SalaryID=@id", P("@id", id));
            AccAudit.Log("حذف حقوق", "AccSalary", id, "");
        }

        // یک ردیف حقوق کامل برای ساخت فیش حقوقی چاپی
        public DataRow GetSalaryById(int id)
        {
            DataTable dt = _db.Query(@"
SELECT s.SalaryID, s.EmployeeName, s.Position, s.Amount, s.Note, s.CreatedAt,
       COALESCE(p.Title, ('برج ' || p.Month || ' سال ' || p.Year)) AS PeriodTitle
FROM AccSalary s
LEFT JOIN AccPeriod p ON p.PeriodID = s.PeriodID
WHERE s.SalaryID = @id AND (@cid = 0 OR s.CenterID = @cid)", P("@id", id), P("@cid", Cid));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        // ═══════════════════════════════════════════════════════════════════
        // هزینه‌های جاری (اقلام) — مطابق شیت «حساب جاری»
        // ═══════════════════════════════════════════════════════════════════
        public DataTable GetExpenseItems(int? periodId)
        {
            return _db.Query(@"
SELECT e.ItemID, e.PeriodID, ec.Name AS [دسته‌بندی], e.Description AS [شرح], e.Qty AS [تعداد/مقدار],
       e.Price AS [قیمت], e.DocNo AS [شماره سند], e.ItemDate AS [تاریخ], e.FundID, f.Name AS [صندوق]
FROM AccExpenseItem e
LEFT JOIN AccExpenseCategory ec ON ec.CatID = e.CategoryID
LEFT JOIN AccFund f ON f.FundID = e.FundID
WHERE (@cid = 0 OR e.CenterID = @cid) AND (@per IS NULL OR e.PeriodID = @per)
ORDER BY e.ItemID",
                P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
        }

        public int AddExpenseItem(int? periodId, int? categoryId, string categoryName, string description, string qty, double price, string docNo, string itemDate, int? fundId)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO AccExpenseItem (PeriodID, CategoryID, CategoryName, Description, Qty, Price, DocNo, ItemDate, FundID, CenterID)
VALUES (@per,@cat,@catn,@desc,@qty,@price,@doc,@date,@fund,@cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@cat", (object)categoryId ?? DBNull.Value), P("@catn", categoryName),
                P("@desc", description), P("@qty", qty), P("@price", price), P("@doc", docNo), P("@date", itemDate),
                P("@fund", (object)fundId ?? DBNull.Value), P("@cid", CurrentCid));
            int id = Convert.ToInt32(_db.ExecuteScalar("SELECT last_insert_rowid()"));
            AccAudit.Log("ثبت هزینه جاری", "AccExpenseItem", id, description + " / " + price.ToString("N0"));
            return id;
        }

        public void UpdateExpenseItem(int id, int? categoryId, string categoryName, string description, string qty, double price, string docNo, string itemDate, int? fundId)
        {
            _db.ExecuteNonQuery(@"
UPDATE AccExpenseItem SET CategoryID=@cat, CategoryName=@catn, Description=@desc, Qty=@qty, Price=@price, DocNo=@doc, ItemDate=@date, FundID=@fund
WHERE ItemID=@id",
                P("@cat", (object)categoryId ?? DBNull.Value), P("@catn", categoryName), P("@desc", description),
                P("@qty", qty), P("@price", price), P("@doc", docNo), P("@date", itemDate), P("@fund", (object)fundId ?? DBNull.Value), P("@id", id));
        }

        public void DeleteExpenseItem(int id)
        {
            _db.ExecuteNonQuery("DELETE FROM AccExpenseItem WHERE ItemID=@id", P("@id", id));
            AccAudit.Log("حذف هزینه جاری", "AccExpenseItem", id, "");
        }

        // یک ردیف هزینه جاری کامل برای ساخت سند هزینه چاپی
        public DataRow GetExpenseItemById(int id)
        {
            DataTable dt = _db.Query(@"
SELECT e.ItemID, e.CategoryName, e.Description, e.Qty, e.Price, e.DocNo, e.ItemDate, e.CreatedAt,
       COALESCE(p.Title, ('برج ' || p.Month || ' سال ' || p.Year)) AS PeriodTitle
FROM AccExpenseItem e
LEFT JOIN AccPeriod p ON p.PeriodID = e.PeriodID
WHERE e.ItemID = @id AND (@cid = 0 OR e.CenterID = @cid)", P("@id", id), P("@cid", Cid));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        // ═══════════════════════════════════════════════════════════════════
        // پشتیبانی گزارش‌ها — جمع‌های دوره برای صورت حساب کلی/جزیی
        // ═══════════════════════════════════════════════════════════════════
        public double SumStipend(int? periodId, string sadatType)
        {
            return ToDouble(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(TotalPaid),0) FROM AccStipend WHERE (@per IS NULL OR PeriodID=@per) AND (@st IS NULL OR SadatType=@st) AND (@cid = 0 OR CenterID = @cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@st", (object)sadatType ?? DBNull.Value), P("@cid", Cid)));
        }

        // آموزش — رفع باگ نشتِ چندمرکزی (یافته‌ی حسابرسی): بر خلاف
        // SumStipend/SumExpenseItems/SumTransactions، این متد فیلتر CenterID
        // نداشت؛ یعنی «صورت حساب کلی» و «صورت حساب دریافت بودجه» یک مرکز،
        // حقوق کارکنانِ مراکز دیگر را هم در همان دوره جمع می‌زد و مانده‌ی
        // گزارش‌شده را اشتباه نشان می‌داد. حالا مثل بقیه فیلتر مرکز دارد.
        public double SumSalary(int? periodId)
        {
            return ToDouble(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(Amount),0) FROM AccSalary WHERE (@per IS NULL OR PeriodID=@per) AND (@cid = 0 OR CenterID = @cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@cid", Cid)));
        }

        public double SumExpenseItems(int? periodId)
        {
            return ToDouble(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(Price),0) FROM AccExpenseItem WHERE (@per IS NULL OR PeriodID=@per) AND (@cid = 0 OR CenterID = @cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@cid", Cid)));
        }

        public double SumExpenseByCategory(int? periodId, string categoryName)
        {
            return ToDouble(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(Price),0) FROM AccExpenseItem WHERE (@per IS NULL OR PeriodID=@per) AND CategoryName=@cn AND (@cid = 0 OR CenterID = @cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@cn", categoryName), P("@cid", Cid)));
        }

        public double SumTransactions(int? periodId, string direction, string categoryType)
        {
            return ToDouble(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE (@per IS NULL OR PeriodID=@per) AND Direction=@dir AND (@ct IS NULL OR CategoryType=@ct) AND (@cid = 0 OR CenterID = @cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@dir", direction), P("@ct", (object)categoryType ?? DBNull.Value), P("@cid", Cid)));
        }

        public DataTable GetTransactionsRaw(int? periodId, int? partyId, int? fundId)
        {
            return _db.Query(@"
SELECT t.TxnID, t.DocNo, t.TxnDate, t.Direction, t.Amount, t.Description,
       p.Name AS PartyName, f.Name AS FundName,
       CASE WHEN t.CategoryType='Income' THEN ic.Name ELSE ec.Name END AS CategoryName,
       t.DollarAmount, t.DollarRate
FROM AccTransaction t
LEFT JOIN AccParty p ON p.PartyID = t.PartyID
LEFT JOIN AccFund f ON f.FundID = t.FundID
LEFT JOIN AccIncomeCategory ic ON ic.CatID = t.CategoryID AND t.CategoryType='Income'
LEFT JOIN AccExpenseCategory ec ON ec.CatID = t.CategoryID AND t.CategoryType='Expense'
WHERE (@cid = 0 OR t.CenterID = @cid)
  AND (@per IS NULL OR t.PeriodID = @per)
  AND (@party IS NULL OR t.PartyID = @party)
  AND (@fund IS NULL OR t.FundID = @fund)
ORDER BY t.TxnDate, t.TxnID",
                P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value),
                P("@party", (object)partyId ?? DBNull.Value), P("@fund", (object)fundId ?? DBNull.Value));
        }

        public string GetPeriodTitle(int periodId)
        {
            object v = _db.ExecuteScalar("SELECT COALESCE(Title, ('برج ' || Month || ' سال ' || Year)) FROM AccPeriod WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", periodId), P("@cid", Cid));
            return v == null ? "" : v.ToString();
        }

        // ═══════════════════════════════════════════════════════════════════
        // تنظیمات گزارش (سربرگ/پاورقی/امضاها/مهر)
        // ═══════════════════════════════════════════════════════════════════
        public string GetSetting(string key)
        {
            object v = _db.ExecuteScalar("SELECT SettingValue FROM AccSettings WHERE SettingKey=@k", P("@k", key));
            return v == null || v == DBNull.Value ? "" : v.ToString();
        }

        public void SetSetting(string key, string value)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO AccSettings (SettingKey, SettingValue, UpdatedAt) VALUES (@k, @v, datetime('now'))
ON CONFLICT(SettingKey) DO UPDATE SET SettingValue=@v, UpdatedAt=datetime('now')",
                P("@k", key), P("@v", value ?? ""));
        }

        // ═══════════════════════════════════════════════════════════════════
        // پشتیبانی گزارش ۶/۷: دفتر صندوق و دفتر طرف حساب (مانده تجمعی)
        // ═══════════════════════════════════════════════════════════════════
        public string GetFundName(int fundId)
        {
            object v = _db.ExecuteScalar("SELECT Name FROM AccFund WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", fundId), P("@cid", Cid));
            return v == null ? "" : v.ToString();
        }

        public double GetFundOpening(int fundId)
        {
            return ToDouble(_db.ExecuteScalar("SELECT OpeningBalance FROM AccFund WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", fundId), P("@cid", Cid)));
        }

        public string GetPartyName(int partyId)
        {
            object v = _db.ExecuteScalar("SELECT Name FROM AccParty WHERE PartyID=@id AND (@cid = 0 OR CenterID = @cid)", P("@id", partyId), P("@cid", Cid));
            return v == null ? "" : v.ToString();
        }

        // آموزش — این سه متد برای «دفتر صندوق» (گزارش ۶) لازم شدند تا مانده‌ی
        // نهایی گزارش با GetFundBalance (که حالا این سه را هم کم می‌کند)
        // هم‌خوان بماند؛ بدون این‌ها گزارش عددی متفاوت و اشتباه نشان می‌داد.
        public DataTable GetStipendsByFund(int fundId)
        {
            return _db.Query(@"
SELECT SadatType, FamilySize, TotalPaid FROM AccStipend
WHERE FundID=@fund AND (@cid = 0 OR CenterID = @cid)", P("@fund", fundId), P("@cid", Cid));
        }

        public DataTable GetSalariesByFund(int fundId)
        {
            return _db.Query(@"
SELECT EmployeeName, Amount FROM AccSalary
WHERE FundID=@fund AND (@cid = 0 OR CenterID = @cid)", P("@fund", fundId), P("@cid", Cid));
        }

        public DataTable GetExpenseItemsByFund(int fundId)
        {
            return _db.Query(@"
SELECT Description, ItemDate, Price FROM AccExpenseItem
WHERE FundID=@fund AND (@cid = 0 OR CenterID = @cid)", P("@fund", fundId), P("@cid", Cid));
        }

        // تمام تراکنش‌های یک صندوق به ترتیب ثبت (برای محاسبه مانده تجمعی صحیح)
        public DataTable GetFundTransactionsChronological(int fundId)
        {
            return _db.Query(@"
SELECT t.TxnID, t.DocNo, t.TxnDate, t.Direction, t.Amount, t.PeriodID,
       p.Name AS PartyName, t.Description
FROM AccTransaction t
LEFT JOIN AccParty p ON p.PartyID = t.PartyID
WHERE t.FundID = @fund AND (@cid = 0 OR t.CenterID = @cid)
ORDER BY t.TxnID", P("@fund", fundId), P("@cid", Cid));
        }

        // تمام تراکنش‌های یک طرف حساب به ترتیب ثبت
        public DataTable GetPartyTransactionsChronological(int partyId)
        {
            return _db.Query(@"
SELECT t.TxnID, t.DocNo, t.TxnDate, t.Direction, t.Amount, t.Description,
       f.Name AS FundName
FROM AccTransaction t
LEFT JOIN AccFund f ON f.FundID = t.FundID
WHERE t.PartyID = @party AND (@cid = 0 OR t.CenterID = @cid)
ORDER BY t.TxnID", P("@party", partyId), P("@cid", Cid));
        }

        public DataTable GetExpenseCategorySummary(int? periodId)
        {
            return _db.Query(@"
SELECT COALESCE(ec.Name,'سایر') AS [عنوان], COALESCE(SUM(e.Price),0) AS [مبلغ]
FROM AccExpenseItem e
LEFT JOIN AccExpenseCategory ec ON ec.CatID = e.CategoryID
WHERE (@per IS NULL OR e.PeriodID = @per) AND (@cid = 0 OR e.CenterID = @cid)
GROUP BY COALESCE(ec.Name,'سایر')
ORDER BY [مبلغ] DESC",
                P("@per", (object)periodId ?? DBNull.Value), P("@cid", Cid));
        }
    }
}

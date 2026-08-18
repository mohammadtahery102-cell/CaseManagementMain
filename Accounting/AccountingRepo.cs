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
            // آموزش — ExecuteInsertReturningId جایگزین الگوی قبلیِ
            // «ExecuteNonQuery سپس ExecuteScalar(last_insert_rowid)» شد، چون آن
            // الگو روی دو کانکشن جدا اجرا می‌شد و همیشه صفر برمی‌گرداند.
            // توضیح کامل در DAL/DatabaseHelper.ExecuteInsertReturningId.
            int id = (int)_db.ExecuteInsertReturningId(@"
INSERT INTO AccPeriod (Year, Month, MonthTo, Title, StartDate, EndDate, OpeningBalance, Status, CenterID, CreatedBy)
VALUES (@y, @m, @mt, @t, @s, @e, @o, 'باز', @cid, @by)",
                P("@y", year), P("@m", monthFrom), P("@mt", monthTo), P("@t", title), P("@s", start), P("@e", end),
                P("@o", opening), P("@cid", CurrentCid), P("@by", SecurityContext.Username));
            AccAudit.Log("ثبت دوره مالی", "AccPeriod", id, title + " / مانده ابتدا " + opening.ToString("N0"));
            return id;
        }

        // آموزش — «AND (@cid = 0 OR CenterID = @cid)» به همه‌ی دستورهای نوشتن
        // اضافه شد. قبلاً فقط SELECTها فیلتر مرکز داشتند و UPDATE/DELETEها
        // نداشتند؛ یعنی خواندن محدود به مرکز کاربر بود ولی نوشتن نه.
        public void UpdatePeriod(int id, int year, int monthFrom, int monthTo, string title, string start, string end, double opening)
        {
            double oldOpening = GetPeriodOpening(id);

            int affected = _db.ExecuteNonQuery(@"
UPDATE AccPeriod SET Year=@y, Month=@m, MonthTo=@mt, Title=@t, StartDate=@s, EndDate=@e, OpeningBalance=@o
WHERE PeriodID=@id AND Status='باز' AND (@cid = 0 OR CenterID = @cid)",
                P("@y", year), P("@m", monthFrom), P("@mt", monthTo), P("@t", title), P("@s", start), P("@e", end),
                P("@o", opening), P("@id", id), P("@cid", Cid));

            if (affected == 0)
                throw new AccountingRuleException("این دوره مالی قابل ویرایش نیست — یا «بسته» شده یا متعلق به مرکز دیگری است.");

            AccAudit.LogChange("ویرایش دوره مالی", "AccPeriod", id,
                "مانده ابتدا " + oldOpening.ToString("N0"), title + " / مانده ابتدا " + opening.ToString("N0"), "");
        }

        public void SetPeriodStatus(int id, string status)
        {
            string oldStatus = _db.ExecuteScalar(
                "SELECT Status FROM AccPeriod WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid)",
                P("@id", id), P("@cid", Cid)) as string;

            int affected = _db.ExecuteNonQuery("UPDATE AccPeriod SET Status=@st WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid)",
                P("@st", status), P("@id", id), P("@cid", Cid));

            if (affected == 0)
                throw new AccountingRuleException("این دوره مالی در مرکز فعال شما یافت نشد.");

            // بستن/بازکردن دوره یک رویداد مالیِ حساس است و باید ردّ حسابرسی
            // داشته باشد — قبلاً هیچ لاگی برای آن ثبت نمی‌شد.
            AccAudit.LogChange(status == "بسته" ? "بستن دوره مالی" : "بازکردن دوره مالی",
                "AccPeriod", id, oldStatus ?? "", status,
                "مانده پایان دوره: " + GetPeriodClosing(id).ToString("N0"));
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
        // آموزش — «AND COALESCE(IsReversed,0)=0» در تمام جمع‌ها اضافه شد تا
        // اسناد باطل‌شده در هیچ مانده‌ای شمرده نشوند. COALESCE لازم است چون
        // ستون تازه اضافه شده و در دیتابیس‌های قدیمی ممکن است NULL باشد.
        public double GetPeriodClosing(int id)
        {
            double opening = GetPeriodOpening(id);
            double income = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE PeriodID=@id AND Direction='دریافت' AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@id", id), P("@cid", Cid)));
            double payments = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE PeriodID=@id AND Direction='پرداخت' AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@id", id), P("@cid", Cid)));
            double stipend = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(TotalPaid),0) FROM AccStipend WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@id", id), P("@cid", Cid)));
            double salary = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccSalary WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@id", id), P("@cid", Cid)));
            double items = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Price),0) FROM AccExpenseItem WHERE PeriodID=@id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@id", id), P("@cid", Cid)));
            return Money.Round(opening + income - payments - stipend - salary - items);
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
            double oldOpening = GetFundOpening(id);

            int affected = _db.ExecuteNonQuery("UPDATE AccFund SET Name=@n, FundType=@t, OpeningBalance=@o WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid)",
                P("@n", name), P("@t", type), P("@o", opening), P("@id", id), P("@cid", Cid));

            if (affected == 0)
                throw new AccountingRuleException("این صندوق در مرکز فعال شما یافت نشد.");

            // مانده اولیه‌ی صندوق مستقیماً روی همه‌ی مانده‌ها اثر می‌گذارد، پس
            // تغییرش باید مقدار قبلی و جدید را در ردّ حسابرسی ثبت کند.
            AccAudit.LogChange("ویرایش صندوق", "AccFund", id,
                "مانده اولیه " + oldOpening.ToString("N0"), name + " / مانده اولیه " + opening.ToString("N0"), "");
        }

        public void ToggleFund(int id)
        {
            _db.ExecuteNonQuery("UPDATE AccFund SET IsActive = CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid)",
                P("@id", id), P("@cid", Cid));
            AccAudit.Log("تغییر وضعیت صندوق", "AccFund", id, "");
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
            double income = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE FundID=@id AND Direction='دریافت' AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@id", fundId), P("@cid", Cid)));
            double payments = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE FundID=@id AND Direction='پرداخت' AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@id", fundId), P("@cid", Cid)));
            double stipend = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(TotalPaid),0) FROM AccStipend WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@id", fundId), P("@cid", Cid)));
            double salary = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Amount),0) FROM AccSalary WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@id", fundId), P("@cid", Cid)));
            double expense = ToDouble(_db.ExecuteScalar("SELECT COALESCE(SUM(Price),0) FROM AccExpenseItem WHERE FundID=@id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0", P("@id", fundId), P("@cid", Cid)));
            return Money.Round(opening + income - payments - stipend - salary - expense);
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
            string oldName = GetPartyName(id);

            int affected = _db.ExecuteNonQuery("UPDATE AccParty SET Name=@n, PartyType=@t, Phone=@p, Note=@no WHERE PartyID=@id AND (@cid = 0 OR CenterID = @cid)",
                P("@n", name), P("@t", type), P("@p", phone), P("@no", note), P("@id", id), P("@cid", Cid));

            if (affected == 0)
                throw new AccountingRuleException("این طرف حساب در مرکز فعال شما یافت نشد.");

            AccAudit.LogChange("ویرایش طرف حساب", "AccParty", id, oldName, name, "");
        }

        public void ToggleParty(int id)
        {
            _db.ExecuteNonQuery("UPDATE AccParty SET IsActive = CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE PartyID=@id AND (@cid = 0 OR CenterID = @cid)",
                P("@id", id), P("@cid", Cid));
            AccAudit.Log("تغییر وضعیت طرف حساب", "AccParty", id, "");
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
        // شناسه‌های خامِ یک سند، برای پر کردنِ فرم هنگام اصلاح.
        // آموزش — چرا GetTransactionById کافی نیست: آن متد برای نمایش و چاپ
        // ساخته شده و *نامِ* طرف‌حساب/صندوق/دسته را برمی‌گرداند، نه شناسه‌شان.
        // برای انتخابِ درستِ آیتم در کمبوباکس‌ها به خودِ شناسه نیاز است؛
        // تطبیق با نام شکننده است (دو صندوق هم‌نام، تغییر نام بعدی).
        public DataRow GetTransactionForEdit(int txnId)
        {
            DataTable dt = _db.Query(@"
SELECT TxnID, DocNo, TxnDate, Direction, PeriodID, PartyID, FundID, CategoryType, CategoryID,
       Amount, Qty, DollarAmount, DollarRate, Description, AttachmentPath,
       COALESCE(IsReversed,0) AS IsReversed
FROM AccTransaction
WHERE TxnID = @id AND (@cid = 0 OR CenterID = @cid)",
                P("@id", txnId), P("@cid", Cid));

            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

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

        // نتیجه‌ی ثبت یک تراکنش — شماره سندِ نهایی ممکن است با شماره‌ی پیشنهادی
        // فرق کند (اگر هم‌زمان کاربر دیگری همان شماره را گرفته باشد).
        public class TransactionSaveResult
        {
            public int TxnId;
            public string DocNo;
            public bool DocNoReassigned;
        }

        // امضای قدیمی — حفظ شده تا هیچ فراخوانی موجودی نشکند.
        public int AddTransaction(string docNo, string date, string direction, int? periodId, int? partyId,
            int? fundId, string categoryType, int? categoryId, double amount, string qty,
            double? dollarAmount, double? dollarRate, string description, string attachment)
        {
            return AddTransactionAtomic(docNo, date, direction, periodId, partyId, fundId, categoryType,
                categoryId, amount, qty, dollarAmount, dollarRate, description, attachment, true).TxnId;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ثبت اتمیک تراکنش — کل «بررسی تکرار + گرفتن شماره سند + درج» داخل یک
        // تراکنش پایگاه‌داده.
        //
        // آموزش — چرا این تغییر لازم بود: مسیر قبلی سه گام جدا داشت که هرکدام
        // روی کانکشن خودش اجرا می‌شد:
        //     ۱) DocNoExists(docNo)      ← خواندن
        //     ۲) NextDocNo(period)       ← خواندن MAX+1
        //     ۳) AddTransaction(...)     ← نوشتن
        // بین گام ۱/۲ و گام ۳ هیچ قفلی وجود نداشت. اگر دو کاربر (یا یک کاربر
        // با دوبار کلیک سریع روی دکمه) هم‌زمان ثبت می‌کردند، هر دو همان
        // «شماره‌ی بعدی» را می‌خواندند و هر دو با همان شماره درج می‌شدند.
        // این دقیقاً همان چیزی است که در دیتابیس فعلی دیده می‌شود: تراکنش‌های
        // ۱ و ۲ با تاریخ، جهت، صندوق و مبلغ کاملاً یکسان (۱٬۳۲۲٬۰۰۰ افغانی).
        //
        // حالا هر سه گام داخل یک تراکنش با قفلِ نوشتنِ فوری (BeginImmediate)
        // انجام می‌شوند، پس دو ثبت هم‌زمان ناچار پشت‌سرهم اجرا می‌شوند و
        // دومی شماره‌ی واقعاً بعدی را می‌گیرد.
        //
        // confirmedDuplicate: اگر کاربر آگاهانه تأیید کرده باشد که این پرداختِ
        // تکراری واقعی است (مثلاً دو پرداخت جداگانه با مبلغ یکسان در یک روز)،
        // ثبت انجام می‌شود؛ در غیر این صورت با خطای قابل‌فهم متوقف می‌شود.
        // ─────────────────────────────────────────────────────────────────────
        public TransactionSaveResult AddTransactionAtomic(string docNo, string date, string direction,
            int? periodId, int? partyId, int? fundId, string categoryType, int? categoryId,
            double amount, string qty, double? dollarAmount, double? dollarRate,
            string description, string attachment, bool confirmedDuplicate)
        {
            if (!Money.IsValidPositive(amount))
                throw new AccountingRuleException("مبلغ تراکنش باید عددی بزرگ‌تر از صفر و حداکثر " +
                                                  Money.MaxAmount.ToString("N0") + " افغانی باشد.");

            if (!Money.IsConversionConsistent(amount, dollarAmount ?? 0, dollarRate ?? 0))
                throw new AccountingRuleException(
                    "مبلغ افغانی با مبلغ دلاری و نرخ هم‌خوان نیست.\n" +
                    "مبلغ دلاری × نرخ = " + Money.Convert(dollarAmount ?? 0, dollarRate ?? 0).ToString("N0") +
                    "\nمبلغ واردشده = " + amount.ToString("N0"));

            var result = new TransactionSaveResult();
            string finalDoc = (docNo ?? "").Trim();
            double roundedAmount = Money.Round(amount);
            int cid = Cid;
            object currentCid = CurrentCid;

            _db.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                // ─ ۱) تشخیص سند تکراری (همان دوره/صندوق/جهت/مبلغ/تاریخ) ─
                if (!confirmedDuplicate)
                {
                    using (var dup = new SQLiteCommand(@"
SELECT COUNT(1) FROM AccTransaction
WHERE (@per IS NULL OR PeriodID = @per) AND Direction = @dir AND TxnDate = @date
  AND COALESCE(FundID,-1) = COALESCE(@fund,-1) AND ABS(Amount - @amt) < 0.005
  AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0) = 0", con, tr))
                    {
                        dup.Parameters.AddRange(new[]
                        {
                            P("@per", (object)periodId ?? DBNull.Value), P("@dir", direction), P("@date", date),
                            P("@fund", (object)fundId ?? DBNull.Value), P("@amt", roundedAmount), P("@cid", cid)
                        });

                        if (Convert.ToInt32(dup.ExecuteScalar()) > 0)
                            throw new AccountingDuplicateException(
                                "یک تراکنش کاملاً مشابه از قبل ثبت شده است:\n" +
                                "تاریخ " + date + " — " + direction + " — " + roundedAmount.ToString("N0") + " افغانی\n\n" +
                                "اگر این واقعاً یک پرداخت جداگانه است، تأیید کنید تا ثبت شود.");
                    }
                }

                // ─ ۲) گرفتن شماره سند داخل همین تراکنش ─
                bool needNewDoc = string.IsNullOrEmpty(finalDoc);
                if (!needNewDoc)
                {
                    using (var chk = new SQLiteCommand(
                        "SELECT COUNT(1) FROM AccTransaction WHERE DocNo=@d AND (@cid = 0 OR CenterID = @cid) AND (@per IS NULL OR PeriodID = @per)", con, tr))
                    {
                        chk.Parameters.AddRange(new[]
                        {
                            P("@d", finalDoc), P("@cid", cid), P("@per", (object)periodId ?? DBNull.Value)
                        });
                        needNewDoc = Convert.ToInt32(chk.ExecuteScalar()) > 0;
                    }
                }

                if (needNewDoc)
                {
                    using (var next = new SQLiteCommand(@"
SELECT COALESCE(MAX(CAST(CASE WHEN DocNo GLOB '*[0-9]*' AND DocNo NOT GLOB '*[^0-9]*' THEN DocNo ELSE '0' END AS INTEGER)),0)+1
FROM AccTransaction WHERE (@cid = 0 OR CenterID = @cid) AND (@per IS NULL OR PeriodID = @per)", con, tr))
                    {
                        next.Parameters.AddRange(new[] { P("@cid", cid), P("@per", (object)periodId ?? DBNull.Value) });
                        finalDoc = Convert.ToInt32(next.ExecuteScalar()).ToString();
                        result.DocNoReassigned = true;
                    }
                }

                // ─ ۳) درج ─
                using (var ins = new SQLiteCommand(@"
INSERT INTO AccTransaction
    (DocNo, TxnDate, Direction, PeriodID, PartyID, FundID, CategoryType, CategoryID,
     Amount, Qty, DollarAmount, DollarRate, Description, AttachmentPath, CenterID, CreatedBy)
VALUES
    (@doc, @date, @dir, @per, @party, @fund, @ctype, @cat,
     @amt, @qty, @damt, @drate, @desc, @att, @cid, @by)", con, tr))
                {
                    ins.Parameters.AddRange(new[]
                    {
                        P("@doc", finalDoc), P("@date", date), P("@dir", direction),
                        P("@per", (object)periodId ?? DBNull.Value), P("@party", (object)partyId ?? DBNull.Value),
                        P("@fund", (object)fundId ?? DBNull.Value), P("@ctype", categoryType),
                        P("@cat", (object)categoryId ?? DBNull.Value), P("@amt", roundedAmount), P("@qty", qty),
                        P("@damt", (object)dollarAmount ?? DBNull.Value), P("@drate", (object)dollarRate ?? DBNull.Value),
                        P("@desc", description), P("@att", attachment),
                        P("@cid", currentCid), P("@by", SecurityContext.Username)
                    });
                    ins.ExecuteNonQuery();
                }

                using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                    result.TxnId = Convert.ToInt32(idCmd.ExecuteScalar());
            });

            result.DocNo = finalDoc;
            AccAudit.LogChange(direction == "دریافت" ? "ثبت دریافت" : "ثبت پرداخت",
                "AccTransaction", result.TxnId, null,
                "سند " + finalDoc + " / " + roundedAmount.ToString("N0") + " افغانی", "");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // اصلاح سند — «ابطال + صدور سند اصلاحی» در یک تراکنش اتمیک.
        //
        // آموزش — چرا سند مالی مستقیماً UPDATE نمی‌شود: سندِ ثبت‌شده یک رویدادِ
        // واقع‌شده است. اگر مبلغش را جای خود عوض کنیم، گزارشی که دیروز چاپ و
        // امضا شده دیگر با دیتابیس نمی‌خواند و هیچ‌کس نمی‌تواند بگوید کدام‌یک
        // درست است. روشِ اصولی این است که سندِ غلط «باطل» و یک سندِ تازه با
        // مقادیر درست صادر شود؛ آن‌وقت هر دو در دفتر می‌مانند و مسیرِ اصلاح
        // قابل ردیابی است.
        //
        // چرا اتمیک: اگر ابطال انجام شود ولی درجِ سندِ اصلاحی شکست بخورد (نقض
        // قاعده، خطای دیسک، قطع برق)، مبلغ به‌کلی از دفتر ناپدید می‌شود و
        // مانده‌ها غلط می‌شوند. هر دو گام داخل یک تراکنش پایگاه‌داده‌اند، پس
        // یا هر دو انجام می‌شوند یا هیچ‌کدام.
        //
        // خروجی: همان TransactionSaveResult سندِ تازه.
        // ─────────────────────────────────────────────────────────────────────
        public TransactionSaveResult ReviseTransactionAtomic(int originalId, string docNo, string date,
            string direction, int? periodId, int? partyId, int? fundId, string categoryType, int? categoryId,
            double amount, string qty, double? dollarAmount, double? dollarRate,
            string description, string attachment, string reason, bool confirmedDuplicate)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new AccountingRuleException("نوشتن دلیل اصلاح الزامی است؛ بدون آن ردّ حسابرسی ناقص می‌ماند.");

            if (!Money.IsValidPositive(amount))
                throw new AccountingRuleException("مبلغ تراکنش باید عددی بزرگ‌تر از صفر و حداکثر " +
                                                  Money.MaxAmount.ToString("N0") + " افغانی باشد.");

            if (!Money.IsConversionConsistent(amount, dollarAmount ?? 0, dollarRate ?? 0))
                throw new AccountingRuleException(
                    "مبلغ افغانی با مبلغ دلاری و نرخ هم‌خوان نیست.\n" +
                    "مبلغ دلاری × نرخ = " + Money.Convert(dollarAmount ?? 0, dollarRate ?? 0).ToString("N0") +
                    "\nمبلغ واردشده = " + amount.ToString("N0"));

            // دوره‌ی خودِ سندِ اصلی باید باز باشد — نه فقط دوره‌ای که در کمبو
            // انتخاب شده. (همان نگهبانی که VoidTransaction هم به کار می‌برد.)
            EnsureMutable("AccTransaction", "TxnID", originalId, "تراکنش");

            DataRow before = GetTransactionById(originalId);
            if (before == null)
                throw new AccountingRuleException("سند اصلی پیدا نشد؛ ممکن است کاربر دیگری آن را باطل کرده باشد.");

            string oldSnapshot = "سند " + before["DocNo"] + " / " + before["Direction"] + " / " +
                                 Convert.ToDouble(before["Amount"]).ToString("N0") + " افغانی";

            var result = new TransactionSaveResult();
            string finalDoc = (docNo ?? "").Trim();
            double roundedAmount = Money.Round(amount);
            int cid = Cid;
            object currentCid = CurrentCid;

            _db.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                // ─ ۱) ابطال سند اصلی ─
                // شرط COALESCE(IsReversed,0)=0 مسابقه را می‌بندد: اگر بین
                // بررسی بالا و اینجا کاربر دیگری همین سند را باطل کرده باشد،
                // هیچ سطری به‌روز نمی‌شود و ما کل تراکنش را برمی‌گردانیم.
                int affected;
                using (var vd = new SQLiteCommand(@"
UPDATE AccTransaction
SET IsReversed = 1, VoidReason = @r, VoidedBy = @by, VoidedAt = datetime('now')
WHERE TxnID = @id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0) = 0", con, tr))
                {
                    vd.Parameters.AddRange(new[]
                    {
                        P("@id", originalId), P("@r", "اصلاح سند — " + reason),
                        P("@by", SecurityContext.Username), P("@cid", cid)
                    });
                    affected = vd.ExecuteNonQuery();
                }

                if (affected == 0)
                    throw new AccountingRuleException(
                        "این سند هم‌اکنون توسط کاربر دیگری باطل شده است. فهرست را تازه کنید و دوباره تلاش کنید.");

                // ─ ۲) تشخیص سند تکراری برای سندِ تازه ─
                // سندِ اصلی همین الان باطل شد، پس خودش در این شمارش نمی‌آید و
                // «اصلاحِ بدون تغییرِ مبلغ» به‌اشتباه تکراری اعلام نمی‌شود.
                if (!confirmedDuplicate)
                {
                    using (var dup = new SQLiteCommand(@"
SELECT COUNT(1) FROM AccTransaction
WHERE (@per IS NULL OR PeriodID = @per) AND Direction = @dir AND TxnDate = @date
  AND COALESCE(FundID,-1) = COALESCE(@fund,-1) AND ABS(Amount - @amt) < 0.005
  AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0) = 0", con, tr))
                    {
                        dup.Parameters.AddRange(new[]
                        {
                            P("@per", (object)periodId ?? DBNull.Value), P("@dir", direction), P("@date", date),
                            P("@fund", (object)fundId ?? DBNull.Value), P("@amt", roundedAmount), P("@cid", cid)
                        });

                        if (Convert.ToInt32(dup.ExecuteScalar()) > 0)
                            throw new AccountingDuplicateException(
                                "یک تراکنش کاملاً مشابه از قبل ثبت شده است:\n" +
                                "تاریخ " + date + " — " + direction + " — " + roundedAmount.ToString("N0") + " افغانی\n\n" +
                                "اگر این واقعاً سند درستِ اصلاح‌شده است، تأیید کنید تا ثبت شود.");
                    }
                }

                // ─ ۳) شماره سندِ اصلاحی ─
                // آموزش — چرا شماره‌ی سندِ قبلی دوباره استفاده نمی‌شود: آن شماره
                // حالا به یک سندِ باطل‌شده تعلق دارد و در دفتر باقی است. دادنِ
                // همان شماره به سندِ تازه یعنی دو سند با یک شماره، که یکتاییِ
                // شماره سند را می‌شکند. پس همیشه شماره‌ی تازه گرفته می‌شود.
                using (var next = new SQLiteCommand(@"
SELECT COALESCE(MAX(CAST(CASE WHEN DocNo GLOB '*[0-9]*' AND DocNo NOT GLOB '*[^0-9]*' THEN DocNo ELSE '0' END AS INTEGER)),0)+1
FROM AccTransaction WHERE (@cid = 0 OR CenterID = @cid) AND (@per IS NULL OR PeriodID = @per)", con, tr))
                {
                    next.Parameters.AddRange(new[] { P("@cid", cid), P("@per", (object)periodId ?? DBNull.Value) });
                    string generated = Convert.ToInt32(next.ExecuteScalar()).ToString();
                    if (generated != finalDoc) result.DocNoReassigned = true;
                    finalDoc = generated;
                }

                // ─ ۴) درج سندِ اصلاحی، با پیوند به سندِ باطل‌شده ─
                using (var ins = new SQLiteCommand(@"
INSERT INTO AccTransaction
    (DocNo, TxnDate, Direction, PeriodID, PartyID, FundID, CategoryType, CategoryID,
     Amount, Qty, DollarAmount, DollarRate, Description, AttachmentPath, CenterID, CreatedBy, RevisesTxnID)
VALUES
    (@doc, @date, @dir, @per, @party, @fund, @ctype, @cat,
     @amt, @qty, @damt, @drate, @desc, @att, @cid, @by, @orig)", con, tr))
                {
                    ins.Parameters.AddRange(new[]
                    {
                        P("@doc", finalDoc), P("@date", date), P("@dir", direction),
                        P("@per", (object)periodId ?? DBNull.Value), P("@party", (object)partyId ?? DBNull.Value),
                        P("@fund", (object)fundId ?? DBNull.Value), P("@ctype", categoryType),
                        P("@cat", (object)categoryId ?? DBNull.Value), P("@amt", roundedAmount), P("@qty", qty),
                        P("@damt", (object)dollarAmount ?? DBNull.Value), P("@drate", (object)dollarRate ?? DBNull.Value),
                        P("@desc", description), P("@att", attachment),
                        P("@cid", currentCid), P("@by", SecurityContext.Username), P("@orig", originalId)
                    });
                    ins.ExecuteNonQuery();
                }

                using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                    result.TxnId = Convert.ToInt32(idCmd.ExecuteScalar());
            });

            result.DocNo = finalDoc;

            // دو ردیفِ حسابرسی: یکی روی سندِ باطل‌شده، یکی روی سندِ تازه. هر دو
            // لازم‌اند تا از هر طرف که به سند نگاه شود، مسیرِ اصلاح پیدا باشد.
            AccAudit.LogChange("ابطال بابت اصلاح", "AccTransaction", originalId,
                oldSnapshot, "باطل شد — جایگزین: سند " + finalDoc, reason);
            AccAudit.LogChange("صدور سند اصلاحی", "AccTransaction", result.TxnId,
                oldSnapshot, "سند " + finalDoc + " / " + roundedAmount.ToString("N0") + " افغانی", reason);

            return result;
        }

        // آموزش — تغییر معنایی مهم (طبق اصول حسابداری): «حذف» یک سند مالی
        // دیگر رکورد را از دیتابیس پاک نمی‌کند، بلکه آن را «باطل» می‌کند.
        // دلیل: با DELETE واقعی، مبلغ از تمام مانده‌ها و گزارش‌ها ناپدید می‌شد
        // بدون آن‌که هیچ نشانی از آنچه حذف شده باقی بماند — ردّ حسابرسی فقط
        // یک شماره‌ی شناسه ثبت می‌کرد. حالا رکورد سرجایش می‌ماند، با پرچم
        // IsReversed و دلیل ابطال؛ همه‌ی محاسبات مانده آن را نادیده می‌گیرند
        // اما در حسابرسی کاملاً قابل ردیابی است.
        //
        // امضای قدیمی حفظ شده تا کدهای موجود بشکنند نشوند؛ به Void هدایت می‌شود.
        public void DeleteTransaction(int id)
        {
            VoidTransaction(id, "");
        }

        public void VoidTransaction(int id, string reason)
        {
            EnsureMutable("AccTransaction", "TxnID", id, "تراکنش");

            DataRow before = GetTransactionById(id);
            string snapshot = before == null ? "" :
                "سند " + before["DocNo"] + " / " + before["Direction"] + " / " +
                Convert.ToDouble(before["Amount"]).ToString("N0") + " افغانی";

            _db.ExecuteNonQuery(@"
UPDATE AccTransaction
SET IsReversed = 1, VoidReason = @r, VoidedBy = @by, VoidedAt = datetime('now')
WHERE TxnID = @id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0) = 0",
                P("@id", id), P("@r", reason), P("@by", SecurityContext.Username), P("@cid", Cid));

            AccAudit.LogChange("ابطال تراکنش", "AccTransaction", id, snapshot, "باطل شد", reason);
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
  AND COALESCE(t.IsReversed,0) = 0
ORDER BY t.TxnID DESC",
                P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value), P("@fund", (object)fundId ?? DBNull.Value));
        }

        // آموزش — هر مقدار مبلغی که از دیتابیس بیرون می‌آید از یک نقطه عبور و
        // گِرد می‌شود تا خطای انباشته‌ی ممیز شناور در جمع‌ها (SUM) به مقایسه‌های
        // صحت‌سنجی نشت نکند. توضیح کامل در Accounting/Money.cs.
        private static double ToDouble(object v)
        {
            return v == null || v == DBNull.Value ? 0 : Money.Round(Convert.ToDouble(v));
        }

        // ═══════════════════════════════════════════════════════════════════
        // نگهبان‌های صحت (اعمال در لایه‌ی داده، نه فقط UI)
        // ═══════════════════════════════════════════════════════════════════
        // آموزش — چرا این نگهبان‌ها در Repo هستند و نه فقط در فرم: بررسی‌های
        // قبلی («دوره باز است؟») فقط در FrmAccounting و فقط روی *دوره‌ی
        // انتخاب‌شده در کمبو* انجام می‌شد، نه روی دوره‌ی خودِ رکوردی که ویرایش
        // می‌شود. یعنی کافی بود کاربر ردیفی از یک دوره‌ی بسته را انتخاب کند و
        // در کمبو یک دوره‌ی باز بگذارد تا ویرایش انجام شود. هم‌چنین دستورهای
        // UPDATE/DELETE هیچ‌کدام فیلتر CenterID نداشتند، در حالی که تمام
        // SELECTها داشتند — یعنی خواندن محدود به مرکز بود ولی نوشتن نه.
        // با گذاشتن نگهبان در Repo، هر مسیری (فرم فعلی یا هر کد آینده) که به
        // این متدها برسد ناچار از رعایت قاعده است.

        private struct RecordState
        {
            public bool Exists;
            public bool PeriodClosed;
            public bool Reversed;
        }

        private RecordState GetRecordState(string table, string idColumn, int id)
        {
            var st = new RecordState();
            DataTable dt = _db.Query(
                "SELECT COALESCE(p.Status,'باز') AS St, COALESCE(t.IsReversed,0) AS Rv " +
                "FROM " + table + " t LEFT JOIN AccPeriod p ON p.PeriodID = t.PeriodID " +
                "WHERE t." + idColumn + " = @id AND (@cid = 0 OR t.CenterID = @cid)",
                P("@id", id), P("@cid", Cid));

            if (dt.Rows.Count == 0) return st;
            st.Exists = true;
            st.PeriodClosed = dt.Rows[0]["St"].ToString() == "بسته";
            st.Reversed = Convert.ToInt32(dt.Rows[0]["Rv"]) != 0;
            return st;
        }

        // پیش از هر ویرایش/ابطال، رکورد باید: در مرکز فعال کاربر باشد، دوره‌اش
        // باز باشد، و قبلاً باطل نشده باشد.
        private void EnsureMutable(string table, string idColumn, int id, string entityLabel)
        {
            RecordState st = GetRecordState(table, idColumn, id);

            if (!st.Exists)
                throw new AccountingRuleException(entityLabel + " مورد نظر در مرکز فعال شما یافت نشد.");

            if (st.PeriodClosed)
                throw new AccountingRuleException(
                    "دوره مالی این " + entityLabel + " «بسته» است.\n" +
                    "رکوردهای دوره‌ی بسته‌شده قابل ویرایش یا حذف نیستند. " +
                    "برای اصلاح، یک سند اصلاحی در دوره‌ی باز ثبت کنید.");

            if (st.Reversed)
                throw new AccountingRuleException("این " + entityLabel + " قبلاً باطل شده و دیگر قابل تغییر نیست.");
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
  AND COALESCE(s.IsReversed,0) = 0
ORDER BY s.SadatType, s.FamilySize",
                P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
        }

        public int AddStipend(int? periodId, string province, string district, string center, string sadatType,
            int familySize, int familyCount, int orphanCount, double amountPerFamily, int? fundId)
        {
            // آموزش — این بررسی‌ها قبلاً هیچ‌جا نبودند (نه در فرم و نه اینجا)،
            // پس ثبت ردیف شهریه با «۰ خانوار» یا «مبلغ ۰» کاملاً موفق انجام
            // می‌شد و یک رکورد مالیِ بی‌معنا با TotalPaid=0 می‌ساخت.
            if (familyCount <= 0)
                throw new AccountingRuleException("تعداد خانوار باید بزرگ‌تر از صفر باشد.");
            if (!Money.IsValidPositive(amountPerFamily))
                throw new AccountingRuleException("مبلغ شهریه هر خانواده باید بزرگ‌تر از صفر باشد.");
            if (orphanCount < 0)
                throw new AccountingRuleException("تعداد یتیم نمی‌تواند منفی باشد.");

            double total = Money.Round(familyCount * amountPerFamily);

            if (!Money.IsValidPositive(total))
                throw new AccountingRuleException("جمع پرداختی محاسبه‌شده معتبر نیست (از سقف مجاز فراتر رفته است).");

            int id = (int)_db.ExecuteInsertReturningId(@"
INSERT INTO AccStipend (PeriodID, Province, District, Center, SadatType, FamilySize, FamilyCount, OrphanCount, AmountPerFamily, TotalPaid, FundID, CenterID)
VALUES (@per,@prov,@dist,@cen,@sadat,@size,@fc,@oc,@amt,@tot,@fund,@cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@prov", province), P("@dist", district), P("@cen", center),
                P("@sadat", sadatType), P("@size", familySize), P("@fc", familyCount), P("@oc", orphanCount),
                P("@amt", amountPerFamily), P("@tot", total), P("@fund", (object)fundId ?? DBNull.Value), P("@cid", CurrentCid));
            AccAudit.Log("ثبت شهریه", "AccStipend", id, sadatType + " / " + familySize + "نفره / " + total.ToString("N0"));
            return id;
        }

        public void UpdateStipend(int id, string province, string district, string center, string sadatType,
            int familySize, int familyCount, int orphanCount, double amountPerFamily, int? fundId)
        {
            EnsureMutable("AccStipend", "StipendID", id, "ردیف شهریه");

            if (familyCount <= 0)
                throw new AccountingRuleException("تعداد خانوار باید بزرگ‌تر از صفر باشد.");
            if (!Money.IsValidPositive(amountPerFamily))
                throw new AccountingRuleException("مبلغ شهریه هر خانواده باید بزرگ‌تر از صفر باشد.");

            DataRow before = GetStipendById(id);
            string oldValue = before == null ? "" :
                before["SadatType"] + " / " + before["FamilyCount"] + " خانوار × " +
                Convert.ToDouble(before["AmountPerFamily"]).ToString("N0") + " = " +
                Convert.ToDouble(before["TotalPaid"]).ToString("N0");

            double total = Money.Round(familyCount * amountPerFamily);
            _db.ExecuteNonQuery(@"
UPDATE AccStipend SET Province=@prov, District=@dist, Center=@cen, SadatType=@sadat, FamilySize=@size,
       FamilyCount=@fc, OrphanCount=@oc, AmountPerFamily=@amt, TotalPaid=@tot, FundID=@fund
WHERE StipendID=@id AND (@cid = 0 OR CenterID = @cid)",
                P("@prov", province), P("@dist", district), P("@cen", center), P("@sadat", sadatType),
                P("@size", familySize), P("@fc", familyCount), P("@oc", orphanCount), P("@amt", amountPerFamily),
                P("@tot", total), P("@fund", (object)fundId ?? DBNull.Value), P("@id", id), P("@cid", Cid));

            AccAudit.LogChange("ویرایش شهریه", "AccStipend", id, oldValue,
                sadatType + " / " + familyCount + " خانوار × " + amountPerFamily.ToString("N0") + " = " + total.ToString("N0"), "");
        }

        public void DeleteStipend(int id)
        {
            VoidStipend(id, "");
        }

        public void VoidStipend(int id, string reason)
        {
            EnsureMutable("AccStipend", "StipendID", id, "ردیف شهریه");

            DataRow before = GetStipendById(id);
            string snapshot = before == null ? "" :
                before["SadatType"] + " / " + Convert.ToDouble(before["TotalPaid"]).ToString("N0") + " افغانی";

            _db.ExecuteNonQuery(@"
UPDATE AccStipend SET IsReversed = 1, VoidReason = @r, VoidedBy = @by, VoidedAt = datetime('now')
WHERE StipendID = @id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0) = 0",
                P("@id", id), P("@r", reason), P("@by", SecurityContext.Username), P("@cid", Cid));

            AccAudit.LogChange("ابطال شهریه", "AccStipend", id, snapshot, "باطل شد", reason);
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
  AND COALESCE(s.IsReversed,0) = 0
ORDER BY s.EmployeeName",
                P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
        }

        public int AddSalary(int? periodId, string name, string position, double amount, string note, int? fundId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new AccountingRuleException("نام کارمند نمی‌تواند خالی باشد.");
            if (!Money.IsValidPositive(amount))
                throw new AccountingRuleException("مبلغ حقوق باید بزرگ‌تر از صفر باشد.");

            int id = (int)_db.ExecuteInsertReturningId("INSERT INTO AccSalary (PeriodID, EmployeeName, Position, Amount, Note, FundID, CenterID) VALUES (@per,@n,@p,@a,@note,@fund,@cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@n", name), P("@p", position), P("@a", amount), P("@note", note),
                P("@fund", (object)fundId ?? DBNull.Value), P("@cid", CurrentCid));
            AccAudit.Log("ثبت حقوق", "AccSalary", id, name + " / " + amount.ToString("N0"));
            return id;
        }

        public void UpdateSalary(int id, string name, string position, double amount, string note, int? fundId)
        {
            EnsureMutable("AccSalary", "SalaryID", id, "ردیف حقوق");

            if (string.IsNullOrWhiteSpace(name))
                throw new AccountingRuleException("نام کارمند نمی‌تواند خالی باشد.");
            if (!Money.IsValidPositive(amount))
                throw new AccountingRuleException("مبلغ حقوق باید بزرگ‌تر از صفر باشد.");

            DataRow before = GetSalaryById(id);
            string oldValue = before == null ? "" :
                before["EmployeeName"] + " / " + Convert.ToDouble(before["Amount"]).ToString("N0");

            _db.ExecuteNonQuery("UPDATE AccSalary SET EmployeeName=@n, Position=@p, Amount=@a, Note=@note, FundID=@fund WHERE SalaryID=@id AND (@cid = 0 OR CenterID = @cid)",
                P("@n", name), P("@p", position), P("@a", amount), P("@note", note), P("@fund", (object)fundId ?? DBNull.Value), P("@id", id), P("@cid", Cid));

            AccAudit.LogChange("ویرایش حقوق", "AccSalary", id, oldValue, name + " / " + amount.ToString("N0"), "");
        }

        public void DeleteSalary(int id)
        {
            VoidSalary(id, "");
        }

        public void VoidSalary(int id, string reason)
        {
            EnsureMutable("AccSalary", "SalaryID", id, "ردیف حقوق");

            DataRow before = GetSalaryById(id);
            string snapshot = before == null ? "" :
                before["EmployeeName"] + " / " + Convert.ToDouble(before["Amount"]).ToString("N0") + " افغانی";

            _db.ExecuteNonQuery(@"
UPDATE AccSalary SET IsReversed = 1, VoidReason = @r, VoidedBy = @by, VoidedAt = datetime('now')
WHERE SalaryID = @id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0) = 0",
                P("@id", id), P("@r", reason), P("@by", SecurityContext.Username), P("@cid", Cid));

            AccAudit.LogChange("ابطال حقوق", "AccSalary", id, snapshot, "باطل شد", reason);
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
  AND COALESCE(e.IsReversed,0) = 0
ORDER BY e.ItemID",
                P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
        }

        public int AddExpenseItem(int? periodId, int? categoryId, string categoryName, string description, string qty, double price, string docNo, string itemDate, int? fundId)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new AccountingRuleException("شرح هزینه نمی‌تواند خالی باشد.");
            if (!Money.IsValidPositive(price))
                throw new AccountingRuleException("قیمت/مبلغ هزینه باید بزرگ‌تر از صفر باشد.");

            int id = (int)_db.ExecuteInsertReturningId(@"
INSERT INTO AccExpenseItem (PeriodID, CategoryID, CategoryName, Description, Qty, Price, DocNo, ItemDate, FundID, CenterID)
VALUES (@per,@cat,@catn,@desc,@qty,@price,@doc,@date,@fund,@cid)",
                P("@per", (object)periodId ?? DBNull.Value), P("@cat", (object)categoryId ?? DBNull.Value), P("@catn", categoryName),
                P("@desc", description), P("@qty", qty), P("@price", price), P("@doc", docNo), P("@date", itemDate),
                P("@fund", (object)fundId ?? DBNull.Value), P("@cid", CurrentCid));
            AccAudit.Log("ثبت هزینه جاری", "AccExpenseItem", id, description + " / " + price.ToString("N0"));
            return id;
        }

        public void UpdateExpenseItem(int id, int? categoryId, string categoryName, string description, string qty, double price, string docNo, string itemDate, int? fundId)
        {
            EnsureMutable("AccExpenseItem", "ItemID", id, "قلم هزینه");

            if (string.IsNullOrWhiteSpace(description))
                throw new AccountingRuleException("شرح هزینه نمی‌تواند خالی باشد.");
            if (!Money.IsValidPositive(price))
                throw new AccountingRuleException("قیمت/مبلغ هزینه باید بزرگ‌تر از صفر باشد.");

            DataRow before = GetExpenseItemById(id);
            string oldValue = before == null ? "" :
                before["Description"] + " / " + Convert.ToDouble(before["Price"]).ToString("N0");

            _db.ExecuteNonQuery(@"
UPDATE AccExpenseItem SET CategoryID=@cat, CategoryName=@catn, Description=@desc, Qty=@qty, Price=@price, DocNo=@doc, ItemDate=@date, FundID=@fund
WHERE ItemID=@id AND (@cid = 0 OR CenterID = @cid)",
                P("@cat", (object)categoryId ?? DBNull.Value), P("@catn", categoryName), P("@desc", description),
                P("@qty", qty), P("@price", price), P("@doc", docNo), P("@date", itemDate), P("@fund", (object)fundId ?? DBNull.Value), P("@id", id), P("@cid", Cid));

            AccAudit.LogChange("ویرایش هزینه جاری", "AccExpenseItem", id, oldValue, description + " / " + price.ToString("N0"), "");
        }

        public void DeleteExpenseItem(int id)
        {
            VoidExpenseItem(id, "");
        }

        public void VoidExpenseItem(int id, string reason)
        {
            EnsureMutable("AccExpenseItem", "ItemID", id, "قلم هزینه");

            DataRow before = GetExpenseItemById(id);
            string snapshot = before == null ? "" :
                before["Description"] + " / " + Convert.ToDouble(before["Price"]).ToString("N0") + " افغانی";

            _db.ExecuteNonQuery(@"
UPDATE AccExpenseItem SET IsReversed = 1, VoidReason = @r, VoidedBy = @by, VoidedAt = datetime('now')
WHERE ItemID = @id AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0) = 0",
                P("@id", id), P("@r", reason), P("@by", SecurityContext.Username), P("@cid", Cid));

            AccAudit.LogChange("ابطال هزینه جاری", "AccExpenseItem", id, snapshot, "باطل شد", reason);
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
                "SELECT COALESCE(SUM(TotalPaid),0) FROM AccStipend WHERE (@per IS NULL OR PeriodID=@per) AND (@st IS NULL OR SadatType=@st) AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0",
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
                "SELECT COALESCE(SUM(Amount),0) FROM AccSalary WHERE (@per IS NULL OR PeriodID=@per) AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0",
                P("@per", (object)periodId ?? DBNull.Value), P("@cid", Cid)));
        }

        public double SumExpenseItems(int? periodId)
        {
            return ToDouble(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(Price),0) FROM AccExpenseItem WHERE (@per IS NULL OR PeriodID=@per) AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0",
                P("@per", (object)periodId ?? DBNull.Value), P("@cid", Cid)));
        }

        public double SumExpenseByCategory(int? periodId, string categoryName)
        {
            return ToDouble(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(Price),0) FROM AccExpenseItem WHERE (@per IS NULL OR PeriodID=@per) AND CategoryName=@cn AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0",
                P("@per", (object)periodId ?? DBNull.Value), P("@cn", categoryName), P("@cid", Cid)));
        }

        public double SumTransactions(int? periodId, string direction, string categoryType)
        {
            return ToDouble(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(Amount),0) FROM AccTransaction WHERE (@per IS NULL OR PeriodID=@per) AND Direction=@dir AND (@ct IS NULL OR CategoryType=@ct) AND (@cid = 0 OR CenterID = @cid) AND COALESCE(IsReversed,0)=0",
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
  AND COALESCE(t.IsReversed,0) = 0
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
        // آموزش — رفع باگ گزارش «دفتر صندوق»: این سه متد هیچ فیلتر دوره‌ای
        // نداشتند، در حالی که تراکنش‌های همان گزارش *با* فیلتر دوره نمایش داده
        // می‌شدند. نتیجه: وقتی کاربر دوره‌ی خاصی را انتخاب می‌کرد، دفتر صندوق
        // تراکنش‌های آن دوره را با شهریه/حقوق/هزینه‌ی *همه‌ی دوره‌ها* قاطی
        // نشان می‌داد. حالا پارامتر دوره اضافه شده است.
        //
        // امضای بدون‌پارامترِ قبلی به‌صورت overload حفظ شده تا اگر جای دیگری
        // از آن استفاده شود، کد بشکند نه.
        public DataTable GetStipendsByFund(int fundId) { return GetStipendsByFund(fundId, null); }

        public DataTable GetStipendsByFund(int fundId, int? periodId)
        {
            return _db.Query(@"
SELECT SadatType, FamilySize, TotalPaid FROM AccStipend
WHERE FundID=@fund AND (@cid = 0 OR CenterID = @cid)
  AND (@per IS NULL OR PeriodID = @per) AND COALESCE(IsReversed,0)=0",
                P("@fund", fundId), P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
        }

        public DataTable GetSalariesByFund(int fundId) { return GetSalariesByFund(fundId, null); }

        public DataTable GetSalariesByFund(int fundId, int? periodId)
        {
            return _db.Query(@"
SELECT EmployeeName, Amount FROM AccSalary
WHERE FundID=@fund AND (@cid = 0 OR CenterID = @cid)
  AND (@per IS NULL OR PeriodID = @per) AND COALESCE(IsReversed,0)=0",
                P("@fund", fundId), P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
        }

        public DataTable GetExpenseItemsByFund(int fundId) { return GetExpenseItemsByFund(fundId, null); }

        public DataTable GetExpenseItemsByFund(int fundId, int? periodId)
        {
            return _db.Query(@"
SELECT Description, ItemDate, Price FROM AccExpenseItem
WHERE FundID=@fund AND (@cid = 0 OR CenterID = @cid)
  AND (@per IS NULL OR PeriodID = @per) AND COALESCE(IsReversed,0)=0",
                P("@fund", fundId), P("@cid", Cid), P("@per", (object)periodId ?? DBNull.Value));
        }

        // تمام تراکنش‌های یک صندوق به ترتیب ثبت (برای محاسبه مانده تجمعی صحیح)
        public DataTable GetFundTransactionsChronological(int fundId)
        {
            return _db.Query(@"
SELECT t.TxnID, t.DocNo, t.TxnDate, t.Direction, t.Amount, t.PeriodID,
       p.Name AS PartyName, t.Description
FROM AccTransaction t
LEFT JOIN AccParty p ON p.PartyID = t.PartyID
WHERE t.FundID = @fund AND (@cid = 0 OR t.CenterID = @cid) AND COALESCE(t.IsReversed,0) = 0
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
WHERE t.PartyID = @party AND (@cid = 0 OR t.CenterID = @cid) AND COALESCE(t.IsReversed,0) = 0
ORDER BY t.TxnID", P("@party", partyId), P("@cid", Cid));
        }

        public DataTable GetExpenseCategorySummary(int? periodId)
        {
            return _db.Query(@"
SELECT COALESCE(ec.Name,'سایر') AS [عنوان], COALESCE(SUM(e.Price),0) AS [مبلغ]
FROM AccExpenseItem e
LEFT JOIN AccExpenseCategory ec ON ec.CatID = e.CategoryID
WHERE (@per IS NULL OR e.PeriodID = @per) AND (@cid = 0 OR e.CenterID = @cid)
  AND COALESCE(e.IsReversed,0) = 0
GROUP BY COALESCE(ec.Name,'سایر')
ORDER BY [مبلغ] DESC",
                P("@per", (object)periodId ?? DBNull.Value), P("@cid", Cid));
        }
    }
}

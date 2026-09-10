using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;

namespace CaseManagement.Accounting.Ledger.Infrastructure
{
    /// <summary>
    /// Persistence for Gl* only. Never emits DELETE FROM. Optimistic concurrency via RowVersion.
    /// </summary>
    public partial class LedgerRepository
    {
        private readonly DatabaseHelper _db;

        public LedgerRepository() : this(new DatabaseHelper()) { }

        public LedgerRepository(DatabaseHelper db)
        {
            _db = db;
        }

        public DatabaseHelper Db { get { return _db; } }

        public void ExecuteInTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            _db.ExecuteInTransaction(action);
        }

        // ── Company / settings / FX ─────────────────────────────────────────

        public GlCompany GetCompany(int companyId)
        {
            return MapCompany(QueryRow(
                "SELECT * FROM GlCompany WHERE CompanyID = @id AND IsDeleted = 0;",
                P("@id", companyId)));
        }

        public GlCompany GetCompany(SQLiteConnection con, SQLiteTransaction tr, int companyId)
        {
            return MapCompany(QueryRow(con, tr,
                "SELECT * FROM GlCompany WHERE CompanyID = @id AND IsDeleted = 0;",
                P("@id", companyId)));
        }

        public IList<GlAccountType> ListAccountTypes(int companyId)
        {
            DataTable table = Query(
                "SELECT * FROM GlAccountType WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY AccountTypeCode;",
                P("@c", companyId));
            List<GlAccountType> list = new List<GlAccountType>();
            foreach (DataRow r in table.Rows)
            {
                list.Add(new GlAccountType
                {
                    AccountTypeId = Long(r["AccountTypeID"]),
                    CompanyId = Int(r["CompanyID"]),
                    AccountTypeCode = Str(r["AccountTypeCode"]),
                    Name = Str(r["Name"]),
                    NormalBalance = Str(r["NormalBalance"]),
                    Statement = Str(r["Statement"])
                });
            }
            return list;
        }

        public IList<GlFiscalYear> ListYears(int companyId)
        {
            DataTable table = Query(
                "SELECT * FROM GlFiscalYear WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY StartDate DESC;",
                P("@c", companyId));
            List<GlFiscalYear> list = new List<GlFiscalYear>();
            foreach (DataRow r in table.Rows)
                list.Add(MapYear(r));
            return list;
        }

        public IList<GlJournal> ListJournalHeaders(int companyId, int centerFilter, string fromDate, string toDate)
        {
            DataTable table = Query(@"
SELECT * FROM GlJournal
WHERE CompanyID = @c AND IsDeleted = 0
  AND (@from = '' OR PostingDate >= @from)
  AND (@to = '' OR PostingDate <= @to)
  AND (@ctr = 0 OR CenterID = @ctr)
ORDER BY PostingDate DESC, JournalID DESC
LIMIT 500;",
                P("@c", companyId),
                P("@from", fromDate ?? ""),
                P("@to", toDate ?? ""),
                P("@ctr", centerFilter));
            List<GlJournal> list = new List<GlJournal>();
            foreach (DataRow r in table.Rows)
                list.Add(MapJournal(r));
            return list;
        }

        public DataTable QueryPostedLineSums(int companyId, int centerFilter, string fromDate, string toDate,
            long costCenterId, long projectId)
        {
            return Query(@"
SELECT a.AccountID, a.AccountCode, a.AccountName, a.AccountTypeCode, a.IsContra,
  COALESCE(SUM(CASE WHEN j.PostingDate < @from THEN l.DebitBaseMinor ELSE 0 END), 0) AS OpenDr,
  COALESCE(SUM(CASE WHEN j.PostingDate < @from THEN l.CreditBaseMinor ELSE 0 END), 0) AS OpenCr,
  COALESCE(SUM(CASE WHEN j.PostingDate >= @from AND j.PostingDate <= @to THEN l.DebitBaseMinor ELSE 0 END), 0) AS PeriodDr,
  COALESCE(SUM(CASE WHEN j.PostingDate >= @from AND j.PostingDate <= @to THEN l.CreditBaseMinor ELSE 0 END), 0) AS PeriodCr
FROM GlAccount a
LEFT JOIN GlJournalLine l ON l.AccountID = a.AccountID
  AND (@cc = 0 OR l.CostCenterID = @cc)
  AND (@pr = 0 OR l.ProjectID = @pr)
LEFT JOIN GlJournal j ON j.JournalID = l.JournalID
  AND j.IsDeleted = 0 AND j.Status IN ('Posted', 'Reversed')
  AND (@ctr = 0 OR j.CenterID = @ctr)
WHERE a.CompanyID = @c AND a.IsDeleted = 0 AND a.IsLeaf = 1
GROUP BY a.AccountID, a.AccountCode, a.AccountName, a.AccountTypeCode, a.IsContra
ORDER BY a.AccountCode;",
                P("@c", companyId),
                P("@from", fromDate ?? ""),
                P("@to", toDate ?? "9999-12-31"),
                P("@ctr", centerFilter),
                P("@cc", costCenterId),
                P("@pr", projectId));
        }

        public DataTable QueryGeneralLedgerLines(int companyId, int centerFilter, long accountId, string fromDate, string toDate,
            long costCenterId, long projectId)
        {
            return Query(@"
SELECT j.PostingDate, j.JournalNumber, COALESCE(l.Description, j.Description) AS Description,
  l.DebitBaseMinor, l.CreditBaseMinor, l.LineNo, j.JournalID
FROM GlJournalLine l
JOIN GlJournal j ON j.JournalID = l.JournalID
WHERE l.AccountID = @acc AND l.CompanyID = @c
  AND j.IsDeleted = 0 AND j.Status IN ('Posted', 'Reversed')
  AND (@ctr = 0 OR j.CenterID = @ctr)
  AND (@cc = 0 OR l.CostCenterID = @cc)
  AND (@pr = 0 OR l.ProjectID = @pr)
  AND (@from = '' OR j.PostingDate >= @from)
  AND (@to = '' OR j.PostingDate <= @to)
  AND (l.DebitMinor > 0 OR l.CreditMinor > 0)
ORDER BY j.PostingDate, j.JournalID, l.LineNo;",
                P("@c", companyId),
                P("@acc", accountId),
                P("@from", fromDate ?? ""),
                P("@to", toDate ?? ""),
                P("@ctr", centerFilter),
                P("@cc", costCenterId),
                P("@pr", projectId));
        }

        public long QueryOpeningNet(int companyId, int centerFilter, long accountId, string fromDate,
            long costCenterId, long projectId)
        {
            object v = _db.ExecuteScalar(@"
SELECT COALESCE(SUM(l.DebitBaseMinor - l.CreditBaseMinor), 0)
FROM GlJournalLine l
JOIN GlJournal j ON j.JournalID = l.JournalID
WHERE l.AccountID = @acc AND l.CompanyID = @c
  AND j.IsDeleted = 0 AND j.Status IN ('Posted', 'Reversed')
  AND (@ctr = 0 OR j.CenterID = @ctr)
  AND (@cc = 0 OR l.CostCenterID = @cc)
  AND (@pr = 0 OR l.ProjectID = @pr)
  AND (@from = '' OR j.PostingDate < @from);",
                P("@c", companyId), P("@acc", accountId), P("@from", fromDate ?? ""), P("@ctr", centerFilter),
                P("@cc", costCenterId), P("@pr", projectId));
            if (v == null || v == DBNull.Value) return 0;
            return Convert.ToInt64(v);
        }

        public void InsertMasterAudit(string operation, string entity, long entityId,
            string oldValue, string newValue, CaseManagement.Accounting.Ledger.Application.ILedgerIdentity identity)
        {
            if (identity == null) return;
            try
            {
                int id32 = entityId > int.MaxValue ? 0 : (int)entityId;
                _db.ExecuteNonQuery(@"
INSERT INTO TblAuditLog (UserID, Username, Operation, EntityName, EntityID, OldValue, NewValue, CenterID)
VALUES (@uid, @un, @op, @en, @id, @ov, @nv, @cid);",
                    P("@uid", identity.UserId > 0 ? (object)identity.UserId : DBNull.Value),
                    P("@un", identity.UserName ?? ""),
                    P("@op", operation ?? ""),
                    P("@en", entity ?? ""),
                    P("@id", id32),
                    P("@ov", (object)oldValue ?? DBNull.Value),
                    P("@nv", (object)newValue ?? DBNull.Value),
                    P("@cid", identity.CenterId > 0 ? (object)identity.CenterId : DBNull.Value));
            }
            catch
            {
                // Master audit must not block GL writes if TblAuditLog is absent (isolated ledger tests).
            }
        }

        public GlLedgerSetting GetSetting(int companyId)
        {
            return MapSetting(QueryRow(
                "SELECT * FROM GlLedgerSetting WHERE CompanyID = @id AND IsDeleted = 0;",
                P("@id", companyId)));
        }

        public GlCurrency GetCurrency(int companyId, string code)
        {
            return MapCurrency(QueryRow(
                "SELECT * FROM GlCurrency WHERE CompanyID = @id AND CurrencyCode = @c AND IsDeleted = 0;",
                P("@id", companyId), P("@c", code)));
        }

        public long? GetRateToBaseMicros(SQLiteConnection con, SQLiteTransaction tr,
            int companyId, string currencyCode, string rateDate)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT RateToBaseMicros FROM GlExchangeRate
WHERE CompanyID = @cid AND CurrencyCode = @c AND IsDeleted = 0
  AND (CenterID = 0) AND RateDate <= @d
ORDER BY RateDate DESC, ExchangeRateID DESC LIMIT 1;", con, tr))
            {
                cmd.Parameters.AddWithValue("@cid", companyId);
                cmd.Parameters.AddWithValue("@c", currencyCode);
                cmd.Parameters.AddWithValue("@d", rateDate);
                object v = cmd.ExecuteScalar();
                if (v == null || v == DBNull.Value) return null;
                return Convert.ToInt64(v);
            }
        }

        public long? GetRateToBaseMicros(int companyId, string currencyCode, string rateDate)
        {
            object v = _db.ExecuteScalar(@"
SELECT RateToBaseMicros FROM GlExchangeRate
WHERE CompanyID = @cid AND CurrencyCode = @c AND IsDeleted = 0
  AND (CenterID = 0) AND RateDate <= @d
ORDER BY RateDate DESC, ExchangeRateID DESC LIMIT 1;",
                P("@cid", companyId), P("@c", currencyCode), P("@d", rateDate ?? ""));
            if (v == null || v == DBNull.Value) return null;
            return Convert.ToInt64(v);
        }

        public IList<GlCurrency> ListCurrencies(int companyId)
        {
            DataTable table = Query(
                "SELECT * FROM GlCurrency WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY CurrencyCode;",
                P("@c", companyId));
            List<GlCurrency> list = new List<GlCurrency>();
            foreach (DataRow r in table.Rows)
                list.Add(MapCurrency(r));
            return list;
        }

        public IList<GlExchangeRate> ListRates(int companyId, string currencyCode)
        {
            DataTable table = Query(@"
SELECT * FROM GlExchangeRate
WHERE CompanyID = @c AND IsDeleted = 0 AND (@ccy = '' OR CurrencyCode = @ccy)
ORDER BY RateDate DESC, ExchangeRateID DESC;",
                P("@c", companyId), P("@ccy", currencyCode ?? ""));
            List<GlExchangeRate> list = new List<GlExchangeRate>();
            foreach (DataRow r in table.Rows)
                list.Add(MapRate(r));
            return list;
        }

        public long UpsertExchangeRate(GlExchangeRate rate)
        {
            object existing = _db.ExecuteScalar(@"
SELECT ExchangeRateID FROM GlExchangeRate
WHERE CompanyID = @c AND CenterID = @ctr AND CurrencyCode = @ccy AND RateDate = @d AND IsDeleted = 0;",
                P("@c", rate.CompanyId), P("@ctr", rate.CenterId), P("@ccy", rate.CurrencyCode), P("@d", rate.RateDate));
            if (existing != null && existing != DBNull.Value)
            {
                long id = Convert.ToInt64(existing);
                _db.ExecuteNonQuery(@"
UPDATE GlExchangeRate SET RateToBaseMicros = @r, UpdatedAt = @now, UpdatedBy = @by, RowVersion = RowVersion + 1
WHERE ExchangeRateID = @id;",
                    P("@r", rate.RateToBaseMicros), P("@now", rate.UpdatedAt), P("@by", rate.UpdatedBy), P("@id", id));
                return id;
            }
            _db.ExecuteNonQuery(@"
INSERT INTO GlExchangeRate (CompanyID, CenterID, CurrencyCode, RateDate, RateToBaseMicros,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @ccy, @d, @r, 0, 1, @now, @now, @by, @by);",
                P("@c", rate.CompanyId), P("@ctr", rate.CenterId), P("@ccy", rate.CurrencyCode),
                P("@d", rate.RateDate), P("@r", rate.RateToBaseMicros), P("@now", rate.CreatedAt), P("@by", rate.CreatedBy));
            object created = _db.ExecuteScalar(@"
SELECT ExchangeRateID FROM GlExchangeRate
WHERE CompanyID = @c AND CenterID = @ctr AND CurrencyCode = @ccy AND RateDate = @d AND IsDeleted = 0;",
                P("@c", rate.CompanyId), P("@ctr", rate.CenterId), P("@ccy", rate.CurrencyCode), P("@d", rate.RateDate));
            return created == null || created == DBNull.Value ? 0 : Convert.ToInt64(created);
        }

        public DataTable QueryCurrencyNets(int companyId, int centerFilter, string asOfDate, string baseCurrency)
        {
            return Query(@"
SELECT a.AccountID, a.AccountCode, a.AccountName, a.AccountTypeCode, l.CurrencyCode,
  COALESCE(SUM(l.DebitMinor - l.CreditMinor), 0) AS TxnNet,
  COALESCE(SUM(l.DebitBaseMinor - l.CreditBaseMinor), 0) AS BaseNet
FROM GlJournalLine l
JOIN GlJournal j ON j.JournalID = l.JournalID
JOIN GlAccount a ON a.AccountID = l.AccountID
WHERE l.CompanyID = @c AND j.IsDeleted = 0 AND j.Status IN ('Posted', 'Reversed')
  AND a.IsDeleted = 0 AND a.IsLeaf = 1
  AND (@ctr = 0 OR j.CenterID = @ctr)
  AND (@to = '' OR j.PostingDate <= @to)
  AND l.CurrencyCode <> @base
  AND (l.DebitMinor > 0 OR l.CreditMinor > 0)
GROUP BY a.AccountID, a.AccountCode, a.AccountName, a.AccountTypeCode, l.CurrencyCode
HAVING COALESCE(SUM(l.DebitMinor - l.CreditMinor), 0) <> 0
    OR COALESCE(SUM(l.DebitBaseMinor - l.CreditBaseMinor), 0) <> 0
ORDER BY a.AccountCode, l.CurrencyCode;",
                P("@c", companyId),
                P("@ctr", centerFilter),
                P("@to", asOfDate ?? ""),
                P("@base", baseCurrency ?? LedgerCodes.BaseCurrency));
        }

        public void ReleaseSourceKey(long journalId)
        {
            _db.ExecuteNonQuery(@"
UPDATE GlJournal SET SourceModule = NULL, SourceDocumentType = NULL, SourceDocumentID = NULL,
  RowVersion = RowVersion + 1
WHERE JournalID = @id AND Status = @st;",
                P("@id", journalId), P("@st", LedgerCodes.JournalReversed));
        }

        public long AllocateJournalNumber(SQLiteConnection con, SQLiteTransaction tr,
            int companyId, string yearCode, string now, string user, out string journalNumber)
        {
            long next;
            string prefix;
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT NextJournalNumber, NumberPrefix FROM GlLedgerSetting
WHERE CompanyID = @cid AND IsDeleted = 0;", con, tr))
            {
                cmd.Parameters.AddWithValue("@cid", companyId);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        journalNumber = null;
                        return 0;
                    }
                    next = Convert.ToInt64(r["NextJournalNumber"]);
                    prefix = Convert.ToString(r["NumberPrefix"]);
                }
            }

            using (SQLiteCommand upd = new SQLiteCommand(@"
UPDATE GlLedgerSetting
SET NextJournalNumber = NextJournalNumber + 1,
    RowVersion = RowVersion + 1,
    UpdatedAt = @now,
    UpdatedBy = @user
WHERE CompanyID = @cid AND IsDeleted = 0;", con, tr))
            {
                upd.Parameters.AddWithValue("@now", now);
                upd.Parameters.AddWithValue("@user", user);
                upd.Parameters.AddWithValue("@cid", companyId);
                upd.ExecuteNonQuery();
            }

            journalNumber = (prefix ?? "JE") + "-" + yearCode + "-" + next.ToString("000000");
            return next;
        }

        // ── Accounts ────────────────────────────────────────────────────────

        public GlAccount GetAccount(long accountId)
        {
            return MapAccount(QueryRow("SELECT * FROM GlAccount WHERE AccountID = @id;", P("@id", accountId)));
        }

        public GlAccount GetAccount(SQLiteConnection con, SQLiteTransaction tr, long accountId)
        {
            return MapAccount(QueryRow(con, tr, "SELECT * FROM GlAccount WHERE AccountID = @id;", P("@id", accountId)));
        }

        public GlAccount GetAccountByCode(int companyId, string code)
        {
            return MapAccount(QueryRow(
                "SELECT * FROM GlAccount WHERE CompanyID = @c AND AccountCode = @code;",
                P("@c", companyId), P("@code", code)));
        }

        public IList<GlAccount> ListAccounts(int companyId, bool includeDeleted)
        {
            string sql = includeDeleted
                ? "SELECT * FROM GlAccount WHERE CompanyID = @c ORDER BY AccountCode;"
                : "SELECT * FROM GlAccount WHERE CompanyID = @c AND IsDeleted = 0 ORDER BY AccountCode;";
            return MapAccounts(Query(sql, P("@c", companyId)));
        }

        public bool AccountHasChildren(long accountId)
        {
            return ScalarInt(
                "SELECT COUNT(1) FROM GlAccount WHERE ParentAccountID = @id AND IsDeleted = 0;",
                P("@id", accountId)) > 0;
        }

        public bool AccountHasPostedLines(long accountId)
        {
            return ScalarInt(@"
SELECT COUNT(1)
FROM GlJournalLine l
JOIN GlJournal j ON j.JournalID = l.JournalID
WHERE l.AccountID = @id
  AND j.IsDeleted = 0
  AND j.Status IN ('Posted', 'Reversed');", P("@id", accountId)) > 0;
        }

        public long InsertAccount(GlAccount a)
        {
            return _db.ExecuteInsertReturningId(@"
INSERT INTO GlAccount (CompanyID, CenterID, AccountCode, AccountName, AccountTypeCode, ParentAccountID,
  Level, IsLeaf, AllowPosting, IsActive, IsContra, ControlCurrencyCode, IsDeleted, RowVersion,
  CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@cid, @ctr, @code, @name, @type, @parent, @lvl, @leaf, @post, @act, @contra, @ccy, 0, 1,
  @ca, @ua, @cb, @ub);",
                P("@cid", a.CompanyId), P("@ctr", a.CenterId), P("@code", a.AccountCode), P("@name", a.AccountName),
                P("@type", a.AccountTypeCode), P("@parent", (object)a.ParentAccountId ?? DBNull.Value),
                P("@lvl", a.Level), P("@leaf", a.IsLeaf ? 1 : 0), P("@post", a.AllowPosting ? 1 : 0),
                P("@act", a.IsActive ? 1 : 0), P("@contra", a.IsContra ? 1 : 0),
                P("@ccy", (object)a.ControlCurrencyCode ?? DBNull.Value),
                P("@ca", a.CreatedAt), P("@ua", a.UpdatedAt), P("@cb", a.CreatedBy), P("@ub", a.UpdatedBy));
        }

        public bool UpdateAccountConcurrency(GlAccount a, long expectedRowVersion)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE GlAccount SET
  AccountName = @name, IsLeaf = @leaf, AllowPosting = @post, IsActive = @act, IsContra = @contra,
  ControlCurrencyCode = @ccy, IsDeleted = @del, DeletedAt = @dat, DeletedBy = @dby,
  RowVersion = RowVersion + 1, UpdatedAt = @ua, UpdatedBy = @ub
WHERE AccountID = @id AND RowVersion = @rv;",
                P("@name", a.AccountName), P("@leaf", a.IsLeaf ? 1 : 0), P("@post", a.AllowPosting ? 1 : 0),
                P("@act", a.IsActive ? 1 : 0), P("@contra", a.IsContra ? 1 : 0),
                P("@ccy", (object)a.ControlCurrencyCode ?? DBNull.Value),
                P("@del", a.IsDeleted ? 1 : 0), P("@dat", (object)a.DeletedAt ?? DBNull.Value),
                P("@dby", (object)a.DeletedBy ?? DBNull.Value),
                P("@ua", a.UpdatedAt), P("@ub", a.UpdatedBy), P("@id", a.AccountId), P("@rv", expectedRowVersion));
            return n == 1;
        }

        // ── Fiscal calendar ─────────────────────────────────────────────────

        public GlFiscalYear GetYear(long yearId)
        {
            return MapYear(QueryRow("SELECT * FROM GlFiscalYear WHERE FiscalYearID = @id;", P("@id", yearId)));
        }

        public GlFiscalYear GetYear(SQLiteConnection con, SQLiteTransaction tr, long yearId)
        {
            return MapYear(QueryRow(con, tr, "SELECT * FROM GlFiscalYear WHERE FiscalYearID = @id;", P("@id", yearId)));
        }

        public GlFiscalPeriod GetPeriod(long periodId)
        {
            return MapPeriod(QueryRow("SELECT * FROM GlFiscalPeriod WHERE FiscalPeriodID = @id;", P("@id", periodId)));
        }

        public GlFiscalPeriod GetPeriod(SQLiteConnection con, SQLiteTransaction tr, long periodId)
        {
            return MapPeriod(QueryRow(con, tr, "SELECT * FROM GlFiscalPeriod WHERE FiscalPeriodID = @id;", P("@id", periodId)));
        }

        public GlFiscalPeriod ResolvePeriod(int companyId, string postingDate)
        {
            return MapPeriod(QueryRow(@"
SELECT p.* FROM GlFiscalPeriod p
JOIN GlFiscalYear y ON y.FiscalYearID = p.FiscalYearID
WHERE p.CompanyID = @cid AND p.IsDeleted = 0 AND y.IsDeleted = 0
  AND p.StartDate <= @d AND p.EndDate >= @d
ORDER BY p.PeriodNo
LIMIT 1;", P("@cid", companyId), P("@d", postingDate)));
        }

        public GlFiscalPeriod ResolvePeriod(SQLiteConnection con, SQLiteTransaction tr, int companyId, string postingDate)
        {
            return MapPeriod(QueryRow(con, tr, @"
SELECT p.* FROM GlFiscalPeriod p
JOIN GlFiscalYear y ON y.FiscalYearID = p.FiscalYearID
WHERE p.CompanyID = @cid AND p.IsDeleted = 0 AND y.IsDeleted = 0
  AND p.StartDate <= @d AND p.EndDate >= @d
ORDER BY p.PeriodNo
LIMIT 1;", P("@cid", companyId), P("@d", postingDate)));
        }

        public IList<GlFiscalPeriod> ListPeriods(long yearId, bool includeDeleted)
        {
            string sql = includeDeleted
                ? "SELECT * FROM GlFiscalPeriod WHERE FiscalYearID = @y ORDER BY PeriodNo;"
                : "SELECT * FROM GlFiscalPeriod WHERE FiscalYearID = @y AND IsDeleted = 0 ORDER BY PeriodNo;";
            DataTable table = Query(sql, P("@y", yearId));
            List<GlFiscalPeriod> list = new List<GlFiscalPeriod>();
            foreach (DataRow row in table.Rows)
                list.Add(MapPeriod(row));
            return list;
        }

        public bool YearOverlaps(int companyId, string start, string end, long excludeYearId)
        {
            return ScalarInt(@"
SELECT COUNT(1) FROM GlFiscalYear
WHERE CompanyID = @c AND IsDeleted = 0 AND FiscalYearID <> @ex
  AND StartDate <= @end AND EndDate >= @start;",
                P("@c", companyId), P("@ex", excludeYearId), P("@start", start), P("@end", end)) > 0;
        }

        public bool PeriodOverlaps(long yearId, string start, string end, long excludePeriodId)
        {
            return ScalarInt(@"
SELECT COUNT(1) FROM GlFiscalPeriod
WHERE FiscalYearID = @y AND IsDeleted = 0 AND FiscalPeriodID <> @ex
  AND StartDate <= @end AND EndDate >= @start;",
                P("@y", yearId), P("@ex", excludePeriodId), P("@start", start), P("@end", end)) > 0;
        }

        public bool YearHasPostedJournals(long yearId)
        {
            return ScalarInt(@"
SELECT COUNT(1) FROM GlJournal
WHERE FiscalYearID = @y AND IsDeleted = 0 AND Status IN ('Posted', 'Reversed');",
                P("@y", yearId)) > 0;
        }

        public bool PeriodHasPostedJournals(long periodId)
        {
            return ScalarInt(@"
SELECT COUNT(1) FROM GlJournal
WHERE FiscalPeriodID = @p AND IsDeleted = 0 AND Status IN ('Posted', 'Reversed');",
                P("@p", periodId)) > 0;
        }

        public long InsertYear(GlFiscalYear y)
        {
            return _db.ExecuteInsertReturningId(@"
INSERT INTO GlFiscalYear (CompanyID, CenterID, Code, Name, CalendarType, StartDate, EndDate, Status,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@cid, @ctr, @code, @name, @cal, @s, @e, @st, 0, 1, @ca, @ua, @cb, @ub);",
                P("@cid", y.CompanyId), P("@ctr", y.CenterId), P("@code", y.Code), P("@name", y.Name),
                P("@cal", y.CalendarType), P("@s", y.StartDate), P("@e", y.EndDate), P("@st", y.Status),
                P("@ca", y.CreatedAt), P("@ua", y.UpdatedAt), P("@cb", y.CreatedBy), P("@ub", y.UpdatedBy));
        }

        public long InsertPeriod(GlFiscalPeriod p)
        {
            return _db.ExecuteInsertReturningId(@"
INSERT INTO GlFiscalPeriod (CompanyID, CenterID, FiscalYearID, PeriodNo, Name, StartDate, EndDate, Status,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@cid, @ctr, @y, @n, @name, @s, @e, @st, 0, 1, @ca, @ua, @cb, @ub);",
                P("@cid", p.CompanyId), P("@ctr", p.CenterId), P("@y", p.FiscalYearId), P("@n", p.PeriodNo),
                P("@name", p.Name), P("@s", p.StartDate), P("@e", p.EndDate), P("@st", p.Status),
                P("@ca", p.CreatedAt), P("@ua", p.UpdatedAt), P("@cb", p.CreatedBy), P("@ub", p.UpdatedBy));
        }

        public bool UpdateYearConcurrency(GlFiscalYear y, long expectedRowVersion)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE GlFiscalYear SET
  Status = @st, ClosedAt = @ca, ClosedBy = @cb, LockedAt = @la, LockedBy = @lb,
  IsDeleted = @del, DeletedAt = @dat, DeletedBy = @dby,
  RowVersion = RowVersion + 1, UpdatedAt = @ua, UpdatedBy = @ub
WHERE FiscalYearID = @id AND RowVersion = @rv;",
                P("@st", y.Status), P("@ca", (object)y.ClosedAt ?? DBNull.Value), P("@cb", (object)y.ClosedBy ?? DBNull.Value),
                P("@la", (object)y.LockedAt ?? DBNull.Value), P("@lb", (object)y.LockedBy ?? DBNull.Value),
                P("@del", y.IsDeleted ? 1 : 0), P("@dat", (object)y.DeletedAt ?? DBNull.Value),
                P("@dby", (object)y.DeletedBy ?? DBNull.Value),
                P("@ua", y.UpdatedAt), P("@ub", y.UpdatedBy), P("@id", y.FiscalYearId), P("@rv", expectedRowVersion));
            return n == 1;
        }

        public bool UpdatePeriodConcurrency(GlFiscalPeriod p, long expectedRowVersion)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE GlFiscalPeriod SET
  Status = @st, IsDeleted = @del, DeletedAt = @dat, DeletedBy = @dby,
  RowVersion = RowVersion + 1, UpdatedAt = @ua, UpdatedBy = @ub
WHERE FiscalPeriodID = @id AND RowVersion = @rv;",
                P("@st", p.Status), P("@del", p.IsDeleted ? 1 : 0),
                P("@dat", (object)p.DeletedAt ?? DBNull.Value), P("@dby", (object)p.DeletedBy ?? DBNull.Value),
                P("@ua", p.UpdatedAt), P("@ub", p.UpdatedBy), P("@id", p.FiscalPeriodId), P("@rv", expectedRowVersion));
            return n == 1;
        }

        // ── Journals ────────────────────────────────────────────────────────

        public GlJournal GetJournal(long journalId)
        {
            GlJournal j = MapJournal(QueryRow("SELECT * FROM GlJournal WHERE JournalID = @id;", P("@id", journalId)));
            if (j != null)
                j.Lines = ListLines(journalId);
            return j;
        }

        public GlJournal GetJournal(SQLiteConnection con, SQLiteTransaction tr, long journalId)
        {
            GlJournal j = MapJournal(QueryRow(con, tr, "SELECT * FROM GlJournal WHERE JournalID = @id;", P("@id", journalId)));
            if (j != null)
                j.Lines = ListLines(con, tr, journalId);
            return j;
        }

        public GlJournal GetJournalBySource(int companyId, string module, string docType, long sourceId)
        {
            GlJournal j = MapJournal(QueryRow(@"
SELECT * FROM GlJournal
WHERE CompanyID = @c AND SourceModule = @m AND SourceDocumentType = @t AND SourceDocumentID = @id
  AND IsDeleted = 0
ORDER BY CASE Status WHEN 'Posted' THEN 0 WHEN 'Approved' THEN 1 WHEN 'Draft' THEN 2 ELSE 3 END, JournalID
LIMIT 1;",
                P("@c", companyId), P("@m", module), P("@t", docType), P("@id", sourceId)));
            if (j != null)
                j.Lines = ListLines(j.JournalId);
            return j;
        }

        public GlJournal GetJournalByClientGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return null;
            GlJournal j = MapJournal(QueryRow(
                "SELECT * FROM GlJournal WHERE ClientJournalGuid = @g AND IsDeleted = 0 LIMIT 1;",
                P("@g", guid)));
            if (j != null)
                j.Lines = ListLines(j.JournalId);
            return j;
        }

        public IList<GlJournalLine> ListLines(long journalId)
        {
            return MapLines(Query(
                "SELECT * FROM GlJournalLine WHERE JournalID = @id ORDER BY LineNo;", P("@id", journalId)));
        }

        public IList<GlJournalLine> ListLines(SQLiteConnection con, SQLiteTransaction tr, long journalId)
        {
            return MapLines(Query(con, tr,
                "SELECT * FROM GlJournalLine WHERE JournalID = @id ORDER BY LineNo;", P("@id", journalId)));
        }

        public long InsertJournal(SQLiteConnection con, SQLiteTransaction tr, GlJournal j)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO GlJournal (CompanyID, CenterID, FiscalYearID, FiscalPeriodID, JournalNumber, JournalSource,
  SourceModule, SourceDocumentType, SourceDocumentID, PostingDate, DocumentDate, ReferenceNumber, Description,
  Status, ReversesJournalID, ClientJournalGuid, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy,
  ApprovedAt, ApprovedBy, PostedAt, PostedBy)
VALUES (@cid, @ctr, @fy, @fp, @no, @src, @sm, @sdt, @sdi, @pd, @dd, @ref, @desc, @st, @rev, @guid,
  @del, 1, @ca, @ua, @cb, @ub, @aa, @ab, @pa, @pb);", con, tr))
            {
                BindJournal(cmd, j);
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public bool UpdateJournalConcurrency(SQLiteConnection con, SQLiteTransaction tr, GlJournal j, long expectedRowVersion)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
UPDATE GlJournal SET
  FiscalYearID = @fy, FiscalPeriodID = @fp, JournalNumber = @no, JournalSource = @src,
  SourceModule = @sm, SourceDocumentType = @sdt, SourceDocumentID = @sdi,
  PostingDate = @pd, DocumentDate = @dd, ReferenceNumber = @ref, Description = @desc,
  Status = @st, ReversesJournalID = @rev, ClientJournalGuid = @guid,
  IsDeleted = @del, DeletedAt = @dat, DeletedBy = @dby,
  ApprovedAt = @aa, ApprovedBy = @ab, PostedAt = @pa, PostedBy = @pb,
  RowVersion = RowVersion + 1, UpdatedAt = @ua, UpdatedBy = @ub
WHERE JournalID = @id AND RowVersion = @rv;", con, tr))
            {
                BindJournal(cmd, j);
                cmd.Parameters.AddWithValue("@dat", (object)j.DeletedAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@dby", (object)j.DeletedBy ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", j.JournalId);
                cmd.Parameters.AddWithValue("@rv", expectedRowVersion);
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public long InsertLine(SQLiteConnection con, SQLiteTransaction tr, GlJournalLine l)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO GlJournalLine (JournalID, CompanyID, CenterID, LineNo, AccountID, DebitMinor, CreditMinor,
  CurrencyCode, ExchangeRateMicros, DebitBaseMinor, CreditBaseMinor, Description,
  CostCenterID, ProjectID, PartyID, FundID, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@jid, @cid, @ctr, @ln, @acc, @dr, @cr, @ccy, @fx, @drb, @crb, @desc,
  @cc, @pr, @pty, @fund, 1, @ca, @ua, @cb, @ub);", con, tr))
            {
                BindLine(cmd, l);
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public bool UpdateLineConcurrency(SQLiteConnection con, SQLiteTransaction tr, GlJournalLine l, long expectedRowVersion)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
UPDATE GlJournalLine SET
  AccountID = @acc, DebitMinor = @dr, CreditMinor = @cr, CurrencyCode = @ccy,
  ExchangeRateMicros = @fx, DebitBaseMinor = @drb, CreditBaseMinor = @crb, Description = @desc,
  CostCenterID = @cc, ProjectID = @pr, PartyID = @pty, FundID = @fund,
  RowVersion = RowVersion + 1, UpdatedAt = @ua, UpdatedBy = @ub
WHERE JournalLineID = @id AND RowVersion = @rv;", con, tr))
            {
                BindLine(cmd, l);
                cmd.Parameters.AddWithValue("@id", l.JournalLineId);
                cmd.Parameters.AddWithValue("@rv", expectedRowVersion);
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public void InsertAudit(SQLiteConnection con, SQLiteTransaction tr,
            string operation, string entity, long entityId, string oldValue, string newValue,
            string reason, string username, int centerId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO AccAudit (Operation, EntityName, EntityID, Detail, OldValue, NewValue, Reason,
  Username, MachineName, IPAddress, CenterID)
VALUES (@op, @en, @id, @d, @ov, @nv, @rs, @u, @m, @ip, @cid);", con, tr))
            {
                string detail = operation + " " + entity + " " + entityId;
                int id32 = entityId > int.MaxValue ? 0 : (int)entityId;
                cmd.Parameters.AddWithValue("@op", operation ?? "");
                cmd.Parameters.AddWithValue("@en", entity ?? "");
                cmd.Parameters.AddWithValue("@id", id32);
                cmd.Parameters.AddWithValue("@d", detail);
                cmd.Parameters.AddWithValue("@ov", (object)oldValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nv", (object)newValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rs", (object)reason ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@u", (object)username ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@m", Environment.MachineName);
                cmd.Parameters.AddWithValue("@ip", DBNull.Value);
                cmd.Parameters.AddWithValue("@cid", centerId > 0 ? (object)centerId : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static void BindJournal(SQLiteCommand cmd, GlJournal j)
        {
            cmd.Parameters.AddWithValue("@cid", j.CompanyId);
            cmd.Parameters.AddWithValue("@ctr", j.CenterId);
            cmd.Parameters.AddWithValue("@fy", j.FiscalYearId);
            cmd.Parameters.AddWithValue("@fp", j.FiscalPeriodId);
            cmd.Parameters.AddWithValue("@no", j.JournalNumber ?? "");
            cmd.Parameters.AddWithValue("@src", j.JournalSource ?? LedgerCodes.SourceManual);
            cmd.Parameters.AddWithValue("@sm", (object)j.SourceModule ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sdt", (object)j.SourceDocumentType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sdi", j.SourceDocumentId.HasValue ? (object)j.SourceDocumentId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@pd", j.PostingDate ?? "");
            cmd.Parameters.AddWithValue("@dd", (object)j.DocumentDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ref", (object)j.ReferenceNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@desc", (object)j.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@st", j.Status ?? LedgerCodes.JournalDraft);
            cmd.Parameters.AddWithValue("@rev", j.ReversesJournalId.HasValue ? (object)j.ReversesJournalId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@guid", (object)j.ClientJournalGuid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@del", j.IsDeleted ? 1 : 0);
            cmd.Parameters.AddWithValue("@ca", j.CreatedAt);
            cmd.Parameters.AddWithValue("@ua", j.UpdatedAt);
            cmd.Parameters.AddWithValue("@cb", j.CreatedBy);
            cmd.Parameters.AddWithValue("@ub", j.UpdatedBy);
            cmd.Parameters.AddWithValue("@aa", (object)j.ApprovedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ab", (object)j.ApprovedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pa", (object)j.PostedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pb", (object)j.PostedBy ?? DBNull.Value);
        }

        private static void BindLine(SQLiteCommand cmd, GlJournalLine l)
        {
            cmd.Parameters.AddWithValue("@jid", l.JournalId);
            cmd.Parameters.AddWithValue("@cid", l.CompanyId);
            cmd.Parameters.AddWithValue("@ctr", l.CenterId);
            cmd.Parameters.AddWithValue("@ln", l.LineNo);
            cmd.Parameters.AddWithValue("@acc", l.AccountId);
            cmd.Parameters.AddWithValue("@dr", l.DebitMinor);
            cmd.Parameters.AddWithValue("@cr", l.CreditMinor);
            cmd.Parameters.AddWithValue("@ccy", l.CurrencyCode ?? LedgerCodes.BaseCurrency);
            cmd.Parameters.AddWithValue("@fx", l.ExchangeRateMicros);
            cmd.Parameters.AddWithValue("@drb", l.DebitBaseMinor);
            cmd.Parameters.AddWithValue("@crb", l.CreditBaseMinor);
            cmd.Parameters.AddWithValue("@desc", (object)l.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cc", l.CostCenterId.HasValue ? (object)l.CostCenterId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@pr", l.ProjectId.HasValue ? (object)l.ProjectId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@pty", l.PartyId.HasValue ? (object)l.PartyId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@fund", l.FundId.HasValue ? (object)l.FundId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ca", l.CreatedAt);
            cmd.Parameters.AddWithValue("@ua", l.UpdatedAt);
            cmd.Parameters.AddWithValue("@cb", l.CreatedBy);
            cmd.Parameters.AddWithValue("@ub", l.UpdatedBy);
        }

        private DataTable Query(string sql, params SQLiteParameter[] p)
        {
            return _db.Query(sql, p);
        }

        private DataTable Query(SQLiteConnection con, SQLiteTransaction tr, string sql, params SQLiteParameter[] p)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con, tr))
            {
                if (p != null) cmd.Parameters.AddRange(p);
                using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                {
                    DataTable table = new DataTable();
                    da.Fill(table);
                    return table;
                }
            }
        }

        private DataRow QueryRow(string sql, params SQLiteParameter[] p)
        {
            DataTable t = Query(sql, p);
            return t.Rows.Count == 0 ? null : t.Rows[0];
        }

        private DataRow QueryRow(SQLiteConnection con, SQLiteTransaction tr, string sql, params SQLiteParameter[] p)
        {
            DataTable t = Query(con, tr, sql, p);
            return t.Rows.Count == 0 ? null : t.Rows[0];
        }

        private int ScalarInt(string sql, params SQLiteParameter[] p)
        {
            object v = _db.ExecuteScalar(sql, p);
            if (v == null || v == DBNull.Value) return 0;
            return Convert.ToInt32(v);
        }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        private static bool Flag(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            return Convert.ToInt32(v) != 0;
        }

        private static string Str(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            return Convert.ToString(v);
        }

        private static int Int(object v)
        {
            if (v == null || v == DBNull.Value) return 0;
            return Convert.ToInt32(v);
        }

        private static long Long(object v)
        {
            if (v == null || v == DBNull.Value) return 0;
            return Convert.ToInt64(v);
        }

        private static long? LongN(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            return Convert.ToInt64(v);
        }

        private static int? IntN(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            return Convert.ToInt32(v);
        }

        private static GlCompany MapCompany(DataRow r)
        {
            if (r == null) return null;
            return new GlCompany
            {
                CompanyId = Int(r["CompanyID"]),
                CenterId = Int(r["CenterID"]),
                Code = Str(r["Code"]),
                Name = Str(r["Name"]),
                BaseCurrencyCode = Str(r["BaseCurrencyCode"]),
                MinorUnits = Int(r["MinorUnits"]),
                IsActive = Flag(r["IsActive"]),
                IsDeleted = Flag(r["IsDeleted"]),
                DeletedAt = Str(r["DeletedAt"]),
                DeletedBy = Str(r["DeletedBy"]),
                RowVersion = Long(r["RowVersion"]),
                CreatedAt = Str(r["CreatedAt"]),
                UpdatedAt = Str(r["UpdatedAt"]),
                CreatedBy = Str(r["CreatedBy"]),
                UpdatedBy = Str(r["UpdatedBy"])
            };
        }

        private static GlLedgerSetting MapSetting(DataRow r)
        {
            if (r == null) return null;
            return new GlLedgerSetting
            {
                LedgerSettingId = Long(r["LedgerSettingID"]),
                CompanyId = Int(r["CompanyID"]),
                CenterId = Int(r["CenterID"]),
                RequiresJournalApproval = Flag(r["RequiresJournalApproval"]),
                NextJournalNumber = Long(r["NextJournalNumber"]),
                NumberPrefix = Str(r["NumberPrefix"]),
                IsDeleted = Flag(r["IsDeleted"]),
                RowVersion = Long(r["RowVersion"]),
                CreatedAt = Str(r["CreatedAt"]),
                UpdatedAt = Str(r["UpdatedAt"]),
                CreatedBy = Str(r["CreatedBy"]),
                UpdatedBy = Str(r["UpdatedBy"])
            };
        }

        private static GlCurrency MapCurrency(DataRow r)
        {
            if (r == null) return null;
            return new GlCurrency
            {
                CurrencyId = Long(r["CurrencyID"]),
                CompanyId = Int(r["CompanyID"]),
                CenterId = Int(r["CenterID"]),
                CurrencyCode = Str(r["CurrencyCode"]),
                Name = Str(r["Name"]),
                MinorUnits = Int(r["MinorUnits"]),
                IsActive = Flag(r["IsActive"]),
                IsDeleted = Flag(r["IsDeleted"]),
                RowVersion = Long(r["RowVersion"])
            };
        }

        private static GlExchangeRate MapRate(DataRow r)
        {
            if (r == null) return null;
            return new GlExchangeRate
            {
                ExchangeRateId = Long(r["ExchangeRateID"]),
                CompanyId = Int(r["CompanyID"]),
                CenterId = Int(r["CenterID"]),
                CurrencyCode = Str(r["CurrencyCode"]),
                RateDate = Str(r["RateDate"]),
                RateToBaseMicros = Long(r["RateToBaseMicros"]),
                IsDeleted = Flag(r["IsDeleted"]),
                RowVersion = Long(r["RowVersion"])
            };
        }

        private static GlAccount MapAccount(DataRow r)
        {
            if (r == null) return null;
            return new GlAccount
            {
                AccountId = Long(r["AccountID"]),
                CompanyId = Int(r["CompanyID"]),
                CenterId = Int(r["CenterID"]),
                AccountCode = Str(r["AccountCode"]),
                AccountName = Str(r["AccountName"]),
                AccountTypeCode = Str(r["AccountTypeCode"]),
                ParentAccountId = LongN(r["ParentAccountID"]),
                Level = Int(r["Level"]),
                IsLeaf = Flag(r["IsLeaf"]),
                AllowPosting = Flag(r["AllowPosting"]),
                IsActive = Flag(r["IsActive"]),
                IsContra = Flag(r["IsContra"]),
                ControlCurrencyCode = Str(r["ControlCurrencyCode"]),
                IsDeleted = Flag(r["IsDeleted"]),
                DeletedAt = Str(r["DeletedAt"]),
                DeletedBy = Str(r["DeletedBy"]),
                RowVersion = Long(r["RowVersion"]),
                CreatedAt = Str(r["CreatedAt"]),
                UpdatedAt = Str(r["UpdatedAt"]),
                CreatedBy = Str(r["CreatedBy"]),
                UpdatedBy = Str(r["UpdatedBy"])
            };
        }

        private static IList<GlAccount> MapAccounts(DataTable table)
        {
            List<GlAccount> list = new List<GlAccount>();
            foreach (DataRow row in table.Rows)
                list.Add(MapAccount(row));
            return list;
        }

        private static GlFiscalYear MapYear(DataRow r)
        {
            if (r == null) return null;
            return new GlFiscalYear
            {
                FiscalYearId = Long(r["FiscalYearID"]),
                CompanyId = Int(r["CompanyID"]),
                CenterId = Int(r["CenterID"]),
                Code = Str(r["Code"]),
                Name = Str(r["Name"]),
                CalendarType = Str(r["CalendarType"]),
                StartDate = Str(r["StartDate"]),
                EndDate = Str(r["EndDate"]),
                Status = Str(r["Status"]),
                ClosedAt = Str(r["ClosedAt"]),
                ClosedBy = Str(r["ClosedBy"]),
                LockedAt = Str(r["LockedAt"]),
                LockedBy = Str(r["LockedBy"]),
                IsDeleted = Flag(r["IsDeleted"]),
                DeletedAt = Str(r["DeletedAt"]),
                DeletedBy = Str(r["DeletedBy"]),
                RowVersion = Long(r["RowVersion"]),
                CreatedAt = Str(r["CreatedAt"]),
                UpdatedAt = Str(r["UpdatedAt"]),
                CreatedBy = Str(r["CreatedBy"]),
                UpdatedBy = Str(r["UpdatedBy"])
            };
        }

        private static GlFiscalPeriod MapPeriod(DataRow r)
        {
            if (r == null) return null;
            return new GlFiscalPeriod
            {
                FiscalPeriodId = Long(r["FiscalPeriodID"]),
                CompanyId = Int(r["CompanyID"]),
                CenterId = Int(r["CenterID"]),
                FiscalYearId = Long(r["FiscalYearID"]),
                PeriodNo = Int(r["PeriodNo"]),
                Name = Str(r["Name"]),
                StartDate = Str(r["StartDate"]),
                EndDate = Str(r["EndDate"]),
                Status = Str(r["Status"]),
                IsDeleted = Flag(r["IsDeleted"]),
                RowVersion = Long(r["RowVersion"]),
                CreatedAt = Str(r["CreatedAt"]),
                UpdatedAt = Str(r["UpdatedAt"]),
                CreatedBy = Str(r["CreatedBy"]),
                UpdatedBy = Str(r["UpdatedBy"])
            };
        }

        private static GlJournal MapJournal(DataRow r)
        {
            if (r == null) return null;
            return new GlJournal
            {
                JournalId = Long(r["JournalID"]),
                CompanyId = Int(r["CompanyID"]),
                CenterId = Int(r["CenterID"]),
                FiscalYearId = Long(r["FiscalYearID"]),
                FiscalPeriodId = Long(r["FiscalPeriodID"]),
                JournalNumber = Str(r["JournalNumber"]),
                JournalSource = Str(r["JournalSource"]),
                SourceModule = Str(r["SourceModule"]),
                SourceDocumentType = Str(r["SourceDocumentType"]),
                SourceDocumentId = LongN(r["SourceDocumentID"]),
                PostingDate = Str(r["PostingDate"]),
                DocumentDate = Str(r["DocumentDate"]),
                ReferenceNumber = Str(r["ReferenceNumber"]),
                Description = Str(r["Description"]),
                Status = Str(r["Status"]),
                ReversesJournalId = LongN(r["ReversesJournalID"]),
                ClientJournalGuid = Str(r["ClientJournalGuid"]),
                IsDeleted = Flag(r["IsDeleted"]),
                DeletedAt = Str(r["DeletedAt"]),
                DeletedBy = Str(r["DeletedBy"]),
                RowVersion = Long(r["RowVersion"]),
                CreatedAt = Str(r["CreatedAt"]),
                UpdatedAt = Str(r["UpdatedAt"]),
                CreatedBy = Str(r["CreatedBy"]),
                UpdatedBy = Str(r["UpdatedBy"]),
                ApprovedAt = Str(r["ApprovedAt"]),
                ApprovedBy = Str(r["ApprovedBy"]),
                PostedAt = Str(r["PostedAt"]),
                PostedBy = Str(r["PostedBy"])
            };
        }

        private static IList<GlJournalLine> MapLines(DataTable table)
        {
            List<GlJournalLine> list = new List<GlJournalLine>();
            foreach (DataRow r in table.Rows)
            {
                list.Add(new GlJournalLine
                {
                    JournalLineId = Long(r["JournalLineID"]),
                    JournalId = Long(r["JournalID"]),
                    CompanyId = Int(r["CompanyID"]),
                    CenterId = Int(r["CenterID"]),
                    LineNo = Int(r["LineNo"]),
                    AccountId = Long(r["AccountID"]),
                    DebitMinor = Long(r["DebitMinor"]),
                    CreditMinor = Long(r["CreditMinor"]),
                    CurrencyCode = Str(r["CurrencyCode"]),
                    ExchangeRateMicros = Long(r["ExchangeRateMicros"]),
                    DebitBaseMinor = Long(r["DebitBaseMinor"]),
                    CreditBaseMinor = Long(r["CreditBaseMinor"]),
                    Description = Str(r["Description"]),
                    CostCenterId = LongN(r["CostCenterID"]),
                    ProjectId = LongN(r["ProjectID"]),
                    PartyId = IntN(r["PartyID"]),
                    FundId = IntN(r["FundID"]),
                    RowVersion = Long(r["RowVersion"]),
                    CreatedAt = Str(r["CreatedAt"]),
                    UpdatedAt = Str(r["UpdatedAt"]),
                    CreatedBy = Str(r["CreatedBy"]),
                    UpdatedBy = Str(r["UpdatedBy"])
                });
            }
            return list;
        }
    }
}

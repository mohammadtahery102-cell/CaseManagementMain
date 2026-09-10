using System;
using System.Collections.Generic;
using System.Data;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting.Ledger.Infrastructure
{
    /// <summary>
    /// Read-only Acc* access for the GL adapter. Does not write Acc rows.
    /// </summary>
    public class CashBookReadRepository
    {
        private readonly DatabaseHelper _db;

        public CashBookReadRepository() : this(new DatabaseHelper()) { }

        public CashBookReadRepository(DatabaseHelper db)
        {
            _db = db;
        }

        public CashBookTxn GetTransaction(long txnId)
        {
            DataTable table = _db.Query("SELECT * FROM AccTransaction WHERE TxnID = @id;", new System.Data.SQLite.SQLiteParameter("@id", txnId));
            if (table.Rows.Count == 0) return null;
            return Map(table.Rows[0], false, null);
        }

        public IList<CashBookTxn> List(int centerFilter)
        {
            DataTable table = _db.Query(@"
SELECT t.*,
  CASE WHEN j.JournalID IS NULL THEN 0 ELSE 1 END AS GlPosted,
  j.JournalID AS GlJournalID
FROM AccTransaction t
LEFT JOIN GlJournal j ON j.SourceModule = @mod AND j.SourceDocumentType = @typ
  AND j.SourceDocumentID = t.TxnID AND j.IsDeleted = 0 AND j.Status = 'Posted'
WHERE (@ctr = 0 OR t.CenterID = @ctr)
ORDER BY t.TxnID DESC
LIMIT 400;",
                new System.Data.SQLite.SQLiteParameter("@mod", LedgerCodes.SourceCashBook),
                new System.Data.SQLite.SQLiteParameter("@typ", LedgerCodes.DocAccTransaction),
                new System.Data.SQLite.SQLiteParameter("@ctr", centerFilter));
            List<CashBookTxn> list = new List<CashBookTxn>();
            foreach (DataRow r in table.Rows)
            {
                bool posted = Convert.ToInt64(r["GlPosted"]) != 0;
                long? jid = r["GlJournalID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["GlJournalID"]);
                list.Add(Map(r, posted, jid));
            }
            return list;
        }

        public IList<KeyValuePair<long, string>> ListFunds(int centerFilter)
        {
            return ListPair("SELECT FundID AS Id, Name AS Name FROM AccFund WHERE IsActive = 1 AND (@c = 0 OR CenterID = @c OR CenterID IS NULL) ORDER BY FundID;", centerFilter);
        }

        public IList<KeyValuePair<long, string>> ListIncomeCategories()
        {
            return ListPair("SELECT CatID AS Id, Name AS Name FROM AccIncomeCategory WHERE IsActive = 1 ORDER BY SortOrder, CatID;", 0);
        }

        public IList<KeyValuePair<long, string>> ListExpenseCategories()
        {
            return ListPair("SELECT CatID AS Id, Name AS Name FROM AccExpenseCategory WHERE IsActive = 1 ORDER BY SortOrder, CatID;", 0);
        }

        private IList<KeyValuePair<long, string>> ListPair(string sql, int center)
        {
            DataTable table = center == 0 && !sql.Contains("@c")
                ? _db.Query(sql)
                : _db.Query(sql, new System.Data.SQLite.SQLiteParameter("@c", center));
            List<KeyValuePair<long, string>> list = new List<KeyValuePair<long, string>>();
            foreach (DataRow r in table.Rows)
                list.Add(new KeyValuePair<long, string>(Convert.ToInt64(r["Id"]), r["Name"].ToString()));
            return list;
        }

        private static CashBookTxn Map(DataRow r, bool posted, long? journalId)
        {
            string raw = r["TxnDate"] == DBNull.Value ? "" : r["TxnDate"].ToString();
            DateTime greg = ToGregorianDate(raw);
            string iso = greg == DateTime.MinValue
                ? null
                : greg.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            int center = r["CenterID"] == DBNull.Value ? 0 : Convert.ToInt32(r["CenterID"]);
            CashBookTxn t = new CashBookTxn();
            t.TxnId = Convert.ToInt64(r["TxnID"]);
            t.DocNo = r["DocNo"] == DBNull.Value ? "" : r["DocNo"].ToString();
            t.TxnDateRaw = raw;
            t.PostingDateIso = iso;
            t.Direction = r["Direction"] == DBNull.Value ? "" : r["Direction"].ToString();
            t.CenterId = center;
            t.FundId = r["FundID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["FundID"]);
            t.CategoryType = r["CategoryType"] == DBNull.Value ? "" : r["CategoryType"].ToString();
            t.CategoryId = r["CategoryID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["CategoryID"]);
            t.AmountMajor = r["Amount"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Amount"]);
            t.Description = r["Description"] == DBNull.Value ? "" : r["Description"].ToString();
            t.IsReversed = r.Table.Columns.Contains("IsReversed") && r["IsReversed"] != DBNull.Value && Convert.ToInt32(r["IsReversed"]) != 0;
            t.PartyId = r["PartyID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PartyID"]);
            t.GlPosted = posted;
            t.GlJournalId = journalId;
            return t;
        }

        private static DateTime ToGregorianDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DateTime.MinValue;
            string s = raw.Trim().Replace('-', '/');
            string[] parts = s.Split('/');
            int y;
            if (parts.Length >= 1 && int.TryParse(parts[0], out y) && y > 0 && y < 1700)
            {
                try { return PersianDateHelper.ParsePersianDate(raw).Date; }
                catch { return DateTime.MinValue; }
            }
            return PersianDateHelper.ParseStoredDate(raw, DateTime.MinValue);
        }
    }
}

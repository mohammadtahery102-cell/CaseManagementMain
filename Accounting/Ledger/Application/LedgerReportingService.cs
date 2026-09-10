using System.Collections.Generic;
using System.Data;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;

namespace CaseManagement.Accounting.Ledger.Application
{
    public class LedgerReportingService : ILedgerReporting
    {
        private readonly LedgerRepository _repo;

        public LedgerReportingService() : this(new LedgerRepository()) { }

        public LedgerReportingService(LedgerRepository repo)
        {
            _repo = repo;
        }

        public IList<TrialBalanceRow> GetTrialBalance(LedgerReportQuery query, ILedgerIdentity identity)
        {
            List<TrialBalanceRow> empty = new List<TrialBalanceRow>();
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return empty;

            int companyId = Company(query, identity);
            int center = Center(query, identity);
            string from = LedgerTime.DateOnly(query != null ? query.FromDate : null) ?? "";
            string to = LedgerTime.DateOnly(query != null ? query.ToDate : null) ?? "9999-12-31";
            if (from.Length == 0) from = "0001-01-01";
            ApplyFiscalYear(query, companyId, ref from, ref to);
            long cc = query != null ? query.CostCenterId : 0;
            long pr = query != null ? query.ProjectId : 0;

            DataTable table = _repo.QueryPostedLineSums(companyId, center, from, to, cc, pr);
            List<TrialBalanceRow> rows = new List<TrialBalanceRow>();
            foreach (DataRow r in table.Rows)
            {
                long openDr = ToLong(r["OpenDr"]);
                long openCr = ToLong(r["OpenCr"]);
                long perDr = ToLong(r["PeriodDr"]);
                long perCr = ToLong(r["PeriodCr"]);
                long openNet = openDr - openCr;
                long closeNet = openNet + (perDr - perCr);
                TrialBalanceRow row = new TrialBalanceRow();
                row.AccountId = ToLong(r["AccountID"]);
                row.AccountCode = r["AccountCode"].ToString();
                row.AccountName = r["AccountName"].ToString();
                row.AccountTypeCode = r["AccountTypeCode"].ToString();
                row.IsContra = ToLong(r["IsContra"]) != 0;
                long openDebit, openCredit, closeDebit, closeCredit;
                Split(openNet, out openDebit, out openCredit);
                Split(closeNet, out closeDebit, out closeCredit);
                row.OpeningDebit = openDebit;
                row.OpeningCredit = openCredit;
                row.PeriodDebit = perDr;
                row.PeriodCredit = perCr;
                row.ClosingDebit = closeDebit;
                row.ClosingCredit = closeCredit;
                if (row.OpeningDebit == 0 && row.OpeningCredit == 0 && row.PeriodDebit == 0 && row.PeriodCredit == 0)
                    continue;
                rows.Add(row);
            }
            return rows;
        }

        public IList<GeneralLedgerLineRow> GetGeneralLedger(LedgerReportQuery query, ILedgerIdentity identity)
        {
            List<GeneralLedgerLineRow> list = new List<GeneralLedgerLineRow>();
            if (identity == null || !identity.HasPermission(LedgerPermissions.View) || query == null || query.AccountId <= 0)
                return list;

            int companyId = Company(query, identity);
            int center = Center(query, identity);
            string from = LedgerTime.DateOnly(query.FromDate) ?? "";
            string to = LedgerTime.DateOnly(query.ToDate) ?? "";
            ApplyFiscalYear(query, companyId, ref from, ref to);
            long running = _repo.QueryOpeningNet(companyId, center, query.AccountId, from, query.CostCenterId, query.ProjectId);
            DataTable table = _repo.QueryGeneralLedgerLines(companyId, center, query.AccountId, from, to, query.CostCenterId, query.ProjectId);
            foreach (DataRow r in table.Rows)
            {
                long dr = ToLong(r["DebitBaseMinor"]);
                long cr = ToLong(r["CreditBaseMinor"]);
                running += dr - cr;
                GeneralLedgerLineRow row = new GeneralLedgerLineRow();
                row.PostingDate = r["PostingDate"].ToString();
                row.JournalNumber = r["JournalNumber"].ToString();
                row.Description = r["Description"] == null || r["Description"] == System.DBNull.Value
                    ? "" : r["Description"].ToString();
                row.DebitBaseMinor = dr;
                row.CreditBaseMinor = cr;
                row.RunningNet = running;
                list.Add(row);
            }
            return list;
        }

        public BalanceSheetResult GetBalanceSheet(LedgerReportQuery query, ILedgerIdentity identity)
        {
            BalanceSheetResult result = new BalanceSheetResult();
            result.Assets = new List<StatementLine>();
            result.Liabilities = new List<StatementLine>();
            result.Equity = new List<StatementLine>();
            IList<TrialBalanceRow> tb = GetTrialBalance(query, identity);
            long pnl = 0;
            for (int i = 0; i < tb.Count; i++)
            {
                TrialBalanceRow t = tb[i];
                long net = t.ClosingDebit - t.ClosingCredit;
                if (t.IsContra) net = -net;
                string type = t.AccountTypeCode ?? "";
                if (type == LedgerCodes.TypeRevenue)
                    pnl += -net;
                else if (type == LedgerCodes.TypeExpense)
                    pnl -= net;
                else if (type == LedgerCodes.TypeAsset)
                    result.AssetTotal = Add(result.Assets, t, net, result.AssetTotal);
                else if (type == LedgerCodes.TypeLiability)
                    result.LiabilityTotal = Add(result.Liabilities, t, -net, result.LiabilityTotal);
                else if (type == LedgerCodes.TypeEquity)
                    result.EquityTotal = Add(result.Equity, t, -net, result.EquityTotal);
            }
            result.CurrentPeriodNetIncome = pnl;
            result.EquityTotal += pnl;
            result.Equity.Add(new StatementLine
            {
                AccountCode = "",
                AccountName = "سود (زیان) دوره",
                AccountTypeCode = LedgerCodes.TypeEquity,
                AmountMinor = pnl
            });
            result.EquationHolds = result.AssetTotal == (result.LiabilityTotal + result.EquityTotal);
            return result;
        }

        public ProfitAndLossResult GetProfitAndLoss(LedgerReportQuery query, ILedgerIdentity identity)
        {
            ProfitAndLossResult result = new ProfitAndLossResult();
            result.Revenue = new List<StatementLine>();
            result.Expenses = new List<StatementLine>();
            IList<TrialBalanceRow> tb = GetTrialBalance(query, identity);
            for (int i = 0; i < tb.Count; i++)
            {
                TrialBalanceRow t = tb[i];
                long periodNet = t.PeriodDebit - t.PeriodCredit;
                if (t.AccountTypeCode == LedgerCodes.TypeRevenue)
                {
                    long amt = -periodNet;
                    result.RevenueTotal = Add(result.Revenue, t, amt, result.RevenueTotal);
                }
                else if (t.AccountTypeCode == LedgerCodes.TypeExpense)
                {
                    result.ExpenseTotal = Add(result.Expenses, t, periodNet, result.ExpenseTotal);
                }
            }
            result.NetIncome = result.RevenueTotal - result.ExpenseTotal;
            return result;
        }

        public IList<CurrencyPositionRow> GetCurrencyPositions(LedgerReportQuery query, ILedgerIdentity identity)
        {
            List<CurrencyPositionRow> list = new List<CurrencyPositionRow>();
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return list;

            int companyId = Company(query, identity);
            int center = Center(query, identity);
            string to = LedgerTime.DateOnly(query != null ? query.ToDate : null) ?? "";
            string fromUnused = LedgerTime.DateOnly(query != null ? query.FromDate : null) ?? "";
            ApplyFiscalYear(query, companyId, ref fromUnused, ref to);
            GlCompany co = _repo.GetCompany(companyId);
            string baseCcy = co != null ? co.BaseCurrencyCode : LedgerCodes.BaseCurrency;
            System.Data.DataTable table = _repo.QueryCurrencyNets(companyId, center, to, baseCcy);
            foreach (System.Data.DataRow r in table.Rows)
            {
                CurrencyPositionRow row = new CurrencyPositionRow();
                row.AccountId = ToLong(r["AccountID"]);
                row.AccountCode = r["AccountCode"].ToString();
                row.AccountName = r["AccountName"].ToString();
                row.AccountTypeCode = r["AccountTypeCode"].ToString();
                row.CurrencyCode = r["CurrencyCode"].ToString();
                row.TransactionNetMinor = ToLong(r["TxnNet"]);
                row.BookedBaseMinor = ToLong(r["BaseNet"]);
                long? rate = _repo.GetRateToBaseMicros(companyId, row.CurrencyCode, to);
                row.RateToBaseMicros = rate.HasValue ? rate.Value : 0;
                row.RevaluedBaseMinor = rate.HasValue
                    ? row.TransactionNetMinor * rate.Value / LedgerCodes.RateOne
                    : 0;
                row.UnrealizedBaseMinor = row.RevaluedBaseMinor - row.BookedBaseMinor;
                list.Add(row);
            }
            return list;
        }

        private static long Add(IList<StatementLine> list, TrialBalanceRow t, long amount, long total)
        {
            if (amount == 0) return total;
            list.Add(new StatementLine
            {
                AccountCode = t.AccountCode,
                AccountName = t.AccountName,
                AccountTypeCode = t.AccountTypeCode,
                AmountMinor = amount
            });
            return total + amount;
        }

        private static void Split(long net, out long debit, out long credit)
        {
            if (net >= 0) { debit = net; credit = 0; }
            else { debit = 0; credit = -net; }
        }

        private void ApplyFiscalYear(LedgerReportQuery query, int companyId, ref string from, ref string to)
        {
            if (query == null || query.FiscalYearId <= 0) return;
            GlFiscalYear y = _repo.GetYear(query.FiscalYearId);
            if (y == null || y.IsDeleted || y.CompanyId != companyId) return;
            from = y.StartDate;
            to = y.EndDate;
        }

        private static int Company(LedgerReportQuery query, ILedgerIdentity identity)
        {
            if (query != null && query.CompanyId > 0) return query.CompanyId;
            return identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId;
        }

        private static int Center(LedgerReportQuery query, ILedgerIdentity identity)
        {
            if (!(identity.IsSuperAdmin && identity.CenterId == 0))
                return identity.CenterId;
            if (query != null && query.CenterId > 0) return query.CenterId;
            return 0;
        }

        private static long ToLong(object v)
        {
            if (v == null || v == System.DBNull.Value) return 0;
            return System.Convert.ToInt64(v);
        }
    }
}

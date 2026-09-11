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
            long opening = _repo.QueryOpeningNet(companyId, center, query.AccountId, from, query.CostCenterId, query.ProjectId);
            list.Add(GlBalanceRow(LedgerCodes.LineOpening, from, opening));
            long running = opening;
            DataTable table = _repo.QueryGeneralLedgerLines(companyId, center, query.AccountId, from, to, query.CostCenterId, query.ProjectId);
            foreach (DataRow r in table.Rows)
            {
                long dr = ToLong(r["DebitBaseMinor"]);
                long cr = ToLong(r["CreditBaseMinor"]);
                running += dr - cr;
                GeneralLedgerLineRow row = new GeneralLedgerLineRow();
                row.PostingDate = r["PostingDate"].ToString();
                row.JournalNumber = r["JournalNumber"].ToString();
                row.Description = Str(r, "Description");
                row.DebitBaseMinor = dr;
                row.CreditBaseMinor = cr;
                row.RunningNet = running;
                row.JournalId = ToLong(r, "JournalID");
                row.LineKind = LedgerCodes.LineMovement;
                row.Status = Str(r, "Status");
                row.SourceModule = Str(r, "SourceModule");
                row.SourceDocumentType = Str(r, "SourceDocumentType");
                long src = ToLong(r, "SourceDocumentID");
                row.SourceDocumentId = src > 0 ? (long?)src : null;
                list.Add(row);
            }
            list.Add(GlBalanceRow(LedgerCodes.LineClosing, to, running));
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

        public IList<DaybookLineRow> GetDaybook(LedgerReportQuery query, ILedgerIdentity identity)
        {
            return LoadDaybook(query, identity);
        }

        public IList<DaybookLineRow> GetSubsidiary(LedgerReportQuery query, ILedgerIdentity identity)
        {
            return LoadSubsidiary(query, identity);
        }

        public IList<DaybookLineRow> GetDetailLedger(LedgerReportQuery query, ILedgerIdentity identity)
        {
            List<DaybookLineRow> list = new List<DaybookLineRow>();
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return list;

            int companyId = Company(query, identity);
            int center = Center(query, identity);
            string from = LedgerTime.DateOnly(query != null ? query.FromDate : null) ?? "";
            string to = LedgerTime.DateOnly(query != null ? query.ToDate : null) ?? "";
            ApplyFiscalYear(query, companyId, ref from, ref to);
            long accountId = query != null ? query.AccountId : 0;
            long cc = query != null ? query.CostCenterId : 0;
            long pr = query != null ? query.ProjectId : 0;
            long party = query != null ? query.PartyId : 0;
            long fund = query != null ? query.FundId : 0;
            string kind = query != null && !string.IsNullOrWhiteSpace(query.DetailKind)
                ? query.DetailKind : LedgerCodes.DetailParty;

            Dictionary<string, AccBucket> buckets = new Dictionary<string, AccBucket>();
            DataTable opens = _repo.QueryOpeningNetsByDimension(companyId, center, from, accountId, cc, pr, party, fund);
            foreach (DataRow r in opens.Rows)
            {
                DaybookLineRow seed = MapDaybook(r, 0);
                ApplyDimension(seed, r, kind);
                string key = seed.DimensionKey + "\t" + seed.AccountId;
                AccBucket b = EnsureBucket(buckets, key, seed.AccountId, seed.AccountCode, seed.AccountName);
                b.Opening = ToLong(r["OpenNet"]);
                b.DimensionKey = seed.DimensionKey;
                b.DimensionName = seed.DimensionName;
            }

            DataTable table = _repo.QueryDaybookLines(companyId, center, accountId, from, to, cc, pr, party, fund);
            foreach (DataRow r in table.Rows)
            {
                DaybookLineRow move = MapDaybook(r, 0);
                ApplyDimension(move, r, kind);
                string key = move.DimensionKey + "\t" + move.AccountId;
                AccBucket b = EnsureBucket(buckets, key, move.AccountId, move.AccountCode, move.AccountName);
                b.DimensionKey = move.DimensionKey;
                b.DimensionName = move.DimensionName;
                b.Lines.Add(move);
            }

            List<AccBucket> ordered = new List<AccBucket>(buckets.Values);
            ordered.Sort(delegate (AccBucket a, AccBucket b)
            {
                int d = string.CompareOrdinal(a.DimensionName ?? "", b.DimensionName ?? "");
                if (d != 0) return d;
                return string.CompareOrdinal(a.Code ?? "", b.Code ?? "");
            });
            for (int i = 0; i < ordered.Count; i++)
                EmitBucket(list, ordered[i], from, to, true);
            return list;
        }

        public IList<DocumentFlowRow> GetDocumentFlow(LedgerReportQuery query, ILedgerIdentity identity)
        {
            List<DocumentFlowRow> list = new List<DocumentFlowRow>();
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return list;

            int companyId = Company(query, identity);
            int center = Center(query, identity);
            string from = LedgerTime.DateOnly(query != null ? query.FromDate : null) ?? "";
            string to = LedgerTime.DateOnly(query != null ? query.ToDate : null) ?? "";
            ApplyFiscalYear(query, companyId, ref from, ref to);
            DataTable table = _repo.QueryDocumentFlow(companyId, center, from, to);
            foreach (DataRow r in table.Rows)
            {
                DocumentFlowRow row = new DocumentFlowRow();
                row.JournalId = ToLong(r["JournalID"]);
                row.JournalNumber = Str(r, "JournalNumber");
                row.PostingDate = Str(r, "PostingDate");
                row.Description = Str(r, "Description");
                row.Status = Str(r, "Status");
                row.JournalSource = Str(r, "JournalSource");
                row.SourceModule = Str(r, "SourceModule");
                row.SourceDocumentType = Str(r, "SourceDocumentType");
                long src = ToLong(r, "SourceDocumentID");
                row.SourceDocumentId = src > 0 ? (long?)src : null;
                long rev = ToLong(r, "ReversesJournalID");
                row.ReversesJournalId = rev > 0 ? (long?)rev : null;
                row.DebitBaseMinor = ToLong(r["DebitBaseMinor"]);
                row.CreditBaseMinor = ToLong(r["CreditBaseMinor"]);
                list.Add(row);
            }
            return list;
        }

        public AccountLedgerSummary GetAccountSummary(LedgerReportQuery query, ILedgerIdentity identity)
        {
            AccountLedgerSummary s = new AccountLedgerSummary();
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return s;

            if (query != null && query.AccountId > 0)
            {
                IList<GeneralLedgerLineRow> gl = GetGeneralLedger(query, identity);
                for (int i = 0; i < gl.Count; i++)
                {
                    GeneralLedgerLineRow row = gl[i];
                    if (row.LineKind == LedgerCodes.LineOpening)
                        s.OpeningNet = row.RunningNet;
                    else if (row.LineKind == LedgerCodes.LineClosing)
                        s.ClosingNet = row.RunningNet;
                    else
                    {
                        s.PeriodDebit += row.DebitBaseMinor;
                        s.PeriodCredit += row.CreditBaseMinor;
                        s.MovementCount++;
                    }
                }
            }
            else
            {
                IList<DaybookLineRow> day = GetDaybook(query, identity);
                for (int i = 0; i < day.Count; i++)
                {
                    s.PeriodDebit += day[i].DebitBaseMinor;
                    s.PeriodCredit += day[i].CreditBaseMinor;
                    s.MovementCount++;
                }
            }
            s.OpeningPlusMovementEqualsClosing = s.OpeningNet + (s.PeriodDebit - s.PeriodCredit) == s.ClosingNet;
            s.PeriodDebitsEqualCredits = s.PeriodDebit == s.PeriodCredit;
            return s;
        }

        public CashFlowResult GetCashFlow(LedgerReportQuery query, ILedgerIdentity identity)
        {
            CashFlowResult result = new CashFlowResult();
            result.Lines = new List<StatementLine>();
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return result;

            IList<TrialBalanceRow> tb = GetTrialBalance(query, identity);
            for (int i = 0; i < tb.Count; i++)
            {
                TrialBalanceRow t = tb[i];
                if (!IsCashLike(t)) continue;
                long openNet = t.OpeningDebit - t.OpeningCredit;
                long closeNet = t.ClosingDebit - t.ClosingCredit;
                result.OpeningMinor += openNet;
                result.InflowMinor += t.PeriodDebit;
                result.OutflowMinor += t.PeriodCredit;
                result.ClosingMinor += closeNet;
                result.Lines.Add(new StatementLine
                {
                    AccountCode = t.AccountCode,
                    AccountName = t.AccountName,
                    AccountTypeCode = t.AccountTypeCode,
                    AmountMinor = closeNet
                });
            }
            return result;
        }

        private IList<DaybookLineRow> LoadDaybook(LedgerReportQuery query, ILedgerIdentity identity)
        {
            List<DaybookLineRow> list = new List<DaybookLineRow>();
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return list;

            int companyId = Company(query, identity);
            int center = Center(query, identity);
            string from = LedgerTime.DateOnly(query != null ? query.FromDate : null) ?? "";
            string to = LedgerTime.DateOnly(query != null ? query.ToDate : null) ?? "";
            ApplyFiscalYear(query, companyId, ref from, ref to);
            long accountId = query != null ? query.AccountId : 0;
            long cc = query != null ? query.CostCenterId : 0;
            long pr = query != null ? query.ProjectId : 0;
            long party = query != null ? query.PartyId : 0;
            long fund = query != null ? query.FundId : 0;
            DataTable table = _repo.QueryDaybookLines(companyId, center, accountId, from, to, cc, pr, party, fund);
            foreach (DataRow r in table.Rows)
            {
                DaybookLineRow row = MapDaybook(r, 0);
                row.LineKind = LedgerCodes.LineMovement;
                list.Add(row);
            }
            return list;
        }

        private IList<DaybookLineRow> LoadSubsidiary(LedgerReportQuery query, ILedgerIdentity identity)
        {
            List<DaybookLineRow> list = new List<DaybookLineRow>();
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return list;

            int companyId = Company(query, identity);
            int center = Center(query, identity);
            string from = LedgerTime.DateOnly(query != null ? query.FromDate : null) ?? "";
            string to = LedgerTime.DateOnly(query != null ? query.ToDate : null) ?? "";
            ApplyFiscalYear(query, companyId, ref from, ref to);
            long accountId = query != null ? query.AccountId : 0;
            long cc = query != null ? query.CostCenterId : 0;
            long pr = query != null ? query.ProjectId : 0;
            long party = query != null ? query.PartyId : 0;
            long fund = query != null ? query.FundId : 0;

            Dictionary<long, AccBucket> buckets = new Dictionary<long, AccBucket>();
            DataTable opens = _repo.QueryOpeningNets(companyId, center, from, accountId, cc, pr, party, fund);
            foreach (DataRow r in opens.Rows)
            {
                long id = ToLong(r["AccountID"]);
                AccBucket b = EnsureBucket(buckets, id.ToString(), id, Str(r, "AccountCode"), Str(r, "AccountName"));
                b.Opening = ToLong(r["OpenNet"]);
            }

            DataTable table = _repo.QueryDaybookLines(companyId, center, accountId, from, to, cc, pr, party, fund);
            foreach (DataRow r in table.Rows)
            {
                DaybookLineRow move = MapDaybook(r, 0);
                AccBucket b = EnsureBucket(buckets, move.AccountId.ToString(), move.AccountId, move.AccountCode, move.AccountName);
                b.Lines.Add(move);
            }

            List<AccBucket> ordered = new List<AccBucket>(buckets.Values);
            ordered.Sort(delegate (AccBucket a, AccBucket b)
            {
                return string.CompareOrdinal(a.Code ?? "", b.Code ?? "");
            });
            for (int i = 0; i < ordered.Count; i++)
                EmitBucket(list, ordered[i], from, to, false);
            return list;
        }

        private static AccBucket EnsureBucket(Dictionary<string, AccBucket> buckets, string key, long accountId, string code, string name)
        {
            AccBucket b;
            if (!buckets.TryGetValue(key, out b))
            {
                b = new AccBucket();
                b.AccountId = accountId;
                b.Code = code;
                b.Name = name;
                b.Lines = new List<DaybookLineRow>();
                buckets[key] = b;
            }
            return b;
        }

        private static AccBucket EnsureBucket(Dictionary<long, AccBucket> buckets, string unused, long accountId, string code, string name)
        {
            AccBucket b;
            if (!buckets.TryGetValue(accountId, out b))
            {
                b = new AccBucket();
                b.AccountId = accountId;
                b.Code = code;
                b.Name = name;
                b.Lines = new List<DaybookLineRow>();
                buckets[accountId] = b;
            }
            return b;
        }

        private static void EmitBucket(List<DaybookLineRow> list, AccBucket bucket, string from, string to, bool withDim)
        {
            if (bucket == null) return;
            if (bucket.Opening == 0 && (bucket.Lines == null || bucket.Lines.Count == 0)) return;
            long running = bucket.Opening;
            list.Add(DayBalanceRow(LedgerCodes.LineOpening, from, bucket, running, withDim));
            if (bucket.Lines != null)
            {
                for (int i = 0; i < bucket.Lines.Count; i++)
                {
                    DaybookLineRow move = bucket.Lines[i];
                    running += move.DebitBaseMinor - move.CreditBaseMinor;
                    move.RunningNet = running;
                    move.LineKind = LedgerCodes.LineMovement;
                    if (withDim)
                    {
                        move.DimensionKey = bucket.DimensionKey;
                        move.DimensionName = bucket.DimensionName;
                    }
                    list.Add(move);
                }
            }
            list.Add(DayBalanceRow(LedgerCodes.LineClosing, to, bucket, running, withDim));
        }

        private static DaybookLineRow MapDaybook(DataRow r, long running)
        {
            DaybookLineRow row = new DaybookLineRow();
            row.PostingDate = Str(r, "PostingDate");
            row.JournalNumber = Str(r, "JournalNumber");
            row.Status = Str(r, "Status");
            row.AccountCode = Str(r, "AccountCode");
            row.AccountName = Str(r, "AccountName");
            row.Description = Str(r, "Description");
            row.DebitBaseMinor = ToLong(r, "DebitBaseMinor");
            row.CreditBaseMinor = ToLong(r, "CreditBaseMinor");
            row.RunningNet = running;
            row.JournalId = ToLong(r, "JournalID");
            row.AccountId = ToLong(r, "AccountID");
            row.SourceModule = Str(r, "SourceModule");
            row.SourceDocumentType = Str(r, "SourceDocumentType");
            long src = ToLong(r, "SourceDocumentID");
            row.SourceDocumentId = src > 0 ? (long?)src : null;
            row.PartyId = ToLong(r, "PartyID");
            row.FundId = ToLong(r, "FundID");
            row.CostCenterId = ToLong(r, "CostCenterID");
            row.ProjectId = ToLong(r, "ProjectID");
            row.LineKind = LedgerCodes.LineMovement;
            return row;
        }

        private static void ApplyDimension(DaybookLineRow row, DataRow r, string kind)
        {
            if (kind == LedgerCodes.DetailFund)
            {
                row.DimensionKey = row.FundId.ToString();
                string name = Str(r, "FundName");
                row.DimensionName = name.Length > 0 ? name : (row.FundId > 0 ? ("صندوق " + row.FundId) : "بدون تفصیل");
            }
            else if (kind == LedgerCodes.DetailCostCenter)
            {
                row.DimensionKey = row.CostCenterId.ToString();
                string name = Str(r, "CostCenterName");
                row.DimensionName = name.Length > 0 ? name : (row.CostCenterId > 0 ? ("مرکز " + row.CostCenterId) : "بدون تفصیل");
            }
            else if (kind == LedgerCodes.DetailProject)
            {
                row.DimensionKey = row.ProjectId.ToString();
                string name = Str(r, "ProjectName");
                row.DimensionName = name.Length > 0 ? name : (row.ProjectId > 0 ? ("پروژه " + row.ProjectId) : "بدون تفصیل");
            }
            else
            {
                row.DimensionKey = row.PartyId.ToString();
                string name = Str(r, "PartyName");
                row.DimensionName = name.Length > 0 ? name : (row.PartyId > 0 ? ("طرف‌حساب " + row.PartyId) : "بدون تفصیل");
            }
        }

        private static GeneralLedgerLineRow GlBalanceRow(string kind, string date, long net)
        {
            long dr, cr;
            Split(net, out dr, out cr);
            GeneralLedgerLineRow row = new GeneralLedgerLineRow();
            row.PostingDate = date ?? "";
            row.JournalNumber = "";
            row.Description = kind == LedgerCodes.LineOpening ? "مانده افتتاحیه" : "مانده اختتامیه";
            row.DebitBaseMinor = dr;
            row.CreditBaseMinor = cr;
            row.RunningNet = net;
            row.LineKind = kind;
            return row;
        }

        private static DaybookLineRow DayBalanceRow(string kind, string date, AccBucket bucket, long net, bool withDim)
        {
            long dr, cr;
            Split(net, out dr, out cr);
            DaybookLineRow row = new DaybookLineRow();
            row.PostingDate = date ?? "";
            row.JournalNumber = "";
            row.AccountId = bucket.AccountId;
            row.AccountCode = bucket.Code;
            row.AccountName = bucket.Name;
            row.Description = kind == LedgerCodes.LineOpening ? "مانده افتتاحیه" : "مانده اختتامیه";
            row.DebitBaseMinor = dr;
            row.CreditBaseMinor = cr;
            row.RunningNet = net;
            row.LineKind = kind;
            if (withDim)
            {
                row.DimensionKey = bucket.DimensionKey;
                row.DimensionName = bucket.DimensionName;
            }
            return row;
        }

        private sealed class AccBucket
        {
            public long AccountId;
            public string Code;
            public string Name;
            public long Opening;
            public List<DaybookLineRow> Lines;
            public string DimensionKey;
            public string DimensionName;
        }

        private static bool IsCashLike(TrialBalanceRow t)
        {
            if (t == null || t.AccountTypeCode != LedgerCodes.TypeAsset) return false;
            string name = (t.AccountName ?? "") + " " + (t.AccountCode ?? "");
            return name.IndexOf("صندوق", System.StringComparison.Ordinal) >= 0
                || name.IndexOf("بانک", System.StringComparison.Ordinal) >= 0
                || name.IndexOf("نقد", System.StringComparison.Ordinal) >= 0
                || name.IndexOf("Cash", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Bank", System.StringComparison.OrdinalIgnoreCase) >= 0
                || (t.AccountCode ?? "").StartsWith("11");
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
            int requested = query != null && query.CompanyId > 0
                ? query.CompanyId
                : (identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId);
            if (identity != null && identity.IsSuperAdmin) return requested;
            if (identity != null && identity.CompanyId > 0) return identity.CompanyId;
            return requested;
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

        private static long ToLong(DataRow r, string col)
        {
            if (r == null || r.Table == null || !r.Table.Columns.Contains(col)) return 0;
            return ToLong(r[col]);
        }

        private static string Str(DataRow r, string col)
        {
            if (r == null || r.Table == null || !r.Table.Columns.Contains(col)) return "";
            object v = r[col];
            if (v == null || v == System.DBNull.Value) return "";
            return v.ToString();
        }
    }
}

using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;

namespace CaseManagement.Accounting.Ledger.Application
{
    /// <summary>
    /// Functional vs transaction currency, rate book, and revaluation journals via IGeneralLedger.
    /// </summary>
    public class CurrencyAccountingService : ICurrencyAccountingService
    {
        private readonly LedgerRepository _repo;
        private readonly IGeneralLedger _gl;

        public CurrencyAccountingService() : this(new LedgerRepository(), new PostingEngine()) { }

        public CurrencyAccountingService(LedgerRepository repo, IGeneralLedger gl)
        {
            _repo = repo;
            _gl = gl;
        }

        public string FunctionalCurrency(int companyId)
        {
            GlCompany c = _repo.GetCompany(companyId > 0 ? companyId : LedgerCodes.DefaultCompanyId);
            return c != null && !string.IsNullOrWhiteSpace(c.BaseCurrencyCode) ? c.BaseCurrencyCode : LedgerCodes.BaseCurrency;
        }

        public IList<GlCurrency> ListCurrencies(int companyId)
        {
            return _repo.ListCurrencies(companyId > 0 ? companyId : LedgerCodes.DefaultCompanyId);
        }

        public IList<GlExchangeRate> ListRates(int companyId, string currencyCode)
        {
            return _repo.ListRates(companyId > 0 ? companyId : LedgerCodes.DefaultCompanyId, currencyCode);
        }

        public LedgerResult UpsertRate(UpsertExchangeRateCommand command, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.Post))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Post);
            if (command == null || string.IsNullOrWhiteSpace(command.CurrencyCode) || command.RateToBaseMicros <= 0)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Currency and positive RateToBaseMicros are required.");

            string date = LedgerTime.DateOnly(command.RateDate);
            if (string.IsNullOrEmpty(date))
                return LedgerResult.Fail(LedgerErrorCodes.InvalidDate, "RateDate is required.");

            int companyId = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
            string ccy = command.CurrencyCode.Trim().ToUpperInvariant();
            if (ccy == FunctionalCurrency(companyId))
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Do not store a rate for the functional currency.");

            GlCurrency book = _repo.GetCurrency(companyId, ccy);
            if (book == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Unknown currency " + ccy);

            string now = LedgerTime.UtcNow(identity.UtcNow);
            long id = _repo.UpsertExchangeRate(new GlExchangeRate
            {
                CompanyId = companyId,
                CenterId = LedgerCodes.SharedCenterId,
                CurrencyCode = ccy,
                RateDate = date,
                RateToBaseMicros = command.RateToBaseMicros,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = identity.UserName,
                UpdatedBy = identity.UserName
            });
            _repo.InsertMasterAudit("UpsertFxRate", "GlExchangeRate", id, null, ccy + " " + date, identity);
            return LedgerResult.Entity(id, 1);
        }

        public LedgerResult Revalue(RevalueCommand command, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.Post))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Post);
            if (command == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Command required.");

            string asOf = LedgerTime.DateOnly(command.AsOfDate);
            if (string.IsNullOrEmpty(asOf))
                return LedgerResult.Fail(LedgerErrorCodes.InvalidDate, "AsOfDate is required.");

            int companyId = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
            int center = command.CenterId > 0 ? command.CenterId : (identity.CenterId > 0 ? identity.CenterId : 1);
            string baseCcy = FunctionalCurrency(companyId);
            long sourceId = DateKey(asOf);

            GlJournal existing = _repo.GetJournalBySource(companyId, LedgerCodes.SourceRevaluation,
                LedgerCodes.DocFxRevaluation, sourceId);
            if (existing != null && existing.Status == LedgerCodes.JournalPosted)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);
            if (existing != null && existing.Status == LedgerCodes.JournalReversed)
                _repo.ReleaseSourceKey(existing.JournalId);

            GlAccount gain = _repo.GetAccountByCode(companyId, LedgerCodes.AccountFxGain);
            GlAccount loss = _repo.GetAccountByCode(companyId, LedgerCodes.AccountFxLoss);
            if (gain == null || loss == null)
                return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "FX gain/loss accounts 4300/5300 are required.");

            System.Data.DataTable table = _repo.QueryCurrencyNets(companyId, 0, asOf, baseCcy);
            List<JournalLineDraft> lines = new List<JournalLineDraft>();
            foreach (System.Data.DataRow r in table.Rows)
            {
                string type = r["AccountTypeCode"].ToString();
                if (type != LedgerCodes.TypeAsset && type != LedgerCodes.TypeLiability)
                    continue;
                string ccy = r["CurrencyCode"].ToString();
                long txnNet = ToLong(r["TxnNet"]);
                long booked = ToLong(r["BaseNet"]);
                if (txnNet == 0) continue;
                long? rate = _repo.GetRateToBaseMicros(companyId, ccy, asOf);
                if (!rate.HasValue)
                    return LedgerResult.Fail(LedgerErrorCodes.FxRateMissing, "Exchange rate missing for " + ccy + " on " + asOf);
                long revalued = txnNet * rate.Value / LedgerCodes.RateOne;
                long delta = revalued - booked;
                if (delta == 0) continue;
                long accountId = ToLong(r["AccountID"]);
                if (delta > 0)
                {
                    lines.Add(Line(accountId, delta, 0));
                    lines.Add(Line(gain.AccountId, 0, delta));
                }
                else
                {
                    long amt = -delta;
                    lines.Add(Line(loss.AccountId, amt, 0));
                    lines.Add(Line(accountId, 0, amt));
                }
            }

            if (lines.Count < 2)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "No monetary FX exposure to revalue.");

            return _gl.Post(new PostJournalCommand
            {
                CompanyId = companyId,
                CenterId = center,
                PostingDate = asOf,
                Description = "FX revaluation " + asOf,
                JournalSource = LedgerCodes.SourceRevaluation,
                SourceModule = LedgerCodes.SourceRevaluation,
                SourceDocumentType = LedgerCodes.DocFxRevaluation,
                SourceDocumentId = sourceId,
                Lines = lines
            }, identity);
        }

        private static JournalLineDraft Line(long accountId, long debit, long credit)
        {
            JournalLineDraft d = new JournalLineDraft();
            d.AccountId = accountId;
            d.DebitMinor = debit;
            d.CreditMinor = credit;
            return d;
        }

        internal static long DateKey(string isoDate)
        {
            if (string.IsNullOrEmpty(isoDate) || isoDate.Length < 10) return 0;
            string compact = isoDate.Substring(0, 4) + isoDate.Substring(5, 2) + isoDate.Substring(8, 2);
            long n;
            long.TryParse(compact, out n);
            return n;
        }

        private static long ToLong(object v)
        {
            if (v == null || v == System.DBNull.Value) return 0;
            return System.Convert.ToInt64(v);
        }
    }
}

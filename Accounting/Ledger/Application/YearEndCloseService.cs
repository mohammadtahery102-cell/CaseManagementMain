using System;
using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;

namespace CaseManagement.Accounting.Ledger.Application
{
    /// <summary>
    /// Year-end close, opening balances, and next-year init. Posts only through IGeneralLedger.
    /// </summary>
    public class YearEndCloseService : IYearEndService
    {
        private readonly LedgerRepository _repo;
        private readonly IGeneralLedger _gl;
        private readonly IFiscalCalendarService _calendar;
        private readonly ILedgerReporting _reports;

        public YearEndCloseService()
            : this(new LedgerRepository(), new PostingEngine(), new FiscalCalendarService(), new LedgerReportingService())
        {
        }

        public YearEndCloseService(LedgerRepository repo, IGeneralLedger gl, IFiscalCalendarService calendar, ILedgerReporting reports)
        {
            _repo = repo;
            _gl = gl;
            _calendar = calendar;
            _reports = reports;
        }

        public LedgerResult Close(YearEndCloseCommand command, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.CloseYear))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.CloseYear);
            if (command == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Command required.");

            GlFiscalYear year = _repo.GetYear(command.FiscalYearId);
            if (year == null || year.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Fiscal year not found.");
            if (year.Status != LedgerCodes.StatusOpen)
                return LedgerResult.Fail(LedgerErrorCodes.YearNotOpen, "Fiscal year is not Open.");

            IList<GlFiscalPeriod> periods = _repo.ListPeriods(year.FiscalYearId, false);
            if (periods == null || periods.Count == 0)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Cannot close a year with no periods.");

            for (int i = 0; i < periods.Count - 1; i++)
            {
                if (periods[i].Status == LedgerCodes.StatusOpen)
                    return LedgerResult.Fail(LedgerErrorCodes.Validation, "Earlier periods must be Closed or Locked.");
            }

            GlFiscalPeriod last = periods[periods.Count - 1];
            if (last.Status == LedgerCodes.StatusLocked)
                return LedgerResult.Fail(LedgerErrorCodes.PeriodLocked, "Last period is locked; unlock before year-end close.");
            if (last.Status != LedgerCodes.StatusOpen)
            {
                LedgerResult reopenLast = SetPeriod(last, LedgerCodes.StatusOpen, identity);
                if (!reopenLast.Ok) return reopenLast;
                last = _repo.GetPeriod(last.FiscalPeriodId);
            }

            LedgerResult posted = EnsureClosingJournal(year, Center(command, identity), identity);
            if (!posted.Ok) return posted;

            last = _repo.GetPeriod(last.FiscalPeriodId);
            LedgerResult closeLast = SetPeriod(last, LedgerCodes.StatusClosed, identity);
            if (!closeLast.Ok) return closeLast;

            year = _repo.GetYear(year.FiscalYearId);
            LedgerResult closed = _calendar.CloseYear(new CalendarStatusCommand
            {
                EntityId = year.FiscalYearId,
                ExpectedRowVersion = year.RowVersion
            }, identity);
            if (!closed.Ok) return closed;

            _repo.InsertMasterAudit("YearEndClose", "GlFiscalYear", year.FiscalYearId,
                LedgerCodes.StatusOpen, LedgerCodes.StatusClosed, identity);
            closed.JournalId = posted.JournalId;
            closed.Journal = posted.Journal;
            return closed;
        }

        public LedgerResult Reopen(YearEndCloseCommand command, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.ReopenYear))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ReopenYear);
            if (command == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Command required.");

            GlFiscalYear year = _repo.GetYear(command.FiscalYearId);
            if (year == null || year.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Fiscal year not found.");
            if (year.Status == LedgerCodes.StatusLocked)
                return LedgerResult.Fail(LedgerErrorCodes.PeriodLocked, "Locked year cannot be reopened without UnlockYear.");

            LedgerResult reopened = _calendar.ReopenYear(new CalendarStatusCommand
            {
                EntityId = year.FiscalYearId,
                ExpectedRowVersion = command.ExpectedRowVersion > 0 ? command.ExpectedRowVersion : year.RowVersion
            }, identity);
            if (!reopened.Ok) return reopened;

            IList<GlFiscalPeriod> periods = _repo.ListPeriods(year.FiscalYearId, false);
            if (periods.Count > 0)
            {
                GlFiscalPeriod last = periods[periods.Count - 1];
                if (last.Status != LedgerCodes.StatusOpen)
                {
                    LedgerResult rp = SetPeriod(last, LedgerCodes.StatusOpen, identity);
                    if (!rp.Ok) return rp;
                }
            }

            GlJournal closeJ = _repo.GetJournalBySource(year.CompanyId, LedgerCodes.SourceClose,
                LedgerCodes.DocFiscalYear, year.FiscalYearId);
            if (closeJ != null && closeJ.Status == LedgerCodes.JournalPosted)
            {
                LedgerResult rev = _gl.Reverse(new ReverseJournalCommand
                {
                    JournalId = closeJ.JournalId,
                    ExpectedRowVersion = closeJ.RowVersion,
                    PostingDate = year.EndDate,
                    Description = "Year-end close reversal"
                }, identity);
                if (!rev.Ok) return rev;
                _repo.ReleaseSourceKey(closeJ.JournalId);
            }

            _repo.InsertMasterAudit("YearEndReopen", "GlFiscalYear", year.FiscalYearId,
                LedgerCodes.StatusClosed, LedgerCodes.StatusOpen, identity);
            return reopened;
        }

        public LedgerResult PostOpening(OpeningBalanceCommand command, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.Post))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Post);
            if (command == null || command.Lines == null || command.Lines.Count < 2)
                return LedgerResult.Fail(LedgerErrorCodes.TooFewLines, "Opening journal needs at least two lines.");

            GlFiscalYear year = _repo.GetYear(command.FiscalYearId);
            if (year == null || year.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Fiscal year not found.");
            if (year.Status != LedgerCodes.StatusOpen)
                return LedgerResult.Fail(LedgerErrorCodes.YearNotOpen, "Fiscal year is not Open.");

            for (int i = 0; i < command.Lines.Count; i++)
            {
                GlAccount acc = _repo.GetAccount(command.Lines[i].AccountId);
                if (acc == null)
                    return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Account missing on opening line.");
                if (acc.AccountTypeCode == LedgerCodes.TypeRevenue || acc.AccountTypeCode == LedgerCodes.TypeExpense)
                    return LedgerResult.Fail(LedgerErrorCodes.Validation, "Opening journals cannot post to P&L accounts.");
            }

            string date = LedgerTime.DateOnly(command.PostingDate);
            if (string.IsNullOrEmpty(date)) date = year.StartDate;
            if (string.CompareOrdinal(date, year.StartDate) < 0 || string.CompareOrdinal(date, year.EndDate) > 0)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidDate, "Opening date must lie in the fiscal year.");

            return _gl.Post(new PostJournalCommand
            {
                CompanyId = year.CompanyId,
                CenterId = Center(command.CenterId, identity),
                PostingDate = date,
                Description = string.IsNullOrWhiteSpace(command.Description) ? "Opening balances" : command.Description,
                JournalSource = LedgerCodes.SourceOpening,
                SourceModule = LedgerCodes.SourceOpening,
                SourceDocumentType = LedgerCodes.DocFiscalYear,
                SourceDocumentId = year.FiscalYearId,
                Lines = command.Lines
            }, identity);
        }

        public LedgerResult PostOpeningFromPriorYear(long priorYearId, long nextYearId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.Post))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Post);

            GlFiscalYear prior = _repo.GetYear(priorYearId);
            GlFiscalYear next = _repo.GetYear(nextYearId);
            if (prior == null || next == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Prior or next fiscal year not found.");
            if (prior.Status != LedgerCodes.StatusClosed && prior.Status != LedgerCodes.StatusLocked)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Prior year must be Closed before generating opening journals.");

            GlJournal existing = _repo.GetJournalBySource(next.CompanyId, LedgerCodes.SourceOpening,
                LedgerCodes.DocFiscalYear, next.FiscalYearId);
            if (existing != null && existing.Status == LedgerCodes.JournalPosted)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);

            LedgerReportQuery q = new LedgerReportQuery
            {
                CompanyId = prior.CompanyId,
                FromDate = prior.StartDate,
                ToDate = prior.EndDate,
                FiscalYearId = prior.FiscalYearId
            };
            IList<TrialBalanceRow> tb = _reports.GetTrialBalance(q, identity);
            List<JournalLineDraft> lines = new List<JournalLineDraft>();
            for (int i = 0; i < tb.Count; i++)
            {
                TrialBalanceRow t = tb[i];
                if (t.AccountTypeCode == LedgerCodes.TypeRevenue || t.AccountTypeCode == LedgerCodes.TypeExpense)
                    continue;
                long net = t.ClosingDebit - t.ClosingCredit;
                if (net == 0) continue;
                JournalLineDraft d = new JournalLineDraft();
                d.AccountId = t.AccountId;
                if (net > 0) { d.DebitMinor = net; d.CreditMinor = 0; }
                else { d.DebitMinor = 0; d.CreditMinor = -net; }
                lines.Add(d);
            }
            if (lines.Count < 2)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Prior year has no balance-sheet amounts to open.");

            return PostOpening(new OpeningBalanceCommand
            {
                FiscalYearId = next.FiscalYearId,
                CenterId = identity.CenterId,
                PostingDate = next.StartDate,
                Description = "Opening from " + prior.Code,
                Lines = lines
            }, identity);
        }

        public LedgerResult InitializeNextYear(InitializeNextYearCommand command, ILedgerIdentity identity)
        {
            if (command == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Command required.");
            GlFiscalYear prior = _repo.GetYear(command.PriorFiscalYearId);
            if (prior == null || prior.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Prior fiscal year not found.");

            DateTime end;
            if (!DateTime.TryParse(prior.EndDate, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out end))
                return LedgerResult.Fail(LedgerErrorCodes.InvalidDate, "Prior year EndDate is invalid.");
            DateTime start = end.Date.AddDays(1);
            DateTime nextEnd = start.AddYears(1).AddDays(-1);
            string startIso = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            string endIso = nextEnd.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            string code = string.IsNullOrWhiteSpace(command.Code) ? (start.Year.ToString()) : command.Code.Trim();

            LedgerResult year = _calendar.CreateYear(new CreateFiscalYearCommand
            {
                CompanyId = prior.CompanyId,
                Code = code,
                Name = string.IsNullOrWhiteSpace(command.Name) ? code : command.Name.Trim(),
                StartDate = startIso,
                EndDate = endIso
            }, identity);
            if (!year.Ok) return year;

            LedgerResult period = _calendar.AddPeriod(new CreateFiscalPeriodCommand
            {
                FiscalYearId = year.EntityId,
                PeriodNo = 1,
                Name = code,
                StartDate = startIso,
                EndDate = endIso
            }, identity);
            if (!period.Ok) return period;

            if (command.PostOpeningFromPrior)
            {
                LedgerResult open = PostOpeningFromPriorYear(prior.FiscalYearId, year.EntityId, identity);
                if (!open.Ok) return open;
                year.JournalId = open.JournalId;
                year.Journal = open.Journal;
            }
            return year;
        }

        private LedgerResult EnsureClosingJournal(GlFiscalYear year, int centerId, ILedgerIdentity identity)
        {
            GlJournal existing = _repo.GetJournalBySource(year.CompanyId, LedgerCodes.SourceClose,
                LedgerCodes.DocFiscalYear, year.FiscalYearId);
            if (existing != null && existing.Status == LedgerCodes.JournalPosted)
                return LedgerResult.Success(existing.JournalId, existing.RowVersion);

            GlAccount re = _repo.GetAccountByCode(year.CompanyId, LedgerCodes.AccountRetainedEarnings);
            if (re == null)
                return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Retained earnings account 3200 is missing.");

            IList<TrialBalanceRow> tb = _reports.GetTrialBalance(new LedgerReportQuery
            {
                CompanyId = year.CompanyId,
                FromDate = year.StartDate,
                ToDate = year.EndDate,
                FiscalYearId = year.FiscalYearId
            }, identity);

            List<JournalLineDraft> lines = new List<JournalLineDraft>();
            long plug = 0;
            for (int i = 0; i < tb.Count; i++)
            {
                TrialBalanceRow t = tb[i];
                if (t.AccountTypeCode != LedgerCodes.TypeRevenue && t.AccountTypeCode != LedgerCodes.TypeExpense)
                    continue;
                long net = t.ClosingDebit - t.ClosingCredit;
                if (net == 0) continue;
                JournalLineDraft d = new JournalLineDraft();
                d.AccountId = t.AccountId;
                if (net > 0)
                {
                    d.DebitMinor = 0;
                    d.CreditMinor = net;
                    plug += net;
                }
                else
                {
                    d.DebitMinor = -net;
                    d.CreditMinor = 0;
                    plug -= -net;
                }
                lines.Add(d);
            }

            if (lines.Count == 0)
                return LedgerResult.Success(0, 0);

            JournalLineDraft reLine = new JournalLineDraft();
            reLine.AccountId = re.AccountId;
            if (plug > 0)
            {
                reLine.DebitMinor = plug;
                reLine.CreditMinor = 0;
                lines.Add(reLine);
            }
            else if (plug < 0)
            {
                reLine.DebitMinor = 0;
                reLine.CreditMinor = -plug;
                lines.Add(reLine);
            }

            if (lines.Count < 2)
                return LedgerResult.Success(0, 0);

            return _gl.Post(new PostJournalCommand
            {
                CompanyId = year.CompanyId,
                CenterId = centerId,
                PostingDate = year.EndDate,
                Description = "Year-end close " + year.Code,
                JournalSource = LedgerCodes.SourceClose,
                SourceModule = LedgerCodes.SourceClose,
                SourceDocumentType = LedgerCodes.DocFiscalYear,
                SourceDocumentId = year.FiscalYearId,
                Lines = lines
            }, identity);
        }

        private LedgerResult SetPeriod(GlFiscalPeriod period, string status, ILedgerIdentity identity)
        {
            string from = period.Status ?? "";
            period.Status = status;
            period.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
            period.UpdatedBy = identity.UserName;
            if (!_repo.UpdatePeriodConcurrency(period, period.RowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "Period RowVersion mismatch.");
            string op = status == LedgerCodes.StatusOpen ? "YearEndReopenPeriod" : "YearEndClosePeriod";
            _repo.InsertMasterAudit(op, "GlFiscalPeriod", period.FiscalPeriodId, from, status, identity);
            return LedgerResult.Entity(period.FiscalPeriodId, period.RowVersion + 1);
        }

        private static int Center(YearEndCloseCommand command, ILedgerIdentity identity)
        {
            if (command.CenterId > 0) return command.CenterId;
            return Center(0, identity);
        }

        private static int Center(int centerId, ILedgerIdentity identity)
        {
            if (centerId > 0) return centerId;
            if (identity.CenterId > 0) return identity.CenterId;
            return 1;
        }
    }
}

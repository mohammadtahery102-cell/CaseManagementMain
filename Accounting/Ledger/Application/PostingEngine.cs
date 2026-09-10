using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;

namespace CaseManagement.Accounting.Ledger.Application
{
    /// <summary>
    /// UI-free posting application service. The only writer of Posted/Reversed money.
    /// Must not reference System.Windows.Forms or static SecurityContext.
    /// </summary>
    public class PostingEngine : IGeneralLedger
    {
        private readonly LedgerRepository _repo;

        public PostingEngine() : this(new LedgerRepository()) { }

        public PostingEngine(LedgerRepository repo)
        {
            _repo = repo;
        }

        public LedgerResult GetJournal(long journalId, ILedgerIdentity identity)
        {
            if (!Require(identity, LedgerPermissions.View))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.View);

            GlJournal j = _repo.GetJournal(journalId);
            if (j == null || j.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Journal not found.");
            if (!CanSeeCompany(identity, j.CompanyId) || !CanSeeCenter(identity, j.CenterId))
                return LedgerResult.Fail(LedgerErrorCodes.CenterDenied, "Center denied.");

            LedgerResult r = LedgerResult.Success(j.JournalId, j.RowVersion);
            r.Journal = j;
            return r;
        }

        public IList<GlJournal> ListJournals(string fromDate, string toDate, ILedgerIdentity identity)
        {
            if (!Require(identity, LedgerPermissions.View))
                return new List<GlJournal>();
            int companyId = identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId;
            int center = identity.IsSuperAdmin && identity.CenterId == 0 ? 0 : identity.CenterId;
            string from = LedgerTime.DateOnly(fromDate) ?? "";
            string to = LedgerTime.DateOnly(toDate) ?? "";
            return _repo.ListJournalHeaders(companyId, center, from, to);
        }

        public LedgerResult SaveDraft(SaveDraftJournalCommand command, ILedgerIdentity identity)
        {
            if (!Require(identity, LedgerPermissions.Create))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Create);
            if (command == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Command required.");

            LedgerResult gate = GuardCenter(command.CenterId, identity);
            if (!gate.Ok) return gate;

            int saveCompany = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
            if (!CanSeeCompany(identity, saveCompany))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, "Company denied.");
            command.CompanyId = saveCompany;

            LedgerResult prepared = PrepareLines(saveCompany,
                command.PostingDate, command.Lines, identity);
            if (!prepared.Ok) return prepared;

            LedgerResult saved = null;
            try
            {
                _repo.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
                {
                    saved = SaveDraftCore(con, tr, command, identity, false);
                    if (!saved.Ok) throw new LedgerAbortException(saved);
                });
            }
            catch (LedgerAbortException ex)
            {
                return ex.Result;
            }
            return saved;
        }

        public LedgerResult SubmitForApproval(JournalStatusCommand command, ILedgerIdentity identity)
        {
            return TouchDraft(command, identity, LedgerPermissions.Create, null);
        }

        public LedgerResult Approve(JournalStatusCommand command, ILedgerIdentity identity)
        {
            if (!Require(identity, LedgerPermissions.Approve))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Approve);

            return MutateHeader(command, identity, delegate (GlJournal j, string now)
            {
                if (j.Status != LedgerCodes.JournalDraft)
                    return LedgerResult.Fail(LedgerErrorCodes.NotDraft, "Only Draft journals can be approved.");
                j.Status = LedgerCodes.JournalApproved;
                j.ApprovedAt = now;
                j.ApprovedBy = identity.UserName;
                return null;
            });
        }

        public LedgerResult Reject(JournalStatusCommand command, ILedgerIdentity identity)
        {
            if (!Require(identity, LedgerPermissions.Approve))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Approve);

            return MutateHeader(command, identity, delegate (GlJournal j, string now)
            {
                if (j.Status != LedgerCodes.JournalApproved)
                    return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Only Approved journals can be rejected.");
                j.Status = LedgerCodes.JournalDraft;
                j.ApprovedAt = null;
                j.ApprovedBy = null;
                return null;
            });
        }

        public LedgerResult SoftDeleteDraft(JournalStatusCommand command, ILedgerIdentity identity)
        {
            if (!Require(identity, LedgerPermissions.Create))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Create);

            return MutateHeader(command, identity, delegate (GlJournal j, string now)
            {
                if (j.Status == LedgerCodes.JournalPosted || j.Status == LedgerCodes.JournalReversed)
                    return LedgerResult.Fail(LedgerErrorCodes.PostedImmutable, "Posted journals cannot be deleted.");
                j.IsDeleted = true;
                j.DeletedAt = now;
                j.DeletedBy = identity.UserName;
                return null;
            });
        }

        public LedgerResult Post(PostJournalCommand command, ILedgerIdentity identity)
        {
            if (!Require(identity, LedgerPermissions.Post))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Post);
            if (command == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Command required.");

            if (!string.IsNullOrWhiteSpace(command.ClientJournalGuid))
            {
                GlJournal existingGuid = _repo.GetJournalByClientGuid(command.ClientJournalGuid.Trim());
                if (existingGuid != null && existingGuid.Status == LedgerCodes.JournalPosted)
                    return OkJournal(existingGuid);
            }

            if (!string.IsNullOrWhiteSpace(command.SourceModule)
                && !string.IsNullOrWhiteSpace(command.SourceDocumentType)
                && command.SourceDocumentId.HasValue)
            {
                int companyId = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
                GlJournal existingSrc = _repo.GetJournalBySource(companyId, command.SourceModule,
                    command.SourceDocumentType, command.SourceDocumentId.Value);
                if (existingSrc != null && existingSrc.Status == LedgerCodes.JournalPosted)
                    return OkJournal(existingSrc);
            }

            LedgerResult posted = null;
            try
            {
                _repo.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
                {
                    posted = PostCore(con, tr, command, identity);
                    if (!posted.Ok) throw new LedgerAbortException(posted);
                });
            }
            catch (LedgerAbortException ex)
            {
                return ex.Result;
            }
            return posted;
        }

        public LedgerResult Reverse(ReverseJournalCommand command, ILedgerIdentity identity)
        {
            if (!Require(identity, LedgerPermissions.Reverse))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.Reverse);
            if (command == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Command required.");

            LedgerResult reversed = null;
            try
            {
                _repo.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
                {
                    reversed = ReverseCore(con, tr, command, identity);
                    if (!reversed.Ok) throw new LedgerAbortException(reversed);
                });
            }
            catch (LedgerAbortException ex)
            {
                return ex.Result;
            }
            return reversed;
        }

        private LedgerResult PostCore(SQLiteConnection con, SQLiteTransaction tr,
            PostJournalCommand command, ILedgerIdentity identity)
        {
            GlJournal journal;
            long expected;

            if (command.JournalId > 0)
            {
                journal = _repo.GetJournal(con, tr, command.JournalId);
                if (journal == null || journal.IsDeleted)
                    return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Journal not found.");
                expected = command.ExpectedRowVersion;
                if (journal.RowVersion != expected)
                    return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
                if (journal.Status == LedgerCodes.JournalPosted)
                    return OkJournal(journal);
                if (journal.Status == LedgerCodes.JournalReversed)
                    return LedgerResult.Fail(LedgerErrorCodes.AlreadyReversed, "Journal already reversed.");
            }
            else
            {
                SaveDraftJournalCommand draft = ToDraft(command);
                LedgerResult saved = SaveDraftCore(con, tr, draft, identity, true);
                if (!saved.Ok) return saved;
                journal = _repo.GetJournal(con, tr, saved.JournalId);
                expected = journal.RowVersion;
            }

            LedgerResult center = GuardCenter(journal.CenterId, identity);
            if (!center.Ok) return center;
            if (!CanSeeCompany(identity, journal.CompanyId))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, "Company denied.");

            GlLedgerSetting setting = _repo.GetSetting(journal.CompanyId);
            bool requiresApproval = setting != null && setting.RequiresJournalApproval;
            if (requiresApproval && journal.Status != LedgerCodes.JournalApproved)
                return LedgerResult.Fail(LedgerErrorCodes.ApprovalRequired, "Journal must be Approved before post.");
            if (!requiresApproval && journal.Status != LedgerCodes.JournalDraft && journal.Status != LedgerCodes.JournalApproved)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Journal cannot be posted from status " + journal.Status);

            string postingDate = LedgerTime.DateOnly(journal.PostingDate);
            LedgerResult calendar = ValidateCalendar(con, tr, journal.CompanyId, postingDate);
            if (!calendar.Ok) return calendar;
            GlFiscalPeriod period = _repo.ResolvePeriod(con, tr, journal.CompanyId, postingDate);
            GlFiscalYear year = _repo.GetYear(con, tr, period.FiscalYearId);

            List<GlJournalLine> live = LiveLines(journal.Lines);
            LedgerResult lineCheck = ValidatePreparedLines(con, tr, journal.CompanyId, postingDate, live, identity);
            if (!lineCheck.Ok) return lineCheck;

            string now = LedgerTime.UtcNow(identity.UtcNow);
            journal.FiscalYearId = year.FiscalYearId;
            journal.FiscalPeriodId = period.FiscalPeriodId;
            journal.Status = LedgerCodes.JournalPosted;
            journal.PostedAt = now;
            journal.PostedBy = identity.UserName;
            journal.UpdatedAt = now;
            journal.UpdatedBy = identity.UserName;

            if (!_repo.UpdateJournalConcurrency(con, tr, journal, expected))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");

            _repo.InsertAudit(con, tr, "Post", "GlJournal", journal.JournalId,
                LedgerCodes.JournalDraft, LedgerCodes.JournalPosted, journal.JournalNumber,
                identity.UserName, journal.CenterId);

            GlJournal reloaded = _repo.GetJournal(con, tr, journal.JournalId);
            return OkJournal(reloaded);
        }

        private LedgerResult ReverseCore(SQLiteConnection con, SQLiteTransaction tr,
            ReverseJournalCommand command, ILedgerIdentity identity)
        {
            GlJournal original = _repo.GetJournal(con, tr, command.JournalId);
            if (original == null || original.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Journal not found.");
            if (original.RowVersion != command.ExpectedRowVersion)
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            if (original.Status == LedgerCodes.JournalReversed)
                return LedgerResult.Fail(LedgerErrorCodes.AlreadyReversed, "Journal already reversed.");
            if (original.Status != LedgerCodes.JournalPosted)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Only Posted journals can be reversed.");

            LedgerResult center = GuardCenter(original.CenterId, identity);
            if (!center.Ok) return center;
            if (!CanSeeCompany(identity, original.CompanyId))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, "Company denied.");

            string postingDate = string.IsNullOrWhiteSpace(command.PostingDate)
                ? original.PostingDate : LedgerTime.DateOnly(command.PostingDate);

            LedgerResult calendar = ValidateCalendar(con, tr, original.CompanyId, postingDate);
            if (!calendar.Ok) return calendar;
            GlFiscalPeriod period = _repo.ResolvePeriod(con, tr, original.CompanyId, postingDate);
            GlFiscalYear year = _repo.GetYear(con, tr, period.FiscalYearId);

            string now = LedgerTime.UtcNow(identity.UtcNow);
            string number;
            _repo.AllocateJournalNumber(con, tr, original.CompanyId, year.Code, now, identity.UserName, out number);

            GlJournal reversal = new GlJournal
            {
                CompanyId = original.CompanyId,
                CenterId = original.CenterId,
                FiscalYearId = year.FiscalYearId,
                FiscalPeriodId = period.FiscalPeriodId,
                JournalNumber = number,
                JournalSource = LedgerCodes.SourceReversal,
                SourceModule = LedgerCodes.SourceReversal,
                SourceDocumentType = "GlJournal",
                SourceDocumentId = original.JournalId,
                PostingDate = postingDate,
                DocumentDate = original.DocumentDate,
                ReferenceNumber = original.ReferenceNumber,
                Description = string.IsNullOrWhiteSpace(command.Description)
                    ? ("Reversal of " + original.JournalNumber) : command.Description,
                Status = LedgerCodes.JournalPosted,
                ReversesJournalId = original.JournalId,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = identity.UserName,
                UpdatedBy = identity.UserName,
                PostedAt = now,
                PostedBy = identity.UserName,
                Lines = new List<GlJournalLine>()
            };

            long reversalId = _repo.InsertJournal(con, tr, reversal);
            IList<GlJournalLine> originals = LiveLines(original.Lines);
            for (int i = 0; i < originals.Count; i++)
            {
                GlJournalLine src = originals[i];
                GlJournalLine line = CloneLine(src, reversalId, original.CompanyId, original.CenterId, now, identity.UserName);
                line.LineNo = i + 1;
                long dr = src.DebitMinor;
                long cr = src.CreditMinor;
                line.DebitMinor = cr;
                line.CreditMinor = dr;
                line.DebitBaseMinor = src.CreditBaseMinor;
                line.CreditBaseMinor = src.DebitBaseMinor;
                _repo.InsertLine(con, tr, line);
            }

            original.Status = LedgerCodes.JournalReversed;
            original.UpdatedAt = now;
            original.UpdatedBy = identity.UserName;
            if (!_repo.UpdateJournalConcurrency(con, tr, original, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");

            _repo.InsertAudit(con, tr, "Reverse", "GlJournal", original.JournalId,
                LedgerCodes.JournalPosted, LedgerCodes.JournalReversed, reversal.JournalNumber,
                identity.UserName, original.CenterId);

            GlJournal loaded = _repo.GetJournal(con, tr, reversalId);
            return OkJournal(loaded);
        }

        private LedgerResult SaveDraftCore(SQLiteConnection con, SQLiteTransaction tr,
            SaveDraftJournalCommand command, ILedgerIdentity identity, bool insidePost)
        {
            int companyId = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
            int centerId = command.CenterId > 0 ? command.CenterId : identity.CenterId;
            LedgerResult gate = GuardCenter(centerId, identity);
            if (!gate.Ok) return gate;

            string postingDate = LedgerTime.DateOnly(command.PostingDate);
            if (string.IsNullOrEmpty(postingDate))
                return LedgerResult.Fail(LedgerErrorCodes.InvalidDate, "PostingDate is required.");

            GlFiscalPeriod period = _repo.ResolvePeriod(con, tr, companyId, postingDate);
            if (period == null)
                return LedgerResult.Fail(LedgerErrorCodes.PeriodNotOpen, "No fiscal period contains this date.");
            GlFiscalYear year = _repo.GetYear(con, tr, period.FiscalYearId);
            if (year == null)
                return LedgerResult.Fail(LedgerErrorCodes.YearNotOpen, "Fiscal year not found.");

            string now = LedgerTime.UtcNow(identity.UtcNow);
            List<GlJournalLine> incoming = new List<GlJournalLine>();
            if (command.Lines != null)
            {
                for (int i = 0; i < command.Lines.Count; i++)
                {
                    LedgerResult built = BuildLine(con, tr, companyId, centerId, postingDate, command.Lines[i], i + 1, identity, now);
                    if (!built.Ok) return built;
                    incoming.Add(built.Journal.Lines[0]);
                }
            }

            if (command.JournalId > 0)
            {
                GlJournal existing = _repo.GetJournal(con, tr, command.JournalId);
                if (existing == null || existing.IsDeleted)
                    return LedgerResult.Fail(LedgerErrorCodes.JournalMissing, "Journal not found.");
                if (existing.Status == LedgerCodes.JournalPosted || existing.Status == LedgerCodes.JournalReversed)
                    return LedgerResult.Fail(LedgerErrorCodes.PostedImmutable, "Posted journals are immutable.");
                if (existing.RowVersion != command.ExpectedRowVersion)
                    return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");

                existing.CenterId = centerId;
                existing.FiscalYearId = year.FiscalYearId;
                existing.FiscalPeriodId = period.FiscalPeriodId;
                existing.PostingDate = postingDate;
                existing.DocumentDate = LedgerTime.DateOnly(command.DocumentDate);
                existing.ReferenceNumber = command.ReferenceNumber;
                existing.Description = command.Description;
                existing.JournalSource = string.IsNullOrWhiteSpace(command.JournalSource)
                    ? existing.JournalSource : command.JournalSource;
                ApplySource(existing, command);
                existing.UpdatedAt = now;
                existing.UpdatedBy = identity.UserName;

                if (!_repo.UpdateJournalConcurrency(con, tr, existing, command.ExpectedRowVersion))
                    return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");

                IList<GlJournalLine> oldLines = existing.Lines ?? new List<GlJournalLine>();
                int max = incoming.Count > oldLines.Count ? incoming.Count : oldLines.Count;
                for (int i = 0; i < max; i++)
                {
                    if (i < incoming.Count && i < oldLines.Count)
                    {
                        GlJournalLine upd = incoming[i];
                        upd.JournalLineId = oldLines[i].JournalLineId;
                        upd.JournalId = existing.JournalId;
                        upd.LineNo = i + 1;
                        if (!_repo.UpdateLineConcurrency(con, tr, upd, oldLines[i].RowVersion))
                            return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "Line RowVersion mismatch.");
                    }
                    else if (i < incoming.Count)
                    {
                        GlJournalLine ins = incoming[i];
                        ins.JournalId = existing.JournalId;
                        ins.LineNo = i + 1;
                        _repo.InsertLine(con, tr, ins);
                    }
                    else
                    {
                        // CONFLICT-1: no DELETE. Surplus draft lines are zeroed and ignored at post.
                        GlJournalLine tomb = oldLines[i];
                        tomb.DebitMinor = 0;
                        tomb.CreditMinor = 0;
                        tomb.DebitBaseMinor = 0;
                        tomb.CreditBaseMinor = 0;
                        tomb.UpdatedAt = now;
                        tomb.UpdatedBy = identity.UserName;
                        if (!_repo.UpdateLineConcurrency(con, tr, tomb, tomb.RowVersion))
                            return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "Line RowVersion mismatch.");
                    }
                }

                GlJournal reloaded = _repo.GetJournal(con, tr, existing.JournalId);
                return OkJournal(reloaded);
            }

            string number;
            _repo.AllocateJournalNumber(con, tr, companyId, year.Code, now, identity.UserName, out number);
            if (string.IsNullOrEmpty(number))
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Ledger settings missing.");

            GlJournal journal = new GlJournal
            {
                CompanyId = companyId,
                CenterId = centerId,
                FiscalYearId = year.FiscalYearId,
                FiscalPeriodId = period.FiscalPeriodId,
                JournalNumber = number,
                JournalSource = string.IsNullOrWhiteSpace(command.JournalSource)
                    ? LedgerCodes.SourceManual : command.JournalSource,
                PostingDate = postingDate,
                DocumentDate = LedgerTime.DateOnly(command.DocumentDate),
                ReferenceNumber = command.ReferenceNumber,
                Description = command.Description,
                Status = LedgerCodes.JournalDraft,
                ClientJournalGuid = string.IsNullOrWhiteSpace(command.ClientJournalGuid)
                    ? null : command.ClientJournalGuid.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = identity.UserName,
                UpdatedBy = identity.UserName
            };
            ApplySource(journal, command);

            long id = _repo.InsertJournal(con, tr, journal);
            for (int i = 0; i < incoming.Count; i++)
            {
                incoming[i].JournalId = id;
                incoming[i].LineNo = i + 1;
                _repo.InsertLine(con, tr, incoming[i]);
            }

            GlJournal created = _repo.GetJournal(con, tr, id);
            return OkJournal(created);
        }

        private static void ApplySource(GlJournal journal, SaveDraftJournalCommand command)
        {
            journal.SourceModule = EmptyToNull(command.SourceModule);
            journal.SourceDocumentType = EmptyToNull(command.SourceDocumentType);
            journal.SourceDocumentId = command.SourceDocumentId;
        }

        private static void ApplySource(GlJournal journal, PostJournalCommand command)
        {
            journal.SourceModule = EmptyToNull(command.SourceModule);
            journal.SourceDocumentType = EmptyToNull(command.SourceDocumentType);
            journal.SourceDocumentId = command.SourceDocumentId;
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private LedgerResult BuildLine(SQLiteConnection con, SQLiteTransaction tr,
            int companyId, int centerId, string postingDate, JournalLineDraft draft, int lineNo,
            ILedgerIdentity identity, string now)
        {
            if (draft == null)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidLine, "Line is required.");

            GlCompany company = _repo.GetCompany(con, tr, companyId);
            string baseCcy = company != null ? company.BaseCurrencyCode : LedgerCodes.BaseCurrency;
            string ccy = string.IsNullOrWhiteSpace(draft.CurrencyCode) ? baseCcy : draft.CurrencyCode.Trim().ToUpperInvariant();

            bool debitPos = draft.DebitMinor > 0;
            bool creditPos = draft.CreditMinor > 0;
            if (debitPos == creditPos)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidLine, "Each line must have debit XOR credit.");

            long rate = draft.ExchangeRateMicros;
            if (string.Equals(ccy, baseCcy, StringComparison.OrdinalIgnoreCase))
            {
                rate = LedgerCodes.RateOne;
            }
            else if (rate <= 0)
            {
                long? found = _repo.GetRateToBaseMicros(con, tr, companyId, ccy, postingDate);
                if (!found.HasValue)
                    return LedgerResult.Fail(LedgerErrorCodes.FxRateMissing, "Exchange rate missing for " + ccy);
                rate = found.Value;
            }

            long txn = debitPos ? draft.DebitMinor : draft.CreditMinor;
            long baseAmt = txn * rate / LedgerCodes.RateOne;

            GlJournalLine line = new GlJournalLine
            {
                CompanyId = companyId,
                CenterId = centerId,
                LineNo = lineNo,
                AccountId = draft.AccountId,
                DebitMinor = debitPos ? draft.DebitMinor : 0,
                CreditMinor = creditPos ? draft.CreditMinor : 0,
                CurrencyCode = ccy,
                ExchangeRateMicros = rate,
                DebitBaseMinor = debitPos ? baseAmt : 0,
                CreditBaseMinor = creditPos ? baseAmt : 0,
                Description = draft.Description,
                CostCenterId = draft.CostCenterId,
                ProjectId = draft.ProjectId,
                PartyId = draft.PartyId,
                FundId = draft.FundId,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = identity.UserName,
                UpdatedBy = identity.UserName
            };

            LedgerResult wrap = new LedgerResult { Ok = true };
            wrap.Journal = new GlJournal { Lines = new List<GlJournalLine> { line } };
            return wrap;
        }

        private LedgerResult PrepareLines(int companyId, string postingDate, List<JournalLineDraft> lines, ILedgerIdentity identity)
        {
            return LedgerResult.Success(0, 0);
        }

        private LedgerResult ValidatePreparedLines(SQLiteConnection con, SQLiteTransaction tr,
            int companyId, string postingDate, List<GlJournalLine> lines, ILedgerIdentity identity)
        {
            if (lines == null || lines.Count < 2)
                return LedgerResult.Fail(LedgerErrorCodes.TooFewLines, "A journal needs at least two lines.");

            long debit = 0;
            long credit = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                GlJournalLine line = lines[i];
                if (!((line.DebitMinor > 0 && line.CreditMinor == 0) || (line.CreditMinor > 0 && line.DebitMinor == 0)))
                    return LedgerResult.Fail(LedgerErrorCodes.InvalidLine, "Invalid debit/credit on line " + line.LineNo);

                GlAccount account = _repo.GetAccount(con, tr, line.AccountId);
                if (account == null)
                    return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Account missing on line " + line.LineNo);
                if (account.IsDeleted)
                    return LedgerResult.Fail(LedgerErrorCodes.AccountDeleted, "Account is deleted.");
                if (!account.IsActive)
                    return LedgerResult.Fail(LedgerErrorCodes.AccountInactive, "Account is inactive.");
                if (!account.IsLeaf || !account.AllowPosting)
                    return LedgerResult.Fail(LedgerErrorCodes.AccountNotLeaf, "Posting is only allowed on leaf accounts.");
                if (account.CompanyId != companyId)
                    return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Account belongs to another company.");

                if (line.CostCenterId.HasValue)
                {
                    GlCostCenter cc = _repo.GetCostCenter(con, tr, line.CostCenterId.Value);
                    if (cc == null || cc.IsDeleted || cc.CompanyId != companyId)
                        return LedgerResult.Fail(LedgerErrorCodes.DimensionMissing, "Cost center not found.");
                    if (!cc.IsActive)
                        return LedgerResult.Fail(LedgerErrorCodes.DimensionInactive, "Cost center is inactive.");
                    if (!cc.IsLeaf)
                        return LedgerResult.Fail(LedgerErrorCodes.DimensionNotLeaf, "Posting requires a leaf cost center.");
                }
                if (line.ProjectId.HasValue)
                {
                    GlProject pr = _repo.GetProject(con, tr, line.ProjectId.Value);
                    if (pr == null || pr.IsDeleted || pr.CompanyId != companyId)
                        return LedgerResult.Fail(LedgerErrorCodes.DimensionMissing, "Project not found.");
                    if (!pr.IsActive)
                        return LedgerResult.Fail(LedgerErrorCodes.DimensionInactive, "Project is inactive.");
                    if (!pr.IsLeaf)
                        return LedgerResult.Fail(LedgerErrorCodes.DimensionNotLeaf, "Posting requires a leaf project.");
                }

                debit += line.DebitBaseMinor;
                credit += line.CreditBaseMinor;
            }

            if (debit != credit || debit <= 0)
                return LedgerResult.Fail(LedgerErrorCodes.Unbalanced, "Debit base must equal credit base and be greater than zero.");

            return LedgerResult.Success(0, 0);
        }

        private LedgerResult ValidateCalendar(SQLiteConnection con, SQLiteTransaction tr, int companyId, string postingDate)
        {
            GlFiscalPeriod period = _repo.ResolvePeriod(con, tr, companyId, postingDate);
            if (period == null)
                return LedgerResult.Fail(LedgerErrorCodes.PeriodNotOpen, "No fiscal period contains this date.");
            if (period.Status == LedgerCodes.StatusLocked)
                return LedgerResult.Fail(LedgerErrorCodes.PeriodLocked, "Period is locked.");
            if (period.Status != LedgerCodes.StatusOpen)
                return LedgerResult.Fail(LedgerErrorCodes.PeriodNotOpen, "Period is not Open.");

            GlFiscalYear year = _repo.GetYear(con, tr, period.FiscalYearId);
            if (year == null || year.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.YearNotOpen, "Fiscal year not found.");
            if (year.Status != LedgerCodes.StatusOpen)
                return LedgerResult.Fail(LedgerErrorCodes.YearNotOpen, "Fiscal year is not Open.");

            return LedgerResult.Success(period.FiscalPeriodId, period.RowVersion);
        }

        private static List<GlJournalLine> LiveLines(IList<GlJournalLine> lines)
        {
            List<GlJournalLine> live = new List<GlJournalLine>();
            if (lines == null) return live;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].DebitMinor > 0 || lines[i].CreditMinor > 0)
                    live.Add(lines[i]);
            }
            return live;
        }

        private static GlJournalLine CloneLine(GlJournalLine src, long journalId, int companyId, int centerId, string now, string user)
        {
            return new GlJournalLine
            {
                JournalId = journalId,
                CompanyId = companyId,
                CenterId = centerId,
                AccountId = src.AccountId,
                CurrencyCode = src.CurrencyCode,
                ExchangeRateMicros = src.ExchangeRateMicros,
                Description = src.Description,
                CostCenterId = src.CostCenterId,
                ProjectId = src.ProjectId,
                PartyId = src.PartyId,
                FundId = src.FundId,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = user,
                UpdatedBy = user
            };
        }

        private LedgerResult MutateHeader(JournalStatusCommand command, ILedgerIdentity identity,
            Func<GlJournal, string, LedgerResult> mutate)
        {
            if (command == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Command required.");

            LedgerResult result = null;
            try
            {
                _repo.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
                {
                    GlJournal j = _repo.GetJournal(con, tr, command.JournalId);
                    if (j == null || j.IsDeleted)
                    {
                        result = LedgerResult.Fail(j != null && j.IsDeleted
                            ? LedgerErrorCodes.AlreadyDeleted : LedgerErrorCodes.JournalMissing, "Journal not found.");
                        throw new LedgerAbortException(result);
                    }
                    if (j.RowVersion != command.ExpectedRowVersion)
                    {
                        result = LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
                        throw new LedgerAbortException(result);
                    }
                    LedgerResult c = GuardCenter(j.CenterId, identity);
                    if (!c.Ok) { result = c; throw new LedgerAbortException(c); }
                    if (!CanSeeCompany(identity, j.CompanyId))
                    {
                        result = LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, "Company denied.");
                        throw new LedgerAbortException(result);
                    }

                    string now = LedgerTime.UtcNow(identity.UtcNow);
                    LedgerResult fail = mutate(j, now);
                    if (fail != null && !fail.Ok) { result = fail; throw new LedgerAbortException(fail); }

                    j.UpdatedAt = now;
                    j.UpdatedBy = identity.UserName;
                    if (!_repo.UpdateJournalConcurrency(con, tr, j, command.ExpectedRowVersion))
                    {
                        result = LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
                        throw new LedgerAbortException(result);
                    }
                    GlJournal reloaded = _repo.GetJournal(con, tr, j.JournalId);
                    result = OkJournal(reloaded);
                });
            }
            catch (LedgerAbortException ex)
            {
                return ex.Result;
            }
            return result;
        }

        private LedgerResult TouchDraft(JournalStatusCommand command, ILedgerIdentity identity, string perm, object unused)
        {
            if (!Require(identity, perm))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, perm);
            return MutateHeader(command, identity, delegate (GlJournal j, string now)
            {
                if (j.Status != LedgerCodes.JournalDraft)
                    return LedgerResult.Fail(LedgerErrorCodes.NotDraft, "Only Draft journals can be submitted.");
                return null;
            });
        }

        private static SaveDraftJournalCommand ToDraft(PostJournalCommand command)
        {
            return new SaveDraftJournalCommand
            {
                JournalId = command.JournalId,
                ExpectedRowVersion = command.ExpectedRowVersion,
                CompanyId = command.CompanyId,
                CenterId = command.CenterId,
                PostingDate = command.PostingDate,
                DocumentDate = command.DocumentDate,
                ReferenceNumber = command.ReferenceNumber,
                Description = command.Description,
                JournalSource = command.JournalSource,
                SourceModule = command.SourceModule,
                SourceDocumentType = command.SourceDocumentType,
                SourceDocumentId = command.SourceDocumentId,
                ClientJournalGuid = command.ClientJournalGuid,
                Lines = command.Lines
            };
        }

        private static LedgerResult OkJournal(GlJournal j)
        {
            LedgerResult r = LedgerResult.Success(j.JournalId, j.RowVersion);
            r.Journal = j;
            return r;
        }

        private static bool Require(ILedgerIdentity identity, string permission)
        {
            if (identity == null) return false;
            return identity.HasPermission(permission);
        }

        private static bool CanSeeCompany(ILedgerIdentity identity, int companyId)
        {
            if (identity == null) return false;
            if (identity.IsSuperAdmin) return true;
            if (identity.CompanyId <= 0 || companyId <= 0) return false;
            return identity.CompanyId == companyId;
        }

        private static bool CanSeeCenter(ILedgerIdentity identity, int centerId)
        {
            if (identity == null) return false;
            if (identity.IsSuperAdmin && identity.CenterId == 0) return true;
            if (identity.CenterId <= 0) return false;
            return identity.CenterId == centerId;
        }

        private static LedgerResult GuardCenter(int centerId, ILedgerIdentity identity)
        {
            if (centerId <= 0)
                return LedgerResult.Fail(LedgerErrorCodes.CenterRequired, "Journal CenterID must be a real branch.");
            if (!CanSeeCenter(identity, centerId))
                return LedgerResult.Fail(LedgerErrorCodes.CenterDenied, "Center denied.");
            return LedgerResult.Success(0, 0);
        }

        private sealed class LedgerAbortException : Exception
        {
            public LedgerResult Result { get; private set; }
            public LedgerAbortException(LedgerResult result) { Result = result; }
        }
    }
}

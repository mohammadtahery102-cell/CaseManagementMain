using System;
using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;

namespace CaseManagement.Accounting.Ledger.Application
{
    public class FiscalCalendarService : IFiscalCalendarService
    {
        private readonly LedgerRepository _repo;

        public FiscalCalendarService() : this(new LedgerRepository()) { }

        public FiscalCalendarService(LedgerRepository repo)
        {
            _repo = repo;
        }

        public GlFiscalPeriod Resolve(int companyId, string postingDate)
        {
            return _repo.ResolvePeriod(companyId, LedgerTime.DateOnly(postingDate));
        }

        public LedgerResult CreateYear(CreateFiscalYearCommand command, ILedgerIdentity identity)
        {
            if (!identity.HasPermission(LedgerPermissions.CloseYear) && !identity.HasPermission(LedgerPermissions.ManageCoA)
                && !identity.HasPermission(LedgerPermissions.Create))
            {
                if (!identity.IsSuperAdmin)
                    return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, "Fiscal year create denied.");
            }

            string start = LedgerTime.DateOnly(command.StartDate);
            string end = LedgerTime.DateOnly(command.EndDate);
            if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end) || string.CompareOrdinal(start, end) >= 0)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidDate, "StartDate must be before EndDate.");

            int companyId = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
            if (_repo.YearOverlaps(companyId, start, end, 0))
                return LedgerResult.Fail(LedgerErrorCodes.Overlap, "Fiscal year overlaps an existing year.");

            string now = LedgerTime.UtcNow(identity.UtcNow);
            GlFiscalYear y = new GlFiscalYear
            {
                CompanyId = companyId,
                CenterId = LedgerCodes.SharedCenterId,
                Code = (command.Code ?? "").Trim(),
                Name = (command.Name ?? command.Code ?? "").Trim(),
                CalendarType = string.IsNullOrWhiteSpace(command.CalendarType)
                    ? LedgerCodes.CalendarSolarHijri : command.CalendarType,
                StartDate = start,
                EndDate = end,
                Status = LedgerCodes.StatusOpen,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = identity.UserName,
                UpdatedBy = identity.UserName
            };
            if (string.IsNullOrEmpty(y.Code))
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Year code is required.");

            long id = _repo.InsertYear(y);
            _repo.InsertMasterAudit("CreateYear", "GlFiscalYear", id, null, y.Code, identity);
            return LedgerResult.Entity(id, 1);
        }

        public LedgerResult AddPeriod(CreateFiscalPeriodCommand command, ILedgerIdentity identity)
        {
            GlFiscalYear y = _repo.GetYear(command.FiscalYearId);
            if (y == null || y.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Fiscal year not found.");

            string start = LedgerTime.DateOnly(command.StartDate);
            string end = LedgerTime.DateOnly(command.EndDate);
            if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end) || string.CompareOrdinal(start, end) >= 0)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidDate, "Period StartDate must be before EndDate.");
            if (string.CompareOrdinal(start, y.StartDate) < 0 || string.CompareOrdinal(end, y.EndDate) > 0)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidDate, "Period must lie inside the fiscal year.");
            if (_repo.PeriodOverlaps(y.FiscalYearId, start, end, 0))
                return LedgerResult.Fail(LedgerErrorCodes.Overlap, "Period overlaps another period.");

            string now = LedgerTime.UtcNow(identity.UtcNow);
            GlFiscalPeriod p = new GlFiscalPeriod
            {
                CompanyId = y.CompanyId,
                CenterId = LedgerCodes.SharedCenterId,
                FiscalYearId = y.FiscalYearId,
                PeriodNo = command.PeriodNo,
                Name = (command.Name ?? ("P" + command.PeriodNo)).Trim(),
                StartDate = start,
                EndDate = end,
                Status = LedgerCodes.StatusOpen,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = identity.UserName,
                UpdatedBy = identity.UserName
            };
            long id = _repo.InsertPeriod(p);
            _repo.InsertMasterAudit("AddPeriod", "GlFiscalPeriod", id, null, p.Name, identity);
            return LedgerResult.Entity(id, 1);
        }

        public LedgerResult ClosePeriod(CalendarStatusCommand command, ILedgerIdentity identity)
        {
            if (!identity.HasPermission(LedgerPermissions.ClosePeriod))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ClosePeriod);
            return SetPeriodStatus(command, identity, LedgerCodes.StatusClosed);
        }

        public LedgerResult LockPeriod(CalendarStatusCommand command, ILedgerIdentity identity)
        {
            if (!identity.HasPermission(LedgerPermissions.ClosePeriod))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ClosePeriod);
            return SetPeriodStatus(command, identity, LedgerCodes.StatusLocked);
        }

        public LedgerResult CloseYear(CalendarStatusCommand command, ILedgerIdentity identity)
        {
            if (!identity.HasPermission(LedgerPermissions.CloseYear))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.CloseYear);

            GlFiscalYear y = _repo.GetYear(command.EntityId);
            if (y == null || y.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Fiscal year not found.");

            IList<GlFiscalPeriod> periods = _repo.ListPeriods(y.FiscalYearId, false);
            if (periods.Count == 0)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Cannot close a year with no periods.");
            for (int i = 0; i < periods.Count; i++)
            {
                if (periods[i].Status == LedgerCodes.StatusOpen)
                    return LedgerResult.Fail(LedgerErrorCodes.Validation, "All periods must be Closed or Locked.");
            }

            y.Status = LedgerCodes.StatusClosed;
            y.ClosedAt = LedgerTime.UtcNow(identity.UtcNow);
            y.ClosedBy = identity.UserName;
            y.UpdatedAt = y.ClosedAt;
            y.UpdatedBy = identity.UserName;
            if (!_repo.UpdateYearConcurrency(y, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            return LedgerResult.Entity(y.FiscalYearId, y.RowVersion + 1);
        }

        public LedgerResult LockYear(CalendarStatusCommand command, ILedgerIdentity identity)
        {
            if (!identity.HasPermission(LedgerPermissions.UnlockYear) && !identity.HasPermission(LedgerPermissions.CloseYear))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.CloseYear);

            GlFiscalYear y = _repo.GetYear(command.EntityId);
            if (y == null || y.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Fiscal year not found.");
            if (y.Status != LedgerCodes.StatusClosed)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Year must be Closed before lock.");

            y.Status = LedgerCodes.StatusLocked;
            y.LockedAt = LedgerTime.UtcNow(identity.UtcNow);
            y.LockedBy = identity.UserName;
            y.UpdatedAt = y.LockedAt;
            y.UpdatedBy = identity.UserName;
            if (!_repo.UpdateYearConcurrency(y, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            return LedgerResult.Entity(y.FiscalYearId, y.RowVersion + 1);
        }

        public LedgerResult ReopenYear(CalendarStatusCommand command, ILedgerIdentity identity)
        {
            if (!identity.HasPermission(LedgerPermissions.ReopenYear))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ReopenYear);

            GlFiscalYear y = _repo.GetYear(command.EntityId);
            if (y == null || y.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Fiscal year not found.");
            if (y.Status == LedgerCodes.StatusLocked)
                return LedgerResult.Fail(LedgerErrorCodes.PeriodLocked, "Locked year cannot be reopened without UnlockYear.");
            if (y.Status != LedgerCodes.StatusClosed)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Only a Closed year can be reopened.");

            y.Status = LedgerCodes.StatusOpen;
            y.ClosedAt = null;
            y.ClosedBy = null;
            y.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
            y.UpdatedBy = identity.UserName;
            if (!_repo.UpdateYearConcurrency(y, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            return LedgerResult.Entity(y.FiscalYearId, y.RowVersion + 1);
        }

        public LedgerResult UnlockYear(CalendarStatusCommand command, ILedgerIdentity identity)
        {
            if (!identity.HasPermission(LedgerPermissions.UnlockYear))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.UnlockYear);

            GlFiscalYear y = _repo.GetYear(command.EntityId);
            if (y == null || y.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Fiscal year not found.");
            if (y.Status != LedgerCodes.StatusLocked)
                return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Only a Locked year can be unlocked.");

            y.Status = LedgerCodes.StatusClosed;
            y.LockedAt = null;
            y.LockedBy = null;
            y.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
            y.UpdatedBy = identity.UserName;
            if (!_repo.UpdateYearConcurrency(y, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            _repo.InsertMasterAudit("UnlockYear", "GlFiscalYear", y.FiscalYearId, LedgerCodes.StatusLocked, LedgerCodes.StatusClosed, identity);
            return LedgerResult.Entity(y.FiscalYearId, y.RowVersion + 1);
        }

        public LedgerResult ReopenPeriod(CalendarStatusCommand command, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.ClosePeriod))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ClosePeriod);
            return SetPeriodStatus(command, identity, LedgerCodes.StatusOpen);
        }

        public IList<GlFiscalYear> ListYears(int companyId)
        {
            return _repo.ListYears(companyId);
        }

        public IList<GlFiscalPeriod> ListPeriods(long fiscalYearId)
        {
            return _repo.ListPeriods(fiscalYearId, false);
        }

        private LedgerResult SetPeriodStatus(CalendarStatusCommand command, ILedgerIdentity identity, string status)
        {
            GlFiscalPeriod p = _repo.GetPeriod(command.EntityId);
            if (p == null || p.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Period not found.");

            p.Status = status;
            p.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
            p.UpdatedBy = identity.UserName;
            if (!_repo.UpdatePeriodConcurrency(p, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            return LedgerResult.Entity(p.FiscalPeriodId, p.RowVersion + 1);
        }
    }
}

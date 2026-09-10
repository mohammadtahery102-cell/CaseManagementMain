using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Payroll.Domain;
using CaseManagement.Payroll.Infrastructure;
using CaseManagement.Trade;

namespace CaseManagement.Payroll.Application
{
    public class EmployeeService
    {
        private readonly PayrollStore _store;
        public EmployeeService() : this(new PayrollStore()) { }
        public EmployeeService(PayrollStore store) { _store = store; }

        public TradeResult CreateDepartment(string code, string name, ILedgerIdentity identity)
        {
            if (!CanCreate(identity)) return TradeResult.Fail("PERMISSION", PayrollPermissions.Create);
            int c = Co(identity);
            long id = _store.InsertMaster("PrDepartment", "DepartmentID", c, Ctr(identity), code, name, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            return id > 0 ? TradeResult.Success(id, 1) : TradeResult.Fail("VALIDATION", "Department code required.");
        }

        public TradeResult CreatePosition(string code, string name, ILedgerIdentity identity)
        {
            if (!CanCreate(identity)) return TradeResult.Fail("PERMISSION", PayrollPermissions.Create);
            long id = _store.InsertMaster("PrPosition", "PositionID", Co(identity), Ctr(identity), code, name, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            return id > 0 ? TradeResult.Success(id, 1) : TradeResult.Fail("VALIDATION", "Position code required.");
        }

        public TradeResult CreateEmployee(PrEmployee e, ILedgerIdentity identity)
        {
            if (!CanCreate(identity)) return TradeResult.Fail("PERMISSION", PayrollPermissions.Create);
            if (e == null || string.IsNullOrWhiteSpace(e.Name) || e.GrossMinor <= 0)
                return TradeResult.Fail("VALIDATION", "Employee name and gross pay are required.");
            if (!identity.IsSuperAdmin && identity.CenterId > 0 && e.CenterId > 0 && identity.CenterId != e.CenterId)
                return TradeResult.Fail("PERMISSION", "Cross-branch payroll is not allowed.");
            e.CompanyId = e.CompanyId > 0 ? e.CompanyId : Co(identity);
            e.CenterId = e.CenterId > 0 ? e.CenterId : Ctr(identity);
            if (e.CenterId <= 0) e.CenterId = 1;
            if (e.DepartmentId <= 0) e.DepartmentId = _store.DefaultDept(e.CompanyId);
            if (e.PositionId <= 0) e.PositionId = _store.DefaultPosition(e.CompanyId);
            if (string.IsNullOrWhiteSpace(e.Code)) e.Code = "E" + Guid.NewGuid().ToString("N").Substring(0, 8);
            long id = _store.InsertEmployee(e, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            return id > 0 ? TradeResult.Success(id, 1) : TradeResult.Fail("VALIDATION", "Could not create employee.");
        }

        public IList<EmployeeListRow> List(ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PayrollPermissions.View)) return new List<EmployeeListRow>();
            return _store.Employees(Co(identity), identity.IsSuperAdmin && identity.CenterId == 0 ? 0 : identity.CenterId);
        }

        private static bool CanCreate(ILedgerIdentity identity)
        {
            return identity != null && identity.HasPermission(PayrollPermissions.Create);
        }

        private static int Co(ILedgerIdentity identity)
        {
            return identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId;
        }

        private static int Ctr(ILedgerIdentity identity)
        {
            return identity.CenterId > 0 ? identity.CenterId : 0;
        }
    }

    public class PayrollService
    {
        private readonly PayrollStore _store;
        public PayrollService() : this(new PayrollStore()) { }
        public PayrollService(PayrollStore store) { _store = store; }

        public TradeResult CreatePeriod(string code, string start, string end, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PayrollPermissions.Create))
                return TradeResult.Fail("PERMISSION", PayrollPermissions.Create);
            if (string.IsNullOrWhiteSpace(code)) return TradeResult.Fail("VALIDATION", "Period code is required.");
            long id = _store.InsertPeriod(Co(identity), identity.CenterId, code, start, end, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            return id > 0 ? TradeResult.Success(id, 1) : TradeResult.Fail("VALIDATION", "Could not create period.");
        }

        public TradeResult CreateRun(long periodId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PayrollPermissions.Create))
                return TradeResult.Fail("PERMISSION", PayrollPermissions.Create);
            int companyId = Co(identity);
            int center = identity.CenterId > 0 ? identity.CenterId : 1;
            IList<PrEmployee> all = _store.ActiveEmployees(companyId);
            List<PrEmployee> emps = new List<PrEmployee>();
            for (int i = 0; i < all.Count; i++)
            {
                if (TradeIsolation.CanSeeCenter(identity, all[i].CenterId))
                    emps.Add(all[i]);
            }
            if (emps.Count == 0) return TradeResult.Fail("VALIDATION", "No active employees.");
            long total = 0;
            for (int i = 0; i < emps.Count; i++) total += emps[i].GrossMinor;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                string no = _store.AllocateNo(con, tr, companyId);
                newId = _store.InsertRun(con, tr, companyId, center, periodId, no, identity.UtcNow.ToString("yyyy-MM-dd"), total, now, identity.UserName);
                for (int i = 0; i < emps.Count; i++)
                    _store.InsertLine(con, tr, newId, companyId, emps[i].EmployeeId, emps[i].GrossMinor);
            });
            DocumentWorkflowService.Ensure(PayrollCodes.EntityRun, newId, center, identity);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult Submit(long id, ILedgerIdentity identity)
        { return Move(id, TradeCodes.Draft, TradeCodes.Submitted, "SUBMITTED", identity, PayrollPermissions.Create); }

        public TradeResult Approve(long id, ILedgerIdentity identity)
        { return Move(id, TradeCodes.Submitted, TradeCodes.Approved, "APPROVED", identity, PayrollPermissions.Approve); }

        public TradeResult Post(long id, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PayrollPermissions.Post))
                return TradeResult.Fail("PERMISSION", PayrollPermissions.Post);
            PrRun run = _store.GetRun(id);
            if (run == null) return TradeResult.Fail("NOT_FOUND", "Payroll run not found.");
            TradeResult iso = TradeIsolation.DenyIfCrossTenant(identity, run.CompanyId, run.CenterId);
            if (!iso.Ok) return iso;
            if (run.Status != TradeCodes.Approved && !(!_store.RequiresApproval(run.CompanyId) && run.Status == TradeCodes.Draft))
                return TradeResult.Fail("INVALID_STATUS", "Run must be Approved.");
            if (run.TotalMinor <= 0) return TradeResult.Fail("VALIDATION", "Run total must be greater than zero.");
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                AccOutboxWriter.Enqueue(con, tr, LedgerCodes.SourcePayroll, PayrollCodes.DocRun, id, LedgerCodes.OutboxPost, run.CompanyId, run.CenterId, identity.UserName);
            });
            if (!_store.UpdateStatus(id, TradeCodes.Posted, run.RowVersion, LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(PayrollCodes.EntityRun, id, run.CenterId, "POSTED", identity);
            return TradeResult.Success(id, 0);
        }

        public PrRun GetRun(long id) { return _store.GetRun(id); }

        public IList<PayrollSummaryRow> Summary(ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PayrollPermissions.View)) return new List<PayrollSummaryRow>();
            return _store.Summary(Co(identity), identity.IsSuperAdmin && identity.CenterId == 0 ? 0 : identity.CenterId);
        }

        public IList<PayrollRegisterRow> Register(long runId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(PayrollPermissions.View)) return new List<PayrollRegisterRow>();
            PrRun run = _store.GetRun(runId);
            if (run == null || !TradeIsolation.CanSeeCompany(identity, run.CompanyId) || !TradeIsolation.CanSeeCenter(identity, run.CenterId))
                return new List<PayrollRegisterRow>();
            return _store.Register(runId);
        }

        private TradeResult Move(long id, string from, string to, string wf, ILedgerIdentity identity, string perm)
        {
            if (identity == null || !identity.HasPermission(perm))
                return TradeResult.Fail("PERMISSION", perm);
            PrRun run = _store.GetRun(id);
            if (run == null) return TradeResult.Fail("NOT_FOUND", "Payroll run not found.");
            TradeResult iso = TradeIsolation.DenyIfCrossTenant(identity, run.CompanyId, run.CenterId);
            if (!iso.Ok) return iso;
            if (run.Status != from) return TradeResult.Fail("INVALID_STATUS", "Expected " + from);
            if (!_store.UpdateStatus(id, to, run.RowVersion, LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(PayrollCodes.EntityRun, id, run.CenterId, wf, identity);
            return TradeResult.Success(id, 0);
        }

        private static int Co(ILedgerIdentity identity)
        {
            return identity.CompanyId > 0 ? identity.CompanyId : LedgerCodes.DefaultCompanyId;
        }
    }
}

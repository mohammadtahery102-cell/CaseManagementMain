using System;
using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;

namespace CaseManagement.Accounting.Ledger.Application
{
    public class ChartOfAccountsService : IChartOfAccountsService
    {
        private readonly LedgerRepository _repo;

        public ChartOfAccountsService() : this(new LedgerRepository()) { }

        public ChartOfAccountsService(LedgerRepository repo)
        {
            _repo = repo;
        }

        public IList<GlAccount> List(int companyId, bool includeDeleted)
        {
            return _repo.ListAccounts(companyId, includeDeleted);
        }

        public GlAccount Get(long accountId)
        {
            return _repo.GetAccount(accountId);
        }

        public GlCompany GetCompany(int companyId)
        {
            return _repo.GetCompany(companyId);
        }

        public LedgerResult Create(CreateAccountCommand command, ILedgerIdentity identity)
        {
            if (identity == null) return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, "Identity required.");
            if (!identity.HasPermission(LedgerPermissions.ManageCoA))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ManageCoA);

            int companyId = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
            if (string.IsNullOrWhiteSpace(command.AccountCode) || string.IsNullOrWhiteSpace(command.AccountName))
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Account code and name are required.");
            if (string.IsNullOrWhiteSpace(command.AccountTypeCode))
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Account type is required.");

            if (_repo.GetAccountByCode(companyId, command.AccountCode.Trim()) != null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Account code already exists.");

            int level = 1;
            if (command.ParentAccountId.HasValue)
            {
                GlAccount parent = _repo.GetAccount(command.ParentAccountId.Value);
                if (parent == null || parent.IsDeleted || parent.CompanyId != companyId)
                    return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Parent account not found.");
                if (!string.Equals(parent.AccountTypeCode, command.AccountTypeCode, StringComparison.OrdinalIgnoreCase))
                    return LedgerResult.Fail(LedgerErrorCodes.TypeMismatch, "Child type must match parent type.");
                if (parent.Level >= LedgerCodes.MaxAccountDepth)
                    return LedgerResult.Fail(LedgerErrorCodes.Validation, "Maximum account depth exceeded.");
                level = parent.Level + 1;
            }

            bool leaf = command.IsLeaf;
            bool post = leaf && command.AllowPosting;
            if (!leaf) post = false;

            string now = LedgerTime.UtcNow(identity.UtcNow);
            GlAccount a = new GlAccount
            {
                CompanyId = companyId,
                CenterId = LedgerCodes.SharedCenterId,
                AccountCode = command.AccountCode.Trim(),
                AccountName = command.AccountName.Trim(),
                AccountTypeCode = command.AccountTypeCode.Trim().ToUpperInvariant(),
                ParentAccountId = command.ParentAccountId,
                Level = level,
                IsLeaf = leaf,
                AllowPosting = post,
                IsActive = true,
                IsContra = command.IsContra,
                ControlCurrencyCode = command.ControlCurrencyCode,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = identity.UserName,
                UpdatedBy = identity.UserName
            };

            long id = _repo.InsertAccount(a);
            _repo.InsertMasterAudit("Create", "GlAccount", id, null, a.AccountCode, identity);
            return LedgerResult.Entity(id, 1);
        }

        public LedgerResult SoftDelete(SoftDeleteCommand command, ILedgerIdentity identity)
        {
            if (!identity.HasPermission(LedgerPermissions.ManageCoA))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ManageCoA);

            GlAccount a = _repo.GetAccount(command.EntityId);
            if (a == null) return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Account not found.");
            if (a.IsDeleted) return LedgerResult.Fail(LedgerErrorCodes.AlreadyDeleted, "Account already deleted.");
            if (_repo.AccountHasChildren(a.AccountId))
                return LedgerResult.Fail(LedgerErrorCodes.HasChildren, "Account has children.");
            if (_repo.AccountHasPostedLines(a.AccountId))
                return LedgerResult.Fail(LedgerErrorCodes.HasPostings, "Account has posted lines.");

            a.IsDeleted = true;
            a.DeletedAt = LedgerTime.UtcNow(identity.UtcNow);
            a.DeletedBy = identity.UserName;
            a.UpdatedAt = a.DeletedAt;
            a.UpdatedBy = identity.UserName;
            if (!_repo.UpdateAccountConcurrency(a, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            return LedgerResult.Entity(a.AccountId, a.RowVersion + 1);
        }

        public IList<GlAccountType> ListTypes(int companyId)
        {
            return _repo.ListAccountTypes(companyId);
        }

        public LedgerResult Activate(SoftDeleteCommand command, ILedgerIdentity identity)
        {
            if (!identity.HasPermission(LedgerPermissions.ManageCoA))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ManageCoA);

            GlAccount a = _repo.GetAccount(command.EntityId);
            if (a == null || a.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Account not found.");

            a.IsActive = true;
            if (a.IsLeaf) a.AllowPosting = true;
            a.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
            a.UpdatedBy = identity.UserName;
            if (!_repo.UpdateAccountConcurrency(a, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            _repo.InsertMasterAudit("Activate", "GlAccount", a.AccountId, "0", "1", identity);
            return LedgerResult.Entity(a.AccountId, a.RowVersion + 1);
        }

        public LedgerResult Deactivate(SoftDeleteCommand command, ILedgerIdentity identity)
        {
            if (!identity.HasPermission(LedgerPermissions.ManageCoA))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ManageCoA);

            GlAccount a = _repo.GetAccount(command.EntityId);
            if (a == null || a.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Account not found.");

            a.IsActive = false;
            a.AllowPosting = false;
            a.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
            a.UpdatedBy = identity.UserName;
            if (!_repo.UpdateAccountConcurrency(a, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            _repo.InsertMasterAudit("Deactivate", "GlAccount", a.AccountId, "1", "0", identity);
            return LedgerResult.Entity(a.AccountId, a.RowVersion + 1);
        }
    }
}

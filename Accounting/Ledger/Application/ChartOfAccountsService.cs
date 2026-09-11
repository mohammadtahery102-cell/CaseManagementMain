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
            if (_repo.FindAccountByName(companyId, command.AccountName.Trim(), 0) != null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Account name already exists.");
            if (!TypeAllowed(companyId, command.AccountTypeCode))
                return LedgerResult.Fail(LedgerErrorCodes.TypeMismatch, "Unknown account type.");

            int level = 1;
            GlAccount parent = null;
            if (command.ParentAccountId.HasValue)
            {
                parent = _repo.GetAccount(command.ParentAccountId.Value);
                if (parent == null || parent.IsDeleted || parent.CompanyId != companyId)
                    return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Parent account not found.");
                if (!string.Equals(parent.AccountTypeCode, command.AccountTypeCode, StringComparison.OrdinalIgnoreCase))
                    return LedgerResult.Fail(LedgerErrorCodes.TypeMismatch, "Child type must match parent type.");
                if (parent.Level >= LedgerCodes.MaxAccountDepth)
                    return LedgerResult.Fail(LedgerErrorCodes.Validation, "Maximum account depth exceeded.");
                if (parent.IsLeaf && _repo.AccountHasPostedLines(parent.AccountId))
                    return LedgerResult.Fail(LedgerErrorCodes.HasPostings, "Cannot add a child under an account that has ledger movements.");
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
            if (parent != null && parent.IsLeaf)
            {
                parent.IsLeaf = false;
                parent.AllowPosting = false;
                parent.UpdatedAt = now;
                parent.UpdatedBy = identity.UserName;
                _repo.UpdateAccountConcurrency(parent, parent.RowVersion);
            }
            _repo.InsertMasterAudit("Create", "GlAccount", id, null, a.AccountCode, identity);
            return LedgerResult.Entity(id, 1);
        }

        public LedgerResult Update(UpdateAccountCommand command, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.ManageCoA))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ManageCoA);
            if (command == null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Command required.");

            GlAccount a = _repo.GetAccount(command.AccountId);
            if (a == null || a.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Account not found.");

            string code = (command.AccountCode ?? a.AccountCode ?? "").Trim();
            string name = (command.AccountName ?? a.AccountName ?? "").Trim();
            string type = string.IsNullOrWhiteSpace(command.AccountTypeCode)
                ? a.AccountTypeCode : command.AccountTypeCode.Trim().ToUpperInvariant();
            if (code.Length == 0 || name.Length == 0)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Account code and name are required.");
            if (!TypeAllowed(a.CompanyId, type))
                return LedgerResult.Fail(LedgerErrorCodes.TypeMismatch, "Unknown account type.");

            GlAccount byCode = _repo.GetAccountByCode(a.CompanyId, code);
            if (byCode != null && byCode.AccountId != a.AccountId)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Account code already exists.");
            GlAccount byName = _repo.FindAccountByName(a.CompanyId, name, a.AccountId);
            if (byName != null)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Account name already exists.");

            bool hasPostings = _repo.AccountHasPostedLines(a.AccountId);
            bool hasChildren = _repo.AccountHasChildren(a.AccountId);
            if (hasPostings && !string.Equals(type, a.AccountTypeCode, StringComparison.OrdinalIgnoreCase))
                return LedgerResult.Fail(LedgerErrorCodes.HasPostings, "Cannot change type of an account with ledger movements.");
            if (hasPostings && !string.Equals(code, a.AccountCode, StringComparison.Ordinal))
                return LedgerResult.Fail(LedgerErrorCodes.HasPostings, "Cannot change the code of an account with ledger movements.");

            long? newParent = command.ParentAccountId;
            int level = 1;
            if (newParent.HasValue)
            {
                if (newParent.Value == a.AccountId)
                    return LedgerResult.Fail(LedgerErrorCodes.Cycle, "An account cannot be its own parent.");
                if (CreatesCycle(a.AccountId, newParent.Value))
                    return LedgerResult.Fail(LedgerErrorCodes.Cycle, "Circular parent reference.");
                GlAccount parent = _repo.GetAccount(newParent.Value);
                if (parent == null || parent.IsDeleted || parent.CompanyId != a.CompanyId)
                    return LedgerResult.Fail(LedgerErrorCodes.AccountMissing, "Parent account not found.");
                if (!string.Equals(parent.AccountTypeCode, type, StringComparison.OrdinalIgnoreCase))
                    return LedgerResult.Fail(LedgerErrorCodes.TypeMismatch, "Child type must match parent type.");
                if (parent.IsLeaf && _repo.AccountHasPostedLines(parent.AccountId))
                    return LedgerResult.Fail(LedgerErrorCodes.HasPostings, "Cannot move under an account that has ledger movements.");
                if (parent.Level >= LedgerCodes.MaxAccountDepth)
                    return LedgerResult.Fail(LedgerErrorCodes.Validation, "Maximum account depth exceeded.");
                level = parent.Level + 1;
                if (parent.IsLeaf)
                {
                    parent.IsLeaf = false;
                    parent.AllowPosting = false;
                    parent.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
                    parent.UpdatedBy = identity.UserName;
                    _repo.UpdateAccountConcurrency(parent, parent.RowVersion);
                }
            }

            if (hasChildren && !string.Equals(type, a.AccountTypeCode, StringComparison.OrdinalIgnoreCase))
                return LedgerResult.Fail(LedgerErrorCodes.HasChildren, "Cannot change type while children exist.");

            a.AccountCode = code;
            a.AccountName = name;
            a.AccountTypeCode = type;
            a.ParentAccountId = newParent;
            a.Level = level;
            a.IsContra = command.IsContra;
            a.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
            a.UpdatedBy = identity.UserName;
            if (!_repo.UpdateAccountConcurrency(a, command.ExpectedRowVersion > 0 ? command.ExpectedRowVersion : a.RowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            _repo.InsertMasterAudit("Update", "GlAccount", a.AccountId, null, code, identity);
            return LedgerResult.Entity(a.AccountId, a.RowVersion + 1);
        }

        public AccountUsageInfo GetUsage(long accountId, ILedgerIdentity identity)
        {
            AccountUsageInfo info = new AccountUsageInfo();
            info.AccountId = accountId;
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return info;
            GlAccount a = _repo.GetAccount(accountId);
            if (a == null) return info;
            info.AccountCode = a.AccountCode;
            info.AccountName = a.AccountName;
            info.ChildCount = _repo.AccountChildCount(accountId);
            int posted;
            string last;
            _repo.QueryAccountUsage(accountId, out posted, out last);
            info.PostedLineCount = posted;
            info.LastPostingDate = last;
            return info;
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

        private bool CreatesCycle(long accountId, long parentId)
        {
            long current = parentId;
            int guard = 0;
            while (current > 0 && guard++ < LedgerCodes.MaxAccountDepth + 2)
            {
                if (current == accountId) return true;
                GlAccount node = _repo.GetAccount(current);
                if (node == null || !node.ParentAccountId.HasValue) return false;
                current = node.ParentAccountId.Value;
            }
            return false;
        }

        private bool TypeAllowed(int companyId, string typeCode)
        {
            if (string.IsNullOrWhiteSpace(typeCode)) return false;
            IList<GlAccountType> types = _repo.ListAccountTypes(companyId);
            string want = typeCode.Trim().ToUpperInvariant();
            for (int i = 0; i < types.Count; i++)
                if (string.Equals(types[i].AccountTypeCode, want, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}

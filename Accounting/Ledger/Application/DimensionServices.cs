using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;

namespace CaseManagement.Accounting.Ledger.Application
{
    public class CostCenterService : ICostCenterService
    {
        private readonly LedgerRepository _repo;

        public CostCenterService() : this(new LedgerRepository()) { }

        public CostCenterService(LedgerRepository repo) { _repo = repo; }

        public IList<GlCostCenter> List(int companyId, bool includeInactive)
        {
            return _repo.ListCostCenters(companyId, includeInactive);
        }

        public GlCostCenter Get(long costCenterId) { return _repo.GetCostCenter(costCenterId); }

        public LedgerResult Create(CreateDimensionCommand command, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.ManageCostCenter))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ManageCostCenter);
            if (command == null || string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Code and name are required.");

            int companyId = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
            int level = 1;
            if (command.ParentId.HasValue)
            {
                GlCostCenter parent = _repo.GetCostCenter(command.ParentId.Value);
                if (parent == null || parent.IsDeleted || parent.CompanyId != companyId)
                    return LedgerResult.Fail(LedgerErrorCodes.DimensionMissing, "Parent cost center not found.");
                if (parent.Level >= LedgerCodes.MaxAccountDepth)
                    return LedgerResult.Fail(LedgerErrorCodes.Validation, "Maximum depth exceeded.");
                level = parent.Level + 1;
            }

            string now = LedgerTime.UtcNow(identity.UtcNow);
            GlCostCenter n = new GlCostCenter
            {
                CompanyId = companyId,
                CenterId = LedgerCodes.SharedCenterId,
                Code = command.Code.Trim(),
                Name = command.Name.Trim(),
                ParentCostCenterId = command.ParentId,
                Level = level,
                IsLeaf = command.IsLeaf,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = identity.UserName,
                UpdatedBy = identity.UserName
            };
            long id = _repo.InsertCostCenter(n);
            _repo.InsertMasterAudit("Create", "GlCostCenter", id, null, n.Code, identity);
            return LedgerResult.Entity(id, 1);
        }

        public LedgerResult Activate(SoftDeleteCommand command, ILedgerIdentity identity)
        {
            return SetActive(command, identity, true);
        }

        public LedgerResult Deactivate(SoftDeleteCommand command, ILedgerIdentity identity)
        {
            return SetActive(command, identity, false);
        }

        private LedgerResult SetActive(SoftDeleteCommand command, ILedgerIdentity identity, bool active)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.ManageCostCenter))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ManageCostCenter);
            GlCostCenter n = _repo.GetCostCenter(command.EntityId);
            if (n == null || n.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.DimensionMissing, "Cost center not found.");
            n.IsActive = active;
            n.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
            n.UpdatedBy = identity.UserName;
            if (!_repo.UpdateCostCenterConcurrency(n, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            _repo.InsertMasterAudit(active ? "Activate" : "Deactivate", "GlCostCenter", n.CostCenterId, null, n.Code, identity);
            return LedgerResult.Entity(n.CostCenterId, n.RowVersion + 1);
        }
    }

    public class ProjectService : IProjectService
    {
        private readonly LedgerRepository _repo;

        public ProjectService() : this(new LedgerRepository()) { }

        public ProjectService(LedgerRepository repo) { _repo = repo; }

        public IList<GlProject> List(int companyId, bool includeInactive)
        {
            return _repo.ListProjects(companyId, includeInactive);
        }

        public GlProject Get(long projectId) { return _repo.GetProject(projectId); }

        public LedgerResult Create(CreateDimensionCommand command, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.ManageProject))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ManageProject);
            if (command == null || string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Code and name are required.");

            int companyId = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
            int level = 1;
            if (command.ParentId.HasValue)
            {
                GlProject parent = _repo.GetProject(command.ParentId.Value);
                if (parent == null || parent.IsDeleted || parent.CompanyId != companyId)
                    return LedgerResult.Fail(LedgerErrorCodes.DimensionMissing, "Parent project not found.");
                if (parent.Level >= LedgerCodes.MaxAccountDepth)
                    return LedgerResult.Fail(LedgerErrorCodes.Validation, "Maximum depth exceeded.");
                level = parent.Level + 1;
            }

            string now = LedgerTime.UtcNow(identity.UtcNow);
            GlProject n = new GlProject
            {
                CompanyId = companyId,
                CenterId = LedgerCodes.SharedCenterId,
                Code = command.Code.Trim(),
                Name = command.Name.Trim(),
                ParentProjectId = command.ParentId,
                Level = level,
                IsLeaf = command.IsLeaf,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = identity.UserName,
                UpdatedBy = identity.UserName
            };
            long id = _repo.InsertProject(n);
            _repo.InsertMasterAudit("Create", "GlProject", id, null, n.Code, identity);
            return LedgerResult.Entity(id, 1);
        }

        public LedgerResult Activate(SoftDeleteCommand command, ILedgerIdentity identity)
        {
            return SetActive(command, identity, true);
        }

        public LedgerResult Deactivate(SoftDeleteCommand command, ILedgerIdentity identity)
        {
            return SetActive(command, identity, false);
        }

        private LedgerResult SetActive(SoftDeleteCommand command, ILedgerIdentity identity, bool active)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.ManageProject))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.ManageProject);
            GlProject n = _repo.GetProject(command.EntityId);
            if (n == null || n.IsDeleted)
                return LedgerResult.Fail(LedgerErrorCodes.DimensionMissing, "Project not found.");
            n.IsActive = active;
            n.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
            n.UpdatedBy = identity.UserName;
            if (!_repo.UpdateProjectConcurrency(n, command.ExpectedRowVersion))
                return LedgerResult.Fail(LedgerErrorCodes.ConcurrencyConflict, "RowVersion mismatch.");
            _repo.InsertMasterAudit(active ? "Activate" : "Deactivate", "GlProject", n.ProjectId, null, n.Code, identity);
            return LedgerResult.Entity(n.ProjectId, n.RowVersion + 1);
        }
    }
}

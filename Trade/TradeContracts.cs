using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Enterprise;

namespace CaseManagement.Trade
{
    public class TradeResult
    {
        public bool Ok { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public long EntityId { get; set; }
        public long RowVersion { get; set; }

        public static TradeResult Success(long id, long rowVersion)
        {
            return new TradeResult { Ok = true, EntityId = id, RowVersion = rowVersion };
        }

        public static TradeResult Fail(string code, string message)
        {
            return new TradeResult { Ok = false, ErrorCode = code, Message = message };
        }
    }

    public static class TradeIsolation
    {
        public static bool CanSeeCompany(ILedgerIdentity identity, int companyId)
        {
            if (identity == null) return false;
            if (identity.IsSuperAdmin) return true;
            if (identity.CompanyId <= 0 || companyId <= 0) return identity.CompanyId <= 0;
            return identity.CompanyId == companyId;
        }

        public static bool CanSeeCenter(ILedgerIdentity identity, int centerId)
        {
            if (identity == null) return false;
            if (identity.IsSuperAdmin && identity.CenterId == 0) return true;
            if (identity.CenterId <= 0) return identity.IsSuperAdmin;
            if (centerId <= 0) return false;
            return identity.CenterId == centerId;
        }

        public static TradeResult DenyIfCrossTenant(ILedgerIdentity identity, int companyId, int centerId)
        {
            if (!CanSeeCompany(identity, companyId) || !CanSeeCenter(identity, centerId))
                return TradeResult.Fail("PERMISSION", "Cross-company or cross-branch access is not allowed.");
            return TradeResult.Success(0, 0);
        }

        public static int ResolveCompany(ILedgerIdentity identity, int requested)
        {
            if (identity != null && !identity.IsSuperAdmin && identity.CompanyId > 0)
                return identity.CompanyId;
            if (requested > 0) return requested;
            if (identity != null && identity.CompanyId > 0) return identity.CompanyId;
            return CaseManagement.Accounting.Ledger.Domain.LedgerCodes.DefaultCompanyId;
        }
    }

    public static class TradeCodes
    {
        public const string Draft = "Draft";
        public const string Submitted = "Submitted";
        public const string Approved = "Approved";
        public const string Posted = "Posted";
        public const string Rejected = "Rejected";
        public const string Converted = "Converted";
    }

    /// <summary>
    /// Thin adapter over existing EntWorkflow. Does not change WorkflowService.
    /// Document status remains source of truth for posting gates.
    /// </summary>
    public static class DocumentWorkflowService
    {
        public static void Ensure(string entityName, long documentId, int centerId, ILedgerIdentity identity)
        {
            if (documentId <= 0 || documentId > int.MaxValue) return;
            BindGate(identity);
            WorkflowService.EnsureInstance(entityName, (int)documentId, centerId);
        }

        public static TradeResult MoveTo(string entityName, long documentId, int centerId, string toStateCode, ILedgerIdentity identity)
        {
            if (documentId <= 0 || documentId > int.MaxValue)
                return TradeResult.Fail("WORKFLOW", "Document id is not compatible with EntWorkflow EntityID.");
            BindGate(identity);
            WorkflowInstanceModel inst = WorkflowService.EnsureInstance(entityName, (int)documentId, centerId);
            if (inst == null) return TradeResult.Success(documentId, 0);

            System.Collections.Generic.List<WorkflowTransitionModel> list = WorkflowService.GetAvailableTransitions(inst);
            for (int i = 0; i < list.Count; i++)
            {
                WorkflowStateModel to = WorkflowService.GetState(list[i].ToStateID);
                if (to != null && to.Code == toStateCode)
                {
                    WorkflowActionResult r = WorkflowService.ApplyTransition(inst, list[i].TransitionID, toStateCode);
                    if (r.Applied || r.PendingApproval) return TradeResult.Success(documentId, 0);
                    return TradeResult.Fail("WORKFLOW", r.Message);
                }
            }
            return TradeResult.Success(documentId, 0);
        }

        private static void BindGate(ILedgerIdentity identity)
        {
            ILedgerIdentity id = identity;
            WorkflowService.PermissionGate = delegate (string key)
            {
                return id != null && id.HasPermission(key);
            };
        }
    }
}

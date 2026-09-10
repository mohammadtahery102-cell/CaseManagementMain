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

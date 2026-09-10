using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Enterprise;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting.Ledger.Adapters
{
    /// <summary>
    /// Desktop session adapter. Maps SecurityContext → ILedgerIdentity.
    /// PostingEngine must never call this; only WinForms (or future hosts) do.
    /// </summary>
    public static class DesktopLedgerIdentity
    {
        public static ILedgerIdentity FromSession()
        {
            return FromSession(LedgerCodes.DefaultCompanyId);
        }

        public static ILedgerIdentity FromSession(int companyId)
        {
            LedgerIdentity identity = new LedgerIdentity();
            identity.UserName = SecurityContext.Username ?? "";
            identity.UserId = SecurityContext.UserId;
            identity.CompanyId = companyId;
            identity.CenterId = SecurityContext.CurrentCenterId;
            identity.IsSuperAdmin = SecurityContext.IsSuperAdmin();
            identity.UtcNow = System.DateTime.UtcNow;
            identity.PermissionChecker = PermissionService.HasPermission;
            return identity;
        }
    }
}

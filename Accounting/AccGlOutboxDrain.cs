using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Adapters;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting
{
    /// <summary>
    /// Best-effort drain after Acc commit. Never called from inside the Acc SQLite transaction.
    /// </summary>
    public static class AccGlOutboxDrain
    {
        public static int AfterAccCommit()
        {
            try
            {
                return new IntegrationOutboxProcessor().ProcessDue(DesktopLedgerIdentity.FromSession());
            }
            catch
            {
                return 0;
            }
        }
    }
}

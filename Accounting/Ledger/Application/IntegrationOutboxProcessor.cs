using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;

namespace CaseManagement.Accounting.Ledger.Application
{
    public interface IOutboxHandler
    {
        bool CanHandle(string sourceModule);
        LedgerResult Handle(AccOutboxRow row, ILedgerIdentity identity);
    }

    public interface IOutboxProcessor
    {
        int ProcessDue(ILedgerIdentity identity);
        LedgerResult Requeue(long outboxId, ILedgerIdentity identity);
    }

    public interface IOutboxMonitor
    {
        OutboxSnapshot GetSnapshot(ILedgerIdentity identity);
    }

    public class CashBookOutboxHandler : IOutboxHandler
    {
        private readonly ICashBookGlService _cash;

        public CashBookOutboxHandler() : this(new CashBookGlService()) { }

        public CashBookOutboxHandler(ICashBookGlService cash) { _cash = cash; }

        public bool CanHandle(string sourceModule)
        {
            return sourceModule == LedgerCodes.SourceCashBook;
        }

        public LedgerResult Handle(AccOutboxRow row, ILedgerIdentity identity)
        {
            if (row.DocumentType != LedgerCodes.DocAccTransaction)
                return LedgerResult.Fail(LedgerErrorCodes.Validation, "Unsupported cash-book document type.");
            if (row.Operation == LedgerCodes.OutboxReverse)
                return _cash.ReverseIfVoided(row.DocumentId, identity);
            return _cash.PostTransaction(row.DocumentId, identity);
        }
    }

    public class IntegrationOutboxProcessor : IOutboxProcessor, IOutboxMonitor
    {
        private readonly AccOutboxStore _store;
        private readonly LedgerRepository _audit;
        private readonly IList<IOutboxHandler> _handlers;

        public IntegrationOutboxProcessor()
            : this(new AccOutboxStore(), new LedgerRepository(),                 new IOutboxHandler[]
            {
                new CashBookOutboxHandler(),
                new CaseManagement.Inventory.Application.InventoryOutboxAdapter(),
                new CaseManagement.Purchase.Application.PurchaseOutboxAdapter(),
                new CaseManagement.Sales.Application.SalesOutboxAdapter(),
                new CaseManagement.Assets.Application.AssetOutboxAdapter(),
                new CaseManagement.Payroll.Application.PayrollOutboxAdapter(),
                new CaseManagement.Pos.Application.PosOutboxAdapter()
            })
        {
        }

        public IntegrationOutboxProcessor(AccOutboxStore store, LedgerRepository audit, IList<IOutboxHandler> handlers)
        {
            _store = store;
            _audit = audit;
            _handlers = handlers;
        }

        public OutboxSnapshot GetSnapshot(ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.View))
                return new OutboxSnapshot { Recent = new List<AccOutboxRow>() };
            return _store.Snapshot();
        }

        public LedgerResult Requeue(long outboxId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(LedgerPermissions.OutboxProcess))
                return LedgerResult.Fail(LedgerErrorCodes.PermissionDenied, LedgerPermissions.OutboxProcess);
            if (!_store.Requeue(outboxId))
                return LedgerResult.Fail(LedgerErrorCodes.InvalidStatus, "Row is not Failed or DeadLetter.");
            _audit.InsertMasterAudit("OutboxRequeue", "AccOutbox", outboxId, LedgerCodes.OutboxDeadLetter, LedgerCodes.OutboxPending, identity);
            return LedgerResult.Entity(outboxId, 1);
        }

        public int ProcessDue(ILedgerIdentity identity)
        {
            if (identity == null || !(identity.HasPermission(LedgerPermissions.OutboxProcess) || identity.HasPermission(LedgerPermissions.Post)))
                return 0;

            IList<AccOutboxRow> due = _store.ListDue(identity.UtcNow, 25);
            int done = 0;
            for (int i = 0; i < due.Count; i++)
            {
                AccOutboxRow row = due[i];
                if (!_store.Claim(row.OutboxId, identity.UserName, identity.UtcNow))
                    continue;

                IOutboxHandler handler = Find(row.SourceModule);
                if (handler == null)
                {
                    _store.Fail(row, LedgerErrorCodes.Validation, "No handler for " + row.SourceModule, identity.UtcNow);
                    _audit.InsertMasterAudit("OutboxFail", "AccOutbox", row.OutboxId, row.Status, "NoHandler", identity);
                    continue;
                }

                LedgerResult result;
                try
                {
                    result = handler.Handle(row, identity);
                }
                catch (System.Exception ex)
                {
                    result = LedgerResult.Fail(LedgerErrorCodes.Validation, ex.Message);
                }

                if (result != null && result.Ok)
                {
                    _store.Complete(row.OutboxId, identity.UtcNow);
                    _audit.InsertMasterAudit("OutboxComplete", "AccOutbox", row.OutboxId, row.Operation, result.JournalId.ToString(), identity);
                    done++;
                }
                else
                {
                    string code = result != null ? result.ErrorCode : LedgerErrorCodes.Validation;
                    string msg = result != null ? result.Message : "Handler failed.";
                    _store.Fail(row, code, msg, identity.UtcNow);
                    int next = row.AttemptCount + 1;
                    int max = row.MaxAttempts > 0 ? row.MaxAttempts : 8;
                    string op = next >= max ? "OutboxDeadLetter" : "OutboxFail";
                    _audit.InsertMasterAudit(op, "AccOutbox", row.OutboxId, code, msg, identity);
                }
            }
            return done;
        }

        private IOutboxHandler Find(string module)
        {
            for (int i = 0; i < _handlers.Count; i++)
                if (_handlers[i].CanHandle(module)) return _handlers[i];
            return null;
        }
    }
}

using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Infrastructure;
using CaseManagement.Inventory.Domain;
using CaseManagement.Inventory.Infrastructure;

namespace CaseManagement.Inventory.Application
{
    public class InventoryQueryService
    {
        private readonly InventoryStore _store;
        private readonly LedgerRepository _repo;

        public InventoryQueryService() : this(new InventoryStore(), new LedgerRepository()) { }

        public InventoryQueryService(InventoryStore store, LedgerRepository repo)
        {
            _store = store;
            _repo = repo;
        }

        public IList<InvItem> ListItems(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InvItem>();
            return _store.ListItems(Company(identity));
        }

        public IList<InvWarehouse> ListWarehouses(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InvWarehouse>();
            return _store.ListWarehouses(Company(identity), CenterFilter(identity));
        }

        public IList<InvDocument> ListDocuments(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InvDocument>();
            return _store.ListDocuments(Company(identity), CenterFilter(identity));
        }

        public InvDocument GetDocument(long id, ILedgerIdentity identity)
        {
            if (!CanView(identity)) return null;
            return _store.GetDocument(id);
        }

        public IList<InvDocumentLine> ListLines(long documentId, ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InvDocumentLine>();
            return _store.ListLines(documentId);
        }

        public IList<StockOnHandRow> StockOnHand(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<StockOnHandRow>();
            return _store.ListOnHand(Company(identity), CenterFilter(identity));
        }

        public IList<StockOnHandRow> Valuation(ILedgerIdentity identity)
        {
            return StockOnHand(identity);
        }

        public IList<InvItemLedger> Ledger(long itemId, ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InvItemLedger>();
            return _store.ListLedger(Company(identity), itemId);
        }

        public IList<ReorderRow> Reorder(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<ReorderRow>();
            return _store.ListReorder(Company(identity), CenterFilter(identity));
        }

        public InventoryVsGlRow VsGl(ILedgerIdentity identity)
        {
            InventoryVsGlRow row = new InventoryVsGlRow();
            if (!CanView(identity)) return row;
            int companyId = Company(identity);
            row.InventoryValueMinor = _store.SumBalanceValue(companyId);
            IList<long> accounts = _store.ListInventoryMapAccountIds(companyId);
            row.GlNetMinor = _store.SumGlNet(companyId, accounts);
            if (accounts.Count > 0)
            {
                row.AccountId = accounts[0];
                CaseManagement.Accounting.Ledger.Domain.GlAccount acc = _repo.GetAccount(accounts[0]);
                if (acc != null) row.AccountCode = acc.AccountCode;
            }
            row.DifferenceMinor = row.InventoryValueMinor - row.GlNetMinor;
            return row;
        }

        public InvItemBalance GetBalance(int companyId, long itemId, long warehouseId, long locationId)
        {
            return _store.GetBalance(companyId, itemId, warehouseId, locationId);
        }

        public long DefaultWarehouseId(int companyId)
        {
            return _store.DefaultWarehouseId(companyId);
        }

        public long DefaultLocationId(long warehouseId)
        {
            return _store.DefaultLocationId(warehouseId);
        }

        private static bool CanView(ILedgerIdentity identity)
        {
            return identity != null && (identity.IsSuperAdmin || identity.HasPermission(InventoryPermissions.View));
        }

        private static int Company(ILedgerIdentity identity)
        {
            return identity.CompanyId > 0 ? identity.CompanyId : CaseManagement.Accounting.Ledger.Domain.LedgerCodes.DefaultCompanyId;
        }

        private static int CenterFilter(ILedgerIdentity identity)
        {
            if (identity.IsSuperAdmin && identity.CenterId == 0) return 0;
            return identity.CenterId;
        }
    }
}

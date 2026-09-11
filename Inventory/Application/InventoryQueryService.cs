using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Infrastructure;
using CaseManagement.Inventory.Domain;
using CaseManagement.Inventory.Infrastructure;
using CaseManagement.Trade;

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
            return ListItems(identity, null, true);
        }

        public IList<InvItem> ListItems(ILedgerIdentity identity, string search, bool includeInactive)
        {
            if (!CanView(identity)) return new List<InvItem>();
            return _store.ListItems(Company(identity), search, includeInactive);
        }

        public IList<InvItem> ListItems(ILedgerIdentity identity, InvItemFilter filter)
        {
            if (!CanView(identity)) return new List<InvItem>();
            if (filter == null) return _store.ListItems(Company(identity));
            return _store.ListItems(Company(identity), filter.Query, filter.CategoryId, filter.ActiveMode);
        }

        public IList<InvItem> ListActiveItems(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InvItem>();
            return _store.ListItems(Company(identity), null, 0, 1);
        }

        public InvItem GetItem(long itemId, ILedgerIdentity identity)
        {
            if (!CanView(identity)) return null;
            InvItem item = _store.GetItem(itemId);
            if (item == null || !TradeIsolation.CanSeeCompany(identity, item.CompanyId)) return null;
            return item;
        }

        public IList<InvItemCategory> ListCategories(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InvItemCategory>();
            return _store.ListCategories(Company(identity));
        }

        public IList<InvUnitOfMeasure> ListUoms(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InvUnitOfMeasure>();
            return _store.ListUoms(Company(identity));
        }

        public IList<InvDocument> ListDocuments(ILedgerIdentity identity)
        {
            return ListDocuments(identity, null, null);
        }

        public IList<InvDocument> ListDocuments(ILedgerIdentity identity, string typeFilter, string search)
        {
            if (!CanView(identity)) return new List<InvDocument>();
            return _store.ListDocuments(Company(identity), CenterFilter(identity), typeFilter, search, 400);
        }

        public IList<InvWarehouse> ListWarehouses(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InvWarehouse>();
            return _store.ListWarehouses(Company(identity), CenterFilter(identity));
        }

        public InvDocument GetDocument(long id, ILedgerIdentity identity)
        {
            if (!CanView(identity)) return null;
            InvDocument doc = _store.GetDocument(id);
            if (doc == null) return null;
            if (!TradeIsolation.CanSeeCompany(identity, doc.CompanyId) || !TradeIsolation.CanSeeCenter(identity, doc.CenterId))
                return null;
            return doc;
        }

        public IList<InvDocumentLine> ListLines(long documentId, ILedgerIdentity identity)
        {
            if (GetDocument(documentId, identity) == null) return new List<InvDocumentLine>();
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

        public long OnHand(int companyId, long itemId)
        {
            return _store.ItemOnHand(companyId, itemId);
        }

        public long OnHandAt(int companyId, long itemId, long warehouseId, long locationId)
        {
            InvItemBalance b = _store.GetBalance(companyId, itemId, warehouseId, locationId);
            return b == null ? 0 : b.QuantityOnHand;
        }

        public IList<InvLocation> ListLocations(long warehouseId, ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InvLocation>();
            return _store.ListLocations(warehouseId);
        }

        public IList<InventoryIntegrityRow> Integrity(ILedgerIdentity identity)
        {
            if (!CanView(identity)) return new List<InventoryIntegrityRow>();
            return _store.ListIntegrity(Company(identity), CenterFilter(identity));
        }

        public InventoryIntegritySummary IntegritySummary(ILedgerIdentity identity)
        {
            InventoryIntegritySummary summary = new InventoryIntegritySummary();
            summary.Rows = Integrity(identity);
            summary.TotalItems = summary.Rows.Count;
            int balanced = 0;
            for (int i = 0; i < summary.Rows.Count; i++)
                if (summary.Rows[i].Balanced) balanced++;
            summary.BalancedItems = balanced;
            summary.BrokenItems = summary.TotalItems - balanced;
            summary.IntegrityPercent = summary.TotalItems == 0 ? 100d : (balanced * 100d / summary.TotalItems);
            return summary;
        }

        public IList<InventoryVelocityRow> Velocity(ILedgerIdentity identity, bool fast)
        {
            if (!CanView(identity)) return new List<InventoryVelocityRow>();
            return _store.ListVelocity(Company(identity), CenterFilter(identity), fast);
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

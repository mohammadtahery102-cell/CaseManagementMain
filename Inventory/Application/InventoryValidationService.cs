using System;
using System.Collections.Generic;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Inventory.Domain;
using CaseManagement.Inventory.Infrastructure;
using CaseManagement.Trade;

namespace CaseManagement.Inventory.Application
{
    public class InventoryValidationService
    {
        private readonly InventoryStore _store;
        private readonly IFiscalCalendarService _calendar;

        public InventoryValidationService()
            : this(new InventoryStore(), new FiscalCalendarService()) { }

        public InventoryValidationService(InventoryStore store, IFiscalCalendarService calendar)
        {
            _store = store;
            _calendar = calendar;
        }

        public InventoryResult GuardIdentity(ILedgerIdentity identity, string permission)
        {
            if (identity == null) return InventoryResult.Fail("PERMISSION", "Identity is required.");
            if (!identity.HasPermission(permission))
                return InventoryResult.Fail("PERMISSION", permission);
            return InventoryResult.Success(0, 0);
        }

        public InventoryResult GuardPeriod(int companyId, string postingDate)
        {
            GlFiscalPeriod period = _calendar.Resolve(companyId, postingDate);
            if (period == null || period.IsDeleted)
                return InventoryResult.Fail("PERIOD_CLOSED", "No open fiscal period for posting date.");
            if (period.Status != LedgerCodes.StatusOpen)
                return InventoryResult.Fail("PERIOD_CLOSED", "Fiscal period is not Open.");
            return InventoryResult.Success(period.FiscalPeriodId, period.RowVersion);
        }

        public InventoryResult GuardDocument(InvDocument doc, InvWarehouse warehouse, IList<InvDocumentLine> lines,
            ILedgerIdentity identity, bool allowNegative)
        {
            if (doc == null) return InventoryResult.Fail("NOT_FOUND", "Document not found.");
            if (warehouse == null || !warehouse.IsActive)
                return InventoryResult.Fail("VALIDATION", "Warehouse not found.");
            if (warehouse.CompanyId != doc.CompanyId)
                return InventoryResult.Fail("VALIDATION", "Warehouse company mismatch.");
            if (warehouse.CenterId != doc.CenterId)
                return InventoryResult.Fail(InventoryCodes.InterBranch, "Document CenterID must match warehouse CenterID.");
            if (!TradeIsolation.CanSeeCompany(identity, doc.CompanyId) || !TradeIsolation.CanSeeCenter(identity, doc.CenterId))
                return InventoryResult.Fail("PERMISSION", "Cross-company or cross-branch inventory is not allowed.");
            if (lines == null || lines.Count == 0)
                return InventoryResult.Fail("VALIDATION", "At least one line is required.");

            for (int i = 0; i < lines.Count; i++)
            {
                InvDocumentLine line = lines[i];
                if (line.QtyBase == 0 && doc.DocumentType != InventoryCodes.TypeCount)
                    return InventoryResult.Fail("VALIDATION", "QtyBase cannot be zero.");
                InvItem item = _store.GetItem(line.ItemId);
                if (item == null || !item.IsStockable)
                    return InventoryResult.Fail("VALIDATION", "Item is not stockable.");
                if (!item.IsActive)
                    return InventoryResult.Fail(InventoryCodes.InactiveItem, "Item is inactive.");
                if (item.CompanyId != doc.CompanyId)
                    return InventoryResult.Fail("VALIDATION", "Item company mismatch.");
                InvLocation loc = _store.GetLocation(line.LocationId);
                if (doc.DocumentType == InventoryCodes.TypeTransfer)
                {
                    if (loc == null || !loc.IsLeaf || loc.WarehouseId != doc.WarehouseId)
                        return InventoryResult.Fail("VALIDATION", "Source location must belong to the source warehouse.");
                    if (doc.ToLocationId.HasValue && doc.ToLocationId.Value == line.LocationId
                        && (!doc.ToWarehouseId.HasValue || doc.ToWarehouseId.Value == doc.WarehouseId))
                        return InventoryResult.Fail("VALIDATION", "Source and destination cannot be the same.");
                }
                else if (loc == null || !loc.IsLeaf || loc.WarehouseId != doc.WarehouseId)
                    return InventoryResult.Fail("VALIDATION", "Location must be a leaf of the document warehouse.");

                bool outbound = IsOutbound(doc.DocumentType, line.QtyBase);
                long absQty = line.QtyBase < 0 ? -line.QtyBase : line.QtyBase;
                if (outbound && !allowNegative)
                {
                    InventoryStore.WarehouseTotals tot = _store.GetWarehouseTotals(doc.CompanyId, item.ItemId, doc.WarehouseId);
                    InvItemBalance locBal = _store.GetBalance(doc.CompanyId, item.ItemId, doc.WarehouseId, line.LocationId);
                    long locQty = locBal == null ? 0 : locBal.QuantityOnHand;
                    if (locQty < absQty || tot.Qty < absQty)
                        return InventoryResult.Fail(InventoryCodes.NegativeStock, "Insufficient quantity on hand.");
                }
            }

            if (doc.DocumentType == InventoryCodes.TypeTransfer)
            {
                InventoryResult tr = GuardTransfer(doc, identity);
                if (!tr.Ok) return tr;
            }
            return InventoryResult.Success(doc.DocumentId, doc.RowVersion);
        }

        public InventoryResult GuardTransfer(InvDocument doc, ILedgerIdentity identity)
        {
            long destWhId = doc.ToWarehouseId.HasValue ? doc.ToWarehouseId.Value : 0;
            InvLocation destLoc = doc.ToLocationId.HasValue ? _store.GetLocation(doc.ToLocationId.Value) : null;
            if (destWhId <= 0 && destLoc != null) destWhId = destLoc.WarehouseId;
            if (destWhId <= 0)
                return InventoryResult.Fail("VALIDATION", "Destination warehouse is required.");
            InvWarehouse dest = _store.GetWarehouse(destWhId);
            if (dest == null || !dest.IsActive)
                return InventoryResult.Fail("VALIDATION", "Destination warehouse not found.");
            if (dest.CompanyId != doc.CompanyId)
                return InventoryResult.Fail("VALIDATION", "Destination warehouse company mismatch.");
            if (dest.CenterId != doc.CenterId)
                return InventoryResult.Fail(InventoryCodes.InterBranch, "Same-branch transfer only.");
            if (!TradeIsolation.CanSeeCenter(identity, dest.CenterId))
                return InventoryResult.Fail("PERMISSION", "Cross-warehouse access is not allowed.");
            if (destLoc == null)
                destLoc = _store.GetLocation(_store.DefaultLocationId(destWhId));
            if (destLoc == null || !destLoc.IsLeaf || destLoc.WarehouseId != destWhId)
                return InventoryResult.Fail("VALIDATION", "Destination location must be a leaf of the destination warehouse.");
            return InventoryResult.Success(doc.DocumentId, doc.RowVersion);
        }

        public InventoryResult GuardMaps(InvDocument doc, InvItem item, long absValue, bool inbound)
        {
            if (absValue == 0) return InventoryResult.Success(0, 0);
            if (_store.ResolveAccount(doc.CompanyId, item.ItemId, item.CategoryId, InventoryCodes.RoleInventory) <= 0)
                return InventoryResult.Fail("MAPPING_MISSING", "Inventory account map is required.");

            string offsetRole = OffsetRole(doc, inbound);
            if (string.IsNullOrEmpty(offsetRole)) return InventoryResult.Success(0, 0);
            if (_store.ResolveAccount(doc.CompanyId, item.ItemId, item.CategoryId, offsetRole) <= 0)
                return InventoryResult.Fail("MAPPING_MISSING", offsetRole + " account map is required.");
            return InventoryResult.Success(0, 0);
        }

        public static string OffsetRole(InvDocument doc, bool inbound)
        {
            if (doc != null && (doc.DocumentType == InventoryCodes.TypeReceipt || doc.DocumentType == InventoryCodes.TypeOpening)
                && doc.SourceModule == CaseManagement.Accounting.Ledger.Domain.LedgerCodes.SourcePurchase)
                return InventoryCodes.RoleGrir;
            return OffsetRole(doc == null ? null : doc.DocumentType, inbound);
        }

        public static string OffsetRole(string documentType, bool inbound)
        {
            if (documentType == InventoryCodes.TypeIssue) return InventoryCodes.RoleCogs;
            if (documentType == InventoryCodes.TypeReceipt || documentType == InventoryCodes.TypeOpening)
                return InventoryCodes.RoleOpeningOffset;
            if (documentType == InventoryCodes.TypeAdjustment)
                return inbound ? InventoryCodes.RoleAdjGain : InventoryCodes.RoleAdjLoss;
            if (documentType == InventoryCodes.TypeCount)
                return inbound ? InventoryCodes.RoleAdjGain : InventoryCodes.RoleAdjLoss;
            if (documentType == InventoryCodes.TypeRevalue)
                return inbound ? InventoryCodes.RoleAdjGain : InventoryCodes.RoleAdjLoss;
            return null;
        }

        public static bool IsOutbound(string documentType, long qtyBase)
        {
            if (documentType == InventoryCodes.TypeIssue) return true;
            if (documentType == InventoryCodes.TypeTransfer) return true;
            if (documentType == InventoryCodes.TypeAdjustment || documentType == InventoryCodes.TypeRevalue
                || documentType == InventoryCodes.TypeCount)
                return qtyBase < 0;
            return false;
        }
    }
}

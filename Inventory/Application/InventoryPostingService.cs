using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;
using CaseManagement.Inventory.Domain;
using CaseManagement.Inventory.Infrastructure;
using CaseManagement.Trade;

namespace CaseManagement.Inventory.Application
{
    public class InventoryPostingService
    {
        private readonly InventoryStore _store;
        private readonly InventoryValidationService _validation;
        private readonly LedgerRepository _audit;

        public InventoryPostingService()
            : this(new InventoryStore(), new InventoryValidationService(), new LedgerRepository()) { }

        public InventoryPostingService(InventoryStore store, InventoryValidationService validation, LedgerRepository audit)
        {
            _store = store;
            _validation = validation;
            _audit = audit;
        }

        public InventoryResult CreateItem(InvItem item, ILedgerIdentity identity)
        {
            InventoryResult perm = _validation.GuardIdentity(identity, InventoryPermissions.ManageItem);
            if (!perm.Ok && !_validation.GuardIdentity(identity, InventoryPermissions.Create).Ok)
                return perm;
            if (item == null || string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name))
                return InventoryResult.Fail("VALIDATION", "Item code and name are required.");
            int companyId = TradeIsolation.ResolveCompany(identity, item.CompanyId);
            if (companyId <= 0) companyId = LedgerCodes.DefaultCompanyId;
            item.CompanyId = companyId;
            item.Code = item.Code.Trim();
            item.Name = item.Name.Trim();
            if (!string.IsNullOrWhiteSpace(item.Barcode)) item.Barcode = item.Barcode.Trim();
            if (_store.GetItemByCode(companyId, item.Code) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateCode, "Duplicate item code.");
            if (_store.GetItemByName(companyId, item.Name, 0) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateName, "Duplicate item name.");
            if (!string.IsNullOrWhiteSpace(item.Barcode) && _store.GetItemByBarcode(companyId, item.Barcode) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateBarcode, "Duplicate barcode.");
            if (item.MaxQtyBase > 0 && item.MaxQtyBase < item.MinQtyBase)
                return InventoryResult.Fail("VALIDATION", "Max quantity must be greater than or equal to min, or zero for unlimited.");
            if (item.MinQtyBase < 0 || item.MaxQtyBase < 0)
                return InventoryResult.Fail("VALIDATION", "Min and max quantity cannot be negative.");
            if (item.CategoryId <= 0) item.CategoryId = _store.DefaultCategoryId(companyId);
            if (item.BaseUomId <= 0) item.BaseUomId = _store.DefaultUomId(companyId);
            if (string.IsNullOrEmpty(item.CostingMethod)) item.CostingMethod = InventoryCodes.CostingMovingAverage;
            item.IsStockable = true;
            item.IsActive = true;
            item.CreatedAt = LedgerTime.UtcNow(identity.UtcNow);
            item.CreatedBy = identity.UserName;
            long id = _store.InsertItem(item);
            if (id <= 0) return InventoryResult.Fail(InventoryCodes.DuplicateCode, "Could not create item (duplicate code?).");
            return InventoryResult.Success(id, 1);
        }

        public InventoryResult UpdateItem(InvItem item, ILedgerIdentity identity)
        {
            InventoryResult perm = _validation.GuardIdentity(identity, InventoryPermissions.ManageItem);
            if (!perm.Ok) return perm;
            if (item == null || item.ItemId <= 0) return InventoryResult.Fail("VALIDATION", "Item is required.");
            InvItem live = _store.GetItem(item.ItemId);
            if (live == null) return InventoryResult.Fail("NOT_FOUND", "Item not found.");
            if (!TradeIsolation.CanSeeCompany(identity, live.CompanyId))
                return InventoryResult.Fail("PERMISSION", "Cross-company inventory is not allowed.");
            if (string.IsNullOrWhiteSpace(item.Name))
                return InventoryResult.Fail("VALIDATION", "Item name is required.");
            item.Name = item.Name.Trim();
            if (!string.IsNullOrWhiteSpace(item.Barcode)) item.Barcode = item.Barcode.Trim();
            if (_store.GetItemByName(live.CompanyId, item.Name, live.ItemId) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateName, "Duplicate item name.");
            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                InvItem byBc = _store.GetItemByBarcode(live.CompanyId, item.Barcode);
                if (byBc != null && byBc.ItemId != live.ItemId)
                    return InventoryResult.Fail(InventoryCodes.DuplicateBarcode, "Duplicate barcode.");
            }
            live.Name = item.Name;
            live.Barcode = item.Barcode;
            live.Code = string.IsNullOrWhiteSpace(item.Code) ? live.Code : item.Code.Trim();
            live.MinQtyBase = item.MinQtyBase;
            live.MaxQtyBase = item.MaxQtyBase;
            if (item.CategoryId > 0) live.CategoryId = item.CategoryId;
            if (item.BaseUomId > 0) live.BaseUomId = item.BaseUomId;
            if (item.MinQtyBase < 0 || item.MaxQtyBase < 0)
                return InventoryResult.Fail("VALIDATION", "Min and max quantity cannot be negative.");
            if (item.MaxQtyBase > 0 && item.MaxQtyBase < item.MinQtyBase)
                return InventoryResult.Fail("VALIDATION", "Max quantity must be greater than or equal to min, or zero for unlimited.");
            if (_store.GetItemByCode(live.CompanyId, live.Code, live.ItemId) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateCode, "Duplicate item code.");
            if (_store.ItemHasUsage(live.ItemId) && item.BaseUomId > 0 && item.BaseUomId != _store.GetItem(live.ItemId).BaseUomId)
                return InventoryResult.Fail(InventoryCodes.InUse, "Unit of measure cannot change after stock movements.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.UpdateItem(live, now, identity.UserName))
                return InventoryResult.Fail("CONCURRENCY", "Item was changed by another user.");
            return InventoryResult.Success(live.ItemId, live.RowVersion + 1);
        }

        public InventoryResult SetItemActive(long itemId, bool active, ILedgerIdentity identity)
        {
            InventoryResult perm = _validation.GuardIdentity(identity, InventoryPermissions.ManageItem);
            if (!perm.Ok) return perm;
            InvItem live = _store.GetItem(itemId);
            if (live == null) return InventoryResult.Fail("NOT_FOUND", "Item not found.");
            if (!TradeIsolation.CanSeeCompany(identity, live.CompanyId))
                return InventoryResult.Fail("PERMISSION", "Cross-company inventory is not allowed.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.SetItemActive(itemId, live.CompanyId, active, now, identity.UserName))
                return InventoryResult.Fail("VALIDATION", "Could not update item.");
            return InventoryResult.Success(itemId, live.RowVersion + 1);
        }

        public InventoryResult DeleteItem(long itemId, ILedgerIdentity identity)
        {
            InventoryResult perm = _validation.GuardIdentity(identity, InventoryPermissions.ManageItem);
            if (!perm.Ok) return perm;
            InvItem live = _store.GetItem(itemId);
            if (live == null) return InventoryResult.Fail("NOT_FOUND", "Item not found.");
            if (!TradeIsolation.CanSeeCompany(identity, live.CompanyId))
                return InventoryResult.Fail("PERMISSION", "Cross-company inventory is not allowed.");
            if (_store.ItemHasUsage(itemId) || _store.ItemOnHand(live.CompanyId, itemId) != 0)
                return InventoryResult.Fail(InventoryCodes.InUse, "Item has documents or stock and cannot be deleted. Deactivate it instead.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.SoftDeleteItem(itemId, live.CompanyId, now, identity.UserName))
                return InventoryResult.Fail("VALIDATION", "Could not delete item.");
            return InventoryResult.Success(itemId, 0);
        }

        public InventoryResult CreateWarehouse(InvWarehouse warehouse, ILedgerIdentity identity)
        {
            InventoryResult perm = _validation.GuardIdentity(identity, InventoryPermissions.ManageWarehouse);
            if (!perm.Ok) return perm;
            if (warehouse == null || string.IsNullOrWhiteSpace(warehouse.Code) || string.IsNullOrWhiteSpace(warehouse.Name))
                return InventoryResult.Fail("VALIDATION", "Warehouse code and name are required.");
            int companyId = TradeIsolation.ResolveCompany(identity, warehouse.CompanyId);
            int centerId = warehouse.CenterId > 0 ? warehouse.CenterId : identity.CenterId;
            if (centerId <= 0) centerId = 1;
            if (!TradeIsolation.CanSeeCompany(identity, companyId) || !TradeIsolation.CanSeeCenter(identity, centerId))
                return InventoryResult.Fail("PERMISSION", "Cross-company or cross-warehouse access is not allowed.");
            warehouse.CompanyId = companyId;
            warehouse.CenterId = centerId;
            warehouse.Code = warehouse.Code.Trim();
            warehouse.Name = warehouse.Name.Trim();
            IList<InvWarehouse> existing = _store.ListWarehouses(companyId, 0);
            for (int i = 0; i < existing.Count; i++)
                if (string.Equals(existing[i].Code, warehouse.Code, StringComparison.OrdinalIgnoreCase))
                    return InventoryResult.Fail("VALIDATION", "Duplicate warehouse code.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long id = _store.InsertWarehouse(warehouse, now, identity.UserName);
            if (id <= 0) return InventoryResult.Fail("VALIDATION", "Could not create warehouse.");
            return InventoryResult.Success(id, 1);
        }

        public InventoryResult SetWarehouseActive(long warehouseId, bool active, ILedgerIdentity identity)
        {
            InventoryResult perm = _validation.GuardIdentity(identity, InventoryPermissions.ManageWarehouse);
            if (!perm.Ok) return perm;
            InvWarehouse w = _store.GetWarehouse(warehouseId);
            if (w == null) return InventoryResult.Fail("NOT_FOUND", "Warehouse not found.");
            if (!TradeIsolation.CanSeeCompany(identity, w.CompanyId) || !TradeIsolation.CanSeeCenter(identity, w.CenterId))
                return InventoryResult.Fail("PERMISSION", "Cross-warehouse access is not allowed.");
            if (!active && _store.WarehouseOnHand(w.CompanyId, warehouseId) != 0)
                return InventoryResult.Fail("VALIDATION", "Cannot deactivate a warehouse with stock.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.SetWarehouseActive(warehouseId, w.CompanyId, active, now, identity.UserName))
                return InventoryResult.Fail("VALIDATION", "Could not update warehouse.");
            return InventoryResult.Success(warehouseId, w.RowVersion + 1);
        }

        public InventoryResult CreateAndPost(InventoryPostCommand command, ILedgerIdentity identity)
        {
            InventoryResult created = CreateDraft(command, identity);
            if (!created.Ok) return created;
            command.DocumentId = created.EntityId;
            command.ExpectedRowVersion = created.RowVersion;
            return Post(command.DocumentId, identity);
        }

        public InventoryResult CreateDraft(InventoryPostCommand command, ILedgerIdentity identity)
        {
            if (command == null) return InventoryResult.Fail("VALIDATION", "Command is required.");
            string permKey = PermissionForType(command.DocumentType, true);
            InventoryResult perm = _validation.GuardIdentity(identity, permKey);
            if (!perm.Ok) return perm;

            int companyId = TradeIsolation.ResolveCompany(identity, command.CompanyId);
            if (companyId <= 0) companyId = LedgerCodes.DefaultCompanyId;
            command.CompanyId = companyId;
            InvWarehouse warehouse = _store.GetWarehouse(command.WarehouseId);
            if (warehouse == null)
            {
                long defWh = _store.DefaultWarehouseId(companyId);
                warehouse = _store.GetWarehouse(defWh);
            }
            if (warehouse == null) return InventoryResult.Fail("VALIDATION", "Warehouse is required.");

            InvDocument doc = new InvDocument();
            doc.CompanyId = companyId;
            doc.CenterId = command.CenterId > 0 ? command.CenterId : warehouse.CenterId;
            doc.DocumentType = command.DocumentType;
            doc.Status = InventoryCodes.StatusDraft;
            doc.PostingDate = LedgerTime.DateOnly(command.PostingDate);
            if (string.IsNullOrEmpty(doc.PostingDate))
                doc.PostingDate = identity.UtcNow.ToString("yyyy-MM-dd");
            doc.WarehouseId = warehouse.WarehouseId;
            if (command.ToWarehouseId > 0) doc.ToWarehouseId = command.ToWarehouseId;
            if (command.ToLocationId > 0) doc.ToLocationId = command.ToLocationId;
            if (doc.DocumentType == InventoryCodes.TypeTransfer && !doc.ToLocationId.HasValue && command.ToWarehouseId > 0)
                doc.ToLocationId = _store.DefaultLocationId(command.ToWarehouseId);
            doc.Description = command.Description;
            doc.SourceModule = command.SourceModule;
            doc.SourceDocumentType = command.SourceDocumentType;
            doc.SourceDocumentId = command.SourceDocumentId;
            doc.PartyId = command.PartyId;
            doc.CreatedAt = LedgerTime.UtcNow(identity.UtcNow);
            doc.CreatedBy = identity.UserName;
            doc.UpdatedAt = doc.CreatedAt;
            doc.UpdatedBy = identity.UserName;

            IList<InvDocumentLine> lines = NormalizeLines(command.Lines, warehouse);
            InventoryResult check = _validation.GuardDocument(doc, warehouse, lines, identity, true);
            if (!check.Ok) return check;

            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                doc.DocNo = _store.AllocateDocNo(con, tr, companyId, doc.DocumentType);
                newId = _store.InsertDocument(con, tr, doc);
                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i].DocumentId = newId;
                    lines[i].LineNo = i + 1;
                    _store.InsertLine(con, tr, lines[i], companyId);
                }
            });
            return InventoryResult.Success(newId, 1);
        }

        public InventoryResult UpdateDraft(InventoryPostCommand command, ILedgerIdentity identity)
        {
            if (command == null || command.DocumentId <= 0)
                return InventoryResult.Fail("VALIDATION", "Document is required.");
            InvDocument doc = _store.GetDocument(command.DocumentId);
            if (doc == null) return InventoryResult.Fail("NOT_FOUND", "Document not found.");
            if (doc.Status != InventoryCodes.StatusDraft)
                return InventoryResult.Fail("INVALID_STATUS", "Only Draft documents can be edited.");
            InventoryResult perm = _validation.GuardIdentity(identity, PermissionForType(doc.DocumentType, true));
            if (!perm.Ok) return perm;
            if (!TradeIsolation.CanSeeCompany(identity, doc.CompanyId) || !TradeIsolation.CanSeeCenter(identity, doc.CenterId))
                return InventoryResult.Fail("PERMISSION", "Cross-company or cross-branch inventory is not allowed.");

            InvWarehouse warehouse = _store.GetWarehouse(command.WarehouseId > 0 ? command.WarehouseId : doc.WarehouseId);
            if (warehouse == null) warehouse = _store.GetWarehouse(doc.WarehouseId);
            if (warehouse == null) return InventoryResult.Fail("VALIDATION", "Warehouse is required.");
            doc.WarehouseId = warehouse.WarehouseId;
            doc.CenterId = command.CenterId > 0 ? command.CenterId : warehouse.CenterId;
            if (!string.IsNullOrWhiteSpace(command.PostingDate))
                doc.PostingDate = LedgerTime.DateOnly(command.PostingDate);
            if (command.Description != null) doc.Description = command.Description;
            if (command.ToWarehouseId > 0) doc.ToWarehouseId = command.ToWarehouseId;
            if (command.ToLocationId > 0) doc.ToLocationId = command.ToLocationId;
            else if (command.ToWarehouseId > 0)
                doc.ToLocationId = _store.DefaultLocationId(command.ToWarehouseId);
            doc.UpdatedAt = LedgerTime.UtcNow(identity.UtcNow);
            doc.UpdatedBy = identity.UserName;

            IList<InvDocumentLine> lines = NormalizeLines(command.Lines, warehouse);
            InventoryResult check = _validation.GuardDocument(doc, warehouse, lines, identity, true);
            if (!check.Ok) return check;

            bool ok = false;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                if (!_store.UpdateDraftHeader(con, tr, doc, doc.RowVersion))
                    return;
                _store.DeleteLines(con, tr, doc.DocumentId);
                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i].DocumentId = doc.DocumentId;
                    lines[i].LineNo = i + 1;
                    _store.InsertLine(con, tr, lines[i], doc.CompanyId);
                }
                ok = true;
            });
            if (!ok) return InventoryResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            InvDocument saved = _store.GetDocument(doc.DocumentId);
            return InventoryResult.Success(doc.DocumentId, saved == null ? doc.RowVersion + 1 : saved.RowVersion);
        }

        public InventoryResult Submit(long documentId, ILedgerIdentity identity)
        {
            InventoryResult perm = _validation.GuardIdentity(identity, InventoryPermissions.Approve);
            if (!perm.Ok) return perm;
            InvDocument doc = _store.GetDocument(documentId);
            if (doc == null) return InventoryResult.Fail("NOT_FOUND", "Document not found.");
            if (doc.Status != InventoryCodes.StatusDraft)
                return InventoryResult.Fail("INVALID_STATUS", "Only Draft documents can be submitted.");
            if (!TradeIsolation.CanSeeCompany(identity, doc.CompanyId) || !TradeIsolation.CanSeeCenter(identity, doc.CenterId))
                return InventoryResult.Fail("PERMISSION", "Cross-company or cross-branch inventory is not allowed.");
            InventoryResult period = _validation.GuardPeriod(doc.CompanyId, doc.PostingDate);
            if (!period.Ok) return period;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            bool ok = false;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                InvDocument live = _store.GetDocument(con, tr, documentId);
                if (live == null || live.Status != InventoryCodes.StatusDraft) return;
                live.Status = InventoryCodes.StatusSubmitted;
                live.UpdatedAt = now;
                live.UpdatedBy = identity.UserName;
                ok = _store.UpdateDocumentStatus(con, tr, live, live.RowVersion);
            });
            if (!ok) return InventoryResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            return InventoryResult.Success(documentId, 0);
        }

        public InventoryResult Post(long documentId, ILedgerIdentity identity)
        {
            InvDocument doc = _store.GetDocument(documentId);
            if (doc == null) return InventoryResult.Fail("NOT_FOUND", "Document not found.");
            string permKey = PermissionForType(doc.DocumentType, false);
            InventoryResult perm = _validation.GuardIdentity(identity, permKey);
            if (!perm.Ok) return perm;
            if (_store.RequiresApproval(doc.CompanyId) && doc.Status != InventoryCodes.StatusSubmitted)
            {
                InventoryResult ap = _validation.GuardIdentity(identity, InventoryPermissions.Approve);
                if (!ap.Ok && doc.Status != InventoryCodes.StatusSubmitted)
                    return InventoryResult.Fail("INVALID_STATUS", "Document must be submitted for approval.");
            }
            if (doc.Status != InventoryCodes.StatusDraft && doc.Status != InventoryCodes.StatusSubmitted)
                return InventoryResult.Fail("INVALID_STATUS", "Only Draft or Submitted documents can be posted.");

            InventoryResult period = _validation.GuardPeriod(doc.CompanyId, doc.PostingDate);
            if (!period.Ok) return period;

            InvWarehouse warehouse = _store.GetWarehouse(doc.WarehouseId);
            IList<InvDocumentLine> lines = _store.ListLines(documentId);
            bool allowNeg = _store.AllowNegativeStock(doc.CompanyId);
            InventoryResult guard = _validation.GuardDocument(doc, warehouse, lines, identity, allowNeg);
            if (!guard.Ok) return guard;

            string now = LedgerTime.UtcNow(identity.UtcNow);
            string fail = null;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                InvDocument live = _store.GetDocument(con, tr, documentId);
                if (live == null || (live.Status != InventoryCodes.StatusDraft && live.Status != InventoryCodes.StatusSubmitted))
                {
                    fail = "INVALID_STATUS";
                    return;
                }
                if (_store.HasLedgerForDocument(con, tr, documentId))
                {
                    fail = InventoryCodes.DuplicateMovement;
                    return;
                }
                IList<InvDocumentLine> liveLines = _store.ListLines(con, tr, documentId);
                for (int i = 0; i < liveLines.Count; i++)
                {
                    InvDocumentLine line = liveLines[i];
                    InvItem item = _store.GetItem(line.ItemId);
                    if (live.DocumentType == InventoryCodes.TypeTransfer)
                    {
                        fail = PostTransferLine(con, tr, live, line, item, identity, allowNeg, now);
                        if (fail != null) return;
                        continue;
                    }

                    long signedQty = SignedQty(live.DocumentType, line.QtyBase);
                    if (signedQty == 0) continue;
                    long absQty = signedQty < 0 ? -signedQty : signedQty;
                    InventoryStore.WarehouseTotals tot = _store.GetWarehouseTotals(con, tr, live.CompanyId, item.ItemId, live.WarehouseId);
                    bool inbound = signedQty > 0;
                    long unit;
                    long value;
                    if (inbound)
                    {
                        unit = line.UnitCostMinor;
                        if ((live.DocumentType == InventoryCodes.TypeAdjustment || live.DocumentType == InventoryCodes.TypeCount) && unit == 0)
                            unit = tot.Avg;
                        value = unit * absQty;
                    }
                    else
                    {
                        unit = tot.Avg;
                        if (tot.Qty == absQty)
                            value = tot.Value;
                        else
                            value = unit * absQty;
                    }

                    InventoryResult maps = _validation.GuardMaps(live, item, value, inbound);
                    if (!maps.Ok)
                    {
                        fail = maps.ErrorCode + ":" + maps.Message;
                        return;
                    }

                    if (!allowNeg && tot.Qty + signedQty < 0)
                    {
                        fail = InventoryCodes.NegativeStock;
                        return;
                    }

                    _store.UpdateLineValue(con, tr, documentId, line.LineNo, unit, inbound ? value : -value);

                    InvItemLedger led = new InvItemLedger();
                    led.CompanyId = live.CompanyId;
                    led.CenterId = live.CenterId;
                    led.ItemId = item.ItemId;
                    led.WarehouseId = live.WarehouseId;
                    led.LocationId = line.LocationId;
                    led.DocumentType = live.DocumentType;
                    led.DocumentId = documentId;
                    led.DocumentLineNo = line.LineNo;
                    led.MovementType = inbound ? InventoryCodes.MoveIn : InventoryCodes.MoveOut;
                    led.QtyBase = signedQty;
                    led.UnitCostMinor = unit;
                    led.ValueMinor = inbound ? value : -value;
                    led.PostingDate = live.PostingDate;
                    led.CreatedBy = identity.UserName;
                    _store.InsertLedger(con, tr, led);

                    long newQty = tot.Qty + signedQty;
                    long newVal = inbound ? tot.Value + value : tot.Value - value;
                    if (newQty == 0) newVal = 0;
                    long newAvg = newQty == 0 ? 0 : newVal / newQty;
                    _store.ApplyQtyDelta(con, tr, live.CompanyId, item.ItemId, live.WarehouseId, line.LocationId, signedQty, now, identity.UserName);
                    _store.AllocateWarehouseValue(con, tr, live.CompanyId, item.ItemId, live.WarehouseId, newVal, newAvg, now, identity.UserName);
                }

                live.Status = InventoryCodes.StatusPosted;
                live.UpdatedAt = now;
                live.UpdatedBy = identity.UserName;
                if (!_store.UpdateDocumentStatus(con, tr, live, live.RowVersion))
                {
                    fail = "CONCURRENCY";
                    return;
                }

                AccOutboxWriter.Enqueue(con, tr, LedgerCodes.SourceInventory, InventoryCodes.DocInvDocument,
                    documentId, LedgerCodes.OutboxPost, live.CompanyId, live.CenterId, identity.UserName);

                if (live.DocumentType == InventoryCodes.TypeAdjustment || live.DocumentType == InventoryCodes.TypeCount)
                {
                    _audit.InsertAudit(con, tr, live.DocumentType == InventoryCodes.TypeCount ? "InventoryCount" : "InventoryAdjustment",
                        "InvDocument", documentId,
                        null, live.DocNo, live.DocumentType + " posted", identity.UserName, live.CenterId);
                }
            });

            if (fail != null)
            {
                if (fail == InventoryCodes.NegativeStock)
                    return InventoryResult.Fail(InventoryCodes.NegativeStock, "Insufficient quantity on hand.");
                if (fail == InventoryCodes.DuplicateMovement)
                    return InventoryResult.Fail(InventoryCodes.DuplicateMovement, "Document already has ledger movements.");
                if (fail == "CONCURRENCY")
                    return InventoryResult.Fail("CONCURRENCY", "RowVersion mismatch.");
                if (fail.StartsWith("MAPPING_MISSING", StringComparison.Ordinal))
                    return InventoryResult.Fail("MAPPING_MISSING", fail);
                return InventoryResult.Fail(fail, fail);
            }

            InvDocument posted = _store.GetDocument(documentId);
            return InventoryResult.Success(documentId, posted == null ? 1 : posted.RowVersion);
        }

        public InventoryResult Reverse(long documentId, ILedgerIdentity identity)
        {
            InventoryResult perm = _validation.GuardIdentity(identity, InventoryPermissions.Reverse);
            if (!perm.Ok) return perm;
            InvDocument doc = _store.GetDocument(documentId);
            if (doc == null || doc.Status != InventoryCodes.StatusPosted)
                return InventoryResult.Fail("INVALID_STATUS", "Only Posted documents can be reversed.");
            InventoryResult period = _validation.GuardPeriod(doc.CompanyId, doc.PostingDate);
            if (!period.Ok) return period;

            string now = LedgerTime.UtcNow(identity.UtcNow);
            string fail = null;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                InvDocument live = _store.GetDocument(con, tr, documentId);
                if (live == null || live.Status != InventoryCodes.StatusPosted)
                {
                    fail = "INVALID_STATUS";
                    return;
                }
                IList<InvItemLedger> rows = _store.ListOriginalLedgerForDocument(con, tr, documentId);
                for (int i = 0; i < rows.Count; i++)
                {
                    InvItemLedger src = rows[i];
                    long signedQty = -src.QtyBase;
                    long signedVal = -src.ValueMinor;
                    InventoryStore.WarehouseTotals tot = _store.GetWarehouseTotals(con, tr, live.CompanyId, src.ItemId, src.WarehouseId);
                    InvItemLedger led = new InvItemLedger();
                    led.CompanyId = src.CompanyId;
                    led.CenterId = src.CenterId;
                    led.ItemId = src.ItemId;
                    led.WarehouseId = src.WarehouseId;
                    led.LocationId = src.LocationId;
                    led.DocumentType = src.DocumentType;
                    led.DocumentId = documentId;
                    led.DocumentLineNo = src.DocumentLineNo;
                    led.MovementType = signedQty > 0 ? InventoryCodes.MoveIn : InventoryCodes.MoveOut;
                    led.QtyBase = signedQty;
                    led.UnitCostMinor = src.UnitCostMinor;
                    led.ValueMinor = signedVal;
                    led.PostingDate = live.PostingDate;
                    led.CreatedBy = identity.UserName;
                    led.ReversesLedgerId = src.ItemLedgerId;
                    _store.InsertLedger(con, tr, led);

                    long newQty = tot.Qty + signedQty;
                    long newVal = tot.Value + signedVal;
                    if (newQty == 0) newVal = 0;
                    long newAvg = newQty == 0 ? 0 : newVal / newQty;
                    _store.ApplyQtyDelta(con, tr, live.CompanyId, src.ItemId, src.WarehouseId, src.LocationId, signedQty, now, identity.UserName);
                    _store.AllocateWarehouseValue(con, tr, live.CompanyId, src.ItemId, src.WarehouseId, newVal, newAvg, now, identity.UserName);
                }

                live.Status = InventoryCodes.StatusReversed;
                live.UpdatedAt = now;
                live.UpdatedBy = identity.UserName;
                if (!_store.UpdateDocumentStatus(con, tr, live, live.RowVersion))
                {
                    fail = "CONCURRENCY";
                    return;
                }
                AccOutboxWriter.Enqueue(con, tr, LedgerCodes.SourceInventory, InventoryCodes.DocInvDocument,
                    documentId, LedgerCodes.OutboxReverse, live.CompanyId, live.CenterId, identity.UserName);
            });
            if (fail != null) return InventoryResult.Fail(fail, fail);
            return InventoryResult.Success(documentId, 0);
        }

        private static long SignedQty(string type, long qtyBase)
        {
            long abs = qtyBase < 0 ? -qtyBase : qtyBase;
            if (type == InventoryCodes.TypeIssue) return -abs;
            if (type == InventoryCodes.TypeTransfer) return 0;
            if (type == InventoryCodes.TypeAdjustment || type == InventoryCodes.TypeRevalue || type == InventoryCodes.TypeCount)
                return qtyBase;
            return abs;
        }

        private static string PermissionForType(string documentType, bool creating)
        {
            if (documentType == InventoryCodes.TypeAdjustment || documentType == InventoryCodes.TypeCount
                || documentType == InventoryCodes.TypeRevalue)
                return InventoryPermissions.Adjust;
            return creating ? InventoryPermissions.Create : InventoryPermissions.Post;
        }

        private string PostTransferLine(SQLiteConnection con, SQLiteTransaction tr, InvDocument live, InvDocumentLine line,
            InvItem item, ILedgerIdentity identity, bool allowNeg, string now)
        {
            long absQty = line.QtyBase < 0 ? -line.QtyBase : line.QtyBase;
            if (absQty == 0) return "VALIDATION";
            long destWhId = live.ToWarehouseId.HasValue ? live.ToWarehouseId.Value : 0;
            long destLocId = live.ToLocationId.HasValue ? live.ToLocationId.Value : 0;
            if (destLocId <= 0 && destWhId > 0) destLocId = _store.DefaultLocationId(destWhId);
            InvLocation destLoc = _store.GetLocation(destLocId);
            if (destLoc == null) return "VALIDATION";
            destWhId = destLoc.WarehouseId;
            if (destWhId == live.WarehouseId && destLocId == line.LocationId)
                return "VALIDATION";

            InventoryStore.WarehouseTotals src = _store.GetWarehouseTotals(con, tr, live.CompanyId, item.ItemId, live.WarehouseId);
            InventoryStore.WarehouseTotals dst = _store.GetWarehouseTotals(con, tr, live.CompanyId, item.ItemId, destWhId);
            if (!allowNeg && src.Qty < absQty)
                return InventoryCodes.NegativeStock;
            InvItemBalance locBal = _store.GetBalance(live.CompanyId, item.ItemId, live.WarehouseId, line.LocationId);
            long locQty = locBal == null ? 0 : locBal.QuantityOnHand;
            if (!allowNeg && locQty < absQty)
                return InventoryCodes.NegativeStock;

            long unit = src.Avg;
            long value = src.Qty == absQty ? src.Value : unit * absQty;
            _store.UpdateLineValue(con, tr, live.DocumentId, line.LineNo, unit, 0);

            InsertMove(con, tr, live, line, item, identity, live.WarehouseId, line.LocationId,
                InventoryCodes.MoveOut, -absQty, unit, -value);
            InsertMove(con, tr, live, line, item, identity, destWhId, destLocId,
                InventoryCodes.MoveIn, absQty, unit, value);

            _store.ApplyQtyDelta(con, tr, live.CompanyId, item.ItemId, live.WarehouseId, line.LocationId, -absQty, now, identity.UserName);
            _store.ApplyQtyDelta(con, tr, live.CompanyId, item.ItemId, destWhId, destLocId, absQty, now, identity.UserName);

            if (destWhId == live.WarehouseId)
            {
                _store.AllocateWarehouseValue(con, tr, live.CompanyId, item.ItemId, live.WarehouseId,
                    src.Value, src.Avg, now, identity.UserName);
            }
            else
            {
                long srcQty = src.Qty - absQty;
                long srcVal = src.Value - value;
                if (srcQty == 0) srcVal = 0;
                long srcAvg = srcQty == 0 ? 0 : srcVal / srcQty;
                long dstQty = dst.Qty + absQty;
                long dstVal = dst.Value + value;
                long dstAvg = dstQty == 0 ? 0 : dstVal / dstQty;
                _store.AllocateWarehouseValue(con, tr, live.CompanyId, item.ItemId, live.WarehouseId, srcVal, srcAvg, now, identity.UserName);
                _store.AllocateWarehouseValue(con, tr, live.CompanyId, item.ItemId, destWhId, dstVal, dstAvg, now, identity.UserName);
            }
            return null;
        }

        private void InsertMove(SQLiteConnection con, SQLiteTransaction tr, InvDocument live, InvDocumentLine line,
            InvItem item, ILedgerIdentity identity, long warehouseId, long locationId,
            string movement, long signedQty, long unit, long signedValue)
        {
            InvItemLedger led = new InvItemLedger();
            led.CompanyId = live.CompanyId;
            led.CenterId = live.CenterId;
            led.ItemId = item.ItemId;
            led.WarehouseId = warehouseId;
            led.LocationId = locationId;
            led.DocumentType = live.DocumentType;
            led.DocumentId = live.DocumentId;
            led.DocumentLineNo = line.LineNo;
            led.MovementType = movement;
            led.QtyBase = signedQty;
            led.UnitCostMinor = unit;
            led.ValueMinor = signedValue;
            led.PostingDate = live.PostingDate;
            led.CreatedBy = identity.UserName;
            _store.InsertLedger(con, tr, led);
        }

        private IList<InvDocumentLine> NormalizeLines(IList<InvDocumentLine> raw, InvWarehouse warehouse)
        {
            List<InvDocumentLine> lines = new List<InvDocumentLine>();
            if (raw == null) return lines;
            long defLoc = _store.DefaultLocationId(warehouse.WarehouseId);
            for (int i = 0; i < raw.Count; i++)
            {
                InvDocumentLine src = raw[i];
                InvDocumentLine line = new InvDocumentLine();
                line.ItemId = src.ItemId;
                line.LocationId = src.LocationId > 0 ? src.LocationId : defLoc;
                line.QtyDoc = src.QtyDoc != 0 ? src.QtyDoc : src.QtyBase;
                line.QtyBase = src.QtyBase != 0 ? src.QtyBase : src.QtyDoc;
                line.UnitCostMinor = src.UnitCostMinor;
                line.ValueMinor = src.ValueMinor;
                lines.Add(line);
            }
            return lines;
        }
    }
}

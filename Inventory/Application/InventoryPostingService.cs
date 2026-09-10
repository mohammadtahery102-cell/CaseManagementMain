using System;
using System.Collections.Generic;
using System.Data.SQLite;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Accounting.Ledger.Infrastructure;
using CaseManagement.Inventory.Domain;
using CaseManagement.Inventory.Infrastructure;

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
            int companyId = item.CompanyId > 0 ? item.CompanyId : identity.CompanyId;
            if (companyId <= 0) companyId = LedgerCodes.DefaultCompanyId;
            item.CompanyId = companyId;
            if (item.CategoryId <= 0) item.CategoryId = _store.DefaultCategoryId(companyId);
            if (item.BaseUomId <= 0) item.BaseUomId = _store.DefaultUomId(companyId);
            if (string.IsNullOrEmpty(item.CostingMethod)) item.CostingMethod = InventoryCodes.CostingMovingAverage;
            item.IsStockable = true;
            item.CreatedAt = LedgerTime.UtcNow(identity.UtcNow);
            item.CreatedBy = identity.UserName;
            long id = _store.InsertItem(item);
            if (id <= 0) return InventoryResult.Fail("VALIDATION", "Could not create item (duplicate code?).");
            return InventoryResult.Success(id, 1);
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
            string permKey = command.DocumentType == InventoryCodes.TypeAdjustment
                ? InventoryPermissions.Adjust : InventoryPermissions.Create;
            InventoryResult perm = _validation.GuardIdentity(identity, permKey);
            if (!perm.Ok) return perm;

            int companyId = command.CompanyId > 0 ? command.CompanyId : identity.CompanyId;
            if (companyId <= 0) companyId = LedgerCodes.DefaultCompanyId;
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

        public InventoryResult Post(long documentId, ILedgerIdentity identity)
        {
            InvDocument doc = _store.GetDocument(documentId);
            if (doc == null) return InventoryResult.Fail("NOT_FOUND", "Document not found.");
            string permKey = doc.DocumentType == InventoryCodes.TypeAdjustment
                ? InventoryPermissions.Adjust : InventoryPermissions.Post;
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
                IList<InvDocumentLine> liveLines = _store.ListLines(con, tr, documentId);
                for (int i = 0; i < liveLines.Count; i++)
                {
                    InvDocumentLine line = liveLines[i];
                    InvItem item = _store.GetItem(line.ItemId);
                    long signedQty = SignedQty(live.DocumentType, line.QtyBase);
                    long absQty = signedQty < 0 ? -signedQty : signedQty;
                    InventoryStore.WarehouseTotals tot = _store.GetWarehouseTotals(con, tr, live.CompanyId, item.ItemId, live.WarehouseId);
                    bool inbound = signedQty > 0;
                    long unit;
                    long value;
                    if (inbound)
                    {
                        unit = line.UnitCostMinor;
                        if (live.DocumentType == InventoryCodes.TypeAdjustment && unit == 0)
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

                if (live.DocumentType == InventoryCodes.TypeAdjustment)
                {
                    _audit.InsertAudit(con, tr, "InventoryAdjustment", "InvDocument", documentId,
                        null, live.DocNo, "Adjustment posted", identity.UserName, live.CenterId);
                }
            });

            if (fail != null)
            {
                if (fail == InventoryCodes.NegativeStock)
                    return InventoryResult.Fail(InventoryCodes.NegativeStock, "Insufficient quantity on hand.");
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
                IList<InvItemLedger> rows = _store.ListLedgerForDocument(documentId);
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
            if (type == InventoryCodes.TypeAdjustment || type == InventoryCodes.TypeRevalue)
                return qtyBase;
            return abs;
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

using System;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Inventory.Domain;
using CaseManagement.Inventory.Infrastructure;
using CaseManagement.Trade;

namespace CaseManagement.Inventory.Application
{
    public class InventoryItemService
    {
        private readonly InventoryStore _store;
        private readonly InventoryValidationService _validation;

        public InventoryItemService()
            : this(new InventoryStore(), new InventoryValidationService()) { }

        public InventoryItemService(InventoryStore store, InventoryValidationService validation)
        {
            _store = store;
            _validation = validation;
        }

        public InventoryResult SaveItem(InvItem item, ILedgerIdentity identity)
        {
            if (item != null && item.ItemId > 0)
                return UpdateItem(item, identity);
            return CreateItem(item, identity);
        }

        public InventoryResult CreateItem(InvItem item, ILedgerIdentity identity)
        {
            InventoryResult perm = RequireManageOrCreate(identity);
            if (!perm.Ok) return perm;
            InventoryResult prepared = PrepareItem(item, identity, 0);
            if (!prepared.Ok) return prepared;
            item.IsActive = true;
            item.IsStockable = true;
            item.CreatedAt = LedgerTime.UtcNow(identity.UtcNow);
            item.CreatedBy = identity.UserName;
            long id = _store.InsertItem(item);
            if (id <= 0)
                return InventoryResult.Fail(InventoryCodes.DuplicateCode, "An item with this code already exists.");
            return InventoryResult.Success(id, 1);
        }

        public InventoryResult UpdateItem(InvItem item, ILedgerIdentity identity)
        {
            InventoryResult perm = RequireManage(identity);
            if (!perm.Ok) return perm;
            if (item == null || item.ItemId <= 0)
                return InventoryResult.Fail("NOT_FOUND", "Item is required.");
            InvItem existing = _store.GetItem(item.ItemId);
            if (existing == null) return InventoryResult.Fail("NOT_FOUND", "Item not found.");
            int companyId = TradeIsolation.ResolveCompany(identity, existing.CompanyId);
            if (existing.CompanyId != companyId)
                return InventoryResult.Fail("PERMISSION", "Cross-company item update is not allowed.");
            InventoryResult prepared = PrepareItem(item, identity, existing.ItemId);
            if (!prepared.Ok) return prepared;
            if (_store.ItemHasUsage(existing.ItemId) && existing.BaseUomId != item.BaseUomId)
                return InventoryResult.Fail(InventoryCodes.InUse, "Unit of measure cannot change after stock movements.");
            item.CompanyId = existing.CompanyId;
            item.RowVersion = existing.RowVersion;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.UpdateItem(item, now, identity.UserName))
                return InventoryResult.Fail("CONCURRENCY", "Item was changed by another user.");
            InvItem saved = _store.GetItem(existing.ItemId);
            return InventoryResult.Success(existing.ItemId, saved == null ? existing.RowVersion + 1 : saved.RowVersion);
        }

        public InventoryResult SetItemActive(long itemId, bool active, ILedgerIdentity identity)
        {
            InventoryResult perm = RequireManage(identity);
            if (!perm.Ok) return perm;
            InvItem existing = _store.GetItem(itemId);
            if (existing == null) return InventoryResult.Fail("NOT_FOUND", "Item not found.");
            int companyId = TradeIsolation.ResolveCompany(identity, existing.CompanyId);
            if (existing.CompanyId != companyId)
                return InventoryResult.Fail("PERMISSION", "Cross-company item update is not allowed.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.SetItemActive(itemId, existing.CompanyId, active, now, identity.UserName))
                return InventoryResult.Fail("CONCURRENCY", "Item was changed by another user.");
            return InventoryResult.Success(itemId, existing.RowVersion + 1);
        }

        public InventoryResult DeleteItem(long itemId, ILedgerIdentity identity)
        {
            InventoryResult perm = RequireManage(identity);
            if (!perm.Ok) return perm;
            InvItem existing = _store.GetItem(itemId);
            if (existing == null) return InventoryResult.Fail("NOT_FOUND", "Item not found.");
            int companyId = TradeIsolation.ResolveCompany(identity, existing.CompanyId);
            if (existing.CompanyId != companyId)
                return InventoryResult.Fail("PERMISSION", "Cross-company item delete is not allowed.");
            if (_store.ItemHasUsage(itemId) || _store.ItemOnHand(existing.CompanyId, itemId) != 0)
                return InventoryResult.Fail(InventoryCodes.InUse, "Item has documents or stock and cannot be deleted. Deactivate it instead.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.SoftDeleteItem(itemId, existing.CompanyId, now, identity.UserName))
                return InventoryResult.Fail("CONCURRENCY", "Item was changed by another user.");
            return InventoryResult.Success(itemId, existing.RowVersion + 1);
        }

        public InventoryResult SaveCategory(InvItemCategory category, ILedgerIdentity identity)
        {
            InventoryResult perm = RequireManage(identity);
            if (!perm.Ok) return perm;
            if (category == null || string.IsNullOrWhiteSpace(category.Code) || string.IsNullOrWhiteSpace(category.Name))
                return InventoryResult.Fail("VALIDATION", "Category code and name are required.");
            int companyId = TradeIsolation.ResolveCompany(identity, category.CompanyId);
            if (companyId <= 0) companyId = LedgerCodes.DefaultCompanyId;
            category.CompanyId = companyId;
            category.Code = category.Code.Trim();
            category.Name = category.Name.Trim();
            if (category.Code.Length > 32 || category.Name.Length > 120)
                return InventoryResult.Fail("VALIDATION", "Category code or name is too long.");
            if (_store.GetCategoryByCode(companyId, category.Code, category.CategoryId) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateCode, "A category with this code already exists.");
            if (_store.GetCategoryByName(companyId, category.Name, category.CategoryId) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateName, "A category with this name already exists.");

            int level = 1;
            if (category.ParentCategoryId > 0)
            {
                InvItemCategory parent = _store.GetCategory(category.ParentCategoryId);
                if (parent == null || parent.CompanyId != companyId)
                    return InventoryResult.Fail("VALIDATION", "Parent category was not found.");
                if (category.CategoryId > 0 && WouldCycle(category.CategoryId, category.ParentCategoryId))
                    return InventoryResult.Fail("VALIDATION", "Category parent would create a cycle.");
                level = parent.Level + 1;
            }
            if (level > InventoryCodes.MaxCategoryLevel)
                return InventoryResult.Fail("VALIDATION", "Category hierarchy cannot exceed " + InventoryCodes.MaxCategoryLevel + " levels.");
            category.Level = level;
            category.IsActive = true;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (category.CategoryId <= 0)
            {
                long id = _store.InsertCategory(category, now, identity.UserName);
                if (id <= 0) return InventoryResult.Fail(InventoryCodes.DuplicateCode, "Could not create category.");
                return InventoryResult.Success(id, 1);
            }
            InvItemCategory existing = _store.GetCategory(category.CategoryId);
            if (existing == null) return InventoryResult.Fail("NOT_FOUND", "Category not found.");
            category.RowVersion = existing.RowVersion;
            if (!_store.UpdateCategory(category, now, identity.UserName))
                return InventoryResult.Fail("CONCURRENCY", "Category was changed by another user.");
            return InventoryResult.Success(category.CategoryId, existing.RowVersion + 1);
        }

        public InventoryResult DeleteCategory(long categoryId, ILedgerIdentity identity)
        {
            InventoryResult perm = RequireManage(identity);
            if (!perm.Ok) return perm;
            InvItemCategory existing = _store.GetCategory(categoryId);
            if (existing == null) return InventoryResult.Fail("NOT_FOUND", "Category not found.");
            int companyId = TradeIsolation.ResolveCompany(identity, existing.CompanyId);
            if (existing.CompanyId != companyId)
                return InventoryResult.Fail("PERMISSION", "Cross-company category delete is not allowed.");
            if (_store.CategoryHasChildren(categoryId) || _store.CategoryHasItems(categoryId))
                return InventoryResult.Fail(InventoryCodes.InUse, "Category has children or items and cannot be deleted.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.SoftDeleteCategory(categoryId, existing.CompanyId, now, identity.UserName))
                return InventoryResult.Fail("CONCURRENCY", "Category was changed by another user.");
            return InventoryResult.Success(categoryId, existing.RowVersion + 1);
        }

        public InventoryResult SaveUom(InvUnitOfMeasure uom, ILedgerIdentity identity)
        {
            InventoryResult perm = RequireManage(identity);
            if (!perm.Ok) return perm;
            if (uom == null || string.IsNullOrWhiteSpace(uom.Code) || string.IsNullOrWhiteSpace(uom.Name))
                return InventoryResult.Fail("VALIDATION", "Unit code and name are required.");
            int companyId = TradeIsolation.ResolveCompany(identity, uom.CompanyId);
            if (companyId <= 0) companyId = LedgerCodes.DefaultCompanyId;
            uom.CompanyId = companyId;
            uom.Code = uom.Code.Trim();
            uom.Name = uom.Name.Trim();
            if (uom.DecimalPlaces < 0 || uom.DecimalPlaces > 6)
                return InventoryResult.Fail("VALIDATION", "Decimal places must be between 0 and 6.");
            if (_store.GetUomByCode(companyId, uom.Code, uom.UomId) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateCode, "A unit with this code already exists.");
            if (_store.GetUomByName(companyId, uom.Name, uom.UomId) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateName, "A unit with this name already exists.");
            uom.IsActive = true;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (uom.UomId <= 0)
            {
                long id = _store.InsertUom(uom, now, identity.UserName);
                if (id <= 0) return InventoryResult.Fail(InventoryCodes.DuplicateCode, "Could not create unit.");
                return InventoryResult.Success(id, 1);
            }
            InvUnitOfMeasure existing = _store.GetUom(uom.UomId);
            if (existing == null) return InventoryResult.Fail("NOT_FOUND", "Unit not found.");
            uom.RowVersion = existing.RowVersion;
            if (!_store.UpdateUom(uom, now, identity.UserName))
                return InventoryResult.Fail("CONCURRENCY", "Unit was changed by another user.");
            return InventoryResult.Success(uom.UomId, existing.RowVersion + 1);
        }

        public InventoryResult DeleteUom(long uomId, ILedgerIdentity identity)
        {
            InventoryResult perm = RequireManage(identity);
            if (!perm.Ok) return perm;
            InvUnitOfMeasure existing = _store.GetUom(uomId);
            if (existing == null) return InventoryResult.Fail("NOT_FOUND", "Unit not found.");
            int companyId = TradeIsolation.ResolveCompany(identity, existing.CompanyId);
            if (existing.CompanyId != companyId)
                return InventoryResult.Fail("PERMISSION", "Cross-company unit delete is not allowed.");
            if (_store.UomHasItems(uomId))
                return InventoryResult.Fail(InventoryCodes.InUse, "Unit is used by items and cannot be deleted.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.SoftDeleteUom(uomId, existing.CompanyId, now, identity.UserName))
                return InventoryResult.Fail("CONCURRENCY", "Unit was changed by another user.");
            return InventoryResult.Success(uomId, existing.RowVersion + 1);
        }

        private InventoryResult PrepareItem(InvItem item, ILedgerIdentity identity, long exceptItemId)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name))
                return InventoryResult.Fail("VALIDATION", "Item code and name are required.");
            int companyId = TradeIsolation.ResolveCompany(identity, item.CompanyId);
            if (companyId <= 0) companyId = LedgerCodes.DefaultCompanyId;
            item.CompanyId = companyId;
            item.Code = item.Code.Trim();
            item.Name = item.Name.Trim();
            item.Barcode = string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode.Trim();
            if (item.Code.Length > 64 || item.Name.Length > 200 || (item.Barcode != null && item.Barcode.Length > 64))
                return InventoryResult.Fail("VALIDATION", "Item code, name, or barcode is too long.");
            if (item.MinQtyBase < 0 || item.MaxQtyBase < 0)
                return InventoryResult.Fail("VALIDATION", "Min and max quantity cannot be negative.");
            if (item.MaxQtyBase > 0 && item.MaxQtyBase < item.MinQtyBase)
                return InventoryResult.Fail("VALIDATION", "Max quantity must be greater than or equal to min, or zero for unlimited.");
            if (_store.GetItemByCode(companyId, item.Code, exceptItemId) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateCode, "An item with this code already exists.");
            if (_store.GetItemByName(companyId, item.Name, exceptItemId) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateName, "An item with this name already exists.");
            if (!string.IsNullOrEmpty(item.Barcode) && _store.GetItemByBarcode(companyId, item.Barcode, exceptItemId) != null)
                return InventoryResult.Fail(InventoryCodes.DuplicateBarcode, "An item with this barcode already exists.");
            if (item.CategoryId <= 0) item.CategoryId = _store.DefaultCategoryId(companyId);
            if (item.BaseUomId <= 0) item.BaseUomId = _store.DefaultUomId(companyId);
            InvItemCategory cat = _store.GetCategory(item.CategoryId);
            if (cat == null || cat.CompanyId != companyId)
                return InventoryResult.Fail("VALIDATION", "Category is required.");
            InvUnitOfMeasure uom = _store.GetUom(item.BaseUomId);
            if (uom == null || uom.CompanyId != companyId)
                return InventoryResult.Fail("VALIDATION", "Unit of measure is required.");
            if (string.IsNullOrEmpty(item.CostingMethod)) item.CostingMethod = InventoryCodes.CostingMovingAverage;
            return InventoryResult.Success(0, 0);
        }

        private bool WouldCycle(long categoryId, long parentId)
        {
            long walk = parentId;
            int guard = 0;
            while (walk > 0 && guard++ < 32)
            {
                if (walk == categoryId) return true;
                InvItemCategory parent = _store.GetCategory(walk);
                if (parent == null) break;
                walk = parent.ParentCategoryId;
            }
            return false;
        }

        private InventoryResult RequireManageOrCreate(ILedgerIdentity identity)
        {
            InventoryResult perm = _validation.GuardIdentity(identity, InventoryPermissions.ManageItem);
            if (perm.Ok) return perm;
            InventoryResult create = _validation.GuardIdentity(identity, InventoryPermissions.Create);
            if (create.Ok) return create;
            return perm;
        }

        private InventoryResult RequireManage(ILedgerIdentity identity)
        {
            return _validation.GuardIdentity(identity, InventoryPermissions.ManageItem);
        }
    }
}

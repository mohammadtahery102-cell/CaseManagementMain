using System;

namespace CaseManagement.Inventory.Domain
{
    public static class InventoryCodes
    {
        public const string DocInvDocument = "InvDocument";
        public const string TypeReceipt = "Receipt";
        public const string TypeIssue = "Issue";
        public const string TypeAdjustment = "Adjustment";
        public const string TypeTransfer = "Transfer";
        public const string TypeRevalue = "Revalue";
        public const string TypeOpening = "Opening";
        public const string TypeCount = "Count";

        public const string StatusDraft = "Draft";
        public const string StatusSubmitted = "Submitted";
        public const string StatusPosted = "Posted";
        public const string StatusReversed = "Reversed";

        public const string MoveIn = "In";
        public const string MoveOut = "Out";

        public const string OwnerCompany = "Company";
        public const string OwnerCategory = "Category";
        public const string OwnerItem = "Item";

        public const string RoleInventory = "Inventory";
        public const string RoleCogs = "COGS";
        public const string RoleAdjGain = "AdjustmentGain";
        public const string RoleAdjLoss = "AdjustmentLoss";
        public const string RoleRevaluation = "Revaluation";
        public const string RoleGrir = "GRIR";
        public const string RoleOpeningOffset = "OpeningOffset";

        public const string CostingMovingAverage = "MovingAverage";
        public const string DefaultUom = "PCS";
        public const string NegativeStock = "NEGATIVE_STOCK";
        public const string InterBranch = "INTER_BRANCH";
        public const string DuplicateCode = "DUPLICATE_CODE";
        public const string DuplicateBarcode = "DUPLICATE_BARCODE";
        public const string DuplicateName = "DUPLICATE_NAME";
        public const string DuplicateMovement = "DUPLICATE_MOVEMENT";
        public const string InactiveItem = "INACTIVE_ITEM";
        public const string InUse = "IN_USE";
        public const int MaxCategoryLevel = 8;
    }

    public static class InventoryPermissions
    {
        public const string View = "Inventory.View";
        public const string Create = "Inventory.Create";
        public const string Post = "Inventory.Post";
        public const string Approve = "Inventory.Approve";
        public const string Reverse = "Inventory.Reverse";
        public const string Adjust = "Inventory.Adjust";
        public const string Revalue = "Inventory.Revalue";
        public const string ManageItem = "Inventory.ManageItem";
        public const string ManageWarehouse = "Inventory.ManageWarehouse";
        public const string MapAccounts = "Inventory.MapAccounts";
    }

    public class InventoryResult
    {
        public bool Ok { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public long EntityId { get; set; }
        public long RowVersion { get; set; }

        public static InventoryResult Success(long id, long rowVersion)
        {
            return new InventoryResult { Ok = true, EntityId = id, RowVersion = rowVersion };
        }

        public static InventoryResult Fail(string code, string message)
        {
            return new InventoryResult { Ok = false, ErrorCode = code, Message = message };
        }
    }

    public class InvItemCategory
    {
        public long CategoryId { get; set; }
        public int CompanyId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public long ParentCategoryId { get; set; }
        public int Level { get; set; }
        public bool IsLeaf { get; set; }
        public bool IsActive { get; set; }
        public long RowVersion { get; set; }
    }

    public class InvUnitOfMeasure
    {
        public long UomId { get; set; }
        public int CompanyId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int DecimalPlaces { get; set; }
        public bool IsActive { get; set; }
        public long RowVersion { get; set; }
    }

    public class InvItem
    {
        public long ItemId { get; set; }
        public int CompanyId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Barcode { get; set; }
        public long CategoryId { get; set; }
        public long BaseUomId { get; set; }
        public string CostingMethod { get; set; }
        public bool IsStockable { get; set; }
        public long MinQtyBase { get; set; }
        public long MaxQtyBase { get; set; }
        public bool IsActive { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string CategoryName { get; set; }
        public string UomName { get; set; }
    }

    public class InvItemFilter
    {
        public string Query { get; set; }
        public long CategoryId { get; set; }
        public int ActiveMode { get; set; }
    }

    public class InvWarehouse
    {
        public long WarehouseId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public long RowVersion { get; set; }
    }

    public class InvLocation
    {
        public long LocationId { get; set; }
        public long WarehouseId { get; set; }
        public int CompanyId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool IsLeaf { get; set; }
        public long RowVersion { get; set; }
    }

    public class InvItemMap
    {
        public long MapId { get; set; }
        public int CompanyId { get; set; }
        public string OwnerType { get; set; }
        public long OwnerId { get; set; }
        public string MapRole { get; set; }
        public long AccountId { get; set; }
        public long RowVersion { get; set; }
    }

    public class InvDocument
    {
        public long DocumentId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocNo { get; set; }
        public string DocumentType { get; set; }
        public string Status { get; set; }
        public string PostingDate { get; set; }
        public long WarehouseId { get; set; }
        public long? ToWarehouseId { get; set; }
        public long? ToLocationId { get; set; }
        public long? CostCenterId { get; set; }
        public long? ProjectId { get; set; }
        public int? PartyId { get; set; }
        public string SourceModule { get; set; }
        public string SourceDocumentType { get; set; }
        public long? SourceDocumentId { get; set; }
        public string GroupId { get; set; }
        public string Description { get; set; }
        public long RowVersion { get; set; }
        public string CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class InvDocumentLine
    {
        public long LineId { get; set; }
        public long DocumentId { get; set; }
        public int LineNo { get; set; }
        public long ItemId { get; set; }
        public long LocationId { get; set; }
        public long QtyDoc { get; set; }
        public long QtyBase { get; set; }
        public long UnitCostMinor { get; set; }
        public long ValueMinor { get; set; }
        public long? CostCenterId { get; set; }
        public long? ProjectId { get; set; }
    }

    public class InvItemLedger
    {
        public long ItemLedgerId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long ItemId { get; set; }
        public long WarehouseId { get; set; }
        public long LocationId { get; set; }
        public string DocumentType { get; set; }
        public long DocumentId { get; set; }
        public int DocumentLineNo { get; set; }
        public string MovementType { get; set; }
        public long QtyBase { get; set; }
        public long UnitCostMinor { get; set; }
        public long ValueMinor { get; set; }
        public string PostingDate { get; set; }
        public long? CostCenterId { get; set; }
        public long? ProjectId { get; set; }
        public long? ReversesLedgerId { get; set; }
        public string CreatedBy { get; set; }
    }

    public class InvItemBalance
    {
        public long BalanceId { get; set; }
        public int CompanyId { get; set; }
        public long ItemId { get; set; }
        public long WarehouseId { get; set; }
        public long LocationId { get; set; }
        public long QuantityOnHand { get; set; }
        public long InventoryValueMinor { get; set; }
        public long AverageCostMinor { get; set; }
        public long RowVersion { get; set; }
    }

    public class StockOnHandRow
    {
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string WarehouseCode { get; set; }
        public string LocationCode { get; set; }
        public int CenterId { get; set; }
        public long QuantityOnHand { get; set; }
        public long InventoryValueMinor { get; set; }
        public long AverageCostMinor { get; set; }
    }

    public class ReorderRow
    {
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public long QuantityOnHand { get; set; }
        public long MinQtyBase { get; set; }
    }

    public class InventoryVsGlRow
    {
        public long AccountId { get; set; }
        public string AccountCode { get; set; }
        public long InventoryValueMinor { get; set; }
        public long GlNetMinor { get; set; }
        public long DifferenceMinor { get; set; }
    }

    public class InventoryPostCommand
    {
        public long DocumentId { get; set; }
        public long ExpectedRowVersion { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocumentType { get; set; }
        public string PostingDate { get; set; }
        public long WarehouseId { get; set; }
        public long ToWarehouseId { get; set; }
        public long ToLocationId { get; set; }
        public string Description { get; set; }
        public string Reason { get; set; }
        public string SourceModule { get; set; }
        public string SourceDocumentType { get; set; }
        public long? SourceDocumentId { get; set; }
        public int? PartyId { get; set; }
        public System.Collections.Generic.IList<InvDocumentLine> Lines { get; set; }
    }

    public class InventoryIntegrityRow
    {
        public long ItemId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public long OpeningQty { get; set; }
        public long ReceiptQty { get; set; }
        public long IssueQty { get; set; }
        public long AdjustmentQty { get; set; }
        public long CountQty { get; set; }
        public long TransferInQty { get; set; }
        public long TransferOutQty { get; set; }
        public long ComputedQty { get; set; }
        public long BalanceQty { get; set; }
        public long LedgerQty { get; set; }
        public bool Balanced { get; set; }
    }

    public class InventoryIntegritySummary
    {
        public int TotalItems { get; set; }
        public int BalancedItems { get; set; }
        public int BrokenItems { get; set; }
        public double IntegrityPercent { get; set; }
        public System.Collections.Generic.IList<InventoryIntegrityRow> Rows { get; set; }
    }

    public class InventoryVelocityRow
    {
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public long QuantityOnHand { get; set; }
        public long IssuedQty { get; set; }
        public long ReceiptQty { get; set; }
    }
}

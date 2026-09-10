using System.Collections.Generic;

namespace CaseManagement.Purchase.Domain
{
    public static class PurchaseCodes
    {
        public const string EntityOrder = "PurPurchaseOrder";
        public const string EntityReceipt = "PurGoodsReceipt";
        public const string EntityInvoice = "PurInvoice";
        public const string DocInvoice = "PurInvoice";
        public const string DocGoodsReceipt = "GoodsReceipt";
        public const string DocOrder = "PurchaseOrder";
        public const string DocRequest = "PurchaseRequest";
    }

    public static class PurchasePermissions
    {
        public const string View = "Purchase.View";
        public const string Create = "Purchase.Create";
        public const string Approve = "Purchase.Approve";
        public const string Post = "Purchase.Post";
    }

    public class PurVendor
    {
        public long VendorId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long CategoryId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public long RowVersion { get; set; }
    }

    public class PurLine
    {
        public long ItemId { get; set; }
        public long Qty { get; set; }
        public long UnitPriceMinor { get; set; }
    }

    public class PurOrder
    {
        public long OrderId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public long VendorId { get; set; }
        public long WarehouseId { get; set; }
        public string OrderDate { get; set; }
        public long RowVersion { get; set; }
    }

    public class PurGoodsReceipt
    {
        public long ReceiptId { get; set; }
        public long OrderId { get; set; }
        public long? InvDocumentId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public string PostingDate { get; set; }
        public long WarehouseId { get; set; }
        public long VendorId { get; set; }
        public long RowVersion { get; set; }
    }

    public class PurInvoice
    {
        public long InvoiceId { get; set; }
        public long ReceiptId { get; set; }
        public long OrderId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public string InvoiceDate { get; set; }
        public long AmountMinor { get; set; }
        public long VendorId { get; set; }
        public long RowVersion { get; set; }
    }

    public class OpenPurchaseOrderRow
    {
        public string DocNo { get; set; }
        public string VendorName { get; set; }
        public string Status { get; set; }
        public string OrderDate { get; set; }
    }

    public class VendorPurchaseRow
    {
        public string VendorCode { get; set; }
        public string VendorName { get; set; }
        public long AmountMinor { get; set; }
    }

    public class GrIrRow
    {
        public string ReceiptNo { get; set; }
        public long InventoryValueMinor { get; set; }
        public string InvoiceNo { get; set; }
        public long InvoiceAmountMinor { get; set; }
    }

    public class PurchaseCommand
    {
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long VendorId { get; set; }
        public long WarehouseId { get; set; }
        public string Date { get; set; }
        public long RequestId { get; set; }
        public long OrderId { get; set; }
        public long ReceiptId { get; set; }
        public IList<PurLine> Lines { get; set; }
    }
}

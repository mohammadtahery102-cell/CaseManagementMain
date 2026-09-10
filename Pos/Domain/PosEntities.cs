using System.Collections.Generic;

namespace CaseManagement.Pos.Domain
{
    public static class PosCodes
    {
        public const string EntitySale = "PosSale";
        public const string EntityReturn = "PosReturn";
        public const string DocSale = "PosSale";
        public const string DocReturn = "PosReturn";
        public const string WalkInCode = "POS-WALK";
    }

    public static class PosPermissions
    {
        public const string View = "POS.View";
        public const string Create = "POS.Create";
        public const string Approve = "POS.Approve";
        public const string Post = "POS.Post";
    }

    public class PosLine
    {
        public long ItemId { get; set; }
        public long Qty { get; set; }
        public long UnitPriceMinor { get; set; }
    }

    public class PosSale
    {
        public long SaleId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long TerminalId { get; set; }
        public long DrawerId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public string SaleDate { get; set; }
        public long AmountMinor { get; set; }
        public long? OrderId { get; set; }
        public long? DeliveryId { get; set; }
        public long? InvoiceId { get; set; }
        public long CustomerId { get; set; }
        public long WarehouseId { get; set; }
        public long RowVersion { get; set; }
    }

    public class PosReturnDoc
    {
        public long ReturnId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long SaleId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public string ReturnDate { get; set; }
        public long AmountMinor { get; set; }
        public long? InvDocumentId { get; set; }
        public long RowVersion { get; set; }
    }

    public class DailySalesRow
    {
        public string DocNo { get; set; }
        public string SaleDate { get; set; }
        public long AmountMinor { get; set; }
        public string Status { get; set; }
    }

    public class CashSummaryRow
    {
        public string TerminalCode { get; set; }
        public long AmountMinor { get; set; }
    }

    public class PosTxnRow
    {
        public string Kind { get; set; }
        public string DocNo { get; set; }
        public long AmountMinor { get; set; }
        public string Status { get; set; }
    }
}

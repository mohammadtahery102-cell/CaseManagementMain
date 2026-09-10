using System.Collections.Generic;

namespace CaseManagement.Sales.Domain
{
    public static class SalesCodes
    {
        public const string EntityOrder = "SalSalesOrder";
        public const string EntityDelivery = "SalDeliveryNote";
        public const string EntityInvoice = "SalInvoice";
        public const string DocInvoice = "SalInvoice";
        public const string DocDelivery = "DeliveryNote";
        public const string DocOrder = "SalesOrder";
        public const string DocQuote = "SalesQuotation";
    }

    public static class SalesPermissions
    {
        public const string View = "Sales.View";
        public const string Create = "Sales.Create";
        public const string Approve = "Sales.Approve";
        public const string Post = "Sales.Post";
    }

    public class SalCustomer
    {
        public long CustomerId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long CategoryId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public long RowVersion { get; set; }
    }

    public class SalLine
    {
        public long ItemId { get; set; }
        public long Qty { get; set; }
        public long UnitPriceMinor { get; set; }
    }

    public class SalOrder
    {
        public long OrderId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public long CustomerId { get; set; }
        public long WarehouseId { get; set; }
        public string OrderDate { get; set; }
        public long RowVersion { get; set; }
    }

    public class SalDelivery
    {
        public long DeliveryId { get; set; }
        public long OrderId { get; set; }
        public long? InvDocumentId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public string PostingDate { get; set; }
        public long WarehouseId { get; set; }
        public long CustomerId { get; set; }
        public long RowVersion { get; set; }
    }

    public class SalInvoice
    {
        public long InvoiceId { get; set; }
        public long DeliveryId { get; set; }
        public long OrderId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public string InvoiceDate { get; set; }
        public long AmountMinor { get; set; }
        public long CustomerId { get; set; }
        public long RowVersion { get; set; }
    }

    public class OpenSalesOrderRow
    {
        public string DocNo { get; set; }
        public string CustomerName { get; set; }
        public string Status { get; set; }
        public string OrderDate { get; set; }
    }

    public class CustomerSalesRow
    {
        public string CustomerCode { get; set; }
        public string CustomerName { get; set; }
        public long AmountMinor { get; set; }
    }

    public class RevenueRow
    {
        public string InvoiceNo { get; set; }
        public string InvoiceDate { get; set; }
        public long AmountMinor { get; set; }
    }

    public class SalesCommand
    {
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long CustomerId { get; set; }
        public long WarehouseId { get; set; }
        public string Date { get; set; }
        public long QuoteId { get; set; }
        public IList<SalLine> Lines { get; set; }
    }
}

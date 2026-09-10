using System.Collections.Generic;

namespace CaseManagement.Crm.Domain
{
    public static class CrmCodes
    {
        public const string EntityLead = "CrmLead";
        public const string EntityOpportunity = "CrmOpportunity";
        public const string StatusDraft = "Draft";
        public const string StatusOpen = "Open";
        public const string StatusQualified = "Qualified";
        public const string StatusConverted = "Converted";
        public const string StatusRejected = "Rejected";
        public const string StatusWon = "Won";
        public const string StatusLost = "Lost";
        public const string StageProspect = "Prospect";
        public const string StageQualified = "Qualified";
        public const string StageProposal = "Proposal";
        public const string StageNegotiation = "Negotiation";
        public const string StageWon = "Won";
        public const string StageLost = "Lost";
        public const string TaskOpen = "Open";
        public const string TaskDone = "Done";
        public const string TaskCancelled = "Cancelled";
        public const string EventActivity = "Activity";
        public const string EventTask = "Task";
        public const string EventStage = "Stage";
        public const string EventConvert = "Convert";
        public const string EventSalesLink = "SalesLink";
    }

    public static class CrmPermissions
    {
        public const string View = "CRM.View";
        public const string Create = "CRM.Create";
        public const string Edit = "CRM.Edit";
        public const string Approve = "CRM.Approve";
        public const string Delete = "CRM.Delete";
    }

    public class CrmLead
    {
        public long LeadId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string Source { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public long? OpportunityId { get; set; }
        public long? CustomerId { get; set; }
        public long RowVersion { get; set; }
    }

    public class CrmOpportunity
    {
        public long OpportunityId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string Stage { get; set; }
        public long? LeadId { get; set; }
        public long? CustomerId { get; set; }
        public long ExpectedAmountMinor { get; set; }
        public string ExpectedCloseDate { get; set; }
        public long? QuoteId { get; set; }
        public long? OrderId { get; set; }
        public long? InvoiceId { get; set; }
        public long RowVersion { get; set; }
    }

    public class CrmContact
    {
        public long ContactId { get; set; }
        public int CompanyId { get; set; }
        public long? LeadId { get; set; }
        public long? OpportunityId { get; set; }
        public long? CustomerId { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public long? SalContactId { get; set; }
        public long RowVersion { get; set; }
    }

    public class LeadPipelineRow
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
    }

    public class OpportunityPipelineRow
    {
        public string Stage { get; set; }
        public string Status { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public long ExpectedAmountMinor { get; set; }
    }

    public class SalesFunnelRow
    {
        public string Stage { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public long? QuoteId { get; set; }
        public long? OrderId { get; set; }
        public long? InvoiceId { get; set; }
        public long InvoiceAmountMinor { get; set; }
    }

    public class CustomerActivityRow
    {
        public string OccurredAt { get; set; }
        public string EventType { get; set; }
        public string Summary { get; set; }
    }

    public class FollowUpRow
    {
        public string Title { get; set; }
        public string DueDate { get; set; }
        public string Status { get; set; }
        public bool Overdue { get; set; }
    }
}

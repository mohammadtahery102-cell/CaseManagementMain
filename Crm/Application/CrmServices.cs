using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Crm.Domain;
using CaseManagement.Crm.Infrastructure;
using CaseManagement.Enterprise;
using CaseManagement.Sales.Application;
using CaseManagement.Sales.Domain;
using CaseManagement.Trade;

namespace CaseManagement.Crm.Application
{
    public class CrmWorkflowService
    {
        private readonly CrmStore _store;
        public CrmWorkflowService() : this(new CrmStore()) { }
        public CrmWorkflowService(CrmStore store) { _store = store; }

        public TradeResult SubmitLead(long id, ILedgerIdentity identity)
        { return MoveLead(id, CrmCodes.StatusDraft, CrmCodes.StatusOpen, "SUBMITTED", identity, CrmPermissions.Create); }

        public TradeResult QualifyLead(long id, ILedgerIdentity identity)
        { return MoveLead(id, CrmCodes.StatusOpen, CrmCodes.StatusQualified, "APPROVED", identity, CrmPermissions.Approve); }

        public TradeResult RejectLead(long id, ILedgerIdentity identity)
        { return MoveLead(id, CrmCodes.StatusOpen, CrmCodes.StatusRejected, "REJECTED", identity, CrmPermissions.Approve); }

        public TradeResult SubmitOpportunity(long id, ILedgerIdentity identity)
        { return MoveOpp(id, CrmCodes.StatusDraft, CrmCodes.StatusOpen, null, "SUBMITTED", identity, CrmPermissions.Create); }

        public TradeResult WinOpportunity(long id, ILedgerIdentity identity)
        { return MoveOpp(id, CrmCodes.StatusOpen, CrmCodes.StatusWon, CrmCodes.StageWon, "APPROVED", identity, CrmPermissions.Approve); }

        public TradeResult LoseOpportunity(long id, ILedgerIdentity identity)
        { return MoveOpp(id, CrmCodes.StatusOpen, CrmCodes.StatusLost, CrmCodes.StageLost, "REJECTED", identity, CrmPermissions.Approve); }

        private TradeResult MoveLead(long id, string from, string to, string wf, ILedgerIdentity identity, string perm)
        {
            if (identity == null || !identity.HasPermission(perm))
                return TradeResult.Fail("PERMISSION", perm);
            CrmLead lead = _store.GetLead(id);
            if (lead == null) return TradeResult.Fail("NOT_FOUND", "Lead not found.");
            TradeResult gate = CrmGuard.Branch(identity, lead.CenterId);
            if (!gate.Ok) return gate;
            if (lead.Status != from) return TradeResult.Fail("INVALID_STATUS", "Expected " + from);
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.UpdateLeadStatus(id, to, lead.RowVersion, now, identity.UserName, null, null))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(CrmCodes.EntityLead, id, lead.CenterId, wf, identity);
            _store.AppendComm(lead.CompanyId, id, null, lead.CustomerId, CrmCodes.EventStage, "CrmLead", id, to, now, identity.UserName);
            return TradeResult.Success(id, lead.RowVersion + 1);
        }

        private TradeResult MoveOpp(long id, string from, string to, string stage, string wf, ILedgerIdentity identity, string perm)
        {
            if (identity == null || !identity.HasPermission(perm))
                return TradeResult.Fail("PERMISSION", perm);
            CrmOpportunity o = _store.GetOpportunity(id);
            if (o == null) return TradeResult.Fail("NOT_FOUND", "Opportunity not found.");
            TradeResult gate = CrmGuard.Branch(identity, o.CenterId);
            if (!gate.Ok) return gate;
            if (o.Status != from) return TradeResult.Fail("INVALID_STATUS", "Expected " + from);
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.UpdateOpp(id, to, stage, o.RowVersion, now, identity.UserName, null, null, null, null))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.MoveTo(CrmCodes.EntityOpportunity, id, o.CenterId, wf, identity);
            _store.AppendComm(o.CompanyId, o.LeadId, id, o.CustomerId, CrmCodes.EventStage, "CrmOpportunity", id, to, now, identity.UserName);
            return TradeResult.Success(id, o.RowVersion + 1);
        }
    }

    public class LeadService
    {
        private readonly CrmStore _store;
        private readonly CrmWorkflowService _wf;
        public LeadService() : this(new CrmStore(), new CrmWorkflowService()) { }
        public LeadService(CrmStore store, CrmWorkflowService wf) { _store = store; _wf = wf; }

        public TradeResult Create(CrmLead lead, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Create))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Create);
            if (lead == null || string.IsNullOrWhiteSpace(lead.Name))
                return TradeResult.Fail("VALIDATION", "Lead name is required.");
            lead.CompanyId = CrmGuard.Company(lead.CompanyId, identity);
            lead.CenterId = CrmGuard.Center(lead.CenterId, identity);
            TradeResult gate = CrmGuard.Branch(identity, lead.CenterId);
            if (!gate.Ok) return gate;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                lead.Code = string.IsNullOrWhiteSpace(lead.Code) ? _store.AllocateLeadNo(con, tr, lead.CompanyId) : lead.Code;
                lead.Status = CrmCodes.StatusDraft;
                newId = _store.InsertLead(con, tr, lead, now, identity.UserName);
            });
            if (newId <= 0) return TradeResult.Fail("VALIDATION", "Could not create lead.");
            DocumentWorkflowService.Ensure(CrmCodes.EntityLead, newId, lead.CenterId, identity);
            _store.AppendComm(lead.CompanyId, newId, null, null, CrmCodes.EventStage, "CrmLead", newId, CrmCodes.StatusDraft, now, identity.UserName);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult Submit(long id, ILedgerIdentity identity) { return _wf.SubmitLead(id, identity); }
        public TradeResult Qualify(long id, ILedgerIdentity identity) { return _wf.QualifyLead(id, identity); }
        public TradeResult Reject(long id, ILedgerIdentity identity) { return _wf.RejectLead(id, identity); }

        public TradeResult ConvertToOpportunity(long leadId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Create))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Create);
            CrmLead lead = _store.GetLead(leadId);
            if (lead == null) return TradeResult.Fail("NOT_FOUND", "Lead not found.");
            TradeResult gate = CrmGuard.Branch(identity, lead.CenterId);
            if (!gate.Ok) return gate;
            if (lead.Status != CrmCodes.StatusQualified)
                return TradeResult.Fail("INVALID_STATUS", "Lead must be Qualified.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long oppId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                CrmOpportunity o = new CrmOpportunity();
                o.CompanyId = lead.CompanyId;
                o.CenterId = lead.CenterId;
                o.Code = _store.AllocateOppNo(con, tr, lead.CompanyId);
                o.Name = lead.Name;
                o.Status = CrmCodes.StatusDraft;
                o.Stage = CrmCodes.StageQualified;
                o.LeadId = leadId;
                oppId = _store.InsertOpportunity(con, tr, o, now, identity.UserName);
            });
            if (!_store.UpdateLeadStatus(leadId, CrmCodes.StatusConverted, lead.RowVersion, now, identity.UserName, oppId, null))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            DocumentWorkflowService.Ensure(CrmCodes.EntityOpportunity, oppId, lead.CenterId, identity);
            _store.AppendComm(lead.CompanyId, leadId, oppId, null, CrmCodes.EventConvert, "CrmOpportunity", oppId, "Lead converted", now, identity.UserName);
            return TradeResult.Success(oppId, 1);
        }

        public TradeResult Delete(long id, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Delete))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Delete);
            CrmLead lead = _store.GetLead(id);
            if (lead == null) return TradeResult.Fail("NOT_FOUND", "Lead not found.");
            TradeResult gate = CrmGuard.Branch(identity, lead.CenterId);
            if (!gate.Ok) return gate;
            if (lead.Status == CrmCodes.StatusConverted)
                return TradeResult.Fail("INVALID_STATUS", "Converted lead cannot be deleted.");
            if (!_store.SoftDeleteLead(id, lead.RowVersion, LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            return TradeResult.Success(id, lead.RowVersion + 1);
        }

        public CrmLead Get(long id) { return _store.GetLead(id); }
    }

    public class OpportunityService
    {
        private readonly CrmStore _store;
        private readonly CrmWorkflowService _wf;
        private readonly CustomerService _customers;
        public OpportunityService() : this(new CrmStore(), new CrmWorkflowService(), new CustomerService()) { }
        public OpportunityService(CrmStore store, CrmWorkflowService wf, CustomerService customers)
        {
            _store = store;
            _wf = wf;
            _customers = customers;
        }

        public TradeResult Create(CrmOpportunity o, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Create))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Create);
            if (o == null || string.IsNullOrWhiteSpace(o.Name))
                return TradeResult.Fail("VALIDATION", "Opportunity name is required.");
            o.CompanyId = CrmGuard.Company(o.CompanyId, identity);
            o.CenterId = CrmGuard.Center(o.CenterId, identity);
            TradeResult gate = CrmGuard.Branch(identity, o.CenterId);
            if (!gate.Ok) return gate;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long newId = 0;
            _store.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                o.Code = string.IsNullOrWhiteSpace(o.Code) ? _store.AllocateOppNo(con, tr, o.CompanyId) : o.Code;
                o.Status = CrmCodes.StatusDraft;
                if (string.IsNullOrWhiteSpace(o.Stage)) o.Stage = CrmCodes.StageProspect;
                newId = _store.InsertOpportunity(con, tr, o, now, identity.UserName);
            });
            DocumentWorkflowService.Ensure(CrmCodes.EntityOpportunity, newId, o.CenterId, identity);
            _store.AppendComm(o.CompanyId, o.LeadId, newId, o.CustomerId, CrmCodes.EventStage, "CrmOpportunity", newId, CrmCodes.StatusDraft, now, identity.UserName);
            return TradeResult.Success(newId, 1);
        }

        public TradeResult Submit(long id, ILedgerIdentity identity) { return _wf.SubmitOpportunity(id, identity); }
        public TradeResult Win(long id, ILedgerIdentity identity) { return _wf.WinOpportunity(id, identity); }
        public TradeResult Lose(long id, ILedgerIdentity identity) { return _wf.LoseOpportunity(id, identity); }

        public TradeResult UpdateStage(long id, string stage, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Edit))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Edit);
            CrmOpportunity o = _store.GetOpportunity(id);
            if (o == null) return TradeResult.Fail("NOT_FOUND", "Opportunity not found.");
            TradeResult gate = CrmGuard.Branch(identity, o.CenterId);
            if (!gate.Ok) return gate;
            if (o.Status != CrmCodes.StatusOpen)
                return TradeResult.Fail("INVALID_STATUS", "Stage can change only while Open.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.UpdateOpp(id, null, stage, o.RowVersion, now, identity.UserName, null, null, null, null))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            _store.AppendComm(o.CompanyId, o.LeadId, id, o.CustomerId, CrmCodes.EventStage, "CrmOpportunity", id, stage, now, identity.UserName);
            return TradeResult.Success(id, o.RowVersion + 1);
        }

        public TradeResult ConvertToCustomer(long opportunityId, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Approve))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Approve);
            if (!identity.HasPermission(SalesPermissions.Create))
                return TradeResult.Fail("PERMISSION", SalesPermissions.Create);
            CrmOpportunity o = _store.GetOpportunity(opportunityId);
            if (o == null) return TradeResult.Fail("NOT_FOUND", "Opportunity not found.");
            TradeResult gate = CrmGuard.Branch(identity, o.CenterId);
            if (!gate.Ok) return gate;
            if (o.Status != CrmCodes.StatusWon)
                return TradeResult.Fail("INVALID_STATUS", "Opportunity must be Won.");
            long customerId = _store.FindCustomerId(o.CompanyId, o.Code);
            if (customerId <= 0)
            {
                SalCustomer c = new SalCustomer();
                c.CompanyId = o.CompanyId;
                c.CenterId = o.CenterId;
                c.Code = o.Code;
                c.Name = o.Name;
                TradeResult created = _customers.CreateCustomer(c, identity);
                if (!created.Ok) return created;
                customerId = created.EntityId;
            }
            IList<CrmContact> contacts = _store.ListContacts(o.LeadId, opportunityId, null);
            for (int i = 0; i < contacts.Count; i++)
            {
                _customers.CreateContact(customerId, contacts[i].Name, identity);
                _store.SetContactCustomer(contacts[i].ContactId, customerId);
            }
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.UpdateOpp(opportunityId, CrmCodes.StatusConverted, CrmCodes.StageWon, o.RowVersion, now, identity.UserName, customerId, null, null, null))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            if (o.LeadId.HasValue)
            {
                CrmLead lead = _store.GetLead(o.LeadId.Value);
                if (lead != null)
                    _store.UpdateLeadStatus(lead.LeadId, lead.Status, lead.RowVersion, now, identity.UserName, null, customerId);
            }
            _store.AppendComm(o.CompanyId, o.LeadId, opportunityId, customerId, CrmCodes.EventConvert, "SalCustomer", customerId, "Converted to customer", now, identity.UserName);
            return TradeResult.Success(customerId, 1);
        }

        public TradeResult LinkQuote(long opportunityId, long quoteId, ILedgerIdentity identity)
        {
            return LinkSales(opportunityId, quoteId, "Quote", identity);
        }

        public TradeResult LinkOrder(long opportunityId, long orderId, ILedgerIdentity identity)
        {
            return LinkSales(opportunityId, orderId, "Order", identity);
        }

        public TradeResult LinkInvoice(long opportunityId, long invoiceId, ILedgerIdentity identity)
        {
            return LinkSales(opportunityId, invoiceId, "Invoice", identity);
        }

        private TradeResult LinkSales(long opportunityId, long docId, string kind, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Edit))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Edit);
            CrmOpportunity o = _store.GetOpportunity(opportunityId);
            if (o == null) return TradeResult.Fail("NOT_FOUND", "Opportunity not found.");
            TradeResult gate = CrmGuard.Branch(identity, o.CenterId);
            if (!gate.Ok) return gate;
            if (!o.CustomerId.HasValue)
                return TradeResult.Fail("INVALID_STATUS", "Convert to customer first.");
            bool ok = false;
            long? q = null, ord = null, inv = null;
            if (kind == "Quote") { ok = _store.QuoteMatches(docId, o.CompanyId, o.CustomerId.Value); q = docId; }
            else if (kind == "Order") { ok = _store.OrderMatches(docId, o.CompanyId, o.CustomerId.Value); ord = docId; }
            else { ok = _store.InvoiceMatches(docId, o.CompanyId, o.CustomerId.Value); inv = docId; }
            if (!ok) return TradeResult.Fail("VALIDATION", kind + " does not belong to this customer.");
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.UpdateOpp(opportunityId, null, null, o.RowVersion, now, identity.UserName, null, q, ord, inv))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            _store.AppendComm(o.CompanyId, o.LeadId, opportunityId, o.CustomerId, CrmCodes.EventSalesLink, kind, docId, kind + " " + docId, now, identity.UserName);
            return TradeResult.Success(opportunityId, o.RowVersion + 1);
        }

        public TradeResult Delete(long id, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Delete))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Delete);
            CrmOpportunity o = _store.GetOpportunity(id);
            if (o == null) return TradeResult.Fail("NOT_FOUND", "Opportunity not found.");
            TradeResult gate = CrmGuard.Branch(identity, o.CenterId);
            if (!gate.Ok) return gate;
            if (o.Status == CrmCodes.StatusConverted)
                return TradeResult.Fail("INVALID_STATUS", "Converted opportunity cannot be deleted.");
            if (!_store.SoftDeleteOpp(id, o.RowVersion, LedgerTime.UtcNow(identity.UtcNow), identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            return TradeResult.Success(id, o.RowVersion + 1);
        }

        public CrmOpportunity Get(long id) { return _store.GetOpportunity(id); }
    }

    public class CrmContactService
    {
        private readonly CrmStore _store;
        public CrmContactService() : this(new CrmStore()) { }
        public CrmContactService(CrmStore store) { _store = store; }

        public TradeResult Create(CrmContact contact, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Create))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Create);
            if (contact == null || string.IsNullOrWhiteSpace(contact.Name))
                return TradeResult.Fail("VALIDATION", "Contact name is required.");
            if (!contact.LeadId.HasValue && !contact.OpportunityId.HasValue)
                return TradeResult.Fail("VALIDATION", "Lead or opportunity is required.");
            contact.CompanyId = CrmGuard.Company(contact.CompanyId, identity);
            long id = _store.InsertContact(contact, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
            return TradeResult.Success(id, 1);
        }
    }

    public class ActivityService
    {
        private readonly CrmStore _store;
        public ActivityService() : this(new CrmStore()) { }
        public ActivityService(CrmStore store) { _store = store; }

        public TradeResult Log(int companyId, int centerId, string kind, long? leadId, long? oppId, long? customerId,
            string subject, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Create))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Create);
            if (string.IsNullOrWhiteSpace(subject))
                return TradeResult.Fail("VALIDATION", "Subject is required.");
            companyId = CrmGuard.Company(companyId, identity);
            centerId = CrmGuard.Center(centerId, identity);
            TradeResult gate = CrmGuard.Branch(identity, centerId);
            if (!gate.Ok) return gate;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long id = _store.InsertActivity(companyId, centerId, string.IsNullOrWhiteSpace(kind) ? "Note" : kind,
                leadId, oppId, customerId, subject, null, now, identity.UserName);
            _store.AppendComm(companyId, leadId, oppId, customerId, CrmCodes.EventActivity, "CrmActivity", id, subject, now, identity.UserName);
            return TradeResult.Success(id, 1);
        }
    }

    public class CrmTaskService
    {
        private readonly CrmStore _store;
        public CrmTaskService() : this(new CrmStore()) { }
        public CrmTaskService(CrmStore store) { _store = store; }

        public TradeResult Create(int companyId, int centerId, long? leadId, long? oppId, long? customerId,
            string title, string dueDate, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Create))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Create);
            if (string.IsNullOrWhiteSpace(title))
                return TradeResult.Fail("VALIDATION", "Title is required.");
            companyId = CrmGuard.Company(companyId, identity);
            centerId = CrmGuard.Center(centerId, identity);
            TradeResult gate = CrmGuard.Branch(identity, centerId);
            if (!gate.Ok) return gate;
            string now = LedgerTime.UtcNow(identity.UtcNow);
            long id = _store.InsertTask(companyId, centerId, leadId, oppId, customerId, title, dueDate, identity.UserName, now, identity.UserName);
            if (id > 0 && id <= int.MaxValue)
            {
                try
                {
                    long ent = TaskService.Create(title, "", CrmCodes.EntityOpportunity, (int)id, identity.UserId, null, "متوسط", dueDate, "CRM", (int)id);
                    if (ent > 0) _store.SetEntTaskId(id, ent);
                }
                catch (Exception)
                {
                }
            }
            _store.AppendComm(companyId, leadId, oppId, customerId, CrmCodes.EventTask, "CrmTask", id, title, now, identity.UserName);
            return TradeResult.Success(id, 1);
        }

        public TradeResult Complete(long id, ILedgerIdentity identity)
        {
            if (identity == null || !identity.HasPermission(CrmPermissions.Edit))
                return TradeResult.Fail("PERMISSION", CrmPermissions.Edit);
            DataRow row = _store.GetTaskRow(id);
            if (row == null) return TradeResult.Fail("NOT_FOUND", "Task not found.");
            TradeResult gate = CrmGuard.Branch(identity, Convert.ToInt32(row["CenterID"]));
            if (!gate.Ok) return gate;
            long rv = Convert.ToInt64(row["RowVersion"]);
            string now = LedgerTime.UtcNow(identity.UtcNow);
            if (!_store.CompleteTask(id, rv, now, identity.UserName))
                return TradeResult.Fail("CONCURRENCY", "RowVersion mismatch.");
            return TradeResult.Success(id, rv + 1);
        }
    }

    public class CrmReporting
    {
        private readonly CrmStore _store;
        public CrmReporting() : this(new CrmStore()) { }
        public CrmReporting(CrmStore store) { _store = store; }

        public IList<LeadPipelineRow> LeadPipeline(ILedgerIdentity identity)
        {
            if (!CrmGuard.CanView(identity)) return new List<LeadPipelineRow>();
            return _store.LeadPipeline(CrmGuard.Company(0, identity), CrmGuard.ReportCenter(identity));
        }

        public IList<OpportunityPipelineRow> OpportunityPipeline(ILedgerIdentity identity)
        {
            if (!CrmGuard.CanView(identity)) return new List<OpportunityPipelineRow>();
            return _store.OpportunityPipeline(CrmGuard.Company(0, identity), CrmGuard.ReportCenter(identity));
        }

        public IList<SalesFunnelRow> SalesFunnel(ILedgerIdentity identity)
        {
            if (!CrmGuard.CanView(identity)) return new List<SalesFunnelRow>();
            IList<SalesFunnelRow> rows = _store.SalesFunnel(CrmGuard.Company(0, identity), CrmGuard.ReportCenter(identity));
            if (identity.HasPermission(SalesPermissions.View)) return rows;
            for (int i = 0; i < rows.Count; i++) rows[i].InvoiceAmountMinor = 0;
            return rows;
        }

        public IList<CustomerActivityRow> CustomerActivity(long customerId, ILedgerIdentity identity)
        {
            if (!CrmGuard.CanView(identity)) return new List<CustomerActivityRow>();
            return _store.CustomerActivity(CrmGuard.Company(0, identity), customerId);
        }

        public IList<FollowUpRow> FollowUps(string asOf, ILedgerIdentity identity)
        {
            if (!CrmGuard.CanView(identity)) return new List<FollowUpRow>();
            return _store.FollowUps(CrmGuard.Company(0, identity), CrmGuard.ReportCenter(identity), asOf);
        }
    }

    internal static class CrmGuard
    {
        public static bool CanView(ILedgerIdentity identity)
        {
            return identity != null && identity.HasPermission(CrmPermissions.View);
        }

        public static int Company(int companyId, ILedgerIdentity identity)
        {
            if (identity != null && !identity.IsSuperAdmin && identity.CompanyId > 0)
                return identity.CompanyId;
            if (companyId > 0) return companyId;
            if (identity != null && identity.CompanyId > 0) return identity.CompanyId;
            return LedgerCodes.DefaultCompanyId;
        }

        public static int Center(int centerId, ILedgerIdentity identity)
        {
            if (centerId > 0) return centerId;
            if (identity != null && identity.CenterId > 0) return identity.CenterId;
            return 1;
        }

        public static int ReportCenter(ILedgerIdentity identity)
        {
            // هویت نامعتبر نباید با مقدار ۰ به گزارش همه شعب دسترسی پیدا کند.
            if (identity == null) return -1;
            if (identity.IsSuperAdmin && identity.CenterId == 0) return 0;
            return identity.CenterId;
        }

        public static TradeResult Branch(ILedgerIdentity identity, int centerId)
        {
            if (!TradeIsolation.CanSeeCenter(identity, centerId))
                return TradeResult.Fail("PERMISSION", "Cross-branch CRM is not allowed.");
            return TradeResult.Success(0, 0);
        }
    }
}

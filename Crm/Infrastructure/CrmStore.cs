using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.Crm.Domain;
using CaseManagement.DAL;

namespace CaseManagement.Crm.Infrastructure
{
    public class CrmStore
    {
        private readonly DatabaseHelper _db;
        public CrmStore() : this(new DatabaseHelper()) { }
        public CrmStore(DatabaseHelper db) { _db = db; }

        public void ExecuteInTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            _db.ExecuteInTransaction(action);
        }

        public string AllocateLeadNo(SQLiteConnection con, SQLiteTransaction tr, int companyId)
        {
            return Allocate(con, tr, companyId, "L", "NextLeadNumber");
        }

        public string AllocateOppNo(SQLiteConnection con, SQLiteTransaction tr, int companyId)
        {
            return Allocate(con, tr, companyId, "O", "NextOppNumber");
        }

        private string Allocate(SQLiteConnection con, SQLiteTransaction tr, int companyId, string kind, string col)
        {
            long next = 1;
            string prefix = "CRM";
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT " + col + ", NumberPrefix FROM CrmSetting WHERE CompanyID = @c AND IsDeleted = 0;", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        next = Convert.ToInt64(r[0]);
                        prefix = Convert.ToString(r["NumberPrefix"]);
                    }
                }
            }
            using (SQLiteCommand upd = new SQLiteCommand("UPDATE CrmSetting SET " + col + " = " + col + " + 1 WHERE CompanyID = @c;", con, tr))
            {
                upd.Parameters.AddWithValue("@c", companyId);
                upd.ExecuteNonQuery();
            }
            return prefix + "-" + kind + next.ToString();
        }

        public long InsertLead(SQLiteConnection con, SQLiteTransaction tr, CrmLead lead, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO CrmLead (CompanyID, CenterID, Code, Name, Status, Source, Phone, Email, OwnerUser, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @code, @name, @st, @src, @ph, @em, @own, 0, 1, @n, @n, @u, @u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", lead.CompanyId);
                cmd.Parameters.AddWithValue("@ctr", lead.CenterId);
                cmd.Parameters.AddWithValue("@code", lead.Code);
                cmd.Parameters.AddWithValue("@name", lead.Name);
                cmd.Parameters.AddWithValue("@st", lead.Status);
                cmd.Parameters.AddWithValue("@src", (object)lead.Source ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ph", (object)lead.Phone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@em", (object)lead.Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@own", (object)user ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public long InsertOpportunity(SQLiteConnection con, SQLiteTransaction tr, CrmOpportunity o, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO CrmOpportunity (CompanyID, CenterID, Code, Name, Status, Stage, LeadID, CustomerID, ExpectedAmountMinor, ExpectedCloseDate, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @code, @name, @st, @sg, @lead, @cust, @amt, @dt, 0, 1, @n, @n, @u, @u);", con, tr))
            {
                cmd.Parameters.AddWithValue("@c", o.CompanyId);
                cmd.Parameters.AddWithValue("@ctr", o.CenterId);
                cmd.Parameters.AddWithValue("@code", o.Code);
                cmd.Parameters.AddWithValue("@name", o.Name);
                cmd.Parameters.AddWithValue("@st", o.Status);
                cmd.Parameters.AddWithValue("@sg", o.Stage);
                cmd.Parameters.AddWithValue("@lead", o.LeadId.HasValue ? (object)o.LeadId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@cust", o.CustomerId.HasValue ? (object)o.CustomerId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@amt", o.ExpectedAmountMinor);
                cmd.Parameters.AddWithValue("@dt", (object)o.ExpectedCloseDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@n", now);
                cmd.Parameters.AddWithValue("@u", user ?? "");
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        public CrmLead GetLead(long id)
        {
            DataTable t = _db.Query("SELECT * FROM CrmLead WHERE LeadID = @id AND IsDeleted = 0;", P("@id", id));
            if (t.Rows.Count == 0) return null;
            DataRow r = t.Rows[0];
            CrmLead l = new CrmLead();
            l.LeadId = Convert.ToInt64(r["LeadID"]);
            l.CompanyId = Convert.ToInt32(r["CompanyID"]);
            l.CenterId = Convert.ToInt32(r["CenterID"]);
            l.Code = Convert.ToString(r["Code"]);
            l.Name = Convert.ToString(r["Name"]);
            l.Status = Convert.ToString(r["Status"]);
            l.Source = r["Source"] == DBNull.Value ? null : Convert.ToString(r["Source"]);
            l.Phone = r["Phone"] == DBNull.Value ? null : Convert.ToString(r["Phone"]);
            l.Email = r["Email"] == DBNull.Value ? null : Convert.ToString(r["Email"]);
            l.OpportunityId = r["OpportunityID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["OpportunityID"]);
            l.CustomerId = r["CustomerID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["CustomerID"]);
            l.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return l;
        }

        public CrmOpportunity GetOpportunity(long id)
        {
            DataTable t = _db.Query("SELECT * FROM CrmOpportunity WHERE OpportunityID = @id AND IsDeleted = 0;", P("@id", id));
            if (t.Rows.Count == 0) return null;
            DataRow r = t.Rows[0];
            CrmOpportunity o = new CrmOpportunity();
            o.OpportunityId = Convert.ToInt64(r["OpportunityID"]);
            o.CompanyId = Convert.ToInt32(r["CompanyID"]);
            o.CenterId = Convert.ToInt32(r["CenterID"]);
            o.Code = Convert.ToString(r["Code"]);
            o.Name = Convert.ToString(r["Name"]);
            o.Status = Convert.ToString(r["Status"]);
            o.Stage = Convert.ToString(r["Stage"]);
            o.LeadId = r["LeadID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["LeadID"]);
            o.CustomerId = r["CustomerID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["CustomerID"]);
            o.ExpectedAmountMinor = Convert.ToInt64(r["ExpectedAmountMinor"]);
            o.ExpectedCloseDate = r["ExpectedCloseDate"] == DBNull.Value ? null : Convert.ToString(r["ExpectedCloseDate"]);
            o.QuoteId = r["QuoteID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["QuoteID"]);
            o.OrderId = r["OrderID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["OrderID"]);
            o.InvoiceId = r["InvoiceID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["InvoiceID"]);
            o.RowVersion = Convert.ToInt64(r["RowVersion"]);
            return o;
        }

        public bool UpdateLeadStatus(long id, string status, long rowVersion, string now, string user, long? opportunityId, long? customerId)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE CrmLead SET Status = @st, OpportunityID = COALESCE(@opp, OpportunityID), CustomerID = COALESCE(@cust, CustomerID),
  RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u
WHERE LeadID = @id AND RowVersion = @rv AND IsDeleted = 0;",
                P("@st", status), P("@opp", opportunityId.HasValue ? (object)opportunityId.Value : DBNull.Value),
                P("@cust", customerId.HasValue ? (object)customerId.Value : DBNull.Value),
                P("@n", now), P("@u", user ?? ""), P("@id", id), P("@rv", rowVersion));
            return n == 1;
        }

        public bool UpdateOpp(long id, string status, string stage, long rowVersion, string now, string user,
            long? customerId, long? quoteId, long? orderId, long? invoiceId)
        {
            int n = _db.ExecuteNonQuery(@"
UPDATE CrmOpportunity SET Status = COALESCE(@st, Status), Stage = COALESCE(@sg, Stage),
  CustomerID = COALESCE(@cust, CustomerID), QuoteID = COALESCE(@q, QuoteID), OrderID = COALESCE(@o, OrderID), InvoiceID = COALESCE(@i, InvoiceID),
  RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u
WHERE OpportunityID = @id AND RowVersion = @rv AND IsDeleted = 0;",
                P("@st", (object)status ?? DBNull.Value), P("@sg", (object)stage ?? DBNull.Value),
                P("@cust", customerId.HasValue ? (object)customerId.Value : DBNull.Value),
                P("@q", quoteId.HasValue ? (object)quoteId.Value : DBNull.Value),
                P("@o", orderId.HasValue ? (object)orderId.Value : DBNull.Value),
                P("@i", invoiceId.HasValue ? (object)invoiceId.Value : DBNull.Value),
                P("@n", now), P("@u", user ?? ""), P("@id", id), P("@rv", rowVersion));
            return n == 1;
        }

        public bool SoftDeleteLead(long id, long rowVersion, string now, string user)
        {
            return _db.ExecuteNonQuery(@"
UPDATE CrmLead SET IsDeleted = 1, RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u
WHERE LeadID = @id AND RowVersion = @rv AND IsDeleted = 0;",
                P("@n", now), P("@u", user ?? ""), P("@id", id), P("@rv", rowVersion)) == 1;
        }

        public bool SoftDeleteOpp(long id, long rowVersion, string now, string user)
        {
            return _db.ExecuteNonQuery(@"
UPDATE CrmOpportunity SET IsDeleted = 1, RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u
WHERE OpportunityID = @id AND RowVersion = @rv AND IsDeleted = 0;",
                P("@n", now), P("@u", user ?? ""), P("@id", id), P("@rv", rowVersion)) == 1;
        }

        public long InsertContact(CrmContact c, string now, string user)
        {
            long newId = 0;
            _db.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO CrmContact (CompanyID, LeadID, OpportunityID, CustomerID, Name, Phone, Email, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @l, @o, @cust, @n, @ph, @em, 0, 1, @now, @now, @u, @u);", con, tr))
                {
                    cmd.Parameters.AddWithValue("@c", c.CompanyId);
                    cmd.Parameters.AddWithValue("@l", c.LeadId.HasValue ? (object)c.LeadId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@o", c.OpportunityId.HasValue ? (object)c.OpportunityId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@cust", c.CustomerId.HasValue ? (object)c.CustomerId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@n", c.Name);
                    cmd.Parameters.AddWithValue("@ph", (object)c.Phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@em", (object)c.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@now", now);
                    cmd.Parameters.AddWithValue("@u", user ?? "");
                    cmd.ExecuteNonQuery();
                }
                using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                    newId = Convert.ToInt64(id.ExecuteScalar());
            });
            return newId;
        }

        public IList<CrmContact> ListContacts(long? leadId, long? oppId, long? customerId)
        {
            List<CrmContact> list = new List<CrmContact>();
            DataTable t = _db.Query(@"
SELECT * FROM CrmContact WHERE IsDeleted = 0
  AND (
    (@l <> 0 AND LeadID = @l) OR (@o <> 0 AND OpportunityID = @o) OR (@c <> 0 AND CustomerID = @c)
  );",
                P("@l", leadId.HasValue ? leadId.Value : 0),
                P("@o", oppId.HasValue ? oppId.Value : 0),
                P("@c", customerId.HasValue ? customerId.Value : 0));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                DataRow r = t.Rows[i];
                CrmContact c = new CrmContact();
                c.ContactId = Convert.ToInt64(r["ContactID"]);
                c.CompanyId = Convert.ToInt32(r["CompanyID"]);
                c.LeadId = r["LeadID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["LeadID"]);
                c.OpportunityId = r["OpportunityID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["OpportunityID"]);
                c.CustomerId = r["CustomerID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["CustomerID"]);
                c.Name = Convert.ToString(r["Name"]);
                list.Add(c);
            }
            return list;
        }

        public void SetContactCustomer(long contactId, long customerId)
        {
            _db.ExecuteNonQuery("UPDATE CrmContact SET CustomerID = @c, RowVersion = RowVersion + 1 WHERE ContactID = @id AND IsDeleted = 0;",
                P("@c", customerId), P("@id", contactId));
        }

        public long InsertActivity(int companyId, int centerId, string kind, long? leadId, long? oppId, long? customerId,
            string subject, string body, string occurredAt, string user)
        {
            long newId = 0;
            _db.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO CrmActivity (CompanyID, CenterID, Kind, LeadID, OpportunityID, CustomerID, Subject, Body, OccurredAt, RowVersion, CreatedAt, CreatedBy)
VALUES (@c, @ctr, @k, @l, @o, @cust, @s, @b, @oc, 1, @oc, @u);", con, tr))
                {
                    cmd.Parameters.AddWithValue("@c", companyId);
                    cmd.Parameters.AddWithValue("@ctr", centerId);
                    cmd.Parameters.AddWithValue("@k", kind);
                    cmd.Parameters.AddWithValue("@l", leadId.HasValue ? (object)leadId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@o", oppId.HasValue ? (object)oppId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@cust", customerId.HasValue ? (object)customerId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@s", subject);
                    cmd.Parameters.AddWithValue("@b", (object)body ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@oc", occurredAt);
                    cmd.Parameters.AddWithValue("@u", user ?? "");
                    cmd.ExecuteNonQuery();
                }
                using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                    newId = Convert.ToInt64(id.ExecuteScalar());
            });
            return newId;
        }

        public long InsertTask(int companyId, int centerId, long? leadId, long? oppId, long? customerId,
            string title, string due, string assignee, string now, string user)
        {
            long newId = 0;
            _db.ExecuteInTransaction(delegate (SQLiteConnection con, SQLiteTransaction tr)
            {
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO CrmTask (CompanyID, CenterID, LeadID, OpportunityID, CustomerID, Title, DueDate, Status, Assignee, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, @ctr, @l, @o, @cust, @t, @d, @st, @a, 0, 1, @n, @n, @u, @u);", con, tr))
                {
                    cmd.Parameters.AddWithValue("@c", companyId);
                    cmd.Parameters.AddWithValue("@ctr", centerId);
                    cmd.Parameters.AddWithValue("@l", leadId.HasValue ? (object)leadId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@o", oppId.HasValue ? (object)oppId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@cust", customerId.HasValue ? (object)customerId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@t", title);
                    cmd.Parameters.AddWithValue("@d", (object)due ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@st", CrmCodes.TaskOpen);
                    cmd.Parameters.AddWithValue("@a", (object)assignee ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@n", now);
                    cmd.Parameters.AddWithValue("@u", user ?? "");
                    cmd.ExecuteNonQuery();
                }
                using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con, tr))
                    newId = Convert.ToInt64(id.ExecuteScalar());
            });
            return newId;
        }

        public void SetEntTaskId(long taskId, long entTaskId)
        {
            _db.ExecuteNonQuery("UPDATE CrmTask SET EntTaskID = @e WHERE TaskID = @id;", P("@e", entTaskId), P("@id", taskId));
        }

        public bool CompleteTask(long id, long rowVersion, string now, string user)
        {
            return _db.ExecuteNonQuery(@"
UPDATE CrmTask SET Status = @st, RowVersion = RowVersion + 1, UpdatedAt = @n, UpdatedBy = @u
WHERE TaskID = @id AND RowVersion = @rv AND IsDeleted = 0;",
                P("@st", CrmCodes.TaskDone), P("@n", now), P("@u", user ?? ""), P("@id", id), P("@rv", rowVersion)) == 1;
        }

        public DataRow GetTaskRow(long id)
        {
            DataTable t = _db.Query("SELECT * FROM CrmTask WHERE TaskID = @id AND IsDeleted = 0;", P("@id", id));
            return t.Rows.Count == 0 ? null : t.Rows[0];
        }

        public void AppendComm(int companyId, long? leadId, long? oppId, long? customerId,
            string eventType, string refTable, long? refId, string summary, string occurredAt, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO CrmCommunication (CompanyID, LeadID, OpportunityID, CustomerID, EventType, RefTable, RefID, Summary, OccurredAt, CreatedBy, IsDeleted)
VALUES (@c, @l, @o, @cust, @e, @rt, @rid, @s, @oc, @u, 0);",
                P("@c", companyId),
                P("@l", leadId.HasValue ? (object)leadId.Value : DBNull.Value),
                P("@o", oppId.HasValue ? (object)oppId.Value : DBNull.Value),
                P("@cust", customerId.HasValue ? (object)customerId.Value : DBNull.Value),
                P("@e", eventType), P("@rt", (object)refTable ?? DBNull.Value),
                P("@rid", refId.HasValue ? (object)refId.Value : DBNull.Value),
                P("@s", summary), P("@oc", occurredAt), P("@u", user ?? ""));
        }

        public long FindCustomerId(int companyId, string code)
        {
            object v = _db.ExecuteScalar(
                "SELECT CustomerID FROM SalCustomer WHERE CompanyID = @c AND Code = @code AND IsDeleted = 0;",
                P("@c", companyId), P("@code", code));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public bool QuoteMatches(long quoteId, int companyId, long customerId)
        {
            object v = _db.ExecuteScalar(
                "SELECT COUNT(1) FROM SalQuotation WHERE QuoteID = @id AND CompanyID = @c AND CustomerID = @cust AND IsDeleted = 0;",
                P("@id", quoteId), P("@c", companyId), P("@cust", customerId));
            return v != null && v != DBNull.Value && Convert.ToInt64(v) > 0;
        }

        public bool OrderMatches(long orderId, int companyId, long customerId)
        {
            object v = _db.ExecuteScalar(
                "SELECT COUNT(1) FROM SalOrder WHERE OrderID = @id AND CompanyID = @c AND CustomerID = @cust AND IsDeleted = 0;",
                P("@id", orderId), P("@c", companyId), P("@cust", customerId));
            return v != null && v != DBNull.Value && Convert.ToInt64(v) > 0;
        }

        public bool InvoiceMatches(long invoiceId, int companyId, long customerId)
        {
            object v = _db.ExecuteScalar(
                "SELECT COUNT(1) FROM SalInvoice WHERE InvoiceID = @id AND CompanyID = @c AND CustomerID = @cust AND IsDeleted = 0;",
                P("@id", invoiceId), P("@c", companyId), P("@cust", customerId));
            return v != null && v != DBNull.Value && Convert.ToInt64(v) > 0;
        }

        public long InvoiceAmount(long invoiceId)
        {
            object v = _db.ExecuteScalar("SELECT AmountMinor FROM SalInvoice WHERE InvoiceID = @id AND IsDeleted = 0;", P("@id", invoiceId));
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public IList<LeadPipelineRow> LeadPipeline(int companyId, int centerId)
        {
            List<LeadPipelineRow> list = new List<LeadPipelineRow>();
            DataTable t = _db.Query(@"
SELECT Status, Code, Name FROM CrmLead WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr)
ORDER BY Status, LeadID;",
                P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                LeadPipelineRow row = new LeadPipelineRow();
                row.Status = Convert.ToString(t.Rows[i]["Status"]);
                row.Code = Convert.ToString(t.Rows[i]["Code"]);
                row.Name = Convert.ToString(t.Rows[i]["Name"]);
                row.Count = 1;
                list.Add(row);
            }
            return list;
        }

        public IList<OpportunityPipelineRow> OpportunityPipeline(int companyId, int centerId)
        {
            List<OpportunityPipelineRow> list = new List<OpportunityPipelineRow>();
            DataTable t = _db.Query(@"
SELECT Stage, Status, Code, Name, ExpectedAmountMinor FROM CrmOpportunity
WHERE CompanyID = @c AND IsDeleted = 0 AND (@ctr = 0 OR CenterID = @ctr)
ORDER BY Stage, OpportunityID;",
                P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                OpportunityPipelineRow row = new OpportunityPipelineRow();
                row.Stage = Convert.ToString(t.Rows[i]["Stage"]);
                row.Status = Convert.ToString(t.Rows[i]["Status"]);
                row.Code = Convert.ToString(t.Rows[i]["Code"]);
                row.Name = Convert.ToString(t.Rows[i]["Name"]);
                row.ExpectedAmountMinor = Convert.ToInt64(t.Rows[i]["ExpectedAmountMinor"]);
                list.Add(row);
            }
            return list;
        }

        public IList<SalesFunnelRow> SalesFunnel(int companyId, int centerId)
        {
            List<SalesFunnelRow> list = new List<SalesFunnelRow>();
            DataTable t = _db.Query(@"
SELECT o.Stage, o.Code, o.Name, o.QuoteID, o.OrderID, o.InvoiceID, IFNULL(i.AmountMinor, 0) AS AmountMinor
FROM CrmOpportunity o
LEFT JOIN SalInvoice i ON i.InvoiceID = o.InvoiceID AND i.IsDeleted = 0
WHERE o.CompanyID = @c AND o.IsDeleted = 0 AND (@ctr = 0 OR o.CenterID = @ctr)
ORDER BY o.Stage, o.OpportunityID;",
                P("@c", companyId), P("@ctr", centerId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                DataRow r = t.Rows[i];
                SalesFunnelRow row = new SalesFunnelRow();
                row.Stage = Convert.ToString(r["Stage"]);
                row.Code = Convert.ToString(r["Code"]);
                row.Name = Convert.ToString(r["Name"]);
                row.QuoteId = r["QuoteID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["QuoteID"]);
                row.OrderId = r["OrderID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["OrderID"]);
                row.InvoiceId = r["InvoiceID"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["InvoiceID"]);
                row.InvoiceAmountMinor = Convert.ToInt64(r["AmountMinor"]);
                list.Add(row);
            }
            return list;
        }

        public IList<CustomerActivityRow> CustomerActivity(int companyId, long customerId)
        {
            List<CustomerActivityRow> list = new List<CustomerActivityRow>();
            DataTable t = _db.Query(@"
SELECT OccurredAt, EventType, Summary FROM CrmCommunication
WHERE CompanyID = @c AND CustomerID = @cust AND IsDeleted = 0
ORDER BY OccurredAt DESC, CommID DESC;",
                P("@c", companyId), P("@cust", customerId));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                CustomerActivityRow row = new CustomerActivityRow();
                row.OccurredAt = Convert.ToString(t.Rows[i]["OccurredAt"]);
                row.EventType = Convert.ToString(t.Rows[i]["EventType"]);
                row.Summary = Convert.ToString(t.Rows[i]["Summary"]);
                list.Add(row);
            }
            return list;
        }

        public IList<FollowUpRow> FollowUps(int companyId, int centerId, string asOf)
        {
            List<FollowUpRow> list = new List<FollowUpRow>();
            DataTable t = _db.Query(@"
SELECT Title, DueDate, Status FROM CrmTask
WHERE CompanyID = @c AND IsDeleted = 0 AND Status = @st AND (@ctr = 0 OR CenterID = @ctr)
  AND DueDate IS NOT NULL AND DueDate <= @asof
ORDER BY DueDate;",
                P("@c", companyId), P("@ctr", centerId), P("@st", CrmCodes.TaskOpen), P("@asof", asOf ?? ""));
            for (int i = 0; i < t.Rows.Count; i++)
            {
                FollowUpRow row = new FollowUpRow();
                row.Title = Convert.ToString(t.Rows[i]["Title"]);
                row.DueDate = Convert.ToString(t.Rows[i]["DueDate"]);
                row.Status = Convert.ToString(t.Rows[i]["Status"]);
                row.Overdue = string.CompareOrdinal(row.DueDate, asOf) < 0;
                list.Add(row);
            }
            return list;
        }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }
    }
}

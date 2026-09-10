using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Crm.Domain;
using CaseManagement.DAL;
using CaseManagement.Helpers;
using CaseManagement.Trade;

namespace CaseManagement.Crm.Infrastructure
{
    public static class CrmInitializer
    {
        public static void Ensure()
        {
            SchemaVersion.Ensure();
            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                CreateTables(con);
                Seed(con);
                TradeWorkflowSeed.Ensure(con, "CRM_LEAD", "سرنخ CRM", CrmCodes.EntityLead,
                    CrmPermissions.Create, CrmPermissions.Approve, CrmPermissions.Approve);
                TradeWorkflowSeed.Ensure(con, "CRM_OPP", "فرصت CRM", CrmCodes.EntityOpportunity,
                    CrmPermissions.Create, CrmPermissions.Approve, CrmPermissions.Approve);
            }
            SchemaVersion.SetIfNewer(SchemaVersion.ComponentCrm, 1, "CRM Foundation V1");
        }

        private static void CreateTables(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS CrmSetting (
  SettingID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  RequiresApproval INTEGER NOT NULL DEFAULT 1, DefaultCenterID INTEGER NOT NULL DEFAULT 0,
  NextLeadNumber INTEGER NOT NULL DEFAULT 1, NextOppNumber INTEGER NOT NULL DEFAULT 1,
  NumberPrefix TEXT NOT NULL DEFAULT 'CRM',
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_CrmSetting ON CrmSetting(CompanyID) WHERE IsDeleted = 0;");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS CrmLead (
  LeadID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  Code TEXT NOT NULL, Name TEXT NOT NULL, Status TEXT NOT NULL,
  Source TEXT NULL, Industry TEXT NULL, Phone TEXT NULL, Email TEXT NULL, Note TEXT NULL,
  OpportunityID INTEGER NULL, CustomerID INTEGER NULL, OwnerUser TEXT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_CrmLead ON CrmLead(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_CrmLead_Center ON CrmLead(CompanyID, CenterID, Status);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS CrmOpportunity (
  OpportunityID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  Code TEXT NOT NULL, Name TEXT NOT NULL, Status TEXT NOT NULL, Stage TEXT NOT NULL,
  LeadID INTEGER NULL, CustomerID INTEGER NULL,
  ExpectedAmountMinor INTEGER NOT NULL DEFAULT 0, ExpectedCloseDate TEXT NULL,
  QuoteID INTEGER NULL, OrderID INTEGER NULL, InvoiceID INTEGER NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE UNIQUE INDEX IF NOT EXISTS UX_CrmOpp ON CrmOpportunity(CompanyID, Code) WHERE IsDeleted = 0;");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_CrmOpp_Stage ON CrmOpportunity(CompanyID, Stage, Status);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_CrmOpp_Customer ON CrmOpportunity(CompanyID, CustomerID);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS CrmContact (
  ContactID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  LeadID INTEGER NULL, OpportunityID INTEGER NULL, CustomerID INTEGER NULL,
  Name TEXT NOT NULL, Phone TEXT NULL, Email TEXT NULL, Role TEXT NULL, SalContactID INTEGER NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_CrmContact_Lead ON CrmContact(LeadID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_CrmContact_Opp ON CrmContact(OpportunityID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_CrmContact_Customer ON CrmContact(CustomerID);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS CrmActivity (
  ActivityID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  Kind TEXT NOT NULL, LeadID INTEGER NULL, OpportunityID INTEGER NULL, CustomerID INTEGER NULL, ContactID INTEGER NULL,
  Subject TEXT NOT NULL, Body TEXT NULL, OccurredAt TEXT NOT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_CrmActivity_Customer ON CrmActivity(CompanyID, CustomerID, OccurredAt);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS CrmTask (
  TaskID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL, CenterID INTEGER NOT NULL,
  LeadID INTEGER NULL, OpportunityID INTEGER NULL, CustomerID INTEGER NULL,
  Title TEXT NOT NULL, DueDate TEXT NULL, Status TEXT NOT NULL, Assignee TEXT NULL, EntTaskID INTEGER NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0, RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL, UpdatedBy TEXT NOT NULL
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_CrmTask_Due ON CrmTask(CompanyID, Status, DueDate);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS CrmCommunication (
  CommID INTEGER PRIMARY KEY, CompanyID INTEGER NOT NULL,
  LeadID INTEGER NULL, OpportunityID INTEGER NULL, CustomerID INTEGER NULL,
  EventType TEXT NOT NULL, RefTable TEXT NULL, RefID INTEGER NULL,
  Summary TEXT NOT NULL, OccurredAt TEXT NOT NULL, CreatedBy TEXT NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0
);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_CrmComm_Opp ON CrmCommunication(OpportunityID, OccurredAt);");
        }

        private static void Seed(SQLiteConnection con)
        {
            int c = LedgerCodes.DefaultCompanyId;
            string now = LedgerTime.UtcNow(DateTime.UtcNow);
            string u = LedgerCodes.SystemUser;
            if (Scalar(con, "SELECT COUNT(1) FROM CrmSetting WHERE CompanyID = " + c) == 0)
            {
                Exec(con, @"
INSERT INTO CrmSetting (CompanyID, RequiresApproval, DefaultCenterID, NextLeadNumber, NextOppNumber, NumberPrefix, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@c, 1, 0, 1, 1, 'CRM', 0, 1, @n, @n, @u, @u);",
                    P("@c", c), P("@n", now), P("@u", u));
            }
        }

        private static long Scalar(SQLiteConnection con, string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                object v = cmd.ExecuteScalar();
                if (v == null || v == DBNull.Value) return 0;
                return Convert.ToInt64(v);
            }
        }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        private static void Exec(SQLiteConnection con, string sql, params SQLiteParameter[] p)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                if (p != null && p.Length > 0) cmd.Parameters.AddRange(p);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

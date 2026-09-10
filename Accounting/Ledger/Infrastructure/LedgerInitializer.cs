using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting.Ledger.Infrastructure
{
    /// <summary>
    /// Additive Gl* schema + idempotent seeds. Does not modify Acc* or charity tables.
    /// </summary>
    public static class LedgerInitializer
    {
        public const int AccountingSchemaVersion = 1;

        public static void Ensure()
        {
            SchemaVersion.Ensure();

            using (SQLiteConnection con = new DatabaseHelper().GetConnection())
            {
                con.Open();
                CreateTables(con);
                CreateIndexes(con);
                Seed(con);
            }

            SchemaVersion.SetIfNewer(
                SchemaVersion.ComponentAccounting,
                AccountingSchemaVersion,
                "Phase 2: General Ledger foundation (Gl*)");
        }

        private static void CreateTables(SQLiteConnection con)
        {
            Exec(con, @"
CREATE TABLE IF NOT EXISTS GlCompany (
  CompanyID INTEGER PRIMARY KEY,
  CenterID INTEGER NOT NULL,
  Code TEXT NOT NULL UNIQUE,
  Name TEXT NOT NULL,
  BaseCurrencyCode TEXT NOT NULL,
  MinorUnits INTEGER NOT NULL,
  IsActive INTEGER NOT NULL DEFAULT 1,
  IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL,
  DeletedBy TEXT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL,
  UpdatedBy TEXT NOT NULL
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS GlCurrency (
  CurrencyID INTEGER PRIMARY KEY,
  CompanyID INTEGER NOT NULL,
  CenterID INTEGER NOT NULL,
  CurrencyCode TEXT NOT NULL,
  Name TEXT NOT NULL,
  MinorUnits INTEGER NOT NULL,
  IsActive INTEGER NOT NULL DEFAULT 1,
  IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL,
  DeletedBy TEXT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL,
  UpdatedBy TEXT NOT NULL,
  UNIQUE (CompanyID, CurrencyCode)
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS GlExchangeRate (
  ExchangeRateID INTEGER PRIMARY KEY,
  CompanyID INTEGER NOT NULL,
  CenterID INTEGER NOT NULL,
  CurrencyCode TEXT NOT NULL,
  RateDate TEXT NOT NULL,
  RateToBaseMicros INTEGER NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL,
  DeletedBy TEXT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL,
  UpdatedBy TEXT NOT NULL,
  UNIQUE (CompanyID, CenterID, CurrencyCode, RateDate)
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS GlAccountType (
  AccountTypeID INTEGER PRIMARY KEY,
  CompanyID INTEGER NOT NULL,
  CenterID INTEGER NOT NULL,
  AccountTypeCode TEXT NOT NULL,
  Name TEXT NOT NULL,
  NormalBalance TEXT NOT NULL,
  Statement TEXT NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL,
  DeletedBy TEXT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL,
  UpdatedBy TEXT NOT NULL,
  UNIQUE (CompanyID, AccountTypeCode)
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS GlFiscalYear (
  FiscalYearID INTEGER PRIMARY KEY,
  CompanyID INTEGER NOT NULL,
  CenterID INTEGER NOT NULL,
  Code TEXT NOT NULL,
  Name TEXT NOT NULL,
  CalendarType TEXT NOT NULL,
  StartDate TEXT NOT NULL,
  EndDate TEXT NOT NULL,
  Status TEXT NOT NULL,
  ClosedAt TEXT NULL,
  ClosedBy TEXT NULL,
  LockedAt TEXT NULL,
  LockedBy TEXT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL,
  DeletedBy TEXT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL,
  UpdatedBy TEXT NOT NULL,
  UNIQUE (CompanyID, Code)
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS GlFiscalPeriod (
  FiscalPeriodID INTEGER PRIMARY KEY,
  CompanyID INTEGER NOT NULL,
  CenterID INTEGER NOT NULL,
  FiscalYearID INTEGER NOT NULL,
  PeriodNo INTEGER NOT NULL,
  Name TEXT NOT NULL,
  StartDate TEXT NOT NULL,
  EndDate TEXT NOT NULL,
  Status TEXT NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL,
  DeletedBy TEXT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL,
  UpdatedBy TEXT NOT NULL,
  UNIQUE (FiscalYearID, PeriodNo)
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS GlAccount (
  AccountID INTEGER PRIMARY KEY,
  CompanyID INTEGER NOT NULL,
  CenterID INTEGER NOT NULL,
  AccountCode TEXT NOT NULL,
  AccountName TEXT NOT NULL,
  AccountTypeCode TEXT NOT NULL,
  ParentAccountID INTEGER NULL,
  Level INTEGER NOT NULL,
  IsLeaf INTEGER NOT NULL,
  AllowPosting INTEGER NOT NULL,
  IsActive INTEGER NOT NULL DEFAULT 1,
  IsContra INTEGER NOT NULL DEFAULT 0,
  ControlCurrencyCode TEXT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL,
  DeletedBy TEXT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL,
  UpdatedBy TEXT NOT NULL,
  UNIQUE (CompanyID, AccountCode)
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS GlJournal (
  JournalID INTEGER PRIMARY KEY,
  CompanyID INTEGER NOT NULL,
  CenterID INTEGER NOT NULL,
  FiscalYearID INTEGER NOT NULL,
  FiscalPeriodID INTEGER NOT NULL,
  JournalNumber TEXT NOT NULL,
  JournalSource TEXT NOT NULL,
  SourceModule TEXT NULL,
  SourceDocumentType TEXT NULL,
  SourceDocumentID INTEGER NULL,
  PostingDate TEXT NOT NULL,
  DocumentDate TEXT NULL,
  ReferenceNumber TEXT NULL,
  Description TEXT NULL,
  Status TEXT NOT NULL,
  ReversesJournalID INTEGER NULL,
  ClientJournalGuid TEXT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL,
  DeletedBy TEXT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL,
  UpdatedBy TEXT NOT NULL,
  ApprovedAt TEXT NULL,
  ApprovedBy TEXT NULL,
  PostedAt TEXT NULL,
  PostedBy TEXT NULL,
  UNIQUE (CompanyID, FiscalYearID, JournalNumber)
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS GlJournalLine (
  JournalLineID INTEGER PRIMARY KEY,
  JournalID INTEGER NOT NULL,
  CompanyID INTEGER NOT NULL,
  CenterID INTEGER NOT NULL,
  LineNo INTEGER NOT NULL,
  AccountID INTEGER NOT NULL,
  DebitMinor INTEGER NOT NULL DEFAULT 0,
  CreditMinor INTEGER NOT NULL DEFAULT 0,
  CurrencyCode TEXT NOT NULL,
  ExchangeRateMicros INTEGER NOT NULL,
  DebitBaseMinor INTEGER NOT NULL DEFAULT 0,
  CreditBaseMinor INTEGER NOT NULL DEFAULT 0,
  Description TEXT NULL,
  CostCenterID INTEGER NULL,
  ProjectID INTEGER NULL,
  PartyID INTEGER NULL,
  FundID INTEGER NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL,
  UpdatedBy TEXT NOT NULL,
  UNIQUE (JournalID, LineNo)
);");

            Exec(con, @"
CREATE TABLE IF NOT EXISTS GlLedgerSetting (
  LedgerSettingID INTEGER PRIMARY KEY,
  CompanyID INTEGER NOT NULL,
  CenterID INTEGER NOT NULL,
  RequiresJournalApproval INTEGER NOT NULL DEFAULT 1,
  NextJournalNumber INTEGER NOT NULL DEFAULT 1,
  NumberPrefix TEXT NOT NULL DEFAULT 'JE',
  IsDeleted INTEGER NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL,
  DeletedBy TEXT NULL,
  RowVersion INTEGER NOT NULL DEFAULT 1,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  CreatedBy TEXT NOT NULL,
  UpdatedBy TEXT NOT NULL,
  UNIQUE (CompanyID)
);");
        }

        private static void CreateIndexes(SQLiteConnection con)
        {
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlJournal_CompanyCenterDate ON GlJournal(CompanyID, CenterID, PostingDate);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlJournal_StatusPeriod ON GlJournal(Status, FiscalPeriodID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlJournal_Source ON GlJournal(CompanyID, SourceModule, SourceDocumentType, SourceDocumentID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlJournal_IsDeleted ON GlJournal(IsDeleted);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlJournalLine_CoCenterAccount ON GlJournalLine(CompanyID, CenterID, AccountID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlJournalLine_AccountJournal ON GlJournalLine(AccountID, JournalID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlJournalLine_CostCenter ON GlJournalLine(CostCenterID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlJournalLine_Project ON GlJournalLine(ProjectID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlJournalLine_Currency ON GlJournalLine(CurrencyCode);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlAccount_Parent ON GlAccount(CompanyID, CenterID, ParentAccountID);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlAccount_Type ON GlAccount(CompanyID, AccountTypeCode);");
            Exec(con, "CREATE INDEX IF NOT EXISTS IX_GlAccount_IsDeleted ON GlAccount(IsDeleted);");
            Exec(con, @"
CREATE UNIQUE INDEX IF NOT EXISTS UX_GlJournal_ClientGuid
ON GlJournal(ClientJournalGuid)
WHERE ClientJournalGuid IS NOT NULL;");
            Exec(con, @"
CREATE UNIQUE INDEX IF NOT EXISTS UX_GlJournal_SourceDoc
ON GlJournal(CompanyID, SourceModule, SourceDocumentType, SourceDocumentID)
WHERE SourceModule IS NOT NULL AND SourceDocumentType IS NOT NULL AND SourceDocumentID IS NOT NULL;");
        }

        private static void Seed(SQLiteConnection con)
        {
            string now = LedgerTime.UtcNow(DateTime.UtcNow);
            string user = LedgerCodes.SystemUser;
            int companyId = LedgerCodes.DefaultCompanyId;

            if (ScalarInt(con, "SELECT COUNT(1) FROM GlCompany WHERE CompanyID = " + companyId) == 0)
            {
                string name = "Organization";
                try
                {
                    string org = SettingsHelper.Get(SettingsHelper.OrgName);
                    if (!string.IsNullOrWhiteSpace(org)) name = org;
                }
                catch { }

                Exec(con, @"
INSERT INTO GlCompany (CompanyID, CenterID, Code, Name, BaseCurrencyCode, MinorUnits, IsActive,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@id, 0, 'DEFAULT', @name, @ccy, 2, 1, 0, 1, @now, @now, @user, @user);",
                    P("@id", companyId), P("@name", name), P("@ccy", LedgerCodes.BaseCurrency),
                    P("@now", now), P("@user", user));
            }

            SeedAccountType(con, companyId, now, user, LedgerCodes.TypeAsset, "Asset", LedgerCodes.BalanceDebit, LedgerCodes.StatementBs);
            SeedAccountType(con, companyId, now, user, LedgerCodes.TypeLiability, "Liability", LedgerCodes.BalanceCredit, LedgerCodes.StatementBs);
            SeedAccountType(con, companyId, now, user, LedgerCodes.TypeEquity, "Equity", LedgerCodes.BalanceCredit, LedgerCodes.StatementBs);
            SeedAccountType(con, companyId, now, user, LedgerCodes.TypeRevenue, "Revenue", LedgerCodes.BalanceCredit, LedgerCodes.StatementPl);
            SeedAccountType(con, companyId, now, user, LedgerCodes.TypeExpense, "Expense", LedgerCodes.BalanceDebit, LedgerCodes.StatementPl);

            SeedCurrency(con, companyId, now, user, "AFN", "Afghani", 2);
            SeedCurrency(con, companyId, now, user, "USD", "US Dollar", 2);
            SeedCurrency(con, companyId, now, user, "EUR", "Euro", 2);

            if (ScalarInt(con, "SELECT COUNT(1) FROM GlLedgerSetting WHERE CompanyID = " + companyId) == 0)
            {
                Exec(con, @"
INSERT INTO GlLedgerSetting (CompanyID, CenterID, RequiresJournalApproval, NextJournalNumber, NumberPrefix,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@cid, 0, 1, 1, 'JE', 0, 1, @now, @now, @user, @user);",
                    P("@cid", companyId), P("@now", now), P("@user", user));
            }

            if (ScalarInt(con, "SELECT COUNT(1) FROM GlAccount WHERE CompanyID = " + companyId) == 0)
                SeedChartOfAccounts(con, companyId, now, user);
        }

        private static void SeedAccountType(SQLiteConnection con, int companyId, string now, string user,
            string code, string name, string balance, string statement)
        {
            Exec(con, @"
INSERT OR IGNORE INTO GlAccountType (CompanyID, CenterID, AccountTypeCode, Name, NormalBalance, Statement,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@cid, 0, @code, @name, @bal, @st, 0, 1, @now, @now, @user, @user);",
                P("@cid", companyId), P("@code", code), P("@name", name), P("@bal", balance),
                P("@st", statement), P("@now", now), P("@user", user));
        }

        private static void SeedCurrency(SQLiteConnection con, int companyId, string now, string user,
            string code, string name, int minor)
        {
            Exec(con, @"
INSERT OR IGNORE INTO GlCurrency (CompanyID, CenterID, CurrencyCode, Name, MinorUnits, IsActive,
  IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@cid, 0, @code, @name, @minor, 1, 0, 1, @now, @now, @user, @user);",
                P("@cid", companyId), P("@code", code), P("@name", name), P("@minor", minor),
                P("@now", now), P("@user", user));
        }

        private static void SeedChartOfAccounts(SQLiteConnection con, int companyId, string now, string user)
        {
            long a1000 = InsertAccount(con, companyId, "1000", "دارایی‌ها", LedgerCodes.TypeAsset, null, 1, false, now, user);
            long a1100 = InsertAccount(con, companyId, "1100", "نقد و بانک", LedgerCodes.TypeAsset, a1000, 2, false, now, user);
            InsertAccount(con, companyId, "1101", "صندوق", LedgerCodes.TypeAsset, a1100, 3, true, now, user);
            InsertAccount(con, companyId, "1102", "بانک", LedgerCodes.TypeAsset, a1100, 3, true, now, user);
            InsertAccount(con, companyId, "1200", "دریافتنی (AR)", LedgerCodes.TypeAsset, a1000, 2, true, now, user);
            InsertAccount(con, companyId, "1300", "موجودی کالا", LedgerCodes.TypeAsset, a1000, 2, true, now, user);

            long a2000 = InsertAccount(con, companyId, "2000", "بدهی‌ها", LedgerCodes.TypeLiability, null, 1, false, now, user);
            InsertAccount(con, companyId, "2100", "پرداختنی (AP)", LedgerCodes.TypeLiability, a2000, 2, true, now, user);

            long a3000 = InsertAccount(con, companyId, "3000", "حقوق مالکانه", LedgerCodes.TypeEquity, null, 1, false, now, user);
            InsertAccount(con, companyId, "3100", "سرمایه", LedgerCodes.TypeEquity, a3000, 2, true, now, user);
            InsertAccount(con, companyId, "3200", "سود انباشته", LedgerCodes.TypeEquity, a3000, 2, true, now, user);

            long a4000 = InsertAccount(con, companyId, "4000", "درآمدها", LedgerCodes.TypeRevenue, null, 1, false, now, user);
            InsertAccount(con, companyId, "4100", "درآمد عملیاتی", LedgerCodes.TypeRevenue, a4000, 2, true, now, user);

            long a5000 = InsertAccount(con, companyId, "5000", "هزینه‌ها", LedgerCodes.TypeExpense, null, 1, false, now, user);
            InsertAccount(con, companyId, "5100", "حقوق", LedgerCodes.TypeExpense, a5000, 2, true, now, user);
            InsertAccount(con, companyId, "5200", "عملیاتی", LedgerCodes.TypeExpense, a5000, 2, true, now, user);
        }

        private static long InsertAccount(SQLiteConnection con, int companyId, string code, string name,
            string type, long? parentId, int level, bool leaf, string now, string user)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO GlAccount (CompanyID, CenterID, AccountCode, AccountName, AccountTypeCode, ParentAccountID,
  Level, IsLeaf, AllowPosting, IsActive, IsContra, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@cid, 0, @code, @name, @type, @parent, @lvl, @leaf, @post, 1, 0, 0, 1, @now, @now, @user, @user);", con))
            {
                cmd.Parameters.AddWithValue("@cid", companyId);
                cmd.Parameters.AddWithValue("@code", code);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@parent", parentId.HasValue ? (object)parentId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@lvl", level);
                cmd.Parameters.AddWithValue("@leaf", leaf ? 1 : 0);
                cmd.Parameters.AddWithValue("@post", leaf ? 1 : 0);
                cmd.Parameters.AddWithValue("@now", now);
                cmd.Parameters.AddWithValue("@user", user);
                cmd.ExecuteNonQuery();
            }
            using (SQLiteCommand id = new SQLiteCommand("SELECT last_insert_rowid();", con))
                return Convert.ToInt64(id.ExecuteScalar());
        }

        private static int ScalarInt(SQLiteConnection con, string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                object v = cmd.ExecuteScalar();
                if (v == null || v == DBNull.Value) return 0;
                return Convert.ToInt32(v);
            }
        }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        private static void Exec(SQLiteConnection con, string sql, params SQLiteParameter[] parameters)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, con))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

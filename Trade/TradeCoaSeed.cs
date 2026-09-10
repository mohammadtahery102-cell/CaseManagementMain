using System;
using System.Data.SQLite;
using CaseManagement.Accounting.Ledger.Domain;

namespace CaseManagement.Trade
{
    public static class TradeCoaSeed
    {
        public static void EnsureLeaf(SQLiteConnection con, int companyId, string code, string name, string type, string parentCode)
        {
            string now = LedgerTime.UtcNow(DateTime.UtcNow);
            using (SQLiteCommand exists = new SQLiteCommand(
                "SELECT COUNT(1) FROM GlAccount WHERE CompanyID = @c AND AccountCode = @code AND IsDeleted = 0;", con))
            {
                exists.Parameters.AddWithValue("@c", companyId);
                exists.Parameters.AddWithValue("@code", code);
                object v = exists.ExecuteScalar();
                if (v != null && v != DBNull.Value && Convert.ToInt64(v) > 0) return;
            }
            long parentId = 0;
            using (SQLiteCommand p = new SQLiteCommand(
                "SELECT AccountID FROM GlAccount WHERE CompanyID = @c AND AccountCode = @p AND IsDeleted = 0 LIMIT 1;", con))
            {
                p.Parameters.AddWithValue("@c", companyId);
                p.Parameters.AddWithValue("@p", parentCode);
                object v = p.ExecuteScalar();
                if (v != null && v != DBNull.Value) parentId = Convert.ToInt64(v);
            }
            using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO GlAccount (CompanyID, CenterID, AccountCode, AccountName, AccountTypeCode, ParentAccountID,
  Level, IsLeaf, AllowPosting, IsActive, IsContra, IsDeleted, RowVersion, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES (@cid, 0, @code, @name, @type, @parent, 2, 1, 1, 1, 0, 0, 1, @now, @now, @user, @user);", con))
            {
                cmd.Parameters.AddWithValue("@cid", companyId);
                cmd.Parameters.AddWithValue("@code", code);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@parent", parentId > 0 ? (object)parentId : DBNull.Value);
                cmd.Parameters.AddWithValue("@now", now);
                cmd.Parameters.AddWithValue("@user", LedgerCodes.SystemUser);
                cmd.ExecuteNonQuery();
            }
        }

        public static long AccountId(SQLiteConnection con, int companyId, string code)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(
                "SELECT AccountID FROM GlAccount WHERE CompanyID = @c AND AccountCode = @code AND IsDeleted = 0 LIMIT 1;", con))
            {
                cmd.Parameters.AddWithValue("@c", companyId);
                cmd.Parameters.AddWithValue("@code", code);
                object v = cmd.ExecuteScalar();
                if (v == null || v == DBNull.Value) return 0;
                return Convert.ToInt64(v);
            }
        }
    }
}

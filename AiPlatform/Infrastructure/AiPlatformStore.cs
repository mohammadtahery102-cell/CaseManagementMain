using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CaseManagement.AiPlatform.Domain;
using CaseManagement.DAL;

namespace CaseManagement.AiPlatform.Infrastructure
{
    public class AiPlatformStore
    {
        private readonly DatabaseHelper _db;
        public AiPlatformStore() : this(new DatabaseHelper()) { }
        public AiPlatformStore(DatabaseHelper db) { _db = db; }

        public AiSetting GetSetting(int companyId)
        {
            DataTable t = _db.Query("SELECT * FROM AipSetting WHERE CompanyID = @c AND IsDeleted = 0;", P("@c", companyId));
            if (t.Rows.Count == 0) return null;
            DataRow r = t.Rows[0];
            return new AiSetting
            {
                SettingId = Convert.ToInt64(r["SettingID"]),
                CompanyId = Convert.ToInt32(r["CompanyID"]),
                Provider = Convert.ToString(r["Provider"]),
                Endpoint = Convert.ToString(r["Endpoint"]),
                Model = Convert.ToString(r["Model"]),
                ApiKey = Convert.ToString(r["ApiKey"]),
                TimeoutMs = Convert.ToInt32(r["TimeoutMs"]),
                MaxTokens = Convert.ToInt32(r["MaxTokens"]),
                Temperature = Convert.ToDouble(r["Temperature"]),
                LicenseLevel = Convert.ToString(r["LicenseLevel"]),
                RowVersion = Convert.ToInt64(r["RowVersion"])
            };
        }

        public bool SaveSetting(AiSetting s, string now, string user)
        {
            if (s == null) return false;
            int n = _db.ExecuteNonQuery(@"
UPDATE AipSetting SET Provider=@p, Endpoint=@e, Model=@m, ApiKey=@k, TimeoutMs=@t, MaxTokens=@mt,
  Temperature=@tmp, LicenseLevel=@lic, RowVersion=RowVersion+1, UpdatedAt=@n, UpdatedBy=@u
WHERE SettingID=@id AND CompanyID=@c AND IsDeleted=0 AND RowVersion=@rv;",
                P("@p", s.Provider ?? AiProviderNames.None),
                P("@e", s.Endpoint ?? ""),
                P("@m", s.Model ?? ""),
                P("@k", s.ApiKey ?? ""),
                P("@t", s.TimeoutMs),
                P("@mt", s.MaxTokens),
                P("@tmp", s.Temperature),
                P("@lic", s.LicenseLevel ?? AiLicenseLevels.Free),
                P("@n", now),
                P("@u", user ?? ""),
                P("@id", s.SettingId),
                P("@c", s.CompanyId),
                P("@rv", s.RowVersion));
            return n > 0;
        }

        public long InsertConversation(int companyId, int centerId, int userId, string title, string now, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO AipConversation (CompanyID, CenterID, UserID, Title, IsDeleted, CreatedAt, CreatedBy)
VALUES (@c,@ctr,@u,@t,0,@n,@by);",
                P("@c", companyId), P("@ctr", centerId), P("@u", userId),
                P("@t", title ?? "گفتگو"), P("@n", now), P("@by", user ?? ""));
            object id = _db.ExecuteScalar(
                "SELECT MAX(ConversationID) FROM AipConversation WHERE CompanyID=@c AND UserID=@u AND CreatedAt=@n;",
                P("@c", companyId), P("@u", userId), P("@n", now));
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt64(id);
        }

        public AiConversation GetConversation(long id, int companyId)
        {
            DataTable t = _db.Query(
                "SELECT * FROM AipConversation WHERE ConversationID=@id AND CompanyID=@c AND IsDeleted=0;",
                P("@id", id), P("@c", companyId));
            if (t.Rows.Count == 0) return null;
            DataRow r = t.Rows[0];
            return new AiConversation
            {
                ConversationId = Convert.ToInt64(r["ConversationID"]),
                CompanyId = Convert.ToInt32(r["CompanyID"]),
                CenterId = Convert.ToInt32(r["CenterID"]),
                UserId = Convert.ToInt32(r["UserID"]),
                Title = Convert.ToString(r["Title"]),
                CreatedAt = Convert.ToString(r["CreatedAt"])
            };
        }

        public void InsertMessage(long conversationId, int companyId, string role, string body, string provider, string now, string user)
        {
            _db.ExecuteNonQuery(@"
INSERT INTO AipMessage (ConversationID, CompanyID, Role, Body, Provider, CreatedAt, CreatedBy)
VALUES (@cid,@c,@r,@b,@p,@n,@u);",
                P("@cid", conversationId), P("@c", companyId), P("@r", role ?? ""),
                P("@b", body ?? ""), P("@p", provider ?? ""), P("@n", now), P("@u", user ?? ""));
        }

        public IList<AiHistoryMessage> ListMessages(long conversationId)
        {
            DataTable t = _db.Query(
                "SELECT Role, Body, CreatedAt FROM AipMessage WHERE ConversationID=@id ORDER BY MessageID;",
                P("@id", conversationId));
            List<AiHistoryMessage> list = new List<AiHistoryMessage>();
            for (int i = 0; i < t.Rows.Count; i++)
            {
                DataRow r = t.Rows[i];
                list.Add(new AiHistoryMessage
                {
                    Role = Convert.ToString(r["Role"]),
                    Body = Convert.ToString(r["Body"]),
                    CreatedAt = Convert.ToString(r["CreatedAt"])
                });
            }
            return list;
        }

        public void InsertAudit(int companyId, int userId, string userName, string prompt, string provider,
            string timestamp, int inputTokens, int outputTokens, string status)
        {
            string clipped = prompt ?? "";
            if (clipped.Length > 2000) clipped = clipped.Substring(0, 2000);
            _db.ExecuteNonQuery(@"
INSERT INTO AipAuditLog (CompanyID, UserID, UserName, Prompt, Provider, TimestampUtc, InputTokens, OutputTokens, Status)
VALUES (@c,@uid,@un,@p,@pr,@ts,@in,@out,@st);",
                P("@c", companyId), P("@uid", userId), P("@un", userName ?? ""),
                P("@p", clipped), P("@pr", provider ?? ""), P("@ts", timestamp),
                P("@in", inputTokens), P("@out", outputTokens), P("@st", status ?? ""));
        }

        public long Count(string sql, params SQLiteParameter[] p)
        {
            object v = _db.ExecuteScalar(sql, p);
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        public DataTable Query(string sql, params SQLiteParameter[] p)
        {
            return _db.Query(sql, p);
        }

        private static SQLiteParameter P(string n, object v)
        {
            return new SQLiteParameter(n, v ?? DBNull.Value);
        }
    }
}

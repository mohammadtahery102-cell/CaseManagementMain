using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Text;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.AiPlatform.Domain;
using CaseManagement.AiPlatform.Infrastructure;
using CaseManagement.Trade;

namespace CaseManagement.AiPlatform.Application
{
    public sealed class AiLicenseService
    {
        private readonly AiPlatformStore _store;
        public AiLicenseService() : this(new AiPlatformStore()) { }
        public AiLicenseService(AiPlatformStore store) { _store = store; }

        public AiSetting CompanySetting(int companyId)
        {
            AiSetting s = _store.GetSetting(companyId);
            if (s == null) return null;
            return new AiSetting
            {
                SettingId = s.SettingId,
                CompanyId = s.CompanyId,
                Provider = s.Provider,
                Endpoint = s.Endpoint,
                Model = s.Model,
                ApiKey = "",
                TimeoutMs = s.TimeoutMs,
                MaxTokens = s.MaxTokens,
                Temperature = s.Temperature,
                LicenseLevel = s.LicenseLevel,
                RowVersion = s.RowVersion
            };
        }

        public int CompanyRank(int companyId)
        {
            AiSetting s = _store.GetSetting(companyId);
            return s == null ? 0 : AiLicenseLevels.Rank(s.LicenseLevel);
        }

        public int UserRank(ILedgerIdentity identity)
        {
            if (identity == null) return 0;
            if (identity.HasPermission(AiPermissions.LicenseExecutive)) return 3;
            if (identity.HasPermission(AiPermissions.LicensePro)) return 2;
            if (identity.HasPermission(AiPermissions.LicenseFree)) return 1;
            return 0;
        }

        public int EffectiveRank(ILedgerIdentity identity)
        {
            if (identity == null) return 0;
            int company = CompanyRank(identity.CompanyId);
            int user = UserRank(identity);
            return company < user ? company : user;
        }

        public string EffectiveLevel(ILedgerIdentity identity)
        {
            return AiLicenseLevels.FromRank(EffectiveRank(identity));
        }

        public bool CanView(ILedgerIdentity identity)
        {
            return identity != null && identity.HasPermission(AiPermissions.View) && EffectiveRank(identity) >= 1;
        }

        public bool CanAnalytics(ILedgerIdentity identity)
        {
            return CanView(identity) && identity.HasPermission(AiPermissions.Analytics) && EffectiveRank(identity) >= 2;
        }

        public bool CanReports(ILedgerIdentity identity)
        {
            return CanAnalytics(identity);
        }

        public bool CanChat(ILedgerIdentity identity)
        {
            return CanView(identity) && identity.HasPermission(AiPermissions.Chat) && EffectiveRank(identity) >= 2;
        }

        public bool CanExecutive(ILedgerIdentity identity)
        {
            return CanView(identity) && identity.HasPermission(AiPermissions.Executive) && EffectiveRank(identity) >= 3;
        }

        public bool CanAlerts(ILedgerIdentity identity)
        {
            return CanExecutive(identity);
        }

        public bool CanForecast(ILedgerIdentity identity)
        {
            return CanExecutive(identity);
        }

        public bool CanSettings(ILedgerIdentity identity)
        {
            return identity != null && identity.HasPermission(AiPermissions.Settings);
        }
    }

    public abstract class AiReadService
    {
        protected readonly AiPlatformStore Store;
        protected readonly AiLicenseService License;
        protected AiReadService(AiPlatformStore store, AiLicenseService license)
        {
            Store = store ?? new AiPlatformStore();
            License = license ?? new AiLicenseService(Store);
        }

        protected static SQLiteParameter P(string n, object v) { return new SQLiteParameter(n, v ?? DBNull.Value); }

        protected long Scalar(string sql, params SQLiteParameter[] p)
        {
            return Store.Count(sql, p);
        }

        protected static string N(long v)
        {
            return v.ToString("N0", CultureInfo.InvariantCulture);
        }

        protected static AiMetricRow Row(string label, string value)
        {
            return new AiMetricRow { Label = label, Value = value };
        }

        protected static AiAnalysisReport Denied()
        {
            return new AiAnalysisReport
            {
                Title = "دسترسی ندارد",
                Rows = new List<AiMetricRow> { Row("وضعیت", "مجوز یا سطح لایسنس کافی نیست.") }
            };
        }
    }

    public sealed class SalesAnalysisService : AiReadService
    {
        public SalesAnalysisService() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public SalesAnalysisService(AiPlatformStore store, AiLicenseService license) : base(store, license) { }

        public AiAnalysisReport Analyze(ILedgerIdentity identity)
        {
            if (!License.CanAnalytics(identity)) return Denied();
            return Build(identity);
        }

        public AiAnalysisReport Build(ILedgerIdentity identity)
        {
            return AiSmartAnalytics.Sales(Store, identity);
        }
    }

    public sealed class PurchaseAnalysisService : AiReadService
    {
        public PurchaseAnalysisService() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public PurchaseAnalysisService(AiPlatformStore store, AiLicenseService license) : base(store, license) { }

        public AiAnalysisReport Analyze(ILedgerIdentity identity)
        {
            if (!License.CanAnalytics(identity)) return Denied();
            return Build(identity);
        }

        public AiAnalysisReport Build(ILedgerIdentity identity)
        {
            return AiSmartAnalytics.Purchase(Store, identity);
        }
    }

    public sealed class InventoryAnalysisService : AiReadService
    {
        public InventoryAnalysisService() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public InventoryAnalysisService(AiPlatformStore store, AiLicenseService license) : base(store, license) { }

        public AiAnalysisReport Analyze(ILedgerIdentity identity)
        {
            if (!License.CanAnalytics(identity)) return Denied();
            return Build(identity);
        }

        public AiAnalysisReport Build(ILedgerIdentity identity)
        {
            return AiSmartAnalytics.Inventory(Store, identity);
        }
    }

    public sealed class FinancialAnalysisService : AiReadService
    {
        public FinancialAnalysisService() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public FinancialAnalysisService(AiPlatformStore store, AiLicenseService license) : base(store, license) { }

        public AiAnalysisReport Analyze(ILedgerIdentity identity)
        {
            if (!License.CanAnalytics(identity)) return Denied();
            return Build(identity);
        }

        public AiAnalysisReport Build(ILedgerIdentity identity)
        {
            return AiSmartAnalytics.Financial(Store, identity);
        }

        public long CashPosition(int companyId)
        {
            return Scalar(@"
SELECT IFNULL(SUM(l.DebitBaseMinor - l.CreditBaseMinor),0)
FROM GlJournalLine l
JOIN GlJournal j ON j.JournalID = l.JournalID
JOIN GlAccount a ON a.AccountID = l.AccountID
WHERE l.CompanyID=@c AND j.Status=@s AND j.IsDeleted=0 AND a.IsDeleted=0
  AND a.AccountCode LIKE '11%';",
                P("@c", companyId), P("@s", LedgerCodes.JournalPosted));
        }
    }

    public sealed class CustomerAnalysisService : AiReadService
    {
        public CustomerAnalysisService() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public CustomerAnalysisService(AiPlatformStore store, AiLicenseService license) : base(store, license) { }

        public AiAnalysisReport Analyze(ILedgerIdentity identity)
        {
            if (!License.CanAnalytics(identity)) return Denied();
            return Build(identity);
        }

        public AiAnalysisReport Build(ILedgerIdentity identity)
        {
            return AiSmartAnalytics.Customers(Store, identity);
        }
    }

    public sealed class ExecutiveAnalysisService : AiReadService
    {
        public ExecutiveAnalysisService() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public ExecutiveAnalysisService(AiPlatformStore store, AiLicenseService license) : base(store, license) { }

        public AiAnalysisReport Analyze(ILedgerIdentity identity)
        {
            return new AiForecastService(Store, License).Forecast(identity);
        }
    }

    public sealed class AiDashboardService
    {
        private readonly AiLicenseService _license;
        private readonly SalesAnalysisService _sales;
        private readonly PurchaseAnalysisService _purchase;
        private readonly InventoryAnalysisService _inventory;
        private readonly FinancialAnalysisService _financial;
        private readonly CustomerAnalysisService _customer;
        private readonly ExecutiveAnalysisService _exec;
        private readonly AiPlatformStore _store;

        public AiDashboardService()
        {
            _store = new AiPlatformStore();
            _license = new AiLicenseService(_store);
            _sales = new SalesAnalysisService(_store, _license);
            _purchase = new PurchaseAnalysisService(_store, _license);
            _inventory = new InventoryAnalysisService(_store, _license);
            _financial = new FinancialAnalysisService(_store, _license);
            _customer = new CustomerAnalysisService(_store, _license);
            _exec = new ExecutiveAnalysisService(_store, _license);
        }

        public AiDashboardSnapshot GetSnapshot(ILedgerIdentity identity)
        {
            AiDashboardSnapshot snap = new AiDashboardSnapshot
            {
                Widgets = new List<AiMetricRow>(),
                Alerts = new List<AiMetricRow>(),
                Executive = new List<AiMetricRow>()
            };
            if (!_license.CanView(identity))
            {
                snap.Alerts.Add(new AiMetricRow { Label = "دسترسی", Value = "مجوز AI.View یا لایسنس Free لازم است." });
                return snap;
            }

            int c = identity.CompanyId;
            AiAnalysisReport sales = _sales.Build(identity);
            AiAnalysisReport purchase = _purchase.Build(identity);
            AiAnalysisReport inv = _inventory.Build(identity);
            AiAnalysisReport fin = _financial.Build(identity);
            AiAnalysisReport cust = _customer.Build(identity);

            Add(snap.Widgets, "خلاصه فروش", Pick(sales, "مبلغ فروش ثبت‌شده (جزئی)"));
            Add(snap.Widgets, "خلاصه خرید", Pick(purchase, "مبلغ خرید ثبت‌شده (جزئی)"));
            Add(snap.Widgets, "سلامت موجودی", Pick(inv, "موجودی روی دست"));
            Add(snap.Widgets, "موقعیت نقد", Pick(fin, "موقعیت نقد (حساب‌های ۱۱xx)"));
            Add(snap.Widgets, "فعالیت مشتری", Pick(cust, "فعالیت CRM"));
            Add(snap.Widgets, "فعالیت فروشنده", Pick(cust, "فروشندگان"));
            Add(snap.Widgets, "مطالبات باز", Pick(fin, "مطالبات باز (فاکتور فروش ثبت‌شده)"));
            Add(snap.Widgets, "بدهی باز", Pick(fin, "بدهی باز (فاکتور خرید ثبت‌شده)"));

            long draftPo = _store.Count("SELECT COUNT(1) FROM PurOrder WHERE CompanyID=@c AND IsDeleted=0 AND Status<>@s;",
                new SQLiteParameter("@c", c), new SQLiteParameter("@s", TradeCodes.Posted));
            long draftSo = _store.Count("SELECT COUNT(1) FROM SalOrder WHERE CompanyID=@c AND IsDeleted=0 AND Status<>@s;",
                new SQLiteParameter("@c", c), new SQLiteParameter("@s", TradeCodes.Posted));
            long zero = _store.Count(@"
SELECT COUNT(1) FROM InvItem i WHERE i.CompanyID=@c AND i.IsDeleted=0
  AND NOT EXISTS (SELECT 1 FROM InvItemBalance b WHERE b.CompanyID=i.CompanyID AND b.ItemID=i.ItemID AND b.QuantityOnHand<>0);",
                new SQLiteParameter("@c", c));
            snap.Alerts.Add(new AiMetricRow { Label = "سفارش خرید باز", Value = draftPo.ToString(CultureInfo.InvariantCulture) });
            snap.Alerts.Add(new AiMetricRow { Label = "سفارش فروش باز", Value = draftSo.ToString(CultureInfo.InvariantCulture) });
            snap.Alerts.Add(new AiMetricRow { Label = "کالای بدون موجودی", Value = zero.ToString(CultureInfo.InvariantCulture) });

            if (_license.CanAlerts(identity))
            {
                IList<AiAlert> smart = new AiAlertEngine(_store, _license).Evaluate(identity);
                for (int i = 0; i < smart.Count; i++)
                    snap.Alerts.Add(new AiMetricRow { Label = smart[i].Title, Value = smart[i].Detail });
            }

            if (_license.CanExecutive(identity))
            {
                AiAnalysisReport exec = _exec.Analyze(identity);
                snap.Executive = exec.Rows;
            }
            return snap;
        }

        private static void Add(IList<AiMetricRow> list, string label, string value)
        {
            list.Add(new AiMetricRow { Label = label, Value = value });
        }

        private static string Pick(AiAnalysisReport report, string label)
        {
            if (report == null || report.Rows == null) return "0";
            for (int i = 0; i < report.Rows.Count; i++)
            {
                if (report.Rows[i].Label == label) return report.Rows[i].Value;
            }
            return report.Rows.Count > 0 ? report.Rows[0].Value : "0";
        }
    }

    public sealed class AiSettingsService
    {
        private readonly AiPlatformStore _store;
        private readonly AiLicenseService _license;
        public AiSettingsService() : this(new AiPlatformStore(), new AiLicenseService()) { }
        public AiSettingsService(AiPlatformStore store, AiLicenseService license)
        {
            _store = store;
            _license = license;
        }

        public AiSetting Get(ILedgerIdentity identity)
        {
            if (identity == null || !_license.CanSettings(identity)) return null;
            return _store.GetSetting(identity.CompanyId);
        }

        public bool Save(AiSetting setting, ILedgerIdentity identity)
        {
            if (identity == null || !_license.CanSettings(identity) || setting == null) return false;
            setting.CompanyId = identity.CompanyId;
            return _store.SaveSetting(setting, LedgerTime.UtcNow(identity.UtcNow), identity.UserName);
        }
    }

    public sealed class AiChatService : IAiChatService
    {
        private readonly AiPlatformStore _store;
        private readonly AiLicenseService _license;
        private readonly SalesAnalysisService _sales;
        private readonly PurchaseAnalysisService _purchase;
        private readonly InventoryAnalysisService _inventory;
        private readonly FinancialAnalysisService _financial;
        private readonly CustomerAnalysisService _customer;
        private readonly AiQueryService _queries;
        private readonly Func<string, IAiProvider> _factory;

        public AiChatService()
            : this(new AiPlatformStore(), new AiLicenseService(), AiProviderFactory.Create)
        {
        }

        public AiChatService(AiPlatformStore store, AiLicenseService license, Func<string, IAiProvider> factory)
        {
            _store = store;
            _license = license ?? new AiLicenseService(store);
            _factory = factory ?? new Func<string, IAiProvider>(AiProviderFactory.Create);
            _sales = new SalesAnalysisService(store, _license);
            _purchase = new PurchaseAnalysisService(store, _license);
            _inventory = new InventoryAnalysisService(store, _license);
            _financial = new FinancialAnalysisService(store, _license);
            _customer = new CustomerAnalysisService(store, _license);
            _queries = new AiQueryService(store, _license);
        }

        public AiResponse Ask(AiPrompt prompt, ILedgerIdentity identity)
        {
            string text = prompt == null ? "" : (prompt.Text ?? "").Trim();
            if (identity == null)
                return Fail("", "PERMISSION", "Identity required.", "Denied");
            AiSetting setting = _store.GetSetting(identity.CompanyId);
            string providerName = setting == null ? AiProviderNames.None : setting.Provider;
            string now = LedgerTime.UtcNow(identity.UtcNow);

            if (!_license.CanChat(identity))
            {
                _store.InsertAudit(identity.CompanyId, identity.UserId, identity.UserName, text, providerName, now, 0, 0, "Denied");
                return Fail("", "LICENSE", "Chat requires Pro license and AI.Chat.", "Denied");
            }
            if (string.IsNullOrEmpty(text))
                return Fail("", "VALIDATION", "Prompt is required.", "Failed");

            long convId = prompt.ConversationId;
            if (convId <= 0)
            {
                string title = text.Length > 40 ? text.Substring(0, 40) : text;
                convId = _store.InsertConversation(identity.CompanyId, identity.CenterId, identity.UserId, title, now, identity.UserName);
            }
            else
            {
                AiConversation existing = _store.GetConversation(convId, identity.CompanyId);
                if (existing == null)
                    return Fail("", "NOT_FOUND", "Conversation not found.", "Failed");
            }

            _store.InsertMessage(convId, identity.CompanyId, "user", text, providerName, now, identity.UserName);

            AiQueryAnswer routed = _queries.Ask(text, identity);
            if (routed.Matched)
            {
                _store.InsertAudit(identity.CompanyId, identity.UserId, identity.UserName, text, "QueryCatalog", now, 0, 0, "Ok");
                _store.InsertMessage(convId, identity.CompanyId, "assistant", routed.Text ?? "", "QueryCatalog", now, identity.UserName);
                return new AiResponse
                {
                    Ok = true,
                    Text = routed.Text,
                    ConversationId = convId,
                    Provider = "QueryCatalog",
                    Status = "Ok"
                };
            }

            string snapshot = BuildReadOnlySnapshot(identity);
            AiCompletionRequest req = new AiCompletionRequest
            {
                SystemPrompt = "You are the Ganjineh ERP intelligence assistant. Read-only. Never invent postings, never execute SQL, never modify documents. Use only the supplied snapshot.",
                UserPrompt = snapshot + "\n\nQuestion:\n" + text,
                Model = setting.Model,
                Endpoint = setting.Endpoint,
                ApiKey = setting.ApiKey,
                TimeoutMs = setting.TimeoutMs,
                MaxTokens = setting.MaxTokens,
                Temperature = setting.Temperature
            };
            IAiProvider provider = _factory(providerName);
            AiCompletionResult result = provider.Complete(req);
            string status = result.Ok ? "Ok" : (result.ErrorCode ?? "Failed");
            if (!result.Ok && string.Equals(result.ErrorCode, "NOT_CONFIGURED", StringComparison.OrdinalIgnoreCase))
                status = "NotConfigured";
            _store.InsertAudit(identity.CompanyId, identity.UserId, identity.UserName, text, provider.Name, now,
                result.InputTokens, result.OutputTokens, status);

            string reply = result.Ok ? result.Text : (result.Error ?? result.ErrorCode);
            _store.InsertMessage(convId, identity.CompanyId, "assistant", reply ?? "", provider.Name, now, identity.UserName);

            return new AiResponse
            {
                Ok = result.Ok,
                ErrorCode = result.ErrorCode,
                Message = result.Error,
                Text = reply,
                ConversationId = convId,
                Provider = provider.Name,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                Status = status
            };
        }

        public AiConversationHistory History(long conversationId, ILedgerIdentity identity)
        {
            if (identity == null || !_license.CanChat(identity)) return null;
            AiConversation conv = _store.GetConversation(conversationId, identity.CompanyId);
            if (conv == null) return null;
            return new AiConversationHistory { Conversation = conv, Messages = _store.ListMessages(conversationId) };
        }

        private string BuildReadOnlySnapshot(ILedgerIdentity identity)
        {
            StringBuilder sb = new StringBuilder();
            Append(sb, _sales.Build(identity));
            Append(sb, _purchase.Build(identity));
            Append(sb, _inventory.Build(identity));
            Append(sb, _financial.Build(identity));
            Append(sb, _customer.Build(identity));
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, AiAnalysisReport report)
        {
            if (report == null) return;
            sb.AppendLine(report.Title);
            if (report.Rows == null) return;
            for (int i = 0; i < report.Rows.Count; i++)
                sb.AppendLine(report.Rows[i].Label + "=" + report.Rows[i].Value);
        }

        private static AiResponse Fail(string text, string code, string message, string status)
        {
            return new AiResponse { Ok = false, ErrorCode = code, Message = message, Text = text, Status = status };
        }
    }
}

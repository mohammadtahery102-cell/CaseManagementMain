using System;
using System.Collections.Generic;

namespace CaseManagement.AiPlatform.Domain
{
    public static class AiPermissions
    {
        public const string View = "AI.View";
        public const string Chat = "AI.Chat";
        public const string Analytics = "AI.Analytics";
        public const string Executive = "AI.Executive";
        public const string Settings = "AI.Settings";
        public const string LicenseFree = "AI.License.Free";
        public const string LicensePro = "AI.License.Pro";
        public const string LicenseExecutive = "AI.License.Executive";
    }

    public static class AiLicenseLevels
    {
        public const string Free = "Free";
        public const string Pro = "Pro";
        public const string Executive = "Executive";

        public static int Rank(string level)
        {
            if (string.Equals(level, Executive, StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(level, Pro, StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(level, Free, StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        }

        public static string FromRank(int rank)
        {
            if (rank >= 3) return Executive;
            if (rank >= 2) return Pro;
            if (rank >= 1) return Free;
            return "";
        }
    }

    public static class AiProviderNames
    {
        public const string None = "None";
        public const string OpenAI = "OpenAI";
        public const string Claude = "Claude";
        public const string Gemini = "Gemini";
        public const string Grok = "Grok";
        public const string Fake = "Fake";
    }

    public sealed class AiSetting
    {
        public long SettingId { get; set; }
        public int CompanyId { get; set; }
        public string Provider { get; set; }
        public string Endpoint { get; set; }
        public string Model { get; set; }
        public string ApiKey { get; set; }
        public int TimeoutMs { get; set; }
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
        public string LicenseLevel { get; set; }
        public long RowVersion { get; set; }
    }

    public sealed class AiConversation
    {
        public long ConversationId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; }
        public string CreatedAt { get; set; }
    }

    public sealed class AiPrompt
    {
        public string Text { get; set; }
        public long ConversationId { get; set; }
    }

    public sealed class AiResponse
    {
        public bool Ok { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public string Text { get; set; }
        public long ConversationId { get; set; }
        public string Provider { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public string Status { get; set; }
    }

    public sealed class AiConversationHistory
    {
        public AiConversation Conversation { get; set; }
        public IList<AiHistoryMessage> Messages { get; set; }
    }

    public sealed class AiHistoryMessage
    {
        public string Role { get; set; }
        public string Body { get; set; }
        public string CreatedAt { get; set; }
    }

    public sealed class AiAuditEntry
    {
        public long AuditId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string Prompt { get; set; }
        public string Provider { get; set; }
        public string TimestampUtc { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public string Status { get; set; }
    }

    public sealed class AiCompletionRequest
    {
        public string SystemPrompt { get; set; }
        public string UserPrompt { get; set; }
        public string Model { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public int TimeoutMs { get; set; }
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
    }

    public sealed class AiCompletionResult
    {
        public bool Ok { get; set; }
        public string Text { get; set; }
        public string ErrorCode { get; set; }
        public string Error { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }

        public static AiCompletionResult Success(string text, int inputTokens, int outputTokens)
        {
            return new AiCompletionResult { Ok = true, Text = text ?? "", InputTokens = inputTokens, OutputTokens = outputTokens };
        }

        public static AiCompletionResult Fail(string code, string error)
        {
            return new AiCompletionResult { Ok = false, ErrorCode = code, Error = error ?? "", Text = "" };
        }
    }

    public interface IAiProvider
    {
        string Name { get; }
        AiCompletionResult Complete(AiCompletionRequest request);
    }

    public interface IAiChatService
    {
        AiResponse Ask(AiPrompt prompt, CaseManagement.Accounting.Ledger.Application.ILedgerIdentity identity);
        AiConversationHistory History(long conversationId, CaseManagement.Accounting.Ledger.Application.ILedgerIdentity identity);
    }

    public sealed class AiMetricRow
    {
        public string Label { get; set; }
        public string Value { get; set; }
    }

    public sealed class AiDashboardSnapshot
    {
        public IList<AiMetricRow> Widgets { get; set; }
        public IList<AiMetricRow> Alerts { get; set; }
        public IList<AiMetricRow> Executive { get; set; }
    }

    public sealed class AiAnalysisReport
    {
        public string Title { get; set; }
        public IList<AiMetricRow> Rows { get; set; }
    }

    public sealed class AiQueryAnswer
    {
        public string Intent { get; set; }
        public bool Matched { get; set; }
        public AiAnalysisReport Report { get; set; }
        public string Text { get; set; }
    }

    public sealed class AiAlert
    {
        public string Code { get; set; }
        public string Severity { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
    }

    public sealed class AiExecutiveSnapshot
    {
        public long RevenueMinor { get; set; }
        public long ExpenseMinor { get; set; }
        public long ProfitMinor { get; set; }
        public long InventoryValueMinor { get; set; }
        public long ReceivablesMinor { get; set; }
        public long PayablesMinor { get; set; }
        public long CashMinor { get; set; }
        public int HealthScore { get; set; }
        public string Summary { get; set; }
        public IList<AiMetricRow> Widgets { get; set; }
    }

    public static class AiQueryIntents
    {
        public const string TopCustomers = "TopCustomers";
        public const string TopProducts = "TopProducts";
        public const string SlowProducts = "SlowProducts";
        public const string Cash = "Cash";
        public const string Debtors = "Debtors";
        public const string ProfitTrend = "ProfitTrend";
        public const string InventoryRisk = "InventoryRisk";
        public const string Unknown = "Unknown";
    }
}

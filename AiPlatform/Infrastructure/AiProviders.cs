using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using CaseManagement.AiPlatform.Domain;

namespace CaseManagement.AiPlatform.Infrastructure
{
    public static class AiProviderFactory
    {
        public static IAiProvider Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return new NoneAiProvider();
            string n = name.Trim();
            if (n.Equals(AiProviderNames.OpenAI, StringComparison.OrdinalIgnoreCase)) return new OpenAiProvider();
            if (n.Equals(AiProviderNames.Claude, StringComparison.OrdinalIgnoreCase)
                || n.Equals("Anthropic", StringComparison.OrdinalIgnoreCase)) return new ClaudeProvider();
            if (n.Equals(AiProviderNames.Gemini, StringComparison.OrdinalIgnoreCase)
                || n.Equals("Google", StringComparison.OrdinalIgnoreCase)) return new GeminiProvider();
            if (n.Equals(AiProviderNames.Grok, StringComparison.OrdinalIgnoreCase)
                || n.Equals("xAI", StringComparison.OrdinalIgnoreCase)) return new GrokProvider();
            if (n.Equals(AiProviderNames.Fake, StringComparison.OrdinalIgnoreCase)) return new FakeAiProvider();
            return new NoneAiProvider();
        }
    }

    public sealed class NoneAiProvider : IAiProvider
    {
        public string Name { get { return AiProviderNames.None; } }

        public AiCompletionResult Complete(AiCompletionRequest request)
        {
            return AiCompletionResult.Fail("NOT_CONFIGURED", "No AI provider is configured.");
        }
    }

    public sealed class FakeAiProvider : IAiProvider
    {
        public string Name { get { return AiProviderNames.Fake; } }

        public AiCompletionResult Complete(AiCompletionRequest request)
        {
            string prompt = request == null ? "" : (request.UserPrompt ?? "");
            string text = "READ-ONLY ERP summary: " + (prompt.Length > 120 ? prompt.Substring(0, 120) : prompt);
            int tokens = Math.Max(1, prompt.Length / 4);
            return AiCompletionResult.Success(text, tokens, Math.Max(1, text.Length / 4));
        }
    }

    public sealed class OpenAiProvider : IAiProvider
    {
        public string Name { get { return AiProviderNames.OpenAI; } }

        public AiCompletionResult Complete(AiCompletionRequest request)
        {
            string endpoint = AiStr.First(request == null ? null : request.Endpoint, "https://api.openai.com/v1/chat/completions");
            string model = AiStr.First(request == null ? null : request.Model, "gpt-4o-mini");
            return OpenAiCompatible.Post(Name, endpoint, request, model, "Authorization", "Bearer ");
        }
    }

    public sealed class GrokProvider : IAiProvider
    {
        public string Name { get { return AiProviderNames.Grok; } }

        public AiCompletionResult Complete(AiCompletionRequest request)
        {
            string endpoint = AiStr.First(request == null ? null : request.Endpoint, "https://api.x.ai/v1/chat/completions");
            string model = AiStr.First(request == null ? null : request.Model, "grok-2-latest");
            return OpenAiCompatible.Post(Name, endpoint, request, model, "Authorization", "Bearer ");
        }
    }

    public sealed class ClaudeProvider : IAiProvider
    {
        public string Name { get { return AiProviderNames.Claude; } }

        public AiCompletionResult Complete(AiCompletionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ApiKey))
                return AiCompletionResult.Fail("NOT_CONFIGURED", "Claude API key is missing.");
            string endpoint = AiStr.First(request.Endpoint, "https://api.anthropic.com/v1/messages");
            string model = AiStr.First(request.Model, "claude-3-5-sonnet-latest");
            string body = "{\"model\":\"" + AiJson.Escape(model) + "\",\"max_tokens\":" + request.MaxTokens
                + ",\"temperature\":" + request.Temperature.ToString("0.###", CultureInfo.InvariantCulture)
                + ",\"system\":\"" + AiJson.Escape(request.SystemPrompt)
                + "\",\"messages\":[{\"role\":\"user\",\"content\":\"" + AiJson.Escape(request.UserPrompt) + "\"}]}";
            return HttpJson.Post(endpoint, body, request.TimeoutMs, delegate (HttpWebRequest http)
            {
                http.Headers["x-api-key"] = request.ApiKey;
                http.Headers["anthropic-version"] = "2023-06-01";
            }, "text");
        }
    }

    public sealed class GeminiProvider : IAiProvider
    {
        public string Name { get { return AiProviderNames.Gemini; } }

        public AiCompletionResult Complete(AiCompletionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ApiKey))
                return AiCompletionResult.Fail("NOT_CONFIGURED", "Gemini API key is missing.");
            string model = AiStr.First(request.Model, "gemini-1.5-flash");
            string root = AiStr.First(request.Endpoint,
                "https://generativelanguage.googleapis.com/v1beta/models/" + model + ":generateContent");
            string sep = root.IndexOf('?') >= 0 ? "&" : "?";
            string url = root + sep + "key=" + Uri.EscapeDataString(request.ApiKey);
            string prompt = (request.SystemPrompt ?? "") + "\n\n" + (request.UserPrompt ?? "");
            string body = "{\"contents\":[{\"parts\":[{\"text\":\"" + AiJson.Escape(prompt) + "\"}]}],\"generationConfig\":{"
                + "\"maxOutputTokens\":" + request.MaxTokens
                + ",\"temperature\":" + request.Temperature.ToString("0.###", CultureInfo.InvariantCulture) + "}}";
            return HttpJson.Post(url, body, request.TimeoutMs, null, "text");
        }
    }

    internal static class OpenAiCompatible
    {
        public static AiCompletionResult Post(string name, string endpoint, AiCompletionRequest request, string model,
            string headerName, string headerPrefix)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ApiKey))
                return AiCompletionResult.Fail("NOT_CONFIGURED", name + " API key is missing.");
            string body = "{\"model\":\"" + AiJson.Escape(model) + "\",\"temperature\":"
                + request.Temperature.ToString("0.###", CultureInfo.InvariantCulture)
                + ",\"max_tokens\":" + request.MaxTokens
                + ",\"messages\":[{\"role\":\"system\",\"content\":\"" + AiJson.Escape(request.SystemPrompt)
                + "\"},{\"role\":\"user\",\"content\":\"" + AiJson.Escape(request.UserPrompt) + "\"}]}";
            return HttpJson.Post(endpoint, body, request.TimeoutMs, delegate (HttpWebRequest http)
            {
                http.Headers[headerName] = headerPrefix + request.ApiKey;
            }, "content");
        }
    }

    internal static class HttpJson
    {
        public static AiCompletionResult Post(string url, string json, int timeoutMs, Action<HttpWebRequest> headers, string textKey)
        {
            try
            {
                HttpWebRequest http = (HttpWebRequest)WebRequest.Create(url);
                http.Method = "POST";
                http.ContentType = "application/json";
                http.Timeout = timeoutMs <= 0 ? 30000 : timeoutMs;
                if (headers != null) headers(http);
                byte[] bytes = Encoding.UTF8.GetBytes(json ?? "{}");
                http.ContentLength = bytes.Length;
                using (Stream s = http.GetRequestStream())
                    s.Write(bytes, 0, bytes.Length);
                using (HttpWebResponse resp = (HttpWebResponse)http.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string raw = reader.ReadToEnd();
                    string text = ExtractJsonString(raw, textKey);
                    if (string.IsNullOrEmpty(text)) text = raw;
                    return AiCompletionResult.Success(text, Estimate(json), Estimate(text));
                }
            }
            catch (WebException ex)
            {
                return AiCompletionResult.Fail("PROVIDER", ex.Message);
            }
            catch (Exception ex)
            {
                return AiCompletionResult.Fail("PROVIDER", ex.Message);
            }
        }

        private static int Estimate(string s)
        {
            return Math.Max(1, (s ?? "").Length / 4);
        }

        private static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            string needle = "\"" + key + "\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return "";
            int colon = json.IndexOf(':', i + needle.Length);
            if (colon < 0) return "";
            int q = json.IndexOf('"', colon + 1);
            if (q < 0) return "";
            StringBuilder sb = new StringBuilder();
            for (int p = q + 1; p < json.Length; p++)
            {
                char c = json[p];
                if (c == '\\' && p + 1 < json.Length)
                {
                    char n = json[p + 1];
                    if (n == 'n') sb.Append('\n');
                    else if (n == 't') sb.Append('\t');
                    else sb.Append(n);
                    p++;
                    continue;
                }
                if (c == '"') break;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

    internal static class AiJson
    {
        public static string Escape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }

    internal static class AiStr
    {
        public static string First(string a, string b)
        {
            return string.IsNullOrWhiteSpace(a) ? b : a.Trim();
        }
    }
}

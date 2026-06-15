using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class OpenAiPlainSummaryService
    {
        private static readonly HttpClient Client = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public string ProviderName => "OpenAI-compatible Summary";

        public async Task<string> SummarizeAsync(
            string sourceText,
            string outputLanguage,
            CancellationToken cancellationToken)
        {
            var config = Translator.Setting?["OpenAI"] as OpenAIConfig;
            ValidateConfig(config);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            var requestData = new
            {
                model = config!.ModelName,
                temperature = config.Temperature,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = $"Write a plain-language summary in {outputLanguage}. Do not create action items, priority labels, checklists, deadlines, or ClearBridge structured fields. Do not add facts that are not in the text."
                    },
                    new
                    {
                        role = "user",
                        content = sourceText
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestData, JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, TextUtil.NormalizeUrl(config.ApiUrl));
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

            try
            {
                var response = await Client.SendAsync(request, timeoutCts.Token);
                var responseText = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                if (!response.IsSuccessStatusCode)
                    throw new ClearBridgeAnalysisException(
                        "HttpError",
                        $"The summary provider returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");

                return ExtractAssistantContent(responseText).Trim();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ClearBridgeAnalysisException("ProviderTimeout", "The summary provider timed out.");
            }
            catch (HttpRequestException ex)
            {
                throw new ClearBridgeAnalysisException("NetworkError", "The summary provider could not be reached.", ex);
            }
        }

        private static void ValidateConfig(OpenAIConfig? config)
        {
            if (config == null)
                throw new ClearBridgeAnalysisException("ProviderNotConfigured", "OpenAI provider settings were not found.");
            if (string.IsNullOrWhiteSpace(config.ApiUrl))
                throw new ClearBridgeAnalysisException("ProviderNotConfigured", "OpenAI-compatible API URL is missing.");
            if (string.IsNullOrWhiteSpace(config.ModelName))
                throw new ClearBridgeAnalysisException("ProviderNotConfigured", "OpenAI-compatible model name is missing.");
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new ClearBridgeAnalysisException("ApiKeyMissing", "OpenAI-compatible API key is missing.");
        }

        private static string ExtractAssistantContent(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                throw new ClearBridgeAnalysisException("EmptyResponse", "The summary provider returned an empty response.");

            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }

            throw new ClearBridgeAnalysisException("InvalidJson", "The summary response did not contain choices[0].message.content.");
        }
    }
}

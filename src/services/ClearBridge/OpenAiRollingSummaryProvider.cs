using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class OpenAiRollingSummaryProvider : IRollingSummaryProvider
    {
        private static readonly HttpClient Client = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public string Name => "OpenAI-compatible";

        public async Task<RollingSummaryResult> AnalyzeBatchAsync(
            RollingSummaryRequest request,
            string outputLanguage,
            CancellationToken cancellationToken)
        {
            var config = Translator.Setting?["OpenAI"] as OpenAIConfig;
            ValidateConfig(config);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));

            var started = Stopwatch.StartNew();
            var responseText = string.Empty;
            try
            {
                var previousContextJson = JsonSerializer.Serialize(request.PreviousContext, JsonOptions);
                var systemPrompt = CrisisActionPromptBuilder.BuildRollingSummarySystemPrompt(outputLanguage);
                var userPrompt = CrisisActionPromptBuilder.BuildRollingSummaryUserPrompt(
                    previousContextJson,
                    request.BatchTranscript);

                var content = await SendChatRequestAsync(config!, systemPrompt, userPrompt, timeoutCts.Token);
                responseText = content;
                var result = await ParseWithSingleRetryAsync(
                    content,
                    config!,
                    systemPrompt,
                    userPrompt,
                    timeoutCts.Token);
                CrisisActionSourceEvidenceSanitizer.KeepOnlyExactSourceEvidence(
                    ToCrisisResult(result),
                    request.BatchTranscript);
                RollingSummaryJsonParser.ClampContext(result.ContextCache);
                LogDiagnostic("Completed", started.ElapsedMilliseconds, request.CharacterCount, content.Length);
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LogDiagnostic("Timeout", started.ElapsedMilliseconds, request.CharacterCount, responseText.Length, "ProviderTimeout");
                throw new ClearBridgeAnalysisException("ProviderTimeout", "The provider timed out before returning a result.");
            }
            catch (HttpRequestException ex)
            {
                LogDiagnostic("NetworkError", started.ElapsedMilliseconds, request.CharacterCount, responseText.Length, ex.GetType().Name);
                throw new ClearBridgeAnalysisException("NetworkError", "The provider could not be reached.", ex);
            }
            catch (ClearBridgeAnalysisException ex)
            {
                LogDiagnostic("Failed", started.ElapsedMilliseconds, request.CharacterCount, responseText.Length, ex.ErrorCode);
                throw;
            }
        }

        private static async Task<RollingSummaryResult> ParseWithSingleRetryAsync(
            string content,
            OpenAIConfig config,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
        {
            try
            {
                return RollingSummaryJsonParser.Parse(content);
            }
            catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode is "InvalidJson" or "EmptyResponse")
            {
                var retryPrompt = userPrompt +
                    "\n\nThe previous response was not valid JSON. Return only one complete parseable JSON object. " +
                    "Use the exact English snake_case property names from the schema. Do not translate JSON keys. " +
                    "Use standard double-quoted JSON strings and escape quotes, backslashes, and line breaks.";
                var retryContent = await SendChatRequestAsync(config, systemPrompt, retryPrompt, cancellationToken);
                return RollingSummaryJsonParser.Parse(retryContent);
            }
        }

        private static async Task<string> SendChatRequestAsync(
            OpenAIConfig config,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
        {
            var requestData = new
            {
                model = config.ModelName,
                temperature = config.Temperature,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestData, JsonOptions);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, TextUtil.NormalizeUrl(config.ApiUrl));
            httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            httpRequest.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

            using var response = await Client.SendAsync(httpRequest, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new ClearBridgeAnalysisException(
                    "HttpError",
                    $"The provider returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");

            return ExtractAssistantContent(responseText);
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
                throw new ClearBridgeAnalysisException("EmptyResponse", "The provider returned an empty response.");

            try
            {
                using var document = JsonDocument.Parse(responseText);
                if (document.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }
            }
            catch (JsonException ex)
            {
                throw new ClearBridgeAnalysisException("InvalidJson", "The provider response envelope was not valid JSON.", ex);
            }

            throw new ClearBridgeAnalysisException("InvalidJson", "The provider response did not contain choices[0].message.content.");
        }

        private static CrisisActionAnalysisResult ToCrisisResult(RollingSummaryResult result)
        {
            return new CrisisActionAnalysisResult
            {
                Title = result.CurrentTopic,
                Summary = result.BatchSummary,
                ImportantPoints = result.KeyPoints,
                Actions = result.NewActions,
                Warnings = result.Warnings,
                UnclearItems = result.UnresolvedQuestions,
                SourceEvidence = result.SourceEvidence
            };
        }

        private static void LogDiagnostic(
            string status,
            long latencyMs,
            int inputLength,
            int outputLength,
            string errorType = "")
        {
            Debug.WriteLine(
                $"ClearBridge RollingSummary Provider={nameof(OpenAiRollingSummaryProvider)} Status={status} " +
                $"LatencyMs={latencyMs} InputLength={inputLength} OutputLength={outputLength} ErrorType={errorType}");
        }
    }
}

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
    public sealed class OpenAiCrisisActionAnalysisProvider : ICrisisActionAnalysisProvider
    {
        private static readonly HttpClient Client = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly CrisisActionPromptMode promptMode;

        public OpenAiCrisisActionAnalysisProvider(
            CrisisActionPromptMode promptMode = CrisisActionPromptMode.Notice)
        {
            this.promptMode = promptMode;
        }

        public string Name => "OpenAI-compatible";

        public async Task<CrisisActionAnalysisResult> AnalyzeAsync(
            string sourceText,
            string outputLanguage,
            CancellationToken cancellationToken)
        {
            var config = Translator.Setting?["OpenAI"] as OpenAIConfig;
            ValidateConfig(config);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            var started = Stopwatch.StartNew();
            var responseText = string.Empty;
            try
            {
                var normalizedOutputLanguage = ClearBridgeOutputLanguages.Normalize(outputLanguage);
                var systemPrompt = promptMode == CrisisActionPromptMode.CaptionTranscript
                    ? CrisisActionPromptBuilder.BuildCaptionSystemPrompt(normalizedOutputLanguage)
                    : CrisisActionPromptBuilder.BuildSystemPrompt(normalizedOutputLanguage);
                var userPrompt = promptMode == CrisisActionPromptMode.CaptionTranscript
                    ? CrisisActionPromptBuilder.BuildCaptionUserPrompt(sourceText)
                    : CrisisActionPromptBuilder.BuildUserPrompt(sourceText);

                var response = await SendRequestAsync(config!, systemPrompt, userPrompt, timeoutCts.Token);
                responseText = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                    throw new ClearBridgeAnalysisException(
                        "HttpError",
                        $"The provider returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");

                var content = ExtractAssistantContent(responseText);
                var result = CrisisActionJsonParser.Parse(content);
                if (promptMode == CrisisActionPromptMode.CaptionTranscript)
                    CrisisActionSourceEvidenceSanitizer.KeepOnlyExactSourceEvidence(result, sourceText);
                try
                {
                    ClearBridgeOutputLanguageValidator.EnsureCrisisResultMatches(result, normalizedOutputLanguage);
                }
                catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "OutputLanguageMismatch")
                {
                    LogDiagnostic(
                        "LanguageRetry",
                        started.ElapsedMilliseconds,
                        sourceText.Length,
                        content.Length,
                        ex.ErrorCode);

                    var retryPrompt = CrisisActionPromptBuilder.BuildLanguageRetryUserPrompt(
                        userPrompt,
                        normalizedOutputLanguage);
                    var retryResponse = await SendRequestAsync(config!, systemPrompt, retryPrompt, timeoutCts.Token);
                    responseText = await retryResponse.Content.ReadAsStringAsync(timeoutCts.Token);
                    if (!retryResponse.IsSuccessStatusCode)
                        throw new ClearBridgeAnalysisException(
                            "HttpError",
                            $"The provider returned HTTP {(int)retryResponse.StatusCode} ({retryResponse.StatusCode}).");

                    content = ExtractAssistantContent(responseText);
                    result = CrisisActionJsonParser.Parse(content);
                    if (promptMode == CrisisActionPromptMode.CaptionTranscript)
                        CrisisActionSourceEvidenceSanitizer.KeepOnlyExactSourceEvidence(result, sourceText);

                    ClearBridgeOutputLanguageValidator.EnsureCrisisResultMatches(result, normalizedOutputLanguage);
                }

                LogDiagnostic("Completed", started.ElapsedMilliseconds, sourceText.Length, content.Length);
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LogDiagnostic("Timeout", started.ElapsedMilliseconds, sourceText.Length, responseText.Length, "ProviderTimeout");
                throw new ClearBridgeAnalysisException("ProviderTimeout", "The provider timed out before returning a result.");
            }
            catch (HttpRequestException ex)
            {
                LogDiagnostic("NetworkError", started.ElapsedMilliseconds, sourceText.Length, responseText.Length, ex.GetType().Name);
                throw new ClearBridgeAnalysisException("NetworkError", "The provider could not be reached. Mock Mode can be used as a fallback.", ex);
            }
            catch (ClearBridgeAnalysisException ex)
            {
                LogDiagnostic("Failed", started.ElapsedMilliseconds, sourceText.Length, responseText.Length, ex.ErrorCode);
                throw;
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

        private static async Task<HttpResponseMessage> SendRequestAsync(
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
            using var request = new HttpRequestMessage(HttpMethod.Post, TextUtil.NormalizeUrl(config.ApiUrl));
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

            return await Client.SendAsync(request, cancellationToken);
        }

        private static string ExtractAssistantContent(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                throw new ClearBridgeAnalysisException("EmptyResponse", "The provider returned an empty response.");

            try
            {
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
            }
            catch (JsonException ex)
            {
                throw new ClearBridgeAnalysisException("InvalidJson", "The provider response envelope was not valid JSON.", ex);
            }

            throw new ClearBridgeAnalysisException("InvalidJson", "The provider response did not contain choices[0].message.content.");
        }

        private static void LogDiagnostic(
            string status,
            long latencyMs,
            int inputLength,
            int outputLength,
            string errorType = "")
        {
            Debug.WriteLine(
                $"ClearBridge Provider={nameof(OpenAiCrisisActionAnalysisProvider)} Status={status} " +
                $"LatencyMs={latencyMs} InputLength={inputLength} OutputLength={outputLength} ErrorType={errorType}");
        }
    }
}

using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator.services.Ocr
{
    public sealed class AiVisionOcrProvider : IClearBridgeOcrProvider
    {
        private static readonly HttpClient Client = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public string Id => "openai-compatible-vision";

        public string DisplayName => "AI OCR (OpenAI-compatible)";

        public bool IsCloudBased => true;

        public async Task<ClearBridgeOcrResult> ExtractTextAsync(
            ClearBridgeImageInput input,
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
                var response = await SendRequestAsync(config!, input, timeoutCts.Token);
                responseText = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                if (!response.IsSuccessStatusCode)
                    throw new ClearBridgeOcrException(
                        "AiOcrHttpError",
                        $"The AI OCR provider returned HTTP {(int)response.StatusCode} ({response.StatusCode}). The selected model may not support vision input.");

                var text = ExtractAssistantContent(responseText).Trim();
                started.Stop();

                return new ClearBridgeOcrResult
                {
                    Text = text,
                    EngineId = Id,
                    EngineName = DisplayName,
                    IsCloudBased = IsCloudBased,
                    Duration = started.Elapsed,
                    ImageWidth = input.Width,
                    ImageHeight = input.Height,
                    ImageBytes = input.ByteLength
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ClearBridgeOcrException("AiOcrTimeout", "AI OCR timed out before returning text.");
            }
            catch (HttpRequestException ex)
            {
                throw new ClearBridgeOcrException("AiOcrNetwork", "The AI OCR provider could not be reached.", ex);
            }
            catch (ClearBridgeOcrException)
            {
                throw;
            }
            catch (JsonException ex)
            {
                throw new ClearBridgeOcrException("AiOcrInvalidResponse", "The AI OCR provider returned invalid JSON.", ex);
            }
        }

        private static void ValidateConfig(OpenAIConfig? config)
        {
            if (config == null)
                throw new ClearBridgeOcrException("AiProviderNotConfigured", "OpenAI provider settings were not found.");
            if (string.IsNullOrWhiteSpace(config.ApiUrl))
                throw new ClearBridgeOcrException("AiProviderNotConfigured", "OpenAI-compatible API URL is missing.");
            if (string.IsNullOrWhiteSpace(config.ModelName))
                throw new ClearBridgeOcrException("AiProviderNotConfigured", "OpenAI-compatible model name is missing.");
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new ClearBridgeOcrException("AiOcrApiKeyMissing", "OpenAI-compatible API key is missing.");
        }

        private static async Task<HttpResponseMessage> SendRequestAsync(
            OpenAIConfig config,
            ClearBridgeImageInput input,
            CancellationToken cancellationToken)
        {
            var dataUrl = "data:image/png;base64," + Convert.ToBase64String(input.PngBytes);
            var requestData = new
            {
                model = config.ModelName,
                temperature = 0,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Extract only the visible text from the image. Preserve reading order, dates, times, amounts, addresses, document names, and contact details. Do not summarize, explain, translate, infer, or add missing information. Use [unclear] for text that cannot be read. Return plain text only."
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Extract the text from this image. Return plain text only." },
                            new { type = "image_url", image_url = new { url = dataUrl } }
                        }
                    }
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
                throw new ClearBridgeOcrException("AiOcrEmptyResponse", "The AI OCR provider returned an empty response.");

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

            throw new ClearBridgeOcrException("AiOcrInvalidResponse", "The AI OCR response did not contain choices[0].message.content.");
        }
    }
}

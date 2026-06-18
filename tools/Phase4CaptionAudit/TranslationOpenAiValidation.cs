using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class TranslationOpenAiValidation
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> RunAsync(string[] args)
    {
        var settingPath = GetOption(args, "--setting")
            ?? @"D:\USALL\USALL-Git\test-build\ClearBridge-Latest\setting.json";
        var config = LoadOpenAiConfig(settingPath);

        Console.WriteLine("Provider: OpenAI-compatible");
        Console.WriteLine($"Final URL: {NormalizeUrl(config.ApiUrl)}");
        Console.WriteLine($"Model: {config.ModelName}");
        Console.WriteLine("Key exposed: No");

        var legacy = await SendAsync(
            config,
            BuildLegacyPayload(config, "As gentle as sunlight.", "Simplified Chinese"),
            CancellationToken.None);
        Console.WriteLine();
        Console.WriteLine("Legacy realtime caption payload");
        Console.WriteLine($"Status: {legacy.Status}");
        Console.WriteLine($"Fields: {string.Join(",", legacy.FieldNames)}");
        Console.WriteLine($"Messages: {legacy.MessageSummary}");
        Console.WriteLine($"Error message: {legacy.SafeErrorMessage}");
        Console.WriteLine($"Error type: {legacy.ErrorType}");
        Console.WriteLine($"Error param: {legacy.ErrorParam}");
        Console.WriteLine($"Error code: {legacy.ErrorCode}");

        var results = new List<TranslationTestResult>();
        await RunAndRecordAsync(results, "Short sentence", () =>
            TranslateAsync(config, "As gentle as sunlight.", "Simplified Chinese", CancellationToken.None));
        await RunAndRecordAsync(results, "Normal sentence", () =>
            TranslateAsync(config, "Doctor Jane Goodall is going to tell you a story today.", "Simplified Chinese", CancellationToken.None));
        await RunAndRecordAsync(results, "Long sentence", () =>
            TranslateAsync(
                config,
                "The community meeting began with a short reminder about the science project, then the teacher explained that every student should review the worksheet, check the submission page, ask questions before Friday afternoon, and make sure the final file is uploaded clearly so the project team can review it without confusion.",
                "Simplified Chinese",
                CancellationToken.None));
        results.Add(TestEmptyInput());
        await RunAndRecordAsync(results, "Cancel", async () =>
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(1);
            return await TranslateAsync(config, "This request should be cancelled before completion.", "Simplified Chinese", cts.Token);
        });
        await RunAndRecordAsync(results, "Invalid model", () =>
            TranslateAsync(config with { ModelName = "invalid-caption-translation-model" }, "As gentle as sunlight.", "Simplified Chinese", CancellationToken.None));
        await RunAndRecordAsync(results, "Google regression", () =>
            GoogleTranslateAsync("As gentle as sunlight.", "zh-CN", CancellationToken.None));

        Console.WriteLine();
        Console.WriteLine("Translation validation summary");
        foreach (var result in results)
        {
            Console.WriteLine(
                $"{result.Name}: {result.Status}; LatencyMs={result.LatencyMs}; " +
                $"OutputChars={result.OutputChars}; ErrorType={result.ErrorType}; ErrorCode={result.ErrorCode}; " +
                $"SafeMessage={result.SafeMessage}");
        }

        var hardFailures = results.Count(result =>
            result.Name is "Short sentence" or "Normal sentence" or "Long sentence" or "Google regression"
            && result.Status != "Success");
        return hardFailures == 0 ? 0 : 1;
    }

    private static async Task RunAndRecordAsync(
        List<TranslationTestResult> results,
        string name,
        Func<Task<TranslationTestResult>> test)
    {
        try
        {
            var result = await test();
            results.Add(result with { Name = name });
        }
        catch (OperationCanceledException)
        {
            results.Add(new TranslationTestResult
            {
                Name = name,
                Status = "Cancelled"
            });
        }
        catch (Exception ex)
        {
            results.Add(new TranslationTestResult
            {
                Name = name,
                Status = "Failed",
                ErrorType = ex.GetType().Name,
                SafeMessage = ex.Message
            });
        }
    }

    private static async Task<TranslationTestResult> TranslateAsync(
        ProviderConfig config,
        string text,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return TestEmptyInput();

        var payload = BuildMinimalPayload(config, text, targetLanguage);
        var stopwatch = Stopwatch.StartNew();
        var response = await SendAsync(config, payload, cancellationToken);
        stopwatch.Stop();

        return new TranslationTestResult
        {
            Status = response.IsSuccess ? "Success" : "Failed",
            LatencyMs = stopwatch.ElapsedMilliseconds,
            OutputChars = response.OutputLength,
            ErrorType = response.ErrorType,
            ErrorCode = response.ErrorCode,
            SafeMessage = response.SafeErrorMessage
        };
    }

    private static TranslationTestResult TestEmptyInput()
    {
        return new TranslationTestResult
        {
            Name = "Empty",
            Status = "BlockedLocally",
            SafeMessage = "Input is empty."
        };
    }

    private static async Task<TranslationTestResult> GoogleTranslateAsync(
        string text,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var encodedText = Uri.EscapeDataString(text);
        var url = $"https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl={targetLanguage}&q={encodedText}";
        using var response = await Client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        return new TranslationTestResult
        {
            Status = response.IsSuccessStatusCode ? "Success" : "Failed",
            LatencyMs = stopwatch.ElapsedMilliseconds,
            OutputChars = response.IsSuccessStatusCode ? body.Length : 0,
            ErrorType = response.IsSuccessStatusCode ? string.Empty : response.StatusCode.ToString()
        };
    }

    private static async Task<ProviderResponse> SendAsync(
        ProviderConfig config,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, NormalizeUrl(config.ApiUrl));
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        using var response = await Client.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        var error = ExtractError(responseText);

        return new ProviderResponse
        {
            IsSuccess = response.IsSuccessStatusCode,
            Status = $"{(int)response.StatusCode} {response.StatusCode}",
            FieldNames = ExtractFieldNames(payload),
            MessageSummary = ExtractMessageSummary(payload),
            OutputLength = response.IsSuccessStatusCode ? ExtractAssistantContent(responseText).Length : 0,
            SafeErrorMessage = error.Message,
            ErrorType = error.Type,
            ErrorParam = error.Param,
            ErrorCode = error.Code
        };
    }

    private static object BuildMinimalPayload(ProviderConfig config, string text, string targetLanguage)
    {
        return new
        {
            model = config.ModelName,
            messages = new[]
            {
                new { role = "system", content = $"Translate the following text into {targetLanguage}. Return only the translation." },
                new { role = "user", content = text }
            },
            temperature = config.Temperature
        };
    }

    private static object BuildLegacyPayload(ProviderConfig config, string text, string targetLanguage)
    {
        return new
        {
            model = config.ModelName,
            messages = new[]
            {
                new { role = "system", content = $"Translate the following text into {targetLanguage}. Return only the translation." },
                new { role = "user", content = $"🔤 {text} 🔤" }
            },
            temperature = config.Temperature,
            max_tokens = 128,
            stream = false,
            keep_alive = 600,
            think = false,
            enable_thinking = false,
            reasoning_effort = "low",
            reasoning = new
            {
                exclude = true,
                enabled = false,
                effort = "low"
            },
            thinking = new
            {
                type = "disabled"
            }
        };
    }

    private static ProviderConfig LoadOpenAiConfig(string settingPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(settingPath));
        var root = document.RootElement;
        var index = 0;
        if (root.TryGetProperty("ConfigIndices", out var indices) &&
            indices.TryGetProperty("OpenAI", out var indexElement) &&
            indexElement.TryGetInt32(out var configuredIndex))
        {
            index = configuredIndex;
        }

        var openAi = root.GetProperty("Configs").GetProperty("OpenAI")[index];
        var config = new ProviderConfig
        {
            ApiKey = ReadString(openAi, "ApiKey"),
            ApiUrl = ReadString(openAi, "ApiUrl"),
            ModelName = ReadString(openAi, "ModelName"),
            Temperature = ReadDouble(openAi, "Temperature", 0.7)
        };

        if (string.IsNullOrWhiteSpace(config.ApiKey) ||
            string.IsNullOrWhiteSpace(config.ApiUrl) ||
            string.IsNullOrWhiteSpace(config.ModelName))
        {
            throw new InvalidOperationException("OpenAI-compatible provider is missing ApiKey, ApiUrl, or ModelName.");
        }

        return config;
    }

    private static ErrorFields ExtractError(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                return new ErrorFields
                {
                    Message = Redact(ReadString(error, "message")),
                    Type = Redact(ReadString(error, "type")),
                    Param = Redact(ReadString(error, "param")),
                    Code = Redact(ReadString(error, "code"))
                };
            }
        }
        catch (JsonException)
        {
        }

        return new ErrorFields();
    }

    private static string ExtractAssistantContent(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            var choices = document.RootElement.GetProperty("choices");
            return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<string> ExtractFieldNames(object payload)
    {
        return payload.GetType()
            .GetProperties()
            .Select(property => property.Name)
            .ToList();
    }

    private static string ExtractMessageSummary(object payload)
    {
        var messagesProperty = payload.GetType().GetProperty("messages");
        if (messagesProperty?.GetValue(payload) is not System.Collections.IEnumerable messages)
            return string.Empty;

        var roles = new List<string>();
        foreach (var message in messages)
        {
            var role = message.GetType().GetProperty("role")?.GetValue(message)?.ToString() ?? "";
            var content = message.GetType().GetProperty("content")?.GetValue(message)?.ToString() ?? "";
            roles.Add($"{role}:{content.Length}");
        }

        return string.Join("|", roles);
    }

    private static string NormalizeUrl(string apiUrl)
    {
        var trimmed = apiUrl.Trim().TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + "/chat/completions";
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback = "")
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static double ReadDouble(JsonElement element, string propertyName, double fallback)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetDouble(out var value)
            ? value
            : fallback;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var redacted = System.Text.RegularExpressions.Regex.Replace(value, @"sk-[A-Za-z0-9_-]{20,}", "[REDACTED_API_KEY]");
        return System.Text.RegularExpressions.Regex.Replace(redacted, @"AIza[0-9A-Za-z_-]{20,}", "[REDACTED_API_KEY]");
    }

    private sealed record ProviderConfig
    {
        public string ApiKey { get; init; } = string.Empty;
        public string ApiUrl { get; init; } = string.Empty;
        public string ModelName { get; init; } = string.Empty;
        public double Temperature { get; init; }
    }

    private sealed record ErrorFields
    {
        public string Message { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Param { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
    }

    private sealed record ProviderResponse
    {
        public bool IsSuccess { get; init; }
        public string Status { get; init; } = string.Empty;
        public IReadOnlyList<string> FieldNames { get; init; } = [];
        public string MessageSummary { get; init; } = string.Empty;
        public int OutputLength { get; init; }
        public string SafeErrorMessage { get; init; } = string.Empty;
        public string ErrorType { get; init; } = string.Empty;
        public string ErrorParam { get; init; } = string.Empty;
        public string ErrorCode { get; init; } = string.Empty;
    }

    private sealed record TranslationTestResult
    {
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public long LatencyMs { get; init; }
        public int OutputChars { get; init; }
        public string ErrorType { get; init; } = string.Empty;
        public string ErrorCode { get; init; } = string.Empty;
        public string SafeMessage { get; init; } = string.Empty;
    }
}

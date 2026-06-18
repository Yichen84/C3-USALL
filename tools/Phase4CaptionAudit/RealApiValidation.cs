using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.ClearBridge;

internal static class RealApiValidation
{
    private static readonly HttpClient Client = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> RunAsync(string[] args)
    {
        var settingPath = GetOption(args, "--setting")
            ?? @"D:\USALL\USALL-Git\test-build\ClearBridge-Latest\setting.json";
        var includeFailureTests = args.Contains("--include-failure-tests", StringComparer.OrdinalIgnoreCase);

        var config = LoadOpenAiConfig(settingPath);
        Console.WriteLine($"Provider: OpenAI-compatible");
        Console.WriteLine($"Model: {config.ModelName}");
        Console.WriteLine($"API configuration source: {settingPath}");
        Console.WriteLine("Key exposed: No");

        var results = new List<ApiTestResult>();
        await RunAndRecordAsync(results, "5-25 range", "Dataset A", ClearBridgeOutputLanguages.English, () =>
            AnalyzePreparedAsync(config, DatasetA(), analyzeAll: false, from: 5, to: 25, ClearBridgeOutputLanguages.English));
        await RunAndRecordAsync(results, "120 all", "Dataset B", ClearBridgeOutputLanguages.SimplifiedChinese, () =>
            AnalyzePreparedAsync(config, DatasetB(), analyzeAll: true, from: 1, to: 120, ClearBridgeOutputLanguages.SimplifiedChinese));
        await RunAndRecordAsync(results, "Arabic", "Dataset A", ClearBridgeOutputLanguages.Arabic, () =>
            AnalyzePreparedAsync(config, DatasetA(), analyzeAll: false, from: 5, to: 25, ClearBridgeOutputLanguages.Arabic));
        await RunAndRecordAsync(results, "400", "Dataset C", ClearBridgeOutputLanguages.English, () =>
            AnalyzePreparedAsync(config, DatasetC(), analyzeAll: true, from: 1, to: 400, ClearBridgeOutputLanguages.English));

        results.Add(Test401Blocked());

        await RunAndRecordAsync(results, "No-action", "Dataset E", ClearBridgeOutputLanguages.English, () =>
            AnalyzePreparedAsync(config, DatasetE(), analyzeAll: true, from: 1, to: 12, ClearBridgeOutputLanguages.English));
        await RunAndRecordAsync(results, "Ambiguous", "Dataset F", ClearBridgeOutputLanguages.English, () =>
            AnalyzePreparedAsync(config, DatasetF(), analyzeAll: true, from: 1, to: 8, ClearBridgeOutputLanguages.English));
        await RunAndRecordAsync(results, "Cancel", "Dataset B", ClearBridgeOutputLanguages.English, () =>
            AnalyzePreparedAsync(config, DatasetB(), analyzeAll: true, from: 1, to: 120, ClearBridgeOutputLanguages.English, cancelAfterMs: 100));

        if (includeFailureTests)
        {
            await RunAndRecordAsync(results, "Network error", "Dataset A", ClearBridgeOutputLanguages.English, () =>
                AnalyzePreparedAsync(config with { ApiUrl = "http://127.0.0.1:9/v1/chat/completions" }, DatasetA(), false, 5, 25, ClearBridgeOutputLanguages.English));
            await RunAndRecordAsync(results, "Invalid model", "Dataset A", ClearBridgeOutputLanguages.English, () =>
                AnalyzePreparedAsync(config with { ModelName = "invalid-phase4-validation-model" }, DatasetA(), false, 5, 25, ClearBridgeOutputLanguages.English));
        }

        Console.WriteLine();
        Console.WriteLine("Real API validation summary");
        foreach (var result in results)
        {
            Console.WriteLine(
                $"{result.Name}: {result.Status}; Dataset={result.Dataset}; Language={result.Language}; " +
                $"Scope={result.Scope}; Range={result.Range}; InputSentences={result.InputSentences}; " +
                $"ProcessedSentences={result.ProcessedSentences}; LatencyMs={result.LatencyMs}; " +
                $"OutputChars={result.OutputChars}; SavedHistory={result.SavedHistory}; " +
                $"OutOfRangeEvidence={result.OutOfRangeEvidence}; ErrorType={result.ErrorType}");
        }

        var hardFailures = results.Count(result =>
            result.Name != "400" &&
            result.Name != "Cancel" &&
            result.Name != "Network error" &&
            result.Name != "Invalid model" &&
            result.Status == "Failed");

        return hardFailures == 0 ? 0 : 1;
    }

    private static async Task RunAndRecordAsync(
        List<ApiTestResult> results,
        string name,
        string dataset,
        string language,
        Func<Task<ApiTestResult>> test)
    {
        try
        {
            var result = await test();
            results.Add(result with
            {
                Name = name,
                Dataset = dataset,
                Language = language
            });
        }
        catch (Exception ex)
        {
            results.Add(new ApiTestResult
            {
                Name = name,
                Dataset = dataset,
                Language = language,
                Status = "Failed",
                ErrorType = ex.GetType().Name
            });
        }
    }

    private static async Task<ApiTestResult> AnalyzePreparedAsync(
        ProviderConfig config,
        IReadOnlyList<CaptionAnalysisSentence> sentences,
        bool analyzeAll,
        int from,
        int to,
        string outputLanguage,
        int? cancelAfterMs = null)
    {
        var request = CaptionAnalysisPreprocessor.Prepare(sentences, analyzeAll, from, to);
        using var cts = new CancellationTokenSource();
        if (cancelAfterMs.HasValue)
            cts.CancelAfter(cancelAfterMs.Value);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await SendCaptionAnalysisAsync(config, request.Text, outputLanguage, cts.Token);
            CrisisActionSourceEvidenceSanitizer.KeepOnlyExactSourceEvidence(result, request.Text);
            stopwatch.Stop();

            return new ApiTestResult
            {
                Status = "Success",
                Scope = request.AnalysisScope,
                Range = $"{request.RangeStart}-{request.RangeEnd}",
                InputSentences = request.OriginalSentenceCount,
                ProcessedSentences = request.ProcessedSentenceCount,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                OutputChars = EstimateOutputLength(result),
                SavedHistory = "No",
                OutOfRangeEvidence = HasOutOfRangeEvidence(result, request) ? "Yes" : "No",
                ErrorType = ValidateExpectedResult(result, request, outputLanguage)
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new ApiTestResult
            {
                Status = "Cancelled",
                Scope = request.AnalysisScope,
                Range = $"{request.RangeStart}-{request.RangeEnd}",
                InputSentences = request.OriginalSentenceCount,
                ProcessedSentences = request.ProcessedSentenceCount,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                SavedHistory = "No",
                OutOfRangeEvidence = "NotApplicable",
                ErrorType = string.Empty
            };
        }
        catch (ClearBridgeAnalysisException ex)
        {
            stopwatch.Stop();
            return new ApiTestResult
            {
                Status = "Failed",
                Scope = request.AnalysisScope,
                Range = $"{request.RangeStart}-{request.RangeEnd}",
                InputSentences = request.OriginalSentenceCount,
                ProcessedSentences = request.ProcessedSentenceCount,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                SavedHistory = "No",
                OutOfRangeEvidence = "NotApplicable",
                ErrorType = ex.ErrorCode
            };
        }
    }

    private static ApiTestResult Test401Blocked()
    {
        try
        {
            _ = CaptionAnalysisPreprocessor.Prepare(DatasetD(), analyzeAll: true, fromSentence: 1, toSentence: 401);
            return new ApiTestResult
            {
                Name = "401 blocked",
                Dataset = "Dataset D",
                Language = "Local",
                Status = "Failed",
                InputSentences = 401,
                SavedHistory = "No",
                OutOfRangeEvidence = "NotApplicable",
                ErrorType = "NotBlocked"
            };
        }
        catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "RangeTooLarge")
        {
            return new ApiTestResult
            {
                Name = "401 blocked",
                Dataset = "Dataset D",
                Language = "Local",
                Status = "BlockedLocally",
                Scope = "All",
                Range = "1-401",
                InputSentences = 401,
                SavedHistory = "No",
                OutOfRangeEvidence = "NotApplicable",
                ErrorType = "RangeTooLarge"
            };
        }
    }

    private static async Task<CrisisActionAnalysisResult> SendCaptionAnalysisAsync(
        ProviderConfig config,
        string sourceText,
        string outputLanguage,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(90));

        var requestData = new
        {
            model = config.ModelName,
            temperature = config.Temperature,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = CrisisActionPromptBuilder.BuildCaptionSystemPrompt(outputLanguage) },
                new { role = "user", content = CrisisActionPromptBuilder.BuildCaptionUserPrompt(sourceText) }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, NormalizeUrl(config.ApiUrl));
        request.Content = new StringContent(JsonSerializer.Serialize(requestData, JsonOptions), Encoding.UTF8, "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        using var response = await Client.SendAsync(request, timeoutCts.Token);
        var responseText = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
            throw new ClearBridgeAnalysisException("HttpError", $"HTTP {(int)response.StatusCode}");

        return CrisisActionJsonParser.Parse(ExtractAssistantContent(responseText));
    }

    private static string ExtractAssistantContent(string responseText)
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

        throw new ClearBridgeAnalysisException("InvalidJson", "Provider response did not contain assistant content.");
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

        var openAiConfigs = root.GetProperty("Configs").GetProperty("OpenAI");
        var openAi = openAiConfigs[index];
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

    private static string ValidateExpectedResult(
        CrisisActionAnalysisResult result,
        CaptionAnalysisRequest request,
        string outputLanguage)
    {
        var issues = new List<string>();
        if (HasOutOfRangeEvidence(result, request))
            issues.Add("OutOfRangeEvidence");

        if (request.Text.Contains("submit the worksheet", StringComparison.OrdinalIgnoreCase) &&
            result.Actions.Count == 0)
        {
            issues.Add("MissingWorksheetAction");
        }

        if (request.Text.Contains("Friday, June 19", StringComparison.OrdinalIgnoreCase) &&
            !ContainsInResult(result, "June 19") &&
            !ContainsInResult(result, "Friday") &&
            !ContainsInEvidence(result, "Friday, June 19"))
        {
            issues.Add("MissingDeadline");
        }

        if (request.Text.Contains("Maybe", StringComparison.OrdinalIgnoreCase) &&
            result.UnclearItems.Count == 0)
        {
            issues.Add("MissingUnclearItems");
        }

        if (!request.Text.Contains("submit", StringComparison.OrdinalIgnoreCase) &&
            !request.Text.Contains("worksheet", StringComparison.OrdinalIgnoreCase) &&
            result.Actions.Count > 0)
        {
            issues.Add("InventedAction");
        }

        return issues.Count == 0 ? string.Empty : string.Join(",", issues);
    }

    private static bool HasOutOfRangeEvidence(CrisisActionAnalysisResult result, CaptionAnalysisRequest request)
    {
        foreach (var evidence in result.SourceEvidence)
        {
            if (string.IsNullOrWhiteSpace(evidence.SourceText))
                continue;

            if (!request.Text.Contains(evidence.SourceText.Trim(), StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ContainsInResult(CrisisActionAnalysisResult result, string value)
    {
        return result.Summary.Contains(value, StringComparison.OrdinalIgnoreCase) ||
               result.ImportantPoints.Any(point => point.Contains(value, StringComparison.OrdinalIgnoreCase)) ||
               result.Actions.Any(action =>
                   action.Task.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                   action.Deadline.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                   action.Location.Contains(value, StringComparison.OrdinalIgnoreCase)) ||
               result.UnclearItems.Any(item => item.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsInEvidence(CrisisActionAnalysisResult result, string value)
    {
        return result.SourceEvidence.Any(item =>
            item.SourceText.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            item.Claim.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static int EstimateOutputLength(CrisisActionAnalysisResult result)
    {
        return result.Title.Length +
               result.Summary.Length +
               result.ImportantPoints.Sum(point => point.Length) +
               result.Actions.Sum(action => action.Task.Length + action.Deadline.Length + action.Location.Length) +
               result.UnclearItems.Sum(item => item.Length) +
               result.Warnings.Sum(item => item.Length) +
               result.SourceEvidence.Sum(item => item.Claim.Length + item.SourceText.Length);
    }

    private static IReadOnlyList<CaptionAnalysisSentence> DatasetA()
    {
        var data = GenericDataset(30, "school project meeting");
        data[4] = Caption(5, "The school project meeting today is about the science worksheet.");
        data[9] = Caption(10, "Please submit the worksheet by Friday, June 19 at 5:00 PM.");
        data[14] = Caption(15, "Submit the worksheet in Google Classroom before the deadline.");
        data[19] = Caption(20, "Late submissions may not receive full credit.");
        data[23] = Caption(24, "The room may change if the library is unavailable.");
        data[24] = Caption(25, "Ask the project coordinator if the room changes.");
        return data;
    }

    private static IReadOnlyList<CaptionAnalysisSentence> DatasetB()
    {
        var data = GenericDataset(120, "school project meeting");
        for (var i = 1; i <= 20; i++)
            data[i - 1] = Caption(i, $"Meeting introduction sentence {i} explains the project goals.");
        for (var i = 21; i <= 50; i++)
            data[i - 1] = Caption(i, $"Project background sentence {i} explains the community research topic.");
        data[50] = Caption(51, "The worksheet...");
        data[51] = Caption(52, "The worksheet is due...");
        data[52] = Caption(53, "The worksheet is due Friday, June 19 at 5:00 PM.");
        data[59] = Caption(60, "Please submit the worksheet through Google Classroom.");
        data[66] = Caption(67, "Bring the signed project planning form on Monday, June 22.");
        data[74] = Caption(75, "Meet in Room 214 for the planning session.");
        data[80] = Caption(81, "The room may change if another group reserves the space.");
        data[87] = Caption(88, "The teacher might post an update if the deadline changes.");
        data[94] = Caption(95, "Late submissions may not receive full credit.");
        data[99] = Caption(100, "Late submissions may not receive full credit.");
        data[100] = Caption(101, "Summary sentence 101 restates the project timeline.");
        data[109] = Caption(110, "The planning form...");
        data[110] = Caption(111, "The planning form is required for Monday.");
        return data;
    }

    private static IReadOnlyList<CaptionAnalysisSentence> DatasetC()
    {
        var data = GenericDataset(400, "caption boundary validation");
        for (var i = 50; i <= 400; i += 50)
            data[i - 1] = Caption(i, $"Checkpoint {i}: submit the progress note for checkpoint {i} by Friday, June 19 through Google Classroom.");
        return data;
    }

    private static IReadOnlyList<CaptionAnalysisSentence> DatasetD()
    {
        return GenericDataset(401, "local blocking validation");
    }

    private static IReadOnlyList<CaptionAnalysisSentence> DatasetE()
    {
        return Enumerable.Range(1, 12)
            .Select(i => Caption(i, $"History background sentence {i} explains a concept and gives an example without assigning work."))
            .ToList();
    }

    private static IReadOnlyList<CaptionAnalysisSentence> DatasetF()
    {
        return
        [
            Caption(1, "Maybe submit it next week after the final version is posted."),
            Caption(2, "The room could change if the meeting moves."),
            Caption(3, "Ask the relevant person later before making plans."),
            Caption(4, "The final time has not been confirmed."),
            Caption(5, "I think someone will clarify the details after class."),
            Caption(6, "No confirmed deadline was announced in this part."),
            Caption(7, "No confirmed location was announced in this part."),
            Caption(8, "This selected caption section is intentionally ambiguous.")
        ];
    }

    private static List<CaptionAnalysisSentence> GenericDataset(int count, string topic)
    {
        return Enumerable.Range(1, count)
            .Select(i => Caption(i, $"Synthetic {topic} caption sentence {i} contains predictable non-personal test content."))
            .ToList();
    }

    private static CaptionAnalysisSentence Caption(int number, string sourceText)
    {
        return new CaptionAnalysisSentence
        {
            Number = number,
            SourceText = sourceText,
            Timestamp = $"2026-06-18T01:{number % 60:00}:00Z"
        };
    }

    private static string NormalizeUrl(string apiUrl)
    {
        var trimmed = apiUrl.TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + "/chat/completions";
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
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

    private sealed record ProviderConfig
    {
        public string ApiKey { get; init; } = string.Empty;
        public string ApiUrl { get; init; } = string.Empty;
        public string ModelName { get; init; } = string.Empty;
        public double Temperature { get; init; }
    }

    private sealed record ApiTestResult
    {
        public string Name { get; init; } = string.Empty;
        public string Dataset { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Scope { get; init; } = string.Empty;
        public string Range { get; init; } = string.Empty;
        public int InputSentences { get; init; }
        public int ProcessedSentences { get; init; }
        public long LatencyMs { get; init; }
        public int OutputChars { get; init; }
        public string SavedHistory { get; init; } = "No";
        public string OutOfRangeEvidence { get; init; } = string.Empty;
        public string ErrorType { get; init; } = string.Empty;
    }
}

using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.ClearBridge;
using LiveCaptionsTranslator.utils;

var tests = new List<(string Name, Func<Task> Test)>
{
    ("Three batches evolve compressed context", TestThreeBatchesAsync),
    ("Three batches correct superseded facts", TestThreeBatchCorrectionAsync),
    ("New captions are consumed once only after success", TestConsumeOnceAsync),
    ("Minimum threshold blocks tiny batches", TestMinimumThresholdAsync),
    ("Cancel rollback keeps captions pending", TestCancelRollbackAsync),
    ("Provider failure keeps captions pending", TestProviderFailureRollbackAsync),
    ("Concurrent processing is rejected", TestConcurrentProcessingAsync),
    ("Pause and stop prevent processing", TestPauseAndStopAsync),
    ("Ten batches keep cache bounded", TestTenBatchCacheBoundAsync),
    ("Mock supports English Chinese Arabic", TestMockLanguagesAsync),
    ("Confirmed history metadata is explicit", TestConfirmedHistoryMetadataAsync),
    ("Source evidence is limited to current batch", TestSourceEvidenceCurrentBatchOnlyAsync),
    ("Rolling JSON parser tolerates null fields", TestParserNullFieldsAsync),
    ("Rolling JSON parser extracts fenced or wrapped JSON", TestParserWrappedJsonAsync),
    ("Rolling JSON parser rejects invalid JSON", TestParserInvalidJsonAsync)
};

var failures = 0;
foreach (var (name, test) in tests)
{
    try
    {
        await test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

Console.WriteLine(failures == 0
    ? $"PASS Phase 5 rolling summary audit completed {tests.Count} checks."
    : $"FAIL Phase 5 rolling summary audit found {failures} failing check(s).");

if (failures == 0 && args.Any(arg => string.Equals(arg, "--real-api", StringComparison.OrdinalIgnoreCase)))
{
    try
    {
        await RunRealApiValidationAsync(args);
        Console.WriteLine("PASS Phase 5 real API validation completed with synthetic captions only.");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL Phase 5 real API validation: {ex.GetType().Name} {ex.Message}");
    }
}

return failures == 0 ? 0 : 1;

static async Task TestThreeBatchesAsync()
{
    var service = new RollingSummarySessionService();
    service.Start();
    var captions = BuildBatch(1, 8, "Today we are introducing the science project and collecting details.");
    var first = await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);
    AssertEqual(1, first.Request.BatchNumber, "First batch number.");
    AssertEqual(first.Request.RangeEnd, service.ContextCache.LastProcessedSentenceNumber, "First batch cursor.");

    captions.AddRange(BuildBatch(9, 8, "Submit the worksheet Friday at 5 PM on Google Classroom."));
    var second = await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);
    AssertEqual(2, second.Request.BatchNumber, "Second batch number.");
    Assert(second.Result.NewActions.Count > 0, "Second batch should include an action.");

    captions.AddRange(BuildBatch(17, 8, "Correction: the meeting has moved from Room 204 to Room 310."));
    var third = await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);
    AssertEqual(3, third.Request.BatchNumber, "Third batch number.");
    Assert(third.Result.ContextCache.Locations.Contains("Room 310"), "Corrected location should be retained.");
}

static async Task TestThreeBatchCorrectionAsync()
{
    var service = new RollingSummarySessionService();
    service.Start();
    var captions = BuildBatch(1, 8, "The speaker introduced the accessibility project and said some details were still unclear.");
    await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);

    captions.AddRange(BuildBatch(9, 8, "Submit the worksheet Friday at 5 PM on Google Classroom."));
    await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);

    captions.AddRange(BuildBatch(17, 8, "Correction: submit it Monday at 10 AM on the project portal. The final room is not confirmed."));
    var third = await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);
    var cache = third.Result.ContextCache;

    Assert(cache.DatesAndDeadlines.Any(item => item.Contains("Monday", StringComparison.OrdinalIgnoreCase)), "Corrected Monday deadline should be retained.");
    Assert(!cache.DatesAndDeadlines.Any(item => item.Contains("Friday", StringComparison.OrdinalIgnoreCase)), "Superseded Friday deadline should be removed.");
    Assert(cache.Locations.Any(item => item.Contains("project portal", StringComparison.OrdinalIgnoreCase)), "Corrected project portal location should be retained.");
    Assert(!cache.Locations.Any(item => item.Contains("Google Classroom", StringComparison.OrdinalIgnoreCase)), "Superseded Google Classroom location should be removed.");
    Assert(cache.UnresolvedQuestions.Any(item => item.Contains("room", StringComparison.OrdinalIgnoreCase)), "Unconfirmed room should remain unresolved.");
}

static async Task TestConsumeOnceAsync()
{
    var service = new RollingSummarySessionService();
    service.Start();
    var captions = BuildBatch(1, 6, "The worksheet is due Friday at 5 PM.");
    await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);

    try
    {
        service.CreatePendingRequest(captions);
        throw new InvalidOperationException("Expected pending request to be blocked.");
    }
    catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "WaitingForContent")
    {
    }
}

static Task TestMinimumThresholdAsync()
{
    var service = new RollingSummarySessionService();
    service.Start();
    var captions = BuildBatch(1, 1, "Short.");

    try
    {
        service.CreatePendingRequest(captions);
        throw new InvalidOperationException("Expected tiny batch to be blocked.");
    }
    catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "WaitingForContent")
    {
        return Task.CompletedTask;
    }
}

static async Task TestCancelRollbackAsync()
{
    var service = new RollingSummarySessionService();
    service.Start();
    var captions = BuildBatch(1, 8, "The teacher assigned a worksheet and said to check the portal.");
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    try
    {
        await service.ProcessPendingAsync(captions, "Mock", "English", cts.Token);
        throw new InvalidOperationException("Expected cancellation.");
    }
    catch (OperationCanceledException)
    {
    }

    var retry = await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);
    AssertEqual(1, retry.Request.BatchNumber, "Cancelled batch should remain pending.");
}

static async Task TestProviderFailureRollbackAsync()
{
    var provider = new FailOnceRollingSummaryProvider("NetworkError");
    var service = new RollingSummarySessionService(provider, provider);
    service.Start();
    var captions = BuildBatch(1, 8, "The worksheet is due Friday at 5 PM and students should review the project portal.");

    try
    {
        await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);
        throw new InvalidOperationException("Expected provider failure.");
    }
    catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "NetworkError")
    {
    }

    AssertEqual(0, service.ContextCache.LastProcessedSentenceNumber, "Failed provider request should not advance cursor.");
    AssertEqual(0, service.ContextCache.BatchCount, "Failed provider request should not advance batch count.");

    var retry = await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);
    AssertEqual(1, retry.Request.BatchNumber, "Retry should still process the first batch.");
}

static async Task TestConcurrentProcessingAsync()
{
    var provider = new SlowRollingSummaryProvider();
    var service = new RollingSummarySessionService(provider, provider);
    service.Start();
    var captions = BuildBatch(1, 8, "The workshop requires a reflection response and students should watch the portal.");

    var first = service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);
    await provider.WaitUntilStartedAsync();

    try
    {
        await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);
        throw new InvalidOperationException("Expected concurrent request to be rejected.");
    }
    catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "AlreadyProcessing")
    {
    }

    provider.Release();
    await first;
}

static async Task TestPauseAndStopAsync()
{
    var service = new RollingSummarySessionService();
    service.Start();
    service.Pause();
    await AssertThrowsClearBridgeAsync(
        () => service.ProcessPendingAsync(BuildBatch(1, 8, "The activity has a Friday deadline and a portal update."), "Mock", "English", CancellationToken.None),
        "RollingSummaryNotRunning",
        "Paused service should not consume captions.");

    service.Resume();
    service.Stop();
    await AssertThrowsClearBridgeAsync(
        () => service.ProcessPendingAsync(BuildBatch(1, 8, "The activity has a Friday deadline and a portal update."), "Mock", "English", CancellationToken.None),
        "RollingSummaryNotRunning",
        "Stopped service should not consume captions.");
}

static async Task TestTenBatchCacheBoundAsync()
{
    var service = new RollingSummarySessionService();
    service.Start();
    var captions = new List<CaptionAnalysisSentence>();
    for (var batch = 0; batch < 10; batch++)
    {
        captions.AddRange(BuildBatch(batch * 8 + 1, 8, $"Batch {batch + 1} includes a Friday deadline and an unresolved question."));
        await service.ProcessPendingAsync(captions, "Mock", "English", CancellationToken.None);
    }

    var cache = service.ContextCache;
    Assert(cache.BatchCount == 10, "Batch count should reach 10.");
    Assert(cache.CompressedNarrative.Length <= 2500, "Compressed narrative should be bounded.");
    Assert(cache.EstablishedFacts.Count <= 20, "Facts list should be bounded.");
}

static async Task TestMockLanguagesAsync()
{
    foreach (var language in new[] { "English", "Simplified Chinese", "Arabic" })
    {
        var service = new RollingSummarySessionService();
        service.Start();
        var result = await service.ProcessPendingAsync(
            BuildBatch(1, 8, "The project starts today and students should watch for updates."),
            "Mock",
            language,
            CancellationToken.None);
        Assert(!string.IsNullOrWhiteSpace(result.Result.BatchSummary), $"Summary missing for {language}.");
    }
}

static async Task TestConfirmedHistoryMetadataAsync()
{
    var service = new RollingSummarySessionService();
    service.Start();
    var outcome = await service.ProcessPendingAsync(
        BuildBatch(1, 8, "Students should submit the reflection by Friday and note any unclear requirements."),
        "Mock",
        "English",
        CancellationToken.None);

    var json = service.BuildConfirmedHistoryJson(outcome);
    using var document = JsonDocument.Parse(json);
    var root = document.RootElement;
    Assert(root.GetProperty("temporary_context_persisted").GetBoolean() == false, "History metadata should say temporary context is not persisted.");
    Assert(root.GetProperty("user_confirmed").GetBoolean(), "History metadata should require user confirmation.");
    Assert(root.GetProperty("batch_count").GetInt32() == 1, "History metadata should include batch count.");
}

static Task TestSourceEvidenceCurrentBatchOnlyAsync()
{
    var result = new CrisisActionAnalysisResult
    {
        SourceEvidence =
        [
            new SourceEvidenceItem
            {
                Claim = "Old deadline",
                SourceText = "Friday at 5 PM"
            },
            new SourceEvidenceItem
            {
                Claim = "Current correction",
                SourceText = "Monday at 10 AM"
            }
        ]
    };

    CrisisActionSourceEvidenceSanitizer.KeepOnlyExactSourceEvidence(
        result,
        "[17] Correction: submit it Monday at 10 AM on the project portal.");

    Assert(result.SourceEvidence.Count == 1, "Only current batch evidence should remain.");
    Assert(result.SourceEvidence[0].SourceText == "Monday at 10 AM", "Current batch evidence should be preserved.");
    return Task.CompletedTask;
}

static Task TestParserNullFieldsAsync()
{
    var result = RollingSummaryJsonParser.Parse("{\"current_topic\":null,\"batch_summary\":null,\"key_points\":null,\"new_actions\":null,\"context_cache\":null}");
    Assert(!string.IsNullOrWhiteSpace(result.CurrentTopic), "Parser should default topic.");
    Assert(result.NewActions.Count == 0, "Null actions should normalize to empty.");
    return Task.CompletedTask;
}

static Task TestParserWrappedJsonAsync()
{
    var result = RollingSummaryJsonParser.Parse("Here is the JSON:\n{\"current_topic\":\"Wrapped\",\"batch_summary\":\"Parsed\",\"context_cache\":{\"current_topic\":\"Wrapped\",\"compressed_narrative\":\"Parsed\"}}\nDone.");
    AssertEqual("Wrapped", result.CurrentTopic, "Parser should extract the first complete JSON object.");
    return Task.CompletedTask;
}

static Task TestParserInvalidJsonAsync()
{
    try
    {
        RollingSummaryJsonParser.Parse("{ not json");
        throw new InvalidOperationException("Expected invalid JSON.");
    }
    catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "InvalidJson")
    {
        return Task.CompletedTask;
    }
}

static async Task RunRealApiValidationAsync(string[] arguments)
{
    var settingsPath = arguments
        .FirstOrDefault(arg => arg.StartsWith("--settings=", StringComparison.OrdinalIgnoreCase))?
        .Split('=', 2)[1];
    settingsPath = string.IsNullOrWhiteSpace(settingsPath)
        ? Path.Combine(Environment.CurrentDirectory, "test-build", "ClearBridge-Latest", "setting.json")
        : settingsPath;

    var config = ReadOpenAiSettings(settingsPath);
    Console.WriteLine("REAL_API Config=OpenAI HasApiKey=True Model=present Url=present");

    using var client = new HttpClient();
    var context = new RollingContextCache();
    var captions = new List<CaptionAnalysisSentence>();

    var batches = new[]
    {
        "The speaker introduced the accessibility project. Some requirements are still unclear. Students should watch for updates.",
        "Submit the worksheet Friday at 5 PM on Google Classroom. Late submissions may not be reviewed.",
        "Correction: submit it Monday at 10 AM on the project portal. The final room is not confirmed."
    };

    for (var index = 0; index < batches.Length; index++)
    {
        captions.AddRange(BuildBatch(index * 8 + 1, 8, batches[index]));
        var request = BuildRollingRequest(captions, context);
        var result = await SendRealRollingRequestAsync(client, config, request, "English", CancellationToken.None);
        context = result.ContextCache.Clone();
        context.LastProcessedSentenceNumber = request.RangeEnd;
        context.BatchCount = request.BatchNumber;
        RollingSummaryJsonParser.ClampContext(context);

        Console.WriteLine(
            $"REAL_API Batch={request.BatchNumber} Status=Success InputSentences={request.ProcessedSentenceCount} " +
            $"InputChars={request.CharacterCount} OutputActions={result.NewActions.Count} " +
            $"EvidenceItems={result.SourceEvidence.Count} CacheChars={context.CompressedNarrative.Length}");
    }

    var correctedDeadline = context.DatesAndDeadlines.Any(item => item.Contains("Monday", StringComparison.OrdinalIgnoreCase));
    var supersededDeadlineRemoved = !context.DatesAndDeadlines.Any(item => item.Contains("Friday", StringComparison.OrdinalIgnoreCase));
    var correctedLocation = context.Locations.Any(item => item.Contains("project portal", StringComparison.OrdinalIgnoreCase));
    var supersededLocationRemoved = !context.Locations.Any(item => item.Contains("Google Classroom", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine(
        $"REAL_API CorrectionHandling Monday={correctedDeadline} FridaySuperseded={supersededDeadlineRemoved} " +
        $"ProjectPortal={correctedLocation} GoogleClassroomSuperseded={supersededLocationRemoved}");

    await ValidateRealLanguageAsync(client, config, "Simplified Chinese");
    await ValidateRealLanguageAsync(client, config, "Arabic");
    await ValidateRealFailureDoesNotAdvanceAsync();
}

static async Task ValidateRealLanguageAsync(HttpClient client, OpenAiAuditSettings config, string outputLanguage)
{
    Console.WriteLine($"REAL_API Language={outputLanguage} Status=Starting");
    var captions = BuildBatch(1, 8, "Students should submit the worksheet by Friday and ask the coordinator if anything is unclear.");
    var request = BuildRollingRequest(captions, new RollingContextCache());
    var result = await SendRealRollingRequestAsync(client, config, request, outputLanguage, CancellationToken.None);
    Console.WriteLine(
        $"REAL_API Language={outputLanguage} Status=Success InputSentences={request.ProcessedSentenceCount} " +
        $"OutputChars={result.BatchSummary.Length} Actions={result.NewActions.Count}");
}

static async Task ValidateRealFailureDoesNotAdvanceAsync()
{
    var provider = new FailOnceRollingSummaryProvider("HttpError");
    var service = new RollingSummarySessionService(provider, provider);
    service.Start();
    var captions = BuildBatch(1, 8, "Students should submit the worksheet by Friday and check the project portal.");

    try
    {
        await service.ProcessPendingAsync(captions, "OpenAI-compatible", "English", CancellationToken.None);
        throw new InvalidOperationException("Expected synthetic HTTP failure.");
    }
    catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "HttpError")
    {
    }

    AssertEqual(0, service.ContextCache.LastProcessedSentenceNumber, "Synthetic HTTP failure should not advance cursor.");
    AssertEqual(0, service.ContextCache.BatchCount, "Synthetic HTTP failure should not advance batch.");
    Console.WriteLine("REAL_API FailureRollback Status=Pass Cursor=0 Batch=0");
}

static async Task<RollingSummaryResult> SendRealRollingRequestAsync(
    HttpClient client,
    OpenAiAuditSettings config,
    RollingSummaryRequest request,
    string outputLanguage,
    CancellationToken cancellationToken)
{
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
    var started = Stopwatch.StartNew();
    var previousContextJson = JsonSerializer.Serialize(request.PreviousContext, AuditJson.Options);
    var systemPrompt = CrisisActionPromptBuilder.BuildRollingSummarySystemPrompt(outputLanguage);
    var userPrompt = CrisisActionPromptBuilder.BuildRollingSummaryUserPrompt(previousContextJson, request.BatchTranscript);
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

    var content = await SendAuditChatRequestAsync(client, config, requestData, timeoutCts.Token);
    RollingSummaryResult result;
    try
    {
        result = RollingSummaryJsonParser.Parse(content);
    }
    catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode is "InvalidJson" or "EmptyResponse")
    {
        Console.WriteLine($"REAL_API RetryReason={ex.ErrorCode} Batch={request.BatchNumber} OutputChars={content.Length}");
        var retryRequestData = new
        {
            model = config.ModelName,
            temperature = config.Temperature,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new
                {
                    role = "user",
                    content = userPrompt +
                        "\n\nThe previous response was not valid JSON. Return only one complete parseable JSON object. " +
                        "Use the exact English snake_case property names from the schema. Do not translate JSON keys. " +
                        "Use standard double-quoted JSON strings and escape quotes, backslashes, and line breaks."
                }
            }
        };
        content = await SendAuditChatRequestAsync(client, config, retryRequestData, timeoutCts.Token);
        result = RollingSummaryJsonParser.Parse(content);
    }
    result.SourceEvidence = result.SourceEvidence
        .Where(item =>
            string.IsNullOrWhiteSpace(item.SourceText) ||
            request.BatchTranscript.Contains(item.SourceText.Trim(), StringComparison.Ordinal))
        .Select(item => new SourceEvidenceItem
        {
            Claim = item.Claim,
            SourceText = item.SourceText.Trim()
        })
        .ToList();
    RollingSummaryJsonParser.ClampContext(result.ContextCache);
    Console.WriteLine(
        $"REAL_API Request Batch={request.BatchNumber} LatencyMs={started.ElapsedMilliseconds} " +
        $"InputChars={request.CharacterCount} OutputChars={content.Length}");
    return result;
}

static async Task<string> SendAuditChatRequestAsync(
    HttpClient client,
    OpenAiAuditSettings config,
    object requestData,
    CancellationToken cancellationToken)
{
    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, TextUtil.NormalizeUrl(config.ApiUrl));
    httpRequest.Content = new StringContent(JsonSerializer.Serialize(requestData, AuditJson.Options), Encoding.UTF8, "application/json");
    httpRequest.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

    using var response = await client.SendAsync(httpRequest, cancellationToken);
    var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
        throw new ClearBridgeAnalysisException("HttpError", $"HTTP {(int)response.StatusCode} ({response.StatusCode}).");

    return ExtractAssistantContent(responseText);
}

static RollingSummaryRequest BuildRollingRequest(
    IReadOnlyList<CaptionAnalysisSentence> allSentences,
    RollingContextCache previousContext)
{
    var pending = allSentences
        .Where(sentence => sentence.Number > previousContext.LastProcessedSentenceNumber)
        .OrderBy(sentence => sentence.Number)
        .Select(CloneSentence)
        .ToList();
    var processed = CaptionAnalysisPreprocessor.RemoveConsecutiveDuplicateCaptions(pending).ToList();
    var transcript = string.Join(
        Environment.NewLine,
        processed.Select(sentence => $"[{sentence.Number}] {sentence.SourceText.Trim()}"));

    return new RollingSummaryRequest
    {
        BatchNumber = previousContext.BatchCount + 1,
        Sentences = pending,
        ProcessedSentences = processed,
        PreviousContext = previousContext.Clone(),
        BatchTranscript = transcript,
        OriginalSentenceCount = pending.Count,
        ProcessedSentenceCount = processed.Count,
        CharacterCount = transcript.Length,
        RangeStart = pending.First().Number,
        RangeEnd = pending.Last().Number
    };
}

static string ExtractAssistantContent(string responseText)
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

    throw new ClearBridgeAnalysisException("InvalidJson", "The provider response did not include choices[0].message.content.");
}

static OpenAiAuditSettings ReadOpenAiSettings(string settingsPath)
{
    if (!File.Exists(settingsPath))
        throw new FileNotFoundException("Fixed package setting.json was not found.", settingsPath);

    using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
    var configs = document.RootElement.GetProperty("Configs");
    var openAi = configs.GetProperty("OpenAI");
    var config = openAi.ValueKind == JsonValueKind.Array
        ? openAi.EnumerateArray().FirstOrDefault()
        : openAi;

    var apiKey = GetRequiredString(config, "ApiKey");
    var apiUrl = GetRequiredString(config, "ApiUrl");
    var modelName = GetRequiredString(config, "ModelName");
    var temperature = config.TryGetProperty("Temperature", out var temperatureElement) &&
        temperatureElement.TryGetDouble(out var parsedTemperature)
            ? parsedTemperature
            : 1.0;

    return new OpenAiAuditSettings(apiKey, apiUrl, modelName, temperature);
}

static string GetRequiredString(JsonElement element, string propertyName)
{
    if (element.ValueKind == JsonValueKind.Undefined ||
        !element.TryGetProperty(propertyName, out var property) ||
        string.IsNullOrWhiteSpace(property.GetString()))
    {
        throw new ClearBridgeAnalysisException("ProviderNotConfigured", $"OpenAI {propertyName} is missing.");
    }

    return property.GetString()!;
}

static CaptionAnalysisSentence CloneSentence(CaptionAnalysisSentence sentence)
{
    return new CaptionAnalysisSentence
    {
        Number = sentence.Number,
        SourceText = sentence.SourceText,
        TranslatedText = sentence.TranslatedText,
        Timestamp = sentence.Timestamp
    };
}

static List<CaptionAnalysisSentence> BuildBatch(int start, int count, string text)
{
    return Enumerable.Range(start, count)
        .Select(number => new CaptionAnalysisSentence
        {
            Number = number,
            SourceText = $"{text} Sentence {number}.",
            Timestamp = DateTime.Now.AddSeconds(number).ToString("yyyy-MM-dd HH:mm:ss")
        })
        .ToList();
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
}

static async Task AssertThrowsClearBridgeAsync(
    Func<Task> action,
    string errorCode,
    string message)
{
    try
    {
        await action();
        throw new InvalidOperationException(message);
    }
    catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == errorCode)
    {
    }
}

sealed class FailOnceRollingSummaryProvider(string errorCode) : IRollingSummaryProvider
{
    private bool hasFailed;

    public string Name => "Failing Test Provider";

    public async Task<RollingSummaryResult> AnalyzeBatchAsync(
        RollingSummaryRequest request,
        string outputLanguage,
        CancellationToken cancellationToken)
    {
        if (!hasFailed)
        {
            hasFailed = true;
            throw new ClearBridgeAnalysisException(errorCode, "Synthetic provider failure.");
        }

        return await new MockRollingSummaryProvider().AnalyzeBatchAsync(request, outputLanguage, cancellationToken);
    }
}

sealed class SlowRollingSummaryProvider : IRollingSummaryProvider
{
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Name => "Slow Test Provider";

    public Task WaitUntilStartedAsync() => started.Task;

    public void Release() => release.TrySetResult();

    public async Task<RollingSummaryResult> AnalyzeBatchAsync(
        RollingSummaryRequest request,
        string outputLanguage,
        CancellationToken cancellationToken)
    {
        started.TrySetResult();
        await release.Task.WaitAsync(cancellationToken);
        return await new MockRollingSummaryProvider().AnalyzeBatchAsync(request, outputLanguage, cancellationToken);
    }
}

sealed record OpenAiAuditSettings(
    string ApiKey,
    string ApiUrl,
    string ModelName,
    double Temperature);

static class AuditJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}

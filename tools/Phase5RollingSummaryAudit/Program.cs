using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.ClearBridge;

var tests = new List<(string Name, Func<Task> Test)>
{
    ("Three batches evolve compressed context", TestThreeBatchesAsync),
    ("New captions are consumed once only after success", TestConsumeOnceAsync),
    ("Minimum threshold blocks tiny batches", TestMinimumThresholdAsync),
    ("Cancel rollback keeps captions pending", TestCancelRollbackAsync),
    ("Ten batches keep cache bounded", TestTenBatchCacheBoundAsync),
    ("Mock supports English Chinese Arabic", TestMockLanguagesAsync),
    ("Rolling JSON parser tolerates null fields", TestParserNullFieldsAsync),
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

static Task TestParserNullFieldsAsync()
{
    var result = RollingSummaryJsonParser.Parse("{\"current_topic\":null,\"batch_summary\":null,\"key_points\":null,\"new_actions\":null,\"context_cache\":null}");
    Assert(!string.IsNullOrWhiteSpace(result.CurrentTopic), "Parser should default topic.");
    Assert(result.NewActions.Count == 0, "Null actions should normalize to empty.");
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

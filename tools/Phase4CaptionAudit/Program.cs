using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.ClearBridge;

if (args.Contains("--real-api", StringComparer.OrdinalIgnoreCase))
    return await RealApiValidation.RunAsync(args);

var tests = new List<(string Name, Func<Task> Test)>
{
    ("Range boundaries are inclusive", TestRangeBoundariesAsync),
    ("400 sentence limit allows 400 and blocks 401", TestSentenceLimitAsync),
    ("Caption snapshot request is immutable", TestSnapshotImmutabilityAsync),
    ("Conservative duplicate and incremental caption handling", TestDeduplicationAsync),
    ("Mock caption provider respects selected text and languages", TestMockProviderAsync),
    ("Mock no-action captions do not invent actions", TestNoActionMockAsync),
    ("Mock ambiguous captions produce unclear items", TestAmbiguousMockAsync),
    ("JSON parser handles invalid and partial provider output", TestJsonParserAsync),
    ("Caption evidence sanitizer removes unsupported source text", TestEvidenceSanitizerAsync),
    ("Cancellation is surfaced without producing a result", TestCancellationAsync)
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
    ? $"PASS Phase 4 caption audit harness completed {tests.Count} checks."
    : $"FAIL Phase 4 caption audit harness found {failures} failing check(s).");

return failures == 0 ? 0 : 1;

static Task TestRangeBoundariesAsync()
{
    var dataset = BuildDatasetA(120);

    var all = CaptionAnalysisPreprocessor.Prepare(dataset, analyzeAll: true, fromSentence: 1, toSentence: 120);
    AssertEqual(120, all.OriginalSentenceCount, "Analyze All should include all 120 captions.");

    var range = CaptionAnalysisPreprocessor.Prepare(dataset, analyzeAll: false, fromSentence: 20, toSentence: 70);
    AssertEqual(51, range.OriginalSentenceCount, "Range 20-70 should include 51 captions.");
    AssertEqual(20, range.RangeStart, "RangeStart should be inclusive.");
    AssertEqual(70, range.RangeEnd, "RangeEnd should be inclusive.");
    Assert(range.Text.Contains("[20]", StringComparison.Ordinal), "Request text should include sentence 20.");
    Assert(range.Text.Contains("[70]", StringComparison.Ordinal), "Request text should include sentence 70.");
    Assert(!range.Text.Contains("[19]", StringComparison.Ordinal), "Request text should exclude sentence 19.");
    Assert(!range.Text.Contains("[71]", StringComparison.Ordinal), "Request text should exclude sentence 71.");

    var firstOnly = CaptionAnalysisPreprocessor.Prepare(dataset, analyzeAll: false, fromSentence: 1, toSentence: 1);
    AssertEqual(1, firstOnly.OriginalSentenceCount, "Range 1-1 should include exactly one caption.");

    var lastOnly = CaptionAnalysisPreprocessor.Prepare(dataset, analyzeAll: false, fromSentence: 120, toSentence: 120);
    AssertEqual(1, lastOnly.OriginalSentenceCount, "Range 120-120 should include exactly one caption.");

    return Task.CompletedTask;
}

static Task TestSentenceLimitAsync()
{
    var dataset400 = BuildGenericDataset(400);
    var all400 = CaptionAnalysisPreprocessor.Prepare(dataset400, analyzeAll: true, fromSentence: 1, toSentence: 400);
    AssertEqual(400, all400.OriginalSentenceCount, "Analyze All should allow exactly 400 captions.");

    var range400 = CaptionAnalysisPreprocessor.Prepare(dataset400, analyzeAll: false, fromSentence: 1, toSentence: 400);
    AssertEqual(400, range400.OriginalSentenceCount, "Range 1-400 should allow exactly 400 captions.");

    var range351 = CaptionAnalysisPreprocessor.Prepare(dataset400, analyzeAll: false, fromSentence: 50, toSentence: 400);
    AssertEqual(351, range351.OriginalSentenceCount, "Range 50-400 should include 351 captions.");

    var dataset401 = BuildGenericDataset(401);
    ExpectClearBridgeError(
        "RangeTooLarge",
        () => CaptionAnalysisPreprocessor.Prepare(dataset401, analyzeAll: true, fromSentence: 1, toSentence: 401),
        "Analyze All should block 401 captions.");

    var oneTo400 = CaptionAnalysisPreprocessor.Prepare(dataset401, analyzeAll: false, fromSentence: 1, toSentence: 400);
    AssertEqual(400, oneTo400.OriginalSentenceCount, "Range 1-400 should be allowed inside a 401-caption set.");

    var twoTo401 = CaptionAnalysisPreprocessor.Prepare(dataset401, analyzeAll: false, fromSentence: 2, toSentence: 401);
    AssertEqual(400, twoTo401.OriginalSentenceCount, "Range 2-401 should be allowed inside a 401-caption set.");

    ExpectClearBridgeError(
        "RangeTooLarge",
        () => CaptionAnalysisPreprocessor.Prepare(dataset401, analyzeAll: false, fromSentence: 1, toSentence: 401),
        "Range 1-401 should be blocked.");

    return Task.CompletedTask;
}

static Task TestSnapshotImmutabilityAsync()
{
    var live = BuildDatasetA(120);
    var snapshot = live
        .Select(sentence => new CaptionAnalysisSentence
        {
            Number = sentence.Number,
            SourceText = sentence.SourceText,
            TranslatedText = sentence.TranslatedText,
            Timestamp = sentence.Timestamp
        })
        .ToList();

    var request = CaptionAnalysisPreprocessor.Prepare(snapshot, analyzeAll: false, fromSentence: 20, toSentence: 70);
    live[19] = new CaptionAnalysisSentence
    {
        Number = 20,
        SourceText = "MUTATED CAPTION SHOULD NOT ENTER REQUEST",
        Timestamp = "changed"
    };
    live.Add(new CaptionAnalysisSentence
    {
        Number = 121,
        SourceText = "Late caption should not enter the existing snapshot.",
        Timestamp = "late"
    });

    Assert(!request.Text.Contains("MUTATED", StringComparison.Ordinal), "Existing request text should not change after live list mutation.");
    Assert(!request.Text.Contains("[121]", StringComparison.Ordinal), "Existing request text should not include captions added later.");
    return Task.CompletedTask;
}

static Task TestDeduplicationAsync()
{
    var dataset = new List<CaptionAnalysisSentence>
    {
        Caption(1, "The worksheet..."),
        Caption(2, "The worksheet is due..."),
        Caption(3, "The worksheet is due Friday."),
        Caption(4, "The worksheet is due Friday."),
        Caption(5, "Submit it on Google Classroom.")
    };

    var request = CaptionAnalysisPreprocessor.Prepare(dataset, analyzeAll: true, fromSentence: 1, toSentence: 5);
    AssertEqual(2, request.ProcessedSentenceCount, "Incremental captions and exact duplicates should reduce to two retained captions.");
    AssertEqual("The worksheet is due Friday.", request.ProcessedSentences[0].SourceText, "The most complete worksheet sentence should be retained.");
    AssertEqual("Submit it on Google Classroom.", request.ProcessedSentences[1].SourceText, "The Google Classroom sentence should be retained.");
    Assert(request.Text.Contains("Friday", StringComparison.Ordinal), "Friday should not be lost.");
    Assert(request.Text.Contains("Google Classroom", StringComparison.Ordinal), "Google Classroom should not be lost.");
    return Task.CompletedTask;
}

static async Task TestMockProviderAsync()
{
    var request = CaptionAnalysisPreprocessor.Prepare(BuildDatasetA(120), analyzeAll: false, fromSentence: 20, toSentence: 70);
    var provider = new MockCaptionCrisisActionAnalysisProvider();

    foreach (var language in new[]
             {
                 ClearBridgeOutputLanguages.English,
                 ClearBridgeOutputLanguages.SimplifiedChinese,
                 ClearBridgeOutputLanguages.Arabic
             })
    {
        var result = await provider.AnalyzeAsync(request.Text, language, CancellationToken.None);
        Assert(result.Actions.Count > 0, $"Mock result should include the explicit action for {language}.");
        Assert(result.SourceEvidence.Count > 0, $"Mock result should include evidence for {language}.");
        AssertEvidenceWithinRequest(result, request.Text);
    }
}

static async Task TestNoActionMockAsync()
{
    var request = CaptionAnalysisPreprocessor.Prepare(BuildNoActionDataset(), analyzeAll: true, fromSentence: 1, toSentence: 6);
    var provider = new MockCaptionCrisisActionAnalysisProvider();
    var result = await provider.AnalyzeAsync(request.Text, ClearBridgeOutputLanguages.English, CancellationToken.None);

    AssertEqual(0, result.Actions.Count, "No-action classroom content should not create a checklist item.");
    Assert(!result.Summary.Contains("Friday", StringComparison.OrdinalIgnoreCase), "No-action content should not invent a Friday deadline.");
    AssertEvidenceWithinRequest(result, request.Text);
}

static async Task TestAmbiguousMockAsync()
{
    var request = CaptionAnalysisPreprocessor.Prepare(BuildAmbiguousDataset(), analyzeAll: true, fromSentence: 1, toSentence: 3);
    var provider = new MockCaptionCrisisActionAnalysisProvider();
    var result = await provider.AnalyzeAsync(request.Text, ClearBridgeOutputLanguages.English, CancellationToken.None);

    Assert(result.UnclearItems.Count > 0, "Ambiguous captions should produce unclear items.");
    Assert(result.Actions.Count == 0 || result.Actions.All(action =>
        string.IsNullOrWhiteSpace(action.Deadline) &&
        string.IsNullOrWhiteSpace(action.Location)), "Ambiguous captions should not create firm deadlines or locations.");
    AssertEvidenceWithinRequest(result, request.Text);
}

static Task TestJsonParserAsync()
{
    ExpectClearBridgeError("EmptyResponse", () => CrisisActionJsonParser.Parse(""), "Empty provider responses should be rejected.");
    ExpectClearBridgeError("InvalidJson", () => CrisisActionJsonParser.Parse("not json"), "Non-JSON provider responses should be rejected.");
    ExpectClearBridgeError("InvalidJson", () => CrisisActionJsonParser.Parse("{\"title\":\"Broken\""), "Truncated JSON should be rejected.");

    var missingFields = CrisisActionJsonParser.Parse("{}");
    AssertEqual("ClearBridge Analysis", missingFields.Title, "Missing title should receive a safe default.");
    AssertEqual("No summary was returned.", missingFields.Summary, "Missing summary should receive a safe default.");
    AssertEqual("medium", missingFields.Priority, "Missing priority should default to medium.");

    var normalized = CrisisActionJsonParser.Parse("""
        {
          "title": "Provider output",
          "summary": "Summary",
          "priority": "extreme",
          "important_points": null,
          "actions": null,
          "unclear_items": null,
          "warnings": null,
          "source_evidence": null
        }
        """);
    AssertEqual("medium", normalized.Priority, "Illegal priority should fall back to medium.");
    AssertEqual(0, normalized.Actions.Count, "Null actions should normalize to an empty list.");
    AssertEqual(0, normalized.SourceEvidence.Count, "Null evidence should normalize to an empty list.");

    var partialLists = CrisisActionJsonParser.Parse("""
        {
          "title": "Provider output",
          "summary": "Summary",
          "priority": "low",
          "actions": [
            { "task": "Submit worksheet", "deadline": null, "location": null, "required_documents": null },
            { "task": "   " }
          ],
          "source_evidence": [
            {},
            { "claim": "Supported", "source_text": "Submit worksheet" }
          ]
        }
        """);
    AssertEqual(1, partialLists.Actions.Count, "Blank actions should be removed.");
    AssertEqual(1, partialLists.SourceEvidence.Count, "Blank evidence entries should be removed.");

    return Task.CompletedTask;
}

static Task TestEvidenceSanitizerAsync()
{
    var result = new CrisisActionAnalysisResult
    {
        SourceEvidence =
        [
            new SourceEvidenceItem
            {
                Claim = "Exact evidence",
                SourceText = "Submit worksheet by Friday."
            },
            new SourceEvidenceItem
            {
                Claim = "Paraphrased evidence",
                SourceText = "The worksheet should be submitted before Friday."
            },
            new SourceEvidenceItem
            {
                Claim = "Empty evidence",
                SourceText = ""
            }
        ]
    };

    CrisisActionSourceEvidenceSanitizer.KeepOnlyExactSourceEvidence(
        result,
        "[1] Submit worksheet by Friday.");

    AssertEqual(2, result.SourceEvidence.Count, "Unsupported paraphrased source evidence should be removed.");
    Assert(result.SourceEvidence.Any(item => item.SourceText == "Submit worksheet by Friday."), "Exact evidence should remain.");
    Assert(result.SourceEvidence.Any(item => item.SourceText == ""), "Empty evidence should remain allowed for uncertain claims.");
    return Task.CompletedTask;
}

static async Task TestCancellationAsync()
{
    var provider = new MockCaptionCrisisActionAnalysisProvider();
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    await ExpectThrowsAsync<OperationCanceledException>(
        () => provider.AnalyzeAsync(MockCaptionCrisisActionAnalysisProvider.SampleTranscript, ClearBridgeOutputLanguages.English, cts.Token),
        "Cancelled caption analysis should surface OperationCanceledException.");
}

static List<CaptionAnalysisSentence> BuildDatasetA(int count)
{
    var captions = BuildGenericDataset(count);
    captions[0] = Caption(1, "Today we are reviewing the complete community service overview before the assignment details.");
    captions[19] = Caption(20, "Today we are reviewing the community service worksheet.");
    captions[20] = Caption(21, "Today we are reviewing the community service worksheet.");
    captions[44] = Caption(45, "Please submit the worksheet by Friday through Google Classroom.");
    captions[45] = Caption(46, "The reading example about last year's project is only an example, not a new assignment.");
    captions[59] = Caption(60, "I think the due date could change if the portal is updated later.");
    captions[69] = Caption(70, "Maybe ask someone later if the worksheet file is not visible.");
    captions[^1] = Caption(count, "This final caption is long enough to support a one-sentence range validation case.");
    return captions;
}

static List<CaptionAnalysisSentence> BuildGenericDataset(int count)
{
    return Enumerable.Range(1, count)
        .Select(number => Caption(number, $"Classroom discussion sentence {number} covers context for the lesson."))
        .ToList();
}

static List<CaptionAnalysisSentence> BuildNoActionDataset()
{
    return
    [
        Caption(1, "Today we discussed the water cycle and reviewed evaporation in class."),
        Caption(2, "The teacher gave an example about clouds forming after warm air rises."),
        Caption(3, "Students asked questions about condensation during the review."),
        Caption(4, "The class compared the diagram with yesterday's notes."),
        Caption(5, "No homework instruction was stated in this selected section."),
        Caption(6, "The lecture ended with a reminder to keep listening carefully.")
    ];
}

static List<CaptionAnalysisSentence> BuildAmbiguousDataset()
{
    return
    [
        Caption(1, "Maybe submit it next week after we know which file is final."),
        Caption(2, "I think the room could change before the meeting starts."),
        Caption(3, "Ask someone later because this instruction is unclear right now.")
    ];
}

static CaptionAnalysisSentence Caption(int number, string sourceText)
{
    return new CaptionAnalysisSentence
    {
        Number = number,
        SourceText = sourceText,
        Timestamp = $"2026-06-18T00:{number % 60:00}:00Z"
    };
}

static void AssertEvidenceWithinRequest(CrisisActionAnalysisResult result, string requestText)
{
    foreach (var evidence in result.SourceEvidence)
    {
        if (string.IsNullOrWhiteSpace(evidence.SourceText))
            continue;

        Assert(
            requestText.Contains(evidence.SourceText, StringComparison.Ordinal),
            $"Evidence should come from the selected request text: {evidence.SourceText}");
    }
}

static void ExpectClearBridgeError(string errorCode, Action action, string message)
{
    try
    {
        action();
    }
    catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == errorCode)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static async Task ExpectThrowsAsync<TException>(Func<Task> action, string message)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message} Expected: {expected}; actual: {actual}.");
}

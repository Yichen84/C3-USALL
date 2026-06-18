using System.Text.Json;

using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class RollingSummarySessionService
    {
        public const int MinBatchSentences = 5;
        public const int MinBatchCharacters = 200;
        public const int DefaultIntervalSeconds = 90;
        public const int MinIntervalSeconds = 60;
        public const int MaxIntervalSeconds = 120;

        private readonly MockRollingSummaryProvider mockProvider = new();
        private readonly OpenAiRollingSummaryProvider openAiProvider = new();
        private readonly SemaphoreSlim requestGate = new(1, 1);

        private RollingContextCache contextCache = new();
        private RollingSummaryStatus status = RollingSummaryStatus.Stopped;
        private DateTimeOffset sessionStart = DateTimeOffset.Now;

        public string SessionId { get; private set; } = Guid.NewGuid().ToString("N");

        public RollingSummaryStatus Status => status;

        public RollingContextCache ContextCache => contextCache.Clone();

        public RollingSummaryOutcome? LastOutcome { get; private set; }

        public bool IsRunning => status is RollingSummaryStatus.Listening or
            RollingSummaryStatus.WaitingForContent or
            RollingSummaryStatus.Processing or
            RollingSummaryStatus.Paused or
            RollingSummaryStatus.Error;

        public bool IsPaused => status == RollingSummaryStatus.Paused;

        public bool IsProcessing => status == RollingSummaryStatus.Processing;

        public DateTimeOffset SessionStart => sessionStart;

        public void Start()
        {
            if (IsRunning)
            {
                status = RollingSummaryStatus.Listening;
                return;
            }

            SessionId = Guid.NewGuid().ToString("N");
            sessionStart = DateTimeOffset.Now;
            contextCache = new RollingContextCache();
            LastOutcome = null;
            status = RollingSummaryStatus.Listening;
        }

        public void Pause()
        {
            if (status != RollingSummaryStatus.Processing && IsRunning)
                status = RollingSummaryStatus.Paused;
        }

        public void Resume()
        {
            if (status == RollingSummaryStatus.Paused)
                status = RollingSummaryStatus.Listening;
        }

        public void Stop()
        {
            if (status != RollingSummaryStatus.Processing)
                status = RollingSummaryStatus.Stopped;
        }

        public void ClearTemporaryContext()
        {
            contextCache = new RollingContextCache();
            LastOutcome = null;
            status = RollingSummaryStatus.Stopped;
            SessionId = Guid.NewGuid().ToString("N");
            sessionStart = DateTimeOffset.Now;
        }

        public int NormalizeIntervalSeconds(int seconds)
        {
            return seconds switch
            {
                <= MinIntervalSeconds => MinIntervalSeconds,
                >= MaxIntervalSeconds => MaxIntervalSeconds,
                _ => seconds <= 75 ? MinIntervalSeconds : seconds <= 105 ? DefaultIntervalSeconds : MaxIntervalSeconds
            };
        }

        public RollingSummaryRequest CreatePendingRequest(
            IReadOnlyList<CaptionAnalysisSentence> allSentences)
        {
            if (allSentences.Count == 0)
                throw new ClearBridgeAnalysisException("NoCaptionsAvailable", "No captions are available for rolling summary.");

            var pending = allSentences
                .Where(sentence => sentence.Number > contextCache.LastProcessedSentenceNumber)
                .OrderBy(sentence => sentence.Number)
                .Select(CloneSentence)
                .ToList();

            if (pending.Count == 0)
                throw new ClearBridgeAnalysisException("WaitingForContent", "Waiting for more captions.");

            var processed = CaptionAnalysisPreprocessor.RemoveConsecutiveDuplicateCaptions(pending);
            if (processed.Count == 0)
                throw new ClearBridgeAnalysisException("WaitingForContent", "Waiting for more captions.");

            var text = string.Join(
                Environment.NewLine,
                processed.Select(sentence => $"[{sentence.Number}] {sentence.SourceText.Trim()}"));

            if (processed.Count < MinBatchSentences && text.Length < MinBatchCharacters)
                throw new ClearBridgeAnalysisException("WaitingForContent", "Waiting for more captions.");

            return new RollingSummaryRequest
            {
                BatchNumber = contextCache.BatchCount + 1,
                Sentences = pending,
                ProcessedSentences = processed,
                PreviousContext = contextCache.Clone(),
                BatchTranscript = text,
                OriginalSentenceCount = pending.Count,
                ProcessedSentenceCount = processed.Count,
                CharacterCount = text.Length,
                RangeStart = pending.First().Number,
                RangeEnd = pending.Last().Number
            };
        }

        public async Task<RollingSummaryOutcome> ProcessPendingAsync(
            IReadOnlyList<CaptionAnalysisSentence> allSentences,
            string providerName,
            string outputLanguage,
            CancellationToken cancellationToken)
        {
            if (!await requestGate.WaitAsync(0, cancellationToken))
                throw new ClearBridgeAnalysisException("AlreadyProcessing", "A rolling summary request is already running.");

            var previousStatus = status;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                status = RollingSummaryStatus.Processing;
                var request = CreatePendingRequest(allSentences);
                var provider = GetProvider(providerName);
                RollingSummaryResult result;
                try
                {
                    result = await provider.AnalyzeBatchAsync(request, outputLanguage, cancellationToken);
                }
                catch (ClearBridgeAnalysisException ex)
                    when (provider is not MockRollingSummaryProvider &&
                          ex.ErrorCode is "ProviderTimeout" or "HttpError" or "NetworkError")
                {
                    result = await mockProvider.AnalyzeBatchAsync(request, outputLanguage, cancellationToken);
                    var fallback = CompleteRequest(request, result, mockProvider.Name, isMock: true);
                    status = RollingSummaryStatus.Listening;
                    var fallbackOutcome = new RollingSummaryOutcome
                    {
                        Request = fallback.Request,
                        Result = fallback.Result,
                        ProviderName = fallback.ProviderName,
                        IsMock = fallback.IsMock,
                        UsedFallback = true,
                        FallbackReason = ex.ErrorCode,
                        CompletedAt = fallback.CompletedAt
                    };
                    LastOutcome = fallbackOutcome;
                    return fallbackOutcome;
                }

                var outcome = CompleteRequest(request, result, provider.Name, provider is MockRollingSummaryProvider);
                status = RollingSummaryStatus.Listening;
                return outcome;
            }
            catch (OperationCanceledException)
            {
                status = previousStatus == RollingSummaryStatus.Paused
                    ? RollingSummaryStatus.Paused
                    : RollingSummaryStatus.Listening;
                throw;
            }
            catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "WaitingForContent")
            {
                status = RollingSummaryStatus.WaitingForContent;
                throw;
            }
            catch
            {
                status = RollingSummaryStatus.Error;
                throw;
            }
            finally
            {
                requestGate.Release();
            }
        }

        public string BuildConfirmedHistoryJson(RollingSummaryOutcome outcome)
        {
            return JsonSerializer.Serialize(new
            {
                session_id = SessionId,
                session_start = SessionStart,
                session_end = DateTimeOffset.Now,
                batch_count = contextCache.BatchCount,
                provider = outcome.ProviderName,
                is_mock = outcome.IsMock,
                result = outcome.Result,
                temporary_context_persisted = false,
                user_confirmed = true
            });
        }

        private RollingSummaryOutcome CompleteRequest(
            RollingSummaryRequest request,
            RollingSummaryResult result,
            string providerName,
            bool isMock)
        {
            result.ContextCache.LastProcessedSentenceNumber = request.RangeEnd;
            result.ContextCache.BatchCount = request.BatchNumber;
            RollingSummaryJsonParser.ClampContext(result.ContextCache);
            contextCache = result.ContextCache.Clone();

            var outcome = new RollingSummaryOutcome
            {
                Request = request,
                Result = result,
                ProviderName = providerName,
                IsMock = isMock,
                CompletedAt = DateTimeOffset.Now
            };
            LastOutcome = outcome;
            return outcome;
        }

        private IRollingSummaryProvider GetProvider(string providerName)
        {
            return providerName switch
            {
                "Mock" => mockProvider,
                "OpenAI-compatible" => openAiProvider,
                _ => throw new ClearBridgeAnalysisException("ProviderNotConfigured", "The selected provider is not available.")
            };
        }

        private static CaptionAnalysisSentence CloneSentence(CaptionAnalysisSentence sentence)
        {
            return new CaptionAnalysisSentence
            {
                Number = sentence.Number,
                SourceText = sentence.SourceText,
                TranslatedText = sentence.TranslatedText,
                Timestamp = sentence.Timestamp
            };
        }
    }
}

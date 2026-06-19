namespace LiveCaptionsTranslator.models.ClearBridge
{
    public sealed class RollingSummaryOutcome
    {
        public RollingSummaryResult Result { get; init; } = new();

        public RollingSummaryRequest Request { get; init; } = new();

        public string ProviderName { get; init; } = string.Empty;

        public bool IsMock { get; init; }

        public bool UsedFallback { get; init; }

        public string FallbackReason { get; init; } = string.Empty;

        public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.Now;
    }
}

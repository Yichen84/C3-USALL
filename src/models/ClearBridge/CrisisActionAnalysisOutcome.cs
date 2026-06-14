namespace LiveCaptionsTranslator.models.ClearBridge
{
    public sealed class CrisisActionAnalysisOutcome
    {
        public CrisisActionAnalysisResult Result { get; init; } = new();

        public string ProviderName { get; init; } = string.Empty;

        public bool IsMock { get; init; }

        public bool UsedFallback { get; init; }

        public string FallbackReason { get; init; } = string.Empty;
    }
}

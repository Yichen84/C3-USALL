namespace LiveCaptionsTranslator.models.ClearBridge
{
    public sealed class RollingSummaryDisplayState
    {
        public RollingSummaryStatus Status { get; init; }

        public string Detail { get; init; } = string.Empty;

        public bool IsProcessing { get; init; }

        public bool IsRunning { get; init; }

        public bool IsPaused { get; init; }

        public bool CanSave { get; init; }

        public bool IsSaved { get; init; }

        public IReadOnlyList<RollingSummaryOutcome> Outcomes { get; init; } = [];
    }
}

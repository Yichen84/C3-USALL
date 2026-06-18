namespace LiveCaptionsTranslator.models.ClearBridge
{
    public sealed class RollingSummaryResult
    {
        public string CurrentTopic { get; set; } = string.Empty;

        public string BatchSummary { get; set; } = string.Empty;

        public List<string> KeyPoints { get; set; } = [];

        public List<ActionItem> NewActions { get; set; } = [];

        public List<string> DatesAndDeadlines { get; set; } = [];

        public List<string> Locations { get; set; } = [];

        public List<string> Warnings { get; set; } = [];

        public List<string> UnresolvedQuestions { get; set; } = [];

        public List<SourceEvidenceItem> SourceEvidence { get; set; } = [];

        public RollingContextCache ContextCache { get; set; } = new();
    }
}

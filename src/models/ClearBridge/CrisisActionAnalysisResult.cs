namespace LiveCaptionsTranslator.models.ClearBridge
{
    public sealed class CrisisActionAnalysisResult
    {
        public string Title { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public string Priority { get; set; } = "medium";

        public List<string> ImportantPoints { get; set; } = new();

        public List<ActionItem> Actions { get; set; } = new();

        public List<string> UnclearItems { get; set; } = new();

        public List<string> Warnings { get; set; } = new();

        public List<SourceEvidenceItem> SourceEvidence { get; set; } = new();
    }
}

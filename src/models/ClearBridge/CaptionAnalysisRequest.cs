namespace LiveCaptionsTranslator.models.ClearBridge
{
    public sealed class CaptionAnalysisRequest
    {
        public string AnalysisScope { get; init; } = "All";

        public int RangeStart { get; init; }

        public int RangeEnd { get; init; }

        public int OriginalSentenceCount { get; init; }

        public int ProcessedSentenceCount { get; init; }

        public int CharacterCount { get; init; }

        public string Text { get; init; } = string.Empty;

        public IReadOnlyList<CaptionAnalysisSentence> SelectedSentences { get; init; } = [];

        public IReadOnlyList<CaptionAnalysisSentence> ProcessedSentences { get; init; } = [];
    }
}

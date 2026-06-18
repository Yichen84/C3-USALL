namespace LiveCaptionsTranslator.models.ClearBridge
{
    public sealed class RollingSummaryRequest
    {
        public int BatchNumber { get; set; }

        public IReadOnlyList<CaptionAnalysisSentence> Sentences { get; set; } = [];

        public IReadOnlyList<CaptionAnalysisSentence> ProcessedSentences { get; set; } = [];

        public RollingContextCache PreviousContext { get; set; } = new();

        public string BatchTranscript { get; set; } = string.Empty;

        public int OriginalSentenceCount { get; set; }

        public int ProcessedSentenceCount { get; set; }

        public int CharacterCount { get; set; }

        public int RangeStart { get; set; }

        public int RangeEnd { get; set; }
    }
}

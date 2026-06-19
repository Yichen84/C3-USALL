namespace LiveCaptionsTranslator.models.ClearBridge
{
    public sealed class RollingContextCache
    {
        public string CurrentTopic { get; set; } = string.Empty;

        public List<string> EstablishedFacts { get; set; } = [];

        public List<string> ConfirmedActions { get; set; } = [];

        public List<string> DatesAndDeadlines { get; set; } = [];

        public List<string> Locations { get; set; } = [];

        public List<string> Warnings { get; set; } = [];

        public List<string> UnresolvedQuestions { get; set; } = [];

        public string CompressedNarrative { get; set; } = string.Empty;

        public int LastProcessedSentenceNumber { get; set; }

        public int BatchCount { get; set; }

        public RollingContextCache Clone()
        {
            return new RollingContextCache
            {
                CurrentTopic = CurrentTopic,
                EstablishedFacts = [.. EstablishedFacts],
                ConfirmedActions = [.. ConfirmedActions],
                DatesAndDeadlines = [.. DatesAndDeadlines],
                Locations = [.. Locations],
                Warnings = [.. Warnings],
                UnresolvedQuestions = [.. UnresolvedQuestions],
                CompressedNarrative = CompressedNarrative,
                LastProcessedSentenceNumber = LastProcessedSentenceNumber,
                BatchCount = BatchCount
            };
        }
    }
}

namespace LiveCaptionsTranslator.models.ClearBridge
{
    public sealed class CaptionAnalysisSentence
    {
        public int Number { get; init; }

        public string SourceText { get; init; } = string.Empty;

        public string TranslatedText { get; init; } = string.Empty;

        public string Timestamp { get; init; } = string.Empty;
    }
}

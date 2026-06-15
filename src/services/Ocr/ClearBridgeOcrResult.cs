namespace LiveCaptionsTranslator.services.Ocr
{
    public sealed class ClearBridgeOcrResult
    {
        public string Text { get; set; } = string.Empty;

        public string EngineId { get; set; } = string.Empty;

        public string EngineName { get; set; } = string.Empty;

        public bool IsCloudBased { get; set; }

        public double? Confidence { get; set; }

        public TimeSpan Duration { get; set; }

        public int ImageWidth { get; set; }

        public int ImageHeight { get; set; }

        public long ImageBytes { get; set; }

        public bool WasEditedByUser { get; set; }
    }
}

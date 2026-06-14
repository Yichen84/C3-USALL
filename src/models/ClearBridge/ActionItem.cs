namespace LiveCaptionsTranslator.models.ClearBridge
{
    public sealed class ActionItem
    {
        public string Task { get; set; } = string.Empty;

        public string Deadline { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public List<string> RequiredDocuments { get; set; } = new();

        public bool Completed { get; set; }
    }
}

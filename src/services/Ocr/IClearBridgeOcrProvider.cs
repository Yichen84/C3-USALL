namespace LiveCaptionsTranslator.services.Ocr
{
    public interface IClearBridgeOcrProvider
    {
        string Id { get; }

        string DisplayName { get; }

        bool IsCloudBased { get; }

        Task<ClearBridgeOcrResult> ExtractTextAsync(
            ClearBridgeImageInput input,
            CancellationToken cancellationToken);
    }
}

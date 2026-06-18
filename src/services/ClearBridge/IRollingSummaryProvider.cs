using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public interface IRollingSummaryProvider
    {
        string Name { get; }

        Task<RollingSummaryResult> AnalyzeBatchAsync(
            RollingSummaryRequest request,
            string outputLanguage,
            CancellationToken cancellationToken);
    }
}

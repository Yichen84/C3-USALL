using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public interface ICrisisActionAnalysisProvider
    {
        string Name { get; }

        Task<CrisisActionAnalysisResult> AnalyzeAsync(
            string sourceText,
            string outputLanguage,
            CancellationToken cancellationToken);
    }
}

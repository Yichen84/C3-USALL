using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public static class CrisisActionSourceEvidenceSanitizer
    {
        public static void KeepOnlyExactSourceEvidence(
            CrisisActionAnalysisResult result,
            string sourceText)
        {
            result.SourceEvidence = result.SourceEvidence
                .Where(item =>
                    string.IsNullOrWhiteSpace(item.SourceText) ||
                    sourceText.Contains(item.SourceText.Trim(), StringComparison.Ordinal))
                .Select(item => new SourceEvidenceItem
                {
                    Claim = item.Claim,
                    SourceText = item.SourceText.Trim()
                })
                .ToList();
        }
    }
}

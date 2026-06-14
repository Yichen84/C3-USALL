using System.Diagnostics;

using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class CrisisActionAnalysisService
    {
        public const int MinInputLength = 30;
        public const int MaxInputLength = 12000;

        private readonly MockCrisisActionAnalysisProvider mockProvider = new();
        private readonly OpenAiCrisisActionAnalysisProvider openAiProvider = new();

        public async Task<CrisisActionAnalysisOutcome> AnalyzeAsync(
            string providerName,
            string sourceText,
            string outputLanguage,
            CancellationToken cancellationToken)
        {
            ValidateInput(sourceText, outputLanguage);

            var provider = GetProvider(providerName);
            try
            {
                var result = await provider.AnalyzeAsync(sourceText, outputLanguage, cancellationToken);
                return new CrisisActionAnalysisOutcome
                {
                    Result = result,
                    ProviderName = provider.Name,
                    IsMock = provider is MockCrisisActionAnalysisProvider
                };
            }
            catch (ClearBridgeAnalysisException ex)
                when (provider is not MockCrisisActionAnalysisProvider &&
                      ex.ErrorCode is "ProviderTimeout" or "HttpError" or "NetworkError")
            {
                Debug.WriteLine(
                    $"ClearBridge Provider={provider.Name} Status=FallbackToMock " +
                    $"InputLength={sourceText.Length} ErrorType={ex.ErrorCode}");

                var result = await mockProvider.AnalyzeAsync(
                    MockCrisisActionAnalysisProvider.SampleNotice,
                    outputLanguage,
                    cancellationToken);

                return new CrisisActionAnalysisOutcome
                {
                    Result = result,
                    ProviderName = mockProvider.Name,
                    IsMock = true,
                    UsedFallback = true,
                    FallbackReason = ex.ErrorCode
                };
            }
        }

        private ICrisisActionAnalysisProvider GetProvider(string providerName)
        {
            return providerName switch
            {
                "Mock" => mockProvider,
                "OpenAI-compatible" => openAiProvider,
                _ => throw new ClearBridgeAnalysisException("ProviderNotConfigured", "The selected provider is not available.")
            };
        }

        private static void ValidateInput(string sourceText, string outputLanguage)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                throw new ClearBridgeAnalysisException("InputEmpty", "Paste a notice or message before analyzing.");

            if (sourceText.Trim().Length < MinInputLength)
                throw new ClearBridgeAnalysisException("InputTooShort", $"Enter at least {MinInputLength} characters.");

            if (sourceText.Length > MaxInputLength)
                throw new ClearBridgeAnalysisException("InputTooLong", $"The text is too long. Keep it under {MaxInputLength} characters.");

            if (string.IsNullOrWhiteSpace(outputLanguage) ||
                !ClearBridgeOutputLanguages.Supported.Contains(outputLanguage))
            {
                throw new ClearBridgeAnalysisException("OutputLanguageMissing", "Choose an output language.");
            }
        }
    }
}

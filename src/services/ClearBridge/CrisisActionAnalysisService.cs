using System.Diagnostics;

using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class CrisisActionAnalysisService
    {
        public const int MinInputLength = ClearBridgeInputLimits.MinInputLength;
        public const int MaxInputLength = ClearBridgeInputLimits.MaxInputLength;
        public const int MaxCaptionInputLength = ClearBridgeInputLimits.MaxCaptionInputLength;

        private readonly MockCrisisActionAnalysisProvider mockProvider = new();
        private readonly MockCaptionCrisisActionAnalysisProvider mockCaptionProvider = new();
        private readonly OpenAiCrisisActionAnalysisProvider openAiProvider = new();
        private readonly OpenAiCrisisActionAnalysisProvider openAiCaptionProvider =
            new(CrisisActionPromptMode.CaptionTranscript);

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

        public async Task<CrisisActionAnalysisOutcome> AnalyzeCaptionAsync(
            string providerName,
            string sourceText,
            string outputLanguage,
            CancellationToken cancellationToken)
        {
            ValidateCaptionInput(sourceText, outputLanguage);

            var provider = GetCaptionProvider(providerName);
            try
            {
                var result = await provider.AnalyzeAsync(sourceText, outputLanguage, cancellationToken);
                return new CrisisActionAnalysisOutcome
                {
                    Result = result,
                    ProviderName = provider.Name,
                    IsMock = provider is MockCaptionCrisisActionAnalysisProvider
                };
            }
            catch (ClearBridgeAnalysisException ex)
                when (provider is not MockCaptionCrisisActionAnalysisProvider &&
                      ex.ErrorCode is "ProviderTimeout" or "HttpError" or "NetworkError")
            {
                Debug.WriteLine(
                    $"ClearBridge Caption Provider={provider.Name} Status=FallbackToMock " +
                    $"InputLength={sourceText.Length} ErrorType={ex.ErrorCode}");

                var result = await mockCaptionProvider.AnalyzeAsync(
                    sourceText,
                    outputLanguage,
                    cancellationToken);

                return new CrisisActionAnalysisOutcome
                {
                    Result = result,
                    ProviderName = mockCaptionProvider.Name,
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

        private ICrisisActionAnalysisProvider GetCaptionProvider(string providerName)
        {
            return providerName switch
            {
                "Mock" => mockCaptionProvider,
                "OpenAI-compatible" => openAiCaptionProvider,
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

        private static void ValidateCaptionInput(string sourceText, string outputLanguage)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                throw new ClearBridgeAnalysisException("InputEmpty", "Choose captions before analyzing.");

            if (sourceText.Trim().Length < MinInputLength)
                throw new ClearBridgeAnalysisException("InputTooShort", $"Select at least {MinInputLength} characters.");

            if (sourceText.Length > MaxCaptionInputLength)
                throw new ClearBridgeAnalysisException(
                    "InputTooLong",
                    $"The selected captions are too long. Keep them under {MaxCaptionInputLength} characters.");

            if (string.IsNullOrWhiteSpace(outputLanguage) ||
                !ClearBridgeOutputLanguages.Supported.Contains(outputLanguage))
            {
                throw new ClearBridgeAnalysisException("OutputLanguageMissing", "Choose an output language.");
            }
        }
    }
}

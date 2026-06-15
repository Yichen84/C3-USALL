using LiveCaptionsTranslator.services.Localization;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class ClearBridgeLocalizationService
    {
        public static IReadOnlyList<UiLanguageOption> SupportedUiLanguages =>
            AppLocalizationService.SupportedLanguages;

        public string Language => AppLocalizationService.CurrentLanguage;

        public void SetLanguage(string? language)
        {
            AppLocalizationService.SetLanguage(language);
        }

        public string T(string key)
        {
            return AppLocalizationService.T(key);
        }

        public string Format(string key, params object[] args)
        {
            return AppLocalizationService.Format(key, args);
        }
    }
}

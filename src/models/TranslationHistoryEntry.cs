using CsvHelper.Configuration.Attributes;

using LiveCaptionsTranslator.services.Localization;

namespace LiveCaptionsTranslator.models
{
    public class TranslationHistoryEntry
    {
        public required string Timestamp { get; set; }

        [Ignore]
        public required string TimestampFull { get; set; }

        public required string SourceText { get; set; }

        public required string TranslatedText { get; set; }

        public required string TargetLanguage { get; set; }

        public required string FeatureType { get; set; }

        [Ignore]
        public string FeatureTypeDisplay => AppLocalizationService.FeatureTypeLabel(FeatureType);

        public required string ApiUsed { get; set; }
    }
}

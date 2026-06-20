namespace LiveCaptionsTranslator.services.ClearBridge
{
    public static class ClearBridgeOutputLanguages
    {
        public const string English = "English";
        public const string SimplifiedChinese = "Simplified Chinese";
        public const string Arabic = "Arabic";
        public const string Spanish = "Spanish";
        public const string French = "French";

        public static readonly string[] Supported =
        [
            English,
            SimplifiedChinese,
            Arabic
        ];

        public static string Normalize(string outputLanguage)
        {
            var normalized = outputLanguage?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            return normalized.ToLowerInvariant() switch
            {
                "en" or "en-us" or "en-gb" or "english" => English,
                "zh" or "zh-cn" or "zh-hans" or "zh-hans-cn" or "chinese" or
                    "simplified chinese" or "simplified-chinese" => SimplifiedChinese,
                "ar" or "ar-sa" or "arabic" => Arabic,
                _ => normalized
            };
        }
    }
}

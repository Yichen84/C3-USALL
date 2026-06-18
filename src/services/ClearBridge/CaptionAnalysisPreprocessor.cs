using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public static class CaptionAnalysisPreprocessor
    {
        public const int MaxSentences = ClearBridgeInputLimits.MaxCaptionSentences;

        public static CaptionAnalysisRequest Prepare(
            IReadOnlyList<CaptionAnalysisSentence> sentences,
            bool analyzeAll,
            int fromSentence,
            int toSentence)
        {
            if (sentences.Count == 0)
                throw new ClearBridgeAnalysisException("NoCaptionsAvailable", "No captions are available for analysis.");

            var total = sentences.Count;
            var rangeStart = analyzeAll ? 1 : fromSentence;
            var rangeEnd = analyzeAll ? total : toSentence;

            if (rangeStart < 1 || rangeEnd > total || rangeStart > rangeEnd)
                throw new ClearBridgeAnalysisException("InvalidRange", "The selected caption range is invalid.");

            var selected = sentences
                .Where(sentence => sentence.Number >= rangeStart && sentence.Number <= rangeEnd)
                .ToList();

            if (selected.Count > MaxSentences)
                throw new ClearBridgeAnalysisException("RangeTooLarge", "Caption analysis supports up to 400 sentences.");

            var processed = RemoveConsecutiveDuplicateCaptions(selected);
            if (processed.Count == 0)
                throw new ClearBridgeAnalysisException("InputEmpty", "The selected captions are empty.");

            var text = string.Join(
                Environment.NewLine,
                processed.Select(sentence => $"[{sentence.Number}] {sentence.SourceText.Trim()}"));

            if (text.Trim().Length < ClearBridgeInputLimits.MinInputLength)
                throw new ClearBridgeAnalysisException("InputTooShort", "The selected captions are too short.");

            return new CaptionAnalysisRequest
            {
                AnalysisScope = analyzeAll ? "All" : "Range",
                RangeStart = rangeStart,
                RangeEnd = rangeEnd,
                OriginalSentenceCount = selected.Count,
                ProcessedSentenceCount = processed.Count,
                CharacterCount = text.Length,
                Text = text,
                SelectedSentences = selected,
                ProcessedSentences = processed
            };
        }

        public static IReadOnlyList<CaptionAnalysisSentence> RemoveConsecutiveDuplicateCaptions(
            IReadOnlyList<CaptionAnalysisSentence> selected)
        {
            var processed = new List<CaptionAnalysisSentence>();
            string? previousNormalized = null;
            string? previousIncrementalNormalized = null;

            foreach (var sentence in selected)
            {
                var text = sentence.SourceText?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var normalized = NormalizeForExactDuplicateCheck(text);
                if (string.Equals(normalized, previousNormalized, StringComparison.Ordinal))
                    continue;

                var incrementalNormalized = NormalizeForIncrementalCheck(text);
                if (processed.Count > 0 &&
                    previousIncrementalNormalized != null &&
                    IsConservativeIncrementalReplacement(previousIncrementalNormalized, incrementalNormalized))
                {
                    processed[^1] = new CaptionAnalysisSentence
                    {
                        Number = sentence.Number,
                        SourceText = text,
                        TranslatedText = sentence.TranslatedText,
                        Timestamp = sentence.Timestamp
                    };
                    previousNormalized = normalized;
                    previousIncrementalNormalized = incrementalNormalized;
                    continue;
                }

                processed.Add(new CaptionAnalysisSentence
                {
                    Number = sentence.Number,
                    SourceText = text,
                    TranslatedText = sentence.TranslatedText,
                    Timestamp = sentence.Timestamp
                });
                previousNormalized = normalized;
                previousIncrementalNormalized = incrementalNormalized;
            }

            return processed;
        }

        private static string NormalizeForExactDuplicateCheck(string text)
        {
            return string.Join(
                " ",
                text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                .Trim()
                .ToLowerInvariant();
        }

        private static string NormalizeForIncrementalCheck(string text)
        {
            var normalized = NormalizeForExactDuplicateCheck(text)
                .TrimEnd('.', ',', ';', ':', '!', '?', '-', '…');

            while (normalized.EndsWith("...", StringComparison.Ordinal))
                normalized = normalized[..^3].TrimEnd();

            return normalized.Trim();
        }

        private static bool IsConservativeIncrementalReplacement(string previous, string current)
        {
            if (previous.Length < 8 || current.Length <= previous.Length + 2)
                return false;

            return current.StartsWith(previous + " ", StringComparison.Ordinal) ||
                   current.StartsWith(previous, StringComparison.Ordinal);
        }
    }
}

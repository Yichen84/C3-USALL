using System.Text;

using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public static class ClearBridgeOutputLanguageValidator
    {
        public static void EnsureCrisisResultMatches(
            CrisisActionAnalysisResult result,
            string outputLanguage)
        {
            if (!HasRecognizedCrisisContent(result) ||
                !MatchesTargetLanguage(CollectCrisisVisibleText(result), outputLanguage))
            {
                throw new ClearBridgeAnalysisException(
                    "OutputLanguageMismatch",
                    "The provider response did not follow the selected output language.");
            }
        }

        public static void EnsureRollingResultMatches(
            RollingSummaryResult result,
            string outputLanguage)
        {
            if (!HasRecognizedRollingContent(result) ||
                !MatchesTargetLanguage(CollectRollingVisibleText(result), outputLanguage))
            {
                throw new ClearBridgeAnalysisException(
                    "OutputLanguageMismatch",
                    "The provider response did not follow the selected output language.");
            }
        }

        public static bool CrisisResultMatches(
            CrisisActionAnalysisResult result,
            string outputLanguage)
        {
            return HasRecognizedCrisisContent(result) &&
                MatchesTargetLanguage(CollectCrisisVisibleText(result), outputLanguage);
        }

        public static bool RollingResultMatches(
            RollingSummaryResult result,
            string outputLanguage)
        {
            return HasRecognizedRollingContent(result) &&
                MatchesTargetLanguage(CollectRollingVisibleText(result), outputLanguage);
        }

        private static bool HasRecognizedCrisisContent(CrisisActionAnalysisResult result)
        {
            var hasNonDefaultTitle = !string.IsNullOrWhiteSpace(result.Title) &&
                !string.Equals(result.Title, "ClearBridge Analysis", StringComparison.Ordinal);
            var hasNonDefaultSummary = !string.IsNullOrWhiteSpace(result.Summary) &&
                !string.Equals(result.Summary, "No summary was returned.", StringComparison.Ordinal);

            return hasNonDefaultTitle ||
                hasNonDefaultSummary ||
                result.ImportantPoints.Count > 0 ||
                result.Actions.Count > 0 ||
                result.UnclearItems.Count > 0 ||
                result.Warnings.Count > 0 ||
                result.SourceEvidence.Any(item => !string.IsNullOrWhiteSpace(item.Claim));
        }

        private static bool HasRecognizedRollingContent(RollingSummaryResult result)
        {
            var hasNonDefaultTopic = !string.IsNullOrWhiteSpace(result.CurrentTopic) &&
                !string.Equals(result.CurrentTopic, "Rolling Summary", StringComparison.Ordinal);
            var hasNonDefaultSummary = !string.IsNullOrWhiteSpace(result.BatchSummary) &&
                !string.Equals(result.BatchSummary, "No new summary was returned.", StringComparison.Ordinal);

            return hasNonDefaultTopic ||
                hasNonDefaultSummary ||
                result.KeyPoints.Count > 0 ||
                result.NewActions.Count > 0 ||
                result.DatesAndDeadlines.Count > 0 ||
                result.Locations.Count > 0 ||
                result.Warnings.Count > 0 ||
                result.UnresolvedQuestions.Count > 0 ||
                result.SourceEvidence.Any(item => !string.IsNullOrWhiteSpace(item.Claim)) ||
                result.ContextCache.EstablishedFacts.Count > 0 ||
                result.ContextCache.ConfirmedActions.Count > 0 ||
                result.ContextCache.DatesAndDeadlines.Count > 0 ||
                result.ContextCache.Locations.Count > 0 ||
                result.ContextCache.Warnings.Count > 0 ||
                result.ContextCache.UnresolvedQuestions.Count > 0 ||
                !string.IsNullOrWhiteSpace(result.ContextCache.CompressedNarrative);
        }

        private static string CollectCrisisVisibleText(CrisisActionAnalysisResult result)
        {
            var builder = new StringBuilder();
            Append(builder, result.Title);
            Append(builder, result.Summary);
            Append(builder, result.ImportantPoints);
            foreach (var action in result.Actions)
            {
                Append(builder, action.Task);
                Append(builder, action.Deadline);
                Append(builder, action.Location);
                Append(builder, action.RequiredDocuments);
            }

            Append(builder, result.UnclearItems);
            Append(builder, result.Warnings);
            foreach (var item in result.SourceEvidence)
                Append(builder, item.Claim);

            return builder.ToString();
        }

        private static string CollectRollingVisibleText(RollingSummaryResult result)
        {
            var builder = new StringBuilder();
            Append(builder, result.CurrentTopic);
            Append(builder, result.BatchSummary);
            Append(builder, result.KeyPoints);
            foreach (var action in result.NewActions)
            {
                Append(builder, action.Task);
                Append(builder, action.Deadline);
                Append(builder, action.Location);
                Append(builder, action.RequiredDocuments);
            }

            Append(builder, result.DatesAndDeadlines);
            Append(builder, result.Locations);
            Append(builder, result.Warnings);
            Append(builder, result.UnresolvedQuestions);
            foreach (var item in result.SourceEvidence)
                Append(builder, item.Claim);

            Append(builder, result.ContextCache.CurrentTopic);
            Append(builder, result.ContextCache.EstablishedFacts);
            Append(builder, result.ContextCache.ConfirmedActions);
            Append(builder, result.ContextCache.DatesAndDeadlines);
            Append(builder, result.ContextCache.Locations);
            Append(builder, result.ContextCache.Warnings);
            Append(builder, result.ContextCache.UnresolvedQuestions);
            Append(builder, result.ContextCache.CompressedNarrative);
            return builder.ToString();
        }

        private static bool MatchesTargetLanguage(string text, string outputLanguage)
        {
            var language = ClearBridgeOutputLanguages.Normalize(outputLanguage);
            var counts = CountScripts(text);
            var meaningfulLetters = counts.Cjk + counts.Arabic + counts.Latin;
            if (meaningfulLetters < 12)
                return true;

            return language switch
            {
                ClearBridgeOutputLanguages.SimplifiedChinese =>
                    counts.Cjk >= 8 &&
                    counts.Cjk >= counts.Arabic &&
                    counts.Cjk * 2 >= counts.Latin,
                ClearBridgeOutputLanguages.Arabic =>
                    counts.Arabic >= 8 &&
                    counts.Arabic >= counts.Cjk &&
                    counts.Arabic * 2 >= counts.Latin,
                ClearBridgeOutputLanguages.English =>
                    counts.Latin >= 12 &&
                    counts.Cjk <= Math.Max(2, counts.Latin / 4) &&
                    counts.Arabic <= Math.Max(2, counts.Latin / 4),
                _ => true
            };
        }

        private static ScriptCounts CountScripts(string text)
        {
            var counts = new ScriptCounts();
            foreach (var c in text)
            {
                if (IsCjk(c))
                    counts.Cjk++;
                else if (IsArabic(c))
                    counts.Arabic++;
                else if (IsLatin(c))
                    counts.Latin++;
            }

            return counts;
        }

        private static bool IsCjk(char c)
        {
            return c is >= '\u4E00' and <= '\u9FFF' or
                >= '\u3400' and <= '\u4DBF' or
                >= '\uF900' and <= '\uFAFF';
        }

        private static bool IsArabic(char c)
        {
            return c is >= '\u0600' and <= '\u06FF' or
                >= '\u0750' and <= '\u077F' or
                >= '\u08A0' and <= '\u08FF' or
                >= '\uFB50' and <= '\uFDFF' or
                >= '\uFE70' and <= '\uFEFF';
        }

        private static bool IsLatin(char c)
        {
            return c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
        }

        private static void Append(StringBuilder builder, IEnumerable<string> values)
        {
            foreach (var value in values)
                Append(builder, value);
        }

        private static void Append(StringBuilder builder, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append(value);
        }

        private sealed class ScriptCounts
        {
            public int Cjk { get; set; }

            public int Arabic { get; set; }

            public int Latin { get; set; }
        }
    }
}

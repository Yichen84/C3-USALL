using System.Text.Json;
using System.Text.Json.Serialization;

using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public static class RollingSummaryJsonParser
    {
        private const int MaxNarrativeLength = 2500;
        private const int MaxListItems = 20;

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static RollingSummaryResult Parse(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
                throw new ClearBridgeAnalysisException("EmptyResponse", "The provider returned an empty response.");

            var json = TrimMarkdownFence(rawJson.Trim());
            RollingSummaryDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<RollingSummaryDto>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new ClearBridgeAnalysisException("InvalidJson", "The provider did not return valid JSON.", ex);
            }

            if (dto == null)
                throw new ClearBridgeAnalysisException("InvalidJson", "The provider returned JSON that could not be read.");

            return Normalize(dto);
        }

        public static void ClampContext(RollingContextCache cache)
        {
            cache.CurrentTopic = Clean(cache.CurrentTopic);
            cache.EstablishedFacts = CleanList(cache.EstablishedFacts);
            cache.ConfirmedActions = CleanList(cache.ConfirmedActions);
            cache.DatesAndDeadlines = CleanList(cache.DatesAndDeadlines);
            cache.Locations = CleanList(cache.Locations);
            cache.Warnings = CleanList(cache.Warnings);
            cache.UnresolvedQuestions = CleanList(cache.UnresolvedQuestions);
            cache.CompressedNarrative = Clean(cache.CompressedNarrative);
            if (cache.CompressedNarrative.Length > MaxNarrativeLength)
                cache.CompressedNarrative = cache.CompressedNarrative[^MaxNarrativeLength..];
        }

        private static RollingSummaryResult Normalize(RollingSummaryDto dto)
        {
            var result = new RollingSummaryResult
            {
                CurrentTopic = Clean(dto.CurrentTopic),
                BatchSummary = Clean(dto.BatchSummary),
                KeyPoints = CleanList(dto.KeyPoints),
                NewActions = dto.NewActions?
                    .Where(action => action != null)
                    .Select(action => new ActionItem
                    {
                        Task = Clean(action.Task),
                        Deadline = Clean(action.Deadline),
                        Location = Clean(action.Location),
                        RequiredDocuments = CleanList(action.RequiredDocuments)
                    })
                    .Where(action => !string.IsNullOrWhiteSpace(action.Task))
                    .Take(MaxListItems)
                    .ToList() ?? [],
                DatesAndDeadlines = CleanList(dto.DatesAndDeadlines),
                Locations = CleanList(dto.Locations),
                Warnings = CleanList(dto.Warnings),
                UnresolvedQuestions = CleanList(dto.UnresolvedQuestions),
                SourceEvidence = dto.SourceEvidence?
                    .Where(item => item != null)
                    .Select(item => new SourceEvidenceItem
                    {
                        Claim = Clean(item.Claim),
                        SourceText = Clean(item.SourceText)
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.Claim) ||
                                   !string.IsNullOrWhiteSpace(item.SourceText))
                    .Take(MaxListItems)
                    .ToList() ?? [],
                ContextCache = NormalizeCache(dto.ContextCache)
            };

            if (string.IsNullOrWhiteSpace(result.CurrentTopic))
                result.CurrentTopic = "Rolling Summary";
            if (string.IsNullOrWhiteSpace(result.BatchSummary))
                result.BatchSummary = "No new summary was returned.";
            if (string.IsNullOrWhiteSpace(result.ContextCache.CurrentTopic))
                result.ContextCache.CurrentTopic = result.CurrentTopic;
            if (string.IsNullOrWhiteSpace(result.ContextCache.CompressedNarrative))
                result.ContextCache.CompressedNarrative = result.BatchSummary;

            return result;
        }

        private static RollingContextCache NormalizeCache(RollingContextCacheDto? dto)
        {
            var cache = new RollingContextCache
            {
                CurrentTopic = Clean(dto?.CurrentTopic),
                EstablishedFacts = CleanList(dto?.EstablishedFacts),
                ConfirmedActions = CleanList(dto?.ConfirmedActions),
                DatesAndDeadlines = CleanList(dto?.DatesAndDeadlines),
                Locations = CleanList(dto?.Locations),
                Warnings = CleanList(dto?.Warnings),
                UnresolvedQuestions = CleanList(dto?.UnresolvedQuestions),
                CompressedNarrative = Clean(dto?.CompressedNarrative)
            };
            ClampContext(cache);
            return cache;
        }

        private static List<string> CleanList(List<string>? values)
        {
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(Clean)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxListItems)
                .ToList() ?? [];
        }

        private static string Clean(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string TrimMarkdownFence(string text)
        {
            if (!text.StartsWith("```", StringComparison.Ordinal))
                return text;

            var firstNewLine = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
                return text[(firstNewLine + 1)..lastFence].Trim();

            return text;
        }

        private sealed class RollingSummaryDto
        {
            [JsonPropertyName("current_topic")]
            public string? CurrentTopic { get; set; }

            [JsonPropertyName("batch_summary")]
            public string? BatchSummary { get; set; }

            [JsonPropertyName("key_points")]
            public List<string>? KeyPoints { get; set; }

            [JsonPropertyName("new_actions")]
            public List<ActionItemDto>? NewActions { get; set; }

            [JsonPropertyName("dates_and_deadlines")]
            public List<string>? DatesAndDeadlines { get; set; }

            [JsonPropertyName("locations")]
            public List<string>? Locations { get; set; }

            [JsonPropertyName("warnings")]
            public List<string>? Warnings { get; set; }

            [JsonPropertyName("unresolved_questions")]
            public List<string>? UnresolvedQuestions { get; set; }

            [JsonPropertyName("source_evidence")]
            public List<SourceEvidenceItemDto>? SourceEvidence { get; set; }

            [JsonPropertyName("context_cache")]
            public RollingContextCacheDto? ContextCache { get; set; }
        }

        private sealed class ActionItemDto
        {
            [JsonPropertyName("task")]
            public string? Task { get; set; }

            [JsonPropertyName("deadline")]
            public string? Deadline { get; set; }

            [JsonPropertyName("location")]
            public string? Location { get; set; }

            [JsonPropertyName("required_documents")]
            public List<string>? RequiredDocuments { get; set; }
        }

        private sealed class SourceEvidenceItemDto
        {
            [JsonPropertyName("claim")]
            public string? Claim { get; set; }

            [JsonPropertyName("source_text")]
            public string? SourceText { get; set; }
        }

        private sealed class RollingContextCacheDto
        {
            [JsonPropertyName("current_topic")]
            public string? CurrentTopic { get; set; }

            [JsonPropertyName("established_facts")]
            public List<string>? EstablishedFacts { get; set; }

            [JsonPropertyName("confirmed_actions")]
            public List<string>? ConfirmedActions { get; set; }

            [JsonPropertyName("dates_and_deadlines")]
            public List<string>? DatesAndDeadlines { get; set; }

            [JsonPropertyName("locations")]
            public List<string>? Locations { get; set; }

            [JsonPropertyName("warnings")]
            public List<string>? Warnings { get; set; }

            [JsonPropertyName("unresolved_questions")]
            public List<string>? UnresolvedQuestions { get; set; }

            [JsonPropertyName("compressed_narrative")]
            public string? CompressedNarrative { get; set; }
        }
    }
}

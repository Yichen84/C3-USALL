using System.Text.Json;
using System.Text.Json.Serialization;

using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public static class CrisisActionJsonParser
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static CrisisActionAnalysisResult Parse(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
                throw new ClearBridgeAnalysisException("EmptyResponse", "The provider returned an empty response.");

            var json = TrimMarkdownFence(rawJson.Trim());

            CrisisActionAnalysisDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<CrisisActionAnalysisDto>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new ClearBridgeAnalysisException("InvalidJson", "The provider did not return valid JSON.", ex);
            }

            if (dto == null)
                throw new ClearBridgeAnalysisException("InvalidJson", "The provider returned JSON that could not be read.");

            return Normalize(dto);
        }

        private static CrisisActionAnalysisResult Normalize(CrisisActionAnalysisDto dto)
        {
            var result = new CrisisActionAnalysisResult
            {
                Title = dto.Title?.Trim() ?? string.Empty,
                Summary = dto.Summary?.Trim() ?? string.Empty,
                Priority = NormalizePriority(dto.Priority),
                ImportantPoints = CleanList(dto.ImportantPoints),
                Actions = dto.Actions?
                    .Where(action => action != null)
                    .Select(action => new ActionItem
                    {
                        Task = action.Task?.Trim() ?? string.Empty,
                        Deadline = action.Deadline?.Trim() ?? string.Empty,
                        Location = action.Location?.Trim() ?? string.Empty,
                        RequiredDocuments = CleanList(action.RequiredDocuments)
                    })
                    .Where(action => !string.IsNullOrWhiteSpace(action.Task))
                    .ToList() ?? new List<ActionItem>(),
                UnclearItems = CleanList(dto.UnclearItems),
                Warnings = CleanList(dto.Warnings),
                SourceEvidence = dto.SourceEvidence?
                    .Where(item => item != null)
                    .Select(item => new SourceEvidenceItem
                    {
                        Claim = item.Claim?.Trim() ?? string.Empty,
                        SourceText = item.SourceText?.Trim() ?? string.Empty
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.Claim) ||
                                   !string.IsNullOrWhiteSpace(item.SourceText))
                    .ToList() ?? new List<SourceEvidenceItem>()
            };

            if (string.IsNullOrWhiteSpace(result.Title))
                result.Title = "ClearBridge Analysis";
            if (string.IsNullOrWhiteSpace(result.Summary))
                result.Summary = "No summary was returned.";

            return result;
        }

        private static string NormalizePriority(string? priority)
        {
            var normalized = priority?.Trim().ToLowerInvariant();
            return normalized is "low" or "medium" or "high" or "urgent"
                ? normalized
                : "medium";
        }

        private static List<string> CleanList(List<string>? values)
        {
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList() ?? new List<string>();
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

        private sealed class CrisisActionAnalysisDto
        {
            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("summary")]
            public string? Summary { get; set; }

            [JsonPropertyName("priority")]
            public string? Priority { get; set; }

            [JsonPropertyName("important_points")]
            public List<string>? ImportantPoints { get; set; }

            [JsonPropertyName("actions")]
            public List<ActionItemDto>? Actions { get; set; }

            [JsonPropertyName("unclear_items")]
            public List<string>? UnclearItems { get; set; }

            [JsonPropertyName("warnings")]
            public List<string>? Warnings { get; set; }

            [JsonPropertyName("source_evidence")]
            public List<SourceEvidenceItemDto>? SourceEvidence { get; set; }
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
    }
}
